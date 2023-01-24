using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Oxide.Core.Configuration;
using UnityEngine;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("RecycleManager", "redBDGR", "1.0.15")]
    [Description("Easily change features about the recycler")]

    class RecycleManager : RustPlugin
    {
        private bool changed;

        #region Data

        private DynamicConfigFile OutputData;
        private StoredData storedData;

        private void SaveData()
        {
            storedData.table = ingredientList;
            OutputData.WriteObject(storedData);
        }

        private void LoadData()
        {
            try
            {
                storedData = OutputData.ReadObject<StoredData>();
                ingredientList = storedData.table;
            }
            catch
            {
                Puts("Failed to load data, creating new file");
                storedData = new StoredData();
            }
        }

        private class StoredData
        {
            public Dictionary<string, List<ItemInfo>> table = new Dictionary<string, List<ItemInfo>>();
        }

        #endregion

        private class ItemInfo
        {
            public string itemName;
            public int itemAmount;
        }

        private class IngredientInfo
        {
            public List<ItemInfo> items;
        }

        public float recycleTime = 5.0f;
        private const string permissionNameADMIN = "recyclemanager.admin";
        private const string permissionNameCREATE = "recyclemanager.create";
        private int maxItemsPerRecycle = 100;

        private static Dictionary<string, object> Multipliers()
        {
            var at = new Dictionary<string, object> {{"*", 1}, {"metal.refined", 1}};
            return at;
        }

        private static List<object> Blacklist()
        {
            var at = new List<object> {"hemp.seed"};
            return at;
        }

        private static List<object> OutputBlacklist()
        {
            var at = new List<object> {"hemp.seed"};
            return at;
        }

        private List<object> blacklistedItems;
        private List<object> outputBlacklistedItems;
        private Dictionary<string, object> multiplyList;
        private Dictionary<string, List<ItemInfo>> ingredientList = new Dictionary<string, List<ItemInfo>>();

        private void LoadVariables()
        {
            blacklistedItems = (List<object>)GetConfig("Lists", "Input Blacklist", Blacklist());
            recycleTime = Convert.ToSingle(GetConfig("Settings", "Recycle Time", 5.0f));
            multiplyList = (Dictionary<string, object>)GetConfig("Lists", "Recycle Output Multipliers", Multipliers());
            maxItemsPerRecycle = Convert.ToInt32(GetConfig("Settings", "Max Items Per Recycle", 100));
            outputBlacklistedItems = (List<object>)GetConfig("Lists", "Output Blacklist", OutputBlacklist());

            if (!changed) return;
            SaveConfig();
            changed = false;
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private void Init()
        {
            LoadVariables();
            permission.RegisterPermission(permissionNameADMIN, this);
            permission.RegisterPermission(permissionNameCREATE, this);
            OutputData = Interface.Oxide.DataFileSystem.GetFile("RecycleManager");
            LoadData();

            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["No Permissions"] = "You cannot use this command!",
                ["addrecycler CONSOLE invalid syntax"] = "Invalid syntax! addrecycler <playername/id>",
                ["No Player Found"] = "No player was found or they are offline",
                ["AddRecycler CONSOLE success"] = "A recycler was successfully placed at the players location!",
                ["AddRecycler CannotPlace"] = "You cannot place a recycler here",
                ["RemoveRecycler CHAT NoEntityFound"] = "There were no valid entities found",
                ["RemoveRecycler CHAT EntityWasRemoved"] = "The targeted entity was removed",

            }, this);
        }

        private void OnServerInitialized()
        {
            if (ingredientList.Count == 0)
                RefreshIngredientList();
        }

        [ChatCommand("addrecycler")]
        private void AddRecyclerCMD(BasePlayer player, string command, String[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionNameCREATE) && !permission.UserHasPermission(player.UserIDString, permissionNameADMIN))
            {
                player.ChatMessage(msg("No Permissions", player.UserIDString));
                return;
            }

            if (!player.IsBuildingAuthed())
            {
                if (!permission.UserHasPermission(player.UserIDString, permissionNameADMIN))
                {
                    player.ChatMessage(msg("AddRecycler CannotPlace", player.UserIDString));
                    return;
                }
            }

            BaseEntity ent = GameManager.server.CreateEntity("assets/bundled/prefabs/static/recycler_static.prefab", player.transform.position, player.GetNetworkRotation(), true);
            ent.Spawn();
            return;
        }

        [ConsoleCommand("recyclemanager.addrecycler")]
        private void AddRecyclerCMDConsole(ConsoleSystem.Arg arg)
        {
            if (arg?.Args == null)
            {
                arg.ReplyWith(msg("addrecycler CONSOLE invalid syntax"));
                return;
            }
            if (arg.Connection != null) return;
            if (arg.Args.Length != 1)
            {
                arg.ReplyWith(msg("addrecycler CONSOLE invalid syntax"));
                return;
            }
            BasePlayer target = FindPlayer(arg.Args[0]);
            if (target == null || !target.IsValid())
            {
                arg.ReplyWith(msg("No Player Found"));
                return;
            }
            BaseEntity ent = GameManager.server.CreateEntity("assets/bundled/prefabs/static/recycler_static.prefab", target.transform.position, target.GetNetworkRotation(), true);
            ent.Spawn();
            arg.ReplyWith(msg("AddRecycler CONSOLE success"));
        }

        [ChatCommand("removerecycler")]
        private void RemoveRecyclerCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionNameADMIN))
            {
                player.ChatMessage(msg("No Permissions", player.UserIDString));
                return;
            }
            RaycastHit hit;
            Physics.Raycast(player.eyes.HeadRay(), out hit);
            if (hit.GetEntity() == null)
            {
                player.ChatMessage(msg("RemoveRecycler CHAT NoEntityFound", player.UserIDString));
                return;
            }
            BaseEntity ent = hit.GetEntity();
            if (!ent.name.Contains("recycler"))
            {
                player.ChatMessage(msg("RemoveRecycler CHAT NoEntityFound", player.UserIDString));
                return;
            }
            ent.Kill();
            player.ChatMessage(msg("RemoveRecycler CHAT EntityWasRemoved", player.UserIDString));
        }

        [ConsoleCommand("recyclemanager.reloadingredientlist")]
        private void reloadDataCONSOLECMD(ConsoleSystem.Arg args)
        {
            if (args.Connection != null)
                return;
            OutputData = Interface.Oxide.DataFileSystem.GetFile("RecycleManager");
            LoadData();
            Puts("Recycler output list has successfully been updated!");
        }

        [ConsoleCommand("recyclemanager.updateingredientlist")]
        private void UpdateIngredientListCMD(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
                return;
            UpdateIngredientList();
            Puts("Recycler ingredients list has been updated!");
        }

        private void OnRecyclerToggle(Recycler recycler, BasePlayer player)
        {
            if (recycler.IsOn())
            {
                recycler.CancelInvoke("RecycleThink");
                return;
            }
            recycler.CancelInvoke("RecycleThink");
            timer.Once(0.1f, () => { recycler.Invoke("RecycleThink", recycleTime); });
        }

        private object CanRecycle(Recycler recycler, Item item)
        {
            bool stopRecycle = true;
            for (int i = 0; i < 6; i++)
            {
                Item slot = recycler.inventory.GetSlot(i);
                if (slot == null)
                    continue;

                if (!blacklistedItems.Contains(slot.info.shortname) && ingredientList.ContainsKey(slot.info.shortname))
                {
                    stopRecycle = false;
                    break;
                }
            }
            if (stopRecycle)
                return false;
            return true;
        }

        private object OnRecycleItem(Recycler recycler, Item item)
        {
            if (!ingredientList.ContainsKey(item.info.shortname) || blacklistedItems.Contains(item.info.shortname))
            {
                item.Drop(recycler.transform.TransformPoint(new Vector3(-0.3f, 1.7f, 1f)), Vector3.up, new Quaternion());
                return false;
            }

            bool flag = false;
            int usedItems = 1;

            if (item.amount > 1)
                usedItems = item.amount;
            if (usedItems > maxItemsPerRecycle)
                usedItems = maxItemsPerRecycle;

            item.UseItem(usedItems);
            foreach (ItemInfo ingredient in ingredientList[item.info.shortname])
            {
                double multi = 1;
                if (multiplyList.ContainsKey("*"))
                    multi = Convert.ToDouble(multiplyList["*"]);
                if (multiplyList.ContainsKey(ingredient.itemName))
                    multi = Convert.ToDouble(multiplyList[ingredient.itemName]);
                int outputamount = Convert.ToInt32(usedItems * Convert.ToDouble(ingredient.itemAmount) * multi);
                if (outputamount < 1)
                    continue;
                if (!recycler.MoveItemToOutput(ItemManager.CreateByName(ingredient.itemName, outputamount)))
                    flag = true;
            }
            if (flag || !recycler.HasRecyclable())
            {
                recycler.StopRecycling();
                for (int i = 5; i <= 11; i++)
                {
                    Item _item = recycler.inventory.GetSlot(i);
                    if (_item == null) continue;
                    if (_item.IsValid())
                        if (outputBlacklistedItems.Contains(_item.info.shortname))
                        {
                            _item.Remove();
                            _item.RemoveFromContainer();
                        }
                }
            }
            return true;
        }

        private void RefreshIngredientList()
        {
            foreach (ItemDefinition itemInfo in ItemManager.itemList)
            {
                if (itemInfo.Blueprint == null)
                    continue;
                if (itemInfo.Blueprint.ingredients?.Count == 0)
                    continue;
                List<ItemInfo> x = itemInfo.Blueprint.ingredients?.Select(entry => new ItemInfo {itemAmount = (int) entry.amount / 2, itemName = entry.itemDef.shortname}).ToList();
                ingredientList.Add(itemInfo.shortname, x);
            }
            SaveData();
        }

        private void UpdateIngredientList()
        {
            foreach (ItemDefinition itemInfo in ItemManager.itemList)
            {
                if (itemInfo.Blueprint == null)
                    continue;
                if (itemInfo.Blueprint.ingredients?.Count == 0)
                    continue;
                if (ingredientList.ContainsKey(itemInfo.shortname))
                    continue;
                List<ItemInfo> x = itemInfo.Blueprint.ingredients?.Select(entry => new ItemInfo { itemAmount = (int)entry.amount / 2, itemName = entry.itemDef.shortname }).ToList();
                ingredientList.Add(itemInfo.shortname, x);
            }
            SaveData();
            LoadData();
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                changed = true;
            }
            object value;
            if (data.TryGetValue(datavalue, out value)) return value;
            value = defaultValue;
            data[datavalue] = value;
            changed = true;
            return value;
        }

        private static BasePlayer FindPlayer(string nameOrId)
        {
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString == nameOrId)
                    return activePlayer;
                if (activePlayer.displayName.Contains(nameOrId, CompareOptions.OrdinalIgnoreCase))
                    return activePlayer;
                if (activePlayer.net?.connection != null && activePlayer.net.connection.ipaddress == nameOrId)
                    return activePlayer;
            }
            return null;
        }

        private string msg(string key, string id = null) => lang.GetMessage(key, this, id);
    }
}