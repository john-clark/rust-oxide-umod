using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Corpse Location", "shinnova", "2.2.3")]
    [Description("Allows users to locate their latest corpse")]

    class CorpseLocation : RustPlugin
    {
        #region Variable Declaration
        const string UsePerm = "corpselocation.use";
        const string TPPerm = "corpselocation.tp";
        const string VIPPerm = "corpselocation.vip";
        const string AdminPerm = "corpselocation.admin";
        const float calgon = 0.0066666666666667f;
        float WorldSize = (ConVar.Server.worldsize);
        Dictionary<string, Vector3> InternalGrid = new Dictionary<string, Vector3>();
        Dictionary<string, Timer> ActiveTimers = new Dictionary<string, Timer>();
        #endregion

        #region Config
        Configuration config;
        public class Configuration
        {
            [JsonProperty(PropertyName = "Show grid location")]
            public bool showGrid { get; set; } = true;
            [JsonProperty(PropertyName = "Track a corpse's location for x seconds")]
            public int trackTime { get; set; } = 30;
            [JsonProperty(PropertyName = "Allow teleporting to own corpse x times per day (0 for unlimited)")]
            public int tpAmount { get; set; } = 5;
            [JsonProperty(PropertyName = "Allow teleporting to own corpse x times per day (0 for unlimited), for VIPs")]
            public int viptpAmount { get; set; } = 10;
            [JsonProperty(PropertyName = "Reset players' remaining teleports at this time (HH:mm:ss format)")]
            public string resetTime { get; set; } = "00:00:00";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                    LoadDefaultConfig();
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            Puts("No or faulty config detected. Generating new configuration file");
            config = new Configuration();
        }

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Data
        public class StoredData
        {
            public Dictionary<string, List<float>> deaths = new Dictionary<string, List<float>>();
            public Dictionary<string, int> teleportsRemaining = new Dictionary<string, int>();
            public Dictionary<string, Vector3> GridInfo = new Dictionary<string, Vector3>();
            public StoredData(){}
        }

        StoredData storedData;

        void SaveData() => Interface.Oxide.DataFileSystem.WriteObject("CorpseLocation", storedData);
        #endregion

        #region Hooks
        void OnNewSave(string filename)
        {
            NewData();
        }

        void OnServerInitialized()
        {
            permission.RegisterPermission(UsePerm, this);
            permission.RegisterPermission(TPPerm, this);
            permission.RegisterPermission(VIPPerm, this);
            permission.RegisterPermission(AdminPerm, this);
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("CorpseLocation");
            if (storedData == null || storedData.deaths == null)
            {
                Puts("Faulty data detected. Generating new data file");
                NewData();
            }
            if (storedData.GridInfo == null || storedData.GridInfo.Count == 0) CreateGrid();
            InternalGrid = storedData.GridInfo;
            timer.Every(1f, () => {
                if (System.DateTime.Now.ToString("HH:mm:ss") == config.resetTime)
                {
                    foreach (string PlayerID in storedData.teleportsRemaining.Keys)
                        if (permission.UserHasPermission(PlayerID, VIPPerm))
                            storedData.teleportsRemaining[PlayerID] = config.viptpAmount;
                        else
                            storedData.teleportsRemaining[PlayerID] = config.tpAmount;
                    SaveData();
                }
            });
        }
        new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["YouDied"] = "Your corpse was last seen {0} meters from here.",
                ["YouDiedGrid"] = "Your corpse was last seen {0} meters from here, in {1}.",
                ["ArrivedAtYourCorpse"] = "You have arrived at your corpse.",
                ["ArrivedAtTheCorpse"] = "You have arrived at the corpse of {0}.",
                ["OutOfTeleports"] = "You have no more teleports left today.",
                ["TeleportsRemaining"] = "You have {0} teleports remaining today.",
                ["UnknownLocation"] = "Your last death location is unknown.",
                ["UnknownLocationTarget"] = "{0}'s last death location is unknown.",
                ["NeedTarget"] = "You need to specify a player to teleport to the corpse of, using either their name or steam id.",
                ["InvalidPlayer"] = "{0} is not part of a known player's name/id.",
                ["NotAllowed"] = "You do not have permission to use that command.",
            }, this);
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            if (storedData.deaths.ContainsKey(player.UserIDString))
                SendCorpseLocation(player);
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            var player = entity as BasePlayer;
            if (player != null)
            {
                if (entity.IsNpc)
                    return;
                string UserID = player.UserIDString;
                Vector3 DeathPosition = entity.transform.position;
                List<float> ShortDeathPosition = new List<float> { DeathPosition.x, DeathPosition.y, DeathPosition.z };
                storedData.deaths[UserID] = ShortDeathPosition;
                SaveData();
                Puts(String.Format("{0} ({1}) died at {2}", player.displayName, UserID, DeathPosition));
            }
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            BaseCorpse corpse = entity as BaseCorpse;
            if (corpse != null)
            {
                if (!corpse is PlayerCorpse || !corpse?.parentEnt?.ToPlayer() || corpse.parentEnt.ToPlayer().IsNpc)
                    return;
                BasePlayer player = corpse.parentEnt.ToPlayer();
                string UserID = player.UserIDString;
                if (ActiveTimers.ContainsKey(UserID))
                {
                    ActiveTimers[UserID].Destroy();
                    ActiveTimers.Remove(UserID);
                }
                Timer CorpseChecker = timer.Repeat(1, config.trackTime, () => {
                    if (!corpse.IsDestroyed)
                    {
                        Vector3 BodyLocation = corpse.transform.position;
                        List<float> ShortBodyLocation = new List<float> { BodyLocation.x, BodyLocation.y, BodyLocation.z };
                        storedData.deaths[UserID] = ShortBodyLocation;
                        SaveData();
                    }
                });
                ActiveTimers[UserID] = CorpseChecker;
            }
        }
        #endregion

        #region Functions
        void NewData()
        {
            storedData = new StoredData();
            SaveData();
            CreateGrid();
            InternalGrid = storedData.GridInfo;
        }

        float GetStepSize()
        {
            float GridWidth = (calgon * WorldSize);
            return WorldSize / GridWidth;
        }

        void CreateGrid()
        {
            if (storedData.GridInfo == null)
            {
                Dictionary<string, List<float>> DeathsBackup = storedData.deaths;
                storedData = new StoredData();
                storedData.deaths = DeathsBackup;
            }
            if (storedData.GridInfo.Count > 0) storedData.GridInfo.Clear();
            float offset = WorldSize / 2;
            float step = GetStepSize();
            string start = "";

            char letter = 'A';
            int number = 0;

            for (float zz = offset; zz > -offset; zz -= step)
            {
                for (float xx = -offset; xx < offset; xx += step)
                {
                    Vector3 GridStart = new Vector3(xx, 0, zz);
                    string GridReference = String.Format("{0}{1}{2}", start, letter, number);
                    storedData.GridInfo.Add(GridReference, GridStart);
                    if (letter.ToString().ToUpper() == "Z")
                    {
                        start = "A";
                        letter = 'A';
                    }
                    else
                    {
                        letter = (char)(((int)letter) + 1);
                    }


                }
                number++;
                start = "";
                letter = 'A';
            }
            SaveData();
        }

        string GetGrid(Vector3 DeathLocation)
        {
            string DeathGrid = "the unknown";
            foreach (var Grid in InternalGrid)
            {
                if (DeathLocation.x >= Grid.Value.x && DeathLocation.x < Grid.Value.x + GetStepSize() && DeathLocation.z <= Grid.Value.z && DeathLocation.z > Grid.Value.z - GetStepSize())
                {
                    DeathGrid = Grid.Key;
                    break;
                }
            }
            return DeathGrid;
        }

        void SendCorpseLocation(BasePlayer player)
        {
            List<float> ShortDeathLocation = storedData.deaths[player.UserIDString];
            Vector3 DeathLocation = new Vector3(ShortDeathLocation[0], ShortDeathLocation[1], ShortDeathLocation[2]);
            int DistanceToCorpse = (int)Vector3.Distance(player.transform.position, DeathLocation);
            string DeathGrid = GetGrid(DeathLocation);
            if (config.showGrid)
                SendReply(player, String.Format(lang.GetMessage("YouDiedGrid", this, player.UserIDString), DistanceToCorpse, DeathGrid));
            else
                SendReply(player, String.Format(lang.GetMessage("YouDied", this, player.UserIDString), DistanceToCorpse));
        }

        Dictionary<ulong, string> GetPlayers(string NameOrID)
        {
            var pl = covalence.Players.FindPlayers(NameOrID).ToList();
            return pl.Select(p => new KeyValuePair<ulong, string>(ulong.Parse(p.Id), p.Name)).ToDictionary(x => x.Key, x => x.Value);
        }
        #endregion

        #region Commands
        [ChatCommand("where")]
        void whereCommand(BasePlayer player, string command, string[] args)
        {
            string PlayerID = player.UserIDString;
            if (args.Length > 0 && args[0] == "tp" && permission.UserHasPermission(PlayerID, TPPerm))
            {
                int TPAllowed = config.tpAmount;
                if (permission.UserHasPermission(PlayerID, VIPPerm))
                    TPAllowed = config.viptpAmount;
                if (!storedData.teleportsRemaining.ContainsKey(PlayerID) || storedData.teleportsRemaining[PlayerID] > TPAllowed)
                {
                    storedData.teleportsRemaining[PlayerID] = TPAllowed;
                    SaveData();
                }
                List<float> TargetCorpse = storedData.deaths[PlayerID];
                Vector3 destination = new Vector3(TargetCorpse[0], TargetCorpse[1], TargetCorpse[2]);
                if (TPAllowed == 0)
                {
                    player.Teleport(destination);
                    SendReply(player, String.Format(lang.GetMessage("ArrivedAtYourCorpse", this, PlayerID)));
                }
                else
                {
                    if (storedData.teleportsRemaining[PlayerID] > 0)
                    {
                        player.Teleport(destination);
                        storedData.teleportsRemaining[PlayerID]--;
                        SaveData();
                        SendReply(player, String.Format(lang.GetMessage("ArrivedAtYourCorpse", this, PlayerID)));
                        SendReply(player, String.Format(lang.GetMessage("TeleportsRemaining", this, PlayerID), storedData.teleportsRemaining[PlayerID]));
                    }
                    else
                        SendReply(player, String.Format(lang.GetMessage("OutOfTeleports", this, PlayerID)));   
                }
                return; 
            }
            if (permission.UserHasPermission(PlayerID, UsePerm))
            {
                if (storedData.deaths.ContainsKey(player.UserIDString))
                    SendCorpseLocation(player);
                else
                    SendReply(player, String.Format(lang.GetMessage("UnknownLocation", this, PlayerID)));
            }
            else
                SendReply(player, String.Format(lang.GetMessage("NotAllowed", this, PlayerID)));
        }

        [ChatCommand("tpcorpse")]
        void tpCommand(BasePlayer player, string command, string[] args)
        {
            string PlayerID = player.UserIDString;
            if (permission.UserHasPermission(PlayerID, AdminPerm))
            {
                if (args.Length == 0)
                {
                    SendReply(player, String.Format(lang.GetMessage("NeedTarget", this, PlayerID)));
                    return;
                }
                Dictionary<ulong, string> FoundPlayers = GetPlayers(args[0]);
                if (FoundPlayers.Count == 0)
                {
                    SendReply(player, String.Format(lang.GetMessage("InvalidPlayer", this, PlayerID), args[0]));
                    return;
                }
                string TargetID = FoundPlayers.First().Key.ToString();
                if (storedData.deaths.ContainsKey(TargetID))
                {
                    List<float> TargetCorpse = storedData.deaths[TargetID];
                    Vector3 destination = new Vector3(TargetCorpse[0], TargetCorpse[1], TargetCorpse[2]);
                    player.Teleport(destination);
                    SendReply(player, String.Format(lang.GetMessage("ArrivedAtTheCorpse", this, PlayerID), FoundPlayers.First().Value));
                }
                else
                    SendReply(player, String.Format(lang.GetMessage("UnknownLocationTarget", this, PlayerID), FoundPlayers.First().Value));
            }
            else
                SendReply(player, String.Format(lang.GetMessage("NotAllowed", this, PlayerID)));
        }
        #endregion
    }
}