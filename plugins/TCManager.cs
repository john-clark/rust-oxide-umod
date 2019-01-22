/// <summary>
/// Author: S0N_0F_BISCUIT
/// Permissions:
///		tcmanager.upkeep - Allows player to use the /upkeep command
///		tcmanager.taxrate - Allows player to use the /taxrate command
///		tcmanager.openinv - Allows player to use the /openinv command
///		tcmanager.auth - Allows player to use the /auth command
///	Chat Commands:
///		/upkeep [0-4] - Get the current building's upkeep requirements. If a grade is provided get the current building's upkeep at that levle.
///		/taxrate - Get the resource tax rate used to calculate the upkeep
///		/tcinv - Opens the inventory of the building privlege's tool cupboard
///		/auth <player_name> - Authorizes the given player on the current tool cupboard
/// </summary>
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("TCManager", "S0N_0F_BISCUIT", "1.0.2", ResourceId = 2744)]
	[Description("Manage your tool cupboard remotely.")]
	class TCManager : RustPlugin
	{
		#region Variables
		[PluginReference]
		Plugin MasterLock, GameTipAPI;
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
				["NoBuildingPrivilege"] = "You are not within a building privlege.",
				["NoAuthorization"] = "You are not authorized in this building privlege.",
				["NoCommandPermission"] = "You do not have permission to use this command!",
				["UpkeepUsage"] = "Usage: /upkeep [0|1|2|3|4]",
				["DefaultCostHeader"] = "Upkeep Cost",
				["GradedCostHeader"] = "Maintenance Costs (Grade: {0})",
				["ItemCost"] = "{0}: {1}",
				["Line"] = "----------------------------------",
				["TaxUsage"] = "Usage: /taxrate",
				["TaxRate"] = "Tax Rate: {0}%",
				["CantLoot"] = "Can't loot right now",
				["AuthUsage"] = "Usage: /auth <player name>",
				["AuthorizePlayer"] = "Successfully authorized {0}.",
				["PlayerNotFound"] = "Unable to find player {0}.",
				["MultiplePlayers"] = "Multiple players found: {0}",
				["Tooltip /tcinv"] = "Use /tcinv to access your tool cupboard remotely.",
				["Tooltip /auth"] = "Use /auth \"player name\" to authorize another player."
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
			permission.RegisterPermission("tcmanager.upkeep", this);
			permission.RegisterPermission("tcmanager.taxrate", this);
			permission.RegisterPermission("tcmanager.openinv", this);
			permission.RegisterPermission("tcmanager.auth", this);
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

		#region Chat Commands
		/// <summary>
		/// Calculate the upkeep cost for the building
		/// </summary>
		/// <param name="player"></param>
		/// <param name="command"></param>
		/// <param name="args"></param>
		[ChatCommand("upkeep")]
		void CalculateUpkeepCost(BasePlayer player, string command, string[] args)
		{
			if (!permission.UserHasPermission(player.UserIDString, "tcmanager.upkeep") && !player.IsAdmin)
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
			if (!privilege.IsAuthed(player) && !player.IsAdmin)
			{
				player.ChatMessage(Lang("NoAuthorization", player.UserIDString));
				return;
			}

			if (args.Length == 0) // Default
			{
				List<ItemAmount> itemAmounts = Facepunch.Pool.GetList<ItemAmount>();
				privilege.CalculateUpkeepCostAmounts(itemAmounts);
				player.ChatMessage(Lang("DefaultCostHeader", player.UserIDString));
				player.ChatMessage(Lang("Line", player.UserIDString));
				foreach (ItemAmount amount in itemAmounts)
				{
					player.ChatMessage(Lang("ItemCost", player.UserIDString, amount.itemDef.displayName.translated, Math.Ceiling(amount.amount)));
				}
				Facepunch.Pool.FreeList(ref itemAmounts);
			}
			else
			{
				BuildingGrade.Enum grade = BuildingGrade.Enum.None;
				switch(args[0].ToLower())
				{
					case "0":
						grade = BuildingGrade.Enum.Twigs;
						break;
					case "1":
						grade = BuildingGrade.Enum.Wood;
						break;
					case "2":
						grade = BuildingGrade.Enum.Stone;
						break;
					case "3":
						grade = BuildingGrade.Enum.Metal;
						break;
					case "4":
						grade = BuildingGrade.Enum.TopTier;
						break;
					default:
						player.ChatMessage(Lang("UpkeepUsage", player.UserIDString));
						return;
				}
				List<ItemAmount> itemAmounts = Facepunch.Pool.GetList<ItemAmount>();
				BuildingManager.Building building = privilege.GetBuilding();
				foreach (BuildingBlock block in  building.buildingBlocks)
				{
					BuildingGrade.Enum original = block.grade;
					block.grade = grade;
					block.CalculateUpkeepCostAmounts(itemAmounts, privilege.CalculateUpkeepCostFraction());
					block.grade = original;
				}
				player.ChatMessage(Lang("GradedCostHeader", player.UserIDString, grade));
				player.ChatMessage(Lang("Line", player.UserIDString));
				foreach (ItemAmount amount in itemAmounts)
				{
					player.ChatMessage(Lang("ItemCost", player.UserIDString, amount.itemDef.displayName.translated, Math.Ceiling(amount.amount)));
				}
				Facepunch.Pool.FreeList(ref itemAmounts);
			}
		}
		/// <summary>
		/// Calculate the tax rate on the building
		/// </summary>
		/// <param name="player"></param>
		/// <param name="command"></param>
		/// <param name="args"></param>
		[ChatCommand("taxrate")]
		void GetTaxBracket(BasePlayer player, string command, string[] args)
		{
			if (!permission.UserHasPermission(player.UserIDString, "tcmanager.taxrate") && !player.IsAdmin)
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
			if (!privilege.IsAuthed(player) && !player.IsAdmin)
			{
				player.ChatMessage(Lang("NoAuthorization", player.UserIDString));
				return;
			}

			player.ChatMessage(Lang("TaxRate", player.UserIDString, CalculateTaxRate(privilege)));
		}
		/// <summary>
		/// Open the current tool cupboard inventory
		/// </summary>
		/// <param name="player"></param>
		/// <param name="command"></param>
		/// <param name="args"></param>
		[ChatCommand("tcinv")]
		void OpenToolCupboard(BasePlayer player, string command, string[] args)
		{
			if (!permission.UserHasPermission(player.UserIDString, "tcmanager.openinv") && !player.IsAdmin)
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
			if (!privilege.IsAuthed(player) && !player.IsAdmin)
			{
				player.ChatMessage(Lang("NoAuthorization", player.UserIDString));
				return;
			}

			player.EndLooting();
			timer.Once(0.1f, delegate () {
				LootContainer(player, privilege);
			});
		}
		/// <summary>
		/// Authorize given player on tool cupboard
		/// </summary>
		/// <param name="player"></param>
		/// <param name="command"></param>
		/// <param name="args"></param>
		[ChatCommand("auth")]
		void AuthorizePlayer(BasePlayer player, string command, string[] args)
		{
			if (!permission.UserHasPermission(player.UserIDString, "tcmanager.auth") && !player.IsAdmin)
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
			if (args.Length == 0)
			{
				player.ChatMessage(Lang("AuthUsage", player.UserIDString));
				return;
			}

			List<BasePlayer> playerList = FindPlayer(args[0]);

			if (playerList.Count == 1)
			{
				BasePlayer newPlayer = playerList[0];
				privilege.authorizedPlayers.Add(new ProtoBuf.PlayerNameID() { userid = newPlayer.userID, username = newPlayer.displayName });
				privilege.SendNetworkUpdateImmediate();
				player.ChatMessage(Lang("AuthorizePlayer", player.UserIDString, newPlayer.displayName));
				if (MasterLock)
					MasterLock.Call("AddAuthorization", privilege, newPlayer, player);
			}
			else if (playerList.Count == 0)
				player.ChatMessage(Lang("PlayerNotFound", player.UserIDString, args[0].ToString()));
			else
			{
				String playerNames = String.Empty;
				foreach (BasePlayer bPlayer in playerList)
				{
					if (!String.IsNullOrEmpty(playerNames))
						playerNames += ", ";
					playerNames += bPlayer.displayName;
				}
				player.ChatMessage(Lang("MultiplePlayers", player.UserIDString, playerNames));
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
			if (!(bool)Config["Display Tooltips"])
				return;
			if (entity is BuildingPrivlidge)
			{
				BasePlayer player = BasePlayer.FindByID((entity as BuildingPrivlidge).OwnerID);
				DisplayTooltips(player);
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
		/// Calculate the tax rate
		/// </summary>
		/// <param name="privlidge"></param>
		/// <returns></returns>
		private double CalculateTaxRate(BuildingPrivlidge privlidge)
		{
			BuildingPrivlidge.UpkeepBracket[] upkeepBrackets = new List<BuildingPrivlidge.UpkeepBracket>()
			{
				new BuildingPrivlidge.UpkeepBracket(ConVar.Decay.bracket_0_blockcount, ConVar.Decay.bracket_0_costfraction),
				new BuildingPrivlidge.UpkeepBracket(ConVar.Decay.bracket_1_blockcount, ConVar.Decay.bracket_1_costfraction),
				new BuildingPrivlidge.UpkeepBracket(ConVar.Decay.bracket_2_blockcount, ConVar.Decay.bracket_2_costfraction),
				new BuildingPrivlidge.UpkeepBracket(ConVar.Decay.bracket_3_blockcount, ConVar.Decay.bracket_3_costfraction)
			}.ToArray();

			BuildingManager.Building building = privlidge.GetBuilding();
			if (building == null || !building.HasBuildingBlocks())
				return ConVar.Decay.bracket_0_costfraction;
			int count = building.buildingBlocks.Count;
			int a = count;
			for (int index = 0; index < upkeepBrackets.Length; ++index)
			{
				BuildingPrivlidge.UpkeepBracket upkeepBracket = upkeepBrackets[index];
				upkeepBracket.blocksTaxPaid = 0.0f;
				if (a > 0)
				{
					int num = index != upkeepBrackets.Length - 1 ? Mathf.Min(a, upkeepBrackets[index].objectsUpTo) : a;
					a -= num;
					upkeepBracket.blocksTaxPaid = num * upkeepBracket.fraction;
				}
			}
			float num1 = 0.0f;
			for (int index = 0; index < upkeepBrackets.Length; ++index)
			{
				BuildingPrivlidge.UpkeepBracket upkeepBracket = upkeepBrackets[index];
				if (upkeepBracket.blocksTaxPaid > 0.0)
					num1 += upkeepBracket.blocksTaxPaid;
				else
					break;
			}
			return Math.Ceiling((num1 / count) * 100);
		}
		/// <summary>
		/// Loot the given container
		/// </summary>
		/// <param name="player"></param>
		/// <param name="container"></param>
		/// <returns></returns>
		private bool LootContainer(BasePlayer player, StorageContainer container)
		{
			if (container.IsLocked())
			{
				player.ChatMessage(Lang("CantLoot", player.UserIDString));
				return false;
			}
			if (!container.CanOpenLootPanel(player, container.panelName))
				return false;
			container.SetFlag(BaseEntity.Flags.Open, true, false);
			using (TimeWarning.New("PlayerOpenLoot", 0.1f))
			{
				player.inventory.loot.StartLootingEntity(container, false);
				player.inventory.loot.AddContainer(container.inventory);
				player.inventory.loot.SendImmediate();
				player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", container.panelName);
				container.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
			}
			return true;
		}
		/// <summary>
		/// Find players with the given name
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		private List<BasePlayer> FindPlayer(string name)
		{
			List<BasePlayer> playerList = BasePlayer.activePlayerList.FindAll(i => i.displayName.ToLower().Contains(name.ToLower()));
			playerList.AddRange(BasePlayer.sleepingPlayerList.FindAll(i => i.displayName.ToLower().Contains(name.ToLower())));

			return playerList;
		}
		/// <summary>
		/// Display a game tip to the given player
		/// </summary>
		/// <param name="player"></param>
		/// <param name="tip"></param>
		private void DisplayTooltips(BasePlayer player)
		{
			if (player == null)
				return;

			if (GameTipAPI)
			{
				GameTipAPI.CallHook("ShowGameTip", player, Lang("Tooltip /tcinv", player.UserIDString), 5f);
				GameTipAPI.CallHook("ShowGameTip", player, Lang("Tooltip /auth", player.UserIDString), 5f);
			}
			else
			{
				player.SendConsoleCommand("gametip.hidegametip");
				player.SendConsoleCommand("gametip.showgametip", Lang("Tooltip /tcinv", player.UserIDString));
				timer.Once(5f, () =>
				{
					player?.SendConsoleCommand("gametip.hidegametip");
					player.SendConsoleCommand("gametip.showgametip", Lang("Tooltip /auth", player.UserIDString));
					timer.Once(5f, () => player?.SendConsoleCommand("gametip.hidegametip"));
				});
			}
		}
		#endregion
	}
}