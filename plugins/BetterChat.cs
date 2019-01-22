using Oxide.Plugins.BetterChatExtensions;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System;
using Newtonsoft.Json.Linq;

#if RUST
using ConVar;
using Facepunch;
using Facepunch.Math;
#endif

namespace Oxide.Plugins
{
    [Info("Better Chat", "LaserHydra", "5.0.19", ResourceId = 979)]
    [Description("Manage Chat Groups, Customize Colors And Add Titles.")]
    internal class BetterChat : CovalencePlugin
    {
        #region Variables

        public static BetterChat Instance;
        public static List<ChatGroup> ChatGroups;
        public static Dictionary<Plugin, Func<IPlayer, string>> ThirdPartyTitles = new Dictionary<Plugin, Func<IPlayer, string>>();

        private static readonly ChatGroup FallbackGroup = new ChatGroup("default");

#if RUST
        public static readonly ChatGroup RustDeveloperGroup = new ChatGroup("RustDeveloper")
        {
            Priority  = 100,
            Title =
            {
                Text  = "[Rust Developer]",
                Color = "#ffaa55"
            }
        };
#endif

        #endregion    

        #region Classes

        public class BetterChatMessage
        {
            public IPlayer Player;
            public string Text;
            public List<string> Titles;
            public string PrimaryGroup;
            public ChatGroup.UsernameSettings Username;
            public ChatGroup.MessageSettings Message;
            public ChatGroup.FormatSettings Format;
            public List<string> BlockedReceivers = new List<string>();

            public ChatGroup.FormatSettings GetOutput()
            {
                ChatGroup.FormatSettings output = new ChatGroup.FormatSettings();

                Dictionary<string, string> replacements = new Dictionary<string, string>
                {
                    ["Title"] = string.Join(" ", Titles.ToArray()),
                    ["Username"] = $"[#{Username.GetUniversalColor()}][+{Username.Size}]{StripRichText(Player.Name)}[/+][/#]",
                    ["Group"] = PrimaryGroup,
                    ["Message"] = $"[#{Message.GetUniversalColor()}][+{Message.Size}]{Text}[/+][/#]",
                    ["ID"] = Player.Id,
                    ["Time"] = DateTime.Now.TimeOfDay.ToString(),
                    ["Date"] = DateTime.Now.ToString()
                };

                output.Chat = Format.Chat;
                output.Console = Format.Console;

                foreach (var replacement in replacements)
                {
                    output.Console = StripRichText(output.Console.Replace($"{{{replacement.Key}}}", replacement.Value));
                    output.Chat = Instance.covalence.FormatText(output.Chat.Replace($"{{{replacement.Key}}}", replacement.Value));
                }

                if (output.Chat.StartsWith(" "))
                    output.Chat = output.Chat.Remove(0, 1);

                if (output.Console.StartsWith(" "))
                    output.Console = output.Console.Remove(0, 1);

                return output;
            }

            public static BetterChatMessage FromDictionary(Dictionary<string, object> dict) => new BetterChatMessage
            {
                Player = (IPlayer)dict["Player"],
                Text = (string)dict["Text"],
                Titles = (List<string>)dict["Titles"],
                PrimaryGroup = (string)dict["PrimaryGroup"],
                BlockedReceivers = (List<string>)dict["BlockedReceivers"],
                Username = new ChatGroup.UsernameSettings
                {
                    Color = (string)((Dictionary<string, object>)dict["Username"])["Color"],
                    Size = (int)((Dictionary<string, object>)dict["Username"])["Size"]
                },
                Message = new ChatGroup.MessageSettings
                {
                    Color = (string)((Dictionary<string, object>)dict["Message"])["Color"],
                    Size = (int)((Dictionary<string, object>)dict["Message"])["Size"]
                },
                Format = new ChatGroup.FormatSettings
                {
                    Chat = (string)((Dictionary<string, object>)dict["Format"])["Chat"],
                    Console = (string)((Dictionary<string, object>)dict["Format"])["Console"]
                }
            };

