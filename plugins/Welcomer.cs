using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Welcomer", "Tricky", "1.2")]
    [Description("Provides welcome and join/leave messages")]

    public class Welcomer : RustPlugin
    {
        ConfigData configData;

        class ConfigData
        {
            [JsonProperty(PropertyName = "Enable: Welcome Message")]
            public bool WelcomeMessage { get; set; }

            [JsonProperty(PropertyName = "Enable: Join Messages")]
            public bool JoinMessages { get; set; }

            [JsonProperty(PropertyName = "Enable: Leave Messages")]
            public bool LeaveMessages { get; set; }

            [JsonProperty(PropertyName = "Chat Icon (SteamID64)")]
            public string ChatIcon { get; set; }

            [JsonProperty(PropertyName = "Display Steam Avatar of Player - Join/Leave")]
            public bool SteamAvatar { get; set; }

            [JsonProperty(PropertyName = "Hide Admins - Join/Leave")]
            public bool HideAdmins { get; set; }

            [JsonProperty(PropertyName = "Broadcast To Console - Join/Leave")]
            public bool BroadcasttoConsole { get; set; }
        }

        class Response
        {
            [JsonProperty("country")]
            public string Country { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                WelcomeMessage = true,
                JoinMessages = true,
                LeaveMessages = true,
                ChatIcon = "",
                SteamAvatar = true,
                HideAdmins = false,
                BroadcasttoConsole = true
            };
            SaveConfig(config);
        }

        void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);

        void Init()
        {
            LoadConfigVariables();
            LoadDefaultMessages();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Welcome Message"] = "<size=17>Welcome to the <color=#0099CC>Server</color></size>\n----------------------------------------------\n<color=#0099CC>★</color> Type <color=#0099CC>/info</color> for all available commands\n<color=#0099CC>★</color> Read the server rules by typing <color=#0099CC>/info</color>\n<color=#0099CC>★</color> Have fun and respect other players",
                ["Join Message"] = "<color=#37BC61>✔</color> {0} <color=#37BC61>joined the server</color> from <color=#37BC61>{1}</color>",
                ["Join Message Unknown"] = "<color=#37BC61>✔</color> {0} <color=#37BC61>joined the server</color>",
                ["Leave Message"] = "<color=#FF4040>✘</color> {0} <color=#FF4040>left the server</color> ({1})"
            }, this);
        }

        private void OnUserConnected(IPlayer player)
        {
            string message = lang.GetMessage("Join Message", this);
            string messageunknown = lang.GetMessage("Join Message Unknown", this);
            string api = "http://ip-api.com/json/";

            if (configData.JoinMessages)
            {
                if (configData.HideAdmins && player.IsAdmin) return;
                webrequest.Enqueue(api + player.Address, null, (code, response) =>
                {
                    if (code != 200 || response == null)
                    {
                        if (configData.SteamAvatar)
                        {
                            if (configData.BroadcasttoConsole)
                            {
                                rust.BroadcastChat(null, (String.Format(messageunknown, player.Name.ToString())), player.Id);
                                Puts($"{player.Name} joined the server");
                            }
                            else
                            {
                                rust.BroadcastChat(null, (String.Format(messageunknown, player.Name.ToString())), player.Id);
                            }
                        }
                        else
                        {
                            if (configData.BroadcasttoConsole)
                            {
                                if (configData.ChatIcon == String.Empty)
                                {
                                    rust.BroadcastChat(null, (String.Format(messageunknown, player.Name.ToString())));
                                    Puts($"{player.Name} joined the server");
                                }
                                else
                                {
                                    rust.BroadcastChat(null, (String.Format(messageunknown, player.Name.ToString())), configData.ChatIcon);
                                    Puts($"{player.Name} joined the server");
                                }
                            }
                            else
                            {
                                if (configData.ChatIcon == String.Empty)
                                {
                                    rust.BroadcastChat(null, (String.Format(messageunknown, player.Name.ToString())));
                                }
                                else
                                {
                                    rust.BroadcastChat(null, (String.Format(messageunknown, player.Name.ToString())), configData.ChatIcon);
                                }
                            }
                        }
                        return;
                    }

                    string country = JsonConvert.DeserializeObject<Response>(response).Country;
                    if (configData.SteamAvatar)
                    {
                        if (configData.BroadcasttoConsole)
                        {
                            rust.BroadcastChat(null, (String.Format(message, player.Name.ToString(), country)), player.Id);
                            Puts($"{player.Name} joined the server from {country}");
                        }
                        else
                        {
                            rust.BroadcastChat(null, (String.Format(message, player.Name.ToString(), country)), player.Id);
                        }
                    }
                    else
                    {
                        if (configData.BroadcasttoConsole)
                        {
                            if (configData.ChatIcon == String.Empty)
                            {
                                rust.BroadcastChat(null, (String.Format(message, player.Name.ToString(), country)));
                                Puts($"{player.Name} joined the server from {country}");
                            }
                            else
                            {
                                rust.BroadcastChat(null, (String.Format(message, player.Name.ToString(), country)), configData.ChatIcon);
                                Puts($"{player.Name} joined the server from {country}");
                            }
                        }
                        else
                        {
                            if (configData.ChatIcon == String.Empty)
                            {
                                rust.BroadcastChat(null, (String.Format(message, player.Name.ToString(), country)));
                            }
                            else
                            {
                                rust.BroadcastChat(null, (String.Format(message, player.Name.ToString(), country)), configData.ChatIcon);
                            }
                        }
                    }
                }, this);
            }
        }

        private void OnUserDisconnected(IPlayer player, string reason)
        {
            string message = lang.GetMessage("Leave Message", this);

            if (configData.LeaveMessages)
            {
                if (configData.HideAdmins && player.IsAdmin) return;
                if (configData.SteamAvatar)
                {
                    if (configData.BroadcasttoConsole)
                    {
                        rust.BroadcastChat(null, (String.Format(message, player.Name.ToString(), reason)), player.Id);
                        Puts($"{player.Name} left the server ({reason})");
                    }
                    else
                    {
                        rust.BroadcastChat(null, (String.Format(message, player.Name.ToString(), reason)), player.Id);
                    }
                }
                else
                {
                    if (configData.BroadcasttoConsole)
                    {
                        if (configData.ChatIcon == String.Empty)
                        {
                            rust.BroadcastChat(null, (String.Format(message, player.Name.ToString(), reason)));
                            Puts($"{player.Name} left the server ({reason})");
                        }
                        else
                        {
                            rust.BroadcastChat(null, (String.Format(message, player.Name.ToString(), reason)), configData.ChatIcon);
                            Puts($"{player.Name} left the server ({reason})");
                        }
                    }
                    else
                    {
                        if (configData.ChatIcon == String.Empty)
                        {
                            rust.BroadcastChat(null, (String.Format(message, player.Name.ToString(), reason)));
                        }
                        else
                        {
                            rust.BroadcastChat(null, (String.Format(message, player.Name.ToString(), reason)), configData.ChatIcon);
                        }
                    }
                }
            }
        }

        private void OnPlayerInit(BasePlayer player)
        {
            string message = lang.GetMessage("Welcome Message", this);

            if (configData.WelcomeMessage)
            {
                if (configData.ChatIcon == String.Empty)
                {
                    rust.SendChatMessage(player, null, String.Format(message));
                }
                else
                {
                    rust.SendChatMessage(player, null, String.Format(message), configData.ChatIcon);
                }
            }
        }
    }
}
