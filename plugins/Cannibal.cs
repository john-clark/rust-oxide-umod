using System.Collections.Generic;   //list
using Oxide.Core;   // storeddata
using Convert = System.Convert;
using UnityEngine;  //Vector3
using System;   // timespan
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
	[Info("Cannibal", "BuzZ[PHOQUE]", "0.0.3")]
	[Description("Turn to a cannibal, and get some bonus when eating human...and malus on normal food")]

/*======================================================================================================================= 
*
*   
*   20th september 2018
*   command : /cannibal to toggle on/off. Minimum 1 day as cannibal by default.
*   0.0.3 => added permission
*
*   BONUS for cannibals : [stops poison effect] + [comfort for 1 minute] + [stops bleeding]
*
*	THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*   
*
*=======================================================================================================================*/


	public class Cannibal : RustPlugin
	{
        [PluginReference]
        Plugin ImageLibrary;

        string Prefix = "[CANNIBAL] ";                  // CHAT PLUGIN PREFIX
        string PrefixColor = "#bf0000";                 // CHAT PLUGIN PREFIX COLOR
        string ChatColor = "#dd8e8e";                   // CHAT MESSAGE COLOR
        ulong SteamIDIcon = 76561198261703362;    
        int experiencetime = 1440;                      // in minutes ( 1 day = 1440 min.)

        bool debug = false;
		string cannibalicon;
        string ONCannibalHUD;
        private bool ConfigChanged;

        const string FleshEater = "cannibal.player"; 

        public List<string> NotForCannibal = new List<string>()
        {

            "Apple","berries","Bear","Chicken","Deer","Pork","Wolf",  "Cactus","Beans","Tuna","Candy",
            "Chocholate",   // maybe it's an error from rust that will be fixed later ?
            "Fish","Corn","Granola","Hemp","Minnows","Mushroom","Pickle","Pumpkin","Horse",

        };

        class StoredData
        {
            public Dictionary<ulong, string> Cannibalz = new Dictionary<ulong, string>();
            public StoredData()
            {
            }
        }
        private StoredData storedData;

        void Init()
        {
            LoadVariables();
            permission.RegisterPermission(FleshEater, this);   
        }

        void OnServerInitialized()
        {
            ulong imageId = 666999666999666;
			if (ImageLibrary == true)
			{
				bool exists = (bool)ImageLibrary?.Call ("HasImage", "cannibal_plugin_buzz", imageId);
				if (exists == false)
				{
					ImageLibrary?.Call ("AddImage","https://image.ibb.co/kZRdsK/cannibal_icon.jpg", "cannibal_plugin_buzz", imageId);
					if (debug == true) {Puts($"ADDING ICON TO ImageLibrary");}
				}
				if (debug == true) {Puts($"LOADING ICON from ImageLibrary");}
				cannibalicon = (string)ImageLibrary?.Call ("GetImage","cannibal_plugin_buzz", imageId);
			}

			if (ImageLibrary == false)
			{
				PrintWarning($"You are missing plugin reference : ImageLibrary");
			}
            
            foreach(BasePlayer player in BasePlayer.activePlayerList)
            {
                if (storedData.Cannibalz.ContainsKey(player.userID))
                {
                    CannibalHUD(player);
                }

            }
        }

        void Loaded()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("CannibalsOnServer");

        }

        void Unload()
        {
            Interface.Oxide.DataFileSystem.WriteObject("CannibalsOnServer", storedData);
            foreach(BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, ONCannibalHUD);
            }
        }

