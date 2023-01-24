// Requires: EMInterface

using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json.Linq;
using Rust;
using Network;

namespace Oxide.Plugins
{
    [Info("Event Manager", "Reneb / k1lly0u", "3.0.76", ResourceId = 740)]
    [Description("A versitile arena event plugin")]
    class EventManager : RustPlugin
    {
        #region Fields
        [PluginReference] EMInterface EMInterface;
        [PluginReference] Plugin Economics;
        [PluginReference] Plugin FriendlyFire;
        [PluginReference] Plugin Kits;
        [PluginReference] Plugin ServerRewards;
        [PluginReference] Plugin Spawns;
        [PluginReference] Plugin ZoneManager;

        MethodInfo killLifestory;

        private static EventManager ins;
        private static List<MessageData> popupQueue;
        private static List<BasePlayer> unnetworkedPlayers;

        private Dictionary<ulong, Timer> KillTimers;
        private RestorationManager Restoration;

        private GameTimer _GameTimer;
        private WaveTimer _WaveTimer;
        private DynamicConfigFile P_Stats;
        private DynamicConfigFile RestoreData;

        private bool _GodEnabled;
        private bool debugEnabled = false;

        private bool ResetNoCorpse;

        private static string ScoreUI = "EMUI_Scoreboard";
        private static string ClockUI = "EMUI_Timer";
        private static string DeathUI = "EMUI_Death";
        private static string TimerUI = "EMUI_DeathTimer";

        private FieldInfo spectateFilter;

        public Dictionary<ulong, PlayerStatistics> StatsCache;
        public List<EMInterface.AEConfig> ValidAutoEvents;
        public Dictionary<string, Events> ValidEvents;
        public Events _Event;

        public string _EventName;
        public string _CurrentEventConfig;
        public int _AutoEventNum = 0;

        public bool _ForceNextConfig;
        public string _NextConfigName;
        public int _NextEventNum;

        public bool _Open;
        public bool _Started;
        public bool _Prestarting;
        public bool _Ended;
        public bool _Pending;
        public bool _Destoyed;
        public bool _Launched;
        public bool _RandomizeAuto;
        public bool _TimerStarted;

        public List<Timer> EventTimers;
        public Dictionary<ulong, Timer> RespawnTimers;
        public Dictionary<string, EventSetting> EventGames;
        public ScoreData GameScores;
        public SpawnManager SpawnCount;
        public List<EventPlayer> EventPlayers;
        public List<BasePlayer> Joiners;

        public Statistics GameStatistics;
        public GameMode EventMode;
        #endregion

        #region UI
        #region Popup Messages
        private static string Popup = "EMUI_Popupmsg";
        private List<string> PopupPanels = new List<string>();
        private int popUpCount = 0;

