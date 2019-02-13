using System.Collections.Generic;
using Facepunch;
using ProtoBuf;
using Facepunch.Math;
using UnityEngine;
using System;
using Newtonsoft.Json;
using System.Linq;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
	[Info("BedShare", "ignignokt84", "0.0.5", ResourceId = 2343)]
	[Description("Bed sharing plugin")]
	class BedShare : RustPlugin
	{
		#region Variables

		static BedShare Instance;

		BagData data = new BagData();
		Dictionary<ulong, string> playerNameCache = new Dictionary<ulong, string>();
		Dictionary<uint, ulong> dummyBags = new Dictionary<uint, ulong>();

		const string PermCanUse = "bedshare.use";
		const string PermNoShare = "bedshare.noshare";
		const string PermCanSharePublic = "bedshare.public";
		const string PermCanSharePrivate = "bedshare.private";
		const string PermCanClear = "bedshare.canclear";

		const string BedPrefabName = "bed_deployed";
		
		const string UIElementPrefix = "BSUI";

		// list of PlayerUI for lookup
		List<PlayerUI> playerUIs = new List<PlayerUI>();
		// empty guid for validation
		readonly Guid EmptyGuid = new Guid(new byte[16]);

		const string GUIRespawnCommand = "BSUICmd_Respawn";

		enum Command { clear, share, sharewith, status, unshare, unsharewith };

		#endregion

		#region Lang

		// load default messages to Lang
		void LoadDefaultMessages()
		{
			var messages = new Dictionary<string, string>
			{
				{"Prefix", "<color=orange>[ BedShare ]</color> "},
				{"CannotShareOther", "You cannot {1} another player's {0}"},
				{"ShareSuccess", "This {0} is now {1}" },
				{"NotShared", "This {0} is not shared" },
				{"NoBag", "No bag or bed found" },
				{"ClearSuccess", "Successfully cleared {0} bags/beds" },
				{"NoClearPerm", "You do not have permission to clear shared beds/bags" },
				{"CommandList", "<color=cyan>Valid Commands:</color>" + System.Environment.NewLine + "{0}"},
				{"Status", "This {0} is currently {1}" },
				{"ValidateStats", "Shared bag/bed mappings validated - {0} removed" },
				{"SharedHeaderText", "Spawn in Shared Sleeping Bag" },
				{"SharedBagNameText", "{0} (public) [Shared by {1}]" },
				{"SharedBagNameTextPrivate", "{0} (private) [Shared by {1}]" },
				{"InvalidArguments", "Invalid arguments for command: {0}" },
				{"PlayersNotFound", "Unable to find player(s): {0}" }
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
			Instance = this;
			LoadDefaultMessages();
			// register both /bag and /bed since they're really ambiguous
			cmd.AddChatCommand("bag", this, "CommandDelegator");
			cmd.AddChatCommand("bed", this, "CommandDelegator");
			permission.RegisterPermission(PermCanUse, this);
			permission.RegisterPermission(PermNoShare, this);
			permission.RegisterPermission(PermCanSharePublic, this);
			permission.RegisterPermission(PermCanSharePrivate, this);
			permission.RegisterPermission(PermCanClear, this);
			LoadData();
		}

		// unload
		void Unload()
		{
			DestroyAllDummyBags();
			DestroyAllGUI();
			SaveData();
		}

		// server initialized
		void OnServerInitialized()
		{
			ValidateSharedBags();
		}

		// save data when server saves
		void OnServerSave()
		{
			timer.In(5f, () => SaveData());
		}

		#endregion

		#region Configuration

		// load default config
		bool LoadDefaultConfig()
		{
			data = new BagData();
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
				data = Config.ReadObject<BagData>();
			}
			catch (Exception) { }
			dirty = CheckConfig();
			if (data.sharedBags == null)
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
			if (data.ui == null)
			{
				data.ui = new UIConfig();
				dirty = true;
			}
			return dirty;
		}

		#endregion

		#region Command Handling

		[ConsoleCommand(GUIRespawnCommand)]
		void BSUI_Respawn(ConsoleSystem.Arg arg)
		{
			if (arg == null || !arg.HasArgs(2)) return;
			if (!CheckGUID(arg)) return;
			
			BasePlayer player = arg.Player();
			if (player == null) return;

			PlayerUI ui = FindPlayerUI(player);

			uint bagId;
			if (!uint.TryParse(arg.Args[1], out bagId))
			{
				PrintWarning("UI Error: Could not parse bagId for respawn");
				return;
			}
			if (!SleepingBag.SpawnPlayer(player, bagId))
			{
				BagUIInfo bag = ui.FindBagInfo(bagId);
				bag.message = "Failed to spawn at bag/bed";
				RefreshUI(ui, bagId);
				return;
			}
			ui.DestroyUI();
		}

		// validate guid
		bool CheckGUID(ConsoleSystem.Arg arg)
		{
			string msg = "Missing GUID";
			if (arg.Args != null && arg.Args[0] != null)
			{
				Guid g = new Guid(arg.Args[0]);
				if (g == EmptyGuid)
				{
					msg = "Empty GUID";
					goto end;
				}
				if (!playerUIs.Any(ui => ui.guid == g))
				{
					msg = "Invalid GUID";
					goto end;
				}
				return true;
			}
			// log guid validation failure as console warning
			// * this shouldn't get hit unless a player attempts to manually run a UI command, or the plugin breaks
			end:
			PrintWarning($"Invalid call to {arg.cmd.Name} by {arg.Player()?.displayName ?? "Unknown"}: {msg}");
			return false;
		}

		// command delegator
		void CommandDelegator(BasePlayer player, string command, string[] args)
		{
			if (!hasPermission(player, PermCanUse)) return;

			string message = "InvalidCommand";
			// assume args[0] is the command (beyond /bed)
			if (args != null && args.Length > 0)
				command = args[0];
			// shift arguments
			if (args != null)
			{
				if (args.Length > 1)
					args = args.Skip(1).ToArray();
				else
					args = new string[] { };
			}
			object[] opts = new object[] { command };
			if (Enum.IsDefined(typeof(Command), command))
			{
				Command cmd = (Command)Enum.Parse(typeof(Command), command);
				if ((!hasPermission(player, PermCanSharePublic) && (cmd == Command.share || cmd == Command.unshare)) ||
					(!hasPermission(player, PermCanSharePrivate) && (cmd == Command.sharewith || cmd == Command.unsharewith)))
				{
					return;
				}
				switch (cmd)
				{
					case Command.clear:
						if (hasPermission(player, PermCanClear))
							HandleClear(out message, out opts);
						else
							message = "NoClearPerm";
						break;
					case Command.share:
						HandleShare(player, true, null, out message, out opts);
						break;
					case Command.sharewith:
						if(args == null && args.Length == 0)
						{
							message = "InvalidArguments";
							break;
						}
						HandleShare(player, true, args, out message, out opts);
						break;
					case Command.status:
						HandleStatus(player, out message, out opts);
						break;
					case Command.unshare:
						HandleShare(player, false, null, out message, out opts);
						break;
					case Command.unsharewith:
						if (args == null && args.Length == 0)
						{
							message = "InvalidArguments";
							break;
						}
						HandleShare(player, false, args, out message, out opts);
						break;
					default:
						break;
				}
			}
			else
				ShowCommands(out message, out opts);
			if (message != null && message != "")
				SendMessage(player, message, opts);
		}

		// handle sharing/unsharing
		void HandleShare(BasePlayer player, bool share, string[] args, out string message, out object[] opts)
		{
			message = "ShareSuccess";
			opts = new object[] { "bag", share ? "shared" : "unshared" };

			bool with = args != null;
			bool all = false;
			List<ulong> players = new List<ulong>();
			List<string> names = new List<string>();
			if(with)
			{
				foreach (string s in args)
				{
					if (!share && s == "all")
					{
						all = true;
						break;
					}
					BasePlayer p = rust.FindPlayer(s);
					if (p == null)
					{
						names.Add(s);
						continue;
					}
					players.Add(p.userID);
				}
			}

			if(with && !all && (players.Count == 0 || players.Count != args.Length))
			{
				message = "PlayersNotFound";
				opts = new object[] { string.Join(", ", names.ToArray()) };
				return;
			}

			object entity;
			if (GetRaycastTarget(player, out entity) && entity is SleepingBag)
			{
				SleepingBag bag = entity as SleepingBag;
				if (bag.ShortPrefabName == BedPrefabName)
					opts[0] = "bed";
				if (bag.deployerUserID != player.userID && !isAdmin(player))
				{
					message = "CannotShareOther";
					opts[1] = "share";
					return;
				}
				else
				{
					if (share)
					{
						bag.secondsBetweenReuses = 0f;
						data.AddOrUpdateBag(bag.net.ID, bag.deployerUserID, players);
						playerNameCache[player.userID] = player.displayName;
					}
					else
					{
						if (!data.RemoveOrUpdateBag(bag.net.ID, players, all))
							message = "NotShared";
					}
				}
			}
			else
			{
				message = "NoBag";
				opts = new object[] { };
			}
		}

		// handle checking status of a bed/bag
		void HandleStatus(BasePlayer player, out string message, out object[] opts)
		{
			message = "Status";
			opts = new object[] { "bag", "unshared" };
			object entity;
			if (GetRaycastTarget(player, out entity) && entity is SleepingBag)
			{
				SleepingBag bag = entity as SleepingBag;
				if (bag.ShortPrefabName == BedPrefabName)
					opts[0] = "bed";
				SharedBagInfo i = data.sharedBags.FirstOrDefault(s => s.bagId == bag.net.ID);
				if (i != null)
					opts[1] = "shared " + (i.isPublic ? " (public)" : " (private)");
			}
			else
			{
				message = "NoBag";
				opts = new object[] { };
			}
		}

		// handle clearing shared bag/beds
		void HandleClear(out string message, out object[] opts)
		{
			message = "ClearSuccess";
			opts = new object[] { data.sharedBags.Count };
			data.sharedBags.Clear();
			SaveData();
		}

		#endregion

		#region Hooks

		// on player death, wait for 1s then rebuild respawnInformation including shared beds/bags
		object OnPlayerDie(BasePlayer player, HitInfo hitinfo)
		{
			if (!data.HasSharedBags() || hasPermission(player, PermNoShare, false))
				return null;
			// after 1 second, send player updated respawn info
			timer.Once(1f, () => {
				using (RespawnInformation respawnInformation = Pool.Get<RespawnInformation>())
				{
					respawnInformation.spawnOptions = Pool.Get<List<RespawnInformation.SpawnOptions>>();
					SleepingBag[] sleepingBagArray = SleepingBag.FindForPlayer(player.userID, true);
					for (int i = 0; i < (int)sleepingBagArray.Length; i++)
					{
						SleepingBag sleepingBag = sleepingBagArray[i];
						if (data.sharedBags.Count(s => s.bagId == sleepingBag.net.ID) > 0 || dummyBags.ContainsKey(sleepingBag.net.ID))
							continue;
						RespawnInformation.SpawnOptions d = Pool.Get<RespawnInformation.SpawnOptions>();
						d.id = sleepingBag.net.ID;
						d.name = sleepingBag.niceName;
						d.type = RespawnInformation.SpawnOptions.RespawnType.SleepingBag;
						d.unlockSeconds = sleepingBag.unlockSeconds;
						respawnInformation.spawnOptions.Add(d);
					}
					respawnInformation.previousLife = SingletonComponent<ServerMgr>.Instance.persistance.GetLastLifeStory(player.userID);
					respawnInformation.fadeIn = (respawnInformation.previousLife == null ? false : respawnInformation.previousLife.timeDied > (Epoch.Current - 5));
					player.ClientRPCPlayer(null, player, "OnRespawnInformation", respawnInformation);
				}
			});
			// after 6 seconds, build/display shared bag/bed UI
			if (data.HasSharedBags())
				timer.Once(6f, () => ShowGUI(player));
			return null;
		}

		// on respawn, destroy dummy bags and gui
		object OnPlayerRespawn(BasePlayer player)
		{
			DestroyDummyBags(player);
			DestroyGUI(player);
			return null;
		}

		#endregion

		#region GUI

		// find or create a PlayerUI
		PlayerUI FindPlayerUI(BasePlayer player)
		{
			PlayerUI ui = playerUIs.FirstOrDefault(u => u.UserId == player.userID);
			if (ui == null)
				playerUIs.Add(ui = new PlayerUI(player));
			return ui;
		}

		void ShowGUI(BasePlayer player)
		{
			PlayerUI ui = FindPlayerUI(player);
			bool dirty = false;
			int counter = 0;
			foreach (SharedBagInfo entry in data.sharedBags.Where(i => i.isPublic || i.sharedWith.Contains(player.userID)))
			{
				SleepingBag sleepingBag = SleepingBag.FindForPlayer(entry.owner, entry.bagId, true);
				if (sleepingBag == null)
				{
					dirty = true; // no longer a valid shared bag
					continue;
				}
				uint bagId;
				if (SpawnDummyBag(sleepingBag, player, out bagId))
				{
					string messageName = "SharedBagNameText";
					if (!entry.isPublic)
						messageName = "SharedBagNameTextPrivate";
					string bagName = string.Format(GetMessage(messageName, player.UserIDString), new object[] { sleepingBag.niceName, GetPlayerName(sleepingBag.deployerUserID) });
					CreateRespawnButton(ui, bagId, bagName, counter++);
				}
			}
			// save changes to shared mappings
			if (dirty)
				ValidateSharedBags();
		}

		void RefreshUI(PlayerUI ui, uint bagId)
		{
			BagUIInfo bag = ui.FindBagInfo(bagId);
			if (bag == null || !ui.IsPanelOpen(bagId)) return;

			CreateRespawnButton(ui, bag.id, bag.name, bag.index);
		}

		// build respawn button
		void CreateRespawnButton(PlayerUI ui, uint bagId, string bagName, int index)
		{
			BagUIInfo bag = new BagUIInfo()
			{
				id = bagId,
				index = index,
				name = bagName
			};
			// set up button position
			float xPosMin = data.ui.screenMarginX;
			float yPosMin = data.ui.screenMarginY + ((data.ui.verticalSpacer + data.ui.buttonHeight) * index);
			float xPosMax = xPosMin + data.ui.buttonWidth;
			float yPosMax = yPosMin + data.ui.buttonHeight;

			Vector2 buttonAnchorMin = new Vector2(xPosMin, yPosMin);
			Vector2 buttonAnchorMax = new Vector2(xPosMax, yPosMax);

			// set up icon layout
			float iconXMin = data.ui.iconPaddingX - data.ui.iconWidth;
			float iconYMin = data.ui.iconPaddingY;
			float iconXMax = data.ui.iconPanelWidth - data.ui.iconWidth;
			float iconYMax = 1f - iconYMin;

			Vector2 iconPosMin = new Vector2(iconXMin, iconYMin);
			Vector2 iconPosMax = new Vector2(iconXMax, iconYMax);

			// set up text layout
			float spawnTextYMin = data.ui.spawnTextPaddingY;

			Vector2 spawnTextPosMin = new Vector2(iconXMax, spawnTextYMin);
			Vector2 spawnTextPosMax = Vector2.one;

			float bagNameTextYMin = data.ui.bagNameTextPaddingY;

			Vector2 bagNameTextPosMin = new Vector2(iconXMax, bagNameTextYMin);
			Vector2 bagNameTextPosMax = new Vector2(1f, spawnTextYMin);

			string headerText = GetMessage("SharedHeaderText", ui.Player?.UserIDString);

			// build GUI elements

			string elementName = UIElementPrefix + bagId;
			string message = ui.Message(bagId);

			CuiElementContainer container = UI.CreateElementContainer(elementName, data.ui.buttonColor, buttonAnchorMin, buttonAnchorMax);
			UI.CreateLabel(ref container, elementName, headerText, 16, spawnTextPosMin, spawnTextPosMax, TextAnchor.MiddleLeft);
			UI.CreateLabel(ref container, elementName, bagName, 12, bagNameTextPosMin, bagNameTextPosMax, TextAnchor.UpperLeft, "RobotoCondensed-Regular.ttf");
			UI.CreateImage(ref container, elementName, data.ui.bagIcon, data.ui.bagIconColor, iconPosMin, iconPosMax);
			UI.CreateButton(ref container, elementName, new Color(1f, 1f, 1f, 0.05f), string.Empty, 1, Vector2.zero, Vector2.one, ui.BuildCommand(GUIRespawnCommand, bagId));

			ui.CreateUI(bag, container);
			if (!string.IsNullOrEmpty(message))
				OverlayMessageUI(ui, bagId, message, buttonAnchorMin, buttonAnchorMax);
		}

		// create a message overlay on a button
		void OverlayMessageUI(PlayerUI ui, uint id, string message, Vector2 aMin, Vector2 aMax, float displayTime = 5f)
		{
			string panelName = UIElementPrefix + id + "_Message";
			CuiElementContainer container = UI.CreateElementContainer(panelName, new Color(0.7f, 0.3f, 0.3f, 0.9f), aMin, aMax, fadeIn: 0f);
			UI.CreateLabel(ref container, panelName, $"<color=white>{message}</color>", 14, Vector2.zero, Vector2.one, fadeIn: 0f);
			ui.CreateMessageUI(id, container);
			timer.In(displayTime, () => ui.DestroyMessageUI(id));
		}

		// destroy a player's GUI elements
		void DestroyGUI(BasePlayer player, bool kill = false)
		{
			if (kill)
				FindPlayerUI(player).Kill();
			else
				FindPlayerUI(player).DestroyUI();
		}

		// destroy all player GUI elements
		void DestroyAllGUI()
		{
			foreach (BasePlayer player in BasePlayer.activePlayerList)
				DestroyGUI(player, true);
		}

		#endregion

		#region Helper Procedures

		// spawn a dummy bag at location of shared bag/bed to be used as a respawn point
		bool SpawnDummyBag(SleepingBag bag, BasePlayer player, out uint bagId)
		{
			bagId = 0;
			BaseEntity entity = GameManager.server.CreateEntity(bag.PrefabName, bag.transform.position, bag.transform.rotation, false);
			entity.limitNetworking = true;
			entity.Spawn();
			if (entity != null && entity is SleepingBag)
			{
				SleepingBag newBag = entity as SleepingBag;
				newBag.model.enabled = false;
				newBag.deployerUserID = player.userID;
				newBag.secondsBetweenReuses = 0f;
				bagId = newBag.net.ID;
				dummyBags[bagId] = player.userID;
				return true;
			}
			return false;
		}
		
		// Destroy all dummy bags for a player
		void DestroyDummyBags(BasePlayer player)
		{
			uint[] bags = dummyBags.Where(x => x.Value == player.userID).Select(pair => pair.Key).ToArray();
			if (bags == null || bags.Length == 0)
				return;
			foreach (uint bagId in bags)
				SleepingBag.DestroyBag(player, bagId);
		}

		// Destroy all dummy bags
		void DestroyAllDummyBags()
		{
			foreach(KeyValuePair<uint, ulong> entry in dummyBags)
			{
				SleepingBag bag = SleepingBag.FindForPlayer(entry.Value, entry.Key, true);
				if (bag != null)
					bag.Kill(BaseNetworkable.DestroyMode.None);
			}
			dummyBags.Clear();
		}

		// validate shared bag list
		void ValidateSharedBags()
		{
			if (!data.HasSharedBags()) return;
			List<uint> toRemove = new List<uint>();
			// check each bag in the shared bags list and remove any invalid bags
			foreach (SharedBagInfo entry in data.sharedBags)
			{
				SleepingBag sleepingBag = SleepingBag.FindForPlayer(entry.owner, entry.bagId, true);
				if (sleepingBag == null)
					toRemove.Add(entry.bagId); // no longer a valid shared bag
			}

			if (data.sharedBags.RemoveWhere(i => toRemove.Contains(i.bagId)) > 0)
			{
				Puts(GetMessage("Prefix") + string.Format(GetMessage("ValidateStats"), new object[] { toRemove.Count }));
				SaveData();
			}
		}

		// handle raycast from player
		bool GetRaycastTarget(BasePlayer player, out object closestEntity)
		{
			closestEntity = false;

			RaycastHit hit;
			if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 5f))
				return false;

			closestEntity = hit.GetEntity();
			return true;
		}

		// get a player name (using cache if possible)
		string GetPlayerName(ulong userID)
		{
			if (playerNameCache.ContainsKey(userID))
				return playerNameCache[userID];
			else
			{
				BasePlayer player = BasePlayer.FindByID(userID);
				if(player == null)
					player = BasePlayer.FindSleeping(userID);

				if (player != null)
				{
					playerNameCache[userID] = player.displayName;
					return player.displayName;
				}
			}
			return "unknown";
		}

		// check if player is an admin
		private static bool isAdmin(BasePlayer player)
		{
			if (player?.net?.connection == null) return true;
			return player.net.connection.authLevel > 0;
		}

		// check if player has permission or is an admin
		private bool hasPermission(BasePlayer player, string permname, bool allowAdmin = true)
		{
			return (allowAdmin && isAdmin(player)) || permission.UserHasPermission(player.UserIDString, permname);
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

		// show list of valid commands
		void ShowCommands(out string message, out object[] opts)
		{
			message = "CommandList";
			opts = new object[] { string.Join(", ", Enum.GetValues(typeof(Command)).Cast<Command>().Select(x => x.ToString()).ToArray()) };
		}

		#endregion

		#region Subclasses

		// config data
		class BagData
		{
			public HashSet<SharedBagInfo> sharedBags = new HashSet<SharedBagInfo>();
			public UIConfig ui;

			public void AddOrUpdateBag(uint bagID, ulong playerID, List<ulong> players)
			{
				bool isPublic = players == null || players.Count == 0;
				SharedBagInfo i = sharedBags.FirstOrDefault(s => s.bagId == bagID);
				if (i == null)
					sharedBags.Add(i = new SharedBagInfo(bagID, playerID, isPublic));
				if (!i.isPublic && !isPublic)
					i.sharedWith.UnionWith(players);
			}
			
			public bool RemoveOrUpdateBag(uint bagID, List<ulong> players, bool all = false)
			{
				if (sharedBags == null || sharedBags.Count == 0) return false;
				SharedBagInfo i = sharedBags.FirstOrDefault(s => s.bagId == bagID);
				if (i == null)
					return false;
				if(i.isPublic || all)
					return sharedBags.Remove(i);
				i.sharedWith.ExceptWith(players);
				if (i.sharedWith.Count == 0)
					sharedBags.Remove(i);
				return true;
			}

			public bool HasSharedBags()
			{
				return (sharedBags != null && sharedBags.Count > 0);
			}
		}

		// shared bag details
		class SharedBagInfo
		{
			public uint bagId;
			public ulong owner;
			public bool isPublic { private set; get; } = false;
			public HashSet<ulong> sharedWith = new HashSet<ulong>();
			public SharedBagInfo(uint bagId, ulong owner, bool isPublic)
			{
				this.bagId = bagId;
				this.owner = owner;
				this.isPublic = isPublic;
			}
		}

		// ui config
		class UIConfig
		{
			// note: any precision beyond 3 is likely visually insignificant
			public float buttonWidth = 0.25f;
			public float buttonHeight = 0.09722222222222222222222222222222f;
			public float screenMarginX = 0.025f;
			public float screenMarginY = 0.04444444444444444444444444444444f;
			public float verticalSpacer = 0.02222222222222222222222222222222f;
			public float iconWidth = 0.03854166666666666666666666666667f;
			public float iconPanelWidth = 0.21875f;
			public float iconPaddingX = 0.07083333333333333333333333333333f;
			public float iconPaddingY = 0.16190476190476190476190476190476f;
			public float spawnTextPaddingY = 0.57142857142857142857142857142857f;
			public float bagNameTextPaddingY = 0.3047619047619047619047619047619f;
			public string buttonColor = UI.FormatRGBA(new Color(0.5f, 0.4f, 0.2f, 0.9f));
			public string bagIconColor = UI.FormatRGBA(new Color(1f, 1f, 1f, 0.6f));
			public string bagIcon = "assets/icons/sleepingbag.png";
		}

		// bag ui details (for refresh)
		class BagUIInfo
		{
			public uint id;
			public string name;
			public int index = -1;
			public string message = string.Empty;
		}

		// per-player UI manager
		class PlayerUI
		{
			// use GUID to manage console commands for the UI (to prevent players from being able to execute commands directly)
			internal Guid guid = Guid.NewGuid();
			// player reference
			BasePlayer _player;
			// player id/name container (for lookup)
			internal PlayerNameID nameId;
			// player reference delegator
			internal BasePlayer Player { get { TryResolvePlayer(); return _player; } }
			// user ID delegator
			internal ulong UserId { get { return nameId != null ? nameId.userid : 0UL; } }
			// map of panels and child element names
			Dictionary<uint, List<string>> elements = new Dictionary<uint, List<string>>();
			List<BagUIInfo> bags = new List<BagUIInfo>();

			// constructor
			public PlayerUI(BasePlayer player)
			{
				_player = player;
				nameId = new PlayerNameID()
				{
					userid = player.userID,
					username = player.displayName
				};
			}

			// basic player resolution
			void TryResolvePlayer()
			{
				if (_player == null && nameId != null)
					_player = BasePlayer.FindByID(nameId.userid);
			}
			// handle UI creation
			internal void CreateUI(BagUIInfo bag, CuiElementContainer container)
			{
				DestroyUI(bag.id);
				bags.Add(bag);
				UI.RenameComponents(container);
				elements[bag.id] = container.Select(e => e.Name).ToList();
				CuiHelper.AddUi(Player, container);
			}
			internal void CreateMessageUI(uint id, CuiElementContainer container)
			{
				UI.RenameComponents(container);
				elements[id].AddRange(container.Select(e => e.Name).ToList());
				CuiHelper.AddUi(Player, container);
			}
			// destroy all UI elements
			internal void DestroyUI()
			{
				foreach (uint bagId in elements.Keys.ToList())
					DestroyUI(bagId);
			}
			// destroy a specific UI panel
			internal void DestroyUI(uint bagId)
			{
				List<string> children;
				if (elements.TryGetValue(bagId, out children))
					foreach (string child in children)
						CuiHelper.DestroyUi(Player, child);
				CuiHelper.DestroyUi(Player, UIElementPrefix + bagId.ToString());
				elements.Remove(bagId);
				bags.RemoveAll(b => b.id == bagId);
			}
			internal void DestroyMessageUI(uint bagId)
			{
				string elementName = UIElementPrefix + bagId + "_Message";
				CuiHelper.DestroyUi(Player, elementName);
				if(elements.ContainsKey(bagId))
					elements[bagId].RemoveAll(e => e == elementName);
			}
			// is UI open
			public bool IsOpen => elements.Count > 0;
			// is panel open
			public bool IsPanelOpen(uint bagId) => elements.ContainsKey(bagId);
			public List<uint> BagIds => elements.Keys.ToList();
			public BagUIInfo FindBagInfo(uint bagId) => bags.FirstOrDefault(b => b.id == bagId);
			public bool HasMessage(uint bagId) => !string.IsNullOrEmpty(Message(bagId));
			public string Message(uint bagId) => FindBagInfo(bagId)?.message ?? string.Empty;
			// destroy this
			internal void Kill()
			{
				DestroyUI();
				_player = null;
				Instance.playerUIs.Remove(this);
			}
			// build UI command by inserting GUID as first parameter
			internal string BuildCommand(params object[] command)
			{
				if (command == null || command.Length == 0) return null;
				List<string> split = command.Where(o => o != null).Select(o => o.ToString()).ToList();
				if (split.Count == 0) return null;
				split.Insert(1, guid.ToString());
				return string.Join(" ", split.ToArray());
			}
		}

		// UI build helper
		class UI
		{
			const string format = "F4";
			static string Format(float f) => f.ToString(format);
			static string Format(Vector2 v) => $"{Format(v.x)} {Format(v.y)}";
			public static string FormatRGBA(Color color) => $"{Format(color.r)} {Format(color.g)} {Format(color.b)} {Format(color.a)}";
			public static string FormatHex(Color color) => $"#{ColorUtility.ToHtmlStringRGBA(color)}";
			
			internal static string AsString(object o)
			{
				if (o is string) return (string)o;
				if (o is Color) return FormatRGBA((Color)o);
				if (o is Vector2) return Format((Vector2)o);
				if (o is float) return Format((float)o);
				return o.ToString();
			}

			public static CuiElementContainer CreateElementContainer(string panelName, object color, object aMin, object aMax, bool useCursor = true, float fadeOut = 0f, float fadeIn = 0.25f)
			{
				return CreateElementContainer(panelName, "", color, aMin, aMax, useCursor, fadeOut, fadeIn);
			}
			public static CuiElementContainer CreateElementContainer(string panelName, string background, object color, object aMin, object aMax, bool useCursor = true, float fadeOut = 0f, float fadeIn = 0.25f)
			{
				return CreateElementContainer("Overlay", panelName, background, color, aMin, aMax, useCursor, fadeOut, fadeIn);
			}
			public static CuiElementContainer CreateElementContainer(string parent, string panelName, string background, object color, object aMin, object aMax, bool useCursor = true, float fadeOut = 0f, float fadeIn = 0.25f)
			{
				return new CuiElementContainer()
				{
					{
						new CuiPanel
						{
							Image = { Color = AsString(color), Sprite = string.IsNullOrEmpty(background) ? "assets/content/ui/ui.background.tile.psd" : background, FadeIn = fadeIn },
							RectTransform = { AnchorMin = AsString(aMin), AnchorMax = AsString(aMax) },
							CursorEnabled = useCursor, FadeOut = fadeOut
						},
						new CuiElement().Parent = parent,
						panelName
					}
				};
			}
			public static void CreatePanel(ref CuiElementContainer container, string panelName, object color, object aMin, object aMax, bool cursor = false)
			{
				container.Add(new CuiPanel
				{
					Image = { Color = AsString(color), FadeIn = 0.25f },
					RectTransform = { AnchorMin = AsString(aMin), AnchorMax = AsString(aMax) },
					CursorEnabled = cursor
				},
				panelName);
			}
			public static void CreateLabel(ref CuiElementContainer container, string panelName, string text, int size, object aMin, object aMax, TextAnchor align = TextAnchor.MiddleCenter, string font = "RobotoCondensed-Bold.ttf", float fadeIn = 0.25f)
			{
				container.Add(new CuiLabel
				{
					Text = { FontSize = size, Align = align, Text = text, Font = font, FadeIn = fadeIn },
					RectTransform = { AnchorMin = AsString(aMin), AnchorMax = AsString(aMax) }
				},
				panelName);

			}
			public static void CreateButton(ref CuiElementContainer container, string panelName, object color, string text, int size, object aMin, object aMax, string command, TextAnchor align = TextAnchor.MiddleCenter)
			{
				container.Add(new CuiButton
				{
					Button = { Color = AsString(color), Command = command, FadeIn = 0.25f },
					RectTransform = { AnchorMin = AsString(aMin), AnchorMax = AsString(aMax) },
					Text = { Text = text, FontSize = size, Align = align, FadeIn = 0.25f }
				},
				panelName);
			}
			public static void CreateImage(ref CuiElementContainer container, string panelName, string png, object color, object aMin, object aMax, float fadeIn = 0.25f)
			{
				container.Add(new CuiElement
				{
					Name = CuiHelper.GetGuid(),
					Parent = panelName,
					Components =
					{
						new CuiImageComponent() {
							Sprite = png,
							Color = AsString(color),
							FadeIn = fadeIn
						},
						new CuiRectTransformComponent { AnchorMin = AsString(aMin), AnchorMax = AsString(aMax) }
					}
				});
			}
			public static void RenameComponents(CuiElementContainer container)
			{
				foreach (var element in container)
				{
					if (element.Name == "AddUI CreatedPanel")
						element.Name = CuiHelper.GetGuid();
				}
			}
		}

		#endregion
	}
}