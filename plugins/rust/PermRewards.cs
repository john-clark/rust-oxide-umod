using Newtonsoft.Json;
using Oxide.Core;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Perm Rewards", "Ryan", "1.0.2")]
    [Description("Gives players a kit-like reward if they're in an Oxide group")]
    internal class PermRewards : RustPlugin
    {
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #region Lang

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["DayFormat"] = "<color=orange>{0}</color> day and <color=orange>{1}</color> hours",
                ["DaysFormat"] = "<color=orange>{0}</color> days and <color=orange>{1}</color> hours",
                ["HourFormat"] = "<color=orange>{0}</color> hour and <color=orange>{1}</color> minutes",
                ["HoursFormat"] = "<color=orange>{0}</color> hours and <color=orange>{1}</color> minutes",
                ["MinFormat"] = "<color=orange>{0}</color> minute and <color=orange>{1}</color> seconds",
                ["MinsFormat"] = "<color=orange>{0}</color> minutes and <color=orange>{1}</color> seconds",
                ["SecsFormat"] = "<color=orange>{0}</color> seconds",
                ["Cooldown"] = "Try again in {0}",
                ["AlreadyUsed"] = "You've already claimed all of your rewards!",
                ["Reward"] = "Thanks for being in our steam group {0}!",
                ["NotAllowed"] = "You're not in our steam group!",
                ["FullInventory"] = "<color=#ff6666>You don't have enough room for your reward items</color>"
            }, this);
        }

        #endregion Lang

        #region Data

        private static Cooldowns cooldowns;

        public class Cooldowns
        {
            public Dictionary<ulong, DateTime> PlayerCooldowns = new Dictionary<ulong, DateTime>();
        }

        public class Data
        {
            public static void Add(BasePlayer player)
            {
                if (cooldowns.PlayerCooldowns.ContainsKey(player.userID))
                    return;
                else
                {
                    cooldowns.PlayerCooldowns.Add(player.userID, DateTime.UtcNow);
                    Save();
                }
            }
            public static bool Exists(BasePlayer player)
            {
                if (cooldowns.PlayerCooldowns.ContainsKey(player.userID))
                    return true;
                else
                    return false;
            }
            public static void Remove(BasePlayer player)
            {
                if (cooldowns.PlayerCooldowns.ContainsKey(player.userID))
                {
                    cooldowns.PlayerCooldowns.Remove(player.userID);
                    Save();
                }
            }
            public static void Clear()
            {
                cooldowns.PlayerCooldowns.Clear();
                Save();
            }

            public static void Save() => Interface.Oxide.DataFileSystem.WriteObject("PermRewards", cooldowns);
        }

        #endregion Data

        #region Config

        private ConfigFile _Config;

        public class Items
        {
            public string ItemName { get; set; }
            public int Amount { get; set; }
        }

        public class ConfigFile
        {
            [JsonProperty(PropertyName = "Item names and their amounts")]
            public List<Items> ItemInfo;

            [JsonProperty(PropertyName = "Command to use the reward")]
            public string Command;

            [JsonProperty(PropertyName = "Reward cooldown (seconds)")]
            public float Cooldown;

            [JsonProperty(PropertyName = "Enable the cooldown (true/false)")]
            public bool UseCooldown;

            [JsonProperty(PropertyName = "Oxide permission group")]
            public string OxideGroup;

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    OxideGroup = "steam",
                    Command = "steam",
                    UseCooldown = true,
                    Cooldown = 600,
                    ItemInfo = new List<Items>()
                    {
                        new Items
                        {
                            ItemName = "explosive.timed",
                            Amount = 2
                        },
                        new Items
                        {
                            ItemName = "gunpowder",
                            Amount = 250
                        },
                        new Items
                        {
                            ItemName = "lowgradefuel",
                            Amount = 500
                        }
                    }
                };
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating default configuration file...");
            _Config = ConfigFile.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _Config = Config.ReadObject<ConfigFile>();
        }

        protected override void SaveConfig() => Config.WriteObject(_Config);

        #endregion Config

        #region Methods

        private void GiveItem(BasePlayer player)
        {
            foreach (var item in _Config.ItemInfo)
                player.GiveItem(ItemManager.CreateByName(item.ItemName, item.Amount));
            LogToFile("Redeems", $"[{DateTime.Now}] {player.displayName} ({player.UserIDString}) has redeemed their reward", this);
            PrintToChat(player, Lang("Reward", player.UserIDString, player.displayName));
        }

        public double GetCooldown(BasePlayer player)
        {
            if (Data.Exists(player))
                return Math.Floor((cooldowns.PlayerCooldowns[player.userID].AddSeconds(_Config.Cooldown) - DateTime.UtcNow).TotalSeconds);
            else return 0;
        }

        private string GetFormattedMsg(double cooldown)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(cooldown);

            if (timeSpan == null) return null;

            if (Math.Floor(timeSpan.TotalDays) >= 1)
                return string.Format(timeSpan.Days > 1 ? Lang("DaysFormat", null, timeSpan.Days, timeSpan.Hours) : Lang("DayFormat", null, timeSpan.Days, timeSpan.Hours));

            if (Math.Floor(timeSpan.TotalMinutes) > 60)
                return string.Format(timeSpan.Hours > 1 ? Lang("HoursFormat", null, timeSpan.Hours, timeSpan.Minutes) : Lang("HourFormat", null, timeSpan.Hours, timeSpan.Minutes));

            if (Math.Floor(timeSpan.TotalSeconds) > 60)
                return string.Format(timeSpan.Minutes > 1 ? Lang("MinsFormat", null, timeSpan.Minutes, timeSpan.Seconds) : Lang("MinFormat", null, timeSpan.Minutes, timeSpan.Seconds));

            return Lang("SecsFormat", null, timeSpan.Seconds);
        }

        #endregion Methods

        #region Hooks

        private void OnNewSave(string filename) => Data.Clear();

        private void Loaded()
        {
            cooldowns = Interface.Oxide.DataFileSystem.ReadObject<Cooldowns>(Name);
            cmd.AddChatCommand(_Config.Command, this, "chatCmd");
            SaveConfig();
        }
        private bool CanUse(BasePlayer player)
        {
            if (!permission.UserHasGroup(player.UserIDString, _Config.OxideGroup))
            {
                PrintToChat(player, Lang("NotMember", player.UserIDString));
                return false;
            }
            if (Data.Exists(player))
            {
                if (!_Config.UseCooldown)
                {
                    PrintToChat(Lang("AlreadyUsed", player.UserIDString));
                    return false;
                }
                if (cooldowns.PlayerCooldowns[player.userID].AddSeconds(_Config.Cooldown) > DateTime.UtcNow)
                {
                    PrintToChat(Lang("Cooldown", player.UserIDString, GetFormattedMsg(GetCooldown(player))));
                    return false;
                }
                else if (cooldowns.PlayerCooldowns[player.userID].AddSeconds(_Config.Cooldown) < DateTime.UtcNow)
                {
                    if (!player.inventory.containerMain.IsFull() && !player.inventory.containerBelt.IsFull())
                    {
                        Data.Remove(player);
                        return true;
                    }
                    PrintToChat(player, Lang("FullInventory", player.UserIDString));
                    return false;
                }
            }
            if (!Data.Exists(player))
            {
                if (!player.inventory.containerMain.IsFull() && !player.inventory.containerBelt.IsFull())
                {
                    Data.Add(player);
                    return true;
                }
                PrintToChat(player, Lang("FullInventory", player.UserIDString));
                return false;
            }
            return true;
        }

        private void chatCmd(BasePlayer player, string command, string[] args)
        {
            if (!CanUse(player)) return;
            GiveItem(player);
        }

        #endregion Hooks
    }
}
