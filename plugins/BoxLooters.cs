using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Configuration;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("BoxLooters", "4seti / k1lly0u", "0.3.4", ResourceId = 989)]
    class BoxLooters : RustPlugin
    {
        #region Fields
        BoxDS boxData;
        PlayerDS playerData;
        private DynamicConfigFile bdata;
        private DynamicConfigFile pdata;

        static BoxLooters ins;
        
        private bool eraseData = false;

        private Hash<uint, BoxData> boxCache;
        private Hash<ulong, PlayerData> playerCache;
        #endregion

        #region Oxide Hooks
        void Loaded()
        {
            bdata = Interface.Oxide.DataFileSystem.GetFile("Boxlooters/box_data");
            pdata = Interface.Oxide.DataFileSystem.GetFile("Boxlooters/player_data");
            
            boxCache = new Hash<uint, BoxData>();
            playerCache = new Hash<ulong, PlayerData>();

            lang.RegisterMessages(messages, this);
            permission.RegisterPermission("boxlooters.checkbox", this);
        }
        void OnServerInitialized()
        {
            ins = this;
            LoadVariables();
            LoadData();
            if (eraseData)
                ClearAllData();
            else RemoveOldData();
        }
        void OnNewSave(string filename) => eraseData = true;        
        void OnServerSave() => SaveData();
        void Unload()
        {
            SaveData();
            ins = null;
        }

        void OnLootEntity(BasePlayer looter, BaseEntity entity)
        {
            if (looter == null || entity == null || !entity.IsValid() || !IsValidType(entity)) return;

            var time = GrabCurrentTime();
            var date = DateTime.Now.ToString("d/M @ HH:mm:ss");
            
            if (entity is BasePlayer)
            {
                if (!configData.LogPlayerLoot) return;
                var looted = entity.ToPlayer();
                if (!playerCache.ContainsKey(looted.userID))
                    playerCache[looted.userID] = new PlayerData(looter, time, date);
                else playerCache[looted.userID].AddLooter(looter, time, date);                
            }
            else
            {
                if (!configData.LogBoxLoot) return;
                var boxId = entity.net.ID;
                if (!boxCache.ContainsKey(boxId))
                    boxCache[boxId] = new BoxData(looter, time, date, entity.transform.position);
                else boxCache[boxId].AddLooter(looter, time, date); 
            }

        }
        void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo)
        {
            try
            {
                if (entity == null || !entity.IsValid() || !IsValidType(entity) || entity is BasePlayer) return;
                if (hitInfo?.Initiator is BasePlayer)
                {
                    var boxId = entity.net.ID;
                    if (!boxCache.ContainsKey(boxId)) return;
                    boxCache[boxId].OnDestroyed(hitInfo.InitiatorPlayer);
                }
            }
            catch { }
        }
        #endregion

        #region Data Cleanup
        void ClearAllData()
        {
            PrintWarning("Detected map wipe, resetting loot data!");
            boxCache.Clear();
            playerCache.Clear();
        }
        void RemoveOldData()
        {
            PrintWarning("Attempting to remove old log entries");
            int boxCount = 0;
            int playerCount = 0;
            double time = GrabCurrentTime() - (configData.RemoveHours * 3600);

            for (int i = 0; i < boxCache.Count; i++)
            {
                KeyValuePair<uint, BoxData> boxEntry = boxCache.ElementAt(i);
                if (boxEntry.Value.lastAccess < time)
                {
                    boxCache.Remove(boxEntry.Key);
                    ++boxCount;
                }
            }
            PrintWarning($"Removed {boxCount} old records from BoxData");

            for (int i = 0; i < playerCache.Count; i++)
            {
                KeyValuePair<ulong, PlayerData> playerEntry = playerCache.ElementAt(i);
                if (playerEntry.Value.lastAccess < time)
                {
                    playerCache.Remove(playerEntry.Key);
                    ++playerCount;
                }
            }
            PrintWarning($"Removed {playerCount} old records from PlayerData");
        }       
        #endregion

        #region Functions
        object FindBoxFromRay(BasePlayer player)
        {
            Ray ray = new Ray(player.eyes.position, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward);
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 20))
                return null;

            var hitEnt = hit.collider.GetComponentInParent<BaseEntity>();
            if (hitEnt != null)
            {
                if (IsValidType(hitEnt))
                    return hitEnt;
            }
            return null;            
        }
        void ReplyInfo(BasePlayer player, string Id, bool isPlayer = false, string additional = "")
        {
            var entId = Id;
            if (!string.IsNullOrEmpty(additional))
                entId = $"{additional} - {Id}";

            if (!isPlayer)
            {                
                if (boxCache.ContainsKey(uint.Parse(Id)))
                {
                    var box = boxCache[uint.Parse(Id)];
                    SendReply(player, string.Format(lang.GetMessage("BoxInfo", this, player.UserIDString), entId));

                    if (!string.IsNullOrEmpty(box.killerName))
                        SendReply(player, string.Format(lang.GetMessage("DetectDestr", this, player.UserIDString), box.killerName, box.killerId));

                    int i = 1;
                    string response1 = string.Empty;
                    string response2 = string.Empty;
                    foreach (var data in box.lootList.GetLooters().Reverse().Take(10))
                    {
                        var respString = string.Format(lang.GetMessage("DetectedLooters", this, player.UserIDString), i, data.userName, data.userId, data.firstLoot, data.lastLoot);
                        if (i < 6) response1 += respString;
                        else response2 += respString;
                        i++;                        
                    }
                    SendReply(player, response1);
                    SendReply(player, response2);
                }
                else SendReply(player, string.Format(lang.GetMessage("NoLooters", this, player.UserIDString), entId));
            }
            else
            {
                if (playerCache.ContainsKey(ulong.Parse(Id)))
                {
                    SendReply(player, string.Format(lang.GetMessage("PlayerData", this, player.UserIDString), entId));

                    int i = 1;
                    string response1 = string.Empty;
                    string response2 = string.Empty;
                    foreach (var data in playerCache[ulong.Parse(Id)].lootList.GetLooters().Reverse().Take(10))
                    {
                        var respString = string.Format(lang.GetMessage("DetectedLooters", this, player.UserIDString), i, data.userName, data.userId, data.firstLoot, data.lastLoot);
                        if (i < 6) response1 += respString;
                        else response2 += respString;
                        i++;
                    }
                    SendReply(player, response1);
                    SendReply(player, response2);
                }
                else SendReply(player, string.Format(lang.GetMessage("NoLootersPlayer", this, player.UserIDString), entId));
            }
        }
        #endregion

        #region Helpers
        double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
        bool HasPermission(BasePlayer player) => permission.UserHasPermission(player.UserIDString, "boxlooters.checkbox") || player.net.connection.authLevel > 0;
        float GetDistance(Vector3 init, Vector3 target) => Vector3.Distance(init, target);
        bool IsValidType(BaseEntity entity) => !entity.GetComponent<LootContainer>() && (entity is StorageContainer || entity is MiningQuarry || entity is ResourceExtractorFuelStorage || entity is BasePlayer);
        #endregion

        #region Commands
        [ChatCommand("box")]
        void cmdBox(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player)) return;
            if (args == null || args.Length == 0)
            {
                var success = FindBoxFromRay(player);
                if (success is MiningQuarry)
                {
                    var children = (success as MiningQuarry).children;
                    if (children != null)
                    {
                        foreach (var child in children)
                        {
                            if (child.GetComponent<StorageContainer>())
                            {
                                ReplyInfo(player, child.net.ID.ToString(), false, child.ShortPrefabName);
                            }
                        }
                    }
                    else SendReply(player, lang.GetMessage("Nothing", this, player.UserIDString));
                }
                else if (success is BasePlayer)
                    ReplyInfo(player, (success as BasePlayer).UserIDString, true);
                else if (success is BaseEntity)
                    ReplyInfo(player, (success as BaseEntity).net.ID.ToString());

                else SendReply(player, lang.GetMessage("Nothing", this, player.UserIDString));
                return;
            }
            switch (args[0].ToLower())
            {
                case "help":
                    {
                        SendReply(player, $"<color=#4F9BFF>{Title}  v{Version}</color>");
                        SendReply(player, "<color=#4F9BFF>/box help</color> - Display the help menu");
                        SendReply(player, "<color=#4F9BFF>/box</color> - Retrieve information on the box you are looking at");                        
                        SendReply(player, "<color=#4F9BFF>/box id <number></color> - Retrieve information on the specified box");
                        SendReply(player, "<color=#4F9BFF>/box near <opt:radius></color> - Show nearby boxes (current and destroyed) and their ID numbers");
                        SendReply(player, "<color=#4F9BFF>/box player <partialname/id></color> - Retrieve loot information about a player");
                        SendReply(player, "<color=#4F9BFF>/box clear</color> - Clears all saved data");
                        SendReply(player, "<color=#4F9BFF>/box save</color> - Saves box data");
                    }
                    return;
                case "id":
                    if (args.Length >= 2)
                    {
                        uint id;
                        if (uint.TryParse(args[1], out id))                        
                            ReplyInfo(player, id.ToString());                        
                        else SendReply(player, lang.GetMessage("NoID", this, player.UserIDString));
                        return;
                    }
                    break;
                case "near":
                    {
                        float radius = 20f;
                        if (args.Length >= 2)
                        {
                            if (!float.TryParse(args[1], out radius))
                                radius = 20f;
                        }
                        foreach(var box in boxCache)
                        {
                            if (GetDistance(player.transform.position, box.Value.GetPosition()) <= radius)
                            {
                                player.SendConsoleCommand("ddraw.text", 20f, Color.green, box.Value.GetPosition() + new Vector3(0, 1.5f, 0), $"<size=40>{box.Key}</size>");
                                player.SendConsoleCommand("ddraw.box", 20f, Color.green, box.Value.GetPosition(), 1f);
                            }
                        }
                    }
                    return;
                case "player":
                    if (args.Length >= 2)
                    {
                        var target = covalence.Players.FindPlayer(args[1]);
                        if (target != null)                        
                            ReplyInfo(player, target.Id, true);
                        else SendReply(player, lang.GetMessage("NoPlayer", this, player.UserIDString));
                        return;
                    }
                    break;
                case "clear":
                    boxCache.Clear();
                    playerCache.Clear();
                    SendReply(player, lang.GetMessage("ClearData", this, player.UserIDString));
                    return;
                case "save":
                    SaveData();
                    SendReply(player, lang.GetMessage("SavedData", this, player.UserIDString));
                    return;
                default:
                    break;
            }
            SendReply(player, lang.GetMessage("SynError", this, player.UserIDString));
        }
        #endregion

        #region Config        
        private ConfigData configData;
        class ConfigData
        {
            public int RemoveHours { get; set; }  
            public int RecordsPerContainer { get; set; } 
            public bool LogPlayerLoot { get; set; }
            public bool LogBoxLoot { get; set; }         
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
                RemoveHours = 48,
                RecordsPerContainer = 10,
                LogBoxLoot = true,
                LogPlayerLoot = true
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Data Management        
        class BoxData
        {
            public float x, y, z;
            public string killerId, killerName;
            public LootList lootList;
            public double lastAccess;

            public BoxData() { }
            public BoxData(BasePlayer player, double time, string date, Vector3 pos)
            {
                x = pos.x;
                y = pos.y;
                z = pos.z;
                lootList = new LootList(player, date);
                lastAccess = time;
            }
            public void AddLooter(BasePlayer looter, double time, string date)
            {
                lootList.AddEntry(looter, date);
                lastAccess = time;
            }

            public void OnDestroyed(BasePlayer killer)
            {
                killerId = killer.UserIDString;
                killerName = killer.displayName;
            }
            public Vector3 GetPosition() => new Vector3(x, y, z);            
        }
        class PlayerData
        {
            public LootList lootList;
            public double lastAccess;

            public PlayerData() { }
            public PlayerData(BasePlayer player, double time, string date)
            {
                lootList = new LootList(player, date);
                lastAccess = time;
            }
            public void AddLooter(BasePlayer looter, double time, string date)
            {
                lootList.AddEntry(looter, date);
                lastAccess = time;
            }        
        }
        class LootList
        {
            public List<LootEntry> looters;

            public LootList() { }
            public LootList(BasePlayer player, string date)
            {
                looters = new List<LootEntry>();
                looters.Add(new LootEntry(player, date));
            }
            public void AddEntry(BasePlayer player, string date)
            {
                LootEntry lastEntry = null;
                try { lastEntry = looters.Single(x => x.userId == player.UserIDString); } catch { }                 
                if (lastEntry != null)
                {
                    looters.Remove(lastEntry);
                    lastEntry.lastLoot = date;
                }
                else
                {
                    if (looters.Count == ins.configData.RecordsPerContainer)
                        looters.Remove(looters.ElementAt(0));
                    lastEntry = new LootEntry(player, date);
                }
                looters.Add(lastEntry);
            }
            public LootEntry[] GetLooters() => looters.ToArray();

            public class LootEntry
            {
                public string userId, userName, firstLoot, lastLoot;
                            
                public LootEntry() { }
                public LootEntry(BasePlayer player, string firstLoot)
                {
                    userId = player.UserIDString;
                    userName = player.displayName;
                    this.firstLoot = firstLoot;
                    lastLoot = firstLoot;                    
                }
            }
        }
        
        void SaveData()
        {
            if (configData.LogBoxLoot)
            {
                boxData.boxes = boxCache;
                bdata.WriteObject(boxData);
            }
            if (configData.LogPlayerLoot)
            {
                playerData.players = playerCache;
                pdata.WriteObject(playerData);
            }
            PrintWarning("Saved Boxlooters data");
        }
        void LoadData()
        {            
            try
            {
                boxData = bdata.ReadObject<BoxDS>();
                boxCache = boxData.boxes;
            }
            catch
            {
                boxData = new BoxDS();
            }
            try
            {
                playerData = pdata.ReadObject<PlayerDS>();
                playerCache = playerData.players;
            }
            catch
            {
                playerData = new PlayerDS();                
            }
        }
        class BoxDS
        {
            public Hash<uint, BoxData> boxes = new Hash<uint, BoxData>();
        }
        class PlayerDS
        {
            public Hash<ulong, PlayerData> players = new Hash<ulong, PlayerData>();
        }       
        #endregion

        #region Localization
        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"BoxInfo", "List of looters for this Box [<color=#F5D400>{0}</color>]:"},
            {"PlayerData", "List of looters for this Player [<color=#F5D400>{0}</color>]:"},            
            {"DetectedLooters", "<color=#F5D400>[{0}]</color><color=#4F9BFF>{1}</color> ({2})\nF:<color=#F80>{3}</color> L:<color=#F80>{4}</color>\n"},
            {"DetectDestr", "Destoyed by: <color=#4F9BFF>{0}</color> ID:{1}"},
            {"NoLooters", "<color=#4F9BFF>The box [{0}] is clear!</color>"},
            {"NoLootersPlayer", "<color=#4F9BFF>The player [{0}] is clear!</color>"},
            {"Nothing", "<color=#4F9BFF>Unable to find a valid entity</color>"},
            {"NoID", "<color=#4F9BFF>You must enter a valid entity ID</color>"},
            {"NoPlayer",  "No players with that name/ID found!"},
            {"SynError", "<color=#F5D400>Syntax Error: Type '/box' to view available options</color>" },
            {"SavedData", "You have successfully saved loot data" },
            {"ClearData", "You have successfully cleared all loot data" }
        };
        #endregion
    }
}