using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using ServerGuardMod.Common.Players;

namespace ServerGuardMod.Common.AntiCheat
{
    public class MemoryWatcher : ModSystem
    {
        private static Dictionary<int, PlayerSnapshot> _snapshots = new();
        private int _checkTick = 0;
        private const int CHECK_EVERY = 20;

        public override void PostUpdateEverything()
        {
            if (Main.netMode != Terraria.ID.NetmodeID.Server) return;

            _checkTick++;
            if (_checkTick < CHECK_EVERY) return;
            _checkTick = 0;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                var player = Main.player[i];
                if (!player.active) continue;

                var sgPlayer = player.GetModPlayer<SGPlayer>();
                if (!sgPlayer.IsLoggedIn) continue;

                CheckPlayerIntegrity(player, sgPlayer, i);
            }
        }

        private void CheckPlayerIntegrity(Player player, SGPlayer sgPlayer, int whoAmI)
        {
            var current = new PlayerSnapshot(player);

            if (!_snapshots.TryGetValue(whoAmI, out var previous))
            {
                _snapshots[whoAmI] = current;
                return;
            }

            // --- HP sudden jump ---
            int hpGain = current.Life - previous.Life;
            if (hpGain > 50 && !player.potionDelay.Equals(0))
            {
                player.statLife = sgPlayer.ServerSideHP;
                TriggerViolation(player, sgPlayer, "HP_CHEAT",
                    $"HP jumped {previous.Life} -> {current.Life} (+{hpGain})");
            }

            // --- Mana sudden jump ---
            int manaGain = current.Mana - previous.Mana;
            if (manaGain > 200)
            {
                player.statMana = sgPlayer.ServerSideMana;
                TriggerViolation(player, sgPlayer, "MANA_CHEAT",
                    $"Mana jumped {previous.Mana} -> {current.Mana} (+{manaGain})");
            }

            // --- Buff overflow ---
            if (current.BuffCount > 22)
            {
                for (int b = 22; b < Player.MaxBuffs; b++)
                    player.buffType[b] = 0;
                TriggerViolation(player, sgPlayer, "BUFF_EXPLOIT",
                    $"Buff count: {current.BuffCount} (max: 22)");
            }

            // --- Inventory value sudden explosion (duplication) ---
            long valueDiff = current.InventoryValue - previous.InventoryValue;
            if (valueDiff > 10_000_000 && previous.InventoryValue > 0)
            {
                TriggerViolation(player, sgPlayer, "INVENTORY_DUPE",
                    $"Inventory value spiked: {previous.InventoryValue} -> {current.InventoryValue}");
            }

            _snapshots[whoAmI]       = current;
            sgPlayer.ServerSideHP    = player.statLife;
            sgPlayer.ServerSideMana  = player.statMana;
        }

        private static void TriggerViolation(Player player, SGPlayer sgPlayer,
                                              string type, string details)
        {
            string msg = $"[ANTICHEAT] {type} | Player:{sgPlayer.Username} | {details}";

            ServerGuardMod.Instance.Logger.Warn(msg);

            string path = System.IO.Path.Combine(Main.SavePath, "ServerGuard", "cheat_log.txt");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            System.IO.File.AppendAllText(path,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n");

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                var p = Main.player[i];
                if (!p.active) continue;
                if (!p.GetModPlayer<SGPlayer>().IsAdmin) continue;

                var pkt = ServerGuardMod.CreatePacket(Network.PacketType.AdminMessage);
                pkt.Write(msg);
                pkt.Send(i);
            }
        }

        public override void PostUpdatePlayers()
        {
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                if (!Main.player[i].active && _snapshots.ContainsKey(i))
                    _snapshots.Remove(i);
            }
        }
    }

    public class PlayerSnapshot
    {
        public int  Life           { get; }
        public int  Mana           { get; }
        public int  BuffCount      { get; }
        public long InventoryValue { get; }

        public PlayerSnapshot(Player player)
        {
            Life = player.statLife;
            Mana = player.statMana;

            int bc = 0;
            for (int i = 0; i < Player.MaxBuffs; i++)
                if (player.buffType[i] > 0) bc++;
            BuffCount = bc;

            long iv = 0;
            for (int i = 0; i < 59; i++)
            {
                var item = player.inventory[i];
                if (item.type > 0)
                    iv += (long)item.value * item.stack;
            }
            InventoryValue = iv;
        }
    }
}
