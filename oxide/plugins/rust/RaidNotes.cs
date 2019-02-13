using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;

using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;

using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using UnityEngine;
using Rust;
using Facepunch;

namespace Oxide.Plugins
{
    [Info ("RaidNotes", "Calytic", "1.0.21", ResourceId = 2117)]
    [Description ("Broadcasts raid activity to chat & more")]
    public class RaidNotes : RustPlugin
    {
        #region Variables

        [PluginReference]
        Plugin Discord, Slack, Clans, LustyMap;

        DynamicConfigFile data;
        public static JsonSerializer SERIALIZER = new JsonSerializer ();
        public static JsonConverter [] CONVERTERS = new JsonConverter [] { new UnityVector3Converter (), new DateTimeConverter () };
        Dictionary<string, bool> _raidableCache = new Dictionary<string, bool> ();
        int blockLayer = UnityEngine.LayerMask.GetMask (new string [] { "Player (Server)" });
        static Dictionary<string, int> reverseItems = new Dictionary<string, int> ();
        static List<string> explosionRadiusPrefabs = new List<string> ();
        Dictionary<Raid, Timer> timers = new Dictionary<Raid, Timer> ();
        Dictionary<string, DateTime> detectionCooldowns = new Dictionary<string, DateTime> ();
        static Regex _htmlRegex = new Regex ("<.*?>", RegexOptions.Compiled);
        Color [] colors = new Color [7] { Color.blue, Color.cyan, Color.gray, Color.green, Color.magenta, Color.red, Color.yellow };

        float raidDuration = 300f;
        float raidDistance = 50f;
        float shoulderHeight = 0.45f;
        bool isNewSave = false;
        float detectionDuration = 60f;
        string slackType = "FancyMessage";
        int logHours = 12;

        bool trackExplosives = true;
        bool checkEntityDamage, checkEntityDeath, announceGlobal, printToLog = true;
        bool announceRaidStart, announceRaidEnd, announceToVictims, announceToSlack, announceToDiscord, announceToLustyMap, announceClan = false;
        string announcePrefixColor = "orange";
        string announceIcon;
        string announceNameColor = "lightblue";
        string announceClanColor = "#00eaff";
        string lustyMapIcon = "special";
        float lustyMapDuration = 10f;
        int detectionDistance = 50;
        float detectionCountdown = 1f;

        private Dictionary<string, object> weaponColors = new Dictionary<string, object> ()
        {
            { "0", "#666333" },
            { "498591726", "#666666" }
        };

        private Dictionary<string, object> gradeColors = new Dictionary<string, object> ()
        {
            { "Wood", "#a68b44" },
            { "Stone", "#a4a4a4" },
            { "Metal", "#9b5050" },
            { "TopTier", "#473131" }
        };

        private float announceDelay, announceRadius = 0f;

        internal int announceMinParticipants, announceMinWeapons, announceMinKills, announceMinDestroyed, announceMinMinutes = 0;

        List<string> prefabs = new List<string> ()
        {
            "door.hinged",
            "door.double.hinged",
            "window.bars",
            "floor.ladder.hatch",
            "floor.frame",
            "wall.frame",
            "shutter"
        };

        Dictionary<long, Raid> raids = new Dictionary<long, Raid> ();

        #endregion

        #region Classes

        public class AttackVector
        {
            public Vector3 start;
            public Vector3 end;
            public int weapon;
            public ulong victim;
            public ulong initiator;

            [JsonConstructor]
            public AttackVector (Vector3 start, Vector3 end, int weapon, ulong victim = 0, ulong initiator = 0)
            {
                this.start = start;
                this.end = end;
                this.weapon = weapon;
                this.victim = victim;
                this.initiator = initiator;
            }
        }

        public class Raid
        {
            public long start = DateTime.Now.ToBinary ();
            public long end = 0;
            public Vector3 firstDamage;
            public Vector3 lastDamage;
            public List<AttackVector> attackVector = new List<AttackVector> ();
            public List<AttackVector> killMap = new List<AttackVector> ();
            public ulong initiator;
            public ulong victim;
            public List<ulong> blockOwners = new List<ulong> ();
            public List<ulong> participants = new List<ulong> ();
            public int lastWeapon;
            public Dictionary<int, int> weapons = new Dictionary<int, int> ();

            public Dictionary<BuildingGrade.Enum, int> blocksDestroyed = new Dictionary<BuildingGrade.Enum, int> ();
            public Dictionary<string, int> entitiesDestroyed = new Dictionary<string, int> ();

            [JsonConstructor]
            public Raid (
                long start,
                long end,
                Vector3 firstDamage,
                Vector3 lastDamage,
                List<AttackVector> attackVector,
                ulong initiator,
                ulong victim,
                List<ulong> blockOwners,
                List<ulong> participants,
                int lastWeapon,
                Dictionary<int, int> weapons,
                List<AttackVector> killMap = null,
                Dictionary<BuildingGrade.Enum, int> blocksDestroyed = null,
                Dictionary<string, int> entitiesDestroyed = null
            )
            {
                this.start = start;
                this.end = end;
                this.firstDamage = firstDamage;
                this.lastDamage = lastDamage;
                this.attackVector = attackVector;
                this.initiator = initiator;
                this.victim = victim;
                this.blockOwners = blockOwners;
                this.participants = participants;
                this.lastWeapon = lastWeapon;
                this.weapons = weapons;
                if (killMap != null) {
                    this.killMap = killMap;
                }

                if (blocksDestroyed != null) {
                    this.blocksDestroyed = blocksDestroyed;
                }

                if (entitiesDestroyed != null) {
                    this.entitiesDestroyed = entitiesDestroyed;
                }
            }

            [JsonIgnore]
            internal AttackVector lastAttackVector = null;

            [JsonIgnore]
            RaidNotes plugin;

            [JsonIgnore]
            public bool Completed {
                get {
                    return end != 0;
                }
            }

            [JsonIgnore]
            public DateTime lastRefresh;

            [JsonIgnore]
            public IPlayer Initiator {
                get {
                    return plugin.covalence.Players.FindPlayerById (initiator.ToString ());
                }
            }

            [JsonIgnore]
            public IPlayer Victim {
                get {
                    return plugin.covalence.Players.FindPlayerById (victim.ToString ());
                }
            }

            [JsonIgnore]
            [JsonConverter (typeof (IsoDateTimeConverter))]
            public DateTime Start {
                get {
                    return DateTime.FromBinary (start);
                }
            }

            [JsonIgnore]
            [JsonConverter (typeof (IsoDateTimeConverter))]
            public DateTime End {
                get {
                    return DateTime.FromBinary (end);
                }
            }

            public Raid (RaidNotes plugin, ulong initiator, ulong victim, Vector3 firstDamage)
            {
                this.plugin = plugin;
                this.initiator = initiator;
                this.victim = victim;
                this.firstDamage = firstDamage;
            }

            [JsonIgnore]
            public double Hours {
                get {
                    if (!Completed) {
                        return 0;
                    }
                    var ts = DateTime.Now - End;
                    return ts.TotalHours;
                }
            }

