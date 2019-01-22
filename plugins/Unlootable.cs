using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Unlootable", "Oranger", "1.1.1")]
    [Description("Give player/group a permission so they can't open any boxes or anything with loot in")]
    public class Unlootable : RustPlugin
    {
        #region Oxide Hooks

        private void Init()
        {
            OnStart();
        }

        private object CanLootEntity(BasePlayer player, DroppedItemContainer container)
        {
            return CanLoot(container.ShortPrefabName, player);
        }
        
        private object CanLootEntity(BasePlayer player, LootableCorpse corpse)
        {
            return CanLoot(corpse.ShortPrefabName, player);
        }

        private object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            return CanLoot(container.ShortPrefabName, player);
        }

        #endregion
        
        #region Helpers

        private void OnStart()
        {
            foreach (var perm in config.containers.Keys)
            {
                permission.RegisterPermission(perm, this);
            }
        }

        private object CanLoot(string container, BasePlayer player)
        {
            foreach (var key in config.containers.Keys)
            {
                if (!permission.UserHasPermission(player.UserIDString, key))
                {
                    continue;
                }

                if (!config.containers.ContainsKey(container))
                {
                    continue;
                }

                if (!config.containers[key].Contains(container))
                {
                    continue;
                }

                return false;
            }

            return null;
        }

        #endregion

        #region Configuration
        
        private ConfigData config;
        
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Permission - List of containers (shortname)")]
            public Dictionary<string, List<string>> containers;
        }
        
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                containers = new Dictionary<string, List<string>>
                {
                    ["permission.example"] = new List<string>
                    {
                        "box1.example",
                        "box2.example",
                        "box3.example"
                    },
                    ["unlootable.bags"] = new List<string>
                    {
                        "murderer_corpse",
                        "player_corpse",
                        "etc, etc, etc"
                    },
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