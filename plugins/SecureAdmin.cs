/*
 * TODO:
 * Add "banlist" command support
 * Add "banlistex" command support
 * Add "bans" command support
 * Add "kickall" command support
 * Add "mutechat" command support
 * Add "revoke" command support
 * Add "unmutechat" command support
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Secure Admin", "Wulf/lukespragg", "1.1.8", ResourceId = 1449)]
    [Description("Restricts the basic admin commands to players with permission")]
    public class SecureAdmin : CovalencePlugin
    {
        #region Initialization

        private const string permBan = "secureadmin.ban"; // global.ban, global.banlist, global.banlistex, global.listid, global.bans
        private const string permKick = "secureadmin.kick"; // global.kick, global.kickall
        private const string permSay = "secureadmin.say"; // global.mutechat, global.say, global.unmutechat
        private const string permUnban = "secureadmin.unban"; // global.banlist, global.bans, global.unban

        private bool broadcastBans;
        private bool broadcastKicks;
        private bool commandBan;
        private bool commandKick;
        private bool commandSay;
        private bool commandUnban;
        private bool protectAdmin;

        protected override void LoadDefaultConfig()
        {
            Config["Broadcast Bans (true/false)"] = broadcastBans = GetConfig("Broadcast Bans (true/false)", true);
            Config["Broadcast Kicks (true/false)"] = broadcastKicks = GetConfig("Broadcast Kicks (true/false)", true);
            Config["Enable Ban Command (true/false)"] = commandBan = GetConfig("Enable Ban Command (true/false)", true);
            Config["Enable Kick Command (true/false)"] = commandKick = GetConfig("Enable Kick Command (true/false)", true);
            Config["Enable Say Command (true/false)"] = commandSay = GetConfig("Enable Say Command (true/false)", true);
            Config["Enable Unban Command (true/false)"] = commandUnban = GetConfig("Enable Unban Command (true/false)", true);
            Config["Protect Admin (true/false)"] = protectAdmin = GetConfig("Protect Admin (true/false)", true);

            SaveConfig();
        }

        private void Init()
        {
            LoadDefaultConfig();

            permission.RegisterPermission(permBan, this);
            permission.RegisterPermission(permKick, this);
            permission.RegisterPermission(permSay, this);
            permission.RegisterPermission(permUnban, this);

            if (commandBan) AddCovalenceCommand("ban", "BanCommand");
            if (commandKick) AddCovalenceCommand("kick", "KickCommand");
            if (commandSay) AddCovalenceCommand("say", "SayCommand");
            if (commandUnban) AddCovalenceCommand("unban", "UnbanCommand");
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["PlayerAlreadyBanned"] = "{0} is already banned",
                ["PlayerBanned"] = "{0} has been banned for '{1}'",
                ["PlayerIsAdmin"] = "{0} is admin and cannot be banned or kicked",
                ["PlayerKicked"] = "{0} has been kicked for '{1}'",
                ["PlayerNotBanned"] = "{0} is not banned",
                ["PlayerNotFound"] = "No players were found with that name or ID",
                ["PlayerUnbanned"] = "{0} has been unbanned",
                ["PlayersFound"] = "Multiple players were found, please specify: {0}",
                ["ReasonUnknown"] = "Unknown",
                ["UsageBan"] = "Usage: {0} <name or id> <reason>",
                ["UsageKick"] = "Usage: {0} <name or id> <reason>",
                ["UsageSay"] = "Usage: {0} <message>",
                ["UsageUnban"] = "Usage: {0} <name or id>",
            }, this);

            // French
            /*lang.RegisterMessages(new Dictionary<string, string>
            {
                // TODO
            }, this, "fr");*/

            // German
            /*lang.RegisterMessages(new Dictionary<string, string>
            {
                // TODO
            }, this, "de");*/

            // Russian
            /*lang.RegisterMessages(new Dictionary<string, string>
            {
                // TODO
            }, this, "ru");*/

            // Spanish
            /*lang.RegisterMessages(new Dictionary<string, string>
            {
                // TODO
            }, this, "es");*/
        }

        #endregion

        #region Ban Command

        private void BanCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permBan))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            if (args.Length < 1)
            {
                player.Reply(Lang("UsageBan", player.Id, command));
                return;
            }

            ulong targetId;
            ulong.TryParse(args[0], out targetId);
            var foundPlayers = players.FindPlayers(args[0]).ToArray();
            if (foundPlayers.Length > 1)
            {
                player.Reply("PlayersFound", player.Id, string.Concat(", ", foundPlayers.Select(p => p.Name).ToArray()));
                return;
            }
            var target = foundPlayers.Length == 1 ? foundPlayers[0] : null;
            if (target != null) ulong.TryParse(target.Id, out targetId);

            if (!targetId.IsSteamId())
            {
                player.Reply(Lang("PlayerNotFound", player.Id, args[0].Sanitize()));
                return;
            }

            if (server.IsBanned(targetId.ToString()))
            {
                player.Reply(Lang("PlayerAlreadyBanned", player.Id, args[0].Sanitize()));
                return;
            }

            if (protectAdmin && target != null && target.IsAdmin)
            {
                player.Reply(Lang("PlayerIsAdmin", player.Id, target.Name.Sanitize()));
                return;
            }

            var reason = args.Length >= 2 ? string.Join(" ", args.Skip(1).ToArray()) : Lang("ReasonUnknown", targetId.ToString());
            if (target != null && target.IsConnected) target.Ban(reason);
            else server.Ban(targetId.ToString(), reason);

            var targetName = target != null ? $"{target.Name.Sanitize()} ({target.Id})" : args[0].Sanitize();
            if (broadcastBans) Broadcast("PlayerBanned", targetName, reason);
            else player.Reply(Lang("PlayerBanned", player.Id, targetName, reason));
        }

        #endregion

        #region Kick Command

        private void KickCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permKick))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            if (args.Length < 1)
            {
                player.Reply(Lang("UsageKick", player.Id, command));
                return;
            }

            var foundPlayers = players.FindPlayers(args[0]).ToArray();
            if (foundPlayers.Length > 1)
            {
                player.Reply("PlayersFound", player.Id, string.Concat(", ", foundPlayers.Select(p => p.Name).ToArray()));
                return;
            }
            var target = foundPlayers.Length == 1 ? foundPlayers[0] : null;
            if (target == null || !target.IsConnected)
            {
                player.Reply(Lang("PlayerNotFound", player.Id, args[0].Sanitize()));
                return;
            }

            if (protectAdmin && target.IsAdmin)
            {
                player.Reply(Lang("PlayerIsAdmin", player.Id, target.Name.Sanitize()));
                return;
            }

            var reason = args.Length >= 2 ? string.Join(" ", args.Skip(1).ToArray()) : Lang("ReasonUnknown", target.Id);
            target.Kick(reason);

            var targetName = $"{target.Name.Sanitize()} ({target.Id})";
            if (broadcastKicks) Broadcast("PlayerKicked", targetName, reason);
            else player.Reply(Lang("PlayerKicked", player.Id, targetName, reason));
        }

        #endregion

        #region Say Command

        private void SayCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permSay))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            if (args.Length < 1)
            {
                player.Reply(Lang("UsageSay", player.Id, command));
                return;
            }

            var message = string.Join(" ", args.ToArray());
            server.Broadcast(message);
        }

        #endregion

        #region Unban Command

        private void UnbanCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUnban))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            if (args.Length < 1)
            {
                player.Reply(Lang("UsageUnban", player.Id, command));
                return;
            }

            ulong targetId;
            ulong.TryParse(args[0], out targetId);
            var foundPlayers = players.FindPlayers(args[0]).ToArray();
            if (foundPlayers.Length > 1)
            {
                player.Reply("PlayersFound", player.Id, string.Concat(", ", foundPlayers.Select(p => p.Name).ToArray()));
                return;
            }
            var target = foundPlayers.Length == 1 ? foundPlayers[0] : null;
            if (target != null) ulong.TryParse(target.Id, out targetId);

            if (!targetId.IsSteamId())
            {
                player.Reply(Lang("PlayerNotFound", player.Id, args[0].Sanitize()));
                return;
            }

            var targetName = target != null ? $"{target.Name.Sanitize()} ({target.Id})" : args[0].Sanitize();

            if (!server.IsBanned(targetId.ToString()))
            {
                player.Reply(Lang("PlayerNotBanned", player.Id, targetName));
                return;
            }

            server.Unban(targetId.ToString());

            player.Reply(Lang("PlayerUnbanned", player.Id, targetName));
        }

        #endregion

        #region Helpers

        private T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void Broadcast(string key, params object[] args)
        {
            foreach (var player in players.Connected.Where(p => p.IsConnected)) player.Message(Lang(key, player.Id, args));
            Interface.Oxide.LogInfo(Lang(key, null, args));
        }

        #endregion
    }
}