            public bool HasHours (int hours)
            {
                if (Hours >= hours) {
                    return true;
                }

                return false;
            }

            public void Participate (BasePlayer player)
            {
                var behavior = player.gameObject.AddComponent<RaidBehavior> ();
                behavior.raid = this;
                if (!participants.Contains (player.userID)) {
                    participants.Add (player.userID);
                }
            }

            public bool IsAnnounced ()
            {
                if (participants.Count < plugin.announceMinParticipants)
                    return false;
                if (killMap.Count < plugin.announceMinKills)
                    return false;
                if (weapons.Count < plugin.announceMinWeapons)
                    return false;
                if ((entitiesDestroyed.Count + blocksDestroyed.Count) < plugin.announceMinDestroyed)
                    return false;

                var ts = End - Start;
                if (ts.TotalMinutes < plugin.announceMinMinutes)
                    return false;

                return true;
            }

            internal JObject Vector2JObject (Vector3 vector)
            {
                var obj = new JObject ();
                obj.Add ("x", vector.x);
                obj.Add ("y", vector.y);
                obj.Add ("z", vector.z);

                return obj;
            }

            public override string ToString ()
            {
                return ToJObject ().ToString ();
            }

            internal JObject ToJObject ()
            {
                var obj = new JObject ();

                obj ["start"] = Start.ToString ();
                obj ["end"] = End.ToString ();
                var explosions = new JObject ();
                explosions.Add ("first", Vector2JObject (firstDamage));
                explosions.Add ("last", Vector2JObject (lastDamage));
                obj ["explosions"] = explosions;

                obj ["initiator"] = initiator;
                obj ["victim"] = victim;

                JArray owners = new JArray ();
                foreach (var owner in blockOwners)
                    owners.Add (owner);

                obj ["owners"] = owners;

                JArray participantsData = new JArray ();
                foreach (var participant in participants)
                    participantsData.Add (participant);

                obj ["participants"] = participantsData;


                JObject weaponsData = new JObject ();
                foreach (var kvp in weapons)
                    weaponsData.Add (kvp.Key.ToString (), kvp.Value);

                obj ["weapons"] = weaponsData;

                obj ["attackvector"] = JArray.FromObject (attackVector, SERIALIZER);
                obj ["kills"] = JArray.FromObject (killMap, SERIALIZER);

                return obj;
            }

            internal void OnEnded ()
            {
                end = DateTime.Now.ToBinary ();
                Interface.CallHook ("OnRaidEnded", ToJObject ());
            }

            internal void OnStarted ()
            {
                Interface.CallHook ("OnRaidStarted", ToJObject ());
            }

            internal void Attack (AttackVector vector)
            {
                lastAttackVector = vector;
                attackVector.Add (vector);
            }

            internal void Kill (AttackVector vector)
            {
                killMap.Add (vector);
            }
        }

        public class RaidBehavior : MonoBehaviour
        {
            public BasePlayer player;
            internal Raid raid;


            void Awake ()
            {
                player = GetComponent<BasePlayer> ();
            }

            void OnDestroy ()
            {
                GameObject.Destroy (this);
            }
        }

        public class ExplosiveTracker : MonoBehaviour
        {
            public BaseEntity entity;
            public Vector3 lastValidPosition;

            public BasePlayer thrownBy;
            public Vector3 thrownFrom;

            void Awake ()
            {
                entity = GetComponent<BaseEntity> ();
                lastValidPosition = entity.transform.position;
            }

            void Update ()
            {
                if (Vector3.Distance (entity.transform.position, Vector3.zero) > 3) {
                    lastValidPosition = entity.transform.position;
                }
            }

            void OnDestroy ()
            {
                if (thrownBy == null)
                    return;
                var behavior = thrownBy.GetComponent<RaidBehavior> ();

                if (behavior != null && behavior.raid != null) {
                    int itemid;
                    if (reverseItems.TryGetValue (entity.PrefabName, out itemid))
                        behavior.raid.Attack (new AttackVector (thrownFrom, lastValidPosition, itemid, 0, thrownBy.userID));

                }
                GameObject.Destroy (this);
            }
        }

        enum AnnouncementType
        {
            Start = 1,
            End = 2,
            Slack_Start = 3,
            Slack_End = 4,
            Discord_Start = 5,
            Discord_End = 6
        }

        #endregion

        #region Initialization

        protected override void LoadDefaultConfig ()
        {
            PrintToConsole ("Creating new configuration");
            Config.Clear ();

            Config ["Settings", "trackEntityDamage"] = true;
            Config ["Settings", "trackEntityDeath"] = true;
            Config ["Settings", "trackExplosives"] = true;

            Config ["Raid", "distance"] = 50f;
            Config ["Raid", "duration"] = 300f;
            Config ["Raid", "logUpToHours"] = 3;
            Config ["Raid", "detectionDistance"] = 50;
            Config ["Raid", "detectionDuration"] = 60f;
            Config ["Raid", "detectionCountdownMinutes"] = 1f;

            Config ["AnnounceWhen", "raidEnds"] = false;
            Config ["AnnounceWhen", "raidStarts"] = false;
            Config ["AnnounceWhen", "minParticipants"] = 0;
            Config ["AnnounceWhen", "minWeapons"] = 0;
            Config ["AnnounceWhen", "minKills"] = 0;
            Config ["AnnounceWhen", "minMinutes"] = 0;

            Config ["AnnounceTo", "global"] = false;
            Config ["AnnounceTo", "clan"] = true;
            Config ["AnnounceTo", "victims"] = true;
            Config ["AnnounceTo", "slack"] = false;
            Config ["AnnounceTo", "discord"] = false;
            Config ["AnnounceTo", "lustymap"] = false;
            Config ["AnnounceTo", "log"] = true;

            Config ["Announcements", "icon"] = 0;
            Config ["Announcements", "prefixColor"] = "orange";
            Config ["Announcements", "nameColor"] = "lightblue";
            Config ["Announcements", "clanColor"] = "#00eaff";
            Config ["Announcements", "weaponColors"] = weaponColors;
            Config ["Announcements", "delay"] = 0f;
            Config ["Announcements", "radius"] = 0f;

            Config ["LustyMap", "icon"] = "special";
            Config ["LustyMap", "duration"] = 10f;

            Config ["Slack", "messageType"] = "FancyMessage";
        }

