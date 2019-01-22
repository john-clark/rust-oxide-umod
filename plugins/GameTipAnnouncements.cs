using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Game Tip Announcements", "redBDGR", "1.0.2")]
    [Description("Send notifications to players as gametips")]

    //
    // TODO:
    //
    //  - Add init messages
    //

    class GameTipAnnouncements : RustPlugin
    {
        private bool Changed;
        private const string permissionNameADMIN = "gametipannouncements.admin";

        private float defaultLengnth = 15f;

        private void Init()
        {
            permission.RegisterPermission(permissionNameADMIN, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["NoPermission"] = "You are not allowed to use this command!",
                ["sentgt Invalid Format"] = "Invalid format! /sendgt <playername/id> <message> <length>",
                ["sendgtall Invalid Format"] = "Invalid format! /sendgtall <message> <length>",
                ["sentgt Invalid Format CONSOLE"] = "Invalid format! /sendgt <playername/id> <message> <length>",
                ["sendgtall Invalid Format CONSOLE"] = "Invalid format! /sendgtall <message> <length>",
                ["No Player Found"] = "No players were found with this name / ID",
            }, this);
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {
            defaultLengnth = Convert.ToSingle(GetConfig("Settings", "Default Fade Length", 15f));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        [ChatCommand("sendgt")]
        private void SendGameTipCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionNameADMIN))
            {
                player.ChatMessage(msg("NoPermission", player.UserIDString));
                return;
            }
            if (args.Length != 2 && args.Length != 3)
            {
                player.ChatMessage(msg("sentgt Invalid Format", player.UserIDString));
                return;
            }
            float length = defaultLengnth;
            if (args.Length == 3)
                float.TryParse(args[2], out length);
            BasePlayer receiver = BasePlayer.Find(args[0]);
            if (receiver == null)
            {
                player.ChatMessage(msg("No Player Found", player.UserIDString));
                return;
            }
            CreateGameTip(args[1], receiver, length);
        }

        [ChatCommand("sendgtall")]
        private void SendGameTipAllCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionNameADMIN))
            {
                player.ChatMessage(msg("NoPermission", player.UserIDString));
                return;
            }
            if (args.Length != 1 && args.Length != 2)
            {
                player.ChatMessage(msg("sendgtall Invalid Format", player.UserIDString));
                return;
            }
            float length = defaultLengnth;
            if (args.Length == 2)
                float.TryParse(args[1], out length);
            CreateGameTipAll(args[0], length);
        }

        [ConsoleCommand("sendgt")]
        private void SendGameTipAllCONSOLECMD(ConsoleSystem.Arg args)
        {
            if (args.Connection != null)
                return;
            if (args.Args.Length != 2 && args.Args.Length != 3)
            {
                Puts(msg("sentgt Invalid Format CONSOLE"));
                return;
            }
            float length = defaultLengnth;
            if (args.Args.Length == 3)
                float.TryParse(args.Args[2], out length);
            BasePlayer receiver = BasePlayer.Find(args.Args[0]);
            if (receiver == null)
            {
                Puts(msg("No Player Found"));
                return;
            }
            CreateGameTip(args.Args[1], receiver, length);
        }

        [ConsoleCommand("sendgtall")]
        private void SendGameTipCONSOLSECMD(ConsoleSystem.Arg args)
        {
            if (args.Connection != null)
                return;
            if (args.Args.Length != 1 && args.Args.Length != 2)
            {
                Puts(msg("sendgtall Invalid Format CONSOLE"));
                return;
            }
            float length = defaultLengnth;
            if (args.Args.Length == 2)
                float.TryParse(args.Args[1], out length);
            CreateGameTipAll(args.Args[0], length);
        }

        private void CreateGameTip(string text, BasePlayer player, float length = 30f)
        {
            if (player == null)
                return;
            player.SendConsoleCommand("gametip.hidegametip");
            player.SendConsoleCommand("gametip.showgametip", text);
            timer.Once(length, () => player?.SendConsoleCommand("gametip.hidegametip"));
        }

        private void CreateGameTipAll(string text, float length = 30f)
        {
            foreach(BasePlayer player in BasePlayer.activePlayerList)
                CreateGameTip(text, player, length);
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

        string msg(string key, string id = null) => lang.GetMessage(key, this, id);
    }
}
