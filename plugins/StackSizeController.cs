using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Stack Size Controller", "Canopy Sheep", "1.9.9", ResourceId = 2320)]
    [Description("Allows you to set the max stack size of every item.")]
    public class StackSizeController : RustPlugin
    {
        #region Hooks
        protected override void LoadDefaultConfig()
        {
			PrintWarning("Creating a new configuration file.");

			var gameObjectArray = FileSystem.LoadAll<GameObject>("Assets/", ".item");
			var itemList = gameObjectArray.Select(x => x.GetComponent<ItemDefinition>()).Where(x => x != null).ToList();

			foreach (var item in itemList)
			{
				if (item.condition.enabled && item.condition.max > 0) { continue; }

				Config[item.displayName.english] = item.stackable;
			}
		}

        void OnServerInitialized()
        {
            permission.RegisterPermission("stacksizecontroller.canChangeStackSize", this);

			var dirty = false;
			var itemList = ItemManager.itemList;

			foreach (var item in itemList)
			{
				if (item.condition.enabled && item.condition.max > 0)
                {
                    if (Config[item.displayName.english] != null && (int)Config[item.displayName.english] != 1)
                    {
                        PrintWarning("WARNING: Item '" + item.displayName.english + "' will not stack more than 1 in game because it has durabililty (FACEPUNCH OVERRIDE). Changing stack size to 1..");
                        Config[item.displayName.english] = 1;
                        dirty = true;
                    }
                    continue;
                }

				if (Config[item.displayName.english] == null)
				{
					Config[item.displayName.english] = item.stackable;
					dirty = true;
				}

				item.stackable = (int)Config[item.displayName.english];
			}

			if (dirty == false) { return; }

			PrintWarning("Updating configuration file with new values.");
			SaveConfig();
		}

        bool hasPermission(BasePlayer player, string perm)
        {
            if (player.net.connection.authLevel > 1)
            {
                return true;
            }
            return permission.UserHasPermission(player.userID.ToString(), perm);
        }
        
        object CanMoveItem(Item item, PlayerInventory inventory, uint container, int slot, uint amount)
        {
            if (item.amount < UInt16.MaxValue) { return null; }

            ItemContainer itemContainer = inventory.FindContainer(container);
            if (itemContainer == null) { return null; }
            
            ItemContainer playerInventory = inventory.GetContainer(PlayerInventory.Type.Main);
            BasePlayer player = playerInventory.GetOwnerPlayer();

            if (!(player == null) && !(player.userID == 0) && FurnaceSplitter)
            {
                bool success = true;
                bool enabled = false;
                bool hasPermission = true;
                try
                {
                    enabled = (bool)FurnaceSplitter?.CallHook("GetEnabled", player);
                    hasPermission = (bool)FurnaceSplitter?.CallHook("HasPermission", player);
                }
                catch
                {
                    success = false;
                }
                if (success && enabled && hasPermission)
                {
                    List<string> CookingContainers = new List<string>()
                    {
                        "refinery_small_deployed",
                        "furnace",
                        "campfire",
                        "furnace.large",
                        "hobobarrel_static"
                    };

                    BaseEntity baseEntity = itemContainer.entityOwner;
                    if (!(baseEntity == null))
                    {
                        foreach (string CookingContainer in CookingContainers)
                        {
                            if (baseEntity.ShortPrefabName.Contains(CookingContainer))
                            {
                                return null;
                            }
                        }
                    }
                }
            }

            bool aboveMaxStack = false;
            int configAmount = (int)Config[item.info.displayName.english];

            if (item.amount > configAmount) { aboveMaxStack = true; }
            if (amount + item.amount / UInt16.MaxValue == item.amount % UInt16.MaxValue)
            {
                if (aboveMaxStack)
                {
                    Item item2 = item.SplitItem(configAmount);
                    if (!item2.MoveToContainer(itemContainer, slot, true))
                    {
                        item.amount += item2.amount;
                        item2.Remove(0f);
                    }
                    ItemManager.DoRemoves();
                    inventory.ServerUpdate(0f);
                    return true;
                }
                item.MoveToContainer(itemContainer, slot, true);
                return true;
            }
            else if (amount + (item.amount / 2) / UInt16.MaxValue == (item.amount / 2) % UInt16.MaxValue + item.amount % 2)
            {
                if (aboveMaxStack)
                {
                    Item split;
					if (configAmount > item.amount / 2) { split = item.SplitItem(Convert.ToInt32(item.amount) / 2); }
                    else { split = item.SplitItem(configAmount); }

                    if (!split.MoveToContainer(itemContainer, slot, true))
                    {
                        item.amount += split.amount;
                        split.Remove(0f);
                    }
                    ItemManager.DoRemoves();
                    inventory.ServerUpdate(0f);
                    return true;
                }
                Item item2 = item.SplitItem(item.amount / 2);
				if (!((item.amount + item2.amount) % 2 == 0)) { item2.amount++; item.amount--; }
				
                if (!item2.MoveToContainer(itemContainer, slot, true))
                {
                    item.amount += item2.amount;
                    item2.Remove(0f);
                }
                ItemManager.DoRemoves();
                inventory.ServerUpdate(0f);
                return true;
            }
            else if (item.amount > UInt16.MaxValue && amount != item.amount / 2)
            {
                Item item2;
                if (aboveMaxStack) { item2 = item.SplitItem(configAmount); }
                else { item2 = item.SplitItem(65000); }
                if (!item2.MoveToContainer(itemContainer, slot, true))
                {
                    item.amount += item2.amount;
                    item2.Remove(0f);
                }
                ItemManager.DoRemoves();
                inventory.ServerUpdate(0f);
                return true;
            }
            return null;
        }
        #endregion
        #region Commands
        [ChatCommand("stack")]
        private void StackCommand(BasePlayer player, string command, string[] args)
        {
            int stackAmount = 0;

            if (!hasPermission(player, "stacksizecontroller.canChangeStackSize"))
			{
				SendReply(player, "You don't have permission to use this command.");

				return;
			}

			if (args.Length <= 1)
			{
                SendReply(player, "Syntax Error: Requires 2 arguments. Syntax Example: /stack ammo.rocket.hv 64 (Use shortname)");

				return;
			}

            if (int.TryParse(args[1], out stackAmount) == false)
            {
                SendReply(player, "Syntax Error: Stack Amount is not a number. Syntax Example: /stack ammo.rocket.hv 64 (Use shortname)");

                return;
            }

            List<ItemDefinition> items = ItemManager.itemList.FindAll(x => x.shortname.Equals(args[0]));

            if (items.Count == 0)
            {
                SendReply(player, "Syntax Error: That is an incorrect item name. Please use a valid shortname.");
                return;
            }
            else
            {
                if (items[0].condition.enabled && items[0].condition.max > 0) { SendReply(player, "Error: This item cannot be stacked higher than 1."); return; }

                Config[items[0].displayName.english] = Convert.ToInt32(stackAmount);
                items[0].stackable = Convert.ToInt32(stackAmount);

                SaveConfig();

                SendReply(player, "Updated Stack Size for " + items[0].displayName.english + " (" + items[0].shortname + ") to " + stackAmount + ".");
            }
        }

        [ChatCommand("stackall")]
        private void StackAllCommand(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, "stacksizecontroller.canChangeStackSize"))
			{
				SendReply(player, "You don't have permission to use this command.");

				return;
			}

			if (args.Length == 0)
			{
                SendReply(player, "Syntax Error: Requires 1 argument. Syntax Example: /stackall 65000");

				return;
			}
			
            int stackAmount = 0;

            if (int.TryParse(args[0], out stackAmount) == false)
            {
                SendReply(player, "Syntax Error: Stack Amount is not a number. Syntax Example: /stackall 65000");

                return;
            }

            var itemList = ItemManager.itemList;

			foreach (var item in itemList)
			{
                if (item.condition.enabled && item.condition.max > 0) { continue; }
                if (item.displayName.english.ToString() == "Salt Water" ||
                item.displayName.english.ToString() == "Water") { continue; }

				Config[item.displayName.english] = Convert.ToInt32(args[0]);
				item.stackable = Convert.ToInt32(args[0]);
			}

            SaveConfig();

            SendReply(player, "The Stack Size of all stackable items has been set to " + args[0]);
        }

        [ConsoleCommand("stack")]
        private void StackConsoleCommand(ConsoleSystem.Arg arg)
        {
            int stackAmount = 0;

            if(arg.IsAdmin != true) { return; }

            if (arg.Args == null)
            {
                Puts("Syntax Error: Requires 2 arguments. Syntax Example: stack ammo.rocket.hv 64 (Use shortname)");

                return;
            }

            if (arg.Args.Length <= 1)
            {
                Puts("Syntax Error: Requires 2 arguments. Syntax Example: stack ammo.rocket.hv 64 (Use shortname)");

                return;
            }

            if (int.TryParse(arg.Args[1], out stackAmount) == false)
            {
                Puts("Syntax Error: Stack Amount is not a number. Syntax Example: stack ammo.rocket.hv 64 (Use shortname)");

                return;
            }

            List<ItemDefinition> items = ItemManager.itemList.FindAll(x => x.shortname.Equals(arg.Args[0]));

            if (items.Count == 0)
            {
                Puts("Syntax Error: That is an incorrect item name. Please use a valid shortname.");
                return;
            }
            else
            {
                if (items[0].condition.enabled && items[0].condition.max > 0) { Puts("Error: This item cannot be stacked higher than 1."); return; }
              
                Config[items[0].displayName.english] = Convert.ToInt32(stackAmount);
              
                items[0].stackable = Convert.ToInt32(stackAmount);

                SaveConfig();

                Puts("Updated Stack Size for " + items[0].displayName.english + " (" + items[0].shortname + ") to " + stackAmount + ".");
            }
        }

        [ConsoleCommand("stackall")]
        private void StackAllConsoleCommand(ConsoleSystem.Arg arg)
        {
            if(arg.IsAdmin != true) { return; }

            if (arg.Args.Length == 0)
			{
                Puts("Syntax Error: Requires 1 argument. Syntax Example: stackall 65000");

				return;
			}

			int stacksize;
          
            if (!(int.TryParse(arg.Args[0].ToString(), out stacksize)))
            {
                Puts("Syntax Error: That's not a number");
                return;
            }
			
            var itemList = ItemManager.itemList;

			foreach (var item in itemList)
			{
                if (item.condition.enabled && item.condition.max > 0) { continue; }
                if (item.displayName.english.ToString() == "Salt Water" ||
                item.displayName.english.ToString() == "Water") { continue; }

				Config[item.displayName.english] = Convert.ToInt32(arg.Args[0]);
				item.stackable = Convert.ToInt32(arg.Args[0]);
			}

            SaveConfig();

            Puts("The Stack Size of all stackable items has been set to " + arg.Args[0]);
        }
        #endregion
        #region Plugin References
        [PluginReference("FurnaceSplitter")]
        private Plugin FurnaceSplitter;
        #endregion
    }
}