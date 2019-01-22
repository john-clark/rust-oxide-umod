using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;

using Newtonsoft.Json.Linq;

using UnityEngine;
using Facepunch;
using Rust;

namespace Oxide.Plugins
{
    [Info ("NoEscape", "Calytic", "2.1.21")]
    [Description ("Prevent commands/actions while raid and/or combat is occuring")]
    class NoEscape : RustPlugin
    {
        #region Setup & Configuration

        List<string> blockTypes = new List<string> ()
        {
            "remove",
            "tp",
            "bank",
            "trade",
            "recycle",
            "shop",
            "bgrade",
            "build",
            "repair",
            "upgrade",
            "vend",
            "kit",
            "assignbed",
            "craft",
            "mailbox"
        };

        // COMBAT SETTINGS
        bool combatBlock;
        static float combatDuration;
        bool combatOnHitPlayer;
        float combatOnHitPlayerMinCondition;
        float combatOnHitPlayerMinDamage;

        bool combatOnTakeDamage;
        float combatOnTakeDamageMinCondition;
        float combatOnTakeDamageMinDamage;

        bool combatOnHitNPC;
        bool combatOnTakeDamageNPC;

        // RAID BLOCK SETTINGS
        bool raidBlock;
        static float raidDuration;
        float raidDistance;
        bool blockOnDamage;
        float blockOnDamageMinCondition;
        bool blockOnDestroy;

        // RAID-ONLY SETTINGS
        bool ownerCheck;
        bool blockUnowned;
        bool blockAll;
        // IGNORES ALL OTHER CHECKS
        bool ownerBlock;
        bool cupboardShare;
        bool friendShare;
        bool clanShare;
        bool clanCheck;
        bool friendCheck;
        bool raiderBlock;
        List<string> raidDamageTypes;
        List<string> combatDamageTypes;

        // RAID UNBLOCK SETTINGS
        bool raidUnblockOnDeath;
        bool raidUnblockOnWakeup;
        bool raidUnblockOnRespawn;

        // COMBAT UNBLOCK SETTINGS
        bool combatUnblockOnDeath;
        bool combatUnblockOnWakeup;
        bool combatUnblockOnRespawn;

        float cacheTimer;

        // MESSAGES
        bool raidBlockNotify;
        bool combatBlockNotify;

        bool useZoneManager;
        bool zoneEnter;
        bool zoneLeave;

        bool sendUINotification;
        bool sendChatNotification;
        bool sendGUIAnnouncementsNotification;
        bool sendLustyMapNotification;

        string GUIAnnouncementTintColor = "Red";
        string GUIAnnouncementTextColor = "White";

        string LustyMapIcon = "special";
        float LustyMapDuration = 150f;

        Dictionary<string, RaidZone> zones = new Dictionary<string, RaidZone> ();
        Dictionary<string, List<string>> memberCache = new Dictionary<string, List<string>> ();
        Dictionary<string, string> clanCache = new Dictionary<string, string> ();
        Dictionary<string, List<string>> friendCache = new Dictionary<string, List<string>> ();
        Dictionary<string, DateTime> lastClanCheck = new Dictionary<string, DateTime> ();
        Dictionary<string, DateTime> lastCheck = new Dictionary<string, DateTime> ();
        Dictionary<string, DateTime> lastFriendCheck = new Dictionary<string, DateTime> ();
        Dictionary<string, bool> prefabBlockCache = new Dictionary<string, bool> ();
        internal Dictionary<ulong, BlockBehavior> blockBehaviors = new Dictionary<ulong, BlockBehavior> ();

        public static NoEscape plugin;

        [PluginReference]
        Plugin Clans, Friends, ZoneManager, GUIAnnouncements, LustyMap;

        readonly int cupboardMask = LayerMask.GetMask ("Deployed");
        readonly int blockLayer = LayerMask.GetMask ("Player (Server)");
        Dictionary<string, bool> _cachedExcludedWeapons = new Dictionary<string, bool> ();

        List<string> blockedPrefabs = new List<string> ()
        {
            "door",
            "window.bars",
            "floor.ladder.hatch",
            "floor.frame",
            "wall.frame",
            "shutter",
            "external"
        };

        List<string> exceptionPrefabs = new List<string> ()
        {
            "ladder.wooden"
        };

        List<string> exceptionWeapons = new List<string> ()
        {
            "torch"
        };

        private List<string> GetDefaultRaidDamageTypes ()
        {
            return new List<DamageType> ()
            {
                DamageType.Bullet,
                DamageType.Blunt,
                DamageType.Stab,
                DamageType.Slash,
                DamageType.Explosion,
                DamageType.Heat
            }.Select (x => x.ToString ()).ToList<string> ();
        }

        private List<string> GetDefaultCombatDamageTypes ()
        {
            return new List<DamageType> ()
            {
                DamageType.Bullet,
                DamageType.Arrow,
                DamageType.Blunt,
                DamageType.Stab,
                DamageType.Slash,
                DamageType.Explosion,
                DamageType.Heat,
                DamageType.ElectricShock
            }.Select (x => x.ToString ()).ToList<string> ();
        }

        Dictionary<string, object> blockWhenRaidDamageDefault = new Dictionary<string, object> () {
            {"enabled", true},
            {"minCondition", 100f},
        };

        Dictionary<string, object> blockWhenCombatDamageDefault = new Dictionary<string, object> () {
            {"enabled", false},
            {"minCondition", 100f},
            {"minDamage", 1f},
        };

        static Regex _htmlRegex = new Regex ("<.*?>", RegexOptions.Compiled);

        protected override void LoadDefaultConfig ()
        {
            Config ["VERSION"] = Version.ToString ();

            // RAID SETTINGS
            Config ["Raid", "Block", "enabled"] = true;
            Config ["Raid", "Block", "duration"] = 300f; // 5 minutes
            Config ["Raid", "Block", "distance"] = 100f;
            Config ["Raid", "Block", "notify"] = true;
            Config ["Raid", "Block", "damageTypes"] = GetDefaultRaidDamageTypes ();
            Config ["Raid", "Block", "includePrefabs"] = blockedPrefabs;
            Config ["Raid", "Block", "excludePrefabs"] = exceptionPrefabs;
            Config ["Raid", "Block", "excludeWeapons"] = exceptionWeapons;

            Config ["Raid", "BlockWhen", "damage"] = blockWhenRaidDamageDefault;

            Config ["Raid", "BlockWhen", "destroy"] = true;
            Config ["Raid", "BlockWhen", "unowned"] = false;

            Config ["Raid", "BlockWho", "everyone"] = true;
            Config ["Raid", "BlockWho", "owner"] = false;
            Config ["Raid", "BlockWho", "cupboardAuthorized"] = false;
            Config ["Raid", "BlockWho", "clan"] = false;
            Config ["Raid", "BlockWho", "friends"] = false;
            Config ["Raid", "BlockWho", "raider"] = false;

            Config ["Raid", "BlockExcept", "owner"] = true;
            Config ["Raid", "BlockExcept", "friends"] = false;
            Config ["Raid", "BlockExcept", "clan"] = false;

            Config ["Raid", "Zone", "enabled"] = false;
            Config ["Raid", "Zone", "enter"] = true;
            Config ["Raid", "Zone", "leave"] = false;

            Config ["Raid", "Map", "enabled"] = false;
            Config ["Raid", "Map", "icon"] = "special";
            Config ["Raid", "Map", "duration"] = 150f;

            Config ["Raid", "UnblockWhen", "death"] = true;
            Config ["Raid", "UnblockWhen", "wakeup"] = false;
            Config ["Raid", "UnblockWhen", "respawn"] = true;

            // COMBAT SETTINGS
            Config ["Combat", "Block", "enabled"] = false;
            Config ["Combat", "Block", "duration"] = 180f; // 3 minutes
            Config ["Combat", "Block", "notify"] = true;
            Config ["Combat", "Block", "damageTypes"] = GetDefaultCombatDamageTypes ();

            Config ["Combat", "BlockWhen", "giveDamage"] = blockWhenCombatDamageDefault;
            Config ["Combat", "BlockWhen", "takeDamage"] = blockWhenCombatDamageDefault;

            Config ["Combat", "BlockWhen", "npcGiveDamage"] = false;
            Config ["Combat", "BlockWhen", "npcTakeDamage"] = false;

            Config ["Combat", "UnblockWhen", "death"] = true;
            Config ["Combat", "UnblockWhen", "wakeup"] = false;
            Config ["Combat", "UnblockWhen", "respawn"] = true;

            Config ["Settings", "cacheMinutes"] = 1f;
            Config ["Settings", "Block", "Types"] = blockTypes;

            Config ["Notifications", "UI"] = true;
            Config ["Notifications", "Chat"] = true;
            Config ["Notifications", "GUIAnnouncements", "enabled"] = false;
            Config ["Notifications", "GUIAnnouncements", "backgroundColor"] = "Red";
            Config ["Notifications", "GUIAnnouncements", "textColor"] = "White";

            Config ["VERSION"] = Version.ToString ();
        }

