using Rust;
using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core.Configuration;
using Random = System.Random;
using Oxide.Core;
using Oxide.Core.Plugins;
using Facepunch.Extend;

namespace Oxide.Plugins
{
	[Info("BetterLoot", "FuJiCuRa / dcode", "3.0.0")]
	[Description("A light lootcontainer modification system")]
	public class BetterLoot : RustPlugin
	{
		[PluginReference]
		Plugin CustomLootSpawns;

		bool Changed = false;
		int populatedContainers;
		StoredExportNames storedExportNames = new StoredExportNames();
		Random rng = new Random();
		bool initialized = false;
		Dictionary<string, List<string>[]> Items = new Dictionary<string, List<string>[]>();
        Dictionary<string, List<string>[]> Blueprints = new Dictionary<string, List<string>[]>();
		Dictionary<string, int[]> itemWeights = new Dictionary<string, int[]>();
        Dictionary<string, int[]> blueprintWeights = new Dictionary<string, int[]>();
		Dictionary<string, int> totalItemWeight = new Dictionary<string, int>();
		Dictionary<string, int> totalBlueprintWeight = new Dictionary<string, int>();
		DynamicConfigFile lootTable;

		DynamicConfigFile getFile(string file) => Interface.Oxide.DataFileSystem.GetDatafile($"{this.Title}/{file}");
		bool chkFile(string file) => Interface.Oxide.DataFileSystem.ExistsDatafile($"{this.Title}/{file}");
		Dictionary<string, object> lootTables = null;

		static List<object> lootPrefabDefaults()
		{
			var dp = new List<object>()
			{
				"assets/bundled/prefabs/radtown/crate_basic.prefab",
				"assets/bundled/prefabs/radtown/crate_elite.prefab",
				"assets/bundled/prefabs/radtown/crate_mine.prefab",
				"assets/bundled/prefabs/radtown/crate_normal.prefab",
				"assets/bundled/prefabs/radtown/crate_normal_2.prefab",
				"assets/bundled/prefabs/radtown/crate_normal_2_food.prefab",
				"assets/bundled/prefabs/radtown/crate_normal_2_medical.prefab",
				"assets/bundled/prefabs/radtown/crate_tools.prefab",
				"assets/bundled/prefabs/radtown/crate_underwater_advanced.prefab",
				"assets/bundled/prefabs/radtown/crate_underwater_basic.prefab",
				"assets/bundled/prefabs/radtown/foodbox.prefab",
				"assets/bundled/prefabs/radtown/loot_barrel_1.prefab",
				"assets/bundled/prefabs/radtown/loot_barrel_2.prefab",
				"assets/bundled/prefabs/radtown/loot_trash.prefab",
				"assets/bundled/prefabs/radtown/minecart.prefab",
				"assets/bundled/prefabs/radtown/oil_barrel.prefab",
				"assets/prefabs/npc/m2bradley/bradley_crate.prefab",
				"assets/prefabs/npc/patrol helicopter/heli_crate.prefab",
				"assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab",
				"assets/prefabs/misc/supply drop/supply_drop.prefab"
			};
			return dp;
		}

