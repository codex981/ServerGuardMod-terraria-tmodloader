using System;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using ServerGuardMod.Common.Players;
using ServerGuardMod.Common.Network;

namespace ServerGuardMod.Common.AntiCheat
{
    public class PacketFilter : ModSystem
    {
        private static int[] _packetCount = new int[256];
        private static int _rateTick = 0;
        private const int MAX_PACKETS_PER_SECOND = 600;

        public override void PostUpdateEverything()
        {
            if (Main.netMode != Terraria.ID.NetmodeID.Server) return;

            _rateTick++;
            if (_rateTick >= 60)
            {
                _rateTick = 0;
                Array.Clear(_packetCount, 0, _packetCount.Length);
            }
        }

        public static bool ValidatePacket(int playerIndex, int packetType)
        {
            if (Main.netMode != Terraria.ID.NetmodeID.Server) return true;
            if (playerIndex < 0 || playerIndex >= Main.maxPlayers) return false;

            var player = Main.player[playerIndex];
            if (!player.active) return false;

            // Rate limiting
            _packetCount[playerIndex]++;
            if (_packetCount[playerIndex] > MAX_PACKETS_PER_SECOND)
            {
                ServerGuardMod.Instance.Logger.Warn(
                    $"[PacketFlood] {player.name} sending suspicious packet rate: {_packetCount[playerIndex]}/sec"
                );
                return false;
            }

            var sgPlayer = player.GetModPlayer<SGPlayer>();

            // Block most packets before login
            // Only allow essential connection packets
            if (!sgPlayer.IsLoggedIn)
            {
                // These are the only MessageID values confirmed to exist in tModLoader 1.4.4
                // Reference: https://docs.tmodloader.net/docs/stable/class_message_i_d.html
                bool allowed = false;

                if (packetType == MessageID.SyncPlayer)         allowed = true; // ID 4
                if (packetType == MessageID.SyncEquipment)      allowed = true; // ID 5
                if (packetType == MessageID.RequestWorldData)   allowed = true; // ID 6
                if (packetType == MessageID.SpawnTileData)      allowed = true; // ID 8
                if (packetType == MessageID.PlayerSpawn)        allowed = true; // ID 12
                if (packetType == MessageID.SyncMods)           allowed = true; // ID 251 (tML only)
                if (packetType == MessageID.SocialHandshake)    allowed = true; // ID 93
                if (packetType == MessageID.RequestPassword)    allowed = true; // ID 37
                if (packetType == MessageID.SendPassword)       allowed = true; // ID 38

                if (!allowed)
                    return false;
            }

            return true;
        }

        public static bool ValidateServerGuardPacket(int playerIndex, PacketType packetType)
        {
            if (Main.netMode != NetmodeID.Server) return true;
            if (playerIndex < 0 || playerIndex >= Main.maxPlayers) return false;

            var player = Main.player[playerIndex];
            if (!player.active) return false;

            _packetCount[playerIndex]++;
            if (_packetCount[playerIndex] > MAX_PACKETS_PER_SECOND)
            {
                ServerGuardMod.Instance.Logger.Warn(
                    $"[PacketFlood] {player.name} sending suspicious ServerGuard packet rate: {_packetCount[playerIndex]}/sec"
                );
                return false;
            }

            var sgPlayer = player.GetModPlayer<SGPlayer>();
            if (!sgPlayer.IsLoggedIn &&
                packetType != PacketType.RequestLogin &&
                packetType != PacketType.RequestRegister)
            {
                ServerGuardMod.Instance.Logger.Warn(
                    $"[PacketInjection] {player.name} sent {packetType} before login"
                );
                return false;
            }

            return true;
        }
    }
}
