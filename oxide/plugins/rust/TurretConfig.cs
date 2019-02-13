using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info ("TurretConfig", "Calytic", "2.0.6", ResourceId = 1418)]
    [Description ("Customized turrets")]
    class TurretConfig : RustPlugin
    {
        readonly string autoTurretPrefab = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab";
        readonly string flameTurretPrefab = "assets/prefabs/npc/flame turret/flameturret.deployed.prefab";
        uint autoTurretPrefabId;
        uint flameTurretPrefabId;

        [PluginReference]
        Plugin Vanish;

        // GLOBAL OVERRIDES
        bool adminOverride;
        List<object> animals;
        bool animalOverride;
        bool sleepOverride;
        bool infiniteAmmo;

        // MODIFIERS
        bool useGlobalDamageModifier;
        float globalDamageModifier;

        // AUTO TURRET DEFAULTS
        float defaultBulletModifier;
        float defaultBulletSpeed;
        string defaultAmmoType;
        float defaultSightRange;
        float defaultAutoHealth;
        float defaultAimCone;

        // AUTO TURRET PERMISSION-BASED SETTINGS
        Dictionary<string, object> bulletModifiers;
        Dictionary<string, object> bulletSpeeds;
        Dictionary<string, object> ammoTypes;
        Dictionary<string, object> sightRanges;
        Dictionary<string, object> autoHealths;
        Dictionary<string, object> aimCones;

        // FLAME TURRET DEFAULTS
        float defaultFlameModifier;
        float defaultArc;
        float defaultTriggerDuration;
        float defaultFlameRange;
        float defaultFlameRadius;
        float defaultFuelPerSec;
        float defaultFlameHealth;

        // FLAME TURRET PERMISSION-BASED SETTINGS
        Dictionary<string, object> flameModifiers;
        Dictionary<string, object> arcs;
        Dictionary<string, object> triggerDurations;
        Dictionary<string, object> flameRanges;
        Dictionary<string, object> flameRadiuses;
        Dictionary<string, object> fuelPerSecs;
        Dictionary<string, object> flameHealths;

        void Init ()
        {
            LoadData ();

            autoTurretPrefabId = StringPool.Get (autoTurretPrefab);
            flameTurretPrefabId = StringPool.Get (flameTurretPrefab);

            permission.RegisterPermission ("turretconfig.infiniteammo", this);

            adminOverride = GetConfig ("Settings", "adminOverride", true);
            animalOverride = GetConfig ("Settings", "animalOverride", false);
            sleepOverride = GetConfig ("Settings", "sleepOverride", false);
            animals = GetConfig ("Settings", "animals", GetPassiveAnimals ());
            infiniteAmmo = GetConfig ("Settings", "infiniteAmmo", false);

            useGlobalDamageModifier = GetConfig ("Settings", "useGlobalDamageModifier", false);
            globalDamageModifier = GetConfig ("Settings", "globalDamageModifier", 1f);

            defaultBulletModifier = GetConfig ("Auto", "defaultBulletModifier", 1f);
            defaultAutoHealth = GetConfig ("Auto", "defaultAutoHealth", 1000f);
            defaultAimCone = GetConfig ("Auto", "defaultAimCone", 5f);
            defaultSightRange = GetConfig ("Auto", "defaultSightRange", 30f);
            defaultBulletSpeed = GetConfig ("Auto", "defaultBulletSpeed", 10f);
            defaultAmmoType = GetConfig ("Auto", "defaultAmmoType", "ammo.rifle");

            bulletModifiers = GetConfig ("Auto", "bulletModifiers", GetDefaultBulletModifiers ());
            bulletSpeeds = GetConfig ("Auto", "bulletSpeeds", GetDefaultBulletSpeeds ());
            ammoTypes = GetConfig ("Auto", "ammoTypes", GetDefaultAmmoTypes ());
            sightRanges = GetConfig ("Auto", "sightRanges", GetDefaultSightRanges ());
            autoHealths = GetConfig ("Auto", "autoHealths", GetDefaultAutoHealths ());
            aimCones = GetConfig ("Auto", "aimCones", GetDefaultAimCones ());

            defaultFlameModifier = GetConfig ("Flame", "defaultFlameModifier", 1f);
            defaultArc = GetConfig ("Flame", "defaultArc", 45f);
            defaultTriggerDuration = GetConfig ("Flame", "defaultTriggerDuration", 5f);
            defaultFlameRange = GetConfig ("Flame", "defaultFlameRange", 7f);
            defaultFlameRadius = GetConfig ("Flame", "defaultFlameRadius", 4f);
            defaultFuelPerSec = GetConfig ("Flame", "defaultFuelPerSec", 1f);
            defaultFlameHealth = GetConfig ("Flame", "defaultFlameHealth", 300f);

            flameModifiers = GetConfig ("Flame", "flameModifiers", GetDefaultFlameModifiers ());
            arcs = GetConfig ("Flame", "arcs", GetDefaultArcs ());
            triggerDurations = GetConfig ("Flame", "triggerDurations", GetDefaultTriggerDurations ());
            flameRanges = GetConfig ("Flame", "flameRanges", GetDefaultFlameRanges ());
            flameRadiuses = GetConfig ("Flame", "flameRadiuses", GetDefaultFlameRadiuses ());
            fuelPerSecs = GetConfig ("Flame", "fuelPerSecs", GetDefaultFuelPerSecs ());
            flameHealths = GetConfig ("Flame", "flameHealths", GetDefaultFlameHealths ());

            LoadPermissions (bulletModifiers);
            LoadPermissions (bulletSpeeds);
            LoadPermissions (ammoTypes);
            LoadPermissions (sightRanges);
            LoadPermissions (autoHealths);
            LoadPermissions (aimCones);

            LoadPermissions (arcs);
            LoadPermissions (triggerDurations);
            LoadPermissions (flameRanges);
            LoadPermissions (flameRadiuses);
            LoadPermissions (fuelPerSecs);
            LoadPermissions (flameHealths);
        }

        void OnServerInitialized ()
        {
            LoadAutoTurrets ();
            LoadFlameTurrets ();

            if (useGlobalDamageModifier) {
                Subscribe ("OnPlayerAttack");
            } else {
                Unsubscribe ("OnPlayerAttack");
            }

            if (infiniteAmmo) {
                Subscribe ("OnItemUse");
                Subscribe ("OnLootEntity");
            } else {
                Unsubscribe ("OnItemUse");
                Unsubscribe ("OnLootEntity");
            }

            if (animalOverride || adminOverride || sleepOverride) {
                Subscribe ("CanBeTargeted");
            } else {
                Unsubscribe ("CanBeTargeted");
            }
        }

        void LoadPermissions (Dictionary<string, object> type)
        {
            foreach (var kvp in type) {
                if (!permission.PermissionExists (kvp.Key)) {
                    if (!string.IsNullOrEmpty (kvp.Key)) {
                        permission.RegisterPermission (kvp.Key, this);
                    }
                }
            }
        }

        protected void LoadFlameTurrets ()
        {
        }

        protected void LoadAutoTurrets ()
        {
            AutoTurret [] turrets = GameObject.FindObjectsOfType<AutoTurret> ();

            if (turrets.Length > 0) {
                int i = 0;
                foreach (AutoTurret turret in turrets.ToList ()) {
                    UpdateAutoTurret (turret);
                    i++;
                }

                PrintWarning ("Configured {0} turrets", i);
            }
        }

        protected override void LoadDefaultConfig ()
        {
            PrintWarning ("Creating new configuration");
            Config.Clear ();

            Config ["Settings", "adminOverride"] = true;
            Config ["Settings", "sleepOverride"] = false;
            Config ["Settings", "animalOverride"] = true;
            Config ["Settings", "useGlobalDamageModifier"] = false;
            Config ["Settings", "globalDamageModifier"] = 1f;

            Config ["Settings", "animals"] = GetPassiveAnimals ();
            Config ["Settings", "infiniteAmmo"] = false;

            Config ["Auto", "defaultBulletModifier"] = 1f;
            Config ["Auto", "defaultBulletSpeed"] = 200f;
            Config ["Auto", "defaultAmmoType"] = "ammo.rifle";
            Config ["Auto", "defaultSightRange"] = 30f;
            Config ["Auto", "defaultAutoHealth"] = 1000;
            Config ["Auto", "defaultAimCone"] = 5f;

            Config ["Auto", "bulletModifiers"] = GetDefaultBulletModifiers ();
            Config ["Auto", "bulletSpeeds"] = GetDefaultBulletSpeeds ();
            Config ["Auto", "ammoTypes"] = GetDefaultAmmoTypes ();
            Config ["Auto", "sightRanges"] = GetDefaultSightRanges ();
            Config ["Auto", "autoHealths"] = GetDefaultAutoHealths ();
            Config ["Auto", "aimCones"] = GetDefaultAimCones ();

            Config ["Flame", "defaultFlameModifier"] = 1f;
            Config ["Flame", "defaultArc"] = 45f;
            Config ["Flame", "defaultTriggerDuration"] = 5f;
            Config ["Flame", "defaultFlameRange"] = 7f;
            Config ["Flame", "defaultFlameRadius"] = 4f;
            Config ["Flame", "defaultFuelPerSec"] = 1f;
            Config ["Flame", "defaultFlameHealth"] = 300f;

            Config ["Flame", "flameModifiers"] = GetDefaultFlameModifiers ();
            Config ["Flame", "arcs"] = GetDefaultArcs ();
            Config ["Flame", "triggerDurations"] = GetDefaultTriggerDurations ();
            Config ["Flame", "flameRanges"] = GetDefaultFlameRanges ();
            Config ["Flame", "flameRadiuses"] = GetDefaultFlameRadiuses ();
            Config ["Flame", "fuelPerSecs"] = GetDefaultFuelPerSecs ();
            Config ["Flame", "flameHealths"] = GetDefaultFlameHealths ();
        }

        protected void ReloadConfig ()
        {
            Config ["VERSION"] = Version.ToString ();

            // NEW CONFIGURATION OPTIONS HERE
            // END NEW CONFIGURATION OPTIONS

            PrintWarning ("Upgrading Configuration File");
            SaveConfig ();
        }

        void LoadDefaultMessages ()
        {
            lang.RegisterMessages (new Dictionary<string, string>
            {
                {"Denied: Permission", "You lack permission to do that"},
            }, this);
        }

        List<object> GetPassiveAnimals ()
        {
            return new List<object>
            {
                "stag",
                "boar",
                "chicken",
                "horse",
            };
        }

        Dictionary<string, object> GetDefaultBulletModifiers ()
        {
            return new Dictionary<string, object> () {
                {"turretconfig.default", 2f},
            };
        }

        Dictionary<string, object> GetDefaultFlameModifiers ()
        {
            return new Dictionary<string, object> () {
                {"turretconfig.default", 2f},
            };
        }

        Dictionary<string, object> GetDefaultBulletSpeeds ()
        {
            return new Dictionary<string, object> () {
                {"turretconfig.default", 200f},
            };
        }

        Dictionary<string, object> GetDefaultSightRanges ()
        {
            return new Dictionary<string, object> () {
                {"turretconfig.default", 30f},
            };
        }

        Dictionary<string, object> GetDefaultAmmoTypes ()
        {
            return new Dictionary<string, object> () {
                {"turretconfig.default", "ammo.rifle"},
            };
        }

        Dictionary<string, object> GetDefaultAutoHealths ()
        {
            return new Dictionary<string, object> () {
                {"turretconfig.default", 1000f},
            };
        }

        Dictionary<string, object> GetDefaultAimCones ()
        {
            return new Dictionary<string, object> () {
                {"turretconfig.default", 5f},
            };
        }

        Dictionary<string, object> GetDefaultArcs ()
        {
            return new Dictionary<string, object> () {
                {"turretconfig.default", 45f},
            };
        }

        Dictionary<string, object> GetDefaultTriggerDurations ()
        {
            return new Dictionary<string, object> () {
                {"turretconfig.default", 5f},
            };
        }

        Dictionary<string, object> GetDefaultFlameRanges ()
        {
            return new Dictionary<string, object> () {
                {"turretconfig.default", 7f},
            };
        }

        Dictionary<string, object> GetDefaultFlameRadiuses ()
        {
            return new Dictionary<string, object> () {
                {"turretconfig.default", 4f},
            };
        }

        Dictionary<string, object> GetDefaultFuelPerSecs ()
        {
            return new Dictionary<string, object> () {
                {"turretconfig.default", 1f},
            };
        }

        Dictionary<string, object> GetDefaultFlameHealths ()
        {
            return new Dictionary<string, object> () {
                {"turretconfig.default", 300f},
            };
        }

        void LoadData ()
        {
            if (Config ["VERSION"] == null) {
                // FOR COMPATIBILITY WITH INITIAL VERSIONS WITHOUT VERSIONED CONFIG
                ReloadConfig ();
            } else if (GetConfig ("VERSION", Version.ToString ()) != Version.ToString ()) {
                // ADDS NEW, IF ANY, CONFIGURATION OPTIONS
                ReloadConfig ();
            }
        }

        [ConsoleCommand ("turrets.reload")]
        void ccTurretReload (ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) {
                if (arg.Connection.authLevel < 1) {
                    SendReply (arg, GetMsg ("Denied: Permission", arg.Connection.userid.ToString ()));
                    return;
                }
            }

            LoadAutoTurrets ();
            LoadFlameTurrets ();
        }

        void OnLootEntity (BasePlayer looter, BaseEntity target)
        {
            if (!infiniteAmmo) return;
            if (!permission.UserHasPermission (target.OwnerID.ToString (), "turretconfig.infiniteammo")) return;

            if (target is AutoTurret || target is FlameTurret) {
                timer.Once (0.01f, looter.EndLooting);
            }
        }

        void OnItemUse (Item item, int amount)
        {
            if (!infiniteAmmo) return;

            var entity = item.parent?.entityOwner;
            if (entity != null) {
                if (entity is AutoTurret) {
                    var autoTurret = entity as AutoTurret;
                    if (autoTurret != null) {
                        if (!permission.UserHasPermission (autoTurret.OwnerID.ToString (), "turretconfig.infiniteammo")) return;
                        item.amount++;
                    }
                }
            }
        }

        void CheckAutoTurretAmmo (AutoTurret turret)
        {
            if (!infiniteAmmo) return;
            if (!permission.UserHasPermission (turret.OwnerID.ToString (), "turretconfig.infiniteammo")) return;

            var items = new List<Item> ();
            var projectile = turret.ammoType.GetComponent<ItemModProjectile> ();
            turret.inventory.FindAmmo (items, projectile.ammoType);

            int total = items.Sum (x => x.amount);

            if (total < 1) {
                turret.inventory.AddItem (turret.ammoType, 1);
            }
        }

        object CanBeTargeted (BaseCombatEntity target, MonoBehaviour turret)
        {
            if (target is BasePlayer) {
                var isInvisible = Vanish?.Call ("IsInvisible", target);
                if (isInvisible != null && (bool)isInvisible) {
                    return null;
                }
            }

            if (!(turret is AutoTurret) && !(turret is FlameTurret)) {
                return null;
            }

            if (animalOverride == true && target.GetComponent<BaseNpc> () != null) {
                if (animals.Count > 0) {
                    if (animals.Contains (target.ShortPrefabName.Replace (".prefab", "").ToLower ())) {
                        return false;
                    }

                    return null;
                }

                return false;
            }

            if (target.ToPlayer () == null) {
                return null;
            }

            BasePlayer targetPlayer = target.ToPlayer ();

            if (adminOverride && targetPlayer.IsConnected && targetPlayer.net.connection.authLevel > 0) {
                return false;
            }
            if (sleepOverride && targetPlayer.IsSleeping ()) {
                return false;
            }

            return null;
        }

        void OnEntitySpawned (BaseNetworkable entity)
        {
            if (entity == null) return;

            if (entity.prefabID == autoTurretPrefabId) {
                UpdateAutoTurret ((AutoTurret)entity, true);
            } else if (entity.prefabID == flameTurretPrefabId) {
                UpdateFlameTurret ((FlameTurret)entity, true);
            }
        }

        T FromPermission<T> (string userID, Dictionary<string, object> options, T defaultValue)
        {
            if (!string.IsNullOrEmpty (userID) && userID != "0") {
                foreach (KeyValuePair<string, object> kvp in options) {
                    if (permission.UserHasPermission (userID, kvp.Key)) {
                        return (T)Convert.ChangeType (kvp.Value, typeof (T));
                    }
                }
            }

            return defaultValue;
        }

        void InitializeTurret (BaseCombatEntity turret, float turretHealth, bool justCreated = false)
        {
            if (justCreated) {
                turret._health = turretHealth;
            }
            turret._maxHealth = turretHealth;

            if (justCreated) {
                turret.InitializeHealth (turretHealth, turretHealth);
            } else {
                turret.InitializeHealth (turret.health, turretHealth);
            }

            turret.startHealth = turretHealth;
        }

        void UpdateFlameTurret (FlameTurret turret, bool justCreated = false)
        {
            string userID = turret.OwnerID.ToString ();

            float turretHealth = FromPermission (userID, flameHealths, defaultFlameHealth);

            InitializeTurret (turret, turretHealth, justCreated);

            turret.arc = FromPermission (userID, arcs, defaultArc);
            turret.triggeredDuration = FromPermission (userID, triggerDurations, defaultTriggerDuration);
            turret.flameRange = FromPermission (userID, flameRanges, defaultFlameRange);
            turret.flameRadius = FromPermission (userID, flameRadiuses, defaultFlameRadius);
            turret.fuelPerSec = FromPermission (userID, fuelPerSecs, defaultFuelPerSec);

            turret.SendNetworkUpdateImmediate (justCreated);
        }

        void UpdateAutoTurret (AutoTurret turret, bool justCreated = false)
        {
            CheckAutoTurretAmmo (turret);

            string userID = turret.OwnerID.ToString ();

            float turretHealth = FromPermission (userID, autoHealths, defaultAutoHealth);
            string ammoType = FromPermission (userID, ammoTypes, defaultAmmoType);

            InitializeTurret (turret, turretHealth, justCreated);

            turret.bulletSpeed = FromPermission (userID, bulletSpeeds, defaultBulletSpeed);
            turret.sightRange = FromPermission (userID, sightRanges, defaultSightRange);
            turret.aimCone = FromPermission (userID, aimCones, defaultAimCone);

            var def = ItemManager.FindItemDefinition (ammoType);
            if (def is ItemDefinition) {
                turret.ammoType = def;
                ItemModProjectile projectile = def.GetComponent<ItemModProjectile> ();
                if (projectile is ItemModProjectile) {
                    turret.gun_fire_effect.guid = projectile.projectileObject.guid;
                    turret.bulletEffect.guid = projectile.projectileObject.guid;
                }
            } else {
                PrintWarning ("No ammo of type ({0})", ammoType);
            }

            turret.Reload ();
            turret.SendNetworkUpdateImmediate (justCreated);
        }

        void OnEntityTakeDamage (BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (hitInfo.Initiator != null) {
                float modifier = 1f;
                if (hitInfo.Initiator.prefabID == autoTurretPrefabId) {
                    modifier = FromPermission (hitInfo.Initiator.OwnerID.ToString (), bulletModifiers, defaultBulletModifier);
                } else if (hitInfo.Initiator.prefabID == flameTurretPrefabId) {
                    modifier = FromPermission (hitInfo.Initiator.OwnerID.ToString (), flameModifiers, defaultFlameModifier);
                }

                hitInfo.damageTypes.ScaleAll (modifier);
            }
        }

        void OnPlayerAttack (BasePlayer attacker, HitInfo hitInfo)
        {
            if (attacker == null || hitInfo == null || hitInfo.HitEntity == null) return;

            if (useGlobalDamageModifier && hitInfo.HitEntity.prefabID == autoTurretPrefabId || hitInfo.HitEntity.prefabID == flameTurretPrefabId) {
                hitInfo.damageTypes.ScaleAll (globalDamageModifier);
                return;
            }
        }

        T GetConfig<T> (string name, T defaultValue)
        {
            if (Config [name] == null) {
                return defaultValue;
            }

            return (T)Convert.ChangeType (Config [name], typeof (T));
        }

        T GetConfig<T> (string name, string name2, T defaultValue)
        {
            if (Config [name, name2] == null) {
                return defaultValue;
            }

            return (T)Convert.ChangeType (Config [name, name2], typeof (T));
        }

        string GetMsg (string key, string userID = null)
        {
            return lang.GetMessage (key, this, userID);
        }
    }
}
