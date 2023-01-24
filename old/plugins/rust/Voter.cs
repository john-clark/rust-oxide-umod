using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using System.Linq;
using System;

namespace Oxide.Plugins
{
    [Info("VoteRewards", "k1lly0u", "2.1.0", ResourceId = 752)]
    class Voter : RustPlugin
    {
        #region Fields 
        [PluginReference] Plugin ServerRewards;
        [PluginReference] Plugin Economics;

        StoredData storedData;
        private DynamicConfigFile data;

        private Dictionary<string, ItemDefinition> itemDefs = new Dictionary<string, ItemDefinition>();

        private Timer broadcastTimer;
        private TrackerType trackerType;

        const string rsTracker = "http://rust-servers.net/api/?action=custom&object=plugin&element=reward&key={KEY}&steamid=";
        const string trsTracker = "http://api.toprustservers.com/api/get?plugin=voter&key={KEY}&uid=";
        const string bcTracker = "http://beancan.io/vote/get/{KEY}/";

        string trackerName;

        private string col1;
        private string col2;
        #endregion

        #region Oxide Hooks
        void Loaded()
        {
            lang.RegisterMessages(Messages, this);
            data = Interface.Oxide.DataFileSystem.GetFile("voter_data");
        }
        void OnServerInitialized()
        {
            itemDefs = ItemManager.itemList.ToDictionary(i => i.shortname);
            LoadVariables();
            LoadData();
            col1 = $"<color={configData.Messaging.MainColor}>";
            col2 = $"<color={configData.Messaging.MSGColor}>";

            if (string.IsNullOrEmpty(configData.Tracker.APIKey))
            {
                PrintError("Please enter your API key in the config!");
                Interface.Oxide.UnloadPlugin("Voter");
                return;
            }

            if (ParseTrackerType())
            {
                cmd.AddChatCommand(configData.Commands.VoteCommand, this, cmdVote);
                cmd.AddChatCommand(configData.Commands.RewardCommand, this, cmdRewards);

                if (configData.Messaging.Enabled)
                    BroadcastLoop();
            }
        }
        void Unload()
        {
            if (broadcastTimer != null)
                broadcastTimer.Destroy();
        }
        #endregion

        #region Functions
        private bool ParseTrackerType()
        {
            try
            {
                trackerType = (TrackerType)Enum.Parse(typeof(TrackerType), configData.Tracker.TrackerType, true);
                trackerName = trackerType == TrackerType.RustServers ? "Rust-Servers" : trackerType == TrackerType.Beancan ? "BeancanIO" : "TopRustServers";
                return true;
            }
            catch
            {
                PrintError("Invalid tracker type set in the config. Either use \"TopRustServers\", \"RustServers\" or \"Beancan\"");
                Interface.Oxide.UnloadPlugin(Title);
                return false;
            }

        }
        private void BroadcastLoop()
        {
            PrintToChat(string.Format(msg("broadcastMessage1").Replace("{TYPE}", trackerName), configData.Commands.VoteCommand));
            broadcastTimer = timer.Once(configData.Messaging.Timer * 60, () => BroadcastLoop());
        }
        private void CheckForVotes(BasePlayer player) => GetWebRequest(player);
        

        private void GetWebRequest(BasePlayer player)
        {
            string tracker = trackerType == TrackerType.RustServers ? rsTracker : trackerType == TrackerType.Beancan ? bcTracker : trsTracker;
            Puts(tracker.Replace("{KEY}", configData.Tracker.APIKey) + player.UserIDString);
            webrequest.EnqueueGet(tracker.Replace("{KEY}", configData.Tracker.APIKey) + player.UserIDString, (code, response) =>
            {
                if (response == null || code != 200)
                {
                    PrintWarning($"Error: {code} - Couldn't get an answer from {trackerName} for {player.displayName}");
                    SendReply(player, $"{col1}{msg("contactError1", player.UserIDString).Replace("{TYPE}", trackerName)}</color>");
                }
                else
                {
                    Puts(response);
                    int responeNum;
                    if (!int.TryParse(response, out responeNum))
                    {
                        PrintError($"There was a error processing what was returned from {trackerName}");
                        SendReply(player, $"{col1}{msg("voteError", player.UserIDString)}</color>");
                    }
                    else if (responeNum == 0 || responeNum == 2)
                    {
                        SendReply(player, $"{col1}{msg("noVotes", player.UserIDString)}</color>");
                    }
                    else if (responeNum == 1)
                    {
                        if (configData.Rewards.Count == 1)                        
                            GiveReward(player, configData.Rewards.First().Value);                        
                        else
                        {
                            storedData.userData[player.userID] += configData.Tracker.PointsPerVote;
                            SaveData();
                            SendReply(player, $"{col2}{string.Format(msg("voteSuccess", player.UserIDString), $"</color>{col1}{storedData.userData[player.userID]}</color>{col2}", $"</color>{col1}/{configData.Commands.RewardCommand}</color>{col2}")}</color>");
                        }
                    }                   
                }
            }, this);
        }
        #endregion
       
