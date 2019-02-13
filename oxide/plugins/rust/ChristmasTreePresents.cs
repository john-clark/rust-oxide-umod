using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Christmas Tree Presents", "redBDGR", "1.0.1")]
    [Description("Spawns Christmas presents under Christmas trees")]
    class ChristmasTreePresents : RustPlugin
    {
        private int minNumOfPresents = 3;
        private int maxNumOfPresents = 4;
        private bool treeNeedsAllOrnaments = true;
        private bool treeNeedsToBeOnFoundation = true;

        private bool presentSpawned;
        private bool Changed;

        #region Configuration

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {
            minNumOfPresents = Convert.ToInt32(GetConfig("Settings", "Minimum number of presents per tree", 3));
            maxNumOfPresents = Convert.ToInt32(GetConfig("Settings", "Maximum number of presents per tree", 4));
            treeNeedsAllOrnaments = Convert.ToBoolean(GetConfig("Settings", "Tree needs all ornaments", true));
            treeNeedsToBeOnFoundation = Convert.ToBoolean(GetConfig("Settings", "Tree needs to be on foundation", true));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            LoadVariables();
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (presentSpawned)
                return;
            if (entity.ShortPrefabName != "giftbox_loot")
                return;
            presentSpawned = true;
            timer.Once(1f, () => presentSpawned = false);
            foreach(ChristmasTree tree in UnityEngine.Object.FindObjectsOfType<ChristmasTree>())
                SpawnPresents(tree);
        }

        #endregion

        #region Helper Methods

        private void SpawnPresents(ChristmasTree tree)
        {
            if (treeNeedsAllOrnaments)
                if (!CheckOrnaments(tree))
                    return;
            if (treeNeedsToBeOnFoundation)
                if (!CheckBuilding(tree))
                    return;
            for (int i = 0; i < UnityEngine.Random.Range(minNumOfPresents, maxNumOfPresents + 1); i++)
                CreatePresent(new Vector3(UnityEngine.Random.Range(-1.2f, 1.2f), 0, UnityEngine.Random.Range(-1.2f, 1.2f)) + tree.transform.localPosition, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
        }

        private static void CreatePresent(Vector3 pos, Quaternion rot)
        {
            BaseEntity present = GameManager.server.CreateEntity("assets/prefabs/misc/xmas/giftbox/giftbox_loot.prefab", pos, rot);
            present.Spawn();
        }

        private static bool CheckOrnaments(ChristmasTree tree)
        {
            return tree.GetComponent<StorageContainer>().inventory.IsFull();
        }

        private static bool CheckBuilding(ChristmasTree tree)
        {
            DecayEntity decay = tree.GetComponent<DecayEntity>();
            if (decay == null)
                return false;
            return decay.GetBuilding() != null;
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
