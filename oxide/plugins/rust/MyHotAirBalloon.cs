using UnityEngine;
using System.Collections.Generic;
using Oxide.Core;
using Convert = System.Convert;
using System.Linq;
using Oxide.Game.Rust.Cui;
using System;
//using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("My Hot Air Balloon", "BuzZ[PHOQUE]", "0.1.3")]
    [Description("Spawn a Hot Air Balloon")]

/*======================================================================================================================= 
*   12th november 2018
*   chat commands : /balloon    /noballoon
*
*   0.0.2   20181112    configfile + langfile + permission + only one balloon by player + health info
*   0.0.3   20181115    no damage
*   0.0.4   20181115    added 4x christmas lights
*   0.0.5   20181116    code optimization
*   0.0.8   20181118    optimz + tp + 4socks
*   0.0.9   20181123    NRE kill/damage entity balloontimer   permission.god.fuel   HUD bar prepared for lock   upboost speed+/-
*   0.1.0   20181123    bar bottom in config file
*   0.1.1   20181124    permission for tp (myhotairballoon.tp)
*   0.1.2   20181209    navigation system on permission.navigate + change how spawn + console commands (spawn/despawn playerID)
*   0.1.3               work around console commands
*                       new perm needed on despawn + HUD back on plugin reload + HUD back on reconnect
*                       + new perm bomb + bomb HUD + bomb drop/explosion + null balloon to player damage
*
*   notes for later :
*       population (float)
*       currentWindVec (vector3)
*       liftAmount (float)
*       outsidedecayminutes (float)
*       public virtual bool UseFuel(float32 seconds)
*       public virtual bool HasFuel([Optional, DefaultParameterValue(False)] bool forceCheck)
*       public void DecayTick()
*       public override void Save(.SaveInfo info)
*       public bool WaterLogged()
*       public UnityEngine.Transform buoyancyPoint;
*       public UnityEngine.Transform[] windFlags;
*       public float32 windForce;
*       add option/config -> if bool godballoon true 0 damage; else chat message with health/max
*=======================================================================================================================*/

    public class MyHotAirBalloon : RustPlugin
    {
        bool debug = false;
        private string DaBalloonBar;
		private string DaBalloonBarHealth;
        private string DaBalloonBarWind;
        private string DaBalloonBarInflation;
        private string DaBalloonBarLift;
        private string DaBalloonBarFuel;
        private string DaBalloonBarLock;
        private string DaBalloonBarupboost;
        private string DaBalloonNavigatorBar;
        private string DaBalloonNavigatorBarWest;
        private string DaBalloonNavigatorBarEast;
        private string DaBalloonNavigatorBarNorth;
        private string DaBalloonNavigatorBarSouth;
        private string DaBalloonBombBar;
        string Prefix = "[My Balloon] ";
        string PrefixColor = "#008000";
        string ChatColor = "#a5d9ff"; 
        ulong SteamIDIcon = 76561198387807862;
        float fuelrate = 0.25f;
        double barbottom = 0.12;

////// maximum bombs/player
////////// perm for unlimited bombs
////////// player timer to refill bombs quantity
////// true/false to remove all balloons with no owners

        const string prefab = "assets/prefabs/deployable/hot air balloon/hotairballoon.prefab";
        string lightprefab = "assets/prefabs/misc/xmas/christmas_lights/xmas.lightstring.deployed.prefab";
        string stockprefab = "assets/prefabs/misc/xmas/stockings/stocking_large_deployed.prefab";

        private bool ConfigChanged;
        const string Ballooner = "myhotairballoon.player"; 
        const string UnBallooner = "myhotairballoon.despawn"; 
        const string GodBalloon = "myhotairballoon.god"; 
        const string FuelRateBalloon = "myhotairballoon.fuel"; 
        const string BalloonTP = "myhotairballoon.tp";
        const string Navigator = "myhotairballoon.navigate";
        const string Bomber = "myhotairballoon.bomb";

        //public Dictionary<HotAirBalloon, bool > balloonlock = new Dictionary<HotAirBalloon, bool>();    // FOR FUTURE USE
        public Dictionary<BasePlayer, HotAirBalloon > baseplayerballoon = new Dictionary<BasePlayer, HotAirBalloon>();
        public Dictionary<HotAirBalloon, Timer > balloontimer = new Dictionary<HotAirBalloon, Timer>();
        //public Dictionary<HotAirBalloon, Timer > balloontimernavigate = new Dictionary<HotAirBalloon, Timer>();

        public List<BasePlayer> NavigateMode = new List<BasePlayer>();
        public Dictionary<HotAirBalloon, Vector3 > balloonposition = new Dictionary<HotAirBalloon, Vector3>();
        public List<BasePlayer> BalloonWest = new List<BasePlayer>();
        public List<BasePlayer> BalloonEast = new List<BasePlayer>();
        public List<BasePlayer> BalloonNorth = new List<BasePlayer>();
        public List<BasePlayer> BalloonSouth = new List<BasePlayer>();
//        public List<BasePlayer> PlayerInBalloon = new List<BasePlayer>();

    class StoredData
    {
        public Dictionary<ulong, uint> playerballoon = new Dictionary<ulong, uint>();

        public StoredData()
        {
        }
    }
        private StoredData storedData;

        void Init()
        {
            LoadVariables();
            permission.RegisterPermission(Ballooner, this);
            permission.RegisterPermission(GodBalloon, this);
            permission.RegisterPermission(FuelRateBalloon, this);
            permission.RegisterPermission(BalloonTP, this);
            permission.RegisterPermission(Navigator, this);
            permission.RegisterPermission(Bomber, this);
            permission.RegisterPermission(UnBallooner, this);

            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name); 
        }

        void OnServerInitialized()
        {
////////////// after reload - get back balloon uint to class hotairballoon
            PluginReload();
        }

        void Unload()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
            foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
            {
                CuiHelper.DestroyUi(player, DaBalloonBar);
                CuiHelper.DestroyUi(player, DaBalloonNavigatorBar);
                CuiHelper.DestroyUi(player, DaBalloonNavigatorBarWest);
                CuiHelper.DestroyUi(player, DaBalloonNavigatorBarEast);
                CuiHelper.DestroyUi(player, DaBalloonNavigatorBarNorth);
                CuiHelper.DestroyUi(player, DaBalloonNavigatorBarSouth);
                CuiHelper.DestroyUi(player, DaBalloonBombBar);
            }        
        }