        void LoadMessages ()
        {
            lang.RegisterMessages (new Dictionary<string, string>
                {
                    { "Announce: Prefix", "Raid" },
                    { "Announce: Start", "{initiatorClan} {initiator} ({initiatorClanMates}) is raiding {victimClan} {victim} ({victimClanMates})" },
                    { "Announce: End", "{initiatorClan} {initiator} ({initiatorClanMates}) raided {victimClan} {victim} ({victimClanMates}) using {weaponList} destroying {destroyedList}" },
                    { "Announce: Slack Start", "{initiatorClan} {initiator} ({initiatorClanMates}) is raiding {victimClan} {victim} ({victimClanMates})" },
                    { "Announce: Slack End", "{initiatorClan} {initiator} ({initiatorClanMates}) raided {victimClan} {victim} ({victimClanMates}) with {weaponList} destroying {destroyedList}" },
                    { "Announce: Discord Start", "{initiatorClan} {initiator} ({initiatorClanMates}) is raiding {victimClan} {victim} ({victimClanMates})" },
                    { "Announce: Discord End", "{initiatorClan} {initiator} ({initiatorClanMates}) raided {victimClan} {victim} ({victimClanMates}) with {weaponList} destroying {destroyedList}" },
                    { "Denied: Permission", "You lack permission to do that" },
                    { "Raid: Found", "<size=15>Raid(s) found: {raidCount}</size>" },
                    { "Raid: Started", "Started: <b>{date}</b>" },
                    { "Raid: Ended", "Ended: <b>{date}</b>" },
                    { "Raid: Duration", "Duration: <b>{duration}</b> minutes" },
                    { "Raid: Initiator", "Initiator: <b>{initiatorName}</b> ({initiatorID})" },
                    { "Raid: PlayerList", "{listName}: <b>{list}</b>" },
                    { "Raid: Activity", "Raid" },
                    { "Target: Nothing", "Nothing" },
                    { "Cooldown: Seconds", "You are doing that too often, try again in a {0} seconds(s)." },
                    { "Cooldown: Minutes", "You are doing that too often, try again in a {0} minute(s)." },
                }, this);
        }

        void OnServerInitialized ()
        {
            LoadData ();
            LoadMessages ();

            foreach (JsonConverter converter in CONVERTERS) {
                SERIALIZER.Converters.Add (converter);
            }

            permission.RegisterPermission ("raidnotes.inspect", this);

            raidDistance = GetConfig ("Raid", "distance", 50f);
            raidDuration = GetConfig ("Raid", "duration", 300f);
            logHours = GetConfig ("Raid", "logUpToHours", 3);
            detectionDistance = GetConfig ("Raid", "detectionDistance", 50);
            detectionDuration = GetConfig ("Raid", "detectionDuration", 60f);
            detectionDuration = GetConfig ("Raid", "detectionCountdownMinutes", 1f);

            checkEntityDamage = GetConfig ("Settings", "hookEntityDamage", true);
            checkEntityDeath = GetConfig ("Settings", "hookEntityDeath", true);
            trackExplosives = GetConfig ("Settings", "trackExplosives", true);

            announceGlobal = GetConfig ("AnnounceTo", "global", false);
            announceClan = GetConfig ("AnnounceTo", "clan", true);
            announceToVictims = GetConfig ("AnnounceTo", "victims", true);
            announceToSlack = GetConfig ("AnnounceTo", "slack", false);
            announceToLustyMap = GetConfig ("AnnounceTo", "lustymap", false);
            announceToDiscord = GetConfig ("AnnounceTo", "discord", false);
            printToLog = GetConfig ("AnnounceTo", "log", true);

            announceRaidEnd = GetConfig ("AnnounceWhen", "raidEnds", false);
            announceRaidStart = GetConfig ("AnnounceWhen", "raidStarts", false);
            announceMinParticipants = GetConfig ("AnnounceWhen", "minParticipants", 0);
            announceMinWeapons = GetConfig ("AnnounceWhen", "minWeapons", 0);
            announceMinDestroyed = GetConfig ("AnnounceWhen", "minDestroyed", 0);
            announceMinKills = GetConfig ("AnnounceWhen", "minKills", 0);
            announceMinMinutes = GetConfig ("AnnounceWhen", "minMinutes", 0);

            announceIcon = GetConfig ("Announcements", "icon", "0");
            announcePrefixColor = GetConfig ("Announcements", "prefixColor", "orange");
            announceNameColor = GetConfig ("Announcements", "nameColor", "lightblue");
            announceClanColor = GetConfig ("Announcements", "clanColor", "#00eaff");
            weaponColors = GetConfig ("Announcements", "weaponColors", weaponColors);
            announceDelay = GetConfig ("Announcements", "delay", 0f);
            announceRadius = GetConfig ("Announcements", "radius", 0f);

            lustyMapIcon = GetConfig ("LustyMap", "icon", "special");
            lustyMapDuration = GetConfig ("LustyMap", "duration", 10f);

            slackType = GetConfig ("Slack", "messageType", "FancyMessage");

            if (announceToSlack && !Slack) {
                PrintWarning ("Slack plugin not found, please install http://oxidemod.org/plugins/slack.1952/");
                announceToSlack = false;
            }

            if (announceToDiscord && !Discord) {
                PrintWarning ("Discord plugin not found, please install http://oxidemod.org/plugins/discord.2149/");
                announceToDiscord = false;
            }

            if (announceToLustyMap && !LustyMap) {
                PrintWarning ("LustyMap plugin not found, please install http://oxidemod.org/plugins/lustymap.1333/");
            }

            if (logHours == 0) {
                Unsubscribe (nameof (OnServerSave));
            }

            if (!trackExplosives) {
                Unsubscribe (nameof (OnExplosiveThrown));
                Unsubscribe (nameof (OnRocketLaunched));
            }

            foreach (ItemDefinition def in ItemManager.GetItemDefinitions ()) {
                var modEntity = def.GetComponent<ItemModEntity> ();
                if (modEntity != null && modEntity.entityPrefab != null) {
                    var prefab = modEntity.entityPrefab.Get ();
                    var thrownWeapon = prefab.GetComponent<ThrownWeapon> ();

                    if (thrownWeapon != null && !string.IsNullOrEmpty (thrownWeapon.prefabToThrow.guid) && !reverseItems.ContainsKey (thrownWeapon.prefabToThrow.resourcePath)) {
                        reverseItems.Add (thrownWeapon.prefabToThrow.resourcePath, def.itemid);
                        continue;
                    }
                }

                var baseProjectile = def.GetComponent<ItemModProjectile> ();
                if (baseProjectile != null && !string.IsNullOrEmpty (baseProjectile.projectileObject.guid) && !reverseItems.ContainsKey (baseProjectile.projectileObject.resourcePath)) {
                    if (baseProjectile.projectileObject.resourcePath.Contains ("rocket") && !baseProjectile.projectileObject.resourcePath.Contains ("smoke")) {
                        explosionRadiusPrefabs.Add (baseProjectile.projectileObject.resourcePath);
                        reverseItems.Add (baseProjectile.projectileObject.resourcePath, def.itemid);
                    }
                }
            }

            if (announceClan) {
                if (!plugins.Exists ("Clans")) {
                    announceClan = false;
                    PrintWarning ("Clans plugin not found, please install http://oxidemod.org/plugins/clans.2087/");
                }
            }
        }

        protected void ReloadConfig ()
        {
            Config ["VERSION"] = Version.ToString ();
            ClearData ();
            PrintToConsole ("Upgrading configuration file");
            SaveConfig ();
        }

