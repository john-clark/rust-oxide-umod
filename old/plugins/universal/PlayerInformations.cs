using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("PlayerInformations", "Reneb", "1.2.10", ResourceId = 1940)]
    [Description("Logs players informations.")]
    public class PlayerInformations : CovalencePlugin
    {
        [PluginReference]
        Plugin PlayerDatabase;

        #region Fields

        static DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);

        #endregion

        #region Methods

        string GetMsg(string key, object steamid = null) => lang.GetMessage(key, this, steamid.ToString());

        bool IsConnected(string steamid) => players.Connected.Where(x => x.Id == steamid).Count() > 0;

        string NormalizeIP(string ip)
        {
            if (!ip.Contains(":")) return ip;
            return ip.Substring(0, ip.LastIndexOf(":"));
        }

        void SendHelpText(IPlayer player) { player.Reply(HelpText(player.Id)); }

        string HelpText(string steamid)
        {
            string msg = string.Empty;
            if (hasPermission(steamid, IPpermission, IPauthlevel)) { msg += "\r\n<color=\"#ffd479\">/lastips steamid/name</color> - get the last ips used by a user\r\n<color=\"#ffd479\">/ipowners XX.XX.XX.XX </color>- know what players used this ip"; }
            if (hasPermission(steamid, LSpermission, LSauthlevel)) { msg += "\r\n<color=\"#ffd479\">/lastseen steamid/name</color> - know when was this player last seen online"; }
            if (hasPermission(steamid, LPpermission, LPauthlevel)) { msg += "\r\n<color=\"#ffd479\">/lastposition steamid/name</color> - know where is the last position of a player"; }
            if (hasPermission(steamid, FCpermission, FCauthlevel)) { msg += "\r\n<color=\"#ffd479\">/firstconnection steamid/name</color> - know when was this player first seen online"; }
            if (hasPermission(steamid, TPpermission, TPauthlevel)) { msg += "\r\n<color=\"#ffd479\">/played steamid/name</color> - know how much time a player has played on this server"; }
            if (hasPermission(steamid, NAMESpermission, NAMESauthlevel)) { msg += "\r\n<color=\"#ffd479\">/lastnames steamid/name</color> - know the last names used by a user"; }
            if (msg != string.Empty)
            {
                msg = "<size=18>Players Information</size>" + msg;
            }
            return msg;
        }

        bool hasPermission(string steamid, string permissionName, int authLevel)
        {
            if (steamid == "server_console") return true;

            var player = players.FindPlayer(steamid);
            if (player != null && player.IsConnected)
            {
                if (player.IsAdmin) return true;
#if RUST
                var baseplayer = (BasePlayer)(player?.Object);
                if(baseplayer != null)
                {
                    if (baseplayer.net.connection.authLevel >= authLevel) return true;
                }
#endif
            }

            return permission.UserHasPermission(steamid, permissionName);
        }

        void OnPlayerJoined(string steamid, string name, string ip)
        {
            if (IPuse)
                RecordIP(steamid, ip);
            if (NAMESuse)
                RecordName(steamid, name);
            if (FCuse)
                RecordFirstConnection(steamid);
            if (TPuse)
                StartRecordTime(steamid);
        }

        void OnPlayerLeave(IPlayer player)
        {
            var steamid = player.Id.ToString();

            if (LSuse)
                RecordLastSeen(steamid);
            if (TPuse)
                EndRecordTime(steamid);
            if (LPuse)
                RecordPosition(steamid, player.IsConnected ? player.Position().X : 0f, player.IsConnected ? player.Position().Y : 0f, player.IsConnected ? player.Position().Z : 0f);
        }

        static double LogTime() { return DateTime.UtcNow.Subtract(epoch).TotalSeconds; }

        string TimeMinToString(string time) { return TimeMinToString(double.Parse(time)); }

        string TimeMinToString(double time)
        {
            TimeSpan timespan = TimeSpan.FromSeconds(time);
            DateTime date = new DateTime(1970, 1, 1, 0, 0, 0) + timespan;
            return string.Format("{0}:{1} {2}/{3}/{4}", date.Hour.ToString(), date.Minute.ToString(), date.Month.ToString(), date.Day.ToString(), date.Year.ToString());
        }

        string SecondsToString(string time) { return SecondsToString(decimal.Parse(time)); }
        string SecondsToString(decimal time)
        {
            decimal days = Math.Floor(time / 86400);
            time -= days * 86400;
            decimal hours = Math.Floor(time / 3600);
            time -= hours * 3600;
            decimal minutes = Math.Floor(time / 60);
            time -= minutes * 60;
            return string.Format("{0}d {1}h {2}m {3}s", days.ToString(), hours.ToString(), minutes.ToString(), Math.Floor(time).ToString());
        }

        private object FindPlayer(string arg)
        {
            string success = PlayerDatabase.Call("FindPlayer", arg) as string;
            if (success.Length == 17)
            {
                return ulong.Parse(success);
            }
            else
                return success;
        }

        #endregion

        #region Configs

        private static bool IPuse = true;
        private static string IPpermission = "playerinformations.ips";
        private static int IPauthlevel = 1;
        private static int IPmaxLogs = 5;

        private static bool NAMESuse = true;
        private static string NAMESpermission = "playerinformations.names";
        private static int NAMESauthlevel = 1;
        private static int NAMESmaxLogs = 5;

        private static bool FCuse = true;
        private static string FCpermission = "playerinformations.firstconnection";
        private static int FCauthlevel = 1;

        private static bool LSuse = true;
        private static string LSpermission = "playerinformations.lastseen";
        private static int LSauthlevel = 1;

        private static bool LPuse = true;
        private static string LPpermission = "playerinformations.lastposition";
        private static int LPauthlevel = 1;

        private static bool TPuse = true;
        private static string TPpermission = "playerinformations.timeplayed";
        private static int TPauthlevel = 0;

        void Init()
        {
            permission.RegisterPermission(FCpermission, this);
            permission.RegisterPermission(IPpermission, this);
            permission.RegisterPermission(LPpermission, this);
            permission.RegisterPermission(LSpermission, this);
            permission.RegisterPermission(NAMESpermission, this);
            permission.RegisterPermission(TPpermission, this);

            CheckCfg<bool>("IP Logs - activated", ref IPuse);
            CheckCfg<string>("IP Logs - Permission - oxide permission", ref IPpermission);
            CheckCfg<int>("IP Logs - Permission - authlevel - Rust ONLY", ref IPauthlevel);
            CheckCfg<int>("IP Logs - Max Logs per player", ref IPmaxLogs);

            CheckCfg<bool>("Names Logs - activated", ref NAMESuse);
            CheckCfg<string>("Names Logs - Permission - oxide permission", ref NAMESpermission);
            CheckCfg<int>("Names Logs - Permission - authlevel - Rust ONLY", ref NAMESauthlevel);
            CheckCfg<int>("Names Logs - Max Logs per player", ref NAMESmaxLogs);

            CheckCfg<bool>("First Connection - activated", ref FCuse);
            CheckCfg<string>("First Connection - Permission - oxide permission", ref FCpermission);
            CheckCfg<int>("First Connection - Permission - authlevel - Rust ONLY", ref FCauthlevel);

            CheckCfg<bool>("Last Seen - activated", ref LSuse);
            CheckCfg<string>("Last Seen - Permission - oxide permission", ref LSpermission);
            CheckCfg<int>("Last Seen - Permission - authlevel - Rust ONLY", ref LSauthlevel);

            CheckCfg<bool>("Last Position - activated", ref LPuse);
            CheckCfg<string>("Last Position - Permission - oxide permission", ref LPpermission);
            CheckCfg<int>("Last Position - Permission - authlevel - Rust ONLY", ref LPauthlevel);

            CheckCfg<bool>("Time Played - activated", ref TPuse);
            CheckCfg<string>("Time Played - Permission - oxide permission", ref TPpermission);
            CheckCfg<int>("Time Played - Permission - authlevel - Rust ONLY", ref TPauthlevel);

            SaveConfig();
        }

        protected override void LoadDefaultConfig() { }

        void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }

        void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"You don't have permission to use this command.", "You don't have permission to use this command."},
                {"IPs aren't recorded.","IPs aren't recorded."},
                {"/lastips STEAMID/NAME", "/lastips STEAMID/NAME"},
                {"Couldn't find a player that matches this name.", "Couldn't find a player that matches this name."},
                {"No logs for this player.", "No logs for this player."},
                {"/ipowners XX.XX.XX.XX", "/ipowners XX.XX.XX.XX"},
                {"Couldn't get the list of players", "Couldn't get the list of players"},
                {"This command has been deactivated.", "This command has been deactivated."},
                {"/lastseen STEAMID/NAME", "/lastseen STEAMID/NAME"},
                { "This player is connected!",  "This player is connected!"},
                {"/firstconnection STEAMID/NAME", "/firstconnection STEAMID/NAME"},
                { "/lastnames STEAMID/NAME",  "/lastnames STEAMID/NAME"},
                {"/played STEAMID/NAME", "/played STEAMID/NAME"},
                {"/lastposition STEAMID/NAME","/lastposition STEAMID/NAME"},
                {"{0} - {1} last known position is: {2} {3} {4}","{0} - {1} last known position is: {2} {3} {4}"},
                {"{0} - {1} was last seen: {2}","{0} - {1} was last seen: {2}" },
                {"{0} - {1} played: {2}","{0} - {1} played: {2}" }
            }, this);
        }
        #endregion

        #region OxideHooks

        void OnServerInitialized()
        {
            if (PlayerDatabase == null)
            {
                timer.Once(0.01f, () => Interface.Oxide.UnloadPlugin("PlayerInformations"));
                return;
            }
            if (TPuse)
            {
                StartRecordTimeAll();
            }
        }

        void Unload()
        {
            if (TPuse)
            {
                EndRecordTimeAll();
            }
        }

        void OnUserConnected(IPlayer player) { OnPlayerJoined(player.Id, player.Name, NormalizeIP(player.Address)); }

        void OnUserDisconnected(IPlayer player) { OnPlayerLeave(player); }

        #endregion

        #region Played

        Dictionary<string, double> recordPlayTime = new Dictionary<string, double>();

        [Command("played")]
        void cmdChatPlayed(IPlayer player, string command, string[] args)
        {
            string answer = CMD_chatPlayed(player.Id, args);
            player.Reply(answer);
        }

        string CMD_chatPlayed(string steamid, string[] args)
        {
            if (!hasPermission(steamid, TPpermission, TPauthlevel)) { return GetMsg("You don't have permission to use this command.", steamid); }
            if (!IPuse) { return GetMsg("This command has been deactivated.", steamid); }
            if (args.Length == 0)
            {
                return GetMsg("/played STEAMID/NAME", steamid);
            }
            var findplayer = FindPlayer(args[0]);
            if (!(findplayer is ulong))
            {
                return findplayer is string ? (string)findplayer : GetMsg("Couldn't find a player that matches this name.", steamid);
            }

            var name = (string)PlayerDatabase.Call("GetPlayerData", findplayer.ToString(), "name") ?? "Unknown";
            var totaltime = TimePlayed(findplayer.ToString());

            return string.Format(GetMsg("{0} - {1} played: {2}",steamid), name, findplayer.ToString(), SecondsToString(totaltime.ToString()));
        }

        void StartRecordTimeAll()
        {
            foreach (IPlayer player in players.Connected)
            {
                StartRecordTime(player.Id);
            }
        }
        void EndRecordTimeAll()
        {
            foreach (IPlayer player in players.Connected)
            {
                EndRecordTime(player.Id);
            }
        }

        double LastRecordTime(string steamid)
        {
            var success = PlayerDatabase.Call("GetPlayerData", steamid, "Time Played");
            if(success is string)
            {
                return double.Parse((string)success);
            }
            return 0.0;
        }
        double SessionRecordTime(string steamid)
        {
            if (recordPlayTime.ContainsKey(steamid))
            {
                return LogTime() - recordPlayTime[steamid];
            }
            else return 0.0;
        }

        double TimePlayed(string steamid)
        {
            return SessionRecordTime(steamid) + LastRecordTime(steamid);
        }

        void StartRecordTime(string steamid)
        {
            if (recordPlayTime.ContainsKey(steamid))
                recordPlayTime.Remove(steamid);
            recordPlayTime.Add(steamid, LogTime());
        }

        void EndRecordTime(string steamid)
        {
            if (!recordPlayTime.ContainsKey(steamid)) return;

            var totaltime = TimePlayed(steamid);

            PlayerDatabase.Call("SetPlayerData", steamid, "Time Played", totaltime.ToString());
        }

        #endregion

        #region Position

        [Command("lastposition")]
        void cmdChatLastPosition(IPlayer player, string command, string[] args)
        {
            string answer = CMD_chatLastPosition(player.Id, args);
            player.Reply(answer);
        }

        GenericPosition FindPosition(string steamid)
        {
            GenericPosition position = FindCurrentPosition(steamid);
            if(position == default(GenericPosition))
            {
                position = FindLastPosition(steamid);
            }
            return position;
        }

        GenericPosition FindCurrentPosition(string steamid)
        {
            var player = players.FindPlayer(steamid);
            if (player == null) return default(GenericPosition);
            return player.Position();
        }

        GenericPosition FindLastPosition(string steamid)
        {
            GenericPosition LastPos = default(GenericPosition);
            var success = PlayerDatabase.Call("GetPlayerDataRaw", steamid, "Last Position");
            if (success is string)
            {
                LastPos = JsonConvert.DeserializeObject<GenericPosition>((string)success);
            }
            return LastPos;
        }

        void RecordPosition(string steamid, float x, float y, float z)
        {
            var LastPos = new GenericPosition(x, y, z);
            PlayerDatabase.Call("SetPlayerData", steamid, "Last Position", LastPos);
        }

        string CMD_chatLastPosition(string steamid, string[] args)
        {
            if (!hasPermission(steamid, LSpermission, LSauthlevel)) { return GetMsg("You don't have permission to use this command.", steamid); }
            if (!IPuse) { return GetMsg("This command has been deactivated.", steamid); }
            if (args.Length == 0) { return GetMsg("/lastposition STEAMID/NAME", steamid); }

            var findplayer = FindPlayer(args[0]);
            if (!(findplayer is ulong))
            {
                return findplayer is string ? (string)findplayer : GetMsg("Couldn't find a player that matches this name.", steamid);
            }

            var name = (string)PlayerDatabase.Call("GetPlayerData", findplayer.ToString(), "name") ?? "Unknown";

            var lastKnownPosition = FindPosition(findplayer.ToString());
            if(lastKnownPosition == default(GenericPosition))
            {
                return GetMsg("No logs for this player.", steamid);
            }

            return string.Format(GetMsg("{0} - {1} last known position is: {2} {3} {4}",steamid), findplayer.ToString(), name, lastKnownPosition.X.ToString(), lastKnownPosition.Y.ToString(), lastKnownPosition.Z.ToString());
        }

        #endregion

        #region Lastseen

        [Command("lastseen")]
        void cmdChatLastseen(IPlayer player, string command, string[] args)
        {
            string answer = CMD_chatLastseen(player.Id, args);
            player.Reply(answer);
        }

        void RecordLastSeen(string steamid)
        {
            PlayerDatabase.Call("SetPlayerData", steamid, "Last Seen", LogTime().ToString());
        }

        float LastSeen(string steamid)
        {
            if (IsConnected(steamid))
            {
                return -1f;
            }
            var success = PlayerDatabase.Call("GetPlayerData", steamid, "Last Seen");
            if (!(success is string))
            {
                return 0f;
            }
            return float.Parse((string)success);
        }

        string CMD_chatLastseen(string steamid, string[] args)
        {
            if (!hasPermission(steamid, LSpermission, LSauthlevel)) { return GetMsg("You don't have permission to use this command.", steamid); }
            if (!IPuse) { return GetMsg("This command has been deactivated.", steamid); }

            if (args.Length == 0)
            {
                return GetMsg("/lastseen STEAMID/NAME", steamid);
            }

            var findplayer = FindPlayer(args[0]);
            if (!(findplayer is ulong))
            {
                return findplayer is string ? (string)findplayer : GetMsg("Couldn't find a player that matches this name.", steamid);
            }

            float lastSeen = LastSeen(findplayer.ToString());
            if(lastSeen == -1f)
            {
                return GetMsg("This player is connected!", steamid);
            }
            if(lastSeen == 0f)
            {
                return GetMsg("No logs for this player.", steamid);
            }
            var name = (string)PlayerDatabase.Call("GetPlayerData", findplayer.ToString(), "name") ?? "Unknown";
            return string.Format(GetMsg("{0} - {1} was last seen: {2}",steamid), name, findplayer.ToString(), TimeMinToString(lastSeen.ToString()));
        }

        #endregion

        #region IPS

        [Command("lastips")]
        void cmdChatLastIps(IPlayer player, string command, string[] args)
        {
            string answer = CMD_lastIps(player.Id, args);
            player.Reply(answer);
        }

        [Command("ipowners")]
        void cmdChatIpOwners(IPlayer player, string command, string[] args)
        {
            string answer = CMD_chatIps(player.Id, args);
            player.Reply(answer);
        }

        void RecordIP(string steamid, string playerip)
        {
            if (string.IsNullOrEmpty(playerip) || string.IsNullOrEmpty(steamid))
            {
                return;
            }

            var IPlist = new List<string>();

            var success = PlayerDatabase?.Call("GetPlayerData", steamid, "IPs");

            if (success is List<string>)
                IPlist = (List<string>)success;

            if (IPlist.Contains(playerip)) return;

            if (IPlist.Count >= IPmaxLogs)
            {
                for (int i = 0; i < (IPlist.Count - IPmaxLogs + 1); i++)
                {
                    IPlist.RemoveAt(0);
                }
            }
            IPlist.Add(playerip);
            PlayerDatabase.Call("SetPlayerData", steamid, "IPs", IPlist);
        }

        string CMD_lastIps(string steamid, string[] args)
        {
            if (!hasPermission(steamid, IPpermission, IPauthlevel)) { return GetMsg("You don't have permission to use this command.", steamid); }
            if (!IPuse) { return GetMsg("IPs aren't recorded.", steamid); }
            if (args.Length == 0)
            {
                return GetMsg("/lastips STEAMID/NAME", steamid);
            }
            var findplayer = FindPlayer(args[0]);
            if (!(findplayer is ulong))
            {
                return findplayer is string ? (string)findplayer : GetMsg("Couldn't find a player that matches this name.", steamid);
            }
            var IPlist = new List<string>();
            var success = PlayerDatabase.Call("GetPlayerDataRaw", findplayer.ToString(), "IPs");
            if (success is string)
            {
                IPlist = JsonConvert.DeserializeObject<List<string>>((string)success);
            }

            if (IPlist.Count == 0)
            {
                return GetMsg("No logs for this player.", steamid);
            }
            var name = (string)PlayerDatabase?.Call("GetPlayerData", findplayer.ToString(), "name");
            string replystring = string.Format("IP List for {0} - {1}", name, findplayer.ToString());
            foreach (var ip in IPlist)
            {
                replystring += string.Format("\r\n{0}", ip);
            }
            return replystring;
        }

        string CMD_chatIps(string steamid, string[] args)
        {
            if (!hasPermission(steamid, IPpermission, IPauthlevel)) { return GetMsg("You don't have permission to use this command.", steamid); }
            if (!IPuse) { return GetMsg("IPs aren't recorded.", steamid); }
            if (args.Length == 0)
            {
                return GetMsg("/ipowners XX.XX.XX.XX", steamid);
            }
            HashSet<string> knownPlayers = new HashSet<string>();
            var success = PlayerDatabase.Call("GetAllKnownPlayers");
            if (success is HashSet<string>)
                knownPlayers = (HashSet<string>)success;
            if (knownPlayers.Count == 0)
            {
                return GetMsg("Couldn't get the list of players", steamid);
            }

            var foundPlayers = new List<string>();
            foreach (string playerID in knownPlayers)
            {
                var playerIPs = new List<string>();
                var successs = PlayerDatabase.Call("GetPlayerDataRaw", playerID, "IPs");
                if (successs is string)
                {
                    playerIPs = JsonConvert.DeserializeObject<List<string>>((string)successs);
                }
                if (playerIPs.Count == 0) { continue; }
                if (playerIPs.Contains(args[0]))
                {
                    foundPlayers.Add(playerID);
                }
            }
            string replystring = string.Format("Found {0} players with this matching ip", foundPlayers.Count.ToString());

            foreach (string userid in foundPlayers)
            {
                var name = (string)PlayerDatabase.Call("GetPlayerData", userid, "name") ?? "Unknown";
                replystring += string.Format("\r\n{0} - {1}", userid, name);
            }
            return replystring;
        }

        #endregion

        #region FirstConnection

        [Command("firstconnection")]
        void cmdChatFirstconnection(IPlayer player, string command, string[] args)
        {
            string answer = CMD_chatFirstconnection(player.Id, args);
            player.Reply(answer);
        }

        float FirstConnection(string steamid)
        {
            var success = PlayerDatabase.Call("GetPlayerData", steamid, "First Connection");
            if (success is string)
            {
                return float.Parse((string)success);
            }
            return 0f;
        }

        void RecordFirstConnection(string steamid)
        {
            var firstConnection = FirstConnection(steamid);
            if (firstConnection == 0f)
            {
                var FirstConnectionTable = LogTime().ToString();
                PlayerDatabase.Call("SetPlayerData", steamid, "First Connection", FirstConnectionTable);
            }
        }

        string CMD_chatFirstconnection(string steamid, string[] args)
        {
            if (!hasPermission(steamid, FCpermission, FCauthlevel)) { return GetMsg("You don't have permission to use this command.", steamid); }
            if (!IPuse) { return GetMsg("This command has been deactivated.", steamid); }
            if (args.Length == 0)
            {
                return GetMsg("/firstconnection STEAMID/NAME", steamid);
            }
            var findplayer = FindPlayer(args[0]);
            if (!(findplayer is ulong))
            {
                return findplayer is string ? (string)findplayer : GetMsg("Couldn't find a player that matches this name.", steamid);
            }
            var firstConnection = FirstConnection(findplayer.ToString());
            if(firstConnection == 0f)
            {
                return GetMsg("No logs for this player.", steamid);
            }

            var name = (string)PlayerDatabase.Call("GetPlayerData", findplayer.ToString(), "name") ?? "Unknown";
            return string.Format("{0} - {1} first connected: {2}", name, findplayer.ToString(), TimeMinToString(firstConnection.ToString()));
        }

        #endregion

        #region Names

        [Command("lastnames")]
        void cmdChatLastname(IPlayer player, string command, string[] args)
        {
            string answer = CMD_chatLastname(player.Id, args);
            player.Reply(answer);
        }

        List<string> LastNames(string steamid)
        {
            List<string> returnList = new List<string>();
            var success = PlayerDatabase.Call("GetPlayerDataRaw", steamid, "Names");
            if (success is string)
            {
                returnList = JsonConvert.DeserializeObject<List<string>>((string)success);
            }
            return returnList;
        }

        void RecordName(string steamid, string playername)
        {
            var NameList = LastNames(steamid);

            if (NameList.Contains(playername)) return;

            if (NameList.Count >= NAMESmaxLogs)
            {
                for (int i = 0; i < (NameList.Count - NAMESmaxLogs + 1); i++)
                {
                    NameList.RemoveAt(0);
                }
            }

            NameList.Add(playername);

            PlayerDatabase.Call("SetPlayerData", steamid, "Names", NameList);
        }

        string CMD_chatLastname(string steamid, string[] args)
        {
            if (!hasPermission(steamid, NAMESpermission, NAMESauthlevel)) { return GetMsg("You don't have permission to use this command.", steamid); }
            if (!IPuse) { return GetMsg("This command has been deactivated.", steamid); }
            if (args.Length == 0)
            {
                return GetMsg("/lastnames STEAMID/NAME", steamid);
            }
            var findplayer = FindPlayer(args[0]);
            if (!(findplayer is ulong))
            {
                return findplayer is string ? (string)findplayer : GetMsg("Couldn't find a player that matches this name.", steamid);
            }
            var NameList = LastNames(findplayer.ToString());
            if (NameList.Count == 0)
            {
                return GetMsg("No logs for this player.", steamid);
            }
            var name = (string)PlayerDatabase.Call("GetPlayerData", findplayer.ToString(), "name") ?? "Unknown";
            string replyanswer = string.Format("Name List for {0} - {1}", name, findplayer.ToString());
            foreach (var n in NameList)
            {
                replyanswer += string.Format("\r\n{0}", n);
            }
            return replyanswer;
        }

        #endregion
    }
}