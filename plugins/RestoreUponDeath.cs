using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("RestoreUponDeath", "k1lly0u", "0.2.0", ResourceId = 0)]
    class RestoreUponDeath : RustPlugin
    {
        #region Fields
        private StoredData storedData;
        private DynamicConfigFile data;

        private Dictionary<string, Dictionary<ContainerType, float>> permToContainer = new Dictionary<string, Dictionary<ContainerType, float>>();
        #endregion

        #region Oxide Hooks        
        private void OnServerInitialized()
        {
            data = Interface.Oxide.DataFileSystem.GetFile("restoreupondeath_data");
            LoadData();

            ConvertConfigPermissions();

            if (configData.DropActiveItem)
                Unsubscribe(nameof(CanDropActiveItem));

            foreach(string perm in configData.Permissions.Keys)
                permission.RegisterPermission(!perm.StartsWith("restoreupondeath.") ? $"restoreupondeath.{perm}" : perm, this);           
        }

        private void OnServerSave() => SaveData();

        private object CanDropActiveItem(BasePlayer player) => false;

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            LootableCorpse corpse = entity?.GetComponent<LootableCorpse>();
            if (corpse == null || corpse is NPCPlayerCorpse)
                return;
            
            TakePlayerItems(corpse);
        }                
       
        private void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null)
                return;

            if (player.IsSleeping() || player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.In(1, () => OnPlayerRespawned(player));
                return;
            }

            if (!configData.DefaultItems && storedData.HasStoredItems(player.userID))
                StripContainer(player.inventory.containerBelt);

            storedData.RestoreData(player);
        }
        #endregion

        #region Functions
        private T ParseType<T>(string type) => (T)Enum.Parse(typeof(T), type, true);

        private void ConvertConfigPermissions()
        {
            foreach(var perm in configData.Permissions)            
                permToContainer.Add(perm.Key, perm.Value.ToDictionary(x => ParseType<ContainerType>(x.Key), y => Mathf.Clamp(y.Value, 0, 100)));
        }

        private Dictionary<ContainerType, float> GetLossPercentage(ulong playerId)
        {
            foreach(var perm in permToContainer)
            {
                if (permission.UserHasPermission(playerId.ToString(), perm.Key))
                    return perm.Value;
            }
            return null;
        }

        private void TakePlayerItems(LootableCorpse corpse)
        {
            Dictionary<ContainerType, float> lossPercentage = GetLossPercentage(corpse.playerSteamID);
            if (lossPercentage == null || lossPercentage.Sum(x => x.Value) == 300)           
                return; 

            if (lossPercentage.Sum(x => x.Value) == 0)
            {
                storedData.AddData(corpse);

                foreach (ItemContainer container in corpse.containers)
                    StripContainer(container);
            }
            else
            {
                StoredData.InventoryData inventoryData = new StoredData.InventoryData();

                for (int i = 0; i < corpse.containers.Length; i++)
                {               
                    ItemContainer container = corpse.containers[i];
                    float percentage = Mathf.Clamp(lossPercentage[(ContainerType)i], 0, 100);

                    if (container.itemList.Count == 0)
                        return;

                    if (percentage == 0)
                    {
                        for (int y = container.itemList.Count - 1; y >= 0 ; y--)
                        {
                            Item item = container.itemList[y];
                            inventoryData.AddItem(item, (ContainerType)i);
                            item.RemoveFromContainer();
                            item.Remove();
                            container.itemList.Remove(item);
                        }
                    }
                    else
                    {
                        int amount = container.itemList.Count - Convert.ToInt32(((float)container.itemList.Count * (float)percentage) / 100);

                        for (int y = 0; y < amount; y++)
                        {
                            Item item = container.itemList.GetRandom();
                            inventoryData.AddItem(item, (ContainerType)i);
                            item.RemoveFromContainer();
                            item.Remove();
                            container.itemList.Remove(item);
                        }
                    }
                }
                storedData.AddData(corpse.playerSteamID, inventoryData);
            }
            ItemManager.DoRemoves();
        }
        
        private void StripContainer(ItemContainer container)
        {
            for (int i = container.itemList.Count - 1; i >= 0; i--)
            {
                Item item = container.itemList[i];
                item.RemoveFromContainer();
                item.Remove();
            }
        }
        #endregion

        #region Config        
        private ConfigData configData;
        private class ConfigData
        {
            [JsonProperty("Give default items upon respawn if the players is having items restored")]
            public bool DefaultItems { get; set; }

            [JsonProperty("Can drop active item on death")]
            public bool DropActiveItem { get; set; }

            [JsonProperty("Percentage of total items lost (Permission Name | Percentage)")]
            public Dictionary<string, Dictionary<string, float>> Permissions { get; set; }
           
            public VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                DefaultItems = false,
                DropActiveItem = false,                
                Permissions = new Dictionary<string, Dictionary<string, float>>
                {
                    ["restoreupondeath.default"] = new Dictionary<string, float>
                    {
                        ["Belt"] = 75f,
                        ["Wear"] = 75f,
                        ["Main"] = 75f
                    },
                    ["restoreupondeath.beltonly"] = new Dictionary<string, float>
                    {
                        ["Belt"] = 0f,
                        ["Wear"] = 100f,
                        ["Main"] = 100f
                    },
                    ["restoreupondeath.admin"] = new Dictionary<string, float>
                    {
                        ["Belt"] = 0f,
                        ["Wear"] = 0f,
                        ["Main"] = 0f
                    },
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new VersionNumber(0, 2, 0))
                configData = baseConfig;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

        #region Data Management
        public enum ContainerType { Main, Wear, Belt }

        private void SaveData() => data.WriteObject(storedData);

        private void LoadData()
        {
            try
            {
                storedData = data.ReadObject<StoredData>();
            }
            catch
            {
                storedData = new StoredData();
            }
        }

        private class StoredData
        {
            private Hash<ulong, InventoryData> storedInventory = new Hash<ulong, InventoryData>();

            public void AddData(LootableCorpse corpse) => storedInventory[corpse.playerSteamID] = new InventoryData(corpse);

            public void AddData(ulong playerId, InventoryData data) => storedInventory[playerId] = data;

            public void RestoreData(BasePlayer player)
            {
                if (player == null)
                    return;

                InventoryData data;
                if (!storedInventory.TryGetValue(player.userID, out data))
                    return;

                RestoreItems(player, data);
                storedInventory.Remove(player.userID);
            }

            public bool HasStoredItems(ulong playerId)
            {
                InventoryData data;
                if (!storedInventory.TryGetValue(playerId, out data) || data.items.Count == 0)
                    return false;
                return true;
            }
            
            private void RestoreItems(BasePlayer player, InventoryData inventoryData)
            {
                if (inventoryData == null || inventoryData.items.Count == 0)
                    return;

                for (int i = 0; i < inventoryData.items.Count; i++)
                {
                    ItemData data = inventoryData.items[i];

                    Item item = CreateItem(data);
                    ItemContainer container = data.container == ContainerType.Belt ? player.inventory.containerBelt : data.container == ContainerType.Wear ? player.inventory.containerWear : player.inventory.containerMain;

                    item.MoveToContainer(container, data.position, true);
                }
                return;
            }

            private Item CreateItem(ItemData itemData)
            {
                Item item = ItemManager.CreateByItemID(itemData.itemid, itemData.amount, itemData.skin);
                item.condition = itemData.condition;
                if (itemData.instanceData != null)
                    itemData.instanceData.Restore(item);

                item.blueprintTarget = itemData.blueprintTarget;

                BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                if (weapon != null)
                {
                    if (!string.IsNullOrEmpty(itemData.ammotype))
                        weapon.primaryMagazine.ammoType = ItemManager.FindItemDefinition(itemData.ammotype);
                    weapon.primaryMagazine.contents = itemData.ammo;
                }
                if (itemData.contents != null)
                {
                    foreach (var contentData in itemData.contents)
                    {
                        var newContent = ItemManager.CreateByItemID(contentData.itemid, contentData.amount);
                        if (newContent != null)
                        {
                            newContent.condition = contentData.condition;
                            newContent.MoveToContainer(item.contents);
                        }
                    }
                }
                return item;
            }

            public class InventoryData
            {
                public List<ItemData> items = new List<ItemData>();
                
                public InventoryData() { }

                public InventoryData(LootableCorpse corpse)
                {
                    for (int i = 0; i < corpse.containers.Length; i++)                    
                        items.AddRange(GetItems(corpse.containers[i], (ContainerType)i));                                                               
                }

                public void AddItem(Item item, ContainerType containerType)
                {                    
                    ItemData itemData = new ItemData
                    {
                        itemid = item.info.itemid,
                        amount = item.amount,
                        ammo = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.contents ?? 0,
                        ammotype = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.ammoType.shortname ?? null,
                        position = item.position,
                        skin = item.skin,
                        condition = item.condition,
                        instanceData = new ItemData.InstanceData(item),
                        blueprintTarget = item.blueprintTarget,
                        contents = item.contents?.itemList.Select(item1 => new ItemData
                        {
                            itemid = item1.info.itemid,
                            amount = item1.amount,
                            condition = item1.condition
                        }).ToArray(),
                        container = containerType
                    };

                    items.Add(itemData);
                }

                private IEnumerable<ItemData> GetItems(ItemContainer container, ContainerType containerType)
                {
                    return container.itemList.Select(item => new ItemData
                    {
                        itemid = item.info.itemid,
                        amount = item.amount,
                        ammo = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.contents ?? 0,
                        ammotype = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.ammoType.shortname ?? null,
                        position = item.position,
                        skin = item.skin,
                        condition = item.condition,
                        instanceData = new ItemData.InstanceData(item),
                        blueprintTarget = item.blueprintTarget,
                        contents = item.contents?.itemList.Select(item1 => new ItemData
                        {
                            itemid = item1.info.itemid,
                            amount = item1.amount,
                            condition = item1.condition
                        }).ToArray(),
                        container = containerType
                    });
                }
            }

            public class ItemData
            {
                public int itemid;
                public ulong skin;
                public int amount;
                public float condition;
                public int ammo;
                public string ammotype;
                public int position;
                public int blueprintTarget;
                public InstanceData instanceData;
                public ItemData[] contents;
                public ContainerType container;

                public class InstanceData
                {
                    public int dataInt;
                    public int blueprintTarget;
                    public int blueprintAmount;

                    public InstanceData() { }
                    public InstanceData(Item item)
                    {
                        if (item.instanceData == null)
                            return;

                        dataInt = item.instanceData.dataInt;
                        blueprintAmount = item.instanceData.blueprintAmount;
                        blueprintTarget = item.instanceData.blueprintTarget;
                    }

                    public void Restore(Item item)
                    {
                        item.instanceData = new ProtoBuf.Item.InstanceData();
                        item.instanceData.blueprintAmount = blueprintAmount;
                        item.instanceData.blueprintTarget = blueprintTarget;
                        item.instanceData.dataInt = dataInt;
                    }
                }
            }
        }
        #endregion
    }
}