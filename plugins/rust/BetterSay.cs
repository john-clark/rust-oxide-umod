using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Better Say", "LaserHydra", "3.0.0")]
    [Description("Allows customizing the output of the Rust 'say' command")]
    public class BetterSay : RustPlugin
    {
        private Configuration _config;

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission("bettersay.use", this);
            LoadConfig();
        }

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg?.cmd?.FullName == null || arg.cmd.FullName != "global.say")
                return null;

            BasePlayer player = arg.Player();

            if (player != null && !permission.UserHasPermission(player.UserIDString, "bettersay.use"))
            {
                arg.ReplyWith(lang.GetMessage("No Permission", this, player.UserIDString));
                return true;
            }

            if (!arg.HasArgs())
            {
                arg.ReplyWith(lang.GetMessage("Syntax Reminder", this));
                return true;
            }

            string chatOutput = _config.Format
                .Replace("{Title}", $"[#{_config.TitleColor.TrimStart('#')}]{_config.Title}[/#]")
                .Replace("{Message}", $"[#{_config.MessageColor.TrimStart('#')}]{string.Join(" ", arg.Args)}[/#]");

            string consoleOutput = _config.Format
                .Replace("{Title}", _config.Title)
                .Replace("{Message}", string.Join(" ", arg.Args));

            Server.Broadcast(chatOutput, _config.ChatIcon);
            Puts(consoleOutput);

            return true;
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["No Permission"] = "You don't have permission to use this command.",
                ["Syntax Reminder"] = "Syntax: say <message>"
            }, this);
        }

        #endregion

        #region Configuration

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();

        private class Configuration
        {
            [JsonProperty("Format")]
            public string Format { get; set; } = "{Title}: {Message}";

            [JsonProperty("Title")]
            public string Title { get; set; } = "Server";

            [JsonProperty("Title Color")]
            public string TitleColor { get; set; } = "cyan";

            [JsonProperty("Message Color")]
            public string MessageColor { get; set; } = "white";

            [JsonProperty("Chat Icon (SteamID64)")]
            public ulong ChatIcon { get; set; } = 0;
        }

        #endregion
    }
}
