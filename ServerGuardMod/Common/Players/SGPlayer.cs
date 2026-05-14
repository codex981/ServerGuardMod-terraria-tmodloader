using System;
using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Microsoft.Xna.Framework;
using ServerGuardMod.Common.Systems;
using ServerGuardMod.Common.Network;

namespace ServerGuardMod.Common.Players
{
    public class SGPlayer : ModPlayer
    {
        // ----------------------------------------------------------------
        // State
        // ----------------------------------------------------------------
        public bool   IsLoggedIn     { get; set; } = false;
        public string Username       { get; set; } = "";
        public bool   IsAdmin        { get; set; } = false;
        public bool   IsFrozen       { get; set; } = false;
        public bool   IsGodMode      { get; set; } = false; // Added true GodMode

        // ----------------------------------------------------------------
        // Server-side reference values for anti-cheat
        // ----------------------------------------------------------------
        public int ServerSideHP      { get; set; } = 100;
        public int ServerSideHPMax   { get; set; } = 100;
        public int ServerSideMana    { get; set; } = 20;
        public int ServerSideManaMax { get; set; } = 20;

        // Whether server has already snapshotted this player for the first time
        public bool SnapshotReady    { get; set; } = false;

        private long  _lastInventoryValue = 0;
        private int[] _lastStacks         = new int[59];
        private bool  _itemNearLastTick   = false;
        
        private int   _antiCheatTick = 0;
        private float _lastSafeX     = 0f;
        private float _lastSafeY     = 0f;
        private int   _noClipCount   = 0;
        private int   _autoSaveTick  = 0;
        private const int CHECK_INTERVAL = 60; // every 60 ticks (1 sec) for heavy checks

