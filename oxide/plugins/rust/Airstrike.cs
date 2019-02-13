using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;
using System.Reflection;

namespace Oxide.Plugins
{
    [Info("Airstrike", "k1lly0u", "0.3.5", ResourceId = 1489)]
    [Description("Calls an airstrike using a supply signal or on chat command")]
    class Airstrike : RustPlugin
    {
        #region Fields
        [PluginReference]
        private Plugin Economics, ServerRewards;

        private StoredData storedData;
        private DynamicConfigFile data;

        private Dictionary<ulong, StrikeType> toggleList = new Dictionary<ulong, StrikeType>();

        private Dictionary<string, int> shortnameToId = new Dictionary<string, int>();
        private Dictionary<string, string> shortnameToDn = new Dictionary<string, string>();

        private static Airstrike ins;

        const string cargoPlanePrefab = "assets/prefabs/npc/cargo plane/cargo_plane.prefab";
        const string basicRocket = "assets/prefabs/ammo/rocket/rocket_basic.prefab";
        const string fireRocket = "assets/prefabs/ammo/rocket/rocket_fire.prefab";

        enum StrikeType { Strike, Squad }
        #endregion

        #region Oxide Hooks
        void Loaded()
        {
            data = Interface.Oxide.DataFileSystem.GetFile("airstrike_data");

            lang.RegisterMessages(Messages, this);
            permission.RegisterPermission("airstrike.signal.strike", this);
            permission.RegisterPermission("airstrike.signal.squad", this);
            permission.RegisterPermission("airstrike.purchase.strike", this);
            permission.RegisterPermission("airstrike.purchase.squad", this);
            permission.RegisterPermission("airstrike.chat.strike", this);
            permission.RegisterPermission("airstrike.chat.squad", this);
            permission.RegisterPermission("airstrike.ignorecooldown", this);
        }

        void OnServerInitialized()
        {
            ins = this;
            LoadVariables();
            LoadData();

            shortnameToId = ItemManager.itemList.ToDictionary(x => x.shortname, y => y.itemid);
            shortnameToDn = ItemManager.itemList.ToDictionary(x => x.shortname, y => y.displayName.translated);

            CallRandomStrike();
        }

        void Unload()
        {
            ins = null;
            SaveData();

            var objects = UnityEngine.Object.FindObjectsOfType<StrikePlane>();
            if (objects != null)
            {
                foreach (var obj in objects)
                    UnityEngine.Object.Destroy(obj);
            }
        }

        void OnServerSave() => SaveData();

        void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (toggleList.ContainsKey(player.userID) && entity is SupplySignal)
            {
                StrikeType type = toggleList[player.userID];
                toggleList.Remove(player.userID);
                AddCooldownData(player, type);

                entity.CancelInvoke((entity as SupplySignal).Explode);
                entity.Invoke(entity.KillMessage, 30f);
                timer.Once(3, () =>
                {
                    Effect.server.Run("assets/bundled/prefabs/fx/smoke_signal_full.prefab", entity, 0, new Vector3(), new Vector3());
                    Vector3 pos = entity.transform.position;
                    switch (type)
                    {
                        case StrikeType.Strike:
                            SendReply(player, string.Format(msg("strikeConfirmed", player.UserIDString), pos));
                            CallStrike(pos);
                            return;
                        case StrikeType.Squad:
                            SendReply(player, string.Format(msg("strikeConfirmed", player.UserIDString), pos));
                            CallSquad(pos);
                            return;
                    }
                });
            }
        }
        #endregion

        #region Plane Control
        class StrikePlane : MonoBehaviour
        {
            private CargoPlane entity;
            private Vector3 targetPos;

            private RocketOptions rocketOptions;

            private Vector3 startPos;
            private Vector3 endPos;
            private float secondsToTake;

            private int rocketsFired;
            private float fireDistance;
            private bool isFiring;

            private void Awake()
            {
                entity = GetComponent<CargoPlane>();
                rocketOptions = ins.configData.Rocket;
                fireDistance = ins.configData.Plane.Distance;

                entity.dropped = true;
                enabled = false;
            }
            private void Update()
            {
                if (!isFiring && Vector3.Distance(transform.position, targetPos) <= fireDistance)
                {
                    isFiring = true;
                    FireRocketLoop();
                }
            }
            private void OnDestroy()
            {
                entity.CancelInvoke(LaunchRocket);
            }

