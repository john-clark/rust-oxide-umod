using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Beds Cooldowns", "Orange", "1.0.0")]
    [Description("Allows to change cooldowns on bags and beds")]
    public class BedsCooldowns : RustPlugin
    {
        #region Config

        private static ConfigData config;
        
        private class ConfigData
        {    
            [JsonProperty(PropertyName = "Permission - Params")]
            public Dictionary<string, OInfo> lists = new Dictionary<string, OInfo>();

            public class OInfo
            {
                [JsonProperty(PropertyName = "Priority")]
                public int priority;
                
                [JsonProperty(PropertyName = "Sleeping bag cooldown")]
                public float bag;
                
                [JsonProperty(PropertyName = "Bed cooldown")]
                public float bed;
            }
        }
        
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                lists = new Dictionary<string, ConfigData.OInfo>
                {
                    ["bedscooldowns.vip1"] = new ConfigData.OInfo
                    {
                        priority = 0,
                        bag = 100,
                        bed = 100
                    },
                    ["bedscooldowns.vip2"] = new ConfigData.OInfo
                    {
                        priority = 2,
                        bag = 50,
                        bed = 50
                    },
                    ["bedscooldowns.vip3"] = new ConfigData.OInfo
                    {
                        priority = 3,
                        bag = 0,
                        bed = 0
                    }
                }
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

        private void Init()
        {
            foreach (var list in config.lists)
            {
                permission.RegisterPermission(list.Key, this);
            }
        }

        private void OnEntitySpawned(BaseNetworkable a)
        {
            CheckEntity(a);
        }

        private void OnPlayerInit(BasePlayer player)
        {
            CheckPlayer(player);
        }

        #endregion

        #region Helpers

        private void CheckEntity(BaseNetworkable a)
        {
            var entity = a.GetComponent<SleepingBag>();
            if (entity == null) {return;}
            if (!entity.OwnerID.IsSteamId()) {return;}
            var cfg = GetConfig(entity.OwnerID.ToString());
            if (cfg == null) {return;}
            SetCooldown(entity, cfg);
        }

        private void CheckPlayer(BasePlayer player)
        {
            var cfg = GetConfig(player.UserIDString);
            if (cfg == null) {return;}

            foreach (var entity in UnityEngine.Object.FindObjectsOfType<SleepingBag>())
            {
                if (entity.OwnerID.IsSteamId() && entity.OwnerID == player.userID)
                {
                    SetCooldown(entity, cfg);
                }
            }
        }
        
        private void SetCooldown(SleepingBag entity, ConfigData.OInfo info)
        {
            var value = entity.ShortPrefabName.Contains("bed") ? info.bed : info.bag;
            entity.secondsBetweenReuses = value;
            entity.SendNetworkUpdate();
        }

        private ConfigData.OInfo GetConfig(string id)
        {
            var i = -1;
            var info = (ConfigData.OInfo) null;

            foreach (var pair in config.lists)
            {
                var perm = pair.Key;
                var value = pair.Value;

                if (permission.UserHasPermission(id, perm))
                {
                    var priority = value.priority;
                    
                    if (priority > i)
                    {
                        i = priority;
                        info = value;
                    }
                }
            }

            return info;
        }

        #endregion
    }
}