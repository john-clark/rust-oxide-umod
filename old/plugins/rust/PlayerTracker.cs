using System;
using System.Collections.Generic;
using UnityEngine;
using Time = UnityEngine.Time;

namespace Oxide.Plugins
{
    [Info("PlayerTracker", "redBDGR", "1.0.1", ResourceId = 2584)]
    [Description("Easily track the movements of players")]

    class PlayerTracker : RustPlugin
    {
        private static PlayerTracker plugin;
        private bool Changed;

        private float intervalTime = 1f;
        private float drawLength = 120f;
        private bool clearPositionsOnCheck = true;
        private bool trackAllPlayers;
        private float startDeleteTime = 600f;

        // Arrows
        private float arrowHead = 0.5f;

        private const string permissionName = "playertracker.admin";

        private void Init()
        {
            plugin = this;
            LoadVariables();
            permission.RegisterPermission(permissionName, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["No Permission"] = "You are not allowed to use this command",
                ["Invalid Syntax"] = "Invalid Syntax! /track <start | stop | show> <playername | id> <length>",
                ["Starttrack Invalid Syntax"] = "Invalid Syntax! /starttrack <playername | id>",
                ["Tracker Started"] = "You have started tracking {0}",
                ["Tracker Stopped"] = "You have stopped tracking {0}",
                ["Not Being Tracked"] = "This player is not currently being tracked",
                ["No Player Found"] = "No players were found with this name/id",
                ["Already Being Tracked"] = "This player is already being tacked",
            }, this);

