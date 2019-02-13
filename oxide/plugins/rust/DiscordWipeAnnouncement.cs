using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Discord Wipe Announcement", "MisterPixie", "1.1.0")]
    [Description("Post to discord when server wipes")]
    class DiscordWipeAnnouncement : RustPlugin
    {
        [PluginReference] Plugin DiscordMessages;

        #region Hooks
        private void Init()
        {
            LoadVariables();
            if (configData.Enable == false)
            {
                Unsubscribe("OnNewSave");
            }
        }

        [ChatCommand("dwatest")]
        void DisCommand(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin)
            {
                if(configData.UsingEmbedText == false)
                {
                    SendDiscordMessage();
                }
                else
                {
                    SendEmbedDiscordMessage();
                }
            }
        }

        void SendDiscordMessage()
        {
            string seed = ConVar.Server.seed.ToString();
            string hostname = ConVar.Server.hostname;
            string ip = covalence.Server.Address.ToString();
            string port = covalence.Server.Port.ToString();
            string worldsize = ConVar.Server.worldsize.ToString();

            string Message = string.Join("\n", configData.WipeMessage.ToArray());

            DiscordMessages?.Call("API_SendTextMessage", configData.WebhookURL, string.Format(Message, hostname, ip, port, seed, worldsize));
        }

        public class EmbedFieldList
        {
            public string name { get; set;}
            public string value { get; set; }
            public bool inline { get; set; }
        }

        void SendEmbedDiscordMessage()
        {
            string seed = ConVar.Server.seed.ToString();
            string hostname = ConVar.Server.hostname;
            string ip = covalence.Server.Address.ToString();
            string port = covalence.Server.Port.ToString();
            string worldsize = ConVar.Server.worldsize.ToString();

            List<EmbedFieldList> fields = new List<EmbedFieldList>();

            foreach (var i in configData.EmbedText)
            {
                fields.Add(new EmbedFieldList()
                {
                    name = string.Format(i.Value.name, hostname, ip, port, seed, worldsize),
                    inline = i.Value.inline,
                    value = string.Format(i.Value.value, hostname, ip, port, seed, worldsize)
                });
            }

            var fieldsObject = fields.Cast<object>().ToArray();

            string json = JsonConvert.SerializeObject(fieldsObject);

            DiscordMessages?.Call("API_SendFancyMessage", configData.WebhookURL, string.Format(configData.EmbedTextTitle, hostname, ip, port, seed, worldsize), configData.EmbedColor, json);

        }

        void OnNewSave(string filename)
        {
            if (configData.UsingEmbedText == false)
            {
                SendDiscordMessage();
            }
            else
            {
                SendEmbedDiscordMessage();
            }
        }
        #endregion


        #region Config
        public class EmbedList
        {
            public string name;
            public string value;
            public bool inline;
        }

        private ConfigData configData;
        private class ConfigData
        {
            public string WebhookURL;
            public bool Enable;
            public bool UsingEmbedText;
            public string EmbedTextTitle;
            public int EmbedColor;
            public List<string> WipeMessage;
            public Dictionary<int, EmbedList> EmbedText;
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                WebhookURL = "Webhook URL Here",
                Enable = false,
                EmbedColor = 9044189,
                UsingEmbedText = false,
                EmbedTextTitle = "Your Embeded Title",
                WipeMessage = new List<string>
                {
                    "First Line",
                    "Second Line",
                    "Third Line..."
                },
                EmbedText = new Dictionary<int, EmbedList>
                {
                    [1] = new EmbedList()
                    {
                        name = "First Embed Name",
                        value = "First Value",
                        inline = false
                    },
                    [2] = new EmbedList()
                    {
                        name = "Second Embed Name",
                        value = "Second Value",
                        inline = false
                    }
                }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion
    }
}