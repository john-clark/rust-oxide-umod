using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Radiation Manager", "redBDGR", "1.0.0")]
    [Description("Allows for slight management of radiated zones around the map")]
    class RadiationManager : RustPlugin
    {
        private bool Changed;

        // Minimal
        private float minimalAmount;

        // Low
        private float lowAmount;

        // Medium
        private float mediumAmount;

        // High
        private float highAmount;

        private void OnServerInitialized()
        {
            LoadVariables();
            foreach (TriggerRadiation rad in UnityEngine.Object.FindObjectsOfType<TriggerRadiation>())
            {
                switch (rad.radiationTier)
                {
                    case TriggerRadiation.RadiationTier.MINIMAL:
                        rad.RadiationAmountOverride = minimalAmount;
                        break;
                    case TriggerRadiation.RadiationTier.LOW:
                        rad.RadiationAmountOverride = lowAmount;
                        break;
                    case TriggerRadiation.RadiationTier.MEDIUM:
                        rad.RadiationAmountOverride = mediumAmount;
                        break;
                    case TriggerRadiation.RadiationTier.HIGH:
                        rad.RadiationAmountOverride = highAmount;
                        break;
                }
            }
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {
            minimalAmount = Convert.ToSingle(GetConfig("Minimal Tier Zones", "Radiation Amount", 2f));

            lowAmount = Convert.ToSingle(GetConfig("Low Tier Zones", "Radiation Amount", 10f));

            mediumAmount = Convert.ToSingle(GetConfig("Medium Tier Zones", "Radiation Amount", 25f));

            highAmount = Convert.ToSingle(GetConfig("High Tier Zones", "Radiation Amount", 51));    // Change to 50 to get rid of "radiation leak"

            if (!Changed) return;
            SaveConfig();
            Changed = false;
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
            if (data.TryGetValue(datavalue, out value)) return value;
            value = defaultValue;
            data[datavalue] = value;
            Changed = true;
            return value;
        }
    }
}
