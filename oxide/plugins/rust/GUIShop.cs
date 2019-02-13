using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/********************************************************
 *
 * Credits to Nogrod and Reneb for the original plugin.
 * TODO Fix a few commands here and there.
 * 
 *******************************************************/

namespace Oxide.Plugins
{
    [Info("GUI Shop", "Default", "1.5.7")]
    [Description("GUI shop based on Economics, with NPC support")]
    public class GUIShop : RustPlugin
    {
        private const string ShopOverlayName = "ShopOverlay";
        private const string ShopContentName = "ShopContent";
        private const string ShopDescOverlay = "ShopDescOverlay";
        public const string BlockAllow = "guishop.allow";
        private readonly int[] steps = { 1, 10, 100, 1000 };
        int playersMask;

        //////////////////////////////////////////////////////////////////////////////////////
        // References ////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

        [PluginReference]
        Plugin Economics, Kits, ImageCache;

        private void OnServerInitialized()
        {
            permission.RegisterPermission(BlockAllow, this);
            displaynameToShortname.Clear();
            foreach (var itemdef in ItemManager.itemList)
            {
                var displayName = itemdef.displayName.english.ToLower();
                if (!displaynameToShortname.ContainsKey(displayName))
                    displaynameToShortname.Add(displayName, itemdef.shortname);
                if (string.IsNullOrEmpty(IconUrl)) continue;
                ImageCache?.CallHook("Add", string.Format(IconUrl, itemdef.shortname));
            }

            var shopCategories = DefaultShopCategories();
            CheckCfg("Shop - Shop Categories", ref shopCategories);
            foreach (var shopCategory in shopCategories)
            {
                var itemdata = shopCategory.Value as Dictionary<string, object>;
                if (itemdata == null) continue;
                try
                {
                    var data = new ItemData();
                    object obj;
                    if (itemdata.TryGetValue("item", out obj))
                    {
                        var itemname = ((string)obj).ToLower();
                        if (displaynameToShortname.ContainsKey(itemname))
                            itemname = displaynameToShortname[itemname];
                        var definition = ItemManager.FindItemDefinition(itemname);
                        if (definition == null)
                        {
                            Puts("ShopCategory: {0} Unknown item: {1}", shopCategory.Key, itemname);
                            continue;
                        }
                        data.Shortname = definition.shortname;
                    }
                    if (itemdata.TryGetValue("cmd", out obj))
                        data.Cmd = ((List<object>)obj).ConvertAll(c => (string)c);
                    double value;
                    if (itemdata.TryGetValue("cooldown", out obj) && double.TryParse((string)obj, out value))
                        data.Cooldown = value;
                    if (itemdata.TryGetValue("buy", out obj) && double.TryParse((string)obj, out value))
                        data.Buy = value;
                    if (itemdata.TryGetValue("sell", out obj) && double.TryParse((string)obj, out value))
                        data.Sell = value;
                    if (itemdata.TryGetValue("fixed", out obj))
                        data.Fixed = Convert.ToBoolean(obj);
                    if (itemdata.TryGetValue("img", out obj))
                        data.Img = (string)obj;
                    ShopCategories[shopCategory.Key] = data;
                }
                catch (Exception e)
                {
                    Puts("Failed to load ShopCategory: {0} Error: {1}", shopCategory.Key, e.Message);
                }
            }
            if (configChanged) SaveConfig();

            foreach (var itemData in ShopCategories.Values)
            {
                if (string.IsNullOrEmpty(itemData.Img)) continue;
                ImageCache?.CallHook("Add", itemData.Img);
            }
            if (!Economics) PrintWarning("Economics plugin not found. " + Name + " will not function!");
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // Configs Manager ///////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

        private void LoadDefaultConfig() { }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
            {
                Config[Key] = var;
                configChanged = true;
            }
        }

        class ItemData
        {
            public string Shortname { get; set; }
            public double Cooldown { get; set; }
            public double Buy { get; set; } = -1;
            public double Sell { get; set; } = -1;
            public bool Fixed { get; set; }
            public List<string> Cmd { get; set; }
            public string Img { get; set; }
        }

        private Dictionary<string, ItemData> ShopCategories = new Dictionary<string, ItemData>();
        private Dictionary<string, object> Shops = DefaultShops();
        private bool Balance;
        private string IconUrl = string.Empty;

        string MessageShowNoEconomics = "Couldn't get informations out of Economics. Is it installed?";
        string MessageBought = "You've successfully bought {0}x {1}";
        string MessageSold = "You've successfully sold {0}x {1}";
        string MessageErrorCooldown = "This item has a cooldown of {0} seconds.";
        string MessageErrorCooldownAmount = "This item has a cooldown and amount is limited to 1.";
        string MessageErrorInventoryFull = "Your inventory is full.";
        string MessageErrorInventorySlots = "Your inventory needs {0} free slots.";
        string MessageErrorNoShop = "This shop doesn't seem to exist.";
        string MessageErrorNoActionShop = "You are not allowed to {0} in this shop";
        string MessageErrorNoNPC = "The NPC owning this shop was not found around you";
        string MessageErrorNoActionItem = "You are not allowed to {0} this item here";
        string MessageErrorItemItem = "WARNING: The admin didn't set this item properly! (item)";
        string MessageErrorItemNoValid = "WARNING: It seems like it's not a valid item";
        string MessageErrorRedeemKit = "WARNING: There was an error while giving you this kit";
        string MessageErrorBuyCmd = "Can't buy multiple";
        string MessageErrorBuyPrice = "WARNING: No buy price was given by the admin, you can't buy this item";
        string MessageErrorSellPrice = "WARNING: No sell price was given by the admin, you can't sell this item";
        string MessageErrorNotEnoughMoney = "You need {0} coins to buy {1} of {2}";
        string MessageErrorNotEnoughSell = "You don't have enough of this item.";
        string MessageErrorNotNothing = "You cannot buy nothing of this item.";
        string MessageErrorItemNoExist = "WARNING: The item you are trying to buy doesn't seem to exist";
        string MessageErrorNPCRange = "You may not use the chat shop. You might need to find the NPC Shops.";
        string MessageErrorBuildingBlocked = "You cannot shop while in building blocked area.";

