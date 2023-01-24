using UnityEngine; 
using System;
using System.Collections.Generic;
using System.Reflection;
using Oxide.Core;
using Oxide.Game.Rust.Libraries;

namespace Oxide.Plugins
{
    [Info("Staffmode", "Canopy Sheep", "1.2.3", ResourceId = 2263)]
    [Description("Toggle on/off staff mode")]
    class Staffmode : RustPlugin
    {
        #region Helpers
        private string Version = "1.2.0";
        readonly Dictionary<ulong, string> groupEditor = new Dictionary<ulong, string>();

        readonly List<string> editValues = new List<string>()
        {
            "authlevel",
            "offdutygroup",
            "ondutygroup",
            "permission"
        };

        static string UppercaseFirst(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;
            s = s.ToLower();
            var a = s.ToCharArray();
            a[0] = char.ToUpper(a[0]);
            return new string(a);
        }

        bool CheckPermission(BasePlayer player, string perm)
		{
			if(permission.UserHasPermission(player.UserIDString, perm)) return true;
			return false;
		}

        #endregion
        #region Data

        class Data
		{
			public Dictionary<string, StaffData> StaffData = new Dictionary<string, StaffData>();
		}
		
		Data data;

		class GroupData
		{
			public Dictionary<string, Group> Groups = new Dictionary<string, Group>();
		}
		
		GroupData groupData; 
		
		class StaffData
		{
			public bool EnabledOffDutyMode;
			
			public StaffData(BasePlayer player)
			{
				EnabledOffDutyMode = false;
			}
		}
		
		class Group
		{
			public string GroupName;
			public int AuthLevel;
			public string OffDutyGroup;
			public string OnDutyGroup;
			public string PermissionNode;
		}

        #endregion
        #region Config

        private int AuthLevel;
        private string OffDutygroup;
        private string OnDutygroup;
        private string Permissionnode;
		private string groupname;
        private int groupcount = 0;
	    private int possibletotalerrors = 0;
        private int possiblemajorerrors = 0;
		private bool AlreadyPowered = false;
		private bool AlreadyAnnounced = false;
		private bool PermissionDenied = false;
		private bool AlreadyToggled = false;
        private ConfigData configData;

        class ConfigData
		{
			public SettingsData Settings { get; set; }
            public GamePlayeSettingsData GameplaySettings { get; set; }
            public DebugData Debug { get; set; }
            public string ConfigVersion { get; set; }
		}

		class SettingsData
		{
			public string PluginPrefix { get; set; }
			public string EditPermission { get; set; }
            public string Command { get; set; }
			public bool AnnounceOnToggle { get; set; }
			public bool LogOnToggle { get; set; }
			public bool DisconnectOnToggle { get; set; }
			public bool EnableGroupToggle { get; set; }
		}

        class GamePlayeSettingsData
        {
            public bool ShowMessages { get; set; }
            public bool CanAttack { get; set; }
            public bool CanBeTargetedByHeliAndTurrets { get; set; }
            public bool CanLootPlayer { get; set; }
        }

        class DebugData
        {
            public bool CheckGroupDataOnLoad { get; set; }
            public bool Dev { get; set; }
        }

        void TryConfig()
        {
            try
            {
                configData = Config.ReadObject<ConfigData>();
            }
            catch (Exception)
            {
                Puts("Corrupt config");
                LoadDefaultConfig();
            }
            if (configData.ConfigVersion != Version || configData.ConfigVersion == null)
            {
                PrintWarning("A config update is available, please regenerate a new config.");
            }
        }

        void LoadConfig()
        {
            Config.WriteObject(new ConfigData
            {
                Settings = new SettingsData
                {
                    PluginPrefix = "<color=orange>[StaffMode]</color>",
                    EditPermission = "staffmode.canedit",
                    Command = "staffmode",
                    AnnounceOnToggle = true,
                    LogOnToggle = true,
                    DisconnectOnToggle = true,
                    EnableGroupToggle = true
                },
                GameplaySettings = new GamePlayeSettingsData
                {
                    ShowMessages = true,
                    CanAttack = true,
                    CanBeTargetedByHeliAndTurrets = true,
                    CanLootPlayer = true
                },
                Debug = new DebugData
                {
                    CheckGroupDataOnLoad = false,
                    Dev = false
                },
                ConfigVersion = "1.2.0",
            }, true);
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Generating a new config file...");
            LoadConfig();
        }

