using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using UnityEngine;
using System.Linq;
using System.Diagnostics;
using System.Collections;
//Reference: Facepunch.Sqlite

namespace Oxide.Plugins
{
    [Info("BanFix", "Jake_Rich", "1.0.0")]
    [Description("")]

    public class BanFix : RustPlugin
    {
        private Facepunch.Sqlite.Database banDB;

        public const string TableName = "server_users";

        void SetupDatabase()
        {
            banDB = new Facepunch.Sqlite.Database();
            banDB.Open($"{ConVar.Server.rootFolder}/ServerUsers.db");
            if (!banDB.TableExists(TableName))
            {
                //group is a reserved word: using usergroup instead
                banDB.Execute($"CREATE TABLE {TableName} ( steamid INTEGER PRIMARY KEY, usergroup INTEGER, username TEXT, notes TEXT )");
                OnServerSaveUsers();
            }
        }

        private IEnumerator SaveBansToDatabase()
        {
            var bans = ServerUsers.GetAll(ServerUsers.UserGroup.Banned).ToList();
            yield return null; //Gotta have that debug message show when you first load the plguin
            Puts($"Starting To Push {bans.Count} Bans To Database");
            foreach (var user in bans)
            {
                UpdateUserGroup(user.steamid, user.group, user.username, user.notes);
                yield return null;
            }
            Puts("Finished Saving All Bans To The Database");
            yield return null;
        }


        #region Hooks

        void OnServerInitialized()
        {
            SetupDatabase();
            OnServerLoadUsers();
        }

        void Unload()
        {
            banDB?.Close();
        }

        void OnServerShutdown()
        {
            //May as well write to default file on shutdown
            ServerUsers.Save();
        }

