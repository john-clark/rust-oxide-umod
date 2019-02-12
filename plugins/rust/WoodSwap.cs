
using System.Collections.Generic;
using Rust;
using System;

namespace Oxide.Plugins
{

    [Info("WoodSwap", "Serenity", "0.1.0", ResourceId = 2367)]
    class WoodSwap : RustPlugin
    {
        #region Constants
        // Constant values (Non Variable Values) This is to ensure that nothing changes in the process of making these items. 
        const int Wood = 3655341;
        const int Charcoal = 1436001773;
        const string perm = "WoodSwap.Use";
        #endregion

        #region Init
        void Init()
        {
            permission.RegisterPermission(perm, this);
            //Lang
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "TooManyArguments", "Use /swapwood <amount> "},
                { "NotEnoughWood", "Not Enough Wood" },
            }, this);
        }
        #endregion

        #region Config
        int CharcoalWood;

        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new configuration file!");
            Config.Clear();
            LoadVariables();
        }

        void LoadVariables()
        {
            CharcoalWood = Convert.ToInt32(GetConfig("Values", "Charcoaltowood", 2));
            SaveConfig();
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
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
        #endregion

        #region Wood Swap
        [ChatCommand("swapwood")]
        void swapwoodCMD(BasePlayer player, string command, string[] args)
        {
            //Gets Amount of wood in the arg
            var amount = Convert.ToInt32(args[0]);
            //Gets Amount of wood in the inventory
            var charcwood = player.inventory.GetAmount(Wood);
            //Checks for the perms
            if (!permission.UserHasPermission(player.UserIDString, perm)) return;
            
            if (args.Length > 1)
            {
                player.ChatMessage(lang.GetMessage("TooManyArgs", this, player.UserIDString));
                return;
            }

            if (amount > charcwood)
            {
                player.ChatMessage(lang.GetMessage("NotEnoughWood", this, player.UserIDString));
                return;

            }

            else
            {
                player.inventory.Take(null, Wood, amount);
                player.inventory.GiveItem(ItemManager.CreateByItemID(Charcoal, amount * CharcoalWood), player.inventory.containerMain);
            }
        }
        #endregion
    }
}