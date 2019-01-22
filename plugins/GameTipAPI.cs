/// <summary>
/// Author: S0N_0F_BISCUIT
/// </summary>
using Newtonsoft.Json;
using Oxide.Core;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
	[Info("GameTipAPI", "S0N_0F_BISCUIT", "1.0.0", ResourceId = 2759)]
	[Description("API for displaying queued gametips to players.")]
	class GameTipAPI : RustPlugin
	{
		#region Variables
		/// <summary>
		/// Message to display in a game tip
		/// </summary>
		class Message
		{
			[JsonProperty(PropertyName = "Text")]
			public string text;
			[JsonProperty(PropertyName = "Duration")]
			public float duration;
		}
		/// <summary>
		/// Game tip queue for a given player
		/// </summary>
		class PlayerTips
		{
			public BasePlayer player = new BasePlayer();
			public Queue<Message> queue = new Queue<Message>();
			public bool active = false;
		}
		/// <summary>
		/// Scheduled game tip to broadcast to players
		/// </summary>
		class ScheduledTip
		{
			[JsonProperty(PropertyName = "Message")]
			public Message message = new Message();
			[JsonProperty(PropertyName = "Period")]
			public float period = 0;
			[JsonProperty(PropertyName = "Mandatory")]
			public bool mandatory = false;
			[JsonProperty(PropertyName = "Enabled")]
			public bool enabled = false;
		}
		/// <summary>
		/// Configuration data
		/// </summary>
		class ConfigData
		{
			[JsonProperty(PropertyName = "Scheduled Tips")]
			public List<ScheduledTip> ScheduledTips = new List<ScheduledTip>();
		}
		/// <summary>
		/// Stored plugin data
		/// </summary>
		class StoredData
		{
			public List<ulong> blacklist = new List<ulong>();
		}
		
		private ConfigData config = new ConfigData();
		private StoredData data = new StoredData();
		private List<PlayerTips> gameTips = new List<PlayerTips>();
		#endregion

		#region Localization
		/// <summary>
		/// Load messages relayed to player
		/// </summary>
		private new void LoadDefaultMessages()
		{
			// English
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["HideTips"] = "Game tips are now hidden.",
				["ShowTips"] = "Game tips are now shown.",
				["ScheduledTips"] = "Starting {0} scheduled tips."
			}, this);
		}
		#endregion

		#region Config Handling
		/// <summary>
		/// Load default config file
		/// </summary>
		protected override void LoadDefaultConfig()
		{
			Config.Clear();
			var config = new ConfigData()
			{
				ScheduledTips = new List<ScheduledTip>()
				{
					new ScheduledTip()
					{
						message = new Message()
						{
							text = "Example Message",
							duration = 5f
						},
						period = 300,
						mandatory = false,
						enabled = false
					}
				}
			};
			Config.WriteObject(config, true);
		}
		/// <summary>
		/// Load config data
		/// </summary>
		private void LoadConfigData()
		{
			config = Config.ReadObject<ConfigData>();
			Config.WriteObject(config, true);
		}
		#endregion

		#region Data Handling
		/// <summary>
		/// Load plugin data
		/// </summary>
		private void LoadData()
		{
			try
			{
				data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Title);
			}
			catch
			{
				data = new StoredData();
				SaveData();
			}
		}
		/// <summary>
		/// Save PlayerData
		/// </summary>
		private void SaveData()
		{
			Interface.Oxide.DataFileSystem.WriteObject(Title, data);
		}
		#endregion

		#region Initialization
		/// <summary>
		/// Plugin initialization
		/// </summary>
		private void Init()
		{
			// Configuration
			try
			{
				LoadConfigData();
			}
			catch
			{
				LoadDefaultConfig();
			}
			// Data
			LoadData();
		}
		/// <summary>
		/// Start all scheduled tips
		/// </summary>
		void OnServerInitialized()
		{
			try
			{
				int count = 0;
				foreach (ScheduledTip tip in config.ScheduledTips.Where(x => x.enabled))
				{
					AddScheduledTip(tip);
					count++;
				}
				Puts(Lang("ScheduledTips", null, count));
			}
			catch { }
		}
		/// <summary>
		/// Unloading Plugin
		/// </summary>
		void Unload()
		{
			SaveData();
		}
		#endregion

		#region Commands
		/// <summary>
		/// Add player to game tip blacklist
		/// </summary>
		/// <param name="player"></param>
		/// <param name="command"></param>
		/// <param name="args"></param>
		[ChatCommand("hidetips")]
		void HideGameTips(BasePlayer player, string command, string[] args)
		{
			if (!data.blacklist.Contains(player.userID))
			{
				data.blacklist.Add(player.userID);
				player.ChatMessage(Lang("HideTips", player.UserIDString));
				SaveData();
			}
		}
		/// <summary>
		/// Remove player from game tip blacklist
		/// </summary>
		/// <param name="player"></param>
		/// <param name="command"></param>
		/// <param name="args"></param>
		[ChatCommand("showtips")]
		void ShowGameTips(BasePlayer player, string command, string[] args)
		{
			if (data.blacklist.Contains(player.userID))
			{
				data.blacklist.Remove(player.userID);
				player.ChatMessage(Lang("ShowTips", player.UserIDString));
				SaveData();
			}
		}
		#endregion

		#region Functionality
		/// <summary>
		/// Create a new game tip
		/// </summary>
		/// <param name="player"></param>
		/// <param name="message"></param>
		/// <param name="duration"></param>
		/// <param name="mandatory"></param>
		void ShowGameTip(BasePlayer player, string message, float duration = 5f, bool mandatory = false)
		{
			if (player == null || string.IsNullOrEmpty(message))
				return;
			if (!mandatory && data.blacklist.Contains(player.userID))
				return;

			PlayerTips tip;
			if ((tip = gameTips.Find(x => x.player == player)) != null)
				tip.queue.Enqueue(new Message() { text = message, duration = duration });
			else
			{
				gameTips.Add(tip = new PlayerTips() { player = player });
				tip.queue.Enqueue(new Message() { text = message, duration = duration });
			}
			
			if (!tip.active)
				Display(tip);
		}
		/// <summary>
		/// Broadcast a game tip to all players online
		/// </summary>
		/// <param name="message"></param>
		/// <param name="duration"></param>
		/// <param name="mandatory"></param>
		void BroadcastGameTip(string message, float duration = 5f, bool mandatory = false)
		{
			if (string.IsNullOrEmpty(message))
				return;

			foreach (BasePlayer player in BasePlayer.activePlayerList)
			{
				if (!mandatory && data.blacklist.Contains(player.userID))
					continue;

				PlayerTips playerTips;
				if ((playerTips = gameTips.Find(x => x.player == player)) != null)
					playerTips.queue.Enqueue(new Message() { text = message, duration = duration });
				else
				{
					gameTips.Add(playerTips = new PlayerTips() { player = player });
					playerTips.queue.Enqueue(new Message() { text = message, duration = duration });
				}

				if (!playerTips.active)
					Display(playerTips);
			}
		}
		/// <summary>
		/// Create a scheduled tip
		/// </summary>
		/// <param name="tip"></param>
		private void AddScheduledTip(ScheduledTip tip)
		{
			BroadcastGameTip(tip.message.text, tip.message.duration, tip.mandatory);
			timer.Once(tip.period, () => { AddScheduledTip(tip); });
		}
		/// <summary>
		/// Display a tip to a given player
		/// </summary>
		/// <param name="tips"></param>
		private void Display(PlayerTips tips)
		{
			tips.active = true;
			Message message = tips.queue.Dequeue();

			if (!tips.player.IsConnected)
			{
				gameTips.Remove(tips);
				return;
			}
			
			tips.player?.SendConsoleCommand("gametip.hidegametip");
			tips.player?.SendConsoleCommand("gametip.showgametip", message.text);
			timer.Once(message.duration, () =>
			{
				tips.player?.SendConsoleCommand("gametip.hidegametip");
				if (tips.queue.Count > 0)
					Display(tips);
				else
					gameTips.Remove(tips);
			});
		}
		#endregion

		#region Helpers
		/// <summary>
		/// Get string and format from lang file
		/// </summary>
		/// <param name="key"></param>
		/// <param name="userId"></param>
		/// <param name="args"></param>
		/// <returns></returns>
		private string Lang(string key, string userId = null, params object[] args) => string.Format(lang.GetMessage(key, this, userId), args);
		#endregion
	}
}