		void LoadAllContainers()
        {
			try { lootTable = getFile("LootTables");}
			catch (JsonReaderException e)
			{
				PrintWarning($"JSON error in 'LootTables' > Line: {e.LineNumber} | {e.Path}");
				Interface.GetMod().UnloadPlugin(this.Title);
				return;
			}
			lootTables = new Dictionary<string, object>();
			lootTables = lootTable["LootTables"] as Dictionary<string, object>;
			if (lootTables == null)
				lootTables = new Dictionary<string, object>();
			bool wasAdded = false;
			foreach (var lootPrefab in lootPrefabsToUse)
            {
				if (!lootTables.ContainsKey((string)lootPrefab))
				{
					var loot = GameManager.server.FindPrefab((string)lootPrefab)?.GetComponent<LootContainer>();
					if (loot == null)
						continue;
					var container = new Dictionary<string, object>();
					container.Add("Enabled", ( (((string)lootPrefab).Contains("bradley_crate")) || (((string)lootPrefab).Contains("heli_crate")) ) ? false : true);
					container.Add("Scrap", loot.scrapAmount);
					int slots = 0;
					if (loot.LootSpawnSlots.Length > 0)
					{
						LootContainer.LootSpawnSlot[] lootSpawnSlots = loot.LootSpawnSlots;
						for (int i = 0; i < lootSpawnSlots.Length; i++)
							slots += lootSpawnSlots[i].numberToSpawn;
					}
					else
						slots = loot.maxDefinitionsToSpawn;
					container.Add("ItemsMin", slots);
					container.Add("ItemsMax", slots);
					container.Add("MaxBPs", 1);
					var itemList = new Dictionary<string, object>();
					if (loot.lootDefinition != null)
						GetLootSpawn(loot.lootDefinition, ref itemList);
					else if(loot.LootSpawnSlots.Length > 0)
					{
						LootContainer.LootSpawnSlot[] lootSpawnSlots = loot.LootSpawnSlots;
						for (int i = 0; i < lootSpawnSlots.Length; i++)
						{
							LootContainer.LootSpawnSlot lootSpawnSlot = lootSpawnSlots[i];
							GetLootSpawn(lootSpawnSlot.definition, ref itemList);
						}
					}
					container.Add("ItemList", itemList);
					lootTables.Add((string)lootPrefab, container );
					wasAdded = true;
				}

			}
			if (wasAdded)
			{
				lootTable.Set("LootTables", lootTables);
				lootTable.Save();
			}
			wasAdded = false;
			bool wasRemoved = false;
			int activeTypes = 0;
			foreach (var lootTable in lootTables.ToList())
			{
				var loot = GameManager.server.FindPrefab(lootTable.Key)?.GetComponent<LootContainer>();
				if (loot == null)
				{
					lootTables.Remove(lootTable.Key);
					wasRemoved = true;
					continue;
				}
				var container = lootTable.Value as Dictionary<string, object>;
				if (!container.ContainsKey("Enabled"))
				{
					container.Add("Enabled", true);
					wasAdded = true;
				}
				if ((bool)container["Enabled"])
					activeTypes++;
				if (!container.ContainsKey("Scrap"))
				{
					container.Add("Scrap", loot.scrapAmount);
					wasAdded = true;
				}

				int slots = 0;
				if (loot.LootSpawnSlots.Length > 0)
				{
					LootContainer.LootSpawnSlot[] lootSpawnSlots = loot.LootSpawnSlots;
					for (int i = 0; i < lootSpawnSlots.Length; i++)
						slots += lootSpawnSlots[i].numberToSpawn;
				}
				else
					slots = loot.maxDefinitionsToSpawn;
				if (!container.ContainsKey("MaxBPs"))
				{
					container.Add("MaxBPs", 1);
					wasAdded = true;
				}
				if (!container.ContainsKey("ItemsMin"))
				{
					container.Add("ItemsMin", slots);
					wasAdded = true;
				}
				if (!container.ContainsKey("ItemsMax"))
				{
					container.Add("ItemsMax", slots);
					wasAdded = true;
				}
				if (!container.ContainsKey("ItemsMax"))
				{
					container.Add("ItemsMax", slots);
					wasAdded = true;
				}
				if (!container.ContainsKey("ItemList"))
				{
					var itemList = new Dictionary<string, object>();
					if (loot.lootDefinition != null)
						GetLootSpawn(loot.lootDefinition, ref itemList);
					else if(loot.LootSpawnSlots.Length > 0)
					{
						LootContainer.LootSpawnSlot[] lootSpawnSlots = loot.LootSpawnSlots;
						for (int i = 0; i < lootSpawnSlots.Length; i++)
						{
							LootContainer.LootSpawnSlot lootSpawnSlot = lootSpawnSlots[i];
							GetLootSpawn(lootSpawnSlot.definition, ref itemList);
						}
					}
					container.Add("ItemList", itemList);
					wasAdded = true;
				}
				Items.Add(lootTable.Key, new List<string>[5]);
				Blueprints.Add(lootTable.Key, new List<string>[5]);
				for (var i = 0; i < 5; ++i)
				{
                    Items[lootTable.Key][i] = new List<string>();
					Blueprints[lootTable.Key][i] = new List<string>();
				}
				foreach (var itemEntry in container["ItemList"] as Dictionary<string, object>)
				{
					bool isBP = itemEntry.Key.EndsWith(".blueprint") ? true : false;
					var def = ItemManager.FindItemDefinition(itemEntry.Key.Replace(".blueprint", ""));

					if (def != null)
					{
						if (isBP && def.Blueprint != null && def.Blueprint.isResearchable)
						{
							int index = (int)def.rarity;
								if (!Blueprints[lootTable.Key][index].Contains(def.shortname))
									Blueprints[lootTable.Key][index].Add(def.shortname);
						}
						else
						{
							int index = 0;
							object indexoverride;
							if (rarityItemOverride.TryGetValue(def.shortname, out indexoverride))
								index = Convert.ToInt32(indexoverride);
							else
								index = (int)def.rarity;
							if (!Items[lootTable.Key][index].Contains(def.shortname))
								Items[lootTable.Key][index].Add(def.shortname);
						}
					}
				}
				totalItemWeight.Add(lootTable.Key, 0);
                totalBlueprintWeight.Add(lootTable.Key, 0);
				itemWeights.Add(lootTable.Key, new int[5]);
				blueprintWeights.Add(lootTable.Key, new int[5]);
				for (var i = 0; i < 5; ++i)
				{
                    totalItemWeight[lootTable.Key] += (itemWeights[lootTable.Key][i] = ItemWeight(baseItemRarity, i) * Items[lootTable.Key][i].Count);
					totalBlueprintWeight[lootTable.Key] += (blueprintWeights[lootTable.Key][i] = ItemWeight(baseItemRarity, i) * Blueprints[lootTable.Key][i].Count);
				}

			}
			if (wasAdded || wasRemoved)
			{
				lootTable.Set("LootTables", lootTables);
				lootTable.Save();
			}
			lootTable.Clear();
			Puts($"Using '{activeTypes}' active of '{lootTables.Count}' supported containertypes");
        }

