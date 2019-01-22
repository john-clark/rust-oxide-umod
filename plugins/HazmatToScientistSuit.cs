using System;                   //config
using System.Collections.Generic;   //config


namespace Oxide.Plugins
{
	[Info("Hazmat To Scientist Suit", "BuzZ[PHOQUE]", "0.0.5")]
	[Description("Craft scientist blue or green peacekeeper suit instead of Hazmat for players with permission.")]

/*======================================================================================================================= 
*
*   
*   09th september 2018
* 
*
*	THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*   NO COMMAND NEEDED.
*   NO CHANGE IN THE CODE IS NEEDED : PLEASE SET YOUR CONFIG FILE.
*
*=======================================================================================================================*/


	public class HazmatToScientistSuit : RustPlugin
	{

        string Prefix = "[HTSS] ";                       // CHAT PLUGIN PREFIX
        string PrefixColor = "#555555";                 // CHAT PLUGIN PREFIX COLOR
        string ChatColor = "#999999";                   // CHAT MESSAGE COLOR
        ulong SteamIDIcon = 76561198859224394;          //  STEAMID created for this plugin 76561198859224394
        bool SuitGreenBool = true;
        private bool ConfigChanged;
        string suitname = "Admin Scientist Suit";
        bool loaded = false;

        const string PluginScientistAdminPermission = "hazmattoscientistsuit.use"; 

        protected override void LoadDefaultConfig()

        {
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {

            Prefix = Convert.ToString(GetConfig("Chat Settings", "Prefix", "[SuitAdmin] "));                       // CHAT PLUGIN PREFIX
            PrefixColor = Convert.ToString(GetConfig("Chat Settings", "PrefixColor", "#47ff6f"));                // CHAT PLUGIN PREFIX COLOR
            ChatColor = Convert.ToString(GetConfig("Chat Settings", "ChatColor", "#a0ffb5"));                    // CHAT  COLOR
            SteamIDIcon = Convert.ToUInt64(GetConfig("Chat Settings", "SteamIDIcon", 76561198857725741));        // SteamID FOR PLUGIN ICON - STEAM PROFILE CREATED FOR THIS PLUGIN / 76561198857725741 /
            SuitGreenBool = Convert.ToBoolean(GetConfig("Suit Color Peacekeeper", "Not Blue but Green suit", true));
            suitname = Convert.ToString(GetConfig("Suit Name", "Custom Name", "Admin Scientist Suit"));

            if (!ConfigChanged) return;
            SaveConfig();
            ConfigChanged = false;
        }

#region MESSAGES / LANG

        void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {

                {"TransMsg", "Your crafted Hazmat Suit received a transformation"},
                {"BackTransMsg", "It returned to a classic Hazmat Suit."},
                {"NoPermMsg", "You are not allowed to wear this Admin Suit."},

            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {

                {"TransMsg", "Votre tenue anti radioactivité a subie une transformation"},
                {"BackTransMsg", "La tenue est revenue à son type d'origine."},
                {"NoPermMsg", "Vous n'êtes pas autorisé à porter cette tenue."},

            }, this, "fr");
        }

#endregion

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                ConfigChanged = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                ConfigChanged = true;
            }
            return value;
        }

		private void Init()
        {
            LoadVariables();
            permission.RegisterPermission(PluginScientistAdminPermission, this);
            loaded = true;
        }

        void OnItemCraftFinished(ItemCraftTask task, Item item)
        {

            if (loaded == false){return;}
            if (item == null){return;}

            BasePlayer owner = task.owner as BasePlayer;
            bool hasperm = permission.UserHasPermission(owner.UserIDString, PluginScientistAdminPermission);
            int color = -253079493;

            if (hasperm == true)
            {
                        //Puts($"1");

                if (item.info.shortname == "hazmatsuit")
                {
                                //Puts($"2");
                    item.Remove();
                    ulong unull = 0;

                    if (SuitGreenBool == true){color = -1958316066;}

                    Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(color).itemid, 1, unull);
                            
                                //Puts($"3");

                    itemtogive.name = $"<color=cyan>{suitname}</color>"; 
                    if (SuitGreenBool == true){itemtogive.name = $"<color=green>{suitname} - Peacekeeper</color>"; }
                    if (itemtogive == null){return;}
                    if (owner == null){return;}


                    owner.GiveItem(itemtogive);    

                    Player.Message(owner, $"<color={ChatColor}>{lang.GetMessage("TransMsg", this, owner.UserIDString)}</color>",$"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);

                }

            }

        }

        void CanWearItem(PlayerInventory inventory, Item item, int targetPos)
        {
            if (loaded == false){return;}
            if (item == null){return;}
            if (inventory == null){return;}
            BasePlayer wannawear = inventory.GetComponent<BasePlayer>();

            if (item.info.shortname == "hazmatsuit")
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (wannawear == player)
                    {

                        bool hasperm = permission.UserHasPermission(wannawear.UserIDString, PluginScientistAdminPermission);

                        if (hasperm == false)
                        {
                                    //Puts($"1");

                            if (item.name.Contains("<color=") == true)
                            {
                                            //Puts($"2");
                                item.Remove();
                                ulong unull = 0;

                                Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(1266491000).itemid, 1, unull);
                                if (itemtogive == null){return;}
                                if (wannawear == null){return;}

                                wannawear.GiveItem(itemtogive);    

                                Player.Message(wannawear, $"<color={ChatColor}>{lang.GetMessage("NoPermMsg", this, wannawear.UserIDString)} </color><color=yellow>{lang.GetMessage("BackTransMsg", this, player.UserIDString)}</color>",$"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);

                            }
                        }
                    }
                }
            }    
        }
    }
}


