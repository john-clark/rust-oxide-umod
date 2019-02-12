using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Tracker", "Maurice", "1.0.3", ResourceId = 1278)]
    [Description("Check the amount of an Item on the Map and where it is stored in")]
    class Tracker : RustPlugin
    {
        #region Variables

        private bool Changed;

        private int neededAuthLevel;

        #endregion

        #region ServerInitialization

        private void OnServerInitialized()
        {
            LoadVariables();
        }

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CONTAINER_FOUND"] = "{0} has {1} at position {2}",
                ["FOUND"] = "{0}'s found",
                ["INVALID_MINIMUM_AMOUNT"] = "Invalid minimum amount! Try again!",
                ["INVALID_NAME"] = "Invalid Name! Try again!",
                ["ITEM_FOUND"] = "There are {0} of '{1}' in game!",
                ["ITEM_NOT_FOUND"] = "Looks like there is no such item in game! (No player or chest has it)",
                ["MULTIPLE_PLAYERS_FOUND"] = "Multiple players found with that name! Try again or use SteamID",
                ["NO_CONTAINERS"] = "There are no containers in game.",
                ["NO_PERMISSION"] = "You don't have permission to use this command.",
                ["NO_SUCH_ITEM"] = "Looks like there is no such item named '{0}'.",
                ["PLAYER_NOT_FOUND"] = "No player found with that name! Check if you've written it correctly!",
                ["PLAYER_FOUND_ITEM"] = "{0} ({1}) has {2} of {3}",
                ["SAVED_TO"] = "Saved Item List to {0}",
                ["STEAMID_NOT_FOUND"] = "No player found with that SteamID! Check if you've written it correctly!",
                ["USAGE"] = "Usage:",
                ["USAGE_COMMAND_LOGITEM"] = " logitem item",
                ["USAGE_COMMAND_LOGPLAYERITEM"] = " logplayeritem item",
                ["USAGE_COMMAND_TRACKCOUNT"] = " trackitemcount item -- Shows the amount of certain items in game.",
                ["USAGE_COMMAND_TRACKITEM"] = " trackitem item optional:place optional:minamount",
                ["USAGE_COMMAND_TRACKITEM_ADDITIONALS"] = " Places: chests / players / all (Default: all)",
                ["WRONG_SYNTAX"] = "Wrong Syntax! Try again!"

            }, this);
        }

        #endregion

        #region TrackedItem

        public class TrackedItem
        {
            public String container_name;
            public String location;
            public Int64 count;
        }

        public class PlayerItem
        {
            public ulong steamid;
            public Int64 count;
        }

        #endregion

        #region Commands

        /// <summary>
        /// Displays total item count ingame
        /// </summary>
        /// <param name="arg">ConsoleSystem Arguments</param>
        [ConsoleCommand("trackitemcount")]
        void cmdTrackItemCount(ConsoleSystem.Arg arg)
        {
            if (!CheckAccess(arg))
                return;

            if (!arg.HasArgs())
            {
                SendReply(arg, lang.GetMessage("USAGE", this));
                SendReply(arg, lang.GetMessage("USAGE_COMMAND_TRACKCOUNT", this));
                return;
            }

            var item = arg.Args.Length > 0 ? arg.Args[0].ToLower() : "";

            if (!itemExist(item))
            {
                SendReply(arg, string.Format(lang.GetMessage("NO_SUCH_ITEM", this), item));
                return;
            }

            List<TrackedItem> items_list = new List<TrackedItem>();

            items_list.AddRange(findChestItems(item, 0, arg));
            items_list.AddRange(findPlayerItems(item, 0));

            Int64 count = 0;

            foreach (TrackedItem t in items_list)
            {
                if (t != null)
                    count += t.count;
            }

            SendReply(arg, string.Format(lang.GetMessage("ITEM_FOUND", this), String.Format("{0:N0}", count), item));
        }

        /// <summary>
        /// Displays a list of all containers/players that have the certain item
        /// </summary>
        /// <param name="arg">ConsoleSystem Arguments</param>
        [ConsoleCommand("trackitem")]
        void cmdTrackItem(ConsoleSystem.Arg arg)
        {
            if (!CheckAccess(arg))
                return;

            if (!arg.HasArgs())
            {
                SendReply(arg, lang.GetMessage("USAGE", this));
                SendReply(arg, lang.GetMessage("USAGE_COMMAND_TRACKITEM", this));
                SendReply(arg, lang.GetMessage("USAGE_COMMAND_TRACKITEM_ADDITIONALS", this));
                return;
            }

            var item = arg.Args.Length > 0 ? arg.Args[0].ToLower() : "";
            var location = arg.Args.Length > 1 ? arg.Args[1].ToLower() : "all";
            int min_amount = 0;
            try
            {
                min_amount = arg.Args.Length > 2 ? int.Parse(arg.Args[2]) : 0;
            }
            catch (Exception e)
            {
                SendReply(arg, lang.GetMessage("INVALID_MINIMUM_AMOUNT", this));
                return;
            }

            if (item.Length == 0 || location.Length == 0 || min_amount < 0 || (!location.Equals("all") && !location.Equals("players") && !location.Equals("chests")))
            {
                SendReply(arg, lang.GetMessage("WRONG_SYNTAX", this));
                return;
            }

            if (!itemExist(item))
            {
                SendReply(arg, string.Format(lang.GetMessage("NO_SUCH_ITEM", this), item));
                return;
            }

            List<TrackedItem> items_list = new List<TrackedItem>();

            if (location.Equals("all"))
            {
                items_list.AddRange(findChestItems(item, min_amount, arg));
                items_list.AddRange(findPlayerItems(item, min_amount));
            }
            else if (location.Equals("chests"))
                items_list.AddRange(findChestItems(item, min_amount, arg));
            else
                items_list.AddRange(findPlayerItems(item, min_amount));

            if (items_list.ToArray().Length == 0)
            {
                SendReply(arg, lang.GetMessage("ITEM_NOT_FOUND", this));
                return;
            }

            SendReply(arg, string.Format(lang.GetMessage("FOUND", this), item));

            for (int i = 0; i < items_list.Count; i++)
            {
                TrackedItem t;
                t = items_list[i];
                if (t != null)
                {
                    SendReply(arg, string.Format(lang.GetMessage("CONTAINER_FOUND", this), t.container_name, t.count, t.location));
                }
            }
        }

        [ConsoleCommand("logplayeritem")]
        void cmdLogPlayerItem(ConsoleSystem.Arg arg)
        {
            if (!CheckAccess(arg))
                return;

            if(!arg.HasArgs())
            {
                SendReply(arg, lang.GetMessage("USAGE", this));
                SendReply(arg, lang.GetMessage("USAGE_COMMAND_LOGPLAYERITEM", this));
                return;
            }

            var item = arg.Args.Length > 0 ? arg.Args[0].ToLower() : "";

            if(!itemExist(item))
            {
                SendReply(arg, string.Format(lang.GetMessage("NO_SUCH_ITEM", this), item));
                return;
            }

            List<PlayerItem> items_list = new List<PlayerItem>();

            items_list.AddRange(findChestItem(item, 0, arg));
            items_list.AddRange(findPlayerItem(item, 0));

            SendReply(arg, string.Format(lang.GetMessage("SAVED_TO", this), "oxide/logs"));
            LogToFile("player", $"Logged Item: {item}", this);

            var newList = items_list.OrderByDescending(x => x.count)
                  .ThenBy(x => x.steamid)
                  .ToList();

            for (int i = 0; i < newList.Count; i++)
            {
                PlayerItem t;
                t = newList[i];
                if (t != null)
                {
                    LogToFile("player", string.Format(lang.GetMessage("PLAYER_FOUND_ITEM", this), FindPlayerByID(t.steamid), t.steamid, t.count, item), this);
                }
            }
        }

        [ConsoleCommand("logitem")]
        void cmdLogItem(ConsoleSystem.Arg arg)
        {

            if (!CheckAccess(arg))
                return;

            if (!arg.HasArgs())
            {
                SendReply(arg, lang.GetMessage("USAGE", this));
                SendReply(arg, lang.GetMessage("USAGE_COMMAND_LOGITEM", this));
                return;
            }

            var item = arg.Args.Length > 0 ? arg.Args[0].ToLower() : "";

            if (!itemExist(item))
            {
                SendReply(arg, string.Format(lang.GetMessage("NO_SUCH_ITEM", this), item));
                return;
            }

            List<TrackedItem> items_list = new List<TrackedItem>();

            items_list.AddRange(findChestItems(item, 0, arg));
            items_list.AddRange(findPlayerItems(item, 0));

            SendReply(arg, string.Format(lang.GetMessage("SAVED_TO", this), "oxide/logs"));
            LogToFile("item", $"Logged Item: {item}", this);

            for (int i = 0; i < items_list.Count; i++)
            {
                TrackedItem t;
                t = items_list[i];
                if (t != null)
                {
                    LogToFile("item", string.Format(lang.GetMessage("CONTAINER_FOUND", this), t.container_name, t.count, t.location), this);
                }
            }
        }

        #endregion

        #region Methods

        #region ItemFilters

        /// <summary>
        /// Returns a list of all players that have the item
        /// </summary>
        /// <param name="item">Item to be searched for</param>
        /// <param name="min_amount">Minimum amount searched for</param>
        /// <returns></returns>
        List<PlayerItem> findPlayerItem(String item, int min_amount)
        {
            var players = UnityEngine.Object.FindObjectsOfType<BasePlayer>();
            var items_found = new List<PlayerItem>();

            if (players.Length == 0)
            {
                return items_found;
            }
            List<Item> items = new List<Item>();
            PlayerItem t;

            foreach (BasePlayer player in players)
            {
                if (player != null)
                {
                    items.Clear();
                    items.AddRange(player.inventory.containerMain.itemList);
                    items.AddRange(player.inventory.containerBelt.itemList);
                    items.AddRange(player.inventory.containerWear.itemList);
                    t = new PlayerItem();

                    if (items.ToArray().Length > 0)
                    {
                        foreach (Item i in items)
                        {
                            if (i != null && i.info.displayName.english.ToLower().Equals(item.ToLower()))
                            {
                                t.steamid = player.userID;
                                t.count += i.amount;
                            }
                        }
                    }

                    if (t != null && t.count > 0 && t.count >= min_amount)
                    {
                        items_found.Add(t);
                    }
                }
            }

            return items_found;
        }

        /// <summary>
        /// Returns a list of all players that have the item
        /// </summary>
        /// <param name="item">Item searched for</param>
        /// <param name="min_amount">Minimum amount searched for</param>
        /// <returns></returns>
        List<TrackedItem> findPlayerItems(String item, int min_amount)
        {
            var players = UnityEngine.Object.FindObjectsOfType<BasePlayer>();
            var items_found = new List<TrackedItem>();

            if (players.Length == 0)
            {
                return items_found;
            }
            List<Item> items = new List<Item>();
            TrackedItem t;

            foreach (BasePlayer player in players)
            {
                if (player != null)
                {
                    items.Clear();
                    items.AddRange(player.inventory.containerMain.itemList);
                    items.AddRange(player.inventory.containerBelt.itemList);
                    items.AddRange(player.inventory.containerWear.itemList);
                    t = new TrackedItem();

                    if (items.ToArray().Length > 0)
                    {
                        foreach (Item i in items)
                        {
                            if (i != null && i.info.displayName.english.ToLower().Equals(item.ToLower()))
                            {

                                t.container_name = player.displayName + " (" + player.userID.ToString() + ") " + "" + (player.IsSleeping() ? "(Sleeping)" : "(Online)");
                                t.location = (int)player.transform.position.x + " " + (int)player.transform.position.y + " " + (int)player.transform.position.z;
                                t.count += i.amount;
                            }
                        }
                    }

                    if (t != null && t.count > 0 && t.count >= min_amount)
                    {
                        items_found.Add(t);
                    }
                }
            }

            return items_found;
        }

        /// <summary>
        /// Returns a list of Chests that have the item
        /// </summary>
        /// <param name="item">Item that is searched for</param>
        /// <param name="min_amount">Minimum amount searched for</param>
        /// <returns></returns>
        List<PlayerItem> findChestItem(String item, int min_amount, ConsoleSystem.Arg arg)
        {
            var containers = UnityEngine.Object.FindObjectsOfType<StorageContainer>();
            var items_found = new List<PlayerItem>();

            if (containers.Length == 0)
            {
                SendReply(arg, lang.GetMessage("NO_CONTAINERS", this));
                return items_found;
            }

            PlayerItem t;

            foreach (StorageContainer container in containers)
            {
                if (container != null && container.inventory != null)
                {
                    t = new PlayerItem();

                    if (container.inventory.itemList.ToArray().Length > 0)
                    {
                        foreach (Item i in container.inventory.itemList)
                        {
                            if (i != null && i.info.displayName.english.ToLower().Equals(item.ToLower()))
                            {
                                t.steamid = container.OwnerID;
                                t.count += i.amount;
                            }
                        }
                    }

                    if (t != null && t.count > 0 && t.count >= min_amount)
                    {
                        items_found.Add(t);
                    }
                }
            }

            return items_found;
        }

        /// <summary>
        /// Returns a list of Chests that have the item
        /// </summary>
        /// <param name="item">Item that is searched for</param>
        /// <param name="min_amount">Minimum amount searched for</param>
        /// <returns></returns>
        List<TrackedItem> findChestItems(String item, int min_amount, ConsoleSystem.Arg arg)
        {
            var containers = UnityEngine.Object.FindObjectsOfType<StorageContainer>();
            var items_found = new List<TrackedItem>();

            if (containers.Length == 0)
            {
                SendReply(arg, lang.GetMessage("NO_CONTAINERS", this));
                return items_found;
            }

            TrackedItem t;

            foreach (StorageContainer container in containers)
            {
                if (container != null && container.inventory != null)
                {
                    t = new TrackedItem();

                    if (container.inventory.itemList.ToArray().Length > 0)
                    {
                        foreach (Item i in container.inventory.itemList)
                        {
                            if (i != null && i.info.displayName.english.ToLower().Equals(item.ToLower()))
                            {
                                t.container_name = container.ShortPrefabName + " (Owner: " + container.OwnerID.ToString() + ")";
                                t.location = (int)container.transform.position.x + " " + (int)container.transform.position.y + " " + (int)container.transform.position.z;
                                t.count += i.amount;
                            }
                        }
                    }

                    if (t != null && t.count > 0 && t.count >= min_amount)
                    {
                        items_found.Add(t);
                    }
                }
            }

            return items_found;
        }

        #endregion


        /// <summary>
        /// Checks is item exists
        /// </summary>
        /// <param name="item">Item Name given via command</param>
        /// <returns></returns>
        bool itemExist(String item)
        {
            foreach (ItemDefinition i in ItemManager.GetItemDefinitions())
            {
                if (i != null && i.displayName.english.ToLower().Equals(item))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if player has access
        /// </summary>
        /// <param name="arg">ConsoleSystem Arguments</param>
        /// <returns></returns>
        bool CheckAccess(ConsoleSystem.Arg arg)
        {
            if (arg != null && arg.Connection == null || arg.Player() != null && (arg.Player().IsAdmin || arg.Connection.authLevel > neededAuthLevel))
                return true;
            SendReply(arg, lang.GetMessage("NO_PERMISSION", this));
            return false;
        }

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
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        /// <summary>
        /// Finds player by SteamID
        /// </summary>
        /// <param name="steamid">SteamID of the player to be searched</param>
        /// <returns></returns>
        object FindPlayerByID(ulong steamid)
        {
            BasePlayer targetplayer = BasePlayer.FindByID(steamid);
            if (targetplayer != null)
            {
                return targetplayer;
            }
            targetplayer = BasePlayer.FindSleeping(steamid);
            if (targetplayer != null)
            {
                return targetplayer;
            }
            return null;
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        void LoadVariables()
        {
            neededAuthLevel = Convert.ToInt32(GetConfig("Generic", "Needed Auth Level", 1));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        #endregion
    }
}