		int ItemWeight(double baseRarity, int index) { return (int)(Math.Pow(baseRarity, 4 - index) * 1000); }

		void GetLootSpawn(LootSpawn lootSpawn, ref Dictionary<string, object> items)
        {
			if (lootSpawn.subSpawn != null && lootSpawn.subSpawn.Length > 0)
            {
				foreach (var entry in lootSpawn.subSpawn)
                    GetLootSpawn(entry.category, ref items);
                return;
            }
			if (lootSpawn.items != null && lootSpawn.items.Length > 0)
            {
				foreach (var amount in lootSpawn.items)
                {
					object options = GetAmounts(amount, 1);
					string itemName = amount.itemDef.shortname;
					if (amount.itemDef.spawnAsBlueprint)
						itemName += ".blueprint";
					if (!items.ContainsKey(itemName))
						items.Add(itemName, options);
                }
            }
        }

		object GetAmounts(ItemAmount amount, int mul = 1)
		{
			if (amount.itemDef.isWearable || (amount.itemDef.condition.enabled && amount.itemDef.GetComponent<ItemModDeployable>() == null))
				mul = 1;
			object options = new Dictionary<string, object>
			{
				["Min"] = (int)amount.amount * mul,
				["Max"] = (amount as ItemAmountRanged).maxAmount > 0f && (amount as ItemAmountRanged).maxAmount > amount.amount ? (int)(amount as ItemAmountRanged).maxAmount * mul : (int)amount.amount * mul
			};
			return options;
		}

