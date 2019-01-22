namespace Oxide.Plugins
{
    [Info("Infinity", "Nikita -", "0.0.1")]
    class Infinity : RustPlugin
    {

        /////#region Permissions/////

        private void Init()
        {
          permission.RegisterPermission("infinity.durability", this);
          permission.RegisterPermission("infinity.explosives", this);
          permission.RegisterPermission("infinity.rockets", this);
          permission.RegisterPermission("infinity.ammo", this);
          permission.RegisterPermission("infinity.thrown", this);
        }
        bool IsAllowed(BasePlayer player, string perm)
        {
            if (permission.UserHasPermission(player.userID.ToString(), perm)) return false;
            return true;
        }

        /////#endregion Permissions/////

        /////#region Weapon Hooks/////

        private void OnLoseCondition(Item item, ref float amount)
        {
            var player = item?.GetOwnerPlayer();
            if (player == null) return;
            if (IsAllowed(player, "infinity.durability")) return;
            player.GetActiveItem().condition = player.GetActiveItem().info.condition.max;
            item.condition = item.maxCondition;
        }


        private void OnWeaponFired(BaseProjectile projectile, BasePlayer player)
        {
            if (player == null) return;
            if (IsAllowed(player, "infinity.ammo")) return;
            if (projectile.primaryMagazine.contents > 0) return;
            projectile.primaryMagazine.contents = projectile.primaryMagazine.capacity;
            projectile.SendNetworkUpdateImmediate();
        }


        private void OnRocketLaunched(BasePlayer player)
        {
            if (player == null) return;
            if (IsAllowed(player, "infinity.rockets")) return;
            var weapon = player.GetActiveItem().GetHeldEntity() as BaseProjectile;
            if (weapon == null) return;
            if (weapon.primaryMagazine.contents > 0) return;
            weapon.primaryMagazine.contents = weapon.primaryMagazine.capacity;
            weapon.SendNetworkUpdateImmediate();
        }


        private void OnExplosiveThrown(BasePlayer player)
        {
            if (player == null) return;
            if (IsAllowed(player, "infinity.explosives")) return;
            var weapon = player.GetActiveItem().GetHeldEntity() as ThrownWeapon;
            if (weapon == null) return;
                weapon.GetItem().amount += 1;
        }


        private void OnMeleeThrown(BasePlayer player, Item item)
        {
            if (player == null) return;
            if (IsAllowed(player, "infinity.thrown")) return;
            Item targetItem = player.inventory.containerBelt.FindItemsByItemName(item.info.shortname);
            if (targetItem != null)
            {
                targetItem.amount += 1;
                return;
            }
            Item newItem = ItemManager.CreateByPartialName(item.info.shortname, 1);
            newItem.MoveToContainer(player.inventory.containerBelt);

            /////#endregion Weapon Hooks/////
        }
    }
}