        void Loaded ()
        {
            LoadMessages ();
        }

        void Unload ()
        {
            if (useZoneManager)
                foreach (var zone in zones.ToList ())
                    EraseZone (zone.Value.zoneid);

            var objects = GameObject.FindObjectsOfType (typeof (RaidBlock));
            if (objects != null)
                foreach (var gameObj in objects)
                    if (!((RaidBlock)gameObj).Active)
                        GameObject.Destroy (gameObj);

            objects = GameObject.FindObjectsOfType (typeof (CombatBlock));
            if (objects != null)
                foreach (var gameObj in objects)
                    if (!((CombatBlock)gameObj).Active)
                        GameObject.Destroy (gameObj);
        }

        void LoadMessages ()
        {
            lang.RegisterMessages (new Dictionary<string, string>
                {
                    { "Raid Blocked Message", "You may not do that while raid blocked ({time})" },
                    { "Combat Blocked Message", "You may do that while a in combat ({time})" },
                    { "Raid Block Complete", "You are no longer raid blocked." },
                    { "Combat Block Complete", "You are no longer combat blocked." },
                    { "Raid Block Notifier", "You are raid blocked for {time}" },
                    { "Combat Block Notifier", "You are combat blocked for {time}" },
                    { "Combat Block UI Message", "COMBAT BLOCK" },
                    { "Raid Block UI Message", "RAID BLOCK" },
                    { "Unit Seconds", "second(s)" },
                    { "Unit Minutes", "minute(s)" },
                    { "Prefix", string.Empty }
                }, this);
        }

        void CheckConfig ()
        {
            if (Config ["VERSION"] == null) {
                // FOR COMPATIBILITY WITH INITIAL VERSIONS WITHOUT VERSIONED CONFIG
                ReloadConfig ();
            } else if (GetConfig ("VERSION", string.Empty) != Version.ToString ()) {
                // ADDS NEW, IF ANY, CONFIGURATION OPTIONS
                ReloadConfig ();
            }
        }

        protected void ReloadConfig ()
        {
            Config ["VERSION"] = Version.ToString ();

            // NEW CONFIGURATION OPTIONS HERE
            // END NEW CONFIGURATION OPTIONS

            PrintToConsole ("Upgrading configuration file");
            SaveConfig ();
        }

        void OnServerInitialized ()
        {
            NoEscape.plugin = this;

            permission.RegisterPermission ("noescape.disable", this);

            blockTypes = GetConfig ("Settings", "Block", "Types", blockTypes);

            foreach (string command in blockTypes) {
                permission.RegisterPermission ("noescape.raid." + command + "block", this);
                permission.RegisterPermission ("noescape.combat." + command + "block", this);
            }

            CheckConfig ();

            // RAID SETTINGS
            raidBlock = GetConfig ("Raid", "Block", "enabled", true);
            raidDuration = GetConfig ("Raid", "Block", "duration", 300f);
            raidDistance = GetConfig ("Raid", "Block", "distance", 100f);
            raidBlockNotify = GetConfig ("Raid", "Block", "notify", true);
            raidDamageTypes = GetConfig ("Raid", "Block", "damageTypes", GetDefaultRaidDamageTypes ());
            blockedPrefabs = GetConfig ("Raid", "Block", "includePrefabs", blockedPrefabs);
            exceptionPrefabs = GetConfig ("Raid", "Block", "excludePrefabs", exceptionPrefabs);
            exceptionWeapons = GetConfig ("Raid", "Block", "excludeWeapons", exceptionWeapons);

            Dictionary<string, object> blockOnRaidDamageDetails = GetConfig ("Raid", "BlockWhen", "damage", blockWhenRaidDamageDefault);
            if (blockOnRaidDamageDetails.ContainsKey ("enabled")) {
                blockOnDamage = (bool)blockOnRaidDamageDetails ["enabled"];
            } else {
                blockOnDamage = true;
            }

            if (blockOnRaidDamageDetails.ContainsKey ("minCondition")) {
                blockOnDamageMinCondition = Convert.ToSingle (blockOnRaidDamageDetails ["minCondition"]);
            } else {
                blockOnDamageMinCondition = 100f;
            }

            blockOnDestroy = GetConfig ("Raid", "BlockWhen", "destroy", true);
            blockUnowned = GetConfig ("Raid", "BlockWhen", "unowned", false);

            blockAll = GetConfig ("Raid", "BlockWho", "everyone", true);
            ownerBlock = GetConfig ("Raid", "BlockWho", "owner", false);
            friendShare = GetConfig ("Raid", "BlockWho", "friends", false);
            clanShare = GetConfig ("Raid", "BlockWho", "clan", false);
            cupboardShare = GetConfig ("Raid", "BlockWho", "cupboardAuthorized", false);
            raiderBlock = GetConfig ("Raid", "BlockWho", "raider", false);

            ownerCheck = GetConfig ("Raid", "BlockExcept", "owner", true);
            friendCheck = GetConfig ("Raid", "BlockExcept", "friends", false);
            clanCheck = GetConfig ("Raid", "BlockExcept", "clan", false);

            useZoneManager = GetConfig ("Raid", "Zone", "enabled", false);
            zoneEnter = GetConfig ("Raid", "Zone", "enter", true);
            zoneLeave = GetConfig ("Raid", "Zone", "leave", false);

            sendLustyMapNotification = GetConfig ("Raid", "Map", "enabled", false);
            LustyMapIcon = GetConfig ("Raid", "Map", "icon", "special");
            LustyMapDuration = GetConfig ("Raid", "Map", "duration", 150f);

            raidUnblockOnDeath = GetConfig ("Raid", "UnblockWhen", "death", true);
            raidUnblockOnWakeup = GetConfig ("Raid", "UnblockWhen", "wakeup", false);
            raidUnblockOnRespawn = GetConfig ("Raid", "UnblockWhen", "respawn", true);

            // COMBAT SETTINGS
            combatBlock = GetConfig ("Combat", "Block", "enabled", false);
            combatDuration = GetConfig ("Combat", "Block", "duration", 180f);
            combatBlockNotify = GetConfig ("Combat", "Block", "notify", true);
            combatDamageTypes = GetConfig ("Combat", "Block", "damageTypes", GetDefaultCombatDamageTypes ());

            //combatOnHitPlayer = GetConfig ("Combat", "BlockWhen", "giveDamage", true);
            Dictionary<string, object> blockOnCombatGiveDamageDetails = GetConfig ("Combat", "BlockWhen", "giveDamage", blockWhenCombatDamageDefault);
            if (blockOnCombatGiveDamageDetails.ContainsKey ("enabled")) {
                combatOnHitPlayer = (bool)blockOnCombatGiveDamageDetails ["enabled"];
            } else {
                combatOnHitPlayer = false;
            }

            if (blockOnCombatGiveDamageDetails.ContainsKey ("minCondition")) {
                combatOnHitPlayerMinCondition = Convert.ToSingle (blockOnCombatGiveDamageDetails ["minCondition"]);
            } else {
                combatOnHitPlayerMinCondition = 100f;
            }

            //combatOnTakeDamage = GetConfig ("Combat", "BlockWhen", "takeDamage", true);
            Dictionary<string, object> blockOnCombatTakeDamageDetails = GetConfig ("Combat", "BlockWhen", "takeDamage", blockWhenCombatDamageDefault);
            if (blockOnCombatTakeDamageDetails.ContainsKey ("enabled")) {
                combatOnTakeDamage = (bool)blockOnCombatTakeDamageDetails ["enabled"];
            } else {
                combatOnTakeDamage = false;
            }

            if (blockOnCombatTakeDamageDetails.ContainsKey ("minCondition")) {
                combatOnTakeDamageMinCondition = Convert.ToSingle (blockOnCombatTakeDamageDetails ["minCondition"]);
            } else {
                combatOnTakeDamageMinCondition = 100f;
            }

            if (blockOnCombatTakeDamageDetails.ContainsKey ("minDamage")) {
                combatOnTakeDamageMinDamage = Convert.ToSingle (blockOnCombatTakeDamageDetails ["minDamage"]);
            } else {
                combatOnTakeDamageMinDamage = 1f;
            }

            combatOnHitNPC = GetConfig ("Combat", "BlockWhen", "npcGiveDamage", false);
            combatOnTakeDamageNPC = GetConfig ("Combat", "BlockWhen", "npcTakeDamage", false);

            combatUnblockOnDeath = GetConfig ("Combat", "UnblockWhen", "death", true);
            combatUnblockOnWakeup = GetConfig ("Combat", "UnblockWhen", "wakeup", false);
            combatUnblockOnRespawn = GetConfig ("Combat", "UnblockWhen", "respawn", true);

            cacheTimer = GetConfig ("Settings", "cacheMinutes", 1f);

            sendUINotification = GetConfig ("Notifications", "UI", true);
            sendChatNotification = GetConfig ("Notifications", "Chat", true);

            sendGUIAnnouncementsNotification = GetConfig ("Notifications", "GUIAnnouncements", "enabled", false);
            GUIAnnouncementTintColor = GetConfig ("Notifications", "GUIAnnouncements", "backgroundColor", "Red");
            GUIAnnouncementTextColor = GetConfig ("Notifications", "GUIAnnouncements", "textColor", "White");

            if ((clanShare || clanCheck) && !Clans) {
                clanShare = false;
                clanCheck = false;
                PrintWarning ("Clans not found! All clan options disabled. Cannot use clan options without this plugin. http://oxidemod.org/plugins/clans.2087");
            }

            if (friendShare && !Friends) {
                friendShare = false;
                friendCheck = false;
                PrintWarning ("Friends not found! All friend options disabled. Cannot use friend options without this plugin. http://oxidemod.org/plugins/friends-api.686");
            }

            if (useZoneManager && !ZoneManager) {
                useZoneManager = false;
                PrintWarning ("ZoneManager not found! All zone options disabled. Cannot use zone options without this plugin. http://oxidemod.org/plugins/zones-manager.739");
            }

            if (sendGUIAnnouncementsNotification && !GUIAnnouncements) {
                sendGUIAnnouncementsNotification = false;
                PrintWarning ("GUIAnnouncements not found! GUI announcement option disabled. Cannot use gui announcement integration without this plugin. http://oxidemod.org/plugins/gui-announcements.1222");
            }

            if (sendLustyMapNotification && !LustyMap) {
                sendLustyMapNotification = false;
                PrintWarning ("LustyMap not found! LustyMap notification option disabled. Cannot use LustyMap integration without this plugin. http://oxidemod.org/plugins/lustymap.1333");
            }

            if (sendLustyMapNotification && LustyMap && LustyMapDuration <= 0) {
                PrintWarning ("LustyMap icon duration is zero, no icon will be displayed");
            }

            UnsubscribeHooks ();
        }

