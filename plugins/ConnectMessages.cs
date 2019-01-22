using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using System;

namespace Oxide.Plugins
{
    [Info("ConnectMessages", "Spicy", "1.1.9", ResourceId = 2178)]
    [Description("Provides connect and disconnect messages.")]
    public class ConnectMessages : CovalencePlugin
    {
        #region Country API Class

        private class Response
        {
            [JsonProperty("country")]
            public string Country { get; set; }
        }

        #endregion

        #region Helpers

        private void Broadcast(string key, params object[] args)
        {
            foreach (var player in players.Connected)
                player.Message(string.Format(GetLangValue(key, player.Id), args));
        }

        private bool GetConfigValue(string key) => Config.Get<bool>("Settings", key);

        private string GetLangValue(string key, string userId) => lang.GetMessage(key, this, userId);

        #endregion

        #region Config

        private bool showConnectMessage;
        private bool showConnectCountry;
        private bool showDisconnectMessage;
        private bool showDisconnectReason;
        private bool showAdminMessages;

        protected override void LoadDefaultConfig()
        {
            Config["Settings"] = new Dictionary<string, bool>
            {
                ["ShowConnectMessage"] = true,
                ["ShowConnectCountry"] = false,
                ["ShowDisconnectMessage"] = true,
                ["ShowDisconnectReason"] = false,
                ["ShowAdminMessages"] = true
            };
        }

        private void InitialiseConfig()
        {
            try
            {
                showConnectMessage = GetConfigValue("ShowConnectMessage");
                showConnectCountry = GetConfigValue("ShowConnectCountry");
                showDisconnectMessage = GetConfigValue("ShowDisconnectMessage");
                showDisconnectReason = GetConfigValue("ShowDisconnectReason");
                showAdminMessages = GetConfigValue("ShowAdminMessages");
            }
            catch (InvalidCastException)
            {
                Puts("Your configuration file seems to be invalid. Please make sure only true/false values are being used and check the file isn't corrupt using \"http://pro.jsonlint.com/\".");
            }
        }

        #endregion

        #region Lang

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ConnectMessage"] = "{0} has connected.",
                ["ConnectMessageCountry"] = "{0} has connected from {1}.",
                ["DisconnectMessage"] = "{0} has disconnected.",
                ["DisconnectMessageReason"] = "{0} has disconnected. ({1})"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ConnectMessage"] = "{0} s'est connect�(e).",
                ["ConnectMessageCountry"] = "{0} s'est connect�(e) de {1}.",
                ["DisconnectMessage"] = "{0} s'est disconnect�(e).",
                ["DisconnectMessageReason"] = "{0} s'est disconnect�(e). ({1})"
            }, this, "fr");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ConnectMessage"] = "{0} ha conectado.",
                ["ConnectMessageCountry"] = "{0} se ha conectado de {1}.",
                ["DisconnectMessage"] = "{0} se ha desconectado.",
                ["DisconnectMessageReason"] = "{0} se ha desconectado. ({1})"
            }, this, "es");
        }

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
#if HURTWORLD
            GameManager.Instance.ServerConfig.ChatConnectionMessagesEnabled = false;
#endif
            InitialiseConfig();
        }

        private void OnUserConnected(IPlayer player)
        {
            if (!showConnectMessage || (player.IsAdmin && !showAdminMessages))
                return;

            if (!showConnectCountry)
            {
                Broadcast("ConnectMessage", player.Name.Sanitize());
                return;
            }

            string apiUrl = "http://ip-api.com/json/";

            webrequest.Enqueue(apiUrl + player.Address, null, (code, response) =>
            {
                if (code != 200 || response == null)
                {
                    Puts($"WebRequest to {apiUrl} failed, sending connect message without the country.");
                    Broadcast("ConnectMessage", player.Name.Sanitize());
                    return;
                }

                string country = JsonConvert.DeserializeObject<Response>(response).Country;
                Broadcast("ConnectMessageCountry", player.Name.Sanitize(), country);
            }, this);
        }

        private void OnUserDisconnected(IPlayer player, string reason)
        {
            if (!showDisconnectMessage || (player.IsAdmin && !showAdminMessages))
                return;

            if (!showDisconnectReason)
                Broadcast("DisconnectMessage", player.Name.Sanitize());
            else
                Broadcast("DisconnectMessageReason", player.Name.Sanitize(), reason);
        }

        #endregion
    }
}