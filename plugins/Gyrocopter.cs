using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using UnityEngine;
using ProtoBuf;
using Rust;
using System.Linq;
using Network;

namespace Oxide.Plugins
{
    [Info("Gyrocopter", "ColonBlow", "1.2.5", ResourceId = 2521)]
    class Gyrocopter : RustPlugin
    {

        // modified ground prox checks.
        // fixed players being able to Pickup Items when hammer is active item

        #region Load

        static LayerMask layerMask;
        BaseEntity newCopter;

        static Dictionary<ulong, PlayerCopterData> loadplayer = new Dictionary<ulong, PlayerCopterData>();
        static List<ulong> pilotslist = new List<ulong>();

        public class PlayerCopterData
        {
            public BasePlayer player;
            public int coptercount;
        }

        void Init()
        {
            lang.RegisterMessages(messages, this);
            LoadVariables();
            layerMask = (1 << 29);
            layerMask |= (1 << 18);
            layerMask = ~layerMask;
            permission.RegisterPermission("gyrocopter.build", this);
            permission.RegisterPermission("gyrocopter.unlimited", this);
        }

        bool isAllowed(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);

        #endregion

        #region Configuration

        bool UseMaxCopterChecks = true;
        public int maxcopters = 1;

        static float MinAltitude = 10f;
        static float RechargeRange = 12f; //Needs to be more than the MinAltitude

        static int NormalCost = 5;
        static int SprintCost = 12;

        static int BonusRechargeRate = 5;
        static int BaseRechargeRate = 1;

        static float NormalSpeed = 12f;
        static float SprintSpeed = 25f;

        static float bombdamageradius = 2f;
        static float bombdamage = 250f;

        static bool enablebombs = true;
        static bool enablestoragebox = true;
        static bool enablepassstoragebox = true;

        bool OwnerLockPaint = true;

        bool Changed;

        void LoadDefaultConfig()
        {
            Puts("Creating a new config file");
            Config.Clear();
            LoadVariables();
        }

        private void LoadConfigVariables()
        {
            CheckCfgFloat("Minimum Flight Altitude : ", ref MinAltitude);
            CheckCfgFloat("Recharge - Range - From substation (must be higher than Min Altitude) : ", ref RechargeRange);
            CheckCfgFloat("Speed - Normal Flight Speed is : ", ref NormalSpeed);
            CheckCfgFloat("Speed - Sprint Flight Speed is : ", ref SprintSpeed);

            CheckCfg("Recharge - Bonus Substation Rate : ", ref BonusRechargeRate);
            CheckCfg("Recharge - Base Rate : ", ref BaseRechargeRate);
            CheckCfg("Movement - Normal - Cost (normal speeed) : ", ref NormalCost);
            CheckCfg("Movement - Sprint - Cost (fast speed) : ", ref SprintCost);
            CheckCfg("Only the Builder (owner) of copter can lock paint job : ", ref OwnerLockPaint);
            CheckCfg("Deploy - Enable limited Gyrocopters per person : ", ref UseMaxCopterChecks);
            CheckCfg("Deploy - Limit of Copters players can build : ", ref maxcopters);
            CheckCfg("Bomb - Amount of Explosive Damage to deal : ", ref bombdamage);
            CheckCfg("Bomb - Radius the damage will effect : ", ref bombdamageradius);
            CheckCfg("Bomb - Enable the use of Bombs ? ", ref enablebombs);
            CheckCfg("Storage Box - Enable storage box under pilot seat ? ", ref enablestoragebox);
            CheckCfg("Storage Box - Enable storage box under passenger seats ? ", ref enablepassstoragebox);
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }

        private void CheckCfgFloat(string Key, ref float var)
        {
            if (Config[Key] != null)
                var = Convert.ToSingle(Config[Key]);
            else
                Config[Key] = var;
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

        #endregion

        #region Language Area

        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"helptext1", "type /copterbuild to spawn a gyrocopter and automount it." },
            {"helptext2", "type /copterlockpaint to lock copter paintjob and /copterunlockpaint to unlock" },
            {"helptext3", "use Spinner wheel while seated to start and stop flying copter." },
            {"helptext4", "Rehcharge - land copter to recharge, hover over substation to fast charge." },
            {"helptext5", "Locking codelock will prevent anyone from using copter (even owner)." },
            {"helptext6", "Once copter runs out of charge, it will autoland." },
            {"helptext7", "To reload the Bomb Barrel on back, use the Reload key while hovering over a barrel." },
            {"helptext8", "To Drop a Bomb, press or click mouse wheel down." },
            {"notauthorized", "You don't have permission to do that !!" },
            {"copterlocked", "You must unlock Copter to start engines !!" },
            {"tellabouthelp", "type /copterhelp to see a list of commands !!" },
            {"dropnet", "Dropping cargo netting !!" },
            {"raisenet", "Raising cargo netting !!" },
            {"notflyingcopter", "You are not piloting a gyrocopter !!" },
            {"maxcopters", "You have reached the maximum allowed copters" },
            {"landingcopter", "Gryocopter Landing Sequence started !!" }
        };

        #endregion

        #region Chat Commands

        [ChatCommand("copterhelp")]
        void chatCopterHelp(BasePlayer player, string command, string[] args)
        {
            SendReply(player, lang.GetMessage("helptext1", this, player.UserIDString));
            SendReply(player, lang.GetMessage("helptext2", this, player.UserIDString));
            SendReply(player, lang.GetMessage("helptext3", this, player.UserIDString));
            SendReply(player, lang.GetMessage("helptext4", this, player.UserIDString));
            SendReply(player, lang.GetMessage("helptext5", this, player.UserIDString));
            SendReply(player, lang.GetMessage("helptext6", this, player.UserIDString));
            SendReply(player, lang.GetMessage("helptext7", this, player.UserIDString));
            SendReply(player, lang.GetMessage("helptext8", this, player.UserIDString));
        }

        [ChatCommand("copterbuild")]
        void chatBuildCopter(BasePlayer player, string command, string[] args)
        {
            if (!isAllowed(player, "gyrocopter.build")) { SendReply(player, lang.GetMessage("notauthorized", this, player.UserIDString)); return; }
            if (CopterLimitReached(player)) { SendReply(player, lang.GetMessage("maxcopters", this, player.UserIDString)); return; }
            AddCopter(player, player.transform.position);
        }


        [ConsoleCommand("copterbuild")]
        void cmdConsoleBuildCopter(ConsoleSystem.Arg arg)
        {
            var player = arg.Player() ?? null;
            if (player == null) return;
            if (!isAllowed(player, "gyrocopter.build")) { SendReply(player, lang.GetMessage("notauthorized", this, player.UserIDString)); return; }
            if (CopterLimitReached(player)) { SendReply(player, lang.GetMessage("maxcopters", this, player.UserIDString)); return; }
            AddCopter(player, player.transform.position);
        }

