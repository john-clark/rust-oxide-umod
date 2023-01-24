using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Building Health", "Orange", "1.0.7")]
    [Description("Allows you to change the maximum health of buildings")]
    public class BuildingHealth : RustPlugin
    {
        #region Vars

        private Dictionary<uint, float> data = new Dictionary<uint, float>();
        private List<string> changed = new List<string>();

        #endregion
        
        #region Oxide Hooks

        private void OnServerInitialized()
        {
            Update(false);
        }

        private void Unload()
        {
            Update(true);
        }
        
        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            CheckEntity(go.ToBaseEntity());
        }

        #endregion

        #region Helpers

        private void Update(bool unload)
        {
            UpdateDB();
            ResetMultiplier(unload);
            ResetHP();
        }

        private void CheckEntity(BaseEntity entity)
        {
            if (entity == null) {return;}
            var name = entity.ShortPrefabName;
            if (changed.Contains(name)) {return;}
            if (!config.percents.ContainsKey(name)) {return;}
            var block = entity.GetComponent<BuildingBlock>();
            if (block == null) {return;}
            var hp = block.health / block.MaxHealth();
            block.blockDefinition.healthMultiplier = config.percents[name] / 100f;
            block.health = hp * block.MaxHealth();
            changed.Add(name);
        }

        private void UpdateDB()
        {
            data.Clear();
            
            foreach (var block in UnityEngine.Object.FindObjectsOfType<BuildingBlock>())
            {
                var id = block.net.ID;
                var hp = block.health / block.MaxHealth();
                data.TryAdd(id, hp);
            }
        }
        
        private void ResetHP()
        {
            foreach (var block in UnityEngine.Object.FindObjectsOfType<BuildingBlock>())
            {
                var id = block.net.ID;
                if(!data.ContainsKey(id)) {return;}
                block.health = data[id] * block.MaxHealth();
            }
        }

        private void ResetMultiplier(bool reset = false)
        {
            foreach (var block in UnityEngine.Object.FindObjectsOfType<BuildingBlock>())
            {
                var name = block.ShortPrefabName;
                var multiplier = reset ? 1f : config.percents[block.ShortPrefabName] /100f;
                block.blockDefinition.healthMultiplier = multiplier;
                if (!changed.Contains(name)) {changed.Add(name);}
            }
        }

        #endregion
        
        #region Config
        
        private ConfigData config;
        
        private class ConfigData
        {    
            [JsonProperty(PropertyName = "Building health in percents")]
            public Dictionary<string, int> percents;
        }
        
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                percents = new Dictionary<string, int>
                {
                    {"roof", 100},
                    {"block.stair.ushape", 100},
                    {"block.stair.lshape", 100},
                    {"wall.low", 100},
                    {"wall.half", 100},
                    {"wall.frame", 100},
                    {"wall.window", 100},
                    {"wall.doorway", 100},
                    {"wall", 100},
                    {"floor", 100},
                    {"floor.triangle", 100},
                    {"floor.frame", 100},
                    {"foundation.steps", 100},
                    {"foundation.triangle", 100},
                    {"foundation", 100}
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