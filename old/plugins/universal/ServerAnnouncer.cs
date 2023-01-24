using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Server Announcer", "austinv900", "1.0.7")]
    [Description("Allows you to send messages as the server with custom prefix")]
    internal class ServerAnnouncer : CovalencePlugin
    {
        #region  Initialization

        private const string Permission = "ServerAnnouncer.Allowed";

        private void OnServerInitialized()
        {
            permission.RegisterPermission(Permission, this);
        }

        #endregion

        #region Localizations

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["MessageFormat"] = "[ServerConsole]: {0}",
                ["NoAccess"] = "You are not allowed to use this command",
                ["NoMessage"] = "You did not specify a message"
            }, this);
        }

        #endregion

        #region Commands

        [Command("say", "server.say")]
        private void Say(IPlayer player, string command, string[] args)
        {
            if (!IsAdmin(player)) { player.Reply(Lang("NoAccess", player)); return; }

            if (args.Length == 0) { player.Reply(Lang("NoMessage", player)); return; }

            foreach (var user in players.Connected)
            {
                var msg = Lang("MessageFormat", user, string.Join(" ", args));
                user.Message(msg);
            }
        }

        #endregion

        #region Helpers

        private bool IsAdmin(IPlayer player) => permission.UserHasGroup(player.Id, "admin") || permission.UserHasPermission(player.Id, Permission) || player.IsAdmin;

        private string Lang(string key, IPlayer player, params object[] args) => string.Format(lang.GetMessage(key, this, player.Id), args);

        #endregion
    }
}