            public Dictionary<string, object> ToDictionary() => new Dictionary<string, object>
            {
                ["Player"] = Player,
                ["Text"] = Text,
                ["Titles"] = Titles,
                ["PrimaryGroup"] = PrimaryGroup,
                ["BlockedReceivers"] = BlockedReceivers,
                ["Username"] = new Dictionary<string, object>
                {
                    ["Color"] = Username.Color,
                    ["Size"] = Username.Size
                },
                ["Message"] = new Dictionary<string, object>
                {
                    ["Color"] = Message.Color,
                    ["Size"] = Message.Size
                },
                ["Format"] = new Dictionary<string, object>
                {
                    ["Chat"] = Format.Chat,
                    ["Console"] = Format.Console
                }
            };
        }

        public class ChatGroup
        {
            public string GroupName;
            public int Priority = 0;

            public TitleSettings Title = new TitleSettings();
            public UsernameSettings Username = new UsernameSettings();
            public MessageSettings Message = new MessageSettings();
            public FormatSettings Format = new FormatSettings();

            public ChatGroup(string name)
            {
                GroupName = name;
                Title = new TitleSettings(name);
            }

            internal static Dictionary<string, Field> Fields = new Dictionary<string, Field>(StringComparer.InvariantCultureIgnoreCase)
            {
                ["Priority"] = new Field(g => g.Priority, (g, v) => g.Priority = int.Parse(v), "number"),

                ["Title"] = new Field(g => g.Title.Text, (g, v) => g.Title.Text = v, "text"),
                ["TitleColor"] = new Field(g => g.Title.Color, (g, v) => g.Title.Color = v, "color"),
                ["TitleSize"] = new Field(g => g.Title.Size, (g, v) => g.Title.Size = int.Parse(v), "number"),
                ["TitleHidden"] = new Field(g => g.Title.Hidden, (g, v) => g.Title.Hidden = bool.Parse(v), "true/false"),
                ["TitleHiddenIfNotPrimary"] = new Field(g => g.Title.HiddenIfNotPrimary, (g, v) => g.Title.HiddenIfNotPrimary = bool.Parse(v), "true/false"),

                ["UsernameColor"] = new Field(g => g.Username.Color, (g, v) => g.Username.Color = v, "color"),
                ["UsernameSize"] = new Field(g => g.Username.Size, (g, v) => g.Username.Size = int.Parse(v), "number"),

                ["MessageColor"] = new Field(g => g.Message.Color, (g, v) => g.Message.Color = v, "color"),
                ["MessageSize"] = new Field(g => g.Message.Size, (g, v) => g.Message.Size = int.Parse(v), "number"),

                ["ChatFormat"] = new Field(g => g.Format.Chat, (g, v) => g.Format.Chat = v, "text"),
                ["ConsoleFormat"] = new Field(g => g.Format.Console, (g, v) => g.Format.Console = v, "text")
            };

            public static ChatGroup Find(string name) => ChatGroups.Find(g => g.GroupName == name);

            public static List<ChatGroup> GetUserGroups(IPlayer player)
            {
                string[] oxideGroups = Instance.permission.GetUserGroups(player.Id);
                var groups = ChatGroups.Where(g => oxideGroups.Any(name => g.GroupName.ToLower() == name)).ToList();

#if RUST
                BasePlayer bPlayer = BasePlayer.Find(player.Id);

                if (bPlayer?.IsDeveloper ?? false)
                    groups.Add(BetterChat.RustDeveloperGroup);
#endif

                return groups;
            }

            public static ChatGroup GetUserPrimaryGroup(IPlayer player)
            {
                List<ChatGroup> groups = GetUserGroups(player);
                ChatGroup primary = null;

                foreach (ChatGroup group in groups)
                    if (primary == null || group.Priority < primary.Priority)
                        primary = group;

                return primary;
            }