            public void InitializeFlightPath(Vector3 targetPos)
            {
                this.targetPos = targetPos;

                float size = TerrainMeta.Size.x;
                float highestPoint = 170f;

                startPos = Vector3Ex.Range(-1f, 1f);
                startPos.y = 0f;
                startPos.Normalize();
                startPos = startPos * (size * 2f);
                startPos.y = highestPoint;

                endPos = startPos * -1f;
                endPos.y = startPos.y;
                startPos = startPos + targetPos;
                endPos = endPos + targetPos;

                secondsToTake = (Vector3.Distance(startPos, endPos) / ins.configData.Plane.Speed) * UnityEngine.Random.Range(0.95f, 1.05f);

                entity.transform.position = startPos;
                entity.transform.rotation = Quaternion.LookRotation(endPos - startPos);

                entity.startPos = startPos;
                entity.endPos = endPos;
                entity.dropPosition = targetPos;
                entity.secondsToTake = secondsToTake;

                enabled = true;
            }
            public void GetFlightData(out Vector3 startPos, out Vector3 endPos, out float secondsToTake)
            {
                startPos = this.startPos;
                endPos = this.endPos;
                secondsToTake = this.secondsToTake;
            }
            public void SetFlightData(Vector3 startPos, Vector3 endPos, Vector3 targetPos, float secondsToTake)
            {
                this.startPos = startPos;
                this.endPos = endPos;
                this.targetPos = targetPos;
                this.secondsToTake = secondsToTake;

                entity.transform.position = startPos;
                entity.transform.rotation = Quaternion.LookRotation(endPos - startPos);

                entity.startPos = startPos;
                entity.endPos = endPos;
                entity.dropPosition = targetPos;
                entity.secondsToTake = secondsToTake;

                enabled = true;
            }

            private void FireRocketLoop()
            {
                entity.InvokeRepeating(LaunchRocket, 0, rocketOptions.Interval);
            }
            private void LaunchRocket()
            {
                if (rocketsFired >= rocketOptions.Amount)
                {
                    entity.CancelInvoke(LaunchRocket);
                    return;
                }
                var rocketType = rocketOptions.Type == "Normal" ? basicRocket : fireRocket;
                if (rocketOptions.Mixed && UnityEngine.Random.Range(1, rocketOptions.FireChance) == 1)
                    rocketType = fireRocket;

                Vector3 launchPos = entity.transform.position;
                Vector3 newTarget = Quaternion.Euler(GetRandom(), GetRandom(), GetRandom()) * targetPos;

                BaseEntity rocket = GameManager.server.CreateEntity(rocketType, launchPos, new Quaternion(), true);

                TimedExplosive rocketExplosion = rocket.GetComponent<TimedExplosive>();
                ServerProjectile rocketProjectile = rocket.GetComponent<ServerProjectile>();

                rocketProjectile.speed = rocketOptions.Speed;
                rocketProjectile.gravityModifier = 0;
                rocketExplosion.timerAmountMin = 60;
                rocketExplosion.timerAmountMax = 60;
                for (int i = 0; i < rocketExplosion.damageTypes.Count; i++)
                    rocketExplosion.damageTypes[i].amount *= rocketOptions.Damage;

                Vector3 newDirection = (newTarget - launchPos);

                rocket.SendMessage("InitializeVelocity", (newDirection));
                rocket.Spawn();
                ++rocketsFired;
            }

            private float GetRandom() => UnityEngine.Random.Range(-rocketOptions.Accuracy * 0.2f, rocketOptions.Accuracy * 0.2f);
        }
        #endregion

