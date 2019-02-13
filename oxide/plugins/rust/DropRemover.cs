using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Drop Remover", "Ryan", "1.0.2")]
    [Description("Removes world items from the map with no effect on server performance")]

    public class DropRemover : RustPlugin
    {
        #region Declaration

        private bool ConfigChanged;
        private List<string> ItemBlacklist;

        #endregion

        #region Config

        protected override void LoadDefaultConfig() => PrintWarning("Generating default configuration file...");

        private void InitConfig()
        {
            ItemBlacklist = GetConfig(new List<string>()
            {
                "cctv.camera",
                "targeting.computer"
            }, "Item Blacklist");

            if (ConfigChanged)
            {
                PrintWarning("Updated configuration file with new/changed values.");
                SaveConfig();
            }
        }

        private T GetConfig<T>(T defaultVal, params string[] path)
        {
            var data = Config.Get(path);
            if (data != null)
            {
                return Config.ConvertValue<T>(data);
            }

            Config.Set(path.Concat(new object[] { defaultVal }).ToArray());
            ConfigChanged = true;
            return defaultVal;
        }

        #endregion

        #region Lang

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Permission"] = "You don't have permission to use that command",
                ["Removing"] = "Removing all dropped items on the map...",
                ["Removed"] = "Removed {0} dropped items on the map",
                ["CategoryRemove"] = "Removing all dropped items in the {0} category",
                ["Category"] = " in the '{0}' category",
                ["ItemRemove"] = "Removing all dropped {0}'s on the map",
                ["ItemRemoved"] = "Removed {0}x {1} from the map",
                ["NoDrops"] = "No drops found",
                ["InvalidArgs"] = "Invalid arguments",
                ["NoItem"] = "No item found with input '{0}'",
                ["NoCategory"] = "No category found with input '{0}'"
            }, this);
        }

        #endregion

        #region Methods

        private IEnumerator Delete(IEnumerable<BaseNetworkable> list, string userId, ItemCategory? category = null, Item item = null)
        {
            var count = 0;
            foreach (var listEntry in list)
            {
                var droppedItem = listEntry.GetComponent<DroppedItem>();
                if (droppedItem != null)
                {
                    if (ItemBlacklist.Contains(droppedItem.item.name))
                    {
                        continue;
                    }

                    if (item == null && category == null)
                    {
                        count++;
                        droppedItem.Kill();
                        yield return new WaitWhile(() => !droppedItem.IsDestroyed);
                    }
                    if (item != null && droppedItem.item.info.shortname == item.info.shortname)
                    {
                        count++;
                        droppedItem.Kill();
                        yield return new WaitWhile(() => !droppedItem.IsDestroyed);
                    }
                    if (category != null && droppedItem.item.info.category.Equals(category))
                    {
                        count++;
                        droppedItem.Kill();
                        yield return new WaitWhile(() => !droppedItem.IsDestroyed);
                    }
                }
            }

            var message = string.Format(lang.GetMessage("Removed", this, userId), count);

            if (category != ItemCategory.All)
            {
                message += string.Format(lang.GetMessage("Category", this, userId), category);
            }

            if (item != null)
            {
                message = string.Format(lang.GetMessage("ItemRemoved", this, userId), count, item.info.displayName.english);
            }

            if (count == 0)
            {
                message = lang.GetMessage("NoDrops", this, userId);
            }

            foreach (var admin in BasePlayer.activePlayerList.Where(p => p.IsAdmin))
            {
                PrintToChat(admin, message);
            }

            Puts(message);
        }

        #endregion

        #region Hooks

        private void Init()
        {
            InitConfig();
        }

        #endregion

        #region Commands

        [ConsoleCommand("drops.remove")]
        private void DropRemoveCommand(ConsoleSystem.Arg arg)
        {
            var userId = arg.Connection?.userid.ToString();
            if (string.IsNullOrEmpty(userId))
                userId = "0";
            if (arg.Connection != null && arg.Connection.authLevel < 2)
            {
                arg.ReplyWith(lang.GetMessage("Permission", this, userId));
                return;
            }
            arg.ReplyWith(lang.GetMessage("Removing", this, userId));
            ServerMgr.Instance.StartCoroutine(Delete(BaseNetworkable.serverEntities, userId));
        }

        [ConsoleCommand("drops.itemremove")]
        private void ItemRemove(ConsoleSystem.Arg arg)
        {
            var userId = arg.Connection?.userid.ToString();
            if (string.IsNullOrEmpty(userId))
                userId = "0";
            if (arg.Connection != null && arg.Connection.authLevel < 2)
            {
                arg.ReplyWith(lang.GetMessage("Permission", this, userId));
                return;
            }
            var itemName = arg.GetString(0);
            if (!string.IsNullOrEmpty(itemName))
            {
                var createdItem = ItemManager.CreateByPartialName(itemName);
                if (createdItem != null)
                {
                    arg.ReplyWith(string.Format(lang.GetMessage("ItemRemove", this, userId), createdItem.info.displayName.english));
                    ServerMgr.Instance.StartCoroutine(Delete(BaseNetworkable.serverEntities, userId, item: createdItem));
                    return;
                }
                arg.ReplyWith(string.Format(lang.GetMessage("NoItem", this, userId), itemName));
                return;
            }
            arg.ReplyWith(lang.GetMessage("InvalidArgs", this, userId));
        }

        [ConsoleCommand("drops.categoryremove")]
        private void CategoryRemove(ConsoleSystem.Arg arg)
        {
            var userId = arg.Connection?.userid.ToString();
            if (string.IsNullOrEmpty(userId))
                userId = "0";
            if (arg.Connection != null && arg.Connection.authLevel < 2)
            {
                arg.ReplyWith(lang.GetMessage("Permission", this, userId));
                return;
            }
            var categoryName = arg.GetString(0);
            if (!string.IsNullOrEmpty(categoryName))
            {
                var foundCategory = ItemCategory.All;
                foreach (var itemCategory in Enum.GetValues(typeof(ItemCategory)))
                {
                    if (itemCategory.ToString().ToLower().Equals(categoryName.ToLower()))
                        foundCategory = (ItemCategory) itemCategory;
                }
                if (foundCategory != ItemCategory.All)
                {
                    arg.ReplyWith(string.Format(lang.GetMessage("CategoryRemove", this, userId), foundCategory));
                    ServerMgr.Instance.StartCoroutine(Delete(BaseNetworkable.serverEntities, userId, foundCategory));
                    return;
                }
                arg.ReplyWith(string.Format(lang.GetMessage("NoCategory", this, userId), categoryName));
                return;
            }
            arg.ReplyWith(lang.GetMessage("InvalidArgs", this, userId));
        }

        #endregion
    }
}