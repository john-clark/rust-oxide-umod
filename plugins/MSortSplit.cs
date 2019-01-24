// ReSharper disable ArrangeTypeModifiers
// ReSharper disable ArrangeTypeMemberModifiers

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MSortSplit", "AeonLucid", "0.0.1")]
    class MSortSplit : RustPlugin
    {
        public static MSortSplit Instance { get; set; }

        #region Hooks

        private void Loaded()
        {
            Instance = this;
        }
        
        #endregion

        #region Commands

        [ChatCommand("isort")]
        void InventorySort(BasePlayer player, string command, string[] args)
        {
            if (!player.displayName.ToLower().Contains("aeonlucid"))
            {
                player.ChatMessage("[MSort] No permission.");
                return;
            }

            var inventory = player.inventory.containerMain;
            if (inventory == null)
            {
                return;
            }

            // Lock inventory.
            inventory.SetLocked(true);

            try
            {
                using (var container = SortTempContainer.Create(player))
                {
                    Puts($"Player capacity {inventory.capacity} itemlistCount {inventory.itemList.Count}");

                    // Move player inventory to temp storage.
                    foreach (var itemUid in inventory.itemList.Select(x => x.uid).ToArray())
                    {
                        var item = inventory.FindItemByUID(itemUid);

                        item?.MoveToContainer(container.Inventory);
                    }

                    Puts($"=> Player capacity {inventory.capacity} itemlistCount {inventory.itemList.Count}");

                    // Sort temp storage
                    var sortResult = SortInventory(new List<Item>(container.Inventory.itemList));

                    DebugItemStackList(sortResult);
                    
                    // Move temp storage back.
                    var position = 0;

                    foreach (var sortItemStack in sortResult)
                    {
                        var destination = position++;

                        foreach (var sortItem in sortItemStack.Items)
                        {
                            var item = container.Inventory.FindItemByUID(sortItem.SourceUniqueItemId);
                            if (item == null)
                            {
                                throw new Exception($"Item [{sortItem.SourceUniqueItemId}] could not be found in temp storage.");
                            }

                            // Do we have to split from source?
                            if (sortItem.SourceSplit)
                            {
                                item = item.SplitItem(sortItem.Amount);
                            }

                            // Move back.
                            item.MoveToContainer(inventory, destination);
                        }
                    }
                }
            }
            finally
            {
                inventory.SetLocked(false);
            }
        }

        #endregion

        #region Utility

        private List<SortItemStack> SortInventory(List<Item> items)
        {
            // SortedItems holds max stack of items.
            // One array equals one max stacked item.
            var sortedItems = new List<SortItemStack>();

            SortItemStack currentStack = null;

            var previousAmount = 0;
            var previousItemId = -1;
            var previousSkinId = (ulong)0;

            foreach (var item in OrderItems(items))
            {
                // Check if itemid or skinid changed, if so
                // we should create a new list.
                if (currentStack == null ||
                    previousItemId != item.info.itemid ||
                    previousSkinId != item.skin)
                {
                    if (currentStack != null &&
                        currentStack.Items.Count > 0)
                    {
                        sortedItems.Add(currentStack);
                    }

                    currentStack = new SortItemStack(item.info.itemid, item.skin, item.info.displayName.english);
                    previousAmount = 0;
                }

                var currentAmount = previousAmount;
                var currentAmountLeft = item.amount;

                do
                {
                    // Add current list to sorted list if
                    // max stack has been reached for the item.
                    if (currentAmount == item.MaxStackable())
                    {
                        sortedItems.Add(currentStack);

                        currentStack = new SortItemStack(item.info.itemid, item.skin, item.info.displayName.english);
                        currentAmount = 0;
                    }

                    var sourceSplit = false;
                    var amount = currentAmountLeft;
                    if (amount + currentAmount > item.MaxStackable()) // If overflow.
                    {
                        amount = item.MaxStackable() - currentAmount;
                        sourceSplit = true;
                    }

                    currentAmount += amount;
                    currentAmountLeft -= amount;

                    currentStack.Items.Add(new SortItem(item.uid, sourceSplit, amount));
                } while (currentAmountLeft > 0);

                previousAmount = currentAmount;
                previousItemId = item.info.itemid;
                previousSkinId = item.skin;
            }

            if (currentStack != null &&
                currentStack.Items.Count > 0)
            {
                sortedItems.Add(currentStack);
            }

            return sortedItems;
        }

        private IEnumerable<Item> OrderItems(IEnumerable<Item> source)
        {
            return source
                .OrderBy(x => x.info.shortname.Contains("wood"))
                .ThenBy(x => x.info.shortname.Contains(".ore"))
                .ThenBy(x => x.info.category)
                .ThenBy(x => x.info.itemid)
                .ThenBy(x => x.amount)
                .Reverse();
        }

        private void DropItemForPlayer(BasePlayer player, Item item, string reason)
        {
            item.Drop(player.eyes.position, player.estimatedVelocity);

            player.ChatMessage($"[MSort] Dropped {item.amount}x {item.info.displayName.english} because '{reason}'.");
        }

        #endregion

        #region Classes

        class SortTempContainer : IDisposable
        {
            private const string ContainerPrefab = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab";

            private readonly BaseEntity _entity;

            private readonly BasePlayer _player;

            private SortTempContainer(BaseEntity entity, BasePlayer player)
            {
                _entity = entity;
                _player = player;
            }

            public ItemContainer Inventory => _entity?.GetComponent<StorageContainer>().inventory;

            public static SortTempContainer Create(BasePlayer player)
            {
                var entity = GameManager.server.CreateEntity(ContainerPrefab, player.transform.position - new Vector3(0, UnityEngine.Random.Range(2000, 5000)));

                entity.UpdateNetworkGroup();
                entity.SendNetworkUpdateImmediate();
                entity.globalBroadcast = true;
                entity.Spawn();

                return new SortTempContainer(entity, player);
            }

            public void Dispose()
            {
                // Drop any leftovers.
                if (_entity != null && 
                    _player != null)
                {
                    foreach (var itemUid in Inventory.itemList.Select(x => x.uid).ToArray())
                    {
                        var item = Inventory.FindItemByUID(itemUid);
                        if (item != null)
                        {
                            Instance.DropItemForPlayer(_player, item, "leftovers");
                        }
                    }
                }

                // Remove entity.
                _entity?.Kill();
            }
        }

        class SortItemStack
        {
            public SortItemStack(int itemId, ulong skinId, string name)
            {
                ItemId = itemId;
                SkinId = skinId;
                Name = name;
                Items = new List<SortItem>();
            }

            public int ItemId { get; }

            public ulong SkinId { get; }

            public string Name { get; }

            public List<SortItem> Items { get; }
        }

        class SortItem
        {
            public SortItem(uint sourceUniqueItemId, bool sourceSplit, int amount)
            {
                SourceUniqueItemId = sourceUniqueItemId;
                SourceSplit = sourceSplit;
                Amount = amount;
            }

            public uint SourceUniqueItemId { get; }

            public bool SourceSplit { get; }

            public int Amount { get; }
        }

        #endregion

        #region Debug

        private void DebugItemStackList(List<SortItemStack> itemStacks)
        {
            foreach (var itemStack in itemStacks)
            {
                Puts($"[{itemStack.ItemId}] {itemStack.Name}");

                foreach (var item in itemStack.Items)
                {
                    Puts($" - [{item.Amount,-5}x {item.SourceSplit,-5}] {item.SourceUniqueItemId}");
                }
            }
        }

        #endregion
    }
}