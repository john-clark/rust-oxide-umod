using System;

using CodeHatch.Engine.Networking;

namespace Oxide.Plugins
{
    [Info("Personal Messenger", "WuBa", "1.0.0")]
    public class PersonalMessenger : ReignOfKingsPlugin
    {
        [ChatCommand("pm")]
        void sendPM(Player player, string cmd, string[] args)
        {
            if (args.Length < 2)
            {
                PrintToChat(player, "/pm Username Your message!");
                return;
            }

            Player Recipient = Server.GetPlayerByName(args[0]);

            if (Recipient == null)
            {
                PrintToChat(player, "Player " + args[0] + " does not exist, or isn't online!");
                return;
            }

            string[] PM = new string[args.Length];
            Array.Copy(args, 1, PM, 0, args.Length-1);

            PrintToChat(Recipient, "[21A5E3]" + player.DisplayName + " [E35F21](/PM):[21A5E3] " + string.Join(" ", PM) + "[FFFFFF]");
            PrintToChat(player, "[21A5E3]" + player.DisplayName + "[E35F21] → " + Recipient.DisplayName + ": [21A5E3]" + string.Join(" ", PM) + "[FFFFFF]");
        }
    }
}