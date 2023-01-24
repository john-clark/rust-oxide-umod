/// <summary>
/// Author: S0N_0F_BISCUIT
/// Permissions:
///		masterlock.toggle - Gives players the ability to use the masterlock command
///		masterlock.doorcontrol - Gives player the ability to use open/close doors commands
///	Chat Commands:
///		/masterlock - Toggles master lock on or off
///		/opendoors - Opens all doors linked with the master lock
///		/closedoors - Closes all doors linked with the master lock
/// </summary>
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Collections.Generic;

namespace Oxide.Plugins
{
	[Info("MasterLock", "S0N_0F_BISCUIT", "1.0.1", ResourceId = 2746)]
	[Description("Control all locks in a base with the tool cupboard.")]
	class MasterLock : RustPlugin
	{
		#region Variables
		[PluginReference]
		Plugin GameTipAPI;
		/// <summary>
		/// Data saved by the plugin
		/// </summary>
		class StoredData
		{
			public uint seed = 0;
			public Dictionary<uint, bool> buildings { get; set; } = new Dictionary<uint, bool>();
		}

		private StoredData data;
		private bool initialized = false;
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
				["NoBuildingPrivilege"] = "You are not within a building privilege.",
				["NoAuthorization"] = "You are not authorized in this building privilege.",
				["NoCommandPermission"] = "You do not have permission to use this command!",
				["Disabled"] = "Master lock disabled.",
				["Enabled"] = "Master lock enabled.",
				["CodeUpdate"] = "Updated code for {0} locks.",
				["AddAuthorization"] = "Authorized {0} on {1} lock(s).",
				["RemoveAuthorization"] = "Deauthorized {0} on {1} lock(s).",
				["NotEnabled"] = "Master lock is not enabled.",
				["OpenedDoors"] = "Opened {0} doors.",
				["ClosedDoors"] = "Closed {0} doors.",
				["AddLock"] = "Add a code lock to use master lock.",
				["UseCommand"] = "Use /masterlock to enable master lock"
			}, this);
		}
		#endregion

		#region Initialization
		/// <summary>
		/// Plugin initialization
		/// </summary>
		private void Init()
		{
			// Permissions
			permission.RegisterPermission("masterlock.toggle", this);
			permission.RegisterPermission("masterlock.doorcontrol", this);
			// Data
			LoadData();
		}
		/// <summary>
		/// Restore plugin data when server finishes startup
		/// </summary>
		void OnServerInitialized()
		{
			// Restore data
			FindBuildingPrivileges();
			initialized = true;
		}
		#endregion

		#region Config Handling
		/// <summary>
		/// Load default config file
		/// </summary>
		protected override void LoadDefaultConfig()
		{
			Config["Display Tooltips"] = ConfigValue("Display Tooltips");

			SaveConfig();
		}
		/// <summary>
		/// Get stored config value
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		private object ConfigValue(string value)
		{
			switch (value)
			{
				case "Display Tooltips":
					if (Config[value] == null)
						return true;
					else
						return Config[value];
				default:
					return null;
			}
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
				data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("MasterLock");
			}
			catch
			{
				data = new StoredData();
				SaveData();
			}
		}
		/// <summary>
		/// Save plugin data
		/// </summary>
		private void SaveData()
		{
			Interface.Oxide.DataFileSystem.WriteObject("MasterLock", data);
		}
		/// <summary>
		/// Find all building privileges
		/// </summary>
		private void FindBuildingPrivileges()
		{
			List<uint> delete = new List<uint>();
			foreach (uint id in data.buildings.Keys)
			{
				BaseNetworkable networkable = BaseNetworkable.serverEntities.Find(id);

				if (networkable is BuildingPrivlidge)
				{
					if (data.buildings[id])
						InitializeMasterLock(networkable as BuildingPrivlidge);
				}
				else
					delete.Add(id);
			}
			foreach (uint id in delete)
			{
				data.buildings.Remove(id);
			}
			SaveData();
			Puts($"Implemented {data.buildings.Count} saved master locks.");
		}
		#endregion

		#region Chat Commands
		/// <summary>
		/// Toggle master lock on or off
		/// </summary>
		/// <param name="player"></param>
		/// <param name="command"></param>
		/// <param name="args"></param>
		[ChatCommand("masterlock")]
		void ToggleMasterLock(BasePlayer player, string command, string[] args)
		{
			if (!permission.UserHasPermission(player.UserIDString, "masterlock.toggle") && !player.IsAdmin)
			{
				player.ChatMessage(Lang("NoCommandPermission", player.UserIDString));
				return;
			}
			BuildingPrivlidge privilege = player.GetBuildingPrivilege();
			if (!privilege)
			{
				player.ChatMessage(Lang("NoBuildingPrivilege", player.UserIDString));
				return;
			}
			if (!privilege.IsAuthed(player))
			{
				player.ChatMessage(Lang("NoAuthorization", player.UserIDString));
				return;
			}

			if (data.buildings.ContainsKey(privilege.net.ID))
			{
				if (data.buildings[privilege.net.ID])
				{
					data.buildings[privilege.net.ID] = false;
					player.ChatMessage(Lang("Disabled", player.UserIDString));
				}
				else
				{
					data.buildings[privilege.net.ID] = true;
					InitializeMasterLock(privilege);
					player.ChatMessage(Lang("Enabled", player.UserIDString));
				}
			}
			else
			{
				data.buildings.Add(privilege.net.ID, true);
				InitializeMasterLock(privilege);
				player.ChatMessage(Lang("Enabled", player.UserIDString));
			}
			SaveData();
		}
		/// <summary>
		/// Open linked doors
		/// </summary>
		/// <param name="player"></param>
		/// <param name="command"></param>
		/// <param name="args"></param>
		[ChatCommand("opendoors")]
		void OpenConnectedDoors(BasePlayer player, string command, string[] args)
		{
			if (!permission.UserHasPermission(player.UserIDString, "masterlock.doorcontrol") && !player.IsAdmin)
			{
				player.ChatMessage(Lang("NoCommandPermission", player.UserIDString));
				return;
			}
			BuildingPrivlidge privilege = player.GetBuildingPrivilege();
			if (!privilege)
			{
				player.ChatMessage(Lang("NoBuildingPrivilege", player.UserIDString));
				return;
			}
			if (!privilege.IsAuthed(player))
			{
				player.ChatMessage(Lang("NoAuthorization", player.UserIDString));
				return;
			}

			if (data.buildings.ContainsKey(privilege.net.ID))
			{
				if (data.buildings[privilege.net.ID])
					OpenDoors(privilege, player);
				else
					player.ChatMessage(Lang("NotEnabled", player.UserIDString));
			}
			else
			{
				player.ChatMessage(Lang("NotEnabled", player.UserIDString));
			}
		}
		/// <summary>
		/// Close linked doors
		/// </summary>
		/// <param name="player"></param>
		/// <param name="command"></param>
		/// <param name="args"></param>
		[ChatCommand("closedoors")]
		void CloseConnectedDoors(BasePlayer player, string command, string[] args)
		{
			if (!permission.UserHasPermission(player.UserIDString, "masterlock.doorcontrol") && !player.IsAdmin)
			{
				player.ChatMessage(Lang("NoCommandPermission", player.UserIDString));
				return;
			}
			BuildingPrivlidge privilege = player.GetBuildingPrivilege();
			if (!privilege)
			{
				player.ChatMessage(Lang("NoBuildingPrivilege", player.UserIDString));
				return;
			}
			if (!privilege.IsAuthed(player))
			{
				player.ChatMessage(Lang("NoAuthorization", player.UserIDString));
				return;
			}

			if (data.buildings.ContainsKey(privilege.net.ID))
			{
				if (data.buildings[privilege.net.ID])
					CloseDoors(privilege, player);
				else
					player.ChatMessage(Lang("NotEnabled", player.UserIDString));
			}
			else
			{
				player.ChatMessage(Lang("NotEnabled", player.UserIDString));
			}
		}
		#endregion

		#region Hooks
		/// <summary>
		/// Set code on new lock in master lock privilege area
		/// </summary>
		/// <param name="entity"></param>
		void OnEntitySpawned(BaseNetworkable entity)
		{
			if (!initialized)
				return;
			if (entity is CodeLock)
			{
				CodeLock codeLock = entity as CodeLock;
				BuildingPrivlidge privilege = codeLock.GetBuildingPrivilege();
				if (!privilege)
					return;
				if (data.buildings.ContainsKey(privilege.net.ID))
				{
					if (data.buildings[privilege.net.ID])
					{
						BaseEntity lockEntity = privilege.GetSlot(BaseEntity.Slot.Lock);
						if (lockEntity is CodeLock)
						{
							codeLock.code = (lockEntity as CodeLock).code;
							codeLock.SetFlag(BaseEntity.Flags.Locked, true);
							foreach (ProtoBuf.PlayerNameID player in privilege.authorizedPlayers)
								codeLock.whitelistPlayers.Add(player.userid);
							codeLock.SendNetworkUpdateImmediate();
						}
					}
				}
			}
			else if (entity is BuildingPrivlidge && (bool)Config["Display Tooltips"])
			{
				BasePlayer player = BasePlayer.FindByID((entity as BuildingPrivlidge).OwnerID);
				ShowGameTip(player, Lang("AddLock", player.UserIDString));
			}
		}
		/// <summary>
		/// Authorize player on all locks following master lock
		/// </summary>
		/// <param name="privilege"></param>
		/// <param name="player"></param>
		/// <returns></returns>
		object OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player)
		{
			if (data.buildings.ContainsKey(privilege.net.ID))
			{
				if (data.buildings[privilege.net.ID])
					AddAuthorization(privilege, player, player);
			}
			return null;
		}
		/// <summary>
		/// Clear authorization list on all locks following the master lock
		/// </summary>
		/// <param name="privilege"></param>
		/// <param name="player"></param>
		/// <returns></returns>
		object OnCupboardClearList(BuildingPrivlidge privilege, BasePlayer player)
		{
			if (data.buildings.ContainsKey(privilege.net.ID))
			{
				if (data.buildings[privilege.net.ID])
					ClearAuthorizations(privilege);
			}
			return null;
		}
		/// <summary>
		/// Deauthorize player on all locks following master lock
		/// </summary>
		/// <param name="privilege"></param>
		/// <param name="player"></param>
		/// <returns></returns>
		object OnCupboardDeauthorize(BuildingPrivlidge privilege, BasePlayer player)
		{
			if (data.buildings.ContainsKey(privilege.net.ID))
			{
				if (data.buildings[privilege.net.ID])
					RemoveAuthorization(privilege, player);
			}
			return null;
		}
		/// <summary>
		/// Update the code on all locks following the master lock
		/// </summary>
		/// <param name="codeLock"></param>
		/// <param name="player"></param>
		/// <param name="newCode"></param>
		/// <param name="isGuestCode"></param>
		void CanChangeCode(CodeLock codeLock, BasePlayer player, string newCode, bool isGuestCode)
		{
			if (codeLock.GetParentEntity() is BuildingPrivlidge)
			{
				BuildingPrivlidge privilege = codeLock.GetParentEntity() as BuildingPrivlidge;
				if (data.buildings.ContainsKey(privilege.net.ID))
				{
					if (data.buildings[privilege.net.ID] && !isGuestCode)
					{
						uint count = UpdateCode(privilege, newCode);
						player.ChatMessage(Lang("CodeUpdate", player.UserIDString, count));
					}
					else if (!data.buildings[privilege.net.ID] && !isGuestCode && (bool)Config["Display Tooltips"])
						ShowGameTip(BasePlayer.FindByID(privilege.OwnerID), Lang("UseCommand", player.UserIDString));
				}
				else if ((bool)Config["Display Tooltips"])
					ShowGameTip(BasePlayer.FindByID(privilege.OwnerID), Lang("UseCommand", player.UserIDString));
			}
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
		/// <summary>
		/// Initialize locks in a building
		/// </summary>
		/// <param name="privilege"></param>
		private void InitializeMasterLock(BuildingPrivlidge privilege)
		{
			BaseEntity baseLock = privilege.GetSlot(BaseEntity.Slot.Lock);

			if (baseLock is CodeLock)
			{
				string code = (baseLock as CodeLock).code;

				BuildingManager.Building building = privilege.GetBuilding();
				foreach (BuildingBlock block in building.buildingBlocks)
				{
					if (block.GetSlot(BaseEntity.Slot.Lock) is CodeLock)
					{
						CodeLock codeLock = (block.GetSlot(BaseEntity.Slot.Lock) as CodeLock);
						if (!codeLock.IsLocked())
						{
							codeLock.code = code;
							codeLock.SetFlag(BaseEntity.Flags.Locked, true);
							foreach (ProtoBuf.PlayerNameID player in privilege.authorizedPlayers)
								codeLock.whitelistPlayers.Add(player.userid);
							codeLock.SendNetworkUpdateImmediate();
						}
					}
				}
				foreach (DecayEntity entity in building.decayEntities)
				{
					if (entity.GetSlot(BaseEntity.Slot.Lock) is CodeLock)
					{
						CodeLock codeLock = (entity.GetSlot(BaseEntity.Slot.Lock) as CodeLock);
						if (!codeLock.IsLocked())
						{
							codeLock.code = code;
							codeLock.SetFlag(BaseEntity.Flags.Locked, true);
							foreach (ProtoBuf.PlayerNameID player in privilege.authorizedPlayers)
								codeLock.whitelistPlayers.Add(player.userid);
							codeLock.SendNetworkUpdateImmediate();
						}
					}
				}
			}
		}
		/// <summary>
		/// Add player to lock's whitelist
		/// </summary>
		/// <param name="privilege"></param>
		/// <param name="player"></param>
		void AddAuthorization(BuildingPrivlidge privilege, BasePlayer player, BasePlayer caller)
		{
			uint authCount = 0;
			BaseEntity baseLock = privilege.GetSlot(BaseEntity.Slot.Lock);

			if (baseLock is CodeLock)
			{
				CodeLock masterLock = baseLock as CodeLock;
				BuildingManager.Building building = privilege.GetBuilding();
				foreach (BuildingBlock block in building.buildingBlocks)
				{
					if (block.GetSlot(BaseEntity.Slot.Lock) is CodeLock)
					{
						CodeLock codeLock = (block.GetSlot(BaseEntity.Slot.Lock) as CodeLock);
						if (masterLock.code == codeLock.code)
						{
							codeLock.whitelistPlayers.Add(player.userID);
							codeLock.SendNetworkUpdateImmediate();
							authCount++;
						}
					}
				}
				foreach (DecayEntity entity in building.decayEntities)
				{
					if (entity.GetSlot(BaseEntity.Slot.Lock) is CodeLock)
					{
						CodeLock codeLock = (entity.GetSlot(BaseEntity.Slot.Lock) as CodeLock);
						if (masterLock.code == codeLock.code)
						{
							codeLock.whitelistPlayers.Add(player.userID);
							codeLock.SendNetworkUpdateImmediate();
							authCount++;
						}
					}
				}
				if (authCount > 0)
					caller.ChatMessage(Lang("AddAuthorization", caller.UserIDString, player.displayName, authCount));
			}
		}
		/// <summary>
		/// Remove player from lock's whitelist
		/// </summary>
		/// <param name="privilege"></param>
		/// <param name="player"></param>
		void RemoveAuthorization(BuildingPrivlidge privilege, BasePlayer player)
		{
			uint deauthCount = 0;
			BaseEntity baseLock = privilege.GetSlot(BaseEntity.Slot.Lock);

			if (baseLock is CodeLock)
			{
				CodeLock masterLock = baseLock as CodeLock;
				BuildingManager.Building building = privilege.GetBuilding();
				foreach (BuildingBlock block in building.buildingBlocks)
				{
					if (block.GetSlot(BaseEntity.Slot.Lock) is CodeLock)
					{
						CodeLock codeLock = (block.GetSlot(BaseEntity.Slot.Lock) as CodeLock);
						if (masterLock.code == codeLock.code)
						{
							codeLock.whitelistPlayers.Remove(player.userID);
							codeLock.SendNetworkUpdateImmediate();
							deauthCount++;
						}
					}
				}
				foreach (DecayEntity entity in building.decayEntities)
				{
					if (entity.GetSlot(BaseEntity.Slot.Lock) is CodeLock)
					{
						CodeLock codeLock = (entity.GetSlot(BaseEntity.Slot.Lock) as CodeLock);
						if (masterLock.code == codeLock.code)
						{
							codeLock.whitelistPlayers.Remove(player.userID);
							codeLock.SendNetworkUpdateImmediate();
							deauthCount++;
						}
					}
				}
				if (deauthCount > 0)
					player.ChatMessage(Lang("RemoveAuthorization", player.UserIDString, player.displayName, deauthCount));
			}
		}
		/// <summary>
		/// Clear lock's whitelist
		/// </summary>
		/// <param name="privilege"></param>
		void ClearAuthorizations(BuildingPrivlidge privilege)
		{
			BaseEntity baseLock = privilege.GetSlot(BaseEntity.Slot.Lock);

			if (baseLock is CodeLock)
			{
				CodeLock masterLock = baseLock as CodeLock;
				BuildingManager.Building building = privilege.GetBuilding();
				foreach (BuildingBlock block in building.buildingBlocks)
				{
					if (block.GetSlot(BaseEntity.Slot.Lock) is CodeLock)
					{
						CodeLock codeLock = (block.GetSlot(BaseEntity.Slot.Lock) as CodeLock);
						if (masterLock.code == codeLock.code)
						{
							codeLock.whitelistPlayers.Clear();
							codeLock.SendNetworkUpdateImmediate();
						}
					}
				}
				foreach (DecayEntity entity in building.decayEntities)
				{
					if (entity.GetSlot(BaseEntity.Slot.Lock) is CodeLock)
					{
						CodeLock codeLock = (entity.GetSlot(BaseEntity.Slot.Lock) as CodeLock);
						if (masterLock.code == codeLock.code)
						{
							codeLock.whitelistPlayers.Clear();
							codeLock.SendNetworkUpdateImmediate();
						}
					}
				}
			}
		}
		/// <summary>
		/// Update all locks following the master lock
		/// </summary>
		/// <param name="privilege"></param>
		/// <param name="newCode"></param>
		private uint UpdateCode(BuildingPrivlidge privilege, string newCode)
		{
			BaseEntity baseLock = privilege.GetSlot(BaseEntity.Slot.Lock);
			uint codeLocks = 0;
			if (baseLock is CodeLock)
			{
				CodeLock masterLock = baseLock as CodeLock;
				BuildingManager.Building building = privilege.GetBuilding();
				foreach (BuildingBlock block in building.buildingBlocks)
				{
					if (block.GetSlot(BaseEntity.Slot.Lock) is CodeLock)
					{
						CodeLock codeLock = (block.GetSlot(BaseEntity.Slot.Lock) as CodeLock);
						if (masterLock.code == codeLock.code && masterLock != codeLock)
						{
							codeLock.code = newCode;
							codeLock.SendNetworkUpdateImmediate();
							codeLocks++;
						}
					}
				}
				foreach (DecayEntity entity in building.decayEntities)
				{
					if (entity.GetSlot(BaseEntity.Slot.Lock) is CodeLock)
					{
						CodeLock codeLock = (entity.GetSlot(BaseEntity.Slot.Lock) as CodeLock);
						if (masterLock.code == codeLock.code && masterLock != codeLock)
						{
							codeLock.code = newCode;
							codeLock.SendNetworkUpdateImmediate();
							codeLocks++;
						}
					}
				}
			}
			return codeLocks;
		}
		/// <summary>
		/// Open all linked doors in the building
		/// </summary>
		/// <param name="privilege"></param>
		/// <param name="player"></param>
		private void OpenDoors(BuildingPrivlidge privilege, BasePlayer player)
		{
			uint doorCount = 0;
			BaseEntity baseLock = privilege.GetSlot(BaseEntity.Slot.Lock);

			if (baseLock is CodeLock)
			{
				CodeLock masterLock = baseLock as CodeLock;
				BuildingManager.Building building = privilege.GetBuilding();
				foreach (DecayEntity entity in building.decayEntities)
				{
					if (entity.GetSlot(BaseEntity.Slot.Lock) is CodeLock && entity is Door)
					{
						CodeLock codeLock = (entity.GetSlot(BaseEntity.Slot.Lock) as CodeLock);
						if (masterLock.code == codeLock.code && masterLock != codeLock)
						{
							if (!(entity as Door).HasFlag(BaseEntity.Flags.Open))
							{
								(entity as Door).SetFlag(BaseEntity.Flags.Open, true);
								(entity as Door).SendNetworkUpdate();
								doorCount++;
							}
						}
					}
				}
				player.ChatMessage(Lang("OpenedDoors", player.UserIDString, doorCount));
			}
		}
		/// <summary>
		/// Close all linked doors in the building
		/// </summary>
		/// <param name="privilege"></param>
		/// <param name="player"></param>
		private void CloseDoors(BuildingPrivlidge privilege, BasePlayer player)
		{
			uint doorCount = 0;
			BaseEntity baseLock = privilege.GetSlot(BaseEntity.Slot.Lock);

			if (baseLock is CodeLock)
			{
				CodeLock masterLock = baseLock as CodeLock;
				BuildingManager.Building building = privilege.GetBuilding();
				foreach (DecayEntity entity in building.decayEntities)
				{
					if (entity.GetSlot(BaseEntity.Slot.Lock) is CodeLock && entity is Door)
					{
						CodeLock codeLock = (entity.GetSlot(BaseEntity.Slot.Lock) as CodeLock);
						if (masterLock.code == codeLock.code && masterLock != codeLock)
						{
							if ((entity as Door).HasFlag(BaseEntity.Flags.Open))
							{
								(entity as Door).SetFlag(BaseEntity.Flags.Open, false);
								(entity as Door).SendNetworkUpdate();
								doorCount++;
							}
						}
					}
				}
				player.ChatMessage(Lang("ClosedDoors", player.UserIDString, doorCount));
			}
		}
		/// <summary>
		/// Display a game tip to the given player
		/// </summary>
		/// <param name="player"></param>
		/// <param name="tip"></param>
		private void ShowGameTip(BasePlayer player, string tip)
		{
			if (player == null)
				return;

			if (GameTipAPI)
				GameTipAPI.CallHook("ShowGameTip", player, tip, 5f);
			else
			{
				player.SendConsoleCommand("gametip.hidegametip");
				player.SendConsoleCommand("gametip.showgametip", tip);
				timer.Once(5f, () => player?.SendConsoleCommand("gametip.hidegametip"));
			}
		}
		#endregion
	}
}