using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Terraria;
using Terraria.ModLoader;
using Newtonsoft.Json;

namespace ServerGuardMod.Common.Systems
{
    public class PlayerSaveData
    {
        public string Username      { get; set; } = "";
        public string PasswordHash  { get; set; } = "";
        public bool   IsAdmin       { get; set; } = false;
        public bool   IsBanned      { get; set; } = false;
        public string BanReason     { get; set; } = "";
        public int    StatLife      { get; set; } = 100;
        public int    StatLifeMax   { get; set; } = 100;
        public int    StatMana      { get; set; } = 20;
        public int    StatManaMax   { get; set; } = 20;
        public int[]  InventoryIDs     { get; set; } = new int[59];
        public int[]  InventoryStacks  { get; set; } = new int[59];
        public int[]  InventoryPrefix  { get; set; } = new int[59];
        public float  PosX          { get; set; } = 0;
        public float  PosY          { get; set; } = 0;
        public DateTime LastLogin   { get; set; } = DateTime.Now;
        public string LastIP        { get; set; } = "";
        public int    LoginCount    { get; set; } = 0;
    }

    public static class AccountDatabase
    {
        private static Dictionary<string, PlayerSaveData> _accounts = new();

        private static string SavePath => Path.Combine(
            Main.SavePath, "ServerGuard", "accounts.json"
        );

