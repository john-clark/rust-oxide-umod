// Requires: EventManager
using System;
using System.Collections.Generic;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Slasher", "k1lly0u", "0.2.1", ResourceId = 1662)]
    class Slasher : RustPlugin
    {
        #region Fields
        [PluginReference] EventManager EventManager;
        [PluginReference] Plugin Spawns;

        static Slasher instance;
        static string SlasherClock = "SlasherClockUI";
        static string SlasherPopup = "SlasherPopupUI";

        private bool usingSlasher;
        private bool hasStarted;
        private bool isOpen;
        private bool justLaunched;
        private bool gameEnding;

        private int roundNumber;

        private string playerSpawns;

        private RoundTimer roundTimer;
        private Timer cancelTimer;
        private EventManager.Events defaultEvent;

        private List<SlasherPlayer> SlasherPlayers;
        private BasePlayer nextSlasher;      
        #endregion

        #region Oxide Hooks
        void Loaded()
        {
            usingSlasher = false;
            hasStarted = false;
            isOpen = false;
            justLaunched = false;
            roundNumber = 0;
            SlasherPlayers = new List<SlasherPlayer>();
            nextSlasher = null;
        }
        void OnServerInitialized()
        {
            LoadVariables();
            //RegisterGame();
            instance = this;            
        }
        void Unload()
        {
            UnityEngine.Object.Destroy(roundTimer);
            var objPlayers = UnityEngine.Object.FindObjectsOfType<SlasherPlayer>();
            if (objPlayers != null)
                foreach (var gameObj in objPlayers)
                    UnityEngine.Object.Destroy(gameObj);
            if (cancelTimer != null)
                cancelTimer.Destroy();
            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, SlasherClock);
        }
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            try
            {
                var victim = entity.ToPlayer();
                if (victim != null)
                if (usingSlasher && hasStarted)
                {

                }
            }
            catch { }
        }
        #endregion

        #region EventManager Hooks
        private void UpdateScores() 
        {
            if (usingSlasher && hasStarted)
            {
                var sortedList = SlasherPlayers.OrderByDescending(pair => pair.kills).ToList();
                var scoreList = new Dictionary<ulong, EventManager.Scoreboard>();
                foreach (var entry in sortedList)
                    if (!scoreList.ContainsKey(entry.player.userID))
                        scoreList.Add(entry.player.userID, new EventManager.Scoreboard { Name = entry.player.displayName, Position = sortedList.IndexOf(entry), Score = entry.kills });
                EventManager.UpdateScoreboard(new EventManager.ScoreData { Additional = null, Scores = scoreList, ScoreType = "Kills" });
            }
        }
        void RegisterGame()
        {
            EventManager.Events eventData = new EventManager.Events
            {
                CloseOnStart = false,
                DisableItemPickup = true,
                EnemiesToSpawn = 0,
                EventType = Title,
                GameMode = EventManager.GameMode.Normal,
                GameRounds = 0,
                Kit = null,
                MaximumPlayers = 0,
                MinimumPlayers = 2,
                ScoreLimit = 0,
                Spawnfile = configData.GameSettings.DefaultSpawnfile,
                Spawnfile2 = null,
                SpawnType = EventManager.SpawnType.Consecutive,
                RespawnTimer = 10,
                RespawnType = EventManager.RespawnType.Timer,
                UseClassSelector = false,
                WeaponSet = null,
                ZoneID = configData.EventSettings.DefaultZoneID                
            };
            defaultEvent = eventData;
            EventManager.EventSetting eventSettings = new EventManager.EventSetting
            {
                CanChooseRespawn = false,
                CanUseClassSelector = false,
                CanPlayBattlefield = false,
                ForceCloseOnStart = false,
                IsRoundBased = false,
                LockClothing = true,
                RequiresKit = false,
                RequiresMultipleSpawns = false,
                RequiresSpawns = true,
                ScoreType = null,
                SpawnsEnemies = false
            };
            var success = EventManager.RegisterEventGame(Title, eventSettings, eventData);
            if (success == null)
            {
                Puts("Event plugin doesn't exist");
                return;
            }
            if (configData.GameSettings.AutoStart_Use)
                if (ValidateSettings())
                    CheckTime();
        }
        void OnSelectEventGamePost(string name)
        {
            if (Title == name)
                usingSlasher = true;
            else usingSlasher = false;
        }
        void OnEventPlayerSpawn(BasePlayer player)
        {
            if (usingSlasher && hasStarted && !gameEnding)
            {                
                player.health = configData.GameSettings.StartHealth;                
            }
        }
        object CanEventOpen()
        {
            if (usingSlasher)
            {
                if (!(TOD_Sky.Instance.Cycle.Hour > configData.GameSettings.Auto_StartTime || TOD_Sky.Instance.Cycle.Hour < configData.GameSettings.Auto_EndTime))
                    return "Slasher can only be played at night time";                
            }
            return null;
        }
        void OnEventOpenPost()
        {
            if (usingSlasher)
                EventManager.BroadcastToChat("In Slasher, the goal is to hide from the slasher, if you hide long enough you will be given weapons for your chance to take down the slasher");
            SlasherPlayers.Clear();
            nextSlasher = null;
        }
        void OnEventCancel()
        {
            if (usingSlasher && hasStarted)
                CheckScores(true);
        }        
        void OnEventEndPre()
        {
            if (usingSlasher && hasStarted)
            {
                CheckScores(true);
                DestroyTimers();
                
                foreach (var player in BasePlayer.activePlayerList)
                    CuiHelper.DestroyUi(player, SlasherClock);
            }
        }
        void OnEventEndPost()
        {
            if (usingSlasher)
            {
                hasStarted = false;
                isOpen = false;
                var slasherPlayers = UnityEngine.Object.FindObjectsOfType<SlasherPlayer>();
                if (slasherPlayers != null)
                {
                    foreach (var slasher in slasherPlayers)
                    {
                        UnityEngine.Object.Destroy(slasher);
                    }
                }
                SlasherPlayers.Clear();
            }
        }
        void OnEventStartPre()
        {
            if (usingSlasher)
            {
                justLaunched = true;
                roundNumber = 0;                
            }
        }
        object OnEventStartPost()
        {
            if (usingSlasher)
            {
                hasStarted = true;
                UpdateScores();
                NextRound();
            }          
            return null;
        }
        void OnEventJoinPost(BasePlayer player)
        {
            if (usingSlasher)
            {
                if (player.GetComponent<SlasherPlayer>())
                    UnityEngine.Object.Destroy(player.GetComponent<SlasherPlayer>());
                SlasherPlayers.Add(player.gameObject.AddComponent<SlasherPlayer>());
                player.GetComponent<SlasherPlayer>().team = Team.DEAD;
                EventManager.CreateScoreboard(player);
                player.inventory.Strip();   
                if (hasStarted)
                {
                    EventManager.AddRespawnTimer(player, "", false, false);
                }            
            }
        }        
        void OnEventLeavePost(BasePlayer player)
        {
            if (usingSlasher && hasStarted)
            {
                var slasher = player.GetComponent<SlasherPlayer>();
                if (slasher != null)
                {
                    if (slasher.team == Team.SLASHER)
                        EndRound();
                    if (nextSlasher == player) nextSlasher = null;     
                    SlasherPlayers.Remove(slasher);
                    UnityEngine.Object.Destroy(player.GetComponent<SlasherPlayer>());
                    CuiHelper.DestroyUi(player, SlasherClock);
                    CheckScores();                   
                }
            }            
        }
        void OnEventPlayerDeath(BasePlayer victim, HitInfo hitinfo) 
        {
            if (usingSlasher)
            {
                var eventVic = victim.GetComponent<SlasherPlayer>();
                AddKill(victim, hitinfo?.InitiatorPlayer ?? null);                
            }
            return;
        }
        
        object EventChooseSpawn(BasePlayer player, Vector3 destination)
        {
            if (usingSlasher && hasStarted)
            {
                var slasher = player.GetComponent<SlasherPlayer>();
                if (slasher != null)
                {
                    var newPos = EventManager.SpawnCount.GetSpawnPoint(playerSpawns);
                    if (newPos is Vector3)
                        return (Vector3)newPos;
                    else PrintError($"Unable to find a spawnpoint for {player.displayName} on team {slasher.team}");
                }                
            }
            return null;
        }
        void SetSpawnfile(bool isPlayerSpawns, string spawnfile)
        {
            if (isPlayerSpawns)
                playerSpawns = spawnfile;
        }
        object GetRespawnType()
        {
            if (usingSlasher)
                return EventManager.RespawnType.Timer;
            return null;
        }
        object GetRespawnMsg()
        {
            if (usingSlasher)
                return "You must wait until the round is over";
            return null;
        }
        object FreezeRespawn(BasePlayer player)
        {
            if (usingSlasher)
            {
                var eventPlayer = player.GetComponent<SlasherPlayer>();
                if (eventPlayer?.team == Team.DEAD)                
                    return true;                
            }
            return null;
        }
        object HasDamageMultiplier(BasePlayer victim, BasePlayer attacker)
        {
            if (usingSlasher)
            {
                var att = attacker.GetComponent<SlasherPlayer>();
                var vic = victim.GetComponent<SlasherPlayer>();

                if (att == null || vic == null) return null;
                var weapon = attacker.GetActiveItem();
                if (weapon == null) return null;
                switch (att.team)
                {
                    case Team.DEAD:
                        return null;
                    case Team.SLASHER:
                        if (configData.Slashers.Weapon.Shortname == weapon?.info?.shortname)
                            return configData.Slashers.DamageModifier;
                        return null;
                    case Team.HUNTED:
                        if (configData.Players.Weapon.Shortname == weapon?.info?.shortname)                        
                            return configData.Players.DamageModifier;                        
                        return null;
                }
            }
            return null;
        }
        #endregion

        #region Functions
        bool ValidateSettings()
        {
            List<string> errorList = new List<string>();
            var success = EventManager.ValidateSpawnFile(configData.GameSettings.DefaultSpawnfile);
            if (success is string)            
                errorList.Add("Spawn file is invalid");
            success = EventManager.ValidateZoneID(configData.EventSettings.DefaultZoneID);
            if (success is string)
                errorList.Add("Zone ID is invalid");
            if (errorList.Count > 0)
            {
                PrintError("The was a error starting the night time automated Slasher event. You must have valid entries in your config.");
                foreach (var error in errorList)
                    PrintError(error);
                return false;
            }
            return true;
        }
       
        void CheckTime()
        {
            if (!hasStarted)
            {
                var time = TOD_Sky.Instance.Cycle.Hour;
                if (isOpen)
                {
                    if (time >= configData.GameSettings.Auto_StartTime || (time > 0 && time < configData.GameSettings.Auto_EndTime))
                    {
                        if (EventManager.Joiners.Count > 2)
                        {
                            if (cancelTimer != null)
                                cancelTimer.Destroy();
                            StartEvent();
                            return;
                        }
                    }
                }
                else
                {
                    if ((time > configData.GameSettings.Auto_OpenTime && time < 24) || (time > 0 && time < configData.GameSettings.Auto_EndTime))
                    {
                        if (!EventManager._Open && !EventManager._Started)
                        {
                            if (justLaunched) return;

                            EventManager._Event = defaultEvent;
                            SetSpawnfile(true, defaultEvent.Spawnfile);
                            SetSpawnfile(false, defaultEvent.Spawnfile2);
                            EventManager.OpenEvent();

                            isOpen = true;
                            cancelTimer = timer.Once(configData.GameSettings.Auto_CancelTimer * 60, () =>
                            {
                                EventManager.CancelEvent("Not enough players to start the event");
                                isOpen = false;
                                justLaunched = true;
                            });
                        }
                    }
                    else
                    {
                        justLaunched = false;
                        if (isOpen && EventManager._Open && EventManager._Event.EventType == Title)
                        {
                            EventManager.CancelEvent("Daytime is upon us");
                        }
                    }
                }                
            }
            timer.Once(30, () => CheckTime());
        }
        void StartEvent()
        {
            int time = 30;
            timer.Repeat(1, 31, () =>
            {
                if (time == 30) EventManager.BroadcastToChat("Slasher will start in 30 seconds");
                if (time == 10) EventManager.BroadcastToChat("Slasher will start in 10 seconds");
                if (time == 0) EventManager.StartEvent();
                time--;
            });
            return;
        }
        void NextRound()
        {
            EventManager.RespawnAllPlayers();
            roundNumber++;
            if (roundNumber >= configData.GameSettings.RoundsToPlay)
            {
                CheckScores(true);
                return;
            }
            if (configData.GameSettings.AdjustTimeToSuitRounds)
                TOD_Sky.Instance.Cycle.Hour = configData.GameSettings.Auto_StartTime + 1;
            SetSlasherPlayers();
            foreach (var player in SlasherPlayers)
                EventManager.ResetPlayer(player.player);
            StartRoundTimers(true);
        }
        void SetSlasherPlayers()
        {
            if (nextSlasher == null)
                nextSlasher = GetRandomSlasher();
            foreach (var player in SlasherPlayers)
            {
                if (player.player.userID == nextSlasher.userID)
                    player.team = Team.SLASHER;
                else player.team = Team.HUNTED;
            }
        }
        
        BasePlayer GetRandomSlasher()
        {
            var next = SlasherPlayers.GetRandom();
            if (next == null || next.player == null)
                return GetRandomSlasher();
            return next.player;
        }
        void StartRoundTimers(bool initialRound)
        {
            if (gameEnding) return;
            if (initialRound)
            {
                roundTimer = new GameObject().AddComponent<RoundTimer>();
                roundTimer.StartTimer(configData.GameSettings.GameTimer_Slasher, configData.GameSettings.GameTimer_Player);
                foreach (var player in SlasherPlayers)
                {                    
                    player.player.inventory.Strip();
                    string msg;
                    if (player.team == Team.SLASHER)
                    {
                        foreach (var item in configData.Slashers.Clothing)
                            CreateItem(player.player, item.Key, 1, item.Value, "wear", null, null);

                        var weapon = configData.Slashers.Weapon;
                        CreateItem(player.player, weapon.Shortname, weapon.Amount, 0, "belt", weapon.AmmoType, weapon.Attachments);
                        CreateItem(player.player, weapon.AmmoType, weapon.AmmoAmount, 0, "main", null, null);

                        msg = "Kill the <color=#cc0000>Hunted</color>";
                    }
                    else
                    {
                        foreach (var item in configData.Players.Clothing)
                            CreateItem(player.player, item.Key, 1, item.Value, "wear", null, null);

                        var weapon = configData.Players.Weapon;
                        CreateItem(player.player, weapon.Shortname, weapon.Amount, 0, "belt", weapon.AmmoType, weapon.Attachments);
                        CreateItem(player.player, weapon.AmmoType, weapon.AmmoAmount, 0, "main", null, null);

                        msg = "Hide from the <color=#cc0000>Slasher</color>";
                    }
                    PopupMessage(player.player, msg);
                }
            }
            else
            {
                foreach (var player in SlasherPlayers)
                {
                    string msg;
                    if (player.team == Team.SLASHER)
                    {
                        msg = "The hunter has become the <color=#cc0000>Hunted</color>";
                    }
                    else
                    {                        
                        var weapon = configData.Slashers.Weapon;
                        CreateItem(player.player, weapon.Shortname, weapon.Amount, 0, "belt", weapon.AmmoType, weapon.Attachments);
                        CreateItem(player.player, weapon.AmmoType, weapon.AmmoAmount, 0, "main", null, null);

                        msg = "Hunt down the <color=#cc0000>Slasher</color>";
                    }
                    PopupMessage(player.player, msg);
                }
            }
        }
        void PopupMessage(BasePlayer player, string message)
        {            
            var Main = EventManager.UI.CreateElementContainer(SlasherPopup, "0 0 0 0", $"0.25 0.9", $"0.75 0.96", false, "Hud");
            EventManager.UI.CreateLabel(ref Main, SlasherPopup, "", message, 17, "0 0", "1 1");
            CuiHelper.AddUi(player, Main);
            timer.Once(6, () => CuiHelper.DestroyUi(player, SlasherPopup));
        }
        void DestroyTimers()
        {
            if (roundTimer != null)            
                UnityEngine.Object.Destroy(roundTimer);
            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, SlasherClock);
        }
        void EndRound()
        {
            DestroyTimers();
            EventManager.BroadcastEvent("Time has run out, the slasher wins this round!");
            if (roundNumber >= configData.GameSettings.RoundsToPlay)
                EventManager.PopupMessage("The next round starts in 10 seconds");
            timer.Once(10, () => NextRound());
        }
        void CreateItem(BasePlayer player, string shortname, int amount, ulong skin, string container, string ammoType, string[] contents)
        {
            if (shortname == null) return;
            var itemDef = ItemManager.FindItemDefinition(shortname);
            if (itemDef == null)
            {
                PrintError($"Unable to find a item definition for {shortname}. Please check your config for valid entries");
                return;
            }
            var item = ItemManager.Create(itemDef, amount, skin);
            if (item == null)
            {
                PrintError($"Error creating item: {shortname}. Please check your config for valid entries");
                return;
            }
            var weapon = item.GetHeldEntity() as BaseProjectile;
            if (weapon != null)
            {
                if (!string.IsNullOrEmpty(ammoType))
                {
                    var type = ItemManager.FindItemDefinition(ammoType);
                    if (ammoType != null)
                    {
                        weapon.primaryMagazine.ammoType = type;
                        weapon.primaryMagazine.contents = weapon.primaryMagazine.capacity;
                    }
                }                
            }
            if (contents != null)
                foreach (var content in contents)
                    ItemManager.CreateByName(content)?.MoveToContainer(item.contents);

            ItemContainer invContainer;
            switch (container)
            {
                case "wear":
                    invContainer = player.inventory.containerWear;
                    break;
                case "belt":
                    invContainer = player.inventory.containerBelt;
                    break;
                default:
                    invContainer = player.inventory.containerMain;
                    break;
            }
            item.MoveToContainer(invContainer);
        }
        int GetRemainingCount()
        {
            int remaining = 0;
            foreach (var user in SlasherPlayers)
                if (user.team == Team.HUNTED)
                    remaining++;
            return remaining;
        }
        #endregion

        #region Scoring        
        void AddKill(BasePlayer victim, BasePlayer attacker = null)
        {
            var vic = victim.GetComponent<SlasherPlayer>();
            var att = attacker?.GetComponent<SlasherPlayer>();
                        
            if (vic.team == Team.SLASHER)
            {
                vic.team = Team.DEAD;

                if (att != null)
                    nextSlasher = att.player;
                else nextSlasher = GetRandomSlasher();

                foreach(var survivor in SlasherPlayers.Where(x => x.team == Team.HUNTED))
                    EventManager.AddTokens(survivor.player.userID, configData.EventSettings.TokensOnSurvival);

                EventManager.PopupMessage("The slasher has been killed!");
                EndRound();
            }
            else
            {
                vic.team = Team.DEAD;

                if (att != null)
                {
                    if (att.team == Team.SLASHER)
                    {
                        att.kills++;
                        EventManager.AddTokens(att.player.userID, configData.EventSettings.TokensOnKill);

                        if (GetRemainingCount() > 0)
                        {
                            EventManager.PopupMessage(string.Format("{0} players remaining", GetRemainingCount()));                            
                        }
                        else
                        {
                            EventManager.AddTokens(att.player.userID, configData.EventSettings.TokensOnSurvival);
                            EventManager.PopupMessage("All the players are dead. The slasher wins this round!");
                            EndRound();
                        }
                    }
                }
            }
            UpdateScores();
        }
        void CheckScores(bool timelimit = false)
        {
            if (gameEnding) return;
            if (SlasherPlayers.Count == 0)
            {
                EventManager.BroadcastToChat("There are no more players in the event");
                EventManager.CloseEvent();
                EventManager.EndEvent();
                return;
            }
            if (SlasherPlayers.Count == 1)
            {
                Winner(SlasherPlayers[0].player);
                return;
            }

            if (timelimit)
            {
                BasePlayer winner = null;
                int score = 0;
                foreach (var slPlayer in SlasherPlayers)
                {
                    if (slPlayer.kills > score)
                    {
                        winner = slPlayer.player;
                    }
                }
                if (winner != null)
                    Winner(winner);
                return;
            }
        }
        void Winner(BasePlayer player)
        {
            gameEnding = true;
            if (player != null)
            {
                EventManager.AddTokens(player.userID, configData.EventSettings.TokensOnWin, true);
                EventManager.BroadcastToChat(string.Format("{0} has won the event!", player.displayName));
            }
            EventManager.CloseEvent();
            EventManager.EndEvent();
        }
        #endregion

        #region Classes
        class SlasherPlayer : MonoBehaviour
        {
            public BasePlayer player;
            public int kills;
            public Team team;

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                enabled = false;
                kills = 0;
            }
        }
        class RoundTimer : MonoBehaviour
        {
            internal int slasherTime;
            internal int playerTime;
            internal bool initialRound;

            internal int timeRemaining;
            void Awake() => timeRemaining = 0;

            public void StartTimer(int slasherTime, int playerTime)
            {
                this.slasherTime = slasherTime;
                this.playerTime = playerTime;
                timeRemaining = slasherTime;
                initialRound = true;
                InvokeRepeating("TimerTick", 1f, 1f);
            }
            void OnDestroy()
            {
                CancelInvoke("TimerTick");
                Destroy(gameObject);
            }
            internal void TimerTick()
            {
                timeRemaining--;
                
                if (timeRemaining == 0)
                {
                    CancelInvoke("TimerTick");
                    if (initialRound)
                    {
                        initialRound = false;
                        timeRemaining = playerTime;
                        InvokeRepeating("TimerTick", 1f, 1f);
                        instance.StartRoundTimers(false);
                    } 
                    else
                    {
                        instance.EndRound();
                    }                   
                }
                else UpdateUITimer();
            }
            internal void UpdateUITimer()
            {
                string clockTime = "";
                TimeSpan dateDifference = TimeSpan.FromSeconds(timeRemaining);
                var hours = dateDifference.Hours;
                var mins = dateDifference.Minutes;
                var secs = dateDifference.Seconds;
                if (hours > 0)
                    clockTime = string.Format("{0:00}:{1:00}:{2:00}", hours, mins, secs);
                else clockTime = string.Format("{0:00}:{1:00}", mins, secs);

                var CUI = EventManager.UI.CreateElementContainer(SlasherClock, "0.1 0.1 0.1 0.7", "0.45 0.95", "0.55 0.99", false, "Hud.Under");
                EventManager.UI.CreateLabel(ref CUI, SlasherClock, "", clockTime, 16, "0 0", "1 1");
                foreach (var ePlayer in instance.SlasherPlayers)
                {
                    CuiHelper.DestroyUi(ePlayer.player, SlasherClock);
                    CuiHelper.AddUi(ePlayer.player, CUI);
                }
            }
            internal void DestroyUI()
            {
                foreach (var player in BasePlayer.activePlayerList)
                    CuiHelper.DestroyUi(player, SlasherClock);
            }
        }
        class SlasherWeapon
        {
            public string Shortname;
            public int Amount;
            public string AmmoType;
            public int AmmoAmount;
            public string[] Attachments;
        }
        enum Team
        {
            DEAD,
            SLASHER,
            HUNTED            
        }
        #endregion

        #region Config        
        private ConfigData configData;
        class EventSettings
        {
            public string DefaultZoneID { get; set; }
            public int TokensOnKill { get; set; }
            public int TokensOnSurvival { get; set; }
            public int TokensOnWin { get; set; }
        }
        class GameSettings
        {
            public float FFDamageModifier { get; set; }
            public float StartHealth { get; set; }
            public int RoundsToPlay { get; set; }
            public bool AutoStart_Use { get; set; }
            public float Auto_OpenTime { get; set; }
            public float Auto_StartTime { get; set; }
            public float Auto_EndTime { get; set; }
            public int Auto_CancelTimer { get; set; }
            public int GameTimer_Slasher { get; set; }
            public int GameTimer_Player { get; set; }
            public string DefaultSpawnfile { get; set; }
            public bool AdjustTimeToSuitRounds { get; set; }
        }
        class TeamSettings
        {
            public SlasherWeapon Weapon { get; set; }
            public Dictionary<string, ulong> Clothing { get; set; }
            public float DamageModifier { get; set; }            
        }
        class Messaging
        {
            public string MainColor { get; set; }
            public string MSGColor { get; set; }
        }        
        class ConfigData
        {
            public EventSettings EventSettings { get; set; }
            public GameSettings GameSettings { get; set; }
            public TeamSettings Slashers { get; set; }
            public TeamSettings Players { get; set; }
            public Messaging Messaging { get; set; }
            
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
            playerSpawns = configData.GameSettings.DefaultSpawnfile;
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                EventSettings = new EventSettings
                {
                    DefaultZoneID = "slasherzone",
                    TokensOnKill = 1,
                    TokensOnSurvival = 1,
                    TokensOnWin = 5
                },
                GameSettings = new GameSettings
                {
                    AdjustTimeToSuitRounds = true,
                    AutoStart_Use = true,
                    Auto_CancelTimer = 6,
                    Auto_OpenTime = 18f,
                    Auto_EndTime = 6f,
                    Auto_StartTime = 20f,
                    FFDamageModifier = 0.4f,
                    GameTimer_Player = 90,
                    GameTimer_Slasher = 150,
                    RoundsToPlay = 3,
                    DefaultSpawnfile = "slasher_spawns",
                    StartHealth = 100
                },
                Messaging = new Messaging
                {
                    MainColor = "<color=orange>",
                    MSGColor = "<color=#939393>"
                },
                Players = new TeamSettings
                {
                    DamageModifier = 2.2f,
                    Clothing = new Dictionary<string, ulong>
                    {
                        {"tshirt", 10039 },
                        {"pants", 10078 },
                        {"shoes.boots", 10044 }
                    },              
                    Weapon = new SlasherWeapon
                    {
                        AmmoAmount = 0,
                        AmmoType = "",
                        Amount = 2,
                        Attachments = new string[0],
                        Shortname = "torch"
                    }
                },
                Slashers = new TeamSettings
                {
                    DamageModifier = 1.0f,
                    Clothing = new Dictionary<string, ulong>
                    {
                        {"tshirt", 10038 },
                        {"pants", 10078 },
                        {"shoes.boots", 10044 },
                        {"mask.bandana", 10064 }
                    },                    
                    Weapon = new SlasherWeapon
                    {
                        AmmoAmount = 40,
                        AmmoType = "ammo.shotgun.slug",
                        Amount = 1,
                        Attachments = new string[] { "weapon.mod.flashlight" },
                        Shortname = "shotgun.pump"
                    }
                }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion        
    }
}