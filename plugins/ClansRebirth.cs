using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{

    [Info("Clans Rebirth", "Serenity 3", "1.0", ResourceId = 0)]
    [Description("Serenity's Clans Rebirth")]
    internal class ClansRebirth : RustPlugin
    {
        #region Fields

        public static StoreData data = new StoreData();

        public HashSet<Timer> ActiveTimers = new HashSet<Timer>();
        public Dictionary<BasePlayer,StorageContainer> PlayerContainers = new Dictionary<BasePlayer, StorageContainer>();

        [PluginReference]
        public Plugin BetterChat, Friends, FriendlyFire;
        #endregion Fields

        #region Saving Data

        public enum Rank
        {
            Normal = 0,
            Moderator = 1,
            Council = 2,
            Owner = 3
        };

        public struct DataFile
        {
            public const string ClanInviteData = "ClanInviteData";
            public const string ClanData = "ClanData";
            public const string PlayerData = "PlayerData";
        }

        public class StoreData
        {
            public HashSet<Clan> ClanData = new HashSet<Clan>();
            public Dictionary<ulong, Clan> ClanInviteData = new Dictionary<ulong, Clan>();
            public HashSet<PlayerData> PlayerData = new HashSet<PlayerData>();

            public StoreData()
            {
                ReadData(ref ClanData, DataFile.ClanData);
                ReadData(ref ClanInviteData, DataFile.ClanInviteData);
                ReadData(ref PlayerData, DataFile.PlayerData);
            }

            public void ReadData<T>(ref T data, string filename) => data = data = Core.ProtoStorage.Load<T>($"ClansRebirthed/{filename}");

            public void SaveData<T>(T data, string filename) => Core.ProtoStorage.Save<T>(data, $"ClansRebirthed/{filename}");
        }

        public class Clan
        {
            public string ClanName;
            public string Description;
            public string ClanTag;
            public bool FriendlyFireActive;
            public bool HasHome;
            public int Id;
            public Vector3 ClanHome;
            public Dictionary<ulong, Rank> MemberList;
            public List<int> AllianceList;
            public List<int> EnemyList;

            public Clan(string name, string desc, ulong playerWhoMade)
            {
                var currentId = 0;
                var playerFromId = BasePlayer.FindByID(playerWhoMade);
                var allianceList = new List<int>();
                var enemyList = new List<int>();
                var memberList = new Dictionary<ulong, Rank>
                {
                    { playerWhoMade, Rank.Owner }
                };

                foreach (var e in data.ClanData)
                {
                    currentId = e.Id;
                }

                ClanName = name;
                Id = currentId + 1;
                ClanTag = $"[{name}]";
                FriendlyFireActive = true;
                HasHome = false;
                MemberList = memberList;
                AllianceList = allianceList;
                EnemyList = enemyList;
                Description = desc;
                ClanHome = new Vector3();
            }
        }

        public class PlayerData
        {
            public ulong PlayerID = 0;
            public string Name = "";
            public string Clantag = "";
            public Clan BelongsToClan = null;

            public PlayerData(BasePlayer player, Clan clan)
            {
                PlayerID = player.userID;
                Name = player.displayName;
                Clantag = clan.ClanTag;
                BelongsToClan = clan;
            }

        }

        public class InventoryData
        {
            public StorageContainer storageContainer;
            public Clan clan;

            public InventoryData(Clan clanData, StorageContainer storage)
            {
                storageContainer = storage;
                clan = clanData;
            }
        }

        #endregion Saving Data

        #region Helpers

        public struct Permission
        {
            public const string Admin = "clansrebirth.admin";
            public const string Create = "clansrebirth.clan.create";
            public const string Invite = "clansrebirth.clan.invite";
            public const string Leave = "clansrebirth.clan.leave";
            public const string SetHome = "clansrebirth.clan.sethome";
            public const string Promote = "clansrebirth.clan.promote";
            public const string Home = "clansrebirth.clan.home";
            public const string Join = "clansrebirth.clan.join";
            public const string AllyChat = "clansrebirth.clan.allychat";
            public const string ClanChat = "clansrebirth.clan.clanchat";
            public const string Kick = "clansrebirth.clan.kick";
            public const string Disband = "clansrebirth.clan.disband";
            public const string Vault = "clansrebirth.clan.vault";
        }

        public void RegisterAllPerms()
        {
            List<string> Perm = new List<string>
            {
                Permission.Admin,
                Permission.Create,
                Permission.Invite,
                Permission.Leave,
                Permission.SetHome,
                Permission.Promote,
                Permission.Home,
                Permission.Join,
                Permission.AllyChat,
                Permission.ClanChat,
                Permission.Kick,
                Permission.Disband,
                Permission.Vault
            };

            foreach (var e in Perm)
            {
                permission.RegisterPermission(e,this);
            }
        }

        public bool CheckIfPlayerHasPerm(string playerId, string permName)
        {
            var player = BasePlayer.FindByID(ulong.Parse(playerId));
            if (permission.UserHasPermission(playerId, permName))
            {
                return true;
            }

            return false;
        }

        public struct LangMessages
        {
            public const string SendInvite = "SendInvitesToFriends";
            public const string RecieveInviteFromFriend = "RecieveInviteFromFriend";
            public const string NoPermissions = "NoPermissions";
            public const string NotEnoughArguments = "NotEnoughArguments";
            public const string DeleteClan = "DeleteClan";
            public const string ClanNotFound = "ClanNotFound";
            public const string InvitePlayerToClan = "InvitePlayerToClan";
            public const string SendInviteToPlayer = "SendInviteToPlayer";
            public const string CantPurge = "CantPurge";
            public const string AlreadyAClan = "AlreadyAClan";
            public const string AlreadyInAClan = "AlreadyInAClan";
            public const string CreateClan = "CreateClan";
            public const string NotInClan = "NotInClan";
            public const string NotHighEnoughClanRank = "NotHighEnoughClanRank";
            public const string NoInvites = "NoInvites";
            public const string ClanWasRemoved = "ClanWasRemoved";
            public const string ClanJoinPlayer = "ClanJoinPlayer";
            public const string PlayerJoinClan = "PlayerJoinClan";
            public const string PlayerInvites = "PlayerInvites";
            public const string ClanInvitesPlayer = "ClanInvitesPlayer";
            public const string NotAValidPlayer = "NotAValidPlayer";
            public const string PlayerAlreadyInClan = "PlayerAlreadyInClan";
            public const string PlayerNotInClan = "PlayerNotInClan";
            public const string PlayerUnableToRankUp = "PlayerUnableToRankUp";
            public const string PlayerRankedUp = "PlayerRankedUp";
            public const string PlayerNotInYourClan = "PlayerNotInYourClan";
            public const string PlayerCantKickPlayer = "PlayerCantKickPlayer";
            public const string KickedPlayerFromClan = "KickedPlayerFromClan";
            public const string NoAllies = "NoAllies";
            public const string CantLeave = "CantLeave";
            public const string CompletedLeave = "CompletedLeave";
            public const string FriendlyFire = "FriendlyFire";
            public const string CantSetHome = "CantSetHome";
            public const string SetHome = "SetHome";
            public const string NoHome = "NoHome";
        }

        public void RegisterLangMessages()
        {

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"RecieveInviteFromFriend", "Recieved and invite from your friend: {0}"},
                {"NoPermissions", "You don't have the correct permissions."},
                {"NotEnoughArguments", "You have not used the correct amount of arguments."},
                {"DeleteClan", "Deleted clan: {0}."},
                {"ClanNotFound", "Clan {0} not found!."},
                {"InvitePlayerToClan", "Clan {0} invited you to join!"},
                {"SendInviteToPlayer","Player {0} invited {1} to join {2}"},
                {"CantPurge","You can't purge data."},
                {"AlreadyAClan","There is already clan by this name!"},
                {"AlreadyInAClan","You are already in a clan! You must leave the clan {0}."},
                {"CreateClan","Created the clan {0} : {1}"},
                {"NotInClan","You are not in a clan."},
                {"NotHighEnoughClanRank","You are not a high enough position in your clan to invite."},
                {"NoInvites","You were not invite to any clan. :("},
                {"ClanWasRemoved", "This Clan is no longer valid"},
                {"ClanJoinPlayer", "You Have joined the clan {0}"},
                {"PlayerJoinClan", "{0} has joined the clan!"},
                {"PlayerInvites", "{0} has been invited the clan!"},
                {"ClanInvitesPlayer", "You have been invited to {0}."},
                {"NotAValidPlayer","The player is not found."},
                {"PlayerAlreadyInClan","The player is already in a clan."},
                {"PlayerNotInClan","The player is not in the clan."},
                {"PlayerUnableToRankUp","You are not able to rankup."},
                {"PlayerRankedUp","Ranked up {0}"},
                {"PlayerNotInYourClan"," This player is not in your clan :("},
                {"PlayerCantKickPlayer", "You can't kick someone who is higher in rank than you!"},
                {"KickedPlayerFromClan", "You Kicked {0} from the clan!"},
                {"NoAllies", "Your clan has no allies."},
                {"CantLeave", "You are unable to leave the clan as you are the owner you must do /clan disband forever to leave your clan and disband it."},
                {"CompletedLeave", "You have left your clan."},
                {"FriendlyFire", "Friendly Fire is active, you cannot hit other people in your clan."},
                {"CantSetHome" ,"You can't set home as you are not high enough rank."},
                {"SetHome","You have successfully set home!"},
                {"NoHome","Your clan has no home."}
            }, this);
            
        }

        public string GetMessage(string TargetMessage)
        {
            return lang.GetMessage(TargetMessage, this);
        }

        public class ConfigData
        {
            public List<ulong> PurgeAllowedPlayers = new List<ulong>();
            public int SaveTime = 120;
            public bool AnnounceCreation = true;
            public bool AnnounceDeletion = true;
            public bool Enabled = true;
            public string ClanTagColor = "";
            public bool FriendlyFireUse = true;
            
            public ConfigData()
            {
                PurgeAllowedPlayers = new List<ulong>();
                SaveTime = 120;
                AnnounceCreation = true;
                AnnounceDeletion = true;
                Enabled = true;
                ClanTagColor = "#ffffff";
                FriendlyFireUse = true;
               
            }
        }

        public void SaveToInviteList(ulong userId, Clan clan)
        {
            data.ClanInviteData.Add(userId, clan);
        }

        public void SaveToClanData(Clan clan)
        {
            data.ClanData.Add(clan);
        }

        public void SaveAllDataTimer()
        {
            var cfgData = Config.ReadObject<ConfigData>();
            
            ActiveTimers.Add(timer.Repeat(cfgData.SaveTime, 0, () =>
            {
                Puts("STARTING SAVE OF CLAN DATA...");
                data.SaveData(data.ClanData, DataFile.ClanData);
                data.SaveData(data.ClanInviteData, DataFile.ClanInviteData);
                Puts("SAVED ALL CLAN DATA");
            }));


        }

        public void DestroyAllTimers()
        {
            foreach (var e in ActiveTimers)
            {
                e.Destroy();
            }
        }

        public BasePlayer FindPlayer(string nameOrId)
        {
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.displayName == nameOrId) return activePlayer;
                if (activePlayer.displayName.ToLower().Contains(nameOrId.ToLower())) return activePlayer;
                if (activePlayer.UserIDString == nameOrId) return activePlayer;
            }
            return null;
        }

        public void LangMessageToPlayer(BasePlayer player, string message)
        {
            player.SendMessage($"{GetMessage(message)}");
        }


        #region Clan Managment

        private string FindClanTagOf(ulong playerId)
        {
            foreach (var e in data.ClanData)
            {
                if (e.MemberList.ContainsKey(playerId)) return e.ClanName;
            }
            return string.Empty;
        }

        private bool CheckIfPlayerInClan(ulong playerId)
        {
            foreach (var e in data.ClanData)
            {
                if (e.MemberList.ContainsKey(playerId))
                {
                    return true;
                }
            }
            return false;
        }

        private bool SendInviteTo(ulong playerId, Clan clan)
        {
            var playerFromId = BasePlayer.FindByID(playerId);

            if (playerFromId == null)
            {
                return false;
            }

            data.ClanInviteData.Add(playerId, clan);
            playerFromId.SendMessage($"{string.Format(GetMessage("ClanInvitesPlayer"), clan.ClanName)}");
            data.SaveData(data.ClanInviteData, DataFile.ClanInviteData);
            return true;
        }

        private bool PlayerInClan(ulong playerId, Clan clan)
        {
            if (clan.MemberList.ContainsKey(playerId))
            {
                return true;
            }
            return false;
        }

        private void AddPlayerToClan(ulong playerId, string clanName)
        {
            var playerFromId = BasePlayer.FindByID(playerId);

            foreach (var e in data.ClanData)
            {
                if (e.ClanName == clanName)
                {
                    e.MemberList.Add(playerId, Rank.Normal);
                }
            }
        }

        private void DeleteClan(string name)
        {
            data.ReadData(ref data.ClanData, DataFile.ClanData);

            foreach (var e in data.ClanData)
            {
                if (e.ClanName == name)
                {
                    data.ClanData.Remove(e);
                    data.SaveData(data.ClanData, DataFile.ClanData);
                }
            }
        }

        private bool CheckIfClanExists(string name)
        {
            foreach (var e in data.ClanData)
            {
                if (e.ClanName == name)
                {
                    return true;
                }
            }
            return false;
        }

        private Clan GetClanOf(ulong playerId)
        {
            foreach (var e in data.ClanData)
            {
                if (e.MemberList.ContainsKey(playerId))
                {
                    return e;
                }
            }
            return null;
        }

        private Clan ClanFindById(int clanId)
        {
            foreach (var e in data.ClanData)
            {
                if (e.Id == clanId)
                {
                    return e;
                }
            }
            return null;
        }

        private void ClanBroadcast(Clan targetClan, string message)
        {
            foreach (var e in targetClan.MemberList)
            {
                BasePlayer player = BasePlayer.FindByID(e.Key);
                player.SendMessage(message);
            }
        }

        private void AllyBroadcast(Clan TargetClan, string message)
        {
            foreach (var e in TargetClan.AllianceList)
            {
                var allyClan = ClanFindById(e);
                ClanBroadcast(allyClan, message);
            }

        }

        private bool DoesClanHaveAllies(Clan targetClan)
        {
            if (targetClan.AllianceList.Count == 0)
            {
                return false;
            }
            return true;
        }

        private PlayerData GetPlayerDataOf(BasePlayer player)
        {
            foreach (var e in data.PlayerData)
            {
                if (e.PlayerID == player.userID)
                {
                    return e;
                }
            }
            return null;
        }

        #endregion Clan Managment

        #endregion Helpers

        #region Hooks

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            var cfgData = Config.ReadObject<ConfigData>();
            if (info.InitiatorPlayer == null)
            {
                return null;
            }
            if (entity.ToPlayer() == null)
            {
                return null;
            }

            var targetPlayer = info.Initiator as BasePlayer;
            var victim = entity as BasePlayer;

            if (!CheckIfPlayerInClan(targetPlayer.userID))
            {
                return null;
            }
            if (!CheckIfPlayerInClan(victim.userID))
            {
                return null;
            }

            var targetPlayerClan = GetClanOf(targetPlayer.userID);
            var victimClan = GetClanOf(victim.userID);

            if (targetPlayerClan == victimClan && cfgData.FriendlyFireUse == true && victimClan.FriendlyFireActive == true)
            {
                LangMessageToPlayer(targetPlayer, LangMessages.FriendlyFire);
                return false;
            }

            return null;
        }

        void OnPlayerInit(BasePlayer player)
        {
            var cfgData = Config.ReadObject<ConfigData>();
            var targetClan = GetClanOf(player.userID);
            var playerData = new PlayerData(player, targetClan);
            var clanTag = GetPlayerDataOf(BasePlayer.FindByID(player.userID)).Clantag;
            var name = GetPlayerDataOf(BasePlayer.FindByID(player.userID)).Name;

            if (data.PlayerData.Contains(playerData))
            {
                return;
            }

            data.PlayerData.Add(playerData);
            data.SaveData(data.PlayerData, DataFile.PlayerData);
            player.displayName = $"<color={cfgData.ClanTagColor}>{clanTag}</color> {name}";
        }

        private void Loaded()
        {  
            RegisterLangMessages();
            RegisterAllPerms();
            SaveAllDataTimer();
        }

        private void UnLoad()
        {
            DestroyAllTimers();
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            ConfigData cfgData = new ConfigData();
            Config.WriteObject(cfgData);
            Config.Save();
            Config.Load();
        }

        #endregion Hooks

        #region Command

        [ChatCommand("clan")]
        private void ClanCommand(BasePlayer player, string command, string[] args)
        {
            var cfgData = Config.ReadObject<ConfigData>();

            switch (args.Length)
            {
                case 3:
                    if (args[0].ToLower() == "create")
                    {
                        var name = args[1];
                        var desc = args[2];
                        var clanClass = new Clan(name, desc, player.userID);

                        if (!CheckIfPlayerHasPerm(player.UserIDString, Permission.Create))
                        {
                            LangMessageToPlayer(player, LangMessages.NoPermissions);
                            return;
                        }

                        if (CheckIfClanExists(name))
                        {
                            LangMessageToPlayer(player, LangMessages.ClanNotFound);
                            return;
                        }

                        if (CheckIfPlayerInClan(player.userID))
                        {
                            player.ChatMessage($"{string.Format(GetMessage("AlreadyInAClan"), GetClanOf(player.userID))}");
                            return;
                        }

                        data.ClanData.Add(clanClass);
                        data.SaveData(data.ClanData, DataFile.ClanData);
                        player.ChatMessage($"{string.Format(GetMessage("CreateClan"), clanClass.ClanName, clanClass.Description)}");
                        return;
                    }

                    if (args[0].ToLower() == "disband")
                    {
                        if (args[1].ToLower() == "forever")
                        {

                            if (!CheckIfPlayerHasPerm(player.UserIDString, Permission.Disband))
                            {
                                LangMessageToPlayer(player, LangMessages.NoPermissions);
                                return;
                            }

                            if (!CheckIfPlayerInClan(player.userID))
                            {
                                LangMessageToPlayer(player, LangMessages.NotInClan);
                                return;
                            }

                            var targetClan = GetClanOf(player.userID);

                            data.ClanData.Remove(targetClan);
                            data.SaveData(data.ClanData, DataFile.ClanData);

                            LangMessageToPlayer(player, LangMessages.DeleteClan);

                        }

                    }

                    if (args[0].ToLower() == "invite")
                    {

                    }
                    break;

                case 2:

                    if (args[0].ToLower() == "deleteclan")
                    {
                        if (!CheckIfPlayerHasPerm(player.UserIDString, Permission.Admin))
                        {
                            LangMessageToPlayer(player, LangMessages.NoPermissions);
                            return;
                        }
                        if (CheckIfClanExists(args[1]))
                        {
                            DeleteClan(args[1]);
                            player.ChatMessage(string.Format(lang.GetMessage("DeleteClan", this), args[1]));
                            return;
                        }
                        player.ChatMessage(string.Format(lang.GetMessage("ClanNotFound", this), args[1]));
                        return;
                    }

                    if (args[0].ToLower() == "invite")
                    {
                        if (!CheckIfPlayerHasPerm(player.UserIDString, Permission.Invite))
                        {
                            LangMessageToPlayer(player, LangMessages.NoPermissions);
                            return;
                        }

                        if (!CheckIfPlayerInClan(player.userID))
                        {
                            LangMessageToPlayer(player, LangMessages.NotInClan);
                            return;
                        }

                        var targetPlayer = FindPlayer(player.UserIDString);

                        if (targetPlayer == false)
                        {
                            LangMessageToPlayer(player, LangMessages.NotAValidPlayer);
                            return;
                        }

                        var playerClanClass = GetClanOf(player.userID);
                        var getRankFromPlayer = playerClanClass.MemberList[player.userID];

                        if (GetClanOf(targetPlayer.userID) == null)
                        {
                            LangMessageToPlayer(player, LangMessages.NotAValidPlayer);
                            return;
                        }

                        switch (getRankFromPlayer)
                        {
                            case Rank.Council:
                                break;
                            case Rank.Moderator:
                                break;
                            case Rank.Owner:
                                break;
                            default:
                                LangMessageToPlayer(player, LangMessages.NotHighEnoughClanRank);
                                return;
                        }

                        SendInviteTo(targetPlayer.userID, playerClanClass);

                    }

                    if (args[0].ToLower() == "join")
                    {
                        if (!data.ClanInviteData.ContainsKey(player.userID))
                        {
                            LangMessageToPlayer(player, LangMessages.NoPermissions);
                            return;
                        }

                        var targetClan = data.ClanInviteData[player.userID];

                        if (CheckIfPlayerInClan(player.userID))
                        {
                            LangMessageToPlayer(player, LangMessages.AlreadyInAClan);
                        }

                        if (!data.ClanData.Contains(targetClan))
                        {
                            player.SendMessage($"{string.Format(GetMessage("ClanNotFound"), targetClan.ClanName)}");
                            return;
                        }
                        targetClan.MemberList.Add(player.userID, Rank.Normal);

                        player.SendMessage($"{string.Format(GetMessage("ClanJoinPlayer"), targetClan.ClanName)}");

                        ClanBroadcast(targetClan, $"{string.Format(GetMessage("PlayerJoinClan"), player.displayName)}");

                        return;
                    }

                    if (args[0].ToLower() == "promote")
                    {
                        if (!CheckIfPlayerInClan(player.userID))
                        {
                            LangMessageToPlayer(player, LangMessages.NotInClan);
                            return;
                        }
                        if (!CheckIfPlayerHasPerm(player.UserIDString, Permission.Promote))
                        {
                            player.SendMessage($"{GetMessage(LangMessages.NoPermissions)}");
                            return;
                        }

                        if (!CheckIfPlayerInClan(player.userID))
                        {
                            LangMessageToPlayer(player, LangMessages.NotInClan);
                            return;
                        }

                        var targetPlayer = FindPlayer(args[1]);
                        var playerClan = GetClanOf(targetPlayer.userID);

                        if (!PlayerInClan(targetPlayer.userID, playerClan))
                        {
                            LangMessageToPlayer(player, LangMessages.PlayerNotInClan);
                            return;
                        }

                        var targetPlayerRank = playerClan.MemberList[targetPlayer.userID];
                        playerClan.MemberList.Remove(targetPlayer.userID);

                        switch (targetPlayerRank)
                        {
                            case (Rank.Normal):
                                playerClan.MemberList.Add(targetPlayer.userID, Rank.Moderator);
                                data.SaveData(data.ClanData, DataFile.ClanData);
                                player.SendMessage($"{string.Format(GetMessage("PlayerRankedUp"), player.displayName)}");
                                return;

                            case (Rank.Moderator):
                                playerClan.MemberList.Add(targetPlayer.userID, Rank.Council);
                                data.SaveData(data.ClanData, DataFile.ClanData);
                                player.SendMessage($"{string.Format(GetMessage("PlayerRankedUp"), player.displayName)}");
                                return;
                            case (Rank.Council):
                                LangMessageToPlayer(player, LangMessages.PlayerUnableToRankUp);
                                return;
                            case (Rank.Owner):
                                LangMessageToPlayer(player, LangMessages.PlayerUnableToRankUp);
                                return;
                            default:
                                return;
                        }

                    }

                    if (args[0].ToLower() == "kick")
                    {
                        if (!CheckIfPlayerHasPerm(player.UserIDString, Permission.Admin))
                        {
                            player.SendMessage($"{GetMessage(LangMessages.NoPermissions)}");
                            return;
                        }

                        if (!CheckIfPlayerInClan(player.userID))
                        {
                            LangMessageToPlayer(player, LangMessages.NotInClan);
                            return;
                        }

                        var targetPlayer = FindPlayer(args[1]);

                        if (targetPlayer == null)
                        {
                            LangMessageToPlayer(player, LangMessages.NotAValidPlayer);
                            return;
                        }

                        var playerClan = GetClanOf(player.userID);

                        if (playerClan == null)
                        {
                            LangMessageToPlayer(player, LangMessages.NotInClan);
                            return;
                        }

                        var targetPlayerClan = GetClanOf(targetPlayer.userID);

                        if (targetPlayerClan == null)
                        {
                            LangMessageToPlayer(player, LangMessages.PlayerNotInClan);
                            return;
                        }

                        if (targetPlayerClan != playerClan)
                        {
                            LangMessageToPlayer(player, LangMessages.PlayerNotInYourClan);
                            return;
                        }

                        if (playerClan.MemberList[player.userID] <= Rank.Normal)
                        {
                            LangMessageToPlayer(player, LangMessages.NotHighEnoughClanRank);
                            return;
                        }

                        if (playerClan.MemberList[targetPlayer.userID] > playerClan.MemberList[player.userID])
                        {
                            LangMessageToPlayer(player, LangMessages.PlayerCantKickPlayer);
                        }

                        playerClan.MemberList[targetPlayer.userID] = playerClan.MemberList[targetPlayer.userID]++;
                        data.SaveData(data.ClanData, DataFile.ClanData);

                        player.SendMessage(string.Format($"{GetMessage("KickedPlayerFromClan")}", targetPlayer.displayName));
                        return;
                    }
                    
                    break;

                case 1:
                    if (args[0].ToLower() == "purgeallclans")
                    {
                        if (!CheckIfPlayerHasPerm(player.UserIDString, Permission.Admin))
                        {
                            LangMessageToPlayer(player, LangMessages.NoPermissions);
                            return;
                        }
                        if (!cfgData.PurgeAllowedPlayers.Contains(player.userID))
                        {
                            LangMessageToPlayer(player, LangMessages.CantPurge);
                            return;
                        }

                        data.ReadData(ref data.ClanData, DataFile.ClanData);
                        data.ReadData(ref data.ClanInviteData, DataFile.ClanInviteData);

                        foreach (var e in data.ClanData)
                        {
                            data.ClanData.Remove(e);
                        }

                        foreach (var e in data.ClanInviteData)
                        {
                            data.ClanInviteData.Remove(e.Key);
                        }
                        data.SaveData(data.ClanData, DataFile.ClanData);
                        data.SaveData(data.ClanData, DataFile.ClanData);
                    }

                    if (args[0].ToLower() == "leave")
                    {
                        if (!permission.UserHasPermission(player.UserIDString, Permission.Leave))
                        {
                            LangMessageToPlayer(player, LangMessages.NoPermissions);
                            return;
                        }

                        if (!CheckIfPlayerInClan(player.userID))
                        {
                            LangMessageToPlayer(player, LangMessages.NotInClan);
                            return;
                        }

                        var targetClan = GetClanOf(player.userID);
                        var targetClanMemberList = targetClan.MemberList;
                        
                        if (targetClanMemberList[player.userID] > Rank.Owner)
                        {
                            LangMessageToPlayer(player, LangMessages.CantLeave);
                            return;
                        }

                        targetClanMemberList.Remove(player.userID);
                        data.SaveData(data.ClanData, DataFile.ClanData);
                        LangMessageToPlayer(player, LangMessages.CompletedLeave);
                        return;
                    }

                    if (args[0].ToLower() == "sethome")
                    {

                        if (!permission.UserHasPermission(player.UserIDString, Permission.SetHome))
                        {
                            LangMessageToPlayer(player, LangMessages.NoPermissions);
                            return;
                        }

                        if (!CheckIfPlayerInClan(player.userID))
                        {
                            LangMessageToPlayer(player, LangMessages.NotInClan);
                            return;
                        }

                        var targetClan = GetClanOf(player.userID);

                        if (!(targetClan.MemberList[player.userID] > Rank.Moderator))
                        {
                            LangMessageToPlayer(player, LangMessages.CantSetHome);
                            return;
                        }

                        targetClan.HasHome = true;
                        targetClan.ClanHome = player.transform.position;
                        LangMessageToPlayer(player, LangMessages.SetHome);
                        return;
                    }

                    if (args[0].ToLower() == "home")
                    {
                        if (!permission.UserHasPermission(player.UserIDString, Permission.SetHome))
                        {
                            LangMessageToPlayer(player, LangMessages.NoPermissions);
                            return;
                        }

                        if (!CheckIfPlayerInClan(player.userID))
                        {
                            LangMessageToPlayer(player, LangMessages.NotInClan);
                            return;
                        }

                        var targetClan = GetClanOf(player.userID);

                        if (!targetClan.HasHome)
                        {
                            LangMessageToPlayer(player, LangMessages.NoHome);
                            return;
                        }
                        

                    }

                    
                    break;

                default:
                    LangMessageToPlayer(player, LangMessages.NotEnoughArguments);
                    break;
            }

        }

        [ChatCommand("c")]
        private void ClanChatCommand(BasePlayer player, string command, string[] args)
        {
            if (!CheckIfPlayerHasPerm(player.UserIDString, Permission.ClanChat))
            {
                LangMessageToPlayer(player, LangMessages.NoPermissions);
                return;

            }

            if (!CheckIfPlayerInClan(player.userID))
            {
                LangMessageToPlayer(player, LangMessages.NotInClan);
                return;
            }

            var targetClan = GetClanOf(player.userID);
            ClanBroadcast(targetClan, args.ToString());
            Puts(args.ToString());
        }

        [ChatCommand("a")]
        private void AllianceChatCommand(BasePlayer player, string command, string[] args)
        {
            if (!CheckIfPlayerHasPerm(player.UserIDString, Permission.ClanChat))
            {
                LangMessageToPlayer(player, LangMessages.NoPermissions);
                return;
            }

            if (!CheckIfPlayerInClan(player.userID))
            {
                LangMessageToPlayer(player, LangMessages.NotInClan);
                return;
            }

            var targetClan = GetClanOf(player.userID);

            if (!DoesClanHaveAllies(targetClan))
            {
                LangMessageToPlayer(player, LangMessages.NoAllies);
                return;
            }

            AllyBroadcast(targetClan, args.ToString());
            
        }

        #endregion Command
    }
}
