//#define DEBUG
//#define DEBUGND
using System;
using Rust;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

// Note that DEBUGND is particularly noisy - you will see all calls on behalf of RHIB, Rowboat, and HotAirBalloon.
// DEBUG is slightly less noisy, but will consume a lot of disk space if left on for more than a few
// upkeep cycles.  You must reload the plugin to enable or disable these defines.
namespace Oxide.Plugins
{
    [Info("CupboardNoDecay", "RFC1920", "1.1.0", ResourceId = 2341)]
    [Description("Use cupboard to protect from decay with no material requirements.")]
    public class CupboardNoDecay : RustPlugin
    {
        private ConfigData configData;

        void OnServerInitialized()
        {
            LoadVariables();
        }

        #region config
        private class ConfigData
        {
            public bool CheckAuth  { get; set; }
            public bool DecayTwig  { get; set; }
            public bool DecayWood  { get; set; }
            public bool DecayStone { get; set; }
            public bool DecayMetal { get; set; }
            public bool DecayArmor { get; set; }
            public float TwigRate  { get; set; }
            public float WoodRate  { get; set; }
            public float StoneRate { get; set; }
            public float MetalRate { get; set; }
            public float ArmorRate { get; set; }
            public float EntityRadius { get; set; }
            public string[] NeverDecay { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            string[] never = { "RHIB", "Rowboat", "HotAirBalloon" };

            SaveConfig(new ConfigData
            {
                CheckAuth  = false,
                DecayTwig  = true,
                DecayWood  = false,
                DecayStone = false,
                DecayMetal = false,
                DecayArmor = false,
                TwigRate   = 1.0f,
                WoodRate   = 0.0f,
                StoneRate  = 0.0f,
                MetalRate  = 0.0f,
                ArmorRate  = 0.0f,
                EntityRadius = 30f,
                NeverDecay = never
            });
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Main
        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            string entity_name = null;
            if(!hitInfo.damageTypes.Has(DamageType.Decay))
            {
                return null;
            }

            try
            {
                entity_name = entity.LookupPrefab().name;
            }
            catch
            {
                return null;
            }
#if DEBUG
            Puts($"OnEntityTakeDamage: START CHECKING {entity_name}.");
#endif
            string pos = entity.transform.position.ToString();
            ulong hitEntityOwnerID = entity.OwnerID != 0 ? entity.OwnerID: hitInfo.HitEntity.OwnerID;
            float before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);
            float multiplier = 1.0f;

            // First, we check for protected entities (NeverDecay)
            if(configData.NeverDecay.Contains(entity_name))
            {
                multiplier = 0.0f;
#if DEBUGND
                Puts($"OnEntityTakeDamage: START Skipping cupboard check for {entity_name} {pos} - will NOT decay!");
                Puts("OnEntityTakeDamage: END Set damage for " + entity_name + " to " + multiplier.ToString() + ", was " + before.ToString() + ".");
#endif
                // Apply our damage rules and return
                hitInfo.damageTypes.Scale(Rust.DamageType.Decay, multiplier);
                return null;
            }
#if DEBUG
            Puts($"OnEntityTakeDamage: START Checking {entity_name} at {pos}");
#endif
            // Second, we check for attached (BLOCK) or nearby (ENTITY) cupboard
            BuildingBlock block = entity as BuildingBlock;

            string isblock = "";
            string buildGrade = "";
            bool hascup = false;
            if(block != null)
            {
                isblock = " (building block)";
                hascup = CheckCupboardBlock(block, hitInfo, entity_name);

                if(hascup)
                {
                    multiplier = 0.0f;
                    switch(block.grade)
                    {
                        case BuildingGrade.Enum.Twigs:
                            if(configData.DecayTwig == true)
                            {
                                multiplier = configData.TwigRate;
                            }
                            break;
                        case BuildingGrade.Enum.Wood:
                            if(configData.DecayWood == true)
                            {
                                multiplier = configData.WoodRate;
                            }
                            break;
                        case BuildingGrade.Enum.Stone:
                            if(configData.DecayStone == true)
                            {
                                multiplier = configData.StoneRate;
                            }
                            break;
                        case BuildingGrade.Enum.Metal:
                            if(configData.DecayMetal == true)
                            {
                                multiplier = configData.MetalRate;
                            }
                            break;
                        case BuildingGrade.Enum.TopTier:
                            if(configData.DecayArmor == true)
                            {
                                multiplier = configData.ArmorRate;
                            }
                            break;
                    }
#if DEBUG
                    switch(block.grade)
                    {
                        case BuildingGrade.Enum.Twigs:
                            buildGrade = "(TWIG)";
                            break;
                        case BuildingGrade.Enum.Wood:
                            buildGrade = "(Wood)";
                            break;
                        case BuildingGrade.Enum.Stone:
                            buildGrade = "(Stone)";
                            break;
                        case BuildingGrade.Enum.Metal:
                            buildGrade = "(Metal)";
                            break;
                        case BuildingGrade.Enum.TopTier:
                            buildGrade = "(Armor)";
                            break;
                    }

                    Puts($"OnEntityTakeDamage:    Block - Found cupboard attached to {entity_name}{buildGrade}");
#endif
                }
                else
                {
#if DEBUG
                    Puts($"OnEntityTakeDamage:    Block - MISSING cupboard attached to {entity_name}{buildGrade}!");
                    Puts("OnEntityTakeDamage: END Set damage for " + entity_name + buildGrade + " to standard rate of " + before.ToString() + ".");
#endif
                    return null; // Standard damage rates apply
                }
            }
            else if(CheckCupboardEntity(entity, hitInfo, entity_name))
            {
                // Unprotected Entity with cupboard
                multiplier = 0.0f;
#if DEBUG
                Puts($"OnEntityTakeDamage:  Entity - Found cupboard near {entity_name}");
#endif
            }
            else
            {
                // Unprotected Entity with NO Cupboard
#if DEBUG
                Puts($"OnEntityTakeDamage:  Entity - MISSING cupboard near {entity_name}");
                Puts("OnEntityTakeDamage: END Set damage for " + entity_name + " to standard rate of " + before.ToString() + ".");
#endif
                return null; // Standard damage rates apply
            }

