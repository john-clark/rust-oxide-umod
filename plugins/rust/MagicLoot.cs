using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Rust;

namespace Oxide.Plugins
{
    [Info("MagicLoot", "Norn / Kunazai", "0.1.24", ResourceId = 2212)]
    [Description("Basic loot multiplier.")]
    class MagicLoot : RustPlugin
    {
        int VANILLA_MULTIPLIER = 1;
        int MAX_LOOT_CONTAINER_SLOTS = 18;
        bool INIT = false;
		bool LoadedDefaultConfig = false;
        Configuration Exclude = new Configuration();
        Configuration ExcludeFromMultiplication = new Configuration();
        MultiplierConfiguration Components = new MultiplierConfiguration();
		CrateEnabledConfiguration Crates = new CrateEnabledConfiguration();
        MLData LootData = new MLData();

        class MLData
        {
            public Dictionary<string, int> ExtraLootList = new Dictionary<string, int>();
            public MLData()
            {
            }
        }
		
		public class CrateData
		{
			public bool enabled;
			public int minExtraItems;
			public int maxExtraItems;
			public double lootMultiplier;
			
			public CrateData()
			{
				enabled = true;
				minExtraItems = 0;
				maxExtraItems = 0;
				lootMultiplier = 1.0;
			}
		}
		
        public class MultiplierConfiguration
        {
            public Dictionary<String, double> list;
            public MultiplierConfiguration() { list = new Dictionary<String, double>(); }
        }
		
		public class CrateEnabledConfiguration
        {
            public Dictionary<String, CrateData> list;
            public CrateEnabledConfiguration() { list = new Dictionary<String, CrateData>(); }
        }
		
		public class Configuration
        {
            public List<String> list;
            public Configuration() { list = new List<String>(); }
        }

