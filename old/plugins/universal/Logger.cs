using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

// TODO: Finish implementing ItemList and ItemListType config settings
// TODO: Finish implementing RotateLog config option
// TODO: Fix RCON clients screwing up with log output from select commands
/*
    playerlist
    status
    banlist
    banlistex
    bans
    server.fps
    server.hostname
    server.description
    serverinfo
    plugins
*/

namespace Oxide.Plugins
{
    [Info("Logger", "Wulf/lukespragg", "2.2.1")]
    [Description("Configurable logging of chat, commands, connections, and more")]
    public class Logger : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Log chat messages (true/false)")]
            public bool LogChat { get; set; } = true;

            [JsonProperty(PropertyName = "Log command usage (true/false)")]
            public bool LogCommands { get; set; } = true;

            [JsonProperty(PropertyName = "Log player connections (true/false)")]
            public bool LogConnections { get; set; } = true;

            [JsonProperty(PropertyName = "Log player disconnections (true/false)")]
            public bool LogDisconnections { get; set; } = true;

            [JsonProperty(PropertyName = "Log player respawns (true/false)")]
            public bool LogRespawns { get; set; } = true;

#if RUST

            [JsonProperty(PropertyName = "Log when crafting started (true/false)")]
            public bool LogCraftingStarted { get; set; } = true;

            [JsonProperty(PropertyName = "Log when crafting cancelled (true/false)")]
            public bool LogCraftingCancelled { get; set; } = true;

            [JsonProperty(PropertyName = "Log when crafting finished (true/false)")]
            public bool LogCraftingFinished { get; set; } = true;

            [JsonProperty(PropertyName = "Log items dropped by players (true/false)")]
            public bool LogItemDrops { get; set; } = true;

#endif

            [JsonProperty(PropertyName = "Log output to console (true/false)")]
            public bool LogToConsole { get; set; } = false;

            // TODO: Option to listen to commands from admin, moderator, or all

            [JsonProperty(PropertyName = "Rotate logs daily (true/false)")]
            public bool RotateLogs { get; set; } = true;

            [JsonProperty(PropertyName = "Command list (full or short commands)")]
            public List<string> CommandList { get; set; } = new List<string>
            {
                /*"help", "version", "chat.say", "craft.add", "craft.canceltask", "global.kill",
                "global.respawn", "global.respawn_sleepingbag", "global.status", "global.wakeup",
                "inventory.endloot", "inventory.unlockblueprint"*/
            };

            [JsonProperty(PropertyName = "Command list type (blacklist or whitelist)")]
            public string CommandListType { get; set; } = "blacklist";

            //[JsonProperty(PropertyName = "Item list (full or short names)")]
            //public List<string> ItemList { get; set; } = new List<string>
            //{
            //    /*"rock", "torch"*/
            //};

            //[JsonProperty(PropertyName = "Item list type (blacklist or whitelist)")]
            //public string ItemListType { get; set; } = "blacklist";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
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
                ["CommandReason"] = "reason",
                ["CraftingCancelled"] = "{0} ({1}) cancelled crafting {2} {3}",
                ["CraftingFinished"] = "{0} ({1}) finished crafting {2} {3}",
                ["CraftingStarted"] = "{0} ({1}) started crafting {2} {3}",
                ["ItemDropped"] = "{0} ({1}) dropped {2} {3}",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["PlayerCommand"] = "{0} ({1}) ran command: {2} {3}",
                ["PlayerConnected"] = "{0} ({1}) connected from {2}",
                ["PlayerDisconnected"] = "{0} ({1}) disconnected",
                ["PlayerMessage"] = "{0} ({1}) said: {2}",
                ["PlayerRespawned"] = "{0} ({1}) respawned at {2}",
                ["RconCommand"] = "{0} ran command: {1} {2}",
                ["ServerCommand"] = "SERVER ran command: {0} {1}"
            }, this);
        }

        #endregion Localization

        #region Initialization

        private const string commandReason = "loggerreason";
        private const string permReason = "logger.reason";

        private void Init()
        {
            permission.RegisterPermission(permReason, this);

            AddCovalenceCommand(commandReason, "ReasonCommand");
            AddLocalizedCommand("CommandReason", "ReasonCommand");

            if (!config.LogChat) Unsubscribe("OnUserChat");
            if (!config.LogCommands) Unsubscribe("OnServerCommand");
            if (!config.LogConnections) Unsubscribe("OnUserConnected");
            if (!config.LogDisconnections) Unsubscribe("OnUserDisconnected");
            if (!config.LogRespawns) Unsubscribe("OnUserRespawned");
#if RUST
            if (!config.LogCraftingStarted) Unsubscribe("OnItemCraft");
            if (!config.LogCraftingCancelled) Unsubscribe("OnItemCraftCancelled");
            if (!config.LogCraftingFinished) Unsubscribe("OnItemCraftFinished");
            if (!config.LogItemDrops) Unsubscribe("OnItemAction");
#endif
        }

        #endregion Initialization

        #region Logging

        private void OnUserChat(IPlayer player, string message) => Log("chat", "PlayerMessage", player.Name, player.Id, message);

        private void OnUserConnected(IPlayer player) => Log("connections", "PlayerConnected", player.Name, player.Id, player.Address);

        private void OnUserDisconnected(IPlayer player) => Log("disconnections", "PlayerDisconnected", player.Name, player.Id);

        private void OnUserRespawned(IPlayer player) => Log("respawns", "PlayerRespawned", player.Name, player.Id, player.Position().ToString());

