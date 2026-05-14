using System;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Microsoft.Xna.Framework;
using ServerGuardMod.Common.Players;
using ServerGuardMod.Common.Systems;
using ServerGuardMod.Common.Network;

namespace ServerGuardMod.Common.Commands
{
    public class SGCommand : ModCommand
    {
        public override string Command     => "sg";
        public override CommandType Type   => CommandType.Chat | CommandType.Console;
        public override string Description => "ServerGuard - /sg help";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (args.Length == 0)
            {
                caller.Reply("Type /sg help for commands", Color.Yellow);
                return;
            }

            string cmd = args[0].ToLower();

            // ============================================================
            // PUBLIC COMMANDS - no login required
            // ============================================================
            switch (cmd)
            {
                case "help":
                    ShowHelp(caller);
                    return;

                case "register":
                    CmdRegister(caller, args);
                    return;

                case "login":
                    CmdLogin(caller, args);
                    return;
            }

            // ============================================================
            // From this point onward: login is required
            // ============================================================
            bool isAdmin = false;

            if (caller.Player != null)
            {
                var sgPlayer = caller.Player.GetModPlayer<SGPlayer>();
                if (!sgPlayer.IsLoggedIn)
                {
                    caller.Reply("You must /sg login first!", Color.Red);
                    return;
                }
                isAdmin = sgPlayer.IsAdmin;
            }
            else
            {
                // Server console = always admin, no login needed
                isAdmin = true;
            }

            if (!isAdmin)
            {
                caller.Reply("You do not have admin permissions!", Color.Red);
                return;
            }

            // ============================================================
            // ADMIN COMMANDS
            // ============================================================
            switch (cmd)
            {
                case "kick":       CmdKick(caller, args);       break;
                case "ban":        CmdBan(caller, args);        break;
                case "unban":      CmdUnban(caller, args);      break;
                case "god":        CmdGod(caller, args);        break;
                case "give":       CmdGive(caller, args);       break;
                case "tp":
                case "teleport":   CmdTeleport(caller, args);   break;
                case "freeze":     CmdFreeze(caller, args);     break;
                case "setadmin":   CmdSetAdmin(caller, args);   break;
                case "setpass":    CmdSetPass(caller, args);    break;
                case "online":     CmdOnline(caller);           break;
                case "account":
                case "accounts":   CmdAccounts(caller, args);   break;
                case "inv":
                case "showme":     CmdShowMe(caller, args);     break;
                case "broadcast":  CmdBroadcast(caller, args);  break;
                case "save":
                    AccountDatabase.Save();
                    caller.Reply("All data saved.", Color.Green);
                    break;
                case "reload":
                    AccountDatabase.Load();
                    caller.Reply("Database reloaded.", Color.Green);
                    break;
                case "logpath":
                    caller.Reply($"Log path: {System.IO.Path.Combine(Main.SavePath, "ServerGuard")}", Color.Yellow);
                    break;
                default:
                    caller.Reply($"Unknown command: {cmd}  -  type /sg help", Color.Red);
                    break;
            }
        }

        // ================================================================
        // REGISTER - anyone can call this (not logged in yet)
        // ================================================================
        private void CmdRegister(CommandCaller caller, string[] args)
        {
            // Only works in multiplayer client
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                caller.Reply("Use /sg register in multiplayer only.", Color.Red);
                return;
            }

            if (args.Length < 3)
            {
                caller.Reply("Usage: /sg register <username> <password>", Color.Yellow);
                return;
            }

            if (ClientLoginSystem.IsLoggedIn)
            {
                caller.Reply("You are already logged in!", Color.Orange);
                return;
            }

            string username = args[1].Trim();
            string password = args[2].Trim();

            if (username.Length < 3)
            { caller.Reply("Username must be at least 3 characters.", Color.Red); return; }
            if (password.Length < 4)
            { caller.Reply("Password must be at least 4 characters.", Color.Red); return; }