        void LoadData ()
        {
            if (logHours > 0) {
                data = Interface.Oxide.DataFileSystem.GetFile (nameof (RaidNotes));
                data.Settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                data.Settings.Converters = CONVERTERS;

                raids = data.ReadObject<Dictionary<long, Raid>> ();

                if (isNewSave) {
                    ClearData ();
                    isNewSave = false;
                }
            }

            if (Config ["VERSION"] == null) {
                // FOR COMPATIBILITY WITH INITIAL VERSIONS WITHOUT VERSIONED CONFIG
                ReloadConfig ();
            } else if (GetConfig<string> ("VERSION", "") != Version.ToString ()) {
                // ADDS NEW, IF ANY, CONFIGURATION OPTIONS
                ReloadConfig ();
            }
        }

        void SaveData (bool force = false)
        {
            if (raids.Count > 0 && !force) {
                var toRemove = raids.Where (pair => pair.Value.Hours > logHours)
                                             .Select (pair => pair.Key)
                                             .ToList ();

                foreach (var key in toRemove)
                    raids.Remove (key);
            }

            data.WriteObject<Dictionary<long, Raid>> (raids);
        }

        void ClearData ()
        {
            raids.Clear ();
            SaveData ();
        }

        void OnNewSave (string filename)
        {
            isNewSave = true;
        }

        void OnServerSave ()
        {
            SaveData ();
        }

        void Unload ()
        {
            var objects = GameObject.FindObjectsOfType (typeof (RaidBehavior));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy (gameObj);

            objects = GameObject.FindObjectsOfType (typeof (ExplosiveTracker));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy (gameObj);

            if (logHours > 0) {
                SaveData (true);
            }
        }

        #endregion

        #region Oxide Hooks

        void OnExplosiveThrown (BasePlayer player, BaseEntity entity)
        {
            if (player == null || entity.net == null)
                return;
            AddTracker (player, entity);
        }

        void OnRocketLaunched (BasePlayer player, BaseEntity entity)
        {
            if (player == null || entity.net == null || !reverseItems.ContainsKey (entity.PrefabName))
                return;

            AddTracker (player, entity);
        }

        List<uint> recentAttacks = new List<uint> ();

        private void OnEntityTakeDamage (BaseCombatEntity entity, HitInfo hitInfo)
        {
            if(entity == null)
                return;
            if (!checkEntityDamage)
                return;
            if (hitInfo == null ||
                hitInfo.Initiator == null ||
                hitInfo.WeaponPrefab == null ||
                !IsEntityRaidable (entity))
                return;

            var prefabName = hitInfo.WeaponPrefab.PrefabName;

            if (explosionRadiusPrefabs.Contains (prefabName) && hitInfo.Initiator.net != null) {
                if (recentAttacks.Contains (hitInfo.Initiator.net.ID)) {
                    return;
                } else {
                    recentAttacks.Add (hitInfo.Initiator.net.ID);
                }

                Interface.Oxide.NextTick (delegate () {
                    if (recentAttacks.Contains (hitInfo.Initiator.net.ID)) {
                        recentAttacks.Remove (hitInfo.Initiator.net.ID);
                    }
                });
            }

            int itemUsed;
            if (!reverseItems.TryGetValue (prefabName, out itemUsed))
                return;

            if(hitInfo.damageTypes != null) {
                var majorityDamageType = hitInfo.damageTypes.GetMajorityDamageType ();
    
                switch (majorityDamageType) {
                case DamageType.Explosion:
                case DamageType.Heat:
                    StructureAttack (entity, hitInfo.Initiator, itemUsed);
                    break;
                }
            }
        }

        private void OnEntityDeath (BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (!checkEntityDeath || hitInfo == null || hitInfo.WeaponPrefab == null || hitInfo.Initiator == null || !(hitInfo.Initiator is BasePlayer))
                return;
            if (IsEntityRaidable (entity)) {
                var majorityDamageType = hitInfo.damageTypes.GetMajorityDamageType ();

                var prefabName = hitInfo.WeaponPrefab.PrefabName;
                int itemUsed;
                if (reverseItems.TryGetValue (prefabName, out itemUsed)) {
                    switch (majorityDamageType) {
                    case DamageType.Explosion:
                    case DamageType.Heat:
                        StructureAttack (entity, hitInfo.Initiator as BasePlayer, itemUsed, true);
                        break;
                    }
                }
            } else if (entity is BasePlayer)
                RegisterKill (entity as BasePlayer, hitInfo.Initiator as BasePlayer);

        }

        #endregion

        #region Commands

        [ConsoleCommand ("raids.wipe")]
        private void ccRaidsWipe (ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null || (arg.Connection != null && arg.Connection.authLevel > 0)) {
                ClearData ();

                SendReply (arg, "Data wiped");
                return;
            }

            SendReply (arg, GetMsg ("Denied: Permission"));
        }

        [ChatCommand ("inspect")]
        private void cmdInspect (BasePlayer player, string command, string [] args)
        {
            var permission = HasPerm (player, "raidnotes.inspect");
            if (permission || (!permission && player.net.connection.authLevel > 0)) {
                if (!CheckCooldown (player))
                    return;
                else
                    PlayerCooldown (player);

                SendRaids (player, args);
                return;
            }

            SendReply (player, GetMsg ("Denied: Permission", player));
        }

        #endregion

        #region Core Methods

        void PlayerCooldown (BasePlayer player)
        {
            if (player.IsAdmin)
                return;

            if (detectionCooldowns.ContainsKey (player.UserIDString))
                detectionCooldowns.Remove (player.UserIDString);

            detectionCooldowns.Add (player.UserIDString, DateTime.Now);
        }

        bool CheckCooldown (BasePlayer player)
        {
            if (detectionCountdown > 0) {
                DateTime startTime;
                if (detectionCooldowns.TryGetValue (player.UserIDString, out startTime)) {
                    var endTime = DateTime.Now;

                    var span = endTime.Subtract (startTime);
                    if (span.TotalMinutes > 0 && span.TotalMinutes < Convert.ToDouble (detectionCountdown)) {
                        var timeleft = System.Math.Round (Convert.ToDouble (detectionCountdown) - span.TotalMinutes, 2);
                        if (timeleft < 1) {
                            var timelefts = System.Math.Round ((Convert.ToDouble (detectionCountdown) * 60) - span.TotalSeconds);
                            SendReply (player, string.Format (GetMsg ("Cooldown: Seconds", player), timelefts.ToString ()));
                        } else
                            SendReply (player, string.Format (GetMsg ("Cooldown: Minutes", player), System.Math.Round (timeleft).ToString ()));

                        return false;
                    } else
                        detectionCooldowns.Remove (player.UserIDString);

                }
            }

            return true;
        }

        void AddTracker (BasePlayer player, BaseEntity entity)
        {
            var tracker = entity.gameObject.AddComponent<ExplosiveTracker> ();
            tracker.thrownBy = player;
            tracker.thrownFrom = player.transform.position;
            tracker.thrownFrom.y += player.GetHeight () * shoulderHeight;
        }

        Vector3 Track (BaseEntity initiator, BaseEntity entity)
        {
            var fromPos = Vector3.zero;
            var tracker = trackExplosives ? initiator.gameObject.GetComponent<ExplosiveTracker> () : null;
            if (tracker != null)
                fromPos = tracker.thrownFrom;
            else
                fromPos = initiator.transform.position;

            return fromPos;
        }

