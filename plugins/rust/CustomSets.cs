using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using Oxide.Core;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info("CustomSets", "Absolut", "1.0.3", ResourceId = 2425)]
    [Description("An equipment set creation and management system. All Sets are managed through the UI and can be given or sold to players with Economics or ServerRewards")]
    class CustomSets : RustPlugin
    {
        [PluginReference]
        Plugin EventManager, ServerRewards, Economics, ImageLibrary;

        SavedData csData;
        private DynamicConfigFile CSDATA;

        bool ready;
        string TitleColor = "<color=#0045ff>";
        string MsgColor = "<color=#A9A9A9>";
        private List<ulong> SavingCollection = new List<ulong>();
        private Dictionary<string, Timer> timers = new Dictionary<string, Timer>();
        Dictionary<string, List<ulong>> ItemSkins = new Dictionary<string, List<ulong>>();
        private Dictionary<ulong, SetCreation> NewSet = new Dictionary<ulong, SetCreation>();
        class SetCreation
        {
            public string setname;
            public bool Editing;
            public bool UsePermission;
            public Set set = new Set();
        }

        private Dictionary<ulong, screen> CSUIInfo = new Dictionary<ulong, screen>();
        class screen
        {
            public bool open;
            public bool admin;
            public string SelectedCategory = string.Empty;
            public string SelectedSet;
            public int CategoryIndex;
            public int SetIndex;
            public int page;
            public int InspectedGear = -1;
            public string InspectedAttachment = string.Empty;
            public string location = string.Empty;
        }

        void Loaded()
        {
            ready = false;
            CSDATA = Interface.Oxide.DataFileSystem.GetFile("CustomSets_Data");
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                DestroyPlayer(player);
            foreach (var timer in timers)
                timer.Value.Destroy();
            timers.Clear();
            SaveData();
        }

        void OnServerInitialized()
        {
            timer.Once(10, () =>
            {
                if (!ImageLibrary)
                {
                    PrintWarning("Image Library not detected.. This plugin will unload as it will not work", Name);
                    Interface.Oxide.UnloadPlugin(Name);
                    return;
                }
                LoadVariables();
                LoadData();
                InitializeStuff();
                permission.RegisterPermission(this.Title + ".admin", this);
                permission.RegisterPermission(this.Title + ".allow", this);
                foreach (var entry in csData.SavedSets.Where(k => !string.IsNullOrEmpty(k.Value.permission)))
                    permission.RegisterPermission(this.Title + "." + entry.Value.permission.Replace(" ", string.Empty), this);
                timers.Add("info", timer.Once(900, () => InfoLoop()));
                timers.Add("save", timer.Once(600, () => SaveLoop()));
                foreach (BasePlayer p in BasePlayer.activePlayerList)
                    OnPlayerInit(p);
            });
        }


        private void InitializeStuff()
        {
            if (timers.ContainsKey("imageloading"))
            {
                timers["imageloading"].Destroy();
                timers.Remove("imageloading");
            }
            CreateLoadOrder();
            if (!isReady())
            {
                Puts(GetMSG("WaitingImageLibrary"));
                timers.Add("imageloading", timer.Once(60, () => InitializeStuff()));
                return;
            };

            foreach (var itemDef in ItemManager.GetItemDefinitions())
            {
                List<ulong> skins;
                skins = new List<ulong> { 0 };
                skins.AddRange(ItemSkinDirectory.ForItem(itemDef).Select(skin => Convert.ToUInt64(skin.id)));
                List<ulong> templist = GetImageList(itemDef.shortname);
                if (templist != null && templist.Count >= 1)
                    foreach (var entry in templist.Where(k => !skins.Contains(k)))
                        skins.Add(entry);
                ItemSkins.Add(itemDef.shortname, skins);
            }
        }

        private void CreateLoadOrder()
        {
            Dictionary<string, string> newLoadOrder = new Dictionary<string, string>
            {
                { "cs_selectedbutton", "http://i.imgur.com/tfpKLAQ.png"},
                { "cs_unselectedbutton", "http://i.imgur.com/i3XLvJb.png"},
                { "cs_attachmentoverlay", "http://i.imgur.com/Icl5BaB.png"},
                { "cs_next", "http://i.imgur.com/F4FnvVT.png"},
                { "cs_prior", "http://i.imgur.com/SP93UQj.png"},
                { "cs_smallbackground", "http://i.imgur.com/fIetbic.png"},
                { "cs_remove", "http://i.imgur.com/7D6cGeu.png"},
                { "cs_back", "http://i.imgur.com/Ymexrbe.jpg"},
                { "cs_background", "http://i.imgur.com/1sJtIBF.png"}
            };
            ImageLibrary.Call("ImportImageList", Title, newLoadOrder, (ulong)ResourceId, true);
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (player != null)
            {
                string key = String.Empty;
                //if (!string.IsNullOrEmpty(configData.MenuKeyBinding))
                //{
                //    player.Command($"bind {configData.MenuKeyBinding} \"ToggleCSUI\"");
                //    key = GetMSG("CSAltInfo", player, configData.MenuKeyBinding.ToUpper());
                //}
                if (configData.InfoInterval != 0)
                    GetSendMSG(player, "CSInfo", key);
            }
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            DestroyUI(player);
            if (EventManager)
            {
                object isPlaying = EventManager?.Call("isPlaying", new object[] { player });
                if (isPlaying is bool)
                    if ((bool)isPlaying)
                        return;
            }
            foreach (var entry in configData.SpawnSets.OrderBy(k => k.Key))
                if (csData.SavedSets.ContainsKey(entry.Value) && (string.IsNullOrEmpty(csData.SavedSets[entry.Value].permission) || permission.UserHasPermission(player.UserIDString, this.Title + "." + csData.SavedSets[entry.Value].permission.Replace(" ", string.Empty))))
                {
                    var setData = csData.SavedSets[entry.Value];
                    if (setData.cooldown != 0 || setData.dailyUses != 0 && csData.PlayerData.ContainsKey(player.userID))
                    {
                        var data = csData.PlayerData[player.userID];
                        CoolandUse info = null;
                        data.cooldownANDuses.TryGetValue(entry.Value, out info);
                        if (info != null)
                        {
                            if (info.CooldownExpiration < CurrentTotalMinutes()) info.CooldownExpiration = 0;
                            if (CurrentTotalMinutes() > info.FirstUse + (configData.UsesResetInterval_InHours * 60 * 60))
                            {
                                info.timesUsed = 0;
                                info.FirstUse = 0;
                            }
                            if (info.CooldownExpiration != 0 || setData.dailyUses - info.timesUsed == 0) continue;
                        }
                    }
                    player.inventory.Strip();
                    ProcessSelection(player, entry.Value);
                    return;
                }
        }

        private void SRAction(ulong ID, int amount, string action)
        {
            if (action == "ADD")
                ServerRewards?.Call("AddPoints", new object[] { ID, amount });
            if (action == "REMOVE")
                ServerRewards?.Call("TakePoints", new object[] { ID, amount });
        }

        private void ECOAction(ulong ID, int amount, string action)
        {
            if (action == "ADD")
                Economics.Call("DepositS", ID.ToString(), amount);
            if (action == "REMOVE")
                Economics.Call("WithdrawS", ID.ToString(), amount);
        }

        object OnServerCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player != null && SavingCollection.Contains(player.userID) && arg.cmd?.FullName == "chat.say" && !string.IsNullOrEmpty(string.Join(" ", arg.Args)) && !arg.GetString(0).StartsWith("/"))
            {
                CollectionCreationChat(player, string.Join(" ", arg.Args));
                return false;
            }
            return null;
        }

        private void CollectionCreationChat(BasePlayer player, string Args)
        {
            if (Args.Contains("quit"))
            {
                ExitSetCreation(player);
                return;
            }
            if (csData.SavedSets.ContainsKey(Args))
            {
                GetSendMSG(player, "NameTaken", Args);
                return;
            }
            NewSet[player.userID].setname = Args;
            SaveCollection(player);
        }

        private string TryForImage(string shortname, ulong skin = 99)
        {
            if (shortname.Contains("http")) return shortname;
            if (skin == 99) skin = (ulong)ResourceId;
            return GetImage(shortname, skin, true);
        }

        public string GetImage(string shortname, ulong skin = 0, bool returnUrl = false) => (string)ImageLibrary.Call("GetImage", shortname.ToLower(), skin, returnUrl);
        public bool HasImage(string shortname, ulong skin = 0) => (bool)ImageLibrary.Call("HasImage", shortname.ToLower(), skin);
        public bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname.ToLower(), skin);
        public List<ulong> GetImageList(string shortname) => (List<ulong>)ImageLibrary.Call("GetImageList", shortname.ToLower());
        public bool isReady() => (bool)ImageLibrary?.Call("IsReady");
        private object CheckPoints(ulong ID) => ServerRewards?.Call("CheckPoints", ID);

        void DestroyPlayer(BasePlayer player)
        {
            if (player == null) return;
            {
                DestroyUI(player);
                //if (!string.IsNullOrEmpty(configData.MenuKeyBinding))
                //    player.Command($"bind {configData.MenuKeyBinding} \"\"");
                CSUIInfo.Remove(player.userID);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            DestroyPlayer(player);
            SaveData();
        }

        private void GetSendMSG(BasePlayer player, string message, string arg1 = "", string arg2 = "", string arg3 = "")
        {
            string msg = string.Format(lang.GetMessage(message, this, player.UserIDString), arg1, arg2, arg3);
            SendReply(player, TitleColor + lang.GetMessage("title", this, player.UserIDString) + "</color>" + MsgColor + msg + "</color>");
        }

        private string GetMSG(string message, BasePlayer player = null, string arg1 = "", string arg2 = "", string arg3 = "")
        {
            string p = null;
            if (player != null)
                p = player.UserIDString;
            if (messages.ContainsKey(message))
                return string.Format(lang.GetMessage(message, this, p), arg1, arg2, arg3);
            else return message;
        }

        [ChatCommand("sets")]
        private void chatauction(BasePlayer player, string cmd, string[] args)
        {
            if (player == null) return;
            if (!isAuth(player) && !permission.UserHasPermission(player.UserIDString, this.Title + ".allow"))
            {
                GetSendMSG(player, "NoPerm");
                return;
            }
            ToggleCSUI(player);
        }


        [ConsoleCommand("ToggleCSUI")]
        private void cmdToggleCSUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (!isAuth(player) && !permission.UserHasPermission(player.UserIDString, this.Title + ".allow"))
            {
                GetSendMSG(player, "NoPerm");
                return;
            }
            if (arg.Args != null && arg.Args.Length > 0 && arg.Args[0] == "close")
            {
                CuiHelper.DestroyUi(player, BackgroundPanel);
                DestroySetPanels(player);
                return;
            }
            if (!ImageLibrary || !isReady())
            {
                GetSendMSG(player, "ImagesLoading");
                return;
            }
            else if (CSUIInfo.ContainsKey(player.userID))
                if (!CSUIInfo[player.userID].open)
                {
                    CSUIInfo[player.userID].open = true;
                    ToggleCSUI(player);
                }
                else
                {
                    CuiHelper.DestroyUi(player, BackgroundPanel);
                    DestroySetPanels(player);
                }
            else
                ToggleCSUI(player);
        }

        private void ToggleCSUI(BasePlayer player)
        {
            if (!CSUIInfo.ContainsKey(player.userID))
                CSUIInfo.Add(player.userID, new screen());
            CSUIInfo[player.userID].open = true;
            UIPanel(player);
        }


        public void DestroyUI(BasePlayer player)
        {
            DestroySetPanels(player);
            CuiHelper.DestroyUi(player, BackgroundPanel);
            CuiHelper.DestroyUi(player, PanelOnScreen);
            CuiHelper.DestroyUi(player, PanelMisc);
        }

        public void DestroySetPanels(BasePlayer player)
        {
            if (CSUIInfo.ContainsKey(player.userID))
                CSUIInfo[player.userID].open = false;
            CuiHelper.DestroyUi(player, SetContentsPanel);
            CuiHelper.DestroyUi(player, PanelInspector);
            CuiHelper.DestroyUi(player, SetIndexPanel);
            CuiHelper.DestroyUi(player, PanelMisc);
        }

        public void Broadcast(string message, string userid = "0") => PrintToChat(message);

        private void SaveCollection(BasePlayer player)
        {
            var name = NewSet[player.userID].setname;
            if (NewSet[player.userID].UsePermission)
            {
                NewSet[player.userID].set.permission = name.Replace(" ", string.Empty);
                permission.RegisterPermission(this.Title + "." + name, this);
            }
            if (csData.SavedSets.ContainsKey(name))
                csData.SavedSets.Remove(name);
            csData.SavedSets.Add(name, NewSet[player.userID].set);
            NewSet.Remove(player.userID);
            SavingCollection.Remove(player.userID);
            GetSendMSG(player, "NewSetCreated", name);
            CuiHelper.DestroyUi(player, PanelMisc);
            SaveData();
            CSUIInfo[player.userID].SelectedSet = string.Empty;
            ToggleCSUI(player);
        }


        private void ExitSetCreation(BasePlayer player)
        {
            if (SavingCollection.Contains(player.userID))
                SavingCollection.Remove(player.userID);
            SetPanel(player);
        }

        bool isAuth(BasePlayer player)
        {
            if (player.net.connection != null)
                if (player.net.connection.authLevel < 2 && !permission.UserHasPermission(player.UserIDString, this.Title + ".admin"))
                    return false;
            return true;
        }

        private string BackgroundPanel = "BackgroundPanel";
        private string SetIndexPanel = "SetIndexPanel";
        private string SetContentsPanel = "SetContentsPanel";
        private string PanelInspector = "PanelInspector";
        private string PanelOnScreen = "OnScreen";
        private string PanelMisc = "PanelMisc";

        public class UI
        {
            static public CuiElementContainer CreateElementContainer(string panelName, string color, string aMin, string aMax, bool cursor = false)
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
                    new CuiElement().Parent,
                    panelName
                }
            };
                return NewElement;
            }
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

            static public void CreatePanel(ref CuiElementContainer container, string panel, string color, string aMin, string aMax, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    CursorEnabled = cursor
                },
                panel);
            }
            static public void CreateLabel(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, FadeIn = 1.0f, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);
            }

            static public void CreateButton(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiButton
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
        }

        private Dictionary<string, string> UIColors = new Dictionary<string, string>
        {
            {"black", "0 0 0 1.0" },
            {"dark", "0.1 0.1 0.1 0.98" },
            {"header", "0.7 0.7 0.7 0.15" },
            {"background", "0 0 0 1.0" },
            {"white", "1 1 1 1" },
            {"grey", "0.7 0.7 0.7 1.0" },
            {"buttonblue", "0.05 0.08 0.176 1.0" },
            {"buttonbg", "0.4 0.4 0.4 0.5" },
            {"buttongreen", "0.133 0.965 0.133 0.9" },
            {"buttonred", "0.964 0.133 0.133 0.9" },
            {"buttongrey", "0.8 0.8 0.8 0.9" },
            {"SpecialOrange", "0.956 0.388 0.03 1.0" }
        };


        void UIPanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, BackgroundPanel);
            var element = UI.CreateOverlayContainer(BackgroundPanel, "0 0 0 0", "0.1 0.05", "0.9 0.97", true);
            //UI.CreatePanel(ref element, BackgroundPanel, UIColors["header"], "0.005 0.01", "0.995 0.99");
            UI.LoadImage(ref element, BackgroundPanel, TryForImage("CS_Background"), "0 0", "1 1");
            if (CSUIInfo.ContainsKey(player.userID))
                CSUIInfo[player.userID].open = true;
            if (configData.UsePayment)
            {
                string money = ServerRewards && CheckPoints(player.userID) is int ? CheckPoints(player.userID).ToString() : Economics ? Economics.CallHook("GetPlayerMoney", player.userID).ToString() : "0";
                UI.CreateTextOutline(ref element, BackgroundPanel, UIColors["white"], UIColors["black"], money, 26, "0.8 0.92", "0.98 0.97", TextAnchor.LowerLeft);
            }
            UI.CreateButton(ref element, BackgroundPanel, "0 0 0 0", "", 16, "0.95 0.92", ".99 .98", "ToggleCSUI close");
            CuiHelper.AddUi(player, element);
            SetListPanel(player);
            SetPanel(player);
        }

        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static double CurrentTotalMinutes() => DateTime.UtcNow.Subtract(Epoch).TotalMinutes;
        private string GetMinutesFormat(double minutes)
        {
            TimeSpan dateDifference = TimeSpan.FromMinutes(minutes);
            var hours = dateDifference.Hours;
            hours += (dateDifference.Days * 24);
            return string.Format("{0:00}:{1:00}:{2:00}", hours, dateDifference.Minutes, dateDifference.Seconds);
        }

        void SetListPanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, SetIndexPanel);
            var element = UI.CreateOverlayContainer(SetIndexPanel, "0 0 0 0", "0.11 0.1", "0.32 0.95");
            UI.CreateTextOutline(ref element, SetIndexPanel, UIColors["white"], UIColors["black"], GetMSG("ListOFSets", player), 22, "0.1 0.91", "0.9 0.985", TextAnchor.UpperCenter);
            var uiInfo = CSUIInfo[player.userID];
            if (string.IsNullOrEmpty(uiInfo.SelectedCategory))
            {
                //UI.CreateTextOutline(ref element, SetIndexPanel, UIColors["white"], UIColors["black"], GetMSG("SetCategories", player), 16, "0.1 0.85", "0.9 0.9", TextAnchor.MiddleCenter);
                if (configData.Categories.Count() >= 1)
                {
                    if (CSUIInfo[player.userID].CategoryIndex + 10 < configData.Categories.Count())
                    {
                        UI.LoadImage(ref element, SetIndexPanel, TryForImage("CS_down"), "0.3 0.02", "0.45 0.07");
                        UI.CreateButton(ref element, SetIndexPanel, "0 0 0 0", "", 12, "0.3 0.02", "0.45 0.07", $"UI_CategoryIndexShownChange {CSUIInfo[player.userID].CategoryIndex + 1}");
                    }
                    if (CSUIInfo[player.userID].CategoryIndex != 0)
                    {
                        UI.LoadImage(ref element, SetIndexPanel, TryForImage("CS_up"), "0.55 0.02", "0.7 0.07");
                        UI.CreateButton(ref element, SetIndexPanel, "0 0 0 0", "", 12, "0.55 0.02", "0.7 0.07", $"UI_CategoryIndexShownChange {CSUIInfo[player.userID].CategoryIndex - 1}");
                    }
                    var i = -1;
                    foreach (var entry in configData.Categories)
                    {
                        i++;
                        if (i < CSUIInfo[player.userID].CategoryIndex) continue;
                        if (i > CSUIInfo[player.userID].CategoryIndex + 10) continue;
                        var pos = CalcSetButtons(i - CSUIInfo[player.userID].CategoryIndex);
                        UI.LoadImage(ref element, SetIndexPanel, TryForImage("CS_UnselectedButton"), $"{ pos[0]} {pos[1]}", $"{pos[2] - .04f} {pos[3]}");
                        UI.CreateLabel(ref element, SetIndexPanel, UIColors["white"], entry, 24, $"{pos[0] + .15f} {pos[1]}", $"{pos[2]} {pos[3]}", TextAnchor.MiddleLeft);
                        UI.CreateButton(ref element, SetIndexPanel, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_ChangeCategory {entry}", TextAnchor.MiddleLeft);
                    }
                }
            }
            else
            {
                UI.CreateLabel(ref element, SetIndexPanel, UIColors["SpecialOrange"], GetMSG("Category", player, uiInfo.SelectedCategory), 16, "0.14 0.9", "0.75 0.97", TextAnchor.LowerLeft);
                UI.LoadImage(ref element, SetIndexPanel, TryForImage("CS_Back"), "0.75 0.9", "0.95 0.93");
                UI.CreateLabel(ref element, SetIndexPanel, UIColors["white"], GetMSG("Back"), 10, "0.84 0.9", "0.95 0.93", TextAnchor.MiddleLeft);
                UI.CreateButton(ref element, SetIndexPanel, "0 0 0 0", "", 12, "0.75 0.9", "0.95 0.93", $"UI_ChangeCategory BACK");
                List<string> sets = new List<string>();
                if (CSUIInfo[player.userID].admin)
                    sets = csData.SavedSets.Where(k => k.Value.Category == uiInfo.SelectedCategory).Select(k => k.Key).ToList();
                else if (!configData.ShowLockedSets)
                    sets = csData.SavedSets.Where(k => k.Value.Category == uiInfo.SelectedCategory && !k.Value.hidden && (string.IsNullOrEmpty(k.Value.permission) || permission.UserHasPermission(player.UserIDString, this.Title + "." + k.Value.permission.Replace(" ", string.Empty)))).Select(k => k.Key).ToList();
                else sets = csData.SavedSets.Where(k => k.Value.Category == uiInfo.SelectedCategory && !k.Value.hidden).Select(k => k.Key).ToList();
                if (sets.Count() >= 1)
                {
                    if (CSUIInfo[player.userID].SetIndex + 10 < sets.Count())
                    {
                        UI.LoadImage(ref element, SetIndexPanel, TryForImage("CS_Prior"), "0.135 -0.01", "0.58 0.035");
                        UI.CreateButton(ref element, SetIndexPanel, "0 0 0 0", "", 12, "0.135 -0.01", "0.58 0.035", $"UI_GearIndexShownChange {CSUIInfo[player.userID].SetIndex + 1}");
                    }
                    if (CSUIInfo[player.userID].SetIndex != 0)
                    {
                        UI.LoadImage(ref element, SetIndexPanel, TryForImage("CS_Next"), "0.58 -0.01", "1.01 0.035");
                        UI.CreateButton(ref element, SetIndexPanel, "0 0 0 0", "", 12, "0.58 -0.01", "1.01 0.035", $"UI_GearIndexShownChange {CSUIInfo[player.userID].SetIndex - 1}");
                    }
                    var i = -1;
                    foreach (var entry in sets)
                    {
                        i++;
                        if (i < CSUIInfo[player.userID].SetIndex) continue;
                        if (i > CSUIInfo[player.userID].SetIndex + 10) continue;
                        var pos = CalcSetButtons(i - CSUIInfo[player.userID].SetIndex);
                        if (CSUIInfo[player.userID].SelectedSet == entry) UI.LoadImage(ref element, SetIndexPanel, TryForImage("CS_SelectedButton"), $"{ pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                        else UI.LoadImage(ref element, SetIndexPanel, TryForImage("CS_UnselectedButton"), $"{ pos[0]} {pos[1]}", $"{pos[2] - .04f} {pos[3]}");
                        UI.CreateLabel(ref element, SetIndexPanel, UIColors["white"], entry, 24, $"{pos[0] + .15f} {pos[1]}", $"{pos[2]} {pos[3]}", TextAnchor.MiddleLeft);
                        //if (!string.IsNullOrEmpty(csData.SavedSets[entry].permission) && !permission.UserHasPermission(player.UserIDString, this.Title + "." + csData.SavedSets[entry].permission.Replace(" ", string.Empty)))
                        //    UI.LoadImage(ref element, SetIndexPanel, TryForImage("CS_lock"), $"{pos[0] + .02f} {pos[1]}", $"{pos[0] + 0.18f} {pos[3]}");
                        UI.CreateButton(ref element, SetIndexPanel, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_ChangeSet {entry}", TextAnchor.MiddleLeft);
                    }
                }
            }
            CuiHelper.AddUi(player, element);
        }

        private float[] CalcSetButtons(int number)
        {
            Vector2 position = new Vector2(0.13f, 0.83f);
            Vector2 dimensions = new Vector2(0.88f, 0.05f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 15)
            {
                offsetY = (-0.005f - dimensions.y) * number;
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        void SetPanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelMisc);
            CuiHelper.DestroyUi(player, SetContentsPanel);
            CuiHelper.DestroyUi(player, PanelInspector);
            var element = UI.CreateOverlayContainer(SetContentsPanel, "0 0 0 0", "0.33 0.06", "0.7 0.86");
            UI.CreateTextOutline(ref element, SetContentsPanel, UIColors["white"], UIColors["black"], GetMSG("INFO", player), 22, "1.01 0.94", "1.42 1");
            UI.CreateTextOutline(ref element, SetContentsPanel, UIColors["white"], UIColors["black"], GetMSG("Container", player, "HOTBAR"), 20, "0.15 0.71", "0.79 0.84", TextAnchor.MiddleLeft);
            UI.CreateTextOutline(ref element, SetContentsPanel, UIColors["white"], UIColors["black"], GetMSG("Container", player, "INVENTORY"), 20, "0.15 0.55", "0.9 0.6", TextAnchor.MiddleLeft);
            UI.CreateTextOutline(ref element, SetContentsPanel, UIColors["white"], UIColors["black"], GetMSG("Container", player, "WEAR"), 20, "0.15 0.95", "0.9 0.99", TextAnchor.MiddleLeft);
            if (isAuth(player))
                if (CSUIInfo[player.userID].admin) UI.CreateButton(ref element, SetContentsPanel, UIColors["SpecialOrange"], $"<color=black>{GetMSG("ToggleAdminView", player)}</color>", 12, "1.55 1.05", "1.7 1.1", "UI_SwitchAdminView");
                else UI.CreateButton(ref element, SetContentsPanel, UIColors["black"], $"<color=#F56201>{GetMSG("ToggleAdminView", player)}</color>", 12, "1.55 1.05", "1.7 1.1", "UI_SwitchAdminView");
            if (!string.IsNullOrEmpty(CSUIInfo[player.userID].SelectedCategory))
            {
                if (NewSet.ContainsKey(player.userID) && CSUIInfo[player.userID].admin && (!NewSet[player.userID].Editing || (NewSet[player.userID].Editing && NewSet[player.userID].setname == CSUIInfo[player.userID].SelectedSet)))
                {
                    foreach (var entry in new Dictionary<string, List<Gear>> { { "belt", NewSet[player.userID].set.belt }, { "wear", NewSet[player.userID].set.wear }, { "main", NewSet[player.userID].set.main } })
                        GearEntries(player, element, entry.Value, entry.Key);
                    if (NewSet[player.userID].Editing)
                    {
                        if (NewSet[player.userID].setname == CSUIInfo[player.userID].SelectedSet)
                        {
                            var info = NewSet[player.userID].set;

                            UI.CreateButton(ref element, SetContentsPanel, UIColors["black"], $"<color=#F56201>{GetMSG("Cost", player, info.cost.ToString())}</color>", 10, "1.55 .93", "1.7 .98", $"UI_EditSetCost");

                            UI.CreateLabel(ref element, SetContentsPanel, UIColors["white"], GetMSG("DailyUses", player, info.dailyUses <= 0 ? GetMSG("Unlimited") : $"<color=green>{info.dailyUses}</color>/{info.dailyUses}"), 22, "1.03 0.86", "1.42 0.94", TextAnchor.MiddleLeft);
                            UI.CreateButton(ref element, SetContentsPanel, UIColors["black"], $"<color=#F56201>{GetMSG("Uses", player)}</color>", 10, "1.55 0.87", "1.7 0.92", $"UI_EditSetUses");

                            UI.CreateLabel(ref element, SetContentsPanel, UIColors["white"], GetMSG("Cooldown", player, info.cooldown <= 0 ? GetMSG("None", player) : GetMinutesFormat(info.cooldown).ToString()), 22, "1.03 0.78", "1.42 0.86", TextAnchor.MiddleLeft);
                            UI.CreateButton(ref element, SetContentsPanel, UIColors["black"], $"<color=#F56201>{GetMSG("CooldownTitle", player)}</color>", 10, "1.55 0.81", "1.7 0.86", $"UI_EditSetCooldown");



                            UI.CreateButton(ref element, SetContentsPanel, UIColors["black"], $"<color=#F56201>{GetMSG("Hidden", player, info.hidden.ToString())}</color>", 10, "1.55 .75", "1.7 .8", $"UI_HiddenSet");

                            UI.CreateButton(ref element, SetContentsPanel, UIColors["black"], $"<color=#F56201>{GetMSG("Permission", player, string.IsNullOrEmpty(info.permission).ToString())}</color>", 10, "1.55 .69", "1.7 .74", $"UI_Permission");

                            UI.CreateButton(ref element, SetContentsPanel, UIColors["black"], $"<color=#F56201>{GetMSG("ExitSetEditing", player)}</color>", 10, "1.55 .99", "1.7 1.04", $"UI_CancelSetCreation");
                        }
                    }
                    else
                    {
                            UI.CreateButton(ref element, SetContentsPanel, UIColors["black"], $"<color=#F56201>{GetMSG("CancelSet", player)}</color>", 10, "1.55 .99", "1.7 1.04", $"UI_CancelSetCreation");
                            UI.CreateButton(ref element, SetContentsPanel, "0 0 0 0", GetMSG("SaveSet"), 18, "1.01 0.04", "1.42 0.14", $"UI_SaveCollect");
                    }
                }
                else if (string.IsNullOrEmpty(CSUIInfo[player.userID].SelectedSet) && CSUIInfo[player.userID].admin)
                    UI.CreateButton(ref element, SetContentsPanel, UIColors["black"], $"<color=#F56201>{GetMSG("CreateSet", player)}</color>", 10, "1.55 .99", "1.7 1.04", $"UI_CreateGearSet");

                else if (!string.IsNullOrEmpty(CSUIInfo[player.userID].SelectedSet))
                {
                    var setname = CSUIInfo[player.userID].SelectedSet;
                    var money = 0;
                    if (ServerRewards && CheckPoints(player.userID) is int)
                        money = (int)CheckPoints(player.userID);
                    else if (Economics)
                        money = Convert.ToInt32(Economics.CallHook("GetPlayerMoney", player.userID));
                    if (string.IsNullOrEmpty(setname) || !csData.SavedSets.ContainsKey(setname)) return;
                    bool ready = true;
                    bool printed = false;
                    var set = csData.SavedSets[setname];
                    foreach (var entry in new Dictionary<string, List<Gear>> { { "belt", set.belt }, { "wear", set.wear }, { "main", set.main } })
                    {
                        GearEntries(player, element, entry.Value, entry.Key);
                    }
                    if ((set.cooldown != 0 || set.dailyUses != 0) && csData.PlayerData.ContainsKey(player.userID))
                    {
                        var data = csData.PlayerData[player.userID];
                        CoolandUse info = null;
                        data.cooldownANDuses.TryGetValue(setname, out info);
                        if (info != null)
                        {
                            printed = true;
                            if (info.CooldownExpiration < CurrentTotalMinutes()) info.CooldownExpiration = 0;
                            if (CurrentTotalMinutes() > info.FirstUse + (configData.UsesResetInterval_InHours * 60 * 60))
                            {
                                info.timesUsed = 0;
                                info.FirstUse = 0;
                            }
                            UI.CreateLabel(ref element, SetContentsPanel, UIColors["white"], GetMSG("DailyUses", player, set.dailyUses <= 0 ? GetMSG("Unlimited") : $"<color=red>{set.dailyUses - info.timesUsed}</color>/{set.dailyUses}"), 22, "1.03 0.86", "1.42 0.94", TextAnchor.MiddleLeft);
                            UI.CreateLabel(ref element, SetContentsPanel, UIColors["white"], GetMSG("Cooldown", player, set.cooldown <= 0 ? GetMSG("None", player) : info.CooldownExpiration != 0 ? "<color=red> -" + GetMinutesFormat(info.CooldownExpiration - CurrentTotalMinutes()) + "</color>" : GetMinutesFormat(set.cooldown).ToString()), 22, "1.03 0.78", "1.42 0.86", TextAnchor.MiddleLeft);

                            if (info.CooldownExpiration != 0 || set.dailyUses - info.timesUsed == 0) ready = false;
                        }
                    }
                    if (!printed)
                    {
                        UI.CreateLabel(ref element, SetContentsPanel, UIColors["white"], GetMSG("DailyUses", player, set.dailyUses <= 0 ? GetMSG("Unlimited") : $"<color=green>{set.dailyUses}</color>/{set.dailyUses}"), 22, "1.03 0.86", "1.42 0.94", TextAnchor.MiddleLeft);
                        UI.CreateLabel(ref element, SetContentsPanel, UIColors["white"], GetMSG("Cooldown", player, set.cooldown <= 0 ? GetMSG("None", player) : GetMinutesFormat(set.cooldown).ToString()), 22, "1.03 0.78", "1.42 0.86", TextAnchor.MiddleLeft);
                    }
                    if (string.IsNullOrEmpty(set.permission) || permission.UserHasPermission(player.UserIDString, this.Title + "." + set.permission.Replace(" ", string.Empty)))
                    {
                        if (ready)
                            UI.CreateButton(ref element, SetContentsPanel, "0 0 0 0", GetMSG("Redeem", player, setname.ToUpper()), 26, "1.01 0.04", "1.42 0.14", $"UI_ProcessSelection {setname}");
                    }
                    else if (configData.UsePayment)
                    {
                        if (money >= set.cost)
                        {
                            UI.CreateButton(ref element, SetContentsPanel, "0 0 0 0", GetMSG("UnlockSet", player, set.cost.ToString()), 22, "1.01 0.04", "1.42 0.14", $"UI_SetPurchase {setname}");
                        }
                        else
                            UI.CreateTextOutline(ref element, SetContentsPanel, UIColors["white"], UIColors["black"], GetMSG("CostOFSet", player, set.cost.ToString()), 24, "1.01 0.04", "1.42 0.14");
                    }
                    if (CSUIInfo[player.userID].admin)
                    {
                        if (!NewSet.ContainsKey(player.userID) || (NewSet[player.userID].Editing && NewSet[player.userID].setname != CSUIInfo[player.userID].SelectedSet))
                        {
                            UI.CreateButton(ref element, SetContentsPanel, UIColors["black"], $"<color=#F56201>{GetMSG("CreateSet", player)}</color>", 10, "1.55 .99", "1.7 1.04", $"UI_CreateGearSet");
                            UI.CreateButton(ref element, SetContentsPanel, UIColors["black"], $"<color=#F56201>{GetMSG("Edit", player)}</color>", 14, "1.55 0.93", "1.7 0.98", $"UI_EditGearSet");
                            UI.CreateButton(ref element, SetContentsPanel, UIColors["black"], $"<color=#F56201>{GetMSG("Delete", player)}</color>", 14, "1.55 0.87", "1.7 0.92", $"UI_DeleteGearSet");
                        }
                    }
                }
            }
            CuiHelper.AddUi(player, element);
            InspectorPanel(player);
        }

        private void GearEntries(BasePlayer player, CuiElementContainer element, List<Gear> list, string location)
        {
            float[] pos;
            var i = 0;
            foreach (var item in list)
            {
                pos = MainLocation(i);
                if (location == "belt") pos = BeltLocation(i);
                if (location == "wear") pos = WearLocation(i);
                UI.LoadImage(ref element, SetContentsPanel, TryForImage(item.shortname, item.skin), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                if (item.attachments != null && item.attachments.Count > 0)
                    UI.LoadImage(ref element, SetContentsPanel, TryForImage("CS_AttachmentOverlay"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                UI.CreateButton(ref element, SetContentsPanel, "0 0 0 0", "", 16, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_ChangeSelection {i} {location}", TextAnchor.MiddleCenter);
                i++;
            }
        }
        private void InspectorPanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelInspector);
            if (!CSUIInfo.ContainsKey(player.userID)) return;
            CuiElementContainer element = null;
            var i = 0;
            var money = 0;
            if (ServerRewards && CheckPoints(player.userID) is int)
                money = (int)CheckPoints(player.userID);
            else if (Economics)
                money = Convert.ToInt32(Economics.CallHook("GetPlayerMoney", player.userID));
            string set;
            element = UI.CreateOverlayContainer(PanelInspector, "0 0 0 0", "0.705 0.2", "0.86 0.68");
            UI.CreateTextOutline(ref element, PanelInspector, UIColors["white"], UIColors["black"], GetMSG("ITEM", player), 22, "0 0.91", "1 .99");
            if (CSUIInfo[player.userID].InspectedGear != -1 && !string.IsNullOrEmpty(CSUIInfo[player.userID].location))
                if (NewSet.ContainsKey(player.userID) && CSUIInfo[player.userID].admin)
                {
                    var cont = NewSet[player.userID].set.main;
                    if (CSUIInfo[player.userID].location == "belt")
                        cont = NewSet[player.userID].set.belt;
                    else if (CSUIInfo[player.userID].location == "wear")
                        cont = NewSet[player.userID].set.wear;
                    if (cont[CSUIInfo[player.userID].InspectedGear] == null) return;
                    var item = cont[CSUIInfo[player.userID].InspectedGear];
                    UI.LoadImage(ref element, PanelInspector, TryForImage(item.shortname, item.skin), $".05 .24", $".95 .76");
                    if (item.attachments.Count == 0 || string.IsNullOrEmpty(CSUIInfo[player.userID].InspectedAttachment))
                    {
                        UI.CreateTextOutline(ref element, PanelInspector, UIColors["white"], UIColors["black"], item.displayName, 18, "0 0.81", "1 .89");
                        if (ItemSkins.ContainsKey(item.shortname) && ItemSkins[item.shortname].Count() > 1)
                            UI.CreateButton(ref element, PanelInspector, UIColors["black"], $"<color=#F56201>{GetMSG("SetSkin", player)}</color>", 12, $".27 .77", $".73 .81", $"UI_AddItemAttributes");
                    }
                    if (item.attachments.Count > 0)
                    {
                        UI.CreateTextOutline(ref element, PanelInspector, UIColors["white"], UIColors["black"], GetMSG("ATTACHMENTS"), 16, "0 0.15", "1 .21", TextAnchor.UpperCenter);
                        foreach (var entry in item.attachments)
                        {
                            var pos = AttachmentInspectorPos(i);
                            UI.LoadImage(ref element, PanelInspector, TryForImage(entry, 0), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                            if (CSUIInfo[player.userID].InspectedAttachment == entry)
                            {
                                UI.CreateTextOutline(ref element, PanelInspector, UIColors["white"], UIColors["black"], GetDisplayNameFromSN(entry), 18, "0 0.81", "1 .89");
                                UI.CreateButton(ref element, PanelInspector, "0 0 0 0", "", 12, $".08 .3", $".92 .8", $"UI_ChangeSelection clear");
                            }
                            else
                                UI.CreateButton(ref element, PanelInspector, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_ChangeSelection {entry}");
                            i++;
                        }
                    }
                    UI.LoadImage(ref element, PanelInspector, TryForImage("CS_Remove"), "0.85 0.83", ".97 .91");
                    UI.CreateButton(ref element, PanelInspector, "0 0 0 0", "", 12, "0.85 0.83", ".97 .91", $"UI_RemoveItem");
                }
                else
                {
                    set = CSUIInfo[player.userID].SelectedSet;
                    var cont = csData.SavedSets[set].main;
                    if (CSUIInfo[player.userID].location == "belt")
                        cont = csData.SavedSets[set].belt;
                    else if (CSUIInfo[player.userID].location == "wear")
                        cont = csData.SavedSets[set].wear;

                    if (cont[CSUIInfo[player.userID].InspectedGear] == null) return;
                    var item = cont[CSUIInfo[player.userID].InspectedGear];
                    if (item == null) { CuiHelper.AddUi(player, element); return; }
                    UI.LoadImage(ref element, PanelInspector, TryForImage(item.shortname, item.skin), $".05 .24", $".95 .76");
                    if (item.attachments.Count == 0 || string.IsNullOrEmpty(CSUIInfo[player.userID].InspectedAttachment))
                        UI.CreateTextOutline(ref element, PanelInspector, UIColors["white"], UIColors["black"], item.displayName, 18, "0 0.81", "1 .89");
                    if (item.attachments.Count > 0)
                    {
                        UI.CreateTextOutline(ref element, PanelInspector, UIColors["white"], UIColors["black"], GetMSG("ATTACHMENTS"), 16, "0 0.15", "1 .21", TextAnchor.UpperCenter);
                        foreach (var entry in item.attachments)
                        {
                            var pos = AttachmentInspectorPos(i);
                            UI.LoadImage(ref element, PanelInspector, TryForImage(entry, 0), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                            if (CSUIInfo[player.userID].InspectedAttachment == entry)
                            {
                                UI.CreateTextOutline(ref element, PanelInspector, UIColors["white"], UIColors["black"], GetDisplayNameFromSN(entry), 18, "0 0.81", "1 .89");
                                UI.CreateButton(ref element, PanelInspector, "0 0 0 0", "", 12, $".08 .3", $".92 .8", $"UI_ChangeSelection clear");
                            }
                            else
                                UI.CreateButton(ref element, PanelInspector, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_ChangeSelection {entry}");
                            i++;
                        }
                    }
                }
            CuiHelper.AddUi(player, element);
        }


        string GetDisplayNameFromSN(string shortname)
        {
            ItemDefinition def = ItemManager.FindItemDefinition(shortname);
            if (def != null)
                return def.displayName.english;
            else return shortname;
        }


        void OnScreen(BasePlayer player, string msg)
        {
            if (timers.ContainsKey(player.userID.ToString()))
            {
                timers[player.userID.ToString()].Destroy();
                timers.Remove(player.userID.ToString());
            }
            CuiHelper.DestroyUi(player, PanelOnScreen);
            var element = UI.CreateOverlayContainer(PanelOnScreen, "0.0 0.0 0.0 0.0", "0.3 0.35", "0.7 0.65", false);
            UI.LoadImage(ref element, PanelOnScreen, TryForImage("CS_SmallBackground"), "0 0", "1 1");
            UI.CreateTextOutline(ref element, PanelOnScreen, UIColors["white"], UIColors["black"], msg, 32, "0.0 0.0", "1.0 1.0");
            CuiHelper.AddUi(player, element);
            timers.Add(player.userID.ToString(), timer.Once(3, () => CuiHelper.DestroyUi(player, PanelOnScreen)));
        }

        private void SelectIfFree(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelMisc);
            var element = UI.CreateOverlayContainer(PanelMisc, "0 0 0 0", "0.4 0.3", "0.6 0.6");
            UI.LoadImage(ref element, PanelMisc, TryForImage("CS_SmallBackground"), "0 0", "1 1");
            UI.CreateLabel(ref element, PanelMisc, UIColors["white"], GetMSG("UnlockSetFree"), 16, "0.1 0.5", "0.9 .98");
            UI.CreateButton(ref element, PanelMisc, UIColors["buttongreen"], "Yes", 18, "0.2 0.08", "0.475 0.28", $"UI_Free true");
            UI.CreateButton(ref element, PanelMisc, UIColors["buttonred"], "No", 18, "0.525 0.08", "0.8 0.28", $"UI_Free false");
            CuiHelper.AddUi(player, element);
        }

        private void SelectIfPermission(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelMisc);
            var element = UI.CreateOverlayContainer(PanelMisc, "0 0 0 0", "0.4 0.3", "0.6 0.6");
            UI.LoadImage(ref element, PanelMisc, TryForImage("CS_SmallBackground"), "0 0", "1 1");
            UI.CreateLabel(ref element, PanelMisc, UIColors["white"], GetMSG("UseAPermission"), 16, "0.1 0.5", "0.9 .98");
            UI.CreateButton(ref element, PanelMisc, UIColors["black"], $"<color=#F56201>{GetMSG("Yes", player)}</color>", 18, "0.2 0.08", "0.475 0.28", $"UI_Permission true");
            UI.CreateButton(ref element, PanelMisc, UIColors["black"], $"<color=#F56201>{GetMSG("No", player)}</color>", 18, "0.525 0.08", "0.8 0.28", $"UI_Permission false");
            CuiHelper.AddUi(player, element);
        }

        private void SelectIfHidden(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelMisc);
            var element = UI.CreateOverlayContainer(PanelMisc, "0 0 0 0", "0.4 0.3", "0.6 0.6");
            UI.LoadImage(ref element, PanelMisc, TryForImage("CS_SmallBackground"), "0 0", "1 1");
            UI.CreateLabel(ref element, PanelMisc, UIColors["white"], GetMSG("HiddenSet"), 16, "0.1 0.5", "0.9 .98");
            UI.CreateButton(ref element, PanelMisc, UIColors["black"], $"<color=#F56201>{GetMSG("Yes", player)}</color>", 18, "0.2 0.08", "0.475 0.28", $"UI_HiddenSet true");
            UI.CreateButton(ref element, PanelMisc, UIColors["black"], $"<color=#F56201>{GetMSG("No", player)}</color>", 18, "0.525 0.08", "0.8 0.28", $"UI_HiddenSet false");
            CuiHelper.AddUi(player, element);
        }

        private void NumberPad(BasePlayer player, string cmd, string title, bool small = false)
        {
            CuiHelper.DestroyUi(player, PanelMisc);
            var element = UI.CreateOverlayContainer(PanelMisc, "0 0 0 0", "0.35 0.3", "0.65 0.7");
            UI.LoadImage(ref element, PanelMisc, TryForImage("CS_SmallBackground"), "0 0", "1 1");
            UI.CreateLabel(ref element, PanelMisc, UIColors["white"], GetMSG(title, player), 16, "0.1 0.86", "0.9 .99", TextAnchor.UpperCenter);
            var n = 1;
            var i = 0;
            if (small)
            {
                n = 0;
                while (n < 15)
                {
                    CreateNumberPadButton(ref element, PanelMisc, i, n, cmd); i++; n++;
                }
                while (n >= 15 && n < 90)
                {
                    CreateNumberPadButton(ref element, PanelMisc, i, n, cmd); i++; n += 15;
                }
                while (n >= 90 && n < 720)
                {
                    CreateNumberPadButton(ref element, PanelMisc, i, n, cmd); i++; n += 30;
                }
                while (n >= 720 && n <= 1440)
                {
                    CreateNumberPadButton(ref element, PanelMisc, i, n, cmd); i++; n += 60;
                }
            }
            else
            {
                UI.CreateButton(ref element, PanelMisc, UIColors["black"], $"<color=#F56201>{GetMSG("Free", player)}</color>", 12, "0.3 0.04", "0.7 .12", $"{cmd} {0}");
                while (n < 10)
                {
                    CreateNumberPadButton(ref element, PanelMisc, i, n, cmd); i++; n++;
                }
                while (n >= 10 && n < 25)
                {
                    CreateNumberPadButton(ref element, PanelMisc, i, n, cmd); i++; n += 5;
                }
                while (n >= 25 && n < 200)
                {
                    CreateNumberPadButton(ref element, PanelMisc, i, n, cmd); i++; n += 25;
                }
                while (n >= 200 && n <= 950)
                {
                    CreateNumberPadButton(ref element, PanelMisc, i, n, cmd); i++; n += 50;
                }
                while (n >= 1000 && n <= 10000)
                {
                    CreateNumberPadButton(ref element, PanelMisc, i, n, cmd); i++; n += 500;
                }
            }
            CuiHelper.AddUi(player, element);
        }

        private void CreateNumberPadButton(ref CuiElementContainer container, string panelName, int i, int number, string command)
        {
            var pos = CalcNumButtonPos(i);
            UI.CreateButton(ref container, panelName, UIColors["black"], $"<color=#F56201>{number.ToString()}</color>", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"{command} {number}");
        }

        private float[] WearLocation(int number)
        {
            Vector2 position = new Vector2(0.015f, 0.825f);
            Vector2 dimensions = new Vector2(0.15f, 0.12f);
            float offsetY = 0;
            float offsetX = 0;
            offsetX = (0.01f + dimensions.x) * number;
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        private float[] BeltLocation(int number)
        {
            Vector2 position = new Vector2(0.015f, 0.625f);
            Vector2 dimensions = new Vector2(0.15f, 0.12f);
            float offsetY = 0;
            float offsetX = 0;
            offsetX = (0.01f + dimensions.x) * number;
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        private float[] MainLocation(int number)
        {
            Vector2 position = new Vector2(0.015f, 0.425f);
            Vector2 dimensions = new Vector2(0.15f, 0.12f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 6)
            {
                offsetX = (0.01f + dimensions.x) * number;
            }
            if (number > 5 && number < 12)
            {
                offsetX = (0.01f + dimensions.x) * (number - 6);
                offsetY = (-0.011f - dimensions.y) * 1;
            }
            if (number > 11 && number < 18)
            {
                offsetX = (0.01f + dimensions.x) * (number - 12);
                offsetY = (-0.012f - dimensions.y) * 2;
            }
            if (number > 17 && number < 24)
            {
                offsetX = (0.01f + dimensions.x) * (number - 18);
                offsetY = (-0.013f - dimensions.y) * 3;
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        private float[] AttachmentInspectorPos(int number)
        {
            Vector2 position = new Vector2(0.07f, 0.02f);
            Vector2 dimensions = new Vector2(0.25f, 0.13f);
            float offsetY = 0;
            float offsetX = 0;
            offsetX = (0.05f + dimensions.x) * number;
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        private float[] AttachmentMainScreenPos(Vector2 min, int number)
        {
            Vector2 position = min;
            Vector2 dimensions = new Vector2(0.0325f, 0.04f);
            float offsetY = 0;
            float offsetX = 0;
            offsetX = (0.005f + dimensions.x) * number;
            if (number == 2)
            {
                offsetX = (0.02f);
                offsetY = (-0.005f - dimensions.y);
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        private float[] CalcButtonPos(int number)
        {
            Vector2 position = new Vector2(0.03f, 0.75f);
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
                offsetY = (-0.002f - dimensions.y) * 1;
            }
            if (number > 11 && number < 18)
            {
                offsetX = (0.01f + dimensions.x) * (number - 12);
                offsetY = (-0.002f - dimensions.y) * 2;
            }
            if (number > 17 && number < 24)
            {
                offsetX = (0.01f + dimensions.x) * (number - 18);
                offsetY = (-0.002f - dimensions.y) * 3;
            }
            if (number > 23 && number < 30)
            {
                offsetX = (0.01f + dimensions.x) * (number - 24);
                offsetY = (-0.002f - dimensions.y) * 4;
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






        [ConsoleCommand("UI_AddItemAttributes")]
        private void cmdUI_AddGearAttributes(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (!NewSet.ContainsKey(player.userID)) return;
            SelectSkin(player);
        }

        [ConsoleCommand("UI_RemoveItem")]
        private void cmdUI_RemoveItem(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var item = CSUIInfo[player.userID].InspectedGear;
            var cont = NewSet[player.userID].set.main;
            if (CSUIInfo[player.userID].location == "belt")
                cont = NewSet[player.userID].set.belt;
            else if (CSUIInfo[player.userID].location == "wear")
                cont = NewSet[player.userID].set.wear;

            if (string.IsNullOrEmpty(CSUIInfo[player.userID].InspectedAttachment))
            {
                cont.Remove(cont[item]);
                CSUIInfo[player.userID].InspectedGear = -1;
                CSUIInfo[player.userID].InspectedAttachment = string.Empty;
            }
            else
            {
                cont[item].attachments.Remove(CSUIInfo[player.userID].InspectedAttachment);
                CSUIInfo[player.userID].InspectedAttachment = string.Empty;
            }
            SetPanel(player);
        }

        [ConsoleCommand("UI_ChangeSkinPage")]
        private void cmdUI_ChangeSkinPage(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (!NewSet.ContainsKey(player.userID)) return;
            CSUIInfo[player.userID].page = Convert.ToInt32(arg.Args[0]);
            SelectSkin(player);
        }

        private void SelectSkin(BasePlayer player)
        {
            Gear item = null;
            if (CSUIInfo[player.userID].location == "main")
                item = NewSet[player.userID].set.main[CSUIInfo[player.userID].InspectedGear];
            if (CSUIInfo[player.userID].location == "belt")
                item = NewSet[player.userID].set.belt[CSUIInfo[player.userID].InspectedGear];
            else if (CSUIInfo[player.userID].location == "wear")
                item = NewSet[player.userID].set.wear[CSUIInfo[player.userID].InspectedGear];
            if (ItemSkins.ContainsKey(item.shortname) && ItemSkins[item.shortname].Count() > 1)
            {
                CuiHelper.DestroyUi(player, PanelMisc);
                var element = UI.CreateOverlayContainer(PanelMisc, "0 0 0 0", "0.3 0.2", "0.7 0.8");
                UI.LoadImage(ref element, PanelMisc, TryForImage("CS_SmallBackground"), "0 0", "1 1");
                UI.CreateLabel(ref element, PanelMisc, UIColors["white"], GetMSG("SkinTitle", player, item.displayName), 20, "0 .92", "1 1");
                var page = CSUIInfo[player.userID].page;
                var skinlist = ItemSkins[item.shortname];
                int entriesallowed = 30;
                int remainingentries = skinlist.Count - (page * entriesallowed);
                {
                    if (remainingentries > entriesallowed)
                    {
                        UI.CreateButton(ref element, PanelMisc, UIColors["black"], $"<color=#F56201>{GetMSG("Next", player)}</color>", 18, "0.83 0.08", "0.93 0.13", $"UI_ChangeSkinPage {page + 1}");
                    }
                    if (page > 0)
                    {
                        UI.CreateButton(ref element, PanelMisc, UIColors["black"], $"<color=#F56201>{GetMSG("Back", player)}</color>", 18, "0.72 0.08", "0.82 0.13", $"UI_ChangeSkinPage {page - 1}");
                    }
                }
                int shownentries = page * entriesallowed;
                int i = 0;
                int n = 0;
                foreach (var entry in skinlist.Where(k => k != item.skin))
                {
                    i++;
                    if (i < shownentries + 1) continue;
                    else if (i <= shownentries + entriesallowed)
                    {
                        {
                            var pos = CalcButtonPos(n);
                            UI.LoadImage(ref element, PanelMisc, TryForImage(item.shortname, entry), $"{pos[0] + 0.005f} {pos[1] + 0.005f}", $"{pos[2] - 0.005f} {pos[3] - 0.005f}");
                            UI.CreateButton(ref element, PanelMisc, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_SelectSkin {entry}");
                            n++;
                        }
                    }
                    if (n >= entriesallowed)
                        break;
                }
                CuiHelper.AddUi(player, element);
            }
            else
            {
                item.skin = 0;
                SetPanel(player);
            }
        }

        [ConsoleCommand("UI_SetCost")]
        private void cmdUI_SetCollectionCost(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            int cost = Convert.ToInt32(arg.Args[0]);
            NewSet[player.userID].set.cost = cost;
            if (NewSet[player.userID].Editing)
                SetPanel(player);
            else
            {
                NumberPad(player, "UI_SetCooldown", "SetCooldown", true);
            }
        }

        [ConsoleCommand("UI_SetCooldown")]
        private void cmdUI_SetCooldown(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            int amount = Convert.ToInt32(arg.Args[0]);
            NewSet[player.userID].set.cooldown = amount;
            if (NewSet[player.userID].Editing)
                SetPanel(player);
            else
                NumberPad(player, "UI_SetDailyUses", "SetMaxDailyUses", true);
        }

        [ConsoleCommand("UI_SetDailyUses")]
        private void cmdUI_SetDailyUses(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            int amount = Convert.ToInt32(arg.Args[0]);
            NewSet[player.userID].set.dailyUses = amount;
            if (NewSet[player.userID].Editing)
                SetPanel(player);
            else
                SetCollectionName(player);
        }

        void SetCollectionName(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, BackgroundPanel);
            CuiHelper.DestroyUi(player, PanelMisc);
            DestroySetPanels(player);
            var element = UI.CreateOverlayContainer(PanelMisc, "0 0 0 0", "0.4 0.3", "0.6 0.6");
            UI.LoadImage(ref element, PanelMisc, TryForImage("CS_SmallBackground"), "0 0", "1 1");
            UI.CreateLabel(ref element, PanelMisc, UIColors["white"], GetMSG("SetName"), 16, "0.1 0.5", "0.9 .98");
            CuiHelper.AddUi(player, element);
        }

        [ConsoleCommand("UI_EditSetCost")]
        private void cmdUI_EditSetCost(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (NewSet[player.userID].Editing)
                NumberPad(player, "UI_SetCost", "SetCost");
        }

        [ConsoleCommand("UI_EditSetUses")]
        private void cmdUI_EditSetUses(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (NewSet[player.userID].Editing)
                NumberPad(player, "UI_SetDailyUses", "SetMaxDailyUses", true);
        }

        [ConsoleCommand("UI_EditSetCooldown")]
        private void cmdUI_EditSetCooldown(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (NewSet[player.userID].Editing)
                NumberPad(player, "UI_SetCooldown", "SetCooldown", true);
        }



        [ConsoleCommand("UI_Permission")]
        private void cmdUI_Permission(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (NewSet[player.userID].Editing)
            {
                if (string.IsNullOrEmpty(NewSet[player.userID].set.permission))
                    NewSet[player.userID].set.permission = NewSet[player.userID].setname.Replace(" ", string.Empty);
                else
                    NewSet[player.userID].set.permission = string.Empty;
                SetPanel(player);
                OnScreen(player, GetMSG("SettingChangedTo", player, (string.IsNullOrEmpty(NewSet[player.userID].set.permission).ToString().ToUpper())));
            }
            else
            {
                if (arg.Args[0] == "true")
                {
                    NewSet[player.userID].UsePermission = true;
                }
                else
                    NewSet[player.userID].UsePermission = false;
                SelectIfHidden(player);
            }
        }


        [ConsoleCommand("UI_HiddenSet")]
        private void cmdUI_HiddenSet(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (NewSet[player.userID].Editing)
            {
                if (NewSet[player.userID].set.hidden)
                    NewSet[player.userID].set.hidden = false;
                else
                    NewSet[player.userID].set.hidden = true;
                SetPanel(player);
                OnScreen(player, GetMSG("SettingChangedTo", player, NewSet[player.userID].set.hidden.ToString().ToUpper()));
            }
            else
            {
                if (arg.Args[0] == "true")
                    NewSet[player.userID].set.hidden = true;
                else
                {
                    NewSet[player.userID].set.hidden = false;
                    if (configData.UsePayment && NewSet[player.userID].UsePermission)
                    {
                        NumberPad(player, "UI_SetCost", "SetCost");
                        return;
                    }
                }
                NumberPad(player, "UI_SetCooldown", "SetCooldown", true);
            }
        }



        [ConsoleCommand("UI_ChangeCategory")]
        private void cmdUI_ChangeCategory(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var cat = string.Join(" ", arg.Args);
            if (cat == "BACK")
            {
                CSUIInfo[player.userID].SelectedCategory = string.Empty;
                CSUIInfo[player.userID].SelectedSet = string.Empty;
            }
            else
            {
                CSUIInfo[player.userID].SelectedCategory = cat;
                CSUIInfo[player.userID].SetIndex = 0;
            }
            SetListPanel(player);
            if (CSUIInfo[player.userID].admin)
                SetPanel(player);
        }


        [ConsoleCommand("UI_ChangeSet")]
        private void cmdUI_ChangeGearSet(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var set = string.Join(" ", arg.Args);
            CSUIInfo[player.userID].SelectedSet = set;
            CSUIInfo[player.userID].InspectedGear = -1;
            CSUIInfo[player.userID].InspectedAttachment = string.Empty;
            CuiHelper.DestroyUi(player, PanelMisc);
            CuiHelper.DestroyUi(player, PanelInspector);
            SetListPanel(player);
            SetPanel(player);
        }

        [ConsoleCommand("UI_ChangeSelection")]
        private void cmdUI_ChangeSelection(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            int item;
            if (!int.TryParse(arg.Args[0], out item))
            {
                if (arg.Args[0] == "clear")
                {
                    CSUIInfo[player.userID].InspectedAttachment = string.Empty;
                    InspectorPanel(player);
                    return;
                }
                CSUIInfo[player.userID].InspectedAttachment = arg.Args[0];
                InspectorPanel(player);
                return;
            }
            else
            {
                CSUIInfo[player.userID].location = arg.Args[1];
                CSUIInfo[player.userID].InspectedGear = item;
                CSUIInfo[player.userID].InspectedAttachment = string.Empty;
            }
            InspectorPanel(player);
        }

        [ConsoleCommand("UI_SwitchAdminView")]
        private void cmdUI_SwitchAdminView(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (!isAuth(player))
            {
                GetSendMSG(player, "NotAuthorized");
                return;
            }
            if (CSUIInfo[player.userID].admin)
            {
                CSUIInfo[player.userID].admin = false;
                CSUIInfo[player.userID].SetIndex = 0;
            }
            else
            {
                CSUIInfo[player.userID].admin = true;
                CSUIInfo[player.userID].SetIndex = 0;
            }
            UIPanel(player);
            if (!CSUIInfo[player.userID].admin)
                OnScreen(player, GetMSG("ExitAdminView", player));
            else
                OnScreen(player, GetMSG("EnterAdminView", player));
        }

        [ConsoleCommand("UI_CategoryIndexShownChange")]
        private void cmdUI_CategoryIndexShownChange(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var index = Convert.ToInt32(arg.Args[0]);
            CSUIInfo[player.userID].CategoryIndex = index;
            SetListPanel(player);
        }

        [ConsoleCommand("UI_GearIndexShownChange")]
        private void cmdUI_GearIndexShownChange(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var index = Convert.ToInt32(arg.Args[0]);
            CSUIInfo[player.userID].SetIndex = index;
            SetListPanel(player);
        }

        [ConsoleCommand("UI_DestroyPurchaseConfirmation")]
        private void cmdUI_DestroyPurchaseConfirmation(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            CuiHelper.DestroyUi(player, PanelMisc);
        }

        [ConsoleCommand("UI_ProcessSelection")]
        private void cmdUI_ProcessSelection(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var set = string.Join(" ", arg.Args);
            if (!csData.SavedSets.ContainsKey(set)) return;
            ProcessSelection(player, set);
        }

        [ConsoleCommand("UI_SetPurchase")]
        private void cmdUI_SetPurchase(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var setname = string.Join(" ", arg.Args);
            var set = csData.SavedSets[setname];
            CuiHelper.DestroyUi(player, PanelMisc);
            var element = UI.CreateOverlayContainer(PanelMisc, "0 0 0 0", "0.4 0.3", "0.6 0.6");
            UI.LoadImage(ref element, PanelMisc, TryForImage("CS_SmallBackground"), "0 0", "1 1");
            UI.CreateTextOutline(ref element, PanelMisc, UIColors["white"], UIColors["black"], GetMSG("PurchaseSetInfo", player, setname, set.cost.ToString()), 18, "0.1 0.3", "0.9 0.89");
            UI.CreateButton(ref element, PanelMisc, UIColors["black"], $"<color=#F56201>{GetMSG("Yes", player)}</color>", 18, "0.2 0.08", "0.475 0.28", $"UI_ExecutePurchase {setname}");
            UI.CreateButton(ref element, PanelMisc, UIColors["black"], $"<color=#F56201>{GetMSG("No", player)}</color>", 18, "0.525 0.08", "0.8 0.28", $"UI_DestroyPurchaseConfirmation");
            CuiHelper.AddUi(player, element);
        }

        [ConsoleCommand("UI_ExecutePurchase")]
        private void cmdUI_ExecutePurchase(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var set = string.Join(" ", arg.Args);
            if (ServerRewards)
                SRAction(player.userID, csData.SavedSets[set].cost, "REMOVE");
            else if (Economics)
                ECOAction(player.userID, csData.SavedSets[set].cost, "REMOVE");
            else
            {
                GetSendMSG(player, "UnableToPurchase_Payment");
                CuiHelper.DestroyUi(player, PanelMisc);
                return;
            }
            permission.GrantUserPermission(player.UserIDString, this.Title + "." + csData.SavedSets[set].permission.Replace(" ", string.Empty), null);
            DestroySetPanels(player);
            CuiHelper.DestroyUi(player, PanelMisc);
            SetPanel(player);
            OnScreen(player, GetMSG("purchaseset", player, set));
        }

        [ConsoleCommand("UI_SaveCollect")]
        private void cmdUI_SaveCollect(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (NewSet[player.userID].Editing)
            {
                SaveCollection(player);
                return;
            }
            if (!SavingCollection.Contains(player.userID))
                SavingCollection.Add(player.userID);
            SelectIfPermission(player);
        }

        [ConsoleCommand("UI_CreateGearSet")]
        private void cmdUI_CreateGearSet(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !isAuth(player)) return;
            CreateGearSet(player);
        }

        [ConsoleCommand("UI_EditGearSet")]
        private void cmdUI_EditGearSet(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !isAuth(player) || !CSUIInfo.ContainsKey(player.userID)) return;
            var existingSet = csData.SavedSets[CSUIInfo[player.userID].SelectedSet];
            if (NewSet.ContainsKey(player.userID))
                NewSet.Remove(player.userID);
            NewSet.Add(player.userID, new SetCreation { setname = CSUIInfo[player.userID].SelectedSet, set = existingSet, Editing = true });
            SetPanel(player);
        }

        [ConsoleCommand("UI_CancelSetCreation")]
        private void cmdUI_CancelGearSet(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null) return;
            if (NewSet.ContainsKey(player.userID))
                NewSet.Remove(player.userID);
            SetPanel(player);
        }

        [ConsoleCommand("UI_DeleteGearSet")]
        private void cmdUI_DeleteGearSet(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var set = CSUIInfo[player.userID].SelectedSet;
            if (csData.SavedSets.ContainsKey(set))
            {
                csData.SavedSets.Remove(set);
                foreach (var entry in CSUIInfo)
                {
                    if (entry.Value.SelectedSet == set)
                        entry.Value.SelectedSet = "";
                }
                foreach (BasePlayer p in BasePlayer.activePlayerList.Where(p => p != player))
                {
                    if (CSUIInfo[player.userID].open)
                        ToggleCSUI(p);
                }
                SaveData();
            }
            ToggleCSUI(player);
        }

        [ConsoleCommand("UI_SelectSkin")]
        private void cmdUI_SelectSkin(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            ulong skin;
            if (!ulong.TryParse(arg.Args[0], out skin)) skin = 0;
            if (CSUIInfo[player.userID].location == "main")
                NewSet[player.userID].set.main[CSUIInfo[player.userID].InspectedGear].skin = skin;
            else if (CSUIInfo[player.userID].location == "belt")
                NewSet[player.userID].set.belt[CSUIInfo[player.userID].InspectedGear].skin = skin;
            else if (CSUIInfo[player.userID].location == "wear")
                NewSet[player.userID].set.wear[CSUIInfo[player.userID].InspectedGear].skin = skin;
            CSUIInfo[player.userID].page = 0;
            SetPanel(player);
        }


        private void ProcessSelection(BasePlayer player, string name)
        {
            var SetData = csData.SavedSets[name];
            int totalcount = SetData.belt.Count() + SetData.wear.Count() + SetData.main.Count();
            if ((player.inventory.containerBelt.capacity - player.inventory.containerBelt.itemList.Count) < SetData.belt.Count() || (player.inventory.containerWear.capacity - player.inventory.containerWear.itemList.Count) < SetData.wear.Count() || (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count) < SetData.main.Count())
                if (totalcount > (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count))
                {
                    OnScreen(player, "NoInventorySpace");
                    return;
                }
            CuiHelper.DestroyUi(player, BackgroundPanel);
            DestroySetPanels(player);
            if (!csData.PlayerData.ContainsKey(player.userID))
                csData.PlayerData.Add(player.userID, new PlayerSetData());
            if (csData.SavedSets[name].cooldown > 0)
            {
                if (!csData.PlayerData[player.userID].cooldownANDuses.ContainsKey(name))
                    csData.PlayerData[player.userID].cooldownANDuses.Add(name, new CoolandUse());
                csData.PlayerData[player.userID].cooldownANDuses[name].CooldownExpiration = CurrentTotalMinutes() + csData.SavedSets[name].cooldown;
            }
            if (csData.SavedSets[name].dailyUses > 0)
            {
                if (!csData.PlayerData[player.userID].cooldownANDuses.ContainsKey(name))
                    csData.PlayerData[player.userID].cooldownANDuses.Add(name, new CoolandUse());
                csData.PlayerData[player.userID].cooldownANDuses[name].timesUsed++;
                if (csData.PlayerData[player.userID].cooldownANDuses[name].FirstUse == 0)
                    csData.PlayerData[player.userID].cooldownANDuses[name].FirstUse = CurrentTotalMinutes();
            }
            OnScreen(player, GetMSG("GivenSet", player, name));
            GiveGearSet(player, name);
        }

        private void GiveGearSet(BasePlayer player, string setname)
        {
            var set = csData.SavedSets[setname];
            foreach (var item in set.main)
            {
                var gear = BuildSet(item);
                gear.MoveToContainer(player.inventory.containerMain);
            }
            foreach (var item in set.wear)
            {
                var gear = BuildSet(item);
                gear.MoveToContainer(player.inventory.containerWear);
            }
            foreach (var item in set.belt)
            {
                var gear = BuildSet(item);
                int index = -1;
                index = player.inventory.containerBelt.itemList.FindIndex(k => k.position == item.position);
                if (index != -1)
                    player.inventory.containerBelt.itemList[index].MoveToContainer(player.inventory.containerMain);
                gear.MoveToContainer(player.inventory.containerBelt, item.position, false);
            }
        }

        private Item BuildSet(Gear gear)
        {
            var definition = ItemManager.FindItemDefinition(gear.shortname);
            if (definition != null)
            {
                var item = ItemManager.Create(definition, gear.amount, gear.skin);
                if (item != null)
                {
                    var held = item.GetHeldEntity() as BaseProjectile;
                    if (held != null)
                    {
                        if (!string.IsNullOrEmpty(gear.ammoType))
                        {
                            var ammoType = ItemManager.FindItemDefinition(gear.ammoType);
                            if (ammoType != null)
                                held.primaryMagazine.ammoType = ammoType;
                        }
                        held.primaryMagazine.contents = held.primaryMagazine.capacity;
                    }
                    if (gear.attachments != null)
                        foreach (var attachment in gear.attachments)
                        {
                            var att = BuildItem(attachment);
                            att.MoveToContainer(item.contents);
                        }
                    return item;
                }
            }
            Puts("Error making item: " + gear.shortname);
            return null;
        }

        private Item BuildItem(string shortname, int amount = 1, ulong skin = 0)
        {
            var definition = ItemManager.FindItemDefinition(shortname);
            if (definition != null)
            {
                var item = ItemManager.Create(definition, amount, skin);
                if (item != null)
                    return item;
            }
            Puts("Error making attachment: " + shortname);
            return null;
        }



        public void CreateGearSet(BasePlayer player)
        {
            if (NewSet.ContainsKey(player.userID))
                NewSet.Remove(player.userID);
            NewSet.Add(player.userID, new SetCreation());
            foreach (var cont in new List<ItemContainer> { player.inventory.containerBelt, player.inventory.containerMain, player.inventory.containerWear })
            {
                foreach (var entry in cont.itemList)
                {
                    List<string> attachments = new List<string>();
                    int ammo = 0;
                    string ammoType = string.Empty;
                    var held = entry.GetHeldEntity() as BaseProjectile;
                    if (held != null)
                    {
                        if (held.primaryMagazine != null)
                        {
                            ammo = held.primaryMagazine.contents;
                            ammoType = held.primaryMagazine.ammoType.shortname;
                        }
                        if (entry.contents != null && entry.contents.itemList != null)
                            foreach (var mod in entry.contents.itemList)
                                attachments.Add(mod.info.shortname);
                    }
                    if (cont == player.inventory.containerBelt)
                        NewSet[player.userID].set.belt.Add(new Gear
                        {
                            skin = entry.skin,
                            displayName = entry.info.displayName.english,
                            amount = entry.amount,
                            attachments = attachments,
                            ammo = ammo,
                            position = entry.position,
                            ammoType = ammoType,
                            shortname = entry.info.shortname,
                        });
                    else if (cont == player.inventory.containerWear)
                        NewSet[player.userID].set.wear.Add(new Gear
                        {
                            skin = entry.skin,
                            displayName = entry.info.displayName.english,
                            amount = entry.amount,
                            attachments = attachments,
                            ammo = ammo,
                            ammoType = ammoType,
                            shortname = entry.info.shortname,
                        });
                    else if (cont == player.inventory.containerMain)
                        NewSet[player.userID].set.main.Add(new Gear
                        {
                            skin = entry.skin,
                            displayName = entry.info.displayName.english,
                            amount = entry.amount,
                            attachments = attachments,
                            ammo = ammo,
                            ammoType = ammoType,
                            shortname = entry.info.shortname,
                        });
                }
            }
            NewSet[player.userID].set.Category = CSUIInfo[player.userID].SelectedCategory;
            SetPanel(player);
        }

        class SavedData
        {
            public Dictionary<string, Set> SavedSets = new Dictionary<string, Set>();
            public Dictionary<ulong, PlayerSetData> PlayerData = new Dictionary<ulong, PlayerSetData>();
        }

        class PlayerSetData
        {
            public Dictionary<string, CoolandUse> cooldownANDuses = new Dictionary<string, CoolandUse>();
        }

        class CoolandUse
        {
            public double CooldownExpiration;
            public int timesUsed;
            public double FirstUse;
        }

        class Set
        {
            public int cost = 0;
            public string Category;
            public string permission;
            public bool hidden;
            public int cooldown;
            public int dailyUses;
            public List<Gear> belt = new List<Gear>();
            public List<Gear> wear = new List<Gear>();
            public List<Gear> main = new List<Gear>();
        }

        class Gear
        {
            public string shortname;
            public string displayName;
            public ulong skin;
            public int amount;
            public int ammo;
            public string ammoType;
            public List<string> attachments = new List<string>();
            public int position;
        }



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
                string key = String.Empty;
                //if (!string.IsNullOrEmpty(configData.MenuKeyBinding))
                //    key = GetMSG("CSAltInfo", p, configData.MenuKeyBinding.ToUpper());
                GetSendMSG(p, "CSInfo", key);
            }
            timers.Add("info", timer.Once(configData.InfoInterval * 60, () => InfoLoop()));
        }



        #region  External Calls
        private bool isSet(string set)
        {
            if (!csData.SavedSets.ContainsKey(set)) return false;
            return true;
        }

        private bool GiveSet(BasePlayer player, string set)
        {
            if (!csData.SavedSets.ContainsKey(set)) return false;
            GiveGearSet(player, set);
            return true;
        }

        private bool TryGiveSet(BasePlayer player, string set)
        {
            if (!csData.SavedSets.ContainsKey(set)) return false;
            if (!permission.UserHasPermission(player.UserIDString, this.Title + "." + csData.SavedSets[set].permission)) return false;
            GiveGearSet(player, set);
            return true;
        }

        [HookMethod("GetSetInfo")]
        public object GetSetInfo(string set)
        {
            if (!csData.SavedSets.ContainsKey(set)) return false;
            var s = csData.SavedSets[set];
            JObject obj = new JObject();
            obj["category"] = s.Category;
            obj["cooldown"] = s.cooldown;
            obj["cost"] = s.cost;
            obj["uses"] = s.dailyUses;
            obj["hidden"] = s.hidden;
            obj["permission"] = s.permission;
            JArray belt = new JArray();
            foreach (var itemEntry in s.belt)
            {
                JObject item = new JObject();
                item["amount"] = itemEntry.amount;
                item["ammoamount"] = itemEntry.ammo;
                item["ammotype"] = itemEntry.ammoType;
                item["name"] = itemEntry.displayName;
                item["position"] = itemEntry.position;
                item["shortname"] = itemEntry.shortname;
                item["skin"] = itemEntry.skin;
                JArray mods = new JArray();
                foreach (var mod in itemEntry.attachments)
                    mods.Add(mod);
                item["attachments"] = mods;
                belt.Add(item);
            }
            obj["belt"] = belt;
            JArray main = new JArray();
            foreach (var itemEntry in s.main)
            {
                JObject item = new JObject();
                item["amount"] = itemEntry.amount;
                item["ammoamount"] = itemEntry.ammo;
                item["ammotype"] = itemEntry.ammoType;
                item["name"] = itemEntry.displayName;
                item["position"] = itemEntry.position;
                item["shortname"] = itemEntry.shortname;
                item["skin"] = itemEntry.skin;
                JArray mods = new JArray();
                foreach (var mod in itemEntry.attachments)
                    mods.Add(mod);
                item["attachments"] = mods;
                main.Add(item);
            }
            obj["main"] = main;
            JArray wear = new JArray();
            foreach (var itemEntry in s.wear)
            {
                JObject item = new JObject();
                item["amount"] = itemEntry.amount;
                item["ammoamount"] = itemEntry.ammo;
                item["ammotype"] = itemEntry.ammoType;
                item["name"] = itemEntry.displayName;
                item["position"] = itemEntry.position;
                item["shortname"] = itemEntry.shortname;
                item["skin"] = itemEntry.skin;
                JArray mods = new JArray();
                foreach (var mod in itemEntry.attachments)
                    mods.Add(mod);
                item["attachments"] = mods;
                wear.Add(item);
            }
            obj["wear"] = wear;
            return obj;
        }

        private object GetSetContents(string set)
        {
            if (!csData.SavedSets.ContainsKey(set)) return false;
            List<string> contents = new List<string>();
            foreach (var entry in csData.SavedSets[set].belt)
                contents.Add(entry.shortname + "_" + entry.skin + "belt");
            foreach (var entry in csData.SavedSets[set].wear)
                contents.Add(entry.shortname + "_" + entry.skin + "wear");
            foreach (var entry in csData.SavedSets[set].main)
                contents.Add(entry.shortname + "_" + entry.skin + "main");
            return contents;
        }

        private bool isKit(string set)
        {
            if (!csData.SavedSets.ContainsKey(set)) return false;
            return true;
        }

        private object GiveKit(BasePlayer player, string set)
        {
            if (!csData.SavedSets.ContainsKey(set)) return "No Set Found";
            GiveGearSet(player, set);
            return true;
        }

        private string[] GetAllKits() => csData.SavedSets.Keys.ToArray();

        private string[] GetKitContents(string kitname)
        {
            if (csData.SavedSets.ContainsKey(kitname))
            {
                List<string> items = new List<string>();
                foreach (var item in csData.SavedSets[kitname].belt)
                {
                    var itemstring = $"{item.shortname}_{item.amount}";
                    if (item.attachments.Count > 0)
                        foreach (var mod in item.attachments)
                            itemstring = itemstring + $"_{mod}";
                    items.Add(itemstring);
                }
                foreach (var item in csData.SavedSets[kitname].wear)
                {
                    var itemstring = $"{item.shortname}_{item.amount}";
                    if (item.attachments.Count > 0)
                        foreach (var mod in item.attachments)
                            itemstring = itemstring + $"_{mod}";
                    items.Add(itemstring);
                }
                foreach (var item in csData.SavedSets[kitname].main)
                {
                    var itemstring = $"{item.shortname}_{item.amount}";
                    if (item.attachments.Count > 0)
                        foreach (var mod in item.attachments)
                            itemstring = itemstring + $"_{mod}";
                    items.Add(itemstring);
                }
                if (items.Count > 0)
                    return items.ToArray();
            }
            return null;
        }

        CoolandUse GetSetData(ulong userID, string setname)
        {
            CoolandUse info = new CoolandUse();
            if (csData.PlayerData.ContainsKey(userID))
            {
                var Data = csData.PlayerData[userID];
                Data.cooldownANDuses.TryGetValue(setname, out info);
            }
            return info;
        }

        private double KitCooldown(string kitname) => csData.SavedSets[kitname].cooldown;

        private double PlayerKitCooldown(ulong ID, string kitname) => csData.SavedSets[kitname].cooldown <= 0 ? 0 : CurrentTotalMinutes() > GetSetData(ID, kitname).CooldownExpiration ? 0 : GetSetData(ID, kitname).CooldownExpiration - CurrentTotalMinutes();

        private string KitDescription(string kitname) => string.Empty;

        private int KitMax(string kitname) => csData.SavedSets[kitname].dailyUses;

        private double PlayerKitMax(ulong ID, string kitname) => csData.SavedSets[kitname].dailyUses <= 0 ? 0 : csData.SavedSets[kitname].dailyUses - GetSetData(ID, kitname).timesUsed < csData.SavedSets[kitname].dailyUses ? csData.SavedSets[kitname].dailyUses - GetSetData(ID, kitname).timesUsed : csData.SavedSets[kitname].dailyUses;
        #endregion

        void SaveData()
        {
            if (ready)
                CSDATA.WriteObject(csData);
        }

        void LoadData()
        {
            try
            {
                csData = CSDATA.ReadObject<SavedData>();
                if (csData == null)
                {
                    Puts("Corrupt Data file....creating new datafile");
                    csData = new SavedData();
                }
            }
            catch
            {
                Puts("Couldn't load Custom Sets Data, creating a new datafile");
                csData = new SavedData();
            }
            if (csData.SavedSets == null)
                csData.SavedSets = new Dictionary<string, Set>();
            if (csData.PlayerData == null)
                csData.PlayerData = new Dictionary<ulong, PlayerSetData>();
            ready = true;
            SaveData();
        }


        private ConfigData configData;
        class ConfigData
        {
            public int InfoInterval { get; set; }
            //public string MenuKeyBinding { get; set; }
            public List<string> Categories { get; set; }
            public Dictionary<int, string> SpawnSets { get; set; }
            public bool UsePayment { get; set; }
            public bool ShowLockedSets { get; set; }
            public int UsesResetInterval_InHours { get; set; }
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                InfoInterval = 15,
                //MenuKeyBinding = "",
                UsePayment = false,
                ShowLockedSets = true,
                UsesResetInterval_InHours = 24,
                Categories = new List<string> { "Basic", "Special", "Unlimited", "NoCooldowns", "VIP", "ADMIN ONLY" },
                SpawnSets = new Dictionary<int, string>
                {
                    {1, "setName1"},
                    {2, "setName2"},
                    {3, "setName3"},
                }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);

        void LoadDefaultMessages()
        {
            lang.RegisterMessages(messages, this, "en");
        }

        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"title", "CustomSets: " },
            {"CSInfo", "This server is running CustomSet. Type '/sets'{0} to open the menu."},
            {"CSAltInfo", " or press '{0}'" },
            {"NoPerm", "You do not have permission to use this command" },
            {"ImagesLoading", "Unable to open menu. Images are still loading..." },
            {"purchaseset", "You have successfully unlocked the {0} Set" },
            {"SetTitle", "Set: {0}" },
            {"Sets", "Sets in {0}" },
            {"SetCategories", "Set Categories" },
            {"PurchaseSetInfo", "Are you sure you want to purchase the Set: {0} for ${1}?" },
            {"SkinTitle", "Please select a Skin for {0}" },
            {"UnlockSetFree", "Do you want this Set to be FREE?" },
            {"Next", "Next" },
            {"Back", "Back" },
            {"Cancel", "Cancel" },
            {"Delete", "Delete" },
            {"ToggleAdminView", "Admin View" },
            {"NotAuthorized", "You are not authorized to use this function" },
            {"EnterAdminView", "You have entered Admin View." },
            {"ExitAdminView", "You have exited Admin View." },
            {"CostOFSet", "Cost $ {0} to Unlock" },
            {"UnlockSet", "Unlock for $ {0}?" },
            {"Redeem", "REDEEM !" },
            {"SetName", "Please provide a name for the new set. You can also type 'quit' to exit." },
            {"SetCost", "Please Select the Price to Unlock this Set" },
            {"CreateSet", "Create New Set?" },
            {"CancelSet", "Cancel Set Creation?" },
            {"SaveSet", "SAVE SET !" },
            {"GivenSet", "You have been given Set: {0}" },
            {"NewSetCreated", "You have successfully created a new set: {0}" },
            {"ClickToDetail", "Set Item Cost" },
            {"Remove", "Remove Item" },
            {"SetSkin", "Select Skin" },
            {"Container", "ITEMS FOR {0}" },
            {"Cooldown", "Cooldown: {0}" },
            {"RemainingCooldown", "Remaining Cooldown: {0}" },
            {"RemainingUses", "Remaining Uses: {0}" },
            {"DailyUses", "Uses: {0}" },
            {"SetCooldown", "Select a Set Cooldown (in Minutes)" },
            {"SetMaxDailyUses", "Select a Set Max Uses Amount" },
            {"UseAPermission", "Make this Set require Permission?" },
            {"NoInventorySpace", "You do not have enough inventory space for this set!" },
            {"ListOFSets", "LIST OF SETS" },
            {"Category", "Category: {0}" },
            {"HiddenSet", "Hide this Set from the menu?" },
            {"NameTaken", "That Set Name already exists. Try a different one." },
            {"Hidden", "Hidden: {0}" },
            {"Permission", "Permission: {0}" },
            {"Free", "Free" },
            {"Yes", "Yes" },
            {"No", "No" },
            {"CooldownTitle", "Edit Cooldown" },
            {"Cost", "Cost: {0}" },
            {"CostTitle", "Cost" },
            {"Uses", "Edit Uses" },
            {"CurrentStatus", "Current: {0}"},
            {"ExitSetEditing", "Stop Editing Set" },
            {"ITEM", "ITEM" },
            {"INFO", "INFO" },
            {"SettingChangedTo", "Change to {0}" },
            {"WaitingImageLibrary", "Waiting on Image Library to initialize. Trying again in 20 Seconds" }
        };
    }
}