using CodeHatch.Engine.Networking;
using CodeHatch.Common;
using CodeHatch.Thrones.SocialSystem;
using CodeHatch.Engine.Modules.SocialSystem;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("GuildManager", "D-Kay", "0.1.1")]

    class GuildManager : ReignOfKingsPlugin
    {
        #region Save and Load Data

        private void Loaded()
        {
            LoadDefaultMessages();
            permission.RegisterPermission("GuildManager.List", this);
            permission.RegisterPermission("GuildManager.Modify", this);
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "InvalidArgsGuild", "[FF0000]GuildMaster[FFFFFF] : To find out about a guild, type [00FF00]/getguild [FF00FF]<guildname>." },
                { "InvalidArgsPlayer", "[FF0000]GuildManager[FFFFFF] : To find out about a player's guild, type [00FF00]/getguildplayer [FF00FF]<playername>." },
                { "GuildlistTitle", "[FF0000]GuildManager[FFFFFF] : [00FF00]{0}[FFFFFF] has {1} members:" },
                { "GuildlistIsOffline", "[008888] (Offline)" },
                { "GuildlistIsOnline", "[FF0000] (Online)" },
                { "GuildlistPlayerStatus", "[00FF00]{0}{1}" },
                { "NoPlayer", "[FF0000]GuildManager[FFFFFF] : Unable to find the guild of that player." },
                { "NoGuild", "[FF0000]GuildManager[FFFFFF] : Unable to find that guild." },
                { "NoPermission", "[FF0000]GuildManager[FFFFFF] : You do not have permission to use this command." },
                
                { "AddedToGuild", "[FF0000]GuildManager[FFFFFF] : {0} was moved to guild {1}" },
                { "ChangedGuildName", "[FF0000]GuildManager[FFFFFF] : Guild {0} was changed to {1}" },
                { "RemovedFromGuild", "[FF0000]GuildManager[FFFFFF] : {0} was exiled." },
                { "ChangedGuildMembership", "[FF0000]GuildManager[FFFFFF] : {0} was set to {1}." },

                { "HelpTextTitle", "[0000FF]GuildManager Commands[FFFFFF]" },
                { "HelpTextGetGuild", "[00FF00]/getguild (guildname)[FFFFFF] - Shows the guildname, players and their online status of a guild." },
                { "HelpTextGetGuildPlayer", "[00FF00]/getguildplayer (playername)[FFFFFF] - Shows the guildname, players and their online status of a player." },
                { "HelpTextListGuilds", "[00FF00]/listguilds[FFFFFF] - Shows a list of all the guilds." },
                { "HelpTextAddToGuild", "[00FF00]/addtoguild (playername) (guildname)[FFFFFF] - Adds a player to a guild." },
                { "HelpTextChangeGuildName", "[00FF00]/changeguildname (guildname) (newname)[FFFFFF] - Changes the name of a guild." },
                { "HelpTextRemoveFromGuild", "[00FF00]/removefromguild (playername)[FFFFFF] - Removes the player from the guild they're in." },
                { "HelpTextChangeGuildMembership", "[00FF00]/changeguildmembership (playername) (family/member)[FFFFFF] - Changes the membership status of a player to eigher family or member." }
            }, this);
        }

        #endregion

        #region Commands

        [ChatCommand("ListGuilds")]
        private void ListGuilds(Player player, string cmd)
        {
            if (!player.HasPermission("GuildManager.List")) { GetMessage("NoPermission", player); return; }

            List<Guild> guilds = GetAllGuilds();
            foreach (Guild guild in guilds) if (guild.Members().GetAllMembers().Count > 0) PrintToChat(player, guild.DisplayName);
        }

        [ChatCommand("GetGuild")]
        private void GetGuild(Player player, string cmd, string[] args)
        {
            if (!player.HasPermission("GuildManager.List")) { GetMessage("NoPermission", player); return; }

            if (args.Length < 1) return;
            ShowListByGuild(player, args.JoinToString(" "));
        }

        [ChatCommand("GetGuildPlayer")]
        private void GetGuildPlayer(Player player, string cmd, string[] args)
        {
            if (!player.HasPermission("GuildManager.List")) { GetMessage("NoPermission", player); return; }

            if (args.Length < 1) return;
            ShowListByPlayer(player, args.JoinToString(" "));
        }

        [ChatCommand("AddToGuild")]
        private void AddPlayerToGuild(Player player, string cmd, string[] args)
        {
            if (!player.HasPermission("GuildManager.Modify")) { GetMessage("NoPermission", player); return; }
            if (args.Length < 2) return;

            GuildScheme guildScheme = SocialAPI.Get<GuildScheme>();
            Member member = GetMemberByPlayer(args[0]);
            Guild guild = GetGuildByGuild(args[1]);

            guildScheme.ChangeGuildMembership(member.PlayerId, guild.BaseID);

            PrintToChat(player, string.Format(GetMessage("AddedToGuild", player), member.Name, guild.DisplayName));
        }

        [ChatCommand("ChangeGuildName")]
        private void ChangeGuildName(Player player, string cmd, string[] args)
        {
            if (!player.HasPermission("GuildManager.Modify")) { GetMessage("NoPermission", player); return; }
            if (args.Length < 2) return;

            GuildScheme guildScheme = SocialAPI.Get<GuildScheme>();
            Guild guild = GetGuildByGuild(args[0]);
            string oldName = guild.DisplayName;

            guildScheme.SetGuildName(guild.BaseID, args[1]);
            
            PrintToChat(player, string.Format(GetMessage("ChangedGuildName", player), oldName, guild.DisplayName));
        }

        [ChatCommand("RemoveFromGuild")]
        private void RemoveFromGuild(Player player, string cmd, string[] args)
        {
            if (!player.HasPermission("GuildManager.Modify")) { GetMessage("NoPermission", player); return; }
            if (args.Length < 1) return;

            GuildScheme guildScheme = SocialAPI.Get<GuildScheme>();
            Member member = GetMemberByPlayer(args[0]);

            guildScheme.RemoveGuildMembership(member.PlayerId);

            PrintToChat(player, string.Format(GetMessage("RemovedFromGuild", player), member.Name));
        }

        [ChatCommand("ChangeGuildMembership")]
        private void ChangeGuildMembership(Player player, string cmd, string[] args)
        {
            if (!player.HasPermission("GuildManager.Modify")) { GetMessage("NoPermission", player); return; }
            if (args.Length < 1) return;

            GuildScheme guildScheme = SocialAPI.Get<GuildScheme>();
            Member member = GetMemberByPlayer(args[0]);

            byte security = 0;

            if (args[1].ToLower() == "family") security = 2;
            else if (args[1].ToLower() == "member") security = 1;
            else return;

            guildScheme.SetMembersSecurity(member.PlayerId, security);
            
            PrintToChat(player, string.Format(GetMessage("ChangedGuildMembership", player), member.Name, args[1]));
        }

        #endregion

        #region Functions

        private void ShowListByGuild(Player player, string guildName)
        {
            Guild guild = GetGuildByGuild(guildName);
            if (guild == null) { PrintToChat(player, GetMessage("NoGuild", player)); return; }
            ListGuildInfo(guild, player);
        }

        private void ShowListByPlayer(Player player, string playerName)
        {
            Guild guild = GetGuildByPlayer(playerName);
            if (guild == null) { PrintToChat(player, GetMessage("NoPlayer", player)); return; }
            ListGuildInfo(guild, player);
        }

        private void ListGuildInfo(Guild guild, Player player)
        {
            string message = "";

            ReadOnlyCollection<Member> members = guild.Members().GetAllMembers();

            message += string.Format(GetMessage("GuildlistTitle", player), guild.DisplayName, members.Count);
            message += "\n";

            foreach (Member member in members)
            {
                message += "\n";
                message += string.Format(GetMessage("GuildlistPlayerStatus", player), member.Name, GetMessage(member.OnlineStatus ? "GuildlistIsOnline" : "GuildlistIsOffline", player));
            }

            player.ShowPopup("Guild info", message);
        }

        private List<Guild> GetAllGuilds()
        {
            GuildScheme guildScheme = SocialAPI.Get<GuildScheme>();

            List<RootSocialGroup> groups = guildScheme.Storage.TryGetAllGroups();
            List<Guild> guilds = new List<Guild>();
            foreach (RootSocialGroup group in groups)
            {
                Guild guild = guildScheme.TryGetGuild(group.BaseID);
                if (guild != null) guilds.Add(guild);
            }
            return guilds;
        }

        private Member GetMemberByPlayer(string player)
        {
            GuildScheme guildScheme = SocialAPI.Get<GuildScheme>();

            List<Guild> guilds = GetAllGuilds();
            foreach (Guild guild in guilds)
            {
                ReadOnlyCollection<Member> members = guild.Members().GetAllMembers();
                foreach (Member member in members) if (member.Name.ToLower().Contains(player.ToLower())) return member;
            }
            return null;
        }

        private Guild GetGuildByPlayer(string player)
        {
            GuildScheme guildScheme = SocialAPI.Get<GuildScheme>();

            List<Guild> guilds = GetAllGuilds();
            foreach (Guild guild in guilds)
            {
                ReadOnlyCollection<Member> members = guild.Members().GetAllMembers();
                foreach (Member member in members) if (member.Name.ToLower().Contains(player.ToLower())) return guild;
            }
            return null;
        }

        private Guild GetGuildByGuild(string guildName)
        {
            GuildScheme guildScheme = SocialAPI.Get<GuildScheme>();

            List<Guild> guilds = GetAllGuilds();
            foreach (Guild guild in guilds) if (guild.DisplayName.ToLower().Contains(guildName.ToLower())) return guild;
            return null;
        }

        #endregion

        #region Hooks

        private void SendHelpText(Player player)
        {
            if (player.HasPermission("guildmanager.list"))
            {
                PrintToChat(player, GetMessage("HelpTextTitle", player));
                PrintToChat(player, GetMessage("HelpTextListGuilds", player));
                PrintToChat(player, GetMessage("HelpTextGetGuild", player));
                PrintToChat(player, GetMessage("HelpTextGetGuildPlayer", player));
            }
            if (player.HasPermission("guildmanager.modify"))
            {
                PrintToChat(player, GetMessage("HelpTextAddToGuild", player));
                PrintToChat(player, GetMessage("HelpTextChangeGuildName", player));
                PrintToChat(player, GetMessage("HelpTextRemoveFromGuild", player));
                PrintToChat(player, GetMessage("HelpTextChangeGuildMembership", player));
            }
        }

        #endregion

        #region Utility

        private string ConvertArrayToString(string[] args)
        {
            string name = args[0];
            if (args.Length > 1)
            {
                for (int i = 1; i < args.Length; i++)
                {
                    name = name + " " + args[i];
                }
            }
            return name;
        }

        //private T GetConfig<T>(string name, T defaultValue)
        //{
        //    if (Config[name] == null) return defaultValue;
        //    return (T)Convert.ChangeType(Config[name], typeof(T));
        //}

        string GetMessage(string key, Player player = null) => lang.GetMessage(key, this, player == null ? null : player.Id.ToString());

        #endregion
    }
}