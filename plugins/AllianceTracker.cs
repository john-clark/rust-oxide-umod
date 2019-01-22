#region using-directives
using System.Collections.Generic;
using CodeHatch.Common;
using Oxide.Core;
using CodeHatch.Engine.Modules.SocialSystem;
using CodeHatch.Thrones.SocialSystem;
using CodeHatch.Engine.Networking;
using CodeHatch.UserInterface.Dialogues;
using System.Linq;
using Oxide.Core.Plugins;


#endregion
#region Header
namespace Oxide.Plugins
{
    [Info("AllianceTracker", "juk3b0x & Scorpyon", "2.0.0")]
    public class AllianceTracker : ReignOfKingsPlugin
    {
        #endregion
        [PluginReference("StateAssets")]
        Plugin StateAssets;
        #region Inherited Classes
        private class Alliance
        {
            public string Name { get; set; }
            public ulong Owner { get; set; }
            public List<ulong> MemberGuilds { get; set; }

            public Alliance() { }

            public Alliance(ulong owner,  List<ulong> memberGuilds, string name)
            {
                Name = name;
                Owner = owner;
                MemberGuilds = memberGuilds;
            }
        }
        #endregion
        #region Language API
        void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //PopUp Messages
                { "OKBUTTON", "Ok" },
                { "ACCEPTBUTTON", "Accept" },
                { "DECLINEBUTTON", "Decline" },
                { "CANCELBUTTON", "Cancel" },
                { "TitleShowAlliances", "The Current Alliances" },
                { "TextShowAlliances", "{0}{1}The Maximum number of alliances on this server is set to: {2}" },
                { "TitleInviteAlly", "Form a new Alliance" },
                { "TextInviteAlly", "{0}{1}Type The name of the Person you want to ally with" },
                { "TextSendInviteToPlayer", "You Have been invited to form an Alliance by {0}!" },
                { "TitleSendInviteToPlayerApplication", "Application to join your alliance" },
                { "TextSendInviteToPlayerApplication", "You have received an application from {0} to join your alliance!" },
                { "TitleMergeTwoAlliances", "Merge with another Alliance" },
                { "TextMergeTwoAlliancesReceiver", "You Have been invited to merge your alliance with the alliance of {0}!" },
                { "TitleRenameAlliance", "Rename your alliance" },
                { "TextRenameAlliance", "Type your new Alliance-Name:" },
                { "TitleLeaveAlliance", "[FF0000]ALERT[FFFFFF]" },
                { "TextLeaveAllianceLeader", "You are about to DISBAND the whole alliance!{0}Do you want to continue?" },
                { "TextLeaveAllianceSubordinate", "You are about to LEAVE {0}!{1}Do you want to continue? " },
                { "TitleKickFromAlliance", "Excommunicate a Guild from the Alliance" },
                { "TextKickFromAlliance", "Your Allies:{0}{1}{2}Type the ID of the Guild you Want to Excommunicate:" },
                { "TitleExcommunicationReceive", "Choose wisely, changing your profession later will cost {0} {1} !" },
                //Chat Messages
                { "NotAllied", "You need to be allied with someone, to do that ..." },
                { "NotAlliedWith", "It seems you are not allied with {0}! Thus you cannot do that!" },
                { "SelfAlly", "The only REAL ally is oneself... BUT NOT ON THIS ISLAND!!" },
                { "NotLeader", "You are not the leader of this Alliance, you cannot do that" },
                { "ReturnKickReceivers", "Your guild has been excommunicated from {0}!" },
                { "ReturnKickKickers", "Your leader {0} has excommunicated {1} from the alliance!" },
                { "ReturnKickLeader", "You have excommunicated {0} from your alliance!" },
                { "NoArgument", "You HAVE to enter something in the field!" },
                { "CANCELLED", "You have cancelled the process!" },
                { "NoGuildClearance", "You must be the owner of your Guild to do that!" },
                { "MaxAlliancesReached", "The Maximum number of {0} alliances is already reached on this server! You cannot create a new alliance, try alliyng with another" },
                { "MaxGuildsReached1", "Your Alliance is full break up with an existing member to invite a new one!" },
                { "MaxGuildsReached2", "The Person you tried to invite is already allied AND their alliance is full, sorry." },
                { "MaxGuildsReached3", "{0} applied to join your alliance, but your alliance is already full." },
                { "GuildLess", "For some reason that player is GUILDLESS, contact an administrator about the issue!" },
                { "PlayerOfflineOrUnavailable", "You either misspelled the playername, or that player is offline, try again!" },
                { "LeaderOffline", "The Leader of the Alliance you want to merge with is currently away, try again, when he is back!" },
                { "LeaderOffline2", "The Leader of the Guild you want to ally with is currently away, try again, when he is back!" },
                { "AttemptMergeExceedSender", "Sorry, the formed Alliance would exceed the Maximum Member Limit, get rid of some useless Allies maybe ;)" },
                { "AttemptMergeExceedReceiver", "Another Alliance tried to merge with you, but your two alliances together would exceed the maximum Limit, maybe get rid of some useless allies ;)" },
                { "MergeTwoGuildsLeaderAccepted", "You have formed a new alliance with {0}, You are the new Alliance Leader!" },
                { "MergeTwoGuildsSubOrdinateAccepted", "you have accepted {0}'s request for an alliance, {0} is now your Alliance Leader!" },
                { "MergeTwoGuildsLeaderDeclined", "{0} has declined your request to ally with them!" },
                { "MergeTwoGuildsSubOrdinateDeclined", "You have declined {0}'s request for an alliance!" },
                { "MergeAllyToPlayerSubordinateAccepted", "{0} accepted your application for {1}! Welcome aboard!" },
                { "MergeAllyToPlayerLeaderAccepted", "You have accepted {0}'s application for joining your alliance!" },
                { "MergeAllyToPlayerSubordinateDeclined", "{0} has declined your request for an alliance!" },
                { "MergePlayerToAllyLeaderAccepted", "{0} has accepted, he joined your alliance!" },
                { "MergePlayerToAllySubordinateAccepted", "You have successfully joined {0}!" },
                { "MergePlayerToAllyLeaderDeclined", "{0} has declined your request for joining his alliance" },
                { "MergePlayerToAllySubordinateDeclined", "You have declined {0}'s request for an alliance with you!" },
                { "MergeTwoAlliancesLeaderAccepted", "{0} has accepted your request to merge your alliances, you are the new Leader!" },
                { "MergeTwoAlliancesSubordinateAccepted", "You have accepted {0}'s request to merge your alliances. {0} is now your new Leader!" },
                { "MergeTwoAlliancesLeaderDeclined", "{0} has declined your request to merge your alliances" },
                { "MergeTwoAlliancesSubordinateDeclined", "You have declined {0}'s request to merge your alliances!" },
                { "SendRenameReceivers", "Your Alliance has been renamed to {0} by {1}!" },
                { "LeaveReceivers", "The guild {0} has left {1}!" },
                { "LeaveSender", "You have left {0}!" },
                { "LeaveLast", "Your last ally has left the alliance, you are on your own again!" },
                { "DisbandOrCancelReceivers", "Your alliance {0} has been disbanded by {1}! " },
                { "DisbandOrCancelSender", "You have  disbanded your alliance, everybody is on his own again! " },
                { "TryingToAllyWithAllyReceiver", "{0} tried to Invite you to ally with him... AGAIN ! You two should Kiss!" },
                { "TryingToAllyWithAllySender", "You are already allied with that guild!" }
            }, this);
        }
        string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);
        #endregion
        #region Config
        int MaxMembersPerAlliance;
        int MaxAlliances;
        void Init() => LoadDefaultConfig();

        protected override void LoadDefaultConfig()
        {
            Config["Maximum Members per Alliance"] = MaxMembersPerAlliance = GetConfig("Maximum Members per Alliance", 3);
            Config["Maximum Number of Alliances on the Server"] = MaxAlliances = GetConfig("Maximum Number of Alliances on the Server", 2);
            SaveConfig();
        }
        T GetConfig<T>(string name, T defaultValue) => Config[name] == null ? defaultValue : (T)System.Convert.ChangeType(Config[name], typeof(T));
        #endregion
        #region Lists
        List<Alliance> _AllianceList = new List<Alliance>();
        //Dictionary<ulong, ulong[]> _AllianceList = new Dictionary<ulong, ulong[]>();
        // _AllianceList.Owner = AllianceOwnerId
        // _AllianceList.MemberGuilds = All participating GuildIDs
        #endregion
        #region List loading and saving
        void LoadLists()
        {
            _AllianceList = Interface.GetMod().DataFileSystem.ReadObject<List<Alliance>>("AllianceList");
        }
        void SaveAllianceList()
        {
            Interface.GetMod().DataFileSystem.WriteObject("AllianceList", _AllianceList);
        }
        void Loaded()
        {
            LoadLists();
        }
        #endregion
        #region PopUps and Chat-Commands
        [ChatCommand("alliances")]
        void ShowAllAlliances(Player player)
        {
            LoadLists();
            string Message = "";
            for (var i=0; i< _AllianceList.Count;i++)
            {
                string guildname = "";
                for (var o =0; o< _AllianceList.ElementAt(i).MemberGuilds.Count; o++)
                {
                    guildname = guildname  + SocialAPI.Get<GuildScheme>().TryGetGuild(_AllianceList.ElementAt(i).MemberGuilds.ElementAt(o)).Name + "\n";
                }
                Message = Message + "[FF0000]" + _AllianceList.ElementAt(i).Name + "[FFFFFF]" + "\n" + guildname;
            }
            player.ShowPopup(string.Format(GetMessage("TitleShowAlliances", player.Id.ToString())), string.Format(GetMessage("TextShowAlliances", player.Id.ToString()), Message, "\n\n", MaxAlliances.ToString()), string.Format(GetMessage("OKBUTTON", player.Id.ToString())));
        }
        [ChatCommand("kickally")]
        void KickFromAlliance(Player player)
        {
            if (!IsAllied(player.GetGuild()))
            {
                PrintToChat(player, string.Format(GetMessage("NotAllied",player.Id.ToString())));
                return;
            }
            if (!IsAllianceLeader(player))
            {
                PrintToChat(player, string.Format(GetMessage("NotLeader")));
                return;
            }
            string Message = "";
            Alliance allianceToKickFom = new Alliance();
            for (var i = 0; i < _AllianceList.Count();i++)
            {
                if (_AllianceList.ElementAt(i).Owner == player.Id)
                {
                    allianceToKickFom = _AllianceList.ElementAt(i);
                   foreach (ulong guild in _AllianceList.ElementAt(i).MemberGuilds)
                    {
                        Message = Message + SocialAPI.Get<GuildScheme>().TryGetGuild(guild).Name.PadRight(20) + "Guild ID: " + guild.ToString()+ "\n";
                    }
                }
            }
            player.ShowInputPopup(string.Format(GetMessage("TitleKickFromAlliance",player.Id.ToString())), string.Format(GetMessage("TextKickFromAlliance",player.Id.ToString()), "\n\n", Message, "\n\n" ), "",string.Format(GetMessage("OKBUTTON", player.Id.ToString())), string.Format(GetMessage("CANCELBUTTON", player.Id.ToString())), (options, dialogue1, data) => ReturnKick(player, options, dialogue1, data, allianceToKickFom));
        }
        void ReturnKick (Player player, Options options, Dialogue dialogue1, object data, Alliance allianceToKickFrom)
        {
            if (dialogue1.ValueMessage.IsNullEmptyOrWhite())
            {
                PrintToChat(player, string.Format(GetMessage("NoArgument",player.Id.ToString())));
                return;
            }
            string guildstring = string.Concat(dialogue1.ValueMessage);
            ulong guildID = ulong.Parse(guildstring);
            if (!allianceToKickFrom.MemberGuilds.Contains(guildID))
            {
                PrintToChat(player, string.Format(GetMessage("NotAlliedWith", player.Id.ToString()), SocialAPI.Get<GuildScheme>().TryGetGuild(guildID).Name));
                return;
            }
            if (options == Options.OK)
            {
                if (guildID == player.GetGuild().BaseID)
                {
                    LeaveAlliance(player);
                    return;
                }
                for (var i =0; i<_AllianceList.Count;i++)
                {
                    if (_AllianceList.ElementAt(i).Owner == player.Id)
                    {
                        foreach (ulong guild in _AllianceList.ElementAt(i).MemberGuilds)
                        {
                            if (guild == guildID)
                            {
                                List<Player> excommunicatedPlayers = SocialAPI.Get<GuildScheme>().GetPlayersByGuildId(guild);
                                foreach (Player excommunicatedPlayer in excommunicatedPlayers)
                                {
                                    PrintToChat(excommunicatedPlayer, string.Format(GetMessage("ReturnKickReceivers",player.Id.ToString()), _AllianceList.ElementAt(i).Name));
                                }
                            }
                            List<Player> alliancePlayers = SocialAPI.Get<GuildScheme>().GetPlayersByGuildId(guild);
                            foreach (Player alliancePlayer in alliancePlayers)
                            {
                                if (alliancePlayer != player)
                                {
                                    PrintToChat(alliancePlayer, string.Format(GetMessage("ReturnKickKickers", player.Id.ToString()), Server.GetPlayerById(_AllianceList.ElementAt(i).Owner).Name, SocialAPI.Get<GuildScheme>().TryGetGuild(guild).Name));
                                }
                            }
                        }
                        _AllianceList.ElementAt(i).MemberGuilds.Remove(guildID);
                        PrintToChat(player, string.Format(GetMessage("ReturnKickLeader", player.Id.ToString()), SocialAPI.Get<GuildScheme>().TryGetGuild(guildID).Name ));
                        return;
                    }
                }
            }
            PrintToChat(player, string.Format(GetMessage("CANCELLED", player.Id.ToString())));
        }
        [ChatCommand("inviteally")]
        void AllianceInvite(Player player)
        {
            Puts(SocialAPI.GetGroupId(player.Id).ToString());
            LoadLists();
            string Message = "";
            
            string OnlinePlayers = "";
            foreach (Player onlinePlayer in Server.AllPlayersExcept(Server.GetPlayerByName("Server"), player))
            {
                OnlinePlayers = OnlinePlayers + onlinePlayer.Name + "\n";
            }
            foreach (Alliance ally in _AllianceList)
            {
                string guilds = "";
                foreach (ulong guild in ally.MemberGuilds)
                {
                    guilds = guilds + " " + SocialAPI.Get<GuildScheme>().TryGetGuild(guild).Name + "\n";
                }
                Message = Message + ally.Name + "\n" + guilds + "\n\n";
            }
            if (!IsAllied(player.GetGuild()))
            {
                if (!IsGuildOwner(player))
                {
                    PrintToChat(player, string.Format(GetMessage("NoGuildClearance", player.Id.ToString())));
                    return;
                }
                player.ShowPopup(string.Format(GetMessage("TitleShowAlliances", player.Id.ToString())),string.Format(GetMessage("TextShowAlliances", player.Id.ToString()), Message, "\n\n", MaxAlliances.ToString()), string.Format(GetMessage("OKBUTTON",player.Id.ToString())));
                player.ShowInputPopup(string.Format(GetMessage("TitleInviteAlly", player.Id.ToString())), string.Format(GetMessage("TextInviteAlly",player.Id.ToString()), OnlinePlayers, "\n\n") , "", string.Format(GetMessage("OKBUTTON",player.Id.ToString())), string.Format(GetMessage("CANCELBUTTON", player.Id.ToString())), (options, dialogue1, data) => SendInviteToPlayer( player, options, dialogue1, data));
                return;
            }
            if (!IsAllianceLeader(player))
            {
                PrintToChat(player, string.Format(GetMessage("NotLeader", player.Id.ToString())));
                return;
            }
            if (AllianceFull(player))
            {
                PrintToChat(player, string.Format(GetMessage("MaxGuildsReached1", player.Id.ToString())));
                return;
            }
            player.ShowPopup(string.Format(GetMessage("TitleShowAlliances", player.Id.ToString())), string.Format(GetMessage("TextShowAlliances", player.Id.ToString()), Message, "\n\n", MaxAlliances.ToString()), string.Format(GetMessage("OKBUTTON", player.Id.ToString())));
            player.ShowInputPopup(string.Format(GetMessage("TitleInviteAlly", player.Id.ToString())), string.Format(GetMessage("TextInviteAlly", player.Id.ToString()), OnlinePlayers, "\n\n"), "", string.Format(GetMessage("OKBUTTON", player.Id.ToString())), string.Format(GetMessage("CANCELBUTTON", player.Id.ToString())), (options, dialogue1, data) => SendInviteToPlayer(player, options, dialogue1, data));
            return;
        }
        void SendInviteToPlayer(Player player, Options options, Dialogue dialogue1, object data)
        {
            LoadLists();
            if (dialogue1.ValueMessage.IsNullEmptyOrWhite())
            {
                PrintToChat(player, string.Format(GetMessage("NoArgument", player.Id.ToString())));
                return;
            }
            Player invitedPlayer = Server.GetPlayerByName(dialogue1.ValueMessage);
            if (invitedPlayer == null)
            {
                PrintToChat(player, string.Format(GetMessage("PlayerOfflineOrUnavailable", player.Id.ToString())));
                return;
            }
            ulong invitedPlayerGuildId = SocialAPI.GetGroupId(invitedPlayer.Id);
            if(invitedPlayerGuildId == 0)
            {
                PrintToChat(player, string.Format(GetMessage("GuildLess", player.Id.ToString())));
                return;
            }
            if (!IsAllied(player.GetGuild()))
            {
                if (!IsAllied(invitedPlayer.GetGuild()))
                {
                    if (_AllianceList.Count >= MaxAlliances)
                    {
                        PrintToChat(player, string.Format(GetMessage("MaxAlliancesReached", player.Id.ToString()),MaxAlliances.ToString()));
                        return;
                    }
                    invitedPlayer.ShowConfirmPopup(string.Format(GetMessage("TitleInviteAlly", player.Id.ToString())), string.Format(GetMessage("TextSendInviteToPlayer", invitedPlayer.Id.ToString()) ,player.DisplayName.ToString()), string.Format(GetMessage("ACCEPTBUTTON")), string.Format(GetMessage("DECLINEBUTTON")), (selection, dialogue, data1) => MergeTwoGuildsToNewAlliance(player, invitedPlayer, selection, dialogue1, data));
                    return;
                }
                Player AllianceLeader = Server.GetPlayerById(AllianceLeaderId(invitedPlayer));
                if (Server.AllPlayers.Contains(AllianceLeader))
                {
                    if (options == Options.OK)
                    {
                        if (TryingToAllySelf(player, invitedPlayer))
                        {
                            return;
                        }
                        if (AllianceFull(invitedPlayer))
                        {
                            PrintToChat(player, string.Format(GetMessage("MaxGuildsReached2", player.Id.ToString())));
                            PrintToChat(invitedPlayer, string.Format(GetMessage("MaxGuildsReached3", player.Id.ToString()), player.Name));
                            return;
                        }
                        AllianceLeader.ShowConfirmPopup(string.Format(GetMessage("TitleSendInviteToPlayerApplication", AllianceLeader.Id.ToString())), string.Format(GetMessage("TextSendInviteToPlayerApplication", AllianceLeader.Id.ToString()), player.DisplayName.ToString()), string.Format(GetMessage("ACCEPTBUTTON", AllianceLeader.Id.ToString())), string.Format(GetMessage("DECLINEBUTTON", AllianceLeader.Id.ToString())), (selection, dialogue, data1) => MergeAllyToPlayer(player, AllianceLeader, selection, dialogue1, data));
                        return;
                    }
                    PrintToChat(player, string.Format(GetMessage("CANCELLED", player.Id.ToString())));
                    return;
                }
                PrintToChat(player,string.Format(GetMessage("LeaderOffline", player.Id.ToString())));
            }
            if (!IsAllied(invitedPlayer.GetGuild()))
            {
                if (AllianceFull(player))
                {
                    PrintToChat(player, string.Format(GetMessage("MaxGuildsReached1", player.Id.ToString())));
                    return;
                }
                if (!IsGuildOwner(invitedPlayer))
                {
                    ulong GuildOwnerId = PlayerExtensions.GetGuild(invitedPlayer).OwnerId;
                    Player GuildOwner = Server.GetPlayerById(GuildOwnerId);
                    if (Server.AllPlayers.Contains(GuildOwner))
                    {
                        GuildOwner.ShowConfirmPopup(string.Format(GetMessage("TitleInviteAlly", GuildOwner.Id.ToString())), string.Format(GetMessage("TextSendInviteToPlayer", GuildOwner.Id.ToString()), player.DisplayName.ToString()), string.Format(GetMessage("ACCEPTBBUTTON",GuildOwner.Id.ToString())),string.Format(GetMessage("DECLINEBUTTON", GuildOwner.Id.ToString())), (selection, dialogue, data1) => MergePlayerToAlly(player, GuildOwner, selection, dialogue1, data));
                        return;
                    }
                    PrintToChat(player, "");
                    return;
                }
                if (!Server.AllPlayers.Contains(invitedPlayer))
                {
                    PrintToChat(player, string.Format(GetMessage("LeaderOffline2", player.Id.ToString())));
                    return;
                }
                    
                invitedPlayer.ShowConfirmPopup(string.Format(GetMessage("TitleInviteAlly", invitedPlayer.Id.ToString())), string.Format(GetMessage("TextSendInviteToPlayer", invitedPlayer.Id.ToString()), player.DisplayName.ToString()), string.Format(GetMessage("ACCEPTBBUTTON", invitedPlayer.Id.ToString())), string.Format(GetMessage("DECLINEBUTTON", invitedPlayer.Id.ToString())), (selection, dialogue, data1) => MergePlayerToAlly(player, invitedPlayer, selection, dialogue1, data));
                return;
            }
            if (CountMergingMembers(player, invitedPlayer) > MaxMembersPerAlliance)
            {
                PrintToChat(player, string.Format(GetMessage("AttemptMergeExceedSender", player.Id.ToString())));
                PrintToChat(invitedPlayer, string.Format(GetMessage("AttemptMergeExceedReceiver", invitedPlayer.Id.ToString())));
                return;
            }
            if (TryingToAllySelf(player, invitedPlayer))
            {
                return;
            }
            if (TryingToAllyWithAlly(player, invitedPlayer))
            {
                return;
            }
            if (!IsAllianceLeader(invitedPlayer))
            {
                Player AllianceLeader = (Server.GetPlayerById(AllianceLeaderId(invitedPlayer)));
                if (!Server.AllPlayers.Contains(AllianceLeader))
                {
                    PrintToChat(player, string.Format(GetMessage("LeaderOffline", player.Id.ToString())));
                    return;
                }
                AllianceLeader.ShowConfirmPopup(string.Format(GetMessage("TitleMergeTwoAlliances", AllianceLeader.Id.ToString())), string.Format(GetMessage("TextMergeTwoAlliancesReceiver", AllianceLeader.Id.ToString()),player.DisplayName.ToString()), string.Format(GetMessage("ACCEPTBBUTTON", AllianceLeader.Id.ToString())), string.Format(GetMessage("DECLINEBUTTON", AllianceLeader.Id.ToString())), (selection, dialogue, data1) => MergeTwoAlliances(player, AllianceLeader, selection, dialogue1, data));
                return;
            }
            if (!Server.AllPlayers.Contains(invitedPlayer))
            {
                PrintToChat(player, string.Format(GetMessage("LeaderOffline", player.Id.ToString())));
                return;
            }
            invitedPlayer.ShowConfirmPopup(string.Format(GetMessage("TitleMergeTwoAlliances", invitedPlayer.Id.ToString())), string.Format(GetMessage("TextMergeTwoAlliancesReceiver", invitedPlayer.Id.ToString()), player.DisplayName.ToString()), string.Format(GetMessage("ACCEPTBBUTTON", invitedPlayer.Id.ToString())), string.Format(GetMessage("DECLINEBUTTON", invitedPlayer.Id.ToString())), (selection, dialogue, data1) => MergeTwoAlliances(player, invitedPlayer, selection, dialogue1, data));
            return;
        }
        void MergeTwoGuildsToNewAlliance(Player player, Player invitedPlayer, Options selection, Dialogue dialogue1, object data)
        {
            if (selection == Options.Yes)
            {
                Alliance newally = new Alliance();
                List<ulong> tempguildlist = new List<ulong>();
                tempguildlist.Add(SocialAPI.GetGroupId(invitedPlayer.Id));
                tempguildlist.Add(SocialAPI.GetGroupId(player.Id));
                newally.Name = player.Name + "'s Alliance";
                newally.Owner = player.Id;
                newally.MemberGuilds = tempguildlist;
                _AllianceList.Add(newally);
                SaveAllianceList();
                PrintToChat(player, string.Format(GetMessage("MergeTwoGuildsLeaderAccepted", player.Id.ToString()), invitedPlayer.Name));
                PrintToChat(invitedPlayer, string.Format(GetMessage("MergeTwoGuildsSubordinateAccepted", invitedPlayer.Id.ToString()),player.Name));
                return;
            }
            PrintToChat(player, string.Format(GetMessage("MergeTwoGuildsLeaderDeclined", player.Id.ToString()),invitedPlayer.Name));
            PrintToChat(invitedPlayer, string.Format(GetMessage("MergeTwoGuildsSubordinateDeclined", invitedPlayer.Id.ToString()),player.Name));
            return;
        }
        void MergeAllyToPlayer(Player player, Player invitedPlayer, Options selection, Dialogue dialogue1, object data)
        {
            if (selection == Options.Yes)
            {
                Alliance allyToRefresh = new Alliance();
                for (var i = 0; i < _AllianceList.Count(); i++)
                {
                    if (_AllianceList.ElementAt(i).Owner == invitedPlayer.Id)
                    {
                        allyToRefresh = _AllianceList.ElementAt(i);
                        _AllianceList.Remove(_AllianceList.ElementAt(i));
                    }
                }
                allyToRefresh.MemberGuilds.Add(SocialAPI.GetGroupId(player.Id));
                _AllianceList.Add(allyToRefresh);
                SaveAllianceList();
                PrintToChat(player, string.Format(GetMessage("MergeAllyToPlayerSubordinateAccepted", player.Id.ToString()),invitedPlayer.Name, allyToRefresh.Name ));
                PrintToChat(invitedPlayer, string.Format(GetMessage("MergeAllyToPlayerLeaderAccepted", invitedPlayer.Id.ToString()), player.Name.ToString()));
                return;
            }
            PrintToChat(player, string.Format(GetMessage("MergeAllyToPlayerSubordinateDeclined", player.Id.ToString()), invitedPlayer.Name.ToString()));
            PrintToChat(invitedPlayer, string.Format(GetMessage("MergeAllyToPlayerLeaderDeclined", invitedPlayer.Id.ToString()), player.Name.ToString()));
            return;
        }
        void MergePlayerToAlly(Player player, Player invitedPlayer, Options selection, Dialogue dialogue1, object data)
        {
            if (selection == Options.Yes)
            {
                Alliance allyToRefresh = new Alliance();
                for (var i = 0; i < _AllianceList.Count(); i++)
                {
                    if (_AllianceList.ElementAt(i).Owner == player.Id)
                    {
                        allyToRefresh = _AllianceList.ElementAt(i);
                        _AllianceList.Remove(_AllianceList.ElementAt(i));
                    }
                }
                allyToRefresh.MemberGuilds.Add(SocialAPI.GetGroupId(invitedPlayer.Id));
                _AllianceList.Add(allyToRefresh);
                SaveAllianceList();
                PrintToChat(player, string.Format(GetMessage("MergePlayerToAllyLeaderAccepted", player.Id.ToString()), invitedPlayer.Name));
                PrintToChat(invitedPlayer, string.Format(GetMessage("MergePlayerToAllySubordinateAccepted", invitedPlayer.Id.ToString()), allyToRefresh.Name));
                return;
            }
            PrintToChat(player, string.Format(GetMessage("MergePlayerToAllyLeaderDeclined", player.Id.ToString()), invitedPlayer.Name));
            PrintToChat(invitedPlayer, string.Format(GetMessage("MergePlayerToAllySubordinateDeclined", invitedPlayer.Id.ToString()), player.Name.ToString()));
            return;
        }
        void MergeTwoAlliances(Player player, Player invitedPlayer, Options selection, Dialogue dialogue1, object data)
        {
            if (selection == Options.Yes)
            {
                Alliance allyToMergeTo = new Alliance();
                Alliance allyToDestroy = new Alliance();
                for (var i = 0; i < _AllianceList.Count(); i++)
                {
                    if (_AllianceList.ElementAt(i).Owner == player.Id)
                    {
                        allyToMergeTo = _AllianceList.ElementAt(i);
                        _AllianceList.Remove(_AllianceList.ElementAt(i));
                    }
                    if (_AllianceList.ElementAt(i).Owner == invitedPlayer.Id)
                    {
                        allyToDestroy = _AllianceList.ElementAt(i);
                        _AllianceList.Remove(_AllianceList.ElementAt(i));
                    }
                }
                foreach (ulong guild in allyToDestroy.MemberGuilds)
                {
                    allyToMergeTo.MemberGuilds.Add(guild);
                }
                _AllianceList.Add(allyToMergeTo);
                SaveAllianceList();
                PrintToChat(player, string.Format(GetMessage("MergeTwoAlliancesLeaderAccepted",player.Id.ToString()), invitedPlayer.Name));
                PrintToChat(invitedPlayer, string.Format(GetMessage("MergeTwoAlliancesSubordinateAccepted", invitedPlayer.Id.ToString()), player.Name));
            }
            PrintToChat(player, string.Format(GetMessage("MergeTwoAlliancesLeaderDeclined", player.Id.ToString()), invitedPlayer.Name));
            PrintToChat(invitedPlayer, string.Format(GetMessage("MergeTwoAlliancesSubordinateDeclined", invitedPlayer.Id.ToString()),player.Name));
            return;
        }
        [ChatCommand("allyname")]
        void RenameAlliance(Player player)
        {
            Alliance hisAlliance = new Alliance();
            if (!IsAllied(player.GetGuild()))
            {
                PrintToChat(player, string.Format(GetMessage("NotAllied", player.Id.ToString())));
                return;
            }
            if (!IsAllianceLeader(player))
            {
                PrintToChat(player, string.Format(GetMessage("NotLeader", player.Id.ToString())));
                return;
            }
            for (var i =0; i<_AllianceList.Count;i++)
            {
                if (_AllianceList.ElementAt(i).Owner == player.Id) hisAlliance = _AllianceList.ElementAt(i);
            }
            player.ShowInputPopup(string.Format(GetMessage("TitleRenameAlliance", player.Id.ToString())),string.Format(GetMessage("TextRenameAlliance", player.Id.ToString())), hisAlliance.Name, string.Format(GetMessage("OKBUTTON", player.Id.ToString())), string.Format(GetMessage("CANCELBUTTON", player.Id.ToString())), (options, dialogue, data) => SendRename(player, options, dialogue, data, hisAlliance));
        }
        void SendRename (Player player, Options options, Dialogue dialogue, object data, Alliance hisAlliance)
        {
            if ( dialogue.ValueMessage.IsNullEmptyOrWhite())
            {
                PrintToChat(player, string.Format(GetMessage("NoArgument", player.Id.ToString())));
                return;
            }
            string newAllyname = string.Concat(dialogue.ValueMessage);
            if(options == Options.OK)
            {
                for (var i =0; i<_AllianceList.Count;i++)
                {
                    if (_AllianceList.ElementAt(i) == hisAlliance) _AllianceList.ElementAt(i).Name = newAllyname;
                }
                foreach (ulong guild in hisAlliance.MemberGuilds)
                {
                    List<Player> playerlist = SocialAPI.Get<GuildScheme>().GetPlayersByGuildId(guild);
                    foreach (Player guildplayer in playerlist)
                    {
                        PrintToChat(guildplayer, string.Format(GetMessage("SendRenameReceivers", guildplayer.Id.ToString()), newAllyname, player.Name));
                    }
                }
                SaveAllianceList();
                return;
            }
            PrintToChat(player, string.Format(GetMessage("CANCELLED", player.Id.ToString())));
        }
        [ChatCommand("leaveally")]
        void LeaveAlliance(Player player)
        {
            LoadLists();
            Alliance GuildsToRefresh = new Alliance();
            if (!IsAllied(player.GetGuild()))
            {
                PrintToChat(player, string.Format(GetMessage("NotAllied", player.Id.ToString())));
                return;
            }
            if (!IsAllianceLeader(player))
            {
                if (IsGuildOwner(player))
                {
// -----------------------------------------------------------------------------------------------------------------------------------
                    for (var i = 0; i < _AllianceList.Count; i++)
                    {
                        if (_AllianceList.ElementAt(i).MemberGuilds.Contains(SocialAPI.GetGroupId(player.Id)))
                        {
                                player.ShowConfirmPopup(string.Format(GetMessage("TitleLeaveAlliance", player.Id.ToString())),string.Format(GetMessage("TextLeaveAllianceSubordinate",player.Id.ToString()),_AllianceList.ElementAt(i).Name, "\n\n"), string.Format(GetMessage("OKBUTTON", player.Id.ToString())), string.Format(GetMessage("CANCELBUTTON", player.Id.ToString())), (options, dialogue, data) => Leave(player, options, dialogue, data));
                        }
                    }
                    return;
                }
                PrintToChat(player, string.Format(GetMessage("NoGuildClearance", player.Id.ToString())));
                return;
            }
            player.ShowConfirmPopup(string.Format(GetMessage("TitleLeaveAlliance", player.Id.ToString())), string.Format(GetMessage("TextLeaveAllianceLeader", player.Id.ToString()), "\n\n"), string.Format(GetMessage("OKBUTTON", player.Id.ToString())), string.Format(GetMessage("CANCELBUTTON", player.Id.ToString())), (options, dialogue, data) => DisbandOrCancel(player, options, dialogue, data));
        }
        void Leave(Player player, Options options, Dialogue dialogue, object data)
        {
            if (options == Options.Yes)
            {
                for (var i = 0; i < _AllianceList.Count; i++)
                {
                    if (_AllianceList.ElementAt(i).MemberGuilds.Contains(SocialAPI.GetGroupId(player.Id)))
                    {
                        foreach (ulong guild in _AllianceList.ElementAt(i).MemberGuilds)
                        {
                            List<Player> playerlist = SocialAPI.Get<GuildScheme>().GetPlayersByGuildId(guild);
                            foreach (Player guildplayer in playerlist)
                            {
                                if (guildplayer != player)
                                {
                                    PrintToChat(guildplayer, string.Format(GetMessage("LeaveReceivers", guildplayer.Id.ToString()), player.GetGuild().Name, _AllianceList.ElementAt(i).Name));
                                }
                            }
                        }
                        _AllianceList.ElementAt(i).MemberGuilds.Remove(SocialAPI.GetGroupId(player.Id));
                        PrintToChat(player, string.Format(GetMessage("LeaveSender", player.Id.ToString()), _AllianceList.ElementAt(i).Name));
                        if (_AllianceList.ElementAt(i).MemberGuilds.Count < 2)
                        {
                            foreach (var guild in _AllianceList.ElementAt(i).MemberGuilds)
                            {
                                List<Player> tempplayers = SocialAPI.Get<GuildScheme>().GetPlayersByGuildId(guild);
                                foreach (Player tempplayer in tempplayers)
                                {
                                    PrintToChat(tempplayer, string.Format(GetMessage("LeaveLast", tempplayer.Id.ToString())));
                                }
                            }

                            _AllianceList.Remove(_AllianceList.ElementAt(i));
                            SaveAllianceList();
                            return;
                        }
                        SaveAllianceList();
                    }
                }
            }
            PrintToChat(player, string.Format(GetMessage("CANCELLED", player.Id.ToString())));
            return;
        }
        void DisbandOrCancel(Player player, Options options, Dialogue dialogue, object data)
        {

            LoadLists();

            if (options == Options.Yes)
            {
                if (StateAssets != null)
                { 
                    StateAssets.Call<object>("SplitGold", player);
                }
                for (var i = 0; i < _AllianceList.Count; i++)
                {
                    if (_AllianceList.ElementAt(i).Owner == player.Id)
                    {
                        foreach (ulong guild in _AllianceList.ElementAt(i).MemberGuilds)
                        {
                            List<Player> playerlist = SocialAPI.Get<GuildScheme>().GetPlayersByGuildId(guild);
                            foreach (Player guildplayer in playerlist)
                            {
                                if (guildplayer != player)
                                {
                                    PrintToChat(guildplayer, string.Format(GetMessage("DisbandOrCancelReceivers", guildplayer.Id.ToString()),_AllianceList.ElementAt(i).Name, player.Name));
                                } 
                            }
                        }
                        _AllianceList.Remove(_AllianceList.ElementAt(i));
                    }
                }  
                SaveAllianceList();
                PrintToChat(player, string.Format(GetMessage("DisbandOrCancelSender", player.Id.ToString())));

                return;
            }
            PrintToChat(player, string.Format(GetMessage("CANCELLED", player.Id.ToString())));
            return;
        }
        #endregion
        #region Checks
        ulong AllianceLeaderId (Player player)
        {
            LoadLists();
            ulong LeaderId = 0;
            ulong PlayerGuildId = SocialAPI.GetGroupId(player.Id);
            for (var i =0; i< _AllianceList.Count; i++)
            {
                if (_AllianceList.ElementAt(i).MemberGuilds.Contains(PlayerGuildId))
                {
                    LeaderId = _AllianceList.ElementAt(i).Owner;
                }
            }
            return LeaderId;
        }
        bool IsAllianceLeader(Player player)
        {
            LoadLists();
            var playerGuildOwnerId = PlayerExtensions.GetGuild(player).OwnerId;
            if(playerGuildOwnerId == 0)
            {
                return false;
            }
            for (var i = 0; i < _AllianceList.Count; i++)
            {
                if (_AllianceList.ElementAt(i).MemberGuilds.Contains(SocialAPI.GetGroupId(player.Id)) && _AllianceList.ElementAt(i).Owner == player.Id) return true;
            }
            return false;
        }
        bool IsGuildOwner(Player player)
        {
            LoadLists();
            if ((SocialAPI.Get<GuildScheme>().MemberSecurity(player.Id) == SocialAPI.GetSetting<SecuritySettings>().AdminLevel))
            {
                return true;
            }
            return false;
        }
        ulong GuildLeaderId(Player player)
        {
            foreach (Member guildmember in player.GetGuild().Get<Members>().GetAllMembers())
            {
                if (guildmember.SecurityLevel != SocialAPI.GetSetting<SecuritySettings>().AdminLevel) continue;
                return guildmember.PlayerId;
            }
            return 0;
        }
        bool AlliedWithEachOther (Player attacker, Player defender)
        {
            LoadLists();
            ulong AttackerGuildId = SocialAPI.GetGroupId(attacker.Id);
            ulong DefenderGuildId = SocialAPI.GetGroupId(defender.Id);
            for (var i =0; i< _AllianceList.Count; i++)
            {
                if (_AllianceList.ElementAt(i).MemberGuilds.Contains(AttackerGuildId) && _AllianceList.ElementAt(i).MemberGuilds.Contains(DefenderGuildId))
                {
                    return true;
                }
                return false;
            }
            return false;
        }
        bool IsAllied (Guild guild)
        {
            LoadLists();
            for (int kvp = 0; kvp < _AllianceList.Count; kvp++)
                if(_AllianceList.ElementAt(kvp).MemberGuilds.Contains(guild.BaseID))
                {
                    return true;
                }
            return false;
        }
        bool AllianceFull(Player player)
        {
            LoadLists();
            for(var kvp =0; kvp < _AllianceList.Count; kvp++)
            {
                if (_AllianceList.ElementAt(kvp).MemberGuilds.Contains(SocialAPI.GetGroupId(player.Id)))
                {
                    if (_AllianceList.ElementAt(kvp).MemberGuilds.Count() >= MaxMembersPerAlliance)
                    {
                        return true;
                    }
                    return false;
                }
                return false;
            }
            return false;
        }
        bool TryingToAllySelf(Player player, Player invitedPlayer)
        {
            LoadLists();
            if (PlayerExtensions.GetGuild(player)==PlayerExtensions.GetGuild(invitedPlayer))
            {
                PrintToChat(player, string.Format(GetMessage("SelfAlly", player.Id.ToString())));
                return true;
            }
            return false;
        }
        bool TryingToAllyWithAlly(Player player, Player invitedPlayer)
        {
            LoadLists();
            for(var kvp =0; kvp < _AllianceList.Count; kvp++)
            {
                    if (_AllianceList.ElementAt(kvp).MemberGuilds.Contains(SocialAPI.GetGroupId(player.Id)) && (_AllianceList.ElementAt(kvp).MemberGuilds.Contains(SocialAPI.GetGroupId(invitedPlayer.Id))))
                    {
                        PrintToChat(player, string.Format(GetMessage("TryingToAllyWithAllySender", player.Id.ToString())));
                        PrintToChat(invitedPlayer, string.Format(GetMessage("TryingToAllyWithAllyReceiver",invitedPlayer.Id.ToString()),player.Name));
                        return true;
                    }
            }
            
            return false;
        }
        int CountMergingMembers (Player player, Player invitedPlayer)
        {
            LoadLists();
            int result;
            Alliance firstAlliancetoCheck = new Alliance();
            Alliance secondAlliancetoCheck = new Alliance();
            for (var i =0;i< _AllianceList.Count;i++)
            {
                if(_AllianceList.ElementAt(i).MemberGuilds.Contains(SocialAPI.GetGroupId(player.Id)))
                {
                    firstAlliancetoCheck = _AllianceList.ElementAt(i);
                }
                if (_AllianceList.ElementAt(i).MemberGuilds.Contains(SocialAPI.GetGroupId(invitedPlayer.Id)))
                {
                    secondAlliancetoCheck = _AllianceList.ElementAt(i);
                }
            }
            result = firstAlliancetoCheck.MemberGuilds.Count() + secondAlliancetoCheck.MemberGuilds.Count();
            return result;
        }
        #endregion
    }
}