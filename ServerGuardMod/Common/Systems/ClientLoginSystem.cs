using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Microsoft.Xna.Framework;
using ServerGuardMod.Common.Network;
using ServerGuardMod.Common.Players;

namespace ServerGuardMod.Common.Systems
{
    public static class ClientLoginSystem
    {
        // UI state - used by LoginUI to know what to draw
        public static bool ShowingLogin    { get; set; } = false;
        public static bool ShowingRegister { get; set; } = false;
        public static bool IsLoggedIn      { get; set; } = false;

        // Text field values
        public static string InputUsername  { get; set; } = "";
        public static string InputPassword  { get; set; } = "";
        public static string StatusMessage  { get; set; } = "";

        // Reset state when entering world
        public static void Reset()
        {
            ShowingLogin    = false;
            ShowingRegister = false;
            IsLoggedIn      = false;
            InputUsername   = "";
            InputPassword   = "";
            StatusMessage   = "";
        }

        // ----------------------------------------------------------------
        // Called when server sends LoginRequired packet
        // ----------------------------------------------------------------
        public static void ShowLoginUI()
        {
            ShowingLogin    = true;
            ShowingRegister = false;
            IsLoggedIn      = false;
            InputUsername   = "";
            InputPassword   = "";
            StatusMessage   = "Enter your username and password";

            Main.NewText("[ServerGuard] Login required. Use /sg login <user> <pass>", Color.Red);
            Main.NewText("[ServerGuard] New player? Use /sg register <user> <pass>", Color.Yellow);
        }

        // ----------------------------------------------------------------
        // Send login request to server
        // ----------------------------------------------------------------
        public static void SendLogin(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                StatusMessage = "Username and password cannot be empty";
                return;
            }

            var pkt = ServerGuardMod.CreatePacket(PacketType.RequestLogin);
            pkt.Write(username.Trim());
            pkt.Write(password.Trim());
            pkt.Send(); // sends to server
            StatusMessage = "Logging in...";
        }

        // ----------------------------------------------------------------
        // Send register request to server
        // ----------------------------------------------------------------
        public static void SendRegister(string username, string password)
        {
            if (username.Length < 3)
            { StatusMessage = "Username must be 3+ characters"; return; }

            if (password.Length < 4)
            { StatusMessage = "Password must be 4+ characters"; return; }

            var pkt = ServerGuardMod.CreatePacket(PacketType.RequestRegister);
            pkt.Write(username.Trim());
            pkt.Write(password.Trim());
            pkt.Send();
            StatusMessage = "Registering...";
        }

        // ----------------------------------------------------------------
        // Server confirmed login success
        // ----------------------------------------------------------------
        public static void OnLoginSuccess(BinaryReader reader)
        {
            string username = reader.ReadString();
            var data        = AccountDatabase.ReadPlayerData(reader);

            ShowingLogin    = false;
            ShowingRegister = false;
            IsLoggedIn      = true;

            var player = Main.LocalPlayer;
            AccountDatabase.ApplyDataToPlayer(player, data);

            // Update SGPlayer state on the client side
            var sg       = player.GetModPlayer<SGPlayer>();
            sg.IsLoggedIn = true;
            sg.Username   = username;
            sg.IsAdmin    = data.IsAdmin;

            Main.NewText($"[ServerGuard] Welcome {username}!", Color.Green);

            if (data.IsAdmin)
                Main.NewText("[ServerGuard] You are ADMIN. Type /sg help for commands.", Color.Gold);
        }

        // ----------------------------------------------------------------
        // Server rejected login
        // ----------------------------------------------------------------
        public static void OnLoginFail(BinaryReader reader)
        {
            string reason = reader.ReadString();
            StatusMessage = $"Failed: {reason}";
            Main.NewText($"[ServerGuard] Login failed: {reason}", Color.Red);
        }

        // ----------------------------------------------------------------
        // Server pushes a data sync (e.g. after item pickup)
        // ----------------------------------------------------------------
        public static void ApplyServerData(BinaryReader reader)
        {
            var data   = AccountDatabase.ReadPlayerData(reader);
            var player = Main.LocalPlayer;

            AccountDatabase.ApplyDataToPlayer(player, data);
        }
    }
}
