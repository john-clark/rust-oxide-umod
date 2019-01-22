/*
 * TODO: Add support for Covalence console command intercepting when possible
 * TODO: Add optional, standalone logging of blocked command attempts
 * TODO: Remove game-specific hooks when possible
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Command Block", "Wulf/lukespragg", "0.3.1")]
    [Description("Blocks configured commands from being executed on the server")]
    public class CommandBlock : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Log blocked command attempts (true/false)")]
            public bool LogAttempts;

            [JsonProperty(PropertyName = "Blocked commands (full or short commands)")]
            public List<string> BlockedCommands;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    LogAttempts = true,
                    BlockedCommands = new List<string> { "spectate", "kill", "global.status" }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.BlockedCommands == null) LoadDefaultConfig();
            }
            catch
            {
                LogWarning($"Could not read oxide/config/{Name}.json, creating new config file");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Localization

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandAdded"] = "Command '{0}' was added to the command block list",
                ["CommandAlias"] = "blockcmd",
                ["CommandAttempted"] = "{0} ({1}) attempted to use blocked command: {2}",
                ["CommandBlocked"] = "Sorry, the '{0}' command is blocked",
                ["CommandListed"] = "Command '{0}' is already in the command block list",
                ["CommandNotListed"] = "Command '{0}' is not in the command block list",
                ["CommandRemoved"] = "Command '{0}' was removed from the command block list",
                ["CommandUsage"] = "Usage: {0} <add | remove | list> <command>",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command"
            }, this);

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandAdded"] = "Commande « {0} » a été ajouté à la liste de blocage de commande",
                ["CommandAlias"] = "blocdecmd",
                ["CommandAttempted"] = "{0} ({1}) a tenté d’utiliser la commande bloquée : {2}",
                ["CommandBlocked"] = "Désolé, la commande « {0} » est bloquée",
                ["CommandListed"] = "Commande « {0} » est déjà dans la liste de blocage de commande",
                ["CommandNotListed"] = "Commande « {0} » n’est pas dans la liste de blocage de commande",
                ["CommandRemoved"] = "Commande « {0} » a été supprimé de la liste de blocage de commande",
                ["CommandUsage"] = "Utilisation : {0} <add | remove | list> <commande>",
                ["NotAllowed"] = "Vous n’êtes pas autorisé à utiliser la commande « {0} »"
            }, this, "fr");

            // German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandAdded"] = "Befehl '{0}' wurde auf der Befehl Blockierliste",
                ["CommandAlias"] = "befehlblock",
                ["CommandAttempted"] = "{0} ({1}) versucht, blockierten Befehl zu verwenden: {2}",
                ["CommandBlocked"] = "Leider ist der Befehl '{0}' blockiert",
                ["CommandListed"] = "Befehl '{0}' ist bereits in der Befehl Blockierliste",
                ["CommandNotListed"] = "Befehl '{0}' ist nicht in der Befehl Blockierliste",
                ["CommandRemoved"] = "Befehl '{0}' wurde aus der Befehl Blockierliste",
                ["CommandUsage"] = "Verbrauch: {0} <add | remove | list> <befehl>",
                ["NotAllowed"] = "Sie sind nicht berechtigt, verwenden Sie den Befehl '{0}'"
            }, this, "de");

            // Russian
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandAdded"] = "Команда «{0}» был добавлен в cписок команд блок",
                ["CommandAlias"] = "блокcmd",
                ["CommandAttempted"] = "{0} ({1}) попытались использовать заблокированные команды: {2}",
                ["CommandBlocked"] = "К сожалению команда «{0}» заблокирован",
                ["CommandListed"] = "Команда «{0}» уже находится в Список команд блок",
                ["CommandNotListed"] = "Команда «{0}» не включен в cписок команд блок",
                ["CommandRemoved"] = "Команда «{0}» был удален из Список команд блок",
                ["CommandUsage"] = "Использование: {0} <add | remove | list> <команда>",
                ["NotAllowed"] = "Нельзя использовать команду «{0}»"
            }, this, "ru");

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandAdded"] = "Comando '{0}' se añadió a la lista del bloque de comando",
                ["CommandAlias"] = "bloquecmd",
                ["CommandAttempted"] = "{0} ({1}) intentó utilizar el comando bloqueado: {2}",
                ["CommandBlocked"] = "Lo sentimos, el comando '{0}' está bloqueado",
                ["CommandListed"] = "Comando '{0}' ya está en la lista del bloque de comando",
                ["CommandNotListed"] = "Comando '{0}' no está en la lista del bloque de comando",
                ["CommandRemoved"] = "Comando '{0}' se quitó de la lista del bloque de comando",
                ["CommandUsage"] = "Uso: {0} <add | remove | list> <comando>",
                ["NotAllowed"] = "No se permite utilizar el comando '{0}'"
            }, this, "es");
        }

        #endregion

        #region Initialization

        const string permAdmin = "commandblock.admin";
        const string permBypass = "commandblock.bypass";

        private void Init()
        {
            permission.RegisterPermission(permAdmin, this);
            permission.RegisterPermission(permBypass, this);

            AddCommandAliases("CommandAlias", "BlockCommand");
            AddCovalenceCommand("blockcommand", "BlockCommand");
        }

        #endregion

        #region Commands

        private void BlockCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAdmin))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args.Length < 1 || args.Length < 2 && args[0].ToLower() != "list")
            {
                Message(player, "CommandUsage", command);
                return;
            }

            var argCommand = string.Join(" ", args.Skip(1).Select(v => v).ToArray());
            switch (args[0].ToLower())
            {
                case "+":
                case "add":
                    if (config.BlockedCommands.Contains(argCommand))
                    {
                        Message(player, "CommandListed", argCommand);
                        break;
                    }

                    config.BlockedCommands.Add(argCommand);
                    SaveConfig();

                    Message(player, "CommandAdded", argCommand);
                    break;

                case "-":
                case "del":
                case "remove":
                    if (!config.BlockedCommands.Contains(argCommand))
                    {
                        Message(player, "CommandNotListed", argCommand);
                        break;
                    }

                    config.BlockedCommands.Remove(argCommand);
                    SaveConfig();

                    Message(player, "CommandRemoved", argCommand);
                    break;

                case "list":
                    Message(player, "Blocked commands: " + string.Join(", ", config.BlockedCommands.ToArray())); // TODO: Localize
                    break;

                default:
                    Message(player, "CommandUsage", command);
                    break;
            }
        }

        #endregion

        #region Command Blocking

        private object OnUserCommand(IPlayer player, string command, string[] args)
        {
            command = command.TrimStart('/').Substring(command.IndexOf(".", StringComparison.Ordinal) + 1);
            if (player.HasPermission(permBypass) || !config.BlockedCommands.Contains(command)) return null;

            Message(player, "CommandBlocked", command);
            if (config.LogAttempts) LogWarning(Lang("CommandAttempted", null, player.Name, player.Id, command));
            return true;
        }

#if HURTWORLD
        private object OnServerCommand(string command)
        {
            return config.BlockedCommands.Contains(command.Substring(0, command.IndexOf(" "))) ? (object)true : null;
        }
#endif

#if RUST
        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            var connection = arg.Connection;
            if (connection == null || string.IsNullOrEmpty(arg.cmd?.FullName)) return null;
            if (permission.UserHasPermission(connection.userid.ToString(), permBypass)) return null;
            if (!config.BlockedCommands.Contains(arg.cmd.Name) && !config.BlockedCommands.Contains(arg.cmd.FullName)) return null;

            arg.ReplyWith(Lang("CommandBlocked", connection.userid.ToString(), arg.cmd.FullName));
            if (config.LogAttempts) LogWarning(Lang("CommandAttempted", null, connection.username, connection.userid, arg.cmd.FullName));
            return true;
        }
#endif

#if RUSTLEGACY
        private object OnRunCommand(ConsoleSystem.Arg arg)
        {
            var netUser = arg.argUser;
            var command = $"{arg.Class}.{arg.Function}";
            if (netUser == null || permission.UserHasPermission(netUser.userID.ToString(), permBypass)) return null;
            if (!config.BlockedCommands.Contains(arg.Function) && !config.BlockedCommands.Contains(command)) return null;

            arg.ReplyWith(Lang("CommandBlocked", netUser.userID.ToString(), command));
            if (config.LogAttempts) LogWarning(Lang("CommandAttempted", null, netUser.displayName, netUser.userID, command));
            return true;
        }
#endif

        #endregion

        #region Helpers

        private void AddCommandAliases(string key, string command)
        {
            foreach (var language in lang.GetLanguages(this))
            {
                var messages = lang.GetMessages(language, this);
                foreach (var message in messages.Where(m => m.Key.StartsWith(key))) AddCovalenceCommand(message.Value, command);
            }
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void Message(IPlayer player, string key, params object[] args) => player.Message(Lang(key, player.Id, args));

        #endregion
    }
}
