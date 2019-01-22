// Requires: EventManager
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Core.Plugins;
using Rust;

namespace Oxide.Plugins
{
    [Info("TeamDeathmatch", "k1lly0u", "0.3.52", ResourceId = 1484)]
    class TeamDeathmatch : RustPlugin
    {
        #region Fields        
        [PluginReference]
        EventManager EventManager;

        [PluginReference]
        Plugin Spawns;

        private bool UseTDM;
        private bool Started;
        private bool GameEnding;

        public string Kit;
        public string TeamASpawns;
        public string TeamBSpawns;

        public int TeamAKills;
        public int TeamBKills;

        public int ScoreLimit;

        private List<TDMPlayer> TDMPlayers = new List<TDMPlayer>();
        private ConfigData configData;
        #endregion

        #region Oxide Hooks       
        void Loaded()
        {
            lang.RegisterMessages(messages, this);
            UseTDM = false;
            Started = false;
        }
        void OnServerInitialized()
        {            
            LoadVariables();
            TeamASpawns = configData.TeamA.Spawnfile;
            TeamBSpawns = configData.TeamB.Spawnfile;
            ScoreLimit = configData.GameSettings.ScoreLimit;
            //RegisterGame();
        }
        void RegisterGame()
        {
            EventManager.Events eventData = new EventManager.Events
            {
                CloseOnStart = false,
                DisableItemPickup = false,
                EnemiesToSpawn = 0,
                EventType = Title,
                GameMode = EventManager.GameMode.Normal,
                GameRounds = 0,
                Kit = configData.EventSettings.DefaultKit,
                MaximumPlayers = 0,
                MinimumPlayers = 2,
                ScoreLimit = configData.GameSettings.ScoreLimit,
                Spawnfile = configData.TeamA.Spawnfile,
                Spawnfile2 = configData.TeamB.Spawnfile,
                SpawnType = EventManager.SpawnType.Consecutive,                
                RespawnType = EventManager.RespawnType.Timer,
                RespawnTimer = 5,
                UseClassSelector = false,
                WeaponSet = null,
                ZoneID = configData.EventSettings.DefaultZoneID
            };
            EventManager.EventSetting eventSettings = new EventManager.EventSetting
            {
                CanChooseRespawn = true,
                CanUseClassSelector = true,
                CanPlayBattlefield = true,
                ForceCloseOnStart = false,
                IsRoundBased = false,
                LockClothing = true,                
                RequiresKit = true,
                RequiresMultipleSpawns = true,
                RequiresSpawns = true,
                ScoreType = "Kills",
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
            if (UseTDM && Started)            
                EventManager.EndEvent();

            var objects = UnityEngine.Object.FindObjectsOfType<TDMPlayer>();
            if (objects != null)
                foreach (var gameObj in objects)
                    UnityEngine.Object.Destroy(gameObj);
        }
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (UseTDM && Started)
            {
                if (entity is BasePlayer && hitinfo?.Initiator is BasePlayer)
                {
                    var victim = entity.GetComponent<TDMPlayer>();
                    var attacker = hitinfo.Initiator.GetComponent<TDMPlayer>();
                    if (victim != null && attacker != null && victim.player.userID != attacker.player.userID)
                    {
                        if (victim.team == attacker.team)
                        {
                            if (configData.GameSettings.FFDamageModifier <= 0)
                            {
                                hitinfo.damageTypes = new DamageTypeList();
                                hitinfo.DoHitEffects = false;
                            }
                            else
                                hitinfo.damageTypes.ScaleAll(configData.GameSettings.FFDamageModifier);
                            SendReply(attacker.player, TitleM() + lang.GetMessage("ff", this, attacker.player.UserIDString));
                        }
                    }
                }
            }
        }
        #endregion

