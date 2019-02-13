using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Authentication", "José Paulo (FaD)", "2.1.0", ResourceId = 1269)]
    [Description("Players must enter a password after they wake up or else they'll be kicked.")]
    public class Authentication : RustPlugin
    {
		public class Request
		{
			public string m_steamID;
			public BasePlayer m_basePlayer;
			public bool m_authenticated;
			public int m_retries;
			public Timer m_countdown;
			
			public Request(string steamID, BasePlayer basePlayer)
			{
				m_steamID = steamID;
				m_basePlayer = basePlayer;
				m_authenticated = false;
				m_retries = 0;
			}
				
		}
		/*----------------*/
		/*Plugin Variables*/
		/*----------------*/
		
		static List<Request> requests = new List<Request>();
		
		/*----------------*/
		/*Plugin Functions*/
		/*----------------*/
		
		/*Message Functions*/
		private void write(string message)
		{
			PrintToChat(/*"<color=lightblue>[AUTH]</color> " + */ message);
		}
		
		private void write(BasePlayer player, string message)
		{
			PrintToChat(player,/*"<color=lightblue>[AUTH]</color> " + */ message);
		}
		
		/*Auth Functions*/
		private bool isEnabled()
		{
			return Convert.ToBoolean(Config["ENABLED"]);
		}
		
		private void requestAuth(Request request)
		{
			//Find and replace "{TIMEOUT}" to the timeout set in the config file
			string message = Lang("PASSWORD_REQUEST", request.m_steamID).Replace("{TIMEOUT}",Convert.ToString(Config["TIMEOUT"]));
			
			request.m_countdown = timer.Once(Convert.ToInt32(Config["TIMEOUT"]), () => request.m_basePlayer.Kick(Convert.ToString(Lang("AUTHENTICATION_TIMED_OUT", request.m_steamID))));
			
			write(request.m_basePlayer, message);
		}
		
		/*Chat Commands*/
		[ChatCommand("auth")]
		private void cmdAuth(BasePlayer player, string cmd, string[] args)
		{
			Request request = requests.Find(element => element.m_steamID == player.UserIDString);
			
			//Shouldn't happen.
			if(request == null) return;
			
			if(!request.m_authenticated) //Limit available commands if player is not authed yet
			{		
				switch(args.Length)
				{
					case 0:
						write(player, "Correct syntax: /auth [password/command] (arguments)");
						break;
					case 1:
						if(args[0] == Convert.ToString(Config["PASSWORD"]))
						{
							request.m_countdown.Destroy();
							request.m_authenticated = true;
							write(player, Lang("AUTHENTICATION_SUCCESSFUL", request.m_steamID));
						}
						else
						{
							int max_retries = Convert.ToInt32(Config["RETRIES"]);
							
							request.m_retries++;
							
							if(max_retries > 0)
							{
								if(request.m_retries == max_retries)
								{
									request.m_basePlayer.Kick(Lang("AUTHENTICATION_TIMED_OUT", request.m_steamID));
								}
								else
								{
									write(player, "Incorrect password. You have " + (max_retries - request.m_retries) + " retries left.");
								}
							}
							else
							{
								write(player, "Incorrect password. Please try again.");
							}
						}
						break;
				}
			}
			else if(/* request.m_authenticated && */permission.UserHasPermission(request.m_steamID, "authentication.edit"))
			{
				switch(args.Length)
				{
					case 0:
						write(player, "Correct syntax: /auth [password/command] (arguments)");
						break;
					case 1:
						if(args[0] == "password")// /auth password
						{
							write(player, "Password: " + Convert.ToString(Config["PASSWORD"]));
						}
						else if(args[0] == "toggle")// /auth toggle
						{
							Config["ENABLED"] = !isEnabled();
							SaveConfig();
							write(player, "Authentication is now " + ((isEnabled()) ? "enabled" : "disabled") + ".");
						}
						else if(args[0] == "status")// /auth status
						{
							write(player, "Authentication is " + ((isEnabled()) ? "enabled" : "disabled") + ".");
						}
						else if(args[0] == "timeout")
						{
							write(player, "Timeout: " + Convert.ToString(Config["TIMEOUT"]) + " seconds.");
						}
						else if(args[0] == "help")// /auth help
						{
							write(player, 
							"Authentication commands:\n"
							+ "syntax: /auth command [required] (optional)\n"
							+ "<color=lightblue>/auth [password]</color> - authenticates players\n"
							+ "<color=lightblue>/auth password (new password)</color> - shows or sets password\n"
							+ "<color=lightblue>/auth timeout (new timeout)</color> - shows or sets timeout\n"
							+ "<color=lightblue>/auth retries (new retries)</color> - shows or sets retries\n"
							+ "<color=lightblue>/auth toggle (on/off)</color> - toggles Authentication on/off\n"
							+ "<color=lightblue>/auth status</color> - shows Authentication status");
						}
						else if(args[0] == "retries")// /auth retries
						{
							write(player, "Retries: " + Convert.ToString(Config["RETRIES"]));
						}
						break;
					case 2:
						if(args[0] == "password")// /auth password [new password]
						{
							if(args[1] != "password" && args[1] != "help" && args[1] != "toggle" && args[1] != "status" && args[1] != "timeout")
							{
								Config["PASSWORD"] = args[1];
								SaveConfig();
								write(player, "New password: " + Convert.ToString(Config["PASSWORD"]));
							}
						}
						else if(args[0] == "toggle")// /auth toggle (on/off)
						{
							if(args[1] == "on")
							{
								if(!isEnabled())
								{
									Config["ENABLED"] = true;
									SaveConfig();
									write(player, "Authentication is now enabled.");
								}
								else
								{
									write(player, "Authentication is already enabled.");
								}
							}
							else if(args[1] == "off")
							{
								if(isEnabled())
								{
									Config["ENABLED"] = false;
									SaveConfig();
									write(player, "Authentication is now disabled.");
								}
								else
								{
									write(player, "Authentication is already disabled.");
								}
							}
							else
							{
								write(player, "Correct syntax: /auth toggle (on/off)");
							}
						}
						else if(args[0] == "timeout")// /auth timeout [new timeout]
						{
							int converted;
							
							try
							{
								converted = Convert.ToInt32(args[1]);
							}
							catch(FormatException e)
							{
								write(player, "Could not convert " + args[1] + " to an integer.");
								break;
							}
							
							if(converted > 0)
							{	
								Config["TIMEOUT"] = converted;
								SaveConfig();
								write(player, "New timeout: " + Convert.ToString(Config["TIMEOUT"]) + " seconds.");
							}
							else
							{
								write(player, "Timeout must be greater than 0 seconds.");
							}
							
						}
						else if(args[0] == "retries")// /auth retries [new retries]
						{
							int converted;
							
							try
							{
								converted = Convert.ToInt32(args[1]);
							}
							catch(FormatException e)
							{
								write(player, "Could not convert " + args[1] + " to an integer.");
								break;
							}
							
							if(converted >= 0)
							{	
								Config["RETRIES"] = converted;
								SaveConfig();
								write(player, "New retries: " + Convert.ToString(Config["RETRIES"]));
							}
							else
							{
								write(player, "Retries must be a positive integer.");
							}
						}
						break;
				}
			}
		}
		
		/*Console Commands*/
		[ConsoleCommand("global.auth")]
        void ccmdAuth(ConsoleSystem.Arg arg)
        {
			var args = arg.Args;
			
			if(args == null)
			{
				Puts("Correct syntax: auth [command] (arguments)");
				return;
			}
            
			switch(args.Length)
			{
				case 1:
					if(args[0] == "password")// /auth password
					{
						Puts("Password: " + Convert.ToString(Config["PASSWORD"]));
					}
					else if(args[0] == "toggle")// /auth toggle
					{
						Config["ENABLED"] = !isEnabled();
						SaveConfig();
						Puts("Authentication is now " + ((isEnabled()) ? "enabled" : "disabled") + ".");
					}
					else if(args[0] == "status")// /auth status
					{
						Puts("Authentication is " + ((isEnabled()) ? "enabled" : "disabled") + ".");
					}
					else if(args[0] == "timeout")
					{
						Puts("Timeout: " + Convert.ToString(Config["TIMEOUT"]) + " seconds.");
					}
					else if(args[0] == "help")// /auth help
					{
						Puts("Authentication commands:\n"
						+ "syntax: auth command [required] (optional)\n"
						+ "auth password (new password) - shows or sets password\n"
						+ "auth timeout (new timeout) - shows or sets timeout\n"
						+ "auth retries (new timeout) - shows or sets retries\n"
						+ "auth toggle (on/off) - toggles Authentication on/off\n"
						+ "auth status - shows Authentication status");
					}
					else if(args[0] == "retries")// /auth retries
					{
						Puts("Retries: " + Convert.ToString(Config["RETRIES"]));
					}
					break;
				case 2:
					if(args[0] == "password")// /auth password [new password]
					{
						if(args[1] != "password" && args[1] != "help" && args[1] != "toggle" && args[1] != "status" && args[1] != "timeout")
						{
							Config["PASSWORD"] = args[1];
							SaveConfig();
							Puts("New password: " + Convert.ToString(Config["PASSWORD"]));
						}
					}
					else if(args[0] == "toggle")// /auth toggle (on/off)
					{
						if(args[1] == "on")
						{
							if(!isEnabled())
							{
								Config["ENABLED"] = true;
								SaveConfig();
								Puts("Authentication is now enabled.");
							}
							else
							{
								Puts("Authentication is already enabled.");
							}
						}
						else if(args[1] == "off")
						{
							if(isEnabled())
							{
								Config["ENABLED"] = false;
								SaveConfig();
								Puts("Authentication is now disabled.");
							}
							else
							{
								Puts("Authentication is already disabled.");
							}
						}
						else
						{
							Puts("Correct syntax: /auth toggle (on/off)");
						}
					}
					else if(args[0] == "timeout")// /auth timeout [new timeout]
					{
						int converted;
						
						try
						{
							converted = Convert.ToInt32(args[1]);
						}
						catch(FormatException e)
						{
							Puts("Could not convert " + args[1] + " to an integer.");
							break;
						}
						
						if(converted > 0)
						{	
							Config["TIMEOUT"] = converted;
							SaveConfig();
							Puts("New timeout: " + Convert.ToString(Config["TIMEOUT"]) + " seconds.");
						}
						else
						{
							Puts("Timeout must be greater than 0 seconds.");
						}
						
					}
					else if(args[0] == "retries")// /auth retries [new retries]
					{
						int converted;
						
						try
						{
							converted = Convert.ToInt32(args[1]);
						}
						catch(FormatException e)
						{
							Puts("Could not convert " + args[1] + " to an integer.");
							break;
						}
						
						if(converted >= 0)
						{	
							Config["RETRIES"] = converted;
							SaveConfig();
							Puts("New retries: " + Convert.ToString(Config["RETRIES"]));
						}
						else
						{
							Puts("Retries must be a positive integer.");
						}
					}
					break;
			}
		}
		
		/*------------*/
		/*Plugin Hooks*/
		/*------------*/
		
		void Init()
		{
			permission.RegisterPermission("authentication.edit", this);
		}
		
		//Refreshes the request list when the plugin is reloaded
		void Loaded()
		{
			LoadDefaultConfig();
			
			List<BasePlayer> online = BasePlayer.activePlayerList as List<BasePlayer>;
			foreach(BasePlayer player in online)
			{
				Request request = new Request(player.UserIDString, player);
				request.m_authenticated = true;
				requests.Add(request);
			}
		}
		
		void OnPlayerSleepEnded(BasePlayer player)
		{
			if(!requests.Exists(element => element.m_steamID == player.UserIDString))
			{
				Request request = new Request(player.UserIDString, player);
				
				//Authenticate everyone if the plugin is disabled
				request.m_authenticated = !isEnabled();
				requests.Add(request);
				//And don't send the request
				if(isEnabled()) timer.Once(1, () => requestAuth(request));
			}
			
		}
		
		void OnPlayerDisconnected(BasePlayer player)
		{
			Request request = requests.Find(element => element.m_steamID == player.UserIDString);
			requests.RemoveAt(requests.IndexOf(request));
		}
		
		object OnPlayerChat(ConsoleSystem.Arg arg)
		{
			string hidden = "";
			for(int i = 0; i < Convert.ToString(Config["PASSWORD"]).Length; i++) hidden += "*";
			string original = arg.GetString(0, "text");
			string replaced = original.Replace(Convert.ToString(Config["PASSWORD"]), hidden);
			
			BasePlayer player = arg.Connection.player as BasePlayer;
			
			Request request = requests.Find(element => element.m_steamID == player.UserIDString);
			
			if(!request.m_authenticated && Convert.ToBoolean(Config["PREVENT_CHAT"]))
			{
				write(player, "You cannot chat before authentication.");
				return false;
			}
			
			if(original != replaced && Convert.ToBoolean(Config["PREVENT_CHAT_PASSWORD"]))
			{
				rust.BroadcastChat("<color=#5af>" + player.displayName + "</color>", replaced, player.UserIDString);
				return false;
			}
			
			return null;
			
		}
		
		protected override void LoadDefaultConfig()
		{
			Config["ENABLED"] = Config["ENABLED"] ?? true;
			Config["TIMEOUT"] = Config["TIMEOUT"] ?? 30;
			Config["PASSWORD"] = Config["PASSWORD"] ?? "changeme";
			Config["RETRIES"] = Config["RETRIES"] ?? 0;
			Config["PREVENT_CHAT"] = Config["PREVENT_CHAT"] ?? true;
			Config["PREVENT_CHAT_PASSWORD"] = Config["PREVENT_CHAT_PASSWORD"] ?? false;
				
			SaveConfig();	
		}
		
		private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PASSWORD_REQUEST"] = "Type /auth [password] in the following {TIMEOUT} seconds to authenticate or you'll be kicked.",
                ["AUTHENTICATION_TIMED_OUT"] = "You took too long to authenticate or exceeded the maximum amount of retries.",
                ["AUTHENTICATION_SUCCESSFUL"] = "Authentication successful."
            }, this);
		}
		
		private string Lang(string key, string id = null) => lang.GetMessage(key, this, id);
		
    }
}