using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Inventory Viewer", "Mughisi", "3.0.3", ResourceId = 871)]
    [Description("Allows players with permission assigned to view anyone's inventory")]
    public class InventoryViewer : RustPlugin
    {
        private readonly string RequiredPermission = "inventoryviewer.allowed";

        private readonly Dictionary<BasePlayer, List<BasePlayer>> matches = new Dictionary<BasePlayer, List<BasePlayer>>();

        /// <summary>
        /// UnityEngine script to be attached to the player viewing someone's inventory.
        /// </summary>
        private class Inspector : MonoBehaviour
        {
            /// <summary>
            /// The player doing the inspecting.
            /// </summary>
            private BasePlayer player;

            /// <summary>
            /// The player being inspected.
            /// </summary>
            private BasePlayer target;

            /// <summary>
            /// The tick counter used by the Inspector.
            /// </summary>
            private int ticks;

            /// <summary>
            /// Instantiates the Inspector script.
            /// </summary>
            public void Instantiate(BasePlayer player, BasePlayer target)
            {
                this.player = player;
                this.target = target;

                BeginLooting();

                InvokeRepeating("UpdateLoot", 0f, 0.1f);
            }

            /// <summary>
            /// Updates the loot.
            /// </summary>
            private void UpdateLoot()
            {
                if (!target)
                {
                    return;
                }

                if (!target.inventory)
                {
                    return;
                }

                ticks++;

                if (!player.inventory.loot.IsLooting())
                {
                    BeginLooting();
                }

                player.inventory.loot.SendImmediate();

                player.SendNetworkUpdateImmediate();
            }

            /// <summary>
            /// Stops inspecting.
            /// </summary>
            private void StopInspecting(bool forced = false)
            {
                if (ticks < 5 && !forced)
                {
                    return;
                }

                CancelInvoke("UpdateLoot");

                EndLooting();
            }

            /// <summary>
            /// Starts the looting.
            /// </summary>
            private void BeginLooting()
            {
                player.inventory.loot.Clear();

                if (!target)
                {
                    return;
                }

                if (!target.inventory)
                {
                    return;
                }

                player.inventory.loot.AddContainer(target.inventory.containerMain);
                player.inventory.loot.AddContainer(target.inventory.containerWear);
                player.inventory.loot.AddContainer(target.inventory.containerBelt);
                player.inventory.loot.PositionChecks = false;
                player.inventory.loot.entitySource = target;
                player.inventory.loot.itemSource = null;
                player.inventory.loot.MarkDirty();
                player.inventory.loot.SendImmediate();
                player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", "player_corpse");
                player.SendNetworkUpdateImmediate();
            }

            /// <summary>
            /// Ends the looting.
            /// </summary>
            private void EndLooting()
            {
                player.inventory.loot.MarkDirty();

                if (player.inventory.loot.entitySource)
                {
                    player.inventory.loot.entitySource.SendMessage("PlayerStoppedLooting", player, SendMessageOptions.DontRequireReceiver);
                }

                foreach (ItemContainer container in player.inventory.loot.containers)
                {
                    if (container != null)
                    {
                        container.onDirty -= player.inventory.loot.MarkDirty;
                    }
                }

                player.inventory.loot.containers.Clear();
                player.inventory.loot.entitySource = null;
                player.inventory.loot.itemSource = null;
            }

            /// <summary>
            /// Destroys the script
            /// </summary>
            public void Remove(bool forced = false)
            {
                if (ticks < 5 && !forced)
                {
                    return;
                }

                StopInspecting(forced);

                Destroy(this);
            }
        }

        /// <summary>
        /// Oxide hook that is triggered when the plugin is loaded.
        /// </summary>
        private void Loaded()
        {
            permission.RegisterPermission(RequiredPermission, this);
        }

        /// <summary>
        /// Oxide hook that is triggered after the plugin is loaded to setup localized messages.
        /// </summary>
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "InvalidArguments", "Invalid argument(s) supplied! Use '/{0} <name>' or '/{0} list <number>'." },
                { "InvalidSelection", "Invalid number, use the number in front of the player's name. Use '/{0} list' to check the list of players again." },
                { "MultiplePlayersFound", "Multiple players found with that name, please select one of these players by using '/{0} list <number>':" },
                { "NoListAvailable", "You do not have a players list available, use '/{0} <name>' instead." },
                { "NoPlayersFound", "Couldn't find any players matching that name." },
                { "NotAllowed", "You are not allowed to use this command." },
                { "TooManyPlayersFound", "Too many players were found, the list of matches is only showing the first 5. Try to be more specific." }
            }, this);
        }

        /// <summary>
        /// Oxide hook that is triggered when the plugin is unloaded.
        /// </summary>
        private void Unload()
        {
            Inspector[] inspectors = UnityEngine.Object.FindObjectsOfType<Inspector>();

            foreach (Inspector inspector in inspectors)
                inspector.Remove();
        }

        /// <summary>
        /// Oxide hook that is triggered when a console command is executed.
        /// </summary>
        private void OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg.cmd.FullName == "inventory.endloot")
            {
                BasePlayer player = arg.Player();
                player.GetComponent<Inspector>()?.Remove();
            }
        }

        /// <summary>
        /// Oxide hook that is triggered when a player attempts to loot another player
        /// </summary>
        private object CanLootPlayer(BasePlayer target, BasePlayer looter)
        {
            if (looter.GetComponent<Inspector>() == null)
            {
                return null;
            }

            return true;
        }

        /// <summary>
        /// Handles the /inspect command
        /// </summary>
        [ChatCommand("inspect")]
        private void InspectCommand(BasePlayer player, string command, string[] args)
        {
            ViewInventoryCommand(player, command, args);
        }

        /// <summary>
        /// Handles the /viewinv command
        /// </summary>
        [ChatCommand("viewinv")]
        private void ViewInvCommand(BasePlayer player, string command, string[] args)
        {
            ViewInventoryCommand(player, command, args);
        }

        /// <summary>
        /// Handles the /viewinventory command
        /// </summary>
        [ChatCommand("viewinventory")]
        private void ViewInventoryCommand(BasePlayer player, string command, string[] args)
        {
            if (!CanUseCommand(player))
            {
                SendChatMessage(player, "NotAllowed");
                return;
            }

            if (args.Length < 1)
            {
                SendChatMessage(player, "InvalidArguments", command);
                return;
            }

            if (args[0] == "list")
            {
                if (args.Length == 1)
                {
                    if (!matches.ContainsKey(player) || matches[player] == null)
                    {
                        SendChatMessage(player, "NoListAvailable", command);
                        return;
                    }

                    ShowMatches(player);

                    return;
                }

                int num;
                if (int.TryParse(args[1], out num))
                {
                    if (!matches.ContainsKey(player) || matches[player] == null)
                    {
                        SendChatMessage(player, "NoListAvailable", command);
                        return;
                    }

                    if (num > matches[player].Count)
                    {
                        SendChatMessage(player, "InvalidSelection", command);
                        ShowMatches(player);
                        return;
                    }

                    StartInspecting(player, matches[player][num - 1]);
                    return;
                }

                SendChatMessage(player, "InvalidArguments", command);
            }
            else
            {
                string name = string.Join(" ", args);
                List<BasePlayer> players = FindPlayersByNameOrId(name);

                switch (players.Count)
                {
                    case 0:
                        SendChatMessage(player, "NoPlayersFound", command);
                        break;

                    case 1:
                        StartInspecting(player, players[0]);
                        break;

                    default:
                        SendChatMessage(player, "MultiplePlayersFound", command);

                        if (!matches.ContainsKey(player))
                        {
                            matches.Add(player, players);
                        }
                        else
                        {
                            matches[player] = players;
                        }

                        ShowMatches(player);

                        break;
                }
            }
        }

        /// <summary>
        /// Looks up all players (active and sleeping) by a given (partial) name or steam id.
        /// </summary>
        private List<BasePlayer> FindPlayersByNameOrId(string nameOrId)
        {
            List<BasePlayer> matches = new List<BasePlayer>();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (!string.IsNullOrEmpty(player.displayName))
                {
                    if (player.displayName.ToLower().Contains(nameOrId.ToLower()))
                    {
                        matches.Add(player);
                    }
                }

                if (player.UserIDString == nameOrId)
                {
                    matches.Add(player);
                }
            }

            foreach (BasePlayer player in BasePlayer.sleepingPlayerList)
            {
                if (!string.IsNullOrEmpty(player.displayName))
                {
                    if (player.displayName.ToLower().Contains(nameOrId.ToLower()))
                    {
                        matches.Add(player);
                    }
                }

                if (player.UserIDString == nameOrId)
                {
                    matches.Add(player);
                }
            }

            return matches.OrderBy(p => p.displayName).ToList();
        }

        /// <summary>
        /// Shows the cached matches for the player.
        /// </summary>
        private void ShowMatches(BasePlayer player)
        {
            for (int i = 0; i < matches[player].Count; i++)
            {
                SendChatMessage(player, $"{i + 1}. {matches[player][i].displayName}");

                if (i == 4 && i < matches[player].Count)
                {
                    SendChatMessage(player, "TooManyPlayersFound");
                    break;
                }
            }
        }

        /// <summary>
        /// Initializes the inspector for the given player and target.
        /// </summary>
        private void StartInspecting(BasePlayer player, BasePlayer target)
        {
            Inspector inspector = player.gameObject.GetComponent<Inspector>();
            inspector?.Remove();

            inspector = player.gameObject.AddComponent<Inspector>();
            inspector.Instantiate(player, target);
        }

        /// <summary>
        /// Checks if the specified BasePlayer has the required permission.
        /// </summary>
        private bool CanUseCommand(BasePlayer player)
        {
            return permission.UserHasPermission(player.UserIDString, RequiredPermission);
        }

        /// <summary>
        /// Sends a localized chat message using the key to the specified player
        /// </summary>
        private void SendChatMessage(BasePlayer player, string key, params string[] args)
        {
            if (args == null || args.Length == 0)
            {
                Player.Reply(player, lang.GetMessage(key, this, player.UserIDString));
            }
            else
            {
                Player.Reply(player, string.Format(lang.GetMessage(key, this, player.UserIDString), args));
            }
        }
    }
}