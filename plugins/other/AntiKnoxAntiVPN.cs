using Newtonsoft.Json;
using UnityEngine.Networking;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;

namespace Oxide.Plugins
{

    [Info("AntiKnox Anti-VPN", "AntiKnox", "1.0.3")]
    [Description("Disallow VPNs from accessing your server.")]
    class AntiKnoxAntiVPN : RustPlugin
    {

        private string authKey;
        private bool loginWarningShown;

        protected override void LoadConfig()
        {
            base.LoadConfig();

            authKey = (string)Config["Key"];
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"KickMessage", "AntiKnox: You're not allowed to connect with a VPN or proxy."},
            }, this);
        }

        void OnServerInitialized()
        {
            tryPrintSetupGuide();
        }

        protected override void LoadDefaultConfig()
        {
            Config["Key"] = "<PUT YOUR ANTIKNOX KEY HERE>";
        }

        private void tryPrintSetupGuide()
        {
            if (authKey == null || authKey.Length != 64)
            {
                authKey = null;
                printSetupGuide();
            }
        }

        private void printSetupGuide()
        {
            var warning = @"
Welcome to AntiKnox!

Before you can begin filtering VPN connections, you will need
to set your 64-character auth key. Enter the following command
to secure your server:

antiknox.setkey your64characterlonglicensekeyfromyourdashboard

Alternatively, you can edit the config file 'AntiKnox.json'
and set the 'Key' to your auth key manually.

If you don't have a license key yet, sign up for a free account
at https://www.antiknox.net to obtain one.
";

            foreach (var line in warning.Split('\n'))
            {
                PrintWarning(line);
            }
        }

        private void printSetupSuccess()
        {
            var message = @"
Nice - you're all set! Welcome to AntiKnox!

From now on, AntiKnox will guard your Rust server from VPNs, proxies
and other IP hiders. You've taken a big step in increasing your server's
security and playability. Buh-bye, criminals!

Should you require assistance, you can reach out to us at:
https://www.antiknox.com/dashboard
";

            foreach (var line in message.Split('\n'))
            {
                PrintWarning(line);
            }
        }

        void OnUserConnected(IPlayer player)
        {
            // If the auth key is not defined we allow the connection.
            if (authKey == null || authKey.Length != 64)
            {
                // If this is the first player logging in, we send a helpful
                // message indicating we're not set up yet.
                if (!loginWarningShown)
                {
                    printSetupGuide();
                }

                return;
            }

            webrequest.Enqueue("http://api.antiknox.net/lookup/" + player.Address + "?auth=" + authKey, null, (code, response) =>
            {
                handleApiResponse(player, player.Address, code, response);
            }, this, RequestMethod.GET, null);

            return;
        }

        private void handleApiResponse(IPlayer player, string host, int code, string response)
        {
            if (code != 200 || response == null)
            {
                PrintWarning(response);
                return;
            }

            var result = JsonConvert.DeserializeObject<AntiKnoxResult>(response);

            if (result != null && result.match)
            {
                PrintWarning("Disallowing IP " + host + " because it's an anonymous IP address.");
                player.Kick(lang.GetMessage("KickMessage", this, player.Id));
            }
        }

        [ConsoleCommand("antiknox.setkey")]
        private void cmdSetKey(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length < 1)
            {
                PrintError("Usage: antiknox.setkey [64-character-auth-key]");
                return;
            }

            var key = arg.Args[0].Trim();
            if (key.Length != 64)
            {
                PrintError("The auth key must be 64 characters. See the dashboard for your key:");
                PrintError("https://ww.antiknox.net/dashboard");
                return;
            }

            var hadKey = authKey != null && authKey.Length == 64;
            Config["Key"] = authKey = key;
            SaveConfig();

            Puts("Your auth key has been updated.");

            // Print the welcome message once the user has set everything up:
            if (!hadKey)
            {
                printSetupSuccess();
            }
        }

        [System.Serializable]
        class AntiKnoxResult
        {
            public bool match;
        }

    }

}