        List<Raid> GetRaids (BasePlayer player, string [] args)
        {
            var defaultDistance = detectionDistance;
            if (args != null && args.Length == 1)
                int.TryParse (args [0], out defaultDistance);

            if (defaultDistance > detectionDistance)
                defaultDistance = detectionDistance;

            return raids.Where (pair => Vector3.Distance (pair.Value.firstDamage, player.transform.position) <= defaultDistance).Select (pair => pair.Value).ToList ();
        }

        void SendRaids (BasePlayer player, string [] args)
        {
            if (player.net.connection == null)
                return;

            var nearbyRaids = GetRaids (player, args);

            if (nearbyRaids.Count > 0) {
                int found = 0;
                var sbs = new List<StringBuilder> ();
                foreach (var raid in nearbyRaids) {
                    var sb = new StringBuilder ();
                    if (SendRaid (player, raid, sb, found)) {
                        found++;
                        sbs.Add (sb);
                    }
                }

                if (found > 0) {
                    SendReply (player, Format (GetMsg ("Raid: Found", player), raidCount => found));
                    foreach (var sb in sbs) {
                        SendReply (player, sb.ToString ());
                    }
                    return;
                }
            }

            SendReply (player, "No raids found");
        }

        public string ToHex (Color c)
        {
            return string.Format ("#{0:X2}{1:X2}{2:X2}", ToByte (c.r), ToByte (c.g), ToByte (c.b));
        }

        private byte ToByte (float f)
        {
            f = Mathf.Clamp01 (f);
            return (byte)(f * 255);
        }

        bool SendRaid (BasePlayer player, Raid raid, StringBuilder sb, int found = 0)
        {
            var uiduration = detectionDuration * 60;
            var admin = false;
            if (player.net.connection.authLevel > 0)
                admin = true;

            var randomColor = colors [UnityEngine.Random.Range (0, colors.Length - 1)];
            var validAttack = false;
            foreach (AttackVector attack in raid.attackVector) {
                if (!admin && attack.victim != player.userID)
                    continue;

                validAttack = true;
                var weapName = string.Empty;
                var def = ItemManager.FindItemDefinition (attack.weapon);
                if (def is ItemDefinition)
                    weapName = def.displayName.english;

                player.SendConsoleCommand ("ddraw.arrow", uiduration, randomColor, attack.start, attack.end, 0.2);

                if (!string.IsNullOrEmpty (weapName))
                    player.SendConsoleCommand ("ddraw.text", uiduration, GetWeaponColor (attack.weapon), attack.start, weapName);
            }

            foreach (AttackVector kill in raid.killMap) {
                if (!admin && kill.victim != player.userID)
                    continue;

                validAttack = true;

                player.SendConsoleCommand ("ddraw.arrow", uiduration, randomColor, kill.start, kill.end, 0.2);
                player.SendConsoleCommand ("ddraw.sphere", uiduration, Color.red, kill.end, 0.5f);

                var victimPlayer = covalence.Players.FindPlayerById (kill.victim.ToString ());
                var initiatorPlayer = covalence.Players.FindPlayerById (kill.initiator.ToString ());

                if (victimPlayer is IPlayer) {
                    player.SendConsoleCommand ("ddraw.text", uiduration, Color.red, kill.end + Vector3.up, victimPlayer.Name);
                }

                if (initiatorPlayer is IPlayer) {
                    player.SendConsoleCommand ("ddraw.text", uiduration, Color.red, kill.start + Vector3.up, initiatorPlayer.Name);
                }
            }

            if (validAttack) {
                player.SendConsoleCommand ("ddraw.arrow", uiduration, Color.green, raid.firstDamage + new Vector3 (0, 5, 0), raid.firstDamage, 0.2);
                if (raid.lastDamage != Vector3.zero) {
                    player.SendConsoleCommand ("ddraw.arrow", uiduration, Color.red, raid.lastDamage + new Vector3 (0, 5, 0), raid.lastDamage, 0.2);
                }
                sb.Append (Format ("<color={color}><size=17>#{count}</size></color> + ", count => (found + 1), color => ToHex (randomColor)));
                var start = (raid.Start != null) ? raid.Start.ToString () : "N/A";
                sb.AppendLine (Format (GetMsg ("Raid: Started"), date => start));
                if (raid.Completed) {
                    TimeSpan ts = raid.End - raid.Start;
                    var end = (raid.End != null) ? raid.End.ToString () : "N/A";
                    sb.Append ("   |- ").AppendLine (Format (GetMsg ("Raid: Duration"), duration => Math.Round (ts.TotalMinutes, 2)));
                    sb.Append ("   |- ").AppendLine (Format (GetMsg ("Raid: Ended"), date => end));
                }

                var initiator = covalence.Players.FindPlayerById (raid.initiator.ToString ());
                if (initiator != null) {
                    sb.Append ("   |- ").AppendLine (Format (GetMsg ("Raid: Initiator"), initiatorName => initiator.Name, initiatorID => initiator.Id));
                }

                if (raid.blockOwners.Count > 0) {
                    var victimList = string.Join (", ", raid.blockOwners.Select (x => covalence.Players.FindPlayerById (x.ToString ()).Name).ToArray ());
                    sb.Append ("   |- ").AppendLine (Format (GetMsg ("Raid: PlayerList"), listName => "Property Of", list => victimList));
                }

                if (raid.participants.Count > 0) {
                    var participantList = string.Join (", ", raid.blockOwners.Where (x => !raid.blockOwners.Contains (x)).Select (x => covalence.Players.FindPlayerById (x.ToString ()).Name).ToArray ());
                    if (!string.IsNullOrEmpty (participantList.Trim ())) {
                        sb.Append ("   |- ").AppendLine (Format (GetMsg ("Raid: PlayerList"), listName => "Perpetrators", list => participantList));
                    }
                }

                if (raid.weapons.Count > 0)
                    sb.Append ("   |- ").AppendLine (GetWeaponList (raid));


                if (raid.blocksDestroyed.Count > 0 || raid.entitiesDestroyed.Count > 0)
                    sb.Append ("   |- ").AppendLine (GetDestroyedList (raid));


                return true;
            }

            return false;
        }

        void RegisterKill (BasePlayer player, BasePlayer attacker)
        {
            var behavior = player.GetComponent<RaidBehavior> ();
            if (behavior != null && behavior.raid != null) {
                var activeItem = attacker.GetActiveItem ();
                var itemid = 0;
                if (activeItem != null)
                    itemid = activeItem.info.itemid;

                behavior.raid.Kill (new AttackVector (attacker.transform.position, player.transform.position, itemid, player.userID, attacker.userID));

                GameObject.Destroy (behavior);
            }
        }

