using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Storage Blocker", "Orange", "1.1.0")]
    [Description("Allows to setup items that can't be moved to certain containers")]
    public class StorageBlocker : RustPlugin
    {
        #region Oxide Hooks
        
        ItemContainer.CanAcceptResult CanAcceptItem(ItemContainer container, Item item, int targetPos)
        {
            return CheckConiner(container?.entityOwner?.ShortPrefabName ?? "null", item?.info?.shortname ?? "null");
        }

        #endregion

        #region Configuration
        
        private ConfigData config;
        
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Container (shortname) - list of items (shortname)")]
            public Dictionary<string, List<string>> containers;
        }
        
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                containers = new Dictionary<string, List<string>>
                {
                    ["box.example"] = new List<string>
                    {
                        "item.example",
                        "item.example",
                        "item.example"
                    },
                    ["box.wooden.large"] = new List<string>
                    {
                        "battery.small",
                        "blood"
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

        #region Helpers

        private ItemContainer.CanAcceptResult CheckConiner(string container, string item)
        {
            if (!config.containers.ContainsKey(container))
            {
                return ItemContainer.CanAcceptResult.CanAccept;
            }
            
            return config.containers[container].Contains(item) ? ItemContainer.CanAcceptResult.CannotAccept : ItemContainer.CanAcceptResult.CanAccept;
        }

        #endregion
    }
}