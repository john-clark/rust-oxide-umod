using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Anti Items", "redBDGR", "1.0.10")]
    [Description("Removes the need for certain items when crafting and repairing")]
    class AntiItems : RustPlugin
    {
        private bool Changed;
        private Dictionary<string, object> componentList;
        private string permissionName = "antiitems.use";
        private float refreshTime = 120f;
        private bool useActiveRefreshing = true;

        private static Dictionary<string, object> doComponentList()
        {
            var x = new Dictionary<string, object> {{"propanetank", 1000}, {"gears", 1000}, {"metalpipe", 1000}, {"metalspring", 1000}, {"riflebody", 1000}, {"roadsigns", 1000}, {"rope", 1000}, {"semibody", 1000}, {"sewingkit", 1000}, {"smgbody", 1000}, {"tarp", 1000}, {"techparts", 1000}, {"sheetmetal", 1000}};
            return x;
        }

        private void OnPlayerInit(BasePlayer player)
        {
            timer.Once(5f, () => DoItems(player));
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            RemoveItems(player, player.inventory.containerMain);
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            DoItems(player);
        }

        private void Init()
        {
            LoadVariables();
            permission.RegisterPermission(permissionName, this);
            if (useActiveRefreshing)
                timer.Repeat(refreshTime, 0, () =>
                {
                    foreach (var player in BasePlayer.activePlayerList)
                        if (player.IsConnected && !player.IsDead())
                            if (permission.UserHasPermission(player.UserIDString, permissionName))
                                RefreshItems(player);
                });
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!(entity is BasePlayer)) return;
            RemoveItems((BasePlayer) entity, ((BasePlayer) entity).inventory.containerMain);
        }

        private void OnItemCraftCancelled(ItemCraftTask task)
        {
            foreach (var entry in task.takenItems)
                if (componentList.ContainsKey(entry.info.shortname))
                    timer.Once(0.01f, () => entry.RemoveFromContainer());
        }

        private void RefreshItems(BasePlayer player)
        {
            for (var i = 0; i < componentList.Count; i++)
                if (player.inventory.containerMain.GetSlot(24 + i) != null)
                    player.inventory.containerMain.GetSlot(24 + i).RemoveFromContainer();
            DoItems(player);
        }

        private void DoItems(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName)) return;
            player.inventory.containerMain.capacity = 24 + componentList.Count;
            var y = componentList.Select(key => key.Key).ToList();
            for (var i = 0; i < componentList.Count; i++)
            {
                var item = ItemManager.CreateByName(y[i], Convert.ToInt32(componentList[y[i]]));
                item.MoveToContainer(player.inventory.containerMain, 24 + i, true);
            }
        }

        private void RemoveItems(BasePlayer player, ItemContainer container)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName)) return;
            var x = new List<Item>();

            foreach (var item in container.itemList)
            {
                if (item == null) return;
                if (componentList.ContainsKey(item.info.shortname))
                    x.Add(item);
            }
            foreach (var key in x)
            {
                key.RemoveFromContainer();
                key.Remove(0.1f);
            }
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {
            componentList = (Dictionary<string, object>) GetConfig("Settings", "Components", doComponentList());
            useActiveRefreshing = Convert.ToBoolean(GetConfig("Settings", "Use Active Item Refreshing", true));
            refreshTime = Convert.ToSingle(GetConfig("Settings", "Refresh Time", 120f));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (data.TryGetValue(datavalue, out value)) return value;
            value = defaultValue;
            data[datavalue] = value;
            Changed = true;
            return value;
        }
    }
}