        void UnsubscribeHooks ()
        {
            if (!blockOnDestroy && !raidUnblockOnDeath && !combatUnblockOnDeath)
                Unsubscribe ("OnEntityDeath");

            if (!raidUnblockOnWakeup && !combatUnblockOnWakeup)
                Unsubscribe ("OnPlayerSleepEnded");

            if (!combatOnTakeDamage && !combatOnHitPlayer)
                Unsubscribe ("OnPlayerAttack");

            if (!blockOnDamage)
                Unsubscribe ("OnEntityTakeDamage");

            if (!blockTypes.Contains ("repair"))
                Unsubscribe ("OnStructureRepair");

            if (!blockTypes.Contains ("upgrade"))
                Unsubscribe ("OnStructureUpgrade");

            if (!blockTypes.Contains ("mailbox"))
                Unsubscribe ("CanUseMailbox");

            if (!blockTypes.Contains ("vend"))
                Unsubscribe ("CanUseVending");

            if (!blockTypes.Contains ("build"))
                Unsubscribe ("CanBuild");

            if (!blockTypes.Contains ("assignbed"))
                Unsubscribe ("CanAssignBed");

            if (!blockTypes.Contains ("craft"))
                Unsubscribe ("CanCraft");
        }

        #endregion

        #region Classes

        public class RaidZone
        {
            public string zoneid;
            public Vector3 position;
            public Timer timer;

            public RaidZone (string zoneid, Vector3 position)
            {
                this.zoneid = zoneid;
                this.position = position;
            }

            public float Distance (RaidZone zone)
            {
                return Vector3.Distance (position, zone.position);
            }

            public float Distance (Vector3 pos)
            {
                return Vector3.Distance (position, pos);
            }

            public RaidZone ResetTimer ()
            {
                if (timer is Timer && !timer.Destroyed)
                    timer.Destroy ();

                return this;
            }
        }

        public abstract class BlockBehavior : MonoBehaviour
        {
            protected BasePlayer player;
            public DateTime lastBlock = DateTime.MinValue;
            public DateTime lastNotification = DateTime.MinValue;
            internal DateTime lastUINotification = DateTime.MinValue;
            internal Timer timer;
            internal Action notifyCallback;
            internal string iconUID;
            internal bool moved;

            public void CopyFrom (BlockBehavior behavior)
            {
                lastBlock = behavior.lastBlock;
                lastNotification = behavior.lastNotification;
                lastUINotification = behavior.lastUINotification;
                timer = behavior.timer;
                notifyCallback = behavior.notifyCallback;
                iconUID = behavior.iconUID;
                NotificationWindow = behavior.NotificationWindow;
            }

            internal abstract float Duration { get; }

            internal abstract CuiRectTransformComponent NotificationWindow { get; set; }

            internal abstract string notifyMessage { get; }

            internal string BlockName {
                get {
                    return GetType ().Name;
                }
            }

            public bool Active {
                get {
                    if (lastBlock > DateTime.MinValue) {
                        TimeSpan ts = DateTime.Now - lastBlock;
                        if (ts.TotalSeconds < Duration) {
                            return true;
                        }
                    }

                    GameObject.Destroy (this);

                    return false;
                }
            }

            void Awake ()
            {
                player = GetComponent<BasePlayer> ();
                if (plugin.blockBehaviors.ContainsKey (player.userID)) {
                    plugin.blockBehaviors.Remove (player.userID);
                }
                plugin.blockBehaviors.Add (player.userID, this);
            }

            void Destroy ()
            {
                if (!moved) {
                    Stop ();
                }
                CancelInvoke ("Update");
            }

            void Update ()
            {
                if (!plugin.sendUINotification)
                    return;
                bool send = false;
                if (lastUINotification == DateTime.MinValue) {
                    lastUINotification = DateTime.Now;
                    send = true;
                } else {
                    TimeSpan ts = DateTime.Now - lastUINotification;
                    if (ts.TotalSeconds > 2) {
                        send = true;
                    } else {
                        send = false;
                    }
                }

                if (player is BasePlayer && player.IsConnected) {
                    if (!Active) {
                        CuiHelper.DestroyUi (player, "BlockMsg" + BlockName);
                    }

                    if (send && Active) {
                        lastUINotification = DateTime.Now;
                        SendGUI ();
                    }
                }
            }

