using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Slack", "Wulf/lukespragg", "0.1.7", ResourceId = 1952)]
    [Description("Plugin API for sending messages and notifications to Slack")]

    class Slack : CovalencePlugin
    {
        #region Initialization

        static readonly DateTime Epoch = new DateTime(1970, 1, 1);
        static readonly Regex Regex = new Regex(@"<avatarIcon><!\[CDATA\[(.*)\]\]></avatarIcon>");

        bool linkNames;
        bool serverInfo;
        string botName;
        string hookUrl;
        string iconUrl;

        protected override void LoadDefaultConfig()
        {
            Config["BotName"] = botName = GetConfig("BotName", "Oxide");
            Config["HookUrl"] = hookUrl = GetConfig("HookUrl", "");
            Config["IconUrl"] = iconUrl = GetConfig("IconUrl", "");
            Config["LinkNames"] = linkNames = GetConfig("LinkNames", true);
            Config["ServerInfo"] = serverInfo = GetConfig("ServerInfo", true);

            SaveConfig();
        }

        void Init() => LoadDefaultConfig();

        #endregion

        #region Basic Message API

        /// <summary>
        /// Sends a basic message to Slack using configured settings
        /// </summary>
        /// <param name="message"></param>
        /// <param name="channel"></param>
        /// <param name="callback"></param>
        void Message(string message, string channel = null, Action<bool> callback = null)
        {
            if (!ErrorChecks(message, false)) return;

            var payload = new SlackMessage
            {
                channel = channel,
                icon_url = iconUrl,
                link_names = linkNames ? "1" : "0",
                mrkdwn = "true",
                username = botName,
                text = message
            };
            PostToSlack(payload, callback);
        }

        #endregion

        #region Player Message API

        /// <summary>
        /// Sends a fancy message to Slack with linked profile, in-game avatar, and server information
        /// </summary>
        /// <param name="message"></param>
        /// <param name="player"></param>
        /// <param name="channel"></param>
        /// <param name="callback"></param>
        void FancyMessage(string message, IPlayer player, string channel = null, Action<bool> callback = null)
        {
            if (!ErrorChecks(message, true, player)) return;

            webrequest.EnqueueGet($"http://steamcommunity.com/profiles/{player.Id}?xml=1", (code, response) =>
            {
                string avatar = null;
                if (response != null && code == 200) avatar = Regex.Match(response).Groups[1].ToString();

                var payload = new SlackMessage
                {
                    attachments = new List<SlackAttachment>
                    {
                        new SlackAttachment
                        {
                            color = "#E68C17", // TODO: Config option
                            author_icon = avatar,
                            author_name = $"{player.Name} ({player.Id})",
                            author_link = $"http://steamcommunity.com/profiles/{player.Id}",
                            fallback = $"{player.Name}: {message}",
                            footer = serverInfo ? $"{server.Name}" : null, // ({server.Address})
                            text = message
                        }
                    },
                    channel = channel,
                    icon_url = iconUrl,
                    username = botName
                };
                PostToSlack(payload, callback);
            }, this);
        }

        /// <summary>
        /// Sends a simple message to Slack as a fake user with in-game avatar
        /// </summary>
        /// <param name="message"></param>
        /// <param name="player"></param>
        /// <param name="channel"></param>
        /// <param name="callback"></param>
        void SimpleMessage(string message, IPlayer player, string channel = null, Action<bool> callback = null)
        {
            if (!ErrorChecks(message, true, player)) return;

            webrequest.EnqueueGet($"http://steamcommunity.com/profiles/{player.Id}?xml=1", (code, response) =>
            {
                string avatar = null;
                if (response != null && code == 200) avatar = Regex.Match(response).Groups[1].ToString();

                var payload = new SlackMessage
                {
                    channel = channel,
                    icon_url = avatar,
                    link_names = linkNames ? "1" : "0",
                    mrkdwn = "true",
                    username = serverInfo ? $"{player.Name} @ {(server.Name).Truncate(20)}" : player.Name,
                    text = message
                };
                PostToSlack(payload, callback);
            }, this);
        }

        #endregion

        #region Ticket Message API

        /// <summary>
        /// Sends a ticket-styled message to Slack with reporter information and position
        /// </summary>
        /// <param name="message"></param>
        /// <param name="player"></param>
        /// <param name="channel"></param>
        /// <param name="callback"></param>
        void TicketMessage(string message, IPlayer player, string channel = null, Action<bool> callback = null)
        {
            if (!ErrorChecks(message, true, player)) return;

            webrequest.EnqueueGet($"http://steamcommunity.com/profiles/{player.Id}?xml=1", (code, response) =>
            {
                string avatar = null;
                if (response != null && code == 200) avatar = Regex.Match(response).Groups[1].ToString();
                var position = player.Position();

                var payload = new SlackMessage
                {
                    attachments = new List<SlackAttachment>
                    {
                        new SlackAttachment
                        {
                            color = "#E68C17", // TODO: Config option
                            author_name = $"{player.Name} ({player.Id})",
                            author_link = $"http://steamcommunity.com/profiles/{player.Id}",
                            fallback = $"{player.Name}: {message}",
                            fields = new List<SlackAttachmentField>
                            {
                                new SlackAttachmentField
                                {
                                    value = $"X: {position.X}, Y: {position.Y}, Z: {position.Z}",
                                    @short = "true"
                                }
                            },
                            footer = serverInfo ? $"{server.Name}" : null, // ({server.Address})
                            footer_icon = "http://i.imgur.com/AIF29RT.png",
                            text = message,
                            thumb_url = avatar,
                            ts = DateTime.UtcNow.Subtract(Epoch).TotalSeconds.ToString()
                        }
                    },
                    channel = channel,
                    icon_url = iconUrl,
                    username = botName
                };
                PostToSlack(payload, callback);
            }, this);
        }

        #endregion

        #region Slack API

        /// <summary>
        /// Posts SlackMessage payload to configured webhook service URL
        /// </summary>
        /// <param name="payload"></param>
        /// <param name="callback"></param>
        void PostToSlack(SlackMessage payload, Action<bool> callback)
        {
            if (payload == null || string.IsNullOrEmpty(hookUrl)) return;
            webrequest.EnqueuePost(hookUrl, JsonConvert.SerializeObject(payload), (code, response) =>
            {
                if (code != 200 || response == null) PrintWarning($"Slack API responded with: {response} ({code})");
                callback?.Invoke(code == 200 || response != null);
            }, this);
        }

        /// <summary>
        /// Required message base class for sending messages to Slack
        /// Reference: https://api.slack.com/methods/chat.postMessage
        /// </summary>
        class SlackMessage
        {
            public string channel { get; set; } // Channel, private group, or IM channel to send the message to
            public string icon_url { get; set; } // URL to an image to use as the icon for the message
            public string link_names { get; set; } // Pass true to find and link channel names and usernames
            public string mrkdwn { get; set; } // Pass true to allow Markdown formatting in the message
            public string text { get; set; } // Optional text of the message to send
            public string username { get; set; } // Name of the bot sending the message
            public List<SlackAttachment> attachments; // Messages can have zero or more attachments
        }

        /// <summary>
        /// Optional attachment class for use with SlackMessage to send attachment messages
        /// Reference: https://api.slack.com/docs/attachments
        /// </summary>
        class SlackAttachment
        {
            public string author_icon { get; set; } // URL for a small 16px by 16px image beside the author_name text
            public string author_link { get; set; } // URL that will hyperlink the author_name text
            public string author_name { get; set; } // Small text used to display the author's name
            public string color { get; set; } // Optional good, warning, danger, or any hex color code (eg. #439FE0)
            public string fallback { get; set; } // A plain-text summary of the attachment
            public string footer { get; set; } // Brief text to help contextualize and identify an attachment
            public string footer_icon { get; set; } // URL for a small 16px by 16px image beside the footer text
            public string image_url { get; set; } // URL to an image to display inside a message attachment
            public string pretext { get; set; } // Optional text that appears above the attachment block
            public string text { get; set; } // Optional text that appears within the attachment
            public string thumb_url { get; set; } // Optional URL to an image to display as a thumbnail in a message attachment
            public string title { get; set; } // Optional, larger, bold text near the top of a message attachment
            public string title_link { get; set; } // Optional URL used to hyperlink the title text
            public string ts { get; set; } // Optional timestamp to be formatted and shown next to the footer text
            public List<SlackAttachmentField> fields; // A table inside the message attachment
        }

        /// <summary>
        /// Optional field class for use with SlackAttachment to send attachment fields
        /// </summary>
        class SlackAttachmentField
        {
            public string title { get; set; } // Shown as a bold heading above the value text
            public string value { get; set; } // Text value of the field (supports standard markup, can be multi-line)
            public string @short { get; set; } // Pass true to allow value to be side-by-side with another value
        }

        #endregion

        #region Error Checking

        bool ErrorChecks(string message, bool callback, IPlayer player = null)
        {
            if (string.IsNullOrEmpty(hookUrl))
            {
                PrintWarning("Invalid HookUrl, please check your config!");
                return false;
            }
            if (string.IsNullOrEmpty(message))
            {
                PrintWarning("No message was specified in API call!");
                return false;
            }
            if (callback && player == null)
            {
                PrintWarning("No valid player was specified in API call!");
                return false;
            }
            return true;
        }

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        #endregion
    }
}