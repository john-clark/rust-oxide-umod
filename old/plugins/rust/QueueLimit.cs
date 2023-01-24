using System.Collections.Generic;
using ConVar;

namespace Oxide.Plugins
{
    [Info("Queue Limit", "Ryan", "1.0.1")]
    [Description("Limits the number of players allowed in the connection queue")]
    class QueueLimit : RustPlugin
    {
        private int Limit;
        private string Perm = "queuelimit.bypass";

        private void LoadDefaultConfig()
        {
            PrintWarning("Generating default configuration file");
            Config["Queue Limit"] = Limit = 100;
        }

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["KickMessage"] = "Server is full"
            }, this);
        }

        private void Init()
        {
            Limit = (int) Config["Queue Limit"];
            permission.RegisterPermission(Perm, this);
        }

        private string CanClientLogin(Network.Connection connection)
        {
            if (permission.UserHasPermission(connection.userid.ToString(), Perm))
                return null;

            if (Admin.ServerInfo().Queued >= Limit)
                return lang.GetMessage("KickMessage", this, connection.userid.ToString());

            return null;
        }
    }
}
