using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CodeHatch.Engine.Networking;
using CodeHatch.Engine.Events.Player;
using CodeHatch.Networking.Events.Social;
using CodeHatch.Networking.Events.Players;
using CodeHatch.Networking.Events.Entities.Players;

namespace Oxide.Plugins
{
    [Info("Out Of Character chat", "D-Kay", "1.0")]
    public class OOC : ReignOfKingsPlugin
    {
        #region Server Variables
        private Collection<string> _Players = new Collection<string>();
        private string chatPrefix => GetConfig("chatPrefix", "[[4665ff]OOC[ffffff]]");
        #endregion

        #region Save and Load data
        protected override void LoadDefaultConfig()
        {
            Config["chatPrefix"] = chatPrefix;
            SaveConfig();
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "ToggleOocOn", "Now talking out of character." },
                { "ToggleOocOff", "No longer talking out of character." }
            }, this);
        }

        private void Loaded()
        {
            cmd.AddChatCommand("ooc", this, "oocChat");
            LoadDefaultMessages();
            LoadDefaultConfig();
        }
        #endregion

        #region Functions
        private bool CheckPlayerOoc(Player returner)
        {
            bool contained = false;
            string player = returner.ToString();

            if (_Players.Contains(player)) contained = true;
            if (!contained)
            {
                string playerId = player.Substring(player.IndexOf("7"), 17);

                foreach (var playerNameId in _Players)
                {
                    if (playerNameId.Contains(playerId))
                    {
                        _Players.Add(player);
                        _Players.Remove(playerNameId);
                        contained = true;
                        break;
                    }
                }
            }
            return contained;
        }

        void oocChat(Player player, string cmd, string[] args)
        {
            if (args.Length < 1)
            {
                if (CheckPlayerOoc(player))
                {
                    _Players.Remove(player.ToString());
                    PrintToChat(player, GetMessage("ToggleOocOff", player.Id.ToString()));
                }
                else
                {
                    _Players.Add(player.ToString());
                    PrintToChat(player, GetMessage("ToggleOocOn", player.Id.ToString()));
                }
                return;
            }
            
            var Message = "";
            foreach (var message in args)
            {
                Message += " " + message;
            }

            string chatMessage = player.ChatFormat.Replace("%name%", player.DisplayName).Replace("%message%", Message);
            PrintToChat(chatPrefix + chatMessage);
        }
        #endregion

        #region Hooks
        private void OnPlayerChat(PlayerEvent e)
        {
            if (e is PlayerChatEvent)
            {
                var chat = (PlayerChatEvent)e;
                if (chat is PlayerWhisperEvent) return;
                if (chat is GuildMessageEvent) return;
                if (chat is PlayerLocalChatEvent) return;
                if (CheckPlayerOoc(e.Player))
                {
                    string message = chat.Player.ChatFormat.Replace("%name%", chat.Player.DisplayName).Replace("%message%", chat.Message);
                    PrintToChat(chatPrefix + message);
                    chat.Cancel();
                }
            }
        }

        private void OnPlayerDisconnected(Player player)
        {
            if (CheckPlayerOoc(player))
            {
                _Players.Remove(player.ToString());
            }
        }

        /*private void SendHelpText(Player player)
        {
            PrintToChat(player, "[0000FF]OOC Commands[FFFFFF]");
            PrintToChat(player, "[00FF00]/ooc [FFFFFF]- Toggle talking Out of Character in chat.");
            PrintToChat(player, "[00FF00]/ooc (Message) [FFFFFF]- Say an Out of Character message in chat.");
        }*/
        #endregion

        #region Helpers
        T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);
        #endregion
    }
}