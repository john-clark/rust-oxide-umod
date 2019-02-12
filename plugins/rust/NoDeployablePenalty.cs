using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("No Deployable Penalty", "Orange", "1.1.1")]
    [Description("Disables picking up damage")]
    public class NoDeployablePenalty : RustPlugin
    {
        #region Oxide Hooks

        private void OnServerInitialized()
        {
            OnStart();
        }

        private void OnEntitySpawned(BaseCombatEntity entity)
        {
            CheckEntity(entity);
        }

        #endregion

        #region Helpers
        
        private void OnStart()
        {
            foreach (var entity in UnityEngine.Object.FindObjectsOfType<BaseCombatEntity>())
            {
                CheckEntity(entity);
            }
        }

        private void CheckEntity(BaseCombatEntity entity)
        {
            if (!config.entities.Contains(entity.ShortPrefabName))
            {
                return;
            }

            entity.pickup.subtractCondition = 0f;
        }

        #endregion
        
        #region Config
        
        private ConfigData config;
        
        private class ConfigData
        {    
            [JsonProperty(PropertyName = "Entities where pickup damage will be disabled")]
            public List<string> entities;
        }
        
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                entities = new List<string>
                {
                    "autoturret_deployed",
                    "box.wooden.large",
                    "etc, etc, etc"
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