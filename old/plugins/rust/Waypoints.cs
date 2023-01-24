using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Waypoints", "RFC1920", "1.1.3", ResourceId = 982)]
    class Waypoints : RustPlugin
    {
        void Loaded()
        {
            LoadData();
        }

        private DynamicConfigFile data;
        private Dictionary<string, Waypoint> waypoints;

        void SaveData()
        {
            data.WriteObject(waypoints);
        }

        void LoadData()
        {
            try
            {
                data = Interface.Oxide.DataFileSystem.GetFile(nameof(Waypoints));
                data.Settings.Converters = new JsonConverter[] {new UnityVector3Converter()};
                waypoints = data.ReadObject<Dictionary<string, Waypoint>>();
                waypoints = waypoints.ToDictionary(w => w.Key.ToLower(), w => w.Value);
            }
            catch
            {
                waypoints = new Dictionary<string, Waypoint>();
            }
        }

        class WaypointInfo
        {
            [JsonProperty("p")]
            public Vector3 Position;
            [JsonProperty("s")]
            public float Speed;

            public WaypointInfo(Vector3 position, float speed)
            {
                Position = position;
                Speed = speed;
            }
        }

        #region Message
        private string _(string msgId, BasePlayer player, params object[] args)
        {
            var msg = lang.GetMessage(msgId, this, player?.UserIDString);
            return args.Length > 0 ? string.Format(msg, args) : msg;
        }

        private void PrintMsgL(BasePlayer player, string msgId, params object[] args)
        {
            if(player == null) return;
            PrintMsg(player, _(msgId, player, args));
        }

        private void PrintMsg(BasePlayer player, string msg)
        {
            if(player == null) return;
            SendReply(player, $"{msg}");
        }
        #endregion

        void Init()
        {
            AddCovalenceCommand("waypoints_new", "cmdWaypointsNew");
            AddCovalenceCommand("waypoints_add", "cmdWaypointsAdd");
            AddCovalenceCommand("waypoints_list", "cmdWaypointsList");
            AddCovalenceCommand("waypoints_remove", "cmdWaypointsRemove");
            AddCovalenceCommand("waypoints_save", "cmdWaypointsSave");
            AddCovalenceCommand("waypoints_close", "cmdWaypointsClose");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["notauthorized"] = "You don't have access to this command",
                ["wpediting"] = "You are already editing {0}",
                ["notediting"] = "You are not editing any waypoints, say /waypoints_new or /waypoints_edit NAME",
                ["listcreated"] = "Waypoints: New WaypointList created, you may now add waypoints.",
                ["wpadded"] = "Waypoint Added: {0} {1} {2} - Speed: {3}",
                ["nowp"] = "No waypoints created yet",
                ["wpheader"] = "==== Waypoints ====",
                ["wplist"] = "/waypoints_list to get the list of waypoints",
                ["wpnotexist"] = "Waypoint {0} doesn't exist",
                ["wpremoved"] = "Waypoints: {0} was removed",
                ["wpsave"] = "Waypoints: /waypoints_save NAMEOFWAYPOINT",
                ["wperror"] = "Waypoints: Something went wrong while getting your WaypointList",
                ["wpsaved"] = "Waypoints: New waypoint saved with: {0} with {1} waypoints stored",
                ["wpclose"] = "Waypoints: Closed without saving"
            }, this);
        }

        class Waypoint
        {
            public string Name;
            public List<WaypointInfo> Waypoints;

            public Waypoint()
            {
                Waypoints = new List<WaypointInfo>();
            }
            public void AddWaypoint(Vector3 position, float speed)
            {
                Waypoints.Add(new WaypointInfo(position, speed));
            }
        }

        class WaypointEditor : MonoBehaviour
        {
            public Waypoint targetWaypoint;

            void Awake()
            {
            }
        }

        [HookMethod("GetWaypointsList")]
        object GetWaypointsList(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            Waypoint waypoint;
            if (!waypoints.TryGetValue(name.ToLower(), out waypoint)) return null;
            var returndic = new List<object>();

            foreach(var wp in waypoint.Waypoints)
            {
                returndic.Add(new Dictionary<Vector3, float> { { wp.Position, wp.Speed } });
            }
            return returndic;
        }

        bool hasAccess(BasePlayer player)
        {
            if (player.net.connection.authLevel < 1)
            {
                PrintMsgL(player, "notauthorized");
                return false;
            }
            return true;
        }

        bool isEditingWP(BasePlayer player, int ttype)
        {
            if (player.GetComponent<WaypointEditor>() != null)
            {
                if (ttype == 0) PrintMsgL(player, "wpediting", player.GetComponent<WaypointEditor>().targetWaypoint.Name);
                return true;
            }
            else
            {
                if (ttype == 1) PrintMsgL(player, "notediting");
                return false;
            }
        }

        #region commands
        [Command("waypoints_new")]
        void cmdWaypointsNew(IPlayer iplayer, string command, string[] args)
        {
            var player = iplayer.Object as BasePlayer;
            if (!hasAccess(player)) return;
            if (isEditingWP(player, 0)) return;

            var newWaypointEditor = player.gameObject.AddComponent<WaypointEditor>();
            newWaypointEditor.targetWaypoint = new Waypoint();
            PrintMsgL(player, "listcreated");
        }

        [Command("waypoints_add")]
        void cmdWaypointsAdd(IPlayer iplayer, string command, string[] args)
        {
            var player = iplayer.Object as BasePlayer;
            if (!hasAccess(player)) return;
            if (!isEditingWP(player, 1)) return;
            var WaypointEditor = player.GetComponent<WaypointEditor>();
            if (WaypointEditor.targetWaypoint == null)
            {
                PrintMsgL(player, "wperror");
                return;
            }
            float speed = 3f;
            if (args.Length > 0) float.TryParse(args[0], out speed);
            WaypointEditor.targetWaypoint.AddWaypoint(player.transform.position, speed);

            PrintMsgL(player, "wpadded",  player.transform.position.x, player.transform.position.y, player.transform.position.z, speed);
        }

        [Command("waypoints_list")]
        void cmdWaypointsList(IPlayer iplayer, string command, string[] args)
        {
            var player = iplayer.Object as BasePlayer;
            if (!hasAccess(player)) return;
            if (waypoints.Count == 0)
            {
                PrintMsgL(player, "nowp");
                return;
            }
            PrintMsgL(player, "wpheader");
            foreach (var pair in waypoints)
            {
                SendReply(player, pair.Key);
            }
        }

        [Command("waypoints_remove")]
        void cmdWaypointsRemove(IPlayer iplayer, string command, string[] args)
        {
            var player = iplayer.Object as BasePlayer;
            if (!hasAccess(player)) return;
            if (waypoints.Count == 0)
            {
                PrintMsgL(player, "nowp");
                return;
            }
            if(args.Length == 0)
            {
                PrintMsgL(player, "wplist");
                return;
            }
            if (!waypoints.Remove(args[0]))
            {
                PrintMsgL(player, "wpnotexist", args[0]);
                return;
            }
            SaveData();
            PrintMsgL(player, "wpremoved", args[0]);
        }

        [Command("waypoints_save")]
        void cmdWaypointsSave(IPlayer iplayer, string command, string[] args)
        {
            var player = iplayer.Object as BasePlayer;
            if (!hasAccess(player)) return;
            if (!isEditingWP(player, 1)) return;
            if (args.Length == 0)
            {
                PrintMsgL(player, "wpsave");
                return;
            }
            var WaypointEditor = player.GetComponent<WaypointEditor>();
            if (WaypointEditor.targetWaypoint == null)
            {
                PrintMsgL(player, "wperror");
                return;
            }

            var name = args[0];
            WaypointEditor.targetWaypoint.Name = name;
            waypoints[name.ToLower()] = WaypointEditor.targetWaypoint;
            PrintMsgL(player, "wpsaved", WaypointEditor.targetWaypoint.Name, WaypointEditor.targetWaypoint.Waypoints.Count);
            UnityEngine.Object.Destroy(player.GetComponent<WaypointEditor>());
            SaveData();
        }

        [Command("waypoints_close")]
        void cmdWaypointsClose(IPlayer iplayer, string command, string[] args)
        {
            var player = iplayer.Object as BasePlayer;
            if (!hasAccess(player)) return;
            if (!isEditingWP(player, 1)) return;
            PrintMsgL(player, "wpclosed");
            UnityEngine.Object.Destroy(player.GetComponent<WaypointEditor>());
        }
        #endregion

        private class UnityVector3Converter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var vector = (Vector3)value;
                writer.WriteValue($"{vector.x} {vector.y} {vector.z}");
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.String)
                {
                    var values = reader.Value.ToString().Trim().Split(' ');
                    return new Vector3(Convert.ToSingle(values[0]), Convert.ToSingle(values[1]), Convert.ToSingle(values[2]));
                }
                var o = JObject.Load(reader);
                return new Vector3(Convert.ToSingle(o["x"]), Convert.ToSingle(o["y"]), Convert.ToSingle(o["z"]));
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Vector3);
            }
        }
    }
}