        #region Rewards
        private bool GiveReward(BasePlayer player, Rewards reward)
        {
            if (reward.RPAmount > 0)
            {
                if (!ServerRewards)
                {
                    SendReply(player, $"{col1}{msg("noSR", player.UserIDString)}</color>");
                    return false;
                }
                GiveRP(player, reward.RPAmount);
            }
            if (reward.EcoAmount > 0)
            {
                if (!Economics)
                {
                    SendReply(player, $"{col1}{msg("noEco", player.UserIDString)}</color>");
                    return false;
                }
                GiveCoins(player, reward.EcoAmount);
            }            
            foreach(var rewardItem in reward.RewardItems)
            {                
                if (itemDefs.ContainsKey(rewardItem.Shortname))
                {
                    player.GiveItem(ItemManager.CreateByItemID(itemDefs[rewardItem.Shortname].itemid, rewardItem.Amount, rewardItem.SkinID), BaseEntity.GiveItemReason.PickedUp);
                }
                else
                {
                    SendReply(player, $"{col1}{msg("noItem", player.UserIDString)}</color>");
                    PrintError($"The reward {rewardItem.Shortname} does not exist. Check for the correct item shortname");
                    return false;
                }
            }
            SendReply(player, $"{col1}{msg("rewardSuccess", player.UserIDString)}</color>");
            return true;
        }
        private void GiveRP(BasePlayer player, int amount) => ServerRewards?.Call("AddPoints", player.UserIDString, amount);
        private void GiveCoins(BasePlayer player, int amount) => Economics?.Call("Deposit", player.UserIDString, amount);
        #endregion

        #region Chat Commands
        
        private void cmdVote(BasePlayer player, string command, string[] args)
        {
            if (!storedData.userData.ContainsKey(player.userID))
                storedData.userData.Add(player.userID, 0);            

            CheckForVotes(player);
        }
        
