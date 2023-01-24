using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using System.Linq;
using CodeHatch.Common;

using CodeHatch.Engine.Networking;

using Oxide.Core;
using Newtonsoft.Json.Linq;

using CodeHatch.Permissions;
using CodeHatch.ItemContainer;


namespace Oxide.Plugins
{
    [Info("InventoryInfo", "Shadow", "1.0.8")]
    public class InventoryInfo : ReignOfKingsPlugin
    {



		private Dictionary<ulong,Collection<string[]>> inventoryList = new Dictionary<ulong,Collection<string[]>>();
		private Dictionary<ulong,Collection<string[]>> inventorySaveList = new Dictionary<ulong,Collection<string[]>>();
		void Log(string msg) => Puts($"{Title} : {msg}");


        // SAVE DATA ===============================================================================================

       private void LoadInventoryData()
        {
            inventorySaveList = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong,Collection<string[]>>>("InventoryInfo");

        }

        private void SaveInventoryData()
        {
            Log("Saving data for Player Inventory");
            Interface.GetMod().DataFileSystem.WriteObject("InventoryInfo", inventorySaveList);
            Interface.GetMod().DataFileSystem.WriteObject("InventoryInfoWhenLockOut", inventoryList);
        }
        void Loaded()
        {

            inventoryList = new Dictionary<ulong,Collection<string[]>>();
            inventorySaveList = new Dictionary<ulong,Collection<string[]>>();
            LoadInventoryData();
            LoadDefaultMessages();
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {


                { "HelpTextTitle", "[FFFF00]InventoryInfo Commands[FFFF00]" },
                { "HelpTextSavedInventory", "[00BFFF]/Savedinv (playername)[FFFFFF] - Shows the playername, players and savedinventory." },
                { "HelpTextSaveYouInventory", "[00BFFF]/Save [FFFFFF] - Save you inventory inside saved inventory files." },
                { "HelpTextPlayerInventoryWhenLockOut", "[00BFFF]/LockoutInv (playername)[FFFFFF] - Shows Saved inventory when player lockout server." },
                { "HelpTextGivePlayerSavedInventoryWhenHeLockOut", "[00BFFF]/givelockoutinv (playername)[FFFFFF] - give player savedinventory back,what saved when he last time lockout server." },
                { "HelpTextGiveBackPlayerSavedInventory", "[00BFFF]/giveinv (playername)[FFFFFF] - Giving players saved inventory back." }
            }, this);
        }

        // ===========================================================================================================
       [ChatCommand("save")]		
		private void Save(Player player, string cmd, string[] args)
        {
     

			//Get the inventory contents
			var inventory = GetInventoryContents(player);

			// Check if the store exists
			if(inventorySaveList !=null)
			{
				//See if the player has an inventory stored here
				if(inventorySaveList.ContainsKey(player.Id))
				{
					//Remove the old record
					inventorySaveList.Remove(player.Id);
				}
				
				//Add the player's inventory record
				inventorySaveList.Add(player.Id,inventory);
			}

			Log("Saving Player Inventory Data for " + player.DisplayName);
			SaveInventoryData();
                       PrintToChat(player, "[00BFFF]You Inventory its Saved!");
                
        }
		 Collection<string[]> GetInventoryContents(Player player)
		{
			var inventory = player.GetInventory().Contents;
			var inventoryContents = new Collection<string[]>();
			foreach (var item in inventory.Where(item => item != null))


            {
				string[] tempStack = new string[]{ item.Name, item.StackAmount.ToString() };
				inventoryContents.Add(tempStack);
            }
			return inventoryContents;
		}

		private void OnPlayerDisconnected(Player player, string cmd, string[] args)
        {
     

			//Get the inventory contents
			var inventory = GetInventoryContents(player);

			// Check if the store exists
			if(inventoryList !=null)
			{
				//See if the player has an inventory stored here
				if(inventoryList.ContainsKey(player.Id))
				{
					//Remove the old record
					inventoryList.Remove(player.Id);
				}
				
				//Add the player's inventory record
				inventoryList.Add(player.Id,inventory);
			}

			Log("Saving Player Inventory Data for " + player.DisplayName);
			SaveInventoryData();

                
        }


