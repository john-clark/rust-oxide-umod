using Oxide.Plugins.TicketsExtensions;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using System.Reflection;
using System.Linq;
using Oxide.Core;
using System;

namespace Oxide.Plugins
{
    [Info("Tickets", "LaserHydra", "3.1.3")]
    [Description("Allows players to send tickets to admin")]
    public class Tickets : CovalencePlugin
    {
        #region Variable Declaration

        [PluginReference]
        private Plugin Slack, PushAPI, EmailAPI;

        public static Tickets Instance;

        public string ChatTitle;
        public string SlackChannel;

        public bool UseSlack;
        public bool UseEmailAPI;
        public bool UsePushAPI;

        public bool NotifyReply;
        public bool NotifyCreated;
        public bool NotifyClosed;
        public bool NotifyWiped;

        public string OpenStatus;
        public string ClosedStatus;

        #endregion

        #region Classes

        [AttributeUsage(AttributeTargets.Method)]
        private class CommandInfo : Attribute
        {
            protected string[] Commands;

            public static string[] GetCommands(IPlayer player, MethodBase method)
            {
                Attribute[] attributes = GetCustomAttributes(method);

                if (attributes.Any((a) => a is CommandInfo))
                {
                    CommandInfo commandInfo = (CommandInfo)attributes.First((a) => a is CommandInfo && !(a is AdminCommandInfo));
                    AdminCommandInfo adminCommandInfo = (AdminCommandInfo)attributes.First((a) => a is AdminCommandInfo);

                    string[] commands = null;

                    if (commandInfo != null)
                        commands = commandInfo.Commands;

                    if (adminCommandInfo != null && player.HasPermission("tickets.admin"))
                    {
                        if (commands == null)
                            commands = adminCommandInfo.Commands;
                        else
                            commands = commands.Concat(adminCommandInfo.Commands).ToArray();
                    }

                    return commands;
                }

                return new string[0];
            }

            public CommandInfo(params string[] commands)
            {
                this.Commands = commands;
            }
        }

        private class AdminCommandInfo : CommandInfo
        {
            public AdminCommandInfo(params string[] commands)
            {
                this.Commands = commands;
            }
        }

        private class Ticket
        {
            public static List<Ticket> All = new List<Ticket>();
            public static List<Ticket> Unread => All.Where((t) => !t.Read).ToList();

            public static List<Ticket> GetPlayerTickets(IPlayer player) => All.Where((t) => t.Creator.ID == player.Id).ToList();
            public static List<Ticket> GetOpenTickets() => All.Where((t) => !t.Closed).ToList();
            public static List<Ticket> GetClosedTickets() => All.Where((t) => t.Closed).ToList();

            public int ID;
            public bool Closed;
            public bool Read;
            public AuthorInfo Creator;
            public Position Position;
            public List<Reply> Replies = new List<Reply>();
            internal string FormattedReplies => string.Join(Environment.NewLine, Replies.Select((r) => r.ToString()).ToArray());

            public static int LastID => All.Count == 0 ? 0 : All.OrderByDescending(ticket => ticket.ID).ToList()[0].ID;

            public static Ticket Find(int id) => All.Find((t) => t.ID == id);

            public static Ticket Find(IPlayer player, int id) => All.Find((t) => t.ID == id && t.Creator.ID == player.Id);

            public static Ticket Create(IPlayer player, string message)
            {
                Ticket ticket = new Ticket(player);
                All.Add(ticket);

                ticket.Reply(player, message, true);

                return ticket;
            }

            public void Reply(IPlayer player, string message, bool justCreated = false)
            {
                var reply = new Reply
                {
                    Author = AuthorInfo.FromPlayer(player),
                    Message = message,
                    Date = Date.Now
                };

                Replies.Add(reply);

                Updated();

                if (!justCreated)
                    Instance.OnTicketReplied(this, reply);
                else
                    Instance.OnTicketCreated(this, reply);
            }

            public void Close(IPlayer player)
            {
                Closed = true;
                Updated();

                Instance.OnTicketClosed(this, player);
            }

