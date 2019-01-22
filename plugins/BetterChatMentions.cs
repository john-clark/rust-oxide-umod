using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using UnityEngine;

// Requires: BetterChat

namespace Oxide.Plugins
{
    [Info("Better Chat Mentions", "Death and PsychoTea", "1.2.1", ResourceId = 2830)]
    [Description("Format and send alerts to mentioned players")]

    class BetterChatMentions : RustPlugin
    {
        #region Fields

        private const string PermEveryone = "betterchatmentions.everyone";
        private const string PermDisallow = "betterchatmentions.disallow";
        private const string PermExclude  = "betterchatmentions.exclude";

        [PluginReference] Plugin BetterChat;

        Dictionary<ulong, double> LastAlert = new Dictionary<ulong, double>();

        #endregion

        #region Config

        ConfigFile config;

        class ConfigFile
        {
            [JsonProperty("Group Color To Use (Title/Username/Message)")]
            public string GroupColor = "Username";

            [JsonProperty("Alert to play")]
            public string Alert = "assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab";

            [JsonProperty("Delay Between Alert Sounds (Seconds)")]
            public double AlertSoundDelay = 0;

            [JsonProperty("Everyone Ping Color")]
            public string EveryonePingColor = "#55aaff";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<ConfigFile>();
            Config.WriteObject(config, true);
        }

        protected override void LoadDefaultConfig() => config = new ConfigFile();

        protected override void SaveConfig() => Config.WriteObject(config, true);

        #endregion
        
        #region Hooks
             
        void Init()
        {
            permission.RegisterPermission(PermEveryone, this);
            permission.RegisterPermission(PermDisallow, this);
            permission.RegisterPermission(PermExclude, this);
        }

        Dictionary<string, object> OnBetterChat(Dictionary<string, object> data)
        {
            IPlayer player = data["Player"] as IPlayer;

            if (player.HasPermission(PermDisallow))
            {
                return null;
            }

            string text = data["Text"].ToString();

            List<BasePlayer> soundPlayers = new List<BasePlayer>();

            // Get all indexes of '@' within the message
            List<int> indexes = AllIndexesOf('@', text);

            // When we insert text, the index of a particular '@' character will increase
            // We track this increase in the `substringIncrease`
            int substringIncrease = 0;
            foreach (int listIndex in indexes)
            {
                int index = listIndex + substringIncrease;

                // Trim the start of the text down to the index
                string playerName = text.Substring(index);

                // Grab the next space and trim the string down to it (if applicable)
                int spaceIndex = playerName.IndexOf(' ');
                if (spaceIndex != -1)
                {
                    playerName = playerName.Substring(0, spaceIndex);
                }
                
                string nameOnly = playerName.TrimStart('@');

                bool pingEveryone = nameOnly.ToLower() == "everyone" && 
                                    player.HasPermission(PermEveryone);

                string highlightColour = (pingEveryone) ? config.EveryonePingColor : string.Empty;
                string highlightText = nameOnly.ToLower();

                // Find the player (removing the '@' so it leaves just their name)
                if (!pingEveryone)
                {
                    BasePlayer target = FindPlayer(nameOnly);
                    if (target == null) continue;

                    if (ShouldExclude(target)) continue;

                    highlightText = target.displayName;

                    if (!soundPlayers.Contains(target))
                    {
                        soundPlayers.Add(target);
                    }

                    // Get their group colour using the BetterChat API 
                    highlightColour = GetGroupColour(target);
                    if (string.IsNullOrEmpty(highlightColour)) continue;
                }

                // The text to be inserted
                string newText = $"@[{highlightColour}]{highlightText}[/#]";
                
                StringBuilder sb = new StringBuilder(text);
                // Remove the original name at the given index
                sb.Remove(index, playerName.Length);
                // Insert the new message in its place
                sb.Insert(index, newText);
                text = sb.ToString();

                // Increment the substringIncrease based on the number of characters we added
                // Calculated via length of the new string `newText` minus length of the old string `playerName`
                substringIncrease += newText.Length - playerName.Length;

                if (pingEveryone)
                {
                    soundPlayers = BasePlayer.activePlayerList;
                    break;
                }
            }

            foreach (var target in soundPlayers)
            {
                if (!ShouldPlaySound(target)) continue;

                PlaySound(target, config.Alert);

                RecordAlertSoundPlayed(target);
            }
            
            data["Text"] = text;

            return data;
        }

        #endregion
        
        #region Functions
        
        List<int> AllIndexesOf(char character, string input)
        {
            List<int> indexes = new List<int>();
        
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == character)
                {
                    indexes.Add(i);
                }
            }

            return indexes;
        }

        string GetGroupColour(BasePlayer player)
        {
            // Get all the groups the given player is in 
            List<JObject> groups = BetterChat.Call<List<JObject>>("API_GetUserGroups", player.IPlayer);

            // Order ascendingly by the `Priority` field
            groups = groups.OrderBy(g => (int)g["Priority"]).ToList();

            // Get the first item in the list (group w/ lowest priority)
            JObject highestGroup = groups.First();

            // Get the colour based on the config option
            string groupColour = (string)highestGroup[config.GroupColor]["Color"];
            if (string.IsNullOrEmpty(groupColour)) return string.Empty;
        
            // Add a # to the start of the colour so Covalence formatting is valid 
            if (!groupColour.StartsWith("#")) groupColour = "#" + groupColour;

            return groupColour;
        }

        void PlaySound(BasePlayer player, string path) => Effect.server.Run(path, player, 2, Vector3.zero, new Vector3(0f, 2f, 0f));

        bool ShouldPlaySound(BasePlayer player)
        {
            if (config.AlertSoundDelay <= 0)
            {
                return true;
            }

            if (!LastAlert.ContainsKey(player.userID))
            {
                return true;
            }

            double lastTime = LastAlert[player.userID];

            return ((TimeSinceEpoch() - lastTime) > config.AlertSoundDelay);
        }

        void RecordAlertSoundPlayed(BasePlayer player)
        {
            if (config.AlertSoundDelay <= 0) return;

            if (LastAlert.ContainsKey(player.userID))
            {
                LastAlert.Remove(player.userID);
            }

            LastAlert.Add(player.userID, TimeSinceEpoch());
        }

        #endregion

        #region Helpers

        BasePlayer FindPlayer(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            return BasePlayer.activePlayerList.FirstOrDefault(x => x.displayName.ToLower().Contains(name.ToLower()));
        }

        double TimeSinceEpoch() => (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

        bool ShouldExclude(BasePlayer player) => permission.UserHasPermission(player.UserIDString, PermExclude);

        #endregion
    }
}
