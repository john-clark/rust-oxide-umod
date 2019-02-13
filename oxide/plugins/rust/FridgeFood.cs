using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("FridgeFood", "sami37", "1.0.5")]
    [Description("Prevent food from being placed in box instead of fridge.")]
    public class FridgeFood : RustPlugin
    {
        private List<object> listFood = new List<object>();
        private List<object> listContainer = new List<object>();
        List<string> defaultLists = new List<string>();
        private List<object> defaultList = new List<object>();

        private List<object> unallowedContainer = new List<object>
        {
            "box.wooden.large",
            "box.wooden",
            "coffin.storage",
            "small.stash",
            "locker",

        };

        #region ConfigFunction
        string ListToString<T>(List<T> list, int first = 0, string seperator = ", ") => string.Join(seperator, (from val in list select val.ToString()).Skip(first).ToArray());
        void SetConfig(params object[] args) { List<string> stringArgs = (from arg in args select arg.ToString()).ToList(); stringArgs.RemoveAt(args.Length - 1); if (Config.Get(stringArgs.ToArray()) == null) Config.Set(args); }
        T GetConfig<T>(T defaultVal, params object[] args) { List<string> stringArgs = (from arg in args select arg.ToString()).ToList(); if (Config.Get(stringArgs.ToArray()) == null) { PrintError($"The plugin failed to read something from the config: {ListToString(stringArgs, 0, "/")}{Environment.NewLine}Please reload the plugin and see if this message is still showing. If so, please post this into the support thread of this plugin."); return defaultVal; } return (T)System.Convert.ChangeType(Config.Get(stringArgs.ToArray()), typeof(T)); }
        #endregion

        private static BasePlayer GetPlayerFromContainer(ItemContainer container, Item item) =>
            item.GetOwnerPlayer() ??
            BasePlayer.activePlayerList.FirstOrDefault(
                p => p.inventory.loot.IsLooting() && p.inventory.loot.entitySource == container.entityOwner);

        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (container.entityOwner != null)
            {
                var player = GetPlayerFromContainer(container, item);
                if (player != null)
                    foreach (var cont in listContainer)
                        if (container.entityOwner.ShortPrefabName.Replace("_deployed", "").Replace("_", ".") ==
                            cont.ToString())
                            if (listFood.Contains(item.info.itemid) || listFood.Contains(item.info.shortname))
                            {
                                SendReply(player, lang.GetMessage("NotAllowed", this, player.UserIDString));
                                item.Drop(container.dropPosition, container.dropVelocity);
                            }
            }
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();

            SetConfig("Food List", defaultList);
            SetConfig("Unallowed container", unallowedContainer);
            SaveConfig();
        }

        private void OnServerInitialized()
        {
            listFood = GetConfig(defaultList, "Food List");
            defaultLists = ItemManager.GetItemDefinitions().Where(itemDef => itemDef.category == ItemCategory.Food).Select(itemDef => itemDef.shortname).ToList();
            foreach(var item in defaultLists)
                if(!listFood.Contains(item))
                    listFood.Add(item);

            listContainer = GetConfig(unallowedContainer, "Unallowed container");

            SaveConfig();

            lang.RegisterMessages(
                new Dictionary<string, string>
                    {
                        { "NotAllowed", "You are not allowed to put food in normal box." }
                    },
                this);
        }
    }
}