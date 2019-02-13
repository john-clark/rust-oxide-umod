using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Oxide.Plugins.AutoCrafterNamespace;
using Oxide.Plugins.AutoCrafterNamespace.Extensions;
using Rust;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Plugins.AutoCrafterNamespace.UI;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Globalization;
using ProtoBuf;
using Oxide.Plugins.AutoCrafterNamespace.JsonConverters;

namespace Oxide.Plugins
{
	[Info("AutoCrafter", "Skipcast", "1.0.4", ResourceId = 2582)]
	[Description("A machine that automatically crafts items so the player can do more interesting stuff instead.")]
	public class AutoCrafter : RustPlugin
	{
		private readonly List<ItemAmount> UpgradeCost = new List<ItemAmount>();

		/// <summary>
		/// Used for keeping track of when research tables were placed so we know if enough time has passed that upgrading is impossible.
		/// </summary>
		private readonly List<BaseEntity> upgradeableEntities = new List<BaseEntity>();

		/// <summary>
		/// List of players that have received the first join message.
		/// </summary>
		private List<ulong> introducedPlayers = new List<ulong>(); 

		private bool serverInitialized = false;

		#region Rust hooks

		private object OnItemCraft(ItemCraftTask task)
		{
			var player = task.owner;
			var crafter = CrafterManager.FindByPlayer(player);

			if (crafter != null && crafter.PlayerCanAccess(player))
			{
				crafter.AddCraftTask(task);
				return true;
			}

			return null;
		}

		private void OnLootEntity(BasePlayer player, BaseEntity entity)
		{
			var recycler = entity as Recycler;

			if (recycler == null)
				return;

			var crafter = CrafterManager.GetCrafter(recycler);

			if (crafter == null)
				return;

			// Open the output container instead of the recycler ui.
			NextFrame(() =>
			{
				if (!crafter.PlayerCanAccess(player))
				{
					crafter.PlayLockedSound();
					player.CloseInventory();
					return;
				}

				player.inventory.loot.Clear();
				player.inventory.loot.StartLootingEntity(crafter.Output);
				player.inventory.loot.AddContainer(crafter.OutputInventory);
				player.inventory.loot.SendImmediate();
				player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", crafter.Output.lootPanelName);

				if (crafter.IsLocked())
					crafter.PlayAccessSound();
			});
		}

		void OnEntityGroundMissing(BaseEntity entity)
		{
			var recycler = entity as Recycler;

			if (recycler == null)
				return;

			var crafter = CrafterManager.GetCrafter(recycler);

			if (crafter == null)
				return;

			// Empty recycler, otherwise the hidden items inside it will drop into the world.
			recycler.inventory.Clear();
			recycler.inventory.itemList.Clear();
		}

		void OnEntitySpawned(BaseNetworkable networkable)
		{
			if (!serverInitialized) // Check if server is initialized. This hook tends to call on startup before OnServerInitialized has been called.
				return;

			var entity = networkable as BaseEntity;

			if (entity == null)
				return;

			if (entity.OwnerID == 0)
				return;

			var researchTable = entity as ResearchTable;

			if (researchTable == null)
				return;
			
			upgradeableEntities.Add(researchTable);
			timer.Once(Constants.TimeToUpgrade, () => upgradeableEntities.Remove(researchTable));
		}

		void OnEntityKill(BaseNetworkable entity)
		{
			if (!serverInitialized) // Check if server is initialized. This hook tends to call on startup before OnServerInitialized has been called.
				return;

			var researchTable = entity as ResearchTable;

			if (researchTable != null)
			{
				upgradeableEntities.Remove(researchTable);
			}

			var recycler = entity as Recycler;

			if (recycler == null)
				return;

			var crafter = CrafterManager.GetCrafter(recycler);

			if (crafter == null)
				return;

			CrafterManager.DestroyCrafter(crafter, false, false);
		}

		void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
		{
			float newHealth = entity.Health() - info.damageTypes.Total();

			// Empty recycler inventory if it's about to be killed to avoid dropping hidden items.
			if (newHealth <= 0)
			{
				var recycler = entity as Recycler;

				if (!(entity is Recycler))
					return;

				var crafter = CrafterManager.GetCrafter(recycler);

				if (crafter == null)
					return;

				recycler.inventory.Clear();
				recycler.inventory.itemList.Clear();
			}
		}

		void OnPlayerInput(BasePlayer player, InputState input)
		{
			if (input.WasJustPressed(BUTTON.FIRE_PRIMARY))
			{
				var activeItem = player.GetActiveItem();

				if (activeItem?.info.itemid != -975723312) // Codelock
					return;

				var ray = player.eyes.HeadRay();
				RaycastHit hit;

				if (!Physics.Raycast(ray, out hit, 2.2f, 1 << (int) Layer.Deployed))
					return;

				var recycler = hit.transform.GetComponentInParent<Recycler>();

				if (recycler == null)
					return;

				if (player.IsBuildingBlocked(recycler.ServerPosition, recycler.ServerRotation, recycler.bounds))
					return;

				var crafter = CrafterManager.GetCrafter(recycler);

				if (crafter == null)
					return;

				if (crafter.AddCodeLock())
				{
					activeItem.UseItem();
					FxManager.PlayFx(crafter.CodeLock.ServerPosition, Constants.CodelockPlaceSoundPrefab);
				}
			}
		}

		// Show message if enabled
		void OnPlayerSpawn(BasePlayer player)
		{
			if (!serverInitialized) // Check if server is initialized. This hook tends to call on startup before OnServerInitialized has been called.
				return;

			timer.Once(1, () =>
			{
				ShowJoinMessage(player);
			});
		}

		// Make sure nothing is clipping into recycler. Pretty hacky method, but the recycler doesn't block things like other deployables.
		object CanBuild(Planner plan, Construction prefab, Vector3 position)
		{
			BasePlayer player = plan.GetOwnerPlayer();
			
			List<Recycler> recyclers = new List<Recycler>();
			Vis.Entities(position, prefab.bounds.size.magnitude / 3f, recyclers, 1 << (int) Layer.Deployed);
			
			if (recyclers.Count <= 0)
			{
				return null;
			}
			
			return true;
		}

		private object OnServerCommand(ConsoleSystem.Arg arg)
		{
			if (arg.Connection == null)
				return null;

			var player = (BasePlayer) arg.Connection.player;

			if (arg.cmd?.FullName == "craft.canceltask")
			{
				int taskid = arg.GetInt(0, -1);

				if (taskid == -1)
					return null;

				var crafters = CrafterManager.FindAllByPlayer(player);

				foreach (var crafter in crafters)
				{
					if (crafter.CancelByTaskId(player, taskid))
						return true;
				}

				return null;
			}

			return null;
		}

		private object OnHammerHit(BasePlayer player, HitInfo info)
		{
			var entity = info.HitEntity as BaseCombatEntity;
			var recycler = entity as Recycler;
			var researchTable = entity as ResearchTable;
			
			if (entity == null || (recycler == null && researchTable == null))
				return null;

			Func<string> hpMessage = () =>
			{
				return Lang.Translate(player, "hp-message", entity.Health(), entity.MaxHealth());
			};

			// Don't allow upgrading/downgrading/repairing if there's less than 8 seconds since the entity was attacked.
			if (entity.SecondsSinceAttacked < 8)
			{
				if (recycler != null && CrafterManager.ContainsRecycler(recycler))
				{
					// Show hp info if repairing is blocked.
					player.ShowScreenMessage(hpMessage(), 2);
				}
				return null;
			}

			if (!lastHammerHit.ContainsKey(player))
				lastHammerHit[player] = 0;

			((DecayEntity) entity).DecayTouch(); // Reset decay

			// Make sure entity is full health, otherwise repair.
			if (entity.Health() < entity.MaxHealth())
			{
				if (recycler == null)
					return null;

				if (!CrafterManager.ContainsRecycler(recycler))
					return null;

				if (Time.time - lastHammerHit[player] > Constants.HammerConfirmTime)
				{
					player.ShowScreenMessage(hpMessage() + "\n\n" + Lang.Translate(player, "hit-again-to-repair"), Constants.HammerConfirmTime);
					lastHammerHit[player] = Time.time;
					return true;
				}

				lastHammerHit[player] = Time.time;
				player.HideScreenMessage();
				entity.DoRepair(player);
				player.ShowScreenMessage(hpMessage(), 2);

				// Reset last hammer hit so that the player won't accidentally downgrade/upgrade with the next hammer hit.
				if (entity.Health() >= entity.MaxHealth())
				{
					lastHammerHit[player] = 0;
				}

				return true;
			}

			// Only allow upgrading/downgrading if we have building permission.
			if (player.IsBuildingBlocked(entity.ServerPosition, entity.ServerRotation, entity.bounds))
			{
				if (recycler != null && CrafterManager.ContainsRecycler(recycler)) // Only show hp info if this is a crafter
				{
					// Show hp info if building blocked.
					player.ShowScreenMessage(hpMessage(), 2);
				}

				return null;
			}

			// Check permission and if the entity owner is the current player.
			if (!permission.UserHasPermission(player.UserIDString, Constants.UsePermission) || entity.OwnerID != player.userID)
			{
				if (recycler != null && CrafterManager.ContainsRecycler(recycler))
					player.ShowScreenMessage(hpMessage(), 2);

				return null;
			}
			
			if (researchTable != null) // Upgrade to crafter (if less than 10 minutes since placement)
			{
				if (!upgradeableEntities.Contains(researchTable))
					return null;

				return HandleUpgradeRequest(player, researchTable);
			}

			var crafter = CrafterManager.GetCrafter(recycler);

			if (crafter == null)
				return null;

			if (DateTime.UtcNow - crafter.CreationTime > TimeSpan.FromSeconds(Constants.TimeToUpgrade))
			{
				player.ShowScreenMessage(hpMessage(), 2);
				return null;
			}

