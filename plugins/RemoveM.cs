using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("RemoveM", "Iv Misticos", "0.0.1")]
    [Description("Removes stupid messages!")]
    class RemoveM : RustPlugin
    {
        #region Plugin Vars

        #endregion

        #region Configuration
        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Debug")]
            public bool debug;

            [JsonProperty(PropertyName = "Key Words")]
            public List<string> data;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    debug = true,
                    data = new List<string> { "gave" }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config.data == null) LoadDefaultConfig();
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Functions
        object OnUserChat(IPlayer player, string message)
        {
            if (message == null)
                return null;

            foreach (string temp in config.data)
            {
                if (message.Contains(temp))
                    return false;
            }
            return null;
        }

        object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            if (arg == null)
                return null;

            foreach (string temp in config.data)
            {
                if (arg.ToString().Contains(temp))
                    return false;
            }
            return null;
        }

        object OnMessagePlayer(string message, BasePlayer player)
        {
            if (message == null)
                return null;

            foreach (string temp in config.data)
            {
                if (message.Contains(temp))
                    return false;
            }
            return null;
        }

        object OnServerMessage(string message, string name, string color, ulong id)
        {
            if (message == null)
                return null;

            foreach (string temp in config.data)
            {
                if (message.Contains(temp))
                    return false;
            }
            return null;
        }
        #endregion
    }
}
