using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Offline Mail", "redBDGR", "1.0.2")]
    [Description("Send messages to a player so they receive them on their next login")]
    public class OfflineMail : CovalencePlugin
    {
        private DynamicConfigFile offlineMessageData;
        StoredData storedData;

        class StoredData
        {
            public Dictionary<string, MessageInfo> offlineMessageInformation = new Dictionary<string, MessageInfo>();
        }

        // For Config (do not touch)
        bool Changed = false;

        // Config variables
        public const string permissionName = "offlinemail.use";
        public const string permissionNameADMIN = "offlinemail.admin";
        public string consoleName = "Admin";
        public float waitTime = 3.0f;
        Dictionary<string, MessageInfo> playerMessages = new Dictionary<string, MessageInfo>();

        class MessageInfo
        {
            public string senderName;
            public List<string> messages;
        }

        // Method of saving data
        void SaveData()
        {
            storedData.offlineMessageInformation = playerMessages;
            offlineMessageData.WriteObject(storedData);
        }

        // Method of loading data
        void LoadData()
        {
            try
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
                playerMessages = storedData.offlineMessageInformation;
            }
            catch
            {
                Puts("Failed to load data, creating new file");
                storedData = new StoredData();
            }
        }

        // Save data on unload
        void Unload()
        {
            SaveData();
        }

        // Save data on server save
        void OnServerSave()
        {
            SaveData();
        }

        // Register permissions, load data / config variables
        void Init()
        {
            permission.RegisterPermission(permissionName, this);
            permission.RegisterPermission(permissionNameADMIN, this);
            LoadData();
            LoadVariables();
            offlineMessageData = Interface.Oxide.DataFileSystem.GetFile(Name);
        }

        // Config
        void LoadVariables()
        {
            consoleName = Convert.ToString(GetConfig("Settings", "Console Name", "Admin"));
            waitTime = Convert.ToSingle(GetConfig("Settings", "Message Delay Time", 3.0f));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["Login Message"] = "[{0}] {1}: <color=#00FFFF>{2}</color>",
                ["Message Stored"] = "Your message to {0} has been sent. They will receive it next time they log in!",
                ["Player Online"] = "This player is currently online! your message has not been saved",
                ["Player not Found"] = "Player not found!",
                ["No Permissions"] = "You do not have the correct permissions to use this command",
                ["You received mail!"] = "<color=#00FFFF>You have received new mail since you were last online!</color>",
                ["Chat Invalid Syntax /om"] = "Invalid syntax! /om <steamID/playername> <message>",
                ["Chat Invalid Syntax /omclear"] = "Invalid syntax! /omclear steamID/playername>",
                ["Console Invalid Syntax omclear"] = "Invalid syntax! /omclear steamID/playername>",
                ["Console Invalid Syntax om"] = "Invalid syntax! om <steamID/playername> <message>",
                ["Empty Inbox"] = "This user currently has no messages in their inbox!",
                ["Clear Inbox"] = "Wiping {0}'s inbox!",
            }, this);
        }

        // Checks on player Init if the player has any outstanding mail
        void OnUserConnected(IPlayer player)
        {
            if (playerMessages.ContainsKey(player.Id))
            {
                timer.Once(waitTime, () =>
                {
                    player.Message(msg("You received mail!", player.Id));
                    int y = 1;
                    foreach (string x in playerMessages[player.Id].messages)
                    {
                        player.Message(string.Format(msg("Login Message", player.Id), y, playerMessages[player.Id].senderName, x));
                        y += 1;
                    }
                    playerMessages.Remove(player.Id);
                });
            }
        }

        // Chat command for sending offline mail to players
        [Command("mail")]
        void sendomCMD(IPlayer player, string command, string[] args)
        {
            if (player.HasPermission(permissionName))
            {
                if (args == null)
                {
                    player.Message(msg("Chat Invalid Syntax", player.Id));
                    return;
                }
                if (args.Length != 2)
                {
                    player.Message(msg("Chat Invalid Syntax", player.Id));
                    return;
                }
                var targetUser = players.FindPlayer(args[0]);
                if (targetUser == null)
                {
                    player.Message(msg("Player not Found", player.Id));
                    return;
                }
                if (!targetUser.IsConnected)
                {
                    if (!playerMessages.ContainsKey(targetUser.Id))
                    {
                        playerMessages.Add(targetUser.Id, new MessageInfo { messages = new List<string>(), senderName = player.Name.ToString() });
                        playerMessages[targetUser.Id].messages.Add(args[1]);
                        player.Message(string.Format(msg("Message Stored", player.Id), targetUser.Name));
                        return;
                    }
                    else if (playerMessages[targetUser.Id].messages.Count == 0)
                    {
                        playerMessages[targetUser.Id].messages.Add(args[1]);
                        player.Message(string.Format(msg("Message Stored", player.Id), targetUser.Name));
                        return;
                    }
                    else
                    {
                        playerMessages[targetUser.Id].messages.Add(args[1]);
                        player.Message(string.Format(msg("Message Stored", player.Id), targetUser.Name));
                        return;
                    }
                }
                else
                {
                    player.Message(msg("Player Online", player.Id));
                    return;
                }
            }
            else
            {
                player.Message(msg("No Permissions", player.Id));
                return;
            }
        }

        [Command("clearmail")]
        void omclearCMD(IPlayer player, string command, string[] args)
        {
            if (player.HasPermission(permissionNameADMIN))
            {
                if (args.Length != 1 || args == null)
                {
                    player.Message(msg("Chat Invalid Syntax /omclear", player.Id));
                    return;
                }
                var targetUser = players.FindPlayer(args[0]);
                if (targetUser == null)
                {
                    player.Message(msg("Player not Found", player.Id));
                    return;
                }
                if (playerMessages.ContainsKey(targetUser.Id))
                {
                   player.Message(string.Format(msg("Clear Inbox", player.Id), targetUser.Name));
                   playerMessages.Remove(targetUser.Id);
                }
                else
                {
                    player.Message(msg("Empty Inbox", player.Id));
                    return;
                }
            }
            else
            {
                player.Message(msg("No Permissions", player.Id));
                return;
            }
        }

        object GetConfig(string menu, string datavalue, object defaultValue)
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
        string msg(string key, string id = null) => lang.GetMessage(key, this, id);
    }
}