            public void Stop ()
            {
                if (notifyCallback is Action)
                    notifyCallback.Invoke ();

                if (timer is Timer && !timer.Destroyed)
                    timer.Destroy ();

                if (plugin.sendUINotification && player is BasePlayer && player.IsConnected)
                    CuiHelper.DestroyUi (player, "BlockMsg" + BlockName);

                plugin.blockBehaviors.Remove (player.userID);

                GameObject.Destroy (this);
            }

            public void Notify (Action callback)
            {
                if (plugin.sendUINotification)
                    SendGUI ();

                notifyCallback = callback;
                if (timer is Timer && !timer.Destroyed)
                    timer.Destroy ();

                timer = plugin.timer.In (Duration, callback);
            }

            private string FormatTime (TimeSpan ts)
            {
                if (ts.Days > 0)
                    return string.Format ("{0}D, {1}H", ts.Days, ts.Hours);

                if (ts.Hours > 0)
                    return string.Format ("{0}H {1}M", ts.Hours, ts.Minutes);

                return string.Format ("{0}M {1}S", ts.Minutes, ts.Seconds);
            }

            void SendGUI ()
            {
                TimeSpan ts = lastBlock.AddSeconds (Duration) - DateTime.Now;

                string countDown = FormatTime (ts);
                CuiHelper.DestroyUi (player, "BlockMsg" + BlockName);
                var elements = new CuiElementContainer ();
                var BlockMsg = elements.Add (new CuiPanel {
                    Image =
                        {
                            Color = "0.95 0 0.02 0.67"
                        },
                    RectTransform =
                        {
                            AnchorMax = NotificationWindow.AnchorMax,
                            AnchorMin = NotificationWindow.AnchorMin
                        }
                }, "Hud", "BlockMsg" + BlockName);
                elements.Add (new CuiElement {
                    Parent = BlockMsg,
                    Components =
                        {
                            new CuiRawImageComponent
                            {
                                Sprite = "assets/icons/explosion.png",
                                Color = "0.95 0 0.02 0.67"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "0.13 1"
                            }
                        }
                });
                elements.Add (new CuiLabel {
                    RectTransform =
                        {
                            AnchorMin = "0.15 0",
                            AnchorMax = "0.82 1"
                        },
                    Text =
                        {
                            Text = notifyMessage,
                            FontSize = 11,
                            Align = TextAnchor.MiddleLeft,
                        }
                }, BlockMsg);
                elements.Add (new CuiElement {
                    Name = "TimerPanel",
                    Parent = BlockMsg,
                    Components =
                        {
                            new CuiImageComponent
                            {
                                Color = "0 0 0 0.64",
                                ImageType = UnityEngine.UI.Image.Type.Filled
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.73 0",
                                AnchorMax = "1 1"
                            }
                        }
                });
                elements.Add (new CuiLabel {
                    RectTransform =
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        },
                    Text =
                        {
                            Text = countDown,
                            FontSize = 12,
                            Align = TextAnchor.MiddleCenter,
                        }
                }, "TimerPanel");
                CuiHelper.AddUi (player, elements);
            }


        }

        public class CombatBlock : BlockBehavior
        {
            internal override float Duration {
                get {
                    return combatDuration;
                }
            }

            internal override string notifyMessage {
                get { return GetMsg ("Combat Block UI Message", player); }
            }

            CuiRectTransformComponent _notificationWindow = null;

            internal override CuiRectTransformComponent NotificationWindow {
                get {
                    if (_notificationWindow != null) {
                        return _notificationWindow;
                    }
                    return _notificationWindow = new CuiRectTransformComponent () {
                        AnchorMin = "0.87 0.35",
                        AnchorMax = "0.99 0.38"
                    };
                }
                set {
                    _notificationWindow = value;
                }
            }
        }

        public class RaidBlock : BlockBehavior
        {
            internal override float Duration {
                get {
                    return raidDuration;
                }
            }

            internal override string notifyMessage {
                get { return GetMsg ("Raid Block UI Message", player); }
            }

            private CuiRectTransformComponent _notificationWindow = null;

            internal override CuiRectTransformComponent NotificationWindow {
                get {
                    if (_notificationWindow != null) {
                        return _notificationWindow;
                    }
                    return _notificationWindow = new CuiRectTransformComponent () {
                        AnchorMin = "0.87 0.39",
                        AnchorMax = "0.99 0.42"
                    };
                }
                set {
                    _notificationWindow = value;
                }
            }
        }

        #endregion

        #region Oxide Hooks

        void OnEntityTakeDamage (BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (!blockOnDamage || !raidBlock)
                return;
            if (hitInfo == null || hitInfo.Initiator == null || !IsEntityBlocked (entity) || hitInfo.Initiator.transform == null)
                return;
            if (!IsRaidDamage (hitInfo.damageTypes))
                return;
            if (IsExcludedWeapon (hitInfo?.WeaponPrefab?.ShortPrefabName))
                return;

            if (GetHealthPercent (entity, hitInfo.damageTypes.Total ()) > blockOnDamageMinCondition) {
                return;
            }

            StructureAttack (entity, hitInfo.Initiator, hitInfo?.WeaponPrefab?.ShortPrefabName, hitInfo.HitPositionWorld);
        }

        void OnPlayerInit (BasePlayer player)
        {
            BlockBehavior behavior;
            if (blockBehaviors.TryGetValue (player.userID, out behavior)) {
                if (behavior is RaidBlock) {
                    var raidBlockComponent = player.gameObject.AddComponent<RaidBlock> ();
                    raidBlockComponent.CopyFrom (behavior);
                } else if (behavior is CombatBlock) {
                    var combatBlockComponent = player.gameObject.AddComponent<CombatBlock> ();
                    combatBlockComponent.CopyFrom (behavior);
                }

                behavior.moved = true;
                GameObject.Destroy (behavior);
            }
        }

        void OnPlayerAttack (BasePlayer attacker, HitInfo hitInfo)
        {
            if (!combatBlock || !(hitInfo.HitEntity is BasePlayer))
                return;
            if (!combatOnHitNPC && hitInfo.HitEntity.IsNpc)
                return;
            if (!combatOnTakeDamageNPC && attacker.IsNpc) {
                return;
            }
            if (!IsCombatDamage (hitInfo.damageTypes))
                return;

            float totalDamage = hitInfo.damageTypes.Total ();
            BasePlayer target = hitInfo.HitEntity as BasePlayer;

            if (combatOnTakeDamage) {
                if (GetHealthPercent (target, hitInfo.damageTypes.Total ()) > combatOnTakeDamageMinCondition) {
                    return;
                }

                if (totalDamage < combatOnTakeDamageMinDamage) {
                    return;
                }


                StartCombatBlocking (target);
            }

            if (combatOnHitPlayer) {
                if (GetHealthPercent (attacker, hitInfo.damageTypes.Total ()) > combatOnHitPlayerMinCondition) {
                    return;
                }

                if (totalDamage < combatOnHitPlayerMinDamage) {
                    return;
                }

                StartCombatBlocking (attacker);
            }
        }

        void OnEntityDeath (BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (blockOnDestroy && raidBlock) {
                if (hitInfo == null || hitInfo.Initiator == null || !IsRaidDamage (hitInfo.damageTypes) || !IsEntityBlocked (entity))
                    return;

                StructureAttack (entity, hitInfo.Initiator, hitInfo?.WeaponPrefab?.ShortPrefabName, hitInfo.HitPositionWorld);
            }

            if (entity.ToPlayer () == null)
                return;

            var player = entity.ToPlayer ();
            RaidBlock raidBlocker;
            if (raidBlock && raidUnblockOnDeath && TryGetBlocker (player, out raidBlocker)) {
                timer.In (0.3f, delegate () {
                    raidBlocker.Stop ();
                });
            }

            CombatBlock combatBlocker;
            if (combatBlock && combatUnblockOnDeath && TryGetBlocker (player, out combatBlocker)) {
                timer.In (0.3f, delegate () {
                    combatBlocker.Stop ();
                });
            }
        }