        void SaveMagicLootData() { Interface.Oxide.DataFileSystem.WriteObject(this.Title, LootData); }
        void LoadMagicLootData()
        {
            int newitems = 0;
            LootData = Interface.Oxide.DataFileSystem.ReadObject<MLData>(this.Title);
            if (LootData.ExtraLootList.Count == 0) { Puts("Generating item list with limits..."); { foreach (var item in ItemManager.itemList) { LootData.ExtraLootList.Add(item.shortname, item.stackable); } SaveMagicLootData(); } }
            foreach (var item in ItemManager.itemList) { if (!LootData.ExtraLootList.ContainsKey(item.shortname)) { LootData.ExtraLootList.Add(item.shortname, item.stackable); newitems++; } }
            if (newitems != 0) { Puts("Added " + newitems.ToString() + " new items to /data/" + this.Title + ".json"); SaveMagicLootData(); }
			
			// RCon Logging
            Puts("Loaded " + LootData.ExtraLootList.Count + " item limits from /data/" + this.Title + ".json");
            Puts("Loaded " + Components.list.Count + " components from /config/" + this.Title + ".json");
        }
        void OnServerInitialized()
        {
            INIT = true; // Server has fully loaded.
			
			VerifyConfig();
			
			if (LoadedDefaultConfig)
			{
				foreach (ItemDefinition q in ItemManager.itemList.Where(p => p.category == ItemCategory.Component)) 
				{
					Components.list.Add(q.shortname, 1.0); 
				}
				Components.list.Add("antiradpills", 1.0);
				Components.list.Add("wood", 1.0);
				Components.list.Add("apple", 1.0);
				Components.list.Add("chocholate", 1.0);
				Components.list.Add("granolabar", 1.0);
				Components.list.Add("can.beans", 1.0);
				Components.list.Add("can.tuna", 1.0);
				Components.list.Add("metal.fragments", 1.0);
				Components.list.Add("lowgradefuel", 1.0);
				Components.list.Add("largemedkit", 1.0);
				Components.list.Add("syringe.medical", 1.0);
				Components.list.Add("black.raspberries", 1.0);
				Components.list.Add("blueberries", 1.0);
				Components.list.Add("bandage", 1.0);
				Components.list.Add("metal.refined", 1.0);
				Components.list.Add("scrap", 1.0);
				Config["ItemList"] = Components;
				
				foreach (var container in UnityEngine.Resources.FindObjectsOfTypeAll<LootContainer>().Where(c => c.enabled).Cast<BaseEntity>().GroupBy(c => c.ShortPrefabName).OrderBy(c => c.Key))
				{
					Crates.list.Add(container.Key.ToString(), new CrateData());
				}
				Config["LootContainersEnabled"] = Crates;
				
				SaveConfig();
				LoadedDefaultConfig = false;				
			}
			
            //if (Config["Loot", "RefreshMinutes"] == null) { Config["Loot", "RefreshMinutes"] = 25; SaveConfig(); }
            //if(Config["Settings", "RefreshMessage"] == null) { Config["Settings", "RefreshMessage"] = true;  SaveConfig(); }
            try { Exclude = JsonConvert.DeserializeObject<Configuration>(JsonConvert.SerializeObject(Config["Exclude"]).ToString()); } catch { Puts("ERROR: Could not load Exclude list!"); }
            try { Components = JsonConvert.DeserializeObject<MultiplierConfiguration>(JsonConvert.SerializeObject(Config["ItemList"]).ToString()); } catch { Puts("ERROR: Could not load Item list!"); }
			try { Crates = JsonConvert.DeserializeObject<CrateEnabledConfiguration>(JsonConvert.SerializeObject(Config["LootContainersEnabled"]).ToString()); } catch { Puts("ERROR: Could not load LootContainer list!"); }
			try { ExcludeFromMultiplication = JsonConvert.DeserializeObject<Configuration>(JsonConvert.SerializeObject(Config["ExcludeFromMultiplication"]).ToString()); } catch { }
			
            LoadMagicLootData();
			
			// RCon Logging
            Puts("Loaded at x" + Config["_Settings", "Multiplier"].ToString() + " vanilla rate | components rate x" + Config["_Settings", "ItemListMultiplier"].ToString() + "  [Extra Loot: " + Config["ExtraLoot", "Enabled"].ToString() + " | X Only Components: " + Config["_Settings", "MultiplyOnlyItemList"].ToString() + "]");
			
            RefreshLootContainers();
        }
        private readonly Dictionary<string, List<ulong>> skinsCache = new Dictionary<string, List<ulong>>();
        private List<ulong> GetSkins(ItemDefinition def)
        {
            List<ulong> skins;
            if (skinsCache.TryGetValue(def.shortname, out skins)) return skins;
            skins = new List<ulong> { 0 };
            skins.AddRange(ItemSkinDirectory.ForItem(def).Select(skin => (ulong)skin.id));
            skins.AddRange(Rust.Workshop.Approved.All.Where(skin => skin.Skinnable.ItemName == def.shortname).Select(skin => skin.WorkshopdId));
            skinsCache.Add(def.shortname, skins);
            return skins;
        }
        //List<ItemDefinition> ItemList = new List<ItemDefinition>();
        Dictionary<Rarity, List<ItemDefinition>> RarityList = new Dictionary<Rarity, List<ItemDefinition>>();
        void GenerateRarityList()
        {
            RarityList.Add(Rarity.Common, new List<ItemDefinition>(ItemManager.itemList.Where(z => z.rarity == Rarity.Common).Select(z => z)));
            Puts("Added " + RarityList[Rarity.Common].Count.ToString() + " items to Common list.");
            RarityList.Add(Rarity.Rare, new List<ItemDefinition>(ItemManager.itemList.Where(z => z.rarity == Rarity.Rare).Select(z => z)));
            Puts("Added " + RarityList[Rarity.Rare].Count.ToString() + " items to Rare list.");
            RarityList.Add(Rarity.Uncommon, new List<ItemDefinition>(ItemManager.itemList.Where(z => z.rarity == Rarity.Uncommon).Select(z => z)));
            Puts("Added " + RarityList[Rarity.Uncommon].Count.ToString() + " items to Uncommon list.");
            RarityList.Add(Rarity.VeryRare, new List<ItemDefinition>(ItemManager.itemList.Where(z => z.rarity == Rarity.VeryRare).Select(z => z)));
            Puts("Added " + RarityList[Rarity.VeryRare].Count.ToString() + " items to Very Rare list.");

            int itemsremoved = 0;
            foreach(var ra in RarityList[Rarity.Common].ToList()) { int limit = 0; if (LootData.ExtraLootList.TryGetValue(ra.shortname, out limit)) { if (limit == 0) { RarityList[Rarity.Common].Remove(ra); itemsremoved++; } } }
            foreach(var ra in RarityList[Rarity.Rare].ToList()) { int limit = 0; if (LootData.ExtraLootList.TryGetValue(ra.shortname, out limit)) { if (limit == 0) { RarityList[Rarity.Rare].Remove(ra); itemsremoved++; } } }
            foreach (var ra in RarityList[Rarity.Uncommon].ToList()) { int limit = 0; if (LootData.ExtraLootList.TryGetValue(ra.shortname, out limit)) { if (limit == 0) { RarityList[Rarity.Uncommon].Remove(ra); itemsremoved++; } } }
            foreach (var ra in RarityList[Rarity.VeryRare].ToList()) { int limit = 0; if (LootData.ExtraLootList.TryGetValue(ra.shortname, out limit)) { if (limit == 0) { RarityList[Rarity.VeryRare].Remove(ra); itemsremoved++; } } }
            if(itemsremoved != 0) { Puts("Removed " + itemsremoved.ToString() + " items from loot table. [ LIMIT = 0 ]"); }

        }
        protected override void LoadDefaultConfig()
        {
            // -- [ RESET ] ---
            Config.Clear();
            Puts("No configuration file found, generating...");
            // -- [ SETTINGS ] ---
            Config["_Settings", "Multiplier"] = VANILLA_MULTIPLIER;
            Config["_Settings", "ItemListMultiplier"] = VANILLA_MULTIPLIER;

            Config["_Settings", "MultiplyOnlyItemList"] = false;
			Config["_Settings", "RandomWorkshopSkins"] = true;
			Config["_Settings", "ForceRefreshDisabledCratesOnLoad"] = false;
			Config["_Settings", "ReportMissingConfigCrates"] = true;
			Config["_Settings", "DisableBlueprintDropMultiplier"] = true;
			Config["_Settings", "DisableBlueprintDrops"] = false;

            Config["ExtraLoot", "Enabled"] = false;
			Config["ExtraLoot", "VanillaLootTablesOnly"] = true;
            Config["ExtraLoot", "ExtraItemsMin"] = 1;
            Config["ExtraLoot", "ExtraItemsMax"] = 3;
            Config["ExtraLoot", "ItemStackSizeMin"] = 1;
            Config["ExtraLoot", "PreventDuplicates"] = false;
            

            Config["DeveloperDebug", "_EnableLogs"] = false;
            Config["DeveloperDebug", "Skins"] = false;
            Config["DeveloperDebug", "AmountChange"] = false;
            Config["DeveloperDebug", "ExtraItem"] = false;
			Config["DeveloperDebug", "LootRefresh"] = false;


            //Exclude.list.Add("supply.signal");
            Exclude.list.Add("ammo.rocket.smoke");
            Config["Exclude"] = Exclude;

            ExcludeFromMultiplication.list.Add("crude.oil");
            Config["ExcludeFromMultiplication"] = ExcludeFromMultiplication;

			if (INIT)
			{
				foreach (ItemDefinition q in ItemManager.itemList.Where(p => p.category == ItemCategory.Component)) 
				{
					Components.list.Add(q.shortname, 1.0); 
				}
				Components.list.Add("antiradpills", 1.0);
				Components.list.Add("wood", 1.0);
				Components.list.Add("apple", 1.0);
				Components.list.Add("chocholate", 1.0);
				Components.list.Add("granolabar", 1.0);
				Components.list.Add("can.beans", 1.0);
				Components.list.Add("can.tuna", 1.0);
				Components.list.Add("metal.fragments", 1.0);
				Components.list.Add("lowgradefuel", 1.0);
				Components.list.Add("largemedkit", 1.0);
				Components.list.Add("syringe.medical", 1.0);
				Components.list.Add("black.raspberries", 1.0);
				Components.list.Add("blueberries", 1.0);
				Components.list.Add("bandage", 1.0);
				Components.list.Add("metal.refined", 1.0);
				Components.list.Add("scrap", 1.0);
			}
			else
				LoadedDefaultConfig = true;
			
            
			
			
            Puts("Added " + Components.list.Count.ToString() + " components to configuration file.");
            Config["ItemList"] = Components;
			
			//foreach (var container in UnityEngine.Resources.FindObjectsOfTypeAll<LootContainer>().Where(c => c.isActiveAndEnabled).Cast<BaseEntity>().GroupBy(c => c.ShortPrefabName))
			if (INIT)
			{
				foreach (var container in UnityEngine.Resources.FindObjectsOfTypeAll<LootContainer>().Where(c => c.enabled).Cast<BaseEntity>().GroupBy(c => c.ShortPrefabName).OrderBy(c => c.Key))
				{
					Crates.list.Add(container.Key.ToString(), new CrateData());
				}
			}
			else
				LoadedDefaultConfig = true;
			
			//if (!Crates.list.ContainsKey("crate_elite")) Crates.list.Add("crate_elite", true);
			//if (!Crates.list.ContainsKey("bradley_crate")) Crates.list.Add("bradley_crate", false);
			//if (!Crates.list.ContainsKey("heli_crate")) Crates.list.Add("heli_crate", false);
			//if (!Crates.list.ContainsKey("supply_drop")) Crates.list.Add("supply_drop", false);
			Config["LootContainersEnabled"] = Crates;
				
        }
		private void VerifyConfig()
        {
			bool doSave = false;
            // -- [ SETTINGS ] ---
            if (Config["_Settings", "Multiplier"] == null) { Config["_Settings", "Multiplier"] = VANILLA_MULTIPLIER; doSave = true; }
            if (Config["_Settings", "ItemListMultiplier"] == null) { Config["_Settings", "ItemListMultiplier"] = VANILLA_MULTIPLIER; doSave = true; }
            if (Config["_Settings", "MultiplyOnlyItemList"] == null) { Config["_Settings", "MultiplyOnlyItemList"] = false; doSave = true; }
			if (Config["_Settings", "RandomWorkshopSkins"] == null) { Config["_Settings", "RandomWorkshopSkins"] = true; doSave = true; }
			if (Config["_Settings", "ForceRefreshDisabledCratesOnLoad"] == null) { Config["_Settings", "ForceRefreshDisabledCratesOnLoad"] = false; doSave = true; }
			if (Config["_Settings", "ReportMissingConfigCrates"] == null) { Config["_Settings", "ReportMissingConfigCrates"] = true; doSave = true; }
			if (Config["_Settings", "DisableBlueprintDropMultiplier"] == null) { Config["_Settings", "DisableBlueprintDropMultiplier"] = true; doSave = true; }
			if (Config["_Settings", "DisableBlueprintDrops"] == null) { Config["_Settings", "DisableBlueprintDrops"] = false; doSave = true; }

            if (Config["ExtraLoot", "Enabled"] == null) { Config["ExtraLoot", "Enabled"] = false; doSave = true; }
			if (Config["ExtraLoot", "VanillaLootTablesOnly"] == null) { Config["ExtraLoot", "VanillaLootTablesOnly"] = true; doSave = true; }
            if (Config["ExtraLoot", "ExtraItemsMin"] == null) { Config["ExtraLoot", "ExtraItemsMin"] = 1; doSave = true; }
            if (Config["ExtraLoot", "ExtraItemsMax"] == null) { Config["ExtraLoot", "ExtraItemsMax"] = 3; doSave = true; }
            if (Config["ExtraLoot", "ItemStackSizeMin"] == null) { Config["ExtraLoot", "ItemStackSizeMin"] = 1; doSave = true; }
            if (Config["ExtraLoot", "PreventDuplicates"] == null) { Config["ExtraLoot", "PreventDuplicates"] = false; doSave = true; }

            if (Config["DeveloperDebug", "_EnableLogs"] == null) { Config["DeveloperDebug", "_EnableLogs"] = false; doSave = true; }
            if (Config["DeveloperDebug", "Skins"] == null) { Config["DeveloperDebug", "Skins"] = false; doSave = true; }
            if (Config["DeveloperDebug", "AmountChange"] == null) { Config["DeveloperDebug", "AmountChange"] = false; doSave = true; }
            if (Config["DeveloperDebug", "ExtraItem"] == null) { Config["DeveloperDebug", "ExtraItem"] = false; doSave = true; }
			if (Config["DeveloperDebug", "LootRefresh"] == null) { Config["DeveloperDebug", "LootRefresh"] = false; doSave = true; }

            if (Config["Exclude"] == null) { Config["Exclude"] = Exclude; doSave = true; }

            if (Config["ExcludeFromMultiplication"] == null) { Config["ExcludeFromMultiplication"] = ExcludeFromMultiplication; doSave = true; }
			
            if (Config["ItemList"] == null) { Config["ItemList"] = Components; doSave = true; }
			
			if (Config["LootContainersEnabled"] == null) { Config["LootContainersEnabled"] = Crates; doSave = true; }
			
			if (doSave)
				SaveConfig();
        }
        private IEnumerable<int> CalculateStacks(int amount, ItemDefinition item)
        {
            var results = Enumerable.Repeat(item.stackable, amount / item.stackable); if (amount % item.stackable > 0) { results = results.Concat(Enumerable.Repeat(amount % item.stackable, 1)); }
            return results;
        }
        private void RefreshLootContainers()
        {
            int count = 0;
            foreach (var container in UnityEngine.Object.FindObjectsOfType<LootContainer>())
            {
				if (IsLootContainerEnabled(container))
				{
					RepopulateContainer(container);
					count++;
				}
				/*
				else if (Convert.ToBoolean(Config["_Settings", "ForceRefreshDisabledCratesOnLoad"]))
				{
					container.SpawnLoot();
				}
				*/
            }
            Puts("Repopulated " + count.ToString() + " loot containers.");
        }
		private bool IsLootContainerEnabled(LootContainer container) 
		{
			CrateData data = null;
			if (!Crates.list.TryGetValue(container.ShortPrefabName.ToString(), out data) || data == null)
			{
				if (Convert.ToBoolean(Config["_Settings", "ReportMissingConfigCrates"]))
					Puts("Auto added " + container.ShortPrefabName.ToString() + " to config.");
				
				Crates.list.Add(container.ShortPrefabName.ToString(), new CrateData());
				Config["LootContainersEnabled"] = Crates;
				SaveConfig();
			}
				/*
				if (!enabled)
					Puts("" + container.ShortPrefabName.ToString() + " is not enabled; MagicLoot skipped");
				*/
			return (data != null && data.enabled != null && data.enabled);
		}
		
		
		private void SpawnExtraVanillaLoot(LootContainer container)
		{
			CrateData data = null;
			int min = Convert.ToInt16(Config["ExtraLoot", "ExtraItemsMin"]);
			int max = Convert.ToInt16(Config["ExtraLoot", "ExtraItemsMax"]);
			if (Crates.list.TryGetValue(container.ShortPrefabName.ToString(), out data) && data != null)
			{
				min += data.minExtraItems;
				max += data.maxExtraItems;
			}
			
			int itemsToGive = UnityEngine.Random.Range(min, max);
			int tempCapacity = container.inventory.capacity;
            container.inventory.capacity = MAX_LOOT_CONTAINER_SLOTS;
            container.inventorySlots = MAX_LOOT_CONTAINER_SLOTS;
			
			//Puts("subspawn length: " + container.lootDefinition.subSpawn.Length + " " + container + " give: " + itemsToGive + "  min/max: " + Convert.ToInt16(Config["ExtraLoot", "ExtraItemsMin"]) + "/" + Convert.ToInt16(Config["ExtraLoot", "ExtraItemsMax"]));
			
			//Puts("itemsToGive: " + itemsToGive + "  min/max: " + min + "/" + max + " - " + container);
			
			SpawnItem(container, itemsToGive);
			int fcapacity = Math.Max(tempCapacity, container.inventory.itemList.Count());
            container.inventory.capacity = fcapacity;
            container.inventorySlots = fcapacity;
		}
		
