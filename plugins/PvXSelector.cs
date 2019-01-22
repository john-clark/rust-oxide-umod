using System;                      //DateTime
using System.Linq;
//using System.Data;
//using Oxide.Plugins;
//using Oxide.Core.CSharp;
//using System.Reflection;
//using System.Collections;
//using System.Globalization;
using System.Collections.Generic;  //Required for Whilelist
//using System.Text.RegularExpressions;
//using System.Runtime.CompilerServices;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
//using Oxide.Core.Database;
//using Oxide.Core.Libraries;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence; //Requrired for IPlayer stuff
using Rust;
//using Rust.Ai;
//using Rust.AI;
//using RustNative;
//using Rust.Registry;
using UnityEngine;
//using UnityEngine.AI;
//using ConVar;
//using Facepunch;
//using Network;
//using ProtoBuf;

namespace Oxide.Plugins
{
    [Info("PvX Selector", "Alphawar", "0.1.0")]
    [Description("Allow players to play both PvP and PvE on the same server.")]
    class PvXSelector : RustPlugin
    {
        private readonly string PluginName = "pvxselector";
        //temp variables
        


        private HashSet<BaseHelicopter> BaseHelicopters = new HashSet<BaseHelicopter>();
        private HashSet<CH47HelicopterAIController> ChinooksHelicopters = new HashSet<CH47HelicopterAIController>();
        private HashSet<BradleyAPC> BradleyAPCs = new HashSet<BradleyAPC>();
        private HashSet<FireBall> HeliFireBalls = new HashSet<FireBall>();
        private HashSet<FireBall> HeliRocket = new HashSet<FireBall>();

        private readonly string heliPrefab = "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab";
        private readonly string chinookPrefab = "assets/prefabs/npc/ch47/ch47scientists.entity.prefab";

        private readonly int groundLayer = LayerMask.GetMask("Terrain", "World", "Default");
        //Dictionary

        // Enums
        private enum PvXState { NA, PvP, PvE, AI, Unknown, Sleeper };
        private enum PvXNotification { NA, Accepted, Delcined, Timed, NoMode }

        // Variables.
        #region Variables
        bool initialized = false;

        static readonly string PvxIndicatorUI = "PvXPlayerStateIndicator"; // used to show player their mode
        static readonly string PvxAdminUI = "pvxAdminTicketCountUI"; //admin gui to indicate tickets.
        static readonly string[] GuiList = new string[] {
            PvxIndicatorUI,
            PvxAdminUI
        };

        #endregion

        #region Config
        private ConfigData configData;
        class ConfigData
        {
            public Options Options { get; set; }
            public Gui Gui { get; set; }
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);

        class Options
        {
            public bool AllowTickets { get; set; }
            public bool AllowCooldowns { get; set; }
            public float ModeSwitchCooldown { get; set; }
        }
        class Gui
        {
            public bool DisableUI_FadeIn;
            public float playerIndicatorMinWid;
            public float playerIndicatorMinHei;
            public float adminIndicatorMinWid;
            public float adminIndicatorMinHei;
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                Options = new Options
                {
                    AllowTickets = true,
                    AllowCooldowns = true,
                    ModeSwitchCooldown = 2f,
                },
                Gui = new Gui
                {
                    DisableUI_FadeIn = false,
                    playerIndicatorMinWid = 0.484f,
                    playerIndicatorMinHei = 0.111f,
                    adminIndicatorMinWid = 0.16f,
                    adminIndicatorMinHei = 0.06f,
                }
            };
            SaveConfig(config);
        }
        #endregion

        #region Data
        private static PvXSelector PvX;

        private static Dictionary<ulong, PvXPlayer> PvXPlayers = new Dictionary<ulong, PvXPlayer>();
        private static Dictionary<ulong, double> PvXCooldowns = new Dictionary<ulong, double>();
        private static Dictionary<int, PvXTicket> PvXTickets = new Dictionary<int, PvXTicket>();
        private static Dictionary<int, PvXLog> PvXLogs = new Dictionary<int, PvXLog>();

        private static readonly List<object> BuildEntityList = new List<object>() {
            typeof(AutoTurret),typeof(Barricade),typeof(BaseCombatEntity),
            typeof(BaseOven),typeof(BearTrap),typeof(BuildingBlock),
            typeof(BuildingPrivlidge),typeof(CeilingLight),typeof(Door),
            typeof(Landmine),typeof(LiquidContainer),typeof(ReactiveTarget),
            typeof(RepairBench),typeof(ResearchTable),typeof(Signage),
            typeof(SimpleBuildingBlock),typeof(SleepingBag),typeof(StabilityEntity),
            typeof(StorageContainer),typeof(SurvivalFishTrap),typeof(WaterCatcher),
            typeof(WaterPurifier)};
        private static readonly List<object> BasePartEntityList = new List<object>() {
            typeof(BaseOven),typeof(BuildingBlock),typeof(BuildingPrivlidge),
            typeof(CeilingLight),typeof(Door),typeof(LiquidContainer),
            typeof(RepairBench),typeof(ResearchTable),typeof(Signage),
            typeof(SimpleBuildingBlock),typeof(SleepingBag),typeof(StabilityEntity),
            typeof(StorageContainer),typeof(SurvivalFishTrap),typeof(WaterCatcher),
            typeof(WaterPurifier)};
        private static readonly List<object> CombatPartEntityList = new List<object>() {
            typeof(AutoTurret),typeof(Barricade),typeof(BearTrap),typeof(Landmine),
            typeof(ReactiveTarget),typeof(BaseCombatEntity)};
        private static readonly List<object> AnimalList = new List<object>() {
            typeof(Bear),
            typeof(Boar),
            typeof(Chicken),
            typeof(Horse),
            typeof(Stag),
            typeof(Wolf)
        };

        private static List<ulong> AdminsActive = new List<ulong>();
        private static List<ulong> AdminsOnline = new List<ulong>();
        private static Dictionary<ulong,PvXTicket> AdminViewedTicket = new Dictionary<ulong, PvXTicket>();

        TicketDataManager ticketManager;
        PlayerDataManager playerManager;
        LogDataManager logManager;

        #endregion

        #region Classes
        private class PvXPlayer
        {
            public string Username { get; set; }
            public string ConnectionFirst { get; set; }
            public string ConnectionLast { get; set; }
            public PvXState State { get; set; }
            public PvXNotification Notification { get; set; }
            public int TicketID { get; set; }
            public bool Ticket { get; set; }
            public double LastRequestStamp { get; set; }
        }
        private class PvXTicket
        {
            public ulong ID { get; set; }
            public string Name { get; set; }
            public string Reason { get; set; }
            public string TimeStampString { get; set; }
            public double TimeStamp { get; set; }
            public int TicketID { get; set; }
        }
        private class PvXLog
        {
            public ulong PlayerID { get; set; }
            public string PlayerName { get; set; }
            public ulong AdminID { get; set; }
            public string AdminName { get; set; }
            public string Reason { get; set; }
            public string CreatedTimeStamp { get; set; }
            public string ReviewedTimeStamp { get; set; }
            public string Outcome { get; set; }
        }
        class PlayerDataManager
        {
            //Variables
            private PlayerData playerData;
            private DynamicConfigFile PlayerDataFile;

            //Classes
            class PlayerData
            {
                public Dictionary<ulong, PvXPlayer> StoredPlayers = new Dictionary<ulong, PvXPlayer>();
                public Dictionary<ulong, double> StoredCooldowns = new Dictionary<ulong, double>();
            }

