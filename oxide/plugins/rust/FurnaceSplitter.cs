using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Furnace Splitter", "Skipcast", "2.1.7", ResourceId = 2406)]
    [Description("Splits up resources in furnaces automatically and shows useful furnace information")]
    public class FurnaceSplitter : RustPlugin
    {
        private class OvenSlot
        {
            /// <summary>The item in this slot. May be null.</summary>
            public Item Item;

            /// <summary>The slot position</summary>
            public int? Position;

            /// <summary>The slot's index in the itemList list.</summary>
            public int Index;

            /// <summary>How much should be added/removed from stack</summary>
            public int DeltaAmount;
        }

        public class OvenInfo
        {
            public float ETA;
            public float FuelNeeded;
        }

        private class StoredData
        {
            public Dictionary<ulong, PlayerOptions> AllPlayerOptions { get; private set; } = new Dictionary<ulong, PlayerOptions>();
        }

        private class PluginConfig
        {
            //[JsonRequired]
            public Vector2 UiPosition { get; set; } = new Vector2(0.6505f, 0.022f);
        }

        private class PlayerOptions
        {
            public bool Enabled;
            public Dictionary<string, int> TotalStacks = new Dictionary<string, int>();
        }

        public enum MoveResult
        {
            Ok,
            SlotsFilled,
            NotEnoughSlots
        }

        private class Vector2Converter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                Vector2 vec = (Vector2)value;
                serializer.Serialize(writer, new { vec.x, vec.y });
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                Vector2 result = new Vector2();
                JObject jVec = JObject.Load(reader);

                result.x = jVec["x"].ToObject<float>();
                result.y = jVec["y"].ToObject<float>();

                return result;
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Vector2);
            }
        }

        private Dictionary<ulong, PlayerOptions> allPlayerOptions => storedData.AllPlayerOptions;
        private PluginConfig config;
        private StoredData storedData;

        private const string permUse = "furnacesplitter.use";

        private readonly Dictionary<ulong, string> openUis = new Dictionary<ulong, string>();
        private readonly Dictionary<BaseOven, List<BasePlayer>> looters = new Dictionary<BaseOven, List<BasePlayer>>();
        private readonly Stack<BaseOven> queuedUiUpdates = new Stack<BaseOven>();

        private readonly string[] compatibleOvens =
        {
            "bbq.deployed",
            "campfire",
            "fireplace.deployed",
            "furnace",
            "furnace.large",
            "hobobarrel_static",
            "refinery_small_deployed",
            "skull_fire_pit"
        };

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            DestroyUI(player);
        }

        private void OnPlayerInit(BasePlayer player)
        {
            InitPlayer(player);
        }

        private void Init()
        {
            // Only add if it's not already been added in LoadDefaultConfig. That would be the case the first time the plugin is initialized.
            if (Config.Settings.Converters.All(conv => conv.GetType() != typeof(Vector2Converter)))
                Config.Settings.Converters.Add(new Vector2Converter());
        }

        private void Loaded()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);

            permission.RegisterPermission(permUse, this);

            if (config == null)
            {
                // Default config not created, load existing config.
                config = Config.ReadObject<PluginConfig>();
            }
            else
            {
                // Save default config.
                Config.WriteObject(config);
            }
        }

        #region Configuration

        protected override void LoadDefaultConfig()
        {
            Config.Settings.Converters.Add(new Vector2Converter());
            PrintWarning("Creating default config for FurnaceSplitter.");
            config = new PluginConfig();
        }

        #endregion Configuration

        private void OnServerInitialized()
        {
            foreach (BasePlayer player in Player.Players)
            {
                InitPlayer(player);
            }

            lang.RegisterMessages(new Dictionary<string, string>
            {
                // English
                { "turnon", "Turn On" },
                { "turnoff", "Turn Off" },
                { "title", "Furnace Splitter" },
                { "eta", "ETA" },
                { "totalstacks", "Total stacks" },
                { "trim", "Trim fuel" },
                { "lootsource_invalid", "Current loot source invalid" },
                { "unsupported_furnace", "Unsupported furnace." },
                { "nopermission", "You don't have permission to use this." }
            }, this);
        }

        private void OnServerSave()
        {
            SaveData();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("FurnaceSplitter", storedData);
        }

        private void InitPlayer(BasePlayer player)
        {
            if (!allPlayerOptions.ContainsKey(player.userID))
            {
                allPlayerOptions[player.userID] = new PlayerOptions
                {
                    Enabled = true,
                    TotalStacks = new Dictionary<string, int>()
                };
            }

            var initialStackOptions = new Dictionary<string, int>
            {
                {"furnace", 3},
                {"bbq.deployed", 9},
                {"campfire", 2},
                {"fireplace.deployed", 2},
                {"furnace.large", 15},
                {"hobobarrel_static", 2},
                {"refinery_small_deployed", 3},
                {"skull_fire_pit", 2}
            };

            PlayerOptions options = allPlayerOptions[player.userID];

            foreach (var kv in initialStackOptions)
            {
                if (!options.TotalStacks.ContainsKey(kv.Key))
                    options.TotalStacks.Add(kv.Key, kv.Value);
            }
        }

        private void OnTick()
        {
            while (queuedUiUpdates.Count > 0)
            {
                BaseOven oven = queuedUiUpdates.Pop();

                if (!oven || oven.IsDestroyed)
                    continue;

                OvenInfo ovenInfo = GetOvenInfo(oven);

                GetLooters(oven)?.ForEach(plr =>
                {
                    if (plr && !plr.IsDestroyed)
                    {
                        CreateUi(plr, oven, ovenInfo);
                    }
                });
            }
        }

        public OvenInfo GetOvenInfo(BaseOven oven)
        {
            OvenInfo result = new OvenInfo();
            var smeltTimes = GetSmeltTimes(oven);

            if (smeltTimes.Count > 0)
            {
                var longestStack = smeltTimes.OrderByDescending(kv => kv.Value).First();
                float fuelUnits = oven.fuelType.GetComponent<ItemModBurnable>().fuelAmount;
                float neededFuel = (float)Math.Ceiling(longestStack.Value * (oven.cookingTemperature / 200.0f) / fuelUnits);

                result.FuelNeeded = neededFuel;
                result.ETA = longestStack.Value;
            }

            return result;
        }

        private void Unload()
        {
            SaveData();

            foreach (var kv in openUis.ToDictionary(kv => kv.Key, kv => kv.Value))
            {
                BasePlayer player = BasePlayer.FindByID(kv.Key);
                DestroyUI(player);
            }
        }

        private bool GetEnabled(BasePlayer player)
        {
            return allPlayerOptions[player.userID].Enabled;
        }

        private void SetEnabled(BasePlayer player, bool enabled)
        {
            allPlayerOptions[player.userID].Enabled = enabled;
            CreateUiIfFurnaceOpen(player);
        }

        private bool IsSlotCompatible(Item item, BaseOven oven, ItemDefinition itemDefinition)
        {
            ItemModCookable cookable = item.info.GetComponent<ItemModCookable>();

            if (item.amount < item.info.stackable && item.info == itemDefinition)
                return true;

            if (oven.allowByproductCreation && oven.fuelType.GetComponent<ItemModBurnable>().byproductItem == item.info)
                return true;

            if (cookable == null || cookable.becomeOnCooked == itemDefinition)
                return true;

            if (CanCook(cookable, oven))
                return true;

            return false;
        }

        private void OnConsumeFuel(BaseOven oven, Item fuel, ItemModBurnable burnable)
        {
            if (compatibleOvens.Contains(oven.ShortPrefabName))
                queuedUiUpdates.Push(oven);
        }

        private List<BasePlayer> GetLooters(BaseOven oven)
        {
            if (looters.ContainsKey(oven))
                return looters[oven];

            return null;
        }

        private void AddLooter(BaseOven oven, BasePlayer player)
        {
            if (!looters.ContainsKey(oven))
                looters[oven] = new List<BasePlayer>();

            var list = looters[oven];
            list.Add(player);
        }

        private void RemoveLooter(BaseOven oven, BasePlayer player)
        {
            if (!looters.ContainsKey(oven))
                return;

            looters[oven].Remove(player);
        }

        private object CanMoveItem(Item item, PlayerInventory inventory, uint targetContainer, int targetSlot)
        {
            BasePlayer player = inventory.GetComponent<BasePlayer>();
            if (player == null)
                return null;

            ItemContainer container = inventory.FindContainer(targetContainer);
            ItemContainer originalContainer = item.GetRootContainer();

            Func<object> splitFunc = () =>
            {
                if (player == null || !HasPermission(player) || !GetEnabled(player))
                    return null;

                PlayerOptions playerOptions = allPlayerOptions[player.userID];

                if (container == null || container == item.GetRootContainer())
                    return null;

                BaseOven oven = container.entityOwner as BaseOven;
                ItemModCookable cookable = item.info.GetComponent<ItemModCookable>();

                if (oven == null || cookable == null)
                    return null;

                int totalSlots = 2 + (oven.allowByproductCreation ? 1 : 0);

                if (playerOptions.TotalStacks.ContainsKey(oven.ShortPrefabName))
                {
                    totalSlots = playerOptions.TotalStacks[oven.ShortPrefabName];
                }

                if (cookable.lowTemp > oven.cookingTemperature || cookable.highTemp < oven.cookingTemperature)
                    return null;

                MoveSplitItem(item, oven, totalSlots);
                return true;
            };

            object returnValue = splitFunc();

            if (HasPermission(player) && GetEnabled(player))
            {
                BaseOven oven = container?.entityOwner as BaseOven ?? item.GetRootContainer().entityOwner as BaseOven;

                if (oven != null && compatibleOvens.Contains(oven.ShortPrefabName))
                {
                    if (returnValue is bool && (bool)returnValue)
                        AutoAddFuel(inventory, oven);

                    queuedUiUpdates.Push(oven);
                }
            }

            return returnValue;
        }

        private MoveResult MoveSplitItem(Item item, BaseOven oven, int totalSlots)
        {
            ItemContainer container = oven.inventory;
            int invalidItemsCount = container.itemList.Count(slotItem => !IsSlotCompatible(slotItem, oven, item.info));
            int numOreSlots = Math.Min(container.capacity - invalidItemsCount, totalSlots);
            int totalMoved = 0;
            int totalAmount = Math.Min(item.amount + container.itemList.Where(slotItem => slotItem.info == item.info).Take(numOreSlots).Sum(slotItem => slotItem.amount), item.info.stackable * numOreSlots);

            if (numOreSlots <= 0)
            {
                return MoveResult.NotEnoughSlots;
            }

            //Puts("---------------------------");

            int totalStackSize = Math.Min(totalAmount / numOreSlots, item.info.stackable);
            int remaining = totalAmount - totalAmount / numOreSlots * numOreSlots;

            List<int> addedSlots = new List<int>();

            //Puts("total: {0}, remaining: {1}, totalStackSize: {2}", totalAmount, remaining, totalStackSize);

            List<OvenSlot> ovenSlots = new List<OvenSlot>();

            for (int i = 0; i < numOreSlots; ++i)
            {
                Item existingItem;
                int slot = FindMatchingSlotIndex(container, out existingItem, item.info, addedSlots);

                if (slot == -1) // full
                {
                    return MoveResult.NotEnoughSlots;
                }

                addedSlots.Add(slot);

                OvenSlot ovenSlot = new OvenSlot
                {
                    Position = existingItem?.position,
                    Index = slot,
                    Item = existingItem
                };

                int currentAmount = existingItem?.amount ?? 0;
                int missingAmount = totalStackSize - currentAmount + (i < remaining ? 1 : 0);
                ovenSlot.DeltaAmount = missingAmount;

                //Puts("[{0}] current: {1}, delta: {2}, total: {3}", slot, currentAmount, ovenSlot.DeltaAmount, currentAmount + missingAmount);

                if (currentAmount + missingAmount <= 0)
                    continue;

                ovenSlots.Add(ovenSlot);
            }

            foreach (OvenSlot slot in ovenSlots)
            {
                if (slot.Item == null)
                {
                    Item newItem = ItemManager.Create(item.info, slot.DeltaAmount, item.skin);
                    slot.Item = newItem;
                    newItem.MoveToContainer(container, slot.Position ?? slot.Index);
                }
                else
                {
                    slot.Item.amount += slot.DeltaAmount;
                }

                totalMoved += slot.DeltaAmount;
            }

            container.MarkDirty();

            if (totalMoved >= item.amount)
            {
                item.Remove();
                item.GetRootContainer()?.MarkDirty();
                return MoveResult.Ok;
            }
            else
            {
                item.amount -= totalMoved;
                item.GetRootContainer()?.MarkDirty();
                return MoveResult.SlotsFilled;
            }
        }

        private void AutoAddFuel(PlayerInventory playerInventory, BaseOven oven)
        {
            int neededFuel = (int)Math.Ceiling(GetOvenInfo(oven).FuelNeeded);
            neededFuel -= oven.inventory.GetAmount(oven.fuelType.itemid, false);
            var playerFuel = playerInventory.FindItemIDs(oven.fuelType.itemid);

            if (neededFuel <= 0 || playerFuel.Count <= 0)
                return;

            foreach (Item fuelItem in playerFuel)
            {
                if (oven.inventory.CanAcceptItem(fuelItem, -1) != ItemContainer.CanAcceptResult.CanAccept)
                    break;

                Item largestFuelStack = oven.inventory.itemList.Where(item => item.info == oven.fuelType).OrderByDescending(item => item.amount).FirstOrDefault();
                int toTake = Math.Min(neededFuel, oven.fuelType.stackable - (largestFuelStack?.amount ?? 0));

                if (toTake > fuelItem.amount)
                    toTake = fuelItem.amount;

                if (toTake <= 0)
                    break;

                neededFuel -= toTake;

                if (toTake >= fuelItem.amount)
                {
                    fuelItem.MoveToContainer(oven.inventory);
                }
                else
                {
                    Item splitItem = fuelItem.SplitItem(toTake);
                    if (!splitItem.MoveToContainer(oven.inventory)) // Break if oven is full
                        break;
                }

                if (neededFuel <= 0)
                    break;
            }
        }

        private int FindMatchingSlotIndex(ItemContainer container, out Item existingItem, ItemDefinition itemType, List<int> indexBlacklist)
        {
            existingItem = null;
            int firstIndex = -1;
            Dictionary<int, Item> existingItems = new Dictionary<int, Item>();

            for (int i = 0; i < container.capacity; ++i)
            {
                if (indexBlacklist.Contains(i))
                    continue;

                Item itemSlot = container.GetSlot(i);
                if (itemSlot == null || itemType != null && itemSlot.info == itemType)
                {
                    if (itemSlot != null)
                        existingItems.Add(i, itemSlot);

                    if (firstIndex == -1)
                    {
                        existingItem = itemSlot;
                        firstIndex = i;
                    }
                }
            }

            if (existingItems.Count <= 0 && firstIndex != -1)
            {
                return firstIndex;
            }
            else if (existingItems.Count > 0)
            {
                var largestStackItem = existingItems.OrderByDescending(kv => kv.Value.amount).First();
                existingItem = largestStackItem.Value;
                return largestStackItem.Key;
            }

            existingItem = null;
            return -1;
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            BaseOven oven = entity as BaseOven;

            if (oven == null || !HasPermission(player) || !compatibleOvens.Contains(oven.ShortPrefabName))
                return;

            AddLooter(oven, player);
            queuedUiUpdates.Push(oven);
        }

        private void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            BaseOven oven = entity as BaseOven;

            if (oven == null || !compatibleOvens.Contains(oven.ShortPrefabName))
                return;

            DestroyUI(player);
            RemoveLooter(oven, player);
        }

        private void OnEntityKill(BaseNetworkable networkable)
        {
            BaseOven oven = networkable as BaseOven;

            if (oven != null)
            {
                DestroyOvenUI(oven);
            }
        }

        private void OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            if (compatibleOvens.Contains(oven.ShortPrefabName))
                queuedUiUpdates.Push(oven);
        }

        private void CreateUiIfFurnaceOpen(BasePlayer player)
        {
            BaseOven oven = player.inventory.loot?.entitySource as BaseOven;

            if (oven != null && compatibleOvens.Contains(oven.ShortPrefabName))
                queuedUiUpdates.Push(oven);
        }

        private CuiElementContainer CreateUi(BasePlayer player, BaseOven oven, OvenInfo ovenInfo)
        {
            PlayerOptions options = allPlayerOptions[player.userID];
            int totalSlots = GetTotalStacksOption(player, oven) ?? oven.inventory.capacity - (oven.allowByproductCreation ? 1 : 2);
            string remainingTimeStr;
            string neededFuelStr;

            if (ovenInfo.ETA <= 0)
            {
                remainingTimeStr = "0s";
                neededFuelStr = "0";
            }
            else
            {
                remainingTimeStr = FormatTime(ovenInfo.ETA);
                neededFuelStr = ovenInfo.FuelNeeded.ToString("##,###");
            }

            string contentColor = "0.7 0.7 0.7 1.0";
            int contentSize = 10;
            string toggleStateStr = (!options.Enabled).ToString();
            string toggleButtonColor = !options.Enabled
                                        ? "0.415 0.5 0.258 0.4"
                                        : "0.8 0.254 0.254 0.4";
            string toggleButtonTextColor = !options.Enabled
                                            ? "0.607 0.705 0.431"
                                            : "0.705 0.607 0.431";
            string buttonColor = "0.75 0.75 0.75 0.1";
            string buttonTextColor = "0.77 0.68 0.68 1";

            int nextDecrementSlot = totalSlots - 1;
            int nextIncrementSlot = totalSlots + 1;

            DestroyUI(player);

            Vector2 uiPosition = config.UiPosition;
            Vector2 uiSize = new Vector2(0.1785f, 0.111f);

            CuiElementContainer result = new CuiElementContainer();
            string rootPanelName = result.Add(new CuiPanel
            {
                Image = new CuiImageComponent
                {
                    Color = "0 0 0 0"
                },
                RectTransform =
                {
                    AnchorMin = uiPosition.x + " " + uiPosition.y,
                    AnchorMax = uiPosition.x + uiSize.x + " " + (uiPosition.y + uiSize.y)
                    //AnchorMin = "0.6505 0.022",
                    //AnchorMax = "0.829 0.133"
                }
            }, "Hud.Menu");

            string headerPanel = result.Add(new CuiPanel
            {
                Image = new CuiImageComponent
                {
                    Color = "0.75 0.75 0.75 0.1"
                },
                RectTransform =
                {
                    AnchorMin = "0 0.775",
                    AnchorMax = "1 1"
                }
            }, rootPanelName);

            // Header label
            result.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.051 0",
                    AnchorMax = "1 0.95"
                },
                Text =
                {
                    Text = lang.GetMessage("title", this, player.UserIDString),
                    Align = TextAnchor.MiddleLeft,
                    Color = "0.77 0.7 0.7 1",
                    FontSize = 13
                }
            }, headerPanel);

            string contentPanel = result.Add(new CuiPanel
            {
                Image = new CuiImageComponent
                {
                    Color = "0.65 0.65 0.65 0.06"
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 0.74"
                }
            }, rootPanelName);

            // ETA label
            result.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.02 0.7",
                    AnchorMax = "0.98 1"
                },
                Text =
                {
                    Text = string.Format("{0}: " + (ovenInfo.ETA > 0 ? "~" : "") + remainingTimeStr + " (" + neededFuelStr +  " " + oven.fuelType.displayName.english.ToLower() + ")", lang.GetMessage("eta", this, player.UserIDString)),
                    Align = TextAnchor.MiddleLeft,
                    Color = contentColor,
                    FontSize = contentSize
                }
            }, contentPanel);

            // Toggle button
            result.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.02 0.4",
                    AnchorMax = "0.25 0.7"
                },
                Button =
                {
                    Command = "furnacesplitter.enabled " + toggleStateStr,
                    Color = toggleButtonColor
                },
                Text =
                {
                    Align = TextAnchor.MiddleCenter,
                    Text = options.Enabled ? lang.GetMessage("turnoff", this, player.UserIDString) : lang.GetMessage("turnon", this, player.UserIDString),
                    Color = toggleButtonTextColor,
                    FontSize = 11
                }
            }, contentPanel);

            // Trim button
            result.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.27 0.4",
                    AnchorMax = "0.52 0.7"
                },
                Button =
                {
                    Command = "furnacesplitter.trim",
                    Color = buttonColor
                },
                Text =
                {
                    Align = TextAnchor.MiddleCenter,
                    Text = lang.GetMessage("trim", this, player.UserIDString),
                    Color = contentColor,
                    FontSize = 11
                }
            }, contentPanel);

            // Decrease stack button
            result.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.02 0.05",
                    AnchorMax = "0.07 0.35"
                },
                Button =
                {
                    Command = "furnacesplitter.totalstacks " + nextDecrementSlot,
                    Color = buttonColor
                },
                Text =
                {
                    Align = TextAnchor.MiddleCenter,
                    Text = "<",
                    Color = buttonTextColor,
                    FontSize = contentSize
                }
            }, contentPanel);

            // Empty slots label
            result.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.08 0.05",
                    AnchorMax = "0.19 0.35"
                },
                Text =
                {
                    Align = TextAnchor.MiddleCenter,
                    Text = totalSlots.ToString(),
                    Color = contentColor,
                    FontSize = contentSize
                }
            }, contentPanel);

            // Increase stack button
            result.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.19 0.05",
                    AnchorMax = "0.25 0.35"
                },
                Button =
                {
                    Command = "furnacesplitter.totalstacks " + nextIncrementSlot,
                    Color = buttonColor
                },
                Text =
                {
                    Align = TextAnchor.MiddleCenter,
                    Text = ">",
                    Color = buttonTextColor,
                    FontSize = contentSize
                }
            }, contentPanel);

            // Stack itemType label
            result.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.27 0.05",
                    AnchorMax = "1 0.35"
                },
                Text =
                {
                    Align = TextAnchor.MiddleLeft,
                    Text = string.Format("({0})", lang.GetMessage("totalstacks", this, player.UserIDString)),
                    Color = contentColor,
                    FontSize = contentSize
                }
            }, contentPanel);

            openUis.Add(player.userID, rootPanelName);
            CuiHelper.AddUi(player, result);
            return result;
        }

        private string FormatTime(float totalSeconds)
        {
            double hours = Math.Floor(totalSeconds / 3600);
            double minutes = Math.Floor(totalSeconds / 60 % 60);
            float seconds = totalSeconds % 60;

            if (hours <= 0 && minutes <= 0)
                return seconds + "s";
            if (hours <= 0)
                return minutes + "m" + seconds + "s";
            return hours + "h" + minutes + "m" + seconds + "s";
        }

        private Dictionary<ItemDefinition, float> GetSmeltTimes(BaseOven oven)
        {
            ItemContainer container = oven.inventory;
            var cookables = container.itemList.Where(item =>
            {
                ItemModCookable cookable = item.info.GetComponent<ItemModCookable>();
                return cookable != null && CanCook(cookable, oven);
            }).ToList();

            if (cookables.Count == 0)
                return new Dictionary<ItemDefinition, float>();

            var distinctCookables = cookables.GroupBy(item => item.info, item => item).ToList();
            Dictionary<ItemDefinition, int> amounts = new Dictionary<ItemDefinition, int>();

            foreach (var group in distinctCookables)
            {
                int biggestAmount = group.Max(item => item.amount);
                amounts.Add(group.Key, biggestAmount);
            }

            var smeltTimes = amounts.ToDictionary(kv => kv.Key, kv => GetSmeltTime(kv.Key.GetComponent<ItemModCookable>(), kv.Value));
            return smeltTimes;
        }

        private bool CanCook(ItemModCookable cookable, BaseOven oven)
        {
            return oven.cookingTemperature >= cookable.lowTemp && oven.cookingTemperature <= cookable.highTemp;
        }

        private float GetSmeltTime(ItemModCookable cookable, int amount)
        {
            float smeltTime = cookable.cookTime * amount;
            return smeltTime;
        }

        private int? GetTotalStacksOption(BasePlayer player, BaseOven oven)
        {
            PlayerOptions options = allPlayerOptions[player.userID];

            if (options.TotalStacks.ContainsKey(oven.ShortPrefabName))
                return options.TotalStacks[oven.ShortPrefabName];

            return null;
        }

        private void DestroyUI(BasePlayer player)
        {
            if (!openUis.ContainsKey(player.userID))
                return;

            string uiName = openUis[player.userID];

            if (openUis.Remove(player.userID))
                CuiHelper.DestroyUi(player, uiName);
        }

        private void DestroyOvenUI(BaseOven oven)
        {
            if (oven == null) throw new ArgumentNullException(nameof(oven));

            foreach (KeyValuePair<ulong, string> kv in openUis.ToDictionary(kv => kv.Key, kv => kv.Value))
            {
                BasePlayer player = BasePlayer.FindByID(kv.Key);

                BaseOven playerLootOven = player.inventory.loot?.entitySource as BaseOven;

                if (oven == playerLootOven)
                {
                    DestroyUI(player);
                    RemoveLooter(oven, player);
                }
            }
        }

        [ConsoleCommand("furnacesplitter.enabled")]
        private void ConsoleCommand_Toggle(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (!HasPermission(player))
            {
                player.ConsoleMessage(lang.GetMessage("nopermission", this, player.UserIDString));
                return;
            }

            if (!arg.HasArgs())
            {
                player.ConsoleMessage(GetEnabled(player).ToString());
                return;
            }

            bool enabled = arg.GetBool(0);
            SetEnabled(player, enabled);
            CreateUiIfFurnaceOpen(player);
        }

        [ConsoleCommand("furnacesplitter.totalstacks")]
        private void ConsoleCommand_TotalStacks(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            BaseOven lootSource = player.inventory.loot?.entitySource as BaseOven;

            if (!HasPermission(player))
            {
                player.ConsoleMessage(lang.GetMessage("nopermission", this, player.UserIDString));
                return;
            }

            if (lootSource == null || !compatibleOvens.Contains(lootSource.ShortPrefabName))
            {
                player.ConsoleMessage(lang.GetMessage("lootsource_invalid", this, player.UserIDString));
                return;
            }

            string ovenName = lootSource.ShortPrefabName;
            PlayerOptions playerOption = allPlayerOptions[player.userID];

            if (playerOption.TotalStacks.ContainsKey(ovenName))
            {
                if (!arg.HasArgs())
                {
                    player.ConsoleMessage(playerOption.TotalStacks[ovenName].ToString());
                }
                else
                {
                    int newValue = (int)Mathf.Clamp(arg.GetInt(0), 0, lootSource.inventory.capacity);
                    playerOption.TotalStacks[ovenName] = newValue;
                }
            }
            else
            {
                Debug.LogWarning("[FurnaceSplitter] Unsupported furnace '" + ovenName + "'");
                player.ConsoleMessage(lang.GetMessage("unsupported_furnace", this, player.UserIDString));
            }

            CreateUiIfFurnaceOpen(player);
        }

        [ConsoleCommand("furnacesplitter.trim")]
        private void ConsoleCommand_Trim(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            BaseOven lootSource = player.inventory.loot?.entitySource as BaseOven;

            if (!HasPermission(player))
            {
                player.ConsoleMessage(lang.GetMessage("nopermission", this, player.UserIDString));
                return;
            }

            if (lootSource == null || !compatibleOvens.Contains(lootSource.ShortPrefabName))
            {
                player.ConsoleMessage(lang.GetMessage("lootsource_invalid", this, player.UserIDString));
                return;
            }

            OvenInfo ovenInfo = GetOvenInfo(lootSource);
            var fuelSlots = lootSource.inventory.itemList.Where(item => item.info == lootSource.fuelType).ToList();
            int totalFuel = fuelSlots.Sum(item => item.amount);
            int toRemove = (int)Math.Floor(totalFuel - ovenInfo.FuelNeeded);

            if (toRemove <= 0)
                return;

            foreach (Item fuelItem in fuelSlots)
            {
                int toTake = Math.Min(fuelItem.amount, toRemove);
                toRemove -= toTake;

                Vector3 dropPosition = player.GetDropPosition();
                Vector3 dropVelocity = player.GetDropVelocity();

                if (toTake >= fuelItem.amount)
                {
                    if (!player.inventory.GiveItem(fuelItem))
                        fuelItem.Drop(dropPosition, dropVelocity, Quaternion.identity);
                }
                else
                {
                    Item splitItem = fuelItem.SplitItem(toTake);
                    if (!player.inventory.GiveItem(splitItem))
                        splitItem.Drop(dropPosition, dropVelocity, Quaternion.identity);
                }

                if (toRemove <= 0)
                    break;
            }
        }

        private bool HasPermission(BasePlayer player)
        {
            return permission.UserHasPermission(player.UserIDString, permUse);
        }

        #region Exposed plugin methods

        [HookMethod("MoveSplitItem")]
        public string Hook_MoveSplitItem(Item item, BaseOven oven, int totalSlots)
        {
            MoveResult result = MoveSplitItem(item, oven, totalSlots);
            return result.ToString();
        }

        [HookMethod("GetOvenInfo")]
        public JObject Hook_GetOvenInfo(BaseOven oven)
        {
            OvenInfo ovenInfo = GetOvenInfo(oven);
            return JObject.FromObject(ovenInfo);
        }

        #endregion Exposed plugin methods
    }
}