			// Check if player has authed on potential codelock.
			if (!crafter.PlayerCanAccess(player))
			{
				crafter.PlayLockedSound();
				return true;
			}

			return HandleDowngradeRequest(player, crafter);
		}

		protected override void LoadDefaultConfig()
		{
			Utility.Config = new PluginConfig();
			Utility.Config.UpgradeCost.AddRange(new List<PluginConfig.ItemAmount>
			{
				new PluginConfig.ItemAmount("metal.refined", 25),
				new PluginConfig.ItemAmount("metal.fragments", 500),
				new PluginConfig.ItemAmount("techparts", 3),
				new PluginConfig.ItemAmount("gears", 3)
			});
		}

		private void Loaded()
		{
			if (Utility.Config == null)
			{
				Utility.Config = Config.ReadObject<PluginConfig>();
				Config.WriteObject(Utility.Config); // Save any new or removed properties.
			}
			else
			{
				Config.WriteObject(Utility.Config);
			}
		}
		
		private void OnServerInitialized()
		{
			Utility.Timer = timer;

			Config.Settings.AddConverters();
			permission.RegisterPermission(Constants.UsePermission, this);
			lang.RegisterMessages(Lang.DefaultMessages, this, "en");

			UiManager.Initialize();
			Lang.Initialize(this, lang);
			FxManager.Initialize();

			foreach (var itemAmount in Utility.Config.UpgradeCost)
			{
				var itemDef = ItemManager.FindItemDefinition(itemAmount.Shortname);

				if (itemDef == null)
				{
					PrintError(Lang.Translate(null, "item-notfound-skipping-ingredient", itemAmount.Shortname));
					continue;
				}

				UpgradeCost.Add(new ItemAmount(itemDef, itemAmount.Amount));
			}
			
			CrafterManager.Initialize();
			CrafterManager.Load();

			if (Utility.Config.ShowPlayerInstructionsOnFirstJoin)
			{
				// Load previously introduced players
				introducedPlayers = Core.Interface.Oxide.DataFileSystem.ReadObject<List<ulong>>("AutoCrafter/IntroducedPlayers");

				foreach (var player in BasePlayer.activePlayerList)
				{
					ShowJoinMessage(player);
				}
			}

			serverInitialized = true;
		}

		private void OnServerSave()
		{
			CrafterManager.Save();

			if (Utility.Config.ShowPlayerInstructionsOnFirstJoin)
			{
				Core.Interface.Oxide.DataFileSystem.WriteObject("AutoCrafter/IntroducedPlayers", introducedPlayers);
			}
		}

		private void Unload()
		{
			if (!serverInitialized) // Check if server is initialized. This hook tends to call on startup before OnServerInitialized has been called.
				return;

			FxManager.Destroy();
			CrafterManager.Destroy();
			UiManager.Destroy();
		}

		private object OnRecycleItem(Recycler recycler, Item item)
		{
			if (CrafterManager.ContainsRecycler(recycler))
			{
				// Prevent recycling
				return true;
			}

			return null;
		}

		object OnRecyclerToggle(Recycler recycler, BasePlayer player)
		{
			var crafter = CrafterManager.GetCrafter(recycler);

			if (crafter == null)
				return null;

			if (!crafter.PlayerCanAccess(player))
			{
				crafter.PlayLockedSound();
				return true;
			}

			return null;
		}

		void OnUserPermissionGranted(string id, string perm)
		{
			if (perm == Constants.UsePermission)
			{
				ShowJoinMessage(BasePlayer.Find(id));
			}
		}

		void OnGroupPermissionGranted(string name, string perm)
		{
			if (perm == Constants.UsePermission)
			{
				foreach (var player in BasePlayer.activePlayerList)
				{
					ShowJoinMessage(player);
				}
			}
		}

		#endregion

		#region Chat commands

		[ChatCommand("autocrafter")]
		private void ChatCmd_Autocrafter(BasePlayer player, string command, string[] args)
		{
			if (!permission.UserHasPermission(player.UserIDString, Constants.UsePermission))
			{
				player.TranslatedChatMessage("nopermission");
				return;
			}

			string submenu = args.FirstOrDefault();
			StringBuilder message = new StringBuilder();
			string title = null;

			Action appendMenus = () =>
			{
				message.AppendLine("- craft : " + Lang.Translate(player, "chat-description-craft"));
				message.Append("- more : " + Lang.Translate(player, "chat-description-more"));
			};

			switch (submenu)
			{
				default:
				{
					message.Append(Lang.Translate(player, "chat-unknown-selection") + "\n");
					appendMenus();
					break;
				}
				case null:
				{
					message.AppendLine(Lang.Translate(player, "chat-default-text"));
					appendMenus();
					break;
				}
				case "craft":
				{
					title = Lang.Translate(player, "chat-title-craft");
					message.AppendLine(Lang.Translate(player, "chat-craft-text-top"));

					foreach (var itemAmount in UpgradeCost)
					{
						message.AppendLine("- " + itemAmount.amount + "x " + itemAmount.itemDef.displayName.english);
					}

					message.AppendLine();
					message.AppendLine(Lang.Translate(player, "chat-craft-text-bottom"));
					
					break;
				}
				case "usage":
				{
					title = Lang.Translate(player, "chat-title-usage");
					message.AppendLine(Lang.Translate(player, "chat-usage-text"));

					if (Utility.Config.ScanForWorldItems)
					{
						message.AppendLine(Lang.Translate(player, "chat-usage-text-droptop"));
					}
					break;
				}
				case "more":
				{
					title = Lang.Translate(player, "chat-title-more");
					message.AppendLine(Lang.Translate(player, "chat-more-text"));
					break;
				}
			}

			message.Insert(0, "<size=20>" + Lang.Translate(player, "chat-title") + (title != null ? (" - " + title) : "") + "</size>\n");

			player.ChatMessage(message.ToString());
		}

		#endregion

		// For keeping track of how long ago they requested with the previous hammer hit. Used for confirming by hitting twice with hammer to upgrade, downgrade, or repair.
		private readonly Dictionary<BasePlayer, float> lastHammerHit = new Dictionary<BasePlayer, float>();

		// Return value:
		// - null = continue with default behaviour of hammer hit
		// - anything else: prevent default behaviour.
		private object HandleUpgradeRequest(BasePlayer player, ResearchTable researchTable)
		{
			if (UpgradeCost.Count > 0)
			{
				if (!player.CanCraft(UpgradeCost))
				{
					StringBuilder builder = new StringBuilder();

					foreach (var ingredient in UpgradeCost)
					{
						builder.AppendLine("- x" + ingredient.amount.ToString("0") + " " + ingredient.itemDef.displayName.english);
					}

					string ingredientsStr = builder.ToString();

					player.ShowScreenMessage(Lang.Translate(player, "ingredients-missing-youneed") + "\n" + ingredientsStr, 10, TextAnchor.MiddleLeft);
					return true;
				}
			}

			float lastHit = lastHammerHit[player];
			
			if (Time.time - lastHit > Constants.HammerConfirmTime) // Confirm the upgrade
			{
				lastHammerHit[player] = Time.time;
				player.ShowScreenMessage(Lang.Translate(player, "hammer-confirm-upgrade"), Constants.HammerConfirmTime);
				return true;
			}
			
			lastHammerHit[player] = 0; // Reset time

			foreach (var ingredient in UpgradeCost)
			{
				List<Item> takenItems = new List<Item>();
				player.inventory.Take(takenItems, ingredient.itemid, (int)ingredient.amount);
			}

			CrafterManager.CreateCrafter(researchTable);
			FxManager.PlayFx(researchTable.ServerPosition, Constants.UpgradeTopTierFxPrefab);
			player.HideScreenMessage();
			return true;
		}

		// Return value:
		// - null = continue with default behaviour of hammer hit
		// - anything else: prevent default behaviour.
		private object HandleDowngradeRequest(BasePlayer player, Crafter crafter)
		{
			float lastRequest = lastHammerHit[player];

			if (Time.time - lastRequest > Constants.HammerConfirmTime) // Confirm the downgrade
			{
				string message = Lang.Translate(player, "hp-message", crafter.Recycler.Health(), crafter.Recycler.MaxHealth());
				message += "\n\n" + Lang.Translate(player, "hammer-confirm-downgrade");

				lastHammerHit[player] = Time.time;
				player.ShowScreenMessage(message, Constants.HammerConfirmTime);
				return true;
			}
			
			lastHammerHit[player] = 0; // Reset time
			
			CrafterManager.DestroyCrafter(crafter, true, false);
			FxManager.PlayFx(crafter.Position, Constants.UpgradeMetalFxPrefab);
			player.HideScreenMessage();

			foreach (var itemAmount in UpgradeCost)
			{
				player.GiveItem(ItemManager.CreateByItemID(itemAmount.itemid, (int) itemAmount.amount));
			}

			// Refund codelock if one is attached
			if (crafter.CodeLock != null)
			{
				var item = ItemManager.Create(ItemManager.FindItemDefinition("lock.code"));
				player.GiveItem(item);
			}

			return true;
		}

