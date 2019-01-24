using System;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Server Chat", "Tricky", "1.1")]
    [Description("Replaces the default server chat icon and prefix")]

    public class ServerChat : RustPlugin
    {
        ConfigData configData;

        class ConfigData
        {
            [JsonProperty(PropertyName = "Chat Icon (SteamID64)")]
            public string SteamID { get; set; }

            [JsonProperty(PropertyName = "Prefix")]
            public string Prefix { get; set; }

            [JsonProperty(PropertyName = "Prefix Color")]
            public string PrefixColor { get; set; }

            [JsonProperty(PropertyName = "Message Color")]
            public string MessageColor { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                SteamID = "",
                Prefix = "[Server]",
                PrefixColor = "#55aaff",
                MessageColor = "white",
            };
            SaveConfig(config);
        }

        void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();

        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);

        void Init()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        object OnServerMessage(string message, string name, string color, ulong id)
        {
            string Prefix = $"<color={configData.PrefixColor}>{configData.Prefix}</color>";
            string Message = $"<color={configData.MessageColor}>{message}</color>";

                rust.BroadcastChat(Prefix, Message, configData.SteamID);
            return true;
        }
    }
}
