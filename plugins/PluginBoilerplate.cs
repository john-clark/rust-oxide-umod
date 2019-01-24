/**
 * Note: This is a boilerplate meant for educational and jump-start
 * purposes. For best performance, it is suggested to remove all  
 * hooks, use statements, and other features you don't require.
 *
 * The following links may be helpful If you're new to Oxide plugin development:
 * http://docs.oxidemod.org/
 * http://oxidemod.org/threads/c-plugin-development-guide-and-advice-dump.23738/
 * http://oxidemod.org/threads/plugin-submission-guidelines-and-requirements.23233/
 *
 * Features:
 * * Several commonly used hooks ready to go.
 * * Basic config management.
 * * Simple default Lang setup.
 * * Custom, multi-level, LogHelper for easy debugging.
 *   - Set your desired debug level in the plugin's config file.
 * * Example Chat and Console command (/boilerplate) to change the debug level.
 */

using System;
using Oxide.Core;
using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    /**
     * The `Info` attribute should contain the plugin's name,
     * author's name, plugin's version, and a resource id
     * which can be obtained when submit to oxide.org.
     *
     * Plugin Name: Should be description, unique, and avoid
     * redundant words like "plugin", "mod", "admin", etc.
     *
     * Plugin Version: This should follow SemVer (semver.org).
     * The developmental builds ought to be 0.X.X and your
     * initial release will usually increment to 1.0.0.
     * 
     * Resource ID: The Resource ID can be found after publishing
     * your plugin to oxide.org. Your plugin's URL will end in
     * a number. For example: "plugin-starter-template.0000"
     * 
     * The `Description` attribute should contain a brief
     * description or summary about what the plugin is
     * designed to do. Don't get too lengthy on it.
     */
    [Info("PluginBoilerplate", "AuthorName", "0.1.0", ResourceId = 0000)]
    [Description("A boilerplate with a little bit of everything to get you started.")]
    public class PluginBoilerplate : RustPlugin
    {
        #region Config

        /// <summary>
        /// Instance used for accessing config values.
        /// </summary>
        private PluginConfig _config;

        /// <summary>
        /// Indicates if changes to the config need saved.
        /// </summary>
        private bool _configIsDirty;

        /// <summary>
        /// The outer wrapper of the config structure.
        /// </summary>
        private class PluginConfig
        {
            public GeneralSettings Settings { get; set; }
        }

        /// <summary>
        /// The configuration's general setting property definitions.
        /// </summary>
        private class GeneralSettings
        {
            [JsonProperty("Debug Level (Trace, Info, Debug, Warning, Error, Fatal, Disabled)")]
            public string DebugLevel { get; set; }

            [JsonProperty("Unused Example Property")]
            public string UnusedExampleProperty { get; set; }
        }

        /// <summary>
        /// Builds the default configuration object.
        /// </summary>
        /// <returns>The built <see cref="PluginConfig"/> configuration object.</returns>
        private PluginConfig DefaultConfig()
        {
            return new PluginConfig
            {
                Settings = new GeneralSettings
                {
                    DebugLevel = Enum.GetName(typeof(LogHelper.LogLevel), LogHelper.LogLevel.Info),
                    UnusedExampleProperty = Version.ToString()
                }
            };
        }

        /// <summary>
        /// Called when the plugin's config should be initialized.
        /// This will only be called when the config file does
        /// not already exist in the oxide/config directory.
        /// </summary>
        protected override void LoadDefaultConfig()
        {
            _config = DefaultConfig();

            InitLogger();
            _logger.Info("A new configuration file has been created.");
        }

        /// <summary>
        /// Loads the config file from disk and performs integrity checks.
        /// </summary>
        protected override void LoadConfig()
        {
            base.LoadConfig();
            if (_config == null) _config = Config.ReadObject<PluginConfig>();
        }

        /// <summary>
        /// Persist the changes to the config file.
        /// </summary>
        private void UpdateConfig(string message = null, bool force = false)
        {
            if (!_configIsDirty && !force) return;

            SaveConfig();
            _logger.Info(message ?? "The configuration file has been updated.");
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
            _configIsDirty = false;
        }

        #endregion

        #region Lang

        /// <summary>
        /// Registers the default Lang messages.
        /// </summary>
        protected override void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                ["CmdBoilerplate_Help"] = "The <color=orange>DebugLevel</color> is currently set to \"<color=orange>{0}</color>\".\n\n Type \"<color=orange>/boilerplate <DebugLevel></color>\" to change the DebugLevel.\n <color=orange>Valid Options:</color> {1}",
                ["CmdBoilerplate_SetTo"] = "The <color=orange>{0}</color> setting is now set to \"<color=orange>{1}</color>\".",
                ["CmdBoilerplate_AlreadySet"] = "The <color=orange>{0}</color> setting is already set to \"<color=orange>{1}</color>\".",
                ["Log_ChangedSetting"] = "{0} has changed the {1} setting to \"{2}\".",
                ["Error_InvalidOption"] = "\"<color=orange>{0}</color>\" is not a valid option."
            };

            lang.RegisterMessages(messages, this);
        }

        /// <summary>
        /// Builds a string from the registered messages using the Lang API.
        /// </summary>
        /// <param name="key">The key associated with the registered message.</param>
        /// <param name="playerId">The player's ID.</param>
        /// <param name="args">Arguments used to replace anchors in the registered message.</param>
        /// <returns>Returns the built string in the preferred language.</returns>
        private string Lang(string key, string playerId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, playerId), args);
        }

        #endregion

        #region Plugin Body

        /// <summary>
        /// An instance of <see cref="LogHelper"/>.
        /// </summary>
        private LogHelper _logger;

        #region Server Hooks

        /// <summary>
        /// This is called when a plugin is being initialized.
        /// Other plugins may or may not be present just yet
        /// dependant on the order in which they're loaded.
        /// </summary>
        private void Init()
        {
            if (_logger == null) InitLogger();
            _logger.Info("Plugin initialized!");
        }

        /// <summary>
        /// This is called when a plugin has finished loading.
        /// Other plugins may or may not be present just yet
        /// dependant on the order in which they're loaded.
        /// </summary>
        private void Loaded()
        {
            _logger.Info("Plugin loaded!");
        }

        /// <summary>
        /// This is called when a plugin is being unloaded.
        /// </summary>
        private void Unload()
        {
            _logger.Info("Plugin unloaded!");
        }

        /// <summary>
        /// This is called when the player is being initialized,
        /// they have already connected but have not woken up
        /// yet. Great place to take notice of new players.
        /// </summary>
        /// <param name="player">The player that is being initialized.</param>
        private void OnPlayerInit(BasePlayer player)
        {
            _logger.Info($"Player {player.displayName} initialized!");
        }

        /// <summary>
        /// This is called when a player wakes up. This is a great
        /// place to attach your custom UI elements that should
        /// appear quickly or when the player enters the game.
        /// </summary>
        /// <param name="player">The player that has woken up.</param>
        private void OnPlayerSleepEnded(BasePlayer player)
        {
            _logger.Info($"Player {player.displayName} has woken!");
        }

        /// <summary>
        /// This is called when a player is about to go to sleep. Returning a
        /// non-null value overrides the default behavior. This is a great
        /// place to destroy custom UIs to hide while they're sleeping.
        /// </summary>
        /// <param name="player">The player that has fallen asleep.</param>
        private void OnPlayerSleep(BasePlayer player)
        {
            _logger.Info($"Player {player.displayName} has fallen asleep!");
        }

        /// <summary>
        /// This is called after a player has been disconnected.
        /// </summary>
        /// <param name="player">The player that has disconnected.</param>
        private void OnPlayerDisconnected(BasePlayer player)
        {
            _logger.Info($"Player {player.displayName} has disconnected!");
        }

        #endregion

        #region Commands

        /// <summary>
        /// An example chat command for viewing and editing the DebugLevel.
        /// </summary>
        /// <param name="player">A reference to the player who initiated the command.</param>
        /// <param name="command">A string containing the command being called.</param>
        /// <param name="args">A string array containing the command arguments.</param>
        [ChatCommand("boilerplate")]
        private void ChatCommandBoilerplate(BasePlayer player, string command, string[] args)
        {
            CommandBoilerplate(player, args);
        }

        /// <summary>
        /// This method illustrates how to register a console command.
        /// For the purpose of this example/boilerplate, it is just
        /// acting as an alias for ChatCommandBoilerplate above.
        /// </summary>
        /// <param name="arg">An object containing the player, command args, and more.</param>
        [ConsoleCommand("boilerplate")]
        private void ConsoleCommandBoilerplate(ConsoleSystem.Arg arg)
        {
            CommandBoilerplate(arg.Player(), arg.Args ?? new string[]{});
        }

        /// <summary>
        /// A rather dirty implementation of changing a config value from a command.
        /// </summary>
        /// <remarks>
        /// A console command would typically use PrintToConsole() instead of PrintToChat().
        /// </remarks>
        /// <param name="player">A reference to the player who initiated the command.</param>
        /// <param name="args">A string array containing the command arguments.</param>
        private void CommandBoilerplate(BasePlayer player, string[] args)
        {
            string userId = player.UserIDString;

            if (args.Length != 1) {
                string options = string.Join(", ", Enum.GetNames(typeof(LogHelper.LogLevel)));
                PrintToChat(player, Lang("CmdBoilerplate_Help", userId, _config.Settings.DebugLevel, options));
                return;
            }

            if (args[0].TitleCase() == _config.Settings.DebugLevel) {
                PrintToChat(player, Lang("CmdBoilerplate_AlreadySet", userId, "DebugLevel", args[0].TitleCase()));
                return;
            }

            try {
                string debugLevel = _config.Settings.DebugLevel = _logger.GetLogLevel(args[0]).ToString();
                PrintToChat(player, Lang("CmdBoilerplate_SetTo", userId, "DebugLevel", debugLevel));
                UpdateConfig(Lang("Log_ChangedSetting", userId, player.displayName, "DebugLevel", debugLevel), true);
            } catch (Exception exception) {
                PrintToChat(player, Lang("Error_InvalidOption", userId, args[0].TitleCase()));
            }
        }

        #endregion

        /// <summary>
        /// Initialize the LogHelper.
        /// </summary>
        private void InitLogger()
        {
            _logger = new LogHelper(Title);

            try {
                _logger = new LogHelper(Title, _logger.GetLogLevel(_config.Settings.DebugLevel));
            } catch (Exception exception) {
                _logger.Error("Invalid LogLevel configuration value.");
            }
        }

        #endregion

        #region Log Helper

        /// <summary>
        /// Simple multi-level logger class to easily enable/disable console logging.
        /// </summary>
        public class LogHelper
        {
            /// <summary>
            /// The name of the current plugin.
            /// </summary>
            private readonly string _pluginName;

            /// <summary>
            /// The severity level of log messages to show.
            /// </summary>
            public LogLevel Level { get; set; }

            /// <summary>
            /// The possible severity levels.
            /// </summary>
            public enum LogLevel { Trace, Info, Debug, Warning, Error, Fatal, Disabled }

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="pluginName">An instance of the plugin.</param>
            /// <param name="level">The severity level of log messages to show.</param>
            public LogHelper(string pluginName, LogLevel level = LogLevel.Warning)
            {
                _pluginName = pluginName;
                Level = level;
            }

            /// <summary>
            /// Determines if the Enum key exists and returns the matching LogLevel.
            /// </summary>
            /// <param name="key">The key to find in the Enum.</param>
            /// <returns>The <see cref="LogLevel"/> matching the provided key.</returns>
            public LogLevel GetLogLevel(string key)
            {
                return (LogLevel) Enum.Parse(typeof(LogLevel), key.TitleCase());
            }

            #region LogLevelMethods

            /// <summary>
            /// Log a TRACE level message to the console.
            /// </summary>
            /// <param name="text">The text content of the log entry.</param>
            public void Trace(string text) => LogMessage(LogLevel.Trace, text);

            /// <summary>
            /// Log an INFO level message to the console.
            /// </summary>
            /// <param name="text">The text content of the log entry.</param>
            public void Info(string text) => LogMessage(LogLevel.Info, text);

            /// <summary>
            /// Log a DEBUG level message to the console.
            /// </summary>
            /// <param name="text">The text content of the log entry.</param>
            public void Debug(string text) => LogMessage(LogLevel.Debug, text);

            /// <summary>
            /// Log a WARNING level message to the console.
            /// </summary>
            /// <param name="text">The text content of the log entry.</param>
            public void Warning(string text) => LogMessage(LogLevel.Warning, text);

            /// <summary>
            /// Log an ERROR level message to the console.
            /// </summary>
            /// <param name="text">The text content of the log entry.</param>
            public void Error(string text) => LogMessage(LogLevel.Error, text);

            /// <summary>
            /// Log a FATAL level message to the console.
            /// </summary>
            /// <param name="text">The text content of the log entry.</param>
            public void Fatal(string text) => LogMessage(LogLevel.Fatal, text);

            #endregion

            /// <summary>
            /// Writes the log entry to the console if necessary.
            /// </summary>
            /// <param name="level">The severity level of the log entry.</param>
            /// <param name="text">The text content of the log entry.</param>
            private void LogMessage(LogLevel level, string text)
            {
                if (level >= Level)
                {
                    UnityEngine.Debug.Log($"[{_pluginName}] [{level}] {text}");
                }
            }
        }

        #endregion
    }
}
