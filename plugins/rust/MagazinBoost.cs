using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace Oxide.Plugins
{
	[Info("MagazinBoost", "Fujikura", "1.6.2", ResourceId = 1962)]
	[Description("Can change magazines, ammo and condition for most projectile weapons")]
	public class MagazinBoost : RustPlugin
	{
		bool Changed;

		Dictionary<string, object> weaponContainer = new Dictionary <string, object>();
		Dictionary<string, string> guidToPath;

		#region Config

		string permissionAll;
		string permissionMaxAmmo;
		string permissionPreLoad;
		string permissionMaxCondition;
		string permissionAmmoType;
		bool checkPermission;
		bool removeSkinIfNoRights;

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

		void LoadVariables()
		{
			permissionAll = Convert.ToString(GetConfig("Permissions", "permissionAll", "magazinboost.canall"));
			permissionMaxAmmo = Convert.ToString(GetConfig("Permissions", "permissionMaxAmmo", "magazinboost.canmaxammo"));
			permissionPreLoad = Convert.ToString(GetConfig("Permissions", "permissionPreLoad", "magazinboost.canpreload"));
			permissionMaxCondition = Convert.ToString(GetConfig("Permissions", "permissionMaxCondition", "magazinboost.canmaxcondition"));
			permissionAmmoType = Convert.ToString(GetConfig("Permissions", "permissionAmmoType", "magazinboost.canammotype"));
			checkPermission = Convert.ToBoolean(GetConfig("CheckRights", "checkForRightsInBelt", true));
			removeSkinIfNoRights = Convert.ToBoolean(GetConfig("CheckRights", "removeSkinIfNoRights", true));
			weaponContainer = (Dictionary<string, object>)GetConfig("Weapons", "Data", new Dictionary<string, object>());

			if (!Changed) return;
			SaveConfig();
			Changed = false;
		}

		protected override void LoadDefaultConfig()
		{
			Config.Clear();
			LoadVariables();
		}

		#endregion Config

		void GetWeapons()
		{
			weaponContainer = (Dictionary <string, object>)Config["Weapons", "Data"];
			var weapons = ItemManager.GetItemDefinitions().Where(p => p.category == ItemCategory.Weapon && p.GetComponent<ItemModEntity>() != null);

			if (weaponContainer != null && weaponContainer.Count() > 0)
			{
				int countLoadedServerStats = 0;
				foreach (var weapon in weapons)
				{
					if (!guidToPath.ContainsKey(weapon.GetComponent<ItemModEntity>().entityPrefab.guid) || weapon.GetComponent<ItemModEntity>().entityPrefab.Get().GetComponent<BaseProjectile>() == null) continue;
					if (weaponContainer.ContainsKey(weapon.shortname))
					{
						Dictionary <string, object> serverDefaults = weaponContainer[weapon.shortname] as Dictionary <string, object>;
						if(!serverDefaults.ContainsKey("givemaxammo"))
						{
							serverDefaults.Add("givemaxammo", serverDefaults["servermaxammo"]);
							serverDefaults.Add("givepreload", serverDefaults["serverpreload"]);
							serverDefaults.Add("giveammotype", serverDefaults["serverammotype"]);
							serverDefaults.Add("givemaxcondition", serverDefaults["servermaxcondition"]);
							serverDefaults.Add("giveskinid", 0);
						}

						if ((bool)serverDefaults["serveractive"])
						{
							ItemDefinition weaponDef = ItemManager.FindItemDefinition(weapon.shortname);
							weaponDef.GetComponent<ItemModEntity>().entityPrefab.Get().GetComponent<BaseProjectile>().primaryMagazine.definition.builtInSize = (int)serverDefaults["servermaxammo"];
							weaponDef.GetComponent<ItemModEntity>().entityPrefab.Get().GetComponent<BaseProjectile>().primaryMagazine.contents = (int)serverDefaults["serverpreload"];
							ItemDefinition ammo = ItemManager.FindItemDefinition((string)serverDefaults["serverammotype"]);
							if (ammo != null)
								weaponDef.GetComponent<ItemModEntity>().entityPrefab.Get().GetComponent<BaseProjectile>().primaryMagazine.ammoType = ammo;
							weaponDef.condition.max = Convert.ToSingle(serverDefaults["servermaxcondition"]);
							countLoadedServerStats++;
						}
						continue;
					}
					Dictionary <string, object> weaponStats = new Dictionary <string, object>();
					weaponStats.Add("displayname", weapon.displayName.english);
					weaponStats.Add("maxammo", weapon.GetComponent<ItemModEntity>().entityPrefab.Get().GetComponent<BaseProjectile>().primaryMagazine.definition.builtInSize);
					weaponStats.Add("preload", weapon.GetComponent<ItemModEntity>().entityPrefab.Get().GetComponent<BaseProjectile>().primaryMagazine.contents);
					weaponStats.Add("maxcondition", weapon.condition.max);
					weaponStats.Add("ammotype", weapon.GetComponent<ItemModEntity>().entityPrefab.Get().GetComponent<BaseProjectile>().primaryMagazine.ammoType.shortname);
					weaponStats.Add("skinid", 0);
					weaponStats.Add("settingactive", true);
					weaponStats.Add("servermaxammo", weapon.GetComponent<ItemModEntity>().entityPrefab.Get().GetComponent<BaseProjectile>().primaryMagazine.definition.builtInSize);
					weaponStats.Add("serverpreload", weapon.GetComponent<ItemModEntity>().entityPrefab.Get().GetComponent<BaseProjectile>().primaryMagazine.contents);
					weaponStats.Add("serverammotype", weapon.GetComponent<ItemModEntity>().entityPrefab.Get().GetComponent<BaseProjectile>().primaryMagazine.ammoType.shortname);
					weaponStats.Add("servermaxcondition", weapon.condition.max);
					weaponStats.Add("serveractive", false);
					weaponStats.Add("givemaxammo", weapon.GetComponent<ItemModEntity>().entityPrefab.Get().GetComponent<BaseProjectile>().primaryMagazine.definition.builtInSize);
					weaponStats.Add("givepreload", weapon.GetComponent<ItemModEntity>().entityPrefab.Get().GetComponent<BaseProjectile>().primaryMagazine.contents);
					weaponStats.Add("giveammotype", weapon.GetComponent<ItemModEntity>().entityPrefab.Get().GetComponent<BaseProjectile>().primaryMagazine.ammoType.shortname);
					weaponStats.Add("givemaxcondition", weapon.condition.max);
					weaponContainer.Add(weapon.shortname, weaponStats);
					Puts($"Added NEW weapon '{weapon.displayName.english} ({weapon.shortname})' to weapons list");
				}
				if (countLoadedServerStats > 0)
					Puts($"Changed server default values for '{countLoadedServerStats}' weapons");
				Config["Weapons", "Data"] = weaponContainer;
				Config.Save();
				return;
			}
			else
			{
				int counter = 0;
				foreach (var weapon in weapons)
				{
					if (!guidToPath.ContainsKey(weapon.GetComponent<ItemModEntity>().entityPrefab.guid)) continue;
					if (weapon.GetComponent<ItemModEntity>().entityPrefab.Get().GetComponent<BaseProjectile>() == null) continue;

					Dictionary <string, object> weaponStats = new Dictionary <string, object>();
					weaponStats.Add("displayname", weapon.displayName.english);
					weaponStats.Add("maxammo", weapon.GetComponent<ItemModEntity>().entityPrefab.Get().GetComponent<BaseProjectile>().primaryMagazine.definition.builtInSize);
					weaponStats.Add("preload", weapon.GetComponent<ItemModEntity>().entityPrefab.Get().GetComponent<BaseProjectile>().primaryMagazine.contents);
					weaponStats.Add("maxcondition", weapon.condition.max);
					weaponStats.Add("ammotype", weapon.GetComponent<ItemModEntity>().entityPrefab.Get().GetComponent<BaseProjectile>().primaryMagazine.ammoType.shortname);
					weaponStats.Add("skinid", 0);
					weaponStats.Add("settingactive", true);
					weaponStats.Add("servermaxammo", weapon.GetComponent<ItemModEntity>().entityPrefab.Get().GetComponent<BaseProjectile>().primaryMagazine.definition.builtInSize);
					weaponStats.Add("serverpreload", weapon.GetComponent<ItemModEntity>().entityPrefab.Get().GetComponent<BaseProjectile>().primaryMagazine.contents);
					weaponStats.Add("serverammotype", weapon.GetComponent<ItemModEntity>().entityPrefab.Get().GetComponent<BaseProjectile>().primaryMagazine.ammoType.shortname);
					weaponStats.Add("servermaxcondition", weapon.condition.max);
					weaponStats.Add("serveractive", false);
					weaponStats.Add("givemaxammo", weapon.GetComponent<ItemModEntity>().entityPrefab.Get().GetComponent<BaseProjectile>().primaryMagazine.definition.builtInSize);
					weaponStats.Add("givepreload", weapon.GetComponent<ItemModEntity>().entityPrefab.Get().GetComponent<BaseProjectile>().primaryMagazine.contents);
					weaponStats.Add("giveammotype", weapon.GetComponent<ItemModEntity>().entityPrefab.Get().GetComponent<BaseProjectile>().primaryMagazine.ammoType.shortname);
					weaponStats.Add("givemaxcondition", weapon.condition.max);
					weaponStats.Add("giveskinid", 0);
					weaponContainer.Add(weapon.shortname, weaponStats);
					counter++;
				}
				Puts($"Created initial weaponlist with '{counter}' projectile weapons.");
				Config["Weapons", "Data"] = weaponContainer;
				Config.Save();
				return;
			}
		}

		bool hasAnyRight(BasePlayer player)
		{
			if (permission.UserHasPermission(player.UserIDString, permissionAll)) return true;
			if (permission.UserHasPermission(player.UserIDString, permissionMaxAmmo)) return true;
			if (permission.UserHasPermission(player.UserIDString, permissionPreLoad)) return true;
			if (permission.UserHasPermission(player.UserIDString, permissionMaxCondition)) return true;
			if (permission.UserHasPermission(player.UserIDString, permissionAmmoType)) return true;
			return false;
		}

		bool hasRight(BasePlayer player, string perm)
		{
			bool right = false;
			switch (perm)
			{
				case "all":
						if (permission.UserHasPermission(player.UserIDString, permissionAll)) {right = true;}
						break;
				case "maxammo":
						if (permission.UserHasPermission(player.UserIDString, permissionMaxAmmo)) {right = true;}
						break;
				case "preload":
						if (permission.UserHasPermission(player.UserIDString, permissionPreLoad)) {right = true;}
						break;
				case "maxcondition":
						if (permission.UserHasPermission(player.UserIDString, permissionMaxCondition)) {right = true;}
						break;
				case "ammotype":
						if (permission.UserHasPermission(player.UserIDString, permissionAmmoType)) {right = true;}
						break;
				default:
						break;

			}
			return right;
		}

		void OnServerInitialized()
		{
			LoadVariables();
			guidToPath = GameManifest.guidToPath;
			GetWeapons();
			permission.RegisterPermission(permissionAll, this);
			permission.RegisterPermission(permissionMaxAmmo, this);
			permission.RegisterPermission(permissionPreLoad, this);
			permission.RegisterPermission(permissionMaxCondition, this);
			permission.RegisterPermission(permissionAmmoType, this);
		}

		void OnItemCraftFinished(ItemCraftTask task, Item item)
		{
			if(!(item.GetHeldEntity() is BaseProjectile)) return;
			if(!hasAnyRight(task.owner)) return;
			Dictionary <string, object> weaponStats = null;
			if (weaponContainer.ContainsKey(item.info.shortname))
				weaponStats = weaponContainer[item.info.shortname] as Dictionary <string, object>;
			if (!(bool)weaponStats["settingactive"]) return;
			if (hasRight(task.owner,"maxammo") || hasRight(task.owner, "all"))
				(item.GetHeldEntity() as BaseProjectile).primaryMagazine.capacity = (int)weaponStats["maxammo"];
			if (hasRight(task.owner,"preload") || hasRight(task.owner, "all"))
				(item.GetHeldEntity() as BaseProjectile).primaryMagazine.contents = (int)weaponStats["preload"];
			if (hasRight(task.owner,"ammotype") || hasRight(task.owner, "all"))
			{
				var ammo = ItemManager.FindItemDefinition((string)weaponStats["ammotype"]);
				if (ammo != null)
					(item.GetHeldEntity() as BaseProjectile).primaryMagazine.ammoType = ammo;
			}
			if (hasRight(task.owner,"maxcondition") || hasRight(task.owner, "all"))
			{
				item._maxCondition = Convert.ToSingle(weaponStats["maxcondition"]);
				item._condition = Convert.ToSingle(weaponStats["maxcondition"]);
			}
			if((int)weaponStats["skinid"] > 0)
			{
				item.skin = Convert.ToUInt64(weaponStats["skinid"]);
				item.GetHeldEntity().skinID = Convert.ToUInt64(weaponStats["skinid"]);
			}
		}

		private void OnItemAddedToContainer(ItemContainer container, Item item)
		{
			if(!checkPermission) return;
			if(item.GetHeldEntity() is BaseProjectile && container.HasFlag(ItemContainer.Flag.Belt))
			{
				Dictionary <string, object> weaponStats = null;
				object checkStats;
				if (weaponContainer.TryGetValue(item.info.shortname, out checkStats))
				{
					weaponStats = checkStats as Dictionary <string, object>;
					if (!(bool)weaponStats["settingactive"]) return;
				}
				else
					return;
				if ((item.GetHeldEntity() as BaseProjectile).primaryMagazine.capacity > item.info.GetComponent<ItemModEntity>().entityPrefab.Get().GetComponent<BaseProjectile>().primaryMagazine.definition.builtInSize && !(hasRight(container.playerOwner, "maxammo") || hasRight(container.playerOwner, "all")))
				{
					(item.GetHeldEntity() as BaseProjectile).primaryMagazine.capacity = item.info.GetComponent<ItemModEntity>().entityPrefab.Get().GetComponent<BaseProjectile>().primaryMagazine.definition.builtInSize;
					if ((item.GetHeldEntity() as BaseProjectile).primaryMagazine.contents > (item.GetHeldEntity() as BaseProjectile).primaryMagazine.capacity)
						(item.GetHeldEntity() as BaseProjectile).primaryMagazine.contents = (item.GetHeldEntity() as BaseProjectile).primaryMagazine.capacity;
				}
				if (item.maxCondition > item.info.condition.max && !(hasRight(container.playerOwner, "maxcondition") || hasRight(container.playerOwner, "all")))
				{
					var newCon = item.condition * (item.info.condition.max / item.maxCondition);
					item._maxCondition = Convert.ToSingle(item.info.condition.max);
					item._condition = Convert.ToSingle(newCon);
				}
				if (removeSkinIfNoRights && !hasAnyRight(container.playerOwner) && item.GetHeldEntity().skinID == Convert.ToUInt64(weaponStats["skinid"]) && item.GetHeldEntity().skinID != 0uL)
				{
					item.skin = 0uL;
					item.GetHeldEntity().skinID = 0uL;
				}
			}
		}

		[ConsoleCommand("mb.giveplayer")]
		void BoostGive(ConsoleSystem.Arg arg)
		{
			if (arg.Connection != null && arg.Connection.authLevel < 2) return;
			if (arg.Args == null || arg.Args.Length < 2)
			{
				SendReply(arg, "Usage: magazinboost.give playername|id weaponshortname (optional: skinid)");
				return;
			}

			ulong skinid = 0;
			if (arg.Args.Length > 2)
			{
				if (!ulong.TryParse(arg.Args[2], out skinid))
				{
					SendReply(arg, "Skin has to be a number");
					return;
				}
				if (arg.Args[2].Length != 9)
				{
					SendReply(arg, "Skin has to be a 9-digit number");
					return;
				}
			}

			BasePlayer target = BasePlayer.Find(arg.Args[0]);
			if (target == null)
			{
				SendReply(arg, $"Player '{arg.Args[0]}' not found");
				return;
			}

			Dictionary <string, object> weaponStats = null;
			object checkStats;
			if (weaponContainer.TryGetValue(arg.Args[1], out checkStats))
				weaponStats = checkStats as Dictionary <string, object>;
			else
			{
				SendReply(arg, "Weapon '{arg.Args[0]}' not included/supported");
				return;
			}

			Item item = ItemManager.Create(ItemManager.FindItemDefinition(arg.Args[1]), 1, skinid);
			if (item == null)
			{
				SendReply(arg, "Weapon for unknown reason not created");
				return;
			}

			(item.GetHeldEntity() as BaseProjectile).primaryMagazine.capacity = (int)weaponStats["givemaxammo"];
			(item.GetHeldEntity() as BaseProjectile).primaryMagazine.contents = (int)weaponStats["givepreload"];
			var ammo = ItemManager.FindItemDefinition((string)weaponStats["giveammotype"]);
			if (ammo != null)
				(item.GetHeldEntity() as BaseProjectile).primaryMagazine.ammoType = ammo;
			item._maxCondition = Convert.ToSingle(weaponStats["givemaxcondition"]);
			item._condition = Convert.ToSingle(weaponStats["givemaxcondition"]);

			if (skinid == 0 && Convert.ToUInt64(weaponStats["giveskinid"]) > 0)
			skinid = Convert.ToUInt64(weaponStats["giveskinid"]);

			if(skinid > 0)
			{
				item.skin = Convert.ToUInt64(weaponStats["giveskinid"]);
				item.GetHeldEntity().skinID = Convert.ToUInt64(weaponStats["giveskinid"]);
			}
			target.GiveItem(item);
			SendReply(arg, $"Weapon '{arg.Args[1]}' given to Player '{target.displayName}'");
		}
	}
}