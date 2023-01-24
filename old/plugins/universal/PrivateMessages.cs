using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;
using System;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Private Messages", "PaiN", "0.4.0", ResourceId = 2046)]
    class PrivateMessages : CovalencePlugin
    {
        public static List<PrivateMessage> PM = new List<PrivateMessage>();
        public static Dictionary<string, string> lastplayer = new Dictionary<string, string>();
        public static Dictionary<string, double> cooldown = new Dictionary<string, double>();

        public static PrivateMessages Plugin;
        public static Plugin UFilter;

        private static bool UseUFilter;
        private static bool UseCooldown;
        private static int Cooldown_Seconds;
        private bool Changed;

        public class PrivateMessage
        {
            KeyValuePair<string, string> participants = new KeyValuePair<string, string>();
            List<string> history = new List<string>(5);

            public static PrivateMessage Find(string sender, string target) => PM.Find(x => 
                (x.participants.Key == sender && x.participants.Value == target) ||
                (x.participants.Key == target && x.participants.Value == sender)
            );

            public static PrivateMessage FindOrCreate(string sender, string target)
            {
                PrivateMessage msg = Find(sender, target);

                if (msg == null)
                {
                    msg = new PrivateMessage();
                    msg.participants = new KeyValuePair<string, string>(sender, target);
                    PM.Add(msg);
                }

                return msg;
            }

            public static void SendPM(IPlayer sender, IPlayer target, string message)
            {
                if(sender.Id == target.Id)
                {
                    sender.Message(LangMsg("NOT_PM_SELF", sender.Id));
                    return;
                }

                if (UFilter != null && UseUFilter == true)
                {
                    object filter = (object)UFilter?.Call("ProcessText", message, sender);

                    if (filter == null)
                        return;

                    message = filter.ToString();
                }

                if (UseCooldown)
                {
                    double time;
                    if (cooldown.TryGetValue(sender.Id, out time))
                    {
                        if (time > GetTimeStamp())
                        {
                            sender.Message(string.Format(LangMsg("COOLDOWN_MSG", sender.Id), Math.Round(time - GetTimeStamp(), 2)));
                            return;
                        }
                        else
                            cooldown.Remove(sender.Id);
                    }

                    cooldown.Add(sender.Id, GetTimeStamp() + Cooldown_Seconds);
                }
                PrivateMessage msg = FindOrCreate(sender.Id, target.Id);

                target.Message(string.Format(LangMsg("PM_FROM"), sender.Name, message));
                sender.Message(string.Format(LangMsg("PM_TO"), target.Name, message));

                lastplayer[sender.Id] = target.Id;
                lastplayer[target.Id] = sender.Id;

                msg.history.Add(string.Format(LangMsg("HISTORY_SYNTAX"), sender.Name, target.Name, message));

                if (msg.history.Count == 6)
                    msg.history.Remove(msg.history.First());
            }

            public static List<string> GetHistory(string sender, string target)
            {
                PrivateMessage pm = Find(sender, target);

                if(pm == null)
                {
                    IPlayer player = Plugin.covalence.Players.FindPlayerById(sender);
                    player.Message(LangMsg("NOT_SAVED_HISTORY", player.Id));
                    return null;
                }

                return pm.history;
            }

        }

        void Init()
        {
            Plugin = this;
            LoadMessages();
            UFilter = plugins.Find("UFilter");
            LoadVariables();
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new configuration file!");
            Config.Clear();
            LoadVariables();
        }

        void LoadVariables()
        {
            UseUFilter = Convert.ToBoolean(GetConfig("Settings", "UFilter_Enabled", false));
            UseCooldown = Convert.ToBoolean(GetConfig("Settings", "Cooldown_Enabled", false));
            Cooldown_Seconds = Convert.ToInt32(GetConfig("Settings", "Cooldown_Seconds", 3));

            if (Changed)
            {
                SaveConfig();
                Changed = false;

            }
        }

        void LoadMessages()
        {
            Dictionary<string, string> messages = new Dictionary<string, string>
            {
                ["PLAYER_NOT_FOUND"] = "Player not found!",
                ["PM_TO"] = "[PM] TO <color=orange>{0}</color>: {1}",
                ["PM_FROM"] = "[PM] FROM <color=lime>{0}</color>: {1}",
                ["PM_ARG_SYNTAX"] = "Syntax: /pm <player> <message>",
                ["HISTORY_ARG_SYNTAX"] = "Syntax: /pmhistory <player>",
                ["HISTORY_SYNTAX"] = "[PM-H] <color=lime>{0}</color> TO <color=orange>{1}</color>: {2}",
                ["NOT_ENOUGH_ARGUMENTS"] = "Incorrect amount of arguments!",
                ["PLAYER_NOT_ONLINE"] = "This player is not online!",
                ["NOT_SAVED_HISTORY"] = "There is not any saved pm history with this player.",
                ["NOT_RECENT_PMS"] = "You don't have any recent PMs to reply to.",
                ["NOT_PM_SELF"] = "You can't send a message to yourself!",
                ["COOLDOWN_MSG"] = "You will be able to send a private message in {0} seconds",
            };
            lang.RegisterMessages(messages, this);
        }

        [Command("pm")]
        void cmdPM(IPlayer player, string cmd, string[] args)
        {
            if(args.Length == 0)
            {
                player.Message(LangMsg("PM_ARG_SYNTAX", player.Id));
                return;
            }

            if (args.Length > 0)
            {
                IPlayer target = covalence.Players.FindPlayer(args[0]);
                if (target == null)
                {
                    player.Message(LangMsg("PLAYER_NOT_FOUND", player.Id));
                    return;
                }

                string msg = string.Join(" ", args.Skip(1).ToArray());

                PrivateMessage.SendPM(player, target, msg);
            }
        }

        [Command("pmhistory")]
        void cmdHistory(IPlayer player, string cmd, string[] args)
        {
            if (args.Length == 0)
            {
                player.Message(LangMsg("HISTORY_ARG_SYNTAX", player.Id));
                return;
            }

            if (args.Length == 1)
            {
                IPlayer target = covalence.Players.FindPlayer(args[0]);
                if (target == null)
                {
                    player.Message(LangMsg("PLAYER_NOT_FOUND", player.Id));
                    return;
                }
                List<string> history = PrivateMessage.GetHistory(player.Id, target.Id);

                if (history == null)
                    return;

                string msg = string.Join(Environment.NewLine, history.ToArray());
                player.Message(msg);
            }
        }

        [Command("r")]
        void cmdReply(IPlayer player, string cmd, string[] args)
        {
            if(args.Length == 0)
            {
                player.Message(LangMsg("NOT_ENOUGH_ARGUMENTS", player.Id));
                return;
            }

            string steamId;
            if(lastplayer.TryGetValue(player.Id, out steamId))
            {
                IPlayer target = covalence.Players.FindPlayerById(steamId);
                if(target == null)
                {
                    player.Message(LangMsg("PLAYER_NOT_ONLINE", player.Id));
                    return;
                }

                string msg = string.Join(" ", args);
                PrivateMessage.SendPM(player, target, msg);
            }
            else
            {
                player.Message(LangMsg("NOT_RECENT_PMS", player.Id));
                return;
            }
        }

        static string LangMsg(string msg, string uid = null) => Plugin.lang.GetMessage(msg, Plugin, uid);
        static double GetTimeStamp()
        {
            return (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
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
    }
}