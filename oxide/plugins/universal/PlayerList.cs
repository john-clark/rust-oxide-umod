/*
 * TODO:
 * Add pagination to handle large amounts of players
 * Figure out limit for showing in a single message
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("PlayerList", "Wulf/lukespragg", "0.3.2", ResourceId = 2126)]
    [Description("Shows a list and count of all online, non-hidden players")]

    class PlayerList : CovalencePlugin
    {
        #region Initialization

        const string permAllow = "playerlist.allow";
        const string permHide = "playerlist.hide";

        bool adminSeparate;
        string adminColor;

        protected override void LoadDefaultConfig()
        {
            Config["Admin List Separate (true/false)"] = adminSeparate = GetConfig("Admin List Separate (true/false)", false);
            Config["Admin Color (Hex Format or Name)"] = adminColor = GetConfig("Admin Color (Hex Format or Name)", "e68c17");

            // Cleanup
            Config.Remove("SeparateAdmin");
            Config.Remove("AdminColor");

            SaveConfig();
        }

        void Init()
        {
            LoadDefaultConfig();
            LoadDefaultMessages();
            permission.RegisterPermission(permAllow, this);
            permission.RegisterPermission(permHide, this);
        }

        #endregion

        #region Localization

        void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AdminCount"] = "{0} admin online",
                ["AdminList"] = "Admin online ({0}): {1}",
                ["NobodyOnline"] = "No players are currently online",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["OnlyYou"] = "You are the only one online!",
                ["PlayerCount"] = "{0} player(s) online",
                ["PlayerList"] = "Players online ({0}): {1}"
            }, this);

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AdminCount"] = "{0} administrateurs en ligne",
                ["AdminList"] = "Administrateurs en ligne ({0}) : {1}",
                ["NobodyOnline"] = "Aucuns joueurs ne sont actuellement en ligne",
                ["NotAllowed"] = "Vous n’êtes pas autorisé à utiliser la commande « {0} »",
                ["OnlyYou"] = "Vous êtes la seule personne en ligne !",
                ["PlayerCount"] = "{0} joueur(s) en ligne",
                ["PlayerList"] = "Joueurs en ligne ({0}) : {1}"
            }, this, "fr");

            // German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AdminCount"] = "{0} Administratoren online",
                ["AdminList"] = "Administratoren online ({0}): {1}",
                ["NobodyOnline"] = "Keine Spieler sind gerade online",
                ["NotAllowed"] = "Sie sind nicht berechtigt, verwenden Sie den Befehl '{0}'",
                ["OnlyYou"] = "Du bist der einzige Online!",
                ["PlayerCount"] = "{0} Spieler online",
                ["PlayerList"] = "Spieler online ({0}): {1}"
            }, this, "de");

            // Russian
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AdminCount"] = "{0} администраторы онлайн",
                ["AdminList"] = "Администраторы онлайн ({0}): {1}",
                ["NobodyOnline"] = "Ни один из игроков онлайн",
                ["NotAllowed"] = "Нельзя использовать команду «{0}»",
                ["OnlyYou"] = "Вы являетесь единственным онлайн!",
                ["PlayerCount"] = "{0} игрока (ов) онлайн",
                ["PlayerList"] = "Игроков онлайн ({0}): {1}"
            }, this, "ru");

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AdminCount"] = "{0} administradores en línea",
                ["AdminList"] = "Los administradores en línea ({0}): {1}",
                ["NobodyOnline"] = "No hay jugadores están actualmente en línea",
                ["NotAllowed"] = "No se permite utilizar el comando '{0}'",
                ["OnlyYou"] = "Usted es el único en línea!",
                ["PlayerCount"] = "{0} jugadores en línea",
                ["PlayerList"] = "Jugadores en línea ({0}): {1}"
            }, this, "es");
        }

        #endregion

        #region Commands

        [Command("online")]
        void OnlineCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAllow))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            var adminCount = players.Connected.Count(p => p.IsAdmin && !p.HasPermission(permHide));
            var playerCount = players.Connected.Count(p => !p.IsAdmin && !p.HasPermission(permHide));

            player.Reply($"{Lang("AdminCount", player.Id, adminCount)}, {Lang("PlayerCount", player.Id, playerCount)}");
        }

        [Command("players", "who")]
        void PlayersCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAllow))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            var adminCount = players.Connected.Count(p => p.IsAdmin && !p.HasPermission(permHide));
            var playerCount = players.Connected.Count(p => !p.IsAdmin && !p.HasPermission(permHide));
            var totalCount = adminCount + playerCount;

            if (totalCount == 0) player.Reply(Lang("NobodyOnline", player.Id));
            else if (totalCount == 1 && player.Id != "server_console") player.Reply(Lang("OnlyYou", player.Id));
            else
            {
                var adminList = string.Join(", ", players.Connected.Where(p => p.IsAdmin && !p.HasPermission(permHide)).Select(p => covalence.FormatText($"[#{adminColor}]{p.Name.Sanitize()}[/#]")).ToArray());
                var playerList = string.Join(", ", players.Connected.Where(p => !p.IsAdmin && !p.HasPermission(permHide)).Select(p => p.Name.Sanitize()).ToArray());

                if (adminSeparate && !string.IsNullOrEmpty(adminList)) player.Reply(Lang("AdminList", player.Id, adminCount, adminList.TrimEnd(' ').TrimEnd(',')));
                else
                {
                    playerCount = adminCount + playerCount;
                    playerList = string.Concat(adminList, ", ", playerList);
                }
                if (!string.IsNullOrEmpty(playerList)) player.Reply(Lang("PlayerList", player.Id, playerCount, playerList.TrimEnd(' ').TrimEnd(',')));
            }
        }

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
    }
}