        object OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg.cmd.FullName == "global.ban")
            {
                if (arg.IsAdmin)
                {
                    //Need to overwrite ban command as vanilla one will save to file
                    OverwriteBanCommand(arg);
                }
                return false;
            }
            return null;
        }

        //Not really a hook but sorta as we are copy paste overwriting it
        void OverwriteBanCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = ArgEx.GetPlayer(arg, 0);
            if (!(bool)player || player.net == null || player.net.connection == null)
            {
                arg.ReplyWith("Player not found");
            }
            else
            {
                ServerUsers.User user = ServerUsers.Get(player.userID);
                if (user != null && user.group == ServerUsers.UserGroup.Banned)
                {
                    arg.ReplyWith("User " + player.userID + " is already banned");
                }
                else
                {
                    string @string = arg.GetString(1, "No Reason Given");
                    ServerUsers.Set(player.userID, ServerUsers.UserGroup.Banned, player.displayName, @string);
                    string text = string.Empty;
                    if (player.IsConnected && player.net.connection.ownerid != player.net.connection.userid)
                    {
                        text = text + " and also banned ownerid " + player.net.connection.ownerid;
                        ServerUsers.Set(player.net.connection.ownerid, ServerUsers.UserGroup.Banned, player.displayName, arg.GetString(1, "Family share owner of " + player.net.connection.userid));
                    }
                    //We don't actually have to save, because we save on user group changed
                    //OnServerSaveUsers();
                    //ServerUsers.Save();
                    arg.ReplyWith("Kickbanned User: " + player.userID + " - " + player.displayName + text);
                    PrintToChat("Kickbanning " + player.displayName + " (" + @string + ")", "SERVER", "#eee", 0uL);
                    Network.Net.sv.Kick(player.net.connection, "Banned: " + @string);
                }
            }
        }

        Coroutine saveRoutine;

        //Hook Save (Save To DB instead of File) 
        //Not used much: we're saving individual user changes instead of them all
        void OnServerSaveUsers()
        {
            //OK RYAN DO YOUR SAVE SHIT HERE

            //We needed to spread out the queries: took 16,000MS to load 4k bans
            if (saveRoutine != null)
            {
                ServerMgr.Instance.StopCoroutine(saveRoutine); //Don't let users be stupid and save multiple times and lag their server
            }
            saveRoutine = ServerMgr.Instance.StartCoroutine(SaveBansToDatabase());
        }

        //Hook Load (Load from DB instead of File)
        void OnServerLoadUsers()
        {
            //NOW DO YOUR LOAD STUFF HERE
            LoadingServerUsers = true;
            foreach (var row in banDB.Query($"SELECT * FROM {TableName}"))
            {
                //https://stackoverflow.com/questions/11226448/invalidcastexception-long-to-ulong
                //Never knew you had to cast to long then ulong in a specific situation
                ulong steamID = (ulong)((long)row.Value["steamid"]);
                ServerUsers.UserGroup group = (ServerUsers.UserGroup)row.Value.GetInt("usergroup");
                string name = row.Value.GetString("username") ?? "";
                string reason = row.Value.GetString("notes") ?? "";
                ServerUsers.Set(steamID, group, name, reason);
            }
            LoadingServerUsers = false;
        }

        void OnPlayerBanned(string name, ulong steamID, string IP, string reason)
        {
            if (LoadingServerUsers)
            {
                return;
            }
            UpdateUserGroup(steamID, ServerUsers.UserGroup.Banned, name, reason);
        }

        void OnPlayerUnbanned(string name, ulong steamID, string IP)
        {
            if (LoadingServerUsers)
            {
                return;
            }
            RemoveUserGroup(steamID);
        }

        #endregion

        private bool LoadingServerUsers = false;

        void ClearDatabase()
        {
            banDB.Execute($"DELETE FROM {TableName}");
            banDB.Execute("VACUUM");
        }

        void UpdateUserGroup(ulong steamId, ServerUsers.UserGroup group, string name, string notes)
        {
            banDB.Execute($"INSERT OR REPLACE INTO {TableName} ( steamid, usergroup, username, notes ) VALUES ( ?, ?, ?, ? )", (long)steamId, (int)group, name ?? "", notes ?? "");
        }

        void RemoveUserGroup(ulong steamId)
        {
            banDB.Execute($"DELETE FROM {TableName} WHERE steamid = ?", steamId);
        }

        #region Commands

        [ConsoleCommand("bans.save.db")]
        void SaveBansToDB_CMD(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                return;
            }
            OnServerSaveUsers();
        }

        [ConsoleCommand("bans.save.file")]
        void SaveBansToFile_CMD(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                return;
            }
            ServerUsers.Save();
        }

        [ConsoleCommand("bans.clear.db")]
        void ClearBansTest(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                return;
            }
            ClearDatabase();
            Puts($"Cleared Database");
        }

        [ConsoleCommand("bans.clear.file")]
        void ClearLocalBans(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                return;
            }
            LoadingServerUsers = true;
            foreach (var ban in ServerUsers.GetAll(ServerUsers.UserGroup.Banned).ToList())
            {
                ServerUsers.Remove(ban.steamid);
            }
            LoadingServerUsers = false;
            Puts($"Cleared Server Bans");
        }

        [ConsoleCommand("bans.load.db")]
        void LoadBansDB_CMD(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                return;
            }
            OnServerLoadUsers();
            Puts("Loaded Bans From DB");
        }

        [ConsoleCommand("bans.load.file")]
        void LoadBansFile_CMD(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                return;
            }
            LoadingServerUsers = true;
            ServerUsers.Load();
            LoadingServerUsers = false;
            Puts("Loaded Bans From File");
        }

        [ConsoleCommand("bans.check")]
        void ReadBansTest(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                return;
            }
            int result = banDB.Query($"SELECT * FROM {TableName}").Count;
            Puts($"Database: {result} Server: {ServerUsers.GetAll(ServerUsers.UserGroup.Banned).Count()}");
        }

        [ConsoleCommand("bans.test")]
        void TestOldBan(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                return;
            }
            Stopwatch watch = new Stopwatch();
            watch.Start();
            ServerUsers.Save();
            watch.Stop();
            Puts($"Took {watch.ElapsedMilliseconds} ms for old ban method");
            watch.Reset();
            watch.Start();
            UpdateUserGroup(76561198104673895u, ServerUsers.UserGroup.Moderator, "Jake", "PLEASE FUCKING WORK");
            watch.Stop();
            Puts($"Took {watch.ElapsedMilliseconds} ms for new ban method");
            watch.Reset();
            watch.Start();
            RemoveUserGroup(76561198104673895u);
            watch.Stop();
            Puts($"Took {watch.ElapsedMilliseconds} ms for new unban method");
            watch.Reset();
        }

        #endregion
    }
}