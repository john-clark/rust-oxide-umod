using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("BetterChatFlood", "Ryan", "1.0.5")]
    [Description("Adds a cooldown to chat to prevent flooding")]
    public class BetterChatFlood : CovalencePlugin
    {
        [PluginReference]
        private Plugin BetterChat;
        private ConfigFile _Config;
        private Dictionary<string, DateTime> cooldowns = new Dictionary<string, DateTime>();
        private Dictionary<string, int> thresholds = new Dictionary<string, int>();
        private bool canBetterChat = true;
        private const string permBypass = "betterchatflood.bypass";

        public class ConfigFile
        {
            [JsonProperty(PropertyName = "Cooldown Period (seconds)")]
            public float cooldown;

            [JsonProperty(PropertyName = "Number of messages before cooldown")]
            public int threshold;

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    cooldown = 5f,
                    threshold = 3
                };
            }
        }

        protected override void LoadDefaultConfig() => _Config = ConfigFile.DefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _Config = Config.ReadObject<ConfigFile>();
        }

        protected override void SaveConfig() => Config.WriteObject(_Config);

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Cooldown"] = "Try again in {0} seconds"
            }, this);
        }

        private void Init() => permission.RegisterPermission(permBypass, this);

        private void OnServerInitialized()
        {
            if (!BetterChat) Unsubscribe(nameof(OnBetterChat));
            if (BetterChat && BetterChat.Version < new VersionNumber(5, 0, 6))
            {
                Unsubscribe(nameof(OnBetterChat));
                PrintWarning("This plugin is only compatable with BetterChat version 5.0.6 or greater!");
            }
        }

        private void Unload() => cooldowns.Clear();

        private double GetNextMsgTime(IPlayer player)
        {
            if (cooldowns[player.Id].AddSeconds(_Config.cooldown) > DateTime.Now)
                return Math.Ceiling((cooldowns[player.Id].AddSeconds(_Config.cooldown) - DateTime.Now).TotalSeconds);
            return 0;
        }

        private object IsFlooding(IPlayer player, string action = null)
        {
            if (cooldowns.ContainsKey(player.Id))
            {
                if (permission.UserHasPermission(player.Id, permBypass)) return null;

                var hasCooldown = GetNextMsgTime(player) > 0;
                if (hasCooldown)
                {
                    if (thresholds.ContainsKey(player.Id))
                    {
                        if (thresholds[player.Id] > _Config.threshold)
                        {
                            if (action != null)
                                player.Message(string.Format(lang.GetMessage("Cooldown", this, player.Id), GetNextMsgTime(player)));
                            return true;
                        }

                        if (action != null)
                        {
                            thresholds[player.Id] = ++thresholds[player.Id];
                            cooldowns.Remove(player.Id);
                            cooldowns.Add(player.Id, DateTime.Now);
                        }
                        return null;
                    }

                    if (!thresholds.ContainsKey(player.Id))
                        thresholds.Add(player.Id, 1);
                    return null;
                }

                if (!hasCooldown && cooldowns.ContainsKey(player.Id))
                {
                    cooldowns.Remove(player.Id);
                    if (thresholds.ContainsKey(player.Id))
                        thresholds.Remove(player.Id);
                }
            }

            if (action != null && !cooldowns.ContainsKey(player.Id))
                cooldowns.Add(player.Id, DateTime.Now);

            return null;
        }

        private object OnUserChat(IPlayer player, string message) => IsFlooding(player, "chat");

        private object OnBetterChat(Dictionary<string, object> data) => IsFlooding(data["Player"] as IPlayer);
    }
}