		static Dictionary<string,object> defaultItemOverride()
		{
			var dp = new Dictionary<string, object>();
			dp.Add("autoturret", 4);
			dp.Add("lmg.m249", 4);
			dp.Add("targeting.computer", 3);
			return dp;
		}

		double baseItemRarity;
		double blueprintProbability;
		bool removeStackedContainers;
		bool listUpdatesOnLoaded;
		Dictionary<string,object> rarityItemOverride = null;
		List<object> lootPrefabsToUse = null;

		object GetConfig(string menu, string datavalue, object defaultValue)
		{
			var data = Config[menu] as Dictionary<string, object>;
			if (data == null)
			{
				data = new Dictionary<string, object>();
				Config[menu] = data;
				Changed = true;
			}
			object value;
			if (!data.TryGetValue(datavalue, out value))
			{
				value = defaultValue;
				data[datavalue] = value;
				Changed = true;
			}
			return value;
		}

		void LoadVariables()
		{
			baseItemRarity = 2;
			rarityItemOverride = (Dictionary<string, object>)GetConfig("Rarity", "Override", defaultItemOverride());
			lootPrefabsToUse = (List<object>)GetConfig("Generic", "WatchedPrefabs", lootPrefabDefaults());
			listUpdatesOnLoaded = Convert.ToBoolean(GetConfig("Generic", "listUpdatesOnLoaded", true));
			removeStackedContainers = Convert.ToBoolean(GetConfig("Generic", "removeStackedContainers", true));
			blueprintProbability = Convert.ToDouble(GetConfig("Generic", "blueprintProbability", 0.11));

			if (!Changed) return;
			SaveConfig();
			Changed = false;
		}

		protected override void LoadDefaultConfig()
		{
			Config.Clear();
			LoadVariables();
		}

		void Init()
		{
			LoadVariables();
		}

		void OnServerInitialized()
		{
			ItemManager.Initialize();
			LoadAllContainers();
			UpdateInternals(listUpdatesOnLoaded);
		}

		void Unload() {}

		void UpdateInternals(bool doLog)
		{
			SaveExportNames();
			if (Changed)
			{
				SaveConfig();
				Changed = false;
			}
			Puts("Updating internals ...");
			populatedContainers = 0;
			NextTick( () =>
			{
				if (removeStackedContainers)
					FixLoot();
				foreach (var container in BaseNetworkable.serverEntities.Where(p => p != null &&  p.GetComponent<BaseEntity>() != null && p  is LootContainer).Cast<LootContainer>().ToList())
				{
					if (container == null)
						continue;
					if (CustomLootSpawns && (bool)CustomLootSpawns?.Call("IsLootBox", container.GetComponent<BaseEntity>()))
						continue;
					if (PopulateContainer(container))
						populatedContainers++;
				}


				Puts($"Populated '{populatedContainers}' supported containers.");
				initialized = true;
				populatedContainers = 0;
				ItemManager.DoRemoves();
			});
		}


		void FixLoot()
		{
			var spawns = Resources.FindObjectsOfTypeAll<LootContainer>()
				.Where(c => c.isActiveAndEnabled).
				OrderBy(c => c.transform.position.x).ThenBy(c => c.transform.position.z).ThenBy(c => c.transform.position.z)
				.ToList();

			var count = spawns.Count();
			var racelimit = count * count;

			var antirace = 0;
			var deleted = 0;

			for (var i = 0; i < count; i++)
			{
				var box = spawns[i];
				var pos = new Vector2(box.transform.position.x, box.transform.position.z);

				if (++antirace > racelimit)
				{
					return;
				}

				var next = i + 1;
				while (next < count)
				{
					var box2 = spawns[next];
					var pos2 = new Vector2(box2.transform.position.x, box2.transform.position.z);
					var distance = Vector2.Distance(pos, pos2);

					if (++antirace > racelimit)
					{
						return;
					}

					if (distance < 0.25f)
					{
						spawns.RemoveAt(next);
						count--;
						(box2 as BaseEntity).KillMessage();
						deleted++;
					}
					else break;
				}
			}

			if (deleted > 0)
				Puts($"Removed {deleted} stacked LootContainer");
			else
				Puts($"No stacked LootContainer found.");
			ItemManager.DoRemoves();
		}

