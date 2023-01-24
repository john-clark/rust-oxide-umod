using UnityEngine;
using Convert = System.Convert;
using System;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core.Configuration;
using Oxide.Core;
using System.Linq;
using Oxide.Game.Rust.Cui;


namespace Oxide.Plugins
{

    [Info("Happy Hour Plugin", "BuzZ", "2.0.1")]
    public class HappyHour : RustPlugin
    {

/*======================================================================================================================= 
*
*   
*   16th november 2018
*
*	THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*   2.0.0   20181116    complete plugin rewrite by new maintainer (only kept the name of it and main idea) with GUI homemade for all functions
*
*   add randomized skin + config bool
*   add help on main panel at start with decomposed bar
*   message on give at happyhour and if not admin /happy
*
*********************************************
*   Original author :   Feramor on versions <2.0.0
*   Maintainer(s)   :   BuzZ since 20181116 from v2.0.0
*********************************************   
*
*=======================================================================================================================*/


///////////////////////
// LES VARIABLES
/////////////////////////

        bool debug = false;
        private string page;
        private string MainHappyPanel;
        private CuiElementContainer HoursCuiElement;
        private string InsideHappyPanel;
        private string hourbutton;
        private string hourstatus;
        private string houritem;
        private string itembutton;
        private string whathour;
        float rate = 20;
        const string admin = "HappyHour.admin"; 

        private Dictionary<int, int> temporary = new Dictionary<int, int>();

        public class StoredData
        {
            public Dictionary<int, Pack> hourpack = new Dictionary<int, Pack>();
            public StoredData()
            {
            }
        }
        private StoredData storedData;

        public class Pack
        {
            public Dictionary<int, int> intquantity = new Dictionary<int, int>();
            public bool status;
            public List<ulong> received = new List<ulong>();
        }

        void Init()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            permission.RegisterPermission(admin, this);
        }

        void Unload()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
            //CleanZeroz();
        }

        void OnServerInitialized()
        {
            insidetimer();
            timer.Every(rate, () =>
            {   
                insidetimer();
            });
        }

        void insidetimer()
        {
            //float hournow = TOD_Sky.Instance.Cycle.Hour;    //ingame time
            float hournow = System.DateTime.Now.Hour;
            int gamehour = Convert.ToInt32(Math.Round(hournow-0.5));
            if (debug == true){Puts($"INGAME HOUR -> {hournow} => {gamehour}");}
            int game1 = gamehour - 1;
            int game2 = gamehour + 1;
            if (game1 == 0)game1=24;
            if (game2 == 0)game2=24;
            if (gamehour == 0)
            {
                gamehour = 24;
                game1 = 23;
                game2 = 1;
            }


            storedData.hourpack[game1].received.Clear();
            storedData.hourpack[game2].received.Clear();
            foreach (var hourz in storedData.hourpack)
            {
                if (hourz.Key == gamehour)
                {
                    if (storedData.hourpack[gamehour].status == true)
                    {
                        GiveDaHourGifts(gamehour);
                        if (debug == true){Puts($"launching GiveDaHourGifts from insidetimer ! -> {gamehour}");}
                    }
                }
            }
        }

        void CleanZeroz()
        {
            int round = -1;
            int Round = 1;

            /*for (Round = 1; Round <= 24 ; Round++)            
            {
                temporary = new Dictionary<int, int>();

                round = round + 1;
                foreach (KeyValuePair<int, int> togive in storedData.hourpack[round].intquantity.ToArray())
                {

                    temporary.Add(togive.Key, togive.Value);
                    bool etat = storedData.hourpack[round].status;
                    storedData.hourpack.Add(round, new Pack() {intquantity = temporary, status = etat});

                        //storedData.hourpack[round].intquantity.Remove(togive.Key);
                                        //storedData.hourpack.Add(round, new Pack() {intquantity = temporary, status = false}); un gros DEL

                }            
            }*/




        }

        void GiveDaHourGifts(int hour)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
            {
                if (!storedData.hourpack[hour].received.Contains(player.userID))
                {
                    foreach (KeyValuePair<int, int> togive in storedData.hourpack[hour].intquantity.ToArray())
                    {
                        ulong skinitem = 0;
                        if (togive.Value != 0)
                        {
                            Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(togive.Key).itemid, togive.Value, skinitem);
                            if (itemtogive == null)return;
                            //itemtogive.name = name; 
                            player.GiveItem(itemtogive);
                            if (debug == true){Puts($"PLAYER GIVE !");}                            
                        }

                    }
                    storedData.hourpack[hour].received.Add(player.userID);
                    //CleanZeroz();
                    Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
                }      
            }
        }
