using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("ULog", "Iv Misticos", "0.0.1")]
    [Description("Logs some things")]
    class ULog : RustPlugin
    {
        #region Configuration
        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Log messages that players receive")]
            public bool logReceive;

            [JsonProperty(PropertyName = "Log chat messages")]
            public bool logChat;

            [JsonProperty(PropertyName = "Log kills, ...")]
            public bool logKill;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    logReceive = true,
                    logChat = true,
                    logKill = true
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
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        object OnMessagePlayer(string message, BasePlayer player)
        {
            if (config.logReceive)
                LogToFile(player.UserIDString, $"[{System.DateTime.Now}] RECEIVED: " + message, this, false);

            return null;
        }

        object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            if (arg == null || arg.Args == null)
                return null;

            if (config.logChat)
                LogToFile(arg.Connection.userid.ToString(), $"[{System.DateTime.Now}] - CHAT: " + string.Join(" ", arg.Args), this, false);

            return null;
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null)
                return;
            var corpse = entity as BaseCorpse;
            if (corpse != null)
                return;
            if (entity.ToPlayer() == null)
                return;
            if (info?.Initiator?.ToPlayer() == null)
                return;
            LogToFile(entity.ToPlayer().UserIDString, $"[{System.DateTime.Now}] - KILLED: " + "Player {name} died, killed by {init}".Replace("{name}", entity.ToPlayer()._name).Replace("{init}", info.Initiator.ToPlayer()._name), this, false);
        }
    }
}
