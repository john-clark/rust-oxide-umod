using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("KarmaSystem", "Ryan", "1.1.0")]
    [Description("Allows players to upvote/downvote other players")]
    internal class KarmaSystem : CovalencePlugin
    {
        #region Config

        private ConfigFile configFile;

        public class ConfigFile
        {
            public Upvote UpvoteSettings { get; set; }
            public Downvote DownvoteSettings { get; set; }
            public Ranking Top { get; set; }
            public Ranking Bottom { get; set; }
            public bool EnableKarmaCommand { get; set; }
            public float Cooldown { get; set; }
            public bool OneVotePerPlayer { get; set; }

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    UpvoteSettings = new Upvote
                    {
                        Amount = 1,
                        EnableCommand = true
                    },
                    DownvoteSettings = new Downvote
                    {
                        Amount = 1,
                        EnableCommand = true
                    },
                    Top = new Ranking()
                    {
                        ShowAmount = 5
                    },
                    Bottom = new Ranking()
                    {
                        ShowAmount = 5,
                    },
                    EnableKarmaCommand = true,
                    Cooldown = 60,
                    OneVotePerPlayer = false
                };
            }
        }

        public class Upvote
        {
            public int Amount { get; set; }
            public bool EnableCommand { get; set; }
        }

        public class Downvote
        {
            public int Amount { get; set; }
            public bool EnableCommand { get; set; }
        }

        public class Ranking
        {
            public int ShowAmount { get; set; }
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

        protected override void SaveConfig()
        {
            Config.WriteObject(configFile);
        }

        #endregion

        #region Lang

        private string Lang(string key, string id = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, id), args);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                // Commands
                ["Cmd_Upvote"] =
                "You've upvoted player <color=orange>{0}</color>, they now have <color=orange>{1}</color> karma",
                ["Cmd_Downvote"] =
                "You've downvoted player <color=orange>{0}</color>, they now have <color=orange>{1}</color> karma",
                ["Cmd_Upvoted"] =
                "You've been upvoted by player <color=orange>{0}</color>, you now have <color=orange>{1}</color> karma",
                ["Cmd_Downvoted"] =
                "You've been downvoted by player <color=orange>{0}</color>, you now have <color=orange>{1}</color> karma",
                ["Cmd_Karma"] =
                "You have <color=orange>{0}</color> karma and you have voted for <color=orange>{1}</color> players",
                // Invalid args
                ["Cmd_InvalidArgs"] = "Invalid arguments. Usage: '/<color=orange>{0}</color> {1}'",
                ["Arg_Vote"] = "<player>",
                ["Arg_Ranking"] = "<top> OR <bottom>",
                // FindPlayer
                ["FP_NoPlayers"] = "Found no players with input '<color=orange>{0}</color>'",
                ["FP_MultiplePlayers"] =
                "Found the following players with input '<color=orange>{0}</color>', please refine your input. \n{1}",
                // Cant use
                ["CU_Cooldown"] = "You can't vote for a player for another <color=orange>{0}</color> seconds",
                ["CU_AlreadyVoted"] = "You have already voted for player <color=orange>{0}</color>",
                ["CU_Yourself"] = "You can't vote for yourself, that's cheating!",
                // Ranking
                ["Ranking_Info"] = "- <color=orange>{0}</color> (<color=orange>{1}</color> Karma)"
            }, this);
        }

        #endregion

        #region Data

        private static StoredData storedData;

        public class StoredData
        {
            public Dictionary<string, PlayerData> Players = new Dictionary<string, PlayerData>();
        }

        public class PlayerData
        {
            public PlayerData()
            {
                Karma = 0;
                Cooldown = null;
                Votes = new List<string>();
            }

            public int Karma { get; set; }
            public DateTime? Cooldown { get; set; }
            public List<string> Votes { get; set; }
        }

        public class Data
        {
            public static void AddNew(IPlayer player)
            {
                if (!Exists(player))
                {
                    storedData.Players.Add(player.Id, new PlayerData());
                    Save();
                }
            }

            public static void AddVoted(IPlayer player, string ID)
            {
                if (Exists(player))
                {
                    storedData.Players[player.Id].Votes.Add(ID);
                    Save();
                }
            }

            public static bool Exists(IPlayer player)
            {
                if (storedData.Players.ContainsKey(player.Id)) return true;
                return false;
            }

            public static void Remove(IPlayer player)
            {
                if (Exists(player))
                {
                    storedData.Players.Remove(player.Id);
                    Save();
                }
            }

            public static void Clear()
            {
                storedData.Players.Clear();
                Save();
            }

            public static void Save()
            {
                Interface.Oxide.DataFileSystem.WriteObject("KarmaSystem", storedData);
            }
        }

        #endregion

        #region Methods

        private IPlayer FindPlayer(IPlayer player, string input)
        {
            var Players = new List<IPlayer>();
            foreach (var target in players.FindPlayers(input))
                if (target.IsConnected)
                    Players.Add(target);
            if (Players.Count > 1)
            {
                player.Reply(Lang("FP_MultiplePlayers", player.Id, input,
                    string.Join(", ", Players.Select(x => x.Name).ToArray())));
                return null;
            }
            if (Players.Count == 1)
            {
                var target = Players.First();
                if (!Data.Exists(target))
                    Data.AddNew(target);
                return target;
            }
            player.Reply(Lang("FP_NoPlayers", player.Id, input));
            return null;
        }

        private bool CanVote(IPlayer player, IPlayer target)
        {
            if (player.Id == target.Id)
            {
                player.Reply(Lang("CU_Yourself", player.Id));
                return false;
            }
            if (Data.Exists(player) && storedData.Players[player.Id].Cooldown != null && storedData.Players[player.Id].Cooldown.Value.AddMinutes(configFile.Cooldown) > DateTime.UtcNow)
            {
                player.Reply(Lang("CU_Cooldown", player.Id, (storedData.Players[player.Id].Cooldown.Value.AddMinutes(configFile.Cooldown) - DateTime.UtcNow).TotalSeconds));
                return false;
            }
            if (Data.Exists(player) && storedData.Players[player.Id].Votes.Contains(target.Id) && configFile.OneVotePerPlayer)
            {
                player.Reply(Lang("CU_AlreadyVoted", player.Id, target.Name));
                return false;
            }
            return true;
        }

        #endregion

        #region API

        int GetKarma(IPlayer player)
        {
            if (!Data.Exists(player))
                Data.AddNew(player);
            return storedData.Players[player.Id].Karma;
        }

        void AddKarma(IPlayer player, int amount)
        {
            if (!Data.Exists(player))
                Data.AddNew(player);
            storedData.Players[player.Id].Karma = storedData.Players[player.Id].Karma + amount;
            Data.Save();
        }

        void RemoveKarma(IPlayer player, int amount)
        {
            if (!Data.Exists(player))
                Data.AddNew(player);
            storedData.Players[player.Id].Karma = storedData.Players[player.Id].Karma - amount;
            Data.Save();
        }

        void AddPlayer(IPlayer player)
        {
            if (!Data.Exists(player))
                return;
            Data.AddNew(player);
        }

        void AddVoted(IPlayer player, string ID)
        {
            if (!Data.Exists(player))
                Data.AddNew(player);
            Data.AddVoted(player, ID);
        }

        void OnVoted(IPlayer player, IPlayer target, int amount)
        {
            storedData.Players[player.Id].Cooldown = DateTime.UtcNow;
            Interface.Call("OnKarmaVote", player, target, amount);
        }

        #endregion

        #region Hooks

        private void Unload()
        {
            Data.Save();
        }

        private void Loaded()
        {
            SaveConfig();
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
        }

        [Command("upvote")]
        private void upvoteCmd(IPlayer player, string command, string[] args)
        {
            if (!configFile.UpvoteSettings.EnableCommand)
            {
                return;
            }
            if (args.Length == 0)
            {
                player.Reply(Lang("Cmd_InvalidArgs", player.Id, command, Lang("Arg_Vote", player.Id)));
                return;
            }
            var target = FindPlayer(player, args[0]);
            if (target != null && CanVote(player, target))
            {
                AddKarma(target, configFile.UpvoteSettings.Amount);
                AddVoted(player, target.Id);
                OnVoted(player, target, configFile.UpvoteSettings.Amount);
                var karma = GetKarma(target);
                player.Reply(Lang("Cmd_Upvote", player.Id, target.Name, karma));
                target.Reply(Lang("Cmd_Upvoted", target.Id, player.Name, karma));
            }
        }

        [Command("downvote")]
        private void downvoteCmd(IPlayer player, string command, string[] args)
        {
            if (!configFile.DownvoteSettings.EnableCommand)
            {
                return;
            }
            if (args.Length == 0)
            {
                player.Reply(Lang("Cmd_InvalidArgs", player.Id, command, Lang("Arg_Vote", player.Id)));
                return;
            }
            var target = FindPlayer(player, args[0]);
            if (target != null && CanVote(player, target))
            {
                RemoveKarma(target, configFile.DownvoteSettings.Amount);
                AddVoted(player, target.Id);
                OnVoted(player, target, configFile.DownvoteSettings.Amount);
                var karma = GetKarma(target);
                player.Reply(Lang("Cmd_Downvote", player.Id, target.Name, karma));
                target.Reply(Lang("Cmd_Downvoted", target.Id, player.Name, karma));
            }
        }

        [Command("karma")]
        private void karmaCmd(IPlayer player, string command, string[] args)
        {
            if (!configFile.EnableKarmaCommand)
            {
                return;
            }
            if (args.Length == 0)
            {
                player.Reply(Lang("Cmd_Karma", player.Id, GetKarma(player), storedData.Players[player.Id].Votes.Count));
                return;
            }
            if (args.Length == 1)
            {
                switch (args[0])
                {
                    case "top":
                        foreach (var info in storedData.Players.OrderByDescending(x => x.Value.Karma).Take(configFile.Top.ShowAmount))
                            player.Reply(Lang("Ranking_Info", player.Id, players.FindPlayerById(info.Key).Name, info.Value.Karma));
                        return;

                    case "bottom":
                        foreach (var info in storedData.Players.OrderByDescending(x => x.Value.Karma).Reverse().Take(configFile.Bottom.ShowAmount))
                            player.Reply(Lang("Ranking_Info", player.Id, players.FindPlayerById(info.Key).Name, info.Value.Karma));
                        return;

                    default:
                        var target = FindPlayer(player, args[0]);
                        if (target != null)
                        {
                            if(!Data.Exists(target))
                                Data.AddNew(target);
                            player.Reply(Lang("Ranking_Info", player.Id, target.Name, storedData.Players[target.Id].Karma));
                        }
                        return;
                }
            }
            player.Reply(Lang("Cmd_InvalidArgs", player.Id, command, Lang("Arg_Ranking", player.Id)));
        }

        #endregion
    }
}
