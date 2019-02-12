using System;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using System.Text.RegularExpressions;

//Reference: System.Drawing

namespace Oxide.Plugins
{
    [Info("ColouredNames", "PsychoTea", "1.3.1", ResourceId = 1362)]
    [Description("Allows players to change their name colour in chat.")]

    class ColouredNames : RustPlugin
    {
        [PluginReference] Plugin BetterChat;

        const string permUse = "colourednames.use";
        const string permBypass = "colourednames.bypass";
        const string permSetOthers = "colourednames.setothers";
        const string colourRegex = "^#(?:[0-9a-fA-f]{3}){1,2}$";
        readonly string[] blockedValues = { "{", "}", "size" };

        Dictionary<ulong, string> colour = new Dictionary<ulong, string>();
        List<string> blockedColours = new List<string>();
        bool allowHexcode;

        #region Hooks

        void Init()
        {
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permBypass, this);
            permission.RegisterPermission(permSetOthers, this);

            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "NoPermission", "You don't have permission to use this." },
                { "NoPermissionSetOthers", "You don't have permission to set other players' colours." },
                { "IncorrectUsage", "<color=aqua>Incorrect usage!</color><color=orange> /colour {{colour}} [player]\nFor a list of colours do /colours</color>" },
                { "PlayerNotFound", "Player {0} was not found." },
                { "SizeBlocked", "You may not try and change your size! You sneaky player..." },
                { "HexcodeBlocked", "<color=aqua>ColouredNames: </color><color=orange>Hexcode colour codes have been disabled.</color>" },
                { "InvalidCharacters", "The character '{0}' is not allowed in colours. Please remove it." },
                { "ColourBlocked", "<color=aqua>ColouredNames: </color><color=orange>That colour is blocked.</color>" },
                { "ColourRemoved", "<color=aqua>ColouredNames: </color><color=orange>Name colour removed!</color>" },
                { "ColourChanged", "<color=aqua>ColouredNames: </color><color=orange>Name colour changed to </color><color={0}>{0}</color><color=orange>!</color>" },
                { "ColourChangedFor", "<color=aqua>ColouredNames: </color><color=orange>{0}'s name colour changed to </color><color={1}>{1}</color><color=orange>!</color>" },
                { "ChatMessage", "<color={0}>{1}</color>: {2}" },
                { "LogInfo", "[CHAT] {0}[{1}/{2}] : {3}" },
                { "ColoursInfo", "<color=aqua>ColouredNames</color><color=orange>\nYou may use any colour used in HTML\nEg: \"</color><color=red>red</color><color=orange>\", \"</color><color=blue>blue</color><color=orange>\", \"</color><color=green>green</color><color=orange>\" etc\nOr you may use any hexcode (if enabled), eg \"</color><color=#FFFF00>#FFFF00</color><color=orange>\"\nTo remove your colour, use \"clear\" or \"remove\"\nAn invalid colour will default to </color>white<color=orange></color>" },
                { "CantUseClientside", "You may not use this command from ingame - server cosole only." },
                { "ConsoleColourIncorrectUsage", "Incorrect usage! colour {{userid}} {{colour}}" },
                { "InvalidIDConsole", "Error! {0} is not a SteamID!" },
                { "ConsoleColourChanged", "Colour of {0} changed to {1}." },
                { "InvalidColour", "That colour is not valid. Do /colours for more information on valid colours." }
            }, this);

            ReadData();

            allowHexcode = GetConfig<bool>("AllowHexcode");
            foreach (var obj in GetConfig<List<object>>("BlockedColours"))
                blockedColours.Add(obj.ToString().ToLower());
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file.");

            Config["AllowHexcode"] = true;
            Config["BlockedColours"] = new List<string>() { "#000000", "black" };

            Puts("New config file generated.");
        }

        object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            if (BetterChatIns()) return null;

            BasePlayer player = (BasePlayer)arg.Connection.player;

            if (!colour.ContainsKey(player.userID)) return null;

            string argMsg = arg.GetString(0, "text");
            string message = GetMessage("ChatMessage", player, colour[player.userID], player.displayName, argMsg);

            foreach (BasePlayer bp in BasePlayer.activePlayerList)
                bp.SendConsoleCommand("chat.add", player.UserIDString, message);
            Interface.Oxide.LogInfo(GetMessage("LogInfo", player, player.displayName, player.net.ID.ToString(), player.UserIDString, argMsg));
            return true;
        }

        Dictionary<string, object> OnBetterChat(Dictionary<string, object> dict)
        {
            ulong userId = ulong.Parse((dict["Player"] as IPlayer).Id);
            if (!colour.ContainsKey(userId)) return dict;
            ((Dictionary<string, object>)dict["Username"])["Color"] = colour[userId];
            return dict;
        }

        #endregion

        #region Chat Commands

        [ChatCommand("colour")]
        void colourCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPerm(player))
            {
                SendReply(player, GetMessage("NoPermission", player));
                return;
            }

            if (args.Length == 0)
            {
                SendReply(player, GetMessage("IncorrectUsage", player));
                return;
            }

            string colLower = args[0].ToLower();

            if (colLower == "clear" || colLower == "remove")
            {
                colour.Remove(player.userID);
                SaveData();
                SendReply(player, GetMessage("ColourRemoved", player));
                return;
            }

            var invalid = CheckInvalids(colLower);
            if (invalid != "")
            {
                SendReply(player, GetMessage("InvalidCharacters", player, invalid));
                return;
            }

            if (!CanBypass(player))
            {
                if (!allowHexcode && args[0].Contains("#"))
                {
                    SendReply(player, GetMessage("HexcodeBlocked", player));
                    return;
                }

                if (blockedColours.Where(x => x == colLower).Any())
                {
                    SendReply(player, GetMessage("ColourBlocked", player));
                    return;
                }
            }

            if (!IsValidColour(args[0]))
            {
                SendReply(player, GetMessage("InvalidColour", player));
                return;
            }

            if (args.Length > 1)
            {
                if (!CanSetOthers(player))
                {
                    SendReply(player, GetMessage("NoPermissionSetOthers", player));
                    return;
                }

                BasePlayer target = rust.FindPlayerByName(args[1]);
                if (target == null)
                {
                    SendReply(player, GetMessage("PlayerNotFound", player, args[1]));
                    return;
                }

                ChangeColour(target, args[0]);
                SendReply(player, GetMessage("ColourChangedFor", player, target.displayName, args[0]));
                return;
            }

            ChangeColour(player, args[0]);
            SendReply(player, GetMessage("ColourChanged", player, args[0]));
        }

        [ChatCommand("colours")]
        void coloursCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPerm(player))
            {
                SendReply(player, GetMessage("NoPermission", player));
                return;
            }

            SendReply(player, GetMessage("ColoursInfo", player));
        }

        #endregion

        #region Console Commands

        [ConsoleCommand("colour")]
        void colourConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                arg.ReplyWith(GetConsoleMessage("NoPermission"));
                return;
            }

            string[] args = (arg.Args == null) ? new string[] { } : arg.Args;

            if (args.Length < 2)
            {
                arg.ReplyWith(GetConsoleMessage("ConsoleColourIncorrectUsage"));
                return;
            }

            ulong userId;
            if (!ulong.TryParse(args[0], out userId))
            {
                arg.ReplyWith(GetConsoleMessage("InvalidIDConsole", args[0]));
                return;
            }

            ChangeColour(userId, args[1]);
            string name = (BasePlayer.FindByID(userId)?.displayName ?? args[0]);
            arg.ReplyWith(GetConsoleMessage("ConsoleColourChanged", name, args[1]));
        }

        [ConsoleCommand("viewcolours")]
        void viewColoursCommand(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                arg.ReplyWith(GetConsoleMessage("NoPermission"));
                return;
            }

            List<string> hexcode = new List<string>();
            List<string> others = new List<string>();

            foreach (var kvp in colour)
            {
                if (kvp.Value.ToArray()[0] == '#') hexcode.Add($"{kvp.Key}: {kvp.Value}");
                else others.Add($"{kvp.Key}: {kvp.Value}");
            }

            string message = "";

            float i = 1;
            foreach (var str in hexcode)
            {
                message += str;
                if (i % 3 == 0) message += "\n";
                else message += "       ";
                i++;
            }

            message += "\n";

            i = 1;
            foreach (var str in others)
            {
                message += str;
                if (i % 3 == 0) message += "\n";
                else message += "       ";
                i++;
            }

            arg.ReplyWith(message);
        }

        #endregion

        #region Helpers

        bool IsValidColour(string input) => Regex.Match(input, colourRegex).Success || System.Drawing.Color.FromName(input).IsKnownColor;

        bool BetterChatIns() => (BetterChat != null);

        void ChangeColour(BasePlayer target, string newColour)
        {
            if (!colour.ContainsKey(target.userID)) colour.Add(target.userID, "");
            colour[target.userID] = newColour;
            SaveData();
        }
        void ChangeColour(ulong userId, string newColour) => ChangeColour(new BasePlayer() { userID = userId }, newColour);

        bool HasPerm(BasePlayer player) => (player.IsAdmin || permission.UserHasPermission(player.UserIDString, permUse));
        bool CanBypass(BasePlayer player) => (player.IsAdmin || permission.UserHasPermission(player.UserIDString, permBypass));
        bool CanSetOthers(BasePlayer player) => (player.IsAdmin || permission.UserHasPermission(player.UserIDString, permSetOthers));

        string CheckInvalids(string input) => (blockedValues.Where(x => input.Contains(x)).FirstOrDefault()) ?? string.Empty;

        string GetMessage(string key, BasePlayer player, params string[] args) => String.Format(lang.GetMessage(key, this, player.UserIDString), args);
        string GetConsoleMessage(string key, params string[] args) => GetMessage(key, new BasePlayer() { userID = 0 }, args);

        T GetConfig<T>(string key) => (T)Config[key];

        void ReadData() => colour = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, string>>(this.Title);
        void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(this.Title, colour);
        
        #endregion
    }
}