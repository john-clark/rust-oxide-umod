using System;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using System.Linq;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Admin Chatroom", "austinv900", "1.0.2")]
    [Description("Allows admins to send messages back and forth between other admins")]
    class AdminChatroom : CovalencePlugin
    {
        HashSet<IPlayer> AdminChat = new HashSet<IPlayer>();

        #region Oxide Hooks
        void Init()
        {
            LoadDefaultConfig();
            LoadMessages();
            foreach (var pl in players.All)
            {
                if (pl.IsConnected && IsAdmin(pl)) AdminChat.Add(pl);
            }

            permission.RegisterPermission(Permission, this);
        }
        void OnUserConnected(IPlayer player)
        {
            if (IsAdmin(player) && !AdminChat.Contains(player)) AdminChat.Add(player);
        }
        void OnUserDisconnected(IPlayer player)
        {
            if (AdminChat.Contains(player)) AdminChat.Remove(player);
        }
        #endregion

        #region Configuration
        string Permission = "adminchatroom.";
        protected override void LoadDefaultConfig()
        {
            SetConfig("General", "Allowed To Join Permission 'adminchatroom.'", $"allowed");
            SaveConfig();
            Permission += GetConfig($"allowed", "General", "Allowed To Join Permission 'adminchatroom.'");
        }
        #endregion

        #region Localization
        void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["ChatFormat"] = "[AdminChat] {0}: {1}",
                ["NotInvited"] = "You have not have access to this chatroom",
                ["PlayerAdded"] = "{0} has been added to the admin chat",
                ["PlayerRemoved"] = "{0} has been kicked from the chatroom",
                ["SendCommandInfo"] = "You have been invited to the admin chatroom. please use /{0} msg - To chat",
                ["InviteFailed"] = "Failed to invite {0} to the chat. Is he already present?"
            }, this);
        }
        #endregion

        #region Plugin WorkBench
        [Command("a")]
        void cmdAdminChat(IPlayer player, string command, string[] args)
        {
            if (!AdminChat.Contains(player)) { player.Reply(Lang("NotInvited", player.Id)); return; }

            if (args.Length == 0) return;

            if (IsAdmin(player) && args.Length == 2 && args[0].ToLower().Contains("invite") || args[0].ToLower().Contains("kick"))
            {
                switch (args[0].ToLower())
                {
                    case "invite":
                        var targ = FindConnectedPlayer(args[1]);
                        if (targ == null) { player.Reply(Lang("InviteFailed", player.Id, args[1])); return; }
                        if (AdminChat.Contains(targ)) { player.Reply(Lang("InviteFailed", player.Id, targ.Name)); return; }
                        if (!AdminChat.Contains(targ)) { AdminChat.Add(targ); SendRoomMessage("ChatRoom", Lang("PlayerAdded", player.Id, targ.Name)); targ.Reply(Lang("SendCommandInfo", targ.Id, command)); return; }
                        break;

                    case "kick":
                        var target = FindConnectedPlayer(args[1]);
                        if (target != null && AdminChat.ToList().Contains(target)) { AdminChat.Remove(target); SendRoomMessage("ChatRoom", Lang("PlayerRemoved", player.Id, target.Name)); return; }
                        break;
                }
            }

            else
            {
                SendRoomMessage(player.Name, string.Join(" ", args));
                return;
            }
        }

        void SendRoomMessage(string name, string message)
        {
            foreach (var pl in AdminChat)
            {
                pl.Reply(Lang("ChatFormat", pl.Id, name, message));
            }
        }
        #endregion

        #region Helpers
        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        bool IsAdmin(IPlayer player) => permission.UserHasGroup(player.Id, "admin") || permission.UserHasPermission(player.Id, Permission) || player.IsAdmin;
        string ListToString<T>(List<T> list, int first = 0, string seperator = ", ") => string.Join(seperator, (from val in list select val.ToString()).Skip(first).ToArray());
        void SetConfig(params object[] args) { List<string> stringArgs = (from arg in args select arg.ToString()).ToList(); stringArgs.RemoveAt(args.Length - 1); if (Config.Get(stringArgs.ToArray()) == null) Config.Set(args); }
        T GetConfig<T>(T defaultVal, params object[] args) { List<string> stringArgs = (from arg in args select arg.ToString()).ToList(); if (Config.Get(stringArgs.ToArray()) == null) { PrintError($"The plugin failed to read something from the config: {ListToString(stringArgs, 0, "/")}{Environment.NewLine}Please reload the plugin and see if this message is still showing. If so, please post this into the support thread of this plugin."); return defaultVal; } return (T)System.Convert.ChangeType(Config.Get(stringArgs.ToArray()), typeof(T)); }
        IPlayer FindConnectedPlayer(string var) { var pl = players.All.Where(p => p.Id == var || p.Name.ToLower().Contains(var.ToLower()) && p.IsConnected).ToArray(); if (pl.Count() > 1 || pl.Count() == 0) return null; return pl[0]; }
        #endregion
    }
}
