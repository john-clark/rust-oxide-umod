// Requires: Babel

using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Babel Chat", "Wulf/lukespragg", "1.1.5")]
    [Description("Translates chat messages to each player's language preference or server default")]
    public class BabelChat : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Force default server language (true/false)")]
            public bool ForceDefault { get; set; } = false;

            [JsonProperty(PropertyName = "Log chat messages (true/false)")]
            public bool LogChatMessages { get; set; } = true;

            [JsonProperty(PropertyName = "Show original message (true/false)")]
            public bool ShowOriginal { get; set; } = false;

            [JsonProperty(PropertyName = "Use random name colors (true/false)")]
            public bool UseRandomColors { get; set; } = false;
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

        #region Chat Translation

        [PluginReference]
        private Plugin Babel, BetterChat, UFilter;

        private static readonly System.Random Random = new System.Random();

        private void Translate(string message, string targetId, string senderId, Action<string> callback)
        {
            string to = config.ForceDefault ? lang.GetServerLanguage() : lang.GetLanguage(targetId);
            string from = lang.GetLanguage(senderId) ?? "auto";
#if DEBUG
            LogWarning($"To: {to}, From: {from}");
#endif
            Babel.Call("Translate", message, to, from, callback);
        }

        private void SendMessage(IPlayer target, IPlayer sender, string message)
        {
            if (config.LogChatMessages)
            {
                LogToFile("log", message, this);
                Log($"{sender.Name}: {message}");
            }

            string prefix = null;

            if (BetterChat != null)
            {
                message = covalence.FormatText(BetterChat.Call<string>("API_GetFormattedMessage", sender, message));
            }
            else if (config.UseRandomColors)
            {
                prefix = covalence.FormatText($"[#{Random.Next(0x1000000):X6}]{sender.Name}[/#]");
            }
            else
            {
                prefix = covalence.FormatText($"[{(sender.IsAdmin ? "#af5af5" : "#55aaff")}]{sender.Name}[/#]");
            }

#if RUST
            target.Command(BetterChat != null ? "chat.add" : "chat.add2", sender.Id, message, prefix);
#else
            target.Message(message, prefix);
#endif
        }

        private object HandleChat(IPlayer player, string message)
        {
            if (UFilter != null)
            {
                string[] advertisements = UFilter.Call<string[]>("Advertisements");
                if (advertisements != null && Enumerable.Contains(advertisements, message))
                {
                    return null;
                }
            }

            foreach (IPlayer target in players.Connected)
            {
#if !DEBUG
                if (player.Equals(target))
                {
                    SendMessage(player, player, message);
                    continue;
                }
#endif

                Action<string> callback = response =>
                {
                    if (config.ShowOriginal)
                    {
                        response = $"{message}\n{response}";
                    }

                    SendMessage(target, player, response);
                };
                Translate(message, target.Id, player.Id, callback);
            }

            return true;
        }

        private object OnUserChat(IPlayer player, string message)
        {
            return HandleChat(player, message);
        }

        private object OnBetterChat(Dictionary<string, object> data)
        {
            return true;
        }

        #endregion Chat Translation
    }
}
