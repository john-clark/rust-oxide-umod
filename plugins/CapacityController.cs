using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CapacityController", "redBDGR", "1.0.1")]
    [Description("Allows you to modify the sizes of certain containers")]

    class CapacityController : RustPlugin
    {
        bool Changed = false;

        static Dictionary<string, object> EntityContainerTypes()
        {
            var x = new Dictionary<string, object>();
            x.Add("box.wooden.large", 30);
            x.Add("campfire", 5);
            x.Add("fridge.deployed", 30);
            x.Add("furnace", 6);
            x.Add("furnace.large", 18);
            x.Add("locker.deployed", 38);
            x.Add("refinery_small_deployed", 6);
            x.Add("small_stash_deployed", 6);
            x.Add("stocking_small_deployed", 6);
            x.Add("stokcking_large_deployed", 6);
            x.Add("survivalfishtrap.deployed", 6);
            x.Add("vendingmachine.deployed", 30);
            x.Add("woodbox_deployed", 12);
            x.Add("autoturret_deployed", 12);
            return x;
        }

        static Dictionary<string, object> AttachmentContainerTypes()
        {
            var x = new Dictionary<string, object>();
            x.Add("rifle.ak", 3);
            x.Add("rifle.bolt", 3);
            x.Add("rifle.lr300", 3);
            x.Add("rifle.semiauto", 3);
            x.Add("crossbow", 2);
            x.Add("smg.2", 3);
            x.Add("smg.mp5", 3);
            x.Add("smg.thompson", 2);
            x.Add("shotgun.double", 2);
            x.Add("shotgun.pump", 2);
            x.Add("shotgun.waterpipe", 2);
            x.Add("lmg.m249", 3);
            x.Add("pistol.m92", 3);
            x.Add("pistol.python", 3);
            x.Add("pistol.revolver", 1);
            x.Add("pistol.semiauto", 3);
            return x;
        }

        List<BasePlayer> cooldown = new List<BasePlayer>();
        Dictionary<string, object> entitySizeInfo;
        Dictionary<string, object> attachmentSizeInfo;
        Dictionary<string, int> defaultAttachments = new Dictionary<string, int>()
        {
            {"rifle.ak", 3 },       {"rifle.bolt", 3 },     {"rifle.lr300", 3 },        {"rifle.semiauto", 3 },
            {"crossbow", 2 },       {"smg.2", 3 },          {"smg.mp5", 3 },            {"smg.thompson", 2 },
            {"shotgun.double", 2 }, {"shotgun.pump", 2 },   {"shotgun.waterpipe", 2 },  {"lmg.m249", 3 },
            {"pistol.m92", 3 },     {"pistol.python", 3 },  {"pistol.revolver", 1 },    {"pistol.semiauto", 3 }
        };

        private const string permissionName = "capacitycontroller.exempt";

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        void LoadVariables()
        {
            entitySizeInfo = (Dictionary<string, object>)GetConfig("Settings", "Deployables", EntityContainerTypes());
            attachmentSizeInfo = (Dictionary<string, object>)GetConfig("Settings", "Attachments", AttachmentContainerTypes());

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        void Init()
        {
            LoadVariables();
            permission.RegisterPermission(permissionName, this);
        }

        void OnEntitySpawned(BaseNetworkable _entity)
        {
            if (!(_entity is BaseEntity)) return;
            BaseEntity entity = _entity as BaseEntity;
            if (entity == null) return;
            BasePlayer player = FindPlayer(entity.OwnerID.ToString());
            if (!player) return;
            if (permission.UserHasPermission(player.UserIDString, permissionName))
                return;
            if (!entitySizeInfo.ContainsKey(entity.ShortPrefabName)) return;
            var e = entity.GetComponent<StorageContainer>();
            if (e == null) return;
            e.inventory.capacity = Convert.ToInt32(entitySizeInfo[entity.ShortPrefabName]);
            entity.SendNetworkUpdateImmediate();
        }

        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (!item.GetOwnerPlayer()) return;
            BasePlayer player = item.GetOwnerPlayer();
            var x = item.contents;
            if (x == null) return;
            if (attachmentSizeInfo.ContainsKey(item.info.shortname))
            {
                if (permission.UserHasPermission(player.UserIDString, permissionName))
                {
                    x.capacity = defaultAttachments[item.info.shortname];
                    return;
                }
                x.capacity = Convert.ToInt32(attachmentSizeInfo[item.info.shortname]);
            }
        }

        object GetConfig(string menu, string datavalue, object defaultValue)
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
    }
}