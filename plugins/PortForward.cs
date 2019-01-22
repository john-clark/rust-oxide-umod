using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

// TODO: Add support for additional router brands and firmware
// TODO: Fix port forwards not always being applied with DD-WRT
// TODO: Implement https preference when Oxide can support this
// TODO: Forward RCON port if enabled (need Covalence support for this)

namespace Oxide.Plugins
{
    [Info("Port Forward", "Wulf/lukespragg", "0.0.1")]
    [Description("Automatic port forwarding for DD-WRT routers")]
    public class PortForward : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "DD-WRT username (default is root)")]
            public string Username { get; set; } = "root";

            [JsonProperty(PropertyName = "DD-WRT password (default is admin)")]
            public string Password { get; set; } = "admin";
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

        private static Dictionary<string, string> headers;
        private static IPAddress localIp;
        private static IPAddress routerIp;

        private void OnServerInitialized()
        {
            localIp = GetLocalIp();
            routerIp = GetGateway();

            if (localIp == null || routerIp == null)
            {
                LogWarning("This server does not appear to be behind a router");
                return;
            }

            headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.Username}:{config.Password}")),
                ["Referer"] = $"http://{routerIp}/ForwardSpec.asp"
            };

            ToggleForward("on");
        }

        private void Unload()
        {
            ToggleForward("off");
        }

        #endregion Initialization

        #region Commands

        [Command("pf.net")]
        private void NetCommand(IPlayer player, string command, string[] args)
        {
            if (!player.IsServer)
            {
                return;
            }

            LogWarning($"Local IP address: {localIp}");
            LogWarning($"Router IP address: {routerIp}");
            LogWarning($"External IP address: {server.Address}");
            LogWarning($"Server port: {server.Port}");
            // TODO: Show RCON port
        }

        [Command("pf.toggle")]
        private void ToggleCommand(IPlayer player, string command, string[] args)
        {
            if (!player.IsServer)
            {
                return;
            }

            string state = args.Length > 0 ? args[0].ToLower() : "on";
            if (state != "on" && state != "off")
            {
                LogWarning("Please specify 'on' or 'off' state");
                return;
            }

            ToggleForward(state);
        }

        #endregion Commands

        #region The Magic

        private void ToggleForward(string state)
        {
            // Set form data to send
            string data = $"name0={covalence.Game}&pro0=both&ip0={localIp}&from0={server.Port}&to0={server.Port}" +
                $"&enable0={state}&forward_spec=13&submit_button=ForwardSpec&action=Apply";

            // Send web request to control panel
            webrequest.Enqueue($"http://{routerIp}/apply.cgi", data, (code, response) =>
            {
                if (code == 400) // TODO: Handle specific error messages
                {
                    LogWarning("Could not login to router control panel");
                }
                else if (code != 200 || response == null)
                {
                    LogWarning($"HTTP code: {code}");
                    LogWarning($"Response: {response}");
                }
                else
                {
                    LogWarning($"Toggled port {server.Port} forward {state} for {localIp}");
                }
            }, this, RequestMethod.POST, headers);
        }

        #endregion The Magic

        #region Helpers

        private static IPAddress GetGateway() // TODO: Clean this up
        {
            return NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties()?.GatewayAddresses)
                .Select(g => g?.Address)
                .Where(a => a != null)
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .FirstOrDefault(a => Array.FindIndex(a.GetAddressBytes(), b => b != 0) >= 0);
        }

        private static IPAddress GetLocalIp() // TODO: Clean this up
        {
            return NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .Where(n => n.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                            || n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                .SelectMany(n => n.GetIPProperties()?.UnicastAddresses)
                .Select(g => g?.Address)
                .LastOrDefault(a => a?.AddressFamily == AddressFamily.InterNetwork);
        }

        // TODO: Create enum of protocols per game

        #endregion Helpers
    }
}
