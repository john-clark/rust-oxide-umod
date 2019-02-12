using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Rust;
using System.Linq;
using System.Globalization;
using Facepunch;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Sleeper Mark", "Ts3Hosting", "1.0.23")]
    [Description("Create a event to mark sleepers to be killed by players")]
    public class SleeperMark : RustPlugin
    {
        [PluginReference]
        Plugin GUIAnnouncements, ServerRewards;
        Dictionary<ulong, HitInfo> LastWounded = new Dictionary<ulong, HitInfo>();

        static HashSet<PlayerData> LoadedPlayerData = new HashSet<PlayerData>();
        List<UIObject> UsedUI = new List<UIObject>();



        PlayerEntity pcdData;
        private DynamicConfigFile PCDDATA;
        private bool Changed;
        public bool debug = false;
        private int cooldownTime = 120;
        private bool useRewards = false;
        private bool useguiAnnounce = false;
        private bool useAirdrop = true;
        private bool useBloon = false;
        private int LastSeenSeconds = 259200;
        private int reward = 250;
        public bool EventStarted = false;
        public bool hitBalloon = false;
        public bool hitPlayer = false;
        public bool lockcrate = false;
        public bool usedirection = true;
        private int locktime = 120;
        private int CrateAmount;
        public uint BloonNetID;
        public BasePlayer eventplayer;
        public LockedByEntCrate locked;
        public bool Effects = true;
        float max = TerrainMeta.Size.x / 2;
        const string bloonprefab = "assets/prefabs/deployable/hot air balloon/hotairballoon.prefab";
        const string c4Explosion = "assets/prefabs/npc/patrol helicopter/effects/rocket_explosion.prefab";
        const string boom = "assets/prefabs/npc/patrol helicopter/effects/heli_explosion.prefab";
        const string crates = "assets/prefabs/npc/patrol helicopter/heli_crate.prefab";
        const string supply = "assets/prefabs/misc/supply drop/supply_drop.prefab";
        const string fireball = "assets/bundled/prefabs/oilfireballsmall.prefab";
        private HashSet<LockedByEntCrate> lockedCrates = new HashSet<LockedByEntCrate>();
        public StorageContainer fuelTank;
        string colorTextMsg;
        string colorTextMsgB;



        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {

                ["Location"] = "Kill the sleeper <color=green>{0}</color> at Loc X= <color=red>{1}</color>, Z= <color=red>{2}</color> and get a reward!",
                ["noperm"] = "You do not have permissions to use this command!",
                ["found"] = "<color=green> {0}</color> has been found by <color=red>{1}</color>!",
                ["dead"] = "<color=green> {0}</color> has been killed by <color=red>{1}</color> and he received <color=orange>{2}</color> RP for his service!",
                ["msgNorth"] = "North",
                ["msgNorthEast"] = "NorthEast",
                ["msgEast"] = "East",
                ["msgSouthEast"] = "SouthEast",
                ["msgSouth"] = "South",
                ["msgSouthWest"] = "SouthWest",
                ["msgWest"] = "West",
                ["msgNorthWest"] = "NorthWest",
                ["NoRewards"] = "<color=green> {0}</color> has been killed by <color=red>{1}</color>",
                ["noSleepers"] = "No sleepers have been sleeping to long to start event!",
                ["eventstarted"] = "There is one event already started!",
                ["reset"] = "Sleep data reset events!",
                ["NoEvent"] = "There is no event running!",
                ["CoolDown"] = "You are on cooldown, Try again in {0} seconds!",
                ["LocationBloon"] = "Kill the sleeper <color=green>{0}</color> at Loc X= <color=red>{1}</color>, Z= <color=red>{2}</color> He is flying around in the bloon and get a reward!",
                ["LocationBloonDirection"] = "Kill the sleeper <color=green>{2}</color> He is flying around in the bloon at Loc X= <color=red>{3}</color>, Z= <color=red>{4}</color> and get a reward!\nThey are  <color=yellow>{0:F0}m</color> away from you at direction <color=yellow>{1}</color>",
                ["LocationDirection"] = "Kill the sleeper <color=green>{2}</color> at Loc X= <color=red>{3}</color>, Z= <color=red>{4}</color> and get a reward!\nThey are  <color=yellow>{0:F0}m</color> away from you at direction <color=yellow>{1}</color>",
                ["help"] = "/sleeper commands are - event - score - topscore -",
            }, this);
        }

        void Unload()
        {
            EventEnd();
            foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
            {
                foreach (var ui in UsedUI)
                    ui.Destroy(player);
            }


        }
        void Init()
        {
            PCDDATA = Interface.Oxide.DataFileSystem.GetFile("SleeperMark/SleeperMark_Player");
            LoadData();
            LoadVariables();
            RegisterPermissions();
            resetdataboot();
        }
        void OnServerInitialized()
        {
            CheckDependencies();
            lockedCrates = new HashSet<LockedByEntCrate>(GameObject.FindObjectsOfType<LockedByEntCrate>());
            foreach (var player in BasePlayer.activePlayerList.ToList())
            {
                PlayerData.TryLoad(player);
                LoadSleepers();
            }
        }
        private void RegisterPermissions()
        {
            permission.RegisterPermission("SleeperMark.admin", this);
            permission.RegisterPermission("SleeperMark.ignore", this);
            permission.RegisterPermission("SleeperMark.use", this);
        }
        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        void LoadVariables()
        {
            debug = Convert.ToBoolean(GetConfig("SETTINGS", "Debug", false));
            useRewards = Convert.ToBoolean(GetConfig("SETTINGS", "useRewards", false));
            LastSeenSeconds = Convert.ToInt32(GetConfig("SETTINGS", "LastSeenSeconds", 259200));
            reward = Convert.ToInt32(GetConfig("SETTINGS", "reward", 250));
            useguiAnnounce = Convert.ToBoolean(GetConfig("SETTINGS", "usegui", false));
            useBloon = Convert.ToBoolean(GetConfig("SETTINGS", "useBloon", false));
            usedirection = Convert.ToBoolean(GetConfig("Generic", "Broadcast direction in chat messages", true));
            useAirdrop = Convert.ToBoolean(GetConfig("LootDrops", "useLootCrates", true));
            lockcrate = Convert.ToBoolean(GetConfig("LootDrops", "LockLootCrates", true));
            locktime = Convert.ToInt32(GetConfig("LootDrops", "LockTimeCrate", 120));
            CrateAmount = Convert.ToInt32(GetConfig("LootDrops", "CrateAmount", 3));
            cooldownTime = Convert.ToInt32(GetConfig("SETTINGS", "cooldownTime", 120));
            colorTextMsg = Convert.ToString(GetConfig("Generic", "UI Broadcast messages color", "yellow"));
            colorTextMsgB = Convert.ToString(GetConfig("Generic", "UI Broadcast messages Background color", "purple"));
            Puts("Config Loaded");
            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new configuration file!");
            LoadVariables();
        }

        private void CheckDependencies()
        {
            if (ServerRewards == null)
            {
                PrintWarning($"ServerRewards could not be found! Disabling RP feature");
                useRewards = false;
            }
            if (ServerRewards && !useRewards)
            {
                Puts("ServerReward found but not enabled in config.");
            }
            if (GUIAnnouncements == null)
            {
                PrintWarning($"guiAnnounce could not be found! Disabling guiAnnounce feature");
                useguiAnnounce = false;
            }
            if (GUIAnnouncements && !useguiAnnounce)
            {
                Puts("GUIAnnouncements found but not enabled in config.");
            }
        }

        void LoadData()
        {
            try
            {
                pcdData = Interface.Oxide.DataFileSystem.ReadObject<PlayerEntity>("SleeperMark/SleeperMark_Player");
            }
            catch
            {
                Puts("Couldn't load Sleep data, creating new Playerfile");
                pcdData = new PlayerEntity();
            }
        }
        class PlayerEntity
        {
            public Dictionary<ulong, PCDInfo> pEntity = new Dictionary<ulong, PCDInfo>();


            public PlayerEntity() { }
        }
        class PCDInfo
        {
            public long LogOutDay;
            public float LocEntityX;
            public float LocEntityY;
            public float LocEntityZ;
            public uint BloonID;
            public long Cooldown;
            public PCDInfo() { }
            public PCDInfo(long cd)
            {
            }
        }
        void SaveData()
        {
            PCDDATA.WriteObject(pcdData);
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            ulong playerId = player.userID;
            double timeStamp = GrabCurrentTime();
            if (!pcdData.pEntity.ContainsKey(playerId))
            {

                pcdData.pEntity.Add(playerId, new PCDInfo());
                pcdData.pEntity[playerId].LogOutDay = (long)timeStamp;
                pcdData.pEntity[playerId].LocEntityX = player.transform.position.x;
                pcdData.pEntity[playerId].LocEntityY = player.transform.position.y;
                pcdData.pEntity[playerId].LocEntityZ = player.transform.position.z;
                SaveData();
            }

            else
            {
                pcdData.pEntity[playerId].LogOutDay = (long)timeStamp;
                pcdData.pEntity[playerId].LocEntityX = player.transform.position.x;
                pcdData.pEntity[playerId].LocEntityY = player.transform.position.y;
                pcdData.pEntity[playerId].LocEntityZ = player.transform.position.z;
                SaveData();
            }
        }

        readonly Dictionary<ulong, Timer> timers = new Dictionary<ulong, Timer>();
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (info == null) return;
            if (info.Initiator == null) return;
            if (entity == null) return;
            if (!EventStarted) return;


            if (!hitBalloon && entity is HotAirBalloon && EventStarted && entity.net.ID == BloonNetID && (info.Initiator is BasePlayer))
            {

                var controller = entity.GetComponent<HotAirBalloon>();
                hitBalloon = true;

                controller.SetFlag(BaseEntity.Flags.On, false);
                controller.windForce = 1000f;

                if (Effects)
                {

                    Effect.server.Run(boom, entity.transform.position + Vector3.up * 6);
                    timer.Repeat(2, 10, () =>
                                   {
                                       if (entity != null && EventStarted) Effect.server.Run(c4Explosion, entity.transform.position + Vector3.up * 6);
                                   });

                }
                timer.Once(160, () =>
                {
                    hitBalloon = false;
                    controller.SetFlag(BaseEntity.Flags.On, true);
                    controller.windForce = 0f;
                });
            }

            if (!entity || !(entity is BasePlayer) || !(info.Initiator is BasePlayer) || info.Initiator is NPCMurderer || info.Initiator is BaseNpc || info.Initiator is NPCPlayerApex || entity is NPCMurderer || entity is BaseNpc || entity is NPCPlayerApex || entity is HTNPlayer || info.Initiator is HTNPlayer)
                return;
            BasePlayer player = entity as BasePlayer;
            var attacker = info.Initiator as BasePlayer;
            if (!player.userID.IsSteamId() || !attacker.userID.IsSteamId()) return;
            if (!player.IsSleeping())
                return;
            ulong playerId = player.userID;
            double timeStamp = GrabCurrentTime();
            var t = pcdData.pEntity[playerId].LogOutDay + LastSeenSeconds;
            if (player.IsSleeping() && (playerId == eventplayer.userID) && (timeStamp > t) && (!hitPlayer) && (EventStarted))
            {
                PrintToChat(string.Format(lang.GetMessage("found", this), player.displayName, attacker.displayName));
                hitPlayer = true;
                return;
            }

        }

        void OnPlayerInit(BasePlayer player)
        {
            PlayerData.TryLoad(player);
        }




        void rewardplayer(BasePlayer player, BasePlayer attacker)
        {
                if (debug) Puts("Debug running rewards Start...");
                PlayerData victimData = PlayerData.Find(player);
                PlayerData attackerData = PlayerData.Find(attacker);
                if (victimData != null || attackerData != null)
                 {
                if (debug) Puts("Debug running Scoreboard under rewards Start...");
                    attackerData.kills++;
                    victimData.deaths++;
                    attackerData.Save();
                    victimData.Save();
                }
                if (useAirdrop && attacker != null) 
                {
                    SpawnLoot(CrateAmount, attacker);
                }
                if (useRewards)
                {
                    if (debug) Puts("Running Use Rewards");
                    PrintToChat(string.Format(lang.GetMessage("dead", this), player.displayName, attacker.displayName, reward));
                    Effect.server.Run("assets/bundled/prefabs/fx/explosions/explosion_01.prefab", attacker.transform.position);
                    EventEnd();
                    ServerRewards?.Call("AddPoints", attacker.userID, (int)reward);

                }
                else
                {
                    if (debug) Puts("Running Not Use Rewards");
                    PrintToChat(string.Format(lang.GetMessage("NoRewards", this), player.displayName, attacker.displayName));
                    Effect.server.Run("assets/bundled/prefabs/fx/explosions/explosion_01.prefab", attacker.transform.position);
                    EventEnd();                  
                }

            }






        void OnEntityDeath(BaseCombatEntity victimEntity, HitInfo info)
        {
            if (info == null) return;
            if (info.Initiator == null) return;
            if (victimEntity == null) return;
            if (!EventStarted) return;
            if (info.Initiator is NPCMurderer || info.Initiator is BaseNpc || info.Initiator is NPCPlayerApex || victimEntity is NPCMurderer || victimEntity is BaseNpc || victimEntity is NPCPlayerApex || victimEntity is HTNPlayer || info.Initiator is HTNPlayer) return;
            if (debug) Puts("Running OnEntityDeath");
            if ((victimEntity is BasePlayer) && !(info.Initiator is BasePlayer))
            {
                BasePlayer player4 = victimEntity as BasePlayer;
            if (player4.userID == eventplayer.userID) EventEnd();
            if (debug) Puts("Player killed By Unknown.");
            }


            if ((victimEntity is HotAirBalloon) && !(info.Initiator is BasePlayer) && victimEntity.net.ID == BloonNetID)
            {
                EventEnd();
            if (debug) Puts("Bloon Destroyed By Unknown");
            }

            if ((victimEntity is HotAirBalloon) && (info.Initiator is BasePlayer) && victimEntity.net.ID == BloonNetID)
            {
                if (eventplayer != null && eventplayer.IsSleeping())
                eventplayer.Hurt(1000);
                rewardplayer(eventplayer, (BasePlayer)info.Initiator);
                return;
            }
            if (!victimEntity || !(victimEntity is BasePlayer) || !(info.Initiator is BasePlayer) || info.Initiator is NPCMurderer || info.Initiator is BaseNpc || info.Initiator is NPCPlayerApex || victimEntity is NPCMurderer || victimEntity is BaseNpc || victimEntity is NPCPlayerApex || victimEntity is HTNPlayer || info.Initiator is HTNPlayer)
                return;
            BasePlayer player = victimEntity as BasePlayer;
            if (!player.IsSleeping())
                return;
            ulong playerId = player.userID;
            double timeStamp = GrabCurrentTime();
            var attacker = info.Initiator as BasePlayer;
            if (!player.userID.IsSteamId() || !attacker.userID.IsSteamId()) return;

            var t = pcdData.pEntity[playerId].LogOutDay + LastSeenSeconds;
            var bloonEntity = BaseNetworkable.serverEntities.Find(BloonNetID) as BaseEntity;
            if (eventplayer.userID != playerId) return;
            if (player.IsSleeping() && (pcdData.pEntity.ContainsKey(playerId)) && (timeStamp > t) && (EventStarted))
            {
            rewardplayer(eventplayer, attacker);
            }
        }

        static double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

        [ConsoleCommand("sleepermark")]
        void cmdRun(ConsoleSystem.Arg arg)
        {
            FindPositionsleep(null);
        }

        [ChatCommand("sleepermark")]
        private void sleepermark(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, "SleeperMark.admin"))
            {
                SendReply(player, string.Format(lang.GetMessage("noperm", this, player.UserIDString)));
                return;
            }
            else
            {
                FindPositionsleep(player);
            }
        }

        [ChatCommand("sleeper")]
        private void sleeperevent(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "SleeperMark.use"))
            {
                SendReply(player, string.Format(lang.GetMessage("noperm", this, player.UserIDString)));
                return;
            }
            else if (args.Length == 0)
            { 
                    SendReply(player, string.Format(lang.GetMessage("help", this, player.UserIDString)));
                    return;
            }
            else if (args[0] == "topscore")
            { 
                    DrawKDRWindow(player);
                    return;
            }
            else if (args[0] == "score")
            { 
                    GetCurrentStats(player);
                    return;
            }
            else if (EventStarted && args[0] == "event")
            {                   
                    var loc = new Vector3(eventplayer.transform.position.x, 20, eventplayer.transform.position.z);
                    double timeStamp = GrabCurrentTime();
                    var cdTime = pcdData.pEntity[player.userID].Cooldown; // Get the cooldown time of the User
                    if (cdTime > timeStamp)
                    {
                        SendReply(player, string.Format(lang.GetMessage("CoolDown", this, player.UserIDString), (int)(cdTime - timeStamp)));
                        return;
                    }
                    if (!useBloon && !usedirection)
                    {
                    SendReply(player, string.Format(lang.GetMessage("Location", this, player.UserIDString), eventplayer.displayName, eventplayer.transform.position.x, eventplayer.transform.position.z));
                    }
                    else if (!useBloon && usedirection)
                    {
                    var msg1 = string.Format(lang.GetMessage("LocationDirection", this, player.UserIDString), Vector3.Distance(player.transform.position, loc), GetDirectionAngle(Quaternion.LookRotation((loc - player.eyes.position).normalized).eulerAngles.y, player.UserIDString), eventplayer.displayName, eventplayer.transform.position.x, eventplayer.transform.position.z);
                    MessageToPlayer(player, msg1);
                    }
                    else if (useBloon && !usedirection)
                    {    
                    SendReply(player, string.Format(lang.GetMessage("LocationBloon", this, player.UserIDString), eventplayer.displayName, eventplayer.transform.position.x, eventplayer.transform.position.z));
                    }
                    else
                    {
                    var msg = string.Format(lang.GetMessage("LocationBloonDirection", this, player.UserIDString), Vector3.Distance(player.transform.position, loc), GetDirectionAngle(Quaternion.LookRotation((loc - player.eyes.position).normalized).eulerAngles.y, player.UserIDString), eventplayer.displayName, eventplayer.transform.position.x, eventplayer.transform.position.z);
                    MessageToPlayer(player, msg); 
                    }
                    pcdData.pEntity[player.userID].Cooldown = (long)timeStamp + cooldownTime;
                    SaveData();
                }
                else if (!EventStarted && args[0] == "event")
                {
                    SendReply(player, string.Format(lang.GetMessage("NoEvent", this, player.UserIDString)));

                }
            }
        


        private void SpawnLoot(int amount, BaseEntity EntitySpawn)
        {
            if (amount == 0)
                return;

            for (int j = 0; j < amount; j++)
            {
                Vector3 randsphere = UnityEngine.Random.onUnitSphere;
                Vector3 entpos = (EntitySpawn.transform.position + new Vector3(0f, 1.5f, 0f)) + (randsphere * UnityEngine.Random.Range(-2f, 3f));

                var ent = crates;
                BaseEntity crate = GameManager.server.CreateEntity(ent, entpos, Quaternion.LookRotation(randsphere), true);
                if (crate == null) return;
                crate.Spawn();

                Rigidbody rigidbody;

                rigidbody = crate.gameObject.AddComponent<Rigidbody>();
                FireBall fireBall = GameManager.server.CreateEntity(fireball) as FireBall;
                if (fireBall == null) return;
                //SpawnFireball(crate);
                if (rigidbody != null)
                {
                    rigidbody.isKinematic = false;
                    rigidbody.useGravity = true;
                    rigidbody.mass = 0.55f;
                    rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                    rigidbody.drag = 0.25f;
                    rigidbody.angularDrag = 0.1f;
                    rigidbody.AddForce((EntitySpawn.transform.forward + (EntitySpawn.transform.right * UnityEngine.Random.Range(-5f, 5f))) * 50);
                    Puts("Calling for fireballs.");
                    fireBall.Spawn();
                    fireBall.SetParent(crate);

                    fireBall.GetComponent<Rigidbody>().isKinematic = true;
                    fireBall.GetComponent<Collider>().enabled = false;

                    fireBall.Invoke(fireBall.Extinguish, locktime);

                    if (lockcrate)
                    {
                        var cratelock = (crate as LockedByEntCrate);
                        cratelock.SetLocked(true);
                        locked = cratelock;
                        timer.Once(locktime, () => { UnlockCrate(cratelock); locked = null;  });

                    }
                }

            }
        }

        private void UnlockCrate(LockedByEntCrate crate)
        {
            if (debug) Puts("Running UnlockCrate");
            if (crate == null || (crate?.IsDestroyed ?? true)) return;
            var lockingEnt = (crate?.lockingEnt != null) ? crate.lockingEnt.GetComponent<FireBall>() : null;
            if (lockingEnt != null && !lockingEnt.IsDestroyed)
            {
                lockingEnt.enableSaving = false; //again trying to fix issue with savelist
                lockingEnt.CancelInvoke(lockingEnt.Extinguish);
                lockingEnt.Invoke(lockingEnt.Extinguish, 30f);
            }
            crate.CancelInvoke(crate.Think);
            crate.SetLocked(false);
            crate.lockingEnt = null;
        }


        [ChatCommand("sleepermarkfind")]
        private void test(BasePlayer player)
        {
            if (debug) Puts("Running sleepermarkfind");
            if (!permission.UserHasPermission(player.UserIDString, "SleeperMark.admin"))
            {
                SendReply(player, string.Format(lang.GetMessage("noperm", this, player.UserIDString)));
                return;
            }
            if (permission.UserHasPermission(player.UserIDString, "SleeperMark.admin"))
            {
                FindPositionsleepsetup();
            }
        }
        private BaseEntity InstantiateEntity(string type, Vector3 position, Quaternion rotation)
        {
            var gameObject = Instantiate.GameObject(GameManager.server.FindPrefab(type), position, rotation);
            gameObject.name = type;

            SceneManager.MoveGameObjectToScene(gameObject, Rust.Server.EntityScene);

            UnityEngine.Object.Destroy(gameObject.GetComponent<Spawnable>());

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            BaseEntity component = gameObject.GetComponent<BaseEntity>();
            return component;
        }


        private void EventEnd()
        {
            if (useBloon && EventStarted)
            {
                var targetEntity = BaseNetworkable.serverEntities.Find(BloonNetID) as BaseEntity;                
                var player = BasePlayer.sleepingPlayerList.ToList().Find(o => o.userID == eventplayer.userID);

                if (targetEntity != null)
                {      
                    targetEntity.Kill();
                }


        if(player == null)
            {
                    player = BasePlayer.activePlayerList.ToList().Find(o => o.userID == eventplayer.userID);
            }
                if (player != null)
                {
                    player.Teleport(new Vector3(pcdData.pEntity[eventplayer.userID].LocEntityX, pcdData.pEntity[eventplayer.userID].LocEntityY, pcdData.pEntity[eventplayer.userID].LocEntityZ));  
                    
                }   
                    hitPlayer = false;
                    EventStarted = false;
                    hitBalloon = false;
                    eventplayer = null; 
                    BloonNetID = 0;        
            }
        }



        private void FindPositionsleep(BasePlayer player1)
        {
            var onetimer = timer.Repeat(2.5f, 1, () => { SendReply(player1, string.Format(lang.GetMessage("noSleepers", this))); ; });
            if (EventStarted)
            {
                SendReply(player1, string.Format(lang.GetMessage("eventstarted", this)));
                onetimer.Destroy();
                if (eventplayer == null) EventEnd();
                if (!useBloon && eventplayer != null) PrintToChat(string.Format(lang.GetMessage("Location", this), eventplayer.displayName, eventplayer.transform.position.x, eventplayer.transform.position.z));
                if (useBloon && eventplayer != null) PrintToChat(string.Format(lang.GetMessage("LocationBloon", this), eventplayer.displayName, eventplayer.transform.position.x, eventplayer.transform.position.z));
                return;
            }




            
            foreach (BasePlayer player in BasePlayer.sleepingPlayerList.ToList())
            {
                if (EventStarted && player.userID == eventplayer.userID) return;
                double timeStamp = GrabCurrentTime();
                ulong playerId = player.userID;
                FindPositionsleepsetup();

                if (!pcdData.pEntity.ContainsKey(playerId)) return;

                var t = pcdData.pEntity[playerId].LogOutDay + LastSeenSeconds;
                var a = pcdData.pEntity[playerId].LocEntityX;
                var b = pcdData.pEntity[playerId].LocEntityY;
                var c = pcdData.pEntity[playerId].LocEntityZ;

                if (pcdData.pEntity.ContainsKey(playerId) && (timeStamp > t) && !useBloon && !permission.UserHasPermission(player.UserIDString, "SleeperMark.ignore"))
                {
                    Effect.server.Run("assets/bundled/prefabs/fx/explosions/explosion_01.prefab", player.transform.position);
                    SaveData();
                    EventStarted = true;
                    eventplayer = player;
                    if (useguiAnnounce) MessageToAllGui(string.Format(lang.GetMessage("Location", this), player.displayName, a, c));
                    NotifyOnEvent(player);
                    onetimer.Destroy();
                    break;
                }
                if (pcdData.pEntity.ContainsKey(playerId) && (timeStamp > t) && useBloon && !permission.UserHasPermission(player.UserIDString, "SleeperMark.ignore"))
                {
                    var playerPos = player.transform.position + new Vector3(0, 250.1f, 0);
                    var playerRot = player.transform.rotation;   
                    BaseEntity entity = InstantiateEntity(bloonprefab, (Vector3)playerPos, playerRot);
                    entity.Spawn();
                    EventStarted = true;
                    var controller = entity.GetComponent<HotAirBalloon>();
                    fuelTank = controller.fuelStorageInstance.Get(true).GetComponent<StorageContainer>();
                    Item item = ItemManager.CreateByItemID(-946369541, 5);
                    item.MoveToContainer(fuelTank.inventory, -1, true);
                    controller.SetFlag(BaseEntity.Flags.Reserved6, true);
                    controller.SetFlag(BaseEntity.Flags.On, true);
                    controller.SetFlag(BaseEntity.Flags.Locked, true);
                    controller.windForce = 0f;
                    controller.inflationLevel = 1f;
                    controller.fuelPerSec = 0f;
                    var playermount = entity.transform.position + new Vector3(0, 0.4f, 0);
                    var x = entity.transform.position.x;
                    var z = entity.transform.position.z;
                    player.transform.position = playermount;
                    BloonNetID = entity.net.ID;
                    eventplayer = player;
                    SaveData();
                    NotifyOnEvent(player);
                    onetimer.Destroy();
                    if (useguiAnnounce) MessageToAllGui(string.Format(lang.GetMessage("LocationBloon", this), player.displayName, x, z));
                    break;
                }

            }

        }


        [ChatCommand("sleepermarkreset")]
        private void resetdata(BasePlayer reset1)
        {
            SendReply(reset1, string.Format(lang.GetMessage("reset", this, reset1.UserIDString)));
            foreach (BasePlayer player in BasePlayer.sleepingPlayerList.ToList())
            {

                ulong playerId = player.userID;
                FindPositionsleepsetup();
                SaveData();
                EventEnd();

            }
        }

        private void resetdataboot()
        {
            foreach (BasePlayer player in BasePlayer.sleepingPlayerList.ToList())
            {

                ulong playerId = player.userID;
                FindPositionsleepsetup();
                SaveData();
                EventStarted = false;
                hitPlayer = false;
                hitBalloon = false;
            }
        }

        private void FindPositionsleepsetup()
        {
            foreach (BasePlayer player in BasePlayer.sleepingPlayerList.ToList())
            {
                ulong playerId = player.userID;
                double timeStamp = GrabCurrentTime();
                if (!pcdData.pEntity.ContainsKey(playerId))
                {
                    pcdData.pEntity.Add(playerId, new PCDInfo());
                    pcdData.pEntity[playerId].LogOutDay = (long)timeStamp;
                    pcdData.pEntity[playerId].LocEntityX = player.transform.position.x;
                    pcdData.pEntity[playerId].LocEntityY = player.transform.position.y;
                    pcdData.pEntity[playerId].LocEntityZ = player.transform.position.z;
                    SaveData();
                    Puts(player.displayName);
                }
            }
        }
        void MessageToAllGui(string message)
        {
            foreach (var player in BasePlayer.activePlayerList.ToList())
            {
                GUIAnnouncements?.Call("CreateAnnouncement", message, colorTextMsgB, colorTextMsg, player);
            }
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            ulong playerId = player.userID;
            double timeStamp = GrabCurrentTime();
            if (!pcdData.pEntity.ContainsKey(playerId))
            {

                pcdData.pEntity.Add(playerId, new PCDInfo());
                SaveData();

            }
            {
                pcdData.pEntity[playerId].LogOutDay = (long)timeStamp;
                pcdData.pEntity[playerId].LocEntityX = player.transform.position.x;
                pcdData.pEntity[playerId].LocEntityY = player.transform.position.y;
                pcdData.pEntity[playerId].LocEntityZ = player.transform.position.z;
                SaveData();
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            ulong playerId = player.userID;
            double timeStamp = GrabCurrentTime();
            PlayerData.TryLoad(player);
            if (!pcdData.pEntity.ContainsKey(playerId))
            {

                pcdData.pEntity.Add(playerId, new PCDInfo());
                SaveData();
            }
           {

                pcdData.pEntity[playerId].LogOutDay = (long)timeStamp;
                pcdData.pEntity[playerId].LocEntityX = player.transform.position.x;
                pcdData.pEntity[playerId].LocEntityY = player.transform.position.y;
                pcdData.pEntity[playerId].LocEntityZ = player.transform.position.z;
                SaveData();
            }




            if (playerId == eventplayer.userID)
            {
                EventEnd();
                EventStarted = false;
                hitPlayer = false;
                hitBalloon = false;
            }
        }


        void MessageToPlayer(BasePlayer player, string message)
        {
            PrintToChat(player, string.Format(message));
        }
        string GetDirectionAngle(float angle, string UserIDString)
        {
            if (angle > 337.5 || angle < 22.5)
                return lang.GetMessage("msgNorth", this, UserIDString);
            else if (angle > 22.5 && angle < 67.5)
                return lang.GetMessage("msgNorthEast", this, UserIDString);
            else if (angle > 67.5 && angle < 112.5)
                return lang.GetMessage("msgEast", this, UserIDString);
            else if (angle > 112.5 && angle < 157.5)
                return lang.GetMessage("msgSouthEast", this, UserIDString);
            else if (angle > 157.5 && angle < 202.5)
                return lang.GetMessage("msgSouth", this, UserIDString);
            else if (angle > 202.5 && angle < 247.5)
                return lang.GetMessage("msgSouthWest", this, UserIDString);
            else if (angle > 247.5 && angle < 292.5)
                return lang.GetMessage("msgWest", this, UserIDString);
            else if (angle > 292.5 && angle < 337.5)
                return lang.GetMessage("msgNorthWest", this, UserIDString);
            return "";
        }
        void NotifyOnEvent(BasePlayer loc)
        {
            foreach (var player in BasePlayer.activePlayerList.ToList())
            {
            if (!useBloon && !usedirection)
            {
            SendReply(player, string.Format(lang.GetMessage("Location", this, player.UserIDString), eventplayer.displayName, eventplayer.transform.position.x, eventplayer.transform.position.z));
            }
            else if (!useBloon && usedirection)
            {
            var msg1 = string.Format(lang.GetMessage("LocationDirection", this, player.UserIDString), Vector3.Distance(player.transform.position, loc.transform.position), GetDirectionAngle(Quaternion.LookRotation((loc.transform.position - player.eyes.position).normalized).eulerAngles.y, player.UserIDString), eventplayer.displayName, eventplayer.transform.position.x, eventplayer.transform.position.z);
            MessageToPlayer(player, msg1); 
            }
                    else if (useBloon && !usedirection)
                    {    
                    SendReply(player, string.Format(lang.GetMessage("LocationBloon", this, player.UserIDString), eventplayer.displayName, eventplayer.transform.position.x, eventplayer.transform.position.z));                       
                    }
                    else if (useBloon && usedirection)
                    {
                    var msg = string.Format(lang.GetMessage("LocationBloonDirection", this, player.UserIDString), Vector3.Distance(player.transform.position, loc.transform.position), GetDirectionAngle(Quaternion.LookRotation((loc.transform.position - player.eyes.position).normalized).eulerAngles.y, player.UserIDString), eventplayer.displayName, eventplayer.transform.position.x, eventplayer.transform.position.z);
                    MessageToPlayer(player, msg); 
               }       
             }
           }

        class PlayerData
        {
            public ulong id;
            public string name;
            public int kills;
            public int deaths;
            internal float KDR => deaths == 0 ? kills : (float)Math.Round(((float)kills) / deaths, 1);

            internal static void TryLoad(BasePlayer player)
            {
                if (Find(player) != null)
                    return;

                PlayerData data = Interface.Oxide.DataFileSystem.ReadObject<PlayerData>($"SleeperMark/gui/{player.userID}");

                if (data == null || data.id == 0)
                {
                    data = new PlayerData
                    {
                        id = player.userID,
                        name = player.displayName
                    };
                }
                else
                    data.Update(player);

                data.Save();
                LoadedPlayerData.Add(data);
            }

            internal void Update(BasePlayer player)
            {
                name = player.displayName;
                Save();
            }

            internal void Save() => Interface.Oxide.DataFileSystem.WriteObject($"SleeperMark/gui/{id}", this, true);
            internal static PlayerData Find(BasePlayer player)
            {

                PlayerData data = LoadedPlayerData.ToList().Find((p) => p.id == player.userID);

                return data;
            }
        }

        // UI Classes - Created by LaserHydra
        class UIColor
        {
            double red;
            double green;
            double blue;
            double alpha;

            public UIColor(double red, double green, double blue, double alpha)
            {
                this.red = red;
                this.green = green;
                this.blue = blue;
                this.alpha = alpha;
            }

            public override string ToString()
            {
                return $"{red.ToString()} {green.ToString()} {blue.ToString()} {alpha.ToString()}";
            }
        }

        class UIObject
        {
            List<object> ui = new List<object>();
            List<string> objectList = new List<string>();

            public UIObject()
            {
            }

            public string RandomString()
            {
                string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
                List<char> charList = chars.ToList();

                string random = "";

                for (int i = 0; i <= UnityEngine.Random.Range(5, 10); i++)
                    random = random + charList[UnityEngine.Random.Range(0, charList.Count - 1)];

                return random;
            }

            public void Draw(BasePlayer player)
            {
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "AddUI", JsonConvert.SerializeObject(ui).Replace("{NEWLINE}", Environment.NewLine));
            }

            public void Destroy(BasePlayer player)
            {
                foreach (string uiName in objectList)
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "DestroyUI", uiName);
            }

            public string AddPanel(string name, double left, double top, double width, double height, UIColor color, bool mouse = false, string parent = "Overlay")
            {
                name = name + RandomString();

                string type = "";
                if (mouse) type = "NeedsCursor";

                ui.Add(new Dictionary<string, object> {
                    {"name", name},
                    {"parent", parent},
                    {"components",
                        new List<object> {
                            new Dictionary<string, string> {
                                {"type", "UnityEngine.UI.Image"},
                                {"color", color.ToString()}
                            },

                            new Dictionary<string, string> {
                                {"type", "RectTransform"},
                                {"anchormin", $"{left.ToString()} {((1 - top) - height).ToString()}"},
                                {"anchormax", $"{(left + width).ToString()} {(1 - top).ToString()}"}
                            },
                            new Dictionary<string, string> {
                                {"type", type}
                            }
                        }
                    }
                });

                objectList.Add(name);
                return name;
            }

            public string AddText(string name, double left, double top, double width, double height, UIColor color, string text, int textsize = 15, string parent = "Overlay", int alignmode = 0)
            {
                name = name + RandomString(); text = text.Replace("\n", "{NEWLINE}"); string align = "";

                switch (alignmode)
                {
                    case 0: { align = "LowerCenter"; break; };
                    case 1: { align = "LowerLeft"; break; };
                    case 2: { align = "LowerRight"; break; };
                    case 3: { align = "MiddleCenter"; break; };
                    case 4: { align = "MiddleLeft"; break; };
                    case 5: { align = "MiddleRight"; break; };
                    case 6: { align = "UpperCenter"; break; };
                    case 7: { align = "UpperLeft"; break; };
                    case 8: { align = "UpperRight"; break; };
                }

                ui.Add(new Dictionary<string, object> {
                    {"name", name},
                    {"parent", parent},
                    {"components",
                        new List<object> {
                            new Dictionary<string, string> {
                                {"type", "UnityEngine.UI.Text"},
                                {"text", text},
                                {"fontSize", textsize.ToString()},
                                {"color", color.ToString()},
                                {"align", align}
                            },
                            new Dictionary<string, string> {
                                {"type", "RectTransform"},
                                {"anchormin", $"{left.ToString()} {((1 - top) - height).ToString()}"},
                                {"anchormax", $"{(left + width).ToString()} {(1 - top).ToString()}"}
                            }
                        }
                    }
                });

                objectList.Add(name);
                return name;
            }

            public string AddButton(string name, double left, double top, double width, double height, UIColor color, string command = "", string parent = "Overlay", string closeUi = "")
            {
                name = name + RandomString();

                ui.Add(new Dictionary<string, object> {
                    {"name", name},
                    {"parent", parent},
                    {"components",
                        new List<object> {
                            new Dictionary<string, string> {
                                {"type", "UnityEngine.UI.Button"},
                                {"close", closeUi},
                                {"command", command},
                                {"color", color.ToString()},
                                {"imagetype", "Tiled"}
                            },

                            new Dictionary<string, string> {
                                {"type", "RectTransform"},
                                {"anchormin", $"{left.ToString()} {((1 - top) - height).ToString()}"},
                                {"anchormax", $"{(left + width).ToString()} {(1 - top).ToString()}"}
                            }
                        }
                    }
                });

                objectList.Add(name);
                return name;
            }

            public string AddImage(string name, double left, double top, double width, double height, UIColor color, string url = "http://oxidemod.org/data/avatars/l/53/53411.jpg?1427487325", string parent = "Overlay")
            {
                ui.Add(new Dictionary<string, object> {
                    {"name", name},
                    {"parent", parent},
                    {"components",
                        new List<object> {
                            new Dictionary<string, string> {
                                {"type", "UnityEngine.UI.Button"},
                                {"sprite", "assets/content/textures/generic/fulltransparent.tga"},
                                {"url", url},
                                {"color", color.ToString()},
                                {"imagetype", "Tiled"}
                            },

                            new Dictionary<string, string> {
                                {"type", "RectTransform"},
                                {"anchormin", $"{left.ToString().Replace(",", ".")} {((1 - top) - height).ToString().Replace(",", ".")}"},
                                {"anchormax", $"{(left + width).ToString().Replace(",", ".")} {(1 - top).ToString().Replace(",", ".")}"}
                            }
                        }
                    }
                });

                objectList.Add(name);
                return name;
            }
        }

        void DrawKDRWindow(BasePlayer player)
        {
            UIObject ui = new UIObject();
            string panel = ui.AddPanel("panel1", 0.0132382892057026, 0.0285714285714286, 0.958248472505092, 0.874285714285714, new UIColor(0, 0, 0, 1), true, "Overlay");
            ui.AddText("label8", 0.675876726886291, 0.248366013071895, 0.272051009564293, 0.718954248366013, new UIColor(1, 1, 1, 1), GetNames(), 24, panel, 7);
            ui.AddText("label7", 0.483528161530287, 0.248366013071895, 0.0563230605738576, 0.718954248366013, new UIColor(1, 1, 1, 1), GetKDRs(), 24, panel, 6);
            ui.AddText("label6", 0.269925611052072, 0.248366013071895, 0.0456960680127524, 0.718954248366013, new UIColor(1, 1, 1, 1), GetDeaths(), 24, panel, 6);
            ui.AddText("label5", 0.0786397449521785, 0.248366013071895, 0.0456960680127524, 0.718954248366013, new UIColor(1, 1, 1, 1), GetTopKills(), 24, panel, 6);
            string close = ui.AddButton("button1", 0.849096705632306, 0.0326797385620915, 0.124335812964931, 0.0871459694989107, new UIColor(1, 0, 0, 1), "", panel, panel);
            ui.AddText("button1_Text", 0, 0, 1, 1, new UIColor(0, 0, 0, 1), "Close", 19, close, 3);
            ui.AddText("label4", 0.470775770456961, 0.163398692810458, 0.0935175345377258, 0.0610021786492375, new UIColor(1, 0, 0, 1), "K/D Ratio", 24, panel, 7);
            ui.AddText("label3", 0.260361317747078, 0.163398692810458, 0.0722635494155154, 0.0610021786492375, new UIColor(1, 0, 0, 1), "Deaths", 24, panel, 7);
            ui.AddText("label2", 0.0786397449521785, 0.163398692810458, 0.0467587672688629, 0.0610021786492375, new UIColor(1, 0, 0, 1), "Kills", 24, panel, 7);
            ui.AddText("label1", 0.675876726886291, 0.163398692810458, 0.125398512221041, 0.0610021786492375, new UIColor(1, 0, 0, 1), "Player Name", 24, panel, 7);

            ui.Draw(player);
            UsedUI.Add(ui);
        }
        private void LoadSleepers()
        {
            foreach (BasePlayer player in BasePlayer.sleepingPlayerList.ToList())
                PlayerData.TryLoad(player);
        }
        string GetTopKills()
        {
            LoadSleepers();
            return string.Join("\n", LoadedPlayerData.OrderByDescending((d) => d.kills).Select((d) => $"{d.kills}").Take(15).ToArray());
        }
        string GetDeaths()
        {
            return string.Join("\n", LoadedPlayerData.OrderByDescending((d) => d.kills).Select((d) => $"{d.deaths}").Take(15).ToArray());
        }
        string GetKDRs()
        {
            return string.Join("\n", LoadedPlayerData.OrderByDescending((d) => d.kills).Select((d) => $"{d.KDR}").Take(15).ToArray());
        }
        string GetNames()
        {
            return string.Join("\n", LoadedPlayerData.OrderByDescending((d) => d.kills).Select((d) => $"{d.name}").Take(15).ToArray());
        }

        void GetCurrentStats(BasePlayer player)
        {
            PlayerData data = Interface.Oxide.DataFileSystem.ReadObject<PlayerData>($"SleeperMark/gui/{player.userID}");
            int kills = data.kills;
            int deaths = data.deaths;
            string playerName = data.name;
            float kdr = data.KDR;

            PrintToChat(player, "<color=red> Player Name : </color>" + $"{playerName}"
                        + "\n" + "<color=lime> Event-Kills : </color>" + $"{kills}"
                        + "\n" + "<color=lime> Event-Deaths : </color>" + $"{deaths}");
        }
    }
}