		private void SpawnItem(LootContainer container, int itemsToSpawn = 1)
		{
			if (container.lootDefinition != null)
			{
				for (int i = 0; i < itemsToSpawn; i++) 
				{
					container.lootDefinition.SpawnIntoContainer(container.inventory);
				}
			}
			
			if (container.SpawnType == LootContainer.spawnType.ROADSIDE || container.SpawnType == LootContainer.spawnType.TOWN)
			{
				foreach (Item item in container.inventory.itemList)
				{
					if (!item.hasCondition)
					{
						continue;
					}
					item.condition = UnityEngine.Random.Range(item.info.condition.foundCondition.fractionMin, item.info.condition.foundCondition.fractionMax) * item.info.condition.max;
				}
			}
		}
		
		
        void RepopulateContainer(LootContainer container)
        {
            if (container != null)
            {
                ClearContainer(container);
                container.PopulateLoot();
				if (Convert.ToBoolean(Config["ExtraLoot", "Enabled"]) && Convert.ToBoolean(Config["ExtraLoot", "VanillaLootTablesOnly"])) // Extra Loot Items
				{
					SpawnExtraVanillaLoot(container);
				}
				
				// Check for blueprint drops to remove
				if (Convert.ToBoolean(Config["_Settings", "DisableBlueprintDrops"]))
				{
					int count = 0;
					int itemsToReplace = 0;
					bool foundBlueprint = true;
					List<Item> ItemsToRemove = new List<Item>();
					
					while (count < 3 && foundBlueprint)
					{
						for (int i = 0; i < itemsToReplace; i++) 
						{
							SpawnItem(container);
						}
						// iterate through inventory to find blueprints and mark for removal.
						foreach (Item lootitem in container.inventory.itemList) { if (lootitem.IsBlueprint()) { ItemsToRemove.Add(lootitem); continue; } }
						
						if (ItemsToRemove.Count > 0)
						{
							itemsToReplace = ItemsToRemove.Count;
							foreach (Item k in ItemsToRemove)
							{
								//Puts("" + k + " was removed from " + container + " at " + container.GetEstimatedWorldPosition());
								container.inventory.itemList.Remove(k);
								k.RemoveFromContainer();
							}
							ItemsToRemove.Clear();
						}
						else 
						{
							foundBlueprint = false; 
						}
						count++;
					}
					
				}
                ModifyContainerContents(container);
            }
        }
        void ClearContainer(LootContainer container)
        {
            while (container.inventory.itemList.Count > 0)
            {
                var item = container.inventory.itemList[0];
                item.RemoveFromContainer();
                item.Remove(0f);
            }
        }
        