#region MESSAGES

    void LoadDefaultMessages()
    {

        lang.RegisterMessages(new Dictionary<string, string>
        {
            {"NotMyFoodMsg", "You should not eat non human food...."},
            {"ExperienceMsg", "You experienced cannibalism for"},
            {"SinceMsg", "You are cannibal since"},
            {"NormalMsg", "You can't return to normal life before"},
            {"NowCannibalMsg", "You are now a cannibal survivor ! Minimum experience time is"},
            {"NoPermMsg", "You are not allowed to do this."},

        }, this, "en");

        lang.RegisterMessages(new Dictionary<string, string>
        {
            {"NotMyFoodMsg", "Vous ne devriez pas manger autre chose que de l'humain...."},
            {"ExperienceMsg", "Vous avez vécu en tant que cannibal pendant"},
            {"SinceMsg", "Vous êtes cannibal depuis le"},
            {"NormalMsg", "Vous ne pouvez pas retourner à une vie normale avant"},
            {"NowCannibalMsg", "Vous êtes maintent un survivant cannibal ! Pour un temps minimum de"},
            {"NoPermMsg", "Vous n'avez pas la permission nécessaire."},

        }, this, "fr");
    }


#endregion

#region CONFIG

    protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }


        private void LoadVariables()
        {

            Prefix = Convert.ToString(GetConfig("Chat Settings", "Prefix", "[CANNIBAL] "));                       // CHAT PLUGIN PREFIX
            PrefixColor = Convert.ToString(GetConfig("Chat Settings", "PrefixColor", "#bf0000"));                // CHAT PLUGIN PREFIX COLOR
            ChatColor = Convert.ToString(GetConfig("Chat Settings", "ChatColor", "#dd8e8e"));                    // CHAT MESSAGE COLOR
            SteamIDIcon = Convert.ToUInt64(GetConfig("Chat Settings", "SteamIDIcon", 76561198261703362));        // SteamID FOR PLUGIN ICON - STEAM PROFILE CREATED FOR THIS PLUGIN / 76561198842176097 /
            experiencetime = Convert.ToInt32(GetConfig("Minimum time to play as cannibal, before being able to toggle it off", "in real time minutes", "1440"));                // WINNER NAME COLOR

            if (!ConfigChanged) return;
            SaveConfig();
            ConfigChanged = false;
        }

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

#endregion

