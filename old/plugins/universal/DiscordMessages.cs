using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("DiscordMessages", "Slut", "2.0.1")]
    class DiscordMessages : CovalencePlugin
    {
        #region Classes
        public class Data
        {
            public Dictionary<string, PlayerData> Players = new Dictionary<string, PlayerData>();
        }
        public class PlayerData
        {
            public int Reports { get; set; } = 0;
            public DateTime? ReportCooldown { get; set; }
            public DateTime? MessageCooldown { get; set; }
            public bool ReportDisabled { get; set; } = false;
            public PlayerData()
            {
            }
            public PlayerData(DateTime next, CooldownType type)
            {
                switch (type)
                {
                    case CooldownType.ReportCooldown:
                        ReportCooldown = next;
                        break;
                    case CooldownType.MessageCooldown:
                        MessageCooldown = next;
                        break;
                }
            }
        }
        public class FancyMessage
        {
            [JsonProperty("content")]
            private string Content { get; set; }
            [JsonProperty("tts")]
            private bool TextToSpeech { get; set; } = false;
            [JsonProperty("embeds")]
            private EmbedBuilder[] Embeds { get; set; }

            public FancyMessage WithContent(string value)
            {
                Content = value;
                return this;
            }
            public FancyMessage AsTTS(bool value)
            {
                TextToSpeech = value;
                return this;
            }
            public FancyMessage SetEmbed(EmbedBuilder value)
            {
                Embeds = new EmbedBuilder[1]
                {
                    value
                };
                return this;
            }
            public string GetContent()
            {
                return Content;
            }
            public bool IsTTS()
            {
                return TextToSpeech;
            }
            public EmbedBuilder GetEmbed()
            {
                return Embeds[0];
            }
            public string ToJson()
            {
                return JsonConvert.SerializeObject(this, instance.jsonSettings);
            }
        }
        public class EmbedBuilder
        {
            public EmbedBuilder()
            {
                Fields = new List<Field>();
            }
            [JsonProperty("title")]
            private string Title { get; set; }
            [JsonProperty("color")]
            private int Color { get; set; }
            [JsonProperty("fields")]
            private List<Field> Fields { get; set; }
            [JsonProperty("description")]
            private string Description { get; set; }

            public EmbedBuilder WithTitle(string title)
            {
                Title = title;
                return this;
            }
            public EmbedBuilder WithDescription(string description)
            {
                Description = description;
                return this;
            }
            public EmbedBuilder SetColor(int color)
            {
                Color = color;
                return this;
            }
            public EmbedBuilder SetColor(string color)
            {
                Color = ParseColor(color);
                return this;
            }
            public EmbedBuilder AddInlineField(string name, object value)
            {
                Fields.Add(new Field(name, value, true));
                return this;
            }
            public EmbedBuilder AddField(string name, object value)
            {
                Fields.Add(new Field(name, value, false));
                return this;
            }
            public EmbedBuilder AddField(Field field)
            {
                Fields.Add(field);
                return this;
            }
            public int GetColor()
            {
                return Color;
            }
            public string GetTitle()
            {
                return Title;
            }
            public Field[] GetFields()
            {
                return Fields.ToArray();
            }
            private int ParseColor(string input)
            {
                int color;
                if (!int.TryParse(input, out color))
                {
                    color = 3329330;
                }
                return color;
            }
            internal class Field
            {
                [JsonProperty("name")]
                public string Name { get; set; }
                [JsonProperty("value")]
                public object Value { get; set; }
                [JsonProperty("inline")]
                public bool Inline { get; set; }
                public Field(string name, object value, bool inline)
                {
                    Name = name;
                    Value = value;
                    Inline = inline;
                }
            }
        }
        public interface IResponse
        {
            int Code { get; set; }
            string Message { get; set; }
        }
        public class BaseResponse : IResponse
        {
            public RateLimitResponse RateLimit { get; protected set; }

            public int Code { get; set; }
            public string Message { get; set; }

            public bool IsRatelimit => Code == 429;
            public bool IsOk => Code == 200 | Code == 204;
            public bool IsBad => !IsRatelimit && !IsOk;

            public void SetRateLimit()
            {
                RateLimit = JsonConvert.DeserializeObject<RateLimitResponse>(Message);
            }

        }
        public class Request
        {
            public Action<BaseResponse> Response { get; internal set; }
            static readonly RateLimitHandler _handler = new RateLimitHandler();
            private Plugin _plugin { get; set; }

            public Request(string url, FancyMessage message, Action<BaseResponse> response = null, Plugin plugin = null)
            {
                Url = url;
                Payload = message.ToJson();
                Response = response;
                _plugin = plugin;
                Id = GetNextId();
                Send();
            }
            public Request(string url, FancyMessage message, Plugin plugin = null)
            {
                Url = url;
                Payload = message.ToJson();
                _plugin = plugin;
                Id = GetNextId();
                Send();
            }
            public int Id { get; internal set; }
            public string Url { get; internal set; }
            public string Payload { get; internal set; }

            public float NextTime { get; internal set; }

            public Request SetNextTime(float time)
            {
                NextTime = time;
                return this;
            }

            static int _increment = 1;
            static int GetNextId()
            {
                int i = _increment;
                _increment++;
                return i;
            }
            public void Send()
            {
                instance.webrequest.Enqueue(Url, Payload, (code, response) =>
                {
                    BaseResponse _response = new BaseResponse()
                    {
                        Message = response,
                        Code = code
                    };
                    if (_response.IsRatelimit)
                    {
                        _response.SetRateLimit();
                        _handler.AddMessage(_response.RateLimit, this);
                    }
                    if (_response.IsBad)
                    {
                        instance.PrintWarning("Failed! Discord responded with code: {0}. Plugin: {1}\n{2}", code, _plugin != null ? _plugin.Name : "Unknown Plugin", _response.Message);
                    }
                    try
                    {
                        Response?.Invoke(_response);
                    }
                    catch (Exception ex)
                    {
                        Interface.Oxide.LogException("[DiscordMessages] Request callback raised an exception!", ex);
                    }
                }, instance, Core.Libraries.RequestMethod.POST);
            }
        }

        public class RateLimitHandler
        {
            private Timer RateTimer { get; set; }
            private List<Request> Messages { get; set; } = new List<Request>();
            public RateLimitHandler AddMessage(RateLimitResponse response, Request request)
            {
                request.SetNextTime(response.RetryAfter / 1000);
                Messages.Add(request);
                RateTimerHandler();
                return this;
            }
            private void RateTimerHandler()
            {
                if (Messages.Count == 0)
                {
                    RateTimer.Destroy();
                }
                Request request = GetNextMessage();
                RateTimer = instance.timer.Once(request.NextTime, () => DoRateRequest(request));
            }
            private Request GetNextMessage()
            {
                return Messages.First();
            }
            private void DoRateRequest(Request request)
            {
                request.Send();
                Messages.RemoveAt(0);
            }
        }
        public class RateLimitResponse : BaseResponse
        {
            [JsonProperty("global")]
            public bool Global { get; set; }
            [JsonProperty("retry_after")]
            public int RetryAfter { get; set; }
        }

        public enum CooldownType { ReportCooldown, MessageCooldown }

        public enum FeatureType
        {
            BAN,
            MESSAGE,
            REPORT,
            PLAYER_CHAT,
            MUTE
        }
        #endregion

        #region Configuration

        Configuration config;

        public class Configuration
        {
            public General GeneralSettings { get; set; } = new General();

            public Ban BanSettings { get; set; } = new Ban();

            public Report ReportSettings { get; set; } = new Report();

            public Message MessageSettings { get; set; } = new Message();
            public Chat ChatSettings { get; set; } = new Chat();
            public Mute MuteSettings { get; set; } = new Mute();

            [JsonIgnore]
            public Dictionary<FeatureType, WebhookObject> FeatureTypes { get; set; }
            public class General
            {
                public bool Announce { get; set; } = true;
            }
            public class Ban : EmbedObject
            {
            }
            public class Message : EmbedObject
            {
                public bool LogToConsole { get; set; } = true;
                public bool SuggestAlias { get; set; } = false;
                public string Alert { get; set; } = "";
                public int Cooldown { get; set; } = 30;

            }
            public class Report : EmbedObject
            {
                public bool LogToConsole { get; set; } = true;
                public string Alert { get; set; } = "";
                public int Cooldown { get; set; } = 30;
            }
            public class Chat : WebhookObject
            {
                public bool TextToSpeech { get; set; } = false;
            }
            public class Mute : EmbedObject
            {

            }
            public class EmbedObject : WebhookObject
            {
                public string Color { get; set; } = "Lime";
            }
            public class WebhookObject
            {
                public bool Enabled { get; set; } = true;
                public string WebhookUrl { get; set; } = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
            }

            public static Configuration Defaults()
            {
                return new Configuration();
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();
            config.FeatureTypes = new Dictionary<FeatureType, Configuration.WebhookObject>
            {
                [FeatureType.BAN] = config.BanSettings,
                [FeatureType.REPORT] = config.ReportSettings,
                [FeatureType.MESSAGE] = config.MessageSettings,
                [FeatureType.MUTE] = config.MuteSettings,
                [FeatureType.PLAYER_CHAT] = config.ChatSettings,
            };
        }
        protected override void SaveConfig()
        {
            base.SaveConfig();
            Config.WriteObject(config);
        }
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating new config!");
            config = Configuration.Defaults();
        }
        public T GetFeatureConfig<T>(FeatureType type) where T : Configuration.WebhookObject
        {
            return (T)config.FeatureTypes[type];
        }

        #endregion

        #region Variables

        Data data;

        [PluginReference] private readonly Plugin BetterChatMute;

        public static DiscordMessages instance;
        readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings();

        #endregion

        #region Hooks / Load


        private void Init()
        {
            instance = this;
            jsonSettings.NullValueHandling = NullValueHandling.Ignore;
            LoadData();
            RegisterPermissions();
            if (!config.FeatureTypes.Any(x => x.Value.Enabled))
            {
                PrintWarning("All functions are disabled. Please enable at least one.");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }
            foreach (KeyValuePair<FeatureType, Configuration.WebhookObject> feature in config.FeatureTypes)
            {
                Configuration.WebhookObject value = feature.Value;
                if (value.Enabled && (value.WebhookUrl == null || value.WebhookUrl == "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks"))
                {
                    value.Enabled = false;
                    PrintWarning(string.Format("{0} was enabled however the webhook is incorrect.", feature.Key));
                }
            }
            RegisterCommands();
            CheckHooks();
        }
        private void CheckHooks()
        {
            if (!GetFeatureConfig<Configuration.Chat>(FeatureType.PLAYER_CHAT).Enabled)
            {
                Unsubscribe(nameof(OnUserChat));
            }
            if (!GetFeatureConfig<Configuration.Mute>(FeatureType.MUTE).Enabled)
            {
                Unsubscribe(nameof(OnBetterChatMuted));
                Unsubscribe(nameof(OnBetterChatTimeMuted));
            }
        }
        private void Unload()
        {
            SaveData();
        }

        private void OnServerSave()
        {
            SaveData();
        }

        private void RegisterCommands()
        {
            if (GetFeatureConfig<Configuration.Report>(FeatureType.REPORT).Enabled)
            {
                AddCovalenceCommand("report", "ReportCommand", ReportPermission);
                AddCovalenceCommand(new string[] { "reportadmin", "ra" }, "ReportAdminCommand", AdminPermission);
            }
            if (GetFeatureConfig<Configuration.Ban>(FeatureType.BAN).Enabled)
            {
                AddCovalenceCommand("ban", "BanCommand", BanPermission);
            }
            Configuration.Message messageConfig = GetFeatureConfig<Configuration.Message>(FeatureType.MESSAGE);
            if (messageConfig.Enabled)
            {
                AddCovalenceCommand(messageConfig.SuggestAlias ? new string[] { "message", "suggest" } : new string[] { "message" }, "MessageCommand", MessagePermission);
            }
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ReportSyntax"] = "Syntax error. Please use /report \"name/id\" \"reason\"",
                ["BanSyntax"] = "Syntax error. Please use /ban \"name/id\" \"reason\"",
                ["MessageSyntax"] = "Syntax error. Please use /message \"your message\"",
                ["Multiple"] = "Multiple players found:\n{0}",
                ["BanMessage"] = "{0} was banned for {1}",
                ["ReportSent"] = "Your report has been sent!",
                ["MessageSent"] = "Your message has been sent!",
                ["NotFound"] = "Unable to find player {0}",
                ["NoReports"] = "{0} has not been reported yet!",
                ["ReportDisallowed"] = "You have been blacklisted from reporting players.",
                ["ReportAccessChanged"] = "Report feature for {0} is now {1}",
                ["ReportReset"] = "You have reset the report count for {0}",
                ["Cooldown"] = "You must wait {0} seconds to use this command again.",
                ["AlreadyBanned"] = "{0} is already banned!",
                ["NoPermission"] = "You do not have permision for this command!",
                ["Disabled"] = "This feature is currently disabled.",
                ["Failed"] = "Your report failed to send, contact the server owner.",
                ["ToSelf"] = "You cannot perform this action on yourself.",
                ["ReportTooShort"] = "Your report was too short! Please be more descriptive.",
                ["PlayerChatFormat"] = "**{0}:** {1}",
                ["BanPrefix"] = "Banned: {0}",
                ["Embed_ReportPlayer"] = "Reporter",
                ["Embed_ReportTarget"] = "Reported",
                ["Embed_ReportCount"] = "Times Reported",
                ["Embed_ReportReason"] = "Reason",
                ["Embed_Online"] = "Online",
                ["Embed_Offline"] = "Offline",
                ["Embed_ReportStatus"] = "Status",
                ["Embed_ReportTitle"] = "Player Report",
                ["Embed_MuteTitle"] = "Player Muted",
                ["Embed_MuteTarget"] = "Player",
                ["Embed_MutePlayer"] = "Muted by",
                ["Embed_BanPlayer"] = "Banned by",
                ["Embed_BanTarget"] = "Player",
                ["Embed_BanReason"] = "Reason",
                ["Embed_BanTitle"] = "Player Ban",
                ["Embed_MessageTitle"] = "Player Message",
                ["Embed_MessagePlayer"] = "Player",
                ["Embed_MessageMessage"] = "Message",
                ["Embed_MuteTime"] = "Time",
                ["Embed_MuteReason"] = "Reason"
            }, this, "en");
        }
        private object OnUserChat(IPlayer player, string message)
        {
            if (GetFeatureConfig<Configuration.Chat>(FeatureType.PLAYER_CHAT).Enabled)
            {
                HandleMessage(player, message);
            }

            return null;
        }
        #endregion

        #region Permissions
        const string BanPermission = "discordmessages.ban";
        const string ReportPermission = "discordmessages.report";
        const string MessagePermission = "discordmessages.message";
        const string AdminPermission = "discordmessages.admin";

        private void RegisterPermissions()
        {
            permission.RegisterPermission(BanPermission, this);
            permission.RegisterPermission(ReportPermission, this);
            permission.RegisterPermission(MessagePermission, this);
            permission.RegisterPermission(AdminPermission, this);
        }

        #endregion

        #region API
        private void API_SendFancyMessage(string webhookURL, string embedName, int embedColor, string json, string content = null, Plugin plugin = null)
        {
            EmbedBuilder builder = new EmbedBuilder()
                .WithTitle(embedName)
                .SetColor(embedColor);
            foreach (EmbedBuilder.Field field in JsonConvert.DeserializeObject<EmbedBuilder.Field[]>(json))
            {
                builder.AddField(field);
            }
            FancyMessage payload = new FancyMessage()
                .SetEmbed(builder)
                .WithContent(content);
            Request request = new Request(webhookURL, payload, plugin);
        }
        private void API_SendFancyMessage(string webhookURL, string embedName, string json, string content = null, int embedColor = 3329330, Plugin plugin = null)
        {
            API_SendFancyMessage(webhookURL, embedName, embedColor, json, content, plugin);
        }

        private void API_SendTextMessage(string webhookURL, string content, bool tts = false, Plugin plugin = null)
        {
            FancyMessage payload = new FancyMessage().AsTTS(tts).WithContent(content);
            Request request = new Request(webhookURL, payload, plugin);
        }
        #endregion


        #region PlayerChat
        private void HandleMessage(IPlayer player, string playerMessage)
        {
            bool? muted = BetterChatMute?.Call<bool>("API_IsMuted", player);
            if (muted.HasValue && muted.Value)
            {
                return;
            }
            if (!player.HasPermission(AdminPermission))
            {
                playerMessage = playerMessage.Replace("@everyone", "@ everyone").Replace("@here", "@ here");
            }
            Configuration.Chat chatConfig = GetFeatureConfig<Configuration.Chat>(FeatureType.PLAYER_CHAT);
            string discordMessage = $"[{DateTime.Now.ToShortTimeString()}] {GetLang("PlayerChatFormat", null, player.Name, playerMessage)}";
            FancyMessage message = new FancyMessage().WithContent(discordMessage).AsTTS(chatConfig.TextToSpeech);
            Request request = new Request(chatConfig.WebhookUrl, message, this);
        }
        #endregion

        #region Message

        private void MessageCommand(IPlayer player, string command, string[] args)
        {
            if (args.Length < 1)
            {
                SendMessage(player, GetLang("MessageSyntax", player.Id));
                return;
            }
            Configuration.Message messageConfig = GetFeatureConfig<Configuration.Message>(FeatureType.MESSAGE);
            if (OnCooldown(player, CooldownType.MessageCooldown))
            {
                SendMessage(player, GetLang("Cooldown", player.Id, (data.Players[player.Id].MessageCooldown.Value.AddSeconds(messageConfig.Cooldown) - DateTime.UtcNow).Seconds));
                return;
            }
            string _message = string.Join(" ", args.ToArray());
            EmbedBuilder builder = new EmbedBuilder()
                .WithTitle(GetLang("Embed_MessageTitle"))
                .AddInlineField(GetLang("Embed_MessagePlayer"), $"[{ player.Name }](https://steamcommunity.com/profiles/{player.Id})")
                .AddField(GetLang("Embed_MessageMessage"), _message)
                .SetColor(messageConfig.Color);
            FancyMessage payload = new FancyMessage()
                .WithContent(messageConfig.Alert)
                .SetEmbed(builder);
            Request request = new Request(messageConfig.WebhookUrl, payload, response =>
            {
                if (response.IsOk)
                {
                    SendMessage(player, GetLang("MessageSent", player.Id));
                    if (data.Players.ContainsKey(player.Id))
                    {
                        data.Players[player.Id].MessageCooldown = DateTime.UtcNow;
                    }
                    else
                    {
                        data.Players.Add(player.Id, new PlayerData());
                        data.Players[player.Id].MessageCooldown = DateTime.UtcNow;
                    }
                    if (messageConfig.LogToConsole)
                    {
                        Puts($"MESSAGE ({player.Name}/{player.Id}) : {_message}");
                    }
                }
                else if (response.IsBad)
                {
                    SendMessage(player, GetLang("MessageNotSent", player.Id));
                }
            }, this);
        }

        #endregion
        #region Report
        private void ReportAdminCommand(IPlayer player, string command, string[] args)
        {
            IPlayer target = GetPlayer(args[1], player, false);
            if (target == null)
            {
                player.Reply(GetLang("NotFound", player.Id, args[1]));
                return;
            }
            switch (args[0])
            {
                case "enable":
                    if (data.Players.ContainsKey(target.Id))
                    {
                        data.Players[target.Id].ReportDisabled = false;
                    }
                    player.Reply(GetLang("ReportAccessChanged", player.Id, target.Name, "enabled"));
                    return;
                case "disable":
                    if (data.Players.ContainsKey(target.Id))
                    {
                        data.Players[target.Id].ReportDisabled = true;
                    }
                    else
                    {
                        data.Players.Add(target.Id, new PlayerData { ReportDisabled = true });
                    }
                    player.Reply(GetLang("ReportAccessChanged", player.Id, target.Name, "disabled"));
                    return;
                case "reset":
                    if (data.Players.ContainsKey(target.Id))
                    {
                        if (data.Players[target.Id].Reports != 0)
                        {
                            data.Players[target.Id].Reports = 0;
                            player.Reply(GetLang("ReportReset", player.Id, target.Name));
                            return;
                        }
                    }
                    player.Reply(GetLang("NoReports", player.Id, target.Name));
                    return;
            }

        }
        private void ReportCommand(IPlayer player, string command, string[] args)
        {
            if ((player.Name == "Server Console") | !player.IsConnected)
            {
                return;
            }
            if (data.Players.ContainsKey(player.Id))
            {
                if (data.Players[player.Id].ReportDisabled)
                {
                    SendMessage(player, GetLang("ReportDisallowed", player.Id));
                    return;
                }
            }
            else
            {
                data.Players.Add(player.Id, new PlayerData());
            }
            if (args.Length < 2)
            {
                SendMessage(player, GetLang("ReportSyntax", player.Id));
                return;
            }
            Configuration.Report reportConfig = GetFeatureConfig<Configuration.Report>(FeatureType.REPORT);
            if (OnCooldown(player, CooldownType.ReportCooldown))
            {
                SendMessage(player, GetLang("Cooldown", player.Id, (data.Players[player.Id].ReportCooldown.Value.AddSeconds(reportConfig.Cooldown) - DateTime.UtcNow).Seconds));
                return;
            }
            IPlayer target = GetPlayer(args[0], player, true);
            if (target == null)
            {
                return;
            }

            List<string> reason = args.Skip(1).ToList();
            if (player.Id == target.Id)
            {
                SendMessage(player, GetLang("ToSelf", player.Id));
                return;
            }
            string[] targetName = target.Name.Split(' ');
            if (targetName.Length > 1)
            {
                for (int x = 0; x < targetName.Length - 1; x++)
                {
                    if (reason[x].Equals(targetName[x + 1]))
                    {
                        reason.RemoveAt(x);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            if (reason.Count < 1)
            {
                SendMessage(player, GetLang("ReportTooShort", player.Id));
                return;
            }
            string _reason = string.Join(" ", reason.ToArray());
            if (data.Players.ContainsKey(target.Id))
            {
                data.Players[target.Id].Reports++;
            }
            else
            {
                data.Players.Add(target.Id, new PlayerData());
                data.Players[target.Id].Reports++;
            }
            string status = target.IsConnected ? lang.GetMessage("Online", null) : lang.GetMessage("Offline", null);
            EmbedBuilder builder = new EmbedBuilder()
                .WithTitle(GetLang("Embed_MessageTitle"))
                .SetColor(reportConfig.Color)
                .AddInlineField(GetLang("Embed_ReportTarget"), $"[{target.Name}](https://steamcommunity.com/profiles/{target.Id})")
                .AddInlineField(GetLang("Embed_ReportPlayer"), $"[{player.Name}](https://steamcommunity.com/profiles/{player.Id})")
                .AddInlineField(GetLang("Embed_ReportStatus"), status)
                .AddField(GetLang("Embed_ReportReason"), _reason)
                .AddInlineField(GetLang("Embed_ReportCount"), data.Players[target.Id].Reports.ToString());
            FancyMessage payload = new FancyMessage()
                .WithContent(reportConfig.Alert)
                .SetEmbed(builder);
            Request request = new Request(reportConfig.WebhookUrl, payload, response =>
            {
                if (response.IsOk)
                {
                    SendMessage(player, GetLang("ReportSent", player.Id));
                    if (data.Players.ContainsKey(player.Id))
                    {
                        data.Players[player.Id].ReportCooldown = DateTime.UtcNow;
                    }
                    else
                    {
                        data.Players.Add(player.Id, new PlayerData());
                        data.Players[player.Id].ReportCooldown = DateTime.UtcNow;
                    }
                    if (reportConfig.LogToConsole)
                    {
                        Puts($"REPORT ({player.Name}/{player.Id}) -> ({target.Name}/{target.Id}): {_reason}");
                    }
                }
                else if (response.IsBad)
                {
                    SendMessage(player, GetLang("ReportNotSent", player.Id));
                }
            }, this);
        }

        #endregion

        #region Mutes

        string FormatTime(TimeSpan time)
        {
            return $"{(time.Days == 0 ? string.Empty : $"{time.Days} day(s)")}{(time.Days != 0 && time.Hours != 0 ? $", " : string.Empty)}{(time.Hours == 0 ? string.Empty : $"{time.Hours} hour(s)")}{(time.Hours != 0 && time.Minutes != 0 ? $", " : string.Empty)}{(time.Minutes == 0 ? string.Empty : $"{time.Minutes} minute(s)")}{(time.Minutes != 0 && time.Seconds != 0 ? $", " : string.Empty)}{(time.Seconds == 0 ? string.Empty : $"{time.Seconds} second(s)")}";
        }

        private void OnBetterChatTimeMuted(IPlayer target, IPlayer player, TimeSpan expireDate, string reason)
        {
            SendMute(target, player, expireDate, true, reason);
        }

        private void OnBetterChatMuted(IPlayer target, IPlayer player, string reason)
        {
            SendMute(target, player, TimeSpan.Zero, false, reason);
        }

        private void SendMute(IPlayer target, IPlayer player, TimeSpan expireDate, bool timed, string reason)
        {
            if (target == null || player == null)
            {
                return;
            }
            Configuration.Mute muteConfig = GetFeatureConfig<Configuration.Mute>(FeatureType.MUTE);
            EmbedBuilder builder = new EmbedBuilder()
                .WithTitle(GetLang("Embed_MuteTitle"))
                .AddInlineField(GetLang("Embed_MuteTarget"), $"[{target.Name}](https://steamcommunity.com/profiles/{target.Id})")
                .AddInlineField(GetLang("Embed_MutePlayer"), !player.Id.Equals("server_console") ? $"[{player.Name}](https://steamcommunity.com/profiles/{player.Id})" : player.Name)
                .AddInlineField(GetLang("Embed_MuteTime"), timed ? FormatTime(expireDate) : "Permanent")
                .SetColor(muteConfig.Color);
            if (!string.IsNullOrEmpty(reason))
            {
                builder.AddField(GetLang("Embed_MuteReason"), reason);
            }
            FancyMessage message = new FancyMessage()
                .SetEmbed(builder);
            Request request = new Request(muteConfig.WebhookUrl, message, this);
        }
        #endregion

        #region Bans


        private bool BanFromCommand = false;
        private void BanCommand(IPlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                SendMessage(player, GetLang("BanSyntax", player.Id));
                return;
            }
            string reason = args.Length == 1 ? "Banned" : string.Join(" ", args.Skip(1).ToArray());
            IPlayer target = GetPlayer(args[0], player, false);
            if (target != null)
            {
                if (target == player)
                {
                    SendMessage(player, GetLang("ToSelf", player.Id));
                    return;
                }
                ExecuteBan(target, player, reason);
            }
            else
            {
                player.Reply(GetLang("NotFound", player.Id, args[0]));
            }
        }

        private void ExecuteBan(IPlayer target, IPlayer player, string reason)
        {
            if (target.IsBanned)
            {
                SendMessage(player, GetLang("AlreadyBanned", player.Id, target.Name));
                return;
            }
            BanFromCommand = true;
            OnUserBanned(target.Name, target.Id, target.Address, reason, player);
            target.Ban(GetLang("BanPrefix", target.Id) + reason);
            if (config.GeneralSettings.Announce)
            {
                server.Broadcast(GetLang("BanMessage", null, target.Name, reason));
            }
        }

        void OnUserBanned(string name, string bannedId, string address, string reason, IPlayer source = null)
        {
            Configuration.Ban banConfig = GetFeatureConfig<Configuration.Ban>(FeatureType.BAN);
            if (!banConfig.Enabled)
            {
                return;
            }
            EmbedBuilder builder = new EmbedBuilder()
                .WithTitle(GetLang("Embed_BanTitle"))
                .AddInlineField(GetLang("Embed_BanTarget"), $"[{name}](https://steamcommunity.com/profiles/{bannedId})");
            if (source == null)
            {
                if (BanFromCommand)
                {
                    return;
                }
            }
            else
            {
                builder.AddInlineField(GetLang("Embed_BanPlayer"), !source.Id.Equals("server_console") ? $"[{source.Name}](https://steamcommunity.com/profiles/{source.Id})" : source.Name);
            }
            builder.AddField(GetLang("Embed_BanReason"), reason)
                .SetColor(banConfig.Color);
            FancyMessage message = new FancyMessage()
                .SetEmbed(builder);
            Request request = new Request(banConfig.WebhookUrl, message, this);
            BanFromCommand = false;
        }

        #endregion

        #region Helpers

        private string GetLang(string key, string id = null, params object[] args)
        {
            if (args.Length > 0)
            {
                return string.Format(lang.GetMessage(key, this, id), args);
            }
            else
            {
                return lang.GetMessage(key, this, id);
            }
        }

        private void SendMessage(IPlayer player, string message)
        {
            player.Reply(message);
        }

        private bool OnCooldown(IPlayer player, CooldownType type)
        {
            if (data.Players.ContainsKey(player.Id))
            {
                PlayerData playerData = data.Players[player.Id];
                switch (type)
                {
                    case CooldownType.MessageCooldown:
                        {
                            if (playerData.MessageCooldown.HasValue)
                            {
                                return playerData.MessageCooldown.Value.AddSeconds(GetFeatureConfig<Configuration.Message>(FeatureType.MESSAGE).Cooldown) > DateTime.UtcNow;
                            }
                            break;
                        }
                    case CooldownType.ReportCooldown:
                        {
                            if (playerData.ReportCooldown.HasValue)
                            {
                                return playerData.ReportCooldown.Value.AddSeconds(GetFeatureConfig<Configuration.Report>(FeatureType.REPORT).Cooldown) > DateTime.UtcNow;
                            }
                            break;
                        }
                }
            }
            return false;
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, data);
        }

        private void LoadData()
        {
            data = Interface.Oxide.DataFileSystem.ReadObject<Data>(Name);
        }

        private IPlayer GetPlayer(string nameOrID, IPlayer player, bool sendError)
        {
            if (nameOrID.IsSteamId())
            {
                IPlayer result = players.All.ToList().Find(p => p.Id == nameOrID);

                if (result == null)
                {
                    return null;
                }

                return result;
            }

            List<IPlayer> foundPlayers = new List<IPlayer>();

            foreach (IPlayer current in players.Connected)
            {
                if (current.Name.ToLower() == nameOrID.ToLower())
                {
                    return current;
                }

                if (current.Name.ToLower().Contains(nameOrID.ToLower()))
                {
                    foundPlayers.Add(current);
                }
            }
            if (foundPlayers.Count == 0)
            {
                foreach (IPlayer all in players.All)
                {
                    if (all.Name.ToLower() == nameOrID.ToLower())
                    {
                        return all;
                    }

                    if (all.Name.ToLower().Contains(nameOrID.ToLower()))
                    {
                        foundPlayers.Add(all);
                    }
                }
            }

            switch (foundPlayers.Count)
            {
                case 0:
                    if (!nameOrID.IsSteamId())
                    {
                        if (sendError)
                        {
                            SendMessage(player, GetLang("NotFound", player.Id, nameOrID));
                        }
                    }

                    break;

                case 1:
                    return foundPlayers[0];

                default:
                    string[] names = (from current in foundPlayers select current.Name).ToArray();
                    SendMessage(player, GetLang("Multiple", player.Id, string.Join(", ", names)));
                    break;
            }

            return null;
        }
        #endregion
    }
}