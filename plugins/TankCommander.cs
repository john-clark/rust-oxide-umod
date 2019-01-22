using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Globalization;
using Rust;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("Tank Commander", "k1lly0u", "0.1.8")]
    [Description("Drive tanks, shoot stuff")]
    class TankCommander : RustPlugin
    {
        #region Fields
        [PluginReference]
        Plugin Friends, Clans, Godmode;

        RestoreData restoreData;
        private DynamicConfigFile restorationdata;

        static TankCommander ins;

        private List<Controller> controllers = new List<Controller>();
        private List<ulong> passengers = new List<ulong>();

        private Dictionary<ulong, Controller> commanders = new Dictionary<ulong, Controller>();
        private Dictionary<string, string> itemNames = new Dictionary<string, string>();

        private Dictionary<CommandType, BUTTON> controlButtons;

        private bool initialized;
        private int rocketId;
        private int mgId;

        const string tankPrefab = "assets/prefabs/npc/m2bradley/bradleyapc.prefab";
        const string uiHealth = "TCUI_Health";
        const string uiAmmoMg = "TCUI_Ammo_MG";
        const string uiAmmoRocket = "TCUI_Ammo_Rocket";
        #endregion

        #region Oxide Hooks
        void Loaded()
        {
            lang.RegisterMessages(Messages, this);
            permission.RegisterPermission("tankcommander.admin", this);
            permission.RegisterPermission("tankcommander.use", this);
            restorationdata = Interface.Oxide.DataFileSystem.GetFile("tankcommander_data");
        }
        void OnServerInitialized()
        {
            ins = this;
            LoadVariables();
            LoadData();

            ConvertControlButtons();
            itemNames = ItemManager.itemList.ToDictionary(x => x.shortname, y => y.displayName.english);
            rocketId = ItemManager.itemList.Find(x => x.shortname == configData.Weapons.Cannon.Type)?.itemid ?? 0;
            mgId = ItemManager.itemList.Find(x => x.shortname == configData.Weapons.MG.Type)?.itemid ?? 0;

            initialized = true;
        }
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BradleyAPC)
            {
                Controller controller = entity.GetComponent<Controller>();
                if (controller != null)
                    controller.ManageDamage(info);
            }
            if (entity is BasePlayer)
            {
                if (commanders.ContainsKey((entity as BasePlayer).userID) || passengers.Contains((entity as BasePlayer).userID))
                {
                    info.damageTypes = new DamageTypeList();
                    info.HitEntity = null;
                    info.HitMaterial = 0;
                    info.PointStart = Vector3.zero;
                    return;
                }

                if (info.Initiator is BradleyAPC)
                {
                    Controller controller = (info.Initiator as BradleyAPC).GetComponent<Controller>();
                    if (controller != null)
                    {
                        if (!controller.HasCommander())
                        {
                            info.damageTypes = new DamageTypeList();
                            info.HitEntity = null;
                            info.HitMaterial = 0;
                            info.PointStart = Vector3.zero;
                        }
                    }
                }
            }
        }
        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!initialized || player == null || commanders.ContainsKey(player.userID) || passengers.Contains(player.userID) || !HasPermission(player, "tankcommander.use")) return;

            if (configData.Inventory.Enabled && input.WasJustPressed(controlButtons[CommandType.Inventory]))
            {
                RaycastHit hit;
                if (Physics.SphereCast(player.eyes.position, 0.5f, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward, out hit, 3f))
                {
                    Controller controller = hit.GetEntity()?.GetComponent<Controller>();
                    if (controller != null && !controller.HasCommander())
                        OpenTankInventory(player, controller);
                }
                return;
            }

            if (input.WasJustPressed(controlButtons[CommandType.EnterExit]))
            {
                RaycastHit hit;
                if (Physics.SphereCast(player.eyes.position, 0.5f, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward, out hit, 3f))
                {
                    Controller controller = hit.GetEntity()?.GetComponent<Controller>();
                    if (controller != null)
                    {
                        if (player.IsFlying)
                        {
                            SendReply(player, msg("is_flying", player.UserIDString));
                            return;
                        }

                        if (!controller.HasCommander())
                        {
                            commanders.Add(player.userID, controller);
                            controller.EnterTank(player);
                        }
                        else
                        {
                            if (configData.Passengers.Enabled)
                            {
                                BasePlayer commander = controller.GetCommander();

                                if (!configData.Passengers.UseFriends && !configData.Passengers.UseClans)
                                {
                                    controller.PassengerEnter(player);
                                    return;
                                }

                                if (configData.Passengers.UseFriends && AreFriends(commander.userID, player.userID))
                                {
                                    controller.PassengerEnter(player);
                                    return;
                                }

                                if (configData.Passengers.UseClans && IsClanmate(commander.userID, player.userID))
                                {
                                    controller.PassengerEnter(player);
                                    return;
                                }

                                SendReply(player, msg("not_friend", player.UserIDString));
                            }
                            else SendReply(player, msg("in_use", player.UserIDString));
                        }
                    }
                }
            }
        }
        void OnPlayerDisconnected(BasePlayer player)
        {
            if (commanders.ContainsKey(player.userID))
                commanders[player.userID].ExitTank();
        }
        object OnRunPlayerMetabolism(PlayerMetabolism metabolism, BaseCombatEntity entity)
        {
            var player = entity.ToPlayer();
            if (player == null || !commanders.ContainsKey(player.userID)) return null;
            if (Godmode && (bool)Godmode.Call("IsGod", player.UserIDString)) return null;
            return true;
        }

        void Unload()
        {
            foreach (var controller in controllers)
                UnityEngine.Object.Destroy(controller);

            var objects = UnityEngine.Object.FindObjectsOfType<Controller>();
            if (objects != null)
            {
                foreach(var obj in objects)
                    UnityEngine.Object.Destroy(obj);
            }
            controllers.Clear();
        }
        #endregion

        #region Functions
        private T ParseType<T>(string type) => (T)Enum.Parse(typeof(T), type, true);
        private bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm) || permission.UserHasPermission(player.UserIDString, "tankcommander.admin");

        private void ConvertControlButtons()
        {
            controlButtons = new Dictionary<CommandType, BUTTON>
            {
                [CommandType.EnterExit] = ParseType<BUTTON>(configData.Buttons.Enter),
                [CommandType.Lights] = ParseType<BUTTON>(configData.Buttons.Lights),
                [CommandType.Inventory] = ParseType<BUTTON>(configData.Buttons.Inventory),
                [CommandType.Boost] = ParseType<BUTTON>(configData.Buttons.Boost),
                [CommandType.Cannon] = ParseType<BUTTON>(configData.Buttons.Cannon),
                [CommandType.Coax] = ParseType<BUTTON>(configData.Buttons.Coax),
                [CommandType.MG] = ParseType<BUTTON>(configData.Buttons.MG)
            };
        }
        void OpenTankInventory(BasePlayer player, Controller controller)
        {
            player.inventory.loot.Clear();
            player.inventory.loot.entitySource = controller.entity;
            player.inventory.loot.itemSource = null;
            player.inventory.loot.AddContainer(controller.inventory);
            player.inventory.loot.SendImmediate();
            player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", "generic");
            player.SendNetworkUpdate();
        }
        private void StartSpectating(BasePlayer player, Controller controller, bool isOperator)
        {
            player.spectateFilter = "@123nofilter123";
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, true);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, true);
            player.gameObject.SetLayerRecursive(10);
            player.CancelInvoke("InventoryUpdate");
            player.SendNetworkUpdateImmediate();

            timer.In(0.5f, () =>
            {
                player.transform.position = controller.transform.position;
                player.SetParent(controller.entity, 0);
                player.Command("client.camoffset", new object[] { new Vector3(0, 3.5f, 0) });

                if (isOperator)
                {
                    controller.enabled = true;

                    string ctrlStr = msg("controls", player.UserIDString);
                    if (configData.Weapons.Cannon.Enabled)
                        ctrlStr += $"\n{string.Format(msg("fire_cannon", player.UserIDString), configData.Buttons.Cannon)}";
                    if (configData.Weapons.Coax.Enabled)
                        ctrlStr += $"\n{string.Format(msg("fire_coax", player.UserIDString), configData.Buttons.Coax)}";
                    if (configData.Weapons.MG.Enabled)
                        ctrlStr += $"\n{string.Format(msg("fire_mg", player.UserIDString), configData.Buttons.MG)}";
                    ctrlStr += $"\n{string.Format(msg("speed_boost", player.UserIDString), configData.Buttons.Boost)}";
                    ctrlStr += $"\n{string.Format(msg("enter_exit", player.UserIDString), configData.Buttons.Enter)}";
                    ctrlStr += $"\n{string.Format(msg("toggle_lights", player.UserIDString), configData.Buttons.Lights)}";
                    if (configData.Inventory.Enabled)
                        ctrlStr += $"\n{string.Format(msg("access_inventory", player.UserIDString), configData.Buttons.Inventory)}";
                    SendReply(player, ctrlStr);

                    CreateHealthUI(player, controller);
                    CreateMGAmmoUI(player, controller);
                    CreateRocketAmmoUI(player, controller);
                }
                else
                {
                    controller.SetPassengerActive(player);

                    SendReply(player, string.Format(msg("enter_exit", player.UserIDString), configData.Buttons.Enter));
                    if (configData.Inventory.Enabled)
                        SendReply(player, string.Format(msg("access_inventory", player.UserIDString), configData.Buttons.Inventory));
                    CreateHealthUI(player, controller);
                }

            });
        }
        private void EndSpectating(BasePlayer player, Controller commander, bool isOperator)
        {
            player.spectateFilter = string.Empty;
            player.SetParent(null);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, false);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, false);
            player.gameObject.SetLayerRecursive(17);
            player.InvokeRepeating("InventoryUpdate", 1f, 0.1f * UnityEngine.Random.Range(0.99f, 1.01f));
            player.Command("client.camoffset", new object[] { new Vector3(0, 1.2f, 0) });
            player.transform.position = commander.transform.position + Vector3.up + (commander.transform.right * 3);

            if (isOperator)
                commanders.Remove(player.userID);
        }
        #endregion

        #region Component
        enum CommandType { EnterExit, Lights, Inventory, Boost, Cannon, Coax, MG }

        class Controller : MonoBehaviour
        {
            private BasePlayer player;

            public BradleyAPC entity;
            private Rigidbody rigidBody;

            private WheelCollider[] leftWheels;
            private WheelCollider[] rightWheels;

            public ItemContainer inventory;

            private float accelTimeTaken;
            private float accelTimeToTake = 3f;

            private float forwardTorque = 2000f;
            private float maxBrakeTorque = 50f;
            private float turnTorque = 2000f;

            private float lastFireCannon;
            private float lastFireMG;
            private float lastFireCoax;

            private Vector3 aimVector = Vector3.forward;
            private Vector3 aimVectorTop = Vector3.forward;

            private Dictionary<CommandType, BUTTON> controlButtons;
            private ConfigData.WeaponOptions.WeaponSystem cannon;
            private ConfigData.WeaponOptions.WeaponSystem mg;
            private ConfigData.WeaponOptions.WeaponSystem coax;
            private ConfigData.CrushableTypes crushables;

            private List<ulong> enteringPassengers = new List<ulong>();
            private List<BasePlayer> passengers = new List<BasePlayer>();

            private void Awake()
            {
                entity = GetComponent<BradleyAPC>();

                entity.enabled = false;
                enabled = false;

                entity.CancelInvoke(entity.UpdateTargetList);
                entity.CancelInvoke(entity.UpdateTargetVisibilities);

                rigidBody = entity.myRigidBody;
                leftWheels = entity.leftWheels;
                rightWheels = entity.rightWheels;

                forwardTorque = ins.configData.Movement.ForwardTorque;
                turnTorque = ins.configData.Movement.TurnTorque;
                maxBrakeTorque = ins.configData.Movement.BrakeTorque;
                accelTimeToTake = ins.configData.Movement.Acceleration;

                controlButtons = ins.controlButtons;
                cannon = ins.configData.Weapons.Cannon;
                mg = ins.configData.Weapons.MG;
                coax = ins.configData.Weapons.Coax;
                crushables = ins.configData.Crushables;

                if (ins.configData.Inventory.Enabled)
                {
                    inventory = new ItemContainer();
                    inventory.ServerInitialize(null, ins.configData.Inventory.Size);
                    if ((int)inventory.uid == 0)
                        inventory.GiveUID();
                }
            }

            private void OnDestroy()
            {
                if (player != null)
                    ExitTank();
                if (entity != null && !entity.IsDestroyed)
                    entity.Kill();
            }

            private void FixedUpdate()
            {
                CheckOtherInput();
            }

            private void LateUpdate()
            {
                CheckForMovement();
                AdjustAiming();
                entity.SendNetworkUpdate();
            }

            private void OnCollisionEnter(Collision collision)
            {
                if (!HasCommander()) return;

                GameObject gObject = collision.gameObject;
                if (gObject == null)
                    return;

                if (crushables.Players)
                {
                    var triggerPlayer = gObject.GetComponentInParent<BasePlayer>();
                    if (triggerPlayer != null && triggerPlayer != player && !passengers.Contains(triggerPlayer) && !enteringPassengers.Contains(triggerPlayer.userID))
                    {
                        triggerPlayer.Die(new HitInfo(player, triggerPlayer, DamageType.Blunt, 200f));
                        return;
                    }
                }

                if (crushables.Animals)
                {
                    var npc = gObject.GetComponentInParent<BaseNpc>();
                    if (npc != null)
                    {
                        npc.Die(new HitInfo(player, npc, DamageType.Blunt, 200f));
                        return;
                    }
                }

                float impactForce = CalculateImpactForce(collision);

                if (crushables.Buildings)
                {
                    var buildingBlock = gObject.GetComponentInParent<BuildingBlock>();
                    if (buildingBlock != null && impactForce >= crushables.GradeForce[buildingBlock.grade.ToString()])
                    {
                        buildingBlock.Die(new HitInfo(player, buildingBlock, DamageType.Blunt, 1000f));
                        return;
                    }

                    var colliderBatch = gObject.GetComponent<MeshColliderBatch>();
                    if (colliderBatch != null)
                    {
                        var colliders = colliderBatch.meshLookup.src.data;
                        if (colliders == null) return;
                        foreach (var instance in colliders)
                        {
                            var block = instance.collider?.GetComponentInParent<BuildingBlock>();
                            if (block != null && Vector3.Distance(block.transform.position, entity.transform.position) < 4 && impactForce >= crushables.GradeForce[block.grade.ToString()])
                            {
                                block.Die(new HitInfo(player, block, DamageType.Blunt, 1000f));
                                return;
                            }
                        }
                        return;
                    }

                    var simpleBlock = gObject.GetComponentInParent<SimpleBuildingBlock>();
                    if (simpleBlock != null && impactForce >= crushables.WallForce)
                    {
                        simpleBlock.Die(new HitInfo(player, simpleBlock, DamageType.Blunt, 1500));
                        return;
                    }
                }

                if (crushables.Loot)
                {
                    var loot = gObject.GetComponentInParent<LootContainer>();
                    if (loot != null)
                    {
                        loot.Die(new HitInfo(player, loot, DamageType.Blunt, 200f));
                        return;
                    }
                }

                if (crushables.Resources)
                {
                    var resource = gObject.GetComponentInParent<ResourceEntity>();
                    if (resource != null && impactForce >= crushables.ResourceForce)
                    {
                        resource.Kill(BaseNetworkable.DestroyMode.None);
                        return;
                    }
                }
            }

            private float CalculateImpactForce(Collision col)
            {
                var impactVelocityX = rigidBody.velocity.x;
                impactVelocityX *= Mathf.Sign(impactVelocityX);
                var impactVelocityY = rigidBody.velocity.y;
                impactVelocityY *= Mathf.Sign(impactVelocityY);
                var impactVelocity = impactVelocityX + impactVelocityY;
                var impactForce = impactVelocity * rigidBody.mass;
                impactForce *= Mathf.Sign(impactForce);
                return impactForce;
            }

            private void CheckOtherInput()
            {
                for (int i = 0; i < passengers.Count; i++)
                {
                    var passenger = passengers[i];
                    if (passenger.serverInput.WasJustPressed(controlButtons[CommandType.EnterExit]))
                        PassengerExit(passenger);
                }

                if (player.serverInput.WasJustPressed(controlButtons[CommandType.EnterExit]))
                {
                    ExitTank();
                    return;
                }

                if (player.serverInput.WasJustPressed(controlButtons[CommandType.Lights]))
                    ToggleLights();

                if (player.serverInput.WasJustPressed(controlButtons[CommandType.Cannon]))
                    FireCannon();

                if (player.serverInput.IsDown(controlButtons[CommandType.MG]) || player.serverInput.WasJustPressed(controlButtons[CommandType.MG]))
                    FireMG();

                if (player.serverInput.IsDown(controlButtons[CommandType.Coax]) || player.serverInput.WasJustPressed(controlButtons[CommandType.Coax]))
                    FireCoax();
            }

            private void CheckForMovement()
            {
                float accelerate = 0f;
                float steer = 0f;

                if (player.serverInput.IsDown(BUTTON.FORWARD))
                    accelerate += 1f;

                if (player.serverInput.IsDown(BUTTON.BACKWARD))
                    accelerate -= 1f;

                if (player.serverInput.IsDown(BUTTON.RIGHT))
                    steer += 1f;

                if (player.serverInput.IsDown(BUTTON.LEFT))
                    steer -= 1f;

                bool boost = player.serverInput.IsDown(controlButtons[CommandType.Boost]);

                SetThrottleSpeed(accelerate, steer, boost);
            }

            private void AdjustAiming()
            {
                Vector3 aimAngle = player.serverInput.current.aimAngles;
                float adjustedY = aimAngle.y + entity.transform.rotation.eulerAngles.y;

                Vector3 targetPos = new Ray(entity.CannonMuzzle.transform.position, Quaternion.Euler(new Vector3(aimAngle.x, adjustedY, 0)) * Vector3.forward).GetPoint(10);
                Vector3 desiredAim = targetPos - (entity.transform.position + (Vector3.up * 2));

                aimVector = Vector3.Lerp(aimVector, desiredAim, Time.deltaTime * 5f);
                entity.turretAimVector = aimVector;
                AimCannon();
                AimMG();
            }

            private void AimCannon()
            {
                entity.AimWeaponAt(entity.mainTurret, entity.coaxPitch, aimVector, -90f, 90f, 360f, null);
                entity.AimWeaponAt(entity.mainTurret, entity.CannonPitch, aimVector, -90f, 7f, 360f, null);
            }

            private void AimMG()
            {
                Vector3 aimAngle = player.serverInput.current.aimAngles;
                float adjustedY = aimAngle.y + entity.transform.rotation.eulerAngles.y;

                RaycastHit hit;
                if (Physics.Raycast(new Ray(entity.topTurretMuzzle.transform.position, Quaternion.Euler(new Vector3(aimAngle.x, adjustedY, 0)) * Vector3.forward), out hit, 500f))
                {
                    Vector3 desiredAim = hit.point - (entity.topTurretMuzzle.transform.position + (Vector3.up * 2));
                    aimVectorTop = Vector3.Lerp(aimVectorTop, desiredAim, Time.deltaTime * 5f);
                    entity.topTurretAimVector = aimVector;
                    entity.AimWeaponAt(entity.topTurretYaw, entity.topTurretPitch, aimVectorTop, -360f, 360f, 360f, entity.mainTurret);
                }
            }

            private void FireCannon()
            {
                if (cannon.RequireAmmo)
                {
                    if (inventory.itemList.Find(x => x.info.shortname == cannon.Type) == null)
                    {
                        if (ins.itemNames.ContainsKey(cannon.Type))
                            ins.SendReply(player, string.Format(ins.msg("no_ammo_cannon", player.UserIDString), ins.itemNames[cannon.Type]));
                        else print($"Invalid ammo type for the cannon set in config: {cannon.Type}");
                        return;
                    }
                }
                if (Time.realtimeSinceStartup >= lastFireCannon)
                {
                    Vector3 modifiedAimConeDirection = AimConeUtil.GetModifiedAimConeDirection(cannon.Accuracy, entity.CannonMuzzle.rotation * Vector3.forward, true);
                    Vector3 cannonPitch = (entity.CannonPitch.transform.rotation * Vector3.back) + (base.transform.up * -1f);
                    Vector3 vector3 = cannonPitch.normalized;
                    entity.myRigidBody.AddForceAtPosition(vector3 * entity.recoilScale, entity.CannonPitch.transform.position, ForceMode.Impulse);
                    Effect.server.Run(entity.mainCannonMuzzleFlash.resourcePath, entity, StringPool.Get(entity.CannonMuzzle.gameObject.name), Vector3.zero, Vector3.zero, null, false);
                    BaseEntity rocket = GameManager.server.CreateEntity(entity.mainCannonProjectile.resourcePath, entity.CannonMuzzle.transform.position, Quaternion.LookRotation(modifiedAimConeDirection), true);
                    ServerProjectile projectile = rocket.GetComponent<ServerProjectile>();
                    projectile.InitializeVelocity(modifiedAimConeDirection.normalized * projectile.speed);
                    rocket.Spawn();

                    TimedExplosive explosive = rocket.GetComponent<TimedExplosive>();
                    if (explosive != null)
                        explosive.damageTypes.Add(new DamageTypeEntry { amount = cannon.Damage, type = DamageType.Explosion });

                    lastFireCannon = Time.realtimeSinceStartup + cannon.Interval;

                    if (cannon.RequireAmmo)
                        inventory.itemList.Find(x => x.info.shortname == cannon.Type)?.UseItem(1);
                    ins.CreateRocketAmmoUI(player, this);
                }
            }

            private void FireCoax()
            {
                if (coax.RequireAmmo)
                {
                    if (inventory.itemList.Find(x => x.info.shortname == coax.Type) == null)
                    {
                        if (ins.itemNames.ContainsKey(coax.Type))
                            ins.SendReply(player, string.Format(ins.msg("no_ammo_coax", player.UserIDString), ins.itemNames[coax.Type]));
                        else print($"Invalid ammo type for the coaxial gun set in config: {coax.Type}");
                        return;
                    }
                }
                if (Time.realtimeSinceStartup >= lastFireCoax)
                {
                    Transform transforms = entity.coaxMuzzle;
                    Vector3 vector3 = transforms.transform.position - (transforms.forward * 0.25f);
                    Vector3 vector31 = transforms.transform.forward;
                    Vector3 modifiedAimConeDirection = AimConeUtil.GetModifiedAimConeDirection(coax.Accuracy, vector31, true);
                    Vector3 targetPos = vector3 + (modifiedAimConeDirection * 300f);
                    List<RaycastHit> list = Pool.GetList<RaycastHit>();
                    GamePhysics.TraceAll(new Ray(vector3, modifiedAimConeDirection), 0f, list, 300f, 1084435201, QueryTriggerInteraction.UseGlobal);
                    for (int i = 0; i < list.Count; i++)
                    {
                        RaycastHit item = list[i];
                        BaseEntity entity = item.GetEntity();
                        if (!(entity != null) || !(entity == this) && !entity.EqualNetID(entity))
                        {
                            BaseCombatEntity baseCombatEntity = entity as BaseCombatEntity;
                            if (baseCombatEntity != null)
                                ApplyDamage(baseCombatEntity, coax.Damage, item.point, modifiedAimConeDirection);

                            if (!(entity != null) || entity.ShouldBlockProjectiles())
                            {
                                targetPos = item.point;
                                break;
                            }
                        }
                    }
                    entity.ClientRPC(null, "CLIENT_FireGun", true, targetPos);
                    Pool.FreeList<RaycastHit>(ref list);

                    lastFireCoax = Time.realtimeSinceStartup + coax.Interval;

                    if (coax.RequireAmmo)
                        inventory.itemList.Find(x => x.info.shortname == coax.Type)?.UseItem(1);
                    ins.CreateMGAmmoUI(player, this);
                }
            }

            private void FireMG()
            {
                if (mg.RequireAmmo)
                {
                    if (inventory.itemList.Find(x => x.info.shortname == mg.Type) == null)
                    {
                        if (ins.itemNames.ContainsKey(mg.Type))
                            ins.SendReply(player, string.Format(ins.msg("no_ammo_mg", player.UserIDString), ins.itemNames[mg.Type]));
                        else print($"Invalid ammo type for the machine gun set in config: {mg.Type}");
                        return;
                    }
                }
                if (Time.realtimeSinceStartup >= lastFireMG)
                {
                    Transform transforms = (entity.topTurretMuzzle);
                    Vector3 vector3 = transforms.transform.position - (transforms.forward * 0.25f);
                    Vector3 vector31 = transforms.transform.forward;
                    Vector3 modifiedAimConeDirection = AimConeUtil.GetModifiedAimConeDirection(mg.Accuracy, vector31, true);
                    Vector3 targetPos = vector3 + (modifiedAimConeDirection * 300f);
                    List<RaycastHit> list = Pool.GetList<RaycastHit>();
                    GamePhysics.TraceAll(new Ray(vector3, modifiedAimConeDirection), 0f, list, 300f, 1084435201, QueryTriggerInteraction.UseGlobal);
                    for (int i = 0; i < list.Count; i++)
                    {
                        RaycastHit item = list[i];
                        BaseEntity entity = item.GetEntity();
                        if (!(entity != null) || !(entity == this) && !entity.EqualNetID(entity))
                        {
                            BaseCombatEntity baseCombatEntity = entity as BaseCombatEntity;
                            if (baseCombatEntity != null)
                                ApplyDamage(baseCombatEntity, mg.Damage, item.point, modifiedAimConeDirection);

                            if (!(entity != null) || entity.ShouldBlockProjectiles())
                            {
                                targetPos = item.point;
                                break;
                            }
                        }
                    }
                    entity.ClientRPC(null, "CLIENT_FireGun", false, targetPos);
                    Pool.FreeList<RaycastHit>(ref list);

                    lastFireMG = Time.realtimeSinceStartup + mg.Interval;

                    if (mg.RequireAmmo)
                        inventory.itemList.Find(x => x.info.shortname == mg.Type)?.UseItem(1);
                    ins.CreateMGAmmoUI(player, this);
                }
            }

            private void FireSideGuns()
            {

            }

            private void ApplyDamage(BaseCombatEntity entity, float damage, Vector3 point, Vector3 normal)
            {
                float single = damage * UnityEngine.Random.Range(0.9f, 1.1f);
                entity.OnAttacked(new HitInfo(this.entity, entity, DamageType.Bullet, single, point));
                if (entity is BasePlayer || entity is BaseNpc)
                {
                    HitInfo hitInfo = new HitInfo()
                    {
                        HitPositionWorld = point,
                        HitNormalWorld = -normal,
                        HitMaterial = StringPool.Get("Flesh")
                    };
                    Effect.server.ImpactEffect(hitInfo);
                }
            }

            private void SetThrottleSpeed(float acceleration, float steering, bool boost)
            {
                if (acceleration == 0 && steering == 0)
                {
                    ApplyBrakes(0.5f);

                    if (accelTimeTaken > 0)
                        accelTimeTaken = Mathf.Clamp(accelTimeTaken -= (Time.deltaTime * 2), 0, accelTimeToTake);
                }
                else
                {
                    ApplyBrakes(0f);

                    accelTimeTaken += Time.deltaTime;
                    float engineRpm = Mathf.InverseLerp(0f, accelTimeToTake, accelTimeTaken);

                    float throttle = Mathf.InverseLerp(0f, 1f, engineRpm);

                    float leftTrack = 0;
                    float rightTrack = 0;
                    float torque = 0;

                    if (acceleration > 0)
                    {
                        torque = forwardTorque;
                        leftTrack = 1f;
                        rightTrack = 1f;
                    }
                    else if (acceleration < 0)
                    {
                        torque = forwardTorque;
                        leftTrack = -1f;
                        rightTrack = -1f;
                    }
                    if (steering > 0)
                    {
                        if (acceleration == 0)
                        {
                            torque = turnTorque;
                            leftTrack = 1f;
                            rightTrack = -1f;
                        }
                        else
                        {
                            torque = (forwardTorque + turnTorque) * 0.75f;
                            rightTrack *= 0.5f;
                        }
                    }
                    else if (steering < 0)
                    {
                        if (acceleration == 0)
                        {
                            torque = turnTorque;
                            leftTrack = -1f;
                            rightTrack = 1f;
                        }
                        else
                        {
                            torque = (forwardTorque + turnTorque) * 0.75f;
                            leftTrack *= 0.5f;
                        }
                    }

                    if (boost)
                    {
                        if (torque > 0)
                            torque += ins.configData.Movement.BoostTorque;
                        if (torque < 0)
                            torque -= ins.configData.Movement.BoostTorque;
                    }

                    ApplyMotorTorque(Mathf.Clamp(leftTrack * throttle, -1f, 1f) * torque, false);
                    ApplyMotorTorque(Mathf.Clamp(rightTrack * throttle, -1f, 1f) * torque, true);
                }
            }

            private void ApplyBrakes(float amount)
            {
                amount = Mathf.Clamp(maxBrakeTorque * amount, 0, maxBrakeTorque);
                ApplyBrakeTorque(amount, true);
                ApplyBrakeTorque(amount, false);
            }

            private void ApplyBrakeTorque(float amount, bool rightSide)
            {
                WheelCollider[] wheelColliderArray = (!rightSide ? leftWheels : rightWheels);

                for (int i = 0; i < wheelColliderArray.Length; i++)
                    wheelColliderArray[i].brakeTorque = maxBrakeTorque * amount;
            }

            private void ApplyMotorTorque(float torque, bool rightSide)
            {
                WheelCollider[] wheelColliderArray = (!rightSide ? leftWheels : rightWheels);

                for (int i = 0; i < wheelColliderArray.Length; i++)
                    wheelColliderArray[i].motorTorque = torque;
            }

            public void EnterTank(BasePlayer player)
            {
                this.player = player;
                ins.restoreData.AddData(player);
                player.inventory.Strip();
                ins.StartSpectating(player, this, true);
            }
            public void ExitTank()
            {
                this.enabled = false;

                ApplyBrakes(1f);

                if (passengers.Count > 0)
                {
                    for (int i = 0; i < passengers.Count; i++)
                        PassengerExit(passengers[i]);
                }

                ins.EndSpectating(player, this, true);

                ins.DestroyAllUI(player); print("");
                ins.restoreData.RestorePlayer(player);
                player = null;
            }
            public void PassengerEnter(BasePlayer passenger)
            {
                ins.restoreData.AddData(passenger);
                passenger.inventory.Strip();
                ins.passengers.Add(passenger.userID);
                enteringPassengers.Add(passenger.userID);
                ins.StartSpectating(passenger, this, false);
                ins.SendReply(player, ins.msg("passenger_enter", player.UserIDString));
            }
            private void PassengerExit(BasePlayer passenger)
            {
                ins.DestroyAllUI(passenger);
                ins.passengers.Remove(passenger.userID);
                passengers.Remove(passenger);
                ins.EndSpectating(passenger, this, false);
                ins.restoreData.RestorePlayer(player);
            }
            public void SetPassengerActive(BasePlayer passenger)
            {
                passengers.Add(passenger);
                if (enteringPassengers.Contains(passenger.userID))
                    enteringPassengers.Remove(passenger.userID);
            }

            public bool HasCommander() => player != null;

            public BasePlayer GetCommander() => player;

            public bool IsAtMaxCapacity() => passengers.Count >= ins.configData.Passengers.Max;

            private void ToggleLights() => entity.SetFlag(BaseEntity.Flags.Reserved5, !entity.HasFlag(BaseEntity.Flags.Reserved5), false);

            public void ManageDamage(HitInfo info)
            {
                if (info.damageTypes.Total() >= entity.health)
                {
                    info.damageTypes = new DamageTypeList();
                    info.HitEntity = null;
                    info.HitMaterial = 0;
                    info.PointStart = Vector3.zero;

                    OnDeath();
                }
                else
                {
                    ins.NextTick(() =>
                    {
                        ins.CreateHealthUI(player, this);
                        foreach (var passenger in passengers)
                            ins.CreateHealthUI(passenger, this);
                    });
                }
            }

            private void OnDeath()
            {
                if (player != null)
                    ExitTank();

                Effect.server.Run(entity.explosionEffect.resourcePath, entity.transform.position, Vector3.up, null, true);

                List<ServerGib> serverGibs = ServerGib.CreateGibs(entity.servergibs.resourcePath, entity.gameObject, entity.servergibs.Get().GetComponent<ServerGib>()._gibSource, Vector3.zero, 3f);
                for (int i = 0; i < 12 - entity.maxCratesToSpawn; i++)
                {
                    BaseEntity fireBall = GameManager.server.CreateEntity(entity.fireBall.resourcePath, entity.transform.position, entity.transform.rotation, true);
                    if (fireBall)
                    {
                        Vector3 onSphere = UnityEngine.Random.onUnitSphere;
                        fireBall.transform.position = (entity.transform.position + new Vector3(0f, 1.5f, 0f)) + (onSphere * UnityEngine.Random.Range(-4f, 4f));
                        Collider collider = fireBall.GetComponent<Collider>();
                        fireBall.Spawn();
                        fireBall.SetVelocity(Vector3.zero + (onSphere * UnityEngine.Random.Range(3, 10)));
                        foreach (ServerGib serverGib in serverGibs)
                            Physics.IgnoreCollision(collider, serverGib.GetCollider(), true);
                    }
                }

                if (ins.configData.Inventory.DropInv)
                {
                    inventory.Drop("assets/prefabs/misc/item drop/item_drop.prefab", (entity.transform.position + new Vector3(0f, 1.5f, 0f)) + (UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(2f, 3f)), new Quaternion());
                }
                if (ins.configData.Inventory.DropLoot)
                {
                    for (int j = 0; j < entity.maxCratesToSpawn; j++)
                    {
                        Vector3 onSphere = UnityEngine.Random.onUnitSphere;
                        BaseEntity lootCrate = GameManager.server.CreateEntity(entity.crateToDrop.resourcePath, (entity.transform.position + new Vector3(0f, 1.5f, 0f)) + (onSphere * UnityEngine.Random.Range(2f, 3f)), Quaternion.LookRotation(onSphere), true);
                        lootCrate.Spawn();

                        LootContainer lootContainer = lootCrate as LootContainer;
                        if (lootContainer)
                            lootContainer.Invoke(new Action(lootContainer.RemoveMe), 1800f);

                        Collider collider = lootCrate.GetComponent<Collider>();
                        Rigidbody rigidbody = lootCrate.gameObject.AddComponent<Rigidbody>();
                        rigidbody.useGravity = true;
                        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                        rigidbody.mass = 2f;
                        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                        rigidbody.velocity = Vector3.zero + (onSphere * UnityEngine.Random.Range(1f, 3f));
                        rigidbody.angularVelocity = Vector3Ex.Range(-1.75f, 1.75f);
                        rigidbody.drag = 0.5f * (rigidbody.mass / 5f);
                        rigidbody.angularDrag = 0.2f * (rigidbody.mass / 5f);

                        FireBall fireBall = GameManager.server.CreateEntity(entity.fireBall.resourcePath, lootCrate.transform.position, new Quaternion(), true) as FireBall;
                        if (fireBall)
                        {
                            fireBall.transform.position = lootCrate.transform.position;
                            fireBall.Spawn();
                            fireBall.GetComponent<Rigidbody>().isKinematic = true;
                            fireBall.GetComponent<Collider>().enabled = false;
                            fireBall.transform.parent = lootCrate.transform;
                        }
                        lootCrate.SendMessage("SetLockingEnt", fireBall.gameObject, SendMessageOptions.DontRequireReceiver);

                        foreach (ServerGib serverGib1 in serverGibs)
                            Physics.IgnoreCollision(collider, serverGib1.GetCollider(), true);
                    }
                }
                if (entity != null && !entity.IsDestroyed)
                    entity.Kill(BaseNetworkable.DestroyMode.Gib);
            }
        }
        #endregion

        #region Commands
        [ChatCommand("spawntank")]
        void cmdTank(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "tankcommander.admin")) return;

            Vector3 position = player.transform.position + (player.transform.forward * 3) + Vector3.up;

            RaycastHit hit;
            if (Physics.SphereCast(player.eyes.position, 0.5f, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward, out hit, 20f))
                position = hit.point;

            BaseEntity entity = GameManager.server.CreateEntity(tankPrefab, position);
            entity.Spawn();

            Controller commander = entity.gameObject.AddComponent<Controller>();
        }

        [ConsoleCommand("spawntank")]
        void ccmdSpawnTank(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null || arg.Args == null)
                return;

            if (arg.Args.Length == 1)
            {
                BasePlayer player = covalence.Players.Connected.FirstOrDefault(x => x.Id == arg.GetString(0))?.Object as BasePlayer;
                if (player != null)
                {
                    Vector3 position = player.transform.position + (player.transform.forward * 3) + Vector3.up;

                    RaycastHit hit;
                    if (Physics.SphereCast(player.eyes.position, 0.5f, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward, out hit, 20f))
                        position = hit.point;

                    BaseEntity entity = GameManager.server.CreateEntity(tankPrefab, position);
                    entity.Spawn();

                    Controller commander = entity.gameObject.AddComponent<Controller>();
                }
                return;
            }
            if (arg.Args.Length == 3)
            {
                float x;
                float y;
                float z;

                if (float.TryParse(arg.GetString(0), out x))
                {
                    if (float.TryParse(arg.GetString(1), out y))
                    {
                        if (float.TryParse(arg.GetString(2), out z))
                        {
                            BaseEntity entity = GameManager.server.CreateEntity(tankPrefab, new Vector3(x, y, z));
                            entity.Spawn();
                            Controller commander = entity.gameObject.AddComponent<Controller>();
                            return;
                        }
                    }
                }
                PrintError($"Invalid arguments supplied to spawn a tank at position : (x = {arg.GetString(0)}, y = {arg.GetString(1)}, z = {arg.GetString(2)})");
            }
        }
        #endregion

        #region Friends
        private bool AreFriends(ulong playerId, ulong friendId)
        {
            if (Friends && configData.Passengers.UseFriends)
                return (bool)Friends?.Call("AreFriendsS", playerId.ToString(), friendId.ToString());
            return true;
        }
        private bool IsClanmate(ulong playerId, ulong friendId)
        {
            if (Clans && configData.Passengers.UseClans)
            {
                object playerTag = Clans?.Call("GetClanOf", playerId);
                object friendTag = Clans?.Call("GetClanOf", friendId);
                if (playerTag is string && friendTag is string)
                {
                    if (!string.IsNullOrEmpty((string)playerTag) && !string.IsNullOrEmpty((string)friendTag) && (playerTag == friendTag))
                        return true;
                }
                return false;
            }
            return true;
        }
        #endregion

        #region UI
        #region UI Elements
        public static class UI
        {
            static public CuiElementContainer ElementContainer(string panelName, string color, UI4 dimensions, bool useCursor = false, string parent = "Overlay")
            {
                var NewElement = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax()},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
                        panelName
                    }
                };
                return NewElement;
            }
            static public void Panel(ref CuiElementContainer container, string panel, string color, UI4 dimensions, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() },
                    CursorEnabled = cursor
                },
                panel);
            }
            static public void Label(ref CuiElementContainer container, string panel, string text, int size, UI4 dimensions, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Align = align, Text = text, Font = "droidsansmono.ttf" },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() }
                },
                panel);

            }
            public static string Color(string hexColor, float alpha)
            {
                if (hexColor.StartsWith("#"))
                    hexColor = hexColor.Substring(1);
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }
        public class UI4
        {
            public float xMin, yMin, xMax, yMax;
            public UI4(float xMin, float yMin, float xMax, float yMax)
            {
                this.xMin = xMin;
                this.yMin = yMin;
                this.xMax = xMax;
                this.yMax = yMax;
            }
            public string GetMin() => $"{xMin} {yMin}";
            public string GetMax() => $"{xMax} {yMax}";
        }
        #endregion

        #region UI Creation
        private void CreateHealthUI(BasePlayer player, Controller controller)
        {
            var container = UI.ElementContainer(uiHealth, "0.95 0.95 0.95 0.05", new UI4(0.69f, 0.1f, 0.83f, 0.135f));
            UI.Label(ref container, uiHealth, $"HLTH: ", 12, new UI4(0.03f, 0, 1, 1), TextAnchor.MiddleLeft);
            var percentHealth = System.Convert.ToDouble((float)controller.entity.health / (float)controller.entity.MaxHealth());
            float yMaxHealth = 0.25f + (0.73f * (float)percentHealth);
            UI.Panel(ref container, uiHealth, UI.Color("#ce422b", 0.6f), new UI4(0.25f, 0.1f, yMaxHealth, 0.9f));
            DestroyUI(player, uiHealth);
            CuiHelper.AddUi(player, container);
        }
        private void CreateMGAmmoUI(BasePlayer player, Controller controller)
        {
            if (configData.Weapons.MG.Enabled && configData.Weapons.MG.RequireAmmo)
            {
                var container = UI.ElementContainer(uiAmmoMg, "0.95 0.95 0.95 0.05", new UI4(0.69f, 0.060f, 0.83f, 0.096f));
                UI.Label(ref container, uiAmmoMg, $"MGUN: <color=#ce422b>{controller.inventory.GetAmount(mgId, false)}</color>", 12, new UI4(0.03f, 0, 1, 1), TextAnchor.MiddleLeft);
                DestroyUI(player, uiAmmoMg);
                CuiHelper.AddUi(player, container);
            }
        }
        private void CreateRocketAmmoUI(BasePlayer player, Controller controller)
        {
            if (configData.Weapons.Cannon.Enabled && configData.Weapons.Cannon.RequireAmmo)
            {
                var container = UI.ElementContainer(uiAmmoRocket, "0.95 0.95 0.95 0.05", new UI4(0.69f, 0.021f, 0.83f, 0.056f));
                UI.Label(ref container, uiAmmoRocket, $"CNON: <color=#ce422b>{controller.inventory.GetAmount(rocketId, false)}</color>", 12, new UI4(0.03f, 0, 1, 1), TextAnchor.MiddleLeft);
                DestroyUI(player, uiAmmoRocket);
                CuiHelper.AddUi(player, container);
            }
        }
        private void DestroyUI(BasePlayer player, string panel) => CuiHelper.DestroyUi(player, panel);

        private void DestroyAllUI(BasePlayer player)
        {
            DestroyUI(player, uiHealth);
            DestroyUI(player, uiAmmoMg);
            DestroyUI(player, uiAmmoRocket);
        }
        #endregion
        #endregion

        #region Config
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Movement Settings")]
            public MovementSettings Movement { get; set; }
            [JsonProperty(PropertyName = "Button Configuration")]
            public ButtonConfiguration Buttons { get; set; }
            [JsonProperty(PropertyName = "Crushable Types")]
            public CrushableTypes Crushables { get; set; }
            [JsonProperty(PropertyName = "Passenger Options")]
            public PassengerOptions Passengers { get; set; }
            [JsonProperty(PropertyName = "Inventory Options")]
            public InventoryOptions Inventory { get; set; }
            [JsonProperty(PropertyName = "Weapon Options")]
            public WeaponOptions Weapons { get; set; }

            public class CrushableTypes
            {
                [JsonProperty(PropertyName = "Can crush buildings")]
                public bool Buildings { get; set; }
                [JsonProperty(PropertyName = "Can crush resources")]
                public bool Resources { get; set; }
                [JsonProperty(PropertyName = "Can crush loot containers")]
                public bool Loot { get; set; }
                [JsonProperty(PropertyName = "Can crush animals")]
                public bool Animals { get; set; }
                [JsonProperty(PropertyName = "Can crush players")]
                public bool Players { get; set; }
                [JsonProperty(PropertyName = "Amount of force required to crush various building grades")]
                public Dictionary<string, float> GradeForce { get; set; }
                [JsonProperty(PropertyName = "Amount of force required to crush external walls")]
                public float WallForce { get; set; }
                [JsonProperty(PropertyName = "Amount of force required to crush resources")]
                public float ResourceForce { get; set; }
            }
            public class ButtonConfiguration
            {
                [JsonProperty(PropertyName = "Enter/Exit vehicle")]
                public string Enter { get; set; }
                [JsonProperty(PropertyName = "Toggle light")]
                public string Lights { get; set; }
                [JsonProperty(PropertyName = "Open inventory")]
                public string Inventory { get; set; }
                [JsonProperty(PropertyName = "Speed boost")]
                public string Boost { get; set; }
                [JsonProperty(PropertyName = "Fire Cannon")]
                public string Cannon { get; set; }
                [JsonProperty(PropertyName = "Fire Coaxial Gun")]
                public string Coax { get; set; }
                [JsonProperty(PropertyName = "Fire MG")]
                public string MG { get; set; }
            }
            public class MovementSettings
            {
                [JsonProperty(PropertyName = "Forward torque (nm)")]
                public float ForwardTorque { get; set; }
                [JsonProperty(PropertyName = "Rotation torque (nm)")]
                public float TurnTorque { get; set; }
                [JsonProperty(PropertyName = "Brake torque (nm)")]
                public float BrakeTorque { get; set; }
                [JsonProperty(PropertyName = "Time to reach maximum acceleration (seconds)")]
                public float Acceleration { get; set; }
                [JsonProperty(PropertyName = "Boost torque (nm)")]
                public float BoostTorque { get; set; }
            }
            public class PassengerOptions
            {
                [JsonProperty(PropertyName = "Allow passengers")]
                public bool Enabled { get; set; }
                [JsonProperty(PropertyName = "Maximum passengers per tank")]
                public int Max { get; set; }
                [JsonProperty(PropertyName = "Require passenger to be a friend (FriendsAPI)")]
                public bool UseFriends { get; set; }
                [JsonProperty(PropertyName = "Require passenger to be a clan mate (Clans)")]
                public bool UseClans { get; set; }
            }
            public class InventoryOptions
            {
                [JsonProperty(PropertyName = "Enable inventory system")]
                public bool Enabled { get; set; }
                [JsonProperty(PropertyName = "Drop inventory on death")]
                public bool DropInv { get; set; }
                [JsonProperty(PropertyName = "Drop loot on death")]
                public bool DropLoot { get; set; }
                [JsonProperty(PropertyName = "Inventory size (max 36)")]
                public int Size { get; set; }
            }
            public class WeaponOptions
            {
                [JsonProperty(PropertyName = "Cannon")]
                public WeaponSystem Cannon { get; set; }
                [JsonProperty(PropertyName = "Coaxial")]
                public WeaponSystem Coax { get; set; }
                [JsonProperty(PropertyName = "Machine Gun")]
                public WeaponSystem MG { get; set; }

                public class WeaponSystem
                {
                    [JsonProperty(PropertyName = "Enable weapon system")]
                    public bool Enabled { get; set; }
                    [JsonProperty(PropertyName = "Require ammunition in inventory")]
                    public bool RequireAmmo { get; set; }
                    [JsonProperty(PropertyName = "Ammunition type (item shortname)")]
                    public string Type { get; set; }
                    [JsonProperty(PropertyName = "Fire rate (seconds)")]
                    public float Interval { get; set; }
                    [JsonProperty(PropertyName = "Aim cone (smaller number is more accurate)")]
                    public float Accuracy { get; set; }
                    [JsonProperty(PropertyName = "Damage")]
                    public float Damage { get; set; }
                }
            }
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                Buttons = new ConfigData.ButtonConfiguration
                {
                    Enter = "USE",
                    Lights = "RELOAD",
                    Inventory = "RELOAD",
                    Boost = "SPRINT",
                    Cannon = "FIRE_PRIMARY",
                    Coax = "FIRE_SECONDARY",
                    MG = "FIRE_THIRD"
                },
                Crushables = new ConfigData.CrushableTypes
                {
                    Animals = true,
                    Buildings = true,
                    Loot = true,
                    Players = true,
                    Resources = true,
                    GradeForce = new Dictionary<string, float>
                    {
                        [BuildingGrade.Enum.Twigs.ToString()] = 1000f,
                        [BuildingGrade.Enum.Wood.ToString()] = 2000f,
                        [BuildingGrade.Enum.Stone.ToString()] = 3000f,
                        [BuildingGrade.Enum.Metal.ToString()] = 5000f,
                        [BuildingGrade.Enum.TopTier.ToString()] = 7000f,
                    },
                    ResourceForce = 1500f,
                    WallForce = 3000f
                },
                Movement = new ConfigData.MovementSettings
                {
                    Acceleration = 3f,
                    BrakeTorque = 50f,
                    ForwardTorque = 2000f,
                    TurnTorque = 2500f,
                    BoostTorque = 600f
                },
                Passengers = new ConfigData.PassengerOptions
                {
                    Enabled = true,
                    Max = 4,
                    UseClans = true,
                    UseFriends = true
                },
                Inventory = new ConfigData.InventoryOptions
                {
                    Enabled = true,
                    Size = 36,
                    DropInv = true,
                    DropLoot = false
                },
                Weapons = new ConfigData.WeaponOptions
                {
                    Cannon = new ConfigData.WeaponOptions.WeaponSystem
                    {
                        Accuracy = 0.025f,
                        Damage = 90f,
                        Enabled = true,
                        Interval = 1.75f,
                        RequireAmmo = false,
                        Type = "ammo.rocket.hv"
                    },
                    Coax = new ConfigData.WeaponOptions.WeaponSystem
                    {
                        Accuracy = 0.75f,
                        Damage = 10f,
                        Enabled = true,
                        Interval = 0.06667f,
                        RequireAmmo = false,
                        Type = "ammo.rifle.hv"
                    },
                    MG = new ConfigData.WeaponOptions.WeaponSystem
                    {
                        Accuracy = 1.25f,
                        Damage = 10f,
                        Enabled = true,
                        Interval = 0.1f,
                        RequireAmmo = false,
                        Type = "ammo.rifle.hv"
                    }
                }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Data Management
        void SaveData() => restorationdata.WriteObject(restoreData);
        void LoadData()
        {
            try
            {
                restoreData = restorationdata.ReadObject<RestoreData>();
            }
            catch
            {
                restoreData = new RestoreData();
            }
        }
        public class RestoreData
        {
            public Hash<ulong, PlayerData> restoreData = new Hash<ulong, PlayerData>();

            public void AddData(BasePlayer player)
            {
                restoreData[player.userID] = new PlayerData(player);
            }

            public void RemoveData(ulong playerId)
            {
                if (HasRestoreData(playerId))
                    restoreData.Remove(playerId);
            }

            public bool HasRestoreData(ulong playerId) => restoreData.ContainsKey(playerId);

            public void RestorePlayer(BasePlayer player)
            {
                PlayerData playerData;
                if (restoreData.TryGetValue(player.userID, out playerData))
                {
                    player.inventory.Strip();

                    if (player.IsSleeping() || player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
                    {
                        ins.timer.Once(1, () => RestorePlayer(player));
                        return;
                    }

                    ins.NextTick(() =>
                    {
                        RestoreAllItems(player, playerData);
                    });
                }
            }

            private void RestoreAllItems(BasePlayer player, PlayerData playerData)
            {
                if (player == null || !player.IsConnected)
                    return;

                if (RestoreItems(player, playerData.containerBelt, "belt") && RestoreItems(player, playerData.containerWear, "wear") && RestoreItems(player, playerData.containerMain, "main"))
                    RemoveData(player.userID);
            }

            private bool RestoreItems(BasePlayer player, ItemData[] itemData, string type)
            {
                ItemContainer container = type == "belt" ? player.inventory.containerBelt : type == "wear" ? player.inventory.containerWear : player.inventory.containerMain;

                for (int i = 0; i < itemData.Length; i++)
                {
                    ItemData data = itemData[i];
                    if (data.amount == 0)
                        continue;
                    Item item = CreateItem(data);
                    item.position = data.position;
                    item.SetParent(container);
                }
                return true;
            }

            private Item CreateItem(ItemData itemData)
            {
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
                return item;
            }

            public class PlayerData
            {
                public ItemData[] containerMain;
                public ItemData[] containerWear;
                public ItemData[] containerBelt;

                public PlayerData() { }

                public PlayerData(BasePlayer player)
                {
                    containerBelt = GetItems(player.inventory.containerBelt).ToArray();
                    containerMain = GetItems(player.inventory.containerMain).ToArray();
                    containerWear = GetItems(player.inventory.containerWear).ToArray();
                }

                private IEnumerable<ItemData> GetItems(ItemContainer container)
                {
                    return container.itemList.Select(item => new ItemData
                    {
                        itemid = item.info.itemid,
                        amount = item.amount,
                        ammo = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.contents ?? 0,
                        ammotype = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.ammoType.shortname ?? null,
                        position = item.position,
                        skin = item.skin,
                        condition = item.condition,
                        instanceData = item.instanceData ?? null,
                        contents = item.contents?.itemList.Select(item1 => new ItemData
                        {
                            itemid = item1.info.itemid,
                            amount = item1.amount,
                            condition = item1.condition
                        }).ToArray()
                    });
                }
            }

            public class ItemData
            {
                public int itemid;
                public ulong skin;
                public int amount;
                public float condition;
                public int ammo;
                public string ammotype;
                public int position;
                public ProtoBuf.Item.InstanceData instanceData;
                public ItemData[] contents;
            }
        }
        #endregion

        #region Localization
        string msg(string key, string playerId = null) => lang.GetMessage(key, this, playerId);

        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["is_flying"] = "<color=#D3D3D3>You can not enter the tank when you are flying</color>",
            ["in_use"] = "<color=#D3D3D3>This tank is already in use</color>",
            ["not_friend"] = "<color=#D3D3D3>You must be a friend or clanmate with the operator</color>",
            ["passenger_enter"] = "<color=#D3D3D3>You have entered the tank as a passenger</color>",
            ["controls"] = "<color=#ce422b>Tank Controls:</color>",
            ["fire_cannon"] = "<color=#D3D3D3>Fire Cannon </color><color=#ce422b>{0}</color>",
            ["fire_coax"] = "<color=#D3D3D3>Fire Coaxial Gun </color><color=#ce422b>{0}</color>",
            ["fire_mg"] = "<color=#D3D3D3>Fire MG </color><color=#ce422b>{0}</color>",
            ["speed_boost"] = "<color=#D3D3D3>Speed Boost </color><color=#ce422b>{0}</color>",
            ["enter_exit"] = "<color=#D3D3D3>Enter/Exit Vehicle </color><color=#ce422b>{0}</color>",
            ["toggle_lights"] = "<color=#D3D3D3>Toggle Lights </color><color=#ce422b>{0}</color>",
            ["access_inventory"] = "<color=#D3D3D3>Access Inventory (from outside of the vehicle) </color><color=#ce422b>{0}</color>",
            ["no_ammo_cannon"] = "<color=#D3D3D3>You do not have ammunition to fire the cannon. It requires </color><color=#ce422b>{0}</color>",
            ["no_ammo_mg"] = "<color=#D3D3D3>You do not have ammunition to fire the machine gun. It requires </color><color=#ce422b>{0}</color>",
            ["no_ammo_coax"] = "<color=#D3D3D3>You do not have ammunition to fire the coaxial gun. It requires </color><color=#ce422b>{0}</color>",
        };
        #endregion
    }
}