        void OnPlayerSleepEnded (BasePlayer player)
        {
            if (player == null) return;
            RaidBlock raidBlocker;
            if (raidBlock && raidUnblockOnWakeup && TryGetBlocker (player, out raidBlocker)) {
                timer.In (0.3f, delegate () {
                    raidBlocker.Stop ();
                });
            }

            CombatBlock combatBlocker;
            if (combatBlock && combatUnblockOnWakeup && TryGetBlocker (player, out combatBlocker)) {
                timer.In (0.3f, delegate () {
                    combatBlocker.Stop ();
                });
            }
        }

        void OnPlayerRespawned (BasePlayer player)
        {
            if (player == null) return;
            RaidBlock raidBlocker;
            if (raidBlock && raidUnblockOnRespawn && TryGetBlocker (player, out raidBlocker)) {
                timer.In (0.3f, delegate () {
                    raidBlocker.Stop ();
                });
            }

            CombatBlock combatBlocker;
            if (combatBlock && combatUnblockOnRespawn && TryGetBlocker (player, out combatBlocker)) {
                timer.In (0.3f, delegate () {
                    combatBlocker.Stop ();
                });
            }
        }

        #endregion

        #region Block Handling

        void StructureAttack (BaseEntity targetEntity, BaseEntity sourceEntity, string weapon, Vector3 hitPosition)
        {
            BasePlayer source = null;

            if (sourceEntity.ToPlayer () is BasePlayer)
                source = sourceEntity.ToPlayer ();
            else {
                ulong ownerID = sourceEntity.OwnerID;
                if (ownerID.IsSteamId ())
                    source = BasePlayer.FindByID (ownerID);
                else
                    return;
            }

            if (source == null)
                return;

            List<string> sourceMembers = null;

            if (targetEntity.OwnerID.IsSteamId () || (blockUnowned && !targetEntity.OwnerID.IsSteamId ())) {
                if (clanCheck || friendCheck)
                    sourceMembers = getFriends (source.UserIDString);

                if (blockAll) {
                    BlockAll (source, targetEntity, sourceMembers);
                } else {
                    if (ownerBlock)
                        OwnerBlock (source, sourceEntity, targetEntity.OwnerID, targetEntity.transform.position, sourceMembers);

                    if (raiderBlock)
                        RaiderBlock (source, targetEntity.OwnerID, targetEntity.transform.position, sourceMembers);
                }
            }
        }

        float GetHealthPercent (BaseEntity entity, float damage = 0f)
        {
            return (entity.Health () - damage) * 100f / entity.MaxHealth ();
        }

        void BlockAll (BasePlayer source, BaseEntity targetEntity, List<string> sourceMembers = null)
        {
            if (ShouldBlockEscape (targetEntity.OwnerID, source.userID, sourceMembers)) {
                StartRaidBlocking (source, targetEntity.transform.position);
            }

            var nearbyTargets = Pool.GetList<BasePlayer> ();
            Vis.Entities (targetEntity.transform.position, raidDistance, nearbyTargets, blockLayer);
            if (nearbyTargets.Count > 0) {
                RaidBlock blocker;
                foreach (BasePlayer nearbyTarget in nearbyTargets) {
                    if (nearbyTarget.IsNpc) continue;
                    if (nearbyTarget.userID == source.userID) continue;
                    if (TryGetBlocker (nearbyTarget, out blocker) && blocker.Active) {
                        StartRaidBlocking (nearbyTarget, targetEntity.transform.position);
                    } else if (ShouldBlockEscape (nearbyTarget.userID, source.userID, sourceMembers)) {
                        StartRaidBlocking (nearbyTarget, targetEntity.transform.position);
                    }
                }
            }

            Pool.FreeList (ref nearbyTargets);
        }

        void OwnerBlock (BasePlayer source, BaseEntity sourceEntity, ulong target, Vector3 position, List<string> sourceMembers = null)
        {
            if (!ShouldBlockEscape (target, source.userID, sourceMembers))
                return;

            var targetMembers = new List<string> ();

            if (clanShare || friendShare)
                targetMembers = getFriends (target.ToString ());

            var nearbyTargets = Pool.GetList<BasePlayer> ();
            Vis.Entities (position, raidDistance, nearbyTargets, blockLayer);
            if (cupboardShare)
                sourceMembers = CupboardShare (target.ToString (), position, sourceEntity, sourceMembers);

            if (nearbyTargets.Count > 0) {
                foreach (BasePlayer nearbyTarget in nearbyTargets) {
                    if (nearbyTarget.IsNpc) continue;
                    if (nearbyTarget.userID == target || (targetMembers != null && targetMembers.Contains (nearbyTarget.UserIDString)))
                        StartRaidBlocking (nearbyTarget, position);
                }
            }

            Pool.FreeList (ref nearbyTargets);
        }

        List<string> CupboardShare (string owner, Vector3 position, BaseEntity sourceEntity, List<string> sourceMembers = null)
        {
            var nearbyCupboards = Pool.GetList<BuildingPrivlidge> ();
            Vis.Entities (position, raidDistance, nearbyCupboards, cupboardMask);
            if (sourceMembers == null)
                sourceMembers = new List<string> ();

            List<string> cupboardMembers = new List<string> ();

            var sourcePlayer = sourceEntity as BasePlayer;

            if (sourcePlayer != null) {
                foreach (var cup in nearbyCupboards) {
                    if (cup.IsAuthed (sourcePlayer)) {
                        bool ownerOrFriend = false;

                        if (owner == cup.OwnerID.ToString ())
                            ownerOrFriend = true;

                        foreach (var member in sourceMembers) {
                            if (member == cup.OwnerID.ToString ())
                                ownerOrFriend = true;
                        }

                        if (ownerOrFriend)
                            foreach (var proto in cup.authorizedPlayers)
                                if (!sourceMembers.Contains (proto.userid.ToString ()))
                                    cupboardMembers.Add (proto.userid.ToString ());
                    }
                }
            }

            sourceMembers.AddRange (cupboardMembers);
            Pool.FreeList (ref nearbyCupboards);

            return sourceMembers;
        }

        void RaiderBlock (BasePlayer source, ulong target, Vector3 position, List<string> sourceMembers = null)
        {
            if (!ShouldBlockEscape (target, source.userID, sourceMembers))
                return;

            var targetMembers = new List<string> ();

            if ((clanShare || friendShare) && sourceMembers == null)
                sourceMembers = getFriends (source.UserIDString);

            var nearbyTargets = Pool.GetList<BasePlayer> ();
            Vis.Entities (position, raidDistance, nearbyTargets, blockLayer);
            if (nearbyTargets.Count > 0) {
                foreach (BasePlayer nearbyTarget in nearbyTargets) {
                    if (nearbyTarget.IsNpc) continue;
                    if (nearbyTarget == source || (sourceMembers != null && sourceMembers.Contains (nearbyTarget.UserIDString)))
                        StartRaidBlocking (nearbyTarget, position);
                }
            }

            Pool.FreeList (ref nearbyTargets);
        }

        #endregion

        #region API

        bool IsBlocked (string target)
        {
            var player = BasePlayer.Find (target);
            if (player is BasePlayer) {
                return IsBlocked (player);
            }

            return false;
        }

        bool IsBlocked (BasePlayer target)
        {
            if (IsBlocked<RaidBlock> (target) || IsBlocked<CombatBlock> (target))
                return true;

            return false;
        }

        public bool IsBlocked<T> (BasePlayer target) where T : BlockBehavior
        {
            T behavior;
            if (TryGetBlocker<T> (target, out behavior) && behavior.Active)
                return true;

            return false;
        }

        bool IsRaidBlocked (BasePlayer target)
        {
            return IsBlocked<RaidBlock> (target);
        }

        bool IsCombatBlocked (BasePlayer target)
        {
            return IsBlocked<CombatBlock> (target);
        }

        bool IsEscapeBlocked (string target)
        {
            var player = BasePlayer.Find (target);
            if (player is BasePlayer) {
                return IsBlocked (player);
            }

            return false;
        }

