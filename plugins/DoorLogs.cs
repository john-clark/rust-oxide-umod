using Oxide.Core;
using System.Collections.Generic;
using UnityEngine;
using System;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Door Logs", "mvrb", "0.2.0")]
    [Description("Check who opens/closes doors.")]
    class DoorLogs : RustPlugin
    {
        private const string PermissionUse = "doorlogs.allowed";

        private StoredData storedData = new StoredData();

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoDoorFound"] = "No door found.",
                ["NoDataFound"] = "No data found for this door.",
                ["DoorData"] = "Data for this door [{0}]: \n",
                ["Error: NoPermission"] = "You do not have permission to use this command."
            }, this);
        }

        private void OnServerInitialized()
        {
            LoadData();
            LoadVariables();

            permission.RegisterPermission(PermissionUse, this);
        }

        [ChatCommand("door")]
        private void ChatCmdCheckDoor(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionUse))
            {
                player.ChatMessage(Lang("Error: NoPermission", player.UserIDString));
                return;
            }

            RaycastHit hit;
            var raycast = Physics.Raycast(player.eyes.HeadRay(), out hit, 15f, 2097409);
            BaseEntity door = raycast ? hit.GetEntity() : null;

            if (door == null || door as Door == null)
            {
                player.ChatMessage(Lang("NoDoorFound", player.UserIDString));
                return;
            }

            if (!storedData.Doors.ContainsKey(door.net.ID) || storedData.Doors[door.net.ID].Entries.Count == 0)
            {
                player.ChatMessage(Lang("NoDataFound", player.UserIDString));
                return;
            }

            int entries = 1;
            string msg1 = Lang("DoorData", player.UserIDString, door.net.ID);
            string msg2 = string.Empty;

            foreach (KeyValuePair<ulong, PlayerData> entry in storedData.Doors[door.net.ID].Entries.Reverse())
            {
                string last = string.Empty;
                if (entry.Value.L > 0) last = $"L: <color=#FFA500>{UnixToDateTime(entry.Value.L)}</color>";

                if (entries < 6)
                {
                    msg1 += $"[{entries}] <color=#FFFF00>{GetNameFromId(entry.Key.ToString())}</color> ({entry.Key})\n ";
                    msg1 += $"F: <color=#FFA500>{UnixToDateTime(entry.Value.F)}</color> {last}\n";
                }
                else
                {
                    msg2 += $"[{entries}] <color=#FFFF00>{GetNameFromId(entry.Key.ToString())}</color> ({entry.Key})\n ";
                    msg2 += $"F: <color=#FFA500>{UnixToDateTime(entry.Value.F)}</color> {last}\n";
                }

                entries++;
            }

            player.ChatMessage(msg1);
            if (!string.IsNullOrEmpty(msg2)) player.ChatMessage(msg2);

            if (!configData.LogToConsole) return;

            msg1 = Regex.Replace(msg1, @"<color=#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})>|<\/color>", "");
            Puts(msg1);

            if (string.IsNullOrEmpty(msg2)) return;

            msg2 = Regex.Replace(msg2, @"<color=#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})>|<\/color>", "");
            Puts(msg2);
        }

        private void OnDoorClosed(Door door, BasePlayer player) => UpdateDoor(door, player);

        private void OnDoorOpened(Door door, BasePlayer player) => UpdateDoor(door, player);

        private void UpdateDoor(Door door, BasePlayer player)
        {
            if (door.OwnerID == 0) return;

            int timestamp = GetUnix();

            PlayerData playerData = new PlayerData();
            playerData.F = timestamp;

            if (!storedData.Doors.ContainsKey(door.net.ID))
            {
                DoorData doorData = new DoorData();
                doorData.Entries.Add(player.userID, playerData);

                storedData.Doors.Add(door.net.ID, doorData);
            }
            else
            {
                if (!storedData.Doors[door.net.ID].Entries.ContainsKey(player.userID))
                {
                    storedData.Doors[door.net.ID].Entries.Add(player.userID, playerData);
                }
                else
                {
                    storedData.Doors[door.net.ID].Entries[player.userID].L = timestamp;
                }

                if (storedData.Doors[door.net.ID].Entries.Count >= configData.MaxEntries)
                {
                    storedData.Doors[door.net.ID].Entries.Remove(storedData.Doors[door.net.ID].Entries.Keys.First());
                }
            }
        }

        private class StoredData
        {
            public readonly Dictionary<uint, DoorData> Doors = new Dictionary<uint, DoorData>();
        }

        private class DoorData
        {
            public readonly Dictionary<ulong, PlayerData> Entries = new Dictionary<ulong, PlayerData>();
        }

        private class PlayerData
        {
            public Int32 F, L;
        }

        #region Config
        private ConfigData configData;

        class ConfigData
        {
            [JsonProperty(PropertyName = "Log /door output to console")]
            public bool LogToConsole { get; set; }

            [JsonProperty(PropertyName = "Max entries to log per door")]
            public int MaxEntries { get; set; }
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
                LogToConsole = false,
                MaxEntries = 10
            };

            SaveConfig(config);
        }

        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();

        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        private string UnixToDateTime(double unixTimeStamp) => new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixTimeStamp).ToLocalTime().ToString("MM/dd HH:mm:ss");

        private Int32 GetUnix() => (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

        private string GetNameFromId(string id) => covalence.Players.FindPlayer(id)?.Name;

        private void LoadData() => storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>(this.Name);

        private void SaveData() => Interface.GetMod().DataFileSystem.WriteObject(this.Name, storedData);

        private void OnNewSave(string name)
        {
            PrintWarning("Map wipe detected - clearing DoorLogs...");

            storedData.Doors.Clear();
            SaveData();
        }

        private void OnServerSave() => SaveData();

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
    }
}