            public string InsertDataToString(string str)
            {
                Dictionary<string, object> data = new Dictionary<string, object>
                {
                    { "{ID}", ID },
                    { "{Position}", Position },
                    { "{Replies}", FormattedReplies },
                    { "{Creator}", Creator },
                    { "{Status}", Closed ? Instance.ClosedStatus : Instance.OpenStatus }
                };

                foreach (var kvp in data)
                    str = str.Replace(kvp.Key, kvp.Value.ToString());

                return str;
            }

            public static void Updated() => Instance.SaveData(All);

            public Ticket()
            {
                ID = LastID + 1;
            }

            public Ticket(IPlayer player)
            {
                Creator = AuthorInfo.FromPlayer(player);
                ID = LastID + 1;
                Position = Position.FromGeneric(player.Position());
            }
        }

        private struct Reply
        {
            public AuthorInfo Author;
            public string Message;
            public Date Date;

            public override string ToString()
            {
                string str = Instance.LangMsg("Reply");

                Dictionary<string, object> data = new Dictionary<string, object>
                {
                    { "{Author}", Author },
                    { "{Message}", Message },
                    { "{Date}", Date }
                };

                foreach (var kvp in data)
                    str = str.Replace(kvp.Key, kvp.Value.ToString());

                return str;
            }
        }

        private struct AuthorInfo
        {
            public string Name;
            public string ID;

            internal static AuthorInfo FromPlayer(IPlayer player) => new AuthorInfo
            {
                Name = player.Name,
                ID = player.Id
            };

            public override string ToString() => $"{Name} ({ID})";
        }

        private class Position
        {
            public float X;
            public float Y;
            public float Z;

            public GenericPosition ToGeneric() => new GenericPosition(X, Y, Z);

            public static Position FromGeneric(GenericPosition genericPosition) => new Position
            {
                X = genericPosition.X,
                Y = genericPosition.Y,
                Z = genericPosition.Z
            };
        }

        private class Date
        {
            public int Second = 0;
            public int Minute = 0;
            public int Hour = 0;
            public int Day = 1;
            public int Month = 1;
            public int Year = 1;

            internal DateTime DateTime => new DateTime(Year, Month, Day, Hour, Minute, Second);

            internal static Date Now => FromDateTime(DateTime.Now);

            internal static Date FromDateTime(DateTime dateTime) => new Date
            {
                Second = dateTime.Second,
                Minute = dateTime.Minute,
                Hour = dateTime.Hour,
                Day = dateTime.Day,
                Month = dateTime.Month,
                Year = dateTime.Year
            };

            public override string ToString()
            {
                string str = Instance.LangMsg("Date Format");

                Dictionary<string, object> data = new Dictionary<string, object>
                {
                    { "{Year}", Year },
                    { "{Month}", Month },
                    { "{Day}", Day },
                    { "{Hour}", Hour },
                    { "{Minute}", Minute },
                    { "{Second}", Second }
                };

                foreach (var kvp in data)
                    str = str.Replace(kvp.Key, kvp.Value.ToString());

                return str;
            }
        }

        #endregion

        #region Hooks

        private void Loaded()
        {
            Instance = this;

            permission.RegisterPermission("tickets.admin", this);
            permission.RegisterPermission("tickets.wipe", this);

            LoadData(out Ticket.All);
            LoadMessages();
            LoadConfig();
        }

        private void OnTicketReplied(Ticket ticket, Reply reply)
        {
            if (NotifyReply)
            {
                string message = ticket.InsertDataToString(LangMsg("Reply Notification"));
                message = message.Replace("{Reply}", reply.ToString());

                Notify(message);
            }
        }

        private void OnTicketCreated(Ticket ticket, Reply reply)
        {
            if (NotifyCreated)
            {
                string message = ticket.InsertDataToString(LangMsg("Created Notification"));
                message = message.Replace("{Reply}", reply.ToString());

                Notify(message);
            }
        }

