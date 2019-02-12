// Requires: EventManager
using System.Collections.Generic;
using UnityEngine;
using Rust;


namespace Oxide.Plugins
{
    [Info("LastManStanding", "k1lly0u", "2.0.22", ResourceId = 1663)]
    class LastManStanding : RustPlugin
    {
        #region Fields
        [PluginReference] EventManager EventManager;

        private List<LMSPlayer> LMSPlayers;
        private bool usingLMS;
        private bool hasStarted;
        private bool isEnding;

        private string Kit;
        #endregion

        #region Player Class
        class LMSPlayer : MonoBehaviour
        {
            public BasePlayer player;
            public int kills;

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                enabled = false;
                kills = 0;
            }
        }
        #endregion

        #region Oxide Hooks
        void Loaded()
        {
            hasStarted = false;
            usingLMS = false;
            LMSPlayers = new List<LMSPlayer>();
        }

        void OnServerInitialized()
        {
            LoadVariables();
            //RegisterGame();
        }
        void RegisterGame()
        {
            EventManager.Events eventData = new EventManager.Events
            {
                CloseOnStart = true,
                DisableItemPickup = false,
                EnemiesToSpawn = 0,
                EventType = Title,
                GameMode = EventManager.GameMode.Normal,
                GameRounds = 0,
                Kit = configData.EventSettings.DefaultKit,
                MaximumPlayers = 0,
                MinimumPlayers = 2,
                ScoreLimit = 0,
                Spawnfile = configData.EventSettings.DefaultSpawnfile,
                Spawnfile2 = null,
                SpawnType = EventManager.SpawnType.Random,
                RespawnType = EventManager.RespawnType.Timer,
                RespawnTimer = 60,
                UseClassSelector = false,
                WeaponSet = null,
                ZoneID = configData.EventSettings.DefaultZoneID
            };
            EventManager.EventSetting eventSettings = new EventManager.EventSetting
            {
                CanChooseRespawn = false,
                CanUseClassSelector = true,
                CanPlayBattlefield = false,
                ForceCloseOnStart = true,
                IsRoundBased = false,
                RequiresKit = true,
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
        }
        
        void Unload()
        {
            if (usingLMS && hasStarted)
                EventManager.EndEvent();

            var objects = UnityEngine.Object.FindObjectsOfType<LMSPlayer>();
            if (objects != null)
                foreach (var gameObj in objects)
                    UnityEngine.Object.Destroy(gameObj);
        }
        #endregion

        #region Scoreboard        
        private void UpdateScores()
        {
            if (usingLMS && hasStarted && configData.EventSettings.ShowScoreboard)
            {
                var remainingPlayers = new Dictionary<ulong, EventManager.Scoreboard>();
                foreach (var player in LMSPlayers)
                {
                    if (remainingPlayers.ContainsKey(player.player.userID)) continue;
                    remainingPlayers.Add(player.player.userID, new EventManager.Scoreboard { Name = player.player.displayName });
                }
                EventManager.UpdateScoreboard(new EventManager.ScoreData { Additional = $"Players remaining : {LMSPlayers.Count}", Scores = remainingPlayers, ScoreType = null });
            }
        }
        void SendMessage(string message)
        {
            if (configData.EventSettings.UseUINotifications)
                EventManager.PopupMessage(message);
            else PrintToChat(message);
        }
        #endregion

        #region Event Manager Hooks
        void OnSelectEventGamePost(string name)
        {
            if (Title == name)
            {
                usingLMS = true;                
            }
            else
                usingLMS = false;
        }
        void OnEventPlayerSpawn(BasePlayer player)
        {
            if (usingLMS && hasStarted && !isEnding)
            {
                if (!player.GetComponent<LMSPlayer>()) return;
                if (player.IsSleeping())
                {
                    player.EndSleeping();
                    timer.In(1, () => OnEventPlayerSpawn(player));
                    return;
                }                
                EventManager.GivePlayerKit(player, Kit);
                player.health = configData.GameSettings.StartHealth;
            }
        }

        object OnEventOpenPost()
        {
            if (usingLMS)
                EventManager.BroadcastToChat("Become victorious by surviving for the longest time.");
            return null;
        }
        object OnEventCancel()
        {
            if (usingLMS && hasStarted)
                EventManager.EndEvent();
            //CheckScores();
            return null;
        }        
        object OnEventEndPost()
        {
            if (usingLMS)
            {
                hasStarted = false;
                LMSPlayers.Clear();
            }
            return null;
        }
        object OnEventStartPre()
        {
            if (usingLMS)
            {
                hasStarted = true;
                isEnding = false;
            }
            return null;
        }
        object OnEventStartPost()
        {
            if (usingLMS)
            {
                EventManager.CloseEvent();
                UpdateScores();
            }
            return null;
        }        
        object OnSelectKit(string kitname)
        {
            if (usingLMS)
            {
                Kit = kitname;
                return true;
            }
            return null;
        }
        object OnEventJoinPost(BasePlayer player)
        {
            if (usingLMS)
            {
                if (player.GetComponent<LMSPlayer>())
                    UnityEngine.Object.Destroy(player.GetComponent<LMSPlayer>());
                LMSPlayers.Add(player.gameObject.AddComponent<LMSPlayer>());
                EventManager.CreateScoreboard(player);
            }
            return null;
        }
        object OnEventLeavePost(BasePlayer player)
        {
            if (usingLMS)
            {
                if (player.GetComponent<LMSPlayer>())
                {
                    LMSPlayers.Remove(player.GetComponent<LMSPlayer>());
                    UnityEngine.Object.Destroy(player.GetComponent<LMSPlayer>());
                    EventManager.BroadcastEvent(string.Format("{0} has left the event. {1} player(s) remaining!", player.displayName, LMSPlayers.Count));
                    CheckScores();
                }
            }
            return null;
        }
        void OnEventPlayerAttack(BasePlayer attacker, HitInfo hitinfo)
        {
            if (usingLMS)
            {
                if (!(hitinfo.HitEntity is BasePlayer))
                {
                    hitinfo.damageTypes = new DamageTypeList();
                    hitinfo.DoHitEffects = false;
                }
            }
        }

        void OnEventPlayerDeath(BasePlayer victim, HitInfo hitinfo)
        {
            if (usingLMS)
            {
                if (hitinfo.Initiator != null)
                {
                    BasePlayer attacker = hitinfo.Initiator.ToPlayer();
                    if (attacker != null)
                    {
                        if (attacker != victim)
                        {                                                      
                            AddKill(attacker, victim);                            
                        }
                        else if (attacker == null || attacker == victim)
                        {
                            SendMessage(string.Format("Suicide is not the answer {0}. {1} player(s) remaining!", victim.displayName, LMSPlayers.Count - 1));                            
                            EventManager.LeaveEvent(victim);                            
                            CheckScores();
                            SendReply(victim, "You died and were kicked from the event");
                        }
                    }
                }
            }
            return;
        }
        object GetRespawnType()
        {
            if (usingLMS)
                return EventManager.RespawnType.Timer;
            return null;
        }
        object GetRespawnTime()
        {
            if (usingLMS)
                return 60;
            return null;
        }
        #endregion

        #region Scoring
        void AddKill(BasePlayer player, BasePlayer victim)
        {
            if (!player.GetComponent<LMSPlayer>())
                return;

            player.GetComponent<LMSPlayer>().kills++;
            EventManager.AddTokens(player.userID, configData.EventSettings.TokensOnKill);
            SendMessage(string.Format("{0} has killed {1}. {2} player(s) remaining!", player.displayName, victim.displayName, LMSPlayers.Count - 1));
            EventManager.LeaveEvent(victim);
            CheckScores();
            SendReply(victim, "You died and were kicked from the event");
        }
        void CheckScores()
        {
            if (isEnding) return;
            if (LMSPlayers.Count == 0)
            {
                isEnding = true;
                EventManager.BroadcastToChat("There are no more players in the event.");
                timer.Once(10, () => 
                {                    
                    EventManager.EndEvent();
                });
            }            
            else if (LMSPlayers.Count == 1)
            {
                Winner(LMSPlayers[0].player);
            } 
            else UpdateScores();
        }
        void Winner(BasePlayer player)
        {
            isEnding = true;
            EventManager.AddTokens(player.userID, configData.EventSettings.TokensOnWin, true);
            EventManager.BroadcastToChat(string.Format("{0} has won the event by being the last player alive!", player.displayName));
            timer.Once(10, () => 
            {
                EventManager.EndEvent();
            });         
            
        }
        #endregion

        #region Config        
        private ConfigData configData;
        class EventSettings
        {
            public string DefaultSpawnfile { get; set; }
            public string DefaultKit { get; set; }
            public string DefaultZoneID { get; set; }
            public int TokensOnKill { get; set; }
            public int TokensOnWin { get; set; }
            public bool ShowScoreboard { get; set; }
            public bool UseUINotifications { get; set; }
        }
        class GameSettings
        {
            public float StartHealth { get; set; }
        }
        class ConfigData
        {
            public EventSettings EventSettings { get; set; } 
            public GameSettings GameSettings { get; set; }           
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
                EventSettings = new EventSettings
                {
                    DefaultKit = "lmskit",
                    DefaultSpawnfile = "lmsspawns",
                    DefaultZoneID = "lmszone",
                    TokensOnKill = 1,
                    TokensOnWin = 5,
                    ShowScoreboard = true,
                    UseUINotifications = true
                },
                GameSettings = new GameSettings
                {
                    StartHealth = 100
                }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion       
    }
}