using System.Text.RegularExpressions;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Linq;
using System;

namespace Oxide.Plugins
{
    [Info("Timed Permissions", "LaserHydra", "1.3.2", ResourceId = 1926)]
    [Description("Allows you to grant permissions or groups for a specific time")]
    internal class TimedPermissions : CovalencePlugin
    {
        private static TimedPermissions _instance;
        private static List<Player> _players = new List<Player>();

        #region Classes

        private class Player
        {
            public readonly List<TimedAccessValue> Permissions = new List<TimedAccessValue>();
            public readonly List<TimedAccessValue> Groups = new List<TimedAccessValue>();
            public string Name = "unknown";
            public string Id = "0";

            internal static Player Get(string steamId) => _players.Find(p => p.Id == steamId);

            internal static Player GetOrCreate(IPlayer player)
            {
                Player pl = Get(player.Id);

                if (pl == null)
                {
                    pl = new Player(player);

                    _players.Add(pl);
                    SaveData(_players);
                }

                return pl;
            }

            public TimedAccessValue GetTimedPermission(string permission) => Permissions.Find(p => p.Value == permission);

            public TimedAccessValue GetTimedGroup(string group) => Groups.Find(g => g.Value == group);

            public void AddPermission(string permission, DateTime expireDate)
            {
                TimedAccessValue existingPermission = GetTimedPermission(permission);

                if (existingPermission != null)
                {
                    existingPermission.ExpireDate += expireDate - DateTime.UtcNow;

                    _instance.Puts($"----> {Name} ({Id}) - Permission Extended: {permission} to {existingPermission.ExpireDate - DateTime.UtcNow}" + Environment.NewLine);
                }
                else
                {
                    Permissions.Add(new TimedAccessValue(permission, expireDate));
                    _instance.permission.GrantUserPermission(Id, permission, null);

                    _instance.Puts($"----> {Name} ({Id}) - Permission Granted: {permission} for {expireDate - DateTime.UtcNow}" + Environment.NewLine);
                }

                SaveData(_players);
            }

            internal void AddGroup(string group, DateTime expireDate)
            {
                TimedAccessValue existingGroup = GetTimedGroup(group);

                if (existingGroup != null)
                {
                    existingGroup.ExpireDate += expireDate - DateTime.UtcNow;

                    _instance.Puts($"----> {Name} ({Id}) - Group Time Extended: {group} to {existingGroup.ExpireDate - DateTime.UtcNow}" + Environment.NewLine);
                }
                else
                {
                    Groups.Add(new TimedAccessValue(group, expireDate));
                    _instance.permission.AddUserGroup(Id, group);

                    _instance.Puts($"----> {Name} ({Id}) - Added to Group: {group} for {expireDate - DateTime.UtcNow}" + Environment.NewLine);
                }

                SaveData(_players);
            }

            internal void RemovePermission(string permission)
            {
                Permissions.Remove(GetTimedPermission(permission));
                _instance.permission.RevokeUserPermission(Id, permission);

                _instance.Puts($"----> {Name} ({Id}) - Permission Expired: {permission}" + Environment.NewLine);

                if (Groups.Count == 0 && Permissions.Count == 0)
                    _players.Remove(this);

                SaveData(_players);
            }

            internal void RemoveGroup(string group)
            {
                Groups.Remove(GetTimedGroup(group));
                _instance.permission.RemoveUserGroup(Id, group);

                _instance.Puts($"----> {Name} ({Id}) - Group Expired: {group}" + Environment.NewLine);

                if (Groups.Count == 0 && Permissions.Count == 0)
                    _players.Remove(this);

                SaveData(_players);
            }

            internal void UpdatePlayer(IPlayer player) => Name = player.Name;

            private void Update()
            {
                foreach (TimedAccessValue perm in Permissions.ToList())
                    if (perm.Expired)
                        RemovePermission(perm.Value);

                foreach (TimedAccessValue group in Groups.ToList())
                    if (group.Expired)
                        RemoveGroup(group.Value);
            }

