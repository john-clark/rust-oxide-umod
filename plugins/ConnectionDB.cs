using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Random = System.Random;
// ReSharper disable UnusedMember.Local

namespace Oxide.Plugins
{
    [Info("Connection DB", "Iv Misticos", "1.0.3")]
    [Description("Connection database for developers.")]
    public class ConnectionDB : RustPlugin
    {
        #region Variables
        
        private Dictionary<ulong, PlayerData> _data = new Dictionary<ulong, PlayerData>();
        private Dictionary<string, string> _pluginData = new Dictionary<string, string>();

        private Random _rnd = new Random();
        private Time _time = GetLibrary<Time>();
        
        #endregion
        
        #region Configuration

        private Configuration _config;
        
        private class Configuration
        {
            [JsonProperty(PropertyName = "Debug")]
            public bool Debug = false;

            [JsonProperty(PropertyName = "Time Between Self-Deletion and Last Connection (sec)")]
            public uint TimeToDelete = 259200;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
            }
            catch
            {
                Config.WriteObject(_config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}.jsonError");
                PrintError("The configuration file contains an error and has been replaced with a default config.\n" +
                           "The error configuration file was saved in the .jsonError extension");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion
        
        #region Work with Data

        private void SaveData()
        {
            foreach (var kvp in _pluginData)
            {
                Interface.Oxide.DataFileSystem.WriteObject($"ConnectionDB/{kvp.Key}", kvp.Value);
            }
            
            Interface.Oxide.DataFileSystem.WriteObject(Name, _data);
        }

        private void LoadData()
        {
            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>(Name);
            }
            catch (Exception e)
            {
                PrintError($"Error: {e.Message}\n" +
                          $"Description: {e.StackTrace}");
            }

            if (_data == null) _data = new Dictionary<ulong, PlayerData>();
        }

