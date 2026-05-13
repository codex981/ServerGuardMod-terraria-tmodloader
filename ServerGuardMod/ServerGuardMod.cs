using Terraria;
using Terraria.ModLoader;
using ServerGuardMod.Common.Network;

namespace ServerGuardMod
{
    public class ServerGuardMod : Mod
    {
        public static ServerGuardMod Instance;

        public static ModPacket CreatePacket(PacketType type)
        {
            var packet = Instance.GetPacket();
            packet.Write((byte)type);
            return packet;
        }

        public override void Load()
        {
            Instance = this;
            Logger.Info("=== ServerGuard Mod loaded successfully ===");
        }

        public override void Unload()
        {
            Instance = null;
        }

        public override void HandlePacket(System.IO.BinaryReader reader, int whoAmI)
        {
            PacketHandler.Handle(reader, whoAmI);
        }
    }
}
