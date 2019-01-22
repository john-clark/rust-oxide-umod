using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Configuration;

namespace Oxide.Plugins
{

    [Info("Gather bonuses", "wazzzup", "0.0.1")]
    class GatherBonuses : RustPlugin
    {

        #region config

        private DynamicConfigFile config;
        Dictionary<string,int> rates = new Dictionary<string,int>();
        private bool Changed;

        void LoadVariables()
        {
            rates["High Quality Metal Ore"] = Convert.ToInt32(GetConfig("Rates", "High Quality Metal Ore", 1));
            rates["Sulfur Ore"] = Convert.ToInt32(GetConfig("Rates", "Sulfur Ore", 1));
            rates["Metal Ore"] = Convert.ToInt32(GetConfig("Rates", "Metal Ore", 1));
            rates["Stones"] = Convert.ToInt32(GetConfig("Rates", "Stones", 1));

            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Config.Clear();
            LoadVariables();
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

        #endregion

        #region hooks

        void Loaded()
        {
            permission.RegisterPermission("gatherbonuses.magic", this);
            LoadVariables();
            config = Interface.Oxide.DataFileSystem.GetFile(Name);
        }

        private object OnDispenserBonus(ResourceDispenser resource, BasePlayer player, Item obj1)
        {
            obj1.amount = obj1.amount * rates[obj1.info.displayName.english];
            if (Interface.CallHook("IsMagicTool", player.GetActiveItem()) != null || permission.UserHasPermission(player.UserIDString, "gatherbonuses.magic"))
            {
                switch (obj1.info.shortname)
                {
                    case "sulfur.ore":
                        obj1.info = ItemManager.FindItemDefinition(-891243783);
                        break;
                    case "hq.metal.ore":
                        obj1.info = ItemManager.FindItemDefinition(374890416);
                        break;
                    case "metal.ore":
                        obj1.info = ItemManager.FindItemDefinition(688032252);
                        break;
                }
            }
            return obj1;
        }

        #endregion

    }
}