using System;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using System.Text;

namespace Oxide.Plugins
{

                //
                // Credit to the original author, Dora
                //

    [Info("GuessTheNumber", "redBDGR", "2.0.2", ResourceId = 2023)]
    [Description("An event that requires player to guess the correct number")]

    class GuessTheNumber : RustPlugin
    {
        public Dictionary<ulong, int> playerInfo = new Dictionary<ulong, int>();

        bool useEconomics = true;
        bool useServerRewards = false;
        bool autoEventsEnabled = false;
        float autoEventTime = 600f;
        float eventLength = 30f;
        int minDefault = 1;
        int maxDefault = 1000;
        int maxTries = 1;
        int economicsWinReward = 20;
        int serverRewardsWinReward = 20;

        const string permissionNameADMIN = "guessthenumber.admin";
        const string permissionNameENTER = "guessthenumber.enter";

        [PluginReference] Plugin Economics;
        [PluginReference] Plugin ServerRewards;

        bool Changed = false;
        bool eventActive = false;
        Timer eventTimer;
        Timer autoRepeatTimer;
        int minNumber = 0;
        int maxNumber = 0;
        bool hasEconomics = false;
        bool hasServerRewards = false;
        int number = 0;

        void LoadVariables()
        {
            useEconomics = Convert.ToBoolean(GetConfig("Settings", "Use Economics", true));
            useServerRewards = Convert.ToBoolean(GetConfig("Settings", "Use ServerRewards", false));
            autoEventsEnabled = Convert.ToBoolean(GetConfig("Settings", "Auto Events Enabled", false));
            autoEventTime = Convert.ToSingle(GetConfig("Settings", "Auto Event Repeat Time", 600f));
            eventLength = Convert.ToSingle(GetConfig("Settings", "Event Length", 30f));
            minDefault = Convert.ToInt32(GetConfig("Settings", "Min Default Number", 1));
            maxDefault = Convert.ToInt32(GetConfig("Settings", "Max Default Number", 100));
            maxTries = Convert.ToInt32(GetConfig("Settings", "Max Tries", 1));
            economicsWinReward = Convert.ToInt32(GetConfig("Settings", "Economics Win Reward", 20));
            serverRewardsWinReward = Convert.ToInt32(GetConfig("Settings", "ServerRewards Win Reward", 20));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        void Init()
        {
            permission.RegisterPermission(permissionNameADMIN, this);
            permission.RegisterPermission(permissionNameENTER, this);
            LoadVariables();
        }

        private void OnServerInitialized()
        {
            LoadVariables();

            if (autoEventsEnabled)
                autoRepeatTimer = timer.Repeat(autoEventTime, 0, () =>
                {
                    minNumber = minDefault;
                    maxNumber = maxDefault;
                    StartEvent();
                });
            
            // External plugin checking
            if (!Economics)
                hasEconomics = false;
            else
                hasEconomics = true;

            if (!ServerRewards)
                hasServerRewards = false;
            else
                hasServerRewards = true;
        }

        void Loaded()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["No Permission"] = "You cannot use this command!",
                ["Event Already Active"] = "There is currently already an event that is active!",
                ["Event Started"] = "A random number event has started, correctly guess the random number (between {0} and {1}) to win a prize! use /guess <number> to enter",
                ["Help Message"] = "<color=#cccc00>/gtn start</color> (this will use the default min/max set in the config)",
                ["Help Message1"] = "<color=#cccc00>/gtn start <min number> <max number></color> (allows you to set custom min/max numbers)",
                ["Help Message2"] = "<color=#cccc00>/gtn end</color> (will end the current event)",
                ["No Event"] = "There are no current events active",
                ["Max Tries"] = "You have already guessed the maximum number of times",
                ["Event Win"] = "{0} has won the event! (correct number was {1})",
                ["Economics Reward"] = "You have earned ${0} for guessing the correct number!",
                ["ServerRewards Reward"] = "You have earned {0} RP for guessing the correct number!",
                ["Wrong Number"] = "You guessed the wrong number (you have {0} tries left)",
                ["/guess Invalid Syntax"] = "Invalid syntax! /guess <number>",
                ["Event Timed Out"] = "The event time has run out and no one successfully guessed the number!",
                ["Invalid Guess Entry"] = "The guess you entered was invalid! numbers only please",
                ["Event Created"] = "The event has been succesfully created, the winning number is {0}",
                ["GTN console invalid syntax"] = "Invalid syntax! gtn <start/end> <min number> <max number>",

            }, this);
        }

        [ConsoleCommand("gtn")]
        void GTNCONSOLECMD(ConsoleSystem.Arg args)
        {
            //args.ReplyWith("test");
            if (args.Connection != null)
                return;
            if (args.Args == null)
            {
                args.ReplyWith(msg("GTN console invalid syntax"));
                return;
            }
            if (args.Args.Length == 0)
            {
                args.ReplyWith(msg("GTN console invalid syntax"));
                return;
            }
            if (args.Args.Length > 3)
            {
                args.ReplyWith(msg("GTN console invalid syntax"));
                return;
            }
            if (args.Args[0] == null)
            {
                args.ReplyWith(msg("GTN console invalid syntax"));
                return;
            }
            if (args.Args[0] == "start")
            {
                if (eventActive)
                {
                    args.ReplyWith(msg("Event Already Active"));
                    return;
                }
                if (args.Args.Length == 3)
                {
                    minNumber = Convert.ToInt32(args.Args[1]);
                    maxNumber = Convert.ToInt32(args.Args[2]);
                    if (minNumber != 0 && maxNumber != 0)
                    {
                        number = Convert.ToInt32(Math.Round(Convert.ToDouble(UnityEngine.Random.Range(Convert.ToSingle(minNumber), Convert.ToSingle(maxNumber)))));
                        StartEvent();
                        args.ReplyWith(string.Format(msg("Event Created"), number.ToString()));
                    }
                    else
                    {
                        args.ReplyWith(msg("Invalid Params"));
                        return;
                    }
                }
                else
                {
                    minNumber = minDefault;
                    maxNumber = maxDefault;
                    number = Convert.ToInt32(Math.Round(Convert.ToDouble(UnityEngine.Random.Range(Convert.ToSingle(minNumber), Convert.ToSingle(maxNumber)))));
                    StartEvent();
                    args.ReplyWith(string.Format(msg("Event Created"), number.ToString()));
                }
                if (autoEventsEnabled)
                    if (!autoRepeatTimer.Destroyed)
                    {
                        autoRepeatTimer.Destroy();
                        autoRepeatTimer = timer.Repeat(autoEventTime, 0, () => StartEvent());
                    }
                return;
            }
            else if (args.Args[0] == "end")
            {
                if (eventActive == false)
                {
                    args.ReplyWith(msg("No Event"));
                    return;
                }
                if (!eventTimer.Destroyed || eventTimer != null)
                    eventTimer.Destroy();
                if (autoEventsEnabled)
                    if (!autoRepeatTimer.Destroyed)
                    {
                        autoRepeatTimer.Destroy();
                        autoRepeatTimer = timer.Repeat(autoEventTime, 0, () => StartEvent());
                    }
                eventActive = false;
                args.ReplyWith("The current event has been cancelled");
                rust.BroadcastChat(msg("Event Timed Out"));
            }
            else
                args.ReplyWith(msg("GTN console invalid syntax"));
            return;
        }

        [ChatCommand("gtn")]
        private void startGuessNumberEvent(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionNameADMIN))
            {
                player.ChatMessage(msg("No Permission", player.UserIDString));
                return;
            }
            if (args.Length == 0)
            {
                player.ChatMessage(DoHelpMenu());
                return;
            }
            if (args.Length > 3)
            {
                player.ChatMessage(DoHelpMenu());
                return;
            }
            if (args[0] == null)
            {
                player.ChatMessage(DoHelpMenu());
                return;
            }
            if (args[0] == "start")
            {
                if (eventActive)
                {
                    player.ChatMessage(msg("Event Already Active", player.UserIDString));
                    return;
                }
                if (args.Length == 3)
                {
                    minNumber = Convert.ToInt32(args[1]);
                    maxNumber = Convert.ToInt32(args[2]);
                    if (minNumber != 0 && maxNumber != 0)
                    {
                        number = Convert.ToInt32(Math.Round(Convert.ToDouble(UnityEngine.Random.Range(Convert.ToSingle(minNumber), Convert.ToSingle(maxNumber)))));
                        StartEvent();
                        player.ChatMessage(string.Format(msg("Event Created", player.UserIDString), number.ToString()));
                    }
                    else
                    {
                        player.ChatMessage(msg("Invalid Params", player.UserIDString));
                        return;
                    }
                }
                else
                {
                    minNumber = minDefault;
                    maxNumber = maxDefault;
                    number = Convert.ToInt32(Math.Round(Convert.ToDouble(UnityEngine.Random.Range(Convert.ToSingle(minNumber), Convert.ToSingle(maxNumber)))));
                    StartEvent();
                    player.ChatMessage(string.Format(msg("Event Created", player.UserIDString), number.ToString()));
                }
                if (autoEventsEnabled)
                    if (!autoRepeatTimer.Destroyed)
                    {
                        autoRepeatTimer.Destroy();
                        autoRepeatTimer = timer.Repeat(autoEventTime, 0, () => StartEvent());
                    }
                return;
            }
            else if (args[0] == "end")
            {
                if (eventActive == false)
                {
                    player.ChatMessage(msg("No Event", player.UserIDString));
                    return;
                }
                if (!eventTimer.Destroyed || eventTimer != null)
                    eventTimer.Destroy();
                if (autoEventsEnabled)
                    if (!autoRepeatTimer.Destroyed)
                    {
                        autoRepeatTimer.Destroy();
                        autoRepeatTimer = timer.Repeat(autoEventTime, 0, () => StartEvent());
                    }
                eventActive = false;
                rust.BroadcastChat(msg("Event Timed Out"));
            }
            else
                player.ChatMessage(msg("Help Message", player.UserIDString));
            return;
        }

        [ChatCommand("guess")]
        private void numberReply(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionNameENTER))
            {
                player.ChatMessage(msg("No Permission", player.UserIDString));
                return;
            }
            if (!eventActive)
            {
                player.ChatMessage(msg("No Event", player.UserIDString));
                return;
            }

            if (args.Length == 1)
            {
                if (!IsNumber(args[0]))
                {
                    player.ChatMessage(msg("Invalid Guess Entry", player.UserIDString));
                    return;
                }
                int playerNum = Convert.ToInt32(args[0]);
                if (!playerInfo.ContainsKey(player.userID))
                    playerInfo.Add(player.userID, 0);
                if (playerInfo[player.userID] >= maxTries)
                {
                    player.ChatMessage(msg("Max Tries", player.UserIDString));
                    return;
                }
                if (args[0] == "0")
                {
                    player.ChatMessage("You are not allowed to guess this number");
                    return;
                }

                rust.BroadcastChat($"{player.displayName} guessed {args[0].ToString()}");
                if (playerNum == number)
                {
                    rust.BroadcastChat(string.Format(msg("Event Win", player.UserIDString), player.displayName, number.ToString()));
                    if (hasEconomics)
                    {
                        if (useEconomics)
                        {
                            Economics.CallHook("Deposit", player.userID, economicsWinReward);
                            player.ChatMessage(string.Format(msg("Economics Reward", player.UserIDString), economicsWinReward.ToString()));
                        }
                    }

                    if (hasServerRewards)
                    {
                        if (useServerRewards)
                        {
                            ServerRewards?.Call("AddPoints", new object[] { player.userID, serverRewardsWinReward });
                            player.ChatMessage(string.Format(msg("ServerRewards Reward", player.UserIDString), economicsWinReward.ToString()));
                        }
                    }
                    number = 0;
                    eventActive = false;
                    playerInfo.Clear();
                    eventTimer.Destroy();
                    autoRepeatTimer = timer.Repeat(autoEventTime, 0, () => StartEvent());
                }
                else
                {
                    playerInfo[player.userID]++;
                    player.ChatMessage(string.Format(msg("Wrong Number", player.UserIDString), (playerInfo[player.userID] - maxTries).ToString()));
                }
            }
            else
                player.ChatMessage(msg("/guess Invalid Syntax", player.UserIDString));
            return;
        }

        void StartEvent()
        {
            if (eventActive)
                return;
            rust.BroadcastChat(string.Format(msg("Event Started"), minNumber.ToString(), maxNumber.ToString()));
            eventActive = true;
            eventTimer = timer.Once(eventLength, () =>
            {
                rust.BroadcastChat(msg("Event Timed Out"));
                eventActive = false;
                playerInfo.Clear();
            });
        }

        string DoHelpMenu()
        {
            StringBuilder x = new StringBuilder();
            x.AppendLine(msg("Help Message"));
            x.AppendLine(msg("Help Message1"));
            x.AppendLine(msg("Help Message2"));
            return x.ToString().TrimEnd();
        }

        bool IsNumber(string str)
        {
            foreach (char c in str)
                if (c < '0' || c > '9')
                    return false;
            return true;
        }

        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        string msg(string key, string id = null) => lang.GetMessage(key, this, id);
    }
}
