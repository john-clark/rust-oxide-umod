using System;
using System.Collections.Generic;
using System.Collections;

using CodeHatch.Engine.Networking;
using CodeHatch.Engine.Core.Networking;
using CodeHatch.Thrones.SocialSystem;
using CodeHatch.Common;
using CodeHatch.Permissions;

namespace Oxide.Plugins
{
    [Info("Guildname", "DumbleDora", "0.2")]
    public class Guildname : ReignOfKingsPlugin {

		// Guildname Reign of Kings mod by DumbleDora
		// This implementation only works if the play who you want to know what guild they belong to is online.
		#region Configuration Data
		bool configChanged;
		bool configCreated;
		bool adminOnly;
		#endregion

		void Loaded()
        {
            LoadConfigData();
        }

		[ChatCommand("guildname")]
        private void getGuild(Player player, string cmd, string[] args)
        {
            // if we only want admins to be able to use /guildname
            // and the user does not have admin permissions, let user know then do nothing.
            if (adminOnly && !player.HasPermission("admin")) {
                PrintToChat(player, "Only an admin may check guild names!");
                return;
            }

            // if the user has only typed /guildname
            // we want to tell them how to use the command
            if(args.Length < 1){
                PrintToChat(player, "Usage: /guildname username");
                return;
            }

            // if there is a name given after /guildname
            // print to the typer, the guildname for the given player
            if (args[0] != null){
                // find the target player
                Player toGetGuild = Server.GetPlayerByName( args[0] );
                //if we can't find the target tell the user and stop their request.
                if (toGetGuild == null){
                    PrintToChat(player, "Player not found - are they online?");
                    return;
                }
                // otherwise we found the target player, so get their guild
                string playerguild = PlayerExtensions.GetGuild(toGetGuild).DisplayName;
                // and finally tell the user the player's guild
                PrintToChat(player, playerguild);
            }
        }

		protected override void LoadDefaultConfig()
        {
            configCreated = true;
            Warning("New configuration file created.");
        }

		void Warning(string msg) => PrintWarning($"{Title} : {msg}");
		void LoadConfigData()
        {
            // Plugin settings
			adminOnly = Convert.ToBoolean(GetConfigValue("Settings", "AdminOnly", "False"));

            if (configChanged)
            {
                Warning("The configuration file was updated!");
                SaveConfig();
            }
        }

        object GetConfigValue(string category, string setting, object defaultValue)
        {
            var data = Config[category] as Dictionary<string, object>;
            object value;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[category] = data;
                configChanged = true;
            }
            if (!data.TryGetValue(setting, out value))
            {
                value = defaultValue;
                data[setting] = value;
                configChanged = true;
            }

            return value;
        }
    }
}