using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Oxide.Core;

/* --------------------------------------------------------------------- */
/* --- Don't edit anything here if you don't know what you are doing --- */
/* --------------------------------------------------------------------- */

namespace Oxide.Plugins
{
	[Info("LastName", "deer_SWAG", "0.3.0", ResourceId = 1227)]
	[Description("Stores all player usernames")]
	class LastName : RustPlugin
	{
		#region Definitions

		const string PluginPermission = "lastname.use";

		class PluginData
		{
			public HashSet<PlayerData> Players = new HashSet<PlayerData>();

			public void Add(PlayerData player) => Players.Add(player);
		}

		class PlayerData
		{
			public ulong userID;
			public HashSet<string> Names = new HashSet<string>();

			public PlayerData() { }
			public PlayerData(ulong userID) { this.userID = userID; }

			public void Add(string name) => Names.Add(name);
		}

		#endregion Definitions

		PluginData data;

		protected override void LoadDefaultConfig()
		{
			CheckConfig();
			Puts("Default config was saved and loaded");
		}

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>()
			{
				{ "WrongQueryChat", "/names <name/steamID>" },
				{ "WrongQueryConsole", "player.names <name/steamID>" },
				{ "NoPlayerFound", "No players found with that name/steamID" },
				{ "PlayerWasFound", "{name}({id}) was also known as: " },
				{ "DataLoadFail", "Unable to load data file. Creating a new one" }
			}, this);
		}

		void Init()
		{
			CheckConfig();

			data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
			permission.RegisterPermission("lastname.use", this);

			if (data == null)
			{
				PrintWarning(Lang("DataLoadFail"));
				SaveData();
			}
		}

		void OnServerSave()
		{
			SaveData();
		}

		void OnPlayerConnected(Network.Message packet)
		{
			if (Config.Get<bool>("ReplaceWithFirstName"))
			{
				foreach (PlayerData dataPlayer in data.Players)
				{
					if (packet.connection.userid == dataPlayer.userID)
					{
						packet.connection.username = dataPlayer.Names.First();
						break;
					}
				}
			}
		}

		PlayerData FindPlayerData(ulong userId)
		{
			foreach (var player in data.Players)
			{
				if (player.userID == userId)
					return player;
			}

			return null;
		}

		void OnPlayerInit(BasePlayer player)
		{
			var playerData = FindPlayerData(player.userID);

			if (playerData != null)
			{
				if (!playerData.Names.Contains(player.displayName))
					playerData.Add(player.displayName);
			}
			else
			{
				PlayerData p = new PlayerData(player.userID);
				p.Add(player.displayName);
				data.Add(p);
			}
		}

		[ChatCommand("lastname")]
		void cmdChat(BasePlayer player, string command, string[] args)
		{
			if (!PlayerHasPermission(player, PluginPermission))
				return;

			if (args.Length > 0)
				PrintToChat(player, GetNames(args));
			else
				PrintToChat(player, Lang("WrongQueryChat", player));
		}

		[ConsoleCommand("player.names")]
		void cmdConsole(ConsoleSystem.Arg arg)
		{
			if (arg.HasArgs())
				Puts(GetNames(arg.Args));
			else
				Puts(Lang("WrongQueryConsole"));
		}

		string GetNames(string[] args)
		{
			var message = new StringBuilder();
			string name = string.Empty;

			message.Append(Lang("PlayerWasFound"));

			try
			{
				ulong id = Convert.ToUInt64(args[0]);
				var playerData = FindPlayerData(id);

				if (playerData != null)
				{
					name = playerData.Names.First();

					foreach (string n in playerData.Names)
						message.Append(n + ", ");
				}
			}
			catch { }
			finally
			{
				if (name.Length > 0)
				{
					message.Remove(message.Length - 2, 2).Replace("{name}", name).Replace("{id}", args[0]);
				}
				else
				{
					PlayerData found = null;

					for (int i = 0; i < args.Length; i++)
						name += args[i] + " ";

					name = name.TrimEnd();

					foreach (PlayerData dataPlayer in data.Players)
					{
						foreach (string s in dataPlayer.Names)
						{
							if (s.Equals(name, StringComparison.CurrentCultureIgnoreCase))
							{
								found = dataPlayer;
								goto end;
							}
							else if (s.StartsWith(name, StringComparison.CurrentCultureIgnoreCase))
							{
								found = dataPlayer;
								goto end;
							}
							else if (StringContains(s, name, StringComparison.CurrentCultureIgnoreCase))
							{
								found = dataPlayer;
								goto end;
							}
						}
					} end:;

					if (found != null)
					{
						foreach (string s in found.Names)
							message.Append(s + ", ");

						message.Remove(message.Length - 2, 2).Replace("{name}", name).Replace("{id}", found.userID.ToString());
					}
					else
					{
						message.Clear();
						message.Append(Lang("NoPlayerFound"));
					}
				}
			}

			return message.ToString();
		}

		void SendHelpText(BasePlayer player)
		{
			if (PlayerHasPermission(player, PluginPermission))
				PrintToChat(player, Lang("WrongQuery"));
		}

		void CheckConfig()
		{
			ConfigItem("ReplaceWithFirstName", false);

			SaveConfig();
		}

		void SaveData()
		{
			Interface.Oxide.DataFileSystem.WriteObject(Name, data);
		}

		#region Helpers

		void ConfigItem(string name, object defaultValue)
		{
			Config[name] = Config[name] ?? defaultValue;
		}

		bool StringContains(string source, string value, StringComparison comparison)
		{
			return source.IndexOf(value, comparison) >= 0;
		}

		string Lang(string key, BasePlayer player = null)
		{
			return lang.GetMessage(key, this, player?.UserIDString);
		}

		bool PlayerHasPermission(BasePlayer player, string permissionName)
		{
			return player.IsAdmin || permission.UserHasPermission(player.UserIDString, permissionName);
		}

		#endregion Helpers

	}
}