        #region EventManager Hooks       
        void OnSelectEventGamePost(string name)
        {
            if (Title == name)
            {
                if (!string.IsNullOrEmpty(TeamASpawns) && !string.IsNullOrEmpty(TeamBSpawns))
                {
                    UseTDM = true;
                }
                else Puts("Check your config for valid spawn entries");
            }
            else
                UseTDM = false;
        }
        void OnEventPlayerSpawn(BasePlayer player)
        {
            if (UseTDM && Started && !GameEnding)
            {
                if (!player.GetComponent<TDMPlayer>()) return;
                if (player.IsSleeping())
                {
                    player.EndSleeping();
                    timer.In(1, () => OnEventPlayerSpawn(player));
                    return;
                }                
                GiveTeamGear(player);
                EventManager.CreateScoreboard(player);
            }
        }
        private void GiveTeamGear(BasePlayer player)
        {
            if (!GameEnding)
            {
                player.health = configData.GameSettings.StartHealth;
                EventManager.GivePlayerKit(player, Kit);                
            }
        }
        private void OnEventKitGiven(BasePlayer player)
        {
            if (UseTDM)
            {
                GiveTeamShirts(player);
            }
        }
        private void GiveTeamShirts(BasePlayer player)
        {
            if (player.GetComponent<TDMPlayer>().team == Team.A)
            {
                foreach (var item in configData.TeamA.ClothingItems)
                {
                    Item clothing = ItemManager.CreateByPartialName(item.Key);
                    clothing.skin = item.Value;
                    clothing.MoveToContainer(player.inventory.containerWear);
                }
            }
            if (player.GetComponent<TDMPlayer>().team == Team.B)
            {
                foreach (var item in configData.TeamB.ClothingItems)
                {
                    Item clothing = ItemManager.CreateByPartialName(item.Key);
                    clothing.skin = item.Value;
                    clothing.MoveToContainer(player.inventory.containerWear);
                }
            }
        }       
                
        object OnEventOpenPost()
        {
            if (UseTDM)
                PrintToChat(TitleM() + lang.GetMessage("OpenMsg", this));
            return null;
        }        
        object OnEventEndPre()
        {
            if (UseTDM && Started)
            {
                CheckScores(true); 
            }
            return null;
        }        
        object OnEventCancel()
        {
            if (UseTDM && Started)
                CheckScores(true);
            return null;
        }

        object OnEventEndPost()
        {
            if (UseTDM)
            {
                Started = false;
                foreach (TDMPlayer tdmPlayer in TDMPlayers)
                {
                    tdmPlayer.team = Team.NONE;
                    UnityEngine.Object.Destroy(tdmPlayer);
                }
                TDMPlayers.Clear();
                TeamAKills = 0;
                TeamBKills = 0;

                TDMPlayers.Clear();
            }
            return null;
        }
        object OnEventStartPre()
        {
            if (UseTDM)
            {
                Started = true;
                GameEnding = false;
            }
            return null;
        }
        object OnEventStartPost()
        {
            if (UseTDM)            
                UpdateScores();
            return null;
        }
        object CanEventJoin(BasePlayer player)
        {
            if (UseTDM)
                if (player.GetComponent<TDMPlayer>())
                    player.GetComponent<TDMPlayer>().team = Team.NONE;
            return null;
        }
        object OnSelectKit(string kitname)
        {
            if (UseTDM)
            {
                Kit = kitname;
                return true;
            }
            return null;
        }
        object OnEventJoinPost(BasePlayer player)
        {
            if (UseTDM)
            {
                if (player.GetComponent<TDMPlayer>())
                    UnityEngine.Object.Destroy(player.GetComponent<TDMPlayer>());
                TDMPlayers.Add(player.gameObject.AddComponent<TDMPlayer>());
                if (Started)                
                    TeamAssign(player);                
            }
            return null;
        }
        object OnEventLeavePost(BasePlayer player)
        {
            if (UseTDM)
            {
                var tDMPlayer = player.GetComponent<TDMPlayer>();
                if (tDMPlayer)
                {                    
                    TDMPlayers.Remove(tDMPlayer);
                    UnityEngine.Object.Destroy(tDMPlayer);
                    CheckScores();                    
                }
            }            
            return null;
        }
        void OnEventPlayerAttack(BasePlayer attacker, HitInfo hitinfo)
        {
            if (UseTDM)
            {
                if (!(hitinfo.HitEntity is BasePlayer))
                {
                    hitinfo.damageTypes = new DamageTypeList();
                    hitinfo.DoHitEffects = false;
                }
            }
        }
        void OnEventPlayerDeath(BasePlayer vic, HitInfo hitinfo)
        {
            if (UseTDM && Started)
            {                
                if (hitinfo.Initiator != null && vic != null)
                {
                    if (vic.GetComponent<TDMPlayer>())
                    {
                        var victim = vic.GetComponent<TDMPlayer>();
                        if (hitinfo.Initiator is BasePlayer)
                        {

                            var attacker = hitinfo.Initiator.GetComponent<TDMPlayer>();
                            if ((victim.player.userID != attacker.player.userID) && (attacker.team != victim.team))
                            {
                                attacker.kills++;
                                AddKill(attacker.player, victim.player);
                            }
                        }
                    }
                }
            }
            return;
        }      
        
