using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
namespace Oxide.Plugins
{
    [Info("VIP Trial", "Maik8", "1.3.5", ResourceId = 2563)]
    [Description("Plugin that lets Users try VIP functions.")]
    public class VIPTrial : CovalencePlugin
    {
        #region Variables
        StoredData storedData;
        string groupName;
        int days;
        string message;
        List<object> permlist;
        
        #endregion

        #region Commands
        [Command("viptrial")]
        void VIPtrialCommand(IPlayer player, string command, string[] args)
        {
            if (args == null || args.Length <= 0)
            {
                if (checkTrialAllowed(player))
                {
                    if (!checkAlreadyUsed(player))
                    {
                        if (!checkPlayerForGroup(player))
                        {
                            addUserForTrial(player);
                        }
                        else
                        {
                            message = string.Format(GetLangValue("VIPStillRunning", player.Id), getDaysLeft(player).ToString());
                            player.Reply(message);
                        }
                    }
                    else if (checkPlayerForGroup(player))
                    {

                        //player.Reply("VIPStillRunning", getDaysLeft(player).ToString());
                        message = string.Format(GetLangValue("VIPStillRunning", player.Id), getDaysLeft(player).ToString());
                        player.Reply(message);
                    }
                    else
                    {
                        message = string.Format(GetLangValue("VIPAlreadyUsed", player.Id));
                        player.Reply(message);
                    }
                }
                else
                {
                    message = string.Format(GetLangValue("NoPermission", player.Id));
                    player.Reply(message);
                }
            }
            else if ("list".Equals(args[0]))
            {
                if (checkTrialAdmin(player) || player.Id == "server_console")
                {
                    listActiveTrials(player);
                }
                else
                {
                    message = string.Format(GetLangValue("NoPermission", player.Id));
                    player.Reply(message);
                }
            }
            else if ("clean_group".Equals(args[0]))
            {
                if (checkTrialAdmin(player) || player.Id == "server_console")
                {
                    cleanGroup(player);
                }
                else
                {
                    message = string.Format(GetLangValue("NoPermission", player.Id));
                    player.Reply(message);
                }
            }
        }
        #endregion

        #region Methods
        #region ServerHooks
        void Init()
        {
            permission.RegisterPermission("viptrial.allowed", this);
            permission.RegisterPermission("viptrial.admin", this);
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(this.Name);
            LoadDefaultConfig();
            checkGroup();
			cleanGroup();
        }
		
		void cleanGroup(IPlayer player)
		{
			bool usersremoved = false;
                    foreach (string elm in permission.GetUsersInGroup(groupName))
                    {
                        string id = elm.Substring(0, elm.LastIndexOf('('));
						id = id.TrimEnd();
                        if (checkExpired(id))
                        {
                            string name = elm.Substring(elm.LastIndexOf('(') + 1, (elm.Substring(elm.LastIndexOf('(')).Length - 2));
                            permission.RemoveUserGroup(id, groupName);
                            if (!usersremoved)
                            {
                                usersremoved = true;

                            message = string.Format(GetLangValue("CleanGroupFirstFeed", player.Id));
                            player.Reply(message);
                            }
                            message = string.Format(GetLangValue("CleanGroupGiveBackRemovedUser", player.Id), id, name);
                            player.Reply(message);
                        }
                    }
                    if (!usersremoved)
                    {
                        message = string.Format(GetLangValue("CleanGroupNobodyRemoved", player.Id));
                        player.Reply(message);
                    }
		}
		
		void cleanGroup()
		{
			bool usersremoved = false;
                    foreach (string elm in permission.GetUsersInGroup(groupName))
                    {
                        string id = elm.Substring(0, elm.LastIndexOf('('));
						id = id.TrimEnd();
                        if (checkExpired(id))
                        {
                            string name = elm.Substring(elm.LastIndexOf('(') + 1, (elm.Substring(elm.LastIndexOf('(')).Length - 2));
                            permission.RemoveUserGroup(id, groupName);
                            if (!usersremoved)
                            {
                               usersremoved = true;
                               Puts("Removed users:");
                            }
                            Puts(String.Format("{0} | {1}", id, name));
                        }
                    }
                    if (!usersremoved)
                    {
                        Puts("Nobody needs to be removed.");
                    }
		}
		
        void OnUserConnected(IPlayer player)
        {
            if (permission.UserHasGroup(player.Id, groupName))
            {
                if (checkExpired(player.Id))
                {
                    permission.RemoveUserGroup(player.Id, groupName);
                    message = string.Format(GetLangValue("VIPExpired", player.Id));
                    player.Reply(message);
                }
                else
                {
                    message = string.Format(GetLangValue("VIPEndsIn", player.Id), getDaysLeft(player).ToString());
                    player.Reply(message);
                }
            }
        }
        #endregion

