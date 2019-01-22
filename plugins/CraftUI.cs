using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CraftUI", "EinTime/Orange", "1.2.7", ResourceId = 2273)]
    [Description("A fully customizable custom crafting UI, which allows admins to change item ingredients.")]
    public class CraftUI : RustPlugin
    {
        const string ItemInfoListName = "Item List";
        const float refreshTime = 0.25f;

        [PluginReference] private Plugin ImageLibrary;

        public static CraftUI S;

        HashSet<int> customItemCrafts = new HashSet<int>();
        static readonly Dictionary<string, string> displaynameToShortname = new Dictionary<string, string>();
        const string transparentSprite = "assets/content/textures/generic/fulltransparent.tga";

        #region Player Responses
        const string Invalid = "Invalid";
        const string FormatHelp = "FormatHelp";
        const string InvalidFormat = "InvalidFormat";

        const string AlreadyBlocked = "AlreadyBlocked";

        const string UnknownError = "UnknownError";

        const string ItemRateChange = "ItemRateChange";
        const string ItemBlocked = "ItemBlocked";
        const string ItemNotBlocked = "ItemNotBlocked";
        const string AllItemsBlocked = "AllItemsBlocked";
        const string AllItemsUnblocked = "AllItemsUnblocked";

        const string IngredientsCleared = "IngredientsCleared";
        const string IngredientsReset = "IngredientsReset";
        const string AddedIngredient = "AddedIngredient";
        const string RemovedIngredient = "RemovedIngredient";
        const string RemovedAllIngredient = "RemovedAllIngredient";
        const string DoesNotContainIngredient = "DoesNotContainIngredient";
        const string HasNoIngredients = "HasNoIngredients";
        const string IngredientsList = "IngredientsList";

        const string ItemRenamed = "ItemRenamed";
        const string DescriptionChanged = "DescriptionChanged";
        const string IconUrlChanged = "IconURLChanged";
        const string CategoryChanged = "CategoryChanged";

        const string NotAdmin = "NotAdmin";

        const string FacepunchGUIDisabled = "FacepunchGUIDisabled";

        const string ItemListLoaded = "ItemListLoaded";
        const string ItemListSaved = "ItemListSaved";

        const string AddRemoveFormat = "AddRemoveFormat";
        const string IngredientsFormat = "IngredientsFormat";
        const string CraftrateFormat = "CraftrateFormat";
        const string RenameFormat = "RenameFormat";
        const string ChangeDescriptionFormat = "ChangeDescriptionFormat";
        const string IconFormat = "IconFormat";
        const string CategoryFormat = "CategoryFormat";


        static readonly Dictionary<string, string> responseDic = new Dictionary<string, string>()
        {
            [Invalid] = "{0} is an invalid {1}.",
            [FormatHelp] = "Format is: /{0} {1}.",
            [InvalidFormat] = "Invalid format for /{0}. Correct format is: /{0} {1}",

            [AlreadyBlocked] = "'{0}' blocked value is already {1}",

            [UnknownError] = "An unknown error has occured.",

            [ItemRateChange] = "Item '{0}' crafting rate is now '{1}'.",
            [ItemBlocked] = "Item '{0}' is now blocked from crafting.",
            [ItemNotBlocked] = "Item '{0}' is no longer blocked from crafting.",
            [AllItemsBlocked] = "All items are blocked from crafting.",
            [AllItemsUnblocked] = "All items are no longer blocked from crafting",

            [IngredientsCleared] = "Item '{0}' is now free to craft.",
            [IngredientsReset] = "Item '{0}' ingredients have been reset to default values.",
            [AddedIngredient] = "Added {0} {1} to {2} crafting cost.",
            [RemovedIngredient] = "Removed {0} {1} from {2} crafting cost.",
            [RemovedAllIngredient] = "Removed all {0} from {1} crafting cost.",
            [DoesNotContainIngredient] = "Item {0} does not contain the ingredient {1}; cannot remove.",
            [HasNoIngredients] = "Item {0} has no crafting ingredients",
            [IngredientsList] = "Item {0} crafting ingredients are:",

            [ItemRenamed] = "'{0}' name is now '{1}'",
            [DescriptionChanged] = "'{0}' description is now '{1}'",
            [IconUrlChanged] = "'{0}' icon URL is now {1}",
            [CategoryChanged] = "'{0}' category is now '{1}'",

            [NotAdmin] = "Only admins may use this command.",

            [FacepunchGUIDisabled] = "Crafting from the FacePunch Crafting GUI is disabled. Please press {0} to open the correct crafting gui.",

            [ItemListLoaded] = "Item List was loaded successfully",
            [ItemListSaved] = "Item List was saved successfully",

            [AddRemoveFormat] = "\"amount\" \"Ingredient Name\" \"Item Name\"",
            [IngredientsFormat] = "\"Item Name\"",
            [CraftrateFormat] = "\"Item Name\" rate",
            [RenameFormat] = "\"Item Name\" \"New Name\"",
            [ChangeDescriptionFormat] = "\"Item Name\" \"New Description\"",
            [IconFormat] = "\"Item Name\" \"http://url.com\"",
            [CategoryFormat] = "\"Item Name\" \"Category Name\"",

            ["HelpLine1"] = "Welcome to <size=25><b>CraftUI</b></size>! Commands are:",
            ["HelpLine2"] = "<size=16><b>/craftui.add</b></size> - adds ingredients to an item.",
            ["HelpLine3"] = "<size=16><b>/craftui.remove</b></size> - removes ingredients from an item",
            ["HelpLine4"] = "<size=16><b>/craftui.ingredients</b></size> - prints a list of all current ingredients for an item.",
            ["HelpLine5"] = "<size=16><b>/craftui.clearingredients</b></size> - clears ALL ingredients from an item (free to craft)",
            ["HelpLine6"] = "<size=16><b>/craftui.resetingredients</b></size> - resets an item's ingredients to their default values.",
            ["HelpLine7"] = "<size=16><b>/craftui.craftrate</b></size> - sets the craft rate of an item. 0 is instant craft, 2 is double craft time.",
            ["HelpLine8"] = "<size=16><b>/craftui.block</b></size> - blocks an item from being crafted. It will not be displayed in the UI",
            ["HelpLine9"] = "<size=16><b>/craftui.unblock</b></size> - allows an item to be crafted.",
            ["HelpLine10"] = "<size=16><b>/craftui.blockall</b></size> - blocks all items from crafting.",
            ["HelpLine11"] = "<size=16><b>/craftui.unblockall</b></size> - unblocks all items from crafting.",
            ["HelpLine10"] = "<size=16><b>/craftui.rename</b></size> - renames an item (in the CraftUI only - does not affect inventory)",
            ["HelpLine11"] = "<size=16><b>/craftui.seticon</b></size> - sets the icon of an item to a given URL address (in CraftUI only)",
            ["HelpLine12"] = "<size=16><b>/craftui.save</b></size> - saves any changes made to items.",
            ["HelpLine13"] = "<size=16><b>/craftui.load</b></size> - loads saved item's. NOTE: this will erase any unsaved changes.",
            ["HelpLine14"] = "Type any command followed by help (ex: /craftui.add help) to see detailed information about their use.",
        };
        #endregion

        #region Config Values
        bool AllowOldCrafting = false;
        string OpenCraftUIBinding = "q";
        List<string> CategoryNames = new List<string>()
        {
            ItemCategory.Construction.ToString(),
            ItemCategory.Items.ToString(),
            ItemCategory.Resources.ToString(),
            ItemCategory.Attire.ToString(),
            ItemCategory.Tool.ToString(),
            ItemCategory.Medical.ToString(),
            ItemCategory.Weapon.ToString(),
            ItemCategory.Ammunition.ToString(),
            ItemCategory.Traps.ToString(),
            ItemCategory.Misc.ToString()
        };
        bool IncludeUncraftableItems = false;
        int ItemIconSize = 150;
        #endregion

        #region Oxide Hooks
        void OnServerInitialized()
        {
            LoadDefaultMessages();

            CloseCraftUI_AllPlayers();
            CloseOverlay_AllPlayers();
            S = this;

            itemInfoDic = new Dictionary<int, ItemInfo>();
            customItemCrafts = new HashSet<int>();
            playerDic = new Dictionary<ulong, PlayerInfo>();

            CreateMissingBlueprints();

            displaynameToShortname.Clear();
            originalCraftTimes.Clear();
            originalIngredients.Clear();
            foreach (var itemdef in ItemManager.itemList)
            {
                if (itemdef)
                {
                    if(!displaynameToShortname.ContainsKey(itemdef.displayName.english.ToLower()))
                        displaynameToShortname.Add(itemdef.displayName.english.ToLower(), itemdef.shortname);
                    if (itemdef.Blueprint)
                    {
                        originalCraftTimes.Add(itemdef.itemid, itemdef.Blueprint.time);
                        List<ItemIngredient> ingredients = new List<ItemIngredient>();
                        foreach (var ingredient in itemdef.Blueprint.ingredients)
                        {
                            ingredients.Add(new ItemIngredient(ingredient.itemid, ingredient.amount));
                        }
                        originalIngredients.Add(itemdef.itemid, ingredients);
                    }
                }
            }


            LoadItemInfoDic();

            timer.Repeat(1f, 0, () =>
            {
                RefreshCraftQueue();
            });

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                AddPlayer(player);
            }
        }
        void Loaded()
        {
        }
        void Unload()
        {
            CloseCraftUI_AllPlayers();
            CloseOverlay_AllPlayers();
            SaveItemInfoDic();
            foreach (var kvp in playerDic)
            {
                ResetBinding(kvp.Value.basePlayer);
            }
        }

        void OnPlayerInit(BasePlayer player)
        {
            AddPlayer(player);
        }
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            ResetBinding(player);
        }

        void SetBinding(BasePlayer player)
        {
            rust.RunClientCommand(player, "bind f1 consoletoggle;craftui.closeoverlay;craftui.close");
            rust.RunClientCommand(player, "bind escape craftui.closeoverlay;craftui.close");
            rust.RunClientCommand(player, "bind tab inventory.toggle;craftui.toggleoverlay;craftui.close");
            rust.RunClientCommand(player, "bind " + OpenCraftUIBinding + " craftui.toggle");
        }
        void ResetBinding(BasePlayer player)
        {
            rust.RunClientCommand(player, "bind " + OpenCraftUIBinding + " " + defaultBinds[OpenCraftUIBinding]);
            rust.RunClientCommand(player, "bind f1 consoletoggle");
            rust.RunClientCommand(player, "bind tab inventory.toggle");
        }

        object OnItemCraft(ItemCraftTask task, BasePlayer crafter)
        {
            PlayerInfo playerInfo = GetPlayer(crafter);

            if (AllowOldCrafting)
            {
                RenderHudPanelDelayed(refreshTime, CraftQueueName, playerInfo);
                RenderHudPanelDelayed(refreshTime, CraftQueueTimerName, playerInfo);
                return null;
            }
            if (!customItemCrafts.Contains(task.taskUID))
            {
                foreach (var ingredient in task.blueprint.ingredients)
                {
                    crafter.inventory.GiveItem(ItemManager.CreateByItemID(ingredient.itemid, (int)ingredient.amount * task.amount, 0));
                }
                PlayerChat(crafter, FacepunchGUIDisabled, "'" + OpenCraftUIBinding + "'");
                return false;
            }
            RenderHudPanelDelayed(refreshTime, CraftQueueName, playerInfo);
            RenderHudPanelDelayed(refreshTime, CraftQueueTimerName, playerInfo);
            return null;
        }
        void OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            PlayerInfo playerInfo = GetPlayer(task.owner);

            RenderHudPanelDelayed(refreshTime, CraftQueueName, playerInfo);
            RenderHudPanelDelayed(refreshTime, CraftQueueTimerName, playerInfo);
        }
        void OnItemCraftCancelled(ItemCraftTask task)
        {

            if (task.owner == null) return;
            PlayerInfo playerInfo = GetPlayer(task.owner);
            if (playerInfo == null) return;
            if (!playerInfo.uiOpen) return;

            RenderHudPanelDelayed(refreshTime, CraftQueueName, playerInfo);
            RenderHudPanelDelayed(refreshTime, CraftQueueTimerName, playerInfo);
            RenderHudPanelDelayed(refreshTime, ResourceCostName, playerInfo);
        }

        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            PlayerInfo playerInfo = GetPlayer(player);
            StorageContainer storage = entity as StorageContainer;
            if (storage == null) return;
            float columns = Mathf.Max(1, Mathf.CeilToInt((float)storage.inventorySlots / 6f));
            if (storage is BaseOven)
                columns += 1.5f;
            else if (storage is RepairBench)
                columns += 1;
            else if (storage is LiquidContainer)
                columns += 3.5f;
            else if (storage is SupplyDrop)
                columns += 2f;
            else if (storage is LootContainer)
                columns += 2f;

            RenderOverlay(playerInfo, true, columns);
        }

        void RefreshCraftQueue()
        {
            foreach (var kvp in playerDic)
            {
                if (kvp.Value.uiOpen)
                {
                    RenderHudPanel(CraftQueueTimerName, kvp.Value);
                }
            }
        }

        protected override void LoadDefaultConfig() => PrintWarning("New configuration file created.");
        protected override void LoadConfig()
        {
            base.LoadConfig();

            AllowOldCrafting = GetConfigValue("_Settings", "Allow Old Crafting", AllowOldCrafting);
            var tempBinding = GetConfigValue("_Settings", "Open CraftUI Binding", OpenCraftUIBinding);
            if (defaultBinds.ContainsKey(tempBinding))
                OpenCraftUIBinding = tempBinding;
            CategoryNames = GetConfigValue("_Settings", "Category Names", CategoryNames);
            IncludeUncraftableItems = GetConfigValue("_Settings", "Include Uncraftable Items", IncludeUncraftableItems);
            ItemIconSize = GetConfigValue("_Settings", "Item Icon Size", ItemIconSize);
        }

        T GetConfigValue<T>(string category, string name, T defaultValue)
        {
            var catDic = GetConfigCategory(category);
            if (!catDic.ContainsKey(name))
            {
                catDic.Add(name, defaultValue);
                SaveConfig();
            }

            return (T)Convert.ChangeType(catDic[name], typeof(T));
        }
        List<T> GetConfigValue<T>(string category, string name, List<T> defaultValue)
        {
            var catDic = GetConfigCategory(category);
            object value = null;
            if (!catDic.TryGetValue(name, out value))
            {
                catDic[name] = defaultValue;
                Config[category] = catDic;
                SaveConfig();
                return defaultValue;
            }
            List<object> objectList = value as List<object>;
            List<T> ret = new List<T>();
            foreach (object o in objectList)
            {
                ret.Add((T)o);
            }
            return ret;
        }
        Dictionary<string, object> GetConfigCategory(string category)
        {
            var catDic = Config[category] as Dictionary<string, object>;
            if (catDic == null)
            {
                catDic = new Dictionary<string, object>();
                Config[category] = catDic;
                SaveConfig();
            }
            return catDic;
        }

        void LoadDefaultMessages()
        {
            lang.RegisterMessages(responseDic, this);
        }
        #endregion

        #region Players
        class PlayerInfo
        {
            public readonly BasePlayer basePlayer;
            public bool uiOpen;
            public bool overlayOpen;
            public bool overlayLooting;
            public float overlayLootingColumns;
            public string selectedCategory;
            public ItemInfo selectedItem;
            public int currentCraftAmount;

            public PlayerInfo(BasePlayer player)
            {
                basePlayer = player;
                uiOpen = false;
                selectedCategory = string.Empty;
                selectedItem = null;
                currentCraftAmount = 1;
            }

            public class CraftInfo
            {
                public List<string> amounts, itemTypes, totals, haves;
                public bool canCraft;

                public CraftInfo()
                {
                    amounts = new List<string>();
                    itemTypes = new List<string>();
                    totals = new List<string>();
                    haves = new List<string>();
                    canCraft = true;
                }
            }
            public CraftInfo GetCraftInfo()
            {
                CraftInfo info = new CraftInfo();

                if (selectedItem == null || selectedItem.itemID == 0)
                {
                    info.canCraft = false;
                    return info;
                }

                foreach (ItemIngredient ingredient in selectedItem.ingredients)
                {
                    float amountValue = ingredient.amount;
                    float haveValue = basePlayer.inventory.GetAmount(ingredient.ingredientID);
                    float totalValue = ingredient.amount * currentCraftAmount;
                    if (haveValue < totalValue)
                        info.canCraft = false;


                    info.amounts.Add(amountValue.ToString());
                    info.itemTypes.Add(ingredient.realName);
                    info.totals.Add(totalValue.ToString());
                    info.haves.Add(haveValue.ToString());
                }

                return info;
            }
        }

        static Dictionary<ulong, PlayerInfo> playerDic = new Dictionary<ulong, PlayerInfo>();

        /// <summary>
        /// Returns true if the player was successfully added.
        /// </summary>
        bool AddPlayer(BasePlayer player)
        {
            if (!playerDic.ContainsKey(player.userID))
            {
                PlayerInfo playerInfo = new PlayerInfo(player);
                playerDic.Add(player.userID, playerInfo);
                SetBinding(player);
                return true;
            }
            return false;
        }
        PlayerInfo GetPlayer(BasePlayer player)
        {
            PlayerInfo playerInfo = null;
            if (!playerDic.TryGetValue(player.userID, out playerInfo))
            {
                playerInfo = new PlayerInfo(player);
                playerDic.Add(player.userID, playerInfo);
                SetBinding(player);
            }
            return playerInfo;
        }
        #endregion

        #region Item Customization
        class ItemInfo
        {
            public int itemID;
            public string realName;
            public string customName;
            public string customDescription;
            public bool blocked;
            public float craftRate;
            public List<ItemIngredient> ingredients;
            public string iconURL;
            public string category;

            public ItemInfo()
            {
                ingredients = new List<ItemIngredient>();
            }
            public ItemInfo(ItemDefinition itemDef)
            {
                itemID = itemDef.itemid;
                realName = itemDef.displayName.english;
                customName = itemDef.displayName.english;
                customDescription = itemDef.displayDescription.english;
                blocked = false;
                craftRate = 1;
                ingredients = new List<ItemIngredient>();
                if (itemDef.Blueprint != null && itemDef.Blueprint.ingredients != null)
                {
                    foreach (var ingredient in itemDef.Blueprint.ingredients)
                    {
                        ingredients.Add(new ItemIngredient(ingredient.itemid, ingredient.amount));
                    }
                }

                iconURL = S.GetItemIconURL(itemDef.shortname);
                category = itemDef.category.ToString();
            }

            public ItemDefinition GetItemDef()
            {
                return ItemManager.FindItemDefinition(itemID);
            }
            public bool GetCraftable(PlayerInfo playerInfo)
            {
                if (playerInfo == null)
                    return false;

                foreach (var ingredient in ingredients)
                {
                    if (playerInfo.basePlayer.inventory.GetAmount(ingredient.ingredientID) < ingredient.amount)
                        return false;
                }

                return true;
            }
            public int GetCraftCount(PlayerInfo playerInfo)
            {
                if (playerInfo == null)
                    return -1;

                int num = -1;
                foreach (var ingredient in ingredients)
                {
                    num = Mathf.FloorToInt((float)playerInfo.basePlayer.inventory.GetAmount(ingredient.ingredientID) / ingredient.amount);
                    if (num <= 0)
                        return -1;
                }
                return num;
            }
            public string GetFormattedURL()
            {
                return string.Format(iconURL, S.ItemIconSize.ToString());
            }

            public static ItemInfo FromObject(object obj)
            {
                Dictionary<string, object> dic = obj as Dictionary<string, object>;
                if (dic == null)
                    return null;

                ItemInfo itemInfo = new ItemInfo();
                itemInfo.itemID = (int)dic[nameof(itemID)];
                itemInfo.realName = (string)dic[nameof(realName)];
                itemInfo.customName = (string)dic[nameof(customName)];
                itemInfo.customDescription = (string)dic[nameof(customDescription)];
                itemInfo.blocked = (bool)dic[nameof(blocked)];
                itemInfo.craftRate = (float)(double)dic[nameof(craftRate)];
                itemInfo.iconURL = (string)dic[nameof(iconURL)];
                itemInfo.category = (string)dic[nameof(category)];

                itemInfo.ingredients = new List<ItemIngredient>();
                List<object> ingredientList = dic[nameof(ingredients)] as List<object>;
                foreach (var iObj in ingredientList)
                {
                    ItemIngredient ingredient = ItemIngredient.FromObject(iObj);
                    if (ingredient != null)
                        itemInfo.ingredients.Add(ingredient);
                }

                return itemInfo;
            }
        }
        class ItemIngredient
        {
            public int ingredientID;
            public string realName;
            public float amount;

            public ItemIngredient()
            {
                ingredientID = 0;
                realName = "";
                amount = 0;
            }
            public ItemIngredient(int ingredientID, float amount)
            {
                this.ingredientID = ingredientID;
                realName = GetItemDef().displayName.english;
                this.amount = amount;
            }

            public ItemDefinition GetItemDef()
            {
                return ItemManager.FindItemDefinition(ingredientID);
            }
            public ItemAmount GetItemAmount()
            {
                return new ItemAmount(GetItemDef(), amount);
            }

            public static ItemIngredient FromObject(object obj)
            {
                Dictionary<string, object> dic = obj as Dictionary<string, object>;
                if (dic == null)
                    return null;

                ItemIngredient ingredient = new ItemIngredient();
                ingredient.ingredientID = (int)dic[nameof(ingredientID)];
                ingredient.realName = (string)dic[nameof(realName)];
                ingredient.amount = (float)(double)dic[nameof(amount)];

                return ingredient;
            }
        }

        static Dictionary<int, ItemInfo> itemInfoDic = new Dictionary<int, ItemInfo>();
        static Dictionary<int, float> originalCraftTimes = new Dictionary<int, float>();
        static Dictionary<int, List<ItemIngredient>> originalIngredients = new Dictionary<int, List<ItemIngredient>>();
        static HashSet<int> nullBlueprintSet = new HashSet<int>();

        bool CanAddItemInfo(ItemDefinition itemDef)
        {
            if (itemDef == null || itemDef.displayName == null)
                return false;
            if (itemInfoDic.ContainsKey(itemDef.itemid))
                return false;

            return true;
        }
        void AddItemInfo(ItemDefinition itemDef, bool forceAdd = false)
        {
            if (forceAdd || CanAddItemInfo(itemDef))
                itemInfoDic.Add(itemDef.itemid, new ItemInfo(itemDef));
        }
        void SaveItemInfoDic()
        {
            Config[ItemInfoListName] = itemInfoDic.Values;
            SaveConfig();
        }
        void LoadItemInfoDic()
        {
            var objList = Config[ItemInfoListName] as List<object>;
            if (objList == null)
            {
                objList = new List<object>();
                Config[ItemInfoListName] = objList;
                SaveConfig();
            }
            itemInfoDic = new Dictionary<int, ItemInfo>();
            foreach (var obj in objList)
            {
                ItemInfo itemInfo = ItemInfo.FromObject(obj);
                if (itemInfo == null) continue;

                itemInfoDic.Add(itemInfo.itemID, itemInfo);
            }

            VerifyItemInfoDic();
        }
        void VerifyItemInfoDic()
        {
            if (ItemManager.itemList == null)
            {
                PrintError("ItemManager item list is null at a crucial stage of initilization. Cannot populate ItemInfoDic");
                return;
            }
            if (itemInfoDic == null)
                PrintError("Item Info Dic is Null");

            bool changeMade = false;

            List<ItemInfo> values = new List<ItemInfo>(itemInfoDic.Values);
            List<int> toRemove = new List<int>();
            foreach (ItemInfo value in values)
            {
                ItemDefinition itemDef = ItemManager.FindItemDefinition(value.itemID);
                if (itemDef == null)
                {
                    toRemove.Add(value.itemID);
                    continue;
                }

                if (value.realName != itemDef.displayName.english)
                {
                    value.realName = itemDef.displayName.english;
                    changeMade = true;
                }
                if (value.category == null)
                {
                    value.category = itemDef.category.ToString();
                    changeMade = true;
                }
                if (value.iconURL == "")
                {
                    value.iconURL = GetItemIconURL(itemDef.shortname);
                    changeMade = true;
                }

                if (value.ingredients == null)
                    value.ingredients = new List<ItemIngredient>();
                List<int> ingredientIndicesToRemove = new List<int>();
                for (int i = 0; i < value.ingredients.Count; i++)
                {
                    ItemDefinition ingredientDef = ItemManager.FindItemDefinition(value.ingredients[i].ingredientID);
                    if (ingredientDef == null)
                        ingredientIndicesToRemove.Add(i);
                    else if (value.ingredients[i].realName != ingredientDef.displayName.english)
                    {
                        value.ingredients[i].realName = ingredientDef.displayName.english;
                        changeMade = true;
                    }
                }

                foreach (int index in ingredientIndicesToRemove)
                {
                    value.ingredients.RemoveAt(index);
                    changeMade = true;
                }
            }

            foreach (int id in toRemove)
            {
                itemInfoDic.Remove(id);
                changeMade = true;
            }

            foreach (var item in ItemManager.bpList)
            {
                if (CanAddItemInfo(item.targetItem))
                {
                    AddItemInfo(item.targetItem, true);
                    changeMade = true;
                }
            }
            foreach (var item in ItemManager.itemList)
            {
                if (CanAddItemInfo(item))
                {
                    AddItemInfo(item, true);
                    changeMade = true;
                }
            }

            if (changeMade)
                SaveItemInfoDic();
        }
        void CreateMissingBlueprints()
        {
            foreach (ItemDefinition itemDef in ItemManager.itemList)
            {
                if (itemDef == null || itemDef.Blueprint != null)
                    continue;

                CreateMissingBlueprint(itemDef);
            }
        }
        ItemBlueprint CreateMissingBlueprint(ItemDefinition itemDef)
        {
            if (itemDef.Blueprint != null)
                return itemDef.Blueprint;

            ItemBlueprint bp = itemDef.gameObject.AddComponent<ItemBlueprint>();
            bp.ingredients = new List<ItemAmount>();
            bp.userCraftable = false;
            bp.isResearchable = false;
            bp.rarity = Rarity.None;
            bp.NeedsSteamItem = false;
            bp.amountToCreate = 1;
            bp.time = 30f;

            nullBlueprintSet.Add(itemDef.itemid);

            return bp;
        }

        ItemInfo FindItemInfo(ItemDefinition itemDef)
        {
            return FindItemInfo(itemDef.itemid);
        }
        ItemInfo FindItemInfo(int itemID)
        {
            ItemInfo info = null;
            itemInfoDic.TryGetValue(itemID, out info);
            return info;
        }
        #endregion

        #region Chat Commands
        [ChatCommand("craftui.add")]
        void AddIngredient_ChatCommands(BasePlayer player, string command, string[] args)
        {
            if (!AdminCheck(player)) return;
            if (!ArgsCheck(player, args, 3, command, AddRemoveFormat)) return;

            int ingredientAmount;
            if (!int.TryParse(args[0], out ingredientAmount))
            {
                PlayerChat(player, Invalid, args[0], "ingredient amount");
                return;
            }

            ItemDefinition ingredientDef = DisplayNameToItemDef(player, args[1]);
            if (ingredientDef == null) return;
            ItemDefinition itemDef = DisplayNameToItemDef(player, args[2]);
            if (itemDef == null) return;
            ItemInfo itemInfo = FindItemInfo(itemDef);
            if (itemInfo == null) return;

            if (itemInfo.ingredients.Any(x => x.ingredientID == ingredientDef.itemid))
            {
                itemInfo.ingredients.First(x => x.ingredientID == ingredientDef.itemid).amount += ingredientAmount;
            }
            else
                itemInfo.ingredients.Add(new ItemIngredient(ingredientDef.itemid, ingredientAmount));

            PlayerChat(player, AddedIngredient, ingredientAmount, ingredientDef.displayName.english, itemDef.displayName.english);
            Puts("Player '" + player.displayName + "' added " + ingredientAmount + " " + ingredientDef.displayName.english + " to " + itemDef.displayName.english);
        }
        [ChatCommand("craftui.remove")]
        void RemoveIngredient_ChatCommands(BasePlayer player, string command, string[] args)
        {
            if (!AdminCheck(player)) return;
            if (!ArgsCheck(player, args, 3, command, AddRemoveFormat)) return;

            int ingredientAmount;
            if (!int.TryParse(args[0], out ingredientAmount))
            {
                PlayerChat(player, Invalid, args[0], "ingredient amount");
                return;
            }

            ItemDefinition ingredientDef = DisplayNameToItemDef(player, args[1]);
            if (ingredientDef == null) return;
            ItemDefinition itemDef = DisplayNameToItemDef(player, args[2]);
            if (itemDef == null) return;
            ItemInfo itemInfo = FindItemInfo(itemDef);
            if (itemInfo == null) return;

            if (!itemInfo.ingredients.Any(x => x.ingredientID == ingredientDef.itemid))
            {
                PlayerChat(player, DoesNotContainIngredient, itemDef.displayName.english, ingredientDef.displayName.english);
                return;
            }

            ItemIngredient ingredient = itemInfo.ingredients.First(x => x.ingredientID == ingredientDef.itemid);
            if (ingredientAmount < ingredient.amount)
            {
                ingredient.amount -= ingredientAmount;
                PlayerChat(player, RemovedIngredient, ingredientAmount, ingredientDef.displayName.english, itemDef.displayName.english);
                Puts("Player '" + player.displayName + "' removed " + ingredientAmount + " " + ingredientDef.displayName.english + " from " + itemDef.displayName.english);
            }
            else
            {
                itemInfo.ingredients.Remove(ingredient);
                PlayerChat(player, RemovedAllIngredient, ingredientDef.displayName.english, itemDef.displayName.english);
                Puts("Player '" + player.displayName + "' removed all " + ingredientDef.displayName.english + " from " + itemDef.displayName.english);
            }
        }
        [ChatCommand("craftui.ingredients")]
        void IngredientsList_ChatCommand(BasePlayer player, string command, string[] args)
        {
            if (!AdminCheck(player)) return;
            if (!ArgsCheck(player, args, 1, command, IngredientsFormat)) return;

            ItemDefinition itemDef = DisplayNameToItemDef(player, args[0]);
            if (itemDef == null) return;
            ItemInfo info = FindItemInfo(itemDef);
            if (info == null) return;

            if (info.ingredients.Count == 0)
            {
                PlayerChat(player, HasNoIngredients, itemDef.displayName.english);
            }
            else
            {
                PlayerChat(player, IngredientsList, itemDef.displayName.english);
                foreach (ItemIngredient ingredient in info.ingredients)
                {
                    SendReply(player, ingredient.amount + " " + ingredient.GetItemDef().displayName.english);
                }
            }
        }
        [ChatCommand("craftui.clearingredients")]
        void ClearIngredients_ChatCommands(BasePlayer player, string command, string[] args)
        {
            if (!AdminCheck(player)) return;
            if (!ArgsCheck(player, args, 1, command, IngredientsFormat)) return;

            ItemDefinition itemDef = DisplayNameToItemDef(player, args[0]);
            if (itemDef == null) return;
            ItemInfo itemInfo = FindItemInfo(itemDef);
            if (itemInfo == null) return;

            itemInfo.ingredients.Clear();

            PlayerChat(player, IngredientsCleared, itemDef.displayName.english);
            Puts("Player '" + player.displayName + "' cleared " + itemDef.displayName.english + " crafting ingredients");
        }
        [ChatCommand("craftui.resetingredients")]
        void ResetIngredients_ChatCommands(BasePlayer player, string command, string[] args)
        {
            if (!AdminCheck(player)) return;
            if (!ArgsCheck(player, args, 1, command, IngredientsFormat)) return;

            ItemDefinition itemDef = DisplayNameToItemDef(player, args[0]);
            if (itemDef == null) return;
            ItemBlueprint bp = itemDef.Blueprint;
            if (bp == null) return;
            ItemInfo itemInfo = FindItemInfo(itemDef);
            if (itemInfo == null) return;

            itemInfo.ingredients = new List<ItemIngredient>(originalIngredients[itemInfo.itemID]);

            PlayerChat(player, IngredientsReset, itemDef.displayName.english);
            Puts("Player '" + player.displayName + "' reset " + itemDef.displayName.english + " crafting ingredients");
        }
        [ChatCommand("craftui.craftrate")]
        void CraftRate_ChatCommands(BasePlayer player, string command, string[] args)
        {
            if (!AdminCheck(player)) return;
            if (!ArgsCheck(player, args, 2, command, CraftrateFormat)) return;

            ItemDefinition itemDef = DisplayNameToItemDef(player, args[0]);
            if (itemDef == null) return;
            ItemInfo itemInfo = FindItemInfo(itemDef);
            if (itemInfo == null) return;

            float rate = 0;
            if (!float.TryParse(args[1], out rate))
            {
                PlayerChat(player, Invalid, args[1], "crafting rate");
                return;
            }

            itemInfo.craftRate = rate;

            PlayerChat(player, ItemRateChange, itemDef.displayName.english, rate);
            Puts("Player '" + player.displayName + "' changed " + itemDef.displayName.english + " craft rate to " + rate);
        }
        [ChatCommand("craftui.block")]
        void Block_ChatCommands(BasePlayer player, string command, string[] args)
        {
            BlockItem(player, command, args, true);
        }
        [ChatCommand("craftui.unblock")]
        void UnBlock_ChatCommands(BasePlayer player, string command, string[] args)
        {
            BlockItem(player, command, args, false);
        }
        void BlockItem(BasePlayer player, string command, string[] args, bool block)
        {
            if (!AdminCheck(player)) return;
            if (!ArgsCheck(player, args, 1, command, IngredientsFormat)) return;

            ItemDefinition itemDef = DisplayNameToItemDef(player, args[0]);
            if (itemDef == null) return;
            ItemInfo info = FindItemInfo(itemDef);
            if (info == null) return;

            info.blocked = block;

            if (block)
            {
                PlayerChat(player, ItemBlocked, itemDef.displayName.english);
                Puts("Player '" + player.displayName + "' blocked item " + itemDef.displayName.english);
            }
            else
            {
                PlayerChat(player, ItemNotBlocked, itemDef.displayName.english);
                Puts("Player '" + player.displayName + "' unblocked item " + itemDef.displayName.english);
            }
        }
        [ChatCommand("craftui.blockall")]
        void BlockAll(BasePlayer player, string command, string[] args)
        {
            if (!AdminCheck(player)) return;

            foreach (var kvp in itemInfoDic)
            {
                kvp.Value.blocked = true;
            }

            PlayerChat(player, AllItemsBlocked);
            Puts("Player '" + player.displayName + "' blocked all items.");
        }
        [ChatCommand("craftui.unblockall")]
        void UnblockAll(BasePlayer player, string command, string[] args)
        {
            if (!AdminCheck(player)) return;

            foreach (var kvp in itemInfoDic)
            {
                kvp.Value.blocked = false;
            }

            PlayerChat(player, AllItemsUnblocked);
            Puts("Player '" + player.displayName + "' unblocked all items.");
        }
        [ChatCommand("craftui.rename")]
        void Rename_ChatCommand(BasePlayer player, string command, string[] args)
        {
            if (!AdminCheck(player)) return;
            if (!ArgsCheck(player, args, 2, command, RenameFormat)) return;

            ItemDefinition itemDef = DisplayNameToItemDef(player, args[0]);
            if (itemDef == null) return;
            ItemInfo itemInfo = FindItemInfo(itemDef);
            if (itemInfo == null) return;

            itemInfo.customName = args[1];

            PlayerChat(player, ItemRenamed, itemInfo.realName, itemInfo.customName);
            Puts("Player '" + player.displayName + "' changed item " + itemInfo.realName + " name to " + itemInfo.customName);
        }
        [ChatCommand("craftui.description")]
        void ChangeDrescription_ChatCommand(BasePlayer player, string command, string[] args)
        {
            if (!AdminCheck(player)) return;
            if (!ArgsCheck(player, args, 2, command, ChangeDescriptionFormat)) return;

            ItemDefinition itemDef = DisplayNameToItemDef(player, args[0]);
            if (itemDef == null) return;
            ItemInfo itemInfo = FindItemInfo(itemDef);
            if (itemInfo == null) return;

            itemInfo.customDescription = args[1];

            PlayerChat(player, DescriptionChanged, itemInfo.realName, itemInfo.customDescription);
            Puts("Player '" + player.displayName + "' changed item " + itemInfo.realName + " description to " + itemInfo.customName);
        }
        [ChatCommand("craftui.category")]
        void Category_ChatCommand(BasePlayer player, string command, string[] args)
        {
            if (!AdminCheck(player)) return;
            if (!ArgsCheck(player, args, 2, command, CategoryFormat)) return;

            ItemDefinition itemDef = DisplayNameToItemDef(player, args[0]);
            if (itemDef == null) return;
            ItemInfo itemInfo = FindItemInfo(itemDef);
            if (itemInfo == null) return;
            string category = "";
            foreach (var cat in CategoryNames)
            {
                if (cat.ToLower() == args[1].ToLower())
                {
                    category = cat;
                    break;
                }
            }
            if (category == "")
            {
                PlayerChat(player, Invalid, "Category Name");
                return;
            }

            itemInfo.category = category;
            PlayerChat(player, CategoryChanged, itemDef.displayName.english, itemInfo.category);
        }
        [ChatCommand("craftui.seticon")]
        void SetIcon_ChatCommand(BasePlayer player, string command, string[] args)
        {
            if (!AdminCheck(player)) return;
            if (!ArgsCheck(player, args, 2, command, IconFormat)) return;

            ItemDefinition itemDef = DisplayNameToItemDef(player, args[0]);
            if (itemDef == null) return;
            ItemInfo itemInfo = FindItemInfo(itemDef);
            if (itemInfo == null) return;

            itemInfo.iconURL = args[1];
            PlayerChat(player, itemInfo.realName, itemInfo.iconURL);
            Puts("Player '" + player.displayName + "' set " + itemInfo.realName + " icon URL to " + itemInfo.iconURL);
        }
        [ChatCommand("craftui.load")]
        void Load_ChatCommand(BasePlayer player, string command, string[] args)
        {
            if (!AdminCheck(player)) return;

            LoadItemInfoDic();
            PlayerChat(player, ItemListLoaded);
        }
        [ChatCommand("craftui.save")]
        void Save_ChatCommand(BasePlayer player, string command, string[] args)
        {
            if (!AdminCheck(player)) return;

            SaveItemInfoDic();
            PlayerChat(player, ItemListSaved);
        }
        [ChatCommand("craftui")]
        void CraftUI_ChatCommand(BasePlayer player, string command, string[] args)
        {
            if (!AdminCheck(player)) return;
            ShowHelpMessage(player);
        }
        void ShowHelpMessage(BasePlayer player)
        {
            if (!AdminCheck(player)) return;

            for (int i = 0; i < 14; i++)
            {
                PlayerChat(player, "HelpLine" + (i + 1).ToString());
            }
        }
        bool AdminCheck(BasePlayer player)
        {
            if (!player.IsAdmin)
            {
                PlayerChat(player, NotAdmin);
                return false;
            }
            return true;
        }
        bool ArgsCheck(BasePlayer player, string[] args, int minLength, string command, string formatKey)
        {
            if (args == null || args.Length == 0 || args[0].ToLower() == "help")
            {
                PlayerChat(player, FormatHelp, command, Lang(formatKey, player.UserIDString));
                return false;
            }
            if (args.Length < minLength)
            {
                PlayerChat(player, InvalidFormat, command, Lang(formatKey, player.UserIDString));
                return false;
            }
            return true;
        }
        #endregion

        #region UIRendering
        class PanelRect
        {
            public float left, bottom, right, top;
            public PanelRect()
            {
                left = 0f;
                bottom = 0f;
                right = 1f;
                top = 1f;
            }
            public PanelRect(float left, float bottom, float right, float top)
            {
                this.left = left;
                this.bottom = bottom;
                this.right = right;
                this.top = top;
            }

            public string AnchorMin
            {
                get { return left + " " + bottom; }
            }
            public string AnchorMax
            {
                get { return right + " " + top; }
            }

            public PanelRect RelativeTo(PanelRect other)
            {
                left = other.left + (other.right - other.left) * left;
                right = other.left + (other.right - other.left) * right;
                top = other.bottom + (other.top - other.bottom) * top;
                bottom = other.bottom + (other.top - other.bottom) * bottom;
                return this;
            }

            public PanelRect Copy()
            {
                return new PanelRect(left, bottom, right, top);
            }
        }
        class PanelInfo
        {
            public PanelRect rect;
            public string backgroundColor;

            public PanelInfo(PanelRect rect, string color)
            {
                this.rect = rect;
                this.backgroundColor = color;
            }
        }

        const string CraftUIName = "CraftUI";
        const string CategoriesName = "Categories";
        const string ItemListName = "ItemList";
        const string ItemInfoName = "ItemInfo";
        const string ResourceCostName = "ResourceCost";
        const string CraftCountName = "CraftCount";
        const string CraftQueueName = "CraftQueue";
        const string CraftQueueTimerName = "CraftQueueTimer";

        const string CraftingButtonOverlayName = "CraftingButtonOverlay";
        const string QuickCraftOverlayName = "QuickCraftOverlay";
        const string QuickCraftOverlayLootingName = "QuicKCraftOverlayLooting";

        static readonly Dictionary<string, PanelInfo> panelAnchors = new Dictionary<string, PanelInfo>()
        {
            { CraftUIName, new PanelInfo(new PanelRect(0, 0, 1, 1), "0.1 0.1 0.1 0.9") },
            { CategoriesName, new PanelInfo(new PanelRect(0.06f, 0.25f, 0.16f, 0.9f), "0.4 0.4 0.4 0.4") },
            { ItemListName, new PanelInfo(new PanelRect(0.1625f, 0.25f, 0.55f, 0.9f), "0.4 0.4 0.4 0.4") },
            { ItemInfoName, new PanelInfo(new PanelRect(0.5525f, 0.355f, 0.94f, 0.9f), "0.4 0.4 0.4 0.4") },
            { ResourceCostName, new PanelInfo(new PanelRect(0.5525f, 0.2f, 0.94f, 0.45f), "0.4 0.4 0.4 0.4") },
            { CraftCountName, new PanelInfo(new PanelRect(0.5525f, 0.15f, 0.94f, 0.1975f), "0.4 0.4 0.4 0.4") },
            { CraftQueueName, new PanelInfo(new PanelRect(0.06f, 0.15f, 0.55f, 0.246f), "0.4 0.4 0.4 0.4") },
            { CraftQueueTimerName, new PanelInfo(new PanelRect(0, 0, 0, 0), "0 0 0 0") }
        };
        static readonly Dictionary<string, PanelInfo> overlayAnchors = new Dictionary<string, PanelInfo>()
        {
            { CraftingButtonOverlayName, new PanelInfo( new PanelRect(0.2f, 0.9f, 0.8f, 1f), "0 0 0 0") },
            { QuickCraftOverlayName, new PanelInfo( new PanelRect(0.65f, 0.125f, 0.95f, 0.45f), "0.3 0.3 0.3 1") },
            { QuickCraftOverlayLootingName, new PanelInfo( new PanelRect(0.65f, 0.475f, 0.95f, 0.8f), "0.3 0.3 0.3 1") }
        };

        //This dictionary is used for the positiopn of the quick craft overlay. It's position needs to be shifted based on the number of columns on a lootable item.
        static readonly Dictionary<float, PanelRect> lootableOverlayAnchors = new Dictionary<float, PanelRect>()
        {
            { 1, new PanelRect(0.65f, 0.325f, 0.95f, 0.65f) },
            { 2, new PanelRect(0.65f, 0.4f, 0.95f, 0.725f) },
            { 2.5f, new PanelRect(0.65f, 0.45f, 0.95f, 0.775f) },
            { 3, new PanelRect(0.65f, 0.5f, 0.95f, 0.825f) },
            { 4, new PanelRect(0.65f, 0.475f, 0.95f, 0.8f) },
            { 4.5f, new PanelRect(0.65f, 0.55f, 0.95f, 0.875f) },
            { 5, new PanelRect(0.65f, 0.65f, 0.95f, 0.975f) },
        };

        void ToggleCraftUI(PlayerInfo playerInfo)
        {
            if (playerInfo == null)
                return;

            if (playerInfo.uiOpen)
                CloseCraftUI(playerInfo);
            else
                RenderCraftUI(playerInfo);
        }
        void RenderCraftUI(PlayerInfo playerInfo)
        {
            if (playerInfo == null)
                return;

            playerInfo.uiOpen = true;

            foreach (var panel in panelAnchors)
            {
                RenderHudPanel(panel.Key, playerInfo);
            }
        }
        void CloseCraftUI(PlayerInfo playerInfo)
        {
            if (playerInfo == null)
                return;

            foreach (var kvp in panelAnchors)
            {
                CuiHelper.DestroyUi(playerInfo.basePlayer, kvp.Key);
            }
            playerInfo.uiOpen = false;
        }
        void CloseCraftUI_AllPlayers()
        {
            if (BasePlayer.activePlayerList == null || BasePlayer.activePlayerList.Count == 0)
                return;

            foreach (var p in BasePlayer.activePlayerList)
            {
                PlayerInfo playerInfo = GetPlayer(p);
                if (playerInfo != null)
                    CloseCraftUI(playerInfo);
            }
        }

        void RenderHudPanel(string panelName, PlayerInfo playerInfo)
        {
            if (playerInfo == null || playerInfo.basePlayer == null)
            {
                PrintError("Render Panel called with a null PlayerInfo");
                return;
            }

            if (!panelAnchors.ContainsKey(panelName))
            {
                PrintError("Cannot render panel '" + panelName + "' for player '" + playerInfo.basePlayer.displayName + "'. It does not exist");
                PlayerChat(playerInfo.basePlayer, UnknownError);
                return;
            }

            if (!playerInfo.uiOpen)
                return;

            PanelRect panelRect = panelAnchors[panelName].rect;
            CuiHelper.DestroyUi(playerInfo.basePlayer, panelName);
            CuiElementContainer elements = new CuiElementContainer();

            elements.Add(new CuiPanel
            {
                Image = { Color = panelAnchors[panelName].backgroundColor },
                RectTransform = { AnchorMin = panelRect.AnchorMin, AnchorMax = panelRect.AnchorMax },
                CursorEnabled = true
            }, "Hud", panelName);

            switch (panelName)
            {
                case CategoriesName:
                    RenderCategories(elements, playerInfo);
                    break;

                case ItemListName:
                    RenderItemList(elements, playerInfo);
                    break;

                case ItemInfoName:
                    RenderItemInfo(elements, playerInfo);
                    break;

                case ResourceCostName:
                    RenderResourceCosts(elements, playerInfo);
                    break;

                case CraftCountName:
                    RenderCraftCount(elements, playerInfo);
                    break;

                case CraftQueueName:
                    RenderCraftQueue(elements, playerInfo);
                    break;

                case CraftQueueTimerName:
                    RenderCraftQueueTimer(elements, playerInfo);
                    break;
            }

            CuiHelper.AddUi(playerInfo.basePlayer, elements);
        }
        void RenderHudPanelDelayed(float delay, string panelName, PlayerInfo playerInfo)
        {
            NextTick(() => RenderHudPanel(panelName, playerInfo));
            timer.Once(delay, () => RenderHudPanel(panelName, playerInfo));
        }
        void RenderCategories(CuiElementContainer elements, PlayerInfo playerInfo)
        {

            List<string> itemCategories = new List<string>(CategoryNames);
            itemCategories.Reverse();

            for (int i = 0; i < itemCategories.Count; i++)
            {
                bool selected = playerInfo.selectedCategory == itemCategories[i];
                AddCategory(elements, itemCategories.Count, i, itemCategories[i], selected);
            }
        }
        void AddCategory(CuiElementContainer elements, float numElements, float index, string name, bool selected)
        {
            var bottom = (index / numElements);
            var top = ((index + 1) / numElements);
            top -= 0.0025f;
            bottom += 0.0025f;

            string backgroundColor = selected ? "0 0.5 0.9 0.8" : "0.35 0.35 0.35 0.8";

            string cName = name;
            if (CategoryShortNames.ContainsKey(cName))
                cName = CategoryShortNames[cName];

            PanelRect rect = new PanelRect(0, bottom, 1, top);
            elements.Add(new CuiButton
            {
                Button = { Command = $"category.select { name }", Color = backgroundColor },
                RectTransform =
                {
                    AnchorMin = rect.AnchorMin,
                    AnchorMax = rect.AnchorMax
                },
                Text = { Text = cName.ToUpper(), FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 1" }
            }, CategoriesName);
        }

        void RenderItemList(CuiElementContainer elements, PlayerInfo playerInfo)
        {
            if (playerInfo == null || itemInfoDic == null)
                return;

            List<ItemInfo> craftableItems = new List<ItemInfo>();
            List<ItemInfo> uncraftableItems = new List<ItemInfo>();
            foreach (var itemInfo in itemInfoDic.Values)
            {
                if (itemInfo == null) continue;
                if (itemInfo.category != playerInfo.selectedCategory) continue;
                if (itemInfo.blocked) continue;
                ItemDefinition itemDef = itemInfo.GetItemDef();
                if (itemDef == null) continue;
                if (!IncludeUncraftableItems && !itemDef.Blueprint.userCraftable)
                    continue;
                if (itemInfo.GetCraftable(playerInfo))
                    craftableItems.Add(itemInfo);
                else
                    uncraftableItems.Add(itemInfo);
            }


            float lowestBottom = 1f;
            for (int i = 0; i < craftableItems.Count; i++)
            {
                if (craftableItems[i] == null)
                    continue;

                float bottom = AddItem(elements, i, craftableItems[i], playerInfo, true);
                if (bottom < lowestBottom)
                    lowestBottom = bottom;
            }
            for (int i = 0; i < uncraftableItems.Count; i++)
            {
                if (uncraftableItems[i] == null)
                    continue;

                AddItem(elements, i, uncraftableItems[i], playerInfo, false, lowestBottom);
            }
        }
        float AddItem(CuiElementContainer elements, float index, ItemInfo itemInfo, PlayerInfo playerInfo, bool craftable, float startTop = 1)
        {
            const int maxRows = 8;
            const int maxColumns = 9;

            int row = (int)(index / maxColumns);
            int column = (int)index - (row * maxColumns);

            float left = (float)column / maxColumns;
            float right = (float)(column + 1) / maxColumns;
            float top = startTop - ((float)row / maxRows);
            float bottom = startTop - ((float)(row + 1) / maxRows);
            left += 0.0025f;
            right -= 0.0025f;
            top -= 0.0025f;
            bottom += 0.0025f;

            PanelRect rect = new PanelRect(left, bottom, right, top);
            bool selected = playerInfo.selectedItem == itemInfo;
            string buttonColor = selected ? "0 0.4 0.7 0.3" : "0 0 0 0";

            CuiRawImageComponent rawImage = new CuiRawImageComponent();
            //rawImage.Url = itemInfo.GetFormattedURL();
            rawImage.Png = ImageLibrary.Call<string>("GetImage", itemInfo.GetItemDef().shortname);
            rawImage.Sprite = transparentSprite;
            rawImage.Color = craftable ? "1 1 1 1" : "1 1 1 0.4";

            elements.Add(new CuiElement
            {
                Parent = ItemListName,
                Components =
                    {
                        rawImage,
                        new CuiRectTransformComponent {AnchorMin = rect.AnchorMin, AnchorMax = rect.AnchorMax },
                    }
            });

            if (selected)
            {
                float characterSize = 0.04f;
                float width = 0.3f + itemInfo.customName.Length * characterSize;
                PanelRect textRect = new PanelRect(-width / 2f, 1.05f, 1 + width / 2f, 1.35f).RelativeTo(rect);
                elements.Add(new CuiButton
                {
                    Button = { Color = "0 0.6 1 1" },
                    RectTransform = { AnchorMin = textRect.AnchorMin, AnchorMax = textRect.AnchorMax },
                    Text = { Text = itemInfo.customName, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FontSize = 16 }
                }, ItemListName);
            }

            elements.Add(new CuiButton
            {
                Button = { Command = $"item.select { itemInfo.itemID }", Color = buttonColor },//, Sprite = transparentSprite },//, Color = backgroundColor },
                RectTransform =
                {
                    AnchorMin = rect.AnchorMin,
                    AnchorMax = rect.AnchorMax
                },
                Text = { Text = "", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0 0 0 0" },//, Color = "0.8 0.8 0.8 1" }
            }, ItemListName);

            return bottom;
        }

        void RenderItemInfo(CuiElementContainer elements, PlayerInfo playerInfo)
        {
            if (playerInfo.selectedItem == null)
                return;

            PanelRect itemInfoRect = panelAnchors[ItemInfoName].rect;

            PanelRect textRect = new PanelRect(0, 0.825f, 1, 0.975f);
            elements.Add(new CuiElement
            {
                Parent = ItemInfoName,
                Components =
                {
                    new CuiTextComponent { Text = playerInfo.selectedItem.customName, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FontSize = 24 },
                    new CuiRectTransformComponent { AnchorMin = textRect.AnchorMin, AnchorMax = textRect.AnchorMax }
                }
            });

            PanelRect descriptionRect = new PanelRect(0.1f, 0.4f, 0.9f, 0.7f);
            elements.Add(new CuiElement
            {
                Parent = ItemInfoName,
                Components =
                {
                    new CuiTextComponent { Text = playerInfo.selectedItem.customDescription, Align = TextAnchor.UpperLeft, Color = "1 1 1 1", FontSize = 14 },
                    new CuiRectTransformComponent { AnchorMin = descriptionRect.AnchorMin, AnchorMax = descriptionRect.AnchorMax }
                }
            });

            PanelRect iconRect = new PanelRect(0.05f, 0.8f, 0.2f, 0.975f);
            CuiRawImageComponent rawImage = new CuiRawImageComponent();
            //rawImage.Url = playerInfo.selectedItem.GetFormattedURL(); // TODO: Testing
            rawImage.Png = ImageLibrary.Call<string>("GetImage", playerInfo.selectedItem.GetItemDef().shortname);
            rawImage.Sprite = transparentSprite;
            elements.Add(new CuiElement
            {
                Parent = ItemInfoName,
                Components =
                    {
                        rawImage,
                        new CuiRectTransformComponent {AnchorMin = iconRect.AnchorMin, AnchorMax = iconRect.AnchorMax },
                    }
            });

            float craftTime = 1;
            originalCraftTimes.TryGetValue(playerInfo.selectedItem.itemID, out craftTime);
            PanelRect craftTimeRect = new PanelRect(0.875f, 0.925f, 0.99f, 0.99f);
            elements.Add(new CuiButton
            {
                Button = { Color = "0.2 0.2 0.2 0.8" },
                Text = { Text = (craftTime * playerInfo.selectedItem.craftRate).ToString("F0") + "s", Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FontSize = 16 },
                RectTransform = { AnchorMin = craftTimeRect.AnchorMin, AnchorMax = craftTimeRect.AnchorMax }
            }, ItemInfoName);

            PanelRect amounterPerCraftRect = new PanelRect(0.875f, 0.85f, 0.99f, 0.915f);
            elements.Add(new CuiButton
            {
                Button = { Color = "0.2 0.2 0.2 0.8" },
                Text = { Text = playerInfo.selectedItem.GetItemDef().Blueprint.amountToCreate.ToString(), Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FontSize = 16 },
                RectTransform = { AnchorMin = amounterPerCraftRect.AnchorMin, AnchorMax = amounterPerCraftRect.AnchorMax }
            }, ItemInfoName);
        }

        void RenderResourceCosts(CuiElementContainer elements, PlayerInfo playerInfo)
        {
            if (playerInfo.selectedItem == null)
                return;

            PanelRect itemCostRect = panelAnchors[ResourceCostName].rect;

            var craftInfo = playerInfo.GetCraftInfo();

            AddResourceCostColumn(elements, craftInfo.canCraft, "Amount", craftInfo.amounts, new PanelRect(0.005f, 0.005f, 0.255f, 0.995f));
            AddResourceCostColumn(elements, craftInfo.canCraft, "Item Type", craftInfo.itemTypes, new PanelRect(0.265f, 0.005f, 0.7f, 0.995f));
            AddResourceCostColumn(elements, craftInfo.canCraft, "Total", craftInfo.totals, new PanelRect(0.71f, 0.005f, 0.85f, 0.995f));
            AddResourceCostColumn(elements, craftInfo.canCraft, "Have", craftInfo.haves, new PanelRect(0.86f, 0.005f, 0.995f, 0.995f));
        }
        void AddResourceCostColumn(CuiElementContainer elements, bool canCraft, string labelName, List<string> values, PanelRect rect)
        {
            PanelRect labelRect = new PanelRect(0, 0.85f, 1f, 1).RelativeTo(rect);
            elements.Add(new CuiLabel
            {
                Text = { Text = labelName, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FontSize = 12 },
                RectTransform = { AnchorMin = labelRect.AnchorMin, AnchorMax = labelRect.AnchorMax }
            }, ResourceCostName);

            string textColor = canCraft ? "1 1 1 1" : "0.9 0.8 0.3 1";

            for (int i = 0; i < values.Count; i++)
            {
                string textValue = "";
                string buttonColor = "0.2 0.2 0.2 0.2";
                if (values.Count - 1 >= i)
                {
                    textValue = values[i];
                    buttonColor = "0.2 0.2 0.2 0.8";
                }

                float top = 1 - ((float)i / values.Count);
                float bottom = 1 - ((float)(i + 1) / values.Count);
                top -= 0.01f;
                bottom += 0.01f;
                PanelRect tRect = rect.Copy();
                tRect.top *= 0.85f;
                tRect.bottom += 0.005f;
                PanelRect costTotalValueRect = new PanelRect(0, bottom, 1, top).RelativeTo(tRect);
                elements.Add(new CuiButton
                {
                    Button = { Color = buttonColor },
                    RectTransform = { AnchorMin = costTotalValueRect.AnchorMin, AnchorMax = costTotalValueRect.AnchorMax },
                    Text = { Text = textValue, FontSize = 12, Align = TextAnchor.MiddleCenter, Color = textColor }
                }, ResourceCostName);
            }
        }

        void RenderCraftCount(CuiElementContainer elements, PlayerInfo playerInfo)
        {
            if (playerInfo.selectedItem == null)
                return;

            PanelRect minusRect = new PanelRect(0.00f, 0, 0.10f, 1f);
            elements.Add(new CuiButton
            {
                Button = { Command = $"craftamount.change { -1 }", Color = "0.6 0.6 0.6 0.85" },
                RectTransform = { AnchorMin = minusRect.AnchorMin, AnchorMax = minusRect.AnchorMax },
                Text = { Text = "-", FontSize = 30, Align = TextAnchor.MiddleCenter, Color = "1, 1, 1, 1" }
            }, CraftCountName);

            PanelRect amountRect = new PanelRect(0.11f, 0, 0.31f, 1f);
            elements.Add(new CuiButton
            {
                Button = { Color = "0.2 0.2 0.2 0.8" },
                RectTransform = { AnchorMin = amountRect.AnchorMin, AnchorMax = amountRect.AnchorMax },
                Text = { Text = playerInfo.currentCraftAmount.ToString(), FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1, 1, 1, 1" }
            }, CraftCountName);

            PanelRect plusRect = new PanelRect(0.32f, 0, 0.42f, 1f);
            elements.Add(new CuiButton
            {
                Button = { Command = $"craftamount.change { 1 }", Color = "0.6 0.6 0.6 0.85" },
                RectTransform = { AnchorMin = plusRect.AnchorMin, AnchorMax = plusRect.AnchorMax },
                Text = { Text = "+", FontSize = 30, Align = TextAnchor.MiddleCenter, Color = "1, 1, 1, 1" }
            }, CraftCountName);

            PanelRect maxPossibleRect = new PanelRect(0.43f, 0, 0.53f, 1f);
            elements.Add(new CuiButton
            {
                Button = { Command = $"craftamount.max { playerInfo.selectedItem.itemID }", Color = "0.6 0.6 0.6 0.85" },
                RectTransform = { AnchorMin = maxPossibleRect.AnchorMin, AnchorMax = maxPossibleRect.AnchorMax },
                Text = { Text = ">", FontSize = 30, Align = TextAnchor.MiddleCenter, Color = "1, 1, 1, 1" }
            }, CraftCountName);

            PanelRect craftButtonRect = new PanelRect(0.75f, 0, 0.99f, 1f);
            elements.Add(new CuiButton
            {
                Button = { Command = $"craft.begin { playerInfo.selectedItem.itemID }", Color = playerInfo.GetCraftInfo().canCraft ? "0.6 0.6 0.6 0.85" : "0.2 0.2 0.2 0.8" },
                RectTransform = { AnchorMin = craftButtonRect.AnchorMin, AnchorMax = craftButtonRect.AnchorMax },
                Text = { Text = "Craft", FontSize = 30, Align = TextAnchor.MiddleCenter, Color = "1, 1, 1, 1" }
            }, CraftCountName);
        }

        void RenderCraftQueue(CuiElementContainer elements, PlayerInfo playerInfo)
        {
            PanelRect textRect = new PanelRect(0, 0, 1, 1);
            elements.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = textRect.AnchorMin, AnchorMax = textRect.AnchorMax },
                Text = { Text = "CRAFTING QUEUE", FontSize = 58, Align = TextAnchor.MiddleLeft, Color = "1, 1, 1, 0.1" }
            }, CraftQueueName);

            BasePlayer player = playerInfo.basePlayer;
            List<ItemCraftTask> tasks = player.inventory.crafting.queue.Where(x => x.cancelled == false).ToList();
            for (int i = 0; i < tasks.Count; i++)
            {
                AddQueuedItem(elements, playerInfo, tasks[i], i, tasks.Count);
            }
        }
        void AddQueuedItem(CuiElementContainer elements, PlayerInfo player, ItemCraftTask task, int index, int taskCount)
        {
            PanelRect queueRect = panelAnchors[CraftQueueName].rect;
            float maxWidth = (queueRect.top * 1.1f - queueRect.bottom * 0.9f) * 0.8f;

            float bottom = 0.1f;
            float top = 0.9f;
            float right = 1 - ((float)index / (float)taskCount);
            float left = 1 - ((float)(index + 1) / (float)taskCount);
            float diff = right - left;
            if (diff > maxWidth)
            {
                right = 1 - ((float)index * maxWidth);
                left = right - maxWidth;
            }

            PanelRect rect = new PanelRect(left, bottom, right, top);
            ItemInfo itemInfo = FindItemInfo(task.blueprint.targetItem);
            if (itemInfo == null)
                return;

            CuiRawImageComponent rawImage = new CuiRawImageComponent();
            //rawImage.Url = itemInfo.GetFormattedURL(); // TODO: Testing
            rawImage.Png = ImageLibrary.Call<string>("GetImage", itemInfo.GetItemDef().shortname);
            rawImage.Sprite = transparentSprite;

            elements.Add(new CuiElement
            {
                Parent = CraftQueueName,
                Components =
                    {
                        rawImage,
                        new CuiRectTransformComponent {AnchorMin = rect.AnchorMin, AnchorMax = rect.AnchorMax },
                    }
            });

            elements.Add(new CuiButton
            {
                Button = { Command = $"craft.end { task.taskUID }", Color = "0 0 0 0" },
                RectTransform = { AnchorMin = rect.AnchorMin, AnchorMax = rect.AnchorMax },
                Text = { Text = "", Color = "0 0 0 0" }

            }, CraftQueueName);

            if (task.amount > 1)
            {
                PanelRect amountRect = new PanelRect(0.7f, 0f, 1f, 0.3f).RelativeTo(rect);
                elements.Add(new CuiButton
                {
                    Button = { Color = "0.8 0.8 0.8 0.8" },
                    RectTransform = { AnchorMin = amountRect.AnchorMin, AnchorMax = amountRect.AnchorMax },
                    Text = { Text = "x" + task.amount, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FontSize = 10 }
                }, CraftQueueName);
            }
        }
        void RenderCraftQueueTimer(CuiElementContainer elements, PlayerInfo playerInfo)
        {
            if (playerInfo == null || playerInfo.basePlayer.inventory.crafting.queue.Count == 0)
                return;

            ItemCraftTask task = playerInfo.basePlayer.inventory.crafting.queue.Peek();
            PanelRect textRect = new PanelRect(0.9f, 0.7f, 0.99f, 0.9f);//.RelativeTo(panelAnchors[CraftQueueName]);
            float timeRemaining = (task.endTime - 1f) - Time.realtimeSinceStartup;
            if (timeRemaining < 0)
                timeRemaining = 0;
            elements.Add(new CuiButton
            {
                Button = { Color = "0 1 0.3 1" },
                RectTransform = { AnchorMin = textRect.AnchorMin, AnchorMax = textRect.AnchorMax },
                Text = { Text = timeRemaining.ToString("F0") + "s", Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FontSize = 12 }
            }, CraftQueueName);
        }

        void RenderOverlay(PlayerInfo playerInfo, bool looting, float lootColumns = 3)
        {
            if (playerInfo == null)
                return;

            playerInfo.overlayOpen = true;
            playerInfo.overlayLooting = looting;
            playerInfo.overlayLootingColumns = lootColumns;

            foreach (var panel in overlayAnchors)
            {
                if ((looting && panel.Key == QuickCraftOverlayName) || (!looting && panel.Key == QuickCraftOverlayLootingName))
                    continue;
                RenderOverlayPanel(panel.Key, playerInfo);
            }
        }
        void CloseOverlay(PlayerInfo playerInfo)
        {
            if (playerInfo == null)
                return;

            foreach (var kvp in overlayAnchors)
            {
                CuiHelper.DestroyUi(playerInfo.basePlayer, kvp.Key);
            }
            playerInfo.overlayOpen = false;
        }
        void CloseOverlay_AllPlayers()
        {
            if (BasePlayer.activePlayerList == null || BasePlayer.activePlayerList.Count == 0)
                return;

            foreach (var p in BasePlayer.activePlayerList)
            {
                PlayerInfo playerInfo = GetPlayer(p);
                if (playerInfo != null)
                    CloseOverlay(playerInfo);
            }
        }

        void RenderOverlayPanel(string panelName, PlayerInfo playerInfo)
        {
            if (playerInfo == null || playerInfo.basePlayer == null)
            {
                PrintError("Render Overlay Panel called with a null PlayerInfo");
                return;
            }

            if (!overlayAnchors.ContainsKey(panelName))
            {
                PrintError("Cannot render overlay panel '" + panelName + "' for player '" + playerInfo.basePlayer.displayName + "'. It does not exist");
                PlayerChat(playerInfo.basePlayer, UnknownError);
                return;
            }

            if (!playerInfo.overlayOpen)
                return;


            PanelRect panelRect = overlayAnchors[panelName].rect;
            if (panelName == QuickCraftOverlayLootingName)
                panelRect = lootableOverlayAnchors[playerInfo.overlayLootingColumns];

            CuiHelper.DestroyUi(playerInfo.basePlayer, panelName);
            CuiElementContainer elements = new CuiElementContainer();

            elements.Add(new CuiPanel
            {
                Image = { Color = overlayAnchors[panelName].backgroundColor },
                RectTransform = { AnchorMin = panelRect.AnchorMin, AnchorMax = panelRect.AnchorMax }
            }, "Overlay", panelName);

            switch (panelName)
            {
                case CraftingButtonOverlayName:
                    RenderCraftingButtonBlocker(elements, playerInfo);
                    break;
                case QuickCraftOverlayName:
                case QuickCraftOverlayLootingName:
                    RenderQuickCraft(elements, playerInfo);
                    break;
            }

            CuiHelper.AddUi(playerInfo.basePlayer, elements);
        }
        void RenderCraftingButtonBlocker(CuiElementContainer elements, PlayerInfo playerInfo)
        {
            if (playerInfo == null)
                return;

            elements.Add(new CuiButton
            {
                Button = { Command = "craftui.closeoverlayopencraftui", Color = "0 0 0 0" },
                Text = { Text = "" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, CraftingButtonOverlayName);
        }
        void RenderQuickCraft(CuiElementContainer elements, PlayerInfo playerInfo)
        {
            if (playerInfo == null || itemInfoDic == null)
                return;

            List<ItemInfo> craftableItems = new List<ItemInfo>();
            foreach (var itemInfo in itemInfoDic.Values)
            {
                if (itemInfo == null) continue;
                if (!CategoryNames.Contains(itemInfo.category)) continue;
                if (itemInfo.blocked) continue;
                ItemDefinition itemDef = itemInfo.GetItemDef();
                if (itemDef == null) continue;
                if (!IncludeUncraftableItems && !itemDef.Blueprint.userCraftable)
                    continue;

                if (itemInfo.GetCraftable(playerInfo))
                    craftableItems.Add(itemInfo);
            }

            craftableItems = craftableItems.OrderByDescending(x => x.GetCraftCount(playerInfo)).Take(18).ToList();

            string parentName = playerInfo.overlayLooting ? QuickCraftOverlayLootingName : QuickCraftOverlayName;

            for (int i = 0; i < craftableItems.Count; i++)
            {
                AddQuickCraftItem(elements, i, craftableItems[i], playerInfo, parentName);
            }

            elements.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.8", AnchorMax = "1 0.97" },
                Text = { Text = "QUICK CRAFT", Align = TextAnchor.MiddleLeft, Color = "1 1 1 1", FontSize = 20 }
            }, parentName);
        }
        float AddQuickCraftItem(CuiElementContainer elements, float index, ItemInfo itemInfo, PlayerInfo playerInfo, string parentName)
        {
            const int maxRows = 4;
            const int maxColumns = 6;

            int row = (int)(index / maxColumns);
            int column = (int)index - (row * maxColumns);

            float left = (float)column / maxColumns;
            float right = (float)(column + 1) / maxColumns;
            float top = 1 - ((float)row / maxRows);
            float bottom = 1 - ((float)(row + 1) / maxRows);
            left += 0.0025f;
            right -= 0.0025f;
            top -= 0.0025f;
            bottom += 0.0025f;

            top -= 0.2f;
            bottom -= 0.2f;

            PanelRect rect = new PanelRect(left, bottom, right, top);
            string buttonColor = "0 0 0 0";

            CuiRawImageComponent rawImage = new CuiRawImageComponent();
            //rawImage.Url = itemInfo.GetFormattedURL(); // TODO: Testing
            rawImage.Png = ImageLibrary.Call<string>("GetImage", itemInfo.GetItemDef().shortname);
            rawImage.Sprite = transparentSprite;
            rawImage.Color = "1 1 1 1";

            elements.Add(new CuiElement
            {
                Parent = parentName,
                Components =
                    {
                        rawImage,
                        new CuiRectTransformComponent {AnchorMin = rect.AnchorMin, AnchorMax = rect.AnchorMax },
                    }
            });

            elements.Add(new CuiButton
            {
                Button = { Command = $"craft.begin { itemInfo.itemID }", Color = buttonColor },//, Sprite = transparentSprite },//, Color = backgroundColor },
                RectTransform =
                {
                    AnchorMin = rect.AnchorMin,
                    AnchorMax = rect.AnchorMax
                },
                Text = { Text = "", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0 0 0 0" },//, Color = "0.8 0.8 0.8 1" }
            }, parentName);


            return bottom;
        }
        #endregion

        #region Console Commands
        [ConsoleCommand("craftui.toggleoverlay")]
        void ccmdOverlayToggle(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            PlayerInfo playerInfo = GetPlayer(player);

            if (!playerInfo.overlayOpen)
                RenderOverlay(playerInfo, false);
            else
                CloseOverlay(playerInfo);
        }
        [ConsoleCommand("craftui.closeoverlay")]
        void ccmdOverlayClose(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            PlayerInfo playerInfo = GetPlayer(player);

            CloseOverlay(playerInfo);
        }
        [ConsoleCommand("craftui.closeoverlayopencraftui")]
        void ccmdCloseOverlayOpenCraftUI(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            PlayerInfo playerInfo = GetPlayer(player);

            CloseOverlay(playerInfo);
            rust.RunClientCommand(player, "inventory.toggle");
            RenderCraftUI(playerInfo);
        }

        [ConsoleCommand("craftui.toggle")]
        void ccmdCraftUIToggle(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            PlayerInfo playerInfo = GetPlayer(player);
            if (!playerInfo.uiOpen)
                RenderCraftUI(playerInfo);
            else
                CloseCraftUI(playerInfo);
        }
        [ConsoleCommand("craftui.close")]
        void ccmdCraftUIClose(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            PlayerInfo playerInfo = GetPlayer(player);
            CloseCraftUI(playerInfo);
        }

        [ConsoleCommand("category.select")]
        void ccmdCategorySelect(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            PlayerInfo playerInfo = GetPlayer(player);
            if (playerInfo == null) return;

            var categoryName = arg.FullString;

            playerInfo.selectedCategory = categoryName;

            RenderHudPanel(CategoriesName, playerInfo);
            RenderHudPanel(ItemListName, playerInfo);
        }

        [ConsoleCommand("item.select")]
        void ccmdItemSelect(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            PlayerInfo playerInfo = GetPlayer(player);
            if (playerInfo == null) return;

            int itemID = arg.GetInt(0);

            ItemInfo itemInfo = FindItemInfo(itemID);
            if (itemInfo == null) return;

            playerInfo.selectedItem = itemInfo;

            RenderHudPanel(ItemListName, playerInfo);
            RenderHudPanel(ItemInfoName, playerInfo);
            RenderHudPanel(ResourceCostName, playerInfo);
            RenderHudPanel(CraftCountName, playerInfo);
        }

        [ConsoleCommand("craftamount.change")]
        void ccmdCraftAmountChange(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            PlayerInfo playerInfo = GetPlayer(player);
            if (playerInfo == null) return;

            int amount = arg.GetInt(0);

            playerInfo.currentCraftAmount += amount;
            if (playerInfo.currentCraftAmount < 1)
                playerInfo.currentCraftAmount = 1;

            RenderHudPanel(ResourceCostName, playerInfo);
            RenderHudPanel(CraftCountName, playerInfo);
        }
        [ConsoleCommand("craftamount.max")]
        void ccmdCraftAmountMax(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            PlayerInfo playerInfo = GetPlayer(player);
            if (playerInfo == null) return;

            int itemID = arg.GetInt(0);
            ItemInfo itemInfo = FindItemInfo(itemID);
            if (itemInfo == null) return;

            int curMax = int.MaxValue;
            foreach (var ingredient in itemInfo.ingredients)
            {
                float haveValue = player.inventory.GetAmount(ingredient.ingredientID);
                int possibleMax = ingredient.amount == 0 ? 999 : Mathf.FloorToInt(haveValue / ingredient.amount);
                curMax = Mathf.Min(curMax, possibleMax);
            }
            if (curMax == int.MaxValue)
                curMax = 1;

            playerInfo.currentCraftAmount = curMax;

            RenderHudPanel(ResourceCostName, playerInfo);
            RenderHudPanel(CraftCountName, playerInfo);
        }
        [ConsoleCommand("craft.begin")]
        void ccmdCraft(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            PlayerInfo playerInfo = GetPlayer(player);
            if (playerInfo == null) return;

            int itemID = arg.GetInt(0);
            ItemInfo itemInfo = FindItemInfo(itemID);
            if (itemInfo == null) return;
            ItemDefinition itemDef = itemInfo.GetItemDef();
            if (itemDef == null) return;
            ItemBlueprint bp = itemDef.Blueprint;
            if (bp == null) return;

            if (!itemInfo.GetCraftable(playerInfo))
                return;

            bp.ingredients.Clear();
            foreach (var ingredient in itemInfo.ingredients)
            {
                if (player.inventory.GetAmount(ingredient.ingredientID) < ingredient.amount)
                    return;
                bp.ingredients.Add(ingredient.GetItemAmount());
            }

            bp.time = originalCraftTimes[itemDef.itemid] * itemInfo.craftRate;

            CraftItem(bp, player, playerInfo.currentCraftAmount, 0);

            playerInfo.currentCraftAmount = 1;

            RenderHudPanel(ResourceCostName, playerInfo);
            RenderHudPanel(CraftCountName, playerInfo);
            RenderHudPanel(CraftQueueName, playerInfo);
            RenderHudPanel(CraftQueueTimerName, playerInfo);
            RenderOverlayPanel(playerInfo.overlayLooting ? QuickCraftOverlayLootingName : QuickCraftOverlayName, playerInfo);
            //RenderOverlayPanel(QuickCraftOverlayLootingName, playerInfo);
        }
        [ConsoleCommand("craft.end")]
        void ccmdCraftEnd(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            PlayerInfo playerInfo = GetPlayer(player);
            if (playerInfo == null) return;

            int taskID = arg.GetInt(0);
            ItemCraftTask task = null;
            foreach (var t in player.inventory.crafting.queue)
            {
                if (t.taskUID == taskID)
                {
                    task = t;
                    break;
                }
            }
            if (task == null || task.blueprint == null || task.blueprint.targetItem == null)
            {
                PrintWarning("Player '" + player.displayName + "' is attempting to cancel an invalid task.");
                RenderHudPanelDelayed(refreshTime, CraftQueueName, playerInfo);
                RenderHudPanelDelayed(refreshTime, CraftQueueTimerName, playerInfo);
                return;
            }
            ItemInfo itemInfo = FindItemInfo(task.blueprint.targetItem);
            foreach (var ingredient in itemInfo.ingredients)
            {
                player.inventory.GiveItem(ItemManager.CreateByItemID(ingredient.ingredientID, (int)ingredient.amount * task.amount, 0));
            }

            player.inventory.crafting.CancelTask(taskID, false);
            RenderHudPanelDelayed(refreshTime, CraftQueueName, playerInfo);
            RenderHudPanelDelayed(refreshTime, CraftQueueTimerName, playerInfo);
        }
        #endregion

        #region Helper Functions
        Item BuildItem(int itemid, int amount, ulong skin)
        {

            if (amount < 1) amount = 1;
            Item item = ItemManager.CreateByItemID(itemid, amount, skin);
            return item;
        }
        bool CraftItem(ItemBlueprint bp, BasePlayer owner, int amount = 1, int skinID = 0)
        {
            ItemCrafter itemCrafter = owner.inventory.crafting;
            itemCrafter.taskUID = itemCrafter.taskUID + 1;
            ItemCraftTask itemCraftTask = Facepunch.Pool.Get<ItemCraftTask>();
            itemCraftTask.blueprint = bp;
            CollectIngredients(itemCrafter, bp, itemCraftTask, amount, owner);
            itemCraftTask.endTime = 0f;
            itemCraftTask.taskUID = owner.inventory.crafting.taskUID;
            itemCraftTask.owner = owner;
            itemCraftTask.instanceData = null;
            itemCraftTask.amount = amount;
            itemCraftTask.skinID = skinID;
            customItemCrafts.Add(itemCraftTask.taskUID);

            object[] objArray = new object[] { itemCraftTask, owner, null };
            object obj = Interface.CallHook("OnItemCraft", objArray);
            if (obj is bool)
            {
                return (bool)obj;
            }

            PlayerInfo playerInfo = GetPlayer(owner);
            RenderHudPanelDelayed(refreshTime, CraftQueueName, playerInfo);
            RenderHudPanelDelayed(refreshTime, CraftQueueTimerName, playerInfo);
            RenderHudPanelDelayed(refreshTime, ResourceCostName, playerInfo);

            owner.inventory.crafting.queue.Enqueue(itemCraftTask);
            if (itemCraftTask.owner != null)
            {
                if (!nullBlueprintSet.Contains(itemCraftTask.blueprint.targetItem.itemid))
                    itemCraftTask.owner.Command("note.craft_add", new object[] { itemCraftTask.taskUID, itemCraftTask.blueprint.targetItem.itemid, amount });
            }
            return true;
        }
        void CollectIngredient(ItemCrafter crafter, int item, int amount, List<Item> collect)
        {
            foreach (ItemContainer container in crafter.containers)
            {
                amount = amount - container.Take(collect, item, amount);
                if (amount > 0)
                {
                    continue;
                }
                break;
            }
        }
        void CollectIngredients(ItemCrafter crafter, ItemBlueprint bp, ItemCraftTask task, int amount = 1, BasePlayer player = null)
        {
            List<Item> items = new List<Item>();
            foreach (ItemAmount ingredient in bp.ingredients)
            {
                this.CollectIngredient(crafter, ingredient.itemid, (int)ingredient.amount * amount, items);
            }
            task.potentialOwners = new List<ulong>();
            foreach (Item item in items)
            {
                item.CollectedForCrafting(player);
                if (task.potentialOwners.Contains(player.userID))
                {
                    continue;
                }
                task.potentialOwners.Add(player.userID);
            }
            task.takenItems = items;
        }

        ItemDefinition DisplayNameToItemDef(BasePlayer player, string displayName)
        {
            string shortName = string.Empty;
            if (!displaynameToShortname.TryGetValue(displayName.ToLower(), out shortName))
            {
                PlayerChat(player, Invalid, displayName, "Item Name");
                return null;
            }

            ItemDefinition itemDef = ItemManager.FindItemDefinition(shortName);

            if (itemDef == null)
            {
                PlayerChat(player, UnknownError);
                return null;
            }

            return itemDef;
        }

        void PlayerChat(BasePlayer player, string key, params object[] args)
        {
            SendReply(player, Lang(key, player.UserIDString, args));
        }
        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        class UStopWatch
        {
            float startTime;
            int startFrame;
            float stopTime;
            int stopFrame;

            bool _isRunning;
            public bool isRunning { get { return _isRunning; } private set { _isRunning = value; } }

            public float elapsedSeconds { get { return (isRunning ? Time.realtimeSinceStartup : stopTime) - startTime; } }
            public int elapsedMilliseconds { get { return Mathf.RoundToInt(elapsedSeconds / 1000f); } }
            public int elapsedFrames { get { return (isRunning ? Time.frameCount : stopFrame) - startFrame; } }

            public UStopWatch(bool startNow = true)
            {
                if (startNow)
                    Start();
            }

            public void Start()
            {
                Reset();
                _isRunning = true;
            }
            public void Stop()
            {
                stopTime = Time.realtimeSinceStartup;
                stopFrame = Time.frameCount;

                _isRunning = false;
            }
            public void Reset()
            {
                startTime = Time.realtimeSinceStartup;
                startFrame = Time.frameCount;
                stopTime = Time.realtimeSinceStartup;
                stopFrame = Time.frameCount;
            }
        }
        #endregion

        #region ItemidToURL
        string GetItemIconURL(ItemDefinition itemDef)
        {
            return GetItemIconURL(itemDef.shortname);
        }
        string GetItemIconURL(string itemID)
        {
            string url = string.Empty;
            url = ImageLibrary.Call<string>("GetImageURL", itemID, 0);
            if (url == "null" || url == null){url = "";}
            return url;
        }
        readonly Dictionary<int, string> idToURL = new Dictionary<int, string>()
        {
            { 2033918259, "http://vignette2.wikia.nocookie.net/play-rust/images/d/d4/Python_Revolver_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 3655341, "http://vignette4.wikia.nocookie.net/play-rust/images/f/f2/Wood_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 547302405, "http://vignette3.wikia.nocookie.net/play-rust/images/f/f2/Water_Jug_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 112903447, "http://vignette3.wikia.nocookie.net/play-rust/images/7/7f/Water_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 889398893, "http://vignette3.wikia.nocookie.net/play-rust/images/2/22/Sulfur_Ore_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -891243783, "http://vignette4.wikia.nocookie.net/play-rust/images/3/32/Sulfur_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -892070738, "http://vignette4.wikia.nocookie.net/play-rust/images/8/85/Stones_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1616524891, "http://vignette2.wikia.nocookie.net/play-rust/images/9/97/Small_Stocking_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1623330855, "http://vignette1.wikia.nocookie.net/play-rust/images/6/6a/SUPER_Stocking_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 865679437, "http://vignette3.wikia.nocookie.net/play-rust/images/7/70/Small_Trout_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1001265731, "http://vignette1.wikia.nocookie.net/play-rust/images/f/fa/Wolf_Skull_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 960793436, "http://vignette3.wikia.nocookie.net/play-rust/images/2/24/Human_Skull_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 2007564590, "http://vignette4.wikia.nocookie.net/play-rust/images/4/4f/Santa_Hat_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1916127949, "http://vignette2.wikia.nocookie.net/play-rust/images/c/ce/Salt_Water_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 540154065, "http://vignette3.wikia.nocookie.net/play-rust/images/a/ac/Research_Paper_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 466113771, "http://vignette2.wikia.nocookie.net/play-rust/images/6/66/Pumpkin_Seed_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -225085592, "http://vignette3.wikia.nocookie.net/play-rust/images/4/4c/Pumpkin_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1725510067, "http://vignette2.wikia.nocookie.net/play-rust/images/d/da/Small_Present_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -2130280721, "http://vignette3.wikia.nocookie.net/play-rust/images/6/6b/Medium_Present_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1732316031, "http://vignette1.wikia.nocookie.net/play-rust/images/9/99/Large_Present_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 640562379, "http://vignette1.wikia.nocookie.net/play-rust/images/6/61/Pookie_Bear_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 843418712, "http://vignette3.wikia.nocookie.net/play-rust/images/a/a8/Mushroom_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 88869913, "http://vignette1.wikia.nocookie.net/play-rust/images/7/70/Minnows_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -758925787, "http://vignette2.wikia.nocookie.net/play-rust/images/c/c9/Pump_Jack_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 374890416, "http://vignette4.wikia.nocookie.net/play-rust/images/a/a1/High_Quality_Metal_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1059362949, "http://vignette1.wikia.nocookie.net/play-rust/images/0/0a/Metal_Ore_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 688032252, "http://vignette4.wikia.nocookie.net/play-rust/images/7/74/Metal_Fragments_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 431617507, "http://vignette3.wikia.nocookie.net/play-rust/images/f/f2/Spoiled_Wolf_Meat_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 179448791, "http://vignette2.wikia.nocookie.net/play-rust/images/5/5c/Raw_Wolf_Meat_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1691991080, "http://vignette3.wikia.nocookie.net/play-rust/images/1/16/Cooked_Wolf_Meat_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1714986849, "http://vignette4.wikia.nocookie.net/play-rust/images/b/b6/Burned_Wolf_Meat_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -253819519, "http://vignette4.wikia.nocookie.net/play-rust/images/7/78/Pork_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 991728250, "http://vignette2.wikia.nocookie.net/play-rust/images/d/dc/Cooked_Pork_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 968732481, "http://vignette2.wikia.nocookie.net/play-rust/images/6/61/Burned_Pork_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1325935999, "http://vignette4.wikia.nocookie.net/play-rust/images/c/c8/Bear_Meat_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -2043730634, "http://vignette3.wikia.nocookie.net/play-rust/images/1/17/Bear_Meat_Cooked_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -2066726403, "http://vignette2.wikia.nocookie.net/play-rust/images/c/c2/Burnt_Bear_Meat_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 50834473, "http://vignette2.wikia.nocookie.net/play-rust/images/9/9a/Leather_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 661790782, "http://vignette3.wikia.nocookie.net/play-rust/images/b/b7/Spoiled_Human_Meat_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -642008142, "http://vignette1.wikia.nocookie.net/play-rust/images/2/26/Raw_Human_Meat_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -991829475, "http://vignette4.wikia.nocookie.net/play-rust/images/d/d2/Cooked_Human_Meat_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1014825244, "http://vignette3.wikia.nocookie.net/play-rust/images/f/f0/Burned_Human_Meat_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 2133577942, "http://vignette1.wikia.nocookie.net/play-rust/images/8/80/High_Quality_Metal_Ore_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 583506109, "http://vignette1.wikia.nocookie.net/play-rust/images/1/1c/Hemp_Seed_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 3175989, "http://vignette3.wikia.nocookie.net/play-rust/images/6/66/Glue_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 97513422, "http://vignette4.wikia.nocookie.net/play-rust/images/5/57/Flare_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -533484654, "http://vignette1.wikia.nocookie.net/play-rust/images/1/1a/Raw_Fish_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -2078972355, "http://vignette3.wikia.nocookie.net/play-rust/images/8/8b/Cooked_Fish_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1034048911, "http://vignette1.wikia.nocookie.net/play-rust/images/d/d5/Animal_Fat_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1983936587, "http://vignette1.wikia.nocookie.net/play-rust/images/3/3c/Crude_Oil_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 583366917, "http://vignette1.wikia.nocookie.net/play-rust/images/2/29/Corn_Seed_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 3059624, "http://vignette1.wikia.nocookie.net/play-rust/images/0/0a/Corn_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 94756378, "http://vignette1.wikia.nocookie.net/play-rust/images/f/f7/Cloth_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -341443994, "http://vignette1.wikia.nocookie.net/play-rust/images/4/45/Chocolate_Bar_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -726947205, "http://vignette2.wikia.nocookie.net/play-rust/images/7/7d/Spoiled_Chicken_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1658459025, "http://vignette3.wikia.nocookie.net/play-rust/images/8/81/Raw_Chicken_Breast_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1734319168, "http://vignette3.wikia.nocookie.net/play-rust/images/6/6f/Cooked_Chicken_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1711323399, "http://vignette4.wikia.nocookie.net/play-rust/images/b/bb/Burned_Chicken_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1436001773, "http://vignette2.wikia.nocookie.net/play-rust/images/a/ad/Charcoal_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 523409530, "http://vignette1.wikia.nocookie.net/play-rust/images/2/2c/Candy_Cane_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1050986417, "http://vignette3.wikia.nocookie.net/play-rust/images/f/f2/Empty_Tuna_Can_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 2080339268, "http://vignette1.wikia.nocookie.net/play-rust/images/8/88/Empty_Can_Of_Beans_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -2079677721, "http://vignette4.wikia.nocookie.net/play-rust/images/f/fe/Cactus_Flesh_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -365801095, "http://vignette2.wikia.nocookie.net/play-rust/images/0/01/Bone_Fragments_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1887162396, "http://vignette3.wikia.nocookie.net/play-rust/images/8/83/Blueprint_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1386464949, "http://vignette3.wikia.nocookie.net/play-rust/images/a/ac/Bleach_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 2021568998, "http://vignette1.wikia.nocookie.net/play-rust/images/8/8c/Battery_-_Small_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1565095136, "http://vignette2.wikia.nocookie.net/play-rust/images/b/bf/Rotten_Apple_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1471284746, "http://vignette2.wikia.nocookie.net/play-rust/images/e/eb/Tech_Trash_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 3552619, "http://vignette4.wikia.nocookie.net/play-rust/images/1/12/Tarp_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1490499512, "http://vignette3.wikia.nocookie.net/play-rust/images/0/07/Targeting_Computer_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -892259869, "http://vignette1.wikia.nocookie.net/play-rust/images/d/d5/Sticks_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1835797460, "http://vignette2.wikia.nocookie.net/play-rust/images/3/3d/Metal_Spring_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -2092529553, "http://vignette3.wikia.nocookie.net/play-rust/images/d/d8/SMG_Body_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1617374968, "http://vignette3.wikia.nocookie.net/play-rust/images/3/39/Sheet_Metal_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -419069863, "http://vignette1.wikia.nocookie.net/play-rust/images/2/29/Sewing_Kit_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1223860752, "http://vignette2.wikia.nocookie.net/play-rust/images/a/ac/Semi_Automatic_Body_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 3506418, "http://vignette1.wikia.nocookie.net/play-rust/images/1/15/Rope_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -847065290, "http://vignette3.wikia.nocookie.net/play-rust/images/a/a5/Road_Signs_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1939428458, "http://vignette2.wikia.nocookie.net/play-rust/images/0/08/Rifle_Body_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1974032895, "http://vignette4.wikia.nocookie.net/play-rust/images/a/a8/Empty_Propane_Tank_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1057402571, "http://vignette2.wikia.nocookie.net/play-rust/images/4/4a/Metal_Pipe_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1567404401, "http://vignette4.wikia.nocookie.net/play-rust/images/9/9b/Metal_Blade_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 193190034, "http://vignette1.wikia.nocookie.net/play-rust/images/c/c6/M249_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1066276787, "http://vignette2.wikia.nocookie.net/play-rust/images/6/6a/Hazmat_Pants_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1133046397, "http://vignette3.wikia.nocookie.net/play-rust/images/2/23/Hazmat_Jacket_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1079752220, "http://vignette2.wikia.nocookie.net/play-rust/images/5/53/Hazmat_Helmet_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1057685737, "http://vignette3.wikia.nocookie.net/play-rust/images/a/aa/Hazmat_Gloves_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1078788046, "http://vignette4.wikia.nocookie.net/play-rust/images/8/8a/Hazmat_Boots_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 277631078, "http://vignette2.wikia.nocookie.net/play-rust/images/f/f2/Wind_Turbine_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 98228420, "http://vignette2.wikia.nocookie.net/play-rust/images/7/72/Gears_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1891056868, "http://vignette1.wikia.nocookie.net/play-rust/images/f/f8/Duct_Tape_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1200628767, "http://vignette2.wikia.nocookie.net/play-rust/images/8/84/Door_Key_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 3059095, "http://vignette3.wikia.nocookie.net/play-rust/images/f/ff/Coal_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1300054961, "http://vignette2.wikia.nocookie.net/play-rust/images/2/24/CCTV_Camera_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1594947829, "http://vignette4.wikia.nocookie.net/play-rust/images/8/80/Smoke_Rocket_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 110547964, "http://vignette1.wikia.nocookie.net/play-rust/images/4/48/Torch_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 3506021, "http://vignette2.wikia.nocookie.net/play-rust/images/f/ff/Rock_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1192532973, "http://vignette4.wikia.nocookie.net/play-rust/images/b/bc/Water_Bucket_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 3387378, "http://vignette3.wikia.nocookie.net/play-rust/images/d/d5/Note_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -217113639, "http://vignette4.wikia.nocookie.net/play-rust/images/b/bb/Acoustic_Guitar_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 191795897, "http://vignette4.wikia.nocookie.net/play-rust/images/3/3f/Double_Barrel_Shotgun_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1745053053, "http://vignette3.wikia.nocookie.net/play-rust/images/8/8d/Semi-Automatic_Rifle_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -141135377, "http://vignette4.wikia.nocookie.net/play-rust/images/9/9c/4x_Zoom_Scope_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -388967316, "http://vignette3.wikia.nocookie.net/play-rust/images/7/77/Salvaged_Sword_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1775234707, "http://vignette1.wikia.nocookie.net/play-rust/images/7/7e/Salvaged_Cleaver_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1379225193, "http://vignette2.wikia.nocookie.net/play-rust/images/b/b5/Eoka_Pistol_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1569280852, "http://vignette2.wikia.nocookie.net/play-rust/images/3/38/Muzzle_Brake_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1569356508, "http://vignette2.wikia.nocookie.net/play-rust/images/7/7d/Muzzle_Boost_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -2094080303, "http://vignette3.wikia.nocookie.net/play-rust/images/c/c0/MP5A4_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 825308669, "http://vignette3.wikia.nocookie.net/play-rust/images/3/34/Machete_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 146685185, "http://vignette4.wikia.nocookie.net/play-rust/images/3/34/Longsword_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 776005741, "http://vignette3.wikia.nocookie.net/play-rust/images/c/c7/Bone_Knife_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1229879204, "http://vignette3.wikia.nocookie.net/play-rust/images/0/0d/Weapon_Flashlight_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1045869440, "http://vignette3.wikia.nocookie.net/play-rust/images/5/55/Flame_Thrower_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -853695669, "http://vignette2.wikia.nocookie.net/play-rust/images/2/25/Hunting_Bow_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1556671423, "http://vignette4.wikia.nocookie.net/play-rust/images/2/27/Wooden_Window_Bars_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -402507101, "http://vignette1.wikia.nocookie.net/play-rust/images/e/eb/Reinforced_Window_Bars_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1021702157, "http://vignette2.wikia.nocookie.net/play-rust/images/f/fe/Metal_Window_Bars_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1175970190, "http://vignette4.wikia.nocookie.net/play-rust/images/c/c1/Shop_Front_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -378017204, "http://vignette3.wikia.nocookie.net/play-rust/images/2/2a/Chainlink_Fence_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 995306285, "http://vignette3.wikia.nocookie.net/play-rust/images/7/7a/Chainlink_Fence_Gate_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -427925529, "http://vignette4.wikia.nocookie.net/play-rust/images/f/f6/Prison_Cell_Wall_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 562888306, "http://vignette4.wikia.nocookie.net/play-rust/images/3/30/Prison_Cell_Gate_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 486166145, "http://vignette3.wikia.nocookie.net/play-rust/images/2/2b/Wood_Shutters_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -529054134, "http://vignette1.wikia.nocookie.net/play-rust/images/8/88/Metal_Vertical_embrasure_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -529054135, "http://vignette3.wikia.nocookie.net/play-rust/images/5/5d/Metal_horizontal_embrasure_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1908195100, "http://vignette1.wikia.nocookie.net/play-rust/images/9/9e/Lock_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1849912854, "http://vignette1.wikia.nocookie.net/play-rust/images/7/7c/Ladder_Hatch_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1722829188, "http://vignette4.wikia.nocookie.net/play-rust/images/4/48/Floor_Grill_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1456441506, "http://vignette1.wikia.nocookie.net/play-rust/images/7/7e/Wooden_Door_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1571725662, "http://vignette2.wikia.nocookie.net/play-rust/images/b/bc/Armored_Door_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -2104481870, "http://vignette3.wikia.nocookie.net/play-rust/images/8/83/Sheet_Metal_Door_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1575287163, "http://vignette2.wikia.nocookie.net/play-rust/images/4/41/Wood_Double_Door_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -933236257, "http://vignette3.wikia.nocookie.net/play-rust/images/c/c1/Armored_Double_Door_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1598790097, "http://vignette3.wikia.nocookie.net/play-rust/images/1/14/Sheet_Metal_Double_Door_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1257201758, "http://vignette2.wikia.nocookie.net/play-rust/images/5/57/Tool_Cupboard_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -307490664, "http://vignette2.wikia.nocookie.net/play-rust/images/b/ba/Building_Plan_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1840561315, "http://vignette3.wikia.nocookie.net/play-rust/images/6/6e/Water_Purifier_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1628526499, "http://vignette4.wikia.nocookie.net/play-rust/images/e/e2/Water_Barrel_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1369769822, "http://vignette2.wikia.nocookie.net/play-rust/images/9/9d/Survival_Fish_Trap_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -698499648, "http://vignette1.wikia.nocookie.net/play-rust/images/7/70/Small_Wooden_Sign_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -357728804, "http://vignette2.wikia.nocookie.net/play-rust/images/c/c3/Wooden_Sign_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -705305612, "http://vignette4.wikia.nocookie.net/play-rust/images/b/bc/Large_Wooden_Sign_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1962514734, "http://vignette3.wikia.nocookie.net/play-rust/images/6/6e/Huge_Wooden_Sign_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 644359987, "http://vignette1.wikia.nocookie.net/play-rust/images/f/fa/Two_Sided_Town_Sign_Post_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1224714193, "http://vignette1.wikia.nocookie.net/play-rust/images/6/62/One_Sided_Town_Sign_Post_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -163742043, "http://vignette1.wikia.nocookie.net/play-rust/images/1/11/Single_Sign_Post_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -587434450, "http://vignette2.wikia.nocookie.net/play-rust/images/5/5e/Double_Sign_Post_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1579245182, "http://vignette2.wikia.nocookie.net/play-rust/images/1/16/Large_Banner_on_pole_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1455694274, "http://vignette2.wikia.nocookie.net/play-rust/images/9/95/XXL_Picture_Frame_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1338515426, "http://vignette2.wikia.nocookie.net/play-rust/images/b/bf/XL_Picture_Frame_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 2117976603, "http://vignette1.wikia.nocookie.net/play-rust/images/6/65/Tall_Picture_Frame_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 385802761, "http://vignette1.wikia.nocookie.net/play-rust/images/5/50/Portrait_Picture_Frame_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -626812403, "http://vignette3.wikia.nocookie.net/play-rust/images/8/87/Landscape_Picture_Frame_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -632459882, "http://vignette3.wikia.nocookie.net/play-rust/images/4/4f/Two_Sided_Ornate_Hanging_Sign_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1498516223, "http://vignette4.wikia.nocookie.net/play-rust/images/d/df/Two_Sided_Hanging_Sign_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1628490888, "http://vignette3.wikia.nocookie.net/play-rust/images/2/29/Large_Banner_Hanging_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 2057749608, "http://vignette2.wikia.nocookie.net/play-rust/images/a/a5/Salvaged_Shelves_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1987447227, "http://vignette2.wikia.nocookie.net/play-rust/images/2/21/Research_Table_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 2069925558, "http://vignette1.wikia.nocookie.net/play-rust/images/6/60/Reactive_Target_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 148953073, "http://vignette3.wikia.nocookie.net/play-rust/images/a/a7/Small_Planter_Box_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 142147109, "http://vignette1.wikia.nocookie.net/play-rust/images/3/35/Large_Planter_Box_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 107868, "http://vignette4.wikia.nocookie.net/play-rust/images/c/c8/Paper_Map_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1278649848, "http://vignette1.wikia.nocookie.net/play-rust/images/9/92/Jack_O_Lantern_Happy_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1284735799, "http://vignette4.wikia.nocookie.net/play-rust/images/9/96/Jack_O_Lantern_Angry_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1598149413, "http://vignette3.wikia.nocookie.net/play-rust/images/e/ee/Large_Furnace_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -2095387015, "http://vignette3.wikia.nocookie.net/play-rust/images/4/43/Ceiling_Light_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 106434956, "http://vignette3.wikia.nocookie.net/play-rust/images/9/96/Paper_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1580059655, "http://vignette3.wikia.nocookie.net/play-rust/images/1/17/Gun_Powder_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 28178745, "http://vignette4.wikia.nocookie.net/play-rust/images/2/26/Low_Grade_Fuel_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1755466030, "http://vignette2.wikia.nocookie.net/play-rust/images/4/47/Explosives_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1660607208, "http://vignette1.wikia.nocookie.net/play-rust/images/5/57/Longsleeve_T-Shirt_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -864578046, "http://vignette2.wikia.nocookie.net/play-rust/images/6/62/T-Shirt_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1659202509, "http://vignette4.wikia.nocookie.net/play-rust/images/1/1e/Tank_Top_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 24576628, "http://vignette1.wikia.nocookie.net/play-rust/images/8/8c/Shirt_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 102672084, "http://vignette4.wikia.nocookie.net/play-rust/images/7/7f/Hide_Poncho_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -459156023, "http://vignette4.wikia.nocookie.net/play-rust/images/4/46/Shorts_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -135651869, "http://vignette2.wikia.nocookie.net/play-rust/images/e/e4/Hide_Pants_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 106433500, "http://vignette1.wikia.nocookie.net/play-rust/images/3/3f/Pants_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1767561705, "http://vignette4.wikia.nocookie.net/play-rust/images/e/e5/Burlap_Trousers_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 569119686, "http://vignette1.wikia.nocookie.net/play-rust/images/1/14/Bone_Armor_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1211618504, "http://vignette1.wikia.nocookie.net/play-rust/images/b/b5/Hoodie_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1666761111, "http://vignette3.wikia.nocookie.net/play-rust/images/c/c0/Hide_Vest_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -132588262, "http://vignette1.wikia.nocookie.net/play-rust/images/9/91/Hide_Skirt_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -148163128, "http://vignette1.wikia.nocookie.net/play-rust/images/5/57/Hide_Boots_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1178289187, "http://vignette2.wikia.nocookie.net/play-rust/images/c/ca/Bone_Helmet_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1035315940, "http://vignette2.wikia.nocookie.net/play-rust/images/c/c4/Burlap_Headwrap_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1397343301, "http://vignette2.wikia.nocookie.net/play-rust/images/8/88/Boonie_Hat_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 936777834, "http://vignette2.wikia.nocookie.net/play-rust/images/2/25/Hide_Halterneck_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 707432758, "http://vignette4.wikia.nocookie.net/play-rust/images/1/10/Burlap_Shoes_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 707427396, "http://vignette2.wikia.nocookie.net/play-rust/images/d/d7/Burlap_Shirt_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1289478934, "http://vignette2.wikia.nocookie.net/play-rust/images/9/9b/Stone_Hatchet_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 789892804, "http://vignette2.wikia.nocookie.net/play-rust/images/7/77/Stone_Pick_Axe_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1224598842, "http://vignette4.wikia.nocookie.net/play-rust/images/5/57/Hammer_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 498591726, "http://vignette1.wikia.nocookie.net/play-rust/images/6/6c/Timed_Explosive_Charge_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1295154089, "http://vignette2.wikia.nocookie.net/play-rust/images/0/0b/Satchel_Charge_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1035059994, "http://vignette1.wikia.nocookie.net/play-rust/images/2/2f/12_Gauge_Buckshot_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1819281075, "http://vignette3.wikia.nocookie.net/play-rust/images/1/1a/12_Gauge_Slug_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -533875561, "http://vignette2.wikia.nocookie.net/play-rust/images/9/9b/Pistol_Bullet_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 815896488, "http://vignette1.wikia.nocookie.net/play-rust/images/4/49/5.56_Rifle_Ammo_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 523855532, "http://vignette2.wikia.nocookie.net/play-rust/images/3/36/Hazmat_Suit_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1685058759, "http://vignette2.wikia.nocookie.net/play-rust/images/0/0e/Anti-Radiation_Pills_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 93029210, "http://vignette2.wikia.nocookie.net/play-rust/images/d/dc/Apple_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1461508848, "http://vignette3.wikia.nocookie.net/play-rust/images/d/d1/Assault_Rifle_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 563023711, "http://vignette2.wikia.nocookie.net/play-rust/images/f/f9/Auto_Turret_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -337261910, "http://vignette3.wikia.nocookie.net/play-rust/images/f/f8/Bandage_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -46188931, "http://vignette3.wikia.nocookie.net/play-rust/images/9/9f/Bandana_Mask_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1024486167, "http://vignette1.wikia.nocookie.net/play-rust/images/7/7b/Barbed_Wooden_Barricade_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 696727039, "http://vignette2.wikia.nocookie.net/play-rust/images/7/77/Baseball_Cap_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 384204160, "http://vignette1.wikia.nocookie.net/play-rust/images/b/be/Beancan_Grenade_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 97409, "http://vignette3.wikia.nocookie.net/play-rust/images/f/fe/Bed_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1406876421, "http://vignette1.wikia.nocookie.net/play-rust/images/4/43/Blue_Beenie_Hat_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1611480185, "http://vignette4.wikia.nocookie.net/play-rust/images/6/6f/Black_Raspberries_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 93832698, "http://vignette3.wikia.nocookie.net/play-rust/images/a/a3/Blood_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1063412582, "http://vignette1.wikia.nocookie.net/play-rust/images/f/f8/Blueberries_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -55660037, "http://vignette1.wikia.nocookie.net/play-rust/images/5/55/Bolt_Action_Rifle_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 919780768, "http://vignette4.wikia.nocookie.net/play-rust/images/1/19/Bone_Club_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 2107229499, "http://vignette1.wikia.nocookie.net/play-rust/images/b/b3/Boots_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 68998734, "http://vignette2.wikia.nocookie.net/play-rust/images/f/f5/Bota_Bag_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1260209393, "http://vignette1.wikia.nocookie.net/play-rust/images/a/a5/Bucket_Helmet_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1342405573, "http://vignette3.wikia.nocookie.net/play-rust/images/0/0e/Camera_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -139769801, "http://vignette4.wikia.nocookie.net/play-rust/images/3/35/Camp_Fire_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1043746011, "http://vignette2.wikia.nocookie.net/play-rust/images/e/e5/Can_of_Beans_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -171664558, "http://vignette4.wikia.nocookie.net/play-rust/images/2/2d/Can_of_Tuna_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1381682752, "http://vignette1.wikia.nocookie.net/play-rust/images/a/ad/Candle_Hat_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -975723312, "http://vignette2.wikia.nocookie.net/play-rust/images/0/0c/Code_Lock_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -2128719593, "http://vignette4.wikia.nocookie.net/play-rust/images/4/44/Coffee_Can_Helmet_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 498312426, "http://vignette2.wikia.nocookie.net/play-rust/images/b/b3/Concrete_Barricade_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 2123300234, "http://vignette3.wikia.nocookie.net/play-rust/images/2/23/Crossbow_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 109552593, "http://vignette1.wikia.nocookie.net/play-rust/images/9/95/Custom_SMG_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 805088543, "http://vignette3.wikia.nocookie.net/play-rust/images/3/31/Explosive_5.56_Rifle_Ammo_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1308622549, "http://vignette3.wikia.nocookie.net/play-rust/images/5/52/F1_Grenade_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -505639592, "http://vignette4.wikia.nocookie.net/play-rust/images/e/e3/Furnace_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 718197703, "http://vignette3.wikia.nocookie.net/play-rust/images/6/6c/Granola_Bar_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 2115555558, "http://vignette2.wikia.nocookie.net/play-rust/images/0/0d/Handmade_Shell_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 698310895, "http://vignette1.wikia.nocookie.net/play-rust/images/0/06/Hatchet_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -496055048, "http://vignette1.wikia.nocookie.net/play-rust/images/b/b6/High_External_Stone_Wall_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -57285700, "http://vignette2.wikia.nocookie.net/play-rust/images/5/53/High_External_Wooden_Gate_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1792066367, "http://vignette3.wikia.nocookie.net/play-rust/images/9/96/High_External_Wooden_Wall_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1280058093, "http://vignette3.wikia.nocookie.net/play-rust/images/e/e5/High_Velocity_Arrow_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 542276424, "http://vignette3.wikia.nocookie.net/play-rust/images/f/f4/High_Velocity_Rocket_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -465236267, "http://vignette4.wikia.nocookie.net/play-rust/images/4/45/Holosight_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1152393492, "http://vignette2.wikia.nocookie.net/play-rust/images/d/df/HV_5.56_Rifle_Ammo_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -422893115, "http://vignette4.wikia.nocookie.net/play-rust/images/e/e5/HV_Pistol_Ammo_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 997973965, "http://vignette1.wikia.nocookie.net/play-rust/images/5/52/Improvised_Balaclava_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 449771810, "http://vignette2.wikia.nocookie.net/play-rust/images/e/e1/Incendiary_5.56_Rifle_Ammo_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1621541165, "http://vignette4.wikia.nocookie.net/play-rust/images/3/31/Incendiary_Pistol_Bullet_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1436532208, "http://vignette1.wikia.nocookie.net/play-rust/images/f/f9/Incendiary_Rocket_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1167640370, "http://vignette2.wikia.nocookie.net/play-rust/images/8/8b/Jacket_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 255101535, "http://vignette3.wikia.nocookie.net/play-rust/images/8/83/Land_Mine_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -51678842, "http://vignette4.wikia.nocookie.net/play-rust/images/4/46/Lantern_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -789202811, "http://vignette3.wikia.nocookie.net/play-rust/images/9/99/Large_Medkit_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1817873886, "http://vignette2.wikia.nocookie.net/play-rust/images/3/35/Large_Water_Catcher_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 271534758, "http://vignette1.wikia.nocookie.net/play-rust/images/b/b2/Large_Wood_Box_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 115739308, "http://vignette2.wikia.nocookie.net/play-rust/images/a/a1/Leather_Gloves_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1716193401, "http://vignette1.wikia.nocookie.net/play-rust/images/d/d9/LR-300_Assault_Rifle_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 371156815, "http://vignette2.wikia.nocookie.net/play-rust/images/4/43/M92_Pistol_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 3343606, "http://vignette3.wikia.nocookie.net/play-rust/images/4/4d/Mace_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 516382256, "http://vignette1.wikia.nocookie.net/play-rust/images/8/8e/Weapon_Lasersight_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 586484018, "http://vignette3.wikia.nocookie.net/play-rust/images/9/99/Medical_Syringe_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 504904386, "http://vignette3.wikia.nocookie.net/play-rust/images/b/bb/Metal_Barricade_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1265861812, "http://vignette3.wikia.nocookie.net/play-rust/images/9/9d/Metal_Chest_Plate_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -46848560, "http://vignette1.wikia.nocookie.net/play-rust/images/1/1f/Metal_Facemask_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -450738836, "http://vignette1.wikia.nocookie.net/play-rust/images/1/1b/Miners_Hat_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1411620422, "http://vignette1.wikia.nocookie.net/play-rust/images/b/b8/Mining_Quarry_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -578028723, "http://vignette4.wikia.nocookie.net/play-rust/images/8/86/Pick_Axe_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1009492144, "http://vignette3.wikia.nocookie.net/play-rust/images/6/60/Pump_Shotgun_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1026117678, "http://vignette1.wikia.nocookie.net/play-rust/images/3/3b/Repair_Bench_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -930579334, "http://vignette1.wikia.nocookie.net/play-rust/images/5/58/Revolver_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 340009023, "http://vignette4.wikia.nocookie.net/play-rust/images/4/4e/Riot_Helmet_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -288010497, "http://vignette1.wikia.nocookie.net/play-rust/images/8/84/Road_Sign_Jacket_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1595790889, "http://vignette3.wikia.nocookie.net/play-rust/images/3/31/Road_Sign_Kilt_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1578894260, "http://vignette1.wikia.nocookie.net/play-rust/images/9/95/Rocket_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 649603450, "http://vignette3.wikia.nocookie.net/play-rust/images/0/06/Rocket_Launcher_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 790921853, "http://vignette1.wikia.nocookie.net/play-rust/images/c/c9/Salvaged_Axe_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1976561211, "http://vignette2.wikia.nocookie.net/play-rust/images/f/f8/Salvaged_Hammer_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1440143841, "http://vignette1.wikia.nocookie.net/play-rust/images/e/e1/Salvaged_Icepick_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1221200300, "http://vignette4.wikia.nocookie.net/play-rust/images/a/a7/Sandbag_Barricade_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 548699316, "http://vignette2.wikia.nocookie.net/play-rust/images/6/6b/Semi-Automatic_Pistol_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1213686767, "http://vignette3.wikia.nocookie.net/play-rust/images/9/9f/Silencer_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1253290621, "http://vignette2.wikia.nocookie.net/play-rust/images/b/be/Sleeping_Bag_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 470729623, "http://vignette2.wikia.nocookie.net/play-rust/images/a/ac/Small_Oil_Refinery_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1051155022, "http://vignette2.wikia.nocookie.net/play-rust/images/5/53/Small_Stash_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 927253046, "http://vignette2.wikia.nocookie.net/play-rust/images/f/fc/Small_Water_Bottle_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1824679850, "http://vignette2.wikia.nocookie.net/play-rust/images/0/04/Small_Water_Catcher_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1046072789, "http://vignette1.wikia.nocookie.net/play-rust/images/b/b0/Snap_Trap_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1616887133, "http://vignette3.wikia.nocookie.net/play-rust/images/0/04/Snow_Jacket_-_Red_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 510887968, "http://vignette2.wikia.nocookie.net/play-rust/images/c/cc/Stone_Barricade_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -2118132208, "http://vignette1.wikia.nocookie.net/play-rust/images/0/0a/Stone_Spear_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1625468793, "http://vignette3.wikia.nocookie.net/play-rust/images/2/24/Supply_Signal_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1293049486, "http://vignette2.wikia.nocookie.net/play-rust/images/9/9a/Survey_Charge_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 456448245, "http://vignette3.wikia.nocookie.net/play-rust/images/4/4e/Thompson_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 2077983581, "http://vignette3.wikia.nocookie.net/play-rust/images/1/1b/Waterpipe_Shotgun_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 124310981, "http://vignette2.wikia.nocookie.net/play-rust/images/0/00/Wolf_Headdress_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1883959124, "http://vignette2.wikia.nocookie.net/play-rust/images/6/68/Wood_Armor_Pants_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 1554697726, "http://vignette2.wikia.nocookie.net/play-rust/images/4/4f/Wood_Chestplate_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -770311783, "http://vignette2.wikia.nocookie.net/play-rust/images/f/ff/Wood_Storage_Box_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -420273765, "http://vignette3.wikia.nocookie.net/play-rust/images/3/3d/Wooden_Arrow_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -814689390, "http://vignette2.wikia.nocookie.net/play-rust/images/e/e5/Wooden_Barricade_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -685265909, "http://vignette4.wikia.nocookie.net/play-rust/images/f/f7/Wooden_Floor_Spikes_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1779401418, "http://vignette2.wikia.nocookie.net/play-rust/images/8/85/High_External_Stone_Gate_icon.png/revision/latest/scale-to-width-down/{0}" },
            { 108061910, "http://vignette3.wikia.nocookie.net/play-rust/images/c/c8/Wooden_Ladder_icon.png/revision/latest/scale-to-width-down/{0}" },
            { -1127699509, "http://vignette2.wikia.nocookie.net/play-rust/images/f/f2/Wooden_Spear_icon.png/revision/latest/scale-to-width-down/{0}" },
        };

        #endregion

        #region Default Binds
        static readonly Dictionary<string, string> defaultBinds = new Dictionary<string, string>()
        {
            { "f1", "consoletoggle" },
            { "f7", "bugreporter" },
            { "w", "+forward" },
            { "s", "+backward" },
            { "a", "+left" },
            { "d", "+right" },
            { "mouse0", "+attack" },
            { "mouse1", "+attack2" },
            { "mouse2", "+attack3" },
            { "1", "+slot1" },
            { "2", "+slot2" },
            { "3", "+slot3" },
            { "4", "+slot4" },
            { "5", "+slot5" },
            { "6", "+slot6" },
            { "7", "+slot7" },
            { "8", "+slot8" },
            { "leftshift", "+sprint" },
            { "rightshift", "+sprint" },
            { "leftalt", "+altlook" },
            { "r", "+reload" },
            { "space", "+jump" },
            { "leftcontrol", "+duck" },
            { "e", "+use" },
            { "v", "+voice" },
            { "g", "+map" },
            { "t", "chat.open" },
            { "return", "chat.open" },
            { "mousewheeldown", "+invnext" },
            { "mousewheelup", "+invprev" },
            { "tab", "inventory.toggle" },
            { "q", "inventory.togglecrafting" },
            { "f", "lighttoggle" }
        };
        #endregion

        #region CategoryShortnameLookup
        Dictionary<string, string> CategoryShortNames = new Dictionary<string, string>()
        {
            { ItemCategory.Construction.ToString(), "Build" },
            { ItemCategory.Items.ToString(), "Items" },
            { ItemCategory.Resources.ToString(), "Resources" },
            { ItemCategory.Attire.ToString(), "Clothing" },
            { ItemCategory.Tool.ToString(), "Tools" },
            { ItemCategory.Medical.ToString(), "Medical" },
            { ItemCategory.Weapon.ToString(), "Weapons" },
            { ItemCategory.Ammunition.ToString(), "Ammo" },
            { ItemCategory.Traps.ToString(), "Traps" },
            { ItemCategory.Misc.ToString(), "Misc" },
            { ItemCategory.Common.ToString(), "Common" },
            { ItemCategory.Component.ToString(), "Component" },
            { ItemCategory.Food.ToString(), "Food" },
        };
        #endregion
    }
}