            if (!trackAllPlayers) return;
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                Tracker tracker = player.GetComponent<Tracker>();
                if (tracker)
                    return;
                player.gameObject.AddComponent<Tracker>();
            }
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        void LoadVariables()
        {
            // General Settings
            intervalTime = Convert.ToSingle(GetConfig("Settings", "Position Interval Time", 2f));
            trackAllPlayers = Convert.ToBoolean(GetConfig("Settings", "Track All Players", false)); // Can cause lag on larger servers, especially when viewing a players locations
            startDeleteTime = Convert.ToSingle(GetConfig("Settings", "Remove Entries after x seconds", 600f)); // Can help reduce lag by shrinking list sizes

            // Arrow Settings
            arrowHead = Convert.ToSingle(GetConfig("Arrow Settings", "Arrow Head Size", 0.5f));
            drawLength = Convert.ToSingle(GetConfig("Arrow Settings", "Default Display Length", 120f));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                Tracker tracker = player.GetComponent<Tracker>();
                if (tracker)
                    UnityEngine.Object.Destroy(tracker);
                return;
            }
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!trackAllPlayers) return;
            Tracker tracker = player.GetComponent<Tracker>();
            if (!tracker)
                player.gameObject.AddComponent<Tracker>();
        }

        [ChatCommand("track")]
        private void TrackCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName))
            {
                player.ChatMessage(msg("No Permission", player.UserIDString));
                return;
            }
            switch (args.Length)
            {
                case 0:
                {
                    player.ChatMessage(msg("Invalid Syntax", player.UserIDString));
                    return;
                }
                case 1:
                {
                    player.ChatMessage(msg("Invalid Syntax", player.UserIDString));
                    break;
                }
                case 2:
                {
                    BasePlayer target = BasePlayer.Find(args[1]);
                    if (target == null)
                    {
                        player.ChatMessage(msg("No Player Found", player.UserIDString));
                        return;
                    }
                    switch (args[0])
                    {
                        case "start":
                        {
                            Tracker tracker = target.GetComponent<Tracker>();
                            if (!tracker)
                            {
                                target.gameObject.AddComponent<Tracker>();
                                player.ChatMessage(string.Format(msg("Tracker Started", player.UserIDString), target.displayName));
                            }
                            break;
                        }
                        case "stop":
                        {
                            Tracker tracker = target.GetComponent<Tracker>();
                            if (tracker)
                            {
                                UnityEngine.Object.Destroy(tracker);
                                player.ChatMessage(string.Format(msg("Tracker Stopped", player.UserIDString), target.displayName));
                                return;
                            }
                            player.ChatMessage(msg("Not Being Tracked", player.UserIDString));
                            break;
                        }
                        case "show":
                        {
                            Tracker tracker = target.GetComponent<Tracker>();
                            if (!tracker)
                            {
                                player.ChatMessage(msg("Not Being Tracked", player.UserIDString));
                                return;
                            }
                            for (int i = 0; i < tracker.locationList.Count - 1; i++)
                                DoDraws(player, drawLength, tracker.locationList[i], tracker.locationList[i + 1]);
                            if (clearPositionsOnCheck)
                                tracker.locationList.Clear();
                            break;
                        }
                        default:
                        {
                            player.ChatMessage(msg("Invalid Syntax", player.UserIDString));
                            return;
                        }
                    }
                    break;
                    }
                case 3:
                {
                    BasePlayer target = BasePlayer.Find(args[1]);
                    if (target == null)
                    {
                        player.ChatMessage(msg("No Player Found", player.UserIDString));
                        return;
                    }
                    switch (args[0])
                    {
                        case "start":
                        {
                            Tracker tracker = target.GetComponent<Tracker>();
                            if (!tracker)
                            {
                                target.gameObject.AddComponent<Tracker>();
                                player.ChatMessage(msg("Tracker Started", player.UserIDString));
                            }
                        }
                            break;
                        case "stop":
                        {
                            Tracker tracker = target.GetComponent<Tracker>();
                            if (tracker)
                            {
                                UnityEngine.Object.Destroy(tracker);
                                player.ChatMessage(string.Format(msg("Tracker Stopped", player.UserIDString), target.displayName));
                                return;
                            }
                            player.ChatMessage(msg("Not Being Tracked", player.UserIDString));
                        }
                            break;
                        case "show":
                        {
                            Tracker tracker = target.GetComponent<Tracker>();
                            if (!tracker)
                            {
                                player.ChatMessage(msg("Not Being Tracked", player.UserIDString));
                                return;
                            }
                            for (int i = 0; i < tracker.locationList.Count - 1; i++)
                                DoDraws(player, Convert.ToSingle(args[1]), tracker.locationList[i], tracker.locationList[i + 1]);
                            if (clearPositionsOnCheck)
                                tracker.locationList.Clear();
                        }
                            break;
                        default:
                        {
                            player.ChatMessage(msg("Invalid Syntax", player.UserIDString));
                            return;
                        }
                    }
                    break;
                }
                default:
                {
                    player.ChatMessage(msg("No Player Found", player.UserIDString));
                    return;
                }
            }
        }

        private class Tracker : MonoBehaviour
        {
            private BasePlayer player;
            private float nextTime;
            private readonly float intervalTime = plugin.intervalTime;
            public List<Vector3> locationList = new List<Vector3>();
            private float startDeleteTime;
            private Vector3 lastPos;

            private void Awake()
            {
                player = gameObject.GetComponent<BasePlayer>();
                lastPos = player.transform.position;
                if (plugin.startDeleteTime == 0)
                    startDeleteTime = Mathf.Infinity;
                else
                    startDeleteTime = Time.time + plugin.startDeleteTime;
            }

            private void Update()
            {
                if (Time.time < nextTime) return;
                if (locationList.Count > 0)
                {
                    if (Vector3.Distance(lastPos, player.transform.position) < 1f)
                        return;
                }
                lastPos = player.transform.position;
                locationList.Add(player.transform.position + new Vector3(0f, 0.6f, 0f));
                nextTime = Time.time + intervalTime;
                if (!(Time.time > startDeleteTime)) return;
                locationList.Remove(locationList[0]);
            }
        }

        private void DoDraws(BasePlayer player, float length, Vector3 pos1, Vector3 pos2)
        {
            if (player.IsAdmin)
            {
                player.SendConsoleCommand("ddraw.arrow", length, Color.red, pos1, pos2, arrowHead);
            }
            else
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                player.SendNetworkUpdateImmediate();
                player.SendConsoleCommand("ddraw.arrow", length, Color.red, pos1, pos2, arrowHead);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                player.SendNetworkUpdateImmediate();
            }
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (data.TryGetValue(datavalue, out value)) return value;
            value = defaultValue;
            data[datavalue] = value;
            Changed = true;
            return value;
        }

        string msg(string key, string id = null) => lang.GetMessage(key, this, id);
    }
}