        private void Init()
        {
            CheckCfg("Shop - Shop Icon Url", ref IconUrl);
            CheckCfg("Shop - Shop List", ref Shops);
            CheckCfg("Shop - Balance", ref Balance);
            CheckCfg("Message - Error - No Econonomics", ref MessageShowNoEconomics);
            CheckCfg("Message - Bought", ref MessageBought);
            CheckCfg("Message - Sold", ref MessageSold);
            CheckCfg("Message - Error - Cooldown", ref MessageErrorCooldown);
            CheckCfg("Message - Error - Cooldown Amount", ref MessageErrorCooldownAmount);
            CheckCfg("Message - Error - Invetory Full", ref MessageErrorInventoryFull);
            CheckCfg("Message - Error - Invetory Slots", ref MessageErrorInventorySlots);
            CheckCfg("Message - Error - No Shop", ref MessageErrorNoShop);
            CheckCfg("Message - Error - No Action In Shop", ref MessageErrorNoActionShop);
            CheckCfg("Message - Error - No NPC", ref MessageErrorNoNPC);
            CheckCfg("Message - Error - No Action Item", ref MessageErrorNoActionItem);
            CheckCfg("Message - Error - Item Not Set Properly", ref MessageErrorItemItem);
            CheckCfg("Message - Error - Item Not Valid", ref MessageErrorItemNoValid);
            CheckCfg("Message - Error - Redeem Kit", ref MessageErrorRedeemKit);
            CheckCfg("Message - Error - Command Multiple", ref MessageErrorBuyCmd);
            CheckCfg("Message - Error - No Buy Price", ref MessageErrorBuyPrice);
            CheckCfg("Message - Error - No Sell Price", ref MessageErrorSellPrice);
            CheckCfg("Message - Error - Not Enough Money", ref MessageErrorNotEnoughMoney);
            CheckCfg("Message - Error - Not Enough Items", ref MessageErrorNotEnoughSell);
            CheckCfg("Message - Error - Not Nothing", ref MessageErrorNotNothing);
            CheckCfg("Message - Error - Item Doesnt Exist", ref MessageErrorItemNoExist);
            CheckCfg("Message - Error - No Chat Shop", ref MessageErrorNPCRange);
            CheckCfg("Message - Error - Building Blocked", ref MessageErrorBuildingBlocked);
            if (configChanged) SaveConfig();
            LoadData();
        }

        private void LoadData()
        {
            cooldowns = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<string, double>>>(nameof(GUIShop) + "Cooldowns");
            buyed = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, ulong>>(nameof(GUIShop) + "Buyed");
            selled = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, ulong>>(nameof(GUIShop) + "Selled");
        }

        private void SaveData()
        {
            if (cooldowns != null)
                Interface.Oxide.DataFileSystem.WriteObject(nameof(GUIShop) + "Cooldowns", cooldowns);
            if (buyed != null)
                Interface.Oxide.DataFileSystem.WriteObject(nameof(GUIShop) + "Buyed", buyed);
            if (selled != null)
                Interface.Oxide.DataFileSystem.WriteObject(nameof(GUIShop) + "Selled", selled);
        }

        private void Unload()
        {
            SaveData();
        }
        private void OnServerSave()
        {
            SaveData();
        }
        private void OnServerShutdown()
        {
            SaveData();
        }

        private static int CurrentTime() { return Facepunch.Math.Epoch.Current; }

