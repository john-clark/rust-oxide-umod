using System.Collections.Generic;
using System;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Configuration;
using System.Text;

//
// Credit to the original author, Mordenak / HighTower 2
//

namespace Oxide.Plugins
{
    [Info("PersonalBeacon", "redBDGR", "2.0.4")]
    [Description("Displays a beacon at a marked location for easier navigation")]

    class PersonalBeacon : RustPlugin
    {
        #region Data

        private DynamicConfigFile exampleData;
        WaypointDataStorage storedData;

        void SaveData()
        {
            storedData.playerWaypoints = playerWaypointsCache;
            storedData.globalWaypoints = globalWaypointCache;
            exampleData.WriteObject(storedData);
        }
        void LoadData()
        {
            try
            {
                storedData = exampleData.ReadObject<WaypointDataStorage>();
                LoadPlayerWaypoints();
                LoadGlobalWaypoints();
            }
            catch
            {
                Puts("Failed to load data, creating new file");
                storedData = new WaypointDataStorage();
            }
        }

        void LoadPlayerWaypoints()
        {
            foreach (var entry in storedData.playerWaypoints)
            {
                playerWaypointsCache.Add(entry.Key, new List<WaypointData>());
                foreach (var _entry in entry.Value)
                    playerWaypointsCache[entry.Key].Add(new WaypointData() { name = _entry.name, x = _entry.x, y = _entry.y, z = _entry.z });
            }
        }

        void LoadGlobalWaypoints()
        {
            foreach (var entry in storedData.globalWaypoints)
                globalWaypointCache.Add(new WaypointData() { name = entry.name, x = entry.x, y = entry.y, z = entry.z });
        }

        #endregion

        #region Config

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        void LoadVariables()
        {
            // Arrow settings
            arrowHeight = Convert.ToSingle(GetConfig("Arrow Settings", "Height", 100f));
            arrowLevitation = Convert.ToSingle(GetConfig("Arrow Settings", "Levitation from ground", 3f));
            arrowHeadSize = Convert.ToSingle(GetConfig("Arrow Settings", "Arrow Head Size", 3f));

            // General settings
            playerWaypointDisplaytime = Convert.ToSingle(GetConfig("Settings", "Player Display Time", 60f));
            globalRefreshTime = Convert.ToSingle(GetConfig("Settings", "Global Waypoint Refresh Time", 60f));
            maxWaypoints = Convert.ToInt32(GetConfig("Settings", "Max Waypoints", 5));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        #endregion

        bool Changed = false;
        private const string permissionName = "personalbeacon.use";
        private const string permissionNameADMIN = "personalbeacon.admin";
        Dictionary<string, List<WaypointData>> playerWaypointsCache = new Dictionary<string, List<WaypointData>>();
        List<WaypointData> globalWaypointCache = new List<WaypointData>();
        List<TimerData> timers = new List<TimerData>();
        public float arrowHeight = 50f;
        public float arrowLevitation = 3f;
        public float arrowHeadSize = 3;
        public float playerWaypointDisplaytime = 60;
        public float globalRefreshTime = 60f;
        public int maxWaypoints = 5;

        class WaypointDataStorage
        {
            public Dictionary<string, List<WaypointData>> playerWaypoints;
            public List<WaypointData> globalWaypoints;
        }

        class WaypointData
        {
            public string name;
            public float x;
            public float y;
            public float z;
        }

        class TimerData
        {
            public string name;
            public Timer timer;
            public WaypointData data;
        }

        void Loaded()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["No Permission"] = "You cannot use this command!",
                ["/setwp Invalid Syntax"] = "Invalid syntax! /setwp <waypoint name>",
                ["/wp Invalid Syntax"] = "Invalid syntax! /wp <waypoint name>",
                ["/setglobalwp Invalid Syntax"] = "Invalid syntax! /setglobalwp <waypoint name>",
                ["/hideglobalwp Invalid Syntax"] = "Invalid syntax! /hideglobalwp <waypoint name>",
                ["/removewp Invalid Syntax"] = "Invalid syntax! /removewp <waypoint name>",
                ["Waypoint Removed"] = "This waypoint has been removed!",
                ["Waypoint Already Exists"] = "This waypoint name already exists!",
                ["Waypoint Added"] = "You successfully created a new waypoint!",
                ["Waypoint Not Found"] = "Waypoint was not found",
                ["Max Waypoints Allowed"] = "You already have the maxiumum amount of waypoints allowed!",
                ["Global Waypoint Hidden"] = "This waypoint will disapear on the next update cycle",
                ["Global Waypoint Already Showing"] = "This waypoint is already being broadcasted",
                ["No Waypoints"] = "You do not have any waypoints!",
                ["List 1st Line"] = "Your current waypoints are:",
                ["List Entry"] = "- {0}",

            }, this);

            exampleData = Interface.Oxide.DataFileSystem.GetFile("PersonalBeacon");

