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
        private bool  _itemNearLastTick   = false;
        private bool  _serverJoinInitialized = false;
        private bool  _trustedInventoryReady = false;
        private int   _loginPromptTick = 0;
        private readonly int[] _trustedTypes    = new int[59];
        private readonly int[] _trustedStacks   = new int[59];
        private readonly int[] _trustedPrefixes = new int[59];
        private Vector2 _frozenPos;
        
        private int   _antiCheatTick = 0;
        private float _lastSafeX     = 0f;
        private float _lastSafeY     = 0f;
        private int   _noClipCount   = 0;
        private int   _autoSaveTick  = 0;
        private const int CHECK_INTERVAL = 60; // every 60 ticks (1 sec) for heavy checks
        private const int STACK_JUMP_TOLERANCE = 5;
        private const long VALUE_JUMP_TOLERANCE = 5_000_000; // 50 gold
        private const long CRAFTING_VALUE_TOLERANCE = 500_000; // 5 gold wiggle room for recipes/mods

        // ----------------------------------------------------------------
        // When ANY player enters the world (runs on BOTH server and client)
        // ----------------------------------------------------------------
        public override void OnEnterWorld()
        {
            _frozenPos = Player.position;

            // --- CLIENT SIDE ---
            if (Main.netMode != NetmodeID.Server)
            {
                Main.NewText("Welcome! Please wait while ServerGuard synchronizes your data...", Color.Yellow);
                Main.NewText("[WARNING] This server uses Server-Side Characters (SSC).", Color.Red);
                Main.NewText("Please create a NEW empty character for this server to avoid losing your local items!", Color.Red);
            }

            // --- SERVER SIDE ---
            if (Main.netMode == NetmodeID.Server)
            {
                IsLoggedIn    = false;
                SnapshotReady = false;
                _serverJoinInitialized = true;
                _trustedInventoryReady = false;

                // Hard-block the player immediately
                BlockPlayer();

                // Tell the client to show the login UI
                SendLoginRequired();

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
            if (Main.netMode == NetmodeID.Server)
                InitializeServerSessionIfNeeded();

            // Absolute Freeze: Lock position and velocity to stop SpeedHacks & Calamity
            if (IsFrozen || (Main.netMode == NetmodeID.Server && !IsLoggedIn))
            {
                Player.position = _frozenPos;
                Player.velocity = Vector2.Zero;
            }
            else if (IsLoggedIn)
            {
                // Update frozen pos to current safe pos when playing normally
                _frozenPos = Player.position;
            }

            // ---- BLOCK BEFORE LOGIN (server only) ----
            if (Main.netMode == NetmodeID.Server && !IsLoggedIn)
            {
                BlockPlayer();
                _loginPromptTick++;
                if (_loginPromptTick >= 600)
                {
                    _loginPromptTick = 0;
                    SendLoginRequired();
                }
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
            if (Main.netMode != NetmodeID.SinglePlayer) 
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

        private void InitializeServerSessionIfNeeded()
        {
            if (_serverJoinInitialized)
                return;

            _serverJoinInitialized = true;
            _trustedInventoryReady = false;
            IsLoggedIn = false;
            IsFrozen = false;
            IsGodMode = false;
            Username = "";
            IsAdmin = false;
            SnapshotReady = false;
            _frozenPos = Player.position;
            _loginPromptTick = 0;

            BlockPlayer();
            SendLoginRequired();

            ServerGuardMod.Instance.Logger.Info(
                $"[SGPlayer] {Player.name} connected - awaiting login"
            );
        }

        private void SendLoginRequired()
        {
            var pkt = ServerGuardMod.CreatePacket(PacketType.LoginRequired);
            pkt.Send(Player.whoAmI);
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
            Player.aggro          = -999999; // Make monsters completely ignore them

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
            CaptureTrustedInventory();
        }

        public void TrustCurrentServerState(string reason = "")
        {
            TakeSnapshot();
            SnapshotReady = true;
            AccountDatabase.UpdatePlayerDataInMemory(Player, Username);
        }

        private long GetInventoryValue()
        {
            long value = 0;
            for (int i = 0; i < 59; i++)
            {
                var item = Player.inventory[i];
                if (item != null && item.type > ItemID.None)
                    value += (long)item.value * item.stack;
            }
            return value;
        }

        private void CaptureTrustedInventory()
        {
            for (int i = 0; i < 59; i++)
            {
                var item = Player.inventory[i];
                if (item == null || item.type <= ItemID.None || item.stack <= 0)
                {
                    _trustedTypes[i] = 0;
                    _trustedStacks[i] = 0;
                    _trustedPrefixes[i] = 0;
                    continue;
                }

                _trustedTypes[i] = item.type;
                _trustedStacks[i] = item.stack;
                _trustedPrefixes[i] = item.prefix;
            }

            _trustedInventoryReady = true;
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
            if (!_trustedInventoryReady)
            {
                CaptureTrustedInventory();
                _lastInventoryValue = GetInventoryValue();
                return;
            }

            long currentValue = GetInventoryValue();
            long diff = currentValue - _lastInventoryValue;

            bool isInteracting = (Player.chest != -1 || Player.talkNPC != -1);
            bool hasGameplayContext = isInteracting || _itemNearLastTick || Player.itemAnimation > 0 || Player.itemTime > 0;

            long gainedValue = 0;
            long lostValue = 0;
            int largestStackJump = 0;
            string largestStackDetails = "";

            for (int i = 0; i < 59; i++)
            {
                var item = Player.inventory[i];
                bool hasCurrentItem = item != null && item.type > ItemID.None && item.stack > 0;
                int currentType = hasCurrentItem ? item!.type : ItemID.None;
                int currentStack = hasCurrentItem ? item!.stack : 0;
                int currentPrefix = hasCurrentItem ? item!.prefix : 0;
                long currentSlotValue = hasCurrentItem ? (long)item!.value * currentStack : 0;

                int oldType = _trustedTypes[i];
                int oldStack = _trustedStacks[i];
                int oldPrefix = _trustedPrefixes[i];
                long oldSlotValue = GetTrustedSlotValue(oldType, oldStack, oldPrefix);

                if (oldType == currentType && oldPrefix == currentPrefix)
                {
                    int stackDiff = currentStack - oldStack;
                    if (stackDiff > 0)
                    {
                        gainedValue += Math.Max(0, currentSlotValue - oldSlotValue);
                        if (stackDiff > largestStackJump)
                        {
                            largestStackJump = stackDiff;
                            largestStackDetails = $"slot {i}: type {currentType}, stack {oldStack} -> {currentStack}";
                        }
                    }
                    else if (stackDiff < 0)
                    {
                        lostValue += Math.Max(0, oldSlotValue - currentSlotValue);
                    }
                }
                else
                {
                    gainedValue += currentSlotValue;
                    lostValue += oldSlotValue;
                    if (currentType != ItemID.None && currentStack > largestStackJump)
                    {
                        largestStackJump = currentStack;
                        largestStackDetails = $"slot {i}: type {oldType} -> {currentType}, stack {currentStack}";
                    }
                }
            }

            bool plausibleCraftingOrSwap = lostValue > 0 && gainedValue <= lostValue + CRAFTING_VALUE_TOLERANCE;
            bool suspiciousValueJump = diff > VALUE_JUMP_TOLERANCE || gainedValue > lostValue + VALUE_JUMP_TOLERANCE;
            bool suspiciousStackJump = largestStackJump > STACK_JUMP_TOLERANCE;

            if (!hasGameplayContext && !plausibleCraftingOrSwap && (suspiciousValueJump || suspiciousStackJump))
            {
                string reason = suspiciousStackJump
                    ? $"Stack jump without gameplay context ({largestStackDetails})"
                    : $"Inventory value jumped by {diff} copper without gameplay context";

                Flag("MEMORY_HACK_INVENTORY", reason);
                RestoreServerTruth();
                return;
            }

            AccountDatabase.UpdatePlayerDataInMemory(Player, Username);
            CaptureTrustedInventory();
            _lastInventoryValue = currentValue;
        }

        private long GetTrustedSlotValue(int type, int stack, int prefix)
        {
            if (type <= ItemID.None || stack <= 0)
                return 0;

            var item = new Item();
            item.SetDefaults(type);
            item.prefix = (byte)Math.Clamp(prefix, byte.MinValue, byte.MaxValue);
            return (long)item.value * stack;
        }

        private void RestoreServerTruth()
        {
            var account = AccountDatabase.GetAccount(Username);
            if (account != null)
            {
                AccountDatabase.ApplyDataToPlayer(Player, account);
                AccountDatabase.SendPlayerData(Player.whoAmI, account);
                TrustCurrentServerState("restore");
            }

            NetMessage.SendData(MessageID.ChatText, Player.whoAmI, -1,
                Terraria.Localization.NetworkText.FromLiteral("Cheat Detected: inventory restored to the server version."),
                255, 255, 0, 0);
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