       [ChatCommand("savedinv")]
       private void SavedInv(Player player, string cmd, string[] args)
        {

            if (args.Length != 1)
            {
                PrintToChat(player, "Invalid arguments");
                return;
            }
            
            var target = Server.GetPlayerByName(args[0]);
            if (target == null)
            {
                PrintToChat(player, "Invalid target");
                return;
            }

                {
             var contents = inventorySaveList[target.Id];       
            var content = "Inventory contents for '" + target.Name + "':\n";           
            content = contents.Count == 0 ? content + "No items found." : contents.Aggregate(content, (current, item) => current + $"{item[0]} x {item[1]}\n");       
             PrintToChat(player, content);                           
                        
                }
                   
            }

       [ChatCommand("lockoutinv")]
       private void LockOutInv(Player player, string cmd, string[] args)
        {

            if (args.Length != 1)
            {
                PrintToChat(player, "Invalid arguments");
                return;
            }
            
            var target = Server.GetPlayerByName(args[0]);
            if (target == null)
            {
                PrintToChat(player, "Invalid target");
                return;
            }

                {
             var contents = inventoryList[target.Id];       
            var content = "Inventory contents for '" + target.Name + "':\n";           
            content = contents.Count == 0 ? content + "No items found." : contents.Aggregate(content, (current, item) => current + $"{item[0]} x {item[1]}\n");       
             PrintToChat(player, content);                           
                        
                }
                   
            }
        #region Hooks

        private void SendHelpText(Player player)
        {

            {

               PrintToChat(player, GetMessage("HelpTextTitle", player.Id.ToString()));
               PrintToChat(player, GetMessage("HelpTextSaveYouInventory", player.Id.ToString()));

            }
            if (player.HasPermission("admin"))
            {
                PrintToChat(player, GetMessage("HelpTextSavedInventory", player.Id.ToString()));
                PrintToChat(player, GetMessage("HelpTextGiveBackPlayerSavedInventory", player.Id.ToString()));
                PrintToChat(player, GetMessage("HelpTextPlayerInventoryWhenLockOut", player.Id.ToString()));
                PrintToChat(player, GetMessage("HelpTextGivePlayerSavedInventoryWhenHeLockOut", player.Id.ToString()));
            }

        }

        #endregion


        #region Utility

        private string ConvertArrayToString(string[] args)
        {
            string name = args[0];
            if (args.Length > 1)
            {
                for (int i = 1; i < args.Length; i++)
                {
                    name = name + " " + args[i];
                }
            }
            return name;
        }

        //private T GetConfig<T>(string name, T defaultValue)
        //{
        //    if (Config[name] == null) return defaultValue;
        //    return (T)Convert.ChangeType(Config[name], typeof(T));
        //}

        string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion

       [ChatCommand("giveinv")]
		private void GiveInv(Player player, string cmd, string[] args)
        {
            if (!player.HasPermission("admin"))

            {                   
                PrintToChat(player, "You are not allowed to use this command.");
                return;
            }
            if (args.Length != 1)
            {
                PrintToChat(player, "Invalid arguments");
                return;
            }
            
            var target = Server.GetPlayerByName(args[0]);
            if (target == null)
            {
                PrintToChat(player, "Invalid target");
                return;
            }


            
			var inventoryIsWrong = false;
			
			// Check if the player has an inventory saved for InventoryProtection
			if(inventorySaveList.ContainsKey(target.Id))
			{
				// The player's current inventory contents
				var currentContents = GetInventoryContents(target);
				
				// The saved contents
				var savedContents = inventorySaveList[target.Id];
				
				for(var i=0; i<currentContents.Count;i++)
				{
					// If the resource is wrong
					if(currentContents[i][0] != savedContents[i][0] || currentContents[i][1] != savedContents[i][1])
					{
						inventoryIsWrong = true;
						break;
					}
				}
				if(inventoryIsWrong)
				{
					OverwritePlayerInventory(target, savedContents);

				}
				Log("Loading Player Inventory Data for " + target);

			}
			else Log("No inventory save found for InventoryProtection " + target);

        }

		
		private void OverwritePlayerInventory(Player target, Collection<string[]> savedContents)
		{
			EmptyPlayerInventory(target);
			GetItemsFromSavedInventory(target,savedContents);
                        PrintToChat(target, "[00BFFF]You Inventory its Back!");
		}
		
		private void GetItemsFromSavedInventory(Player target, Collection<string[]> savedContents)
		{
			var inventory = target.GetInventory();
			foreach(var savedItem in savedContents)
			{
				var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName(savedItem[0], true, true);
				var invGameItemStack = new InvGameItemStack(blueprintForName, Int32.Parse(savedItem[1]), null);
				ItemCollection.AutoMergeAdd(inventory.Contents, invGameItemStack);
			}
		}