        private void OnTicketClosed(Ticket ticket, IPlayer player)
        {
            if (NotifyClosed)
                Notify(ticket.InsertDataToString(LangMsg("Closed Notification")).Replace("{Admin}", player.Name));
        }

        #endregion

        #region Loading

        protected override void LoadDefaultConfig() => PrintWarning("Generating new configurationfile...");

        private new void LoadConfig()
        {
            ChatTitle = GetConfig<string>("Title", "[#262626][[#C4FF00]Tickets[/#]][/#]");
            SlackChannel = GetConfig<string>("Slack Channel", "general");

            UseSlack = GetConfig<bool>("Use Slack", false);
            UseEmailAPI = GetConfig<bool>("Use Email API", false);
            UsePushAPI = GetConfig<bool>("Use Push API", false);

            NotifyReply = GetConfig<bool>("Notify About Replies", true);
            NotifyCreated = GetConfig<bool>("Notify About New Tickets", true);
            NotifyClosed = GetConfig<bool>("Notify About Closing", true);
            NotifyWiped = GetConfig<bool>("Notify About Wipes", true);

            OpenStatus = GetConfig<string>("Open Sign", "[#262626][[#C4FF00]OPEN[/#]][/#]");
            ClosedStatus = GetConfig<string>("Closed Sign", "[#262626][[#red]CLOSED[/#]][/#]");

            SaveConfig();
        }