        #region Functions
        private void CallRandomStrike()
        {
            if (!configData.Other.RandomStrikes && !configData.Other.RandomSquads) return;

            timer.In(UnityEngine.Random.Range(configData.Other.RandomTimer[0], configData.Other.RandomTimer[1]), () =>
            {
                StrikeType type;
                if (configData.Other.RandomStrikes && configData.Other.RandomSquads)
                    type = UnityEngine.Random.Range(1, 2) == 1 ? type = StrikeType.Strike : type = StrikeType.Squad;
                else if (configData.Other.RandomStrikes)
                    type = StrikeType.Strike;
                else type = StrikeType.Squad;

                if (type == StrikeType.Strike)
                    CallStrike(GetRandomPosition());
                else CallSquad(GetRandomPosition());

                CallRandomStrike();
            });
        }
        private void CallStrike(Vector3 position)
        {
            CargoPlane entity = CreatePlane();
            entity.Spawn();

            StrikePlane plane = entity.gameObject.AddComponent<StrikePlane>();
            plane.InitializeFlightPath(position);

            if (configData.Other.Broadcast)
                PrintToChat(msg("strikeInbound"));
        }
        private void CallSquad(Vector3 position)
        {
            CargoPlane leaderEnt = CreatePlane();
            leaderEnt.Spawn();

            StrikePlane leaderPlane = leaderEnt.gameObject.AddComponent<StrikePlane>();
            leaderPlane.InitializeFlightPath(position);

            Vector3 startPos;
            Vector3 endPos;
            float secondsToTake;
            leaderPlane.GetFlightData(out startPos, out endPos, out secondsToTake);

            CargoPlane leftEnt = CreatePlane();
            leftEnt.Spawn();
            StrikePlane leftPlane = leftEnt.gameObject.AddComponent<StrikePlane>();
            Vector3 leftOffset = (leaderEnt.transform.right * 70) + (-leaderEnt.transform.forward * 80);
            leftPlane.SetFlightData(startPos + leftOffset, endPos + leftOffset, position + (leftOffset / 4), secondsToTake);

            CargoPlane rightEnt = CreatePlane();
            rightEnt.Spawn();
            StrikePlane rightPlane = rightEnt.gameObject.AddComponent<StrikePlane>();
            Vector3 rightOffset = (-leaderEnt.transform.right * 70) + (-leaderEnt.transform.forward * 80);
            rightPlane.SetFlightData(startPos + rightOffset, endPos + rightOffset, position + (rightOffset / 4), secondsToTake);

            if (configData.Other.Broadcast)
                PrintToChat(msg("squadInbound"));
        }