        #endregion
        #region Language

        internal string Replace(string source, string name) => source.Replace(source, name);

        string Lang(string key, object userID = null) => lang.GetMessage(key, this, userID == null ? null : userID.ToString());

        private void Language()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "ToggleOnAnnounce", "{player.name} has switched to staff mode. They can now use staff commands."},
                { "ToggleOffAnnounce", "{player.name} has switched to player mode. They can no longer use staff commands."},
                { "ToggleOn", "You have switched to staff mode!"},
                { "ToggleOff", "You have switched to player mode!"},
                { "Disconnect", "You will be disconnected in {seconds}, please reconnect to update your auth level."},
                { "ToggleOnLog", "{player.name} is now in staff mode."},
                { "ToggleOffLog", "{player.name} is now out of staff mode."},
                { "NoPermission", "You do not have permission to use this command."},
                { "Reconnect", "You will be kicked in 5 seconds to update your status. Please reconnect!"},
                { "Corrupt", "A group you tried to toggle into is corrupt, please check console for more information."},
                { "Usage", "Usage: /{0} {1} {2} {3}"},
                { "AlreadyExists", "This group already exists."},
                { "DoesNotExist", "This group doesn't exists."},
                { "RemovedGroup", "Removed group '{group}' successfully"},
                { "CreatedGroup", "Created group '{group}' successfully."},
                { "NoGroups", "No groups have been configured properly. Check console for data check."},
                { "EditingGroup", "Now editing group '{group}.'"},
                { "NotEditingGroup", "You are not editing a group."},
                { "UpdatedValue", "Updated '{0}' to '{1}' for group '{2}.'"},
                { "OnAttack", "You cannot attack while in staff mode."},
                { "OnLootPlayer", "You cannot loot another player while in staff mode."}
            }, this);
        }

        #endregion
        #region Hooks
        void Init()
        {	
			LoadData();
			Language();
			TryConfig();   
			RegisterPermissions();
			CheckData(1);

            var command = Interface.Oxide.GetLibrary<Command>();
            command.AddChatCommand(configData.Settings.Command, this, "StaffToggleCommand");
        }

		void RegisterPermissions()
		{
			foreach (var group in groupData.Groups.Values)
            {
                if (!string.IsNullOrEmpty(group.PermissionNode))
                    permission.RegisterPermission(group.PermissionNode, this);
            }
			permission.RegisterPermission(configData.Settings.EditPermission, this);
		}

        void LoadData()
        {
            data = Interface.Oxide.DataFileSystem.ReadObject<Data>("Staffmode_PlayerData");
            var groupdata = Interface.Oxide.DataFileSystem.GetFile("Staffmode_Groups");
            try
            {
                groupData = groupdata.ReadObject<GroupData>();
                var update = new List<string>();
            }
            catch
            {
                groupData = new GroupData();
            }
        }

        void SaveData() 
		{
			Interface.Oxide.DataFileSystem.WriteObject("Staffmode_PlayerData", data);
		}
		
		void SaveGroups()
		{
			Interface.Oxide.DataFileSystem.WriteObject("Staffmode_Groups", groupData);
		}

		void CheckData(int value)
		{
			groupcount = 0;
			possibletotalerrors = 0;
			possiblemajorerrors = 0;
            if (!(configData.Debug.CheckGroupDataOnLoad) && value == 1) 
			{
				foreach (var group in groupData.Groups.Values)
				{
					groupcount++;
				}
				return;
			}

			Puts("Checking groups...");
			foreach (var group in groupData.Groups.Values)
			{
				groupcount++;
				if (configData.Settings.EnableGroupToggle)
				{
                    if (!(group.OnDutyGroup == null))
                    {
                        try
                        {
                            if (!(permission.GroupExists(group.OnDutyGroup))) { Puts("Permission Group '" + group.OnDutyGroup.ToString() + "' does not exist. Check to make sure this permission group exists."); possibletotalerrors++; }
                        }
                        catch (NullReferenceException)
                        {
                            Puts("Check could not continue for group '" + group.GroupName.ToString() + ".' Check for any 'null' settings.");
                            possibletotalerrors++;
                            possiblemajorerrors++;
                            continue;
                        }
                    }
                    else { Puts("Group '" + group.GroupName.ToString() + "' OnDutyGroup is null with GroupToggling enabled."); possibletotalerrors++; }

                    if (!(group.OffDutyGroup == null))
                    {
                        try
                        {
                            if (!(permission.GroupExists(group.OffDutyGroup))) { Puts("Permission Group '" + group.OffDutyGroup.ToString() + "' does not exist. Check to make sure this permission group exists."); possibletotalerrors++; }
                        }
                        catch (NullReferenceException)
                        {
                            Puts("Check could not continue for group '" + group.GroupName.ToString() + ".' Check for any 'null' settings.");
                            possibletotalerrors++;
                            possiblemajorerrors++;
                            continue;
                        }
                    }
                    else { Puts("Group '" + group.GroupName.ToString() + "' OffDutyGroup is null with GroupToggling enabled."); possibletotalerrors++; }
                }
                if (group.AuthLevel != null)
				{
                    if (group.AuthLevel != 0)
					{
                        if (group.AuthLevel != 1 && group.AuthLevel != 2) { Puts("Group '" + group.GroupName.ToString() + "' does not have a correct auth level setting. Must be '0' '1' or '2'" ); possibletotalerrors++; possiblemajorerrors++; }
                    }
				}
				if (group.PermissionNode == null)
				{
					Puts("Group '" + group.GroupName + "' permission node is null. Anyone will be able to toggle into this group.");
					possibletotalerrors++;
				}
			}
			Puts("Group check complete. Checked '" + groupcount + "' groups. Detected '" + possibletotalerrors + "' possible error(s), '" + possiblemajorerrors + "' which are critical based on your settings.");
		}

        object OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (configData.GameplaySettings.CanAttack) { return null; }
            if (!data.StaffData.ContainsKey(attacker.UserIDString) || data.StaffData[attacker.UserIDString].EnabledOffDutyMode) { return null; }
            if (configData.GameplaySettings.ShowMessages) { SendReply(attacker, configData.Settings.PluginPrefix + " " + Lang("OnAttack", attacker.UserIDString)); }
            return false;
        }

        object CanBeTargeted(BaseCombatEntity player, MonoBehaviour turret)
        {
            if (configData.GameplaySettings.CanBeTargetedByHeliAndTurrets) { return null; }
            var target = player as BasePlayer;
			if (target == null) { return null; }
            if (!(data.StaffData.ContainsKey(target.UserIDString)) || data.StaffData[target.UserIDString].EnabledOffDutyMode) { return null; }
            return false;
        }

        object CanLootPlayer(BasePlayer target, BasePlayer looter)
        {
            if (configData.GameplaySettings.CanLootPlayer) { return null; }
            if (!data.StaffData.ContainsKey(looter.UserIDString) || data.StaffData[looter.UserIDString].EnabledOffDutyMode) { return null; }
            if (configData.GameplaySettings.ShowMessages) { SendReply(looter, configData.Settings.PluginPrefix + " " + Lang("OnLootPlayer", looter.UserIDString)); }
            return false;
        }

        #endregion
        #region Commands

        [ConsoleCommand("checkgroups")]
        void CheckGroupCommand(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin != true) { return; }
            CheckData(2);
        }

		void StaffToggleCommand(BasePlayer player, string cmd, string[] args)
		{	
			if (args.Length == 0)
			{
				if (groupcount == 0)
				{
					SendReply(player, configData.Settings.PluginPrefix + " " + Lang("NoGroups", player.UserIDString));
                    Puts("Error: No groups detected. Please check your data file.");
					return;
				}
				foreach (var group in groupData.Groups.Values)
				{					
					if(!(CheckPermission(player, group.PermissionNode)) && group.PermissionNode != null)
					{
						continue;
					}
					
					PermissionDenied = false;
					if (configData.Settings.EnableGroupToggle)
					{
						if (group.OffDutyGroup == null) {  Puts("Off Duty Group not configured properly. Skipping group '" + group.GroupName + "'"); SendReply(player, configData.Settings.PluginPrefix + " " + Lang("Corrupt", player.UserIDString)); continue; }
						if (group.OnDutyGroup == null) {  Puts("On Duty Group not configured properly. Skipping group '" + group.GroupName + "'"); SendReply(player, configData.Settings.PluginPrefix + " " + Lang("Corrupt", player.UserIDString)); continue; }
					}
					
					if(!data.StaffData.ContainsKey(player.UserIDString)) { data.StaffData.Add(player.UserIDString, new StaffData(player)); }

					//Toggle on
					if(data.StaffData[player.UserIDString].EnabledOffDutyMode)
					{
						if (group.AuthLevel != 0 && !(AlreadyPowered))
						{
							if (configData.Settings.DisconnectOnToggle)
							{
								if (group.AuthLevel == 1) { ConsoleSystem.Run(ConsoleSystem.Option.Server, "moderatorid", player.UserIDString); ConsoleSystem.Run(ConsoleSystem.Option.Server, "server.writecfg"); AlreadyPowered = true; }
								else if (group.AuthLevel == 2) { ConsoleSystem.Run(ConsoleSystem.Option.Server, "ownerid", player.UserIDString); ConsoleSystem.Run(ConsoleSystem.Option.Server, "server.writecfg"); AlreadyPowered = true; } 
							}
							else if (group.AuthLevel == 1 || group.AuthLevel == 2) { player.SetPlayerFlag( BasePlayer.PlayerFlags.IsAdmin, true); AlreadyPowered = true; }
							else { Puts("Error: AuthLevel invalid for group '" + group.GroupName + ".' No AuthLevel given."); }
						}
						if (configData.Settings.EnableGroupToggle)
						{
							permission.AddUserGroup(player.UserIDString, group.OnDutyGroup.ToString());
							permission.RemoveUserGroup(player.UserIDString, group.OffDutyGroup.ToString());	
						}

						if (!(AlreadyAnnounced))
						{
							SendReply(player, configData.Settings.PluginPrefix + " " + Lang("ToggleOn", player.UserIDString));
							if (configData.Settings.LogOnToggle) { Puts(Lang("ToggleOnLog", player.UserIDString).Replace("{player.name}", player.displayName)); }
							if (configData.Settings.AnnounceOnToggle) { PrintToChat(configData.Settings.PluginPrefix + " " + Lang("ToggleOnAnnounce", player.UserIDString).Replace("{player.name}", player.displayName)); }
							if (configData.Settings.DisconnectOnToggle)
							{
								SendReply(player, configData.Settings.PluginPrefix + " " + Lang("Reconnect", player.UserIDString));
								if (!(configData.Debug.Dev)) { timer.Once(5, () => player.Kick("Disconnected")); }
							}
							AlreadyAnnounced = true;
						}		
						AlreadyToggled = true;
					}
					//Toggle off
					else if(!(data.StaffData[player.UserIDString].EnabledOffDutyMode))
					{
						if (configData.Settings.EnableGroupToggle)
						{
							permission.AddUserGroup(player.UserIDString, group.OffDutyGroup.ToString());
							permission.RemoveUserGroup(player.UserIDString, group.OnDutyGroup.ToString());	
						}
						if (group.AuthLevel != 0 && !AlreadyPowered)
						{
							if (configData.Settings.DisconnectOnToggle)
							{
								if (group.AuthLevel == 1) { ConsoleSystem.Run(ConsoleSystem.Option.Server, "removemoderator", player.UserIDString); ConsoleSystem.Run(ConsoleSystem.Option.Server, "server.writecfg"); }
								else if (group.AuthLevel == 2) { ConsoleSystem.Run(ConsoleSystem.Option.Server, "removeowner", player.UserIDString); ConsoleSystem.Run(ConsoleSystem.Option.Server, "server.writecfg"); }
							}
							else if (group.AuthLevel == 1 || group.AuthLevel == 2) { player.SetPlayerFlag( BasePlayer.PlayerFlags.IsAdmin, false); }
							else { Puts("Error: AuthLevel invalid for group '" + group.GroupName + ".' No AuthLevel revoked."); }
							AlreadyPowered = true;
						}
						if (!(AlreadyAnnounced))
						{
							SendReply(player, configData.Settings.PluginPrefix + " " + Lang("ToggleOff", player.UserIDString));
							if (configData.Settings.LogOnToggle) { Puts(Lang("ToggleOffLog", player.UserIDString).Replace("{player.name}", player.displayName)); }
							if (configData.Settings.AnnounceOnToggle) { PrintToChat(configData.Settings.PluginPrefix + " " + Lang("ToggleOffAnnounce", player.UserIDString).Replace("{player.name}", player.displayName)); }
							if (configData.Settings.DisconnectOnToggle)
							{
								SendReply(player, configData.Settings.PluginPrefix + " " + Lang("Reconnect", player.UserIDString)); 
								if (!configData.Debug.Dev) { timer.Once(5, () => player.Kick("Disconnected")); }
							}
							AlreadyAnnounced = true;
						}
						AlreadyToggled = true;
					}
				}
				if(data.StaffData.ContainsKey(player.UserIDString))
				{
					data.StaffData[player.UserIDString].EnabledOffDutyMode = !data.StaffData[player.UserIDString].EnabledOffDutyMode;
					SaveData();
				}
				AlreadyAnnounced = false;
				AlreadyPowered = false;
				if (PermissionDenied && !(AlreadyToggled)) { SendReply(player, configData.Settings.PluginPrefix + " " + Lang("NoPermission", player.UserIDString)); return; }
				PermissionDenied = true;
				AlreadyToggled = false;
				return;
			}
			else
			{
                if(!(CheckPermission(player, configData.Settings.EditPermission))) { SendReply(player, configData.Settings.PluginPrefix + " " + Lang("NoPermission", player.UserIDString)); return; }
				
                switch (args[0].ToLower())
				{
					case "group":
					{
						if (args.Length < 2)
						{
							SendReply(player, configData.Settings.PluginPrefix + " " + string.Format(Lang("Usage", player.UserIDString), configData.Settings.Command, "group", "create/remove/edit", "[groupname]"));
							return;
						}
						switch (args[1].ToLower())
						{
							case "create":
							{	
								if (args.Length != 3)
								{
									SendReply(player, configData.Settings.PluginPrefix + " " + string.Format(Lang("Usage", player.UserIDString), configData.Settings.Command, "group", "create", "[groupname]"));
                                    return;
								}

                                groupname = UppercaseFirst(args[2]);
								
								if(groupData.Groups.ContainsKey(groupname.ToString())) 
								{ 
									SendReply(player, configData.Settings.PluginPrefix + " " + Lang("AlreadyExists", player.UserIDString));
									return; 
								}
								
								groupData.Groups[groupname] = new Group { GroupName = groupname };
								SaveGroups();
								SendReply(player, configData.Settings.PluginPrefix + " " + Lang("CreatedGroup", player.UserIDString).Replace("{group}", groupname.ToString()));
								break;
							}
							case "remove":
							{	
								if (args.Length != 3)
								{
									SendReply(player, configData.Settings.PluginPrefix + " " + string.Format(Lang("Usage", player.UserIDString), configData.Settings.Command, "group", "remove", "[groupname]"));
                                    return;
								}
								
								groupname = UppercaseFirst(args[2]);
								
								if(!(groupData.Groups.ContainsKey(groupname.ToString()))) 
								{ 
									SendReply(player, configData.Settings.PluginPrefix + " " + Lang("DoesNotExist", player.UserIDString));
									return; 
								}
								
								groupData.Groups.Remove(groupname.ToString());
								SaveGroups();
								SendReply(player, configData.Settings.PluginPrefix + " " + Lang("RemovedGroup", player.UserIDString).Replace("{group}", groupname.ToString()));
								break;
							}
                            case "edit":
                            {
                                if (args.Length != 3)
                                {
                                    SendReply(player, configData.Settings.PluginPrefix + " " + string.Format(Lang("Usage", player.UserIDString), configData.Settings.Command, "group", "edit", "[groupname]"));
                                    return;
                                }

                                groupname = UppercaseFirst(args[2]);

                                if (!(groupData.Groups.ContainsKey(groupname.ToString())))
                                {
                                    SendReply(player, configData.Settings.PluginPrefix + " " + Lang("DoesNotExist", player.UserIDString));
                                    return;
                                }
                                groupEditor[player.userID] = groupname;
                                SendReply(player, configData.Settings.PluginPrefix + " " + Lang("EditingGroup", player.UserIDString).Replace("{group}", groupname));

                                foreach (var editValue in editValues)
                                {
                                    SendReply(player, configData.Settings.PluginPrefix + " " + string.Format(Lang("Usage", player.UserIDString), configData.Settings.Command, "edit", editValue.ToString(), "[value]"));
                                }
                                break;
                            }
							default:
							{	
								SendReply(player, configData.Settings.PluginPrefix + " " + string.Format(Lang("Usage", player.UserIDString), configData.Settings.Command, "group", "create/remove/edit", "[groupname]"));
                                break;
							}
						}
						break;
					}
                    case "edit":
                    {
                        if (!(groupEditor.TryGetValue(player.userID, out groupname)))
                        {
                            SendReply(player, configData.Settings.PluginPrefix + " " + Lang("NotEditingGroup", player.UserIDString));
                            return;
                        }

                        if (args.Length != 3)
                        {
                            foreach (var editValue in editValues)
                            {
                                SendReply(player, configData.Settings.PluginPrefix + " " + string.Format(Lang("Usage", player.UserIDString), configData.Settings.Command, "edit", editValue.ToString(), "[value]"));
                            }
                            return;
                        }

                        Group group;

                        if (!(groupData.Groups.TryGetValue(groupname, out group))) { SendReply(player, configData.Settings.PluginPrefix + " " + "An error has occured, try reselecting this group in the editor."); }

                        switch (args[1].ToLower())
                        {
                            case "authlevel":
                            {
                                AuthLevel = int.Parse(args[2]);
                                if (!(AuthLevel == 1 || AuthLevel == 2 || AuthLevel == 0))  
                                {
                                    SendReply(player, configData.Settings.PluginPrefix + " Error: Invalid auth level, must be 0, 1 or 2.");
                                    return;
                                }
                                group.AuthLevel = AuthLevel;
                                SaveGroups();
                                SendReply(player, configData.Settings.PluginPrefix + " " + string.Format(Lang("UpdatedValue", player.UserIDString), "AuthLevel", AuthLevel, groupname));
                                break;
                            }
                            case "offdutygroup":
                            {
                                OffDutygroup = args[2].ToLower();
                                if (!(permission.GroupExists(OffDutygroup)))
                                {
                                    SendReply(player, configData.Settings.PluginPrefix + " Error: This permission group does not exist.");
                                    return;
                                }
                                group.OffDutyGroup = OffDutygroup;
                                SaveGroups();
                                SendReply(player, configData.Settings.PluginPrefix + " " + string.Format(Lang("UpdatedValue", player.UserIDString), "OffDutyGroup", OffDutygroup, groupname));
                                break;
                            }
                            case "ondutygroup":
                            {
                                OnDutygroup = args[2].ToLower();
                                if (!(permission.GroupExists(OnDutygroup)))
                                {
                                    SendReply(player, configData.Settings.PluginPrefix + " Error: This permission group does not exist.");
                                    return;
                                }
                                group.OnDutyGroup = OnDutygroup;
                                SaveGroups();
                                SendReply(player, configData.Settings.PluginPrefix + " " + string.Format(Lang("UpdatedValue", player.UserIDString), "OnDutyGroup", OnDutygroup, groupname));
                                break;
                            }
                            case "permission":
                            {
                                Permissionnode = args[2].ToLower();
                                if (permission.PermissionExists(Permissionnode))
                                {
                                    SendReply(player, configData.Settings.PluginPrefix + " Warning: This permission already exists.");
                                }
                                else
                                {
                                    permission.RegisterPermission(Permissionnode, this);
                                }
                                group.PermissionNode = Permissionnode;
                                SaveGroups();
                                SendReply(player, configData.Settings.PluginPrefix + " " + string.Format(Lang("UpdatedValue", player.UserIDString), "Permission", Permissionnode, groupname));
                                break;
                            }
                            default:
                            {
                                foreach (var editValue in editValues)
                                {
                                    SendReply(player, configData.Settings.PluginPrefix + " " + string.Format(Lang("Usage", player.UserIDString), configData.Settings.Command, "edit", editValue.ToString(), "[value]"));
                                }
                                break;
                            }
                        }
                        break;
                    }
					default:
					{
                        SendReply(player, configData.Settings.PluginPrefix + " " + string.Format(Lang("Usage", player.UserIDString), configData.Settings.Command, "group", "create/remove/edit", "[groupname]"));
						break;
					}
				}
			}
		}
        #endregion
    }
}