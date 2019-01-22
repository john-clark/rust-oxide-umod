using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Punish Friendly Fire", "BuzZ", "1.1.0")]
    [Description("Punish player by X% of the damage done to friends.")]

/*======================================================================================================================= 
*
*   
*   17th november 2018
*
*	THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*
*********************************************
*   Original author :   Vliek on versions <1.1.0
*   Maintainer(s)   :   BuzZ since 20181116 from v1.1.0
*********************************************   
*=======================================================================================================================*/


    class PunishFF : RustPlugin
    {
        [PluginReference]
        private Plugin Friends;

        private bool Changed;
        private float attackerAmount;
        private bool adminPunish;
        private string adminPerm;
        bool debug = false;

        protected override void LoadDefaultConfig()
        {
            if (debug)Puts("Creating a new config file.");
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {
            attackerAmount = Convert.ToSingle(GetConfig("Settings", "% of punish damage given (Default 50%)", 50));
            adminPunish = Convert.ToBoolean(GetConfig("Settings", "Only punish damage on admins", false));
            adminPerm = Convert.ToString(GetConfig("Settings", "Admin permission", "PunishFF.admin"));  //verif qu on peu mettre nimp ?

            if (!Changed) return;
            SaveConfig();
            Changed = false;
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

        private void Init()
        {
            LoadVariables();
            permission.RegisterPermission(adminPerm, this);
        }

        private object OnAttackInternal(BasePlayer attacker, BasePlayer victim, HitInfo hit)
        {
            if (attacker == victim)
                return null;

            float amount = hit.damageTypes.Get(hit.damageTypes.GetMajorityDamageType());
            float scale = attackerAmount / 100;

            if (!adminPunish)
            {
                var victimId = victim.userID;
                var attackerId = attacker.userID;
                var hasFriend = (bool)(Friends?.CallHook("HasFriend", attackerId, victimId) ?? false);

                if (hasFriend)
                {
                    attacker.Hurt(amount * scale);
                    if (debug)Puts(amount.ToString());
                    if (debug)Puts(scale.ToString());
                    return true;
                }
            }
            else
            {
                if (!permission.UserHasPermission(victim.UserIDString, adminPerm))
                return null;

                attacker.Hurt(amount * scale);
                if (debug)Puts(amount.ToString());
                if (debug)Puts(scale.ToString());
                return true;
            }
            return null;
        }

        void OnPlayerAttack(BasePlayer attacker, HitInfo hitInfo)
        {
            if (attacker == null || hitInfo == null) return;
            if (hitInfo?.HitEntity is BasePlayer)
            OnAttackInternal(attacker, (BasePlayer)hitInfo.HitEntity, hitInfo);
        }
    }
}