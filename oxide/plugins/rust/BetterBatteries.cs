using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Better Batteries", "Orange", "1.0.1")]
    [Description("Allows players to get better batteries")]
    public class BetterBatteries : RustPlugin
    {
        #region Vars

        private const string permUse = "InfiniteBatteries.use";

        #endregion
        
        #region Config

        private static ConfigData config;
        
        private class ConfigData
        {    
            [JsonProperty(PropertyName = "Make batteries infinite capacity")]
            public bool capacity;

            [JsonProperty(PropertyName = "Make batteries increaced power")]
            public bool output;
            
            [JsonProperty(PropertyName = "Make all batteries better")]
            public bool all;
        }
        
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                capacity = false,
                output = false,
                all = false
                
            };
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
   
            try
            {
                config = Config.ReadObject<ConfigData>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintError("Configuration file is corrupt(or not exists), creating new one!");
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region Oxide Hooks

        private void OnServerInitialized()
        {
            permission.RegisterPermission(permUse, this);
            
            if (config.all)
            {
                ChangeAllBatteries();
            }
        }
        
        private void OnEntitySpawned(BaseNetworkable a)
        {
            var entity = a.GetComponent<ElectricBattery>();
            if (entity == null) {return;}

            if (HasPerm(entity.OwnerID.ToString()) || config.all)
            {
                ChangeBattery(entity);
            }
        }

        #endregion

        #region Helpers

        private bool HasPerm(string id)
        {
            return permission.UserHasPermission(id, permUse);
        }

        #endregion

        #region Core

        private void ChangeBattery(ElectricBattery battery)
        {
            if (config.capacity)
            {
                battery.maxCapactiySeconds = 86399;
                battery.capacitySeconds = battery.maxCapactiySeconds;
            }

            if (config.output)
            {
                battery.maxOutput = 10000;
            }
        }

        private void ChangeAllBatteries()
        {
            foreach (var battery in UnityEngine.Object.FindObjectsOfType<ElectricBattery>())
            {
                ChangeBattery(battery);
            }
        }
        #endregion
    }
}