            public static BetterChatMessage FormatMessage(IPlayer player, string message)
            {
                ChatGroup primary = GetUserPrimaryGroup(player);
                List<ChatGroup> groups = GetUserGroups(player);

                if (primary == null)
                {
                    Instance.PrintWarning($"{player.Name} ({player.Id}) does not seem to be in any BetterChat group - falling back to plugin's default group! This should never happen! Please make sure you have a group called 'default'.");
                    primary = FallbackGroup;
                    groups.Add(primary);
                }

                groups.Sort((a, b) => b.Priority.CompareTo(a.Priority));

                var titles = (from g in groups
                              where !g.Title.Hidden && !(g.Title.HiddenIfNotPrimary && primary != g)
                              select $"[#{g.Title.GetUniversalColor()}][+{g.Title.Size}]{g.Title.Text}[/+][/#]")
                              .ToList();

                titles = titles.GetRange(0, Math.Min(Configuration.MaxTitles, titles.Count));

                foreach (var thirdPartyTitle in ThirdPartyTitles)
                {
                    try
                    {
                        string title = thirdPartyTitle.Value(player);

                        if (!string.IsNullOrEmpty(title))
                            titles.Add(title);
                    }
                    catch (Exception ex)
                    {
                        Instance.PrintError($"Error when trying to get third-party title from plugin '{thirdPartyTitle.Key}'{Environment.NewLine}{ex}");
                    }
                }

                return new BetterChatMessage
                {
                    Player = player,
                    Text = StripRichText(message),
                    Titles = titles,
                    PrimaryGroup = primary.GroupName,
                    Username = primary.Username,
                    Message = primary.Message,
                    Format = primary.Format
                };
            }

            public void AddUser(IPlayer player) => Instance.permission.AddUserGroup(player.Id, GroupName);

            public void RemoveUser(IPlayer player) => Instance.permission.RemoveUserGroup(player.Id, GroupName);

            public Field.SetValueResult SetField(string field, string value)
            {
                if (!Fields.ContainsKey(field))
                    return Field.SetValueResult.InvalidField;

                try
                {
                    Fields[field].Setter(this, value);
                }
                catch (FormatException)
                {
                    return Field.SetValueResult.InvalidValue;
                }

                return Field.SetValueResult.Success;
            }

            public Dictionary<string, object> GetFields() => Fields.ToDictionary(field => field.Key, field => field.Value.Getter(this));

            public override int GetHashCode() => GroupName.GetHashCode();

            public class TitleSettings
            {
                public string Text = "[Player]";
                public string Color = "#55aaff";
                public int Size = 15;
                public bool Hidden = false;
                public bool HiddenIfNotPrimary = false;

                public string GetUniversalColor() => Color.StartsWith("#") ? Color.Substring(1) : Color;

                public TitleSettings(string groupName)
                {
                    if (groupName != "default" && groupName != null)
                        Text = $"[{groupName}]";
                }

                public TitleSettings()
                {
                }
            }

            public class UsernameSettings
            {
                public string Color = "#55aaff";
                public int Size = 15;

                public string GetUniversalColor() => Color.StartsWith("#") ? Color.Substring(1) : Color;
            }

            public class MessageSettings
            {
                public string Color = "white";
                public int Size = 15;

                public string GetUniversalColor() => Color.StartsWith("#") ? Color.Substring(1) : Color;
            }

            public class FormatSettings
            {
                public string Chat = "{Title} {Username}: {Message}";
                public string Console = "{Title} {Username}: {Message}";
            }
            
            public class Field
            {
                public Func<ChatGroup, object> Getter { get; }
                public Action<ChatGroup, string> Setter { get; }
                public string UserFriendyType { get; }

                public enum SetValueResult
                {
                    Success,
                    InvalidField,
                    InvalidValue
                }

                public Field(Func<ChatGroup, object> getter, Action<ChatGroup, string> setter, string userFriendyType)
                {
                    Getter = getter;
                    Setter = setter;
                    UserFriendyType = userFriendyType;
                }
            }
        }

        public static class Configuration
        {
            public static int MaxTitles = 3;
            public static int MaxMessageLength = 128;
        }

        #endregion

        #region Loading

