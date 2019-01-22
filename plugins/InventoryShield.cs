using System;
using System.Linq;
using CodeHatch.Engine.Networking;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CodeHatch.Common;
using CodeHatch.Permissions;
using Oxide.Core;
using CodeHatch.ItemContainer;



namespace Oxide.Plugins
{
    [Info("InventoryShield", "Shadow", "1.0.1")]
    public class InventoryShield : ReignOfKingsPlugin
    {
		private Dictionary<ulong,Collection<string[]>> inventorySaveList = new Dictionary<ulong,Collection<string[]>>();
		void Log(string msg) => Puts($"{Title} : {msg}");

		
        // SAVE DATA ===============================================================================================

        private void LoadInventoryData()
        {
            inventorySaveList = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong,Collection<string[]>>>("InventoryInfo");
        }
        private void SaveInventoryData()
        {
            Log("Saving PlayerInventory for InventoryProtection ");
            Interface.GetMod().DataFileSystem.WriteObject("InventoryInfo", inventorySaveList);
        }

        void Loaded()
        {
            inventorySaveList = new Dictionary<ulong,Collection<string[]>>();
            LoadInventoryData();
        }


        // ===========================================================================================================

		
       [ChatCommand("inv")]
       private void Inv(Player player, string cmd, string[] args)
        {
            if (!player.HasPermission("admin"))
            {                   
                PrintToChat(player, "You are not allowed to use this command.");
                return;
            }

            
            var target = Server.GetPlayerByName(args[0]);


            
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
		

		
		private Collection<string[]> GetInventoryContents(Player target)
		{
			var inventory = target.GetInventory().Contents;
			var inventoryContents = new Collection<string[]>();
			foreach (InvGameItemStack item in inventory.Where(item => item != null))
            {
				string[] tempStack = new string[]{ item.Name, item.StackAmount.ToString() };
				inventoryContents.Add(tempStack);
            }
			return inventoryContents;

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