            NextTick(() =>
            {
                foreach (var entry in globalWaypointCache)
                    InitGlobalWaypoint(entry);
            });
        }

        void OnServerInitialized() => LoadData();
        void Unload() => SaveData();
        void OnServerSave() => SaveData();

        void Init()
        {
            permission.RegisterPermission("personalbeacon.use", this);
            permission.RegisterPermission("personalbeacon.admin", this);
            LoadVariables();
        }

        [ChatCommand("setwp")]
        void setwpCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName))
            {
                player.ChatMessage(msg("No Permission", player.UserIDString));
                return;
            }
            if (args.Length != 1)
            {
                player.ChatMessage(msg("/setwp Invalid Syntax", player.UserIDString));
                return;
            }
            if (!playerWaypointsCache.ContainsKey(player.UserIDString))
                playerWaypointsCache.Add(player.UserIDString, new List<WaypointData>());
            if (playerWaypointsCache[player.UserIDString].Count == maxWaypoints)
            {
                player.ChatMessage(msg("Max Waypoints Allowed", player.UserIDString));
                return;
            }
            foreach (var entry in playerWaypointsCache[player.UserIDString])
                if (args[0] == entry.name)
                {
                    player.ChatMessage(msg("Waypoint Already Exists", player.UserIDString));
                    return;
                }
            WaypointData data = new WaypointData() { name = args[0], x = player.transform.position.x, y = player.transform.position.y, z = player.transform.position.z };
            playerWaypointsCache[player.UserIDString].Add(data);
            DrawWaypoint(player, new Vector3(data.x, data.y, data.z), playerWaypointDisplaytime, false, data.name);
        }

        [ChatCommand("wp")]
        void wpCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName))
                if (!permission.UserHasPermission(player.UserIDString, permissionNameADMIN))
                {
                    player.ChatMessage(msg("No Permission", player.UserIDString));
                    return;
                }
            if (args.Length != 1)
            {
                player.ChatMessage(msg("/wp Invalid Syntax", player.UserIDString));
                return;
            }
            if (!playerWaypointsCache.ContainsKey(player.UserIDString))
                playerWaypointsCache.Add(player.UserIDString, new List<WaypointData>());
            WaypointData data = null;
            foreach (var entry in playerWaypointsCache[player.UserIDString])
                if (entry.name == args[0])
                    data = entry;
            if (data == null)
            {
                player.ChatMessage(msg("Waypoint Not Found", player.UserIDString));
                return;
            }
            DrawWaypoint(player, new Vector3(data.x, data.y, data.z), playerWaypointDisplaytime, false, data.name);
            player.ChatMessage(msg("Waypoint Added", player.UserIDString));
        }

        [ChatCommand("removewp")]
        void removewp(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName))
                if (!permission.UserHasPermission(player.UserIDString, permissionNameADMIN))
                {
                    player.ChatMessage(msg("No Permission", player.UserIDString));
                    return;
                }
            if (args.Length != 1)
            {
                player.ChatMessage(msg("/removewp Invalid Syntax", player.UserIDString));
                return;
            }
            if (!playerWaypointsCache.ContainsKey(player.UserIDString))
                playerWaypointsCache.Add(player.UserIDString, new List<WaypointData>());
            foreach (var entry in playerWaypointsCache[player.UserIDString])
                if (args[0] == entry.name)
                {
                    playerWaypointsCache[player.UserIDString].Remove(entry);
                    player.ChatMessage(msg("Waypoint Removed", player.UserIDString));
                    return;
                }
        }

        [ChatCommand("wplist")]
        void wplistCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName))
                if (!permission.UserHasPermission(player.UserIDString, permissionNameADMIN))
                {
                    player.ChatMessage(msg("No Permission", player.UserIDString));
                    return;
                }
            if (!playerWaypointsCache.ContainsKey(player.UserIDString))
            {
                player.ChatMessage(msg("No Waypoints", player.UserIDString));
                return;
            }
            else
            {
                StringBuilder x = new StringBuilder();
                x.AppendLine(msg("List 1st Line", player.UserIDString));
                foreach (var entry in playerWaypointsCache[player.UserIDString])
                    x.AppendLine(string.Format(msg("List Entry", player.UserIDString), entry.name.ToString()));
                player.ChatMessage(x.ToString().TrimEnd());
            }
        }

        // Admin commands:

        [ChatCommand("setglobalwp")]
        void globalwpCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionNameADMIN))
            {
                player.ChatMessage(msg("No Permissions", player.UserIDString));
                return;
            }
            if (args.Length != 1)
            {
                player.ChatMessage(msg("/setglobalwp Invalid Syntax", player.UserIDString));
                return;
            }
            foreach(var entry in globalWaypointCache)
                if (entry.name == args[0])
                {
                    player.ChatMessage(msg("Waypoint Already Exists", player.UserIDString));
                    return;
                }
            WaypointData data = new WaypointData() { name = args[0], x = player.transform.position.x, y = player.transform.position.y, z = player.transform.position.z };
            globalWaypointCache.Add(data);
            InitGlobalWaypoint(data);
        }

        [ChatCommand("removeglobalwp")]
        private void RemoveGlobalWPCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionNameADMIN))
            {
                player.ChatMessage(msg("No Permissions", player.UserIDString));
                return;
            }
            if (args.Length != 1)
            {
                player.ChatMessage(msg("/setglobalwp Invalid Syntax", player.UserIDString));
                return;
            }
            bool x = false;
            WaypointData data = null;
            foreach (var entry in globalWaypointCache)
                if (entry.name == args[0])
                {
                    x = true;
                    data = entry;
                    return;
                }
            if (x == true && data != null)
            {
                TimerData tData = null;
                foreach (var _entry in timers)
                    if (args[0] == _entry.name)
                        tData = _entry;
                if (tData != null)
                {
                    tData.timer.Destroy();
                    tData.timer = null;
                    timers.Remove(tData);
                }
                globalWaypointCache.Remove(data);
                player.ChatMessage("Waypoint was removed");
            }
            else
            {
                player.ChatMessage("That waypoint does not exist");
            }
        }

        [ChatCommand("hideglobalwp")]
        void hideglobalwpCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionNameADMIN))
            {
                player.ChatMessage(msg("No Permission", player.UserIDString));
                return;
            }
            if (args.Length != 1)
            {
                player.ChatMessage(msg("/hideglobalwp Invalid Syntax", player.UserIDString));
                return;
            }
            TimerData data = null;
            foreach (var entry in timers)
                if (args[0] == entry.name)
                    data = entry;
            if (data == null)
            {
                player.ChatMessage(msg("Waypoint Not Found", player.UserIDString));
                return;
            }
            data.timer.Destroy();
            data.timer = null;
            player.ChatMessage(msg("Global Waypoint Hidden", player.UserIDString));
        }

        [ChatCommand("showglobalwp")]
        void showglobalwpCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionNameADMIN))
            {
                player.ChatMessage(msg("No Permission", player.UserIDString));
                return;
            }
            if (args.Length != 1)
            {
                player.ChatMessage(msg("/hideglobalwp Invalid Syntax", player.UserIDString));
                return;
            }
            TimerData data = null;
            foreach (var entry in timers)
                if (entry.name == args[0])
                    data = entry;
            if (data == null)
            {
                player.ChatMessage(msg("Waypoint Not Found", player.UserIDString));
                return;
            }
            if (data.timer != null)
            {
                player.ChatMessage(msg("Global Waypoint Already Showing", player.UserIDString));
                return;
            }
            foreach (BasePlayer _player in BasePlayer.activePlayerList)
                DrawWaypoint(_player, new Vector3(data.data.x, data.data.y, data.data.z), globalRefreshTime, true, data.name);
            data.timer = timer.Repeat(globalRefreshTime, 0, () =>
            {
                foreach (BasePlayer _player in BasePlayer.activePlayerList)
                    DrawWaypoint(_player, new Vector3(data.data.x, data.data.y, data.data.z), globalRefreshTime, true, data.name);
            });
        }

        [ChatCommand("wpshowall")]
        void cmdShowAllBeacons(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionNameADMIN))
            {
                player.ChatMessage(msg("No Permission", player.UserIDString));
                return;
            }

            foreach (var entry in playerWaypointsCache)
                foreach (var _entry in playerWaypointsCache[entry.Key])
                    DrawWaypoint(player, new Vector3(_entry.x, _entry.y, _entry.z), playerWaypointDisplaytime, false, _entry.name);
        }

        void InitGlobalWaypoint(WaypointData data)
        {
            foreach (BasePlayer _player in BasePlayer.activePlayerList)
                DrawWaypoint(_player, new Vector3(data.x, data.y, data.z), globalRefreshTime, true, data.name);

            Timer repeat = timer.Repeat(globalRefreshTime, 0, () =>
            {
                foreach (BasePlayer _player in BasePlayer.activePlayerList)
                    DrawWaypoint(_player, new Vector3(data.x, data.y, data.z), globalRefreshTime, true, data.name);
            });
            timers.Add(new TimerData() { timer = repeat, name = data.name, data = data });
        }

        void DrawWaypoint(BasePlayer player, Vector3 pos, float time, bool isGlobal, string name)
        {
            var color = Color.blue;
            if (isGlobal)
                color = Color.red;

            if (!player.HasPlayerFlag(BasePlayer.PlayerFlags.IsAdmin))
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                player.SendNetworkUpdateImmediate();
                player.SendConsoleCommand("ddraw.arrow", time, color, new Vector3(pos.x, pos.y + arrowHeight + arrowLevitation, pos.z), new Vector3(pos.x, pos.y + arrowLevitation, pos.z), arrowHeadSize);
                player.SendConsoleCommand("ddraw.text", time, color, new Vector3(pos.x, pos.y + arrowHeight + arrowLevitation + 5f, pos.z), name);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                player.SendNetworkUpdate();
            }
            else
            {
                player.SendConsoleCommand("ddraw.arrow", time, color, new Vector3(pos.x, pos.y + arrowHeight + arrowLevitation, pos.z), new Vector3(pos.x, pos.y + arrowLevitation, pos.z), arrowHeadSize);
                player.SendConsoleCommand("ddraw.text", time, color, new Vector3(pos.x, pos.y + arrowHeight + arrowLevitation + 5f, pos.z), name);
            }
                    return;
        }

        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        string msg(string key, string id = null) => lang.GetMessage(key, this, id);
    }
}