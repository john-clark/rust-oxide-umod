using Oxide.Core.Libraries;
using System;
using System.Collections.Generic;
using UnityEngine;


namespace Oxide.Plugins
{
    [Info("Steam ID", "Gimax", "1.2.3")]
    [Description("Allows you to grab steamID's of players & connection status with permission")]
    class SteamID : RustPlugin
    {
        #region Variables
        private string perm = "steamid.use";

        private bool Changed;
        private bool ResultChat;
        private bool ResultConsole;


        #endregion

        #region GetConfig
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
        #endregion

        #region Languages
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = " You have no permissions to use that!",
                ["SyntaxHelp"] = "<color=#f7db3f>[Steam ID Helper]</color>\n<color=grey>/steamid <playername or steamid></color> - Grabs information from a single player.\n<color=grey>/steamidall</color> - Gets information from every single player in the console.",
                ["NoPlayerFound"] = " No player found with the name '<color=#f7db3f>{0}</color>'. (Does not exist or is dead and disconnected)",
                ["GrabAllMsg"] = " Collecting Data . . .\nDone!\nResult is in the Console!\n(Press F1)",
                ["GrabChatMsg"] = " Name: <color=#f7db3f>{0}</color> - {1}\nSteamID: <color=#f7db3f>{2}</color> ",
                ["GrabConsoleMsg"] = " Name: {0} - {1}   |   SteamID: {2}",
                ["ActivePlayersHeader"] = "################# Active Players: {0} #################",
                ["GrabAllConsoleMsg"] = "Player: {0}             SteamID: {1}",
                ["PrefixTag"] = "<color=#f7db3f>[Steam ID] </color>",
                ["Online"] = "<color=green>(Online)</color>",
                ["Sleeping"] = "<color=grey>(Sleeping)</color>"
            }, this, "en");
        }

        #endregion

        #region LoadConfig
        void LoadVariables()
        {
            ResultChat = Convert.ToBoolean(GetConfig("Plugin Settings", "Print Result to Chat", true));
            ResultConsole = Convert.ToBoolean(GetConfig("Plugin Settings", "Print Result to Console", true));

            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }
        #endregion

        #region Hooks

        private new void LoadDefaultConfig()

        {
            PrintWarning("Can't find a valid config file - Creating a new one!");
            Config.Clear();
            LoadVariables();
        }


        private void Init()
        {
            permission.RegisterPermission(perm, this);
            LoadVariables();
        }
        #endregion

        #region Command(s)

        [ChatCommand("steamidall")]
        private void GraballCMD(BasePlayer player, string command, string[] args)
        {
                var playerCount = BasePlayer.activePlayerList.Count;
                PrintToConsole(player, Lang("ActivePlayersHeader", player.UserIDString, playerCount));
                foreach (var t in BasePlayer.activePlayerList)
                {
                    PrintToConsole(player, Lang("GrabAllConsoleMsg", player.UserIDString, t.displayName, t.UserIDString));
                }
                SendReply(player, Lang("PrefixTag", player.UserIDString) + Lang("GrabAllMsg", player.UserIDString));
                return;           
        }


        [ChatCommand("steamid")]
        private void GrabCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, perm))
            {
                SendReply(player, Lang("PrefixTag", player.UserIDString) + Lang("NoPermission", player.UserIDString));
                return;
            }

            if (args == null || args.Length == 0)
            {
                SendReply(player, Lang("SyntaxHelp", player.UserIDString));
                return;
            }



            var input = args[0].ToLower();
            var foundPlayer = new List<BasePlayer>();
            var sleepers = BasePlayer.sleepingPlayerList;
            var active = BasePlayer.activePlayerList;
            string online = Lang("Online", player.UserIDString);
            string sleeping = Lang("Sleeping", player.UserIDString);
            
            
            SendReply(player, Lang("PrefixTag", player.UserIDString));
            foreach (var p in active)
            {
                if (p.UserIDString == input)
                {
                    foundPlayer.Add(p);
                    if (ResultChat == true)
                    {
                        SendReply(player, Lang("GrabChatMsg", player.UserIDString, p.displayName, online, p.UserIDString));
                    }
                    if (ResultConsole == true)
                    {
                        PrintToConsole(player, Lang("GrabConsoleMsg", player.UserIDString, p.displayName, online, p.UserIDString));
                    }
                }

                if (p.displayName.ToLower() == input)
                {
                    foundPlayer.Add(p);
                    if (ResultChat == true)
                    {
                        SendReply(player, Lang("GrabChatMsg", player.UserIDString, p.displayName, online, p.UserIDString));
                    }
                    if (ResultConsole == true)
                    {
                        PrintToConsole(player, Lang("GrabConsoleMsg", player.UserIDString, p.displayName, online, p.UserIDString));
                    }
                }

                else if (p.displayName.ToLower().Contains(input))
                {
                    foundPlayer.Add(p);
                    if (ResultChat == true)
                    {
                        SendReply(player, Lang("GrabChatMsg", player.UserIDString, p.displayName, online, p.UserIDString));
                    }
                    if (ResultConsole == true)
                    {
                        PrintToConsole(player, Lang("GrabConsoleMsg", player.UserIDString, p.displayName, online, p.UserIDString));
                    }
                }
            }

            foreach (var p in sleepers)
            {
                if (p.UserIDString == input)
                {
                    foundPlayer.Add(p);
                    if (ResultChat == true)
                    {
                        SendReply(player, Lang("GrabChatMsg", player.UserIDString, p.displayName, sleeping, p.UserIDString));
                    }
                    if (ResultConsole == true)
                    {
                        PrintToConsole(player, Lang("GrabConsoleMsg", player.UserIDString, p.displayName, sleeping, p.UserIDString));
                    }
                }

                if (p.displayName.ToLower() == input)
                {
                    foundPlayer.Add(p);
                    if (ResultChat == true)
                    {
                        SendReply(player, Lang("GrabChatMsg", player.UserIDString, p.displayName, sleeping, p.UserIDString));
                    }
                    if (ResultConsole == true)
                    {
                        PrintToConsole(player, Lang("GrabConsoleMsg", player.UserIDString, p.displayName, sleeping, p.UserIDString));
                    }
                }

                else if (p.displayName.ToLower().Contains(input))
                {
                    foundPlayer.Add(p);
                    if (ResultChat == true)
                    {
                        SendReply(player, Lang("GrabChatMsg", player.UserIDString, p.displayName, sleeping, p.UserIDString));
                    }
                    if (ResultConsole == true)
                    {
                        PrintToConsole(player, Lang("GrabConsoleMsg", player.UserIDString, p.displayName, sleeping, p.UserIDString));
                    }
                    return;
                }
            }

            if (foundPlayer.Count == 0)
            {
                SendReply(player, Lang("NoPlayerFound", player.UserIDString, input));
                return;
            }       
        }
        #endregion

        #region Helper
        private string Lang(string key, string id, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        #endregion

    }
}
