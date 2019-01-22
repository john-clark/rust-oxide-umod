using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Furnace Sort", "Orange", "2.0.1")]
    [Description("Auto ore-splitting in furnaces")]
    public class FurnaceSort : RustPlugin
    {
        #region Vars

        private Dictionary<string, int> containers = new Dictionary<string, int>
        {
            {"furnace", 3},
            {"refinery_small_deployed", 3},
            {"furnace.large", 12}
        };

        #endregion
        
        #region Oxide Hooks

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            CheckMoving(item, container);
        }

        #endregion

        #region Helpers

        private bool ValidName(string name)
        {
            return !string.IsNullOrEmpty(name) && (name.Contains("ore") || name.Contains("oil"));
        }

        private bool ValidContainer(string name)
        {
            return !string.IsNullOrEmpty(name) && containers.ContainsKey(name);
        }

        #endregion

        #region Core
        
        private void CheckMoving(Item item, ItemContainer container)
        {
            if (item == null || container == null) {return;}
            var prefab = container.entityOwner?.ShortPrefabName;
            if (!ValidName(item.info.shortname)) {return;}
            if (!ValidContainer(prefab)) {return;}
            var expectation = containers[prefab];
            var available = container.capacity - container.itemList.Count;
            var stacks = available < expectation ? available : expectation;
            if (stacks == 0) {return;}
            var unit = item.amount / stacks;
            var list = new List<int>();
            
            if (unit < 1)
            {
                unit = 1;
                stacks = item.amount;
            }
            
            for (var i = 0; i < stacks; i++)
            {
                list.Add(unit);
            }
            
            list[0] += item.amount % stacks;
            
            if (list.Sum(Convert.ToInt32) != item.amount) {return;}
            item.DoRemove();
            CreateItems(container, item.info, list);
        }

        private void CreateItems(ItemContainer container, ItemDefinition def, List<int> stacks)
        {
            Unsubscribe("OnItemAddedToContainer");
            
            foreach (var value in stacks)
            {
                var item = ItemManager.Create(def, value);
                item.MoveToContainer(container, -1, false);
            }
            
            Subscribe("OnItemAddedToContainer");
        }

        #endregion
    }
}