        //////////////////////////////////////////////////////////////////////////////////////
        // Default Shops for Tutorial purpoise ///////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////
        private static Dictionary<string, object> DefaultShops()
        {
            var shops = new Dictionary<string, object>
            {
                {
                    "commands", new Dictionary<string, object>
                    {
                        {"description", "You currently have {0} coins to spend in this commands shop"},
                        {"name", "Commands"},
                        {"buy", new List<object> {"Assault Rifle", "Bolt Action Rifle"}},
                        {"sell", new List<object> {"Assault Rifle", "Bolt Action Rifle"}}
                    }
                },
                {
                    "weapons", new Dictionary<string, object>
                    {
                        {"description", "You currently have {0} coins to spend in this weapons shop"},
                        {"name", "Weapons"},
                        {"buy", new List<object> {"Assault Rifle", "Bolt Action Rifle"}},
                        {"sell", new List<object> {"Assault Rifle", "Bolt Action Rifle"}}
                    }
                },
                {
                    "construction", new Dictionary<string, object>
                    {
                        {"description", "You currently have {0} coins to spend in this construction shop"},
                        {"name", "Construction"},
                        {"buy", new List<object> {"Assault Rifle", "Bolt Action Rifle"}},
                        {"sell", new List<object> {"Assault Rifle", "Bolt Action Rifle"}}
                    }
                },
                {
                    "resources", new Dictionary<string, object>
                    {
                        {"description", "You currently have {0} coins to spend in this resources shop"},
                        {"name", "Resource"},
                        {"buy", new List<object> {"Assault Rifle", "Bolt Action Rifle"}},
                        {"sell", new List<object> {"Assault Rifle", "Bolt Action Rifle"}}
                    }
                },
                {
                    "attire", new Dictionary<string, object>
                    {
                        {"description", "You currently have {0} coins to spend in this attire shop"},
                        {"name", "Clothing"},
                        {"buy", new List<object> {"Assault Rifle", "Bolt Action Rifle"}},
                        {"sell", new List<object> {"Assault Rifle", "Bolt Action Rifle"}}
                    }
                },
                {
                    "tools", new Dictionary<string, object>
                    {
                        {"description", "You currently have {0} coins to spend in this tools shop"},
                        {"name", "Tools"},
                        {"buy", new List<object> {"Assault Rifle", "Bolt Action Rifle"}},
                        {"sell", new List<object> {"Assault Rifle", "Bolt Action Rifle"}}
                    }
                },
                {
                    "medical", new Dictionary<string, object>
                    {
                        {"description", "You currently have {0} coins to spend in this medical shop"},
                        {"name", "Meds"},
                        {"buy", new List<object> {"Assault Rifle", "Bolt Action Rifle"}},
                        {"sell", new List<object> {"Assault Rifle", "Bolt Action Rifle"}}
                    }
                },
                {
                    "food", new Dictionary<string, object>
                    {
                        {"description", "You currently have {0} coins to spend in this food shop"},
                        {"name", "Food"},
                        {"buy", new List<object> {"BlueBerries"}},
                        {"sell", new List<object> {"Apple"}}
                    }
                },
                {
                    "ammunition", new Dictionary<string, object>
                    {
                        {"description", "You currently have {0} coins to spend in this ammunition shop"},
                        {"name", "Ammunition"},
                        {"buy", new List<object> {"Assault Rifle", "Bolt Action Rifle"}},
                        {"sell", new List<object> {"Assault Rifle", "Bolt Action Rifle"}}
                    }
                },
                {
                    "traps", new Dictionary<string, object>
                    {
                        {"description", "You currently have {0} coins to spend in this traps shop"},
                        {"name", "Trap"},
                        {"buy", new List<object> {"Assault Rifle", "Bolt Action Rifle"}},
                        {"sell", new List<object> {"Assault Rifle", "Bolt Action Rifle"}}
                    }
                },
                {
                    "items", new Dictionary<string, object>
                    {
                        {"description", "You currently have {0} coins to spend in this items market"},
                        {"name", "Item"},
                        {"buy", new List<object> {"Apple", "BlueBerries", "Assault Rifle", "Bolt Action Rifle"}},
                        {"sell", new List<object> {"Apple", "BlueBerries", "Assault Rifle", "Bolt Action Rifle"}}
                    }
                },
                {
                    "components", new Dictionary<string, object>
                    {
                        {"description", "You currently have {0} coins to spend in this components shop"},
                        {"name", "Item"},
                        {"buy", new List<object> {"Apple", "BlueBerries", "Assault Rifle", "Bolt Action Rifle"}},
                        {"sell", new List<object> {"Apple", "BlueBerries", "Assault Rifle", "Bolt Action Rifle"}}
                    }
                }
            };
            return shops;
        }
        private static Dictionary<string, object> DefaultShopCategories()
        {
            var dsc = new Dictionary<string, object>
            {
                {
                    "Assault Rifle", new Dictionary<string, object>
                    {
                        {"item", "assault rifle"},
                        {"buy", "10"},
                        {"sell", "8"}
                    }
                },
                {
                    "Bolt Action Rifle", new Dictionary<string, object>
                    {
                        {"item", "bolt action rifle"},
                        {"buy", "10"}, {"sell", "8"}
                    }
                },
                {
                    "Apple", new Dictionary<string, object>
                    {
                        {"item", "apple"},
                        {"buy", "1"},
                        {"sell", "1"}
                    }
                },
                {
                    "BlueBerries", new Dictionary<string, object>
                    {
                        {"item", "blueberries"},
                        {"buy", "1"},
                        {"sell", "1"}
                    }
                }
            };
            return dsc;
        }



        //////////////////////////////////////////////////////////////////////////////////////
        // Item Management ///////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////
        readonly Dictionary<string, string> displaynameToShortname = new Dictionary<string, string>();

        //////////////////////////////////////////////////////////////////////////////////////
        // Oxide Hooks ///////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

        private void Loaded()
        {
            playersMask = LayerMask.GetMask("Player (Server)");
        }

