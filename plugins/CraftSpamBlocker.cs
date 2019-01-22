using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("CraftSpamBlocker", "Wulf/lukespragg", "0.3.0", ResourceId = 1805)]
    [Description("Prevents items from being crafted if the player's inventory is full")]

    class CraftSpamBlocker : RustPlugin
    {
        void Init()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["InventoryFull"] = "Item was not crafted, inventory is full!"
            }, this);
        }

        void Cancel(ItemCraftTask task, bool cancelAll)
        {
            BasePlayer player = task.owner;
            PlayerInventory inventory = player.inventory;

            if (inventory.containerMain.itemList.Count > 23 && inventory.containerBelt.itemList.Count > 5)
            {
                ItemCrafter crafter = inventory.crafting;

                NextTick(() =>
                {
                    if (cancelAll) crafter.CancelAll(false);
                    else crafter.CancelTask(task.taskUID, true);
                });

                SendReply(player, lang.GetMessage("InventoryFull", this, player.UserIDString));
            }
        }

        void OnItemCraft(ItemCraftTask task) => Cancel(task, false);

        void OnItemCraftFinished(ItemCraftTask task) => Cancel(task, true);
    }
}