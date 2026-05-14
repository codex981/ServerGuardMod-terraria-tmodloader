using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.IO;
using ServerGuardMod.Common.Network;

namespace ServerGuardMod.Common.Systems
{
    public class PlayerSaveData
    {
        public int SchemaVersion { get; set; } = 4;
        public string Username      { get; set; } = "";
        public string PasswordHash  { get; set; } = "";
        public bool   IsAdmin       { get; set; } = false;
        public bool   IsBanned      { get; set; } = false;
        public string BanReason     { get; set; } = "";
        public int    StatLife      { get; set; } = 100;
        public int    StatLifeMax   { get; set; } = 100;
        public int    StatMana      { get; set; } = 20;
        public int    StatManaMax   { get; set; } = 20;

        // Legacy fields kept so old accounts migrate cleanly.
        public int[]  InventoryIDs     { get; set; } = new int[59];
        public int[]  InventoryStacks  { get; set; } = new int[59];
        public int[]  InventoryPrefix  { get; set; } = new int[59];

        // Full ItemIO data keeps modded item state instead of only type/stack/prefix.
        public string[] InventoryData  { get; set; } = new string[59];
        public string[] ArmorData      { get; set; } = new string[20];
        public string[] DyeData        { get; set; } = new string[10];
        public string[] MiscEquipsData { get; set; } = new string[5];
        public string[] MiscDyesData   { get; set; } = new string[5];
        public string[] BankData       { get; set; } = new string[40];
        public string[] Bank2Data      { get; set; } = new string[40];
        public string[] Bank3Data      { get; set; } = new string[40];
        public string[] Bank4Data      { get; set; } = new string[40];

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

                    foreach (var account in _accounts.Values)
                        Normalize(account);

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
            => _accounts.ContainsKey(username.ToLowerInvariant());

        public static PlayerSaveData? GetAccount(string username)
        {
            _accounts.TryGetValue(username.ToLowerInvariant(), out var data);
            if (data != null)
                Normalize(data);
            return data;
        }

        public static bool TryLogin(string username, string password, out PlayerSaveData? data)
        {
            data = default;
            string key = username.ToLowerInvariant();

            if (!_accounts.TryGetValue(key, out var account)) return false;
            Normalize(account);
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
            string key = username.ToLowerInvariant();
            var data = CreateStrictServerProfile(username, password);

            data.PosX       = player.position.X;
            data.PosY       = player.position.Y;
            data.LoginCount = 1;
            data.LastLogin  = DateTime.Now;

            _accounts[key] = data;
            Save();
        }

        private static PlayerSaveData CreateStrictServerProfile(string username, string password)
        {
            var data = new PlayerSaveData
            {
                SchemaVersion = 4,
                Username      = username,
                PasswordHash  = HashPassword(password),
                StatLife      = 100,
                StatLifeMax   = 100,
                StatMana      = 20,
                StatManaMax   = 20
            };

            SetSavedItem(data, 0, ItemID.CopperShortsword, 1);
            SetSavedItem(data, 1, ItemID.CopperPickaxe, 1);
            SetSavedItem(data, 2, ItemID.CopperAxe, 1);
            return data;
        }

        private static void SetSavedItem(PlayerSaveData data, int slot, int itemId, int stack)
        {
            if (slot < 0 || slot >= data.InventoryData.Length)
                return;

            var item = new Item();
            item.SetDefaults(itemId);
            item.stack = Math.Max(1, Math.Min(stack, item.maxStack));

            data.InventoryData[slot] = SerializeItem(item);
            data.InventoryIDs[slot] = item.type;
            data.InventoryStacks[slot] = item.stack;
            data.InventoryPrefix[slot] = item.prefix;
        }

        public static void ApplyDataToPlayer(Player player, PlayerSaveData data)
        {
            Normalize(data);

            player.statLifeMax = Math.Max(100, data.StatLifeMax);
            player.statLife    = Math.Max(1, Math.Min(data.StatLife, player.statLifeMax));
            player.statManaMax = Math.Max(20, data.StatManaMax);
            player.statMana    = Math.Max(0, Math.Min(data.StatMana, player.statManaMax));

            ApplyItems(player.inventory, data.InventoryData, 59,
                data.InventoryIDs, data.InventoryStacks, data.InventoryPrefix);
            ApplyItems(player.armor, data.ArmorData, 20);
            ApplyItems(player.dye, data.DyeData, 10);
            ApplyItems(player.miscEquips, data.MiscEquipsData, 5);
            ApplyItems(player.miscDyes, data.MiscDyesData, 5);

            ApplyItems(player.bank.item, data.BankData, 40);
            ApplyItems(player.bank2.item, data.Bank2Data, 40);
            ApplyItems(player.bank3.item, data.Bank3Data, 40);
            ApplyItems(player.bank4.item, data.Bank4Data, 40);

            if (data.PosX > 0 && data.PosY > 0)
                player.Teleport(new Microsoft.Xna.Framework.Vector2(data.PosX, data.PosY));
        }

