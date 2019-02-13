using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Offline Doors", "Slydelix", "1.1.2", ResourceId = 2782)]
    class OfflineDoors : RustPlugin
    {
        [PluginReference] Plugin Clans;
        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"Clans_detected", "Clans plugin detected"},
                {"Clans_notInstalled_first", "Clans plugin is not installed, will check again in 30 seconds"},
                {"Clans_notInstalled", "Clans plugin is not installed!"},
                {"noperm", "You don't have permission to use this command."},
                {"turnedoff", "<color=silver>Turned <color=red>off</color> automatic door closing on disconnect</color>"},
                {"turnedon", "<color=silver>Turned <color=green>on</color> automatic door closing on disconnect, your doors will now close when you disconnect</color>"},
            }, this);
        }

        #endregion

        private const string perm = "offlinedoors.use";
        private bool usePerms, useClans;

        #region Config

        protected override void LoadDefaultConfig()
        {
            Config["Use permissions"] = usePerms = GetConfig("Use permissions", false);
            Config["Use clans (closes doors only when last clan memeber goes offline)"] = useClans = GetConfig("Use clans (closes doors only when last clan memeber goes offline)", false);
            SaveConfig();
        }
            
        private T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        #endregion

        #region Data

        private class StoredData
        {
            public Dictionary<ulong, bool> players = new Dictionary<ulong, bool>();

            public StoredData()
            {
            }
        }

        private StoredData storedData;

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(this.Name, storedData, true);

        #endregion

        #region Hooks

        private void OnUserConnected(IPlayer player)
        {
            ulong ID = ulong.Parse(player.Id);
            if (!storedData.players.ContainsKey(ID)) storedData.players.Add(ID, true);
        }

        private void Init()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(this.Name);
            permission.RegisterPermission(perm, this);
            LoadDefaultConfig();
        }

        private void Loaded()
        {
            if (useClans && Clans == null)
            {
                PrintWarning(lang.GetMessage("Clans_notInstalled_first", this));
                timer.In(30f, () => {
                    if (Clans == null)
                    {
                        PrintWarning(lang.GetMessage("Clans_notInstalled", this));
                        return;
                    }

                    else PrintWarning(lang.GetMessage("Clans_detected", this));
                });
            }
        }

        private void Unload() => SaveData();

        private void OnServerSave() => SaveData();

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (!useClans)
            {
                ClosePlayerDoors(player);
                return;
            }

            var clan = Clans?.Call<string>("GetClanOf", player.userID) ?? string.Empty;
            if (string.IsNullOrEmpty(clan))
            {
                //Not in clan/clan plugin is missing
                ClosePlayerDoors(player);
                return;
            }

            var clannies = GetOnlineClanMembers(clan);
            if (clannies.Contains(player.userID)) clannies.Remove(player.userID);

            if (clannies.Count == 0)
            {
                ClosePlayerDoors(player);
                return;
            }
            return;
        }

        #endregion

        #region Methods

        private List<ulong> GetOnlineClanMembers(string clanName)
        {
            List<ulong> IDlist = new List<ulong>();

            if (string.IsNullOrEmpty(clanName) || Clans == null) return IDlist;

            //Attempting to filter out as much as possible because there can be quite a lot of players
            foreach (var p in covalence.Players.All.Where(x => x.IsConnected))
            {
                var id = ulong.Parse(p.Id);
                string clan = Clans?.Call<string>("GetClanOf", id) ?? string.Empty;

                if (clan == clanName) IDlist.Add(id);
            }

            return IDlist;
        }

        private void ClosePlayerDoors(BasePlayer player)
        {
            if (usePerms && !permission.UserHasPermission(player.UserIDString, perm)) return;
            if (!storedData.players.ContainsKey(player.userID)) storedData.players.Add(player.userID, true);
            if (!storedData.players[player.userID]) return;

            var entList = BaseEntity.saveList.Where(x => (x as Door) != null && x.OwnerID == player.userID).ToList();
            if (entList.Count == 0) return;

            foreach (var item in entList)
            {
                if (item == null) continue;
                if (item.IsOpen()) (item as Door).CloseRequest();
            }
        }

        #endregion

        #region Command

        [ChatCommand("ofd")]
        private void offlinedoorscmd(BasePlayer player, string command, string[] args)
        {
            if (usePerms && !permission.UserHasPermission(player.UserIDString, perm))
            {
                SendReply(player, lang.GetMessage("noperm", this, player.UserIDString));
                return;
            }

            if (!storedData.players.ContainsKey(player.userID)) storedData.players.Add(player.userID, true);

            if (storedData.players[player.userID])
            {
                storedData.players[player.userID] = false;
                SaveData();
                SendReply(player, lang.GetMessage("turnedoff", this, player.UserIDString));
                return;
            }

            storedData.players[player.userID] = true;
            SaveData();
            SendReply(player, lang.GetMessage("turnedon", this, player.UserIDString));
            return;
        }

        #endregion
    }
}