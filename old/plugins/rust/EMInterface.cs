using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Event Manager Menu Interface", "k1lly0u", "1.0.5", ResourceId = 2258)]
    class EMInterface : RustPlugin
    {
        #region Fields
        [PluginReference] EventManager EventManager;
        [PluginReference] Plugin ZoneManager;
        [PluginReference] Plugin Spawns;
        [PluginReference] Plugin Kits;
        [PluginReference] Plugin GunGame;
        [PluginReference] Plugin LustyMap;

        static EMInterface eminterface;

        private EventVoting EventVotes = new EventVoting();

        private Dictionary<string, string> ItemNames = new Dictionary<string, string>();
        private Dictionary<string, string> ClassContents = new Dictionary<string, string>();
        private Dictionary<ulong, Timer> PopupTimers = new Dictionary<ulong, Timer>();
        private Dictionary<ulong, List<string>> UIEntries = new Dictionary<ulong, List<string>>();
        private Dictionary<ulong, EventManager.Events> EventCreators = new Dictionary<ulong, EventManager.Events>();
        private Dictionary<ulong, AEConfig> AutoCreators = new Dictionary<ulong, AEConfig>();

        private string UIMain = "EMUI_Main";
        private string UIAdmin = "EMUI_Admin";
        private string UIPanel = "EMUI_Panel";
        private string UIPopup = "EMUI_Popup";
        private string UIEntry = "EMUI_Entry";

        private Dictionary<string, string> UIColors = new Dictionary<string, string>();
        private string Color1;
        private string Color2;

        public bool VotingOpen = false;
        public Timer VoteTallyTimer;

        private DynamicConfigFile E_Conf;
        public EventConfig Event_Config;
        #endregion

        #region Classes
        public class EventConfig
        {
            public Dictionary<string, EventManager.Events> Event_List = new Dictionary<string, EventManager.Events>();
            public List<string> Classes = new List<string>();
            public AutoEvents AutoEvent_Config = new AutoEvents();
        }        
        public class AutoEvents
        {
            public int GameInterval;
            public bool AutoCancel;
            public int AutoCancel_Timer;
            public List<AEConfig> AutoEvent_List = new List<AEConfig>();
        }
        public class AEConfig
        {
            public string EventConfig;            
            public int TimeLimit = 15;
            public int TimeToJoin = 120;
            public int TimeToStart = 60;
        }
        public class EventVoting
        {
            private static Dictionary<int, int> EventVotes = new Dictionary<int, int>();
            private static Dictionary<ulong, int> PlayerVotes = new Dictionary<ulong, int>();
            private static List<ulong> OpenVotes = new List<ulong>();
            private int requiredVotesToOpen = 0;

            private static void ClearVotes()
            {
                EventVotes.Clear();
                PlayerVotes.Clear();
                OpenVotes.Clear();
            }           
            public static void OpenEventVoting(bool enable)
            {
                ClearVotes();
                if (enable)
                {
                    int i = 0;
                    foreach (var autocfg in eminterface.Event_Config.AutoEvent_Config.AutoEvent_List)
                    {
                        EventVotes.Add(i, 0);
                        i++;
                    }

                    if (eminterface.EventManager._Launched && eminterface.configData.Voting.Auto_AllowEventVoting)
                    {
                        eminterface.VotingOpen = true;
                        eminterface.VoteTallyTimer = eminterface.timer.Once((eminterface.Event_Config.AutoEvent_Config.GameInterval * 60) - 30, () => eminterface.EventVotes.ProcessEventVotes());
                    }
                }
                else
                {
                    eminterface.VotingOpen = false;
                }
            }
            public void ProcessEventVotes()
            {
                eminterface.VotingOpen = false;
                int eventIndex = -1;
                int voteCount = 0;
                foreach (var vote in EventVotes)
                {
                    if (vote.Value > voteCount)
                    {
                        eventIndex = vote.Key;
                        voteCount = vote.Value;
                    }
                }
                if (eventIndex != -1)
                {
                    if (!eminterface.EventManager._Launched)
                    {
                        eminterface.EventManager._Event = eminterface.Event_Config.Event_List[eminterface.EventManager.ValidAutoEvents[eventIndex].EventConfig];
                        eminterface.EventManager.OpenEvent();
                    }
                    else
                    {
                        eminterface.EventManager._AutoEventNum = eventIndex;
                        eminterface.EventManager._NextConfigName = eminterface.EventManager.ValidAutoEvents[eventIndex].EventConfig;
                        eminterface.EventManager._ForceNextConfig = true;
                        eminterface.EventManager.BroadcastToChat(string.Format("Event votes have been tallied. The next event will be: {0}", eminterface.Event_Config.Event_List.ToList()[eventIndex].Key));
                    }
                }
                ClearVotes();
            }
            public void VoteOpenEvent(ulong playerid)
            {
                if (!OpenVotes.Contains(playerid))
                    OpenVotes.Add(playerid);
                if (OpenVotes.Count >= requiredVotesToOpen)
                {
                    eminterface.VotingOpen = true;
                    eminterface.StartEventVoteTimer();
                    OpenVotes.Clear();
                }
            }
            public void AddPlayerVote(ulong playerid, int index)
            {
                if (!PlayerVotes.ContainsKey(playerid))
                {
                    PlayerVotes.Add(playerid, index);
                    EventVotes[index]++;
                }
            }
            public void RemovePlayerVote(ulong playerid)
            {
                if (PlayerVotes.ContainsKey(playerid))
                {
                    EventVotes[PlayerVotes[playerid]]--;
                    PlayerVotes.Remove(playerid);
                }
            }
            public void SetVotesToOpen(int amount) => requiredVotesToOpen = amount;
            public object GetVotedEvent(ulong playerid)
            {
                int voted = -1;
                if (!PlayerVotes.TryGetValue(playerid, out voted))
                    return null;
                return voted;                
            }
            public int GetVoteCount(int index) => EventVotes[index];
            public int GetVoteOpenCount() => OpenVotes.Count;
            public int GetRequiredCount() => requiredVotesToOpen;
            public bool HasVotedOpen(ulong playerid) => OpenVotes.Contains(playerid);
        }
        #endregion

        #region Oxide Hooks
        void Loaded()
        {
            E_Conf = Interface.Oxide.DataFileSystem.GetFile("EventManager/EventsConfig");
            permission.RegisterPermission("eminterface.admin", this);
            lang.RegisterMessages(Messages, this);
        }
        void OnServerInitialized()
        {
            LoadVariables();
            LoadData();
            eminterface = this;
            SetUIColors();
            CollectItemDetails();
            CollectClassList();
            GetRequiredVoteLoop();
            timer.In(15, () =>
            {
                if (EventManager)
                    EventVoting.OpenEventVoting(true);
            });
        }
        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                DestroyAllUI(player);
        }
        #endregion

        #region Other
        string msg(string key, BasePlayer player = null) => lang.GetMessage(key, this, player?.UserIDString);

        [ChatCommand("event")]
        private void cmdEventMenu(BasePlayer player, string command, string[] args)
        {
            if (args != null && args.Length == 1)
            {
                switch (args[0].ToLower())
                {
                    case "join":
                        {
                            var success = EventManager.JoinEvent(player);
                            if (success is string)
                                SendReply(player, (string)success);
                        }
                        return;
                    case "leave":
                        {
                            var success = EventManager.LeaveEvent(player);
                            if (success is string)
                                SendReply(player, (string)success);
                        }
                        return;
                    case "start":
                        if (HasPerm(player))
                        {
                            var success = EventManager.StartEvent();
                            if (success is string)
                                SendReply(player, (string)success);
                        }
                        return;
                    case "end":
                        if (HasPerm(player))
                        {
                            var success = EventManager.EndEvent();
                            if (success is string)
                                SendReply(player, (string)success);
                            SendReply(player, msg("endingEvent", player));
                        }
                        return;
                    default:
                        break;
                }
            }
            OpenMenu(player);
        }
        #endregion

        #region UI
        private void OpenMenu(BasePlayer player)
        {
            CloseMap(player);
            CreateMenuMain(player);
            CreateHome(player);
        }
        public void CreateMenuMain(BasePlayer player)
        {
            var Main = EventManager.UI.CreateElementContainer(UIMain, UIColors["dark"], "0 0.92", "1 1", true);
            EventManager.UI.CreatePanel(ref Main, UIMain, UIColors["light"], "0.01 0.03", "0.99 0.97", true);
            EventManager.UI.CreateLabel(ref Main, UIMain, "", $"{Color1}{msg("Event Manager", player)}  v{EventManager.Version}</color>", 30, "0.05 0", "0.2 1");
            int i = 0;
            CreateMenuButton(ref Main, UIMain, msg("Home", player), "EMI_ChangeElement home", i); i++;
            if (configData.Voting.Auto_AllowEventVoting || configData.Voting.Standard_AllowVoteToOpen) { CreateMenuButton(ref Main, UIMain, msg("Voting", player), "EMI_ChangeElement voting", i); i++; }
            CreateMenuButton(ref Main, UIMain, msg("Statistics", player), "EMI_ChangeElement stats", i); i++;
            if (EventManager._Started && EventManager.isPlaying(player) && EventManager._Event.UseClassSelector) { CreateMenuButton(ref Main, UIMain, msg("Change Class", player), "EMI_ChangeElement selectclass", i); i++; }
            if (HasPerm(player)) { CreateMenuButton(ref Main, UIMain, msg("Admin", player), "EMI_ChangeElement admin", i); i++; }
            CreateMenuButton(ref Main, UIMain, msg("Close", player), "EMI_DestroyAll", i);

            CuiHelper.DestroyUi(player, UIMain);
            CuiHelper.AddUi(player, Main);
        }        
        private void CreateHome(BasePlayer player)
        {            
            var MainCont = EventManager.UI.CreateElementContainer(UIPanel, UIColors["dark"], "0 0", "1 0.92", false);
           
            EventManager.UI.CreatePanel(ref MainCont, UIPanel, UIColors["light"], "0.01 0.01", "0.495 0.55");            
            if (EventManager.GameStatistics.Stats.ContainsKey(player.userID))
            {
                EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Your Statistics", player), 20, "0.1 0.48", "0.4 0.54");
                var Stats = EventManager.StatsCache[player.userID];
                AddInfoEntry(ref MainCont, UIPanel, msg("Rank", player), Stats.Rank.ToString(), 0, 0.42f, 0.47f, 0.04f);
                AddInfoEntry(ref MainCont, UIPanel, msg("K/D Ratio", player), GetRatio(Stats.Kills, Stats.Deaths), 1, 0.42f, 0.47f, 0.04f);
                AddInfoEntry(ref MainCont, UIPanel, msg("Games Played", player), Stats.GamesPlayed.ToString(), 2, 0.42f, 0.47f, 0.04f);
                AddInfoEntry(ref MainCont, UIPanel, msg("Games Won", player), Stats.GamesWon.ToString(), 3, 0.42f, 0.47f, 0.04f);
                AddInfoEntry(ref MainCont, UIPanel, msg("Games Lost", player), Stats.GamesLost.ToString(), 4, 0.42f, 0.47f, 0.04f);
                AddInfoEntry(ref MainCont, UIPanel, msg("Kills", player), Stats.Kills.ToString(), 5, 0.42f, 0.47f, 0.04f);
                AddInfoEntry(ref MainCont, UIPanel, msg("Deaths", player), Stats.Deaths.ToString(), 6, 0.42f, 0.47f, 0.04f);
                AddInfoEntry(ref MainCont, UIPanel, msg("Shots Fired", player), Stats.ShotsFired.ToString(), 7, 0.42f, 0.47f, 0.04f);
                AddInfoEntry(ref MainCont, UIPanel, msg("Helicopters Killed", player), Stats.ChoppersKilled.ToString(), 8, 0.42f, 0.47f, 0.04f);
                AddInfoEntry(ref MainCont, UIPanel, msg("Flags Captured", player), Stats.FlagsCaptured.ToString(), 9, 0.42f, 0.47f, 0.04f);
            }
            else EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("You do not have any saved data", player), 16, "0.1 0.2", "0.4 0.8");
            
            EventManager.UI.CreatePanel(ref MainCont, UIPanel, UIColors["light"], "0.01 0.56", "0.495 0.99");
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Event Status:", player), 18, "0.05 0.87", "0.25 0.93", TextAnchor.MiddleLeft);
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", GetEventStatus(), 18, "0.25 0.87", "0.5 0.93", TextAnchor.MiddleLeft);
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Autoevent Status:", player), 18, "0.05 0.81", "0.25 0.87", TextAnchor.MiddleLeft);
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", GetAutoEventStatus(), 18, "0.25 0.81", "0.5 0.87", TextAnchor.MiddleLeft);
            
            EventManager.UI.CreatePanel(ref MainCont, UIPanel, UIColors["light"], "0.5 0.01", "0.99 0.99");
            if (EventManager._Open || EventManager._Started)
            {                
                GetEventInfo(ref MainCont, UIPanel);
                EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Current Game Scores", player), 18, "0.55 0.87", "0.95 0.93");
                GetGameScores(ref MainCont, UIPanel, 0.6f, 0.82f);
                if (EventManager._Open)
                {
                    if (EventManager.isPlaying(player))
                        EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonbg"], msg("Leave Event", player), 18, "0.38 0.87", "0.48 0.92", "EMI_LeaveEvent");
                    else if (EventManager._Open) EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonbg"], msg("Join Event", player), 18, "0.38 0.87", "0.48 0.92", "EMI_JoinEvent");
                }
            }
            else if (!EventManager._Open && !EventManager._Started)
            {
                if (EventManager.GameScores.Scores != null && EventManager.GameScores.Scores.Count > 0)
                {
                    EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Previous Game Scores", player), 18, "0.55 0.87", "0.95 0.93", TextAnchor.UpperCenter);
                    GetGameScores(ref MainCont, UIPanel, 0.6f, 0.82f);
                }
            }
            CuiHelper.DestroyUi(player, UIPanel);
            CuiHelper.AddUi(player, MainCont);
        }

        private void CreateVoting(BasePlayer player, int page = 0)
        {
            var MainCont = EventManager.UI.CreateElementContainer(UIPanel, UIColors["dark"], "0 0", "1 0.92", false);
            EventManager.UI.CreatePanel(ref MainCont, UIPanel, UIColors["light"], "0.01 0.01", "0.99 0.99", true);
            if (((configData.Voting.Standard_AllowVoteToOpen && !EventManager._Launched) || (configData.Voting.Auto_AllowEventVoting && EventManager._Launched)) && VotingOpen && !EventManager._Open && !EventManager._Started)
            {
                EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", $"Vote for the next event to play!", 18, "0.2 0.92", "0.8 0.98");
                if (Event_Config.AutoEvent_Config.AutoEvent_List.Count > 9)
                {
                    var maxpages = (Event_Config.AutoEvent_Config.AutoEvent_List.Count - 1) / 9 + 1;
                    if (page < maxpages - 1)
                        EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonbg"], "Next", 18, "0.84 0.925", "0.97 0.98", $"EMI_ChangePage vote {page + 1}");
                    if (page > 0)
                        EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonbg"], "Back", 18, "0.03 0.925", "0.16 0.98", $"EMI_ChangePage vote {page - 1}");
                }
                int maxentries = (9 * (page + 1));
                if (maxentries > Event_Config.AutoEvent_Config.AutoEvent_List.Count)
                    maxentries = Event_Config.AutoEvent_Config.AutoEvent_List.Count;
                int eventcount = 9 * page;

                if (Event_Config.AutoEvent_Config.AutoEvent_List.Count == 0)
                    EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", $"There are no saved auto-events", 18, "0.2 0.92", "0.8 0.98");

                CuiHelper.DestroyUi(player, UIPanel);
                CuiHelper.AddUi(player, MainCont);


                int votedEvent = -1;
                var hasVoted = EventVotes.GetVotedEvent(player.userID);
                if (hasVoted != null)
                    votedEvent = (int)hasVoted;

                int i = 0;
                for (int n = eventcount; n < maxentries; n++)
                {
                    var voteCount = EventVotes.GetVoteCount(n);
                    if (n == votedEvent)
                        CreateEventEntry(player, $"Event {n}", Event_Config.Event_List[Event_Config.AutoEvent_Config.AutoEvent_List[n].EventConfig], $"EMI_EventVote unvote", $"{Color1}Unvote</color>", i, 0.32f);
                    else if (votedEvent > -1) CreateEventEntry(player, $"Event {n}", Event_Config.Event_List[Event_Config.AutoEvent_Config.AutoEvent_List[n].EventConfig], $"", $"", i, 0.32f);
                    else CreateEventEntry(player, $"Event {n}", Event_Config.Event_List[Event_Config.AutoEvent_Config.AutoEvent_List[n].EventConfig], $"EMI_EventVote vote {n}", $"{Color1}Vote</color>   ({voteCount})", i, 0.32f);
                    i++;
                }
            }
            else if (!configData.Voting.Standard_AllowVoteToOpen)
            {
                EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Voting requires auto events to be launched", player), 18, "0.2 0.92", "0.8 0.98");
                CuiHelper.DestroyUi(player, UIPanel);
                CuiHelper.AddUi(player, MainCont);
            }
            else if (configData.Voting.Standard_AllowVoteToOpen && !VotingOpen && !EventManager._Launched && !EventManager._Open && !EventManager._Started)
            {
                EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Vote to open a new event", player), 18, "0.2 0.92", "0.8 0.98");
                EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", string.Format(msg("Total Votes : {0}{1}</color>", player), Color1, EventVotes.GetVoteOpenCount()), 18, "0.2 0.75", "0.8 0.8");

                EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", string.Format(msg("Required Votes : {0}{1}</color>", player), Color1, EventVotes.GetRequiredCount()), 18, "0.2 0.7", "0.8 0.75");

                if (EventVotes.HasVotedOpen(player.userID))
                    EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonbg"], msg("Voted", player), 18, "0.45 0.4", "0.55 0.45", "");
                else EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonbg"], msg("Vote", player), 18, "0.45 0.4", "0.55 0.45", "EMI_EventVote open");

                CuiHelper.DestroyUi(player, UIPanel);
                CuiHelper.AddUi(player, MainCont);
            }
            else
            {
                EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", "Voting is currently disabled", 18, "0.2 0.92", "0.8 0.98");
                CuiHelper.DestroyUi(player, UIPanel);
                CuiHelper.AddUi(player, MainCont);
            }
        }
        private void CreateStatistics(BasePlayer player)
        {            
            var MainCont = EventManager.UI.CreateElementContainer(UIPanel, UIColors["dark"], "0 0", "1 0.92", true);            

            EventManager.UI.CreatePanel(ref MainCont, UIPanel, UIColors["light"], "0.01 0.01", "0.495 0.99");
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Global Statistics", player), 20, "0.1 0.92", "0.4 0.98");
            int i = 0;
            AddInfoEntry(ref MainCont, UIPanel, msg("Total Games Played", player), EventManager.GameStatistics.GetTotalGamesPlayed(), i, 0.85f, 0.9f, 0.05f); i++;
            AddInfoEntry(ref MainCont, UIPanel, msg("Total Player Kills", player), EventManager.GameStatistics.GetTotalKills(), i, 0.85f, 0.9f, 0.05f); i++;
            AddInfoEntry(ref MainCont, UIPanel, msg("Total Player Deaths", player), EventManager.GameStatistics.GetTotalDeaths(), i, 0.85f, 0.9f, 0.05f); i++;
            AddInfoEntry(ref MainCont, UIPanel, msg("Total Event Players", player), EventManager.GameStatistics.GetTotalPlayers(), i, 0.85f, 0.9f, 0.05f); i++;
            AddInfoEntry(ref MainCont, UIPanel, msg("Total Shots Fired", player), EventManager.GameStatistics.GetTotalShotsFired(), i, 0.85f, 0.9f, 0.05f); i++;
            if (EventManager.EventGames.ContainsKey("CaptureTheFlag"))
            {
                AddInfoEntry(ref MainCont, UIPanel, msg("Total Flags Captured", player), EventManager.GameStatistics.GetFlagsCaptured(), i, 0.85f, 0.9f, 0.05f); i++;
            }
            if (EventManager.EventGames.ContainsKey("ChopperSurvival"))
            {
                AddInfoEntry(ref MainCont, UIPanel, msg("Total Helicopters Killed", player), EventManager.GameStatistics.GetChoppersKilled(), i, 0.85f, 0.9f, 0.05f); i++;
            }

            i++;
            foreach (var entry in EventManager.GameStatistics.GamesPlayed.OrderByDescending(x => x.Value))
            {
                AddInfoEntry(ref MainCont, UIPanel, entry.Key, entry.Value.ToString(), i, 0.85f, 0.9f, 0.05f);
                i++;
            }

            EventManager.UI.CreatePanel(ref MainCont, UIPanel, UIColors["light"], "0.5 0.01", "0.99 0.99");
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Leader Board", player), 20, "0.6 0.92", "0.9 0.98");
            GetLeaderBoard(ref MainCont, UIPanel);

            CuiHelper.DestroyUi(player, UIPanel);
            CuiHelper.AddUi(player, MainCont);
        }

        private void CreateAdminMenu(BasePlayer player)
        {            
            var MenuElement = EventManager.UI.CreateElementContainer(UIAdmin, UIColors["dark"], "0.88 0", "1 0.92", true);
            EventManager.UI.CreatePanel(ref MenuElement, UIAdmin, UIColors["light"], "0.01 0.01", "0.91 0.99", true);

            int i = 0;
            CreateAdminButton(ref MenuElement, UIAdmin, "Control", "EMI_ChangeElement control", i); i++;
            CreateAdminButton(ref MenuElement, UIAdmin, "Kick", "EMI_ChangeElement kick", i); i++;
            CreateAdminButton(ref MenuElement, UIAdmin, "Join", "EMI_ChangeElement join", i); i++; i++;
            CreateAdminButton(ref MenuElement, UIAdmin, "Classes", "EMI_ChangeElement class", i); i++; i++;

            CreateAdminButton(ref MenuElement, UIAdmin, "Events", "EMI_ChangeElement events", i); i++;
            CreateAdminButton(ref MenuElement, UIAdmin, "Create", "EMI_ChangeElement createevent", i); i++;i++;

            CreateAdminButton(ref MenuElement, UIAdmin, "Auto Events", "EMI_ChangeElement auto", i); i++; 

            CuiHelper.DestroyUi(player, UIAdmin);
            CuiHelper.AddUi(player, MenuElement);
            EventControl(player);
        }
        private void EventControl(BasePlayer player)
        {
            var MainCont = EventManager.UI.CreateElementContainer(UIPanel, UIColors["dark"], "0 0", "0.88 0.92");
            EventManager.UI.CreatePanel(ref MainCont, UIPanel, UIColors["light"], "0.011 0.01", "0.99 0.99", true);
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("ControlHelp", player), 18, "0.2 0.92", "0.8 0.98");

            int i = 1;
            int p = 0;
            AddInfoEntry(ref MainCont, UIPanel, msg("Event Status", player), GetEventStatus(), i); i++;

            if (!string.IsNullOrEmpty(EventManager._Event.EventType))
            {
                if (!EventManager._Open) { CreateControlButton(ref MainCont, UIPanel, msg("Open", player), "EMI_Control open", 0.83f, p); p++; }
                if (EventManager._Open) { CreateControlButton(ref MainCont, UIPanel, msg("Close", player), "EMI_Control close", 0.83f, p); p++; }
                if (EventManager._Open && !EventManager._Started) { CreateControlButton(ref MainCont, UIPanel, msg("Start", player), "EMI_Control start", 0.83f, p); p++; }
                if (EventManager._Started) { CreateControlButton(ref MainCont, UIPanel, msg("End", player), "EMI_Control end", 0.83f, p); p++; }
            }
            else EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Select a event config or event type to proceed", player), 18, "0.5 0.82", "0.9 0.88", TextAnchor.MiddleLeft);

            AddInfoEntry(ref MainCont, UIPanel, msg("Event Config", player), EventManager._CurrentEventConfig, i); 
            CreateControlButton(ref MainCont, UIPanel, msg("Change", player), "EMI_Control config", 0.88f - (0.05f * i), 0); i++;
            AddInfoEntry(ref MainCont, UIPanel, msg("Event Type", player), EventManager._Event.EventType, i);
            CreateControlButton(ref MainCont, UIPanel, msg("Change", player), "EMI_Control type", 0.88f - (0.05f * i), 0); i++;

            if (!string.IsNullOrEmpty(EventManager._Event.EventType))
            {
                if (EventManager.EventGames[EventManager._Event.EventType].CanPlayBattlefield)
                {
                    AddInfoEntry(ref MainCont, UIPanel, msg("Gamemode", player), EventManager._Event.GameMode.ToString(), i);
                    CreateControlButton(ref MainCont, UIPanel, msg("Normal", player), $"EMI_Control gamemode normal", 0.88f - (0.05f * i), 0);
                    CreateControlButton(ref MainCont, UIPanel, msg("Battlefield", player), $"EMI_Control gamemode battlefield", 0.88f - (0.05f * i), 1); i++;
                }

                if (!string.IsNullOrEmpty(EventManager.EventGames[EventManager._Event.EventType].ScoreType) && EventManager._Event.GameMode != EventManager.GameMode.Battlefield)
                {
                    AddInfoEntry(ref MainCont, UIPanel, $"{msg("Score Limit", player)} ({EventManager.EventGames[EventManager._Event.EventType].ScoreType})", EventManager._Event.ScoreLimit.ToString(), i);
                    CreateControlButton(ref MainCont, UIPanel, "+", $"EMI_Control scorelimit {EventManager._Event.ScoreLimit + 1}", 0.88f - (0.05f * i), 0);
                    CreateControlButton(ref MainCont, UIPanel, "-", $"EMI_Control scorelimit {EventManager._Event.ScoreLimit - 1}", 0.88f - (0.05f * i), 1); i++;
                }

                AddInfoEntry(ref MainCont, UIPanel, msg("Max Players", player), EventManager._Event.MaximumPlayers.ToString(), i);
                CreateControlButton(ref MainCont, UIPanel, "+", $"EMI_Control maxplayers {EventManager._Event.MaximumPlayers + 1}", 0.88f - (0.05f * i), 0);
                CreateControlButton(ref MainCont, UIPanel, "-", $"EMI_Control maxplayers {EventManager._Event.MaximumPlayers - 1}", 0.88f - (0.05f * i), 1); i++;

                if (EventManager._Event.EventType == "GunGame")
                {
                    AddInfoEntry(ref MainCont, UIPanel, msg("Weapon Set", player), EventManager._Event.WeaponSet, i);
                    CreateControlButton(ref MainCont, UIPanel, msg("Change", player), "EMI_Control ggconfig", 0.88f - (0.05f * i), 0); i++;
                }
                else if (EventManager.EventGames[EventManager._Event.EventType].RequiresKit && !EventManager._Event.UseClassSelector)
                {
                    AddInfoEntry(ref MainCont, UIPanel, msg("Kit", player), EventManager._Event.Kit, i);
                    CreateControlButton(ref MainCont, UIPanel, msg("Change", player), "EMI_Control kit", 0.88f - (0.05f * i), 0); i++;
                }
                if (EventManager.EventGames[EventManager._Event.EventType].RequiresSpawns)
                {
                    AddInfoEntry(ref MainCont, UIPanel, msg("Spawnfile", player), EventManager._Event.Spawnfile, i);
                    CreateControlButton(ref MainCont, UIPanel, msg("Change", player), "EMI_Control spawns", 0.88f - (0.05f * i), 0); i++;

                    if (EventManager.EventGames[EventManager._Event.EventType].RequiresMultipleSpawns)
                    {
                        AddInfoEntry(ref MainCont, UIPanel, msg("Second Spawnfile", player), EventManager._Event.Spawnfile2, i);
                        CreateControlButton(ref MainCont, UIPanel, msg("Change", player), "EMI_Control spawns2", 0.88f - (0.05f * i), 0); i++;
                    }
                    AddInfoEntry(ref MainCont, UIPanel, msg("Spawn Type", player), EventManager._Event.SpawnType.ToString(), i);
                    CreateControlButton(ref MainCont, UIPanel, msg("Consecutive", player), "EMI_Control spawntype seq", 0.88f - (0.05f * i), 0);
                    CreateControlButton(ref MainCont, UIPanel, msg("Random", player), "EMI_Control spawntype rand", 0.88f - (0.05f * i), 1); i++;
                }                         

                AddInfoEntry(ref MainCont, UIPanel, msg("Zone ID", player), EventManager._Event.ZoneID, i);
                CreateControlButton(ref MainCont, UIPanel, msg("Change", player), "EMI_Control zone", 0.88f - (0.05f * i), 0); i++;

                if (EventManager.EventGames[EventManager._Event.EventType].SpawnsEnemies)
                {
                    AddInfoEntry(ref MainCont, UIPanel, msg("Enemies to spawn", player), EventManager._Event.EnemiesToSpawn.ToString(), i);
                    CreateControlButton(ref MainCont, UIPanel, "+", $"EMI_Control enemycount {EventManager._Event.EnemiesToSpawn + 1}", 0.88f - (0.05f * i), 0);
                    CreateControlButton(ref MainCont, UIPanel, "-", $"EMI_Control enemycount {EventManager._Event.EnemiesToSpawn - 1}", 0.88f - (0.05f * i), 1); i++;
                }
                if (EventManager.EventGames[EventManager._Event.EventType].IsRoundBased)
                {
                    AddInfoEntry(ref MainCont, UIPanel, msg("Rounds to play", player), EventManager._Event.GameRounds.ToString(), i);
                    CreateControlButton(ref MainCont, UIPanel, "+", $"EMI_Control roundcount {EventManager._Event.GameRounds + 1}", 0.88f - (0.05f * i), 0);
                    CreateControlButton(ref MainCont, UIPanel, "-", $"EMI_Control roundcount {EventManager._Event.GameRounds - 1}", 0.88f - (0.05f * i), 1); i++;
                }

                if (EventManager.EventGames[EventManager._Event.EventType].CanUseClassSelector)
                {
                    AddInfoEntry(ref MainCont, UIPanel, msg("Class Selector", player), EventManager._Event.UseClassSelector.ToString(), i);
                    if (!EventManager._Event.UseClassSelector) CreateControlButton(ref MainCont, UIPanel, msg("Enable", player), "EMI_Control classtoggle", 0.88f - (0.05f * i), 0);
                    else CreateControlButton(ref MainCont, UIPanel, msg("Disable", player), "EMI_Control classtoggle", 0.88f - (0.05f * i), 0); i++;
                }

                AddInfoEntry(ref MainCont, UIPanel, msg("Disable Item Pickup", player), EventManager._Event.DisableItemPickup.ToString(), i);
                if (!EventManager._Event.DisableItemPickup) CreateControlButton(ref MainCont, UIPanel, msg("Enable", player), "EMI_Control pickuptoggle", 0.88f - (0.05f * i), 0);
                else CreateControlButton(ref MainCont, UIPanel, msg("Disable", player), "EMI_Control pickuptoggle", 0.88f - (0.05f * i), 0); i++;

                if (!EventManager.EventGames[EventManager._Event.EventType].ForceCloseOnStart)
                {
                    AddInfoEntry(ref MainCont, UIPanel, msg("Close On Start", player), EventManager._Event.CloseOnStart.ToString(), i);
                    if (!EventManager._Event.CloseOnStart) CreateControlButton(ref MainCont, UIPanel, msg("Enable", player), "EMI_Control costoggle", 0.88f - (0.05f * i), 0);
                    else CreateControlButton(ref MainCont, UIPanel, msg("Disable", player), "EMI_Control costoggle", 0.88f - (0.05f * i), 0); i++; i++;
                }
                if (EventManager.EventGames[EventManager._Event.EventType].CanChooseRespawn)
                {
                    AddInfoEntry(ref MainCont, UIPanel, msg("Respawn Type", player), EventManager._Event.RespawnType.ToString(), i);
                    CreateControlButton(ref MainCont, UIPanel, msg("None", player), "EMI_Control respawn none", 0.88f - (0.05f * i), 0); 
                    CreateControlButton(ref MainCont, UIPanel, msg("Timer", player), "EMI_Control respawn timer", 0.88f - (0.05f * i), 1); 
                    CreateControlButton(ref MainCont, UIPanel, msg("Waves", player), "EMI_Control respawn wave", 0.88f - (0.05f * i), 2); i++;

                    if (EventManager._Event.RespawnType != EventManager.RespawnType.None)
                    {
                        AddInfoEntry(ref MainCont, UIPanel, msg("Respawn Timer (seconds)", player), EventManager._Event.RespawnTimer.ToString(), i);
                        CreateControlButton(ref MainCont, UIPanel, "+", $"EMI_Control respawntime {EventManager._Event.RespawnTimer + 1}", 0.88f - (0.05f * i), 0);
                        CreateControlButton(ref MainCont, UIPanel, "-", $"EMI_Control respawntime {EventManager._Event.RespawnTimer - 1}", 0.88f - (0.05f * i), 1); i++;
                    }
                }
            }        

            CuiHelper.DestroyUi(player, UIPanel);
            CuiHelper.AddUi(player, MainCont);
        }                
        private void EventClasses(BasePlayer player)
        {            
            var MainCont = EventManager.UI.CreateElementContainer(UIPanel, UIColors["dark"], "0 0", "0.88 0.92");
            EventManager.UI.CreatePanel(ref MainCont, UIPanel, UIColors["light"], "0.011 0.01", "0.99 0.99", true);
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Add or remove kits from the class selector", player), 18, "0.2 0.92", "0.8 0.98");
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Available Kits", player), 18, "0.05 0.86", "0.76 0.92");
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Available Classes", player), 18, "0.8 0.86", "0.95 0.92");
            EventManager.UI.CreatePanel(ref MainCont, UIPanel, UIColors["medium"], "0.05 0.05", "0.76 0.85", true);
            EventManager.UI.CreatePanel(ref MainCont, UIPanel, UIColors["medium"], "0.8 0.05", "0.95 0.85", true);
            var classList = Event_Config.Classes;
            var kitList = GetKits();
            var Kits = GetKits();
            if (Kits != null && Kits is string[])
            {
                int i = 0;                
                foreach (var kit in (string[])Kits)
                {
                    if (classList.Contains(kit)) continue;
                    CreateClassEditorButton(ref MainCont, UIPanel, kit, i, true);
                    i++;
                }
                if (i == 0) EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Unable to find any kits", player), 18, "0.1 0.5", "0.45 0.6");
            }
            if (classList != null)
            {
                int i = 0;
                foreach (var entry in classList)
                {                    
                    CreateClassEditorButton(ref MainCont, UIPanel, entry, i, false);
                    i++;
                }
                if (i == 0) EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("No classes have been added", player), 18, "0.6 0.5", "0.9 0.6");
            } 
            CuiHelper.DestroyUi(player, UIPanel);
            CuiHelper.AddUi(player, MainCont);
        }
        private void EventAutoEvent(BasePlayer player, int page = 0)
        {
            if (!AutoCreators.ContainsKey(player.userID))
                AutoCreators.Add(player.userID, new AEConfig());
            var autocfg = AutoCreators[player.userID];

            var MainCont = EventManager.UI.CreateElementContainer(UIPanel, UIColors["dark"], "0 0", "0.88 0.92");
            EventManager.UI.CreatePanel(ref MainCont, UIPanel, UIColors["light"], "0.011 0.01", "0.99 0.99", true);
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Manage the auto event roster and settings", player), 18, "0.2 0.92", "0.8 0.98");
            
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Auto-Event Settings", player), 18, "0.05 0.86", "0.45 0.92");
            int info = 2;
            AddInfoAuto(ref MainCont, UIPanel, msg("Auto-Event Status", player), GetAutoEventStatus(), info);
            if (!EventManager._Launched) CreateControlButtonSmall(ref MainCont, UIPanel, msg("Enable", player), "EMI_Control aeenable", 0.88f - (0.06f * info), 0);
            else CreateControlButtonSmall(ref MainCont, UIPanel, msg("Disable", player), "EMI_Control aedisable", 0.88f - (0.06f * info), 0); info++;

            AddInfoAuto(ref MainCont, UIPanel, msg("Next Event", player), EventManager._NextConfigName, info);
            CreateControlButtonSmall(ref MainCont, UIPanel, msg("Change", player), "EMI_Control nextevent", 0.88f - (0.06f * info), 0); info++;

            AddInfoAuto(ref MainCont, UIPanel, msg("Auto Cancel", player), Event_Config.AutoEvent_Config.AutoCancel.ToString(), info); 
            if (Event_Config.AutoEvent_Config.AutoCancel) CreateControlButtonSmall(ref MainCont, UIPanel, msg("Disable", player), "EMI_AutoEditor cancel 1", 0.88f - (0.06f * info), 0);
            else CreateControlButtonSmall(ref MainCont, UIPanel, msg("Enable", player), "EMI_AutoEditor cancel 1", 0.88f - (0.06f * info), 0); info++;

            AddInfoAuto(ref MainCont, UIPanel, msg("Game Interval (minutes)", player), Event_Config.AutoEvent_Config.GameInterval.ToString(), info);
            CreateControlButtonSmall(ref MainCont, UIPanel, msg("Increase", player), $"EMI_AutoEditor gameinterval {Event_Config.AutoEvent_Config.GameInterval + 1}", 0.88f - (0.06f * info), 0);
            CreateControlButtonSmall(ref MainCont, UIPanel, msg("Decrease", player), $"EMI_AutoEditor gameinterval {Event_Config.AutoEvent_Config.GameInterval - 1}", 0.88f - (0.06f * info), 1); info++;

            AddInfoAuto(ref MainCont, UIPanel, msg("Auto Cancel Timer (minutes)", player), Event_Config.AutoEvent_Config.AutoCancel_Timer.ToString(), info);
            CreateControlButtonSmall(ref MainCont, UIPanel, msg("Increase", player), $"EMI_AutoEditor canceltimer {Event_Config.AutoEvent_Config.AutoCancel_Timer + 1}", 0.88f - (0.06f * info), 0);
            CreateControlButtonSmall(ref MainCont, UIPanel, msg("Decrease", player), $"EMI_AutoEditor canceltimer {Event_Config.AutoEvent_Config.AutoCancel_Timer - 1}", 0.88f - (0.06f * info), 1); info++;

            AddInfoAuto(ref MainCont, UIPanel, msg("Randomize List", player), EventManager._RandomizeAuto.ToString(), info);
            if (EventManager._RandomizeAuto) CreateControlButtonSmall(ref MainCont, UIPanel, msg("Disable", player), "EMI_AutoEditor randomize 1", 0.88f - (0.06f * info), 0);
            else CreateControlButtonSmall(ref MainCont, UIPanel, msg("Enable", player), "EMI_AutoEditor randomize 1", 0.88f - (0.06f * info), 0); info++;

            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Auto-Event Creator", player), 18, "0.05 0.36", "0.45 0.42");

            info = 10;
            AddInfoAuto(ref MainCont, UIPanel, msg("Event Config", player), autocfg.EventConfig, info);
            CreateControlButtonSmall(ref MainCont, UIPanel, msg("Change", player), "EMI_NewAutoConfig newconf", 0.88f - (0.06f * info), 0); info++;

            AddInfoAuto(ref MainCont, UIPanel, msg("Join Timer (seconds)", player), autocfg.TimeToJoin.ToString(), info);
            CreateControlButtonSmall(ref MainCont, UIPanel, msg("Increase", player), $"EMI_NewAutoConfig jointimer {autocfg.TimeToJoin + 1}", 0.88f - (0.06f * info), 0);
            CreateControlButtonSmall(ref MainCont, UIPanel, msg("Decrease", player), $"EMI_NewAutoConfig jointimer {autocfg.TimeToJoin - 1}", 0.88f - (0.06f * info), 1); info++;

            AddInfoAuto(ref MainCont, UIPanel, msg("Start Timer (seconds)", player), autocfg.TimeToStart.ToString(), info);
            CreateControlButtonSmall(ref MainCont, UIPanel, msg("Increase", player), $"EMI_NewAutoConfig starttimer {autocfg.TimeToStart + 1}", 0.88f - (0.06f * info), 0);
            CreateControlButtonSmall(ref MainCont, UIPanel, msg("Decrease", player), $"EMI_NewAutoConfig starttimer {autocfg.TimeToStart - 1}", 0.88f - (0.06f * info), 1); info++;

            AddInfoAuto(ref MainCont, UIPanel, msg("Time Limit (minutes)", player), autocfg.TimeLimit.ToString(), info);
            CreateControlButtonSmall(ref MainCont, UIPanel, msg("Increase", player), $"EMI_NewAutoConfig timelimit {autocfg.TimeLimit + 1}", 0.88f - (0.06f * info), 0);
            CreateControlButtonSmall(ref MainCont, UIPanel, msg("Decrease", player), $"EMI_NewAutoConfig timelimit {autocfg.TimeLimit - 1}", 0.88f - (0.06f * info), 1); info++;

            CreateControlButtonSmall(ref MainCont, UIPanel, msg("Save Config", player), $"EMI_NewAutoConfig saveconfig {autocfg.TimeLimit + 1}", 0.88f - (0.06f * info), 0);
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Auto-Event Roster", player), 18, "0.45 0.86", "0.98 0.92");            
            EventManager.UI.CreatePanel(ref MainCont, UIPanel, UIColors["medium"], "0.45 0.075", "0.98 0.85", true);


            var autoList = Event_Config.AutoEvent_Config.AutoEvent_List;            
            if (autoList != null)
            {
                if (autoList.Count > 12)
                {
                    var maxpages = (autoList.Count - 1) / 12 + 1;
                    if (page < maxpages - 1)
                        EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonbg"], msg("Next", player), 18, "0.84 0.015", "0.97 0.07", $"EMI_ChangePage autoevent {page + 1}");
                    if (page > 0)
                        EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonbg"], msg("Back", player), 18, "0.46 0.015", "0.59 0.07", $"EMI_ChangePage autoevent {page - 1}");
                }
                int maxentries = (12 * (page + 1));
                if (maxentries > autoList.Count)
                    maxentries = autoList.Count;
                int autocount = 12 * page;

                int i = 0;
                for (int n = autocount; n < maxentries; n++)
                {
                    CreateAutoEventEditorButton(ref MainCont, UIPanel, autoList[n], i, n);
                    i++;
                }
                if (i == 0) EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("No auto events have been added", player), 18, "0.5 0.5", "0.9 0.6");
            }

            CuiHelper.DestroyUi(player, UIPanel);
            CuiHelper.AddUi(player, MainCont);
        }
        private void EventCreator(BasePlayer player, int page = 0)
        {
            var MainCont = EventManager.UI.CreateElementContainer(UIPanel, UIColors["dark"], "0 0", "0.88 0.92");
            EventManager.UI.CreatePanel(ref MainCont, UIPanel, UIColors["light"], "0.011 0.01", "0.99 0.99", true);

            var infoMessage = msg("CreateHelp", player);

            int i = 1;
            var newEvent = EventCreators[player.userID];      
                        
            AddInfoEntry(ref MainCont, UIPanel, msg("Event Type", player), newEvent.EventType, i);
            CreateControlButton(ref MainCont, UIPanel, msg("Change", player), "EMI_Creator type", 0.88f - (0.05f * i), 0); i++;

            if (!string.IsNullOrEmpty(newEvent.EventType))
            {
                infoMessage = msg("CreateHelp2", player);
                if (newEvent.EventType == "GunGame")
                {
                    AddInfoEntry(ref MainCont, UIPanel, msg("Weapon Set", player), newEvent.WeaponSet, i);
                    CreateControlButton(ref MainCont, UIPanel, msg("Change", player), "EMI_Creator ggconfig", 0.88f - (0.05f * i), 0); i++;
                }
                else if (EventManager.EventGames[newEvent.EventType].RequiresKit && !newEvent.UseClassSelector)
                {
                    AddInfoEntry(ref MainCont, UIPanel, msg("Kit", player), newEvent.Kit, i);
                    CreateControlButton(ref MainCont, UIPanel, msg("Change", player), "EMI_Creator kit", 0.88f - (0.05f * i), 0); i++;
                }
                if (EventManager.EventGames[newEvent.EventType].RequiresSpawns)
                {
                    AddInfoEntry(ref MainCont, UIPanel, msg("Spawnfile", player), newEvent.Spawnfile, i);
                    CreateControlButton(ref MainCont, UIPanel, msg("Change", player), "EMI_Creator spawns", 0.88f - (0.05f * i), 0); i++;

                    if (EventManager.EventGames[newEvent.EventType].RequiresMultipleSpawns)
                    {
                        AddInfoEntry(ref MainCont, UIPanel, msg("Second Spawnfile", player), newEvent.Spawnfile2, i);
                        CreateControlButton(ref MainCont, UIPanel, msg("Change", player), "EMI_Creator spawns2", 0.88f - (0.05f * i), 0); i++;
                    }

                    AddInfoEntry(ref MainCont, UIPanel, msg("Spawn Type", player), newEvent.SpawnType.ToString(), i);
                    CreateControlButton(ref MainCont, UIPanel, msg("Consecutive", player), "EMI_Creator spawntype seq", 0.88f - (0.05f * i), 0);
                    CreateControlButton(ref MainCont, UIPanel, msg("Random", player), "EMI_Creator spawntype rand", 0.88f - (0.05f * i), 1); i++;
                }
                

                AddInfoEntry(ref MainCont, UIPanel, msg("Zone ID", player), newEvent.ZoneID, i);
                CreateControlButton(ref MainCont, UIPanel, msg("Change", player), "EMI_Creator zone", 0.88f - (0.05f * i), 0); i++;

                if (EventManager.EventGames[newEvent.EventType].CanPlayBattlefield)
                {
                    AddInfoEntry(ref MainCont, UIPanel, msg("Gamemode", player), newEvent.GameMode.ToString(), i);
                    CreateControlButton(ref MainCont, UIPanel, msg("Normal", player), $"EMI_Creator gamemode normal", 0.88f - (0.05f * i), 0);
                    CreateControlButton(ref MainCont, UIPanel, msg("Battlefield", player), $"EMI_Creator gamemode battlefield", 0.88f - (0.05f * i), 1); i++;
                }

                if (!string.IsNullOrEmpty(EventManager.EventGames[newEvent.EventType].ScoreType) && newEvent.GameMode != EventManager.GameMode.Battlefield)
                {
                    AddInfoEntry(ref MainCont, UIPanel, $"{msg("Score Limit", player)} ({EventManager.EventGames[newEvent.EventType].ScoreType})", newEvent.ScoreLimit.ToString(), i);
                    CreateControlButton(ref MainCont, UIPanel, msg("Increase", player), $"EMI_Creator scorelimit {newEvent.ScoreLimit + 1}", 0.88f - (0.05f * i), 0);
                    CreateControlButton(ref MainCont, UIPanel, msg("Decrease", player), $"EMI_Creator scorelimit {newEvent.ScoreLimit - 1}", 0.88f - (0.05f * i), 1); i++;
                }

                AddInfoEntry(ref MainCont, UIPanel, msg("Min Players", player), newEvent.MinimumPlayers.ToString(), i);
                CreateControlButton(ref MainCont, UIPanel, msg("Increase", player), $"EMI_Creator min {newEvent.MinimumPlayers + 1}", 0.88f - (0.05f * i), 0);
                CreateControlButton(ref MainCont, UIPanel, msg("Decrease", player), $"EMI_Creator min {newEvent.MinimumPlayers - 1}", 0.88f - (0.05f * i), 1); i++;

                AddInfoEntry(ref MainCont, UIPanel, msg("Max Players", player), newEvent.MaximumPlayers.ToString(), i);
                CreateControlButton(ref MainCont, UIPanel, msg("Increase", player), $"EMI_Creator max {newEvent.MaximumPlayers + 1}", 0.88f - (0.05f * i), 0);
                CreateControlButton(ref MainCont, UIPanel, msg("Decrease", player), $"EMI_Creator max {newEvent.MaximumPlayers - 1}", 0.88f - (0.05f * i), 1); i++;

                if (EventManager.EventGames[newEvent.EventType].SpawnsEnemies)
                {
                    AddInfoEntry(ref MainCont, UIPanel, msg("Enemies to spawn", player), newEvent.EnemiesToSpawn.ToString(), i);
                    CreateControlButton(ref MainCont, UIPanel, "+", $"EMI_Creator enemycount {newEvent.EnemiesToSpawn + 1}", 0.88f - (0.05f * i), 0);
                    CreateControlButton(ref MainCont, UIPanel, "-", $"EMI_Creator enemycount {newEvent.EnemiesToSpawn - 1}", 0.88f - (0.05f * i), 1); i++;
                }
                if (EventManager.EventGames[newEvent.EventType].IsRoundBased)
                {
                    AddInfoEntry(ref MainCont, UIPanel, msg("Rounds to play", player), newEvent.GameRounds.ToString(), i);
                    CreateControlButton(ref MainCont, UIPanel, "+", $"EMI_Creator roundcount {newEvent.GameRounds + 1}", 0.88f - (0.05f * i), 0);
                    CreateControlButton(ref MainCont, UIPanel, "-", $"EMI_Creator roundcount {newEvent.GameRounds - 1}", 0.88f - (0.05f * i), 1); i++;
                }

                if (EventManager.EventGames[newEvent.EventType].CanUseClassSelector)
                {
                    AddInfoEntry(ref MainCont, UIPanel, msg("Class Selector", player), newEvent.UseClassSelector.ToString(), i);
                    if (!newEvent.UseClassSelector) CreateControlButton(ref MainCont, UIPanel, msg("Enable", player), "EMI_Creator classtoggle", 0.88f - (0.05f * i), 0);
                    else CreateControlButton(ref MainCont, UIPanel, msg("Disable", player), "EMI_Creator classtoggle", 0.88f - (0.05f * i), 0); i++;
                }

                AddInfoEntry(ref MainCont, UIPanel, msg("Disable Item Pickup", player), newEvent.DisableItemPickup.ToString(), i);
                if (!newEvent.DisableItemPickup) CreateControlButton(ref MainCont, UIPanel, msg("Enable", player), "EMI_Creator pickuptoggle", 0.88f - (0.05f * i), 0);
                else CreateControlButton(ref MainCont, UIPanel, msg("Disable", player), "EMI_Creator pickuptoggle", 0.88f - (0.05f * i), 0); i++;

                if (!EventManager.EventGames[newEvent.EventType].ForceCloseOnStart)
                {
                    AddInfoEntry(ref MainCont, UIPanel, msg("Close On Start", player), newEvent.CloseOnStart.ToString(), i);
                    if (!newEvent.CloseOnStart) CreateControlButton(ref MainCont, UIPanel, msg("Enable", player), "EMI_Creator costoggle", 0.88f - (0.05f * i), 0);
                    else CreateControlButton(ref MainCont, UIPanel, msg("Disable", player), "EMI_Creator costoggle", 0.88f - (0.05f * i), 0); i++;
                }
                if (EventManager.EventGames[newEvent.EventType].CanChooseRespawn)
                {
                    AddInfoEntry(ref MainCont, UIPanel, msg("Respawn Type", player), newEvent.RespawnType.ToString(), i);
                    CreateControlButton(ref MainCont, UIPanel, msg("None", player), "EMI_Creator respawn none", 0.88f - (0.05f * i), 0);
                    CreateControlButton(ref MainCont, UIPanel, msg("Timer", player), "EMI_Creator respawn timer", 0.88f - (0.05f * i), 1);
                    CreateControlButton(ref MainCont, UIPanel, msg("Waves", player), "EMI_Creator respawn wave", 0.88f - (0.05f * i), 2); i++;

                    if (newEvent.RespawnType != EventManager.RespawnType.None)
                    {
                        AddInfoEntry(ref MainCont, UIPanel, msg("Respawn Timer (seconds)", player), newEvent.RespawnTimer.ToString(), i);
                        CreateControlButton(ref MainCont, UIPanel, "+", $"EMI_Creator respawntime {newEvent.RespawnTimer + 1}", 0.88f - (0.05f * i), 0);
                        CreateControlButton(ref MainCont, UIPanel, "-", $"EMI_Creator respawntime {newEvent.RespawnTimer - 1}", 0.88f - (0.05f * i), 1); i++;
                    }
                }
                EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonbg"], msg("Save Config", player), 18, "0.84 0.015", "0.97 0.07", $"EMI_Creator saveconf");
            }
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", infoMessage, 18, "0.1 0.92", "0.9 0.98");
            CuiHelper.DestroyUi(player, UIPanel);
            CuiHelper.AddUi(player, MainCont);
        }
        private void EventRemover(BasePlayer player, int page = 0)
        {
            DestroyEntries(player);
            var MainCont = EventManager.UI.CreateElementContainer(UIPanel, UIColors["dark"], "0 0", "0.88 0.92");
            EventManager.UI.CreatePanel(ref MainCont, UIPanel, UIColors["light"], "0.011 0.01", "0.99 0.99", true);
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Remove created event configs using the remove button", player), 18, "0.2 0.92", "0.8 0.98");

            if (Event_Config.Event_List.Count > 9)
            {
                var maxpages = (Event_Config.Event_List.Count - 1) / 9 + 1;
                if (page < maxpages - 1)
                    EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonbg"], msg("Next", player), 18, "0.84 0.925", "0.97 0.98", $"EMI_ChangePage remover {page + 1}");
                if (page > 0)
                    EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonbg"], msg("Back", player), 18, "0.03 0.925", "0.16 0.98", $"EMI_ChangePage remover {page - 1}");
            }
            int maxentries = (9 * (page + 1));
            if (maxentries > Event_Config.Event_List.Count)
                maxentries = Event_Config.Event_List.Count;
            int eventcount = 9 * page;            

            if (Event_Config.Event_List.Count == 0)
                EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("No event configs found", player), 24, "0 0.82", "1 0.9");

            CuiHelper.DestroyUi(player, UIPanel);
            CuiHelper.AddUi(player, MainCont);

            var EventNames = new List<string>();
            foreach (var entry in Event_Config.Event_List)
                EventNames.Add(entry.Key);
            int i = 0;
            for (int n = eventcount; n < maxentries; n++)
            {
                CreateEventEntry(player, EventNames[n], Event_Config.Event_List[EventNames[n]], $"EMI_RemoveEvent {EventNames[n]}", "Remove", i);
                i++;
            }
        }        
        public void ClassSelector(BasePlayer player, string Kit = "")
        {            
            var MainCont = EventManager.UI.CreateElementContainer(UIPanel, UIColors["dark"], "0 0", "1 0.92");
            EventManager.UI.CreatePanel(ref MainCont, UIPanel, UIColors["light"], "0.155 0.01", "0.99 0.99", true);
            EventManager.UI.CreatePanel(ref MainCont, UIPanel, UIColors["light"], "0.01 0.01", "0.15 0.99", true);

            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Classes", player), 20, "0.01 0.92", "0.15 0.98");

            var currentClass = msg("Select a class from the options on the left", player);
            if (EventManager.isPlaying(player) && !string.IsNullOrEmpty(EventManager.GetUserClass(player)))            
                currentClass = $"{msg("Currently Selected Class:", player)} {Color1}{EventManager.GetUserClass(player)}</color>";            
                
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Class Selection", player), 20, "0.16 0.92", "0.95 0.98");
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", currentClass, 18, "0.18 0.86", "0.8 0.92", TextAnchor.MiddleLeft);

            int i = 0;
            foreach (var kit in ClassContents)
            {
                var color = UIColors["buttonbg"];
                if (Kit == kit.Key)
                    color = UIColors["buttongrey"];
                if (currentClass == kit.Key)
                    color = UIColors["buttonopen"];
                CreateClassButton(ref MainCont, UIPanel, kit.Key, i, color);
                i++;

            }            
            if (!string.IsNullOrEmpty(Kit))
            {
                EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", $"{Color1}'{Kit}'</color> {msg("Kit Contents:", player)}", 19, "0.3 0.76", "0.7 0.84", TextAnchor.MiddleLeft);
                EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", $"{Color2}{GetClassItems(Kit)}</color>", 17, "0.3 0.17", "0.7 0.75", TextAnchor.UpperLeft);
            }

            if (!string.IsNullOrEmpty(Kit))            
                EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonopen"], msg("Select", player), 18, "0.8 0.1", "0.93 0.17", $"EMI_ChangeClass {Kit}");            
            else EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttongrey"], "---", 18, "0.8 0.1", "0.93 0.17", "");

            CuiHelper.DestroyUi(player, UIPanel);
            CuiHelper.AddUi(player, MainCont);
        }
        public void DeathClassSelector(BasePlayer player)
        {
            string Kit = EventManager.GetUserClass(player) ?? string.Empty;
            var MainCont = EventManager.UI.CreateElementContainer(UIPanel, "0 0 0 0", "0 0", "1 0.92", true);
            EventManager.UI.CreateOutLineLabel(ref MainCont, UIPanel, "0 0 0 1", msg("Switch Class", player), 18, "1.0 1.0", "0.01 0.92", "0.13 0.98");
            EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonbg"], "X", 18, "0.125 0.935", "0.145 0.965", $"EMI_DeathChangeClass $close$");
            int i = 0;
            foreach (var kit in ClassContents)
            {
                var color = UIColors["buttonbg"];
                if (Kit == kit.Key)
                    color = UIColors["buttonopen"];
                CreateClassButton(ref MainCont, UIPanel, kit.Key, i, color, $"EMI_DeathChangeClass");
                i++;
            }
            CuiHelper.DestroyUi(player, UIPanel);
            CuiHelper.AddUi(player, MainCont);
        }
        private void PopupMessage(BasePlayer player, string msg, bool useColor = true, int time = 3)
        {
            if (PopupTimers.ContainsKey(player.userID) && PopupTimers[player.userID] != null)
                PopupTimers[player.userID].Destroy();            
             
            var element = EventManager.UI.CreateElementContainer(UIPopup, UIColors["dark"], "0.25 0.85", "0.75 0.95");
            EventManager.UI.CreatePanel(ref element, UIPopup, UIColors["buttonbg"], "0.005 0.04", "0.995 0.96");
            if (useColor) msg = $"{Color1}{msg}</color>";
            EventManager.UI.CreateLabel(ref element, UIPopup, "", msg, 18, "0 0", "1 1");            

            CuiHelper.DestroyUi(player, UIPopup);
            CuiHelper.AddUi(player, element);

            if (!PopupTimers.ContainsKey(player.userID))
              PopupTimers.Add(player.userID, timer.Once(time, () => CuiHelper.DestroyUi(player, UIPopup)));
            else PopupTimers[player.userID] = timer.Once(time, () => CuiHelper.DestroyUi(player, UIPopup));
        }
        
        private void KitSelection(BasePlayer player, string command, bool isWeaponSet = false)
        {
            var MainCont = EventManager.UI.CreateElementContainer(UIPanel, UIColors["dark"], "0 0", "0.88 0.92");
            EventManager.UI.CreatePanel(ref MainCont, UIPanel, UIColors["light"], "0.011 0.01", "0.99 0.99", true);
            object Kits;
            if (isWeaponSet) Kits = GetWeaponSets();
            else Kits = GetKits();

            int i = 1;
            var nonePos = CalcPlayerNamePos(0);
            var type = "kit";
            var name = msg("kit", player);

            if (isWeaponSet)
            {
                type = "ggconfig";
                name = msg("weapon set", player);
            }

            EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonbg"], msg("None", player), 10, $"{nonePos[0]} {nonePos[1]}", $"{nonePos[2]} {nonePos[3]}", $"{command} {type} none");
            if (Kits != null && Kits is string[])
            {                
                foreach (var kit in (string[])Kits)
                {
                    if (i > 84) break;
                    var pos = CalcPlayerNamePos(i);
                    EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonbg"], kit, 10, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"{command} {type} {kit}");
                    i++;
                }
            }
            if (i > 0) EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", string.Format(msg("Select a {0}", player), name), 18, "0.2 0.92", "0.8 0.98");
            else EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", string.Format(msg("Unable to find any {0}s", player), name), 18, "0.2 0.92", "0.8 0.98");
            CuiHelper.DestroyUi(player, UIPanel);
            CuiHelper.AddUi(player, MainCont);
        }
        private void EventSelection(BasePlayer player, string command)
        {
            var MainCont = EventManager.UI.CreateElementContainer(UIPanel, UIColors["dark"], "0 0", "0.88 0.92");
            EventManager.UI.CreatePanel(ref MainCont, UIPanel, UIColors["light"], "0.011 0.01", "0.99 0.99", true);
            var Games = EventManager.EventGames.Keys;
            int i = 0;
            if (Games != null && Games.Count > 0)
            {                
                foreach (var game in Games)
                {
                    if (i > 29) return;
                    var pos = CalcPlayerNamePos(i);
                    EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonbg"], game, 10, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"{command} type {game}");
                    i++;
                }                
            }
            if (i > 0) EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Select a event type", player), 18, "0.2 0.92", "0.8 0.98");
            else EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("There are no registered events", player), 18, "0.2 0.92", "0.8 0.98");
            CuiHelper.DestroyUi(player, UIPanel);
            CuiHelper.AddUi(player, MainCont);
        }
        private void ZoneSelection(BasePlayer player, string command)
        {
            var MainCont = EventManager.UI.CreateElementContainer(UIPanel, UIColors["dark"], "0 0", "0.88 0.92");
            EventManager.UI.CreatePanel(ref MainCont, UIPanel, UIColors["light"], "0.011 0.01", "0.99 0.99", true);
            var Zones = GetZones();
            int i = 0;
            if (Zones != null && Zones is string[])
            {                
                foreach (var zone in (string[])Zones)
                {
                    if (i > 85) return;
                    var pos = CalcPlayerNamePos(i);
                    var buttonString = zone;
                    var zoneName = GetZoneName(zone);
                    if (zoneName is string && !string.IsNullOrEmpty((string)zoneName))
                        buttonString = (string)zoneName;

                    EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonbg"], buttonString, 10, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"{command} zone {zone}");
                    i++;
                }
            }
            if (i > 0) EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Select a zone", player), 18, "0.2 0.92", "0.8 0.98");
            else EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Unable to find any zones", player), 18, "0.2 0.92", "0.8 0.98");
            CuiHelper.DestroyUi(player, UIPanel);
            CuiHelper.AddUi(player, MainCont);
        }
        private void SpawnSelection(BasePlayer player, string command)
        {
            var MainCont = EventManager.UI.CreateElementContainer(UIPanel, UIColors["dark"], "0 0", "0.88 0.92");
            EventManager.UI.CreatePanel(ref MainCont, UIPanel, UIColors["light"], "0.011 0.01", "0.99 0.99", true);
            var Spawns = GetSpawnfiles();
            int i = 0;
            if (Spawns != null && Spawns is string[])
            {
                foreach (var spawnfile in (string[])Spawns)
                {
                    if (i > 85) return;
                    var pos = CalcPlayerNamePos(i);
                    EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonbg"], spawnfile, 10, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"{command} {spawnfile}");
                    i++;
                } 
            }
            if (i > 0) EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Select a spawn file", player), 18, "0.2 0.92", "0.8 0.98");
            else EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Unable to find any spawn files", player), 18, "0.2 0.92", "0.8 0.98");
            CuiHelper.DestroyUi(player, UIPanel);
            CuiHelper.AddUi(player, MainCont);
        }
        private void KickSelectionMenu(BasePlayer player, int page = 0)
        {
            var MainCont = EventManager.UI.CreateElementContainer(UIPanel, UIColors["dark"], "0 0", "0.88 0.92");
            EventManager.UI.CreatePanel(ref MainCont, UIPanel, UIColors["light"], "0.011 0.01", "0.99 0.99", true);
            if (EventManager._Open || EventManager._Started)
            {
                EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Select a player to kick", player), 18, "0.2 0.92", "0.8 0.98");
                var players = EventManager.EventPlayers;
                if (players.Count > 85)
                {
                    var maxpages = (players.Count - 1) / 85 + 1;
                    if (page < maxpages - 1)
                        EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonbg"], msg("Next", player), 18, "0.84 0.925", "0.97 0.98", $"EMI_ChangePage kick {page + 1}");
                    if (page > 0)
                        EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonbg"], msg("Back", player), 18, "0.03 0.925", "0.16 0.98", $"EMI_ChangePage kick {page - 1}");
                }
                int maxentries = (85 * (page + 1));
                if (maxentries > players.Count)
                    maxentries = players.Count;
                int eventcount = 85 * page;

                int i = 0;
                for (int n = eventcount; n < maxentries; n++)
                {
                    var p = players[n];
                    if (p == null) continue;
                    if (!EventManager.isPlaying(p.GetPlayer()))
                    {
                        var pos = CalcPlayerNamePos(i);
                        EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonbg"], p.GetPlayer().displayName, 10, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"EMI_KickPlayer {p.GetPlayer().UserIDString}");
                        i++;
                    }
                }
            }
            else EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("There is no event in progress", player), 18, "0.2 0.92", "0.8 0.98");
            CuiHelper.DestroyUi(player, UIPanel);
            CuiHelper.AddUi(player, MainCont);
        }
        private void JoinSelectionMenu(BasePlayer player, int page = 0)
        {   
            var MainCont = EventManager.UI.CreateElementContainer(UIPanel, UIColors["dark"], "0 0", "0.88 0.92");
            EventManager.UI.CreatePanel(ref MainCont, UIPanel, UIColors["light"], "0.011 0.01", "0.99 0.99", true);
            if (EventManager._Open || EventManager._Started)
            {
                EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Select a player to force join the event", player), 18, "0.2 0.92", "0.8 0.98");
                var players = BasePlayer.activePlayerList;
                if (players.Count > 85)
                {
                    var maxpages = (players.Count - 1) / 85 + 1;
                    if (page < maxpages - 1)
                        EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonbg"], msg("Next", player), 18, "0.84 0.925", "0.97 0.98", $"EMI_ChangePage join {page + 1}");
                    if (page > 0)
                        EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonbg"], msg("Back", player), 18, "0.03 0.925", "0.16 0.98", $"EMI_ChangePage join {page - 1}");
                }
                int maxentries = (85 * (page + 1));
                if (maxentries > players.Count)
                    maxentries = players.Count;
                int eventcount = 85 * page;

                int i = 0;
                for (int n = eventcount; n < maxentries; n++)
                {
                    var p = players[n];
                    if (p == null) continue;
                    if (!EventManager.isPlaying(p))
                    {
                        var pos = CalcPlayerNamePos(i);
                        EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonbg"], p.displayName, 10, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"EMI_JoinPlayer {p.UserIDString}");
                        i++;
                    }
                }               
            }
            else EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("There is no event in progress", player), 18, "0.2 0.92", "0.8 0.98");
            CuiHelper.DestroyUi(player, UIPanel);
            CuiHelper.AddUi(player, MainCont);
        }
        private void SwitchConfig(BasePlayer player, int page = 0)
        {
            var MainCont = EventManager.UI.CreateElementContainer(UIPanel, UIColors["dark"], "0 0", "0.88 0.92");
            EventManager.UI.CreatePanel(ref MainCont, UIPanel, UIColors["light"], "0.011 0.01", "0.99 0.99", true);            
            if (Event_Config.Event_List.Count > 9)
            {
                var maxpages = (Event_Config.Event_List.Count - 1) / 9 + 1;
                if (page < maxpages - 1)
                    EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonbg"], msg("Next", player), 18, "0.84 0.925", "0.97 0.98", $"EMI_ChangePage config {page + 1}");
                if (page > 0)
                    EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonbg"], msg("Back", player), 18, "0.03 0.925", "0.16 0.98", $"EMI_ChangePage config {page - 1}");
            }
            int maxentries = (9 * (page + 1));
            if (maxentries > Event_Config.Event_List.Count)
                maxentries = Event_Config.Event_List.Count;
            int eventcount = 9 * page;

            if (Event_Config.Event_List.Count > 0) EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Select a event config from your list of preconfigured events", player), 18, "0.2 0.92", "0.8 0.98");
            else EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("You do not have any saved event configs", player), 18, "0.2 0.92", "0.8 0.98");

            CuiHelper.DestroyUi(player, UIPanel);
            CuiHelper.AddUi(player, MainCont);

            var EventNames = new List<string>();
            foreach (var entry in Event_Config.Event_List)
                EventNames.Add(entry.Key);
            int i = 0;
            for (int n = eventcount; n < maxentries; n++)
            {
                if (EventManager.EventGames.ContainsKey(Event_Config.Event_List[EventNames[n]].EventType))
                {
                    if (EventManager._CurrentEventConfig == EventNames[n])
                        CreateEventEntry(player, EventNames[n], Event_Config.Event_List[EventNames[n]], $"", $"{Color1}{msg("Selected", player)}</color>", i);
                    else CreateEventEntry(player, EventNames[n], Event_Config.Event_List[EventNames[n]], $"EMI_SelectConfig {EventNames[n]}", "Select", i);
                    i++;
                }
            }
        }
        private void NextAutoConfig(BasePlayer player)
        {
            var MainCont = EventManager.UI.CreateElementContainer(UIPanel, UIColors["dark"], "0 0", "0.88 0.92");
            EventManager.UI.CreatePanel(ref MainCont, UIPanel, UIColors["light"], "0.011 0.01", "0.99 0.99", true);
            
            int i = 0;
            foreach (var entry in EventManager.ValidAutoEvents)
            {
                if (i > 29) return;
                var pos = CalcEntryPos(i);
                var config = Event_Config.Event_List[entry.EventConfig];
                if (config == null) continue;
                EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonbg"], string.Format(msg("   Config: {0}\n   Type: {1}\n   Spawns: {2}   {3}\n   Kit: {4}{5}\n   Zone: {6}", player), entry.EventConfig, config.EventType, config.Spawnfile, config.Spawnfile2, config.Kit, config.WeaponSet, config.ZoneID), 11, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"EMI_NextAutoConfig {i} {entry.EventConfig}", TextAnchor.MiddleLeft);
                i++;

            }
            if (i == 0) EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("You do not have any saved auto event configs", player), 18, "0.2 0.92", "0.8 0.98");
            else EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", $"{msg("Currently Selected Config:", player)} {Color1}{EventManager._CurrentEventConfig ?? "None"}</color>", 18, "0.2 0.92", "0.8 0.98");
            CuiHelper.DestroyUi(player, UIPanel);
            CuiHelper.AddUi(player, MainCont);
        }
        private void NewAutoConfig(BasePlayer player)
        {
            var MainCont = EventManager.UI.CreateElementContainer(UIPanel, UIColors["dark"], "0 0", "0.88 0.92");
            EventManager.UI.CreatePanel(ref MainCont, UIPanel, UIColors["light"], "0.011 0.01", "0.99 0.99", true);
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("Select a event config to use for the auto-event", player), 18, "0.2 0.92", "0.8 0.98");
            int i = 0;
            foreach (var entry in Event_Config.Event_List)
            {
                if (i > 29) return;
                var pos = CalcEntryPos(i);
                EventManager.UI.CreateButton(ref MainCont, UIPanel, UIColors["buttonbg"], string.Format(msg("   Config: {0}\n   Type: {1}\n   Spawns: {2}   {3}\n   Kit: {4}{5}\n   Zone: {6}", player), entry.Key, entry.Value.EventType, entry.Value.Spawnfile, entry.Value.Spawnfile2, entry.Value.Kit, entry.Value.WeaponSet, entry.Value.ZoneID), 11, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"EMI_NewAutoConfig newconf {entry.Key}", TextAnchor.MiddleLeft);
                i++;

            }
            if (i == 0) EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", msg("You do not have any saved event configs", player), 18, "0.2 0.92", "0.8 0.98");            
            CuiHelper.DestroyUi(player, UIPanel);
            CuiHelper.AddUi(player, MainCont);
        }
        #endregion

        #region UI Creation
        private void CreateEventEntry(BasePlayer player, string name, EventManager.Events entry, string buttonCommand, string buttonText, int num, float dimH = 0.28f, float dimV = 0.263f)
        {
            if (!UIEntries.ContainsKey(player.userID))
                UIEntries.Add(player.userID, new List<string>());            
            Vector2 dimensions = new Vector2(dimH, dimV);
            Vector2 posMin = CalcEventEntryPos(num, dimensions);
            Vector2 posMax = posMin + dimensions;
            var panelname = UIEntries + num.ToString();
            var eventEntry = EventManager.UI.CreateElementContainer(panelname, "0 0 0 0", $"{posMin.x} {posMin.y}", $"{posMax.x} {posMax.y}");
            EventManager.UI.CreatePanel(ref eventEntry, panelname, UIColors["buttonbg"], $"0 0", $"1 1");

            EventManager.UI.CreateButton(ref eventEntry, panelname, UIColors["buttonbg"], buttonText, 18, $"0.75 0.83", $"0.97 0.97", $"{buttonCommand} {name}");
            int i = 0;
            AddInfoEntry(ref eventEntry, panelname, msg("Event Type", player), entry.EventType, i, 0.65f, 0.795f, 0.145f, 0.05f, 0.5f, 1, 16); i++;
            if (entry.EventType == "GunGame")
            { AddInfoEntry(ref eventEntry, panelname, msg("Weapon Set", player), entry.WeaponSet, i, 0.65f, 0.795f, 0.145f, 0.05f, 0.5f, 1, 16); i++; }
            else { AddInfoEntry(ref eventEntry, panelname, msg("Kit", player), entry.Kit, i, 0.65f, 0.795f, 0.145f, 0.05f, 0.5f, 1, 16); i++; }
            AddInfoEntry(ref eventEntry, panelname, msg("Spawnfile", player), $"{entry.Spawnfile}  {entry.Spawnfile2}", i, 0.65f, 0.795f, 0.145f, 0.05f, 0.5f, 1, 16); i++;
            AddInfoEntry(ref eventEntry, panelname, msg("Zone ID", player), entry.ZoneID, i, 0.65f, 0.795f, 0.145f, 0.05f, 0.5f, 1, 16); i++;
            AddInfoEntry(ref eventEntry, panelname, msg("Class Selector", player), entry.UseClassSelector.ToString(), i, 0.65f, 0.795f, 0.145f, 0.05f, 0.5f, 1, 16);

            EventManager.UI.CreateLabel(ref eventEntry, panelname, "", name, 22, $"0.05 0.83", "0.7 0.98", TextAnchor.MiddleLeft);
            UIEntries[player.userID].Add(panelname);
            CuiHelper.AddUi(player, eventEntry);
        }
        private void CreateMenuButton(ref CuiElementContainer container, string panelName, string buttonname, string command, int number)
        {
            Vector2 dimensions = new Vector2(0.1f, 0.6f);
            Vector2 origin = new Vector2(0.25f, 0.2f);
            Vector2 offset = new Vector2((0.01f + dimensions.x) * number, 0);

            Vector2 posMin = origin + offset;
            Vector2 posMax = posMin + dimensions;

            EventManager.UI.CreateButton(ref container, panelName, UIColors["buttonbg"], buttonname, 18, $"{posMin.x} {posMin.y}", $"{posMax.x} {posMax.y}", command);
        }
        private void CreateAdminButton(ref CuiElementContainer container, string panelName, string buttonname, string command, int number)
        {
            Vector2 dimensions = new Vector2(0.8f, 0.05f);
            Vector2 origin = new Vector2(0.055f, 0.9f);
            Vector2 offset = new Vector2(0, (0.01f + dimensions.y) * number);

            Vector2 posMin = origin - offset;
            Vector2 posMax = posMin + dimensions;

            EventManager.UI.CreateButton(ref container, panelName, UIColors["buttonbg"], buttonname, 18, $"{posMin.x} {posMin.y}", $"{posMax.x} {posMax.y}", command);
        }
        private void CreateControlButton(ref CuiElementContainer container, string panelName, string buttonname, string command, float minY, int number)
        {
            Vector2 dimensions = new Vector2(0.095f, 0.04f);
            Vector2 origin = new Vector2(0.5f, minY + 0.005f);
            Vector2 offset = new Vector2((0.005f + dimensions.x) * number, 0);

            Vector2 posMin = origin + offset;
            Vector2 posMax = posMin + dimensions;

            EventManager.UI.CreateButton(ref container, panelName, UIColors["buttonbg"], buttonname, 17, $"{posMin.x} {posMin.y}", $"{posMax.x} {posMax.y}", command);
        }
        private void CreateControlButtonSmall(ref CuiElementContainer container, string panelName, string buttonname, string command, float minY, int number)
        {
            Vector2 dimensions = new Vector2(0.095f, 0.05f);
            Vector2 origin = new Vector2(0.25f, minY + 0.005f);
            Vector2 offset = new Vector2((0.005f + dimensions.x) * number, 0);

            Vector2 posMin = origin + offset;
            Vector2 posMax = posMin + dimensions;

            EventManager.UI.CreateButton(ref container, panelName, UIColors["buttonbg"], buttonname, 14, $"{posMin.x} {posMin.y}", $"{posMax.x} {posMax.y}", command);
        }
        private void CreateClassButton(ref CuiElementContainer container, string panelName, string kit, int number, string color, string command = "EMI_ClassDescription")
        {
            Vector2 dimensions = new Vector2(0.13f, 0.05f);
            Vector2 origin = new Vector2(0.015f, 0.85f);
            float offsetY = (0.005f + dimensions.y) * number;            
            Vector2 offset = new Vector2(0, offsetY);

            Vector2 posMin = origin - offset;
            Vector2 posMax = posMin + dimensions;           

            EventManager.UI.CreateButton(ref container, panelName, color, kit, 16, posMin.x + " " + posMin.y, posMax.x + " " + posMax.y, $"{command} {kit}");
        }
        private void CreateClassEditorButton(ref CuiElementContainer container, string panelName, string name, int number, bool kit)
        {
            float posX = 0.076f;
            string command = $"EMI_AddClass {name}";
            if (!kit)
            {
                posX = 0.815f;
                command = $"EMI_RemoveClass {name}";
            }
            Vector2 dimensions = new Vector2(0.12f, 0.041f);
            Vector2 origin = new Vector2(posX, 0.795f);
            float offsetY = 0;
            float offsetX = 0;

            if (number >= 0 && number < 16)
            {
                offsetY = (0.0075f + dimensions.y) * number;
            }
            if (number > 15 && number < 32)
            {
                offsetY = (0.0075f + dimensions.y) * (number - 16);
                offsetX = (0.01f + dimensions.x) * 1;
            }
            if (number > 31 && number < 48)
            {
                offsetY = (0.0075f + dimensions.y) * (number - 32);
                offsetX = (0.01f + dimensions.x) * 2;
            }
            if (number > 47 && number < 64)
            {
                offsetY = (0.0075f + dimensions.y) * (number - 48);
                offsetX = (0.01f + dimensions.x) * 3;
            }
            if (number > 63 && number < 80)
            {
                offsetY = (0.0075f + dimensions.y) * (number - 64);
                offsetX = (0.01f + dimensions.x) * 4;
            }

            Vector2 posMin = new Vector2(origin.x + offsetX, origin.y - offsetY);
            Vector2 posMax = posMin + dimensions;

            EventManager.UI.CreateButton(ref container, panelName, UIColors["buttonbg"], name, 14, posMin.x + " " + posMin.y, posMax.x + " " + posMax.y, command);
        }
        private void CreateAutoEventEditorButton(ref CuiElementContainer container, string panelName, AEConfig config, int number, int confnum)
        {            
            Vector2 dimensions = new Vector2(0.1f, 0.05f);
            Vector2 origin = new Vector2(0.46f, 0.785f);
            float offsetY = (0.01f + dimensions.y) * number; 

            Vector2 posMin = new Vector2(origin.x, origin.y - offsetY);
            Vector2 posMax = posMin + dimensions;
            EventManager.UI.CreateLabel(ref container, panelName, "", $"{msg("Config:")} {Color1}{config.EventConfig}</color>", 14, $"{posMin.x} {posMin.y}", $"{posMin.x + 0.11f} {posMax.y}");
            EventManager.UI.CreateLabel(ref container, panelName, "", $"{msg("Join Time:")} {Color1}{config.TimeToJoin}</color>", 14, $"{posMin.x + 0.11f} {posMin.y}", $"{posMin.x + 0.21f} {posMax.y}");
            EventManager.UI.CreateLabel(ref container, panelName, "", $"{msg("Start Time:")} {Color1}{config.TimeToStart}</color>", 14, $"{posMin.x + 0.21f} {posMin.y}", $"{posMin.x + 0.31f} {posMax.y}");
            EventManager.UI.CreateLabel(ref container, panelName, "", $"{msg("Time Limit:")} {Color1}{config.TimeLimit}</color>", 14, $"{posMin.x + 0.31f} {posMin.y}", $"{posMin.x + 0.41f} {posMax.y}");
            EventManager.UI.CreateButton(ref container, panelName, UIColors["buttonbg"], msg("Remove"), 14, $"{posMin.x + 0.41f} {posMin.y}", $"{posMin.x + 0.51f} {posMax.y}", $"EMI_RemoveAutoEvent {confnum}");
        }
        private void AddInfoEntry(ref CuiElementContainer MainCont, string UIPanel, string key, string value, int num, float posMiny = 0.88f, float posMaxy = 0.94f, float spacing = 0.05f, float xMin = 0.05f, float xMid = 0.3f, float xMax = 0.5f, int fontSize = 18)
        {            
            if (num > 0)
            {
                posMiny = posMiny - (spacing * num);
                posMaxy = posMaxy - (spacing * num);
            }
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", $"{key}  :", fontSize, $"{xMin} {posMiny}", $"{xMid} {posMaxy}", TextAnchor.MiddleLeft);
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", $"{Color1}{value}</color>", fontSize, $"{xMid} {posMiny}", $"{xMax} {posMaxy}", TextAnchor.MiddleLeft);
        }        
        private void AddLBEntry(ref CuiElementContainer MainCont, string UIPanel, string playername, string kd, string wins, string rank, int num)
        {
            var posMin = 0.86f;
            var posMax = 0.92f;
            if (num > 0)
            {
                posMin = posMin - (0.03f * num);
                posMax = posMax - (0.03f * num);
            }
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", $"{Color1}{playername}</color>", 16, $"0.525 {posMin}", $"0.775 {posMax}", TextAnchor.MiddleCenter);
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", $"{Color1}{rank}</color>", 16, $"0.775 {posMin}", $"0.825 {posMax}", TextAnchor.MiddleCenter);
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", $"{Color1}{kd}</color>", 16, $"0.825 {posMin}", $"0.9 {posMax}", TextAnchor.MiddleCenter);
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", $"{Color1}{wins}</color>", 16, $"0.9 {posMin}", $"0.95 {posMax}", TextAnchor.MiddleCenter);            
        }
        private void AddInfoAuto(ref CuiElementContainer MainCont, string UIPanel, string key, string value, int num)
        {
            var posMin = 0.88f;
            var posMax = 0.95f;
            if (num > 0)
            {
                posMin = posMin - (0.06f * num);
                posMax = posMax - (0.06f * num);
            }
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", $"{key}  :", 16, $"0.02 {posMin}", $"0.175 {posMax}", TextAnchor.MiddleLeft);
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", $"{Color1}{value}</color>", 16, $"0.175 {posMin}", $"0.25 {posMax}", TextAnchor.MiddleLeft);
        }
        private void GetEventInfo(ref CuiElementContainer container, string panel, float xPos = 0.05f, float yPos = 0.72f, int fontSize = 18)
        {
            int i = 0;
            AddInfoEntry(ref container, panel, msg("Event Type"), EventManager._Event.EventType, i, yPos, yPos + 0.06f, 0.03f, xPos, xPos + 0.2f, xPos + 0.5f, fontSize); i++;            
            AddInfoEntry(ref container, panel, msg("Players"), GetPlayerCount(), i, yPos, yPos + 0.06f, 0.03f, xPos, xPos + 0.2f, xPos + 0.5f, fontSize); i++;
            if (EventManager.EventGames[EventManager._Event.EventType].RequiresKit && !EventManager._Event.UseClassSelector)
            { AddInfoEntry(ref container, panel, msg("Kit"), EventManager._Event.Kit, i, yPos, yPos + 0.06f, 0.03f, xPos, xPos + 0.2f, xPos + 0.5f, fontSize); i++; }
            if (EventManager._Event.EventType == "GunGame")
            { AddInfoEntry(ref container, panel, msg("Weapon Set"), EventManager._Event.WeaponSet, i, yPos, yPos + 0.06f, 0.03f, xPos, xPos + 0.2f, xPos + 0.5f, fontSize); i++; }
            if (EventManager._Event.UseClassSelector)
            { AddInfoEntry(ref container, panel, msg("Class Selector"), EventManager._Event.UseClassSelector.ToString(), i, yPos, yPos + 0.06f, 0.03f, xPos, xPos + 0.2f, xPos + 0.5f, fontSize); i++; }

        }
        private void GetGameScores(ref CuiElementContainer container, string panel, float xPos = 0.6f, float yPos = 0.72f, int fontSize = 18)
        {
            var scores = EventManager.GameScores;
            if (scores != null && scores.Scores.Count > 0)
            {
                int i = 2;
                AddInfoEntry(ref container, panel, msg("Leaders"), msg("Score"), 0, yPos, yPos + 0.06f, 0.03f, xPos, xPos + 0.2f, xPos + 0.35f, fontSize);
                foreach (var score in scores.Scores.Take(20))
                {                    
                    AddInfoEntry(ref container, panel, score.Value.Name, score.Value.Score.ToString(), i, yPos, yPos + 0.06f, 0.03f, xPos, xPos + 0.2f, xPos + 0.35f, fontSize);
                    i++;
                }
            }
        }
        private string GetEventStatus()
        {
            string status;
            if (EventManager._Open) status = msg("Open");
            else status = msg("Closed");
            if (EventManager._Started) status += msg(" - Started");
            else if (EventManager._Open) status += msg(" - Pending");
            else status += msg(" - Finished");
            return status;
        }
        private string GetAutoEventStatus()
        {
            if (EventManager._Launched) return msg("Launched");
            else return msg("Disabled");
        }
        private string GetPlayerCount()
        {
            int count;
            string maxCount;
            var max = EventManager._Event.MaximumPlayers;

            if (EventManager._Open)
            {
                if (!EventManager._Started)
                    count = EventManager.Joiners.Count;
                else count = EventManager.EventPlayers.Count;
            }
            else count = 0;

            if (max < 1) maxCount = "~";
            else maxCount = max.ToString();
            return $"{count} / {maxCount}";
        }
        private Vector2 CalcEventEntryPos(int number, Vector2 dimensions)
        {
            Vector2 position = new Vector2(0.015f, 0.56f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 3)
            {
                offsetX = (0.005f + dimensions.x) * number;
            }
            if (number > 2 && number < 6)
            {
                offsetX = (0.005f + dimensions.x) * (number - 3);
                offsetY = (-0.005f - dimensions.y) * 1;
            }
            if (number > 5 && number < 9)
            {
                offsetX = (0.005f + dimensions.x) * (number - 6);
                offsetY = (-0.005f - dimensions.y) * 2;
            }            
            return position + new Vector2(offsetX, offsetY);
        }
        private float[] CalcEntryPos(int number)
        {
            Vector2 position = new Vector2(0.019f, 0.8f);
            Vector2 dimensions = new Vector2(0.19f, 0.12f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 5)
            {
                offsetX = (0.002f + dimensions.x) * number;
            }
            if (number > 4 && number < 10)
            {
                offsetX = (0.002f + dimensions.x) * (number - 5);
                offsetY = (-0.0055f - dimensions.y) * 1;
            }
            if (number > 9 && number < 15)
            {
                offsetX = (0.002f + dimensions.x) * (number - 10);
                offsetY = (-0.0055f - dimensions.y) * 2;
            }
            if (number > 14 && number < 20)
            {
                offsetX = (0.002f + dimensions.x) * (number - 15);
                offsetY = (-0.0055f - dimensions.y) * 3;
            }
            if (number > 19 && number < 25)
            {
                offsetX = (0.002f + dimensions.x) * (number - 20);
                offsetY = (-0.0055f - dimensions.y) * 4;
            }
            if (number > 24 && number < 30)
            {
                offsetX = (0.002f + dimensions.x) * (number - 25);
                offsetY = (-0.0055f - dimensions.y) * 5;
            }

            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }
        private float[] CalcPlayerNamePos(int number)
        {
            Vector2 position = new Vector2(0.0145f, 0.82f);
            Vector2 dimensions = new Vector2(0.137f, 0.06f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 7)
            {
                offsetX = (0.002f + dimensions.x) * number;
            }
            if (number > 6 && number < 14)
            {
                offsetX = (0.002f + dimensions.x) * (number - 7);
                offsetY = (-0.0055f - dimensions.y) * 1;
            }
            if (number > 13 && number < 21)
            {
                offsetX = (0.002f + dimensions.x) * (number - 14);
                offsetY = (-0.0055f - dimensions.y) * 2;
            }
            if (number > 20 && number < 28)
            {
                offsetX = (0.002f + dimensions.x) * (number - 21);
                offsetY = (-0.0055f - dimensions.y) * 3;
            }
            if (number > 27 && number < 35)
            {
                offsetX = (0.002f + dimensions.x) * (number - 28);
                offsetY = (-0.0055f - dimensions.y) * 4;
            }
            if (number > 34 && number < 42)
            {
                offsetX = (0.002f + dimensions.x) * (number - 35);
                offsetY = (-0.0055f - dimensions.y) * 5;
            }
            if (number > 41 && number < 49)
            {
                offsetX = (0.002f + dimensions.x) * (number - 42);
                offsetY = (-0.0055f - dimensions.y) * 6;
            }
            if (number > 48 && number < 56)
            {
                offsetX = (0.002f + dimensions.x) * (number - 49);
                offsetY = (-0.0055f - dimensions.y) * 7;
            }
            if (number > 55 && number < 63)
            {
                offsetX = (0.002f + dimensions.x) * (number - 56);
                offsetY = (-0.0055f - dimensions.y) * 8;
            }
            if (number > 62 && number < 70)
            {
                offsetX = (0.002f + dimensions.x) * (number - 63);
                offsetY = (-0.0055f - dimensions.y) * 9;
            }
            if (number > 69 && number < 77)
            {
                offsetX = (0.002f + dimensions.x) * (number - 70);
                offsetY = (-0.0055f - dimensions.y) * 10;
            }
            if (number > 76 && number < 85)
            {
                offsetX = (0.002f + dimensions.x) * (number - 77);
                offsetY = (-0.0055f - dimensions.y) * 11;
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }
        private string GetClassItems(string kit)
        {
            if (ClassContents.ContainsKey(kit))
                return ClassContents[kit];                
            return "";
        }
        #region Colors
        private void SetUIColors()
        {
            UIColors.Add("dark", EventManager.UI.Color(configData.Colors.Background_Dark.Color, configData.Colors.Background_Dark.Alpha));
            UIColors.Add("medium", EventManager.UI.Color(configData.Colors.Background_Medium.Color, configData.Colors.Background_Medium.Alpha));
            UIColors.Add("light", EventManager.UI.Color(configData.Colors.Background_Light.Color, configData.Colors.Background_Light.Alpha));
            UIColors.Add("buttonbg", EventManager.UI.Color(configData.Colors.Button_Standard.Color, configData.Colors.Button_Standard.Alpha));
            UIColors.Add("buttonopen", EventManager.UI.Color(configData.Colors.Button_Accept.Color, configData.Colors.Button_Accept.Alpha));
            UIColors.Add("buttongrey", EventManager.UI.Color(configData.Colors.Button_Inactive.Color, configData.Colors.Button_Inactive.Alpha));
        }        
        #endregion
        #endregion

        #region UI Commands
        [ConsoleCommand("EMI_ChangeElement")]
        private void ccmdChangeElement(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            DestroyEntries(player);
            switch (arg.Args[0].ToLower())
            {
                case "home":
                    CuiHelper.DestroyUi(player, UIAdmin);
                    CreateHome(player);
                    return;
                case "voting":
                    CuiHelper.DestroyUi(player, UIAdmin);
                    CreateVoting(player);
                    return;
                case "stats":
                    CuiHelper.DestroyUi(player, UIAdmin);
                    CreateStatistics(player);
                    return;
                case "selectclass":
                    {
                        if (EventManager.isPlaying(player))
                            ClassSelector(player, player.GetComponent<EventManager.EventPlayer>().currentClass);
                        else ClassSelector(player);  
                    }
                    return;
                case "admin":
                    if (HasPerm(player))
                        CreateAdminMenu(player);                    
                    return;
                case "auto":
                    if (HasPerm(player))
                        EventAutoEvent(player);
                    return;
                case "class":
                    if (HasPerm(player))
                        EventClasses(player);
                    return;
                case "control":
                    if (HasPerm(player))
                        EventControl(player);
                    return;
                case "events":
                    if (HasPerm(player))
                        EventRemover(player); return;
                case "createevent":
                    if (HasPerm(player))
                    {
                        if (!EventCreators.ContainsKey(player.userID))
                            EventCreators.Add(player.userID, new EventManager.Events());
                        else EventCreators[player.userID] = new EventManager.Events();
                        EventCreator(player);
                    }
                    return;                
                case "kick":
                    if (HasPerm(player))
                        KickSelectionMenu(player);
                    return;
                case "join":
                    if (HasPerm(player))
                        JoinSelectionMenu(player);
                    return;
                default:
                    break;
            }
        }
        [ConsoleCommand("EMI_ChangePage")]
        private void ccmdChangePage(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            DestroyEntries(player);
            
            var type = arg.GetString(0);
            var page = int.Parse(arg.GetString(1));
            switch (type)
            {
                case "remover":
                    if (HasPerm(player))                    
                        EventRemover(player, page);
                    return;
                case "config":
                    if (HasPerm(player))
                        SwitchConfig(player, page);
                    return;
                case "autoevent":
                    if (HasPerm(player))
                        EventAutoEvent(player, page);
                    return;
                case "vote":
                    CreateVoting(player, page);
                    return;
                case "join":
                    JoinSelectionMenu(player, page);
                    return;
                case "kick":
                    KickSelectionMenu(player, page);
                    return;
                default:
                    break;
            }
        }
        [ConsoleCommand("EMI_AutoEditor")]
        private void ccmdAutoEditor(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            DestroyEntries(player);
            if (!HasPerm(player)) return;

            var type = arg.GetString(0);
            var num = int.Parse(arg.GetString(1));
            switch (type)
            {
                case "cancel":
                    if (Event_Config.AutoEvent_Config.AutoCancel)
                    {
                        Event_Config.AutoEvent_Config.AutoCancel = false;
                        SaveData();
                        EventAutoEvent(player);
                        PopupMessage(player, msg("You have disabled the auto cancel timer for auto events", player));
                        return;
                    }
                    else
                    {
                        Event_Config.AutoEvent_Config.AutoCancel = true;
                        SaveData();
                        EventAutoEvent(player);
                        PopupMessage(player, msg("You have enabled the auto cancel timer for auto events", player));
                        return;
                    }
                case "gameinterval":
                    if (num < 1) num = 1;
                    Event_Config.AutoEvent_Config.GameInterval = num;                    
                    SaveData();
                    EventAutoEvent(player);
                    return;
                case "canceltimer":
                    if (num < 1) num = 1;
                    Event_Config.AutoEvent_Config.AutoCancel_Timer = num;                    
                    SaveData();
                    EventAutoEvent(player);
                    return;
                case "randomize":
                    EventManager._RandomizeAuto = !EventManager._RandomizeAuto;                    
                    EventAutoEvent(player);
                    return;
                default:
                    break;
            }
        }
        [ConsoleCommand("EMI_Control")]
        private void ccmdControls(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (!HasPerm(player)) return;
            DestroyEntries(player);
            switch (arg.Args[0].ToLower())
            {
                case "open":
                    {
                        var success = EventManager.OpenEvent();
                        if (success is string)
                        {
                            PopupMessage(player, (string)success);
                            return;
                        }
                        else EventControl(player);
                    }
                    return;
                case "close":
                    {
                        var success = EventManager.CloseEvent();
                        if (success is string)
                        {
                            PopupMessage(player, (string)success);
                            return;
                        }
                        else EventControl(player);
                    }                    
                    return;
                case "start":
                    {
                        var success = EventManager.StartEvent();
                        if (success is string)
                        {
                            PopupMessage(player, (string)success);
                            return;
                        }
                        else if (!EventManager.isPlaying(player))
                            EventControl(player);
                    }
                    return;
                case "end":
                    {
                        var success = EventManager.EndEvent();
                        if (success is string)
                        {
                            PopupMessage(player, (string)success);
                            return;
                        }
                        else EventControl(player);
                    }
                    return;
                case "config":
                    {
                        if (EventManager._Started || EventManager._Open)
                        {
                            PopupMessage(player, msg("You can not switch event config whilst a game is underway", player));
                            return;
                        }
                        else SwitchConfig(player);
                    }
                    return;
                case "kit":
                    if (arg.Args.Length > 1)
                    {
                        string kit = arg.GetString(1);
                        if (kit == "None") kit = "";
                        EventManager._Event.Kit = kit;

                        if (!string.IsNullOrEmpty(kit))
                            Interface.CallHook("OnSelectKit", kit);

                        EventControl(player);
                        PopupMessage(player, $"{msg("You have changed the event kit to:", player)} {Color1}{kit}</color>", false);
                    }
                    else KitSelection(player, "EMI_Control");
                    return;
                case "gamemode":                    
                    {
                        if (EventManager._Started || EventManager._Open)
                        {
                            PopupMessage(player, msg("You can not switch the game mode whilst a game is underway", player));
                            return;
                        }
                        string mode = arg.GetString(1);
                        if (mode == "normal") EventManager._Event.GameMode = EventManager.GameMode.Normal;
                        else { EventManager._Event.GameMode = EventManager.GameMode.Battlefield; EventManager._Event.ScoreLimit = 0; }
                        EventControl(player);
                        PopupMessage(player, $"{msg("You have changed the game mode to:", player)} {Color1}{EventManager._Event.GameMode}</color>", false);
                    }                    
                    return;
                case "ggconfig":
                    if (arg.Args.Length > 1)
                    {
                        if (EventManager._Started || EventManager._Open)
                        {
                            PopupMessage(player, msg("You can not change weapon set whilst a game is underway", player));
                            return;
                        }
                        string set = arg.GetString(1);
                        if (set == "None") set = "";
                        EventManager._Event.WeaponSet = set;

                        if (!string.IsNullOrEmpty(set))
                            Interface.CallHook("ChangeWeaponSet", set);

                        EventControl(player);
                        PopupMessage(player, $"{msg("You have changed the weapon set to:", player)} {Color1}{set}</color>", false);
                    }
                    else KitSelection(player, "EMI_Control", true);
                    return;
                case "maxplayers":
                    int max = arg.GetInt(1);
                    EventManager._Event.MaximumPlayers = max;
                    EventControl(player); 
                    return;
                case "scorelimit":
                    int score = arg.GetInt(1);
                    EventManager._Event.ScoreLimit = score;
                    Interface.CallHook("SetScoreLimit", score);
                    EventControl(player);
                    return;
                case "enemycount":
                    var enemies = arg.GetInt(1);
                    EventManager._Event.EnemiesToSpawn = enemies;
                    if (enemies > 0)
                        Interface.CallHook("SetEnemyCount", enemies);
                    EventControl(player);
                    return;
                case "roundcount":
                    var rounds = arg.GetInt(1);
                    EventManager._Event.GameRounds = rounds;
                    if (rounds > 0)
                        Interface.CallHook("SetGameRounds", rounds);
                    EventControl(player);
                    return;
                case "type":
                    if (EventManager._Started || EventManager._Open)
                    {
                        PopupMessage(player, msg("You can not switch event type whilst a game is underway", player));
                        return;
                    }
                    if (arg.Args.Length > 1)
                    {
                        string type = arg.GetString(1);
                        EventManager._Event = new EventManager.Events { EventType = type, MinimumPlayers = 2, RespawnTimer = 10 };
                        EventManager._CurrentEventConfig = null;
                        var success = EventManager.SelectEvent(type);
                        if (success is string)
                        {
                            PopupMessage(player, (string) success, false);
                            return;
                        }
                        EventControl(player);
                        PopupMessage(player, $"{msg("You have changed the event type to:", player)} {Color1}{type}</color>", false);
                    }
                    else EventSelection(player, "EMI_Control");
                    return;
                case "spawns":
                    if (EventManager._Started || EventManager._Open)
                    {
                        PopupMessage(player, msg("You can not switch event spawn file whilst a game is underway", player));
                        return;
                    }
                    if (arg.Args.Length > 1)
                    {
                        string spawns = arg.GetString(1);
                        EventManager._Event.Spawnfile = spawns;
                        EventControl(player);
                        PopupMessage(player, $"{msg("You have changed the event spawn file to:", player)} {Color1}{spawns}</color>", false);
                    }
                    else SpawnSelection(player, "EMI_Control spawns");
                    return;
                case "spawns2":
                    if (EventManager._Started || EventManager._Open)
                    {
                        PopupMessage(player, msg("You can not switch event spawn file whilst a game is underway", player));
                        return;
                    }
                    if (arg.Args.Length > 1)
                    {
                        string spawns = arg.GetString(1);
                        EventManager._Event.Spawnfile2 = spawns;
                        EventControl(player);
                        PopupMessage(player, $"{msg("You have changed the second spawn file to:", player)} {Color1}{spawns}</color>", false);
                    }
                    else SpawnSelection(player, "EMI_Control spawns2");
                    return;
                case "zone":
                    if (EventManager._Started || EventManager._Open)
                    {
                        PopupMessage(player, msg("You can not switch event zone whilst a game is underway", player));
                        return;
                    }
                    if (arg.Args.Length > 1)
                    {
                        string zone = arg.GetString(1);
                        EventManager._Event.ZoneID = zone;
                        EventControl(player);
                        PopupMessage(player, $"{msg("You have changed the event zone to:", player)} {Color1}{zone}</color>", false);
                    }
                    else ZoneSelection(player, "EMI_Control");
                    return;
                case "classtoggle":
                    EventManager._Event.UseClassSelector = !EventManager._Event.UseClassSelector;
                    EventControl(player);
                    return;                
                case "pickuptoggle":
                    EventManager._Event.DisableItemPickup = !EventManager._Event.DisableItemPickup;
                    EventControl(player);
                    return;               
                case "costoggle":
                    EventManager._Event.CloseOnStart = !EventManager._Event.CloseOnStart;
                    EventControl(player);
                    return;               
                case "respawn":
                    {
                        if (EventManager._Started || EventManager._Open)
                        {
                            PopupMessage(player, msg("You can not change respawn type during a match!", player));
                            return;
                        }
                        switch (arg.Args[1])
                        {
                            case "none":
                                EventManager._Event.RespawnType = EventManager.RespawnType.None;
                                break;
                            case "timer":
                                EventManager._Event.RespawnType = EventManager.RespawnType.Timer;
                                break;
                            case "wave":
                                EventManager._Event.RespawnType = EventManager.RespawnType.Waves;
                                break;                            
                        }
                        EventControl(player);
                    }
                    return;
                case "spawntype":
                    {
                        if (EventManager._Started || EventManager._Open)
                        {
                            PopupMessage(player, msg("You can not change spawn type during a match!", player));
                            return;
                        }
                        switch (arg.Args[1])
                        {
                            case "seq":
                                EventManager._Event.SpawnType = EventManager.SpawnType.Consecutive;
                                break;
                            case "rand":
                                EventManager._Event.SpawnType = EventManager.SpawnType.Random;
                                break;                           
                        }
                        EventControl(player);
                    }
                    return;
                case "respawntime":
                    var time = arg.GetInt(1);
                    EventManager._Event.RespawnTimer = time;
                    EventControl(player);
                    return;
                case "aeenable":
                    {
                        var success = EventManager.LaunchEvent();
                        if (success is string)
                        {
                            PopupMessage(player, (string)success);
                            return;
                        }
                        else EventAutoEvent(player);
                    }                    
                    return;
                case "aedisable":
                    {
                        EventManager.CancelAutoEvent("AutoEvents disabled");                        
                        EventAutoEvent(player);
                    }
                    return;
                case "nextevent":
                    NextAutoConfig(player);
                    return;                
                default:
                    break;
            }
        }
        [ConsoleCommand("EMI_Creator")]
        private void ccmdCreator(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (!HasPerm(player)) return;
            DestroyEntries(player);
            var newEvent = EventCreators[player.userID];
            switch (arg.Args[0].ToLower())
            {                
                case "kit":
                    if (arg.Args.Length > 1)
                    {
                        var kit = arg.GetString(1);
                        if (kit == "None") kit = "";
                        newEvent.Kit = kit;
                        EventCreator(player);
                    }
                    else KitSelection(player, "EMI_Creator");
                    return;
                case "gamemode":
                    {
                        string mode = arg.GetString(1);
                        if (mode == "normal") newEvent.GameMode = EventManager.GameMode.Normal;
                        else { newEvent.GameMode = EventManager.GameMode.Battlefield; newEvent.ScoreLimit = 0; }
                            EventCreator(player);
                    }
                    return;
                case "ggconfig":
                    if (arg.Args.Length > 1)
                    {
                        var set = arg.GetString(1);
                        if (set == "None") set = "";
                        newEvent.WeaponSet = set;
                        EventCreator(player);
                    }
                    else KitSelection(player, "EMI_Creator", true);
                    return;                
                case "type":
                    if (arg.Args.Length > 1)
                    {
                        var type = arg.GetString(1);
                        newEvent.EventType = type;                        
                        EventCreator(player);
                    }
                    else EventSelection(player, "EMI_Creator");
                    return;
                case "spawns":
                    if (arg.Args.Length > 1)
                    {
                        var spawns = arg.GetString(1);
                        newEvent.Spawnfile = spawns;
                        EventCreator(player);
                    }
                    else SpawnSelection(player, "EMI_Creator spawns");
                    return;
                case "spawns2":
                    if (arg.Args.Length > 1)
                    {
                        var spawns = arg.GetString(1);
                        newEvent.Spawnfile2 = spawns;
                        EventCreator(player);
                    }
                    else SpawnSelection(player, "EMI_Creator spawns2");
                    return;
                case "scorelimit":
                    int score = arg.GetInt(1);
                    newEvent.ScoreLimit = score;
                    EventCreator(player);
                    return;
                case "zone":
                    if (arg.Args.Length > 1)
                    {
                        var zone = arg.GetString(1);
                        newEvent.ZoneID = zone;
                        EventCreator(player);
                    }
                    else ZoneSelection(player, "EMI_Creator");
                    return;
                case "classtoggle":
                    newEvent.UseClassSelector = !newEvent.UseClassSelector;
                    EventCreator(player);
                    return;                
                case "pickuptoggle":
                    newEvent.DisableItemPickup = !newEvent.DisableItemPickup;
                    EventCreator(player);
                    return;               
                case "costoggle":
                    newEvent.CloseOnStart = !newEvent.CloseOnStart;
                    EventCreator(player);
                    return;               
                case "min":
                    var min = arg.GetInt(1);
                    if (min < 0) min = 0;
                    newEvent.MinimumPlayers = min;
                    EventCreator(player);
                    return;
                case "max":
                    var max = arg.GetInt(1);
                    if (max < 0) max = 0;
                    newEvent.MaximumPlayers = max;
                    EventCreator(player);
                    return;
                case "enemycount":
                    var enemies = arg.GetInt(1);
                    newEvent.EnemiesToSpawn = enemies;
                    EventCreator(player);                    
                    return;
                case "roundcount":
                    var rounds = arg.GetInt(1);
                    newEvent.GameRounds = rounds;
                    EventCreator(player);
                    return;
                case "respawn":
                    {
                        switch (arg.Args[1])
                        {
                            case "none":
                                newEvent.RespawnType = EventManager.RespawnType.None;
                                break;
                            case "timer":
                                newEvent.RespawnType = EventManager.RespawnType.Timer;
                                break;
                            case "wave":
                                newEvent.RespawnType = EventManager.RespawnType.Waves;
                                break;
                        }
                        EventCreator(player);
                    }
                    return;
                case "spawntype":
                    {                        
                        switch (arg.Args[1])
                        {
                            case "seq":
                                newEvent.SpawnType = EventManager.SpawnType.Consecutive;
                                break;
                            case "rand":
                                newEvent.SpawnType = EventManager.SpawnType.Random;
                                break;
                        }
                        EventCreator(player);
                    }
                    return;
                case "respawntime":
                    var time = arg.GetInt(1);
                    newEvent.RespawnTimer = time;
                    EventCreator(player);
                    return;
                case "saveconf":
                    var success = EventManager.ValidateEvent(newEvent);
                    if (success is string)
                    {
                        EventCreator(player);
                        PopupMessage(player, (string)success, false);
                        return;
                    }
                    else
                    {
                        int i = 1;
                        foreach(var eventconf in Event_Config.Event_List)
                        {
                            if (eventconf.Value.EventType == newEvent.EventType)
                                i++;
                        }
                        var name = $"{newEvent.EventType}_{i}";
                        if (Event_Config.Event_List.ContainsKey(name))
                            name += UnityEngine.Random.Range(1, 10000);
                        if (Event_Config.Event_List.ContainsKey(name))
                            Event_Config.Event_List[name] = newEvent;
                        else Event_Config.Event_List.Add(name, newEvent);
                        if (EventManager.ValidEvents.ContainsKey(name))
                            EventManager.ValidEvents[name] = newEvent;
                        else EventManager.ValidEvents.Add(name, newEvent);
                        SaveData();
                        EventControl(player);
                        PopupMessage(player, string.Format(msg("CreateSuccess", player), Color1, name), false, 8);
                    }
                    return;              
                default:
                    break;
            }
        }

        [ConsoleCommand("EMI_ChangeClass")]
        private void ccmdChangeClass(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            string kit = string.Join(" ", arg.Args);
            if (EventManager.isPlaying(player))
            {
                var ePlayer = player.GetComponent<EventManager.EventPlayer>();
                if (Event_Config.Classes.Contains(kit))
                {
                    bool noGear = false;
                    if (string.IsNullOrEmpty(ePlayer.currentClass)) noGear = true;
                    ePlayer.currentClass = kit;
                    if (noGear)
                    {
                        EventManager.GivePlayerKit(player, kit);
                        DestroyAllUI(player);
                        return;
                    }
                    else
                    {
                        DestroyPanelUI(player);
                        DestroyAdminUI(player);
                        DestroyPopupUI(player);
                        DestroyEntries(player);
                        ClassSelector(player);
                    }                    
                }
            }

        }
        [ConsoleCommand("EMI_DeathChangeClass")]
        private void ccmdDeathChangeClass(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            string kit = string.Join(" ", arg.Args);
            if (kit == "$close$")
            {
                CuiHelper.DestroyUi(player, UIPanel);
                return;
            }
            if (EventManager.isPlaying(player))
            {
                var ePlayer = player.GetComponent<EventManager.EventPlayer>();
                if (Event_Config.Classes.Contains(kit))
                {
                    ePlayer.currentClass = kit;
                    DeathClassSelector(player);
                }
            }

        }
        [ConsoleCommand("EMI_AddClass")]
        private void ccmdAddClass(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (HasPerm(player))
            {
                if (Event_Config.Classes.Count >= 16)
                {
                    PopupMessage(player, msg("You have reached the class limit", player), false);
                    return;
                }
                string kit = arg.Args[0];
                Event_Config.Classes.Add(kit);
                GetKitItems(kit);
                SaveData();
                EventClasses(player);
                PopupMessage(player, string.Format(msg("You have added the kit {0}' {1} ' </color> to the class list", player), Color1, kit), false);
            }
        }
        [ConsoleCommand("EMI_RemoveClass")]
        private void ccmdRemoveClass(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (HasPerm(player))
            {
                string kit = arg.Args[0];
                Event_Config.Classes.Remove(kit);
                SaveData();
                EventClasses(player);
                PopupMessage(player, string.Format(msg("You have removed the kit {0}' {1} '</color> from the class list", player), Color1, kit), false);
            }
        }

        [ConsoleCommand("EMI_SelectConfig")]
        private void ccmdSelectConfig(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (HasPerm(player))
            {
                EventManager._Event = Event_Config.Event_List[arg.GetString(0)];
                EventManager._CurrentEventConfig = arg.Args[0];
                EventControl(player);
            }
        }
        [ConsoleCommand("EMI_NextAutoConfig")]
        private void ccmdNextAutoConfig(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (HasPerm(player))
            {
                EventManager._NextEventNum = arg.GetInt(0);
                EventManager._NextConfigName = arg.GetString(1);
                EventManager._ForceNextConfig = true;
                EventAutoEvent(player);
            }
        }
        [ConsoleCommand("EMI_NewAutoConfig")]
        private void ccmdNewAutoConfig(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (HasPerm(player))
            {
                var autocfg = AutoCreators[player.userID];
                if (autocfg == null) return;
                switch (arg.GetString(0))
                {
                    case "newconf":
                        if (arg.Args.Length == 2)
                        {
                            var confname = arg.GetString(1);
                            autocfg.EventConfig = confname;
                            EventAutoEvent(player);
                            return;
                        }
                        else NewAutoConfig(player);
                        return;
                    case "jointimer":
                        var jointime = int.Parse(arg.GetString(1));
                        if (jointime < 0) jointime = 0;
                        autocfg.TimeToJoin = jointime;
                        EventAutoEvent(player);
                        return;
                    case "starttimer":
                        var starttime = int.Parse(arg.GetString(1));
                        if (starttime < 0) starttime = 0;
                        autocfg.TimeToStart = starttime;
                        EventAutoEvent(player);
                        return;
                    case "timelimit":
                        var timelim = int.Parse(arg.GetString(1));
                        if (timelim < 0) timelim = 0;
                        autocfg.TimeLimit = timelim;
                        EventAutoEvent(player);
                        return;
                    case "saveconfig":
                        if (string.IsNullOrEmpty(autocfg.EventConfig))
                        {
                            PopupMessage(player, msg("You need to select a event config", player), false, 5);
                            return;
                        }
                        if (autocfg.TimeLimit < 1)
                        {
                            PopupMessage(player, msg("You need to set a time limit of atleast 1 minute", player), false, 5);
                            return;
                        }
                        Event_Config.AutoEvent_Config.AutoEvent_List.Add(autocfg);
                        EventManager.ValidAutoEvents.Add(autocfg);
                        SaveData();
                        AutoCreators[player.userID] = new AEConfig();
                        EventAutoEvent(player);
                        PopupMessage(player, msg("You have successfully added a new auto event", player), false, 5);
                        return;
                    default:
                        break;
                }
            }
        }
        [ConsoleCommand("EMI_KickPlayer")]
        private void ccmdKickPlayer(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var ID = arg.Args[0];
            var target = BasePlayer.FindByID(ulong.Parse(ID));
            if (target != null)
            {
                EventManager.LeaveEvent(target);
                KickSelectionMenu(player);
                PopupMessage(player, string.Format(msg("You have kicked: {0}", player), target.displayName));
                PopupMessage(target, msg("You have been kicked from the event", target));                
            }                
        }
        [ConsoleCommand("EMI_LeaveEvent")]
        private void ccmdLeaveEvent(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;            
            PopupMessage(player, msg("You have left the event", player));
            EventManager.LeaveEvent(player);
            CreateHome(player);           
        }
        [ConsoleCommand("EMI_JoinEvent")]
        private void ccmdJoinEvent(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;            
            EventManager.JoinEvent(player);
            if (!EventManager._Started)
                CreateHome(player);
            PopupMessage(player, msg("You have joined the event", player));
        }
        [ConsoleCommand("EMI_JoinPlayer")]
        private void ccmdJoinPlayer(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var ID = arg.Args[0];
            var target = BasePlayer.FindByID(ulong.Parse(ID));
            if (target != null)
            {
                EventManager.JoinEvent(target);
                JoinSelectionMenu(player);
                PopupMessage(player, string.Format(msg("You have forced joined: {0}", player), target.displayName));
                PopupMessage(target, msg("You have been sent to the event", player));                                
            }
        }
        [ConsoleCommand("EMI_ClassDescription")]
        private void ccmdClassDescription(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            string kit = string.Join(" ", arg.Args);
            if (EventManager.isPlaying(player))
            {
                DestroyPanelUI(player);
                ClassSelector(player, kit);
            }

        }
        [ConsoleCommand("EMI_DestroyAll")]
        private void ccmdDestroyAll(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            DestroyAllUI(player);
        }
        [ConsoleCommand("EMI_RemoveEvent")]
        private void ccmdRemoveEvent(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (HasPerm(player))
            {
                string name = arg.Args[0];
                if (Event_Config.Event_List.ContainsKey(name))
                {
                    Event_Config.Event_List.Remove(name);
                    if (EventManager.ValidEvents.ContainsKey(name))
                        EventManager.ValidEvents.Remove(name);
                    if (EventManager._CurrentEventConfig == name)
                        EventManager._Event = EventManager.DefaultConfig;
                    SaveData();
                    EventRemover(player);
                    PopupMessage(player, string.Format(msg("You have removed the event config: {0}{1}</color>", player), Color1, name), false);
                    return;
                }
                else 
                PopupMessage(player, string.Format(msg("Error removing the event config: {0}{1}</color>", player), Color1, name), false);
            }
        }
        [ConsoleCommand("EMI_RemoveAutoEvent")]
        private void ccmdRemoveAutoEvent(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (HasPerm(player))
            {
                int number = int.Parse(arg.GetString(0));
                var conf = Event_Config.AutoEvent_Config.AutoEvent_List[number];
                Event_Config.AutoEvent_Config.AutoEvent_List.Remove(conf);
                SaveData();
                EventAutoEvent(player);
                PopupMessage(player, msg("You have successfully removed the event config from the roster", player), false);
                return;
            }
        }
        [ConsoleCommand("EMI_EventVote")]
        private void ccmdEventVote(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            DestroyEntries(player);
            var args = arg.GetString(0);
            switch (args)
            {
                case "vote":
                    var number = int.Parse(arg.GetString(1));
                    EventVotes.AddPlayerVote(player.userID, number);
                    CreateVoting(player);
                    PopupMessage(player, string.Format(msg("You have voted for Event {0}", player), number), false, 5);
                    return;
                case "unvote":
                    EventVotes.RemovePlayerVote(player.userID);
                    CreateVoting(player);
                    PopupMessage(player, msg("You have retracted your vote", player), false, 5);
                    return;
                case "open":
                    EventVotes.VoteOpenEvent(player.userID);
                    CreateVoting(player);
                    PopupMessage(player, msg("You have voted to open an event", player), false, 5);
                    return;
                default:
                    break;
            }
        }
        #endregion

        #region UI Functions
        public void DestroyMenuUI(BasePlayer player) => CuiHelper.DestroyUi(player, UIMain);        
        public void DestroyPanelUI(BasePlayer player) => CuiHelper.DestroyUi(player, UIPanel);        
        public void DestroyAdminUI(BasePlayer player) => CuiHelper.DestroyUi(player, UIAdmin);
        public void DestroyPopupUI(BasePlayer player) => CuiHelper.DestroyUi(player, UIPopup);
        public void DestroyEntries(BasePlayer player)
        {
            DestroyPopupUI(player);
            if (UIEntries.ContainsKey(player.userID))
            {
                foreach (var entry in UIEntries[player.userID])
                    CuiHelper.DestroyUi(player, entry);
            }
        }
        public void DestroyAllUI(BasePlayer player)
        {            
            DestroyMenuUI(player);
            DestroyPanelUI(player);
            DestroyAdminUI(player);
            DestroyPopupUI(player);
            DestroyEntries(player);
            OpenMap(player);
        }
        #endregion

        #region Functions
        private void GetRequiredVoteLoop()
        {
            if (configData.Voting.Standard_AllowVoteToOpen)
            {
                var required = Convert.ToInt32(BasePlayer.activePlayerList.Count * configData.Voting.Standard_RequiredVoteFraction);
                if (required < configData.Voting.Standard_MinPlayersRequired)
                    required = configData.Voting.Standard_MinPlayersRequired;
                EventVotes.SetVotesToOpen(required);
                timer.Once(60, () => GetRequiredVoteLoop());
            }
        }
        public void StartEventVoteTimer()
        {
            VotingOpen = true;
            eminterface.VoteTallyTimer = timer.Once((Event_Config.AutoEvent_Config.GameInterval * 60) - 30, () => EventVotes.ProcessEventVotes()); 
        }
        
        private void CollectItemDetails()
        {
            foreach(var item in ItemManager.itemList)
            {
                if (!ItemNames.ContainsKey(item.itemid.ToString()))
                    ItemNames.Add(item.itemid.ToString(), item.displayName.translated);
            }
        }
        private void CollectClassList()
        {
            foreach(var kit in Event_Config.Classes)
            {
                GetKitItems(kit);
            }
        }
        private void GetKitItems(string kit)
        {
            var contents = GetKitContents(kit);
            if (!string.IsNullOrEmpty(contents))
            {
                ClassContents.Add(kit, contents);
            }
        }
        
        private object GetKits() => Kits?.Call("GetAllKits");
        private object GetWeaponSets() => GunGame?.Call("GetWeaponSets");
        private string GetKitContents(string kitname)
        {
            var contents = Kits?.Call("GetKitContents", kitname);
            if (contents != null)
            {
                var itemString = "";
                foreach (var item in (string[])contents)
                {                    
                    var entry = item.Split('_');
                    if (!ItemNames.ContainsKey(entry[0]))
                        continue;
                    itemString = itemString + $"{entry[1]}x {ItemNames[entry[0]]}";

                    if (entry.Length > 2)
                    {
                        itemString = itemString + " ( ";
                        for (int i = 2; i < entry.Length; i++)
                        {
                            itemString = itemString + $"{ItemNames[entry[i]]}";
                            if (i < entry.Length - 1)
                                itemString += ", ";
                            else itemString += " )";
                        }
                    }
                    itemString = itemString + "\n";
                }
                return itemString;
            }
            return null;
        }
        private object GetZones() => ZoneManager?.Call("GetZoneIDs");
        private object GetZoneName(string zoneid) => ZoneManager?.Call("GetZoneName", zoneid);
        private object GetSpawnfiles() => Spawns?.Call("GetSpawnfileNames");
        private string GetRatio(int kills, int deaths)
        {
            var divisor = GCD(kills, deaths);
            string k;
            string d;
            if (kills == 0)
                k = "0";
            else k = $"{kills / divisor}";
            if (deaths == 0)
                d = "0";
            else d = $"{deaths / divisor}";
            return $"{k} : {d}";
        }
        private int GCD(int a, int b)
        {
            return b == 0 ? a : GCD(b, a % b);
        }
        private void GetLeaderBoard(ref CuiElementContainer MainCont, string panel)
        {
            var leaders = EventManager.GameStatistics.Stats.OrderByDescending(key => key.Value.Score).Take(15);
            int i = 1;
            var posMin = 0.86f;
            var posMax = 0.92f;
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", $"{Color2}{msg("Name")}</color>", 16, $"0.525 {posMin}", $"0.775 {posMax}", TextAnchor.MiddleCenter);
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", $"{Color2}{msg("Rank")}</color>", 16, $"0.775 {posMin}", $"0.825 {posMax}", TextAnchor.MiddleCenter);
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", $"{Color2}{msg("K/D")}</color>", 16, $"0.825 {posMin}", $"0.9 {posMax}", TextAnchor.MiddleCenter);
            EventManager.UI.CreateLabel(ref MainCont, UIPanel, "", $"{Color2}{msg("Wins")}</color>", 16, $"0.9 {posMin}", $"0.95 {posMax}", TextAnchor.MiddleCenter);
            foreach (var entry in leaders)
            {
                AddLBEntry(ref MainCont, panel, entry.Value.Name, GetRatio(entry.Value.Kills, entry.Value.Deaths), entry.Value.GamesWon.ToString(), entry.Value.Rank.ToString(), i);
                i++;
            }            
        }
        #endregion

        #region External Calls        
        public void CloseMap(BasePlayer player)
        {
            if (LustyMap)
            {
                LustyMap.Call("DisableMaps", player);
            }
        }
        private void OpenMap(BasePlayer player)
        {
            if (LustyMap)
            {
                LustyMap.Call("EnableMaps", player);
            }
        }       
        #endregion

        #region Config        
        private ConfigData configData;
        class Voting
        {
            public bool Auto_AllowEventVoting { get; set; }
            public bool Standard_AllowVoteToOpen { get; set; }
            public float Standard_RequiredVoteFraction { get; set; }
            public int Standard_MinPlayersRequired { get; set; }
        }
        class Colors
        {
            public string TextColor_Primary { get; set; }
            public string TextColor_Secondary { get; set; }
            public UIColor Background_Dark { get; set; }
            public UIColor Background_Medium { get; set; }
            public UIColor Background_Light { get; set; }
            public UIColor Button_Standard { get; set; }
            public UIColor Button_Accept { get; set; }
            public UIColor Button_Inactive { get; set; }
        }
        class UIColor
        {
            public string Color { get; set; }
            public float Alpha { get; set; }
        }
        class ConfigData
        {
            public Colors Colors { get; set; }
            public Voting Voting { get; set; }
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
            Color1 = $"<color={configData.Colors.TextColor_Primary}>";
            Color2 = $"<color={configData.Colors.TextColor_Secondary}>";
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                Colors = new Colors
                {
                    Background_Dark = new UIColor { Color = "#2a2a2a", Alpha = 0.98f },
                    Background_Medium = new UIColor { Color = "#373737", Alpha = 0.98f },
                    Background_Light = new UIColor { Color = "#696969", Alpha = 0.3f },
                    Button_Accept = new UIColor { Color = "#00cd00", Alpha = 0.9f },
                    Button_Inactive = new UIColor { Color = "#a8a8a8", Alpha = 0.9f },
                    Button_Standard = new UIColor { Color = "#2a2a2a", Alpha = 0.9f },
                    TextColor_Primary = "#ce422b",
                    TextColor_Secondary = "#939393"
                },
                Voting = new Voting
                {
                    Auto_AllowEventVoting = true,
                    Standard_AllowVoteToOpen = true,
                    Standard_RequiredVoteFraction = 0.4f,
                    Standard_MinPlayersRequired = 10
                }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Data       
        void SaveData()
        {            
            E_Conf.WriteObject(Event_Config);
        }
        void LoadData()
        {
            try
            {
                Event_Config = E_Conf.ReadObject<EventConfig>();                
            }
            catch
            {
                Puts("Couldn't load event data, creating new datafile");
                Event_Config = new EventConfig();
            }
        }
        #endregion

        #region Commands
        [ChatCommand("renameevent")]
        void cmdRenameEvent(BasePlayer player, string command, string[] args)
        {
            if (!HasPerm(player)) return;

            if (args.Length == 2)
            {
                if (EventManager._Open || EventManager._Started)
                {
                    SendReply(player, msg("You can not rename events whilst a game is open or is being played", player));
                    return;
                }
                if (Event_Config.Event_List.ContainsKey(args[0]))
                {
                    if (!Event_Config.Event_List.ContainsKey(args[1]))
                    {
                        var config = Event_Config.Event_List[args[0]];
                        Event_Config.Event_List.Remove(args[0]);
                        Event_Config.Event_List.Add(args[1], config);
                        if (EventManager._CurrentEventConfig == args[0])
                            EventManager._Event = EventManager.DefaultConfig;
                        SaveData();
                        EventManager.ValidateAllEvents();
                        SendReply(player, string.Format(msg("You have successfully renamed the event config '{0}' to '{1}'", player), args[0], args[1]));
                        return;
                    }
                    else
                    {
                        SendReply(player, string.Format(msg("An event config with the name '{0}' already exists", player), args[1]));
                        return;
                    }
                }
                else
                {
                    SendReply(player, string.Format(msg("Could not find a event config with the name: {0}", player), args[0]));
                    return;
                }
            }
            SendReply(player, "/renameevent <currentname> <newname>");
            
        }
        
        [ConsoleCommand("event")]
        void ccmdEvent(ConsoleSystem.Arg arg)
        {
            if (!HasAccess(arg)) return;
            if (arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(arg, msg("event open - Open a event"));
                SendReply(arg, msg("event cancel - Cancel a event"));
                SendReply(arg, msg("event start - Start a event"));
                SendReply(arg, msg("event close - Close a event to new entries"));
                SendReply(arg, msg("event end - End a event"));
                SendReply(arg, msg("event launch - Launch auto events"));

                SendReply(arg, msg("event battlefield - Toggle battlefield mode"));
                SendReply(arg, msg("event classselector - Toggle the class selector"));
                SendReply(arg, msg("event config - Load a premade event config"));
                SendReply(arg, msg("event config list - Display all event config names"));
                SendReply(arg, msg("event enemies XX - Set the enemy count for this event"));
                SendReply(arg, msg("event game \"Game Name\" - Change event game"));
                SendReply(arg, msg("event kit \"kitname\" - Change the event kit"));
                SendReply(arg, msg("event minplayers XX - Set minimum required players"));
                SendReply(arg, msg("event maxplayers XX - Set maximum players"));
                SendReply(arg, msg("event rounds XX - Set the amount of rounds for this event"));
                SendReply(arg, msg("event spawnfile \"filename\" - Change the event spawnfile"));
                SendReply(arg, msg("event spawnfile2 \"filename\" - Change the second event spawnfile"));                
                SendReply(arg, msg("event scorelimit XX - Set the event scorelimit"));
                SendReply(arg, msg("event weaponset \"filename\" - Change the event weapon set (GunGame)"));
                SendReply(arg, msg("event zone \"zoneID\" - Change the event zone"));
                return;
            }
            if (arg.Args[0].ToLower() == "config")
            {
                if (arg.Args.Length == 2)
                {
                    if (arg.Args[1].ToLower() == "list")
                    {
                        if (Event_Config.Event_List.Count > 0)
                        {
                            SendReply(arg, msg("Config List:"));
                            foreach (var conf in Event_Config.Event_List)
                                SendReply(arg, conf.Key);
                        }
                        else SendReply(arg, msg("No configs have been saved"));
                        return;
                    }
                    if (Event_Config.Event_List.ContainsKey(arg.Args[1]))
                    {
                        EventManager._Event = Event_Config.Event_List[arg.Args[1]];
                        EventManager._CurrentEventConfig = arg.Args[1];

                        SendReply(arg, string.Format(msg("{0} has been set as the current event config"), arg.Args[1]));
                    }
                    else SendReply(arg, string.Format(msg("{0} is not a valid event config"), arg.Args[1]));
                }
                else SendReply(arg, string.Format(msg("Current event config: {0}"), EventManager._CurrentEventConfig));
                return;
            }
            if (arg.Args[0].ToLower() == "game")
            {
                if (arg.Args.Length > 1)
                {
                    object game = EventManager.SelectEvent(arg.Args[1]);
                    if (game is string)
                    {
                        SendReply(arg, (string)game);
                        return;
                    }
                    SendReply(arg, string.Format(msg("{0} is now the next Event game."), arg.Args[1]));
                    return;
                }
                else SendReply(arg, string.Format(msg("{0} is the next Event game."), EventManager._Event.EventType));
                return;
            }
            if (EventManager._Event == null || string.IsNullOrEmpty(EventManager._Event.EventType))
            {
                SendReply(arg, msg("You must set the event game first"));
                return;
            }
            var _Event = EventManager._Event;
            switch (arg.Args[0].ToLower())
            {
                case "cancel":
                    EventManager._Launched = false;
                    if (EventManager._Open) EventManager.CancelAutoEvent(msg("The event has been cancelled"));
                    SendReply(arg, msg("The event has been cancelled"));
                    return;
                case "open":
                    object open = EventManager.OpenEvent();
                    if (open is string)
                    {
                        SendReply(arg, (string)open);
                        return;
                    }
                    SendReply(arg, string.Format(msg("Event \"{0}\" is now open."), EventManager._Event.EventType));
                    return;
                case "start":
                    object start = EventManager.StartEvent();
                    if (start is string)
                    {
                        SendReply(arg, (string)start);
                        return;
                    }
                    SendReply(arg, string.Format(msg("Event \"{0}\" has started."), EventManager._Event.EventType));
                    return;
                case "close":
                    object close = EventManager.CloseEvent();
                    if (close is string)
                    {
                        SendReply(arg, (string)close);
                        return;
                    }
                    SendReply(arg, string.Format(msg("Event \"{0}\" is now closed for entries."), EventManager._Event.EventType));
                    return;
                case "end":
                    object end = EventManager.EndEvent();
                    if (end is string)
                    {
                        SendReply(arg, (string)end);
                        return;
                    }
                    SendReply(arg, string.Format(msg("Event \"{0}\" has ended."), EventManager._Event.EventType));
                    return;
                case "minplayers":
                    if (arg.Args.Length == 2)
                    {
                        int amount;
                        if (int.TryParse(arg.Args[1], out amount))
                        {
                            _Event.MinimumPlayers = amount;
                            SendReply(arg, string.Format(msg("Minimum Players for {0} is now {1}."), EventManager._Event.EventType, arg.Args[1]));
                        }
                        else SendReply(arg, msg("You must enter a valid number"));
                    }
                    else SendReply(arg, string.Format(msg("Minimum players: {0}"), _Event.MinimumPlayers));
                    return;
                case "maxplayers":
                    if (arg.Args.Length == 2)
                    {
                        int amount;
                        if (int.TryParse(arg.Args[1], out amount))
                        {
                            _Event.MaximumPlayers = amount;
                            SendReply(arg, string.Format(msg("Maximum Players for {0} is now {1}."), EventManager._Event.EventType, arg.Args[1]));
                        }
                        else SendReply(arg, msg("You must enter a valid number"));
                    }
                    else SendReply(arg, string.Format(msg("Maximum players: {0}"), _Event.MinimumPlayers));
                    return;
                case "spawnfile":
                    if (arg.Args.Length == 2)
                    {
                        if (EventManager._Open || EventManager._Started)
                        {
                            SendReply(arg, msg("You can not switch event spawn file whilst a game is underway"));
                            return;
                        }
                        object spawnfile = EventManager.ValidateSpawnFile(arg.Args[1]);
                        if (spawnfile is string)
                        {
                            SendReply(arg, (string)spawnfile);
                            return;
                        }
                        _Event.Spawnfile = arg.Args[1];
                        SendReply(arg, string.Format(msg("Spawnfile for {0} is now {1} ."), _Event.EventType, arg.Args[1]));
                    }
                    else SendReply(arg, string.Format(msg("Spawnfile: {0}"), _Event.Spawnfile));
                    return;
                case "spawnfile2":
                    if (arg.Args.Length == 2)
                    {
                        if (EventManager._Open || EventManager._Started)
                        {
                            SendReply(arg, msg("You can not switch event spawn file whilst a game is underway"));
                            return;
                        }
                        object spawnfile2 = EventManager.ValidateSpawnFile(arg.Args[1]);
                        if (spawnfile2 is string)
                        {
                            SendReply(arg, (string)spawnfile2);
                            return;
                        }
                        _Event.Spawnfile2 = arg.Args[1];
                        SendReply(arg, string.Format(msg("Spawnfile 2 for {0} is now {1}."), _Event.EventType, arg.Args[1]));
                    }
                    else SendReply(arg, string.Format(msg("Second Spawnfile: {0}"), _Event.Spawnfile2));
                    return;
                case "kit":
                    if (arg.Args.Length == 2)
                    {
                        object success = EventManager.ValidateKit(arg.Args[1]);
                        if (success is string)
                        {
                            SendReply(arg, (string)success);
                            return;
                        }
                        _Event.Kit = arg.Args[1];
                        Interface.CallHook("OnSelectKit", _Event.Kit);
                        SendReply(arg, string.Format(msg("The new Kit for {0} is now {1}"), _Event.EventType, arg.Args[1]));
                    }
                    else SendReply(arg, string.Format(msg("Kit: {0}"), _Event.Kit));
                    return;
                case "launch":
                    object launch = EventManager.LaunchEvent();
                    if (launch is string)
                    {
                        SendReply(arg, (string)launch);
                        return;
                    }
                    SendReply(arg, string.Format(msg("Event \"{0}\" is now launched."), _Event.EventType));
                    return;
                case "scorelimit":
                    if (string.IsNullOrEmpty(EventManager.EventGames[_Event.EventType].ScoreType))
                    {
                        SendReply(arg, msg("This event does not have a scoring system"));
                        return;
                    }
                    if (arg.Args.Length == 2)
                    {
                        int amount;
                        if (!int.TryParse(arg.Args[1], out amount))
                        {
                            SendReply(arg, msg("You must enter a valid number"));
                            return;
                        }
                        _Event.ScoreLimit = amount;
                        Interface.CallHook("SetScoreLimit", _Event.ScoreLimit);

                        SendReply(arg, string.Format(msg("You have set the score limit for {0} to {1}"), _Event.EventType, amount));
                    }
                    else SendReply(arg, string.Format(msg("Scorelimit: {0}"), _Event.ScoreLimit));
                    return;
                case "weaponset":                    
                    if (_Event.EventType != "GunGame")
                    {
                        SendReply(arg, msg("Only GunGame requires weapon sets"));
                        return;
                    }
                    if (arg.Args.Length == 2)
                    {
                        if (EventManager._Open || EventManager._Started)
                        {
                            SendReply(arg, msg("You can not change weapon set whilst a game is underway"));
                            return;
                        }
                        var sets = GetWeaponSets();
                        if (sets != null)
                        {
                            if ((sets as string[]).Contains(arg.Args[1]))
                            {
                                _Event.WeaponSet = arg.Args[1];
                                SendReply(arg, string.Format(msg("You have set the event weapon set to {0}"), arg.Args[1]));
                                return;
                            }
                            else SendReply(arg, string.Format(msg("{0} is not a valid weapon set"), arg.Args[1]));
                        }
                        else SendReply(arg, msg("Unable to retrieve the weapon set list"));
                    }
                    else SendReply(arg, string.Format(msg("Weapon set: {0}"), _Event.WeaponSet));
                    return;
                case "zone":
                    if (arg.Args.Length == 2)
                    {
                        if (EventManager._Open || EventManager._Started)
                        {
                            SendReply(arg, msg("You can not switch event zone whilst a game is underway"));
                            return;
                        }
                        var success = EventManager.ValidateZoneID(arg.Args[1]);
                        if (success is string)
                        {
                            SendReply(arg, (string)success);
                            return;
                        }
                        _Event.ZoneID = arg.Args[1];
                        SendReply(arg, string.Format(msg("You have set the event zone to {0}"), arg.Args[1]));
                    }
                    else SendReply(arg, string.Format(msg("Zone ID: {0}"), _Event.ZoneID));
                    return;
                case "classselector":
                    if (EventManager.EventGames[_Event.EventType].CanUseClassSelector)
                    {
                        if (_Event.UseClassSelector)
                        {
                            _Event.UseClassSelector = false;
                            SendReply(arg, msg("Disabled class selector"));
                            return;
                        }
                        else
                        {
                            _Event.UseClassSelector = true;
                            SendReply(arg, msg("Enabled class selector"));
                            return;
                        }
                    }
                    else SendReply(arg, msg("Class selector is unavailable for this event type"));
                    return;
                case "battlefield":
                    if (EventManager.EventGames[_Event.EventType].CanPlayBattlefield)
                    {
                        if (_Event.GameMode == EventManager.GameMode.Normal)
                        {
                            _Event.GameMode = EventManager.GameMode.Battlefield;
                            SendReply(arg, msg("Battlefield enabled"));
                            return;
                        }
                        else
                        {
                            _Event.GameMode = EventManager.GameMode.Normal;
                            SendReply(arg, msg("Battlefield disabled"));
                            return;
                        }
                    }
                    else SendReply(arg, msg("Battlefield is unavailable for this event type"));
                    return;
                case "enemies":
                    if (!EventManager.EventGames[_Event.EventType].SpawnsEnemies)
                    {
                        SendReply(arg, msg("This event does not require enemies"));
                        return;
                    }
                    if (arg.Args.Length == 2)
                    {
                        int enemies;
                        if (int.TryParse(arg.Args[1], out enemies))
                        {
                            _Event.EnemiesToSpawn = enemies;
                            Interface.CallHook("SetEnemyCount", _Event.EnemiesToSpawn);
                            SendReply(arg, string.Format(msg("You have successfully set the enemy count to {0}"), enemies));
                        }
                        else SendReply(arg, msg("You must enter a valid number"));
                    }
                    else SendReply(arg, string.Format(msg("Enemies to spawn: {0}"), _Event.EnemiesToSpawn));
                    return;
                case "rounds":
                    if (EventManager.EventGames[_Event.EventType].IsRoundBased)
                    {
                        SendReply(arg, msg("This event is not round based"));
                        return;
                    }
                    if (arg.Args.Length == 2)
                    {
                        int rounds;
                        if (int.TryParse(arg.Args[1], out rounds))
                        {
                            _Event.GameRounds = rounds;
                            Interface.CallHook("SetGameRounds", _Event.GameRounds);
                            SendReply(arg, string.Format(msg("You have successfully set the round limit to {0}"), rounds));
                        }
                        else SendReply(arg, msg("You must enter a valid number"));
                    }
                    else SendReply(arg, string.Format(msg("Rounds to play: {0}"), _Event.GameRounds));
                    return;               
                case "game":                    
                    if (arg.Args.Length == 2)
                    {
                        if (EventManager._Open || EventManager._Started)
                        {
                            SendReply(arg, msg("You can not change game types when a game is underway"));
                            return;
                        }
                        if (EventManager.EventGames.ContainsKey(arg.Args[1]))
                        {
                            _Event.EventType = arg.Args[1];
                            SendReply(arg, string.Format(msg("{0} has been selected as the next game type"), arg.Args[1]));
                        }
                        else SendReply(arg, string.Format(msg("{0} is not a valid event game"), arg.Args[1]));
                    }
                    else SendReply(arg, string.Format(msg("Event game: {0}"), _Event.EventType));
                    return;
            }
        }
        #endregion

        #region Authorization
        bool HasAccess(ConsoleSystem.Arg arg) => arg.Connection == null || arg.Connection?.authLevel < 1;
        bool HasPerm(BasePlayer player) => permission.UserHasPermission(player.UserIDString, "eminterface.admin") || player.IsAdmin;
        #endregion

        #region Localization
        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            {"Event Manager", "Event Manager" },
            {"Home", "Home" },
            {"Statistics", "Statistics" },
            {"Voting", "Voting" },
            {"Change Class", "Change Class" },
            {"Admin", "Admin" },
            {"Rank", "Rank" },
            {"K/D Ratio", "K/D Ratio" },
            {"Games Played", "Games Played" },
            {"Games Won", "Games Won" },
            {"Games Lost", "Games Lost" },
            {"Kills", "Kills" },
            {"Deaths" ,"Deaths" },
            {"Shots Fired" ,"Shots Fired" },
            {"Helicopters Killed" ,"Helicopters Killed" },
            {"You do not have any saved data" ,"You do not have any saved data" },
            {"Event Status:" ,"Event Status:" },
            {"Autoevent Status:" ,"Autoevent Status:" },
            {"Current Game Scores" ,"Current Game Scores" },
            {"Leave Event" ,"Leave Event" },
            {"Join Event" ,"Join Event" },
            {"Previous Game Scores" ,"Previous Game Scores" },
            {"Voting requires auto events to be launched" ,"Voting requires auto events to be launched" },
            {"Vote to open a new event" ,"Vote to open a new event" },
            {"Total Votes : {0}{1}</color>" ,"Total Votes : {0}{1}</color>" },
            {"Required Votes : {0}{1}</color>" ,"Required Votes : {0}{1}</color>" },
            {"Voted" ,"Voted" },
            {"Vote" ,"Vote" },
            {"Vote for the next event to play!" ,"Vote for the next event to play!" },
            {"Next" ,"Next" },
            {"Back" ,"Back" },
            {"There are no saved events" ,"There are no saved events" },
            {"Global Statistics" ,"Global Statistics" },
            {"Total Games Played" ,"Total Games Played" },
            {"Total Player Kills" ,"Total Player Kills" },
            {"Total Player Deaths" ,"Total Player Deaths" },
            {"Total Event Players" ,"Total Event Players" },
            {"Total Shots Fired" ,"Total Shots Fired" },
            {"Total Flags Captured" ,"Total Flags Captured" },
            {"Total Helicopters Killed" ,"Total Helicopters Killed" },
            {"Leader Board" ,"Leader Board" },
            {"ControlHelp" ,"The control menu allows you to customize and control events." },
            {"Event Status" ,"Event Status" },
            {"Open" ,"Open" },
            {"Close" ,"Close" },
            {"Start" , "Start" },
            {"End" ,"End" },
            {"Select a event config or event type to proceed" ,"Select a event config or event type to proceed" },
            {"Event Config" ,"Event Config" },
            {"Change" ,"Change" },
            {"Event Type" , "Event Type"},
            {"Gamemode" ,"Gamemode" },
            {"Normal" ,"Normal" },
            {"Battlefield" ,"Battlefield" },
            {"Score Limit" , "Score Limit" },
            {"Max Players" ,"Max Players" },
            {"Weapon Set" , "Weapon Set"},
            {"Kit" ,"Kit" },
            {"Spawnfile" ,"Spawnfile" },
            {"Second Spawnfile" ,"Second Spawnfile" },
            {"Zone ID" ,"Zone ID" },
            {"Enemies to spawn" , "Enemies to spawn"},
            {"Rounds to play" ,"Rounds to play" },
            {"Class Selector" ,"Class Selector" },
            {"Disable Item Pickup" , "Disable Item Pickup"},
            {"Close On Start" ,"Close On Start" },
            {"Respawn Type" ,"Respawn Type" },
            {"Enable" ,"Enable" },
            {"Disable" ,"Disable" },
            {"None" , "None"},
            {"Timer" ,"Timer" },
            {"Waves" ,"Waves" },
            {"Respawn Timer (seconds)" , "Respawn Timer (seconds)" },
            {"Add or remove kits from the class selector" ,"Add or remove kits from the class selector" },
            {"Available Kits" ,"Available Kits" },
            {"Available Classes" ,"Available Classes" },
            {"Unable to find any kits" ,"Unable to find any kits" },
            {"No classes have been added" ,"No classes have been added" },
            {"Manage the auto event roster and settings" ,"Manage the auto event roster and settings" },
            {"Auto-Event Settings" ,"Auto-Event Settings" },
            {"Auto-Event Status" ,"Auto-Event Status" },
            {"Next Event" ,"Next Event" },
            {"Auto Cancel" ,"Auto Cancel" },
            {"Game Interval (minutes)" ,"Game Interval (minutes)" },
            {"Auto Cancel Timer (minutes)" ,"Auto Cancel Timer (minutes)" },
            {"Increase" ,"Increase" },
            {"Decrease" ,"Decrease" },
            {"Randomize List" ,"Randomize List" },
            {"Auto-Event Creator" ,"Auto-Event Creator" },
            {"Join Timer (seconds)" ,"Join Timer (seconds)" },
            {"Start Timer (seconds)" ,"Start Timer (seconds)" },
            {"Time Limit (minutes)" , "Time Limit (minutes)"},
            {"Save Config" ,"Save Config" },
            {"Auto-Event Roster" ,"Auto-Event Roster" },
            {"No auto events have been added" ,"No auto events have been added" },
            {"CreateHelp" ,"Create a new event config by selecting your options here, start by selecting your game type" },
            {"CreateHelp2" , "Change the available options to create a new event config. When you are done click 'Save Config'"},
            {"Min Players" ,"Min Players" },
            {"Remove created event configs using the remove button" ,"Remove created event configs using the remove button" },
            {"No event configs found" ,"No event configs found" },
            {"Classes" ,"Classes" },
            {"Select a class from the options on the left" ,"Select a class from the options on the left" },
            {"Currently Selected Class:" , "Currently Selected Class:" },
            {"Class Selection" ,"Class Selection" },
            {"Kit Contents:" ,"Kit Contents:" },
            {"Select" ,"Select" },
            {"weapon set" ,"weapon set" },
            {"kit" ,"kit" },
            {"Select a {0}" ,"Select a {0}" },
            {"Unable to find any {0}s" ,"Unable to find any {0}s" },
            {"Select a event type" ,"Select a event type" },
            {"There are no registered events" ,"There are no registered events" },
            {"Select a zone" ,"Select a zone" },
            {"Unable to find any zones" ,"Unable to find any zones" },
            {"Select a spawn file" ,"Select a spawn file" },
            {"Unable to find any spawn files" ,"Unable to find any spawn files" },
            {"Select a player to kick" ,"Select a player to kick" },
            {"There is no event in progress" ,"There is no event in progress" },
            {"Select a player to force join the event" ,"Select a player to force join the event" },
            {"Select a event config from your list of preconfigured events" ,"Select a event config from your list of preconfigured events" },
            {"You do not have any saved event configs" , "You do not have any saved event configs"},
            {"You do not have any saved auto event configs" , "You do not have any saved auto event configs"},
            {"Currently Selected Config:" , "Currently Selected Config:" },
            {"Selected" ,"Selected" },
            {"   Config: {0}\n   Type: {1}\n   Spawns: {2}   {3}\n   Kit: {4}{5}\n   Zone: {6}" ,"   Config: {0}\n   Type: {1}\n   Spawns: {2}   {3}\n   Kit: {4}{5}\n   Zone: {6}" },
            {"Select a event config to use for the auto-event" ,"Select a event config to use for the auto-event" },
            {"Config:" ,"Config:" },
            {"Join Time:" ,"Join Time:" },
            {"Start Time:" ,"Start Time:" },
            {"Time Limit:" ,"Time Limit:" },
            {"Remove" ,"Remove" },
            {"Players" ,"Players" },
            {"Leaders" ,"Leaders" },
            {"Score" ,"Score" },
            {"Closed" , "Closed"},
            {" - Started" ," - Started" },
            {" - Pending" ," - Pending" },
            {" - Finished" , " - Finished"},
            {"Launched" ,"Launched" },
            {"Disabled" ,"Disabled" },
            {"You have disabled the auto cancel timer for auto events" , "You have disabled the auto cancel timer for auto events"},
            {"You have enabled the auto cancel timer for auto events" , "You have enabled the auto cancel timer for auto events"},
            {"You can not switch event config whilst a game is underway" , "You can not switch event config whilst a game is underway"},
            {"You have changed the event kit to:" ,"You have changed the event kit to:" },
            {"You can not switch the game mode whilst a game is underway" ,"You can not switch the game mode whilst a game is underway" },
            {"You can not switch event type whilst a game is underway" ,"You can not switch event type whilst a game is underway" },
            {"You can not switch event spawn file whilst a game is underway" ,"You can not switch event spawn file whilst a game is underway" },
            {"You can not switch event zone whilst a game is underway" ,"You can not switch event zone whilst a game is underway" },
            {"You can not change respawn type during a match!" ,"You can not change respawn type during a match!" },
            {"CreateSuccess" ,"You have successfully created a new event named: {0}{1}</color>\nYou can rename this event by typing in chat: {0}\n/renameevent <currentname> <newname></color>" },
            {"You have reached the class limit" ,"You have reached the class limit" },
            {"You have added the kit {0}' {1} ' </color> to the class list" ,"You have added the kit {0}' {1} ' </color> to the class list" },
            {"You have removed the kit {0}' {1} '</color> from the class list" ,"You have removed the kit {0}' {1} '</color> from the class list" },
            {"You need to select a event config" ,"You need to select a event config" },
            {"You need to set a time limit of atleast 1 minute" ,"You need to set a time limit of atleast 1 minute" },
            {"You have successfully added a new auto event" ,"You have successfully added a new auto event" },
            {"You have kicked: {0}" ,"You have kicked: {0}" },
            {"You have been kicked from the event" , "You have been kicked from the event"},
            {"You have left the event" ,"You have left the event" },
            {"You have joined the event" ,"You have joined the event" },
            {"You have forced joined: {0}" ,"You have forced joined: {0}" },
            {"You have been sent to the event" , "You have been sent to the event"},
            {"You have removed the event config: {0}{1}</color>" ,"You have removed the event config: {0}{1}</color>" },
            {"Error removing the event config: {0}{1}</color>" ,"Error removing the event config: {0}{1}</color>" },
            {"You have successfully removed the event config from the roster" ,"You have successfully removed the event config from the roster" },
            {"You have retracted your vote" ,"You have retracted your vote" },
            {"You have voted to open an event" ,"You have voted to open an event" },
            {"You have voted for Event {0}" ,"You have voted for Event {0}" },
            {"You can not rename events whilst a game is open or is being played" ,"You can not rename events whilst a game is open or is being played" },
            {"You have successfully renamed the event config '{0}' to '{1}'" ,"You have successfully renamed the event config '{0}' to '{1}'" },
            {"An event config with the name '{0}' already exists" ,"An event config with the name '{0}' already exists" },
            {"Could not find a event config with the name: {0}" ,"Could not find a event config with the name: {0}" },
            {"event open - Open a event" ,"event open - Open a event" },
            {"event cancel - Cancel a event" ,"event cancel - Cancel a event" },
            {"event start - Start a event" ,"event start - Start a event" },
            {"event close - Close a event to new entries" ,"event close - Close a event to new entries" },
            {"event end - End a event" ,"event end - End a event" },
            {"event launch - Launch auto events" ,"event launch - Launch auto events" },
            {"event battlefield - Toggle battlefield mode" ,"event battlefield - Toggle battlefield mode" },
            {"event classselector - Toggle the class selector" ,"event classselector - Toggle the class selector" },
            {"event config - Load a premade event config" ,"event config - Load a premade event config" },
            {"event config list - Display all event config names" ,"event config list - Display all event config names" },
            {"event enemies XX - Set the enemy count for this event" ,"event enemies XX - Set the enemy count for this event" },
            {"event game \"Game Name\" - Change event game" ,"event game \"Game Name\" - Change event game" },
            {"event kit \"kitname\" - Change the event kit" ,"event kit \"kitname\" - Change the event kit" },
            {"event minplayers XX - Set minimum required players (auto event)" ,"event minplayers XX - Set minimum required players" },
            {"event maxplayers XX - Set maximum players (auto event)" ,"event maxplayers XX - Set maximum players" },
            {"event rounds XX - Set the amount of rounds for this event" ,"event rounds XX - Set the amount of rounds for this event" },
            {"event spawnfile \"filename\" - Change the event spawnfile" , "event spawnfile \"filename\" - Change the event spawnfile"},
            {"event spawnfile2 \"filename\" - Change the second event spawnfile" ,"event spawnfile2 \"filename\" - Change the second event spawnfile" },
            {"event scorelimit XX - Set the event scorelimit" ,"event scorelimit XX - Set the event scorelimit" },
            {"event weaponset \"filename\" - Change the event weapon set (GunGame)" ,"event weaponset \"filename\" - Change the event weapon set (GunGame)" },
            {"event zone \"zoneID\" - Change the event zone" ,"event zone \"zoneID\" - Change the event zone" },
            {"{0} is now the next Event game." ,"{0} is now the next Event game." },
            {"You must set the event game first" ,"You must set the event game first" },
            {"The event has been cancelled" , "The event has been cancelled"},
            {"Event \"{0}\" is now open." ,"Event \"{0}\" is now open." },
            {"Event \"{0}\" has started." ,"Event \"{0}\" has started." },
            {"Event \"{0}\" is now closed for entries." ,"Event \"{0}\" is now closed for entries." },
            {"Event \"{0}\" has ended." ,"Event \"{0}\" has ended." },
            {"Minimum Players for {0} is now {1}." ,"Minimum Players for {0} is now {1}." },
            {"You must enter a valid number" ,"You must enter a valid number" },
            {"Minimum players: {0}" ,"Minimum players: {0}" },
            {"Maximum Players for {0} is now {1}." ,"Maximum Players for {0} is now {1}." },
            {"Maximum players: {0}" ,"Maximum players: {0}" },
            {"Spawnfile for {0} is now {1}.","Spawnfile for {0} is now {1}." },
            {"Spawnfile: {0}","Spawnfile: {0}" },
            {"Spawnfile 2 for {0} is now {1}.","Spawnfile 2 for {0} is now {1}." },
            {"Second Spawnfile: {0}" ,"Second Spawnfile: {0}" },
            {"The new Kit for {0} is now {1}" ,"The new Kit for {0} is now {1}" },
            {"Kit: {0}" , "Kit: {0}"},
            {"Event \"{0}\" is now launched." ,"Event \"{0}\" is now launched." },
            {"This event does not have a scoring system" ,"This event does not have a scoring system" },
            {"You have set the score limit for {0} to {1}" ,"You have set the score limit for {0} to {1}" },
            {"Only GunGame requires weapon sets" ,"Only GunGame requires weapon sets" },
            {"You can not change weapon set whilst a game is underway" ,"You can not change weapon set whilst a game is underway" },
            {"You have set the event weapon set to {0}" ,"You have set the event weapon set to {0}" },
            {"{0} is not a valid weapon set" ,"{0} is not a valid weapon set" },
            {"Unable to retrieve the weapon set list" ,"Unable to retrieve the weapon set list" },
            {"Weapon set: {0}" ,"Weapon set: {0}" },
            {"You have set the event zone to {0}" ,"You have set the event zone to {0}" },
            {"Zone ID: {0}" ,"Zone ID: {0}" },
            {"Disabled class selector" ,"Disabled class selector" },
            {"Enabled class selector" ,"Enabled class selector" },
            {"Class selector is unavailable for this event type" ,"Class selector is unavailable for this event type" },
            {"Battlefield enabled" ,"Battlefield enabled" },
            {"Battlefield disabled" , "Battlefield disabled"},
            {"Battlefield is unavailable for this event type" ,"Battlefield is unavailable for this event type" },
            {"This event does not require enemies" ,"This event does not require enemies" },
            {"You have successfully set the enemy count to {0}" ,"You have successfully set the enemy count to {0}" },
            {"Enemies to spawn: {0}" ,"Enemies to spawn: {0}" },
            { "This event is not round based" , "This event is not round based" },
            {"You have successfully set the round limit to {0}" , "You have successfully set the round limit to {0}"},
            {"Rounds to play: {0}" ,"Rounds to play: {0}" },
            {"Config List:" ,"Config List:" },
            {"No configs have been saved" ,"No configs have been saved" },
            {"{0} has been set as the current event config" ,"{0} has been set as the current event config" },
            {"{0} is not a valid event config" ,"{0} is not a valid event config" },
            {"Current event config: {0}" ,"Current event config: {0}" },
            {"You can not change game types when a game is underway" ,"You can not change game types when a game is underway" },
            {"{0} has been selected as the next game type" ,"{0} has been selected as the next game type" },
            {"{0} is not a valid event game" ,"{0} is not a valid event game" },
            {"Event game: {0}" ,"Event game: {0}" },
            {"You have changed the game mode to:", "You have changed the game mode to:" },
            {"You have changed the weapon set to:", "You have changed the weapon set to:" },
            {"You have changed the event type to:", "You have changed the event type to:" },
            {"You have changed the event spawn file to:", "You have changed the event spawn file to:" },
            {"You have changed the second spawn file to:", "You have changed the second spawn file to:" },
            {"You have changed the event zone to:", "You have changed the event zone to:" },
            {"Name", "Name" },
            {"K/D", "K/D" },
            {"Wins", "Wins" },
            {"Spawn Type", "Spawn Type" },
            {"Consecutive","Consecutive" },
            {"Random","Random" },
            {"Switch Class", "Switch Class" },
            {"endingEvent", "The event is now ending" }
        };
        #endregion
    }
}