using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("BenchCraft", "ignignokt84", "0.0.15", ResourceId = 2338)]
	[Description("Involve repair benches in crafting")]
	class BenchCraft : RustPlugin
	{
		#region Variables

		[PluginReference]
		Plugin ZLevelsRemastered;
		bool useZLevels;

		BenchCraftData data = new BenchCraftData();
		// map of item shortname -> craft time
		Dictionary<string, float> timeCache = new Dictionary<string, float>();
		// map of player ID -> GUIFlags
		Dictionary<ulong, int> guiCache = new Dictionary<ulong, int>();
		// map of crafting task ID -> timer
		Dictionary<int, Timer> timers = new Dictionary<int, Timer>();

		const string PermNoPenalty = "benchcraft.nopenalty";
		const string PermNoBoost = "benchcraft.noboost";
		const string PermCraftAnywhere = "benchcraft.craftanywhere";
		const string PermIgnore = "benchcraft.ignore";

		CuiElementContainer GUIContainer;
		CuiElement GUIElement;
		CuiTextComponent GUIText;
		CuiElement LockBackground;
		CuiRectTransformComponent GUIPosition;

		const string GUIName = "BenchCraftOverlay";

		enum GUIFlags
		{
			Boost = 1,
			Penalty = 2,
			Lock = 4
		}

		enum Option
		{
			boost,
			penalty,
			baseRate,
			proximity,
			useRepairBench,
			useResearchTable,
			useItemList,
			useZLevels,
			instantBoost
		}

		object[] defaults = new object[] { 0f, 0f, 1f, 1f, true, false, false, true, false };

		// repeat rate for timers
		const float tickrate = 0.5f;

		bool instacraft = false;

		#endregion

		#region Lang

		// load default messages to Lang
		void LoadDefaultMessages()
		{
			var messages = new Dictionary<string, string>
			{
				{"Prefix", "<color=orange>[ BenchCraft ]</color> "},
				{"RequiresBench", "Crafting {0} requires a workbench"},
				{"WarnPenaltyTooHigh", "{0} penalty must be less than 1.00: penalty will be ignored" },
				{"BoostText", "Boost\n{0:+0%;-0%}" },
				{"PenaltyText", "Penalty\n{0:+0%;-0%}" },
				{"BoostTextInstant", "Boost\n{0:+0;-0}" },
				{"PenaltyTextInstant", "Penalty\n{0:+0;-0}" },
				{"BoostTextInstantInf", "Boost\n\u221E" },
				{"MissingItem", "Item does not exist in time map: {0}" }
			};
			lang.RegisterMessages(messages, this);
		}

		// get message from Lang
		string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);

		#endregion

		#region Loading/Unloading

		// on load
		void Loaded()
		{
			LoadDefaultMessages();
			permission.RegisterPermission(PermNoPenalty, this);
			permission.RegisterPermission(PermNoBoost, this);
			permission.RegisterPermission(PermCraftAnywhere, this);
			permission.RegisterPermission(PermIgnore, this);
			LoadData();
			CheckPenalties();
			InitGUI();
			useZLevels = ConfigValue<bool>(Option.useZLevels) && ZLevelsRemastered != null;
		}

		// on unload
		void Unload()
		{
			RestoreFromCache();
			DestroyTimers();
			DestroyAllGui();
		}

		// server initialized
		void OnServerInitialized()
		{
			SaveToCache();
			instacraft = ConVar.Craft.instant;
		}

		void OnPluginLoaded(Plugin plugin)
		{
			if (plugin.Name == "ZLevelsRemastered")
			{
				ZLevelsRemastered = plugin;
				useZLevels = ConfigValue<bool>(Option.useZLevels) && ZLevelsRemastered != null;
			}
		}

		void OnPluginUnloaded(Plugin plugin)
		{
			if (plugin.Name == "ZLevelsRemastered")
			{
				ZLevelsRemastered = null;
				useZLevels = false;
			}
		}

		// initialize GUI elements
		void InitGUI()
		{
			GUIContainer = new CuiElementContainer();

			GUIElement = new CuiElement() {
				Name = GUIName,
				FadeOut = data.ui.fadeOut,
				Components = {
					(GUIText = new CuiTextComponent() {
						Text = "",
						FontSize = data.ui.fontSize,
						Color = data.ui.boostTextColor,
						FadeIn = data.ui.fadeIn,
						Align = TextAnchor.MiddleCenter
					}),
					(GUIPosition = new CuiRectTransformComponent() {
						AnchorMin = data.ui.anchorMin,
						AnchorMax = data.ui.anchorMax
					})
				}
			};

			LockBackground = new CuiElement() {
				Name = GUIName,
				FadeOut = data.ui.fadeOut,
				Components = {
					new CuiImageComponent() {
						Sprite = "assets/icons/lock.png",
						Color = data.ui.lockIconColor,
						FadeIn = data.ui.fadeIn
					},
					new CuiRectTransformComponent() {
						AnchorMin = data.ui.lockAnchorMin,
						AnchorMax = data.ui.lockAnchorMax
					}
				}
			};
		}

		#endregion

		#region Configuration

		// load default config
		bool LoadDefaultConfig()
		{
			data.items = new Dictionary<string, CraftSettings>();
			CheckConfig();
			return true;
		}

		// load data
		void LoadData()
		{
			bool dirty = false;
			Config.Settings.NullValueHandling = NullValueHandling.Include;
			try
			{
				data = Config.ReadObject<BenchCraftData>();
			}
			catch (Exception) { }
			dirty = CheckConfig();
			if (data.items == null)
				dirty |= LoadDefaultConfig();
			if (dirty)
				SaveData();
		}

		// write data container to config
		void SaveData()
		{
			Config.WriteObject(data);
		}

		// get value from config (handles type conversion)
		T GetConfig<T>(string group, string name, T value)
		{
			if (Config[group, name] == null)
			{
				Config[group, name] = value;
				SaveConfig();
			}
			return (T)Convert.ChangeType(Config[group, name], typeof(T));
		}

		// validate config
		bool CheckConfig()
		{
			bool dirty = false;
			foreach (Option option in Enum.GetValues(typeof(Option)))
				if (!data.config.ContainsKey(option))
				{
					data.config[option] = defaults[(int)option];
					dirty = true;
				}
			if (data.ui == null)
			{
				data.ui = new UIConfig();
				dirty = true;
			}
			return dirty;
		}

		#endregion

		#region Hooks

		// on item craft task created
		object OnItemCraft(ItemCraftTask task, BasePlayer player, Item item)
		{
			if (hasPermission(player, PermIgnore) || instacraft) return null;
			if (task.blueprint.targetItem.shortname == "door.key") return null; // exclude door keys
			float boost = ConfigValue<float>(Option.boost);
			float penalty = ConfigValue<float>(Option.penalty);
			float baseRate = ConfigValue<float>(Option.baseRate);
			bool requireBench = false;
			if (ConfigValue<bool>(Option.useItemList) && data.items.Count > 0)
			{
				CraftSettings settings;
				if (data.items.TryGetValue(task.blueprint.targetItem.shortname, out settings))
				{
					boost = settings.boost.HasValue ? settings.boost.Value : boost;
					penalty = settings.penalty.HasValue ? settings.penalty.Value : penalty;
					baseRate = settings.baseRate.HasValue ? settings.baseRate.Value : baseRate;
					requireBench = settings.requireBench.HasValue ? settings.requireBench.Value : false;
				}
			}
			requireBench &= !hasPermission(player, PermCraftAnywhere);
			bool inRange = IsPlayerInRange(task.owner);
			if (requireBench && !inRange)
			{
				NextTick(() => {
					SendMessage(player, "RequiresBench", new object[] { task.blueprint.targetItem.displayName.translated });
					player.inventory.crafting.CancelTask(task.taskUID, true);
				});
				return null;
			}
			if (hasPermission(player, PermNoBoost))
				boost = 0f;
			if (hasPermission(player, PermNoPenalty))
				penalty = 0f;

			if (!useZLevels)
				if (timeCache.ContainsKey(task.blueprint.targetItem.shortname))
					// alter standard crafting time
					task.blueprint.time = timeCache[task.blueprint.targetItem.shortname] * baseRate;
				else
					PrintWarning(GetMessage("MissingItem"), task.blueprint.targetItem.shortname);

			// if boost and penalty will not affect crafting, skip timer creation
			if (boost > 0f || penalty > 0f || requireBench)
			{
				int taskId = task.taskUID;
				if (timers.ContainsKey(taskId))
					timers[taskId].Destroy();
				// create timer to check proximity at interval defined by tickrate constant
				timers[taskId] = timer.Every(tickrate, () => RealtimeScale(ref task, taskId, boost, penalty, requireBench));
			}

			return null;
		}

		// update at crafting finished for instacraft
		void OnItemCraftFinished(ItemCraftTask task, Item item)
		{
			if (hasPermission(task.owner, PermIgnore) || !instacraft) return;
			float boost = ConfigValue<float>(Option.boost);
			float penalty = ConfigValue<float>(Option.penalty);
			if (ConfigValue<bool>(Option.useItemList) && data.items.Count > 0)
			{
				CraftSettings settings;
				if (data.items.TryGetValue(task.blueprint.targetItem.shortname, out settings))
				{
					boost = settings.boost.HasValue ? settings.boost.Value : boost;
					penalty = settings.penalty.HasValue ? settings.penalty.Value : penalty;
				}
			}
			if (hasPermission(task.owner, PermNoBoost))
				boost = 0f;
			if (hasPermission(task.owner, PermNoPenalty))
				penalty = 0f;
			int bonus = 0;
			if (IsPlayerInRange(task.owner) && (boost > 0f))
			{
				if (UnityEngine.Random.value < boost)
				{
					bonus = item.amount;
					item.amount *= 2;
				}
			}
			else if (penalty > 0f)
			{
				if (UnityEngine.Random.value < penalty)
				{
					bonus = -item.amount;
					item.amount = 0;
				}
			}
			if (bonus != 0)
				UpdateGUI(task.owner, bonus);
		}

		#endregion

		#region Crafting Timers / Scaling

		// scale remaining time based on proximity to workbench
		void RealtimeScale(ref ItemCraftTask task, int taskId, float boost, float penalty, bool requireBench)
		{
			try
			{
				bool destroy = false;
				// task killed?
				if (task == null) destroy = true;
				// crafting not started
				else if (task.endTime == 0f) return;

				ItemCrafter crafter = task.owner.inventory.crafting;

				// crafting queue empty
				if (crafter.queue.Count == 0)
					destroy = true;

				// task is at the top of the crafting queue
				else if (task == crafter.queue.Peek())
				{
					// task is cancelled
					if (task.cancelled)
						destroy = true;

					// task is processing
					else
					{
						bool inRange = IsPlayerInRange(task.owner);
						if (!inRange && requireBench)
						{
							SendMessage(task.owner, "RequiresBench", new object[] { task.blueprint.targetItem.displayName.translated });
							task.owner.inventory.crafting.CancelTask(task.taskUID, true);
							destroy = true;
						}
						else
						{
							if (ConfigValue<bool>(Option.instantBoost) && boost > 0f && inRange)
							{
								task.endTime = Time.realtimeSinceStartup;
								UpdateGUI(task.owner, 1);
							}
							else
							{
								float factor = inRange ? boost : -penalty;
								ScaleTask(task, factor, requireBench);
								UpdateGUI(task.owner, factor, requireBench);
							}
						}
					}
				}
				if (destroy)
				{
					DestroyGUI(task.owner);
					DestroyTimer(taskId);
					return;
				}
			}
			// just in case, destroy GUI and timer
			catch (Exception)
			{
				DestroyGUI(task.owner);
				DestroyTimer(taskId);
				return;
			}
		}

		// scale the task based on the tickrate and boost/penalty
		void ScaleTask(ItemCraftTask task, float factor, bool requireBench)
		{
			task.endTime -= (tickrate * factor);

			// update player with remaining time (adjusted for current crafting rate)
			float remaining = (task.endTime - Time.realtimeSinceStartup) / (1f + factor);
			task.owner.Command("note.craft_start", new object[] { task.taskUID, remaining, task.amount });
		}

		// destroy a timer if it exists
		void DestroyTimer(int timer)
		{
			if (timers.ContainsKey(timer))
				NextTick(() => { if (timers[timer] == null || timers[timer].Destroyed) return; timers[timer].Destroy(); timers.Remove(timer); });
		}

		#endregion

		#region GUI

		// show GUI
		void ShowGUI(BasePlayer player, int flags)
		{
			CuiHelper.AddUi(player, GUIContainer);
			guiCache[player.userID] = flags;
		}

		// update GUI (instacraft)
		void UpdateGUI(BasePlayer player, int bonus)
		{
			int flags = bonus > 0 ? (int)GUIFlags.Boost : 0;
			flags += bonus < 0 ? (int)GUIFlags.Penalty : 0;

			int cacheFlags = 0;
			guiCache.TryGetValue(player.userID, out cacheFlags);

			// no GUI change
			if (cacheFlags == flags) return;

			// has existing GUI
			if (cacheFlags > 0) DestroyGUI(player);

			// needs GUI built
			if (flags > 0)
			{
				GUIContainer.Clear();
				if (bonus > 0)
				{
					if (ConfigValue<bool>(Option.instantBoost) && !instacraft)
						GUIText.Text = GetMessage("BoostTextInstantInf", player.UserIDString);
					else
						GUIText.Text = String.Format(GetMessage("BoostTextInstant", player.UserIDString), bonus);
					GUIText.Color = data.ui.boostTextColor;
				}
				else if (bonus < 0)
				{
					GUIText.Text = String.Format(GetMessage("PenaltyTextInstant", player.UserIDString), bonus);
					GUIText.Color = data.ui.penaltyTextColor;
				}

				GUIElement.Name = GUIName;
				GUIElement.Parent = "Hud";
				GUIPosition.AnchorMin = data.ui.anchorMin;
				GUIPosition.AnchorMax = data.ui.anchorMax;

				if (bonus != 0)
					GUIContainer.Add(GUIElement);
				ShowGUI(player, flags);
				if (!ConfigValue<bool>(Option.instantBoost))
					timer.In(tickrate, () => DestroyGUI(player));
			}
		}

		// update GUI if needed
		void UpdateGUI(BasePlayer player, float factor, bool requireBench)
		{
			int flags = factor > 0f ? (int)GUIFlags.Boost : 0;
			flags += factor < 0f ? (int)GUIFlags.Penalty : 0;
			flags += requireBench ? (int)GUIFlags.Lock : 0;

			int cacheFlags = 0;
			guiCache.TryGetValue(player.userID, out cacheFlags);

			// no GUI change
			if (cacheFlags == flags) return;

			// has existing GUI
			if (cacheFlags > 0) DestroyGUI(player);

			// needs GUI built
			if (flags > 0)
			{
				GUIContainer.Clear();
				if (factor > 0)
				{
					GUIText.Text = String.Format(GetMessage("BoostText", player.UserIDString), factor);
					GUIText.Color = data.ui.boostTextColor;
				}
				else if (factor < 0)
				{
					GUIText.Text = String.Format(GetMessage("PenaltyText", player.UserIDString), factor);
					GUIText.Color = data.ui.penaltyTextColor;
				}

				if (factor != 0 && requireBench)
				{
					GUIElement.Name = "SonOf" + GUIName;
					GUIElement.Parent = GUIName;
					GUIPosition.AnchorMin = "0 0";
					GUIPosition.AnchorMax = "1 1";
				}
				else
				{
					GUIElement.Name = GUIName;
					GUIElement.Parent = "Hud";
					GUIPosition.AnchorMin = data.ui.anchorMin;
					GUIPosition.AnchorMax = data.ui.anchorMax;
				}

				if (requireBench)
					GUIContainer.Add(LockBackground);
				if (factor != 0)
					GUIContainer.Add(GUIElement);
				ShowGUI(player, flags);
			}
		}

		// destroy GUI
		void DestroyGUI(BasePlayer player)
		{
			CuiHelper.DestroyUi(player, GUIName);
			guiCache.Remove(player.userID);
		}

		// destroy GUIs for all players
		void DestroyAllGui()
		{
			foreach (BasePlayer player in BasePlayer.activePlayerList)
				DestroyGUI(player);
		}

		#endregion

		#region Messaging

		// send reply to a player
		void SendMessage(BasePlayer player, string message, object[] options = null)
		{
			string msg = GetMessage(message, player.UserIDString);
			if (options != null && options.Length > 0)
				msg = String.Format(msg, options);
			SendReply(player, GetMessage("Prefix", player.UserIDString) + msg);
		}

		// log a console warning for invalid penalty
		void WarnPenaltyTooHigh(string id) => PrintWarning(GetMessage("WarnPenaltyTooHigh"), id);

		#endregion

		#region Helper Procedures

		// cache original item names and crafting times
		void SaveToCache()
		{
			foreach (ItemBlueprint bp in ItemManager.bpList)
				timeCache[bp.targetItem.shortname] = bp.time;
		}

		// restore all original crafting times
		void RestoreFromCache()
		{
			foreach (ItemBlueprint bp in ItemManager.bpList)
				bp.time = timeCache[bp.targetItem.shortname];
		}

		// check for penalties that are >= 1.0 and override them to 0.0
		void CheckPenalties()
		{
			if (ConfigValue<float>(Option.penalty) >= 1f)
			{
				data.config[Option.penalty] = 0f;
				WarnPenaltyTooHigh("Global");
			}
			if (ConfigValue<bool>(Option.useItemList) && data.items != null && data.items.Count > 0)
				foreach (KeyValuePair<string, CraftSettings> entry in data.items)
					if (entry.Value.penalty >= 1f)
					{
						entry.Value.penalty = 0f;
						WarnPenaltyTooHigh(entry.Key);
					}
		}

		// refund items on crafting cancellation
		void RefundItems(ItemCraftTask task)
		{
			task.owner.Command("note.craft_done", new object[] { task.taskUID, 0 });
			if (task.takenItems != null && task.takenItems.Count > 0)
			{
				foreach (Item takenItem in task.takenItems)
				{
					if (takenItem != null && takenItem.amount > 0)
					{
						if (takenItem.IsBlueprint() && takenItem.blueprintTargetDef == task.blueprint.targetItem)
							takenItem.UseItem(task.numCrafted);
						if (takenItem.amount <= 0 || takenItem.MoveToContainer(task.owner.inventory.containerMain, -1, true))
							continue;
						takenItem.Drop(task.owner.inventory.containerMain.dropPosition, task.owner.inventory.containerMain.dropVelocity, new Quaternion());
						task.owner.Command("note.inv", new object[] { takenItem.info.itemid, -takenItem.amount });
					}
				}
			}
		}

		// determine if player is in range of a repair bench or research table
		bool IsPlayerInRange(BasePlayer player)
		{
			List<StorageContainer> entities = new List<StorageContainer>();
			Vis.Entities(player.transform.position, ConfigValue<float>(Option.proximity), entities);
			foreach (StorageContainer e in entities)
				if ((ConfigValue<bool>(Option.useRepairBench) && e is RepairBench) || (ConfigValue<bool>(Option.useResearchTable) && e is ResearchTable))
					return true;
			return false;
		}

		// check if player has permission
		private bool hasPermission(BasePlayer player, string permname)
		{
			return permission.UserHasPermission(player.UserIDString, permname);
		}

		// get value of config option and convert type
		T ConfigValue<T>(Option option)
		{
			return (T)Convert.ChangeType(data.config[option], typeof(T));
		}

		// destroy all timers
		void DestroyTimers()
		{
			foreach (Timer timer in timers.Values)
				timer.Destroy();
		}

		#endregion

		#region Subclasses

		class BenchCraftData
		{
			public Dictionary<Option, object> config = new Dictionary<Option, object>();
			public Dictionary<string, CraftSettings> items;
			public UIConfig ui;
		}
		class CraftSettings
		{
			public float? boost;
			public float? penalty;
			public float? baseRate;
			public bool? requireBench;
		}
		class UIConfig
		{
			public float fadeIn = 0.2f;
			public float fadeOut = 0.2f;
			public int fontSize = 10;
			public string boostTextColor = "0 1 0.3 1";
			public string penaltyTextColor = "1 0 0 1";
			public string lockIconColor = "0.9 0 0 0.6";
			public string anchorMin = "0.9375 0.144";
			public string anchorMax = "0.9775 0.20";
			public string lockAnchorMin = "0.9425 0.144";
			public string lockAnchorMax = "0.9725 0.20";
		}

		#endregion
	}
}