        public void PopupMessage(string message)
        {
            popupQueue.Add(new MessageData(message, 6, ""));
            UpdateMessages();
        }
        private CuiElementContainer CreateMessageEntry(int number, string panelName, MessageData data)
        {
            PopupPanels.Add(panelName);
            var pos = GetFeedPosition(number);
            var Main = UI.CreateElementContainer(panelName, "0 0 0 0", $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", false, "Hud");
            UI.CreateOutLineLabel(ref Main, panelName, "0 0 0 1", data.message, 17, "1 1", "0 0", "1 1");
            return Main;
        }
        private float[] GetFeedPosition(int number)
        {
            Vector2 initialPos = new Vector2(0.25f, 0.89f);
            Vector2 dimensions = new Vector2(0.5f, 0.04f);
            var yPos = initialPos.y - ((dimensions.y + 0.005f) * number);
            return new float[] { initialPos.x, yPos, initialPos.x + dimensions.x, yPos + dimensions.y };
        }
        private void UpdateMessages(bool destroyed = false)
        {
            if (destroyed)
            {
                if (popupQueue.Count > 0)
                {
                    for (int i = 0; i < popupQueue.Count; i++)
                    {
                        if (i >= 3)
                            return;

                        var feed = popupQueue[i];
                        var panelName = feed.elementID;
                        if (!feed.started)
                        {
                            panelName = Popup + popUpCount;
                            popUpCount++;
                            feed.Begin(panelName);
                        }
                        AddUI(CreateMessageEntry(i, panelName, popupQueue[i]));
                    }
                }
            }
            else
            {
                if (popupQueue.Count > 0 && popupQueue.Count < 3)
                {
                    var feed = popupQueue[popupQueue.Count - 1];
                    var panelName = Popup + popUpCount;
                    popUpCount++;
                    AddUI(CreateMessageEntry(popupQueue.Count - 1, panelName, feed));
                    feed.Begin(panelName);
                }
            }
        }
        private void AddUI(CuiElementContainer element)
        {
            foreach (var player in EventPlayers)
            {
                if (player.inEvent && player.enabled)
                    CuiHelper.AddUi(player.GetPlayer(), element);
            }
        }
        private void DestroyUpdate()
        {
            for (int i = 0; i < popupQueue.Count; i++)
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(player, popupQueue[i].elementID);
                }
            }
            UpdateMessages(true);
        }
        private void DestroyPopupUI(BasePlayer player)
        {
            for (int i = 0; i < popupQueue.Count; i++)
            {
                CuiHelper.DestroyUi(player, popupQueue[i].elementID);
            }
        }
        private void DestroyPopupUI(MessageData element)
        {
            if (!string.IsNullOrEmpty(element.elementID))
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(player, element.elementID);
                }
            }
            popupQueue.Remove(element);
            DestroyUpdate();
        }
        private void DestroyAllPopups()
        {
            foreach (var player in BasePlayer.activePlayerList)
                foreach (var message in PopupPanels)
                    CuiHelper.DestroyUi(player, message);
            PopupPanels.Clear();
        }


        #endregion

        #region Scoreboards
        public class ScoreData
        {
            public Dictionary<ulong, Scoreboard> Scores = new Dictionary<ulong, Scoreboard>();
            public string ScoreType;
            public string Additional;
        }
        public class Scoreboard
        {
            public string Name;
            public int Position;
            public int Score;
        }

        public void CreateScoreboard(BasePlayer player)
        {
            if (GameScores == null) return;
            var scores = GameScores.Scores;
            var type = GameScores.ScoreType;
            var additional = GameScores.Additional;

            var Main = UI.CreateElementContainer(ScoreUI, "0.1 0.1 0.1 0.7", "0.82 0.55", "0.99 0.98", false, "Hud");

            int count = 0;
            if (!string.IsNullOrEmpty(additional))
            {
                UI.CreateLabel(ref Main, ScoreUI, "", additional, 12, $"0.05 {GetHeight(count)}", $"0.95 {GetHeight(count) + 0.06f}");
                count++;
            }
            if (scores != null)
            {
                var topScores = scores.Take(11);
                UI.CreateLabel(ref Main, ScoreUI, "", $"<color={configData.Messaging.MainColor}>Name</color>", 12, $"0.25 {GetHeight(count)}", $"0.7 {GetHeight(count) + 0.06f}");
                if (type != null) UI.CreateLabel(ref Main, ScoreUI, "", $"<color={configData.Messaging.MainColor}>{type}</color>", 12, $"0.7 {GetHeight(count)}", $"0.95 {GetHeight(count) + 0.06f}");
                count++;

                if (scores.ContainsKey(player.userID) && scores[player.userID].Position > 10)
                {
                    UI.CreateLabel(ref Main, ScoreUI, "", $"<color={configData.Messaging.MainColor}>{scores[player.userID].Position + 1}</color>", 12, $"0.05 {GetHeight(count)}", $"0.25 {GetHeight(count) + 0.06f}");
                    UI.CreateLabel(ref Main, ScoreUI, "", $"<color={configData.Messaging.MsgColor}>{scores[player.userID].Name}</color>", 12, $"0.25 {GetHeight(count)}", $"0.7 {GetHeight(count) + 0.06f}");
                    UI.CreateLabel(ref Main, ScoreUI, "", $"<color={configData.Messaging.MsgColor}>{scores[player.userID].Score}</color>", 12, $"0.7 {GetHeight(count)}", $"0.95 {GetHeight(count) + 0.06f}");
                    count++;
                }
                foreach (var score in topScores)
                {
                    UI.CreateLabel(ref Main, ScoreUI, "", $"<color={configData.Messaging.MainColor}>{score.Value.Position + 1}</color>", 12, $"0.05 {GetHeight(count)}", $"0.25 {GetHeight(count) + 0.06f}");
                    UI.CreateLabel(ref Main, ScoreUI, "", $"<color={configData.Messaging.MsgColor}>{score.Value.Name}</color>", 12, $"0.25 {GetHeight(count)}", $"0.7 {GetHeight(count) + 0.06f}");
                    UI.CreateLabel(ref Main, ScoreUI, "", $"<color={configData.Messaging.MsgColor}>{score.Value.Score}</color>", 12, $"0.7 {GetHeight(count)}", $"0.95 {GetHeight(count) + 0.06f}");
                    count++;
                }
            }
            DestroyScoreboard(player);
            CuiHelper.AddUi(player, Main);
        }
        public void UpdateScoreboard(ScoreData data)
        {
            GameScores = data;
            foreach (var eventPlayer in EventPlayers)
            {
                CreateScoreboard(eventPlayer.GetPlayer());
            }
        }
        float GetHeight(int num) => 0.89f - (0.06f * num);
        void DestroyScoreboard(BasePlayer player) => CuiHelper.DestroyUi(player, ScoreUI);

        #endregion
        #endregion

        #region Oxide Hooks
        void Loaded()
        {
            Unsubscribe(nameof(OnEntityTakeDamage));
            Unsubscribe(nameof(OnPlayerAttack));
            Unsubscribe(nameof(OnItemPickup));
            Unsubscribe(nameof(OnRunPlayerMetabolism));
            Unsubscribe(nameof(CanNetworkTo));
            Unsubscribe(nameof(CanLootPlayer));
            Unsubscribe(nameof(OnCreateWorldProjectile));

            P_Stats = Interface.Oxide.DataFileSystem.GetFile("EventManager/Statistics");
            RestoreData = Interface.Oxide.DataFileSystem.GetFile("EventManager/Restoration_data");
            Restoration = new RestorationManager();

            killLifestory = typeof(BasePlayer).GetMethod("LifeStoryEnd", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
        }
        void OnServerInitialized()
        {
            lang.RegisterMessages(Messages, this);
            if (!Spawns)
            {
                PrintError("Unable to load EventManager - Spawns database not found. Please download Spawns database to continue");
                rust.RunServerCommand("oxide.unload", new object[] { "EventManager" });
                return;
            }
            if (!ZoneManager)
            {
                PrintError("Unable to load EventManager - ZoneManager not found. Please download ZoneManager to continue");
                rust.RunServerCommand("oxide.unload", new object[] { "EventManager" });
                return;
            }
            if (!Kits)
                PrintError("Kits is not installed! Unable to issue any weapon kits");
            SetVars();
            LoadVariables();
            LoadData();
            ins = this;

            foreach(var player in BasePlayer.activePlayerList)
                CheckForRestore(player);

            timer.In(5, ()=>
            {
                Interface.CallHook("RegisterGame");
                ValidateAllEvents();
            });
        }
        void Unload()
        {
            SaveStatistics();
            SaveRestoreInfo();
            if (_Open) CloseEvent();
            if (_Started)
            {
                foreach(var eventPlayer in EventPlayers)
                {
                    eventPlayer?.GetPlayer().DieInstantly();
                    UnityEngine.Object.DestroyImmediate(eventPlayer);
                }
            }
            else DestroyGame();
        }

        void OnPlayerInit(BasePlayer player)
        {
            CheckForRestore(player);
        }
        void OnPlayerDisconnected(BasePlayer player)
        {
            if (Joiners.Contains(player))
                Joiners.Remove(player);
            if (GetUser(player))
                LeaveEvent(player);
        }
        void OnPlayerRespawned(BasePlayer player)
        {
            if (!_Started && !_Open)
                CheckForRestore(player);
            else
            {
                var eventPlayer = GetUser(player);
                if (eventPlayer == null) return;
                if (!EventPlayers.Contains(eventPlayer))
                {
                    UnityEngine.Object.DestroyImmediate(eventPlayer);
                    return;
                }
                if (eventPlayer.inEvent && !eventPlayer.isLeaving)
                    TeleportPlayerToEvent(player);
                else if (!eventPlayer.isLeaving)
                    Restoration.RestorePlayer(player);
            }
        }

        void OnPlayerAttack(BasePlayer player, HitInfo hitinfo)
        {
            var eventPlayer = GetUser(player);
            if (eventPlayer == null || !eventPlayer.inEvent)
                return;

            if (hitinfo.HitEntity != null)
                Interface.Oxide.CallHook("OnEventPlayerAttack", player, hitinfo);

            if (hitinfo.IsProjectile())
                AddStats(player, StatType.Shots);
            return;
        }
        void OnEntityTakeDamage(BaseEntity entity, HitInfo info)
        {
            if (entity.GetComponent<EventCorpse>())
            {
                NullifyDamage(info);
                return;
            }
            var player = entity.ToPlayer();
            var attacker = info.InitiatorPlayer;

            if (player != null)
            {
                var eventPlayer = GetUser(player);

                if (eventPlayer != null)
                {
                    if (eventPlayer.isLeaving || eventPlayer.isDead)
                    {
                        NullifyDamage(info);
                        return;
                    }
                    if (_GodEnabled)
                    {
                        NullifyDamage(info);
                        return;
                    }

                    float damageAmount = info.damageTypes.Total();
                    if (attacker != null)
                    {
                        if (!GetUser(attacker))
                        {
                            NullifyDamage(info);
                            return;
                        }
                        else
                        {
                            object multiply = Interface.CallHook("HasDamageMultiplier", player, attacker);
                            if (multiply is float)
                            {
                                damageAmount *= (float)multiply;
                            }
                        }
                    }

                    if (info.isHeadshot)
                        damageAmount *= 2;

                    if (player.health - damageAmount < 1)
                    {
                        NullifyDamage(info);
                        OnPlayerDeath(player, info);
                        return;
                    }
                }
                else if (attacker != null)
                {
                    if (GetUser(attacker))
                    {
                        NullifyDamage(info);
                        return;
                    }
                }
            }
        }
        object OnCreateWorldProjectile(HitInfo info, Item item)
        {
            if (info?.InitiatorPlayer != null)
            {
                var eventPlayer = GetUser(info.InitiatorPlayer);
                if (eventPlayer != null)
                    return false;
            }
            if (info?.HitEntity?.ToPlayer() != null)
            {
                var eventPlayer = GetUser(info.HitEntity.ToPlayer());
                if (eventPlayer != null)
                    return false;
            }
            return null;
        }
        private void OnRunPlayerMetabolism(PlayerMetabolism metabolism, BaseCombatEntity entity, float delta)
        {
            var eventPlayer = GetUser(entity?.ToPlayer());
            if (eventPlayer == null) return;
            if (eventPlayer.isDead)
            {
                metabolism.bleeding.value = 0;
                metabolism.poison.value = 0;
                metabolism.radiation_level.value = 0;
                metabolism.radiation_poison.value = 0;
                metabolism.wetness.value = 0;
            }
            else
                metabolism.bleeding.value = 0f;
        }
        object CanNetworkTo(BaseEntity entity, BasePlayer target)
        {
            var player = entity as BasePlayer;
            var eventPlayer = GetUser(player ?? (entity as HeldEntity)?.GetOwnerPlayer());
            if (eventPlayer == null || target == null)
                return null;
            if (unnetworkedPlayers.Contains(player))
                return false;
            return null;
        }
        void OnItemPickup(Item item, BasePlayer player)
        {
            if (_Started && GetUser(player) != null && _Event.DisableItemPickup)
            {
                item.Remove(0.01f);
                SendMsg(player, "ItemPickup");
            }
        }
        object CanLootPlayer(BasePlayer target, BasePlayer looter)
        {
            if (_Started && GetUser(looter) != null && _Event.DisableItemPickup)
            {
                SendMsg(looter, "NoLooting");
                return true;
            }
            return null;
        }

        void SetVars()
        {
            EventGames = new Dictionary<string, EventSetting>();
            EventPlayers = new List<EventPlayer>();
            Joiners = new List<BasePlayer>();
            EventTimers = new List<Timer>();
            RespawnTimers = new Dictionary<ulong, Timer>();
            ValidAutoEvents = new List<EMInterface.AEConfig>();
            ValidEvents = new Dictionary<string, Events>();
            KillTimers = new Dictionary<ulong, Timer>();
            StatsCache = new Dictionary<ulong, PlayerStatistics>();
            GameScores = new ScoreData();
            SpawnCount = new SpawnManager();
            Restoration = new RestorationManager();
            popupQueue = new List<MessageData>();
            unnetworkedPlayers = new List<BasePlayer>();

            _Open = false;
            _Started = false;
            _Ended = true;
            _Pending = false;
            _Destoyed = true;
            _Launched = false;
            _GodEnabled = false;
            _RandomizeAuto = false;
            _ForceNextConfig = false;
            _EventName = null;
            _NextConfigName = null;
            _NextEventNum = 0;
            _CurrentEventConfig = null;
            _Event = DefaultConfig;
            EventMode = GameMode.Normal;

            _TimerStarted = false;
            _GameTimer = null;
        }
        void CheckForRestore(BasePlayer player)
        {
            if (player.IsSleeping() || player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.Once(3, () => CheckForRestore(player));
                return;
            }
            var eventPlayer = GetUser(player);
            if (eventPlayer != null && eventPlayer.inEvent && _Started) return;

            if (Restoration.HasPendingRestore(player.userID))
            {
                if (Restoration.ReadyToRestore(player))
                {
                    if (!Restoration.RestorePlayer(player))
                    {
                        SendMsg(player, "failedRestore");
                    }
                    else SaveRestoreInfo();
                }
            }
        }
        #endregion

        #region Config
        private ConfigData configData;
        class ConfigData
        {
            public EM_Messaging Messaging { get; set; }
            public EM_Options Options { get; set; }
        }
        class EM_Options
        {
            public bool AllowClassSelectionOnDeath { get; set; }
            public bool LaunchAutoEventsOnStartup { get; set; }
            public int Battlefield_Timer { get; set; }
            public int Required_AuthLevel { get; set; }
            public bool DropCorpseOnDeath { get; set; }
            public bool UseSpectateMode { get; set; }
            public bool UseEconomicsAsTokens { get; set; }
            public bool UseEventPrestart { get; set; }
            public int EventPrestartTimer { get; set; }
        }
        class EM_Messaging
        {
            public bool AnnounceEvent { get; set; }
            public bool AnnounceEvent_During { get; set; }
            public int AnnounceEvent_Interval { get; set; }
            public string MainColor { get; set; }
            public string MsgColor { get; set; }
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        private void LoadConfigVariables()
        {
            try
            {
                configData = Config.ReadObject<ConfigData>();
            }
            catch (Exception)
            {
                Puts("Invalid config file, restoring default");
                LoadDefaultConfig();
            }
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                Messaging = new EM_Messaging
                {
                    AnnounceEvent_During = true,
                    AnnounceEvent_Interval = 120,
                    AnnounceEvent = true,
                    MainColor = "#FF8C00",
                    MsgColor = "#939393"
                },
                Options = new EM_Options
                {
                    AllowClassSelectionOnDeath = true,
                    Battlefield_Timer = 1200,
                    DropCorpseOnDeath = true,
                    EventPrestartTimer = 30,
                    LaunchAutoEventsOnStartup = false,
                    Required_AuthLevel = 1,
                    UseEconomicsAsTokens = false,
                    UseSpectateMode = true,
                    UseEventPrestart = true
                }
            };
            SaveConfig(config);
        }
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Messaging
        public void BroadcastEvent(string msg)
        {
            foreach (EventPlayer eventplayer in EventPlayers)
                SendReply(eventplayer.GetPlayer(), $"<color={configData.Messaging.MainColor}>" + msg + "</color>");
        }
        public void BroadcastToChat(string message) => PrintToChat($"<color={configData.Messaging.MainColor}>{msg("Title")}</color><color={configData.Messaging.MsgColor}>{msg(message)}</color>");
        static string msg(string key, BasePlayer player = null) => ins.lang.GetMessage(key, ins, player?.UserIDString);
        private void SendMsg(BasePlayer player, string langkey, bool title = true)
        {
            string message = $"<color={configData.Messaging.MsgColor}>{msg(langkey)}</color>";
            if (title) message = $"<color={configData.Messaging.MainColor}>{msg("Title")}</color>" + message;
            SendReply(player, message);
        }
        void AnnounceEvent()
        {
            object success = Interface.Oxide.CallHook("OnEventAnnounce");
            if (success is string)
                BroadcastToChat((string)success);
            else BroadcastToChat(string.Format(msg("eventOpen"), _Event.EventType));
        }
        void AnnounceDuringEvent()
        {
            if (configData.Messaging.AnnounceEvent_During)
            {
                if (_Open && _Started)
                {
                    foreach (BasePlayer player in BasePlayer.activePlayerList)
                    {
                        if (!GetUser(player))
                            SendMsg(player, string.Format(msg("stillOpen"), _Event.EventType));
                    }
                }
            }
        }

        #endregion

        #region Information Classes
        public class Events
        {
            public bool CloseOnStart;
            public bool DisableItemPickup;
            public bool UseClassSelector;
            public int MinimumPlayers;
            public int MaximumPlayers;
            public int ScoreLimit;
            public int RespawnTimer;
            public int GameRounds;
            public int EnemiesToSpawn;
            public string EventType;
            public string Kit;
            public string WeaponSet;
            public string Spawnfile;
            public string Spawnfile2;
            public string ZoneID;
            public GameMode GameMode;
            public RespawnType RespawnType;
            public SpawnType SpawnType;
        }
        public class EventSetting
        {
            public bool LockClothing;
            public bool RequiresKit;
            public bool RequiresSpawns;
            public bool RequiresMultipleSpawns;
            public bool CanUseClassSelector;
            public bool CanPlayBattlefield;
            public bool CanChooseRespawn;
            public bool ForceCloseOnStart;
            public bool IsRoundBased;
            public bool SpawnsEnemies;
            public string ScoreType;
        }
        public class EventInvItem
        {
            public int itemid;
            public ulong skin;
            public int amount;
            public float condition;
            public int ammo;
            public string ammotype;
            public ProtoBuf.Item.InstanceData instanceData;
            public EventInvItem[] contents;
        }
        public class RestoreInfo
        {
            public Dictionary<InventoryType, List<EventInvItem>> inventory = new Dictionary<InventoryType, List<EventInvItem>>();
            public float health, hydration, calories, x, y, z;
        }

        public class UI
        {
            static public CuiElementContainer CreateElementContainer(string panelName, string color, string aMin, string aMax, bool useCursor = false, string parent = "Overlay")
            {
                var NewElement = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
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
                    Text = { Color = color, FontSize = size, Align = align, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);

            }
            static public void CreateOutLineLabel(ref CuiElementContainer container, string panel, string color, string text, int size, string distance, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter, string parent = "Overlay")
            {
                CuiElement textElement = new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    FadeOut = 0.2f,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = text,
                            FontSize = size,
                            Align = TextAnchor.MiddleCenter,
                            FadeIn = 0.2f
                        },
                        new CuiOutlineComponent
                        {
                            Distance = distance,
                            Color = color
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = aMin,
                            AnchorMax = aMax
                        }
                    }
                };
                container.Add(textElement);
            }
            static public void CreateButton(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 0f },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel);
            }
            static public void ImageFromStorage(ref CuiElementContainer container, string panel, string png, string aMin, string aMax)
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
            static public void ImageFromURL(ref CuiElementContainer container, string panel, string url, string aMin, string aMax)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent {Url = url },
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

        public enum InventoryType { Main, Wear, Belt };
        public enum GameMode
        {
            Normal,
            Battlefield
        }
        public enum SpawnType
        {
            Random,
            Consecutive
        }
        public enum RespawnType
        {
            None,
            Timer,
            Waves
        }
        #endregion

        #region Components
        public class EventPlayer : MonoBehaviour
        {
            private BasePlayer player;
            private EventCorpse corpse;

            public bool inEvent, isDead, isRespawning, isSpectating, OOB, isLeaving;
            public string currentClass;
            public int restoreAttempts;

            void Awake()
            {
                inEvent = true;
                isDead = false;
                isRespawning = false;
                isSpectating = false;
                isLeaving = false;
                OOB = false;
                currentClass = string.Empty;
                restoreAttempts = 0;
                player = GetComponent<BasePlayer>();
            }
            private void OnDestroy()
            {
                AddToNetwork();
            }
            public BasePlayer GetPlayer() => player;
            public void SetCorpse(EventCorpse corpse) => this.corpse = corpse ?? null;
            public EventCorpse GetCorpse() => corpse ?? null;

            public void RemoveFromNetwork(float refreshIn = 0)
            {
                if (!unnetworkedPlayers.Contains(player))
                {
                    if (Net.sv.write.Start())
                    {
                        Net.sv.write.PacketID(Message.Type.EntityDestroy);
                        Net.sv.write.EntityID(player.net.ID);
                        Net.sv.write.UInt8((byte)BaseNetworkable.DestroyMode.None);
                        Net.sv.write.Send(new SendInfo(player.net.group.subscribers.Where(x => x.userid != player.userID).ToList()));
                    }

                    unnetworkedPlayers.Add(player);
                    if (refreshIn > 0)
                        InvokeHandler.Invoke(this, AddToNetwork, refreshIn);
                }
            }
            public void AddToNetwork()
            {
                if (unnetworkedPlayers.Contains(player))
                {
                    unnetworkedPlayers.Remove(player);
                    player.SendFullSnapshot();
                }
            }
        }
        public class EventCorpse : MonoBehaviour
        {
            public LootableCorpse entity;
            void Awake()
            {
                entity = GetComponent<LootableCorpse>();
            }
            void OnDestroy()
            {
                if (entity == null) return;
                if (entity?.containers == null) return;
                foreach (var container in entity.containers)
                {
                    Item[] array = container.itemList.ToArray();
                    for (int i = 0; i < array.Length; i++)
                        array[i].Remove(0f);
                }
                ItemManager.DoRemoves();
                entity.DieInstantly();
            }
        }

        private class GameTimer : MonoBehaviour
        {
            int timeRemaining;
            void Awake() => timeRemaining = 0;
            public void StartTimer(int time)
            {
                timeRemaining = time;
                InvokeRepeating("TimerTick", 1f, 1f);
                ins._TimerStarted = true;
            }
            void OnDestroy()
            {
                CancelInvoke("TimerTick");
                ins._TimerStarted = false;
                Destroy(this);
            }
            internal void TimerTick()
            {
                timeRemaining--;
                if (timeRemaining == 0)
                {
                    DestroyUI();
                    ins.CancelEvent(msg("TimeLimit"));
                    CancelInvoke("TimerTick");
                }
                else UpdateUITimer();
            }
            internal void UpdateUITimer()
            {
                string clockTime = "";
                TimeSpan dateDifference = TimeSpan.FromSeconds(timeRemaining);
                var hours = dateDifference.Hours;
                var mins = dateDifference.Minutes;
                var secs = dateDifference.Seconds;
                if (hours > 0)
                    clockTime = string.Format("{0:00}:{1:00}:{2:00}", hours, mins, secs);
                else clockTime = string.Format("{0:00}:{1:00}", mins, secs);

                var CUI = UI.CreateElementContainer(ClockUI, "0.1 0.1 0.1 0.7", "0.45 0.95", "0.55 0.99", false, "Hud");
                UI.CreateLabel(ref CUI, ClockUI, "", clockTime, 16, "0 0", "1 1");
                foreach (var ePlayer in ins.EventPlayers)
                {
                    CuiHelper.DestroyUi(ePlayer.GetPlayer(), ClockUI);
                    CuiHelper.AddUi(ePlayer.GetPlayer(), CUI);
                }
            }
            internal void DestroyUI()
            {
                foreach (var player in BasePlayer.activePlayerList)
                    CuiHelper.DestroyUi(player, ClockUI);
            }
        }
        private class WaveTimer : MonoBehaviour
        {
            private int waveTimer;
            void Awake()
            {
                waveTimer = ins._Event.RespawnTimer;
                InvokeRepeating("RespawnTick", 1f, 1f);
            }
            void OnDestroy() => CancelInvoke("RespawnTick");
            private void RespawnTick()
            {
                waveTimer--;
                if (waveTimer <= 0)
                    waveTimer = ins._Event.RespawnTimer;
            }
            public int GetTime() => waveTimer;
        }
        public class SpawnManager
        {
            public int spawnsCountA = 0;
            public int spawnsCountB = 0;
            private int lastSpawnA = 0;
            private int lastSpawnB = 0;

            public object GetSpawnPoint(string file, bool team = true, int min = -1, int max = -1)
            {
                if (ins._Event.SpawnType == SpawnType.Consecutive)
                {
                    if (min > 0)
                    {
                        if (team)
                        {
                            if (lastSpawnA < min || (max > 0 && lastSpawnA > max))
                            {
                                lastSpawnA = min - 1;
                                return GetNextSpawn(file, team);
                            }
                        }
                        else
                        {
                            if (lastSpawnB < min || (max > 0 && lastSpawnB > max))
                            {
                                lastSpawnB = min - 1;
                                return GetNextSpawn(file, team);
                            }
                        }
                    }
                    return GetNextSpawn(file, team);
                }
                else
                {
                    if (min > 0)
                    {
                        if (max > 0)
                            return GetRandomRange(file, min, max);
                        else return GetRandomRange(file, min, 9999);
                    }
                    return GetRandomSpawn(file);
                }
            }
            private object GetRandomSpawn(string file) => ins.Spawns.Call("GetRandomSpawn", file);
            private object GetRandomRange(string file, int min, int max) => ins.Spawns.Call("GetRandomSpawnRange", file, min, max);
            private object GetNextSpawn(string file, bool team)
            {
                int number;
                if (team)
                {
                    ++lastSpawnA;
                    if (lastSpawnA >= spawnsCountA)
                        lastSpawnA = 0;
                    number = lastSpawnA;
                }
                else
                {
                    ++lastSpawnB;
                    if (lastSpawnB >= spawnsCountB)
                        lastSpawnB = 0;
                    number = lastSpawnB;
                }
                return ins.Spawns.Call("GetSpawn", file, number);
            }
            public void SetSpawnCount(bool team, int number)
            {
                if (team)
                    spawnsCountA = number;
                else spawnsCountB = number;
            }
        }
        private class MessageData
        {
            public int timecount;
            public string elementID;
            public string message;
            public bool started;

            public MessageData(string message, int timecount, string elementID)
            {
                this.timecount = timecount;
                this.elementID = elementID;
                this.message = message;
                started = false;
            }
            public void Begin(string elementID)
            {
                this.elementID = elementID;
                started = true;
                StartDestroy();
            }
            void StartDestroy()
            {
                if (timecount > 0)
                {
                    timecount--;
                    ins.timer.Once(1, () => StartDestroy());
                }
                else if (timecount == 0)
                {
                    ins.DestroyPopupUI(this);
                }
            }
        }

        private EventPlayer GetUser(BasePlayer player)
        {
            if (player == null) return null;
            var eventPlayer = player.GetComponent<EventPlayer>();
            if (eventPlayer == null) return null;
            return eventPlayer;
        }
        #endregion

        #region Event Management
        public object SelectEvent(string name)
        {
            if (!(EventGames.ContainsKey(name))) return string.Format(msg("noEvent"), name);
            if (_Started || _Open) return msg("isAlreadyStarted");
            Interface.CallHook("OnSelectEventGamePost", new object[] { name });
            _Event.EventType = name;
            return true;
        }
        object SetEventDetails()
        {
            var success = SelectEvent(_Event.EventType);
            if (success is string) return (string)success;
            if (EventGames.ContainsKey(_Event.EventType))
            {
                EventMode = _Event.GameMode;
                if (EventGames[_Event.EventType].RequiresSpawns && !string.IsNullOrEmpty(_Event.Spawnfile))
                {
                    Interface.CallHook("SetSpawnfile", true, _Event.Spawnfile);
                    object count = Spawns.Call("GetSpawnsCount", _Event.Spawnfile);
                    if (count is int)
                        SpawnCount.SetSpawnCount(true, (int)count);
                }
                if (EventGames[_Event.EventType].RequiresMultipleSpawns && !string.IsNullOrEmpty(_Event.Spawnfile2))
                {
                    Interface.CallHook("SetSpawnfile", false, _Event.Spawnfile2);
                    object count = Spawns.Call("GetSpawnsCount", _Event.Spawnfile2);
                    if (count is int)
                        SpawnCount.SetSpawnCount(false, (int)count);
                }
                if (_Event.GameRounds > 0)
                    Interface.CallHook("SetGameRounds", _Event.GameRounds);
                if (_Event.EnemiesToSpawn > 0)
                    Interface.CallHook("SetEnemyCount", _Event.EnemiesToSpawn);
                if (!string.IsNullOrEmpty(EventGames[_Event.EventType].ScoreType))
                    Interface.CallHook("SetScoreLimit", _Event.ScoreLimit);
                if (!string.IsNullOrEmpty(_Event.WeaponSet))
                    Interface.CallHook("ChangeWeaponSet", _Event.WeaponSet);
                if (!string.IsNullOrEmpty(_Event.Kit))
                    Interface.CallHook("OnSelectKit", _Event.Kit);
                if (!string.IsNullOrEmpty(_Event.ZoneID))
                {
                    Interface.CallHook("SetEventZone", _Event.ZoneID);
                    if (configData.Options.DropCorpseOnDeath)
                    {
                        var hasFlag = ZoneManager?.Call("HasFlag", _Event.ZoneID, "NoCorpse");
                        if (hasFlag is bool && (bool)hasFlag)
                        {
                            ZoneManager?.Call("RemoveFlag", _Event.ZoneID, "NoCorpse");
                            ResetNoCorpse = true;
                        }
                    }
                }
                if (EventGames[_Event.EventType].ForceCloseOnStart)
                    _Event.CloseOnStart = true;
                if (!EventGames[_Event.EventType].CanChooseRespawn)
                {
                    var type = Interface.CallHook("GetRespawnType");
                    if (type != null)
                        _Event.RespawnType = (RespawnType)type;
                    var time = Interface.CallHook("GetRespawnTime");
                    if (time != null)
                        _Event.RespawnTimer = (int)time;
                }
                return null;
            }
            return "Error setting event details. Please check your settings";
        }

        public object OpenEvent()
        {
            if (_Event == null) return msg("noneSelected");
            if (string.IsNullOrEmpty(_Event.EventType)) return msg("noTypeSelected");
            if (_Open) return string.Format(msg("isAlreadyOpen"), _Event.EventType);
            if (_Started && !_Open)
            {
                if (!EventGames[_Event.EventType].ForceCloseOnStart)
                {
                    _Open = true;
                    return null;
                }
                else return msg("cantOpen");
            }
            var success = ValidateEvent(_Event);
            if (success is string)
                return (string)success;
            success = SetEventDetails();
            if (success is string)
                return (string)success;
            success = Interface.Oxide.CallHook("CanEventOpen");
            if (success is string)
                return (string)success;
            EMInterface.EventVoting.OpenEventVoting(false);

            _Open = true;
            GameScores = new ScoreData();
            EventPlayers = new List<EventPlayer>();
            Joiners = new List<BasePlayer>();

            _EventName = _Event.EventType;
            if (_Event.GameMode == GameMode.Battlefield)
                _EventName = $"{msg("Battlefield")} - {_EventName}";

            if (!string.IsNullOrEmpty(_CurrentEventConfig))
                _EventName = $"{_CurrentEventConfig} ({_Event.EventType})";

            BroadcastToChat(string.Format(msg("eventOpen"), _EventName));
            Interface.Oxide.CallHook("OnEventOpenPost");

            return true;
        }
        public object CloseEvent()
        {
            if (!_Open) return msg("eventAlreadyClosed");
            _Open = false;
            Interface.Oxide.CallHook("OnEventClosePost");

            if (_Started)
                BroadcastToChat(msg("eventClose"));
            else
            {
                DestroyTimers();
                BroadcastToChat(msg("eventCancel"));
                EMInterface.EventVoting.OpenEventVoting(true);
            }
            return true;
        }

        public object StartEvent()
        {
            object success = Interface.Oxide.CallHook("CanEventStart");
            if (success is string)
                return (string)success;

            Subscribe(nameof(OnEntityTakeDamage));
            Subscribe(nameof(OnPlayerAttack));
            Subscribe(nameof(OnRunPlayerMetabolism));
            Subscribe(nameof(OnItemPickup));
            Subscribe(nameof(CanNetworkTo));
            Subscribe(nameof(CanLootPlayer));
            Subscribe(nameof(OnCreateWorldProjectile));

            Interface.Oxide.CallHook("OnEventStartPre");
            BroadcastToChat(string.Format(msg("eventBegin"), _EventName));
            _Started = true;
            _Prestarting = true;
            _Ended = false;
            _Destoyed = false;

            if (!GameStatistics.GamesPlayed.ContainsKey(_Event.EventType))
                GameStatistics.GamesPlayed.Add(_Event.EventType, 1);
            else GameStatistics.GamesPlayed[_Event.EventType]++;

            DestroyTimers();
            PreStartEvent();
            return true;
        }
        public void PreStartEvent()
        {
            if (_Event.CloseOnStart)
                CloseEvent();
            EnableGod();
            CreateEventPlayers();
        }

        public object EndEvent()
        {
            DestroyAllPopups();

            if (_Ended) return msg("noGamePlaying");

            EnableGod();
            RespawnAllPlayers();

            _Open = false;
            _Started = false;
            _Pending = false;

            DestroyTimers();

            timer.In(3, () =>
            {
                Interface.CallHook("OnEventEndPre");
                for (int i = 0; i < EventPlayers.Count; i++)
                {
                    BasePlayer player = EventPlayers[i].GetPlayer();
                    DestroyScoreboard(player);
                    CuiHelper.DestroyUi(player, ClockUI);
                }

                BroadcastToChat(string.Format(msg("restoringPlayers"), _Event.EventType));

                _Ended = true;

                Restoration.StartRestoration();
            });
            return true;
        }
        void FinalizeGameEnd()
        {
            CalculateRanks();
            DestroyGame();
            Interface.Oxide.CallHook("OnEventEndPost");
            SaveRestoreInfo();

            if (ResetNoCorpse)
            {
                ZoneManager?.Call("AddFlag", _Event.ZoneID, "NoCorpse");
                ResetNoCorpse = false;
            }
            EMInterface.EventVoting.OpenEventVoting(true);
        }

        public void CancelEvent(string reason)
        {
            DestroyTimers();

            if (_Open)
                CloseEvent();

            object success = Interface.Oxide.CallHook("OnEventCancel");
            if (success is string)
                BroadcastToChat((string)success);
            else BroadcastToChat(string.Format(msg("EventCancelled"), _EventName, reason));
            EMInterface.EventVoting.OpenEventVoting(true);
        }
        public void CancelAutoEvent(string reason)
        {
            if (_Launched)
                _Launched = false;

            CancelEvent(reason);
        }
        #endregion

        #region Player TP Management
        private void MovePosition(BasePlayer player, Vector3 destination)
        {
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "StartLoading");
            StartSleeping(player);
            player.MovePosition(destination);
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "ForcePositionTo", destination);
            if (player.net?.connection != null)
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.UpdateNetworkGroup();
            player.SendNetworkUpdateImmediate(false);
            if (player.net?.connection == null) return;
            try { player.ClearEntityQueue(null); } catch { }
            player.SendFullSnapshot();
        }
        private void StartSleeping(BasePlayer player)
        {
            if (player.IsSleeping())
                return;
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
            if (!BasePlayer.sleepingPlayerList.Contains(player))
                BasePlayer.sleepingPlayerList.Add(player);
            player.CancelInvoke("InventoryUpdate");
        }
        private object GetSpawnPosition(BasePlayer player)
        {
            var targetpos = SpawnCount.GetSpawnPoint(_Event.Spawnfile);
            if (targetpos is string)
                return null;
            var newpos = Interface.Oxide.CallHook("EventChooseSpawn", player, targetpos);
            if (newpos is Vector3)
                targetpos = newpos;
            return (Vector3)targetpos;
        }
        public void TeleportPlayerToEvent(BasePlayer player)
        {
            EMInterface.DestroyAllUI(player);
            var eventPlayer = GetUser(player);
            if (eventPlayer == null || player.net?.connection == null) return;

            var targetpos = GetSpawnPosition(player);
            if (targetpos == null)
            {
                LeaveEvent(player);
                return;
            }

            player.inventory.Strip();
            MovePosition(player, (Vector3)targetpos);
            timer.Once(2, () =>
            {
                var success = ConfirmTeleportation(player, (Vector3)targetpos);
                if (!success)
                {
                    LeaveEvent(player);
                    SendMsg(player, "tpError");
                }
                else
                {
                    eventPlayer.isDead = false;
                    ResetMetabolism(player);

                    if (_Started) Interface.Oxide.CallHook("OnEventPlayerSpawn", player);
                    if (_Event.UseClassSelector && string.IsNullOrEmpty(eventPlayer.currentClass))
                    {
                        EMInterface.CloseMap(player);
                        EMInterface.CreateMenuMain(player);
                        EMInterface.ClassSelector(player);
                    }
                }
                return;
            });
        }
        private bool ConfirmTeleportation(BasePlayer player, Vector3 position)
        {
            if (Vector3.Distance(player.transform.position, position) < 50)
                return true;
            return false;
        }
        #endregion

        #region Death and Respawn Management
        void NullifyDamage(HitInfo info)
        {
            info.damageTypes = new DamageTypeList();
            info.HitEntity = null;
            info.HitMaterial = 0;
            info.PointStart = Vector3.zero;
        }
        void OnPlayerDeath(BasePlayer player, HitInfo hitinfo)
        {
            var eventPlayer = GetUser(player);
            if (eventPlayer == null) return;

            eventPlayer.isDead = true;
            player.RemoveFromTriggers();

            Interface.Oxide.CallHook("OnEventPlayerDeath", player, hitinfo);

            AddStats(player, StatType.Deaths);

            if (_Ended)
                return;

            string attackerName = GetAttacker(player, hitinfo);
            switch (_Event.RespawnType)
            {
                case RespawnType.None:
                    ResetPlayer(player);
                    return;
                case RespawnType.Timer:
                    AddRespawnTimer(player, attackerName, false);
                    return;
                case RespawnType.Waves:
                    AddRespawnTimer(player, attackerName, true);
                    return;
            }
        }
        string GetAttacker(BasePlayer player, HitInfo hitinfo)
        {
            if (hitinfo?.Initiator == null)
                return string.Empty;
            if (hitinfo?.InitiatorPlayer != null)
            {
                var attacker = hitinfo.InitiatorPlayer;
                if (attacker != player)
                {
                    if (GetUser(attacker))
                    {
                        AddStats(attacker, StatType.Kills);
                        return attacker.displayName;
                    }
                }
            }
            if (hitinfo.Initiator is BaseHelicopter)
                return msg("a Helicopter");
            if (hitinfo.Initiator is AutoTurret)
                return msg("a AutoTurret");
            if (hitinfo.Initiator is BaseNpc)
                return hitinfo.Initiator.ShortPrefabName;
            return string.Empty;
        }

        public void ResetPlayer(BasePlayer player)
        {
            var eventPlayer = GetUser(player);
            if (eventPlayer == null) return;

            var eventCorpse = eventPlayer.GetCorpse();
            if (eventCorpse != null)
                UnityEngine.Object.Destroy(eventCorpse);

            if (eventPlayer.inEvent)
            {
                ResetMetabolism(player);

                if (eventPlayer.isLeaving)
                {
                    var restoreData = Restoration.GetPlayerData(player.userID);
                    if (restoreData != null)
                    {
                        Vector3 homePos = new Vector3(restoreData.x, restoreData.y, restoreData.z);
                        MovePosition(player, homePos);
                    }
                    else player.Respawn();
                }
                else
                {
                    var targetpos = GetSpawnPosition(player);
                    if (targetpos == null)
                    {
                        LeaveEvent(player);
                        return;
                    }
                    player.MovePosition((Vector3)targetpos);
                    player.ClientRPCPlayer(null, player, "ForcePositionTo", (Vector3)targetpos);
                    player.SendNetworkUpdateImmediate();
                    try { player.ClearEntityQueue(null); } catch { }
                }

                eventPlayer.isRespawning = false;
                eventPlayer.isDead = false;

                if (_Started && !eventPlayer.isLeaving)
                    Interface.Oxide.CallHook("OnEventPlayerSpawn", player);

                //eventPlayer.RemoveFromNetwork(1.8f);
            }
            else Restoration.RestorePlayer(player);
        }
        void ResetMetabolism(BasePlayer player)
        {
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, false);
            player.metabolism.calories.value = player.metabolism.calories.max;
            player.metabolism.hydration.value = player.metabolism.hydration.max;
            player.metabolism.bleeding.value = 0;
            player.metabolism.radiation_level.value = 0;
            player.metabolism.radiation_poison.value = 0;
            player.metabolism.SendChangesToClient();
        }

        #region Death UI
        LootableCorpse SpawnCorpse(BasePlayer player)
        {
            LootableCorpse lootableCorpse = player.DropCorpse("assets/prefabs/player/player_corpse.prefab") as LootableCorpse;
            if (lootableCorpse)
            {
                lootableCorpse.TakeFrom(player.inventory.containerMain, player.inventory.containerWear, player.inventory.containerBelt);
                lootableCorpse.playerName = player.displayName;
                lootableCorpse.playerSteamID = player.userID;
                lootableCorpse.transform.position = player.transform.position + Vector3.up;
                lootableCorpse.TakeChildren(player);
                lootableCorpse.Spawn();
                player.MovePosition(new Vector3(player.transform.position.x, -10, player.transform.position.z));
                return lootableCorpse;
            }
            return null;
        }
        public void AddRespawnTimer(BasePlayer player, string attackerName, bool isWave, bool showMsg = true)
        {
            var eventPlayer = GetUser(player);
            if (eventPlayer == null) return;

            string message = string.Empty;
            if (string.IsNullOrEmpty(attackerName)) message = msg("suicide");
            else message = string.Format(msg("deathBy"), attackerName);

            if (configData.Options.DropCorpseOnDeath)
            {
                var corpse = SpawnCorpse(player);
                if (corpse != null)
                    eventPlayer.SetCorpse(corpse.gameObject.AddComponent<EventCorpse>());
            }

            if (showMsg)
                DeathMessageUI(player, message);

            player.inventory.Strip();

            if (configData.Options.UseSpectateMode)
                StartSpectating(eventPlayer);
            else UnnetworkPlayer(eventPlayer);

            if (isWave)
                DeathTimerUI(player, _WaveTimer.GetTime(), true);
            else
                DeathTimerUI(player, _Event.RespawnTimer, false);
        }
        void DeathMessageUI(BasePlayer player, string message)
        {
            var container = UI.CreateElementContainer(DeathUI, "0 0 0 0", "0.25 0.4", "0.75 0.6", false);
            UI.CreateLabel(ref container, DeathUI, "", message, 26, "0 0", "1 1");
            CuiHelper.AddUi(player, container);
        }
        void DeathTimerUI(BasePlayer player, int time, bool wave)
        {
            int timeRemaining = time;
            var canStartTimer = Interface.CallHook("FreezeRespawn", player);
            if (canStartTimer is bool && (bool)canStartTimer)
            {
                var deathMessage = Interface.CallHook("GetRespawnMsg");
                var container = UI.CreateElementContainer(TimerUI, "0 0 0 0", "0.25 0.7", "0.75 0.85", false);
                UI.CreateOutLineLabel(ref container, TimerUI, "0 0 0 1", deathMessage is string ? (string)deathMessage : msg("respawnWait"), 26, "1.0 1.0", "0 0", "1 1");
                CuiHelper.AddUi(player, container);
            }
            else
            {
                if (RespawnTimers.ContainsKey(player.userID))
                {
                    RespawnTimers[player.userID].Destroy();
                    RespawnTimers.Remove(player.userID);
                }
                RespawnTimers.Add(player.userID, timer.Repeat(1, timeRemaining + 1, () =>
                {
                    CuiHelper.DestroyUi(player, TimerUI);
                    if (timeRemaining <= 0)
                    {
                        CuiHelper.DestroyUi(player, "EMUI_Panel");
                        EndRespawnScreen(player);
                        return;
                    }
                    string message = msg("respawnTime");
                    if (wave) message = msg("respawnWave");
                    var container = UI.CreateElementContainer(TimerUI, "0 0 0 0", "0.25 0.7", "0.75 0.85", false);
                    UI.CreateOutLineLabel(ref container, TimerUI, "0 0 0 1", string.Format(message, timeRemaining), 26, "1.0 1.0", "0 0", "1 1");
                    CuiHelper.AddUi(player, container);
                    timeRemaining--;
                }));
            }
            if (configData.Options.AllowClassSelectionOnDeath && _Event.UseClassSelector)
                EMInterface.DeathClassSelector(player);
        }
        void EndRespawnScreen(BasePlayer player)
        {
            if (RespawnTimers.ContainsKey(player.userID))
            {
                RespawnTimers[player.userID].Destroy();
                RespawnTimers.Remove(player.userID);
            }
            CuiHelper.DestroyUi(player, DeathUI);
            CuiHelper.DestroyUi(player, TimerUI);

            if (configData.Options.UseSpectateMode)
                EndSpectating(player);
            else NetworkPlayer(player);

            ResetPlayer(player);
        }
        public void RespawnAllPlayers()
        {
            foreach (EventPlayer eventPlayer in EventPlayers)
            {
                if (eventPlayer.isDead || eventPlayer.isRespawning)
                {
                    EndRespawnScreen(eventPlayer.GetPlayer());
                }
            }
        }
        #endregion

        #region Spectate
        void StartSpectating(EventPlayer eventPlayer)
        {
            var player = eventPlayer.GetPlayer();
            if (!eventPlayer.isSpectating)
            {
                eventPlayer.isSpectating = true;
                eventPlayer.isRespawning = true;

                player.inventory.Strip();
                player.playerFlags = player.playerFlags | BasePlayer.PlayerFlags.Spectating;
                player.gameObject.SetLayerRecursive(10);
                player.CancelInvoke("MetabolismUpdate");
                player.CancelInvoke("InventoryUpdate");
                player.spectateFilter = "@123nofilter123";

                if (configData.Options.DropCorpseOnDeath)
                {
                    var entity = eventPlayer?.GetCorpse()?.entity;
                    if (entity != null)
                    {
                        player.ClearEntityQueue(null);
                        player.gameObject.Identity();
                        player.SetParent(entity, 0);
                    }
                }
            }
        }
        void EndSpectating(BasePlayer player)
        {
            var eventPlayer = GetUser(player);
            if (eventPlayer == null) return;
            if (eventPlayer.isSpectating)
            {
                if (configData.Options.DropCorpseOnDeath)
                {
                    var parentEnt = player.GetParentEntity();
                    if (parentEnt != null)
                        parentEnt.RemoveChild(player);
                    player.parentEntity.Set(null);
                    player.parentBone = 0;
                }
                player.playerFlags = player.playerFlags & ~BasePlayer.PlayerFlags.Spectating;
                player.gameObject.SetLayerRecursive(17);
                eventPlayer.isSpectating = false;
                player.InvokeRepeating("InventoryUpdate", 1f, 0.1f * UnityEngine.Random.Range(0.99f, 1.01f));
                eventPlayer.isRespawning = false;
            }
        }

        void UnnetworkPlayer(EventPlayer eventPlayer)
        {
            var player = eventPlayer.GetPlayer();
            if (!eventPlayer.isSpectating)
            {
                eventPlayer.isSpectating = true;
                eventPlayer.isRespawning = true;
                eventPlayer.RemoveFromNetwork(0);
            }
        }
        void NetworkPlayer(BasePlayer player)
        {
            var eventPlayer = GetUser(player);
            if (eventPlayer == null) return;
            if (eventPlayer.isSpectating)
            {
                eventPlayer.AddToNetwork();
                eventPlayer.isSpectating = false;
                eventPlayer.isRespawning = false;
            }
        }
        #endregion
        #endregion

        #region Kit Management
        public void GivePlayerKit(BasePlayer player, string kit)
        {
            player.inventory.Strip();
            if (_Started)
            {
                if (_Event.UseClassSelector)
                {
                    if (string.IsNullOrEmpty(player.GetComponent<EventPlayer>().currentClass))
                    {
                        EMInterface.CloseMap(player);
                        EMInterface.CreateMenuMain(player);
                        EMInterface.ClassSelector(player);
                    }
                    else timer.In(1, () => GiveClassKit(player));
                }
                else timer.In(1, ()=> GiveKit(player, kit));
            }
        }
        private void GiveKit(BasePlayer player, string kitname)
        {
            Kits?.Call("GiveKit", player, kitname);
            Interface.CallHook("OnEventKitGiven", player);
            Interface.CallHook("OnPlayerSelectClass", player); // Temp fix for headquarters
        }
        private void GiveClassKit(BasePlayer player)
        {
            GiveKit(player, player.GetComponent<EventPlayer>().currentClass);
        }
        #endregion

        #region Zone Management
        void OnExitZone(string zoneId, BasePlayer player)
        {
            var eventPlayer = GetUser(player);
            if (eventPlayer == null) return;
            if (eventPlayer.isDead) return;
            if (!_Prestarting && _Started && zoneId.Equals(_Event.ZoneID))
            {
                eventPlayer.OOB = true;
                if (!KillTimers.ContainsKey(player.userID))
                {
                    SendReply(player, msg("oobMsg").Replace("{MsgColor}", configData.Messaging.MsgColor).Replace("{MainColor}", configData.Messaging.MainColor));
                    int time = 10;
                    KillTimers.Add(player.userID, timer.Repeat(1, time, () =>
                    {
                        if (eventPlayer.OOB)
                        {
                            time--;
                            SendReply(player, msg("oobMsg2").Replace("{MsgColor}", configData.Messaging.MsgColor).Replace("{MainColor}", configData.Messaging.MainColor).Replace("{time}", time.ToString()), false);

                            if (time == 0)
                            {
                                Effect.server.Run("assets/prefabs/tools/c4/effects/c4_explosion.prefab", player.transform.position);
                                if (!eventPlayer.isDead)
                                    OnPlayerDeath(player, new HitInfo());
                                PopupMessage(msg("oobMsg3").Replace("{MsgColor}", configData.Messaging.MsgColor).Replace("{MainColor}", configData.Messaging.MainColor).Replace("{playerName}", player.displayName));
                            }
                        }
                    }));
                }
            }
        }
        void OnEnterZone(string zoneID, BasePlayer player)
        {
            var eventPlayer = GetUser(player);
            if (eventPlayer == null) return;
            if (!_Prestarting && _Started && zoneID.Equals(_Event.ZoneID))
            {
                eventPlayer.OOB = false;
                if (KillTimers.ContainsKey(player.userID))
                {
                    KillTimers[player.userID].Destroy();
                    KillTimers.Remove(player.userID);
                }
            }
        }
        #endregion

        #region Player Event Management
        void EnableGod() => _GodEnabled = true;
        void DisableGod() => _GodEnabled = false;

        public object JoinEvent(BasePlayer player)
        {
            var notNull = GetUser(player);
            if ((notNull && EventPlayers.Contains(notNull)) || Joiners.Contains(player))
                return msg("alreadyJoined");

            object success = Interface.Oxide.CallHook("CanEventJoin", player);
            if (success is string)
                return (string)success;

            killLifestory.Invoke(player, null);

            UpdateName(player);

            if (_Started)
            {
                player.inventory.crafting.CancelAll(true);
                var eventPlayer = player.gameObject.AddComponent<EventPlayer>();
                EventPlayers.Add(eventPlayer);
                BroadcastToChat(string.Format(msg("successJoined"), player.displayName, EventPlayers.Count));
                if (SetEventPlayer(eventPlayer))
                {
                    HasJoinedEvent(player);
                    Interface.Oxide.CallHook("OnEventJoinPost", player);
                    TeleportPlayerToEvent(player);
                    AddStats(player, StatType.Played);
                    CreateScoreboard(player);
                }
            }
            else
            {
                Joiners.Add(player);
                BroadcastToChat(string.Format(msg("successJoined"), player.displayName, Joiners.Count));
                if (_Launched && _Open && !_Pending && Joiners.Count >= _Event.MinimumPlayers)
                {
                    int timerStart = EMInterface.Event_Config.AutoEvent_Config.AutoEvent_List[_AutoEventNum].TimeToStart;
                    BroadcastToChat(string.Format(msg("reachedMinPlayers"), _EventName, timerStart));

                    _Pending = true;
                    DestroyTimers();
                    timer.Once(timerStart, () => StartEvent());
                }
            }
            return true;
        }
        public object LeaveEvent(BasePlayer player)
        {
            if (!_Started)
            {
                if (!Joiners.Contains(player))
                    return msg("notInEvent");
                Joiners.Remove(player);
                BroadcastToChat(string.Format(msg("leftEvent"), player.displayName, Joiners.Count));
            }
            else
            {
                var eventPlayer = GetUser(player);
                if (eventPlayer == null || !EventPlayers.Contains(eventPlayer))
                    return msg("notInEvent");

                eventPlayer.isLeaving = true;

                Interface.Oxide.CallHook("OnEventLeavePre");
                Interface.Oxide.CallHook("DisableBypass", player.userID);

                if (!_Ended || _Started)
                    BroadcastToChat(string.Format(msg("leftEvent"), player.displayName, (EventPlayers.Count - 1)));

                Restoration.LeaveLoop(player);
            }
            return true;
        }

        void CreateEventPlayers()
        {
            if (Joiners.Count > 0)
            {
                var player = Joiners[0];
                if (player != null)
                {
                    if (GetUser(player) != null)
                    {
                        UnityEngine.Object.DestroyImmediate(player.GetComponent<EventPlayer>());
                        CreateEventPlayers();
                        return;
                    }
                    player.inventory.crafting.CancelAll(true);
                    var eventPlayer = player.gameObject.AddComponent<EventPlayer>();
                    EventPlayers.Add(eventPlayer);

                    if (SetEventPlayer(eventPlayer))
                    {
                        HasJoinedEvent(player);
                        Joiners.Remove(player);

                        Interface.Oxide.CallHook("OnEventJoinPost", player);

                        if (!string.IsNullOrEmpty(_Event.ZoneID))
                            ZoneManager?.Call("AddPlayerToZoneWhitelist", _Event.ZoneID, player);

                        TeleportPlayerToEvent(player);
                        AddStats(player, StatType.Played);
                    }
                }
                CreateEventPlayers();
            }
            else WaitToStart();
        }
        void WaitToStart()
        {
            if (configData.Options.UseEventPrestart && configData.Options.EventPrestartTimer > 0)
            {
                int remaining = configData.Options.EventPrestartTimer;
                EventTimers.Add(timer.Repeat(1, configData.Options.EventPrestartTimer, () =>
                {
                    remaining--;
                    if (remaining < 1)
                    {
                        foreach(var eventPlayer in EventPlayers)
                        {
                            var player = eventPlayer.GetPlayer();
                            ResetMetabolism(player);

                            var targetpos = GetSpawnPosition(player);
                            if (targetpos == null)
                            {
                                LeaveEvent(player);
                                return;
                            }
                            player.MovePosition((Vector3)targetpos);
                            player.ClientRPCPlayer(null, player, "ForcePositionTo", (Vector3)targetpos);
                            player.SendNetworkUpdateImmediate();
                            try { player.ClearEntityQueue(null); } catch { }
                        }
                        EventBegin();
                        return;
                    }
                    popupQueue.Add(new MessageData(string.Format(msg("eventBeginIn"), remaining), 1, ""));
                    UpdateMessages();
                }));
            }
            else
                EventBegin();
        }
        void EventBegin()
        {
            if (_Event.RespawnType == RespawnType.Waves)
                _WaveTimer = new GameObject().AddComponent<WaveTimer>();
            _Prestarting = false;
            DisableGod();
            Interface.Oxide.CallHook("OnEventStartPost");
        }
        bool SetEventPlayer(EventPlayer eventPlayer)
        {
            var player = eventPlayer.GetPlayer();

            Interface.Oxide.CallHook("EnableBypass", player.userID);
            eventPlayer.enabled = true;
            eventPlayer.inEvent = true;
            Restoration.StorePlayer(player);

            object lockInv = Interface.CallHook("LockingPlayerInventory", player);

            if (EventGames[_Event.EventType].LockClothing && !player.inventory.containerWear.HasFlag(ItemContainer.Flag.IsLocked))
            {
                player.inventory.containerWear.SetFlag(ItemContainer.Flag.IsLocked, true);
                lockInv = true;
            }

            if (lockInv is bool && (bool)lockInv)
                player.inventory.SendSnapshot();
            return true;
        }
        #endregion

        #region Event Methods
        public object RegisterEventGame(string name, EventSetting eventSettings, Events defaultConfig)
        {
            if (!EventGames.ContainsKey(name))
                EventGames.Add(name, eventSettings);

            var success = ValidateEvent(defaultConfig);
            if (success is string)
            {
                PrintError($"Error generating a default event config for game: {name}\n{(string)success}");
            }
            else ValidEvents.Add($"{name} Default", defaultConfig);

            Puts(string.Format("Registered event game: {0}", name));

            return true;
        }
        void OnEventStartPost()
        {
            DestroyTimers();
            if (_Launched)
                OnEventStartPostAutoEvent();
            if (_Event.GameMode == GameMode.Battlefield && !_TimerStarted)
                StartTimer(configData.Options.Battlefield_Timer);
            if (configData.Messaging.AnnounceEvent_During)
                EventTimers.Add(timer.Repeat(configData.Messaging.AnnounceEvent_Interval, 0, () => AnnounceDuringEvent()));
        }
        void OnEventStartPostAutoEvent()
        {
            if (EMInterface.Event_Config.AutoEvent_Config.AutoEvent_List[_AutoEventNum].TimeLimit != 0)
                StartTimer(EMInterface.Event_Config.AutoEvent_Config.AutoEvent_List[_AutoEventNum].TimeLimit * 60);
        }
        object CanEventStart()
        {
            if (_Event.EventType == null) return msg("noEventSet");
            if (EventGames[_Event.EventType].RequiresSpawns && string.IsNullOrEmpty(_Event.Spawnfile))
                return msg("noSpawnsSet");
            if (EventGames[_Event.EventType].RequiresMultipleSpawns && string.IsNullOrEmpty(_Event.Spawnfile2))
                return msg("noSpawnsSet");
            return _Started ? msg("alreadyStarted") : null;
        }
        object CanEventJoin(BasePlayer player)
        {
            if (!_Open) return msg("isClosed");

            if (!_Started && _Event.MaximumPlayers != 0)
                if (Joiners.Count >= _Event.MaximumPlayers)
                    return string.Format(msg("reachedMaxPlayers"), _EventName);

            if (_Started && _Event.MaximumPlayers != 0)
                if (EventPlayers.Count >= _Event.MaximumPlayers)
                    return string.Format(msg("reachedMaxPlayers"), _EventName);

            return null;
        }
        void OnEventEndPost()
        {
            if (_Launched)
                AutoEventNext();
        }
        void DestroyGame()
        {
            Unsubscribe(nameof(OnEntityTakeDamage));
            Unsubscribe(nameof(OnPlayerAttack));
            Unsubscribe(nameof(OnItemPickup));
            Unsubscribe(nameof(CanNetworkTo));
            Unsubscribe(nameof(CanLootPlayer));
            Unsubscribe(nameof(OnCreateWorldProjectile));

            DestroyTimers();
            EventPlayers.Clear();
            Joiners.Clear();
            DisableGod();
            SpawnCount.SetSpawnCount(true, -1);
            SpawnCount.SetSpawnCount(false, -1);
            _Open = false;
            _Pending = false;
            _Destoyed = true;

            var players = UnityEngine.Object.FindObjectsOfType<EventPlayer>();
            if (players != null)
                foreach (var gameObj in players)
                    UnityEngine.Object.DestroyImmediate(gameObj);

            var corpses = UnityEngine.Object.FindObjectsOfType<EventCorpse>();
            if (corpses != null)
                foreach (var gameObj in corpses)
                    UnityEngine.Object.DestroyImmediate(gameObj);
        }
        void StartTimer(int time)
        {
            if (_GameTimer != null || _TimerStarted) return;
            _GameTimer = new GameObject().AddComponent<GameTimer>();
            _GameTimer.StartTimer(time);
        }
        void DestroyTimers()
        {
            if (_GameTimer != null)
            {
                UnityEngine.Object.Destroy(_GameTimer);
                _GameTimer = null;
                _TimerStarted = false;
            }
            if (_WaveTimer != null)
            {
                UnityEngine.Object.Destroy(_WaveTimer);
            }

            foreach (Timer eventtimer in EventTimers)
                eventtimer.Destroy();
            EventTimers.Clear();

            foreach (var eventtimer in RespawnTimers)
                eventtimer.Value.Destroy();
            RespawnTimers.Clear();
        }
        #endregion

        #region Player Restoration
        private RestoreStorage DataStorage;
        class RestoreStorage
        {
            public Dictionary<ulong, RestoreInfo> playerData = new Dictionary<ulong, RestoreInfo>();
        }
        class RestorationManager
        {
            public Dictionary<ulong, RestoreInfo> playerData = new Dictionary<ulong, RestoreInfo>();
            private int restoreNum;
            private int restoreCycles;
            private bool restoreStarted;

            #region Storage
            public void StorePlayer(BasePlayer player)
            {
                RestoreInfo info = new RestoreInfo
                {
                    inventory = new Dictionary<InventoryType, List<EventInvItem>>
                    {
                        {InventoryType.Belt, GetItems(player.inventory.containerBelt).ToList() },
                        {InventoryType.Main, GetItems(player.inventory.containerMain).ToList() },
                        {InventoryType.Wear, GetItems(player.inventory.containerWear).ToList() }
                    },
                    health = player.Health(),
                    calories = player.metabolism.calories.value,
                    hydration = player.metabolism.hydration.value,
                    x = player.transform.position.x,
                    y = player.transform.position.y,
                    z = player.transform.position.z
                };
                if (!playerData.ContainsKey(player.userID))
                    playerData.Add(player.userID, info);
                else playerData[player.userID] = info;
            }
            public RestoreInfo GetPlayerData(ulong playerId)
            {
                RestoreInfo returnData;
                if (playerData.TryGetValue(playerId, out returnData))
                    return returnData;
                return null;
            }
            public void RemovePlayer(ulong playerId)
            {
                //LogInfo("- Removing player restore data");
                if (playerData.ContainsKey(playerId))
                    playerData.Remove(playerId);
            }
            public bool HasPendingRestore(ulong playerId) => playerData.ContainsKey(playerId);
            private IEnumerable<EventInvItem> GetItems(ItemContainer container)
            {
                return container.itemList.Select(item => new EventInvItem
                {
                    itemid = item.info.itemid,
                    amount = item.amount,
                    ammo = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.contents ?? 0,
                    ammotype = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.ammoType.shortname ?? null,
                    skin = item.skin,
                    condition = item.condition,
                    instanceData = item.instanceData ?? null,
                    contents = item.contents?.itemList.Select(item1 => new EventInvItem
                    {
                        itemid = item1.info.itemid,
                        amount = item1.amount,
                        condition = item1.condition
                    }).ToArray()
                });
            }
            #endregion

            #region Restoration
            public void StartRestoration()
            {
                if (restoreStarted)
                    return;
                restoreStarted = true;

                //LogInfo("--------------------------");
                //LogInfo("Initiating restore loop");
                restoreNum = 0;
                restoreCycles = 0;
                RestoreLoop();
            }
            public void LeaveLoop(BasePlayer player, int attempts = 0)
            {
                if (player != null)
                {
                    if (attempts > 3)
                    {
                        player.DieInstantly();
                        var eventPlayer = ins.GetUser(player);
                        if (eventPlayer != null)
                        {
                            ins.EventPlayers.Remove(eventPlayer);
                            UnityEngine.Object.DestroyImmediate(eventPlayer);
                        }
                        Interface.CallHook("OnEventLeavePost", player);
                        return;
                    }
                    if (ReadyToRestore(player))
                    {
                        if (RestorePlayer(player))
                        {
                            Interface.CallHook("OnEventLeavePost", player);
                            return;
                        }
                    }
                    ins.timer.In(5, ()=> LeaveLoop(player, ++attempts));
                    return;
                }
            }
            private void RestoreLoop()
            {
                //LogInfo("Running restore loop cycle");
                if (ins.EventPlayers.Count > 0)
                {
                    //LogInfo($"- There are still {ins.EventPlayers.Count} players awaiting restoration");
                    if (restoreNum > ins.EventPlayers.Count - 1)
                    {
                        //LogInfo("- Cycle complete, restarting loop");
                        restoreCycles++;
                        if (restoreCycles > 4)
                        {
                            //LogInfo($"- Cycle limit reached, destroying {ins.EventPlayers.Count} players without restoration");
                            foreach (var eventPlayer in ins.EventPlayers)
                            {
                                var eplayer = eventPlayer?.GetPlayer();
                                if (eplayer != null)
                                {
                                    eplayer?.DieInstantly();
                                    ins.SendReply(eplayer, msg("failedRestore", eplayer));
                                }
                            }
                            restoreStarted = false;
                            ins.FinalizeGameEnd();
                            return;
                        }
                        restoreNum = 0;
                        --restoreCycles;
                        ins.timer.In(5, () => RestoreLoop());
                        return;
                    }
                    //LogInfo("- Finding next player to restore");
                    var player = ins.EventPlayers[restoreNum]?.GetPlayer();
                    if (player != null)
                    {
                        //LogInfo("-- Player found");
                        if (ReadyToRestore(player))
                        {
                            //LogInfo("-- Player is ready to restore");
                            if (RestorePlayer(player))
                            {
                                //LogInfo("-- Player restored successfully");
                                RestoreLoop();
                                return;
                            }
                            //LogInfo("-- Player restoration failed");
                        }
                    }
                    else
                    {
                        ins.EventPlayers.RemoveAt(restoreNum);
                        //LogInfo("-- Invalid player found, removing from restore list");
                    }
                    restoreNum++;
                    RestoreLoop();
                    return;
                }
                else
                {
                    restoreStarted = false;
                    ins.FinalizeGameEnd();
                }
            }
            public bool ReadyToRestore(BasePlayer player)
            {
                //LogInfo($"Checking conditions for restoration: {player.displayName}");
                player.inventory.Strip();
                UnlockInventory(player);
                DestroyUI(player);

                var eventPlayer = ins.GetUser(player);
                if (eventPlayer != null)
                {
                    //LogInfo("- EventPlayer component found");
                    eventPlayer.AddToNetwork();
                    if (eventPlayer.isRespawning)
                    {
                        //LogInfo("- EPlayer is respawning");
                        ins.EndRespawnScreen(player);
                        return false;
                    }
                    if (eventPlayer.isDead)
                    {
                        //LogInfo("- EPlayer is dead");
                        ins.ResetPlayer(player);
                        return false;
                    }
                    if (eventPlayer.isSpectating)
                    {
                        //LogInfo("- EPlayer is spectating");
                        if (ins.configData.Options.UseSpectateMode)
                            ins.EndSpectating(player);
                        else ins.NetworkPlayer(player);
                        return false;
                    }
                }

                if (player.IsSleeping())
                {
                    //LogInfo("- Player is sleeping");
                    player.EndSleeping();
                    return false;
                }

                if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
                {
                    //LogInfo("- Player is receiving snapshot");
                    return false;
                }

                if (player.IsDead() || !player.IsAlive())
                {
                    //LogInfo("- Player is dead");
                    var spawnPos = ins.GetSpawnPosition(player);
                    if (spawnPos is Vector3)
                        player.RespawnAt((Vector3)spawnPos, new Quaternion());
                    else player.Respawn();
                    return false;
                }

                if (player.IsWounded() || player.health < 1)
                {
                    //LogInfo("- Player is wounded");
                    player.metabolism.Reset();
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, false);
                    return false;
                }
                //LogInfo("* Player is ready for restoration *");
                return true;
            }
            public bool RestorePlayer(BasePlayer player)
            {
                if (player == null) return true;
                //LogInfo($"Attempting to restore player: {player.displayName}");
                var restoreData = GetPlayerData(player.userID);
                if (restoreData == null)
                {
                    //LogInfo("- No restoration data found");
                    return true;
                }
                var eventPlayer = ins.GetUser(player);
                if (eventPlayer != null)
                {
                    //LogInfo("- Found EventPlayer component");
                    eventPlayer.restoreAttempts++;
                    if (eventPlayer.restoreAttempts > 3)
                    {
                        //LogInfo("- Player has reached maximum restore attempts. Destroying player");
                        ins.EventPlayers.Remove(eventPlayer);
                        UnityEngine.Object.DestroyImmediate(eventPlayer);
                        player.RespawnAt(new Vector3(restoreData.x, restoreData.y, restoreData.z), new Quaternion());
                        return true;
                    }

                    if (eventPlayer.GetCorpse() != null)
                        UnityEngine.Object.Destroy(eventPlayer.GetCorpse());
                }

                player.RemoveFromTriggers();
                RemoveFromZone(player);
                RestorePlayerStats(player, restoreData.health, restoreData.hydration, restoreData.calories);

                if (!RestoreInventory(player, restoreData))
                    return false;

                SendPlayerHome(player, new Vector3(restoreData.x, restoreData.y, restoreData.z));

                if (eventPlayer != null)
                    DestroyEventPlayer(eventPlayer);

                RemovePlayer(player.userID);
                return true;
            }
            private void UnlockInventory(BasePlayer player)
            {
                LogInfo("- Unlocking inventory");
                if (player.inventory.containerWear.HasFlag(ItemContainer.Flag.IsLocked))
                    player.inventory.containerWear.SetFlag(ItemContainer.Flag.IsLocked, false);

                if (player.inventory.containerBelt.HasFlag(ItemContainer.Flag.IsLocked))
                    player.inventory.containerBelt.SetFlag(ItemContainer.Flag.IsLocked, false);

                if (player.inventory.containerMain.HasFlag(ItemContainer.Flag.IsLocked))
                    player.inventory.containerMain.SetFlag(ItemContainer.Flag.IsLocked, false);

                player.inventory.SendSnapshot();
            }
            private void DestroyUI(BasePlayer player)
            {
                LogInfo("- Destroying game UI");
                ins.DestroyScoreboard(player);
                ins.DestroyPopupUI(player);
                CuiHelper.DestroyUi(player, ClockUI);
                CuiHelper.DestroyUi(player, DeathUI);
                CuiHelper.DestroyUi(player, TimerUI);
                CuiHelper.DestroyUi(player, TimerUI);
                CuiHelper.DestroyUi(player, "EMUI_Panel");
            }
            private void RemoveFromZone(BasePlayer player)
            {
                LogInfo("- Removing player from zone lists");
                string zoneId = ins._Event.ZoneID;
                if (!string.IsNullOrEmpty(zoneId))
                    ins.ZoneManager?.Call("RemovePlayerFromZoneWhitelist", zoneId, player);

                Interface.Oxide.CallHook("DisableBypass", player.userID);
            }
            private void RestorePlayerStats(BasePlayer player, float health, float hydration, float calories)
            {
                LogInfo("- Restoring player statistics");
                player.metabolism.Reset();
                player.health = health;
                player.metabolism.calories.value = calories;
                player.metabolism.hydration.value = hydration;
                player.metabolism.bleeding.value = 0;
                player.metabolism.SendChangesToClient();
            }
            private void SendPlayerHome(BasePlayer player, Vector3 position)
            {
                //LogInfo("- Sending player home");
                ins.MovePosition(player, position);
            }
            private bool RestoreInventory(BasePlayer player, RestoreInfo info)
            {
                LogInfo("- Attempting to restore player inventory");
                if (RestoreItems(player, info, InventoryType.Belt) && RestoreItems(player, info, InventoryType.Main) && RestoreItems(player, info, InventoryType.Wear))
                    return true;
                else
                {
                    LogInfo("- Inventory restoration failed");
                    player.inventory.Strip();
                    return false;
                }
            }
            private bool RestoreItems(BasePlayer player, RestoreInfo info, InventoryType type)
            {
                ItemContainer container = type == InventoryType.Belt ? player.inventory.containerBelt : type == InventoryType.Wear ? player.inventory.containerWear : player.inventory.containerMain;

                for (int i = 0; i < container.capacity; i++)
                {
                    var existingItem = container.GetSlot(i);
                    if (existingItem != null)
                    {
                        existingItem.RemoveFromContainer();
                        existingItem.Remove(0f);
                    }
                    if (info.inventory[type].Count > i)
                    {
                        var itemData = info.inventory[type][i];
                        var item = ItemManager.CreateByItemID(itemData.itemid, itemData.amount, itemData.skin);
                        item.condition = itemData.condition;
                        if (itemData.instanceData != null)
                            item.instanceData = itemData.instanceData;

                        var weapon = item.GetHeldEntity() as BaseProjectile;
                        if (weapon != null)
                        {
                            if (!string.IsNullOrEmpty(itemData.ammotype))
                                weapon.primaryMagazine.ammoType = ItemManager.FindItemDefinition(itemData.ammotype);
                            weapon.primaryMagazine.contents = itemData.ammo;
                        }
                        if (itemData.contents != null)
                        {
                            foreach (var contentData in itemData.contents)
                            {
                                var newContent = ItemManager.CreateByItemID(contentData.itemid, contentData.amount);
                                if (newContent != null)
                                {
                                    newContent.condition = contentData.condition;
                                    newContent.MoveToContainer(item.contents);
                                }
                            }
                        }
                        item.position = i;
                        item.SetParent(container);
                    }
                }
                if (container.itemList.Count == info.inventory[type].Count)
                    return true;
                return false;
            }
            private void DestroyEventPlayer(EventPlayer eventPlayer)
            {
                //LogInfo("- Destroying event player");
                eventPlayer.inEvent = false;
                eventPlayer.enabled = false;
                ins.EventPlayers.Remove(eventPlayer);
                UnityEngine.Object.DestroyImmediate(eventPlayer);
            }
            #endregion
        }

        static void LogInfo(string info)
        {
            if (ins.debugEnabled)
                ins.LogToFile("debug", info, ins);
        }

        [ChatCommand("restoreme")]
        void cmdRestoreMe(BasePlayer player, string command, string[] args)
        {
            if (Restoration.HasPendingRestore(player.userID))
            {
                if (Restoration.ReadyToRestore(player))
                {
                    if (Restoration.RestorePlayer(player))
                    {
                        return;
                    }
                }
                SendMsg(player, "restoreFailed");
            }
            else SendMsg(player, "noRestoreSaved");
        }
        #endregion

        #region AutoEvent Management
        public object LaunchEvent()
        {
            _Launched = true;
            if (!_Started)
            {
                if (!_Open)
                {
                    object success = AutoEventNext();
                    if (success is string)
                        return (string)success;

                    //success = OpenEvent();
                    //if (success is string)
                    //    return (string)success;
                }
                else OnEventOpenPostAutoEvent();
            }
            else OnEventStartPostAutoEvent();
            return null;
        }
        void OnEventOpenPost()
        {
            if (configData.Messaging.AnnounceEvent)
                EventTimers.Add(timer.Repeat(configData.Messaging.AnnounceEvent_Interval, 0, ()=> AnnounceEvent()));

            if (_Launched && EMInterface.Event_Config.AutoEvent_Config.AutoCancel)
                EventTimers.Add(timer.Once(EMInterface.Event_Config.AutoEvent_Config.AutoCancel_Timer * 60, () => { CloseEvent(); AutoEventNext(); }));
        }
        void OnEventOpenPostAutoEvent()
        {
            if (!_Launched) return;
            DestroyTimers();
            var autocfg = EMInterface.Event_Config.AutoEvent_Config;
            if (autocfg.AutoCancel_Timer != 0)
                EventTimers.Add(timer.Once(autocfg.AutoCancel_Timer, () => CancelEvent(msg("notEnoughPlayers"))));
            if (configData.Messaging.AnnounceEvent)
                EventTimers.Add(timer.Repeat(configData.Messaging.AnnounceEvent_Interval, 0, () => AnnounceEvent()));
        }
        object AutoEventNext()
        {
            if (ValidAutoEvents.Count == 0)
            {
                _Launched = false;
                return msg("noAuto");
            }
            var nextAutoNum = -1;
            if (_ForceNextConfig)
            {
                nextAutoNum = _NextEventNum;
                _ForceNextConfig = false;
                _NextConfigName = string.Empty;
            }
            else
            {
                if (_RandomizeAuto)
                    nextAutoNum = UnityEngine.Random.Range(0, ValidAutoEvents.Count - 1);
                else
                {
                    nextAutoNum = _AutoEventNum + 1;
                    if (nextAutoNum > ValidAutoEvents.Count - 1)
                        nextAutoNum = 0;
                }
            }

            var autocfg = ValidAutoEvents[nextAutoNum];
            if (autocfg != null)
            {
                if (EMInterface.Event_Config.Event_List.ContainsKey(autocfg.EventConfig))
                {
                    _Event = EMInterface.Event_Config.Event_List[autocfg.EventConfig];
                    _CurrentEventConfig = autocfg.EventConfig;
                    _AutoEventNum = nextAutoNum;
                }
                else return $"{msg("errorAutoFind")} {autocfg.EventConfig}";
            }
            EMInterface.EventVoting.OpenEventVoting(true);
            EventTimers.Add(timer.Once(EMInterface.Event_Config.AutoEvent_Config.GameInterval * 60, () => OpenEvent()));
            return null;
        }

        public void ValidateAllEvents()
        {
            PrintWarning("--- Validating all event configs and auto events ---");
            ValidEvents.Clear();
            if (EMInterface.Event_Config.AutoEvent_Config.AutoEvent_List.Count == 0)
            {
                PrintError("No auto events found!");
                return;
            }
            for (int i = 0; i < EMInterface.Event_Config.Event_List.Count; i++)
            {
                var eventcfg = EMInterface.Event_Config.Event_List.Keys.ToList()[i];
                if (EMInterface.Event_Config.Event_List.ContainsKey(eventcfg))
                {
                    var success = ValidateEvent(EMInterface.Event_Config.Event_List[eventcfg]);
                    if (success is string)
                    {
                        PrintError((string)success);
                    }
                    else ValidEvents.Add(eventcfg, EMInterface.Event_Config.Event_List[eventcfg]);
                }
            }
            for (int i = 0; i < EMInterface.Event_Config.AutoEvent_Config.AutoEvent_List.Count; i++)
            {
                var autocfg = EMInterface.Event_Config.AutoEvent_Config.AutoEvent_List[i];
                var success = ValidateAutoEvent(autocfg, i);
                if (success is string)
                    PrintError((string)success);
                else
                {
                    if (ValidEvents.ContainsKey(autocfg.EventConfig))
                        ValidAutoEvents.Add(autocfg);
                }
            }
            PrintWarning("--- Finished event validation ---");
            if (ValidAutoEvents.Count > 0 && configData.Options.LaunchAutoEventsOnStartup)
                LaunchEvent();
        }
        private object ValidateAutoEvent(EMInterface.AEConfig autocfg, int number)
        {
            var errorList = new List<string>();
            if (autocfg != null)
            {
                if (string.IsNullOrEmpty(autocfg.EventConfig) || !ValidEvents.ContainsKey(autocfg.EventConfig))
                {
                    EMInterface.Event_Config.AutoEvent_Config.AutoEvent_List.Remove(autocfg);
                    return $"No valid event config selected for autoevent : #{number}";
                }
                else return null;
            }
            return $"AutoEvent config is null: #{number}";
        }
        public object ValidateEvent(Events eventcfg)
        {
            var errorList = new List<string>();
            if (eventcfg != null)
            {
                if (!EventGames.ContainsKey(eventcfg.EventType))
                    errorList.Add($"Event game not registered: {eventcfg.EventType}");
                else
                {
                    if (EventGames[eventcfg.EventType].RequiresSpawns)
                    {
                        if (string.IsNullOrEmpty(eventcfg.Spawnfile))
                            errorList.Add("No spawnfile selected");
                        else if (ValidateSpawnFile(eventcfg.Spawnfile) != null)
                            errorList.Add("Invalid spawnfile selected");
                    }

                    if (EventGames[eventcfg.EventType].RequiresMultipleSpawns)
                    {
                        if (string.IsNullOrEmpty(eventcfg.Spawnfile2))
                            errorList.Add("No secondary spawnfile selected");
                        else if (ValidateSpawnFile(eventcfg.Spawnfile2) != null)
                            errorList.Add("Invalid spawnfile selected");
                    }

                    if (string.IsNullOrEmpty(eventcfg.ZoneID))
                        errorList.Add("No Zone ID selected");
                    else if (ValidateZoneID(eventcfg.ZoneID) != null)
                        errorList.Add("Invalid Zone ID");

                    if (EventGames[eventcfg.EventType].RequiresKit)
                    {
                        if (!eventcfg.UseClassSelector)
                        {
                            if (string.IsNullOrEmpty(eventcfg.Kit))
                                errorList.Add("No kit selected");
                            else if (ValidateKit(eventcfg.Kit) != null)
                                errorList.Add("Invalid kit selected");
                        }
                    }

                    if (eventcfg.MinimumPlayers <= 0)
                        errorList.Add("Minimum Players must be greater than 0");
                }
            }
            else errorList.Add("Invalid event config selected");

            if (errorList.Count > 0)
                return errorList.ToSentence();
            else return null;
        }
        #endregion

        #region File Validation
        public object ValidateSpawnFile(string name)
        {
            var success = Spawns?.Call("GetSpawnsCount", name);
            if (success is string)
                return (string)success;
            else return null;
        }
        public object ValidateZoneID(string name)
        {
            var success = ZoneManager?.Call("CheckZoneID", name);
            if (name is string && !string.IsNullOrEmpty((string)name))
                return null;
            else return msg("zoneNotExist");
        }
        public object ValidateKit(string name)
        {
            object success = Kits?.Call("isKit", name);
            if ((success is bool))
                if (!(bool)success)
                    return string.Format(msg("kitNotExist"), name);
            return null;
        }
        #endregion

        #region Prizes
        [HookMethod("AddTokens")]
        public void AddTokens(ulong userid, int amount, bool winner = false)
        {
            if (amount == 0) return;
            string tokentype = "";
            if (configData.Options.UseEconomicsAsTokens)
            {
                if (Economics)
                {
                    Economics?.Call("Deposit", userid.ToString(), amount);
                    tokentype = msg("rewardCoins");
                }
            }
            else if (ServerRewards)
            {
                ServerRewards?.Call("AddPoints", userid.ToString(), amount);
                tokentype = msg("rewardRP");
            }
            if (winner)
            {
                if (GameStatistics.Stats.ContainsKey(userid))
                    GameStatistics.Stats[userid].GamesWon++;
            }
            BasePlayer player = BasePlayer.FindByID(userid);
            if (player != null && !string.IsNullOrEmpty(tokentype))
            {
                SendReply(player, $"<color={configData.Messaging.MainColor}>{Title}:</color><color={configData.Messaging.MsgColor}> {msg("rewardText")} </color><color={configData.Messaging.MainColor}>{amount} {tokentype}</color>");
            }
        }
        #endregion

        #region Statistics
        public enum StatType
        {
            Kills, Deaths, Played, Won, Lost, Flags, Shots, Choppers, Rank
        }
        public class PlayerStatistics
        {
            public string Name;
            public int Kills;
            public int Deaths;
            public int GamesPlayed;
            public int GamesWon;
            public int GamesLost;
            public double Score;
            public int Rank;
            public int FlagsCaptured;
            public int ShotsFired;
            public int ChoppersKilled;

            public PlayerStatistics(string Name)
            {
                this.Name = Name;
            }
        }
        public class Statistics
        {
            public Dictionary<ulong, PlayerStatistics> Stats = new Dictionary<ulong, PlayerStatistics>();
            public Dictionary<string, int> GamesPlayed = new Dictionary<string, int>();

            public string GetTotalKills()
            {
                int Kills = 0;
                foreach (var player in Stats)
                    Kills += player.Value.Kills;
                return Kills.ToString();
            }
            public string GetTotalDeaths()
            {
                int Deaths = 0;
                foreach (var player in Stats)
                    Deaths += player.Value.Deaths;
                return Deaths.ToString();
            }
            public string GetTotalGamesPlayed()
            {
                int GamesPlayed = 0;
                foreach (var player in Stats)
                    if (player.Value.GamesPlayed > GamesPlayed)
                    GamesPlayed += (player.Value.GamesPlayed - GamesPlayed);
                return GamesPlayed.ToString();
            }
            public string GetTotalShotsFired()
            {
                int ShotsFired = 0;
                foreach (var player in Stats)
                    ShotsFired += player.Value.ShotsFired;
                return ShotsFired.ToString();
            }
            public string GetFlagsCaptured()
            {
                int FlagsCaptured = 0;
                foreach (var player in Stats)
                    FlagsCaptured += player.Value.FlagsCaptured;
                return FlagsCaptured.ToString();
            }
            public string GetChoppersKilled()
            {
                int ChoppersKilled = 0;
                foreach (var player in Stats)
                    ChoppersKilled += player.Value.ChoppersKilled;
                return ChoppersKilled.ToString();
            }
            public string GetTotalPlayers() => Stats.Count.ToString();

            public Dictionary<string, int> GetGamesPlayed() => GamesPlayed;
        }
        private void HasStats(BasePlayer player)
        {
            if (!StatsCache.ContainsKey(player.userID))
                StatsCache.Add(player.userID, new PlayerStatistics(player.displayName));
        }
        private void UpdateName(BasePlayer player)
        {
            HasStats(player);
            if (StatsCache[player.userID].Name != player.displayName)
                StatsCache[player.userID].Name = player.displayName;
        }
        public void AddStats(BasePlayer player, StatType type, int amount = 1)
        {
            HasStats(player);
            switch (type)
            {
                case StatType.Kills:
                    StatsCache[player.userID].Kills += amount;
                    return;
                case StatType.Deaths:
                    StatsCache[player.userID].Deaths += amount;
                    return;
                case StatType.Played:
                    StatsCache[player.userID].GamesPlayed += amount;
                    return;
                case StatType.Won:
                    StatsCache[player.userID].GamesWon += amount;
                    return;
                case StatType.Lost:
                    StatsCache[player.userID].GamesLost += amount;
                    return;
                case StatType.Flags:
                    StatsCache[player.userID].FlagsCaptured += amount;
                    return;
                case StatType.Shots:
                    StatsCache[player.userID].ShotsFired += amount;
                    return;
                case StatType.Choppers:
                    StatsCache[player.userID].ChoppersKilled += amount;
                    return;
                case StatType.Rank:
                    StatsCache[player.userID].Rank = amount;
                    return;
            }
        }
        public int GetStats(BasePlayer player, StatType type)
        {
            HasStats(player);
            switch (type)
            {
                case StatType.Kills:
                    return StatsCache[player.userID].Kills;
                case StatType.Deaths:
                    return StatsCache[player.userID].Deaths;
                case StatType.Played:
                    return StatsCache[player.userID].GamesPlayed;
                case StatType.Won:
                    return StatsCache[player.userID].GamesWon;
                case StatType.Lost:
                    return StatsCache[player.userID].GamesLost;
                case StatType.Flags:
                    return StatsCache[player.userID].FlagsCaptured;
                case StatType.Shots:
                    return StatsCache[player.userID].ShotsFired;
                case StatType.Choppers:
                    return StatsCache[player.userID].ChoppersKilled;
                case StatType.Rank:
                    return StatsCache[player.userID].Rank;
            }
            return 0;
        }
        void CalculateRanks()
        {
            foreach (var eplayer in StatsCache)
            {
                int score = 0;
                if (eplayer.Value.Kills > 0) score += eplayer.Value.Kills * 2;
                if (eplayer.Value.GamesWon > 0) score += eplayer.Value.GamesWon * 2;
                score -= eplayer.Value.Deaths;
                score += eplayer.Value.GamesPlayed;
                score -= eplayer.Value.GamesLost;
                eplayer.Value.Score = score;
            }
            var sortedPlayers = StatsCache.OrderByDescending(x => x.Value.Score).ToList();
            foreach (var eplayer in sortedPlayers)
            {
                StatsCache[eplayer.Key].Rank = sortedPlayers.IndexOf(eplayer) + 1;
            }
            SaveStatistics();
        }
        void SaveStatistics()
        {
            GameStatistics.Stats = StatsCache;
            P_Stats.WriteObject(GameStatistics);
            Puts("Saved player statistics");
        }
        void SaveRestoreInfo()
        {
            DataStorage.playerData = Restoration.playerData;
            RestoreData.WriteObject(DataStorage);
        }
        void LoadData()
        {
            try
            {
                GameStatistics = P_Stats.ReadObject<Statistics>();
                StatsCache = GameStatistics.Stats;
            }
            catch
            {
                Puts("Couldn't load player statistics, creating new datafile");
                GameStatistics = new Statistics();
            }
            try
            {
                DataStorage = RestoreData.ReadObject<RestoreStorage>();
                Restoration.playerData = DataStorage.playerData;
            }
            catch
            {
                DataStorage = new RestoreStorage();
            }
        }
        #endregion

        #region API
        [HookMethod("isPlaying")]
        public bool isPlaying(BasePlayer player)
        {
            if (GetUser(player) != null) return true;
            if (Joiners.Contains(player)) return true;
            return false;
        }
        [HookMethod("GetUserClass")]
        public string GetUserClass(BasePlayer player)
        {
            var eventPlayer = GetUser(player);
            if (eventPlayer != null)
                return eventPlayer.currentClass;
            return null;
        }

        [HookMethod("GetUserStats")]
        public JObject GetUserStats(string userId)
        {
            ulong playerId;
            if (ulong.TryParse(userId, out playerId))
            {
                if (!StatsCache.ContainsKey(playerId)) return null;
                var obj = new JObject();
                var stats = StatsCache[playerId];
                obj["ChoppersKilled"] = stats.ChoppersKilled;
                obj["Deaths"] = stats.Deaths;
                obj["FlagsCaptured"] = stats.FlagsCaptured;
                obj["GamesLost"] = stats.GamesLost;
                obj["GamesPlayed"] = stats.GamesPlayed;
                obj["GamesWon"] = stats.GamesWon;
                obj["Kills"] = stats.Kills;
                obj["Name"] = stats.Name;
                obj["Rank"] = stats.Rank;
                obj["Score"] = stats.Score;
                obj["ShotsFired"] = stats.ShotsFired;
                return obj;
            }
            else return null;
        }

        [HookMethod("GetAllStats")]
        public JObject GetAllStats()
        {
            var obj = new JObject();
            foreach(var player in StatsCache)
            {
                var stats = new JObject();
                stats["ChoppersKilled"] = player.Value.ChoppersKilled;
                stats["Deaths"] = player.Value.Deaths;
                stats["FlagsCaptured"] = player.Value.FlagsCaptured;
                stats["GamesLost"] = player.Value.GamesLost;
                stats["GamesPlayed"] = player.Value.GamesPlayed;
                stats["GamesWon"] = player.Value.GamesWon;
                stats["Kills"] = player.Value.Kills;
                stats["Rank"] = player.Value.Rank;
                stats["Name"] = player.Value.Name;
                stats["Score"] = player.Value.Score;
                stats["ShotsFired"] = player.Value.ShotsFired;
                obj[player.Key] = stats;
            }
            return obj;
        }

        [HookMethod("GetGamesPlayed")]
        public JObject GetGamesPlayed()
        {
            var obj = new JObject();
            foreach (var game in GameStatistics.GamesPlayed)
            {
                obj[game.Key] = game.Value;
            }
            return obj;
        }

        [HookMethod("GetGameStats")]
        public JObject GetGameStats()
        {
            var obj = new JObject();
            obj["ChoppersKilled"] = GameStatistics.GetChoppersKilled();
            obj["FlagsCaptured"] = GameStatistics.GetFlagsCaptured();
            obj["TotalDeaths"] = GameStatistics.GetTotalDeaths();
            obj["TotalGamesPlayed"] = GameStatistics.GetTotalGamesPlayed();
            obj["TotalKills"] = GameStatistics.GetTotalKills();
            obj["TotalPlayers"] = GameStatistics.GetTotalPlayers();
            obj["TotalShotsFired"] = GameStatistics.GetTotalShotsFired();
            return obj;
        }

        void HasJoinedEvent(BasePlayer player) => Interface.CallHook("JoinedEvent", player);
        void HasLeftEvent(BasePlayer player) => Interface.CallHook("LeftEvent", player);
        #endregion

        #region External Hooks
        private object canRedeemKit(BasePlayer player)
        {
            if (GetUser(player) != null && _Started) { return msg("noKits"); }
            return null;
        }
        private object CanTeleport(BasePlayer player)
        {
            if (GetUser(player) != null && _Started) { return msg("noTP"); }
            return null;
        }
        private object canRemove(BasePlayer player)
        {
            if (GetUser(player) != null && _Started) { return msg("noRemove"); }
            return null;
        }
        private object canShop(BasePlayer player)
        {
            if (GetUser(player) != null && _Started) { return msg("noShop"); }
            return null;
        }
        private object CanTrade(BasePlayer player)
        {
            if (GetUser(player) != null && _Started) { return msg("noTrade"); }
            return null;
        }
        #endregion

        public Events DefaultConfig = new Events
        {
            CloseOnStart = false,
            DisableItemPickup = false,
            UseClassSelector = false,
            EventType = string.Empty,
            GameMode = GameMode.Normal,
            Kit = string.Empty,
            MaximumPlayers = 0,
            MinimumPlayers = 2,
            Spawnfile = string.Empty,
            SpawnType = SpawnType.Consecutive,
            ZoneID = string.Empty,
            RespawnType = RespawnType.None,
            RespawnTimer = 10,
            EnemiesToSpawn = 0,
            GameRounds = 1,
            ScoreLimit = 10,
            Spawnfile2 = string.Empty,
            WeaponSet = string.Empty
        };

        #region Localization
        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            { "Title", "Event Manager: "},
            { "reachedMinPlayers", "The event {0} has reached min players and will start in {1} seconds"},
            { "reachedMaxPlayers", "The event {0} has reached max players. You may not join for the moment"},
            { "eventBegin", "{0} is about to begin!"},
            { "leftEvent", "{0} has left the Event! (Total Players: {1})"},
            { "successJoined", "{0} has joined the Event!  (Total Players: {1})"},
            { "alreadyJoined", "You are already in the Event."},
            { "restoringPlayers", "{0} is now over, restoring players and sending them home!"},
            { "MessagesEventEnd", "All players respawned, {0} has ended!"},
            { "noGamePlaying", "An event game is not underway."},
            { "eventCancel", "The event was cancelled!"},
            { "eventClose", "The event entrance is now closed!"},
            { "noEventSet", "An event config must first be chosen."},
            { "noSpawnsSet", "You must select spawnfiles for this event"},
            { "eventAlreadyClosed", "The event is already closed."},
            { "alreadyStarted", "An event game has already started."},
            { "notEnoughPlayers", "Not enough players" },
            { "noAuto", "No automatic events configured" },
            { "noAutoInit", "No events were successfully initialized, check that your events are correctly configured" },
            { "TimeLimit", "Time limit reached" },
            { "EventCancelled", "Event {0} was cancelled because: {1}" },
            { "eventOpen", "{0} is now open, you can join it by typing /event join" },
            { "stillOpen", "{0} is still open for contestants! You can join it by typing /event join" },
            { "isClosed", "The event is currently closed." },
            { "notInEvent", "You are not currently in the event." },
            { "kitNotExist", "The kit {0} doesn't exist" },
            {"zoneNotExist", "Invalid Zone ID" },
            { "CancelAuto", "Auto events have been cancelled" },
            {"ItemPickup", "Item pickup has been disabled during this event!" },
            {"NoLooting", "Looting has been disabled during this event!" },
            {"noEvent", "Unable to find a event called: {0}" },
            {"isAlreadyStarted", "An event is already underway" },
            {"noneSelected", "No event has been selected" },
            {"noTypeSelected", "No event type has been selected" },
            {"isAlreadyOpen","{0} is already open" },
            {"cantOpen", "This game type can not be opened once it has started" },
            {"Battlefield", "Battlefield" },
            {"tpError", "There was a error sending you to the event. Please try again" },
            {"a Helicopter","a Helicopter" },
            {"a AutoTurret","a AutoTurret" },
            {"suicide", "You killed yourself..." },
            {"deathBy", "You were killed by {0}" },
            {"respawnWait","Waiting to respawn" },
            {"respawnTime", "Respawning in {0} seconds..." },
            {"respawnWave", "The next wave spawns in {0} seconds..." },
            {"oobMsg", "<color={MsgColor}>You have</color> <color={MainColor}>10</color><color={MsgColor}> seconds to return to the arena</color>" },
            {"oobMsg2", "<color={MainColor}>{time}</color><color={MsgColor}> seconds</color>" },
            {"oobMsg3", "<color={MainColor}>{playerName}</color><color={MsgColor}> tried to run away...</color>" },
            {"errorAutoFind", "Error finding event config:" },
            {"rewardText", "You have been awarded" },
            {"rewardCoins", "Coins" },
            {"rewardRP", "RP" },
            {"noKits", "You may not redeem a kit in the arena" },
            {"noTP", "You may not teleport in the arena" },
            {"noRemove", "You may not use the remover tool in the arena" },
            {"noShop", "You can not use the store in the arena" },
            {"restoreSuccess", "You have successfully been restored" },
            {"noTrade", "You can not trade in the arena" },
            {"failedRestore", "An attempt to restore your previous state was unsuccessful. You can opt to manually restore at anytime by typing \"/restoreme\"" },
            {"noRestoreSaved", "You do not have any pending restore data" },
            {"restoreFailed", "Unable to restore you at this time as all requirements have not been met. Please try again shortly" },
            {"eventBeginIn", "The event will start in {0} seconds!" }
        };
        #endregion
    }
}