        bool IsRaidBlocked (string target)
        {
            var player = BasePlayer.Find (target);
            if (player is BasePlayer) {
                return IsBlocked<RaidBlock> (player);
            }

            return false;
        }

        bool IsCombatBlocked (string target)
        {
            var player = BasePlayer.Find (target);
            if (player is BasePlayer) {
                return IsBlocked<CombatBlock> (player);
            }

            return false;
        }

        bool ShouldBlockEscape (ulong target, ulong source, List<string> sourceMembers = null)
        {
            if (target == source) {
                if ((ownerBlock || raiderBlock || blockAll) && (!ownerCheck))
                    return true;

                return false;
            }

            if (sourceMembers is List<string> && sourceMembers.Count > 0 && sourceMembers.Contains (target.ToString ()))
                return false;

            return true;
        }

        //[ChatCommand ("bblocked")]
        //void cmdBBlocked (BasePlayer player, string command, string [] args)
        //{
        //    StartCombatBlocking (player);
        //    StartRaidBlocking (player);
        //}

        //[ChatCommand ("bunblocked")]
        //void cmdBUnblocked (BasePlayer player, string command, string [] args)
        //{
        //    StopCombatBlocking (player);
        //    StopRaidBlocking (player);
        //}

        void StartRaidBlocking (BasePlayer target, bool createZone = true)
        {
            StartRaidBlocking (target, target.transform.position, createZone);
        }

        void StartRaidBlocking (BasePlayer target, Vector3 position, bool createZone = true)
        {
            if (HasPerm (target.UserIDString, "disable")) {
                return;
            }

            if (target.gameObject == null) {
                return;
            }

            if (Interface.Call ("CanRaidBlock", target, position, createZone) != null) {
                return;
            }

            if (target.gameObject == null)
                return;
            var raidBlocker = target.gameObject.GetComponent<RaidBlock> ();
            if (raidBlocker == null) {
                raidBlocker = target.gameObject.AddComponent<RaidBlock> ();
            }

            Interface.CallHook ("OnRaidBlock", target, position);

            raidBlocker.lastBlock = DateTime.Now;

            if (raidBlockNotify)
                SendBlockMessage (target, raidBlocker, "Raid Block Notifier", "Raid Block Complete");

            if (useZoneManager && createZone && (zoneEnter || zoneLeave))
                CreateRaidZone (position);
        }

        void StartCombatBlocking (BasePlayer target)
        {
            if (HasPerm (target.UserIDString, "disable")) {
                return;
            }

            if (target.gameObject == null) {
                return;
            }

            if (Interface.Call ("CanCombatBlock", target) != null) {
                return;
            }

            var combatBlocker = target.gameObject.GetComponent<CombatBlock> ();
            if (combatBlocker == null) {
                combatBlocker = target.gameObject.AddComponent<CombatBlock> ();
            }

            Interface.CallHook ("OnCombatBlock", target);

            combatBlocker.lastBlock = DateTime.Now;

            if (combatBlockNotify)
                SendBlockMessage (target, combatBlocker, "Combat Block Notifier", "Combat Block Complete");
        }

        void StopBlocking (BasePlayer target)
        {
            if (IsRaidBlocked (target))
                StopBlocking<RaidBlock> (target);
            if (IsCombatBlocked (target))
                StopBlocking<CombatBlock> (target);
        }

        public void StopBlocking<T> (BasePlayer target) where T : BlockBehavior
        {
            if (target.gameObject == null)
                return;
            var block = target.gameObject.GetComponent<T> ();
            if (block is BlockBehavior)
                block.Stop ();

            if (block is RaidBlock) {
                Interface.CallHook ("OnRaidBlockStopped", target);
            } else if (block is CombatBlock) {
                Interface.CallHook ("OnCombatBlockStopped", target);
            }
        }

        void ClearRaidBlockingS (string target)
        {
            StopRaidBlocking (target);
        }

        void StopRaidBlocking (BasePlayer player)
        {
            if (player is BasePlayer && IsRaidBlocked (player))
                StopBlocking<RaidBlock> (player);
        }

        void StopRaidBlocking (string target)
        {
            var player = BasePlayer.Find (target);
            StopRaidBlocking (player);
        }

        void StopCombatBlocking (BasePlayer player)
        {
            if (player is BasePlayer && IsRaidBlocked (player))
                StopBlocking<CombatBlock> (player);
        }

        void StopCombatBlocking (string target)
        {
            var player = BasePlayer.Find (target);
            StopCombatBlocking (player);
        }

        void ClearCombatBlocking (string target)
        {
            StopCombatBlocking (target);
        }

        #endregion

        #region Zone Handling

        void EraseZone (string zoneid)
        {
            ZoneManager.CallHook ("EraseZone", zoneid);
            zones.Remove (zoneid);
        }

        void ResetZoneTimer (RaidZone zone)
        {
            zone.ResetTimer ().timer = timer.In (raidDuration, delegate () {
                EraseZone (zone.zoneid);
            });
        }

        void CreateRaidZone (Vector3 position)
        {
            var zoneid = position.ToString ();

            RaidZone zone;
            if (zones.TryGetValue (zoneid, out zone)) {
                ResetZoneTimer (zone);
                return;
            }

            foreach (var nearbyZone in zones) {
                if (nearbyZone.Value.Distance (position) < (raidDistance / 2)) {
                    ResetZoneTimer (nearbyZone.Value);
                    return;
                }
            }

            ZoneManager.CallHook ("CreateOrUpdateZone", zoneid, new string []
                {
                    "radius",
                    raidDistance.ToString()
                }, position);

            zones.Add (zoneid, zone = new RaidZone (zoneid, position));

            ResetZoneTimer (zone);
        }

        [HookMethod ("OnEnterZone")]
        void OnEnterZone (string zoneid, BasePlayer player)
        {
            if (!zoneEnter)
                return;
            if (!zones.ContainsKey (zoneid))
                return;

            StartRaidBlocking (player, player.transform.position, false);
        }

        [HookMethod ("OnExitZone")]
        void OnExitZone (string zoneid, BasePlayer player)
        {
            if (!zoneLeave)
                return;
            if (!zones.ContainsKey (zoneid))
                return;

            if (IsRaidBlocked (player)) {
                StopBlocking<RaidBlock> (player);
            }
        }

        #endregion

        #region Friend/Clan Integration

        public List<string> getFriends (string player)
        {
            var players = new List<string> ();
            if (player == null)
                return players;

            if (friendShare || friendCheck) {
                var friendList = getFriendList (player);
                if (friendList != null)
                    players.AddRange (friendList);
            }

            if (clanShare || clanCheck) {
                var members = getClanMembers (player);
                if (members != null)
                    players.AddRange (members);
            }
            return players;
        }

        public List<string> getFriendList (string player)
        {
            object friends_obj = null;
            DateTime lastFriendCheckPlayer;
            var players = new List<string> ();

            if (lastFriendCheck.TryGetValue (player, out lastFriendCheckPlayer)) {
                if ((DateTime.Now - lastFriendCheckPlayer).TotalMinutes <= cacheTimer && friendCache.TryGetValue (player, out players)) {
                    return players;
                } else {
                    friends_obj = Friends?.CallHook ("IsFriendOfS", player);
                    lastFriendCheck [player] = DateTime.Now;
                }
            } else {
                friends_obj = Friends?.CallHook ("IsFriendOfS", player);
                lastFriendCheck.Add (player, DateTime.Now);
            }

            if (friends_obj == null)
                return players;

            string [] friends = friends_obj as string [];

            foreach (string fid in friends)
                players.Add (fid);

            if (friendCache.ContainsKey (player))
                friendCache [player] = players;
            else
                friendCache.Add (player, players);

            return players;
        }

