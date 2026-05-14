using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Microsoft.Xna.Framework;
using ServerGuardMod.Common.Systems;
using ServerGuardMod.Common.Players;
using ServerGuardMod.Common.AntiCheat;

namespace ServerGuardMod.Common.Network
{
    public static class PacketHandler
    {
        public static void Handle(BinaryReader reader, int whoAmI)
        {
            PacketType type = (PacketType)reader.ReadByte();

            if (Main.netMode == NetmodeID.Server && !PacketFilter.ValidateServerGuardPacket(whoAmI, type))
                return;

            switch (type)
            {
                // ========================================================
                // CLIENT -> SERVER
                // ========================================================

                case PacketType.RequestLogin:
                    if (Main.netMode == NetmodeID.Server)
                        HandleLogin(reader, whoAmI);
                    break;

                case PacketType.RequestRegister:
                    if (Main.netMode == NetmodeID.Server)
                        HandleRegister(reader, whoAmI);
                    break;

                // ========================================================
                // SERVER -> CLIENT
                // ========================================================

                case PacketType.LoginRequired:
                    if (Main.netMode == NetmodeID.MultiplayerClient)
                        ClientLoginSystem.ShowLoginUI();
                    break;

                case PacketType.LoginSuccess:
                    if (Main.netMode == NetmodeID.MultiplayerClient)
                        ClientLoginSystem.OnLoginSuccess(reader);
                    break;

                case PacketType.LoginFail:
                    if (Main.netMode == NetmodeID.MultiplayerClient)
                        ClientLoginSystem.OnLoginFail(reader);
                    break;

                case PacketType.SyncPlayerData:
                    if (Main.netMode == NetmodeID.MultiplayerClient)
                        ClientLoginSystem.ApplyServerData(reader);
                    break;

                case PacketType.Kick:
                    if (Main.netMode == NetmodeID.MultiplayerClient)
                    {
                        string reason = reader.ReadString();
                        Main.NewText($"[ServerGuard] Kicked: {reason}", Color.Red);
                        Netplay.Disconnect = true;
                    }
                    break;

                case PacketType.FreezePlayer:
                    if (Main.netMode == NetmodeID.MultiplayerClient)
                    {
                        bool frozen = reader.ReadBoolean();
                        Main.LocalPlayer.GetModPlayer<SGPlayer>().IsFrozen = frozen;
                    }
                    break;

                case PacketType.AdminMessage:
                    if (Main.netMode == NetmodeID.MultiplayerClient)
                    {
                        string msg = reader.ReadString();
                        Main.NewText($"[Admin] {msg}", Color.Yellow);
                    }
                    break;
            }
        }

        // ================================================================
        // Handle login request on the server
        // ================================================================
        private static void HandleLogin(BinaryReader reader, int whoAmI)
        {
            string username = reader.ReadString();
            string password = reader.ReadString();

            var player   = Main.player[whoAmI];
            var sgPlayer = player.GetModPlayer<SGPlayer>();

            // Already logged in - ignore duplicate requests
            if (sgPlayer.IsLoggedIn)
                return;

            // Check ban first (before verifying password)
            var account = AccountDatabase.GetAccount(username);
            if (account != null && account.IsBanned)
            {
                SendFail(whoAmI, $"You are banned: {account.BanReason}");
                // Kick immediately
                NetMessage.SendData(MessageID.Kick, whoAmI, -1,
                    Terraria.Localization.NetworkText.FromLiteral($"Banned: {account.BanReason}"));
                return;
            }

            bool ok = AccountDatabase.TryLogin(username, password, out var savedData);

            if (ok && savedData != null)
            {
                // Store IP
                savedData.LastIP = Netplay.Clients[whoAmI].Socket.GetRemoteAddress().ToString();

                sgPlayer.IsLoggedIn = true;
                sgPlayer.Username   = username;
                sgPlayer.IsAdmin    = savedData.IsAdmin;

                // Apply server data to the player object on the server
                AccountDatabase.ApplyDataToPlayer(player, savedData);
                sgPlayer.TrustCurrentServerState("login");

                // Send success + data to client
                var pkt = ServerGuardMod.CreatePacket(PacketType.LoginSuccess);
                pkt.Write(username);
                AccountDatabase.WritePlayerData(pkt, savedData);
                pkt.Send(whoAmI);

                ServerGuardMod.Instance.Logger.Info(
                    $"[Login] {username} logged in (IP: {savedData.LastIP})"
                );
                AccountDatabase.Save();
                Main.NewText($"[ServerGuard] {username} joined the server", Color.Green);
            }
            else
            {
                SendFail(whoAmI, "Wrong username or password");
            }
        }

        // ================================================================
        // Handle register request on the server
        // ================================================================
        private static void HandleRegister(BinaryReader reader, int whoAmI)
        {
            string username = reader.ReadString();
            string password = reader.ReadString();

            // Validate Username (English letters and numbers only)
            if (!System.Text.RegularExpressions.Regex.IsMatch(username, @"^[a-zA-Z0-9_]+$"))
            {
                SendFail(whoAmI, "Username must be English letters/numbers only");
                return;
            }

            if (username.Length < 3 || password.Length < 3)
            {
                SendFail(whoAmI, "Username and password must be at least 3 chars");
                return;
            }

            var player   = Main.player[whoAmI];
            var sgPlayer = player.GetModPlayer<SGPlayer>();

            if (sgPlayer.IsLoggedIn)
                return;

            if (AccountDatabase.AccountExists(username))
            {
                SendFail(whoAmI, "That username is already taken");
                return;
            }

            if (username.Length < 3 || password.Length < 4)
            {
                SendFail(whoAmI, "Username must be 3+ chars, password 4+ chars");
                return;
            }

            AccountDatabase.CreateAccount(username, password, player);

            sgPlayer.IsLoggedIn = true;
            sgPlayer.Username   = username;
            sgPlayer.IsAdmin    = false;

            var newData = AccountDatabase.GetAccount(username);
            if (newData == null) { SendFail(whoAmI, "Internal error, try again"); return; }

            AccountDatabase.ApplyDataToPlayer(player, newData);
            sgPlayer.TrustCurrentServerState("register");

            var pkt = ServerGuardMod.CreatePacket(PacketType.LoginSuccess);
            pkt.Write(username);
            AccountDatabase.WritePlayerData(pkt, newData);
            pkt.Send(whoAmI);

            ServerGuardMod.Instance.Logger.Info($"[Register] New account: {username}");
            Main.NewText($"[ServerGuard] {username} created an account", Color.Cyan);
        }

        // ================================================================
        // Helper - send a LoginFail packet with a message
        // ================================================================
        private static void SendFail(int whoAmI, string reason)
        {
            var pkt = ServerGuardMod.CreatePacket(PacketType.LoginFail);
            pkt.Write(reason);
            pkt.Send(whoAmI);
        }
    }
}
