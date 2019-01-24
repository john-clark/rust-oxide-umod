﻿using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using ProtoBuf;

namespace Oxide.Plugins
{
    [Info("Friends", "Nogrod", "2.2.5", ResourceId = 686)]
    [Description("An API to manage a friend list")]
    public class Friends : CovalencePlugin
    {
        #region Configuration and Stored Data

        private ConfigData configData;
        private Dictionary<ulong, PlayerData> FriendsData;
        private readonly Dictionary<ulong, HashSet<ulong>> ReverseData = new Dictionary<ulong, HashSet<ulong>>();
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1);

        private class ConfigData
        {
            public int MaxFriends { get; set; }
            public bool ShareCodeLocks { get; set; }
            public bool ShareAutoTurrets { get; set; }
            public int CacheTime { get; set; }
        }

        private class PlayerData
        {
            public string Name { get; set; } = string.Empty;
            public HashSet<ulong> Friends { get; set; } = new HashSet<ulong>();
            public Dictionary<ulong, int> Cached { get; set; } = new Dictionary<ulong, int>();

            public bool IsCached(ulong userId)
            {
                int time;
                if (!Cached.TryGetValue(userId, out time)) return false;
                if (time >= (int)DateTime.UtcNow.Subtract(Epoch).TotalSeconds) return true;
                Cached.Remove(userId);
                return false;
            }
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                MaxFriends = 30,
                ShareCodeLocks = false,
                ShareAutoTurrets = false,
                CacheTime = 0 //60 * 60 * 24
            };
            Config.WriteObject(config, true);
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"AlreadyOnList", "{0} is already your friend."},
                {"CantAddSelf", "You cant add yourself."},
                {"FriendAdded", "{0} is now your friend."},
                {"FriendRemoved", "{0} was removed from your friendlist."},
                {"FriendlistFull", "Your friendlist is full."},
                {"HelpText", "Use /friend <add|+|remove|-|list> <name/steamID> to add/remove/list friends"},
                {"List", "Friends {0}:\n{1}"},
                {"MultiplePlayers", "Multiple players were found, please specify: {0}"},
                {"NoFriends", "You don't have friends."},
                {"NotOnFriendlist", "{0} not found on your friendlist."},
                {"PlayerNotFound", "Player '{0}' not found."},
                {"Syntax", "Syntax: /friend <add/+/remove/-> <name/steamID> or /friend list"}
            }, this);
        }

        #endregion

        #region Initialization

        private void Init()
        {
            configData = Config.ReadObject<ConfigData>();
            try
            {
                FriendsData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>(nameof(Friends));
            }
            catch
            {
                FriendsData = new Dictionary<ulong, PlayerData>();
            }

            foreach (var data in FriendsData)
                foreach (var friend in data.Value.Friends)
                    AddFriendReverse(data.Key, friend);
        }

        #endregion

#if RUST
        private object OnTurretTarget(AutoTurret turret, BaseCombatEntity targ)
        {
            if (!configData.ShareAutoTurrets || !(targ is BasePlayer) || turret.OwnerID <= 0) return null;
            var player = (BasePlayer) targ;
            if (turret.IsAuthed(player) || !HasFriend(turret.OwnerID, player.userID)) return null;
            turret.authorizedPlayers.Add(new PlayerNameID
            {
                userid = player.userID,
                username = player.displayName
            });
            return false;
        }

        private object CanUseLockedEntity(BasePlayer player, BaseLock @lock)
        {
            if (!configData.ShareCodeLocks || !(@lock is CodeLock) || @lock.GetParentEntity().OwnerID <= 0) return null;
            if (HasFriend(@lock.GetParentEntity().OwnerID, player.userID))
            {
                var codeLock = @lock as CodeLock;
                var whitelistPlayers = (List<ulong>)codeLock.whitelistPlayers;
                if (!whitelistPlayers.Contains(player.userID)) whitelistPlayers.Add(player.userID);
            }
            return null;
        }