//////////////////////////
// OUVERTURE DU MAIN PANEL
//////////////////////////

        [ChatCommand("happy")]
        private void OpenMainHappyPanel(BasePlayer player, string command, string[] args, int editing)
        {
            bool isadmin = permission.UserHasPermission(player.UserIDString, admin);
            if (isadmin == false)
            {
                return;
            }
            CuiHelper.DestroyUi(player, MainHappyPanel);
            if (debug == true){Puts($"DEBUT VOID PANEL");}

#region MAIN PANEL COLONNES & COULEURS

            string PanelColor = "0.0 0.0 0.0 1.0";
            string buttonCloseColor = "0.6 0.26 0.2 1";

#endregion

#region MAIN PANEL CUI de base

            var CuiElement = new CuiElementContainer();

            MainHappyPanel = CuiElement.Add(new CuiPanel

            {Image = {Color = $"{PanelColor}"},
                RectTransform = {AnchorMin = "0.0 0.0", AnchorMax = "1.0 1.0"},
                CursorEnabled = true
            }, new CuiElement().Parent = "Overlay", MainHappyPanel);
            
            var closeButton = new CuiButton
            {Button = {Close = MainHappyPanel, Color = $"{buttonCloseColor}"},
                RectTransform = {AnchorMin = "0.95 0.95", AnchorMax = "0.99 0.99"},
                Text = {Text = "[X]", FontSize = 16, Align = TextAnchor.MiddleCenter}
            };

            CuiElement.Add(closeButton, MainHappyPanel);

            var Title = CuiElement.Add(new CuiLabel
            {Text = {Text = $"HAPPY HOUR(s) !", Color = "1.0 1.0 1.0 1.0", FontSize = 24, Align = TextAnchor.MiddleRight},
            RectTransform = {AnchorMin = $"0.70 0.95", AnchorMax = $"0.94 0.99"}
            }, MainHappyPanel);

            var Editing = CuiElement.Add(new CuiLabel
            {Text = {Text = $"edit with <ITEMS> and toggle <ON/OFF> button(s) ", Color = "1.0 1.0 1.0 1.0", FontSize = 20, Align = TextAnchor.MiddleCenter},
            RectTransform = {AnchorMin = $"0.20 0.95", AnchorMax = $"0.65 0.99"}
            }, MainHappyPanel);

            CuiHelper.AddUi(player, CuiElement);

            DisplayHoursButtons (player, editing);
        }

