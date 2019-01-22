using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("Babel", "Wulf/lukespragg", "1.0.4")]
    [Description("Plugin API for translating messages using free or paid translation services")]
    public class Babel : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "API key (if required)")]
            public string ApiKey { get; set; } = "";

            [JsonProperty(PropertyName = "Translation service")]
            public string Service { get; set; } = "google";
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

        #region Initialization

        private static readonly Regex GoogleRegex = new Regex(@"\[\[\[""((?:\s|.)+?)"",""(?:\s|.)+?""");
        private static readonly Regex MicrosoftRegex = new Regex("\"(.*)\"");

        private void Init()
        {
            if (string.IsNullOrEmpty(config.ApiKey) && config.Service.ToLower() != "google")
            {
                LogWarning("Invalid API key, please check that it is set and valid");
            }
        }

        #endregion Initialization

        #region Translation API

        /// <summary>
        /// Translates text from one language to another language
        /// </summary>
        /// <param name="text"></param>
        /// <param name="to"></param>
        /// <param name="from"></param>
        /// <param name="callback"></param>
        private void Translate(string text, string to, string from = "auto", Action<string> callback = null)
        {
            string apiKey = config.ApiKey;
            string service = config.Service.ToLower();
            to = to.Contains("-") ? to.Split('-')[0] : to;
            from = from.Contains("-") ? from.Split('-')[0] : from;

            if (string.IsNullOrEmpty(config.ApiKey) && service != "google")
            {
                LogWarning("Invalid API key, please check that it is set and valid");
                return;
            }

            switch (service)
            {
                case "google":
                    {
                        // Reference: https://cloud.google.com/translate/v2/quickstart

                        string url = string.IsNullOrEmpty(apiKey)
                            ? $"https://translate.googleapis.com/translate_a/single?client=gtx&tl={to}&sl={from}&dt=t&q={Uri.EscapeUriString(text)}"
                            : $"https://www.googleapis.com/language/translate/v2?key={apiKey}&target={to}&source={from}&q={Uri.EscapeUriString(text)}";
                        webrequest.Enqueue(url, null, (code, response) =>
                        {
                            if (code != 200 || string.IsNullOrEmpty(response) || response.Equals("[null,null,\"\"]"))
                            {
                                LogWarning($"No valid response received from {service.Humanize()}, try again later");
                                callback?.Invoke(text);
                                return;
                            }

                            Callback(code, response, text, callback);
                        }, this, RequestMethod.POST);
                        break;
                    }

                case "bing":
                case "microsoft":
                    {
                        // Reference: https://www.microsoft.com/en-us/translator/getstarted.aspx
                        // Supported language codes: https://msdn.microsoft.com/en-us/library/hh456380.aspx
                        // TODO: Implement the new access token method for Bing/Microsoft

                        webrequest.Enqueue($"http://api.microsofttranslator.com/V2/Ajax.svc/Detect?appId={apiKey}&text={Uri.EscapeUriString(text)}", null, (c, r) =>
                        {
                            if (string.IsNullOrEmpty(r) || r.Contains("<html>"))
                            {
                                LogWarning($"No valid response received from {service.Humanize()}, try again later");
                                callback?.Invoke(text);
                                return;
                            }

                            if (r.Contains("ArgumentException: Invalid appId"))
                            {
                                LogWarning("Invalid API key, please check that it is valid and try again");
                                callback?.Invoke(text);
                                return;
                            }

                            if (r.Contains("ArgumentOutOfRangeException: 'to' must be a valid language"))
                            {
                                LogWarning($"Invalid language code, please check that it is valid and try again (to: {to}, from: {from})");
                                callback?.Invoke(text);
                                return;
                            }

                            string url = $"http://api.microsofttranslator.com/V2/Ajax.svc/Translate?appId={apiKey}&to={to}&from={r}&text={Uri.EscapeUriString(text)}";
                            webrequest.Enqueue(url, null, (code, response) =>
                            {
                                if (string.IsNullOrEmpty(response) || response.Contains("<html>"))
                                {
                                    LogWarning($"No valid response received from {service.Humanize()}, try again later");
                                    callback?.Invoke(text);
                                    return;
                                }

                                if (response.Contains("ArgumentOutOfRangeException: 'from' must be a valid language"))
                                {
                                    LogWarning($"Invalid language code, please check that it is valid and try again (to: {to}, from: {from})");
                                    callback?.Invoke(text);
                                    return;
                                }

                                Callback(code, response, text, callback);
                            }, this);
                        }, this, RequestMethod.POST);
                        break;
                    }

                case "yandex":
                    {
                        // Reference: https://tech.yandex.com/keys/get/?service=trnsl

                        webrequest.Enqueue($"https://translate.yandex.net/api/v1.5/tr.json/detect?key={apiKey}&hint={from}&text={Uri.EscapeUriString(text)}", null, (c, r) =>
                        {
                            if (string.IsNullOrEmpty(r))
                            {
                                LogWarning($"No valid response received from {service.Humanize()}, try again later");
                                callback?.Invoke(text);
                                return;
                            }

                            if (c == 502 || r.Contains("Invalid parameter: hint"))
                            {
                                LogWarning($"Invalid language code, please check that it is valid and try again (to: {to}, from: {from})");
                                callback?.Invoke(text);
                                return;
                            }

                            from = (string)JObject.Parse(r).GetValue("lang");
                            string url = $"https://translate.yandex.net/api/v1.5/tr.json/translate?key={apiKey}&lang={from}-{to}&text={Uri.EscapeUriString(text)}";
                            webrequest.Enqueue(url, null, (code, response) =>
                            {
                                if (string.IsNullOrEmpty(response))
                                {
                                    LogWarning($"No valid response received from {service.Humanize()}, try again later");
                                    callback?.Invoke(text);
                                    return;
                                }

                                if (c == 501 || c == 502 || r.Contains("The specified translation direction is not supported") || r.Contains("Invalid parameter: lang"))
                                {
                                    LogWarning($"Invalid language code, please check that it is valid and try again (to: {to}, from: {from})");
                                    callback?.Invoke(text);
                                    return;
                                }

                                Callback(code, response, text, callback);
                            }, this, RequestMethod.POST);
                        }, this);
                        break;
                    }

                default:
                    LogWarning($"Translation service '{service}' is not a valid setting");
                    break;
            }
        }

        private void Callback(int code, string response, string text, Action<string> callback = null)
        {
            if (code != 200 || string.IsNullOrEmpty(response))
            {
                LogWarning($"Translation failed! {config.Service.Humanize()} responded with: {response} ({code})");
                return;
            }

            string translated = null;
            string service = config.Service.ToLower();

            if (service == "google" && string.IsNullOrEmpty(config.ApiKey))
            {
                translated = GoogleRegex.Match(response).Groups[1].ToString();
            }
            else if (service == "google" && !string.IsNullOrEmpty(config.ApiKey))
            {
                translated = (string)JObject.Parse(response)["data"]["translations"]["translatedText"];
            }
            else if (service == "microsoft" || service.ToLower() == "bing")
            {
                translated = MicrosoftRegex.Match(response).Groups[1].ToString();
            }
            else if (service == "yandex")
            {
                translated = (string)JObject.Parse(response).GetValue("text").First;
            }
#if DEBUG
            LogWarning($"Original: {text}");
            LogWarning($"Translated: {translated}");
            if (translated == text)
            {
                LogWarning("Translated text is the same as original text");
            }
#endif
            callback?.Invoke(string.IsNullOrEmpty(translated) ? text : Regex.Unescape(translated));
        }

        #endregion Translation API
    }
}
