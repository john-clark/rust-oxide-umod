using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Flippable Turrets", "Orange", "1.0.1")]
    [Description("Allows users to place turrets how they like")]
    public class FlippableTurrets : RustPlugin
    {
        #region Vars

        private const string prefab = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab";
        private const string permUse = "flippableturrets.use";

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            permission.RegisterPermission(permUse, this);
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            CheckInput(player, input);
        }

        #endregion

        #region Core
        
        private void CheckInput(BasePlayer player, InputState input)
        {
            if (!input.WasJustPressed(BUTTON.FIRE_PRIMARY)) {return;}
            var heldEntity = player.GetActiveItem();
            if (heldEntity == null || heldEntity?.info?.shortname != "autoturret") {return;}
            if (!permission.UserHasPermission(player.UserIDString, permUse)) {return;}
            if (!player.CanBuild()){return;}
            RaycastHit rhit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out rhit)) {return;}
            var entity = rhit.GetEntity();
            if (entity == null) {return;}
            if (rhit.distance > 5f) {return;}
            if (!entity.ShortPrefabName.Contains("floor")) {return;}
            if (entity.transform.position.y <= player.transform.position.y) {return;}
            var turret = GameManager.server.CreateEntity(prefab);
            if (turret == null) {return;}
            turret.transform.position = rhit.point;
            turret.transform.LookAt(player.transform);
            turret.transform.Rotate(-45, 0, 180);
            turret.OwnerID = player.userID;
            turret.Spawn();
            player.inventory.Take(null, heldEntity.info.itemid, 1);
        }

        #endregion
    }
}