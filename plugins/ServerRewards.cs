using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ServerRewards", "k1lly0u", "0.4.67", ResourceId = 1751)]
    class ServerRewards : RustPlugin
    {
        #region Fields
        [PluginReference]
        Plugin Kits, Economics, EventManager, HumanNPC, LustyMap, PlaytimeTracker, ImageLibrary;

        private PlayerData playerData;
        private NPCData npcData;
        private RewardData rewardData;
        private SaleData saleData;
        private CooldownData cooldownData;

        private DynamicConfigFile playerdata, npcdata, rewarddata, saledata, cooldowndata;

        private static ServerRewards ins;
        private UIManager uiManager;
        private Timer saveTimer;

        private static bool uiFadeIn;
        private bool isILReady;
        private string color1;
        private string color2;
        private int blueprintId = -996920608;

        private Dictionary<string, string> uiColors = new Dictionary<string, string>();
        private Dictionary<ulong, int> playerRP = new Dictionary<ulong, int>();
        private Hash<ulong, Timer> popupMessages = new Hash<ulong, Timer>();
        private Dictionary<int, string> itemIds = new Dictionary<int, string>();
        private Dictionary<string, string> itemNames = new Dictionary<string, string>();
        private Dictionary<ulong, KeyValuePair<string, NPCData.NPCInfo>> npcCreator = new Dictionary<ulong, KeyValuePair<string, NPCData.NPCInfo>>();
        private Dictionary<ulong, UserNPC> userNpc = new Dictionary<ulong, UserNPC>();

        enum UserNPC { Add, Edit, Remove }
        enum UIPanel { None, Navigation, Kits, Items, Commands, Exchange, Transfer, Sell }
        enum PurchaseType { Kit, Item, Command }
        enum Category { None, Weapon, Construction, Items, Resources, Attire, Tool, Medical, Food, Ammunition, Traps, Misc, Component }
        #endregion

        #region UI
        const string UIMain = "SR_Store";
        const string UISelect = "SR_Select";
        const string UIRP = "SR_RPPanel";
        const string UIPopup = "SR_Popup";

        class UI
        {
            static public CuiElementContainer CreateElementContainer(string panelName, string color, string aMin, string aMax)
            {
                var NewElement = new CuiElementContainer()
            {
                {
                    new CuiPanel
                    {
                        Image = {Color = color},
                        RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                        CursorEnabled = true
                    },
                    new CuiElement().Parent = "Overlay",
                    panelName
                }
            };
                return NewElement;
            }
            static public void CreatePanel(ref CuiElementContainer container, string panel, string color, string aMin, string aMax, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    CursorEnabled = cursor
                },
                panel, CuiHelper.GetGuid());
            }
            static public void CreateLabel(ref CuiElementContainer container, string panel, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter, string color = null, float fadein = 1.0f)
            {
                if (uiFadeIn)
                    fadein = 0;
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Align = align, FadeIn = fadein, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel, CuiHelper.GetGuid());

            }
            static public void CreateButton(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter, float fadein = 1.0f)
            {
                if (uiFadeIn)
                    fadein = 0;
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = fadein },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel, CuiHelper.GetGuid());
            }
            static public void LoadImage(ref CuiElementContainer container, string panel, string png, string aMin, string aMax)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent {Png = png },
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax }
                    }
                });
            }
            static public string Color(string hexColor, float alpha)
            {
                if (hexColor.StartsWith("#"))
                    hexColor = hexColor.TrimStart('#');
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }
        class UIManager
        {
            public Dictionary<UIPanel, Dictionary<Category, Dictionary<int, CuiElementContainer>>> standardElements = new Dictionary<UIPanel, Dictionary<Category, Dictionary<int, CuiElementContainer>>>();
            public Dictionary<string, Dictionary<UIPanel, Dictionary<Category, Dictionary<int, CuiElementContainer>>>> npcElements = new Dictionary<string, Dictionary<UIPanel, Dictionary<Category, Dictionary<int, CuiElementContainer>>>>();

            public Dictionary<ulong, PlayerUI> playerUi = new Dictionary<ulong, PlayerUI>();


            public void AddUI(BasePlayer player, UIPanel type, Category subType = Category.None, int pageNumber = 0, string npcId = null)
            {
                PlayerUI data;
                if (!playerUi.TryGetValue(player.userID, out data))
                {
                    data = new PlayerUI { npcId = npcId };
                    playerUi.Add(player.userID, data);
                }

                CuiElementContainer container = null;
                if (type == UIPanel.Navigation)
                {
                    if (!string.IsNullOrEmpty(npcId))
                        container = npcElements[npcId][UIPanel.Navigation][Category.None][0];
                    else container = standardElements[UIPanel.Navigation][Category.None][0];

                    data.navigationIds.AddRange(container.Select(x => x.Name));
                    ins.DisplayPoints(player);
                }
                else
                {
                    if (type != UIPanel.Sell && type != UIPanel.Transfer)
                    {
                        if (!string.IsNullOrEmpty(npcId))
                            container = npcElements[npcId][type][subType][pageNumber];
                        else container = standardElements[type][subType][pageNumber];
                    }
                    else
                    {
                        switch (type)
                        {
                            case UIPanel.Transfer:
                                container = ins.CreateTransferElement(player, pageNumber);
                                break;
                            case UIPanel.Sell:
                                container = ins.CreateSaleElement(player);
                                break;
                        }
                    }
                    data.elementIds.AddRange(container.Select(x => x.Name));
                }
                if (container != null)
                    CuiHelper.AddUi(player, container);
                playerUi[player.userID] = data;
            }
            public void DestroyUI(BasePlayer player, bool destroyNav = false)
            {
                PlayerUI data;
                if (playerUi.TryGetValue(player.userID, out data))
                {
                    foreach (var elementId in data.elementIds)
                        CuiHelper.DestroyUi(player, elementId);

                    if (destroyNav)
                    {
                        foreach (var elementId in data.navigationIds)
                            CuiHelper.DestroyUi(player, elementId);

                        CuiHelper.DestroyUi(player, UIRP);
                        CuiHelper.DestroyUi(player, UISelect);

                        playerUi.Remove(player.userID);
                        ins.OpenMap(player);
                    }
                }
            }
            public void SwitchElement(BasePlayer player, UIPanel type, Category subType = Category.None, int pageNumber = 0, string npcId = null)
            {
                DestroyUI(player);
                AddUI(player, type, subType, pageNumber, npcId);
            }
            public bool IsOpen(BasePlayer player) => playerUi.ContainsKey(player.userID);

            public bool NPCHasUI(string npcId) => npcElements.ContainsKey(npcId);

            public void RemoveNPCUI(string npcId)
            {
                if (NPCHasUI(npcId))
                    npcElements.Remove(npcId);
            }

            public void RenameComponents(CuiElementContainer container)
            {
                foreach (var element in container)
                {
                    if (element.Name == "AddUI CreatedPanel")
                        element.Name = CuiHelper.GetGuid();
                }
            }
            public string GetNPCInUse(BasePlayer player)
            {
                if (playerUi.ContainsKey(player.userID))
                    return playerUi[player.userID].npcId;
                return string.Empty;
            }
            public class PlayerUI
            {
                public string npcId = string.Empty;
                public List<string> elementIds = new List<string>();
                public List<string> navigationIds = new List<string>();
            }
        }
        #endregion

        #region UI Creation
        private void CreateAllElements()
        {            
            isILReady = true;

            CreateNewElement();
            foreach (var npc in npcData.npcInfo)
                CreateNewElement(npc.Key, npc.Value);
            PrintWarning("All UI elements have been successfully generated!");
        }
        private void CreateNewElement(string npcId = null, NPCData.NPCInfo info = null)
        {
            Category[] categories = new Category[] { Category.Ammunition, Category.Attire, Category.Component, Category.Construction, Category.Food, Category.Items, Category.Medical, Category.Misc, Category.Resources, Category.Tool, Category.Traps, Category.Weapon };

            SetNewElement(npcId);
            CreateNavUI(npcId, info);
            CreateKitsUI(npcId, info);
            foreach (var category in categories)
            {
                CreateItemsUI(category, npcId, info);
            }
            CreateCommandsUI(npcId, info);
            CreateExchangeUI(npcId);
        }
        private void SetNewElement(string npcId = null)
        {
            var structure = new Dictionary<UIPanel, Dictionary<Category, Dictionary<int, CuiElementContainer>>>
                {
                    { UIPanel.Commands, new Dictionary<Category, Dictionary<int, CuiElementContainer>> { { Category.None, new Dictionary<int, CuiElementContainer>()} } },
                    { UIPanel.Exchange, new Dictionary<Category, Dictionary<int, CuiElementContainer>> { { Category.None, new Dictionary<int, CuiElementContainer>()} } },
                    { UIPanel.Items, new Dictionary<Category, Dictionary<int, CuiElementContainer>>
                        {
                            { Category.Ammunition, new Dictionary<int, CuiElementContainer>()},
                            { Category.Attire, new Dictionary<int, CuiElementContainer>()},
                            { Category.Component, new Dictionary<int, CuiElementContainer>()},
                            { Category.Construction, new Dictionary<int, CuiElementContainer>()},
                            { Category.Food, new Dictionary<int, CuiElementContainer>()},
                            { Category.Items, new Dictionary<int, CuiElementContainer>()},
                            { Category.Medical, new Dictionary<int, CuiElementContainer>()},
                            { Category.Misc, new Dictionary<int, CuiElementContainer>()},
                            { Category.None, new Dictionary<int, CuiElementContainer>()},
                            { Category.Resources, new Dictionary<int, CuiElementContainer>()},
                            { Category.Tool, new Dictionary<int, CuiElementContainer>()},
                            { Category.Traps, new Dictionary<int, CuiElementContainer>()},
                            { Category.Weapon, new Dictionary<int, CuiElementContainer>()},
                        }
                    },
                    { UIPanel.Kits, new Dictionary<Category, Dictionary<int, CuiElementContainer>> { { Category.None, new Dictionary<int, CuiElementContainer>()} } },
                    { UIPanel.Navigation, new Dictionary<Category, Dictionary<int, CuiElementContainer>> { { Category.None, new Dictionary<int, CuiElementContainer>()} } }

                };
            if (!string.IsNullOrEmpty(npcId))
                uiManager.npcElements.Add(npcId, structure);
            else uiManager.standardElements = structure;
        }

        private void CreateNavUI(string npcId = null, NPCData.NPCInfo npcInfo = null)
        {
            var container = UI.CreateElementContainer(UISelect, uiColors["dark"], "0 0.93", "1 1");
            UI.CreatePanel(ref container, UISelect, uiColors["light"], "0.01 0", "0.99 1", true);
            UI.CreateLabel(ref container, UISelect, $"{color1}{msg("storeTitle")}</color>", 26, "0.01 0", "0.2 1");

            int i = 0;
            if (string.IsNullOrEmpty(npcId))
            {
                if (configData.Tabs.Kits)
                {
                    CreateMenuButton(ref container, UISelect, msg("storeKits"), $"SRUI_ChangeElement Kits 0", i);
                    i++;
                }
                if (configData.Tabs.Items)
                {
                    CreateMenuButton(ref container, UISelect, msg("storeItems"), $"SRUI_ChangeElement Items 0", i);
                    i++;
                }
                if (configData.Tabs.Commands)
                {
                    CreateMenuButton(ref container, UISelect, msg("storeCommands"), $"SRUI_ChangeElement Commands 0", i);
                    i++;
                }
                if (Economics && configData.Tabs.Exchange)
                {
                    CreateMenuButton(ref container, UISelect, msg("storeExchange"), "SRUI_ChangeElement Exchange 0", i);
                    i++;
                }
                if (configData.Tabs.Transfer)
                {
                    CreateMenuButton(ref container, UISelect, msg("storeTransfer"), "SRUI_ChangeElement Transfer 0", i);
                    i++;
                }
                if (configData.Tabs.Seller)
                {
                    CreateMenuButton(ref container, UISelect, msg("sellItems"), "SRUI_ChangeElement Sell 0", i);
                    i++;
                }
                CreateMenuButton(ref container, UISelect, msg("storeClose"), "SRUI_DestroyAll", i);

                uiManager.RenameComponents(container);
                uiManager.standardElements[UIPanel.Navigation][Category.None][0] = container;
            }
            else
            {
                if (npcInfo != null)
                {
                    if (configData.Tabs.Kits && npcInfo.sellKits)
                    {
                        CreateMenuButton(ref container, UISelect, msg("storeKits"), $"SRUI_ChangeElement Kits 0 {npcId}", i);
                        i++;
                    }
                    if (configData.Tabs.Items && npcInfo.sellItems)
                    {
                        CreateMenuButton(ref container, UISelect, msg("storeItems"), $"SRUI_ChangeElement Items 0 {npcId}", i);
                        i++;
                    }
                    if (configData.Tabs.Commands && npcInfo.sellCommands)
                    {
                        CreateMenuButton(ref container, UISelect, msg("storeCommands"), $"SRUI_ChangeElement Commands 0 {npcId}", i);
                        i++;
                    }
                    if (Economics && configData.Tabs.Exchange && npcInfo.canExchange)
                    {
                        CreateMenuButton(ref container, UISelect, msg("storeExchange"), $"SRUI_ChangeElement Exchange 0 {npcId}", i);
                        i++;
                    }
                    if (configData.Tabs.Transfer && npcInfo.canTransfer)
                    {
                        CreateMenuButton(ref container, UISelect, msg("storeTransfer"), $"SRUI_ChangeElement Transfer 0 {npcId}", i);
                        i++;
                    }
                    if (configData.Tabs.Seller && npcInfo.canSell)
                    {
                        CreateMenuButton(ref container, UISelect, msg("sellItems"), $"SRUI_ChangeElement Sell 0 {npcId}", i);
                        i++;
                    }
                    CreateMenuButton(ref container, UISelect, msg("storeClose"), "SRUI_DestroyAll", i);

                    uiManager.RenameComponents(container);
                    uiManager.npcElements[npcId][UIPanel.Navigation][Category.None][0] = container;
                }
                else PrintWarning($"Failed to create the navigation menu for NPC: {npcId}. Invalid data was supplied!");
            }
        }
        private void CreateItemsUI(Category category, string npcId = null, NPCData.NPCInfo npcInfo = null)
        {
            Category[] categories = new Category[] { Category.Ammunition, Category.Attire, Category.Component, Category.Construction, Category.Food, Category.Items, Category.Medical, Category.Misc, Category.Resources, Category.Tool, Category.Traps, Category.Weapon };

            if (string.IsNullOrEmpty(npcId))
            {
                int maxPages = 1;
                var items = rewardData.items.Where(x => x.Value.category == category).ToList();
                bool[] hasItems = new bool[12];
                for (int i = 0; i < categories.Length; i++)
                {
                    var cat = categories[i];
                    hasItems[i] = rewardData.items.Where(x => x.Value.category == cat).Count() > 0;
                }
                if (items.Count == 0)
                {
                    CuiElementContainer container = CreateItemsElement(new List<KeyValuePair<string, RewardData.RewardItem>>(), category, hasItems, 0, false, false, null);
                    uiManager.RenameComponents(container);
                    uiManager.standardElements[UIPanel.Items][category][0] = container;
                }
                else
                {
                    if (items.Count > 36)
                        maxPages = (items.Count - 1) / 36 + 1;
                    for (int i = 0; i < maxPages; i++)
                    {
                        int min = i * 36;
                        int max = items.Count < 36 ? items.Count : min + 36 > items.Count ? (items.Count - min) : 36;
                        var range = items.OrderBy(x => x.Value.displayName).ToList().GetRange(min, max);
                        CuiElementContainer container = CreateItemsElement(range, category, hasItems, i, i < maxPages - 1, i > 0, null);
                        uiManager.RenameComponents(container);
                        uiManager.standardElements[UIPanel.Items][category][i] = container;
                    }
                }
            }
            else
            {
                if (npcInfo != null)
                {
                    int maxPages = 1;

                    List<KeyValuePair<string, RewardData.RewardItem>> items = new List<KeyValuePair<string, RewardData.RewardItem>>();
                    if (npcInfo.useCustom)
                        items = rewardData.items.Where(y => npcInfo.items.Contains(y.Key)).Where(x => x.Value.category == category).ToList();
                    else items = rewardData.items.Where(x => x.Value.category == category).ToList();
                    bool[] hasItems = new bool[12];
                    for (int i = 0; i < categories.Length; i++)
                    {
                        var cat = categories[i];
                        if (npcInfo.useCustom)
                            hasItems[i] = rewardData.items.Where(y => npcInfo.items.Contains(y.Key)).Where(x => x.Value.category == cat).Count() > 0;
                        else hasItems[i] = rewardData.items.Where(x => x.Value.category == cat).Count() > 0;
                    }
                    if (items.Count == 0)
                    {
                        CuiElementContainer container = CreateItemsElement(new List<KeyValuePair<string, RewardData.RewardItem>>(), category, hasItems, 0, false, false, npcId);
                        uiManager.RenameComponents(container);
                        uiManager.npcElements[npcId][UIPanel.Items][category][0] = container;
                    }
                    else
                    {
                        if (items.Count > 36)
                            maxPages = (items.Count - 1) / 36 + 1;
                        for (int i = 0; i < maxPages; i++)
                        {
                            int min = i * 36;
                            int max = items.Count < 36 ? items.Count : min + 36 > items.Count ? (items.Count - min) : 36;
                            var range = items.OrderBy(x => x.Value.displayName).ToList().GetRange(min, max);
                            CuiElementContainer container = CreateItemsElement(range, category, hasItems, i, i < maxPages - 1, i > 0, npcId);
                            uiManager.RenameComponents(container);
                            uiManager.npcElements[npcId][UIPanel.Items][category][i] = container;
                        }
                    }
                }
                else PrintWarning($"Failed to create items menu for NPC: {npcId} Category: {category}. Invalid data was supplied!");
            }
        }
        private void CreateKitsUI(string npcId = null, NPCData.NPCInfo npcInfo = null)
        {
            if (string.IsNullOrEmpty(npcId))
            {
                int maxPages = 1;
                var kits = rewardData.kits;
                if (kits.Count == 0)
                {
                    CuiElementContainer container = CreateKitsElement(new List<KeyValuePair<string, RewardData.RewardKit>>(), 0, false, false);
                    uiManager.RenameComponents(container);
                    uiManager.standardElements[UIPanel.Kits][Category.None][0] = container;
                }
                else
                {
                    if (kits.Count > 10)
                        maxPages = (kits.Count - 1) / 10 + 1;
                    for (int i = 0; i < maxPages; i++)
                    {
                        int min = i * 10;
                        int max = kits.Count < 10 ? kits.Count : min + 10 > kits.Count ? (kits.Count - min) : 10;
                        var range = kits.OrderBy(x => x.Value.displayName).ToList().GetRange(min, max);
                        CuiElementContainer container = CreateKitsElement(range, i, i < maxPages - 1, i > 0);
                        uiManager.RenameComponents(container);
                        uiManager.standardElements[UIPanel.Kits][Category.None][i] = container;
                    }
                }
            }
            else
            {
                if (npcInfo != null)
                {
                    int maxPages = 1;
                    var kits = rewardData.kits.Where(x => npcInfo.kits.Contains(x.Key)).ToDictionary(v => v.Key, y => y.Value);
                    if (kits.Count == 0)
                    {
                        CuiElementContainer container = CreateKitsElement(new List<KeyValuePair<string, RewardData.RewardKit>>(), 0, false, false, npcId);
                        uiManager.RenameComponents(container);
                        uiManager.npcElements[npcId][UIPanel.Kits][Category.None][0] = container;
                    }
                    else
                    {
                        if (kits.Count > 10)
                            maxPages = (kits.Count - 1) / 10 + 1;
                        for (int i = 0; i < maxPages; i++)
                        {
                            int min = i * 10;
                            int max = kits.Count < 10 ? kits.Count : min + 10 > kits.Count ? (kits.Count - min) : 10;
                            var range = kits.OrderBy(x => x.Value.displayName).ToList().GetRange(min, max);
                            CuiElementContainer container = CreateKitsElement(range, i, i < maxPages - 1, i > 0, npcId);
                            uiManager.RenameComponents(container);
                            uiManager.npcElements[npcId][UIPanel.Kits][Category.None][i] = container;
                        }
                    }
                }
                else PrintWarning($"Failed to create kits menu for NPC: {npcId}. Invalid data was supplied!");
            }
        }
        private void CreateCommandsUI(string npcId = null, NPCData.NPCInfo npcInfo = null)
        {
            if (string.IsNullOrEmpty(npcId))
            {
                int maxPages = 1;
                var commands = rewardData.commands;
                if (commands.Count == 0)
                {
                    CuiElementContainer container = CreateCommandsElement(new List<KeyValuePair<string, RewardData.RewardCommand>>(), 0, false, false);
                    uiManager.RenameComponents(container);
                    uiManager.standardElements[UIPanel.Commands][Category.None][0] = container;
                }
                else
                {
                    if (commands.Count > 10)
                        maxPages = (commands.Count - 1) / 10 + 1;
                    for (int i = 0; i < maxPages; i++)
                    {
                        int min = i * 10;
                        int max = commands.Count < 10 ? commands.Count : min + 10 > commands.Count ? (commands.Count - min) : 10;
                        var range = commands.OrderBy(x => x.Value.displayName).ToList().GetRange(min, max);
                        CuiElementContainer container = CreateCommandsElement(range, i, i < maxPages - 1, i > 0);
                        uiManager.RenameComponents(container);
                        uiManager.standardElements[UIPanel.Commands][Category.None][i] = container;
                    }
                }
            }
            else
            {
                if (npcInfo != null)
                {
                    int maxPages = 1;
                    var commands = rewardData.commands.Where(x => npcInfo.commands.Contains(x.Key)).ToDictionary(v => v.Key, y => y.Value);
                    if (commands.Count == 0)
                    {
                        CuiElementContainer container = CreateCommandsElement(new List<KeyValuePair<string, RewardData.RewardCommand>>(), 0, false, false, npcId);
                        uiManager.RenameComponents(container);
                        uiManager.npcElements[npcId][UIPanel.Commands][Category.None][0] = container;
                    }
                    else
                    {
                        if (commands.Count > 10)
                            maxPages = (commands.Count - 1) / 10 + 1;
                        for (int i = 0; i < maxPages; i++)
                        {
                            int min = i * 10;
                            int max = commands.Count < 10 ? commands.Count : min + 10 > commands.Count ? (commands.Count - min) : 10;
                            var range = commands.OrderBy(x => x.Value.displayName).ToList().GetRange(min, max);
                            CuiElementContainer container = CreateCommandsElement(range, i, i < maxPages - 1, i > 0, npcId);
                            uiManager.RenameComponents(container);
                            uiManager.npcElements[npcId][UIPanel.Commands][Category.None][i] = container;
                        }
                    }
                }
                else PrintWarning($"Failed to create commands menu for NPC: {npcId}. Invalid data was supplied!");
            }
        }
        private void CreateExchangeUI(string npcId = null)
        {
            var container = UI.CreateElementContainer(UIMain, uiColors["dark"], "0 0", "1 0.93");
            UI.CreateLabel(ref container, UIMain, $"<color={configData.Colors.Background_Dark.Color}>{msg("storeExchange")}</color>", 200, "0 0", "1 1", TextAnchor.MiddleCenter);
            UI.CreatePanel(ref container, UIMain, uiColors["light"], "0.01 0.01", "0.99 0.99", true);
            UI.CreateLabel(ref container, UIMain, $"{color1}{msg("exchange1")}</color>", 24, "0 0.82", "1 0.9");
            UI.CreateLabel(ref container, UIMain, $"{color2}{msg("exchange2")}</color>{color1}{configData.Exchange.RP} {msg("storeRP")}</color> -> {color1}{configData.Exchange.Economics} {msg("storeCoins")}</color>", 20, "0 0.6", "1 0.7");
            UI.CreateLabel(ref container, UIMain, $"{color1}{msg("storeRP")} => {msg("storeEcon")}</color>", 20, "0.25 0.4", "0.4 0.55");
            UI.CreateLabel(ref container, UIMain, $"{color1}{msg("storeEcon")} => {msg("storeRP")}</color>", 20, "0.6 0.4", "0.75 0.55");
            UI.CreateButton(ref container, UIMain, uiColors["buttonbg"], msg("storeExchange"), 20, "0.25 0.3", "0.4 0.38", "SRUI_Exchange 1");
            UI.CreateButton(ref container, UIMain, uiColors["buttonbg"], msg("storeExchange"), 20, "0.6 0.3", "0.75 0.38", "SRUI_Exchange 2");

            uiManager.RenameComponents(container);
            if (!string.IsNullOrEmpty(npcId))
                uiManager.npcElements[npcId][UIPanel.Exchange][Category.None][0] = container;
            else uiManager.standardElements[UIPanel.Exchange][Category.None][0] = container;
        }

        private CuiElementContainer CreateItemsElement(List<KeyValuePair<string, RewardData.RewardItem>> items, Category category, bool[] hasItems, int page, bool pageUp, bool pageDown, string npcId)
        {
            var container = UI.CreateElementContainer(UIMain, uiColors["dark"], "0 0", "1 0.93");
            UI.CreateLabel(ref container, UIMain, $"<color={configData.Colors.Background_Dark.Color}>{msg(category.ToString())}</color>", 200, "0 0", "1 1", TextAnchor.MiddleCenter);

            UI.CreatePanel(ref container, UIMain, uiColors["light"], "0.01 0.94", "0.99 0.99", true);

            CreateSubMenu(ref container, UIMain, hasItems, npcId);
            UI.CreatePanel(ref container, UIMain, uiColors["light"], "0.01 0.01", "0.99 0.93", true);

            if (pageUp) UI.CreateButton(ref container, UIMain, uiColors["buttonbg"], ">>>", 16, "0.87 0.03", "0.955 0.07", $"SRUI_ChangeElement Items {page + 1} {npcId ?? "null"} {category}");
            if (pageDown) UI.CreateButton(ref container, UIMain, uiColors["buttonbg"], "<<<", 16, "0.045 0.03", "0.13 0.07", $"SRUI_ChangeElement Items {page - 1} {npcId ?? "null"} {category}");

            for (int i = 0; i < items.Count; i++)
            {
                CreateItemEntry(ref container, UIMain, items[i].Key, items[i].Value, i);
            }

            return container;
        }
        private CuiElementContainer CreateKitsElement(List<KeyValuePair<string, RewardData.RewardKit>> kits, int page, bool pageUp, bool pageDown, string npcId = null)
        {
            var container = UI.CreateElementContainer(UIMain, uiColors["dark"], "0 0", "1 0.93");
            UI.CreateLabel(ref container, UIMain, $"<color={configData.Colors.Background_Dark.Color}>{msg("storeKits")}</color>", 200, "0 0", "1 1", TextAnchor.MiddleCenter);

            UI.CreatePanel(ref container, UIMain, uiColors["light"], "0.01 0.01", "0.99 0.99", true);

            if (kits.Count == 0)
                UI.CreateLabel(ref container, UIMain, $"{color1}{msg("noKits")}</color>", 24, "0 0.82", "1 0.9");
            else
            {
                if (pageUp) UI.CreateButton(ref container, UIMain, uiColors["buttonbg"], ">>>", 16, "0.87 0.03", "0.955 0.07", $"SRUI_ChangeElement Kits {page + 1} {npcId}");
                if (pageDown) UI.CreateButton(ref container, UIMain, uiColors["buttonbg"], "<<<", 16, "0.045 0.03", "0.13 0.07", $"SRUI_ChangeElement Kits {page - 1} {npcId}");

                for (int i = 0; i < kits.Count; i++)
                {
                    RewardData.RewardKit kit = kits[i].Value;
                    string description = string.Empty;
                    if (configData.UIOptions.KitContents)
                        description = GetKitContents(kit.kitName);
                    else description = kit.description;
                    CreateKitCommandEntry(ref container, UIMain, kit.displayName, kits[i].Key, description, kit.cost, i, true, kit.iconName);
                }
            }
            return container;
        }
        private CuiElementContainer CreateCommandsElement(List<KeyValuePair<string, RewardData.RewardCommand>> commands, int page, bool pageUp, bool pageDown, string npcId = null)
        {
            var container = UI.CreateElementContainer(UIMain, uiColors["dark"], "0 0", "1 0.93");
            UI.CreateLabel(ref container, UIMain, $"<color={configData.Colors.Background_Dark.Color}>{msg("storeCommands")}</color>", 200, "0 0", "1 1", TextAnchor.MiddleCenter);
            UI.CreatePanel(ref container, UIMain, uiColors["light"], "0.01 0.01", "0.99 0.99", true);

            if (commands.Count == 0)
                UI.CreateLabel(ref container, UIMain, $"{color1}{msg("noCommands")}</color>", 24, "0 0.82", "1 0.9");
            else
            {
                if (pageUp) UI.CreateButton(ref container, UIMain, uiColors["buttonbg"], ">>>", 16, "0.87 0.03", "0.955 0.07", $"SRUI_ChangeElement Commands {page + 1} {npcId}");
                if (pageDown) UI.CreateButton(ref container, UIMain, uiColors["buttonbg"], "<<<", 16, "0.045 0.03", "0.13 0.07", $"SRUI_ChangeElement Commands {page - 1} {npcId}");

                for (int i = 0; i < commands.Count; i++)
                {
                    RewardData.RewardCommand command = commands[i].Value;
                    CreateKitCommandEntry(ref container, UIMain, command.displayName, commands[i].Key, command.description, command.cost, i, false, command.iconName);
                }
            }
            return container;
        }

        private void PopupMessage(BasePlayer player, string msg)
        {
            var element = UI.CreateElementContainer(UIPopup, uiColors["dark"], "0.33 0.45", "0.67 0.6");
            UI.CreatePanel(ref element, UIPopup, uiColors["light"], "0.01 0.04", "0.99 0.96");
            UI.CreateLabel(ref element, UIPopup, $"{color1}{msg}</color>", 20, "0.05 0.05", "0.95 0.95");

            if (popupMessages.ContainsKey(player.userID))
            {
                CuiHelper.DestroyUi(player, UIPopup);
                popupMessages[player.userID].Destroy();
                popupMessages.Remove(player.userID);
            }
            CuiHelper.AddUi(player, element);
            popupMessages.Add(player.userID, timer.In(3.5f, () =>
            {
                CuiHelper.DestroyUi(player, UIPopup);
                popupMessages.Remove(player.userID);
            }));
        }
        private void DisplayPoints(BasePlayer player)
        {
            if (player == null) return;

            CuiHelper.DestroyUi(player, UIRP);
            if (!uiManager.IsOpen(player)) return;

            int playerPoints = CheckPoints(player.userID);

            var element = UI.CreateElementContainer(UIRP, "0 0 0 0", "0.3 0", "0.7 0.1");
            string message = $"{color1}{msg("storeRP", player.UserIDString)}: {playerPoints}</color>";
            if (Economics && configData.Tabs.Exchange)
            {
                var amount = Economics?.Call("Balance", player.UserIDString);
                message = message + $"  {color2}||</color> {color1}Economics: {amount}</color>";
            }
            if (configData.UIOptions.ShowPlaytime)
            {
                var time = Interface.CallHook("GetPlayTime", player.UserIDString);
                if (time is double)
                {
                    var playTime = FormatTime((double)time);
                    if (!string.IsNullOrEmpty(playTime))
                        message = $"{color1}{msg("storePlaytime", player.UserIDString)}: {playTime}</color> {color2}||</color> " + message;
                }
            }

            UI.CreateLabel(ref element, UIRP, message, 20, "0 0", "1 1", TextAnchor.MiddleCenter, null, 0f);

            CuiHelper.AddUi(player, element);
            timer.Once(1, () => DisplayPoints(player));
        }

        #region Sale System
        private CuiElementContainer CreateSaleElement(BasePlayer player)
        {
            var container = UI.CreateElementContainer(UIMain, uiColors["dark"], "0 0", "1 0.93");
            UI.CreateLabel(ref container, UIMain, $"<color={configData.Colors.Background_Dark.Color}>{msg("storeSales")}</color>", 200, "0 0", "1 1", TextAnchor.MiddleCenter);
            UI.CreatePanel(ref container, UIMain, uiColors["light"], "0.01 0.01", "0.99 0.99", true);
            UI.CreateLabel(ref container, UIMain, $"{color1}{msg("selectItemSell")}</color>", 20, "0 0.9", "1 1");

            int i = 0;
            foreach (var item in player.inventory.containerMain.itemList)
            {
                if (saleData.items.ContainsKey(item.info.shortname))
                {
                    if (!saleData.items[item.info.shortname].ContainsKey(item.skin))
                    {
                        saleData.items[item.info.shortname].Add(item.skin, new SaleData.SaleItem { displayName = item?.info?.steamItem?.displayName?.translated ?? $"{item.info.displayName.translated} {item.skin}" });
                        SaveSales();
                    }

                    var name = saleData.items[item.info.shortname][item.skin].displayName;
                    CreateInventoryEntry(ref container, UIMain, item.info.shortname, item.skin, name, item.amount, i);
                    i++;

                }
            }
            return container;
        }
        private void CreateInventoryEntry(ref CuiElementContainer container, string panelName, string shortname, ulong skinId, string name, int amount, int number)
        {
            var pos = CalcPosInv(number);

            UI.CreateLabel(ref container, panelName, $"{msg("Name")}:  {color1}{name}</color>", 14, $"{pos[0]} {pos[1]}", $"{pos[0] + 0.22f} {pos[3]}", TextAnchor.MiddleLeft);
            UI.CreateLabel(ref container, panelName, $"{msg("Amount")}:  {color1}{amount}</color>", 14, $"{pos[0] + 0.22f} {pos[1]}", $"{pos[0] + 0.32f} {pos[3]}", TextAnchor.MiddleLeft);
            if (saleData.items[shortname][skinId].enabled)
                UI.CreateButton(ref container, panelName, uiColors["buttonbg"], msg("Sell"), 14, $"{pos[0] + 0.35f} {pos[1]}", $"{pos[2]} {pos[3]}", $"SRUI_SellItem {shortname} {skinId} {amount}");
            else UI.CreateButton(ref container, panelName, uiColors["buttonbg"], msg("CantSell"), 14, $"{pos[0] + 0.35f} {pos[1]}", $"{pos[2]} {pos[3]}", "");
        }
        private void SellItem(BasePlayer player, string shortname, ulong skinId, int amount)
        {
            var saleItem = saleData.items[shortname][skinId];
            var name = saleItem.displayName;
            var container = UI.CreateElementContainer(UIMain, uiColors["dark"], "0 0", "1 0.93");
            UI.CreateLabel(ref container, UIMain, $"<color={configData.Colors.Background_Dark.Color}>{msg("storeSales")}</color>", 200, "0 0", "1 1", TextAnchor.MiddleCenter);
            UI.CreatePanel(ref container, UIMain, uiColors["light"], "0.01 0.01", "0.99 0.99", true);
            UI.CreateLabel(ref container, UIMain, $"{color1}{msg("selectToSell")}</color>", 20, "0 0.9", "1 1");
            int salePrice = (int)Math.Floor(saleItem.price * amount);

            UI.CreateLabel(ref container, UIMain, string.Format(msg("sellItemF"), color1, name), 18, "0.1 0.8", "0.3 0.84", TextAnchor.MiddleLeft);
            UI.CreateLabel(ref container, UIMain, string.Format(msg("sellPriceF"), color1, saleItem.price, msg("storeRP")), 18, "0.1 0.76", "0.3 0.8", TextAnchor.MiddleLeft);
            UI.CreateLabel(ref container, UIMain, string.Format(msg("sellUnitF"), color1, amount), 18, "0.1 0.72", "0.3 0.76", TextAnchor.MiddleLeft);
            UI.CreateLabel(ref container, UIMain, string.Format(msg("sellTotalF"), color1, salePrice, msg("storeRP")), 18, "0.1 0.68", "0.3 0.72", TextAnchor.MiddleLeft);


            UI.CreateButton(ref container, UIMain, uiColors["buttonbg"], "+ 10000", 16, "0.84 0.72", "0.89 0.76", $"SRUI_SellItem {shortname} {skinId} {amount + 10000}");
            UI.CreateButton(ref container, UIMain, uiColors["buttonbg"], "+ 1000", 16, "0.78 0.72", "0.83 0.76", $"SRUI_SellItem {shortname} {skinId} {amount + 1000}");
            UI.CreateButton(ref container, UIMain, uiColors["buttonbg"], "+ 100", 16, "0.72 0.72", "0.77 0.76", $"SRUI_SellItem {shortname} {skinId} {amount + 100}");
            UI.CreateButton(ref container, UIMain, uiColors["buttonbg"], "+ 10", 16, "0.66 0.72", "0.71 0.76", $"SRUI_SellItem {shortname} {skinId} {amount + 10}");
            UI.CreateButton(ref container, UIMain, uiColors["buttonbg"], "+ 1", 16, "0.6 0.72", "0.65 0.76", $"SRUI_SellItem {shortname} {skinId} {amount + 1}");
            UI.CreateButton(ref container, UIMain, uiColors["buttonbg"], "-1", 16, "0.54 0.72", "0.59 0.76", $"SRUI_SellItem {shortname} {skinId} {amount - 1}");
            UI.CreateButton(ref container, UIMain, uiColors["buttonbg"], "-10", 16, "0.48 0.72", "0.53 0.76", $"SRUI_SellItem {shortname} {skinId} {amount - 10}");
            UI.CreateButton(ref container, UIMain, uiColors["buttonbg"], "-100", 16, "0.42 0.72", "0.47 0.76", $"SRUI_SellItem {shortname} {skinId} {amount - 100}");
            UI.CreateButton(ref container, UIMain, uiColors["buttonbg"], "-1000", 16, "0.36 0.72", "0.41 0.76", $"SRUI_SellItem {shortname} {skinId} {amount - 1000}");
            UI.CreateButton(ref container, UIMain, uiColors["buttonbg"], "-10000", 16, "0.3 0.72", "0.35 0.76", $"SRUI_SellItem {shortname} {skinId} {amount - 10000}");

            UI.CreateButton(ref container, UIMain, uiColors["buttonbg"], msg("cancelSale"), 16, "0.75 0.34", "0.9 0.39", "SRUI_CancelSale");
            UI.CreateButton(ref container, UIMain, uiColors["buttonbg"], msg("confirmSale"), 16, "0.55 0.34", "0.7 0.39", $"SRUI_Sell {shortname} {skinId} {amount}");

            CuiHelper.DestroyUi(player, UIMain);
            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region Transfer System
        private CuiElementContainer CreateTransferElement(BasePlayer player, int page = 0)
        {
            var container = UI.CreateElementContainer(UIMain, uiColors["dark"], "0 0", "1 0.93");
            UI.CreateLabel(ref container, UIMain, $"<color={configData.Colors.Background_Dark.Color}>{msg("storeTransfer")}</color>", 200, "0 0", "1 1", TextAnchor.MiddleCenter);
            UI.CreatePanel(ref container, UIMain, uiColors["light"], "0.01 0.01", "0.99 0.99", true);
            UI.CreateLabel(ref container, UIMain, $"{color1}{msg("transfer1", player.UserIDString)}</color>", 20, "0 0.9", "1 1");

            var playerCount = BasePlayer.activePlayerList.Count;
            if (playerCount > 96)
            {
                var maxpages = (playerCount - 1) / 96 + 1;
                if (page < maxpages - 1)
                    UI.CreateButton(ref container, UIMain, uiColors["buttonbg"], msg("storeNext", player.UserIDString), 18, "0.87 0.92", "0.97 0.97", $"SRUI_Transfer {page + 1}");
                if (page > 0)
                    UI.CreateButton(ref container, UIMain, uiColors["buttonbg"], msg("storeBack", player.UserIDString), 18, "0.03 0.92", "0.13 0.97", $"SRUI_Transfer {page - 1}");
            }
            int maxentries = (96 * (page + 1));
            if (maxentries > playerCount)
                maxentries = playerCount;
            int rewardcount = 96 * page;

            int i = 0;
            for (int n = rewardcount; n < maxentries; n++)
            {
                if (BasePlayer.activePlayerList[n] == null) continue;
                CreatePlayerNameEntry(ref container, UIMain, BasePlayer.activePlayerList[n].displayName, BasePlayer.activePlayerList[n].UserIDString, i);
                i++;
            }
            return container;
        }
        private void TransferElement(BasePlayer player, string name, string id)
        {
            CuiHelper.DestroyUi(player, UIMain);
            var container = UI.CreateElementContainer(UIMain, uiColors["dark"], "0 0", "1 0.93");
            UI.CreateLabel(ref container, UIMain, $"<color={configData.Colors.Background_Dark.Color}>{msg("storeTransfer")}</color>", 200, "0 0", "1 1", TextAnchor.MiddleCenter);
            UI.CreatePanel(ref container, UIMain, uiColors["light"], "0.01 0.01", "0.99 0.99", true);

            UI.CreateLabel(ref container, UIMain, $"{color1}{msg("transfer2", player.UserIDString)}</color>", 24, "0 0.82", "1 0.9");
            UI.CreateButton(ref container, UIMain, uiColors["buttonbg"], "1", 20, "0.27 0.3", "0.37 0.38", $"SRUI_TransferID {id} 1");
            UI.CreateButton(ref container, UIMain, uiColors["buttonbg"], "10", 20, "0.39 0.3", "0.49 0.38", $"SRUI_TransferID {id} 10");
            UI.CreateButton(ref container, UIMain, uiColors["buttonbg"], "100", 20, "0.51 0.3", "0.61 0.38", $"SRUI_TransferID {id} 100");
            UI.CreateButton(ref container, UIMain, uiColors["buttonbg"], "1000", 20, "0.63 0.3", "0.73 0.38", $"SRUI_TransferID {id} 1000");

            CuiHelper.DestroyUi(player, UIMain);
            CuiHelper.AddUi(player, container);
        }
        #endregion
        #endregion

        #region UI Functions
        private void CreateMenuButton(ref CuiElementContainer container, string panelName, string buttonname, string command, int number)
        {
            Vector2 dimensions = new Vector2(0.1f, 0.6f);
            Vector2 origin = new Vector2(0.2f, 0.2f);
            Vector2 offset = new Vector2((0.005f + dimensions.x) * number, 0);

            Vector2 posMin = origin + offset;
            Vector2 posMax = posMin + dimensions;

            UI.CreateButton(ref container, panelName, uiColors["buttonbg"], buttonname, 16, $"{posMin.x} {posMin.y}", $"{posMax.x} {posMax.y}", command);
        }
        private void CreateSubMenu(ref CuiElementContainer container, string panelName, bool[] hasItems, string npcId = null)
        {
            Category[] categories = new Category[] { Category.Ammunition, Category.Attire, Category.Component, Category.Construction, Category.Food, Category.Items, Category.Medical, Category.Misc, Category.Resources, Category.Tool, Category.Traps, Category.Weapon };

            float sizeX = 0.96f / categories.Length;
            int i = 0;
            int y = 0;
            foreach (var cat in categories)
            {
                if (hasItems[y])
                {
                    float xMin = 0.02f + (sizeX * i) + 0.003f;
                    float xMax = xMin + sizeX - 0.006f;
                    UI.CreateButton(ref container, panelName, uiColors["buttonbg"], msg(cat.ToString()), 12, $"{xMin} 0.945", $"{xMax} 0.985", $"SRUI_ChangeElement Items 0 {npcId ?? "null"} {cat.ToString()}");
                    i++;
                }
                y++;
            }
        }
        private void CreateItemEntry(ref CuiElementContainer container, string panelName, string itemId, RewardData.RewardItem item, int number)
        {
            Vector2 dimensions = new Vector2(0.1f, 0.19f);
            Vector2 origin = new Vector2(0.03f, 0.72f);
            float offsetY = 0;
            float offsetX = 0;

            if (number > 0 && number < 9)
                offsetX = (0.005f + dimensions.x) * number;
            if (number > 8 && number < 18)
            {
                offsetX = (0.005f + dimensions.x) * (number - 9);
                offsetY = (0.02f + dimensions.y) * 1;
            }
            if (number > 17 && number < 27)
            {
                offsetX = (0.005f + dimensions.x) * (number - 18);
                offsetY = (0.02f + dimensions.y) * 2;
            }
            if (number > 26 && number < 36)
            {
                offsetX = (0.005f + dimensions.x) * (number - 27);
                offsetY = (0.02f + dimensions.y) * 3;
            }

            Vector2 offset = new Vector2(offsetX, -offsetY);
            Vector2 posMin = origin + offset;
            Vector2 posMax = posMin + dimensions;

            string itemIcon = string.IsNullOrEmpty(item.customIcon) ? GetImage(item.shortname, item.skinId) : GetImage(item.customIcon, 0);
            if (!string.IsNullOrEmpty(itemIcon))
            {
                UI.LoadImage(ref container, panelName, itemIcon, $"{posMin.x + 0.02} {posMin.y + 0.08}", $"{posMax.x - 0.02} {posMax.y}");
                if (item.amount > 1)
                    UI.CreateLabel(ref container, panelName, $"{color1}x{item.amount}</color>", 16, $"{posMin.x + 0.02} {posMin.y + 0.09}", $"{posMax.x - 0.02} {posMax.y - 0.02}", TextAnchor.LowerLeft);
            }
            UI.CreateLabel(ref container, panelName, $"{item.displayName}{(item.isBp ? " " + msg("isBp") : "")}", 14, $"{posMin.x} {posMin.y + 0.04}", $"{posMax.x} {posMin.y + 0.09}");
            UI.CreateButton(ref container, panelName, uiColors["buttonbg"], $"{msg("storeCost")}: {item.cost}", 14, $"{posMin.x + 0.015} {posMin.y}", $"{posMax.x - 0.015} {posMin.y + 0.04}", $"SRUI_BuyItem {itemId}");
        }
        private void CreateKitCommandEntry(ref CuiElementContainer container, string panelName, string displayName, string name, string description, int cost, int number, bool kit, string icon = null)
        {
            Vector2 dimensions = new Vector2(0.8f, 0.079f);
            Vector2 origin = new Vector2(0.03f, 0.86f);
            float offsetY = (0.004f + dimensions.y) * number;
            Vector2 offset = new Vector2(0, offsetY);
            Vector2 posMin = origin - offset;
            Vector2 posMax = posMin + dimensions;
            string command = kit ? $"SRUI_BuyKit {name}" : $"SRUI_BuyCommand {name}";

            if (!string.IsNullOrEmpty(icon))
            {
                string iconId = GetImage(icon);
                if (!string.IsNullOrEmpty(iconId))
                {
                    UI.LoadImage(ref container, panelName, iconId, $"{posMin.x} {posMin.y}", $"{posMin.x + 0.05} {posMax.y}");
                    posMin.x = 0.09f;
                }
            }

            UI.CreateLabel(ref container, panelName, $"{color1}{displayName}</color> -- {color2}{description}</color>", 16, $"{posMin.x} {posMin.y}", $"{posMax.x} {posMax.y}", TextAnchor.MiddleLeft);
            UI.CreateButton(ref container, panelName, uiColors["buttonbg"], $"{msg("storeCost")}: {cost}", 16, $"0.87 {posMin.y + 0.02}", $"0.97 {posMax.y - 0.015f}", command);
        }
        private void CreatePlayerNameEntry(ref CuiElementContainer container, string panelName, string name, string id, int number)
        {
            var pos = CalcPlayerNamePos(number);
            UI.CreateButton(ref container, panelName, uiColors["buttonbg"], name, 10, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"SRUI_TransferNext {id}");
        }
        private float[] CalcPlayerNamePos(int number)
        {
            Vector2 position = new Vector2(0.014f, 0.82f);
            Vector2 dimensions = new Vector2(0.12f, 0.055f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 8)
            {
                offsetX = (0.002f + dimensions.x) * number;
            }
            if (number > 7 && number < 16)
            {
                offsetX = (0.002f + dimensions.x) * (number - 8);
                offsetY = (-0.0055f - dimensions.y) * 1;
            }
            if (number > 15 && number < 24)
            {
                offsetX = (0.002f + dimensions.x) * (number - 16);
                offsetY = (-0.0055f - dimensions.y) * 2;
            }
            if (number > 23 && number < 32)
            {
                offsetX = (0.002f + dimensions.x) * (number - 24);
                offsetY = (-0.0055f - dimensions.y) * 3;
            }
            if (number > 31 && number < 40)
            {
                offsetX = (0.002f + dimensions.x) * (number - 32);
                offsetY = (-0.0055f - dimensions.y) * 4;
            }
            if (number > 39 && number < 48)
            {
                offsetX = (0.002f + dimensions.x) * (number - 40);
                offsetY = (-0.0055f - dimensions.y) * 5;
            }
            if (number > 47 && number < 56)
            {
                offsetX = (0.002f + dimensions.x) * (number - 48);
                offsetY = (-0.0055f - dimensions.y) * 6;
            }
            if (number > 55 && number < 64)
            {
                offsetX = (0.002f + dimensions.x) * (number - 56);
                offsetY = (-0.0055f - dimensions.y) * 7;
            }
            if (number > 63 && number < 72)
            {
                offsetX = (0.002f + dimensions.x) * (number - 64);
                offsetY = (-0.0055f - dimensions.y) * 8;
            }
            if (number > 71 && number < 80)
            {
                offsetX = (0.002f + dimensions.x) * (number - 72);
                offsetY = (-0.0055f - dimensions.y) * 9;
            }
            if (number > 79 && number < 88)
            {
                offsetX = (0.002f + dimensions.x) * (number - 80);
                offsetY = (-0.0055f - dimensions.y) * 10;
            }
            if (number > 87 && number < 96)
            {
                offsetX = (0.002f + dimensions.x) * (number - 88);
                offsetY = (-0.0055f - dimensions.y) * 11;
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }
        private float[] CalcPosInv(int number)
        {
            Vector2 dimensions = new Vector2(0.45f, 0.04f);
            Vector2 origin = new Vector2(0.015f, 0.86f);
            float offsetY = 0.005f;
            float offsetX = 0.033f;
            float posX = 0;
            float posY = 0;
            if (number < 18)
            {
                posX = origin.x;
                posY = (offsetY + dimensions.y) * number;
            }
            else
            {
                number -= 18;
                posX = offsetX + dimensions.x;
                posY = (offsetY + dimensions.y) * number;
            }
            Vector2 offset = new Vector2(posX, -posY);
            Vector2 posMin = origin + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }
        #endregion

        #region UI Commands
        [ConsoleCommand("SRUI_BuyKit")]
        private void cmdBuyKit(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var kitName = arg.FullString;
            if (rewardData.kits.ContainsKey(kitName))
            {
                var kit = rewardData.kits[kitName];

                double remainingTime = 0;
                if (cooldownData.HasCooldown(player.userID, PurchaseType.Kit, kitName, out remainingTime))
                {
                    PopupMessage(player, string.Format(msg("hasCooldownKit", player.UserIDString), FormatTime(remainingTime)));
                    return;
                }

                var pd = CheckPoints(player.userID);
                if (pd >= kit.cost)
                {
                    if (TakePoints(player.userID, kit.cost, "Kit " + kit.displayName) != null)
                    {
                        Kits?.Call("GiveKit", new object[] { player, kit.kitName });

                        if (kit.cooldown > 0)
                            cooldownData.AddCooldown(player.userID, PurchaseType.Kit, kitName, kit.cooldown);

                        PopupMessage(player, string.Format(msg("buyKit", player.UserIDString), kitName));
                        return;
                    }
                }
                PopupMessage(player, msg("notEnoughPoints", player.UserIDString));
                return;
            }
            PopupMessage(player, msg("errorKit", player.UserIDString));
            return;
        }

        [ConsoleCommand("SRUI_BuyCommand")]
        private void cmdBuyCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var commandname = arg.FullString;
            if (rewardData.commands.ContainsKey(commandname))
            {
                var command = rewardData.commands[commandname];

                double remainingTime = 0;
                if (cooldownData.HasCooldown(player.userID, PurchaseType.Command, commandname, out remainingTime))
                {
                    PopupMessage(player, string.Format(msg("hasCooldownCommand", player.UserIDString), FormatTime(remainingTime)));
                    return;
                }

                var pd = CheckPoints(player.userID);
                if (pd >= command.cost)
                {
                    if (TakePoints(player.userID, command.cost, "Command") != null)
                    {
                        foreach (var cmd in command.commands)
                            rust.RunServerCommand(cmd.Replace("$player.id", player.UserIDString).Replace("$player.name", player.displayName).Replace("$player.x", player.transform.position.x.ToString()).Replace("$player.y", player.transform.position.y.ToString()).Replace("$player.z", player.transform.position.z.ToString()));

                        if (command.cooldown > 0)
                            cooldownData.AddCooldown(player.userID, PurchaseType.Command, commandname, command.cooldown);

                        PopupMessage(player, string.Format(msg("buyCommand", player.UserIDString), commandname));
                        return;
                    }
                }
                PopupMessage(player, msg("notEnoughPoints", player.UserIDString));
                return;
            }
            PopupMessage(player, msg("errorCommand", player.UserIDString));
            return;
        }

        [ConsoleCommand("SRUI_BuyItem")]
        private void cmdBuyItem(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var itemname = arg.GetString(0);
            if (rewardData.items.ContainsKey(itemname))
            {
                var item = rewardData.items[itemname];

                double remainingTime = 0;
                if (cooldownData.HasCooldown(player.userID, PurchaseType.Item, itemname, out remainingTime))
                {
                    PopupMessage(player, string.Format(msg("hasCooldownItem", player.UserIDString), FormatTime(remainingTime)));
                    return;
                }

                var pd = CheckPoints(player.userID);
                if (pd >= item.cost)
                {
                    if (player.inventory.containerMain.itemList.Count == 24)
                    {
                        PopupMessage(player, msg("fullInv", player.UserIDString));
                        return;
                    }

                    if (TakePoints(player.userID, item.cost, item.displayName) != null)
                    {
                        GiveItem(player, itemname);

                        if (item.cooldown > 0)
                            cooldownData.AddCooldown(player.userID, PurchaseType.Item, itemname, item.cooldown);

                        PopupMessage(player, string.Format(msg("buyItem", player.UserIDString) + $"{(item.isBp ? " " + msg("isBp", player.UserIDString) : "")}", item.amount, item.displayName));
                        return;
                    }
                }
                PopupMessage(player, msg("notEnoughPoints", player.UserIDString));
                return;
            }
            PopupMessage(player, msg("errorItem", player.UserIDString));
            return;
        }

        [ConsoleCommand("SRUI_ChangeElement")]
        private void cmdChangeElement(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            string type = arg.GetString(0);
            int page = 0;
            string npcid = null;
            Category cat = Category.None;

            if (arg.Args.Length >= 2)
                page = arg.GetInt(1);

            if (arg.Args.Length >= 3)
            {
                npcid = arg.GetString(2);
                if (npcid == "null")
                    npcid = string.Empty;
            }

            if (arg.Args.Length >= 4)
                cat = (Category)Enum.Parse(typeof(Category), arg.GetString(3), true);

            switch (type)
            {
                case "Kits":
                    uiManager.SwitchElement(player, UIPanel.Kits, Category.None, page, npcid);
                    return;
                case "Commands":
                    uiManager.SwitchElement(player, UIPanel.Commands, Category.None, page, npcid);
                    return;
                case "Items":
                    uiManager.SwitchElement(player, UIPanel.Items, cat == Category.None ? Category.Ammunition : cat, page, npcid);
                    return;
                case "Exchange":
                    uiManager.SwitchElement(player, UIPanel.Exchange, Category.None, 0, npcid);
                    return;
                case "Transfer":
                    uiManager.SwitchElement(player, UIPanel.Transfer, Category.None, 0, npcid);
                    return;
                case "Sell":
                    uiManager.SwitchElement(player, UIPanel.Sell, Category.None, 0, npcid);
                    return;
            }
        }

        [ConsoleCommand("SRUI_Exchange")]
        private void cmdExchange(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var type = int.Parse(arg.GetString(0).Replace("'", ""));
            if (type == 1)
            {
                if (CheckPoints(player.userID) < configData.Exchange.RP)
                {
                    PopupMessage(player, msg("notEnoughPoints", player.UserIDString));
                    return;
                }
                if (TakePoints(player.userID, configData.Exchange.RP, "RP Exchange") != null)
                {
                    Economics?.Call("Deposit", player.UserIDString, (double)configData.Exchange.Economics);
                    PopupMessage(player, $"{msg("exchange", player.UserIDString)}{configData.Exchange.RP} {msg("storeRP", player.UserIDString)} for {configData.Exchange.Economics} {msg("storeCoins", player.UserIDString)}");
                }
            }
            else
            {
                double amount = (double)Economics?.Call("Balance", player.UserIDString);
                if (amount < configData.Exchange.Economics)
                {
                    PopupMessage(player, msg("notEnoughCoins", player.UserIDString));
                    return;
                }
                if ((bool)Economics?.Call("Withdraw", player.UserIDString, (double)configData.Exchange.Economics))
                {
                    AddPoints(player.userID, configData.Exchange.RP);
                    PopupMessage(player, $"{msg("exchange", player.UserIDString)}{configData.Exchange.Economics} {msg("storeCoins", player.UserIDString)} for {configData.Exchange.RP} {msg("storeRP", player.UserIDString)}");
                }
            }
        }

        [ConsoleCommand("SRUI_Transfer")]
        private void ccmdTransfer(ConsoleSystem.Arg args)
        {
            var player = args.Connection.player as BasePlayer;
            if (player == null)
                return;
            var type = args.GetInt(0);
            uiManager.SwitchElement(player, UIPanel.Transfer, Category.None, 0, uiManager.GetNPCInUse(player));
        }

        [ConsoleCommand("SRUI_TransferNext")]
        private void ccmdTransferNext(ConsoleSystem.Arg args)
        {
            var player = args.Connection.player as BasePlayer;
            if (player == null)
                return;
            var ID = args.GetString(0);
            var name = (covalence.Players.FindPlayerById(ID)?.Object as BasePlayer)?.displayName ?? ID;
            TransferElement(player, name, ID);
        }

        [ConsoleCommand("SRUI_TransferID")]
        private void ccmdTransferID(ConsoleSystem.Arg args)
        {
            var player = args.Connection.player as BasePlayer;
            if (player == null)
                return;
            string ID = args.GetString(0);
            int amount = args.GetInt(1);
            string name = (covalence.Players.FindPlayerById(ID)?.Object as BasePlayer)?.displayName ?? ID;
            var hasPoints = CheckPoints(player.userID);
            if (hasPoints >= amount)
            {
                if (TakePoints(player.userID, amount) != null)
                {
                    AddPoints(ID, amount);
                    PopupMessage(player, string.Format(msg("transfer3"), amount, msg("storeRP"), name));
                    return;
                }
            }
            PopupMessage(player, msg("notEnoughPoints"));
        }

        [ConsoleCommand("SRUI_DestroyAll")]
        private void cmdDestroyAll(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            uiManager.DestroyUI(player, true);
        }

        [ConsoleCommand("SRUI_CancelSale")]
        private void cmdCancelSale(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            uiManager.SwitchElement(player, UIPanel.Sell, Category.None, 0, uiManager.GetNPCInUse(player));
        }

        [ConsoleCommand("SRUI_SellItem")]
        private void cmdSellItem(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            string shortname = arg.GetString(0);
            ulong skinId = arg.GetUInt64(1);
            int amount = arg.GetInt(2);
            var max = GetAmount(player, shortname, skinId);

            if (amount <= 0)
                amount = 1;
            if (amount > max)
                amount = max;

            SellItem(player, shortname, skinId, amount);
        }

        [ConsoleCommand("SRUI_Sell")]
        private void cmdSell(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            string itemId = arg.GetString(0);
            ulong skinId = arg.GetUInt64(1);
            int amount = arg.GetInt(2);
            var saleItem = saleData.items[itemId][skinId];
            int salePrice = (int)Math.Floor(saleItem.price * amount);

            if (TakeResources(player, itemId, skinId, amount))
            {
                AddPoints(player.userID, salePrice);

                if (configData.Options.Logs)
                {
                    var message = $"{player.displayName} sold {amount}x {itemId} for {salePrice}";
                    LogToFile($"Sold Items", $"[{DateTime.Now.ToString("hh:mm:ss")}] {message}", this);
                }

                uiManager.SwitchElement(player, UIPanel.Sell, Category.None, 0, uiManager.GetNPCInUse(player));
                PopupMessage(player, string.Format(msg("saleSuccess"), amount, saleItem.displayName, salePrice, msg("storeRP")));
            }
        }
        #endregion

        #region Oxide Hooks
        void Loaded()
        {
            lang.RegisterMessages(messages, this);

            playerdata = Interface.Oxide.DataFileSystem.GetFile("ServerRewards/player_data");
            npcdata = Interface.Oxide.DataFileSystem.GetFile("ServerRewards/npc_data");
            rewarddata = Interface.Oxide.DataFileSystem.GetFile("ServerRewards/reward_data");
            saledata = Interface.Oxide.DataFileSystem.GetFile("ServerRewards/sale_data");
            cooldowndata = Interface.Oxide.DataFileSystem.GetFile("ServerRewards/cooldown_data");

            ins = this;
            uiManager = new UIManager();
        }
        void OnServerInitialized()
        {
            LoadVariables();
            LoadData();

            if (!ImageLibrary)
            {
                PrintWarning("Image Library not detected, unloading ServerRewards");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }

            itemIds = ItemManager.itemList.ToDictionary(x => x.itemid, v => v.shortname);
            itemNames = ItemManager.itemList.ToDictionary(x => x.shortname, v => v.displayName.translated);
            LoadUIColors();
            LoadAllImages();
            UpdatePriceList();

            //CreateAllElements();

            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerInit(player);

            SaveLoop();
        }
        void OnPlayerInit(BasePlayer player)
        {
            if (player != null)
            {
                uiManager.DestroyUI(player, true);
                var ID = player.userID;
                if (CheckPoints(ID) > 0)
                    InformPoints(player);
            }
        }
        void OnPlayerDisconnected(BasePlayer player) => uiManager.DestroyUI(player, true);
        void Unload()
        {
            if (saveTimer != null)
                saveTimer.Destroy();
            foreach (var player in BasePlayer.activePlayerList)
                uiManager.DestroyUI(player, true);
            SaveRP();
        }
        #endregion

        #region Functions
        private void LoadUIColors()
        {
            uiColors.Add("dark", UI.Color(configData.Colors.Background_Dark.Color, configData.Colors.Background_Dark.Alpha));
            uiColors.Add("medium", UI.Color(configData.Colors.Background_Medium.Color, configData.Colors.Background_Medium.Alpha));
            uiColors.Add("light", UI.Color(configData.Colors.Background_Light.Color, configData.Colors.Background_Light.Alpha));
            uiColors.Add("buttonbg", UI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha));
            uiColors.Add("buttoncom", UI.Color(configData.Colors.Button_Accept.Color, configData.Colors.Button_Accept.Alpha));
            uiColors.Add("buttongrey", UI.Color(configData.Colors.Button_Inactive.Color, configData.Colors.Button_Inactive.Alpha));
        }

        private void LoadAllImages()
        {
            ImageLibrary.Call("LoadImageList", Title, rewardData.items.Where(y => string.IsNullOrEmpty(y.Value.customIcon)).Select(x => new KeyValuePair<string, ulong>(x.Value.shortname, x.Value.skinId)).ToList(), new Action(CreateAllElements));
            Dictionary<string, string> newLoadOrder = new Dictionary<string, string>();

            string dataDir = $"file://{Interface.Oxide.DataDirectory}{Path.DirectorySeparatorChar}ServerRewards{Path.DirectorySeparatorChar}Images{Path.DirectorySeparatorChar}";
            foreach(var item in rewardData.items.Where(x => !string.IsNullOrEmpty(x.Value.customIcon)))
            {
                if (newLoadOrder.ContainsKey(item.Value.customIcon))
                    continue;
                var url = item.Value.customIcon;
                if (!url.StartsWith("http") && !url.StartsWith("www"))
                    url = $"{dataDir}{item.Value.customIcon}.png";
                newLoadOrder.Add(item.Value.customIcon, url);
            }
            foreach (var kit in rewardData.kits)
            {
                if (!string.IsNullOrEmpty(kit.Value.iconName))
                {
                    if (newLoadOrder.ContainsKey(kit.Value.iconName))
                        continue;
                    var url = kit.Value.iconName;
                    if (!url.StartsWith("http") && !url.StartsWith("www"))
                        url = $"{dataDir}{kit.Value.iconName}.png";
                    newLoadOrder.Add(kit.Value.iconName, url);
                }
            }
            foreach (var command in rewardData.commands)
            {
                if (!string.IsNullOrEmpty(command.Value.iconName))
                {
                    if (newLoadOrder.ContainsKey(command.Value.iconName))
                        continue;
                    var url = command.Value.iconName;
                    if (!url.StartsWith("http") && !url.StartsWith("www"))
                        url = $"{dataDir}{command.Value.iconName}.png";
                    newLoadOrder.Add(command.Value.iconName, url);
                }
            }
            if (newLoadOrder.Count > 0)
                ImageLibrary.Call("ImportImageList", Title, newLoadOrder);
        }

        private void SaveLoop() => saveTimer = timer.Once(configData.Options.SaveInterval, () => { SaveRP(); SaveLoop(); });

        private void SendMSG(BasePlayer player, string msg, string keyword = "title")
        {
            if (keyword == "title") keyword = lang.GetMessage("title", this, player.UserIDString);
            SendReply(player, $"{color1}{keyword}</color> {color2}{msg}</color>");
        }

        private void InformPoints(BasePlayer player)
        {
            var outstanding = CheckPoints(player.userID);
            if (configData.Options.NPCOnly)
                SendMSG(player, string.Format(msg("msgOutRewardsnpc", player.UserIDString), outstanding));
            else SendMSG(player, string.Format(msg("msgOutRewards1", player.UserIDString), outstanding));
        }

        private object FindPlayer(BasePlayer player, string arg)
        {
            ulong targetID;
            if (ulong.TryParse(arg, out targetID))
            {
                var target = covalence.Players.FindPlayer(arg);
                if (target != null && target.Object is BasePlayer)
                    return target.Object as BasePlayer;
            }

            var targets = covalence.Players.FindPlayers(arg);

            if (targets.ToArray().Length == 0)
            {
                if (player != null)
                {
                    SendMSG(player, msg("noPlayers", player.UserIDString));
                    return null;
                }
                else return msg("noPlayers");
            }
            if (targets.ToArray().Length > 1)
            {
                if (player != null)
                {
                    SendMSG(player, msg("multiPlayers", player.UserIDString));
                    return null;
                }
                else return msg("multiPlayers");
            }
            if ((targets.ToArray()[0].Object as BasePlayer) != null)
                return targets.ToArray()[0].Object as BasePlayer;
            else
            {
                if (player != null)
                {
                    SendMSG(player, msg("noPlayers", player.UserIDString));
                    return null;
                }
                else return msg("noPlayers");
            }
        }

        private bool RemovePlayer(ulong ID)
        {
            if (playerRP.ContainsKey(ID))
            {
                playerRP.Remove(ID);
                return true;
            }
            return false;
        }

        private void SendEchoConsole(Network.Connection cn, string msg)
        {
            if (Network.Net.sv.IsConnected())
            {
                Network.Net.sv.write.Start();
                Network.Net.sv.write.PacketID(Network.Message.Type.ConsoleMessage);
                Network.Net.sv.write.String(msg);
                Network.Net.sv.write.Send(new Network.SendInfo(cn));
            }
        }

        private void OpenStore(BasePlayer player, string npcid = null)
        {
            if (!isILReady)
            {
                SendMSG(player, "", msg("imWait", player.UserIDString));
                return;
            }
            if (uiManager.IsOpen(player))
                return;

            object success = Interface.Call("canShop", player);
            if (success != null)
            {
                string message = "You are not allowed to shop at the moment";
                if (success is string)
                    message = (string)success;
                SendReply(player, message);
                return;
            }

            CloseMap(player);
            uiManager.AddUI(player, UIPanel.Navigation, Category.None, 0, npcid);

            if (configData.Tabs.Kits)
                uiManager.AddUI(player, UIPanel.Kits, Category.None, 0, npcid);
            else if (configData.Tabs.Items)
                uiManager.AddUI(player, UIPanel.Items, Category.Ammunition, 0, npcid);
            else if (configData.Tabs.Commands)
                uiManager.AddUI(player, UIPanel.Commands, Category.None, 0, npcid);
            else
            {
                uiManager.DestroyUI(player, true);
                PopupMessage(player, "All reward options are currently disabled. Closing the store.");
            }
        }

        private void GiveItem(BasePlayer player, string itemkey)
        {
            if (rewardData.items.ContainsKey(itemkey))
            {
                var entry = rewardData.items[itemkey];
                Item item = null;
                if (entry.isBp)
                {
                    item = ItemManager.CreateByItemID(blueprintId, entry.amount, entry.skinId);
                    item.blueprintTarget = ItemManager.itemList.Find(x => x.shortname == entry.shortname)?.itemid ?? 0;
                }
                else item = ItemManager.CreateByName(entry.shortname, entry.amount, entry.skinId);
                player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
            }
        }

        private int GetAmount(BasePlayer player, string shortname, ulong skinid)
        {
            List<Item> items = player.inventory.AllItems().ToList().FindAll(x => x.info.shortname == shortname);
            int num = 0;
            foreach (Item item in items)
            {
                if (!item.IsBusy())
                {
                    if (item.skin == skinid)
                        num = num + item.amount;
                }
            }
            return num;
        }

        private bool TakeResources(BasePlayer player, string shortname, ulong skinId, int iAmount)
        {
            int num = TakeResourcesFrom(player, player.inventory.containerMain.itemList, shortname, skinId, iAmount);
            if (num < iAmount)
                num += TakeResourcesFrom(player, player.inventory.containerBelt.itemList, shortname, skinId, iAmount);
            if (num < iAmount)
                num += TakeResourcesFrom(player, player.inventory.containerWear.itemList, shortname, skinId, iAmount);
            if (num >= iAmount)
                return true;
            return false;
        }

        private int TakeResourcesFrom(BasePlayer player, List<Item> container, string shortname, ulong skinId, int iAmount)
        {
            List<Item> collect = new List<Item>();
            List<Item> items = new List<Item>();
            int num = 0;
            foreach (Item item in container)
            {
                if (item.info.shortname == shortname && item.skin == skinId)
                {
                    int num1 = iAmount - num;
                    if (num1 > 0)
                    {
                        if (item.amount <= num1)
                        {
                            if (item.amount <= num1)
                            {
                                num = num + item.amount;
                                items.Add(item);
                                if (collect != null)
                                    collect.Add(item);
                            }
                            if (num != iAmount)
                                continue;
                            break;
                        }
                        else
                        {
                            item.MarkDirty();
                            Item item1 = item;
                            item1.amount = item1.amount - num1;
                            num = num + num1;
                            Item item2 = ItemManager.CreateByName(shortname, 1, skinId);
                            item2.amount = num1;
                            item2.CollectedForCrafting(player);
                            if (collect != null)
                                collect.Add(item2);
                            break;
                        }
                    }
                }
            }
            foreach (Item item3 in items)
                item3.RemoveFromContainer();
            return num;
        }

        private string FormatTime(double time)
        {
            TimeSpan dateDifference = TimeSpan.FromSeconds((float)time);
            var days = dateDifference.Days;
            var hours = dateDifference.Hours;
            hours += (days * 24);
            var mins = dateDifference.Minutes;
            var secs = dateDifference.Seconds;
            return string.Format("{0:00}:{1:00}:{2:00}", hours, mins, secs);
        }

        public static double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
        #endregion

        #region Hooks and API
        #region API
        private object AddPoints(object userID, int amount)
        {
            ulong ID;
            var success = GetUserID(userID);
            if (success is bool)
                return false;
            else ID = (ulong)success;

            if (!playerRP.ContainsKey(ID))
                playerRP.Add(ID, amount);
            else playerRP[ID] += amount;

            if (configData.Options.Logs)
            {
                string message = string.Empty;
                BasePlayer player = BasePlayer.FindByID(ID);
                if (player != null)
                    message = $"{ID} - {player.displayName} has been given {amount}x RP";
                else message = $"(offline){ID} has been given {amount}x RP";

                LogToFile($"Earnings", $"[{DateTime.Now.ToString("hh:mm:ss")}] {message}", this);
            }
            return true;
        }
        private object TakePoints(object userID, int amount, string item = "")
        {
            ulong ID;
            var success = GetUserID(userID);
            if (success is bool)
                return false;
            else ID = (ulong)success;

            if (!playerRP.ContainsKey(ID)) return null;
            playerRP[ID] -= amount;

            if (configData.Options.Logs)
            {
                string message = string.Empty;
                BasePlayer player = BasePlayer.FindByID(ID);
                if (player != null)
                    message = $"{ID} - {player.displayName} has spent {amount}x RP{(string.IsNullOrEmpty(item) ? "" : $" on: {item}")}";
                else message = $"(offline){ID} has spent {amount}x RP";

                LogToFile($"SpentRP", $"[{DateTime.Now.ToString("hh:mm:ss")}] {message}", this);
            }
            return true;
        }
        private int CheckPoints(object userID)
        {
            ulong ID;
            var success = GetUserID(userID);
            if (success is bool)
                return 0;
            else ID = (ulong)success;

            if (!playerRP.ContainsKey(ID)) return 0;
            return playerRP[ID];
        }

        private object GetUserID(object userID)
        {
            if (userID == null)
                return false;
            if (userID is ulong)
                return (ulong)userID;
            else if (userID is string)
            {
                ulong ID = 0U;
                if (ulong.TryParse((string)userID, out ID))
                    return ID;
                return false;
            }
            else if (userID is BasePlayer)
                return (userID as BasePlayer).userID;
            else if (userID is IPlayer)
                return ulong.Parse((userID as IPlayer).Id);
            return false;
        }

        private JObject GetItemList()
        {
            var obj = new JObject();
            foreach (var item in rewardData.items)
            {
                var itemobj = new JObject();
                itemobj["shortname"] = item.Key;
                itemobj["skinid"] = item.Value.skinId;
                itemobj["amount"] = item.Value.amount;
                itemobj["cost"] = item.Value.cost;
                itemobj["category"] = item.Value.category.ToString();
                obj[item.Key] = itemobj;
            }
            return obj;
        }
        private bool AddItem(string shortname, ulong skinId, int amount, int cost, string category, bool isBp = false)
        {
            Category cat = (Category)Enum.Parse(typeof(Category), category, true);

            RewardData.RewardItem newItem = new RewardData.RewardItem
            {
                amount = amount,
                cost = cost,
                shortname = shortname,
                skinId = skinId,
                category = cat,
                isBp = isBp
            };
            string itemName = $"{newItem.shortname}_{newItem.skinId}";

            if (rewardData.items.ContainsKey(itemName))
                return false;

            rewardData.items.Add(itemName, newItem);
            return true;
        }
        #endregion

        #region Hooks
        private void AddImage(string fileName)
        {
            var url = fileName;
            if (!url.StartsWith("http") && !url.StartsWith("www"))
                url = $"file://{Interface.Oxide.DataDirectory}{Path.DirectorySeparatorChar}ServerRewards{Path.DirectorySeparatorChar}Images{Path.DirectorySeparatorChar}{fileName}.png";
            ImageLibrary?.Call("AddImage", url, fileName, 0UL);
        }
        private string GetImage(string fileName, ulong skin = 0)
        {
            string imageId = (string)ImageLibrary.Call("GetImage", fileName, skin);
            if (!string.IsNullOrEmpty(imageId))
                return imageId;
            return string.Empty;
        }
        private void CloseMap(BasePlayer player)
        {
            if (LustyMap)
            {
                LustyMap.Call("DisableMaps", player);
            }
        }
        private void OpenMap(BasePlayer player)
        {
            if (LustyMap)
            {
                LustyMap.Call("EnableMaps", player);
            }
        }
        private void AddMapMarker(float x, float z, string name, string icon = "rewarddealer")
        {
            if (LustyMap)
            {
                LustyMap.Call("AddMarker", x, z, name, icon);
            }
        }
        private void RemoveMapMarker(string name)
        {
            if (LustyMap)
                LustyMap.Call("RemoveMarker", name);
        }
        void OnUseNPC(BasePlayer npc, BasePlayer player)
        {
            if (player == null || npc == null) return;
            var npcID = npc.UserIDString;
            if (userNpc.ContainsKey(player.userID))
            {
                ModifyNPC(player, npc);
                return;
            }
            if (IsRegisteredNPC(npcID) && uiManager.NPCHasUI(npcID) && !uiManager.IsOpen(player))
                OpenStore(player, npcID);
        }
        #endregion
        #endregion

        #region NPC Registration
        private bool IsRegisteredNPC(string ID)
        {
            if (npcData.npcInfo.ContainsKey(ID))
                return true;
            return false;
        }
        [ChatCommand("srnpc")]
        void cmdSRNPC(BasePlayer player, string command, string[] args)
        {
            if (!isAuth(player)) return;
            if (args == null || args.Length == 0)
            {
                SendMSG(player, "/srnpc add - Add a new NPC vendor");
                SendMSG(player, "/srnpc remove - Remove a NPC vendor");
                SendMSG(player, "/srnpc loot - Create a custom loot table for the specified NPC vendor");
                return;
            }
            switch (args[0].ToLower())
            {
                case "add":
                    userNpc[player.userID] = UserNPC.Add;
                    SendMSG(player, "Press USE on the NPC you wish to add!");
                    return;
                case "remove":
                    userNpc[player.userID] = UserNPC.Remove;
                    SendMSG(player, "Press USE on the NPC you wish to remove!");
                    return;
                case "loot":
                    userNpc[player.userID] = UserNPC.Edit;
                    SendMSG(player, "Press USE on the NPC you wish to edit!");
                    return;
                default:
                    break;
            }
        }
        private void ModifyNPC(BasePlayer player, BasePlayer NPC)
        {
            var isRegistered = IsRegisteredNPC(NPC.UserIDString);
            var type = userNpc[player.userID];
            userNpc.Remove(player.userID);
            switch (type)
            {
                case UserNPC.Add:
                    {
                        if (isRegistered)
                        {
                            SendMSG(player, msg("npcExist", player.UserIDString));
                            return;
                        }
                        int key = npcData.npcInfo.Count + 1;
                        npcData.npcInfo.Add(NPC.UserIDString, new NPCData.NPCInfo { name = $"{msg("Reward Dealer")} {key}", x = NPC.transform.position.x, z = NPC.transform.position.z });
                        AddMapMarker(NPC.transform.position.x, NPC.transform.position.z, $"{msg("Reward Dealer")} {key}");
                        CreateNewElement(NPC.UserIDString, npcData.npcInfo[NPC.UserIDString]);
                        SendMSG(player, msg("npcNew"));
                        SaveNPC();
                    }
                    return;
                case UserNPC.Remove:
                    {
                        if (isRegistered)
                        {
                            RemoveMapMarker(npcData.npcInfo[NPC.UserIDString].name);
                            npcData.npcInfo.Remove(NPC.UserIDString);
                            uiManager.RemoveNPCUI(NPC.UserIDString);

                            for (int i = 0; i < npcData.npcInfo.Count; i++)
                            {
                                KeyValuePair<string, NPCData.NPCInfo> info = npcData.npcInfo.ElementAt(i);

                                if (info.Value.name.StartsWith(msg("Reward Dealer")))
                                {
                                    RemoveMapMarker(info.Value.name);
                                    info.Value.name = $"{msg("Reward Dealer")} {i + 1}";
                                    npcData.npcInfo[info.Key] = info.Value;
                                    AddMapMarker(info.Value.x, info.Value.z, info.Value.name);
                                }
                            }

                            SendMSG(player, msg("npcRem"));
                            SaveNPC();
                        }
                        else SendMSG(player, msg("npcNotAdded"));
                    }
                    return;
                case UserNPC.Edit:
                    {
                        if (isRegistered)
                        {
                            if (!npcCreator.ContainsKey(player.userID))
                                npcCreator.Add(player.userID, new KeyValuePair<string, NPCData.NPCInfo>(NPC.UserIDString, new NPCData.NPCInfo()));

                            if (npcData.npcInfo[NPC.UserIDString].useCustom)
                                npcCreator[player.userID] = new KeyValuePair<string, NPCData.NPCInfo>(NPC.UserIDString, npcData.npcInfo[NPC.UserIDString]);
                            var container = UI.CreateElementContainer(UIMain, uiColors["dark"], "0 0", "1 1");
                            UI.CreatePanel(ref container, UIMain, uiColors["light"], "0.01 0.01", "0.99 0.99", true);
                            UI.CreateLabel(ref container, UIMain, msg("cldesc", player.UserIDString), 18, "0.25 0.88", "0.75 0.98");
                            UI.CreateLabel(ref container, UIMain, msg("storeKits", player.UserIDString), 18, "0 0.8", "0.33 0.88");
                            UI.CreateLabel(ref container, UIMain, msg("storeItems", player.UserIDString), 18, "0.33 0.8", "0.66 0.88");
                            UI.CreateLabel(ref container, UIMain, msg("storeCommands", player.UserIDString), 18, "0.66 0.8", "1 0.88");
                            CuiHelper.AddUi(player, container);
                            NPCLootMenu(player);
                        }
                        else SendMSG(player, msg("npcNotAdded"));
                    }
                    return;
                default:
                    break;
            }
        }
        private void NPCLootMenu(BasePlayer player, int page = 0)
        {
            var container = UI.CreateElementContainer(UISelect, "0 0 0 0", "0 0", "1 1");
            UI.CreateButton(ref container, UISelect, uiColors["buttonbg"], msg("save", player.UserIDString), 16, "0.85 0.91", "0.95 0.96", "SRUI_NPCSave", TextAnchor.MiddleCenter, 0f);
            UI.CreateButton(ref container, UISelect, uiColors["buttonbg"], msg("storeClose", player.UserIDString), 16, "0.05 0.91", "0.15 0.96", "SRUI_NPCCancel", TextAnchor.MiddleCenter, 0f);

            string[] itemNames = rewardData.items.Keys.ToArray();
            string[] kitNames = rewardData.kits.Keys.ToArray();
            string[] commNames = rewardData.commands.Keys.ToArray();

            int maxCount = itemNames.Length;
            if (kitNames.Length > maxCount) maxCount = kitNames.Length;
            if (commNames.Length > maxCount) maxCount = commNames.Length;

            if (maxCount > 30)
            {
                var maxpages = (maxCount - 1) / 30 + 1;
                if (page < maxpages - 1)
                    UI.CreateButton(ref container, UISelect, uiColors["buttonbg"], ">>>", 18, "0.84 0.05", "0.97 0.1", $"SRUI_NPCPage {page + 1}", TextAnchor.MiddleCenter, 0f);
                if (page > 0)
                    UI.CreateButton(ref container, UISelect, uiColors["buttonbg"], "<<<", 18, "0.03 0.05", "0.16 0.1", $"SRUI_NPCPage {page - 1}", TextAnchor.MiddleCenter, 0f);
            }

            int maxComm = (30 * (page + 1));
            if (maxComm > commNames.Length)
                maxComm = commNames.Length;
            int commcount = 30 * page;

            int comm = 0;
            for (int n = commcount; n < maxComm; n++)
            {
                KeyValuePair<string, RewardData.RewardCommand> command = rewardData.commands.ElementAt(n);

                string color1 = uiColors["buttonbg"];
                string text1 = command.Value.displayName;
                string command1 = $"SRUI_CustomList Commands {command.Key.Replace(" ", "%!%")} true {page}";
                string color2 = "0 0 0 0";
                string text2 = "";
                string command2 = "";

                if (npcCreator[player.userID].Value.commands.Contains(command.Key))
                {
                    color1 = uiColors["buttoncom"];
                    command1 = $"SRUI_CustomList Commands {command.Key.Replace(" ", "%!%")} false {page}";
                }
                if (n + 1 < commNames.Length)
                {
                    command = rewardData.commands.ElementAt(n + 1);
                    color2 = uiColors["buttonbg"];
                    text2 = command.Value.displayName;
                    command2 = $"SRUI_CustomList Commands {command.Key.Replace(" ", "%!%")} true {page}";
                    if (npcCreator[player.userID].Value.commands.Contains(command.Key))
                    {
                        color2 = uiColors["buttoncom"];
                        command2 = $"SRUI_CustomList Commands {command.Key.Replace(" ", "%!%")} false {page}";
                    }
                    ++n;
                }

                CreateItemButton(ref container, UISelect, color1, text1, command1, color2, text2, command2, comm, 0.66f);
                comm++;
            }

            int maxKit = (30 * (page + 1));
            if (maxKit > kitNames.Length)
                maxKit = kitNames.Length;
            int kitcount = 30 * page;

            int kits = 0;
            for (int n = kitcount; n < maxKit; n++)
            {
                KeyValuePair<string, RewardData.RewardKit> kit = rewardData.kits.ElementAt(n);

                string color1 = uiColors["buttonbg"];
                string text1 = kit.Value.displayName;
                string command1 = $"SRUI_CustomList Kits {kit.Key.Replace(" ", "%!%")} true {page}";
                string color2 = "0 0 0 0";
                string text2 = "";
                string command2 = "";
                if (npcCreator[player.userID].Value.kits.Contains(kit.Key))
                {
                    color1 = uiColors["buttoncom"];
                    command1 = $"SRUI_CustomList Kits {kit.Key.Replace(" ", "%!%")} false {page}";
                }
                if (n + 1 < kitNames.Length)
                {
                    kit = rewardData.kits.ElementAt(n + 1);
                    color2 = uiColors["buttonbg"];
                    text2 = kit.Value.displayName;
                    command2 = $"SRUI_CustomList Kits {kit.Key.Replace(" ", "%!%")} true {page}";
                    if (npcCreator[player.userID].Value.kits.Contains(kit.Key))
                    {
                        color2 = uiColors["buttoncom"];
                        command2 = $"SRUI_CustomList Kits {kit.Key.Replace(" ", "%!%")} false {page}";
                    }
                    ++n;
                }

                CreateItemButton(ref container, UISelect, color1, text1, command1, color2, text2, command2, kits, 0f);
                kits++;
            }

            int maxItem = (30 * (page + 1));
            if (maxItem > itemNames.Length)
                maxItem = itemNames.Length;
            int itemcount = 30 * page;

            int items = 0;
            for (int n = itemcount; n < maxItem; n++)
            {
                KeyValuePair<string, RewardData.RewardItem> item = rewardData.items.ElementAt(n);
                string color1 = uiColors["buttonbg"];
                string text1 = item.Value.displayName;
                string command1 = $"SRUI_CustomList Items {item.Key.Replace(" ", "%!%")} true {page}";
                string color2 = "0 0 0 0";
                string text2 = "";
                string command2 = "";

                if (npcCreator[player.userID].Value.items.Contains(item.Key))
                {
                    color1 = uiColors["buttoncom"];
                    command1 = $"SRUI_CustomList Items {item.Key.Replace(" ", "%!%")} false {page}";
                }
                if (n + 1 < rewardData.items.Count)
                {
                    item = rewardData.items.ElementAt(n + 1);
                    color2 = uiColors["buttonbg"];
                    text2 = item.Value.displayName;
                    command2 = $"SRUI_CustomList Items {item.Key.Replace(" ", "%!%")} true {page}";
                    if (npcCreator[player.userID].Value.items.Contains(item.Key))
                    {
                        color2 = uiColors["buttoncom"];
                        command2 = $"SRUI_CustomList Items {item.Key.Replace(" ", "%!%")} false {page}";
                    }
                    ++n;
                }

                CreateItemButton(ref container, UISelect, color1, text1, command1, color2, text2, command2, items, 0.33f);
                items++;
            }
            if (npcCreator[player.userID].Value.useCustom)
                UI.CreateButton(ref container, UISelect, uiColors["buttoncom"], msg("useCustom"), 16, "0.21 0.05", "0.34 0.1", $"SRUI_NPCOption {page} custom", TextAnchor.MiddleCenter, 0f);
            else UI.CreateButton(ref container, UISelect, uiColors["buttonbg"], msg("useCustom"), 16, "0.21 0.05", "0.34 0.1", $"SRUI_NPCOption {page} custom", TextAnchor.MiddleCenter, 0f);

            if (npcCreator[player.userID].Value.canExchange)
                UI.CreateButton(ref container, UISelect, uiColors["buttoncom"], msg("allowExchange"), 16, "0.36 0.05", "0.49 0.1", $"SRUI_NPCOption {page} exchange", TextAnchor.MiddleCenter, 0f);
            else UI.CreateButton(ref container, UISelect, uiColors["buttonbg"], msg("allowExchange"), 16, "0.36 0.05", "0.49 0.1", $"SRUI_NPCOption {page} exchange", TextAnchor.MiddleCenter, 0f);

            if (npcCreator[player.userID].Value.canSell)
                UI.CreateButton(ref container, UISelect, uiColors["buttoncom"], msg("allowSales"), 16, "0.51 0.05", "0.64 0.1", $"SRUI_NPCOption {page} sales", TextAnchor.MiddleCenter, 0f);
            else UI.CreateButton(ref container, UISelect, uiColors["buttonbg"], msg("allowSales"), 16, "0.51 0.05", "0.64 0.1", $"SRUI_NPCOption {page} sales", TextAnchor.MiddleCenter, 0f);

            if (npcCreator[player.userID].Value.canTransfer)
                UI.CreateButton(ref container, UISelect, uiColors["buttoncom"], msg("allowTransfer"), 16, "0.66 0.05", "0.79 0.1", $"SRUI_NPCOption {page} transfer", TextAnchor.MiddleCenter, 0f);
            else UI.CreateButton(ref container, UISelect, uiColors["buttonbg"], msg("allowTransfer"), 16, "0.66 0.05", "0.79 0.1", $"SRUI_NPCOption {page} transfer", TextAnchor.MiddleCenter, 0f);

            CuiHelper.DestroyUi(player, UISelect);
            CuiHelper.AddUi(player, container);
        }
        void CreateItemButton(ref CuiElementContainer container, string panel, string b1color, string b1text, string b1command, string b2color, string b2text, string b2command, int number, float xPos)
        {
            float offsetX = 0.01f;
            float offsetY = 0.0047f;
            Vector2 dimensions = new Vector2(0.15f, 0.04f);
            Vector2 origin = new Vector2(xPos + offsetX, 0.76f);

            Vector2 offset = new Vector2(0, (offsetY + dimensions.y) * number);

            Vector2 posMin = origin - offset;
            Vector2 posMax = posMin + dimensions;

            UI.CreateButton(ref container, panel, b1color, b1text, 14, $"{posMin.x} {posMin.y}", $"{posMax.x} {posMax.y}", b1command, TextAnchor.MiddleCenter, 0f);
            UI.CreateButton(ref container, panel, b2color, b2text, 14, $"{posMin.x + offsetX + dimensions.x} {posMin.y}", $"{posMax.x + offsetX + dimensions.x} {posMax.y}", b2command, TextAnchor.MiddleCenter, 0f);
        }

        [ConsoleCommand("SRUI_CustomList")]
        private void cmdCustomList(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            string type = arg.GetString(0);
            string key = arg.GetString(1).Replace("%!%", " ");
            bool isAdding = arg.GetBool(2);
            int page = arg.GetInt(3);

            switch (type)
            {
                case "Kits":
                    if (isAdding)
                        npcCreator[player.userID].Value.kits.Add(key);
                    else npcCreator[player.userID].Value.kits.Remove(key);
                    break;
                case "Commands":
                    if (isAdding)
                        npcCreator[player.userID].Value.commands.Add(key);
                    else npcCreator[player.userID].Value.commands.Remove(key);
                    break;
                case "Items":
                    if (isAdding)
                        npcCreator[player.userID].Value.items.Add(key);
                    else npcCreator[player.userID].Value.items.Remove(key);
                    break;
            }
            NPCLootMenu(player, page);
        }
        [ConsoleCommand("SRUI_NPCPage")]
        private void cmdNPCPage(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            int page = arg.GetInt(0);
            NPCLootMenu(player, page);
        }
        [ConsoleCommand("SRUI_NPCOption")]
        private void cmdNPCOption(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            switch (arg.Args[1])
            {
                case "exchange":
                    npcCreator[player.userID].Value.canExchange = !npcCreator[player.userID].Value.canExchange;
                    break;
                case "transfer":
                    npcCreator[player.userID].Value.canTransfer = !npcCreator[player.userID].Value.canTransfer;
                    break;
                case "sales":
                    npcCreator[player.userID].Value.canSell = !npcCreator[player.userID].Value.canSell;
                    break;
                case "custom":
                    npcCreator[player.userID].Value.useCustom = !npcCreator[player.userID].Value.useCustom;
                    break;
                default:
                    break;
            }
            int page = arg.GetInt(0);
            NPCLootMenu(player, page);
        }
        [ConsoleCommand("SRUI_NPCCancel")]
        private void cmdNPCCancel(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            npcCreator.Remove(player.userID);
            CuiHelper.DestroyUi(player, UIMain);
            CuiHelper.DestroyUi(player, UISelect);
            SendReply(player, msg("clcanc", player.UserIDString));
        }
        [ConsoleCommand("SRUI_NPCSave")]
        private void cmdNPCSave(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            CuiHelper.DestroyUi(player, UIMain);
            CuiHelper.DestroyUi(player, UISelect);
            var info = npcCreator[player.userID];
            if (info.Value.useCustom)
            {
                if (info.Value.kits.Count > 0)
                    info.Value.sellKits = true;
                else info.Value.sellKits = false;
                if (info.Value.items.Count > 0)
                    info.Value.sellItems = true;
                else info.Value.sellItems = false;
                if (info.Value.commands.Count > 0)
                    info.Value.sellCommands = true;
                else info.Value.sellCommands = false;
            }
            else
            {
                info.Value.sellKits = true;
                info.Value.sellItems = true;
                info.Value.sellCommands = true;
            }
            npcData.npcInfo[info.Key] = info.Value;
            SaveNPC();

            if (uiManager.npcElements.ContainsKey(info.Key))
                uiManager.npcElements.Remove(info.Key);
            CreateNewElement(info.Key, info.Value);
            npcCreator.Remove(player.userID);
            SendReply(player, msg("clootsucc", player.UserIDString));
        }
        #endregion

        #region Commands
        [ChatCommand("s")]
        private void cmdStore(BasePlayer player, string command, string[] args)
        {
            if ((configData.Options.NPCOnly && isAuth(player)) || !configData.Options.NPCOnly)
            {
                OpenStore(player);
            }
        }

        [ChatCommand("rewards")]
        private void cmdRewards(BasePlayer player, string command, string[] args)
        {
            if (args == null || args.Length == 0)
            {
                SendMSG(player, "V " + Version, msg("title", player.UserIDString));
                SendMSG(player, msg("chatCheck1", player.UserIDString), msg("chatCheck", player.UserIDString));
                SendMSG(player, msg("storeSyn2", player.UserIDString), msg("storeSyn21", player.UserIDString));
                if (isAuth(player))
                {
                    SendMSG(player, msg("chatAddKit", player.UserIDString), msg("addSynKit", player.UserIDString));
                    SendMSG(player, msg("chatAddItem2", player.UserIDString), msg("addSynItem2", player.UserIDString));
                    SendMSG(player, msg("chatAddCommand", player.UserIDString), msg("addSynCommand", player.UserIDString));
                    SendMSG(player, msg("editSynKit1", player.UserIDString), msg("editSynKit", player.UserIDString));
                    SendMSG(player, msg("editSynItem1", player.UserIDString), msg("editSynItem2", player.UserIDString));
                    SendMSG(player, msg("editSynCommand1", player.UserIDString), msg("editSynCommand", player.UserIDString));
                    SendMSG(player, msg("chatRemove", player.UserIDString), msg("remSynKit", player.UserIDString));
                    SendMSG(player, msg("chatRemove", player.UserIDString), msg("remSynItem", player.UserIDString));
                    SendMSG(player, msg("chatRemove", player.UserIDString), msg("remSynCommand", player.UserIDString));
                    SendMSG(player, msg("chatListOpt1", player.UserIDString), msg("chatListOpt", player.UserIDString));
                }
                return;
            }
            if (args.Length >= 1)
            {
                switch (args[0].ToLower())
                {
                    case "check":
                        int points = CheckPoints(player.userID);
                        SendMSG(player, string.Format(msg("tpointsAvail", player.UserIDString), points));
                        return;
                    #region Lists
                    case "list":
                        if (args.Length >= 2)
                        {
                            if (isAuth(player))
                            {
                                switch (args[1].ToLower())
                                {
                                    case "items":
                                        foreach (var entry in rewardData.items)
                                        {
                                            SendEchoConsole(player.net.connection, string.Format("Item ID: {0} - Name: {1} Skin ID: {4} - Amount: {2} - Cost: {3}", entry.Key, entry.Value.displayName, entry.Value.amount, entry.Value.cost, entry.Value.skinId));
                                        }
                                        return;
                                    case "kits":
                                        foreach (var entry in rewardData.kits)
                                        {
                                            SendEchoConsole(player.net.connection, string.Format("Kit ID: {0} - Name: {1} - Cost: {2} - Description: {3}", entry.Key, entry.Value.displayName, entry.Value.cost, entry.Value.description));
                                        }
                                        return;
                                    case "commands":
                                        foreach (var entry in rewardData.commands)
                                        {
                                            SendEchoConsole(player.net.connection, string.Format("Command ID: {0} - Name: {1} - Cost: {2} - Description: {3} - Commands: {4}", entry.Key, entry.Value.displayName, entry.Value.cost, entry.Value.description, entry.Value.commands.ToSentence()));
                                        }
                                        return;
                                    default:
                                        return;
                                }
                            }
                        }
                        return;
                    #endregion
                    #region Additions
                    case "add":
                        if (args.Length >= 2)
                        {
                            if (isAuth(player))
                            {
                                switch (args[1].ToLower())
                                {
                                    case "kit":
                                        if (args.Length == 5)
                                        {
                                            int i = 0;
                                            if (!int.TryParse(args[4], out i))
                                            {
                                                SendMSG(player, msg("noCost", player.UserIDString));
                                                return;
                                            }

                                            object isKit = Kits?.Call("isKit", new object[] { args[3] });
                                            if (isKit is bool && (bool)isKit)
                                            {
                                                if (rewardData.kits.ContainsKey(args[2]))
                                                    SendMSG(player, string.Format(msg("rewardExisting", player.UserIDString), args[2]));
                                                else
                                                {
                                                    rewardData.kits.Add(args[3], new RewardData.RewardKit { displayName = args[2], kitName = args[3], cost = i, description = "" });
                                                    SendMSG(player, string.Format(msg("addSuccess", player.UserIDString), "kit", args[2], i));
                                                    SaveRewards();
                                                }
                                            }
                                            else SendMSG(player, msg("noKit", player.UserIDString), "");
                                        }
                                        else SendMSG(player, "", msg("addSynKit", player.UserIDString));
                                        return;
                                    case "item":
                                        if (args.Length >= 3)
                                        {
                                            int i = 0;
                                            if (!int.TryParse(args[2], out i))
                                            {
                                                SendMSG(player, msg("noCost", player.UserIDString));
                                                return;
                                            }
                                            if (player.GetActiveItem() != null)
                                            {
                                                Item item = player.GetActiveItem();
                                                if (item == null)
                                                {
                                                    SendMSG(player, "", "You must place the item in your hands");
                                                    return;
                                                }
                                                Category cat = (Category)Enum.Parse(typeof(Category), item.info.category.ToString(), true);

                                                RewardData.RewardItem newItem = new RewardData.RewardItem
                                                {
                                                    amount = item.amount,
                                                    cost = i,
                                                    displayName = item.info.displayName.english,
                                                    skinId = item.skin,
                                                    shortname = item.info.shortname,
                                                    category = cat,
                                                    isBp = (args.Length >= 4 && args[3].ToLower() == "bp")
                                                };
                                                string key = $"{item.info.shortname}_{item.skin}";
                                                if (rewardData.items.ContainsKey(key))
                                                    key += $"_{UnityEngine.Random.Range(0, 1000)}";
                                                rewardData.items.Add(key, newItem);
                                                SendMSG(player, string.Format(msg("addSuccess", player.UserIDString), "item", newItem.displayName + $"{(newItem.isBp ? " " + msg("isBp", player.UserIDString) : "")}", i));
                                                SaveRewards();
                                            }
                                            else SendMSG(player, "", msg("itemInHand", player.UserIDString));
                                        }
                                        else SendMSG(player, "", msg("addSynItem2", player.UserIDString));
                                        return;
                                    case "command":
                                        if (args.Length == 5)
                                        {
                                            int i = 0;
                                            if (!int.TryParse(args[4], out i))
                                            {
                                                SendMSG(player, msg("noCost", player.UserIDString));
                                                return;
                                            }
                                            rewardData.commands.Add(args[2], new RewardData.RewardCommand { commands = new List<string> { args[3] }, cost = i, description = "" });
                                            SendMSG(player, string.Format(msg("addSuccess", player.UserIDString), "command", args[2], i));
                                            SaveRewards();
                                        }
                                        else SendMSG(player, "", msg("addSynCommand", player.UserIDString));
                                        return;
                                }
                            }
                        }
                        return;
                    #endregion
                    #region Removal
                    case "remove":
                        if (isAuth(player))
                        {
                            if (args.Length == 3)
                            {
                                switch (args[1].ToLower())
                                {
                                    case "kit":
                                        if (rewardData.kits.ContainsKey(args[2]))
                                        {
                                            rewardData.kits.Remove(args[2]);
                                            SendMSG(player, "", string.Format(msg("remSuccess", player.UserIDString), args[2]));
                                            SaveRewards();
                                        }
                                        else SendMSG(player, msg("noKitRem", player.UserIDString), "");
                                        return;
                                    case "item":
                                        if (rewardData.items.ContainsKey(args[2]))
                                        {
                                            rewardData.items.Remove(args[2]);
                                            SendMSG(player, "", string.Format(msg("remSuccess", player.UserIDString), args[2]));
                                            SaveRewards();
                                        }
                                        else SendMSG(player, msg("noItemRem", player.UserIDString), "");
                                        return;
                                    case "command":
                                        if (rewardData.commands.ContainsKey(args[2]))
                                        {
                                            rewardData.commands.Remove(args[2]);
                                            SendMSG(player, "", string.Format(msg("remSuccess", player.UserIDString), args[2]));
                                            SaveRewards();
                                        }
                                        else SendMSG(player, msg("noCommandRem", player.UserIDString), "");
                                        return;
                                }
                            }
                        }
                        return;
                    #endregion
                    #region Editing
                    case "edit":
                        if (isAuth(player))
                        {
                            if (args.Length >= 3)
                            {
                                switch (args[1].ToLower())
                                {
                                    case "kit":
                                        if (rewardData.kits.ContainsKey(args[2]))
                                        {
                                            if (args.Length >= 5)
                                            {
                                                switch (args[3].ToLower())
                                                {
                                                    case "cost":
                                                        int cost = 0;
                                                        if (int.TryParse(args[4], out cost))
                                                        {
                                                            rewardData.kits[args[2]].cost = cost;
                                                            SaveRewards();
                                                            SendMSG(player, string.Format("Kit {0} cost set to {1}", args[2], cost));
                                                        }
                                                        else SendMSG(player, msg("noCost", player.UserIDString));
                                                        return;
                                                    case "description":
                                                        rewardData.kits[args[2]].description = args[4];
                                                        SaveRewards();
                                                        SendMSG(player, string.Format("Kit {0} description set to {1}", args[2], args[4]));
                                                        return;
                                                    case "name":
                                                        rewardData.kits[args[2]].displayName = args[4];
                                                        SaveRewards();
                                                        SendMSG(player, string.Format("Kit {0} name set to {1}", args[2], args[4]));
                                                        return;
                                                    case "icon":
                                                        rewardData.kits[args[2]].iconName = args[4];
                                                        SaveRewards();
                                                        SendMSG(player, string.Format("Kit {0} icon set to {1}", args[2], args[4]));
                                                        return;
                                                    case "cooldown":
                                                        int cooldown = 0;
                                                        if (int.TryParse(args[4], out cooldown))
                                                        {
                                                            rewardData.kits[args[2]].cooldown = cooldown;
                                                            SaveRewards();
                                                            SendMSG(player, string.Format("Kit {0} cooldown set to {1} seconds", args[2], args[4]));
                                                        }
                                                        else SendMSG(player, "You must enter a cooldown number");
                                                        return;
                                                    default:
                                                        SendMSG(player, msg("editSynKit", player.UserIDString), "");
                                                        return; ;
                                                }
                                            }
                                            else SendMSG(player, msg("editSynKit", player.UserIDString), "");
                                        }
                                        SendMSG(player, msg("noKitRem", player.UserIDString), "");
                                        return;
                                    case "item":
                                        if (rewardData.items.ContainsKey(args[2]))
                                        {
                                            if (args.Length >= 5)
                                            {
                                                switch (args[3].ToLower())
                                                {

                                                    case "amount":
                                                        int amount = 0;
                                                        if (int.TryParse(args[4], out amount))
                                                        {
                                                            rewardData.items[args[2]].amount = amount;
                                                            SaveRewards();
                                                            SendMSG(player, string.Format("Item {0} amount set to {1}", args[2], amount));
                                                        }
                                                        else SendMSG(player, msg("noCost", player.UserIDString));
                                                        return;
                                                    case "cost":
                                                        int cost = 0;
                                                        if (int.TryParse(args[4], out cost))
                                                        {
                                                            rewardData.items[args[2]].cost = cost;
                                                            SaveRewards();
                                                            SendMSG(player, string.Format("Item {0} cost set to {1}", args[2], cost));
                                                        }
                                                        else SendMSG(player, msg("noCost", player.UserIDString));
                                                        return;
                                                    case "name":
                                                        rewardData.items[args[2]].displayName = args[4];
                                                        SaveRewards();
                                                        SendMSG(player, string.Format("Item {0} name set to {1}", args[2], args[4]));
                                                        return;
                                                    case "icon":
                                                        rewardData.items[args[2]].customIcon = args[4];
                                                        SaveRewards();
                                                        SendMSG(player, string.Format("Item {0} icon set to {1}", args[2], args[4]));
                                                        return;
                                                    case "cooldown":
                                                        int cooldown = 0;
                                                        if (int.TryParse(args[4], out cooldown))
                                                        {
                                                            rewardData.items[args[2]].cooldown = cooldown;
                                                            SaveRewards();
                                                            SendMSG(player, string.Format("Item {0} cooldown set to {1} seconds", args[2], args[4]));
                                                        }
                                                        else SendMSG(player, "You must enter a cooldown number");
                                                        return;
                                                    default:
                                                        SendMSG(player, msg("editSynItem2", player.UserIDString), "");
                                                        return;
                                                }
                                            }
                                            else SendMSG(player, msg("editSynKit", player.UserIDString), "");
                                        }
                                        SendMSG(player, msg("noItemRem", player.UserIDString), "");
                                        return;
                                    case "command":
                                        if (rewardData.commands.ContainsKey(args[2]))
                                        {
                                            if (args.Length >= 5)
                                            {
                                                switch (args[3].ToLower())
                                                {
                                                    case "cost":
                                                        int cost = 0;
                                                        if (int.TryParse(args[4], out cost))
                                                        {
                                                            rewardData.commands[args[2]].cost = cost;
                                                            SaveRewards();
                                                            SendMSG(player, string.Format("Command {0} cost set to {1}", args[2], cost));
                                                        }
                                                        else SendMSG(player, msg("noCost", player.UserIDString));
                                                        return;
                                                    case "description":
                                                        rewardData.commands[args[2]].description = args[4];
                                                        SaveRewards();
                                                        SendMSG(player, string.Format("Command {0} description set to {1}", args[2], args[4]));
                                                        return;
                                                    case "name":
                                                        rewardData.commands[args[2]].displayName = args[4];
                                                        SaveRewards();
                                                        SendMSG(player, string.Format("Command {0} name set to {1}", args[2], args[4]));
                                                        return;
                                                    case "icon":
                                                        rewardData.commands[args[2]].iconName = args[4];
                                                        SaveRewards();
                                                        SendMSG(player, string.Format("Command {0} icon set to {1}", args[2], args[4]));
                                                        return;
                                                    case "add":
                                                        if (!rewardData.commands[args[2]].commands.Contains(args[4]))
                                                        {
                                                            rewardData.commands[args[2]].commands.Add(args[4]);
                                                            SaveRewards();
                                                            SendMSG(player, string.Format("Added command \"{1}\" to Reward Command {0}", args[2], args[4]));
                                                        }
                                                        else SendMSG(player, string.Format("The command \"0\" is already registered to this reward command", args[4]));
                                                        return;
                                                    case "remove":
                                                        if (rewardData.commands[args[2]].commands.Contains(args[4]))
                                                        {
                                                            rewardData.commands[args[2]].commands.Remove(args[4]);
                                                            SaveRewards();
                                                            SendMSG(player, string.Format("Removed command \"{1}\" to Command {0}", args[2], args[4]));
                                                        }
                                                        else SendMSG(player, string.Format("The command \"{0}\" is not registered to this reward command", args[4]));
                                                        return;
                                                    case "cooldown":
                                                        int cooldown = 0;
                                                        if (int.TryParse(args[4], out cooldown))
                                                        {
                                                            rewardData.commands[args[2]].cooldown = cooldown;
                                                            SaveRewards();
                                                            SendMSG(player, string.Format("Command {0} cooldown set to {1} seconds", args[2], args[4]));
                                                        }
                                                        else SendMSG(player, "You must enter a cooldown number");
                                                        return;
                                                    default:
                                                        SendMSG(player, msg("editSynCommand", player.UserIDString), "");
                                                        return;
                                                }
                                            }
                                            else SendMSG(player, msg("editSynKit", player.UserIDString), "");
                                        }
                                        SendMSG(player, msg("noCommandRem", player.UserIDString), "");
                                        return;
                                }
                            }
                        }
                        return;
                        #endregion
                }
            }
        }

        [ConsoleCommand("rewards")]
        private void ccmdRewards(ConsoleSystem.Arg conArgs)
        {
            if (conArgs.Connection != null) return;

            var args = conArgs.Args;
            if (args == null || args.Length == 0)
            {
                SendReply(conArgs, $"{Title}  v{Version}");
                SendReply(conArgs, "--- List Rewards ---");
                SendReply(conArgs, "rewards list <items | kits | commands> - Display a list of rewards for the specified category, which information on each item");
                SendReply(conArgs, "--- Add Rewards ---");
                SendReply(conArgs, "rewards add item <shortname> <skinId> <amount> <cost> <opt: cooldown> <opt:bp> - Add a new reward item to the store (add \"bp\" to add the item as a blueprint)");
                SendReply(conArgs, "rewards add kit <name> <kitname> <cost> <opt: cooldown> - Add a new reward kit to the store");
                SendReply(conArgs, "rewards add command <name> <command> <cost> <opt: cooldown> - Add a new reward command to the store");
                SendReply(conArgs, "--- Editing Rewards ---");
                SendReply(conArgs, "rewards edit item <ID> <name | cost | amount | cooldown> \"edit value\" - Edit the specified field of the item with ID number <ID>");
                SendReply(conArgs, "rewards edit kit <ID> <name | cost | description | icon | cooldown> \"edit value\" - Edit the specified field of the kit with ID number <ID>");
                SendReply(conArgs, "rewards edit command <ID> <name | cost | amount | description | icon | add | remove | cooldown> \"edit value\" - Edit the specified field of the kit with ID number <ID>");
                SendReply(conArgs, "Icon field : The icon field can either be a URL, or a image saved to disk under the folder \"oxide/data/ServerRewards/Images/\"");
                SendReply(conArgs, "Command add/remove field: Here you add additional commands or remove existing commands. Be sure to type the command inside quotation marks");
                SendReply(conArgs, "--- Removing Rewards ---");
                SendReply(conArgs, "rewards remove item <ID> - Removes the item with the specified ID");
                SendReply(conArgs, "rewards remove kit <ID> - Removes the kit with the specified ID");
                SendReply(conArgs, "rewards remove command <ID> - Removes the command with the specified ID");
                SendReply(conArgs, "--- Important Note ---");
                SendReply(conArgs, "Any changes you make to the store items/kits/commands will NOT be reflected until you reload the plugin!");
                return;
            }
            if (args.Length >= 1)
            {
                switch (args[0].ToLower())
                {
                    #region Lists
                    case "list":
                        if (args.Length >= 2)
                        {
                            switch (args[1].ToLower())
                            {
                                case "items":
                                    foreach (var entry in rewardData.items)
                                    {
                                        SendReply(conArgs, string.Format("Item ID: {0} || Name: {1} || Skin ID: {4} || Amount: {2} || Cost: {3} || Cooldown : {4}", entry.Key, entry.Value.displayName, entry.Value.amount, entry.Value.cost, entry.Value.skinId, entry.Value.cooldown));
                                    }
                                    return;
                                case "kits":
                                    foreach (var entry in rewardData.kits)
                                    {
                                        SendReply(conArgs, string.Format("Kit ID: {0} || Name: {1} || Cost: {2} || Description: {3} || Cooldown : {4}", entry.Key, entry.Value.displayName, entry.Value.cost, entry.Value.description, entry.Value.cooldown));
                                    }
                                    return;
                                case "commands":
                                    foreach (var entry in rewardData.commands)
                                    {
                                        SendReply(conArgs, string.Format("Command ID: {0} || Name: {1} || Cost: {2} || Description: {3} || Commands: {4} || Cooldown : {5}", entry.Key, entry.Value.displayName, entry.Value.cost, entry.Value.description, entry.Value.commands.ToSentence(), entry.Value.cooldown));
                                    }
                                    return;
                                default:
                                    return;
                            }
                        }
                        return;
                    #endregion
                    #region Additions
                    case "add":
                        if (args.Length >= 2)
                        {
                            switch (args[1].ToLower())
                            {
                                case "item":
                                    if (args.Length >= 6)
                                    {
                                        var shortname = args[2];
                                        ulong skinId;
                                        if (!ulong.TryParse(args[3], out skinId))
                                        {
                                            SendReply(conArgs, "You must enter a number for the skin ID. If you dont wish to select any skin use 0");
                                            return;
                                        }
                                        int amount;
                                        if (!int.TryParse(args[4], out amount))
                                        {
                                            SendReply(conArgs, "You must enter an amount of this item to sell");
                                            return;
                                        }
                                        int cost;
                                        if (!int.TryParse(args[5], out cost))
                                        {
                                            SendReply(conArgs, "You must enter a price for this item");
                                            return;
                                        }
                                        var itemDef = ItemManager.FindItemDefinition(shortname);
                                        if (itemDef != null)
                                        {
                                            Category cat = (Category)Enum.Parse(typeof(Category), itemDef.category.ToString(), true);
                                            int cooldown = 0;
                                            if (args.Length >= 7)
                                                int.TryParse(args[6], out cooldown);
                                            RewardData.RewardItem newItem = new RewardData.RewardItem
                                            {
                                                amount = amount,
                                                cost = cost,
                                                displayName = itemDef.displayName.translated,
                                                skinId = skinId,
                                                shortname = shortname,
                                                category = cat,
                                                cooldown = cooldown,                                                
                                                isBp = (args.Length >= 8 && args[7].ToLower() == "bp")
                                            };
                                            string key = $"{shortname}_{skinId}";
                                            if (rewardData.items.ContainsKey(key))
                                                key += $"_{UnityEngine.Random.Range(0, 1000)}";
                                            rewardData.items.Add(key, newItem);
                                            SendReply(conArgs, string.Format(msg("addSuccess"), "item", newItem.displayName + $"{(newItem.isBp ? " " + msg("isBp") : "")}", cost));
                                            SaveRewards();
                                        }
                                        else SendReply(conArgs, "Invalid item selected!");
                                    }
                                    else SendReply(conArgs, msg("addSynItemCon"));
                                    return;
                                case "kit":
                                    if (args.Length >= 5)
                                    {
                                        int i = 0;
                                        if (!int.TryParse(args[4], out i))
                                        {
                                            SendReply(conArgs, msg("noCost"));
                                            return;
                                        }

                                        int cooldown = 0;
                                        if (args.Length >= 6)
                                            int.TryParse(args[5], out cooldown);

                                        object isKit = Kits?.Call("isKit", new object[] { args[3] });
                                        if (isKit is bool && (bool)isKit)
                                        {
                                            if (rewardData.kits.ContainsKey(args[2]))
                                                SendReply(conArgs, string.Format(msg("rewardExisting"), args[2]));
                                            else
                                            {
                                                rewardData.kits.Add(args[3], new RewardData.RewardKit { displayName = args[2], kitName = args[3], cost = i, description = "", cooldown = cooldown });
                                                SendReply(conArgs, string.Format(msg("addSuccess"), "kit", args[2], i));
                                                SaveRewards();
                                            }
                                        }
                                        else SendReply(conArgs, msg("noKit"), "");
                                    }
                                    else SendReply(conArgs, "", msg("addSynKit"));
                                    return;
                                case "command":
                                    if (args.Length >= 5)
                                    {
                                        int i = 0;
                                        if (!int.TryParse(args[4], out i))
                                        {
                                            SendReply(conArgs, msg("noCost"));
                                            return;
                                        }

                                        int cooldown = 0;
                                        if (args.Length >= 6)
                                            int.TryParse(args[5], out cooldown);

                                        rewardData.commands.Add(args[2], new RewardData.RewardCommand { commands = new List<string> { args[3] }, cost = i, description = "", cooldown = cooldown });
                                        SendReply(conArgs, string.Format(msg("addSuccess"), "command", args[2], i));
                                        SaveRewards();
                                    }
                                    else SendReply(conArgs, "", msg("addSynCommand"));
                                    return;
                            }
                        }

                        return;
                    #endregion
                    #region Removal
                    case "remove":
                        if (args.Length == 3)
                        {
                            switch (args[1].ToLower())
                            {
                                case "kit":
                                    if (rewardData.kits.ContainsKey(args[2]))
                                    {
                                        rewardData.kits.Remove(args[2]);
                                        SendReply(conArgs, "", string.Format(msg("remSuccess"), args[2]));
                                        SaveRewards();
                                    }
                                    else SendReply(conArgs, msg("noKitRem"), "");
                                    return;
                                case "item":
                                    if (rewardData.items.ContainsKey(args[2]))
                                    {
                                        rewardData.items.Remove(args[2]);
                                        SendReply(conArgs, "", string.Format(msg("remSuccess"), args[2]));
                                        SaveRewards();
                                    }
                                    else SendReply(conArgs, msg("noItemRem"), "");
                                    return;
                                case "command":
                                    if (rewardData.commands.ContainsKey(args[2]))
                                    {
                                        rewardData.commands.Remove(args[2]);
                                        SendReply(conArgs, "", string.Format(msg("remSuccess"), args[2]));
                                        SaveRewards();
                                    }
                                    else SendReply(conArgs, msg("noCommandRem"), "");
                                    return;
                            }
                        }
                        return;
                    #endregion
                    #region Editing
                    case "edit":
                        if (args.Length >= 3)
                        {
                            switch (args[1].ToLower())
                            {
                                case "kit":
                                    if (rewardData.kits.ContainsKey(args[2]))
                                    {
                                        if (args.Length >= 5)
                                        {
                                            switch (args[3].ToLower())
                                            {
                                                case "cost":
                                                    int cost = 0;
                                                    if (int.TryParse(args[4], out cost))
                                                    {
                                                        rewardData.kits[args[2]].cost = cost;
                                                        SaveRewards();
                                                        SendReply(conArgs, string.Format("Kit {0} cost set to {1}", args[2], cost));
                                                    }
                                                    else SendReply(conArgs, msg("noCost"));
                                                    return;
                                                case "description":
                                                    rewardData.kits[args[2]].description = args[4];
                                                    SaveRewards();
                                                    SendReply(conArgs, string.Format("Kit {0} description set to {1}", args[2], args[4]));
                                                    return;
                                                case "name":
                                                    rewardData.kits[args[2]].displayName = args[4];
                                                    SaveRewards();
                                                    SendReply(conArgs, string.Format("Kit {0} name set to {1}", args[2], args[4]));
                                                    return;
                                                case "icon":
                                                    rewardData.kits[args[2]].iconName = args[4];
                                                    SaveRewards();
                                                    SendReply(conArgs, string.Format("Kit {0} icon set to {1}", args[2], args[4]));
                                                    return;
                                                case "cooldown":
                                                    int cooldown= 0;
                                                    if (int.TryParse(args[4], out cooldown))
                                                    {
                                                        rewardData.kits[args[2]].cooldown = cooldown;
                                                        SaveRewards();
                                                        SendReply(conArgs, string.Format("Kit {0} cooldown set to {1} seconds", args[2], args[4]));
                                                    }
                                                    else SendReply(conArgs, "You must enter a cooldown number");
                                                    return;
                                                default:
                                                    SendReply(conArgs, msg("editSynKit"), "");
                                                    return; ;
                                            }
                                        }
                                        else SendReply(conArgs, msg("editSynKit"), "");
                                    }
                                    else SendReply(conArgs, msg("noKitRem"), "");
                                    return;
                                case "item":
                                    if (rewardData.items.ContainsKey(args[2]))
                                    {
                                        if (args.Length >= 5)
                                        {
                                            switch (args[3].ToLower())
                                            {

                                                case "amount":
                                                    int amount = 0;
                                                    if (int.TryParse(args[4], out amount))
                                                    {
                                                        rewardData.items[args[2]].amount = amount;
                                                        SaveRewards();
                                                        SendReply(conArgs, string.Format("Item {0} amount set to {1}", args[2], amount));
                                                    }
                                                    else SendReply(conArgs, msg("noCost"));
                                                    return;
                                                case "cost":
                                                    int cost = 0;
                                                    if (int.TryParse(args[4], out cost))
                                                    {
                                                        rewardData.items[args[2]].cost = cost;
                                                        SaveRewards();
                                                        SendReply(conArgs, string.Format("Item {0} cost set to {1}", args[2], cost));
                                                    }
                                                    else SendReply(conArgs, msg("noCost"));
                                                    return;
                                                case "name":
                                                    rewardData.items[args[2]].displayName = args[4];
                                                    SaveRewards();
                                                    SendReply(conArgs, string.Format("Item {0} name set to {1}", args[2], args[4]));
                                                    return;
                                                case "icon":
                                                    rewardData.items[args[2]].customIcon = args[4];
                                                    SaveRewards();
                                                    SendReply(conArgs, string.Format("Item {0} icon set to {1}", args[2], args[4]));
                                                    return;
                                                case "cooldown":
                                                    int cooldown = 0;
                                                    if (int.TryParse(args[4], out cooldown))
                                                    {
                                                        rewardData.items[args[2]].cooldown = cooldown;
                                                        SaveRewards();
                                                        SendReply(conArgs, string.Format("Item {0} cooldown set to {1} seconds", args[2], args[4]));
                                                    }
                                                    else SendReply(conArgs, "You must enter a cooldown number");
                                                    return;
                                                default:
                                                    SendReply(conArgs, msg("editSynItem2"), "");
                                                    return;
                                            }
                                        }
                                        else SendReply(conArgs, msg("editSynKit"), "");
                                    }
                                    else SendReply(conArgs, msg("noItemRem"), "");
                                    return;
                                case "command":
                                    if (rewardData.commands.ContainsKey(args[2]))
                                    {
                                        if (args.Length >= 5)
                                        {
                                            switch (args[3].ToLower())
                                            {
                                                case "cost":
                                                    int cost = 0;
                                                    if (int.TryParse(args[4], out cost))
                                                    {
                                                        rewardData.commands[args[2]].cost = cost;
                                                        SaveRewards();
                                                        SendReply(conArgs, string.Format("Command {0} cost set to {1}", args[2], cost));
                                                    }
                                                    else SendReply(conArgs, msg("noCost"));
                                                    return;
                                                case "description":
                                                    rewardData.commands[args[2]].description = args[4];
                                                    SaveRewards();
                                                    SendReply(conArgs, string.Format("Command {0} description set to {1}", args[2], args[4]));
                                                    return;
                                                case "name":
                                                    rewardData.commands[args[2]].displayName = args[4];
                                                    SaveRewards();
                                                    SendReply(conArgs, string.Format("Command {0} name set to {1}", args[2], args[4]));
                                                    return;
                                                case "icon":
                                                    rewardData.commands[args[2]].iconName = args[4];
                                                    SaveRewards();
                                                    SendReply(conArgs, string.Format("Command {0} icon set to {1}", args[2], args[4]));
                                                    return;
                                                case "add":
                                                    if (!rewardData.commands[args[2]].commands.Contains(args[4]))
                                                    {
                                                        rewardData.commands[args[2]].commands.Add(args[4]);
                                                        SaveRewards();
                                                        SendReply(conArgs, string.Format("Added command \"{1}\" to Reward Command {0}", args[2], args[4]));
                                                    }
                                                    else SendReply(conArgs, string.Format("The command \"0\" is already registered to this reward command", args[4]));
                                                    return;
                                                case "remove":
                                                    if (rewardData.commands[args[2]].commands.Contains(args[4]))
                                                    {
                                                        rewardData.commands[args[2]].commands.Remove(args[4]);
                                                        SaveRewards();
                                                        SendReply(conArgs, string.Format("Removed command \"{1}\" to Command {0}", args[2], args[4]));
                                                    }
                                                    else SendReply(conArgs, string.Format("The command \"{0}\" is not registered to this reward command", args[4]));
                                                    return;
                                                case "cooldown":
                                                    int cooldown = 0;
                                                    if (int.TryParse(args[4], out cooldown))
                                                    {
                                                        rewardData.commands[args[2]].cooldown = cooldown;
                                                        SaveRewards();
                                                        SendReply(conArgs, string.Format("Command {0} cooldown set to {1} seconds", args[2], args[4]));
                                                    }
                                                    else SendReply(conArgs, "You must enter a cooldown number");
                                                    return;
                                                default:
                                                    SendReply(conArgs, msg("editSynCommand"), "");
                                                    return;
                                            }
                                        }
                                        else SendReply(conArgs, msg("editSynKit"), "");
                                    }
                                    else SendReply(conArgs, msg("noCommandRem"), "");
                                    return;
                            }
                        }
                        return;
                        #endregion
                }
            }
        }

        [ChatCommand("sr")]
        private void cmdSR(BasePlayer player, string command, string[] args)
        {
            if (!isAuth(player)) return;
            if (args == null || args.Length == 0)
            {
                SendMSG(player, msg("srAdd2", player.UserIDString), "/sr add <playername> <amount>");
                SendMSG(player, msg("srTake2", player.UserIDString), "/sr take <playername> <amount>");
                SendMSG(player, msg("srClear2", player.UserIDString), "/sr clear <playername>");
                SendMSG(player, msg("srCheck", player.UserIDString), "/sr check <playername>");
                SendMSG(player, msg("srAdd3", player.UserIDString), "/sr add all <amount>");
                SendMSG(player, msg("srTake3", player.UserIDString), "/sr take all <amount>");
                SendMSG(player, msg("srClear3", player.UserIDString), "/sr clear all");
                return;
            }
            if (args.Length >= 2)
            {
                if (args[1].ToLower() == "all")
                {
                    switch (args[0].ToLower())
                    {
                        case "add":
                            if (args.Length == 3)
                            {
                                int i = 0;
                                if (int.TryParse(args[2], out i))
                                {
                                    var pList = playerRP.Keys.ToArray();
                                    foreach (var entry in pList)
                                        AddPoints(entry, i);
                                    SendMSG(player, string.Format(msg("addPointsAll", player.UserIDString), i));
                                }
                            }
                            return;

                        case "take":
                            if (args.Length == 3)
                            {
                                int i = 0;
                                if (int.TryParse(args[2], out i))
                                {
                                    var pList = playerRP.Keys.ToArray();
                                    foreach (var entry in pList)
                                    {
                                        var amount = CheckPoints(entry);
                                        if (amount >= i)
                                            TakePoints(entry, i);
                                        else TakePoints(entry, amount);
                                    }

                                    SendMSG(player, string.Format(msg("remPointsAll", player.UserIDString), i));
                                }
                            }
                            return;
                        case "clear":
                            playerRP.Clear();
                            SendMSG(player, msg("clearAll", player.UserIDString));
                            return;
                    }
                }
                object target = FindPlayer(player, args[1]);
                if (target != null && target is BasePlayer)
                {
                    switch (args[0].ToLower())
                    {
                        case "add":
                            if (args.Length == 3)
                            {
                                int i = 0;
                                int.TryParse(args[2], out i);
                                if (i != 0)
                                    if (AddPoints((target as BasePlayer).userID, i) != null)
                                        SendMSG(player, string.Format(msg("addPoints", player.UserIDString), (target as BasePlayer).displayName, i));
                            }
                            return;

                        case "take":
                            if (args.Length == 3)
                            {
                                int i = 0;
                                int.TryParse(args[2], out i);
                                if (i != 0)
                                    if (TakePoints((target as BasePlayer).userID, i) != null)
                                        SendMSG(player, string.Format(msg("removePoints", player.UserIDString), i, (target as BasePlayer).displayName));
                            }
                            return;
                        case "clear":
                            RemovePlayer((target as BasePlayer).userID);
                            SendMSG(player, string.Format(msg("clearPlayer", player.UserIDString), (target as BasePlayer).displayName));
                            return;
                        case "check":
                            if (args.Length == 2)
                            {
                                var points = CheckPoints((target as BasePlayer).userID);
                                SendMSG(player, string.Format("{0} - {2}: {1}", (target as BasePlayer).displayName, points, msg("storeRP")));
                            }
                            return;
                    }
                }
            }
        }

        [ConsoleCommand("sr")]
        private void ccmdSR(ConsoleSystem.Arg arg)
        {
            if (!isAuthCon(arg)) return;
            if (arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(arg, "sr add <playername> <amount>" + msg("srAdd2"));
                SendReply(arg, "sr take <playername> <amount>" + msg("srTake2"));
                SendReply(arg, "sr clear <playername>" + msg("srClear2"));
                SendReply(arg, "sr check <playername>" + msg("srCheck"));
                SendReply(arg, "sr add all <amount>" + msg("srAdd3"));
                SendReply(arg, "sr take all <amount>" + msg("srTake3"));
                SendReply(arg, "sr clear all" + msg("srClear3"));
                return;
            }
            if (arg.Args.Length >= 2)
            {
                if (arg.Args[1].ToLower() == "all")
                {
                    switch (arg.Args[0].ToLower())
                    {
                        case "add":
                            if (arg.Args.Length == 3)
                            {
                                int i = 0;
                                if (int.TryParse(arg.Args[2], out i))
                                {
                                    var pList = playerRP.Keys.ToArray();
                                    foreach (var entry in pList)
                                        AddPoints(entry, i);
                                    SendReply(arg, string.Format(msg("addPointsAll"), i));
                                }
                            }
                            return;

                        case "take":
                            if (arg.Args.Length == 3)
                            {
                                int i = 0;
                                if (int.TryParse(arg.Args[2], out i))
                                {
                                    var pList = playerRP.Keys.ToArray();
                                    foreach (var entry in pList)
                                    {
                                        var amount = CheckPoints(entry);
                                        if (amount >= i)
                                            TakePoints(entry, i);
                                        else TakePoints(entry, amount);
                                    }

                                    SendReply(arg, string.Format(msg("remPointsAll"), i));
                                }
                            }
                            return;
                        case "clear":
                            playerRP.Clear();
                            SendReply(arg, msg("clearAll"));
                            return;
                    }
                }
                object target = FindPlayer(null, arg.Args[1]);
                if (target is string)
                {
                    SendReply(arg, (string)target);
                    return;
                }
                if (target != null && target is BasePlayer)
                {
                    switch (arg.Args[0].ToLower())
                    {
                        case "add":
                            if (arg.Args.Length == 3)
                            {
                                int i = 0;
                                int.TryParse(arg.Args[2], out i);
                                if (i != 0)
                                    if (AddPoints((target as BasePlayer).userID, i) != null)
                                        SendReply(arg, string.Format(msg("addPoints"), (target as BasePlayer).displayName, i));
                            }
                            return;
                        case "take":
                            if (arg.Args.Length == 3)
                            {
                                int i = 0;
                                int.TryParse(arg.Args[2], out i);
                                if (i != 0)
                                    if (TakePoints((target as BasePlayer).userID, i) != null)
                                        SendReply(arg, string.Format(msg("removePoints"), i, (target as BasePlayer).displayName));
                            }
                            return;
                        case "clear":
                            RemovePlayer((target as BasePlayer).userID);
                            SendReply(arg, string.Format(msg("clearPlayer"), (target as BasePlayer).displayName));
                            return;
                        case "check":
                            if (arg.Args.Length == 2)
                            {
                                var points = CheckPoints((target as BasePlayer).userID);
                                SendReply(arg, string.Format("{0} - {2}: {1}", (target as BasePlayer).displayName, points, msg("storeRP")));
                            }
                            return;
                    }
                }
            }
        }

        bool isAuth(BasePlayer player)
        {
            if (player.net.connection != null)
                if (player.net.connection.authLevel < 1)
                    return false;
            return true;
        }
        bool isAuthCon(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                if (arg.Connection.authLevel < 1)
                {
                    SendReply(arg, "You dont not have permission to use this command.");
                    return false;
                }
            }
            return true;
        }
        #endregion

        #region Kit Contents
        private string GetKitContents(string kitname)
        {
            var contents = Kits?.Call("GetKitInfo", kitname);
            if (contents != null && contents is JObject)
            {
                List<string> contentList = new List<string>();
                JObject kitContents = contents as JObject;

                JArray items = kitContents["items"] as JArray;
                foreach (var itemEntry in items)
                {
                    JObject item = itemEntry as JObject;
                    string itemString = (int)item["amount"] > 1 ? $"{(int)item["amount"]}x " : "";
                    itemString += itemNames[itemIds[(int)item["itemid"]]];

                    List<string> mods = new List<string>();
                    foreach (var mod in item["mods"] as JArray)
                        mods.Add(itemNames[itemIds[(int)mod]]);

                    if (mods.Count > 0)
                        itemString += $" ({mods.ToSentence()})";

                    contentList.Add(itemString);
                }
                return contentList.ToSentence();
            }
            return null;
        }
        #endregion

        #region Config
        private ConfigData configData;

        private class Colors
        {
            [JsonProperty(PropertyName = "Primary text color")]
            public string TextColor_Primary { get; set; }
            [JsonProperty(PropertyName = "Secondary text color")]
            public string TextColor_Secondary { get; set; }
            [JsonProperty(PropertyName = "Background color")]
            public UIColor Background_Dark { get; set; }
            [JsonProperty(PropertyName = "Secondary panel color")]
            public UIColor Background_Medium { get; set; }
            [JsonProperty(PropertyName = "Primary panel color")]
            public UIColor Background_Light { get; set; }
            [JsonProperty(PropertyName = "Button color - standard")]
            public UIColor Button_Standard { get; set; }
            [JsonProperty(PropertyName = "Button color - accept")]
            public UIColor Button_Accept { get; set; }
            [JsonProperty(PropertyName = "Button color - inactive")]
            public UIColor Button_Inactive { get; set; }
        }

        private class UIColor
        {
            [JsonProperty(PropertyName = "Hex color")]
            public string Color { get; set; }
            [JsonProperty(PropertyName = "Transparency (0 - 1)")]
            public float Alpha { get; set; }
        }

        private class Tabs
        {
            [JsonProperty(PropertyName = "Show kits tab")]
            public bool Kits { get; set; }
            [JsonProperty(PropertyName = "Show commands tab")]
            public bool Commands { get; set; }
            [JsonProperty(PropertyName = "Show items tab")]
            public bool Items { get; set; }
            [JsonProperty(PropertyName = "Show exchange tab")]
            public bool Exchange { get; set; }
            [JsonProperty(PropertyName = "Show transfer tab")]
            public bool Transfer { get; set; }
            [JsonProperty(PropertyName = "Show seller tab")]
            public bool Seller { get; set; }
        }

        private class Exchange
        {
            [JsonProperty(PropertyName = "Value of RP")]
            public int RP { get; set; }
            [JsonProperty(PropertyName = "Value of Economics")]
            public int Economics { get; set; }
        }

        private class Options
        {
            [JsonProperty(PropertyName = "Log all transactions")]
            public bool Logs { get; set; }
            [JsonProperty(PropertyName = "Data save interval")]
            public int SaveInterval { get; set; }
            [JsonProperty(PropertyName = "Use NPC dealers only")]
            public bool NPCOnly { get; set; }
        }

        private class UIOptions
        {
            [JsonProperty(PropertyName = "Disable fade in effect")]
            public bool FadeIn { get; set; }
            [JsonProperty(PropertyName = "Display kit contents as the description")]
            public bool KitContents { get; set; }
            [JsonProperty(PropertyName = "Display user playtime")]
            public bool ShowPlaytime { get; set; }
        }

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Coloring")]
            public Colors Colors { get; set; }
            [JsonProperty(PropertyName = "Active categories (global)")]
            public Tabs Tabs { get; set; }
            [JsonProperty(PropertyName = "Currency exchange rates")]
            public Exchange Exchange { get; set; }
            [JsonProperty(PropertyName = "Options")]
            public Options Options { get; set; }
            [JsonProperty(PropertyName = "UI Options")]
            public UIOptions UIOptions { get; set; }
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
            color1 = $"<color={configData.Colors.TextColor_Primary}>";
            color2 = $"<color={configData.Colors.TextColor_Secondary}>";
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                Colors = new Colors
                {
                    Background_Dark = new UIColor { Color = "#2a2a2a", Alpha = 0.98f },
                    Background_Medium = new UIColor { Color = "#373737", Alpha = 0.98f },
                    Background_Light = new UIColor { Color = "#696969", Alpha = 0.4f },
                    Button_Accept = new UIColor { Color = "#00cd00", Alpha = 0.9f },
                    Button_Inactive = new UIColor { Color = "#a8a8a8", Alpha = 0.9f },
                    Button_Standard = new UIColor { Color = "#2a2a2a", Alpha = 0.9f },
                    TextColor_Primary = "#ce422b",
                    TextColor_Secondary = "#939393"
                },
                Exchange = new Exchange
                {
                    Economics = 100,
                    RP = 1
                },
                Options = new Options
                {
                    Logs = true,
                    NPCOnly = false,
                    SaveInterval = 600
                },
                Tabs = new Tabs
                {
                    Commands = true,
                    Exchange = true,
                    Items = true,
                    Kits = true,
                    Seller = true,
                    Transfer = true
                },
                UIOptions = new UIOptions
                {
                    FadeIn = true,
                    KitContents = true,
                    ShowPlaytime = true
                }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();

        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Data Management
        private void SaveRP()
        {
            playerData.playerRP = playerRP;
            playerdata.WriteObject(playerData);
            cooldowndata.WriteObject(cooldownData);
        }

        private void SaveRewards() => rewarddata.WriteObject(rewardData);

        private void SaveNPC() => npcdata.WriteObject(npcData);

        private void SaveSales() => saledata.WriteObject(saleData);

        private void LoadData()
        {
            try
            {
                playerData = playerdata.ReadObject<PlayerData>();
                playerRP = playerData.playerRP;
            }
            catch
            {
                PrintWarning("No player data found! Creating a new data file");
                playerData = new PlayerData();
            }
            try
            {
                rewardData = rewarddata.ReadObject<RewardData>();
            }
            catch
            {
                PrintWarning("No reward data found! Creating a new data file");
                rewardData = new RewardData();
            }
            try
            {
                npcData = npcdata.ReadObject<NPCData>();
            }
            catch
            {
                PrintWarning("No npc data found! Creating a new data file");
                npcData = new NPCData();
            }
            try
            {
                saleData = saledata.ReadObject<SaleData>();
            }
            catch
            {
                PrintWarning("No sale data found! Creating a new data file");
                saleData = new SaleData();
            }
            try
            {
                cooldownData = cooldowndata.ReadObject<CooldownData>();
            }
            catch
            {
                PrintWarning("No cooldown data found! Creating a new data file");
                cooldownData = new CooldownData();
            }
        }

        private class PlayerData
        {
            public Dictionary<ulong, int> playerRP = new Dictionary<ulong, int>();
        }

        private class CooldownData
        {
            public Dictionary<ulong, CooldownUser> users = new Dictionary<ulong, CooldownUser>();

            public void AddCooldown(ulong playerId, PurchaseType type, string key, int time)
            {
                CooldownUser userData;
                if (!users.TryGetValue(playerId, out userData))
                {
                    userData = new CooldownUser();
                    users.Add(playerId, userData);
                }

                userData.AddCooldown(type, key, time);
            }

            public bool HasCooldown(ulong playerId, PurchaseType type, string key, out double remaining)
            {
                remaining = 0;
                CooldownUser userData;
                if (!users.TryGetValue(playerId, out userData))
                    return false;

                return userData.HasCooldown(type, key, out remaining);
            }

            public class CooldownUser
            {
                Dictionary<PurchaseType, Dictionary<string, double>> items = new Dictionary<PurchaseType, Dictionary<string, double>>
                {
                    [PurchaseType.Command] = new Dictionary<string, double>(),
                    [PurchaseType.Item] = new Dictionary<string, double>(),
                    [PurchaseType.Kit] = new Dictionary<string, double>()
                };

                public void AddCooldown(PurchaseType type, string key, int time)
                {
                    if (!items[type].ContainsKey(key))                    
                        items[type].Add(key, time + GrabCurrentTime());                    
                    else items[type][key] = time + GrabCurrentTime();
                }

                public bool HasCooldown(PurchaseType type, string key, out double remaining)
                {
                    remaining = 0;
                    double time;
                    if (items[type].TryGetValue(key, out time))
                    {
                        double currentTime = GrabCurrentTime();
                        if (time > currentTime)
                        {
                            remaining = time - currentTime;
                            return true;
                        }
                    }
                    return false;
                }
            }
        }

        private class NPCData
        {
            public Dictionary<string, NPCInfo> npcInfo = new Dictionary<string, NPCInfo>();

            public class NPCInfo
            {
                public string name;
                public float x, z;
                public bool useCustom, sellItems, sellKits, sellCommands, canTransfer, canSell, canExchange;
                public List<string> items = new List<string>();
                public List<string> kits = new List<string>();
                public List<string> commands = new List<string>();
            }
        }

        private class RewardData
        {
            public Dictionary<string, RewardItem> items = new Dictionary<string, RewardItem>();
            public SortedDictionary<string, RewardKit> kits = new SortedDictionary<string, RewardKit>();
            public SortedDictionary<string, RewardCommand> commands = new SortedDictionary<string, RewardCommand>();

            public class RewardItem : Reward
            {
                public string shortname, customIcon;
                public int amount;
                public ulong skinId;
                public bool isBp;
                public Category category;
            }

            public class RewardKit : Reward
            {
                public string kitName, description, iconName;
            }

            public class RewardCommand : Reward
            {
                public string description, iconName;
                public List<string> commands = new List<string>();               
            }

            public class Reward
            {
                public string displayName;
                public int cost;
                public int cooldown;
            }
        }

        private class SaleData
        {
            public Dictionary<string, Dictionary<ulong, SaleItem>> items = new Dictionary<string, Dictionary<ulong, SaleItem>>();

            public class SaleItem
            {
                public float price = 0;
                public string displayName;
                public bool enabled = false;
            }
        }
        #endregion

        #region Sales Updater
        private void UpdatePriceList()
        {
            bool changed = false;
            foreach (var item in ItemManager.itemList)
            {
                if (!saleData.items.ContainsKey(item.shortname))
                {
                    saleData.items.Add(item.shortname, new Dictionary<ulong, SaleData.SaleItem> { { 0, new SaleData.SaleItem { displayName = item.displayName.translated } } });
                    changed = true;
                }
                if (HasSkins(item))
                {
                    foreach (var skin in ItemSkinDirectory.ForItem(item))
                    {
                        ulong skinId = Convert.ToUInt64(skin.id);
                        if (!saleData.items[item.shortname].ContainsKey(skinId))
                        {
                            saleData.items[item.shortname].Add(skinId, new SaleData.SaleItem { displayName = skin.invItem.displayName.translated });
                            changed = true;
                        }
                    }
                    foreach (var skin in Rust.Workshop.Approved.All.Where(x => x.Name == item.shortname))
                    {
                        if (!saleData.items[item.shortname].ContainsKey(skin.WorkshopdId))
                        {
                            saleData.items[item.shortname].Add(skin.WorkshopdId, new SaleData.SaleItem() { displayName = skin.Name });
                            changed = true;
                        }
                    }
                }
            }
            if (changed)
                SaveSales();
        }

        private bool HasSkins(ItemDefinition item)
        {
            if (item != null)
            {
                var skins = ItemSkinDirectory.ForItem(item).ToList();
                if (skins.Count > 0)
                    return true;
                else if (Rust.Workshop.Approved.All.Where(x => x.Name == item.shortname).Count() > 0)
                    return true;

            }
            return false;
        }
        #endregion

        #region Localization
        string msg(string key, string playerId = null) => lang.GetMessage(key, this, playerId);
        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"title", "ServerRewards: " },
            { "msgOutRewards1", "You currently have {0} unspent reward tokens! Spend them in the reward store using /s" },
            { "msgOutRewardsnpc", "You currently have {0} unspent reward tokens! Spend them in the reward store by finding a NPC reward dealer" },
            {"msgNoPoints", "You dont have enough reward points" },
            {"errorProfile", "Error getting your profile from the database"},
            {"errorPCon", "There was a error pulling {0}'s profile from the database" },
            {"errorItemPlayer", "There was an error whilst retrieving your reward, please contact an administrator" },
            {"noFind", "Unable to find {0}" },
            {"rInviter", "You have recieved {0} reward points for inviting {1}" },
            {"rInvitee", "You have recieved {0} reward points" },
            {"refSyn", "/refer <playername>" },
            {"remSynKit", "/rewards remove kit <name>" },
            {"remSynItem", "/rewards remove item <number>" },
            {"remSynCommand", "/rewards remove command <name>" },
            {"noKit", "Kit's could not confirm that the kit exists. Check Kit's and your kit data" },
            {"noKitRem", "Unable to find a reward kit with that name" },
            {"noItemRem", "Unable to find a reward item with that number" },
            {"noCommandRem", "Unable to find a reward command with that name" },
            {"remSuccess", "You have successfully removed {0} from the rewards list" },
            {"addSynKit", "/rewards add kit <Name> <kitname> <cost>" },
            {"addSynItem2", "/rewards add item <cost> <opt:bp>" },
            {"addSynItemCon", "rewards add item <shortname> <skinId> <amount> <cost> <opt:bp>" },
            {"addSynCommand", "/rewards add command <Name> <command> <cost>" },
            {"editSynItem2", "/rewards edit item <ID> <cost|amount|name|cooldown> \"info here\"" },
            {"editSynItem1", "- Edit a reward item information" },
            {"editSynKit", "/rewards edit kit <ID> <cost|description|name|icon|cooldown> \"info here\"" },
            {"editSynKit1", "- Edit a reward kit information" },
            {"editSynCommand", "/rewards edit command <ID> <cost|description|name|icon|add|remove|cooldown> \"info here\"" },
            {"editSynCommand1", "- Edit a reward command information" },
            {"storeSyn21", "/s" },
            {"storeSyn2", " - Opens the reward store" },
            {"addSuccess", "You have added the {0} {1}, available for {2} RP" },
            {"rewardExisting", "You already have a reward kit named {0}" },
            {"noCost", "You must enter a reward cost" },
            {"reward", "Reward: " },
            {"desc1", ", Description: " },
            {"cost", ", Cost: " },
            {"claimSyn", "/claim <rewardname>" },
            {"noReward", "This reward doesnt exist!" },
            {"claimSuccess", "You have claimed {0}" },
            {"multiPlayers", "Multiple players found with that name" },
            {"noPlayers", "No players found" },
            {"tpointsAvail", "You have {0} reward point(s) to spend" },
            {"rewardAvail", "Available Rewards;" },
            {"chatClaim", " - Claim the reward"},
            {"chatCheck", "/rewards check" },
            {"chatCheck1", " - Displays you current time played and current reward points"},
            {"chatListOpt", "/rewards list <items|commands|kits>"},
            {"chatListOpt1", " - Display rewards with their ID numbers and info in F1 console"},
            {"chatAddKit", " - Add a new reward kit"},
            {"chatAddItem2", " - Add a new reward item (add \"bp\" to the end of the command to add a blueprint)"},
            {"chatAddCommand", " - Add a new reward command"},
            {"chatRemove", " - Removes a reward"},
            {"chatRefer", " - Acknowledge your referral from <playername>"},
            {"alreadyRefer1", "You have already been referred" },
            {"addPoints", "You have given {0} {1} points" },
            {"removePoints", "You have taken {0} points from {1}"},
            {"clearPlayer", "You have removed {0}'s reward profile" },
            {"addPointsAll", "You have given everyone {0} points" },
            {"remPointsAll", "You have taken {0} points from everyone"},
            {"clearAll", "You have removed all reward profiles" },
            {"srAdd2", " - Adds <amount> of reward points to <playername>" },
            {"srAdd3", " - Adds <amount> of reward points to all players" },
            {"srTake2", " - Takes <amount> of reward points from <playername>" },
            {"srTake3", " - Takes <amount> of reward points from all players" },
            {"srClear2", " - Clears <playername>'s reward profile" },
            {"srClear3", " - Clears all reward profiles" },
            {"srCheck", " - Check a players point count" },
            {"notSelf", "You cannot refer yourself. But nice try!" },
            {"noCommands", "There are currently no commands set up" },
            {"noTypeItems", "This store currently has no {0} items available" },
            {"noKits", "There are currently no kits set up" },
            {"exchange1", "Here you can exchange economics money (Coins) for reward points (RP) and vice-versa" },
            {"exchange2", "The current exchange rate is " },
            {"buyKit", "You have purchased a {0} kit" },
            {"notEnoughPoints", "You don't have enough points" },
            {"errorKit", "There was a error purchasing this kit. Contact a administrator" },
            {"buyCommand", "You have purchased the {0} command" },
            {"errorCommand", "There was a error purchasing this command. Contact a administrator" },
            {"buyItem", "You have purchased {0}x {1}" },
            {"errorItem", "There was a error purchasing this item. Contact a administrator" },
            {"notEnoughCoins", "You do not have enough coins to exchange" },
            {"exchange", "You have exchanged " },
            {"itemInHand", "You must place the item you wish to add in your hands" },
            {"itemIDHelp", "You must enter the items number. Type /rewards list to see available entries" },
            {"noProfile", "{0} does not have any saved data" },
            {"storeTitle", "Reward Store" },
            {"storeKits", "Kits" },
            {"storeCommands", "Commands" },
            {"storeItems", "Items" },
            {"storeExchange", "Exchange" },
            {"storeTransfer", "Transfer" },
            {"storeSales", "Sales" },
            {"storeClose", "Close" },
            {"storeNext", "Next" },
            {"storeBack", "Back" },
            {"storePlaytime", "Playtime" },
            {"storeCost", "Cost" },
            {"storeRP", "RP" },
            {"storeEcon", "Economics" },
            {"storeCoins", "Coins" },
            {"npcExist", "This NPC is already a Reward Dealer" },
            {"npcNew", "You have successfully added a new Reward Dealer" },
            {"npcRem", "You have successfully removed a Reward Dealer" },
            {"npcNotAdded", "This NPC is not a Reward Dealer" },
            {"noNPC", "Could not find a NPC to register" },
            {"Reward Dealer", "Reward Dealer" },
            {"fullInv", "Your inventory is full" },
            {"transfer1", "Select a user to transfer money to" },
            {"transfer2", "Select a amount to send" },
            {"transfer3", "You have transferred {0} {1} to {2}" },
            {"clootsucc", "You have successfully created a new loot list for this NPC" },
            {"save", "Save"},
            {"cldesc", "Select items, kits and commands to add to this NPC's custom store list" },
            {"clcanc", "You have cancelled custom loot creation"},
            {"sellItems", "Sell Items" },
            {"selectItemSell", "Select an item to sell. You can only sell items that are in your main inventory container" },
            {"Name", "Name" },
            {"Amount", "Amount" },
            {"Sell","Sell" },
            {"CantSell","Not Sell-able" },
            {"selectToSell", "Select an amount of the item you wish to sell" },
            {"sellItemF","Item: {0}{1}</color>" },
            {"sellPriceF","Price per unit: {0}{1} {2}</color>" },
            {"sellUnitF","Units to sell: {0}{1}</color>" },
            {"sellTotalF","Total sale price: {0}{1} {2}</color>" },
            {"cancelSale","Cancel Sale" },
            {"confirmSale","Sell Item" },
            {"saleSuccess", "You have sold {0}x {1} for {2} {3}" },
            {"allowExchange", "Currency Exchange" },
            {"allowTransfer", "Currency Transfer" },
            {"allowSales", "Item Sales" },
            {"Weapon", "Weapons" },
            {"Construction", "Construction" },
            {"Items", "Items" },
            {"Resources", "Resources" },
            {"Attire", "Attire" },
            {"Tool", "Tools" },
            {"Medical", "Medical" },
            {"Food", "Food" },
            {"Ammunition", "Ammunition" },
            {"Traps", "Traps" },
            {"Misc", "Misc" },
            {"Component", "Components" },
            {"imWait", "You must wait until ImageLibrary has finished processing its images" },
            {"useCustom", "Use Custom Loot" },
            {"isBp", "(BP)" },
            {"hasCooldownCommand", "You have {0} remaining on the cooldown for this command" },
            {"hasCooldownKit", "You have {0} remaining on the cooldown for this kit" },
            {"hasCooldownItem", "You have {0} remaining on the cooldown for this item" }
        };
        #endregion
    }
}
