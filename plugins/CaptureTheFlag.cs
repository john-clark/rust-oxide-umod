// Requires: EventManager

using System.Collections.Generic;
using UnityEngine;
using Rust;
using System.Linq;
using System.IO;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Globalization;
using System.Collections;

namespace Oxide.Plugins
{
    [Info("Capture the Flag", "k1lly0u", "0.1.63")]
    [Description("A capture the flag event for Event Manager")]
    class CaptureTheFlag : RustPlugin
    {
        #region Fields
        [PluginReference] EventManager EventManager;
        [PluginReference] Plugin Spawns;

        static GameObject webObject;
        static UnityWeb uWeb;

        private static readonly int playerLayer = LayerMask.GetMask("Player (Server)");

        static CaptureTheFlag ctf;

        private List<CTFPlayer> CTFPlayers;
        private Dictionary<Team, uint> flagIDs;

        private bool usingCTF;
        private bool hasStarted;
        private bool gameEnding;

        private int ACaps;
        private int BCaps;
        private int ScoreLimit;

        private string TeamASpawns;
        private string TeamBSpawns;

        private string Kit;

        private CTFFlag FlagA;
        private CTFFlag FlagB;
        #endregion

        #region Player Class
        class CTFPlayer : MonoBehaviour
        {
            public BasePlayer player;
            public Team team;
            public int caps;
            public bool hasFlag;

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                enabled = false;
                caps = 0;
                hasFlag = false;
            }
            public void ClearInventory()
            {
                for (int i = 0; i < player.inventory.containerBelt.itemList.Count; i++)
                    player.inventory.containerBelt.itemList[i].Remove(0.01f);
                for (int i = 0; i < player.inventory.containerMain.itemList.Count; i++)
                    player.inventory.containerMain.itemList[i].Remove(0.01f);
                player.SendNetworkUpdateImmediate();
            }
        }

        class CTFFlag : MonoBehaviour
        {
            private Vector3 homePos;
            private BasePlayer carrier;
            private Color color;

            public BaseEntity flag;
            public Team team;
            public FlagState flagState;

            void Awake()
            {
                gameObject.layer = (int)Layer.Reserved1;
                enabled = false;
            }
            private void OnDestroy()
            {
                CancelInvoke();

                carrier = null;
                flag.parentEntity.Set(null);
                flag.transform.localPosition = Vector3.zero;
                flag.transform.position = homePos;
                flag.UpdateNetworkGroup();
                flag.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                flagState = FlagState.Stationary;

                (flag as BaseCombatEntity).DieInstantly();
            }
            public void InitializeFlag(Team team, Vector3 spawnPoint)
            {
                gameObject.name = $"CTFFlag_{team}";

                this.team = team;
                color = HexToColor(team == Team.A ? ctf.configData.TeamA.Color : ctf.configData.TeamB.Color);

                transform.position = spawnPoint;
                homePos = spawnPoint;

                flag = GameManager.server.CreateEntity(ctf.configData.GameSettings.FlagType, spawnPoint, new Quaternion(), true);
                flag.enableSaving = false;
                if (flag.GetComponent<Signage>())
                {
                    flag.GetComponent<Signage>().textureID = ctf.flagIDs[team];
                    flag.SetFlag(BaseEntity.Flags.Locked, true, false);
                    flag.SendNetworkUpdateImmediate();
                    flag.OwnerID = 0U;
                }
                flag.Spawn();

                transform.SetParent(flag.transform);

                foreach (Collider c in flag.GetComponents<Collider>())
                    c.enabled = false;

                var innerRB = flag.gameObject.AddComponent<Rigidbody>();
                innerRB.useGravity = false;
                innerRB.isKinematic = true;

                var collider = gameObject.AddComponent<SphereCollider>();
                collider.transform.position = flag.transform.position + Vector3.up;
                collider.isTrigger = true;
                collider.radius = 1f;

                gameObject.SetActive(true);
                enabled = true;

                InvokeRepeating("ShowLocation", 0f, ctf.configData.GameSettings.FlagMarkerRefreshRate);
            }