        private void OnUseNPC(BasePlayer npc, BasePlayer player)
        {
            if (!Shops.ContainsKey(npc.UserIDString)) return;
            ShowShop(player, npc.UserIDString, 0);
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // GUI ///////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

        private static CuiElementContainer CreateShopOverlay(string shopname)
        {
            return new CuiElementContainer
            {
                {
                    new CuiPanel
                    {
                        Image = {Color = "0.1 0.1 0.1 0.8"},
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                        CursorEnabled = true
                    },
                    new CuiElement().Parent,
                    ShopOverlayName
                },
                {
                    new CuiLabel
                    {
                        Text = {Text = shopname, FontSize = 30, Align = TextAnchor.MiddleCenter},
                        RectTransform = {AnchorMin = "0.3 0.8", AnchorMax = "0.7 0.9"}
                    },
                    ShopOverlayName
                },
                {
                    new CuiLabel
                    {
                        Text = {Text = "Item", FontSize = 20, Align = TextAnchor.MiddleLeft},
                        RectTransform = {AnchorMin = "0.2 0.6", AnchorMax = "0.4 0.65"}
                    },
                    ShopOverlayName
                },
                {
                    new CuiLabel
                    {
                        Text = {Text = "Buy", FontSize = 20, Align = TextAnchor.MiddleLeft},
                        RectTransform = {AnchorMin = "0.55 0.6", AnchorMax = "0.7 0.65"}
                    },
                    ShopOverlayName
                },
                {
                    new CuiLabel
                    {
                        Text = {Text = "Sell", FontSize = 20, Align = TextAnchor.MiddleLeft},
                        RectTransform = {AnchorMin = "0.75 0.6", AnchorMax = "0.9 0.65"}
                    },
                    ShopOverlayName
                },
                {
                    new CuiButton
                    {
                        Button = {Close = ShopOverlayName, Color = "0.5 0.5 0.5 0.2"},
                        RectTransform = {AnchorMin = "0.5 0.15", AnchorMax = "0.7 0.2"},
                        Text = {Text = "Close", FontSize = 20, Align = TextAnchor.MiddleCenter}
                    },
                    ShopOverlayName
                }
            };
        }

        private readonly CuiLabel shopDescription = new CuiLabel
        {
            Text = { Text = "{shopdescription}", FontSize = 15, Align = TextAnchor.MiddleCenter },
            RectTransform = { AnchorMin = "0.2 0.7", AnchorMax = "0.8 0.79" }
        };

        private CuiElementContainer CreateShopItemEntry(string price, float ymax, float ymin, string shop, string item, string color, bool sell, bool cooldown)
        {
            var container = new CuiElementContainer
            {
                {
                    new CuiLabel
                    {
                        Text = {Text = price, FontSize = 15, Align = TextAnchor.MiddleLeft},
                        RectTransform = {AnchorMin = $"{(sell ? 0.725 : 0.45)} {ymin}", AnchorMax = $"{(sell ? 0.755 : 0.5)} {ymax}"}
                    },
                    ShopContentName
                }
            };
            for (var i = 0; i < steps.Length; i++)
            {
                container.Add(new CuiButton
                {
                    Button = { Command = $"shop.{(sell ? "sell" : "buy")} {shop} {item} {steps[i]}", Color = color },
                    RectTransform = { AnchorMin = $"{(sell ? 0.775 : 0.5) + i * 0.03 + 0.001} {ymin}", AnchorMax = $"{(sell ? 0.805 : 0.53) + i * 0.03 - 0.001} {ymax}" },
                    Text = { Text = steps[i].ToString(), FontSize = 15, Align = TextAnchor.MiddleCenter }
                }, ShopContentName);
                //if (cooldown) break;
            }
            if (!cooldown)
            {
                container.Add(new CuiButton
                {
                    Button = { Command = $"shop.{(sell ? "sell" : "buy")} {shop} {item} all", Color = color },
                    RectTransform = { AnchorMin = $"{(sell ? 0.775 : 0.5) + steps.Length * 0.03 + 0.001} {ymin}", AnchorMax = $"{(sell ? 0.805 : 0.53) + steps.Length * 0.03 - 0.001} {ymax}" },
                    Text = { Text = "All", FontSize = 15, Align = TextAnchor.MiddleCenter }
                }, ShopContentName);
            }
            return container;
        }

        private CuiElementContainer CreateShopItemIcon(string name, float ymax, float ymin, ItemData data)
        {
            string url = null;
            if (!string.IsNullOrEmpty(data.Img))
                url = data.Img;
            else if (!string.IsNullOrEmpty(data.Shortname))
                url = string.Format(IconUrl, data.Shortname);
            var label = new CuiLabel
            {
                Text = { Text = name, FontSize = 15, Align = TextAnchor.MiddleLeft },
                RectTransform = { AnchorMin = $"0.1 {ymin}", AnchorMax = $"0.3 {ymax}" }
            };
            if (string.IsNullOrEmpty(url))
                return new CuiElementContainer
                {
                    {
                        label,
                        ShopContentName
                    }
                };
            var rawImage = new CuiRawImageComponent();
            if (url.StartsWith("http") || url.StartsWith("file"))
            {
                var id = (string)ImageCache?.CallHook("Get", url);
                if (!string.IsNullOrEmpty(id))
                    rawImage.Png = id;
                else
                    rawImage.Url = url;
                rawImage.Sprite = "assets/content/textures/generic/fulltransparent.tga";
            }
            else
                rawImage.Sprite = url;
            var container = new CuiElementContainer
            {
                {
                    label,
                    ShopContentName
                },
                new CuiElement
                {
                    Parent = ShopContentName,
                    Components =
                    {
                        rawImage,
                        new CuiRectTransformComponent {AnchorMin = $"0.05 {ymin}", AnchorMax = $"0.08 {ymax}"}
                    }
                }
            };
            return container;
        }

        /* private static CuiElementContainer CreateShopChangePage(string currentshop, int shoppageminus, int shoppageplus)
         {
             return new CuiElementContainer
             {
                 {
                     new CuiButton
                     {
                         Button = {Command = $"shop.show {currentshop} {shoppageminus}", Color = "0.5 0.5 0.5 0.2"},
                         RectTransform = {AnchorMin = "0.2 0.15", AnchorMax = "0.3 0.2"},
                         Text = {Text = "<<", FontSize = 20, Align = TextAnchor.MiddleCenter}
                     },
                     ShopOverlayName,
                     "ButtonBack"
                 },
                 {
                     new CuiButton
                     {
                         Button = {Command = $"shop.show {currentshop} {shoppageplus}", Color = "0.5 0.5 0.5 0.2"},
                         RectTransform = {AnchorMin = "0.35 0.15", AnchorMax = "0.45 0.2"},
                         Text = {Text = ">>", FontSize = 20, Align = TextAnchor.MiddleCenter}
                     },
                     ShopOverlayName,
                     "ButtonForward"
                 }
             };
         } */

        //TODO MMMEEERRGGGGEEEE
        private static CuiElementContainer CreateShopChangePage(string currentshop, int shoppageminus, int shoppageplus)
        {
            return new CuiElementContainer
            {
                {
                    new CuiButton
                    {
                        Button = {Command = $"shop.show {currentshop} {shoppageminus}", Color = "0.5 0.5 0.5 0.2"},
                        RectTransform = {AnchorMin = "0.2 0.12", AnchorMax = "0.3 0.17"},
                        Text =
                        {
                            Text = "<<", FontSize = 20, Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf"
                        }
                    },
                    ShopOverlayName,
                    "ButtonBack"
                },
                {
                    new CuiButton
                    {
                        Button = {Command = $"shop.show {currentshop} {shoppageplus}", Color = "0.5 0.5 0.5 0.2"},
                        RectTransform = {AnchorMin = "0.35 0.12", AnchorMax = "0.45 0.17"},
                        Text =
                        {
                            Text = ">>", FontSize = 20, Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf"
                        }
                    },
                    ShopOverlayName,
                    "ButtonForward"
                },
                {
                    new CuiButton
                    {
                        Button = {Command = $"shop.show components {shoppageminus}", Color = "0.5 0.5 0.5 0.5"},
                        RectTransform = {AnchorMin = "0.90 0.78", AnchorMax = "0.97 0.83"},
                        Text =
                        {
                            Text = $"Components", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf",
                            FontSize = 14
                        }
                    },
                    ShopOverlayName,
                    "Components"
                },
                {
                    new CuiButton
                    {
                        Button = {Command = $"shop.show traps {shoppageminus}", Color = "0.5 0.5 0.5 0.5"},
                        RectTransform = {AnchorMin = "0.82 0.78", AnchorMax = "0.89 0.83"},
                        Text =
                        {
                            Text = $"Traps", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf",
                            FontSize = 14
                        }
                    },
                    ShopOverlayName,
                    "Traps"
                },
                {
                    new CuiButton
                    {
                        Button = {Command = $"shop.show ammunition {shoppageminus}", Color = "0.5 0.5 0.5 0.5"},
                        RectTransform = {AnchorMin = "0.74 0.78", AnchorMax = "0.81 0.83"},
                        Text =
                        {
                            Text = $"Ammunition", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf",
                            FontSize = 14
                        }
                    },
                    ShopOverlayName,
                    "Ammunition"
                },
                {
                    new CuiButton
                    {
                        Button = {Command = $"shop.show food {shoppageminus}", Color = "0.5 0.5 0.5 0.5"},
                        RectTransform = {AnchorMin = "0.66 0.78", AnchorMax = "0.73 0.83"},
                        Text =
                        {
                            Text = $"Food", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf",
                            FontSize = 14
                        }
                    },
                    ShopOverlayName,
                    "Food"
                },
                {
                    new CuiButton
                    {
                        Button = {Command = $"shop.show medical {shoppageminus}", Color = "0.5 0.5 0.5 0.5"},
                        RectTransform = {AnchorMin = "0.58 0.78", AnchorMax = "0.65 0.83"},
                        Text =
                        {
                            Text = $"Medical", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf",
                            FontSize = 14
                        }
                    },
                    ShopOverlayName,
                    "Medical"
                },
                {
                    new CuiButton
                    {
                        Button = {Command = $"shop.show tools {shoppageminus}", Color = "0.5 0.5 0.5 0.5"},
                        RectTransform = {AnchorMin = "0.50 0.78", AnchorMax = "0.57 0.83"},
                        Text =
                        {
                            Text = $"Tools", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf",
                            FontSize = 14
                        }
                    },
                    ShopOverlayName,
                    "Tools"
                },
                {
                    new CuiButton
                    {
                        Button = {Command = $"shop.show attire {shoppageminus}", Color = "0.5 0.5 0.5 0.5"},
                        RectTransform = {AnchorMin = "0.42 0.78", AnchorMax = "0.49 0.83"},
                        Text =
                        {
                            Text = $"Attire", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf",
                            FontSize = 14
                        }
                    },
                    ShopOverlayName,
                    "Attire"
                },
                {
                    new CuiButton
                    {
                        Button = {Command = $"shop.show resources {shoppageminus}", Color = "0.5 0.5 0.5 0.5"},
                        RectTransform = {AnchorMin = "0.34 0.78", AnchorMax = "0.41 0.83"},
                        Text =
                        {
                            Text = $"Resources", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf",
                            FontSize = 14
                        }
                    },
                    ShopOverlayName,
                    "Resources"
                },
                {
                    new CuiButton
                    {
                        Button = {Command = $"shop.show items {shoppageminus}", Color = "0.5 0.5 0.5 0.5"},
                        RectTransform = {AnchorMin = "0.26 0.78", AnchorMax = "0.33 0.83"},
                        Text =
                        {
                            Text = $"Items", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf",
                            FontSize = 14
                        }
                    },
                    ShopOverlayName,
                    "Items"
                },
                {
                    new CuiButton
                    {
                        Button = {Command = $"shop.show construction {shoppageminus}", Color = "0.5 0.5 0.5 0.5"},
                        RectTransform = {AnchorMin = "0.18 0.78", AnchorMax = "0.25 0.83"},
                        Text =
                        {
                            Text = $"Construction", Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf", FontSize = 14
                        }
                    },
                    ShopOverlayName,
                    "Construction"
                },
                {
                    new CuiButton
                    {
                        Button = {Command = $"shop.show weapons {shoppageminus}", Color = "0.5 0.5 0.5 0.5"},
                        RectTransform = {AnchorMin = "0.10 0.78", AnchorMax = "0.17 0.83"},
                        Text =
                        {
                            Text = $"Weapons", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf",
                            FontSize = 14
                        }
                    },
                    ShopOverlayName,
                    "Weapons"
                },
                {
                    new CuiButton
                    {
                        Button = {Command = $"shop.show commands {shoppageminus}", Color = "0.5 0.5 0.5 0.5"},
                        RectTransform = {AnchorMin = "0.02 0.78", AnchorMax = "0.09 0.83"},
                        Text =
                        {
                            Text = $"Commands", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf",
                            FontSize = 14
                        }
                    },
                    ShopOverlayName,
                    "Commands"
                }
            };
        }


        readonly Hash<ulong, int> shopPage = new Hash<ulong, int>();
        private Dictionary<ulong, Dictionary<string, double>> cooldowns;
        private Dictionary<string, ulong> buyed;
        private Dictionary<string, ulong> selled;
        private bool configChanged;

        private void ShowShop(BasePlayer player, string shopid, int from = 0, bool fullPaint = true, bool refreshMoney = false)
        {
            shopPage[player.userID] = from;
            object shopObj;
            if (!Shops.TryGetValue(shopid, out shopObj))
            {
                SendReply(player, MessageErrorNoShop);
                return;
            }
            if (Economics == null)
            {
                SendReply(player, MessageShowNoEconomics);
                return;
            }
            var playerCoins = (double)Economics.CallHook("Balance", player.UserIDString);

            var shop = (Dictionary<string, object>)shopObj;

            shopDescription.Text.Text = string.Format((string)shop["description"], playerCoins);

            if (refreshMoney)
            {
                CuiHelper.DestroyUi(player, ShopDescOverlay);
                CuiHelper.AddUi(player, new CuiElementContainer { { shopDescription, ShopOverlayName, ShopDescOverlay } });
                return;
            }
            DestroyUi(player, fullPaint);
            CuiElementContainer container;
            if (fullPaint)
            {
                container = CreateShopOverlay((string)shop["name"]);
                container.Add(shopDescription, ShopOverlayName, ShopDescOverlay);
            }
            else
                container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0.2", AnchorMax = "1 0.6" }
            }, ShopOverlayName, ShopContentName);
            if (from < 0)
            {
                CuiHelper.AddUi(player, container);
                return;
            }

