using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Loot Config", "Nogrod", "1.0.30", ResourceId = 861)]
    [Description("Allows you to adjust the server's loot list")]
    public class LootConfig : RustPlugin
    {
        private const int VersionConfig = 15;

        private readonly Regex _findLoot = new Regex(@"(crate[\-_](basic|elite|mine|normal|tools)[\-_\d\w]*(food|medical)*|foodbox[\-_\d\w]*|loot[\-_](barrel|trash)[\-_\d\w]*|heli[\-_]crate[\-_\d\w]*|oil[\-_]barrel[\-_\d\w]*|supply[\-_]drop[\-_\d\w]*|trash[\-_]pile[\-_\d\w]*|/dmloot/.*|giftbox[\-_]loot|stocking[\-_](small|large)[\-_]deployed|minecart|murderer(_corpse)*|bradley[\-_]crate[\-_\d\w]*)\.prefab", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private ConfigData _config;
        private Dictionary<string, ItemDefinition> _itemsDict;

        private new void LoadDefaultConfig()
        {
        }

        private new bool LoadConfig()
        {
            try
            {
                Config.Settings = new JsonSerializerSettings();
                if (!Config.Exists())
                    return CreateDefaultConfig();
                _config = Config.ReadObject<ConfigData>();
            }
            catch (Exception e)
            {
                Puts("Config load failed: {0}{1}{2}", e.Message, Environment.NewLine, e.StackTrace);
                return false;
            }
            return true;
        }

        private void OnServerInitialized()
        {
            if (!LoadConfig())
                return;
            var allPrefabs = GameManifest.Current.pooledStrings.ToList().ConvertAll(p => p.str);
            var prefabs = allPrefabs.Where(p => _findLoot.IsMatch(p)).ToArray();
#if DEBUG
            Puts(string.Join(Environment.NewLine, allPrefabs.ToArray()));
            Puts("Count: " + prefabs.Length);
#endif
            foreach (var source in prefabs)
            {
#if DEBUG
                Puts(source);
#endif
                GameManager.server.FindPrefab(source);
            }
            if (!CheckConfig()) return;
            NextTick(UpdateLoot);
        }

        private void OnSpawnSubCategory(LootSpawn lootSpawn)
        {
            Puts("OnSpawnSubCategory: {0}", lootSpawn.name);
        }

        private void OnSubCategoryIntoContainer(LootSpawn lootSpawn)
        {
            Puts("OnSubCategoryIntoContainer: {0}", lootSpawn.name);
        }

        [ConsoleCommand("loot.reload")]
        private void cmdConsoleReload(ConsoleSystem.Arg arg)
        {
            if (!LoadConfig())
                return;
            if (!CheckConfig()) return;
            UpdateLoot();
            Puts("Loot config reloaded.");
        }

        [ConsoleCommand("loot.dump")]
        private void cmdLootDump(ConsoleSystem.Arg arg)
        {
            LootDump();
        }

        [ConsoleCommand("loot.stats")]
        private void cmdLootStats(ConsoleSystem.Arg arg)
        {
            var lootContainers = Resources.FindObjectsOfTypeAll<LootContainer>();
            var itemModReveal = Resources.FindObjectsOfTypeAll<ItemModReveal>();
            var itemModUnwrap = Resources.FindObjectsOfTypeAll<ItemModUnwrap>();
            var sb = new StringBuilder();
            sb.AppendLine();
            foreach (var lootContainer in lootContainers)
            {
                sb.AppendLine(lootContainer.name);
                PrintLootSpawn(lootContainer.lootDefinition, 1, sb, 1);
            }
            foreach (var reveal in itemModReveal)
            {
                sb.AppendLine(reveal.name);
                PrintLootSpawn(reveal.revealList, 1, sb, 1);
            }
            foreach (var unwrap in itemModUnwrap)
            {
                sb.AppendLine(unwrap.name);
                PrintLootSpawn(unwrap.revealList, 1, sb, 1);
            }
            LogToFile("loot", sb.ToString(), this);
            Puts("Stats written to 'oxide/logs'");
        }

        private bool CreateDefaultConfig()
        {
            Config.Clear();
            var lootSpawns = Resources.FindObjectsOfTypeAll<LootSpawn>();
            var itemModReveals = Resources.FindObjectsOfTypeAll<ItemModReveal>();
            var itemModUnwraps = Resources.FindObjectsOfTypeAll<ItemModUnwrap>();
            var murderers = Resources.FindObjectsOfTypeAll<NPCMurderer>();
            var workbenches = Resources.FindObjectsOfTypeAll<Workbench>();
#if DEBUG
            var sb = new StringBuilder();
            foreach (var reveal in itemModReveals)
            {
                var items = new List<ItemAmount>();
                var stack = new Stack<LootSpawn>();
                stack.Push(reveal.revealList);
                while (stack.Count > 0)
                {
                    var lootSpawn = stack.Pop();
                    if (lootSpawn.subSpawn != null && lootSpawn.subSpawn.Length > 0)
                    {
                        foreach (var entry in lootSpawn.subSpawn)
                        {
                            stack.Push(entry.category);
                        }
                        continue;
                    }
                    if (lootSpawn.items != null) items.AddRange(lootSpawn.items);
                }
                sb.Clear();
                sb.AppendLine(reveal.name);
                sb.AppendLine("Items:");
                foreach (var item in items)
                    sb.AppendLine($"\t{item.itemDef.shortname}: {item.amount}");
                Puts(sb.ToString());
            }
            Puts("LootContainer: {0} LootSpawn: {1} ItemModReveal: {2}", Resources.FindObjectsOfTypeAll<LootContainer>().Length, lootSpawns.Length, itemModReveals.Length);
#endif
            var caseInsensitiveComparer = new CaseInsensitiveComparer();
            Array.Sort(lootSpawns, (a, b) => caseInsensitiveComparer.Compare(a.name, b.name));
            Array.Sort(itemModReveals, (a, b) => caseInsensitiveComparer.Compare(a.name, b.name));
            Array.Sort(itemModUnwraps, (a, b) => caseInsensitiveComparer.Compare(a.name, b.name));
            Array.Sort(murderers, (a, b) => caseInsensitiveComparer.Compare(a.name, b.name));
            Array.Sort(workbenches, (a, b) => caseInsensitiveComparer.Compare(a.name, b.name));
            var spawnGroupsData = new Dictionary<string, Dictionary<string, LootContainer>>();
            var spawnGroups = SpawnHandler.Instance.SpawnGroups;
            //var spawnGroups = (List<SpawnGroup>)SpawnGroupsField.GetValue(SpawnHandler.Instance);
            var monuments = Resources.FindObjectsOfTypeAll<MonumentInfo>();
            var indexes = GetSpawnGroupIndexes(spawnGroups, monuments);
            foreach (var spawnGroup in spawnGroups)
            {
                var spawnGroupKey = GetSpawnGroupKey(spawnGroup, monuments, indexes);
                if (spawnGroup.prefabs == null) continue;
                foreach (var entry in spawnGroup.prefabs)
                {
                    var container = entry.prefab?.Get()?.GetComponent<LootContainer>();
                    if (container?.lootDefinition == null) continue;
                    Dictionary<string, LootContainer> spawnGroupData;
                    if (!spawnGroupsData.TryGetValue(spawnGroupKey, out spawnGroupData))
                        spawnGroupsData[spawnGroupKey] = spawnGroupData = new Dictionary<string, LootContainer>();
                    spawnGroupData[container.PrefabName] = container;
                }
            }
            var containerData = new Dictionary<string, LootContainer>();
            var allPrefabs = GameManifest.Current.pooledStrings.ToList().ConvertAll(p => p.str).Where(p => _findLoot.IsMatch(p)).ToArray();
            Array.Sort(allPrefabs, (a, b) => caseInsensitiveComparer.Compare(a, b));
            foreach (var strPrefab in allPrefabs)
            {
                var container = GameManager.server.FindPrefab(strPrefab)?.GetComponent<LootContainer>();
                if (container == null || container.lootDefinition == null && (container.LootSpawnSlots == null || container.LootSpawnSlots.Length <= 0)) continue;
                containerData[strPrefab] = container;
            }
            /*foreach (var container in containers)
            {
                if (container.gameObject.activeInHierarchy || container.GetComponent<SpawnPointInstance>() != null) continue; //skip spawned & spawn groups
                containerData[container.PrefabName] = container;
            }*/
            /*foreach (var workbench in workbenches)
            {
                Puts("{0} {1} {2}", workbench.name, workbench.isActiveAndEnabled, workbench.gameObject.activeInHierarchy);
            }*/
            try
            {
                Config.Settings = new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    Converters = new List<JsonConverter>
                    {
                        new ItemAmountRangedConverter(),
                        new LootSpawnEntryConverter(),
                        new LootContainerConverter(),
                        new LootSpawnConverter(),
                        new LootSpawnSlotConverter(),
                        new ItemModRevealConverter(),
                        new ItemModUnwrapConverter(),
                        new MurdererConverter(),
                        new WorkbenchConverter(),
                        new StringEnumConverter(),
                    }
                };
                Config.WriteObject(new ExportData
                {
                    Version = Protocol.network,
                    VersionConfig = VersionConfig,
                    WorldSize = World.Size,
                    WorldSeed = World.Seed,
                    LootContainers = containerData,
                    SpawnGroups = spawnGroupsData.OrderBy(l => l.Key).Distinct().Distinct().ToDictionary(l => l.Key, l => l.Value),
                    ItemModReveals = itemModReveals.Distinct().Distinct().ToDictionary(l => l.name),
                    ItemModUnwraps = itemModUnwraps.Where(l => l.revealList != null).Distinct().Distinct().ToDictionary(l => l.name),
                    Murderers = murderers.Where(l => !l.gameObject.activeInHierarchy).Distinct().Distinct().ToDictionary(l => l.name),
                    Workbenches = workbenches.Where(l => !l.gameObject.activeInHierarchy).Distinct().Distinct().ToDictionary(l => l.name),
                    Categories = lootSpawns.Distinct().Distinct().ToDictionary(l => l.name)
                });
            }
            catch (Exception e)
            {
                Puts("Config save failed: {0}{1}{2}", e.Message, Environment.NewLine, e.StackTrace);
                return false;
            }
            Puts("Created new config");
            return LoadConfig();
        }

        private bool CheckConfig()
        {
            if (_config.Version == Protocol.network && _config.VersionConfig == VersionConfig && _config.WorldSize == World.Size && _config.WorldSeed == World.Seed) return true;
            Puts("Incorrect config version({0}/{1}[{2}, {3}])", _config.Version, _config.VersionConfig, _config.WorldSize, _config.WorldSeed);
            if (_config.Version > 0) Config.WriteObject(_config, false, $"{Config.Filename}.old");
            return CreateDefaultConfig();
        }

        private void LootDump()
        {
            var containers = Resources.FindObjectsOfTypeAll<LootContainer>();
            Puts("Containers: {0}", containers.Length);
            foreach (var container in containers)
            {
                Puts("Container: {0} {1} {2}", container.name, container.PrefabName, container.GetInstanceID());
                Puts("Loot: {0} {1}", container.lootDefinition.name, container.lootDefinition.GetInstanceID());
            }
        }

        private static void PrintLootSpawn(LootSpawn lootSpawn, float parentChance, StringBuilder sb, int depth = 0)
        {
            if (lootSpawn.subSpawn != null && lootSpawn.subSpawn.Length > 0)
            {
                sb.Append('\t', depth);
                sb.AppendLine($"{lootSpawn.name} {parentChance:P1}");
                depth++;
                var sum = lootSpawn.subSpawn.Sum(l => l.weight);
                var cur = 0;
                foreach (var entry in lootSpawn.subSpawn)
                {
                    cur += entry.weight;
                    PrintLootSpawn(entry.category, parentChance * (cur / (float)sum), sb, depth);
                }
                return;
            }
            if (lootSpawn.items != null && lootSpawn.items.Length > 0)
            {
                foreach (var amount in lootSpawn.items)
                {
                    sb.Append('\t', depth);
                    sb.AppendLine($"{parentChance:P1} {amount.amount}x {amount.itemDef.shortname} ({lootSpawn.name})");
                }
            }
        }

        private void UpdateLoot()
        {
            _itemsDict = ItemManager.itemList.Distinct().Distinct().ToDictionary(i => i.shortname);
            var lootContainers = Resources.FindObjectsOfTypeAll<LootContainer>();
            var lootSpawnsOld = Resources.FindObjectsOfTypeAll<LootSpawn>();
            var itemModReveals = Resources.FindObjectsOfTypeAll<ItemModReveal>();
            var itemModUnwraps = Resources.FindObjectsOfTypeAll<ItemModUnwrap>();
            var murderers = Resources.FindObjectsOfTypeAll<NPCMurderer>();
            var workbenches = Resources.FindObjectsOfTypeAll<Workbench>();
            var monuments = Resources.FindObjectsOfTypeAll<MonumentInfo>();
#if DEBUG
            Puts("LootContainer: {0} LootSpawn: {1} ItemModReveal: {2}", lootContainers.Length, lootSpawnsOld.Length, itemModReveals.Length);
#endif
            var spawnGroups = SpawnHandler.Instance.SpawnGroups;
            //var spawnGroups = (List<SpawnGroup>)SpawnGroupsField.GetValue(SpawnHandler.Instance);
            var spawnGroupsEnabled = !spawnGroups.Any(spawnGroup => spawnGroup.prefabs.Any(entry => GameManager.server.FindPrefab(entry.prefab.Get().GetComponent<BaseNetworkable>().PrefabName) == entry.prefab.Get()));
            var lootSpawns = new Dictionary<string, LootSpawn>();
            var indexes = GetSpawnGroupIndexes(spawnGroups, monuments);
            foreach (var lootContainer in lootContainers)
            {
#if DEBUG
                Puts("Update LootContainer: {0} {1} {2}", lootContainer.name, lootContainer.PrefabName, lootContainer.GetInstanceID());
#endif
                if (GameManager.server.FindPrefab(lootContainer.PrefabName) != lootContainer.gameObject)
                {
                    if (lootContainer.GetComponent<SpawnPointInstance>() != null)
                    {
                        if (!spawnGroupsEnabled)
                        {
                            UpdateLootContainer(_config.LootContainers, lootContainer, lootSpawns);
                            continue;
                        }
                        var spawnPointInstance = lootContainer.GetComponent<SpawnPointInstance>();
                        var parentSpawnGroup = spawnPointInstance.parentSpawnGroup;
                        Dictionary<string, LootContainerData> spawnGroupData;
                        if (!_config.SpawnGroups.TryGetValue(GetSpawnGroupKey(parentSpawnGroup, monuments, indexes), out spawnGroupData))
                        {
                            Puts("No spawngroup data found: {0}", GetSpawnGroupKey(parentSpawnGroup, monuments, indexes));
                            continue;
                        }
                        UpdateLootContainer(spawnGroupData, lootContainer, lootSpawns);
                        continue;
                    }
                    if (lootContainer.GetComponent<Spawnable>() == null)
                    {
                        if (lootContainer.name.Equals(lootContainer.PrefabName) || lootContainer.name.Equals(Core.Utility.GetFileNameWithoutExtension(lootContainer.PrefabName)))
                        {
#if DEBUG
                            var components = lootContainer.GetComponents<MonoBehaviour>();
                            Puts("Name: {0} Identical: {1}\tActiveP: {2}\tActiveC: {3}", lootContainer.name, GameManager.server.FindPrefab(lootContainer.PrefabName) == lootContainer.gameObject, GameManager.server.FindPrefab(lootContainer.PrefabName).activeInHierarchy, lootContainer.gameObject.activeInHierarchy);
                            Puts("Components: {0}", string.Join(",", components.Select(c => c.GetType().FullName).ToArray()));
                            Puts("Position: {0}", lootContainer.transform.position);
#endif
                            UpdateLootContainer(_config.LootContainers, lootContainer, lootSpawns);
                        }
                        continue;
                    }
                }
                UpdateLootContainer(_config.LootContainers, lootContainer, lootSpawns);
            }
            if (spawnGroupsEnabled)
            {
                foreach (var spawnGroup in spawnGroups)
                {
                    var shouldEmpty = false;
                    foreach (var entry in spawnGroup.prefabs)
                        if (entry.prefab.Get().GetComponent<LootContainer>()?.lootDefinition != null)
                            shouldEmpty = true;
                    if (!shouldEmpty)
                        continue;
                    Dictionary<string, LootContainerData> spawnGroupData;
                    if (!_config.SpawnGroups.TryGetValue(GetSpawnGroupKey(spawnGroup, monuments, indexes), out spawnGroupData))
                    {
                        Puts("No spawngroup data found: {0}", GetSpawnGroupKey(spawnGroup, monuments, indexes));
                        continue;
                    }
                    foreach (var entry in spawnGroup.prefabs)
                        UpdateLootContainer(spawnGroupData, entry.prefab.Get().GetComponent<LootContainer>(), lootSpawns);
                }
            }
            else
            {
                Puts("No SpawnConfig loaded, skipping SpawnGroups...");
            }
            foreach (var reveal in itemModReveals)
            {
#if DEBUG
                Puts("Update ItemModReveal: {0}", reveal.name);
#endif
                ItemModRevealData revealConfig;
                if (!_config.ItemModReveals.TryGetValue(reveal.name.Replace("(Clone)", ""), out revealConfig))
                {
                    Puts("No reveal data found: {0}", reveal.name.Replace("(Clone)", ""));
                    continue;
                }
                var lootSpawn = GetLootSpawn(revealConfig.RevealList, lootSpawns);
                if (lootSpawn == null)
                {
                    Puts("RevealList category '{0}' for '{1}' not found, skipping", revealConfig.RevealList, reveal.name.Replace("(Clone)", ""));
                    continue;
                }
                reveal.numForReveal = revealConfig.NumForReveal;
                reveal.revealedItemAmount = revealConfig.RevealedItemAmount;
                reveal.revealedItemOverride = GetItem(revealConfig.RevealedItemOverride);
                reveal.revealList = lootSpawn;
            }
            foreach (var unwrap in itemModUnwraps)
            {
#if DEBUG
                Puts("Update ItemModUnwrap: {0}", unwrap.name);
#endif
                ItemModUnwrapData unwrapConfig;
                if (!_config.ItemModUnwraps.TryGetValue(unwrap.name.Replace("(Clone)", ""), out unwrapConfig))
                {
                    Puts("No reveal data found: {0}", unwrap.name.Replace("(Clone)", ""));
                    continue;
                }
                var lootSpawn = GetLootSpawn(unwrapConfig.RevealList, lootSpawns);
                if (lootSpawn == null)
                {
                    Puts("RevealList category '{0}' for '{1}' not found, skipping", unwrapConfig.RevealList, unwrap.name.Replace("(Clone)", ""));
                    continue;
                }
                unwrap.revealList = lootSpawn;
            }
            foreach (var murderer in murderers)
            {
#if DEBUG
                Puts("Update Murderer: {0}", murderer.name);
#endif
                MurdererData murdererConfig;
                if (!_config.Murderers.TryGetValue(murderer.name.Replace("(Clone)", ""), out murdererConfig))
                {
                    Puts("No murderer data found: {0}", murderer.name.Replace("(Clone)", ""));
                    continue;
                }
                murderer.LootSpawnSlots = new LootContainer.LootSpawnSlot[murdererConfig.LootSpawnSlots.Length];
                for (var i = 0; i < murdererConfig.LootSpawnSlots.Length; i++)
                {
                    var lootSpawnSlot = murdererConfig.LootSpawnSlots[i];
                    murderer.LootSpawnSlots[i] = new LootContainer.LootSpawnSlot
                    {
                        definition = GetLootSpawn(lootSpawnSlot.Definition, lootSpawns),
                        numberToSpawn = lootSpawnSlot.NumberToSpawn,
                        probability = lootSpawnSlot.Probability
                    };
                }
            }
            foreach (var workbench in workbenches)
            {
#if DEBUG
                Puts("Update Workbench: {0}", workbench.name);
#endif
                WorkbenchData workbenchConfig;
                if (!_config.Workbenches.TryGetValue(workbench.name.Replace("(Clone)", ""), out workbenchConfig))
                {
                    Puts("No workbench data found: {0}", workbench.name.Replace("(Clone)", ""));
                    continue;
                }
                var lootSpawn = GetLootSpawn(workbenchConfig.ExperimentalItems, lootSpawns);
                if (lootSpawn == null)
                {
                    Puts("ExperimentalItems category '{0}' for '{1}' not found, skipping", workbenchConfig.ExperimentalItems, workbench.name.Replace("(Clone)", ""));
                    continue;
                }
                workbench.experimentalItems = lootSpawn;
            }
            _itemsDict = null;
            foreach (var lootSpawn in lootSpawnsOld)
                UnityEngine.Object.Destroy(lootSpawn);
        }

        private void UpdateLootContainer(Dictionary<string, LootContainerData> containerData, LootContainer container, Dictionary<string, LootSpawn> lootSpawns)
        {
            if (container == null) return;
            LootContainerData containerConfig;
            if (containerData == null || !containerData.TryGetValue(container.PrefabName, out containerConfig))
            {
                Puts("No container data found: {0}", container.PrefabName);
                return;
            }
            container.maxDefinitionsToSpawn = containerConfig.MaxDefinitionsToSpawn;
            container.minSecondsBetweenRefresh = containerConfig.MinSecondsBetweenRefresh;
            container.maxSecondsBetweenRefresh = containerConfig.MaxSecondsBetweenRefresh;
            container.destroyOnEmpty = containerConfig.DestroyOnEmpty;
            container.lootDefinition = GetLootSpawn(containerConfig.LootDefinition, lootSpawns);
            container.inventorySlots = containerConfig.InventorySlots;
            container.initialLootSpawn = containerConfig.InitialLootSpawn;
            container.BlockPlayerItemInput = containerConfig.BlockPlayerItemInput;
            container.scrapAmount = containerConfig.ScrapAmount;
            container.SpawnType = containerConfig.SpawnType;
            container.LootSpawnSlots = new LootContainer.LootSpawnSlot[containerConfig.LootSpawnSlots.Length];
            for (var i = 0; i < containerConfig.LootSpawnSlots.Length; i++)
            {
                var lootSpawnSlot = containerConfig.LootSpawnSlots[i];
                container.LootSpawnSlots[i] = new LootContainer.LootSpawnSlot
                {
                    definition = GetLootSpawn(lootSpawnSlot.Definition, lootSpawns),
                    numberToSpawn = lootSpawnSlot.NumberToSpawn,
                    probability = lootSpawnSlot.Probability
                };
            }
            if (container.inventory == null) return;
            container.CancelInvoke(new Action(container.SpawnLoot));
            container.inventory.capacity = containerConfig.InventorySlots;
            container.inventory.SetFlag(ItemContainer.Flag.NoItemInput, container.BlockPlayerItemInput);
            container.inventory.Clear();
            if (container.gameObject.activeInHierarchy && container.initialLootSpawn)
                container.SpawnLoot();
        }

        private LootSpawn GetLootSpawn(string lootSpawnName, Dictionary<string, LootSpawn> lootSpawns)
        {
            if (string.IsNullOrEmpty(lootSpawnName)) return null;
            LootSpawn lootSpawn;
            if (lootSpawns.TryGetValue(lootSpawnName, out lootSpawn)) return lootSpawn;
            LootSpawnData lootSpawnData;
            if (!_config.Categories.TryGetValue(lootSpawnName, out lootSpawnData))
            {
                Puts("Loot category config not found: {0}", lootSpawnName);
                return null;
            }
            lootSpawns[lootSpawnName] = lootSpawn = ScriptableObject.CreateInstance<LootSpawn>();
            lootSpawn.name = lootSpawnName;
            lootSpawn.items = new ItemAmountRanged[lootSpawnData.Items.Length];
            lootSpawn.subSpawn = new LootSpawn.Entry[lootSpawnData.SubSpawn.Length];
            FillItemAmount(lootSpawn.items, lootSpawnData.Items, lootSpawnName);
            for (var i = 0; i < lootSpawnData.SubSpawn.Length; i++)
            {
                var subSpawn = lootSpawnData.SubSpawn[i];
                var category = GetLootSpawn(subSpawn.Category, lootSpawns);
                lootSpawn.subSpawn[i] = new LootSpawn.Entry { category = category, weight = subSpawn.Weight };
            }
            return lootSpawn;
        }

        private void FillItemAmount(ItemAmountRanged[] amounts, ItemAmountRangedData[] amountRangedDatas, string parent)
        {
            for (var i = 0; i < amountRangedDatas.Length; i++)
            {
                var itemAmountData = amountRangedDatas[i];
                var def = GetItem(itemAmountData.Shortname);
                if (def == null)
                {
                    Puts("Item does not exist: {0} for: {1}", itemAmountData.Shortname, parent);
                    continue;
                }
                if (itemAmountData.Amount <= 0)
                {
                    Puts("Item amount too low: {0} for: {1}", itemAmountData.Shortname, parent);
                    continue;
                }
                amounts[i] = new ItemAmountRanged(def, itemAmountData.Amount, itemAmountData.MaxAmount);
            }
        }

        private ItemDefinition GetItem(string shortname)
        {
            if (string.IsNullOrEmpty(shortname) || _itemsDict == null) return null;
            ItemDefinition item;
            return _itemsDict.TryGetValue(shortname, out item) ? item : null;
        }

        private Vector3 FindCenter(SpawnGroup spawnGroup)
        {
            var spawnPoints = SpawnHandler.Instance.SpawnGroups;
            var centroid = new Vector3(0, 0, 0);
            centroid = spawnPoints.Aggregate(centroid, (current, spawnPoint) => current + spawnPoint.transform.position);
            centroid /= spawnPoints.Count;
            return centroid;
        }

        private string GetSpawnGroupKey(SpawnGroup spawnGroup, MonumentInfo[] monuments, Dictionary<SpawnGroup, int> indexes)
        {
            if (!indexes.ContainsKey(spawnGroup))
                return "unknown";
            var index = indexes[spawnGroup];
            return $"{GetSpawnGroupId(spawnGroup, monuments)}{(index > 0 ? $"_{index}" : string.Empty)}";
        }

        private string GetSpawnGroupId(SpawnGroup spawnGroup, MonumentInfo[] monuments)
        {
            var centroid = FindCenter(spawnGroup);
            var monument = FindClosest(centroid, monuments);
            return (monument == null ? $"{spawnGroup.name.Replace(" ", "_")}_{Id(centroid)}" : $"{Utility.GetFileNameWithoutExtension(monument.name)}_{spawnGroup.name.Replace(" ", "_")}_{Id(monument)}").ToLower();
        }

        private Dictionary<SpawnGroup, int> GetSpawnGroupIndexes(List<SpawnGroup> spawnGroups, MonumentInfo[] monuments)
        {
            var monumentGroups = new Dictionary<string, List<SpawnGroup>>();
            foreach (var spawnGroup in spawnGroups)
            {
                var monument = GetSpawnGroupId(spawnGroup, monuments);
                List<SpawnGroup> groups;
                if (!monumentGroups.TryGetValue(monument, out groups))
                    monumentGroups[monument] = groups = new List<SpawnGroup>();
                groups.Add(spawnGroup);
            }
            var indexes = new Dictionary<SpawnGroup, int>();
            foreach (var monumentGroup in monumentGroups)
            {
                monumentGroup.Value.Sort((a, b) =>
                {
                    var centerA = FindCenter(a);
                    var centerB = FindCenter(b);
                    if (centerA.y < centerB.y)
                        return -1;
                    if (centerA.y > centerB.y)
                        return 1;
                    return 0;
                });
                for (var i = 0; i < monumentGroup.Value.Count; i++)
                    indexes[monumentGroup.Value[i]] = i;
            }
            return indexes;
        }

        private static string Id(MonoBehaviour entity)
        {
            if (entity == null) return "XYZ";
            return Id(entity.transform.position);
        }

        private static string Id(Vector3 position)
        {
            return $"X{Math.Ceiling(position.x)}Y{Math.Ceiling(position.y)}Z{Math.Ceiling(position.z)}";
        }

        private static MonumentInfo FindClosest(Vector3 point, MonumentInfo[] monumentInfos)
        {
            MonumentInfo monument = null;
            var distance = 9999f;
            foreach (var monumentInfo in monumentInfos)
            {
                if (!monumentInfo.gameObject.activeInHierarchy) continue;
                var curDistance = Vector3.Distance(point, monumentInfo.transform.position);
                if (!(curDistance < distance)) continue;
                distance = curDistance;
                monument = monumentInfo;
            }
            return monument;
        }

        #region Nested type: ConfigData

        public class ConfigData
        {
            public int Version { get; set; }
            public int VersionConfig { get; set; }
            public uint WorldSize { get; set; }
            public uint WorldSeed { get; set; }
            public Dictionary<string, LootContainerData> LootContainers { get; set; } = new Dictionary<string, LootContainerData>();
            public Dictionary<string, Dictionary<string, LootContainerData>> SpawnGroups { get; set; } = new Dictionary<string, Dictionary<string, LootContainerData>>();
            public Dictionary<string, ItemModRevealData> ItemModReveals { get; set; } = new Dictionary<string, ItemModRevealData>();
            public Dictionary<string, ItemModUnwrapData> ItemModUnwraps { get; set; } = new Dictionary<string, ItemModUnwrapData>();
            public Dictionary<string, MurdererData> Murderers { get; set; } = new Dictionary<string, MurdererData>();
            public Dictionary<string, WorkbenchData> Workbenches { get; set; } = new Dictionary<string, WorkbenchData>();
            public Dictionary<string, LootSpawnData> Categories { get; set; } = new Dictionary<string, LootSpawnData>();
        }

        #endregion Nested type: ConfigData

        #region Nested type: ExportData

        public class ExportData
        {
            public int Version { get; set; }
            public int VersionConfig { get; set; }
            public uint WorldSize { get; set; }
            public uint WorldSeed { get; set; }
            public Dictionary<string, LootContainer> LootContainers { get; set; } = new Dictionary<string, LootContainer>();
            public Dictionary<string, Dictionary<string, LootContainer>> SpawnGroups { get; set; } = new Dictionary<string, Dictionary<string, LootContainer>>();
            public Dictionary<string, ItemModReveal> ItemModReveals { get; set; } = new Dictionary<string, ItemModReveal>();
            public Dictionary<string, ItemModUnwrap> ItemModUnwraps { get; set; } = new Dictionary<string, ItemModUnwrap>();
            public Dictionary<string, NPCMurderer> Murderers { get; set; } = new Dictionary<string, NPCMurderer>();
            public Dictionary<string, Workbench> Workbenches { get; set; } = new Dictionary<string, Workbench>();
            public Dictionary<string, LootSpawn> Categories { get; set; } = new Dictionary<string, LootSpawn>();
        }

        #endregion Nested type: ExportData

        #region Nested type: ItemAmountRangedConverter

        private class ItemAmountRangedConverter : JsonConverter
        {
            public override bool CanRead => false;

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var itemAmount = (ItemAmountRanged)value;
                if (itemAmount.itemDef == null) return;
                writer.WriteStartObject();
                writer.WritePropertyName("Shortname");
                writer.WriteValue(itemAmount.itemDef.shortname);
                writer.WritePropertyName("Amount");
                writer.WriteValue(itemAmount.amount);
                writer.WritePropertyName("MaxAmount");
                writer.WriteValue(itemAmount.maxAmount);
                writer.WriteEndObject();
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                return null;
            }

            public override bool CanConvert(Type objectType)
            {
                return typeof(ItemAmountRanged).IsAssignableFrom(objectType);
            }
        }

        #endregion Nested type: ItemAmountRangedConverter

        #region Nested type: ItemAmountRangedData

        public class ItemAmountRangedData
        {
            public string Shortname { get; set; }
            public float Amount { get; set; }
            public float MaxAmount { get; set; } = -1;
        }

        #endregion Nested type: ItemAmountRangedData

        #region Nested type: ItemModRevealConverter

        private class ItemModRevealConverter : JsonConverter
        {
            public override bool CanRead => false;

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var entry = (ItemModReveal)value;
                writer.WriteStartObject();
                writer.WritePropertyName("NumForReveal");
                writer.WriteValue(entry.numForReveal);
                writer.WritePropertyName("RevealedItemOverride");
                writer.WriteValue(entry.revealedItemOverride?.shortname);
                writer.WritePropertyName("RevealedItemAmount");
                writer.WriteValue(entry.revealedItemAmount);
                writer.WritePropertyName("RevealList");
                writer.WriteValue(entry.revealList.name);
                writer.WriteEndObject();
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                return null;
            }

            public override bool CanConvert(Type objectType)
            {
                return typeof(ItemModReveal).IsAssignableFrom(objectType);
            }
        }

        #endregion Nested type: ItemModRevealConverter

        #region Nested type: ItemModRevealData

        public class ItemModRevealData
        {
            public int NumForReveal { get; set; } = 10;
            public string RevealedItemOverride { get; set; }
            public int RevealedItemAmount { get; set; } = 1;
            public string RevealList { get; set; }
        }

        #endregion Nested type: ItemModRevealData

        #region Nested type: ItemModUnwrapConverter

        private class ItemModUnwrapConverter : JsonConverter
        {
            public override bool CanRead => false;

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var entry = (ItemModUnwrap)value;
                writer.WriteStartObject();
                writer.WritePropertyName("RevealList");
                writer.WriteValue(entry.revealList.name);
                writer.WriteEndObject();
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                return null;
            }

            public override bool CanConvert(Type objectType)
            {
                return typeof(ItemModUnwrap).IsAssignableFrom(objectType);
            }
        }

        #endregion Nested type: ItemModUnwrapConverter

        #region Nested type: ItemModUnwrapData

        public class ItemModUnwrapData
        {
            public string RevealList { get; set; }
        }

        #endregion Nested type: ItemModUnwrapData

        #region Nested type: LootContainerConverter

        private class LootContainerConverter : JsonConverter
        {
            public override bool CanRead => false;

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var container = (LootContainer)value;
                writer.WriteStartObject();
                writer.WritePropertyName("DestroyOnEmpty");
                writer.WriteValue(container.destroyOnEmpty);
                writer.WritePropertyName("LootDefinition");
                writer.WriteValue(container.lootDefinition?.name ?? string.Empty);
                writer.WritePropertyName("MaxDefinitionsToSpawn");
                writer.WriteValue(container.maxDefinitionsToSpawn);
                writer.WritePropertyName("MinSecondsBetweenRefresh");
                writer.WriteValue(container.minSecondsBetweenRefresh);
                writer.WritePropertyName("MaxSecondsBetweenRefresh");
                writer.WriteValue(container.maxSecondsBetweenRefresh);
                writer.WritePropertyName("InitialLootSpawn");
                writer.WriteValue(container.initialLootSpawn);
                writer.WritePropertyName("BlockPlayerItemInput");
                writer.WriteValue(container.BlockPlayerItemInput);
                writer.WritePropertyName("ScrapAmount");
                writer.WriteValue(container.scrapAmount);
                writer.WritePropertyName("SpawnType");
                serializer.Serialize(writer, container.SpawnType);
                //writer.WriteValue(container.SpawnType.ToString());
                writer.WritePropertyName("InventorySlots");
                writer.WriteValue(container.inventorySlots);
                writer.WritePropertyName("LootSpawnSlots");
                serializer.Serialize(writer, container.LootSpawnSlots);
                writer.WriteEndObject();
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                return null;
            }

            public override bool CanConvert(Type objectType)
            {
                return typeof(LootContainer).IsAssignableFrom(objectType);
            }
        }

        #endregion Nested type: LootContainerConverter

        #region Nested type: LootContainerData

        public class LootContainerData
        {
            public bool DestroyOnEmpty { get; set; } = true;
            public string LootDefinition { get; set; }
            public int MaxDefinitionsToSpawn { get; set; }
            public float MinSecondsBetweenRefresh { get; set; } = 3600f;
            public float MaxSecondsBetweenRefresh { get; set; } = 7200f;
            public bool InitialLootSpawn { get; set; } = true;
            public bool BlockPlayerItemInput { get; set; }
            public int ScrapAmount { get; set; }
            public LootContainer.spawnType SpawnType { get; set; }
            public int InventorySlots { get; set; }
            public LootSpawnSlotData[] LootSpawnSlots { get; set; }
        }

        #endregion Nested type: LootContainerData

        #region Nested type: LootSpawnConverter

        private class LootSpawnConverter : JsonConverter
        {
            public override bool CanRead => false;

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var lootSpawn = (LootSpawn)value;
                writer.WriteStartObject();
                writer.WritePropertyName("Items");
                serializer.Serialize(writer, lootSpawn.items);
                writer.WritePropertyName("SubSpawn");
                serializer.Serialize(writer, lootSpawn.subSpawn);
                writer.WriteEndObject();
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                return null;
            }

            public override bool CanConvert(Type objectType)
            {
                return typeof(LootSpawn).IsAssignableFrom(objectType);
            }
        }

        #endregion Nested type: LootSpawnConverter

        #region Nested type: LootSpawnData

        public class LootSpawnData
        {
            public ItemAmountRangedData[] Items { get; set; } = new ItemAmountRangedData[0];
            public LootSpawnEntryData[] SubSpawn { get; set; } = new LootSpawnEntryData[0];
        }

        #endregion Nested type: LootSpawnData

        #region Nested type: LootSpawnSlotConverter

        private class LootSpawnSlotConverter : JsonConverter
        {
            public override bool CanRead => false;

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var lootSpawnSlot = (LootContainer.LootSpawnSlot)value;
                writer.WriteStartObject();
                writer.WritePropertyName("Definition");
                serializer.Serialize(writer, lootSpawnSlot.definition.name);
                writer.WritePropertyName("NumberToSpawn");
                serializer.Serialize(writer, lootSpawnSlot.numberToSpawn);
                writer.WritePropertyName("Probability");
                serializer.Serialize(writer, lootSpawnSlot.probability);
                writer.WriteEndObject();
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                return null;
            }

            public override bool CanConvert(Type objectType)
            {
                return typeof(LootContainer.LootSpawnSlot).IsAssignableFrom(objectType);
            }
        }

        #endregion Nested type: LootSpawnSlotConverter

        #region Nested type: LootSpawnSlotData

        public class LootSpawnSlotData
        {
            public string Definition { get; set; }
            public int NumberToSpawn { get; set; }
            public float Probability { get; set; }
        }

        #endregion Nested type: LootSpawnSlotData

        #region Nested type: LootSpawnEntryConverter

        private class LootSpawnEntryConverter : JsonConverter
        {
            public override bool CanRead => false;

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var entry = (LootSpawn.Entry)value;
                writer.WriteStartObject();
                writer.WritePropertyName("Category");
                writer.WriteValue(entry.category.name);
                writer.WritePropertyName("Weight");
                writer.WriteValue(entry.weight);
                writer.WriteEndObject();
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                return null;
            }

            public override bool CanConvert(Type objectType)
            {
                return typeof(LootSpawn.Entry).IsAssignableFrom(objectType);
            }
        }

        #endregion Nested type: LootSpawnEntryConverter

        #region Nested type: LootSpawnEntryData

        public class LootSpawnEntryData
        {
            public string Category { get; set; }
            public int Weight { get; set; }
        }

        #endregion Nested type: LootSpawnEntryData

        #region Nested type: MurdererConverter

        private class MurdererConverter : JsonConverter
        {
            public override bool CanRead => false;

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var entry = (NPCMurderer)value;
                writer.WriteStartObject();
                writer.WritePropertyName("LootSpawnSlots");
                serializer.Serialize(writer, entry.LootSpawnSlots);
                writer.WriteEndObject();
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                return null;
            }

            public override bool CanConvert(Type objectType)
            {
                return typeof(NPCMurderer).IsAssignableFrom(objectType);
            }
        }

        #endregion Nested type: MurdererConverter

        #region Nested type: MurdererData

        public class MurdererData
        {
            public LootSpawnSlotData[] LootSpawnSlots { get; set; }
        }

        #endregion Nested type: MurdererData

        #region Nested type: WorkbenchConverter

        private class WorkbenchConverter : JsonConverter
        {
            public override bool CanRead => false;

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var entry = (Workbench)value;
                writer.WriteStartObject();
                writer.WritePropertyName("ExperimentalItems");
                writer.WriteValue(entry.experimentalItems.name);
                writer.WriteEndObject();
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                return null;
            }

            public override bool CanConvert(Type objectType)
            {
                return typeof(Workbench).IsAssignableFrom(objectType);
            }
        }

        #endregion Nested type: WorkbenchConverter

        #region Nested type: WorkbenchData

        public class WorkbenchData
        {
            public string ExperimentalItems { get; set; }
        }

        #endregion Nested type: WorkbenchData
    }
}
