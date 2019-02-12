using System;
using System.Collections.Generic;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Automatic Authentication", "Waizujin", 1.0)]
    [Description("Runs the ownerid or moderatorid command on server startup to fix the issue of owners and moderators not being saved.")]
    public class AutomaticAuth : RustPlugin
    {
        class AutomaticAuthData
        {
            public Dictionary<string, int> AutomaticAuths = new Dictionary<string, int>();

            public AutomaticAuthData()
            {
            }
        }

        class AutomaticAuthInfo
        {
            public string SteamID;
            public int Type;

            public AutomaticAuthInfo()
            {
            }

            public AutomaticAuthInfo(string steamid, int type)
            {
                SteamID = steamid;
                Type = type;
            }
        }

        AutomaticAuthData automaticAuthData;

        void OnServerInitialized()
        {
            automaticAuthData = Interface.GetMod().DataFileSystem.ReadObject<AutomaticAuthData>("AutomaticAuths");

            foreach (KeyValuePair<string, int> automaticAuths in automaticAuthData.AutomaticAuths)
            {
                if (automaticAuths.Value == 2)
                {
                    ConsoleSystem.Run.Server.Normal("ownerid " + automaticAuths.Key + " '' 'Set automatically by AutomaticAuth'");
                }
                else if (automaticAuths.Value == 1)
                {
                    ConsoleSystem.Run.Server.Normal("moderatorid " + automaticAuths.Key + " '' 'Set automatically by AutomaticAuth'");
                }
            }
        }

        [ChatCommand("setowner")]
        private void SetOwnerCommand(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel != 2)
            {
                SendReply(player, "You do not have access to this command.");

                return;
            }

            var info = new AutomaticAuthInfo(args[0], 2);

            if (automaticAuthData.AutomaticAuths.ContainsKey(info.SteamID))
            {
                SendReply(player, "Player already set as owner or moderator!");
            }
            else
            {
                automaticAuthData.AutomaticAuths.Add(args[0], 2);
                ConsoleSystem.Run.Server.Normal("ownerid " + args[0] + " '' 'Set automatically by AutomaticAuth'");
                SendReply(player, "Player has been set as owner!");
            }

            Interface.GetMod().DataFileSystem.WriteObject("AutomaticAuths", automaticAuthData);
        }

        [ChatCommand("setmoderator")]
        private void SetModeratorCommand(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel != 2)
            {
                SendReply(player, "You do not have access to this command.");

                return;
            }

            var info = new AutomaticAuthInfo(args[0], 1);

            if (automaticAuthData.AutomaticAuths.ContainsKey(info.SteamID))
            {
                SendReply(player, "Player already set as moderator or ownerid!");
            }
            else
            {
                automaticAuthData.AutomaticAuths.Add(args[0], 1);
                ConsoleSystem.Run.Server.Normal("moderatorid " + args[0] + " '' 'Set automatically by AutomaticAuth'");
                SendReply(player, "Player has been set as moderator!");
            }

            Interface.GetMod().DataFileSystem.WriteObject("AutomaticAuths", automaticAuthData);
        }

        [ChatCommand("authlist")]
        private void AuthListCommand(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel != 2)
            {
                SendReply(player, "You do not have access to this command.");

                return;
            }
            
            var players = UnityEngine.Object.FindObjectsOfType<BasePlayer>();
            automaticAuthData = Interface.GetMod().DataFileSystem.ReadObject<AutomaticAuthData>("AutomaticAuths");

            int count = 0;
            foreach (KeyValuePair<string, int> automaticAuths in automaticAuthData.AutomaticAuths)
            {
                count++;
                string authedPlayer = "";

                foreach (BasePlayer basePlayer in players)
                {
                    if (basePlayer.userID == Convert.ToUInt64(automaticAuths.Key))
                    {
                        authedPlayer = basePlayer.displayName;
                    }
                    else
                    {
                        authedPlayer = automaticAuths.Key;
                    }
                }

                if (automaticAuths.Value == 2)
                {
                    SendReply(player, count + ". Player " + authedPlayer + " (" + automaticAuths.Key + ") is set as an owner.");
                }
                else if (automaticAuths.Value == 1)
                {
                    SendReply(player, count + ". Player " + authedPlayer + " (" + automaticAuths.Key + ") is set as a moderator.");
                }
            }
        }
    }
}
