using Newtonsoft.Json;
using Oxide.Core;
using System.IO;

namespace Oxide.Plugins
{
    [Info("Pills Here", "Wulf/lukespragg", "3.1.2")]
    [Description("Recovers health, hunger, and/or hydration by set amounts on item use")]
    public class PillsHere : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Calories amount (0.0 - 500.0)")]
            public float CaloriesAmount { get; set; } = 0f;

            [JsonProperty(PropertyName = "Health amount (0.0 - 100.0)")]
            public float HealthAmount { get; set; } = 20f;

            [JsonProperty(PropertyName = "Hydration amount (0.0 - 250.0)")]
            public float HydrationAmount { get; set; } = 0f;

            [JsonProperty(PropertyName = "Item ID or short name to use")]
            public string ItemIdOrShortName { get; set; } = "antiradpills";
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

        private const string permUse = "pillshere.use";

        private void Init() => permission.RegisterPermission(permUse, this);

        #endregion Initialization

        #region Item Handling

        private void OnItemUse(Item item)
        {
            // Check of item was used by a real player
            BasePlayer player = item.GetOwnerPlayer();
            if (player == null)
            {
                return;
            }

            // Check if item name or ID used is old or not set
            if (string.IsNullOrEmpty(config.ItemIdOrShortName) || config.ItemIdOrShortName == "1685058759")
            {
                config.ItemIdOrShortName = "antiradpills";
                LogWarning("Old or no item configured, using default item: antiradpills");
            }
            
            // Check if item name or ID used matches what is configured
            if (item.info.itemid.ToString() != config.ItemIdOrShortName && item.info.name != config.ItemIdOrShortName)
            {
                return;
            }

            // Check if player has permission to use this
            if (!permission.UserHasPermission(player.UserIDString, permUse))
            {
                return;
            }

            // Heal player and restore calories and hydration
            player.Heal(config.HealthAmount);
            player.metabolism.calories.value += config.CaloriesAmount;
            player.metabolism.hydration.value = player.metabolism.hydration.lastValue + config.HydrationAmount;
        }

        #endregion Item Handling
    }
}
