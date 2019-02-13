using System;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
namespace Oxide.Plugins
{
    [Info("Slowmode Chat", "Death", "1.0.6")]
    [Description("Restrict players messages per second on a configurable interval")]
    class SlowmodeChat : RustPlugin
    {
        #region Declarations
        List<string> CD = new List<string>();
        const string perm = "slowmodechat.exclude";
        #endregion

        #region Hooks
        object OnUserChat(IPlayer player)
        {
            if (player == null) return null;
            if (CD.Contains(player.Id))
            {
                if (configData.Options.Enabled && !permission.UserHasPermission(player.Id, perm))
                {
                    player.Message(lang.GetMessage("errmsg", this, player.Id).Replace("{i}", configData.Settings.Interval.ToString()));
                    return true;
                }
            }
            else
            {
                CD.Add(player.Id);
                timer.Once(configData.Settings.Interval, ()
                    => CD.Remove(player.Id));
            }
            return null;
        }
        void Init()
        {
            LoadConfigVariables();
            if (configData.Options.Permission_Enabled)
                permission.RegisterPermission(perm, this);
            if (!configData.Options.Enabled)
                Unsubscribe(nameof(OnUserChat));
        }
        #endregion

        #region Functions
        [ConsoleCommand("slowmode")]
        void ChangeSettings(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer; // Not returning if null for rcon commands)
            if (arg.Args == null || arg.Args.Length <= 0)
            {
                arg.ReplyWith(lang.GetMessage("info1msg", this, player?.UserIDString));
                return;
            }
            if (configData.Options.Rcon_Only)
                if (player != null) return; // Returns if NOT null to only allow the commands to be sent via rcon
            var cmd = arg.Args[0].ToLower();
            if (string.IsNullOrEmpty(cmd)) return;
            switch (cmd)
            {
                case "enable":
                    arg.ReplyWith(lang.GetMessage("enmsg", this, player?.UserIDString));
                    configData.Options.Enabled = true;
                    break;
                case "disable":
                    arg.ReplyWith(lang.GetMessage("dimsg", this, player?.UserIDString));
                    configData.Options.Enabled = false;
                    break;
                case "interval":
                    if (arg.Args.Length < 2)
                    {
                        arg.ReplyWith(lang.GetMessage("info4msg", this, player?.UserIDString));
                        return;
                    }
                    var inti = arg.Args[1];
                    if (inti == null) return;
                    arg.ReplyWith(lang.GetMessage("info3msg", this, player?.UserIDString).Replace("{0}", inti));
                    configData.Settings.Interval = Convert.ToInt16(inti);
                    break;
                case "reload":
                    arg.ReplyWith(lang.GetMessage("info2msg", this, player?.UserIDString));
                    LoadConfigVariables();
                    break;
            }
            SaveConfig(configData);
        }
        #endregion

        #region Config
        private ConfigData configData;
        class ConfigData
        {
            public Options Options = new Options();
            public Settings Settings = new Settings();
        }
        class Options
        {
            public bool Enabled = false;
            public bool Rcon_Only = false;
            public bool Permission_Enabled = true;
        }
        class Settings
        {
            public int Interval = 5;
        }
        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();
            SaveConfig(configData);
        }
        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            var config = new ConfigData();
            SaveConfig(config);
        }
        void SaveConfig(ConfigData config)
            => Config.WriteObject(config, true);
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"errmsg", "Slowmode is currently active. You may only send one message every {i} seconds!" },
                {"enmsg", "Slowmode has been enabled!" },
                {"dimsg", "Slowmode has been disabled!" },
                {"info1msg", "\nCommands:\nenable - Enables slowmode\ndisable - Disables slowmode\ninterval - Adjust the interval between messages\nreload - Load new config values without reloading the entire plugin\n\nPermissions:\nslowmode.exclude - Excluded granted users/groups from slowmode" },
                {"info2msg", "Loaded new config values!e" },
                {"info3msg", "Interval has been adjusted to {0} seconds! Pre-existing timers will not be affected!" },
                {"info4msg", "You must specify an interval (in seconds.)" }
            }, this, "en");
        }
        private string msg(string key, string id = null) => lang.GetMessage(key, this, id);
        #endregion
    }
}