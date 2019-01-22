using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ReviveArrows", "Vliek/redBDGR", "1.0.1", ResourceId = 2824)]
    [Description("Easily revive people with arrows")]

    class ReviveArrows : RustPlugin
    {
        private List<string> activeList = new List<string>();
        private const string permissionName = "revivearrows.use";
        private bool Changed;

        #region Config
        // Config Vars
        private Dictionary<string, object> neededItems = new Dictionary<string, object>();

        private static Dictionary<string, object> NeededItems()
        {
            var al = new Dictionary<string, object> { { "syringe.medical", 1 } };
            return al;
        }

        private class ItemInfo
        {
            public string itemName;
            public int itemAmount;
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {
            neededItems = (Dictionary<string, object>)GetConfig("Settings", "Items Needed To Revive", NeededItems());

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        #endregion

        private void Init()
        {
            permission.RegisterPermission(permissionName, this);
            LoadVariables();

            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["No Permission"] = "You are not allowed to use this command",
                ["Disabled"] = "Revive arrows now disabled",
                ["Enabled"] = "Revive arrows now enabled",
            }, this);
        }

        private void OnPlayerAttack(BasePlayer player, HitInfo info)
        {
            if (player == null)
                return;
            if (!info.Weapon.ShortPrefabName.ToLower().Contains("bow"))
                return;
            if (!activeList.Contains(player.UserIDString))
                return;
            if (info.HitEntity == null)
                return;
            BasePlayer target = info.HitEntity.GetComponent<BasePlayer>();
            if (!target)
                return;
            if (!target.IsWounded())
                return;
            if (!HasItems(player))
                return;
            info.damageTypes.ScaleAll(0);
            target.StopWounded();
            TakeItems(player);
        }

        [ChatCommand("togglerevivearrows")]
        private void ToggleReviveArrowsCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName))
            {
                player.ChatMessage(msg("No Permission", player.UserIDString));
                return;
            }
            if (activeList.Contains(player.UserIDString))
            {
                player.ChatMessage(msg("Disabled", player.UserIDString));
                activeList.Remove(player.UserIDString);
            }
            else
            {
                player.ChatMessage(msg("Activated", player.UserIDString));
                activeList.Add(player.UserIDString);
            }
        }

        private bool HasItems(BasePlayer player)
        {
            List<ItemInfo> x = neededItems.Select(entry => new ItemInfo { itemAmount = (int)entry.Value, itemName = entry.Key }).ToList();
            foreach (Item item in player.inventory.AllItems())
                foreach (ItemInfo info in x.ToList())
                    if (item.info.shortname == info.itemName)
                        if (item.amount >= info.itemAmount)
                            x.Remove(info);
            return x.Count == 0;
        }

        private void TakeItems(BasePlayer player)
        {
            foreach (Item item in player.inventory.AllItems())
                foreach (ItemInfo info in neededItems.Select(entry => new ItemInfo { itemAmount = (int)entry.Value, itemName = entry.Key }).ToList())
                {
                    if (item.info.shortname != info.itemName) continue;
                    if (item.amount == info.itemAmount)
                    {
                        ItemManager.RemoveItem(item);
                        continue;
                    }
                    if (item.amount < info.itemAmount) continue;
                    item.amount -= info.itemAmount;
                }

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

        private string msg(string key, string id = null)
        {
            return lang.GetMessage(key, this, id);
        }
    }
}