        [ChatCommand("copterdropnet")]
        void chatDropNetCopter(BasePlayer player, string command, string[] args)
        {
            if (!player.isMounted) return;
            var activecopter = player.GetMounted().GetComponentInParent<GyroCopter>();
            if (activecopter == null) return;
            if (activecopter.islanding || !activecopter.engineon) return;
            activecopter.DropNet();
        }

        [ChatCommand("copterlockpaint")]
        void chatCopterLockPaint(BasePlayer player, string command, string[] args)
        {
            if (!player.isMounted) return;
            var activecopter = player.GetMounted().GetComponentInParent<GyroCopter>();
            if (activecopter == null) return;
            if (activecopter.islanding) return;
            if (OwnerLockPaint && activecopter.ownerid != player.userID) return;
            if (!activecopter.paintingsarelocked) { activecopter.LockPaintings(); return; }
        }

        [ChatCommand("copterunlockpaint")]
        void chatCopterUnLockPaint(BasePlayer player, string command, string[] args)
        {
            if (!player.isMounted) return;
            var activecopter = player.GetMounted().GetComponentInParent<GyroCopter>();
            if (activecopter == null) return;
            if (activecopter.islanding) return;
            if (OwnerLockPaint && activecopter.ownerid != player.userID) return;
            if (activecopter.paintingsarelocked) { activecopter.UnLockPaintings(); return; }
        }

        private void AddCopter(BasePlayer player, Vector3 location)
        {
            if (player == null && location == null) return;
            if (location == null && player != null) location = player.transform.position;
            var spawnpos = new Vector3(location.x, location.y + 0.5f, location.z);
            string staticprefab = "assets/prefabs/deployable/chair/chair.deployed.prefab";
            newCopter = GameManager.server.CreateEntity(staticprefab, spawnpos, new Quaternion(), true);
            var chairmount = newCopter.GetComponent<BaseMountable>();
            chairmount.isMobile = true;
            newCopter.enableSaving = false;
            newCopter.OwnerID = player.userID;
            newCopter.Spawn();
            var gyrocopter = newCopter.gameObject.AddComponent<GyroCopter>();
            AddPlayerID(player.userID);
            if (chairmount != null && player != null) { chairmount.MountPlayer(player); return; }
            var passengermount = newCopter.GetComponent<GyroCopter>().passengerchair1.GetComponent<BaseMountable>();
            if (passengermount != null && player != null && isAllowed(player, "gyrocopter.build")) { passengermount.MountPlayer(player); return; }
        }

        [ChatCommand("copterswag")]
        void chatGetCopterSwag(BasePlayer player, string command, string[] args)
        {
            if (!isAllowed(player, "gyrocopter.build")) { SendReply(player, lang.GetMessage("notauthorized", this, player.UserIDString)); return; }
            Item num = ItemManager.CreateByItemID(-864578046, 1, 961776748);
            player.inventory.GiveItem(num, null);
            player.Command("note.inv", -864578046, 1);
        }

        [ChatCommand("coptercount")]
        void cmdChatCopterCount(BasePlayer player, string command, string[] args)
        {
            if (!loadplayer.ContainsKey(player.userID))
            {
                SendReply(player, "You have no GyroCopters");
                return;
            }
            SendReply(player, "Current Copters : " + (loadplayer[player.userID].coptercount));
        }

        [ChatCommand("copterdestroy")]
        void cmdChatCopterDestroy(BasePlayer player, string command, string[] args)
        {
            RemoveCopter(player);
            DestroyLocalCopter(player);
        }

        #endregion

        #region Hooks

        public bool PilotListContainsPlayer(BasePlayer player)
        {
            if (pilotslist.Contains(player.userID)) return true;
            return false;
        }

        void AddPlayerToPilotsList(BasePlayer player)
        {
            if (PilotListContainsPlayer(player)) return;
            pilotslist.Add(player.userID);
        }

        public void RemovePlayerFromPilotsList(BasePlayer player)
        {
            if (PilotListContainsPlayer(player))
            {
                pilotslist.Remove(player.userID);
                return;
            }
        }