#if RUST
        private void OnItemAction(Item item, string action)
        {
            BasePlayer player = item.parent?.playerOwner;

            if (action.ToLower() == "drop" && player != null)
            {
                Log("itemdrops", "ItemDropped", player.displayName.Sanitize(), player.UserIDString, item.amount, item.info.displayName?.english ?? item.name);
            }
        }

        private void OnItemCraft(ItemCraftTask task)
        {
            BasePlayer player = task.owner;
            ItemDefinition item = task.blueprint.targetItem;
            Log("crafting", "CraftingStarted", player.displayName.Sanitize(), player.UserIDString, task.amount, item.displayName.english);
        }

        private void OnItemCraftCancelled(ItemCraftTask task)
        {
            BasePlayer player = task.owner;
            ItemDefinition item = task.blueprint.targetItem;
            Log("crafting", "CraftingCancelled", player.displayName.Sanitize(), player.UserIDString, task.amount, item.displayName.english);
        }

        private void OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            BasePlayer player = task.owner;
            Log("crafting", "CraftingFinished", player.displayName.Sanitize(), player.UserIDString, item.amount, item.info.displayName.english);
        }

        private void OnRconCommand(IPEndPoint ip, string command, string[] args)
        {
            if (command == "chat.say" || command == "say")
            {
                return;
            }

            if (config.CommandListType.ToLower() == "blacklist" && config.CommandList.Contains(command) || config.CommandList.Contains(command))
            {
                return;
            }

            if (config.CommandListType.ToLower() == "whitelist" && !config.CommandList.Contains(command) && !config.CommandList.Contains(command))
            {
                return;
            }

            Log("commands", "RconCommand", ip.Address, command, string.Join(" ", args));
        }

        private void OnServerCommand(ConsoleSystem.Arg arg)
        {
            string command = arg.cmd.Name;
            string fullCommand = arg.cmd.FullName;

            if (fullCommand == "chat.say")
            {
                return;
            }

            if (config.CommandListType.ToLower() == "blacklist" && config.CommandList.Contains(command) || config.CommandList.Contains(fullCommand))
            {
                return;
            }

            if (config.CommandListType.ToLower() == "whitelist" && !config.CommandList.Contains(command) && !config.CommandList.Contains(fullCommand))
            {
                return;
            }

            if (arg.Connection != null)
            {
                Log("commands", "PlayerCommand", arg.Connection.username.Sanitize(), arg.Connection.userid, fullCommand, arg.FullString);
            }
            else
            {
                Log("commands", "ServerCommand", fullCommand, arg.FullString);
            }
        }
#endif

        private void OnUserCommand(IPlayer player, string command, string[] args)
        {
            if (config.CommandListType.ToLower() == "blacklist" && config.CommandList.Contains(command) || config.CommandList.Contains("/" + command))
            {
                return;
            }

            if (config.CommandListType.ToLower() == "whitelist" && !config.CommandList.Contains(command) && !config.CommandList.Contains("/" + command))
            {
                return;
            }

            Log("commands", "PlayerCommand", player.Name, player.Id, command, string.Join(" ", args));
        }

        // TODO: Add command logged message in player's console/chat?
        // TODO: Prompt for reason within X seconds when command is used?

        #endregion Logging

        #region Command

        private void ReasonCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permReason))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            Log("reasons", "Reason");
            Message(player, "ReasonLogged", string.Join(" ", args));
        }

        #endregion Command

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

        private void Log(string filename, string key, params object[] args)
        {
            if (config.LogToConsole)
            {
                Puts(Lang(key, null, args));
            }
            LogToFile(filename, $"[{DateTime.Now}] {Lang(key, null, args)}", this);
        }

        private void Message(IPlayer player, string key, params object[] args)
        {
            player.Reply(Lang(key, player.Id, args));
        }

        #endregion Helpers
    }
}