            void OnTriggerEnter(Collider obj)
            {
                var player = obj?.GetComponentInParent<BasePlayer>();
                if (player != null)
                {
                    if (ctf.gameEnding) return;
                    if (flagState == FlagState.Stationary)
                    {
                        if (player.IsDead() || !player.IsAlive() || player.IsWounded()) return;
                        var ctfPlayer = player.GetComponent<CTFPlayer>();
                        if (ctfPlayer != null)
                        {
                            if (ctfPlayer.team != team)
                            {
                                if (IsInvoking("AutoRestore"))
                                    CancelInvoke("AutoRestore");
                                carrier = player;
                                ctfPlayer.hasFlag = true;
                                ctfPlayer.ClearInventory();
                                SetupFlag();
                                ctf.SendMessage($"{ctf.configData.Messaging.MainColor}{player.displayName}</color>{ctf.configData.Messaging.MSGColor} {msg("has taken")} </color>{ctf.configData.Messaging.MainColor}{ctf.GetTeamName(team)}'s</color>{ctf.configData.Messaging.MSGColor} {msg("flag")}!</color>");
                                return;
                            }
                            if (ctfPlayer.team == team && flag.transform.position != homePos)
                            {
                                if (IsInvoking("AutoRestore"))
                                    CancelInvoke("AutoRestore");
                                RestoreFlag(player.displayName);
                                return;
                            }
                            if (ctfPlayer.team == team && flag.transform.position == homePos && ctfPlayer.hasFlag)
                            {
                                ctfPlayer.hasFlag = false;
                                Team otherTeam = Team.NONE;
                                switch (team)
                                {
                                    case Team.A:
                                        ctf.FlagB.RestoreFlag();
                                        otherTeam = Team.B;
                                        break;
                                    case Team.B:
                                        ctf.FlagA.RestoreFlag();
                                        otherTeam = Team.A;
                                        break;
                                    default:
                                        break;
                                }
                                ctf.EventManager.GivePlayerKit(player, ctf.Kit);
                                ctfPlayer.caps++;
                                ctf.EventManager.AddStats(player, EventManager.StatType.Flags);
                                ctf.AddCapturePoint(ctfPlayer.player, team);
                                ctf.SendMessage($"{ctf.configData.Messaging.MainColor}{player.displayName}</color>{ctf.configData.Messaging.MSGColor} {msg("has captured")} </color>{ctf.configData.Messaging.MainColor}{ctf.GetTeamName(otherTeam)}'s</color>{ctf.configData.Messaging.MSGColor} {msg("flag")}!</color>");
                                return;
                            }
                        }
                    }
                }
            }
            private void ShowLocation()
            {
                //foreach (var p in ctf.CTFPlayers)
                //{
                //    p.player.SendConsoleCommand("ddraw.text", 2f, color, (carrier != null ? carrier.transform.position : flag.transform.position) + Vector3.up, $"<size=20>{ctf.GetTeamName(team)} {msg("flag")}</size>");
                //}
            }
            private void SetupFlag()
            {
                flagState = FlagState.Collected;
                flag.parentEntity.Set(carrier);
                flag.transform.localPosition = new Vector3(0, 1.5f, 0);
                flag.UpdateNetworkGroup();
                flag.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            }
            public void RestoreFlag(string name = null)
            {
                if (string.IsNullOrEmpty(name))
                    ctf.SendMessage($"{ctf.configData.Messaging.MainColor}{ctf.GetTeamName(team)}'s</color>{ctf.configData.Messaging.MSGColor} {msg("flag has been returned to base!")}</color>");
                else ctf.SendMessage($"{ctf.configData.Messaging.MainColor}{name}</color>{ctf.configData.Messaging.MSGColor} {msg("has returned")} </color>{ctf.configData.Messaging.MainColor}{ctf.GetTeamName(team)}'s</color>{ctf.configData.Messaging.MSGColor} {msg("flag to base")}!</color>");

                carrier = null;
                flag.parentEntity.Set(null);
                flag.transform.localPosition = Vector3.zero;
                flag.transform.position = homePos;
                flag.UpdateNetworkGroup();
                flag.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                flagState = FlagState.Stationary;
            }
            public void DropFlag(Vector3 position)
            {
                carrier = null;
                flag.parentEntity.Set(null);
                flag.transform.localPosition = Vector3.zero;
                flag.transform.position = position;
                flag.UpdateNetworkGroup();
                flag.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                flagState = FlagState.Stationary;
                Invoke("AutoRestore", 30f);
            }
            private void AutoRestore()
            {
                RestoreFlag();
            }
        }
        enum FlagState
        {
            Stationary,
            Collected
        }
        #endregion