        void DestroyLocalCopter(BasePlayer player)
        {
            if (player == null) return;
            List<BaseEntity> copterlist = new List<BaseEntity>();
            Vis.Entities<BaseEntity>(player.transform.position, 10f, copterlist);

            foreach (BaseEntity p in copterlist)
            {
                var foundent = p.GetComponentInParent<GyroCopter>() ?? null;
                if (foundent != null)
                {
                    if (foundent.ownerid != player.userID) return;
                    foundent.entity.Kill(BaseNetworkable.DestroyMode.Gib);
                }
            }
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || input == null) return;
            if (!player.isMounted) return;
            var activecopter = player.GetMounted().GetComponentInParent<GyroCopter>() ?? null;
            if (activecopter == null) return;
            if (player.GetMounted() != activecopter.entity) return;
            if (input != null)
            {
                activecopter.CopterInput(input, player);
            }
            return;
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity == null || hitInfo == null) return;
            var iscopter = entity.GetComponentInParent<GyroCopter>() ?? null;
            if (iscopter != null) hitInfo.damageTypes.ScaleAll(0);
            return;
        }

        object OnEntityGroundMissing(BaseEntity entity)
        {
            var iscopter = entity.GetComponentInParent<GyroCopter>() ?? null;
            if (iscopter != null) return true;
            return null;
        }

        void OnSpinWheel(BasePlayer player, SpinnerWheel wheel)
        {
            if (!player.isMounted) return;
            var activecopter = player.GetMounted().GetComponentInParent<GyroCopter>() ?? null;
            if (activecopter == null) return;
            if (activecopter.copterlock != null && activecopter.copterlock.IsLocked()) { SendReply(player, lang.GetMessage("copterlocked", this, player.UserIDString)); return; }
            if (player.GetMounted() != activecopter.entity) return;
            if (activecopter != null)
            {
                var ison = activecopter.engineon;
                if (ison) { activecopter.islanding = true; SendReply(player, lang.GetMessage("landingcopter", this, player.UserIDString)); wheel.velocity = 0f; return; }
                if (!ison) { AddPlayerToPilotsList(player); activecopter.engineon = true; wheel.velocity = 0f; return; }
            }
        }

        bool CopterLimitReached(BasePlayer player)
        {
            if (isAllowed(player, "gyrocopter.unlimited")) return false;
            if (UseMaxCopterChecks)
            {
                if (loadplayer.ContainsKey(player.userID))
                {
                    var currentcount = loadplayer[player.userID].coptercount;
                    var maxallowed = maxcopters;
                    if (currentcount >= maxallowed) return true;
                }
            }
            return false;
        }

        object CanDismountEntity(BasePlayer player, BaseMountable entity)
        {
            if (player == null) return null;
            if (PilotListContainsPlayer(player)) return true;
            return null;
        }

        void OnEntityMounted(BaseMountable mountable, BasePlayer player)
        {
            var activecopter = mountable.GetComponentInParent<GyroCopter>() ?? null;
            if (activecopter != null)
            {
                if (mountable.GetComponent<BaseEntity>() != activecopter.entity) return;
                player.gameObject.AddComponent<FuelControl>();
            }
        }

        void OnEntityDismounted(BaseMountable mountable, BasePlayer player)
        {
            var activecopter = mountable.GetComponentInParent<GyroCopter>() ?? null;
            if (activecopter != null)
            {
                if (mountable.GetComponent<BaseEntity>() != activecopter.entity) return;
                var fuelcontrol = player.GetComponent<FuelControl>() ?? null;
                if (fuelcontrol != null) fuelcontrol.OnDestroy();
            }
        }

        object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (container == null || player == null) return null;
            var iscopter = container.GetComponentInParent<GyroCopter>() ?? null;
            if (iscopter != null)
            {
                if (iscopter.copterlock != null && iscopter.copterlock.IsLocked()) return true;
            }
            return null;
        }

        object CanPickupEntity(BasePlayer player, BaseCombatEntity entity)
        {
            if (entity == null || player == null) return null;
            if (entity.GetComponentInParent<GyroCopter>()) return false;
            return null;
        }

        void AddPlayerID(ulong ownerid)
        {
            if (!loadplayer.ContainsKey(ownerid))
            {
                loadplayer.Add(ownerid, new PlayerCopterData
                {
                    coptercount = 1
                });
                return;
            }
            loadplayer[ownerid].coptercount = loadplayer[ownerid].coptercount + 1;
        }

        void RemovePlayerID(ulong ownerid)
        {
            if (loadplayer.ContainsKey(ownerid)) loadplayer[ownerid].coptercount = loadplayer[ownerid].coptercount - 1;
            return;
        }

        object OnPlayerDie(BasePlayer player, HitInfo info)
        {
            RemovePlayerFromPilotsList(player);
            return null;
        }

        void RemoveCopter(BasePlayer player)
        {
            var hasgyro = player.GetComponent<FuelControl>() ?? null;
            if (hasgyro != null) GameObject.Destroy(hasgyro);
            RemovePlayerFromPilotsList(player);
            return;
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            RemoveCopter(player);
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            RemoveCopter(player);
        }

        void DestroyAll<T>()
        {
            var objects = GameObject.FindObjectsOfType(typeof(T));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
        }

        void Unload()
        {
            DestroyAll<GyroCopter>();
            DestroyAll<FuelControl>();
        }

        #endregion

        #region Copter Antihack check

        static List<BasePlayer> copterantihack = new List<BasePlayer>();

        object OnPlayerViolation(BasePlayer player, AntiHackType type, float amount)
        {
            if (player == null) return null;
            if (copterantihack.Contains(player)) return false;
            return null;
        }

        #endregion

        #region GyroCopter Entity

        class GyroCopter : BaseEntity
        {
            public BaseEntity entity;
            public BasePlayer player;
            public BaseEntity wheel;
            public BaseEntity deck1;
            public BaseEntity deck2;
            public BaseEntity barrel;
            public BaseEntity barcenter;
            public BaseEntity rotor1;
            public BaseEntity rotor2;
            public BaseEntity rotor3;
            public BaseEntity rotor4;
            public BaseEntity skid1;
            public BaseEntity skid2;
            public BaseEntity skid3;
            public BaseEntity skid4;
            public BaseEntity tailrotor1;
            public BaseEntity tailrotor2;
            public BaseEntity floor;
            public BaseEntity nosesign;
            public BaseEntity tail1;
            public BaseEntity tail2;
            public BaseEntity passengerchair1;
            public BaseEntity passengerchair2;
            public BaseEntity copterlock;
            public BaseEntity panel;
            public BaseEntity dabomb;
            public BaseEntity pilotstorage;
            public BaseEntity passstorage1;
            public BaseEntity passstorage2;

            Quaternion entityrot;
            Vector3 entitypos;

            public bool moveforward;
            public bool movebackward;
            public bool moveup;
            public bool movedown;
            public bool rotright;
            public bool rotleft;
            public bool sprinting;
            public bool islanding;
            public bool hasbonuscharge;
            public bool paintingsarelocked;

            public ulong ownerid;
            int count;
            public bool engineon;
            float minaltitude;
            Gyrocopter instance;
            public bool throttleup;
            int sprintcost;
            int normalcost;
            float sprintspeed;
            float normalspeed;
            public int currentfuel;
            int baserechargerate;
            int bonusrechargerate;
            bool isenabled;
            bool hasdabomb;
            SphereCollider sphereCollider;

            string prefabdeck = "assets/prefabs/deployable/signs/sign.post.town.prefab";
            string prefabbar = "assets/prefabs/deployable/signs/sign.post.single.prefab";
            string prefabrotor = "assets/prefabs/deployable/signs/sign.pictureframe.tall.prefab";
            string prefabbarrel = "assets/prefabs/deployable/liquidbarrel/waterbarrel.prefab";
            string prefabbomb = "assets/bundled/prefabs/radtown/oil_barrel.prefab";
            string prefabfloor = "assets/prefabs/building/floor.grill/floor.grill.prefab";
            string prefabnosesign = "assets/prefabs/deployable/signs/sign.medium.wood.prefab";
            string prefabpanel = "assets/prefabs/deployable/signs/sign.small.wood.prefab";
            string prefabskid = "assets/prefabs/deployable/mailbox/mailbox.deployed.prefab";
            string wheelprefab = "assets/prefabs/deployable/spinner_wheel/spinner.wheel.deployed.prefab";
            string copterlockprefab = "assets/prefabs/locks/keypad/lock.code.prefab";
            string prefabchair = "assets/prefabs/deployable/chair/chair.deployed.prefab";
            string prefabnetting = "assets/prefabs/building/wall.frame.netting/wall.frame.netting.prefab";
            string prefabdabomb = "assets/bundled/prefabs/radtown/oil_barrel.prefab";

            void Awake()
            {
                entity = GetComponentInParent<BaseEntity>();
                entityrot = Quaternion.identity;
                entitypos = entity.transform.position;
                minaltitude = MinAltitude;
                instance = new Gyrocopter();
                ownerid = entity.OwnerID;
                gameObject.name = "Gyrocopter";
                baserechargerate = BaseRechargeRate;
                bonusrechargerate = BonusRechargeRate;

                engineon = false;
                moveforward = false;
                movebackward = false;
                moveup = false;
                movedown = false;
                rotright = false;
                rotleft = false;
                sprinting = false;
                islanding = false;
                throttleup = false;
                hasbonuscharge = false;
                paintingsarelocked = false;
                sprintcost = SprintCost;
                sprintspeed = SprintSpeed;
                normalcost = NormalCost;
                normalspeed = NormalSpeed;
                currentfuel = 10000;
                isenabled = false;
                hasdabomb = false;
                SpawnCopter();
                if (enablestoragebox) SpawnFuelBox();
                ReloadDaBombs();

                sphereCollider = entity.gameObject.AddComponent<SphereCollider>();
                sphereCollider.gameObject.layer = (int)Layer.Reserved1;
                sphereCollider.isTrigger = true;
                sphereCollider.radius = 6f;
            }

            BaseEntity SpawnPart(string prefab, BaseEntity entitypart, bool setactive, int eulangx, int eulangy, int eulangz, float locposx, float locposy, float locposz, BaseEntity parent, ulong skinid)
            {
                entitypart = new BaseEntity();
                entitypart = GameManager.server.CreateEntity(prefab, entitypos, entityrot, setactive);
                entitypart.transform.localEulerAngles = new Vector3(eulangx, eulangy, eulangz);
                entitypart.transform.localPosition = new Vector3(locposx, locposy, locposz);
                entitypart.SetParent(parent, 0);
                entitypart.skinID = skinid;
                entitypart?.Spawn();
                SpawnRefresh(entitypart);
                return entitypart;
            }

            void SpawnRefresh(BaseEntity entity)
            {
                var hasstab = entity.GetComponent<StabilityEntity>() ?? null;
                if (hasstab != null)
                {
                    hasstab.grounded = true;
                }
                var hasmount = entity.GetComponent<BaseMountable>() ?? null;
                if (hasmount != null)
                {
                    hasmount.isMobile = true;
                }
            }

            public void SpawnCopter()
            {
                deck1 = SpawnPart(prefabdeck, deck1, false, 90, 0, 0, 0f, 0f, -0.4f, entity, 1);
                deck2 = SpawnPart(prefabdeck, deck2, false, -90, 0, 0, 0f, 0f, -0.2f, entity, 1);
                barrel = SpawnPart(prefabbarrel, barrel, false, 0, 180, 0, 0f, -0.5f, -1.1f, entity, 1);
                barcenter = SpawnPart(prefabbar, barcenter, false, 0, 0, 0, 0f, 1f, -1.1f, entity, 1);
                rotor1 = SpawnPart(prefabrotor, rotor1, false, 90, 0, 0, 0f, 2f, 0.6f, barcenter, 1);
                rotor2 = SpawnPart(prefabrotor, rotor2, false, 90, 0, 0, 0f, 2f, -3f, barcenter, 1);
                rotor3 = SpawnPart(prefabbar, rotor3, false, 90, 90, 0, -2f, 2f, 0f, barcenter, 1);
                rotor4 = SpawnPart(prefabbar, rotor4, false, -90, 90, 0, 2f, 2f, 0f, barcenter, 1);
                floor = SpawnPart(prefabdeck, floor, false, -90, 0, 0, 0f, 0f, 1.4f, entity, 1);
                nosesign = SpawnPart(prefabnosesign, nosesign, true, 310, 0, 0, 0f, -0.3f, 2.4f, entity, 1);

                tail1 = SpawnPart(prefabbar, tail1, false, 90, 90, -90, 0.5f, 0f, -2.5f, entity, 1);
                tail2 = SpawnPart(prefabbar, tail2, false, 90, 90, -90, -0.5f, 0f, -2.5f, entity, 1);
                tailrotor1 = SpawnPart(prefabbar, tailrotor1, false, 0, 90, -90, 0.5f, 0f, -2.5f, entity, 1);
                tailrotor2 = SpawnPart(prefabbar, tailrotor2, false, 0, 90, -90, -0.5f, 0f, -2.5f, entity, 1);

                skid1 = SpawnPart(prefabskid, skid1, false, -90, 0, 0, 0.8f, -0.5f, 0.5f, entity, 1);
                skid2 = SpawnPart(prefabskid, skid2, false, -90, 0, 0, -0.8f, -0.5f, 0.5f, entity, 1);
                skid3 = SpawnPart(prefabskid, skid3, false, -90, 180, 0, 0.8f, -0.5f, -2.6f, entity, 1);
                skid4 = SpawnPart(prefabskid, skid4, false, -90, 180, 0, -0.8f, -0.5f, -2.6f, entity, 1);

                wheel = SpawnPart(wheelprefab, wheel, true, 90, 0, 90, 0.8f, 0.4f, 0f, entity, 1);
                panel = SpawnPart(prefabpanel, panel, false, 210, 0, 0, 0f, 0.4f, 1.7f, entity, 1);
                copterlock = SpawnPart(copterlockprefab, copterlock, true, 0, 90, 30, 0f, 0.3f, 1.55f, entity, 1);

                passengerchair1 = SpawnPart(prefabchair, passengerchair1, true, 0, 90, 0, 0.7f, -0.1f, -2.1f, entity, 1);
                passengerchair2 = SpawnPart(prefabchair, passengerchair2, true, 0, 270, 0, -0.7f, -0.1f, -2.1f, entity, 1);

                skid1.SetFlag(BaseEntity.Flags.Busy, true, true);
                skid2.SetFlag(BaseEntity.Flags.Busy, true, true);
                skid3.SetFlag(BaseEntity.Flags.Busy, true, true);
                skid4.SetFlag(BaseEntity.Flags.Busy, true, true);
                tail1.SetFlag(BaseEntity.Flags.Busy, true, true);
                tail2.SetFlag(BaseEntity.Flags.Busy, true, true);
                tailrotor1.SetFlag(BaseEntity.Flags.Busy, true, true);
                tailrotor2.SetFlag(BaseEntity.Flags.Busy, true, true);
                deck1.SetFlag(BaseEntity.Flags.Busy, true, true);
                deck2.SetFlag(BaseEntity.Flags.Busy, true, true);
                floor.SetFlag(BaseEntity.Flags.Busy, true, true);
            }

            void SpawnFuelBox()
            {
                string prefabfuelbox = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";

                pilotstorage = GameManager.server.CreateEntity(prefabfuelbox, entitypos, entityrot, true);
                pilotstorage.transform.localEulerAngles = new Vector3(0, 0, 0);
                pilotstorage.transform.localPosition = new Vector3(0f, -0.3f, 0f);
                pilotstorage.OwnerID = ownerid;
                pilotstorage?.Spawn();
                pilotstorage.SetParent(entity, 0);
                //var fuelboxcontainer = fuelbox.GetComponent<StorageContainer>();
                //fuelboxcontainer.inventory.capacity = 12;

                if (enablepassstoragebox)
                {
                    passstorage1 = GameManager.server.CreateEntity(prefabfuelbox, passengerchair1.transform.position, passengerchair1.transform.rotation, true);
                    passstorage1.transform.localEulerAngles = new Vector3(0, 0, 0);
                    passstorage1.transform.localPosition = new Vector3(0f, -0.3f, 0f);
                    passstorage1.OwnerID = ownerid;
                    passstorage1?.Spawn();
                    passstorage1.SetParent(passengerchair1, 0);
                    //var fuelboxcontainer = stashbox2.GetComponent<StorageContainer>();
                    //fuelboxcontainer.inventory.capacity = 12;


                    passstorage2 = GameManager.server.CreateEntity(prefabfuelbox, passengerchair2.transform.position, passengerchair2.transform.rotation, true);
                    passstorage2.transform.localEulerAngles = new Vector3(0, 0, 0);
                    passstorage2.transform.localPosition = new Vector3(0f, -0.3f, 0f);
                    passstorage2.OwnerID = ownerid;
                    passstorage2?.Spawn();
                    passstorage2.SetParent(passengerchair2, 0);
                    //var fuelboxcontainer = stashbox3.GetComponent<StorageContainer>();
                    //fuelboxcontainer.inventory.capacity = 12;
                }
            }

            private void OnTriggerEnter(Collider col)
            {
                var target = col.GetComponentInParent<BasePlayer>();
                if (target != null)
                {
                    copterantihack.Add(target);
                }
            }

            private void OnTriggerExit(Collider col)
            {
                var target = col.GetComponentInParent<BasePlayer>();
                if (target != null)
                {
                    copterantihack.Remove(target);
                }
            }

            public void DropNet()
            {
                var hasnet = entity.GetComponent<CopterNet>() ?? null;
                if (hasnet == null) { entity.gameObject.AddComponent<CopterNet>(); return; }
                GameObject.Destroy(hasnet);
            }

            public void LockPaintings()
            {
                floor.SetFlag(BaseEntity.Flags.Busy, true, true);
                deck1.SetFlag(BaseEntity.Flags.Busy, true, true);
                deck2.SetFlag(BaseEntity.Flags.Busy, true, true);
                barrel.SetFlag(BaseEntity.Flags.Busy, true, true);
                panel.SetFlag(BaseEntity.Flags.Busy, true, true);
                RefreshEntities();
                paintingsarelocked = true;
            }

            public void UnLockPaintings()
            {
                floor.SetFlag(BaseEntity.Flags.Busy, false, true);
                deck1.SetFlag(BaseEntity.Flags.Busy, false, true);
                deck2.SetFlag(BaseEntity.Flags.Busy, false, true);
                barrel.SetFlag(BaseEntity.Flags.Busy, false, true);
                panel.SetFlag(BaseEntity.Flags.Busy, false, true);
                RefreshEntities();
                paintingsarelocked = false;
            }

            BasePlayer GetPilot()
            {
                player = entity.GetComponent<BaseMountable>().GetMounted() as BasePlayer;
                return player;
            }

            void FuelCheck()
            {
                player = GetPilot();
                if (player != null && instance.isAllowed(player, "gyrocopter.unlimited")) { if (currentfuel <= 9999) currentfuel = 10000; return; }
                if (currentfuel >= 1 && !throttleup && engineon && !hasbonuscharge) { currentfuel = currentfuel - 1; return; }
                if (currentfuel >= 1 && throttleup && engineon && !hasbonuscharge) { currentfuel = currentfuel - sprintcost; return; }
                if (currentfuel <= 9999 && !hasbonuscharge) currentfuel = currentfuel + baserechargerate;
                if (currentfuel <= 9999 && hasbonuscharge) currentfuel = currentfuel + bonusrechargerate;
            }

            public void CopterInput(InputState input, BasePlayer player)
            {
                if (input == null || player == null) return;
                if (input.WasJustPressed(BUTTON.FORWARD)) moveforward = true;
                if (input.WasJustReleased(BUTTON.FORWARD)) moveforward = false;
                if (input.WasJustPressed(BUTTON.BACKWARD)) movebackward = true;
                if (input.WasJustReleased(BUTTON.BACKWARD)) movebackward = false;
                if (input.WasJustPressed(BUTTON.RIGHT)) rotright = true;
                if (input.WasJustReleased(BUTTON.RIGHT)) rotright = false;
                if (input.WasJustPressed(BUTTON.LEFT)) rotleft = true;
                if (input.WasJustReleased(BUTTON.LEFT)) rotleft = false;
                if (input.IsDown(BUTTON.SPRINT)) throttleup = true;
                if (input.WasJustReleased(BUTTON.SPRINT)) throttleup = false;
                if (input.WasJustPressed(BUTTON.JUMP)) moveup = true;
                if (input.WasJustReleased(BUTTON.JUMP)) moveup = false;
                if (input.WasJustPressed(BUTTON.DUCK)) movedown = true;
                if (input.WasJustReleased(BUTTON.DUCK)) movedown = false;
                if (!engineon) return;
                if (!enablebombs) return;
                if (input.WasJustPressed(BUTTON.RELOAD)) FindMoreDaBombs(player);
                if (input.WasJustPressed(BUTTON.FIRE_THIRD)) UseDaBombs(player);
            }

            public void UseDaBombs(BasePlayer player)
            {
                if (!enablebombs) return;
                if (dabomb == null) { instance.SendReply(player, "You do not have DA BOMB to drop !!!!"); return; }
                if (dabomb != null)
                {
                    dabomb.Invoke("KillMessage", 0.1f);
                    dabomb.transform.hasChanged = true;
                    dabomb.SendNetworkUpdateImmediate();
                    instance.SendReply(player, "You have dropped DA BOMD !!!!");
                    entity.gameObject.AddComponent<DaBomb>();
                }
                return;
            }

            public void ReloadDaBombs(BasePlayer player = null)
            {
                if (!enablebombs) return;
                if (dabomb == null)
                {
                    dabomb = GameManager.server.CreateEntity(prefabdabomb, entity.transform.position, Quaternion.identity, false);
                    dabomb.enableSaving = false;
                    dabomb?.Spawn();
                    dabomb.SetParent(entity);
                    dabomb.transform.localEulerAngles = new Vector3(90, 0, 0);
                    dabomb.transform.localPosition = new Vector3(0f, 0.5f, -2.7f);
                    dabomb.transform.hasChanged = true;
                    dabomb.SendNetworkUpdateImmediate();
                    hasdabomb = true;
                }
                return;
            }

            public void FindMoreDaBombs(BasePlayer player = null)
            {
                if (!enablebombs) return;
                List<BaseEntity> barrellist = new List<BaseEntity>();
                Vis.Entities<BaseEntity>(entity.transform.position, MinAltitude + 5f, barrellist);
                foreach (BaseEntity barrel in barrellist)
                {
                    if (barrel.name.Contains("barrel"))
                    {
                        if (dabomb != null) return;
                        barrel.Invoke("KillMessage", 0.1f);
                        ReloadDaBombs(player);
                        return;
                    }
                }
                instance.SendReply(player, "Unable to locate DA BOMB !!");
            }

            void FixedUpdate()
            {
                FuelCheck();
                if (engineon)
                {
                    if (!GetPilot()) islanding = true;
                    var currentspeed = normalspeed;
                    var throttlespeed = 30;
                    if (throttleup) { throttlespeed = 60; currentspeed = sprintspeed; }
                    barcenter.transform.eulerAngles += new Vector3(0, throttlespeed, 0);
                    count = count + 1;
                    if (count == 3)
                    {
                        Effect.server.Run("assets/bundled/prefabs/fx/player/swing_weapon.prefab", this.transform.position);
                    }
                    if (count == 6 && throttleup) Effect.server.Run("assets/bundled/prefabs/fx/player/swing_weapon.prefab", this.transform.position);
                    throttleup = false;
                    if (count >= 6) count = 0;

                    if (islanding || currentfuel <= 0)
                    {
                        islanding = true;
                        var hasnet = entity.GetComponent<CopterNet>() ?? null;
                        if (hasnet != null) GameObject.Destroy(hasnet);
                        entity.transform.localPosition += (transform.up * -5f) * Time.deltaTime;
                        RaycastHit hit;
                        if (Physics.Raycast(new Ray(entity.transform.position, Vector3.down), out hit, 1f, layerMask))
                        {
                            islanding = false;
                            engineon = false;
                            if (pilotslist.Contains(player.userID))
                            {
                                pilotslist.Remove(player.userID);
                            }
                        }
                        ResetMovement();
                        DoMovementSync(entity);
                        RefreshEntities();
                        return;
                    }

                    if (Physics.Raycast(new Ray(entity.transform.position + Vector3.down, Vector3.down), minaltitude, layerMask))
                    {
                        entity.transform.localPosition += transform.up * minaltitude * Time.deltaTime;
                        DoMovementSync(entity);
                        RefreshEntities();
                        return;
                    }

                    if (rotright) entity.transform.eulerAngles += new Vector3(0, 2, 0);
                    else if (rotleft) entity.transform.eulerAngles += new Vector3(0, -2, 0);

                    if (moveforward) entity.transform.localPosition += ((transform.forward * currentspeed) * Time.deltaTime);
                    else if (movebackward) entity.transform.localPosition = entity.transform.localPosition - ((transform.forward * currentspeed) * Time.deltaTime);

                    if (moveup) entity.transform.localPosition += ((transform.up * currentspeed) * Time.deltaTime);
                    else if (movedown) entity.transform.localPosition += ((transform.up * -currentspeed) * Time.deltaTime);

                    DoMovementSync(entity);
                    RefreshEntities();
                }
            }

            void ResetMovement()
            {
                moveforward = false;
                movebackward = false;
                moveup = false;
                movedown = false;
                rotright = false;
                rotleft = false;
                throttleup = false;
            }

            public void DoMovementSync(BaseEntity entity, bool force = false)
            {
                if (force || (barrel != null && entity.PrefabName == StringPool.Get(this.barrel.prefabID)) || (pilotstorage != null && entity.PrefabName == StringPool.Get(this.pilotstorage.prefabID)) || (skid1 != null && entity.PrefabName == StringPool.Get(this.skid1.prefabID)))
                {
                    if (Net.sv.write.Start())
                    {
                        Net.sv.write.PacketID(Message.Type.EntityDestroy);
                        Net.sv.write.UInt32(entity.net.ID);
                        Net.sv.write.UInt8(0);
                        Net.sv.write.Send(new SendInfo(entity.net.group.subscribers));
                    }
                    entity.SendNetworkUpdateImmediate(true);
                    entity.UpdateNetworkGroup();
                }
                else
                {
                    if (Net.sv.write.Start())
                    {
                        Net.sv.write.PacketID(Message.Type.GroupChange);
                        Net.sv.write.EntityID(entity.net.ID);
                        Net.sv.write.GroupID(entity.net.group.ID);
                        Net.sv.write.Send(new SendInfo(entity.net.group.subscribers));
                    }
                    if (Net.sv.write.Start())
                    {
                        Net.sv.write.PacketID(Message.Type.EntityPosition);
                        Net.sv.write.EntityID(entity.net.ID);
                        Net.sv.write.Vector3(entity.GetNetworkPosition());
                        Net.sv.write.Vector3(entity.GetNetworkRotation().eulerAngles);
                        Net.sv.write.Float(entity.GetNetworkTime());
                        Write write = Net.sv.write;
                        SendInfo info = new SendInfo(entity.net.group.subscribers);
                        info.method = SendMethod.ReliableUnordered;
                        info.priority = Priority.Immediate;
                        write.Send(info);
                    }
                }
                if (entity.children != null)
                    foreach (BaseEntity current in entity.children)
                        DoMovementSync(current, force);
            }

            public void RefreshEntities()
            {
                entity.transform.hasChanged = true;
                entity.SendNetworkUpdateImmediate();
                entity.UpdateNetworkGroup();

                barcenter.transform.hasChanged = true;
                barcenter.SendNetworkUpdateImmediate();
                barcenter.UpdateNetworkGroup();
                rotor1.transform.hasChanged = true;
                rotor1.SendNetworkUpdateImmediate();
                rotor1.UpdateNetworkGroup();
                rotor2.transform.hasChanged = true;
                rotor2.SendNetworkUpdateImmediate();
                rotor2.UpdateNetworkGroup();
                rotor3.transform.hasChanged = true;
                rotor3.SendNetworkUpdateImmediate();
                rotor3.UpdateNetworkGroup();
                rotor4.transform.hasChanged = true;
                rotor4.SendNetworkUpdateImmediate();
                rotor4.UpdateNetworkGroup();

                if (entity.children != null)
                    for (int i = 0; i < entity.children.Count; i++)
                    {
                        entity.children[i].transform.hasChanged = true;
                        entity.children[i].SendNetworkUpdateImmediate();
                        entity.children[i].UpdateNetworkGroup();
                    }
            }

            public void OnDestroy()
            {
                if (loadplayer.ContainsKey(ownerid)) loadplayer[ownerid].coptercount = loadplayer[ownerid].coptercount - 1;
                if (entity != null) { entity.Invoke("KillMessage", 0.1f); }
            }
        }

        #endregion

        #region DaBomb Spawner

        class DaBomb : MonoBehaviour
        {
            BaseEntity entity;
            GyroCopter copter;
            Vector3 entitypos;
            Quaternion entityrot;
            BaseEntity dabomb;
            bool onGround;
            float damageradius;
            float damageamount;

            void Awake()
            {
                entity = GetComponentInParent<BaseEntity>();
                copter = entity.GetComponentInParent<GyroCopter>();
                entitypos = entity.transform.position;
                entityrot = Quaternion.identity;
                onGround = false;
                damageradius = bombdamageradius;
                damageamount = bombdamage;
                var dropfrom = copter.deck2.transform.position + new Vector3(0f, -3f, 0f);
                dabomb = GameManager.server.CreateEntity("assets/bundled/prefabs/radtown/oil_barrel.prefab", dropfrom, Quaternion.identity, true);
                dabomb.enableSaving = false;
                dabomb.Spawn();
                SpawnFireEffects();
            }

            void ImpactDamage(Vector3 hitpos)
            {
                List<BaseCombatEntity> playerlist = new List<BaseCombatEntity>();
                Vis.Entities<BaseCombatEntity>(hitpos, damageradius, playerlist);
                foreach (BaseCombatEntity p in playerlist)
                {
                    if (!(p is BuildingPrivlidge))
                    {
                        p.Hurt(damageamount, Rust.DamageType.Explosion, null, false);
                    }
                }
            }

            public void ImpactFX(Vector3 pos)
            {
                Effect.server.Run("assets/bundled/prefabs/fx/weapons/landmine/landmine_explosion.prefab", pos);
                Effect.server.Run("assets/bundled/prefabs/napalm.prefab", pos);
                BaseEntity firebomb = GameManager.server.CreateEntity("assets/bundled/prefabs/oilfireballsmall.prefab", pos);
                firebomb?.Spawn();
            }

            void SpawnFireEffects()
            {
                Effect.server.Run("assets/bundled/prefabs/fx/survey_explosion.prefab", dabomb.transform.position);
            }

            void FixedUpdate()
            {
                if (onGround) return;
                var currentpos = dabomb.transform.position;
                if (Physics.Raycast(new Ray(currentpos + Vector3.down, Vector3.down), 1f, layerMask))
                {
                    ImpactDamage(currentpos);
                    ImpactFX(currentpos);
                    if (dabomb != null) { dabomb.Invoke("KillMessage", 0.1f); }
                    onGround = true;
                    GameObject.Destroy(this);
                }
                dabomb.transform.rotation = Quaternion.Slerp(dabomb.transform.rotation, dabomb.transform.rotation * Quaternion.Euler(new Vector3(15f, 15f, 15f)), Time.deltaTime * 3.0f);
                dabomb.transform.position = dabomb.transform.position + Vector3.down * (10f * Time.deltaTime);
                dabomb.transform.hasChanged = true;
                dabomb.SendNetworkUpdateImmediate();
            }

            public void DoMovementSync(BaseEntity entity, bool force = false)
            {
                if (force || entity.PrefabName == StringPool.Get(this.dabomb.prefabID))
                {
                    if (Net.sv.write.Start())
                    {
                        Net.sv.write.PacketID(Message.Type.EntityDestroy);
                        Net.sv.write.UInt32(entity.net.ID);
                        Net.sv.write.UInt8(0);
                        Net.sv.write.Send(new SendInfo(entity.net.group.subscribers));
                    }
                    entity.SendNetworkUpdateImmediate(true);
                    entity.UpdateNetworkGroup();
                }
                else
                {
                    if (Net.sv.write.Start())
                    {
                        Net.sv.write.PacketID(Message.Type.GroupChange);
                        Net.sv.write.EntityID(entity.net.ID);
                        Net.sv.write.GroupID(entity.net.group.ID);
                        Net.sv.write.Send(new SendInfo(entity.net.group.subscribers));
                    }
                    if (Net.sv.write.Start())
                    {
                        Net.sv.write.PacketID(Message.Type.EntityPosition);
                        Net.sv.write.EntityID(entity.net.ID);
                        Net.sv.write.Vector3(entity.GetNetworkPosition());
                        Net.sv.write.Vector3(entity.GetNetworkRotation().eulerAngles);
                        Net.sv.write.Float(entity.GetNetworkTime());
                        Write write = Net.sv.write;
                        SendInfo info = new SendInfo(entity.net.group.subscribers);
                        info.method = SendMethod.ReliableUnordered;
                        info.priority = Priority.Immediate;
                        write.Send(info);
                    }
                }
                if (entity.children != null)
                    foreach (BaseEntity current in entity.children)
                        DoMovementSync(current, force);
            }

            void OnDestroy()
            {
                if (dabomb != null) { dabomb.Invoke("KillMessage", 0.1f); }
                GameObject.Destroy(this);
            }
        }

        #endregion

        #region Copter Netting

        class CopterNet : MonoBehaviour
        {
            public BaseEntity netting1;
            public BaseEntity netting2;
            public BaseEntity netting3;
            BaseEntity entity;
            GyroCopter copter;
            Vector3 entitypos;
            Quaternion entityrot;

            void Awake()
            {
                entity = GetComponentInParent<BaseEntity>();
                copter = entity.GetComponentInParent<GyroCopter>();
                entitypos = entity.transform.position;
                entityrot = Quaternion.identity;
                string prefabnetting = "assets/prefabs/building/wall.frame.netting/wall.frame.netting.prefab";

                netting1 = GameManager.server.CreateEntity(prefabnetting, entitypos, entityrot, false);
                netting1?.Spawn();
                netting1.transform.localEulerAngles = new Vector3(0, 0, 0);
                netting1.transform.localPosition = new Vector3(0.9f, -2.9f, -1.4f);
                var netstab1 = netting1.GetComponent<StabilityEntity>();
                netstab1.grounded = true;
                netting1.enableSaving = false;
                netting1.SetParent(entity);

                netting2 = GameManager.server.CreateEntity(prefabnetting, entitypos, entityrot, false);
                netting2?.Spawn();
                netting2.transform.localEulerAngles = new Vector3(0, 0, 0);
                netting2.transform.localPosition = new Vector3(0.9f, -5.9f, -1.4f);
                var netstab2 = netting2.GetComponent<StabilityEntity>();
                netstab2.grounded = true;
                netting2.enableSaving = false;
                netting2.SetParent(entity);

                netting3 = GameManager.server.CreateEntity(prefabnetting, entitypos, entityrot, false);
                netting3?.Spawn();
                netting3.transform.localEulerAngles = new Vector3(0, 0, 0);
                netting3.transform.localPosition = new Vector3(0.9f, -8.9f, -1.4f);
                var netstab3 = netting3.GetComponent<StabilityEntity>();
                netstab3.grounded = true;
                netting3.enableSaving = false;
                netting3.SetParent(entity);
            }

            void RefreshNetting()
            {
                if (netting1 != null) netting1.transform.hasChanged = true;
                if (netting1 != null) netting1.SendNetworkUpdateImmediate();
                if (netting1 != null) netting1.UpdateNetworkGroup();

                if (netting2 != null) netting2.transform.hasChanged = true;
                if (netting2 != null) netting2.SendNetworkUpdateImmediate();
                if (netting2 != null) netting2.UpdateNetworkGroup();

                if (netting3 != null) netting3.transform.hasChanged = true;
                if (netting3 != null) netting3.SendNetworkUpdateImmediate();
                if (netting3 != null) netting3.UpdateNetworkGroup();
            }

            void FixedUpdate()
            {
                RefreshNetting();
            }

            void OnDestroy()
            {
                if (netting3 != null) { netting3.Invoke("KillMessage", 0.1f); }
                if (netting2 != null) { netting2.Invoke("KillMessage", 0.1f); };
                if (netting1 != null) { netting1.Invoke("KillMessage", 0.1f); }
                GameObject.Destroy(this);
            }
        }

        #endregion

        #region FuelControl and Fuel Cui

        class FuelControl : MonoBehaviour
        {
            BasePlayer player;
            GyroCopter copter;
            public string anchormaxstr;
            public string colorstr;
            Vector3 playerpos;
            Gyrocopter instance;
            bool ischarging;
            int count;
            float rechargerange;
            int rechargerate;

            void Awake()
            {
                instance = new Gyrocopter();
                player = GetComponentInParent<BasePlayer>() ?? null;
                copter = player.GetMounted().GetComponentInParent<GyroCopter>() ?? null;
                playerpos = player.transform.position;
                rechargerange = RechargeRange;
                rechargerate = BonusRechargeRate;

                ischarging = false;
                count = 0;
            }

            void Recharge()
            {
                var hits = Physics.OverlapSphere(copter.transform.position, rechargerange);
                foreach (var hit in hits)
                {
                    if (hit.name.Contains("substation"))
                    {
                        ischarging = true;
                        ChargingFX();
                        RechargeIndicator(player);
                        copter.hasbonuscharge = true;
                        return;
                    }
                }
                DestroyChargeCui(player);
                ischarging = false;
                copter.hasbonuscharge = false;
            }

            void ChargingFX()
            {
                if (count == 15)
                {
                    Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.shock.prefab", playerpos + Vector3.down);
                    count = 0;
                    return;
                }
                count = count + 1;
            }

            void FixedUpdate()
            {
                var copterfuel = copter.currentfuel;
                playerpos = player.transform.position;
                if (copterfuel >= 10000) copterfuel = 10000;
                if (copterfuel <= 0) copterfuel = 0;
                fuelIndicator(player, copterfuel);
                Recharge();
            }

            public void RechargeIndicator(BasePlayer player)
            {
                DestroyChargeCui(player);
                if (ischarging == false) return;
                var chargeindicator = new CuiElementContainer();
                chargeindicator.Add(new CuiButton
                {
                    Button = { Command = $"", Color = "1.0 1.0 0.0 0.8" },
                    RectTransform = { AnchorMin = "0.47 0.155", AnchorMax = "0.53 0.175" },
                    Text = { Text = ("CHARGING"), FontSize = 14, Color = "0.0 0.0 0.0 1.0", Align = TextAnchor.MiddleCenter }
                }, "Overall", "recharge");
                CuiHelper.AddUi(player, chargeindicator);
            }

            public void fuelIndicator(BasePlayer player, int fuel)
            {
                DestroyCui(player);
                var displayfuel = fuel;
                var fuelstr = displayfuel.ToString();
                var colorstrred = "0.6 0.1 0.1 0.8";
                var colorstryellow = "0.8 0.8 0.0 0.8";
                var colorstrgreen = "0.0 0.6 0.1 0.8";
                colorstr = colorstrgreen;
                if (fuel >= 9001) anchormaxstr = "0.60 0.145";
                if (fuel >= 8001 && fuel <= 9000) anchormaxstr = "0.58 0.145";
                if (fuel >= 7001 && fuel <= 8000) anchormaxstr = "0.56 0.145";
                if (fuel >= 6001 && fuel <= 7000) anchormaxstr = "0.54 0.145";
                if (fuel >= 5001 && fuel <= 6000) anchormaxstr = "0.52 0.145";
                if (fuel >= 4001 && fuel <= 5000) anchormaxstr = "0.50 0.145";
                if (fuel >= 3001 && fuel <= 4000) { anchormaxstr = "0.48 0.145"; colorstr = colorstryellow; }
                if (fuel >= 2001 && fuel <= 3000) { anchormaxstr = "0.46 0.145"; colorstr = colorstryellow; }
                if (fuel >= 1001 && fuel <= 2000) { anchormaxstr = "0.44 0.145"; colorstr = colorstrred; }
                if (fuel <= 1000) { anchormaxstr = "0.42 0.145"; colorstr = colorstrred; }
                var fuelindicator = new CuiElementContainer();
                fuelindicator.Add(new CuiButton
                {
                    Button = { Command = $"", Color = "0.0 0.0 0.0 0.3" },
                    RectTransform = { AnchorMin = "0.40 0.12", AnchorMax = "0.60 0.15" },
                    Text = { Text = (""), FontSize = 18, Color = "1.0 1.0 1.0 1.0", Align = TextAnchor.MiddleLeft }
                }, "Overall", "fuelGuia");

                fuelindicator.Add(new CuiButton
                {
                    Button = { Command = $"", Color = colorstr },
                    RectTransform = { AnchorMin = "0.40 0.125", AnchorMax = anchormaxstr },
                    Text = { Text = (fuelstr), FontSize = 14, Color = "1.0 1.0 1.0 0.6", Align = TextAnchor.MiddleRight }
                }, "Overall", "fuelGui");

                CuiHelper.AddUi(player, fuelindicator);
            }

            void DestroyChargeCui(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "recharge");
            }

            void DestroyCui(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "fuelGui");
                CuiHelper.DestroyUi(player, "fuelGuia");
            }

            public void OnDestroy()
            {
                DestroyChargeCui(player);
                DestroyCui(player);
                Destroy(this);
            }
        }

        #endregion

    }
}