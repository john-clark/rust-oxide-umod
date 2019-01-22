using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Temporary Barricades", "Norn", "0.0.2")]
    [Description("Remove defensive barricades after a set time")]
    public class TemporaryBarricades : RustPlugin
    {
        private void Init()
        {
            LoadConfig();
            Puts($"Loaded {config.barricadePrefabs.Count.ToString()} barricade prefabs.");
            Puts($"Removing entities after {config.removeAfter.ToString()} seconds.");
        }

        private ConfigFile config;

        public class ConfigFile
        {
            [JsonProperty(PropertyName = "BarricadePrefabs")]
            public List<string> barricadePrefabs;

            [JsonProperty(PropertyName = "RemoveAfter")]
            public int removeAfter;

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile()
                {
                    removeAfter = 1800,
                    barricadePrefabs = new List<string>
                    {
                        "wall.external.high.stone",
                        "wall.external.high.wood",
                        "barricade.stone",
                        "barricade.sandbags",
                        "barricade.concrete"
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<ConfigFile>();
            if (config == null) { LoadDefaultConfig(); }
        }

        protected override void LoadDefaultConfig()
        {
            config = ConfigFile.DefaultConfig();
            PrintWarning("Default configuration initiated.");
        }

        protected override void SaveConfig() { Config.WriteObject(config); }

        private void OnEntityBuilt(Planner builder, GameObject o)
        {
            if (builder == null || o == null) { return; }
            BaseEntity entity = o.GetComponent<BaseEntity>();
            if(entity == null) { return; }
            if (config.barricadePrefabs.Contains(entity.ShortPrefabName))
            {
                BasePlayer player = builder.GetOwnerPlayer();
                if (player == null) return;
                if (player.IsBuildingAuthed()) return;
                InvokeHandler.Invoke(entity, delegate { entity.Kill(); }, config.removeAfter);
            }
        }
    }
}