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

        // ----------------------------------------------------------------
        // Server-side reference values for anti-cheat
        // ----------------------------------------------------------------
        public int ServerSideHP      { get; set; } = 100;
        public int ServerSideHPMax   { get; set; } = 100;
        public int ServerSideMana    { get; set; } = 20;
        public int ServerSideManaMax { get; set; } = 20;

        // Whether server has already snapshotted this player for the first time
        public bool SnapshotReady    { get; set; } = false;

        private int   _antiCheatTick = 0;
        private float _lastSafeX     = 0f;
        private float _lastSafeY     = 0f;
        private int   _noClipCount   = 0;
        private int   _autoSaveTick  = 0;
        private const int CHECK_INTERVAL = 20; // every 20 ticks (~3x/sec)

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
            if (Main.netMode == NetmodeID.Server && IsLoggedIn)
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

            // Keep HP full so they don't die while waiting
            Player.statLife       = Player.statLifeMax;

            // Wipe inventory so nothing smuggled in is kept
            for (int i = 0; i < Player.inventory.Length; i++)
                Player.inventory[i] = new Item();

            // Wipe armor and accessories slots too
            for (int i = 0; i < Player.armor.Length; i++)
                Player.armor[i] = new Item();
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
            CheckInventoryHack();
        }

        private void TakeSnapshot()
        {
            ServerSideHP      = Player.statLife;
            ServerSideHPMax   = Player.statLifeMax;
            ServerSideMana    = Player.statMana;
            ServerSideManaMax = Player.statManaMax;
            _lastSafeX        = Player.position.X;
            _lastSafeY        = Player.position.Y;
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
        // Inventory injection: item not recorded on server
        // ----------------------------------------------------------------
        private void CheckInventoryHack()
        {
            var account = AccountDatabase.GetAccount(Username);
            if (account == null) return;

            for (int i = 0; i < 59; i++)
            {
                var cur        = Player.inventory[i];
                int savedID    = account.InventoryIDs[i];
                int savedStack = account.InventoryStacks[i];

                // Completely foreign item (server has no record of it)
                if (cur.type != 0 && cur.type != savedID && savedID == 0)
                {
                    Player.inventory[i] = new Item();
                    Flag("ITEM_INJECT", $"Slot {i}: unknown item ID {cur.type} removed");
                    continue;
                }

                // Stack grew impossibly fast
                if (cur.type == savedID && savedID != 0 && cur.stack > savedStack + 10)
                {
                    Player.inventory[i].stack = savedStack;
                    Flag("STACK_HACK", $"Slot {i}: stack {savedStack}->{cur.stack}");
                }
            }
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
