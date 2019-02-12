using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Libraries;
using UnityEngine;
using Random = System.Random;
using Time = Oxide.Core.Libraries.Time;

// ReSharper disable UnusedMember.Local

namespace Oxide.Plugins
{
    [Info("Smart Chat Bot", "Iv Misticos", "2.0.6")]
    [Description("I send chat messages based on some triggers or time.")]
    public class SmartChatBot : RustPlugin
    {
        #region Variables

        private static Random Random = new Random();
        private Dictionary<BasePlayer, uint> _lastSent = new Dictionary<BasePlayer, uint>();
        private uint _lastSentGlobal;

        private static Time Time = GetLibrary<Time>();

        private const string CountryRequest = "http://ip-api.com/json/{ip}?fields=country,countryCode,status";
        
        #endregion
        
        #region Configuration

        private Configuration _config = new Configuration();
        
        public class Configuration
        {
            [JsonProperty(PropertyName = "Chat Prefix")]
            public string Prefix = "<color=#787FFF>Bot </color>";

            [JsonProperty(PropertyName = "Show Chat Prefix")]
            public bool ShowPrefix = true;

            [JsonProperty(PropertyName = "Bot Icon (SteamID)")]
            public ulong ChatSteamID = 0;

            [JsonProperty(PropertyName = "Cooldown Between Auto Responses For User")]
            public string Cooldown = "10s";

            [JsonProperty(PropertyName = "Global Cooldown Between Auto Responses")]
            public string CooldownGlobal = "2s";

            [JsonProperty(PropertyName = "Use Default Chat (0), Chat Plus (1), Better Chat (2)")]
            public ushort ChatSystem = 0;

            [JsonIgnore] public uint ParsedCooldown;
            [JsonIgnore] public uint ParsedCooldownGlobal;

            [JsonProperty(PropertyName = "Debug")]
            public bool Debug = false;

            [JsonProperty(PropertyName = "Allow Multiple Auto Responses")]
            public bool MultipleAutoResponses = false;

            [JsonProperty(PropertyName = "Minimal Time Between Message And Answer")]
            public int MinTime = 1;

            [JsonProperty(PropertyName = "Maximal Time Between Message And Answer")]
            public int MaxTime = 5;