        private bool LoadCustomData(string key)
        {
            try
            {
                var data = Interface.GetMod().DataFileSystem.ReadObject<string>($"ConnectionDB/{key}");
                if (data == null)
                    return false;

                _pluginData[key] = data;
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        private class PlayerData
        {
            public List<string> Names;
            // ReSharper disable once InconsistentNaming
            public List<string> IPs;
            public List<uint> TimeStamps;
            public uint SecondsPlayed;
        }

        #endregion
        
        #region Hooks

        private void OnServerInitialized()
        {
            LoadData();
            Puts($"Loaded users: {ConnectionsCount()}.");

            var players = BasePlayer.activePlayerList;
            var playersCount = players.Count;
            for (var i = 0; i < playersCount; i++)
                OnPlayerInit(players[i]);
            
            var users = _data.ToArray();
            var usersCount = users.Length;
            var timeStamp = _time.GetUnixTimestamp();
            var deleted = 0;

            for (var i = 0; i < usersCount; i++)
            {
                var kvp = users[i];
                if (kvp.Value.TimeStamps[kvp.Value.TimeStamps.Count - 1] + _config.TimeToDelete >
                    timeStamp) continue;

                _data.Remove(kvp.Key);
                deleted++;
            }

            PrintDebug($"Deleted old users: {deleted}.");
            SaveData();

            cmd.AddConsoleCommand("connectiondb.wipe", this, CommandConsoleWipe);
            cmd.AddConsoleCommand("connectiondb.debuginfo", this, CommandConsoleDebugInfo);
        }

        private void OnServerSave() => SaveData();
        
        private void Unload() => SaveData();

        private void OnPlayerInit(BasePlayer player) => InitPlayer(player);
        
        // ReSharper disable once UnusedParameter.Local
        private void OnPlayerDisconnected(BasePlayer player, string reason) => InitPlayer(player, true);

        #endregion
        
        #region Commands

        private bool CommandConsoleDebugInfo(ConsoleSystem.Arg arg)
        {
            if (!arg.IsRcon && !arg.IsConnectionAdmin)
                return false;

            arg.ReplyWith($"Plugin: {Name}\n" +
                          $"Version: {Version}\n" +
                          $"Debug enabled: {_config.Debug}\n" +
                          $"Server IP: {ConVar.Server.ip}\n" +
                          $"Average FPS: {Performance.current.frameRateAverage}\n" +
                          $"Data entries: {ConnectionsCount()}");
            return true;
        }

        private bool CommandConsoleWipe(ConsoleSystem.Arg arg)
        {
            if (!arg.IsRcon && !arg.IsConnectionAdmin)
                return false;

            _data.Clear();
            SaveData();
            
            arg.ReplyWith("Wipe was completed");
            return true;
        }
        
        #endregion
        
        #region Helpers

        private void InitPlayer(BasePlayer player, bool isDisconnect = false)
        {
            var id = player.userID;
            var name = player.displayName;
            var ip = player.net.connection.ipaddress;
            var time = _time.GetUnixTimestamp();
            ip = ip.Substring(0, ip.LastIndexOf(':'));

            PlayerData p;
            if (!_data.TryGetValue(id, out p))
            {
                var info = new PlayerData
                {
                    Names = new List<string> {name},
                    IPs = new List<string> {ip},
                    TimeStamps = new List<uint> {time},
                    SecondsPlayed = 1
                };

                PrintDebug($"Added new user {name} ({id})");
                _data.Add(id, info);
                SaveData();
                return;
            }

            if (!p.Names.Contains(name))
                p.Names.Add(name);
            if (!p.IPs.Contains(ip))
                p.Names.Add(ip);

            if (isDisconnect)
                p.SecondsPlayed += time - p.TimeStamps.Last();
            p.TimeStamps.Add(time);
                

            PrintDebug($"Updated user {name} ({id})");
        }

        private void PrintDebug(string s)
        {
            if (_config.Debug)
                Puts(s);
        }
        
        private bool ConnectionDataExists(ulong steamid) => _data.ContainsKey(steamid);

        private int ConnectionsCount() => _data.Count;

        private string GetStringFromData(object data) => JsonConvert.SerializeObject(data);

        private string GetPluginData(string key)
        {
            PrintDebug($"Getting data from key '{key}'..");
            if (!_pluginData.ContainsKey(key) && !LoadCustomData(key))
                return string.Empty;
            
            return _pluginData[key];
        }
        
        #endregion
        
        #region API
        
        // General API

        private List<string> API_GetNames(ulong id) => ConnectionDataExists(id) ? _data[id].Names : null;

        private string API_GetFirstName(ulong id) => ConnectionDataExists(id) ? _data[id].Names[0] : null;

        private string API_GetLastName(ulong id) => ConnectionDataExists(id) ? _data[id].Names.Last() : null;

        private List<string> API_GetIPs(ulong id) => ConnectionDataExists(id) ? _data[id].IPs : null;

        private string API_GetFirstIP(ulong id) => ConnectionDataExists(id) ? _data[id].IPs[0] : null;

        private string API_GetLastIP(ulong id) => ConnectionDataExists(id) ? _data[id].IPs.Last() : null;

        private List<uint> API_GetTimeStamps(ulong id) => ConnectionDataExists(id) ? _data[id].TimeStamps : null;

        private uint API_GetFirstTimeStamp(ulong id) => ConnectionDataExists(id) ? _data[id].TimeStamps[0] : 0;

        private uint API_GetLastTimeStamp(ulong id) => ConnectionDataExists(id) ? _data[id].TimeStamps.Last() : 0;

        private uint API_GetSecondsPlayed(ulong id) => ConnectionDataExists(id) ? _data[id].SecondsPlayed : 0;
        
        // Custom API

        private void API_SetValue(string key, object value) => _pluginData[key] = GetStringFromData(value);

        private object API_GetValue(string key) => JsonConvert.DeserializeObject(GetPluginData(key), new JsonSerializerSettings
        {
            ObjectCreationHandling = ObjectCreationHandling.Replace
        });
        
        private string API_GetValueRaw(string key) => GetPluginData(key);

        #endregion
    }
}