        private void cmdRewards(BasePlayer player, string command, string[] args)
        {
            if (!storedData.userData.ContainsKey(player.userID))
                storedData.userData.Add(player.userID, 0);
            if (args == null || args.Length == 0)
            {
                SendReply(player, $"{col1}Voter</color>  {col2}v </color>{col1}{Version}</color>");
                SendReply(player, $"{col2}{string.Format(msg("hasPoints", player.UserIDString), $"</color>{col1}{storedData.userData[player.userID]}</color>{col2}")}</color>");
                SendReply(player, $"{col2}{string.Format(msg("rewardHelp", player.UserIDString), $"</color>{col1}/{configData.Commands.RewardCommand}")}</color>");
                SendReply(player, $"{col2}{msg("available", player.UserIDString)}</color>");
                foreach (var reward in configData.Rewards)
                {
                    string rewardString = $"{col1}{msg("id", player.UserIDString)}</color> {col2}{reward.Key}</color>\n{col1}{msg("cost", player.UserIDString)}</color> {col2}{reward.Value.CostToBuy}</color>";
                    if (Economics && reward.Value.EcoAmount > 0)
                        rewardString += $"\n{col1}{msg("economics", player.UserIDString)}</color> {col2}{reward.Value.EcoAmount}</color>";
                    if (ServerRewards && reward.Value.RPAmount > 0)
                        rewardString += $"\n{col1}{msg("serverrewards", player.UserIDString)}</color> {col2}{reward.Value.RPAmount}</color>";

                    string rewardItems = string.Empty;
                    if (reward.Value.RewardItems.Count > 0)
                    {
                        rewardItems += $"\n{col1}{msg("rewardItems", player.UserIDString)}</color> {col2}";
                        for (int i = 0; i < reward.Value.RewardItems.Count; i++)
                        {
                            var item = reward.Value.RewardItems[i];
                            rewardItems += $"{item.Amount}x {itemDefs[item.Shortname].displayName.english}";
                            if (i < reward.Value.RewardItems.Count - 1)
                                rewardItems += ", ";
                            else rewardItems += "</color>";
                        } 
                    }
                    rewardString += rewardItems;
                    SendReply(player, rewardString);
                }
            }
            if (args.Length == 1)
            {
                int key;
                if (!int.TryParse(args[0], out key))
                {
                    SendReply(player, $"{col2}{msg("noId", player.UserIDString)}</color>");
                    return;
                }
                if (!configData.Rewards.ContainsKey(key))
                {
                    SendReply(player, $"{col2}{msg("notExist", player.UserIDString)} {key}</color>");
                    return;
                }
                var reward = configData.Rewards[key];
                if (storedData.userData[player.userID] < reward.CostToBuy)
                {
                    SendReply(player, $"{col2}{msg("noPoints", player.UserIDString)}</color>");
                    return;
                }
                else
                {
                    if (GiveReward(player, reward))
                    {
                        storedData.userData[player.userID] -= reward.CostToBuy;
                        SaveData();
                    }
                }
            }

        }
        #endregion

        #region HelpText
        private void SendHelpText(BasePlayer player)
        {
            SendReply(player, string.Format(msg("helptext1", player.UserIDString), configData.Commands.VoteCommand).Replace("{TYPE}", trackerName));
            SendReply(player, string.Format(msg("helptext2", player.UserIDString), configData.Commands.RewardCommand));
        }
        #endregion

        #region Config  
        enum TrackerType { TopRustServers, RustServers, Beancan } 
        class Tracker
        {
            [JsonProperty(PropertyName = "Tracker type (TopRustServers, RustServers, Beancan)")]
            public string TrackerType { get; set; }
            [JsonProperty(PropertyName = "API Key")]
            public string APIKey { get; set; }
            [JsonProperty(PropertyName = "Points received per vote")]
            public int PointsPerVote { get; set; }
        }  
        class Commands
        {
            [JsonProperty(PropertyName = "Chat Command - Reward Menu")]
            public string RewardCommand { get; set; }
            [JsonProperty(PropertyName = "Chat Command - Vote Checking")]
            public string VoteCommand { get; set; }
        } 
        class Rewards
        {
            [JsonProperty(PropertyName = "Reward Items")]
            public List<RewardItem> RewardItems { get; set; }
            [JsonProperty(PropertyName = "Reward RP (Server Rewards)")]
            public int RPAmount { get; set; }
            [JsonProperty(PropertyName = "Reward Money (Economics)")]
            public int EcoAmount { get; set; }
            [JsonProperty(PropertyName = "Reward Cost")]
            public int CostToBuy { get; set; }

            public class RewardItem
            {
                [JsonProperty(PropertyName = "Item Shortname")]
                public string Shortname { get; set; }
                [JsonProperty(PropertyName = "Item Skin ID")]
                public ulong SkinID { get; set; }
                [JsonProperty(PropertyName = "Item Amount")]
                public int Amount { get; set; }
            }
        }
        
