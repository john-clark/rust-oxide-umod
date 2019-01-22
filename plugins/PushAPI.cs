using System;
using System.Collections.Generic;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("PushAPI", "Wulf/lukespragg", "1.0.1", ResourceId = 705)]
    [Description("API for sending messages via various mobile notification services")]

    class PushAPI : CovalencePlugin
    {
        #region Initialization

        Dictionary<string, string> pushbulletHeaders;

        const string pushalotUrl = "https://pushalot.com/api/sendmessage";
        const string pushbulletUrl = "https://api.pushbullet.com/v2/pushes";
        const string pushoverUrl = "https://api.pushover.net/1/messages.json";

        static string pushalotToken;
        static string pushbulletToken;
        static string pushoverAppKey;
        static string pushoverUserKey;
        static string serviceName;

        protected override void LoadDefaultConfig()
        {
            // Settings
            Config["Pushalot Auth Token (32 characters)"] = pushalotToken = GetConfig("Pushalot Auth Token (32 characters)", "");
            Config["Pushbullet Access Token (34 characters)"] = pushbulletToken = GetConfig("Pushbullet Access Token (34 characters)", "");
            Config["Pushover App Key (30 characters)"] = pushoverAppKey = GetConfig("Pushover App Key (30 characters)", "");
            Config["Pushover User Key (30 characters)"] = pushoverUserKey = GetConfig("Pushover User Key (30 characters)", "");
            Config["Service Name (Ex. pushover)"] = serviceName = GetConfig("Service Name (Ex. pushover)", "");

            SaveConfig();
        }

        void Init()
        {
            LoadDefaultConfig();
            //LoadDefaultMessages();

            pushbulletHeaders = new Dictionary<string, string> { ["Access-Token"] = pushbulletToken };
        }

        #endregion

        #region Push Message

        /// <summary>
        /// Sends a message to the configured service
        /// </summary>
        /// <param name="title"></param>
        /// <param name="message"></param>
        /// <param name="priority"></param>
        /// <param name="callback"></param>
        void PushMessage(string title, string message, string priority = "high", Action<bool> callback = null)
        {
            // TODO: Null and empty handling

            switch (serviceName.ToLower())
            {
                case "pushalot":
                    Pushalot(title, message, priority, callback);
                    break;
                case "pushbullet":
                    Pushbullet(title, message, callback);
                    break;
                case "pushover":
                    Pushover(title, message, priority, "gamelan", callback);
                    break;
                default:
                    PrintError("Configured push service is not valid");
                    break;
            }
        }

        #endregion

        #region Pushalot Service

        /// <summary>
        /// Required base message class for pushing to Pushalot
        /// https://pushalot.com/api#basics
        /// </summary>
        class PushalotMessage
        {
            //public string AuthenticationToken { get; set; } // App authorization token
            public string Title { get; set; } // Title of the notification message
            public string Body { get; set; } // Notification message body to be pushed
            //public string LinkTitle { get; set; } // Title for enclosed link, only used if Link is set
            //public string Link { get; set; } // Enclosed URL link formatted in absolute URI form
            public bool IsImportant { get; set; } // Enable/disable visually marking message as important
            public bool IsQuiet { get; set; } // Enable/disable sending toast notifications to client(s)
            //public string Image { get; set; } // Image thumbnail URL link, recommended size is 72x72 pixels
            //public string Source { get; set; } // Notification source name that is displayed instad of app name
            //public int TimeToLive { get; set; } // Time in minutes until message gets sent to configured client(s)

            public string QueryString() => $"AuthorizationToken={pushalotToken}&Title={Title}&Body={Body}&IsImportant={IsImportant}&IsQuiet={IsQuiet}";
        }

        /// <summary>
        /// Sends a message to the Pushalot service
        /// </summary>
        /// <param name="title"></param>
        /// <param name="message"></param>
        /// <param name="priority"></param>
        /// <param name="callback"></param>
        void Pushalot(string title, string message, string priority = "high", Action<bool> callback = null)
        {
            var important = false;
            var quiet = false;

            switch (priority.ToLower())
            {
                case "high":
                    important = true;
                    break;
                case "quiet":
                    quiet = true;
                    break;
            }

            var payload = new PushalotMessage { Title = title, Body = message, IsImportant = important, IsQuiet = quiet };
            if (ErrorHandling(payload))
            {
                callback?.Invoke(false);
                return;
            }

            WebRequest(pushalotUrl, payload.QueryString(), callback);
        }

        bool ErrorHandling(PushalotMessage payload)
        {
            if (string.IsNullOrEmpty(pushbulletToken) || pushbulletToken.Length != 34)
            {
                LogWarning("Pushbullet access token not set! Please set it and try again");
                return false;
            }

            if (string.IsNullOrEmpty(payload.Title))
            {
                LogWarning("Title not given! Please enter one and try again");
                return false;
            }

            if (string.IsNullOrEmpty(payload.Body))
            {
                LogWarning("Body not given! Please enter one and try again");
                return false;
            }

            return true;
        }

        #endregion

        #region Pushbullet Service

        /// <summary>
        /// Required base note class for pushing to Pushbullet
        /// https://docs.pushbullet.com/#create-push
        /// </summary>
        class PushbulletNote
        {
            public string title { get; set; } // Title of the push, used for all types of pushes
            public string body { get; set; } // Body of the push, used for all types of pushes
            public string type { get; set; } // Type of the push, one of "note", "file", "link"
            //public string device_iden { get; set; } // Specific device identity to push to
            //public string email { get; set; } // Specific email address to push to
            //public string channel_tag { get; set; } // Channel tag of subscribers to push to
            //public string client_iden { get; set; } // Client identity used to push to multiple users

            public string QueryString() => $"&title={title}&body={body}&type={type}";
        }

        /// <summary>
        /// Optional file class for pushing files to Pushbullet
        /// https://docs.pushbullet.com/#push-a-file
        /// </summary>
        class PushbulletFile : PushbulletNote
        {
            public string file_name { get; set; } // File name
            public string file_type { get; set; } // File mime type, ex. "image/jpeg"
            public string file_url { get; set; } // File download URL

            public new string QueryString() => $"&title={title}&body={body}&type={type}&file_name={file_name}&file_type={file_type}&file_url={file_url}";
        }

        /// <summary>
        /// Sends a note to the Pushbullet service
        /// </summary>
        /// <param name="title"></param>
        /// <param name="message"></param>
        /// <param name="callback"></param>
        void Pushbullet(string title, string message, Action<bool> callback = null)
        {
            var payload = new PushbulletNote { title = title, body = message, type = "note" };
            if (!ErrorHandling(payload)) return;

            WebRequest(pushbulletUrl, payload.QueryString(), callback, pushbulletHeaders);
        }

        /// <summary>
        /// Sends a file to the Pushbullet service
        /// </summary>
        /// <param name="title"></param>
        /// <param name="message"></param>
        /// <param name="fileUrl"></param>
        /// <param name="fileName"></param>
        /// <param name="fileType"></param>
        /// <param name="callback"></param>
        void Pushbullet(string title, string message, string fileUrl, string fileName = "Unnamed", string fileType = "text/plain", Action<bool> callback = null)
        {
            // TODO: Try to detect file type from URL?
            // application/octet-stream
            // image/jpeg
            // image/png
            // text/plain

            var payload = new PushbulletFile { title = title, body = message, type = "file", file_url = fileUrl, file_name = fileName, file_type = fileType };
            if (!ErrorHandling(payload))
            {
                callback?.Invoke(false);
                return; // TODO: Need to check title and body too...
            }

            WebRequest(pushbulletUrl, payload.QueryString(), callback, pushbulletHeaders);
        }

        bool ErrorHandling(PushbulletNote payload, Action<bool> callback = null)
        {
            if (string.IsNullOrEmpty(pushbulletToken) || pushbulletToken.Length != 34)
            {
                LogWarning("Pushbullet access token not set! Please set it and try again");
                return false;
            }

            if (string.IsNullOrEmpty(payload.title))
            {
                LogWarning("Title not given! Please enter one and try again");
                return false;
            }

            if (string.IsNullOrEmpty(payload.body))
            {
                LogWarning("Body not given! Please enter one and try again");
                return false;
            }

            return true;
        }

        bool ErrorHandling(PushbulletFile payload, Action<bool> callback = null)
        {
            if (string.IsNullOrEmpty(payload.file_name))
            {
                LogWarning("File name not given! Please enter one and try again");
                return false;
            }

            if (string.IsNullOrEmpty(payload.file_url))
            {
                LogWarning("File URL not given! Please enter one and try again");
                return false;
            }

            return true;
        }

        #endregion

        #region Pushover Service

        /// <summary>
        /// Required base message class for pushing to Pushover
        /// https://pushover.net/api#messages
        /// </summary>
        class PushoverMessage
        {
            //public string token = pushoverAppKey; // The application's API token/key from https://pushover.net/apps
            //public string user = pushoverUserKey; // The user/group key (not e-mail address) for the account
            public string title { get; set; } // Title of the message, otherwise app's name is used
            public string message { get; set; } // Message body to be pushed, basic HTML is supported
            public string priority { get; set; } // -2 for no alert, -1 for quiet, 1 to bypass quiet hours, or 2 to require confirmation
            public string sound { get; set; } // Sound to override default sound choice, see https://pushover.net/api#soundspushoverUserKey
            //public string url { get; set; } // Supplementary URL to show with your message
            //public string url_title { get; set; } // Title for your supplementary URL, otherwise just the URL is shown
            //public long timestamp { get; set; } // Unix timestamp to use for date/time of message instead of API set time
            //public string device { get; set; } // One or more specific device names (comma separated) to send message to
            //public int html = 1; // Enable (1) or disable (0) HTML support

            public string QueryString() => $"token={pushoverAppKey}&user={pushoverUserKey}&title={title}&message={message}&priority={priority}&sound={sound}&html=1";
        }

        /// <summary>
        /// Sends a message to the Pushover service
        /// </summary>
        /// <param name="title"></param>
        /// <param name="message"></param>
        /// <param name="priority"></param>
        /// <param name="sound"></param>
        /// <param name="callback"></param>
        void Pushover(string title, string message, string priority = "high", string sound = "gamelan", Action<bool> callback = null)
        {
            switch (priority.ToLower())
            {
                case "high":
                    priority = "1";
                    break;
                case "low":
                    priority = "0";
                    break;
                case "quiet":
                    priority = "-1";
                    break;
            }

            var payload = new PushoverMessage { title = title, message = message, priority = priority, sound = sound };
            if (!ErrorHandling(payload))
            {
                callback?.Invoke(false);
                return;
            }

            WebRequest(pushoverUrl, payload.QueryString(), callback);
        }

        bool ErrorHandling(PushoverMessage payload, Action<bool> callback = null)
        {
            if (string.IsNullOrEmpty(pushoverAppKey) || pushoverAppKey.Length != 30)
            {
                LogWarning("Pushover application key not set! Please set it and try again");
                return false;
            }

            if (string.IsNullOrEmpty(pushoverUserKey) || pushoverUserKey.Length != 30)
            {
                LogWarning("Pushover user key not set! Please set it and try again");
                return false;
            }

            if (string.IsNullOrEmpty(payload.title))
            {
                LogWarning("Title not given! Please enter one and try again");
                return false;
            }

            if (string.IsNullOrEmpty(payload.message))
            {
                LogWarning("Message not given! Please enter one and try again");
                return false;
            }

            return true;
        }

        #endregion

        #region Web Request

        void WebRequest(string url, string body, Action<bool> callback, Dictionary<string, string> headers = null)
        {
            if (url == null || string.IsNullOrEmpty(body)) return;

            webrequest.EnqueuePost(url, body, (code, response) =>
            {
                if (response == null || code != 200) PrintWarning($"{serviceName.Titleize()} service responded with: {response} ({code})");
                callback?.Invoke(code == 200 || response != null);
            }, this, headers);
        }

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        #endregion
    }
}