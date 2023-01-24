using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Don't Target Me", "Quantum", "1.0.7")]
    [Description("Makes turrets, player npcs and normal npcs ignore you.")]

    class DontTargetMe : RustPlugin
    {
        #region Const
        const string permAllow = "donttargetme.allow";
        #endregion

        #region Configuration

        private new void LoadDefaultConfig()
        {
            Config.Clear();

            Config["Turrets"] = true;
            Config["PlayerNPC"] = true;
            Config["NPC"] = true;

            SaveConfig();
        }


        #endregion Configuration

        #region Hooks
        private void Init()
        {
            permission.RegisterPermission(permAllow, this);
        }

        private object CanBeTargeted(BaseCombatEntity entity, MonoBehaviour behaviour)
        {
            BasePlayer player = entity as BasePlayer;
            if (player != null && permission.UserHasPermission(player.UserIDString, permAllow) && (bool)Config["Turrets"])
            {
                return false;
            }

            return null;
        }

        private object OnNpcPlayerTarget(NPCPlayerApex npcPlayer, BaseEntity entity)
        {
            BasePlayer player = entity as BasePlayer;
            if (player != null && permission.UserHasPermission(player.UserIDString, permAllow) && (bool)Config["PlayerNPC"])
            {
                return true;
            }

            return null;
        }

        object OnNpcTarget(BaseNpc npc, BaseEntity entity)
        {
            BasePlayer player = entity as BasePlayer;
            if (player != null && permission.UserHasPermission(player.UserIDString, permAllow) && (bool)Config["NPC"])
            {
                return true;
            }

            return null;
        }

        bool CanBradleyApcTarget(BradleyAPC apc, BaseEntity entity)
        {
            BasePlayer player = entity as BasePlayer;
            if (player != null && permission.UserHasPermission(player.UserIDString, permAllow) && (bool)Config["Turrets"])
            {
                return false;
            }

            return true;
        }
        #endregion
    }
}
