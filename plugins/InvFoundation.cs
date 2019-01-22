using System;
using Rust;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("InvFoundation", "sami37", "1.2.6", ResourceId = 2096)]
    [Description("Invulnerable foundation")]
    public class InvFoundation : RustPlugin
    {
        #region Building Owners Support
        [PluginReference("BuildingOwners")]
        Plugin BuildingOwners;
        #endregion

        #region Entity Owners Support
        [PluginReference("EntityOwner")]
        Plugin EntityOwner;
        #endregion

        private Dictionary<string, object> damageList => GetConfig("DamageList", defaultDamageScale());
        private Dictionary<string, object> damageGradeScaling => GetConfig("TierScalingDamage", defaultDamageTierScaling());
        private bool UseEntityOwner => GetConfig("UseEntityOwner", false);
        private bool UseBuildOwners => GetConfig("UseBuildingOwner", false);
        private bool UseDamageScaling => GetConfig("UseDamageScaling", false);
        private bool ExcludeCave => GetConfig("Exclude cave", false);
        private bool allowdecay => GetConfig("Allow Decay", true);
        private static readonly int colisionentity = LayerMask.GetMask("Construction");
        private readonly int cupboardMask = LayerMask.GetMask("Trigger");
        private readonly int groundLayer = LayerMask.GetMask("Terrain", "World");

        T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

		static Dictionary<string,object> defaultDamageScale()
		{
		    var dp = new Dictionary<string, object>
		    {
		        {"Bullet", 0.0},
		        {"Blunt", 0.0},
		        {"Stab", 0.0},
		        {"Slash", 0.0},
		        {"Explosion", 0.0}
		    };

		    return dp;
		}

        static Dictionary<string, object> defaultDamageTierScaling()
        {
            var dp = new Dictionary<string, object>();
            dp.Add("Twigs", 0.0);
            dp.Add("Wood", 0.0);
            dp.Add("Stone", 0.0);
            dp.Add("Metal", 0.0);
            dp.Add("TopTier", 0.0);
            return dp;
        }


        void Loaded()
        {
            Config["UseBuildingOwner"] = UseBuildOwners;
            Config["UseEntityOwner"] = UseEntityOwner;
            Config["UseDamageScaling"] = UseDamageScaling;
            Config["Exclude cave"] = ExcludeCave;
            Config["DamageList"] = damageList;
            Config["TierScalingDamage"] = damageGradeScaling;
            Config["Allow Decay"] = allowdecay;
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating new config file...");
            SaveConfig();
        }

        private void OnServerInitialized()
        {
            Config["UseBuildingOwner"] = UseBuildOwners;
            Config["UseEntityOwner"] = UseEntityOwner;
            Config["UseDamageScaling"] = UseDamageScaling;
            Config["Exclude cave"] = ExcludeCave;
            Config["DamageList"] = damageList;
            Config["TierScalingDamage"] = damageGradeScaling;
            Config["Allow Decay"] = allowdecay;
            SaveConfig();
            var messages = new Dictionary<string, string>
            {
				{"NoPerm", "You don't have permission to do this."}
            };
            lang.RegisterMessages(messages, this);
        }
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity == null || hitInfo == null)
                return;

            var buildingBlock = entity as BuildingBlock;
            if (buildingBlock != null)
            {
                if(!allowdecay)
                    if (hitInfo.damageTypes.Has(DamageType.Decay))
                    {
                        hitInfo.damageTypes = new DamageTypeList();
                        hitInfo.DoHitEffects = false;
                        hitInfo.HitMaterial = 0;
                        return;
                    }
                else
                    if(hitInfo.damageTypes.Has(DamageType.Decay)) return;

                BuildingBlock block = buildingBlock;
                BasePlayer attacker = hitInfo.Initiator?.ToPlayer();
                if (attacker == null) return;

                object modifier;
                if (block.LookupPrefab().name.ToLower().Contains("foundation") && !CupboardPrivlidge(attacker, block.transform.position))
                {
                    if (IsOwner(attacker, block))
                        return;
                    RaycastHit hit;
                    if (ExcludeCave && Physics.SphereCast(block.transform.position, .1f, Vector3.down, out hit, 250, groundLayer) && hit.collider.name.Contains("cave_")) return;
                    if (!UseDamageScaling)
                    {
                        hitInfo.damageTypes = new DamageTypeList();
                        hitInfo.DoHitEffects = false;
                        hitInfo.HitMaterial = 0;
                        SendReply(attacker, lang.GetMessage("NoPerm", this, attacker.UserIDString));
                        return;
                    }
                    DamageType type = hitInfo.damageTypes.GetMajorityDamageType();
                    if (damageList.TryGetValue(type.ToString(), out modifier))
                    {
                        float mod = Convert.ToSingle(modifier);
                        if (mod > 0.0f)
                        {
                            hitInfo.damageTypes.Scale(type, mod);
                            damageGradeScaling.TryGetValue(block.grade.ToString(), out modifier);
                            mod = Convert.ToSingle(modifier);
                            if (Math.Abs(mod) > 0)
                            {
                                hitInfo.damageTypes.Scale(type, mod);
                                return;
                            }
                            hitInfo.damageTypes = new DamageTypeList();
                            hitInfo.DoHitEffects = false;
                            hitInfo.HitMaterial = 0;
                            SendReply(attacker, lang.GetMessage("NoPerm", this, attacker.UserIDString));
                        }
                        else
                        {
                            hitInfo.damageTypes = new DamageTypeList();
                            hitInfo.DoHitEffects = false;
                            hitInfo.HitMaterial = 0;
                            SendReply(attacker, lang.GetMessage("NoPerm", this, attacker.UserIDString));
                        }
                    }
                }
                else if(block.LookupPrefab().name.ToLower().Contains("foundation") && CupboardPrivlidge(attacker, block.transform.position))
                {
                    if (IsOwner(attacker, block))
                        return;
                    if (!UseDamageScaling)
                    {
                        hitInfo.damageTypes = new DamageTypeList();
                        hitInfo.DoHitEffects = false;
                        hitInfo.HitMaterial = 0;
                        SendReply(attacker, lang.GetMessage("NoPerm", this, attacker.UserIDString));
                        return;
                    }
                    DamageType type = hitInfo.damageTypes.GetMajorityDamageType();
                    if (damageList.TryGetValue(type.ToString(), out modifier))
                    {
                        var mod = Convert.ToSingle(modifier);
                        if (Math.Abs(mod) > 0)
                        {
                            hitInfo.damageTypes.Scale(type, mod);
                            damageGradeScaling.TryGetValue(block.grade.ToString(), out modifier);
                            mod = Convert.ToSingle(modifier);
                            if (Math.Abs(mod) > 0)
                            {
                                hitInfo.damageTypes.Scale(type, mod);
                                return;
                            }
                            hitInfo.damageTypes = new DamageTypeList();
                            hitInfo.DoHitEffects = false;
                            hitInfo.HitMaterial = 0;
                            SendReply(attacker, lang.GetMessage("NoPerm", this, attacker.UserIDString));
                        }
                        else
                        {
                            hitInfo.damageTypes = new DamageTypeList();
                            hitInfo.DoHitEffects = false;
                            hitInfo.HitMaterial = 0;
                            SendReply(attacker, lang.GetMessage("NoPerm", this, attacker.UserIDString));
                        }
                    }
                }
            }
        }

        bool IsOwner(BasePlayer player, BaseEntity targetEntity)
        {
            if (targetEntity == null) return false;
            BuildingBlock block = targetEntity.GetComponent<BuildingBlock>();
            if (block == null)
            {
                RaycastHit supportHit;
                if (Physics.Raycast(targetEntity.transform.position + new Vector3(0f, 0.1f, 0f), new Vector3(0f, -1f, 0f), out supportHit, 3f, colisionentity))
                {
                    BaseEntity supportEnt = supportHit.GetEntity();
                    if (supportEnt != null)
                    {
                        block = supportEnt.GetComponent<BuildingBlock>();
                    }
                }
            }
            if (block != null)
            {
				if (UseBuildOwners)
				{
					if (BuildingOwners != null && BuildingOwners.IsLoaded)
					{
                        var returnhook = Interface.GetMod().CallHook("FindBlockData", new object[] {block});
                        if (returnhook is string)
                        {
                            string ownerid = (string) returnhook;
                            if (player.UserIDString == ownerid) return true;
                        }
                    }
                }
				if (UseEntityOwner)
				{
					if (EntityOwner != null && EntityOwner.IsLoaded)
					{
                        var returnhook = Interface.GetMod().CallHook("FindEntityData", new object[] {targetEntity});
                        if (returnhook is string)
                        {
                            string ownerid = (string) returnhook;
                            if (player.UserIDString == ownerid) return true;
                        }
                    }
                }
            }
            return false;
        }

        private bool CupboardPrivlidge(BasePlayer player, Vector3 position)
        {
            var hits = Physics.OverlapSphere(position, 2f, cupboardMask);
            foreach (var collider in hits)
            {
                var buildingPrivlidge = collider.GetComponentInParent<BuildingPrivlidge>();
                if (buildingPrivlidge == null) continue;

                List<string> ids = (from id in buildingPrivlidge.authorizedPlayers select id.userid.ToString()).ToList();
                foreach (string priv in ids)
                {
                    if (priv == player.UserIDString)
                    {
                        return true;
                    }
                }        
            }
            return false;
        }

    }
}