            // Apply our damage rules and return
            hitInfo.damageTypes.Scale(Rust.DamageType.Decay, multiplier);
#if DEBUG
            Puts("OnEntityTakeDamage: END Set damage for " + entity_name + isblock + " to " + multiplier.ToString() + ", was " + before.ToString() + ".");
#endif
            return null;
        }

        // Check that an entity is in range of a cupboard
        private bool CheckCupboardEntity(BaseEntity entity, HitInfo hitInfo, string name)
        {
            int targetLayer = LayerMask.GetMask("Construction", "Construction Trigger", "Trigger", "Deployed");
            Collider[] hit = Physics.OverlapSphere(entity.transform.position, configData.EntityRadius, targetLayer);
#if DEBUG
            string hits = hit.Length.ToString();
            string range = configData.EntityRadius.ToString();
            Puts($"CheckCupboardEntity:   Checking for cupboard within {range}m of {name}.  Found {hits} layer hits.");
#endif
            // loop through hit layers and check for 'Building Privlidge'
            foreach(var ent in hit)
            {
                BuildingPrivlidge privs = ent.GetComponentInParent<BuildingPrivlidge>();
                if(privs != null)
                {
                    // cupboard overlap.  Entity safe from decay
#if DEBUG
                    Puts($"CheckCupboardEntity:     Found entity layer in range of cupboard!");
#endif
                    if(configData.CheckAuth == true)
                    {
#if DEBUG
                        Puts($"CheckCupboardEntity:     Checking that owner is authed to cupboard!");
#endif
                        ulong hitEntityOwnerID = entity.OwnerID != 0 ? entity.OwnerID : hitInfo.HitEntity.OwnerID;
                        return CupboardAuthCheck(privs, hitEntityOwnerID);
                    }
                    else
                    {
                        return true;
                    }
                }
            }

            if(hit.Length > 0)
            {
#if DEBUG
                Puts($"CheckCupboardEntity:     Unable to find entity layer in range of cupboard.");
#endif
                return false;
            }
            else
            {
#if DEBUG
                Puts($"CheckCupboardEntity:     Unable to check for cupboard.");
#endif
            }
            return true;
        }

        // Check that a building block is owned by/attached to a cupboard
        private bool CheckCupboardBlock(BuildingBlock block, HitInfo hitInfo, string ename = "unknown")
        {
            BuildingManager.Building building = block.GetBuilding();
#if DEBUG
            Puts($"CheckCupboardBlock:   Checking for cupboard connected to {ename}.");
#endif
            if(building != null)
            {
                // cupboard overlap.  Block safe from decay.
                if(building.buildingPrivileges == null)
                {
#if DEBUG
                    Puts($"CheckCupboardBlock:     Block NOT owned by cupboard!");
#endif
                    return false;
                }

                if(configData.CheckAuth == true)
                {
#if DEBUG
                    Puts($"CheckCupboardBlock:     Checking that owner is authed to cupboard!");
#endif
                    ulong hitEntityOwnerID = block.OwnerID != 0 ? block.OwnerID : hitInfo.HitEntity.OwnerID;
                    foreach(var privs in building.buildingPrivileges)
                    {
                        if(CupboardAuthCheck(privs, hitEntityOwnerID) == true)
                        {
                            return true;
                        }
                    }
                }
                else
                {
#if DEBUG
                    Puts($"CheckCupboardBlock:     Block owned by cupboard!");
#endif
                    return true;
                }
            }
            else
            {
#if DEBUG
                Puts($"CheckCupboardBlock:     Unable to find cupboard.");
#endif
            }
            return false;
        }

        private bool CupboardAuthCheck(BuildingPrivlidge priv, ulong hitEntityOwnerID)
        {
            string hitId = null;
            string entowner = null;
            foreach(var auth in priv.authorizedPlayers.Select(x => x.userid).ToArray())
            {
#if DEBUG
                hitId = auth.ToString();
                entowner =  hitEntityOwnerID.ToString();
                Puts($"CupboardAuthCheck:       Comparing {hitId} to {entowner}");
#endif
                if(auth == hitEntityOwnerID)
                {
#if DEBUG
                    Puts("CupboardAuthCheck:       Entity/block protected by authed cupboard!");
#endif
                    return true;
                }
            }
#if DEBUG
            Puts("CupboardAuthCheck:       Entity/block NOT protected by authed cupboard!");
#endif
            return false;
        }
        #endregion
    }
}