            //Functions
            internal PlayerDataManager()
            {
                //Load Mod, Start any timers
                PvX.Puts("Creating TicketManager Class");
                Initiate();
                Load();
            }
            private void Initiate()
            {
                PlayerDataFile = Interface.Oxide.DataFileSystem.GetFile("PvX/PlayerData");
            }
            private void Load()
            {
                try
                {
                    PvX.Puts("Loading Player Data");
                    playerData = PlayerDataFile.ReadObject<PlayerData>();
                    PvXPlayers = playerData.StoredPlayers;
                    PvXCooldowns = playerData.StoredCooldowns;
                }
                catch
                {
                    PvX.Puts("Couldn't load Player Data, Creating New file");
                    playerData = new PlayerData();
                    PvXPlayers = playerData.StoredPlayers;
                    PvXCooldowns = playerData.StoredCooldowns;
                }
            }
            public void Save()
            {
                playerData.StoredPlayers = PvXPlayers;
                playerData.StoredCooldowns = PvXCooldowns;
                PlayerDataFile.WriteObject(playerData);
            }
        }
        class TicketDataManager
        {
            //Variables
            private TicketData ticketData;
            private DynamicConfigFile TicketDataFile;

            //Classes
            class TicketData
            {
                public Dictionary<int, PvXTicket> StoredTickets = new Dictionary<int, PvXTicket>();
            }

            //Functions
            public TicketDataManager()
            {
                //Load Mod, Start any timers
                PvX.Puts("Creating TicketManager Class");
                Initiate();
                Load();
            }
            private void Initiate()
            {
                TicketDataFile = Interface.Oxide.DataFileSystem.GetFile("PvX/TicketData");
            }
            private void Load()
            {
                try
                {
                    PvX.Puts("Loading Ticket Data");
                    ticketData = TicketDataFile.ReadObject<TicketData>();
                    PvXTickets = ticketData.StoredTickets;
                }
                catch
                {
                    PvX.Puts("Couldn't load Ticket Data, Creating New file");
                    ticketData = new TicketData();
                    PvXTickets = ticketData.StoredTickets;
                }
            }
            public void Save()
            {
                ticketData.StoredTickets = PvXTickets;
                TicketDataFile.WriteObject(ticketData);
            }
        }
        class LogDataManager
        {
            //Variables
            private LogData logData;
            private DynamicConfigFile LogDataFile;

            //Classes
            class LogData
            {
                public Dictionary<int, PvXLog> StoredLogs = new Dictionary<int, PvXLog>();
            }

            //Functions
            internal LogDataManager()
            {
                //Load Mod, Start any timers
                PvX.Puts("Creating LogManager Class");
                Initiate();
                Load();
            }
            private void Initiate()
            {
                LogDataFile = Interface.Oxide.DataFileSystem.GetFile("PvX/LogData");
            }
            private void Load()
            {
                try
                {
                    PvX.Puts("Loading Log Data");
                    logData = LogDataFile.ReadObject<LogData>();
                    PvXLogs = logData.StoredLogs;
                }
                catch
                {
                    PvX.Puts("Couldn't load Log Data, Creating New file");
                    logData = new LogData();
                    PvXLogs = logData.StoredLogs;
                }
            }
            public void Save()
            {
                logData.StoredLogs = PvXLogs;
                LogDataFile.WriteObject(logData);
            }

        }
        #endregion

        #region Functions

        void Init()
        {
            PvX = this;
            LoadDefaultMessages();
            RegisterPermissions();
            //AddCovalenceCommand();
            ticketManager = new TicketDataManager();
            playerManager = new PlayerDataManager();
            logManager = new LogDataManager();
            lang.RegisterMessages(Messages, this);
        }

        void OnServerInitialized()
        {
            LoadVariables();

            timer.Every(20f, () => CheckHelicopter());
            BaseHelicopters = new HashSet<BaseHelicopter>(GameObject.FindObjectsOfType<BaseHelicopter>());
            ChinooksHelicopters = new HashSet<CH47HelicopterAIController>(GameObject.FindObjectsOfType<CH47HelicopterAIController>());
            BradleyAPCs = new HashSet<BradleyAPC>(GameObject.FindObjectsOfType<BradleyAPC>());
            HeliFireBalls = new HashSet<FireBall>(BaseNetworkable.serverEntities?.Where(p => p != null && p is FireBall && (p.PrefabName.Contains("napalm") || p.PrefabName.Contains("oil")))?.Select(p => p as FireBall) ?? null);

            initialized = true;
            foreach (BasePlayer Player in BasePlayer.activePlayerList)
            {
                OnPlayerInit(Player);
            }
        }