#endregion

        void DisplayHoursButtons (BasePlayer player, int editing)
        {
#region MAIN MENU COLONNES & COULEURS

            var debutcolonne00 = 0.01;
            var fincolonne00 = 0.04;
            var debutcolonne01 = 0.04;
            var fincolonne01 = 0.07;
            var debutcolonne02 = 0.07;
            var fincolonne02 = 0.13;            
            var debutcolonne03 = 0.13;
            var fincolonne03 = 0.16;             
            var hauteurmax = 0.98;             
            var greencolor = "0.5 1.0 0.0 1.0";
            var redcolor = "1.0 0.1 0.1 1.0";
#endregion

#region HOURS AUTO EXPAND

            var HoursCuiElement = new CuiElementContainer();
            int round = -1;
            int Round = 1;
            int hour = 0;
            var status = "0";
            var statuscolor = "0";
            temporary = new Dictionary<int, int>();
            for (Round = 1; Round <= 24 ; Round++)            
            {
                round = round + 1;
                hour = hour + 1;
                string itemcolor = "0.0 0.0 0.0 1.0";
            if (!storedData.hourpack.ContainsKey(hour))
            {
                if (debug == true){Puts($"-> NULL");}
                storedData.hourpack.Add(hour, new Pack() {intquantity = temporary, status = false});
            }
            if (hour == editing)
            {
                itemcolor = "0.2 0.1 0.9 1.0";
            }
            double lignehaut = hauteurmax - (round * 0.04);   //lignebas = lignehaut - 0.03
            var hourbutton = HoursCuiElement.Add(new CuiButton
            {Button ={Command = $"",Color = $"0.0 0.0 0.0 1.0"},Text ={Text = $"{hour}h",Color = "1.0 1.0 1.0 1.0",FontSize = 16,Align = TextAnchor.MiddleCenter},
                RectTransform ={AnchorMin = $"{debutcolonne00} {lignehaut - 0.03}",   AnchorMax = $"{fincolonne00} {lignehaut}"}
            }, MainHappyPanel);

            if (storedData.hourpack[hour].status == true)
            {
                statuscolor = greencolor;
                status = "ON";
            }
            else
            {
                status = "OFF";
                statuscolor = redcolor;
            }
            int total = new int();
            foreach (KeyValuePair<int, int> old in storedData.hourpack[hour].intquantity.ToArray())
            {
                if (old.Value != 0)
                {
                    total = total + 1;
                }
            }

            var hourstatus = HoursCuiElement.Add(new CuiButton
            {Button ={Command = $"hour_toggle {hour}",Color = $"{statuscolor}"},Text ={Text = $"{status}",Color = "0.0 0.0 0.0 1.0",FontSize = 16,Align = TextAnchor.MiddleCenter},
                RectTransform ={AnchorMin = $"{debutcolonne01} {lignehaut - 0.03}",   AnchorMax = $"{fincolonne01} {lignehaut}"}
            }, MainHappyPanel);

            var houritem = HoursCuiElement.Add(new CuiButton
            {Button ={Command = $"hour_display {hour}",Color = $"{itemcolor}"},Text ={Text = $"ITEMS",Color = "1.0 1.0 1.0 1.0",FontSize = 16,Align = TextAnchor.MiddleCenter},
                RectTransform ={AnchorMin = $"{debutcolonne02} {lignehaut - 0.03}",   AnchorMax = $"{fincolonne02} {lignehaut}"}
            }, MainHappyPanel);

            var houritemcount = HoursCuiElement.Add(new CuiButton
            {Button ={Command = $"",Color = $"1.0 1.0 1.0 1.0"},Text ={Text = $"{total}",Color = "0.0 0.0 0.0 1.0",FontSize = 16,Align = TextAnchor.MiddleCenter},
                RectTransform ={AnchorMin = $"{debutcolonne03} {lignehaut - 0.03}",   AnchorMax = $"{fincolonne03} {lignehaut}"}
            }, MainHappyPanel);

#endregion
            }
            CuiHelper.AddUi(player, HoursCuiElement);
        }

        [ConsoleCommand("hour_toggle")]
        private void ToggleDaHourStatus(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            int hour = Convert.ToInt32(arg.Args[0]);
            storedData.hourpack[hour].status = !storedData.hourpack[hour].status;
            if (debug == true){Puts($"-> PACK FOR {hour}h TOGGLE");}
            hourbutton = null;
            hourstatus = null;
            houritem = null;
            DisplayHoursButtons (player, hour);
        }

        [ConsoleCommand("hour_display")]
        private void ItemCatalogForDaHour(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            int hour = Convert.ToInt32(arg.Args[0]);
            ItemCatalogForDaHour(player, hour);
        }
        
        private void ItemCatalogForDaHour(BasePlayer player, int hour)
        {

            OpenMainHappyPanel(player, null, null, hour);

            var greencolor = "0.2 0.6 0.2 1.0";
            var redcolor = "0.9 0.1 0.2 1.0";
            var casecolor = "0.0 0.0 0.0 1.0";
            var debutcolonne00 = 0.17;
            var hauteurmax = 0.94;   
            int round = -1;
            int Round = 1;
            List<string> itemslist = new List<string>();
            foreach(var pair in items)
            {
                itemslist.Add(pair.Value);
            }
            string[] itemsarray = itemsarray = itemslist.ToArray();
            Array.Sort(itemsarray);
            int howmany = itemslist.Count();
            var ItemsCuiElement = new CuiElementContainer();
            whathour = ItemsCuiElement.Add(new CuiButton
            {Button ={Command = $"",Color = $"0.0 0.0 0.0 0.0"},Text ={Text = $"YOU ARE EDITING {hour}H HAPPY HOUR ITEMS",Color = "1.0 1.0 1.0 1.0",FontSize = 16,Align = TextAnchor.MiddleCenter},
                RectTransform ={AnchorMin = $"0.20 0.01",   AnchorMax = $"0.90 0.1"}
            }, MainHappyPanel);
            if (debug == true){Puts($"ITEMS =>{howmany}");}
            for (Round = 1; Round <= 112 ; Round++)            // replace with a count. max and add pages and optimize
            {
                round = round + 1;
                double lignehaut = hauteurmax - (round * 0.05);   //lignebas = lignehaut - 0.03

                if (round < 16) casecolor = "0.0 0.0 0.0 1.0";

                if (round >= 16 && round <= 32)
                {
                    debutcolonne00 = 0.285;
                    lignehaut = hauteurmax - ((round - 16) * 0.05);
                    casecolor = "0.5 0.5 0.5 1.0";
                }
                if (round >= 32 && round <= 48)
                {
                    debutcolonne00 = 0.405;
                    lignehaut = hauteurmax - ((round - 32) * 0.05);
                    casecolor = "0.0 0.0 0.0 1.0";
                }
                if (round >= 48 && round <= 64)
                {
                    debutcolonne00 = 0.52;
                    lignehaut = hauteurmax - ((round - 48) * 0.05);
                    casecolor = "0.5 0.5 0.5 1.0";

                }
                if (round >= 64 && round <= 80)
                {
                    debutcolonne00 = 0.635;
                    lignehaut = hauteurmax - ((round - 64) * 0.05);
                    casecolor = "0.0 0.0 0.0 1.0";
                }
                if (round >= 80 && round <= 96)
                {
                    debutcolonne00 = 0.75;
                    lignehaut = hauteurmax - ((round - 80) * 0.05);
                    casecolor = "0.5 0.5 0.5 1.0";
                }
                if (round >= 96 && round <= 112)
                {
                    debutcolonne00 = 0.865;
                    lignehaut = hauteurmax - ((round - 96) * 0.05);
                    casecolor = "0.0 0.0 0.0 1.0";
                }
                int qty = new int();
                int itemint = new int();
                foreach (var itemnow in items)
                {
                    if (itemnow.Value == itemsarray[round])
                    {
                        itemint = itemnow.Key;
                    }
                }
                foreach (KeyValuePair<int, int> old in storedData.hourpack[hour].intquantity.ToArray())
                {
                    if (itemint == old.Key)
                    {
                        if (old.Value == null)
                        {
                            qty = 0;
                        }
                        else
                        qty = old.Value;
                    }
                }
                if (qty != 0)
                {
                    casecolor = "0.0 0.0 1.0 1.0"; 

                }

                if (debug == true){Puts($"ITEMS =>{qty}x{itemint}");}

                itembutton = ItemsCuiElement.Add(new CuiButton
                {Button ={Command = $"",Color = $"0.0 0.0 0.0 1.0"},Text ={Text = $"",Color = "1.0 1.0 1.0 1.0",FontSize = 16,Align = TextAnchor.MiddleCenter},
                    RectTransform ={AnchorMin = $"{debutcolonne00} {lignehaut - 0.04}",   AnchorMax = $"{debutcolonne00 + 0.12} {lignehaut}"}
                }, MainHappyPanel);

                var itemname = ItemsCuiElement.Add(new CuiButton
                {Button ={Command = $"",Color = $"{casecolor}"},Text ={Text = $"{itemsarray[round]}",Color = "1.0 1.0 1.0 1.0",FontSize = 12,Align = TextAnchor.MiddleCenter},
                    RectTransform ={AnchorMin = $"0.01 0.5",   AnchorMax = $"0.99 0.99"}
                }, itembutton);

                var plus1button = ItemsCuiElement.Add(new CuiButton
                {Button ={Command = $"hour_item add {itemint} 1 {hour}",Color = $"{greencolor}"},Text ={Text = $"+1",Color = "1.0 1.0 1.0 1.0",FontSize = 10,Align = TextAnchor.MiddleCenter},
                    RectTransform ={AnchorMin = $"0.61 0.01",   AnchorMax = $"0.73 0.49"}
                }, itembutton);

                var plus10button = ItemsCuiElement.Add(new CuiButton
                {Button ={Command = $"hour_item add {itemint} 10 {hour}",Color = $"{greencolor}"},Text ={Text = $"+10",Color = "1.0 1.0 1.0 1.0",FontSize = 10,Align = TextAnchor.MiddleCenter},
                    RectTransform ={AnchorMin = $"0.74 0.01",   AnchorMax = $"0.86 0.49"}
                }, itembutton);

                var plus100button = ItemsCuiElement.Add(new CuiButton
                {Button ={Command = $"hour_item add {itemint} 100 {hour}",Color = $"{greencolor}"},Text ={Text = $"+100",Color = "1.0 1.0 1.0 1.0",FontSize = 10,Align = TextAnchor.MiddleCenter},
                    RectTransform ={AnchorMin = $"0.87 0.01",   AnchorMax = $"0.99 0.49"}
                }, itembutton);

                var minus1button = ItemsCuiElement.Add(new CuiButton
                {Button ={Command = $"hour_item remove {itemint} 1 {hour}",Color = $"{redcolor}"},Text ={Text = $"-1",Color = "1.0 1.0 1.0 1.0",FontSize = 10,Align = TextAnchor.MiddleCenter},
                    RectTransform ={AnchorMin = $"0.27 0.01",   AnchorMax = $"0.39 0.49"}
                }, itembutton);

                var minus10button = ItemsCuiElement.Add(new CuiButton
                {Button ={Command = $"hour_item remove {itemint} 10 {hour}",Color = $"{redcolor}"},Text ={Text = $"-10",Color = "1.0 1.0 1.0 1.0",FontSize = 10,Align = TextAnchor.MiddleCenter},
                    RectTransform ={AnchorMin = $"0.14 0.01",   AnchorMax = $"0.26 0.49"}
                }, itembutton);

                var minus100button = ItemsCuiElement.Add(new CuiButton
                {Button ={Command = $"hour_item remove {itemint} 100 {hour}",Color = $"{redcolor}"},Text ={Text = $"-100",Color = "1.0 1.0 1.0 1.0",FontSize = 10,Align = TextAnchor.MiddleCenter},
                    RectTransform ={AnchorMin = $"0.01 0.01",   AnchorMax = $"0.13 0.49"}
                }, itembutton);

                var countbutton = ItemsCuiElement.Add(new CuiButton
                {Button ={Command = $"",Color = $"0.9 0.82 0.65 1.0"},Text ={Text = $"{qty}",Color = "0.0 0.0 0.0 1.0",FontSize = 12,Align = TextAnchor.MiddleCenter},
                    RectTransform ={AnchorMin = $"0.41 0.01",   AnchorMax = $"0.60 0.49"}
                }, itembutton);
            }
            CuiHelper.AddUi(player, ItemsCuiElement);
        }

        [ConsoleCommand("hour_item")]
        private void AddItemOnHourPack(ConsoleSystem.Arg arg)
        {
            temporary = new Dictionary<int, int>();
            var player = arg.Connection.player as BasePlayer;
            string reason = arg.Args[0];
            int itemnow = Convert.ToInt32(arg.Args[1]);
            int qty = Convert.ToInt32(arg.Args[2]);
            int hour = Convert.ToInt32(arg.Args[3]);
            int oldqty = 0;
            foreach (KeyValuePair<int, int> old in storedData.hourpack[hour].intquantity.ToArray())
            {
                temporary.Add(old.Key, old.Value);
                if (itemnow == old.Key)
                {
                    if (old.Value == null)
                    {
                        oldqty = 0;
                    }
                        else
                        oldqty = old.Value;
                }
            }
            temporary.Remove(itemnow);

            if (reason == "add")
            {
            temporary.Add(itemnow, oldqty + qty);
            }
            else
            {
            int newqty = oldqty - qty;
            if (newqty < 0) newqty = 0;
            temporary.Add(itemnow, newqty);

            }


            storedData.hourpack.Remove(hour);
            storedData.hourpack.Add(hour, new Pack() {intquantity = temporary});
            if (debug == true){Puts($"OLD ITEMS {oldqty} => NEW {qty}x{itemnow}");}
            OpenMainHappyPanel(player, null, null, hour);
            ItemCatalogForDaHour(player, hour);
        }

        /*[ConsoleCommand("hour_item_remove")]
        private void RemoveItemOnHourPack(ConsoleSystem.Arg arg)
        {
            temporary = new Dictionary<int, int>();
            var player = arg.Connection.player as BasePlayer;
            int itemnow = Convert.ToInt32(arg.Args[0]);
            int qty = Convert.ToInt32(arg.Args[1]);
            int hour = Convert.ToInt32(arg.Args[2]);
            int oldqty = 0;
            foreach (KeyValuePair<int, int> old in storedData.hourpack[hour].intquantity.ToArray())
            {
                temporary.Add(old.Key, old.Value);
                if (itemnow == old.Key)
                {
                    if (old.Value == null)
                    {
                        oldqty = 0;
                    }
                    else
                    oldqty = old.Value;
                }
            }
            int newqty = oldqty - qty;
            if (newqty < 0) newqty = 0;
            temporary.Remove(itemnow);
            temporary.Add(itemnow, newqty);
            storedData.hourpack.Remove(hour);
            storedData.hourpack.Add(hour, new Pack() {intquantity = temporary});
            if (debug == true){Puts($"OLD ITEMS {oldqty} => NEW {qty}x{itemnow}");}
            OpenMainHappyPanel(player, null, null, hour);
            ItemCatalogForDaHour(player, hour);
        }*/

        public Dictionary<int, string> items = new Dictionary<int, string>
        {

            //{-702051347, "mask.bandana"},   //35
            //{-2012470695, "mask.balaclava"},
            //{1675639563, "hat.beenie"},
            //{-761829530, "burlap.shoes"},
            //{602741290, "burlap.shirt"},
            //{1992974553, "burlap.trousers"},
            //{1877339384, "burlap.headwrap"},
            {850280505, "bucket.helmet"},
            //{-23994173, "hat.boonie"},
            {-1022661119, "hat.cap"},
            //{-2025184684, "shirt.collared"},
            //{-803263829, "coffeecan.helmet"},
            {-1903165497, "deer.skull.mask"},
            //{-1773144852, "attire.hide.skirt"},
            //{196700171, "attire.hide.vest"}, // hide shirt ???!!
            //{1722154847, "attire.hide.pants"},
            //{794356786, "attire.hide.boots"},   // hide shoes
            //{3222790, "attire.hide.helterneck"},
            //{1751045826, "hoodie"},
            //{980333378, "attire.hide.poncho"},
            //{1366282552, "burlap.gloves"},  //Leather Gloves
            //{935692442, "tshirt.long"},
            {1110385766, "metal.plate.torso"},
            {-194953424, "metal.facemask"},
            {-1539025626, "hat.miner"},
            {237239288, "pants"},
            //{-2002277461, "roadsign.jacket"},   // roadside vest
            //{1850456855, "roadsign.kilt"},          // roadside pants
            {671063303, "riot.helmet"},
            {-48090175, "jacket.snow"},
            {-1695367501, "pants.shorts"},
            {1608640313, "shirt.tanktop"},
            {223891266, "tshirt"},
            {-1163532624, "jacket"},    // vagabond jacket ???????
            {-1549739227, "shoes.boots"},
////////////////////////----
            {1545779598, "rifle.ak"},   //29
            {1588298435, "rifle.bolt"},
            //{1711033574, "bone.club"},
            //{1814288539, "knife.bone"},
            {1965232394, "crossbow"},
            {-765183617, "shotgun.double"},
            {-75944661, "pistol.eoka"},
            {143803535, "grenade.f1"},
            {1326180354, "salvaged.sword"},
            {1318558775, "smg.mp5"},        //MP5A4
            {795371088, "shotgun.pump"},
            //{963906841, "rock"},
            {-1506397857, "hammer.salvaged"},
            {-1780802565, "icepick.salvaged"},
            //{-1878475007, "explosive.satchel"},
            {818877484, "pistol.semiauto"},
            {-1583967946, "stonehatchet"},
            {171931394, "stone.pickaxe"},
            {-1469578201, "longsword"},
            {-1758372725, "smg.thompson"},
            {200773292, "hammer"},
            {-1302129395, "pickaxe"},
            {649912614, "pistol.revolver"},
            {442886268, "rocket.launcher"},
            {-904863145, "rifle.semiauto"},
            //{-1367281941, "shotgun.waterpipe"},
            //{1796682209, "smg.2"},
            {1373971859, "pistol.python"},
            {-1812555177, "rifle.lr300"},
////////////////////////----
            //{1353298668, "door.hinged.toptier"},  //armore door
            {-1950721390, "barricade.concrete"},
            //{833533164, "box.wooden.large"},
            {-1736356576, "target.reactive"},
            //{-1754948969, "sleepingbag"},
            //{-2067472972, "door.hinged.metal"},    //sheet metal door
            //{2114754781, "water.purifier"},
            //{-180129657, "box.wooden"},     //wood storage box
            //{1729120840, "door.hinged.wood"},     //wooden door
////////////////////////----
            {-2124352573, "fun.guitar"},  //armore door
            {588596902, "ammo.handmade.shell"},
            //{-2097376851, "ammo.nailgun.nails"}, 
            {785728077, "ammo.pistol"}, 
            {51984655, "ammo.pistol.fire"}, 
            {-1691396643, "ammo.pistol.hv"}, 
            {-1211166256, "ammo.rifle"}, 
            {-1321651331, "ammo.rifle.explosive"}, 
            {1712070256, "ammo.rifle.hv"}, 
            {605467368, "ammo.rifle.incendiary"}, 
            {-742865266, "ammo.rocket.basic"}, 
            {1638322904, "ammo.rocket.fire"}, 
            {-1841918730, "ammo.rocket.hv"}, 
            {-17123659, "ammo.rocket.smoke"}, 
            {-1685290200, "ammo.shotgun"}, 
            {-1036635990, "ammo.shotgun.fire"}, 
            {-727717969, "ammo.shotgun.slug"}, 
            {-1432674913, "antiradpills"}, 
            {-989755543, "bearmeat.burned"},
            {1873897110, "bearmeat.cooked"},
            {1973684065, "chicken.burned"},
            {-1848736516, "chicken.cooked"},
            {-78533081, "deermeat.burned"},
            {-1509851560, "deermeat.cooked"},
            {1668129151, "fish.cooked"},
            {1917703890, "horsemeat.burned"},
            {-1162759543, "horsemeat.cooked"},
            {-682687162, "humanmeat.burned"},
            {1536610005, "humanmeat.cooked"},
            {1391703481, "meat.pork.burned"},
            {-242084766, "meat.pork.cooked"},
            {1827479659, "wolfmeat.burned"},
            {813023040, "wolfmeat.cooked"},
            {-151838493, "wood"}, 
            {215754713, "arrow.bone"},
            {14241751, "arrow.fire"},
            {-1023065463, "arrow.hv"}, 
            {-1234735557, "arrow.wooden"}, 
            {609049394, "battery.small"}, 
            {1099314009, "bbq"}, 
            {1121925526, "candycane"}, 
            {296519935, "diving.fins"}, 
            {-113413047, "diving.mask"}, 
            {-2022172587, "diving.tank"}, 
            {-1101924344, "diving.wetsuit"}, 
            {-265876753, "gunpowder"}, 
            {-1982036270, "hq.metal.ore"}, 
            {254522515, "largemedkit"}, 
            {-946369541, "lowgradefuel"}, 
            {69511070, "metal.fragments"}, 
            {-4031221, "metal.ore"}, 
            //{-1651220691, "pookie.bear"}, 
            {-1667224349, "xmas.decoration.baubels"},
            {-209869746, "xmas.decoration.candycanes"},
            {1686524871, "xmas.decoration.gingerbreadmen"},
            {1723747470, "xmas.decoration.lights"},
            {-129230242, "xmas.decoration.pinecone"}, 
            {-1331212963, "xmas.decoration.star"},
            {2106561762, "xmas.decoration.tinsel"},
            {674734128, "xmas.door.garland"},
            {1058261682, "xmas.lightstring"},
            {-1622660759, "xmas.present.large"},
            {756517185, "xmas.present.medium"},
            {-722241321, "xmas.present.small"},
            {794443127, "xmas.tree"},
            {-1379835144, "xmas.window.garland"},
            {2009734114, "xmasdoorwreath"},
            {952603248, "weapon.mod.flashlight"},
            {442289265, "weapon.mod.holosight"},
            {-132516482, "weapon.mod.lasersight"}, 
            {-1405508498, "weapon.mod.muzzleboost"},
            {1478091698, "weapon.mod.muzzlebrake"},
            {-1850571427, "weapon.mod.silencer"},
            {-855748505, "weapon.mod.simplesight"}, 
            {567235583, "weapon.mod.small.scope"}, 
        };
    }
}
