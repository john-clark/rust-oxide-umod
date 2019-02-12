using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Research Block", "Orange", "1.0.0")]
    [Description("Allows to block researching several items")]
    public class ResearchBlock : RustPlugin
    {
        #region Oxide Hooks

        private object CanResearchItem(BasePlayer player, Item item)
        {
            return config.shortnames.Contains(item.info.shortname) ? false : (object) null;
        }

        #endregion
        
        #region Config
        
        private static ConfigData config;
        
        private class ConfigData
        {    
            [JsonProperty(PropertyName = "Blocked shortnames")]
            public List<string> shortnames;
        }
        
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                shortnames = new List<string>
                {
                    "rifle.ak",
                    "ammo.rifle"
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