using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("NoFoundationObjects", "mvrb", "1.0.0")]
	[Description("Blocks objects under foundations")]
    class NoFoundationObjects : RustPlugin
    {
        private Dictionary<string, string> deployables = new Dictionary<string, string>();

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Error: CannotPlace"] = "You can't place {0} under foundations!"
            }, this);
        }

        private void OnServerInitialized()
        {
            LoadVariables();

            foreach (ItemDefinition itemDef in ItemManager.GetItemDefinitions())
            {
                if (itemDef.GetComponent<ItemModDeployable>() == null) continue;

                if (!deployables.ContainsKey(itemDef.GetComponent<ItemModDeployable>().entityPrefab.resourcePath))
                {
                    deployables.Add(itemDef.GetComponent<ItemModDeployable>().entityPrefab.resourcePath, itemDef.displayName.english);
                }
            }
        }

        private void OnEntityBuilt(Planner planner, GameObject obj)
        {
            if (planner == null || obj == null) return;

            if (configData.PreventBuildAboveStash && obj.GetComponent<StashContainer>() != null)
            {
                obj.layer = LayerMask.NameToLayer("Prevent Building");
                obj.transform.localScale = new Vector3(1f, 3f, 1f);
            }

            if (obj.GetComponent<BuildingBlock>() != null) return;

            BaseEntity deployable = obj.GetComponent<BaseEntity>();

            if (deployables.ContainsKey(deployable?.gameObject.name))
            {
                string name = deployables[deployable?.gameObject.name];

                if (configData.BlackList.Contains(name))
                {
                    List<BaseEntity> nearby = new List<BaseEntity>();
                    Vis.Entities(obj.transform.position, 2f, nearby, LayerMask.GetMask("Construction"), QueryTriggerInteraction.Ignore);

                    foreach (BaseEntity entity in nearby.Distinct().ToList().Where(x => x.ShortPrefabName == "foundation" && (int)(x as BuildingBlock)?.grade > 1))
                    {
                        float distFromCenter = Vector3.Distance(entity.CenterPoint(), obj.transform.position);

                        if (distFromCenter <= 1.75f && entity.transform.position.y > obj.transform.position.y)
                        {
                            BasePlayer player = planner.GetOwnerPlayer();
                            var ent = obj.GetComponent<BaseEntity>();

                            player.ChatMessage(Lang("Error: CannotPlace", player.UserIDString, name));

                            ent?.KillMessage();
                            break;
                        }
                    }
                }
            }
        }

        #region Config        
        private ConfigData configData;

        class ConfigData
        {
            [JsonProperty(PropertyName = "Prevent building above Small Stashes")]
            public bool PreventBuildAboveStash { get; set; }

            [JsonProperty(PropertyName = "List of blocked objects")]
            public List<string> BlackList { get; set; }
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                PreventBuildAboveStash = true,
                BlackList = new List<string>()
                {
                    "Sleeping Bag",
                    "Furnace",
                    "Repair Bench",
                    "Research Table",
                    "Barbeque",
                    "Lantern",
                    "Skull Fire Pit",
                    "Small Stash",
                    "Camp Fire",
                    "Wood Storage Box",
                    "Large Wood Box",
                    "Jack O lantern Angry",
                    "Jack O lantern Happy"
                }
            };

            SaveConfig(config);
        }

        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();

        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
    }
}