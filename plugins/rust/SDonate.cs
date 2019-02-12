using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SDonate", "Webmaster10", "1.7.7", ResourceId = 1965)]
    [Description("A plugin to interface with an SDonate web server to automatically handle donations.")]
    public class SDonate : RustPlugin
    {
        private string[] ParseResponse(string response)
        {
            string[] commands = JsonConvert.DeserializeObject<string[]>(response);
            return commands;
        }

        private void GetRequest(string URL, string requestType)
        {
            webrequest.Enqueue(URL, null, (code, response) => GetRequestCallback(code, response, URL, requestType), this);
        }

        private void GetRequestCallback(int code, string response, string URL, string requestType)
        {
            if (response == null || code != 200)
            {
                Puts($"SDonate Error: {code} - Couldn't connect to server. URL: {URL}");
                return;
            }
            switch (requestType) {
                case "requestCommands":
                    if (response != "" && response != "No commands" && response != "Error")
                    {
                        string[] commands = ParseResponse(response);
                        float commandWait = 0f;
                        for (int i = 0; i < commands.Length; i++) {
                            commandWait = commandWait + 1f;
                            string commandToRun = commands[i];
                            timer.Once(commandWait, () =>
                            {
                                RunCommand(commandToRun);
                            });
                        }
                    }
                    break;
                case "requestCommandsToRunNow":
                    if (response != "" && response != "No commands" && response != "Error")
                    {
                        string[] commands = ParseResponse(response);
                        float commandWait = 0f;
                        for (int i = 0; i < commands.Length; i++) {
                            commandWait = commandWait + 1f;
                            string commandToRun = commands[i];
                            timer.Once(commandWait, () =>
                            {
                                RunCommand(commandToRun);
                            });
                        }
                    }
                    break;
                case "confirmCommand":
                    Puts("SDonate ran a command.");
                    break;
            }
        }

        private void RunCommand(string command)
        {
            rust.RunServerCommand(command);
            string apiKey = Uri.EscapeDataString(Config["SDonateAPIKey"].ToString());
            string ip = Uri.EscapeDataString(Config["ServerIP"].ToString());
            string port = Uri.EscapeDataString(Config["ServerPort"].ToString());
            string command1 = Uri.EscapeDataString(command);
            string apiURL = Config["PluginAPIUrl"].ToString().Replace("https://sdonate.com", "http://sdonate.com");
            string URL = apiURL + "?game=rust&apikey=" + apiKey + "&ip=" + ip + "&port=" + port + "&confirmcommand=" + command1;
            GetRequest(URL, "confirmCommand");
        }

        private void RequestPlayerCommands(BasePlayer player)
        {
            string apiKey = Uri.EscapeDataString(Config["SDonateAPIKey"].ToString());
            string ip = Uri.EscapeDataString(Config["ServerIP"].ToString());
            string port = Uri.EscapeDataString(Config["ServerPort"].ToString());
            string steamID = player.userID.ToString();
            steamID = Uri.EscapeDataString(steamID);
            string apiURL = Config["PluginAPIUrl"].ToString().Replace("https://sdonate.com", "http://sdonate.com");
            string URL = apiURL + "?game=rust&apikey=" + apiKey + "&player=" + steamID + "&ip=" + ip + "&port=" + port;
            GetRequest(URL, "requestCommands");
        }

        private void RequestCommandsToRunNow()
        {
            string apiKey = Uri.EscapeDataString(Config["SDonateAPIKey"].ToString());
            string ip = Uri.EscapeDataString(Config["ServerIP"].ToString());
            string port = Uri.EscapeDataString(Config["ServerPort"].ToString());
            string apiURL = Config["PluginAPIUrl"].ToString().Replace("https://sdonate.com", "http://sdonate.com");
            string URL = apiURL + "?game=rust&apikey=" + apiKey + "&ip=" + ip + "&port=" + port + "&checkcommands=";
            GetRequest(URL, "requestCommandsToRunNow");
        }

        private void OnPlayerInit(BasePlayer player)
        {
            RequestPlayerCommands(player);
        }

        private void Init()
        {
            timer.Repeat(30.0f, 0, () =>
            {
                RequestCommandsToRunNow();
            });
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating SDonate configuration file");
            Config.Clear();
            Config["ServerIP"] = "127.0.0.1";
            Config["ServerPort"] = "28015";
            Config["PluginAPIUrl"] = "http://yoursite.com/sdonate/pluginapi.php";
            Config["SDonateAPIKey"] = "YOURAPIKEY";
            SaveConfig();
        }
    }
}