        #region Oxide Hooks
        void Loaded()
        {
            hasStarted = false;
            usingCTF = false;
            CTFPlayers = new List<CTFPlayer>();
            flagIDs = new Dictionary<Team, uint>();
            lang.RegisterMessages(Messages, this);
        }

        void OnServerInitialized()
        {
            ctf = this;
            LoadVariables();
            //RegisterGame();

            ScoreLimit = configData.GameSettings.ScoreLimit;

            webObject = new GameObject("WebObject");
            uWeb = webObject.AddComponent<UnityWeb>();
            uWeb.Add(configData.TeamA.FlagImageURL, Team.A);
            uWeb.Add(configData.TeamB.FlagImageURL, Team.B);
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
                RespawnType = EventManager.RespawnType.Waves,
                RespawnTimer = 30,
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
                ScoreType = msg("Flag Captures"),
                SpawnsEnemies = false
            };
            var success = EventManager?.RegisterEventGame(Title, eventSettings, eventData);
            if (success == null)
            {
                Puts("Event plugin doesn't exist");
                return;
            }
        }
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (usingCTF && hasStarted)
            {
                if (entity is Signage && entity.GetComponent<CTFFlag>())
                {
                    hitinfo.damageTypes = new DamageTypeList();
                    hitinfo.DoHitEffects = false;
                    return;
                }
                if (entity is BasePlayer && hitinfo?.Initiator is BasePlayer)
                {
                    var victim = entity?.GetComponent<CTFPlayer>();
                    var attacker = hitinfo?.Initiator?.GetComponent<CTFPlayer>();
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
                            SendReply(attacker.player, "Friendly Fire!");
                        }
                    }
                }
            }
        }

        void Unload()
        {
            if (usingCTF && hasStarted)
                EventManager.EndEvent();

            OnEventEndPre();

            var objects = UnityEngine.Object.FindObjectsOfType<CTFPlayer>();
            if (objects != null)
                foreach (var gameObj in objects)
                    UnityEngine.Object.Destroy(gameObj);

            var flags = UnityEngine.Object.FindObjectsOfType<CTFFlag>();
            if (flags != null)
                foreach (var gameObj in flags)
                    UnityEngine.Object.Destroy(gameObj);
        }
        object CanUpdateSign(BasePlayer player, Signage sign)
        {
            if (usingCTF && hasStarted)
            {
                if (sign == FlagA?.flag || sign == FlagB?.flag)
                    return false;
            }
            return null;
        }
        #endregion

        #region UI
        #region Scoreboard
        private void UpdateScores()
        {
            if (usingCTF && hasStarted && configData.EventSettings.ShowScoreboard)
            {
                var sortedList = CTFPlayers.OrderByDescending(pair => pair.caps).ToList();
                var scoreList = new Dictionary<ulong, EventManager.Scoreboard>();
                foreach (var entry in sortedList)
                {
                    if (scoreList.ContainsKey(entry.player.userID)) continue;
                    scoreList.Add(entry.player.userID, new EventManager.Scoreboard { Name = entry.player.displayName, Position = sortedList.IndexOf(entry), Score = entry.caps });
                }
                EventManager.UpdateScoreboard(new EventManager.ScoreData { Additional = $"{ctf.GetTeamName(Team.A)} : <color={configData.TeamA.Color}>{ACaps}</color> || {ctf.GetTeamName(Team.B)} : <color={configData.TeamB.Color}>{BCaps}</color>", Scores = scoreList, ScoreType = msg("Flags") });
            }
        }
        #endregion

        #endregion

        #region CTF Functions
        void AddFlag(Team team)
        {
            string spawnFile = team == Team.A ? TeamASpawns : TeamBSpawns;

            var spawnPoint = Spawns.Call("GetSpawn", spawnFile, 0);
            if (spawnPoint is string)
            {
                PrintError((string)spawnPoint);
                return;
            }
            else
            {
                CTFFlag ctfFlag = new GameObject().gameObject.AddComponent<CTFFlag>();
                ctfFlag.InitializeFlag(team, (Vector3)spawnPoint);
                if (team == Team.A) FlagA = ctfFlag;
                else FlagB = ctfFlag;
            }
        }
        void AddCapturePoint(BasePlayer player, Team team)
        {
            EventManager.AddTokens(player.userID, configData.EventSettings.TokensOnFlagCapture, true);
            if (team == Team.A) ACaps++;
            else BCaps++;
            UpdateScores();
            CheckScores();
        }
        void RemovePlayer(CTFPlayer player)
        {
            if (player.hasFlag)
            {
                CTFFlag flag = player.team == Team.A ? FlagB : FlagA;

                flag.DropFlag(player.transform.position);
                player.player.SendNetworkUpdate();
                player.hasFlag = false;
                SendMessage($"{ctf.configData.Messaging.MainColor}{player.player.displayName}</color>{ctf.configData.Messaging.MSGColor} {msg("has dropped")} </color>{ctf.configData.Messaging.MainColor}{ctf.GetTeamName(flag.team)}'s</color>{ctf.configData.Messaging.MSGColor} {msg("flag")}!</color>");
            }
        }
        #endregion

        #region Event Manager Hooks
        void OnSelectEventGamePost(string name)
        {
            if (Title == name)
                usingCTF = true;
            else usingCTF = false;
        }
        object CanEventOpen()
        {
            if (usingCTF)
            {
                object count = Spawns.Call("GetSpawnsCount", TeamASpawns);
                if (count is int && (int)count <= 1)
                    return "Team A spawnfile does not have enough spawnpoints";
                count = Spawns.Call("GetSpawnsCount", TeamBSpawns);
                if (count is int && (int)count <= 1)
                    return "Team B spawnfile does not have enough spawnpoints";
            }
            return null;
        }
        void OnEventPlayerSpawn(BasePlayer player)
        {
            if (usingCTF && hasStarted && !gameEnding)
            {
                if (!player.GetComponent<CTFPlayer>()) return;
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
        private void OnEventKitGiven(BasePlayer player)
        {
            if (usingCTF)
            {
                GiveTeamShirts(player);
            }
        }
        object OnEventOpenPost()
        {
            if (usingCTF)
                EventManager.BroadcastEvent(msg("description"));
            return null;
        }
        object OnEventCancel()
        {
            if (usingCTF && hasStarted)
            {
                FlagA.RestoreFlag();
                FlagB.RestoreFlag();
                UnityEngine.Object.Destroy(FlagA);
                UnityEngine.Object.Destroy(FlagB);
                CheckScores(true);
            }
            return null;
        }

        void OnEventEndPre()
        {
            if (usingCTF && hasStarted)
            {
                FlagA.RestoreFlag();
                FlagB.RestoreFlag();
                UnityEngine.Object.Destroy(FlagA);
                UnityEngine.Object.Destroy(FlagB);
                CheckScores(true);
            }
        }
        object OnEventEndPost()
        {
            if (usingCTF)
            {
                hasStarted = false;

                var ctfPlayers = UnityEngine.Object.FindObjectsOfType<CTFPlayer>();
                if (ctfPlayers != null)
                {
                    foreach (var ctfplayer in ctfPlayers)
                        UnityEngine.Object.Destroy(ctfplayer);
                }
                CTFPlayers.Clear();
            }
            return null;
        }
        object OnEventStartPre()
        {
            if (usingCTF)
            {
                hasStarted = true;
                gameEnding = false;
                ACaps = 0;
                BCaps = 0;
            }
            return null;
        }
        object OnEventStartPost()
        {
            if (usingCTF)
            {
                AddFlag(Team.A);
                AddFlag(Team.B);
                UpdateScores();
            }
            return null;
        }
        object OnSelectKit(string kitname)
        {
            if (usingCTF)
            {
                Kit = kitname;
                return true;
            }
            return null;
        }
        object OnEventJoinPost(BasePlayer player)
        {
            if (usingCTF)
            {
                if (player.GetComponent<CTFPlayer>())
                    UnityEngine.Object.Destroy(player.GetComponent<CTFPlayer>());
                CTFPlayers.Add(player.gameObject.AddComponent<CTFPlayer>());
                TeamAssign(player);
                EventManager.CreateScoreboard(player);
            }
            return null;
        }
        object OnEventLeavePost(BasePlayer player)
        {
            if (usingCTF)
            {
                if (player.GetComponent<CTFPlayer>())
                {
                    RemovePlayer(player.GetComponent<CTFPlayer>());
                    CTFPlayers.Remove(player.GetComponent<CTFPlayer>());
                    UnityEngine.Object.Destroy(player.GetComponent<CTFPlayer>());
                    CheckScores();
                }
            }
            return null;
        }
        void OnEventPlayerAttack(BasePlayer attacker, HitInfo hitinfo)
        {
            if (usingCTF)
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
            if (usingCTF)
            {
                if (victim.GetComponent<CTFPlayer>())
                {
                    RemovePlayer(victim.GetComponent<CTFPlayer>());
                    var vicPlayer = victim.GetComponent<CTFPlayer>();
                    BasePlayer attacker = hitinfo?.Initiator?.ToPlayer();
                    if (attacker != null)
                    {
                        if (attacker != victim)
                        {
                            AddKill(attacker, victim);
                        }
                    }
                }
            }
            return;
        }
        object EventChooseSpawn(BasePlayer player, Vector3 destination)
        {
            if (usingCTF)
            {
                if (!CheckForTeam(player))
                {
                    TeamAssign(player);
                    return false;
                }
                Team team = player.GetComponent<CTFPlayer>().team;

                var spawnPos = EventManager.SpawnCount.GetSpawnPoint(team == Team.A ? TeamASpawns : TeamBSpawns, team == Team.A, 1);
                if (spawnPos is string)
                {
                    PrintError((string)spawnPos);
                    return null;
                }
                else return (Vector3)spawnPos;
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

        #region Teams
        enum Team
        {
            NONE,
            A,
            B
        }
        private bool CheckForTeam(BasePlayer player)
        {
            if (!player.GetComponent<CTFPlayer>())
                CTFPlayers.Add(player.gameObject.AddComponent<CTFPlayer>());
            if (player.GetComponent<CTFPlayer>().team == Team.NONE)
                return false;
            return true;
        }
        private void TeamAssign(BasePlayer player)
        {
            if (usingCTF && hasStarted)
            {
                Team team = CountForBalance();
                if (player.GetComponent<CTFPlayer>().team == Team.NONE)
                {
                    player.GetComponent<CTFPlayer>().team = team;
                    string color = team == Team.A ? configData.TeamA.Color : configData.TeamB.Color;
                    SendReply(player, string.Format(msg("teamAssign", player.UserIDString), GetTeamName(team, player.UserIDString), color));
                    //player.Respawn();
                }
            }
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
            foreach (var player in CTFPlayers)
            {
                if (player.team == team) count++;
            }
            return count;
        }
        private void GiveTeamShirts(BasePlayer player)
        {
            if (player.GetComponent<CTFPlayer>().team == Team.A)
            {
                foreach(var item in configData.TeamA.ClothingItems)
                {
                    Item clothing = ItemManager.CreateByPartialName(item.Key);
                    clothing.skin = item.Value;
                    clothing.MoveToContainer(player.inventory.containerWear);
                }
            }
            else if (player.GetComponent<CTFPlayer>().team == Team.B)
            {
                foreach (var item in configData.TeamB.ClothingItems)
                {
                    Item clothing = ItemManager.CreateByPartialName(item.Key);
                    clothing.skin = item.Value;
                    clothing.MoveToContainer(player.inventory.containerWear);
                }
            }
        }
        public static Color HexToColor(string hexColor)
        {
            if (hexColor.IndexOf('#') != -1)
                hexColor = hexColor.Replace("#", "");

            int red = 0;
            int green = 0;
            int blue = 0;

            if (hexColor.Length == 6)
            {
                red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
            }
            else if (hexColor.Length == 3)
            {
                red = int.Parse(hexColor[0].ToString() + hexColor[0].ToString(), NumberStyles.AllowHexSpecifier);
                green = int.Parse(hexColor[1].ToString() + hexColor[1].ToString(), NumberStyles.AllowHexSpecifier);
                blue = int.Parse(hexColor[2].ToString() + hexColor[2].ToString(), NumberStyles.AllowHexSpecifier);
            }

            return new Color(((float)red) / 100f, ((float)green) / 100f, ((float)blue) / 100f);
        }
        string Msg(string key, string userId) => lang.GetMessage(key, this, userId);
        void SendMessage(string message)
        {
            if (configData.EventSettings.UseUINotifications)
                EventManager.PopupMessage(message);
            else PrintToChat(message);
        }
        #endregion

        #region Scoring
        void AddKill(BasePlayer player, BasePlayer victim)
        {
            if (!player.GetComponent<CTFPlayer>())
                return;

            EventManager.AddTokens(player.userID, configData.EventSettings.TokensOnKill);
            EventManager.BroadcastEvent(string.Format(msg("killMsg"), player.displayName, victim.displayName));
        }
        void CheckScores(bool timelimit = false)
        {
            if (gameEnding) return;
            if (CTFPlayers.Count == 0)
            {
                gameEnding = true;
                EventManager.BroadcastToChat(msg("noPlayers"));
                EventManager.CloseEvent();
                EventManager.EndEvent();
                return;
            }
            if (CTFPlayers.Count == 1)
            {
                Winner(CTFPlayers[0].team);
                return;
            }
            if (timelimit)
            {
                if (ACaps > BCaps) Winner(Team.A);
                else if (BCaps > ACaps) Winner(Team.B);
                else if (ACaps == BCaps) Winner(Team.NONE);
                return;
            }
            if (EventManager._Event.GameMode == EventManager.GameMode.Battlefield)
                return;

            if (ScoreLimit > 0)
            {
                if (ACaps >= ScoreLimit)
                    Winner(Team.A);
                else if (BCaps >= ScoreLimit)
                    Winner(Team.B);
            }
        }
        void Winner(Team team)
        {
            gameEnding = true;
            foreach (var member in CTFPlayers)
            {
                if (member.hasFlag)
                {
                    CTFFlag flag = member.team == Team.A ? FlagB : FlagA;

                    flag.DropFlag(member.transform.position);
                    member.player.SendNetworkUpdate();
                    member.hasFlag = false;
                }
                if (member.team == team)
                    EventManager.AddTokens(member.player.userID, configData.EventSettings.TokensOnWin, true);
            }
            if (team == Team.NONE)
                EventManager.BroadcastToChat(msg("draw"));
            else EventManager.BroadcastToChat(string.Format(msg("winner"), GetTeamName(team)));
            timer.In(2, ()=>
            {
                EventManager.CloseEvent();
                EventManager.EndEvent();
            });
        }
        #endregion

        #region Config
        private ConfigData configData;
        class EventSettings
        {
            public string DefaultKit { get; set; }
            public string DefaultZoneID { get; set; }
            public int TokensOnKill { get; set; }
            public int TokensOnWin { get; set; }
            public int TokensOnFlagCapture { get; set; }
            public bool ShowScoreboard { get; set; }
            public bool UseUINotifications { get; set; }
        }
        class GameSettings
        {
            public float StartHealth { get; set; }
            public float FFDamageModifier { get; set; }
            public int ScoreLimit { get; set; }
            public string FlagType { get; set; }
            public float FlagMarkerRefreshRate { get; set; }
        }
        class TeamSettings
        {
            public Dictionary<string, ulong> ClothingItems { get; set; }
            public string Color { get; set; }
            public string Spawnfile { get; set; }
            public string FlagImageURL { get; set; }
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
                    DefaultKit = "ctfkit",
                    DefaultZoneID = "ctfzone",
                    TokensOnKill = 1,
                    TokensOnFlagCapture = 2,
                    TokensOnWin = 5,
                    ShowScoreboard = true,
                    UseUINotifications = true
                },
                GameSettings = new GameSettings
                {
                    StartHealth = 100,
                    FFDamageModifier = 0.25f,
                    ScoreLimit = 5,
                    FlagType = "assets/prefabs/deployable/signs/sign.post.double.prefab",
                    FlagMarkerRefreshRate = 2f
                },
                TeamA = new TeamSettings
                {
                    Color = "#33CC33",
                    ClothingItems = new Dictionary<string, ulong>
                    {
                        { "tshirt", 0 }
                    },
                    Spawnfile = "CTF_ASpawns",
                    FlagImageURL = "https://kienforcefidele.files.wordpress.com/2011/08/green-square-copy.jpg"
                },
                TeamB = new TeamSettings
                {
                    Color = "#003366",
                    ClothingItems = new Dictionary<string, ulong>
                    {
                        { "tshirt", 14177 }
                    },
                    Spawnfile = "CTF_BSpawns",
                    FlagImageURL = "http://www.polyvore.com/cgi/img-thing?.out=jpg&size=l&tid=19393880"
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

        #region Unity WWW
        class QueueItem
        {
            public string url;
            public Team team;
            public QueueItem(string ur, Team te)
            {
                url = ur;
                team = te;
            }
        }
        class UnityWeb : MonoBehaviour
        {
            CaptureTheFlag filehandler;
            const int MaxActiveLoads = 3;
            private Queue<QueueItem> QueueList = new Queue<QueueItem>();
            static byte activeLoads;

            private void Awake()
            {
                filehandler = (CaptureTheFlag)Interface.Oxide.RootPluginManager.GetPlugin(nameof(CaptureTheFlag));
            }
            private void OnDestroy()
            {
                QueueList.Clear();
                filehandler = null;
            }
            public void Add(string url, Team team)
            {
                QueueList.Enqueue(new QueueItem(url, team));
                if (activeLoads < MaxActiveLoads) Next();
            }

            void Next()
            {
                if (QueueList.Count <= 0) return;
                activeLoads++;
                StartCoroutine(WaitForRequest(QueueList.Dequeue()));
            }

            IEnumerator WaitForRequest(QueueItem info)
            {
                using (var www = new WWW(info.url))
                {
                    yield return www;
                    if (filehandler == null) yield break;
                    if (www.error != null)
                    {
                        print(string.Format("Image loading fail! Error: {0}", www.error));
                    }
                    else
                    {
                        uint textureID = FileStorage.server.Store(www.bytes, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);
                        if (!filehandler.flagIDs.ContainsKey(info.team))
                            filehandler.flagIDs.Add(info.team, textureID);
                    }
                    activeLoads--;
                    if (QueueList.Count > 0) Next();
                }
            }
        }
        #endregion

        #region Localization
        static string msg(string key, string playerId = null) => ctf.lang.GetMessage(key, ctf, playerId);
        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            {"has taken", "has taken" },
            {"has dropped", "has dropped" },
            {"has captured", "has captured" },
            {"has returned", "has returned" },
            {"flag to base", "flag to base" },
            {"Flags", "Flags" },
            {"flag", "flag" },
            {"flag has been returned to base!", "flag has been returned to base!"},
            {"Flag Captures","Flag Captures" },
            {"description", "Capture the enemys flag and return it to your base to earn points" },
            {"teamAssign", "You have been assigned to team <color={1}>{0}</color>" },
            {"TeamA", "Team A" },
            {"TeamB", "Team B" },
            {"TeamNone", "No Team" },
            {"killMsg", "{0} has killed {1}" },
            {"noPlayers", "There are no more players in the event." },
            {"draw", "It's a draw! No winners today" },
            {"winner", "{0} has won the event!" }
        };
        #endregion

    }
}
