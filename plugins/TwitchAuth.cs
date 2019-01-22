using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// TODO: Add support for multiple channels

namespace Oxide.Plugins
{
    [Info("Twitch Auth", "Wulf/lukespragg", "0.1.4")]
    [Description("Only allow Twitch channel followers to join your server")]
    public class TwitchAuth : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Twitch channel name")]
            public string TwitchChannel { get; set; } = "";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            string configPath = $"{Interface.Oxide.ConfigDirectory}{Path.DirectorySeparatorChar}{Name}.json";
            LogWarning($"Could not load a valid configuration file, creating a new configuration file at {configPath}");
            config = new Configuration();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion Configuration

        #region Localization

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandAuth"] = "twitchauth",
                ["CommandUsage"] = "Usage: {0} <Twitch channel name>",
                ["IsExcluded"] = "{0} is excluded from Twitch auth check",
                ["IsFollowing"] = "{0} is a Twitch follower, allowing connection",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["NotAFollower"] = "{0} tried to join, but is not a Twitch follower",
                ["NotFollowing"] = "Please follow @ twitch.tv/{0} to join",
                ["NotLinked"] = "Please link your Twitch.tv account to Steam to join",
                ["TryAgainLater"] = "Currently unavailable, please try again later",
                ["TwitchChannelSet"] = "Twitch channel set to: {0}"
            }, this);
        }

        #endregion Localization

        #region Initialization

        private const string clientId = "5ekouzzlfd08acyaqdj3afpzi4djni";
        private const string permAdmin = "twitchauth.admin";
        private const string permExclude = "twitchauth.exclude";

        public class TwitchUser
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        private void Init()
        {
            permission.RegisterPermission(permAdmin, this);
            permission.RegisterPermission(permExclude, this);

            AddLocalizedCommand("CommandTwitchAuth", "TwitchAuthCommand");
        }

        #endregion Initialization

        #region Commands

        private void TwitchAuthCommand(IPlayer player, string command, string[] args)
        {
            if (player.HasPermission(permAdmin))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args.Length < 1)
            {
                Message(player, "CommandUsage", command);
                return;
            }

            config.TwitchChannel = args[0];
            Message(player, "TwitchChannelSet", args[0]);
            SaveConfig();
        }

        #endregion Commands

        #region Twitch Handling

        private void OnUserConnected(IPlayer player)
        {
            if (player.HasPermission(permExclude))
            {
                Puts(Lang("IsExcluded", null, player.Name));
                return;
            }

            SteamCheck(player);
        }

        private void SteamCheck(IPlayer player)
        {
            string url = $"https://api.twitch.tv/api/steam/{player.Id}?client_id={clientId}";
            webrequest.Enqueue(url, null, (code, response) =>
            {
                Callback(code, response, player);
            }, this);
        }

        private void IsFollowing(string name, IPlayer player)
        {
            string url = $"https://api.twitch.tv/kraken/users/{name}/follows/channels/{config.TwitchChannel}?client_id={clientId}";
            webrequest.Enqueue(url, null, (code, response) =>
            {
                Callback(code, response, player);
            }, this);
        }

        private void Callback(int code, string response, IPlayer player)
        {
            JObject json = JObject.Parse(response);
            string message = json["message"]?.ToString() ?? "No response message";

            if (code == 400 || code == 401 || code == 403 || code == 429 || code == 500 || code == 503)
            {
                // 400  Bad Request  {"error":"Bad Request","status":400,"message":"No client id specified"}
                //                   {"error":"Bad Request","status":400,"message":"Requests must be made over SSL"}
                // 401  Unauthorized  {"error":"Unauthorized","status":401,"message":"Token invalid or missing required scope"}
                // 403  Forbidden
                // 429  Too Many Requests
                // 500  Internal Server Error
                // 503  Service Unavailable
                LogWarning(message);
                player.Kick(Lang("TryAgainLater", player.Id));
                return;
            }

            if (code == 404 && message.Contains("does not exist") || message.Contains("No user found for steam_id"))
            {
                // {"error":"Not Found","status":404,"message":"No user found for steam_id X"}
                // {"error":"Not Found","status":404,"message":"User X does not exist"}
                LogWarning(message);
                player.Kick(Lang("NotLinked", player.Id, config.TwitchChannel));
                return;
            }

            if (code == 404 && message.Contains("is not following"))
            {
                // {"error":"Not Found","status":404,"message":"X is not following X"}
                Puts(Lang("NotAFollower", player.Id, player.Name));
                player.Kick(Lang("NotFollowing", player.Id, config.TwitchChannel));
                return;
            }

            if (code == 200)
            {
                TwitchUser twitchUser = JsonConvert.DeserializeObject<TwitchUser>(response);
                if (!string.IsNullOrEmpty(twitchUser?.Name))
                {
                    IsFollowing(twitchUser.Name, player);
                }
                else
                {
                    Puts(Lang("IsFollowing", player.Id, player.Name));
                }
            }
        }

        #endregion Twitch Handling

        #region Helpers

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void AddLocalizedCommand(string key, string command)
        {
            foreach (string language in lang.GetLanguages(this))
            {
                Dictionary<string, string> messages = lang.GetMessages(language, this);
                foreach (KeyValuePair<string, string> message in messages.Where(m => m.Key.Equals(key)))
                {
                    if (!string.IsNullOrEmpty(message.Value))
                    {
                        AddCovalenceCommand(message.Value, command);
                    }
                }
            }
        }

        private void Message(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));

        #endregion Helpers
    }
}