        void StructureAttack (BaseEntity targetEntity, BaseEntity sourceEntity, int weapon, bool destroy = false)
        {
            BasePlayer source;

            if (sourceEntity.ToPlayer () is BasePlayer)
                source = sourceEntity.ToPlayer ();
            else {
                var ownerID = (sourceEntity.OwnerID == 0) ? sourceEntity.OwnerID.ToString () : string.Empty;
                if (!string.IsNullOrEmpty (ownerID))
                    source = BasePlayer.Find (ownerID);
                else
                    return;
            }

            if (source == null)
                return;

            var targetID = targetEntity.OwnerID.IsSteamId () ? targetEntity.OwnerID.ToString () : string.Empty;

            if (!string.IsNullOrEmpty (targetID) && targetID != source.UserIDString) {
                var targetIDUint = Convert.ToUInt64 (targetID);
                /* var target = covalence.Players.FindPlayerById (targetID);*/
                Raid raid;
                var raidFound = TryGetRaid (source, targetIDUint, targetEntity.transform.position, out raid);
                raid.lastWeapon = weapon;


                if (raid.blockOwners.Count == 0)
                    raid.victim = targetIDUint;

                if (!raid.blockOwners.Contains (targetIDUint))
                    raid.blockOwners.Add (targetIDUint);

                if (destroy) {
                    if (targetEntity is BuildingBlock) {
                        var grade = ((BuildingBlock)targetEntity).grade;
                        if (raid.blocksDestroyed.ContainsKey (grade))
                            raid.blocksDestroyed [grade]++;
                        else
                            raid.blocksDestroyed.Add (grade, 1);
                    } else if (targetEntity is BaseCombatEntity) {
                        var name = targetEntity.ShortPrefabName;
                        if (raid.entitiesDestroyed.ContainsKey (name))
                            raid.entitiesDestroyed [name]++;
                        else
                            raid.entitiesDestroyed.Add (name, 1);
                    }
                } else {
                    if (raid.weapons.ContainsKey (weapon))
                        raid.weapons [weapon]++;
                    else
                        raid.weapons.Add (weapon, 1);
                }

                if (raid.lastAttackVector != null)
                    raid.lastAttackVector.victim = targetIDUint;

                raid.lastDamage = targetEntity.transform.position;

                if (!raidFound && announceRaidStart) {
                    AnnounceRaidMsg (raid, AnnouncementType.Start);
                    if (announceToSlack)
                        AnnounceRaidMsg (raid, AnnouncementType.Slack_Start);

                    if (announceToDiscord)
                        AnnounceRaidMsg (raid, AnnouncementType.Discord_Start);
                }
            }
        }

        Raid FindRaid (Vector3 position, out List<BasePlayer> nearbyTargets)
        {
            Raid existingRaid = null;

            nearbyTargets = GetNearbyPlayers (position);

            if (existingRaid == null && nearbyTargets.Count > 0) {
                foreach (var nearbyTarget in nearbyTargets) {
                    var behavior = nearbyTarget.GetComponent<RaidBehavior> ();
                    if (behavior != null && behavior.raid != null && existingRaid != behavior.raid && !behavior.raid.Completed) {
                        existingRaid = behavior.raid;
                        break;
                    }
                }
            }

            return existingRaid;
        }

        List<BasePlayer> GetNearbyPlayers (Vector3 position)
        {
            var nearbyTargets = Pool.GetList<BasePlayer> ();
            Vis.Entities<BasePlayer> (position, raidDistance, nearbyTargets, blockLayer);
            nearbyTargets = Sort (position, nearbyTargets);

            return nearbyTargets;
        }

        bool TryGetRaid (BasePlayer source, ulong victim, Vector3 position, out Raid raid)
        {
            Raid existingRaid = null;
            List<BasePlayer> nearbyTargets = null;
            var sourceBehavior = source.GetComponent<RaidBehavior> ();

            if (sourceBehavior != null && sourceBehavior.raid != null && !sourceBehavior.raid.Completed)
                existingRaid = sourceBehavior.raid;
            else
                existingRaid = FindRaid (position, out nearbyTargets);

            bool found = true;

            if (existingRaid == null || (existingRaid != null && existingRaid.Completed)) {
                found = false;
                var newRaid = StartRaid (source, victim, position);
                existingRaid = newRaid;

                if (nearbyTargets == null)
                    nearbyTargets = GetNearbyPlayers (position);

                foreach (var nearbyTarget in nearbyTargets) {
                    var behavior = nearbyTarget.GetComponent<RaidBehavior> ();
                    if (behavior == null || (behavior != null && behavior.raid == null)) {
                        existingRaid.Participate (nearbyTarget);
                    }
                }
            } else if (sourceBehavior == null || (sourceBehavior != null && sourceBehavior.raid == null))
                existingRaid.Participate (source);


            if (nearbyTargets != null)
                Pool.FreeList<BasePlayer> (ref nearbyTargets);

            RefreshRaid (existingRaid);
            raid = existingRaid;
            return found;
        }

        public Raid StartRaid (BasePlayer source, ulong victim, Vector3 position)
        {
            var raid = new Raid (this, source.userID, victim, position);

            RefreshRaid (raid);

            raid.Participate (source);
            raid.OnStarted ();

            return raid;
        }

        private string GetAnnouncementMsg (AnnouncementType type)
        {
            var msgName = string.Empty;
            switch (type) {
            case AnnouncementType.Start:
                msgName = "Announce: Start";
                break;
            case AnnouncementType.End:
                msgName = "Announce: End";
                break;
            case AnnouncementType.Slack_Start:
                msgName = "Announce: Slack Start";
                break;
            case AnnouncementType.Slack_End:
                msgName = "Announce: Slack End";
                break;
            case AnnouncementType.Discord_Start:
                msgName = "Announce: Discord Start";
                break;
            case AnnouncementType.Discord_End:
                msgName = "Announce: Discord End";
                break;
            }

            if (!string.IsNullOrEmpty (msgName))
                return GetMsg (msgName);

            return msgName;
        }

        public void CheckRaid (Raid raid)
        {
            var ts = DateTime.Now - raid.lastRefresh;
            if (ts.TotalSeconds > raidDuration) {
                if (announceToLustyMap && lustyMapDuration > 0)
                    LustyMap?.Call ("RemoveMarker", raid.start.ToString ());

                StopRaid (raid);
                if (announceRaidEnd) {
                    AnnounceRaidMsg (raid, AnnouncementType.End);
                    if (announceToSlack)
                        AnnounceRaidMsg (raid, AnnouncementType.Slack_End);

                    if (announceToDiscord)
                        AnnounceRaidMsg (raid, AnnouncementType.Discord_End);

                }
            }
        }

        void AnnounceRaidMsg (Raid raid, AnnouncementType type)
        {
            if (announceDelay > 0)
                timer.In (announceDelay, delegate () {
                    AnnounceRaid (raid, type);
                });
            else
                AnnounceRaid (raid, type);
        }

        public void RefreshRaid (Raid raid)
        {

            raid.lastRefresh = DateTime.Now;
            Timer t;

            if (timers.TryGetValue (raid, out t)) {
                if (t.Destroyed) {
                    timers.Add (raid, t = timer.Repeat (raidDuration, 0, () => CheckRaid (raid)));
                }
            } else {
                timers.Add (raid, t = timer.Repeat (raidDuration, 0, () => CheckRaid (raid)));
            }
        }

