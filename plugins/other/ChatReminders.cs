using System;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Chat Reminders", "SoftDevAndy", "1.0.0")]
    [Description("Used to remind new and old players about server features.")]
    class ChatReminders : RustPlugin
    {
        const int ONE_SECOND = 1;

        Data data = new Data();
        Dictionary<string, string> idToCurrentName;

        bool ALLOW_REMINDERS = false;
        string COLOR_TAG = "#bdc3c7";
        string TAG = "[REMINDER]";
        int secondCount = 0;
        
        int NOOB_DAYS = 3;
        int NOOB_LOGINS = 3;

        #region System Hooks

        void Loaded()
        {
            LoadDefaultConfig();

            if (ALLOW_REMINDERS)
            {
                data = Interface.Oxide.DataFileSystem.ReadObject<Data>("ChatReminders");
                idToCurrentName = new Dictionary<string, string>();

                var Online = BasePlayer.activePlayerList as List<BasePlayer>;

                foreach (BasePlayer player in Online)
                {
                    idToCurrentName.Add(player.UserIDString, player.displayName);
                }

                timer.Repeat(ONE_SECOND, 0, () =>
                {
                    foreach (Reminder reminder in data.reminders)
                    {
                        if (secondCount % reminder.remindTime == 0 && secondCount != 0)
                        {
                            foreach (Player p in data.playersNewbs)
                            {
                                var name = getUsernameByID(p.userID);

                                if (name != "null")
                                {
                                    var foundPlayer = rust.FindPlayer(name);

                                    if (foundPlayer != null)
                                    {
                                        PrintToChat(foundPlayer, getReminderPre() + reminder.reminderMessage);
                                    }
                                }
                            }
                        }
                    }

                    secondCount += ONE_SECOND;
                });
            }
        }

        protected override void LoadDefaultConfig()
        {
            ALLOW_REMINDERS = Config.Get<bool>("ALLOW_REMINDERS");
            COLOR_TAG = Config.Get<string>("COLOR_TAG");
            TAG = Config.Get<string>("TAG");
            NOOB_DAYS = Config.Get<int>("REMINDER_DAYS_PERIOD");
            NOOB_LOGINS = Config.Get<int>("LOGINS_REQUIRED");
        }

        void OnServerSave()
        {
            if (ALLOW_REMINDERS)
            {
                Interface.Oxide.DataFileSystem.WriteObject("ChatReminders", data);
            }
        }
        
        void OnPlayerInit(BasePlayer player)
        {
            if (data.isNewToServer(player.UserIDString))
            {
                data.playersNewbs.Add(new Player(player.displayName, player.UserIDString));
            }
            else
            {
                Player temp = new Player(player.UserIDString);

                if (data.playersNewbs.Contains(temp))
                {
                    if (data.getNewb(temp.userID).isNewbie(NOOB_DAYS,NOOB_LOGINS) == false)
                    {
                        data.getNewb(temp.userID).LoggedIn();

                        data.playersVets.Add(data.getNewb(temp.userID));
                        data.playersNewbs.Remove(temp);
                    }
                    else
                    {
                        data.getNewb(temp.userID).LoggedIn();
                    }
                }
            }
        }

        #endregion
        
        #region Helpers

        public string getReminderPre()
        {
            return "<color=\"" + COLOR_TAG + "\">" + TAG + "</color> ";
        }

        public string getUsernameByID(string userID)
        {
            if (idToCurrentName.ContainsKey(userID))
                return idToCurrentName[userID];
            else
                return "null";
        }

        public bool IsOnlineAndValid(BasePlayer player, string partialName)
        {
            var foundPlayer = rust.FindPlayer(partialName);

            if (foundPlayer == null)
            {
                return false;
            }
            else
            {
                var Online = BasePlayer.activePlayerList as List<BasePlayer>;

                if (Online.Contains(foundPlayer))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        #endregion

        #region Data Classes

        class Data
        {
            public List<Reminder> reminders { get; set; }
            public List<Player> playersNewbs { get; set; }
            public List<Player> playersVets { get; set; }

            public Data()
            {
                reminders = new List<Reminder>();
                playersNewbs = new List<Player>();
                playersVets = new List<Player>();
            }

            public bool isNewToServer(string userid)
            {
                Player p = new Player(userid);

                if (playersNewbs.Contains(p) || playersVets.Contains(p))
                    return false;

                return true;
            }

            public Player getNewb(string userid)
            {
                foreach(Player p in playersNewbs)
                {
                    if (p.userID == userid)
                        return p;
                }

                return null;
            }

            public Player getVet(string userid)
            {
                foreach (Player p in playersVets)
                {
                    if (p.userID == userid)
                        return p;
                }

                return null;
            }
        }

        class Reminder
        {
            public string reminderMessage { get; set; }
            public int remindTime { get; set; }

            public Reminder(string reminderMessage, int remindTime)
            {
                this.reminderMessage = reminderMessage;
                this.remindTime = remindTime;
            }
        }

        class Player
        {
            public DateTime joinTime { get; set; }
            public string originaldisplayName { get; set; }
            public string userID { get; set; }
            public int logins { get; set; }

            public Player() { }

            public Player(string uid)
            {
                this.userID = uid;
            }

            public Player(string dname,string uid)
            {
                this.logins = 0;
                this.originaldisplayName = dname;
                this.userID = uid;
                this.joinTime = TimeZoneInfo.ConvertTimeToUtc(DateTime.Now);
            }

            public Player(string dname, string uid, DateTime dt)
            {
                this.logins = 0;
                this.originaldisplayName = dname;
                this.userID = uid;
                this.joinTime = TimeZoneInfo.ConvertTimeToUtc(dt);
            }

            public void LoggedIn()
            {
                logins++;
            }
            
            public bool isNewbie(int NOOB_DAYS, int NOOB_LOGINS)
            {
                if ((TimeZoneInfo.ConvertTimeToUtc(DateTime.Now) - joinTime).Days <= NOOB_DAYS || logins <= NOOB_LOGINS)
                    return true;
                else
                    return false;
            }
            
            public override bool Equals(object obj)
            {
                var item = obj as Player;

                if (item == null)
                    return false;

                if (item.userID == null)
                    return false;

                if (item.userID == "")
                    return false;

                if (this.userID == item.userID)
                    return true;

                return false;
            }

            public override int GetHashCode()
            {
                return userID.GetHashCode();
            }
        }

        #endregion 
    }

}//namespace