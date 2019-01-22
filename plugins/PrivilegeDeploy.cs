using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{ 
    [Info("PrivilegeDeploy", "k1lly0u", "0.1.5")]
    [Description("Choose which deployable items require building privilege to deploy")]
    class PrivilegeDeploy : RustPlugin
    {
        private bool isInitialized = false;

        private Dictionary<string, string> prefabToItem = new Dictionary<string, string>();
        private Dictionary<string, List<ItemAmount>> constructionToIngredients = new Dictionary<string, List<ItemAmount>>();

        private Dictionary<ulong, PendingItem> pendingItems = new Dictionary<ulong, PendingItem>();

        private void OnServerInitialized()
        {
            LoadVariables();
            RegisterMessages();
            InitValidList();

            isInitialized = true;
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (isInitialized)
            {
                if (configData.deployables.Contains(entity.ShortPrefabName) || configData.deployables.Contains(entity.PrefabName))
                {
                    var ownerID = entity.GetComponent<BaseEntity>().OwnerID;
                    if (ownerID != 0)
                    {
                        BasePlayer player = BasePlayer.FindByID(ownerID);
                        if (player == null || player.IsAdmin || IsInPrivilege(player)) return;

                        List<ItemAmount> items = new List<ItemAmount>();
                        if (entity is BuildingBlock && constructionToIngredients.ContainsKey(entity.PrefabName))
                        {
                            foreach (var ingredient in constructionToIngredients[entity.PrefabName])
                                items.Add(ingredient);
                        }
                        else if (prefabToItem.ContainsKey(entity.PrefabName))
                            items.Add(new ItemAmount { amount = 1, startAmount = 1, itemDef = ItemManager.FindItemDefinition(prefabToItem[entity.PrefabName]) });

                        if (!pendingItems.ContainsKey(player.userID))
                            pendingItems.Add(player.userID, new PendingItem());
                        pendingItems[player.userID].items = items;

                        CheckForDuplicate(player);

                        StorageContainer container = entity.GetComponent<StorageContainer>();
                        if (container != null)                        
                            container.inventory.Clear();                        

                        if (entity is BaseTrap || !(entity is BaseCombatEntity))
                            entity.Kill();
                        else (entity as BaseCombatEntity).DieInstantly();
                    }
                }
            }
        }
      
        private void CheckForDuplicate(BasePlayer player)
        {
            if (pendingItems[player.userID].timer != null)
                pendingItems[player.userID].timer.Destroy();               
            pendingItems[player.userID].timer = timer.Once(0.01f, () => GivePlayerItem(player));
        }

        private void GivePlayerItem(BasePlayer player)
        {
            foreach(var itemAmount in pendingItems[player.userID].items)
            {
                Item item = ItemManager.Create(itemAmount.itemDef, (int)itemAmount.amount);
                var deployable = item.info.GetComponent<ItemModDeployable>();
                if (deployable != null)
                {
                    var oven = deployable.entityPrefab.Get()?.GetComponent<BaseOven>();
                    if (oven != null)
                        oven.startupContents = null;
                }
                player.GiveItem(item);
            }            
            SendReply(player, lang.GetMessage("blocked", this, player.UserIDString));
            pendingItems.Remove(player.userID);
        }

        private bool IsInPrivilege(BasePlayer player)
        {
            BuildingPrivlidge buildingPrivilege = player.GetBuildingPrivilege(player.WorldSpaceBounds());
            if (buildingPrivilege == null)            
                return false;            
            return buildingPrivilege.IsAuthed(player);
        }

        #region Prefab to Item links
        private void InitValidList()
        {
            foreach (var item in ItemManager.GetItemDefinitions())
            {
                var deployable = item?.GetComponent<ItemModDeployable>();
                if (deployable == null) continue;
                
                if (!prefabToItem.ContainsKey(deployable.entityPrefab.resourcePath))                
                    prefabToItem.Add(deployable.entityPrefab.resourcePath, item.shortname);                
            }
            foreach (var construction in GetAllPrefabs<Construction>())
            {
                if (construction.deployable == null && !string.IsNullOrEmpty(construction.info.name.english))
                {
                    if (!constructionToIngredients.ContainsKey(construction.fullName))                    
                        constructionToIngredients.Add(construction.fullName, construction.defaultGrade.costToBuild);
                }
            }
        }

        private T[] GetAllPrefabs<T>()
        {
            Dictionary<uint, PrefabAttribute.AttributeCollection> prefabs = PrefabAttribute.server.prefabs;
            if (prefabs == null)
                return new T[0];

            List<T> results = new List<T>();
            foreach (PrefabAttribute.AttributeCollection prefab in prefabs.Values)
            {
                T[] arrayCache = prefab.Find<T>();
                if (arrayCache == null || !arrayCache.Any())
                    continue;

                results.AddRange(arrayCache);
            }

            return results.ToArray();
        }
        #endregion

        #region Config
        private ConfigData configData;

        class ConfigData
        {
            public List<string> deployables { get; set; }
        }

        private void LoadVariables()
        {           
            LoadConfigVariables();
            SaveConfig();
        }
        private void RegisterMessages() => lang.RegisterMessages(messages, this);
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                deployables = new List<string>
                    {
                        "barricade.concrete",
                        "barricade.metal",
                        "barricade.sandbags",
                        "barricade.stone",
                        "barricade.wood",
                        "barricade.woodwire",
                        "campfire",
                        "gates.external.high.stone",
                        "gates.external.high.wood",
                        "wall.external.high",
                        "wall.external.high.stone",
                        "landmine",
                        "beartrap"
                    }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();

        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);

        class PendingItem
        {
            public Timer timer;
            public List<ItemAmount> items = new List<ItemAmount>();
        }
        #endregion

        Dictionary<string, string> messages = new Dictionary<string, string>
        {
            {"blocked", "You can not build this outside of a building privileged area!" }
        };
    }
}

