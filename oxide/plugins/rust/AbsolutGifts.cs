using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("AbsolutGifts", "Absolut", "1.5.1", ResourceId = 2159)]

    class AbsolutGifts : RustPlugin
    {
        [PluginReference]
        Plugin ImageLibrary, ServerRewards, Economics, AbsolutCombat;

        GiftData agdata;
        private DynamicConfigFile AGData;

        bool initialized;
        int GlobalTime = 0;

        string TitleColor = "<color=orange>";
        string MsgColor = "<color=#A9A9A9>";

        int max;

        private Dictionary<int, float[]> GiftPositions = new Dictionary<int, float[]>();
        private Dictionary<string, Timer> timers = new Dictionary<string, Timer>();
        private Dictionary<ulong, GiftCreation> giftprep = new Dictionary<ulong, GiftCreation>();
        private Dictionary<ulong, Vector3> AFK = new Dictionary<ulong, Vector3>();
        private Dictionary<ulong, Info> UIinfo = new Dictionary<ulong, Info>();
        class Info
        {
            public bool open;
            public int page;
            public bool admin;
            public ItemCategory cat = ItemCategory.All;
        }


        #region Server Hooks

        void Loaded()
        {
            AGData = Interface.Oxide.DataFileSystem.GetFile("AbsolutGifts_Data");
            lang.RegisterMessages(messages, this);
        }

        void Unload()
        {
            foreach (var entry in timers)
                entry.Value.Destroy();
            timers.Clear();
            foreach (BasePlayer p in BasePlayer.activePlayerList)
            {
                DestroyPlayer(p);
            }
            if (initialized)
                SaveData();
        }

        void OnServerInitialized()
        {
            initialized = false;
            try
            {
                ImageLibrary.Call("isLoaded", null);
            }
            catch (Exception)
            {
                PrintWarning("No Image Library.. Images will not work without ImageLibrary", Name);
                //Interface.Oxide.UnloadPlugin(Name);
                //return;
            }
            LoadVariables();
            LoadData();
            LoadImages();
            permission.RegisterPermission(this.Name + ".vip", this);
            permission.RegisterPermission(this.Name + ".admin", this);
            timers.Add("info", timer.Once(900, () => InfoLoop()));
            timers.Add("save", timer.Once(600, () => SaveLoop()));
            SaveData();
            SetGiftPositions();
            timer.Once(5, () =>
            {
                foreach (BasePlayer p in BasePlayer.activePlayerList)
                    OnPlayerInit(p);
            });
            timers.Add("time", timer.Once(60, () => ChangeGlobalTime()));
        
        }

        void LoadImages()
        {
            if (!ImageLibrary) return;
            if (timers.ContainsKey("imageloading"))
            {
                timers["imageloading"].Destroy();
                timers.Remove("imageloading");
            }
            if (!isReady())
            { Puts(GetMSG("WaitingImageLibrary")); timers.Add("imageloading", timer.Once(60, () => LoadImages())); return; };
            if (string.IsNullOrEmpty(configData.GiftIconImage))
                AddImage("http://i.imgur.com/zMe9ky5.png", "newgift", (ulong)ResourceId);
            else AddImage(configData.GiftIconImage, "newgift", (ulong)ResourceId);
            CreateLoadOrder();
            LoadAllItemImages();
            if (timers.ContainsKey("imageloading"))
            {
                timers["imageloading"].Destroy();
                timers.Remove("imageloading");
            }
        }

        private void LoadAllItemImages()
        {
            ImageLibrary.Call("LoadImageList", Title, ItemManager.itemList.Select(x => new KeyValuePair<string, ulong>(x.shortname, 0)).ToList());
        }
        private void CreateLoadOrder()
        {
            Dictionary<string, string> newLoadOrder = new Dictionary<string, string>
            {
            {      "sr", "http://oxidemod.org/data/resource_icons/1/1751.jpg?1456924271" },
            {      "eco", "http://oxidemod.org/data/resource_icons/0/717.jpg?1465675504" },
            {      "ac", "http://oxidemod.org/data/resource_icons/2/2103.jpg?1472590458" },
            };
            ImageLibrary.Call("ImportImageList", Title, newLoadOrder, (ulong)ResourceId, true);
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            DestroyPlayer(player);
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (player != null)
            {
                GetSendMSG(player, "AGInfo");
                InitializePlayer(player);
            }
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            NewGiftIcon(player);
        }

        private void InitializePlayer(BasePlayer player)
        {
            if (!UIinfo.ContainsKey(player.userID))
                UIinfo.Add(player.userID, new Info { page = 0 });
            if (!agdata.Players.ContainsKey(player.userID))
            {
                agdata.Players.Add(player.userID, new playerdata { PlayerTime = 0, ReceivedGifts = new List<int>(), ResetTime = GrabCurrentTime() + (configData.ResetInDays * 86400) });
            }
            else
            {
                //Puts($"CurrentTime: {GrabCurrentTime()}");
                if (GrabCurrentTime() >= agdata.Players[player.userID].ResetTime)
                {
                    //Puts("Time is Greater");
                    agdata.Players[player.userID].ReceivedGifts.Clear();
                    agdata.Players[player.userID].pendingGift.Clear();
                    agdata.Players[player.userID].PlayerTime = 0;
                    agdata.Players[player.userID].ResetTime = GrabCurrentTime() + (configData.ResetInDays * 86400);
                    if (UIinfo[player.userID].open)
                        GiftPanel(player);
                    SaveData();
                }
            }
            double timeremaining = agdata.Players[player.userID].ResetTime - GrabCurrentTime();
            if (timers.ContainsKey(player.UserIDString))
            {
                timers[player.UserIDString].Destroy();
                timers.Remove(player.UserIDString);
            }
            timers.Add(player.UserIDString, timer.Once((float)timeremaining, () => InitializePlayer(player)));
            NewGiftIcon(player);
        }

        private void DestroyPlayer(BasePlayer player)
        {
            if (!agdata.Players.ContainsKey(player.userID))
                agdata.Players.Add(player.userID, new playerdata { PlayerTime = 0, ReceivedGifts = new List<int>() });
            if (timers.ContainsKey(player.UserIDString))
            {
                timers[player.UserIDString].Destroy();
                timers.Remove(player.UserIDString);
            }
            DestroyGiftPanel(player, true);
            CuiHelper.DestroyUi(player, PanelIcon);
            CuiHelper.DestroyUi(player, PanelOnScreen);
        }

        static double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

        #endregion

        #region Player Hooks

        #endregion

        #region Functions
        private string TryForImage(string shortname, ulong skin = 99)
        {
            if (!ImageLibrary) return "https://i.imgur.com/yxESUQJ.png";
            if (shortname.Contains("http") || shortname.Contains("www")) return shortname;
            if (skin == 99) skin = (ulong)ResourceId;
            return GetImage(shortname, skin, true);
        }

        public string GetImage(string shortname, ulong skin = 0, bool returnUrl = false) => (string)ImageLibrary.Call("GetImage", shortname.ToLower(), skin, returnUrl);
        public bool HasImage(string shortname, ulong skin = 0) => (bool)ImageLibrary.Call("HasImage", shortname.ToLower(), skin);
        public bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname.ToLower(), skin);
        public List<ulong> GetImageList(string shortname) => (List<ulong>)ImageLibrary.Call("GetImageList", shortname.ToLower());
        public bool isReady() => (bool)ImageLibrary?.Call("IsReady");


        private void CancelGiftCreation(BasePlayer player)
        {
            DestroyGiftPanel(player, false);
            if (giftprep.ContainsKey(player.userID))
                giftprep.Remove(player.userID);
            GetSendMSG(player, "GiftCreationCanceled");
            UIinfo[player.userID].page = 0;
            Background(player);
        }

        public void DestroyGiftPanel(BasePlayer player, bool background)
        {
            if (UIinfo.ContainsKey(player.userID))
            {
                UIinfo[player.userID].open = false;
                UIinfo[player.userID].page = 0;
            }
            if (background) CuiHelper.DestroyUi(player, PanelStatic);
            CuiHelper.DestroyUi(player, PanelGift);
            CuiHelper.DestroyUi(player, PanelTime);
            for (int i = 0; i < max; i++)
                CuiHelper.DestroyUi(player, "GiftEntry" + i);
        }

        void ChangeGlobalTime()
        {
            if (timers.ContainsKey("time"))
            {
                timers["time"].Destroy();
                timers.Remove("time");
            }
            GlobalTime++;
            timers.Add("time", timer.Once(60, () => ChangeGlobalTime()));       
            CheckPlayers();
        }

        private void CheckPlayers()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                if (agdata.Players.ContainsKey(player.userID))
                {
                    if (configData.NoAFK)
                        if (CheckAFK(player)) continue;
                    agdata.Players[player.userID].PlayerTime++;
                    int giftIndex = 0;
                    if (permission.UserHasPermission(player.UserIDString, this.Title + ".vip")) giftIndex = agdata.Gifts.Where(k => !agdata.Players[player.userID].ReceivedGifts.Contains(k.Key)).OrderBy(k => k.Key).Select(k => k.Key).FirstOrDefault();
                    else giftIndex = agdata.Gifts.Where(k => !k.Value.vip && !agdata.Players[player.userID].ReceivedGifts.Contains(k.Key)).OrderBy(k => k.Key).Select(k => k.Key).FirstOrDefault();
                    if (giftIndex != 0 && giftIndex <= agdata.Players[player.userID].PlayerTime)
                    {
                        OnScreen(player, "CompletedNewGift", giftIndex.ToString());
                        ProcessGift(player, giftIndex);
                    }
                    if (UIinfo[player.userID].open)
                        PlayerTimeCounter(player);
                }
                else InitializePlayer(player);
        }

        bool CheckAFK(BasePlayer player)
        {
            if (player == null) return false;
            if (!AFK.ContainsKey(player.userID))
            {
                AFK.Add(player.userID, player.transform.position);
                //Puts("Not in Dictionary");
                return false;
            }
            else if (AFK[player.userID] == player.transform.position)
            {
                //Puts("Same Position - AFK");
                return true;
            }
            else if (AFK[player.userID] != player.transform.position)
            {
                AFK[player.userID] = player.transform.position;
                //Puts("New Position - Not AFK");
            }
            return false;
        }

        private void ProcessGift(BasePlayer player, int GiftIndex)
        {
            if (!agdata.Gifts.ContainsKey(GiftIndex)) return;
            var Gift = agdata.Gifts[GiftIndex];
            if (Gift.gifts.Where(k => !k.AC && !k.SR && !k.Eco).Count() == 0)
                foreach (var entry in Gift.gifts)
                {
                    if (entry.SR)
                    {
                        ServerRewards?.Call("AddPoints", player.userID.ToString(), entry.amount);
                        GetSendMSG(player, "NewGiftGiven", entry.amount.ToString(), "ServerRewards Points");
                    }
                    else if (entry.Eco)
                    {
                        Economics.Call("DepositS", player.userID.ToString(), entry.amount);
                        GetSendMSG(player, "NewGiftGiven", entry.amount.ToString(), "Economics");
                    }
                    else if (entry.AC)
                    {
                        AbsolutCombat.Call("AddMoney", player.userID.ToString(), entry.amount, false);
                        GetSendMSG(player, "NewGiftGiven", entry.amount.ToString(), "AbsolutCombat Money");
                    }
                }
            else
            {
                agdata.Players[player.userID].pendingGift.Add(GiftIndex, Gift);
                NewGiftIcon(player);
            }
            agdata.Players[player.userID].ReceivedGifts.Add(GiftIndex);
        }

        private void CreateNumberPadButton(ref CuiElementContainer container, string panelName, int i, int number, string command)
        {
            var pos = CalcNumButtonPos(i);
            UI.CreateButton(ref container, panelName, UIColors["buttonbg"], number.ToString(), 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"{command} {number}");
        }

        private void GetSendMSG(BasePlayer player, string message, string arg1 = "", string arg2 = "", string arg3 = "", string arg4 = "")
        {
            string msg = string.Format(lang.GetMessage(message, this, player.UserIDString), arg1, arg2, arg3, arg4);
            SendReply(player, TitleColor + lang.GetMessage("title", this, player.UserIDString) + "</color>" + MsgColor + msg + "</color>");
        }

        private string GetMSG(string message, BasePlayer player = null, string arg1 = "", string arg2 = "", string arg3 = "", string arg4 = "")
        {
            string p = null;
            if (player != null)
                p = player.UserIDString;
            if (messages.ContainsKey(message))
                return string.Format(lang.GetMessage(message, this, p), arg1, arg2, arg3, arg4);
            else return message;
        }

        bool isAuth(BasePlayer player)
        {
            if (player.net.connection != null)
                if (player.net.connection.authLevel < 2 && !permission.UserHasPermission(player.UserIDString, this.Title + ".admin"))
                    return false;
            return true;
        }

        #endregion

        #region UI Creation

        private string PanelStatic = "PanelStatic";     
        private string PanelGift = "PanelGift";
        private string PanelIcon = "PanelIcon";
        private string PanelOnScreen = "PanelOnScreen";
        private string PanelTime = "PanelTime";

        public class UI
        {
            static public CuiElementContainer CreateOverlayContainer(string panelName, string color, string aMin, string aMax, bool cursor = false)
            {
                var NewElement = new CuiElementContainer()
            {
                {
                    new CuiPanel
                    {
                        Image = {Color = color},
                        RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                        CursorEnabled = cursor
                    },
                    new CuiElement().Parent = "Overlay",
                    panelName
                }
            };
                return NewElement;
            }
            static public void CreatePanel(ref CuiElementContainer element, string panel, string color, string aMin, string aMax, bool cursor = false)
            {
                element.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    CursorEnabled = cursor
                },
                panel);
            }
            static public void CreateLabel(ref CuiElementContainer element, string panel, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                element.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, FadeIn = 1.0f, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);
            }

            static public void CreateButton(ref CuiElementContainer element, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter)
            {
                element.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 1.0f },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel);
            }
            static public void LoadImage(ref CuiElementContainer container, string panel, string img, string aMin, string aMax)
            {
                if (img.StartsWith("http") || img.StartsWith("www"))
                {
                    container.Add(new CuiElement
                    {
                        Parent = panel,
                        Components =
                    {
                        new CuiRawImageComponent {Url = img, Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax }
                    }
                    });
                }
                else
                    container.Add(new CuiElement
                    {
                        Parent = panel,
                        Components =
                    {
                        new CuiRawImageComponent {Png = img, Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax }
                    }
                    });
            }

            static public void CreateTextOutline(ref CuiElementContainer element, string panel, string colorText, string colorOutline, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                element.Add(new CuiElement
                {
                    Parent = panel,
                    Components =
                    {
                        new CuiTextComponent{Color = colorText, FontSize = size, Align = align, Text = text },
                        new CuiOutlineComponent {Distance = "1 1", Color = colorOutline},
                        new CuiRectTransformComponent {AnchorMax = aMax, AnchorMin = aMin }
                    }
                });
            }
            static public void CreateTextOverlay(ref CuiElementContainer element, string panel, string text, string color, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter, float fadein = 1.0f)
            {
                //if (configdata.DisableUI_AG_FadeIn)
                //    fadein = 0;
                element.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, FadeIn = fadein, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);

            }
        }

        private Dictionary<string, string> UIColors = new Dictionary<string, string>
        {
            {"black", "0 0 0 1.0" },
            {"dark", "0.1 0.1 0.1 0.98" },
            {"header", "1 1 1 0.3" },
            {"light", ".564 .564 .564 1.0" },
            {"grey1", "0.6 0.6 0.6 1.0" },
            {"brown", "0.3 0.16 0.0 1.0" },
            {"yellow", "0.9 0.9 0.0 1.0" },
            {"limegreen", "0.42 1.0 0 1.0" },
            {"blue", "0.2 0.6 1.0 1.0" },
            {"red", "1.0 0.1 0.1 1.0" },
            {"white", "1 1 1 1" },
            {"green", "0.28 0.82 0.28 1.0" },
            {"grey", "0.85 0.85 0.85 1.0" },
            {"lightblue", "0.6 0.86 1.0 1.0" },
            {"buttonbg", "0.2 0.2 0.2 0.7" },
            {"buttongreen", "0.133 0.965 0.133 0.9" },
            {"buttonred", "0.964 0.133 0.133 0.9" },
            {"buttongrey", "0.8 0.8 0.8 0.9" },
            {"orange", "1.0 0.64 0.10 1.0" }
        };

        private Dictionary<string, string> TextColors = new Dictionary<string, string>
        {
            {"limegreen", "<color=#6fff00>" }
        };

        #endregion

        #region UI Panels

        void Background(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelStatic);
            if (!UIinfo.ContainsKey(player.userID))
                UIinfo.Add(player.userID, new Info { page = 0 });
            UIinfo[player.userID].open = true;
            var element = UI.CreateOverlayContainer(PanelStatic, "0 0 0 0", "0.2 0.15", "0.8 0.85", true);
            CuiHelper.AddUi(player, element);
            GiftPanel(player);
        }

        void PlayerTimeCounter(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelTime);
            if (!UIinfo.ContainsKey(player.userID))
                UIinfo.Add(player.userID, new Info { page = 0 });
            var element = UI.CreateOverlayContainer(PanelTime, "0 0 0 0", "0.2 0.85", "0.8 0.95");
            UI.CreateTextOutline(ref element, PanelTime, UIColors["white"], UIColors["black"], GetMSG("AccumulatedTime",player, agdata.Players[player.userID].PlayerTime.ToString()), 30, $"0 0", $"1 1", TextAnchor.MiddleCenter);
            CuiHelper.AddUi(player, element);
        }       

        void GiftPanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelGift);
            var element = UI.CreateOverlayContainer(PanelGift, "0.1 0.1 0.1 0.8", "0.2 0.15", "0.8 0.85");
            //UI.CreatePanel(ref element, PanelGift, UIColors["dark"], "0. 0", "1 1");
            var i = 0;
            var page = UIinfo[player.userID].page;
            double count = agdata.Gifts.Count();
            //Puts(count.ToString());
            int entriesallowed = max;
            double remainingentries = count - (page * entriesallowed);
            double totalpages = (Math.Floor(count / entriesallowed));
            if (remainingentries > entriesallowed)
            {
                UI.CreateButton(ref element, PanelGift, UIColors["header"], GetMSG("Next", player), 16, "0.76 0.02", "0.86 0.075", $"UI_AG_GiftMenu {page + 1}");
            }
            if (page > 0)
            {
                UI.CreateButton(ref element, PanelGift, UIColors["header"], GetMSG("Back", player), 16, "0.65 0.02", "0.75 0.075", $"UI_AG_GiftMenu {page - 1}");
            }
            if (isAuth(player))
            {
                UI.CreateButton(ref element, PanelGift, UIColors["header"], GetMSG("ToggleAdmin", player), 12, "0.03 0.02", "0.13 0.075", $"UI_AG_ToggleAdmin");
                if (UIinfo[player.userID].admin)
                    UI.CreateButton(ref element, PanelGift, UIColors["orange"], GetMSG("CreateGift", player), 12, "0.16 0.02", "0.26 0.075", $"UI_AG_CreateGifts");
            }
            UI.CreateButton(ref element, PanelGift, UIColors["buttonred"], GetMSG("Close", player), 16, "0.87 0.02", "0.97 0.075", $"UI_AG_DestroyGiftPanel");
            CuiHelper.AddUi(player, element);
            int n = 0;
            int shownentries = page * entriesallowed;
            List<int> completed = new List<int>();
            if (agdata.Players.ContainsKey(player.userID))
                foreach (var entry in agdata.Players[player.userID].ReceivedGifts)
                    completed.Add(entry);
            foreach (var entry in agdata.Gifts.OrderBy(kvp => kvp.Key))
            {
                i++;
                if (i < shownentries + 1) continue;
                if (i <= shownentries + entriesallowed)
                {
                    CreateGiftMenuEntry(player, entry.Key, n);
                    n++;
                }
                if (n >= entriesallowed) break;
            }
            PlayerTimeCounter(player);
        }

        private void CreateGifts(BasePlayer player, int step = 0)
        {
            var i = 0;
            CuiElementContainer element = UI.CreateOverlayContainer(PanelGift, "0.1 0.1 0.1 0.8", "0.3 0.3", "0.7 0.9");
            switch (step)
            {
                case 0:
                    CuiHelper.DestroyUi(player, PanelGift);
                    if (giftprep.ContainsKey(player.userID))
                        giftprep.Remove(player.userID);
                    giftprep.Add(player.userID, new GiftCreation());
                    //UI.CreateLabel(ref element, PanelGift, UIColors["black"], $"{TextColors["limegreen"]} {GetMSG("SelectGiftTimer")}", 20, "0.05 0", ".95 1", TextAnchor.MiddleCenter);
                    NumberPad(player, "UI_AG_SelectTime", "SelectTime");
                    return;
                case 1:
                    CuiHelper.DestroyUi(player, PanelGift);
                    element = UI.CreateOverlayContainer(PanelGift, "0.1 0.1 0.1 0.8", "0.4 0.3", "0.6 0.6");
                    UI.CreateLabel(ref element, PanelGift, UIColors["limegreen"], GetMSG("VIPGift", player, giftprep[player.userID].time.ToString()), 20, "0.05 .4", ".95 .95");
                    UI.CreateButton(ref element, PanelGift, UIColors["buttonbg"], GetMSG("Yes", player), 18, "0.2 0.05", "0.4 0.25", $"UI_AG_VIP true", TextAnchor.MiddleCenter);
                    UI.CreateButton(ref element, PanelGift, UIColors["buttonred"], GetMSG("No", player), 18, "0.6 0.05", "0.8 0.25", $"UI_AG_VIP false");
                    break;
                case 2:
                    CuiHelper.DestroyUi(player, PanelGift);
                    var cat = UIinfo[player.userID].cat;
                    float[] pos;
                    UI.CreatePanel(ref element, PanelGift, "0 0 0 0", $".0001 0.0001", $"0.0002 0.0002");
                    UI.CreateLabel(ref element, PanelGift, UIColors["limegreen"], GetMSG("SelectGift", player, giftprep[player.userID].time.ToString()), 20, "0.05 .95", ".95 1", TextAnchor.MiddleCenter);
                    foreach (var entry in Enum.GetValues(typeof(ItemCategory)).Cast<ItemCategory>().ToList().OrderBy(k=> k == ItemCategory.All).ThenBy(k=>k.ToString().First()))
                    {
                        if (entry == ItemCategory.Search || entry == ItemCategory.Common) continue;
                        pos = FilterButton(i);
                        if(cat == entry)
                            UI.CreateButton(ref element, PanelGift, UIColors["yellow"], entry.ToString(), 10, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_AG_ChangeCat {entry.ToString()}");
                        else UI.CreateButton(ref element, PanelGift, UIColors["black"], entry.ToString(), 10, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_AG_ChangeCat {entry.ToString()}");
                        i++;
                    }
                    var itemList = ItemManager.itemList.Where(k => cat != ItemCategory.All ? k.category == cat : k.category != ItemCategory.All).Select(k => k).ToList();
                    double count = itemList.Count();
                    var page = UIinfo[player.userID].page;
                    double entriesallowed = 30;
                    double remainingentries = count - (page * (entriesallowed));
                    double totalpages = (Math.Floor(count / (entriesallowed))) - 1;
                    {
                        if (remainingentries > entriesallowed)
                        {
                            UI.CreateButton(ref element, PanelGift, UIColors["header"], GetMSG("Next", player), 16, "0.85 0.02", "0.95 0.075", $"UI_AG_CreateGifts {2} {page + 1}");
                        }
                        if (page > 0)
                        {
                            UI.CreateButton(ref element, PanelGift, UIColors["header"], GetMSG("Back", player), 16, "0.74 0.02", "0.84 0.075", $"UI_AG_CreateGifts {2} {page - 1}");
                        }
                    }
                    i = 0;
                    int n = 0;
                    pos = CalcButtonPos(n);
                    double shownentries = page * entriesallowed;
                    if (page == 0 && (cat == ItemCategory.All || cat == ItemCategory.Search || cat == ItemCategory.Common))
                    {
                        if (ServerRewards)
                        {
                            UI.LoadImage(ref element, PanelGift, TryForImage("SR"), $"{pos[0] + 0.005f} {pos[1] + 0.005f}", $"{pos[2] - 0.005f} {pos[3] - 0.005f}");
                            UI.CreateButton(ref element, PanelGift, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_AG_SelectGift SR");
                            n++;
                            i++;
                        }
                        if (Economics)
                        {
                            pos = CalcButtonPos(n);
                            UI.LoadImage(ref element, PanelGift, TryForImage("ECO"), $"{pos[0] + 0.005f} {pos[1] + 0.005f}", $"{pos[2] - 0.005f} {pos[3] - 0.005f}");
                            UI.CreateButton(ref element, PanelGift, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_AG_SelectGift ECO");
                            n++;
                            i++;
                        }
                        if (AbsolutCombat)
                        {
                            pos = CalcButtonPos(n);
                            UI.LoadImage(ref element, PanelGift, TryForImage("AC"), $"{pos[0] + 0.005f} {pos[1] + 0.005f}", $"{pos[2] - 0.005f} {pos[3] - 0.005f}");
                            UI.CreateButton(ref element, PanelGift, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_AG_SelectGift AC");
                            n++;
                            i++;
                        }
                        //if (configData.UseGatherIncrease)
                        //{
                        //    pos = CalcButtonPos(n);
                        //    UI.LoadImage(ref element, PanelGift, TryForImage("GATHER"), $"{pos[0] + 0.005f} {pos[1] + 0.005f}", $"{pos[2] - 0.005f} {pos[3] - 0.005f}");
                        //    UI.CreateButton(ref element, PanelGift, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_AG_SelectGift GATHER");
                        //    n++;
                        //    i++;
                        //}
                    }
                    foreach (var item in itemList.OrderBy(k=>k.displayName.english))
                    {
                        i++;
                        if (i < shownentries + 1) continue;
                        else if (i <= shownentries + entriesallowed)
                        {
                            pos = CalcButtonPos(n);
                            UI.CreatePanel(ref element, PanelGift, UIColors["header"], $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                            UI.LoadImage(ref element, PanelGift, TryForImage(item.shortname, 0), $"{pos[0] + 0.005f} {pos[1] + 0.005f}", $"{pos[2] - 0.005f} {pos[3] - 0.005f}");
                            //UI.CreateLabel(ref element, PanelGift, UIColors["limegreen"], item.shortname, 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", TextAnchor.MiddleCenter);
                            UI.CreateButton(ref element, PanelGift, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_AG_SelectGift {item.itemid}");
                            n++;
                        }
                    }
                    UI.CreateButton(ref element, PanelGift, UIColors["buttonred"], GetMSG("Close", player), 16, "0.03 0.02", "0.13 0.075", $"UI_AG_DestroyGiftPanel");
                    break;
                default:
                    CuiHelper.DestroyUi(player, PanelGift);
                    UI.CreatePanel(ref element, PanelGift, "0 0 0 0", $".0001 0.0001", $"0.0002 0.0002");
                    UI.CreateLabel(ref element, PanelGift, UIColors["limegreen"], GetMSG("NewGiftInfo", player, giftprep[player.userID].time.ToString()), 20, "0.05 .8", ".95 .95");
                    string GiftDetails = "";
                    var alt = "";
                    foreach (var entry in giftprep[player.userID].gifts)
                        if (entry.ID != 0)
                            GiftDetails += $"{GetMSG("GiftDetails", player, GetDisplayNameFromSN(entry.ID), entry.amount.ToString())}\n";
                        else
                        {
                            if (entry.AC) alt = "AbsolutCombat Money";
                            if (entry.Eco) alt = "Economics";
                            if (entry.SR) alt = "ServerRewards Points";
                            GiftDetails += $"{GetMSG("GiftDetails", player, alt, entry.amount.ToString())}\n";
                        }
                    UI.CreateLabel(ref element, PanelGift, UIColors["limegreen"], GiftDetails, 20, "0.1 0.16", "0.9 0.75", TextAnchor.MiddleLeft);
                    UI.CreateButton(ref element, PanelGift, UIColors["buttonbg"], GetMSG("FinalizeGift", player), 18, "0.2 0.05", "0.4 0.15", $"UI_AG_FinalizeGift", TextAnchor.MiddleCenter);
                    if (giftprep[player.userID].gifts.Count() < 9)
                        UI.CreateButton(ref element, PanelGift, UIColors["buttonbg"], GetMSG("AddToGift", player), 18, "0.401 0.05", "0.599 0.15", $"UI_AG_CreateGifts {2} {0}", TextAnchor.MiddleCenter);
                    UI.CreateButton(ref element, PanelGift, UIColors["buttonred"], GetMSG("Cancel", player), 18, "0.6 0.05", "0.8 0.15", $"UI_AG_CancelGiftCreation");
                    break;
            }
            CuiHelper.AddUi(player, element);
        }

        string GetDisplayNameFromSN(int id)
        {
            ItemDefinition def = ItemManager.FindItemDefinition(id);
            if (def != null) return def.displayName.translated;
            else return id.ToString();
        }

        private void CreateGiftMenuEntry(BasePlayer player, int ID, int num)
        {
            var panelName = "GiftEntry" + num;
            CuiHelper.DestroyUi(player, "GiftEntry" + num);
            var pos = GiftPos(num);
            CuiElementContainer container = UI.CreateOverlayContainer(panelName, UIColors["header"], $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
            //UI.CreatePanel(ref container, panelName, UIColors["header"], $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
            if (agdata.Gifts[ID].vip)
            {
                if (configData.HideVIP && !permission.UserHasPermission(player.UserIDString, this.Title + ".vip")) return;
                UI.CreateLabel(ref container, panelName, UIColors["red"], "VIP", 30, $"0 0", $"1 1", TextAnchor.MiddleCenter);
            }
            var i = 0;
            float[] loc;
            foreach (var entry in agdata.Gifts[ID].gifts)
            {
                var item = ItemManager.CreateByItemID(entry.ID);
                {
                    loc = giftentrypos(i);
                    UI.CreatePanel(ref container, panelName, UIColors["buttonbg"], $"{loc[0]} {loc[1]}", $"{loc[2]} {loc[3]}");
                    if (item != null)
                        UI.LoadImage(ref container, panelName, TryForImage(item.info.shortname, entry.Skin), $"{loc[0]} {loc[1]}", $"{loc[2]} {loc[3]}");
                    else if (entry.Eco)
                        UI.LoadImage(ref container, panelName, TryForImage("ECO"), $"{loc[0]} {loc[1]}", $"{loc[2]} {loc[3]}");
                    else if (entry.SR)
                        UI.LoadImage(ref container, panelName, TryForImage("SR"), $"{loc[0]} {loc[1]}", $"{loc[2]} {loc[3]}");
                    else if (entry.AC)
                        UI.LoadImage(ref container, panelName, TryForImage("AC"), $"{loc[0]} {loc[1]}", $"{loc[2]} {loc[3]}");
                    UI.CreateLabel(ref container, panelName, UIColors["limegreen"], entry.amount.ToString(), configData.UITextSize, $"{loc[0]} {loc[1]}", $"{loc[2]} {loc[3]}", TextAnchor.LowerCenter);
                    i++;
                }
            }
            loc = giftentrypos(9);
            if (isAuth(player) && UIinfo[player.userID].admin)
            {
                UI.CreateTextOutline(ref container, panelName, UIColors["white"], UIColors["black"], GetMSG("GiftTitle", player, ID.ToString()), configData.UITextSize, "0 0", "1 1", TextAnchor.UpperCenter);
                UI.CreateButton(ref container, panelName, UIColors["buttonred"], GetMSG("Delete", player), (int)(configData.UITextSize * .8), $"{loc[0]} {loc[1]}", $"{loc[2]} {loc[3]}", $"UI_AG_RemoveGift {ID}");
            }
            else if (agdata.Players[player.userID].ReceivedGifts.Contains(ID))
            {
                UI.CreateTextOutline(ref container, panelName, UIColors["green"], UIColors["black"], GetMSG("GiftTitleCompleted", player, ID.ToString()), configData.UITextSize, "0 0", "1 1", TextAnchor.UpperCenter);
                if (agdata.Players[player.userID].pendingGift.ContainsKey(ID)) UI.CreateButton(ref container, panelName, UIColors["green"], GetMSG("Redeem", player), (int)(configData.UITextSize * .8), $"{loc[0]} {loc[1]}", $"{loc[2]} {loc[3]}", $"UI_AG_RedeemGift {ID}");
            }
            else
            {
                UI.CreateTextOutline(ref container, panelName, UIColors["white"], UIColors["black"], GetMSG("GiftTitle", player, ID.ToString()), configData.UITextSize, "0 0", "1 1", TextAnchor.UpperCenter);
            }
            CuiHelper.AddUi(player, container);
        }

        void NewGiftIcon(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelIcon);
            if (!agdata.Players.ContainsKey(player.userID) || agdata.Players[player.userID].pendingGift.Count() < 1) return;
            var element = UI.CreateOverlayContainer(PanelIcon, "0 0 0 0", $"{configData.minx} {configData.miny}", $"{configData.maxx} {configData.maxy}");
            UI.LoadImage(ref element, PanelIcon, TryForImage("NewGift"), "0 0", "1 1");
            UI.CreateButton(ref element, PanelIcon, "0 0 0 0", "", 12, "0 0", "1 1", "UI_AG_GiftMenu");
            CuiHelper.AddUi(player, element);
        }

        private void NumberPad(BasePlayer player, string cmd, string title)
        {
            CuiHelper.DestroyUi(player, PanelGift);
            var element = UI.CreateOverlayContainer(PanelGift, UIColors["dark"], "0.35 0.3", "0.65 0.7");
            UI.CreatePanel(ref element, PanelGift, UIColors["light"], "0.01 0.02", "0.99 0.98");
            UI.CreateLabel(ref element, PanelGift, UIColors["limegreen"], GetMSG(title, player), 16, "0.1 0.85", "0.9 .98", TextAnchor.UpperCenter);
            var n = 1;
            var i = 0;
            if (title == "SelectTime")
            {
                while (n < 20)
                {
                    CreateNumberPadButton(ref element, PanelGift, i, n, cmd); i++; n++;
                }
                while (n >= 20 && n < 60)
                {
                    CreateNumberPadButton(ref element, PanelGift, i, n, cmd); i++; n += 5;
                }
                while (n >= 60 && n < 240)
                {
                    CreateNumberPadButton(ref element, PanelGift, i, n, cmd); i++; n += 30;
                }
                while (n >= 240 && n <= 1470)
                {
                    CreateNumberPadButton(ref element, PanelGift, i, n, cmd); i++; n += 60;
                }
            }
            else if (title == "SelectAmount")
            {
                while (n < 10)
                {
                    CreateNumberPadButton(ref element, PanelGift, i, n, cmd); i++; n++;
                }
                while (n >= 10 && n < 25)
                {
                    CreateNumberPadButton(ref element, PanelGift, i, n, cmd); i++; n += 5;
                }
                while (n >= 25 && n < 200)
                {
                    CreateNumberPadButton(ref element, PanelGift, i, n, cmd); i++; n += 25;
                }
                while (n >= 200 && n <= 950)
                {
                    CreateNumberPadButton(ref element, PanelGift, i, n, cmd); i++; n += 50;
                }
                while (n >= 1000 && n <= 10000)
                {
                    CreateNumberPadButton(ref element, PanelGift, i, n, cmd); i++; n += 500;
                }
            }
            UI.CreateButton(ref element, PanelGift, UIColors["buttonred"], GetMSG("Quit", player), 10, "0.03 0.02", "0.13 0.075", $"UI_AG_CancelGiftCreation");
            CuiHelper.AddUi(player, element);
        }

        void OnScreen(BasePlayer player, string msg, string arg1 = "", string arg2 = "", string arg3 = "")
        {
            if (timers.ContainsKey("Onscreen_"+player.userID.ToString()))
            {
                timers["Onscreen_" + player.userID.ToString()].Destroy();
                timers.Remove("Onscreen_" + player.userID.ToString());
            }
            CuiHelper.DestroyUi(player, PanelOnScreen);
            var element = UI.CreateOverlayContainer(PanelOnScreen, "0.0 0.0 0.0 0.0", "0.15 0.8", "0.85 .9");
            UI.CreateTextOutline(ref element, PanelOnScreen, UIColors["white"], UIColors["black"], GetMSG(msg, player, arg1, arg2, arg3), 24, "0.0 0.0", "1.0 1.0");
            CuiHelper.AddUi(player, element);
            timers.Add("Onscreen_" + player.userID.ToString(), timer.Once(4, () => CuiHelper.DestroyUi(player, PanelOnScreen)));
        }


        #endregion

        #region UI Calculations

        void SetGiftPositions()
        {
            GiftPositions.Clear();
            int rows = 2;
            int columns = 2;
            if (max > 4)
            {
                if (max <= 6)
                    columns = 3;
                else if (max <= 8)
                    columns = 4;
                else if (max <= 10)
                    columns = 5;
                else if (max <= 12)
                    columns = 6;
            }
            //int rows = configData.GiftsPerPanel > 8 4;
            //if (max % rows > 0)
            //{
            //    if (max % 3 == 0) rows = 3;
            //    else if (max % 2 == 0) rows = 2;
            //    else max++;
            //}
            //Puts($"Number: {number}");
            //Puts($"Max: {max}");
            //int columns = max / rows;
            //Puts($"Columns: {columns} - Rows {rows}");
            //"0.2 0.15", "0.8 0.85"
            Vector2 position = new Vector2(0.205f, .848f);
            Vector2 dimensions = new Vector2();
            dimensions.x = (.6f - (.005f * rows)) / rows;
            dimensions.y = (.6f - (.01f * columns)) / columns;
            position.y = .848f - dimensions.y;
            float gap = (.6f - (dimensions.x * rows)) / (rows+1);
            position.x = .2f + gap;
            //position.x = (float)(1 - ((dimensions.x + .005f) * root));
            int testValue = 0;
            for (int i = 0; i < max; i++)
            {
                float offsetY = 0;
                float offsetX = 0;
                if (i%columns != 0)
                {
                    testValue = i;
                   // Puts($"Index: {i}: TestValue {testValue}");
                    while (testValue - columns > 0)
                    {
                        testValue -= columns;
                      //  Puts($">>Index: {i}: TestValue {testValue}");
                    }
                    offsetY = (-0.01f - dimensions.y) * testValue;
                }
                offsetX = (dimensions.x + gap) * (i < columns ? 0 : (int)Math.Floor((double)i / columns));
                Vector2 offset = new Vector2(offsetX, offsetY);
                Vector2 posMin = position + offset;
                Vector2 posMax = posMin + dimensions;
                GiftPositions.Add(i, new float[] { posMin.x, posMin.y, posMax.x, posMax.y });
            }
            //foreach (var entry in GiftPositions)
            //    Puts($"{entry.Key}: MinX{entry.Value[0]}, MaxX{entry.Value[2]}, MinY{entry.Value[1]}, MaxY{entry.Value[3]}");
        }

        private float[] GiftPos(int number)
        {
            return GiftPositions[number];
        }

        //private float[] GiftPos(int number)
        //{
        //    Vector2 position = new Vector2(0.03f, 0.525f);
        //    Vector2 dimensions = new Vector2(0.46f, 0.425f);
        //    float offsetY = 0;
        //    float offsetX = 0;
        //    if (number >= 0 && number < 2)
        //    {
        //        offsetY = (-0.01f - dimensions.y) * number;
        //    }
        //    if (number > 1 && number < 4)
        //    {
        //        offsetX = dimensions.x + 0.005f;
        //        offsetY = (-0.01f - dimensions.y) * (number - 2);
        //    }
        //    Vector2 offset = new Vector2(offsetX, offsetY);
        //    Vector2 posMin = position + offset;
        //    Vector2 posMax = posMin + dimensions;
        //    return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        //}

        private float[] FilterButton(int number)
        {
            Vector2 position = new Vector2(0.01f, 1.01f);
            Vector2 dimensions = new Vector2(0.1f, 0.04f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 10)
            {
            offsetX = (0.01f + dimensions.x) * number;
            }
            if (number >= 10 && number < 20)
            {
                offsetX = (0.01f + dimensions.x) * (number - 10);
                offsetY = (-0.005f - dimensions.y) * 1;
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        private float[] BackgroundButton(int number)
        {
            Vector2 position = new Vector2(0.3f, 0.97f);
            Vector2 dimensions = new Vector2(0.035f, 0.03f);
            float offsetY = 0;
            float offsetX = 0;
            offsetX = (0.005f + dimensions.x) * number;
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        
        private float[] giftentrypos(int number)
        {
            Vector2 position = new Vector2(0.005f, 0.42f);
            Vector2 dimensions = new Vector2(0.19f, 0.35f);
            if (max > 4)
            {
                if (max <= 6)
                    dimensions.x = 0.15f;
                else if (max <= 8)
                {
                    dimensions.x = 0.12f;
                    dimensions.y = .38f;
                }
                else if (max <= 10)
                    dimensions.x = 0.1f;
                else if (max <= 12)
                    dimensions.x = 0.08f;
            }
            float gap = (1 - (dimensions.x *5)) / 6;
            position.x = gap;
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 5)
            {
                offsetX = (gap + dimensions.x) * number;
            }
            if (number > 4 && number < 10)
            {
                offsetX = (gap + dimensions.x) * (number - 5);
                offsetY = (-0.03f - dimensions.y) * 1;
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }


        private float[] CalcButtonPos(int number)
        {
            Vector2 position = new Vector2(0.02f, 0.78f);
            Vector2 dimensions = new Vector2(0.15f, 0.15f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 6)
            {
                offsetX = (0.01f + dimensions.x) * number;
            }
            if (number > 5 && number < 12)
            {
                offsetX = (0.01f + dimensions.x) * (number - 6);
                offsetY = (-0.025f - dimensions.y) * 1;
            }
            if (number > 11 && number < 18)
            {
                offsetX = (0.01f + dimensions.x) * (number - 12);
                offsetY = (-0.025f - dimensions.y) * 2;
            }
            if (number > 17 && number < 24)
            {
                offsetX = (0.01f + dimensions.x) * (number - 18);
                offsetY = (-0.025f - dimensions.y) * 3;
            }
            if (number > 23 && number < 30)
            {
                offsetX = (0.01f + dimensions.x) * (number - 24);
                offsetY = (-0.025f - dimensions.y) * 4;
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        private float[] CalcNumButtonPos(int number)
        {
            Vector2 position = new Vector2(0.05f, 0.75f);
            Vector2 dimensions = new Vector2(0.09f, 0.10f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 9)
            {
                offsetX = (0.01f + dimensions.x) * number;
            }
            if (number > 8 && number < 18)
            {
                offsetX = (0.01f + dimensions.x) * (number - 9);
                offsetY = (-0.02f - dimensions.y) * 1;
            }
            if (number > 17 && number < 27)
            {
                offsetX = (0.01f + dimensions.x) * (number - 18);
                offsetY = (-0.02f - dimensions.y) * 2;
            }
            if (number > 26 && number < 36)
            {
                offsetX = (0.01f + dimensions.x) * (number - 27);
                offsetY = (-0.02f - dimensions.y) * 3;
            }
            if (number > 35 && number < 45)
            {
                offsetX = (0.01f + dimensions.x) * (number - 36);
                offsetY = (-0.02f - dimensions.y) * 4;
            }
            if (number > 44 && number < 54)
            {
                offsetX = (0.01f + dimensions.x) * (number - 45);
                offsetY = (-0.02f - dimensions.y) * 5;
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        #endregion

        #region UI Commands

        [ChatCommand("gift")]
        private void cmdgift(BasePlayer player, string command, string[] args)
        {
            Background(player);
            return;
        }

        [ChatCommand("max")]
        private void chatmax(BasePlayer player, string command, string[] args)
        {
            //if not absolut doesnt work... for testing only. To prevent people fucking stuff up.
            if (player.userID != 76561197977401750) return;
            max = Convert.ToInt32(args[0]);
            SetGiftPositions();
        }

        [ConsoleCommand("UI_AG_GiftMenu")]
        private void cmdUI_AG_GiftMenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            int page;
            if (arg.Args == null || arg.Args.Length == 0 || !int.TryParse(arg.Args[0], out page)) page = 0;
            if (!UIinfo.ContainsKey(player.userID))
                UIinfo.Add(player.userID, new Info { page = 0 });
            UIinfo[player.userID].page = page;
            for (int i = 0; i < max; i++)
                CuiHelper.DestroyUi(player, "GiftEntry" + i);
            GiftPanel(player);
        }     

        [ConsoleCommand("UI_AG_SelectTime")]
        private void cmdUI_AG_SelectTime(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            int time = Convert.ToInt32(arg.Args[0]);
            if (agdata.Gifts.ContainsKey(time))
                GetSendMSG(player, "TimeAlreadyExists", time.ToString());
            giftprep[player.userID].time = time;
            CreateGifts(player, 1);
        }

        [ConsoleCommand("UI_AG_VIP")]
        private void cmdUI_AG_VIP(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var answer = arg.Args[0];
            if (answer == "true")
                giftprep[player.userID].vip = true;
            else giftprep[player.userID].vip = false;
            UIinfo[player.userID].page = 0;
            CreateGifts(player, 2);
        }
   
        [ConsoleCommand("UI_AG_ToggleAdmin")]
        private void cmdUI_AG_ToggleAdmin(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !isAuth(player)) return;
            if (UIinfo[player.userID].admin)
                UIinfo[player.userID].admin = false;
            else UIinfo[player.userID].admin = true;
            GiftPanel(player);
        }

        
        [ConsoleCommand("UI_AG_ChangeCat")]
        private void cmdUI_AG_ChangeCat(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !isAuth(player)) return;
            ItemCategory cat = (ItemCategory)Enum.Parse(typeof(ItemCategory), arg.Args[0]);
            UIinfo[player.userID].cat = cat;
            UIinfo[player.userID].page = 0;
            CreateGifts(player, 2);
        }


        [ConsoleCommand("UI_AG_CreateGifts")]
        private void cmdUI_AG_CreateGifts(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !isAuth(player)) return;
            if (arg.Args == null || arg.Args.Length == 0) { DestroyGiftPanel(player, false); CreateGifts(player); }
            else
            {
                int step;
                if (!int.TryParse(arg.Args[0], out step)) return;
                int page;
                if (arg.Args.Length < 2 || !int.TryParse(arg.Args[1], out page)) page = 0;
                UIinfo[player.userID].page = page;
                CreateGifts(player, step);
            }
        }



        [ConsoleCommand("UI_AG_RedeemGift")]
        private void cmdUI_AG_RedeemGift(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            int ID;
            if (!int.TryParse(arg.Args[0], out ID)) return;
            if (agdata.Players[player.userID].pendingGift.ContainsKey(ID))
            {

                if(agdata.Gifts[ID].gifts.Count() > ((player.inventory.containerBelt.capacity - player.inventory.containerBelt.itemList.Count) + (player.inventory.containerWear.capacity - player.inventory.containerWear.itemList.Count) + (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count)))
                {
                    GetSendMSG(player, "NotEnoughSpace");
                    return;
                }
                foreach (var entry in agdata.Players[player.userID].pendingGift[ID].gifts)
                {
                    if (entry.SR)
                    {
                        ServerRewards?.Call("AddPoints", player.userID.ToString(), entry.amount);
                        GetSendMSG(player, "NewGiftGiven", entry.amount.ToString(), "ServerRewards Points");
                    }
                    else if (entry.Eco)
                    {
                        Economics.Call("DepositS", player.userID.ToString(), entry.amount);
                        GetSendMSG(player, "NewGiftGiven", entry.amount.ToString(), "Economics");
                    }
                    else if (entry.AC)
                    {
                        AbsolutCombat.Call("AddMoney", player.userID.ToString(), entry.amount, false);
                        GetSendMSG(player, "NewGiftGiven", entry.amount.ToString(), "AbsolutCombat Money");
                    }
                    else
                    {
                        Item item = ItemManager.CreateByItemID(entry.ID, entry.amount, entry.Skin);
                        if (item != null)
                        {
                            player.GiveItem(item);
                            GetSendMSG(player, "NewGiftGiven", item.amount.ToString(), item.info.displayName.english);
                        }
                    }
                }
                agdata.Players[player.userID].pendingGift.Remove(ID);
            }
            NewGiftIcon(player);
            GiftPanel(player);
        }

    [ConsoleCommand("UI_AG_RemoveGift")]
        private void cmdUI_AG_RemoveGift(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            int ID;
            if (!int.TryParse(arg.Args[0], out ID)) return;
            if (agdata.Gifts.ContainsKey(ID))
                agdata.Gifts.Remove(ID);
            GetSendMSG(player, "GiftRemoved", ID.ToString());
            GiftPanel(player);
            SaveData();
        }

        [ConsoleCommand("UI_AG_FinalizeGift")]
        private void cmdUI_AG_FinalizeGift(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (agdata.Gifts.ContainsKey(giftprep[player.userID].time))
                agdata.Gifts.Remove(giftprep[player.userID].time);
            agdata.Gifts.Add(giftprep[player.userID].time, new GiftEntry {ID = giftprep[player.userID].time, gifts = giftprep[player.userID].gifts, vip = giftprep[player.userID].vip });
            GetSendMSG(player, "NewGift", giftprep[player.userID].time.ToString());
            SaveData();
            UIinfo[player.userID].page = 0;
            Background(player);
        }



        [ConsoleCommand("UI_AG_DestroyGiftPanel")]
        private void cmdUI_AG_DestroyGiftPanel(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null) return;
            DestroyGiftPanel(player, true);
        }

        [ConsoleCommand("UI_AG_CancelGiftCreation")]
        private void cmdUI_AG_CancelListing(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            CancelGiftCreation(player);
        }


        [ConsoleCommand("UI_AG_SelectGift")]
        private void cmdUI_AG_SelectGift(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            int item;
            if (int.TryParse(arg.Args[0], out item)) giftprep[player.userID].currentgift = new Gift { ID = item };
            else
            {
                if (arg.Args[0] == "SR")
                    giftprep[player.userID].currentgift = new Gift { SR = true };
                else if (arg.Args[0] == "ECO")
                    giftprep[player.userID].currentgift = new Gift { Eco = true };
                else if (arg.Args[0] == "AC")
                    giftprep[player.userID].currentgift = new Gift { AC = true };
            }
            DestroyGiftPanel(player, false);
            NumberPad(player, "UI_AG_SelectAmount", "SelectAmount");
        }

        [ConsoleCommand("UI_AG_SelectAmount")]
        private void cmdUI_AG_SelectPriceAmount(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            int amount = Convert.ToInt32(arg.Args[0]);
            giftprep[player.userID].currentgift.amount = amount;
            giftprep[player.userID].gifts.Add(giftprep[player.userID].currentgift);
            giftprep[player.userID].currentgift = null;
            CreateGifts(player, 99);
        }     
        #endregion

        #region Timers

        private void SaveLoop()
        {
            if (timers.ContainsKey("save"))
            {
                timers["save"].Destroy();
                timers.Remove("save");
            }
            SaveData();
            timers.Add("save", timer.Once(600, () => SaveLoop()));
        }

        private void InfoLoop()
        {
            if (timers.ContainsKey("info"))
            {
                timers["info"].Destroy();
                timers.Remove("info");
            }
            if (configData.InfoInterval == 0) return;
            foreach (BasePlayer p in BasePlayer.activePlayerList)
            {
                GetSendMSG(p, "AGInfo");
            }
            timers.Add("info", timer.Once(configData.InfoInterval * 60, () => InfoLoop()));
        }

        #endregion

        #region Classes
        class GiftData
        {
            public Dictionary<int, GiftEntry> Gifts = new Dictionary<int, GiftEntry>();
            public Dictionary<ulong, playerdata> Players = new Dictionary<ulong, playerdata>();
        }
        class GiftEntry
        {
            public bool vip;
            public List<Gift> gifts = new List<Gift>();
            public int ID;
        }

        class Gift
        {
            public ulong Skin;
            public int amount;
            public int ID;
            public bool SR;
            public bool Eco;
            public bool AC;
        }

        class playerdata
        {
            public int PlayerTime;
            public Dictionary<int, GiftEntry> pendingGift = new Dictionary<int, GiftEntry>();
            public List<int> ReceivedGifts = new List<int>();
            public double ResetTime;
        }

        class GiftCreation
        {
            public int time;
            public bool vip;
            public List<Gift> gifts = new List<Gift>();
            public Gift currentgift;
        }

        #endregion

        #region Data Management

        void SaveData()
        {
            AGData.WriteObject(agdata);
        }

        void LoadData()
        {
            try
            {
                agdata = AGData.ReadObject<GiftData>();
            }
            catch
            {
                Puts("Couldn't load the Absolut Gift Data, creating a new datafile");
                agdata = new GiftData();
            }
            if (agdata.Gifts == null)
                agdata.Gifts = new Dictionary<int, GiftEntry>();
            if (agdata.Players == null)
                agdata.Players = new Dictionary<ulong, playerdata>();
            initialized = true;
        }

        #endregion
        float Default_minx = 0.21f;
        float Default_miny = 0.005f;
        float Default_maxx = 0.34f;
        float Default_maxy = 0.055f;
        #region Config        
        private ConfigData configData;
        class ConfigData
        {
            public int InfoInterval { get; set; }
            public bool NoAFK { get; set; }
            public bool UseGatherIncrease { get; set; }
            public int ResetInDays { get; set; }
            public int GiftsPerPanel { get; set; }
            public bool HideVIP { get; set; }
            public string GiftIconImage { get; set; }
            public int UITextSize { get; set; }
            public float minx { get; set; }
            public float miny { get; set; }
            public float maxx { get; set; }
            public float maxy { get; set; }

        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
            bool changed = false;
            if (configData.maxx == new float() && configData.maxy == new float() && configData.minx == new float() && configData.miny == new float())
            {
                configData.minx = Default_minx;
                configData.miny = Default_miny;
                configData.maxx = Default_maxx;
                configData.maxy = Default_maxy;
                changed = true;
            }
            if (configData.GiftsPerPanel < 4)
            {
                configData.GiftsPerPanel = 4;
                changed = true;
            }
            else if (configData.GiftsPerPanel > 12)
            {
                configData.GiftsPerPanel = 12;
                Puts("You have Gift Per Panel set above 12. 12 is the max; and has been adjusted for you.");
                changed = true;
            }
            if (configData.UITextSize <= 2)
            {
                configData.UITextSize = 12;
                changed = true;
            }
            if(changed) SaveConfig(configData);
            max = configData.GiftsPerPanel;
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                InfoInterval = 15,
                NoAFK = true,
                UseGatherIncrease = true,
                ResetInDays = 1,
                GiftIconImage = "http://i.imgur.com/zMe9ky5.png",
                GiftsPerPanel = 4,
                UITextSize = 12,
                minx = Default_minx,
                miny = Default_miny,
                maxx = Default_maxx,
                maxy = Default_maxy,
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Messages
        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"title", "Absolut Gifts: " },
            {"AGInfo", "This server is running Absolut Gifts. Type /gift to access the Gift Menu!"},
            {"Next", "Next" },
            {"Back", "Back" },
            {"First", "First" },
            {"Last", "Last" },
            {"Close", "Close"},
            {"Quit", "Quit"},
            {"ImgReload", "Images have been wiped and reloaded!" },
            {"ImgRefresh", "Images have been refreshed !" },
            {"NewGiftInfo", "New Gift Information for Playing {0} Minute(s)." },
            {"GiftDetails", "{1} - {0}" },
            {"TimeAlreadyExists", "The select time {0} already exists as a Gift, if you continue the old entry will be removed" },
            {"GiftCreationCanceled", "You have successfully cancelled Gift Creation." },
            {"ToggleAdmin", "Toggle Admin" },
            {"Delete", "Delete" },
            {"GiftTitle", "Gift Requirement: {0} Minute(s)" },
            {"GiftTitleInProgress", "IN PROGRESS: {0} Minute(s)\nYou have {1} Minute(s) Remaining!" },
            {"GiftTitleCompleted", "COMPLETED: {0} Minute(s)" },
            {"NewGift", "You have successfully created a new gift for {0} Minute(s)!" },
            {"GiftRemoved", "You have deleted the gift for {0} Minute(s)!" },
            {"NewGiftGiven", "You have been given {0} {1} for your PlayTime! Thanks for playing on the server today!" },
            {"SelectTime", "Select Minute Requirement for this Gift..." },
            {"SelectAmount", "Select the amount of the chosen item for this Gift." },
            {"CreateGift", "Create a Gift" },
            {"ManageGifts", "Manage Gifts" },
            {"SelectGift", "Select a Gift Item" },
            {"FinalizeGift", "Save Gift" },
            {"AddToGift", "Add More..." },
            {"Cancel", "Cancel" },
            {"NotAuth", "You are not an admin." },
            {"VIPGift", "Make this a VIP only gift?" },
            {"Yes", "Yes" },
            {"No", "No" },
            {"Redeem", "Redeem" },
            {"CompletedNewGift", "You have been given a new gift for the {0} Minute PlayTime Objective!" },
            {"AccumulatedTime", "Accumulated Time: {0}" },
            {"NotEnoughSpace", "You do not have enough room to redeem this gift!" },
            {"WaitingImageLibrary", "Waiting on Image Library to initialize. Trying again in 60 Seconds" },
        };
        #endregion
    }
}