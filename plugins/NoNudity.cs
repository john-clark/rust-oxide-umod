using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("NoNudity", "Absolut", "1.0.3", ResourceId = 2394)]

    class NoNudity : RustPlugin
    {
        void OnServerInitialized()
        {
            LoadVariables();
            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerInit(player);
        }
        void OnPlayerInit(BasePlayer player)
        {
            if (player.inventory.containerWear.itemList.Count() == 0) { GivePants(player); return; }
            Item valid = player.inventory.containerWear.itemList.Where(k => Pants.Contains(k.info.shortname)).Select(k => k).FirstOrDefault();
            if (valid == null) GivePants(player);
        }
        void OnPlayerRespawned(BasePlayer player)
        {
            GivePants(player);
        }
        void GivePants(BasePlayer player)
        {
            var definition = ItemManager.FindItemDefinition(configData.TypeOfPants_Shortname);
            if (definition == null) definition = ItemManager.FindItemDefinition("pants");
            Item newPants = ItemManager.Create(definition, 1, configData.PantsSkin);
            if (newPants == null) newPants = ItemManager.Create(definition, 1, 0);
            newPants.MoveToContainer(player.inventory.containerWear);
        }

        object CanMoveItem(Item item, PlayerInventory playerLoot, uint targetContainer, int targetSlot)
        {
            if (item == null || playerLoot == null) return null;
            if (playerLoot.containerWear.itemList.Contains(item) && Pants.Contains(item.info.shortname)) return false;
            Item valid = playerLoot.containerWear.itemList.Where(k => Pants.Contains(k.info.shortname)).Select(k => k).FirstOrDefault();
            if (Pants.Contains(item.info.shortname) && valid == null)
            {
                timer.Once(1, () => MovePants(item, playerLoot.containerWear));
                return false;
            }
            if (playerLoot.containerWear.uid == targetContainer && valid != null && !Pants.Contains(item.info.shortname) && targetSlot == valid.position) return false;
            return null;
        }

        void MovePants(Item item, ItemContainer cont)
        {
            item.MoveToContainer(cont);
        }


        object OnItemAction(Item item, string cmd)
        {
            if (item == null || item.parent == null || item.parent.playerOwner == null) return null;
            if (cmd == "drop" && item.parent.playerOwner.inventory.containerWear.itemList.Contains(item) && Pants.Contains(item.info.shortname)) return true;
            return null;
        }

        private List<string> Pants = new List<string>
        {
        {"pants"},
        {"pants.shorts"},
        {"hazmat.pants"},
        {"burlap.trousers"},
        {"attire.hide.pants"},
        {"attire.hide.skirt"},
        {"roadsign.kilt"},
        {"heavy.plate.pants"},
        {"wood.armor.pants"},
        {"hazmatsuit" },
        };

        private ConfigData configData;
        class ConfigData
        {
            public string TypeOfPants_Shortname { get; set; }
            public ulong PantsSkin { get; set; }
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                PantsSkin = 0,
                TypeOfPants_Shortname = "pants",
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);

    }
}