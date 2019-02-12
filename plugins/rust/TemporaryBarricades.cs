using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
namespace Oxide.Plugins
{
    [Info("Temporary Barricades", "Norn", "0.0.4")]
    [Description("Remove defensive barricades after a set time")]
    public class TemporaryBarricades : RustPlugin
    {
        private void Init()
        {
            Puts($"Loaded {config.barricadePrefabs.Count.ToString()} barricade prefabs.");
            Puts($"Removing entities after {config.removeAfter.ToString()} seconds.");
            if(config.debugMode) { PrintWarning("Debug mode is active."); }
        }

        private ConfigFile config;
        public class ConfigFile
        {
            [JsonProperty(PropertyName = "BarricadePrefabs")]
            public List<string> barricadePrefabs;

            [JsonProperty(PropertyName = "RemoveAfter")]
            public int removeAfter;

            [JsonProperty(PropertyName = "DebugMode")]
            public bool debugMode;

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile()
                {
                    removeAfter = 1800,
                    debugMode = false,
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

        private void VerifyConfig()
        {
            int updateCount = 0;
            if (Config["BarricadePrefabs"] == null) { config.barricadePrefabs = ConfigFile.DefaultConfig().barricadePrefabs; updateCount++; }
            if (Config["RemoveAfter"] == null) { config.removeAfter = ConfigFile.DefaultConfig().removeAfter; updateCount++; }
            if (Config["DebugMode"] == null) { config.debugMode = ConfigFile.DefaultConfig().debugMode; updateCount++; }
            if (updateCount != 0) { Puts($"Updating configuration with {updateCount} new changes."); SaveConfig(); }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<ConfigFile>();
            VerifyConfig();
            if (config == null) { LoadDefaultConfig(); }
        }

        protected override void LoadDefaultConfig() => config = ConfigFile.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config); 

        private void OnEntityBuilt(Planner builder, GameObject gameObject)
        {
            if (builder == null || gameObject == null) { return; }
            BaseEntity entity = gameObject.GetComponent<BaseEntity>();
            if (config.debugMode) { Puts($"[DEBUG] => {entity.ShortPrefabName}"); }
            if (entity == null) { return; }
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