            var itemslist = new Dictionary<string, Dictionary<string, bool>>();
            object type;
            if (shop.TryGetValue("sell", out type))
            {
                foreach (string itemname in (List<object>)type)
                {
                    Dictionary<string, bool> itemEntry;
                    if (!itemslist.TryGetValue(itemname, out itemEntry))
                        itemslist[itemname] = itemEntry = new Dictionary<string, bool>();
                    itemEntry["sell"] = true;
                }
            }
            if (shop.TryGetValue("buy", out type))
            {
                foreach (string itemname in (List<object>)type)
                {
                    Dictionary<string, bool> itemEntry;
                    if (!itemslist.TryGetValue(itemname, out itemEntry))
                        itemslist[itemname] = itemEntry = new Dictionary<string, bool>();
                    itemEntry["buy"] = true;
                }
            }
            var current = 0;
            foreach (var pair in itemslist)
            {
                ItemData data;
                if (!ShopCategories.TryGetValue(pair.Key, out data)) continue;

                if (current >= from && current < from + 7)
                {
                    var pos = 0.85f - 0.125f * (current - from);

                    var cooldown = data.Cooldown > 0;
                    var name = pair.Key;
                    if (cooldown)
                        name += $" ({FormatTime((long)data.Cooldown)})";
                    container.AddRange(CreateShopItemIcon(name, pos + 0.125f, pos, data));
                    var buyed = false;
                    if (cooldown)
                    {
                        Dictionary<string, double> itemCooldowns;
                        double itemCooldown;
                        if (cooldowns.TryGetValue(player.userID, out itemCooldowns)
                            && itemCooldowns.TryGetValue(pair.Key, out itemCooldown)
                            && itemCooldown > CurrentTime())
                        {
                            buyed = true;
                            container.Add(new CuiLabel
                            {
                                Text = { Text = GetBuyPrice(data).ToString(), FontSize = 15, Align = TextAnchor.MiddleLeft },
                                RectTransform = { AnchorMin = $"0.45 {pos}", AnchorMax = $"0.5 {pos + 0.125f}" }
                            }, ShopContentName);
                            container.Add(new CuiLabel
                            {
                                Text = { Text = FormatTime((long)(itemCooldown - CurrentTime())), FontSize = 15, Align = TextAnchor.MiddleLeft },
                                RectTransform = { AnchorMin = $"0.5 {pos}", AnchorMax = $"0.6 {pos + 0.125f}" }
                            }, ShopContentName);
                            //current++;
                            //continue;
                        }
                    }
                    if (!buyed && pair.Value.ContainsKey("buy"))
                        container.AddRange(CreateShopItemEntry(GetBuyPrice(data).ToString(), pos + 0.125f, pos, $"'{shopid}'", $"'{pair.Key}'", "0 0.6 0 0.1", false, cooldown));
                    if (pair.Value.ContainsKey("sell"))
                        container.AddRange(CreateShopItemEntry(GetSellPrice(data).ToString(), pos + 0.125f, pos, $"'{shopid}'", $"'{pair.Key}'", "1 0 0 0.1", true, cooldown));
                }
                current++;
            }
            var minfrom = from <= 7 ? 0 : from - 7;
            var maxfrom = from + 7 >= current ? from : from + 7;
            container.AddRange(CreateShopChangePage(shopid, minfrom, maxfrom));
            CuiHelper.AddUi(player, container);
        }

