using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("InstantSmelt", "redBDGR", "2.0.3")]
    [Description("Smelt resources as soon as they are mined")]

    class InstantSmelt : RustPlugin
    {
        private bool Changed;
        private const string permissionNameUSE = "instantsmelt.use";
        List<object> itemBlacklist = new List<object>();

        private static List<object> GetDefaultConfigValues()
        {
            List<object> x = new List<object>();
            x.Add("can.tuna.empty");
            x.Add("can.beans.empty");
            return x;
        }

        private void Init()
        {
            LoadVariables();
            permission.RegisterPermission(permissionNameUSE, this);
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {
            itemBlacklist = (List<object>)GetConfig("Settings", "Item Blacklist", GetDefaultConfigValues());

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (!item.info.shortname.Contains(".empty") && item.info.shortname != "crude.oil")
                return;
            BasePlayer player = container.GetOwnerPlayer();
            if (!OnGather(item, player))
                return;
            item.Remove();
        }

        private object OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            BasePlayer player = entity.GetComponent<BasePlayer>();
            if (!OnGather(item, player))
                return null;
            item.Remove();
            return false;
        }

        private object OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (!OnGather(item, player))
                return null;
            item.Remove();
            return false;
        }

        private object OnCollectiblePickup(Item item, BasePlayer player)
        {
            if (!OnGather(item, player))
                return null;
            item.Remove();
            return null;
        }

        private bool OnGather(Item item, BasePlayer player)
        {
            if (!player)
                return false;
            if (!permission.UserHasPermission(player.UserIDString, permissionNameUSE))
                return false;
            ItemModCookable cookable = item.info.GetComponent<ItemModCookable>();
            if (!cookable)
                return false;
            if (itemBlacklist.Contains(item.info.shortname))
                return false;
            Item newItem = ItemManager.CreateByName(cookable.becomeOnCooked.shortname, cookable.amountOfBecome * item.amount);
            if (newItem == null)
                return false;
            player.GiveItem(newItem, BaseEntity.GiveItemReason.ResourceHarvested);
            return true;
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
            if (data.TryGetValue(datavalue, out value)) return value;
            value = defaultValue;
            data[datavalue] = value;
            Changed = true;
            return value;
        }
    }
}