            public override int GetHashCode() => Id.GetHashCode();

            private Player(IPlayer player)
            {
                Id = player.Id;
                Name = player.Name;

                _instance.timer.Repeat(60, 0, Update);
            }

            public Player()
            {
                _instance.timer.Repeat(60, 0, Update);
            }
        }

        private class TimedAccessValue
        {
            public string Value = string.Empty;
            public DateTime ExpireDate;

            internal bool Expired => DateTime.Compare(DateTime.UtcNow, ExpireDate) > 0;

            public override int GetHashCode() => Value.GetHashCode();

            internal TimedAccessValue(string value, DateTime expireDate)
            {
                Value = value;
                ExpireDate = expireDate;
            }

            public TimedAccessValue()
            {
            }
        }

        #endregion
        
        #region Hooks & Loading

        private void Loaded()
        {
            _instance = this;

            LoadMessages();

            MigrateData();

            LoadData(ref _players);
        }
        
        private void MigrateData()
        {
            List<JObject> data = new List<JObject>();
            LoadData(ref data);

            foreach (JObject playerData in data)
            {
                if (playerData["permissions"] != null)
                {
                    JArray permissions = (JArray) playerData["permissions"];
                    
                    foreach (JObject obj in permissions)
                    {
                        if (obj["permission"] != null)
                        {
                            obj["Value"] = obj["permission"]; 
                            obj.Remove("permission");
                        }

                        if (obj["_expireDate"] != null)
                        {
                            string expireDate = obj["_expireDate"].Value<string>();
                            
                            int[] date = (from val in expireDate.Split('/') select Convert.ToInt32(val)).ToArray(); 
                            obj["ExpireDate"] = new DateTime(date[4], date[3], date[2], date[1], date[0], 0);

                            obj.Remove("_expireDate");
                        }
                    }
                    
                    playerData["Permissions"] = permissions;
                    playerData.Remove("permissions");
                }

                if (playerData["groups"] != null)
                {
                    JArray permissions = (JArray)playerData["groups"];
                    
                    foreach (JObject obj in permissions)
                    {
                        if (obj["group"] != null)
                        {
                            obj["Value"] = obj["group"];
                            obj.Remove("group");
                        }

                        if (obj["_expireDate"] != null)
                        {
                            string expireDate = obj["_expireDate"].Value<string>();

                            int[] date = (from val in expireDate.Split('/') select Convert.ToInt32(val)).ToArray();
                            obj["ExpireDate"] = new DateTime(date[4], date[3], date[2], date[1], date[0], 0);

                            obj.Remove("_expireDate"); 
                        }
                    }

                    playerData["Groups"] = permissions;
                    playerData.Remove("groups");
                }

                if (playerData["steamID"] != null)
                {
                    playerData["Id"] = playerData["steamID"];
                    playerData.Remove("steamID");
                }

                if (playerData["name"] != null)
                {
                    playerData["Name"] = playerData["name"];
                    playerData.Remove("name");
                }
            }

            SaveData(data);
        }

