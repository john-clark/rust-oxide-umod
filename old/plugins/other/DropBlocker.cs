using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Drop Blocker", "Krava", 1.2)]
    [Description("Anti drop items at the craft.")]

    public class DropBlocker : RustPlugin
    {
        private class TempData
        {
            public TempData(int itemId, int amount)
            {
                ItemId = itemId;
                Amount = amount;
            }

            public int ItemId { get; set; }
            public int Amount { get; set; }
        }

        private void Loaded()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "CantCraft", "Item not crafted! Your inventory is full." }
            }, this);
        }

        /// <summary>
        /// Called when a player attempts to craft an item
        /// Returning true or false overrides default behavior
        /// </summary>
        private object CanCraft(ItemCrafter itemCrafter, ItemBlueprint bp, int amount)
        {
            var slots = new List<TempData>();

            foreach (var container in itemCrafter.containers)
                slots.AddRange(container.itemList.Select(x => new TempData(x.info.itemid, x.amount)));

            foreach (var craftTask in itemCrafter.queue)
                Merge(slots, craftTask.blueprint.targetItem, craftTask.amount);

            Merge(slots, bp.targetItem, amount);

            if (slots.Count() > itemCrafter.containers.Sum(x => x.capacity))
            {
                SendReply(itemCrafter.containers.First().playerOwner, lang.GetMessage("CantCraft", this));
                return false;
            }

            return null;
        }

        private void Merge(List<TempData> slots, ItemDefinition item, int amount)
        {
            foreach (var slot in slots.Where(x => x.ItemId == item.itemid && x.Amount < item.stackable))
            {
                if (amount == 0)
                    return;

                var toStack = item.stackable - slot.Amount;

                if (toStack >= amount)
                {
                    slot.Amount += amount;
                    amount = 0;
                }
                else
                {
                    slot.Amount += toStack;
                    amount = amount - toStack;
                }
            }

            while (amount > 0)
            {
                var temp = item.stackable > amount ? amount : item.stackable;
                amount -= temp;

                slots.Add(new TempData(item.itemid, temp));
            }
        }
    }
}