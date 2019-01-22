using Oxide.Core;
using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Oxide.Core.Plugins;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("HeliControl", "Shady", "1.3.2", ResourceId = 1348)]
    [Description("Tweak various settings of helicopters.")]
    class HeliControl : RustPlugin
    {
        #region Config/Init
        StoredData lootData = new StoredData();
        StoredData2 weaponsData = new StoredData2();
        StoredData3 cooldownData = new StoredData3();
        StoredData4 spawnsData = new StoredData4();
        private float boundary;
        private HashSet<BaseHelicopter> BaseHelicopters = new HashSet<BaseHelicopter>();
        private HashSet<CH47HelicopterAIController> Chinooks = new HashSet<CH47HelicopterAIController>();
        private HashSet<HelicopterDebris> Gibs = new HashSet<HelicopterDebris>();
        private HashSet<FireBall> FireBalls = new HashSet<FireBall>();
        private HashSet<BaseHelicopter> forceCalled = new HashSet<BaseHelicopter>();
        private HashSet<CH47HelicopterAIController> forceCalledCH = new HashSet<CH47HelicopterAIController>();
        private HashSet<LockedByEntCrate> lockedCrates = new HashSet<LockedByEntCrate>();
        private HashSet<HackableLockedCrate> hackLockedCrates = new HashSet<HackableLockedCrate>();
        private Dictionary<BaseHelicopter, int> strafeCount = new Dictionary<BaseHelicopter, int>();
        private FieldInfo tooHotUntil = typeof(HelicopterDebris).GetField("tooHotUntil", (BindingFlags.Instance | BindingFlags.NonPublic));
        private bool init;
        private static System.Random rng = new System.Random(); //used for loot crates
        private readonly int groundLayer = LayerMask.GetMask("Terrain", "World", "Default");
        private readonly string heliPrefab = "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab";
        private readonly string chinookPrefab = "assets/prefabs/npc/ch47/ch47scientists.entity.prefab";


        bool DisableHeli;
        bool DisableDefaultHeliSpawns;
        bool DisableDefaultChinookSpawns;
        bool UseCustomLoot;
        bool DisableGibs;
        bool DisableNapalm;
        bool AutoCallIfExists;
        bool AutoCallIfExistsCH47;
        bool DisableCratesDeath;

        bool HelicopterCanShootWhileDying;
        bool UseCustomHeliSpawns;
        bool UseOldSpawning;
        bool UseOldSpawningCH47;

        float GlobalDamageMultiplier;
        float HeliBulletDamageAmount;
        float MainRotorHealth;
        float TailRotorHealth;
        float BaseHealth;
        float HeliSpeed;
        float HeliStartSpeed;
        float HeliStartLength;
        float HeliAccuracy;
        float TimeBeforeUnlocking;
        float TimeBeforeUnlockingHack;
        float TurretFireRate;
        float TurretburstLength;
        float TurretTimeBetweenBursts;
        float TurretMaxRange;
        float GibsTooHotLength;
        float GibsHealth;
        float TimeBetweenRockets;
        float MinSpawnTime;
        float MinSpawnTimeCH47;
        float MaxSpawnTime;
        float MaxSpawnTimeCH47;
        float RocketDamageBlunt;
        float RocketDamageExplosion;
        float RocketExplosionRadius;


        int MaxLootCrates;
        int MaxHeliRockets;
        int BulletSpeed;
        int LifeTimeMinutes;
        int LifeTimeMinutesCH47;
        int WaterRequired;
        int MaxActiveHelicopters;
        int HelicoptersToSpawn;
        int ChinooksToSpawn;


        Dictionary<string, object> cds => GetConfig("Cooldowns", new Dictionary<string, object>());
        Dictionary<string, object> limits => GetConfig("Limits", new Dictionary<string, object>());


        [PluginReference]
        Plugin Vanish;
        private float LastSpawnTimer;
        private float LastSpawnTimerCH47;
        private DateTime LastTimerStart;
        private DateTime LastTimerStartCH47;
        private Timer _callTimer;
        private Timer _callTimerCH47;

        private Timer CallTimer
        {
            get { return _callTimer; }
            set
            {
                if (_callTimer != null) _callTimer.Destroy();
                LastTimerStart = DateTime.UtcNow;
                LastSpawnTimer = value.Delay;
                _callTimer = value;
            }
        }
        private Timer CallTimerCH47
        {
            get { return _callTimerCH47; }
            set
            {
                if (_callTimerCH47 != null) _callTimerCH47.Destroy();
                LastTimerStartCH47 = DateTime.UtcNow;
                LastSpawnTimerCH47 = value.Delay;
                _callTimerCH47 = value;
            }
        }
        
        private BaseHelicopter TimerHeli;
        private CH47HelicopterAIController TimerCH47;


        protected override void LoadDefaultConfig()
        {
            Config["Spawning - Disable Helicopter"] = DisableHeli = GetConfig("Spawning - Disable Helicopter", false);
            Config["Spawning - Disable Rust's default spawns"] = DisableDefaultHeliSpawns = GetConfig("Spawning - Disable Rust's default spawns", false);
            Config["Spawning - Disable CH47 default spawns"] = DisableDefaultChinookSpawns = GetConfig("Spawning - Disable CH47 default spawns", false);
            Config["Loot - Use Custom loot spawns"] = UseCustomLoot = GetConfig("Loot - Use Custom loot spawns", false);
            Config["Damage - Global damage multiplier"] = GlobalDamageMultiplier = GetConfig("Damage - Global damage multiplier", 1f);
            Config["Turrets - Helicopter bullet damage"] = HeliBulletDamageAmount = GetConfig("Turrets - Helicopter bullet damage", 20f);
            Config["Misc - Helicopter can shoot while dying"] = HelicopterCanShootWhileDying = GetConfig("Misc - Helicopter can shoot while dying", true);
            Config["Health - Main rotor health"] = MainRotorHealth = GetConfig("Health - Main rotor health", 750f);
            Config["Health - Tail rotor health"] = TailRotorHealth = GetConfig("Health - Tail rotor health", 375f);
            Config["Health - Base Helicopter health"] = BaseHealth = GetConfig("Health - Base Helicopter health", 10000f);
            Config["Loot - Max Crates to drop"] = MaxLootCrates = GetConfig("Loot - Max Crates to drop", 4);
            Config["Misc - Helicopter speed"] = HeliSpeed = GetConfig("Misc - Helicopter speed", 25f);
            Config["Turrets - Helicopter bullet accuracy"] = HeliAccuracy = GetConfig("Turrets - Helicopter bullet accuracy", 2f);
            Config["Rockets - Max helicopter rockets"] = MaxHeliRockets = GetConfig("Rockets - Max helicopter rockets", 12);
            Config["Spawning - Disable helicopter gibs"] = DisableGibs = GetConfig("Spawning - Disable helicopter gibs", false);
            Config["Spawning - Disable helicopter napalm"] = DisableNapalm = GetConfig("Spawning - Disable helicopter napalm", false);
            Config["Turrets - Helicopter bullet speed"] = BulletSpeed = GetConfig("Turrets - Helicopter bullet speed", 250);
            Config["Loot - Time before unlocking crates"] = TimeBeforeUnlocking = GetConfig("Loot - Time before unlocking crates", -1f);
            Config["Loot - Time before unlocking CH47 crates"] = TimeBeforeUnlockingHack = GetConfig("Loot - Time before unlocking CH47 crates", -1f);
            Config["Misc - Maximum helicopter life time in minutes"] = LifeTimeMinutes = GetConfig("Misc - Maximum helicopter life time in minutes", 15);
            Config["Misc - Maximum CH47 life time in minutes"] = LifeTimeMinutesCH47 = GetConfig("Misc - Maximum helicopter life time in minutes", 15);
            Config["Rockets - Time between each rocket in seconds"] = TimeBetweenRockets = GetConfig("Rockets - Time between each rocket in seconds", 0.2f);
            Config["Turrets - Turret fire rate in seconds"] = TurretFireRate = GetConfig("Turrets - Fire rate in seconds", 0.125f);
            Config["Turrets - Turret burst length in seconds"] = TurretburstLength = GetConfig("Turrets - Burst length in seconds", 3f);
            Config["Turrets - Time between turret bursts in seconds"] = TurretTimeBetweenBursts = GetConfig("Turrets - Time between turret bursts in seconds", 3f);
            Config["Turrets - Max range"] = TurretMaxRange = GetConfig("Turrets - Max range", 300f);
            Config["Rockets - Blunt damage to deal"] = RocketDamageBlunt = GetConfig("Rockets - Blunt damage to deal", 175f);
            Config["Rockets - Explosion damage to deal"] = RocketDamageExplosion = GetConfig("Rockets - Explosion damage to deal", 100f);
            Config["Rockets - Explosion radius"] = RocketExplosionRadius = GetConfig("Rockets - Explosion radius", 6f);
            Config["Gibs - Time until gibs can be harvested in seconds"] = GibsTooHotLength = GetConfig("Gibs - Time until gibs can be harvested in seconds", 480f);
            Config["Gibs - Health of gibs"] = GibsHealth = GetConfig("Gibs - Health of gibs", 500f);
            Config["Spawning - Automatically call helicopter between min seconds"] = MinSpawnTime = GetConfig("Spawning - Automatically call helicopter between min seconds", 0f);
            Config["Spawning - Automatically call helicopter between max seconds"] = MaxSpawnTime = GetConfig("Spawning - Automatically call helicopter between max seconds", 0f);
            Config["Spawning - Automatically call CH47 between min seconds"] = MinSpawnTimeCH47 = GetConfig("Spawning - Automatically call CH47 between min seconds", 0f);
            Config["Spawning - Automatically call CH47 between max seconds"] = MaxSpawnTimeCH47 = GetConfig("Spawning - Automatically call CH47 between max seconds", 0f);
            Config["Spawning - Use static spawning"] = UseOldSpawning = GetConfig("Spawning - Use static spawning", false);
            Config["Spawning - Use static spawning for CH47"] = UseOldSpawningCH47 = GetConfig("Spawning - Use static spawning for CH47", false);
            Config["Spawning - Automatically call helicopter if one is already flying"] = AutoCallIfExists = GetConfig("Spawning - Automatically call helicopter if one is already flying", false);
            Config["Spawning - Automatically call CH47 if one is already flying"] = AutoCallIfExistsCH47 = GetConfig("Spawning - Automatically call CH47 if one is already flying", false);
            Config["Spawning - Helicopters to spawn"] = HelicoptersToSpawn = GetConfig("Spawning - Helicopters to spawn", 1);
            Config["Spawning - Chinooks to spawn"] = ChinooksToSpawn = GetConfig("Spawning - Chinooks to spawn", 1);
            Config["Misc - Water required to extinguish napalm flames"] = WaterRequired = GetConfig("Misc - Water required to extinguish napalm flames", 10000);
            Config["Spawning - Use custom helicopter spawns"] = UseCustomHeliSpawns = GetConfig("Spawning - Use custom helicopter spawns", false);
            Config["Misc - Helicopter startup speed"] = HeliStartSpeed = GetConfig("Misc - Helicopter startup speed", 25f);
            Config["Misc - Helicopter startup length in seconds"] = HeliStartLength = GetConfig("Misc - Helicopter startup length in seconds", 0f);
            Config["Misc - Prevent crates from spawning when forcefully killing helicopter"] = DisableCratesDeath = GetConfig("Misc - Prevent crates from spawning when forcefully killing helicopter", true);
            Config["Spawning - Max active helicopters"] = MaxActiveHelicopters = GetConfig("Spawning - Max active helicopters", -1);
            for (int i = 0; i < 10; i++)
            {
                object outObj;
                var cd = "Cooldown." + i;
                var limit = "Limit." + i;
                var cdCh47 = "cooldown.ch47." + i;
                var limitCh47 = "limit.ch47." + i;
                if (!cds.TryGetValue(cd, out outObj)) cds[cd] = 86400f;
                if (!limits.TryGetValue(limit, out outObj)) limits[limit] = 5;
                if (!cds.TryGetValue(cdCh47, out outObj)) cds[cdCh47] = 86400f;
                if (!limits.TryGetValue(limitCh47, out outObj)) limits[limitCh47] = 5;
            }
            Config["Cooldowns"] = cds;
            Config["Limits"] = limits;
            SaveConfig();
        }
        void Init()
        {
            cooldownData = Interface.Oxide?.DataFileSystem?.ReadObject<StoredData3>("HeliControlCooldowns") ?? new StoredData3();
            LoadDefaultConfig();

            Config["Cooldowns"] = cds; // unsure if needed
            Config["Limits"] = limits;
            SaveConfig();

            string[] perms = { "callheli", "callheliself", "callhelitarget", "callch47", "callch47self", "callch47target", "killch47", "killheli", "strafe", "update", "destination", "dropcrate", "killnapalm", "killgibs", "unlockcrates", "admin", "ignorecooldown", "ignorelimits", "tpheli", "tpch47", "helispawn", "callmultiple", "callmultiplech47", "nodrop", "nextheli" };


            for (int j = 0; j < perms.Length; j++) permission.RegisterPermission("helicontrol." + perms[j], this);
            foreach (var limit in limits.Keys) permission.RegisterPermission("helicontrol." + limit, this);
            foreach (var cd in cds.Keys) permission.RegisterPermission("helicontrol." + cd, this);
            if (HelicopterCanShootWhileDying)
            {
                Unsubscribe(nameof(CanBeTargeted));
                Unsubscribe(nameof(OnHelicopterTarget));
                Unsubscribe(nameof(CanHelicopterStrafeTarget));
                Unsubscribe(nameof(CanHelicopterStrafe));
                Unsubscribe(nameof(CanHelicopterTarget));
            }
            if (!DisableNapalm) Unsubscribe(nameof(CanHelicopterUseNapalm));
            AddCovalenceCommand("unlockcrates", "cmdUnlockCrates");
            AddCovalenceCommand("tpheli", "cmdTeleportHeli");
            AddCovalenceCommand("killheli", "cmdKillHeli");
            AddCovalenceCommand("killch47", "cmdKillCH47");
            AddCovalenceCommand("dropcrate", "cmdDropCH47Crate");
            AddCovalenceCommand("updatehelis", "cmdUpdateHelicopters");
            AddCovalenceCommand("strafe", "cmdStrafeHeli");
            AddCovalenceCommand("helidest", "cmdDestChangeHeli");
            AddCovalenceCommand("killnapalm", "cmdKillFB");
            AddCovalenceCommand("killgibs", "cmdKillGibs");
            AddCovalenceCommand("nextheli", "cmdNextHeli");
            LoadDefaultMessages();
        }

        protected override void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                //DO NOT EDIT LANGUAGE FILES HERE! Navigate to oxide\lang
                {"noPerms", "You do not have permission to use this command!"},
                {"invalidSyntax", "Invalid Syntax, usage example: {0} {1}"},
                {"invalidSyntaxMultiple", "Invalid Syntax, usage example: {0} {1} or {2} {3}"},
                {"heliCalled", "Helicopter Inbound!"},
                {"helisCalledPlayer", "{0} Helicopter(s) called on: {1}"},
                {"entityDestroyed", "{0} {1}(s) were annihilated!"},
                {"helisForceDestroyed", "{0} Helicopter(s) were forcefully destroyed!"},
                {"heliAutoDestroyed", "Helicopter auto-destroyed because config has it disabled!" },
                {"playerNotFound", "Could not find player: {0}"},
                {"noHelisFound", "No active helicopters were found!"},
                {"cannotBeCalled", "This can only be called on a single Helicopter, there are: {0} active."},
                {"strafingOtherPosition", "Helicopter is now strafing {0}'s position."},
                {"destinationOtherPosition", "Helicopter's destination has been set to {0}'s position."},
                {"IDnotFound", "Could not find player by ID: {0}" },
                {"updatedHelis", "{0} helicopters were updated successfully!" },
                {"callheliCooldown", "You must wait before using this again! You've waited: {0}/{1}" },
                {"invalidCoordinate", "Incorrect argument supplied for {0} coordinate!" },
                {"coordinatesOutOfBoundaries", "Coordinates are out of map boundaries!" },
                {"callheliLimit", "You've used your daily limit of {0} heli calls!" },
                {"unlockedAllCrates", "Unlocked all Helicopter crates!" },
                {"teleportedToHeli", "You've been teleported to the ground below the active Helicopter!" },
                {"removeAddSpawn", "To remove a Spawn, type: /helispawn remove SpawnName\n\nTo add a Spawn, type: /helispawn add SpawnName -- This will add the spawn on your current position." },
                {"addedSpawn", "Added helicopter spawn {0} with the position of: {1}" },
                {"spawnExists", "A spawn point with this name already exists!" },
                {"noSpawnsExist", "No Helicopter spawns have been created!" },
                {"removedSpawn", "Removed Helicopter spawn point: {0}: {1}" },
                {"noSpawnFound", "No spawn could be found with that name!" },
                {"onlyCallSelf", "You can only call a Helicopter on yourself, try: /callheli {0}" },
                {"spawnCommandLiner", "<color=orange>----</color>Spawns<color=orange>----</color>\n" },
                {"spawnCommandBottom", "\n<color=orange>----------------</color>" },
                {"cantCallTargetOrSelf", "You do not have the permission to call a Helicopter on a target! Try: /callheli" },
                {"maxHelis", "Killing helicopter because the maximum active helicopters has been reached" },
                {"cmdError", "An error happened while using this command. Please report this to your server administrator." },
                {"ch47AlreadyDropped", "This CH47 has already dropped a crate!" },
                {"ch47DroppedCrate", "Dropped crate!" },
                {"noTimeFound", "No spawn time found for helicopter." },
                {"noTimeFoundCH47", "No spawn time found for CH47." },
                {"nextHeliSpawn", "Next helicopter spawn: {0}" },
                {"nextCH47Spawn", "Next CH47 spawn: {0}" },
                {"nextAlreadyActive", "A helicopter is already active." },
                {"itemNotFound", "Item not found!" },
            };
            lang.RegisterMessages(messages, this);
        }
        private string GetMessage(string key, string steamId = null) => lang.GetMessage(key, this, steamId);
        void OnServerInitialized()
        {
            timer.Every(10f, () => CheckHelicopter());
            foreach(var entity in BaseNetworkable.serverEntities)
            {
                if (entity == null) continue;
                var heli = entity as BaseHelicopter;
                var ch47 = entity as CH47HelicopterAIController;
                var crate = entity as LockedByEntCrate;
                var hackCrate = entity as HackableLockedCrate;
                var debris = entity as HelicopterDebris;
                var fireball = entity as FireBall;
                if (heli != null) BaseHelicopters.Add(heli);
                if (ch47 != null) Chinooks.Add(ch47);
                if (crate != null) lockedCrates.Add(crate);
                if (hackCrate != null) hackLockedCrates.Add(hackCrate);
                if (debris != null) Gibs.Add(debris);
                if (fireball != null && (fireball.ShortPrefabName.Contains("napalm") || fireball.ShortPrefabName.Contains("oilfire"))) FireBalls.Add(fireball);
            }

            foreach (var heli in BaseHelicopters)
            {
                if (heli == null) continue;
                UpdateHeli(heli, false);
            }
            var eventPrefabs = (DisableDefaultHeliSpawns || DisableDefaultChinookSpawns) ? GameObject.FindObjectsOfType<TriggeredEventPrefab>() : null;
            if (DisableDefaultHeliSpawns)
            {
                var heliEvent = eventPrefabs?.Where(p => (p?.targetPrefab?.resourcePath ?? string.Empty).Contains("heli"))?.FirstOrDefault() ?? null;
                if (heliEvent != null)
                {
                    GameObject.Destroy(heliEvent);
                    Puts("Disabled default Helicopter spawning.");
                }
            }
            if (DisableDefaultChinookSpawns)
            {
                var heliEvent = eventPrefabs?.Where(p => (p?.targetPrefab?.resourcePath ?? string.Empty).Contains("ch47"))?.FirstOrDefault() ?? null;
                if (heliEvent != null)
                {
                    GameObject.Destroy(heliEvent);
                    Puts("Disabled default Chinook spawning.");
                }
            }
            
            ConVar.PatrolHelicopter.bulletAccuracy = HeliAccuracy;
            ConVar.PatrolHelicopter.lifetimeMinutes = LifeTimeMinutes;
            if (TimeBeforeUnlockingHack > 0f) HackableLockedCrate.requiredHackSeconds = TimeBeforeUnlockingHack;

            if (UseCustomLoot) LoadSavedData();
            LoadHeliSpawns();
            LoadWeaponData();


            boundary = TerrainMeta.Size.x / 2;

            CH47Instance = Chinooks?.FirstOrDefault() ?? null;

            var rngTime = GetRandomSpawnTime();
            var rngTimeCH47 = GetRandomSpawnTime(true);
            if (rngTime > 0f)
            {
                if (!UseOldSpawning) CallTimer = timer.Once(rngTime, () => { if (HeliCount < 1 || AutoCallIfExists) TimerHeli = callHelis(HelicoptersToSpawn, forced:false)?.FirstOrDefault(); });
                else
                {
                    Timer newTimer = null;
                    newTimer = timer.Every(rngTime, () =>
                    {
                        if (HeliCount < 1 || AutoCallIfExists) callHelis(HelicoptersToSpawn, forced: false);
                        LastTimerStart = DateTime.UtcNow.AddSeconds(-newTimer.Delay);
                        LastSpawnTimer = newTimer.Delay * 2;
                        Puts("Next Helicopter spawn: " + (rngTime >= 60 ? ((rngTime / 60).ToString("N0") + " minutes") : rngTime.ToString("N0") + " seconds"));
                    });
                    CallTimer = newTimer;
                }
                Puts("Next Helicopter spawn: " + (rngTime >= 60 ? ((rngTime / 60).ToString("N0") + " minutes") : rngTime.ToString("N0") + " seconds"));
            }
            if (rngTimeCH47 > 0f)
            {
                if (!UseOldSpawningCH47) CallTimerCH47 = timer.Once(rngTimeCH47, () => { if (CH47Count < 1 || AutoCallIfExistsCH47) TimerCH47 = callChinooks(HelicoptersToSpawn, forced:false)?.FirstOrDefault(); });
                else
                {
                    Timer newTimer = null;
                    newTimer = timer.Every(rngTime, () =>
                    {
                        if (CH47Count < 1 || AutoCallIfExistsCH47) callChinooks(ChinooksToSpawn, forced:false);
                        LastTimerStart = DateTime.UtcNow.AddSeconds(-newTimer.Delay);
                        LastSpawnTimer = newTimer.Delay * 2;
                        Puts("Next CH47 spawn: " + (rngTimeCH47 >= 60 ? ((rngTimeCH47 / 60).ToString("N0") + " minutes") : rngTimeCH47.ToString("N0") + " seconds"));
                    });
                    CallTimerCH47 = newTimer;
                }
            
                Puts("Next CH47 spawn: " + (rngTimeCH47 >= 60 ? ((rngTimeCH47 / 60).ToString("N0") + " minutes") : rngTimeCH47.ToString("N0") + " seconds"));
            }
            init = true;
        }
        #endregion
        #region Hooks
        void Unload()
        {
            SaveData3();
            SaveData4();
        }

        void OnServerSave()
        {
            SaveData3();
            SaveData4();
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity == null || !init) return;
            var prefabname = entity?.ShortPrefabName ?? string.Empty;
            var longprefabname = entity?.PrefabName ?? string.Empty;
            if (string.IsNullOrEmpty(prefabname) || string.IsNullOrEmpty(longprefabname)) return;
            var ownerID = (entity as BaseEntity)?.OwnerID ?? 0;
            if (entity is LockedByEntCrate) lockedCrates.Add(entity as LockedByEntCrate);
            if (entity is HackableLockedCrate) hackLockedCrates.Add(entity as HackableLockedCrate);
            if (entity is CH47HelicopterAIController)
            {
                var ch = entity as CH47HelicopterAIController;
                ch.Invoke(() =>
                {
                    if (!ch.IsDestroyed) ch.Kill();
                }, (LifeTimeMinutesCH47 * 60));
                Chinooks.Add(ch);
                CH47Instance = ch;
            }
            if (entity is FireBall && (entity.ShortPrefabName.Contains("napalm") || entity.ShortPrefabName.Contains("oil"))) FireBalls.Add(entity as FireBall);

            if (prefabname.Contains("rocket_heli"))
            {
                var explosion = entity as TimedExplosive;
                if (explosion == null || explosion.IsDestroyed) return;
                if (MaxHeliRockets < 1) explosion.Kill();
                else
                {
                    if (MaxHeliRockets > 12 && ownerID == 0)
                    {
                        var strafeHeli = BaseHelicopters?.Where(p => p != null && !p.IsDestroyed && p.gameObject != null && (p?.GetComponent<PatrolHelicopterAI>()?._currentState ?? PatrolHelicopterAI.aiState.IDLE) == PatrolHelicopterAI.aiState.STRAFE)?.FirstOrDefault() ?? null;
                        if (strafeHeli == null || strafeHeli.IsDestroyed) return;
                        var curCount = 0;
                        if (!strafeCount.TryGetValue(strafeHeli, out curCount)) curCount = (strafeCount[strafeHeli] = 1);
                        else curCount = (strafeCount[strafeHeli] += 1);
                        if (curCount >= 12)
                        {
                            var heliAI = strafeHeli?.GetComponent<PatrolHelicopterAI>() ?? null;
                            if (heliAI == null) return;
                            var actCount = 0;
                            Action fireAct = null;
                            fireAct = new Action(() =>
                            {
                                if (actCount >= (MaxHeliRockets - 12))
                                {
                                    InvokeHandler.CancelInvoke(heliAI, fireAct);
                                    return;
                                }
                                actCount++;
                                FireRocket(heliAI);
                            });
                            InvokeHandler.InvokeRepeating(heliAI, fireAct, TimeBetweenRockets, TimeBetweenRockets);
                            strafeCount[strafeHeli] = 0;
                        }
                    }
                    else if (MaxHeliRockets < 12 && (HeliInstance.ClipRocketsLeft() > MaxHeliRockets))
                    {
                        explosion.Kill();
                        return;
                    }

                    var dmgTypes = explosion?.damageTypes ?? null;
                    explosion.explosionRadius = RocketExplosionRadius;
                    if (dmgTypes != null && dmgTypes.Count > 0)
                    {
                        for (int i = 0; i < dmgTypes.Count; i++)
                        {
                            var dmg = dmgTypes[i];
                            if (dmg.type == Rust.DamageType.Blunt) dmg.amount = RocketDamageBlunt;
                            if (dmg.type == Rust.DamageType.Explosion) dmg.amount = RocketDamageExplosion;
                        }
                    }
                }
            }

            if (prefabname == "heli_crate")
            {
                if (UseCustomLoot && lootData?.HeliInventoryLists != null && lootData.HeliInventoryLists.Count > 0)
                {
                    var heli_crate = entity as LootContainer;
                    if (heli_crate == null || heli_crate?.inventory == null) return; //possible that the inventory is somehow null? not sure
                    int index;
                    index = rng.Next(lootData.HeliInventoryLists.Count);
                    var inv = lootData.HeliInventoryLists[index];
                    var itemList = heli_crate?.inventory?.itemList?.ToList() ?? null;
                    if (itemList != null && itemList.Count > 0) for (int i = 0; i < itemList.Count; i++) RemoveFromWorld(itemList[i]); //completely remove all existing items in crate
                    for (int i = 0; i < inv.lootBoxContents.Count; i++)
                    {
                        var itemDef = inv.lootBoxContents[i];
                        if (itemDef == null) continue;
                        var amount = (itemDef.amountMin > 0 && itemDef.amountMax > 0) ? UnityEngine.Random.Range(itemDef.amountMin, itemDef.amountMax) : itemDef.amount;
                        var skinID = 0ul;
                        ulong.TryParse(itemDef.skinID.ToString(), out skinID);
                        var def = ItemManager.FindItemDefinition(itemDef.name);
                        if (def != null)
                        {
                            var item = ItemManager.Create(def, amount, skinID);
                            if (item != null && !item.MoveToContainer(heli_crate.inventory)) RemoveFromWorld(item); //ensure the item is completely removed if we can't move it, so we're not causing issues
                        }
                    }
                    heli_crate.inventory.MarkDirty();
                }


                if (TimeBeforeUnlocking != -1f)
                {
                    var crate2 = entity as LockedByEntCrate;
                    if (TimeBeforeUnlocking == 0f) UnlockCrate(crate2);
                    else timer.Once(TimeBeforeUnlocking, () =>
                    {
                        if (entity == null || entity.IsDestroyed || crate2 == null) return;
                        UnlockCrate(crate2);
                    });
                }
            }

            if (entity is HelicopterDebris)
            {
                var debris = entity as HelicopterDebris;
                if (debris == null) return;
                if (DisableGibs)
                {
                    NextTick(() => { if (!(entity?.IsDestroyed ?? true)) entity.Kill(); });
                    return;
                }
                if (GibsHealth != 500f)
                {
                    debris.InitializeHealth(GibsHealth, GibsHealth);
                    debris.SendNetworkUpdate();
                }
               
                Gibs.Add(debris);
                if (GibsTooHotLength != 480f) tooHotUntil.SetValue(debris, Time.realtimeSinceStartup + GibsTooHotLength);
            }

            if (entity is BaseHelicopter)
            {
                var isMax = (HeliCount >= MaxActiveHelicopters && MaxActiveHelicopters != -1);
                if (DisableHeli || isMax) NextTick(() => { if (!(entity?.IsDestroyed ?? true)) entity.Kill(); });
                if (DisableHeli)
                {
                    Puts(GetMessage("heliAutoDestroyed"));
                    return;
                }
                else if (isMax)
                {
                    Puts(GetMessage("maxHelis"));
                    return;
                }
                var AIHeli = entity?.GetComponent<PatrolHelicopterAI>() ?? null;
                var BaseHeli = entity as BaseHelicopter;
                if (AIHeli == null || BaseHeli == null) return;
                BaseHelicopters.Add(BaseHeli);
                UpdateHeli(BaseHeli, true);
                if (UseCustomHeliSpawns && spawnsData?.HelicopterSpawns != null && spawnsData.HelicopterSpawns.Count > 0 && !forceCalled.Contains(BaseHeli))
                {
                    var valCount = spawnsData.HelicopterSpawns.Count;
                    var rng = UnityEngine.Random.Range(0, valCount);
                    var pos = spawnsData.HelicopterSpawns[rng].Position;
                    BaseHeli.transform.position = pos;
                    AIHeli.transform.position = pos;
                    PrintWarning("using custom heli spawn: " + rng + ", " + pos);
                }
                if (HeliStartLength > 0.0f && HeliStartSpeed != HeliSpeed)
                {
                    AIHeli.maxSpeed = HeliStartSpeed;
                    timer.Once(HeliStartLength, () =>
                    {
                        if (AIHeli == null || BaseHeli == null || BaseHeli.IsDead()) return;
                        AIHeli.maxSpeed = HeliSpeed;
                    });
                }
            }
        }

        object CanBeTargeted(BaseCombatEntity entity, MonoBehaviour monoTurret)
        {
            if (!init || entity == null || (entity?.IsDestroyed ?? true) || monoTurret == null) return null;
            var aiHeli = (monoTurret as HelicopterTurret)?._heliAI ?? null;
            if (aiHeli == null) return null;
            var player = entity as BasePlayer;
            if (player != null && Vanish != null && (Vanish?.Call<bool>("IsInvisible", player) ?? false)) return null;
            if ((aiHeli?._currentState ?? PatrolHelicopterAI.aiState.DEATH) == PatrolHelicopterAI.aiState.DEATH) return false;
            return null;
        }

        object OnHelicopterTarget(HelicopterTurret turret, BaseCombatEntity entity)
        {
            if (turret == null || entity == null) return null;
            if ((turret?._heliAI?._currentState ?? PatrolHelicopterAI.aiState.DEATH) == PatrolHelicopterAI.aiState.DEATH) return false;
            return null;
        }

        object CanHelicopterStrafeTarget(PatrolHelicopterAI entity, BasePlayer target)
        {
            if (entity == null || target == null) return null;
            if ((entity?._currentState ?? PatrolHelicopterAI.aiState.DEATH) == PatrolHelicopterAI.aiState.DEATH) return false;
            return null;
        }

        object CanHelicopterStrafe(PatrolHelicopterAI entity)
        {
            if (entity == null) return null;
            if ((entity?._currentState ?? PatrolHelicopterAI.aiState.DEATH) == PatrolHelicopterAI.aiState.DEATH) return false;
            return null;
        }

        object CanHelicopterTarget(PatrolHelicopterAI entity, BasePlayer player)
        {
            if (entity == null) return null;
            if ((entity?._currentState ?? PatrolHelicopterAI.aiState.DEATH) == PatrolHelicopterAI.aiState.DEATH) return false;
            return null;
        }

        object CanHelicopterUseNapalm(PatrolHelicopterAI entity)
        {
            if (entity == null) return null;
            return false;
        }

        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null) return;
            var name = entity?.ShortPrefabName ?? string.Empty;
            var crate = entity as LockedByEntCrate;
            var CH47 = entity as CH47HelicopterAIController;
            if (crate != null && lockedCrates.Contains(crate)) lockedCrates.Remove(crate);
            if (name.Contains("patrolhelicopter") && !name.Contains("gib"))
            {
                var baseHeli = entity?.GetComponent<BaseHelicopter>() ?? null;
                if (baseHeli == null) return;
                if (BaseHelicopters.Contains(baseHeli)) BaseHelicopters.Remove(baseHeli);
                if (forceCalled.Contains(baseHeli)) forceCalled.Remove(baseHeli);
                if (!UseOldSpawning && (CallTimer == null || CallTimer.Destroyed) && baseHeli == TimerHeli)
                {
                    var rngTime = GetRandomSpawnTime();
                    CallTimer = timer.Once(rngTime, () => { if (HeliCount < 1 || AutoCallIfExists) TimerHeli = callHelis(HelicoptersToSpawn, forced:false)?.FirstOrDefault(); });
                    LastTimerStart = DateTime.UtcNow;
                    LastSpawnTimer = rngTime;
                    Puts("Next Helicopter spawn: " + (rngTime >= 60 ? ((rngTime / 60).ToString("N0") + " minutes") : rngTime.ToString("N0") + " seconds"));
                } //otherwise, a timer is already firing (or heli is not a timer heli or old spawning is enabled)
            }
            if (CH47 != null)
            {
                if (Chinooks.Contains(CH47)) Chinooks.Remove(CH47);
                if (forceCalledCH.Contains(CH47)) forceCalledCH.Remove(CH47);
                if (!UseOldSpawningCH47 && (CallTimerCH47 == null || CallTimerCH47.Destroyed) && CH47 == TimerCH47)
                {
                    var rngTime = GetRandomSpawnTime(true);
                    CallTimerCH47 = timer.Once(rngTime, () => { if (CH47Count < 1 || AutoCallIfExistsCH47) TimerCH47 = callChinooks(HelicoptersToSpawn, forced:false)?.FirstOrDefault(); });
                    LastTimerStartCH47 = DateTime.UtcNow;
                    LastSpawnTimerCH47 = rngTime;
                    Puts("Next CH47 spawn: " + (rngTime >= 60 ? ((rngTime / 60).ToString("N0") + " minutes") : rngTime.ToString("N0") + " seconds"));
                } //otherwise, a timer is already firing (or heli is not a timer heli or old spawning is enabled)
            }
            if (entity is FireBall || name.Contains("fireball") || name.Contains("napalm"))
            {
                var fireball = entity as FireBall;
                if (fireball != null && FireBalls.Contains(fireball)) FireBalls.Remove(fireball);
            }
            if (entity is HelicopterDebris)
            {
                var debris = entity as HelicopterDebris;
                if (debris != null && Gibs.Contains(debris)) Gibs.Remove(debris);
            }
        }

        void OnPlayerAttack(BasePlayer attacker, HitInfo hitInfo)
        {
            if (attacker == null || hitInfo == null || hitInfo.HitEntity == null) return;
            var name = hitInfo?.HitEntity?.ShortPrefabName ?? string.Empty;

            if (name == "patrolhelicopter")
            {
                if (GlobalDamageMultiplier != 1f && !(GlobalDamageMultiplier < 0))
                {
                    hitInfo?.damageTypes?.ScaleAll(GlobalDamageMultiplier);
                    return;
                }
                var shortName = hitInfo?.Weapon?.GetItem()?.info?.shortname ?? string.Empty;
                var displayName = hitInfo?.Weapon?.GetItem()?.info?.displayName?.english ?? string.Empty;
                var weaponConfig = 0.0f;
                if (string.IsNullOrEmpty(shortName)) return;
                if (!weaponsData.WeaponList.TryGetValue(shortName, out weaponConfig)) weaponsData.WeaponList.TryGetValue(displayName, out weaponConfig);
                if (weaponConfig != 0.0f && weaponConfig != 1.0f) hitInfo?.damageTypes?.ScaleAll(weaponConfig);
            }
        }
        #endregion
        #region Main
        private void UpdateHeli(BaseHelicopter heli, bool justCreated = false)
        {
            if (heli == null || heli.IsDestroyed || heli.IsDead()) return;
            heli.startHealth = BaseHealth;
            if (justCreated) heli.InitializeHealth(BaseHealth, BaseHealth);
            heli.maxCratesToSpawn = MaxLootCrates;
            heli.bulletDamage = HeliBulletDamageAmount;
            heli.bulletSpeed = BulletSpeed;
            var weakspots = heli.weakspots;
            if (weakspots != null && weakspots.Length > 1) //not even sure if this is needed, but may fix some very strange NRE
            {
                if (justCreated)
                {

                    weakspots[0].health = MainRotorHealth;
                    weakspots[1].health = TailRotorHealth;
                }
                weakspots[0].maxHealth = MainRotorHealth;
                weakspots[1].maxHealth = TailRotorHealth;
            }
            var heliAI = heli?.GetComponent<PatrolHelicopterAI>() ?? null;
            if (heliAI == null) return;
            heliAI.maxSpeed = Mathf.Clamp(HeliSpeed, 0.1f, 125);
            heliAI.timeBetweenRockets = Mathf.Clamp(TimeBetweenRockets, 0.1f, 1f);
            heliAI.numRocketsLeft = Mathf.Clamp(MaxHeliRockets, 0, 48);
            updateTurrets(heliAI);
            heli.SendNetworkUpdateImmediate(justCreated);
        }

        //nearly exact code used by Rust to fire helicopter rockets
        private void FireRocket(PatrolHelicopterAI heliAI)
        {
            if (heliAI == null || !(heliAI?.IsAlive() ?? false)) return;
            var num1 = 4f;
            var strafeTarget = heliAI.strafe_target_position;
            if (strafeTarget == Vector3.zero) return;
            var vector3 = heliAI.transform.position + heliAI.transform.forward * 1f;
            var direction = (strafeTarget - vector3).normalized;
            if (num1 > 0.0) direction = Quaternion.Euler(UnityEngine.Random.Range((float)(-(double)num1 * 0.5), num1 * 0.5f), UnityEngine.Random.Range((float)(-(double)num1 * 0.5), num1 * 0.5f), UnityEngine.Random.Range((float)(-(double)num1 * 0.5), num1 * 0.5f)) * direction;
            var flag = heliAI.leftTubeFiredLast;
            heliAI.leftTubeFiredLast = !flag;
            Effect.server.Run(heliAI.helicopterBase.rocket_fire_effect.resourcePath, heliAI.helicopterBase, StringPool.Get(!flag ? "rocket_tube_right" : "rocket_tube_left"), Vector3.zero, Vector3.forward, (Network.Connection)null, true);
            var entity = GameManager.server.CreateEntity(!heliAI.CanUseNapalm() ? heliAI.rocketProjectile.resourcePath : heliAI.rocketProjectile_Napalm.resourcePath, vector3, new Quaternion(), true);
            if (entity == null) return;
            entity.SendMessage("InitializeVelocity", (direction * 1f));
            entity.OwnerID = 1337; //assign ownerID so it doesn't infinitely loop on OnEntitySpawned
            entity.Spawn();
        }

        private BaseHelicopter callHeli(Vector3 coordinates = new Vector3(), bool forced = true)
        {
            var heli = (BaseHelicopter)GameManager.server.CreateEntity(heliPrefab, new Vector3(), new Quaternion(), true);
            if (heli == null) return null;
            var heliAI = heli?.GetComponent<PatrolHelicopterAI>() ?? null;
            if (heliAI == null) return null;
            if (coordinates != Vector3.zero) heliAI.SetInitialDestination(coordinates + new Vector3(0f, 10f, 0f), 0.25f);
            if (forced) forceCalled.Add(heliAI.helicopterBase);
            heli.Spawn();
            return heli;
        }

        //chinook position setting code is based off of the same code used by patrolhelicopter in order to (try to) get it to spawn out of the map and fly in
        private CH47HelicopterAIController callChinook(Vector3 coordinates = new Vector3(), bool forced = true)
        {
            var heli = (CH47HelicopterAIController)GameManager.server.CreateEntity(chinookPrefab, new Vector3(0, 100, 0), new Quaternion(), true);
            if (heli == null) return null;
            float x = TerrainMeta.Size.x;
            float num = coordinates.y + 50f;
            var mapScaleDistance = 0.8f; //high scale for further out distances/positions
            var vector3_1 = Vector3Ex.Range(-1f, 1f);
            vector3_1.y = 0.0f;
            vector3_1.Normalize();
            var vector3_2 = vector3_1 * (x * mapScaleDistance);
            vector3_2.y = num;
            heli.transform.position = vector3_2;
            if (forced) forceCalledCH.Add(heli);
            heli.Spawn();
            if (coordinates != Vector3.zero) timer.Once(1f, () => { if (heli != null && !heli.IsDestroyed) SetDestination(heli, coordinates + new Vector3(0f, 10f, 0));});
            return heli;
        }

        private List<BaseHelicopter> callHelis(int amount, Vector3 coordinates = new Vector3(), bool forced = true)
        {
            if (amount < 1) return null;
            var listHelis = new List<BaseHelicopter>(amount);
            for (int i = 0; i < amount; i++) listHelis.Add(callHeli(coordinates, forced));
            return listHelis;
        }

        private List<CH47HelicopterAIController> callChinooks(int amount, Vector3 coordinates = new Vector3(), bool forced = true)
        {
            if (amount < 1) return null;
            var listHelis = new List<CH47HelicopterAIController>(amount);
            for (int i = 0; i < amount; i++) listHelis.Add(callChinook(coordinates, forced));
            return listHelis;
        }
        

        BaseHelicopter callCoordinates(Vector3 coordinates)
        {
            var heli = (BaseHelicopter)GameManager.server.CreateEntity(heliPrefab, new Vector3(), new Quaternion(), true);
            if (heli == null) return null;
            var heliAI = heli?.GetComponent<PatrolHelicopterAI>() ?? null;
            if (heliAI == null) return null;
            heliAI.SetInitialDestination(coordinates + new Vector3(0f, 10f, 0f), 0.25f);
            forceCalled.Add(heliAI.helicopterBase);
            heli.Spawn();
            return heli;
        }

        private void updateTurrets(PatrolHelicopterAI helicopter)
        {
            if (helicopter == null || helicopter.leftGun == null || helicopter.rightGun == null) return;
            helicopter.leftGun.fireRate = (helicopter.rightGun.fireRate = TurretFireRate);
            helicopter.leftGun.timeBetweenBursts = (helicopter.rightGun.timeBetweenBursts = TurretTimeBetweenBursts);
            helicopter.leftGun.burstLength = (helicopter.rightGun.burstLength = TurretburstLength);
            helicopter.leftGun.maxTargetRange = (helicopter.rightGun.maxTargetRange = TurretMaxRange);
        }

        private int killAllHelis(bool isForced = false)
        {
            CheckHelicopter();
            var count = 0;
            if (BaseHelicopters.Count < 1) return count;
            foreach (var helicopter in BaseHelicopters.ToList())
            {
                if (helicopter == null || helicopter.IsDead()) continue;
                if (DisableCratesDeath) helicopter.maxCratesToSpawn = 0;
                if (isForced) helicopter.Kill();
                else helicopter.DieInstantly();
                count++;
            }
            CheckHelicopter();
            return count;
        }
        private int killAllChinooks(bool isForced = false)
        {
            CheckHelicopter();
            var count = 0;
            if (Chinooks.Count < 1) return count;
            foreach (var ch47 in Chinooks.ToList())
            {
                if (ch47 == null || ch47.IsDestroyed) continue;
                if (isForced) ch47.Kill();
                else ch47.DieInstantly();
                count++;
            }
            CheckHelicopter();
            return count;
        }
        #endregion
        #region Commands
        [ChatCommand("helispawn")]
        private void cmdHeliSpawns(BasePlayer player, string command, string[] args)
        {
            if (!HasPerms(player.UserIDString, "helispawn"))
            {
                SendNoPerms(player);
                return;
            }
            if (args.Length < 1)
            {
                var msgSB = new StringBuilder();
                for(int i = 0; i < spawnsData.HelicopterSpawns.Count; i++)
                {
                    var sp = spawnsData.HelicopterSpawns[i];
                    msgSB.Append(sp.Name + ": " + sp.Position + ", ");
                }
                var msg = msgSB.ToString().TrimEnd(", ".ToCharArray());
                if (!string.IsNullOrEmpty(msg)) SendReply(player, GetMessage("spawnCommandLiner", player.UserIDString) + msgSB + GetMessage("spawnCommandBottom", player.UserIDString));
                SendReply(player, GetMessage("removeAddSpawn"), player.UserIDString); //this isn't combined with a new line with the above because there is a strange character limitation per-message, so we send two messages
                return;
            }
            var lowerArg0 = args[0].ToLower();
            var spawn = args.Length > 1 ? FindSpawn(args[1]) : null;
            if (lowerArg0 == "add" && args.Length > 1)
            {
                if (spawn == null)
                {
                    var pos = player?.transform?.position ?? Vector3.zero;
                    if (pos == Vector3.zero) return;
                    spawnsData.HelicopterSpawns.Add(new HelicopterSpawn { Position = pos, Name = args[1] });
                    SendReply(player, string.Format(GetMessage("addedSpawn", player.UserIDString), args[1], pos));
                }
                else SendReply(player, GetMessage("spawnExists", player.UserIDString));
            }
            else if (lowerArg0 == "remove" && args.Length > 1)
            {
                if (spawnsData?.HelicopterSpawns == null || spawnsData.HelicopterSpawns.Count < 1)
                {
                    SendReply(player, GetMessage("noSpawnsExist", player.UserIDString));
                    return;
                }
                if (spawn != null)
                {
                    var value = spawn.Position;
                    spawnsData.HelicopterSpawns.Remove(spawn);
                    SendReply(player, string.Format(GetMessage("removedSpawn", player.UserIDString), args[1], value));
                }
                else SendReply(player, GetMessage("noSpawnFound", player.UserIDString));
            }
            else SendReply(player, string.Format(GetMessage("invalidSyntaxMultiple", player.UserIDString), "/helispawn add", "SpawnName", "/helispawn remove", "SpawnName"));
        }

        private void cmdUnlockCrates(IPlayer player, string command, string[] args)
        {
            if (!HasPerms(player.Id, "unlockcrates"))
            {
                SendNoPerms(player);
                return;
            }
            var chCrate = args.Length > 0 ? args[0].Equals("ch47", StringComparison.OrdinalIgnoreCase) : false;
            var bothCrates = args.Length > 0 ? args[0].Equals("all", StringComparison.OrdinalIgnoreCase) : false;
            if (!chCrate || bothCrates) foreach (var crate in lockedCrates) UnlockCrate(crate);
            else if (bothCrates || chCrate) foreach (var crate in hackLockedCrates) UnlockCrate(crate);
            player.Message(GetMessage("unlockedAllCrates", player.Id));
        }

        private void cmdNextHeli(IPlayer player, string command, string[] args)
        {
            if (!HasPerms(player.Id, "nextheli"))
            {
                SendNoPerms(player);
                return;
            }
            var now = DateTime.UtcNow;
            var addTime = TimeSpan.MinValue;
            if (LastTimerStart == DateTime.MinValue || LastSpawnTimer <= 0f) player.Message(GetMessage("noTimeFound", player.Id));
            else
            {
                addTime = LastTimerStart.AddSeconds(LastSpawnTimer) - now;
                player.Message(string.Format(GetMessage("nextHeliSpawn", player.Id), (addTime.TotalMilliseconds < 0) ? GetMessage("nextAlreadyActive", player.Id) : ReadableTimeSpan(addTime)));
            }

            if (LastTimerStartCH47 == DateTime.MinValue || LastSpawnTimerCH47 <= 0f)
            {
                player.Message(GetMessage("noTimeFoundCH47", player.Id));
                return;
            }
            addTime = LastTimerStartCH47.AddSeconds(LastSpawnTimerCH47) - now;
            player.Message(string.Format(GetMessage("nextCH47Spawn", player.Id), (addTime.TotalMilliseconds < 0) ? GetMessage("nextAlreadyActive", player.Id) : ReadableTimeSpan(addTime)));
        }

        private void cmdDropCH47Crate(IPlayer player, string command, string[] args)
        {
            if (!HasPerms(player.Id, "dropcrate"))
            {
                SendNoPerms(player);
                return;
            }
            if (CH47Instance == null || CH47Instance.IsDestroyed || CH47Instance.IsDead())
            {
                player.Message(GetMessage("noHelisFound", player.Id));
                return;
            }
            var all = args.Length > 0 && args[0].ToLower() == "all";
            if (CH47Count > 1 && !all)
            {
                player.Message(string.Format(GetMessage("cannotBeCalled", player.Id), HeliCount.ToString("N0")));
                return;
            }
            if (all) foreach (var ch47 in Chinooks) { if (ch47.CanDropCrate()) ch47.DropCrate(); }
            else
            {
                if (!CH47Instance.CanDropCrate())
                {
                    player.Message(GetMessage("ch47AlreadyDropped", player.Id));
                    return;
                }
                CH47Instance.DropCrate();
            }
            player.Message(GetMessage("ch47DroppedCrate", player.Id));
        }


        private void cmdTeleportHeli(IPlayer player, string command, string[] args)
        {
            var ply = player?.Object as BasePlayer;
            if (player.IsServer || ply == null) return;
           
            var tpPos = Vector3.zero;
            var tpCh47 = args.Length > 0 ? args[0].Equals("ch47", StringComparison.OrdinalIgnoreCase) : false;
            if (!tpCh47)
            {
                if (!HasPerms(player.Id, "tpheli"))
                {
                    SendNoPerms(player);
                    return;
                }
                if (HeliInstance == null || HeliInstance?.transform == null)
                {
                    player.Message(GetMessage("noHelisFound", player.Id));
                    return;
                }
                if (HeliCount > 1)
                {
                    player.Message(string.Format(GetMessage("cannotBeCalled", player.Id), HeliCount.ToString("N0")));
                    return;
                }
                tpPos = HeliInstance.transform.position;
            }
            else
            {
                if (!HasPerms(player.Id, "tpch47"))
                {
                    SendNoPerms(player);
                    return;
                }
                if (CH47Instance == null || CH47Instance?.transform == null)
                {
                    player.Message(GetMessage("noHelisFound", player.Id));
                    return;
                }
                if (HeliCount > 1)
                {
                    player.Message(string.Format(GetMessage("cannotBeCalled", player.Id), HeliCount.ToString("N0")));
                    return;
                }
                tpPos = CH47Instance.transform.position;
            }
            
            if (tpPos == Vector3.zero)
            {
                player.Message(GetMessage("noHelisFound", player.Id));
                return;
            }
            TeleportPlayer(ply, GetGround(tpPos));
            player.Message(GetMessage("teleportedToHeli", player.Id));
        }

        object CanPlayerCallHeli(BasePlayer player, bool ch47 = false)
        {
            if (player == null) return null;
            var permMsg = GetNoPerms(player.UserIDString);
            var callPerm = ch47 ? "callch47" : "callheli";
            var callMult = ch47 ? "callmultiplech47" : "callmultiple";
            var callTarget = ch47 ? "callch47target" : "callhelitarget";
            var callSelf = ch47 ? "callch47self" : "callheliself";
            var cooldownTime = GetLowestCooldown(player, ch47);
            var limit = GetHighestLimit(player, ch47);
            var now = DateTime.Now;
            var today = now.ToString("d");
            var cdd = GetCooldownInfo(player.userID);
            if (cdd == null)
            {
                cdd = new CooldownInfo(player);
                cooldownData.cooldownList.Add(cdd);
            }
            var timesCalled = (ch47) ? cdd.TimesCalledCH47 : cdd.TimesCalled;
            var lastCall = (ch47) ? cdd.LastCallDayCH47 : cdd.LastCallDay;
            var coolTime = (ch47) ? cdd.CooldownTimeCH47 : cdd.CooldownTime;
            if (limit < 1 && !ignoreLimits(player) && !HasPerms(player.UserIDString, callPerm)) return permMsg;
            if (!HasPerms(player.UserIDString, callPerm))
            {
                if (!ignoreLimits(player) && limit > 0)
                {
                    if (timesCalled >= limit && today == lastCall) return string.Format(GetMessage("callheliLimit", player.UserIDString), limit);
                    else if (today != lastCall)
                    {
                        if (ch47) cdd.TimesCalledCH47 = 0;
                        else cdd.TimesCalled = 0;
                    }
                }
                if (!ignoreCooldown(player) && cooldownTime > 0.0f && !string.IsNullOrEmpty(coolTime))
                {
                    DateTime cooldownDT;
                    if (!DateTime.TryParse(coolTime, out cooldownDT))
                    {
                        PrintWarning("An error has happened while trying to parse date time ''" + coolTime + "''! Report this issue on plugin thread.");
                        return GetMessage("cmdError", player.UserIDString);
                    }
                    var diff = now - cooldownDT;
                    if (diff.TotalSeconds < cooldownTime)
                    {
                        var cooldownDiff = TimeSpan.FromSeconds(cooldownTime);
                        var waitedString = diff.TotalHours >= 1 ? diff.TotalHours.ToString("N0") + " hours" : diff.TotalMinutes >= 1 ? diff.TotalMinutes.ToString("N0") + " minutes" : diff.TotalSeconds.ToString("N0") + " seconds";
                        var timeToWait = cooldownDiff.TotalHours >= 1 ? cooldownDiff.TotalHours.ToString("N0") + " hours" : cooldownDiff.TotalMinutes >= 1 ? cooldownDiff.TotalMinutes.ToString("N0") + " minutes" : cooldownDiff.TotalSeconds.ToString("N0") + " seconds";
                        return string.Format(GetMessage("callheliCooldown", player.UserIDString), waitedString, timeToWait);
                    }
                }
            }
            if ((ch47 ? CH47Count : HeliCount) > 0 && !HasPerms(player.UserIDString, callMult)) return string.Format(GetMessage("cannotBeCalled", player.UserIDString), ch47 ? CH47Count : HeliCount);
            return null;
        }


        [ChatCommand("callheli")]
        private void cmdCallToPlayer(BasePlayer player, string command, string[] args)
        {
            var argsStr = args.Length > 0 ? string.Join(" ", args) : string.Empty;
            try
            {
                var canCall = CanPlayerCallHeli(player) as string;
                if (!string.IsNullOrEmpty(canCall))
                {
                    SendReply(player, canCall);
                    return;
                }
                
                var now = DateTime.Now;
                var cdd = GetCooldownInfo(player.userID);
                if (cdd == null)
                {
                    cdd = new CooldownInfo(player);
                    cooldownData.cooldownList.Add(cdd);
                }

                if (args.Length == 0)
                {
                    var newHeli = callHeli();
                    if (newHeli != null && permission.UserHasPermission(player.UserIDString, "helicontrol.nodrop")) newHeli.maxCratesToSpawn = 0;
                    SendReply(player, GetMessage("heliCalled", player.UserIDString));
                    cdd.CooldownTime = now.ToString();
                    cdd.LastCallDay = now.ToString("d");
                    cdd.TimesCalled += 1;
                    return;
                }
                var ID = 0ul;
                var target = (ulong.TryParse(args[0], out ID)) ? FindPlayerByID(ID) : FindPlayerByPartialName(args[0]);
                if (target == null)
                {
                    SendReply(player, string.Format(GetMessage("playerNotFound", player.UserIDString), args[0]));
                    return;
                }

                if (target != null && HasPerms(player.UserIDString, "callheliself") && !HasPerms(player.UserIDString, "callhelitarget") && target != player)
                {
                    SendReply(player, string.Format(GetMessage("onlyCallSelf", player.UserIDString), player.displayName));
                    return;
                }
                if (target != null && !HasPerms(player.UserIDString, "callheliself") && !HasPerms(player.UserIDString, "callhelitarget"))
                {
                    SendReply(player, GetMessage("cantCallTargetOrSelf", player.UserIDString));
                    return;
                }

                var num = 1;
                if (args.Length == 2 && HasPerms(player.UserIDString, "callheli") && !int.TryParse(args[1], out num)) num = 1;

                var newHelis = callHelis(num, target.transform.position);
                if (newHelis.Count > 0 && permission.UserHasPermission(player.UserIDString, "helicontrol.nodrop")) for (int i = 0; i < newHelis.Count; i++) newHelis[i].maxCratesToSpawn = 0;
                SendReply(player, string.Format(GetMessage("helisCalledPlayer", player.UserIDString), num, target.displayName));
                cdd.CooldownTime = now.ToString();
                cdd.TimesCalled += 1;
                cdd.LastCallDay = now.ToString("d");
            }
            catch (Exception ex)
            {
                var errorMsg = GetMessage("cmdError", player.UserIDString);
                if (!string.IsNullOrEmpty(errorMsg)) SendReply(player, errorMsg);
                PrintError("Error while using /callheli with args: " + argsStr + System.Environment.NewLine + ex.ToString());
            }
        }

        [ChatCommand("callch47")]
        private void cmdCallCH47(BasePlayer player, string command, string[] args)
        {
            var canCall = CanPlayerCallHeli(player, true) as string;
            if (!string.IsNullOrEmpty(canCall))
            {
                SendReply(player, canCall);
                return;
            }
            var now = DateTime.Now;
            var cdd = GetCooldownInfo(player.userID);
            if (cdd == null)
            {
                cdd = new CooldownInfo(player);
                cooldownData.cooldownList.Add(cdd);
            }

            if (args.Length < 1)
            {
                callChinook();
                SendReply(player, GetMessage("heliCalled", player.UserIDString));
                cdd.CooldownTimeCH47 = now.ToString();
                cdd.LastCallDayCH47 = now.ToString("d");
                cdd.TimesCalledCH47 += 1;
                return;
            }
            var ID = 0ul;
            var target = (ulong.TryParse(args[0], out ID)) ? FindPlayerByID(ID) : FindPlayerByPartialName(args[0]);
            if (target == null)
            {
                SendReply(player, string.Format(GetMessage("playerNotFound", player.UserIDString), args[0]));
                return;
            }
            if (target != null && HasPerms(player.UserIDString, "callch47self") && !HasPerms(player.UserIDString, "callch47target") && target != player)
            {
                SendReply(player, string.Format(GetMessage("onlyCallSelf", player.UserIDString), player.displayName));
                return;
            }
            if (target != null && !HasPerms(player.UserIDString, "callch47self") && !HasPerms(player.UserIDString, "callch47target"))
            {
                SendReply(player, GetMessage("cantCallTargetOrSelf", player.UserIDString));
                return;
            }
            var num = 1;
            if (args.Length == 2 && HasPerms(player.UserIDString, "callch47") && !int.TryParse(args[1], out num)) num = 1;

            var newHelis = callChinooks(num, target.transform.position);
            SendReply(player, string.Format(GetMessage("helisCalledPlayer", player.UserIDString), num, target.displayName));
            cdd.CooldownTimeCH47 = now.ToString();
            cdd.TimesCalledCH47 += 1;
            cdd.LastCallDayCH47 = now.ToString("d");
        }


        private void cmdKillHeli(IPlayer player, string command, string[] args)
        {
            if (!HasPerms(player.Id, "killheli"))
            {
                SendNoPerms(player);
                return;
            }
            var forced = args.Length > 0 ? args[0].Equals("forced", StringComparison.OrdinalIgnoreCase) : false;
            var numKilled = killAllHelis(forced);
            player.Message(string.Format(GetMessage(forced ? "helisForceDestroyed" : "entityDestroyed", player.Id), numKilled.ToString("N0"), "helicopter"));
        }

        private void cmdKillCH47(IPlayer player, string command, string[] args)
        {
            if (!HasPerms(player.Id, "killch47"))
            {
                SendNoPerms(player);
                return;
            }
            var forced = args.Length > 0 ? args[0].Equals("forced", StringComparison.OrdinalIgnoreCase) : false;
            var numKilled = killAllChinooks(forced);
            player.Message(string.Format(GetMessage(forced ? "helisForceDestroyed" : "entityDestroyed", player.Id), numKilled.ToString("N0"), "helicopter"));
        }

        private void cmdUpdateHelicopters(IPlayer player, string command, string[] args)
        {
            if (!HasPerms(player.Id, "update"))
            {
                SendNoPerms(player);
                return;
            }
            CheckHelicopter();
            if (HeliCount < 1)
            {
                player.Message(GetMessage("noHelisFound", player.Id));
                return;
            }
            var count = 0;
            foreach (var helicopter in BaseHelicopters)
            {
                if (helicopter == null) continue;
                UpdateHeli(helicopter, false);
                count++;
            }
            player.Message(string.Format(GetMessage("updatedHelis", player.Id), count));
        }


        private void cmdStrafeHeli(IPlayer player, string command, string[] args)
        {
            if (!HasPerms(player.Id, "strafe"))
            {
                SendNoPerms(player);
                return;
            }
            if (HeliCount < 1)
            {
                player.Message(GetMessage("noHelisFound", player.Id));
                return;
            }
            var lowerArg0 = (args.Length > 0) ? args[0].ToLower() : string.Empty;
            if (HeliCount > 1 && lowerArg0 != "all")
            {
                player.Message(string.Format(GetMessage("cannotBeCalled", player.Id), HeliCount));
                return;
            }
            if (args.Length < 1)
            {
                player.Message(string.Format(GetMessage("invalidSyntax", player.Id), "/strafe", "<player name>"));
                return;
            }
            var ID = 0ul;
            var findArg = (lowerArg0 == "all") ? args[1] : args[0];
            var target = FindPlayerByPartialName(findArg);
            if (ulong.TryParse(findArg, out ID)) target = FindPlayerByID(ID);
            if (target == null)
            {
                player.Message(string.Format(GetMessage("playerNotFound", player.Id), findArg));
                return;
            }
            var targPos = target?.transform?.position ?? Vector3.zero;
            if (lowerArg0 == "all")
            {
                foreach (var heli in BaseHelicopters)
                {
                    var ai = heli?.GetComponent<PatrolHelicopterAI>() ?? null;
                    if (ai != null) StartStrafe(ai, targPos, ai.CanUseNapalm());
                }
            }
            else StartStrafe(HeliInstance, targPos, HeliInstance.CanUseNapalm());
            player.Message(string.Format(GetMessage("strafingOtherPosition", player.Id), target.displayName));
        }


        private void cmdDestChangeHeli(IPlayer player, string command, string[] args)
        {
            if (!HasPerms(player.Id, "destination") && !HasPerms(player.Id, "ch47destination"))
            {
                SendNoPerms(player);
                return;
            }
            if (args.Length < 1)
            {
                player.Message(string.Format(GetMessage("invalidSyntax", player.Id), "/strafe", "<player name>"));
                return;
            }
            var isCh47 = args.Last().Equals("ch47", StringComparison.OrdinalIgnoreCase);
            if ((isCh47 && !HasPerms(player.Id, "ch47destination")) || (!isCh47 && !HasPerms(player.Id, "destination")))
            {
                SendNoPerms(player);
                return;
            }
            if (isCh47 && CH47Count < 1)
            {
                player.Message(GetMessage("noHelisFound", player.Id));
                return;
            }
            else if (!isCh47 && HeliCount < 1)
            {
                player.Message(GetMessage("noHelisFound", player.Id));
                return;
            }
            var lowerArg0 = (args.Length > 0) ? args[0].ToLower() : string.Empty;
            if ((isCh47 && CH47Count > 1 || !isCh47 && HeliCount > 1) && lowerArg0 != "all")
            {
                player.Message(string.Format(GetMessage("cannotBeCalled", player.Id), isCh47 ? CH47Count : HeliCount));
                return;
            }
            var ID = 0ul;
            var findArg = (lowerArg0 == "all") ? args[1] : args[0];
            var target = FindPlayerByPartialName(findArg);
            if (ulong.TryParse(findArg, out ID)) target = FindPlayerByID(ID);
            if (target == null)
            {
                player.Message(string.Format(GetMessage("playerNotFound", player.Id), findArg));
                return;
            }
            var targPos = target?.transform?.position ?? Vector3.zero;
            var newY = GetGround(targPos).y + 10f;
            if (newY > targPos.y) targPos.y = newY;
            if (lowerArg0 == "all")
            {
                if (!isCh47)
                {
                    foreach (var heli in BaseHelicopters)
                    {
                        var ai = heli?.GetComponent<PatrolHelicopterAI>() ?? null;
                        if (ai != null) SetDestination(ai, targPos);
                    }
                }
                else foreach (var ch47 in Chinooks) SetDestination(ch47, targPos);
            }
            else
            {
                if (!isCh47) SetDestination(HeliInstance, targPos);
                else SetDestination(CH47Instance, targPos);
            }
            player.Message(string.Format(GetMessage("destinationOtherPosition", player.Id), target.displayName));
        }


        private void cmdKillFB(IPlayer player, string command, string[] args)
        {
            if (!HasPerms(player.Id, "killnapalm"))
            {
                SendNoPerms(player);
                return;
            }
            player.Message(string.Format(GetMessage("entityDestroyed", player.Id), killAllFB().ToString("N0"), "fireball"));
        }


        private void cmdKillGibs(IPlayer player, string command, string[] args)
        {
            if (!HasPerms(player.Id, "killgibs"))
            {
                SendNoPerms(player);
                return;
            }
            player.Message(string.Format(GetMessage("entityDestroyed", player.Id), killAllGibs().ToString("N0"), "helicopter gib"));
        }


        [ConsoleCommand("callheli")]
        private void consoleCallHeli(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player() ?? null;
            if (player != null && !HasPerms(player.UserIDString, "callheli"))
            {
                SendNoPerms(player);
                return;
            }
            var userIDString = player?.UserIDString ?? string.Empty;
            var noDrop = (player != null) ? permission.UserHasPermission(player.UserIDString, "helicontrol.nodrop") : false;
            var newHelis = new List<BaseHelicopter>();

            if (arg.Args == null || arg?.Args?.Length < 1)
            {
                var newHeli = callHeli();
                if (newHeli != null && noDrop) newHeli.maxCratesToSpawn = 0;
                SendReply(arg, GetMessage("heliCalled", userIDString));
                return;
            }
            if (arg.Args[0].ToLower() == "pos" && arg.Args.Length < 4)
            {
                SendReply(arg, "You must supply 3 args for coordinates!");
                return;
            }

            if (arg.Args[0].ToLower() == "pos")
            {
                var coords = default(Vector3);
                var callNum = 1;
                if (!float.TryParse(arg.Args[1], out coords.x))
                {
                    SendReply(arg, string.Format(GetMessage("invalidCoordinate", userIDString), "X"));
                    return;
                }
                if (!float.TryParse(arg.Args[2], out coords.y))
                {
                    SendReply(arg, string.Format(GetMessage("invalidCoordinate", userIDString), "Y"));
                    return;
                }
                if (!float.TryParse(arg.Args[3], out coords.z))
                {
                    SendReply(arg, string.Format(GetMessage("invalidCoordinate", userIDString), "Z"));
                    return;
                }
                if (!CheckBoundaries(coords.x, coords.y, coords.z))
                {
                    SendReply(arg, GetMessage("coordinatesOutOfBoundaries", userIDString));
                    return;
                }
                if (arg.Args.Length > 4) if (!int.TryParse(arg.Args[4], out callNum)) callNum = 1;
                newHelis = callHelis(callNum, coords);
                if (newHelis.Count > 0 && noDrop) for (int i = 0; i < newHelis.Count; i++) newHelis[i].maxCratesToSpawn = 0;
                SendReply(arg, string.Format(GetMessage("helisCalledPlayer", userIDString), callNum, coords));
                return;
            }

            var ID = 0ul;
            var target = (ulong.TryParse(arg.Args[0], out ID)) ? FindPlayerByID(ID) : FindPlayerByPartialName(arg.Args[0]);

            if (target == null)
            {
                SendReply(arg, string.Format(GetMessage("playerNotFound", userIDString), arg.Args[0]));
                return;
            }

            var num = 1;
            if (arg.Args.Length == 2 && !int.TryParse(arg.Args[1], out num)) num = 1;
            newHelis = callHelis(num, (target?.transform?.position ?? Vector3.zero));
            if (newHelis.Count > 0 && noDrop) for (int i = 0; i < newHelis.Count; i++) newHelis[i].maxCratesToSpawn = 0;
            SendReply(arg, string.Format(GetMessage("helisCalledPlayer", userIDString), num, target.displayName));
        }

        [ConsoleCommand("callch47")]
        private void consoleCallCH47(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player() ?? null;
            if (player != null && !HasPerms(player.UserIDString, "callch47"))
            {
                SendNoPerms(player);
                return;
            }
            var userIDString = player?.UserIDString ?? string.Empty;
            var newHelis = new List<CH47HelicopterAIController>();

            if (arg.Args == null || arg.Args.Length < 1)
            {
                callChinook();
                SendReply(arg, GetMessage("heliCalled", userIDString));
                return;
            }
            if (arg.Args[0].ToLower() == "pos" && arg.Args.Length < 4)
            {
                SendReply(arg, "You must supply 3 args for coordinates!");
                return;
            }

            if (arg.Args[0].ToLower() == "pos")
            {
                var coords = default(Vector3);
                var callNum = 1;
                if (!float.TryParse(arg.Args[1], out coords.x))
                {
                    SendReply(arg, string.Format(GetMessage("invalidCoordinate", userIDString), "X"));
                    return;
                }
                if (!float.TryParse(arg.Args[2], out coords.y))
                {
                    SendReply(arg, string.Format(GetMessage("invalidCoordinate", userIDString), "Y"));
                    return;
                }
                if (!float.TryParse(arg.Args[3], out coords.z))
                {
                    SendReply(arg, string.Format(GetMessage("invalidCoordinate", userIDString), "Z"));
                    return;
                }
                if (!CheckBoundaries(coords.x, coords.y, coords.z))
                {
                    SendReply(arg, GetMessage("coordinatesOutOfBoundaries", userIDString));
                    return;
                }
                if (arg.Args.Length > 4) if (!int.TryParse(arg.Args[4], out callNum)) callNum = 1;
                callChinooks(callNum, coords);
                SendReply(arg, string.Format(GetMessage("helisCalledPlayer", userIDString), callNum, coords));
                return;
            }

            var ID = 0ul;
            var target = (ulong.TryParse(arg.Args[0], out ID)) ? FindPlayerByID(ID) : FindPlayerByPartialName(arg.Args[0]);

            if (target == null)
            {
                SendReply(arg, string.Format(GetMessage("playerNotFound", userIDString), arg.Args[0]));
                return;
            }

            var num = 1;
            if (arg.Args.Length == 2 && !int.TryParse(arg.Args[1], out num)) num = 1;
            callChinooks(num, (target?.transform?.position ?? Vector3.zero));
            SendReply(arg, string.Format(GetMessage("helisCalledPlayer", userIDString), num, target.displayName));
        }

        #endregion
        #region Util
        string ReadableTimeSpan(TimeSpan span, string stringFormat = "N0")
        {
            if (span == TimeSpan.MinValue) return string.Empty;
            var str = string.Empty;
            if (span.TotalHours >= 24) str = (int)span.TotalDays + " day" + (span.TotalDays >= 2 ? "s" : string.Empty) + " " + (span.TotalHours - ((int)span.TotalDays * 24)).ToString(stringFormat) + " hour(s)";
            else if (span.TotalMinutes > 60) str = (int)span.TotalHours + " hour" + (span.TotalHours >= 2 ? "s" : string.Empty) + " " + (span.TotalMinutes - ((int)span.TotalHours * 60)).ToString(stringFormat) + " minute(s)";
            if (!string.IsNullOrEmpty(str)) return str;
            return (span.TotalDays >= 1.0) ? (span.TotalDays.ToString(stringFormat)) + " day" + (span.TotalDays >= 1.5 ? "s" : string.Empty) : (span.TotalHours >= 1.0) ? (span.TotalHours.ToString(stringFormat)) + " hour" + (span.TotalHours >= 1.5 ? "s" : string.Empty) : (span.TotalMinutes >= 1.0) ? (span.TotalMinutes.ToString(stringFormat)) + " minute" + (span.TotalMinutes >= 1.5 ? "s" : string.Empty) : (span.TotalSeconds >= 1.0) ? (span.TotalSeconds.ToString(stringFormat)) + " second" + (span.TotalSeconds >= 1.5 ? "s" : string.Empty) : span.TotalMilliseconds.ToString("N0") + " millisecond" + (span.TotalMilliseconds >= 1.5 ? "s" : string.Empty);
        }

        private bool TeleportPlayer(BasePlayer player, Vector3 dest, bool distChecks = true, bool doSleep = true)
        {
            try
            {
                if (player == null || player?.transform == null) return false;
                var playerPos = player?.transform?.position ?? Vector3.zero;
                var isConnected = player?.IsConnected ?? false;
                var distFrom = Vector3.Distance(playerPos, dest);
                player.SetParent(null, false, false);

                if (distFrom >= 250 && isConnected && distChecks) player.ClientRPCPlayer(null, player, "StartLoading");
                if (doSleep && isConnected && !player.IsSleeping()) player.StartSleeping();
                player.MovePosition(dest);
                if (isConnected)
                {
                    player.ClientRPCPlayer(null, player, "ForcePositionTo", dest);
                    if (distFrom >= 250 && distChecks) player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
                    player.UpdateNetworkGroup();
                    player.SendNetworkUpdate();
                    if (distFrom >= 50)
                    {
                        player.ClearEntityQueue();
                        player.SendFullSnapshot();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                PrintError(ex.ToString());
                return false;
            }
        }

        private float GetRandomSpawnTime(bool ch47 = false)
        {
            if (!ch47) return (MinSpawnTime > 0 && MaxSpawnTime > 0 && MaxSpawnTime >= MinSpawnTime) ? UnityEngine.Random.Range(MinSpawnTime, MaxSpawnTime) : -1f;
            else return (MinSpawnTimeCH47 > 0 && MaxSpawnTimeCH47 > 0 && MaxSpawnTimeCH47 >= MinSpawnTimeCH47) ? UnityEngine.Random.Range(MinSpawnTimeCH47, MaxSpawnTimeCH47) : -1f;
        }
       

        private PatrolHelicopterAI HeliInstance
        {
            get { return PatrolHelicopterAI.heliInstance; }
            set { PatrolHelicopterAI.heliInstance = value; }
        }

        private CH47HelicopterAIController CH47Instance { get; set; }
        

        void StartStrafe(PatrolHelicopterAI heli, Vector3 target, bool useNapalm)
        {
            if (heli == null || !(heli?.IsAlive() ?? false) || (heli?._currentState ?? PatrolHelicopterAI.aiState.DEATH) == PatrolHelicopterAI.aiState.DEATH ||target == Vector3.zero) return;
            heli.interestZoneOrigin = target;
            heli.ExitCurrentState();
            heli.State_Strafe_Enter(target, useNapalm);
        }

        void SetDestination(PatrolHelicopterAI heli, Vector3 target)
        {
            if (heli == null || !heli.IsAlive() || (heli?._currentState ?? PatrolHelicopterAI.aiState.DEATH) == PatrolHelicopterAI.aiState.DEATH || target == Vector3.zero) return;
            heli.interestZoneOrigin = target;
            heli.ExitCurrentState();
            heli.State_Move_Enter(target);
        }

        void SetDestination(CH47HelicopterAIController heli, Vector3 target)
        {
            if (heli == null || heli.IsDestroyed || heli.IsDead() || target == Vector3.zero) return;
            heli.SetMoveTarget(target);
            var brain = heli?.GetComponent<CH47AIBrain>() ?? null;
            if (brain == null) return;
            brain.mainInterestPoint = target;
        }

        private void CheckHelicopter()
        {
            BaseHelicopters.RemoveWhere(p => (p?.IsDestroyed ?? true));
            Chinooks.RemoveWhere(p => (p?.IsDestroyed ?? true));
            Gibs.RemoveWhere(p => (p?.IsDestroyed ?? true));
            FireBalls.RemoveWhere(p => (p?.IsDestroyed ?? true));
            forceCalled.RemoveWhere(p => (p?.IsDestroyed ?? true));
            forceCalledCH.RemoveWhere(p => (p?.IsDestroyed ?? true));
            lockedCrates.RemoveWhere(p => (p?.IsDestroyed ?? true));
        }

        private void UnlockCrate(LockedByEntCrate crate)
        {
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

        private void UnlockCrate(HackableLockedCrate crate)
        {
            if (crate == null || crate.IsDestroyed) return;
            crate.SetFlag(BaseEntity.Flags.Reserved1, true);
            crate.SetFlag(BaseEntity.Flags.Reserved2, true);
            crate.isLootable = true;
            crate.CancelInvoke(new Action(crate.HackProgress));
        }

        private int HeliCount { get { return BaseHelicopters?.Count ?? 0; } }

        private int CH47Count { get { return Chinooks?.Count ?? 0; } }

        CooldownInfo GetCooldownInfo(ulong userId) { return cooldownData?.cooldownList?.Where(p => p?.UserID == userId)?.FirstOrDefault() ?? null; }

        private void SendNoPerms(IPlayer player) => player?.Message(GetMessage("noPerms", player.Id));
        private void SendNoPerms(BasePlayer player) { if (player != null && player.IsConnected) player.ChatMessage(GetMessage("noPerms", player.UserIDString)); }
        private string GetNoPerms(string userID = "") { return GetMessage("noPerms", userID); }

        //**Borrowed from Nogrod's NTeleportation, with permission**//
        private Vector3 GetGround(Vector3 sourcePos)
        {
            var oldPos = sourcePos;
            sourcePos.y = TerrainMeta.HeightMap.GetHeight(sourcePos);
            RaycastHit hitinfo;
            if (Physics.SphereCast(oldPos, .1f, Vector3.down, out hitinfo, 300f, groundLayer)) sourcePos.y = hitinfo.point.y;
            return sourcePos;
        }

        public static Vector3 GetVector3FromString(string vectorStr)
        {
            var vector = Vector3.zero;
            if (string.IsNullOrEmpty(vectorStr)) return vector;
            var split = vectorStr.Replace("(", string.Empty).Replace(")", string.Empty).Split(',');
            return new Vector3(Convert.ToSingle(split[0]), Convert.ToSingle(split[1]), Convert.ToSingle(split[2]));
        }

        private int killAllFB()
        {
            CheckHelicopter();
            var countfb = 0;
            if (FireBalls.Count < 1) return countfb;
            foreach (var fb in FireBalls.ToList())
            {
                if (fb == null || fb.IsDestroyed) continue;
                fb.Kill();
                countfb++;
            }
            CheckHelicopter();
            return countfb;
        }

        private int killAllGibs()
        {
            CheckHelicopter();
            var countgib = 0;
            if (Gibs.Count < 1) return countgib;
            foreach (var Gib in Gibs.ToList())
            {
                if (Gib == null || Gib.IsDestroyed) continue;
                Gib.Kill();
                countgib++;
            }
            CheckHelicopter();
            return countgib;
        }

        T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        private bool HasPerms(string userId, string perm) { return (userId == "server_console" || permission.UserHasPermission(userId, "helicontrol.admin")) ? true : permission.UserHasPermission(userId, (!perm.StartsWith("helicontrol") ? "helicontrol." + perm : perm)); }
        

        private BasePlayer FindPlayerByPartialName(string name, bool sleepers = false)
        {
            if (string.IsNullOrEmpty(name)) return null;
            BasePlayer player = null;
            try
            {
                for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
                {
                    var p = BasePlayer.activePlayerList[i];
                    if (p == null) continue;
                    var pName = p?.displayName ?? string.Empty;
                    if (string.Equals(pName, name, StringComparison.OrdinalIgnoreCase))
                    {
                        if (player != null) return null;
                        player = p;
                        return player;
                    }
                    if (pName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (player != null) return null;
                        player = p;
                        return player;
                    }
                }
                if (sleepers)
                {
                    for (int i = 0; i < BasePlayer.sleepingPlayerList.Count; i++)
                    {
                        var p = BasePlayer.sleepingPlayerList[i];
                        if (p == null) continue;
                        var pName = p?.displayName ?? string.Empty;
                        if (string.Equals(pName, name, StringComparison.OrdinalIgnoreCase))
                        {
                            if (player != null) return null;
                            player = p;
                            return player;
                        }
                        if (pName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            if (player != null) return null;
                            player = p;
                            return player;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                PrintError(ex.ToString());
                return null;
            }
            return player;
        }

        private BasePlayer FindPlayerByID(ulong userID) { return BasePlayer.FindByID(userID) ?? BasePlayer.FindSleeping(userID) ?? null; }


        void RemoveFromWorld(Item item)
        {
            if (item == null) return;
            item.RemoveFromWorld();
            item.RemoveFromContainer();
            item.Remove();
        }

        //CheckBoundaries taken from Nogrod's NTeleportation, with permission
        private bool CheckBoundaries(float x, float y, float z) { return x <= boundary && x >= -boundary && y < 2000 && y >= -100 && z <= boundary && z >= -boundary; }

        private float GetLowestCooldown(BasePlayer player, bool ch47 = false)
        {
            try
            {
                var perms = new List<string>();
                var time = 0f;
                var cont = false;
                var getPerms = permission.GetUserPermissions(player.UserIDString);
                if (getPerms != null && getPerms.Length > 0)
                {
                    for (int i = 0; i < getPerms.Length; i++)
                    {
                        var perm = getPerms[i];
                        if ((perm.Contains("helicontrol.cooldown") && !perm.Contains("ch47") && !ch47) || (perm.Contains("helicontrol.cooldown.ch47") && ch47))
                        {
                            var addPerm = perm.Replace("helicontrol.", string.Empty);
                            if (!ch47) addPerm = addPerm.Replace("cooldown", "Cooldown"); //temp workaround that will probably be permanent for originally named cooldowns & limits
                            perms.Add(addPerm);
                            cont = true;
                        }
                    }
                }
                if (!cont) return time;
                var nums = new HashSet<float>();
                for (int i = 0; i < perms.Count; i++)
                {
                    var perm = perms[i];
                    var tempTime = 0f;
                    object outObj;
                    if (!cds.TryGetValue(perm, out outObj))
                    {
                        PrintWarning("Cooldowns dictionary does not contain: " + perm);
                        continue;
                    }
                    if (outObj == null || !float.TryParse(outObj.ToString(), out tempTime))
                    {
                        PrintWarning("Failed to parse cooldown time! -- report this on plugin thread");
                        continue;
                    }
                    nums.Add(tempTime);
                }
                if (nums.Count > 0) time = nums.Min();
                return time;
            }
            catch (Exception ex)
            {
                PrintError(ex.ToString());
                return -1f;
            }
        }

        private int GetHighestLimit(BasePlayer player, bool ch47 = false)
        {
            try
            {
                var perms = new List<string>();
                var limit = 0;
                var cont = false;
                var getPerms = permission.GetUserPermissions(player.UserIDString);
                if (getPerms != null && getPerms.Length > 0)
                {
                    for (int i = 0; i < getPerms.Length; i++)
                    {
                        var perm = getPerms[i];
                        if ((perm.Contains("helicontrol.limit") && !ch47) || (perm.Contains("helicontrol.limit.ch47") && ch47))
                        {
                            var addPerm = perm.Replace("helicontrol.", string.Empty);
                            if (!ch47) addPerm = addPerm.Replace("limit", "Limit"); //temp workaround that will probably be permanent for originally named cooldowns & limits
                            perms.Add(addPerm);
                            cont = true;
                        }
                    }
                }
                if (!cont) return limit;
                var nums = new HashSet<int>();
                for (int i = 0; i < perms.Count; i++)
                {
                    var perm = perms[i];
                    var tempTime = 0;
                    object outObj;
                    if (!limits.TryGetValue(perm, out outObj))
                    {
                        PrintWarning("Limits dictionary does not contain: " + perm);
                        continue;
                    }
                    if (outObj == null || !int.TryParse(outObj.ToString(), out tempTime))
                    {
                        PrintWarning("Failed to parse limits! -- report this on plugin thread");
                        continue;
                    }
                    nums.Add(tempTime);
                }
                if (nums.Count > 0) limit = nums.Max();
                return limit;
            }
            catch (Exception ex)
            {
                PrintError(ex.ToString());
                return -1;
            }
        }

        private bool ignoreCooldown(BasePlayer player) { return (permission.UserHasPermission(player.UserIDString, "helicontrol.ignorecooldown")); }

        private bool ignoreLimits(BasePlayer player) { return (permission.UserHasPermission(player.UserIDString, "helicontrol.ignorelimits")); }


        #endregion
        #region Classes

        class StoredData
        {
            public List<BoxInventory> HeliInventoryLists = new List<BoxInventory>();
            public StoredData() { }
        }

        class StoredData2
        {
            public Dictionary<string, float> WeaponList = new Dictionary<string, float>();
            public StoredData2() { }
        }

        class StoredData3
        {
            public List<CooldownInfo> cooldownList = new List<CooldownInfo>();
            public StoredData3() { }
        }

        public class HelicopterSpawn
        {
            public enum HeliType { Patrol, Chinook };
            public HeliType Type = HeliType.Patrol;

            [JsonRequired]
            private string _pos = string.Empty;

            [JsonIgnore]
            public Vector3 Position
            {
                get { return string.IsNullOrEmpty(_pos) ? Vector3.zero : GetVector3FromString(_pos); }
                set { _pos = value.ToString(); }
            }

            public string Name { get; set; } = string.Empty;

            public HelicopterSpawn() { }
            public HelicopterSpawn(Vector3 position, HeliType type)
            {
                Position = position;
                Type = type;
            }
            
        }


        class StoredData4
        {
            public List<HelicopterSpawn> HelicopterSpawns = new List<HelicopterSpawn>();
            public StoredData4() { }
        }

        public HelicopterSpawn FindSpawn(string name, StringComparison comparison = StringComparison.Ordinal)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return spawnsData?.HelicopterSpawns?.Where(p => p.Name == name)?.FirstOrDefault() ?? null;
        }

        class CooldownInfo
        {
            public string LastCallDay { get; set; }

            public string CooldownTime { get; set; }

            public int TimesCalled { get; set; }

            public string LastCallDayCH47 { get; set; } = string.Empty;

            public string CooldownTimeCH47 { get; set; } = string.Empty;

            public int TimesCalledCH47 { get; set; }

            public ulong UserID { get; set; }

            public CooldownInfo() { }

            public CooldownInfo(BasePlayer newPlayer) { UserID = newPlayer?.userID ?? 0; }

            public CooldownInfo(string userID)
            {
                var newUID = 0ul;
                if (ulong.TryParse(userID, out newUID)) UserID = newUID;
            }

            public CooldownInfo(ulong userID) { UserID = userID; }

        }

        class BoxInventory
        {
            public List<ItemDef> lootBoxContents = new List<ItemDef>();

            public BoxInventory() { }

            public BoxInventory(List<ItemDef> list) { lootBoxContents = list; }

            public BoxInventory(List<Item> list)
            {
                if (list == null || list.Count < 1) return;
                for (int i = 0; i < list.Count; i++)
                {
                    var item = list[i];
                    if (item == null) continue;
                    var skinID = 0;
                    int.TryParse(item.skin.ToString(), out skinID);
                    lootBoxContents.Add(new ItemDef(item.info.shortname, item.amount, skinID));
                }
            }

            public BoxInventory(string name, int amount, int amountMin = 0, int amountMax = 0, int skinID = 0)
            {
                if (amountMin > 0 && amountMax > 0) amount = UnityEngine.Random.Range(amountMin, amountMax);
                lootBoxContents.Add(new ItemDef(name, amount, skinID));
            }

        }

        class ItemDef
        {
            public string name;
            public int amountMin;
            public int amountMax;
            public int amount;
            public int skinID;

            public ItemDef() { }

            public ItemDef(string name, int amount, int skinID = 0)
            {
                this.name = name;
                this.amount = amount;
                this.skinID = skinID;
            }
        }
        #endregion
        #region Data
        void LoadSavedData()
        {
            lootData = Interface.Oxide?.DataFileSystem?.ReadObject<StoredData>("HeliControlData") ?? null;
            var count = lootData?.HeliInventoryLists?.Count ?? 0;
            //Create a default data file if there was none:
            if (lootData == null || lootData.HeliInventoryLists == null || count < 1)
            {
                Puts("No Lootdrop Data found, creating new file...");
                lootData = new StoredData();
                BoxInventory inv;
                inv = new BoxInventory("rifle.ak", 1);
                inv.lootBoxContents.Add(new ItemDef("ammo.rifle.hv", 128));
                lootData.HeliInventoryLists.Add(inv);

                inv = new BoxInventory("rifle.bolt", 1);
                inv.lootBoxContents.Add(new ItemDef("ammo.rifle.hv", 128));
                lootData.HeliInventoryLists.Add(inv);

                inv = new BoxInventory("explosive.timed", 3);
                inv.lootBoxContents.Add(new ItemDef("ammo.rocket.hv", 3));
                lootData.HeliInventoryLists.Add(inv);

                inv = new BoxInventory("lmg.m249", 1);
                inv.lootBoxContents.Add(new ItemDef("ammo.rifle", 100));
                lootData.HeliInventoryLists.Add(inv);

                inv = new BoxInventory("rifle.lr300", 1);
                inv.lootBoxContents.Add(new ItemDef("ammo.rifle", 100));
                lootData.HeliInventoryLists.Add(inv);

                SaveData();
            }
            else
            {
                var invalidSB = new StringBuilder();
                for(int i = 0; i < lootData.HeliInventoryLists.Count; i++)
                {
                    var inv = lootData.HeliInventoryLists[i];
                    if (inv == null || inv?.lootBoxContents == null || inv.lootBoxContents.Count < 1) continue;
                    for(int j = 0; j < inv.lootBoxContents.Count; j++)
                    {
                        var content = inv.lootBoxContents[j];
                        if (content == null) continue;
                        var findDef = ItemManager.FindItemDefinition(content.name);
                        if (findDef == null) invalidSB.AppendLine("Invalid item name in loot table: " + content.name);
                    }
                }
                if (invalidSB.Length > 0) PrintWarning(invalidSB.ToString().TrimEnd());
            }
        }

        void LoadWeaponData()
        {
            weaponsData = Interface.Oxide?.DataFileSystem?.ReadObject<StoredData2>("HeliControlWeapons") ?? null;
            var count = weaponsData?.WeaponList?.Count ?? 0;
            if (weaponsData == null || weaponsData.WeaponList == null || count < 1)
            {
                Puts("No weapons data found, creating new file...");
                weaponsData = new StoredData2();
                var itemDefs = ItemManager.itemList;
                if (itemDefs != null && itemDefs.Count > 0)
                {
                    for (int i = 0; i < itemDefs.Count; i++)
                    {
                        var itemdef = itemDefs[i];
                        if (itemdef == null) continue;
                        var category = itemdef.category;
                        if (category != ItemCategory.Weapon) continue;
                        var shortName = itemdef.shortname;
                        var englishName = itemdef.displayName?.english ?? shortName;
                        if (!shortName.Contains("weapon.mod")) weaponsData.WeaponList[englishName] = 1f;
                    }
                }
                SaveData2();
            }
        }


        void LoadHeliSpawns() => spawnsData = Interface.Oxide?.DataFileSystem?.ReadObject<StoredData4>("HeliControlSpawns") ?? new StoredData4();
        

        void SaveData() => Interface.Oxide.DataFileSystem.WriteObject("HeliControlData", lootData);
        void SaveData2() => Interface.Oxide.DataFileSystem.WriteObject("HeliControlWeapons", weaponsData);
        void SaveData3() => Interface.Oxide.DataFileSystem.WriteObject("HeliControlCooldowns", cooldownData);
        void SaveData4() => Interface.Oxide.DataFileSystem.WriteObject("HeliControlSpawns", spawnsData);
    }
    #endregion
}
