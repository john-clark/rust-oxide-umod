using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("NoRaid", "Ryan", "1.3.2", ResourceId = 2530)]
    [Description("Prevents players destroying buildings of those they're not associated with")]

    class NoRaid : RustPlugin
    {
        #region Declaration

        // Helpers
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        // Config, instance, plugin references
        [PluginReference] private Plugin Friends, Clans;
        private static ConfigFile cFile;
        private static NoRaid _instance;

        // Variables
        private bool _canWipeRaid;
        private bool _canRaid;
        private float _startMinutes;

        // Timers
        private Timer _uiTimer;
        private Timer _startTimer;
        private Timer _wipeCheckTimer;
        private Timer _startUiTimer;

        // Cached dates
        private DateTime _cachedWipeTime;
        private DateTime _cachedRaidTime;

        // Active UI players, cached UI container, UI parent constants
        private HashSet<ulong> _uiPlayers = new HashSet<ulong>();
        private CuiElementContainer _cachedContainer;
        private const string _uiParent = "Timer_Body";
        private const string _timerParent = "Timer_Parent";

        // Permissions
        private const string _perm = "noraid.admin";

        #endregion

        #region Config

        private class ConfigFile
        {
            public FriendBypass FriendBypass;

            public bool StopAllRaiding;

            public WipeRaiding WipeRaiding;

            public NoRaidCommand NoRaidCommand;

            public UiSettings Ui;

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    FriendBypass = new FriendBypass()
                    {
                        Enabled = true,
                        FriendsApi = new FriendsAPI()
                        {
                            Enabled = true
                        },
                        PlayerOwner = new PlayerOwner()
                        {
                            Enabled = true
                        },
                        RustIoClans = new RustIOClans()
                        {
                            Enabled = true
                        }
                    },
                    WipeRaiding = new WipeRaiding()
                    {
                        Enabled = false,
                        MinsFromWipe = 60f,
                        CheckInterval = 5f
                    },
                    NoRaidCommand = new NoRaidCommand()
                    {
                        Enabled = true,
                        DefaultMin = 30f,
                        CheckInterval = 2.5f
                    },
                    Ui = new UiSettings()
                    {
                        Enabled = false,
                        RefreshInterval = 1f,
                        PrimaryColor = new Rgba(196, 65, 50, 1),
                        DarkColor = new Rgba(119, 38, 0, 1),
                        TextColor = new Rgba(255, 255, 255, 1),
                        AnchorMin = new Anchor(0.75f, 0.92f),
                        AnchorMax = new Anchor(0.98f, 0.98f)
                    },
                    StopAllRaiding = true
                };
            }
        }

        private class FriendBypass
        {
            public bool Enabled { get; set; }
            public FriendsAPI FriendsApi { get; set; }
            public RustIOClans RustIoClans { get; set; }
            public PlayerOwner PlayerOwner { get; set; }
        }

        private class FriendsAPI
        {
            public bool Enabled { get; set; }
        }

        private class RustIOClans
        {
            public bool Enabled { get; set; }
        }

        private class PlayerOwner
        {
            public bool Enabled { get; set; }
        }

        private class WipeRaiding
        {
            public bool Enabled { get; set; }

            [JsonProperty("Amount of time from wipe people can raid (minutes)")]
            public float MinsFromWipe { get; set; }

            [JsonProperty("Amount of seconds to check if players can raid")]
            public float CheckInterval { get; set; }
        }

        private class NoRaidCommand
        {
            public bool Enabled { get; set; }
            public float DefaultMin { get; set; }
            public float CheckInterval { get; set; }
        }

        private class UiSettings
        {
            public bool Enabled { get; set; }
            public float RefreshInterval { get; set; }
            public Rgba PrimaryColor { get; set; }
            public Rgba DarkColor { get; set; }
            public Anchor AnchorMin { get; set; }
            public Anchor AnchorMax { get; set; }
            public Rgba TextColor { get; set; }
        }

        private class Rgba
        {
            public float R { get; set; }
            public float G { get; set; }
            public float B { get; set; }
            public float A { get; set; }

            public Rgba()
            {
            }

            public Rgba(float r, float g, float b, float a)
            {
                R = r;
                G = g;
                B = b;
                A = a;
            }

            public static string Format(Rgba rgba)
            {
                return $"{rgba.R / 255} {rgba.G / 255} {rgba.B / 255} {rgba.A}";
            }
        }

        private class Anchor
        {
            public float X { get; set; }
            public float Y { get; set; }

            public Anchor()
            {
            }

            public Anchor(float x, float y)
            {
                X = x;
                Y = y;
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating default configuration file...");
            cFile = ConfigFile.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            cFile = Config.ReadObject<ConfigFile>();
        }

        protected override void SaveConfig() => Config.WriteObject(cFile);

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CantDamage"] = "You cannot damage that entity because you are not associated with the building owner",
                ["CanRaid"] = "The raid cooldown has now been lifted, you're now able to raid!",

                ["Cmd_CanRaid"] = "The cooldown has been lifted, you're able to raid whoever you want!",
                ["Cmd_CantRaid"] = "You can't raid just yet, wait another <color=orange>{0}</color>",
                ["Cmd_Permission"] = "You don't have permission to use that command",
                ["Cmd_InvalidArgs"] = "Invalid arguments. Usage: </color=orange>/noraid</color> <color=silver><start/stop> OPTIONAL: <min></color>",
                ["Cmd_CantStart"] = "Cannot start a no raid period, there's already one running.",
                ["Cmd_NoRaid"] = "A no raid period has begun, you can raid in <color=orange>{0}</color>",

                ["Ui_Title"] = "RAID COOLDOWN",

                ["Msg_DayFormat"] = "{0}D {1}H",
                ["Msg_DaysFormat"] = "{0}D {1}H",
                ["Msg_HourFormat"] = "{0}H {1}M",
                ["Msg_HoursFormat"] = "{0}H {1}M",
                ["Msg_MinFormat"] = "{0}M {1}S",
                ["Msg_MinsFormat"] = "{0}M {1}S",
                ["Msg_SecsFormat"] = "{0}S",
            }, this);
        }

        #endregion

        #region Methods

        private string GetFormattedTime(double time)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(time);
            if (timeSpan.TotalSeconds < 1) return null;

            if (Math.Floor(timeSpan.TotalDays) >= 1)
                return string.Format(timeSpan.Days > 1 ? Lang("Msg_DaysFormat", null, timeSpan.Days, timeSpan.Hours) : Lang("Msg_DayFormat", null, timeSpan.Days, timeSpan.Hours));
            if (Math.Floor(timeSpan.TotalMinutes) >= 60)
                return string.Format(timeSpan.Hours > 1 ? Lang("Msg_HoursFormat", null, timeSpan.Hours, timeSpan.Minutes) : Lang("Msg_HourFormat", null, timeSpan.Hours, timeSpan.Minutes));
            if (Math.Floor(timeSpan.TotalSeconds) >= 60)
                return string.Format(timeSpan.Minutes > 1 ? Lang("Msg_MinsFormat", null, timeSpan.Minutes, timeSpan.Seconds) : Lang("Msg_MinFormat", null, timeSpan.Minutes, timeSpan.Seconds));
            return Lang("Msg_SecsFormat", null, timeSpan.Seconds);
        }

        private void StartPeriod()
        {
            _cachedRaidTime = DateTime.UtcNow;
            _canRaid = false;
            foreach (var p in BasePlayer.activePlayerList)
            {
                PrintToChat(p, Lang("Cmd_NoRaid", p.UserIDString, GetFormattedTime((_cachedRaidTime.AddMinutes(_startMinutes)
                    - DateTime.UtcNow).TotalSeconds)));
            }
            _startTimer = timer.Every(cFile.NoRaidCommand.CheckInterval, () =>
            {
                if (UI.ShouldDestroy(_cachedRaidTime, _startMinutes))
                {
                    _canRaid = true;
                    PrintToChat(Lang("CanRaid"));
                    _startTimer?.Destroy();
                    _startUiTimer?.Destroy();
                    _startTimer = null;
                    _startUiTimer = null;
                    foreach (var p in BasePlayer.activePlayerList)
                    {
                        if (_uiPlayers.Contains(p.userID))
                            _uiPlayers.Remove(p.userID);
                        CuiHelper.DestroyUi(p, _uiParent);
                    }
                }

            });
            if (cFile.Ui.Enabled)
            {
                UI.ConstructCachedUi();
                _startUiTimer = timer.Every(cFile.Ui.RefreshInterval, () =>
                {
                    if (UI.ShouldDestroy(_cachedRaidTime, _startMinutes))
                        return;

                    var container = UI.ConstructTimer(_cachedRaidTime, _startMinutes);

                    foreach (var p in BasePlayer.activePlayerList)
                    {
                        if (!_uiPlayers.Contains(p.userID))
                        {
                            CuiHelper.AddUi(p, _cachedContainer);
                            _uiPlayers.Add(p.userID);
                        }
                        CuiHelper.DestroyUi(p, _timerParent);
                        CuiHelper.AddUi(p, container);
                    }
                });
            }
        }

        private void StopPeriod()
        {
            if (_startTimer != null)
            {
                _canRaid = true;
                PrintToChat(Lang("CanRaid"));
                _startTimer?.Destroy();
                _startUiTimer?.Destroy();
                _startTimer = null;
                _startUiTimer = null;
                foreach (var p in BasePlayer.activePlayerList)
                {
                    if (_uiPlayers.Contains(p.userID))
                        _uiPlayers.Remove(p.userID);
                    CuiHelper.DestroyUi(p, _uiParent);
                }
            }
        }

        #region UI

        private class UI
        {
            public static CuiElementContainer Container(string name, string bgColor, Anchor Min, Anchor Max,
                string parent = "Overlay", float fadeOut = 0f, float fadeIn = 0f)
            {
                var newElement = new CuiElementContainer()
                {
                    new CuiElement()
                    {
                        Name = name,
                        Parent = parent,
                        FadeOut = fadeOut,
                        Components =
                        {
                            new CuiImageComponent()
                            {
                                Color = bgColor,
                                FadeIn = fadeIn
                            },
                            new CuiRectTransformComponent()
                            {
                                AnchorMin = $"{Min.X} {Min.Y}",
                                AnchorMax = $"{Max.X} {Max.Y}"
                            }
                        }
                    },
                };
                return newElement;
            }

            public static void Text(string name, string parent, ref CuiElementContainer container, TextAnchor anchor,
                string color, int fontSize, string text,
                Anchor Min, Anchor Max, string font = "robotocondensed-regular.ttf", float fadeOut = 0f,
                float fadeIn = 0f)
            {
                container.Add(new CuiElement()
                {
                    Name = name,
                    Parent = parent,
                    FadeOut = fadeOut,
                    Components =
                    {
                        new CuiTextComponent()
                        {
                            Text = text,
                            Align = anchor,
                            FontSize = fontSize,
                            Font = font,
                            FadeIn = fadeIn,
                            Color = color
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = $"{Min.X} {Min.Y}",
                            AnchorMax = $"{Max.X} {Max.Y}"
                        }
                    }
                });
            }

            public static void Element(string name, string parent, ref CuiElementContainer container, Anchor Min, Anchor Max,
                string bgColor, float fadeOut = 0f, float fadeIn = 0f)
            {
                container.Add(new CuiElement()
                {
                    Name = name,
                    Parent = parent,
                    FadeOut = fadeOut,
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Color = bgColor,
                            Material = "",
                            FadeIn = fadeIn
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = $"{Min.X} {Min.Y}",
                            AnchorMax = $"{Max.X} {Max.Y}"
                        }
                    }
                });
            }

            public static void Image(string name, string parent, ref CuiElementContainer container, Anchor Min, Anchor Max, string img, string color)
            {
                container.Add(new CuiElement
                {
                    Name = name,
                    Parent = parent,
                    Components =
                    {
                        new CuiRawImageComponent()
                        {
                            Url = img,
                            Sprite = "assets/content/textures/generic/fulltransparent.tga",
                            Color = color,
                            Material = "Assets/Icons/IconMaterial.mat"
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = $"{Min.X} {Min.Y}",
                            AnchorMax = $"{Max.X} {Max.Y}"
                        }
                    }
                });
            }

            public static bool ShouldDestroy(DateTime time, float minutes)
            {
                if (time.AddMinutes(minutes) <= DateTime.UtcNow ||
                _instance.GetFormattedTime((time.AddMinutes(minutes) -
                DateTime.UtcNow).TotalSeconds) == null)
                {
                    return true;
                }
                return false;
            }

            public static void ConstructCachedUi()
            {
                _instance._cachedContainer = Container(_uiParent, "0 0 0 0.1", cFile.Ui.AnchorMin, cFile.Ui.AnchorMax);

                Element("Title_Element", _uiParent, ref _instance._cachedContainer, new Anchor(0.2f, 0f), new Anchor(0.75f, 1f), Rgba.Format(cFile.Ui.PrimaryColor));

                Element("Title_Padded", "Title_Element", ref _instance._cachedContainer, new Anchor(0.05f, 0.05f), new Anchor(0.95f, 0.95f), "0 0 0 0");

                Text("Title_Text", "Title_Padded", ref _instance._cachedContainer, TextAnchor.MiddleLeft, Rgba.Format(cFile.Ui.TextColor), 15, _instance.Lang("Ui_Title"), new Anchor(0f, 0f),
                    new Anchor(1f, 1f), "robotocondensed-bold.ttf");

                Element("Icon_Element", _uiParent, ref _instance._cachedContainer, new Anchor(0f, 0f), new Anchor(0.2f, 1f), Rgba.Format(cFile.Ui.PrimaryColor));

                Element("Icon_Padded", "Icon_Element", ref _instance._cachedContainer, new Anchor(0.2f, 0.15f), new Anchor(0.8f, 0.85f), "0 0 0 0");

                Image("Icon_Image", "Icon_Padded", ref _instance._cachedContainer, new Anchor(0f, 0f), new Anchor(1f, 1f), "http://i.imgur.com/jDo2bgn.png", Rgba.Format(cFile.Ui.DarkColor));
            }

            public static CuiElementContainer ConstructTimer(DateTime time, float minutes)
            {
                var container = Container(_timerParent, Rgba.Format(cFile.Ui.DarkColor), new Anchor(0.75f, 0f), new Anchor(1, 1f), _uiParent);

                Text("Timer_Time", _timerParent, ref container, TextAnchor.MiddleCenter, Rgba.Format(cFile.Ui.TextColor), 15,
                    _instance.GetFormattedTime((time.AddMinutes(minutes) - DateTime.UtcNow).TotalSeconds),
                    new Anchor(0.05f, 0.05f), new Anchor(0.95f, 0.95f));

                return container;
            }
        }

        #endregion

        #endregion

        #region Hooks

        private void Init()
        {
            _instance = this;
            permission.RegisterPermission(_perm, this);
            SaveConfig();
            if (cFile.NoRaidCommand.Enabled && cFile.WipeRaiding.Enabled)
            {
                PrintWarning("It is not recomended to have NoRaidCommand and WipeRaiding enabled at the same time. They could conflict!");
                cFile.NoRaidCommand.Enabled = false;
            }
        }

        private void OnServerInitialized()
        {
            if (!Clans && cFile.FriendBypass.RustIoClans.Enabled)
            {
                cFile.FriendBypass.RustIoClans.Enabled = false;
                PrintWarning("RustIO Clans not detected, disabling RustIO Clans integration");
            }
            if (!Friends && cFile.FriendBypass.FriendsApi.Enabled)
            {
                cFile.FriendBypass.FriendsApi.Enabled = false;
                PrintWarning("FriendsAPI not detected, disabling FriendsAPI integration");
            }

            _cachedWipeTime = SaveRestore.SaveCreatedTime;
            _canRaid = true;

            if (cFile.WipeRaiding.Enabled && _cachedWipeTime.AddMinutes(cFile.WipeRaiding.MinsFromWipe) > DateTime.UtcNow)
            {
                _wipeCheckTimer = timer.Every(cFile.WipeRaiding.CheckInterval, () =>
                {
                    if (UI.ShouldDestroy(_cachedWipeTime, cFile.WipeRaiding.MinsFromWipe))
                    {
                        _canWipeRaid = true;
                        PrintToChat(Lang("CanRaid"));
                        _wipeCheckTimer?.Destroy();
                        _uiTimer?.Destroy();
                        foreach (var player in BasePlayer.activePlayerList)
                            CuiHelper.DestroyUi(player, _uiParent);
                    }
                });
                if (cFile.Ui.Enabled)
                {
                    UI.ConstructCachedUi();

                    _uiTimer = timer.Every(cFile.Ui.RefreshInterval, () =>
                    {
                        if (UI.ShouldDestroy(_cachedWipeTime, cFile.WipeRaiding.MinsFromWipe))
                            return;

                        var container = UI.ConstructTimer(_cachedWipeTime, cFile.WipeRaiding.MinsFromWipe);

                        foreach (var player in BasePlayer.activePlayerList)
                        {
                            if (!_uiPlayers.Contains(player.userID))
                            {
                                CuiHelper.AddUi(player, _cachedContainer);
                                _uiPlayers.Add(player.userID);
                            }
                            CuiHelper.DestroyUi(player, _timerParent);
                            CuiHelper.AddUi(player, container);
                        }
                    });
                }
            }
            else
            {
                _canWipeRaid = true;
            }
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, _uiParent);
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (cFile.WipeRaiding.Enabled && _cachedWipeTime.AddMinutes(cFile.WipeRaiding.MinsFromWipe) > DateTime.UtcNow && cFile.Ui.Enabled)
            {
                timer.Once(3f, () =>
                {
                    if (!_uiPlayers.Contains(player.userID))
                    {
                        CuiHelper.AddUi(player, _cachedContainer);
                        _uiPlayers.Add(player.userID);
                    }
                });
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (_uiPlayers.Contains(player.userID))
                _uiPlayers.Remove(player.userID);
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BuildingBlock || entity.name.Contains("deploy") || entity.name.Contains("building"))
            {
                var player = info?.Initiator?.ToPlayer();

                if (!player || !entity.OwnerID.IsSteamId())
                {
                    return null;
                }

                if (cFile.FriendBypass.Enabled)
                {
                    // Owner checks
                    if (cFile.FriendBypass.PlayerOwner.Enabled && player.userID == entity.OwnerID)
                    {
                        return null;
                    }
                    // Friend checks

                    if (Friends)
                    {
                        var hasFriend = Friends?.Call("HasFriend", entity.OwnerID.ToString(), player.UserIDString) ?? false;
                        if (cFile.FriendBypass.FriendsApi.Enabled && (bool)hasFriend)
                        {
                            return null;
                        }
                    }

                    if (Clans)
                    {
                        // Clan checks
                        var targetClan = (string)Clans?.Call("GetClanOf", entity.OwnerID.ToString());
                        var playerClan = (string)Clans?.Call("GetClanOf", player.UserIDString);
                        if (cFile.FriendBypass.RustIoClans.Enabled && playerClan != null && targetClan != null && targetClan == playerClan)
                        {
                            return null;
                        }
                    }
                }

                // Prevents player from damaging after friendbypass checks
                if (cFile.StopAllRaiding)
                {
                    PrintToChat(player, Lang("CantDamage", player.UserIDString));
                    return true;
                }

                // No raid command checks
                if (!_canRaid)
                {
                    PrintToChat(player, Lang("Cmd_CantRaid", player.UserIDString, GetFormattedTime((_cachedRaidTime.AddMinutes(_startMinutes)
                        - DateTime.UtcNow).TotalSeconds)));
                    return true;
                }

                // Wipe raid checks
                if (cFile.WipeRaiding.Enabled && !_canWipeRaid)
                {
                    PrintToChat(player, Lang("Cmd_CantRaid", player.UserIDString, GetFormattedTime((_cachedWipeTime.AddMinutes(cFile.WipeRaiding.MinsFromWipe)
                        - DateTime.UtcNow).TotalSeconds)));
                    return true;
                }
                if (cFile.WipeRaiding.Enabled)
                {
                    return null;
                }
            }
            return null;
        }

        [ChatCommand("canraid")]
        private void RaidCmd(BasePlayer player, string command, string[] args)
        {
            if (cFile.WipeRaiding.Enabled && !_canWipeRaid)
            {
                PrintToChat(player, Lang("Cmd_CantRaid", player.UserIDString, GetFormattedTime((_cachedWipeTime.AddMinutes(cFile.WipeRaiding.MinsFromWipe)
                    - DateTime.UtcNow).TotalSeconds)));
                return;
            }
            PrintToChat(player, Lang("Cmd_CanRaid", player.UserIDString));
        }

        [ChatCommand("noraid")]
        private void NoRaidCmd(BasePlayer player, string command, string[] args)
        {
            if (!cFile.NoRaidCommand.Enabled)
                return;

            if (!permission.UserHasPermission(player.UserIDString, _perm))
            {
                PrintToChat(player, Lang("Cmd_Permission", player.UserIDString));
                return;
            }
            if (args.Length == 0)
            {
                PrintToChat(player, Lang("Cmd_InvalidArgs", player.UserIDString));
                return;
            }
            switch (args[0].ToLower())
            {
                case "start":
                    {
                        if (_startTimer != null)
                        {
                            PrintToChat(player, Lang("Cmd_CantStart", player.UserIDString));
                            return;
                        }
                        float outNum;
                        _startMinutes = args.Length == 2 && float.TryParse(args[1], out outNum) ? outNum : cFile.NoRaidCommand.DefaultMin;
                        StartPeriod();
                        return;
                    }
                case "stop":
                    {
                        StopPeriod();
                        return;
                    }
            }
        }

        [ConsoleCommand("noraid.start")]
        private void ConsoleStartCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && !permission.UserHasPermission(arg.Connection.userid.ToString(), _perm))
            {
                arg.ReplyWith(Lang("Cmd_Permission", arg.Connection.userid.ToString()));
                return;
            }
            if (_startTimer != null)
            {
                arg.ReplyWith(Lang("Cmd_CantStart"));
                return;
            }
            float outNum;
            _startMinutes = arg.Args.Length >= 1 && float.TryParse(arg.Args[0], out outNum) ? outNum : cFile.NoRaidCommand.DefaultMin;
            arg.ReplyWith($"Started NoRaid period that lasts for {_startMinutes} minutes");
            StartPeriod();
        }

        [ConsoleCommand("noraid.stop")]
        private void ConsoleStopCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && !permission.UserHasPermission(arg.Connection.userid.ToString(), _perm))
            {
                arg.ReplyWith(Lang("Cmd_Permission", arg.Connection.userid.ToString()));
                return;
            }
            StopPeriod();
            arg.ReplyWith("NoRaid period stopped");
        }

        #endregion
    }
}