#region BackOnTimer
        void PluginReload()
        {
           foreach (var Balloon in UnityEngine.Object.FindObjectsOfType<HotAirBalloon>())
            {
                if (debug) Puts("ONE Balloon found");
                BaseEntity Loloon = Balloon as BaseEntity;
                if (Loloon == null)
                {
                    if (debug) Puts("Loloon NULL !!");
                    return;
                }
                if (debug) Puts($"Loloon.OwnerID {Loloon.OwnerID}");
                if (Loloon.OwnerID == 0)
                {
                    if (debug) Puts("RELOAD EACH - HAB Owner is null");
                }
                else
                {
                    //playerballoonreload.Add(Loloon.OwnerID, Loloon.net.ID);
                    foreach(BasePlayer player in BasePlayer.activePlayerList.ToList())
                    {
                        if (player.userID == Loloon.OwnerID)
                        {
                            baseplayerballoon.Remove(player);
                            baseplayerballoon.Add(player, Balloon);
                            BalloonBar(player);
                            BalloonTimer(Balloon, Loloon, player);
                            //BalloonBarButtons(player, Balloon.fuelPerSec, Balloon.inflationLevel, Balloon.liftAmount, Balloon.windForce, 0);
//                            BalloonBarButtons(player, Balloon.fuelPerSec, Balloon.inflationLevel, Balloon.liftAmount, Balloon.windForce, Loloon.Health());

                            if (debug) Puts("RELOAD - ONE HAB BACK for online player");
                        }
                    }
                }
            }
        }

////////////// ON PLAYER RESPAWN - get back HUD etc.
        void OnPlayerSleepEnded(BasePlayer player)
        {
           foreach (var Balloon in UnityEngine.Object.FindObjectsOfType<HotAirBalloon>())
            {
                BaseEntity Loloon = Balloon as BaseEntity;
                if (Loloon.OwnerID == null)
                {
                    if (debug) Puts("SLEEPENDED EACH - HAB Owner is null");
                }
                else
                {
                    if (player.userID == Loloon.OwnerID)
                    {
                        baseplayerballoon.Remove(player);
                        baseplayerballoon.Add(player, Balloon);
                        BalloonBar(player);
                        BalloonTimer(Balloon, Loloon, player);
                        if (debug) Puts("SLEEPENDED - ONE HAB BACK for player");
                    }
                }
            }
        }
#endregion
#region MESSAGES

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"AlreadyMsg", "You already have a Hot Air Balloon.\nuse command '/noballoon' to remove it."},
                {"SpawnedMsg", "Your Hot Air Balloon has spawned !\nuse command '/noballoon' to remove it and its HUD."},
                {"HealthMsg", "Your Balloon' Health is now"},
                {"KilledMsg", "Your Balloon has been removed/killed."},
                {"NoPermMsg", "You are not allowed to do this."},
                {"healthinfoMsg", "Health :"},
                {"windforceMsg", "Speed :"},
                {"liftMsg", "Lift :"},
                {"inflationMsg", "Inflation :"},
                {"lockMsg", "Lock :"},
                {"fuelMsg", "Fuel/sec :"},
                {"NavButtonMsg", "Please Toggle NAV MODE to ON"},
                {"NoSpeedMsg", "NAV MODE is ON. Speed is automatic only."},

            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"AlreadyMsg", "Vous avez déjà une Montgolfière.\nutilisez la commande '/noballoon' pour la supprimer."},
                {"SpawnedMsg", "Votre Montgolfière est arrivée !\nutilisez la commande '/noballoon' pour la supprimer ainsi que son interface."},
                {"HealthMsg", "La santé de votre Montgolfière est de"},
                {"KilledMsg", "Votre Montgolfière a disparu du monde."},
                {"NoPermMsg", "Vous n'êtes pas autorisé."},
                {"healthinfoMsg", "Vie :"},
                {"windforceMsg", "Vitesse :"},
                {"liftMsg", "Lift :"},
                {"inflationMsg", "Inflation :"},
                {"lockMsg", "Lock :"},
                {"fuelMsg", "Fuel/sec :"},
                {"NavButtonMsg", "Activez d'abord le MODE NAV"},
                {"NoSpeedMsg", "Le MODE NAV est activé. La vitesse n'est pas modifiable."},

            }, this, "fr");
        }

