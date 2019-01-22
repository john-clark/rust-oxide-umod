using System;

using UnityEngine;

using Oxide.Core;
using Oxide.Core.Plugins;

using Rust;

namespace Oxide.Plugins
{
    [Info("Safety Barrel", "Panduck", "0.0.7")]
    [Description("Makes you untargetable by most entities while wearing the barrel costume.")]
    public class SafetyBarrel : RustPlugin
    {
        private SafetyBarrelSettings settings;

        private const string SAFETYBARREL_PROTECT = "safetybarrel.protect";

        void Init()
        {
            settings = Config.ReadObject<SafetyBarrelSettings>();

            permission.RegisterPermission(SAFETYBARREL_PROTECT, this);
        }

        private SafetyBarrelSettings GetDefaultConfig()
        {
            return new SafetyBarrelSettings
            {
                GodModeWhileWearingBarrel = false,
                ProximityDetection = true,
                ProximityAttackDistance = 5f,
                ProximityStareDistance = 10f
            };
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private bool IsWearingBarrel(BaseEntity entity)
        {
            BasePlayer player = entity as BasePlayer;

            if (player != null)
            {
                if (permission.UserHasPermission(player.UserIDString, SAFETYBARREL_PROTECT))
                {
                    PlayerInventory playerInv = player.inventory;
                    ItemContainer playerInv_Clothing = playerInv.containerWear;

                    foreach (Item item in playerInv_Clothing.itemList)
                    {
                        if (item != null)
                        {
                            ItemDefinition itemDef = item.info;

                            if (itemDef.shortname == "barrelcostume")
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private object ScanProximity(BaseEntity player, BaseNpc npc)
        {
            float distanceFromNpc = player.Distance(npc);

            if (distanceFromNpc <= settings.ProximityStareDistance)
            {
                if (distanceFromNpc <= settings.ProximityAttackDistance)
                {
                    return null;
                }
                else
                {
                    npc.StopMoving();
                    npc.transform.LookAt(player.transform);
                }
            }

            return false;
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (IsWearingBarrel(entity))
                if (settings.GodModeWhileWearingBarrel)
                    return true;

            return null;
        }

        private object OnNpcPlayerTarget(BaseNpc npc, BaseEntity entity)
        {
            if (IsWearingBarrel(entity))
            {
                if (settings.ProximityDetection)
                {
                    return ScanProximity(entity, npc);
                }
                else
                {
                    return false;
                }
            }

            return null;
        }

        private object OnNpcTarget(BaseNpc npc, BaseEntity entity)
        {
            if (IsWearingBarrel(entity))
            {
                if (settings.ProximityDetection)
                {
                    return ScanProximity(entity, npc);
                }
                else
                {
                    return false;
                }
            }

            return null;
        }

        private object CanBeTargeted(BaseCombatEntity entity)
        {
            if (IsWearingBarrel(entity))
                return false;

            return null;
        }

        private object CanBradleyApcTarget(BradleyAPC apc, BaseEntity entity)
        {
            if (IsWearingBarrel(entity))
                return false;

            return null;
        }

        private object CanHelicopterTarget(PatrolHelicopterAI heli, BaseEntity entity)
        {
            if (IsWearingBarrel(entity))
                return false;

            return null;
        }

        public class SafetyBarrelSettings
        {
            public bool GodModeWhileWearingBarrel;

            public bool ProximityDetection;
            public float ProximityAttackDistance;
            public float ProximityStareDistance;
        }
    }
}