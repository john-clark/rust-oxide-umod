using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

// TODO: Add queue for old member removal
// TODO: Add log rotation support for cleanup
// TODO: Cache members in data file and compare when updating

namespace Oxide.Plugins
{
    [Info("Steam Groups", "Wulf/lukespragg", "0.4.1")]
    [Description("Automatically adds members of Steam group(s) to a permissions group")]
    public class SteamGroups : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Group Setup")]
            public List<GroupInfo> GroupSetup { get; set; } = new List<GroupInfo>();

            [JsonProperty(PropertyName = "Update Interval in seconds")]
            public int UpdateInterval { get; set; } = 300;

            [JsonProperty(PropertyName = "Log member changes to console")]
            public bool LogToConsole { get; set; } = true;

            [JsonProperty(PropertyName = "Log member changes to file")]
            public bool LogToFile { get; set; } = true;
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

        #region Localization

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandGroup"] = "steamgroup",
                ["CommandMembers"] = "steammembers",
                ["CommandUsageGroup"] = "Usage: {0} <add/remove> <steam group> [oxide group]",
                ["CheckingGroups"] = "Checking for new Steam group members...",
                ["GroupAdded"] = "Steam group '{0}' will be added to Oxide group '{1}'",
                ["GroupExists"] = "Steam group '{0}' and Oxide group '{1}' setup already exists",
                ["GroupRemoved"] = "Steam group '{0}' has been removed and will not longer be checked"
            }, this);
        }

        #endregion Localization

        #region Initialization

        private readonly Dictionary<string, string> steamGroups = new Dictionary<string, string>();
        private readonly Dictionary<string, string> groups = new Dictionary<string, string>();
        private readonly HashSet<string> members = new HashSet<string>();
        private readonly Queue<Member> membersQueue = new Queue<Member>();
        private readonly Regex idRegex = new Regex(@"<steamID64>(?<id>.+)</steamID64>");
        private readonly Regex pageRegex = new Regex(@"<currentPage>(?<page>.+)</currentPage>");
        private readonly Regex pagesRegex = new Regex(@"<totalPages>(?<pages>.+)</totalPages>");

        private const string permAdmin = "steamgroups.admin";

        private bool backoffPoll = false;

        public class GroupInfo
        {
            public readonly string Oxide;
            public readonly string Steam;

            public GroupInfo(string oxide, string steam)
            {
                Oxide = oxide.ToLower();
                Steam = steam.ToLower();
            }
        }

        private class Member
        {
            public readonly string Id;
            public readonly string Group;

            public Member(string id, string group)
            {
                Id = id;
                Group = group;
            }
        }

        private void OnServerInitialized()
        {
            foreach (GroupInfo group in config.GroupSetup)
            {
                if (!permission.GroupExists(group.Oxide))
                {
                    permission.CreateGroup(group.Oxide, group.Oxide, 0);
                }

                AddSteamGroup(group.Steam, group.Oxide);
            }

            permission.RegisterPermission(permAdmin, this);

            AddLocalizedCommand("CommandGroup", "GroupCommand");
            AddLocalizedCommand("CommandMembers", "MembersCommand");

            // Start the timed group checking (only once, will be enabled entire plugin lifecycle)
            StartTimedChecking();

            // Trigger check of Steam members immediately
            QueueWorkerThread(worker => CheckSteamGroups());

            // Start member dequeue/cleanup loop
            RunQueueAndCleanup();
        }

        #endregion Initialization

        #region Group Handling

        private void AddSteamGroup(string steamGroup, string oxideGroup)
        {
            ulong result;
            const string urlFormat = "http://steamcommunity.com/{0}/{1}/memberslistxml/?xml=1";
            string url = string.Format(urlFormat, ulong.TryParse(steamGroup, out result) ? "gid" : "groups", steamGroup);

            string groupValue;
            if (!(groups.TryGetValue(steamGroup, out groupValue) && groupValue == oxideGroup))
            {
                groups.Add(steamGroup, oxideGroup);
            }

            if (!steamGroups.ContainsKey(steamGroup))
            {
                steamGroups.Add(steamGroup, url);
            }
        }

        private void ProcessQueuedMembers()
        {
            QueueWorkerThread(worker =>
            {
                try
                {
                    Member member = membersQueue.Dequeue();

                    // Check if player is already in appropriate Oxide group, else add
                    if (!permission.UserHasGroup(member.Id, groups[member.Group]))
                    {
                        permission.AddUserGroup(member.Id, groups[member.Group]);
                        Log($"{member.Id} from {member.Group} added to '{groups[member.Group]}' group", "additions");
                    }
                }
                catch (Exception e)
                {
                    Puts($"An error occurred while processing queue: {e}");
                }
            });
        }

        private void RunQueueAndCleanup()
        {
            timer.Every(1f, () =>
            {
                if (membersQueue.Count != 0)
                {
                    ProcessQueuedMembers();
                }
                else
                {
                    //RemoveOldMembers();
                }
            });
        }

        #endregion Group Handling

        #region Group Cleanup

        private void RemoveOldMembers()
        {
            foreach (KeyValuePair<string, string> group in groups)
            {
                foreach (string user in permission.GetUsersInGroup(group.Value))
                {
                    string id = Regex.Replace(user, "[^0-9]", "");
                    if (!members.Contains(id))
                    {
                        permission.RemoveUserGroup(id, group.Value);
                        Log($"{id} from '{group.Value}' group", "removals");
                    }
                }
            }
        }

        #endregion Group Cleanup

        #region Member Checking

        private void StartTimedChecking()
        {
            timer.Every(config.UpdateInterval, () =>
            {
                // While backoff is enabled, we do not want to check members
                if (backoffPoll)
                {
                    Puts("Currently in backoff state, will not poll Steam group(s) for members");
                    return;
                }

                try
                {
                    QueueWorkerThread(worker => CheckSteamGroups());
                }
                catch (Exception e)
                {
                    Puts($"Exception occurred: {e}");
                }
            });
        }

        private void CheckSteamGroups()
        {
            try
            {
                Puts("Polling Steam group(s) to get member list");

                foreach (KeyValuePair<string, string> group in steamGroups)
                {
                    const int page = 1;

                    // One group at the time, no additional threading, we don't want to flood the API
                    Puts($"Checking Steam group {group.Key}, starting from page {page}");
                    QueueWebRequest(group.Key, group.Value, page);
                }
            }
            catch (Exception e)
            {
                Puts($"Exception occurred: {e}");
            }
        }

        private void QueueWebRequest(string groupName, string baseUrl, int page)
        {
            try
            {
                // Queue web request to get member list for Steam group
                webrequest.Enqueue($"{baseUrl}&p={page}", null, (code, response) =>
                {
                    WebRequestCallback(code, response, baseUrl, groupName);
                }, this, RequestMethod.GET, null, 10000f);
            }
            catch (Exception e)
            {
                Puts($"Exception occurred: {e}");
            }
        }

        private void WebRequestCallback(int code, string response, string baseUrl, string groupName)
        {
            if (code == 403 || code == 429)
            {
                Puts($"Steam is currently not allowing connections from your server! code={code}. Aborting this call");
                ToggleBackoff();
                return;
            }

            if (code != 200 || response == null)
            {
                Puts($"Checking for Steam group members failed! code={code}. Aborting this call");
                ToggleBackoff();
                return;
            }

            MatchCollection ids = idRegex.Matches(response);
            Log($"Found {ids.Count} member(s) in {groupName} to check");

            int newMembers = 0;
            foreach (Match match in ids)
            {
                string id = match.Groups["id"].Value;
                if (!members.Contains(id))
                {
                    newMembers++;
                    members.Add(id);
                    membersQueue.Enqueue(new Member(id, groupName));
                }
            }
            Puts($"Added {newMembers} member(s) in {groupName} to queue");

            int currentPage;
            int totalPages;
            int.TryParse(pageRegex.Match(response).Groups[1].Value, out currentPage);
            int.TryParse(pagesRegex.Match(response).Groups[1].Value, out totalPages);

            if (currentPage != 0 && totalPages != 0 && currentPage < totalPages)
            {
                QueueWorkerThread(worker => QueueWebRequest(groupName, baseUrl, currentPage + 1));
            }
        }

        private void ToggleBackoff()
        {
            if (!backoffPoll)
            {
                backoffPoll = true;
                timer.Once(600f, () => QueueWorkerThread(worker => ToggleBackoff()));
                Puts("Backoff polling state enabled");
            }
            else
            {
                backoffPoll = false;
                Puts("Backoff polling state disabled");
            }
        }

        #endregion Member Checking

        #region Commands

        private void GroupCommand(IPlayer player, string command, string[] args)
        {
            if (player.HasPermission(permAdmin))
            {
                if (args.Length < 2)
                {
                    Message(player, "CommandUsageGroup", command);
                    return;
                }

                string steamGroup = args[1].ToLower();
                string oxideGroup = args.Length >= 3 ? args[2].ToLower() : Interface.Oxide.Config.Options.DefaultGroups.Players.ToLower();

                switch (args[0].ToLower())
                {
                    case "+":
                    case "add":
                        {
                            if (!config.GroupSetup.Any(g => g.Steam == steamGroup && g.Oxide == oxideGroup))
                            {
                                config.GroupSetup.Add(new GroupInfo(oxideGroup, args[1]));
                                Message(player, "GroupAdded", args[1], oxideGroup);
                            }
                            else
                            {
                                Message(player, "GroupExists", args[1], oxideGroup);
                                return;
                            }

                            break;
                        }

                    case "-":
                    case "del":
                    case "delete":
                    case "remove":
                        {
                            config.GroupSetup.RemoveAll(g => g.Oxide == args[1] || g.Steam == args[1]);
                            Message(player, "GroupRemoved", args[1]);
                            break;
                        }

                    default:
                        {
                            Message(player, "CommandUsageGroup", command);
                            return;
                        }
                }
                SaveConfig();
            }
        }

        private void MembersCommand(IPlayer player, string command, string[] args)
        {
            if (player.HasPermission(permAdmin))
            {
                // Trigger check of Steam members immediately
                QueueWorkerThread(worker => CheckSteamGroups());

                Message(player, "CheckingGroups");
            }
        }

        #endregion Commands

        #region Helpers

        private string Lang(string key, string id = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, id), args);
        }

        private void AddLocalizedCommand(string key, string command)
        {
            foreach (string language in lang.GetLanguages(this))
            {
                Dictionary<string, string> messages = lang.GetMessages(language, this);
                foreach (KeyValuePair<string, string> message in messages.Where(m => m.Key.Equals(key)))
                {
                    if (!string.IsNullOrEmpty(message.Value))
                    {
                        AddCovalenceCommand(message.Value, command);
                    }
                }
            }
        }

        private void Log(string text, string filename)
        {
            if (config.LogToConsole)
            {
                Puts(text);
            }

            if (config.LogToFile)
            {
                LogToFile(filename, $"[{DateTime.Now}] {text}", this);
            }
        }

        private void Message(IPlayer player, string key, params object[] args)
        {
            player.Reply(Lang(key, player.Id, args));
        }

        #endregion Helpers
    }
}
