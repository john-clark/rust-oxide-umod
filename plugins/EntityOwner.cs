using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Facepunch;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info ("Entity Owner", "Calytic", "3.2.0")]
    [Description ("Modify entity ownership and cupboard/turret authorization")]
    class EntityOwner : RustPlugin
    {
        #region Data & Config
        readonly int layerMasks = LayerMask.GetMask ("Construction", "Construction Trigger", "Trigger", "Deployed");

        int EntityLimit = 8000;
        float DistanceThreshold = 3f;
        float CupboardDistanceThreshold = 20f;

        bool debug;

        #endregion

        #region Data Handling & Initialization

        Dictionary<string, string> texts = new Dictionary<string, string> {
            {"Denied: Permission", "You are not allowed to use this command"},
            {"Target: None", "No target found"},
            {"Target: Owner", "Owner: {0}"},
            {"Target: Limit", "Exceeded entity limit."},
            {"Syntax: Owner", "Invalid syntax: /owner"},
            {"Syntax: Own", "Invalid Syntax. \n/own type player\nTypes: all/block/storage/cupboard/sign/sleepingbag/plant/oven/door/turret\n/own player"},
            {"Syntax: Unown", "Invalid Syntax. \n/unown type player\nTypes: all/block/storage/cupboard/sign/sleepingbag/plant/oven/door/turret\n/unown player"},
            {"Syntax: Prod2", "Invalid Syntax. \n/prod2 type \nTypes:\n all/block/entity/storage/cupboard/sign/sleepingbag/plant/oven/door/turret"},
            {"Syntax: Auth", "Invalid Syntax. \n/auth turret player\n/auth cupboard player/auth player\n/auth"},
            {"Syntax: Deauth", "Invalid Syntax. \n/deauth turret player\n/deauth cupboard player/deauth player\n/deauth"},
            {"Ownership: Changing", "Changing ownership.."},
            {"Ownership: Removing", "Removing ownership.."},
            {"Ownership: New", "New owner of all around is: {0}"},
            {"Ownership: New Self", "Owner: You were given ownership of this house and nearby deployables"},
            {"Ownership: Count", "Count ({0})"},
            {"Ownership: Removed", "Ownership removed"},
            {"Ownership: Changed", "Ownership changed"},
            {"Entities: None", "No entities found."},
            {"Entities: Authorized", "({0}) Authorized"},
            {"Entities: Count", "Counted {0} entities ({1}/{2})"},
            {"Structure: Prodding","Prodding structure.."},
            {"Structure: Condition Percent", "Condition: {0}%"},
            {"Player: Unknown Percent", "Unknown: {0}%"},
            {"Player: None", "Target player not found"},
            {"Cupboards: Prodding", "Prodding cupboards.."},
            {"Cupboards: Authorizing", "Authorizing cupboards.."},
            {"Cupboards: Authorized", "Authorized {0} on {1} cupboards"},
            {"Cupboards: Deauthorizing", "Deauthorizing cupboards.."},
            {"Cupboard: Deauthorized", "Deauthorized {0} on {1} cupboards"},
            {"Turrets: Authorized", "Authorized {0} on {1} turrets"},
            {"Turrets: Authorizing", "Authorizing turrets.."},
            {"Turrets: Prodding", "Prodding turrets.."},
            {"Turrets: Deauthorized", "Deauthorized {0} on {1} turrets"},
            {"Turrets: Deauthorizing", "Deauthorizing turrets.."},
            {"Lock: Code", "Code: {0}"}
        };

        // Loads the default configuration
        protected override void LoadDefaultConfig ()
        {
            Config ["VERSION"] = Version.ToString ();
            Config ["EntityLimit"] = 8000;
            Config ["DistanceThreshold"] = 3.0f;
            Config ["CupboardDistanceThreshold"] = 20f;

            Config.Save ();
        }

        new void LoadDefaultMessages ()
        {
            lang.RegisterMessages (texts, this);
        }

        protected void ReloadConfig ()
        {
            Config ["VERSION"] = Version.ToString ();

            // NEW CONFIGURATION OPTIONS HERE
            // END NEW CONFIGURATION OPTIONS

            PrintToConsole ("Upgrading Configuration File");
            SaveConfig ();
        }

        // Gets a config value of a specific type
        T GetConfig<T> (string name, T defaultValue)
        {
            if (Config [name] == null) {
                return defaultValue;
            }

            return (T)Convert.ChangeType (Config [name], typeof (T));
        }

        string GetMsg (string key, BasePlayer player = null)
        {
            return lang.GetMessage (key, this, player == null ? null : player.UserIDString);
        }

        void OnServerInitialized ()
        {
            try {
                LoadConfig ();

                debug = GetConfig ("Debug", false);
                EntityLimit = GetConfig ("EntityLimit", 8000);
                DistanceThreshold = GetConfig ("DistanceThreshold", 3f);
                CupboardDistanceThreshold = GetConfig ("CupboardDistanceThreshold", 20f);

                if (DistanceThreshold >= 5) {
                    PrintWarning ("ALERT: Distance threshold configuration option is ABOVE 5.  This may cause serious performance degradation (lag) when using EntityOwner commands");
                }

                if (!permission.PermissionExists ("entityowner.cancheckowners")) permission.RegisterPermission ("entityowner.cancheckowners", this);
                if (!permission.PermissionExists ("entityowner.cancheckcodes")) permission.RegisterPermission ("entityowner.cancheckcodes", this);
                if (!permission.PermissionExists ("entityowner.canchangeowners")) permission.RegisterPermission ("entityowner.canchangeowners", this);
                if (!permission.PermissionExists ("entityowner.seedetails")) permission.RegisterPermission ("entityowner.seedetails", this);

                LoadData ();
            } catch (Exception ex) {
                PrintError ("OnServerInitialized failed: {0}", ex.Message);
            }
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

        [HookMethod ("SendHelpText")]
        void SendHelpText (BasePlayer player)
        {
            var sb = new StringBuilder ();
            if (canCheckOwners (player) || canChangeOwners (player)) {
                sb.Append ("<size=18>EntityOwner</size> by <color=#ce422b>Calytic</color> at <color=#ce422b>http://rustservers.io</color>\n");
            }

            if (canCheckOwners (player)) {
                sb.Append ("  ").Append ("<color=\"#ffd479\">/prod</color> - Check ownership of entity you are looking at").Append ("\n");
                sb.Append ("  ").Append ("<color=\"#ffd479\">/prod2</color> - Check ownership of entire structure/all deployables").Append ("\n");
                sb.Append ("  ").Append ("<color=\"#ffd479\">/prod2 block</color> - Check ownership structure only").Append ("\n");
                sb.Append ("  ").Append ("<color=\"#ffd479\">/prod2 cupboard</color> - Check authorization on all nearby cupboards").Append ("\n");
                sb.Append ("  ").Append ("<color=\"#ffd479\">/auth</color> - Check authorization list of tool cupboard you are looking at").Append ("\n");
            }

            if (canChangeOwners (player)) {
                sb.Append ("  ").Append ("<color=\"#ffd479\">/own [all/block]</color> - Take ownership of entire structure").Append ("\n");
                sb.Append ("  ").Append ("<color=\"#ffd479\">/own [all/block] PlayerName</color> - Give ownership of entire structure to specified player").Append ("\n");
                sb.Append ("  ").Append ("<color=\"#ffd479\">/unown [all/block]</color> - Remove ownership from entire structure").Append ("\n");
                sb.Append ("  ").Append ("<color=\"#ffd479\">/auth PlayerName</color> - Authorize specified player on all nearby cupboards").Append ("\n");
            }

            SendReply (player, sb.ToString ());
        }

        #endregion



        #region Chat Commands

        [ChatCommand ("prod")]
        void cmdProd (BasePlayer player, string command, string [] args)
        {
            if (!canCheckOwners (player)) {
                SendReply (player, GetMsg ("Denied: Permission", player));
                return;
            }
            if (args == null || args.Length == 0) {
                //var input = serverinput.GetValue(player) as InputState;
                //var currentRot = Quaternion.Euler(input.current.aimAngles) * Vector3.forward;
                //var target = RaycastAll<BaseEntity>(player.transform.position + new Vector3(0f, 1.5f, 0f), currentRot);
                var target = RaycastAll<BaseEntity> (player.eyes.HeadRay ());
                if (target is bool) {
                    SendReply (player, GetMsg ("Target: None", player));
                    return;
                }
                if (target is BaseEntity) {
                    var targetEntity = target as BaseEntity;
                    var owner = GetOwnerName ((BaseEntity)target);
                    if (string.IsNullOrEmpty (owner)) {
                        owner = "N/A";
                    }

                    string msg = string.Format (GetMsg ("Target: Owner", player), owner);

                    if (canSeeDetails (player)) {
                        msg += "\n<color=lightgrey>Name: " + targetEntity.ShortPrefabName + "</color>";
                        if (targetEntity.skinID > 0) {
                            msg += "\n<color=lightgrey>Skin: " + targetEntity.skinID + "</color>";
                        }

                        if (targetEntity.PrefabName != targetEntity.ShortPrefabName) {
                            msg += "\n<color=lightgrey>Prefab: \"" + targetEntity.PrefabName + "\"</color>";
                        }

                        msg += "\n<color=lightgrey>Outside: " + (targetEntity.IsOutside () ? "Yes" : "No") + "</color>";
                    }

                    if (canCheckCodes (player)) {
                        var baseLock = targetEntity.GetSlot (BaseEntity.Slot.Lock);
                        if (baseLock is CodeLock) {
                            CodeLock codeLock = (CodeLock)baseLock;
                            string keyCode = codeLock.code;
                            msg += "\n" + string.Format (GetMsg ("Lock: Code", player), keyCode);
                        }
                    }

                    SendReply (player, msg);
                }
            } else {
                SendReply (player, GetMsg ("Syntax: Owner", player));
            }
        }

        [ChatCommand ("own")]
        void cmdOwn (BasePlayer player, string command, string [] args)
        {
            if (!canChangeOwners (player)) {
                SendReply (player, GetMsg ("Denied: Permission", player));
                return;
            }

            var massTrigger = false;
            string type = null;
            ulong target = player.userID; ;

            if (args.Length == 0) {
                args = new string [1] { "1" };
            }
            if (args.Length > 2) {
                SendReply (player, GetMsg ("Syntax: Own", player));
                return;
            }
            if (args.Length == 1) {
                if (type == "all" || type == "storage" || type == "block" || type == "cupboard" || type == "sign" || type == "sleepingbag" || type == "plant" || type == "oven" || type == "door" || type == "turret") {
                    massTrigger = true;
                    target = player.userID;
                } else if (!string.IsNullOrEmpty (type)) {
                    target = FindUserIDByPartialName (type);
                    type = "1";
                    if (target == 0) {
                        SendReply (player, GetMsg ("Player: None", player));
                    } else {
                        massTrigger = true;
                    }
                } else {
                    massTrigger = true;
                    type = "1";
                }
            } else if (args.Length == 2) {
                type = args [0];
                target = FindUserIDByPartialName (args [1]);
                if (target == 0) {
                    SendReply (player, GetMsg ("Player: None", player));
                } else {
                    massTrigger = true;
                }
            }

            if (!massTrigger || type == null) return;
            switch (type) {
            case "1":
                BaseEntity entity;
                if (TryGetEntity<BaseEntity> (player, out entity)) {
                    ChangeOwner (entity, target);
                    SendReply (player, GetMsg ("Ownership: Changed", player));
                } else {
                    SendReply (player, GetMsg ("Target: None", player));
                }
                break;
            case "all":
                massChangeOwner<BaseEntity> (player, target);
                break;
            case "block":
                massChangeOwner<BuildingBlock> (player, target);
                break;
            case "storage":
                massChangeOwner<StorageContainer> (player, target);
                break;
            case "sign":
                massChangeOwner<Signage> (player, target);
                break;
            case "sleepingbag":
                massChangeOwner<SleepingBag> (player, target);
                break;
            case "plant":
                massChangeOwner<PlantEntity> (player, target);
                break;
            case "oven":
                massChangeOwner<BaseOven> (player, target);
                break;
            case "turret":
                massChangeOwner<AutoTurret> (player, target);
                break;
            case "door":
                massChangeOwner<Door> (player, target);
                break;
            case "cupboard":
                massChangeOwner<BuildingPrivlidge> (player, target);
                break;
            }
        }

        [ChatCommand ("unown")]
        void cmdUnown (BasePlayer player, string command, string [] args)
        {
            if (!canChangeOwners (player)) {
                SendReply (player, GetMsg ("Denied: Permission", player));
                return;
            }

            if (args.Length == 0) {
                args = new [] { "1" };
            }

            if (args.Length > 1) {
                SendReply (player, GetMsg ("Syntax: Unown", player));
                return;
            }
            if (args.Length != 1) return;
            switch (args [0]) {
            case "1":
                BaseEntity entity;
                if (TryGetEntity<BaseEntity> (player, out entity)) {
                    RemoveOwner (entity);
                    SendReply (player, GetMsg ("Ownership: Removed", player));
                } else {
                    SendReply (player, GetMsg ("Target: None", player));
                }
                break;
            case "all":
                massChangeOwner<BaseEntity> (player);
                break;
            case "block":
                massChangeOwner<BuildingBlock> (player);
                break;
            case "storage":
                massChangeOwner<StorageContainer> (player);
                break;
            case "sign":
                massChangeOwner<Signage> (player);
                break;
            case "sleepingbag":
                massChangeOwner<SleepingBag> (player);
                break;
            case "plant":
                massChangeOwner<PlantEntity> (player);
                break;
            case "oven":
                massChangeOwner<BaseOven> (player);
                break;
            case "turret":
                massChangeOwner<AutoTurret> (player);
                break;
            case "door":
                massChangeOwner<Door> (player);
                break;
            case "cupboard":
                massChangeOwner<BuildingPrivlidge> (player);
                break;
            }
        }

        [ChatCommand ("auth")]
        void cmdAuth (BasePlayer player, string command, string [] args)
        {
            if (!canChangeOwners (player)) {
                SendReply (player, GetMsg ("Denied: Permission", player));
                return;
            }

            var massCupboard = false;
            var massTurret = false;
            var checkCupboard = false;
            var checkTurret = false;
            var error = false;
            BasePlayer target = null;

            if (args.Length > 2) {
                error = true;
            } else if (args.Length == 1) {
                if (args [0] == "cupboard") {
                    checkCupboard = true;
                } else if (args [0] == "turret") {
                    checkTurret = true;
                } else {
                    massCupboard = true;
                    target = FindPlayerByPartialName (args [0]);
                }
            } else if (args.Length == 0) {
                checkCupboard = true;
            } else if (args.Length == 2) {
                if (args [0] == "cupboard") {
                    massCupboard = true;
                    target = FindPlayerByPartialName (args [1]);
                } else if (args [0] == "turret") {
                    massTurret = true;
                    target = FindPlayerByPartialName (args [1]);
                } else {
                    error = true;
                }
            }

            if ((massTurret || massCupboard) && target?.net?.connection == null) {
                SendReply (player, GetMsg ("Player: None", player));
                return;
            }

            if (error) {
                SendReply (player, GetMsg ("Syntax: Auth", player));
                return;
            }

            if (massCupboard) {
                massCupboardAuthorize (player, target);
            }

            if (checkCupboard) {
                var priv = RaycastAll<BuildingPrivlidge> (player.eyes.HeadRay ());
                if (priv is bool) {
                    SendReply (player, GetMsg ("Target: None", player));
                    return;
                }
                if (priv is BuildingPrivlidge) {
                    ProdCupboard (player, (BuildingPrivlidge)priv);
                }
            }

            if (massTurret) {
                massTurretAuthorize (player, target);
            }

            if (checkTurret) {
                var turret = RaycastAll<AutoTurret> (player.eyes.HeadRay ());
                if (turret is bool) {
                    SendReply (player, GetMsg ("Target: None", player));
                    return;
                }
                if (turret is AutoTurret) {
                    ProdTurret (player, (AutoTurret)turret);
                }
            }
        }

        [ChatCommand ("deauth")]
        void cmdDeauth (BasePlayer player, string command, string [] args)
        {
            if (!canChangeOwners (player)) {
                SendReply (player, GetMsg ("Denied: Permission", player));
                return;
            }

            var massCupboard = false;
            var massTurret = false;
            var error = false;
            BasePlayer target = null;

            if (args.Length > 2) {
                error = true;
            } else if (args.Length == 1) {
                if (args [0] == "cupboard") {
                    SendReply (player, "Invalid Syntax. /deauth cupboard PlayerName");
                    return;
                }
                if (args [0] == "turret") {
                    SendReply (player, "Invalid Syntax. /deauth turret PlayerName");
                    return;
                }

                massCupboard = true;
                target = FindPlayerByPartialName (args [0]);
            } else if (args.Length == 0) {
                SendReply (player, "Invalid Syntax. /deauth PlayerName\n/deauth turret/cupboard PlayerName");
                return;
            } else if (args.Length == 2) {
                if (args [0] == "cupboard") {
                    massCupboard = true;
                    target = FindPlayerByPartialName (args [1]);
                } else if (args [0] == "turret") {
                    massTurret = true;
                    target = FindPlayerByPartialName (args [1]);
                } else {
                    error = true;
                }
            }

            if ((massTurret || massCupboard) && target?.net?.connection == null) {
                SendReply (player, GetMsg ("Player: None", player));
                return;
            }

            if (error) {
                SendReply (player, GetMsg ("Syntax: Deauth", player));
                return;
            }

            if (massCupboard) {
                massCupboardDeauthorize (player, target);
            }

            if (massTurret) {
                massTurretDeauthorize (player, target);
            }
        }

        [ChatCommand ("prod2")]
        void cmdProd2 (BasePlayer player, string command, string [] args)
        {
            if (!canCheckOwners (player)) {
                SendReply (player, GetMsg ("Denied: Permission", player));
                return;
            }

            bool highlight = false;
            if (args.Length > 0) {
                if (args [0] == "highlight") {
                    highlight = true;
                    args = args.Skip (1).ToArray ();
                }
                if (args.Length == 0) {
                    massProd<BaseEntity> (player, highlight);
                    return;
                }

                switch (args [0]) {
                case "all":
                    args = args.Skip (1).ToArray ();
                    massProd<BaseEntity> (player, highlight, args);
                    break;
                case "block":
                    massProd<BuildingBlock> (player, highlight);
                    break;
                case "storage":
                    massProd<StorageContainer> (player, highlight);
                    break;
                case "sign":
                    massProd<Signage> (player, highlight);
                    break;
                case "sleepingbag":
                    massProd<SleepingBag> (player, highlight);
                    break;
                case "plant":
                    massProd<PlantEntity> (player, highlight);
                    break;
                case "oven":
                    massProd<BaseOven> (player, highlight);
                    break;
                case "turret":
                    massProdTurret (player, highlight);
                    break;
                case "cupboard":
                    massProdCupboard (player, highlight);
                    break;
                default:
                    massProd<BaseEntity> (player, highlight, args);
                    break;
                }
            } else if (args.Length == 0) {
                massProd<BaseEntity> (player);
            } else {
                SendReply (player, GetMsg ("Syntax: Prod2", player));
            }
        }

        #endregion

        #region Permission Checks

        bool canCheckOwners (BasePlayer player)
        {
            if (player == null) return false;
            if (player.net.connection.authLevel > 0) return true;
            return permission.UserHasPermission (player.UserIDString, "entityowner.cancheckowners");
        }

        bool canCheckCodes (BasePlayer player)
        {
            if (player == null) return false;
            if (player.net.connection.authLevel > 0) return true;
            return permission.UserHasPermission (player.UserIDString, "entityowner.cancheckcodes");
        }

        bool canSeeDetails (BasePlayer player)
        {
            if (player == null) return false;
            if (player.net.connection.authLevel > 0) return true;
            return permission.UserHasPermission (player.UserIDString, "entityowner.seedetails");
        }

        bool canChangeOwners (BasePlayer player)
        {
            if (player == null) return false;
            if (player.net.connection.authLevel > 0) return true;
            return permission.UserHasPermission (player.UserIDString, "entityowner.canchangeowners");
        }

        #endregion

        #region Ownership Methods

        bool TryGetEntity<T> (BasePlayer player, out BaseEntity entity) where T : BaseEntity
        {
            entity = null;

            var target = RaycastAll<BaseEntity> (player.eyes.HeadRay ());

            if (target is T) {
                entity = target as T;
                return true;
            }

            return false;
        }

        void massChangeOwner<T> (BasePlayer player, ulong target = 0) where T : BaseEntity
        {
            object entityObject = false;

            if (typeof (T) == typeof (BuildingBlock)) {
                entityObject = FindBuilding (player.transform.position, DistanceThreshold);
            } else {
                entityObject = FindEntity (player.transform.position, DistanceThreshold);
            }

            if (entityObject is bool) {
                SendReply (player, GetMsg ("Entities: None", player));
            } else {
                if (target == 0) {
                    SendReply (player, GetMsg ("Ownership: Removing", player));
                } else {
                    SendReply (player, GetMsg ("Ownership: Changing", player));
                }

                var entity = entityObject as T;
                var entityList = new HashSet<T> ();
                var checkFrom = new List<Vector3> ();
                entityList.Add ((T)entity);
                checkFrom.Add (entity.transform.position);
                var c = 1;
                if (target == 0) {
                    RemoveOwner (entity);
                } else {
                    ChangeOwner (entity, target);
                }
                var current = 0;
                var bbs = 0;
                var ebs = 0;
                if (entity is BuildingBlock) {
                    bbs++;
                } else {
                    ebs++;
                }
                while (true) {
                    current++;
                    if (current > EntityLimit) {
                        if (debug) {
                            SendReply (player, GetMsg ("Target: Limit", player) + " " + EntityLimit);
                        }
                        SendReply (player, string.Format (GetMsg ("Entities: Count", player), c, bbs, ebs));
                        break;
                    }
                    if (current > checkFrom.Count) {
                        SendReply (player, string.Format (GetMsg ("Entities: Count", player), c, bbs, ebs));
                        break;
                    }

                    var hits = FindEntities<T> (checkFrom [current - 1], DistanceThreshold);

                    foreach (var entityComponent in hits.ToList ()) {
                        if (!entityList.Add (entityComponent)) continue;
                        c++;
                        checkFrom.Add (entityComponent.transform.position);

                        if (entityComponent is BuildingBlock) {
                            bbs++;
                        } else {
                            ebs++;
                        }

                        if (target == 0) {
                            RemoveOwner (entityComponent);
                        } else {
                            ChangeOwner (entityComponent, target);
                        }
                    }
                    Pool.FreeList (ref hits);
                }

                if (target == 0) {
                    SendReply (player, string.Format (GetMsg ("Ownership: New", player), "No one"));
                } else {
                    BasePlayer targetPlayer = BasePlayer.FindByID (target);

                    if (targetPlayer != null) {
                        SendReply (player, string.Format (GetMsg ("Ownership: New", player), targetPlayer.displayName));
                        SendReply (targetPlayer, GetMsg ("Ownership: New Self", player));
                    } else {
                        IPlayer pl = covalence.Players.FindPlayerById (target.ToString ());
                        SendReply (player, string.Format (GetMsg ("Target: Owner", player), pl.Name));
                    }
                }
            }
        }

        void massProd<T> (BasePlayer player, bool highlight = false, params string [] filter) where T : BaseEntity
        {
            object entityObject = false;

            entityObject = FindEntity (player.transform.position, DistanceThreshold);
            if (entityObject is bool) {
                SendReply (player, GetMsg ("Entities: None", player));
            } else {
                float health = 0f;
                float maxHealth = 0f;
                var prodOwners = new Dictionary<ulong, int> ();
                var entity = entityObject as BaseEntity;
                if (entity.transform == null) {
                    SendReply (player, GetMsg ("Entities: None", player));
                    return;
                }

                SendReply (player, GetMsg ("Structure: Prodding", player));

                var entityList = new HashSet<T> ();
                var checkFrom = new List<Vector3> ();

                if (entity is T) {
                    entityList.Add ((T)entity);
                }

                var total = 0;
                var skip = false;
                if (entity is T) {
                    if (filter.Length > 0) {
                        skip = true;
                        foreach (var f in filter) {
                            if (entity.name.ToLower ().Contains (f.ToLower ())) {
                                skip = false;
                                break;
                            }
                        }
                    }

                    if (!skip) {
                        prodOwners.Add (entity.OwnerID, 1);
                        health += entity.Health ();
                        maxHealth += entity.MaxHealth ();
                        total++;
                    }
                }

                var current = -1;
                var distanceThreshold = DistanceThreshold;
                if (typeof (T) != typeof (BuildingBlock) && typeof (T) != typeof (BaseEntity))
                    distanceThreshold += 30;

                while (true) {
                    current++;
                    if (current > EntityLimit) {
                        SendReply (player, GetMsg ("Target: Limit", player) + " " + EntityLimit);

                        break;
                    }
                    if (current > checkFrom.Count) {
                        break;
                    }

                    var hits = FindEntities<T> (checkFrom.Count > 0 ? checkFrom [current - 1] : entity.transform.position, distanceThreshold);
                    skip = false;
                    foreach (var fentity in hits) {
                        if (fentity.transform == null || !entityList.Add (fentity) || fentity.name == "player/player")
                            continue;

                        if (filter.Length > 0) {
                            skip = true;
                            foreach (var f in filter) {
                                if (fentity.name.ToLower ().Contains (f.ToLower ())) {
                                    skip = false;
                                    break;
                                }
                            }
                        }

                        checkFrom.Add (fentity.transform.position);

                        if (!skip) {
                            total++;
                            if (highlight) {
                                SendHighlight (player, fentity.transform.position);
                            }

                            var pid = fentity.OwnerID;
                            if (prodOwners.ContainsKey (pid)) {
                                prodOwners [pid]++;
                            } else {
                                prodOwners.Add (pid, 1);
                            }

                            health += fentity.Health ();
                            maxHealth += fentity.MaxHealth ();
                        }
                    }

                    Pool.FreeList (ref hits);
                }

                var unknown = 100;

                var msg = string.Empty;

                msg = "<size=16>Structure</size>\n";
                msg += $"Entities: {total}\n";

                if (health > 0 && maxHealth > 0) {
                    var condition = Mathf.Round (health * 100 / maxHealth);
                    msg += string.Format (GetMsg ("Structure: Condition Percent", player), condition);
                }

                SendReply (player, msg);

                msg = "<size=16>Ownership</size>\n";

                if (total > 0) {
                    foreach (var kvp in prodOwners) {
                        var perc = kvp.Value * 100 / total;
                        if (kvp.Key != 0) {
                            var n = FindPlayerName (kvp.Key);
                            msg += $"{n}: {perc}%\n";
                            unknown -= perc;
                        }
                    }
                }

                if (unknown > 0)
                    msg += string.Format (GetMsg ("Player: Unknown Percent", player), unknown);

                SendReply (player, msg);
            }
        }

        void SendHighlight (BasePlayer player, Vector3 position)
        {
            player.SendConsoleCommand ("ddraw.sphere", 30f, Color.magenta, position, 2f);
            player.SendNetworkUpdateImmediate ();
        }

        void ProdCupboard (BasePlayer player, BuildingPrivlidge cupboard)
        {
            List<string> authorizedUsers;
            var sb = new StringBuilder ();
            if (TryGetCupboardUserNames (cupboard, out authorizedUsers)) {
                sb.AppendLine (string.Format (GetMsg ("Entities: Authorized", player), authorizedUsers.Count));
                foreach (var n in authorizedUsers)
                    sb.AppendLine (n);

            } else
                sb.Append (string.Format (GetMsg ("Target: None", player)));

            SendReply (player, sb.ToString ());
        }

        void ProdTurret (BasePlayer player, AutoTurret turret)
        {
            List<string> authorizedUsers;
            var sb = new StringBuilder ();
            if (TryGetTurretUserNames (turret, out authorizedUsers)) {
                sb.AppendLine (string.Format (GetMsg ("Entities: Authorized", player), authorizedUsers.Count));
                foreach (var n in authorizedUsers)
                    sb.AppendLine (n);
            } else {
                sb.Append (string.Format (GetMsg ("Target: None", player)));
            }

            SendReply (player, sb.ToString ());
        }

        void massProdCupboard (BasePlayer player, bool highlight = false)
        {
            object entityObject = false;

            entityObject = FindEntity (player.transform.position, DistanceThreshold);

            if (entityObject is bool) {
                SendReply (player, GetMsg ("Entities: None", player));
            } else {
                var total = 0;
                var prodOwners = new Dictionary<ulong, int> ();
                SendReply (player, GetMsg ("Cupboards: Prodding", player));
                var entity = entityObject as BaseEntity;
                var entityList = new HashSet<BaseEntity> ();
                var checkFrom = new List<Vector3> ();

                checkFrom.Add (entity.transform.position);

                var current = 0;
                while (true) {
                    current++;
                    if (current > EntityLimit) {
                        if (debug)
                            SendReply (player, GetMsg ("Target: Limit", player) + " " + EntityLimit);

                        SendReply (player, string.Format (GetMsg ("Ownership: Count", player), total));
                        break;
                    }
                    if (current > checkFrom.Count) {
                        SendReply (player, string.Format (GetMsg ("Ownership: Count", player), total));
                        break;
                    }

                    var entities = FindEntities<BuildingPrivlidge> (checkFrom [current - 1], CupboardDistanceThreshold);

                    foreach (var e in entities) {
                        if (!entityList.Add (e)) { continue; }
                        if (highlight) {
                            SendHighlight (player, e.transform.position);
                        }
                        checkFrom.Add (e.transform.position);

                        foreach (var pnid in e.authorizedPlayers) {
                            if (prodOwners.ContainsKey (pnid.userid))
                                prodOwners [pnid.userid]++;
                            else
                                prodOwners.Add (pnid.userid, 1);
                        }

                        total++;
                    }
                    Pool.FreeList (ref entities);
                }

                var percs = new Dictionary<ulong, int> ();
                var unknown = 100;
                if (total > 0) {
                    foreach (var kvp in prodOwners) {
                        var perc = kvp.Value * 100 / total;
                        percs.Add (kvp.Key, perc);
                        var n = FindPlayerName (kvp.Key);

                        if (!n.Contains ("Unknown: ")) {
                            SendReply (player, n + ": " + perc + "%");
                            unknown -= perc;
                        }
                    }

                    if (unknown > 0)
                        SendReply (player, string.Format (GetMsg ("Player: Unknown Percent", player), unknown));
                }
            }
        }

        void massProdTurret (BasePlayer player, bool highlight = false)
        {
            object entityObject = false;

            entityObject = FindEntity (player.transform.position, DistanceThreshold);

            if (entityObject is bool) {
                SendReply (player, GetMsg ("Entities: None", player));
            } else {
                var total = 0;
                var prodOwners = new Dictionary<ulong, int> ();
                SendReply (player, GetMsg ("Turrets: Prodding", player));
                var entity = entityObject as BaseEntity;
                var entityList = new HashSet<BaseEntity> ();
                var checkFrom = new List<Vector3> ();

                checkFrom.Add (entity.transform.position);

                var current = 0;
                while (true) {
                    current++;
                    if (current > EntityLimit) {
                        if (debug)
                            SendReply (player, GetMsg ("Target: Limit", player) + " " + EntityLimit);

                        SendReply (player, string.Format (GetMsg ("Ownership: Count", player), total));
                        break;
                    }
                    if (current > checkFrom.Count) {
                        SendReply (player, string.Format (GetMsg ("Ownership: Count", player), total));
                        break;
                    }

                    var entities = FindEntities<BaseEntity> (checkFrom [current - 1], DistanceThreshold);

                    foreach (var e in entities) {
                        if (!entityList.Add (e)) { continue; }
                        if (highlight) {
                            SendHighlight (player, e.transform.position);
                        }
                        checkFrom.Add (e.transform.position);

                        if (e is AutoTurret) {
                            var turret = e as AutoTurret;
                            if (turret.OwnerID.IsSteamId ()) {
                                if (prodOwners.ContainsKey (turret.OwnerID))
                                    prodOwners [turret.OwnerID]++;
                                else
                                    prodOwners.Add (turret.OwnerID, 1);
                            }

                            foreach (var pnid in turret.authorizedPlayers) {
                                if (prodOwners.ContainsKey (pnid.userid))
                                    prodOwners [pnid.userid]++;
                                else
                                    prodOwners.Add (pnid.userid, 1);
                            }
                        } else if (e is FlameTurret) {
                            var turret = e as FlameTurret;
                            if (turret.OwnerID.IsSteamId ()) {
                                if (prodOwners.ContainsKey (turret.OwnerID))
                                    prodOwners [turret.OwnerID]++;
                                else
                                    prodOwners.Add (turret.OwnerID, 1);
                            }
                        } else {
                            continue;
                        }



                        total++;
                    }

                    Pool.FreeList (ref entities);
                }

                var percs = new Dictionary<ulong, int> ();
                var unknown = 100;
                if (total > 0) {
                    foreach (var kvp in prodOwners) {
                        var perc = kvp.Value * 100 / total;
                        percs.Add (kvp.Key, perc);
                        var n = FindPlayerName (kvp.Key);

                        if (!n.Contains ("Unknown: ")) {
                            SendReply (player, n + ": " + perc + "%");
                            unknown -= perc;
                        }
                    }

                    if (unknown > 0)
                        SendReply (player, string.Format (GetMsg ("Player: Unknown Percent", player), unknown));
                }
            }
        }

        void massCupboardAuthorize (BasePlayer player, BasePlayer target)
        {
            object entityObject = false;

            entityObject = FindEntity (player.transform.position, DistanceThreshold);

            if (entityObject is bool) {
                SendReply (player, GetMsg ("Entities: None", player));
            } else {
                var total = 0;
                SendReply (player, GetMsg ("Cupboards: Authorizing", player));
                var entity = entityObject as BaseEntity;
                var entityList = new HashSet<BaseEntity> ();
                var checkFrom = new List<Vector3> ();

                checkFrom.Add (entity.transform.position);

                var current = 0;
                while (true) {
                    current++;
                    if (current > EntityLimit) {
                        if (debug)
                            SendReply (player, GetMsg ("Target: Limit", player) + " " + EntityLimit);

                        SendReply (player, string.Format (GetMsg ("Ownership: Count", player), total));
                        break;
                    }
                    if (current > checkFrom.Count) {
                        SendReply (player, string.Format (GetMsg ("Ownership: Count", player), total));
                        break;
                    }

                    var entities = FindEntities<BuildingPrivlidge> (checkFrom [current - 1], CupboardDistanceThreshold);

                    foreach (var priv in entities) {
                        if (!entityList.Add (priv)) continue;
                        checkFrom.Add (priv.transform.position);
                        if (HasCupboardAccess (priv, target)) continue;
                        priv.authorizedPlayers.Add (new ProtoBuf.PlayerNameID () {
                            userid = target.userID,
                            username = target.displayName
                        });

                        priv.SendNetworkUpdate (BasePlayer.NetworkQueue.Update);

                        total++;
                    }
                    Pool.FreeList (ref entities);
                }

                SendReply (player, string.Format (GetMsg ("Cupboards: Authorized", player), target.displayName, total));
            }
        }

        void massCupboardDeauthorize (BasePlayer player, BasePlayer target)
        {
            object entityObject = false;

            entityObject = FindEntity (player.transform.position, DistanceThreshold);

            if (entityObject is bool) {
                SendReply (player, GetMsg ("Entities: None", player));
            } else {
                var total = 0;
                SendReply (player, GetMsg ("Cupboards: Deauthorizing", player));
                var entity = entityObject as BaseEntity;
                var entityList = new HashSet<BaseEntity> ();
                var checkFrom = new List<Vector3> ();

                checkFrom.Add (entity.transform.position);

                var current = 0;
                while (true) {
                    current++;
                    if (current > EntityLimit) {
                        if (debug)
                            SendReply (player, GetMsg ("Target: Limit", player) + " " + EntityLimit);

                        SendReply (player, string.Format (GetMsg ("Ownership: Count", player), total));
                        break;
                    }
                    if (current > checkFrom.Count) {
                        SendReply (player, string.Format (GetMsg ("Ownership: Count", player), total));
                        break;
                    }

                    var entities = FindEntities<BuildingPrivlidge> (checkFrom [current - 1], CupboardDistanceThreshold);

                    foreach (var priv in entities) {
                        if (!entityList.Add (priv)) continue;
                        checkFrom.Add (priv.transform.position);

                        if (!HasCupboardAccess (priv, target)) continue;
                        foreach (var p in priv.authorizedPlayers.ToArray ()) {
                            if (p.userid == target.userID) {
                                priv.authorizedPlayers.Remove (p);
                                priv.SendNetworkUpdate (BasePlayer.NetworkQueue.Update);
                            }
                        }

                        total++;
                    }
                    Pool.FreeList (ref entities);
                }

                SendReply (player, string.Format (GetMsg ("Cupboard: Deauthorized", player), target.displayName, total));
            }
        }

        void massTurretAuthorize (BasePlayer player, BasePlayer target)
        {
            object entityObject = false;

            entityObject = FindEntity (player.transform.position, DistanceThreshold);

            if (entityObject is bool) {
                SendReply (player, GetMsg ("Entities: None", player));
            } else {
                var total = 0;
                SendReply (player, GetMsg ("Turrets: Authorizing", player));
                var entity = entityObject as BaseEntity;
                var entityList = new HashSet<BaseEntity> ();
                var checkFrom = new List<Vector3> ();

                checkFrom.Add (entity.transform.position);

                var current = 0;
                while (true) {
                    current++;
                    if (current > EntityLimit) {
                        if (debug)
                            SendReply (player, GetMsg ("Target: Limit", player) + " " + EntityLimit);

                        SendReply (player, string.Format (GetMsg ("Ownership: Count", player), total));
                        break;
                    }
                    if (current > checkFrom.Count) {
                        SendReply (player, string.Format (GetMsg ("Ownership: Count", player), total));
                        break;
                    }

                    var entities = FindEntities<BaseEntity> (checkFrom [current - 1], DistanceThreshold);

                    foreach (var e in entities) {
                        if (!entityList.Add (e)) continue;
                        checkFrom.Add (e.transform.position);

                        var turret = e as AutoTurret;
                        if (turret == null || HasTurretAccess (turret, target)) continue;
                        turret.authorizedPlayers.Add (new ProtoBuf.PlayerNameID () {
                            userid = target.userID,
                            username = target.displayName
                        });

                        turret.SendNetworkUpdate (BasePlayer.NetworkQueue.Update);
                        turret.SetTarget (null);
                        total++;
                    }
                    Pool.FreeList (ref entities);
                }

                SendReply (player, string.Format (GetMsg ("Turrets: Authorized", player), target.displayName, total));
            }
        }

        void massTurretDeauthorize (BasePlayer player, BasePlayer target)
        {
            object entityObject = false;

            entityObject = FindEntity (player.transform.position, DistanceThreshold);

            if (entityObject is bool) {
                SendReply (player, GetMsg ("Entities: None", player));
            } else {
                var total = 0;
                SendReply (player, GetMsg ("Turrets: Deauthorizing", player));
                var entity = entityObject as BaseEntity;
                var entityList = new HashSet<BaseEntity> ();
                var checkFrom = new List<Vector3> ();

                checkFrom.Add (entity.transform.position);

                var current = 0;
                while (true) {
                    current++;
                    if (current > EntityLimit) {
                        if (debug)
                            SendReply (player, GetMsg ("Target: Limit", player) + " " + EntityLimit);

                        SendReply (player, string.Format (GetMsg ("Ownership: Count", player), total));
                        break;
                    }
                    if (current > checkFrom.Count) {
                        SendReply (player, string.Format (GetMsg ("Ownership: Count", player), total));
                        break;
                    }

                    var entities = FindEntities<BaseEntity> (checkFrom [current - 1], DistanceThreshold);

                    foreach (var e in entities) {
                        if (!entityList.Add (e)) continue;
                        checkFrom.Add (e.transform.position);

                        var turret = e as AutoTurret;
                        if (turret == null || !HasTurretAccess (turret, target)) continue;
                        foreach (var p in turret.authorizedPlayers.ToArray ()) {
                            if (p.userid == target.userID) {
                                turret.authorizedPlayers.Remove (p);
                                turret.SetTarget (null);
                                total++;
                            }
                        }

                        turret.SendNetworkUpdate (BasePlayer.NetworkQueue.Update);
                    }
                    Pool.FreeList (ref entities);
                }

                SendReply (player, string.Format (GetMsg ("Turrets: Deauthorized", player), target.displayName, total));
            }
        }

        bool TryGetCupboardUserNames (BuildingPrivlidge cupboard, out List<string> names)
        {
            names = new List<string> ();
            if (cupboard.authorizedPlayers == null)
                return false;
            if (cupboard.authorizedPlayers.Count == 0)
                return false;

            foreach (var pnid in cupboard.authorizedPlayers)
                names.Add ($"{FindPlayerName (pnid.userid)} - {pnid.userid}");

            return true;
        }

        bool TryGetTurretUserNames (AutoTurret turret, out List<string> names)
        {
            names = new List<string> ();
            if (turret.authorizedPlayers == null)
                return false;
            if (turret.authorizedPlayers.Count == 0)
                return false;

            foreach (var pnid in turret.authorizedPlayers)
                names.Add ($"{FindPlayerName (pnid.userid)} - {pnid.userid}");

            return true;
        }

        bool HasCupboardAccess (BuildingPrivlidge cupboard, BasePlayer player)
        {
            return cupboard.IsAuthed (player);
        }

        bool HasTurretAccess (AutoTurret turret, BasePlayer player)
        {
            return turret.IsAuthed (player);
        }

        string GetOwnerName (BaseEntity entity)
        {
            return FindPlayerName (entity.OwnerID);
        }

        BasePlayer GetOwnerPlayer (BaseEntity entity)
        {
            if (entity.OwnerID.IsSteamId ()) {
                return BasePlayer.FindByID (entity.OwnerID);
            }

            return null;
        }

        IPlayer GetOwnerIPlayer (BaseEntity entity)
        {
            if (entity.OwnerID.IsSteamId ()) {
                return covalence.Players.FindPlayerById (entity.OwnerID.ToString ());
            }

            return null;
        }

        void RemoveOwner (BaseEntity entity)
        {
            entity.OwnerID = 0;
        }

        bool ChangeOwner (BaseEntity entity, object player)
        {
            if (player is BasePlayer) {
                entity.OwnerID = ((BasePlayer)player).userID;
                return true;
            }
            if (player is IPlayer) {
                entity.OwnerID = Convert.ToUInt64 (((IPlayer)player).Id);
                return true;
            }

            if (player is ulong && ((ulong)player).IsSteamId ()) {
                entity.OwnerID = (ulong)player;
                return true;
            }

            if (player is string) {
                ulong id;
                if (ulong.TryParse ((string)player, out id) && id.IsSteamId ()) {
                    entity.OwnerID = id;
                    return true;
                }

                var basePlayer = BasePlayer.Find ((string)player);
                if (basePlayer is BasePlayer) {
                    entity.OwnerID = basePlayer.userID;
                    return true;
                }
            }

            return false;
        }

        object FindEntityData (BaseEntity entity)
        {
            if (!entity.OwnerID.IsSteamId ()) {
                return false;
            }

            return entity.OwnerID.ToString ();
        }

        #endregion

        #region Utility Methods

        object RaycastAll<T> (Vector3 position, Vector3 aim) where T : BaseEntity
        {
            var hits = Physics.RaycastAll (position, aim);
            GamePhysics.Sort (hits);
            var distance = 100f;
            object target = false;
            foreach (var hit in hits) {
                var ent = hit.GetEntity ();
                if (ent is T && hit.distance < distance) {
                    target = ent;
                    break;
                }
            }

            return target;
        }

        object RaycastAll<T> (Ray ray) where T : BaseEntity
        {
            var hits = Physics.RaycastAll (ray);
            GamePhysics.Sort (hits);
            var distance = 100f;
            object target = false;
            foreach (var hit in hits) {
                var ent = hit.GetEntity ();
                if (ent is T && hit.distance < distance) {
                    target = ent;
                    break;
                }
            }

            return target;
        }

        object FindBuilding (Vector3 position, float distance = 3f)
        {
            var hit = FindEntity<BuildingBlock> (position, distance);

            if (hit != null) {
                return hit;
            }

            return false;
        }

        object FindEntity (Vector3 position, float distance = 3f, params string [] filter)
        {
            var hit = FindEntity<BaseEntity> (position, distance, filter);

            if (hit != null) {
                return hit;
            }

            return false;
        }

        T FindEntity<T> (Vector3 position, float distance = 3f, params string [] filter) where T : BaseEntity
        {
            var list = Pool.GetList<T> ();
            Vis.Entities (position, distance, list, layerMasks);

            if (list.Count > 0) {
                foreach (var e in list) {
                    if (filter.Length > 0) {
                        foreach (var f in filter) {
                            if (e.name.Contains (f)) {
                                return e;
                            }
                        }
                    } else {
                        return e;
                    }
                }
                Pool.FreeList (ref list);
            }

            return null;
        }

        List<T> FindEntities<T> (Vector3 position, float distance = 3f) where T : BaseEntity
        {
            var list = Pool.GetList<T> ();
            Vis.Entities (position, distance, list, layerMasks);
            return list;
        }

        List<BuildingBlock> GetProfileConstructions (BasePlayer player)
        {
            var result = new List<BuildingBlock> ();
            var blocks = UnityEngine.Object.FindObjectsOfType<BuildingBlock> ();
            foreach (var block in blocks) {
                if (block.OwnerID == player.userID) {
                    result.Add (block);
                }
            }

            return result;
        }

        List<BaseEntity> GetProfileDeployables (BasePlayer player)
        {
            var result = new List<BaseEntity> ();
            var entities = UnityEngine.Object.FindObjectsOfType<BaseEntity> ();
            foreach (var entity in entities) {
                if (entity.OwnerID == player.userID && !(entity is BuildingBlock)) {
                    result.Add (entity);
                }
            }

            return result;
        }

        void ClearProfile (BasePlayer player)
        {
            var entities = UnityEngine.Object.FindObjectsOfType<BaseEntity> ();
            foreach (var entity in entities) {
                if (entity.OwnerID == player.userID && !(entity is BuildingBlock)) {
                    RemoveOwner (entity);
                }
            }
        }

        string FindPlayerName (ulong playerID)
        {
            if (playerID.IsSteamId ()) {
                var player = FindPlayerByPartialName (playerID.ToString ());
                if (player) {
                    if (player.IsSleeping ()) {
                        return $"{player.displayName} [<color=lightblue>Sleeping</color>]";
                    }

                    return $"{player.displayName} [<color=lime>Online</color>]";
                }

                var p = covalence.Players.FindPlayerById (playerID.ToString ());
                if (p != null) {
                    return $"{p.Name} [<color=red>Offline</color>]";
                }
            }

            return $"Unknown: {playerID}";
        }

        ulong FindUserIDByPartialName (string name)
        {
            if (string.IsNullOrEmpty (name))
                return 0;

            ulong userID;
            if (ulong.TryParse (name, out userID)) {
                return userID;
            }

            IPlayer player = covalence.Players.FindPlayer (name);

            if (player != null) {
                return Convert.ToUInt64 (player.Id);
            }

            return 0;
        }

        BasePlayer FindPlayerByPartialName (string name)
        {
            if (string.IsNullOrEmpty (name))
                return null;

            IPlayer player = covalence.Players.FindPlayer (name);

            if (player != null) {
                return (BasePlayer)player.Object;
            }

            return null;
        }

        #endregion
    }
}