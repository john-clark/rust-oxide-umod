using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("ConnectionLimiter", "Exel80", "1.0.1", ResourceId = 2649)]
    [Description("Help server admins block spam re-connecting")]
    class ConnectionLimiter : CovalencePlugin
    {
        public Dictionary<string, DateTime> ConnectedPlayers = new Dictionary<string, DateTime>();

        #region Configuration
        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Enable Connection Limit (true/false)")]
            public bool limitEnabled;

            [JsonProperty(PropertyName = "Kick message show current waiting time (true/false)")]
            public bool limitCurrenttime;

            [JsonProperty(PropertyName = "Connection cooldown (in Seconds)")]
            public int limitCooldown;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    limitEnabled = true,
                    limitCurrenttime = true,
                    limitCooldown = 30,
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
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

        #region Initialization
        private void Init()
        {
            if (!config.limitEnabled)
            {
                Unsubscribe("CanUserLogin");
                Unsubscribe("OnUserDisconnected");
            }
        }
        #endregion

        #region Lang
        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["WaitConnection"] = "Wait connection: {0} second"
            }, this);
        }
        #endregion

        #region Oxide Hooks
        object CanUserLogin(string name, string id, string ip)
        {
            if (!ConnectedPlayers.ContainsKey(id))
                return null;

            if (ConnectedPlayers[id] > DateTime.Now)
            {
                //Puts($"Kicked from server {DateTime.Now.AddSeconds(config.limitCooldown)}");
                if (config.limitCurrenttime)
                    return Lang("WaitConnection", id, Math.Round((ConnectedPlayers[id] - DateTime.Now).TotalSeconds));
                else
                    return Lang("WaitConnection", id, config.limitCooldown);
            }
            else
            {
                //Puts($"Removed from DB");
                ConnectedPlayers.Remove(id);
            }
            return null;
        }

        private void OnUserDisconnected(IPlayer player)
        {
            if (!ConnectedPlayers.ContainsKey(player.Id))
            {
                //Puts($"Disconnected, added to DB {DateTime.Now.AddSeconds(config.limitCooldown)}");
                ConnectedPlayers.Add(player.Id, DateTime.Now.AddSeconds(config.limitCooldown));
            }
        }
        #endregion
    }
}