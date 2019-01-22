using System;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Ping", "Wulf/lukespragg", "1.8.0", ResourceId = 1921)]
    [Description("Ping chekcing on command and automatic kicking of players with high pings")]

    class Ping : CovalencePlugin
    {
        #region Initialization

        const string permBypass = "ping.bypass";
        const string permCheck = "ping.check";

        bool highPingKick;
        bool kickNotices;
        bool repeatChecking;
        bool warnBeforeKick;

        int highPingLimit;
        int kickGracePeriod;

        protected override void LoadDefaultConfig()
        {
            // Options
            Config["High Ping Kick (true/false)"] = highPingKick = GetConfig("High Ping Kick (true/false)", true);
            Config["Kick Notice Messages (true/false)"] = kickNotices = GetConfig("Kick Notice Messages (true/false)", true);
            Config["Repeat Checking (true/false)"] = repeatChecking = GetConfig("Repeat Checking (true/false)", true);
            Config["Warn Before Kicking (true/false)"] = warnBeforeKick = GetConfig("Warn Before Kicking (true/false)", true);

            // Settings
            Config["High Ping Limit (Milliseconds)"] = highPingLimit = GetConfig("High Ping Limit (Milliseconds)", 200);
            Config["Kick Grace Period (Seconds)"] = kickGracePeriod = GetConfig("Kick Grace Period (Seconds)", 30);

            SaveConfig();
        }

        void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["KickWarning"] = "You will be kicked in {0} seconds if your ping is not lowered",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["PingTooHigh"] = "Ping is too high: {0}ms",
                ["PlayerKicked"] = "{0} kicked for high ping ({1}ms)",
                ["PlayerNotFound"] = "Player '{0}' was not found",
                ["PlayerPing"] = "{0} has a ping of {1}ms",
                ["YourPing"] = "You have a ping of {0}ms"
            }, this);

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["KickWarning"] = "Vous sera lancé dans {0} secondes si votre ping n’est pas abaissé",
                ["NotAllowed"] = "Vous n’êtes pas autorisé à utiliser la commande « {0} »",
                ["PingTooHigh"] = "Ping est trop élevée : {0} ms",
                ["PlayerKicked"] = "{0} expulsé pour ping élevé ({1} ms)",
                ["PlayerNotFound"] = "Player « {0} » n’a pas été trouvée",
                ["YourPing"] = "Vous avez un ping de {0} ms"
            }, this, "fr");

            // German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["KickWarning"] = "Sie werden in {0} Sekunden gekickt wenn Ihr Ping nicht gesenkt wird",
                ["NotAllowed"] = "Sie sind nicht berechtigt, verwenden Sie den Befehl '{0}'",
                ["PingTooHigh"] = "Ping ist zu hoch: {0} ms",
                ["PlayerKicked"] = "{0} gekickt für hohen Ping ({1} ms)",
                ["PlayerNotFound"] = "Player '{0}' wurde nicht gefunden",
                ["YourPing"] = "Sie haben einen Ping von {0} ms"
            }, this, "de");

            // Russian
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["KickWarning"] = "Вам будет ногами в {0} секунд если пинг не опустил",
                ["NotAllowed"] = "Нельзя использовать команду «{0}»",
                ["PingTooHigh"] = "Пинг слишком высока: {0} ms",
                ["PlayerKicked"] = "{0} ногами высокий пинг ({1} ms)",
                ["PlayerNotFound"] = "Игрок «{0}» не найден",
                ["YourPing"] = "У вас пинг {0} ms"
            }, this, "ru");

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["KickWarning"] = "Usted va ser pateado en {0} segundos si el ping no baja",
                ["NotAllowed"] = "No se permite utilizar el comando '{0}'",
                ["PingTooHigh"] = "Ping es demasiado alto: {0} ms",
                ["PlayerKicked"] = "{0} expulsado por ping alto ({1} ms)",
                ["PlayerNotFound"] = "Jugador '{0}' no se encontró",
                ["YourPing"] = "Tienes un ping de {0} ms"
            }, this, "es");
        }

        void Init()
        {
            LoadDefaultConfig();
            LoadDefaultMessages();
            permission.RegisterPermission(permBypass, this);
            permission.RegisterPermission(permCheck, this);
        }

        #endregion

        #region Game Hooks

        void OnServerInitialized()
        {
            foreach (var player in players.Connected) timer.Once(5f, () => PingCheck(player));
        }

        void OnServerSave()
        {
            if (!repeatChecking) return;
            foreach (var player in players.Connected) timer.Once(5f, () => PingCheck(player));
        }

        void OnUserConnected(IPlayer player) => timer.Once(10f, () => PingCheck(player));

        #endregion

        #region Ping Checking

        void PingCheck(IPlayer player, bool warned = false)
        {
            if (!player.IsConnected || player.HasPermission(permBypass)) return;

            var ping = player.Ping;
            if (ping < highPingLimit || !highPingKick) return;

            if (warnBeforeKick && !warned)
            {
                player.Message(Lang("KickWarning", player.Id, kickGracePeriod));
                timer.Once(kickGracePeriod, () => PingCheck(player, true));
            }
            else
                PingKick(player, ping.ToString());
        }

        void PingKick(IPlayer player, string ping)
        {
            player.Kick(Lang("PingTooHigh", player.Id, ping));

            if (!kickNotices) return;
            Puts(Lang("PlayerKicked", null, player.Name, ping));
            server.Broadcast(Lang("PlayerKicked", null, player.Name, ping));
        }

        #endregion

        #region Commands

        [Command("ping", "pong")]
        void PingCommand(IPlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                player.Reply(Lang("YourPing", player.Id, player.Ping));
                return;
            }

            if (player.HasPermission(permCheck))
            {
                var target = players.FindPlayer(args[0]);
                if (target == null || !target.IsConnected)
                {
                    player.Reply(Lang("PlayerNotFound", player.Id, args[0]));
                    return;
                }

                player.Reply(Lang("PlayerPing", player.Id, target.Name, target.Ping));
            }
            else
                player.Reply(Lang("NotAllowed", player.Id, command));
        }

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
    }
}