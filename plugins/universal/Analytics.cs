using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.IO;

namespace Oxide.Plugins
{
    [Info("Analytics", "Wulf/lukespragg", "1.1.2")]
    [Description("Real-time collection and reporting of server events to Google Analytics")]
    public class Analytics : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Google tracking ID (ex. UA-XXXXXXXX-Y)")]
            public string TrackingId { get; set; } = "";

            [JsonProperty(PropertyName = "Track connections (true/false)")]
            public bool TrackConnections { get; set; } = true;

            [JsonProperty(PropertyName = "Track disconnections (true/false)")]
            public bool TrackDisconnections { get; set; } = true;
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

        #region Google Analytics

        private static readonly Dictionary<string, string> Headers = new Dictionary<string, string>
        {
            ["User-Agent"] = $"Oxide/{OxideMod.Version} ({Environment.OSVersion}; {Environment.OSVersion.Platform})"
        };

        private void Collect(IPlayer player, string session)
        {
            if (string.IsNullOrEmpty(config.TrackingId))
            {
                LogWarning("Google tracking ID is not set, analytics will not be collected." +
                           "If you do not have one, see https://support.google.com/analytics/answer/7476135?hl=en#trackingID");
                return;
            }

            string data = $"v=1&tid={config.TrackingId}&sc={session}&t=screenview&cd={server.Name}" +
                          $"&an={covalence.Game}&av={covalence.Game}+v{server.Version}+({server.Protocol})" +
                          $"&cid={player.Id}&ul={player.Language}&uip={player.Address}";

            webrequest.Enqueue("https://www.google-analytics.com/collect", Uri.EscapeUriString(data), (code, response) =>
            {
                if (code != 200 || response == null)
                {
                    LogWarning($"Data: {data}");
                    LogWarning($"HTTP code: {code}");
                    LogWarning($"Response: {response}");
                }
            }, this, RequestMethod.POST, Headers);
        }

        #endregion Google Analytics

        #region Event Collection

        private void CollectAll(string session)
        {
            foreach (IPlayer player in players.Connected)
            {
                Collect(player, session);
            }
        }

        private void OnServerInitialized() => CollectAll("start");

        private void OnServerSave() => CollectAll("start");

        private void OnServerShutdown() => CollectAll("end");

        private void OnUserConnected(IPlayer player) => Collect(player, "start");

        private void OnUserDisconnected(IPlayer player) => Collect(player, "end");

        #endregion Event Collection
    }
}
