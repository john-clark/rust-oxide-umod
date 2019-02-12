using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;

#if RUST
using UnityEngine;
#endif

namespace Oxide.Plugins
{
    [Info("Freeze", "Wulf/lukespragg", "2.2.2")]
    [Description("Stops one or more players from moving and keeps them from moving")]
    class Freeze : CovalencePlugin
    {
        #region Localization

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandFreeze"] = "freeze",
                ["CommandFreezeAll"] = "freezeall",
                ["CommandUnfreeze"] = "unfreeze",
                ["CommandUnfreezeAll"] = "unfreezeall",
                ["CommandUsage"] = "Usage: {0} <name or id>",
                ["NoPlayersFound"] = "No players found with '{0}'",
                ["NoPlayersFreeze"] = "No players to freeze",
                ["NoPlayersUnfreeze"] = "No players to unfreeze",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["PlayerFrozen"] = "{0} has been frozen",
                ["PlayerIsProtected"] = "{0} is protected and cannot be frozen",
                ["PlayerIsFrozen"] = "{0} is already frozen",
                ["PlayerNotFrozen"] = "{0} is not frozen",
                ["PlayerUnfrozen"] = "{0} has been unfrozen",
                ["PlayersFound"] = "Multiple players were found, please specify: {0}",
                ["PlayersFrozen"] = "All players have been frozen",
                ["PlayersUnfrozen"] = "All players have been unfrozen",
                ["YouAreFrozen"] = "You are frozen",
                ["YouWereUnfrozen"] = "You were unfrozen"
            }, this);
        }

        #endregion Localization

        #region Initialization

        private readonly Dictionary<string, Timer> timers = new Dictionary<string, Timer>();

        private const string permFrozen = "freeze.frozen";
        private const string permProtect = "freeze.protect";
        private const string permUse = "freeze.use";

        private void Init()
        {
            permission.RegisterPermission(permFrozen, this);
            permission.RegisterPermission(permProtect, this);
            permission.RegisterPermission(permUse, this);

            AddLocalizedCommand("CommandFreeze", "FreezeCommand");
            AddLocalizedCommand("CommandFreezeAll", "FreezeAllCommand");
            AddLocalizedCommand("CommandUnfreeze", "UnfreezeCommand");
            AddLocalizedCommand("CommandUnfreezeAll", "UnfreezeAllCommand");
        }

        #endregion Initialization

        #region Freeze Command

        private void FreezeCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUse))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args.Length < 1)
            {
                Message(player, "CommandUsage", command);
                return;
            }

            IPlayer[] foundPlayers = players.FindPlayers(args[0]).Where(p => p.IsConnected).ToArray();
            if (foundPlayers.Length > 1)
            {
                Message(player, "PlayersFound", string.Join(", ", foundPlayers.Select(p => p.Name).ToArray()));
                return;
            }

            IPlayer target = foundPlayers.Length == 1 ? foundPlayers[0] : null;
            if (target == null || !target.IsConnected)
            {
                Message(player, "NoPlayersFound", args[0]);
                return;
            }

            if (target.HasPermission(permProtect))
            {
                Message(player, "PlayerIsProtected", target.Name.Sanitize());
            }
            else if (target.HasPermission(permFrozen))
            {
                Message(player, "PlayerIsFrozen", target.Name.Sanitize());
            }
            else
            {
                FreezePlayer(target);
                Message(target, "YouAreFrozen");
                Message(player, "PlayerFrozen", target.Name.Sanitize());
            }
        }

        #endregion Freeze Command

        #region Freeze All Command

        private void FreezeAllCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUse))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            foreach (IPlayer target in players.Connected)
            {
                if (!target.HasPermission(permProtect) && !target.HasPermission(permFrozen))
                {
                    FreezePlayer(target);
                    if (target.IsConnected)
                    {
                        Message(target, "YouAreFrozen");
                    }
                }
            }

            Message(player, players.Connected.Any() ? "PlayersFrozen" : "NoPlayersFreeze");
        }

        #endregion Freeze All Command

        #region Unfreeze Command

        private void UnfreezeCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUse))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args.Length < 1)
            {
                Message(player, "CommandUsage", command);
                return;
            }

            IPlayer[] foundPlayers = players.FindPlayers(args[0]).Where(p => p.IsConnected).ToArray();
            if (foundPlayers.Length > 1)
            {
                Message(player, "PlayersFound", string.Join(", ", foundPlayers.Select(p => p.Name).ToArray()));
                return;
            }

            IPlayer target = foundPlayers.Length == 1 ? foundPlayers[0] : null;
            if (target == null || !target.IsConnected)
            {
                Message(player, "NoPlayersFound", args[0]);
                return;
            }

            if (target.HasPermission(permFrozen))
            {
                UnfreezePlayer(target);
                Message(target, "YouWereUnfrozen", target.Id);
                Message(player, "PlayerUnfrozen", target.Name.Sanitize());
            }
            else
            {
                Message(player, "PlayerNotFrozen", target.Name.Sanitize());
            }
        }

        #endregion Unfreeze Command

        #region Unfreeze All Command

        private void UnfreezeAllCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUse))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            foreach (IPlayer target in players.Connected)
            {
                if (target.HasPermission(permFrozen))
                {
                    UnfreezePlayer(target);
                    if (target.IsConnected)
                    {
                        Message(target, "YouWereUnfrozen");
                    }
                }
            }

            player.Reply(Lang(players.Connected.Any() ? "PlayersUnfrozen" : "NoPlayersUnfreeze"));
        }

        #endregion Unfreeze All Command

        #region Freeze Handling

        private void FreezePlayer(IPlayer player)
        {
            player.GrantPermission(permFrozen);

            GenericPosition pos = player.Position();
            timers[player.Id] = timer.Every(0.01f, () =>
            {
                if (!player.IsConnected)
                {
                    timers[player.Id].Destroy();
                    return;
                }

                if (!player.HasPermission(permFrozen))
                {
                    UnfreezePlayer(player);
                }
                else
                {
                    player.Teleport(pos.X, pos.Y, pos.Z);
                }
            });
        }

        private void UnfreezePlayer(IPlayer player)
        {
            player.RevokePermission(permFrozen);

            if (timers.ContainsKey(player.Id))
            {
                timers[player.Id].Destroy();
            }
        }

        private void OnUserConnected(IPlayer player)
        {
            if (player.HasPermission(permFrozen))
            {
                FreezePlayer(player);
                Log(Lang("PlayerFrozen", null, player.Name.Sanitize()));
            }
        }

        private void OnServerInitialized()
        {
            foreach (IPlayer player in players.Connected)
            {
                if (player.HasPermission(permFrozen))
                {
                    FreezePlayer(player);
                    Log(Lang("PlayerFrozen", null, player.Name.Sanitize()));
                }
            }
        }

