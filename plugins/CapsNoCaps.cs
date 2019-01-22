using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("CapsNoCaps", "PsychoTea", "1.3.4", ResourceId = 1324)]
    [Description("Turns all uppercase chat into lowercase")]
    public class CapsNoCaps : CovalencePlugin
    {
        [PluginReference]
        private Plugin BetterChat;

        private const string permIgnore = "capsnocaps.ignore";

        private void Init() => permission.RegisterPermission(permIgnore, this);

        private object OnUserChat(IPlayer player, string message)
        {
            if (BetterChat != null) return null;
            if (player.HasPermission(permIgnore)) return null;

            foreach (var target in players.Connected)
            {
#if RUST
                var rPlayer = player.Object as BasePlayer;
                var rTarget = target.Object as BasePlayer;

                var colour = "#5af";
                if (rPlayer.IsAdmin) colour = "#af5";
                if (rPlayer.IsDeveloper) colour = "#fa5";

                rTarget?.SendConsoleCommand("chat.add2", rPlayer.userID, message, rPlayer.displayName, colour);
#else
                target.Message(message, player.Name);
#endif
            }

            Log($"[CHAT] {player.Name} : {message}");
            return true;
        }

        private Dictionary<string, object> OnBetterChat(Dictionary<string, object> dict)
        {
            var player = dict["Player"] as IPlayer;
            if (permission.UserHasPermission(player.Id, permIgnore)) return null;

            dict["Text"] = (dict["Text"] as string).SentenceCase();
            return dict;
        }
    }
}