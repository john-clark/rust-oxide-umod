using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Explosion Tracker", "Ryan", "1.0.2")]
    [Description("Tracks and logs every explosion that happens on the server")]
    internal class ExplosionTracker : RustPlugin
    {
        [PluginReference] private Plugin Slack, Discord;

        private enum  Actions
        {
            Throw,
            Drop,
            Launch
        }

        #region Config

        private ConfigFile ConfigData;

        public class ConfigFile
        {
            [JsonProperty(PropertyName = "Enable RCON messages")]
            public bool RCON;

            [JsonProperty(PropertyName = "Enable Log messages")]
            public bool Log;

            [JsonProperty(PropertyName = "Send messages to online admins")]
            public bool AdminMsg;

            [JsonProperty(PropertyName = "Enable Discord messages")]
            public bool Discord;

            [JsonProperty(PropertyName = "Slack Settings")]
            public SlackInfo SlackInfo;

            [JsonProperty(PropertyName = "Enable rocket launch notifications")]
            public bool Launch;

            [JsonProperty(PropertyName = "Enable explosive throw notifications")]
            public bool Throw;

            [JsonProperty(PropertyName = "Enable explosive drop notifications")]
            public bool Drop;

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    RCON = true,
                    Log = true,
                    AdminMsg = true,
                    Discord = false,
                    SlackInfo = new SlackInfo
                    {
                        Enabled = false,
                        Channel = "mychannelname"
                    },
                    Launch = true,
                    Drop = false,
                    Throw = true
                };
            }
        }

        public class SlackInfo
        {
            [JsonProperty(PropertyName = "Enable Slack messages")]
            public bool Enabled { get; set; }

            [JsonProperty(PropertyName = "Channel name")]
            public string Channel { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating default configuration file...");
            ConfigData = ConfigFile.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            ConfigData = Config.ReadObject<ConfigFile>();
        }

        protected override void SaveConfig() => Config.WriteObject(ConfigData);

        #endregion Config

        #region Lang

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Dropped Message"] = "{0} ({1}) dropped {2} at (X: {3}, Y: {4}, Z: {5})",
                ["Launched Message"] = "{0} ({1}) launched a rocket at (X: {3}, Y: {4}, Z: {5})",
                ["Thrown Message"] = "{0} ({1}) dropped {2} at (X: {3}, Y: {4}, Z: {5})"
            }, this);
        }

        #endregion Lang

        #region Methods

        private void Log(string message, string action) => LogToFile(action, string.Format($"[{DateTime.Now}] " + message), this);

        private void SendSlack(string message, BasePlayer player) => Slack?.Call("SimpleMessage", message, covalence.Players.FindPlayerById(player.UserIDString), ConfigData.SlackInfo.Channel);

        private void SendDiscord(string message) => Discord?.Call("SendMessage", message);

        private string ThrowMsg(BasePlayer player, BaseEntity entity)
        {
            return string.Format(lang.GetMessage("Thrown Message", this, player.UserIDString), player.displayName, player.UserIDString, entity.ShortPrefabName,
                Math.Round(entity.transform.position.x, 2), Math.Round(entity.transform.position.y, 2), Math.Round(entity.transform.position.z, 2));
        }

        private string LaunchMsg(BasePlayer player, BaseEntity entity)
        {
            return string.Format(lang.GetMessage("Launched Message", this, player.UserIDString), player.displayName, player.UserIDString, entity.ShortPrefabName,
                Math.Round(entity.transform.position.x, 2), Math.Round(entity.transform.position.y, 2), Math.Round(entity.transform.position.z, 2));
        }

        private string DropMsg(BasePlayer player, BaseEntity entity)
        {
            return string.Format(lang.GetMessage("Dropped Message", this, player.UserIDString), player.displayName, player.UserIDString, entity.ShortPrefabName,
                Math.Round(entity.transform.position.x, 2), Math.Round(entity.transform.position.y, 2), Math.Round(entity.transform.position.z, 2));
        }

        private void SendMessages(string message, Actions action, BasePlayer player)
        {
            if (ConfigData.Log) Log(message, action.ToString());
            if (ConfigData.RCON) Puts(message);
            if (ConfigData.AdminMsg)
            {
                foreach (var p in BasePlayer.activePlayerList)
                    if (p.IsAdmin) PrintToChat(player, message);
            }
            if (ConfigData.SlackInfo.Enabled) SendSlack(message, player);
            if (ConfigData.Discord) SendDiscord(message);
        }

        private void ConstructMessage(BasePlayer player, BaseEntity entity, Actions action)
        {
            if (action.Equals(Actions.Throw) && ConfigData.Throw)
                SendMessages(ThrowMsg(player, entity), action, player);
            if (action.Equals(Actions.Launch) && ConfigData.Launch)
                SendMessages(LaunchMsg(player, entity), action, player);
            if (action.Equals(Actions.Drop) && ConfigData.Drop)
                SendMessages(DropMsg(player, entity), action, player);
        }

        #endregion Methods

        #region Hooks

        private void Init() => SaveConfig();

        private void OnServerInitialized()
        {
            if (ConfigData.Discord && !Discord) PrintWarning("You have Discord notifications on, but 'Discord' plugin isn't detected");
            if (ConfigData.SlackInfo.Enabled && !Slack) PrintWarning("You have Slack notifications on, but 'Slack' plugin isn't detected");
        }

        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            NextTick(() =>
            {
                try { ConstructMessage(player, entity, Actions.Throw); }
                catch { }
            });
        }

        private void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            NextTick(() =>
            {
                try { ConstructMessage(player, entity, Actions.Launch); }
                catch { }
            });
        }

        private void OnExplosiveDropped(BasePlayer player, BaseEntity entity)
        {
            NextTick(() =>
            {
                try { ConstructMessage(player, entity, Actions.Drop); }
                catch { }
            });
        }

        #endregion Hooks
    }
}
