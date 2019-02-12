using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("CustomChatCommands", "PsychoTea", "2.1.1")]
    [Description("Allows you to set up custom commands.")]

    class CustomChatCommands : CovalencePlugin
    {
        class ChatCommand
        {
            public string Command;
            public List<string> Messages;
            public string Permission;
            public List<string> ConsoleCmd;
            public ulong UserID;
            public bool Broadcast;
            public List<string> RconCmd;
            public float Cooldown;
            public int MaxUses;

            public ChatCommand(string Command, List<string> Messages, string Permission = "", List<string> ConsoleCmd = null, ulong UserID = 0, bool Broadcast = false, List<string> RconCmd = null, float Cooldown = 0f, int MaxUses = 0)
            {
                this.Command = Command;
                this.Messages = Messages;
                this.Permission = Permission;
                this.ConsoleCmd = ConsoleCmd;
                this.UserID = UserID;
                this.Broadcast = Broadcast;
                this.RconCmd = RconCmd;
                this.Cooldown = Cooldown;
                this.MaxUses = MaxUses;
            }
        }

        class Cooldown
        {
            public string CommandName;
            public double ExpiryPoint;

            public Cooldown(string CommandName, double ExpiryPoint)
            {
                this.CommandName = CommandName;
                this.ExpiryPoint = ExpiryPoint;
            }
        }

        class MaxUses
        {
            public string CommandName;
            public int Uses;

            public MaxUses(string CommandName, int Uses)
            {
                this.CommandName = CommandName;
                this.Uses = Uses;
            }
        }

        #region Config

        ConfigFile config;

        class ConfigFile
        {
            [JsonProperty(PropertyName = "Reset Cooldowns On New Map")]
            public bool ResetCooldownsOnNewMap;

            [JsonProperty(PropertyName = "Reset Max Uses On New Map")]
            public bool ResetMaxUsesOnNewMap;

            [JsonProperty(PropertyName = "Reset Max Uses At Midnight")]
            public bool ResetMaxUsesAtMidnight;

            public List<ChatCommand> Commands;

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    ResetCooldownsOnNewMap = true,
                    ResetMaxUsesOnNewMap = true,
                    ResetMaxUsesAtMidnight = true,
                    Commands = new List<ChatCommand>()
                    {
                        new ChatCommand("sinfo", new List<string>() { "<color=lime>Insert your server info here!</color>" }),
                        new ChatCommand("website", new List<string>() { "Insert your server website here! This is broadcasted to all users!" }, "customchatcommands.admin", null, 0, true, null, 30f, 3),
                        new ChatCommand("adminhelp", new List<string>() { "Password for TeamSpeak channel: xyz", "Discord invite: website.com/discord" }, "customchatcommands.admin"),
                        new ChatCommand("noclip", new List<string>() { "NoClip toggled." }, "customchatcommands.admin", new List<string> { "noclip" }, 0, false, new List<string>() { "say {player.name} / {player.id} has used the /noclip command!" })
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<ConfigFile>();
        }

        protected override void LoadDefaultConfig() => config = ConfigFile.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Data

        class StoredData
        {
            public Dictionary<ulong, List<Cooldown>> Cooldowns = new Dictionary<ulong, List<Cooldown>>();
            public Dictionary<ulong, List<MaxUses>> MaxUses = new Dictionary<ulong, List<MaxUses>>();
        }
        StoredData storedData;

        void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(this.Title, storedData);

        void ReadData() => storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(this.Title);

        #endregion

        #region Oxide Hooks

        void Init()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "Command On Cooldown - Minutes", "This command is on cooldown for another {0} minutes." },
                { "Command On Cooldown - Hours", "This command is on cooldwon for another {0} hours." },
                { "Max Uses Reached", "You have reached the maximum of {0} uses for this command." }
            }, this);

            foreach (var command in config.Commands)
            {
                AddCovalenceCommand(command.Command, "CustomCommand", command.Permission);
            }

            ReadData();

            if (config.ResetMaxUsesAtMidnight)
            {
                timer.Every(60f, () =>
                {
                    if (DateTime.Now.Hour == 0 && DateTime.Now.Minute == 0)
                    {
                        ResetMaxUses();
                    }
                });
            }
        }

        void Unload() => SaveData();

        void OnNewSave()
        {
            if (config.ResetCooldownsOnNewMap)
            {
                ResetCooldowns();
            }

            if (config.ResetMaxUsesOnNewMap)
            {
                ResetMaxUses();
            }
        }

        void OnServerSave() => SaveData();

        #endregion

        #region Commands

        void CustomCommand(IPlayer player, string command, string[] args)
        {
            ulong userID = ulong.Parse(player.Id);

            var cmd = config.Commands.SingleOrDefault(x => x.Command.ToLower() == command.ToLower());
            if (cmd == null) return;

            if (!CanUseCommand(userID, cmd.Command))
            {
                player.Message(GetMessage("Max Uses Reached", player.Id, cmd.MaxUses.ToString()));
                return;
            }

            if (IsOnCooldown(userID, cmd.Command))
            {
                double timeRemaining = CooldownRemaining(userID, cmd.Command);

                double hours;
                if (IsHours(timeRemaining, out hours))
                {
                    Puts(hours.ToString());
                    player.Message(GetMessage("Command On Cooldown - Hours", player.Id, hours.ToString("N1")));
                    return;
                }

                double minutes = timeRemaining / 60;
                player.Message(GetMessage("Command On Cooldown - Minutes", player.Id, minutes.ToString("N1")));
                
                return;
            }

            if (cmd.Broadcast) BroadcastMessages(cmd.Messages, cmd.UserID);
            else SendMessages(player, cmd.Messages, cmd.UserID);

            if (cmd.ConsoleCmd != null && cmd.ConsoleCmd.Count > 0)
                foreach (var consoleCmd in cmd.ConsoleCmd)
                    player.Command(consoleCmd);

            if (cmd.RconCmd != null && cmd.RconCmd.Count > 0)
            {
                foreach (var rconCmd in cmd.RconCmd)
                {
                    var newCmd = rconCmd.Replace("{player.name}", player.Name)
                                        .Replace("{player.id}", player.Id);
                    server.Command(newCmd);
                }
            }

            if (cmd.Cooldown > 0f) AddCooldown(userID, cmd.Command, cmd.Cooldown);
            if (cmd.MaxUses > 0) AddUse(userID, cmd.Command);
        }

        [Command("ccc.resetcooldowns")]
        void CCCResetCooldowns(IPlayer player, string command, string[] args)
        {
            if (player.Id != "server_console") return;

            ResetCooldowns();
            player.Message($"[{this.Title}] Reset all cooldowns.");
        }

        [Command("ccc.resetmaxuses")]
        void CCCResetMaxUses(IPlayer player, string command, string[] args)
        {
            if (player.Id != "server_console") return;

            ResetMaxUses();
            player.Message($"[{this.Title}] Reset all max uses.");
        }

        #endregion

        #region Functions

        void SendMessages(IPlayer player, List<string> messages, ulong userID = 0)
        {
            foreach (var message in messages)
            {
                #if RUST
                var basePlayer = player.Object as BasePlayer;
                basePlayer?.SendConsoleCommand("chat.add", userID, message);
                #else
                player.Message(message);
                #endif
            }
        }

        void BroadcastMessages(List<string> messages, ulong userID = 0) => players.Connected.ToList().ForEach(x => SendMessages(x, messages, userID));

        void AddCooldown(ulong userID, string commandName, float time)
        {
            if (!storedData.Cooldowns.ContainsKey(userID))
            {
                storedData.Cooldowns.Add(userID, new List<Cooldown>());
            }

            var search = storedData.Cooldowns[userID].Where(x => x.CommandName.ToLower() == commandName.ToLower());
            if (search.Any()) search.ToList().ForEach(x => storedData.Cooldowns[userID].Remove(x));

            var cooldown = new Cooldown(commandName, TimeSinceEpoch() + time);
            storedData.Cooldowns[userID].Add(cooldown);
            SaveData();
        }

        bool IsOnCooldown(ulong userID, string commandName)
        {
            if (!storedData.Cooldowns.ContainsKey(userID)) return false;

            var cooldown = storedData.Cooldowns[userID].SingleOrDefault(x => x.CommandName.ToLower() == commandName.ToLower());
            if (cooldown == null) return false;

            return cooldown.ExpiryPoint > TimeSinceEpoch();
        }

        double CooldownRemaining(ulong userID, string commandName)
        {
            if (!storedData.Cooldowns.ContainsKey(userID)) return -1f;

            var cooldown = storedData.Cooldowns[userID].SingleOrDefault(x => x.CommandName.ToLower() == commandName.ToLower());
            return cooldown.ExpiryPoint - TimeSinceEpoch();
        }

        void ResetCooldowns()
        {
            storedData.Cooldowns.Clear();
            SaveData();
        }

        void AddUse(ulong userID, string commandName)
        {
            if (!storedData.MaxUses.ContainsKey(userID))
            {
                storedData.MaxUses.Add(userID, new List<MaxUses>());
            }

            var foundUses = storedData.MaxUses[userID].SingleOrDefault(x => x.CommandName.ToLower() == commandName.ToLower());
            if (foundUses != null)
            {
                foundUses.Uses++;
                return;
            }

            var maxUses = new MaxUses(commandName, 1);
            storedData.MaxUses[userID].Add(maxUses);
            SaveData();
        }

        bool CanUseCommand(ulong userID, string commandName)
        {
            var configMax = config.Commands.Where(x => x.Command.ToLower() == commandName.ToLower()).FirstOrDefault()?.MaxUses;
            if (configMax == null || configMax < 1) return true;

            if (!storedData.MaxUses.ContainsKey(userID)) return true;

            var playerMax = storedData.MaxUses[userID].SingleOrDefault(x => x.CommandName.ToLower() == commandName.ToLower());
            if (playerMax == null) return true;

            return playerMax.Uses < configMax;
        }

        void ResetMaxUses()
        {
            storedData.MaxUses.Clear();
            SaveData();
        }

        double TimeSinceEpoch() => (DateTime.UtcNow - DateTime.MinValue).TotalSeconds;

        #endregion

        #region Helpers

        bool IsHours(double seconds, out double hours)
        {
            hours = seconds / (60d * 60d);

            return hours >= 1;
        }
        
        string GetMessage(string key, string userID, params string[] args) => string.Format(lang.GetMessage(key, this, userID), args);

        #endregion
    }
}