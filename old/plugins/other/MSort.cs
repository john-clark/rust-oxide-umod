// ReSharper disable ArrangeTypeModifiers
// ReSharper disable ArrangeTypeMemberModifiers

using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("MSort", "AeonLucid", "0.0.1")]
    class MSort : RustPlugin
    {
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

            // Setup variables.
            var freshItems = RecreateInventory(new List<Item>(inventory.itemList));

            // All items that will be replaced.
            var unsafeItems = freshItems
                .SelectMany(x => x.UniqueItemIds)
                .Distinct()
                .ToList();

            // All items that have to be moved to a safe location.
            var safeItems = inventory.itemList
                .Where(x => !unsafeItems.Contains(x.uid))
                .Select(x => x.uid)
                .Distinct()
                .ToList();

            // Setup a temp inventory to move safe items to.
            var tempEntity = GameManager.server.CreateEntity("assets/prefabs/deployable/large wood storage/box.wooden.large.prefab");
            tempEntity.Spawn();
            var tempContainer = tempEntity.GetComponent<StorageContainer>();
            if (tempContainer == null)
            {
                player.ChatMessage("[MSort] Unable to create temp container.");
                return;
            }

            tempContainer.inventory.Clear();

            try
            {
                // Clean inventory.
                foreach (var itemUniqueId in inventory.itemList.Select(x => x.uid).ToArray())
                {
                    var item = inventory.FindItemByUID(itemUniqueId);
                    if (item == null)
                    {
                        continue;
                    }

                    if (unsafeItems.Contains(itemUniqueId))
                    {
                        item.Remove();
                    }
                    else if (!item.MoveToContainer(tempContainer.inventory))
                    {
                        DropItemForPlayer(player, item, "old to temp broke");
                    }
                }
                
                foreach (var freshItem in freshItems)
                {
                    var newItem = ItemManager.CreateByItemID(freshItem.ItemId, freshItem.Amount, freshItem.ItemSkinId);
                    if (newItem != null)
                    {
                        newItem.condition = freshItem.MaxCondition;
                        newItem.maxCondition = freshItem.MaxCondition;
                        newItem.fuel = freshItem.Fuel;

                        // FIXME: Fails to move if started with with semi-full inventory.
                        if (!newItem.MoveToContainer(inventory))
                        {
                            DropItemForPlayer(player, newItem, "new to inventory broke");
                        }
                    }
                }

                // Place temp items back.
                foreach (var tempItemUid in tempContainer.inventory.itemList.Select(x => x.uid).ToArray())
                {
                    var tempItem = tempContainer.inventory.FindItemByUID(tempItemUid);

                    // FIXME: Fails to move if started with with semi-full inventory.
                    if (tempItem != null && !tempItem.MoveToContainer(inventory, -1, false))
                    {
                        DropItemForPlayer(player, tempItem, "temp to inventory broke");
                    }
                }

                player.ChatMessage("[MSort] Your inventory has been sorted.");
            }
            finally
            {
                // Cleanup.
                tempEntity.Kill();

                inventory.SetLocked(false);
            }
        }

        private void DropItemForPlayer(BasePlayer player, Item item, string reason)
        {
            item.Drop(player.eyes.position, player.estimatedVelocity);

            player.ChatMessage($"[MSort] Dropped {item.amount}x {item.info.displayName.english} because '{reason}'.");
        }

        private List<FreshItem> RecreateInventory(List<Item> items)
        {
            var sortedItems = new List<FreshItem>();

            FreshItem currentItem = null;

            var previousAmount = 0;
            var previousItemId = -1;
            var previousSkinId = (ulong) 0;

            foreach (var item in OrderItems(items))
            {
                if (item.maxCondition.CompareTo(0) != 0 ||
                    item.contents != null)
                {
                    continue;
                }

                // Add current item to item list if
                //  item id changes
                //  skin id changes
                if (currentItem == null ||
                    previousItemId != item.info.itemid || 
                    previousSkinId != item.skin)
                {
                    if (currentItem != null)
                    {
                        sortedItems.Add(currentItem);
                    }

                    currentItem = new FreshItem(item);
                    currentItem.UniqueItemIds.Add(item.uid);

                    previousAmount = 0;
                }

                var currentAmount = previousAmount;
                var currentAmountLeft = item.amount;

                if (!currentItem.UniqueItemIds.Contains(item.uid))
                {
                    currentItem.UniqueItemIds.Add(item.uid);
                }

                do
                {
                    // Add current item to item list if
                    //  max stack has been reached
                    if (currentAmount == item.MaxStackable())
                    {
                        sortedItems.Add(currentItem);
                        
                        currentItem = new FreshItem(item);
                        currentItem.UniqueItemIds.Add(item.uid);
                        currentAmount = 0;
                    }

                    var amount = currentAmountLeft;
                    if (amount + currentAmount > item.MaxStackable()) // If overflow.
                    {
                        amount = item.MaxStackable() - currentAmount;
                    }
                    
                    currentAmount += amount;
                    currentAmountLeft -= amount;
                    currentItem.Amount += amount;
                } while (currentAmountLeft > 0);

                previousAmount = currentAmount;
                previousItemId = currentItem.ItemId;
                previousSkinId = currentItem.ItemSkinId;
            }

            if (currentItem != null)
            {
                sortedItems.Add(currentItem);
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

        class FreshItem
        {
            public FreshItem(Item item)
            {
                ItemName = item.info.displayName.english;
                ItemId = item.info.itemid;
                ItemSkinId = item.skin;
                Amount = 0;

                Fuel = item.fuel;
                Condition = item.condition;
                MaxCondition = item.maxCondition;
            }

            // Cleanup
            public List<uint> UniqueItemIds { get; } = new List<uint>();
            
            // Basic
            public string ItemName { get; }

            public int ItemId { get; }

            public ulong ItemSkinId { get; }

            public int Amount { get; set; }

            // Meta

            public float Fuel { get; }

            public float Condition { get; }

            public float MaxCondition { get; }
        }
    }
}