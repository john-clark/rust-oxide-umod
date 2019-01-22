using Newtonsoft.Json;
using Oxide.Core;
using Rust;
using System.Collections.Generic;
using System.IO;

namespace Oxide.Plugins
{
    [Info("Water Limits", "Wulf/lukespragg", "3.1.0")]
    [Description("Hurts or kills players that are in water under conditions")]
    public class WaterLimits : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Hurt player on contact (true/false)")]
            public bool HurtOnContact { get; set; } = false;

            [JsonProperty(PropertyName = "Hurt player on disconnect (true/false)")]
            public bool HurtOnDisconnect { get; set; } = true;

            [JsonProperty(PropertyName = "Hurt player over time (true/false)")]
            public bool HurtOverTime { get; set; } = true;

            [JsonProperty(PropertyName = "Kill player on contact (true/false)")]
            public bool KillOnContact { get; set; } = false;

            [JsonProperty(PropertyName = "Kill player on disconnect (true/false)")]
            public bool KillOnDisconnect { get; set; } = false;

            [JsonProperty(PropertyName = "Damage player amount (1 - 500)")]
            public int DamageAmount { get; set; } = 1;

            [JsonProperty(PropertyName = "Damage player every (seconds)")]
            public int DamageEvery { get; set; } = 10;
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

        #region Initialization

        private readonly Dictionary<ulong, Timer> timers = new Dictionary<ulong, Timer>();

        private const string permExclude = "waterlimits.exclude";

        private void Init()
        {
            permission.RegisterPermission(permExclude, this);

            if (!config.HurtOnContact && !config.KillOnContact)
            {
                Unsubscribe(nameof(OnRunPlayerMetabolism));
            }
        }

        #endregion Initialization

        #region Water Checking

        private bool IsInWater(BasePlayer player)
        {
            ModelState modelState = player.modelState;
#if DEBUG
            LogWarning($"{player.displayName} is {System.Math.Ceiling(modelState.waterLevel)}% underwater");
#endif
            return modelState != null && modelState.waterLevel > 0f && player.metabolism.wetness.value > 0f;
        }

        private void WaterCheck(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, permExclude) || !IsInWater(player))
            {
                return;
            }

#if DEBUG
            LogWarning($"{player.displayName} is in water: {IsInWater(player)}");
            LogWarning($"{player.displayName} is {System.Math.Ceiling(player.metabolism.wetness.value)}% wet");
#endif

            if (config.KillOnContact || config.KillOnDisconnect)
            {
                player.Hurt(1000f, DamageType.Drowned, null, false);
            }
            else if (config.HurtOnContact || config.HurtOnDisconnect)
            {
                if (config.HurtOverTime)
                {
                    if (!timers.ContainsKey(player.userID))
                    {
                        timers[player.userID] = timer.Every(config.DamageEvery, () =>
                        {
                            if (player.IsDead() && timers.ContainsKey(player.userID))
                            {
                                timers[player.userID].Destroy();
                            }
                            else
                            {
                                player.Hurt(config.DamageAmount, DamageType.Drowned, null, false);
                            }
                        });
                    }
                }
                else
                {
                    player.Hurt(config.DamageAmount, DamageType.Drowned, null, false);
                }
            }
        }

        #endregion Water Checking

        #region Player Handling

        private void OnServerInitialized()
        {
            foreach (BasePlayer sleeper in BasePlayer.sleepingPlayerList)
            {
                WaterCheck(sleeper);
            }
        }

        private void OnPlayerConnected(Network.Message packet)
        {
            if (timers.ContainsKey(packet.connection.userid))
            {
                timers[packet.connection.userid].Destroy();
            }
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            WaterCheck(player);
        }

        private void OnRunPlayerMetabolism(PlayerMetabolism metabolism, BaseCombatEntity entity)
        {
            BasePlayer player = entity.ToPlayer();
            if (player != null)
            {
                WaterCheck(player);
            }
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (timers.ContainsKey(player.userID))
            {
                timers[player.userID].Destroy();
            }
        }

        #endregion Player Handling
    }
}