        void ModifyContainerContents(BaseNetworkable entity)
        {
            var e = entity as LootContainer; if (e?.inventory?.itemList == null) return;
            List<Rarity> RaritiesUsed = new List<Rarity>();
			List<Item> ItemsToRemove = new List<Item>();
			
            foreach (Item lootitem in e.inventory.itemList)
            {
                if (Exclude.list.Contains(lootitem.info.shortname)) { ItemsToRemove.Add(lootitem); continue; }
				if (lootitem.IsBlueprint() && Convert.ToBoolean(Config["_Settings", "DisableBlueprintDropMultiplier"])) { continue; }
                if (ExcludeFromMultiplication.list.Contains(lootitem.info.shortname)) { continue; }
                var skins = GetSkins(ItemManager.FindItemDefinition(lootitem.info.itemid));
                if (skins.Count > 1 && Convert.ToBoolean(Config["_Settings", "RandomWorkshopSkins"])) // If workshop skins enabled, randomise skin
                {
                    lootitem.skin = skins.GetRandom(); if (lootitem.GetHeldEntity() != null) { lootitem.GetHeldEntity().skinID = lootitem.skin; }
					
					// Debug msges
                    if (Convert.ToBoolean(Config["DeveloperDebug", "_EnableLogs"]) && Convert.ToBoolean(Config["DeveloperDebug", "Skins"]))
                    { string debugs = "[" + lootitem.info.displayName.english + "] Skin has been modified to: " + lootitem.skin; Puts(debugs); PrintToChat(debugs); }
                }
				double multiplier = 1.0;
                if (Components.list.TryGetValue(lootitem.info.shortname, out multiplier))
                {
					CrateData data = null;
					Crates.list.TryGetValue(e.ShortPrefabName.ToString(), out data);
					
                    if (Convert.ToInt16(Config["_Settings", "ItemListMultiplier"]) != VANILLA_MULTIPLIER || multiplier != VANILLA_MULTIPLIER || (data != null && data.lootMultiplier != VANILLA_MULTIPLIER) )
                    {
                        if (lootitem.info.stackable > 1) // Detect whether to change Amounts
                        {
							
							// Debug msges
                            if (Convert.ToBoolean(Config["DeveloperDebug", "_EnableLogs"]) && Convert.ToBoolean(Config["DeveloperDebug", "AmountChange"]))
                            { string debugs = "[<color=green>" + e.GetInstanceID().ToString() + "</color> | " + e.ShortPrefabName + "] <color=yellow>" + lootitem.info.displayName.english + " : original amount: " + lootitem.amount.ToString() + "</color>"; Puts(debugs); PrintToChat(debugs); }
						
							double crateMult = 1.0;
							if (data != null) crateMult = data.lootMultiplier;

                            int limit = 0;
                            int ac = (int) (lootitem.amount * Convert.ToUInt16(Config["_Settings", "ItemListMultiplier"]) * multiplier * crateMult);
                            if (LootData.ExtraLootList.TryGetValue(lootitem.info.shortname, out limit)) { lootitem.amount = Math.Min(ac, Math.Min(limit, lootitem.info.stackable)); } else { break; }

                            if (Convert.ToBoolean(Config["DeveloperDebug", "_EnableLogs"]) && Convert.ToBoolean(Config["DeveloperDebug", "AmountChange"]))
                            { string debugs = "[<color=green>" + e.GetInstanceID().ToString() + "</color> | " + e.ShortPrefabName + "] <color=white>" + lootitem.info.displayName.english + " : new amount: " + lootitem.amount.ToString() + "</color>"; Puts(debugs); PrintToChat(debugs); }
                        }
                    }
                }
                else
                {
					CrateData data = null;
					Crates.list.TryGetValue(e.ShortPrefabName.ToString(), out data);
					
                    if (Convert.ToInt16(Config["_Settings", "Multiplier"]) != VANILLA_MULTIPLIER || (data != null && data.lootMultiplier != VANILLA_MULTIPLIER))
                    {
                        if (lootitem.info.stackable > 1 && !Convert.ToBoolean(Config["_Settings", "MultiplyOnlyItemList"])) // Detect whether to change Amounts
                        {
							
							// Debug msges
                            if (Convert.ToBoolean(Config["DeveloperDebug", "_EnableLogs"]) && Convert.ToBoolean(Config["DeveloperDebug", "AmountChange"]))
                            { string debugs = "[<color=green>" + e.GetInstanceID().ToString() + "</color> | " + e.ShortPrefabName + "] <color=yellow>" + lootitem.info.displayName.english + " : original amount: " + lootitem.amount.ToString() + "</color>"; Puts(debugs); PrintToChat(debugs); }

							double crateMult = 1.0;
							if (data != null) crateMult = data.lootMultiplier;
						
                            int limit = 0;
                            int ac = (int) (lootitem.amount * Convert.ToUInt16(Config["_Settings", "Multiplier"]) * crateMult);
                            if (LootData.ExtraLootList.TryGetValue(lootitem.info.shortname, out limit)) { lootitem.amount = Math.Min(ac, Math.Min(limit, lootitem.info.stackable)); } else { break; }

							// Debug msges
                            if (Convert.ToBoolean(Config["DeveloperDebug", "_EnableLogs"]) && Convert.ToBoolean(Config["DeveloperDebug", "AmountChange"]))
                            { string debugs = "[<color=green>" + e.GetInstanceID().ToString() + "</color> | " + e.ShortPrefabName + "] <color=white>" + lootitem.info.displayName.english + " : new amount: " + lootitem.amount.ToString() + "</color>"; Puts(debugs); PrintToChat(debugs); }
                        }
                    }
                }
				if (lootitem.info.rarity != Rarity.None && !RaritiesUsed.Contains(lootitem.info.rarity)) { RaritiesUsed.Add(lootitem.info.rarity); }
            }
			
			if (ItemsToRemove.Count > 0)
			{
				foreach (Item k in ItemsToRemove)
				{
					k.RemoveFromContainer();  
					e.inventory.itemList.Remove(k);
				}
			}				
			
            if (Convert.ToBoolean(Config["ExtraLoot", "Enabled"]) && !Convert.ToBoolean(Config["ExtraLoot", "VanillaLootTablesOnly"])) // Extra Loot Items
            {
                if (RarityList.Count == 0) { GenerateRarityList(); }
				
				//Puts("RarityList.Count: " + RarityList.Count + "  RaritiesUsed.Count - " + RaritiesUsed.Count + " " + e.GetEstimatedWorldPosition());
                if (RaritiesUsed.Count >= 1 && RaritiesUsed != null)
                {
                    Rarity rarity = RaritiesUsed.GetRandom();
                    ItemDefinition item;
					CrateData data = null;
					int min = Convert.ToInt16(Config["ExtraLoot", "ExtraItemsMin"]);
					int max = Convert.ToInt16(Config["ExtraLoot", "ExtraItemsMax"]);
					if (Crates.list.TryGetValue(e.ShortPrefabName.ToString(), out data) && data != null)
					{
						min += data.minExtraItems;
						max += data.maxExtraItems;
					}
					
                    int itemstogive = UnityEngine.Random.Range(min, max);
					int tempCap = e.inventory.capacity;
                    e.inventory.capacity = MAX_LOOT_CONTAINER_SLOTS;
                    e.inventorySlots = MAX_LOOT_CONTAINER_SLOTS;
                    for (int i = 1; i <= itemstogive; i++)
                    {
                        item = RarityList[rarity].GetRandom();
                        if (e.inventory.FindItemsByItemID(item.itemid).Count >= 1 && item.stackable == 1 && Convert.ToBoolean(Config["ExtraLoot", "PreventDuplicates"]))
                        { break; }
                        if (item != null)
                        {
                            if (Exclude.list.Contains(item.shortname)) { break; }
                            int limit = 0; int amounttogive = 0;
                            if (LootData.ExtraLootList.TryGetValue(item.shortname, out limit) && item.stackable > 1) { amounttogive = UnityEngine.Random.Range(Convert.ToInt16(Config["ExtraLoot", "ItemStackSizeMin"]), Math.Min(limit, item.stackable)); } else { amounttogive = item.stackable; }
                            var skins = GetSkins(item);
                            if (skins.Count > 1 && Convert.ToBoolean(Config["_Settings", "RandomWorkshopSkins"]))
                            { Item skinned = ItemManager.CreateByItemID(item.itemid, amounttogive, skins.GetRandom()); skinned.MoveToContainer(e.inventory, -1, false); }
                            else
                            { e.inventory.AddItem(item, amounttogive); }
						
							// Debug msges
                            if (Convert.ToBoolean(Config["DeveloperDebug", "_EnableLogs"]) && Convert.ToBoolean(Config["DeveloperDebug", "ExtraItem"]))
                            { string debugs = "[<color=green>" + e.GetInstanceID().ToString() + "</color> | " + e.ShortPrefabName + "] <color=white>Extra Item: " + item.displayName.english + " : amount: " + amounttogive.ToString() + "</color>"; Puts(debugs); PrintToChat(debugs); }
                        }
                    }
					int fcapacity = Math.Max(tempCap, e.inventory.itemList.Count());
					e.inventory.capacity = fcapacity;
					e.inventorySlots = fcapacity;
                }
            }
        }
		
		
        object OnLootSpawn(LootContainer container)
        {
            if (INIT)
            {
                if (container?.inventory?.itemList == null) return null;
				
				if (!IsLootContainerEnabled(container)) return null;
				
                RepopulateContainer(container);
				
				// Debug msg
				if (Convert.ToBoolean(Config["DeveloperDebug", "_EnableLogs"]) && Convert.ToBoolean(Config["DeveloperDebug", "LootRefresh"]))
                            { string debugs = "Repopulated: " + container.ToString() + " : isLootable[" + container.isLootable + "]"; Puts(debugs); }
						 
                if (container.shouldRefreshContents && container.isLootable)
                {
					// Debug msg
					if (Convert.ToBoolean(Config["DeveloperDebug", "_EnableLogs"]) && Convert.ToBoolean(Config["DeveloperDebug", "LootRefresh"]))
                            { string debugs = "Invoked refresh on " + container.ToString(); Puts(debugs); }
						
                    container.Invoke(new Action(container.SpawnLoot), UnityEngine.Random.Range(container.minSecondsBetweenRefresh, container.maxSecondsBetweenRefresh));
                }
                return container;
            }
            return null;
        }
    }
}