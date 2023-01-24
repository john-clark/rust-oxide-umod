using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using Oxide.Core;
using UnityEngine;
using System.Reflection;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries;
using Rust;
using Facepunch;
using System.Linq;
using Oxide.Core.Libraries.Covalence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info ("AntiOfflineRaid", "Calytic", "0.3.6")]
    [Description ("Prevents/reduces offline raiding")]
    public class AntiOfflineRaid : RustPlugin
    {
        #region Variables

        [PluginReference]
        Plugin Clans;

        float tickRate = 5f;

        Dictionary<ulong, LastOnline> lastOnline = new Dictionary<ulong, LastOnline> ();
        Dictionary<string, object> damageScale = new Dictionary<string, object> ();
        Dictionary<string, object> absoluteDamageScale = new Dictionary<string, object> ();

        internal static int cooldownMinutes;
        float interimDamage;
        static int afkMinutes;
        bool showMessage;
        bool playSound;
        string sound;

        bool clanShare;
        bool clanFirstOffline;
        int minMembers;

        Timer lastOnlineTimer;

        List<object> prefabs;

        Dictionary<string, List<string>> memberCache = new Dictionary<string, List<string>> ();

        #endregion

        #region Class

        class LastOnline
        {
            public ulong userid;
            public long lastOnlineLong;

            [JsonIgnore]
            public Vector3 lastPosition = default (Vector3);

            [JsonIgnore]
            public float afkMinutes = 0;

            [JsonIgnore]
            public DateTime lastOnline {
                get {
                    return DateTime.FromBinary (lastOnlineLong);
                }

                set {
                    lastOnlineLong = value.ToBinary ();
                }
            }

            [JsonConstructor]
            public LastOnline (ulong userid, long lastOnlineLong)
            {
                this.userid = userid;
                this.lastOnlineLong = lastOnlineLong;
            }

            public LastOnline (BasePlayer player, DateTime lastOnline)
            {
                this.userid = player.userID;
                this.lastOnline = lastOnline;
            }

            [JsonIgnore]
            public BasePlayer player {
                get {
                    return BasePlayer.FindByID (userid);
                }
            }

            public bool IsConnected ()
            {
                var player = this.player;
                if (player != null && player.IsConnected)
                    return true;

                return false;
            }

            public bool IsOffline ()
            {
                return Minutes >= cooldownMinutes;
            }

            [JsonIgnore]
            public double Days {
                get {
                    var ts = DateTime.Now - lastOnline;
                    return ts.TotalDays;
                }
            }

            [JsonIgnore]
            public double Minutes {
                get {
                    var ts = DateTime.Now - lastOnline;
                    return ts.TotalMinutes;
                }
            }

            [JsonIgnore]
            public double Hours {
                get {
                    var ts = DateTime.Now - lastOnline;
                    return ts.TotalHours;
                }
            }

            public bool IsAFK ()
            {
                if (afkMinutes >= AntiOfflineRaid.afkMinutes)
                    return true;

                return false;
            }

            public bool HasMoved (Vector3 position)
            {
                var equal = true;

                if (lastPosition.Equals (position))
                    equal = false;

                lastPosition = new Vector3 (position.x, position.y, position.z);

                return equal;
            }
        }

        #endregion

        #region Initialization & Configuration

        void OnServerInitialized ()
        {
            permission.RegisterPermission ("antiofflineraid.protect", this);
            permission.RegisterPermission ("antiofflineraid.check", this);

            LoadMessages ();
            LoadData ();

            damageScale = GetConfig ("damageScale", GetDefaultScales ());
            absoluteDamageScale = GetConfig ("absoluteTimeScale", GetDefaultAbsoluteDamageScales ());
            prefabs = GetConfig ("prefabs", GetDefaultPrefabs ());
            afkMinutes = GetConfig ("afkMinutes", 5);
            cooldownMinutes = GetConfig ("cooldownMinutes", 10);
            interimDamage = GetConfig ("interimDamage", 0f);

            clanShare = GetConfig ("clanShare", false);
            clanFirstOffline = GetConfig ("clanFirstOffline", false);
            minMembers = GetConfig ("minMembers", 1);
            showMessage = GetConfig ("showMessage", true);
            playSound = GetConfig ("playSound", false);
            sound = GetConfig ("sound", "assets/prefabs/weapon mods/silencers/effects/silencer_attach.fx.prefab");

            if (clanShare) {
                if (!plugins.Exists ("Clans")) {
                    clanShare = false;
                    PrintWarning ("Clans not found! clanShare disabled. Cannot use clanShare without a clans plugin.");
                }
            }

            var wakeupTriggers = GetConfig ("activity", GetDefaultActivityTriggers ());
            if (wakeupTriggers.Count == 0) {
                PrintWarning ("At least one activity trigger must be configured for this plugin to operate.");
                ToggleHooks (new List<object> ()); // Unsub all hooks
            } else {
                ToggleHooks (wakeupTriggers); // Sub/unsub configured hooks

                UpdateLastOnlineAll (false);
                lastOnlineTimer = timer.Repeat (tickRate * 60, 0, delegate () {
                    UpdateLastOnlineAll ();
                });
            }
        }

        protected void ToggleHooks (List<object> wakeupTriggers)
        {
            var defaultWakeupTriggers = GetDefaultActivityTriggers ();

            foreach (string trigger in defaultWakeupTriggers) {
                switch (trigger) {
                case "input":
                    ToggleHook (wakeupTriggers, trigger, "OnPlayerInput");
                    break;
                case "loot":
                    ToggleHook (wakeupTriggers, trigger, "OnLootPlayer");
                    ToggleHook (wakeupTriggers, trigger, "OnLootItem");
                    break;
                case "respawn":
                    ToggleHook (wakeupTriggers, trigger, "OnPlayerRespawn");
                    break;
                case "chat":
                    ToggleHook (wakeupTriggers, trigger, "OnPlayerChat");
                    break;
                case "wakeup":
                    ToggleHook (wakeupTriggers, trigger, "OnPlayerSleepEnded");
                    break;
                }
            }
        }

        protected void ToggleHook (List<object> wakeupTriggers, string trigger, string hook)
        {
            if (wakeupTriggers.Contains (trigger)) {
                Subscribe (hook);
            } else {
                Unsubscribe (hook);
            }
        }

        protected Dictionary<string, object> GetDefaultScales ()
        {
            return new Dictionary<string, object> ()
            {
                { "1", 0.2 },
                { "3", 0.35f },
                { "6", 0.5f },
                { "12", 0.8f },
                { "48", 1 }
            };
        }

        protected Dictionary<string, object> GetDefaultAbsoluteDamageScales ()
        {
            return new Dictionary<string, object> ()
            {
                { "03", 0.1 },
            };
        }

        List<object> GetDefaultPrefabs ()
        {
            return new List<object> ()
            {
                "door.hinged",
                "door.double.hinged",
                "window.bars",
                "floor.ladder.hatch",
                "floor.frame",
                "wall.frame",
                "shutter",
                "wall.external",
                "gates.external",
                "box",
                "locker"
            };
        }

        List<object> GetDefaultActivityTriggers ()
        {
            return new List<object> ()
            {
                "input",
                "loot",
                "respawn",
                "chat",
                "wakeup"
            };
        }

        void LoadMessages ()
        {
            lang.RegisterMessages (new Dictionary<string, string>
                {
                    { "Protection Message", "This building is protected: {amount}%" },
                    { "Denied: Permission", "You lack permission to do that" }
                }, this);
        }

        protected override void LoadDefaultConfig ()
        {
            PrintToConsole ("Creating new configuration");
            Config.Clear ();

            Config ["damageScale"] = GetDefaultScales ();
            Config ["absoluteTimeScale"] = GetDefaultAbsoluteDamageScales ();
            Config ["afkMinutes"] = 5;
            Config ["cooldownMinutes"] = 10;
            Config ["interimDamage"] = 0f;
            Config ["minMembers"] = 1;
            Config ["clanShare"] = false;
            Config ["clanFirstOffline"] = false;
            Config ["showMessage"] = true;
            Config ["playSound"] = false;
            Config ["prefabs"] = GetDefaultPrefabs ();
            Config ["activity"] = GetDefaultActivityTriggers ();
            Config ["sound"] = "assets/prefabs/weapon mods/silencers/effects/silencer_attach.fx.prefab";
            Config ["VERSION"] = Version.ToString ();
        }

        protected void ReloadConfig ()
        {
            Config ["VERSION"] = Version.ToString ();

            // NEW CONFIGURATION OPTIONS HERE
            Config ["activity"] = GetConfig ("activity", GetDefaultActivityTriggers ());
            // END NEW CONFIGURATION OPTIONS

            PrintWarning ("Upgrading configuration file");
            SaveConfig ();
        }

        void OnServerSave ()
        {
            SaveData ();
        }

        void OnServerShutdown ()
        {
            UpdateLastOnlineAll ();
            SaveData ();
        }

        //void Unload()
        //{
        //    SaveData();
        //}

        void LoadData ()
        {
            lastOnline = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, LastOnline>> ("antiofflineraid");

            if (Config ["VERSION"] == null) {
                // FOR COMPATIBILITY WITH INITIAL VERSIONS WITHOUT VERSIONED CONFIG
                ReloadConfig ();
            } else if (GetConfig ("VERSION", "") != Version.ToString ()) {
                // ADDS NEW, IF ANY, CONFIGURATION OPTIONS
                ReloadConfig ();
            }
        }

        void SaveData ()
        {
            Interface.Oxide.DataFileSystem.WriteObject<Dictionary<ulong, LastOnline>> ("antiofflineraid", lastOnline);
        }

        #endregion

        #region Oxide hooks

        void OnPlayerDisconnected (BasePlayer player)
        {
            UpdateLastOnline (player);
        }

        void OnLootPlayer (BasePlayer player, BasePlayer target)
        {
            UpdateLastOnline (player, true);
        }

        void OnLootItem (BasePlayer player, Item item)
        {
            UpdateLastOnline (player, true);
        }

        void OnPlayerInit (BasePlayer player)
        {
            UpdateLastOnline (player, true);
        }

        void OnPlayerSleepEnded (BasePlayer player)
        {
            UpdateLastOnline (player, true);
        }

        void OnPlayerRespawn (BasePlayer player)
        {
            UpdateLastOnline (player, true);
        }

        void OnPlayerChat (ConsoleSystem.Arg args)
        {
            UpdateLastOnline (args.Player (), true);
        }

        void OnPlayerInput (BasePlayer player, InputState input)
        {
            if (player == null || input == null)
                return;
            if (input.current == null)
                return;
            if (input.previous == null)
                return;

            LastOnline lastOnlinePlayer = null;
            if (lastOnline.TryGetValue (player.userID, out lastOnlinePlayer) && input.current.buttons != 0 && !input.previous.Equals (input.current)) {
                lastOnlinePlayer.afkMinutes = 0;
            }
        }

        object OnEntityTakeDamage (BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (hitInfo == null)
                return null;
            if (entity == null)
                return null;

            if (IsBlocked (entity))
                return OnStructureAttack (entity, hitInfo);

            return null;
        }

        #endregion

        #region Core Methods

        void UpdateLastOnlineAll (bool afkCheck = true)
        {
            foreach (var player in BasePlayer.activePlayerList) {
                if (!player.IsConnected)
                    continue;

                var hasMoved = true;

                if (afkCheck && afkMinutes >= 5) {
                    LastOnline lastOnlinePlayer;
                    if (lastOnline.TryGetValue (player.userID, out lastOnlinePlayer)) {
                        if (!lastOnlinePlayer.HasMoved (player.transform.position)) {
                            hasMoved = false;
                            lastOnlinePlayer.afkMinutes += tickRate;
                        }

                        if (lastOnlinePlayer.IsAFK ()) {
                            continue;
                        }
                    }
                }

                UpdateLastOnline (player, hasMoved);
            }
        }

        void UpdateLastOnline (BasePlayer player, bool hasMoved = true)
        {
            if (player == null) {
                return;
            }
            LastOnline lastOnlinePlayer;
            if (!lastOnline.TryGetValue (player.userID, out lastOnlinePlayer)) {
                lastOnline.Add (player.userID, new LastOnline (player, DateTime.Now));
            } else {
                lastOnlinePlayer.lastOnline = DateTime.Now;
                if (hasMoved)
                    lastOnlinePlayer.afkMinutes = 0;
            }
        }

        object OnStructureAttack (BaseEntity entity, HitInfo hitinfo)
        {
            ulong targetID = 0;

            targetID = entity.OwnerID;
            if (targetID.IsSteamId () && HasPerm (targetID.ToString (), "antiofflineraid.protect") && lastOnline.ContainsKey (targetID)) {
                float scale = scaleDamageCached (targetID);
                if (clanShare) {
                    if (IsClanOffline (targetID))
                        return mitigateDamage (hitinfo, scale);
                } else
                    return mitigateDamage (hitinfo, scale);
            }

            return null;
        }

        public object mitigateDamage (HitInfo hitinfo, float scale)
        {
            if (scale > -1 && scale != 1) {
                var isFire = hitinfo.damageTypes.GetMajorityDamageType () == DamageType.Heat;

                if (scale == 0) {
                    // completely cancel damage
                    //hitinfo.damageTypes = new DamageTypeList();
                    //hitinfo.DoHitEffects = false;
                    //hitinfo.HitMaterial = 0;
                    if (showMessage && ((isFire && hitinfo.WeaponPrefab != null) || (!isFire)))
                        sendMessage (hitinfo);

                    if (playSound && hitinfo.Initiator is BasePlayer && !isFire)
                        Effect.server.Run (sound, hitinfo.Initiator.transform.position);

                    return false;
                } else {
                    // only scale damage
                    hitinfo.damageTypes.ScaleAll (scale);
                    if (scale < 1) {
                        if (showMessage && ((isFire && hitinfo.WeaponPrefab != null) || (!isFire)))
                            sendMessage (hitinfo, 100 - Convert.ToInt32 (scale * 100));

                        if (playSound && hitinfo.Initiator is BasePlayer && !isFire)
                            Effect.server.Run (sound, hitinfo.Initiator.transform.position);
                    }
                }
            }

            return null;
        }

        void sendMessage (HitInfo hitinfo, int amt = 100)
        {
            if (hitinfo.Initiator is BasePlayer)
                ShowMessage ((BasePlayer)hitinfo.Initiator, amt);
        }

        class ScaleCacheItem
        {
            public DateTime expires;
            public float scale;

            public ScaleCacheItem (DateTime expires, float scale)
            {
                this.expires = expires;
                this.scale = scale;
            }
        }

        Dictionary<ulong, ScaleCacheItem> cachedScales = new Dictionary<ulong, ScaleCacheItem> ();

        float scaleDamageCached (ulong targetID)
        {
            ScaleCacheItem kvp = null;
            if (cachedScales.TryGetValue (targetID, out kvp) && DateTime.Now < kvp.expires) {
                return kvp.scale;
            }

            return cacheDamageScale (targetID, scaleDamage (targetID), kvp);
        }

        float cacheDamageScale (ulong targetID, float scale, ScaleCacheItem kvp = null)
        {
            if (kvp == null) {
                kvp = new ScaleCacheItem (DateTime.Now.AddMinutes (1), scale);
                cachedScales.Add (targetID, kvp);
            } else {
                kvp.expires = DateTime.Now.AddMinutes (1);
                kvp.scale = scale;
            }

            return scale;
        }

        public float scaleDamage (ulong targetID)
        {
            float scale = -1;

            var lastOffline = targetID;
            if (clanShare && Clans != null) {
                var tag = Clans.Call<string> ("GetClanOf", targetID);
                if (!string.IsNullOrEmpty (tag)) {
                    lastOffline = getClanOffline (tag);
                }
            }

            LastOnline lastOnlinePlayer;
            if (!lastOnline.TryGetValue (lastOffline, out lastOnlinePlayer)) {
                // player is not known to this plugin (yet)
                return -1;
            }

            if (!lastOnlinePlayer.IsOffline ()) {
                // must be logged out for atleast x minutes before damage is reduced
                return -1;
            }

            if (lastOnlinePlayer.Minutes >= 60) {
                // if absolute scale is configured, override relative scaling
                if (absoluteDamageScale.Count > 0) {
                    var hour = DateTime.Now.ToString ("HH", System.Globalization.DateTimeFormatInfo.InvariantInfo);
                    object scaleObj;
                    if (absoluteDamageScale.TryGetValue (hour, out scaleObj)) {
                        return Convert.ToSingle (scaleObj);
                    }
                }

                // if you've been offline/afk for more than an hour, use hourly scales
                var keys = damageScale.Keys.Select (int.Parse).ToList ();
                keys.Sort ();

                foreach (int key in keys) {
                    if (lastOnlinePlayer.Hours >= key) {
                        scale = Convert.ToSingle (damageScale [key.ToString ()]);
                    }
                }
            } else {
                // if you have been offline for more than x minutes but less than an hour, use interimDamage
                scale = interimDamage;
            }


            return scale;
        }

        public Dictionary<string, bool> blockCache = new Dictionary<string, bool> ();

        public bool IsBlocked (BaseCombatEntity entity)
        {
            if (entity is BuildingBlock)
                return true;

            var result = false;

            if (!result && !string.IsNullOrEmpty (entity.ShortPrefabName)) {
                var prefabName = entity.ShortPrefabName;

                if (blockCache.TryGetValue (prefabName, out result))
                    return result;

                foreach (string p in prefabs)
                    if (prefabName.IndexOf (p) != -1) {
                        result = true;
                        break;
                    }

                if (!blockCache.ContainsKey (prefabName))
                    blockCache.Add (prefabName, result);
            }


            return result;
        }

        public bool IsOffline (ulong playerID)
        {
            LastOnline lastOnlinePlayer;
            if (lastOnline.TryGetValue (playerID, out lastOnlinePlayer)) {
                return lastOnlinePlayer.IsOffline ();
            }

            var player = BasePlayer.FindByID (playerID);
            if (player == null) {
                return true;
            }

            if (player.IsConnected) {
                return false;
            }

            return true;
        }

        string SendStatus (Network.Connection connection, string [] args)
        {
            if (args.Length == 1) {
                var target = FindPlayerByPartialName (args [0]);
                ulong userID;
                LastOnline lo;
                if (target is IPlayer && ulong.TryParse (target.Id, out userID) && lastOnline.TryGetValue (userID, out lo)) {
                    StringBuilder sb = new StringBuilder ();

                    if (IsOffline (userID)) {
                        sb.AppendLine ("<color=red><size=15>AntiOfflineRaid Status</size></color>: " + target.Name);
                        if (target.IsConnected) {
                            sb.AppendLine ("<color=lightblue>Player Status</color>: <color=orange>AFK</color>: " + lo.lastOnline.ToString ());
                        } else {
                            sb.AppendLine ("<color=lightblue>Player Status</color>: <color=red>Offline</color>: " + lo.lastOnline.ToString ());
                        }
                    } else {
                        sb.AppendLine ("<color=lime><size=15>AntiOfflineRaid Status</size></color>: " + target.Name);
                        sb.AppendLine ("<color=lightblue>Player Status</color>: <color=lime>Online</color>");
                    }
                    sb.AppendLine ("<color=lightblue>AFK</color>: " + lo.afkMinutes + " minutes");
                    if (clanShare) {
                        sb.AppendLine ("<color=lightblue>Clan Status</color>: " + (IsClanOffline (userID) ? "<color=red>Offline</color>" : "<color=lime>Online</color>") + " (" + getClanMembersOnline (userID) + ")");
                        var tag = Clans.Call<string> ("GetClanOf", userID);
                        if (!string.IsNullOrEmpty (tag)) {
                            ulong lastOffline = 0;
                            string msg = "";
                            lastOffline = getClanOffline (tag);
                            if (clanFirstOffline) {
                                msg = "First Offline";
                            } else {
                                msg = "Last Offline";
                            }

                            LastOnline lastOfflinePlayer;

                            if (lastOnline.TryGetValue (lastOffline, out lastOfflinePlayer)) {
                                DateTime lastOfflineTime = lastOfflinePlayer.lastOnline;
                                IPlayer p = covalence.Players.FindPlayerById (lastOffline.ToString ());
                                sb.AppendLine ("<color=lightblue>Clan " + msg + "</color>: " + p.Name + " - " + lastOfflineTime.ToString ());
                            }
                        }
                    }

                    var scale = scaleDamage (userID);
                    if (scale != -1) {
                        sb.AppendLine ("<color=lightblue>Scale</color>: " + scale);
                    }

                    return sb.ToString ();
                }

                return "No player found.";
            }

            return "Invalid Syntax. ao <PlayerName>";
        }

        #endregion

        #region Clan Integration

        public bool IsClanOffline (ulong targetID)
        {
            var mcount = getClanMembersOnline (targetID);

            if (mcount >= minMembers) {
                return false;
            }

            return true;
        }

        public int getClanMembersOnline (ulong targetID)
        {
            var player = covalence.Players.FindPlayerById (targetID.ToString ());
            var start = (player.IsConnected == false) ? 0 : 1;

            if (Clans == null) {
                PrintWarning ("Clans plugin not installed");
                clanShare = false;
            }

            var tag = Clans.Call<string> ("GetClanOf", targetID);
            if (tag == null) {
                return start;
            }

            var members = getClanMembers (tag);

            int mcount = start;

            foreach (string memberid in members) {
                var mid = Convert.ToUInt64 (memberid);
                if (mid == targetID)
                    continue;
                if (!IsOffline (mid))
                    mcount++;
            }

            return mcount;
        }

        public List<string> getClanMembers (string tag)
        {
            List<string> memberList;
            if (memberCache.TryGetValue (tag, out memberList))
                return memberList;

            return CacheClan (tag);
        }

        public List<string> CacheClan (string tag)
        {
            if (string.IsNullOrEmpty (tag))
                return null;

            var clan = Clans.Call<JObject> ("GetClan", tag);

            var members = new List<string> ();

            if (clan == null) {
                return members;
            }

            if (clan ["members"] == null) {
                return members;
            }

            foreach (string memberid in clan ["members"]) {
                members.Add (memberid);
            }

            if (memberCache.ContainsKey (tag)) {
                memberCache [tag] = members;
            } else {
                memberCache.Add (tag, members);
            }

            return members;
        }

        public ulong getClanOffline (string tag)
        {
            var clanMembers = getClanMembers (tag);

            var members = new Dictionary<string, double> ();

            if (clanMembers == null || (clanMembers != null && clanMembers.Count == 0)) {
                return 0;
            }

            foreach (string memberid in clanMembers) {
                var mid = Convert.ToUInt64 (memberid);
                LastOnline lastOnlineMember;
                if (lastOnline.TryGetValue (mid, out lastOnlineMember)) {
                    members.Add (memberid, lastOnlineMember.Minutes);
                }
            }

            if (clanFirstOffline) {
                // loop through all offline members, sorted by who is most offline
                foreach (var kvp in members.OrderByDescending (p => p.Value)) {
                    return Convert.ToUInt64 (kvp.Key);
                }
            } else {
                // loop through all offline members, sorted by who is least offline
                foreach (var kvp in members.OrderBy (p => p.Value)) {
                    return Convert.ToUInt64 (kvp.Key);
                }
            }

            return 0;
        }

        void OnClanCreate (string tag)
        {
            CacheClan (tag);
        }

        void OnClanUpdate (string tag)
        {
            CacheClan (tag);
        }

        void OnClanDestroy (string tag)
        {
            if (memberCache.ContainsKey (tag)) {
                memberCache.Remove (tag);
            }
        }

        #endregion

        #region Commands

        [ConsoleCommand ("ao")]
        void ccStatus (ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
                return;
            if (arg.Connection.player is BasePlayer) {
                if (!HasPerm (arg.Connection.player as BasePlayer, "antiofflineraid.check") && arg.Connection.authLevel < 1) {
                    SendReply (arg, GetMsg ("Denied: Permission", arg.Connection.userid));
                    return;
                }
            }
            SendReply (arg, SendStatus (arg.Connection, arg.Args));
        }

        [ChatCommand ("ao")]
        void cmdStatus (BasePlayer player, string command, string [] args)
        {
            if (!HasPerm (player, "antiofflineraid.check") && player.net.connection.authLevel < 1) {
                SendReply (player, GetMsg ("Denied: Permission", player.UserIDString));
                return;
            }

            SendReply (player, SendStatus (player.net.connection, args));
        }

        [ChatCommand ("aor.test.offline")]
        void cmdboffline (BasePlayer player, string command, string [] args)
        {
            if (!player.IsAdmin)
                return;
            lastOnline [player.userID].lastOnline = lastOnline [player.userID].lastOnline.Subtract (TimeSpan.FromHours (3));
        }

        [ChatCommand ("aor.test.online")]
        void cmdbonline (BasePlayer player, string command, string [] args)
        {
            if (!player.IsAdmin)
                return;
            lastOnline [player.userID].lastOnline = DateTime.Now;
            lastOnline [player.userID].afkMinutes = 0;
        }

        #endregion

        #region HelpText

        void SendHelpText (BasePlayer player)
        {
            var sb = new StringBuilder ()
               .Append ("AntiOfflineRaid by <color=#ce422b>http://rustservers.io</color>\n");

            if (cooldownMinutes > 0) {
                sb.Append ("  ").Append (string.Format ("<color=\"#ffd479\">First {0} minutes</color>: 100%", cooldownMinutes)).Append ("\n");
                sb.Append ("  ").Append (string.Format ("<color=\"#ffd479\">Between {0} minutes and 1 hour</color>: {1}%", cooldownMinutes, interimDamage * 100)).Append ("\n");
            } else {
                sb.Append ("  ").Append (string.Format ("<color=\"#ffd479\">First hour</color>: {0}%", interimDamage * 100)).Append ("\n");
            }

            var keys = damageScale.Keys.Select (int.Parse).ToList ();
            keys.Sort ();

            foreach (var key in keys) {
                double scale = System.Math.Round (Convert.ToDouble (damageScale [key.ToString ()]) * 100, 0);
                double hours = System.Math.Round (Convert.ToDouble (key), 1);
                if (hours >= 24) {
                    double days = System.Math.Round (hours / 24, 1);
                    sb.Append ("  ").Append (string.Format ("<color=\"#ffd479\">After {0} days(s)</color>: {1}%", days, scale)).Append ("\n");
                } else {
                    sb.Append ("  ").Append (string.Format ("<color=\"#ffd479\">After {0} hour(s)</color>: {1}%", hours, scale)).Append ("\n");
                }
            }

            player.ChatMessage (sb.ToString ());
        }

        #endregion

        #region Helper Methods

        T GetConfig<T> (string name, T defaultValue)
        {
            if (Config [name] == null) {
                Config [name] = defaultValue;
                Config.Save ();
                return defaultValue;
            }


            return (T)Convert.ChangeType (Config [name], typeof (T));
        }

        protected IPlayer FindPlayerByPartialName (string nameOrIdOrIp)
        {
            if (string.IsNullOrEmpty (nameOrIdOrIp))
                return null;

            var player = covalence.Players.FindPlayer (nameOrIdOrIp);

            if (player is IPlayer) {
                return player;
            }

            return null;
        }

        bool HasPerm (BasePlayer p, string pe)
        {
            return permission.UserHasPermission (p.userID.ToString (), pe);
        }

        bool HasPerm (string userid, string pe)
        {
            return permission.UserHasPermission (userid, pe);
        }

        string GetMsg (string key, object userID = null)
        {
            return lang.GetMessage (key, this, userID == null ? null : userID.ToString ());
        }

        #endregion

        #region GUI

        void HideMessage (BasePlayer player)
        {
            if (player.net == null)
                return;
            if (player.net.connection == null)
                return;

            CommunityEntity.ServerInstance.ClientRPCEx (new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "AntiOfflineRaidMsg");
        }

        StringBuilder sb = new StringBuilder ();

        void ShowMessage (BasePlayer player, int amount = 100)
        {
            HideMessage (player);
            sb.Clear ();
            sb.Append (jsonMessage);
            sb.Replace ("{1}", Oxide.Core.Random.Range (1, 99999).ToString ());
            sb.Replace ("{protection_message}", GetMsg ("Protection Message", player.UserIDString));
            sb.Replace ("{amount}", amount.ToString ());
            CommunityEntity.ServerInstance.ClientRPCEx (new Network.SendInfo { connection = player.net.connection }, null, "AddUI", sb.ToString ());

            timer.In (3f, delegate () {
                HideMessage (player);
            });
        }

        string jsonMessage = @"[{""name"":""AntiOfflineRaidMsg"",""parent"":""Overlay"",""components"":[{""type"":""UnityEngine.UI.Image"",""color"":""0 0 0 0.78""},{""type"":""RectTransform"",""anchormax"":""0.64 0.88"",""anchormin"":""0.38 0.79""}]},{""name"":""MessageLabel{1}"",""parent"":""AntiOfflineRaidMsg"",""components"":[{""type"":""UnityEngine.UI.Text"",""align"":""MiddleCenter"",""fontSize"":""19"",""text"":""{protection_message}""},{""type"":""RectTransform"",""anchormax"":""1 1"",""anchormin"":""0 0""}]}]";

        #endregion
    }
}