        object EventChooseSpawn(BasePlayer player, Vector3 destination)
        {
            if (UseTDM)
            {
                if (!CheckForTeam(player))
                {
                    TeamAssign(player);
                    return false;
                }
                Team team = player.GetComponent<TDMPlayer>().team;
                object newpos = EventManager.SpawnCount.GetSpawnPoint(team == Team.A ? TeamASpawns : TeamBSpawns, team == Team.A); 
                               
                if (!(newpos is Vector3))
                {
                    Puts("Error finding a spawn point, spawnfile corrupt or invalid");
                    return null;
                }
                return (Vector3)newpos;
            }
            return null;
        }
        void SetSpawnfile(bool isTeamA, string spawnfile)
        {
            if (isTeamA)
                TeamASpawns = spawnfile;
            else TeamBSpawns = spawnfile;
        }
        void SetScoreLimit(int scoreLimit) => ScoreLimit = scoreLimit;
        #endregion

        #region team funtions
        enum Team
        {
            A,
            B,
            NONE
        }
        private bool CheckForTeam(BasePlayer player)
        {
            if (!player.GetComponent<TDMPlayer>())
                TDMPlayers.Add(player.gameObject.AddComponent<TDMPlayer>());
            if (player.GetComponent<TDMPlayer>().team == Team.NONE)
                return false;
            return true;
        }
        private void TeamAssign(BasePlayer player)
        {
            if (UseTDM && Started && !GameEnding)
            {
                Team team = CountForBalance();
                if (player.GetComponent<TDMPlayer>().team == Team.NONE)
                {
                    player.GetComponent<TDMPlayer>().team = team;
                    string color = team == Team.A ? configData.TeamA.Color : configData.TeamB.Color;
                    SendReply(player, string.Format(lang.GetMessage("AssignTeam", this, player.UserIDString), GetTeamName(team, player), color));
                    Puts("Player " + player.displayName + " assigned to Team " + team);
                    //player.Respawn();                    
                }
            }
        }

        private string GetTeamName(Team team, BasePlayer player = null)
        {
            switch (team)
            {
                case Team.A:
                    return lang.GetMessage("TeamA", this, player?.UserIDString);
                case Team.B:
                    return lang.GetMessage("TeamB", this, player?.UserIDString);
                default:
                    return lang.GetMessage("TeamNone", this, player?.UserIDString);
            }
        }
        private Team CountForBalance()
        {
            Team PlayerNewTeam;
            int aCount = Count(Team.A);
            int bCount = Count(Team.B);

            if (aCount > bCount) PlayerNewTeam = Team.B;
            else PlayerNewTeam = Team.A;

            return PlayerNewTeam;
        }
        private int Count(Team team)
        {
            int count = 0;
            foreach (var player in TDMPlayers)
            {
                if (player.team == team) count++;
            }
            return count;
        }
        #endregion

        #region Scoreboard        
        private void UpdateScores()
        {
            if (UseTDM && Started && configData.EventSettings.ShowScoreboard)
            {
                var sortedList = TDMPlayers.OrderByDescending(pair => pair.kills).ToList();
                var scoreList = new Dictionary<ulong, EventManager.Scoreboard>();
                foreach (var entry in sortedList)
                {
                    if (scoreList.ContainsKey(entry.player.userID)) continue;
                    scoreList.Add(entry.player.userID, new EventManager.Scoreboard { Name = entry.player.displayName, Position = sortedList.IndexOf(entry), Score = entry.kills });
                }
                var scoreString = $"{GetTeamName(Team.A)} kills : <color={configData.TeamA.Color}>{TeamAKills}</color>   ||   {GetTeamName(Team.B)} Kills : <color={configData.TeamB.Color}>{TeamBKills}</color>";
                EventManager.UpdateScoreboard(new EventManager.ScoreData { Additional = scoreString, ScoreType = "Kills", Scores = scoreList });
            }
        }
        #endregion

