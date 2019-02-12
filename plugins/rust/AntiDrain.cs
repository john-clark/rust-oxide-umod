using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Anti Drain", "Orange", "1.1.0")]
    [Description("Plugin allows to prevent turret draining")]
    public class AntiDrain : RustPlugin
    {
        #region Vars

        private List<uint> checking = new List<uint>();
        
        private List<uint> blocked = new List<uint>();

        #endregion

        #region Oxide Hooks
        
        private bool CanBeTargeted(BasePlayer player, StorageContainer turret)
        {
            return CheckTurret(turret, player);
        }

        #endregion

        #region Core

        private bool CheckTurret(StorageContainer turret, BasePlayer player)
        {
            if (turret == null || player == null) {return true;}
            if (!turret.OwnerID.IsSteamId()) {return true;}
            var id = turret.net.ID;
            if (blocked.Contains(id)) {return false;}
            if (checking.Contains(id)) {return true;}
            Check(turret);
            return true;
        }
        
        private void Check(StorageContainer turret)
        {
            var id = turret.net.ID;
            var was = GetAmmoCount(turret);
            checking.Add(id);
            
            timer.Once(config.checkTime, () =>
            {
                checking.Remove(id);
                var now = GetAmmoCount(turret);

                if (was - now > config.ammoDifference)
                {
                    Block(id);
                }
            });
        }
        
        private void Block(uint id)
        {
            if (blocked.Contains(id)) {return;}
            blocked.Add(id);
            timer.Once(config.blockTime, () => { blocked.Remove(id); });
        }
        
        private int GetAmmoCount(StorageContainer container)
        {
            var i = 0;

            foreach (var item in container.inventory.itemList)
            {
                if (item.info.category == ItemCategory.Ammunition)
                {
                    i += item.amount;
                }
            }

            return i;
        }

        #endregion

        #region Config

        private static ConfigData config;
        
        private class ConfigData
        {    
            [JsonProperty(PropertyName = "1. Time after shooting when will be checked ammo count")]
            public int checkTime;
            
            [JsonProperty(PropertyName = "2. Ammo count difference to block the turret")]
            public int ammoDifference;
            
            [JsonProperty(PropertyName = "3. Time when turret will be blocked to shoot after drain-catch")]
            public int blockTime;
        }
        
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                blockTime = 60,
                checkTime = 30,
                ammoDifference = 100
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
    }
}