using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Karma Kills", "Ryan", "1.0.3")]
    [Description("Rewards players on karma on kill, or takes karma away from them")]
    public class KarmaKills : RustPlugin
    {
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        [PluginReference] private Plugin KarmaSystem;

        #region Config

        private ConfigFile configFile;

        public class ConfigFile
        {
            public PlayerKills PlayerKills { get; set; }
            public AnimalKills AnimalKills { get; set; }
            public Killstreak Killstreak { get; set; }
            public Hero Hero { get; set; }
            public Bandit Bandit { get; set; }

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    PlayerKills = new PlayerKills()
                    {
                        Enabled = true,
                        AddKarma = new Karma()
                        {
                            Enabled = false,
                            Amount = 1
                        },
                        RemoveKarma = new Karma()
                        {
                            Enabled = true,
                            Amount = 1
                        }
                    },
                    AnimalKills = new AnimalKills()
                    {
                        Enabled = true,
                        AddKarma = new Karma()
                        {
                            Enabled = true,
                            Amount = 1
                        },
                        RemoveKarma = new Karma()
                        {
                            Enabled = false,
                            Amount = 1
                        }
                    },
                    Killstreak = new Killstreak()
                    {
                        Amount = 5
                    },
                    Hero = new Hero()
                    {
                        RemoveKarma = new Karma()
                        {
                            Amount = 2,
                            Enabled = true
                        }
                    },
                    Bandit = new Bandit()
                    {
                        AddKarma = new Karma()
                        {
                            Amount = 2,
                            Enabled = true
                        }
                    }
                };
            }
        }

        public class Killstreak
        {
            [JsonProperty(PropertyName = "Kills before a bounty is placed")]
            public int Amount { get; set; }
        }

        public class PlayerKills
        {
            public bool Enabled { get; set; }
            public Karma AddKarma { get; set; }
            public Karma RemoveKarma { get; set; }
        }

        public class AnimalKills
        {
            public bool Enabled { get; set; }
            public Karma AddKarma { get; set; }
            public Karma RemoveKarma { get; set; }
        }

        public class Karma
        {
            public bool Enabled { get; set; }
            public int Amount { get; set; }
        }

        public class Hero
        {
            [JsonProperty(PropertyName = "Remove Karma to killer of Hero")]
            public Karma RemoveKarma { get; set; }
        }

        public class Bandit
        {
            [JsonProperty(PropertyName = "Add Karma to killer of Bandit")]
            public Karma AddKarma { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating default configuration file...");
            configFile = ConfigFile.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configFile = Config.ReadObject<ConfigFile>();
        }

        protected override void SaveConfig() => Config.WriteObject(configFile);

        #endregion Config

        #region Data

        private static StoredData storedData;

        public class StoredData
        {
            public Dictionary<ulong, int> Killstreaks = new Dictionary<ulong, int>();
            public List<ulong> Heroes = new List<ulong>();
            public List<ulong> Bandits = new List<ulong>();
        }

        public class Data
        {
            public class Killstreaks
            {
                public static void Add(BasePlayer player)
                {
                    if (!Exists(player))
                    {
                        storedData.Killstreaks.Add(player.userID, 0);
                        SaveData();
                    }
                }

                public static void Remove(BasePlayer player)
                {
                    if (Exists(player))
                    {
                        storedData.Killstreaks.Remove(player.userID);
                        SaveData();
                    }
                }

                public static bool Exists(BasePlayer player)
                {
                    if (storedData.Killstreaks.ContainsKey(player.userID))
                    {
                        return true;
                    }
                    return false;
                }

                public static void IncrementStreak(BasePlayer player)
                {
                    if (Exists(player))
                    {
                        storedData.Killstreaks[player.userID]++;
                        SaveData();
                    }
                }

                public static int GetStreak(BasePlayer player)
                {
                    if (Exists(player))
                    {
                        return storedData.Killstreaks[player.userID];
                    }
                    return 0;
                }
            }

            public class Heroes
            {
                public static void Add(BasePlayer player)
                {
                    if (!Exists(player))
                    {
                        storedData.Heroes.Add(player.userID);
                    }
                }

                public static void Remove(BasePlayer player)
                {
                    if (Exists(player))
                    {
                        storedData.Heroes.Remove(player.userID);
                        SaveData();
                    }
                }

                public static bool Exists(BasePlayer player)
                {
                    if (storedData.Heroes.Exists(x => x == player.userID))
                    {
                        return true;
                    }
                    return false;
                }
            }

            public class Bandits
            {
                public static void Add(BasePlayer player)
                {
                    if (!Exists(player))
                    {
                        storedData.Bandits.Add(player.userID);
                    }
                }

                public static void Remove(BasePlayer player)
                {
                    if (Exists(player))
                    {
                        storedData.Bandits.Remove(player.userID);
                        SaveData();
                    }
                }

                public static bool Exists(BasePlayer player)
                {
                    if (storedData.Bandits.Exists(x => x == player.userID))
                    {
                        return true;
                    }
                    return false;
                }
            }

            public static void SaveData() => Interface.Oxide.DataFileSystem.WriteObject("KarmaKills", storedData);
        }

        #endregion Data

        #region Lang

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Add_PlayerKill"] =
                "You've been awarded <color=orange>{0}</color> Karma for killing player <color=orange>{1}</color>",
                ["Add_AnimalKill"] =
                "You've been awareded <color=orange>{0}</color> Karma for killing a <color=orange>{1}</color>",
                ["Remove_PlayerKill"] =
                "You have lost <color=orange>{0}</color> Karma for killing player <color=orange>{1}</color>",
                ["Remove_AnimalKill"] =
                "You have lost <color=orange>{0}</color> Karma for killing a <color=orange>{1}</color>",

                ["Chat_KillStreak"] =
                "<color=orange>{0}</color> is now on a <color=orange>{1}</color> killstreak, kill the Bandit for a <color=orange>{2} Karma reward.",
                ["Chat_Reward"] =
                "<color=orange>{0}</color> is now a Hero for killing a Bandit named <color=orange>{1}</color>, the Hero was rewarded <color=orange>{2}</color> Karma.",
                ["Chat_Punishment"] =
                "<color=orange>{0}</color> is now a Bandit for killing a Hero named <color=orange>{1}</color>, the Bandit has lost <color=orange>{2}</color> Karma.",

                ["Cmd_CurrentBandits"] = "The current Bandits online are \n{0}",
                ["Cmd_NoBandits"] = "There's currently no Bandits on the server",
                ["Cmd_CurrentHeroes"] = "The current Heroes online are \n{0}",
                ["Cmd_NoHeroes"] = "There's currently no Heroes on the server",
            }, this);
        }

        #endregion Lang

        #region Methods

        private void CheckStatus(BasePlayer player)
        {
            if (Data.Killstreaks.Exists(player))
            {
                if (Data.Killstreaks.GetStreak(player) > configFile.Killstreak.Amount)
                {
                    if (!Data.Bandits.Exists(player))
                    {
                        Data.Bandits.Add(player);
                        foreach (var target in BasePlayer.activePlayerList)
                            PrintToChat(player,
                                Lang("Chat_KillStreak", target.UserIDString, player.displayName,
                                    Data.Killstreaks.GetStreak(player)));
                    }
                }
            }
        }

        private bool WasBandit(BasePlayer player)
        {
            if (Data.Bandits.Exists(player))
            {
                return true;
            }
            return false;
        }

        private bool WasHero(BasePlayer player)
        {
            if (Data.Heroes.Exists(player))
            {
                return true;
            }
            return false;
        }

        private string GetAnimalName(string name)
        {
            if (name.Contains("bear"))
                return "Bear";
            if (name.Contains("boar"))
                return "Boar";
            if (name.Contains("chicken"))
                return "Chicken";
            if (name.Contains("horse"))
                return "Horse";
            if (name.Contains("stag"))
                return "Stag";
            if (name.Contains("wolf"))
                return "Wolf";
            return null;
        }

        #endregion Methods

        #region Hooks

        private void Loaded()
        {
            SaveConfig();
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            if (!KarmaSystem)
                PrintWarning("KarmaSystem is required for this plugin to function properly!");
            if (configFile.AnimalKills.AddKarma.Enabled && configFile.AnimalKills.RemoveKarma.Enabled)
                PrintWarning(
                    "It's not recommended to have 'AddKarma' AND 'RemoveKarma' enabled at the same time (AnimalKills)");
            if (configFile.PlayerKills.AddKarma.Enabled && configFile.PlayerKills.RemoveKarma.Enabled)
                PrintWarning(
                    "It's not recommended to have 'AddKarma' AND 'RemoveKarma' enabled at the same time (PlayerKills)");
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (info == null || entity == null) return;

            var player = info.Initiator?.ToPlayer();
            var attacker = info.Initiator?.ToPlayer();

            if (player == null) return;
            if (attacker != null && attacker.userID == player.userID)
                return;

            if (entity.ToPlayer() != null)
            {
                if (configFile.PlayerKills.AddKarma.Enabled)
                {
                    KarmaSystem?.Call("AddKarma", covalence.Players.FindPlayerById(player.UserIDString),
                        configFile.PlayerKills.AddKarma.Amount);
                    PrintToChat(player,
                        Lang("Add_PlayerKill", player.UserIDString, configFile.PlayerKills.AddKarma.Amount,
                            entity.ToPlayer().displayName));
                }
                else
                {
                    KarmaSystem?.Call("RemoveKarma", covalence.Players.FindPlayerById(player.UserIDString),
                        configFile.PlayerKills.RemoveKarma.Amount);
                    PrintToChat(player,
                        Lang("Remove_PlayerKill", player.UserIDString, configFile.PlayerKills.RemoveKarma.Amount,
                            entity.ToPlayer().displayName));
                }

                if (Data.Killstreaks.Exists(player))
                {
                    Data.Killstreaks.IncrementStreak(player);
                    CheckStatus(player);
                }
                else
                    Data.Killstreaks.Add(player);

                if (Data.Killstreaks.Exists(entity.ToPlayer()))
                    Data.Killstreaks.Remove(player);

                if (WasBandit(entity.ToPlayer()) && configFile.Bandit.AddKarma.Enabled)
                {
                    foreach (var p in BasePlayer.activePlayerList)
                        PrintToChat(p,
                            Lang("Chat_Reward", player.UserIDString, player.displayName, entity.ToPlayer().displayName,
                                configFile.Bandit.AddKarma.Amount));

                    Data.Bandits.Remove(entity.ToPlayer());
                    Data.Heroes.Add(player);
                }
                else if (WasHero(entity.ToPlayer()) && configFile.Hero.RemoveKarma.Enabled)
                {
                    foreach (var p in BasePlayer.activePlayerList)
                        PrintToChat(p,
                            Lang("Chat_Punishment", player.UserIDString, player.displayName,
                                entity.ToPlayer().displayName, configFile.Hero.RemoveKarma.Amount));

                    Data.Heroes.Remove(entity.ToPlayer());
                    Data.Bandits.Add(player);
                }
            }
            else if (entity.name.ToLower().Contains("rust.ai"))
            {
                if (configFile.AnimalKills.AddKarma.Enabled)
                {
                    KarmaSystem?.Call("AddKarma", covalence.Players.FindPlayerById(player.UserIDString),
                        configFile.AnimalKills.AddKarma.Amount);
                    PrintToChat(player,
                        Lang("Add_AnimalKill", player.UserIDString, configFile.AnimalKills.AddKarma.Amount,
                            GetAnimalName(entity.name)));
                }
                if (configFile.AnimalKills.RemoveKarma.Enabled)
                {
                    KarmaSystem?.Call("RemoveKarma", covalence.Players.FindPlayerById(player.UserIDString),
                        configFile.AnimalKills.RemoveKarma.Amount);
                    PrintToChat(player,
                        Lang("Remove_AnimalKill", player.UserIDString, configFile.AnimalKills.RemoveKarma.Amount,
                            GetAnimalName(entity.name)));
                }
            }
        }

        [ChatCommand("bandits")]
        private void banditCmd(BasePlayer player, string command, string[] args)
        {
            var banditNames = new List<string>();

            foreach (var bandit in storedData.Bandits)
            {
                var banditPlayer = covalence.Players.FindPlayerById(bandit.ToString());
                if (banditPlayer.IsConnected)
                    banditNames.Add(banditPlayer.Name);
            }

            if (banditNames.Count > 0)
                PrintToChat(player,
                    Lang("Cmd_CurrentBandits", player.UserIDString, string.Join(",", banditNames.ToArray())));
            else
                PrintToChat(player, Lang("Cmd_NoBandits", player.UserIDString));
        }

        [ChatCommand("heroes")]
        private void heroCmd(BasePlayer player, string command, string[] args)
        {
            var heroNames = new List<string>();

            foreach (var hero in storedData.Bandits)
            {
                var heroPlayer = covalence.Players.FindPlayerById(hero.ToString());
                if (heroPlayer.IsConnected)
                    heroNames.Add(heroPlayer.Name);
            }

            if (heroNames.Count > 0)
                PrintToChat(player,
                    Lang("Cmd_CurrentHeroes", player.UserIDString, string.Join(",", heroNames.ToArray())));
            else
                PrintToChat(player, Lang("Cmd_NoHeroes", player.UserIDString));
        }

        #endregion Hooks
    }
}
