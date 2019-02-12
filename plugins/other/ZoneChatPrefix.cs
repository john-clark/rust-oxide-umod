// Requires: ZoneManager
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Zone Chat Prefix", "BuzZ", "0.0.4")]
    [Description("Add Zone prefix to player chat")]

/*======================================================================================================================= 
*
*   
*   29th january 2019
*
*   0.0.1   20190129    creation
*   0.0.2   20190129    changed plugin mechanic
*   0.0.3   20190131    code
*   0.0.4   20180205    message sentence
*
*	THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*=======================================================================================================================*/

    public class ZoneChatPrefix : RustPlugin
    {
        [PluginReference]     
        Plugin ZoneManager;
        
        bool debug = false;

        public List<BasePlayer> blabla = new List<BasePlayer>();

#region ZONEMANAGER HOOKS

        string StringZonesPlayerIsIn(BasePlayer player)
        {
            string[] array = (string[]) ZoneManager.Call("GetPlayerZoneIDs", player);
            string message = string.Empty;
            if (array == null)
            {
                return "NOPE";
            }
            else
            {
                if (debug) Puts($"Count {array.Count()} ZONE(s)");
                int round = 1;
                int Round = 1;
                for (Round = 1; round <= array.Count() ; Round++)            
                {
                    string zone_name = GetThatZoneNamePlease(array[round-1]);
                    if (string.IsNullOrEmpty(message))
                    {
                        if (zone_name == "NOTFOUND")
                        {
                            message = $"[{array[round-1]}]";
                        }
                        else message = $"[{zone_name}]";
                    }
                    else
                    {
                        if (zone_name == "NOTFOUND")
                        {
                            message = $"{message} [{array[round-1]}]";
                        }
                        else message = $"{message} [{zone_name}] ";
                    }
                    if (debug) Puts($"{player.userID} - {player.displayName}");
                    if (debug) Puts($"round {round}");
                    round = round + 1;
                }
                return message;
            }
        }

        string GetThatZoneNamePlease (string zone_id)
        {
            string zone_name = (string)ZoneManager.Call("GetZoneName", zone_id);
            if (debug) Puts($"zone_name {zone_name}");
            if (string.IsNullOrEmpty(zone_name)) return "NOTFOUND";
            else return zone_name;
        }

#endregion
#region PLAYERCHAT
        object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            if (debug) Puts("OnPlayerChat");
            var player = arg.Connection.player as BasePlayer;
            if (player == null) return null;
            if (blabla.Contains(player)) return null;
            string chat = string.Empty;
            int round = 0;
            int Round = 1;
            for (Round = 1; round <= arg.Args.Length - 1; Round++)            
            {
                chat = chat + arg.Args[round];
                round = round + 1;
            }
            if (chat == null) return null;
            string zones = StringZonesPlayerIsIn(player);
            if (zones != "NOPE")
            {
                string playername = player.displayName + " ";
                if (player.net.connection.authLevel == 2) playername = $"<color=green>{playername}</color>";
                if (debug) Puts("OnPlayerChat - is in zone(s) !");
                Server.Broadcast(chat, zones +" " + playername , player.userID);
                blabla.Add(player);
                timer.Once(1f, () =>
                {
                    blabla.Remove(player);                    
                });
                return true;
            }
            else
            {
                if (debug) Puts("Not in Zone");
                return null;
            }
        }
#endregion
    }
}
