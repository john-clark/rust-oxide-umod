using System;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("AntiLootDespawn", "Iv Misticos", "2.0.0")]
    [Description("Change loot despawn time in cupboard radius")]
    public class AntiLootDespawn : RustPlugin
    {
        #region Configuration
        
        private static Configuration _config = new Configuration();

        private class Configuration
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled = true;
            
            [JsonProperty(PropertyName = "Multiplier Inside Building Privilege")]
            public float MultiplierCupboard = 2;
            
            [JsonProperty(PropertyName = "Multiplier Outside Building Privilege")]
            public float MultiplierNonCupboard = 0.5f;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
            }
            catch
            {
                Config.WriteObject(_config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}.jsonError");
                PrintError("The configuration file contains an error and has been replaced with a default config.\n" +
                           "The error configuration file was saved in the .jsonError extension");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);
        
        #endregion
        
        #region Hooks

        private void OnItemDropped(Item item, BaseEntity entity) => SetDespawnTime(entity as DroppedItem);
        
        #endregion

        #region Helpers
        
        private void SetDespawnTime(DroppedItem item)
        {
            if (!_config.Enabled || item == null)
                return;
            
            item.CancelInvoke(nameof(DroppedItem.IdleDestroy));
            item.Invoke(nameof(DroppedItem.IdleDestroy), item.GetDespawnDuration() * (item.GetBuildingPrivilege() == null ? _config.MultiplierNonCupboard : _config.MultiplierCupboard));
        }
        
        #endregion
    }
}