		private void ShowJoinMessage(BasePlayer player)
		{
			if (!Utility.Config.ShowPlayerInstructionsOnFirstJoin || !permission.UserHasPermission(player.UserIDString, Constants.UsePermission) || introducedPlayers.Contains(player.userID))
				return;

			string message = Lang.Translate(player, "join-message");

			if (Utility.Config.ShowInstructionsAsGameTip)
				player.ShowGameTip(message, 10f);
			else
				player.ChatMessage(message);

			introducedPlayers.Add(player.userID);
		}
	}
}
namespace Oxide.Plugins.AutoCrafterNamespace
{
	public static class Constants
	{
		public const string ItemDropPrefab = "assets/prefabs/misc/item drop/item_drop.prefab";
		public const string StaticRecyclerPrefab = "assets/bundled/prefabs/static/recycler_static.prefab";
		public const string DeployedResearchTablePrefab = "assets/prefabs/deployable/research table/researchtable_deployed.prefab";
		public const string StackSoundFxPrefab = "assets/bundled/prefabs/fx/notice/stack.world.fx.prefab";
		public const string UpgradeTopTierFxPrefab = "assets/bundled/prefabs/fx/build/promote_toptier.prefab";
		public const string UpgradeMetalFxPrefab = "assets/bundled/prefabs/fx/build/promote_metal.prefab";
		public const string CodelockPrefab = "assets/prefabs/locks/keypad/lock.code.prefab";
		public const string CodelockPlaceSoundPrefab = "assets/prefabs/locks/keypad/effects/lock-code-deploy.prefab";

		public const int RecyclerNumInputSlots = 6;
		public const float CrafterNearRadius = 0.6f;
		public const float HammerConfirmTime = 2f;
		public const float TimeToUpgrade = 600f;

		public const string UsePermission = "autocrafter.use";
	}
}

namespace Oxide.Plugins.AutoCrafterNamespace
{
	public class Crafter
	{
		public delegate void PlayerEnterDelegate(Crafter crafter, BasePlayer player);
		public delegate void PlayerLeaveDelegate(Crafter crafter, BasePlayer player);

		public class CraftTask
		{
			[JsonIgnore] public ItemBlueprint Blueprint;
			public int Amount;
			public int SkinID;

			[JsonProperty("ItemID")]
			private int _itemid => Blueprint.targetItem.itemid;

			[JsonIgnore]
			public List<Item> TakenItems { get; set; } = new List<Item>();

			[JsonProperty("TakenItems")]
			private object _takenItems => TakenItems.Select(item => new
			{
				item.info.itemid,
				item.skin,
				item.amount
			}).ToList();

			/// <summary>
			/// Number of seconds this has been crafting for.
			/// </summary>
			public float Elapsed;

			public CraftTask(ItemBlueprint blueprint, int amount, int skinId)
			{
				Blueprint = blueprint;
				Amount = amount;
				SkinID = skinId;
			}
		}

		[JsonIgnore]
		public Recycler Recycler { get; private set; }

		[JsonIgnore]
		public Vector3 Position => Recycler.ServerPosition;

		/// <summary>
		/// Gets a list of players that are near the crafter, and should receive craft queue updates and be able to add/delete from queue
		/// </summary>
		[JsonIgnore]
		public List<BasePlayer> NearbyPlayers { get; private set; } = new List<BasePlayer>();

		public List<CraftTask> CraftingTasks { get; private set; } = new List<CraftTask>();

		[JsonIgnore]
		public ItemContainer OutputInventory => outputInventory;

		[JsonIgnore]
		public DroppedItemContainer Output => outputContainer;

		/// <summary>
		/// Gets the codelock on this crafter. May be null.
		/// </summary>
		[JsonIgnore]
		public CodeLock CodeLock { get; private set; }

		#region Json exclusive properties for saving/loading

		[JsonProperty("Code")]
		private string _code => CodeLock?.code;

		[JsonProperty("GuestCode")]
		private string _guestCode => CodeLock?.guestCode;

		[JsonProperty("AuthedPlayers")]
		private List<ulong> _authedPlayers => CodeLock?.whitelistPlayers;

		[JsonProperty("GuestPlayers")]
		private List<ulong> _guestPlayers => CodeLock?.guestPlayers;

		[JsonProperty("HasCodeLock")]
		private bool _hasCodelock => CodeLock != null;

		[JsonProperty("IsLocked")]
		private bool _locked => CodeLock?.IsLocked() ?? false;

		[JsonProperty("OutputItems")]
		private object _outputItems => OutputInventory.itemList.Select(item =>
		{
			if (item.info.itemid == 98228420) // Hidden item
				return null;

			return new
			{
				item.position,
				item.info.itemid,
				item.amount,
				item.skin
			};
		}).Where(obj => obj != null).ToList();

		[JsonProperty("On")]
		private bool _turnedOn => Recycler.IsOn();

		[JsonProperty("Health")]
		private float _health => Recycler.Health();

