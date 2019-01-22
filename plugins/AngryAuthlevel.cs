using System;
using System.Collections.Generic;

using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
	[Info("AngryAuthlevel", "Tori1157", "2.0.0")]
	[Description("Automatically gives or removes Authlevel for users.")]
	
	class AngryAuthlevel : CovalencePlugin
	{
		#region Fields

		private bool Changed;
        private bool delayedAdd;
        private bool loadedAdd;
        private bool addingMessage;
        private bool logAdding;
        private bool removeUnlisted;

        private string Auth1List;
		private string Auth2List;

        private const string UpdatePermission = "angryauthlevel.update";
        private const string logFilename = "logging";

        #endregion Fields

        #region Loading

        private void Init()
		{
            permission.RegisterPermission(UpdatePermission, this);

			LoadVariables();
            AddingAuthCheck();
		}

		protected override void LoadDefaultConfig()
		{
			Puts("Creating a new configuration file!");
			Config.Clear();
			LoadVariables();
        }

		private void LoadVariables() 
		{
			Auth1List = ListConfig("Authentication List", "Auth Level 1", new List<string>{""});
			Auth2List = ListConfig("Authentication List", "Auth Level 2", new List<string>{""});

            delayedAdd = BoolConfig("Options", "Add Auths On Connection", true);
            loadedAdd = BoolConfig("Options", "Add Auths On Plugin Load", false);
            addingMessage = BoolConfig("Options", "Adding Message", true);
            logAdding = BoolConfig("Options", "Log Adding", false);
            removeUnlisted = BoolConfig("Options", "Remove Unlisted Users", true);

			if (Changed)
			{
				SaveConfig();
				Changed = false;		
			}	
		}

        private void Loaded() => LoadingAdd();

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["No Permission"] = "You do not have permission to the [#cyan]{0}[/#] command!",
                ["Added By"] = "Added by {0}",
                ["Added Message"] = "Added [#cyan]{0}[/#] as [#cyan]{1}[/#]",
                ["Removed Message"] = "Removed [#cyan]{0}[/#] as [#cyan]{1}[/#]",
                ["Info Message"] = "[#red]FINISHED[/#] - Users need to rejoin for effect",
                ["Adder Message"] = "Added by {0}",
                ["Auth 1"] = "moderator",
                ["Auth 2"] = "owner",
                ["Not SteamId"] = "{0} this is not a valid SteamId.",
                ["Given Auth Level"] = "[#red]You have been given Auth Level {0} automatically, reconnect for it to activate.[/#]",
                ["Console Message"] = "{0} has been given {1} automatically.",
                ["Console Message Removal"] = "{0} has been removed from {1} automatically.",
                ["Log Added"] = "Added {0} to {1}",
                ["Log Removed"] = "Removed {0} from {1}",
                ["Activation Warning"] = "No adding option chosen, users will not be given authlevel automatically. \nEither change settings or use command. (can disable this message in config)",
            }, this);
        }

		#endregion Loading

		#region Functions

        private void AddingAuthCheck()
        {
            if (!delayedAdd && !loadedAdd)
                PrintWarning(Lang("No Activation Warning"));
        }

        private void LoadingAdd()
        {
            if (!loadedAdd)
                return;

            AddAuths(1);
            AddAuths(2);
        }

        private void OnUserConnected(IPlayer player)
        {
            if (player == null || loadedAdd || !delayedAdd)
                return;

            AddAuths(1, null, player, true);
            AddAuths(2, null, player, true);
        }

        [Command("updateauth", "updateauths")]
        private void AuthCommand(IPlayer player, string command, string[] args)
        {
            if (!CanUpdate(player) && !player.IsServer)
            {
                player.Reply(Lang("No Permission", player.Id, command));
                return;
            }

            AddAuths(1, player);
            AddAuths(2, player);

            player.Reply(Lang("Info Message", player.Id));
        }

        void AddAuths(int level, IPlayer adder = null, IPlayer player = null, bool singleAdd = false)
        {
            string lvl = Convert.ToString(level);

            if (singleAdd && player != null)
            {
                if (!AuthList(lvl).Contains(player.Id))
                {
                    if (IsMod(player.Id))
                    {
                        ServerUsers.Set(Convert.ToUInt64(player.Id), ServerUsers.UserGroup.None, null, null);
                        if (logAdding) Log(logFilename, Lang("Log Removed", null, player.Id, Lang("Auth 1")));
                    }

                    if (IsOwner(player.Id))
                    {
                        ServerUsers.Set(Convert.ToUInt64(player.Id), ServerUsers.UserGroup.None, null, null);
                        if (logAdding) Log(logFilename, Lang("Log Removed", null, player.Id, Lang("Auth 2")));
                    }
                    ServerUsers.Save();

                    return;
                }

                if (IsMod(player.Id) || IsOwner(player.Id))
                    return;

                if (level == 1 && !IsMod(player.Id))
                {
                    ServerUsers.Set(Convert.ToUInt64(player.Id), ServerUsers.UserGroup.Moderator, (player != null ? player.Name : string.Empty), (adder != null ? Lang("Adder Message", adder.Id, adder.Name) : string.Empty));

                    if (logAdding) Log(logFilename, Lang("Log Added", null, player.Id, Lang("Auth 1")));
                }

                if (level == 2 || !IsOwner(player.Id))
                {
                    ServerUsers.Set(Convert.ToUInt64(player.Id), ServerUsers.UserGroup.Owner, (player != null ? player.Name : string.Empty), (adder != null ? Lang("Adder Message", adder.Id, adder.Name) : string.Empty));

                    if (logAdding) Log(logFilename, Lang("Log Added", null, player.Id, Lang("Auth 2")));
                }

                ServerUsers.Save();

                if (addingMessage)
                    Puts(Lang($"Console Message", null, player.Id, lvl));

                if (adder != null)
                    adder.Reply(Lang($"Added Message", adder.Id, player.Id, Lang($"Auth {lvl}")));

                if (player != null)
                    timer.Once(5f, () => {
                        player.Reply(Lang($"Given Auth Level", player.Id, lvl));
                    });

                return;
            }

            if (removeUnlisted)
            {
                //int times = Members(1);
                foreach (var member in ServerUsers.GetAll(ServerUsers.UserGroup.Moderator))
                {
                    if (!AuthList("1").Contains(member.steamid.ToString()))
                    {
                        timer.Once(0.01f, () =>
                        {
                            ServerUsers.Set(member.steamid, ServerUsers.UserGroup.None, null, null);
                            ServerUsers.Save();

                            if (logAdding) Log(logFilename, Lang("Log Removed", null, member.steamid.ToString(), Lang("Auth 1")));

                            if (addingMessage)
                                Puts(Lang($"Console Message Removal", null, member.steamid.ToString(), member.group.ToString()));

                            if (adder != null)
                                adder.Reply(Lang($"Removed Message", adder.Id, member.steamid.ToString(), member.group.ToString()));
                        });
                    }
                }

                foreach (var member in ServerUsers.GetAll(ServerUsers.UserGroup.Owner))
                {
                    if (!AuthList("2").Contains(member.steamid.ToString()))
                    {
                        timer.Once(0.01f, () =>
                        {
                            ServerUsers.Set(member.steamid, ServerUsers.UserGroup.None, null, null);
                            ServerUsers.Save();

                            if (logAdding) Log(logFilename, Lang("Log Removed", null, member.steamid.ToString(), Lang("Auth 2")));

                            if (addingMessage)
                                Puts(Lang($"Console Message Removal", null, member.steamid.ToString(), member.group.ToString()));

                            if (adder != null)
                                adder.Reply(Lang($"Removed Message", adder.Id, member.steamid.ToString(), member.group.ToString()));
                        });
                    }
                }
            }

            foreach (string ID in Config["Authentication List", $"Auth Level {lvl}"] as List<object>)
            {
                if (string.IsNullOrEmpty(ID))
                    return;

                if (!ID.IsSteamId())
                {
                    PrintWarning(Lang("Not SteamId", null, ID));
                    return;
                }

                if (level == 1 && AuthList(lvl).Contains(ID) && !IsMod(ID))
                {
                    if (adder != null && !IsMod(ID) || !IsOwner(ID))
                        adder.Reply(Lang($"Added Message", adder.Id, ID, Lang($"Auth {lvl}", adder.Id)));

                    if (addingMessage && !IsMod(ID) || !IsOwner(ID))
                        Puts(Lang($"Console Message", null, ID, lvl));

                    ServerUsers.Set(Convert.ToUInt64(ID), ServerUsers.UserGroup.Moderator, (player != null ? player.Name : string.Empty), (adder != null ? Lang("Adder Message", adder.Id, adder.Name) : string.Empty));

                    if (logAdding) Log(logFilename, Lang("Log Added", null, ID, Lang("Auth 1")));
                }

                if (level == 2 && AuthList(lvl).Contains(ID) && !IsOwner(ID))
                {
                    if (adder != null && !IsMod(ID) || !IsOwner(ID))
                        adder.Reply(Lang($"Added Message", adder.Id, ID, Lang($"Auth {lvl}", adder.Id)));

                    if (addingMessage && !IsMod(ID) || !IsOwner(ID))
                        Puts(Lang($"Console Message", null, ID, lvl));

                    ServerUsers.Set(Convert.ToUInt64(ID), ServerUsers.UserGroup.Owner, (player != null ? player.Name : string.Empty), (adder != null ? Lang("Adder Message", adder.Id, adder.Name) : string.Empty));

                    if (logAdding) Log(logFilename, Lang("Log Added", null, ID, Lang("Auth 2")));
                }

                ServerUsers.Save();
            }
        }

        /*
        int Members(int level)
        {
            int num = 0;
            string ding = null;

            foreach (var member in ServerUsers.GetAll(ServerUsers.UserGroup.Moderator))
                ding = string.Join(" ", member.steamid.ToString());

            num = Convert.ToInt16(ding.LongCount()) / Convert.ToUInt16(AuthList("1").LongCount());

            if (level == 2)
                return Convert.ToInt16(ding.LongCount());

            return num;
        }
        */
        #endregion Functions

        #region Helpers

        private bool CanUpdate(IPlayer player) => player.HasPermission(UpdatePermission);
        bool IsMod(string steamId) => ServerUsers.Is(Convert.ToUInt64(steamId), ServerUsers.UserGroup.Moderator);
        bool IsOwner(string steamId) => ServerUsers.Is(Convert.ToUInt64(steamId), ServerUsers.UserGroup.Owner);
        private string AuthList(string level) => AuthenticationList(Config["Authentication List", $"Auth Level {level}"] as List<object>);
        private string AuthenticationList(List<object> list) => string.Join(" ", list);

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

        private bool BoolConfig(string menu, string dataValue, bool defaultValue) => Convert.ToBoolean(GetConfig(menu, dataValue, defaultValue));
        private string StringConfig(string menu, string dataValue, string defaultValue) => Convert.ToString(GetConfig(menu, dataValue, defaultValue));
        private string ListConfig(string menu, string dataValue, List<string> defaultValue) => Convert.ToString(GetConfig(menu, dataValue, defaultValue));

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void Log(string filename, string text) => LogToFile(filename, $"[{DateTime.Now}] {text}", this);

        #endregion Helpers
    }
}