        double GetBuyPrice(ItemData data)
        {
            if (!Balance || data.Fixed) return data.Buy;
            return Math.Round(data.Buy * GetFactor(data), 2);
        }

        double GetSellPrice(ItemData data)
        {
            if (!Balance || data.Fixed) return data.Sell;
            return Math.Round(data.Sell * GetFactor(data), 2);
        }

        double GetFactor(ItemData data)
        {
            if (data.Shortname == null) return 1;
            var itemname = data.Shortname;
            ulong buy;
            if (!buyed.TryGetValue(itemname, out buy))
                buy = 1;
            ulong sell;
            if (!selled.TryGetValue(itemname, out sell))
                sell = 1;
            return Math.Min(Math.Max(buy / (double)sell, .25), 4);
        }

        private static string FormatTime(long seconds)
        {
            var timespan = TimeSpan.FromSeconds(seconds);
            return string.Format(timespan.TotalHours >= 1 ? "{2:00}:{0:00}:{1:00}" : "{0:00}:{1:00}", timespan.Minutes, timespan.Seconds, Math.Floor(timespan.TotalHours));
        }
        //////////////////////////////////////////////////////////////////////////////////////
        // Shop Functions ////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

        object CanDoAction(BasePlayer player, string shop, string item, string ttype)
        {
            var shopdata = (Dictionary<string, object>)Shops[shop];
            if (!shopdata.ContainsKey(ttype))
                return string.Format(MessageErrorNoActionShop, ttype);
            var actiondata = (List<object>)shopdata[ttype];
            if (!actiondata.Contains(item))
                return string.Format(MessageErrorNoActionItem, ttype);
            return true;
        }

        bool CanFindNPC(Vector3 pos, string npcid)
        {
            //return Physics.OverlapSphere(pos, 3f, playersMask).Select(col => col.GetComponentInParent<BasePlayer>()).Any(player => player != null && player.UserIDString == npcid);
            return true; //TODO Make work once again
        }

        object CanShop(BasePlayer player, string shopname)
        {
            if (!Shops.ContainsKey(shopname)) return MessageErrorNoShop;
            if (shopname != "commands" && !CanFindNPC(player.transform.position, shopname))
                return MessageErrorNoNPC;
            return true;
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // Buy Functions /////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