        public static void SavePlayerData(Player player, string username)
        {
            CapturePlayerData(player, username, saveToDisk: true);
        }

        public static void UpdatePlayerDataInMemory(Player player, string username)
        {
            CapturePlayerData(player, username, saveToDisk: false);
        }

        private static void CapturePlayerData(Player player, string username, bool saveToDisk)
        {
            string key = username.ToLowerInvariant();
            if (!_accounts.TryGetValue(key, out var data)) return;
            Normalize(data);

            data.SchemaVersion = 4;
            data.StatLife      = Math.Max(1, player.statLife);
            data.StatLifeMax   = Math.Max(100, player.statLifeMax);
            data.StatMana      = Math.Max(0, player.statMana);
            data.StatManaMax   = Math.Max(20, player.statManaMax);
            data.PosX          = player.position.X;
            data.PosY          = player.position.Y;

            CaptureItems(player.inventory, data.InventoryData, 59,
                data.InventoryIDs, data.InventoryStacks, data.InventoryPrefix);
            CaptureItems(player.armor, data.ArmorData, 20);
            CaptureItems(player.dye, data.DyeData, 10);
            CaptureItems(player.miscEquips, data.MiscEquipsData, 5);
            CaptureItems(player.miscDyes, data.MiscDyesData, 5);

            CaptureItems(player.bank.item, data.BankData, 40);
            CaptureItems(player.bank2.item, data.Bank2Data, 40);
            CaptureItems(player.bank3.item, data.Bank3Data, 40);
            CaptureItems(player.bank4.item, data.Bank4Data, 40);

            if (saveToDisk)
                Save();
        }

        public static void SendPlayerData(int whoAmI, PlayerSaveData data, PacketType type = PacketType.SyncPlayerData)
        {
            var pkt = ServerGuardMod.CreatePacket(type);
            WritePlayerData(pkt, data);
            pkt.Send(whoAmI);
        }

        public static void WritePlayerData(System.IO.BinaryWriter writer, PlayerSaveData data)
        {
            Normalize(data);

            writer.Write(data.StatLife);
            writer.Write(data.StatLifeMax);
            writer.Write(data.StatMana);
            writer.Write(data.StatManaMax);
            writer.Write(data.IsAdmin);
            writer.Write(data.PosX);
            writer.Write(data.PosY);

            WriteItemArray(writer, data.InventoryData, 59);
            WriteItemArray(writer, data.ArmorData, 20);
            WriteItemArray(writer, data.DyeData, 10);
            WriteItemArray(writer, data.MiscEquipsData, 5);
            WriteItemArray(writer, data.MiscDyesData, 5);
            WriteItemArray(writer, data.BankData, 40);
            WriteItemArray(writer, data.Bank2Data, 40);
            WriteItemArray(writer, data.Bank3Data, 40);
            WriteItemArray(writer, data.Bank4Data, 40);
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
                PosY        = reader.ReadSingle(),

                InventoryData  = ReadItemArray(reader, 59),
                ArmorData      = ReadItemArray(reader, 20),
                DyeData        = ReadItemArray(reader, 10),
                MiscEquipsData = ReadItemArray(reader, 5),
                MiscDyesData   = ReadItemArray(reader, 5),
                BankData       = ReadItemArray(reader, 40),
                Bank2Data      = ReadItemArray(reader, 40),
                Bank3Data      = ReadItemArray(reader, 40),
                Bank4Data      = ReadItemArray(reader, 40)
            };

            Normalize(data);
            return data;
        }

        public static bool BanPlayer(string username, string reason)
        {
            string key = username.ToLowerInvariant();
            if (!_accounts.TryGetValue(key, out var data)) return false;
            data.IsBanned  = true;
            data.BanReason = reason;
            Save();
            return true;
        }

        public static bool UnbanPlayer(string username)
        {
            string key = username.ToLowerInvariant();
            if (!_accounts.TryGetValue(key, out var data)) return false;
            data.IsBanned  = false;
            data.BanReason = "";
            Save();
            return true;
        }

        public static bool SetAdmin(string username, bool isAdmin)
        {
            string key = username.ToLowerInvariant();
            if (!_accounts.TryGetValue(key, out var data)) return false;
            data.IsAdmin = isAdmin;
            Save();
            return true;
        }

        public static IEnumerable<PlayerSaveData> GetAllAccounts()
        {
            foreach (var account in _accounts.Values)
                Normalize(account);
            return _accounts.Values;
        }