        #region Functions        
        List<TDMPlayer> FindPlayer(string arg)
        {
            var foundPlayers = new List<TDMPlayer>();

            ulong steamid;
            ulong.TryParse(arg, out steamid);
            string lowerarg = arg.ToLower();

            foreach (var p in TDMPlayers)
            {
                if (steamid != 0L)
                    if (p.player.userID == steamid)
                    {
                        foundPlayers.Clear();
                        foundPlayers.Add(p);
                        return foundPlayers;
                    }
                string lowername = p.player.displayName.ToLower();
                if (lowername.Contains(lowerarg))
                {
                    foundPlayers.Add(p);
                }
            }
            return foundPlayers;
        }
        void SendMessage(string message)
        {
            if (configData.EventSettings.UseUINotifications)
                EventManager.PopupMessage(message);
            else PrintToChat(message);
        }
        #endregion

        #region Commands 
        [ConsoleCommand("tdm.team")]
        private void cmdTeam(ConsoleSystem.Arg arg)
        {
            if (!UseTDM) return;
            if (!isAuth(arg)) return;
            if (arg.Args == null || arg.Args.Length != 2)
            {
                SendReply(arg, "Format: tdm.team \"playername\" \"A\" or \"B\"");
                return;
            }
            var fplayer = FindPlayer(arg.Args[0]);
            if (fplayer.Count == 0)
            {
                SendReply(arg, "No players found.");
                return;
            }
            if (fplayer.Count > 1)
            {
                SendReply(arg, "Multiple players found.");
                return;
            }
            var newTeamArg = arg.Args[1].ToUpper();
            var newTeam = Team.NONE;
            switch (newTeamArg)
            {
                case "A":
                    newTeam = Team.A;
                    break;

                case "B":
                    newTeam = Team.B;
                    break;
                default:
                    return;
            }
            var p = fplayer[0].GetComponent<TDMPlayer>();
            var currentTeam = p.team;

            if (newTeam == currentTeam)
            {
                SendReply(arg, p.player.displayName + " is already on " + currentTeam);
                return;
            }
            p.team = newTeam;
            p.player.Hurt(300, DamageType.Bullet, null, true);

            string color = string.Empty;
            if (p.team == Team.A) color = configData.TeamA.Color;
            else if (p.team == Team.B) color = configData.TeamB.Color;

            SendReply(p.player, string.Format(TitleM() + "You have been moved to <color=" + color + ">Team {0}</color>", newTeam.ToString().ToUpper()));
            SendReply(arg, string.Format("{0} has been moved to Team {1}", p.player.displayName, newTeam.ToString().ToUpper()));
        }
        bool isAuth(ConsoleSystem.Arg arg)
        {
            if (arg.Connection?.authLevel < 1)
            {
                SendReply(arg, "You dont not have permission to use this command.");
                return false;
            }
            return true;
        }
        #endregion      

        #region Scoring