#endregion
#region CONFIG

    protected override void LoadDefaultConfig()
        {
            LoadVariables();
        }

        private void LoadVariables()
        {
            Prefix = Convert.ToString(GetConfig("Chat Settings", "Prefix", "[My Balloon] "));                       // CHAT PLUGIN PREFIX
            PrefixColor = Convert.ToString(GetConfig("Chat Settings", "PrefixColor", "#008000"));                // CHAT PLUGIN PREFIX COLOR
            ChatColor = Convert.ToString(GetConfig("Chat Settings", "ChatColor", "#a5d9ff"));                    // CHAT MESSAGE COLOR
            SteamIDIcon = Convert.ToUInt64(GetConfig("Chat Settings", "SteamIDIcon", "76561198387807862"));        // SteamID FOR PLUGIN ICON - STEAM PROFILE CREATED FOR THIS PLUGIN / NONE YET /
            fuelrate = Convert.ToSingle(GetConfig("Fuel per second", "0.25 by default", "0.25"));        // SteamID FOR PLUGIN ICON - STEAM PROFILE CREATED FOR THIS PLUGIN / NONE YET /
            barbottom = Convert.ToDouble(GetConfig("HUD position - bar bottom", "0.12 by default", "0.12"));

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
#region EntitySpawn

/////////// PLAYER SPAWNS HIS BALLOON //////////////

            BaseEntity SpawnEntity(string prefab, bool active, int angx, int angy, int angz, float posx, float posy, float posz, BaseEntity parent, ulong skinid)
            {
                    BaseEntity entity = GameManager.server.CreateEntity(prefab, new Vector3(), new Quaternion(), active);
                    if (entity == null) return null;
                    entity.transform.localEulerAngles = new Vector3(angx, angy, angz);
                    entity.transform.localPosition = new Vector3(posx, posy, posz);
                    if (parent == null) return null;                    
                    entity.SetParent(parent, 0);
                    entity.skinID = skinid;
                    entity?.Spawn();
                    RefreshPosition(entity);
                    return entity;
            }

            void RefreshPosition(BaseEntity entity)
            {
                var stability = entity.GetComponent<StabilityEntity>();
                if(stability != null){stability.grounded = true;}
                var mountable = entity.GetComponent<BaseMountable>();
                if(mountable != null){mountable.isMobile = true;}
            }

#endregion
#region Teleport

        [ChatCommand("balloon_tp")]         
        private void TeleportTo(BasePlayer player, string command, string[] args)
        {
            bool isballooner = permission.UserHasPermission(player.UserIDString, Ballooner);
            bool balloontp = permission.UserHasPermission(player.UserIDString, BalloonTP);

            if (isballooner == false || balloontp == false)
            {
                Player.Message(player, $"<color={ChatColor}><i>{lang.GetMessage("NoPermMsg", this, player.UserIDString)}</i></color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                return;
            }
            if (storedData.playerballoon.ContainsKey(player.userID) == true)
            {
                uint deluint;
                storedData.playerballoon.TryGetValue(player.userID, out deluint);
                var balloonposition = BaseNetworkable.serverEntities.Find(deluint);
                if (balloonposition == null)return;
                Vector3 WhereBalloonIs = new Vector3(balloonposition.transform.position.x,balloonposition.transform.position.y+0.5f,balloonposition.transform.position.z);

                player.Teleport(WhereBalloonIs);
            }
        }

#endregion

        [ChatCommand("balloon_invite")]         
        private void InviteAFriend(BasePlayer player, string command, string[] args)
        {
            /*bool isballooner = permission.UserHasPermission(player.UserIDString, Ballooner);
            if (isballooner == false)
            {
                Player.Message(player, $"<color={ChatColor}><i>{lang.GetMessage("NoPermMsg", this, player.UserIDString)}</i></color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                return;
            }*/
        }

        [ChatCommand("balloon_lock")]         
        private void LockMyBalloon(BasePlayer player, string command, string[] args)
        {



        }

#region Bomber

        void MyBallonBombHUD(HotAirBalloon Balloon, BaseEntity Loloon, BasePlayer player)
        {
			var CuiElement = new CuiElementContainer();
			DaBalloonBombBar = CuiElement.Add(new CuiPanel{Image ={Color = "0.0 0.0 0.0 0.0"},RectTransform ={AnchorMin = $"0.685 {barbottom+0.10}",AnchorMax = $"0.715 {barbottom+0.13}"},CursorEnabled = false
//                }, new CuiElement().Parent = "Overlay", DaBalloonBar);
            });
            string bombbuttoncolor = "1.0 0.7 0.2 0.8";
            Vector3 pouzichon = Balloon.transform.position;
            float height = TerrainMeta.HeightMap.GetHeight(pouzichon);
            float meters = Balloon.transform.position.y - height;
            if (debug) Puts($"meters {meters}");
            if (meters>20)
            {
                bombbuttoncolor = "0.5 1.0 0.0 0.8";
            }
            var BombButton = CuiElement.Add(new CuiButton
                {Button ={Command = $"DropABombFromMyBalloon",Color = bombbuttoncolor},Text ={Text = $"BOMB",Color = "0.0 0.0 0.0 1.0",FontSize = 10,Align = TextAnchor.MiddleCenter},
                    RectTransform ={AnchorMin = "0.0 0.0",   AnchorMax = "1.0 1.0"}
                }, DaBalloonBombBar);

			CuiHelper.AddUi(player, CuiElement);
        }

        [ConsoleCommand("DropABombFromMyBalloon")]
        private void MyBallonBombDrop(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            HotAirBalloon Balloon = new HotAirBalloon();
            if (debug) Puts("BOMB Commanded !!!");
            foreach (var item in baseplayerballoon)
            {
                if (item.Key == player)
                {
                    if (debug) Puts("BOMB Command - found balloon");
                    Balloon = item.Value;
                    Vector3 daboom = new Vector3(Balloon.transform.position.x,Balloon.transform.position.y - 3,Balloon.transform.position.z);
                    BaseEntity BombJack = ItemManager.CreateByName("jackolantern.angry", 1).Drop(daboom, Vector3.up);

                    if (BombJack == null)
                    {
                        if (debug) Puts("BombJack == null");
                        return;
                    }
                    if (debug) Puts("BOMB DROP !!!");
                    timer.Once(3f, () =>
                    {
                        daboom = BombJack.transform.position;
                        BombJack?.Kill();
                        BaseEntity GrenadeF1 = GameManager.server.CreateEntity("assets/prefabs/weapons/f1 grenade/grenade.f1.deployed.prefab", daboom, new Quaternion(), true);
                        TimedExplosive boom = new TimedExplosive();
                        boom = GrenadeF1.GetComponent<TimedExplosive>();
                        boom?.Explode();

                    });
                }
            }
        }

#endregion
#region Navigator
/////////////////////////
// NAVIGATION HUD/SYSTEM
/////////////////////////

        private void NavigateMyBalloon(HotAirBalloon Balloon, BaseEntity Loloon, BasePlayer player)
        {
            if (!player.HasParent())
            {
                if (debug)Puts($"player NOT in Balloon");
                return;
            }
            else
            {
                var isparentaballoon = player.GetParentEntity() as HotAirBalloon;
                if (isparentaballoon == null) return;
                if (isparentaballoon != Balloon) return;
            }
            if (debug)Puts($"player IS in Balloon. HUD launch");
            bool isnavigator = permission.UserHasPermission(player.UserIDString, Navigator);
            //if (!isnavigator) return;
            bool isbomber = permission.UserHasPermission(player.UserIDString, Bomber);
            if (isbomber) MyBallonBombHUD(Balloon, Loloon, player);
            if (isnavigator) NavigatorBarHUD(Balloon, Loloon, player);
            //list playerinballoon
        }

        void NavigatorBarHUD(HotAirBalloon Balloon, BaseEntity Loloon, BasePlayer player)
        {
			var CuiElement = new CuiElementContainer();
			DaBalloonNavigatorBar = CuiElement.Add(new CuiPanel{Image ={Color = "0.0 0.0 0.0 0.2"},RectTransform ={AnchorMin = $"0.645 {barbottom+0.06}",AnchorMax = $"0.68 {barbottom+0.09}"},CursorEnabled = false
//                }, new CuiElement().Parent = "Overlay", DaBalloonBar);
            });

            if (debug)Puts($"WIND VECTOR => {Balloon.currentWindVec}");
            Vector3 actualwind = Balloon.currentWindVec;
            Vector3 finalwind = new Vector3(0,0,0);
            Vector3 eastwind = new Vector3(1000,0,0);
            Vector3 westwind = new Vector3(-1000,0,0);
            Vector3 northwind = new Vector3(0,0,1000);
            Vector3 southwind = new Vector3(0,0,-1000);

            string navigatecolor = "1.0 0.5 0.5 0.2";
            string navigatewest = "1.0 1.0 0.5 0.2";
            string navigateeast = "1.0 1.0 0.5 0.2";
            string navigatenorth = "1.0 1.0 0.5 0.2";
            string navigatesouth = "1.0 1.0 0.5 0.2";

            if (NavigateMode.Contains(player))
            {
                navigatecolor = "0.0 1.0 0.0 0.2";
                if (BalloonEast.Contains(player))
                {
                    navigateeast = "0.0 1.0 0.0 0.6";
                    finalwind = finalwind + eastwind;
                }
                if (BalloonWest.Contains(player))
                {
                    navigatewest = "0.0 1.0 0.0 0.6";
                    finalwind = finalwind + westwind;
                }
                if (BalloonNorth.Contains(player))
                {
                    navigatenorth = "0.0 1.0 0.0 0.6";
                    finalwind = finalwind + northwind;
                }
                if (BalloonSouth.Contains(player))
                {
                    navigatesouth = "0.0 1.0 0.0 0.6";
                    finalwind = finalwind + southwind;
                }
                Balloon.windForce = 0;
                if (debug)Puts($"FINAL VECTOR3 => {finalwind}");
                if (Balloon.inflationLevel >= 1) Balloon.myRigidbody.AddForce(finalwind,ForceMode.Impulse);
            }
            else
            {
                if (Balloon.windForce == 0) Balloon.windForce = 600f;
            }
            var NavigatorButton = CuiElement.Add(new CuiButton
                {Button ={Command = $"NavigateMyHotAirBalloon state",Color = navigatecolor},Text ={Text = $"NAV\nON/OFF",Color = "1.0 1.0 1.0 1.0",FontSize = 8,Align = TextAnchor.MiddleCenter},
                    RectTransform ={AnchorMin = "0.0 0.0",   AnchorMax = "1.0 1.0"}
                }, DaBalloonNavigatorBar);
			CuiHelper.AddUi(player, CuiElement);

			var CuiElementWest = new CuiElementContainer();
			DaBalloonNavigatorBarWest = CuiElementWest.Add(new CuiPanel{Image ={Color = "0.0 0.0 0.0 0.2"},RectTransform ={AnchorMin = $"0.61 {barbottom+0.06}",AnchorMax = $"0.64 {barbottom+0.09}"},CursorEnabled = false
//                }, new CuiElement().Parent = "Overlay", DaBalloonBar);
            });

			var CuiElementEast = new CuiElementContainer();
			DaBalloonNavigatorBarEast = CuiElementEast.Add(new CuiPanel{Image ={Color = "0.0 0.0 0.0 0.2"},RectTransform ={AnchorMin = $"0.685 {barbottom+0.06}",AnchorMax = $"0.715 {barbottom+0.09}"},CursorEnabled = false
//                }, new CuiElement().Parent = "Overlay", DaBalloonBar);
            });
            
			var CuiElementNorth = new CuiElementContainer();
			DaBalloonNavigatorBarNorth = CuiElementNorth.Add(new CuiPanel{Image ={Color = "0.0 0.0 0.0 0.2"},RectTransform ={AnchorMin = $"0.645 {barbottom+0.10}",AnchorMax = $"0.68 {barbottom+0.13}"},CursorEnabled = false
//                }, new CuiElement().Parent = "Overlay", DaBalloonBar);
            });

			var CuiElementSouth = new CuiElementContainer();
			DaBalloonNavigatorBarSouth = CuiElementSouth.Add(new CuiPanel{Image ={Color = "0.0 0.0 0.0 0.2"},RectTransform ={AnchorMin = $"0.645 {barbottom+0.02}",AnchorMax = $"0.68 {barbottom+0.05}"},CursorEnabled = false
//                }, new CuiElement().Parent = "Overlay", DaBalloonBar);
            });

            var NavigatorWestButton = CuiElementWest.Add(new CuiButton
                {Button ={Command = $"NavigateMyHotAirBalloon west",Color = navigatewest},Text ={Text = $"WEST",Color = "1.0 1.0 1.0 1.0",FontSize = 12,Align = TextAnchor.MiddleCenter},
                    RectTransform ={AnchorMin = "0.0 0.0",   AnchorMax = "1.0 1.0"}
                }, DaBalloonNavigatorBarWest);

            var NavigatorEastButton = CuiElementEast.Add(new CuiButton
                {Button ={Command = $"NavigateMyHotAirBalloon east",Color = navigateeast},Text ={Text = $"EAST",Color = "1.0 1.0 1.0 1.0",FontSize = 12,Align = TextAnchor.MiddleCenter},
                    RectTransform ={AnchorMin = "0.0 0.0",   AnchorMax = "1.0 1.0"}
                }, DaBalloonNavigatorBarEast);

            var NavigatorNorthButton = CuiElementNorth.Add(new CuiButton
                {Button ={Command = $"NavigateMyHotAirBalloon north",Color = navigatenorth},Text ={Text = $"NORTH",Color = "1.0 1.0 1.0 1.0",FontSize = 12,Align = TextAnchor.MiddleCenter},
                    RectTransform ={AnchorMin = "0.0 0.0",   AnchorMax = "1.0 1.0"}
                }, DaBalloonNavigatorBarNorth);

            var NavigatorSouthButton = CuiElementSouth.Add(new CuiButton
                {Button ={Command = $"NavigateMyHotAirBalloon south",Color = navigatesouth},Text ={Text = $"SOUTH",Color = "1.0 1.0 1.0 1.0",FontSize = 12,Align = TextAnchor.MiddleCenter},
                    RectTransform ={AnchorMin = "0.0 0.0",   AnchorMax = "1.0 1.0"}
                }, DaBalloonNavigatorBarSouth);

			CuiHelper.AddUi(player, CuiElementWest);
			CuiHelper.AddUi(player, CuiElementEast);
			CuiHelper.AddUi(player, CuiElementNorth);
			CuiHelper.AddUi(player, CuiElementSouth);
        }

        [ConsoleCommand("NavigateMyHotAirBalloon")]
        private void NavigateMyHotAirBalloon(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;

            if (arg.Args[0] == "state")
            {
                if (NavigateMode.Contains(player))
                {
                    NavigateMode.Remove(player);
                    BalloonEast.Remove(player);
                    BalloonWest.Remove(player);
                    BalloonSouth.Remove(player);
                    BalloonNorth.Remove(player);
                }
                else NavigateMode.Add(player);
            }
            else
            {
                if (!NavigateMode.Contains(player))
                {
                    Player.Message(player, $"<color={ChatColor}>{lang.GetMessage("NavButtonMsg", this, player.UserIDString)}</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                    return;
                }
                if (arg.Args[0] == "west")
                {
                    if (BalloonWest.Contains(player)) BalloonWest.Remove(player);
                    else BalloonWest.Add(player);
                    if (BalloonEast.Contains(player)) BalloonEast.Remove(player);
                    return;
                }
                if (arg.Args[0] == "east")
                {
                    if (BalloonEast.Contains(player)) BalloonEast.Remove(player);
                    else BalloonEast.Add(player);
                    if (BalloonWest.Contains(player)) BalloonWest.Remove(player);
                    return;
                }
                if (arg.Args[0] == "north")
                {
                    if (BalloonNorth.Contains(player)) BalloonNorth.Remove(player);
                    else BalloonNorth.Add(player);
                    if (BalloonSouth.Contains(player)) BalloonSouth.Remove(player);
                    return;
                }
                if (arg.Args[0] == "south")
                {
                    if (BalloonSouth.Contains(player)) BalloonSouth.Remove(player);
                    else BalloonSouth.Add(player);
                    if (BalloonNorth.Contains(player)) BalloonNorth.Remove(player);
                    return;  
                }
            }
        }
#endregion
#region BalloonSpawn
//////////////////////////
// BALLOON SPAWN
//////////////////////////

        float RandomAFloatBuddy()
        {
            System.Random randomized = new System.Random();
            float x = randomized.Next(-10,10);
            return x;
        }

        [ConsoleCommand("SpawnMyHotAirBalloon")]
        private void SpawnMyHotAirBalloonConsole(ConsoleSystem.Arg arg)
        {
            //BasePlayer player = arg.Connection.player as BasePlayer;
            if(arg.Args == null || arg.Args.Length == 0)
            {
                if (debug)PrintWarning($"SPAWN CONSOLE COMMAND NULL");
                return;
            }
            ulong searchID = 0;
            if (ulong.TryParse(arg.Args[0], out searchID) == false)
            {
                if (debug)PrintWarning($"SPAWN CONSOLE COMMAND - NOT ULONG");               
                return;
            }
            BasePlayer playerfound;
            foreach (var playerzonline in BasePlayer.activePlayerList.ToList())
            {
                if (playerzonline.userID == searchID)
                {
                    playerfound = playerzonline;
                    if (debug)Puts($"SPAWN CONSOLE COMMAND ON PLAYER");
                    SpawnMyBalloon(playerfound, null, null);
                    return;
                }
            }
            if (debug)Puts($"PLAYER NOT FOUND");
        }

        [ChatCommand("balloon")]         
        private void SpawnMyBalloonChatCommand(BasePlayer player, string command, string[] args)
        {
            bool isballooner = permission.UserHasPermission(player.UserIDString, Ballooner);
            if (isballooner == false)
            {
                Player.Message(player, $"<color={ChatColor}><i>{lang.GetMessage("NoPermMsg", this, player.UserIDString)}</i></color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                return;
            }
            if (storedData.playerballoon.ContainsKey(player.userID) == true)
            {
                Player.Message(player, $"<color={ChatColor}>{lang.GetMessage("AlreadyMsg", this, player.UserIDString)}</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                return;
            }
            SpawnMyBalloon(player, null, null); 

        }

        private void SpawnMyBalloon(BasePlayer player, string command, string[] args)
        {
            //Vector3 position = player.transform.position + (player.transform.forward * 20);

            float randomx = RandomAFloatBuddy();
            float randomy = RandomAFloatBuddy();

            Vector3 random3 = new Vector3(randomx,15,randomy);
            Vector3 position = player.transform.position + random3;
            
            if (position == null) return;
            HotAirBalloon Balloon = (HotAirBalloon)GameManager.server.CreateEntity(prefab, position, new Quaternion());
            if (Balloon == null) return;
            Balloon.fuelPerSec = fuelrate;
            bool isfuel = permission.UserHasPermission(player.UserIDString, FuelRateBalloon);
            if (isfuel == true)
            {
                Balloon.fuelPerSec = 0; //0.25 by default
            }
            Balloon.Spawn();
            Player.Message(player, $"<color={ChatColor}>{lang.GetMessage("SpawnedMsg", this, player.UserIDString)}</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
            BaseEntity Loloon = Balloon as BaseEntity;
            Loloon.OwnerID = player.userID;
#endregion
#region BalloonLights
//////////////////
//BALLOON LIGHTS
//////////////////

            //Quaternion entityrot;
            //Vector3 entitypos;


            BaseEntity lightunderleft = SpawnEntity(lightprefab, true, 0, 90, 0, 1.4f, 0.31f, 0.1f, Loloon, 1);
            if (lightunderleft != null){lightunderleft.SetFlag(BaseEntity.Flags.Busy, true);}
            BaseEntity lightunderright = SpawnEntity(lightprefab, true, 0, 90, 0, -1.5f, 0.31f, 0.1f, Loloon, 1);
            if (lightunderright != null){lightunderright.SetFlag(BaseEntity.Flags.Busy, true);}
            BaseEntity lightdoorleft = SpawnEntity(lightprefab, true, 0, 0, 90, 0.7f, 1.4f, 1.5f, Loloon, 1);
            if (lightdoorleft != null){lightdoorleft.SetFlag(BaseEntity.Flags.Busy, true);}
            BaseEntity lightdoorright = SpawnEntity(lightprefab, true, 0, 0, 90, -0.8f, 1.4f, 1.5f, Loloon, 1);
            if (lightdoorright != null){lightdoorright.SetFlag(BaseEntity.Flags.Busy, true);}
#endregion
#region BalloonSocks
//////////////////
//STOCKS
//////////////////
            BaseEntity stock1L = SpawnEntity(stockprefab, true, 0, 90, 0, 1.5f, 0.2f, 0.6f, Loloon, 1);
            if (stock1L != null){stock1L.SetFlag(BaseEntity.Flags.Busy, true);}
            BaseEntity stock2L = SpawnEntity(stockprefab, true, 0, 90, 0, 1.5f, 0.2f, -0.6f, Loloon, 1);
            if (stock2L != null){stock1L.SetFlag(BaseEntity.Flags.Busy, true);}
            BaseEntity stock1R = SpawnEntity(stockprefab, true, 0, 90, 0, -1.5f, 0.2f, 0.6f, Loloon, 1);
            if (stock1L != null){stock1R.SetFlag(BaseEntity.Flags.Busy, true);}
            BaseEntity stock2R = SpawnEntity(stockprefab, true, 0, 90, 0, -1.5f, 0.2f, -0.6f, Loloon, 1);
            if (stock2L != null){stock2R.SetFlag(BaseEntity.Flags.Busy, true);}

            uint balluint = Balloon.net.ID;
            if (debug == true) {Puts($"SPAWNED BALLOON {balluint.ToString()} for player {player.displayName}");}

            //BaseCombatEntity Loon = Balloon as BaseCombatEntity;
            storedData.playerballoon.Remove(player.userID);
            storedData.playerballoon.Add(player.userID,balluint);
            //balloonlist.Add(balluint,Balloon);
            baseplayerballoon.Remove(player);
            baseplayerballoon.Add(player, Balloon);
//////////////////
            float entityhealth = Loloon.Health();
            BalloonBar(player);
            BalloonTimer(Balloon, Loloon, player);
            BalloonBarButtons(player, Balloon.fuelPerSec, Balloon.inflationLevel, Balloon.liftAmount, Balloon.windForce, entityhealth);
        }
#endregion
#region Despawn/Kill
/////////////////////////////
// BALLOON DESPAWN KILL
////////////////////////////
        [ConsoleCommand("DespawnMyHotAirBalloon")]
        private void DespawnMyHotAirBalloonConsole(ConsoleSystem.Arg arg)
        {
            //BasePlayer player = arg.Connection.player as BasePlayer;
            if(arg.Args == null || arg.Args.Length == 0)
            {
                if (debug)PrintWarning($"DESPAWN CONSOLE COMMAND NULL");
                return;
            }
            ulong searchID = 0;
            if (ulong.TryParse(arg.Args[0], out searchID) == false)
            {
                if (debug)PrintWarning($"SPAWN CONSOLE COMMAND - NOT ULONG");               
                return;
            }
            BasePlayer playerfound;
            foreach (var playerzonline in BasePlayer.activePlayerList.ToList())
            {
                if (playerzonline.userID == searchID)
                {
                    playerfound = playerzonline;
                    if (debug)Puts($"DESPAWN CONSOLE COMMAND ON PLAYER");
                    KillMyBalloon(playerfound, null, null);
                    return;
                }
            }
            if (debug)Puts($"PLAYER NOT FOUND");
        }

        [ChatCommand("noballoon")]         
        private void KillMyBalloonChatCommand(BasePlayer player, string command, string[] args)
        {
            bool isunballooner = permission.UserHasPermission(player.UserIDString, UnBallooner);
            if (isunballooner == false)
            {
                Player.Message(player, $"<color={ChatColor}><i>{lang.GetMessage("NoPermMsg", this, player.UserIDString)}</i></color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                return;
            }
            KillMyBalloon(player, null, null);
        }

        private void KillMyBalloon(BasePlayer player, string command, string[] args)
        {
            if (storedData.playerballoon.ContainsKey(player.userID) == true)
            {
                uint deluint;
                storedData.playerballoon.TryGetValue(player.userID, out deluint);
                var tokill = BaseNetworkable.serverEntities.Find(deluint);
                if (tokill != null) tokill.Kill();
                storedData.playerballoon.Remove(player.userID);
                //balloonlist.Remove(deluint);
                baseplayerballoon.Remove(player);
                CuiHelper.DestroyUi(player, DaBalloonBar);
                CuiHelper.DestroyUi(player, DaBalloonNavigatorBar);
                CuiHelper.DestroyUi(player, DaBalloonNavigatorBarWest);
                CuiHelper.DestroyUi(player, DaBalloonNavigatorBarEast);
                CuiHelper.DestroyUi(player, DaBalloonNavigatorBarNorth);
                CuiHelper.DestroyUi(player, DaBalloonNavigatorBarSouth);
            }
        }
#endregion
/////////// CHAT MESSAGE TO ONLINE PLAYER with ulong //////////////

        private void ChatPlayerOnline(ulong ailldi, string message)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
            {
                if (player.userID == ailldi)
                {
                    if (message == "killed")
                    {
                        Player.Message(player, $"<color={ChatColor}>{lang.GetMessage("KilledMsg", this, player.UserIDString)}</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                    }
                }
            }
        }

/////////////////// SET A SPAWN LOCATION TO RECALL : balloon_set balloon_call /////////////////////////////

#region Damage
////////////////////
// ON DAMAGE - chat owner and in future restore health to max
/////////////////////

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity == null) return;
            if (entity.net.ID == null)return;

            if (hitInfo.Initiator != null) NullMHABDamage(entity, hitInfo);

            HotAirBalloon check = entity as HotAirBalloon;
            if (check == null) return;

            if (storedData.playerballoon == null) return;
            foreach (var item in storedData.playerballoon)
            {
                if (item.Value == entity.net.ID)
                {
                    bool isgod = permission.UserHasPermission(item.Key.ToString(), GodBalloon);
                    if (isgod == true)
                    {
                        hitInfo.damageTypes.ScaleAll(0);
                    }
                }
            }
        }

        void NullMHABDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            HotAirBalloon balloon = hitInfo.Initiator as HotAirBalloon;
            if (balloon == null) return;
            BasePlayer goodguy = entity as BasePlayer;
            if (goodguy == null) return;
            hitInfo.damageTypes.ScaleAll(0);
        }

#endregion
#region HUD
/////////////////
// MAIN HUD
///////////////////////////

        void BalloonBar(BasePlayer player)
        {
			var CuiElement = new CuiElementContainer();
			DaBalloonBar = CuiElement.Add(new CuiPanel{Image ={Color = "0.0 0.0 0.0 0.2"},RectTransform ={AnchorMin = $"0.345 {barbottom}",AnchorMax = $"0.64 {barbottom+0.05}"},CursorEnabled = false
//                }, new CuiElement().Parent = "Overlay", DaBalloonBar);
            });

            var speedminus = CuiElement.Add(new CuiButton
                {Button ={Command = $"BalloonMinus",Color = $"1.0 0.5 0.5 0.2"},Text ={Text = $"-",Color = "1.0 1.0 1.0 1.0",FontSize = 16,Align = TextAnchor.MiddleCenter},
                    RectTransform ={AnchorMin = "0.26 0.10",   AnchorMax = "0.34 0.90"}
                }, DaBalloonBar);

            var speedplus = CuiElement.Add(new CuiButton
                {Button ={Command = $"BalloonPlus",Color = $"0.0 1.0 0.0 0.2"},Text ={Text = $"+",Color = "1.0 1.0 1.0 1.0",FontSize = 16,Align = TextAnchor.MiddleCenter},
                    RectTransform ={AnchorMin = "0.46 0.10",   AnchorMax = "0.53 0.90"}
                }, DaBalloonBar);

			CuiHelper.AddUi(player, CuiElement);
        }

        private void BalloonBarButtons(BasePlayer player, float fuelpersec, float inflation, float lift, float windforce, float entityhealth)
        {
            if (debug) Puts($"lift {lift}");
            bool isgod = permission.UserHasPermission(player.UserIDString, GodBalloon);
            string health = ($"{entityhealth.ToString()}/1500");
            if (isgod == true)
            {
                health = "GOD";
            }
            bool isfuel = permission.UserHasPermission(player.UserIDString, FuelRateBalloon);
            string fuelrated = ($"{fuelrate}");
            if (isfuel == true)
            {
                fuelrated = "UNLIMITED";
            }

            string locked = "N/A";
            string windforced = windforce.ToString();
            if (windforce == 600f)
            {
                windforced = "MIN";
            }
            if (windforce >= 3000f)
            {
                windforced = "MAX";
            }
            if (windforce == 0f)
            {
                windforced = "NAV";
            }
			    var CuiElement = new CuiElementContainer();

			    DaBalloonBarHealth = CuiElement.Add(new CuiLabel{Text ={Text = $"{lang.GetMessage("healthinfoMsg", this, player.UserIDString)} {health}",FontSize = 10,Align = TextAnchor.MiddleCenter,
                Color = "1 1 1 1"},RectTransform ={AnchorMin = "0.01 0.10",   AnchorMax = "0.25 0.90"}
				}, DaBalloonBar);

			    DaBalloonBarWind = CuiElement.Add(new CuiLabel{Text ={Text = $"{lang.GetMessage("windforceMsg", this, player.UserIDString)}\n{windforced}",FontSize = 10,Align = TextAnchor.MiddleCenter,
                Color = "1 1 1 1"},RectTransform ={AnchorMin = "0.35 0.10",   AnchorMax = "0.45 0.90"}
				}, DaBalloonBar);

			    /*DaBalloonBarInflation = CuiElement.Add(new CuiLabel{Text ={Text = $"{lang.GetMessage("inflationMsg", this, player.UserIDString)}\n{inflation}",FontSize = 10,Align = TextAnchor.MiddleCenter,
                Color = "1 1 1 1"},RectTransform ={AnchorMin = "0.54 0.10",   AnchorMax = "0.64 0.90"}
				}, DaBalloonBar);*/
                if (inflation == 1)
                {
                    DaBalloonBarupboost = CuiElement.Add(new CuiButton
                    {Button ={Command = $"BalloonBoost",Color = $"0.0 1.0 0.0 0.2"},Text ={Text = $"upBOOST",Color = "1.0 1.0 1.0 1.0",FontSize = 10,Align = TextAnchor.MiddleCenter},
                        RectTransform ={AnchorMin = "0.54 0.10",   AnchorMax = "0.68 0.90"}
                    }, DaBalloonBar);
                }

                DaBalloonBarLock = CuiElement.Add(new CuiLabel{Text ={Text = $"{lang.GetMessage("lockMsg", this, player.UserIDString)}\n{locked}",FontSize = 10,Align = TextAnchor.MiddleCenter,
                Color = "1 1 1 1"},RectTransform ={AnchorMin = "0.69 0.10",   AnchorMax = "0.81 0.90"}
				}, DaBalloonBar);
                
                DaBalloonBarFuel = CuiElement.Add(new CuiLabel{Text ={Text = $"{lang.GetMessage("fuelMsg", this, player.UserIDString)}\n{fuelrated}",FontSize = 10,Align = TextAnchor.MiddleCenter,
                Color = "1 1 1 1"},RectTransform ={AnchorMin = "0.82 0.10",   AnchorMax = "0.99 0.90"}
				}, DaBalloonBar);

			    CuiHelper.AddUi(player, CuiElement);
        }

        [ConsoleCommand("BalloonMinus")]
        private void BalloonMinus(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (NavigateMode.Contains(player))
            {
                Player.Message(player, $"<color={ChatColor}>{lang.GetMessage("NoSpeedMsg", this, player.UserIDString)}</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                return;
            }
            foreach (var item in baseplayerballoon)
            {
                if (item.Key == player)
                {
                    HotAirBalloon Balloon = item.Value;
                    Balloon.windForce = Balloon.windForce - 100f;
                    if (Balloon.windForce < 600f)
                    {
                        Balloon.windForce = 600f;
                    }
                }
            }
        }

        [ConsoleCommand("BalloonPlus")]
        private void BalloonPlus(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (NavigateMode.Contains(player))
            {
                Player.Message(player, $"<color={ChatColor}>{lang.GetMessage("NoSpeedMsg", this, player.UserIDString)}</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                return;
            }
            foreach (var item in baseplayerballoon)
            {
                if (item.Key == player)
                {
                    HotAirBalloon Balloon = item.Value;
                    Balloon.windForce = Balloon.windForce + 100f;
                    if (Balloon.windForce > 3000f)
                    {
                        Balloon.windForce = 3000f;
                    }
                }
            }
        }

        [ConsoleCommand("BalloonBoost")]
        private void BalloonUpBoost(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            foreach (var item in baseplayerballoon)
            {
                if (item.Key == player)
                {
                    HotAirBalloon Balloon = item.Value;
                    if (Balloon.inflationLevel == 1f)
                    {
                        Balloon.inflationLevel = 2f;
                        timer.Once(3f, () =>
                        {
                            Balloon.inflationLevel = 1f;
                        });
                    }
                }
            }
        }
#endregion
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            CuiHelper.DestroyUi(player, DaBalloonBar);
            CuiHelper.DestroyUi(player, DaBalloonNavigatorBar);
            CuiHelper.DestroyUi(player, DaBalloonBombBar);
        }

        private void BalloonTimer(HotAirBalloon Balloon, BaseEntity Loloon, BasePlayer player)
        {
            bool stand = new bool();
            Vector3 tonpere = new Vector3();
            Vector3 tamere = new Vector3();

           /* var zicontainer = Loloon.gameObject.GetComponent<LootContainer>();
            if (zicontainer !=null)
            {
                    Puts($"CONTAINER ON !!!!!");

            }*/


            Timer newtimer = timer.Every(2f, () =>
            {
                //Balloon.ScheduleOff();
                //Puts($"ScheduleOff");
                tonpere = new Vector3();
                tamere = new Vector3();
                CuiHelper.DestroyUi(player, DaBalloonBarHealth);
                CuiHelper.DestroyUi(player, DaBalloonBarWind);
                CuiHelper.DestroyUi(player, DaBalloonBarLock);
                CuiHelper.DestroyUi(player, DaBalloonBarFuel);
                CuiHelper.DestroyUi(player, DaBalloonBarupboost);
                /////// nav
                CuiHelper.DestroyUi(player, DaBalloonNavigatorBar);
                CuiHelper.DestroyUi(player, DaBalloonNavigatorBarWest);
                CuiHelper.DestroyUi(player, DaBalloonNavigatorBarEast);
                CuiHelper.DestroyUi(player, DaBalloonNavigatorBarNorth);
                CuiHelper.DestroyUi(player, DaBalloonNavigatorBarSouth);
                ///// bomb
                CuiHelper.DestroyUi(player, DaBalloonBombBar);


                tamere = Balloon.transform.position;

                if (!balloonposition.ContainsKey(Balloon))
                {
                    balloonposition.Add(Balloon, tamere);
                    if (debug)Puts($"POSITION VIERGE");
                }
                else
                {
                    balloonposition.TryGetValue(Balloon, out tonpere);
                    if (tonpere == tamere)
                    {
                        if (debug)Puts($"MEME POSITION");
                    }
                    else
                    {
                        balloonposition.Remove(Balloon);
                        balloonposition.Add(Balloon, tamere);
                        if (debug)Puts($"IS MOVING");
                        if (debug)Puts($"1 {tonpere}");
                        if (debug)Puts($"2 {tamere}");
                    }
                }
                float fuelpersec = Balloon.fuelPerSec;
                float entityhealth = Loloon.Health();
                BalloonBarButtons(player, Balloon.fuelPerSec, Balloon.inflationLevel, Balloon.liftAmount, Balloon.windForce, entityhealth);
                NavigateMyBalloon(Balloon, Loloon, player);
            });
            balloontimer.Remove(Balloon);
            balloontimer.Add(Balloon, newtimer);
        }

#region OnKill
////////////////////// ON KILL - chat owner /////////////////////

        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null) return;
            if (entity.net.ID == null)return;
            HotAirBalloon check = entity as HotAirBalloon;
            if (check == null) return;
            if (storedData.playerballoon == null) return;
            foreach (var item in storedData.playerballoon)
            {
                if (item.Value == entity.net.ID)
                {
                    ChatPlayerOnline(item.Key, "killed");
                    storedData.playerballoon.Remove(item.Value);
                    foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
                    {
                        if (player.userID == item.Key)
                        {
                            baseplayerballoon.Remove(player);
                            NavigateMode.Remove(player);
                            BalloonWest.Remove(player);
                            BalloonEast.Remove(player);
                            BalloonNorth.Remove(player);
                            BalloonSouth.Remove(player);
                        }                       
                    }
                }
            }

/// KILL TIMER
            foreach (var item in balloontimer)
            {
                Timer timertokill;
                if (item.Key == check)
                {
                    timertokill = item.Value;
                    timertokill.Destroy();
                    //balloontimer.Remove(item.Value);
                }
            }
        }
#endregion
    }
}