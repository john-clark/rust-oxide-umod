using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Update Notice", "Psystec", "1.0.9", ResourceId = 2837)]
    [Description("Notifies you when new Rust updates are released.")]
    public class UpdateNotice : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin DiscordMessages;
        private const string AdminPermission = "updatenotice.admin";
        private const string ApiUrl = "http://psystec.co.za/api/UpdateInfo";
        private Configuration _configuration;
        private int _serverBuildId = 0, _clientBuildId = 0, _stagingBuildId = 0, _oxideBuildId = 0, _version = 0;

        #endregion Fields

        #region Classes

        public class UpdateInfo
        {
            public int ServerVersion { get; set; }
            public int ClientVersion { get; set; }
            public int StagingVersion { get; set; }
            public int OxideVersion { get; set; }
            public int Version { get; set; }
        }

        #endregion Classes

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Only Notify Admins")]
            public bool OnlyNotifyAdmins { get; set; } = false;

            [JsonProperty("Enable Discord Notifications")]
            public bool EnableDiscordNotify { get; set; } = false;

            [JsonProperty("Discord Webhook URL")]
            public string DiscordWebhookURL { get; set; } = "https://support.discordapp.com/hc/en-us/articles/228383668";

            [JsonProperty("Enable Gui Notifications")]
            public bool EnableGuiNotifications { get; set; } = true;

            [JsonProperty("GUI Removal Delay (in Seconds)")]
            public int GuiRemovalDelay { get; set; } = 300;

            [JsonProperty("Enable Server Version Notifications")]
            public bool EnableServer { get; set; } = true;

            [JsonProperty("Enable Client Version Notifications")]
            public bool EnableClient { get; set; } = true;

            [JsonProperty("Enable Staging Version Notifications")]
            public bool EnableStaging { get; set; } = false;

            [JsonProperty("Enable Oxide Version Notifications")]
            public bool EnableOxide { get; set; } = false;

            [JsonProperty("Checking Interval (in Seconds)")]
            public int CheckingInterval { get; set; } = 60;
        }

        protected override void LoadDefaultConfig()
        {
            _configuration = new Configuration();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _configuration = Config.ReadObject<Configuration>();
        }

        protected override void SaveConfig() => Config.WriteObject(_configuration);

        #endregion Configuration

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ServerUpdated"] = "Server Update Released!",
                ["ClientUpdated"] = "Client Update Released!",
                ["StagingUpdated"] = "Staging Update Released!",
                ["OxideUpdated"] = "Oxide Update Released!",
                ["FailedToCheckUpdates"] = "Failed to check for RUST updates, if this keeps happening please contact the developer."
            }, this);
        }

        #endregion Localization

        #region Hooks

        private void Init()
        {
            LoadConfig();
            permission.RegisterPermission(AdminPermission, this);
        }

        private void Loaded()
        {
            if (_configuration.EnableDiscordNotify && DiscordMessages == null)
            {
                PrintWarning("Discored Notifications is enabled but the DiscoredMessages plugin is not loaded!");
                _configuration.EnableDiscordNotify = false;
            }
            if (_configuration.EnableDiscordNotify && _configuration.DiscordWebhookURL == "https://support.discordapp.com/hc/en-us/articles/228383668")
            {
                PrintWarning("Discored Notifications is enabled but the DiscordWebURL is not configured!");
                _configuration.EnableDiscordNotify = false;
            }

            timer.Every(_configuration.CheckingInterval, CompareBuilds);
        }

        #endregion Hooks

        #region Testing

        //[ChatCommand("updatenoticetest")]
        //private void updateNoticeTest(BasePlayer player, string command, string[] args)
        //{
        //    if (args.Length == 0) return;

        //    if (args[0] == "current")
        //    {
        //        SendReply(player, "Server: " + _serverBuildId.ToString());
        //        SendReply(player, "Client: " + _clientBuildId.ToString());
        //        SendReply(player, "Staging: " + _stagingBuildId.ToString());
        //        SendReply(player, "Oxide: " + _oxideBuildId.ToString());
        //        SendReply(player, "Version: " + _version.ToString());
        //    }

        //    if (args[0] == "server") _serverBuildId = 1;
        //    if (args[0] == "client") _clientBuildId = 1;
        //    if (args[0] == "staging") _stagingBuildId = 1;
        //    if (args[0] == "oxide") _oxideBuildId = 1;
        //    if (args[0] == "version") _version = 999;

        //    if (args[0] == "all")
        //    {
        //        _serverBuildId = 1;
        //        _clientBuildId = 1;
        //        _stagingBuildId = 1;
        //        _oxideBuildId = 1;
        //    }

        //}

        #endregion Testing

        #region Build Comparison

        private void CompareBuilds()
        {
            webrequest.Enqueue(ApiUrl, null, (code, response) =>
            {
                if (code != 200)
                {
                    if (code == 0) return;
                    
                    PrintWarning(Lang("FailedToCheckUpdates") + "\nError Code: " + code + " | Message: " + response);
                    return;
                }

                var updateInfo = JsonConvert.DeserializeObject<UpdateInfo>(response);

                if (_serverBuildId == 0 || _clientBuildId == 0 || _stagingBuildId == 0 || _oxideBuildId == 0 || _version == 0)
                {
                    _serverBuildId = updateInfo.ServerVersion;
                    _clientBuildId = updateInfo.ClientVersion;
                    _stagingBuildId = updateInfo.StagingVersion;
                    _oxideBuildId = updateInfo.OxideVersion;
                    _version = updateInfo.Version;
                }
                else
                {
                    bool serverUpdated = _serverBuildId != updateInfo.ServerVersion;
                    bool clientUpdated = _clientBuildId != updateInfo.ClientVersion;
                    bool stagingUpdated = _stagingBuildId != updateInfo.StagingVersion;
                    bool oxideUpdated = _oxideBuildId != updateInfo.OxideVersion;
                    bool versionUpdated = _version != updateInfo.Version;

                    if (!serverUpdated && !clientUpdated && !stagingUpdated && !oxideUpdated && !versionUpdated)
                        return;

                    //if (versionUpdated)
                    //{
                    //    _version = updateInfo.Version;
                    //    PrintWarning("UpdateNotice is out of date, shutting down.");
                    //    rust.RunServerCommand("o.unload UpdateNotice");
                    //}

                    if (serverUpdated)
                    {
                        _serverBuildId = updateInfo.ServerVersion;
                        if (_configuration.EnableServer && _configuration.EnableGuiNotifications) DrawGuiForAll(Lang("ServerUpdated"));
                        if (_configuration.EnableServer && _configuration.EnableDiscordNotify)
                        {
                            DiscordMessages?.Call("API_SendTextMessage", (string)_configuration.DiscordWebhookURL, (string)Lang("ServerUpdated"));
                        }
                    }
                    if (clientUpdated)
                    {
                        _clientBuildId = updateInfo.ClientVersion;
                        if (_configuration.EnableClient && _configuration.EnableGuiNotifications) DrawGuiForAll(Lang("ClientUpdated"));
                        if (_configuration.EnableClient && _configuration.EnableDiscordNotify)
                        {
                            DiscordMessages?.Call("API_SendTextMessage", (string)_configuration.DiscordWebhookURL, (string)Lang("ClientUpdated"));
                        }
                    }
                    if (stagingUpdated)
                    {
                        _stagingBuildId = updateInfo.StagingVersion;
                        if (_configuration.EnableStaging && _configuration.EnableGuiNotifications) DrawGuiForAll(Lang("StagingUpdated"));
                        if (_configuration.EnableStaging && _configuration.EnableDiscordNotify)
                        {
                            DiscordMessages?.Call("API_SendTextMessage", (string)_configuration.DiscordWebhookURL, (string)Lang("StagingUpdated"));
                        }
                    }
                    if (oxideUpdated)
                    {
                        _oxideBuildId = updateInfo.OxideVersion;
                        if (_configuration.EnableOxide && _configuration.EnableGuiNotifications) DrawGuiForAll(Lang("OxideUpdated"));
                        if (_configuration.EnableOxide && _configuration.EnableDiscordNotify)
                        {
                            DiscordMessages?.Call("API_SendTextMessage", (string)_configuration.DiscordWebhookURL, (string)Lang("OxideUpdated"));
                        }
                    }
                }
            }, this);
        }

        #endregion Build Comparison

        #region Gui Handling

        private void RemoveGuiAfterDelay(int delay) => timer.Once(delay, RemoveGuiForAll);

        private void RemoveGuiForAll()
        {
            BasePlayer.activePlayerList.ForEach(RemoveGui);
            GuiTracker = 0;
            y = 0.98;
        }

        private void RemoveGui(BasePlayer player)
        {
            for (int i = 0; i <= GuiTracker; i++)
            {
                CuiHelper.DestroyUi(player, "UpdateNotice" + i.ToString());
            }
        }

        private void DrawGuiForAll(string message)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (_configuration.OnlyNotifyAdmins && !HasPermission(player, AdminPermission))
                    continue;

                AddGui(player, message);
            }

            RemoveGuiAfterDelay(_configuration.GuiRemovalDelay);
        }

        double y = 0.98;
        int GuiTracker = 0;
        private void AddGui(BasePlayer player, string message)
        {
            y = y - 0.025;
            GuiTracker++;
            var container = new CuiElementContainer();

            var panel = container.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0.1 0.1 0.1 0.5"
                },
                RectTransform =
                {
                    AnchorMin = "0.012 " + y.ToString(), // left down
			        AnchorMax = "0.25 " + (y + 0.02).ToString() // right up
		        },
                CursorEnabled = false
            }, "Hud", "UpdateNotice" + GuiTracker.ToString());
            container.Add(new CuiLabel
            {
                Text =
                {
                    Text = message,
                    FontSize = 14,
                    Align = TextAnchor.MiddleCenter,
                    Color = "0.0 8.0 0.0 1.0"
                },
                RectTransform =
                {
                    AnchorMin = "0.00 0.00",
                    AnchorMax = "1.00 1.00"
                }
            }, panel);
            CuiHelper.AddUi(player, container);
            y = y - 0.005;
        }

        #endregion Procedures

        #region Helpers

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.userID.ToString(), perm);

        #endregion Helpers

        #region User API

        private int GetServerVersion() => _serverBuildId;  // Returns 0 if version could not be determined
        private int GetClientVersion() => _clientBuildId;  // Returns 0 if version could not be determined
        private int GetStagingVersion() => _stagingBuildId;  // Returns 0 if version could not be determined
        private int GetOxideVersion() => _oxideBuildId;  // Returns 0 if version could not be determined

        #endregion User API
    }
}