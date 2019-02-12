using System;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Server Chat", "Tricky", "1.1.1")]
    [Description("Replaces the default server chat icon and prefix")]

    public class ServerChat : RustPlugin
    {
        ConfigData configData;

        class ConfigData
        {
            [JsonProperty(PropertyName = "Chat Icon (SteamID64)")]
            public string SteamID { get; set; }

            [JsonProperty(PropertyName = "Title")]
            public string Title { get; set; }

            [JsonProperty(PropertyName = "Title Color")]
            public string TitleColor { get; set; }

            [JsonProperty(PropertyName = "Title Size")]
            public int TitleSize { get; set; }

            [JsonProperty(PropertyName = "Message Color")]
            public string MessageColor { get; set; }

            [JsonProperty(PropertyName = "Message Size")]
            public int MessageSize { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                SteamID = "",
                Title = "Server",
                TitleColor = "#55aaff",
                TitleSize = 15,
                MessageColor = "white",
                MessageSize = 15
            };
            SaveConfig(config);
        }

        void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();

        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);

        void Init()
        {
            LoadConfigVariables();
        }

        object OnServerMessage(string message, string name, string color, ulong id)
        {
            string Prefix = $"<size={configData.TitleSize}><color={configData.TitleColor}>{configData.Title}</color></size>";
            string Message = $"<size={configData.MessageSize}><color={configData.MessageColor}>{message}</color></size>";

            if(configData.Title == String.Empty)
            {
                rust.BroadcastChat(null, Message, configData.SteamID);
            }
            else
            {
                rust.BroadcastChat(Prefix, Message, configData.SteamID);
            }
            return true;
        }
    }
}
