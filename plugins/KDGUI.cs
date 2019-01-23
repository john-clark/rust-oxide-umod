using System;
using System.Collections.Generic;

using UnityEngine;

using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("KDGUI", "JoeSheep", "1.0.5", ResourceId = 2466)]
    [Description("Simple Plugin For Displaying A User's Kills And Deaths On Screen.")]
    class KDGUI : RustPlugin
    {
        #region Config
        const string Perm = "kdgui.show";

        private bool ConfigUpdated = false;
        private bool GotBackgroundColor = true;
        private bool GotTextColor = true;
        private HashSet<ulong> Exclusions = new HashSet<ulong>();
        private Dictionary<ulong, ulong> WoundedDict = new Dictionary<ulong, ulong>();
        private string BackgroundColor = String.Empty;
        private string TextColor = String.Empty;
        private string AnchorMaxX = "0.693 ";
        private string AnchorMinX = "0.645 ";
        private string BackgroundGrey = "0.8 0.8 0.8 0.05";
        private string BackgroundRed = "0.5 0.1 0.1 0.05";
        private string BackgroundOrange = "0.95294 0.37255 0.06275 0.05";
        private string BackgroundYellow = "1 0.92 0.016 0.05";
        private string BackgroundGreen = "0.1 0.4 0.1 0.055";
        private string BackgroundCyan = "0 1 1 0.05";
        private string BackgroundBlue = "0.09020 0.07843 0.71765 0.05";
        private string BackgroundPurple = "0.53333 0.07843 0.77647 0.05";
        private string TextRed = "0.5 0.2 0.2";
        private string TextOrange = "0.8 0.5 0.1";
        private string TextYellow = "1 0.92 0.016";
        private string TextGreen = "0 1 0";
        private string TextCyan = "0 1 1";
        private string TextBlue = "0.09020 0.07843 0.71765";
        private string TextPurple = "0.53333 0.07843 0.77647";
        private string TextWhite = "1 1 1";

        public string backgroundColorList { get; private set; } = "Grey, Red, Orange, Yellow, Green, Cyan, Blue, Purple";
        public string textColorList { get; private set; } = "White, Red, Orange, Yellow, Green, Cyan, Blue, Purple";
        public string position { get; private set; } = "Right";
        public string backgroundColor { get; private set; } = "Grey";
        public string textColor { get; private set; } = "White";

        void LoadKDGUIConfig()
        {
            position = GetConfig("Settings", "Position Next To Hot Bar (Left or Right)", position);
            if (position.ToLower() != "left" && position.ToLower() != "right")
            {
                PrintWarning("\"Settings, Position Next To Hot Bar (Left or Right): " + position + "\"not left or right, setting to default");
                Config["Settings", "Position Next To Hot Bar (Left or Right)"] = "Right";
            }
            if (position.ToLower() == "left")
            {
                AnchorMaxX = "0.34 ";
                AnchorMinX = "0.292 ";
            }
            backgroundColor = GetConfig("Settings", "Background Color", backgroundColor);
            GetBackgroundColor(backgroundColor);
            if (!GotBackgroundColor)
            {
                PrintWarning("\"Settings - Background Color: " + backgroundColor + "\" not recognised, setting to default.");
                Config["Settings", "Background Color"] = "Grey";
            }
            textColor = GetConfig("Settings", "Text Color", textColor);
            GetTextColor(textColor);
            if (!GotTextColor)
            {
                PrintWarning("\"Settings - Text Color: " + textColor + "\" not recognised, setting to default.");
                Config["Settings", "Text Color"] = "White";
            }
            backgroundColorList = GetConfig("Usable Colors (DON'T CHANGE)", "Background", backgroundColorList);
            textColorList = GetConfig("Usable Colors (DON'T CHANGE)", "Text", textColorList);

            if (ConfigUpdated)
            {
                Puts("Configuration file has been updated.");
                SaveConfig();
            }
        }

        protected override void LoadDefaultConfig() => PrintWarning("A new configuration file has been created.");

        private T GetConfig<T>(string category, string setting, T defaultValue)
        {
            var data = Config[category] as Dictionary<string, object>;
            object value;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[category] = data;
                ConfigUpdated = true;
            }
            if (data.TryGetValue(setting, out value))
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            value = defaultValue;
            data[setting] = value;
            ConfigUpdated = true;
            return (T)Convert.ChangeType(value, typeof(T));
        }

        void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"ChatCommandToggle", "kdguitoggle" },
                {"NoPermission", "You do not have permission to use this commands" },
                {"ToggledOff", "Your KDGUI has been turned off, use kdguitoggle to turn it back on." },
                {"ToggledOn", "Your KDGUI has been turned on, use kdguitoggle to turn it off." },
            }, this);
        }

        class KDInfo
        {
            public int Kills = 0;
            public int Deaths = 0;
            public bool HasGUI = false;
        }

        Dictionary<ulong, KDInfo> KDDict = new Dictionary<ulong, KDInfo>();
        List<ulong> JustJoined = new List<ulong>();

        void OnServerInitialized()
        {
#if !RUST
            throw new NotSupportedException("This plugin does not support this game.");
#endif

            LoadKDGUIConfig();
            LoadDefaultMessages();
            permission.RegisterPermission(Perm, this);
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (permission.UserHasPermission(player.userID.ToString(), Perm))
                {
                    if (!KDDict.ContainsKey(player.userID))
                    {
                        KDDict.Add(player.userID, new KDInfo());
                        GUI(player);
                    }
                }
            }

            cmd.AddChatCommand(Lang("ChatCommandToggle"), this, "cmdToggleGUI");
        }
        #endregion

        #region GUI
        void GUI(BasePlayer player, bool Update = false, bool Killed = false, bool Killer = false)
        {
            if (!Update)
            {
                CuiElementContainer Background = new CuiElementContainer();
                Background.Add(new CuiElement
                {
                    Name = "Background",
                    Components =
                        {
                            new CuiImageComponent {Color = BackgroundColor},
                            new CuiRectTransformComponent {AnchorMin = AnchorMinX + "0.025", AnchorMax = AnchorMaxX + "0.107"}
                        },
                });
                CuiHelper.AddUi(player, Background);
                KDDict[player.userID].HasGUI = true;
            }

            if (!Update || Killed)
            {
                CuiElementContainer Deaths = new CuiElementContainer();
                Deaths.Add(new CuiElement
                {
                    Name = "DeathsOutline",
                    Parent = "Background",
                    Components =
                        {
                             new CuiTextComponent {Text = "D: " + KDDict[player.userID].Deaths.ToString(), FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "0.1 0.1 0.1 0.75"},
                             new CuiRectTransformComponent {AnchorMin = "0.01 " + "0.01", AnchorMax = "0.99 " + "0.49"}
                        },
                });
                Deaths.Add(new CuiElement
                {
                    Name = "Deaths",
                    Parent = "Background",
                    Components =
                        {
                             new CuiTextComponent {Text = "D: " + KDDict[player.userID].Deaths.ToString(), FontSize = 20, Align = TextAnchor.MiddleCenter, Color = TextColor, Font = "RobotoCondensed-Regular.ttf"},
                             new CuiRectTransformComponent {AnchorMin = "0.01 " + "0.01", AnchorMax = "0.99 " + "0.49"}
                        },
                });
                CuiHelper.DestroyUi(player, "Deaths");
                CuiHelper.DestroyUi(player, "DeathsOutline");
                CuiHelper.AddUi(player, Deaths);
            }

            if (!Update || Killer)
            {
                CuiElementContainer Kills = new CuiElementContainer();
                Kills.Add(new CuiElement
                {
                    Name = "KillsOutline",
                    Parent = "Background",
                    Components =
                        {
                             new CuiTextComponent {Text = "K: " + KDDict[player.userID].Kills.ToString(), FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "0.1 0.1 0.1 0.75"},
                             new CuiRectTransformComponent {AnchorMin = "0.01 " + "0.51", AnchorMax = "0.99 " + "0.99"}
                        },
                });
                Kills.Add(new CuiElement
                {
                    Name = "Kills",
                    Parent = "Background",
                    Components =
                        {
                             new CuiTextComponent {Text = "K: " + KDDict[player.userID].Kills.ToString(), FontSize = 20, Align = TextAnchor.MiddleCenter, Color = TextColor, Font = "RobotoCondensed-Regular.ttf"},
                             new CuiRectTransformComponent {AnchorMin = "0.01 " + "0.51", AnchorMax = "0.99 " + "0.99"}
                        },
                });
                CuiHelper.DestroyUi(player, "Kills");
                CuiHelper.DestroyUi(player, "KillsOutline");
                CuiHelper.AddUi(player, Kills);
            }
        }
        #endregion

        #region Functions
        void OnPlayerInit(BasePlayer player)
        {
            if (permission.UserHasPermission(player.ToPlayer().UserIDString, Perm))
                JustJoined.Add(player.userID);
            KDDict.Add(player.userID, new KDInfo());
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            KDDict.Remove(player.userID);
            if (JustJoined.Contains(player.userID))
                JustJoined.Remove(player.userID);
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (JustJoined.Contains(player.userID) && permission.UserHasPermission(player.ToPlayer().UserIDString, Perm))
            {
                JustJoined.Remove(player.userID);
                GUI(player);
            }
        }

        void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                DestroyUI(player);
            }
        }

        void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "Background");
            CuiHelper.DestroyUi(player, "Deaths");
            CuiHelper.DestroyUi(player, "DeathsOutline");
            CuiHelper.DestroyUi(player, "Kills");
            CuiHelper.DestroyUi(player, "KillsOutline");
        }

        void OnEntityDeath(BaseEntity entity, HitInfo info)
        {
            if (entity is BasePlayer)
            {
                if (entity.net.connection != null)
                {
                    if (permission.UserHasPermission(entity.ToPlayer().userID.ToString(), Perm))
                    {
                        BasePlayer Killed = entity.ToPlayer();
                        KDDict[Killed.userID].Deaths = KDDict[Killed.userID].Deaths + 1;
                        if (!Exclusions.Contains(Killed.userID))
                            GUI(Killed, Update: true, Killed: true);
                    }
                }
                BasePlayer Killer = null;
                if (info?.Initiator != null && info?.Initiator.net.connection != null)
                    if (info.Initiator is BasePlayer)
                        Killer = info.Initiator.ToPlayer();
                    else
                    if (WoundedDict.ContainsKey(entity.ToPlayer().userID))
                    {
                        Killer = BasePlayer.FindByID(WoundedDict[entity.ToPlayer().userID]);
                        WoundedDict.Remove(entity.ToPlayer().userID);
                    }
                if (Killer != null && Killer != entity)
                {
                    if (permission.UserHasPermission(Killer.userID.ToString(), Perm))
                    {
                        KDDict[Killer.userID].Kills = KDDict[Killer.userID].Kills + 1;
                        if (!Exclusions.Contains(Killer.userID))
                            GUI(Killer, Update: true, Killer: true);
                    }
                }
            }
        }

        void OnPlayerRecover(BasePlayer player)
        {
            if (WoundedDict.ContainsKey(player.userID))
                WoundedDict.Remove(player.userID);
        }

        void OnPlayerDie(BasePlayer player, HitInfo info)
        {
            if (player.net.connection != null && info?.Initiator is BasePlayer)
            {
                if (permission.UserHasPermission(info.Initiator.ToPlayer().UserIDString, Perm))
                {
                    if (WoundedDict.ContainsKey(player.userID))
                        WoundedDict[player.userID] = info.Initiator.ToPlayer().userID;
                    else
                        WoundedDict.Add(player.userID, info.Initiator.ToPlayer().userID);
                }
            }
        }


        void OnUserPermissionGranted(string id, string perm)
        {
            if (perm == Perm)
            {
                BasePlayer granted = BasePlayer.FindByID(ulong.Parse(id));
                if (granted != null)
                {
                    if (!KDDict.ContainsKey(granted.userID))
                    {
                        KDDict.Add(granted.userID, new KDInfo());
                        if (!Exclusions.Contains(granted.userID))
                            GUI(granted);
                    }
                }
            }
        }

        void OnUserPermissionRevoked(string id, string perm)
        {
            if (perm == Perm)
            {
                BasePlayer revoked = BasePlayer.FindByID(ulong.Parse(id));
                if (revoked != null)
                {
                    bool GroupPermissionFound = false;
                    string[] groups = permission.GetUserGroups(revoked.UserIDString);
                    foreach (string s in groups)
                    {
                        if (!GroupPermissionFound)
                            if (permission.GroupHasPermission(s, Perm))
                                GroupPermissionFound = true;
                    }
                    if (!GroupPermissionFound)
                    {
                        if (KDDict.ContainsKey(revoked.userID))
                        {
                            if (KDDict[revoked.userID].HasGUI)
                                DestroyUI(revoked);
                            KDDict.Remove(revoked.userID);
                        }
                    }
                }
            }
        }

        void OnGroupPermissionGranted(string group, string perm)
        {
            if (perm == Perm)
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    if (permission.UserHasGroup(player.UserIDString, group))
                    {
                        if (!KDDict.ContainsKey(player.userID))
                        {
                            KDDict.Add(player.userID, new KDInfo());
                            if (!Exclusions.Contains(player.userID))
                                GUI(player);
                        }
                    }
                }
            }
        }

        void OnGroupPermissionRevoked(string group, string perm)
        {
            if (perm == Perm)
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    if (!permission.UserHasPermission(player.UserIDString, Perm))
                    {
                        bool GroupPermissionFound = false;
                        string[] groups = permission.GetUserGroups(player.UserIDString);
                        foreach (string s in groups)
                        {
                            if (!GroupPermissionFound)
                                if (permission.GroupHasPermission(s, Perm))
                                    GroupPermissionFound = true;
                        }
                        if (!GroupPermissionFound)
                        {
                            if (KDDict.ContainsKey(player.userID))
                            {
                                if (KDDict[player.userID].HasGUI)
                                    DestroyUI(player);
                                KDDict.Remove(player.userID);
                            }
                        }
                    }
                }
            }
        }

        void GetBackgroundColor(string BColor)
        {
            BColor = BColor.ToLower();
            if (BColor == "grey")
            {
                BackgroundColor = BackgroundGrey; return;
            }
            if (BColor == "red")
            {
                BackgroundColor = BackgroundRed; return;
            }
            if (BColor == "orange")
            {
                BackgroundColor = BackgroundOrange; return;
            }
            if (BColor == "yellow")
            {
                BackgroundColor = BackgroundYellow; return;
            }
            if (BColor == "green")
            {
                BackgroundColor = BackgroundGreen; return;
            }
            if (BColor == "cyan")
            {
                BackgroundColor = BackgroundCyan; return;
            }
            if (BColor == "blue")
            {
                BackgroundColor = BackgroundBlue; return;
            }
            if (BColor == "purple")
            {
                BackgroundColor = BackgroundPurple; return;
            }
            GotBackgroundColor = false;
            BackgroundColor = BackgroundGrey;
        }

        void GetTextColor(string TColor)
        {
            TColor = TColor.ToLower();
            if (TColor == "red")
            {
                TextColor = TextRed; return;
            }
            if (TColor == "orange")
            {
                TextColor = TextOrange; return;
            }
            if (TColor == "yellow")
            {
                TextColor = TextYellow; return;
            }
            if (TColor == "green")
            {
                TextColor = TextGreen; return;
            }
            if (TColor == "cyan")
            {
                TextColor = TextCyan; return;
            }
            if (TColor == "blue")
            {
                TextColor = TextBlue; return;
            }
            if (TColor == "purple")
            {
                TextColor = TextPurple; return;
            }
            if (TColor == "white")
            {
                TextColor = TextWhite; return;
            }
            GotTextColor = false;
            TextColor = TextWhite;
        }

        private bool hasPermission(BasePlayer player, string perm)
        {
            if (!permission.UserHasPermission(player.userID.ToString(), perm))
            {
                SendReply(player, Lang("NoPermission", player.UserIDString));
                return false;
            }
            return true;
        }

        string Lang(string key, string userId = null) => lang.GetMessage(key, this, userId);
        #endregion

        #region Commands
        void cmdToggleGUI(BasePlayer player, string cmd)
        {
            if (hasPermission(player, Perm))
            {
                if (Exclusions.Contains(player.userID))
                {
                    Exclusions.Remove(player.userID);
                    GUI(player);
                    SendReply(player, Lang("ToggledOn", player.UserIDString));
                }
                else
                {
                    Exclusions.Add(player.userID);
                    DestroyUI(player);
                    SendReply(player, Lang("ToggledOff", player.UserIDString));
                }
            }
        }
        #endregion
    }
}