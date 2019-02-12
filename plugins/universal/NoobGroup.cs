using System;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Noob Group", "YpDutch", "1.0.0")]
	[Description("Sends a message when players joins server for the first time and puts them in a noob group")]
    public class NoobGroup : CovalencePlugin
    {
		//Required Permissions
        private const string permReturning = "NoobGroup.Return";
		private const string AdminPerm = "NoobGroup.isadmin";
		
		//Required Groups
		private const string FirstJoinGroup = "Newbies";
		private const string ReturningGroup = "ReturningPlayers";	
		
		private string NoobTimeCounter;

		[PluginReference]
		private Plugin TimedPermissions;

		//Code ran on initializing plugin
        private void Init()
        {
			LoadDefaultConfig();
			
			permission.RegisterPermission(permReturning, this);
			//permission.RegisterPermission(AdminPerm, this);
			
			if (permission.GroupExists(FirstJoinGroup)) return;
				{
					Puts("Group does not exist yet! Creating.....");
					permission.CreateGroup(FirstJoinGroup, "FirstJoiners", 0);
				}
			if (permission.GroupExists(ReturningGroup)) return;
				{
					Puts("Group does not exist yet! Creating.....");
					permission.CreateGroup(ReturningGroup, "Returners", 0);
				}
				
        }
		
		#region Config

        protected override void LoadDefaultConfig()
        {			
			Config["NoobTiming (d/h/m)"] = NoobTimeCounter = GetConfig("NoobTiming (d/h/m)", "60m");
            SaveConfig();
        }

        private T GetConfig<T>(string name, T value)
        {
            return Config[name] == null ? value : (T) Convert.ChangeType(Config[name], typeof(T));
        }

        #endregion
		
		#region Hooks
		
		private void Loaded()
		{
			if (TimedPermissions == null)
			{
				LogError("Timed permissions is not loaded, get it at https://umod.org/plugins/timed-permissions");
			}
			
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["NoobResetMessage"] = "All noobs have been reset!",
				["First Time Message"] = "{0} is a new player, help him/her out and don't kill on sight!",
			}, this);
			
			if (permission.GroupHasPermission(ReturningGroup, permReturning)) return;
				{
					permission.GrantGroupPermission(ReturningGroup, permReturning, this);
					Puts("Adding permission to group!");
				}
		}
		#endregion
		
        private void OnUserConnected(IPlayer player)
        {
            if (player.HasPermission(permReturning)) return;
			{
				string playerID = player.Id;
				Broadcast("First Time Message", player.Name);
				permission.AddUserGroup(playerID, ReturningGroup);
				
				if (player.BelongsToGroup(FirstJoinGroup)) return;
				{
				
					server.Command("addgroup ", player.Name, FirstJoinGroup, NoobTimeCounter);
					
				}
			}
        }

        private void Broadcast(string key, params object[] args)
        {
            foreach (var player in players.Connected) player.Message(string.Format(lang.GetMessage(key, this, player.Id), args));
        }
		
		#region Commands
		[Command("noobgroup.reset"), Permission(AdminPerm)]
        private void CmdReset(IPlayer player, string cmd, string[] args)
		{
				permission.RemoveGroup(ReturningGroup);
				permission.CreateGroup(ReturningGroup, "Returners", 0);

				player.Reply(lang.GetMessage("NoobResetMessage", this, player.Id));
                return;
		
		}
		
		[Command("noobgroup.debug"), Permission(AdminPerm)]
        private void Cmddebug(IPlayer player, string cmd, string[] args)
		{

				Puts("say ", player.Name, FirstJoinGroup, NoobTimeCounter);				
                return;
		
		}
		#endregion
	}
}
