using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Skull Crusher", "redBDGR", "1.0.7")]
    [Description("Adds some extra features to the crushing of human skulls")]

    class SkullCrusher : RustPlugin
    {
        [PluginReference]
        private Plugin Clans, Economics, Friends, ServerRewards;

        private Dictionary<string, int> cacheDic = new Dictionary<string, int>();
        private bool Changed;

        private bool giveItemsOnCrush = true;
        private double moneyPerSkullCrush = 20.0;
        private bool normalCrusherMessage = true;
        private bool nullCrusherMessage = true;
        private bool ownCrusherMessage = true;
        private int RPPerSkullCrush = 20;
        private bool sendNotificaitionMessage = true;
        private bool friendsSupport, clansSupport, teamsSupport;
        private bool useEconomy, useServerRewards;

        #region Data

        private DynamicConfigFile SkullCrusherData;
        private StoredData storedData;

        private class StoredData
        {
            public Dictionary<string, int> PlayerInformation = new Dictionary<string, int>();
        }

        private void SaveData()
        {
            storedData.PlayerInformation = cacheDic;
            SkullCrusherData.WriteObject(storedData);
        }

        private void LoadData()
        {
            try
            {
                storedData = SkullCrusherData.ReadObject<StoredData>();
                cacheDic = storedData.PlayerInformation;
            }
            catch
            {
                Puts("Failed to load data, creating new file");
                storedData = new StoredData();
            }
        }

        #endregion

        #region Hooks

        private void Init()
        {
            LoadVariables();

            SkullCrusherData = Interface.Oxide.DataFileSystem.GetFile(Name);
        }

        private void Loaded()
        {
            if (clansSupport)
                if (Clans == null)
                {
                    clansSupport = false;
                    PrintWarning("Clans.cs was not found... disabling features");
                }

            if (friendsSupport)
                if (Friends == null)
                {
                    friendsSupport = false;
                    PrintWarning("Friends.cs was not found... disabling features");
                }

            if (useEconomy)
                if (!Economics)
                {
                    PrintError("Economics.cs was not found, auto-disabling economic features");
                    useEconomy = false;
                }

            if (useServerRewards)
                if (!ServerRewards)
                {
                    PrintError("ServerRewards.cs was not found, auto-disabling serverrewards features");
                    useServerRewards = false;
                }
        }

        private void OnServerInitialized()
        {
            LoadData();
        }

        private void Unload()
        {
            SaveData();
        }

        private void OnServerSave()
        {
            SaveData();
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {
            giveItemsOnCrush = Convert.ToBoolean(GetConfig("Settings", "Give items on crush", true));

            useEconomy = Convert.ToBoolean(GetConfig("Economy", "Use Economy", false));
            useServerRewards = Convert.ToBoolean(GetConfig("Economy", "Use ServerRewards", false));
            RPPerSkullCrush = Convert.ToInt32(GetConfig("Economy", "RP Per Skull Crush", 20));
            moneyPerSkullCrush = Convert.ToDouble(GetConfig("Economy", "Money Per Skull Crush", 20.0));
            sendNotificaitionMessage = Convert.ToBoolean(GetConfig("Economy", "Send Notification Message", true));

            nullCrusherMessage = Convert.ToBoolean(GetConfig("Settings", "Null Owner Crush Message", true));
            ownCrusherMessage = Convert.ToBoolean(GetConfig("Settings", "Own Skull Crush Message", true));
            normalCrusherMessage = Convert.ToBoolean(GetConfig("Settings", "Normal Crush Message", true));

            friendsSupport = Convert.ToBoolean(GetConfig("Team Settings", "Use Friends", false));
            clansSupport = Convert.ToBoolean(GetConfig("Team Settings", "Use Clans", false));
            teamsSupport = Convert.ToBoolean(GetConfig("Team Settings", "Use Teams", false));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Null Crusher"] = "{0}'s skull was crushed",
                ["Crushed own skull"] = "{0} crushed their own skull!",
                ["Default Crush Message"] = "{0}'s skull was crushed by {1}",
                ["Skulls chat command reply"] = "You have crushed a total of {0} skulls",
                ["Economy Notice"] = "You received ${0} for crushing an enemies skull!",
                ["ServerRewards Notice"] = "You received {0} RP for crushing an enemies skull!"
            }, this);
        }

        private object OnItemAction(Item item, string action)
        {
            if (action != "crush")
                return null;
            if (item.info.shortname != "skull.human")
                return null;
            string skullName = null;
            if (item.name != null)
                skullName = item.name.Substring(10, item.name.Length - 11);
            if (string.IsNullOrEmpty(skullName))
                return DecideReturn(item);

            BasePlayer ownerPlayer = item.GetOwnerPlayer();
            if (ownerPlayer == null)
            {
                if (nullCrusherMessage)
                    rust.BroadcastChat(null, string.Format(msg("Null Crusher"), skullName));
                return DecideReturn(item);
            }
            if (ownerPlayer.displayName == skullName)
            {
                if (ownCrusherMessage)
                    rust.BroadcastChat(null, string.Format(msg("Crushed own skull"), ownerPlayer.displayName));
                return DecideReturn(item);
            }

            BasePlayer skullOwner = BasePlayer.Find(skullName);
            if (skullOwner)
            {
                if (friendsSupport || clansSupport || teamsSupport)
                    if (IsTeamed(ownerPlayer, skullOwner))
                        return DecideReturn(item);
            }

            if (!cacheDic.ContainsKey(ownerPlayer.UserIDString))
                cacheDic.Add(ownerPlayer.UserIDString, 0);
            cacheDic[ownerPlayer.UserIDString]++;
            if (useEconomy)
                if (Economics)
                {
                    if (sendNotificaitionMessage)
                        ownerPlayer.ChatMessage(string.Format(msg("Economy Notice", ownerPlayer.UserIDString), moneyPerSkullCrush));
                    Economics.CallHook("Deposit", ownerPlayer.userID, moneyPerSkullCrush);
                }
            if (useServerRewards)
                if (ServerRewards)
                {
                    if (sendNotificaitionMessage)
                        ownerPlayer.ChatMessage(string.Format(msg("ServerRewards Notice", ownerPlayer.UserIDString), RPPerSkullCrush));
                    ServerRewards.Call("AddPoints", ownerPlayer.userID, RPPerSkullCrush);
                }
            if (normalCrusherMessage)
                rust.BroadcastChat(null, string.Format(msg("Default Crush Message"), skullName, ownerPlayer.displayName));
            return DecideReturn(item);
        }

        #endregion

        #region Commands

        [ChatCommand("skulls")]
        private void skullsCMD(BasePlayer player, string command, string[] args)
        {
            if (!cacheDic.ContainsKey(player.UserIDString))
                cacheDic.Add(player.UserIDString, 0);
            player.ChatMessage(string.Format(msg("Skulls chat command reply", player.UserIDString), cacheDic[player.UserIDString]));
        }

        #endregion

        #region Methods

        private bool IsTeamed(BasePlayer ownerPlayer, BasePlayer skullOwner)
        {
            if (ownerPlayer == null || skullOwner == null)
                return false;
            if (teamsSupport)
            {
                RelationshipManager.PlayerTeam team = RelationshipManager.Instance.FindTeam(ownerPlayer.currentTeam);
                if (team == null)
                    return false;
                foreach (ulong entry in team.members)
                {
                    if (entry == skullOwner.userID)
                        return true;
                }
            }

            if (friendsSupport)
                if ((bool)Friends?.Call("AreFriends", ownerPlayer.userID, skullOwner.userID) == true)
                    return true;

            if (clansSupport)
                if (Clans != null)
                    if ((string)Clans.Call("GetClanOf", ownerPlayer.userID) == (string)Clans.Call("GetClanOf", skullOwner.userID))
                        return true;

            return false;
        }

        private object DecideReturn(Item item)
        {
            if (giveItemsOnCrush)
                return null;
            item.UseItem(); // Remove one of the item from the players inventory
            return true;
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        private string msg(string key, string id = null)
        {
            return lang.GetMessage(key, this, id);
        }

        #endregion
    }
}
