using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("ItemSplitter", "sami37", "1.1.0", ResourceId = 2566)]
    [Description("It allow you to easily split your items from your main inventory.")]
    public class ItemSplitter : RustPlugin
    {
        bool HasAccess(BasePlayer player, string permissionName)
        {
            return player.net.connection.authLevel > 1 || permission.UserHasPermission(player.UserIDString, permissionName);
        }

        private void Init()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
				{"NoPerm", "You don't have permission to do this."},
                {"Success", "You have successful splited you item {0}, half drop at your feet."},
                {"NotFound", "We don't found such items in your inventory"},
                {"NotExist", "Item {0} doesn't exist or not found."},
                {"Syntax", "Syntax: /split wood (will split your possessed wood in equality part"}
            }, this);
            permission.RegisterPermission("itemsplitter.use", this);
        }

        [ChatCommand("split")]
        void cmdSplit(BasePlayer player, string cmd, string[] args)
        {
            if (player == null) return;
            if(!HasAccess(player, "itemsplitter.use"))
                SendReply(player, lang.GetMessage("NoPerm", this, player.UserIDString));
            if (args.Length == 0)
            {
                SendReply(player, lang.GetMessage("Syntax", this, player.UserIDString));
                return;
            }
            var inventory = player.inventory;
            var item = ItemManager.itemList.FindAll(x => x.displayName.english.ToLower().Contains(args[0]));
            if (item.Count == 0)
            {
                SendReply(player, string.Format(lang.GetMessage("NotExist", this, player.UserIDString), args[0]));
                return;
            }
            bool splited = false;
            foreach (var data in item)
            {
                var items = inventory.containerMain.FindItemsByItemID(data.itemid);
                if (items != null)
                {
                    foreach (var invItems in items)
                    {
                        var amount = invItems.amount;
                        if(amount < 2) continue;
                        var createdItem = ItemManager.CreateByItemID(invItems.info.itemid, amount / 2, invItems.skin);
                        if (createdItem != null)
                        {
                            var moved = createdItem.MoveToContainer(player.inventory.containerWear, -1, false) || createdItem.MoveToContainer(player.inventory.containerMain, -1, false);
                            if (!moved)
                            {
                                createdItem.Drop(player.eyes.position, player.eyes.BodyForward()*2f);
                            }
                            invItems.SplitItem(invItems.amount/2);
                            SendReply(player,
                                string.Format(lang.GetMessage("Success", this, player.UserIDString),
                                    invItems.info.displayName.english));
                            splited = true;
                        }
                    }
                }
            }
            if (!splited)
            {
                SendReply(player, string.Format(lang.GetMessage("NotExist", this, player.UserIDString), args[0]));
            }
        }
    }
}