		bool PopulateContainer(LootContainer container)
		{
			Dictionary<string, object> con;
            object containerobj;
            int lootitemcount;
			if (!lootTables.TryGetValue(container.PrefabName, out containerobj))
				return false;
			con = containerobj as Dictionary<string, object>;
			if (!(bool)con["Enabled"])
				return false;
			lootitemcount = (int)(con["ItemList"] as Dictionary<string, object>).Count();
			int itemCount = Mathf.RoundToInt(UnityEngine.Random.Range(Convert.ToSingle(Mathf.Min((int)con["ItemsMin"],(int)con["ItemsMax"]))*100f, Convert.ToSingle(Mathf.Max((int)con["ItemsMin"],(int)con["ItemsMax"]))*100f) / 100f);
			if (lootitemcount > 0 && itemCount > lootitemcount && lootitemcount < 36)
				itemCount = lootitemcount;
			if (container.inventory == null)
			{
				container.inventory = new ItemContainer();
				container.inventory.ServerInitialize(null, 36);
				container.inventory.GiveUID();
			}
			else
			{
				while (container.inventory.itemList.Count > 0)
				{
					var item = container.inventory.itemList[0];
					item.RemoveFromContainer();
					item.Remove(0f);
				}
				container.inventory.capacity = 36;
			}
			var items = new List<Item>();
            var itemNames = new List<string>();
            var itemBlueprints = new List<int>();
			var maxRetry = 10;
			for (int i = 0; i < itemCount; ++i)
            {
                if (maxRetry == 0)
				{
					break;
				}
                var item = MightyRNG(container.PrefabName, itemCount, (bool)(itemBlueprints.Count >= (int)con["MaxBPs"]));

				if (item == null)
				{
					--maxRetry;
					--i;
					continue;
				}
                if (itemNames.Contains(item.info.shortname) || (item.IsBlueprint() && itemBlueprints.Contains(item.blueprintTarget)))
                {
                    item.Remove(0f);
                    --maxRetry;
                    --i;
                    continue;
                }
                else
                    if (item.IsBlueprint())
						itemBlueprints.Add(item.blueprintTarget);
					else
						itemNames.Add(item.info.shortname);
                items.Add(item);
            }
            foreach (var item in items.Where(x => x != null && x.IsValid()))
				item.MoveToContainer(container.inventory, -1, false);
			if ((int)con["Scrap"] > 0)
			{
				int scrapCount = (int)con["Scrap"];
				Item item = ItemManager.Create(ItemManager.FindItemDefinition("scrap"), scrapCount, 0uL);
				item.MoveToContainer(container.inventory, -1, false);
			}
			container.inventory.capacity = container.inventory.itemList.Count;
			container.inventory.MarkDirty();
			container.SendNetworkUpdate();
			populatedContainers++;
			return true;
		}