		[JsonProperty("DecayTimer")]
		private float _decayTimer => (float) (typeof (DecayEntity).GetField("decayTimer", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(Recycler) ?? 0f);

		#endregion

		public event PlayerEnterDelegate PlayerEnter;
		public event PlayerLeaveDelegate PlayerLeave;

		/// <summary>
		/// Gets or sets the time this was created in UTC.
		/// </summary>
		public DateTime CreationTime { get; set; }

		// Lookup table for players on each crafting task.
		private readonly Dictionary<BasePlayer, Dictionary<CraftTask, int>> taskLookup = new Dictionary<BasePlayer, Dictionary<CraftTask, int>>();
		
		private DroppedItemContainer outputContainer;
		private ItemContainer outputInventory;
		private readonly Timer resetDespawnTimer;
		private float nextPickup = Time.time;
		private const float pickupDelay = 0.5f;
		private float nextUiUpdate = Time.time;
		private const float uiUpdateDelay = 0.5f;

		/// <param name="recycler">The recycler entity we're "overwriting".</param>
		public Crafter(Recycler recycler)
		{
			CreationTime = DateTime.UtcNow;

			Recycler = recycler;

			CreateOutputContainer();

			// Reset despawn timer on loot bag once per minute.
			resetDespawnTimer = Utility.Timer.Every(60, () =>
			{
				if (!outputContainer.IsDestroyed)
					outputContainer.ResetRemovalTime();
			});

			recycler.gameObject.AddComponent<GroundWatch>();
			recycler.gameObject.AddComponent<DestroyOnGroundMissing>();

			recycler.repair.enabled = true;
			recycler.repair.itemTarget = ItemManager.FindItemDefinition("wall.frame.shopfront.metal");
			
			// Set health to 1000
			Recycler._maxHealth = 1000;
			Recycler.health = recycler.MaxHealth();

			// Set up damage protection
			Recycler.baseProtection.density = 4;
			
			for (int i = 0; i < Recycler.baseProtection.amounts.Length; ++i)
			{
				Recycler.baseProtection.amounts[i] = Utility.Config.CrafterProtectionProperties[i];
			}

			// Set up decay
			var researchPrefab = GameManager.server.FindPrefab(Constants.DeployedResearchTablePrefab); // Copying decay settings from research table

			if (researchPrefab == null)
			{
				Utility.LogWarning("Could not find research table prefab, skipping decay setup");
			}
			else
			{
				uint prefabID = researchPrefab.GetComponent<BaseEntity>().prefabID;
				var decay = PrefabAttribute.server.Find<Decay>(prefabID);
				var decayPoints = PrefabAttribute.server.FindAll<DecayPoint>(prefabID);

				typeof (DecayEntity).GetField("decay", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(Recycler, decay);
				typeof (DecayEntity).GetField("decayPoints", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(Recycler, decayPoints);
				BuildingManager.DecayEntities.Add(Recycler);
			}
		}

		private void CreateOutputContainer()
		{
			Vector3 position = Position + (Recycler.transform.forward * 0f) + (Recycler.transform.up * 0.72f) + (Recycler.transform.right * -0.25f);
			Quaternion rotation = Recycler.ServerRotation * Quaternion.Euler(90, 0, 0);
			outputContainer = CreateItemContainer(position, rotation, Lang.Translate(null, "crafted-items"), out outputInventory);

			var rigidBody = outputContainer.GetComponent<Rigidbody>();
			rigidBody.isKinematic = true; // Prevent physics from moving the container.

			// Add a hidden inventory slot in output container to prevent it from despawning when closing empty loot.
			outputInventory.capacity = 37;
			var item = ItemManager.Create(ItemManager.FindItemDefinition("gears"), 1);
			item.MoveToContainer(outputInventory, outputInventory.capacity - 1);
		}

		public void Tick(float elapsed)
		{
			if (outputContainer.IsDestroyed)
			{
				CrafterManager.DestroyCrafter(this, false, destroyOutputContainer: false); // Don't destroy output container because it's already destroyed.
			}

			ProcessWorldItems();
			ProcessNearbyPlayers();
			ProcessQueue(elapsed);
			ProcessUiUpdates();
		}
		
		/// <param name="unloading">Specify true if the plugin is unloading.</param>
		public void Destroy(bool destroyOutputContainer, bool unloading = false)
		{
			resetDespawnTimer.DestroyToPool();

			foreach (var player in NearbyPlayers)
			{
				OnPlayerLeave(player);
			}

			if (!unloading)
			{
				// Drop queue items
				if (CraftingTasks.Count > 0)
				{
					var container = new ItemContainer();
					container.ServerInitialize(null, 36);

					foreach (var task in CraftingTasks)
					{
						foreach (var ingredient in task.Blueprint.ingredients)
						{
							var item = ItemManager.CreateByItemID(ingredient.itemid, (int) ingredient.amount * task.Amount);

							if (!item.MoveToContainer(container))
								item.Drop(Position + Recycler.transform.up * 1.25f, Recycler.GetDropVelocity(), Recycler.ServerRotation);
						}
					}

					var droppedContainer = container.Drop(Constants.ItemDropPrefab, Position + Recycler.transform.up * 1.25f, Recycler.ServerRotation);
					droppedContainer.playerName = Lang.Translate(null, "queue-items");
				}
			}

			Recycler.Kill();
			CodeLock?.Kill();

			if (!outputContainer.IsDestroyed)
			{
				// Remove rock from output container that keeps it from despawning when emptied
				outputInventory.GetSlot(outputInventory.capacity - 1).Remove();

				// Force kill output bag if there's nothing in it.
				if (!destroyOutputContainer && OutputInventory.AnyItems())
				{
					// Enable physics on output container
					outputContainer.GetComponent<Rigidbody>().isKinematic = false;
				}
				else
				{
					outputContainer.Kill();
				}
			}
		}

		private void ProcessQueue(float elapsed)
		{
			if (!Recycler.IsOn() || CraftingTasks.Count <= 0)
				return;

			var currentTask = CraftingTasks.FirstOrDefault();

			if (currentTask != null)
			{
				currentTask.Elapsed += elapsed;

				if (currentTask.Elapsed >= currentTask.Blueprint.time)
				{
					ulong workshopSkinId = Rust.Global.SteamServer.Inventory.FindDefinition(currentTask.SkinID)?.GetProperty<ulong>("workshopdownload") ?? 0;

					if (workshopSkinId == 0)
						workshopSkinId = (ulong)currentTask.SkinID;

					var item = ItemManager.CreateByItemID(currentTask.Blueprint.targetItem.itemid, currentTask.Blueprint.amountToCreate, workshopSkinId);
					
					if (!GiveItem(item))
					{
						item.Drop(Recycler.GetDropPosition(), Recycler.GetDropVelocity());
						Recycler.StopRecycling();
					}

					currentTask.Amount -= 1;
					currentTask.Elapsed -= currentTask.Blueprint.time;

					// Take used items
					foreach (var ingredient in currentTask.Blueprint.ingredients)
					{
						foreach (var taskItem in currentTask.TakenItems)
						{
							if (taskItem.info.itemid != ingredient.itemid)
								continue;

							taskItem.amount -= (int) ingredient.amount;

							if (taskItem.amount <= 0)
							{
								taskItem.Remove();
								currentTask.TakenItems.Remove(taskItem);
							}

							break;
						}
					}

					if (currentTask.Amount <= 0)
					{
						// Remove from ui
						foreach (var player in NearbyPlayers)
						{
							SendRemoveCraftingTask(player, currentTask);
						}

						CraftingTasks.RemoveAt(0);

						// Stop recycler if there's nothing more to craft.
						if (CraftingTasks.Count <= 0)
						{
							Recycler.StopRecycling();
						}
					}
					else
					{
						foreach (var player in NearbyPlayers)
						{
							SendCraftingTaskProgress(player, currentTask);
						}
					}
				}
			}
		}

		private void ProcessWorldItems()
		{
			if (Utility.Config.ScanForWorldItems && Recycler.IsOn())
			{
				List<BaseEntity> entities = new List<BaseEntity>();

				Vector3 position = Position + (Recycler.transform.up * 1.5f) + (Recycler.transform.forward * 0.1f) + (Recycler.transform.right * -0.25f);
				float radius = 0.3f;

				Vis.Entities(position, radius, entities);
				entities = entities.Where(ent => ent.GetComponent<WorldItem>() != null).ToList();

				if (nextPickup <= Time.time)
				{
					foreach (var entity in entities)
					{
						if (nextPickup > Time.time)
							break;

						var worldItem = (WorldItem) entity;
						
						bool partiallyInserted = false;

						for (int i = 0; i < outputInventory.capacity - 1; ++i)
						{
							var slot = outputInventory.GetSlot(i);
							if (slot == null)
							{
								worldItem.item.MoveToContainer(outputInventory, i);
								partiallyInserted = true;
								break;
							}

							if (slot.info == worldItem.item.info && slot.skin == worldItem.item.skin && slot.amount < slot.info.stackable)
							{
								int available = slot.info.stackable - slot.amount;
								int toMove = Math.Min(available, worldItem.item.amount);
								worldItem.item.amount -= toMove;
								slot.amount += toMove;

								slot.MarkDirty();

								partiallyInserted = true;

								if (worldItem.item.amount <= 0)
								{
									worldItem.item.Remove();
									worldItem.Kill();
									break;
								}
							}
						}

						if (partiallyInserted)
						{
							FxManager.PlayFx(worldItem.ServerPosition, Constants.StackSoundFxPrefab);
						}
					}
				}
			}
		}

		private void ProcessNearbyPlayers()
		{
			List<BasePlayer> nearPlayers = new List<BasePlayer>();

			Vector3 checkPosition = Position + Recycler.transform.up * 0.75f + Recycler.transform.forward * 1f + Recycler.transform.right * -0.2f;
			float checkRadius = Constants.CrafterNearRadius;
			Vis.Entities(checkPosition, checkRadius, nearPlayers);

			var previousNearbyPlayers = NearbyPlayers.ToList(); // Nearby players last tick
			
			// Keep all players that are the following:
			// - Alive and not sleeping
			// - Has codelock access
			// - Can see the recycler from their position, aka not behind a wall or anything
			nearPlayers = nearPlayers.Where(plr => plr.IsAlive() && !plr.IsSleeping() && PlayerCanAccess(plr) && Recycler.IsVisible(plr.ServerPosition)).ToList();

			var playersLeaving = previousNearbyPlayers.Where(plr => !nearPlayers.Contains(plr)).ToList();
			var playersEntering = nearPlayers.Where(plr => !previousNearbyPlayers.Contains(plr)).ToList();

			foreach (var player in playersLeaving)
			{
				NearbyPlayers.Remove(player);
				OnPlayerLeave(player);
			}

			foreach (var player in playersEntering)
			{
				NearbyPlayers.Add(player);
				OnPlayerEnter(player);
			}
			
			/*foreach (var player in BasePlayer.activePlayerList)
			{
				player.SendConsoleCommand("ddraw.sphere", 0.5f, Color.red, checkPosition, checkRadius);
			}*/
		}

		private void ProcessUiUpdates()
		{
			if (!(Time.time > nextUiUpdate))
				return;

			nextUiUpdate = Time.time + uiUpdateDelay;

			foreach (var player in NearbyPlayers)
			{
				SendCraftingListUpdate(player);
			}
		}

		/// <summary>
		/// Called when a player comes into range of this crafter.
		/// </summary>
		private void OnPlayerEnter(BasePlayer player)
		{
			if (CraftingTasks.Count > 0)
			{
				SendCraftingList(player);
			}

			PlayerEnter?.Invoke(this, player);
		}

		/// <summary>
		/// Called when a player goes out of range of this crafter.
		/// </summary>
		private void OnPlayerLeave(BasePlayer player)
		{
			SendClearCraftingList(player);
			PlayerLeave?.Invoke(this, player);
		}

		private void SendCraftingList(BasePlayer player)
		{
			foreach (var task in CraftingTasks)
			{
				SendAddCraftingTask(player, task);
			}
		}

		private void SendCraftingListUpdate(BasePlayer player)
		{
			foreach (var task in CraftingTasks)
			{
				SendUpdateCraftingTask(player, task);
			}
		}

		private void SendAddCraftingTask(BasePlayer player, CraftTask task)
		{
			var crafting = player.inventory.crafting;
			crafting.taskUID++;

			// The reason for always sending 2 as amount is because if a craft task is started with 1 item, the amount counter won't show in clientside, even if amount is incremented later.
			// The real amount will be sent straight after, but then it will show with the counter, even if there's only 1.
			player.Command("note.craft_add", crafting.taskUID, task.Blueprint.targetItem.itemid, 2, task.SkinID);

			// Correct the craft amount.
			player.Command("note.craft_done", crafting.taskUID, 0, task.Amount);

			var dict = GetTaskLookupDict(player);
			dict.Add(task, crafting.taskUID);
		}

		private void SendUpdateCraftingTask(BasePlayer player, CraftTask task)
		{
			var lookup = GetTaskLookupDict(player);
			int taskUID = lookup[task];
			float time = task.Blueprint.time - task.Elapsed;

			if (!Recycler.IsOn())
				time = (float) Math.Ceiling(time) - 0.01f;

			player.Command("note.craft_start", taskUID, time, task.Amount);
		}

		private void SendCraftingTaskProgress(BasePlayer player, CraftTask task)
		{
			var lookup = GetTaskLookupDict(player);
			var taskUID = lookup[task];
			player.Command("note.craft_done", taskUID, 0, task.Amount);
		}

		private void SendClearCraftingList(BasePlayer player)
		{
			var lookup = GetTaskLookupDict(player);
			foreach (var kv in lookup.ToDictionary(kv => kv.Key, kv => kv.Value))
			{
				SendRemoveCraftingTask(player, kv.Key);
			}
		}

		private void SendRemoveCraftingTask(BasePlayer player, CraftTask task)
		{
			var lookup = GetTaskLookupDict(player);
			int taskUID = lookup[task];
			player.Command("note.craft_done", taskUID, 0);
			lookup.Remove(task);
		}

		private Dictionary<CraftTask, int> GetTaskLookupDict(BasePlayer player)
		{
			if (taskLookup.ContainsKey(player))
				return taskLookup[player];

			var dictionary = new Dictionary<CraftTask, int>();
			taskLookup.Add(player, dictionary);
			return dictionary;
		}

		#region Public api methods

		public CraftTask AddCraftTask(ItemBlueprint blueprint, int amount, int skinId = 0, bool startRecycler = true, List<Item> takenItems = null)
		{
			bool wasEmpty = CraftingTasks.Count == 0;

			// Merge with current craft queue if the item is in queue with matching skin.
			var craftTask = CraftingTasks.FirstOrDefault(task => task.Blueprint.targetItem.itemid == blueprint.targetItem.itemid && task.SkinID == skinId);

			if (craftTask != null)
			{
				craftTask.Amount += amount;

				// Send new amount to all players
				foreach (var player in NearbyPlayers)
				{
					SendCraftingTaskProgress(player, craftTask);
				}

				return craftTask;
			}
			else
			{
				craftTask = new CraftTask(blueprint, amount, skinId);
				CraftingTasks.Add(craftTask);
			}

			if (takenItems != null)
				craftTask.TakenItems.AddRange(takenItems);

			foreach (var player in NearbyPlayers)
			{
				SendAddCraftingTask(player, craftTask);
			}

			// Turn on recycler if the queue was empty before.
			if (startRecycler && !Recycler.IsOn() && wasEmpty)
			{
				Recycler.StartRecycling();
			}

			return craftTask;
		}

		public void AddCraftTask(ItemCraftTask task)
		{
			AddCraftTask(task.blueprint, task.amount, task.skinID, true, task.takenItems);
		}

		/// <summary>
		/// Puts the given item in the output container.
		/// </summary>
		public bool GiveItem(Item item)
		{
			return item.MoveToContainer(outputInventory);
		}

		/// <summary>
		/// Cancels the given craft task. Returns true if the task was found and cancelled.
		/// </summary>
		/// <param name="refundTo">The refunded items will be added to this players inventory.</param>
		public bool CancelTask(CraftTask task, BasePlayer refundTo)
		{
			CraftingTasks.Remove(task);

			foreach (var player in NearbyPlayers)
			{
				SendRemoveCraftingTask(player, task);
			}
			
			foreach (var item in task.TakenItems)
			{
				if (!item.MoveToContainer(refundTo.inventory.containerMain) && !item.MoveToContainer(refundTo.inventory.containerBelt))
				{
					item.Drop(refundTo.GetDropPosition(), refundTo.GetDropVelocity(), Quaternion.identity);
				}
			}
			
			// Stop recycler if crafting queue is empty.
			if (CraftingTasks.Count <= 0)
			{
				Recycler.StopRecycling();
			}

			return true;
		}

		/// <summary>
		/// Cancels the craft task that is associated with the given taskid.
		/// </summary>
		/// <param name="player">The player that the taskid belongs to.</param>
		/// <param name="taskid">The craft taskid.</param>
		public bool CancelByTaskId(BasePlayer player, int taskid)
		{
			if (!PlayerCanAccess(player))
				return false;

			var lookup = GetTaskLookupDict(player);
			var task = lookup.FirstOrDefault(kv => kv.Value == taskid);

			if (task.Key == null)
			{
				return false;
			}

			return CancelTask(task.Key, player);
		}

		/// <summary>
		/// Replaces the recycler with a research table and then destroys the crafter. Default behaviour will drop the output loot onto the ground.
		/// </summary>
		public void Downgrade(bool destroyOutputContainer = false, bool unloading = false)
		{
			var researchTableEntity = GameManager.server.CreateEntity(Constants.DeployedResearchTablePrefab, Recycler.ServerPosition, Recycler.ServerRotation);
			var researchTable = researchTableEntity.GetComponent<ResearchTable>();
			researchTable.OwnerID = Recycler.OwnerID; // Copy ownership to research table.
			researchTable.Spawn();

			Destroy(destroyOutputContainer, unloading);
		}

		/// <summary>
		/// Adds a codelock to this crafter.
		/// </summary>
		public bool AddCodeLock()
		{
			if (CodeLock != null)
				return false;

			var instance = (CodeLock) GameManager.server.CreateEntity(Constants.CodelockPrefab, Position + (Recycler.transform.forward * 0.41f) + (Recycler.transform.up * 0.747f) + (Recycler.transform.right * 0.273f), Recycler.ServerRotation * Quaternion.Euler(0, -90, 0));
			instance.enableSaving = false;
			instance.Spawn();
			CodeLock = instance;
			
			return true;
		}

		/// <summary>
		/// Returns true if the player has authed on codelock if there is one and it's locked.
		/// </summary>
		public bool PlayerCanAccess(BasePlayer player)
		{
			if (!IsLocked())
				return true;
			
			return CodeLock.whitelistPlayers.Contains(player.userID) || CodeLock.guestPlayers.Contains(player.userID);
		}

		public void PlayLockedSound()
		{
			FxManager.PlayFx(CodeLock?.ServerPosition ?? Position, "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab");
		}

		public void PlayAccessSound()
		{
			FxManager.PlayFx(CodeLock?.ServerPosition ?? Position, "assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab");
		}

		public bool IsLocked()
		{
			return CodeLock != null && CodeLock.IsLocked();
		}

		#endregion

		private DroppedItemContainer CreateItemContainer(Vector3 position, Quaternion rotation, string name, out ItemContainer inventory)
		{
			var container = (DroppedItemContainer)GameManager.server.CreateEntity(Constants.ItemDropPrefab, position, rotation);
			
			container.playerName = name;
			container.enableSaving = false;
			container.Spawn();

			container.TakeFrom(new ItemContainer());

			inventory = container.inventory;
			return container;
		}
	}
}

namespace Oxide.Plugins.AutoCrafterNamespace
{
	public static class CrafterManager
	{
		public static Dictionary<Vector3, Crafter> Crafters { get; private set; }
		private static Dictionary<Recycler, Crafter> crafterLookup;
		
		private static float lastTick;
		private static Timer tickTimer;

		private static ActiveCrafterUI activeCrafterUi;

		/// <summary>
		/// Keeps track of how many crafters a player is in range of.
		/// </summary>
		private static Dictionary<BasePlayer, int> numActiveCrafters;

		#region Initialization, destruction and save/loading

		public static void Initialize()
		{
			Crafters = new Dictionary<Vector3, Crafter>();
			crafterLookup = new Dictionary<Recycler, Crafter>();
			numActiveCrafters = new Dictionary<BasePlayer, int>();

			lastTick = Time.time;
			tickTimer = Utility.Timer.Every(0.2f, Tick); // Tick every 200ms

			activeCrafterUi = UiManager.CreateUI<ActiveCrafterUI>();
		}

		public static void Destroy()
		{
			tickTimer.DestroyToPool();

			foreach (var crafter in Crafters.Values)
			{
				crafter.Downgrade(true, true);
			}
			
			Crafters.Clear();
			crafterLookup.Clear();
			UiManager.DestroyUI(activeCrafterUi);
		}

		public static void Save()
		{
			var dataFile = Core.Interface.Oxide.DataFileSystem.GetFile("AutoCrafter/Crafters");
			dataFile.Settings.AddConverters();
			
			dataFile.WriteObject(Crafters.ToDictionary(kv => kv.Key.ToXYZString(), kv => kv.Value));
		}

		public static void Load()
		{
			var jCrafters = Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, JObject>>("AutoCrafter/Crafters");
			var loadedCount = 0;

			foreach (var kv in jCrafters)
			{
				var jCrafter = kv.Value;
				string strPosition = kv.Key;
				Vector3 position = Utility.ParseXYZ(strPosition);

				List<BaseEntity> entities = new List<BaseEntity>();
				Vis.Entities(position, 0.1f, entities); // Find all entities within 0.1 game units of the saved position.

				// Compare entity positions and take the first research table or recycler that is within 0.001 units of the saved position.
				float maxDistanceSqr = 0.001f * 0.001f;
				var baseEntity = entities.FirstOrDefault(ent => (ent is ResearchTable || ent is Recycler) && (position - ent.ServerPosition).sqrMagnitude <= maxDistanceSqr);

				if (baseEntity == null)
				{
					Utility.LogWarning("Unable to load crafter; research table or recycler at saved position was not found. (" + position.ToString("0.########") + ")");
					continue;
				}

				var crafter = baseEntity is Recycler ? CreateCrafter((Recycler) baseEntity) : CreateCrafter((ResearchTable) baseEntity);
				crafter.CreationTime = jCrafter["CreationTime"].ToObject<DateTime>();

				// Load codelock
				bool hasCodeLock = jCrafter["HasCodeLock"].ToObject<bool>();

				if (hasCodeLock)
				{
					crafter.AddCodeLock();
					var codeLock = crafter.CodeLock;

					string code = jCrafter["Code"].ToObject<string>();
					string guestCode = jCrafter["GuestCode"].ToObject<string>();
					ulong[] authedPlayers = jCrafter["AuthedPlayers"].ToObject<ulong[]>();
					ulong[] guestPlayers = jCrafter["GuestPlayers"].ToObject<ulong[]>();
					bool isLocked = jCrafter["IsLocked"].ToObject<bool>();

					codeLock.code = code;
					codeLock.guestCode = guestCode;
					codeLock.whitelistPlayers.AddRange(authedPlayers);
					codeLock.guestPlayers.AddRange(guestPlayers);

					if (isLocked)
						codeLock.SetFlag(BaseEntity.Flags.Locked, true);
				}

				// Restore crafting queue
				foreach (var jTask in jCrafter["CraftingTasks"].Value<JArray>())
				{
					var blueprint = ItemManager.FindBlueprint(ItemManager.FindItemDefinition(jTask["ItemID"].ToObject<int>()));
					int amount = jTask["Amount"].ToObject<int>();
					int skin = jTask["SkinID"].ToObject<int>();
					
					var task = crafter.AddCraftTask(blueprint, amount, skin, false);
					task.Elapsed = jTask["Elapsed"].ToObject<float>();

					// Restore taken items
					var jTakenItems = jTask["TakenItems"].Value<JArray>();

					foreach (var jItem in jTakenItems)
					{
						int itemID = jItem["itemid"].ToObject<int>();
						int amount2 = jItem["amount"].ToObject<int>();
						ulong skin2 = jItem["skin"].ToObject<ulong>();

						var item = ItemManager.CreateByItemID(itemID, amount2, skin2);
						task.TakenItems.Add(item);
					}
				}

				// Restore output container
				foreach (var jItem in jCrafter["OutputItems"].Value<JArray>())
				{
					int itemId = jItem["itemid"].ToObject<int>();
					int amount = jItem["amount"].ToObject<int>();
					ulong skinId = jItem["skin"].ToObject<ulong>();
					int index = jItem["position"].ToObject<int>();

					var item = ItemManager.CreateByItemID(itemId, amount, skinId);
					item.MoveToContainer(crafter.OutputInventory, index);
				}

				// Restore on/off state
				if (jCrafter["On"].ToObject<bool>())
					crafter.Recycler.StartRecycling();

				// Restore hp and decay
				crafter.Recycler.health = Mathf.Clamp(jCrafter["Health"].ToObject<float>(), 0, crafter.Recycler.MaxHealth());
				typeof (DecayEntity).GetField("decayTimer", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(crafter.Recycler, jCrafter["DecayTimer"].ToObject<float>());

				++loadedCount;
			}

			Utility.Log("Loaded " + loadedCount + " crafter(s).");
		}
		
		#endregion

		#region Public api methods

		/// <summary>
		/// Creates a crafter from the given research table.
		/// </summary>
		/// <param name="researchTable">The research table to replace.</param>
		/// <returns></returns>
		public static Crafter CreateCrafter(ResearchTable researchTable)
		{
			var recyclerEntity = GameManager.server.CreateEntity(Constants.StaticRecyclerPrefab, researchTable.ServerPosition, researchTable.ServerRotation);
			var recycler = recyclerEntity.GetComponent<Recycler>();
			recyclerEntity.OwnerID = researchTable.OwnerID; // Copy ownership to recycler.
			recyclerEntity.Spawn();

			// Drop all items in research table onto the ground
			if (researchTable.inventory.AnyItems())
				researchTable.inventory.Drop(Constants.ItemDropPrefab, researchTable.ServerPosition + new Vector3(0, 1.5f, 0), researchTable.ServerRotation);

			// Remove original research table.
			researchTable.Kill();
			
			var crafter = CreateCrafter(recycler);
			return crafter;
		}
		
		/// <summary>
		/// Creates a crafter from the given recycler.
		/// </summary>
		/// <param name="recycler"></param>
		/// <returns></returns>
		public static Crafter CreateCrafter(Recycler recycler)
		{
			var crafter = new Crafter(recycler);
			crafter.PlayerEnter += OnPlayerEnterCrafter;
			crafter.PlayerLeave += OnPlayerLeaveCrafter;

			var gears = ItemManager.Create(ItemManager.FindItemDefinition("gears"), Constants.RecyclerNumInputSlots);

			for (int i = 0; i < Constants.RecyclerNumInputSlots; ++i)
			{
				var split = gears.SplitItem(1) ?? gears;
				split.MoveToContainer(recycler.inventory, i, false);
			}
			
			recycler.inventory.SetLocked(true);
			recycler.SendNetworkUpdateImmediate();

			Crafters.Add(recycler.ServerPosition, crafter);
			crafterLookup.Add(recycler, crafter);

			return crafter;
		}

		/// <summary>
		/// Destroys the given crafter and optionally spawns a research table in its place.
		/// </summary>
		/// <param name="crafter">The crafter to destroy.</param>
		/// <param name="downgrade">If true, then the recycler will be replaced with a research table.</param>
		public static void DestroyCrafter(Crafter crafter, bool downgrade, bool destroyOutputContainer, bool unloading = false)
		{
			Crafters.Remove(crafter.Position);
			crafterLookup.Remove(crafter.Recycler);
			
			if (downgrade)
			{
				crafter.Downgrade(destroyOutputContainer);
			}
			else
			{
				crafter.Destroy(destroyOutputContainer, unloading);
			}

			crafter.PlayerEnter -= OnPlayerEnterCrafter;
			crafter.PlayerLeave -= OnPlayerLeaveCrafter;
		}

		/// <summary>
		/// Returns true if the given recycler is a crafter.
		/// </summary>
		public static bool ContainsRecycler(Recycler recycler)
		{
			return crafterLookup.ContainsKey(recycler);
		}

		/// <summary>
		/// Retrieves the crafter of the given recycler. Returns null if none is found.
		/// </summary>
		public static Crafter GetCrafter(Recycler recycler)
		{
			if (!crafterLookup.ContainsKey(recycler))
				return null;

			return crafterLookup[recycler];
		}

		/// <summary>
		/// Returns the crafter that's within range and visible by the given player. If there's multiple then the closest one will be returned.
		/// </summary>
		public static Crafter FindByPlayer(BasePlayer player)
		{
			// Sort crafters by distance from player and search starting from the closest one.
			var crafters = Crafters.OrderBy(kv => (player.ServerPosition - kv.Key).sqrMagnitude);
			return crafters.FirstOrDefault(kv => kv.Value.NearbyPlayers.Contains(player)).Value;
		}

		/// <summary>
		/// Returns all crafters that are within range and visible by the given player. They will be sorted by ascending range.
		/// </summary>
		public static IEnumerable<Crafter> FindAllByPlayer(BasePlayer player)
		{
			var crafters = Crafters.OrderBy(kv => (player.ServerPosition - kv.Key).sqrMagnitude);

			foreach (var kv in crafters)
			{
				if (kv.Value.NearbyPlayers.Contains(player))
					yield return kv.Value;
			}
		} 

		#endregion

		private static void Tick()
		{
			float elapsed = Time.time - lastTick; // Elapsed time in seconds since last tick.
			lastTick = Time.time;

			foreach (var crafter in Crafters.Values.ToList())
			{
				if (crafter.Recycler.IsDestroyed)
					continue;

				crafter.Tick(elapsed);
			}
		}

		private static void OnPlayerEnterCrafter(Crafter crafter, BasePlayer player)
		{
			if (!numActiveCrafters.ContainsKey(player))
				numActiveCrafters[player] = 0;

			numActiveCrafters[player]++;

			// Only add ui for the first crafter, otherwise we'll add the player multiple times.
			if (numActiveCrafters[player] == 1)
			{
				UiManager.AddPlayerUI(activeCrafterUi, player);
			}
		}

		private static void OnPlayerLeaveCrafter(Crafter crafter, BasePlayer player)
		{
			numActiveCrafters[player]--;

			if (numActiveCrafters[player] <= 0)
			{
				numActiveCrafters.Remove(player);
				UiManager.RemoveUI(activeCrafterUi, player);
			}
		}
	}
}

namespace Oxide.Plugins.AutoCrafterNamespace
{
	public static class FxManager
	{
		public class RepeatedFx
		{
			private static int idCounter = 0;

			public int Id { get; private set; }
			public string FxName { get; private set; }
			public Vector3 Position { get; set; }
			public float Interval { get; private set; }
			public Timer Timer { get; set; }

			public float NextPlay = Time.time;

			public RepeatedFx(string fxName, Vector3 position, float interval)
			{
				Id = idCounter++;
				FxName = fxName;
				Position = position;
				Interval = interval;
				Timer = Utility.Timer.Every(interval, Play);
			}

			private void Play()
			{
				PlayFx(Position, FxName);
			}
		}

		private static Dictionary<int, RepeatedFx> RepeatingFx;
		
		public static void Initialize()
		{
			RepeatingFx = new Dictionary<int, RepeatedFx>();
		}

		public static void Destroy()
		{
			foreach (var fx in RepeatingFx.Values.ToList())
			{
				StopFx(fx);
			}
		}

		/// <summary>
		/// Plays the specified fx at the specified position.
		/// </summary>
		/// <param name="position">The position to play at.</param>
		/// <param name="fxName">The fx to play.</param>
		public static void PlayFx(Vector3 position, string fxName)
		{
			SpawnFx(position, fxName);
		}

		/// <summary>
		/// Plays the specified fx at the specified position repeatedly with the given interval.
		/// </summary>
		/// <param name="position">The position to play at.</param>
		/// <param name="fxName">The fx to play.</param>
		/// <param name="interval">The delay between plays in seconds.</param>
		/// <param name="initialDelay">Specifies an initial delay in seconds before playing the fx for the first time.</param>
		/// <returns></returns>
		public static RepeatedFx PlayFx(Vector3 position, string fxName, float interval, bool playAtSpawn = true)
		{
			var fx = new RepeatedFx(fxName, position, interval);

			if (playAtSpawn)
			{
				PlayFx(fx.Position, fx.FxName);
			}

			RepeatingFx.Add(fx.Id, fx);
			return fx;
		}

		/// <summary>
		/// Stops playing the repeating fx.
		/// </summary>
		/// <param name="fx">The fx to stop repeating.</param>
		public static void StopFx(RepeatedFx fx)
		{
			fx.Timer.DestroyToPool();
			fx.Timer = null;
			RepeatingFx.Remove(fx.Id);
		}

		private static void SpawnFx(Vector3 position, string fxName)
		{
			Effect.server.Run(fxName, position);
		}
	}
}

namespace Oxide.Plugins.AutoCrafterNamespace
{
	public static class Lang
	{
		private static Plugin plugin;
		private static Core.Libraries.Lang lang;

		public static readonly Dictionary<string, string> DefaultMessages = new Dictionary<string, string>
		{
			{"nopermission", "You don't have permission to use this."},
			{"invalid-target", "The deployable in front of you is not a {0}."},
			{"target-notowned", "You are not the owner of this deployable."},
			{"no-target", "No deployable could be found."},
			{"target-not-crafter", "The recycler in front of you is not a crafter."},
			{"crafted-items", "Crafted items"},
			{"queue-items", "Queue items"},
			{"item-notfound-skipping-ingredient", "Could not find an item with the shortname '{0}', skipping this ingredient!"},
			{"hit-again-to-repair", "Hit again to repair"},
			{"hp-message", "HP: {0}/{1}"},
			{"ingredients-missing-youneed", "You do not have the required ingredients.\nYou need:"},
			{"hammer-confirm-upgrade", "Hit again to upgrade to a crafter..."},
			{"hammer-confirm-downgrade", "Hit again to downgrade to a research table...\n\nItems will not be lost."},
			{"crafter-inrange", "Crafter active"},
			{"join-message", "This server has the AutoCrafter mod. Type /autocrafter to read more."},
			{"chat-title", "AutoCrafter"},
			{"chat-title-craft", "Crafting"},
			{"chat-title-usage", "Usage"},
			{"chat-title-more", "More"},
			{"chat-description-craft", "How to craft and what the requirements are."},
			{"chat-description-more", "More info that is useful to know but might not be obvious."},
			{"chat-unknown-selection", "Unknown sub menu selection. Please select one of the following:"},
			{
				"chat-default-text", "AutoCrafter allows for automatic crafting, even after you log off or go out to grind or kill nakeds.\n" +
				                     "To learn more, type /autocrafter and then one of the following words:\n"
			},
			{
				"chat-usage-text", "To start crafting something, stand infront of the crafter and start crafting normally.\n" +
				                   "You will know it's working if the machine starts and there's a message at the bottom of the screen."
			},
			{"chat-usage-text-droptop", "It is possible to put items in by dropping them at the top of the machine."},
			{
				"chat-more-text", "- You can put code locks on the crafters.\n" +
				                  "- Destroying it takes 2 c4, or 6 rockets. Melee is not viable.\n" +
				                  "- If destroyed the loot will spill out on the ground.\n" +
				                  "- You can check the HP by hitting it once with a hammer. Continue hitting it if you want to repair."
			},
			{
				"chat-craft-text-top", "To craft, you must first place a research table, then hit it two times with a hammer.\n" +
				                       "The requirements are:"
			},
			{
				"chat-craft-text-bottom", "It is possible to downgrade by hitting it twice again with a hammer. You will receive a full refund.\n" +
				                          "Note that upgrading and downgrading is limited by a 10 minute window from when you first placed the research table or upgraded."
			}
		};

		public static void Initialize(Plugin plugin, Core.Libraries.Lang lang)
		{
			Lang.plugin = plugin;
			Lang.lang = lang;
		}

		public static string Translate(BasePlayer player, string key, params object[] format)
		{
			return string.Format(lang.GetMessage(key, plugin, player?.UserIDString), format);
		}
	}
}

namespace Oxide.Plugins.AutoCrafterNamespace
{
	public class PluginConfig
	{
		public class ItemAmount
		{
			public string Shortname;
			public int Amount;

			public ItemAmount(string shortname, int amount)
			{
				Shortname = shortname;
				Amount = amount;
			}
		}

		public bool ScanForWorldItems { get; set; } = true;

		public bool ShowPlayerInstructionsOnFirstJoin { get; set; } = true;
		public bool ShowInstructionsAsGameTip { get; set; } = true;

		public List<ItemAmount> UpgradeCost { get; set; } = new List<ItemAmount>();
		
		public float[] CrafterProtectionProperties { get; set; } =
		{
			0.98f, // Generic
			0, // Hunger
			0, // Thirst
			0, // Cold
			0, // Drowned
			1, // Heat
			0, // Bleeding
			0, // Poison
			0, // Suicide
			0.999f, // Bullet
			0.99f, // Slash
			0.99f, // Blunt
			0, // Fall
			1, // Radiation
			0.99f, // Bite
			0.98f, // Stab
			0.3f, // Explosion
			0, // RadiationExposure
			0, // ColdExposure
			0, // Decay
			0, // ElectricShock
			1 // Arrow
		};
	}
}

namespace Oxide.Plugins.AutoCrafterNamespace
{
	public static class UiManager
	{
		/// <summary>
		/// Lookup map of active uis and the players that have it active.
		/// </summary>
		private static Dictionary<string, List<BasePlayer>> activeUis;

		/// <summary>
		/// Lookup table for object based uis.
		/// </summary>
		private static Dictionary<string, UIBase> uiLookup;

		private static Timer tickTimer;
		private static float lastTick = Time.time;

		public static void Initialize()
		{
			activeUis = new Dictionary<string, List<BasePlayer>>();
			uiLookup = new Dictionary<string, UIBase>();
			tickTimer = Utility.Timer.Every(0.5f, Tick); // Update ui every 500ms.
		}

		public static void Destroy()
		{
			tickTimer.DestroyToPool();
			ClearAllUI();
			DestroyAllUI();
		}

		private static void Tick()
		{
			float elapsed = Time.time - lastTick;
			lastTick = Time.time;

			foreach (var ui in uiLookup.Values)
			{
				var playerList = GetPlayerList(ui);

				ui.Tick(elapsed);

				if (ui.Dirty)
				{
					SendUI(ui, playerList);
					ui.ResetDirty();
				}
			}
		}

		/// <summary>
		/// Sends the given ui to the specified players.
		/// </summary>
		/// <param name="ui">The ui to send.</param>
		/// <param name="players">The players to send to.</param>
		private static void SendUI(UIBase ui, IEnumerable<BasePlayer> players)
		{
			foreach (var player in players)
			{
				SendUI(ui, player);
			}
		}

		/// <summary>
		/// Sends the given ui the the specified player.
		/// </summary>
		/// <param name="ui">The ui to send,</param>
		/// <param name="player">The player to send to.</param>
		private static void SendUI(UIBase ui, BasePlayer player)
		{
			CuiHelper.DestroyUi(player, ui.Identifier);
			CuiHelper.AddUi(player, ui.Elements);
		}

		#region Public api methods

		public static T CreateUI<T>() where T : UIBase
		{
			var instance = Activator.CreateInstance<T>();
			instance.CreateUI();

			if (instance.Identifier == null)
				throw new InvalidOperationException("Instantiated UI does not have an identifier set after ui creation.");

			if (uiLookup.ContainsKey(instance.Identifier))
				throw new InvalidOperationException("Instantiated UI does not have a unique identifier set. (conflict found)");

			activeUis.Add(instance.Identifier, new List<BasePlayer>());
			uiLookup.Add(instance.Identifier, instance);

			return instance;
		}

		/// <summary>
		/// Adds the given player to the specified ui. The player will receied the ui and subsequent updates until removed.
		/// </summary>
		/// <param name="ui">The ui to send to the player.</param>
		/// <param name="player">The player to add.</param>
		public static void AddPlayerUI(UIBase ui, BasePlayer player)
		{
			var players = GetPlayerList(ui);
			players.Add(player);

			SendUI(ui, player);
		}

		/// <summary>
		/// Removes all ui instances for all players.
		/// </summary>
		public static void ClearAllUI()
		{
			foreach (string uiKey in activeUis.Keys.ToList())
			{
				ClearUI(uiLookup[uiKey]);
			}
		}

		/// <summary>
		/// Removes the ui for all the given players.
		/// </summary>
		/// <param name="ui">The ui to remove.</param>
		public static void RemoveUI(UIBase ui, IEnumerable<BasePlayer> players)
		{
			foreach (var player in players)
			{
				RemoveUI(ui, player);
			}
		}

		/// <summary>
		/// Removes the ui for the given player.
		/// </summary>
		/// <param name="ui">The ui to remove.</param>
		public static void RemoveUI(UIBase ui, BasePlayer player)
		{
			if (!activeUis.ContainsKey(ui.Identifier))
				throw new ArgumentException("There is no active ui with the specified key.");

			CuiHelper.DestroyUi(player, ui.Identifier);
			activeUis[ui.Identifier].Remove(player);
		}

		/// <summary>
		/// Removes the ui for all players.
		/// </summary>
		/// <param name="ui">The ui to remove.</param>
		public static void ClearUI(UIBase ui)
		{
			if (!activeUis.ContainsKey(ui.Identifier))
				throw new ArgumentException("There is no active ui with the specified key.");

			foreach (var player in activeUis[ui.Identifier].ToList())
			{
				RemoveUI(ui, player);
			}
		}

		public static void DestroyAllUI()
		{
			foreach (var ui in uiLookup.Values.ToList())
			{
				DestroyUI(ui);
			}
		}

		public static void DestroyUI(UIBase ui)
		{
			ClearUI(ui);
			ui.Destroy();

			activeUis.Remove(ui.Identifier);
			uiLookup.Remove(ui.Identifier);
		}

		#endregion

		private static List<BasePlayer> GetPlayerList(UIBase ui)
		{
			if (activeUis.ContainsKey(ui.Identifier))
				return activeUis[ui.Identifier];

			var list = new List<BasePlayer>();
			activeUis[ui.Identifier] = list;
			return list;
		} 
	}
}

namespace Oxide.Plugins.AutoCrafterNamespace
{
	public static class Utility
	{
		public static PluginTimers Timer { get; set; }
		public static PluginConfig Config { get; set; }

		/// <summary>
		/// Converts the Vector3 into a string in the format of "x,y,z".
		/// </summary>
		public static string ToXYZString(this Vector3 vec)
		{
			return vec.x.ToString(CultureInfo.InvariantCulture) + "," +
			       vec.y.ToString(CultureInfo.InvariantCulture) + "," +
			       vec.z.ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Parses a Vector3 from a string with the format "x,y,z".
		/// </summary>
		public static Vector3 ParseXYZ(string str)
		{
			string[] xyz = str.Split(',');
			float x = float.Parse(xyz[0], CultureInfo.InvariantCulture);
			float y = float.Parse(xyz[1], CultureInfo.InvariantCulture);
			float z = float.Parse(xyz[2], CultureInfo.InvariantCulture);
			return new Vector3(x, y, z);
		}

		public static void LogComponents(GameObject gameObject)
		{
			var components = gameObject.GetComponents<MonoBehaviour>();
			var builder = new StringBuilder();

			for (int i = 0; i < components.Length; i++)
			{
				var component = components[i];
				builder.Append(component.GetType().Name);

				if (i < components.Length - 1)
					builder.Append(", ");
			}

			Log(builder.ToString());
		}

		public static void LogComponents(MonoBehaviour behaviour)
		{
			LogComponents(behaviour.gameObject);
		}

		public static void Log(string str)
		{
			Debug.Log("[AutoCrafter] " + str);
		}

		public static void LogWarning(string str)
		{
			Debug.LogWarning("[AutoCrafter] " + str);
		}
	}
}

namespace Oxide.Plugins.AutoCrafterNamespace.Extensions
{
	public static class BasePlayerExtensions
	{
		private static readonly Dictionary<BasePlayer, Timer> gameTipTimers = new Dictionary<BasePlayer, Timer>();
		private static readonly Dictionary<BasePlayer, ScreenMessageUI> screenMessages = new Dictionary<BasePlayer, ScreenMessageUI>(); 

		public static void GiveItems(this BasePlayer player, IEnumerable<Item> items, BaseEntity.GiveItemReason reason = BaseEntity.GiveItemReason.Generic)
		{
			foreach (var item in items)
			{
				player.GiveItem(item, reason);
			}
		}

		public static void TranslatedChatMessage(this BasePlayer player, string key, params object[] format)
		{
			player.ChatMessage(Lang.Translate(player, key, format));
		}

		/// <summary>
		/// Shows a game tip for the player. Optionally hide it after the specified time in seconds.
		/// </summary>
		/// <param name="message">The message to show.</param>
		/// <param name="time">The time in seconds before it dissapears. If 0 or below, it will stay forever. Use HideGameTip to hide it manually.</param>
		public static void ShowGameTip(this BasePlayer player, string message, float time = 0)
		{
			if (gameTipTimers.ContainsKey(player))
			{
				gameTipTimers[player].DestroyToPool();
				gameTipTimers.Remove(player);
			}

			player.SendConsoleCommand("gametip.showgametip", message);

			if (time > 0)
				gameTipTimers.Add(player, Utility.Timer.Once(time, player.HideGameTip));
		}

		/// <summary>
		/// Hides the game tip that the player is currently seeing.
		/// </summary>
		/// <param name="player"></param>
		public static void HideGameTip(this BasePlayer player)
		{
			if (gameTipTimers.ContainsKey(player))
			{
				gameTipTimers[player].DestroyToPool();
				gameTipTimers.Remove(player);
			}

			player.SendConsoleCommand("gametip.hidegametip");
		}

		/// <summary>
		/// Returns true if the player has the specified ingredients.
		/// </summary>
		/// <param name="ingredients">The ingredients to check.</param>
		public static bool CanCraft(this BasePlayer player, IEnumerable<ItemAmount> ingredients)
		{
			foreach (var itemAmount in ingredients)
			{
				int amount = player.inventory.GetAmount(itemAmount.itemid);

				if (amount < itemAmount.amount)
					return false;
			}

			return true;
		}

		/// <summary>
		/// Shows a screen message to the player. Optionally hide it after the specified time in seconds.
		/// </summary>
		/// <param name="message">The message to show.</param>
		/// <param name="time">The time in seconds before it dissapears. If 0 or below, it will stay forever. Use HideScreenMessage to hide it manually.</param>
		public static void ShowScreenMessage(this BasePlayer player, string message, float time, TextAnchor textAnchor = TextAnchor.MiddleCenter)
		{
			message = message.Replace("\r", ""); // Remove \r in new lines from stringbuilder etc.

			if (gameTipTimers.ContainsKey(player))
			{
				HideGameTip(player);
			}

			var screenMessage = GetOrCreateScreenMessage(player);

			screenMessage.Text = message;
			screenMessage.TextAnchor = textAnchor;
			UiManager.AddPlayerUI(screenMessage, player);

			if (time > 0)
			{
				gameTipTimers.Add(player, Utility.Timer.Once(time, () =>
				{
					HideScreenMessage(player);
				}));
			}
		}

		public static void HideScreenMessage(this BasePlayer player)
		{
			if (!screenMessages.ContainsKey(player))
				return;

			UiManager.RemoveUI(screenMessages[player], player);
		}

		private static ScreenMessageUI GetOrCreateScreenMessage(BasePlayer player)
		{
			if (!screenMessages.ContainsKey(player))
				screenMessages.Add(player, UiManager.CreateUI<ScreenMessageUI>());

			return screenMessages[player];
		}

		public static void CloseInventory(this BasePlayer player)
		{
			player.ClientRPC(null, "OnRespawnInformation", new RespawnInformation {spawnOptions = new List<RespawnInformation.SpawnOptions>()}.ToProtoBytes());
		}
	}
}

namespace Oxide.Plugins.AutoCrafterNamespace.Extensions
{
	public static class ContainerExtensions
	{
		/// <summary>
		/// Returns true if there are items in this container that aren't about to be removed.
		/// </summary>
		/// <param name="container"></param>
		/// <returns></returns>
		public static bool AnyItems(this ItemContainer container)
		{
			if (container.itemList == null || container.itemList.Count <= 0)
				return false;

			return container.itemList.Any(item => item.removeTime <= 0f);
		}
	}
}

namespace Oxide.Plugins.AutoCrafterNamespace.Extensions
{
	public static class JsonExtensions
	{
		private static readonly List<JsonConverter> converters = new List<JsonConverter>
		{
			new Vector2Converter(),
			new Vector3Converter()
		};

		/// <summary>
		/// Adds additional json converters to this settings instance. It will not add duplicate converters so it's safe to call multiple times.
		/// </summary>
		/// <param name="settings"></param>
		public static void AddConverters(this JsonSerializerSettings settings)
		{
			foreach (var converter in converters)
			{
				// Make sure the converter isn't already added.
				if (settings.Converters.Any(conv => conv.GetType() == converter.GetType()))
					continue;

				settings.Converters.Add(converter);
			}
		}
	}
}

namespace Oxide.Plugins.AutoCrafterNamespace.JsonConverters
{
	public class Vector2Converter : JsonConverter
	{
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var vec = (Vector2)value;
			serializer.Serialize(writer, new float[] {vec.x, vec.y});
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			Vector2 result = new Vector2();
			JArray jVec = JArray.Load(reader);

			result.x = jVec[0].ToObject<float>();
			result.y = jVec[1].ToObject<float>();

			return result;
		}

		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(Vector2);
		}
	}
}

namespace Oxide.Plugins.AutoCrafterNamespace.JsonConverters
{
	public class Vector3Converter : JsonConverter
	{
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var vec = (Vector3) value;
			serializer.Serialize(writer, new float[] {vec.x, vec.y, vec.z});
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			Vector3 result = new Vector3();
			JArray jVec = JArray.Load(reader);

			result.x = jVec[0].ToObject<float>();
			result.y = jVec[1].ToObject<float>();
			result.z = jVec[2].ToObject<float>();

			return result;
		}

		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(Vector3);
		}
	}
}

namespace Oxide.Plugins.AutoCrafterNamespace.UI
{
	/// <summary>
	/// A notifier at the bottom of the screen that shows up when a player is inside a crafters range.
	/// </summary>
	public class ActiveCrafterUI : UIBase
	{
		public override void CreateUI()
		{
			var root = new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.425 0",
					AnchorMax = "0.575 0.021"
				},
				Image =
				{
					Color = "0 0 0 0",
					FadeIn = 0.2f
				},
				FadeOut = 0.2f
			};

