using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Connection Commands", "Wulf/lukespragg", "0.1.0", ResourceId = 2487)]
    [Description("Runs one or more server command when a player connects/disconnects")]
    public class ConnectionCmds : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Commands on connection (true/false)")]
            public bool ConnectionCommandsEnabled;

            [JsonProperty(PropertyName = "Commands on connection")]
            public List<string> ConnectionCommands;

            [JsonProperty(PropertyName = "Commands on disconnection (true/false)")]
            public bool DisconnectionCommandsEnabled;

            [JsonProperty(PropertyName = "Commands on disconnection")]
            public List<string> DisconnectionCommands;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    ConnectionCommandsEnabled = true,
                    ConnectionCommands = new List<string> { "oxide.grant user {id} epicstuff.temp", "examplecmd" },
                    DisconnectionCommandsEnabled = true,
                    DisconnectionCommands = new List<string> { "oxide.revoke user {name} epicstuff.temp", "example.cmd" }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.ConnectionCommandsEnabled == null)
                {
                    LoadDefaultConfig();
                    SaveConfig();
                }
            }
            catch
            {
                LogWarning($"Could not read oxide/config/{Name}.json, creating new config file");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Command Handling

        private void Init()
        {
            if (!config.ConnectionCommandsEnabled) Unsubscribe(nameof(OnUserConnected));
            if (!config.DisconnectionCommandsEnabled) Unsubscribe(nameof(OnUserDisconnected));
        }

        private void OnUserConnected(IPlayer player)
        {
            foreach (var cmd in config.ConnectionCommands.Where(c => c != "example"))
                server.Command(cmd.Replace("{id}", player.Id).Replace("{name}", player.Name));
        }

        private void OnUserDisconnected(IPlayer player)
        {
            foreach (var cmd in config.DisconnectionCommands.Where(c => c != "example"))
                server.Command(cmd.Replace("{id}", player.Id).Replace("{name}", player.Name));
        }

        [Command("examplecmd", "example.cmd")]
        private void ExampleCommand(IPlayer player, string command, string[] args)
        {
            LogWarning($"[{Title}] The command '{command}' was ran because it is an example in the config");
        }

        #endregion
    }
}