		Item MightyRNG(string type, int itemCount, bool blockBPs = false)
        {
			bool asBP = (rng.NextDouble() < blueprintProbability && !blockBPs);
			List<string> selectFrom;
			int limit = 0;
            string itemName;
            Item item;
            int maxRetry = 10 * itemCount;
			do
			{
                selectFrom = null;
				item = null;
				if (asBP)
				{
					var r = rng.Next(totalBlueprintWeight[type]);
					for (var i = 0; i < 5; ++i)
					{
						limit += blueprintWeights[type][i];
						if (r < limit)
						{
							selectFrom = Blueprints[type][i];
							break;
						}
					}
				}
				else
				{
					var r = rng.Next(totalItemWeight[type]);
					for (var i = 0; i < 5; ++i)
					{
						limit += itemWeights[type][i];
						if (r < limit)
						{
							selectFrom = Items[type][i];
							break;
						}
					}
				}
                if (selectFrom == null)
                {
                    if (--maxRetry <= 0)
						break;
                    continue;
                }
				itemName = selectFrom[rng.Next(0, selectFrom.Count)];
				ItemDefinition itemDef =  ItemManager.FindItemDefinition(itemName);
				if (asBP && itemDef.Blueprint != null && itemDef.Blueprint.isResearchable)
				{
					var blueprintBaseDef = ItemManager.FindItemDefinition("blueprintbase");
					item = ItemManager.Create(blueprintBaseDef, 1, 0uL);
					item.blueprintTarget = itemDef.itemid;
				}
				else
					item = ItemManager.CreateByName(itemName, 1);
				if (item == null || item.info == null)
					continue;
                break;
            } while (true);
			if (item == null)
				return null;
			object itemLimit;
            if ( ((lootTables[type] as Dictionary<string, object>)["ItemList"] as Dictionary<string, object>).TryGetValue(item.info.shortname, out itemLimit))
			{
				Dictionary<string, object> limits = itemLimit as Dictionary<string, object>;
				item.amount = UnityEngine.Random.Range(Math.Min((int)limits["Min"],(int)limits["Max"]), Math.Max((int)limits["Min"],(int)limits["Max"]));
            }
			item.OnVirginSpawn();
			return item;
        }

		object OnLootSpawn(LootContainer container)
		{
			if (!initialized || container == null)
				return null;
			if (CustomLootSpawns && (bool)CustomLootSpawns?.Call("IsLootBox", container.GetComponent<BaseEntity>()))
				return null;
			if (PopulateContainer(container))
			{
				ItemManager.DoRemoves();
				return true;
			}
			return null;
		}

		static int RarityIndex(Rarity rarity)
		{
			switch (rarity) {
				case Rarity.None: return 0;
				case Rarity.Common: return 1;
				case Rarity.Uncommon: return 2;
				case Rarity.Rare: return 3;
				case Rarity.VeryRare: return 4;
			}
			return -1;
		}

		bool ItemExists(string name)
		{
			foreach (var def in ItemManager.itemList) {
				if (def.shortname != name)
					continue;
				var testItem = ItemManager.CreateByName(name, 1);
				if (testItem != null) {
					testItem.Remove(0f);
					return true;
				}
			}
			return false;
		}

		bool isSupplyDropActive()
		{
			Dictionary<string, object> con;
            object containerobj;
			if (!lootTables.TryGetValue("assets/prefabs/misc/supply drop/supply_drop.prefab", out containerobj))
				return false;
			con = containerobj as Dictionary<string, object>;
			if ((bool)con["Enabled"])
				return true;
			return false;
		}

		class StoredExportNames
		{
			public int version;
			public Dictionary<string, string> AllItemsAvailable = new Dictionary<string, string>();
			public StoredExportNames()
			{
			}
		}

		void SaveExportNames()
		{
			storedExportNames = Interface.GetMod().DataFileSystem.ReadObject<StoredExportNames>("BetterLoot\\NamesList");
			if (storedExportNames.AllItemsAvailable.Count == 0 || (int)storedExportNames.version != Rust.Protocol.network)
			{
				storedExportNames = new StoredExportNames();
				var exportItems = new List<ItemDefinition>(ItemManager.itemList);
				storedExportNames.version = Rust.Protocol.network;
				foreach(var it in exportItems)
					storedExportNames.AllItemsAvailable.Add(it.shortname, it.displayName.english);
				Interface.GetMod().DataFileSystem.WriteObject("BetterLoot\\NamesList", storedExportNames);
				Puts($"Exported {storedExportNames.AllItemsAvailable.Count} items to 'NamesList'");
			}
		}
	}
}