        object TryShopBuy(BasePlayer player, string shop, string item, int amount)
        {
            if (amount <= 0) return false;
            object success = CanShop(player, shop);
            if (success is string) return success;
            success = CanDoAction(player, shop, item, "buy");
            if (success is string) return success;
            success = CanBuy(player, item, amount);
            if (success is string) return success;
            success = TryGive(player, item, amount);
            if (success is string) return success;
            var data = ShopCategories[item];
            var tryShopBuy = Economics?.CallHook("Withdraw", player.UserIDString, GetBuyPrice(data) * amount);
            if (tryShopBuy == null || tryShopBuy is bool && !(bool)tryShopBuy)
                return MessageShowNoEconomics;
            if (data.Cooldown > 0)
            {
                Dictionary<string, double> itemCooldowns;
                if (!cooldowns.TryGetValue(player.userID, out itemCooldowns))
                    cooldowns[player.userID] = itemCooldowns = new Dictionary<string, double>();
                itemCooldowns[item] = CurrentTime() + data.Cooldown /** amount*/;
            }
            if (!string.IsNullOrEmpty(data.Shortname))
            {
                ulong count;
                buyed.TryGetValue(data.Shortname, out count);
                buyed[data.Shortname] = count + (ulong)amount;
            }
            return tryShopBuy;
        }

        object TryGive(BasePlayer player, string item, int amount)
        {
            var data = ShopCategories[item];
            if (!string.IsNullOrEmpty(data.Shortname))
            {
                if (player.inventory.containerMain.IsFull()) return MessageErrorInventoryFull;
                object iskit = Kits?.CallHook("isKit", data.Shortname);
                if (iskit is bool && (bool)iskit)
                {
                    object successkit = Kits.CallHook("GiveKit", player, data.Shortname);
                    if (successkit is bool && !(bool)successkit) return MessageErrorRedeemKit;
                    Puts("Player: {0} Buyed Kit: {1}", player.displayName, data.Shortname);
                    return true;
                }
                object success = GiveItem(player, data, amount, player.inventory.containerMain);
                if (success is string) return success;
                Puts("Player: {0} Buyed Item: {1} x{2}", player.displayName, data.Shortname, amount);
            }
            if (data.Cmd != null)
            {
                var cmds = data.Cmd;
                for (var i = 0; i < cmds.Count; i++)
                {
                    var c = cmds[i]
                        .Replace("$player.id", player.UserIDString)
                        .Replace("$player.name", player.displayName)
                        .Replace("$player.x", player.transform.position.x.ToString())
                        .Replace("$player.y", player.transform.position.y.ToString())
                        .Replace("$player.z", player.transform.position.z.ToString());
                    if (c.StartsWith("shop.show close", StringComparison.OrdinalIgnoreCase))
                        NextTick(() => ConsoleSystem.Run(ConsoleSystem.Option.Server, c));
                    else
                        ConsoleSystem.Run(ConsoleSystem.Option.Server, c);
                }
                Puts("Player: {0} Buyed command: {1}", player.displayName, item);
            }
            return true;
        }

        private int GetAmountBuy(BasePlayer player, string item)
        {
            if (player.inventory.containerMain.IsFull()) return 0;
            var data = ShopCategories[item];
            var definition = ItemManager.FindItemDefinition(data.Shortname);
            if (definition == null) return 0;
            var stack = definition.stackable;
            if (stack < 1) stack = 1;
            var freeSlots = player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count;
            var slotAmount = freeSlots * stack;
            var balanceAmount = (int)((double)Economics.CallHook("Balance", player.UserIDString) / GetBuyPrice(data));
            return slotAmount < balanceAmount ? slotAmount : balanceAmount;
        }

        private object GiveItem(BasePlayer player, ItemData data, int amount, ItemContainer pref)
        {
            if (amount <= 0) return MessageErrorNotNothing;
            var definition = ItemManager.FindItemDefinition(data.Shortname);
            if (definition == null) return MessageErrorItemNoExist;
            int stack = definition.stackable;
            if (stack < 1) stack = 1;
            if (pref.itemList.Count + Math.Ceiling(amount / (float)stack) > pref.capacity)
                return string.Format(MessageErrorInventorySlots, Math.Ceiling(amount / (float)stack));
            for (var i = amount; i > 0; i = i - stack)
            {
                var giveamount = i >= stack ? stack : i;
                if (giveamount < 1) return true;
                var item = ItemManager.CreateByItemID(definition.itemid, giveamount);
                if (!player.inventory.GiveItem(item, pref))
                    item.Remove(0);
            }
            return true;
        }
        object CanBuy(BasePlayer player, string item, int amount)
        {
            if (Economics == null) return MessageShowNoEconomics;
            if (!ShopCategories.ContainsKey(item)) return MessageErrorItemNoValid;

            var data = ShopCategories[item];
            if (data.Buy < 0) return MessageErrorBuyPrice;
            if (data.Cmd != null && amount > 1) return MessageErrorBuyCmd;
            var buyprice = GetBuyPrice(data);

            var playerCoins = (double)Economics.CallHook("Balance", player.UserIDString);
            if (playerCoins < buyprice * amount)
                return string.Format(MessageErrorNotEnoughMoney, buyprice * amount, amount, item);
            if (data.Cooldown > 0)
            {
                //if (data.Cmd != null && amount > 1)
                //    return MessageErrorCooldownAmount;
                Dictionary<string, double> itemCooldowns;
                double itemCooldown;
                if (cooldowns.TryGetValue(player.userID, out itemCooldowns)
                    && itemCooldowns.TryGetValue(item, out itemCooldown)
                    && itemCooldown > CurrentTime())
                {
                    return string.Format(MessageErrorCooldown, FormatTime((long)(itemCooldown - CurrentTime())));
                }
            }
            return true;
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // Sell Functions ////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

        object TryShopSell(BasePlayer player, string shop, string item, int amount)
        {
            object success = CanShop(player, shop);
            if (success is string) return success;
            success = CanDoAction(player, shop, item, "sell");
            if (success is string) return success;
            success = CanSell(player, item, amount);
            if (success is string) return success;
            success = TrySell(player, item, amount);
            if (success is string) return success;
            var data = ShopCategories[item];
            var itemdata = ShopCategories[item];
            var cooldown = Convert.ToDouble(itemdata.Cooldown);
            if (cooldown > 0)
            {
                Dictionary<string, double> itemCooldowns;
                if (!cooldowns.TryGetValue(player.userID, out itemCooldowns))
                    cooldowns[player.userID] = itemCooldowns = new Dictionary<string, double>();
                itemCooldowns[item] = CurrentTime() + cooldown;
            }
            Economics?.CallHook("Deposit", player.UserIDString, GetSellPrice(data) * amount);
            if (!string.IsNullOrEmpty(data.Shortname))
            {
                ulong count;
                selled.TryGetValue(data.Shortname, out count);
                selled[data.Shortname] = count + (ulong)amount;
            }
            return true;
        }
        object TrySell(BasePlayer player, string item, int amount)
        {
            var data = ShopCategories[item];
            if (string.IsNullOrEmpty(data.Shortname)) return MessageErrorItemItem;
            object iskit = Kits?.CallHook("isKit", data.Shortname);