        private static void Normalize(PlayerSaveData data)
        {
            data.InventoryIDs    = EnsureArray(data.InventoryIDs, 59);
            data.InventoryStacks = EnsureArray(data.InventoryStacks, 59);
            data.InventoryPrefix = EnsureArray(data.InventoryPrefix, 59);

            data.InventoryData   = EnsureArray(data.InventoryData, 59);
            data.ArmorData       = EnsureArray(data.ArmorData, 20);
            data.DyeData         = EnsureArray(data.DyeData, 10);
            data.MiscEquipsData  = EnsureArray(data.MiscEquipsData, 5);
            data.MiscDyesData    = EnsureArray(data.MiscDyesData, 5);
            data.BankData        = EnsureArray(data.BankData, 40);
            data.Bank2Data       = EnsureArray(data.Bank2Data, 40);
            data.Bank3Data       = EnsureArray(data.Bank3Data, 40);
            data.Bank4Data       = EnsureArray(data.Bank4Data, 40);

            data.SchemaVersion = Math.Max(data.SchemaVersion, 4);
        }

        private static T[] EnsureArray<T>(T[]? source, int size)
        {
            var result = new T[size];
            if (source != null)
                Array.Copy(source, result, Math.Min(source.Length, size));
            return result;
        }

        private static void CaptureItems(Item[] source, string[] target, int count,
            int[]? legacyIds = null, int[]? legacyStacks = null, int[]? legacyPrefixes = null)
        {
            for (int i = 0; i < count && i < source.Length && i < target.Length; i++)
            {
                var item = source[i];
                target[i] = SerializeItem(item);

                if (legacyIds != null && i < legacyIds.Length)
                    legacyIds[i] = IsEmpty(item) ? 0 : item.type;
                if (legacyStacks != null && i < legacyStacks.Length)
                    legacyStacks[i] = IsEmpty(item) ? 0 : item.stack;
                if (legacyPrefixes != null && i < legacyPrefixes.Length)
                    legacyPrefixes[i] = IsEmpty(item) ? 0 : item.prefix;
            }
        }

        private static void ApplyItems(Item[] target, string[]? source, int count,
            int[]? legacyIds = null, int[]? legacyStacks = null, int[]? legacyPrefixes = null)
        {
            int limit = Math.Min(count, target.Length);
            for (int i = 0; i < limit; i++)
            {
                int legacyId = legacyIds != null && i < legacyIds.Length ? legacyIds[i] : 0;
                int legacyStack = legacyStacks != null && i < legacyStacks.Length ? legacyStacks[i] : 0;
                int legacyPrefix = legacyPrefixes != null && i < legacyPrefixes.Length ? legacyPrefixes[i] : 0;
                string encoded = source != null && i < source.Length ? source[i] : "";
                target[i] = DeserializeItem(encoded, legacyId, legacyStack, legacyPrefix);
            }
        }

        private static string SerializeItem(Item item)
        {
            if (IsEmpty(item))
                return "";

            try
            {
                return ItemIO.ToBase64(item);
            }
            catch (Exception ex)
            {
                ServerGuardMod.Instance.Logger.Warn($"[AccountDB] Could not serialize item {item.Name}: {ex.Message}");
                return "";
            }
        }

        private static Item DeserializeItem(string encoded, int legacyType = 0, int legacyStack = 0, int legacyPrefix = 0)
        {
            if (!string.IsNullOrWhiteSpace(encoded))
            {
                try
                {
                    var item = ItemIO.FromBase64(encoded);
                    if (!IsEmpty(item))
                        return item;
                }
                catch (Exception ex)
                {
                    ServerGuardMod.Instance.Logger.Warn($"[AccountDB] Could not load saved item data: {ex.Message}");
                }
            }

            if (legacyType > ItemID.None && legacyStack > 0)
            {
                var item = new Item();
                item.SetDefaults(legacyType);
                item.stack = Math.Max(1, Math.Min(legacyStack, item.maxStack));
                item.prefix = (byte)Math.Clamp(legacyPrefix, byte.MinValue, byte.MaxValue);
                return item;
            }

            return new Item();
        }

        private static bool IsEmpty(Item? item)
            => item == null || item.type <= ItemID.None || item.stack <= 0;

        private static void WriteItemArray(BinaryWriter writer, string[] data, int count)
        {
            writer.Write(count);
            for (int i = 0; i < count; i++)
                writer.Write(i < data.Length ? data[i] ?? "" : "");
        }

        private static string[] ReadItemArray(BinaryReader reader, int expectedCount)
        {
            int count = reader.ReadInt32();
            var data = new string[expectedCount];
            for (int i = 0; i < count; i++)
            {
                string value = reader.ReadString();
                if (i < expectedCount)
                    data[i] = value;
            }
            return data;
        }
    }
}
