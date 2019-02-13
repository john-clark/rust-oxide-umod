using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Better Grenades", "redBDGR", "1.0.2")]
    [Description("Adds enhanced grenades features")]
    class BetterGrenades : RustPlugin
    {
        private bool Changed;
        private static BetterGrenades plugin;

        private Dictionary<string, float> throwerList = new Dictionary<string, float>();

        private float f1FuseTime = 10f;
        private float beancanFuseTime = 5f;

        #region Configuration

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {
            f1FuseTime = Convert.ToSingle(GetConfig("Settings", "F1 Grenade Fuse Time", 10f));
            beancanFuseTime = Convert.ToSingle(GetConfig("Settings", "Beancan Grenade Fuse Time", 5f));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            plugin = this;
            LoadVariables();

            foreach(BasePlayer player in BasePlayer.activePlayerList)
                if (player.IsConnected)
                    if (player.GetActiveItem()?.info.shortname == "grenade.f1" || player.GetActiveItem()?.info.shortname == "grenade.beancan")
                    {
                        GrenadeTimer grenadeTimer = player.GetComponent<GrenadeTimer>();
                        if (grenadeTimer)
                            grenadeTimer.DestroyThis();
                        player.gameObject.AddComponent<GrenadeTimer>();
                    }
        }

        private void Unload()
        {
            foreach(BasePlayer player in BasePlayer.activePlayerList)
                player.GetComponent<GrenadeTimer>()?.DestroyThis();
        }

        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (!throwerList.ContainsKey(player.UserIDString))
                return;
            if (entity.ShortPrefabName != "grenade.f1.deployed" && entity.ShortPrefabName != "grenade.beancan.deployed")
                return;
            TimedExplosive expl = entity.GetComponent<TimedExplosive>();
            if (!expl) return;
            expl.CancelInvoke("Explode");
            expl.SetFuse(throwerList[player.UserIDString]);
        }

        private void OnExplosiveDropped(BasePlayer player, BaseEntity entity)
        {
            if (!throwerList.ContainsKey(player.UserIDString))
                return;
            if (entity.ShortPrefabName != "grenade.f1.deployed" && entity.ShortPrefabName != "grenade.beancan.deployed")
                return;
            TimedExplosive expl = entity.GetComponent<TimedExplosive>();
            if (!expl) return;
            expl.CancelInvoke("Explode");
            expl.SetFuse(throwerList[player.UserIDString]);
        }

        private void OnPlayerActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (newItem != null)
                if (newItem.info.shortname == "grenade.f1" || newItem.info.shortname == "grenade.beancan")
                    player.gameObject.AddComponent<GrenadeTimer>();
            else if (oldItem != null)
                if (oldItem.info.shortname == "grenade.f1" || oldItem.info.shortname == "grenade.beancan")
                    player.GetComponent<GrenadeTimer>()?.DestroyThis();
        }

        #endregion

        #region MonoBehaviour Classes

        private class GrenadeTimer : MonoBehaviour
        {
            private BasePlayer player;
            private Item activeItem;

            private bool cookingGrenade;
            private float startTime;
            private float timeUntilGrenadeFire;

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                activeItem = player.GetActiveItem();
            }

            private void Update()
            {
                if (!player)
                {
                    DestroyThis();
                    return;
                }
                Item latestItem = player.GetActiveItem();
                if (latestItem == null)
                {
                    DestroyThis();
                    return;
                }
                if (latestItem.info.shortname != "grenade.f1" || latestItem.info.shortname != "grenade.beancan")
                {
                    DestroyThis();
                    return;
                }
                if (player.serverInput.IsDown(BUTTON.FIRE_PRIMARY))
                {
                    if (!cookingGrenade)
                    {
                        cookingGrenade = true;
                        startTime = UnityEngine.Time.time;
                        timeUntilGrenadeFire = startTime + GetExplosionLength(activeItem);
                    }
                    if (UnityEngine.Time.time >= timeUntilGrenadeFire)
                        HandleSuicideExplosion(activeItem);
                }
                else
                {
                    if (!cookingGrenade)
                        return;
                    if (plugin.throwerList.ContainsKey(player.UserIDString))
                        plugin.throwerList.Remove(player.UserIDString);
                    plugin.throwerList.Add(player.UserIDString, timeUntilGrenadeFire - UnityEngine.Time.time);
                    cookingGrenade = false;
                }
            }

            private static float GetExplosionLength(Item item)
            {
                switch (item.info.shortname)
                {
                    case "grenade.f1":
                        return plugin.f1FuseTime;
                    case "grenade.beancan":
                        return plugin.beancanFuseTime;
                }
                return 10f;
            }

            private void HandleSuicideExplosion(Item item) // الله أكبر
            {
                player.inventory.containerBelt.Take(new List<Item>{ item }, item.info.itemid, 1);
                BaseEntity grenade = null;
                switch (item.info.shortname)
                {
                    case "grenade.f1":
                        grenade = GameManager.server.CreateEntity("assets/prefabs/weapons/f1 grenade/grenade.f1.deployed.prefab", player.transform.position + new Vector3(0, 1, 0));
                        break;
                    case "grenade.beancan":
                        grenade = GameManager.server.CreateEntity("assets/prefabs/weapons/beancan grenade/grenade.beancan.deployed.prefab", player.transform.position + new Vector3(0, 1, 0));
                        break;
                }
                if (grenade == null)
                    return;
                grenade.Spawn();
                TimedExplosive expl = grenade.GetComponent<TimedExplosive>();
                expl.Explode();
                cookingGrenade = false;
                if (!player.IsDead())
                    RemoveActiveItem(player);
                DestroyThis();
            }

            // Active item removal code courtesy of Fujikura
            private static void RemoveActiveItem(BasePlayer player)
            {
                foreach (var item in player.inventory.containerBelt.itemList.Where(x => x.IsValid() && x.GetHeldEntity()).ToList())
                {
                    var slot = item.position;
                    item.RemoveFromContainer();
                    item.MarkDirty();
                    plugin.timer.Once(0.15f, () =>
                    {
                        if (item == null)
                            return;
                        item.MoveToContainer(player.inventory.containerBelt, slot);
                        item.MarkDirty();
                    });
                }
            }

            public void DestroyThis() => Destroy(this);
        }

        #endregion

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
