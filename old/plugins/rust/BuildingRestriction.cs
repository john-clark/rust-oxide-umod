using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Building Restriction", "Wulf/lukespragg", "1.5.7")]
    [Description("Restricts building height, building in water, number of foundations, and more")]
    public class BuildingRestriction : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Restrict build height (true/false)")]
            public bool RestrictBuildHeight { get; set; } = true;

            [JsonProperty(PropertyName = "Maximum build height")]
            public int MaxBuildHeight { get; set; } = 5;

            [JsonProperty(PropertyName = "Restrict foundations (true/false)")]
            public bool RestrictFoundations { get; set; } = true;

            [JsonProperty(PropertyName = "Maximum foundations")]
            public int MaxFoundations { get; set; } = 16;

            [JsonProperty(PropertyName = "Maximum triangle foundations")]
            public int MaxTriFoundations { get; set; } = 24;

            [JsonProperty(PropertyName = "Restrict tool cupboards (true/false)")]
            public bool RestrictToolCupboards { get; set; } = true;

            [JsonProperty(PropertyName = "Maximum tool cupboards")]
            public int MaxToolCupboards { get; set; } = 5;

            [JsonProperty(PropertyName = "Restrict water depth (true/false)")]
            public bool RestrictWaterDepth { get; set; } = true;

            [JsonProperty(PropertyName = "Maximum water depth")]
            public double MaxWaterDepth { get; set; } = 0.1;

            [JsonProperty(PropertyName = "Refund resources when restricted")]
            public bool RefundResources { get; set; } = true;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }
            }
            catch
            {
                string configPath = $"{Interface.Oxide.ConfigDirectory}{Path.DirectorySeparatorChar}{Name}";
                LogWarning($"Could not load a valid configuration file, creating a new configuration file at {configPath}.json");
                Config.WriteObject(config, false, $"{configPath}_invalid.json");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion Configuration

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["MaxBuildHeight"] = "You have reached the max building height! ({0} building blocks)",
                ["MaxFoundations"] = "You have reached the max foundations allowed! ({0} foundations)",
                ["MaxTriFoundations"] = "You have reached the max triangle foundations allowed! ({0} foundations)",
                ["MaxWaterDepth"] = "You are not allowed to build in water!"
            }, this);
        }

        #endregion Localization

        #region Initialization

        private const string foundation = "assets/prefabs/building core/foundation/foundation.prefab";
        private const string triFoundation = "assets/prefabs/building core/foundation.triangle/foundation.triangle.prefab";
        private const string permBypass = "buildingrestriction.bypass";

        private readonly Dictionary<uint, List<BuildingBlock>> buildingIds = new Dictionary<uint, List<BuildingBlock>>();
        private readonly Dictionary<uint, List<PlayerNameID>> toolCupboards = new Dictionary<uint, List<PlayerNameID>>();
        private readonly List<string> allowedBuildingBlocks = new List<string>
        {
            "assets/prefabs/building core/floor/floor.prefab",
            "assets/prefabs/building core/floor.frame/floor.frame.prefab",
            "assets/prefabs/building core/floor.triangle/floor.triangle.prefab",
            "assets/prefabs/building core/roof/roof.prefab",
            "assets/prefabs/building core/wall.low/wall.low.prefab"
        };

        private void OnServerInitialized()
        {
            permission.RegisterPermission(permBypass, this);

            FindStructures();
            FindToolCupboards();
        }

        private void FindStructures()
        {
            Puts("Searching for structures, this may take awhile...");

            List<BuildingBlock> foundationBlocks = Resources.FindObjectsOfTypeAll<BuildingBlock>().Where(b => b.PrefabName == foundation || b.PrefabName == triFoundation).ToList();
            foreach (BuildingBlock block in foundationBlocks.Where(b => !buildingIds.ContainsKey(b.buildingID)))
            {
                IEnumerable<BuildingBlock> structure = UnityEngine.Object.FindObjectsOfType<BuildingBlock>().Where(b => b.buildingID == block.buildingID && b.PrefabName == foundation || b.PrefabName == triFoundation);
                buildingIds[block.buildingID] = structure.ToList();
            }

            Puts($"Search complete! Found {buildingIds.Count} structures");
        }

        private void FindToolCupboards()
        {
            Puts("Searching for tool cupboards, this may take awhile...");

            BuildingPrivlidge[] cupboards = UnityEngine.Object.FindObjectsOfType<BuildingPrivlidge>();
            foreach (BuildingPrivlidge cupboard in cupboards.Where(c => !toolCupboards.ContainsKey(c.net.ID)))
            {
                toolCupboards.Add(cupboard.net.ID, cupboard.authorizedPlayers);
            }

            Puts($"Search complete! Found {toolCupboards.Count} tool cupboards");
        }

        #endregion Initialization

        #region Refund Handling

        private void RefundResources(BasePlayer player, BuildingBlock buildingBlock)
        {
            foreach (ItemAmount item in buildingBlock.blockDefinition.grades[(int)buildingBlock.grade].costToBuild)
            {
                Item newItem = ItemManager.CreateByItemID(item.itemid, (int)item.amount);
                if (newItem != null)
                {
                    player.inventory.GiveItem(newItem);
                    player.Command("note.inv", item.itemid, item.amount);
                }
            }
        }

        #endregion Refund Handling

        #region Building/Water Handling

        private void OnEntityBuilt(Planner planner, GameObject go)
        {
            BasePlayer basePlayer = planner?.GetOwnerPlayer();
            if (basePlayer == null)
            {
                return;
            }

            IPlayer player = basePlayer.IPlayer;
            if (player == null || player.HasPermission(permBypass))
            {
                return;
            }

            BaseEntity entity = go.ToBaseEntity();
            BuildingBlock buildingBlock = entity?.GetComponent<BuildingBlock>();
            if (buildingBlock == null)
            {
                return;
            }

#if DEBUG
            player.Message($"Water depth from base block: {buildingBlock.WaterFactor()}");
            player.Message($"Maximum water depth: {config.MaxWaterDepth}");
#endif
            if (config.RestrictWaterDepth && buildingBlock.WaterFactor() >= config.MaxWaterDepth)
            {
                if (config.RefundResources)
                {
                    RefundResources(basePlayer, buildingBlock);
                }

                buildingBlock.Kill(BaseNetworkable.DestroyMode.Gib);
                Message(player, "MaxWaterDepth", config.MaxWaterDepth);
                return;
            }

            string blockName = buildingBlock.PrefabName;
            uint buildingId = buildingBlock.buildingID;
            if (buildingIds.ContainsKey(buildingId))
            {
                List<BuildingBlock> connectingStructure = buildingIds[buildingBlock.buildingID];
                if (config.RestrictFoundations && blockName == foundation || blockName == triFoundation)
                {
                    int foundationCount = GetCountOf(connectingStructure, foundation);
                    int triFoundationCount = GetCountOf(connectingStructure, triFoundation);
#if DEBUG
                    player.Message($"Foundation count: {foundationCount}");
                    player.Message($"Triangle foundation count: {triFoundationCount}");
#endif

                    if (blockName == foundation && foundationCount > config.MaxFoundations)
                    {
                        if (config.RefundResources)
                        {
                            RefundResources(basePlayer, buildingBlock);
                        }

                        buildingBlock.Kill(BaseNetworkable.DestroyMode.Gib);
                        Message(player, "MaxFoundations", config.MaxFoundations);
                    }
                    else if (blockName == triFoundation && triFoundationCount > config.MaxTriFoundations)
                    {
                        if (config.RefundResources)
                        {
                            RefundResources(basePlayer, buildingBlock);
                        }

                        buildingBlock.Kill(BaseNetworkable.DestroyMode.Gib);
                        Message(player, "MaxTriFoundations", config.MaxTriFoundations);
                    }
                    else
                    {
                        List<BuildingBlock> structure = new List<BuildingBlock>(connectingStructure) { buildingBlock };
                        buildingIds[buildingId] = structure;
                    }
                }
                else
                {
                    if (config.RestrictBuildHeight && !allowedBuildingBlocks.Contains(blockName))
                    {
                        BuildingBlock firstFoundation = null;
                        foreach (BuildingBlock block in connectingStructure)
                        {
                            if (block.name.Contains(triFoundation) || block.name.Contains(foundation))
                            {
                                firstFoundation = block;
                                break;
                            }
                        }

                        if (firstFoundation != null)
                        {
                            float height = (float)Math.Round(buildingBlock.transform.position.y - firstFoundation.transform.position.y, 0, MidpointRounding.AwayFromZero);
                            int maxHeight = config.MaxBuildHeight * 3;
#if DEBUG
                            player.Message($"Maximum building height: {maxHeight}");
                            player.Message($"Attempted building height: {height}");
#endif

                            if (height > maxHeight)
                            {
                                if (config.RefundResources)
                                {
                                    RefundResources(basePlayer, buildingBlock);
                                }

                                buildingBlock.Kill(BaseNetworkable.DestroyMode.Gib);
                                Message(player, "MaxBuildHeight", config.MaxBuildHeight);
                            }
                        }
                    }
                }
            }
            else
            {
                List<BuildingBlock> structure = new List<BuildingBlock> { buildingBlock };
                buildingIds[buildingId] = structure;
            }
        }

        private void HandleRemoval(BaseCombatEntity entity)
        {
            BuildingBlock buildingBlock = entity?.GetComponent<BuildingBlock>();
            if (buildingBlock == null)
            {
                return;
            }

            string blockName = buildingBlock.PrefabName;
            if (blockName == null || blockName != foundation && blockName != triFoundation)
            {
                return;
            }

            if (buildingIds.ContainsKey(buildingBlock.buildingID))
            {
                List<BuildingBlock> blockList = buildingIds[buildingBlock.buildingID].Where(b => b == buildingBlock).ToList();
                foreach (BuildingBlock block in blockList)
                {
                    buildingIds[buildingBlock.buildingID].Remove(buildingBlock);
                }
            }
        }

        private void OnStructureDemolish(BaseCombatEntity entity) => HandleRemoval(entity);

        #endregion Building/Water Handling

        #region Tool Cupboard Handling

        private void OnItemDeployed(Deployer deployer, BaseEntity entity)
        {
            BuildingPrivlidge cupboard = entity as BuildingPrivlidge;
            if (cupboard == null)
            {
                return;
            }

            BasePlayer player = deployer.ToPlayer();
            if (config.RestrictToolCupboards && player != null)
            {
                IEnumerable<KeyValuePair<uint, List<PlayerNameID>>> cupboards = toolCupboards.Where(c => c.Value.Contains(new PlayerNameID { userid = player.userID }));

                if (cupboards.Count() > config.MaxToolCupboards)
                {
                    cupboard.Kill();
                    Message(player.IPlayer, "MaxToolCupboards", config.MaxToolCupboards);
                }
            }
            else
            {
                if (!toolCupboards.ContainsKey(cupboard.net.ID))
                {
                    toolCupboards.Add(cupboard.net.ID, cupboard.authorizedPlayers);
                }
            }
        }

        private void OnEntityDeath(BaseCombatEntity entity)
        {
            BuildingPrivlidge cupboard = entity as BuildingPrivlidge;
            if (cupboard != null && toolCupboards.ContainsKey(cupboard.net.ID))
            {
                toolCupboards.Remove(cupboard.net.ID);
            }
            else
            {
                HandleRemoval(entity);
            }
        }

        #endregion Tool Cupboard Handling

        #region Helper Methods

        private int GetCountOf(List<BuildingBlock> ConnectingStructure, string buildingObject)
        {
            int count = 0;
            List<BuildingBlock> blockList = ConnectingStructure.ToList();
            foreach (BuildingBlock block in blockList)
            {
                if (block == null || block.IsDestroyed)
                {
                    ConnectingStructure.Remove(block);
                }
                else if (block.PrefabName == buildingObject)
                {
                    count++;
                }
            }
            return count;
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void Message(IPlayer player, string key, params object[] args) => player.Message(Lang(key, player.Id, args));

        #endregion Helper Methods
    }
}
