using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Quick Sort", "Frenk92", "1.3.3")]
    [Description("Adds a GUI that allows players to quickly sort items into containers")]
    class QuickSort : CovalencePlugin
    {
        #region Data

        Dictionary<string, PlayerData> Users = new Dictionary<string, PlayerData>();
        public class PlayerData
        {
            public bool Enabled { get; set; }
            public string UiStyle { get; set; }
            public ContainerTypes Containers { get; set; }

            public PlayerData(string UiStyle, ContainerTypes Containers)
            {
                Enabled = true;
                this.UiStyle = UiStyle;
                this.Containers = Containers;
            }
        }

        private void LoadData() { Users = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, PlayerData>>(Name); }
        private void SaveData() { Interface.Oxide.DataFileSystem.WriteObject(Name, Users); }

        PlayerData GetPlayerData(string id)
        {
            PlayerData data;
            if (!Users.TryGetValue(id, out data))
            {
                Users[id] = data = new PlayerData(config.DefaultUiStyle, config.Containers);
                SaveData();
            }

            return data;
        }

        #endregion Data

        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Default UI style (center, lite, right, custom)")]
            public string DefaultUiStyle { get; set; } = "right";

            [JsonProperty(PropertyName = "Loot all delay in seconds (0 to disable)")]
            public int LootAllDelay { get; set; } = 0;

            [JsonProperty(PropertyName = "Enable/Disable loot all on the sleepers")]
            public bool LootSleepers { get; set; } = false;

            [JsonProperty(PropertyName = "Enable/Disable container types")]
            public ContainerTypes Containers { get; set; } = new ContainerTypes();

            [JsonProperty(PropertyName = "Custom UI Settings")]
            public UiSettings CustomSettings { get; set; } = new UiSettings();
        }

        public class ContainerTypes
        {
            public bool Main { get; set; } = true;
            public bool Wear { get; set; } = false;
            public bool Belt { get; set; } = false;
        }

        public class UiSettings
        {
            public string AnchorMin { get; set; } = "0.637 0";
            public string AnchorMax { get; set; } = "0.845 0.146";
            public string Color { get; set; } = "0.5 0.5 0.5 0.33";
            public string ButtonsColor { get; set; } = "1 0.5 0 0.5";
            public string LootAllColor { get; set; } = "0 0.7 0 0.5";
            public string TextColor { get; set; } = "#FFFFFF";
            public int TextSize { get; set; } = 16;
            public int CategoriesTextSize { get; set; } = 14;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            string configPath = $"{Interface.Oxide.ConfigDirectory}{Path.DirectorySeparatorChar}{Name}.json";
            PrintWarning($"Could not load a valid configuration file, creating a new configuration file at {configPath}");
            config = new Configuration();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion Configuration

        #region Localization

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Deposit"] = "Deposit",
                ["DepositAll"] = "All",
                ["DepositAmmo"] = "Ammo",
                ["DepositAttire"] = "Attire",
                ["DepositConstruction"] = "Construction",
                ["DepositExisting"] = "Existing",
                ["DepositFood"] = "Food",
                ["DepositItems"] = "Deployables",
                ["DepositMedical"] = "Medical",
                ["DepositResources"] = "Resources",
                ["DepositTools"] = "Tools",
                ["DepositTraps"] = "Traps",
                ["DepositWeapons"] = "Weapons",
                ["DepositComponents"] = "Components",
                ["DepositMisc"] = "Misc",
                ["LootAll"] = "Loot All",
                ["InvalidArg"] = "\"{0}\" is an invalid argument.",
                ["Edited"] = "\"{0}\" edited to: {1}",
                ["Help"] = "List Commands:\n/qs enabled \"true/false\" - enable/disable gui.\n/qs style \"center/lite/right/custom\" - change gui style.\n/qs conatiner \"main/wear/belt\" \"true/false\" - add/remove container type from the sort."
            }, this);
        }

        #endregion Localization

        #region Initialization

        private static readonly Dictionary<ulong, string> guiInfo = new Dictionary<ulong, string>();

        private const string permLootAll = "quicksort.lootall";
        private const string permUse = "quicksort.use";

        private void Init()
        {
            permission.RegisterPermission(permLootAll, this);
            permission.RegisterPermission(permUse, this);
            LoadData();
        }

        #endregion Initialization

        #region Game Hooks

        private void OnLootPlayer(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, permUse))
            {
                UserInterface(player);
            }
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (permission.UserHasPermission(player.UserIDString, permUse) && !(entity is VendingMachine) && !(entity is ShopFront))
            {
                UserInterface(player);
            }
        }

        private void OnPlayerLootEnd(PlayerLoot inventory)
        {
            BasePlayer player = inventory.GetComponent<BasePlayer>();

            if (player != null)
            {
                DestroyUi(player);
            }
        }

        void OnPlayerTick(BasePlayer player)
        {
            if (player.IsConnected && player.IsSleeping() && guiInfo.ContainsKey(player.userID))
            {
                DestroyUi(player);
            }
        }

        #endregion Game Hooks

        #region Commands

        #region Chat Commands

        [Command("qs")]
        private void ChatCommand(IPlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.Id, permUse) || args.Length == 0) return;

            var data = GetPlayerData(player.Id);
            var msg = "";
            var error = "";
            switch (args[0].ToLower())
            {
                case "help":
                    {
                        error = Lang("Help", player.Id);
                        break;
                    }
                case "enabled":
                    {
                        var flag = false;
                        if (args.Length > 1 && bool.TryParse(args[1], out flag))
                            data.Enabled = flag;
                        else
                            data.Enabled = !data.Enabled;
                        msg = Lang("Edited", player.Id, args[0], data.Enabled);
                        break;
                    }
                case "style":
                    {
                        if (args.Length < 2) return;
                        switch (args[1].ToLower())
                        {
                            case "center":
                            case "lite":
                            case "right":
                            case "custom":
                                {
                                    data.UiStyle = args[1].ToLower();
                                    msg = Lang("Edited", player.Id, $"{args[0]}", data.UiStyle);
                                    break;
                                }
                            default:
                                {
                                    error = Lang("InvalidArg", player.Id, args[1]);
                                    break;
                                }
                        }
                        break;
                    }
                case "container":
                    {
                        if (args.Length < 2) return;
                        switch (args[1].ToLower())
                        {
                            case "main":
                                {
                                    var flag = false;
                                    if (args.Length > 2 && bool.TryParse(args[2], out flag))
                                        data.Containers.Main = flag;
                                    else
                                        data.Containers.Main = !data.Containers.Main;
                                    msg = Lang("Edited", player.Id, $"{args[0]} {args[1]}", data.Containers.Main);
                                    break;
                                }
                            case "wear":
                                {
                                    var flag = false;
                                    if (args.Length > 2 && bool.TryParse(args[2], out flag))
                                        data.Containers.Wear = flag;
                                    else
                                        data.Containers.Wear = !data.Containers.Wear;
                                    msg = Lang("Edited", player.Id, $"{args[0]} {args[1]}", data.Containers.Wear);
                                    break;
                                }
                            case "belt":
                                {
                                    var flag = false;
                                    if (args.Length > 2 && bool.TryParse(args[2], out flag))
                                        data.Containers.Belt = flag;
                                    else
                                        data.Containers.Belt = !data.Containers.Belt;
                                    msg = Lang("Edited", player.Id, $"{args[0]} {args[1]}", data.Containers.Belt);
                                    break;
                                }
                            default:
                                {
                                    error = Lang("InvalidArg", player.Id, args[1]);
                                    break;
                                }
                        }
                        break;
                    }
                default:
                    {
                        error = Lang("InvalidArg", player.Id, args[0]);
                        break;
                    }
            }

            if (error == "")
            {
                player.Reply(msg);
                SaveData();
            }
            else
                player.Reply(error);
        }

        #endregion Chat Commands

        #region Console Commands

        [Command("quicksort")]
        private void SortCommand(IPlayer player, string command, string[] args)
        {
            if (player.HasPermission(permUse))
            {
                SortItems(player.Object as BasePlayer, args);
            }
        }

        [Command("quicksort.lootall")]
        private void LootAllCommand(IPlayer player, string command, string[] args)
        {
            if (player.HasPermission(permLootAll))
            {
                timer.Once(config.LootAllDelay, () => AutoLoot(player.Object as BasePlayer));
            }
        }

        [Command("quicksort.lootdelay")]
        private void LootDelayCommand(IPlayer player, string command, string[] args)
        {
            if (player.IsAdmin)
            {
                int x;
                if (int.TryParse(args[0], out x))
                {
                    config.LootAllDelay = x;
                    SaveConfig();
                }
            }
        }

        #endregion Console Commands

        #endregion Commands

        #region Loot Handling

        private void AutoLoot(BasePlayer player)
        {
            List<ItemContainer> containers = GetLootedInventory(player);
            ItemContainer playerMain = player.inventory.containerMain;

            if (containers != null && playerMain != null && (containers[0].playerOwner == null || config.LootSleepers))
            {
                List<Item> itemsSelected = new List<Item>();
                foreach (var c in containers)
                {
                    itemsSelected.AddRange(CloneItemList(c.itemList));
                }
                itemsSelected.Sort((item1, item2) => item2.info.itemid.CompareTo(item1.info.itemid));
                MoveItems(itemsSelected, playerMain);
            }
        }

        private void SortItems(BasePlayer player, string[] args)
        {
            if (player == null) return;
            var type = GetPlayerData(player.UserIDString)?.Containers;
            ItemContainer container = GetLootedInventory(player)[0];
            ItemContainer playerMain = player.inventory.containerMain;
            ItemContainer playerWear = player.inventory.containerWear;
            ItemContainer playerBelt = player.inventory.containerBelt;

            if (container != null && playerMain != null)
            {
                List<Item> itemsSelected = new List<Item>();

                if (args.Length == 1)
                {
                    if (string.IsNullOrEmpty(args[0])) return;
                    if (args[0].Equals("existing"))
                    {
                        if (config.Containers.Main && (type == null || type.Main))
                            itemsSelected.AddRange(GetExistingItems(playerMain, container));
                        if (playerWear != null && config.Containers.Wear && type != null && type.Wear)
                            itemsSelected.AddRange(GetExistingItems(playerWear, container));
                        if (playerBelt != null && config.Containers.Belt && type != null && type.Belt)
                            itemsSelected.AddRange(GetExistingItems(playerBelt, container));
                    }
                    else
                    {
                        ItemCategory category = StringToItemCategory(args[0]);
                        if (config.Containers.Main && (type == null || type.Main))
                            itemsSelected.AddRange(GetItemsOfType(playerMain, category));
                        if (playerWear != null && config.Containers.Wear && type != null && type.Wear)
                            itemsSelected.AddRange(GetItemsOfType(playerWear, category));
                        if (playerBelt != null && config.Containers.Belt && type != null && type.Belt)
                            itemsSelected.AddRange(GetItemsOfType(playerBelt, category));
                    }
                }
                else
                {
                    if (config.Containers.Main && (type == null || type.Main))
                        itemsSelected.AddRange(CloneItemList(playerMain.itemList));
                    if (playerWear != null && config.Containers.Wear && type != null && type.Wear)
                        itemsSelected.AddRange(CloneItemList(playerWear.itemList));
                    if (playerBelt != null && config.Containers.Belt && type != null && type.Belt)
                        itemsSelected.AddRange(CloneItemList(playerBelt.itemList));
                }

                IEnumerable<Item> uselessItems = GetUselessItems(itemsSelected, container);

                foreach (Item item in uselessItems)
                {
                    itemsSelected.Remove(item);
                }

                itemsSelected.Sort((item1, item2) => item2.info.itemid.CompareTo(item1.info.itemid));
                MoveItems(itemsSelected, container);
            }
        }

        #endregion Loot Handling

        #region Item Helpers

        private IEnumerable<Item> GetUselessItems(IEnumerable<Item> items, ItemContainer container)
        {
            BaseOven furnace = container.entityOwner?.GetComponent<BaseOven>();
            List<Item> uselessItems = new List<Item>();

            if (furnace != null)
            {
                foreach (Item item in items)
                {
                    ItemModCookable cookable = item.info.GetComponent<ItemModCookable>();

                    if (cookable == null || cookable.lowTemp > furnace.cookingTemperature || cookable.highTemp < furnace.cookingTemperature)
                    {
                        uselessItems.Add(item);
                    }
                }
            }

            return uselessItems;
        }

        private List<Item> CloneItemList(IEnumerable<Item> list)
        {
            List<Item> clone = new List<Item>();

            foreach (Item item in list)
            {
                clone.Add(item);
            }

            return clone;
        }

        private List<Item> GetExistingItems(ItemContainer primary, ItemContainer secondary)
        {
            List<Item> existingItems = new List<Item>();

            if (primary != null && secondary != null)
            {
                foreach (Item t in primary.itemList)
                {
                    foreach (Item t1 in secondary.itemList)
                    {
                        if (t.info.itemid != t1.info.itemid)
                        {
                            continue;
                        }

                        existingItems.Add(t);
                        break;
                    }
                }
            }

            return existingItems;
        }

        private List<Item> GetItemsOfType(ItemContainer container, ItemCategory category)
        {
            List<Item> items = new List<Item>();

            foreach (Item item in container.itemList)
            {
                if (item.info.category == category)
                {
                    items.Add(item);
                }
            }

            return items;
        }

        private List<ItemContainer> GetLootedInventory(BasePlayer player)
        {
            PlayerLoot playerLoot = player.inventory.loot;
            return playerLoot != null && playerLoot.IsLooting() ? playerLoot.containers : null;
        }

        private void MoveItems(IEnumerable<Item> items, ItemContainer to)
        {
            foreach (Item item in items)
            {
                item.MoveToContainer(to);
            }
        }

        private ItemCategory StringToItemCategory(string categoryName)
        {
            string[] categoryNames = Enum.GetNames(typeof(ItemCategory));

            for (int i = 0; i < categoryNames.Length; i++)
            {
                if (categoryName.ToLower().Equals(categoryNames[i].ToLower()))
                {
                    return (ItemCategory)i;
                }
            }

            return (ItemCategory)categoryNames.Length;
        }

        #endregion Item Helpers

        #region User Interface

        private void UserInterface(BasePlayer player)
        {
            var data = GetPlayerData(player.UserIDString);
            if (!data.Enabled) return;

            DestroyUi(player);
            guiInfo[player.userID] = CuiHelper.GetGuid();
            player.inventory.loot.gameObject.AddComponent<UIDestroyer>();

            switch (data.UiStyle)
            {
                case "center":
                    UiCenter(player);
                    break;
                case "lite":
                    UiLite(player);
                    break;
                case "right":
                    UiRight(player);
                    break;
                case "custom":
                    UiCustom(player);
                    break;
            }
        }

        #region UI Custom

        private void UiCustom(BasePlayer player)
        {
            CuiElementContainer elements = new CuiElementContainer();
            var cfg = config.CustomSettings;

            string panel = elements.Add(new CuiPanel
            {
                Image = { Color = cfg.Color },
                RectTransform = { AnchorMin = cfg.AnchorMin, AnchorMax = cfg.AnchorMax }
            }, "Hud.Menu", guiInfo[player.userID]);
            //left
            elements.Add(new CuiLabel
            {
                Text = { Text = Lang("Deposit", player.UserIDString), FontSize = cfg.TextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor },
                RectTransform = { AnchorMin = "0.02 0.8", AnchorMax = "0.3 1" }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort existing", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.02 0.6", AnchorMax = "0.3 0.8" },
                Text = { Text = Lang("DepositExisting", player.UserIDString), FontSize = cfg.TextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.02 0.35", AnchorMax = "0.3 0.55" },
                Text = { Text = Lang("DepositAll", player.UserIDString), FontSize = cfg.TextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
            }, panel);
            if (permission.UserHasPermission(player.UserIDString, permLootAll))
            {
                elements.Add(new CuiButton
                {
                    Button = { Command = "quicksort.lootall", Color = cfg.LootAllColor },
                    RectTransform = { AnchorMin = "0.02 0.05", AnchorMax = "0.3 0.3" },
                    Text = { Text = Lang("LootAll", player.UserIDString), FontSize = cfg.TextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
                }, panel);
            }
            //center
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort weapon", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.35 0.818", AnchorMax = "0.63 0.949" },
                Text = { Text = Lang("DepositWeapons", player.UserIDString), FontSize = cfg.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort ammunition", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.35 0.664", AnchorMax = "0.63 0.796" },
                Text = { Text = Lang("DepositAmmo", player.UserIDString), FontSize = cfg.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort medical", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.35 0.511", AnchorMax = "0.63 0.642" },
                Text = { Text = Lang("DepositMedical", player.UserIDString), FontSize = cfg.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort attire", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.35 0.358", AnchorMax = "0.63 0.489" },
                Text = { Text = Lang("DepositAttire", player.UserIDString), FontSize = cfg.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort resources", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.35 0.204", AnchorMax = "0.63 0.336" },
                Text = { Text = Lang("DepositResources", player.UserIDString), FontSize = cfg.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort component", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.35 0.051", AnchorMax = "0.63 0.182" },
                Text = { Text = Lang("DepositComponents", player.UserIDString), FontSize = cfg.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
            }, panel);
            //right
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort construction", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.67 0.818", AnchorMax = "0.95 0.949" },
                Text = { Text = Lang("DepositConstruction", player.UserIDString), FontSize = cfg.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort items", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.67 0.664", AnchorMax = "0.95 0.796" },
                Text = { Text = Lang("DepositItems", player.UserIDString), FontSize = cfg.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort tool", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.67 0.511", AnchorMax = "0.95 0.642" },
                Text = { Text = Lang("DepositTools", player.UserIDString), FontSize = cfg.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort food", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.67 0.358", AnchorMax = "0.95 0.489" },
                Text = { Text = Lang("DepositFood", player.UserIDString), FontSize = cfg.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort traps", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.67 0.204", AnchorMax = "0.95 0.336" },
                Text = { Text = Lang("DepositTraps", player.UserIDString), FontSize = cfg.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort misc", Color = cfg.ButtonsColor },
                RectTransform = { AnchorMin = "0.67 0.051", AnchorMax = "0.95 0.182" },
                Text = { Text = Lang("DepositMisc", player.UserIDString), FontSize = cfg.CategoriesTextSize, Align = TextAnchor.MiddleCenter, Color = cfg.TextColor }
            }, panel);

            CuiHelper.AddUi(player, elements);
        }

        #endregion UI Custom

        #region UI Center

        private void UiCenter(BasePlayer player)
        {
            CuiElementContainer elements = new CuiElementContainer();

            string panel = elements.Add(new CuiPanel
            {
                Image = { Color = "0.5 0.5 0.5 0.33" },
                RectTransform = { AnchorMin = "0.354 0.625", AnchorMax = "0.633 0.816" }
            }, "Hud.Menu", guiInfo[player.userID]);
            //left
            elements.Add(new CuiLabel
            {
                Text = { Text = Lang("Deposit"), FontSize = 16, Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.02 0.8", AnchorMax = "0.3 1" }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort existing", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.02 0.6", AnchorMax = "0.3 0.8" },
                Text = { Text = Lang("DepositExisting"), FontSize = 16, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.02 0.35", AnchorMax = "0.3 0.55" },
                Text = { Text = Lang("DepositAll"), FontSize = 16, Align = TextAnchor.MiddleCenter }
            }, panel);
            if (permission.UserHasPermission(player.UserIDString, permLootAll))
            {
                elements.Add(new CuiButton
                {
                    Button = { Command = "quicksort.lootall", Color = "0 0.7 0 0.5" },
                    RectTransform = { AnchorMin = "0.02 0.05", AnchorMax = "0.3 0.3" },
                    Text = { Text = Lang("LootAll"), FontSize = 16, Align = TextAnchor.MiddleCenter }
                }, panel);
            }
            //center
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort weapon", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.35 0.818", AnchorMax = "0.63 0.949" },
                Text = { Text = Lang("DepositWeapons", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort ammunition", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.35 0.664", AnchorMax = "0.63 0.796" },
                Text = { Text = Lang("DepositAmmo", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort medical", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.35 0.511", AnchorMax = "0.63 0.642" },
                Text = { Text = Lang("DepositMedical", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort attire", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.35 0.358", AnchorMax = "0.63 0.489" },
                Text = { Text = Lang("DepositAttire", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort resources", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.35 0.204", AnchorMax = "0.63 0.336" },
                Text = { Text = Lang("DepositResources", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort component", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.35 0.051", AnchorMax = "0.63 0.182" },
                Text = { Text = Lang("DepositComponents", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            //right
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort construction", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.67 0.818", AnchorMax = "0.95 0.949" },
                Text = { Text = Lang("DepositConstruction", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort items", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.67 0.664", AnchorMax = "0.95 0.796" },
                Text = { Text = Lang("DepositItems", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort tool", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.67 0.511", AnchorMax = "0.95 0.642" },
                Text = { Text = Lang("DepositTools", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort food", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.67 0.358", AnchorMax = "0.95 0.489" },
                Text = { Text = Lang("DepositFood", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort traps", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.67 0.204", AnchorMax = "0.95 0.336" },
                Text = { Text = Lang("DepositTraps", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort misc", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.67 0.051", AnchorMax = "0.95 0.182" },
                Text = { Text = Lang("DepositMisc", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);

            CuiHelper.AddUi(player, elements);
        }

        #endregion UI Center

        #region UI Lite

        private void UiLite(BasePlayer player)
        {
            CuiElementContainer elements = new CuiElementContainer();

            string panel = elements.Add(new CuiPanel
            {
                Image = { Color = "0.0 0.0 0.0 0.0" },
                RectTransform = { AnchorMin = "0.663 0.769", AnchorMax = "0.928 0.96" }
            }, "Hud.Menu", guiInfo[player.userID]);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort existing", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "-0.88 -1.545", AnchorMax = "-0.63 -1.435" },
                Text = { Text = Lang("DepositExisting", player.UserIDString), FontSize = 13, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "-0.61 -1.545", AnchorMax = "-0.36 -1.435" },
                Text = { Text = Lang("DepositAll", player.UserIDString), FontSize = 13, Align = TextAnchor.MiddleCenter }
            }, panel);
            if (permission.UserHasPermission(player.UserIDString, permLootAll))
            {
                elements.Add(new CuiButton
                {
                    Button = { Command = "quicksort.lootall", Color = "0 0.7 0 0.5" },
                    RectTransform = { AnchorMin = "-0.34 -1.545", AnchorMax = "-0.13 -1.435" },
                    Text = { Text = Lang("LootAll", player.UserIDString), FontSize = 13, Align = TextAnchor.MiddleCenter }
                }, panel);
            }

            CuiHelper.AddUi(player, elements);
        }

        #endregion UI Lite

        #region UI Right

        private void UiRight(BasePlayer player)
        {
            CuiElementContainer elements = new CuiElementContainer();

            string panel = elements.Add(new CuiPanel
            {
                Image = { Color = "0.5 0.5 0.5 0.33" },
                RectTransform = { AnchorMin = "0.638 0.844", AnchorMax = "0.909 1" }
            }, "Hud.Menu", guiInfo[player.userID]);
            //left
            elements.Add(new CuiLabel
            {
                Text = { Text = Lang("Deposit", player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.02 0.8", AnchorMax = "0.3 1" }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort existing", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.02 0.6", AnchorMax = "0.3 0.8" },
                Text = { Text = Lang("DepositExisting", player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.02 0.35", AnchorMax = "0.3 0.55" },
                Text = { Text = Lang("DepositAll", player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter }
            }, panel);
            if (permission.UserHasPermission(player.UserIDString, permLootAll))
            {
                elements.Add(new CuiButton
                {
                    Button = { Command = "quicksort.lootall", Color = "0 0.7 0 0.5" },
                    RectTransform = { AnchorMin = "0.02 0.05", AnchorMax = "0.3 0.3" },
                    Text = { Text = Lang("LootAll", player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter }
                }, panel);
            }
            //center
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort weapon", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.35 0.818", AnchorMax = "0.63 0.949" },
                Text = { Text = Lang("DepositWeapons", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort ammunition", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.35 0.664", AnchorMax = "0.63 0.796" },
                Text = { Text = Lang("DepositAmmo", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort medical", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.35 0.511", AnchorMax = "0.63 0.642" },
                Text = { Text = Lang("DepositMedical", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort attire", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.35 0.358", AnchorMax = "0.63 0.489" },
                Text = { Text = Lang("DepositAttire", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort resources", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.35 0.204", AnchorMax = "0.63 0.336" },
                Text = { Text = Lang("DepositResources", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort component", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.35 0.051", AnchorMax = "0.63 0.182" },
                Text = { Text = Lang("DepositComponents", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            //right
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort construction", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.67 0.818", AnchorMax = "0.95 0.949" },
                Text = { Text = Lang("DepositConstruction", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort items", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.67 0.664", AnchorMax = "0.95 0.796" },
                Text = { Text = Lang("DepositItems", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort tool", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.67 0.511", AnchorMax = "0.95 0.642" },
                Text = { Text = Lang("DepositTools", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort food", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.67 0.358", AnchorMax = "0.95 0.489" },
                Text = { Text = Lang("DepositFood", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort traps", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.67 0.204", AnchorMax = "0.95 0.336" },
                Text = { Text = Lang("DepositTraps", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort misc", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.67 0.051", AnchorMax = "0.95 0.182" },
                Text = { Text = Lang("DepositMisc", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);

            CuiHelper.AddUi(player, elements);
        }

        #endregion UI Right

        #region Cleanup

        private static void DestroyUi(BasePlayer player)
        {
            string gui;
            if (guiInfo.TryGetValue(player.userID, out gui))
            {
                CuiHelper.DestroyUi(player, gui);
                guiInfo.Remove(player.userID);
            }
        }

        private class UIDestroyer : MonoBehaviour
        {
            private void PlayerStoppedLooting(BasePlayer player)
            {
                DestroyUi(player);
                Destroy(this);
            }
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                DestroyUi(player);
            }
        }

        #endregion Cleanup

        #endregion User Interface

        #region Helpers

        private string Lang(string key, string id = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, id), args);
        }

        #endregion Helpers
    }
}