			var background = new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0",
					AnchorMax = "1 1"
				},
				Image =
				{
					Color = "0 0 0 0.8"
				}
			};
			
			var label = new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0",
					AnchorMax = "1 1"
				},
				Text =
				{
					Text = Lang.Translate(null, "crafter-inrange"),
					Color = "0.9 0.9 0.9 1",
					FontSize = 12,
					Align = TextAnchor.MiddleCenter
				}
			};

			string rootKey = Elements.Add(root, "Overlay");
			Elements.Add(background, rootKey);
			Elements.Add(label, rootKey);
		}
	}
}

namespace Oxide.Plugins.AutoCrafterNamespace.UI
{
	public class ScreenMessageUI : UIBase
	{
		public string Text
		{
			get { return contentLabel.Text.Text; }
			set
			{
				contentLabel.Text.Text = value;
				MakeDirty();
			}
		}

		public TextAnchor TextAnchor
		{
			get
			{
				return contentLabel.Text.Align;
			}
			set
			{
				contentLabel.Text.Align = value;
				MakeDirty();
			}
		}

		private CuiLabel contentLabel;

		public override void CreateUI()
		{
			string rootKey = Elements.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.4 0.15",
					AnchorMax = "0.6 0.3"
				},
				Image =
				{
					Color = "0 0 0 0.7"
				}
			});

			contentLabel = new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0.02 0.01",
					AnchorMax = "0.98 0.99"
				},
				Text =
				{
					Text = "",
					Color = "0.9 0.9 0.9 1",
					FontSize = 14,
					Align = TextAnchor.MiddleCenter
				}
			};
			
			Elements.Add(contentLabel, rootKey);
		}
	}
}

namespace Oxide.Plugins.AutoCrafterNamespace.UI
{
	public abstract class UIBase
	{
		public string Identifier => Elements.FirstOrDefault()?.Name;
		public CuiElementContainer Elements { get; protected set; } = new CuiElementContainer();

		public bool Dirty { get; private set; } = true;

		public abstract void CreateUI();

		public virtual void Destroy()
		{
		}

		public virtual void Tick(float elapsed)
		{
		}

		/// <summary>
		/// Sets the dirty flag. The ui will be sent to players on the next ui tick.
		/// </summary>
		protected void MakeDirty()
		{
			Dirty = true;
		}
		
		/// <summary>
		/// Removes the dirty flag.
		/// </summary>
		public void ResetDirty()
		{
			Dirty = false;
		}
	}
}
