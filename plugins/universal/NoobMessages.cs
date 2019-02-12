using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("NoobMessages", "Kappasaurus", "1.1.0", ResourceId = 2443)]
    public class NoobMessages : CovalencePlugin
    {
        private const string permReturning = "noobmessages.returning";

        private void Init()
        {
            permission.RegisterPermission(permReturning, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["First Time Message"] = "{0} is a new player, treat them well!"
            }, this);
        }

        private void OnUserConnected(IPlayer player)
        {
            if (player.HasPermission(permReturning)) return;

            Broadcast("First Time Message", player.Name);
			permission.GrantUserPermission(player.Id, permReturning, null);
        }

        private void Broadcast(string key, params object[] args)
        {
            foreach (var player in players.Connected) player.Message(string.Format(lang.GetMessage(key, this, player.Id), args));
        }
    }
}