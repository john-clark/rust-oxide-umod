using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Keep Active Item", "redBDGR", "1.0.2")]
    [Description("Restores a player's held item in their corpse's hotbar when they die")]
    class KeepActiveItem : RustPlugin
    {
        const string permissionName = "keepactiveitem.use";
        Dictionary<string, Item> kepp = new Dictionary<string, Item>();

        void Init() => permission.RegisterPermission(permissionName, this);

        /*
        void OnPlayerDie(BasePlayer player, HitInfo info)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName))
                return;
            Item item = player.GetActiveItem();
            if (item == null)
                return;
            if (kepp.ContainsKey(player.UserIDString))
                kepp.Remove(player.UserIDString);
            kepp.Add(player.UserIDString, item);
        }
        */

        void CanBeWounded(BasePlayer player, HitInfo info)
        {
            Item item = player.GetActiveItem();
            if (item == null)
                return;
            if (kepp.ContainsKey(player.UserIDString))
                kepp[player.UserIDString] = item;
            kepp.Add(player.UserIDString, item);
        }

        void OnPlayerWound(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName))
                return;
            if (kepp.ContainsKey(player.UserIDString))
                kepp[player.UserIDString].MoveToContainer(player.inventory.containerBelt);
        }

        void OnPlayerRecover(BasePlayer player)
        {
            if (kepp.ContainsKey(player.UserIDString))
                kepp.Remove(player.UserIDString);
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!(entity is BasePlayer))
                return;
            BasePlayer player = entity as BasePlayer;
            if (!permission.UserHasPermission(player.UserIDString, permissionName))
                return;
            if (!kepp.ContainsKey(player.UserIDString))
                return;
            kepp[player.UserIDString].MoveToContainer(player.inventory.containerBelt);
            kepp.Remove(player.UserIDString);
        }
    }
}