        public List<string> getClanMembers (string player)
        {
            string tag = null;
            DateTime lastClanCheckPlayer;
            string lastClanCached;
            if (lastClanCheck.TryGetValue (player, out lastClanCheckPlayer) && clanCache.TryGetValue (player, out lastClanCached)) {
                if ((DateTime.Now - lastClanCheckPlayer).TotalMinutes <= cacheTimer)
                    tag = lastClanCached;
                else {
                    tag = Clans.Call<string> ("GetClanOf", player);
                    clanCache [player] = tag;
                    lastClanCheck [player] = DateTime.Now;
                }
            } else {
                tag = Clans.Call<string> ("GetClanOf", player);
                if (lastClanCheck.ContainsKey (player))
                    lastClanCheck.Remove (player);

                if (clanCache.ContainsKey (player))
                    clanCache.Remove (player);

                clanCache.Add (player, tag);
                lastClanCheck.Add (player, DateTime.Now);
            }

            if (tag == null)
                return null;

            List<string> lastMemberCache;
            if (memberCache.TryGetValue (tag, out lastMemberCache))
                return lastMemberCache;

            var clan = GetClan (tag);

            if (clan == null)
                return null;

            return CacheClan (clan);
        }

        JObject GetClan (string tag)
        {
            if (string.IsNullOrEmpty (tag)) {
                return null;
            }
            return Clans.Call<JObject> ("GetClan", tag);
        }

        List<string> CacheClan (JObject clan)
        {
            string tag = clan ["tag"].ToString ();
            List<string> players = new List<string> ();
            foreach (string memberid in clan ["members"]) {
                if (clanCache.ContainsKey (memberid))
                    clanCache [memberid] = tag;
                else
                    clanCache.Add (memberid, tag);

                players.Add (memberid);
            }

            if (memberCache.ContainsKey (tag))
                memberCache [tag] = players;
            else
                memberCache.Add (tag, players);

            if (lastCheck.ContainsKey (tag))
                lastCheck [tag] = DateTime.Now;
            else
                lastCheck.Add (tag, DateTime.Now);

            return players;
        }

        [HookMethod ("OnClanCreate")]
        void OnClanCreate (string tag)
        {
            var clan = GetClan (tag);
            if (clan != null) {
                CacheClan (clan);
            } else {
                PrintWarning ("Unable to find clan after creation: " + tag);
            }
        }

        [HookMethod ("OnClanUpdate")]
        void OnClanUpdate (string tag)
        {
            var clan = GetClan (tag);
            if (clan != null) {
                CacheClan (clan);
            } else {
                PrintWarning ("Unable to find clan after update: " + tag);
            }
        }

        [HookMethod ("OnClanDestroy")]
        void OnClanDestroy (string tag)
        {
            if (lastCheck.ContainsKey (tag)) {
                lastCheck.Remove (tag);
            }

            if (memberCache.ContainsKey (tag)) {
                memberCache.Remove (tag);
            }
        }

        #endregion

        #region Permission Checking & External API Handling

        bool HasPerm (string userid, string perm)
        {
            return permission.UserHasPermission (userid, "noescape." + perm);
        }

        bool CanRaidCommand (BasePlayer player, string command)
        {
            return raidBlock && HasPerm (player.UserIDString, "raid." + command + "block") && IsRaidBlocked (player);
        }

        bool CanRaidCommand (string playerID, string command)
        {
            return raidBlock && HasPerm (playerID, "raid." + command + "block") && IsRaidBlocked (playerID);
        }

        bool CanCombatCommand (BasePlayer player, string command)
        {
            return combatBlock && HasPerm (player.UserIDString, "combat." + command + "block") && IsCombatBlocked (player);
        }

        bool CanCombatCommand (string playerID, string command)
        {
            return combatBlock && HasPerm (playerID, "combat." + command + "block") && IsCombatBlocked (playerID);
        }

        object CanDo (string command, BasePlayer player)
        {
            if (CanRaidCommand (player, command))
                return GetMessage<RaidBlock> (player, "Raid Blocked Message", raidDuration);
            else if (CanCombatCommand (player, command))
                return GetMessage<CombatBlock> (player, "Combat Blocked Message", combatDuration);

            return null;
        }

        object OnStructureRepair (BaseCombatEntity entity, BasePlayer player)
        {
            var result = CanDo ("repair", player);
            if (result is string) {
                if (entity.health < entity.MaxHealth ()) {
                    return null;
                }
                SendReply (player, result.ToString ());
                return true;
            }

            return null;
        }

        object OnStructureUpgrade (BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade)
        {
            var result = CanDo ("upgrade", player);
            if (result is string) {
                SendReply (player, result.ToString ());
                return true;
            }

            return null;
        }

        object canRedeemKit (BasePlayer player)
        {
            return CanDo ("kit", player);
        }

        object CanUseMailbox (BasePlayer player, Mailbox mailbox)
        {
            var result = CanDo ("mailbox", player);
            if (result is string) {
                SendReply (player, result.ToString ());
                return false;
            }

            return null;
        }

        object CanUseVending (VendingMachine machine, BasePlayer player)
        {
            var result = CanDo ("vend", player);
            if (result is string) {
                SendReply (player, result.ToString ());
                return true;
            }

            return null;
        }

        object CanBuild (Planner plan, Construction prefab)
        {
            var player = plan.GetOwnerPlayer ();
            var result = CanDo ("build", player);
            if (result is string) {
                if (isEntityException (prefab.fullName)) {
                    return null;
                }

                SendReply (player, result.ToString ());
                return true;
            }

            return null;
        }

        object CanAssignBed (SleepingBag bag, BasePlayer player, ulong targetPlayerId)
        {
            var result = CanDo ("assignbed", player);
            if (result is string) {
                SendReply (player, result.ToString ());
                return true;
            }

            return null;
        }

        object CanBank (BasePlayer player)
        {
            return CanDo ("bank", player);
        }

        object CanTrade (BasePlayer player)
        {
            return CanDo ("trade", player);
        }

        object canRemove (BasePlayer player)
        {
            return CanDo ("remove", player);
        }

        object canShop (BasePlayer player)
        {
            return CanDo ("shop", player);
        }

        object CanShop (BasePlayer player)
        {
            return CanDo ("shop", player);
        }

        object CanTeleport (BasePlayer player)
        {
            return CanDo ("tp", player);
        }

        object canTeleport (BasePlayer player) // ALIAS FOR MagicTeleportation
        {
            return CanTeleport (player);
        }

        object CanGridTeleport (BasePlayer player) // ALIAS FOR GrTeleport
        {
            return CanTeleport (player);
        }

        object CanCraft (ItemCrafter itemCrafter, ItemBlueprint bp, int amount)
        {
            BasePlayer player = itemCrafter.containers [0].GetOwnerPlayer ();

            if (player != null) {
                var result = CanDo ("craft", player);
                if (result is string) {
                    SendReply (player, result.ToString ());
                    return false;
                }
            }

            return null;
        }

        object CanRecycleCommand (BasePlayer player)
        {
            return CanDo ("recycle", player);
        }

        object CanBGrade (BasePlayer player, int grade, BuildingBlock buildingBlock, Planner planner)
        {
            if (CanRaidCommand (player, "bgrade") || CanCombatCommand (player, "bgrade"))
                return -1;
            return null;
        }

        #endregion

        #region Messages