        public void DestroyTimer (Raid raid)
        {
            Timer raidTimer;
            if (timers.TryGetValue (raid, out raidTimer)) {
                if (!raidTimer.Destroyed)
                    raidTimer.Destroy ();

                timers.Remove (raid);
            }
        }

        public void StopRaid (Raid raid)
        {
            foreach (ulong part in raid.participants) {
                var partPlayer = BasePlayer.FindByID (part);
                if (partPlayer != null && partPlayer.GetComponent<RaidBehavior> () != null)
                    GameObject.Destroy (partPlayer.GetComponent<RaidBehavior> ());
            }

            DestroyTimer (raid);

            raid.OnEnded ();
            var raidKey = raid.start;
            if (!raids.ContainsKey (raidKey))
                raids.Add (raidKey, raid);
        }

        string GetWeaponColor (int weaponid)
        {
            object color = "#666666";
            if (weaponColors.TryGetValue (weaponid.ToString (), out color))
                return color.ToString ();


            if (weaponColors.TryGetValue ("0", out color))
                return color.ToString ();

            return color.ToString ();
        }

        string GetGradeColor (int grade)
        {
            object color = "#FFFFFF";
            var name = Enum.GetName (typeof (BuildingGrade.Enum), grade);
            if (!string.IsNullOrEmpty(name) && gradeColors.TryGetValue (name, out color))
                return color.ToString ();

            return color.ToString ();
        }

        string GetWeaponList (Raid raid)
        {
            string weaponsNameText = string.Empty;
            var weaponsList = new List<string> ();
            foreach (var kvp in raid.weapons) {
                var weaponsItem = ItemManager.FindItemDefinition (kvp.Key);
                if (weaponsItem is ItemDefinition)
                    weaponsList.Add (kvp.Value + " x " + string.Format ("<color={0}>{1}(s)</color>", GetWeaponColor (weaponsItem.itemid), weaponsItem.displayName.english));
            }

            if (weaponsList.Count > 0)
                weaponsNameText = string.Join (", ", weaponsList.ToArray ());

            return weaponsNameText;
        }

        string GetDestroyedList (Raid raid)
        {
            string destroyedText = string.Empty;
            var destroyedList = new List<string> ();
            foreach (var kvp in raid.blocksDestroyed)
                destroyedList.Add (kvp.Value + " x " + string.Format ("<color={0}>{1}(s)</color>", GetGradeColor ((int)kvp.Key), Enum.GetName (typeof (BuildingGrade.Enum), kvp.Key) + " Structure"));

            foreach (var kvp in raid.entitiesDestroyed)
                destroyedList.Add (kvp.Value + " x " + string.Format ("<color={0}>{1}(s)</color>", "white", kvp.Key));

            if (destroyedList.Count > 0)
                destroyedText = string.Join (", ", destroyedList.ToArray ());

            return destroyedText;
        }

        void AnnounceRaid (Raid raid, AnnouncementType type)
        {
            var format = GetAnnouncementMsg (type);
            if (string.IsNullOrEmpty (format)) {
                return;
            }
            var initiatorClanTag = string.Empty;
            var victimClanTag = string.Empty;
            var initiatorText = raid.Initiator.Name;
            var victimText = raid.Victim.Name;
            var initiatorClanText = string.Empty;
            var victimClanText = string.Empty;
            var initiatorClanMatesText = "1";
            var victimClanMatesText = "1";

            if (announceClan) {
                initiatorClanTag = Clans.Call<string> ("GetClanOf", raid.initiator);
                victimClanTag = Clans.Call<string> ("GetClanOf", raid.victim);

                if (initiatorClanTag != null) {
                    initiatorClanText = string.Format ("<color={0}>{1}</color>", announceClanColor, initiatorClanTag);
                    initiatorClanMatesText = GetClanMembers (initiatorClanTag).Count.ToString ();
                }

                if (victimClanTag != null) {
                    victimClanText = string.Format ("<color={0}>{1}</color>", announceClanColor, victimClanTag);
                    victimClanMatesText = GetClanMembers (victimClanTag).Count.ToString ();
                }
            }

            initiatorText = string.Format ("<color={0}>{1}</color>", announceNameColor, initiatorText);
            victimText = string.Format ("<color={0}>{1}</color>", announceNameColor, victimText);

            var announcePrefix = string.Format ("<color={0}>{1}</color>", announcePrefixColor, GetMsg ("Announce: Prefix"));

            var weaponsNameText = GetWeaponList (raid);
            var destroyedNameText = GetMsg ("Target: Nothing");
            if (raid.blocksDestroyed.Count > 0 || raid.entitiesDestroyed.Count > 0)
                destroyedNameText = GetDestroyedList (raid);

            var message = Format (format,
                              initiator => initiatorText,
                              victim => victimText,
                              initiatorClanMates => initiatorClanMatesText,
                              victimClanMates => victimClanMatesText,
                              initiatorClan => initiatorClanText,
                              victimClan => victimClanText,
                              weaponList => weaponsNameText,
                              destroyedList => destroyedNameText
                          );

            if (type == AnnouncementType.Slack_Start || type == AnnouncementType.Slack_End) {
                Slack?.Call (slackType, StripTags (message), raid.Initiator);
            } else if (type == AnnouncementType.Discord_Start || type == AnnouncementType.Discord_End) {
                Discord?.Call ("SendMessage", StripTags (message));
            } else {
                if (printToLog)
                    PrintToConsole (message);

                if (announceGlobal) {
                    if (announceRadius > 0)
                        BroadcastLocal (announcePrefix, message, raid.firstDamage);
                    else
                        BroadcastGlobal (announcePrefix, message);
                } else {
                    if (announceClan && raid.victim.IsSteamId ()) {
                        string tag = Clans.Call<string> ("GetClanOf", raid.victim);

                        var clan = GetClanMembers (tag);

                        if (clan.Count > 0)
                            foreach (string memberId in clan)
                                if (!string.IsNullOrEmpty (memberId))
                                    BroadcastToPlayer (announcePrefix, memberId, message);
                    }
                    if (announceToVictims)
                        foreach (ulong owner in raid.blockOwners)
                            BroadcastToPlayer (announcePrefix, owner.ToString (), message);
                }

                if (announceToLustyMap && lustyMapDuration > 0 && raid.firstDamage != Vector3.zero) {
                    var obj = LustyMap?.Call ("AddMarker", raid.firstDamage.x, raid.firstDamage.z, raid.start.ToString (), lustyMapIcon);
                    if (obj is bool && (bool)obj == true) {
                        timer.In (lustyMapDuration, delegate () {
                            LustyMap?.Call ("RemoveMarker", raid.start.ToString ());
                        });
                    }
                }
            }
        }

        void BroadcastGlobal (string prefix, string message)
        {
            rust.BroadcastChat (prefix, message, announceIcon);
        }

        void BroadcastLocal (string prefix, string message, Vector3 position)
        {
            foreach (var player in BasePlayer.activePlayerList)
                if (player.Distance (position) <= announceRadius)
                    player.ChatMessage (prefix + ": " + message);
        }

