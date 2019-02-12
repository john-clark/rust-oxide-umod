using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Startup Commands", "Wulf/lukespragg", "1.0.8")]
    [Description("Automatically runs configured commands on server startup")]
    public class StartupCommands : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Commands")]
            public List<string> Commands { get; set; } = new List<string>();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LogWarning("We got here");
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LogWarning("We got here");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            string configPath = $"{Interface.Oxide.ConfigDirectory}{Path.DirectorySeparatorChar}{Name}.json";
            LogWarning($"Could not load a valid configuration file, creating a new configuration file at {configPath}");
            config = new Configuration();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion Configuration

        #region Localization

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandAdded"] = "Command '{0}' was added to the startup command list",
                ["CommandAlias"] = "autocmd",
                ["CommandListed"] = "Command '{0}' is already in the startup command list",
                ["CommandNotListed"] = "Command '{0}' is not in the startup command list",
                ["CommandRemoved"] = "Command '{0}' was removed from the startup command list",
                ["CommandUsage"] = "Usage: {0} <add | remove | list> <command>",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["StartupCommands"] = "Startup commands: {0}"
            }, this);

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandAdded"] = "Commande « {0} » a été ajouté à la liste de commande de démarrage",
                ["CommandAlias"] = "commandeauto",
                ["CommandListed"] = "Commande « {0} » est déjà dans la liste de commande de démarrage",
                ["CommandNotListed"] = "Commande « {0} » n’est pas dans la liste de commande de démarrage",
                ["CommandRemoved"] = "Commande « {0} » a été supprimé de la liste de commande de démarrage",
                ["CommandUsage"] = "Utilisation : {0} <add | remove | list> <commande>",
                ["NotAllowed"] = "Vous n’êtes pas autorisé à utiliser la commande « {0} »",
                ["StartupCommands"] = "Commandes de démarrage: {0}"
            }, this, "fr");

            // German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandAdded"] = "Befehl '{0}' wurde auf der Startliste Befehl hinzugefügt",
                ["CommandAlias"] = "autobefehl",
                ["CommandListed"] = "Befehl '{0}' ist bereits in der Startliste Befehl",
                ["CommandNotListed"] = "Befehl '{0}' ist nicht in der Startliste Befehl",
                ["CommandRemoved"] = "Befehl '{0}' wurde aus der Startliste Befehl entfernt",
                ["CommandUsage"] = "Verbrauch: {0} <add | remove | list> <befehl>",
                ["NotAllowed"] = "Sie sind nicht berechtigt, verwenden Sie den Befehl '{0}'",
                ["StartupCommands"] = "Startbefehle: {0}"
            }, this, "de");

            // Russian
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandAdded"] = "Команда «{0}» был добавлен в список команд запуска",
                ["CommandAlias"] = "командуauto",
                ["CommandListed"] = "Команда «{0}» уже находится в списке команд запуска",
                ["CommandNotListed"] = "Команда «{0}» не включен в список команд запуска",
                ["CommandRemoved"] = "Команда «{0}» был удален из списка команд запуска",
                ["CommandUsage"] = "Использование: {0} <add | remove | list> <команда>",
                ["NotAllowed"] = "Нельзя использовать команду «{0}»",
                ["StartupCommands"] = "При запуске команды: {0}"
            }, this, "ru");

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandAdded"] = "Comando '{0}' se añadió a la lista de comandos de inicio",
                ["CommandAlias"] = "comandodeauto",
                ["CommandListed"] = "Comando '{0}' ya está en la lista de comandos de inicio",
                ["CommandNotListed"] = "Comando '{0}' no está en la lista de comandos de inicio",
                ["CommandRemoved"] = "Comando '{0}' se quitó de la lista de comandos de inicio",
                ["CommandUsage"] = "Uso: {0} <add | remove | list> <comando>",
                ["NotAllowed"] = "No se permite utilizar el comando '{0}'",
                ["StartupCommands"] = "Comandos de inicio de: {0}"
            }, this, "es");
        }

        #endregion Localization

        #region Initialization

        private const string permAdmin = "startupcommands.admin";
        private const string defaultCommand1 = "startcmd";
        private const string defaultCommand2 = "startupcmd";

        private void OnServerInitialized()
        {
            permission.RegisterPermission(permAdmin, this);

            AddLocalizedCommand("CommandAlias", "StartupCommand");
            AddCovalenceCommand(defaultCommand1, "StartupCommand");
            AddCovalenceCommand(defaultCommand2, "StartupCommand");

            foreach (string command in config.Commands)
            {
                server.Command(command);
            }
        }

        #endregion Initialization

        #region Commands

        private void StartupCommand(IPlayer player, string command, string[] args)
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

            string argCommand = string.Join(" ", args.Skip(1).Select(v => v).ToArray());
            switch (args[0].ToLower())
            {
                case "+":
                case "add":
                    if (config.Commands.Contains(argCommand))
                    {
                        Message(player, "CommandListed", argCommand);
                        break;
                    }

                    config.Commands.Add(argCommand);
                    SaveConfig();

                    Message(player, "CommandAdded", argCommand);
                    break;

                case "-":
                case "del":
                case "remove":
                    if (!config.Commands.Contains(argCommand))
                    {
                        Message(player, "CommandNotListed", argCommand);
                        break;
                    }

                    config.Commands.Remove(argCommand);
                    SaveConfig();

                    Message(player, "CommandRemoved", argCommand);
                    break;

                case "list":
                    Message(player, "StartupCommands", string.Join(", ", config.Commands.Cast<string>().ToArray()));
                    break;

                default:
                    Message(player, "CommandUsage", command);
                    break;
            }
        }

        #endregion Commands

        #region Helpers

        private string Lang(string key, string id = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, id), args);
        }

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

        private void Message(IPlayer player, string key, params object[] args)
        {
            player.Reply(Lang(key, player.Id, args));
        }

        #endregion Helpers
    }
}