        // Need to save data when mod is unloaded
        void Unload()
        {
            SaveAll();
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                DestroyAllPvXUI(player);
            }
        }

        void OnPlayerInit(BasePlayer player)
        {
            Puts("OnPlayerInit being called");
            IPlayer iplayer = FindIPlayer(player.UserIDString);
            if (IsNPC(player.userID)) return; // Then check if NPC
            else if (!player.IsConnected)
            {
                timer.Once(3f, () =>
                {
                    OnPlayerInit(player);
                });
                return;
            }//Since player make sure they have "Joined" the game"
            else PlayerLoaded(iplayer); // Now run PvX PlayerLoaded function
        }

        void PlayerLoaded(IPlayer iplayer)
        {
            BasePlayer player = FindBasePlayer(iplayer.Id);
            //Functions to check if new player, old player, unknown/sleeper player.
            if (PvXPlayers.ContainsKey(player.userID))
            {
                Puts("PvXPlayers Contains player");
                if (PvXPlayers[player.userID].State == PvXState.Unknown || PvXPlayers[player.userID].State == PvXState.Sleeper)
                {
                    // Remove player to read clean version, keepingh specific date info.
                    PvXPlayers.Remove(player.userID);
                    PvXPlayers.Add(player.userID, CreatePlayerData(player));
                }
            }
            else
            {
                PvXPlayers.Add(player.userID, CreatePlayerData(player));
                iplayer.Reply(GetMSG("WelcomeMessage", iplayer.Id));
            }

            if (HasPerm(player, "admin"))
            {
                AdminsOnline.Add(player.userID);
                AdminIndicatorGui(player);
            }

            // Update players last connection time to today
            PvXPlayers[player.userID].ConnectionLast = GetDateStamp();
            
            if (PvXPlayers[player.userID].State == PvXState.NA)
            {
                PlayerNotification(iplayer);
            }
            else if (PvXPlayers[player.userID].Notification != PvXNotification.NA)
            {
                PlayerNotification(iplayer);
            }
            PlayerIndicatorGui(player);
        }

        void RegisterPermissions()
        {
            string[] Permissionarray = { "admin", "moderator", "wipe" }; //DO NOT EVER TOUCH THIS EVER!!!!!!
            foreach (string i in Permissionarray)
            {
                string regPerm = PluginName.ToLower() + "." + i;
                if (!permission.PermissionExists(regPerm))
                {
                    permission.RegisterPermission(regPerm, this);
                }
            }
        }

        private PvXPlayer CreatePlayerData(BasePlayer player)
        {
            PvXPlayer Player = new PvXPlayer
            {
                ConnectionFirst = GetDateStamp(),
                ConnectionLast = GetDateStamp(),
                LastRequestStamp = 0,
                Notification = PvXNotification.NoMode,
                State = PvXState.NA,
                Username = player.displayName,
                Ticket = false,
                TicketID = 0
            };
            return Player;
        }
        private PvXTicket CreateTicketData(BasePlayer player, int ticketnumber)
        {
            PvXTicket ticket = new PvXTicket
            {
                ID = player.userID,
                Name = player.displayName,
                Reason = "Null",
                TimeStamp = GetTimeStamp(),
                TimeStampString = GetDateStamp(),
                TicketID = ticketnumber
            };
            return ticket;
        }


        private int GetNewLogID()
        {
            if (PvXLogs.Count > 1000)
            {
                return 0;
            }
            for (int _i = 1; _i <= 1000; _i++)
            {
                if (PvXLogs.ContainsKey(_i))
                {

                }
                else
                {
                    //Puts("Key {0} doesnt exist, Returning ticket number", _i); //debug
                    return _i;
                }
            }
            return 0;
        }
        private int GetNewTicketID()
        {
            if (PvXTickets.Count > 1000)
            {
                return 0;
            }
            for (int _i = 1; _i <= 1000; _i++)
            {
                if (PvXTickets.ContainsKey(_i))
                {

                }
                else
                {
                    return _i;
                }
            }
            return 0;
        }

        private string GetDateStamp()
        {
            return DateTime.Now.ToString("HH:mm dd-MM-yyyy");
        }
        private double GetTimeStamp()
        {
            return (DateTime.Now.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
        }

        
        private bool HasPerm(BasePlayer Player, string perm, string reason = null)
        {
            string regPerm = Title.ToLower() + "." + perm; //pvxselector.admin
            if (permission.UserHasPermission(Player.UserIDString, regPerm)) return true;
            if (reason != "null")
                SendReply(Player, reason);
            return false;
        }

        public static BasePlayer FindBasePlayer(string StringID)
        {
            ulong ID = Convert.ToUInt64(StringID);
            BasePlayer BasePlayer = BasePlayer.FindByID(ID);
            if (BasePlayer == null) BasePlayer = BasePlayer.FindSleeping(ID);
            return BasePlayer;
        }
        public static IPlayer FindIPlayer(string StringID)
        {
            return PvX.covalence.Players.FindPlayerById(StringID);
        }


        private PvXState GetObjectPvXState(BaseEntity baseEntity)
        {
            if (baseEntity is BasePlayer)
            {
                BasePlayer player = (BasePlayer)baseEntity;
                if (player == null) return PvXState.Unknown;
                else if (PvXPlayers.ContainsKey(player.userID))
                {
                    return PvXPlayers[player.userID].State;
                }
                else return PvXState.Unknown;
            }
            else return PvXState.NA;
        }


        private string GetPvXStateString (PvXState state)
        {
            string value;
            switch (state)
            {
                case PvXState.AI:
                    value = "AI";
                    break;

                case PvXState.NA:
                    value = "NA";
                    break;

                case PvXState.PvE:
                    value = "PvE";
                    break;

                case PvXState.PvP:
                    value = "PvP";
                    break;

                case PvXState.Sleeper:
                    value = "Sleeper";
                    break;

                case PvXState.Unknown:
                    value = "Unknown";
                    break;
                default:
                    value = "Error";
                    break;
            }
            return value;
        }

        private void SaveAll()
        {
            playerManager.Save();
            ticketManager.Save();
            logManager.Save();
        }
        private void SavePlayerTicket()
        {
            playerManager.Save();
            ticketManager.Save();
        }


        private void CheckHelicopter()
        {
            BaseHelicopters.RemoveWhere(p => (p?.IsDestroyed ?? true));
            ChinooksHelicopters.RemoveWhere(p => (p?.IsDestroyed ?? true));
            HeliFireBalls.RemoveWhere(p => (p?.IsDestroyed ?? true));
            BradleyAPCs.RemoveWhere(p => (p?.IsDestroyed ?? true));
        }

        private int BaseHeliCount { get { return BaseHelicopters.Count; } }

        private int CH47Count { get { return ChinooksHelicopters.Count; } }

        private int BradleyCount { get { return BradleyAPCs.Count; } }

        private int HeliCounts ()
        {
            return (BaseHeliCount + CH47Count);
        }

        #endregion

        #region Chat Commands
        [ChatCommand("pvx")]
        void PvXChatCmd(BasePlayer player, string cmd, string[] args)
        {
            SaveAll();
               IPlayer iplayer = FindIPlayer(player.UserIDString);
            if (args.Count() == 0)
            {
                iplayer.Reply(GetMSG("PvX Chat Instructions 1", player.UserIDString));
                iplayer.Reply(GetMSG("PvX Chat Instructions 2", player.UserIDString));
                iplayer.Reply(GetMSG("PvX Chat Instructions 3", player.UserIDString));
            }
            else if (args.Count() > 0)
            {
                double CalculatedCooldown = (PvXPlayers[player.userID].LastRequestStamp + (configData.Options.ModeSwitchCooldown * 60f));
                switch (args[0].ToLower())
                {
                    case "pve":
                        if (PvXPlayers[player.userID].State == PvXState.NA)
                        {
                            PvXPlayers[player.userID].State = PvXState.PvE;
                            PvXPlayers[player.userID].Notification = PvXNotification.NA;
                            PvXPlayers[player.userID].LastRequestStamp = GetTimeStamp();
                            
                            playerManager.Save();
                            iplayer.Reply(GetMSG("PlayerModeChange", iplayer.Id, "PvE"));
                            UpdatePlayersGui(player);
                        }
                        else
                        {
                            iplayer.Reply(GetMSG("ChatInstructionChange",iplayer.Id));
                        }
                        break;

                    case "pvp":
                        if (PvXPlayers[player.userID].State == PvXState.NA)
                        {
                            PvXPlayers[player.userID].State = PvXState.PvP;
                            PvXPlayers[player.userID].Notification = PvXNotification.NA;
                            PvXPlayers[player.userID].LastRequestStamp = GetTimeStamp();

                            playerManager.Save();
                            iplayer.Reply(GetMSG("PlayerModeChange", iplayer.Id, "PvP"));
                            UpdatePlayersGui(player);
                        }
                        else
                        {
                            iplayer.Reply(GetMSG("ChatInstructionChange", iplayer.Id));
                        }

                        break;

                    case "change":
                        if (!PvXPlayers.ContainsKey(player.userID))
                        {
                            iplayer.Reply(GetMSG("ChatInstructionPvX", iplayer.Id));
                            return;
                        }

                        if (configData.Options.AllowCooldowns)
                        {
                            if (CalculatedCooldown <= GetTimeStamp())
                            {
                                if (PvXPlayers[player.userID].State == PvXState.PvP)
                                {
                                    PvXPlayers[player.userID].State = PvXState.PvE;
                                    PvXPlayers[player.userID].LastRequestStamp = GetTimeStamp();
                                    iplayer.Reply(GetMSG("PlayerModeChange", iplayer.Id, "PvE"));
                                    playerManager.Save();
                                    UpdatePlayersGui(player);
                                }
                                else if (PvXPlayers[player.userID].State == PvXState.PvE)
                                {
                                    PvXPlayers[player.userID].State = PvXState.PvP;
                                    PvXPlayers[player.userID].LastRequestStamp = GetTimeStamp();
                                    iplayer.Reply(GetMSG("PlayerModeChange", iplayer.Id, "PvP"));
                                    playerManager.Save();
                                    UpdatePlayersGui(player);
                                }
                                else
                                {
                                    iplayer.Reply(GetMSG("ChatInstructionPvX", iplayer.Id));
                                }
                            }
                            else if (configData.Options.AllowTickets)
                            {
                                int TicketNumber = GetNewTicketID();
                                PvXTickets.Add(TicketNumber, CreateTicketData(player, TicketNumber));
                                iplayer.Reply(GetMSG("PlayerTicketCreated",iplayer.Id));
                                PvXPlayers[player.userID].Ticket = true;
                                PvXPlayers[player.userID].TicketID = TicketNumber;
                                SavePlayerTicket();
                                UpdateAdminsGui();
                                UpdatePlayersGui(player);
                            }
                            else
                            {
                                double TimeRemaining = (CalculatedCooldown-GetTimeStamp())/60f;
                                // Notify player of remaining time before they can 
                                iplayer.Reply(GetMSG("PlayerTimeRemainingVar",iplayer.Id, TimeRemaining.ToString("n2")));
                            }
                        }
                        else if (configData.Options.AllowTickets)
                        {
                            int TicketNumber = GetNewTicketID();
                            PvXTickets.Add(TicketNumber, CreateTicketData(player, TicketNumber));
                            iplayer.Reply(GetMSG("PlayerTicketCreated", iplayer.Id));
                            PvXPlayers[player.userID].Ticket = true;
                            PvXPlayers[player.userID].TicketID = TicketNumber;
                            UpdateAdminsGui();
                        }
                        else
                        {
                            Puts("you fked up, no ticket or cooldown option set for PvX");
                        }
                        break;

                    case "ticket":
                        if (PvXPlayers[player.userID].Ticket)
                        {
                            iplayer.Reply(GetMSG("PlayerHasTicketVar", iplayer.Id, PvXPlayers[player.userID].TicketID));
                        }
                        else
                        {
                            iplayer.Reply(GetMSG("PlayerNoTicket", iplayer.Id));
                        }

                        break;

                    case "tickets":
                        if (HasPerm(player, "admin", GetMSG("MissingPermisionVar", iplayer.Id, "admin")))
                        {
                            if (args.Count() > 1)
                            {
                                switch (args[1].ToLower())
                                {
                                    case "list":
                                        if (PvXTickets.Count() == 0)
                                        {
                                            iplayer.Reply(GetMSG("AdminNoTickets",iplayer.Id));
                                        }
                                        foreach (int TicketNumber in PvXTickets.Keys)
                                        {
                                            iplayer.Reply(GetMSG("TicketList1", iplayer.Id, TicketNumber));
                                            iplayer.Reply(GetMSG("TicketList2", iplayer.Id, PvXTickets[TicketNumber].Name, GetPvXStateString(PvXPlayers[PvXTickets[TicketNumber].ID].State)));
                                        }

                                        break;

                                    case "view":
                                        if (args.Count() > 2)
                                        {
                                            int TicketNumber = 0;

                                            if (Int32.TryParse(args[2], out TicketNumber))
                                            {
                                                if (PvXTickets.ContainsKey(TicketNumber))
                                                {
                                                    //Add or Update Ticket View to adminviewlist.
                                                    if (AdminViewedTicket.ContainsKey(player.userID))
                                                    {
                                                        AdminViewedTicket[player.userID] = PvXTickets[TicketNumber];
                                                    }
                                                    else
                                                    {
                                                        AdminViewedTicket.Add(player.userID, PvXTickets[TicketNumber]);
                                                    }

                                                    iplayer.Reply(GetMSG("AdminViewTicket1Var", iplayer.Id, TicketNumber));
                                                    iplayer.Reply(GetMSG("AdminViewTicket2Var", iplayer.Id, PvXTickets[TicketNumber].Name));
                                                    iplayer.Reply(GetMSG("AdminViewTicket3Var", iplayer.Id, PvXTickets[TicketNumber].TimeStampString));
                                                    iplayer.Reply(GetMSG("AdminViewTicket4Var", iplayer.Id, PvXTickets[TicketNumber].Reason));
                                                    iplayer.Reply(GetMSG("AdminViewTicket5", iplayer.Id));
                                                }
                                            }
                                        }

                                        break;

                                    default:

                                        break;
                                }
                            }
                        }

                        break;

                    case "accept":
                        if (HasPerm(player, "admin", GetMSG("MissingPermisionVar", iplayer.Id, "admin")))
                        {
                            if (AdminViewedTicket.ContainsKey(player.userID))
                            {
                                PvXTicket ViewedTicket = AdminViewedTicket[player.userID];
                                if (PvXTickets[ViewedTicket.TicketID] == ViewedTicket)
                                {
                                    int TicketNumber = ViewedTicket.TicketID;
                                    ulong TicketPlayerID = PvXTickets[TicketNumber].ID;
                                    IPlayer TicketIplayer = FindIPlayer(TicketPlayerID.ToString());
                                    BasePlayer TicketPlayer = FindBasePlayer(TicketPlayerID.ToString());

                                    iplayer.Reply(GetMSG("AdminTicketAcceptedVar", iplayer.Id, ViewedTicket.Name));
                                    PvXLog log = new PvXLog
                                    {
                                        AdminID = player.userID,
                                        AdminName = player.displayName,
                                        CreatedTimeStamp = PvXTickets[TicketNumber].TimeStampString,
                                        PlayerID = PvXTickets[TicketNumber].ID,
                                        PlayerName = PvXTickets[TicketNumber].Name,
                                        Reason = PvXTickets[TicketNumber].Reason,
                                        ReviewedTimeStamp = GetDateStamp(),
                                        Outcome = "Accepted"
                                    };
                                    PvXLogs.Add(GetNewLogID(), log);
                                    PvXTickets.Remove(TicketNumber);
                                    if (PvXPlayers[TicketPlayerID].State == PvXState.PvP)
                                    {
                                        PvXPlayers[TicketPlayerID].State = PvXState.PvE;
                                        PvXPlayers[TicketPlayerID].LastRequestStamp = GetTimeStamp();
                                    }
                                    else
                                    {
                                        PvXPlayers[TicketPlayerID].State = PvXState.PvP;
                                        PvXPlayers[TicketPlayerID].LastRequestStamp = GetTimeStamp();
                                    }
                                    PvXPlayers[TicketPlayerID].Ticket = false;
                                    PvXPlayers[TicketPlayerID].TicketID = 0;

                                    if (TicketPlayer.IsConnected)
                                    {
                                        UpdatePlayersGui(player);
                                        TicketIplayer.Reply(GetMSG("PlayerTicketAcceptedVar", TicketIplayer.Id, GetPvXStateString(PvXPlayers[TicketPlayerID].State)));
                                    }
                                    else
                                    {
                                        PvXPlayers[TicketPlayerID].Notification = PvXNotification.Accepted;
                                    }
                                    SaveAll();
                                }
                                else
                                {
                                    iplayer.Reply(GetMSG("AdminTicketsDontMatch", iplayer.Id));
                                    AdminViewedTicket.Remove(player.userID);
                                }
                                AdminViewedTicket.Remove(player.userID);
                            }
                            else
                            {
                                iplayer.Reply(GetMSG("AdminNoViewTicket", iplayer.Id));
                            }
                        }

                        break;

                    case "decline":
                        if (HasPerm(player, "admin", GetMSG("MissingPermisionVar", iplayer.Id, "admin")))
                        {
                            if (AdminViewedTicket.ContainsKey(player.userID))
                            {
                                PvXTicket ViewedTicket = AdminViewedTicket[player.userID];
                                if (PvXTickets[ViewedTicket.TicketID] == ViewedTicket)
                                {

                                    iplayer.Reply(GetMSG("AdminTicketDeclinedVar", iplayer.Id, ViewedTicket.Name));
                                    int TicketNumber = ViewedTicket.TicketID;
                                    ulong TicketPlayerID = PvXTickets[TicketNumber].ID;
                                    IPlayer TicketIplayer = FindIPlayer(TicketPlayerID.ToString());
                                    BasePlayer TicketPlayer = FindBasePlayer(TicketPlayerID.ToString());

                                    PvXLog log = new PvXLog
                                    {
                                        AdminID = player.userID,
                                        AdminName = player.displayName,
                                        CreatedTimeStamp = PvXTickets[TicketNumber].TimeStampString,
                                        PlayerID = PvXTickets[TicketNumber].ID,
                                        PlayerName = PvXTickets[TicketNumber].Name,
                                        Reason = PvXTickets[TicketNumber].Reason,
                                        ReviewedTimeStamp = GetDateStamp(),
                                        Outcome = "Declined"
                                        
                                    };
                                    PvXLogs.Add(GetNewLogID(), log);
                                    PvXTickets.Remove(TicketNumber);
                                    PvXPlayers[TicketPlayerID].Ticket = false;
                                    PvXPlayers[TicketPlayerID].TicketID = 0;

                                    if (TicketPlayer.IsConnected)
                                    {
                                        UpdatePlayersGui(player);
                                        TicketIplayer.Reply(GetMSG("PlayerTicketDeclined", TicketIplayer.Id, GetPvXStateString(PvXPlayers[TicketPlayerID].State)));
                                    }
                                    else
                                    {
                                        PvXPlayers[TicketPlayerID].Notification = PvXNotification.Delcined;
                                    }
                                    SaveAll();
                                }
                                else
                                {
                                    iplayer.Reply(GetMSG("AdminTicketsDontMatch", iplayer.Id));
                                }
                                AdminViewedTicket.Remove(player.userID);
                            }
                            else
                            {
                                iplayer.Reply(GetMSG("AdminNoViewTicket", iplayer.Id));
                            }
                        }

                        break;

                    case "reason":
                        if (args.Count() == 2)
                        {
                            if (PvXPlayers[player.userID].Ticket)
                            {
                                int TicketNumber = PvXPlayers[player.userID].TicketID;
                                PvXTickets[TicketNumber].Reason = args[1];
                                ticketManager.Save();
                            }
                        }
                        else
                        {
                            iplayer.Reply(GetMSG("PlayerReasonIncorrect", iplayer.Id));
                        }

                        break;

                    default:

                        break;
                }
            }
        }

        #endregion

        #region Combat Handle
        
        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity == null || !initialized) return;
            var prefabname = entity?.ShortPrefabName ?? string.Empty;
            var longprefabname = entity?.PrefabName ?? string.Empty;
            if (string.IsNullOrEmpty(prefabname) || string.IsNullOrEmpty(longprefabname)) return;
            
            if (entity is CH47HelicopterAIController)
            {
                ChinooksHelicopters.Add((entity as CH47HelicopterAIController));
                return;
            }

            if (entity is BradleyAPC)
            {
                BradleyAPCs.Add((entity as BradleyAPC));
            }

            if (prefabname.Contains("patrolhelicopter") && !prefabname.Contains("gibs"))
            {
                var AIHeli = entity?.GetComponent<PatrolHelicopterAI>() ?? null;
                var BaseHeli = entity?.GetComponent<BaseHelicopter>() ?? null;
                if (AIHeli == null || BaseHeli == null) return;
                BaseHelicopters.Add(BaseHeli);
                return;
            }

            var ownerID = (entity as BaseEntity)?.OwnerID ?? 0;

            if ((prefabname.Contains("napalm") || prefabname.Contains("oilfireball")) && !prefabname.Contains("rocket"))
            {
                var fireball = entity?.GetComponent<FireBall>() ?? null;
                if (fireball == null) return;
                Puts("Entity is Fireball from helicopter");
                return;
            }

            if (prefabname.Contains("rocket_heli"))
            {
                //Puts(prefabname.ToString());
                //entity.Kill();
            }

            if (prefabname.Contains("maincannonshell")){
                if (entity is TimedExplosive)
                {
                    if (BradleyAPCs.Count > 0)
                    {
                        // maybe do something here
                    }
                    else
                    {
                        // Delete the item
                    }
                }
            }

            //else
            //{
            //    Puts("Entity Name: " + entity.name);
            //    Puts("Entity Prefab Name: " + entity.PrefabName);
            //    Puts("Entity Short Prefab Name: " + entity.ShortPrefabName);
            //    Puts("Entity GameObject: " + entity.gameObject);
            //}


            //if (entity is BaseEntity)
            //{
            //    BaseEntity thing = (BaseEntity)entity;
            //    if (thing is BaseCombatEntity)
            //    {
            //        BaseCombatEntity baseCombatEntity;
            //    }
            //    Puts("OwnerID");
            //    Puts(thing.OwnerID.ToString());
            //    Puts("Creator entity");
            //}
        }

        //void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        //{
        //    Puts("OnEntityDeath works!");
        //}

        //void OnEntityEnter(TriggerBase trigger, BaseEntity entity)
        //{
        //    Puts("OnEntityEnter works!");
        //}

        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null) return;
            var name = entity?.ShortPrefabName ?? string.Empty;
            if (name.Contains("patrolhelicopter") && !name.Contains("gib"))
            {
                var baseHeli = entity?.GetComponent<BaseHelicopter>() ?? null;
                if (baseHeli == null) return;
                if (BaseHelicopters.Contains(baseHeli)) BaseHelicopters.Remove(baseHeli);
            }
            if (entity is CH47HelicopterAIController)
            {
                var CH47 = entity?.GetComponent<CH47HelicopterAIController>() ?? null;
                if (CH47 == null) return;
                if (ChinooksHelicopters.Contains(CH47)) ChinooksHelicopters.Remove(CH47);
            }
            if (entity is BradleyAPC)
            {
                var bradley = entity?.GetComponent<BradleyAPC>() ?? null;
                if (bradley == null) return;
                if (BradleyAPCs.Contains(bradley)) BradleyAPCs.Remove(bradley);
            }
            if (entity is FireBall || name.Contains("fireball") || name.Contains("napalm"))
            {
                var fireball = entity?.GetComponent<FireBall>() ?? null;
                if (fireball != null && HeliFireBalls.Contains(fireball)) HeliFireBalls.Remove(fireball);
            }
            //if (entity.ToString().Contains("maincannonshell"))
            //{

            //}
        }

        //private void FireRocket(PatrolHelicopterAI heliAI)
        //{
        //    Puts("Heli Fire rocket being called");
        //}

        void OnEntityTakeDamage(BaseCombatEntity Target, HitInfo HitInfo)
        {
            if (HitInfo.damageTypes.Has(Rust.DamageType.Decay)) return;
            if (initialized == false)
            {
                ModifyDamage(HitInfo, 0f);
                return;
            }
            if (Target == null || HitInfo == null || Target.GetComponent<ResourceDispenser>() != null) return;

            BaseEntity Attacker = HitInfo?.Initiator;
            Type attackerType = Attacker?.GetType();
            Type targetType = Target?.GetType();
            String Weapon = HitInfo?.WeaponPrefab?.ShortPrefabName;

            //  Handle Attacks with Null damage
            if ((Attacker == null || attackerType == null || targetType == null) && Weapon == null)
            {
                Puts("return");
                return;
            }
            else if ((Attacker == null || attackerType == null || targetType == null) && Weapon == "maincannonshell") BradleyAPCAttacker(Target, HitInfo);
            else if ((Attacker == null || attackerType == null || targetType == null) && Weapon == "rocket_heli_napalm") HelicopterAttacker(Target, HitInfo);
            else if (Attacker == null || attackerType == null || targetType == null)
            {
                if (Weapon != null)
                {
                    Puts("Weapon: " + Weapon);
                }
                if (Attacker == null)
                {
                    HitInfo hitinfocopy = HitInfo;
                    ModifyDamage(HitInfo, 0f);
                    DamageDebuger(hitinfocopy);
                    return;
                }
                if (attackerType == null)
                {
                    HitInfo hitinfocopy = HitInfo;
                    ModifyDamage(HitInfo, 0f);
                    DamageDebuger(hitinfocopy);
                    return;
                }
                if (targetType == null)
                {
                    ModifyDamage(HitInfo, 0f);
                    return;
                }
                return;
            }


            if (targetType == typeof(HelicopterDebris) || targetType == typeof(DroppedItemContainer)) return;
            else if (Attacker is BasePlayer) PlayerAttacker(Target, (BasePlayer)Attacker, HitInfo);
            else if (Attacker is FireBall) FireBallAttacker(Target, HitInfo);
            else if (Attacker is BaseHelicopter) HelicopterAttacker(Target, HitInfo);
            else if (Attacker is BradleyAPC) BradleyAPCAttacker(Target, HitInfo);
            else if (AnimalList.Contains(attackerType)) AnimalAttacker(Target, HitInfo);
            else if (Attacker is MotorRowboat) return; //removes debugging spam
        }

        void PlayerAttacker(BaseEntity Target, BasePlayer Attacker, HitInfo HitInfo)
        {
            if (Target is Scientist)
            {
                if (Attacker is Scientist) return;
                return;

                //BasePlayer BaseTarget = (BasePlayer)Target;
                //BasePlayer BaseAttacker = (BasePlayer)Attacker;
                //if (BaseTarget.userID == BaseAttacker.userID) return;

                //if (PvXPlayers[BaseTarget.userID].State == PvXState.PvP && PvXPlayers[BaseAttacker.userID].State == PvXState.PvP)
                //{
                //    return;
                //}
                //else if (PvXPlayers[BaseTarget.userID].State == PvXState.PvE)
                //{
                //    ModifyDamage(HitInfo, 0f);
                //    IPlayer IplayerAttacker = FindIPlayer(BaseAttacker.UserIDString);
                //    IplayerAttacker.Reply(GetMSG("EnemyPvE", IplayerAttacker.Id));
                //    return;
                //}
                //else
                //{
                //    ModifyDamage(HitInfo, 0f);
                //    IPlayer IplayerAttacker = FindIPlayer(BaseAttacker.UserIDString);
                //    IplayerAttacker.Reply(GetMSG("PlayerPvE", IplayerAttacker.Id));
                //    return;
                //}
            }
            if (Target is BasePlayer)
            {
                if (Attacker is Scientist) return;
                BasePlayer BaseTarget = (BasePlayer)Target;
                BasePlayer BaseAttacker = (BasePlayer)Attacker;
                if (BaseTarget.userID == BaseAttacker.userID) return;

                if (PvXPlayers[BaseTarget.userID].State == PvXState.PvP && PvXPlayers[BaseAttacker.userID].State == PvXState.PvP)
                {
                    return;
                }
                else if (PvXPlayers[BaseTarget.userID].State == PvXState.PvE)
                {
                    ModifyDamage(HitInfo, 0f);
                    IPlayer IplayerAttacker = FindIPlayer(BaseAttacker.UserIDString);
                    IplayerAttacker.Reply(GetMSG("EnemyPvE", IplayerAttacker.Id));
                    return;
                }
                else
                {
                    ModifyDamage(HitInfo, 0f);
                    IPlayer IplayerAttacker = FindIPlayer(BaseAttacker.UserIDString);
                    IplayerAttacker.Reply(GetMSG("PlayerPvE", IplayerAttacker.Id));
                    return;
                }
            }
            else if (BuildEntityList.Contains(Target.GetType()))
            {
                BasePlayer BaseTarget = FindBasePlayer(Target.OwnerID.ToString());
                BasePlayer BaseAttacker = (BasePlayer)Attacker;
                if (Target.OwnerID == 0) return;
                if (BaseTarget.userID == BaseAttacker.userID) return;

                if (PvXPlayers[BaseTarget.userID].State == PvXState.PvP && PvXPlayers[BaseAttacker.userID].State == PvXState.PvP)
                {
                    return;
                }
                else if (PvXPlayers[BaseTarget.userID].State == PvXState.PvE)
                {
                    ModifyDamage(HitInfo, 0f);
                    IPlayer IplayerAttacker = FindIPlayer(BaseAttacker.UserIDString);
                    IplayerAttacker.Reply(GetMSG("EnemyPvE", IplayerAttacker.Id));
                    return;
                }
                else
                {
                    ModifyDamage(HitInfo, 0f);
                    IPlayer IplayerAttacker = FindIPlayer(BaseAttacker.UserIDString);
                    IplayerAttacker.Reply(GetMSG("PlayerPvE", IplayerAttacker.Id));
                    return;
                }
            }
            else if (AnimalList.Contains(Target.GetType()))
            {
                return;
            }
            else if (Target is BaseHelicopter)
            {
                BasePlayer BaseAttacker = (BasePlayer)Attacker;

                if (PvXPlayers[BaseAttacker.userID].State == PvXState.PvP)
                {
                    return;
                }
                else if (PvXPlayers[BaseAttacker.userID].State == PvXState.PvE)
                {
                    ModifyDamage(HitInfo, 0f);
                    IPlayer IplayerAttacker = FindIPlayer(BaseAttacker.UserIDString);
                    IplayerAttacker.Reply(GetMSG("EnemyPvE", IplayerAttacker.Id));
                    return;
                }
                else
                {
                    ModifyDamage(HitInfo, 0f);
                    IPlayer IplayerAttacker = FindIPlayer(BaseAttacker.UserIDString);
                    IplayerAttacker.Reply(GetMSG("PlayerPvE", IplayerAttacker.Id));
                    return;
                }
            }
            else if (Target is BradleyAPC)
            {
                BasePlayer BaseAttacker = (BasePlayer)Attacker;

                if (PvXPlayers[BaseAttacker.userID].State == PvXState.PvP)
                {
                    return;
                }
                else if (PvXPlayers[BaseAttacker.userID].State == PvXState.PvE)
                {
                    ModifyDamage(HitInfo, 0f);
                    IPlayer IplayerAttacker = FindIPlayer(BaseAttacker.UserIDString);
                    IplayerAttacker.Reply(GetMSG("EnemyPvE", IplayerAttacker.Id));
                    return;
                }
                else
                {
                    ModifyDamage(HitInfo, 0f);
                    IPlayer IplayerAttacker = FindIPlayer(BaseAttacker.UserIDString);
                    IplayerAttacker.Reply(GetMSG("PlayerPvE", IplayerAttacker.Id));
                    return;
                }
            }
        }
        void FireBallAttacker(BaseEntity Target, HitInfo hitInfo)
        {
            FireBall fireBall = (FireBall)hitInfo.Initiator;
            var prefabname = fireBall?.ShortPrefabName ?? string.Empty;

            if ((prefabname.Contains("napalm") || prefabname.Contains("oilfireball")) && !prefabname.Contains("rocket")) HelicopterAttacker(Target, hitInfo);
        }
        void HelicopterAttacker(BaseEntity Target, HitInfo hitInfo)
        {
            if (Target is BasePlayer)
            {
                BasePlayer BaseTarget = (BasePlayer)Target;

                if (PvXPlayers[BaseTarget.userID].State == PvXState.PvP)
                {
                    return;
                }
                else if (PvXPlayers[BaseTarget.userID].State == PvXState.PvE)
                {
                    ModifyDamage(hitInfo, 0f);
                    return;
                }
                else
                {
                    ModifyDamage(hitInfo, 0f);
                    return;
                }
            }
            else if (BuildEntityList.Contains(Target?.GetType()))
            {
                BasePlayer BaseTarget = FindBasePlayer(Target.OwnerID.ToString());

                if (PvXPlayers[BaseTarget.userID].State == PvXState.PvP)
                {
                    return;
                }
                else if (PvXPlayers[BaseTarget.userID].State == PvXState.PvE)
                {
                    ModifyDamage(hitInfo, 0f);
                    return;
                }
                else
                {
                    ModifyDamage(hitInfo, 0f);
                    return;
                }
            }
        }
        void BradleyAPCAttacker(BaseEntity Target, HitInfo hitInfo)
        {
            if (Target is BasePlayer)
            {
                BasePlayer BaseTarget = (BasePlayer)Target;

                if (PvXPlayers[BaseTarget.userID].State == PvXState.PvP)
                {
                    return;
                }
                else if (PvXPlayers[BaseTarget.userID].State == PvXState.PvE)
                {
                    ModifyDamage(hitInfo, 0f);
                    return;
                }
                else
                {
                    ModifyDamage(hitInfo, 0f);
                    return;
                }
            }
            else if (BuildEntityList.Contains(Target.GetType()))
            {
                BasePlayer BaseTarget = FindBasePlayer(Target.OwnerID.ToString());

                if (PvXPlayers[BaseTarget.userID].State == PvXState.PvP)
                {
                    return;
                }
                else if (PvXPlayers[BaseTarget.userID].State == PvXState.PvE)
                {
                    ModifyDamage(hitInfo, 0f);
                    return;
                }
                else
                {
                    ModifyDamage(hitInfo, 0f);
                    return;
                }
            }
        }
        void AnimalAttacker(BaseEntity Target, HitInfo hitInfo)
        {
            if (Target is BasePlayer)
            {
                return;
            }
        }

        void DamageDebuger(HitInfo hitInfo) // Need to handle Nills better
        {
            //Puts("HitInfo Data:");
            //if (hitInfo.Initiator != null)
            //{
            //    Puts("Initiatior: " + hitInfo.Initiator?.ToString());
            //    var prefabname1 = hitInfo?.Initiator.PrefabName ?? null;
            //    if (prefabname1 != null) Puts(prefabname1.ToString());
            //}
            //if (hitInfo.GetType() != null)
            //{
            //    Puts("HitInfo Type: " + hitInfo.GetType().ToString());
            //}
            //Puts("ProjectilePrefab:");
            //if (hitInfo.ProjectilePrefab != null)
            //{
            //    Puts("ProjectilePrefab: " + hitInfo.ProjectilePrefab.GetType().ToString() );
            //}
            
            // Puts(hitInfo.HitEntity.name.ToString());
        }
        
        void ModifyDamage(HitInfo HitInfo, float scale)
        {
            if (scale == 0f)
            {
                HitInfo.damageTypes = new DamageTypeList();
                HitInfo.DoHitEffects = false;
                HitInfo.HitMaterial = 0;
                HitInfo.PointStart = Vector3.zero;
                HitInfo.PointEnd = Vector3.zero;
            }
            else if (scale == 1) return;
            else
            {
                //Puts("Modify Damabe by: {0}", scale);
                HitInfo.damageTypes.ScaleAll(scale);
            }
        }
        
        //void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod, ProtoBuf.ProjectileShoot projectiles)
        //{
        //    Puts("OnWeaponFired works!");
        //}

        //void OnRocketLaunched()
        //{
        //    Puts("OnRocketLaunched being Called");
        //}

        //void OnRocketLaunched(BaseHelicopter baseHelicopter, BaseEntity baseEntity)
        //{
        //    Puts("OnRocketLaunched being Called by heli");
        //}

        //void OnCreateWorldProjectile()
        //{
        //    Puts("OnCreateWorldProjectile being Called");
        //    //Puts(item.GetType().ToString());
        //}
        #endregion

        #region Plugin Compatability

        [PluginReference]
        public Plugin HumanNPC;
        internal static bool IsNPC(ulong Player)
        {
            if (PvX.HumanNPC == null) return false;
            else if (Player < 76560000000000000L) return true;
            else return false;
        }
        internal static bool IsNPC(BaseCombatEntity Player)
        {
            BasePlayer _test = (BasePlayer)Player;
            if (PvX.HumanNPC == null) return false;
            else if (_test.userID < 76560000000000000L) return true;
            else return false;
        }
        internal static bool IsNPC(PlayerCorpse Corpse)
        {
            if (PvX.HumanNPC == null) return false;
            else if (Corpse.playerSteamID < 76560000000000000L) return true;
            else return false;
        }

        #endregion

        #region Messaging

        private void PlayerNotification(IPlayer iplayer)
        {
            BasePlayer player = FindBasePlayer(iplayer.Id);
            if (iplayer.IsConnected)
            {
                PvXNotification notification = PvXPlayers[player.userID].Notification;
                switch (notification)
                {
                    case PvXNotification.NoMode:
                        iplayer.Reply(GetMSG("PlayerNotificationNA", iplayer.Id));
                        PvX.timer.Once(10, () => PlayerNotification(iplayer));
                        break;

                    case PvXNotification.Accepted:

                        //remove notification
                        iplayer.Reply(GetMSG("PlayerNotificationAccepted", iplayer.Id));
                        PvXPlayers[player.userID].Notification = PvXNotification.NA;
                        break;

                    case PvXNotification.Delcined:

                        //remove notification
                        iplayer.Reply(GetMSG("PlayerNotificationDeclined", iplayer.Id));
                        PvXPlayers[player.userID].Notification = PvXNotification.NA;
                        break;

                    case PvXNotification.Timed:

                        //remove notification
                        iplayer.Reply(GetMSG("PlayerNotificationTimed", iplayer.Id));
                        PvXPlayers[player.userID].Notification = PvXNotification.NA;
                        break;

                    default:

                        break;
                }
            }
        }

        private string GetMSG(string key, string userid = null, params object[] args) => string.Format(lang.GetMessage(key, this, userid), args);
        private readonly Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            {"AdminViewTicket1Var","Ticket {0} Details"},
            {"AdminViewTicket2Var","Player Name: {0}"},
            {"AdminViewTicket3Var","Time Stamp: {0}"},
            {"AdminViewTicket4Var","Reason: {0}"},
            {"AdminViewTicket5","Please use /pvx [accept/decline] to confirm."},
            {"AdminNoTickets", "No tickets to display."},
            {"AdminTicketsDontMatch","This ticket is no longer available, please view another one."},
            {"AdminTicketAcceptedVar","You have accepted the ticket for: {0}"},
            {"AdminTicketDeclinedVar","You have declined the ticket for: {0}"},
            {"AdminNoViewTicket","No ticket has been selected, please view a ticket."},
            {"Name", "Message"},
            {"EnemyPvE", "Target is PvE"},
            {"PlayerPvE", "You are PvE"},
            {"PlayerNotificationNA", "This server is running PvX, please select a mode: ''/pvx''"},
            {"PlayerModeChange", "You have changed to {0}"},
            {"PlayerAlreadyHasTicket","You already have a ticket, please contact the admin"},
            {"PlayerNotificationAccepted", "Welcome back, A admin has accepted your ticket"},
            {"PlayerNotificationDeclined", "Welcome back, A admin has declined your ticket"},
            {"PlayerNotificationTimed", "Welcome back, your ticket has auto accepted"},
            {"PlayerTicketAcceptedVar", "Your ticket has closed and you are now a {0} player"},
            {"PlayerTicketDeclined", "Your ticket has been declined by an admin."},
            {"PlayerNoTicket", "You do not have a ticket."},
            {"PlayerTimeRemainingVar", "Unable to change, Minutes remaining: {0}."},
            {"PlayerTicketCreated", "Creating Ticket."},
            {"PlayerHasTicketVar", "Your ticket number is: {0}"},
            {"NoValidTicket", "Warning, No valid ticket was located please check data file"},
            {"WelcomeMessage", "Welcome, this server runs PvX, Please type /Command for more information"},
            {"why is this here", "Because I am lazy"},
            {"ChatInstructionChange", "Please use command: /pvx change"},
            {"ChatInstructionPvX", "Please use command: /pvx [PvE/PvP]"},
            {"PvX Chat Instructions 1", "PvX Commands"},
            {"PvX Chat Instructions 2", "Select Mode /pvx [pve/pvx]"},
            {"PvX Chat Instructions 3", "Change Mode /pvx ''change''"},
            {"MissingPermisionVar", "You do not have the required permision: {0}"},
            {"TicketList1", "Ticket Number: {0}"},
            {"TicketList2", "Requester: {0}, Current mode: {1}"},
            {"PlayerReasonIncorrect","Incorrect format please use /pvx reason ''Reason for change''"},
            {"xx","xx"},
            {"x", "Warning, invalid LANG code called"}
        };
        
        #endregion

        #region QUI
        class UIColours
        {
            public static readonly UIColours Black_100 = new UIColours("0.00 0.00 0.00 1.00"); //Black
            public static readonly UIColours Black_050 = new UIColours("0.00 0.00 0.00 0.50");
            public static readonly UIColours Black_015 = new UIColours("0.00 0.00 0.00 0.15");
            public static readonly UIColours Grey2_100 = new UIColours("0.20 0.20 0.20 1.00"); //Grey 2
            public static readonly UIColours Grey2_050 = new UIColours("0.20 0.20 0.20 0.50");
            public static readonly UIColours Grey2_015 = new UIColours("0.20 0.20 0.20 0.15");
            public static readonly UIColours Grey5_100 = new UIColours("0.50 0.50 0.50 1.00"); //Grey 5
            public static readonly UIColours Grey5_050 = new UIColours("0.50 0.50 0.50 0.50");
            public static readonly UIColours Grey5_015 = new UIColours("0.50 0.50 0.50 0.15");
            public static readonly UIColours Grey8_100 = new UIColours("0.80 0.80 0.80 1.00"); //Grey 8
            public static readonly UIColours Grey8_050 = new UIColours("0.80 0.80 0.80 0.50");
            public static readonly UIColours Grey8_015 = new UIColours("0.80 0.80 0.80 0.15");
            public static readonly UIColours White_100 = new UIColours("1.00 1.00 1.00 1.00"); //White
            public static readonly UIColours White_050 = new UIColours("1.00 1.00 1.00 0.50");
            public static readonly UIColours White_015 = new UIColours("1.00 1.00 1.00 0.15");
            public static readonly UIColours Red_100 = new UIColours("0.70 0.20 0.20 1.00");   //Red
            public static readonly UIColours Red_050 = new UIColours("0.70 0.20 0.20 0.50");
            public static readonly UIColours Red_015 = new UIColours("0.70 0.20 0.20 0.15");
            public static readonly UIColours Green_100 = new UIColours("0.20 0.70 0.20 1.00");  //Green
            public static readonly UIColours Green_050 = new UIColours("0.20 0.70 0.20 0.50");
            public static readonly UIColours Green_015 = new UIColours("0.20 0.70 0.20 0.15");
            public static readonly UIColours Blue_100 = new UIColours("0.20 0.20 0.70 1.00");  //Blue
            public static readonly UIColours Blue_050 = new UIColours("0.20 0.20 0.70 0.50");
            public static readonly UIColours Blue_015 = new UIColours("0.20 0.20 0.70 0.15");
            public static readonly UIColours Yellow_100 = new UIColours("0.90 0.90 0.20 1.00");  //Yellow
            public static readonly UIColours Yellow_050 = new UIColours("0.90 0.90 0.20 0.50");
            public static readonly UIColours Yellow_015 = new UIColours("0.90 0.90 0.20 0.15");
            public static readonly UIColours Gold_100 = new UIColours("0.745 0.550 0.045 1.00"); //Gold

            public string Value;
            public int Index;

            private UIColours(string value)
            {
                Value = value;
            }
            public static implicit operator string(UIColours uiColours)
            {
                return uiColours.Value;
            }
        }
        public CuiElementContainer CreateElementContainer(string panelName, string color, string aMin, string aMax, bool useCursor = false, string parent = "Hud")
        {
            var NewElement = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
                        panelName
                    }
                };
                return NewElement;
            }
        public void CreatePanel(ref CuiElementContainer container, string panel, string color, string aMin, string aMax, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    CursorEnabled = cursor
                },
                panel);
            }
        public void CreateLabel(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, float fadein = 1.0f, TextAnchor align = TextAnchor.MiddleCenter)
            {
                if (configData.Gui.DisableUI_FadeIn)
                    fadein = 0;
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, FadeIn = fadein, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);

            }
        public void CreateButton(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, float fadein = 1.0f, TextAnchor align = TextAnchor.MiddleCenter)
            {
                if (configData.Gui.DisableUI_FadeIn)
                    fadein = 0;
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = fadein },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel);
            }
        public void LoadImage(ref CuiElementContainer container, string panel, string png, string aMin, string aMax)
            {
                container.Add(new CuiElement
                {
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent {Png = png },
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax }
                    }
                });
            }
        public void CreateTextOverlay(ref CuiElementContainer container, string panel, string text, string color, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter, float fadein = 1.0f)
        {
            if (configData.Gui.DisableUI_FadeIn)
                fadein = 0;
            container.Add(new CuiLabel
            {
                Text = { Color = color, FontSize = size, Align = align, FadeIn = fadein, Text = text },
                RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
            },
            panel);

        }
        
        #endregion

        #region GUI
        public void AdminIndicatorGui(BasePlayer Player)
        {
            Vector2 dimension = new Vector2(0.174F, 0.028F);
            Vector2 posMin = new Vector2(configData.Gui.adminIndicatorMinWid, configData.Gui.adminIndicatorMinHei);
            Vector2 posMax = posMin + dimension;
            var adminCountContainer = CreateElementContainer(
                PvxAdminUI,
                UIColours.Black_050,
                $"{posMin.x} {posMin.y}",
                $"{posMax.x} {posMax.y}");
            CreateLabel(ref adminCountContainer, PvxAdminUI, UIColours.White_100, "PvX Tickets", 10, "0.0 0.1", "0.3 0.90");
            CreateLabel(ref adminCountContainer, PvxAdminUI, UIColours.White_100, string.Format("Open: {0}", PvXTickets.Count.ToString()), 10, "0.301 0.1", "0.65 0.90");
            CreateLabel(ref adminCountContainer, PvxAdminUI, UIColours.White_100, string.Format("Closed: {0}", PvXLogs.Count.ToString()), 10, "0.651 0.1", "1 0.90");

            CuiHelper.AddUi(Player, adminCountContainer);
        }
        public void PlayerIndicatorGui(BasePlayer Player)
        {
            Vector2 dimension = new Vector2(0.031F, 0.028F);
            Vector2 posMin = new Vector2(configData.Gui.playerIndicatorMinWid, configData.Gui.playerIndicatorMinHei);
            Vector2 posMax = posMin + dimension;
            var indicatorContainer = CreateElementContainer(
                PvxIndicatorUI,
                UIColours.Black_050,
                "0.48 0.11",
                "0.52 0.14"
                );
            if (PvXPlayers[Player.userID].State == PvXState.NA)
                indicatorContainer = CreateElementContainer(
                    PvxIndicatorUI,
                    UIColours.Red_100,
                    "0.48 0.11",
                    "0.52 0.14");
            else if (PvXPlayers[Player.userID].Ticket == true)
                indicatorContainer = CreateElementContainer(
                    PvxIndicatorUI,
                    UIColours.Yellow_015,
                    "0.48 0.11",
                    "0.52 0.14");
            if (AdminsActive.Contains(Player.userID))
            {
                CreateLabel(
                    ref indicatorContainer,
                    PvxIndicatorUI,
                    UIColours.Green_100,
                    GetPvXStateString(PvXPlayers[Player.userID].State),
                    15,
                    "0.1 0.1",
                    "0.90 0.99");
            }
            else
            {
                CreateLabel(ref indicatorContainer,
                    PvxIndicatorUI,
                    UIColours.White_100,
                    GetPvXStateString(PvXPlayers[Player.userID].State),
                    15,
                    "0.1 0.1",
                    "0.90 0.99");
            }
            CuiHelper.AddUi(Player, indicatorContainer);
        }
        private void UpdatePlayersGui(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PvxIndicatorUI);
            PlayerIndicatorGui(player);
        }
        private void UpdateAdminsGui()
        {
            BasePlayer Player;
            foreach (ulong PlayerID in AdminsOnline)
            {
                Player = FindBasePlayer(PlayerID.ToString());
                CuiHelper.DestroyUi(Player, PvxAdminUI);
                AdminIndicatorGui(Player);
            }
        }
        private void UpdateAdminGui(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PvxAdminUI);
            AdminIndicatorGui(player);
        }
        private void DestroyAllPvXUI(BasePlayer player)
        {
            foreach (string _v in GuiList)
            {
                CuiHelper.DestroyUi(player, _v);
            }
            //DestroyEntries(player);
        }
        private void DestroyUIElement(BasePlayer player, string _ui)
        {
            CuiHelper.DestroyUi(player, _ui);
        }
        #endregion

    }
}