        // ----------------------------------------------------------------
        // When ANY player enters the world (runs on BOTH server and client)
        // ----------------------------------------------------------------
        public override void OnEnterWorld()
        {
            // --- SERVER SIDE ---
            if (Main.netMode == NetmodeID.Server)
            {
                IsLoggedIn    = false;
                SnapshotReady = false;

                // Hard-block the player immediately
                BlockPlayer();

                // Tell the client to show the login UI
                var pkt = ServerGuardMod.CreatePacket(PacketType.LoginRequired);
                pkt.Send(Player.whoAmI);

                ServerGuardMod.Instance.Logger.Info(
                    $"[SGPlayer] {Player.name} connected - awaiting login"
                );
            }

            // --- CLIENT SIDE (singleplayer or multiplayer client) ---
            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                // In single player there is no server; bypass login
                IsLoggedIn = true;
                Username   = Player.name;
            }
            else if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                // Reset client state when entering a multiplayer world
                ClientLoginSystem.Reset();
            }
        }

        // ----------------------------------------------------------------
        // Block Damage if GodMode, Frozen, or Not Logged In
        // ----------------------------------------------------------------
        public override bool FreeDodge(Player.HurtInfo info)
        {
            // Note: Main.netMode check is removed so this works on client too!
            if (IsGodMode || IsFrozen || !IsLoggedIn)
            {
                return true; // Dodge all damage completely
            }
            return base.FreeDodge(info);
        }

        public override bool CanBeHitByNPC(NPC npc, ref int cooldownSlot) => IsLoggedIn && !IsFrozen;
        public override bool CanBeHitByProjectile(Projectile proj) => IsLoggedIn && !IsFrozen;
        public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers) 
        {
            if (!IsLoggedIn || IsFrozen || IsGodMode) modifiers.SetMaxDamage(0); // Extra safety
        }

        // ----------------------------------------------------------------
        // Prevent Using Items completely when frozen/not logged in
        // ----------------------------------------------------------------
        public override bool CanUseItem(Item item)
        {
            if (IsFrozen || !IsLoggedIn) return false;
            return base.CanUseItem(item);
        }

        // ----------------------------------------------------------------
        // Stop all movement completely
        // ----------------------------------------------------------------
        public override void SetControls()
        {
            if (IsFrozen || !IsLoggedIn)
            {
                Player.controlLeft  = false;
                Player.controlRight = false;
                Player.controlUp    = false;
                Player.controlDown  = false;
                Player.controlJump  = false;
                Player.controlDownHold = false;
                
                // Allow controlInv so they can open ESC menu to exit
                // Player.controlInv   = false;   
                
                Player.controlHook  = false;   // Prevent grappling
                Player.controlMount = false;
                Player.controlThrow = false;
                Player.controlUseItem = false;
                Player.controlUseTile = false;
                Player.controlQuickHeal = false;
                Player.controlQuickMana = false;
            }
        }

        // ----------------------------------------------------------------
        // Runs every tick
        // ----------------------------------------------------------------
        public override void PreUpdate()
        {
            // ---- FREEZE (admin command) ----
            if (IsFrozen)
            {
                Player.velocity      = Vector2.Zero;
                Player.immune        = true;
                Player.immuneNoBlink = true;
            }

            // ---- BLOCK BEFORE LOGIN (server only) ----
            if (Main.netMode == NetmodeID.Server && !IsLoggedIn)
            {
                BlockPlayer();
                return;
            }

            // ---- ANTI-CHEAT CHECKS (server only, logged-in players) ----
            if (Main.netMode == NetmodeID.Server && IsLoggedIn)
            {
                _antiCheatTick++;
                if (_antiCheatTick >= CHECK_INTERVAL)
                {
                    _antiCheatTick = 0;
                    RunAntiCheatChecks();
                }
            }
        }

        // ----------------------------------------------------------------
        // Called every tick AFTER update
        // ----------------------------------------------------------------
        public override void PostUpdate()
        {
            // Track if any items are near the player for heuristic anti-cheat
            _itemNearLastTick = false;
            if (Main.netMode != NetmodeID.Server) 
            {
                for(int i = 0; i < Main.maxItems; i++) {
                    if (Main.item[i].active && Player.DistanceSQ(Main.item[i].Center) < 40000) { // ~200 pixels
                        _itemNearLastTick = true;
                        break;
                    }
                }
            }

            if (Main.netMode != NetmodeID.Server) return;
            if (!IsLoggedIn) return;
            {
                _autoSaveTick++;
                if (_autoSaveTick >= 1800) // 30 seconds
                {
                    _autoSaveTick = 0;
                    AccountDatabase.SavePlayerData(Player, Username);
                }
            }
        }

        // ----------------------------------------------------------------
        // On disconnect - save data
        // ----------------------------------------------------------------
        public override void PlayerDisconnect()
        {
            if (Main.netMode == NetmodeID.Server && IsLoggedIn && Username != "")
            {
                AccountDatabase.SavePlayerData(Player, Username);
                ServerGuardMod.Instance.Logger.Info($"[Save] Saved data for {Username}");
            }
        }

        // ================================================================
        // BLOCK PLAYER - call every tick while not logged in
        // ================================================================
        private void BlockPlayer()
        {
            Player.velocity       = Vector2.Zero;
            Player.immune         = true;
            Player.immuneNoBlink  = true;
            Player.noBuilding     = true;

            // Make them invisible (ghost-like)
            Player.invis          = true;

            // Add frozen buff just to be extra safe visually
            Player.AddBuff(BuffID.Frozen, 2);

            // Keep HP full so they don't die while waiting
            Player.statLife       = Player.statLifeMax;
        }

        // ================================================================
        // ANTI-CHEAT - runs every CHECK_INTERVAL ticks on the server
        // ================================================================
        private void RunAntiCheatChecks()
        {
            // Take a fresh snapshot of server-trusted values right after login
            if (!SnapshotReady)
            {
                TakeSnapshot();
                SnapshotReady = true;
                return;
            }

            CheckHpHack();
            CheckManaHack();
            CheckMaxHpHack();
            CheckNoClip();
            CheckSpeedHack();
            CheckMemoryEditing();
        }

        private void TakeSnapshot()
        {
            ServerSideHP      = Player.statLife;
            ServerSideHPMax   = Player.statLifeMax;
            ServerSideMana    = Player.statMana;
            ServerSideManaMax = Player.statManaMax;
            _lastSafeX        = Player.position.X;
            _lastSafeY        = Player.position.Y;
            _lastInventoryValue = GetInventoryValue();
            
            for (int i = 0; i < 59; i++)
            {
                _lastStacks[i] = Player.inventory[i].stack;
            }
        }

        private long GetInventoryValue()
        {
            long value = 0;
            for (int i = 0; i < 59; i++)
            {
                var item = Player.inventory[i];
                if (item.type > 0)
                    value += (long)item.value * item.stack;
            }
            return value;
        }

        // ----------------------------------------------------------------
        // HP hack: sudden jump without healing source
        // ----------------------------------------------------------------
        private void CheckHpHack()
        {
            int diff = Player.statLife - ServerSideHP;

            // Allow gradual regen (small positive gains are fine)
            // Flag big jumps that can't come from regen or potions
            if (diff > 100 && Player.potionDelay == 0 && Player.lifeRegen <= 0)
            {
                Flag("HP_HACK", $"HP jumped {ServerSideHP} -> {Player.statLife} (+{diff})");
                Player.statLife = ServerSideHP; // reset to trusted value
            }
            else
            {
                // Accept the new value as trusted
                ServerSideHP = Player.statLife;
            }
        }

        // ----------------------------------------------------------------
        // Max HP hack: statLifeMax raised without crystal
        // ----------------------------------------------------------------
        private void CheckMaxHpHack()
        {
            // Allow raises of up to 20 per check (life crystal = +20)
            if (Player.statLifeMax > ServerSideHPMax + 20)
            {
                Flag("MAXHP_HACK", $"MaxHP {ServerSideHPMax} -> {Player.statLifeMax}");
                Player.statLifeMax = ServerSideHPMax;
            }
            else
            {
                ServerSideHPMax = Player.statLifeMax;
            }
        }

        // ----------------------------------------------------------------
        // Mana hack
        // ----------------------------------------------------------------
        private void CheckManaHack()
        {
            int diff = Player.statMana - ServerSideMana;
            if (diff > 200)
            {
                Flag("MANA_HACK", $"Mana jumped {ServerSideMana} -> {Player.statMana} (+{diff})");
                Player.statMana = ServerSideMana;
            }
            else
            {
                ServerSideMana = Player.statMana;
            }
        }

        // ----------------------------------------------------------------
        // NoClip: player inside a solid tile repeatedly
        // ----------------------------------------------------------------
        private void CheckNoClip()
        {
            int tx = (int)(Player.Center.X / 16);
            int ty = (int)(Player.Center.Y / 16);

            if (tx > 0 && ty > 0 && tx < Main.maxTilesX && ty < Main.maxTilesY)
            {
                var tile = Main.tile[tx, ty];
                if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                {
                    _noClipCount++;
                    if (_noClipCount >= 3)
                    {
                        _noClipCount = 0;
                        Flag("NOCLIP", $"Inside solid tile {tx},{ty}");
                        Player.Teleport(new Vector2(_lastSafeX, _lastSafeY));
                    }
                    return;
                }
            }

            _noClipCount = 0;
            _lastSafeX   = Player.position.X;
            _lastSafeY   = Player.position.Y;
        }

        // ----------------------------------------------------------------
        // Speed hack: moving faster than max allowed
        // ----------------------------------------------------------------
        private void CheckSpeedHack()
        {
            // Max legitimate speed with buffs/mounts is roughly 60 px/tick
            float speed = Player.velocity.Length();
            if (speed > 65f && !Player.mount.Active)
            {
                Flag("SPEED_HACK", $"Speed {speed:F1} px/tick");
                Player.velocity = Vector2.Zero;
            }
        }

        // ----------------------------------------------------------------
        // Heuristic Anti-Cheat (Cheat Engine / Memory Editing)
        // ----------------------------------------------------------------
        private void CheckMemoryEditing()
        {
            long currentValue = GetInventoryValue();
            long diff = currentValue - _lastInventoryValue;

            bool isInteracting = (Player.chest != -1 || Player.talkNPC != -1);

            // Check 1: Value Jump (if value jumped by 1 Gold Coin = 1_000_000 copper)
            if (diff > 1_000_000 && !isInteracting && !_itemNearLastTick)
            {
                Flag("MEMORY_HACK_VALUE", $"Inventory value jumped by {diff} copper without shop/pickup! Possible Cheat Engine.");
                NetMessage.SendData(MessageID.Kick, Player.whoAmI, -1,
                    Terraria.Localization.NetworkText.FromLiteral("Cheat Detected: Memory Editing (Value Jump)"));
            }

            // Check 2: Stack Jump (for low value items like 5 dirt -> 55 dirt)
            for (int i = 0; i < 59; i++)
            {
                int currentStack = Player.inventory[i].stack;
                int stackDiff = currentStack - _lastStacks[i];
                
                // If a single stack jumped by more than 5, and it wasn't looted from chest or ground
                if (stackDiff > 5 && !isInteracting && !_itemNearLastTick)
                {
                    Flag("MEMORY_HACK_STACK", $"Slot {i} stack jumped from {_lastStacks[i]} to {currentStack} without shop/pickup!");
                    NetMessage.SendData(MessageID.Kick, Player.whoAmI, -1,
                        Terraria.Localization.NetworkText.FromLiteral("Cheat Detected: Memory Editing (Stack Injection)"));
                    break;
                }
                _lastStacks[i] = currentStack;
            }

            // Always update to current so legitimate jumps become the new baseline
            _lastInventoryValue = currentValue;
        }

        // ================================================================
        // FLAG - log, alert admins, write cheat_log.txt
        // ================================================================
        private void Flag(string type, string details)
        {
            string msg = $"[ANTICHEAT] {type} | {Username} | {details}";

            // Server log
            ServerGuardMod.Instance.Logger.Warn(msg);

            // Write cheat log file
            try
            {
                string dir  = Path.Combine(Main.SavePath, "ServerGuard");
                string file = Path.Combine(dir, "cheat_log.txt");
                Directory.CreateDirectory(dir);
                File.AppendAllText(file,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n");
            }
            catch { /* never crash the game over logging */ }

            // Alert every online admin
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                var p = Main.player[i];
                if (!p.active) continue;
                if (!p.GetModPlayer<SGPlayer>().IsAdmin) continue;

                var pkt = ServerGuardMod.CreatePacket(PacketType.AdminMessage);
                pkt.Write($"[AC] {msg}");
                pkt.Send(i);
            }
        }
    }
}
