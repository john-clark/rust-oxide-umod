using Oxide.Core.Plugins;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("HungerGames", "open_mailbox", "0.1")]
    [Description("A Hunger Games simulator.")]
    public class HungerGames : RustPlugin
    {
        #region definitions
        private const string START_ZONE = "hungergames.start";
        private const string SAFE_ZONE = "hungergames.safe";

        private static readonly List<string> baseZoneOptions = new List<string> {
            "name",     START_ZONE,
            "nobuild",  "true",
            "nocup",    "true",
            "nodeploy", "true"
        };

        private bool registrationOpen = false;
        private List<BasePlayer> tributes;

        [PluginReference] Plugin Spawns;
        [PluginReference] Plugin ZoneManager;
        #endregion

        #region setup
        void Loaded()
        {
            permission.RegisterPermission("hungergames.admin", this);
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new configuration file for HungerGames.");
            Config.Clear();
            SaveConfig();
        }

        void OnServerInitialized()
        {
            if (ZoneManager == null)
            {
                PrintWarning("Plugin 'ZoneManager' was not found! Hunger Games plugin will not function.");
            }
        }
        #endregion

        #region commands
        [ChatCommand("hgjoin")]
        void CommandJoin(BasePlayer player, string command, string[] args)
        {
            if (tributes.Contains(player))
            {
                player.IPlayer.Reply("You are already registered for the Hunger Games!");
                return;
            }

            tributes.Add(player);
            player.IPlayer.Reply("You have registered for the Hunger Games!");
            Puts(player.IPlayer.Name + " has registered for the Hunger Games.");
        }

        [ChatCommand("hgprepare")]
        void CommandPrepare(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "hungergames.admin")) return;
            if (registrationOpen)
            {
                player.IPlayer.Reply("Hunger Games registration is already open.");
                return;
            }

            object zone = (string)ZoneManager?.Call("GetZoneName", START_ZONE);

            if (zone == null)
            {
                player.IPlayer.Reply("Start zone must be set before preparing Hunger Games.");
                return;
            }

            tributes = new List<BasePlayer>();
            registrationOpen = true;

            // TODO: Global chat message + GUI 
            Puts(player.IPlayer.Name + " has opened registration for Hunger Games.");
            player.IPlayer.Reply("Hunger Games registration opened.");
        }

        [ChatCommand("hgzone")]
        void CommandZone(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "hungergames.admin")) return;

            var zoneArgs    = new List<string>(baseZoneOptions);
            string zoneName = null;

            switch (args[0])
            {
                case "start":
                    zoneArgs.AddRange(new List<string> { "permission", "hungergames.tribute" });
                    zoneName = START_ZONE;
                    break;
                case "safe":
                    zoneArgs.AddRange(new List<string> { "pvegod", "true", "pvpgod", "true" });
                    zoneName = SAFE_ZONE;
                    break;
                default:
                    player.IPlayer.Reply("Invalid choice. Must be one of 'start' or 'safe'.");
                    return;
            }

            var result = (bool)ZoneManager?.Call("CreateOrUpdateZone", zoneName, zoneArgs.ToArray(), player.transform.position);

            var msg = "Setting Hunger Games " + args[0] + " zone to " + player.transform.position;
            Puts(msg);
            player.IPlayer.Reply(msg);
        }
        #endregion
    }
}
