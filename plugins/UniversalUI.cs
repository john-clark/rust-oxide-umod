using System.Collections.Generic;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using System;
using System.Linq;
using Oxide.Core.Plugins;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("UniversalUI", "Absolut", "2.1.4", ResourceId = 2226)]

    class UniversalUI : RustPlugin
    {
        [PluginReference]
        Plugin ImageLibrary, Kits;

        string TitleColor = "<color=orange>";
        string MsgColor = "<color=#A9A9A9>";
        bool SkinsReady = false;
        private Dictionary<ulong, screen> UniversalUIInfo = new Dictionary<ulong, screen>();
        class screen
        {
            public int section;
            public int page;
            public bool open;
            public int showSection;
            public bool admin;
        }
        private bool Debugging;
        private List<ulong> NoInfo = new List<ulong>();
        private Dictionary<ulong, DelayedCommand> OnDelay = new Dictionary<ulong, DelayedCommand>();
        class DelayedCommand
        {
            public bool closeui;
            public List<string> args;
        }
        private Dictionary<string, Timer> timers = new Dictionary<string, Timer>();

        #region Server Hooks

        void Loaded()
        {
            lang.RegisterMessages(messages, this);
            Debugging = false;
        }

        void Unload()
        {
            foreach (BasePlayer p in BasePlayer.activePlayerList)
            {
                DestroyPlayer(p);
            }
            foreach (var entry in timers)
                entry.Value.Destroy();
            timers.Clear();
        }

        void OnServerInitialized()
        {
            try
            {
                ImageLibrary.Call("isLoaded", null);
            }
            catch (Exception)
            {
                PrintWarning($"ImageLibrary is missing. Unloading {Name} as it will not work without ImageLibrary.");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }
            LoadVariables();
            RegisterPermissions();
            foreach (BasePlayer p in BasePlayer.activePlayerList)
                OnPlayerInit(p);
            GetAllImages();
            timers.Add("info", timer.Once(configData.InfoInterval * 60, () => InfoLoop()));
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (player != null)
            {
                if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
                {
                    timer.Once(5, () => OnPlayerInit(player));
                    return;

                }
                if (configData.MenuKeyBinding != "")
                    player.Command($"bind {configData.MenuKeyBinding} \"UI_OpenUniversalUI\"");
                if (configData.InfoInterval != 0)
                    if (configData.MenuKeyBinding != "")
                        GetSendMSG(player, "UIInfo", configData.MenuKeyBinding.ToString());
                    else GetSendMSG(player, "UIInfo1");
                if (configData.ForceOpenOnJoin)
                    OpenUniversalUI(player);
            }
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            DestroyUniversalUI(player);
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            DestroyPlayer(player);
        }

        object OnServerCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player == null)
                return null;
            if (OnDelay.ContainsKey(player.userID) && arg.cmd?.FullName == "chat.say")
            {
                if (arg.Args.Contains("quit"))
                {
                    OnDelay.Remove(player.userID);
                    GetSendMSG(player, "ExitDelayed");
                    return false;
                }
                foreach (var entry in arg.Args)
                    OnDelay[player.userID].args.Add(entry);
                RunCommand(player, OnDelay[player.userID].args.ToArray());
                return false;
            }
            return null;
        }

        private void RunCommand(BasePlayer player, string[] command)
        {
            if (command[0] == "chat.say")
            {
                rust.RunClientCommand(player, $"chat.say", string.Join(" ", command.Skip(1).ToArray()));
                if(Debugging) Puts($"Chat say:");
            }
            else
            {
                rust.RunClientCommand(player, string.Join(" ", command.ToArray()));
                if (Debugging) Puts($"Console Command: {string.Join(" ", command.ToArray())}");
            }
            if (OnDelay.ContainsKey(player.userID))
                OnDelay.Remove(player.userID);
        }

    #endregion

        #region Functions
    [ConsoleCommand("UI_OpenUniversalUI")]
        private void cmdUI_OpenUniversalUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (UniversalUIInfo.ContainsKey(player.userID))
                if (!UniversalUIInfo[player.userID].open)
                {
                    UniversalUIInfo[player.userID].open = true;
                    OpenUniversalUI(player);
                }
                else
                    DestroyUniversalUI(player);
            else
                OpenUniversalUI(player);
        }

        private void OpenUniversalUI(BasePlayer player)
        {
            if (!SkinsReady) { GetSendMSG(player, "ImageLibraryNotReady"); return; }
            if (!UniversalUIInfo.ContainsKey(player.userID))
                UniversalUIInfo.Add(player.userID, new screen { page = 0, section = 0, showSection = 0, open = true });
            UniversalUIPanel(player);
            if (!NoInfo.Contains(player.userID))
                BackgroundPanel(player);
            return;
        }

        [ConsoleCommand("UI_DestroyUniversalUI")]
        private void cmdUI_DestroyUniversalUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            DestroyUniversalUI(player);
        }

        [ConsoleCommand("UI_OpenInfoUI")]
        private void cmdUI_OpenInfoUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (NoInfo.Contains(player.userID))
                NoInfo.Remove(player.userID);
            OpenUniversalUI(player);
        }

        [ConsoleCommand("UI_HideInfo")]
        private void cmdUI_HideInfo(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (!NoInfo.Contains(player.userID))
                NoInfo.Add(player.userID);
            CuiHelper.DestroyUi(player, PanelInfo);
            OpenUniversalUI(player);
        }

        private object KitMax(string kitname) => Kits?.Call("KitMax", kitname);
        public int GetKitMax(string kitname)
        {
            var Max = KitMax(kitname);
            if (Max != null)
            {
                return Convert.ToInt32(Max);
            }
            return 0;
        }

        private object KitItemAmount(string kitname, int ID) => Kits?.Call("KitItemAmount", kitname, ID);
        public int GetKitItemAmount(string kitname, int ID)
        {
            var amount = KitItemAmount(kitname, ID);
            if (amount != null)
            {
                return Convert.ToInt32(amount);
            }
            return 0;
        }

        private object KitDescription(string kitname) => Kits?.Call("KitDescription", kitname);
        public string GetKitDescription(string kitname)
        {
            var Desc = KitDescription(kitname);
            if (Desc != null)
            {
                if (Desc is string)
                {
                    string dsc = Desc as string;
                    return dsc;
                }
            }
            return "NONE";
        }

        
        private object KitImage(string kitname) => Kits?.Call("KitImage", kitname);
        public string GetKitImage(string kitname)
        {
            var image = KitImage(kitname);
            if (image != null)
            {
                if (image is string)
                {
                    string img = image as string;
                    return img;
                }
            }
            return "http://i.imgur.com/xxQnE1R.png";
        }

        private object KitCooldown(string kitname) => Kits?.Call("KitCooldown", kitname);
        public int GetKitCooldown(string kitname)
        {
            var cooldown = KitCooldown(kitname);
            if (cooldown != null)
            {
                return Convert.ToInt32(cooldown) / 60;
            }
            return 0;
        }

        private object GetKits() => Kits?.Call("GetAllKits");
        public string[] GetKitNames()
        {
            var kits = GetKits();
            if (kits != null)
            {
                if (kits is string[])
                {
                    var array = kits as string[];
                    return array;
                }
            }
            return null;
        }



        void AskForPageType(BasePlayer player, int page)
        {
            if (!UniversalUIInfo.ContainsKey(player.userID)) return;
            DestroyUniversalUI(player);
                CuiHelper.DestroyUi(player, PanelInfo);
                var element = UI.CreateElementContainer(PanelInfo, "0 0 0 0", "0.3 0.4", "0.7 0.6", true);
                var types = Enum.GetValues(typeof(PageType)).Cast<PageType>();
                var amount = types.Count();
                var i = 0;
                float[] pos;
                foreach (var entry in types)
                {
                    pos = SelectionButtonLocation(i, amount);
                    UI.LoadImage(ref element, PanelInfo, TryForImage(entry.ToString()), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                    UI.CreateTextOutline(ref element, PanelInfo, UIColors["white"], UIColors["black"], GetMSG("Type",player, entry.ToString()), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3] + .05f}", TextAnchor.UpperCenter);
                    UI.CreateButton(ref element, PanelInfo, "0 0 0 0", "", 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_SetPageType {entry.ToString()} {page}", TextAnchor.MiddleCenter);
                    i++;
                }
                CuiHelper.AddUi(player, element);
        }

        [ConsoleCommand("UI_SetPageType")]
        private void cmdUI_SetPageType(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (!UniversalUIInfo.ContainsKey(player.userID)) return;
            DestroyUniversalUI(player);
            if (configData.sections.ContainsKey(UniversalUIInfo[player.userID].section))
            {
                configData.sections[UniversalUIInfo[player.userID].section].pages[Convert.ToInt32(arg.Args[1])].type = (PageType)Enum.Parse(typeof(PageType), arg.Args[0]);
                Config.WriteObject(configData, true);
            }
            else GetSendMSG(player, "SectionNotFound");
            OpenUniversalUI(player);
        }


        private float[] SelectionButtonLocation(int number, int amount)
        {
            var size = Decimal.Divide(1, amount);
            Vector2 position = new Vector2(0.03f, (float)(1 - size));
            Vector2 dimensions = new Vector2((float)size, .9f);
            float offsetY = 0;
            float offsetX = (0.015f + dimensions.x) * number;
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        [ConsoleCommand("UI_AddNewSection")]
        private void cmdUI_AddNewSection(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (!UniversalUIInfo.ContainsKey(player.userID)) return;
            var index = 1;
            foreach (var entry in configData.sections.Where(k => k.Key == index))
            {
                index++;
                continue;
            }
            configData.sections.Add(index, new Section { name = index.ToString().ToUpper(), pages = new List<Page> { new Page { page = 0, name = $"Added By {player.displayName}" } } });
            Config.WriteObject(configData, true);
            OpenUniversalUI(player);
        }

        [ConsoleCommand("UI_RemoveSection")]
        private void cmdUI_RemoveSection(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (!UniversalUIInfo.ContainsKey(player.userID)) return;
            if (configData.sections.ContainsKey(UniversalUIInfo[player.userID].section))
                configData.sections.Remove(UniversalUIInfo[player.userID].section);
            Config.WriteObject(configData, true);
            UniversalUIInfo[player.userID].section = 0;
            OpenUniversalUI(player);
        }

        [ConsoleCommand("UI_AddNewPage")]
        private void cmdUI_AddNewPage(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (!UniversalUIInfo.ContainsKey(player.userID)) return;
            var existingPages = configData.sections[UniversalUIInfo[player.userID].section].pages;
            var index = 0;
            foreach (var entry in existingPages.Where(k => k.page == index))
                index++;
            configData.sections[UniversalUIInfo[player.userID].section].pages.Add(new Page { page = index, name = $"Added By {player.displayName}", buttons = new List<PageButton> { new PageButton { order = 0 }, new PageButton { order = 1 }, new PageButton { order = 2 }, new PageButton { order = 3 } } });
            AskForPageType(player, index);
            Config.WriteObject(configData, true);
        }

        [ConsoleCommand("UI_RemovePage")]
        private void cmdUI_RemovePage(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (!UniversalUIInfo.ContainsKey(player.userID)) return;
            foreach (var entry in configData.sections[UniversalUIInfo[player.userID].section].pages.Where(k => k.page == UniversalUIInfo[player.userID].page))
            {
                configData.sections[UniversalUIInfo[player.userID].section].pages.Remove(entry);
                break;
            }
            Config.WriteObject(configData, true);
            UniversalUIInfo[player.userID].page = 0;
            OpenUniversalUI(player);
        }




        [ConsoleCommand("UI_RunConsoleCommand")]
        private void cmdUI_RunConsoleCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            //Puts(string.Join(" ",arg.Args));
            var cmd = "";
            if (arg.Args[1] == "delay")
            {
                if (OnDelay.ContainsKey(player.userID))
                    OnDelay.Remove(player.userID);
                OnDelay.Add(player.userID, new DelayedCommand { closeui = Convert.ToBoolean(arg.Args[0]), args = new List<string>() });
                foreach (var e in arg.Args.Skip(2))
                OnDelay[player.userID].args.Add(e);
                var message = string.Join(" ", arg.Args.Skip(2).ToArray());
                //Puts(message);
                //if (arg.Args[2] == "chat.say")
                //   message = string.Join(" ", arg.Args.Skip(3).ToArray());
                GetSendMSG(player, "DelayedCMD", message);
                DestroyUniversalUI(player);
            }
            else if (arg.Args[1] == "chat.say")
            {
                cmd = string.Join(" ", arg.Args.Skip(2).ToArray());
                rust.RunClientCommand(player, $"chat.say", cmd);
                if (Convert.ToBoolean(arg.Args[0]))
                    DestroyUniversalUI(player);
            }
            else
            {
                cmd = string.Join(" ", arg.Args.Skip(1).ToArray());
                rust.RunClientCommand(player, $"{cmd}");
                if (Convert.ToBoolean(arg.Args[0]))
                    DestroyUniversalUI(player);
            }
        }

        [ConsoleCommand("UI_PageTurn")]
        private void cmdUI_PageTurn(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            int page = Convert.ToInt32(arg.Args[0]);
            UniversalUIInfo[player.userID].page = page;
            UIInfoPanel(player);
        }

        [ConsoleCommand("UI_SwitchSection")]
        private void cmdUI_SwitchSection(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var section = Convert.ToInt32(arg.Args[0]);
            if (section != 0)
            {
                var admin = arg.Args[1];
                if (admin == "false")
                {
                    if (arg.Args.Length > 2)
                        if (!isAllowed(player, false, arg.Args[2]))
                        {
                            GetSendMSG(player, "NotAuth");
                            return;
                        }
                }
                else
                {
                    if (!isAllowed(player, true))
                    {
                        GetSendMSG(player, "NotAuth");
                        return;
                    }
                }
            }
            UniversalUIInfo[player.userID].section = section;
            UniversalUIInfo[player.userID].page = 0;
            UIInfoPanel(player);
        }

        [ConsoleCommand("UI_InfoSectionButtonChange")]
        private void cmdUI_InfoSectionButtonChange(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var section = Convert.ToInt32(arg.Args[0]);
            if (Debugging) Puts($"{section}");
            UniversalUIInfo[player.userID].showSection = section;
            UIInfoPanel(player);
        }



        private void DestroyUniversalUI(BasePlayer player)
        {
            if (UniversalUIInfo.ContainsKey(player.userID))
                if (UniversalUIInfo[player.userID].open)
                    UniversalUIInfo[player.userID].open = false;
            CuiHelper.DestroyUi(player, PanelStatic);
            CuiHelper.DestroyUi(player, PanelUUI);
            CuiHelper.DestroyUi(player, PanelInfo);
        }

        private void DestroyPlayer(BasePlayer player)
        {
            if (UniversalUIInfo.ContainsKey(player.userID))
                UniversalUIInfo.Remove(player.userID);
            CuiHelper.DestroyUi(player, PanelUUI);
            CuiHelper.DestroyUi(player, PanelInfo);
            //player.Command("bind tab \"inventory.toggle\"");
            //player.Command("bind q \"inventory.togglecrafting\"");
            //player.Command("bind escape \"\"");
            if (configData.MenuKeyBinding != "")
                player.Command($"bind {configData.MenuKeyBinding} \"\"");
        }

        private string GetLang(string msg, BasePlayer player = null)
        {
            if (messages.ContainsKey(msg))
                return lang.GetMessage(msg, this);
            else return msg;
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
            string msg = string.Format(lang.GetMessage(message, this, p), arg1, arg2, arg3);
            return msg;
        }

        bool isAuth(BasePlayer player)
        {
            if (player.net.connection != null)
                if (player.net.connection.authLevel < 2 && !permission.UserHasPermission(player.UserIDString, "UniversalUI.admin"))
                    return false;
            return true;
        }

        private void RegisterPermissions()
        {
            if (!permission.PermissionExists("UniversalUI.admin"))
                permission.RegisterPermission("UniversalUI.admin", this);
            foreach (var entry in configData.buttons)
                if (!string.IsNullOrEmpty(entry.Value.permission) && !permission.PermissionExists("UniversalUI." + entry.Value.permission))
                    permission.RegisterPermission("UniversalUI."+entry.Value.permission, this);
            foreach (var section in configData.sections)
            {
                if (!string.IsNullOrEmpty(section.Value.permission) && !permission.PermissionExists("UniversalUI." + section.Value.permission))
                    permission.RegisterPermission("UniversalUI." + section.Value.permission, this);
                foreach (var page in section.Value.pages)
                    foreach (var button in page.buttons)
                        if (!string.IsNullOrEmpty(button.permission) && !permission.PermissionExists("UniversalUI." + button.permission))
                            permission.RegisterPermission("UniversalUI." + button.permission, this);
            }

        }

        bool isAllowed(BasePlayer player, bool adminonly, string perm = "")
        {
            if (isAuth(player)) return true;
            if (adminonly) return false;
            if (!string.IsNullOrEmpty(perm))
                if (!permission.UserHasPermission(player.UserIDString, "UniversalUI." + perm)) return false;
            return true;
        }

        private string TryForImage(string shortname, ulong skin = 99)
        {
            if (shortname.Contains("http")) return shortname;
            if (skin == 99) skin = (ulong)ResourceId;
            return GetImage(shortname, skin, true);
        }

        private bool Valid(string name, ulong id = 99)
        {
            if (id == 99) id = (ulong)ResourceId;
            return HasImage(name, id);
        }

        public string GetImage(string shortname, ulong skin = 0, bool returnUrl = false) => (string)ImageLibrary.Call("GetImage", shortname.ToLower(), skin, returnUrl);
        public bool HasImage(string shortname, ulong skin = 0) => (bool)ImageLibrary.Call("HasImage", shortname.ToLower(), skin);
        public bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname.ToLower(), skin);
        public List<ulong> GetImageList(string shortname) => (List<ulong>)ImageLibrary.Call("GetImageList", shortname.ToLower());
        public bool isReady() => (bool)ImageLibrary?.Call("IsReady");
        #endregion

        #region UI Creation

        private string PanelStatic = "PanelStatic";
        private string PanelUUI = "PanelUUI";
        private string PanelInfo = "PanelInfo";
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

            static public void CreateTextOverlay(ref CuiElementContainer element, string panel, string text, string color, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter, float fadein = 1.0f)
            {
                //if (configdata.DisableUI_FadeIn)
                //    fadein = 0;
                element.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, FadeIn = fadein, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);

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
            {"header", "1 1 1 0.3" },
            {"light", "0.7 0.7 0.7 0.3" },
            {"grey1", "0.6 0.6 0.6 1.0" },
            {"brown", "0.3 0.16 0.0 1.0" },
            {"yellow", "0.9 0.9 0.0 1.0" },
            {"orange", "1.0 0.65 0.0 1.0" },
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
            {"CSorange", "1.0 0.64 0.10 1.0" }
        };

        private Dictionary<string, string> TextColors = new Dictionary<string, string>
        {
            {"limegreen", "<color=#6fff00>" }
        };

        #endregion

        #region UI Panels

        void UniversalUIPanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelUUI);
            if (configData.UseButtonPanel)
            {
                var element = UI.CreateElementContainer(PanelUUI, "0 0 0 0", "0.85 0.225", "1.0 0.725", true);
                foreach (var entry in configData.buttons)
                        if (!string.IsNullOrEmpty(entry.Value.command) && isAllowed(player, entry.Value.adminOnly, entry.Value.permission))
                            CreateButtonOnUI(ref element, PanelUUI, entry.Value, entry.Key);
                if (configData.UseInfoPanel)
                    if (NoInfo.Contains(player.userID))
                        UI.CreateButton(ref element, PanelUUI, UIColors["blue"], GetLang("InfoPanel"), 12, "0.2 -.1", "0.8 -.06", "UI_OpenInfoUI");
                CuiHelper.AddUi(player, element);
            }
        }

        private void CreateButtonOnUI(ref CuiElementContainer container, string panelName, MainButton button, int num)
        {
            var pos = CalcButtonPos(num);
            if (Valid($"UUIMainButton{num}"))
            {
                UI.LoadImage(ref container, panelName, TryForImage($"UUIMainButton{num}"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                UI.CreateButton(ref container, panelName, "0 0 0 0", button.name, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_RunConsoleCommand {button.CloseUI} {button.command}");
            }
            else
            {
                UI.CreateButton(ref container, panelName, UIColors["red"], button.name, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_RunConsoleCommand {button.CloseUI} {button.command}");
            }
        }

        void BackgroundPanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelStatic);
            if (!UniversalUIInfo.ContainsKey(player.userID))
                UniversalUIInfo.Add(player.userID, new screen { page = 0, section = 0, showSection = 0 });
            CuiElementContainer element = element = UI.CreateElementContainer(PanelStatic, "0 0 0 0", "0.2 0.2", "0.8 0.8", true);
            CuiHelper.AddUi(player, element);
            UIInfoPanel(player);
        }

        void UIInfoPanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelInfo);
            CuiElementContainer element = element = UI.CreateElementContainer(PanelInfo, "0 0 0 0", "0.2 0.2", "0.8 0.8");
            //if (UniversalUIInfo[player.userID].section != 0)
            foreach (var entry in configData.sections.Where(k => k.Key == UniversalUIInfo[player.userID].section))
            {
                if (Debugging) Puts($"No Home Page - Trying Section: {entry.Key.ToString()}");
                if (entry.Key == UniversalUIInfo[player.userID].section)
                    foreach (var page in entry.Value.pages.Where(kvp => kvp.page == UniversalUIInfo[player.userID].page))
                    {
                        switch (page.type)
                        {
                            case PageType.text:
                                {
                                    if (Valid($"UUIPage{entry.Key}-{UniversalUIInfo[player.userID].page}"))
                                        UI.LoadImage(ref element, PanelInfo, TryForImage($"UUIPage{entry.Key}-{UniversalUIInfo[player.userID].page}"), "0 0", "1 0.88");
                                    else
                                    {
                                        UI.CreatePanel(ref element, PanelInfo, UIColors["dark"], "0 0", "1 1");
                                        UI.CreatePanel(ref element, PanelInfo, UIColors["light"], "0.01 0.02", "0.99 0.98");
                                    }
                                    if (!string.IsNullOrEmpty(page.name))
                                        UI.CreateLabel(ref element, PanelInfo, UIColors["white"], page.name.ToUpper(), 24, "0.3 0.8", "0.7 0.9");
                                    if (!string.IsNullOrEmpty(page.text))
                                        UI.CreateLabel(ref element, PanelInfo, UIColors["white"], page.text, 12, "0.03 0.2", "0.97 0.65");
                                    //foreach (var button in page.buttons.OrderBy(kvp => kvp.order))
                                    //    if (!string.IsNullOrEmpty(button.command) && isAllowed(player, button.adminOnly, button.permission))
                                    //    {
                                    //        var pos = CalcInfoButtonPos(button.order);
                                    //        if (Valid($"UUIPageButton{entry.Key}-{UniversalUIInfo[player.userID].page}-{button.order}"))
                                    //        {
                                    //            UI.LoadImage(ref element, PanelInfo, TryForImage($"UUIPageButton{entry.Key}-{UniversalUIInfo[player.userID].page}-{button.order}"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                    //            if (!string.IsNullOrEmpty(button.name))
                                    //            {
                                    //                UI.CreateLabel(ref element, PanelInfo, UIColors["red"], button.name, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                    //                UI.CreateButton(ref element, PanelInfo, "0 0 0 0", button.name, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_RunConsoleCommand {button.CloseUI} {button.command}");
                                    //            }
                                    //            else
                                    //                UI.CreateButton(ref element, PanelInfo, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_RunConsoleCommand {button.CloseUI} {button.command}");
                                    //        }
                                    //        else
                                    //        {
                                    //            if (!string.IsNullOrEmpty(button.name))
                                    //                UI.CreateButton(ref element, PanelInfo, UIColors["red"], button.name, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_RunConsoleCommand {button.CloseUI} {button.command}");
                                    //            else
                                    //                UI.CreateButton(ref element, PanelInfo, UIColors["red"], "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_RunConsoleCommand {button.CloseUI} {button.command}");
                                    //        }
                                    //    }
                                    break;
                                }
                            case PageType.buttons:
                                {
                                    if (Valid($"UUIPage{entry.Key}-{UniversalUIInfo[player.userID].page}"))
                                        UI.LoadImage(ref element, PanelInfo, TryForImage($"UUIPage{entry.Key}-{UniversalUIInfo[player.userID].page}"), "0 0", "1 0.88");
                                    else
                                    {
                                        UI.CreatePanel(ref element, PanelInfo, UIColors["dark"], "0 0", "1 1");
                                        UI.CreatePanel(ref element, PanelInfo, UIColors["light"], "0.01 0.02", "0.99 0.98");
                                    }
                                    if (!string.IsNullOrEmpty(page.name))
                                        UI.CreateLabel(ref element, PanelInfo, UIColors["white"], page.name.ToUpper(), 24, "0.3 0.8", "0.7 0.9");
                                    //if (!string.IsNullOrEmpty(page.text))
                                    //    UI.CreateLabel(ref element, PanelInfo, UIColors["white"], page.text, 12, "0.03 0.2", "0.85 0.65", TextAnchor.UpperLeft);
                                    foreach (var button in page.buttons.OrderByDescending(kvp => kvp.order))
                                        if (!string.IsNullOrEmpty(button.command) && isAllowed(player, button.adminOnly, button.permission))
                                        {
                                            if (button.order > 69) continue;
                                            var pos = CalcButtonPagePos(button.order);
                                            if (Valid($"UUIPageButton{entry.Key}-{UniversalUIInfo[player.userID].page}-{button.order}"))
                                            {
                                                UI.LoadImage(ref element, PanelInfo, TryForImage($"UUIPageButton{entry.Key}-{UniversalUIInfo[player.userID].page}-{button.order}"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                                if (!string.IsNullOrEmpty(button.name))
                                                {
                                                    UI.CreateLabel(ref element, PanelInfo, UIColors["red"], button.name, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                                    UI.CreateButton(ref element, PanelInfo, "0 0 0 0", button.name, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_RunConsoleCommand {button.CloseUI} {button.command}");
                                                }
                                                else
                                                    UI.CreateButton(ref element, PanelInfo, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_RunConsoleCommand {button.CloseUI} {button.command}");
                                            }
                                            else
                                            {
                                                if (!string.IsNullOrEmpty(button.name))
                                                    UI.CreateButton(ref element, PanelInfo, UIColors["red"], button.name, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_RunConsoleCommand {button.CloseUI} {button.command}");
                                                else
                                                    UI.CreateButton(ref element, PanelInfo, UIColors["red"], "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_RunConsoleCommand {button.CloseUI} {button.command}");
                                            }
                                        }
                                    break;
                                }
                            case PageType.kits:
                                {
                                    if (Valid($"UUIPage{entry.Key}-{UniversalUIInfo[player.userID].page}"))
                                        UI.LoadImage(ref element, PanelInfo, TryForImage($"UUIPage{entry.Key}-{UniversalUIInfo[player.userID].page}"), "0 0", "1 0.88");
                                    else
                                    {
                                        UI.CreatePanel(ref element, PanelInfo, UIColors["dark"], "0 0", "1 1");
                                        UI.CreatePanel(ref element, PanelInfo, UIColors["light"], "0.01 0.02", "0.99 0.98");
                                    }
                                    if (!string.IsNullOrEmpty(page.name))
                                        UI.CreateLabel(ref element, PanelInfo, UIColors["white"], page.name.ToUpper(), 24, "0.3 0.8", "0.7 0.9");
                                    var i = 0;
                                    foreach (var kit in page.Kits)
                                    {
                                        if (i > 7) break;
                                        CreateKitEntry(ref element, PanelInfo, player, kit, i); i++;
                                    }
                                    if (UniversalUIInfo[player.userID].admin)
                                        UI.CreateButton(ref element, PanelInfo, UIColors["header"], GetLang("AddKit"), 12, "0.03 0.11", "0.13 0.17", $"UI_SelectKitToAdd {0}");
                                    break;
                                }
                            default:
                                {
                                    if (Valid($"UUIPage{entry.Key}-{UniversalUIInfo[player.userID].page}"))
                                        UI.LoadImage(ref element, PanelInfo, TryForImage($"UUIPage{entry.Key}-{UniversalUIInfo[player.userID].page}"), "0 0", "1 0.88");
                                    else
                                    {
                                        UI.CreatePanel(ref element, PanelInfo, UIColors["dark"], "0 0", "1 1");
                                        UI.CreatePanel(ref element, PanelInfo, UIColors["light"], "0.01 0.02", "0.99 0.98");
                                    }
                                    if (!string.IsNullOrEmpty(page.name))
                                        UI.CreateLabel(ref element, PanelInfo, UIColors["white"], page.name.ToUpper(), 24, "0.3 0.8", "0.7 0.9");
                                    if (!string.IsNullOrEmpty(page.text))
                                        UI.CreateLabel(ref element, PanelInfo, UIColors["white"], page.text, 12, "0.03 0.2", "0.85 0.65", TextAnchor.UpperLeft);
                                    foreach (var button in page.buttons.OrderBy(kvp => kvp.order))
                                        if (!string.IsNullOrEmpty(button.command) && isAllowed(player, button.adminOnly, button.permission))
                                        {
                                            var pos = CalcInfoButtonPos(button.order);
                                            if (Valid($"UUIPageButton{entry.Key}-{UniversalUIInfo[player.userID].page}-{button.order}"))
                                            {
                                                UI.LoadImage(ref element, PanelInfo, TryForImage($"UUIPageButton{entry.Key}-{UniversalUIInfo[player.userID].page}-{button.order}"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                                if (!string.IsNullOrEmpty(button.name))
                                                {
                                                    UI.CreateLabel(ref element, PanelInfo, UIColors["red"], button.name, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                                    UI.CreateButton(ref element, PanelInfo, "0 0 0 0", button.name, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_RunConsoleCommand {button.CloseUI} {button.command}");
                                                }
                                                else
                                                    UI.CreateButton(ref element, PanelInfo, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_RunConsoleCommand {button.CloseUI} {button.command}");
                                            }
                                            else
                                            {
                                                if (!string.IsNullOrEmpty(button.name))
                                                    UI.CreateButton(ref element, PanelInfo, UIColors["red"], button.name, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_RunConsoleCommand {button.CloseUI} {button.command}");
                                                else
                                                    UI.CreateButton(ref element, PanelInfo, UIColors["red"], "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_RunConsoleCommand {button.CloseUI} {button.command}");
                                            }
                                        }
                                    break;
                                }
                        }
                    }
                break;
            }
            //Create Section Buttons at the Top

            if (UniversalUIInfo[player.userID].section == 0)
            {
                if (Valid(configData.HomePage.name))
                    UI.LoadImage(ref element, PanelInfo, TryForImage(configData.HomePage.name), "0 0", "1 0.88");
                else
                {
                    UI.CreatePanel(ref element, PanelInfo, UIColors["dark"], "0 0", "1 1");
                    UI.CreatePanel(ref element, PanelInfo, UIColors["light"], "0.01 0.02", "0.99 0.98");
                }
                if (!string.IsNullOrEmpty(configData.HomePage.text))
                    UI.CreateLabel(ref element, PanelInfo, UIColors["white"], configData.HomePage.text, 12, "0.02 0.2", "0.97 0.65");

                UI.CreatePanel(ref element, PanelInfo, UIColors["red"], "0.02 0.9", "0.16 0.975");
                UI.CreateLabel(ref element, PanelInfo, UIColors["white"], configData.HomePage.name.ToUpper(), 12, "0.02 0.9", "0.16 0.975");
            }
            else
            {
                UI.CreateButton(ref element, PanelInfo, UIColors["blue"], configData.HomePage.name.ToUpper(), 12, "0.02 0.9", "0.16 0.975", $"UI_SwitchSection {0} false ");
            }
            foreach (var entry in configData.sections/*.Where(k => k.Key >= UniversalUIInfo[player.userID].showSection && k.Key < (UniversalUIInfo[player.userID].showSection + 4))*/)
            {
                if (entry.Key < UniversalUIInfo[player.userID].showSection) continue;
                if (entry.Key > UniversalUIInfo[player.userID].showSection + 5) continue;
                var pos = CalcSectionButtonPos(entry.Key - UniversalUIInfo[player.userID].showSection);
                if (Debugging) Puts($"Trying Section: {entry.Key}");
                if (Debugging) Puts($"Shown Section: { UniversalUIInfo[player.userID].showSection}");
                if (Debugging) Puts($"Last Section: { UniversalUIInfo[player.userID].showSection + 4}");
                var admin = "false";
                if (entry.Value.adminOnly)
                    admin = "true";
                if (entry.Key == UniversalUIInfo[player.userID].section)
                {
                    if (Debugging) Puts($"Section Match: {entry.Key}");
                    var lastpage = configData.sections[UniversalUIInfo[player.userID].section].pages.Count() - 1;
                    var currentpage = UniversalUIInfo[player.userID].page;
                    if (currentpage < lastpage - 1)
                    {
                        UI.CreateButton(ref element, PanelInfo, UIColors["dark"], GetLang("Last"), 12, "0.9 0.03", "0.95 0.085", $"UI_PageTurn {lastpage}");
                    }
                    if (currentpage < lastpage)
                    {
                        UI.CreateButton(ref element, PanelInfo, UIColors["dark"], GetLang("Next"), 12, "0.84 0.03", "0.89 0.085", $"UI_PageTurn {currentpage + 1}");
                    }
                    if (currentpage > 0)
                    {
                        UI.CreateButton(ref element, PanelInfo, UIColors["dark"], GetLang("Back"), 12, "0.77 0.03", "0.82 0.085", $"UI_PageTurn {currentpage - 1}");
                    }
                    if (currentpage > 1)
                    {
                        UI.CreateButton(ref element, PanelInfo, UIColors["dark"], GetLang("First"), 12, "0.71 0.03", "0.76 0.085", $"UI_PageTurn {0}");
                    }
                    UI.CreatePanel(ref element, PanelInfo, UIColors["red"], $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                    if (!string.IsNullOrEmpty(entry.Value.name))
                        UI.CreateLabel(ref element, PanelInfo, UIColors["white"], entry.Value.name.ToUpper(), 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                    else UI.CreateLabel(ref element, PanelInfo, UIColors["white"], $"Section:{entry.Key.ToString().ToUpper()}", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                }
                else
                {
                    if (Debugging) Puts($"Section Didn't Match: {entry.Key}");
                    if (Valid($"UUISectionButton{entry.Key}"))
                    {
                        if (HasImage($"UUISectionButton{entry.Key}", (ulong)ResourceId))
                        {
                            UI.LoadImage(ref element, PanelInfo, TryForImage($"UUISectionButton{entry.Key}"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                            UI.CreateButton(ref element, PanelInfo, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_SwitchSection {entry.Key} {admin} {entry.Value.permission}");
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(entry.Value.name))
                                UI.CreateButton(ref element, PanelInfo, "0 0 0 0", entry.Value.name.ToUpper(), 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_SwitchSection {entry.Key} {admin} {entry.Value.permission}");
                            else
                                UI.CreateButton(ref element, PanelInfo, "0 0 0 0", $"Section:{entry.Key.ToString().ToUpper()}", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_SwitchSection {entry.Key} {admin} {entry.Value.permission}");
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(entry.Value.name))
                            UI.CreateButton(ref element, PanelInfo, UIColors["blue"], entry.Value.name.ToUpper(), 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_SwitchSection {entry.Key} {admin} {entry.Value.permission}");
                        else
                            UI.CreateButton(ref element, PanelInfo, UIColors["blue"], $"Section:{entry.Key.ToString().ToUpper()}", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_SwitchSection {entry.Key} {admin} {entry.Value.permission}");
                    }
                }
            }
            if (UniversalUIInfo[player.userID].showSection != 0)
                UI.CreateButton(ref element, PanelInfo, UIColors["black"], "<--", 12, "0.17 0.9", "0.2 0.975", $"UI_InfoSectionButtonChange {UniversalUIInfo[player.userID].showSection - 1}");
            if (Debugging) Puts($"Max Section: {configData.sections.Max(kvp => kvp.Key) - 1}");
            if (UniversalUIInfo[player.userID].showSection + 5 < configData.sections.Max(kvp => kvp.Key))
                UI.CreateButton(ref element, PanelInfo, UIColors["black"], "-->", 12, "0.95 0.9", "0.98 0.975", $"UI_InfoSectionButtonChange {UniversalUIInfo[player.userID].showSection + 1}");
            UI.CreateButton(ref element, PanelInfo, UIColors["red"], GetLang("Close"), 12, "0.03 0.03", "0.13 0.085", "UI_DestroyUniversalUI");
            if (configData.UseButtonPanel)
                UI.CreateButton(ref element, PanelInfo, UIColors["red"], GetLang("HideInfoPanel"), 12, "0.14 0.03", "0.24 0.085", "UI_HideInfo");
            if (isAuth(player))
                UI.CreateButton(ref element, PanelInfo, UIColors["buttonred"], GetLang("ToggleAdmin"), 13, "-0.13 0.03", "-0.03 0.085", $"UI_AdminView");
            if (UniversalUIInfo[player.userID].admin)
            {
                UI.CreateButton(ref element, PanelInfo, UIColors["green"], GetLang("AddSection"), 12, "0.25 0.03", "0.35 0.085", "UI_AddNewSection");
                if (UniversalUIInfo[player.userID].section != 0)
                {
                    UI.CreateButton(ref element, PanelInfo, UIColors["red"], GetLang("RemoveSection"), 12, "0.36 0.03", "0.46 0.085", "UI_RemoveSection");
                    UI.CreateButton(ref element, PanelInfo, UIColors["green"], GetLang("AddPage"), 12, "0.47 0.03", "0.57 0.085", "UI_AddNewPage");
                    if (UniversalUIInfo[player.userID].page != 0) UI.CreateButton(ref element, PanelInfo, UIColors["red"], GetLang("RemovePage"), 12, "0.58 0.03", "0.68 0.085", "UI_RemovePage");
                }
            }
            CuiHelper.AddUi(player, element);
        }

        private void CreateKitEntry(ref CuiElementContainer container, string panelName, BasePlayer player, string kit, int num)
        {
            var pos = KitPos(num);
            UI.CreatePanel(ref container, panelName, UIColors["header"], $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
            if (num == 0)
            {
                UI.CreateLabel(ref container, panelName, UIColors["white"], "Kit", 12, $"{pos[0] + .1f} {pos[1] + .07f}", $"{pos[0] + .22f} {pos[3] + .07f}");
                UI.CreateLabel(ref container, panelName, UIColors["white"], "Description", 12, $"{pos[0] + .23f} {pos[1] + .07f}", $"{pos[0] + .6f} {pos[3] + .07f}");
                UI.CreateLabel(ref container, panelName, UIColors["white"], "Cooldown", 12, $"{pos[0] + .64f} {pos[1] + .07f}", $"{pos[0] + .73f} {pos[3] + .07f}");
                UI.CreateLabel(ref container, panelName, UIColors["white"], "Max", 12, $"{pos[0] + .72f} {pos[1] + .07f}", $"{pos[0] + .78f} {pos[3] + .07f}");
            }
            var description = "";
            if (!string.IsNullOrEmpty(GetKitDescription(kit)))
                description = GetKitDescription(kit);
            UI.LoadImage(ref container, panelName, GetKitImage(kit) , $"{pos[0]+.01f} {pos[1]}", $"{pos[0] + .09f} {pos[3]}");
            UI.CreateLabel(ref container, panelName, UIColors["white"], kit.ToUpper(), 12, $"{pos[0] + .1f} {pos[1] + .01f}", $"{pos[0] + .22f} {pos[3] - .01f}");
            UI.CreateLabel(ref container, panelName, UIColors["white"], description, 12, $"{pos[0] + .23f} {pos[1] + .01f}", $"{pos[0] + .6f} {pos[3] - .01f}", TextAnchor.MiddleLeft);
            UI.CreateLabel(ref container, panelName, UIColors["white"], GetKitCooldown(kit).ToString(), 12, $"{pos[0] + .66f} {pos[1] + .01f}", $"{pos[0] + .72f} {pos[3] - .01f}");
            UI.CreateLabel(ref container, panelName, UIColors["white"], GetKitMax(kit).ToString(), 12, $"{pos[0] + .72f} {pos[1] + .01f}", $"{pos[0] + .78f} {pos[3] - .01f}");
            UI.CreateButton(ref container, panelName, UIColors["dark"], GetLang("Redeem"), 12, $"{pos[2] - .16f} {pos[1] + .01f}", $"{pos[2] - .09f} {pos[3] - .01f}", $"UI_RedeemKit {kit}");
            if (UniversalUIInfo[player.userID].admin)
                UI.CreateButton(ref container, panelName, UIColors["buttonred"], GetLang("Remove"), 12, $"{pos[2] - .08f} {pos[1] + .01f}", $"{pos[2] - .01f} {pos[3] - .01f}", $"UI_RemoveKit {kit}");
        }

        private void AddKit(BasePlayer player, int page = 0)
        {
            double count = GetKitNames().Count() - configData.sections[UniversalUIInfo[player.userID].section].pages[UniversalUIInfo[player.userID].page].Kits.Count();
            if (count == 0)
            {
                GetSendMSG(player, "NoKitsFound");
                return;
            }
            CuiHelper.DestroyUi(player, PanelInfo);
            var element = UI.CreateElementContainer(PanelInfo, UIColors["dark"], "0.2 0.15", "0.8 0.85", true);
            UI.CreatePanel(ref element, PanelInfo, UIColors["light"], "0.01 0.015", ".99 .985");
            UI.CreateLabel(ref element, PanelInfo, UIColors["limegreen"], GetMSG("SelectKit"), 20, "0.05 .95", ".95 1", TextAnchor.MiddleCenter);
            int entriesallowed = 30;
            double remainingentries = count - (page * (entriesallowed));
            double totalpages = (Math.Floor(count / (entriesallowed)));
            {
                if (page < totalpages - 1)
                {
                    UI.CreateButton(ref element, PanelInfo, UIColors["dark"], GetLang("Last"), 16, "0.8 0.02", "0.85 0.075", $"UI_SelectKitToAdd {totalpages}");
                }
                if (remainingentries > entriesallowed)
                {
                    UI.CreateButton(ref element, PanelInfo, UIColors["dark"], GetLang("Next"), 16, "0.74 0.02", "0.79 0.075", $"UI_SelectKitToAdd {page + 1}");
                }
                if (page > 0)
                {
                    UI.CreateButton(ref element, PanelInfo, UIColors["dark"], GetLang("Back"), 16, "0.68 0.02", "0.73 0.075", $"UI_SelectKitToAdd {page - 1}");
                }
                if (page > 1)
                {
                    UI.CreateButton(ref element, PanelInfo, UIColors["dark"], GetLang("First"), 16, "0.62 0.02", "0.67 0.075", $"UI_SelectKitToAdd {0}");
                }
            }
            var i = 0;
            int n = 0;
            double shownentries = page * entriesallowed;
            foreach (string kitname in GetKitNames().Where(k => !configData.sections[UniversalUIInfo[player.userID].section].pages[UniversalUIInfo[player.userID].page].Kits.Contains(k)))
            {
                i++;
                if (i < shownentries + 1) continue;
                else if (i <= shownentries + entriesallowed)
                {
                    CreateKitButton(ref element, PanelInfo, UIColors["buttonbg"], kitname, $"UI_AddKit {kitname}", n);
                    n++;
                }
            }
            UI.CreateButton(ref element, PanelInfo, UIColors["buttonred"], GetLang("Quit"), 16, "0.87 0.02", "0.97 0.075", $"UI_DestroyUniversalUI");
            CuiHelper.AddUi(player, element);
        }

        private void CreateKitButton(ref CuiElementContainer container, string panelName, string color, string name, string cmd, int num)
        {
            var pos = CalcKitButtonPos(num);
            UI.CreateButton(ref container, panelName, color, name, 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", cmd);
        }

        [ConsoleCommand("UI_SelectKitToAdd")]
        private void cmdUI_SelectKitToAdd(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            int page;
            if (!int.TryParse(arg.Args[0], out page)) return;
            AddKit(player, page);
        }

        [ConsoleCommand("UI_AddKit")]
        private void cmdUI_AddKit(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var kit = arg.Args[0];
            configData.sections[UniversalUIInfo[player.userID].section].pages[UniversalUIInfo[player.userID].page].Kits.Add(kit);
            Config.WriteObject(configData, true);
            OpenUniversalUI(player);
        }

        [ConsoleCommand("UI_RedeemKit")]
        private void cmdUI_RedeemKit(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var kit = arg.Args[0];
            object isKit = Kits?.Call("isKit", new object[] { kit });
            if (isKit is bool)
                if ((bool)isKit)
                {
                    Kits?.Call("TryGiveKit", player, kit);
                }
            UniversalUIPanel(player);
        }

        [ConsoleCommand("UI_RemoveKit")]
        private void cmdUI_RemoveKit(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var kit = arg.Args[0];
            configData.sections[UniversalUIInfo[player.userID].section].pages[UniversalUIInfo[player.userID].page].Kits.Remove(kit);
            Config.WriteObject(configData, true);
            OpenUniversalUI(player);
        }


        private float[] CalcKitButtonPos(int number)
        {
            Vector2 position = new Vector2(0.05f, 0.82f);
            Vector2 dimensions = new Vector2(0.125f, 0.125f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 6)
            {
                offsetX = (0.03f + dimensions.x) * number;
            }
            if (number > 5 && number < 12)
            {
                offsetX = (0.03f + dimensions.x) * (number - 6);
                offsetY = (-0.06f - dimensions.y) * 1;
            }
            if (number > 11 && number < 18)
            {
                offsetX = (0.03f + dimensions.x) * (number - 12);
                offsetY = (-0.06f - dimensions.y) * 2;
            }
            if (number > 17 && number < 24)
            {
                offsetX = (0.03f + dimensions.x) * (number - 18);
                offsetY = (-0.06f - dimensions.y) * 3;
            }
            if (number > 23 && number < 36)
            {
                offsetX = (0.03f + dimensions.x) * (number - 24);
                offsetY = (-0.06f - dimensions.y) * 4;
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        private float[] KitPos(int number)
        {
            Vector2 position = new Vector2(0.015f, 0.71f);
            Vector2 dimensions = new Vector2(0.965f, 0.07f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 10)
            {
                offsetY = (-0.005f - dimensions.y) * number;
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        private float[] CalcButtonPos(int number)
        {
            Vector2 position = new Vector2(0.03f, 0.95f);
            Vector2 dimensions = new Vector2(0.45f, 0.1f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 10)
            {
                offsetY = (-0.01f - dimensions.y) * number;
            }
            if (number > 9 && number < 20)
            {
                offsetY = (-0.01f - dimensions.y) * (number - 10);
                offsetX = (0.01f + dimensions.x) * 1;
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        private float[] CalcButtonPagePos(int number)
        {
            Vector2 position = new Vector2(0.03f, 0.75f);
            Vector2 dimensions = new Vector2(0.125f, 0.05f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 10)
            {
                offsetY = (-0.01f - dimensions.y) * number;
            }
            if (number > 9 && number < 20)
            {
                offsetY = (-0.01f - dimensions.y) * (number - 10);
                offsetX = (0.01f + dimensions.x) * 1;
            }
            if (number > 19 && number < 30)
            {
                offsetY = (-0.01f - dimensions.y) * (number - 20);
                offsetX = (0.01f + dimensions.x) * 2;
            }
            if (number > 29 && number < 40)
            {
                offsetY = (-0.01f - dimensions.y) * (number - 30);
                offsetX = (0.01f + dimensions.x) * 3;
            }
            if (number > 39 && number < 50)
            {
                offsetY = (-0.01f - dimensions.y) * (number - 40);
                offsetX = (0.01f + dimensions.x) * 4;
            }
            if (number > 49 && number < 60)
            {
                offsetY = (-0.01f - dimensions.y) * (number - 50);
                offsetX = (0.01f + dimensions.x) * 5;
            }
            if (number > 59 && number < 70)
            {
                offsetY = (-0.01f - dimensions.y) * (number - 60);
                offsetX = (0.01f + dimensions.x) * 6;
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        private float[] CalcSectionButtonPos(int number)
        {
            number--;
            Vector2 position = new Vector2(0.23f, 0.9f);
            Vector2 dimensions = new Vector2(0.13f, 0.075f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 5)
            {
                offsetX = (0.01f + dimensions.x) * number;
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        private float[] CalcInfoButtonPos(int number)
        {
            Vector2 position = new Vector2(0.85f, 0.75f);
            Vector2 dimensions = new Vector2(0.125f, 0.05f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 10)
            {
                offsetY = (-0.01f - dimensions.y) * number;
            }
            if (number > 9 && number < 20)
            {
                offsetX = (-0.01f - dimensions.x) * 1;
                offsetY = (-0.01f - dimensions.y) * (number - 10);
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        #endregion

        #region Class

        class MainButton
        {
            public string name;
            public string command;
            public string ButtonImage;
            public bool adminOnly;
            public string permission;
            public bool CloseUI;
        }

        class PageButton
        {
            public string name;
            public string command;
            public string PageButtonImage;
            public bool adminOnly;
            public int order;
            public string permission;
            public bool CloseUI;
        }

        class Section
        {
            public string name;
            public string SectionButtonimage;
            public bool adminOnly;
            public List<Page> pages = new List<Page>();
            public string permission;
        }

        class Page
        {
            public int page;
            public string name;
            public string text;
            public string PageImage;
            public List<PageButton> buttons = new List<PageButton>();
            public PageType type = PageType.standard;
            public List<string> Kits = new List<string>();
        }

        enum PageType
        {
            standard,
            text,
            buttons,
            kits
        }

        #endregion

        #region Misc Commands

        [ChatCommand("ui")]
        private void cmdui(BasePlayer player, string command, string[] args)
        {
            if (UniversalUIInfo.ContainsKey(player.userID) && !UniversalUIInfo[player.userID].open)
            {
                UniversalUIInfo[player.userID].open = true;
                OpenUniversalUI(player);
            }
            else
                OpenUniversalUI(player);
        }

        [ConsoleCommand("UI_AdminView")]
        private void cmdUI_AdminView(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (UniversalUIInfo[player.userID].admin == true)
                UniversalUIInfo[player.userID].admin = false;
            else UniversalUIInfo[player.userID].admin = true;
            OpenUniversalUI(player);
        }


        [ChatCommand("uuidebug")]
        private void cmduuidebug(BasePlayer player, string command, string[] args)
        {
            if (Debugging)
                Debugging = false;
            else Debugging = true;
        }

        [ConsoleCommand("GetAllImages")]
        private void cmdGetAllImages(ConsoleSystem.Arg arg)
        {
            GetAllImages();
        }

        private void GetAllImages()
        {
            if (timers.ContainsKey("skins"))
            {
                timers["skins"].Destroy();
                timers.Remove("skins");
            }
            if (!isReady()) { Puts(GetMSG("WaitingImageLibrary")); timers.Add("skins", timer.Once(60, () => GetAllImages())); return; };
            AddImage("http://i.imgur.com/GT0ngNJ.png", "text", (ulong)ResourceId);
            AddImage("http://i.imgur.com/9IpSF2b.png", "buttons", (ulong)ResourceId);
            AddImage("http://i.imgur.com/DUpRzdb.png", "standard", (ulong)ResourceId);
            AddImage("http://i.imgur.com/GbNMTDA.png", "kits", (ulong)ResourceId);
            if (string.IsNullOrEmpty(configData.HomePage.PageImage)) configData.HomePage = DefaultHomePage;
            AddImage(configData.HomePage.PageImage, configData.HomePage.name, (ulong)ResourceId);
            foreach (var entry in configData.buttons)
                if (!string.IsNullOrEmpty(entry.Value.ButtonImage))
                    AddImage(entry.Value.ButtonImage, $"UUIMainButton{entry.Key}", (ulong)ResourceId);
            foreach (var entry in configData.sections)
            {
                if (!string.IsNullOrEmpty(entry.Value.SectionButtonimage))
                    AddImage(entry.Value.SectionButtonimage, $"UUISectionButton{entry.Key}", (ulong)ResourceId);
                foreach (var page in entry.Value.pages)
                {
                    if (!string.IsNullOrEmpty(page.PageImage))
                        AddImage(page.PageImage, $"UUIPage{entry.Key}-{page.page}", (ulong)ResourceId);
                    foreach (var button in page.buttons)
                        if (!string.IsNullOrEmpty(button.PageButtonImage))
                            AddImage(button.PageButtonImage, $"UUIPageButton{entry.Key}-{page.page}-{button.order}", (ulong)ResourceId);
                }
            }
            if (timers.ContainsKey("skins"))
            {
                timers["skins"].Destroy();
                timers.Remove("skins");
            }
            SkinsReady = true;
            Puts(GetMSG("AllImagesInitialized"));
        }

        private void CheckNewImages()
        {
            if (string.IsNullOrEmpty(configData.HomePage.PageImage)) configData.HomePage = DefaultHomePage;
            if (!Valid(configData.HomePage.name))
                AddImage(configData.HomePage.PageImage, configData.HomePage.name, (ulong)ResourceId);
            foreach (var entry in configData.buttons)
                if (!string.IsNullOrEmpty(entry.Value.ButtonImage))
                    if (!Valid($"UUIMainButton{entry.Key}"))
                        AddImage(entry.Value.ButtonImage, $"UUIMainButton{entry.Key}", (ulong)ResourceId);
            foreach (var entry in configData.sections)
            {
                if (!string.IsNullOrEmpty(entry.Value.SectionButtonimage))
                    if (!Valid($"UUISectionButton{entry.Key}"))
                        AddImage(entry.Value.SectionButtonimage, $"UUISectionButton{entry.Key}", (ulong)ResourceId);
                foreach (var page in entry.Value.pages)
                {
                    if (!string.IsNullOrEmpty(page.PageImage))
                        if (!Valid($"UUIPage{entry.Key}-{page.page}"))
                            AddImage(page.PageImage, $"UUIPage{entry.Key}-{page.page}", (ulong)ResourceId);
                    foreach (var button in page.buttons)
                        if (!string.IsNullOrEmpty(button.PageButtonImage))
                            if (!Valid($"UUIPageButton{entry.Key}-{page.page}-{button.order}"))
                                AddImage(button.PageButtonImage, $"UUIPageButton{entry.Key}-{page.page}-{button.order}", (ulong)ResourceId);
                }
            }
        }

        #endregion

        #region Timers
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
                if (configData.MenuKeyBinding != "")
                    GetSendMSG(p, "UIInfo", configData.MenuKeyBinding.ToString());
                else GetSendMSG(p, "UIInfo1");
            }
            timers.Add("info", timer.Once(configData.InfoInterval * 60, () => InfoLoop()));
        }
        #endregion

        #region Config        
        private ConfigData configData;
        class ConfigData
        {
            public string MenuKeyBinding { get; set; }
            public Page HomePage = new Page();
            public Dictionary<int, Section> sections = new Dictionary<int, Section>();
            public Dictionary<int, MainButton> buttons = new Dictionary<int, MainButton>();
            public bool UseInfoPanel { get; set; }
            public bool UseButtonPanel { get; set; }
            public int InfoInterval { get; set; }
            public bool ForceOpenOnJoin { get; set; }
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        private Page DefaultHomePage = new Page
        {
            PageImage = "http://i.imgur.com/ygJ6m7w.png",
            name = "HomePage",
        };

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                MenuKeyBinding = "f5",
                UseInfoPanel = true,
                UseButtonPanel = true,
                InfoInterval = 15,
                ForceOpenOnJoin = false,
                HomePage = new Page
                {
                    PageImage = "http://i.imgur.com/ygJ6m7w.png",
                    name = "HomePage",
                    buttons = new List<PageButton> {
                        new PageButton {order = 0 },
                        new PageButton {order = 1 },
                        new PageButton {order = 2 },
                        new PageButton {order = 3 }
                    } },
                sections = new Dictionary<int, Section>
                    {
                    {1, new Section
                    {pages = new List<Page>
                    {
                    { new Page
                {page = 0, buttons = new List<PageButton> {new PageButton
                { order = 0 },new PageButton
                { order = 1 },new PageButton
                { order = 2 },new PageButton
                { order = 3 } } } },
                    { new Page
                {page = 1, buttons = new List<PageButton> {new PageButton
                { order = 0 },new PageButton
                { order = 1 },new PageButton
                { order = 2 },new PageButton
                { order = 3 } } } },
                    { new Page
                {page = 2, buttons = new List<PageButton> {new PageButton
                { order = 0 },new PageButton
                { order = 1 },new PageButton
                { order = 2 },new PageButton
                { order = 3 } } } }
                    } } },
                    {2, new Section
                    {pages = new List<Page>
                    {
                    { new Page
                {page = 0, buttons = new List<PageButton> {new PageButton
                { order = 0 },new PageButton
                { order = 1 },new PageButton
                { order = 2 },new PageButton
                { order = 3 } } } },
                    { new Page
                {page = 1, buttons = new List<PageButton> {new PageButton
                { order = 0 },new PageButton
                { order = 1 },new PageButton
                { order = 2 },new PageButton
                { order = 3 } } } },
                    { new Page
                {page = 2, buttons = new List<PageButton> {new PageButton
                { order = 0 },new PageButton
                { order = 1 },new PageButton
                { order = 2 },new PageButton
                { order = 3 } } } }
                    } } },
                    {3, new Section
                    {pages = new List<Page>
                    {
                    { new Page
                {page = 0, buttons = new List<PageButton> {new PageButton
                { order = 0 },new PageButton
                { order = 1 },new PageButton
                { order = 2 },new PageButton
                { order = 3 } } } },
                    { new Page
                {page = 1, buttons = new List<PageButton> {new PageButton
                { order = 0 },new PageButton
                { order = 1 },new PageButton
                { order = 2 },new PageButton
                { order = 3 } } } },
                    { new Page
                {page = 2, buttons = new List<PageButton> {new PageButton
                { order = 0 },new PageButton
                { order = 1 },new PageButton
                { order = 2 },new PageButton
                { order = 3 } } } }
                    } } },
                },

                buttons = new Dictionary<int, MainButton>
                {
                    {0, new MainButton() },
                    {1, new MainButton() },
                    {2, new MainButton() },
                    {3, new MainButton() },
                    {4, new MainButton() },
                    {5, new MainButton() },
                    {6, new MainButton() },
                    {7, new MainButton() },
                    {8, new MainButton() },
                    {9, new MainButton() },
                    {10, new MainButton() },
                    {11, new MainButton() },
                    {12, new MainButton() },
                    {13, new MainButton() },
                    {14, new MainButton() },
                    {15, new MainButton() },
                    {16, new MainButton() },
                    {17, new MainButton() },
                    {18, new MainButton() },
                    {19, new MainButton() },
                }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Messages
        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"title", "UniversalUI: " },
            {"UIInfo", "This server is running Universal UI. Press <color=yellow>( {0} )</color> or Type <color=yellow>( /ui )</color> to access the Menu."},
            {"UIInfo1", "This server is running Universal UI. Type <color=yellow>( /ui )</color> to access the Menu."},
            {"InfoPanel","Show Info" },
            {"HideInfoPanel", "Hide Info" },
            {"NotAuth", "You are not authorized." },
            {"DelayedCMD", "You have selected a Delayed Command. To finish the command please type a 'parameter' for command: {0}. Or Type 'quit' to exit." },
            {"ExitDelayed", "You have exited the Delayed Command." },
            {"Type", "Type: {0}" },
            {"ImageLibraryNotReady", "ImageLibrary is not loaded. Unable to open the GUI." },
            {"WaitingImageLibrary", "Waiting on Image Library to initialize. Trying again in 60 Seconds" },
            {"AllImagesInitialized", "All Images Initiailized" },
        };
        #endregion

    }
}