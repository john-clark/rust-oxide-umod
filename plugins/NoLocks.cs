using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("No Locks", "Orange", "1.0.0")]
    [Description("Disabling ability to place locks on certain entities")]
    public class NoLocks : RustPlugin
    {
        #region Oxide Hooks

        private void OnServerInitialized()
        {
            CheckAllEntities();
        }

        #endregion

        #region Helpers

        private void CheckAllEntities()
        {
            foreach (var entity in UnityEngine.Object.FindObjectsOfType<BaseEntity>())
            {
                OnEntitySpawned(entity);
            }
        }
        
        private void OnEntitySpawned(BaseEntity entity)
        {
            if (entity.OwnerID == 0) {return;}
            if (!entity.ShortPrefabName.Contains("lock")) {return;}
            var parent = BaseNetworkable.serverEntities.Find(entity.parentEntity.uid);
            if (parent == null) {return;}
            if (!config.blocked.Contains(parent.ShortPrefabName)){return;}
            entity.Kill();
        }

        #endregion
        
        #region Config

        private static ConfigData config;
        
        private class ConfigData
        {    
            [JsonProperty(PropertyName = "Blocked list of entities:")]
            public List<string> blocked;
        }
        
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                blocked = new List<string>
                {
                    "example",
                    "example",
                    "example"
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
    }
}