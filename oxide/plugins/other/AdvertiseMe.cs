using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("Advertise Me", "Psystec", "1.0.3", ResourceId = 2764)]
    [Description("Lets players place eye catching advertisement messages.")]
    class AdvertiseMe : RustPlugin
    {
        #region You Can Customize The Chat Commands

        private const string chatCommand = "advert";        //This is when players use the chat command in game ex. /advert Best shop now open.
        private const string consoleCommand = "advert";     //This is for admins to change settings in the console ex. advert allowadverttoall false

        #endregion

        #region Configuration

        private const string advertMeAllow = "advertiseMe.allow";
        private const string advertMeAdmin = "advertiseMe.admin";
        List<BasePlayer> AdvertedPlayers = new List<BasePlayer>();
        List<string> CommandsList = new List<string>();

        private bool allowAdvertToAll;
        private bool stripChatSize;
        private bool enableDelay;
        private bool adminNoDelay;
        private int delaySeconds;
        private bool enablePrefixLabel;
        private string prefixLabel;
        private string prefixLabelColor;
        private string advertMessageColor;
        private bool showLogs;

        protected override void LoadDefaultConfig()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //Commands and messages are localized only advert command has to be constant
                //h = Headers, c= commands, d = descriptions, m = messages
                ["hCOMMAND"] = "COMMAND",
                ["hDESCRIPTION"] = "DESCRIPTION",
                ["cAllowAdvertToAll"] = "AllowAdvertToAll",
                ["dAllowAdvertToAll"] = "Enable everyone to use the advert command.",
                ["cStripChatSize"] = "StripChatSize",
                ["dStripChatSize"] = "Removes the functionality for players to adjust the message size.",
                ["cEnableDelay"] = "EnableDelay",
                ["dEnableDelay"] = "Enable a delay so people can't spam advert.",
                ["cAdminNoDelay"] = "AdminNoDelay",
                ["dAdminNoDelay"] = "Lets admins skip the advert delay.",
                ["cDelaySeconds"] = "DelaySeconds",
                ["dDelaySeconds"] = "Delay between adverts per person.",
                ["cEnablePrefixLabel"] = "EnablePrefixLabel",
                ["dEnablePrefixLabel"] = "Prefix the message with a label when being advertised.",
                ["cPrefixLabel"] = "PrefixLabel",
                ["dPrefixLabel"] = "Label that the advert should be prefixed with.",
                ["cPrefixLabelColor"] = "PrefixLabelColor",
                ["dPrefixLabelColor"] = "Prefixed label color.",
                ["cAdvertMessageColor"] = "AdvertMessageColor",
                ["dAdvertMessageColor"] = "Advert Color.",
                ["cTest"] = "Test",
                ["dTest"] = "Sends a test message to you only, quality check.",
                ["cShowLogs"] = "ShowLogs",
                ["dShowLogs"] = "Enables messages to be seen in the RCON Console Log.",
                ["mInvalidCommand"] = "Invalid Command.",
                ["mInvalidTrueFalse"] = "Invalid Syntax: Use true/false or 0/1.",
                ["mInvalidNumbers"] = "Invalid Syntax: Use only numbers.",
                ["mInvalidLabelNone"] = "Please specify a prefix Label.",
                ["mInvalidColorCode"] = "Invalid Syntax: Incorrect Color code.",
                ["mPlayerDelayMessage"] = "Please wait before making another advertisement.",
                ["mPlayerNoMessage"] = "No message specified. Type /advert followed by a message to advertise.",
                ["mPlayerMessageIgnored"] = "(advert ignored)",
            }, this, "en");

            Config["allowAdvertToAll"] = allowAdvertToAll = GetConfig("allowAdvertToAll", true);
            Config["stripChatSize"] = stripChatSize = GetConfig("stripChatSize", true);
            Config["enableDelay"] = enableDelay = GetConfig("enableDelay", true);
            Config["adminNoDelay"] = adminNoDelay = GetConfig("adminNoDelay", true);
            Config["delaySeconds"] = delaySeconds = GetConfig("delaySeconds", 60);
            Config["enablePrefixLabel"] = enablePrefixLabel = GetConfig("enablePrefixLabel", true);
            Config["prefixLabel"] = prefixLabel = GetConfig("prefixLabel", "Advertisement: ");
            Config["prefixLabelColor"] = prefixLabelColor = GetConfig("prefixLabelColor", "#0EB741");
            Config["advertMessageColor"] = advertMessageColor = GetConfig("advertMessageColor", "#ddff00");
            Config["showLogs"] = showLogs = GetConfig("showLogs", true);

            SaveConfig();

            CommandsList.Add(Lang("cAllowAdvertToAll"));
            CommandsList.Add(Lang("cStripChatSize"));
            CommandsList.Add(Lang("cEnableDelay"));
            CommandsList.Add(Lang("cAdminNoDelay"));
            CommandsList.Add(Lang("cDelaySeconds"));
            CommandsList.Add(Lang("cEnablePrefixLabel"));
            CommandsList.Add(Lang("cPrefixLabel"));
            CommandsList.Add(Lang("cPrefixLabelColor"));
            CommandsList.Add(Lang("cAdvertMessageColor"));
            CommandsList.Add(Lang("cTest"));
            CommandsList.Add(Lang("cShowLogs"));
        }

        #endregion

        #region Hooks

        private void Init()
        {
            LoadDefaultConfig();
            permission.RegisterPermission(advertMeAllow, this);
            permission.RegisterPermission(advertMeAdmin, this);
        }

        #endregion

        #region Console Commands

        [ConsoleCommand(consoleCommand)]
        private void AdvertCommands(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            if (!HasPermission(player, advertMeAdmin)) return;

            var args = arg?.Args ?? null;

            if (args == null)
            {
                SendReply(arg, (Lang("hCOMMAND") + ":").PadRight(50) + Lang("hDESCRIPTION") + ":");
                SendReply(arg, (consoleCommand + " " + Lang("cAllowAdvertToAll") + " [true/false][1/0] ").PadRight(50) + Lang("dAllowAdvertToAll"));
                SendReply(arg, (consoleCommand + " " + Lang("cStripChatSize") + " [true/false][1/0]").PadRight(50) + Lang("dStripChatSize"));
                SendReply(arg, (consoleCommand + " " + Lang("cEnableDelay") + " [true/false][1/0] ").PadRight(50) + Lang("dEnableDelay"));
                SendReply(arg, (consoleCommand + " " + Lang("cAdminNoDelay") + " [true/false][1/0] ").PadRight(50) + Lang("dAdminNoDelay"));
                SendReply(arg, (consoleCommand + " " + Lang("cDelaySeconds") + " [0-9]").PadRight(50) + Lang("dDelaySeconds"));
                SendReply(arg, (consoleCommand + " " + Lang("cEnablePrefixLabel") + " [true/false][1/0]").PadRight(50) + Lang("dEnablePrefixLabel"));
                SendReply(arg, (consoleCommand + " " + Lang("cPrefixLabel") + " [A-9]").PadRight(50) + Lang("dPrefixLabel"));
                SendReply(arg, (consoleCommand + " " + Lang("cPrefixLabelColor") + " [#0EB741]").PadRight(50) + Lang("dPrefixLabelColor"));
                SendReply(arg, (consoleCommand + " " + Lang("cAdvertMessageColor") + " [#ddff00]").PadRight(50) + Lang("dAdvertMessageColor"));
                SendReply(arg, (consoleCommand + " " + Lang("cTest") + " [A-9 ]").PadRight(50) + Lang("dTest"));
                SendReply(arg, (consoleCommand + " " + Lang("cShowLogs") + " [true/false][1/0]").PadRight(50) + Lang("dShowLogs"));
                return;
            }

            int commcount = 0;
            List<string> coms = new List<string>();
            foreach (string command in CommandsList)
            {
                if (command.ToLower().Contains(args[0].ToLower()))
                {
                    commcount++;
                    coms.Add(command);
                }
            }
            if (commcount > 1)
            {
                foreach (string comm in coms)
                {
                    SendReply(arg, comm);
                }
                return;
            }

            if (Lang("cAllowAdvertToAll").ToLower().Contains(args[0].ToLower()))
            {
                if (args.Length == 1)
                {
                    SendReply(arg, Lang("cAllowAdvertToAll") + " = \"" + allowAdvertToAll.ToString() + "\"");
                    return;
                }
                if (args[1].ToLower() == "true" || args[1] == "1")
                {
                    Config["allowAdvertToAll"] = true;
                    allowAdvertToAll = true;
                    SaveConfig();
                    SendReply(arg, Lang("cAllowAdvertToAll") + " = \"true\"");
                    return;
                }
                if (args[1].ToLower() == "false" || args[1] == "0")
                {
                    Config["allowAdvertToAll"] = false;
                    allowAdvertToAll = false;
                    SaveConfig();
                    SendReply(arg, Lang("cAllowAdvertToAll") + " = \"false\"");
                    return;
                }
                SendReply(arg, Lang("mInvalidTrueFalse"));
                return;
            }
            if (Lang("cStripChatSize").ToLower().Contains(args[0].ToLower()))
            {
                if (args.Length == 1)
                {
                    SendReply(arg, Lang("cAllowAdvertToAll") + " = \"" + allowAdvertToAll.ToString() + "\"");
                    return;
                }
                if (args[1].ToLower() == "true" || args[1] == "1")
                {
                    Config["stripChatSize"] = true;
                    stripChatSize = true;
                    SaveConfig();
                    SendReply(arg, Lang("cStripChatSize") + " = \"true\"");
                    return;
                }
                if (args[1].ToLower() == "false" || args[1] == "0")
                {
                    Config["stripChatSize"] = false;
                    stripChatSize = false;
                    SaveConfig();
                    SendReply(arg, Lang("cStripChatSize") + " = \"false\"");
                    return;
                }
                SendReply(arg, Lang("mInvalidTrueFalse"));
                return;
            }
            if (Lang("cEnableDelay").ToLower().Contains(args[0].ToLower()))
            {
                if (args.Length == 1)
                {
                    SendReply(arg, Lang("cEnableDelay") + " = \"" + enableDelay.ToString() + "\"");
                    return;
                }
                if (args[1].ToLower() == "true" || args[1] == "1")
                {
                    Config["enableDelay"] = true;
                    enableDelay = true;
                    SaveConfig();
                    SendReply(arg, Lang("cEnableDelay") + " = \"true\"");
                    return;
                }
                if (args[1].ToLower() == "false" || args[1] == "0")
                {
                    Config["enableDelay"] = false;
                    enableDelay = false;
                    SaveConfig();
                    SendReply(arg, Lang("cEnableDelay") + " = \"false\"");
                    return;
                }
                SendReply(arg, Lang("mInvalidTrueFalse"));
                return;
            }
            if (Lang("cAdminNoDelay").ToLower().Contains(args[0].ToLower()))
            {
                if (args.Length == 1)
                {
                    SendReply(arg, Lang("cAdminNoDelay") + " = \"" + adminNoDelay.ToString() + "\"");
                    return;
                }
                if (args[1].ToLower() == "true" || args[1] == "1")
                {
                    Config["adminNoDelay"] = true;
                    adminNoDelay = true;
                    SaveConfig();
                    SendReply(arg, Lang("cAdminNoDelay") + " = \"true\"");
                    return;
                }
                if (args[1].ToLower() == "false" || args[1] == "0")
                {
                    Config["adminNoDelay"] = false;
                    adminNoDelay = false;
                    SaveConfig();
                    SendReply(arg, Lang("cAdminNoDelay") + " = \"false\"");
                    return;
                }
                SendReply(arg, Lang("mInvalidTrueFalse"));
                return;
            }
            if (Lang("cDelaySeconds").ToLower().Contains(args[0].ToLower()))
            {
                if (args.Length == 1)
                {
                    SendReply(arg, Lang("cDelaySeconds") + " = \"" + delaySeconds.ToString() + "\"");
                    return;
                }
                if (IsDigitsOnly(args[1]))
                {
                    Config["delaySeconds"] = Convert.ToInt32(args[1]);
                    delaySeconds = Convert.ToInt32(args[1]);
                    SaveConfig();
                    SendReply(arg, Lang("cDelaySeconds") + " = \"" + args[1] + "\"");
                }
                else
                {
                    SendReply(arg, Lang("mInvalidNumbers"));
                }
                return;
            }
            if (Lang("cEnablePrefixLabel").ToLower().Contains(args[0].ToLower()))
            {
                if (args.Length == 1)
                {
                    SendReply(arg, Lang("cEnablePrefixLabel") + " = \"" + enablePrefixLabel.ToString() + "\"");
                    return;
                }
                if (args[1].ToLower() == "true" || args[1] == "1")
                {
                    Config["enablePrefixLabel"] = true;
                    enablePrefixLabel = true;
                    SaveConfig();
                    SendReply(arg, Lang("cEnablePrefixLabel") + " = \"true\"");
                    return;
                }
                if (args[1].ToLower() == "false" || args[1] == "0")
                {
                    Config["enablePrefixLabel"] = false;
                    enablePrefixLabel = false;
                    SaveConfig();
                    SendReply(arg, Lang("cEnablePrefixLabel") + " = \"false\"");
                    return;
                }
                SendReply(arg, Lang("mInvalidTrueFalse"));
                return;
            }
            if (Lang("cPrefixLabel").ToLower().Contains(args[0].ToLower()))
            {
                if (args.Length == 1)
                {
                    SendReply(arg, Lang("cPrefixLabel") + " = \"" + prefixLabel + "\"");
                    return;
                }

                if (args[2] != null)
                {
                    string rawLabel = string.Join(" ", args);
                    int fWord = rawLabel.IndexOf(" ") + 1;
                    string pLabel = rawLabel.Substring(fWord);
                    Config["prefixLabel"] = pLabel;
                    prefixLabel = pLabel;
                    SaveConfig();
                    SendReply(arg, Lang("cPrefixLabel") + " = \"" + pLabel + "\"");
                }
                else
                {
                    SendReply(arg, Lang("mInvalidLabelNone"));
                }
                return;
            }
            if (Lang("cPrefixLabelColor").ToLower().Contains(args[0].ToLower()))
            {
                if (args.Length == 1)
                {
                    SendReply(arg, Lang("cPrefixLabelColor") + " = \"" + prefixLabelColor + "\"");
                    return;
                }
                if (args[1][0] == '#')
                {
                    Config["prefixLabelColor"] = args[1];
                    prefixLabelColor = args[1];
                    SaveConfig();
                    SendReply(arg, Lang("cPrefixLabelCSolor") + " = \"" + args[1] + "\"");
                }
                else
                {
                    SendReply(arg, Lang("mInvalidColorCode"));
                }
                return;
            }
            if (Lang("cAdvertMessageColor").ToLower().Contains(args[0].ToLower()))
            {
                if (args.Length == 1)
                {
                    SendReply(arg, Lang("cAdvertMessageColor") + " = \"" + advertMessageColor + "\"");
                    return;
                }
                if (args[1][0] == '#')
                {
                    Config["advertMessageColor"] = args[1];
                    advertMessageColor = args[1];
                    SaveConfig();
                    SendReply(arg, Lang("cAdvertMessageColor") + " = \"" + args[1] + "\"");
                }
                else
                {
                    SendReply(arg, Lang("mInvalidColorCode"));
                }
                return;
            }
            if (Lang("cTest").ToLower().Contains(args[0].ToLower()))
            {
                string rawMessage = string.Join(" ", args);
                rawMessage = CheckMessage(rawMessage);
                int firstword = rawMessage.IndexOf(" ") + 1;
                string message = "<color=" + advertMessageColor + ">" + rawMessage.Substring(firstword) + "</color>";
                if (enablePrefixLabel) message = string.Format("<color=" + prefixLabelColor + ">" + prefixLabel + "</color>" + message, player.displayName);
                SendReply(player, message);
                SendReply(arg, "Test Message sent: " + message);
                return;
            }
            if (Lang("cShowLogs").ToLower().Contains(args[0].ToLower()))
            {
                if (args.Length == 1)
                {
                    SendReply(arg, Lang("cShowLogs") + " = \"" +showLogs.ToString() + "\"");
                    return;
                }
                if (args[1].ToLower() == "true" || args[1] == "1")
                {
                    Config["showLogs"] = true;
                    showLogs = true;
                    SaveConfig();
                    SendReply(arg, Lang("cShowLogs") + " = \"true\"");
                    return;
                }
                if (args[1].ToLower() == "false" || args[1] == "0")
                {
                    Config["showLogs"] = false;
                    showLogs = false;
                    SaveConfig();
                    SendReply(arg, Lang("cShowLogs") + " = \"false\"");
                    return;
                }
                SendReply(arg, Lang("mInvalidTrueFalse"));
                return;
            }

            SendReply(arg, Lang("mInvalidCommand"));
        }

        #endregion

        #region Chat Commands

        [ChatCommand(chatCommand)]
        private void Advertisement(BasePlayer player, string command, string[] args)
        {
            if (!allowAdvertToAll)
            {
                if (!HasPermission(player, advertMeAllow, true)) return;
            }

            if (args.Length == 0)
            {
                SendReply(player, "<color=" + prefixLabelColor + ">" + Lang("mPlayerNoMessage") + "</color> <color=#727272>" + Lang("mPlayerMessageIgnored") + "</color>");
                return;
            }

            if (!AdvertedPlayers.Contains(player))
            {
                string rawMessage = string.Join(" ", args);
                rawMessage = CheckMessage(rawMessage);
                string message = "<color=" + advertMessageColor + ">" + rawMessage + "</color>";
                if (enablePrefixLabel) message = string.Format("<color=" + prefixLabelColor + ">" + prefixLabel + "</color>" + message, player.displayName);
                if (showLogs)
                    Puts(player.UserIDString + " | " + player.displayName + ": " + message);
                rust.BroadcastChat(null, message);
                //SendReply(player, message); //^Hash this out and unhash < this to send adverts to you only. (testing)

                if (enableDelay)
                {
                    if (HasPermission(player, advertMeAdmin) && adminNoDelay)
                    {
                        //skip delay
                    }
                    else
                    {
                        AdvertedPlayers.Add(player);
                        timer.Once(delaySeconds, () =>
                        {
                            AdvertedPlayers.Remove(player);
                        });
                    }
                    
                }
            }
            else
            {
                SendReply(player, "<color=" + prefixLabelColor + ">" + Lang("mPlayerDelayMessage") + "</color> <color=#727272>" + Lang("mPlayerMessageIgnored") + " </color>");
            }
        }

        #endregion

        #region Helpers

        private string CheckMessage(string message)
        {
            if (stripChatSize)
            {
                message = Regex.Replace(message, @"<size=..>", "", RegexOptions.IgnoreCase);
                message = Regex.Replace(message, @"</size>", "", RegexOptions.IgnoreCase);
            }

            return message;
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        bool HasPermission(BasePlayer player, string perm, bool skipAdminCheck = false)
        {
            if (player.net.connection.authLevel > 1 && skipAdminCheck)
            {
                return true;
            }
            return permission.UserHasPermission(player.userID.ToString(), perm);
        }

        bool IsDigitsOnly(string str)
        {
            foreach (char c in str)
            {
                if (c < '0' || c > '9')
                    return false;
            }
            return true;
        }

        #endregion
    }
}