            ClientLoginSystem.SendRegister(username, password);
            caller.Reply($"Register request sent for '{username}'...", Color.Cyan);
        }

        // ================================================================
        // LOGIN - anyone can call this (not logged in yet)
        // ================================================================
        private void CmdLogin(CommandCaller caller, string[] args)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                caller.Reply("Use /sg login in multiplayer only.", Color.Red);
                return;
            }

            if (args.Length < 3)
            {
                caller.Reply("Usage: /sg login <username> <password>", Color.Yellow);
                return;
            }

            if (ClientLoginSystem.IsLoggedIn)
            {
                caller.Reply("You are already logged in!", Color.Orange);
                return;
            }

            string username = args[1].Trim();
            string password = args[2].Trim();

            ClientLoginSystem.SendLogin(username, password);
            caller.Reply($"Login request sent for '{username}'...", Color.Cyan);
        }

        // ================================================================
        // HELP
        // ================================================================
        private void ShowHelp(CommandCaller caller)
        {
            caller.Reply("========== ServerGuard ==========", Color.Gold);
            caller.Reply("/sg register <user> <pass>        - Create account", Color.Cyan);
            caller.Reply("/sg login <user> <pass>           - Login", Color.Cyan);
            caller.Reply("--- Admin Commands ---", Color.Yellow);
            caller.Reply("/sg kick <name> [reason]", Color.White);
            caller.Reply("/sg ban <name> [reason]", Color.White);
            caller.Reply("/sg unban <name>", Color.White);
            caller.Reply("/sg god [name]", Color.White);
            caller.Reply("/sg give <name> <itemID> <stack>", Color.White);
            caller.Reply("/sg tp <name>", Color.White);
            caller.Reply("/sg freeze <name>", Color.White);
            caller.Reply("/sg setadmin <name> <true/false>", Color.White);
            caller.Reply("/sg setpass <name> <password>", Color.White);
            caller.Reply("/sg online", Color.White);
            caller.Reply("/sg accounts [page]", Color.White);
            caller.Reply("/sg inv <name>                    - Inspect inventory UI", Color.White);
            caller.Reply("/sg broadcast <message>", Color.White);
            caller.Reply("/sg logpath                       - Show log folder path", Color.White);
            caller.Reply("/sg save  |  /sg reload", Color.White);
            caller.Reply("=================================", Color.Gold);
        }

        // ================================================================
        // KICK
        // ================================================================
        private void CmdKick(CommandCaller caller, string[] args)
        {
            if (args.Length < 2) { caller.Reply("Usage: /sg kick <name> [reason]", Color.Red); return; }
            string reason = args.Length > 2 ? string.Join(" ", args[2..]) : "Kicked by admin";
            var target    = FindPlayer(args[1]);
            
            if (target == null) 
            { 
                caller.Reply($"Player '{args[1]}' not found online", Color.Red); 
                return; 
            }

            NetMessage.SendData(MessageID.Kick, target.whoAmI, -1,
                Terraria.Localization.NetworkText.FromLiteral(reason));

            caller.Reply($"Kicked {target.name}: {reason}", Color.Green);
            Main.NewText($"[ServerGuard] {target.name} was kicked: {reason}", Color.Orange);
        }

        // ================================================================
        // BAN
        // ================================================================
        private void CmdBan(CommandCaller caller, string[] args)
        {
            if (args.Length < 2) { caller.Reply("Usage: /sg ban <name> [reason]", Color.Red); return; }
            string reason = args.Length > 2 ? string.Join(" ", args[2..]) : "Banned";

            string targetName = args[1];
            var onlineTarget = FindPlayer(targetName);
            
            // If they are online, we can get their account name if registered
            if (onlineTarget != null)
            {
                var sgPlayer = onlineTarget.GetModPlayer<SGPlayer>();
                if (sgPlayer.IsLoggedIn && !string.IsNullOrEmpty(sgPlayer.Username))
                {
                    targetName = sgPlayer.Username;
                }
            }

            bool found = AccountDatabase.BanPlayer(targetName, reason);
            
            if (onlineTarget != null)
            {
                NetMessage.SendData(MessageID.Kick, onlineTarget.whoAmI, -1,
                    Terraria.Localization.NetworkText.FromLiteral($"Banned: {reason}"));
                caller.Reply($"Banned and kicked {onlineTarget.name}: {reason}", Color.Green);
            }
            else if (found)
            {
                caller.Reply($"Banned offline account {targetName}: {reason}", Color.Green);
            }
            else
            {
                caller.Reply($"Account '{targetName}' not found, and player not online.", Color.Red);
                return;
            }

            Main.NewText($"[ServerGuard] {args[1]} was banned: {reason}", Color.Red);
        }

        // ================================================================
        // UNBAN
        // ================================================================
        private void CmdUnban(CommandCaller caller, string[] args)
        {
            if (args.Length < 2) { caller.Reply("Usage: /sg unban <name>", Color.Red); return; }
            bool ok = AccountDatabase.UnbanPlayer(args[1]);
            caller.Reply(ok ? $"Unbanned {args[1]}" : "Account not found", ok ? Color.Green : Color.Red);
        }

        // ================================================================
        // GOD MODE
        // ================================================================
        private void CmdGod(CommandCaller caller, string[] args)
        {
            Player found  = args.Length > 1 ? FindPlayer(args[1]) : null;
            Player target = (found != null) ? found : caller.Player;
            if (target == null) { caller.Reply("Player not found", Color.Red); return; }

            var sg = target.GetModPlayer<SGPlayer>();
            sg.IsGodMode = !sg.IsGodMode;
            
            // Also heal them fully when enabling god mode
            if (sg.IsGodMode)
            {
                target.statLife = target.statLifeMax2;
                target.HealEffect(target.statLifeMax2);
            }

            caller.Reply($"God mode for {target.name}: {(sg.IsGodMode ? "ON" : "OFF")}", Color.Gold);
        }

        // ================================================================
        // GIVE ITEM
        // ================================================================
        private void CmdGive(CommandCaller caller, string[] args)
        {
            if (args.Length < 4) { caller.Reply("Usage: /sg give <name> <itemID> <stack>", Color.Red); return; }
            var target = FindPlayer(args[1]);
            if (target == null) { caller.Reply("Player not found", Color.Red); return; }

            if (!int.TryParse(args[2], out int itemID) || !int.TryParse(args[3], out int stack))
            { caller.Reply("itemID and stack must be numbers", Color.Red); return; }

            int slot = -1;
            for (int i = 0; i < 50; i++)
            {
                if (target.inventory[i].type == ItemID.None) { slot = i; break; }
            }
            if (slot == -1) { caller.Reply("Target inventory is full", Color.Red); return; }

            target.inventory[slot] = new Item();
            target.inventory[slot].SetDefaults(itemID);
            target.inventory[slot].stack = Math.Min(stack, target.inventory[slot].maxStack);

            var sg = target.GetModPlayer<SGPlayer>();
            if (sg.IsLoggedIn)
            {
                AccountDatabase.SavePlayerData(target, sg.Username);
                sg.TrustCurrentServerState("admin give");

                var account = AccountDatabase.GetAccount(sg.Username);
                if (account != null)
                    AccountDatabase.SendPlayerData(target.whoAmI, account);
            }

            caller.Reply($"Gave {target.name}: {target.inventory[slot].Name} x{stack}", Color.Green);
        }

        // ================================================================
        // TELEPORT
        // ================================================================
        private void CmdTeleport(CommandCaller caller, string[] args)
        {
            if (args.Length < 2) { caller.Reply("Usage: /sg tp <name>", Color.Red); return; }
            if (caller.Player == null) { caller.Reply("In-game only", Color.Red); return; }
            var target = FindPlayer(args[1]);
            if (target == null) { caller.Reply("Player not found", Color.Red); return; }
            caller.Player.Teleport(target.position);
            caller.Reply($"Teleported to {target.name}", Color.Green);
        }

        // ================================================================
        // FREEZE
        // ================================================================
        private void CmdFreeze(CommandCaller caller, string[] args)
        {
            if (args.Length < 2) { caller.Reply("Usage: /sg freeze <name>", Color.Red); return; }
            var target = FindPlayer(args[1]);
            if (target == null) { caller.Reply("Player not found", Color.Red); return; }

            var sg = target.GetModPlayer<SGPlayer>();
            sg.IsFrozen = !sg.IsFrozen;

            var packet = ServerGuardMod.CreatePacket(PacketType.FreezePlayer);
            packet.Write(sg.IsFrozen);
            packet.Send(target.whoAmI);

            caller.Reply($"{target.name} is {(sg.IsFrozen ? "FROZEN" : "FREE")}", Color.Cyan);
        }

        // ================================================================
        // SET ADMIN
        // ================================================================
        private void CmdSetAdmin(CommandCaller caller, string[] args)
        {
            if (args.Length < 3) { caller.Reply("Usage: /sg setadmin <name> <true/false>", Color.Red); return; }
            bool val = args[2].ToLower() == "true";
            bool ok  = AccountDatabase.SetAdmin(args[1], val);
            var online = FindPlayer(args[1]);
            if (ok && online != null)
                online.GetModPlayer<SGPlayer>().IsAdmin = val;
            caller.Reply(ok ? $"{args[1]} admin = {val}" : "Account not found", ok ? Color.Green : Color.Red);
        }

        // ================================================================
        // SET PASSWORD
        // ================================================================
        private void CmdSetPass(CommandCaller caller, string[] args)
        {
            if (args.Length < 3) { caller.Reply("Usage: /sg setpass <name> <password>", Color.Red); return; }
            var acc = AccountDatabase.GetAccount(args[1]);
            if (acc == null) { caller.Reply("Account not found", Color.Red); return; }
            acc.PasswordHash = AccountDatabase.HashPassword(args[2]);
            AccountDatabase.Save();
            caller.Reply($"Password changed for {args[1]}", Color.Green);
        }

        // ================================================================
        // ONLINE
        // ================================================================
        private void CmdOnline(CommandCaller caller)
        {
            caller.Reply("====== Online Players ======", Color.Gold);
            int count = 0;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                var p = Main.player[i];
                if (!p.active) continue;
                var sg  = p.GetModPlayer<SGPlayer>();
                string status = sg.IsLoggedIn ? $"[{sg.Username}]" : "[not logged in]";
                string adm    = sg.IsAdmin ? " [ADMIN]" : "";
                caller.Reply($"  {p.name} {status}{adm}", Color.White);
                count++;
            }
            caller.Reply($"Total: {count}", Color.Yellow);
        }

        // ================================================================
        // ACCOUNTS (Paginated)
        // ================================================================
        private void CmdAccounts(CommandCaller caller, string[] args)
        {
            int page = 1;
            if (args.Length > 1) int.TryParse(args[1], out page);
            if (page < 1) page = 1;

            var allAccounts = System.Linq.Enumerable.ToList(AccountDatabase.GetAllAccounts());
            int totalAccounts = allAccounts.Count;
            int perPage = 10;
            int totalPages = (int)System.Math.Ceiling(totalAccounts / (double)perPage);

            if (page > totalPages && totalPages > 0) page = totalPages;

            caller.Reply($"====== All Accounts (Page {page}/{totalPages}) ======", Color.Gold);

            int start = (page - 1) * perPage;
            int end = System.Math.Min(start + perPage, totalAccounts);

            for (int i = start; i < end; i++)
            {
                var acc = allAccounts[i];
                string st  = acc.IsBanned ? "[BANNED]" : "[Active]";
                string adm = acc.IsAdmin  ? " [ADMIN]" : "";
                caller.Reply($"  {acc.Username}{adm} | HP:{acc.StatLife}/{acc.StatLifeMax} | {st} | {acc.LastLogin:yyyy-MM-dd}", Color.White);
            }

            if (page < totalPages)
            {
                caller.Reply($"Type '/sg accounts {page + 1}' for next page.", Color.Yellow);
            }
        }

        // ================================================================
        // BROADCAST
        // ================================================================
        private void CmdBroadcast(CommandCaller caller, string[] args)
        {
            if (args.Length < 2) { caller.Reply("Usage: /sg broadcast <message>", Color.Red); return; }
            string msg = string.Join(" ", args[1..]);
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                if (!Main.player[i].active) continue;
                var pkt = ServerGuardMod.CreatePacket(PacketType.AdminMessage);
                pkt.Write($"[Broadcast] {msg}");
                pkt.Send(i);
            }
            Main.NewText($"[Broadcast] {msg}", Color.Gold);
            caller.Reply("Sent.", Color.Green);
        }

        // ================================================================
        // ================================================================
        // SHOW ME (Inspect UI)
        // ================================================================
        private void CmdShowMe(CommandCaller caller, string[] args)
        {
            if (Main.netMode == NetmodeID.Server)
            {
                caller.Reply("This command opens a UI, it can only be used in-game.", Color.Red);
                return;
            }

            if (args.Length < 2) { caller.Reply("Usage: /sg showme <name>", Color.Red); return; }
            
            var target = FindPlayer(args[1]);
            if (target == null) 
            { 
                caller.Reply("Player not found online.", Color.Red); 
                return; 
            }

            // Toggle the Inspect UI for this target
            if (InspectUI.InspectTarget == target)
            {
                InspectUI.InspectTarget = null;
                caller.Reply("Closed inspect UI.", Color.Yellow);
            }
            else
            {
                InspectUI.InspectTarget = target;
                caller.Reply($"Inspecting {target.name}'s inventory...", Color.Green);
            }
        }

        // ================================================================
        // HELPER - find online player by name or username
        // ================================================================
        private Player FindPlayer(string name)
        {
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                var p = Main.player[i];
                if (!p.active) continue;
                if (p.name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    p.GetModPlayer<SGPlayer>().Username.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return p;
            }
            return null;
        }
    }
}
