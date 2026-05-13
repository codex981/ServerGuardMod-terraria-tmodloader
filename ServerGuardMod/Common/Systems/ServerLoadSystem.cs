using System.IO;
using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace ServerGuardMod.Common.Systems
{
    public class ServerLoadSystem : ModSystem
    {
        public override void OnWorldLoad()
        {
            if (Main.netMode == Terraria.ID.NetmodeID.Server ||
                Main.netMode == Terraria.ID.NetmodeID.SinglePlayer)
            {
                AccountDatabase.Load();

                string logDir = Path.Combine(Main.SavePath, "ServerGuard");
                Directory.CreateDirectory(logDir);

                ServerGuardMod.Instance.Logger.Info("[ServerGuard] Loaded successfully");
                ServerGuardMod.Instance.Logger.Info($"[ServerGuard] Data folder: {logDir}");

                // Print to in-game chat
                Main.NewText("[ServerGuard] Protection ACTIVE", Color.Lime);
                Main.NewText($"[ServerGuard] Data: {logDir}", Color.Gray);
            }
        }

        public override void OnWorldUnload()
        {
            if (Main.netMode == Terraria.ID.NetmodeID.Server ||
                Main.netMode == Terraria.ID.NetmodeID.SinglePlayer)
            {
                // Save every logged-in player
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    var player = Main.player[i];
                    if (!player.active) continue;
                    var sg = player.GetModPlayer<Common.Players.SGPlayer>();
                    if (sg.IsLoggedIn && sg.Username != "")
                        AccountDatabase.SavePlayerData(player, sg.Username);
                }

                AccountDatabase.Save();
                ServerGuardMod.Instance.Logger.Info("[ServerGuard] All data saved on world unload");
            }
        }
    }
}