            if (iskit is bool && (bool)iskit) return "You can't sell kits";
            object success = TakeItem(player, data, amount);
            if (success is string) return success;
            Puts("Player: {0} Selled Item: {1} x{2}", player.displayName, data.Shortname, amount);
            return true;
        }

        private int GetAmountSell(BasePlayer player, string item)
        {
            var data = ShopCategories[item];
            var definition = ItemManager.FindItemDefinition(data.Shortname);
            //Puts("Def: {0}", definition?.shortname);
            if (definition == null) return 0;
            //Puts("GetAmount: {0} {1}", definition.shortname, player.inventory.containerMain.GetAmount(definition.itemid, true));
            return player.inventory.containerMain.GetAmount(definition.itemid, true);
        }
        private object TakeItem(BasePlayer player, ItemData data, int amount)
        {
            if (amount <= 0) return MessageErrorNotEnoughSell;
            var definition = ItemManager.FindItemDefinition(data.Shortname);
            if (definition == null) return MessageErrorItemNoExist;

            var pamount = player.inventory.GetAmount(definition.itemid);
            if (pamount < amount) return MessageErrorNotEnoughSell;
            player.inventory.Take(null, definition.itemid, amount);
            return true;
        }

        object CanSell(BasePlayer player, string item, int amount)
        {
            if (!ShopCategories.ContainsKey(item)) return MessageErrorItemNoValid;
            var itemdata = ShopCategories[item];
            if (itemdata.Sell < 0) return MessageErrorSellPrice;
            if (itemdata.Cooldown > 0)
            {
                /*if (amount > 1)
                    return MessageErrorCooldownAmount;*/
                Dictionary<string, double> itemCooldowns;
                double itemCooldown;
                if (cooldowns.TryGetValue(player.userID, out itemCooldowns)
                    && itemCooldowns.TryGetValue(item, out itemCooldown)
                    && itemCooldown > CurrentTime())
                {
                    return string.Format(MessageErrorCooldown, FormatTime((long)(itemCooldown - CurrentTime())));
                }
            }
            return true;
        }

        private void DestroyUi(BasePlayer player, bool full = false)
        {
            CuiHelper.DestroyUi(player, ShopContentName);
            CuiHelper.DestroyUi(player, "ButtonForward");
            CuiHelper.DestroyUi(player, "ButtonBack");
            if (!full) return;
            CuiHelper.DestroyUi(player, ShopDescOverlay);
            CuiHelper.DestroyUi(player, ShopOverlayName);
        }
        //////////////////////////////////////////////////////////////////////////////////////
        // Chat Commands /////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////
        [ChatCommand("shop")]
        private void cmdShop(BasePlayer player, string command, string[] args)
        {
            if (!player.CanBuild())
            {
                if (permission.UserHasPermission(player.UserIDString, BlockAllow))
                    ShowShop(player, "commands");
                else
                    SendReply(player, MessageErrorBuildingBlocked);
                return;
            }

            if (!Shops.ContainsKey("commands"))
            {
                SendReply(player, MessageErrorNPCRange);
                return;
            }
            
            ShowShop(player, "commands");
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // Console Commands //////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////
        [ConsoleCommand("shop.show")]
        private void ccmdShopShow(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs(2)) return;
            var shopid = arg.GetString(0).Replace("'", "");
            if (shopid.Equals("close", StringComparison.OrdinalIgnoreCase))
            {
                var targetPlayer = arg.GetPlayerOrSleeper(1);
                DestroyUi(targetPlayer, true);
                return;
            }
            var player = arg.Player();
            if (player == null) return;
            var shoppage = arg.GetInt(1);
            ShowShop(player, shopid, shoppage, false);
        }

        [ConsoleCommand("shop.buy")]
        private void ccmdShopBuy(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs(3)) return;
            var player = arg.Player();
            if (player == null) return;
            object success = Interface.Oxide.CallHook("canShop", player);
            if (success != null)
            {
                SendReply(player, success as string ?? "You are not allowed to shop at the moment");
                return;
            }

            string shop = arg.Args[0].Replace("'", "");
            string item = arg.Args[1].Replace("'", "");
            int amount = arg.Args[2].Equals("all") ? GetAmountBuy(player, item) : Convert.ToInt32(arg.Args[2]);
            success = TryShopBuy(player, shop, item, amount);
            if (success is string)
            {
                SendReply(player, (string)success);
                return;
            }
            SendReply(player, string.Format(MessageBought, amount, item));
            ShowShop(player, shop, shopPage[player.userID], false, true);
        }
        [ConsoleCommand("shop.sell")]
        private void ccmdShopSell(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs(3)) return;
            var player = arg.Player();
            if (player == null) return;
            object success = Interface.Oxide.CallHook("canShop", player);
            if (success != null)
            {
                string message = "You are not allowed to shop at the moment";
                if (success is string)
                    message = (string)success;
                SendReply(player, message);
                return;
            }
            string shop = arg.Args[0].Replace("'", "");
            string item = arg.Args[1].Replace("'", "");
            int amount = arg.Args[2].Equals("all") ? GetAmountSell(player, item) : Convert.ToInt32(arg.Args[2]);
            success = TryShopSell(player, shop, item, amount);
            if (success is string)
            {
                SendReply(player, (string)success);
                return;
            }
            SendReply(player, string.Format(MessageSold, amount, item));
            ShowShop(player, shop, shopPage[player.userID], false, true);
        }
    }
}