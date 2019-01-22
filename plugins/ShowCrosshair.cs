// Requires: ImageLibrary
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System;

using Oxide.Core.Plugins;
using System.Globalization;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("ShowCrosshair", "Marat, edited by Ловец Душ", "1.0.79", ResourceId = 2057)]
    [Description("Shows a crosshair on the screen.")]
    class ShowCrosshair : RustPlugin
    {
        private List<string> WeaponList = new List<string>();
        private Hash<ulong, Timer> popupMessages = new Hash<ulong, Timer>();
        static bool uiFadeIn;
        private bool isILReady;
        readonly Dictionary<string, string> lastCrosshair = new Dictionary<string, string>();
        readonly Dictionary<string, bool> enabled = new Dictionary<string, bool>();
        readonly Dictionary<string, bool> opened = new Dictionary<string, bool>();

        List<ulong> Cross = new List<ulong>();
        List<ulong> Menu = new List<ulong>();
        bool EnableCross(BasePlayer player) => Cross.Contains(player.userID);
        bool EnableMenu(BasePlayer player) => Menu.Contains(player.userID);

        #region Initialization

        private bool configChanged;
        private const string permShowCrosshair = "showcrosshair.allowed";

        private string GetImage(string name, ulong skin = 0)
        {
            string imageId = ImageLibrary.GetImage(name, skin);
            if (!string.IsNullOrEmpty(imageId))
                return imageId;
            return string.Empty;
        }

        private void Loaded()
        {
            LoadConfiguration();
            LoadDefaultMessages();
            permission.RegisterPermission(permShowCrosshair, this);
            cmd.AddChatCommand(commandmenu, this, "cmdChatShowMenu");
        }

        void OnServerInitialized()
        {
            ValidateImages();
        }

        void ValidateImages()
        {
            Puts("[Warning] Validating imagery");
            if (!ImageLibrary.HasImage("crosshair9", 0))
            {
                LoadImages();
            }
            Puts("[Warning] Images loading validate sucsessefull");
            isILReady = true;
        }

        private void LoadImages()
        {
            if (!ImageLibrary.IsReady())
            {
                timer.In(10, () => LoadImages());
                Puts("[Warning] Waiting for Image Library to finish processing images");
                return;
            }
            ImageLibrary.AddImage(image1, "crosshair1", 0);
            ImageLibrary.AddImage(image2, "crosshair2", 0);
            ImageLibrary.AddImage(image3, "crosshair3", 0);
            ImageLibrary.AddImage(image4, "crosshair4", 0);
            ImageLibrary.AddImage(image5, "crosshair5", 0);
            ImageLibrary.AddImage(image6, "crosshair6", 0);
            ImageLibrary.AddImage(image7, "crosshair7", 0);
            ImageLibrary.AddImage(image8, "crosshair8", 0);
            ImageLibrary.AddImage(background, "background", 0);
            ImageLibrary.AddImage(background2, "background2", 0);
            isILReady = true;
            Puts("Crosshair images loaded");
        }

        #endregion

        #region Configuration

        protected override void LoadDefaultConfig()
        {
            PrintWarning("New configuration file created.");
            Config.Clear();
        }
        [PluginReference]
        ImageLibrary ImageLibrary;
        private bool usePermissions = false;
        private bool ShowOnLogin = false;
        private bool HideWhenAiming = false;
        private bool EnableSound = true;
        private bool ShowMessage = true;
        private string SoundOpen = "assets/bundled/prefabs/fx/build/promote_metal.prefab";
        private string SoundDisable = "assets/prefabs/locks/keypad/effects/lock.code.lock.prefab";
        private string SoundSelect = "assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab";
        private string SoundToggle = "assets/prefabs/misc/xmas/presents/effects/unwrap.prefab";
        private string commandmenu = "crosshair";
        private string colorClose = "0 0 0 0.7";
        private string colorBackground = "0 0 0 0.7";
        private string colorToggle = "0 0 0 0.7";
        private string colorDisable = "0 0 0 0.7";
        private string image1 = "https://image.ibb.co/jC4Fe7/image1.png";
        private string image2 = "https://image.ibb.co/k0zFe7/image2.png";
        private string image3 = "https://image.ibb.co/fjP3XS/image3.png";
        private string image4 = "https://image.ibb.co/erAcsS/image4.png";
        private string image5 = "https://image.ibb.co/fufcsS/image5.png";
        private string image6 = "https://image.ibb.co/cxymmn/image6.png";
        private string image7 = "https://image.ibb.co/j271K7/image7.png";
        private string image8 = "https://image.ibb.co/dko8z7/image8.png";
        private string background = "https://image.ibb.co/fNyDXS/background.png";
        private string background2 = "http://i.imgur.com/mYV1bFs.png";

        private void LoadConfiguration()
        {
            commandmenu = GetConfig("Options", "CommandMenu", commandmenu);
            ShowMessage = GetConfig("Options", "ShowMessage", ShowMessage);
            ShowOnLogin = GetConfig("Options", "ShowOnLogin", ShowOnLogin);
            HideWhenAiming = GetConfig("Options", "HideWhenAiming", HideWhenAiming);
            EnableSound = GetConfig("Options", "EnableSound", EnableSound);
            usePermissions = GetConfig("Options", "UsePermissions", usePermissions);

            SoundOpen = GetConfig("Sound", "SoundOpen", SoundOpen);
            SoundDisable = GetConfig("Sound", "SoundDisable", SoundDisable);
            SoundSelect = GetConfig("Sound", "SoundSelect", SoundSelect);
            SoundToggle = GetConfig("Sound", "SoundToggle", SoundToggle);

            colorClose = GetConfig("Color", "ColorButtonClose", colorClose);
            colorToggle = GetConfig("Color", "ColorButtonToggle", colorToggle);
            colorDisable = GetConfig("Color", "ColorButtonDisable", colorDisable);
            colorBackground = GetConfig("Color", "ColorBackground", colorBackground);

            image1 = GetConfig("Image", "crosshair1", image1);
            image2 = GetConfig("Image", "crosshair2", image2);
            image3 = GetConfig("Image", "crosshair3", image3);
            image4 = GetConfig("Image", "crosshair4", image4);
            image5 = GetConfig("Image", "crosshair5", image5);
            image6 = GetConfig("Image", "crosshair6", image6);
            image7 = GetConfig("Image", "crosshair7", image7);
            image8 = GetConfig("Image", "crosshair8", image8);

            background = GetConfig("Image", "background", background);
            background2 = GetConfig("Image", "background2", background2);

            List<object> weaponList = new List<object>()
            { 
                    "Eoka Pistol",
                    "Custom SMG",
                    "Assault Rifle",
                    "Bolt Action Rifle",
                    "Waterpipe Shotgun",
                    "Revolver",
                    "Thompson",
                    "Semi-Automatic Rifle",
                    "Semi-Automatic Pistol",
                    "Pump Shotgun",
                    "M249",
                    "Flame Thrower",
                    "Double Barrel Shotgun",
                    "MP5A4",
                    "LR-300 Assault Rifle",
                    "M92 Pistol",
                    "Python Revolver",
                    "Crossbow"
            };
            weaponList = GetConfig("Options", "List of weapons with disabled crosshair while aiming", weaponList);
            foreach (var item in weaponList)
            {
                WeaponList.Add(item.ToString());
            }

            if (!configChanged) return;
            PrintWarning("Configuration file updated.");
            SaveConfig();
        }
       
        private T GetConfig<T>(string category, string setting, T defaultValue)
        {
            var data = Config[category] as Dictionary<string, object>;
            object value;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[category] = data;
                configChanged = true;
            }
            if (data.TryGetValue(setting, out value)) return (T)Convert.ChangeType(value, typeof(T));
            value = defaultValue;
            data[setting] = value;
            configChanged = true;
            return (T)Convert.ChangeType(value, typeof(T));
        }

        #endregion

        #region Localization

        private void LoadDefaultMessages()
        {
            //English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You don't have permission to use this command.",
                ["imWait"] = "You must wait until ImageLibrary has finished processing its images",
                ["Enabled"] = "You have enabled the crosshair.",
                ["Disabled"] = "You have disabled the crosshair.",
                ["crosshair1"] = "You set the crosshair №1.",
                ["crosshair2"] = "You set the crosshair №2.",
                ["crosshair3"] = "You set the crosshair №3.",
                ["crosshair4"] = "You set the crosshair №4.",
                ["crosshair5"] = "You set the crosshair №5.",
                ["crosshair6"] = "You set the crosshair №6.",
                ["crosshair7"] = "You set the crosshair №7.",
                ["crosshair8"] = "You set the crosshair №8.",
                ["close"] = "<color=#ff0000>C</color><color=#ff1a1a>l</color><color=#ff3333>o</color><color=#ff1a1a>s</color><color=#ff0000>e</color>",
                ["select"] = "<color=#46d100>S</color><color=#52f500>e</color><color=#66ff1a>l</color><color=#52f500>e</color><color=#46d100>c</color><color=#3aad00>t</color>",
                ["disable"] = "<color=#fbff00>D</color><color=#fbff1a>i</color><color=#fcff33>s</color><color=#fcff4d>a</color><color=#fcff33>b</color><color=#fbff1a>l</color><color=#fbff00>e</color>",
                ["next"] = "<color=#0055ff>N</color><color=#1a66ff>e</color><color=#1a66ff>x</color><color=#0055ff>t</color>",
                ["back"] = "<color=#0055ff>B</color><color=#1a66ff>a</color><color=#1a66ff>c</color><color=#0055ff>k</color>"
            }, this, "en");

            //Russian
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "У вас нет разрешения на использование этой команды",
                ["imWait"] = "Вы должны подождать, пока ImageLibrary закончит обработку изображений",
                ["Enabled"] = "Вы включили прицел",
                ["Disabled"] = "Вы отключили прицел",
                ["crosshair1"] = "Вы установили прицел №1",
                ["crosshair2"] = "Вы установили прицел №2",
                ["crosshair3"] = "Вы установили прицел №3",
                ["crosshair4"] = "Вы установили прицел №4",
                ["crosshair5"] = "Вы установили прицел №5",
                ["crosshair6"] = "Вы установили прицел №6",
                ["crosshair7"] = "Вы установили прицел №7",
                ["crosshair8"] = "Вы установили прицел №8",
                ["close"] = "<color=#ff0000>З</color><color=#ff1a1a>а</color><color=#ff3333>к</color><color=#ff4d4d>р</color><color=#ff3333>ы</color><color=#ff1a1a>т</color><color=#ff0000>ь</color>",
                ["select"] = "<color=#3aad00>В</color><color=#46d100>ы</color><color=#52f500>б</color><color=#66ff1a>р</color><color=#52f500>а</color><color=#46d100>т</color><color=#3aad00>ь</color>",
                ["disable"] = "<color=#e2e600>О</color><color=#fbff00>т</color><color=#fbff1a>к</color><color=#fcff33>л</color><color=#fcff4d>ю</color><color=#fcff33>ч</color><color=#fbff1a>и</color><color=#fbff00>т</color><color=#e2e600>ь</color>",
                ["next"] = "<color=#0055ff>Д</color><color=#1a66ff>а</color><color=#3377ff>л</color><color=#1a66ff>е</color><color=#0055ff>е</color>",
                ["back"] = "<color=#0055ff>Н</color><color=#1a66ff>а</color><color=#3377ff>з</color><color=#1a66ff>а</color><color=#0055ff>д</color>"
            }, this, "ru");
        }

        #endregion

        #region Commands

        ////ShowMenu////
        private void cmdChatShowMenu(BasePlayer player)
        {
            if (usePermissions && !IsAllowed(player.UserIDString, permShowCrosshair))
            {
                Reply(player, Lang("NoPermission", player.UserIDString));
                return;
            }
            if (!isILReady)
            {
                SendReply(player, "", Lang("imWait", player.UserIDString));
                return;
            }
            if (EnableMenu(player))
            {
                DisabledMenu(player);
            }
            else
            {
                EnabledMenu(player);
                if (EnableSound) Effect.server.Run(SoundOpen, player.transform.position, Vector3.zero, null, false);
            }
        }

        [ConsoleCommand("crosshair")]
        private void cmdConsoleShowMenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!isILReady)
            {
                SendReply(player, "", Lang("imWait", player.UserIDString));
                return;
            }
            cmdChatShowMenu(player);
        }

        [ConsoleCommand("loadcrosshairimages")]
        private void cmdRefreshAllImages(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null || arg.Connection.authLevel > 0)
            {
                PrintWarning("Reloading all images crosshair!");
                LoadImages();
            }
        }

        ////CloseMenu////
        [ConsoleCommand("CloseMenu")]
        void cmdConsoleCloseMenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            DisabledMenu(player);
        }
        ////Commands////
        [ConsoleCommand("command1")]
        void cmdConsoleCommand1(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            DestroyCrosshair(player);
            Crosshair1(player);
            if (EnableSound) Effect.server.Run(SoundSelect, player.transform.position, Vector3.zero, null, false);
            if (ShowMessage) OnScreen(player, Lang("crosshair1", player.UserIDString));//Reply(player, Lang("crosshair1", player.UserIDString));
        }
        [ConsoleCommand("command2")]
        void cmdConsoleCommand2(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            DestroyCrosshair(player);
            Crosshair2(player);
            if (EnableSound) Effect.server.Run(SoundSelect, player.transform.position, Vector3.zero, null, false);
            if (ShowMessage) OnScreen(player, Lang("crosshair2", player.UserIDString));//Reply(player, Lang("crosshair2", player.UserIDString));
        }
        [ConsoleCommand("command3")]
        void cmdConsoleCommand3(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            DestroyCrosshair(player);
            Crosshair3(player);
            if (EnableSound) Effect.server.Run(SoundSelect, player.transform.position, Vector3.zero, null, false);
            if (ShowMessage) OnScreen(player, Lang("crosshair3", player.UserIDString));//Reply(player, Lang("crosshair3", player.UserIDString));
        }
        [ConsoleCommand("command4")]
        void cmdConsoleCommand4(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            DestroyCrosshair(player);
            Crosshair4(player);
            if (EnableSound) Effect.server.Run(SoundSelect, player.transform.position, Vector3.zero, null, false);
            if (ShowMessage) OnScreen(player, Lang("crosshair4", player.UserIDString));//Reply(player, Lang("crosshair4", player.UserIDString));
        }
        [ConsoleCommand("command5")]
        void cmdConsoleCommand5(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            DestroyCrosshair(player);
            Crosshair5(player);
            if (EnableSound) Effect.server.Run(SoundSelect, player.transform.position, Vector3.zero, null, false);
            if (ShowMessage) OnScreen(player, Lang("crosshair5", player.UserIDString));//Reply(player, Lang("crosshair5", player.UserIDString));
        }
        [ConsoleCommand("command6")]
        void cmdConsoleCommand6(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            DestroyCrosshair(player);
            Crosshair6(player);
            if (EnableSound) Effect.server.Run(SoundSelect, player.transform.position, Vector3.zero, null, false);
            if (ShowMessage) OnScreen(player, Lang("crosshair6", player.UserIDString));//Reply(player, Lang("crosshair6", player.UserIDString));
        }
        [ConsoleCommand("command7")]
        void cmdConsoleCommand7(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            DestroyCrosshair(player);
            Crosshair7(player);
            if (EnableSound) Effect.server.Run(SoundSelect, player.transform.position, Vector3.zero, null, false);
            if (ShowMessage) OnScreen(player, Lang("crosshair7", player.UserIDString));//Reply(player, Lang("crosshair7", player.UserIDString));
        }
        [ConsoleCommand("command8")]
        void cmdConsoleCommand8(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            DestroyCrosshair(player);
            Crosshair8(player);
            if (EnableSound) Effect.server.Run(SoundSelect, player.transform.position, Vector3.zero, null, false);
            if (ShowMessage) OnScreen(player, Lang("crosshair8", player.UserIDString));//Reply(player, Lang("crosshair8", player.UserIDString));
        }
        [ConsoleCommand("commandNext")]
        void cmdConsoleCommandNext(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            DestroyGUImenu(player);
            var panel = Convert.ToInt16(arg.Args[0]);
            if (panel == 1)
                ShowMenu(player, null);
            else 
                NextMenu2(player, null);
            //else if (panel == 3)
            //    NextMenu3(player, null);
            if (EnableSound) Effect.server.Run(SoundToggle, player.transform.position, Vector3.zero, null, false);
        }
        [ConsoleCommand("commandBack")]
        void cmdConsoleCommandBack(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            DestroyGUImenu(player);

            var panel = Convert.ToInt16(arg.Args[0]);
            if (panel == 1)
                ShowMenu(player, null);
            else 
                NextMenu2(player, null);
            //else if (panel == 3)
            //    NextMenu3(player, null);
            if (EnableSound) Effect.server.Run(SoundToggle, player.transform.position, Vector3.zero, null, false);
        }
        [ConsoleCommand("commandDisable")]
        void cmdConsoleCommandDisable(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            DestroyCrosshair(player);
            if (EnableSound) Effect.server.Run(SoundDisable, player.transform.position, Vector3.zero, null, false);
            enabled[player.UserIDString] = false;
            lastCrosshair.Remove(player.UserIDString);
            if (ShowMessage) OnScreen(player, Lang("Disabled", player.UserIDString));//Reply(player, Lang("Disabled", player.UserIDString));
        }

        #endregion

        #region Hooks

        private void OnPlayerInit(BasePlayer player)
        {
            if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.Once(2, () => OnPlayerInit(player));
                return;
            }
            if (usePermissions && !IsAllowed(player.UserIDString, permShowCrosshair))
            {
                return;
            }
            if (ShowOnLogin)
            {
                EnabledCrosshair(player);
            }
        }
        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (Menu.Contains(player.userID))
            {
                Menu.Remove(player.userID);
                DestroyAll(player);
                return;
            }
        }
        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                Menu.Remove(player.userID);
                DestroyAll(player);
                return;
            }
        }
        private void DestroyAll(BasePlayer player)
        {
            DestroyGUImenu(player);
            DestroyCrosshair(player);
        }
        private void DestroyCrosshair(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "image1");
            CuiHelper.DestroyUi(player, "image2");
            CuiHelper.DestroyUi(player, "image3");
            CuiHelper.DestroyUi(player, "image4");
            CuiHelper.DestroyUi(player, "image5");
            CuiHelper.DestroyUi(player, "image6");
            CuiHelper.DestroyUi(player, "image7");
            CuiHelper.DestroyUi(player, "image8");
        }
        private void DestroyGUImenu(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "GUImenu");
            CuiHelper.DestroyUi(player, "GUImenu2");
            CuiHelper.DestroyUi(player, "GUImenu3");
        }

        private void EnabledCrosshair(BasePlayer player)
        {
            if (!Cross.Contains(player.userID))
            {
                Cross.Add(player.userID);
                DestroyCrosshair(player);
                player.SendConsoleCommand("command1");
            }
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player.GetActiveItem() != null)
                if (input.IsDown(BUTTON.FIRE_SECONDARY) && WeaponList.Contains(player.GetActiveItem().info.displayName.english) && HideWhenAiming)
                {
                    opened[player.UserIDString] = false;
                    DestroyCrosshair(player);
                }
                else
                {
                    if (enabled.ContainsKey(player.UserIDString) && enabled[player.UserIDString] == true)
                    {
                        if (opened.ContainsKey(player.UserIDString) && opened[player.UserIDString] == false)
                        {
                            opened[player.UserIDString] = true;
                            if (lastCrosshair[player.UserIDString] == "crosshair1") Crosshair1(player);
                            else if (lastCrosshair[player.UserIDString] == "crosshair2") Crosshair2(player);
                            else if (lastCrosshair[player.UserIDString] == "crosshair3") Crosshair3(player);
                            else if (lastCrosshair[player.UserIDString] == "crosshair4") Crosshair4(player);
                            else if (lastCrosshair[player.UserIDString] == "crosshair5") Crosshair5(player);
                            else if (lastCrosshair[player.UserIDString] == "crosshair6") Crosshair6(player);
                            else if (lastCrosshair[player.UserIDString] == "crosshair7") Crosshair7(player);
                            else if (lastCrosshair[player.UserIDString] == "crosshair8") Crosshair8(player);
                        }
                    }
                }
        }

        private void DisabledCrosshair(BasePlayer player)
        {
            if (Cross.Contains(player.userID))
            {
                Cross.Remove(player.userID);
                player.SendConsoleCommand("commandDisable");
            }
        }
        private void EnabledMenu(BasePlayer player)
        {
            if (!Menu.Contains(player.userID))
            {
                Menu.Add(player.userID);
                DestroyGUImenu(player);
                ShowMenu(player, null);
            }
        }
        private void DisabledMenu(BasePlayer player)
        {
            if (Menu.Contains(player.userID))
            {
                Menu.Remove(player.userID);
                DestroyGUImenu(player);
            }
        }

        #endregion

        #region Crosshair

        private void PopupMessage(BasePlayer player, string msg)
        {
            var element = UI.CreateElementContainer(UIPopup, UI.Color("#2a2a2a", 0.98f), "0.33 0.45", "0.67 0.6");
            UI.CreatePanel(ref element, UIPopup, UI.Color("#696969", 0.4f), "0.01 0.04", "0.99 0.96");
            UI.CreateLabel(ref element, UIPopup, $"{UI.Color("#2a2a2a", 0.9f)}{msg}</color>", 22, "0 0", "1 1");

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

        private Dictionary<string, Timer> timers = new Dictionary<string, Timer>();
        void OnScreen(BasePlayer player, string msg)
        {
            if (timers.ContainsKey(player.userID.ToString()))
            {
                timers[player.userID.ToString()].Destroy();
                timers.Remove(player.userID.ToString());
            }
            CuiHelper.DestroyUi(player, PanelOnScreen);
            var element = UI.CreateElementContainer(PanelOnScreen, "0.0 0.0 0.0 0.0", "0.15 0.65", "0.85 .85", false);
            UI.CreateTextOutline(ref element, PanelOnScreen, UIColors["black"], UIColors["white"], msg, 24, "0.0 0.0", "1.0 1.0");
            CuiHelper.AddUi(player, element);
            timers.Add(player.userID.ToString(), timer.Once(3, () => CuiHelper.DestroyUi(player, PanelOnScreen)));
        }


        private void Crosshair1(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UIHud1);
            var elements = UI.CreateElementContainer(UIHud1, "1 1 1 0.0", "0.490 0.4812", "0.509 0.517", false);
            var image = GetImage("crosshair1", 0);
            UI.LoadImage(ref elements, UIHud1, image, "0 0", "1 1");

            if (lastCrosshair.ContainsKey(player.UserIDString)) lastCrosshair.Remove(player.UserIDString);
            if (enabled.ContainsKey(player.UserIDString)) enabled.Remove(player.UserIDString);
            lastCrosshair.Add(player.UserIDString, "crosshair1");
            enabled.Add(player.UserIDString, true);
            CuiHelper.AddUi(player, elements);
        }

        private void Crosshair2(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "image2");
            var elements = UI.CreateElementContainer("image2", "1 1 1 0.0", "0.490 0.4812", "0.509 0.517", false);
            var image = GetImage("crosshair2", 0);
            UI.LoadImage(ref elements, "image2", image, "0 0", "1 1");

            if (lastCrosshair.ContainsKey(player.UserIDString)) lastCrosshair.Remove(player.UserIDString);
            if (enabled.ContainsKey(player.UserIDString)) enabled.Remove(player.UserIDString);
            lastCrosshair.Add(player.UserIDString, "crosshair2");
            enabled.Add(player.UserIDString, true);
            CuiHelper.AddUi(player, elements);
        }

        private void Crosshair3(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "image3");
            var elements = UI.CreateElementContainer("image3", "1 1 1 0.0", "0.490 0.4812", "0.509 0.517", false);
            var image = GetImage("crosshair3", 0);
            UI.LoadImage(ref elements, "image3", image, "0 0", "1 1");

            if (lastCrosshair.ContainsKey(player.UserIDString)) lastCrosshair.Remove(player.UserIDString);
            if (enabled.ContainsKey(player.UserIDString)) enabled.Remove(player.UserIDString);
            lastCrosshair.Add(player.UserIDString, "crosshair3");
            enabled.Add(player.UserIDString, true);
            CuiHelper.AddUi(player, elements);
        }

        private void Crosshair4(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "image4");
            var elements = UI.CreateElementContainer("image4", "1 1 1 0.0", "0.490 0.4812", "0.509 0.517", false);
            var image = GetImage("crosshair4", 0);
            UI.LoadImage(ref elements, "image4", image, "0 0", "1 1");

            if (lastCrosshair.ContainsKey(player.UserIDString)) lastCrosshair.Remove(player.UserIDString);
            if (enabled.ContainsKey(player.UserIDString)) enabled.Remove(player.UserIDString);
            lastCrosshair.Add(player.UserIDString, "crosshair4");
            enabled.Add(player.UserIDString, true);
            CuiHelper.AddUi(player, elements);
        }

        private void Crosshair5(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "image5");
            var elements = UI.CreateElementContainer("image5", "1 1 1 0.0", "0.490 0.4812", "0.509 0.517", false);
            var image = GetImage("crosshair5", 0);
            UI.LoadImage(ref elements, "image5", image, "0 0", "1 1");

            if (lastCrosshair.ContainsKey(player.UserIDString)) lastCrosshair.Remove(player.UserIDString);
            if (enabled.ContainsKey(player.UserIDString)) enabled.Remove(player.UserIDString);
            lastCrosshair.Add(player.UserIDString, "crosshair5");
            enabled.Add(player.UserIDString, true);
            CuiHelper.AddUi(player, elements);
        }

        private void Crosshair6(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "image6");
            var elements = UI.CreateElementContainer("image6", "1 1 1 0.0", "0.490 0.4812", "0.509 0.517", false);
            var image = GetImage("crosshair6", 0);
            UI.LoadImage(ref elements, "image6", image, "0 0", "1 1");

            if (lastCrosshair.ContainsKey(player.UserIDString)) lastCrosshair.Remove(player.UserIDString);
            if (enabled.ContainsKey(player.UserIDString)) enabled.Remove(player.UserIDString);
            lastCrosshair.Add(player.UserIDString, "crosshair6");
            enabled.Add(player.UserIDString, true);
            CuiHelper.AddUi(player, elements);
        }

        private void Crosshair7(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "image7");
            var elements = UI.CreateElementContainer("image7", "1 1 1 0.0", "0.490 0.4812", "0.509 0.517", false);
            var image = GetImage("crosshair7", 0);
            UI.LoadImage(ref elements, "image7", image, "0 0", "1 1");

            if (lastCrosshair.ContainsKey(player.UserIDString)) lastCrosshair.Remove(player.UserIDString);
            if (enabled.ContainsKey(player.UserIDString)) enabled.Remove(player.UserIDString);
            lastCrosshair.Add(player.UserIDString, "crosshair7");
            enabled.Add(player.UserIDString, true);
            CuiHelper.AddUi(player, elements);
        }

        private void Crosshair8(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "image8");
            var elements = UI.CreateElementContainer("image8", "1 1 1 0.0", "0.490 0.4812", "0.509 0.517", false);
            var image = GetImage("crosshair8", 0);
            UI.LoadImage(ref elements, "image8", image, "0 0", "1 1");

            if (lastCrosshair.ContainsKey(player.UserIDString)) lastCrosshair.Remove(player.UserIDString);
            if (enabled.ContainsKey(player.UserIDString)) enabled.Remove(player.UserIDString);
            lastCrosshair.Add(player.UserIDString, "crosshair8");
            enabled.Add(player.UserIDString, true);
            CuiHelper.AddUi(player, elements);
        }

        #endregion

        #region GuiMenu

        /////////////////Menu1/////////////////////

        private void ShowMenu(BasePlayer player, string text)
        {
            var elements = UI.CreateElementContainer("GUImenu", "0 0 0 0", "0.2395 0.18", "0.761 0.4525", true);
            UI.CreatePanel(ref elements, "GUImenu", colorBackground, "0 0.18", "1 1", 0.6f, true);

            ////////////MainBackground////////////////
            UI.LoadImage(ref elements, "GUImenu", GetImage("background2", 0), "0 0", "1 1");

            UI.CreateButton(ref elements, "GUImenu", colorClose, Lang("close", player.UserIDString), 18, "0.402 0", "0.596 0.18", $"CloseMenu", TextAnchor.MiddleCenter, 0.6f);

            ////////////////background///////////////
            //background1
            UI.LoadImage(ref elements, "GUImenu", GetImage("background", 0), $"0.030 0.28", $"0.240 0.9");
            //background2
            UI.LoadImage(ref elements, "GUImenu", GetImage("background", 0), $"0.262 0.28", $"0.486 0.9");
            //background3
            UI.LoadImage(ref elements, "GUImenu", GetImage("background", 0), $"0.505 0.28", $"0.730 0.9");
            //background4
            UI.LoadImage(ref elements, "GUImenu", GetImage("background", 0), $"0.750 0.28", $"0.97 0.9");

            ////////////////image////////////////
            //image1
            UI.LoadImage(ref elements, "GUImenu", GetImage("crosshair1", 0), $"0.100 0.530", $"0.150 0.680");
            //image2
            UI.LoadImage(ref elements, "GUImenu", GetImage("crosshair2", 0), $"0.352 0.530", $"0.396 0.680");
            //image3
            UI.LoadImage(ref elements, "GUImenu", GetImage("crosshair3", 0), $"0.585 0.530", $"0.655 0.680");
            //image4
            UI.LoadImage(ref elements, "GUImenu", GetImage("crosshair4", 0), $"0.815 0.530", $"0.895 0.680");

            /////////////button///////////////////
            //button1
            UI.CreateButton(ref elements, "GUImenu", "0 0 0 0", Lang("select", player.UserIDString), 20, $"0.0445 0.320", $"0.206 0.85", $"command1", TextAnchor.LowerCenter, 0.6f);
            //button2
            UI.CreateButton(ref elements, "GUImenu", "0 0 0 0", Lang("select", player.UserIDString), 20, $"0.282 0.320", $"0.476 0.85", $"command2", TextAnchor.LowerCenter, 0.6f);
            //button3
            UI.CreateButton(ref elements, "GUImenu", "0 0 0 0", Lang("select", player.UserIDString), 20, $"0.523 0.320", $"0.715 0.85", $"command3", TextAnchor.LowerCenter, 0.6f);
            //button4
            UI.CreateButton(ref elements, "GUImenu", "0 0 0 0", Lang("select", player.UserIDString), 20, $"0.762 0.320", $"0.954 0.85", $"command4", TextAnchor.LowerCenter, 0.6f);

            //buttonDisable
            UI.CreateButton(ref elements, "GUImenu", colorDisable, Lang("disable", player.UserIDString), 18, "0 0", "0.192 0.18", $"commandDisable", TextAnchor.MiddleCenter, 0.6f);
            //buttonNext
            UI.CreateButton(ref elements, "GUImenu", colorToggle, Lang("next", player.UserIDString), 18, "0.805 0", "1 0.18", $"commandNext 2", TextAnchor.MiddleCenter, 0.6f);

            CuiHelper.AddUi(player, elements);
        }

        /////////////////Menu2/////////////////////

        private void NextMenu2(BasePlayer player, string text)
        {
            var elements = UI.CreateElementContainer("GUImenu2", "0 0 0 0", "0.2395 0.18", "0.761 0.4525", true);
            UI.CreatePanel(ref elements, "GUImenu2", colorBackground, "0 0.18", "1 1", 0.6f, true);

            ////////////MainBackground////////////////
            UI.LoadImage(ref elements, "GUImenu2", GetImage("background2", 0), "0 0", "1 1");

            UI.CreateButton(ref elements, "GUImenu2", colorClose, Lang("close", player.UserIDString), 18, "0.402 0", "0.596 0.18", $"CloseMenu", TextAnchor.MiddleCenter, 0.6f);

            ////////////////background///////////////
            //background1
            UI.LoadImage(ref elements, "GUImenu2", GetImage("background", 0), $"0.030 0.28", $"0.240 0.9");
            //background2
            UI.LoadImage(ref elements, "GUImenu2", GetImage("background", 0), $"0.262 0.28", $"0.486 0.9");
            //background3
            UI.LoadImage(ref elements, "GUImenu2", GetImage("background", 0), $"0.505 0.28", $"0.730 0.9");
            //background4
            UI.LoadImage(ref elements, "GUImenu2", GetImage("background", 0), $"0.750 0.28", $"0.97 0.9");

            ////////////////image////////////////
            //image5
            UI.LoadImage(ref elements, "GUImenu2", GetImage("crosshair5", 0), $"0.100 0.530", $"0.150 0.680");
            //image6
            UI.LoadImage(ref elements, "GUImenu2", GetImage("crosshair6", 0), $"0.352 0.530", $"0.396 0.680");
            //image7
            UI.LoadImage(ref elements, "GUImenu2", GetImage("crosshair7", 0), $"0.585 0.530", $"0.655 0.680");
            //image8
            UI.LoadImage(ref elements, "GUImenu2", GetImage("crosshair8", 0), $"0.825 0.530", $"0.885 0.680");

            /////////////button///////////////////
            //button5
            UI.CreateButton(ref elements, "GUImenu2", "0 0 0 0", Lang("select", player.UserIDString), 20, $"0.0445 0.320", $"0.206 0.85", $"command5", TextAnchor.LowerCenter, 0.6f);
            //button6
            UI.CreateButton(ref elements, "GUImenu2", "0 0 0 0", Lang("select", player.UserIDString), 20, $"0.282 0.320", $"0.476 0.85", $"command6", TextAnchor.LowerCenter, 0.6f);
            //button7
            UI.CreateButton(ref elements, "GUImenu2", "0 0 0 0", Lang("select", player.UserIDString), 20, $"0.523 0.320", $"0.715 0.85", $"command7", TextAnchor.LowerCenter, 0.6f);
            //button8
            UI.CreateButton(ref elements, "GUImenu2", "0 0 0 0", Lang("select", player.UserIDString), 20, $"0.762 0.320", $"0.954 0.85", $"command8", TextAnchor.LowerCenter, 0.6f);

            //buttonDisable
            UI.CreateButton(ref elements, "GUImenu2", colorDisable, Lang("disable", player.UserIDString), 18, "0 0", "0.192 0.18", $"commandDisable", TextAnchor.MiddleCenter, 0.6f);
            //buttonBack
            UI.CreateButton(ref elements, "GUImenu2", colorToggle, Lang("back", player.UserIDString), 18, "0.805 0", "1 0.18", $"commandBack 1", TextAnchor.MiddleCenter, 0.6f);
            CuiHelper.AddUi(player, elements);
        }

        #endregion

        #region UI
        private string PanelOnScreen = "PanelOnScreen";
        string UIHud1 = "image1";
        const string UIPopup = "Popup";

        class UI
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
                    new CuiElement().Parent = "Hud",
                    panelName
                }
            };
                return NewElement;
            }
            static public void CreatePanel(ref CuiElementContainer container, string panel, string color, string aMin, string aMax, float fadein = 1.0f, bool cursor = false)
            {
                if (uiFadeIn)
                    fadein = 0;
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

        private Dictionary<string, string> UIColors = new Dictionary<string, string>
        {
            {"black", "0 0 0 1.0" },
            {"dark", "0.1 0.1 0.1 0.98" },
            {"header", "1 1 1 0.3" },
            {"light", ".564 .564 .564 1.0" },
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
        };
        #endregion

        #region Helpers

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        void Reply(BasePlayer player, string message, string args = null) => PrintToChat(player, $"{message}", args);
        bool IsAllowed(string id, string perm) => permission.UserHasPermission(id, perm);

        #endregion
    }
}