        private bool CanBuyStrike(BasePlayer player, StrikeType type)
        {
            Dictionary<string, int> costToBuy = type == StrikeType.Strike ? configData.Buy.StrikeCost : configData.Buy.SquadCost;

            foreach(var item in costToBuy)
            {
                if (item.Key == "RP")
                {
                    if (ServerRewards)
                    {
                        if ((int)ServerRewards.Call("CheckPoints", player.userID) < item.Value)
                        {
                            SendReply(player, string.Format(msg("buyItem", player.UserIDString), item.Value, item.Key));
                            Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player.transform.position);
                            return false;
                        }
                    }
                }
                if (item.Key == "Economics")
                {
                    if (Economics)
                    {
                        if ((double)Economics.Call("GetPlayerMoney", player.userID) < item.Value)
                        {
                            SendReply(player, string.Format(msg("buyItem", player.UserIDString), item.Value, item.Key));
                            Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player.transform.position);
                            return false;
                        }
                    }
                }
                if (shortnameToId.ContainsKey(item.Key))
                {
                    if (player.inventory.GetAmount(shortnameToId[item.Key]) < item.Value)
                    {
                        SendReply(player, string.Format(msg("buyItem", player.UserIDString), item.Value, shortnameToDn[item.Key]));
                        Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player.transform.position);
                        return false;
                    }
                }
            }
            Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.updated.prefab", player.transform.position);
            return true;
        }

        private void BuyStrike(BasePlayer player, StrikeType type)
        {
            Dictionary<string, int> costToBuy = type == StrikeType.Strike ? configData.Buy.StrikeCost : configData.Buy.SquadCost;

            foreach (var item in costToBuy)
            {
                if (item.Key == "RP")
                {
                    if (ServerRewards)
                        ServerRewards.Call("TakePoints", player.userID, item.Value);
                }
                if (item.Key == "Economics")
                {
                    if (Economics)
                        Economics.Call("Withdraw", player.userID, (double)item.Value);
                }
                if (shortnameToId.ContainsKey(item.Key))
                    player.inventory.Take(null, shortnameToId[item.Key], item.Value);
            }
            if (type == StrikeType.Strike)
            {
                CallStrike(player.transform.position);
                SendReply(player, string.Format(msg("strikeConfirmed", player.UserIDString), player.transform.position));
            }
            else
            {
                CallSquad(player.transform.position);
                SendReply(player, string.Format(msg("squadConfirmed", player.UserIDString), player.transform.position));
            }
        }
        #endregion

        #region Helpers
        private double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
        private CargoPlane CreatePlane() => (CargoPlane)GameManager.server.CreateEntity(cargoPlanePrefab, new Vector3(), new Quaternion(), true);
        private bool isStrikePlane(CargoPlane plane) => plane.GetComponent<StrikePlane>();
        private bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);

        private Vector3 GetRandomPosition()
        {
            float mapSize = (TerrainMeta.Size.x / 2) - 600f;

            float randomX = UnityEngine.Random.Range(-mapSize, mapSize);
            float randomY = UnityEngine.Random.Range(-mapSize, mapSize);

            return new Vector3(randomX, 0f, randomY);
        }

        private string FormatTime(double time)
        {
            TimeSpan dateDifference = TimeSpan.FromSeconds((float)time);
            var days = dateDifference.Days;
            var hours = dateDifference.Hours;
            hours += (days * 24);
            var mins = dateDifference.Minutes;
            var secs = dateDifference.Seconds;
            return string.Format("{0:00}:{1:00}:{2:00}", hours, mins, secs);
        }

        private void AddCooldownData(BasePlayer player, StrikeType type)
        {
            if (!configData.Cooldown.Enabled) return;

            if (!storedData.cooldowns.ContainsKey(player.userID))
                storedData.cooldowns.Add(player.userID, new CooldownData());

            if (type == StrikeType.Strike)
                storedData.cooldowns[player.userID].strikeCd = GrabCurrentTime() + configData.Cooldown.Strike;
            else storedData.cooldowns[player.userID].squadCd = GrabCurrentTime() + configData.Cooldown.Squad;
        }

        private List<BasePlayer> FindPlayer(string arg)
        {
            var foundPlayers = new List<BasePlayer>();

            ulong steamid;
            ulong.TryParse(arg, out steamid);
            string lowerarg = arg.ToLower();

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (steamid != 0L)
                {
                    if (player.userID == steamid)
                    {
                        foundPlayers.Clear();
                        foundPlayers.Add(player);
                        return foundPlayers;
                    }
                }
                string lowername = player.displayName.ToLower();
                if (lowername.Contains(lowerarg))
                {
                    foundPlayers.Add(player);
                }
            }
            return foundPlayers;
        }
        #endregion

        #region Commands
        [ChatCommand("airstrike")]
        void cmdAirstrike(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                SendReply(player, string.Format("Airstrike  v.{0}", Version));
                if (HasPermission(player, "airstrike.signal.strike"))
                    SendReply(player, msg("help1", player.UserIDString));
                if (HasPermission(player, "airstrike.signal.squad"))
                    SendReply(player, msg("help2", player.UserIDString));
                if (HasPermission(player, "airstrike.purchase.strike"))
                    SendReply(player, msg("help3", player.UserIDString));
                if (HasPermission(player, "airstrike.purchase.squad"))
                    SendReply(player, msg("help4", player.UserIDString));
                if (HasPermission(player, "airstrike.chat.strike"))
                {
                    SendReply(player, msg("help5", player.UserIDString));
                    SendReply(player, msg("help6", player.UserIDString));
                    SendReply(player, msg("help7", player.UserIDString));
                }
                if (HasPermission(player, "airstrike.chat.squad"))
                {
                    SendReply(player, msg("help8", player.UserIDString));
                    SendReply(player, msg("help9", player.UserIDString));
                    SendReply(player, msg("help10", player.UserIDString));
                }
                return;
            }
            if (args.Length >= 2)
            {
                var time = GrabCurrentTime();
                StrikeType type = args[1].ToLower() == "squad" ? StrikeType.Squad : StrikeType.Strike;

                if (!HasPermission(player, "airstrike.ignorecooldown"))
                {
                    if (configData.Cooldown.Enabled)
                    {
                        CooldownData data;
                        if (storedData.cooldowns.TryGetValue(player.userID, out data))
                        {
                            double nextUse = type == StrikeType.Strike ? data.strikeCd : data.squadCd;
                            if (nextUse > time)
                            {
                                double remaining = nextUse - time;
                                SendReply(player, string.Format(msg("onCooldown", player.UserIDString), FormatTime(remaining)));
                                return;
                            }
                        }
                    }
                }
                switch (args[0].ToLower())
                {
                    case "signal":
                        if ((type == StrikeType.Strike && configData.Other.SignalStrike) || (type == StrikeType.Squad && configData.Other.SignalSquad))
                        {
                            if (!HasPermission(player, $"airstrike.signal.{type.ToString().ToLower()}"))
                            {
                                SendReply(player, msg("noPerms", player.UserIDString));
                                return;
                            }
                        }
                        if (toggleList.ContainsKey(player.userID))
                            toggleList[player.userID] = type;
                        else toggleList.Add(player.userID, type);
                        SendReply(player, msg("signalReady", player.UserIDString));
                        return;
                    case "buy":
                        if ((type == StrikeType.Strike && configData.Buy.PermissionStrike) || (type == StrikeType.Squad && configData.Buy.PermissionSquad))
                        {
                            if (!HasPermission(player, $"airstrike.purchase.{type.ToString().ToLower()}"))
                            {
                                SendReply(player, msg("noPerms", player.UserIDString));
                                return;
                            }
                        }
                        if (CanBuyStrike(player, type))
                        {
                            BuyStrike(player, type);
                            AddCooldownData(player, type);
                        }
                        return;
                    case "call":
                        if (HasPermission(player, $"airstrike.chat.{type.ToString().ToLower()}"))
                        {
                            Vector3 position;
                            if (args.Length == 4)
                            {
                                float x, z;
                                if (!float.TryParse(args[2], out x) || !float.TryParse(args[3], out z))
                                {
                                    SendReply(player, msg("invCoords", player.UserIDString));
                                    return;
                                }
                                else position = new Vector3(x, 0, z);
                            }
                            else if (args.Length == 3)
                            {
                                var players = FindPlayer(args[2]);
                                if (players.Count > 1)
                                {
                                    SendReply(player, msg("multiplePlayers", player.UserIDString));
                                    return;
                                }
                                else if (players.Count == 0)
                                {
                                    SendReply(player, msg("noPlayers", player.UserIDString));
                                    return;
                                }
                                else position = players[0].transform.position;
                            }
                            else position = player.transform.position;

                            if (type == StrikeType.Strike)
                            {
                                CallStrike(position);
                                SendReply(player, string.Format(msg("strikeConfirmed", player.UserIDString), position));
                            }
                            else
                            {
                                CallSquad(position);
                                SendReply(player, string.Format(msg("squadConfirmed", player.UserIDString), position));
                            }
                            AddCooldownData(player, type);
                        }
                        else SendReply(player, msg("noPerms", player.UserIDString));
                        return;
                    default:
                        break;
                }
            }
        }

        [ConsoleCommand("airstrike")]
        void ccmdAirstrike(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            if (arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(arg, "airstrike strike <x> <z> - Call a airstrike to the target position");
                SendReply(arg, "airstrike squad <x> <z> - Call a squadstrike to the target position");
                SendReply(arg, "airstrike strike <playername> - Call a airstrike to the target player");
                SendReply(arg, "airstrike squad <playername> - Call a squadstrike to the target player");
                SendReply(arg, "airstrike strike random - Call a random airstrike");
                SendReply(arg, "airstrike squad random - Call a random squadstrike");
                return;
            }

            StrikeType type = arg.Args[0].ToLower() == "squad" ? StrikeType.Squad : StrikeType.Strike;

            Vector3 position = Vector3.zero;

            if (arg.Args[1].ToLower() == "random")
                position = GetRandomPosition();
            else if (arg.Args.Length == 3)
            {
                float x, z;
                if (!float.TryParse(arg.Args[1], out x) || !float.TryParse(arg.Args[2], out z))
                {
                    SendReply(arg, "Invalid co-ordinates set. You must enter number values for X and Z");
                    return;
                }
                else position = new Vector3(x, 0, z);
            }
            else if (arg.Args.Length == 2)
            {
                var players = FindPlayer(arg.Args[1]);
                if (players.Count > 1)
                {
                    SendReply(arg, "Multiple players found");
                    return;
                }
                else if (players.Count == 0)
                {
                    SendReply(arg, "No players found");
                    return;
                }
                else position = players[0].transform.position;
            }

            if (type == StrikeType.Strike)
            {
                CallStrike(position);
                SendReply(arg, string.Format("Airstrike confirmed at co-ordinates: {0}!", position));
            }
            else
            {
                CallSquad(position);
                SendReply(arg, string.Format("Squadstrike confirmed at co-ordinates: {0}!", position));
            }
        }
        #endregion

        #region Config
        private ConfigData configData;
        class RocketOptions
        {
            [JsonProperty(PropertyName = "Speed of the rocket")]
            public float Speed { get; set; }
            [JsonProperty(PropertyName = "Damage modifier")]
            public float Damage { get; set; }
            [JsonProperty(PropertyName = "Accuracy of rocket (a lower number is more accurate)")]
            public float Accuracy { get; set; }
            [JsonProperty(PropertyName = "Interval between rockets (seconds)")]
            public float Interval { get; set; }
            [JsonProperty(PropertyName = "Type of rocket (Normal, Napalm)")]
            public string Type { get; set; }
            [JsonProperty(PropertyName = "Use both rocket types")]
            public bool Mixed { get; set; }
            [JsonProperty(PropertyName = "Chance of a fire rocket (when using both types)")]
            public int FireChance { get; set; }
            [JsonProperty(PropertyName = "Amount of rockets to fire")]
            public int Amount { get; set; }
        }
        class CooldownOptions
        {
            [JsonProperty(PropertyName = "Use cooldown timers")]
            public bool Enabled { get; set; }
            [JsonProperty(PropertyName = "Strike cooldown time (seconds)")]
            public int Strike { get; set; }
            [JsonProperty(PropertyName = "Squad cooldown time (seconds)")]
            public int Squad { get; set; }
        }
        class PlaneOptions
        {
            [JsonProperty(PropertyName = "Flight speed (meters per second)")]
            public float Speed { get; set; }
            [JsonProperty(PropertyName = "Distance from target to engage")]
            public float Distance { get; set; }
        }
        class BuyOptions
        {
            [JsonProperty(PropertyName = "Can purchase standard strike")]
            public bool StrikeEnabled { get; set; }
            [JsonProperty(PropertyName = "Can purchase squad strike")]
            public bool SquadEnabled { get; set; }
            [JsonProperty(PropertyName = "Require permission to purchase strike")]
            public bool PermissionStrike { get; set; }
            [JsonProperty(PropertyName = "Require permission to purchase squad strike")]
            public bool PermissionSquad { get; set; }
            [JsonProperty(PropertyName = "Cost to purchase a standard strike (shortname, amount)")]
            public Dictionary<string, int> StrikeCost { get; set; }
            [JsonProperty(PropertyName = "Cost to purchase a squad strike (shortname, amount)")]
            public Dictionary<string, int> SquadCost { get; set; }
        }
        class OtherOptions
        {
            [JsonProperty(PropertyName = "Broadcast strikes to chat")]
            public bool Broadcast { get; set; }
            [JsonProperty(PropertyName = "Can call standard strikes using a supply signal")]
            public bool SignalStrike { get; set; }
            [JsonProperty(PropertyName = "Can call squad strikes using a supply signal")]
            public bool SignalSquad { get; set; }
            [JsonProperty(PropertyName = "Use random airstrikes")]
            public bool RandomStrikes { get; set; }
            [JsonProperty(PropertyName = "Use random squad strikes")]
            public bool RandomSquads { get; set; }
            [JsonProperty(PropertyName = "Random timer (minimum, maximum. In seconds)")]
            public int[] RandomTimer { get; set; }
        }
        class ConfigData
        {
            [JsonProperty(PropertyName = "Rocket Options")]
            public RocketOptions Rocket { get; set; }
            [JsonProperty(PropertyName = "Cooldown Options")]
            public CooldownOptions Cooldown { get; set; }
            [JsonProperty(PropertyName = "Plane Options")]
            public PlaneOptions Plane { get; set; }
            [JsonProperty(PropertyName = "Purchase Options")]
            public BuyOptions Buy { get; set; }
            [JsonProperty(PropertyName = "Other Options")]
            public OtherOptions Other { get; set; }
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
                Buy = new BuyOptions
                {
                    SquadCost = new Dictionary<string, int>
                    {
                        ["metal.refined"] = 100,
                        ["techparts"] = 50,
                        ["targeting.computer"] = 1
                    },
                    SquadEnabled = true,
                    PermissionSquad = true,
                    StrikeCost = new Dictionary<string, int>
                    {
                        ["metal.refined"] = 50,
                        ["targeting.computer"] = 1
                    },
                    StrikeEnabled = true,
                    PermissionStrike = true
                },
                Cooldown = new CooldownOptions
                {
                    Enabled = true,
                    Squad = 3600,
                    Strike = 3600
                },
                Other = new OtherOptions
                {
                    Broadcast = true,
                    SignalSquad = true,
                    SignalStrike = true,
                    RandomSquads = true,
                    RandomStrikes = true,
                    RandomTimer = new int[] { 1800, 3600 }
                },
                Plane = new PlaneOptions
                {
                    Distance = 900,
                    Speed = 105
                },
                Rocket = new RocketOptions
                {
                    Accuracy = 1.5f,
                    Amount = 15,
                    Damage = 1.0f,
                    FireChance = 4,
                    Interval = 0.6f,
                    Mixed = true,
                    Speed = 110f,
                    Type = "Normal"
                }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Data Management
        void SaveData() => data.WriteObject(storedData);
        void LoadData()
        {
            try
            {
                storedData = data.ReadObject<StoredData>();
            }
            catch
            {
                storedData = new StoredData();
            }
        }
        void ClearData()
        {
            storedData = new StoredData();
            SaveData();
        }
        class StoredData
        {
            public Dictionary<ulong, CooldownData> cooldowns = new Dictionary<ulong, CooldownData>();
        }
        class CooldownData
        {
            public double strikeCd, squadCd;
        }
        #endregion

        #region Localization
        string msg(string key, string playerId = null) => lang.GetMessage(key, this, playerId);

        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["strikeConfirmed"] = "Airstrike confirmed at co-ordinates: {0}",
            ["squadConfirmed"] = "Squadstrike confirmed at co-ordinates: {0}",
            ["strikeInbound"] = "Airstrike inbound!",
            ["squadInbound"] = "Squadstrike inbound!",
            ["buyItem"] = "You need another {0} {1} to buy this strike",
            ["help1"] = "/airstrike signal strike - Use a supply signal to mark a airstrike position",
            ["help2"] = "/airstrike signal squad - Use a supply signal to mark a squadstrike position",
            ["help3"] = "/airstrike buy strike - Purchase a airstrike on your position",
            ["help4"] = "/airstrike buy squad - Purchase a squadstrike on your position",
            ["help5"] = "/airstrike call strike - Call a airstrike on your position",
            ["help6"] = "/airstrike call strike <x> <z> - Call a airstrike to the target position",
            ["help7"] = "/airstrike call strike <player name> - Call a airstrike to the target player",
            ["help8"] = "/airstrike call squad - Call a squadstrike on your position",
            ["help9"] = "/airstrike call squad <x> <z> - Call a squadstrike to the target position",
            ["help10"] = "/airstrike call squad <player name> - Call a squadstrike to the target player",
            ["onCooldown"] = "You must wait another {0} before calling this type again",
            ["noPerms"] = "You do not have permission to use that strike type",
            ["signalReady"] = "Throw a supply signal to call a strike",
            ["invCoords"] = "Invalid co-ordinates set. You must enter number values for X and Z",
            ["multiplePlayers"] = "Multiple players found",
            ["noPlayers"] = "No players found"
        };
        #endregion
    }
}