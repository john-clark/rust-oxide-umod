using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Oxide.Core.Plugins;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("AutomaticAuthorization", "k1lly0u", "0.2.03", ResourceId = 2063)]
    class AutomaticAuthorization : RustPlugin
    {
        #region Fields
        [PluginReference] Plugin Clans, Friends;

        ShareData shareData;
        private DynamicConfigFile data;

        private List<ulong> automatedClans = new List<ulong>();
        private List<ulong> automatedFriends = new List<ulong>();
        #endregion

        #region Oxide Hooks        
        private void Loaded()
        {
            data = Interface.Oxide.DataFileSystem.GetFile("automaticauthorization_data");
            permission.RegisterPermission("automaticauthorization.use", this);
            lang.RegisterMessages(Messages, this);
            LoadData();
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity != null && (entity is BuildingPrivlidge || entity is AutoTurret))
            {
                ulong ownerId = entity.GetComponent<BaseEntity>().OwnerID;
                  
                BasePlayer player = null;
                IPlayer iPlayer = covalence.Players.FindPlayerById(ownerId.ToString());
                if (iPlayer != null && iPlayer.IsConnected)
                    player = iPlayer.Object as BasePlayer;

                if (entity is BuildingPrivlidge)
                {
                    (entity as BuildingPrivlidge).authorizedPlayers.Add(new ProtoBuf.PlayerNameID
                    {
                        userid = ownerId,
                        username = player == null ? "" : player.displayName,
                        ShouldPool = true
                    });
                }
                else
                {
                    (entity as AutoTurret).authorizedPlayers.Add(new ProtoBuf.PlayerNameID
                    {
                        userid = ownerId,
                        username = player == null ? "" : player.displayName,
                        ShouldPool = true
                    });
                }

                if (automatedClans.Contains(ownerId))
                {
                    List<ulong> friends = GetClanMembers(ownerId);
                    SortAuthList(entity as BaseEntity, friends, player); 
                }
                if (automatedFriends.Contains(ownerId))
                {
                    List<ulong> friends = GetFriends(ownerId);
                    SortAuthList(entity as BaseEntity, friends, player);
                }
            }
        }

        private void OnServerSave() => SaveData();
        #endregion

        #region Functions
        private BaseEntity FindEntity(BasePlayer player)
        {
            var currentRot = Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward;
            Vector3 eyesAdjust = new Vector3(0f, 1.5f, 0f);

            var rayResult = Ray(player.transform.position + eyesAdjust, currentRot);
            if (rayResult is BaseEntity)
            {
                var target = rayResult as BaseEntity;
                return target;
            }
            return null;
        }
        private object Ray(Vector3 Pos, Vector3 Aim)
        {
            var hits = Physics.RaycastAll(Pos, Aim);
            float distance = 100f;
            object target = null;

            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<BaseEntity>() != null)
                {
                    if (hit.distance < distance)
                    {
                        distance = hit.distance;
                        target = hit.collider.GetComponentInParent<BaseEntity>();
                    }
                }
            }
            return target;
        }

        private List<ulong> GetClanMembers(ulong ownerId)
        {
            List<ulong> authList = new List<ulong>();
            var clanName = Clans?.Call("GetClanOf", ownerId);
            if (clanName != null)
            {
                var clan = Clans?.Call("GetClan", (string)clanName);
                if (clan != null && clan is JObject)
                {
                    var members = (clan as JObject).GetValue("members");
                    if (members != null && members is JArray)
                    {
                        foreach (var member in (JArray)members)
                        {
                            ulong ID;
                            if (!ulong.TryParse(member.ToString(), out ID))
                                continue;
                            authList.Add(ID);
                        }
                    }
                }
            }
            return authList;
        }

        private List<ulong> GetFriends(ulong ownerId)
        {
            var friends = Friends?.Call("IsFriendOf", ownerId);
            if (friends is ulong[])            
                return (friends as ulong[]).ToList();
            return new List<ulong>();
        }
        
        private void SortAuthList(BaseEntity entity, List<ulong> authList, BasePlayer player = null)
        {
            Dictionary<ulong, string> friendData = new Dictionary<ulong, string>();
            for (int i = 0; i < authList.Count; i++)
            {
                var foundPlayer = covalence.Players.FindPlayerById(authList[i].ToString());
                if (foundPlayer != null)
                    friendData.Add(authList[i], foundPlayer.Name);                
                else friendData.Add(authList[i], "");                
            }
            if (entity is BuildingPrivlidge)
                AuthToCupboard(entity as BuildingPrivlidge, friendData, player);
            else AuthToTurret(entity as AutoTurret, friendData, player);
        }

        private void AuthToCupboard(BuildingPrivlidge cupboard, Dictionary<ulong, string> authList, BasePlayer player = null)
        {
            IEnumerable<ulong> currentAuth = cupboard.authorizedPlayers.Select(x => x.userid);
            foreach (var friend in authList)
            {
                if (currentAuth.Contains(friend.Key))
                    continue;

                cupboard.authorizedPlayers.Add(new ProtoBuf.PlayerNameID
                {
                    userid = friend.Key,
                    username = friend.Value,
                    ShouldPool = true
                });
            }           
            cupboard.SendNetworkUpdateImmediate();
            if (player != null)
            {
                player.SendNetworkUpdateImmediate();
                SendReply(player, string.Format(msg("cupboardSuccess"), authList.Count));
            }
        }

        private void AuthToTurret(AutoTurret turret, Dictionary<ulong, string> authList, BasePlayer player = null)
        {
            bool isOnline = false;
            if (turret.IsOnline())
            {
                turret.SetIsOnline(false);
                isOnline = true;
            }

            IEnumerable<ulong> currentAuth = turret.authorizedPlayers.Select(x => x.userid);
            foreach (var friend in authList)
            {
                if (currentAuth.Contains(friend.Key))
                    continue;

                turret.authorizedPlayers.Add(new ProtoBuf.PlayerNameID
                {
                    userid = friend.Key,
                    username = friend.Value
                });
            }

            turret.SendNetworkUpdateImmediate();
            if (isOnline)
                turret.SetIsOnline(true);

            if (player != null)
            {
                player.SendNetworkUpdateImmediate();
                SendReply(player, string.Format(msg("turretSuccess"), authList.Count));
            }
        }
        #endregion

        #region Chat Commands
        [ChatCommand("autoauth")]
        private void cmdAuth(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin || permission.UserHasPermission(player.UserIDString, "automaticauthorization.use"))
            {
                if (args == null || args.Length == 0)
                {
                    if (Clans)
                    {
                        SendReply(player, msg("clanSyn0", player.UserIDString));
                        SendReply(player, msg("clanSyn2", player.UserIDString));
                        SendReply(player, $"{msg("autoShareClans", player.UserIDString)} {(automatedClans.Contains(player.userID) ? msg("enabled", player.UserIDString) : msg("disabled", player.UserIDString))}");
                    }
                    if (Friends)
                    {
                        SendReply(player, msg("friendSyn0", player.UserIDString));
                        SendReply(player, msg("friendSyn2", player.UserIDString));
                        SendReply(player, $"{msg("autoShareFriends", player.UserIDString)} {(automatedFriends.Contains(player.userID) ? msg("enabled", player.UserIDString) : msg("disabled", player.UserIDString))}");
                    }
                    if (!Clans && !Friends)
                    {
                        SendReply(player, msg("noSharePlugin", player.UserIDString));
                        return;
                    }
                    return;
                }
                var entity = FindEntity(player);               
                
                switch (args[0].ToLower())
                {
                    case "clan":
                        if (Clans)
                        {
                            if (entity == null || (!entity.GetComponent<AutoTurret>() && !entity.GetComponent<BuildingPrivlidge>()))
                            {
                                SendReply(player, msg("noEntity", player.UserIDString));
                                return;
                            }
                            if (entity.OwnerID != player.userID)
                            {
                                SendReply(player, msg("noOwner", player.UserIDString));
                                return;
                            }

                            List<ulong> friends = GetClanMembers(player.userID);
                            if (friends.Count == 0)
                                SendReply(player, msg("noClanMembers"));
                            else SortAuthList(entity, friends, player);
                        }
                        else SendReply(player, msg("noClanPlugin", player.UserIDString));
                        return;
                    case "friends":
                        if (Friends)
                        {
                            if (entity == null || (!entity.GetComponent<AutoTurret>() && !entity.GetComponent<BuildingPrivlidge>()))
                            {
                                SendReply(player, msg("noEntity", player.UserIDString));
                                return;
                            }
                            if (entity.OwnerID != player.userID)
                            {
                                SendReply(player, msg("noOwner", player.UserIDString));
                                return;
                            }

                            List<ulong> friends = GetFriends(player.userID);
                            if (friends.Count == 0)
                                SendReply(player, msg("noFriendsList"));
                            else SortAuthList(entity, friends, player);
                        }
                        else SendReply(player, msg("noFriendPlugin", player.UserIDString));
                        return;
                    case "autoclan":
                        if (automatedClans.Contains(player.userID))
                        {
                            automatedClans.Remove(player.userID);
                            SendReply(player, msg("autoClansDisabled", player.UserIDString));
                        }
                        else
                        {
                            automatedClans.Add(player.userID);
                            SendReply(player, msg("autoClansEnabled", player.UserIDString));
                        }
                        return;
                    case "autofriends":
                        if (automatedFriends.Contains(player.userID))
                        {
                            automatedFriends.Remove(player.userID);
                            SendReply(player, msg("autoFriendsDisabled", player.UserIDString));
                        }
                        else
                        {
                            automatedFriends.Add(player.userID);
                            SendReply(player, msg("autoFriendsEnabled", player.UserIDString));
                        }
                        return;
                    default:
                        break;
                }                                                
            }          
        }

        #endregion

        #region Data Management
        void SaveData()
        {
            shareData.shareClans = automatedClans;
            shareData.shareFriends = automatedFriends;
            data.WriteObject(shareData);
        }
        void LoadData()
        {
            try
            {
                shareData = data.ReadObject<ShareData>();
                automatedClans = shareData.shareClans;
                automatedFriends = shareData.shareFriends;
            }
            catch
            {
                shareData = new ShareData();
            }
        }  
        
        class ShareData
        {
            public List<ulong> shareFriends = new List<ulong>();
            public List<ulong> shareClans = new List<ulong>();
        }
        #endregion

        #region Messaging
        string msg(string key, string id = null) => lang.GetMessage(key, this, id);
        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            {"noEntity", "You need to look at either a Autoturret or a Tool Cupboard" },
            {"turretSuccess", "Successfully added <color=#ce422b>{0}</color> friends/clan members to the turret auth list" },
            {"cupboardSuccess", "Successfully added <color=#ce422b>{0}</color> friends/clan members to the cupboard auth list" },
            {"noOwner", "You can not authorize on something you do not own" },
            {"noFriendsList", "Unable to find your friends list" },
            {"noClan", "Unable to find your clan" },
            {"noClanMembers", "Unable to find your clan members" },
            {"clanSyn0", "<color=#ce422b>/autoauth clan</color> - Authorizes your clan mates to the object your looking at" },
            {"clanSyn2", "<color=#ce422b>/autoauth autoclan</color> - Automatically authorizes your clan mates to objects when you place them" },
            {"friendSyn0", "<color=#ce422b>/autoauth friends</color> - Authorizes your friends to the object your looking at" },
            {"friendSyn2", "<color=#ce422b>/autoauth autofriends</color> - Automatically authorizes your friends to objects when you place them" },            
            {"noClanPlugin", "Unable to find the Clans plugin" },
            {"noFriendPlugin", "Unable to find the Friends plugin" },
            {"noSharePlugin", "Clans and Friends is not installed on this server. Unable to automatically authorize other players" },
            {"autoClansDisabled", "You have disabled automatic authorization for clan members" },
            {"autoClansEnabled", "You have enabled automatic authorization for clan members" },
            {"autoFriendsDisabled", "You have disabled automatic authorization for friends" },
            {"autoFriendsEnabled", "You have enabled automatic authorization for friends" },
            {"enabled", "<color=#8ee700>Enabled</color>" },
            {"disabled", "<color=#ce422b>Disabled</color>" },
            {"autoShareClans", "Auto share for clans is: " },
            {"autoShareFriends", "Auto share for friends is: " },
        };
        #endregion
    }
}