        void AddKill(BasePlayer player, BasePlayer victim)
        {
            var p = player.GetComponent<TDMPlayer>();
            if (!p) return;

            if (p.team == Team.A) TeamAKills++;
            else if (p.team == Team.B) TeamBKills++;
            UpdateScores();

            EventManager.AddTokens(player.userID, configData.EventSettings.TokensOnKill);
            string color = string.Empty;
            if (p.team == Team.A) color = configData.TeamA.Color;
            else if (p.team == Team.B) color = configData.TeamB.Color;
            SendMessage(string.Format(lang.GetMessage("KillMsg", this), player.displayName, victim.displayName));
            CheckScores();
        }
        void CheckScores(bool timelimit = false)
        {
            if (GameEnding) return;
            if (TDMPlayers.Count == 0)
            {
                GameEnding = true;
                EventManager.BroadcastToChat(lang.GetMessage("NoPlayers", this));
                EventManager.CloseEvent();
                EventManager.EndEvent();
                return;
            }

            if (TDMPlayers.Count == 1)
            {
                Winner(TDMPlayers[0].team);
                return;
            }
            if (timelimit)
            {
                if (TeamAKills > TeamBKills) Winner(Team.A);
                if (TeamBKills > TeamAKills) Winner(Team.B);
                if (TeamAKills == TeamBKills) Winner(Team.NONE);
                return;
            }
            if (EventManager._Event.GameMode == EventManager.GameMode.Battlefield)
                return;

            if (ScoreLimit > 0)
            {
                if (TeamAKills >= ScoreLimit) Winner(Team.A);
                if (TeamBKills >= ScoreLimit) Winner(Team.B);
            }        
        }
        void Winner(Team team)
        {
            GameEnding = true;
            foreach (var member in TDMPlayers)
                if (member.team == team)
                    EventManager.AddTokens(member.player.userID, configData.EventSettings.TokensOnWin, true);

            if (team == Team.NONE)
                EventManager.BroadcastToChat("It's a draw! No winners today");
            else EventManager.BroadcastToChat(string.Format("{0} has won the event!", GetTeamName(team)));
            EventManager.CloseEvent();
            EventManager.EndEvent();
        }
        #endregion

        #region Class      
        class TDMPlayer : MonoBehaviour
        {
            public BasePlayer player;
            public Team team;  
            public int kills;          

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                enabled = false;
                team = Team.NONE;
                kills = 0;
            }
        }

        #endregion

        #region Config  
        class EventSettings
        {
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
            public float FFDamageModifier { get; set; }
            public int ScoreLimit { get; set; }
        }
        class TeamSettings
        {
            public Dictionary<string, ulong> ClothingItems { get; set; }
            public string Color { get; set; }
            public string Spawnfile { get; set; }
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
            public TeamSettings TeamA { get; set; }
            public TeamSettings TeamB { get; set; }
            public Messaging Messaging { get; set; }            
        }  
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        
        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();
        }
        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new config file");
            var config = new ConfigData
            {
                EventSettings = new EventSettings
                {
                    DefaultKit = "tdmkit",
                    DefaultZoneID = "tdmzone",
                    TokensOnKill = 1,
                    TokensOnWin = 5,
                    ShowScoreboard = true,
                    UseUINotifications = true
                },
                GameSettings = new GameSettings
                {
                    FFDamageModifier = 0,
                    ScoreLimit = 10,
                    StartHealth = 100
                },
                TeamA = new TeamSettings
                {
                    Color = "#33CC33",
                    ClothingItems = new Dictionary<string, ulong>
                    {
                        { "tshirt", 0 }
                    },
                    Spawnfile = "tdmspawnsa"
                },
                TeamB = new TeamSettings
                {
                    Color = "#003366",
                    ClothingItems = new Dictionary<string, ulong>
                    {
                        { "tshirt", 14177 }
                    },
                    Spawnfile = "tdmspawnsb"
                },
                Messaging = new Messaging
                {
                    MainColor = "<color=orange>",
                    MSGColor = "<color=#939393>"
                }                
            };
            SaveConfig(config);
        }
        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        object GetEventConfig(string configname)
        {
            if (!UseTDM) return null;
            return Config[configname];
        }
        #endregion

        #region messages
        private string TitleM() => $"<color=orange>{Title}: </color>";
        Dictionary<string, string> messages = new Dictionary<string, string>
        {
            {"WinMsg", "Team {0} has won the game!" },
            {"NoPlayers", "Not enough players to continue. Ending event" },
            {"KillMsg", "{0} killed {1}" },
            {"OpenMsg", "Use tactics and work together to defeat the enemy team" },
            {"skillTime", "{0} {1} left to kill the slasher!" },
            {"ff", "Don't shoot your team mates!"},
            {"AssignTeam", "You have been assigned to <color={1}>Team {0}</color>"},
            {"TeamA", "A" },
            {"TeamB", "B" },
            {"TeamNone", "None" },
            {"Draw", "The game ended in a draw!" }
        };
        #endregion
    }
}