        class Messaging
        {
            [JsonProperty(PropertyName = "Message color (Primary)")]
            public string MainColor { get; set; }
            [JsonProperty(PropertyName = "Message color (Secondary)")]
            public string MSGColor { get; set; }
            [JsonProperty(PropertyName = "Activate automated broadcasting")]
            public bool Enabled { get; set; }
            [JsonProperty(PropertyName = "Automated broadcast timer (minutes)")]
            public int Timer { get; set; }
        }
        
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Chat Commands")]
            public Commands Commands { get; set; } 
            [JsonProperty(PropertyName = "Messaging Options")]
            public Messaging Messaging { get; set; }
            [JsonProperty(PropertyName = "Reward List")]
            public Dictionary<int, Rewards> Rewards { get; set; }            
            [JsonProperty(PropertyName = "Tracker Information")]
            public Tracker Tracker { get; set; }
            
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {                
                Rewards = new Dictionary<int, Rewards>
                {
                    {0, new Rewards
                    {
                        CostToBuy = 1,
                        RPAmount = 0,
                        EcoAmount = 0,                      
                        RewardItems = new List<Rewards.RewardItem>
                        {
                            new Rewards.RewardItem
                            {
                                Amount = 1,
                                Shortname = "supply.signal",
                                SkinID = 0
                            }
                        }                       
                    } },
                    {1, new Rewards
                    {
                        CostToBuy = 2,
                        RPAmount = 0,
                        EcoAmount = 0,                       
                        RewardItems = new List<Rewards.RewardItem>
                        {
                            new Rewards.RewardItem
                            {
                                Amount = 100,
                                Shortname = "hq.metal.ore",
                                SkinID = 0
                            },
                            new Rewards.RewardItem
                            {
                                Amount = 150,
                                Shortname = "sulfur.ore",
                                SkinID = 0
                            }
                        }
                    } },
                    {2, new Rewards
                    {
                        CostToBuy = 3,
                        RPAmount = 200,
                        EcoAmount = 0,                      
                        RewardItems = new List<Rewards.RewardItem>()
                    } }
                },
                Commands = new Commands
                {
                    RewardCommand = "reward",
                    VoteCommand = "vote"
                },                
                Messaging = new Messaging
                {
                    MainColor = "#ce422b",
                    MSGColor = "#939393",
                    Enabled = true,
                    Timer = 30
                },
                Tracker = new Tracker
                {
                    APIKey = "",
                    PointsPerVote = 1,
                    TrackerType = "RustServers"
                }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Data Management
        void SaveData() => data.WriteObject(storedData);
        void LoadData()
        {
            try
            {
                storedData = data.ReadObject<StoredData>();
            }
            catch
            {
                storedData = new StoredData();
            }
        }
        class StoredData
        {
            public Dictionary<ulong, int> userData = new Dictionary<ulong, int>();
        }       
        #endregion

        #region Localization
        string msg(string key, string playerId = null) => lang.GetMessage(key, this, playerId);

        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            {"contactError1", "There was a error contacting {TYPE}. Please try again later"},
            {"voteError", "There was a error collecting your votes. Please try again later" },
            {"noVotes", "Thank you for voting for us but you have not cast anymore votes since the last time you checked"},
            {"voteSuccess", "Thank you for voting for us! You now have {0} vote points available. You can spend these points by typing {1}"},
            {"noSR", "ServerRewards is not installed. Unable to issue points"},
            {"noEco", "Economics is not installed. Unable to issue coins"},
            {"noItem", "Unable to find the requested reward" },
            {"rewardSuccess", "Thank you for voting for us! Enjoy your reward." },
            {"hasPoints", "You currently have {0} vote points to spend"},
            {"rewardHelp", "You can claim any reward package you have enough vote points to buy by typing {0} <ID>" },
            {"available", "Available Rewards:" },
            {"economics", "Coins (Economics):"},
            {"serverrewards", "RP (ServerRewards):"},
            {"rewardItems", "Items:" },
            {"id", "ID:" },
            {"cost", "Cost:" },
            {"noId", "You need to enter a reward ID"},
            {"notExist", "There is no reward with the ID:" },
            {"noPoints", "You do not have enough vote points to purchase that reward"},
            {"broadcastMessage1", "<color=#939393>Vote for us on </color><color=#ce422b>{TYPE}</color><color=#939393> and receive rewards! Type </color><color=#ce422b>/{0}</color><color=#939393> after voting</color>"},
            {"helptext1", "<color=#ce422b>/{0}</color><color=#939393> - Checks {TYPE} to see if you have voted for this server</color>" },
            {"helptext2", "<color=#ce422b>/{0}</color><color=#939393> - Display's available rewards and how many votepoints you have</color>" }
        };
        #endregion
    }
}