        private new void LoadConfig()
        {
            GetConfig(ref Configuration.MaxTitles, "Maximal Titles");
            GetConfig(ref Configuration.MaxMessageLength, "Maximal Characters Per Message");

            SaveConfig();
        }

        private void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Group Already Exists"] = "Group '{group}' already exists.",
                ["Group Does Not Exist"] = "Group '{group}' doesn't exist.",
                ["Group Field Changed"] = "Changed {field} to {value} for group '{group}'.",
                ["Group Added"] = "Successfully added group '{group}'.",
                ["Group Removed"] = "Successfully removed group '{group}'.",
                ["Invalid Field"] = "{field} is not a valid field. Type 'chat group set' to list all existing fields.",
                ["Invalid Value"] = "'{value}' is not a correct value for field '{field}'! Should be a '{type}'.",
                ["Player Already In Group"] = "{player} already is in group '{group}'.",
                ["Added To Group"] = "{player} was added to group '{group}'.",
                ["Player Not In Group"] = "{player} is not in group '{group}'.",
                ["Removed From Group"] = "{player} was removed from group '{group}'."
            }, this);
        }

        protected override void LoadDefaultConfig() => PrintWarning("Generating new config file...");

        #endregion

        #region Hooks

        void Loaded()
        {
            Instance = this;

            LoadConfig();
            LoadMessages();

            LoadData(ref ChatGroups);

            if (ChatGroups.Count == 0)
                ChatGroups.Add(new ChatGroup("default"));

            foreach (ChatGroup group in ChatGroups)
            {
                if (!permission.GroupExists(group.GroupName))
                    permission.CreateGroup(group.GroupName, string.Empty, 0);
            }

            SaveData(ChatGroups);
        }

        void OnPluginUnloaded(Plugin plugin)
        {
            if (ThirdPartyTitles.ContainsKey(plugin))
                ThirdPartyTitles.Remove(plugin);
        }

        object OnUserChat(IPlayer player, string message)
        {
            if (message.Length > Configuration.MaxMessageLength)
                message = message.Substring(0, Configuration.MaxMessageLength);

            BetterChatMessage chatMessage = ChatGroup.FormatMessage(player, message);

            if (chatMessage == null)
                return null;

            Dictionary<string, object> chatMessageDict = chatMessage.ToDictionary();

            foreach (Plugin plugin in plugins.GetAll())
            {
                object hookResult = plugin.CallHook("OnBetterChat", chatMessageDict);

                if (hookResult is Dictionary<string, object>)
                {
                    try
                    {
                        chatMessageDict = (Dictionary<string, object>)hookResult;
                    }
                    catch (Exception e)
                    {
                        PrintError($"Failed to load modified OnBetterChat data from plugin '{plugin.Title} ({plugin.Version})':{Environment.NewLine}{e}");
                        continue;
                    }
                }
                else if (hookResult != null)
                    return null;
            }

            chatMessage = BetterChatMessage.FromDictionary(chatMessageDict);
            var output = chatMessage.GetOutput();

            List<string> blockedReceivers = (List<string>)chatMessageDict["BlockedReceivers"];

#if RUST
            foreach (BasePlayer p in BasePlayer.activePlayerList.Where(p => !blockedReceivers.Contains(p.UserIDString)))
                p.SendConsoleCommand("chat.add", new object[] { player.Id, output.Chat });
#else
            foreach (IPlayer p in players.Connected.Where(p => !blockedReceivers.Contains(p.Id)))
                p.Message(output.Chat);
#endif

            Puts(output.Console);

#if RUST
            Chat.ChatEntry chatEntry = new Chat.ChatEntry
            {
                Message = output.Console,
                UserId = Convert.ToUInt64(player.Id),
                Username = player.Name,
                Time = Epoch.Current
            };

            RCon.Broadcast(RCon.LogType.Chat, chatEntry);
#endif

            return true;
        }

        #endregion

        #region API

        private bool API_AddGroup(string group)
        {
            if (ChatGroup.Find(group) != null)
                return false;

            ChatGroups.Add(new ChatGroup(group));
            SaveData(ChatGroups);

            return true;
        }

	    private List<JObject> API_GetAllGroups() => ChatGroups.ConvertAll(JObject.FromObject);

		private List<JObject> API_GetUserGroups(IPlayer player) => ChatGroup.GetUserGroups(player).ConvertAll(JObject.FromObject);

        private bool API_GroupExists(string group) => ChatGroup.Find(group) != null;

        private ChatGroup.Field.SetValueResult? API_SetGroupField(string group, string field, string value) => ChatGroup.Find(group)?.SetField(field, value);

        private Dictionary<string, object> API_GetGroupFields(string group) => ChatGroup.Find(group)?.GetFields() ?? new Dictionary<string, object>();

		private Dictionary<string, object> API_GetMessageData(IPlayer player, string message) => ChatGroup.FormatMessage(player, message)?.ToDictionary();

		private string API_GetFormattedUsername(IPlayer player)
        {
            var primary = ChatGroup.GetUserPrimaryGroup(player);

            // Player has no groups - this should never happen
            if (primary == null)
                return player.Name;

            return $"[#{primary.Username.GetUniversalColor()}][+{primary.Username.Size}]{player.Name}[/+][/#]";
        }

        private string API_GetFormattedMessage(IPlayer player, string message, bool console = false) => console ? ChatGroup.FormatMessage(player, message).GetOutput().Console : ChatGroup.FormatMessage(player, message).GetOutput().Chat;

        private void API_RegisterThirdPartyTitle(Plugin plugin, Func<IPlayer, string> titleGetter) => ThirdPartyTitles[plugin] = titleGetter;

        #endregion

        #region Commands

        [Command("chat"), Permission("betterchat.admin")]
        private void CmdChat(IPlayer player, string cmd, string[] args)
        {
            cmd = player.LastCommand == CommandType.Console ? cmd : $"/{cmd}";

            if (args.Length == 0)
            {
                player.Reply($"{cmd} <group|user>");
                return;
            }

            string argsStr = string.Join(" ", args);

            var commands = new Dictionary<string, Action<string[]>>
            {
                ["group add"] = a => {
                    if (a.Length != 1)
                    {
                        player.Reply($"Syntax: {cmd} group add <group>");
                        return;
                    }

                    string groupName = a[0].ToLower();

                    if (ChatGroup.Find(groupName) != null)
                    {
                        player.ReplyLang("Group Already Exists", new KeyValuePair<string, string>("group", groupName));
                        return;
                    }

                    ChatGroup group = new ChatGroup(groupName);

                    ChatGroups.Add(group);

                    if (!permission.GroupExists(group.GroupName))
                        permission.CreateGroup(group.GroupName, string.Empty, 0);

                    SaveData(ChatGroups);

                    player.ReplyLang("Group Added", new KeyValuePair<string, string>("group", groupName));
                },
                ["group remove"] = a => {
                    if (a.Length != 1)
                    {
                        player.Reply($"Syntax: {cmd} group remove <group>");
                        return;
                    }

                    string groupName = a[0].ToLower();
                    ChatGroup group = ChatGroup.Find(groupName);

                    if (group == null)
                    {
                        player.ReplyLang("Group Does Not Exist", new KeyValuePair<string, string>("group", groupName));
                        return;
                    }

                    ChatGroups.Remove(group);
                    SaveData(ChatGroups);

                    player.ReplyLang("Group Removed", new KeyValuePair<string, string>("group", groupName));
                },
                ["group set"] = a => {
                    if (a.Length != 3)
                    {
                        player.Reply($"Syntax: {cmd} group set <group> <field> <value>");
                        player.Reply($"Fields:{Environment.NewLine}{string.Join(", ", ChatGroup.Fields.Select(kvp => $"({kvp.Value.UserFriendyType}) {kvp.Key}").ToArray())}");
                        return;
                    }

                    string groupName = a[0].ToLower();
                    ChatGroup group = ChatGroup.Find(groupName);

                    if (group == null)
                    {
                        player.ReplyLang("Group Does Not Exist", new KeyValuePair<string, string>("group", groupName));
                        return;
                    }

                    string field = a[1];
                    string strValue = a[2];

                    switch (group.SetField(field, strValue))
                    {
                        case ChatGroup.Field.SetValueResult.Success:
                            SaveData(ChatGroups);
                            player.ReplyLang("Group Field Changed", new Dictionary<string, string> { ["group"] = group.GroupName, ["field"] = field, ["value"] = strValue });
                            break;

                        case ChatGroup.Field.SetValueResult.InvalidField:
                            player.ReplyLang("Invalid Field", new KeyValuePair<string, string>("field", field));
                            break;

                        case ChatGroup.Field.SetValueResult.InvalidValue:
                            player.ReplyLang("Invalid Value", new Dictionary<string, string> { ["field"] = field, ["value"] = strValue, ["type"] = ChatGroup.Fields[field].UserFriendyType });
                            break;
                    }
                },
                ["group list"] = a =>
                {
                    player.Reply(string.Join(", ", ChatGroups.Select(g => g.GroupName).ToArray()));
                },
                ["group"] = a => player.Reply($"Syntax: {cmd} group <add|remove|set|list>"),
                ["user add"] = a => {
                    if (a.Length != 2)
                    {
                        player.Reply($"Syntax: {cmd} user add <username|id> <group>");
                        return;
                    }

                    IPlayer targetPlayer = GetPlayer(a[0], player);

                    if (targetPlayer == null)
                        return;

                    string groupName = a[1].ToLower();
                    ChatGroup group = ChatGroup.Find(groupName);

                    if (group == null)
                    {
                        player.ReplyLang("Group Does Not Exist", new KeyValuePair<string, string>("group", groupName));
                        return;
                    }

                    if (permission.UserHasGroup(targetPlayer.Id, groupName))
                    {
                        player.ReplyLang("Player Already In Group", new Dictionary<string, string> { ["player"] = targetPlayer.Name, ["group"] = groupName });
                        return;
                    }

                    group.AddUser(targetPlayer);
                    player.ReplyLang("Added To Group", new Dictionary<string, string> { ["player"] = targetPlayer.Name, ["group"] = groupName });
                },
                ["user remove"] = a => {
                    if (a.Length != 2)
                    {
                        player.Reply($"Syntax: {cmd} user remove <username|id> <group>");
                        return;
                    }

                    IPlayer targetPlayer = GetPlayer(a[0], player);

                    if (targetPlayer == null)
                        return;

                    string groupName = a[1].ToLower();
                    ChatGroup group = ChatGroup.Find(groupName);

                    if (group == null)
                    {
                        player.ReplyLang("Group Does Not Exist", new KeyValuePair<string, string>("group", groupName));
                        return;
                    }

                    if (!permission.UserHasGroup(targetPlayer.Id, groupName))
                    {
                        player.ReplyLang("Player Not In Group", new Dictionary<string, string> { ["player"] = targetPlayer.Name, ["group"] = groupName });
                        return;
                    }

                    group.RemoveUser(targetPlayer);
                    player.ReplyLang("Removed From Group", new Dictionary<string, string> { ["player"] = targetPlayer.Name, ["group"] = groupName });
                },
                ["user"] = a => player.Reply($"Syntax: {cmd} user <add|remove>")
            };

            var command = commands.First(c => argsStr.ToLower().StartsWith(c.Key));
            string remainingArgs = argsStr.Remove(0, command.Key.Length);

            command.Value(remainingArgs.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToArray());
        }

        #endregion

        #region Helper

        #region Finding Helper

        private IPlayer GetPlayer(string nameOrID, IPlayer player)
        {
            if (IsParseableTo<string, ulong>(nameOrID) && nameOrID.StartsWith("7656119") && nameOrID.Length == 17)
            {
                IPlayer result = players.All.ToList().Find((p) => p.Id == nameOrID);

                if (result == null)
                    player.Reply($"Could not find player with ID '{nameOrID}'");

                return result;
            }

            List<IPlayer> foundPlayers = new List<IPlayer>();

            foreach (IPlayer current in players.Connected)
            {
                if (current.Name.ToLower() == nameOrID.ToLower())
                    return current;

                if (current.Name.ToLower().Contains(nameOrID.ToLower()))
                    foundPlayers.Add(current);
            }

            switch (foundPlayers.Count)
            {
                case 0:
                    player.Reply($"Could not find player with name '{nameOrID}'");
                    break;

                case 1:
                    return foundPlayers[0];

                default:
                    string[] names = (from current in foundPlayers select current.Name).ToArray();
                    player.Reply("Multiple matching players found: \n" + string.Join(", ", names));
                    break;
            }

            return null;
        }

        #endregion

        #region Convert Helper

        private bool IsParseableTo<TSource, TResult>(TSource s)
        {
            TResult result;
            return TryParse(s, out result);
        }

        private bool TryParse<TSource, TResult>(TSource s, out TResult c)
        {
            try
            {
                c = (TResult)Convert.ChangeType(s, typeof(TResult));
                return true;
            }
            catch
            {
                c = default(TResult);
                return false;
            }
        }

        #endregion

        #region Data & Config Helper

        private void GetConfig<T>(ref T variable, params string[] path)
        {
            if (path.Length == 0)
                return;

            if (Config.Get(path) == null)
            {
                Config.Set(path.Concat(new object[] { variable }).ToArray());
                PrintWarning($"Added field to config: {string.Join("/", path)}");
            }

            variable = (T)Convert.ChangeType(Config.Get(path), typeof(T));
        }

        private string DataFileName => Title.Replace(" ", "");

        private void LoadData<T>(ref T data, string filename = null) => data = Core.Interface.Oxide.DataFileSystem.ReadObject<T>(filename ?? DataFileName);

        private void SaveData<T>(T data, string filename = null) => Core.Interface.Oxide.DataFileSystem.WriteObject(filename ?? DataFileName, data);

        #endregion

        #region Formatting Helper

        private static string StripRichText(string text)
        {
            var stringReplacements = new string[]
            {
#if RUST || HURTWORLD || UNTURNED
                "<b>", "</b>",
                "<i>", "</i>",
                "</size>",
                "</color>"
#endif
            };

            var regexReplacements = new Regex[]
            {
#if RUST || HURTWORLD || UNTURNED
                new Regex(@"<color=.+?>"),
                new Regex(@"<size=.+?>"),
#elif REIGNOFKINGS || SEVENDAYSTODIE
                new Regex(@"\[[\w\d]{6}\]"),
#elif RUSTLEGACY
                new Regex(@"\[color #[\w\d]{6}\]"),
#elif TERRARIA
                new Regex(@"\[c\/[\w\d]{6}:"),
#endif
            };

            foreach (var replacement in stringReplacements)
                text = text.Replace(replacement, string.Empty);

            foreach (var replacement in regexReplacements)
                text = replacement.Replace(text, string.Empty);

            return Formatter.ToPlaintext(text);
        }

        #endregion

        #endregion

        #region Message Wrapper

        public static string GetMessage(string key, string id) => Instance.lang.GetMessage(key, Instance, id);

        #endregion
    }
}

#region Class Method Extensions

namespace Oxide.Plugins.BetterChatExtensions
{
    internal static class Extend
    {
        public static void ReplyLang(this IPlayer player, string key, Dictionary<string, string> replacements = null)
        {
            string message = BetterChat.GetMessage(key, player.Id);

            if (replacements != null)
                foreach (var replacement in replacements)
                    message = message.Replace($"{{{replacement.Key}}}", replacement.Value);

            replacements = null;

            player.Reply(message);
        }

        public static void ReplyLang(this IPlayer player, string key, KeyValuePair<string, string> replacement)
        {
            string message = BetterChat.GetMessage(key, player.Id);
            message = message.Replace($"{{{replacement.Key}}}", replacement.Value);

            player.Reply(message);
        }
    }
}

#endregion