        void BroadcastToPlayer (string prefix, string userID, string message)
        {
            var player = BasePlayer.Find (userID);

            if (player is BasePlayer)
                player.ChatMessage (prefix + ": " + message);
        }

        void OnPlayerAttack (BasePlayer attacker, HitInfo hitInfo)
        {
            if (!(hitInfo.HitEntity is BasePlayer))
                return;
            if (hitInfo.damageTypes.GetMajorityDamageType () != DamageType.Explosion)
                return;

            var victim = (hitInfo.HitEntity as BasePlayer);

            if (victim != null) {
                var victimBehavior = victim.GetComponent<RaidBehavior> ();
                var attackerBehavior = attacker.GetComponent<RaidBehavior> ();

                if (victimBehavior != null && victimBehavior.raid != null && (attackerBehavior == null || (attackerBehavior != null && attackerBehavior.raid == null)))
                    victimBehavior.raid.Participate (attacker);
            }
        }

        public List<string> GetClanMembers (string tag)
        {
            var members = new List<string> ();

            if (string.IsNullOrEmpty (tag))
                return members;

            var clan = Clans.Call<JObject> ("GetClan", tag);

            if (clan == null)
                return members;

            foreach (string memberid in clan ["members"])
                members.Add (memberid);

            return members;
        }

        public List<string> GetOnlineClanMembers (string tag)
        {
            var allMembers = GetClanMembers (tag);

            var onlineMembers = new List<string> ();
            if (allMembers == null) {
                return onlineMembers;
            }

            foreach (string mid in allMembers) {
                var p = covalence.Players.FindPlayerById (mid);
                if (p is IPlayer && p.IsConnected)
                    onlineMembers.Add (mid);
            }

            return onlineMembers;
        }

        public List<Raid> GetRaids ()
        {
            var raids = new List<Raid> ();
            var objects = GameObject.FindObjectsOfType (typeof (RaidBehavior));
            if (objects != null)
                foreach (var gameObj in objects) {
                    var raidBehavior = gameObj as RaidBehavior;
                    if (raidBehavior.raid != null)
                        raids.Add (raidBehavior.raid);
                }

            return raids;
        }

        public bool IsEntityRaidable (BaseCombatEntity entity)
        {
            if (entity is BuildingBlock) {
                return true;
            }
            var result = false;
            var prefabName = entity.ShortPrefabName;
            if (_raidableCache.TryGetValue (prefabName, out result))
                return result;

            foreach (string p in prefabs) {
                if (prefabName.IndexOf (p, StringComparison.InvariantCultureIgnoreCase) != -1) {
                    result = true;
                    break;
                }
            }

            _raidableCache.Add (prefabName, result);

            return result;
        }

        #endregion

        #region Helper Methods

        string Format (string str, params Expression<Func<string, object>> [] args)
        {
            var sb = new StringBuilder (str);

            if (args.Length > 0) {
                Dictionary<string, object> parameters = new Dictionary<string, object> ();

                foreach (Expression<Func<string, object>> e in args) {
                    if (e == null)
                        continue;
                    if (e.Parameters == null)
                        continue;
                    if (e.Parameters.Count == 0)
                        continue;
                    var func = e.Compile ();
                    if (func == null)
                        continue;

                    var name = e.Parameters [0].Name;
                    if (name == null)
                        continue;
                    var result = func.Invoke (name);
                    if (result == null)
                        continue;
                    parameters.Add ("{" + name + "}", result);
                }

                foreach (var kv in parameters) {
                    if (kv.Key != null && kv.Value != null) {
                        sb.Replace (kv.Key, kv.Value != null ? kv.Value.ToString () : "");
                    }
                }
            }

            return sb.ToString ();
        }

        T GetConfig<T> (string key, T defaultValue)
        {
            try {
                var val = Config [key];
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
            } catch (Exception ex) {
                PrintWarning ("Invalid config value: " + key + " (" + ex.Message + ")");
                return defaultValue;
            }
        }

        T GetConfig<T> (string name, string name2, T defaultValue)
        {
            if (Config [name, name2] == null) {
                return defaultValue;
            }

            return (T)Convert.ChangeType (Config [name, name2], typeof (T));
        }

        string GetMsg (string key, BasePlayer player = null)
        {
            return lang.GetMessage (key, this, player == null ? null : player.UserIDString);
        }

        public static List<BasePlayer> Sort (Vector3 position, List<BasePlayer> hits)
        {
            return hits.OrderBy (i => i.Distance (position)).ToList ();
        }

        bool HasPerm (BasePlayer p, string pe)
        {
            return permission.UserHasPermission (p.userID.ToString (), pe);
        }

        bool HasPerm (string userid, string pe)
        {
            return permission.UserHasPermission (userid, pe);
        }

        class UnityVector3Converter : JsonConverter
        {
            public override void WriteJson (JsonWriter writer, object value, JsonSerializer serializer)
            {
                var vector = (Vector3)value;
                writer.WriteValue ($"{vector.x} {vector.y} {vector.z}");
            }

            public override object ReadJson (JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.String) {
                    var values = reader.Value.ToString ().Trim ().Split (' ');
                    return new Vector3 (Convert.ToSingle (values [0]), Convert.ToSingle (values [1]), Convert.ToSingle (values [2]));
                }
                var o = JObject.Load (reader);
                return new Vector3 (Convert.ToSingle (o ["x"]), Convert.ToSingle (o ["y"]), Convert.ToSingle (o ["z"]));
            }

            public override bool CanConvert (Type objectType)
            {
                return objectType == typeof (Vector3);
            }
        }

        class DateTimeConverter : JsonConverter
        {
            public override void WriteJson (JsonWriter writer, object value, JsonSerializer serializer)
            {
                var datetime = (DateTime)value;
                writer.WriteValue (datetime.ToBinary ().ToString ());
            }

            public override object ReadJson (JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                long binaryDate;
                if (reader.TokenType == JsonToken.String && long.TryParse (reader.Value.ToString (), out binaryDate))
                    return DateTime.FromBinary (binaryDate);

                return DateTime.MinValue;
            }

            public override bool CanConvert (Type objectType)
            {
                return objectType == typeof (DateTime);
            }
        }

        public string StripTags (string source)
        {
            return _htmlRegex.Replace (source, string.Empty);
        }

        #endregion

        #region HelpText

        void SendHelpText (BasePlayer player)
        {
            if (HasPerm (player, "raidnotes.inspect")) {
                var sb = new StringBuilder ()
                   .Append ("RaidNotes\n");

                if (logHours > 0) {
                    if (player.net.connection.authLevel > 0) {
                        sb.Append ("  ").Append ("<color=\"#ffd479\">/raids</color> - Detect any raiding activity up to " + logHours + " hours ago").Append ("\n");
                    } else {
                        sb.Append ("  ").Append ("<color=\"#ffd479\">/raids</color> - Detect raiding activity against structures you own up to " + logHours + " hours ago").Append ("\n");
                    }
                }

                player.ChatMessage (sb.ToString ());
            }
        }

        #endregion
    }
}