		private void EmptyPlayerInventory(Player target)
		{
			var inventory = target.GetInventory().Contents;
			
			foreach (InvGameItemStack item in inventory.Where(item => item != null))
            {
				inventory.SplitItem(item, item.StackAmount, true);


            }

		}
		

		

		
        // public void RemoveItemsFromInventory(Player target, string resource, int amount)
        // {
            // var inventory = target.GetInventory().Contents;

            // // Check how much the target has
            // var amountRemaining = amount;
            // var removeAmount = amountRemaining;
            // foreach (InvGameItemStack item in inventory.Where(item => item != null))
            // {
                // if(item.Name == resource)
                // {
                    // removeAmount = amountRemaining;

                    // // Check if there is enough in the stack
                    // if (item.StackAmount < amountRemaining)
                    // {
                        // removeAmount = item.StackAmount;
                    // }

                    // amountRemaining = amountRemaining - removeAmount;

                    // inventory.SplitItem(item, removeAmount, true);
                    // if (amountRemaining <= 0) return;
                // }
            // }
        // }

        // private bool PlayerHasTheResources(Player target, string resource, string amountAsString)
        // {
            // // Convert the amount to int
            // var amount = Int32.Parse(amountAsString);

            // // Check player's inventory
            // var inventory = target.CurrentCharacter.Entity.GetContainerOfType(CollectionTypes.Inventory);

            // // Check how much the player has
            // var foundAmount = 0;
            // foreach (var item in inventory.Contents.Where(item => item != null))
            // {
                // if(item.Name == resource)
                // {
                    // foundAmount = foundAmount + item.StackAmount;
                // }
            // }

            // if(foundAmount >= amount) return true;
            // return false;

            
        // }

       [ChatCommand("givelockoutinv")]
		private void GiveLockOutInv(Player player, string cmd, string[] args)
        {
            if (!player.HasPermission("admin"))

            {                   
                PrintToChat(player, "You are not allowed to use this command.");
                return;
            }
            if (args.Length != 1)
            {
                PrintToChat(player, "Invalid arguments");
                return;
            }
            
            var target = Server.GetPlayerByName(args[0]);
            if (target == null)
            {
                PrintToChat(player, "Invalid target");
                return;
            }


            
			var inventoryIsWrong = false;
			
			// Check if the player has an inventory saved for InventoryProtection
			if(inventoryList.ContainsKey(target.Id))
			{
				// The player's current inventory contents
				var currentContents = GetInventoryContents(target);
				
				// The saved contents
				var savedContents = inventoryList[target.Id];
				
				for(var i=0; i<currentContents.Count;i++)
				{
					// If the resource is wrong
					if(currentContents[i][0] != savedContents[i][0] || currentContents[i][1] != savedContents[i][1])
					{
						inventoryIsWrong = true;
						break;
					}
				}
				if(inventoryIsWrong)
				{
					OverwritePlayerInventory(target, savedContents);

				}
				Log("Loading Player Inventory Data for " + target);

			}
			else Log("No inventory save found for InventoryProtection " + target);

        }

			

		
        // public void RemoveItemsFromInventory(Player target, string resource, int amount)
        // {
            // var inventory = target.GetInventory().Contents;

            // // Check how much the target has
            // var amountRemaining = amount;
            // var removeAmount = amountRemaining;
            // foreach (InvGameItemStack item in inventory.Where(item => item != null))
            // {
                // if(item.Name == resource)
                // {
                    // removeAmount = amountRemaining;

                    // // Check if there is enough in the stack
                    // if (item.StackAmount < amountRemaining)
                    // {
                        // removeAmount = item.StackAmount;
                    // }

                    // amountRemaining = amountRemaining - removeAmount;

                    // inventory.SplitItem(item, removeAmount, true);
                    // if (amountRemaining <= 0) return;
                // }
            // }
        // }

        // private bool PlayerHasTheResources(Player target, string resource, string amountAsString)
        // {
            // // Convert the amount to int
            // var amount = Int32.Parse(amountAsString);

            // // Check player's inventory
            // var inventory = target.CurrentCharacter.Entity.GetContainerOfType(CollectionTypes.Inventory);

            // // Check how much the player has
            // var foundAmount = 0;
            // foreach (var item in inventory.Contents.Where(item => item != null))
            // {
                // if(item.Name == resource)
                // {
                    // foundAmount = foundAmount + item.StackAmount;
                // }
            // }

            // if(foundAmount >= amount) return true;
            // return false;

            
        // }
	}
}