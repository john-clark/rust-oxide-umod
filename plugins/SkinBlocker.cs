using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SkinBlocker", "redBDGR", "1.0.0")]
    [Description("Block certain skins from being applied to items / deployables")]

    class SkinBlocker : RustPlugin
    {
        private static bool Changed = false;

        #region Configuration

        private class ConfigFile
        {
            public static List<ulong> itemSkinBlacklist = new List<ulong>();
            public static List<ulong> deployableSkinBlacklist = new List<ulong>();

            public static List<object> GetDefaultItemSkinList()
            {
                List<object> x = new List<object>();
                x.Add(12345);
                x.Add(54321);
                return x;
            }

            public static List<object> GetDefaultDeployableSkinList()
            {
                List<object> x = new List<object>();
                x.Add(12345);
                x.Add(54321);
                return x;
            }
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {
            var itemSkins = (List<object>)GetConfig("Settings", "Item skins blacklist", ConfigFile.GetDefaultItemSkinList());
            foreach (var entry in itemSkins)
                ConfigFile.itemSkinBlacklist.Add(Convert.ToUInt64(entry));

            var entitySkins = (List<object>)GetConfig("Settings", "Deployable skins blacklist", ConfigFile.GetDefaultDeployableSkinList());
            foreach(var entry in entitySkins)
                ConfigFile.deployableSkinBlacklist.Add(Convert.ToUInt64(entry));

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
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        #endregion

        #region Hooks

        private void Init()
        {
            LoadVariables();
        }

        private void OnServerInitialized()
        {
            ScanAllItems();
        }

        private void OnItemAddedToContainer(ItemContainer container, Item item) => CheckItem(item);

        private void OnEntitySpawned(BaseNetworkable entity) => CheckEntity(entity.GetComponent<BaseEntity>());

        #endregion

        #region Methods

        private bool CheckEntity(BaseEntity ent)
        {
            if (ent == null)
                return false;
            if (ConfigFile.deployableSkinBlacklist.Contains(ent.skinID))
            {
                ent.skinID = 0;
                ent.SendNetworkUpdateImmediate();
                return true;
            }
            else
                return false;
        }

        private bool CheckItem(Item item)
        {
            if (item == null)
                return false;
            if (ConfigFile.itemSkinBlacklist.Contains(item.skin))
            {
                item.skin = 0;
                item.MarkDirty();
                return true;
            }
            else
                return false;
        }

        private void ScanAllItems()
        {
            ServerMgr.Instance.StartCoroutine(CheckAllEntities());
            ServerMgr.Instance.StartCoroutine(CheckAllItems());
        }

        private IEnumerator CheckAllEntities()
        {
            foreach(BaseEntity entity in UnityEngine.Object.FindObjectsOfType<BaseEntity>())
            {
                yield return new WaitForSeconds(.01f);
                CheckEntity(entity);
            }
        }

        private IEnumerator CheckAllItems()
        {
            foreach (StorageContainer entity in UnityEngine.Object.FindObjectsOfType<StorageContainer>())
            {
                yield return new WaitForSeconds(.01f);
                foreach(Item item in entity.inventory.itemList)
                {
                    yield return new WaitForSeconds(.01f);
                    CheckItem(item);
                }
            }
        }

        #endregion
    }
}
