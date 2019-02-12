namespace Oxide.Plugins
{
    [Info("VIPNoDecay", "ColonBlow", "1.0.0")]
    [Description("Disables Decay Damage for player or oxide group with VIP permissions")]

    class VIPNoDecay : RustPlugin
    {
        void Init() 
	    { 
		    permission.RegisterPermission("vipnodecay.vip", this); 
	    }

	    bool HasPermission(ulong playerID, string perm) => permission.UserHasPermission(playerID.ToString(), perm);

	    object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (hitInfo.damageTypes.GetMajorityDamageType().ToString().Contains("Decay"))
            {
                var ownerid = entity.OwnerID;
                if (HasPermission(ownerid, "vipnodecay.vip"))
                {
                    DecayEntity decayEntity = entity.GetComponent<DecayEntity>();
                    //First resets the decay timer for entity who's owner is a vip
                    decayEntity.DecayTouch();
                    //Then blocks this currrent damage to entity
                    return false;
                }
            }
	        return null;
        }
    }
}
