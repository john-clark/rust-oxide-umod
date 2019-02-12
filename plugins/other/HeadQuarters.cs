// Requires: EventManager
using System.Collections.Generic;
using System;
using UnityEngine;
using Rust;
using System.Linq;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using System.Globalization;
using System.Reflection;
using Oxide.Core.Libraries;
using Oxide.Plugins;
using System.Collections;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("HeadQuarters", "Reneb", "0.0.4", ResourceId = 2211)]
    class HeadQuarters : RustPlugin
    {
        #region Fields
        [PluginReference]
        EventManager EventManager;
        [PluginReference]
        Plugin Spawns;

        public static HeadQuarters hq;

        private string HQSpawns;
        private string PlayersSpawns;

        private string Kit;

        bool usingHQ;
        bool hasStarted;

        bool _debug = true;

        public static List<HQ> HQs = new List<HQ>();
        public static Dictionary<ulong, HQPlayer> HQPlayers = new Dictionary<ulong, HQPlayer>();

        public static HQ lastDestroyedHQ;

        private static int ScoreA;
        private static int ScoreB;

        private int ScoreLimit;

        private static int playerLayer = LayerMask.GetMask(new string[] { "Player (Server)" });
        public static Vector3 Vector3Up3 = new Vector3(0f, 3f, 0f);

        #endregion

        #region Load
        void Loaded()
        {
            hasStarted = false;
            usingHQ = false;

            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Capture the headquarter to gain points, and maintain it at all cost", "Capture the headquarter to gain points, and maintain it at all cost"},
                {"Friendly Fire!","Friendly Fire!" },
                {"You must wait for your headquarters to get destroyed before respawning!","You must wait for your headquarters to get destroyed before respawning!" },
                {"You have been assigned to team <color={1}>{0}</color>","You have been assigned to team <color={1}>{0}</color>" },
                {"{0} has won the event!","{0} has won the event!" },
                {"It's a draw! No winners today","It's a draw! No winners today" },
                {"There is no more players in the event.","There is no more players in the event." }
            }, this);
        }

        void OnServerInitialized()
        {
            LoadVariables();
            RegisterGame();
            ScoreLimit = configData.GameSettings.ScoreLimit;
            hq = this;
            Unsubscribe(nameof(OnEntityTakeDamage));
        }

        void Unload()
        {
            if (usingHQ && hasStarted)
                EventManager.EndEvent();

            OnEventEndPre();

            var objects = UnityEngine.Object.FindObjectsOfType<HQPlayer>();
            if (objects != null)
                foreach (var gameObj in objects)
                    UnityEngine.Object.Destroy(gameObj);

            var flags = UnityEngine.Object.FindObjectsOfType<HQ>();
            if (flags != null)
                foreach (var gameObj in flags)
                    UnityEngine.Object.Destroy(gameObj);
        }
        #endregion

        #region GeneralMethods

        string Msg(string key, string userId) => lang.GetMessage(key, this, userId);

        #endregion

        #region EventManager Hooks
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
                MinimumPlayers = 4,
                ScoreLimit = configData.GameSettings.ScoreLimit,
                Spawnfile = configData.GameSettings.HQSpawnfile,
                Spawnfile2 = configData.GameSettings.PlayersSpawnfile,
                RespawnType = EventManager.RespawnType.Waves,
                RespawnTimer = 10,
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
                ScoreType = "HQ Captures",
                SpawnsEnemies = false
            };
            var success = EventManager.RegisterEventGame(Title, eventSettings, eventData);
            if (success == null)
            {
                Puts("Event plugin doesn't exist");
                return;
            }
        }

        void OnSelectEventGamePost(string name)
        {
            if (Title == name)
                usingHQ = true;
            else usingHQ = false;
        }
        object OnEventJoinPost(BasePlayer player)
        {
            if (usingHQ)
            {
                if (player.GetComponent<HQPlayer>())
                    UnityEngine.Object.Destroy(player.GetComponent<HQPlayer>());
                HQPlayers.Add(player.userID, player.gameObject.AddComponent<HQPlayer>());
                TeamAssign(player);
                EventManager.CreateScoreboard(player);
            }
            return null;
        }
        object OnEventLeavePost(BasePlayer player)
        {
            if (usingHQ)
            {
                if (player.GetComponent<HQPlayer>())
                {
                    HQPlayers.Remove(player.userID);
                    UnityEngine.Object.Destroy(player.GetComponent<HQPlayer>());
                    CheckScores();
                }
            }
            return null;
        }
        object OnEventCancel()
        {
            CheckScores(true);
            return null;
        }
        object CanEventOpen()
        {
            if (usingHQ)
            {
                var c = Spawns.Call("GetSpawnsCount", HQSpawns);
                if (c is string)
                {
                    return (string)c;
                }
                else
                {

                    if ((int)c < 3)
                    {
                        return "3 spawns are required for the headquarters.";
                    }
                }
            }
            return null;
        }
        object OnEventOpenPost()
        {
            if (usingHQ)
            {
                Subscribe(nameof(OnEntityTakeDamage));
                EventManager.BroadcastEvent(Msg("Capture the headquarter to gain points, and maintain it at all cost", null));
            }

            return null;
        }
        void OnEventEndPre()
        {
            if (usingHQ)
            {
                foreach (var hqe in UnityEngine.Object.FindObjectsOfType<HQ>())
                {
                    UnityEngine.Object.Destroy(hqe);
                }
                foreach (var hqplayer in UnityEngine.Object.FindObjectsOfType<HQPlayer>())
                {
                    UnityEngine.Object.Destroy(hqplayer);
                }
                HQPlayers.Clear();
                HQs.Clear();
            }
        }
        object OnEventEndPost()
        {
            if (usingHQ)
            {
                hasStarted = false;
                foreach (HQPlayer hqplayer in HQPlayers.Values)
                {
                    CuiHelper.DestroyUi(hqplayer.player, "WaitingToRespawn");
                    CuiHelper.DestroyUi(hqplayer.player, "FriendlyFire");
                }
                HQPlayers.Clear();
                Unsubscribe(nameof(OnEntityTakeDamage));
            }
            return null;
        }
        object OnEventStartPre()
        {
            if (usingHQ)
            {
                hasStarted = true;
                try
                {
                    ScoreA = 0;
                    ScoreB = 0;
                    AddHeadquarters();
                    InitializeSpawns();
                    InitializeHeadquarters();
                }
                catch (Exception e)
                {
                    PrintError(e.Message);
                    EventManager.CloseEvent();
                    EventManager.EndEvent();
                }

            }
            return null;
        }

        object OnEventStartPost()
        {
            if (usingHQ)
            {
                try
                {
                    UpdateScores();
                }
                catch (Exception e)
                {
                    PrintError(e.Message);
                }
            }
            return null;
        }

        void SetSpawnfile(bool hq, string spawnfile)
        {
            if (hq)
                HQSpawns = spawnfile;
            else PlayersSpawns = spawnfile;
        }
        object OnSelectKit(string kitname)
        {
            if (usingHQ)
            {
                Kit = kitname;
                return true;
            }
            return null;
        }
        void OnPlayerSelectClass(BasePlayer player)
        {
            if (usingHQ && hasStarted)
                GiveTeamShirts(player);
        }

        void OnEventPlayerSpawn(BasePlayer player)
        {
            if (usingHQ && hasStarted)
            {
                player.inventory.Strip();
                EventManager.GivePlayerKit(player, Kit);
                GiveTeamShirts(player);
                player.health = configData.GameSettings.StartHealth;
            }
        }

        object FreezeRespawn(BasePlayer player)
        {
            if (usingHQ && hasStarted)
            {
                if (HQPlayers.ContainsKey(player.userID))
                {
                    if (HQs.Where(x => (x.status == HQStatus.Captured || x.status == HQStatus.Destroying)).Where(w => w.team == HQPlayers[player.userID].team).ToList().Count > 0)
                    {
                        UIWaitingToRespawn(player);
                        return true;
                    }
                }
            }
            return null;
        }

        object EventChooseSpawn(BasePlayer player, Vector3 initialPos)
        {
            if (usingHQ && hasStarted)
            {
                if (HQPlayers.ContainsKey(player.userID))
                {
                    return HQs.Where(x => x.status == HQStatus.Spawnable && x.team == HQPlayers[player.userID].team).ElementAt(0).GetRandomSpawn();
                }
            }
            return null;
        }


        #endregion

        #region UI
        public class UI
        {
            static public CuiElementContainer CreateElementContainer(string parent, string panelName, string color, string aMin, string aMax, bool useCursor)
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
            static public void CreatePanel(ref CuiElementContainer container, string panel, string color, string aMin, string aMax, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    CursorEnabled = cursor
                },
                panel);
            }
            static public void CreateImage(ref CuiElementContainer container, string panel, string url, string name, string aMin, string aMax, bool cursor = false)
            {
                container.Add(new CuiElement
                {
                    Name = name,
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Url = url
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = aMin,
                            AnchorMax = aMax
                        }
                    }
                });
            }
            static public void CreateLabel(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);

            }
            static public void CreateButton(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 1.0f },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel);
            }
        }

        void UIFriendlyFire(BasePlayer attacker)
        {
            var ff_container = UI.CreateElementContainer("Overlay", "FriendlyFire", "1 0 0 0.2", "0 0", "1 1", false);
            UI.CreateLabel(ref ff_container, "FriendlyFire", "1 1 1 1", Msg("Friendly Fire!", attacker.UserIDString), 30, "0.4 0.1", "0.6 0.3");
            CuiHelper.AddUi(attacker, ff_container);
            timer.Once(0.5f, () => CuiHelper.DestroyUi(attacker, "FriendlyFire"));
        }
        void UIWaitingToRespawn(BasePlayer attacker)
        {
            var ff_container = UI.CreateElementContainer("Overlay", "WaitingToRespawn", "0.1 0.1 0.1 0.2", "0 0", "1 1", false);
            UI.CreateLabel(ref ff_container, "WaitingToRespawn", "1 1 1 1", Msg("You must wait for your headquarters to get destroyed before respawning!", attacker.UserIDString), 24, "0.2 0.1", "0.8 0.5");
            CuiHelper.AddUi(attacker, ff_container);
        }

        #endregion

        #region OxideHooks
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (usingHQ && hasStarted)
            {
                if (entity is Signage && entity.GetComponent<HQ>() != null)
                {
                    hitinfo.damageTypes = new DamageTypeList();
                    hitinfo.DoHitEffects = false;
                    return;
                }
                if (entity is BasePlayer && hitinfo?.Initiator is BasePlayer)
                {
                    var victim = entity.GetComponent<HQPlayer>();
                    var attacker = hitinfo.Initiator.GetComponent<HQPlayer>();
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
                            UIFriendlyFire(attacker.player);
                        }
                    }
                }
            }
        }



        #endregion

        #region Teams
        public enum Team
        {
            NONE,
            A,
            B
        }

        enum Colors
        {
            Black,
            White,
            Green,
            Blue,
            Red,
            Yellow,
            Pink
        }

        class ColorsClass
        {
            public string sign;
            public ulong skinID;
            public string Attire;
            public string hex;
            public string rgba;

            public ColorsClass() { }
        }

        Dictionary<Colors, ColorsClass> TeamColors = new Dictionary<Colors, ColorsClass>
        {
            {
                Colors.Black, new ColorsClass
                {
                    sign = "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",
                    Attire = "tshirt",
                    skinID = 10003L,
                    hex = "#000000",
                    rgba = "1,1,1,0.9"
                }
            },
            {
                Colors.Blue, new ColorsClass
                {
                    sign = "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ADLmoAsCJQICoIRdUQIZAgIgYw66IFAiIABK2BUlkCHwrWcbP37+eut5fMcmQOCcgCuAc372JjC1gACYenyaJ3BOQACc87M3gakFBMDU49M8gXMC38/tbm8CBCYSeHt9eb6607/6psNCvArQAdUhCRwUuHnVr3cA3BQ82LjdCBDoINA7ADq07JAECLQSEACtJB2HwIQCAmDCoWmZQCsBAdBK0nEI5AvcPCkvAPKHpkMCrQRunpQXAK1oHYfAhAICYMKhaZlAKwHvBGwl6TgEBgv8966+m0v6vS24AtgrZnsCCwkIgIWGaSkE9goIgL1itiewkIAAWGiYlkJgr4AA2CtmewILCQiAhYZpKQT2CgiAvWK2J7CQgABYaJiWQmCvgADYK2Z7AgsJnH4n0RYL/0HIFqVh29z8u3BfVTa/r4Rqft/inYBDAqCG51jVRzjZW5w4x3TH77XyPFvMUQBsOCdXO4lanDgb2KbaZMYZt5ijADhwms54slwus8WJc3k8X88rIAAazi4pGNzIGw7WoQgQIECAAAECBAgQIECAAAECBAgQIECAAAECBAgQIECAAAECBAgQIECAAAECBAgQIECAAAECBAgQIECAAAECBAgQIECAAAECBAgQIECAAAECBAgQIECAAAECBAgQIECAAAECBAgQIECAAAECBAgQIECAAAECBAgQIECAAAECBAgQIECAAAECBAgQIECAAAECBAgQIECAAIEHE/gXNNGSWtPdjMIAAAAASUVORK5CYII=",
                    Attire = "tshirt",
                    skinID = 14177L,
                    hex = "#2E64FE",
                    rgba = "0,0,1,0.9"
                }
            },
            {
                Colors.Green, new ColorsClass
                {
                    sign = "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",
                    Attire = "tshirt",
                    skinID = 0L,
                    hex = "#74DF00",
                    rgba = "0,1,0,0.9"
                }
            },
            {
                Colors.Red, new ColorsClass
                {
                    sign = "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",
                    Attire = "tshirt",
                    skinID = 101L,
                    hex = "#FF0000",
                    rgba = "1,0,0,0.9"
                }
            },
            {
                Colors.White, new ColorsClass
                {
                    Attire = "tshirt",
                    skinID = 10039L,
                    sign = "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",
                    hex = "#FFFFFF",
                    rgba = "0,0,0,0.9"
                }
            },
            {
                Colors.Yellow, new ColorsClass
                {
                    Attire = "tshirt",
                    skinID = 10053L,
                    sign = "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",
                    hex = "#FFFF00",
                    rgba = "1,1,0,0.9"
                }
            },
            {
                Colors.Pink, new ColorsClass
                {
                    Attire = "tshirt",
                    skinID = 10046L,
                    sign = "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",
                    hex = "#FF00FF",
                    rgba = "1,0.1,1,0.9"
                }
            }
        };

        private void TeamAssign(BasePlayer player)
        {
            if (usingHQ && hasStarted)
            {
                Team team = CountForBalance();
                if (player.GetComponent<HQPlayer>().team == Team.NONE)
                {
                    player.GetComponent<HQPlayer>().team = team;
                    string color = string.Empty;
                    if (team == Team.A) color = TeamColors[((Colors)configData.TeamA.Color)].hex;
                    else if (team == Team.B) color = TeamColors[((Colors)configData.TeamB.Color)].hex;
                    SendReply(player, string.Format(Msg("You have been assigned to team <color={1}>{0}</color>", player.UserIDString), GetTeamName(team, player.UserIDString), color));
                    player.Respawn();
                }
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
        private string GetTeamName(Team team, string id = null)
        {
            switch (team)
            {
                case Team.A:
                    return Msg("TeamA", id);
                case Team.B:
                    return Msg("TeamB", id);
                default:
                    return Msg("TeamNone", id);
            }
        }
        private int Count(Team team)
        {
            return HQPlayers.Where(x => x.Value.team == team).Count();
        }
        private void GiveTeamShirts(BasePlayer player)
        {
            if (player.GetComponent<HQPlayer>().team == Team.A)
            {
                Item shirt = ItemManager.CreateByPartialName(TeamColors[((Colors)configData.TeamA.Color)].Attire);
                shirt.skin = TeamColors[((Colors)configData.TeamA.Color)].skinID;
                shirt.MoveToContainer(player.inventory.containerWear);
            }
            else if (player.GetComponent<HQPlayer>().team == Team.B)
            {
                Item shirt = ItemManager.CreateByPartialName(TeamColors[(Colors)configData.TeamB.Color].Attire);
                shirt.skin = TeamColors[(Colors)configData.TeamB.Color].skinID;
                shirt.MoveToContainer(player.inventory.containerWear);
            }
        }

        #endregion

        #region Config        
        private ConfigData configData;
        class EventSettings
        {
            public string DefaultKit { get; set; }
            public string DefaultZoneID { get; set; }
            public int TokensOnWin { get; set; }
            public int TokensOnCapture { get; set; }
            public int TokensOnDestroy { get; set; }
            public int ScoreOnHQCapture { get; set; }
            public int ScoreOnHQDestroy { get; set; }
            public int ScoreOnHQDefendPerSec { get; set; }
        }
        class GameSettings
        {
            public float StartHealth { get; set; }
            public float FFDamageModifier { get; set; }
            public int ScoreLimit { get; set; }
            public string HQSpawnfile { get; set; }
            public string PlayersSpawnfile { get; set; }
        }
        class TeamSettings
        {
            public int Color { get; set; }
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
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                EventSettings = new EventSettings
                {
                    DefaultKit = "hqkit",
                    DefaultZoneID = "hqzone",
                    TokensOnWin = 20,
                    TokensOnCapture = 3,
                    TokensOnDestroy = 2,
                    ScoreOnHQCapture = 20,
                    ScoreOnHQDestroy = 15,
                    ScoreOnHQDefendPerSec = 1
                },
                GameSettings = new GameSettings
                {
                    StartHealth = 100,
                    FFDamageModifier = 0.25f,
                    ScoreLimit = 300,
                },
                TeamA = new TeamSettings
                {
                    Color = 2,
                },
                TeamB = new TeamSettings
                {
                    Color = 0,
                },
                Messaging = new Messaging
                {
                    MainColor = "<color=orange>",
                    MSGColor = "<color=#939393>"
                }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Scoreboard        
        private void UpdateScores()
        {
            if (usingHQ && hasStarted)
            {
                var sortedList = HQPlayers.OrderByDescending(pair => pair.Value.score).Select(w => w.Value).ToList();
                var scoreList = new Dictionary<ulong, EventManager.Scoreboard>();
                foreach (var entry in sortedList)
                    scoreList.Add(entry.player.userID, new EventManager.Scoreboard { Name = entry.player.displayName, Position = sortedList.IndexOf(entry), Score = entry.score });
                EventManager.UpdateScoreboard(new EventManager.ScoreData { Additional = $"Team A : <color={TeamColors[((Colors)configData.TeamA.Color)].hex}>{ScoreA}</color> || Team B : <color={TeamColors[((Colors)configData.TeamB.Color)].hex}>{ScoreB}</color>", Scores = scoreList, ScoreType = "Score" });
            }
        }
        #endregion

        #region PlayerClass
        public class HQPlayer : MonoBehaviour
        {
            public BasePlayer player;
            public Team team;
            public List<EventManager.EventInvItem> HQItems = new List<EventManager.EventInvItem>();
            public int score;

            void Awake()
            {
                team = Team.NONE;
                player = GetComponent<BasePlayer>();
                score = 0;
                enabled = false;
            }
        }
        #endregion

        #region HQ

        public enum HQStatus
        {
            Spawnable,
            Capturing,
            Captured,
            Contested,
            Destroying,
            Destroyed
        }

        public class HQ : MonoBehaviour
        {

            public HQStatus status;
            public HQStatus lastStatus;
            public Team team;
            string color;
            BaseEntity flag;
            public Vector3 location;
            public List<Vector3> spawns;
            public float size;
            public int timeToCapture;
            private Collider[] results;
            public List<HQPlayer> capturingPlayers;
            public int autoDestroy;

            public HQ() { }

            public Vector3 GetRandomSpawn()
            {
                return spawns[Oxide.Core.Random.Range(0, spawns.Count - 1)];
            }

            public void SetStatus(HQStatus status)
            {
                lastStatus = this.status;
                this.status = status;
                enabled = true;

                if (status == HQStatus.Contested)
                {
                    team = Team.NONE;
                    if (!IsInvoking("UpdateTrigger")) InvokeRepeating("UpdateTrigger", 0f, 0.5f);
                    if (!IsInvoking("Think")) InvokeRepeating("Think", 0f, 1f);
                    SetCaptureTime(15);
                    color = "0.9,0.9,0.9,0.9";

                    foreach (var p in HQPlayers.Select(x => x.Value))
                    {
                        p.player.SendConsoleCommand("ddraw.text", 1f, color, location + Vector3Up3, $"<size=30>Capture the objective</size>");
                    }
                }
                else if (status == HQStatus.Capturing)
                {
                    color = hq.TeamColors[(team == Team.A ? (Colors)hq.configData.TeamA.Color : (Colors)hq.configData.TeamB.Color)].rgba;
                    foreach (var p in HQPlayers.Select(x => x.Value))
                    {
                        p.player.SendConsoleCommand("ddraw.text", 1f, color, location + Vector3Up3, $"<size=20>{hq.GetTeamName(team)} Capturing ... </size>{timeToCapture.ToString()}s");
                    }
                }
                else if (status == HQStatus.Captured)
                {
                    if (lastStatus == HQStatus.Capturing)
                    {
                        Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/rocket_explosion.prefab", location);
                        hq.EventManager.PopupMessage($"<color={hq.TeamColors[(team == Team.A ? (Colors)hq.configData.TeamA.Color : (Colors)hq.configData.TeamB.Color)].hex}>{hq.GetTeamName(team)}</color> captured the head quarters</color>");
                        timeToCapture = 5;
                        autoDestroy = 45;
                        if (team == Team.A) ScoreA += hq.configData.EventSettings.ScoreOnHQCapture;
                        else ScoreB += hq.configData.EventSettings.ScoreOnHQCapture;
                        hq.GivePoints(capturingPlayers, hq.configData.EventSettings.TokensOnCapture);
                        hq.UpdateScores();
                    }
                    else
                    {
                        if (team == Team.A) ScoreA += hq.configData.EventSettings.ScoreOnHQDefendPerSec;
                        else ScoreB += hq.configData.EventSettings.ScoreOnHQDefendPerSec;
                        hq.UpdateScores();
                    }
                    foreach (var p in HQPlayers.Select(x => x.Value))
                    {
                        p.player.SendConsoleCommand("ddraw.text", 1f, color, location + Vector3Up3, $"<size=20>{hq.GetTeamName(team)} Captured</size>");
                    }

                }
                else if (status == HQStatus.Destroying)
                {
                    if (team == Team.A) ScoreA += hq.configData.EventSettings.ScoreOnHQDefendPerSec;
                    else ScoreB += hq.configData.EventSettings.ScoreOnHQDefendPerSec;
                    hq.UpdateScores();
                    foreach (var p in HQPlayers.Select(x => x.Value))
                    {
                        p.player.SendConsoleCommand("ddraw.text", 1f, color, location + Vector3Up3, $"<size=20>{hq.GetTeamName(team == Team.A ? Team.B : Team.A)} Destroying ...</size>{timeToCapture.ToString()}s");
                    }
                }
                else if (status == HQStatus.Destroyed)
                {
                    if (lastStatus == HQStatus.Destroying)
                    {
                        Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/rocket_explosion.prefab", location);
                        hq.EventManager.PopupMessage($"<color={hq.TeamColors[(team == Team.A ? (Colors)hq.configData.TeamB.Color : (Colors)hq.configData.TeamA.Color)].hex}> {hq.GetTeamName(team == Team.A ? Team.B : Team.A)} destroyed the head quarters</color>");
                        if (team == Team.A) ScoreB += hq.configData.EventSettings.ScoreOnHQDestroy;
                        else ScoreA += hq.configData.EventSettings.ScoreOnHQDestroy;
                        hq.GivePoints(capturingPlayers, hq.configData.EventSettings.TokensOnDestroy);
                        hq.UpdateScores();
                    }
                    lastDestroyedHQ = this;
                    hq.LoopHQ();
                }
                else if (status == HQStatus.Spawnable)
                {
                    if (IsInvoking("UpdateTrigger")) CancelInvoke("UpdateTrigger");
                    if (IsInvoking("Think")) CancelInvoke("Think");
                    team = Team.NONE;
                    enabled = false;
                    UpdateSign(flag.GetComponent<Signage>());
                }
            }

            void UpdateTrigger()
            {
                capturingPlayers.Clear();
                foreach (Collider col in Physics.OverlapSphere(location, size, playerLayer))
                {
                    BasePlayer player = col.gameObject.ToBaseEntity() as BasePlayer;
                    if (player != null)
                    {
                        HQPlayer hqplayer = player.GetComponent<HQPlayer>();
                        if (hqplayer != null && player.IsInvoking("InventoryUpdate"))
                        {
                            if (!capturingPlayers.Contains(hqplayer))
                                capturingPlayers.Add(hqplayer);
                        }
                    }
                }
            }

            void SetCapturing(Team team)
            {
                if (status == HQStatus.Captured || status == HQStatus.Destroying)
                {
                    if (this.team != team)
                    {
                        SetCaptureTime(timeToCapture - 1);
                        if (timeToCapture <= 0)
                        {
                            SetStatus(HQStatus.Destroyed);
                        }
                        else SetStatus(HQStatus.Destroying);
                    }
                    else
                    {
                        SetStatus(HQStatus.Captured);
                    }
                }
                else
                {
                    if (this.team != team)
                    {
                        SetCaptureTime(15);
                        this.team = team;
                        color = hq.TeamColors[team == Team.A ? ((Colors)hq.configData.TeamA.Color) : ((Colors)hq.configData.TeamB.Color)].hex;
                    }
                    SetCaptureTime(timeToCapture - 1);
                    if (timeToCapture <= 0)
                    {
                        SetStatus(HQStatus.Captured);
                        UpdateSign(flag.GetComponent<Signage>(), hq.TeamColors[team == Team.A ? ((Colors)hq.configData.TeamA.Color) : ((Colors)hq.configData.TeamB.Color)].sign);
                    }
                    else
                    {
                        SetStatus(HQStatus.Capturing);
                    }
                }
            }

            void SetCaptureTime(int timeleft)
            {
                this.timeToCapture = timeleft;

            }

            void Think()
            {
                if (status == HQStatus.Destroying || status == HQStatus.Captured)
                {
                    autoDestroy--;
                    if (autoDestroy <= 0 && timeToCapture != 0)
                    {
                        lastDestroyedHQ = this;
                        hq.LoopHQ();
                        return;
                    }
                }

                int teamA = capturingPlayers.Where(x => x.team == Team.A).Count();
                int teamB = capturingPlayers.Where(x => x.team == Team.B).Count();
                if (teamA != 0 && teamB != 0)
                {
                    SetStatus(status);
                }
                else if (teamA == 0 && teamB == 0)
                {
                    if (status == HQStatus.Capturing)
                    {
                        if (timeToCapture < 15)
                        {
                            SetCaptureTime(timeToCapture + 1);
                            SetStatus(HQStatus.Capturing);
                        }
                        else
                        {
                            SetStatus(HQStatus.Contested);
                        }
                    }
                    else if (status == HQStatus.Destroying)
                    {
                        if (timeToCapture < 5)
                        {
                            SetCaptureTime(timeToCapture + 1);
                            SetStatus(HQStatus.Destroying);
                        }
                        else
                        {
                            SetStatus(HQStatus.Captured);
                        }
                    }
                    else
                    {
                        SetStatus(status);
                    }
                }
                else
                {
                    SetCapturing(teamA > teamB ? Team.A : Team.B);
                }
            }

            void Awake()
            {
                flag = GetComponent<BaseEntity>();
                status = HQStatus.Spawnable;
                lastStatus = HQStatus.Spawnable;
                spawns = new List<Vector3>();
                location = transform.position;
                capturingPlayers = new List<HQPlayer>();
                size = 3f;
                enabled = false;
                timeToCapture = 15;
            }

            private void OnDestroy()
            {
                flag.KillMessage();
            }
        }
        static void UpdateSign(Signage sign, string hex = "iVBORw0KGgoAAAANSUhEUgAAAIAAAABACAYAAADS1n9/AAAA/klEQVR4Ae3UQQ0AIADDQMC/AP4IhQQb1zlo02zuc+5orIHFkgf+DRQAHkIBFABuAMfvAQoAN4Dj9wAFgBvA8XuAAsAN4Pg9QAHgBnD8HqAAcAM4fg9QALgBHL8HKADcAI7fAxQAbgDH7wEKADeA4/cABYAbwPF7gALADeD4PUAB4AZw/B6gAHADOH4PUAC4ARy/BygA3ACO3wMUAG4Ax+8BCgA3gOP3AAWAG8Dxe4ACwA3g+D1AAeAGcPweoABwAzh+D1AAuAEcvwcoANwAjt8DFABuAMfvAQoAN4Dj9wAFgBvA8XuAAsAN4Pg9QAHgBnD8HqAAcAM4fg+AB/AA1x4DXYHNDIoAAAAASUVORK5CYII=")
        {
            if (sign != null)
            {
                var stream = new MemoryStream();
                var stringSign = Convert.FromBase64String(hex);
                stream.Write(stringSign, 0, stringSign.Length);
                sign.textureID = FileStorage.server.Store(stream, FileStorage.Type.png, sign.net.ID);
                stream.Position = 0;
                stream.SetLength(0);

                sign.SetFlag(BaseEntity.Flags.Locked, true);
                sign.SendNetworkUpdate();
            }

        }

        void InitializeSpawns()
        {
            var c = Spawns.Call("GetSpawnsCount", PlayersSpawns);
            if (c is string)
            {
                PrintError((string)c);
                return;
            }
            else
            {
                for (int i = 0; i < (int)c; i++)
                {
                    Vector3 s = (Vector3)(Spawns.Call("GetSpawn", PlayersSpawns, i));
                    HQ thq = HQs[0];
                    float d = Vector3.Distance(s, HQs[0].location);
                    for (int o = 1; o < HQs.Count; o++)
                    {
                        if (Vector3.Distance(s, HQs[o].location) < d)
                        {

                            d = Vector3.Distance(s, HQs[o].location);
                            thq = HQs[o];
                        }
                    }
                    thq.spawns.Add(s);
                }
            }
            if (_debug)
            {
                for (int i = 0; i < HQs.Count; i++)
                {
                    Interface.Oxide.LogWarning(string.Format("{0} HQ has {1} spawns", i.ToString(), HQs[i].spawns.Count.ToString()));
                }
            }
        }

        void AddHeadquarters()
        {
            var c = Spawns.Call("GetSpawnsCount", HQSpawns);
            if (c is string)
            {
                PrintError((string)c);
                return;
            }
            else
            {
                int count = (int)c;
                for (int i = 0; i < count; i++)
                {
                    BaseEntity flag = GameManager.server.CreateEntity("assets/prefabs/deployable/signs/sign.post.double.prefab", (Vector3)(Spawns.Call("GetSpawn", HQSpawns, i)), new Quaternion(), true);
                    flag.Spawn();
                    flag.enableSaving = false;
                    if (flag.GetComponent<Signage>())
                    {
                        UpdateSign(flag.GetComponent<Signage>());
                        flag.OwnerID = 0U;
                    }
                    HQs.Add(flag.gameObject.AddComponent<HQ>());
                }
            }
        }

        public void LoopHQ()
        {

            var hqes = HQs.Where(w => w != lastDestroyedHQ).ToList();
            SetNextHQ(hqes[Oxide.Core.Random.Range(0, hqes.Count - 1)]);

            if (lastDestroyedHQ != null)
            {
                foreach (var player in HQPlayers.Where(b => !b.Value.player.IsInvoking("InventoryUpdate")).Select(w => w.Value))
                {
                    CuiHelper.DestroyUi(player.player, "WaitingToRespawn");
                    Interface.Oxide.CallHook("DeathTimerUI", player.player, 1, false);
                }
            }

            var hqeteamA = HQs.Where(w => w.status == HQStatus.Spawnable).ToList();
            int randA = Oxide.Core.Random.Range(0, hqeteamA.Count - 1);
            SetNextTeamSpawns(Team.A, hqeteamA[randA]);

            hqeteamA.RemoveAt(randA);
            SetNextTeamSpawns(Team.B, hqeteamA[Oxide.Core.Random.Range(0, hqeteamA.Count - 1)]);

            CheckScores();
        }

        void InitializeHeadquarters()
        {
            LoopHQ();
        }

        public static void SetNextHQ(HQ nextHQ)
        {
            for (int i = 0; i < HQs.Count; i++)
            {
                HQs[i].SetStatus(nextHQ != HQs[i] ? HQStatus.Spawnable : HQStatus.Contested);
            }
        }

        public static void SetNextTeamSpawns(Team team, HQ HQe)
        {
            HQe.team = team;
        }

        #endregion

        #region Scoring

        void GivePoints(List<HQPlayer> players, int points)
        {
            foreach (var p in players)
            {
                p.score += points;
            }
        }
        void CheckScores(bool timelimit = false)
        {
            if (HQPlayers.Count == 0)
            {
                EventManager.BroadcastToChat(Msg("There is no more players in the event.", null));
                EventManager.CloseEvent();
                EventManager.EndEvent();
                return;
            }
            if (HQPlayers.Count == 1)
            {
                Winner(HQPlayers.Select(x => x.Value).ToList()[0].team);
                return;
            }
            if (timelimit)
            {
                if (ScoreA > ScoreB) Winner(Team.A);
                if (ScoreB > ScoreA) Winner(Team.B);
                if (ScoreA == ScoreB) Winner(Team.NONE);
                return;
            }
            if (EventManager._Event.GameMode == EventManager.GameMode.Battlefield)
                return;

            if (ScoreLimit > 0)
            {
                if (ScoreA >= ScoreLimit)
                    Winner(Team.A);
                if (ScoreB >= ScoreLimit)
                    Winner(Team.B);
            }
        }
        void Winner(Team team)
        {
            foreach (var member in HQPlayers.Select(x => x.Value).Where(w => w.team == team))
            {
                EventManager.AddTokens(member.player.userID, configData.EventSettings.TokensOnWin, true);
            }
            foreach (var member in HQPlayers.Select(x => x.Value))
            {
                EventManager.AddTokens(member.player.userID, member.score, false);
            }
            if (team == Team.NONE)
                EventManager.BroadcastToChat(Msg("It's a draw! No winners today", null));
            else EventManager.BroadcastToChat(string.Format(Msg("{0} has won the event!", null), GetTeamName(team)));
            EventManager.CloseEvent();
            EventManager.EndEvent();
        }
        #endregion
    }
}
