using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;

using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("VoteForMoney", "Frenk92", "1.0.5")]
    class VoteForMoney : RustPlugin
    {
        [PluginReference]
        Plugin Economics, ServerRewards, Kits;

        DateTime wipeTime;
        const string permAdmin = "voteformoney.admin";

        #region Sites

        class SiteLinks
        {
            public string _vote { get; set; }
            public string _get { get; set; }
            public string _put { get; set; }
        }

        Dictionary<string, SiteLinks> Site = new Dictionary<string, SiteLinks>
        {
            {
                "Rust-Servers",
                new SiteLinks
                {
                    _vote = "http://rust-servers.net/server/",
                    _get = "http://rust-servers.net/api/?action=custom&object=plugin&element=reward&key={0}&steamid={1}",
                    _put = ""
                }
            },
            {
                "TopRustServers",
                new SiteLinks
                {
                    _vote =  "http://toprustservers.com/",
                    _get = "http://api.toprustservers.com/api/get?plugin=voter&key={0}&uid={1}",
                    _put = "http://api.toprustservers.com/api/put?plugin=voter&key={0}&uid={1}"
                }
            },
            {
                "BeancanIO",
                new SiteLinks
                {
                    _vote = "http://beancan.io/server/",
                    _get =  "http://beancan.io/vote/get/{0}/{1}",
                    _put = "http://beancan.io/vote/put/{0}/{1}"
                }
            }
        };

        #endregion

        #region Config

        ConfigData _config;

        class ConfigData
        {
            public bool InitCheck { get; set; }
            public bool AllGroupsGetDefault { get; set; }
            public string Prefix { get; set; }
            public string BlockVote { get; set; }

            public static ConfigData DefaultConfig()
            {
                return new ConfigData
                {
                    InitCheck = true,
                    AllGroupsGetDefault = false,
                    Prefix = "<color=#808000ff><b>VoteForMoney:</b></color>",
                    BlockVote = "0.6"
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<ConfigData>();
                if (_config == null) LoadDefaultConfig();
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning(Lang("NoConfig"));
            _config = ConfigData.DefaultConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion

        #region Data

        Dictionary<ulong, PlayerVote> Users = new Dictionary<ulong, PlayerVote>();
        class PlayerVote
        {
            public string Name { get; set; }
            public Dictionary<string, SitesVote> Sites { get; set; }

            public PlayerVote(string Name)
            {
                this.Name = Name;
                Sites = new Dictionary<string, SitesVote>();
            }
        }

        class SitesVote
        {
            public int Votes { get; set; }
            public string ExpDate { get; set; }
            public List<string> KitsNotClaimed { get; set; }

            public SitesVote()
            {
                Votes = 0;
                ExpDate = DateTime.Now.ToString();
                KitsNotClaimed = new List<string>();
            }
        }

        RewardsData Rewards;
        class RewardsData
        {
            public Dictionary<string, Dictionary<string, string>> Groups { get; set; }
            public Dictionary<int, Dictionary<string, string>> TopVoters { get; set; } //Coming Soon

            public static RewardsData DefaultRewards()
            {
                return new RewardsData
                {
                    Groups = new Dictionary<string, Dictionary<string, string>>
                {
                    { "default.1" , new Dictionary<string, string>
                        {
                            ["money"] = "250",
                            ["rp"] = "30",
                            ["kit"] = ""
                        }
                    },
                    { "default.2", new Dictionary<string, string>() },
                    { "vip.1", new Dictionary<string, string>() }
                },
                    TopVoters = new Dictionary<int, Dictionary<string, string>>
                {
                    { 1, new Dictionary<string, string>
                        {
                            ["money"] = "1000",
                            ["rp"] = "100",
                            ["kit"] = ""
                        }
                    },
                    { 2, new Dictionary<string, string>() },
                    { 3, new Dictionary<string, string>() }
                }
                };
            }
        }

        Dictionary<string, SiteSettings> Settings = new Dictionary<string, SiteSettings>();
        class SiteSettings
        {
            public string ID { get; set; }
            public string Key { get; set; }
            public string Interval { get; set; }

            public SiteSettings()
            {
                ID = "";
                Key = "";
                Interval = "1.0";
            }
        }

        string FileDestination(string name) => string.Format($"{Title}/{name}");
        private void LoadData<T>(ref T data, string filename) { var file = FileDestination(filename); data = Interface.Oxide.DataFileSystem.ReadObject<T>(file); }
        private void SaveData<T>(T data, string filename) { var file = FileDestination(filename); Interface.Oxide.DataFileSystem.WriteObject(file, data); }

        #endregion

        #region Hooks

        void Loaded()
        {
            AddCovalenceCommand("editvote", "cmdEditVote", permAdmin);
            AddCovalenceCommand("vr", "cmdVoteRewards", permAdmin);
            LoadMessages();

            //Load Data
            var fileSystem = Interface.Oxide.DataFileSystem;

            if (!fileSystem.ExistsDatafile(FileDestination("Rewards")))
            {
                Rewards = RewardsData.DefaultRewards();
                SaveData(Rewards, "Rewards");
            }
            else
                LoadData(ref Rewards, "Rewards");

            if (!fileSystem.ExistsDatafile(FileDestination("Sites")))
            {
                foreach (var s in Site)
                    Settings.Add(s.Key, new SiteSettings());
                SaveData(Settings, "Sites");
            }
            else
                LoadData(ref Settings, "Sites");

            LoadData(ref Users, "VoteData");
        }

        void OnNewSave(string name)
        {
            foreach (var p in Users.Values)
                foreach (var s in p.Sites.Values)
                {
                    s.Votes = 0;
                    s.KitsNotClaimed = new List<string>();
                }

            SaveData(Users, "VoteData");
        }

        private void OnServerInitialized()
		{
            WipeCooldown();
		}

        List<ulong> Join = new List<ulong>();

        void OnPlayerInit(BasePlayer player)
        {
            if (_config.InitCheck && !Join.Contains(player.userID)) Join.Add(player.userID);
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (_config.InitCheck && Join.Contains(player.userID)) Join.Remove(player.userID);
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (_config.InitCheck && Join.Contains(player.userID))
            {
                CheckVote(player);
                Join.Remove(player.userID);
            }
        }

        #endregion

        #region Commands

        #region Chat

        [ChatCommand("vote")]
        void cmdVote(BasePlayer player, string command, string[] args)
        {
            if (WipeBlocked(player)) return;
            CheckVote(player);
        }

        [ChatCommand("claimkit")]
        void cmdClaimKit(BasePlayer player, string command, string[] args)
        {
            if (WipeBlocked(player)) return;

            var userData = GetPlayerData(player.userID, SteamName(player));

            var i = 0;
            var error = false;
            foreach (var s in Site)
            {
                if (error) break;
                List<string> list = new List<string>(userData.Sites[s.Key].KitsNotClaimed);
                if (list.Count() > 0)
                {
                    foreach (var kit in list)
                    {
                        var flag = Kits?.Call("CanRedeemKit", player, kit, true);
                        if (flag is string || flag == null)
                        {
                            error = true;
                            break;
                        }
                        var succecss = Kits?.Call("GiveKit", player, kit);
                        if (succecss is string || succecss == null)
                        {
                            error = true;
                            break;
                        }
                        userData.Sites[s.Key].KitsNotClaimed.Remove(kit);
                    }
                }
                else ++i;
            }

            if (i == 3)
                MessageChat(player, "AlreadyClaimed");
            else if (error)
                MessageChat(player, "ErrorKit");
            else
            {
                MessageChat(player, "RewardKit");
                SaveData(Users, "VoteData");
            }
        }

        #endregion

        #region Editor

        void cmdEditVote(IPlayer player, string command, string[] args)
        {
            if (args.Length == 0) return;

            for(var i = 0; i < args.Length; i++)
            {
                var error = "";
                var param = args[i].ToLower();
                var value = "";
                switch (param)
                {
                    case "check":
                        {
                            var flag = false;
                            if (args.Length > i + 1 && bool.TryParse(args[i + 1], out flag))
                            {
                                _config.InitCheck = flag;
                                ++i;
                            }
                            else
                                _config.InitCheck = !_config.InitCheck;
                            value = _config.InitCheck.ToString();
                            break;
                        }
                    case "alldefault":
                        {
                            var flag = false;
                            if (args.Length > i + 1 && bool.TryParse(args[i + 1], out flag))
                            {
                                _config.AllGroupsGetDefault = flag;
                                ++i;
                            }
                            else
                                _config.AllGroupsGetDefault = !_config.AllGroupsGetDefault;
                            value = _config.AllGroupsGetDefault.ToString();
                            break;
                        }
                    case "prefix":
                        {
                            if (args.Length == i + 1)
                                error = Lang("MissingArg", player.Id);
                            else
                                _config.Prefix = value = args[++i];
                            break;
                        }
                    case "block":
                        {
                            if (args.Length == i + 1)
                            {
                                error = Lang("MissingArg", player.Id);
                                break;
                            }
                            var f = args[++i].Split('.');
                            if (f.Length == 2 && f[0].All(char.IsDigit) && f[1].All(char.IsDigit))
                            {
                                _config.BlockVote = value = args[i];
                                WipeCooldown();
                            }
                            else
                                error = Lang("InvalidValue", player.Id, args[i]);
                            break;
                        }
                    default:
                        {
                            error = Lang("InvalidArg", player.Id, param);
                            break;
                        }
                }

                if (error != "")
                    player.Reply(error);
                else
                {
                    player.Reply(Lang("Edited", player.Id, param, value));
                    SaveConfig();
                }
            }
        }

        string _group = "";

        void cmdVoteRewards(IPlayer player, string command, string[] args)
        {
            if (args.Length < 2)
            {
                player.Reply(Lang("MissingArg", player.Id));
                return;
            }

            var msg = "";
            var error = "";
            switch (args[0].ToLower())
            {
                case "add":
                    {
                        if (args.Length < 3)
                        {
                            error = Lang("MissingArg", player.Id);
                            break;
                        }

                        var p = args[1].ToLower();
                        if (p == "group")
                        {
                            var x = args[2].Split('.');
                            if (x.Length < 2 || !x[1].All(char.IsDigit))
                            {
                                error = Lang("InvalidValue", player.Id, args[2]);
                            }
                            else if (!permission.GroupExists(x[0]))
                            {
                                error = Lang("NoGroup", player.Id, x[0]);
                            }
                            else
                            {
                                Rewards.Groups.Add(args[2], new Dictionary<string, string>());
                                _group = args[2];
                                msg = Lang("GroupAdded", player.Id, args[2]);
                            }
                        }
                        else
                        {
                            if (_group == "")
                            {
                                error = Lang("NotSelected", player.Id);
                                break;
                            }

                            var rg = Rewards.Groups[_group];
                            if (rg.ContainsKey(p) || (p == "cmd" && rg.ContainsKey(args[2])))
                            {
                                error = Lang("AlreadyExists", player.Id);
                                break;
                            }
                            switch (p)
                            {
                                case "money":
                                    {
                                        if (!Economics)
                                        {
                                            error = Lang("NoPlugin", player.Id, "Economics");
                                            break;
                                        }
                                        if (!args[2].All(char.IsDigit))
                                            error = Lang("InvalidValue", player.Id, args[2]);
                                        else
                                            rg.Add("money", args[2]);
                                        break;
                                    }
                                case "rp":
                                    {
                                        if (!ServerRewards)
                                        {
                                            error = Lang("NoPlugin", player.Id, "ServerRewards");
                                            break;
                                        }
                                        if (!args[2].All(char.IsDigit))
                                            error = Lang("InvalidValue", player.Id, args[2]);
                                        else
                                            rg.Add("rp", args[2]);
                                        break;
                                    }
                                case "kit":
                                    {
                                        if (!Kits)
                                        {
                                            error = Lang("NoPlugin", player.Id, "Rust Kits");
                                            break;
                                        }
                                        var flag = (bool)Kits?.Call("isKit", args[2]);
                                        if (!flag)
                                            error = Lang("NoKit", player.Id, args[2]);
                                        else
                                            rg.Add("kit", args[2]);
                                        break;
                                    }
                                case "cmd":
                                    {
                                        if (args.Length < 4)
                                            error = Lang("MissingArg", player.Id);
                                        else
                                        {
                                            rg.Add(args[2], args[3]);
                                            p = args[2];
                                        }
                                        break;
                                    }
                                default:
                                    {
                                        error = Lang("InvalidArg", player.Id, args[1]);
                                        break;
                                    }
                            }
                            if (error == "")
                                msg = Lang("RewardAdded", player.Id, p);
                        }
                        break;
                    }
                case "remove":
                    {
                        var p = args[1].ToLower();
                        if (p == "group")
                        {
                            if (args.Length < 3)
                            {
                                error = Lang("MissingArg", player.Id);
                                break;
                            }

                            if (!Rewards.Groups.ContainsKey(args[2]))
                                error = Lang("NotFound", player.Id, args[2]);
                            else
                            {
                                Rewards.Groups.Remove(args[2]);
                                if (_group == args[2]) _group = "";
                                msg = Lang("GroupRemoved", player.Id, args[2]);
                            }
                        }
                        else
                        {
                            if (_group == "")
                            {
                                error = Lang("NotSelected", player.Id);
                                break;
                            }

                            var rg = Rewards.Groups[_group];
                            if (p == "cmd")
                            {
                                if (args.Length < 3)
                                    error = Lang("MissingArg", player.Id);
                                else if (!rg.ContainsKey(args[2]))
                                    error = Lang("NoReward", player.Id, args[2]);
                                else
                                {
                                    rg.Remove(args[2]);
                                    p = args[2];
                                }
                            }
                            else
                            {
                                if (!rg.ContainsKey(p))
                                {
                                    error = Lang("NoReward", player.Id, p);
                                    break;
                                }
                                switch (p)
                                {
                                    case "money":
                                        {
                                            rg.Remove("money");
                                            break;
                                        }
                                    case "rp":
                                        {
                                            rg.Remove("rp");
                                            break;
                                        }
                                    case "kit":
                                        {
                                            rg.Remove("kit");
                                            break;
                                        }
                                    default:
                                        {
                                            error = Lang("InvalidArg", player.Id, args[1]);
                                            break;
                                        }
                                }
                            }
                            if (error == "")
                                msg = Lang("RewardRemoved", player.Id, p);
                        }
                        break;
                    }
                case "edit":
                    {
                        if (args.Length < 3)
                        {
                            error = Lang("MissingArg", player.Id);
                            break;
                        }
                        if (_group == "")
                        {
                            error = Lang("NotSelected", player.Id);
                            break;
                        }

                        var p = args[1].ToLower();
                        var rg = Rewards.Groups[_group];
                        if (p == "cmd")
                        {
                            if (args.Length < 4)
                                error = Lang("MissingArg", player.Id);
                            else if (!rg.ContainsKey(args[2]))
                                error = Lang("NoReward", player.Id, args[2]);
                            else
                            {
                                rg[args[2]] = args[3];
                                msg = Lang("Edited", player.Id, args[2], args[3]);
                            }
                        }
                        else
                        {
                            if (!rg.ContainsKey(p))
                            {
                                error = Lang("NoReward", player.Id, p);
                                break;
                            }
                            switch (p)
                            {
                                case "money":
                                    {
                                        if (!args[2].All(char.IsDigit))
                                            error = Lang("InvalidValue", player.Id, args[2]);
                                        else
                                            rg["money"] = args[2];
                                        break;
                                    }
                                case "rp":
                                    {
                                        if (!args[2].All(char.IsDigit))
                                            error = Lang("InvalidValue", player.Id, args[2]);
                                        else
                                            rg["rp"] = args[2];
                                        break;
                                    }
                                case "kit":
                                    {
                                        var flag = (bool)Kits?.Call("isKit", args[2]);
                                        if (!flag)
                                            error = Lang("NoKit", player.Id, args[2]);
                                        else
                                            rg["kit"] = args[2];
                                        break;
                                    }
                                default:
                                    {
                                        error = Lang("InvalidArg", player.Id, args[1]);
                                        break;
                                    }
                            }
                            if (error == "")
                                msg = Lang("Edited", player.Id, args[1], args[2]);
                        }
                        break;
                    }
                case "set":
                    {
                        if (!Rewards.Groups.ContainsKey(args[1]))
                            error = Lang("NotFound", player.Id, args[1]);
                        else
                        {
                            _group = args[1];
                            msg = Lang("Selected", player.Id, args[1]);
                        }
                        break;
                    }
                case "list":
                    {
                        switch (args[1].ToLower())
                        {
                            case "groups":
                                msg = Lang("GroupsList", player.Id, string.Join(", ", Rewards.Groups.Keys));
                                break;
                            case "rewards":
                                if (_group == "")
                                {
                                    error = Lang("NotSelected", player.Id);
                                    break;
                                }
                                var list = new StringBuilder();
                                foreach (var r in Rewards.Groups[_group])
                                    list.Append($"\n{r.Key}: {r.Value}");
                                msg = Lang("RewardsList", player.Id, _group, list.ToString());
                                break;
                            default:
                                {
                                    error = Lang("InvalidArg", player.Id, args[1]);
                                    break;
                                }
                        }
                        break;
                    }
                default:
                    {
                        error = Lang("InvalidArg", player.Id, args[0]);
                        break;
                    }
            }

            if (error != "")
                player.Reply(error);
            else
            {
                SaveData(Rewards, "Rewards");
                player.Reply(msg);
            }
        }

        #endregion

        #endregion

        #region Methods

        void CheckVote(BasePlayer player)
        {
            var userData = GetPlayerData(player.userID, SteamName(player));
            
            var time = DateTime.Now;
            foreach(var s in Site)
            {
                if(Settings[s.Key].Key != "")
                {
                    var expdate = Convert.ToDateTime(userData.Sites[s.Key].ExpDate);
                    if (time < expdate)
                    {
                        MessageChat(player, "AlreadyVoted", s.Key);
                        MessageChat(player, "NextVote", expdate);
                    }
                    else
                        GetWebRequest(player.UserIDString, s.Value._get, Settings[s.Key].Key, (code, response) => GetCallback(code, response, player, s));
                }
            }
        }

        void GetCallback(int code, string response, BasePlayer player, KeyValuePair<string, SiteLinks> s)
        {
            if (response == null || code != 200)
            {
                Puts(Lang("ConsNoAnswer", null, code, s.Key));
                MessageChat(player, "NoAnswer", s.Key);
                return;
            }

            switch (response)
            {
                case "0":
                    {
                        MessageChat(player, "NoVote", s.Key, s.Value._vote, Settings[s.Key].ID);
                        break;
                    }
                case "1":
                    {
                        if (s.Value._put != "")
                            GetWebRequest(player.UserIDString, s.Value._put, Settings[s.Key].Key, (putcode, putresp) => PutCallback(putcode, putresp, player, s));
                        else
                            GetReward(player, s.Key);
                        break;
                    }
                case "2":
                    {
                        MessageChat(player, "AlreadyVoted", s.Key);
                        break;
                    }
            }
        }

        void PutCallback(int code, string response, BasePlayer player, KeyValuePair<string, SiteLinks> s)
        {
            if (response == null || code != 200)
            {
                Puts(Lang("ConsNoAnswer", null, code, s.Key));
                MessageChat(player, "NoAnswer", s.Key);
                return;
            }

            if (response == "1") GetReward(player, s.Key);
        }

        void GetReward(BasePlayer player, string s)
        {
            MessageChat(player, "Thanks", s);

            var userData = GetPlayerData(player.userID, SteamName(player));
            userData.Sites[s].Votes++;
            if (userData.Sites[s].KitsNotClaimed.Count > 0) userData.Sites[s].KitsNotClaimed.Clear();

            var getdefault = true;
            foreach (var key in Rewards.Groups.Keys)
            {
                var g = key.Split('.')[0];
                if (g == "default") continue;
                if (permission.UserHasGroup(player.UserIDString, g))
                {
                    getdefault = false;
                    break;
                }
            }

            var totmoney = 0;
            var totrp = 0;
            var kit = false;
            var kiterror = false;
            List<string> commands = new List<string>();
            foreach(var g in Rewards.Groups)
            {
                if (g.Value.Count == 0) continue;
                var args = g.Key.Split('.');
                if ((args[0] == "default" && !getdefault && !_config.AllGroupsGetDefault)
                    || !permission.UserHasGroup(player.UserIDString, args[0])) continue;
                if (!permission.GroupExists(args[0]))
                {
                    PrintWarning(Lang("NoGroup", null, args[0]));
                    continue;
                }
                var m = 0;
                if(args.Length <= 1 || !int.TryParse(args[1], out m))
                {
                    PrintWarning(Lang("InvalidReward", null, g.Key));
                    continue;
                }
                if (userData.Sites[s].Votes < m || (userData.Sites[s].Votes % m) != 0) continue;

                foreach (var r in g.Value)
                {
                    switch(r.Key)
                    {
                        case "money":
                            {
                                if (!Economics) break;
                                var eco = Convert.ToDouble(r.Value);
                                Economics.Call("Deposit", player.userID, eco);
                                totmoney += Convert.ToInt32(r.Value);
                                break;
                            }
                        case "rp":
                            {
                                if (!ServerRewards) break;
                                var p = Convert.ToInt32(r.Value);
                                ServerRewards.Call("AddPoints", new object[] { player.userID, p });
                                totrp += Convert.ToInt32(r.Value);
                                break;
                            }
                        case "kit":
                            {
                                if (!Kits || r.Value == "") break;
                                var flag = Kits?.Call("CanRedeemKit", player, r.Value, true);
                                if (flag is string || flag == null || kiterror)
                                {
                                    if (!kiterror) kiterror = true;
                                    userData.Sites[s].KitsNotClaimed.Add(r.Value);
                                }
                                else
                                {
                                    var success = Kits?.Call("GiveKit", player, r.Value);
                                    if (flag is string || flag == null)
                                    {
                                        kiterror = true;
                                        userData.Sites[s].KitsNotClaimed.Add(r.Value);
                                    }
                                    else if (!kit) kit = true;
                                }
                                break;
                            }
                        default:
                            {
                                rust.RunServerCommand(r.Value.Replace("$player.id", player.UserIDString).Replace("$player.name", player.displayName));
                                if (!commands.Contains(r.Key)) commands.Add(r.Key);
                                break;
                            }
                    }
                }
            }

            if(totmoney > 0) MessageChat(player, "RewardCoins", totmoney);
            if(totrp > 0) MessageChat(player, "RewardRP", totrp);
            if(kiterror) MessageChat(player, "ErrorKits");
            else if(kit) MessageChat(player, "RewardKit");
            foreach (var c in commands) MessageChat(player, "RewardCommand", c);

            try
            {
                var i = Settings[s].Interval.Split('.').Select(n => Convert.ToInt32(n)).ToArray();
                var newdate = DateTime.Now + new TimeSpan(i[0], i[1], 0, 0);
                userData.Sites[s].ExpDate = newdate.ToString();
                SaveData(Users, "VoteData");
            }
            catch
            {
                MessageChat(player, "InvalidInterval");
                var newdate = DateTime.Now + new TimeSpan(1, 0, 0, 0);
                userData.Sites[s].ExpDate = newdate.ToString();
                SaveData(Users, "VoteData");
            }
        }

        void GetWebRequest(string userID, string reqlink, string key, Action<int, string> callback) => webrequest.Enqueue(string.Format(reqlink, key, userID), null, callback, this);

        #endregion

        #region Utility

        PlayerVote GetPlayerData(ulong userID, string name)
        {
            PlayerVote userData;
            if (!Users.TryGetValue(userID, out userData))
            {
                Users[userID] = userData = new PlayerVote(name);
                foreach (var s in Site) userData.Sites.Add(s.Key, new SitesVote());
                SaveData(Users, "VoteData");
            }
            return userData;
        }

        bool WipeBlocked(BasePlayer player)
        {
            var tm = wipeTime - DateTime.UtcNow;
            if (tm < TimeSpan.Zero) return false;
            var p = (int)tm.TotalDays;
            var key = "DaysFormat";
            if (tm.TotalDays < 1)
            {
                if (tm.TotalHours < 1)
                {
                    if (tm.TotalMinutes < 1)
                    {
                        key = "SecondsFormat";
                        p = (int)tm.TotalSeconds;
                    }
                    else
                    {
                        key = "MinutesFormat";
                        p = (int)tm.TotalMinutes;
                    }
                }
                else
                {
                    key = "HoursFormat";
                    p = (int)tm.TotalHours;
                }
            }

            MessageChat(player, key, p);
            return true;
        }

        void WipeCooldown()
        {
            var i = _config.BlockVote.Split('.').Select(n => Convert.ToInt32(n)).ToArray();
            wipeTime = SaveRestore.SaveCreatedTime + new TimeSpan(i[0], i[1], 0, 0);
        }

        string SteamName(BasePlayer player) => player.net.connection.username;

        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);

        #endregion

        #region Localization

        void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Thanks"] = "Thanks for voted on {0}",
                ["RewardCoins"] = "Coins reward: {0}",
                ["RewardRP"] = "RP reward: {0}",
                ["RewardKit"] = "All kist rewarded.",
                ["RewardCommand"] = "{0} rewarded.",
                ["NextVote"] = "Next Vote: {0}",
                ["NoVote"] = "You have not voted yet on {0}.\nLink to vote: {1}{2}",
                ["AlreadyVoted"] = "You have already voted on {0}.",
                ["AlreadyClaimed"] = "You have already claimed all kits.",
                ["AlreadyExists"] = "This reward is already existing.",
                ["Edited"] = "\"{0}\" edited to: {1}",
                ["Selected"] = "Group \"{0}\" has been selected.",
                ["GroupAdded"] = "Group \"{0}\" has been added.",
                ["GroupRemoved"] = "Group \"{0}\" has been removed.",
                ["RewardAdded"] = "Reward \"{0}\" has been added.",
                ["RewardRemoved"] = "Reward \"{0}\" has been removed.",
                ["GroupsList"] = "Groups list: {0}",
                ["RewardsList"] = "\"{0}\" rewards list: {1}",
                ["NoPlugin"] = "Plugin \"{0}\" not loaded.",
                ["NoGroup"] = "Group \"{0}\" doesn't exist.",
                ["NoKit"] = "Kit \"{0}\" doesn't exist.",
                ["NoReward"] = "Reward \"{0}\" not found.",
                ["NotFound"] = "Group \"{0}\" not found.",
                ["NotSelected"] = "Group not selected. Use \"/vr set\" to select one (Example: /vr set default.1).",
                ["NoAnswer"] = "No answer from {0}. Try later.",
                ["NoConfig"] = "Could not read config file. Creating new one...",
                ["ConsNoAnswer"] = "Error: {0} - Couldn't get an answer from {1}",
                ["MissingArg"] = "One or more arguments are missing.",
                ["ErrorBool"] = "Error. Only 'true' or 'false'.",
                ["ErrorKits"] = "Not all kits have been redeemed. Empty the inventory and redeem them with \"/claimkit\".",
                ["InvalidReward"] = "\"{0}\" is an invalid reward.",
                ["InvalidArg"] = "\"{0}\" is an invalid argument.",
                ["InvalidValue"] = "\"{0}\" is an invalid value.",
                ["SecondsFormat"] = "The vote is blocked for {0} second/s.",
                ["MinutesFormat"] = "The vote is blocked for {0} minute/s.",
                ["HoursFormat"] = "The vote is blocked for {0} hour/s.",
                ["DaysFormat"] = "The vote is blocked for {0} day/s.",
                //["Help"] = "\n============== VOTE HELP =============\nType: /editvote \"COMMAND\" \"VALUE\" (Example: /editvote check true)\n============== VOTE HELP =============",
            }, this);
        }

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        void MessageChat(BasePlayer player, string key, params object[] args)
        {
            var msg = Lang(key, player.UserIDString, args);
            PrintToChat(player, $"{_config.Prefix} {msg}");
        }

        #endregion
    }
}