        public static void Load()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
                if (File.Exists(SavePath))
                {
                    string json = File.ReadAllText(SavePath);
                    _accounts = JsonConvert.DeserializeObject<Dictionary<string, PlayerSaveData>>(json)
                                ?? new Dictionary<string, PlayerSaveData>();
                    ServerGuardMod.Instance.Logger.Info($"[AccountDB] Loaded {_accounts.Count} accounts");
                }
            }
            catch (Exception ex)
            {
                ServerGuardMod.Instance.Logger.Error($"[AccountDB] Load error: {ex.Message}");
                _accounts = new Dictionary<string, PlayerSaveData>();
            }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
                string json = JsonConvert.SerializeObject(_accounts, Formatting.Indented);
                File.WriteAllText(SavePath, json);
            }
            catch (Exception ex)
            {
                ServerGuardMod.Instance.Logger.Error($"[AccountDB] Save error: {ex.Message}");
            }
        }

        public static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password + "ServerGuard_Salt_2024"));
            return Convert.ToHexString(bytes);
        }

        public static bool AccountExists(string username)
            => _accounts.ContainsKey(username.ToLower());

        public static PlayerSaveData GetAccount(string username)
        {
            _accounts.TryGetValue(username.ToLower(), out var data);
            return data;
        }

        public static bool TryLogin(string username, string password, out PlayerSaveData data)
        {
            data = default;
            string key = username.ToLower();

            if (!_accounts.TryGetValue(key, out var account)) return false;
            if (account.IsBanned) return false;

            string hash = HashPassword(password);
            if (account.PasswordHash != hash) return false;

            account.LastLogin = DateTime.Now;
            account.LoginCount++;
            data = account;
            Save();
            return true;
        }

        public static void CreateAccount(string username, string password, Player player)
        {
            string key = username.ToLower();
            var data = new PlayerSaveData
            {
                Username      = username,
                PasswordHash  = HashPassword(password),
                StatLife      = player.statLifeMax,
                StatLifeMax   = player.statLifeMax,
                StatMana      = player.statManaMax,
                StatManaMax   = player.statManaMax,
                PosX          = player.position.X,
                PosY          = player.position.Y,
                LoginCount    = 1,
                LastLogin     = DateTime.Now
            };

            for (int i = 0; i < 59; i++)
            {
                data.InventoryIDs[i]    = player.inventory[i].type;
                data.InventoryStacks[i] = player.inventory[i].stack;
                data.InventoryPrefix[i] = player.inventory[i].prefix;
            }

            _accounts[key] = data;
            Save();
        }

        public static void ApplyDataToPlayer(Player player, PlayerSaveData data)
        {
            player.statLife    = data.StatLife;
            player.statLifeMax = data.StatLifeMax;
            player.statMana    = data.StatMana;
            player.statManaMax = data.StatManaMax;

            // Check if DB inventory is entirely empty (first time login)
            bool isDbEmpty = true;
            for (int i = 0; i < 59 && i < data.InventoryIDs.Length; i++)
            {
                if (data.InventoryIDs[i] != 0)
                {
                    isDbEmpty = false;
                    break;
                }
            }

            // If it's not a brand new empty inventory, load it
            if (!isDbEmpty)
            {
                for (int i = 0; i < player.inventory.Length; i++)
                    player.inventory[i] = new Item();

                for (int i = 0; i < 59 && i < data.InventoryIDs.Length; i++)
                {
                    if (data.InventoryIDs[i] != 0)
                    {
                        player.inventory[i] = new Item();
                        player.inventory[i].SetDefaults(data.InventoryIDs[i]);
                        player.inventory[i].stack  = data.InventoryStacks[i];
                        player.inventory[i].prefix = (byte)data.InventoryPrefix[i];
                    }
                }
            }

            if (data.PosX > 0 && data.PosY > 0)
                player.Teleport(new Microsoft.Xna.Framework.Vector2(data.PosX, data.PosY));
        }

        public static void SavePlayerData(Player player, string username)
        {
            string key = username.ToLower();
            if (!_accounts.TryGetValue(key, out var data)) return;

            data.StatLife    = player.statLife;
            data.StatLifeMax = player.statLifeMax;
            data.StatMana    = player.statMana;
            data.StatManaMax = player.statManaMax;
            data.PosX        = player.position.X;
            data.PosY        = player.position.Y;

            for (int i = 0; i < 59; i++)
            {
                var item = player.inventory[i];
                data.InventoryIDs[i]    = item.type;
                data.InventoryStacks[i] = item.stack;
                data.InventoryPrefix[i] = item.prefix;
            }

            Save();
        }

        public static void WritePlayerData(System.IO.BinaryWriter writer, PlayerSaveData data)
        {
            writer.Write(data.StatLife);
            writer.Write(data.StatLifeMax);
            writer.Write(data.StatMana);
            writer.Write(data.StatManaMax);
            writer.Write(data.IsAdmin);
            writer.Write(data.PosX);
            writer.Write(data.PosY);

            for (int i = 0; i < 59; i++)
            {
                writer.Write(data.InventoryIDs[i]);
                writer.Write(data.InventoryStacks[i]);
                writer.Write(data.InventoryPrefix[i]);
            }
        }

        public static PlayerSaveData ReadPlayerData(System.IO.BinaryReader reader)
        {
            var data = new PlayerSaveData
            {
                StatLife    = reader.ReadInt32(),
                StatLifeMax = reader.ReadInt32(),
                StatMana    = reader.ReadInt32(),
                StatManaMax = reader.ReadInt32(),
                IsAdmin     = reader.ReadBoolean(),
                PosX        = reader.ReadSingle(),
                PosY        = reader.ReadSingle()
            };

            for (int i = 0; i < 59; i++)
            {
                data.InventoryIDs[i]    = reader.ReadInt32();
                data.InventoryStacks[i] = reader.ReadInt32();
                data.InventoryPrefix[i] = reader.ReadInt32();
            }

            return data;
        }

        public static bool BanPlayer(string username, string reason)
        {
            string key = username.ToLower();
            if (!_accounts.TryGetValue(key, out var data)) return false;
            data.IsBanned  = true;
            data.BanReason = reason;
            Save();
            return true;
        }

        public static bool UnbanPlayer(string username)
        {
            string key = username.ToLower();
            if (!_accounts.TryGetValue(key, out var data)) return false;
            data.IsBanned  = false;
            data.BanReason = "";
            Save();
            return true;
        }

        public static bool SetAdmin(string username, bool isAdmin)
        {
            string key = username.ToLower();
            if (!_accounts.TryGetValue(key, out var data)) return false;
            data.IsAdmin = isAdmin;
            Save();
            return true;
        }

        public static IEnumerable<PlayerSaveData> GetAllAccounts() => _accounts.Values;
    }
}