#endif

        private void SaveFriends()
        {
            Interface.Oxide.DataFileSystem.WriteObject("Friends", FriendsData);
        }

        #region Add/Remove Friends

        private bool AddFriend(ulong playerId, ulong friendId)
        {
            var playerData = GetPlayerData(playerId);
            if (playerData.Friends.Count >= configData.MaxFriends || !playerData.Friends.Add(friendId)) return false;
            AddFriendReverse(playerId, friendId);
            SaveFriends();
            Interface.Oxide.CallHook("OnFriendAdded", playerId.ToString(), friendId.ToString());
            return true;
        }

        private bool AddFriendS(string playerS, string friendS)
        {
            if (string.IsNullOrEmpty(playerS) || string.IsNullOrEmpty(friendS)) return false;
            var playerId = Convert.ToUInt64(playerS);
            var friendId = Convert.ToUInt64(friendS);
            return AddFriend(playerId, friendId);
        }

        private bool RemoveFriend(ulong playerId, ulong friendId)
        {
            var playerData = GetPlayerData(playerId);
            if (!playerData.Friends.Remove(friendId)) return false;
            HashSet<ulong> friends;
            if (ReverseData.TryGetValue(friendId, out friends))
                friends.Remove(playerId);
            if (configData.CacheTime > 0)
                playerData.Cached[friendId] = (int)DateTime.UtcNow.Subtract(Epoch).TotalSeconds + configData.CacheTime;
#if RUST
            if (configData.ShareAutoTurrets)
            {
                var turrets = UnityEngine.Object.FindObjectsOfType<AutoTurret>();
                foreach (var turret in turrets)
                {
                    if (turret.OwnerID != playerId) continue;
                    turret.authorizedPlayers.RemoveAll(a => a.userid == friendId);
                }
            }
            if (configData.ShareCodeLocks)
            {
                var codeLocks = UnityEngine.Object.FindObjectsOfType<CodeLock>();
                foreach (var codeLock in codeLocks)
                {
                    var entity = codeLock.GetParentEntity();
                    if (entity == null || entity.OwnerID != playerId) continue;
                    var whitelistPlayers = (List<ulong>)codeLock.whitelistPlayers;
                    whitelistPlayers.RemoveAll(a => a == friendId);
                }
            }
#endif
            SaveFriends();
            Interface.Oxide.CallHook("OnFriendRemoved", playerId.ToString(), friendId.ToString());
            return true;
        }

        private bool RemoveFriendS(string playerS, string friendS)
        {
            if (string.IsNullOrEmpty(playerS) || string.IsNullOrEmpty(friendS)) return false;
            var playerId = Convert.ToUInt64(playerS);
            var friendId = Convert.ToUInt64(friendS);
            return RemoveFriend(playerId, friendId);
        }

        #endregion

        #region Friend Checks

        private bool HasFriend(ulong playerId, ulong friendId) => GetPlayerData(playerId).Friends.Contains(friendId);

        private bool HasFriendS(string playerS, string friendS)
        {
            if (string.IsNullOrEmpty(playerS) || string.IsNullOrEmpty(friendS)) return false;
            var playerId = Convert.ToUInt64(playerS);
            var friendId = Convert.ToUInt64(friendS);
            return HasFriend(playerId, friendId);
        }

        private bool HadFriend(ulong playerId, ulong friendId)
        {
            var playerData = GetPlayerData(playerId);
            return playerData.Friends.Contains(friendId) || playerData.IsCached(friendId);
        }

        private bool HadFriendS(string playerS, string friendS)
        {
            if (string.IsNullOrEmpty(playerS) || string.IsNullOrEmpty(friendS)) return false;
            var playerId = Convert.ToUInt64(playerS);
            var friendId = Convert.ToUInt64(friendS);
            return HadFriend(playerId, friendId);
        }

        private bool AreFriends(ulong playerId, ulong friendId)
        {
            return GetPlayerData(playerId).Friends.Contains(friendId) && GetPlayerData(friendId).Friends.Contains(playerId);
        }

        private bool AreFriendsS(string playerS, string friendS)
        {
            if (string.IsNullOrEmpty(playerS) || string.IsNullOrEmpty(friendS)) return false;
            var playerId = Convert.ToUInt64(playerS);
            var friendId = Convert.ToUInt64(friendS);
            return AreFriends(playerId, friendId);
        }

        private bool WereFriends(ulong playerId, ulong friendId)
        {
            var playerData = GetPlayerData(playerId);
            var friendData = GetPlayerData(friendId);
            return (playerData.Friends.Contains(friendId) || playerData.IsCached(friendId)) && (friendData.Friends.Contains(playerId) || friendData.IsCached(playerId));
        }

        private bool WereFriendsS(string playerS, string friendS)
        {
            if (string.IsNullOrEmpty(playerS) || string.IsNullOrEmpty(friendS)) return false;
            var playerId = Convert.ToUInt64(playerS);
            var friendId = Convert.ToUInt64(friendS);
            return WereFriends(playerId, friendId);
        }

        private bool IsFriend(ulong playerId, ulong friendId)
        {
            return GetPlayerData(friendId).Friends.Contains(playerId);
        }

        private bool IsFriendS(string playerS, string friendS)
        {
            if (string.IsNullOrEmpty(playerS) || string.IsNullOrEmpty(friendS)) return false;
            var playerId = Convert.ToUInt64(playerS);
            var friendId = Convert.ToUInt64(friendS);
            return IsFriend(playerId, friendId);
        }

        private bool WasFriend(ulong playerId, ulong friendId)
        {
            var playerData = GetPlayerData(friendId);
            return playerData.Friends.Contains(playerId) || playerData.IsCached(playerId);
        }

        private bool WasFriendS(string playerS, string friendS)
        {
            if (string.IsNullOrEmpty(playerS) || string.IsNullOrEmpty(friendS)) return false;
            var playerId = Convert.ToUInt64(playerS);
            var friendId = Convert.ToUInt64(friendS);
            return WasFriend(playerId, friendId);
        }

        #endregion

        #region Friend Lists

        private ulong[] GetFriends(ulong playerId) => GetPlayerData(playerId).Friends.ToArray();

        private string[] GetFriendsS(string playerS)
        {
            var playerId = Convert.ToUInt64(playerS);
            return GetPlayerData(playerId).Friends.ToList().ConvertAll(f => f.ToString()).ToArray();
        }

        private string[] GetFriendList(ulong playerId)
        {
            var playerData = GetPlayerData(playerId);
            var players = new List<string>();
            foreach (var friend in playerData.Friends)
                players.Add(GetPlayerData(friend).Name);
            return players.ToArray();
        }

        private string[] GetFriendListS(string playerS) => GetFriendList(Convert.ToUInt64(playerS));

        private ulong[] IsFriendOf(ulong playerId)
        {
            HashSet<ulong> friends;
            return ReverseData.TryGetValue(playerId, out friends) ? friends.ToArray() : new ulong[0];
        }

        private string[] IsFriendOfS(string playerS)
        {
            var playerId = Convert.ToUInt64(playerS);
            var friends = IsFriendOf(playerId);
            return friends.ToList().ConvertAll(f => f.ToString()).ToArray();
        }

        #endregion

        private PlayerData GetPlayerData(ulong playerId)
        {
            var player = players.FindPlayerById(playerId.ToString());
            PlayerData playerData;
            if (!FriendsData.TryGetValue(playerId, out playerData))
                FriendsData[playerId] = playerData = new PlayerData();
            if (player != null) playerData.Name = player.Name;
            return playerData;
        }

        #region Commands

        [Command("friend")]
        private void FriendCommand(IPlayer player, string command, string[] args)
        {
            if (player.Id == "server_console")
            {
                player.Reply($"Command '{command}' can only be used by players", command);
                return;
            }

            if (args == null || args.Length <= 0 || args.Length == 1 && !args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                Reply(player, "Syntax");
                return;
            }

            switch (args[0].ToLower())
            {
                case "list":
                    var friendList = GetFriendListS(player.Id);
                    if (friendList.Length > 0)
                        Reply(player, "List", $"{friendList.Length}/{configData.MaxFriends}", string.Join(", ", friendList));
                    else
                        Reply(player, "NoFriends");
                    return;

                case "add":
                case "+":
                    var foundPlayers = players.FindPlayers(args[1]).Where(p => p.IsConnected).ToArray();
                    if (foundPlayers.Length > 1)
                    {
                        Reply(player, "MultiplePlayers", string.Join(", ", foundPlayers.Select(p => p.Name).Take(10).ToArray()).Truncate(60));
                        return;
                    }

                    var friendPlayer = foundPlayers.Length == 1 ? foundPlayers[0] : null;
                    if (friendPlayer == null)
                    {
                        Reply(player, "PlayerNotFound", args[1]);
                        return;
                    }

                    if (player == friendPlayer)
                    {
                        Reply(player, "CantAddSelf");
                        return;
                    }

                    var playerData = GetPlayerData(Convert.ToUInt64(player.Id));
                    if (playerData.Friends.Count >= configData.MaxFriends)
                    {
                        Reply(player, "FriendlistFull");
                        return;
                    }

                    if (playerData.Friends.Contains(Convert.ToUInt64(friendPlayer.Id)))
                    {
                        Reply(player, "AlreadyOnList", friendPlayer.Name);
                        return;
                    }

                    AddFriendS(player.Id, friendPlayer.Id);
                    Reply(player, "FriendAdded", friendPlayer.Name);
                    return;

                case "remove":
                case "-":
                    var friend = FindFriend(args[1]);
                    if (friend <= 0)
                    {
                        Reply(player, "NotOnFriendlist", args[1]);
                        return;
                    }

                    var removed = RemoveFriendS(player.Id, friend.ToString());
                    Reply(player, removed ? "FriendRemoved" : "NotOnFriendlist", args[1]);
                    return;
            }
        }

        #endregion

        private void Reply(IPlayer player, string langKey, params object[] args)
        {
            player.Reply(string.Format(lang.GetMessage(langKey, this, player.Id), args));
        }

        private void SendHelpText(object obj)
        {
            var player = players.FindPlayerByObj(obj);
            if (player != null) Reply(player, "HelpText");
        }

        private void AddFriendReverse(ulong playerId, ulong friendId)
        {
            HashSet<ulong> friends;
            if (!ReverseData.TryGetValue(friendId, out friends))
                ReverseData[friendId] = friends = new HashSet<ulong>();
            friends.Add(playerId);
        }

        private ulong FindFriend(string friend)
        {
            if (string.IsNullOrEmpty(friend)) return 0;
            foreach (var playerData in FriendsData)
            {
                if (playerData.Key.ToString().Equals(friend) || playerData.Value.Name.IndexOf(friend, StringComparison.OrdinalIgnoreCase) >= 0)
                    return playerData.Key;
            }
            return 0;
        }
    }
}