        private void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Ticket List Item"] = "{Status} [#C4FF00]Ticket #{ID}[/#] by {Creator}",
                ["Ticket List Empty"] = "{Title} There are no tickets.",
                ["Ticket Created"] = "{Title} You have successfully created a ticket (#{ID})",
                ["Ticket View"] = "{Title} {Status} Viewing Ticket #{ID} by {Creator}:" + Environment.NewLine + Environment.NewLine + "{Replies}",
                ["Reply"] = "[#C4FF00]{Author}[/#] - {Date}" + Environment.NewLine + "{Message}",
                ["Date Format"] = "{Day}.{Month}.{Year} {Hour}:{Minute}",
                ["Ticket Closed"] = "{Title} You successfully closed ticket #{ID}.",
                ["Replied To Ticket"] = "{Title} You replied to ticket #{ID}.",
                ["Teleported To Ticket"] = "{Title} You teleported to ticket #{ID}.",
                ["Teleported Not Found"] = "{Title} Ticket #{ID} could not be found.",
                ["Reply Notification"] = "New Reply to Ticket #{ID}:" + Environment.NewLine + "{Reply}",
                ["Created Notification"] = "New Ticket #{ID}:" + Environment.NewLine + "{Reply}",
                ["Closed Notification"] = "Ticket #{ID} was closed by {Admin}",
                ["Wiped Notification"] = "{Admin} has deleted all existing tickets.",
                ["Ticket Is Closed"] = "{Title} Ticket #{ID} is closed. No further replies are allowed.",
                ["Already Closed"] = "{Title} Ticket is closed already.",
                ["Wiped"] = "All existing tickets have been deleted."
            }, this);
        }

        #endregion

        #region Commands

        [Command("ticket"),
         CommandInfo("view <ID> | view ticket", "create <Message> | write a new ticket", "reply <ID> <Message> | reply to a ticket", "list | list tickets"),
         AdminCommandInfo("listclosed | list closed tickets", "close <ID> | close a ticket", "teleport <ID> | teleport to a ticket's source position")]
        private void ticketCmd(IPlayer player, string cmd, string[] args)
        {
            if (args.Length == 0)
            {
                player.ReplySafe(GetCommandInfo(player, "ticket"));
                return;
            }

            bool admin = player.HasPermission("tickets.admin");
            Ticket ticket;
            int id;

            switch (args[0])
            {
                case "view":

                    if (args.Length < 2)
                    {
                        player.ReplySafe("Syntax: ticket view <ID>");
                        return;
                    }

                    if (!int.TryParse(args[1], out id))
                    {
                        player.ReplySafe($"Invalid ID Format. {id} is no valid number!");
                        return;
                    }

                    ticket = admin ? Ticket.Find(id) : Ticket.Find(player, id);

                    if (ticket == null)
                    {
                        player.ReplySafe(LangMsg("Ticket Not Found", player.Id));
                        return;
                    }

                    player.ReplySafe(ticket.InsertDataToString(LangMsg("Ticket View", player.Id)));

                    break;

                case "create":

                    if (args.Length < 2)
                    {
                        player.ReplySafe("Syntax: ticket create <Message>");
                        return;
                    }

                    ticket = Ticket.Create(player, string.Join(" ", args.Skip(1).ToArray()));
                    player.ReplySafe(ticket.InsertDataToString(LangMsg("Ticket Created", player.Id)));

                    break;

                case "reply":

                    if (args.Length < 3)
                    {
                        player.ReplySafe("Syntax: ticket reply <ID> <Message>");
                        return;
                    }

                    if (!int.TryParse(args[1], out id))
                    {
                        player.ReplySafe($"Invalid ID Format. {id} is no valid number!");
                        return;
                    }

                    ticket = admin ? Ticket.Find(id) : Ticket.Find(player, id);

                    if (ticket == null)
                    {
                        player.ReplySafe(LangMsg("Ticket Not Found", player.Id));
                        return;
                    }

                    if (ticket.Closed)
                    {
                        if (admin)
                            ticket.Closed = false;
                        else
                        {
                            player.ReplySafe(ticket.InsertDataToString(LangMsg("Ticket Is Closed", player.Id)));
                            return;
                        }
                    }

                    ticket.Reply(player, string.Join(" ", args.Skip(2).ToArray()));
                    player.ReplySafe(ticket.InsertDataToString(LangMsg("Replied To Ticket", player.Id)));

                    break;

                case "list":

                    List<Ticket> tickets = admin ? Ticket.GetOpenTickets() : Ticket.GetPlayerTickets(player);
                    string[] ticketListItems = tickets.Select((t) => t.InsertDataToString(LangMsg("Ticket List Item", player.Id))).ToArray();

                    player.ReplySafe(ticketListItems.Length == 0 ? LangMsg("Ticket List Empty", player.Id) : string.Join(Environment.NewLine, ticketListItems));

                    break;

                // Admin Commands

                case "listclosed":

                    if (!admin)
                        goto default;

                    List<Ticket> closedTickets = Ticket.GetClosedTickets();
                    string[] closedTicketListItems = closedTickets.Select((t) => t.InsertDataToString(LangMsg("Ticket List Item", player.Id))).ToArray();

                    player.ReplySafe(closedTicketListItems.Length == 0 ? LangMsg("Ticket List Empty", player.Id) : string.Join(Environment.NewLine, closedTicketListItems));

                    break;

                case "close":

                    if (!admin)
                        goto default;

                    if (args.Length < 2)
                    {
                        player.ReplySafe("Syntax: ticket close <ID>");
                        return;
                    }

                    if (!int.TryParse(args[1], out id))
                    {
                        player.ReplySafe($"Invalid ID Format. {id} is no valid number!");
                        return;
                    }

                    ticket = admin ? Ticket.Find(id) : Ticket.Find(player, id);

                    if (ticket == null)
                    {
                        player.ReplySafe(LangMsg("Ticket Not Found", player.Id));
                        return;
                    }

                    if (ticket.Closed)
                    {
                        player.ReplySafe(LangMsg("Already Closed", player.Id));
                        return;
                    }

                    ticket.Close(player);
                    player.ReplySafe(ticket.InsertDataToString(LangMsg("Ticket Closed", player.Id)));

                    break;

                case "teleport":

                    if (!admin)
                        goto default;

                    if (args.Length < 2)
                    {
                        player.ReplySafe("Syntax: ticket teleport <ID>");
                        return;
                    }

                    if (!int.TryParse(args[1], out id))
                    {
                        player.ReplySafe($"Invalid ID Format. {id} is no valid number!");
                        return;
                    }

                    ticket = admin ? Ticket.Find(id) : Ticket.Find(player, id);

                    if (ticket == null)
                    {
                        player.ReplySafe(LangMsg("Ticket Not Found", player.Id));
                        return;
                    }

                    player.Teleport(ticket.Position.X, ticket.Position.Y, ticket.Position.Z);
                    player.ReplySafe(ticket.InsertDataToString(LangMsg("Teleported To Ticket", player.Id)));

                    break;

                case "wipe":

                    if (!player.HasPermission("tickets.wipe"))
                        goto default;

                    Ticket.All.Clear();
                    SaveData(Ticket.All);

                    player.ReplySafe(LangMsg("Wiped", player.Id));

                    if (NotifyWiped)
                        Notify(LangMsg("Wiped Notification").Replace("{Admin}", player.Name));

                    break;

                default:
                    // Fancy line:
                    player.ReplySafe(GetCommandInfo(player, "ticket"));
                    break;
            }
        }

        #endregion

        #region Format Helpers

        public static string StripTags(string original) => Formatter.ToPlaintext(original);

        public static string FormatText(string original) => Instance.covalence.FormatText(original);

        #endregion

        #region Lang Helper

        private string LangMsg(string key, string id = null) => lang.GetMessage(key, this, id).Replace("{Title}", ChatTitle);

        #endregion

        #region Command Helper

        private string GetCommandInfo(IPlayer player, string command)
        {
            MethodBase mb = typeof(Tickets).GetMethod(command + "Cmd", BindingFlags.NonPublic | BindingFlags.Instance);

            if (mb == null)
                return string.Empty;

            string[] commands = CommandInfo.GetCommands(player, mb);

            for (int i = 0; i < commands.Length; i++)
                commands[i] = (player.IsServer || player.LastCommand == CommandType.Console ? $"{command} " : $"/{command} ") + commands[i];

            return string.Join(Environment.NewLine, commands);
        }

        #endregion

        #region Notifying Helpers

        private static void Notify(string message)
        {
            Instance.Puts(StripTags(message));
            MessageAdmins(message);
            MessageOther(message);
        }

        private static void MessageAdmins(string message)
        {
            foreach (var player in Instance.players.Connected.Where(p => p.IsAdmin || Instance.permission.UserHasPermission(p.Id, "tickets.admin")))
                player.ReplySafe(message);
        }

        private static void MessageOther(string message)
        {
            if (Instance.UseSlack)
                Instance.Slack?.Call("Message", message, Instance.SlackChannel);

            if (Instance.UseEmailAPI)
                Instance.EmailAPI?.Call("EmailMessage", "Tickets", message);

            if (Instance.UsePushAPI)
                Instance.PushAPI?.Call("PushMessage", "Tickets", message);
        }

        #endregion

        #region Config Helpers

        private T GetConfig<T>(params object[] pathAndValue)
        {
            List<string> pathL = pathAndValue.Select((v) => v.ToString()).ToList();
            pathL.RemoveAt(pathAndValue.Length - 1);
            string[] path = pathL.ToArray();

            if (Config.Get(path) == null)
            {
                Config.Set(pathAndValue);
                PrintWarning($"Added field to config: {string.Join("/", path)}");
            }

            return (T) Convert.ChangeType(Config.Get(path), typeof(T));
        }

        #endregion

        #region Data Helpers

        private string DataFileName => Title.Replace(" ", string.Empty);

        private void LoadData<T>(out T data, string filename = null) => data = Interface.Oxide.DataFileSystem.ReadObject<T>(filename == null ? DataFileName : $"{DataFileName}/{filename}");

        private void SaveData<T>(T data, string filename = null) => Interface.Oxide.DataFileSystem.WriteObject(filename == null ? DataFileName : $"{DataFileName}/{filename}", data);

        #endregion
    }
}

#region Extension

namespace Oxide.Plugins.TicketsExtensions
{
    internal static class Extend
    {
        public static void ReplySafe(this IPlayer player, string message) =>
            player.Reply(player.IsServer ? Tickets.StripTags(message) : Tickets.FormatText(message));
    }
}

#endregion