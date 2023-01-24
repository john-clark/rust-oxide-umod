using System;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Noob Queue Bypass", "Ryan", "1.0.0")]
    [Description("Allows new players to skip the queue")]
    class NoobQueueBypass : RustPlugin
    {
        private string perm = "noobqueuebypass.redeemed";

        #region Hooks

        void Init() => permission.RegisterPermission(perm, this);

        object CanBypassQueue(Network.Connection connection)
        {
            var ID = connection.userid.ToString();
            if (connection.userid.IsSteamId() && !permission.UserHasPermission(ID, perm))
            {
                Puts($"{connection.username} ({connection.userid}) is skipping the queue because he/she is a new player");
                LogToFile("connects", $"[{DateTime.Now}] {connection.username} ({connection.userid}) is skipping the queue because he/she is a new player", this, false);
                return true;
            }
            return null;
        }

        void OnPlayerInit(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, perm))
                permission.GrantUserPermission(player.UserIDString, perm, this);
        }

        #endregion
    }
}
