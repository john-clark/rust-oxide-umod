using System.Collections.Generic;
using System;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AdvanceGather", "Hougan", "1.0.0")]
    [Description("Custom gathering with some action's and extension drop")]
    class AdvanceGather : RustPlugin
    {
        #region Variable
        private int appleChance;
        private int rAppleChance;
        private int berryChance;
        private int berryChancePlanted;
        private int berryAmountMax;
        private int berryAmountMin;
        private string berryItem;

        private int BiofuelChanceCorn;
        private int BiofuelChancePlantedCorn;
        private int BiofuelAmountMaxCorn;
        private int BiofuelAmountMinCorn;
        private string BiofuelItemCorn;

        private int BiofuelChancePumpkin;
        private int BiofuelChancePlantedPumpkin;
        private int BiofuelAmountMaxPumpkin;
        private int BiofuelAmountMinPumpkin;
        private string BiofuelItemPumpkin;

        private bool enableBroadcast;
        #endregion

        #region Function
        object GetVariable(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
            }
            return value;
        }
        string msg(string key, string id = null) => lang.GetMessage(key, this, id);
        #endregion

        #region Hooks
        protected override void LoadDefaultConfig()
        {
            enableBroadcast = Convert.ToBoolean(GetVariable("Option", "Enable broadcast", true));
            rAppleChance = Convert.ToInt32(GetVariable("Get apple from Tree", "Chance to drop rotten apple (Chance depend on chance)", 30));
            appleChance = Convert.ToInt32(GetVariable("Get apple from Tree", "Chance to drop any apples per hit", 3));

            berryItem = Convert.ToString(GetVariable("Get blueberries from Hemp", "berry Item", "blueberries"));
            berryChance = Convert.ToInt32(GetVariable("Get blueberries from Hemp", "Chance to get berry from hemp", 10));
            berryChancePlanted = Convert.ToInt32(GetVariable("Get blueberries from Hemp", "Chance to get berry from planted hemp", 80));
            berryAmountMax = Convert.ToInt32(GetVariable("Get blueberries from Hemp", "Max amount", 2));
            berryAmountMin = Convert.ToInt32(GetVariable("Get blueberries from Hemp", "Min amount", 1));

            BiofuelItemCorn = Convert.ToString(GetVariable("Get biofuels from Corn", "biofuel Item", "lowgradefuel"));
            BiofuelChanceCorn = Convert.ToInt32(GetVariable("Get biofuels from Corn", "Chance to get biofuel from corn", 8));
            BiofuelChancePlantedCorn = Convert.ToInt32(GetVariable("Get biofuels from Corn", "Chance to get biofuel from planted corn", 65));
            BiofuelAmountMaxCorn = Convert.ToInt32(GetVariable("Get biofuels from Corn", "Max amount", 3));
            BiofuelAmountMinCorn = Convert.ToInt32(GetVariable("Get biofuels from Corn", "Min amount", 2));

            BiofuelItemPumpkin = Convert.ToString(GetVariable("Get biofuels from Pumpkin", "biofuel Item", "lowgradefuel"));
            BiofuelChancePumpkin = Convert.ToInt32(GetVariable("Get biofuels from Pumpkin", "Chance to get biofuel from pumpkin", 5));
            BiofuelChancePlantedPumpkin = Convert.ToInt32(GetVariable("Get biofuels from Pumpkin", "Chance to get biofuel from planted pumpkin", 50));
            BiofuelAmountMaxPumpkin = Convert.ToInt32(GetVariable("Get biofuels from Pumpkin", "Max amount", 8));
            BiofuelAmountMinPumpkin = Convert.ToInt32(GetVariable("Get biofuels from Pumpkin", "Min amount", 4));


            SaveConfig();
        }
        void Init()
        {
            LoadDefaultConfig();
            ItemManager.FindItemDefinition(berryItem).stackable = 1000000;
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["Apple"] = "<color=#66FF33>Congratulations!</color> You got <color=#66FF33>apple</color> from tree !",
                ["Berry"] = "<color=#66FF33>Congratulations!</color> You got <color=#66FF33>berry</color> from hemp !",
                ["BiofuelCorn"] = "<color=#66FF33>Congratulations!</color> You got <color=#66FF33>biofuel</color> from corn !",
                ["BiofuelPumpkin"] = "<color=#66FF33>Congratulations!</color> You got <color=#66FF33>biofuel</color> from corn !"
            }, this);
        }
	
	//Get apple from Tree
        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (dispenser.GetComponent<BaseEntity>() is TreeEntity)
            {
                if (Oxide.Core.Random.Range(0, 100) < appleChance)
                {
                    if (Oxide.Core.Random.Range(0, 100) < rAppleChance)
                        ItemManager.CreateByName("apple.spoiled", 1).Drop(new Vector3(entity.transform.position.x, entity.transform.position.y + 20f, entity.transform.position.z), Vector3.zero);
                    else
                        ItemManager.CreateByName("apple", 1).Drop(new Vector3(entity.transform.position.x, entity.transform.position.y + 20f, entity.transform.position.z), Vector3.zero);
                    if (enableBroadcast)
                        SendReply(entity as BasePlayer, String.Format(msg("Apple")));
                }
            }
        }
        
	//Natural plants
        void OnCollectiblePickup(Item item, BasePlayer player)
        {
  	    //Get berry from hemp
            if (item.info.displayName.english.Contains("Cloth"))
            {
                if (Oxide.Core.Random.Range(0, 100) < berryChance)
                {
                    player.inventory.GiveItem(ItemManager.CreateByName(berryItem, Oxide.Core.Random.Range(berryAmountMin, berryAmountMax + 1)));
                    if (enableBroadcast)
                        SendReply(player, String.Format(msg("Berry")));
                }
            }

	    //Get biofuel from corn
            if (item.info.displayName.english.Contains("Corn") && !item.info.displayName.english.Contains("Corn Seed"))
            {
                if (Oxide.Core.Random.Range(0, 100) < BiofuelChanceCorn)
                {
                    player.inventory.GiveItem(ItemManager.CreateByName(BiofuelItemCorn, Oxide.Core.Random.Range(BiofuelAmountMinCorn, BiofuelAmountMaxCorn + 1)));
                    if (enableBroadcast)
                        SendReply(player, String.Format(msg("BiofuelCorn")));
                }
            }

	    //Get biofuel from pumpkin
            if (item.info.displayName.english.Contains("Pumpkin") && !item.info.displayName.english.Contains("Pumpkin Seed"))
            {
                if (Oxide.Core.Random.Range(0, 100) < BiofuelChancePumpkin)
                {
                    player.inventory.GiveItem(ItemManager.CreateByName(BiofuelItemPumpkin, Oxide.Core.Random.Range(BiofuelAmountMinPumpkin, BiofuelAmountMaxPumpkin + 1)));
                    if (enableBroadcast)
                        SendReply(player, String.Format(msg("BiofuelPumpkin")));
                }
            }
        }

	//Planted plants
        void OnCropGather(PlantEntity plant, Item item, BasePlayer player)
        {
	    //Get berry from planted hemp
            if (item.info.displayName.english.Contains("Cloth"))
            {
                if (Oxide.Core.Random.Range(0, 100) < berryChancePlanted)
                {
                    player.inventory.GiveItem(ItemManager.CreateByName(berryItem, Oxide.Core.Random.Range(berryAmountMin, berryAmountMax + 1)));
                    if (enableBroadcast)
                        SendReply(player, String.Format(msg("Berry")));
                }
            }

	    //Get biofuel from planted corn
            if (item.info.displayName.english.Contains("Corn") && !item.info.displayName.english.Contains("Corn Seed"))
            {
                if (Oxide.Core.Random.Range(0, 100) < BiofuelChancePlantedCorn)
                {
                    player.inventory.GiveItem(ItemManager.CreateByName(BiofuelItemCorn, Oxide.Core.Random.Range(BiofuelAmountMinCorn, BiofuelAmountMaxCorn + 1)));
                    if (enableBroadcast)
                        SendReply(player, String.Format(msg("BiofuelCorn")));
                }
            }

	    //Get biofuel from planted pumpkin
            if (item.info.displayName.english.Contains("Pumpkin") && !item.info.displayName.english.Contains("Pumpkin Seed"))
            {
                if (Oxide.Core.Random.Range(0, 100) < BiofuelChancePlantedPumpkin)
                {
                    player.inventory.GiveItem(ItemManager.CreateByName(BiofuelItemPumpkin, Oxide.Core.Random.Range(BiofuelAmountMinPumpkin, BiofuelAmountMaxPumpkin + 1)));
                    if (enableBroadcast)
                        SendReply(player, String.Format(msg("BiofuelPumpkin")));
                }
            }
        }

        #endregion
    }
}