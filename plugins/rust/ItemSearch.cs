using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Item Search", "Jacob", "1.0.2", ResourceId = 2679)]
    class ItemSearch : RustPlugin
    {
        #region Chat Command

        [ChatCommand("item")]
        private void ItemSearchCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "itemsearch.able") && !player.IsAdmin)
            {
                PrintToChat(player, lang.GetMessage("PermissionError", this, player.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                PrintToChat(player, lang.GetMessage("SyntaxError", this, player.UserIDString));
                return;
            }

            ItemDefinition item;
            var name = string.Join(" ", args.Skip(1).ToArray()).ToLower();
            switch (args[0].ToLower())
            {
                case "name":
                    if (args.Length < 2)
                    {
                        PrintToChat(player, lang.GetMessage("SyntaxError", this, player.UserIDString));
                        return;
                    }

                    item = ItemManager.itemList.FirstOrDefault(x => x.displayName.english.ToLower().Contains(name));
                    if (item == null)
                    {
                        PrintToChat(player, lang.GetMessage("NonexistentItem", this, player.UserIDString));
                        return;
                    }

                    PrintToChat(player, lang.GetMessage("ItemInfo", this, player.UserIDString)
                        .Replace("{name}", item.displayName.english)
                        .Replace("{category}", item.category.ToString())
                        .Replace("{description}", item.displayDescription.english)
                        .Replace("{ID}", item.itemid.ToString())
                        .Replace("{shortname}", item.shortname));
                    break;

                case "shortname":
                    if (args.Length < 2)
                    {
                        PrintToChat(player, lang.GetMessage("SyntaxError", this, player.UserIDString));
                        return;
                    }

                    item = ItemManager.itemList.FirstOrDefault(x => x.shortname.Contains(name));
                    if (item == null)
                    {
                        PrintToChat(player, lang.GetMessage("NonexistentItem", this, player.UserIDString));
                        return;
                    }

                    PrintToChat(player, lang.GetMessage("ItemInfo", this, player.UserIDString)
                        .Replace("{name}", item.displayName.english)
                        .Replace("{category}", item.category.ToString())
                        .Replace("{description}", item.displayDescription.english)
                        .Replace("{ID}", item.itemid.ToString())
                        .Replace("{shortname}", item.shortname));
                    break;

                case "help":
                    if (args.Length < 1)
                    {
                        PrintToChat(player, lang.GetMessage("SyntaxError", this, player.UserIDString));
                        return;
                    }

                    PrintToChat(player, lang.GetMessage("Help", this, player.UserIDString));
                    break;
                
                default:
                    PrintToChat(player, lang.GetMessage("SyntaxError", this, player.UserIDString));
                    break;
            }
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages() => lang.RegisterMessages(new Dictionary<string, string>
        {
            {"Help", "<size=16>Item Search Help</size>" +
                     "\n<size=14><color=lightblue>/item name <name></color></size> <size=13>Searches items by name.</size>" +
                     "\n<size=14><color=lightblue>/item shortname <name></color></size> <size=13>Searches items by shortname.</size>"},
            {"ItemInfo", "<size=16>{name}</size>" +
                         "\n<size=14><color=lightblue>Category</color></size> <size=13>{category}</size>" +
                         "\n<size=14><color=lightblue>Description</color></size> <size=13>{description}</size>" +
                         "\n<size=14><color=lightblue>ID</color></size> <size=13>{ID}</size>" +
                         "\n<size=14><color=lightblue>Shortname</color></size> <size=13>{shortname}</size>"},
            {"NonexistentItem", "Error, nonexistent item."},
            {"PermissionError", "Error, you lack permission."},
            {"SyntaxError", "Error, incorrect syntax. Try looking at <color=lightblue>/item help</color>."}
        }, this);

        #endregion

        #region Oxide Hooks

        private void Init() => permission.RegisterPermission("itemsearch.able", this);

        #endregion
    }
}