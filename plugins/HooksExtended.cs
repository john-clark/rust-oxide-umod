#define DEBUG
using System;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info ("HooksExtended", "Calytic", "0.1.7")]
    public class HooksExtended : RustPlugin
    {
        #region Variables

        List<uint> WoodMaterial = new List<uint> () {
            3655341,
            3642589474
        };

        List<uint> RockMaterial = new List<uint> () {
            3506021,
            3712324229,
            2486181786
        };

        List<uint> MetalMaterial = new List<uint> ()
        {
            103787271,
            4214819287,
            1632307590
        };

        List<uint> SnowMaterial = new List<uint> ()
        {
            3535235,
            3757806379
        };

        List<uint> GrassMaterial = new List<uint> () {
            98615734,
            3829453833
        };

        List<uint> SandMaterial = new List<uint> () {
            3522692,
            1533752200
        };

        Vector3 eyesPosition = new Vector3 (0f, 0.5f, 0f);
        static int playerLayer = LayerMask.GetMask ("Player (Server)");
        static int useLayer = LayerMask.GetMask (new string [] { "Player (Server)", "Construction", "Deployed", "Tree", "Resource", "Terrain", "AI", "Clutter", "Debris", "Vehicle Movement", "World", "Clutter" });
        List<BasePlayer> inputCooldown = new List<BasePlayer> ();
        Dictionary<BasePlayer, List<MonoBehaviour>> spotCooldown = new Dictionary<BasePlayer, List<MonoBehaviour>> ();
        int spottingMask = LayerMask.GetMask (new string [] { "Player (Server)", "Construction", "Deployed", "Tree", "Resource", "Terrain", "AI", "Clutter", "Debris", "Vehicle Movement", "World", "Clutter" });
        public PluginSettings settings;

        [OnlinePlayers]
        Hash<BasePlayer, PlayerProfile> onlinePlayers = new Hash<BasePlayer, PlayerProfile> ();

        #endregion

        #region Boilerplate

        Dictionary<string, bool> defaultHookSettings = new Dictionary<string, bool> () {
            {"OnPlayerTick", false},
            {"OnPlayerAttack", false},
            {"OnRunPlayerMetabolism", false},
            {"OnItemDeployed", false},
            {"OnEntityTakeDamage", false},
            {"OnConsumeFuel", false},
            {"OnEntityDeath", false},
            {"OnItemAddedToContainer", false},
            {"OnItemRemovedFromContainer", false},
            {"OnPlayerInput", false},
            {"OnItemCraft", false},
            {"OnItemCraftCancelled", false},
            {"OnItemCraftFinished", false},
            {"CanCraft", false},
            {"OnEntitySpawned", false},
            {"OnItemAction", false},
            {"CanNetworkTo", false},
            {"CanMountEntity", false},
            {"CanDismountEntity", false},
            {"OnEntityMounted", false},
            {"OnEntityDismounted", false},
            {"OnItemResearch", false},
            {"OnItemRepair", false},
            {"OnRecycleItem", false},
            {"OnStructureRepair", false},
            {"OnItemPickup", false},
            {"OnItemDropped", false},
            {"OnFindBurnable", false},
        };

        class PlayerProfile
        {
            public BasePlayer Player;
            public ProfileMetabolism Metabolism;

            public bool wasDucked;
            public bool wasDrowning;
            public bool wasSprinting;
            public bool wasFlying;
            public bool wasSwimming;
            public bool wasAiming;
            public bool wasReceivingSnapshot;
            public Item activeItem;
        }

        class ProfileMetabolism
        {
            double _P = 0.0001;

            public enum MetaAction
            {
                Start,
                Stop
            }

            float wetness;
            float radiation_poison;
            float radiation_level;
            float poison;
            float comfort;
            float bleeding;
            float oxygen;
            float dirtyness;

            public ProfileMetabolism (PlayerMetabolism metabolism)
            {
                Set (metabolism);
            }

            void Set (PlayerMetabolism metabolism)
            {
                wetness = metabolism.wetness.value;
                radiation_poison = metabolism.radiation_poison.value;
                radiation_level = metabolism.radiation_level.value;
                poison = metabolism.poison.value;
                comfort = metabolism.comfort.value;
                bleeding = metabolism.bleeding.value;
                dirtyness = metabolism.dirtyness.value;
            }

            public Dictionary<string, MetaAction> DetectChange (PlayerMetabolism metabolism)
            {
                Dictionary<string, MetaAction> actions = new Dictionary<string, MetaAction> ();

                if (!AlmostEquals (metabolism.dirtyness.value, dirtyness, _P)) {
                    if (AlmostEquals (metabolism.dirtyness.value, metabolism.dirtyness.min, _P)) {
                        actions.Add ("Dirtyness", MetaAction.Stop);
                    } else if (AlmostEquals (dirtyness, metabolism.dirtyness.min, _P)) {
                        actions.Add ("Dirtyness", MetaAction.Start);
                    }
                }

                if (!AlmostEquals (metabolism.wetness.value, wetness, _P)) {
                    if (AlmostEquals (metabolism.wetness.value, metabolism.wetness.min, _P)) {
                        actions.Add ("Wetness", MetaAction.Stop);
                    } else if (AlmostEquals (wetness, metabolism.wetness.min, _P)) {
                        actions.Add ("Wetness", MetaAction.Start);
                    }
                }

                if (!AlmostEquals (metabolism.poison.value, poison, _P)) {
                    if (AlmostEquals (metabolism.poison.value, metabolism.poison.min, _P)) {
                        actions.Add ("Poison", MetaAction.Stop);
                    } else if (AlmostEquals (poison, metabolism.poison.min, _P)) {
                        actions.Add ("Poison", MetaAction.Start);
                    }
                }

                if (!AlmostEquals (metabolism.oxygen.value, oxygen, _P)) {
                    if (AlmostEquals (metabolism.oxygen.value, metabolism.oxygen.min, _P)) {
                        actions.Add ("Drowning", MetaAction.Stop);
                    } else if (AlmostEquals (oxygen, metabolism.oxygen.min, _P)) {
                        actions.Add ("Drowning", MetaAction.Start);
                    }
                }

                if (!AlmostEquals (metabolism.radiation_level.value, radiation_level, _P)) {
                    if (AlmostEquals (metabolism.radiation_level.value, metabolism.radiation_level.min, _P)) {
                        actions.Add ("Radiation", MetaAction.Stop);
                    } else if (AlmostEquals (radiation_level, metabolism.radiation_level.min, _P)) {
                        actions.Add ("Radiation", MetaAction.Start);
                    }
                }

                if (!AlmostEquals (metabolism.radiation_poison.value, radiation_poison, _P)) {
                    if (AlmostEquals (metabolism.radiation_poison.value, metabolism.radiation_poison.min, _P)) {
                        actions.Add ("RadiationPoison", MetaAction.Stop);
                    } else if (AlmostEquals (radiation_poison, metabolism.radiation_poison.min, _P)) {
                        actions.Add ("RadiationPoison", MetaAction.Start);
                    }
                }

                if (!AlmostEquals (metabolism.comfort.value, comfort, _P)) {
                    if (AlmostEquals (metabolism.comfort.value, metabolism.comfort.min, _P)) {
                        actions.Add ("Comfort", MetaAction.Stop);
                    } else if (AlmostEquals (comfort, metabolism.comfort.min, _P)) {
                        actions.Add ("Comfort", MetaAction.Start);
                    }
                }

                if (!AlmostEquals (metabolism.bleeding.value, bleeding, _P)) {
                    if (AlmostEquals (metabolism.bleeding.value, metabolism.bleeding.min, _P)) {
                        actions.Add ("Bleeding", MetaAction.Stop);
                    } else if (AlmostEquals (bleeding, metabolism.bleeding.min, _P)) {
                        actions.Add ("Bleeding", MetaAction.Start);
                    }
                }

                Set (metabolism);

                return actions;
            }

            public bool AlmostEquals (double double1, double double2, double precision)
            {
                return (Math.Abs (double1 - double2) <= precision);
            }
        }

        public class PluginSettings
        {
            public Dictionary<string, bool> HookSettings;
            public string VERSION;
        }

        Dictionary<Type, string> killableTypes = new Dictionary<Type, string> ()
        {
            {typeof(PlantEntity),"OnPlantDeath"},
            {typeof(SearchLight),"OnSearchLightDeath"},
            {typeof(BuildingBlock),"OnBuildingDeath"},
            {typeof(NPCPlayer),"OnNPCDeath"},
            {typeof(HTNPlayer),"OnNPCDeath"},
            {typeof(BasePlayer),"OnPlayerDeath"},
            {typeof(AutoTurret),"OnTurretDeath"},
            {typeof(FlameTurret),"OnTurretDeath"},
            {typeof(SamSite),"OnTurretDeath"},
            {typeof(BaseHelicopter),"OnHelicopterDeath"},
            {typeof(BuildingPrivlidge),"OnCupboardDeath"},
            {typeof(BaseCorpse),"OnCorpseDeath"},
            {typeof(SleepingBag),"OnSleepingBagDeath"},
            {typeof(BaseAnimalNPC),"OnAnimalDeath"},
            {typeof(BradleyAPC),"OnBradleyAPCDeath"},
            {typeof(BaseTrap),"OnTrapDeath"},
            {typeof(GunTrap),"OnTrapDeath"},
            {typeof(WildlifeTrap),"OnTrapDeath"},
            {typeof(SurvivalFishTrap),"OnTrapDeath"},
            {typeof(BaseFuelLightSource),"OnLightDeath"},
            {typeof(ShopFront),"OnShopDeath"},
            {typeof(VendingMachine),"OnVendorDeath"},
            {typeof(StorageContainer),"OnContainerDeath"},
            {typeof(BaseBoat),"OnBoatDeath"},
            {typeof(BaseCar),"OnCarDeath"},
            {typeof(CH47Helicopter),"OnChinookDeath"},
            {typeof(BaseLadder),"OnLadderDeath"},
            {typeof(HotAirBalloon),"OnHotAirBalloonDeath"}
        };

        Dictionary<Type, string> canMountMountableTypes = new Dictionary<Type, string> ()
        {
            {typeof(SearchLight),"CanMountSearchLight"},
            {typeof(BaseBoat),"CanMountBoat"},
            {typeof(BaseCar),"CanMountCar"},
            {typeof(BaseChair),"CanMountChair"},
            {typeof(BaseVehicleSeat),"CanMountSeat"},
            {typeof(BaseHelicopterVehicle),"CanMountHelicopter"}
        };

        Dictionary<Type, string> canDismountMountableTypes = new Dictionary<Type, string> ()
        {
            {typeof(SearchLight),"CanDismountSearchLight"},
            {typeof(BaseBoat),"CanDismountBoat"},
            {typeof(BaseCar),"CanDismountCar"},
            {typeof(BaseChair),"CanDismountChair"},
            {typeof(BaseVehicleSeat),"CanDismountSeat"},
            {typeof(BaseHelicopterVehicle),"CanDismountHelicopter"}
        };

        Dictionary<Type, string> mountedMountableTypes = new Dictionary<Type, string> ()
        {
            {typeof(SearchLight),"OnSearchLightMounted"},
            {typeof(BaseBoat),"OnBoatMounted"},
            {typeof(BaseCar),"OnCarMounted"},
            {typeof(BaseChair),"OnChairMounted"},
            {typeof(BaseVehicleSeat),"OnSeatMounted"},
            {typeof(BaseHelicopterVehicle),"OnHelicopterMounted"}
        };

        Dictionary<Type, string> dismountedMountableTypes = new Dictionary<Type, string> ()
        {
            {typeof(SearchLight),"OnSearchLightDismounted"},
            {typeof(BaseBoat),"OnBoatDismounted"},
            {typeof(BaseCar),"OnCarDismounted"},
            {typeof(BaseChair),"OnChairDismounted"},
            {typeof(BaseVehicleSeat),"OnSeatDismounted"},
            {typeof(BaseHelicopterVehicle),"OnHelicopterDismounted"}
        };

        Dictionary<Type, string> spawnableTypes = new Dictionary<Type, string> ()
        {
            {typeof(PlantEntity),"OnPlantSpawned"},
            {typeof(SearchLight),"OnSearchLightSpawned"},
            {typeof(SupplyDrop),"OnSupplyDropSpawned"},
            {typeof(BaseHelicopter),"OnHelicopterSpawned"},
            {typeof(HelicopterDebris),"OnHelicopterDebrisSpawned"},
            {typeof(BaseAnimalNPC),"OnAnimalSpawned"},
            {typeof(NPCPlayer),"OnNPCSpawned"},
            {typeof(HTNPlayer),"OnNPCSpawned"},
            {typeof(BuildingPrivlidge),"OnCupboardSpawned"},
            {typeof(BaseCorpse),"OnCorpseSpawned"},
            {typeof(SleepingBag),"OnSleepingBagSpawned"},
            {typeof(AutoTurret),"OnTurretSpawned"},
            {typeof(FlameTurret),"OnTurretSpawned"},
            {typeof(SamSite),"OnTurretSpawned"},
            {typeof(BradleyAPC),"OnBradleyAPCSpawned"},
            {typeof(BaseTrap),"OnTrapSpawned"},
            {typeof(GunTrap),"OnTrapSpawned"},
            {typeof(WildlifeTrap),"OnTrapSpawned"},
            {typeof(SurvivalFishTrap),"OnTrapSpawned"},
            {typeof(BaseFuelLightSource),"OnLightSpawned"},
            {typeof(ShopFront),"OnShopSpawned"},
            {typeof(VendingMachine),"OnVendorSpawned"},
            {typeof(StorageContainer),"OnContainerSpawned"},
            {typeof(BaseBoat),"OnBoatSpawned"},
            {typeof(BaseCar),"OnCarSpawned"},
            {typeof(CH47Helicopter),"OnChinookSpawned"},
            {typeof(BaseLadder),"OnLadderSpawned"},
            {typeof(HotAirBalloon),"OnHotAirBalloonSpawned"}
        };

        Dictionary<Type, string> networkableTypes = new Dictionary<Type, string> ()
        {
            {typeof(PlantEntity),"CanPlantNetworkTo"},
            {typeof(SearchLight),"CanSearchLightNetworkTo"},
            {typeof(NPCPlayer),"CanNPCNetworkTo"},
            {typeof(HTNPlayer),"CanNPCNetworkTo"},
            {typeof(BasePlayer),"CanPlayerNetworkTo"},
            {typeof(SupplyDrop),"CanSupplyDropNetworkTo"},
            {typeof(BaseHelicopter),"CanHelicopterNetworkTo"},
            {typeof(HelicopterDebris),"CanHelicopterDebrisNetworkTo"},
            {typeof(BaseAnimalNPC),"CanAnimalNetworkTo"},
            {typeof(BuildingPrivlidge),"CanCupboardNetworkTo"},
            {typeof(BaseCorpse),"CanCorpseNetworkTo"},
            {typeof(SleepingBag),"CanSleepingBagNetworkTo"},
            {typeof(AutoTurret),"CanTurretNetworkTo"},
            {typeof(FlameTurret),"CanTurretNetworkTo"},
            {typeof(SamSite),"CanTurretNetworkTo"},
            {typeof(BradleyAPC),"CanBradleyAPCNetworkTo"},
            {typeof(BaseTrap),"CanTrapNetworkTo"},
            {typeof(GunTrap),"CanTrapNetworkTo"},
            {typeof(WildlifeTrap),"CanTrapNetworkTo"},
            {typeof(SurvivalFishTrap),"CanTrapNetworkTo"},
            {typeof(BaseFuelLightSource),"CanLightNetworkTo"},
            {typeof(ShopFront),"CanShopNetworkTo"},
            {typeof(VendingMachine),"CanVendorNetworkTo"},
            {typeof(StorageContainer),"CanContainerNetworkTo"},
            {typeof(BaseBoat),"CanBoatNetworkTo"},
            {typeof(BaseCar),"CanCarNetworkTo"},
            {typeof(CH47Helicopter),"CanChinookNetworkTo"},
            {typeof(BaseLadder),"CanLadderNetworkTo"},
            {typeof(HotAirBalloon),"CanHotAirBalloonNetworkTo"}
        };

        Dictionary<Type, string> damagableTypes = new Dictionary<Type, string> ()
        {
            {typeof(SearchLight),"OnSearchLightDamage"},
            {typeof(NPCPlayer),"OnNPCDamage"},
            {typeof(HTNPlayer),"OnNPCDamage"},
            {typeof(BuildingBlock),"OnBuildingDamage"},
            {typeof(BasePlayer),"OnPlayerDamage"},
            {typeof(AutoTurret),"OnTurretDamage"},
            {typeof(FlameTurret),"OnTurretDamage"},
            {typeof(SamSite),"OnTurretDamage"},
            {typeof(BaseHelicopter),"OnHelicopterDamage"},
            {typeof(BuildingPrivlidge),"OnCupboardDamage"},
            {typeof(BaseCorpse),"OnCorpseDamage"},
            {typeof(SleepingBag),"OnSleepingBagDamage"},
            {typeof(BaseAnimalNPC),"OnAnimalDamage"},
            {typeof(BradleyAPC),"OnBradleyAPCDamage"},
            {typeof(BaseTrap),"OnTrapDamage"},
            {typeof(GunTrap),"OnTrapDamage"},
            {typeof(WildlifeTrap),"OnTrapDamage"},
            {typeof(SurvivalFishTrap),"OnTrapDamage"},
            {typeof(BaseFuelLightSource),"OnLightDamage"},
            {typeof(ShopFront),"OnShopDamage"},
            {typeof(VendingMachine),"OnVendorDamage"},
            {typeof(StorageContainer),"OnContainerDamage"},
            {typeof(BaseBoat),"OnBoatDamage"},
            {typeof(BaseCar),"OnCarDamage"},
            {typeof(CH47Helicopter),"OnChinookDamage"},
            {typeof(BaseLadder),"OnLadderDamage"},
            {typeof(HotAirBalloon),"OnHotAirBalloonDamage"}
        };

        Dictionary<Type, string> repairableTypes = new Dictionary<Type, string> ()
        {
            {typeof(SearchLight),"OnSearchLightRepair"},
            {typeof(BuildingBlock),"OnBuildingRepair"},
            {typeof(BuildingPrivlidge),"OnCupboardRepair"},
            {typeof(AutoTurret),"OnTurretRepair"},
            {typeof(FlameTurret),"OnTurretRepair"},
            {typeof(SamSite),"OnTurretRepair"},
            {typeof(Door),"OnDoorRepair"},
            {typeof(Barricade),"OnBarricadeRepair"},
            {typeof(Stocking),"OnStockingRepair"},
            {typeof(SleepingBag),"OnSleepingBagRepair"},
            {typeof(Signage),"OnSignRepair"},
            {typeof(BaseTrap),"OnTrapRepair"},
            {typeof(GunTrap),"OnTrapRepair"},
            {typeof(WildlifeTrap),"OnTrapRepair"},
            {typeof(SurvivalFishTrap),"OnTrapRepair"},
            {typeof(BaseFuelLightSource),"OnLightRepair"},
            {typeof(ShopFront),"OnShopRepair"},
            {typeof(VendingMachine),"OnVendorRepair"},
            {typeof(StorageContainer),"OnContainerRepair"},
            {typeof(BaseLadder),"OnLadderRepair"},
            {typeof(BaseBoat),"OnBoatRepair"},
            {typeof(BaseCar),"OnCarRepair"}
        };

        Dictionary<Type, string> attackTypes = new Dictionary<Type, string> ()
        {
            {typeof(AutoTurret),"OnTurretAttack"},
            {typeof(FlameTurret),"OnTurretAttack"},
            {typeof(SamSite),"OnTurretAttack"},
            {typeof(BaseHelicopter),"OnHelicopterAttack"},
            {typeof(BaseAnimalNPC),"OnAnimalAttack"},
            {typeof(NPCPlayer),"OnNPCAttack"},
            {typeof(HTNPlayer),"OnNPCAttack"},
            {typeof(BradleyAPC),"OnBradleyAPCAttack"},
            {typeof(BaseTrap),"OnTrapAttack"},
            {typeof(GunTrap),"OnTrapAttack"},
            {typeof(CH47Helicopter),"OnChinookAttack"}
        };

        Dictionary<Type, string> deployableTypes = new Dictionary<Type, string> ()
        {
            {typeof(PlantEntity),"OnPlantDeployed"},
            {typeof(SearchLight),"OnSearchLightDeployed"},
            {typeof(BuildingPrivlidge),"OnCupboardDeployed"},
            {typeof(AutoTurret),"OnTurretDeployed"},
            {typeof(FlameTurret),"OnTurretDeployed"},
            {typeof(SamSite),"OnTurretDeployed"},
            {typeof(Door),"OnDoorDeployed"},
            {typeof(Barricade),"OnBarricadeDeployed"},
            {typeof(Stocking),"OnStockingDeployed"},
            {typeof(SleepingBag),"OnSleepingBagDeployed"},
            {typeof(Signage),"OnSignDeployed"},
            {typeof(BaseTrap),"OnTrapDeployed"},
            {typeof(GunTrap),"OnTrapDeployed"},
            {typeof(WildlifeTrap),"OnTrapDeployed"},
            {typeof(SurvivalFishTrap),"OnTrapDeployed"},
            {typeof(BaseFuelLightSource),"OnLightDeployed"},
            {typeof(ShopFront),"OnShopDeployed"},
            {typeof(VendingMachine),"OnVendorDeployed"},
            {typeof(StorageContainer),"OnContainerDeployed"},
            {typeof(BaseLadder),"OnLadderDeployed"}
        };

        Dictionary<Type, string> spottableTypes = new Dictionary<Type, string> ()
        {
            {typeof(PlantEntity),"OnSpotPlant"},
            {typeof(NPCPlayer),"OnSpotNPC"},
            {typeof(HTNPlayer),"OnSpotNPC"},
            {typeof(BasePlayer),"OnSpotPlayer"},
            {typeof(BaseAnimalNPC),"OnSpotAnimal"},
            {typeof(BuildingPrivlidge),"OnSpotCupboard"},
            {typeof(AutoTurret),"OnSpotTurret"},
            {typeof(FlameTurret),"OnSpotTurret"},
            {typeof(SamSite),"OnSpotTurret"},
            {typeof(BaseHelicopter),"OnSpotHelicopter"},
            {typeof(ResourceDispenser),"OnSpotResource"},
            {typeof(BradleyAPC),"OnSpotBradleyAPC"},
            {typeof(BaseTrap),"OnSpotTrap"},
            {typeof(GunTrap),"OnSpotTrap"},
            {typeof(BaseFuelLightSource),"OnSpotLight"},
            {typeof(SearchLight),"OnSpotSearchLight"},
            {typeof(ShopFront),"OnSpotShop"},
            {typeof(VendingMachine),"OnSpotVendor"},
            {typeof(StorageContainer),"OnSpotContainer"},
            {typeof(BaseBoat),"OnSpotBoat"},
            {typeof(BaseCar),"OnSpotCar"},
            {typeof(CH47Helicopter),"OnSpotChinook"},
            {typeof(BaseLadder),"OnSpotLadder"},
            {typeof(HotAirBalloon),"OnSpotHotAirBalloon"}
        };

        Dictionary<Type, string> usableTypes = new Dictionary<Type, string> ()
        {
            {typeof(PlantEntity),"OnUsePlant"},
            {typeof(ResearchTable),"OnUseResearchTable"},
            {typeof(RepairBench),"OnUseRepairBench"},
            {typeof(NPCPlayer),"OnUseNPC"},
            {typeof(HTNPlayer),"OnUseNPC"},
            {typeof(BasePlayer),"OnUsePlayer"},
            {typeof(BaseAnimalNPC),"OnUseAnimal"},
            {typeof(BuildingPrivlidge),"OnUseCupboard"},
            {typeof(AutoTurret),"OnUseTurret"},
            {typeof(FlameTurret),"OnUseTurret"},
            {typeof(SamSite),"OnUseTurret"},
            {typeof(BaseHelicopter),"OnUseHelicopter"},
            {typeof(ResourceDispenser),"OnUseResource"},
            {typeof(BradleyAPC),"OnUseBradleyAPC"},
            {typeof(BaseTrap),"OnUseTrap"},
            {typeof(GunTrap),"OnUseTrap"},
            {typeof(WildlifeTrap),"OnUseTrap"},
            {typeof(SurvivalFishTrap),"OnUseTrap"},
            {typeof(BaseFuelLightSource),"OnUseLight"},
            {typeof(SearchLight),"OnUseSearchLight"},
            {typeof(Recycler),"OnUseRecycler"},
            {typeof(ShopFront),"OnUseShop"},
            {typeof(VendingMachine),"OnUseVendor"},
            {typeof(StorageContainer),"OnUseContainer"},
            {typeof(SleepingBag),"OnUseSleepingBag"},
            {typeof(BuildingBlock),"OnUseBuilding"},
            {typeof(BaseBoat),"OnUseBoat"},
            {typeof(BaseCar),"OnUseCar"},
            {typeof(CH47Helicopter),"OnUseChinook"},
            {typeof(BaseLadder),"OnUseLadder"},
            {typeof(HotAirBalloon),"OnUseHotAirBalloon"}
        };

        Dictionary<Type, string> lootableEntityTypes = new Dictionary<Type, string> ()
        {
            {typeof(AutoTurret),"OnLootTurret"},
            {typeof(FlameTurret),"OnLootTurret"},
            {typeof(SamSite),"OnLootTurret"},
            {typeof(BaseOven),"OnLootOven"},
            {typeof(GunTrap),"OnLootTrap"},
            {typeof(LiquidContainer),"OnLootLiquid"},
            {typeof(Locker),"OnLootLocker"},
            {typeof(Mailbox),"OnLootMailbox"},
            {typeof(Recycler),"OnLootRecycler"},
            {typeof(RepairBench),"OnLootBench"},
            {typeof(ResearchTable),"OnLootResearchTable"},
            {typeof(BaseFuelLightSource),"OnLootLight"},
            {typeof(SearchLight),"OnLootSearchLight"},
            {typeof(ShopFront),"OnLootShop"},
            {typeof(StashContainer),"OnLootStash"},
            {typeof(SupplyDrop),"OnLootSupplyDrop"},
            {typeof(SurvivalFishTrap),"OnLootTrap"},
            {typeof(WildlifeTrap),"OnLootTrap"},
            {typeof(VendingMachine),"OnLootVendor"},
            {typeof(BuildingPrivlidge),"OnLootCupboard"},
            {typeof(BaseCorpse),"OnLootCorpse"},
            {typeof(StorageContainer),"OnLootContainer"},
            {typeof(Stocking),"OnLootStocking"},
        };

        List<Type> castTypes = new List<Type> () {
            typeof(AutoTurret),
            typeof(FlameTurret),
            typeof(BaseOven),
            typeof(GunTrap),
            typeof(LiquidContainer),
            typeof(Locker),
            typeof(Mailbox),
            typeof(Recycler),
            typeof(RepairBench),
            typeof(ResearchTable),
            typeof(SamSite),
            typeof(SearchLight),
            typeof(ShopFront),
            typeof(StashContainer),
            typeof(SupplyDrop),
            typeof(SurvivalFishTrap),
            typeof(WildlifeTrap),
            typeof(VendingMachine),
            typeof(Stocking),
            typeof(NPCPlayer),
            typeof(HTNPlayer),
            typeof(BasePlayer),
            typeof(BaseAnimalNPC),
            typeof(BuildingPrivlidge),
            typeof(BaseHelicopter),
            typeof(ResourceDispenser),
            typeof(BradleyAPC),
            typeof(BaseTrap),
            typeof(StorageContainer),
            typeof(SleepingBag),
            typeof(BuildingBlock),
            typeof(BaseBoat),
            typeof(BaseCar),
            typeof(CH47Helicopter),
            typeof(BaseLadder),
            typeof(HotAirBalloon),
            typeof(BaseFuelLightSource),
            typeof(PlantEntity)
        };

        Dictionary<Type, Func<object, object>> typeCasting = new Dictionary<Type, Func<object, object>> ();

        #endregion

        #region Initialization & Configuration

        void Init ()
        {
            //for (var i = 0; i <= 30; i++) {
            //    PrintWarning (LayerMask.LayerToName (i));
            //}

            var method = typeof (HooksExtended).GetMethod ("TypeConverter");
            foreach (Type type in castTypes) {
                var genericMethod = method.MakeGenericMethod (type);
                var delegateFn = (Func<object, object>)genericMethod.Invoke (this, null);
                if (delegateFn != null) {
                    typeCasting.Add (type, delegateFn);
                }
            }

            UnsubscribeHooks ();
        }

        void OnServerInitialized ()
        {
            if (settings == null) {
                LoadConfigValues ();
            }

            SubscribeHooks ();
        }

        void UnsubscribeHooks ()
        {
            foreach (KeyValuePair<string, bool> kvp in defaultHookSettings) {
                Unsubscribe (kvp.Key);
            }
        }

        void SubscribeHooks ()
        {
            if (settings == null) {
                PrintError ("Settings invalid");
                return;
            }

            if (settings.HookSettings == null) {
                PrintError ("Hook Settings invalid");
                return;
            }

            foreach (KeyValuePair<string, bool> kvp in settings.HookSettings) {
                if (kvp.Value) {
                    Subscribe (kvp.Key);
                }
            }
        }

        void EnableHook (string hookName, bool save = true)
        {
            ConfigureHook (hookName, true, save);
        }

        void EnableHooks (params string [] hookNames)
        {
            foreach (string hookName in hookNames) {
                EnableHook (hookName, false);
            }

            SaveSettings ();
            UnsubscribeHooks ();
            SubscribeHooks ();
        }

        void DisableHook (string hookName, bool save = true)
        {
            ConfigureHook (hookName, false, save);
        }

        void DisableHooks (params string [] hookNames)
        {
            foreach (string hookName in hookNames) {
                DisableHook (hookName, false);
            }

            SaveSettings ();
            UnsubscribeHooks ();
            SubscribeHooks ();
        }

        void ConfigureHook (string hookName, bool setting, bool save = true)
        {
            if (settings.HookSettings.ContainsKey (hookName)) {
                settings.HookSettings [hookName] = setting;
            } else {
                settings.HookSettings.Add (hookName, setting);
            }

            if (save) {
                SaveSettings ();
                UnsubscribeHooks ();
                SubscribeHooks ();
            }
        }

        protected override void LoadDefaultConfig ()
        {
            settings = new PluginSettings () {
                HookSettings = defaultHookSettings,
                VERSION = Version.ToString ()
            };
            SaveSettings ();
        }

        protected void SaveSettings ()
        {
            Config.WriteObject (settings, true);
        }

        void LoadConfigValues ()
        {
            settings = Config.ReadObject<PluginSettings> ();

            foreach (KeyValuePair<string, bool> kvp in defaultHookSettings) {
                if (!settings.HookSettings.ContainsKey (kvp.Key)) {
                    settings.HookSettings.Add (kvp.Key, kvp.Value);
                }
            }
        }

        #endregion

        #region Console Commands
        [ConsoleCommand ("hookx")]
        void ccHooksExtendedStatus (ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null) {
                if (arg.Args == null) {

                    Puts ("Hook Configuration:");
                    foreach (KeyValuePair<string, bool> kvp in settings.HookSettings) {
                        Puts ("{0} = {1}", kvp.Key, kvp.Value);
                    }
                } else if (arg.Args.Length > 0) {
                    var command = arg.Args [0].ToLower ();
                    UnsubscribeHooks ();
                    switch (command) {
                    case "enable":
                        foreach (KeyValuePair<string, bool> kvp in defaultHookSettings) {
                            settings.HookSettings [kvp.Key] = true;
                        }
                        Puts ("All hooks enabled");
                        break;
                    case "disable":
                        foreach (KeyValuePair<string, bool> kvp in defaultHookSettings) {
                            settings.HookSettings [kvp.Key] = false;
                        }
                        Puts ("All hooks disabled");
                        break;
                    }
                    SubscribeHooks ();
                }
            }
        }
        #endregion

        #region Extended Hooks

        /// <summary>
        /// CAN NETWORK TO
        /// </summary>
        /// <returns>Whether to network to</returns>
        /// <param name="entity">BaseNetworkable.</param>
        /// <param name="target">BasePlayer.</param>
        object CanNetworkTo (BaseNetworkable entity, BasePlayer target)
        {
            string hook;
            if (TryGetHook (entity.GetType (), networkableTypes, out hook)) {
                return Interface.Oxide.CallHook (hook, entity, target);
            }
            return null;
        }

        private List<Type> seatType = new List<Type> {
            typeof(BaseVehicleSeat)
        };
        /// <summary>
        /// CAN MOUNT
        /// </summary>
        /// <returns>Whether allow mounting</returns>
        /// <param name="player">BasePlayer.</param>
        /// <param name="entity">BaseMountable.</param>
        object CanMountEntity (BasePlayer player, BaseMountable entity)
        {
            string hook;

            object result = null;
            if (TryGetHook (entity.GetType (), canMountMountableTypes, out hook, emptyList, seatType)) {
                result = CallBaseHook (hook, player, entity);
            }

            if (result == null && TryGetHook (entity.GetParentEntity ().GetType (), canMountMountableTypes, out hook, seatType)) {
                result = CallBaseHook (hook, player, entity.GetParentEntity ());
            }

            return result;
        }

        /// <summary>
        /// CAN DISMOUNT
        /// </summary>
        /// <returns>Whether allow dismounting</returns>
        /// <param name="player">BasePlayer.</param>
        /// <param name="entity">BaseMountable.</param>
        object CanDismountEntity (BasePlayer player, BaseMountable entity)
        {
            string hook;

            object result = null;
            if (TryGetHook (entity.GetType (), canDismountMountableTypes, out hook, emptyList, seatType)) {
                result = CallBaseHook (hook, player, entity);
            }

            if (result == null && TryGetHook (entity.GetParentEntity ().GetType (), canDismountMountableTypes, out hook, seatType)) {
                result = CallBaseHook (hook, player, entity.GetParentEntity ());
            }

            return result;
        }

        /// <summary>
        /// ON MOUNT
        /// </summary>
        /// <returns>On player mounted</returns>
        /// <param name="entity">BaseMountable.</param>
        /// <param name="player">BasePlayer.</param>
        void OnEntityMounted (BaseMountable entity, BasePlayer player)
        {
            string hook;
            if (TryGetHook (entity.GetType (), mountedMountableTypes, out hook, emptyList, seatType)) {
                CallBaseHook (hook, entity, player);
            }

            if (TryGetHook (entity.GetParentEntity ().GetType (), mountedMountableTypes, out hook, seatType)) {
                CallBaseHook (hook, entity.GetParentEntity (), player);
            }
        }

        /// <summary>
        /// ON DISMOUNT
        /// </summary>
        /// <returns>On player dismounted</returns>
        /// <param name="entity">BaseMountable.</param>
        /// <param name="player">BasePlayer.</param>
        void OnEntityDismounted (BaseMountable entity, BasePlayer player)
        {
            string hook;
            if (TryGetHook (entity.GetType (), dismountedMountableTypes, out hook, emptyList, seatType)) {
                CallBaseHook (hook, entity, player);
            }

            if (TryGetHook (entity.GetParentEntity ().GetType (), dismountedMountableTypes, out hook, seatType)) {
                CallBaseHook (hook, entity.GetParentEntity (), player);
            }
        }


        /// <summary>
        /// ON ITEM DROP
        /// ON ITEM UNWRAP
        /// ON ITEM * ACTION
        /// </summary>
        /// <returns>Returning non-null cancels action.</returns>
        /// <param name="item">Item.</param>
        /// <param name="action">Action.</param>
        object OnItemAction (Item item, string action)
        {
            return CallBaseHook ("OnItem" + action, item);
        }

        /// <summary>
        /// CAN CATEGORY CRAFT
        /// </summary>
        /// <returns>Craft status.</returns>
        /// <param name="itemCrafter">Item crafter.</param>
        /// <param name="bp">Bp.</param>
        /// <param name="amount">Amount.</param>
        object CanCraft (ItemCrafter itemCrafter, ItemBlueprint bp, int amount)
        {
            string category = bp?.targetItem?.category.ToString ();
            if (!string.IsNullOrEmpty (category)) {
                return CallBaseHook ("Can" + category + "Craft", itemCrafter, bp, amount);
            }

            return null;
        }


        /// <summary>
        /// ON CATEGORY CRAFT
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        object OnItemCraft (ItemCraftTask task)
        {
            string category = task?.blueprint?.targetItem?.category.ToString ();
            if (!string.IsNullOrEmpty (category)) {
                return CallBaseHook ("On" + category + "Craft", task);
            }

            return null;
        }

        /// <summary>
        /// ON CATEGORY CRAFT CANCELLED
        /// </summary>
        /// <param name="task"></param>
        void OnItemCraftCancelled (ItemCraftTask task)
        {
            string category = task?.blueprint?.targetItem?.category.ToString ();
            if (!string.IsNullOrEmpty (category)) {
                CallBaseHook ("On" + category + "CraftCancelled", task);
            }
        }

        /// <summary>
        /// ON CATEGORY CRAFT FINISHED
        /// </summary>
        /// <param name="task"></param>
        /// <param name="item"></param>
        void OnItemCraftFinished (ItemCraftTask task, Item item)
        {
            string category = item?.info?.category.ToString ();
            if (!string.IsNullOrEmpty (category)) {
                CallBaseHook ("On" + category + "CraftFinished", task, item);
            }
        }

        /// <summary>
        /// ON CATEGORY RESEARCH
        /// </summary>
        /// <param name="table"></param>
        /// <param name="targetItem"></param>
        /// <param name="player"></param>
        /// <returns></returns>
        void OnItemResearch (ResearchTable table, Item targetItem, BasePlayer player)
        {
            string category = targetItem?.info?.category.ToString ();
            if (!string.IsNullOrEmpty (category)) {
                CallBaseHook ("On" + category + "Research", table, targetItem, player);
            }
        }

        /// <summary>
        /// ON CATEGORY REPAIR
        /// </summary>
        /// <param name="player"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        void OnItemRepair (BasePlayer player, Item item)
        {
            string category = item?.info?.category.ToString ();
            if (!string.IsNullOrEmpty (category)) {
                CallBaseHook ("On" + category + "Repair", player, item);
            }
        }

        /// <summary>
        /// ON CATEGORY RECYCLE
        /// </summary>
        /// <param name="recycler"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        object OnRecycleItem (Recycler recycler, Item item)
        {
            string category = item?.info?.category.ToString ();
            if (!string.IsNullOrEmpty (category)) {
                return CallBaseHook ("On" + category + "Recycle", recycler, item);
            }

            return null;
        }

        /// <summary>
        /// ON CATEGORY PICKUP
        /// </summary>
        /// <param name="item"></param>
        /// <param name="player"></param>
        /// <returns></returns>
        object OnItemPickup (Item item, BasePlayer player)
        {
            string category = item?.info?.category.ToString ();
            if (!string.IsNullOrEmpty (category)) {
                return CallBaseHook ("On" + category + "Pickup", player, item);
            }

            return null;
        }

        /// <summary>
        /// ON CATEGORY DROPPED
        /// </summary>
        /// <param name="item"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        void OnItemDropped (Item item, BaseEntity entity)
        {
            string category = item?.info?.category.ToString ();
            if (!string.IsNullOrEmpty (category)) {
                CallBaseHook ("On" + category + "Dropped", entity, item);
            }
        }

        /// <summary>
        /// ON ACTIVATE
        /// ON DEACTIVATE
        /// ON DUCK
        /// ON STAND
        /// ON BEGIN SPRINT
        /// ON END SPRINT
        /// </summary>
        /// <param name="player"></param>
        void OnPlayerTick (BasePlayer player)
        {
            PlayerProfile profile;
            if (!TryGetPlayer (player, out profile)) {
                return;
            }

            Item item = player.GetActiveItem ();
            if (item != null && item != profile.activeItem) {
                if (Interface.CallHook ("OnItemActivate", player, item) == null) {
                    string category = item?.info?.category.ToString ();
                    if (!string.IsNullOrEmpty (category)) {
                        Interface.CallHook ("On" + category + "Activate", player, item);
                    }
                }
                profile.activeItem = item;
            } else if (item == null && profile.activeItem != null) {
                if (Interface.CallHook ("OnItemDeactivate", player, item) == null) {
                    string category = item?.info?.category.ToString ();
                    if (!string.IsNullOrEmpty (category)) {
                        Interface.CallHook ("On" + category + "Deactivate", player, item);
                    }
                }
                profile.activeItem = null;
            }

            if (player.modelState.aiming) {
                if (!profile.wasAiming) {
                    Interface.CallHook ("OnStartAiming", player);
                }
                profile.wasAiming = true;
            } else {
                if (profile.wasAiming) {
                    Interface.CallHook ("OnStopAiming", player);
                }
                profile.wasAiming = false;
            }

            if (player.IsReceivingSnapshot) {
                if (!profile.wasReceivingSnapshot) {
                    Interface.CallHook ("OnReceivingSnapshot", player);
                }
                profile.wasReceivingSnapshot = true;
            } else {
                if (profile.wasReceivingSnapshot) {
                    Interface.CallHook ("OnReceivedSnapshot", player);
                }
                profile.wasReceivingSnapshot = false;
            }

            if (player.IsSwimming ()) {
                if (!profile.wasSwimming) {
                    Interface.CallHook ("OnStartSwimming", player);
                }
                profile.wasSwimming = true;
            } else {
                if (profile.wasSwimming) {
                    Interface.CallHook ("OnStopSwimming", player);
                }
                profile.wasSwimming = false;
            }

            if (player.IsFlying) {
                if (!profile.wasFlying) {
                    Interface.CallHook ("OnStartFlying", player);
                }
                profile.wasFlying = true;
            } else {
                if (profile.wasFlying) {
                    Interface.CallHook ("OnStopFlying", player);
                }
                profile.wasFlying = false;
            }

            if (player.IsDucked ()) {
                if (!profile.wasDucked) {
                    Interface.CallHook ("OnPlayerDuck", player);
                }
                profile.wasDucked = true;
            } else {
                if (profile.wasDucked) {
                    Interface.CallHook ("OnPlayerStand", player);
                }
                profile.wasDucked = false;
            }

            if (player.IsRunning ()) {
                if (!profile.wasSprinting) {
                    Interface.CallHook ("OnStartSprint", player);
                }
                profile.wasSprinting = true;
            } else {
                if (profile.wasSprinting) {
                    Interface.CallHook ("OnStopSprint", player);
                }
                profile.wasSprinting = false;
            }
        }

        /// <summary>
        /// ON HIT RESOURCE
        /// ON HIT WOOD
        /// ON HIT ROCK
        /// </summary>
        /// <param name="attacker"></param>
        /// <param name="info"></param>
        void OnPlayerAttack (BasePlayer attacker, HitInfo info)
        {
            if (info.Weapon == null) {
                return;
            }

            if (info.HitEntity != null) {
                var resourceDispenser = info.HitEntity.GetComponentInParent<ResourceDispenser> ();
                if (resourceDispenser != null) {
                    Interface.CallHook ("OnHitResource", attacker, info);
                    return;
                }

                if (info.HitEntity.name.Contains ("junkpile")) {
                    Interface.CallHook ("OnHitJunk", attacker, info);
                    return;
                }
            }
            if (WoodMaterial.Contains (info.HitMaterial)) {
                Interface.CallHook ("OnHitWood", attacker, info);
            } else if (RockMaterial.Contains (info.HitMaterial)) {
                Interface.CallHook ("OnHitRock", attacker, info);
            } else if (MetalMaterial.Contains (info.HitMaterial)) {
                Interface.CallHook ("OnHitMetal", attacker, info);
            } else if (SnowMaterial.Contains (info.HitMaterial)) {
                Interface.CallHook ("OnHitSnow", attacker, info);
            } else if (GrassMaterial.Contains (info.HitMaterial)) {
                Interface.CallHook ("OnHitGrass", attacker, info);
            } else if (SandMaterial.Contains (info.HitMaterial)) {
                Interface.CallHook ("OnHitSand", attacker, info);
            }
        }

        /// <summary>
        /// ON START WETNESS
        /// ON STOP WETNESS
        /// ON START POISON
        /// ON STOP POISON
        /// ON START RADIATION
        /// ON STOP RADIATION
        /// ON START RADIATION POISON
        /// ON STOP RADIATION POISON
        /// ON START COMFORT
        /// ON STOP COMFORT
        /// ON START BLEEDING
        /// ON STOP BLEEDING
        /// </summary>
        /// <param name="metabolism"></param>
        /// <param name="source"></param>
        void OnRunPlayerMetabolism (PlayerMetabolism metabolism, BaseCombatEntity source)
        {
            if (source is BasePlayer) {
                BasePlayer player = (BasePlayer)source;
                PlayerProfile profile;
                if (onlinePlayers.TryGetValue (player, out profile)) {
                    if (profile.Metabolism == null) {
                        profile.Metabolism = new ProfileMetabolism (metabolism);
                        return;
                    }

                    Dictionary<string, ProfileMetabolism.MetaAction> changes = profile.Metabolism.DetectChange (metabolism);

                    foreach (KeyValuePair<string, ProfileMetabolism.MetaAction> kvp in changes) {
                        if (kvp.Value == ProfileMetabolism.MetaAction.Start) {
                            Interface.CallHook ("OnStart" + kvp.Key, player);
                        } else {
                            Interface.CallHook ("OnStop" + kvp.Key, player);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// ON CUPBOARD DEPLOYED
        /// ON TURRET DEPLOYED
        /// ON DOOR DEPLOYED
        /// ON SLEEPING BAG DEPLOYED
        /// ON STOCKING DEPLOYED
        /// ON BARRICADE DEPLOYED
        /// ON CONTAINER DEPLOYED
        /// ON SIGN DEPLOYED
        /// ON FURNACE DEPLOYED
        /// ON CAMPFIRE DEPLOYED
        /// ON LIGHT DEPLOYED
        /// </summary>
        /// <param name="deployer"></param>
        /// <param name="entity"></param>
        void OnItemDeployed (Deployer deployer, BaseEntity entity)
        {
            BasePlayer player = deployer.GetOwnerPlayer ();

            var type = entity.GetType ();

            string hook;
            if (TryGetHook (type, deployableTypes, out hook)) {
                CallBaseHook (hook, player, deployer, entity);
            } else if (entity is BaseOven) {
                if (entity.name.Contains ("furnace")) {
                    CallBaseHook ("OnFurnaceDeployed", player, deployer, entity);
                } else if (entity.name.Contains ("campfire")) {
                    CallBaseHook ("OnCampfireDeployed", player, deployer, entity);
                } else if (entity is CeilingLight) {
                    CallBaseHook ("OnLightDeployed", player, deployer, entity);
                }
            }
        }

        /// <summary>
        /// ON DEPLOYABLE REPAIR
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="player"></param>
        void OnStructureRepair (BaseCombatEntity entity, BasePlayer player)
        {
            string hook;
            if (TryGetHook (entity.GetType (), repairableTypes, out hook)) {
                CallBaseHook (hook, player, entity);
            }
        }

        /// <summary>
        /// ON ANIMAL ATTACK
        /// ON HELICOPTER ATTACK
        /// ON STRUCTURE DAMAGE
        /// ON PLAYER DAMAGE
        /// ON TURRET DAMAGE
        /// ON HELICOPTER DAMAGE
        /// ON CUPBOARD DAMAGE
        /// ON CORPSE DAMAGE
        /// ON SLEEPING BAG DAMAGE
        /// ON NPC DAMAGE
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="info"></param>
        void OnEntityTakeDamage (BaseCombatEntity entity, HitInfo info)
        {
            string hook;

            if (info.Initiator != null) {
                if (TryGetHook (info.Initiator.GetType (), attackTypes, out hook)) {
                    Interface.CallHook (hook, entity, info.Initiator, info);
                }
            }

            if (TryGetHook (entity.GetType (), damagableTypes, out hook)) {
                Interface.CallHook (hook, entity, info);
            }
        }

        /// <summary>
        /// ON ENTITY TYPE SPAWNED
        /// </summary>
        /// <param name="entity">Entity.</param>
        void OnEntitySpawned (BaseNetworkable entity)
        {
            var type = entity.GetType ();

            string hook;
            if (TryGetHook (type, spawnableTypes, out hook)) {
                CallBaseHook (hook, entity);
            }
        }


        /// <summary>
        /// ON COOK FURNACE
        /// ON COOK FIRE
        /// ON FUEL LIGHT
        /// </summary>
        /// <param name="oven"></param>
        /// <param name="fuel"></param>
        /// <param name="burnable"></param>
        void OnConsumeFuel (BaseOven oven, Item fuel, ItemModBurnable burnable)
        {
            if (oven.name.Contains ("furnace")) {
                CallBaseHook ("OnCookFurnace", oven, fuel, burnable);
            } else if (oven.name.Contains ("campfire")) {
                CallBaseHook ("OnCookFire", oven, fuel, burnable);
            } else if (oven.name.Contains ("light") || oven.name.Contains ("lantern")) {
                CallBaseHook ("OnFuelLight", oven, fuel, burnable);
            }
        }

        /// <summary>
        /// ON WEAPON SMELTED
        /// </summary>
        /// <param name="oven"></param>
        Item OnFindBurnable (BaseOven oven)
        {
            foreach (Item item in oven.inventory.itemList) {
                if (item.info.GetComponentInChildren<ItemModBurnable> () == null) {
                    var obj = CallBaseHook ("On" + item.info.category + "Smelt", oven, item);
                    if (obj is Item) {
                        return (Item)obj;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// ON STRUCTURE DEATH
        /// ON PLAYER DEATH
        /// ON TURRET DEATH
        /// ON HELICOPTER DEATH
        /// ON CUPBOARD DEATH
        /// ON SLEEPING BAG DEATH
        /// ON NPC DEATH
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="info"></param>
        void OnEntityDeath (BaseCombatEntity entity, HitInfo info)
        {
            string hook;

            if (TryGetHook (entity.GetType (), killableTypes, out hook)) {
                CallBaseHook (hook, entity, info);
            }
        }

        /// <summary>
        /// ON EQUIP
        /// </summary>
        /// <param name="container"></param>
        /// <param name="item"></param>
        void OnItemAddedToContainer (ItemContainer container, Item item)
        {
            if (!(container.playerOwner is BasePlayer)) {
                return;
            }

            if (container.playerOwner.inventory.containerWear == container) {
                CallBaseHook ("OnEquip", container.playerOwner, item);
            } else if (container.playerOwner.inventory.containerBelt == container && item.CanBeHeld ()) {
                CallBaseHook ("OnEquip", container.playerOwner, item);
            }
        }

        /// <summary>
        /// ON UNEQUIP
        /// </summary>
        /// <param name="container"></param>
        /// <param name="item"></param>
        void OnItemRemovedFromContainer (ItemContainer container, Item item)
        {
            if (!(container.playerOwner is BasePlayer)) {
                return;
            }

            if (container.playerOwner.inventory.containerWear == container) {
                CallBaseHook ("OnUnequip", container.playerOwner, item);
            } else if (container.playerOwner.inventory.containerBelt == container && item.CanBeHeld ()) {
                CallBaseHook ("OnUnequip", container.playerOwner, item);
            }
        }

        /// <summary>
        /// ON SPOT PLAYER
        /// ON SPOT NPC
        /// ON SPOT TURRET
        /// ON SPOT HELICOPTER
        /// ON SPOT RESOURCE
        /// ON USE PLAYER
        /// ON USE TERRAIN
        /// ON USE NPC
        /// ON USE BUILDING
        /// ON USE CUPBOARD
        /// ON USE SLEEPINGBAG
        /// ON USE PLANT
        /// ON USE RESOURCE
        /// </summary>
        /// <param name="player"></param>
        /// <param name="input"></param>
        void OnPlayerInput (BasePlayer player, InputState input)
        {
            if (input.IsDown (BUTTON.FIRE_SECONDARY) && !inputCooldown.Contains (player)) {
                TriggerSpotting (player, input);
            }

            if (input.WasJustPressed (BUTTON.USE)) {
                TriggerUse (player, input);
            }

            if (!player.IsFlying && !player.IsSwimming () && !player.isMounted && input.WasJustPressed (BUTTON.JUMP)) {
                CallBaseHook ("OnPlayerJump", player);
            }
        }

        #endregion

        #region Helpers

        void TriggerUse (BasePlayer player, InputState input)
        {
            Quaternion currentRot;
            TryGetPlayerView (player, out currentRot);
            var hitpoints = Physics.RaycastAll (player.eyes.HeadRay (), 5f, useLayer, QueryTriggerInteraction.Collide);
            GamePhysics.Sort (hitpoints);

            object targetEntity;
            Func<object, object> func;

            for (var i = 0; i < hitpoints.Length; i++) {
                var hit = hitpoints [i];
                var target = hit.collider;
                if (target.name == "Terrain") {
                    CallBaseHook ("OnUseTerrain", player, target);
                    return;
                }

                if (target.name == "MeshColliderBatch") {
                    target = RaycastHitEx.GetCollider (hit);
                }

                foreach (KeyValuePair<Type, string> kvp in usableTypes) {
                    if ((targetEntity = target.GetComponentInParent (kvp.Key)) != null) {

                        if (typeCasting.TryGetValue (kvp.Key, out func)) {
                            CallBaseHook (kvp.Value, player, func.Invoke (targetEntity));
                        } else {
                            CallBaseHook (kvp.Value, player, targetEntity);
                        }

                        return;
                    }
                }
            }
        }

        bool IsSpotting (BasePlayer player)
        {
            return spotCooldown.ContainsKey (player);
        }

        void TriggerSpotting (BasePlayer player, InputState input = null)
        {
            Item activeItem = player.GetActiveItem ();
            if (activeItem == null) {
                return;
            }

            if (activeItem.info.category != ItemCategory.Weapon) {
                return;
            }

            inputCooldown.Add (player);

            timer.Once (1, delegate () {
                inputCooldown.Remove (player);
            });

            Ray ray = (input == null) ? player.eyes.HeadRay () : new Ray (player.eyes.position, Quaternion.Euler (input.current.aimAngles) * Vector3.forward);

            var hitpoints = Physics.RaycastAll (ray, 100f, spottingMask, QueryTriggerInteraction.Collide);
            GamePhysics.Sort (hitpoints);

            object targetEntity;
            Func<object, object> func;

            for (var i = 0; i < hitpoints.Length; i++) {
                var hit = hitpoints [i];
                var target = hit.collider;
                if (target.name == "Terrain") {
                    return;
                }

                if (target.name == "MeshColliderBatch") {
                    target = RaycastHitEx.GetCollider (hit);
                }

                foreach (KeyValuePair<Type, string> kvp in spottableTypes) {
                    if ((targetEntity = target.GetComponentInParent (kvp.Key)) is BaseEntity) {
                        SpotTarget2 (player, (BaseEntity)targetEntity, kvp.Key, kvp.Value);
                        return;
                    }
                }
            }
        }

        void SpotTarget2 (BasePlayer player, BaseEntity hitEntity, Type hitEntityType, string hook)
        {
            Func<object, object> func;

            MonoBehaviour target = hitEntity as MonoBehaviour;
            ResourceDispenser dispenser = hitEntity.GetComponentInParent<ResourceDispenser> ();

            var distanceTo = player.Distance (hitEntity);

            List<MonoBehaviour> playerSpotCooldown;
            if (!spotCooldown.TryGetValue (player, out playerSpotCooldown)) {
                spotCooldown.Add (player, playerSpotCooldown = new List<MonoBehaviour> ());
            }

            if (!playerSpotCooldown.Contains (target)) {
                playerSpotCooldown.Add (target);
                if (typeCasting.TryGetValue (hitEntityType, out func)) {
                    CallBaseHook (hook, player, func.Invoke (hitEntity));
                } else {
                    CallBaseHook (hook, player, hitEntity);
                }

                timer.Once (2, delegate () {
                    if (playerSpotCooldown.Contains (target)) {
                        playerSpotCooldown.Remove (target);
                    }
                });
            }
        }

        void SpotTarget (BasePlayer player, BaseEntity hitEntity, Type hitEntityType, string hook)
        {
            MonoBehaviour target = hitEntity as MonoBehaviour;
            ResourceDispenser dispenser = hitEntity.GetComponentInParent<ResourceDispenser> ();

            var distanceTo = player.Distance (hitEntity);

            List<MonoBehaviour> playerSpotCooldown;
            if (!spotCooldown.TryGetValue (player, out playerSpotCooldown)) {
                spotCooldown.Add (player, playerSpotCooldown = new List<MonoBehaviour> ());
            }

            if (!playerSpotCooldown.Contains (target)) {
                playerSpotCooldown.Add (target);

                CallBaseHook (hook, player, hitEntity, distanceTo);

                timer.Once (2, delegate () {
                    if (playerSpotCooldown.Contains (target)) {
                        playerSpotCooldown.Remove (target);
                    }
                });
            }
        }

        bool TryGetPlayer (BasePlayer player, out PlayerProfile profile)
        {
            return onlinePlayers.TryGetValue (player, out profile);
        }

        bool TryGetPlayerView (BasePlayer player, out Quaternion viewAngle)
        {
            viewAngle = new Quaternion (0f, 0f, 0f, 0f);
            var input = player.serverInput;
            if (input.current == null) return false;
            viewAngle = Quaternion.Euler (input.current.aimAngles);
            return true;
        }

        List<Type> emptyList = new List<Type> ();

        bool TryGetHook (Type type, Dictionary<Type, string> types, out string hook, List<Type> exclude = null, List<Type> only = null)
        {
            hook = string.Empty;
            if (type.Name == "EnvSync") return false;

            if (exclude == null) {
                exclude = emptyList;
            }

            if (only == null) {
                only = emptyList;
            }

            if (!exclude.Contains (type) && types.TryGetValue (type, out hook) && ((only.Count > 0 && only.Contains (type)) || only.Count == 0)) {
                if (string.IsNullOrEmpty (hook)) {
                    return false;
                }

                //PrintWarning ("+ DIRECT: " + type.Name + " = " + hook);
                return true;
            }

            var found = false;

            //PrintWarning ("+ INDIRECT: " + type.Name + " - " + types.Count);
            foreach (KeyValuePair<Type, string> kvp in types) {
                if (exclude.Contains (kvp.Key)) {
                    //PrintWarning ("EXCLUDING 1: " + kvp.Key.Name);
                    continue;
                }

                if (only.Count > 0 && !only.Contains (kvp.Key)) {
                    //PrintWarning ("EXCLUDING 2: " + kvp.Key.Name);
                    continue;
                }
                //PrintWarning ("CHECKING: " + kvp.Key.Name);
                if (type.IsSubclassOf (kvp.Key)) {

                    hook = kvp.Value;
                    //PrintWarning (" - " + kvp.Key.Name + ": " + hook);
                    if (!types.ContainsKey (type)) {
                        types.Add (type, kvp.Value);
                    }
                    found = true;
                    break;
                } else if (kvp.Key.IsSubclassOf (type)) {
                    hook = kvp.Value;
                    //PrintWarning (" - " + kvp.Key.Name + ": " + hook);
                    if (!types.ContainsKey (type)) {
                        types.Add (type, kvp.Value);
                    }
                    found = true;
                    break;
                } else {
                    //PrintWarning (" - " + kvp.Key.Name + ": NO");
                }
            }

            if (!found && !types.ContainsKey (type)) {
                types.Add (type, string.Empty);
            }

            return found;
        }

        public Func<object, object> TypeConverter<T> ()
        {
            return delegate (object obj) {
                return (T)obj;
            };
        }

        object CallBaseHook (string hookname, params object [] args)
        {
#if DEBUG
            PrintWarning (hookname);
#endif
            return Interface.CallHook (hookname, args);
        }

        #endregion
    }
}