        private void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
                {
                    {"No Permission", "You don't have permission to use this command."},
                    {"Invalid Time Format", "Invalid Time Format: Ex: 1d12h30m | d = days, h = hours, m = minutes"},
                    {"Player Has No Info", "There is no info about this player."},
                    {"Player Info", $"Info about <color=#C4FF00>{{player}}</color>:{Environment.NewLine}<color=#C4FF00>Groups</color>: {{groups}}{Environment.NewLine}<color=#C4FF00>Permissions</color>: {{permissions}}"},
                    {"User Doesn't Have Permission", "{target} does not have permission '{permission}'."},
                    {"User Isn't In Group", "{target} isn't in group '{group}'."},
                }, this);
        }

        #endregion

        #region Commands

        [Command("revokeperm"), Permission("timedpermissions.use")]
        private void CmdRevokePerm(IPlayer player, string cmd, string[] args)
        {
            if (args.Length != 2)
            {
                player.Reply($"Syntax: {(player.LastCommand == CommandType.Console ? string.Empty : "/")}revokeperm <player|steamid> <permission>");
                return;
            }

            IPlayer target = GetPlayer(args[0], player);

            if (target == null)
                return;

            Player pl = Player.Get(target.Id);
            
            if (pl == null || !pl.Permissions.Any(p => p.Value == args[1].ToLower()))
            {
                player.Reply(GetMessage("User Doesn't Have Permission", player.Id).Replace("{target}", target.Name).Replace("{permission}", args[1].ToLower()));
                return;
            }

            pl.RemovePermission(args[1].ToLower());
        }

        [Command("grantperm"), Permission("timedpermissions.use")]
        private void CmdGrantPerm(IPlayer player, string cmd, string[] args)
        {
            if (args.Length != 3)
            {
                player.Reply($"Syntax: {(player.LastCommand == CommandType.Console ? string.Empty : "/")}grantperm <player|steamid> <permission> <time Ex: 1d12h30m>");
                return;
            }

            IPlayer target = GetPlayer(args[0], player);
            DateTime expireDate;

            if (target == null)
                return;

            if (!TryGetDateTime(args[2], out expireDate))
            {
                player.Reply(GetMessage("Invalid Time Format", player.Id));
                return;
            }

            Player.GetOrCreate(target).AddPermission(args[1].ToLower(), expireDate);
        }

        [Command("removegroup"), Permission("timedpermissions.use")]
        private void CmdRemoveGroup(IPlayer player, string cmd, string[] args)
        {
            if (args.Length != 2)
            {
                player.Reply($"Syntax: {(player.LastCommand == CommandType.Console ? string.Empty : "/")}removegroup <player|steamid> <group>");
                return;
            }

            IPlayer target = GetPlayer(args[0], player);

            if (target == null)
                return;

            Player pl = Player.Get(target.Id);

            if (pl == null || !pl.Groups.Any(p => p.Value == args[1].ToLower()))
            {
                player.Reply(GetMessage("User Isn't In Group", player.Id).Replace("{target}", target.Name).Replace("{group}", args[1].ToLower()));
                return;
            }

            pl.RemoveGroup(args[1].ToLower());
        }

        [Command("addgroup"), Permission("timedpermissions.use")]
        private void CmdAddGroup(IPlayer player, string cmd, string[] args)
        {
            if (args.Length != 3)
            {
                player.Reply($"Syntax: {(player.LastCommand == CommandType.Console ? string.Empty : "/")}addgroup <player|steamid> <group> <time Ex: 1d12h30m>");
                return;
            }

            IPlayer target = GetPlayer(args[0], player);
            DateTime expireDate;

            if (target == null)
                return;

            if (!TryGetDateTime(args[2], out expireDate))
            {
                player.Reply(GetMessage("Invalid Time Format", player.Id));
                return;
            }

            Player.GetOrCreate(target).AddGroup(args[1], expireDate);
        }

        [Command("pinfo"), Permission("timedpermissions.use")]
        private void CmdPlayerInfo(IPlayer player, string cmd, string[] args)
        {
            if (args.Length != 1)
            {
                player.Reply($"Syntax: {(player.LastCommand == CommandType.Console ? string.Empty : "/")}pinfo <player|steamid>");
                return;
            }

            IPlayer target = GetPlayer(args[0], player);

            if (target == null)
                return;

            Player pl = Player.Get(target.Id);

            if (pl == null)
                player.Reply(GetMessage("Player Has No Info", player.Id));
            else
            {
                string msg = GetMessage("Player Info", player.Id);

                msg = msg.Replace("{player}", $"{pl.Name} ({pl.Id})");
                msg = msg.Replace("{groups}", string.Join(", ", (from g in pl.Groups select $"{g.Value} until {g.ExpireDate.ToLongDateString() + " " + g.ExpireDate.ToShortTimeString()}").ToArray()));
                msg = msg.Replace("{permissions}", string.Join(", ", (from p in pl.Permissions select $"{p.Value} until {p.ExpireDate.ToLongDateString() + " " + p.ExpireDate.ToShortTimeString()}").ToArray()));

                player.Reply(msg);
            }
        }

        #endregion

        #region Helper Methods

        #region DateTime Helper

        private bool TryGetDateTime(string source, out DateTime date)
        {
            int minutes = 0;
            int hours = 0;
            int days = 0;

            Match m = new Regex(@"(\d+?)m", RegexOptions.IgnoreCase).Match(source);
            Match h = new Regex(@"(\d+?)h", RegexOptions.IgnoreCase).Match(source);
            Match d = new Regex(@"(\d+?)d", RegexOptions.IgnoreCase).Match(source);

            if (m.Success)
                minutes = Convert.ToInt32(m.Groups[1].ToString());

            if (h.Success)
                hours = Convert.ToInt32(h.Groups[1].ToString());

            if (d.Success)
                days = Convert.ToInt32(d.Groups[1].ToString());

            source = source.Replace(minutes.ToString() + "m", string.Empty);
            source = source.Replace(hours.ToString() + "h", string.Empty);
            source = source.Replace(days.ToString() + "d", string.Empty);

            if (!string.IsNullOrEmpty(source) || (!m.Success && !h.Success && !d.Success))
            {
                date = default(DateTime);
                return false;
            }

            date = DateTime.UtcNow + new TimeSpan(days, hours, minutes, 0);
            return true;
        }

        #endregion

        #region Finding Helper

        private IPlayer GetPlayer(string nameOrID, IPlayer player)
        {
            if (IsParseableTo<ulong>(nameOrID) && nameOrID.StartsWith("7656119") && nameOrID.Length == 17)
            {
                IPlayer result = players.All.ToList().Find(p => p.Id == nameOrID);

                if (result == null)
                    player.Reply($"Could not find player with ID '{nameOrID}'");

                return result;
            }

            List<IPlayer> foundPlayers = new List<IPlayer>();

            foreach (IPlayer current in players.Connected)
            {
                if (current.Name.ToLower() == nameOrID.ToLower())
                    return current;

                if (current.Name.ToLower().Contains(nameOrID.ToLower()))
                    foundPlayers.Add(current);
            }

            switch (foundPlayers.Count)
            {
                case 0:
                    player.Reply($"Could not find player with name '{nameOrID}'");
                    break;

                case 1:
                    return foundPlayers[0];

                default:
                    string[] names = (from current in foundPlayers select current.Name).ToArray();
                    player.Reply("Multiple matching players found: \n" + string.Join(", ", names));
                    break;
            }

            return null;
        }

        #endregion

        #region Convert Helper

        private bool IsParseableTo<T>(object s)
        {
            try
            {
                var parsed = (T)Convert.ChangeType(s, typeof(T));
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TryParse<S, R>(S s, out R c)
        {
            try
            {
                c = (R)Convert.ChangeType(s, typeof(R));
                return true;
            }
            catch
            {
                c = default(R);
                return false;
            }
        }

        #endregion

        #region Data & Config Helper

        private void GetConfig<T>(ref T variable, params string[] path)
        {
            if (path.Length == 0)
                return;

            if (Config.Get(path) == null)
            {
                Config.Set(path.Concat(new object[] { variable }).ToArray());
                PrintWarning($"Added field to config: {string.Join("/", path)}");
            }

            variable = (T)Convert.ChangeType(Config.Get(path), typeof(T));
        }

        private string DataFileName => Title.Replace(" ", "");

        private static void LoadData<T>(ref T data, string filename = null) => data = Core.Interface.Oxide.DataFileSystem.ReadObject<T>(filename ?? _instance.DataFileName);

        private static void SaveData<T>(T data, string filename = null) => Core.Interface.Oxide.DataFileSystem.WriteObject(filename ?? _instance.DataFileName, data);

        #endregion

        #region Message Wrapper

        public static string GetMessage(string key, string id) => _instance.lang.GetMessage(key, _instance, id);

        #endregion

        #endregion
    }
}