#if RUST
        /*private void OnPlayerMetabolize(PlayerMetabolism metabolism, BaseCombatEntity entity)
        {
            BasePlayer basePlayer = entity as BasePlayer;
            if (basePlayer != null && basePlayer.IPlayer.HasPermission(permFrozen))
            {
                metabolism.temperature.SetValue(-50f);
                basePlayer.metabolism.SendChangesToClient();

                Vector3 breathPosition = basePlayer.eyes.position + Quaternion.Euler(basePlayer.serverInput.current.aimAngles) * new Vector3(0, 0, 0.2f);
                Effect.server.Run("assets/bundled/prefabs/fx/player/frosty_breath.prefab", breathPosition);
            }
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            BasePlayer basePlayer = entity as BasePlayer;
            if (basePlayer != null && basePlayer.IPlayer.HasPermission(permFrozen) && info?.damageTypes.GetMajorityDamageType() == global::Rust.DamageType.Cold)
            {
                info.damageTypes = new global::Rust.DamageTypeList();
                info.HitMaterial = 0;
                info.PointStart = Vector3.zero;
            }
        }*/
#endif

        #endregion Freeze Handling

        #region Helpers

        private void AddLocalizedCommand(string key, string command)
        {
            foreach (string language in lang.GetLanguages(this))
            {
                Dictionary<string, string> messages = lang.GetMessages(language, this);
                foreach (KeyValuePair<string, string> message in messages.Where(m => m.Key.Equals(key)))
                {
                    if (!string.IsNullOrEmpty(message.Value))
                    {
                        AddCovalenceCommand(message.Value, command);
                    }
                }
            }
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void Message(IPlayer player, string key, params object[] args) => player.Message(Lang(key, player.Id, args));

        #endregion Helpers
    }
}