        void SendBlockMessage (BasePlayer target, BlockBehavior blocker, string langMessage, string completeMessage)
        {
            var send = false;
            if (blocker.lastNotification != DateTime.MinValue) {
                TimeSpan diff = DateTime.Now - blocker.lastNotification;
                if (diff.TotalSeconds >= (blocker.Duration / 2))
                    send = true;
            } else
                send = true;

            if (send) {
                string message = string.Empty;

                if (sendChatNotification || sendGUIAnnouncementsNotification)
                    message = GetPrefix (target.UserIDString) + GetMsg (langMessage, target.UserIDString).Replace ("{time}", GetCooldownTime (blocker.Duration, target.UserIDString));

                if (sendChatNotification)
                    SendReply (target, message);

                if (sendGUIAnnouncementsNotification)
                    GUIAnnouncements?.Call ("CreateAnnouncement", message, GUIAnnouncementTintColor, GUIAnnouncementTextColor, target);

                if (sendLustyMapNotification && LustyMapDuration > 0) {
                    blocker.iconUID = Guid.NewGuid ().ToString ("N");
                    var obj = LustyMap?.Call ("AddMarker", target.transform.position.x, target.transform.position.z, blocker.iconUID, LustyMapIcon);
                    if (obj is bool && (bool)obj == true) {
                        timer.In (LustyMapDuration, delegate () {
                            LustyMap?.Call ("RemoveMarker", blocker.iconUID);
                        });
                    }
                }

                blocker.lastNotification = DateTime.Now;
            }

            blocker.Notify (delegate () {
                blocker.notifyCallback = null;
                if (target?.IsConnected == true) {
                    string message = string.Empty;

                    if (sendChatNotification || sendGUIAnnouncementsNotification)
                        message = GetPrefix (target.UserIDString) + GetMsg (completeMessage, target.UserIDString);

                    if (sendChatNotification)
                        SendReply (target, message);

                    if (sendGUIAnnouncementsNotification)
                        GUIAnnouncements?.Call ("CreateAnnouncement", message, GUIAnnouncementTintColor, GUIAnnouncementTextColor, target);

                    if (sendLustyMapNotification && LustyMapDuration > 0)
                        LustyMap?.Call ("RemoveMarker", blocker.iconUID);
                }
            });
        }

        string GetCooldownTime (float f, string userID)
        {
            if (f > 60)
                return Math.Round (f / 60, 1) + " " + GetMsg ("Unit Minutes", userID);

            return f + " " + GetMsg ("Unit Seconds", userID);
        }

        public string GetMessage (BasePlayer player)
        {
            if (IsRaidBlocked (player))
                return GetMessage<RaidBlock> (player, "Raid Blocked Message", raidDuration);
            else if (IsCombatBlocked (player))
                return GetMessage<CombatBlock> (player, "Combat Blocked Message", combatDuration);

            return null;
        }

        public string GetPrefix (string player)
        {
            string prefix = GetMsg ("Prefix", player);
            if (!string.IsNullOrEmpty (prefix)) {
                return prefix + ": ";
            }

            return string.Empty;
        }

        public string GetMessage<T> (BasePlayer player, string blockMsg, float duration) where T : BlockBehavior
        {
            T blocker;
            if (duration > 0 && TryGetBlocker<T> (player, out blocker)) {
                var ts = DateTime.Now - blocker.lastBlock;
                var unblocked = Math.Round ((duration / 60) - Convert.ToSingle (ts.TotalMinutes), 2);

                if (ts.TotalMinutes <= duration) {
                    if (unblocked < 1) {
                        var timelefts = Math.Round (Convert.ToDouble (duration) - ts.TotalSeconds);
                        return GetPrefix (player.UserIDString) + GetMsg (blockMsg, player).Replace ("{time}", timelefts.ToString () + " " + GetMsg ("Unit Seconds", player));
                    }

                    return GetPrefix (player.UserIDString) + GetMsg (blockMsg, player).Replace ("{time}", unblocked.ToString () + " " + GetMsg ("Unit Minutes", player));
                }
            }

            return null;
        }

        #endregion

        #region Utility Methods

        bool TryGetBlocker<T> (BasePlayer player, out T blocker) where T : BlockBehavior
        {
            blocker = null;
            if (player.gameObject == null)
                return false;
            if ((blocker = player.gameObject.GetComponent<T> ()) != null)
                return true;

            return false;
        }

        public bool isEntityException (string prefabName)
        {
            var result = false;

            foreach (string p in exceptionPrefabs) {
                if (prefabName.IndexOf (p) != -1) {
                    result = true;
                    break;
                }
            }

            return result;
        }

        public bool IsEntityBlocked (BaseCombatEntity entity)
        {
            if (entity is BuildingBlock) {
                if (((BuildingBlock)entity).grade == BuildingGrade.Enum.Twigs)
                    return false;

                return true;
            }

            var prefabName = entity.ShortPrefabName;
            var result = false;
            if (prefabBlockCache.TryGetValue (prefabName, out result))
                return result;

            result = false;

            foreach (string p in blockedPrefabs) {
                if (prefabName.IndexOf (p) != -1) {
                    result = true;
                    break;
                }
            }


            prefabBlockCache.Add (prefabName, result);
            return result;
        }

        bool IsRaidDamage (DamageType dt)
        {
            return raidDamageTypes.Contains (dt.ToString ());
        }

        bool IsRaidDamage (DamageTypeList dtList)
        {
            for (int index = 0; index < dtList.types.Length; ++index) {
                if (dtList.types [index] > 0 && IsRaidDamage ((DamageType)index)) {
                    return true;
                }
            }

            return false;
        }

        bool IsExcludedWeapon (string name)
        {
            if (string.IsNullOrEmpty (name)) {
                return false;
            }

            bool cachedValue;

            if (_cachedExcludedWeapons.TryGetValue (name, out cachedValue)) {
                return cachedValue;
            }

            foreach (var weaponName in exceptionWeapons) {
                if (name.Contains (weaponName)) {
                    _cachedExcludedWeapons.Add (name, true);
                    return true;
                }
            }

            _cachedExcludedWeapons.Add (name, false);
            return false;
        }

        bool IsCombatDamage (DamageType dt)
        {
            return combatDamageTypes.Contains (dt.ToString ());
        }

        bool IsCombatDamage (DamageTypeList dtList)
        {
            for (int index = 0; index < dtList.types.Length; ++index) {
                if (dtList.types [index] > 0 && IsCombatDamage ((DamageType)index)) {
                    return true;
                }
            }

            return false;
        }

        T GetConfig<T> (string name, string name2, string name3, T defaultValue)
        {
            try {
                var val = Config [name, name2, name3];

                return ParseValue<T> (val, defaultValue);
            } catch (Exception ex) {
                //PrintWarning ("Invalid config value: " + name + "/" + name2 + "/" + name3 + " (" + ex.Message + ")");
                Config [name, name2, name3] = defaultValue;
                Config.Save ();
                return defaultValue;
            }
        }

        T GetConfig<T> (string name, string name2, T defaultValue)
        {
            try {
                var val = Config [name, name2];

                return ParseValue<T> (val, defaultValue);
            } catch (Exception ex) {
                //PrintWarning ("Invalid config value: " + name + "/" + name2 + " (" + ex.Message + ")");
                Config [name, name2] = defaultValue;
                Config.Save ();
                return defaultValue;
            }
        }

        T GetConfig<T> (string name, T defaultValue)
        {
            try {
                var val = Config [name];

                return ParseValue<T> (val, defaultValue);
            } catch (Exception ex) {
                //PrintWarning ("Invalid config value: " + name + " (" + ex.Message + ")");
                Config [name] = defaultValue;
                Config.Save ();
                return defaultValue;
            }
        }

        T ParseValue<T> (object val, T defaultValue)
        {
            if (val == null)
                return defaultValue;

            if (val is List<object>) {
                var t = typeof (T).GetGenericArguments () [0];
                if (t == typeof (String)) {
                    var cval = new List<string> ();
                    foreach (var v in val as List<object>)
                        cval.Add ((string)v);
                    val = cval;
                } else if (t == typeof (int)) {
                    var cval = new List<int> ();
                    foreach (var v in val as List<object>)
                        cval.Add (Convert.ToInt32 (v));
                    val = cval;
                }
            } else if (val is Dictionary<string, object>) {
                var t = typeof (T).GetGenericArguments () [1];
                if (t == typeof (int)) {
                    var cval = new Dictionary<string, int> ();
                    foreach (var v in val as Dictionary<string, object>)
                        cval.Add (Convert.ToString (v.Key), Convert.ToInt32 (v.Value));
                    val = cval;
                }
            }

            return (T)Convert.ChangeType (val, typeof (T));
        }

        static string GetMsg (string key, object user = null)
        {
            if (user is BasePlayer) {
                user = ((BasePlayer)user).UserIDString;
            }
            return plugin.lang.GetMessage (key, plugin, user == null ? null : user.ToString ());
        }

        #endregion
    }
}