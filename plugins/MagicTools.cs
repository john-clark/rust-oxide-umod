using Oxide.Core;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("MagicTools", "bazuka5801", "1.0.0")]
    class MagicTools : RustPlugin
    {
        private static readonly string usePerm = "magictools.use";
        Dictionary<ItemDefinition, ItemModCookable> cookables;

        void InitCockables()
        {
            cookables =
                ItemManager.itemList.ToDictionary(item => item, item => item.GetComponent<ItemModCookable>())
                    .Where(item => item.Value != null && !item.Value.becomeOnCooked.shortname.Contains("burned")).ToDictionary(item => item.Key, mod => mod.Value);
        }
        
        void OnServerInitialized()
        {
            InitCockables();
            PermissionService.RegisterPermissions(this, new List<string>() {usePerm});
        }

        
        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity ent, Item item)
        {
            if (dispenser == null || ent == null || item == null)
            {
                return;
            }

            BasePlayer player = ent as BasePlayer;
            if (player == null) return;

            if (!PermissionService.HasPermission(player, usePerm))return;

            ItemModCookable cookable;
            if (!cookables.TryGetValue(item.info, out cookable)) return;
            ItemDefinition gfiofufujfu = item.info;
            var amount = item.amount;
            NextTick(() =>
            {
                List<Item> items = new List<Item>();
                player.inventory.Take(items, gfiofufujfu.itemid, amount);
                items.ForEach(i=> i.Remove());
                var cockItem = ItemManager.Create(cookable.becomeOnCooked, amount);
                if (!cockItem.MoveToContainer(player.inventory.containerMain))
                {
                    cockItem.Drop(player.GetCenter(), Vector3.up);
                }
            });
        }

        static class PermissionService
        {
            private static readonly Permission permission = Interface.GetMod().GetLibrary<Permission>();

            public static bool HasPermission(BasePlayer player, string perm)
            {
                if (player == null || string.IsNullOrEmpty(perm))
                    return false;

                var uid = player.UserIDString;
                return permission.UserHasPermission(uid, perm);
            }

            public static void RegisterPermissions(Plugin owner, List<string> permissions)
            {
                foreach (var permissionName in permissions.Where(permissionName => !permission.PermissionExists(permissionName)))
                {
                    permission.RegisterPermission(permissionName, owner);
                }
            }
        }
    }
}