        void listActiveTrials(IPlayer player)
        {
            message = string.Format(GetLangValue("ListActiveVIPStart", player.Id));
            player.Reply(message);
            foreach (VIPDataSaveFile elm in storedData.VIPDataHash)
            {
                if (!checkExpired(elm.userId))
                {
                    message = string.Format(GetLangValue("ListActiveVIPUser2", player.Id), elm.now, elm.userId, players.FindPlayerById(elm.userId).Name);
                    player.Reply(message);
                }
            }
        }
        void checkGroup()
        {
            if (!permission.GroupExists(groupName))
            {
                permission.CreateGroup(groupName, string.Empty, 0);
            }               
                checkPerm(permlist, groupName);
        }
        void checkPerm(List<object> perm, string group)
        {
            foreach (object item in perm)
            {
                if (!permission.GroupHasPermission(group, item.ToString()))
                {
                    permission.GrantGroupPermission(group, item.ToString(), null);
                }
            }
            bool check;
            foreach (string item in permission.GetGroupPermissions(group))
            {
                check = false;
                foreach (object perms in perm)
                {
                    if (perms.ToString() == item)
                    {
                        check = true;
                    }
                }
                if (!check)
                {
                    permission.RevokeGroupPermission(group, item);
                }
            }
        }
        private int getDaysLeft(IPlayer player)
        {
            DateTime usedate = DateTime.Now.Date.AddDays(-1);

            foreach (VIPDataSaveFile elm in storedData.VIPDataHash)
            {  
                if (elm.userId.Equals(player.Id))
                {
                    usedate = Convert.ToDateTime(elm.now);
                    usedate = usedate.Date;
                    break;
                }
            }
            return Convert.ToInt32((usedate.Date - DateTime.Now.Date).TotalDays);
        }

        private void addUserForTrial(IPlayer player)
        {
            permission.AddUserGroup(player.Id, groupName);
            storedData.VIPDataHash.Add( new VIPDataSaveFile( player, DateTime.Now.AddDays( days ) ) );
            Interface.Oxide.DataFileSystem.WriteObject(this.Name, storedData);
            message = string.Format(GetLangValue("VIPStarted", player.Id), DateTime.Now.Date.AddDays(days).ToShortDateString());
            player.Reply(message);
        }

        private bool checkPlayerForGroup(IPlayer player) => permission.UserHasGroup(player.Id, groupName);

        private bool checkTrialAllowed(IPlayer player) => permission.UserHasPermission(player.Id, "viptrial.allowed");

       private bool checkTrialAdmin(IPlayer player) => permission.UserHasPermission(player.Id, "viptrial.admin");

        bool checkAlreadyUsed(IPlayer player)
        {
            foreach (VIPDataSaveFile elm in storedData.VIPDataHash)
            {
                if (elm.userId == player.Id)
                {
                    return true;
                }
            }
            return false;
        }
        bool checkExpired(string Id)
        {
            DateTime usedate = DateTime.Now.Date.AddDays(-1);
            foreach (VIPDataSaveFile elm in storedData.VIPDataHash)
            {
                if (elm.userId.Equals(Id))
                {
                     usedate = Convert.ToDateTime(elm.now);
                }
            }
            if (DateTime.Compare(usedate, DateTime.Now.Date) < 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        //private void Reply(IPlayer player, string langKey, params object[] args) => player.Reply(lang.GetMessage(langKey, this, player.Id), args);
          private string GetLangValue(string key, string userId) => lang.GetMessage(key, this, userId);

        #endregion

        #region Config
        protected override void LoadDefaultConfig()
        {
            Config["VIP group name: "] = groupName = GetConfig("VIP group name: ", "vip_trial");
            Config["Amount of trial Days: "] = days = GetConfig("Amount of trial Days: ", 3);
            Config["Permissions of the group:"] = permlist = GetConfig("Permissions of the group:", new List<object>
            {
                "Oxide.plugins", "oxide.reload"
            });

            SaveConfig();
        }
        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));


        protected override void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "VIPStillRunning", "Your VIP trial is still running. Days left: {0}." },
                { "VIPAlreadyUsed", "You have already used your VIP trial." },
                { "NoPermission", "You are not allowed to use this command!" },
                { "VIPExpired", "Your VIP trial is expired." },
                { "VIPEndsIn", "Your VIP trial ends in {0} days." },
                { "VIPStarted", "Your VIP trial started, lasting till: {0}" },
                { "ListActiveVIPStart", "Currently active VIP trials:" },
                { "ListActiveVIPUser2", "{0} | {1} | {2}" },
                { "RemoveVIP", "Removed {0} from the VIP trial system."},
                { "RemoveVIPFail", "Player {0} could not be found in the VIP list."},
                { "RemoveVIPTarget", "You have been removed from the VIP trial database." },
                { "EndVIP", "The VIP trial of {0} is now over."},
                { "EndVIPFail", "Failed to end the VIP trial of {0}"},
                { "ENDVIPTarget", "You have been removed from the VIP trial."},
                { "ENDVIPTargetNotInGroupAnymore", "The player {0} is not in the VIP trial group."},
                { "CleanGroupGiveBackRemovedUser", "{0} | {1}"},
                { "CleanGroupFirstFeed", "Removed users:"},
                { "CleanGroupNobodyRemoved", "Nobody needs to be removed."}
            }, this);
        }

        #endregion

        #region Classes
        class StoredData
        {
            public HashSet<VIPDataSaveFile> VIPDataHash = new HashSet<VIPDataSaveFile>();

            public StoredData()
            {
            }
        }
        class VIPDataSaveFile
        {
            public string userId;
            public string now;

            public VIPDataSaveFile()
            {
            }

            public VIPDataSaveFile(IPlayer player, DateTime now)
            {
                userId = player.Id.ToString();
                this.now = now.ToShortDateString();
            }
        }
        #endregion
    }
}