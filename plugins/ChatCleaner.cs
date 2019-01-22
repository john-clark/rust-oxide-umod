using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Chat Cleaner", "Wulf/lukespragg", "0.4.0")]
    [Description("Clears/resets a player's chat when joining the server and on command")]
    public class ChatCleaner : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Clean chat on connect (true/false)")]
            public bool CleanOnConnect { get; set; } = true;

            [JsonProperty(PropertyName = "Show chat cleaned message (true/false)")]
            public bool ShowChatCleaned { get; set; } = true;

            [JsonProperty(PropertyName = "Show welcome message (true/false)")]
            public bool ShowWelcome { get; set; } = true;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            string configPath = $"{Interface.Oxide.ConfigDirectory}{Path.DirectorySeparatorChar}{Name}.json";
            LogWarning($"Could not load a valid configuration file, creating a new configuration file at {configPath}");
            config = new Configuration();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion Configuration

        #region Localization

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ChatCleaned"] = "Chat has been cleaned",
                ["CommandClean"] = "cleanchat",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["Welcome"] = "Welcome to {0}, {1}!"
            }, this);
        }

        #endregion Localization

        #region Initialization

        private const string permUse = "chatcleaner.use";

        private void Init()
        {
            permission.RegisterPermission(permUse, this);

            AddLocalizedCommand("CommandClean", "CleanCommand");
        }

        #endregion Initialization

        #region Chat Cleaning

        // TODO: Add support for BetterChat

        private void OnUserConnected(IPlayer player)
        {
            if (!config.CleanOnConnect) return;

            player.Message(new string('\n', 300));

            if (config.ShowChatCleaned)
            {
                Message(player, "ChatCleaned");
            }

            if (config.ShowWelcome)
            {
                Message(player, "Welcome", server.Name, player.Name);
            }
        }

        private void CleanCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUse))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            player.Message(new string('\n', 300));

            if (config.ShowChatCleaned)
            {
                Message(player, "ChatCleaned");
            }
        }

        #endregion Chat Cleaning

        #region Helpers

        private string Lang(string key, string id = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, id), args);
        }

        private void AddLocalizedCommand(string key, string command)
        {
            foreach (string language in lang.GetLanguages(this))
            {
                Dictionary<string, string> messages = lang.GetMessages(language, this);
                foreach (KeyValuePair<string, string> message in messages.Where(m => m.Key.Equals(key)))
                {
                    if (!string.IsNullOrEmpty(message.Value))
                    {
                        AddCovalenceCommand(message.Value, command);
                    }
                }
            }
        }

        private void Message(IPlayer player, string key, params object[] args)
        {
            player.Reply(Lang(key, player.Id, args));
        }

        #endregion Helpers
    }
}
