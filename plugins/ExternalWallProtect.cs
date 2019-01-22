using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Physics = UnityEngine.Physics;

namespace Oxide.Plugins
{
    [Info("External Wall Protect", "redBDGR", "1.0.3")]
    [Description("Prevents ladders from being able to be placed on external walls")]

    class ExternalWallProtect : RustPlugin
    {
        private const string permissionName = "externalwallprotect.exempt";
        private static LayerMask collLayers = LayerMask.GetMask("Construction", "Clutter", "Deployed", "Tree", "Terrain", "World", "Water", "Default");

        private void Init()
        {
            permission.RegisterPermission(permissionName, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["Deny Crusher"] = "You are not allowed to place a ladder there",
            }, this);
        }

        private object CanBuild(Planner plan, Construction prefab)
        {
            if (prefab.prefabID != 2150203378)
                return null;
            BasePlayer player = plan.GetOwnerPlayer();
            if (player == null)
                return null;
            if (permission.UserHasPermission(player.UserIDString, permissionName))
                return null;
            RaycastHit[] hits = Physics.RaycastAll(player.eyes.HeadRay(), 5f, collLayers);
            if (!hits.Where(hit => hit.GetEntity() != null).Any(hit => hit.GetEntity().ShortPrefabName.Contains("external")))
                return null;
            player.ChatMessage(msg("Deny Crusher", player.UserIDString));
            return false;
        }

        private string msg(string key, string id = null) => lang.GetMessage(key, this, id);
    }
}
