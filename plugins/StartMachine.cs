using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Start Machine", "Wulf/lukespragg", "2.1.1")]
    [Description("Automatically start machines on server startup and by manual control")]
    public class StartMachine : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Campfire control (true/false))")]
            public bool CampfireControl;

            [JsonProperty(PropertyName = "Drill control (true/false)")]
            public bool DrillControl;

            [JsonProperty(PropertyName = "Fridge control (true/false)")]
            public bool FridgeControl;

            /*[JsonProperty(PropertyName = "Furnace control (true/false)")]
            public bool FurnaceControl;*/ // TODO: Implement for Rust

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    CampfireControl = true,
                    DrillControl = true,
                    FridgeControl = true,
                    //FurnaceControl = true
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.DrillControl == null) LoadDefaultConfig();
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

        #endregion Configuration

        #region Localization

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandAlias"] = "machines",
                ["MachinesStarted"] = "{0} {1} machines have been started",
                ["MachinesStopped"] = "{0} {1} machines have been stopped",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command"
            }, this);
        }

        #endregion Localization

        #region Initialization

        private const string permControl = "startmachine.control";

        private void OnServerInitialized()
        {
            permission.RegisterPermission(permControl, this);

            AddCommandAliases("CommandAlias", "MachinesCommand");
            AddCovalenceCommand("startmachines", "MachinesCommand");

            ToggleMachines(true);
        }

        #endregion Initialization

        #region Machine Control

        private bool state;

        private void MachinesCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permControl))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            if (args.Length > 0) state = args[0] != "off";

            ToggleMachines(!state, player);
        }

        private void ToggleMachines(bool toggle, IPlayer player = null)
        {
            if (config.CampfireControl)
            {
                var count = 0;
                var enumerator = CampfireMachine.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var camp = enumerator.Current.Value;

                    if (!camp.isActiveAndEnabled) continue;
                    if (camp.Powered == toggle) continue;
                    if (!camp.HasFuel) continue;

                    camp.RPC("SetPoweredServer", 0, toggle);
                    count++;
                }

                var message = Lang(toggle ? "MachinesStarted" : "MachinesStopped", player?.Id, count, "fridge");
                player?.Reply(message);
                Puts(message); // TODO: Logging optional
            }

            if (config.DrillControl)
            {
                var count = 0;
                var drills = DrillMachine.GetEnumerator();
                while (drills.MoveNext())
                {
                    var drill = drills.Current.Value;

                    if (!drill.isActiveAndEnabled) continue;
                    if (drill.Powered == toggle) continue;
                    if (!drill.HasFuel) continue;

                    drill.RPC("SetPoweredServer", 0, toggle);
                    count++;
                }

                var message = Lang(toggle ? "MachinesStarted" : "MachinesStopped", player?.Id, count, "fridge");
                player?.Reply(message);
                Puts(message); // TODO: Logging optional
            }

            if (config.FridgeControl)
            {
                var count = 0;
                var enumerator = FridgeMachine.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var fridge = enumerator.Current.Value;

                    if (!fridge.isActiveAndEnabled) continue;
                    if (fridge.Powered == toggle) continue;

                    fridge.RPC("SetPoweredServer", 0, toggle);
                    count++;
                }

                var message = Lang(toggle ? "MachinesStarted" : "MachinesStopped", player?.Id, count, "fridge");
                player?.Reply(message);
                Puts(message); // TODO: Logging optional
            }

            // TODO: Combine messages into single message. Ex. "2 campfires, 3 drills, and 4 fridges stopped"

            state = toggle;
        }

        #endregion Machine Control

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

        #endregion Helpers
    }
}