            [JsonProperty(PropertyName = "Welcome Message", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> WelcomeMessage = new List<string> { "Welcome, {name}!", "Hello, dear {name}!", "Hello, {name}! Your IP: {ip}" };

            [JsonProperty(PropertyName = "Welcome Message Enabled")]
            public bool WelcomeMessageEnabled = false;

            [JsonProperty(PropertyName = "Joining Message", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> JoiningMessage = new List<string> { "Welcome, {name} ({id}, {ip})!", "Hello, dear {name} ({id}, {ip})!", "{name} came from {country} ({countrycode})" };

            [JsonProperty(PropertyName = "Leaving Message", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> LeavingMessage = new List<string> { "Bye, {name} ({id}, {ip})!\nReason: {reason}", "{name} ({id}, {ip}) left the game. Reason: {reason}", "{name} from {country} ({countrycode}) just left the game!" };

            [JsonProperty(PropertyName = "Joining Message Enabled")]
            public bool JoiningMessageEnabled = false;

            [JsonProperty(PropertyName = "Leaving Message Enabled")]
            public bool LeavingMessageEnabled = false;

            [JsonProperty(PropertyName = "Show Joining Message To Player That Joined")]
            public bool JoiningMessageSelfEnabled = false;

            [JsonProperty(PropertyName = "Show Leaving Message To Player That Left")]
            public bool LeavingMessageSelfEnabled = false;
            
            [JsonProperty(PropertyName = "Auto Messages", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<AutoMessageGroup> AutoMessages = new List<AutoMessageGroup> { new AutoMessageGroup() };
            
            [JsonProperty(PropertyName = "Auto Responses", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<AutoResponseGroup> AutoResponses = new List<AutoResponseGroup> { new AutoResponseGroup() };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
            }
            catch
            {
                Config.WriteObject(_config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}.jsonError");
                PrintError("The configuration file contains an error and has been replaced with a default config.\n" +
                           "The error configuration file was saved in the .jsonError extension");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);

        public class AutoMessageGroup
        {
            [JsonProperty(PropertyName = "Permission")]
            public string Permission = "smartchatbot.messages";
            
            [JsonProperty(PropertyName = "Message Frequency")]
            public string Frequency = "5m";

            [JsonIgnore] public uint ParsedFrequency;
            [JsonIgnore] public short ActiveMessage;

            [JsonProperty(PropertyName = "Auto Messages", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<AutoMessage> AutoMessages = new List<AutoMessage> { new AutoMessage() };
        }

        public class AutoMessage
        {
            [JsonProperty(PropertyName = "Is Enabled")]
            public bool Enabled = true;

            [JsonProperty(PropertyName = "Message")]
            public string Message = "Do not mind, I am just a stupid bot.";
        }

        public class AutoResponseGroup
        {
            [JsonProperty(PropertyName = "Permission")]
            public string Permission = "smartchatbot.response";
            
            [JsonProperty(PropertyName = "Auto Responses", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<AutoResponse> AutoResponses = new List<AutoResponse> { new AutoResponse() };
        }

        public class AutoResponse
        {
            [JsonProperty(PropertyName = "Is Enabled")]
            public bool Enabled = true;
            
            [JsonProperty(PropertyName = "Remove Message From Sender")]
            public bool RemoveMessage = false;
            
            [JsonProperty(PropertyName = "Send Response For Everyone (true) or Only For Sender (false)")]
            public bool SendPublic = true;
            
            [JsonProperty(PropertyName = "Triggers", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<AutoResponseTrigger> Triggers = new List<AutoResponseTrigger> { new AutoResponseTrigger() };

            [JsonProperty(PropertyName = "Answers", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Answers = new List<string> { "This bot really works.", "IT WORKS OMG!" };

            public bool IsValid() => Triggers.Count > 0 && Answers.Count > 0;
        }

        public class AutoResponseTrigger
        {
            [JsonProperty(PropertyName = "Percentage Of Contained Words")]
            public float ContainedWordsPercentage = 0.75f;

            [JsonProperty(PropertyName = "Regex Enabled")]
            public bool Regex = false;
            
            [JsonProperty(PropertyName = "Words", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Words = new List<string> { "How", "this", "bot", "works" };
        }
        
        #endregion
        
        #region Hooks

        private void OnServerInitialized()
        {
            LoadConfig();
            Unsubscribe(nameof(OnChatPlusMessage));
            Unsubscribe(nameof(OnBetterChat));
            Unsubscribe(nameof(OnPlayerChat));

            if (_config.ChatSystem == 0)
                Subscribe(nameof(OnPlayerChat));
            else if (_config.ChatSystem == 1)
                Subscribe(nameof(OnChatPlusMessage));
            else if (_config.ChatSystem == 2)
                Subscribe(nameof(OnBetterChat));

            var pl = plugins.GetAll();
            var plCount = pl.Length;
            for (var i = 0; i < plCount; i++)
            {
                var p = pl[i];
                // ReSharper disable once ConvertIfStatementToSwitchStatement
                if (p.Name == "BetterChat" || p.Name == "Better Chat")
                    PrintWarning("Detected Better Chat. Make sure you enabled it in your configuration.");
                else if (p.Name == "ChatPlus" || p.Name == "Chat Plus")
                    PrintWarning("Detected Chat Plus. Make sure you enabled it in your configuration.");
            }

            if (!ConvertToSeconds(_config.Cooldown, out _config.ParsedCooldown))
            {
                PrintError($"Unable to convert \"{_config.Cooldown}\" to seconds!");
                Interface.GetMod().UnloadPlugin(Name);
                return;
            }

            if (!ConvertToSeconds(_config.CooldownGlobal, out _config.ParsedCooldownGlobal))
            {
                PrintError($"Unable to convert \"{_config.CooldownGlobal}\" to seconds!");
                Interface.GetMod().UnloadPlugin(Name);
                return;
            }

            var messageGroupsCount = _config.AutoMessages.Count;
            for (var i = 0; i < messageGroupsCount; i++)
            {
                var messageGroup = _config.AutoMessages[i];
                PrintDebug($"Handling Message Group (ID: {i})");
                
                // Time Handling
                if (!ConvertToSeconds(messageGroup.Frequency, out messageGroup.ParsedFrequency))
                {
                    PrintError($"Unable to convert \"{messageGroup.Frequency}\" to seconds!");
                    Interface.GetMod().UnloadPlugin(Name);
                    return;
                }
                
                // Permissions
                permission.RegisterPermission(messageGroup.Permission, this);
                
                // Timers
                timer.Every(messageGroup.ParsedFrequency, () => HandleBroadcast(messageGroup));
            }

            var responseGroupsCount = _config.AutoResponses.Count;
            for (var i = 0; i < responseGroupsCount; i++)
            {
                PrintDebug($"Handling Response Group (ID: {i})");
                var responseGroup = _config.AutoResponses[i];
                
                // Permissions
                permission.RegisterPermission(responseGroup.Permission, this);
            }

            var cmdLib = GetLibrary<Command>();
            cmdLib.AddConsoleCommand("smartchatbot.debuginfo", this, CommandConsoleDebugInfo);
        }
 
        // ReSharper disable once SuggestBaseTypeForParameter
        private object OnChatPlusMessage(Dictionary<string, object> data)
        {
            PrintDebug("Called OnChatPlusMessage");
            object playerObj, messageObj;
            if (!data.TryGetValue("Player", out playerObj) || !data.TryGetValue("Message", out messageObj))
                return null;
            
            var player = BasePlayer.Find((playerObj as IPlayer)?.Id);
            var message = messageObj?.ToString();
            if (player == null || !player.IsConnected || string.IsNullOrEmpty(message))
                return null;
            
            return HandleChatMessage(player, message);
        }

        private object OnChatPlusMessage(BasePlayer player, string message)
        {
            PrintDebug("Called OnChatPlusMessage");
            return HandleChatMessage(player, message);
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        private object OnBetterChat(Dictionary<string, object> data)
        {
            PrintDebug("Called OnBetterChat");
            object playerObj, messageObj;
            if (!data.TryGetValue("Player", out playerObj) || !data.TryGetValue("Text", out messageObj))
                return null;
            
            var player = BasePlayer.Find((playerObj as IPlayer)?.Id);
            var message = messageObj?.ToString();
            if (player == null || !player.IsConnected || string.IsNullOrEmpty(message))
                return null;
            
            return HandleChatMessage(player, message);
        }

        private object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            PrintDebug("Called OnPlayerChat");
            var player = arg.Player();
            if (player == null || !arg.HasArgs())
                return null;

            return HandleChatMessage(player, string.Join(" ", arg.Args));
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (_config.JoiningMessage.Count > 0 && _config.JoiningMessageEnabled)
            {
                var message =
                    new StringBuilder(_config.JoiningMessage[Random.Next(0, _config.JoiningMessage.Count - 1)]);
                var ip = player.net.connection.ipaddress.Substring(0, player.net.connection.ipaddress.LastIndexOf(':'));
                message = message.Replace("{name}", player.displayName).Replace("{id}", player.UserIDString)
                    .Replace("{ip}", ip);

                var usePlayer = _config.JoiningMessageSelfEnabled ? null : player;
                if (message.ToString().IndexOf("{country}", StringComparison.CurrentCultureIgnoreCase) != -1 || message.ToString().IndexOf("{countrycode}", StringComparison.CurrentCultureIgnoreCase) != -1)
                    HandleCountryMessage(message, ip, usePlayer);
                else Publish(message.ToString(), player2: usePlayer);
            }

            if (_config.WelcomeMessage.Count <= 0 || !_config.WelcomeMessageEnabled) return;
            
            {
                var message = new StringBuilder(_config.WelcomeMessage[Random.Next(0, _config.WelcomeMessage.Count - 1)]);
                var ip = player.net.connection.ipaddress.Substring(0, player.net.connection.ipaddress.LastIndexOf(':'));
                message = message.Replace("{name}", player.displayName).Replace("{id}", player.UserIDString)
                    .Replace("{ip}", ip);
                
                if (message.ToString().IndexOf("{country}", StringComparison.CurrentCultureIgnoreCase) != -1 || message.ToString().IndexOf("{countrycode}", StringComparison.CurrentCultureIgnoreCase) != -1)
                    HandleCountryMessage(message, ip, player, false);
                else Publish(message.ToString(), player2: player, exclude: false);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (_config.LeavingMessage.Count <= 0 || !_config.LeavingMessageEnabled) return;
            
            var message =
                new StringBuilder(_config.LeavingMessage[Random.Next(0, _config.LeavingMessage.Count - 1)]);
            var ip = player.net.connection.ipaddress.Substring(0, player.net.connection.ipaddress.LastIndexOf(':'));
            message = message.Replace("{name}", player.displayName).Replace("{id}", player.UserIDString)
                .Replace("{ip}", ip).Replace("{reason}", reason);

            var usePlayer = _config.LeavingMessageSelfEnabled ? null : player;
            if (message.ToString().IndexOf("{country}", StringComparison.CurrentCultureIgnoreCase) != -1 ||
                message.ToString().IndexOf("{countrycode}", StringComparison.CurrentCultureIgnoreCase) != -1)
                HandleCountryMessage(message, ip, usePlayer);
            else Publish(message.ToString(), string.Empty, usePlayer);
        }

        #endregion
        
        #region Commands
        
        private bool CommandConsoleDebugInfo(ConsoleSystem.Arg arg)
        {
            if (!arg.IsRcon && !arg.IsConnectionAdmin)
                return false;

            arg.ReplyWith($"Plugin: {Name}\n" +
                          $"Version: {Version}\n" +
                          $"Debug enabled: {_config.Debug}\n" +
                          $"Server IP: {ConVar.Server.ip}\n" +
                          $"Average FPS: {Performance.current.frameRateAverage}\n" +
                          $"Auto Responses Amount: {_config.AutoResponses.Sum(x => x.AutoResponses.Count)}\n" +
                          $"Triggers Amount: {_config.AutoResponses.Sum(x => x.AutoResponses.Sum(y => y.Triggers.Count))}\n" +
                          $"Auto Messages Amount: {_config.AutoMessages.Sum(x => x.AutoMessages.Count)}");
            return true;
        }
        
        #endregion
        
        #region Helpers

        private void HandleCountryMessage(StringBuilder message, string ip, BasePlayer player, bool exclude = true)
        {
            webrequest.Enqueue(CountryRequest.Replace("{ip}", ip), string.Empty, (status, result) =>
            {
                PrintDebug("Requested a country!");
                try
                {
                    // ReSharper disable once InvertIf
                    if (status == 200)
                    {
                        var info = JsonConvert.DeserializeObject<Dictionary<string, object>>(result);
                        if ((string) info["status"] == "success")
                        {
                            message = message.Replace("{country}", (string) info["country"])
                                .Replace("{countrycode}", (string) info["countryCode"]);
                        }
                    }
                }
                catch (Exception e)
                {
                    PrintError(e.ToString());
                }
                finally
                {
                    Publish(message.ToString(), string.Empty, player, exclude);
                }
            }, this);
        }

        private void PrintDebug(string message)
        {
            if (!_config.Debug) return;
            Puts($"DEBUG: {message}");
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        private void Publish(string message, string perm = "", BasePlayer player2 = null, bool exclude = true)
        {
            PrintDebug("Called Publish");
            PrintDebug($"Message: {message}");
            PrintDebug($"Permission: {perm}");
            var notRequirePermission = string.IsNullOrEmpty(perm);
            message = FormatMessage(message);

            if (exclude)
            {
                var players = BasePlayer.activePlayerList;
                var playersCount = players.Count;
                for (var i = 0; i < playersCount; i++)
                {
                    var player = players[i];
                    if (player != player2 &&
                        (notRequirePermission || permission.UserHasPermission(player.UserIDString, perm)))
                        SendMessage(player, message);
                }
            }
            else
                SendMessage(player2, message);
        }

        private void Publish(BasePlayer player, string message) => SendMessage(player, FormatMessage(message));

        private void SendMessage(BasePlayer player, string message) =>
            player.SendConsoleCommand("chat.add", _config.ChatSteamID, message);

        private string FormatMessage(string message) => _config.ShowPrefix ? _config.Prefix + message : message;
        
        #endregion
        
        #region Parsers
        
        private static readonly Regex RegexStringTime = new Regex(@"(\d+)([dhms])", RegexOptions.Compiled);
        private static bool ConvertToSeconds(string time, out uint seconds)
        {
            seconds = 0;
            if (time == "0" || string.IsNullOrEmpty(time)) return true;
            var matches = RegexStringTime.Matches(time);
            if (matches.Count == 0) return false;
            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                // ReSharper disable once SwitchStatementMissingSomeCases
                switch (match.Groups[2].Value)
                {
                    case "d":
                    {
                        seconds += uint.Parse(match.Groups[1].Value) * 24 * 60 * 60;
                        break;
                    }
                    case "h":
                    {
                        seconds += uint.Parse(match.Groups[1].Value) * 60 * 60;
                        break;
                    }
                    case "m":
                    {
                        seconds += uint.Parse(match.Groups[1].Value) * 60;
                        break;
                    }
                    case "s":
                    {
                        seconds += uint.Parse(match.Groups[1].Value);
                        break;
                    }
                }
            }
            return true;
        }
        
        #endregion

        #region Automated Messages

        private object HandleChatMessage(BasePlayer player, string msg)
        {
            PrintDebug("Called HandleChatMessage");

            var response = Interface.GetMod().CallHook("CanSmartHandleMessage", player, msg);
            if (response != null)
                return null;
            
            var autoResponseGroups = _config.AutoResponses;
            var autoResponseGroupsCount = autoResponseGroups.Count;

            var tNow = Time.GetUnixTimestamp();
            uint lastSent;
            _lastSent.TryGetValue(player, out lastSent);

            PrintDebug($"tNow: {tNow}");
            PrintDebug($"lastSent: {lastSent}");
            if (lastSent + _config.ParsedCooldown > tNow ||
                _lastSentGlobal + _config.ParsedCooldownGlobal > tNow)
                return null;

            var matched = false;
            var removeMessage = false;
            // Auto Response Groups
            for (var i1 = 0; i1 < autoResponseGroupsCount; i1++)
            {
                var autoResponseGroup = autoResponseGroups[i1];
                var perm = autoResponseGroup.Permission;
                if (!string.IsNullOrEmpty(perm) &&
                    !permission.UserHasPermission(player.UserIDString, autoResponseGroup.Permission))
                    continue;

                var autoResponses = autoResponseGroup.AutoResponses;
                var autoResponsesCount = autoResponses.Count;
                // Auto Responses
                for (var i2 = 0; i2 < autoResponsesCount; i2++)
                {
                    var autoResponse = autoResponses[i2];
                    if (!autoResponse.IsValid() || !autoResponse.Enabled)
                        continue;

                    var answersCount = autoResponse.Answers.Count;
                    var autoResponseTriggers = autoResponse.Triggers;
                    var autoResponseTriggersCount = autoResponseTriggers.Count;
                    // Triggers
                    for (var i3 = 0; i3 < autoResponseTriggersCount; i3++)
                    {
                        var trigger = autoResponseTriggers[i3];

                        float wordsCount = trigger.Words.Count;
                        ushort wordsMatches = 0;
                        var regex = trigger.Regex;
                        
                        // Each Word In Triggers
                        for (var i4 = 0; i4 < wordsCount; i4++)
                        {
                            if (regex && Regex.IsMatch(msg, trigger.Words[i4]) ||
                                msg.IndexOf(trigger.Words[i4], StringComparison.CurrentCultureIgnoreCase) != -1)
                                wordsMatches++;
                        }

                        var match = wordsMatches / wordsCount;
                        PrintDebug($"Matched: {match}");

                        if (wordsMatches / wordsCount < trigger.ContainedWordsPercentage) continue;

                        matched = true;
                        var answer = autoResponse.Answers[Random.Next(0, answersCount)];
                        PrintDebug("Matched message");

                        response = Interface.GetMod().CallHook("CanSmartAnswerMessage", player, msg, answer);
                        if (response != null)
                            return null;

                        if (_config.MinTime <= 0 && _config.MaxTime <= 0)
                            TrySend(player, autoResponse.SendPublic, answer);
                        else
                            timer.Once(Random.Next(_config.MinTime, _config.MaxTime), () => TrySend(player, autoResponse.SendPublic, answer));

                        if (autoResponse.RemoveMessage)
                            removeMessage = true;

                        break;
                    }

                    if (matched && !_config.MultipleAutoResponses)
                        break;
                }
            }

            if (matched)
            {
                _lastSent[player] = tNow;
                _lastSentGlobal = tNow;
                PrintDebug("Matched. Changing cooldown info.");
            }

            if (removeMessage)
                return false;
            return null;
        }

        private void TrySend(BasePlayer player, bool isPublic, string answer)
        {
            if (isPublic)
                Publish(answer, string.Empty);
            else
                Publish(player, answer);
        }

        private void HandleBroadcast(AutoMessageGroup group)
        {
            PrintDebug("Called HandleBroadcast");
            if (group.ActiveMessage > group.AutoMessages.Count - 1)
                group.ActiveMessage = 0;
            PrintDebug($"Active Message: {group.ActiveMessage}");

            var message = group.AutoMessages[group.ActiveMessage++];
            if (message.Enabled)
                Publish(message.Message, group.Permission);
        }

        #endregion
    }
}