using System;
using System.Collections.Generic;
using Oxide.Core;
using CodeHatch.Engine.Networking;
using CodeHatch.Common;
using Oxide.Core.Configuration;
using CodeHatch.UserInterface.Dialogues;

namespace Oxide.Plugins
{
    [Info("PopUpManager", "Mordeus", "1.0.3")]
    public class PopUpManager : ReignOfKingsPlugin
    {
        PopUpData popupData;       
        private DynamicConfigFile popupdata;        
        //config
        private bool DisplayOnEveryConnect => GetConfig("DisplayOnEveryConnect", false);
        private bool RulesCommandEnabled => GetConfig("RulesCommandEnabled", true);
        private bool CommandsCmdEnabled => GetConfig("CommandsCmdEnabled", true);
        private bool NoticesEnabled => GetConfig("NoticesEnabled", true);
        private int LinesPerPage => GetConfig("LinesPerPage", 12);
        private string RulePopupWindowTitle => GetConfig("RulePopupWindowTitle", "Rules");
        private string CmdPopupWindowTitle => GetConfig("CmdPopupWindowTitle", "Commands");
        private string NoticePopupWindowTitle => GetConfig("NoticePopupWindowTitle", "Notice");

        #region Oxide       
        void OnPlayerConnected(Player player)
        {
            DisplayPopUp(player);
            if (NoticesEnabled)
                DisplayNotice(player);
        }
        #endregion
        #region Data  
        private void Init()
        {
            LoadDefaultConfig();
            permission.RegisterPermission("popupmanager.popup", this);
            permission.RegisterPermission("popupmanager.edit", this);
            popupdata = Interface.Oxide.DataFileSystem.GetFile("PopUpManager/RuleList");
            data = Interface.GetMod().DataFileSystem.ReadObject<Data>("PopUpManager/PlayerRulesdata");
            noticedata = Interface.GetMod().DataFileSystem.ReadObject<NoticeData>("PopUpManager/PlayerNoticedata");
            
            if (RulesCommandEnabled)
                cmd.AddChatCommand("rules", this, "CmdRules");
            if (CommandsCmdEnabled)
                cmd.AddChatCommand("commands", this, "CmdCommandList");
            if (NoticesEnabled)
                cmd.AddChatCommand("notices", this, "CmdNotices");            
            LoadData();
        }
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "notAllowed", "[F5D400]You are not allowed to do this![FFFFFF]" },
                { "help", "[F5D400]type /popup help to open the help menu[FFFFFF]"},
                { "synError", "[F5D400]Syntax Error: [FFFFFF]Type '/popup help' to view available options" },
                { "helpTitle", $"[4F9BFF]{Title}  v{Version}"},
                { "helpPopup", "[4F9BFF]/popup <text>[FFFFFF] - Sends a serverwide Pop Up"},
                { "helpHelp", "[4F9BFF]/popup help[FFFFFF] - Display the help menu"},                
                { "helpListr", "[4F9BFF]/rules [FFFFFF]- Lists all rules"},
                { "helpListc", "[4F9BFF]/commands [FFFFFF]- Lists all commands"},
                { "helpListn", "[4F9BFF]/notices [FFFFFF]- Lists all notices"},
                { "helpAddr", "[4F9BFF]/addrule <text> [FFFFFF]- Adds a rule."},
                { "helpDeleter", "[4F9BFF]/deleterule <num> [FFFFFF]- Removes rule."},                
                { "helpEditr", "[4F9BFF]/editrule <num> <text>[FFFFFF]- Edit rules."},
                { "helpAddc", "[4F9BFF]/addcommand <text> [FFFFFF]- Adds a command."},
                { "helpDeletec", "[4F9BFF]/deletecommand <num> [FFFFFF]- Removes command."},
                { "helpEditc", "[4F9BFF]/editcommand <num> <text>[FFFFFF]- Edit commands."},
                { "helpAddn", "[4F9BFF]/addnotice <text> [FFFFFF]- Adds a notice."},
                { "helpDeleten", "[4F9BFF]/deletenotice <num> [FFFFFF]- Removes notice."},
                { "helpEditn", "[4F9BFF]/editnotice <num> <text>[FFFFFF]- Edit notices."},
                { "helpClearn", "[4F9BFF]/clearnotice[FFFFFF]- Clears Notice PlayerData"},
                { "dataEdited", "[4F9BFF]Data Edited.[FFFFFF]"},
                { "dataDeleted", "[4F9BFF]Data Deleted.[FFFFFF]"},
                { "dataAdded", "[4F9BFF]Data Added.[FFFFFF]"},

        }, this);
        }

        private void LoadData()
        {            
            try
            {
                popupData = popupdata.ReadObject<PopUpData>();
                if (popupData.rules.Count == 0)
                {
                    LoadDefaultRuleList();
                    Puts("Loading Default Rules List");
                }
                if (popupData.commands.Count == 0)
                {
                    LoadDefaultCommandList();
                    Puts("Loading Default Command List");
                }
                if (popupData.notices.Count == 0)
                {
                    LoadDefaultNoticeList();
                    Puts("Loading Default Notice List");
                }
               
                Puts("Loaded {0} Rules", popupData.rules.Count);
                Puts("Loaded {0} Commands", popupData.commands.Count);
                Puts("Loaded {0} Notices", popupData.notices.Count);
            }
            catch
            {
                Puts("Failed to load PopUpData");
                popupData = new PopUpData();
            }
        }        
        private void SaveData()
        {
            popupdata.WriteObject(popupData);
            PrintWarning("Saved PopUpManager data");
        }
        #endregion
        #region List Definitions
        class PopUpData
        {
            public Dictionary<string, Rules> rules = new Dictionary<string, Rules>();
            public Dictionary<string, Commands> commands = new Dictionary<string, Commands>();
            public Dictionary<string, Notices> notices = new Dictionary<string, Notices>();

            public class Rules
            {
                public string text;
            }
            public class Commands
            {
                public string text;
            }
            public class Notices
            {
                public string text;
            }
        }        

        protected override void LoadDefaultConfig()
        {
            Config["DisplayOnEveryConnect"] = DisplayOnEveryConnect;
            Config["RulesCommandEnabled"] = RulesCommandEnabled;
            Config["CommandsCmdEnabled"] = CommandsCmdEnabled;
            Config["NoticesEnabled"] = NoticesEnabled;
            Config["LinesPerPage"] = LinesPerPage;
            Config["RulePopupWindowTitle"] = RulePopupWindowTitle;
            Config["CmdPopupWindowTitle"] = CmdPopupWindowTitle;
            Config["NoticePopupWindowTitle"] = NoticePopupWindowTitle;
            SaveConfig();
        }

        class Data
        {
            public List<string> Players = new List<string> { };
        }
        Data data;
        class NoticeData
        {
            public List<string> PlayerNotice = new List<string> { };
        }
        NoticeData noticedata;
        #endregion
        #region Default Lists
        private Dictionary<string, string> DefaultRuleList = new Dictionary<string, string>()
        {
            { "1", "[4F9BFF]Welcome! [FF0000]The following activities are prohibited in the Game:" },
            { "2", "[F5D400]1.[4F9BFF] No KOS, or Roping, or Attacking on sight."},
            { "3", "[F5D400]2.[4F9BFF] Do not Grief/Troll/Spawn Kill/Harass etc."},
            { "4", "[F5D400]3.[4F9BFF] Do not Block Resources/Roads/Spawn Areas." },
            { "5", "[F5D400]4.[4F9BFF] No Offline Raiding or Sieges."},
            { "6", "[F5D400]5.[4F9BFF] To Siege a base you must Declare War in Chat while player is online."},
            { "7", "[F5D400]6.[4F9BFF] You may kill sleepers unless they are in, or directly near a base." },

        };
        private Dictionary<string, string> DefaultCommandList = new Dictionary<string, string>()
        {
            { "1", "[FF0000]Player commands available in the Game:" },
            { "2", "[F5D400]1.[4F9BFF] /rules" },
            { "3", "[F5D400]2.[4F9BFF] /notices" },
            { "4", "[F5D400]3.[4F9BFF] /kit"},
            { "5", "[F5D400]4.[4F9BFF] /voteday"},
            { "6", "[F5D400]5.[4F9BFF] /votenight"},
            { "7", "[F5D400]6.[4F9BFF] /votewclear- vote to clear the weather"},
            { "8", "[F5D400]7.[4F9BFF] /votewheavy- vote for weather"},
            { "9", "[F5D400]8.[4F9BFF] /defy <playerNick> to defy another player in duel"},

        };
        private Dictionary<string, string> DefaultNoticeList = new Dictionary<string, string>()
        {
            { "1", "[FF0000]Server Changes:" },
            { "2", "[4F9BFF] We have added many custom features and plugins."},
            { "3", "[4F9BFF] Enjoy your stay here, and be sure to report bugs."},
            { "4", "[4F9BFF] - Admins"}

        };
        private void LoadDefaultRuleList()
        {           
            foreach (KeyValuePair<string, string> rule in DefaultRuleList)
            {
                PopUpData.Rules newRule = new PopUpData.Rules
                {
                    text = rule.Value,
                };
                if (popupData.rules.ContainsKey(rule.Key)) return;
                else
                popupData.rules.Add(rule.Key, newRule);                  
            }           
            SaveData();
        }
        private void LoadDefaultCommandList()
        {
            foreach (KeyValuePair<string, string> command in DefaultCommandList)
            {
                PopUpData.Commands newCommand = new PopUpData.Commands
                {
                    text = command.Value,
                };
                if (popupData.commands.ContainsKey(command.Key)) return;
                else
                    popupData.commands.Add(command.Key, newCommand);
            }
            SaveData();
        }
        private void LoadDefaultNoticeList()
        {
            foreach (KeyValuePair<string, string> notice in DefaultNoticeList)
            {
                PopUpData.Notices newNotice = new PopUpData.Notices
                {
                    text = notice.Value,
                };
                if (popupData.notices.ContainsKey(notice.Key)) return;
                else
                    popupData.notices.Add(notice.Key, newNotice);
            }
            SaveData();
        }
        #endregion
        #region Commands
        [ChatCommand("popup")]        
        private void PopUpCommand(Player player, string cmd, string[] args)
        {
            string playerId = player.Id.ToString();
            if (!(player.HasPermission("admin") || player.HasPermission("popupmanager.popup")))
            {
                player.SendError(lang.GetMessage("notAllowed", this, playerId));
                return;
            }
            if (args == null || args.Length == 0)
            {
                player.SendError(lang.GetMessage("help", this, playerId));
                return;
            }
            switch (args[0])
            {
                case "help":
                    {

                        SendReply(player, lang.GetMessage("helpTitle", this, playerId));
                        SendReply(player, lang.GetMessage("helpPopup", this, playerId));
                        SendReply(player, lang.GetMessage("helpHelp", this, playerId));
                        SendReply(player, lang.GetMessage("helpListr", this, playerId));
                        SendReply(player, lang.GetMessage("helpListc", this, playerId));
                        SendReply(player, lang.GetMessage("helpListn", this, playerId));
                        SendReply(player, lang.GetMessage("helpAddr", this, playerId));
                        SendReply(player, lang.GetMessage("helpDeleter", this, playerId));
                        SendReply(player, lang.GetMessage("helpEditr", this, playerId));
                        SendReply(player, lang.GetMessage("helpAddc", this, playerId));
                        SendReply(player, lang.GetMessage("helpDeletec", this, playerId));
                        SendReply(player, lang.GetMessage("helpEditc", this, playerId));
                        SendReply(player, lang.GetMessage("helpAddn", this, playerId));
                        SendReply(player, lang.GetMessage("helpDeleten", this, playerId));
                        SendReply(player, lang.GetMessage("helpClearn", this, playerId));
                        SendReply(player, lang.GetMessage("helpEditn", this, playerId));

                    }
                    
                    return;
                    
                default:                    
                    if (args.Length == 0)
                    {
                        SendReply(player, lang.GetMessage("synError", this, playerId));
                        return;
                    }
                    string str = args.JoinToString<string>(" ");
                    IEnumerator<Player> enumerator = Server.AllPlayers.GetEnumerator();
                    try
                    {
                        while (enumerator.MoveNext())
                        {
                            Player current = enumerator.Current;
                            current.ShowPopup("Alert", str, "Ok", null, false, true);
                        }
                    }
                    finally
                    {
                        if (enumerator == null)
                        {
                        }
                        enumerator.Dispose();
                    }
                    return;
            }
            

        }
        [ChatCommand("addrule")]
        void CmdAddRules(Player player, string cmd, string[] args) => AddData(player, args);
        [ChatCommand("addcommand")]
        void CmdAddCommands(Player player, string cmd, string[] args) => AddData(player, args, false, true);
        [ChatCommand("addnotice")]
        void CmdAddNotices(Player player, string cmd, string[] args) => AddData(player, args, false, false, true);

        [ChatCommand("deleterule")]
        void CmdDeleteRule(Player player, string cmd, string[] args) => DeleteData(player, args);
        [ChatCommand("deletecommand")]
        void CmdDeleteCommand(Player player, string cmd, string[] args) => DeleteData(player, args, false, true);
        [ChatCommand("deletenotice")]
        void CmdDeleteNotice(Player player, string cmd, string[] args) => DeleteData(player, args, false, false, true);

        [ChatCommand("editrule")]
        void CmdEditRule(Player player, string cmd, string[] args) => EditData(player, args);
        [ChatCommand("editcommand")]
        void CmdEditCommand(Player player, string cmd, string[] args) => EditData(player, args, false, true);
        [ChatCommand("editnotice")]
        void CmdEditNotice(Player player, string cmd, string[] args) => EditData(player, args, false, false, true);
        [ChatCommand("clearnotice")]
        void CmdClearNoticeData(Player player, string cmd, string[] args)
        {
            string playerId = player.Id.ToString();
            if (!hasPermission(player))
            {
                player.SendError(lang.GetMessage("notAllowed", this, playerId));
                return;
            }
            noticedata.PlayerNotice.Clear();
            Interface.GetMod().DataFileSystem.WriteObject("PopUpManager/PlayerNoticedata", noticedata);
            SendReply(player, "Player Data Cleared");
        }
        #endregion
        #region Functions    
        void AddData(Player player, string[] args, bool rule = true, bool command = false, bool notice = false)
        {
            string playerId = player.Id.ToString();
            if (!hasPermission(player))
            {
                player.SendError(lang.GetMessage("notAllowed", this, playerId));
                return;
            }
            if (args == null || args.Length == 0)
            {
                player.SendError(lang.GetMessage("help", this, playerId));
                return;
            }
            string text = args[0];
            if (rule)
            {
                PopUpData.Rules newRule = new PopUpData.Rules
                {
                    text = text,
                };

                var key = popupData.rules.Count;
                key++;
                popupData.rules.Add(key.ToString(), newRule);
            }
            if (command)
            {
                PopUpData.Commands newCommand = new PopUpData.Commands
                {
                    text = text,
                };

                var key = popupData.commands.Count;
                key++;
                popupData.commands.Add(key.ToString(), newCommand);
            }
            if (notice)
            {
                PopUpData.Notices newNotice = new PopUpData.Notices
                {
                    text = text,
                };

                var key = popupData.notices.Count;
                key++;
                popupData.notices.Add(key.ToString(), newNotice);
            }
            SaveData();
            SendReply(player, lang.GetMessage("dataAdded", this, playerId));
        }
        void EditData(Player player, string[] args, bool rule = true, bool command = false, bool notice = false)
        {
            string playerId = player.Id.ToString();            
            if (!hasPermission(player))
            {
                player.SendError(lang.GetMessage("notAllowed", this, playerId));
                return;
            }

            if (args == null || args.Length == 1)
            {
                player.SendError(lang.GetMessage("help", this, playerId));
                return;
            }
            string line = args[0];
            string text = args[1];

            if (rule)
                popupData.rules[line].text = text;
            if (command)
                popupData.commands[line].text = text;
            if (notice)
                popupData.notices[line].text = text;
            SaveData();
            SendReply(player, lang.GetMessage("dataEdited", this, playerId));
        }
        void DeleteData(Player player, string[] args, bool rule = true, bool command = false, bool notice = false)
        {
            string playerId = player.Id.ToString();
            if (!hasPermission(player))
            {
                player.SendError(lang.GetMessage("notAllowed", this, playerId));
                return;
            }
            if (args == null || args.Length == 0)
            {
                player.SendError(lang.GetMessage("help", this, playerId));
                return;
            }
            string linenum = args[0];
            if (rule)
                popupData.rules.Remove(linenum);
            if (command)
                popupData.commands.Remove(linenum);
            if (notice)
                popupData.notices.Remove(linenum);
            SaveData();
            SendReply(player, lang.GetMessage("dataDeleted", this, playerId));
        }
        //For The next page stuff I looked at GrandExchange by D-Kay as an example, thanks D-Kay!
        void CmdRules(Player player, string cmd, string[] args)
        {
            string msg = "";
            int linesPerPage = LinesPerPage;
            bool thisPage = false;
            if (linesPerPage > popupData.rules.Count)
            {
                thisPage = true;
                linesPerPage = popupData.rules.Count;
            }
            for (int i = 0; i < linesPerPage; i++)
            {
                KeyValuePair<string, PopUpData.Rules> list = popupData.rules.GetAt(i);

                msg += "" + list.Value.text;
                msg += "\n \n";
            }
            if (thisPage)
            {
                player.ShowPopup(RulePopupWindowTitle, msg, "Exit"); return;
            }
            player.ShowConfirmPopup(RulePopupWindowTitle, msg, "Next Page", "Exit", (selection, dialogue, context) => NextPage(player, selection, dialogue, context, linesPerPage, linesPerPage));

        }
        void CmdCommandList(Player player, string cmd, string[] args)
        {
            string msg = "";
            int linesPerPage = LinesPerPage;
            bool thisPage = false;
            if (linesPerPage > popupData.commands.Count)
            {
                thisPage = true;
                linesPerPage = popupData.commands.Count;
            }
            for (int i = 0; i < linesPerPage; i++)
            {
                KeyValuePair<string, PopUpData.Commands> list = popupData.commands.GetAt(i);

                msg += "" + list.Value.text;
                msg += "\n \n";
            }
            if (thisPage)
            {
                player.ShowPopup(CmdPopupWindowTitle, msg, "Exit"); return;
            }
            player.ShowConfirmPopup(CmdPopupWindowTitle, msg, "Next Page", "Exit", (selection, dialogue, context) => NextPage(player, selection, dialogue, context, linesPerPage, linesPerPage, false, true));            

        }
        void CmdNotices(Player player, string cmd, string[] args)
        {
            string msg = "";
            int linesPerPage = LinesPerPage;
            bool thisPage = false;
            if (linesPerPage > popupData.notices.Count)
            {
                thisPage = true;
                linesPerPage = popupData.notices.Count;
            }
            
            for (int i = 0; i < linesPerPage; i++)
            {
                KeyValuePair<string, PopUpData.Notices> list = popupData.notices.GetAt(i);

                msg += "" + list.Value.text;
                msg += "\n \n";
            }           
            
            if (thisPage)
            {
                player.ShowPopup(NoticePopupWindowTitle, msg, "Exit"); return;
            }
            player.ShowConfirmPopup(NoticePopupWindowTitle, msg.ToString(), "Next Page", "Exit", (selection, dialogue, context) => NextPage(player, selection, dialogue, context, linesPerPage, linesPerPage, false, false, true));            
        }
        //For The below code I looked at GrandExchange by D-Kay as an example, thanks D-Kay!
        private void NextPage(Player player, Options selection, Dialogue dialogue, object context, int linesPerPage, int currentCount, bool rules = true, bool commands = false, bool notices = false)
        {
            if (selection != Options.Yes) return;
            var dataCount = 0;
            var popuptitle = RulePopupWindowTitle;
            if (rules)
            {
                dataCount = popupData.rules.Count;
                popuptitle = RulePopupWindowTitle;
            }
            if (notices)
            {
                dataCount = popupData.notices.Count;
                popuptitle = NoticePopupWindowTitle;
            }
            if (commands)
            {
                dataCount = popupData.commands.Count;
                popuptitle = CmdPopupWindowTitle;
            }

            if ((currentCount + linesPerPage) > dataCount)
                linesPerPage = dataCount - currentCount;

            string msg = "";

            for (int i = currentCount; i < linesPerPage + currentCount; i++)
            {
                if (rules)
                {
                    KeyValuePair<string, PopUpData.Rules> list = popupData.rules.GetAt(i);

                    msg += "" + list.Value.text;
                    msg += "\n \n";
                }
                if (commands)
                {
                    KeyValuePair<string, PopUpData.Commands> list = popupData.commands.GetAt(i);

                    msg += "" + list.Value.text;
                    msg += "\n \n";
                }
                if (notices)
                {
                    KeyValuePair<string, PopUpData.Notices> list = popupData.notices.GetAt(i);

                    msg += "" + list.Value.text;
                    msg += "\n \n";
                }
            }
            currentCount = currentCount + linesPerPage;

            if (currentCount < dataCount)
            {
                player.ShowConfirmPopup(popuptitle, msg, "Next Page", "Exit", (options, dialogue1, context1) => NextPage(player, options, dialogue1, context1, linesPerPage, currentCount));
            }
            else
            {
                player.ShowPopup(popuptitle, msg, "Exit");
            }
        }
        
        void DisplayPopUp(Player player)
        {
            string steamId = Convert.ToString(player.Id);            
            
            if (DisplayOnEveryConnect == true)
            {
                string msg = "";
                int linesPerPage = LinesPerPage;
                bool thisPage = false;
                if (linesPerPage > popupData.rules.Count)
                {
                    thisPage = true;
                    linesPerPage = popupData.rules.Count;
                }
                for (int i = 0; i < linesPerPage; i++)
                {
                    KeyValuePair<string, PopUpData.Rules> list = popupData.rules.GetAt(i);

                    msg += "" + list.Value.text;
                    msg += "\n \n";
                }
                if (thisPage)
                {
                    player.ShowPopup(RulePopupWindowTitle, msg, "Exit"); return;
                }
                player.ShowConfirmPopup(RulePopupWindowTitle, msg, "Next Page", "Exit", (selection, dialogue, context) => NextPage(player, selection, dialogue, context, linesPerPage, linesPerPage));
            }
            else
            {
                if (data.Players.Contains(steamId)) return;
                string msg = "";
                int linesPerPage = LinesPerPage;
                bool thisPage = false;
                if (linesPerPage > popupData.rules.Count)
                {
                    thisPage = true;
                    linesPerPage = popupData.rules.Count;
                }
                for (int i = 0; i < linesPerPage; i++)
                {
                    KeyValuePair<string, PopUpData.Rules> list = popupData.rules.GetAt(i);

                    msg += "" + list.Value.text;
                    msg += "\n \n";
                }
                if (thisPage)
                {
                    data.Players.Add(steamId);
                    Interface.GetMod().DataFileSystem.WriteObject("PopUpManager/PlayerRulesdata", data);
                    player.ShowPopup(RulePopupWindowTitle, msg, "Exit");
                    return;
                }
                player.ShowConfirmPopup(RulePopupWindowTitle, msg, "Next Page", "Exit", (selection, dialogue, context) => NextPage(player, selection, dialogue, context, linesPerPage, linesPerPage));

                data.Players.Add(steamId);
                Interface.GetMod().DataFileSystem.WriteObject("PopUpManager/PlayerRulesdata", data);
            }
        }
        void DisplayNotice(Player player)
        {
            string steamId = Convert.ToString(player.Id);           
            if (noticedata.PlayerNotice.Contains(steamId)) return;
                string msg = "";
            int linesPerPage = LinesPerPage;
            bool thisPage = false;
            if (linesPerPage > popupData.notices.Count)
            {
                thisPage = true;
                linesPerPage = popupData.notices.Count;
            }

            for (int i = 0; i < linesPerPage; i++)
            {
                KeyValuePair<string, PopUpData.Notices> list = popupData.notices.GetAt(i);

                msg += "" + list.Value.text;
                msg += "\n \n";
            }

            if (thisPage)
            {
                noticedata.PlayerNotice.Add(steamId);
                Interface.GetMod().DataFileSystem.WriteObject("PopUpManager/PlayerNoticedata", noticedata);
                player.ShowPopup(NoticePopupWindowTitle, msg, "Exit");
                return;
            }
            player.ShowConfirmPopup(NoticePopupWindowTitle, msg.ToString(), "Next Page", "Exit", (selection, dialogue, context) => NextPage(player, selection, dialogue, context, linesPerPage, linesPerPage, false, false, true));
            noticedata.PlayerNotice.Add(steamId);
            Interface.GetMod().DataFileSystem.WriteObject("PopUpManager/PlayerNoticedata", noticedata);           

        }
        #endregion
        #region Helpers
        T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }
        private bool hasPermission(Player player)
        {
            string playerId = player.Id.ToString();
            if (!(player.HasPermission("admin") || player.HasPermission("popupmanager.edit")))
            {                
                return false;
            }
            return true;
        }
        #endregion Helpers
    }
}