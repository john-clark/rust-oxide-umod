using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Oxide.Core;
using System;

namespace Oxide.Plugins
{
    [Info("WAnswer", "Iv Misticos", "0.0.1")]
    [Description("Questions? Answers!")]
    class WAnswer : RustPlugin
    {
        bool debug = false;

        #region Configuration
        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Questions and answers")]
            public Dictionary<string, string> data;

            [JsonProperty(PropertyName = "Plugin prefix")]
            public string prefix;

            [JsonProperty(PropertyName = "Plugin check mode (1, 2 or 3)")]
            public int pMode;

            [JsonProperty(PropertyName = "Enable regex and {ar} (All symbols)")]
            public bool eReg;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    data = new Dictionary<string, string>
                    {
                        { "WHEN {ar} WIPE?", "WIPE? SOON!" }
                    },
                    prefix = "<color=#ff8000>BOT: </color>",
                    pMode = 2,
                    eReg = true
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config.data == null || config.prefix == null || config.pMode == null || config.eReg == null) LoadDefaultConfig();
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

        object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            if (arg == null || arg.Args == null)
                return null;

            if (config.pMode == 1 || config.pMode == 3)
            {
                foreach (KeyValuePair<string, string> kvp in config.data)
                {
                    foreach (string _arg in arg.Args)
                    {
                        if (_arg.Contains(kvp.Key))
                            SendReply(BasePlayer.FindByID(arg.Connection.userid), config.prefix + kvp.Value);
                    }
                }
            }
            if (config.pMode == 2 || config.pMode == 3)
            {
                foreach (KeyValuePair<string, string> kvp in config.data)
                {
                    if (config.eReg)
                    {
                        try
                        {
                            if (debug) Puts(kvp.Key.Replace("{ar}", "[.\\s\\S]+"));
                            if (Regex.IsMatch(string.Join(" ", arg.Args), kvp.Key.Replace("{ar}", "[.\\s\\S]+")))
                                SendReply(BasePlayer.FindByID(arg.Connection.userid), config.prefix + kvp.Value);
                        }
                        catch (Exception e)
                        {
                            Interface.Oxide.LogError("Error: " + e.Message);
                        }
                    }
                    else
                    {
                        if (string.Join(" ", arg.Args).Contains(kvp.Key))
                            SendReply(BasePlayer.FindByID(arg.Connection.userid), config.prefix + kvp.Value);
                    }
                }
            }

            return null;
        }

        [ConsoleCommand("wanswer")]
        void reloadConfig(ConsoleSystem.Arg arg)
        {
            LoadConfig();
        }

        [ChatCommand("wanswer")]
        void reloadConfig2(BasePlayer player, string cmd, string[] args)
        {
            LoadConfig();
        }
    }
}