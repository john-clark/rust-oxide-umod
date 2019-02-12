using Newtonsoft.Json;
using Oxide.Core;
using System.IO;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Empty Low FPS", "Wulf/lukespragg", "1.0.2")]
    [Description("Sets the server frame rate limit to lower when empty to save CPU usage")]
    class EmptyLowFPS : CovalencePlugin
    {
        #region Configuration

        // DEFAULT FRAME RATE LIMITS
        // #########################
        // 7 Days to Die:  20?
        // Hurtworld:      -1 (no limit)
        // Reign of Kings: 60
        // Rust:           256
        // Unturned:       50

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Frame rate limit when empty")]
            public int FpsLimitEmpty { get; set; } = 10;

            [JsonProperty(PropertyName = "Frame rate limit when not empty")]
            public int FpsLimitNotEmpty { get; set; } = 60;

            [JsonProperty(PropertyName = "Log frame rate limit changes")]
            public bool LogChanges { get; set; } = true;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }
            }
            catch
            {
                string configPath = $"{Interface.Oxide.ConfigDirectory}{Path.DirectorySeparatorChar}{Name}";
                LogWarning($"Could not load a valid configuration file, creating a new configuration file at {configPath}.json");
                Config.WriteObject(config, false, $"{configPath}_invalid.json");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion Configuration

        private void OnServerInitialized()
        {
            if (players.Connected.Any())
            {
                NotEmptyFpsLimit();
            }
            else
            {
                EmptyFpsLimit();
            }
        }

        private void OnUserConnected()
        {
            if (players.Connected.Count() <= 1)
            {
                NotEmptyFpsLimit();
            }
        }

        private void OnUserDisconnected()
        {
            if (players.Connected.Count() <= 1)
            {
                EmptyFpsLimit();
            }
        }

        private void Unload()
        {
            NotEmptyFpsLimit();
        }

        private void EmptyFpsLimit()
        {
            if (config.LogChanges)
            {
                LogWarning("Server is empty, setting FPS limit to " + config.FpsLimitEmpty);
            }

#if HURTWORLD || REIGNOFKINGS || RUST || UNTURNED
            UnityEngine.Application.targetFrameRate = config.FpsLimitEmpty;
#elif SEVENDAYSTODIE
            GameManager.Instance.waitForTargetFPS.TargetFPS = config.FpsLimitEmpty;
#else
            LogWarning("Setting the frame rate is currently not supported for " + covalence.Game.Humanize());
#endif
        }

        private void NotEmptyFpsLimit()
        {
            if (config.LogChanges)
            {
                LogWarning("Server is no longer empty, setting FPS limit to " + config.FpsLimitNotEmpty);
            }

#if HURTWORLD || REIGNOFKINGS || RUST || UNTURNED
            UnityEngine.Application.targetFrameRate = config.FpsLimitNotEmpty;
#elif SEVENDAYSTODIE
            GameManager.Instance.waitForTargetFPS.TargetFPS = config.FpsLimitNotEmpty;
#else
            LogWarning("Setting the frame rate is currently not supported for " + covalence.Game.Humanize());
#endif
        }
    }
}