////////////////////////////////////////////////////////////////////// CORE OF CANNIBALISM ///////////////////////////

        void OnItemUse(Item item, int amountToUse)
        {
            string ItemToEat = item.info.shortname.ToString();
            if (ItemToEat == null){return;}
            //Puts($"{ItemToEat}");

            ItemContainer Container = item.GetRootContainer();
            if (Container == null){return;}

            BasePlayer Eater = Container.GetOwnerPlayer();
            if (Eater == null){return;}

            if (storedData.Cannibalz.ContainsKey(Eater.userID) == false)
            {return;}

//////// CANNIBAL
            if (ItemToEat.Contains("human") == true)
            {

                    WhenACannibalEatsHuman(Eater);

            }

//////// NOT CANNIBAL
            foreach (string miam in NotForCannibal)
            {
                if (ItemToEat.Contains(miam.ToLower()) == true)
                {
                    if (ItemToEat.Contains("trout"))
                    {
                        return;
                    }

                    WhenACannibalEatsNormal(Eater, ItemToEat);
                }
            }
        }


        void WhenACannibalEatsHuman(BasePlayer Eater)
        {

            //Puts($"POISON BEFORE {Eater.metabolism.poison.value}");

            Eater.metabolism.poison.value = 0;
            //Puts($"POISON AFTER {Eater.metabolism.poison.value}");

            Eater.metabolism.comfort.min = 10;
            Eater.metabolism.bleeding.value = 0;

            Eater.metabolism.calories.value = Eater.metabolism.calories.value + 30;
            Eater.health = Eater.health + 10;
            Eater.metabolism.hydration.value = Eater.metabolism.hydration.value + 60;

// 1 MINUTE BONUS - COMFORT MIN
            timer.Once(60, () =>
            {
                RemoveCannibalBonuses(Eater);
            });

        }

        void WhenACannibalEatsNormal(BasePlayer Eater, string ItemToEat)
        {
            Player.Message(Eater, $"<color={ChatColor}><i>{lang.GetMessage("NotMyFoodMsg", this, Eater.UserIDString)}</i></color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);

            Eater.metabolism.poison.value = Eater.metabolism.poison.value + 5;
            Eater.metabolism.calories.value = Eater.metabolism.calories.value - 80;
            Eater.health = Eater.health - 20;
            Eater.metabolism.hydration.value = Eater.metabolism.hydration.value - 30;

            if (ItemToEat.Contains("cooked"))
            {
                Eater.metabolism.calories.value = Eater.metabolism.calories.value - 30;
            }
            
            if (ItemToEat.Contains("bear") || ItemToEat.Contains("pumpkin") || ItemToEat.Contains("corn"))
            {
                Eater.metabolism.calories.value = Eater.metabolism.calories.value - 40;
                Eater.metabolism.hydration.value = Eater.metabolism.hydration.value - 10;

            }

        }

        void RemoveCannibalBonuses(BasePlayer Eater)
        {
            Eater.metabolism.comfort.min = 0;
        }

        [ChatCommand("cannibal")]
        private void ToggleMyCannibalState(BasePlayer player, string command, string[] args)
        {
            bool isflesheater = permission.UserHasPermission(player.UserIDString, FleshEater);
            if (isflesheater == false)
            {

                Player.Message(player, $"<color={ChatColor}><i>{lang.GetMessage("NoPermMsg", this, player.UserIDString)}</i></color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                return;
            }
            if (storedData.Cannibalz.ContainsKey(player.userID))
            {
                string since;
                storedData.Cannibalz.TryGetValue(player.userID, out since);

                System.DateTime Since = DateTime.Parse(since); // as System.DateTime;

                TimeSpan fenetre = new TimeSpan();
                fenetre = DateTime.Now - Since;

                int minuten = Convert.ToInt32(fenetre.TotalMinutes);
                if (experiencetime == null){experiencetime = 1440;}
                int tonormal = experiencetime - minuten;     

                if (minuten > experiencetime)
                {
                    storedData.Cannibalz.Remove(player.userID);
                    Player.Message(player, $"<color={ChatColor}>{lang.GetMessage("ExperienceMsg", this, player.UserIDString)} {minuten} minutes !</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                    CuiHelper.DestroyUi(player, ONCannibalHUD);
                    return;
                }

                Player.Message(player, $"<color={ChatColor}>{lang.GetMessage("SinceMsg", this, player.UserIDString)} : {since}.\n{lang.GetMessage("NormalMsg", this, player.UserIDString)} : <color=#e34f4f>{tonormal}</color> minutes</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                return;
            }

            if (!storedData.Cannibalz.ContainsKey(player.userID))
            {
                storedData.Cannibalz.Add(player.userID, System.DateTime.Now.ToString());
                Player.Message(player, $"<color={ChatColor}>{lang.GetMessage("NowCannibalMsg", this, player.UserIDString)} : <color=#e34f4f>{experiencetime}</color> min.</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
		        CannibalHUD(player);		
            }
        }

		private void CannibalHUD(BasePlayer player)
		{

			var CuiElement = new CuiElementContainer();
			ONCannibalHUD = CuiElement.Add(new CuiPanel{Image ={Color = "0.8 0.0 0.0 0.5"},RectTransform ={AnchorMin = $"0.95 0.80",AnchorMax = $"1.0 0.85"},CursorEnabled = false
                }, new CuiElement().Parent = "Overlay", ONCannibalHUD);
          		
			CuiElement.Add(new CuiLabel{Text ={Text = $"CANNIBAL\nMODE",FontSize = 8,Align = TextAnchor.MiddleCenter,Color = "0.0 0.0 0.0 1"},RectTransform ={AnchorMin = "0.10 0.10",   AnchorMax = "0.90 0.79"}
				}, ONCannibalHUD);

			if (ImageLibrary == true)
				{
					CuiElement.Add(new CuiElement
					{
						Name = CuiHelper.GetGuid(),
						Parent = ONCannibalHUD,
						Components =
							{
								new CuiRawImageComponent {Png = cannibalicon},
								new CuiRectTransformComponent {AnchorMin = $"0.10 0.10", AnchorMax = $"0.90 0.90"}
							}
					});
				}

			CuiHelper.AddUi(player, CuiElement);

		}
    }
}