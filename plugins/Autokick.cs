using System;
using System.Linq;
using System.Collections.Generic;

using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Autokick", "Exel80", "1.2.0", ResourceId = 2138)]
    [Description("Autokick help you change your server to \"maintenance break\" mode, if you need it!")]
    class Autokick : CovalencePlugin
    {
        #region Initialize
        public bool DEBUG = false;
        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void Loaded()
        {
            permission.RegisterPermission("autokick.use", this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Toggle"] = "Autokick is [#yellow]{0}[/#]",
                ["KickHelp"] = "When Autokick is [#yellow]{0}[/#], use [#yellow]{1}[/#] to kick all online players.",
                ["Kicked"] = "All online players has been kicked! Except players whos in whitelist or is admin.",
                ["Set"] = "Kick message is now setted to \"[#yellow]{0}[/#]\"",
                ["Message"] = "Kick message is \"[#yellow]{0}[/#]\"",
                ["ToggleHint"] = "Autokick must be [#yellow]{0}[/#], before can execute [#yellow]{1}[/#] command!",
                ["Usage"] = "[#cyan]Usage:[/#] [#silver]{0}[/#] [#grey]{1}[/#]"
            }, this);
        }
        private void Init()
        {
            if (!config.akEnable)
                Unsubscribe("CanUserLogin");
        }
        #endregion

        #region Commands
        [Command("ak", "autokick")]
        private void cmdAK(IPlayer player, string command, string[] args)
        {
            // Checking that player has permission to use commands
            if (!hasPermission(player, "autokick.use"))
                return;

            string _name = player.Name;
            string _id = player.Id.ToString();

            // Check that args isn't empty.
            if (args?.Length < 1)
            {
                _chat(player, Lang("Usage", _id, $"/{command}", "on/off | kick | set | message"));
                return;
            }

            _debug(player, $"arg: {args[0]} - Name: {_name} - Id: {_id} - isAdmin: {player.IsAdmin}");

            switch (args[0])
            {
                case "on":
                    {
                        // Change Toggle from config file
                        config.akEnable = true;
                        SaveConfig();

                        // Save config
                        Config.Save();

                        // Print Toggle
                        _chat(player, Lang("Toggle", _id, "ACTIVATED!") + "\n" + Lang("KickHelp", _id, "true", "/ak kick"));
                        _debug(player, $"Changed Toggle to {config.akEnable}");
                    }
                    break;
                case "kick":
                    {
                        // Check if Toggle isn't False
                        if (!config.akEnable)
                        {
                            _chat(player, Lang("ToggleHint", _id, "true", "/ak kick"));
                            return;
                        }

                        // Kick all players (Except if config allow auth 1 and/or 2 to stay)
                        KickerFromOnlineList();

                        _chat(player, Lang("Kicked", _id));
                    }
                    break;
                case "off":
                    {
                        // Change Toggle from config file
                        config.akEnable = false;
                        SaveConfig();

                        // Save config
                        Config.Save();

                        // Print Toggle
                        _chat(player, Lang("Toggle", _id, "DE-ACTIVATED!"));
                        _debug(player, $"Changed Toggle to {config.akEnable}");
                    }
                    break;
                case "set":
                    {
                        // Checking that args length isnt less then 5
                        if (args?.Length > 5)
                        {
                            _chat(player, Lang("Usage", _id, $"/{command}", "on/off | kick | set | message"));
                            return;
                        }

                        // Read all args to one string with space.
                        string _arg = string.Join(" ", args)?.Remove(0, 4);

                        // Change KickMessage from config file
                        config.akKickMessage = _arg;
                        SaveConfig();

                        // Save config
                        Config.Save();

                        // Print KickMessage
                        _chat(player, Lang("Set", _id, config.akKickMessage));
                    }
                    break;
                case "message":
                    {
                        // Print KickMessage
                        _chat(player, Lang("Message", _id, config.akKickMessage));
                    }
                    break;
            }
        }
        #endregion

        #region Oxide Hooks
        object CanUserLogin(string name, string id, string ip)
        {
            if (config.akEnable)
            {
                if (config.akWhitelist.Contains(name, StringComparer.OrdinalIgnoreCase))
                    return config.akKickMessage;
                if (config.akWhitelist.Contains(ip))
                    return config.akKickMessage;
                if (config.akWhitelist.Contains(id))
                    return config.akKickMessage;
            }
            return null;
        }
        #endregion

        #region Kicker
        private void KickerFromOnlineList()
        {
            // If Autokick is enabled, then start timer (8sec)
            if (config.akEnable)
            {
                try
                {
                    foreach (IPlayer player in players.Connected.ToList())
                    {
                        string _name = player.Name;
                        string _id = player.Id.ToString();
                        string _ip = player.Address;
                        string message = config.akKickMessage;

                        if (DEBUG) Puts($"[Deubg] Name: {_name}, Id: {_id}, isAdmin: {player.IsAdmin}");

                        if (config.akWhitelist.Contains(_name, StringComparer.OrdinalIgnoreCase))
                            return;
                        if (config.akWhitelist.Contains(_id))
                            return;
                        if (config.akWhitelist.Contains(_ip))
                            return;
                        if (player.IsAdmin)
                            return;

                        if (player.IsConnected)
                            player.Kick(message);
                    }
                }
                catch (Exception e) { PrintWarning($"{e.GetBaseException()}"); }
            }
        }
        #endregion

        #region Helper
        private void _chat(IPlayer player, string msg) => player.Reply(covalence.FormatText($"{config.akPrefix} {msg}"));
        private void _debug(IPlayer player, string msg)
        {
            if (DEBUG)
                Puts($"[Debug] {player.Name} - {msg}");
        }
        bool hasPermission(IPlayer player, string permissionName)
        {
            if (player.IsAdmin) return true;
            return permission.UserHasPermission(player.Id.ToString(), permissionName);
        }
        #endregion

        #region Configuration
        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Enable AutoKick (true/false)")]
            public bool akEnable;

            [JsonProperty(PropertyName = "Messages Prefix (Disable == Empty string)")]
            public string akPrefix;

            [JsonProperty(PropertyName = "Kick Messages")]
            public string akKickMessage;

            [JsonProperty(PropertyName = "Whitelist (SteamID64 or IP Address or Displayname)")]
            public List<string> akWhitelist;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    akEnable = false,
                    akPrefix = "[#cyan][AutoKick][/#]",
                    akKickMessage = "You have been kicked! Reason: Server is on maintenance break!",
                    akWhitelist = new List<string> { "Exel80", "127.0.0.1", "localhost", "76561198014553078" }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.akWhitelist == null) LoadDefaultConfig();
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
    }
}