using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System.Collections.ObjectModel;
using System.Timers;
using CodeHatch.Engine.Networking;
using CodeHatch.Engine.Core.Networking;
using CodeHatch.Thrones.SocialSystem;
using CodeHatch.Common;
using CodeHatch.Permissions;
using Oxide.Core;
using CodeHatch.Networking.Events;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.Networking.Events.Entities.Players;
using CodeHatch.Networking.Events.Players;
using CodeHatch.ItemContainer;
using CodeHatch.UserInterface.Dialogues;
using CodeHatch.Inventory.Blueprints.Components;


namespace Oxide.Plugins
{
    [Info("Bounty Tracker", "Scorpyon", "1.0.5")]
    public class BountyTracker : ReignOfKingsPlugin
    {
        // ===========================================================================================================
        // ===========================================================================================================
        
        // LIST OF COMMANDS :
        // /setbounty                           - Set up the bounty
        // /bounties                            - View all active bounties on players

        // /resetbounty (Admin only)            - Removes all bounties (active and in setup)

        // ===========================================================================================================
        // ===========================================================================================================
        
        private Collection<string[]> bountyList = new Collection<string[]>();
        void Log(string msg) => Puts($"{Title} : {msg}");

        // SAVE DATA ===============================================================================================

		private void LoadBountyData()
		{
            bountyList = Interface.GetMod().DataFileSystem.ReadObject<Collection<string[]>>("SavedBountyList");
		}

        private void SaveBountyListData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("SavedBountyList", bountyList);
        }
        
        void Loaded()
        {            
            LoadBountyData();
		}


        // ===========================================================================================================

		
        // SEE THE CURRENT BOUNTY LIST
        [ChatCommand("bounties")]
        private void ViewTheCurrentBounties(Player player, string cmd)
        {
            var title = "Active Bounties";
            var message = "";
			var maxBounties = 15;

            if(bountyList.Count <= 0)
            {
                message = "There are currently no bounties available.";
            }
            else
            {
				var maxItemsToShow = bountyList.Count;
				if(maxItemsToShow > maxBounties) maxItemsToShow = maxBounties;
                var count = 0;
                for(var i=0; i<maxItemsToShow;i++)
                {
                    if(bountyList[i][4] == "active") 
                    {
                        count++;
                        message = message + "[FF0000]" + Capitalise(bountyList[i][1]) + "[FFFFFF] - [00FF00]" + bountyList[i][2] + " " + bountyList[i][3] + "[FFFFFF]\n (Set by [FF00FF]" + Capitalise(bountyList[i][0]) + "[FFFFFF]) \n";
                    }
                }
                if(count == 0) message = "There are currently no bounties available.";
            }

            player.ShowPopup(title,message,"Ok",  (selection, dialogue, data) => ClosePopup(player, selection, dialogue, data));
			
			// Save the data
			SaveBountyListData();
        }
		
		private void ClosePopup(Player player, Options selection, Dialogue dialogue, object contextData)
		{
			//Do nothing
		}

		
		//SET THE BOUNTY
        [ChatCommand("setbounty")]
        private void SetTheFinalBountyOnThePlayer(Player player, string cmd)
        {
            var playerName = player.Name;

            // Open the new and shiny popup menu!
			player.ShowInputPopup("Set Bounty Details", "Who do you want to set a bounty on?", "", "Confirm", "Cancel", (options, dialogue1, data) => SetBountyPlayerName(player, options, dialogue1, data));
        }
        
        public void RemoveItemsFromInventory(Player player, string resource, int amount)
        {
            var inventory = player.GetInventory().Contents;

            // Check how much the player has
            var amountRemaining = amount;
            var removeAmount = amountRemaining;
            foreach (InvGameItemStack item in inventory.Where(item => item != null))
            {
                if(item.Name == resource)
                {
                    removeAmount = amountRemaining;

                    //Check if there is enough in the stack
                    if (item.StackAmount < amountRemaining)
                    {
                        removeAmount = item.StackAmount;
                    }

                    amountRemaining = amountRemaining - removeAmount;

                    inventory.SplitItem(item, removeAmount, true);
                    if (amountRemaining <= 0) return;
                }
            }
        }

        private void ConfirmTheBounty(Player player, Options selection, Dialogue dialogue, object contextData)
        {
			if (selection != Options.Yes)
            {
                PrintToChat(player, "[FF0000]Assassin's Guild[FFFFFF] : You have cancelled the bounty request.");
                return;
            }
			
			var playerName = player.Name;
			var guild = PlayerExtensions.GetGuild(player).Name;

            // Confirm the bounty in the list
            //var bountyDetails = new string[5];
            foreach(var bounty in bountyList)
            {
                if(bounty[0] == playerName.ToLower() && bounty[4] != "active")
                {
					string resource = bounty[2];
					string amountAsText = bounty[3];
					string targetName = bounty[1];
					
					// Make sure the player has enough resource for this!
					if(PlayerHasTheResources(player, resource, amountAsText) == false) 
					{
						PrintToChat(player, "[FF0000]Assassin's Guild[FFFFFF] : You do not have the resources for this bounty in your inventory!");
						return;
					}
					
					PrintToChat(player, "[FF0000]Assassin's Guild[FFFFFF] : Setting up bounty...");
					
					// Remove the resource
					int removeAmount = Int32.Parse(bounty[3]); 
					RemoveItemsFromInventory(player, bounty[2], removeAmount);
					
                    bounty[4] = "active";
					
					var message = "[FF0000]Assassin's Guild[FFFFFF] : [00FF00]" + playerName + "[FFFFFF] of [FF00FF]" + guild + "[FFFFFF] has set a bounty reward of [FF0000]" + amountAsText + " " + resource + "[FFFFFF] for the death of [00FF00]" + Capitalise(targetName) + "[FFFFFF]!";
					PrintToChat(message);

                }
            }

			
			// Save the data.
			SaveBountyListData();
        }
        
        [ChatCommand("resetbounty")]
        private void ResetAllBounties(Player player, string cmd)
        {
            if (!player.HasPermission("admin"))
            {
                PrintToChat(player, "Only an admin can reset all bounties.");
                return;
            }

            // Reset the list
            bountyList = new Collection<string[]>();
            
            PrintToChat(player, "[FF0000]Assassin's Guild[FFFFFF] : You have removed all bounties on all players.");

            // Save the data
            SaveBountyListData();
        }
        
        //SETTING A BOUNTY AMOUNT
        private void SetBountyAmountOfResource(Player player, Options selection, Dialogue dialogue, object contextData)
        {
			if (selection == Options.Cancel || dialogue.ValueMessage.Length == 0)
            {
                PrintToChat(player, "[FF0000]Assassin's Guild[FFFFFF] : You have cancelled the bounty request.");
                return;
            }
			
            var playerName = player.Name.ToLower();

            // Convert to a single string (in case of many args)
            var amountEntered = dialogue.ValueMessage;

            // Make sure that a Number was entered
            int amount;
            bool acceptable = Int32.TryParse(amountEntered, out amount);
            if(!acceptable)
            {
                PrintToChat(player, "[FF0000]Assassin's Guild[FFFFFF] : That amount was not recognised. The bounty request was cancelled.");
                return;
            }

            //If the number is too little or too much
            if(amount <= 0 || amount > 1000)
            {
                PrintToChat(player, "[FF0000]Assassin's Guild[FFFFFF] : The amount entered must be between 1 and 1000.");
                return;
            }
            
            // Check if I have already set an amiount
            foreach(var bounty in bountyList)
            {
                if(bounty[0] == playerName.ToLower())
                {
                    if(bounty[4] != "active")
                    {
                        // Add the resource to the existing bounty request
                        bounty[3] = amount.ToString();
                        PrintToChat(player, "[FF0000]Assassin's Guild[FFFFFF] : You have set the amount to [00FF00]" + amount.ToString() + "[FFFFFF] for the bounty you are creating.");
                        SaveBountyListData();
						
						// Load the next Popup!
						AskThePlayerToConfirmTheBounty(player, bounty[0], bounty[1], bounty[2], bounty[3]);

                        return;
                    }
                }
            }

            // Create a new bounty to add this resource
            bountyList.Add(CreateEmptyBountyListing());

            // Add the player's name to the bounty listing
            var lastRecord = bountyList.Count - 1;
            bountyList[lastRecord][0] = playerName.ToLower();

            // Add the amount
            bountyList[lastRecord][3] = amount.ToString();

            // Tell the player
             PrintToChat(player, "[FF0000]Assassin's Guild[FFFFFF] : You have set the amount to [00FF00]" + amount.ToString() + "[FFFFFF] for the bounty you are creating.");

            //// Save the data
            SaveBountyListData();

			// Load the next Popup!
			AskThePlayerToConfirmTheBounty(player, bountyList[lastRecord][0], bountyList[lastRecord][1], bountyList[lastRecord][2], bountyList[lastRecord][3]);

		}



        //SETTING A BOUNTY RESOURCE
        private void SetBountyResourceType(Player player, Options selection, Dialogue dialogue, object contextData)
        {
			if (selection == Options.Cancel || dialogue.ValueMessage.Length == 0)
            {
                PrintToChat(player, "[FF0000]Assassin's Guild[FFFFFF] : You have cancelled the bounty request.");
                return;
            }
			
            // Check who is setting the bounty
            var playerName = player.Name;

            // Get resource
            var resourceName = Capitalise(dialogue.ValueMessage);

            if(resourceName != "Wood" && 
                resourceName != "Stone" && 
                resourceName != "Iron" && 
                resourceName != "Flax" && 
                resourceName != "Iron Ingot" && 
                resourceName != "Steel Ingot" && 
                resourceName != "Water" && 
                resourceName != "Lumber" && 
                resourceName != "Wool" && 
                resourceName != "Bone" && 
                resourceName != "Sticks" && 
                resourceName != "Hay" && 
                resourceName != "Dirt" && 
                resourceName != "Clay" && 
                resourceName != "Oil")
            {
                PrintToChat(player, "[FF0000]Assassin's Guild[FFFFFF] : I am afraid that you cannot use that item as a bounty reward at this time. Only harvestable resources may be used.");
                return;
            }

            // Check if I have already set a resource
            foreach(var bounty in bountyList)
            {
                if(bounty[0] == playerName.ToLower())
                {
                    if(bounty[4] != "active")
                    {
                        // Add the resource to the existing bounty request
                        bounty[2] = resourceName;
                        PrintToChat(player, "[FF0000]Assassin's Guild[FFFFFF] : You have added the resource [00FF00]" + resourceName + "[FFFFFF] to the bounty you are creating.");
                        SaveBountyListData();

						// Load the next Popup!
						player.ShowInputPopup("Set Bounty Details", "How much of that resource are you offering as a reward?", "", "Confirm", "Cancel", (options, dialogue1, data) => SetBountyAmountOfResource(player, options, dialogue1, data));

                        return;
                    }
                }
            }

            // Create a new bounty to add this resource
            bountyList.Add(CreateEmptyBountyListing());

            // Add the player's name to the bounty listing
            var lastRecord = bountyList.Count - 1;
            bountyList[lastRecord][0] = playerName.ToLower();

            // Add the resource
            bountyList[lastRecord][2] = resourceName;

            // Tell the player
            PrintToChat(player, "[FF0000]Assassin's Guild[FFFFFF] : You have added the resource [00FF00]" + resourceName + "[FFFFFF] to the bounty you are creating.");

            // Save the data
            SaveBountyListData();
			
			// Load the next Popup!
			player.ShowInputPopup("Set Bounty Details", "How much of that resource are you offering as a reward?", "", "Confirm", "Cancel", (options, dialogue1, data) => SetBountyAmountOfResource(player, options, dialogue1, data));
        }



        // SETTING A BOUNTY NAME
        private void SetBountyPlayerName(Player player, Options selection, Dialogue dialogue, object contextData)
        {
			if (selection == Options.Cancel || dialogue.ValueMessage.Length == 0)
            {
                PrintToChat(player, "[FF0000]Assassin's Guild[FFFFFF] : You have cancelled the bounty request.");
                return;
            }
            var bountyPlayerName = dialogue.ValueMessage;

            // Check who is setting the bounty
            var playerName = player.Name;

            // Check that the bounty target is online
            Player bountyPlayer = Server.GetPlayerByName(bountyPlayerName.ToLower());

            //Check that this player can be found
            if (bountyPlayerName == "" || bountyPlayer == null)
            {
                PrintToChat(player, "[FF0000]Assassin's Guild[FFFFFF] : That person is not currently available. You must wait until they awaken to set a bounty on their head, my Lord.");
                return;
            }

            // Add the name to the listing
            foreach(var bounty in bountyList)
            {
//				PrintToChat(bounty[0] + " " + bounty[1] + " " + bounty[2] + " " + bounty[3]);
				if(bounty[0] == playerName.ToLower() && bounty[1].ToLower() == bountyPlayerName.ToLower() && bounty[4] == "active")
                {   
					PrintToChat(player, "[FF0000]Assassin's Guild[FFFFFF] : You already have an active bounty on this person's head!");
					return;
				}
                if(bounty[0] == playerName.ToLower() && bounty[4] != "active")
                {   
					//Add targets name here
					bounty[1] = bountyPlayerName.ToLower();

					// Tell the player
					PrintToChat(player, "[FF0000]Assassin's Guild[FFFFFF] : You have added [00FF00]" + Capitalise(bountyPlayerName) + "[FFFFFF]'s name to the bounty you are creating.");

					// Save the data
					SaveBountyListData();

					// Load the next Popup!
					player.ShowInputPopup("Set Bounty Details", "What resource are you offering as a reward?", "", "Confirm", "Cancel", (options, dialogue1, data) => SetBountyResourceType(player, options, dialogue1, data));
					
					return;
                }
            }

			
            // Create a new bounty listing
            bountyList.Add(CreateEmptyBountyListing());

            // Add the player's name to the bounty listing
            var lastRecord = bountyList.Count - 1;
            bountyList[lastRecord][0] = playerName.ToLower();

            // Add the target's name to the bounty
            bountyList[lastRecord][1] = bountyPlayerName.ToLower();

            // Tell the player
            PrintToChat(player, "[FF0000]Assassin's Guild[FFFFFF] : You have added [00FF00]" + Capitalise(bountyPlayerName) + "[FFFFFF]'s name to the bounty you are creating.");

            // Save the data
            SaveBountyListData();
			
			// Load the next Popup!
			player.ShowInputPopup("Set Bounty Details", "What resource are you offering as a reward?", "", "Confirm", "Cancel", (options, dialogue1, data) => SetBountyResourceType(player, options, dialogue1, data));
        }

        
        private string[] CreateEmptyBountyListing()
        {
            string[] newBounty = new string[5] { "","","","","" };
            return newBounty;
        }

        private string ConvertArrayToString(string[] textArray)
        {
            var newText = textArray[0];
            if (textArray.Length > 1)
            {
                for (var i = 1; i < textArray.Length; i++)
                {
                    newText = newText + " " + textArray[i];
                }
            }
            return newText;
        }
		
		
		// THIS CONTROLS WHEN A PLAYER IS KILLED
		private void OnEntityDeath(EntityDeathEvent deathEvent)
        {
            if (deathEvent.Entity.IsPlayer)
            {
                var killer = deathEvent.KillingDamage.DamageSource.Owner;
                var player = deathEvent.Entity.Owner;
				
				// Check for bounties
				var reward = GetBountyOnPlayer(player);
				if (reward.Count < 1) return;
				
				// Make sure the player is not in the same guild
				if(player.GetGuild().Name == killer.GetGuild().Name)
				{
					PrintToChat("[FF0000]Assassin's Guild[FFFFFF] : [00FF00]" + player.DisplayName + "[FFFFFF] was slain by a member of the same guild, so no bounty was collected!");
					return;
				}
				
				// Get the inventory
				var inventory = killer.GetInventory();
				
				// Give the rewards to the player
				foreach(var bounty in reward)
				{
					var resource = bounty[0];
					var amount = Int32.Parse(bounty[1]);
					PrintToChat("Reward = " + resource + " " + amount.ToString());
					// Create a blueprint
					var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName(resource, true, true);
					// Create item stack
					var invGameItemStack = new InvGameItemStack(blueprintForName, amount, null);
					// Add the reward to the inventory
					ItemCollection.AutoMergeAdd(inventory.Contents, invGameItemStack);
				}
				
				// Notify everyone
				PrintToChat("[FF0000]Assassin's Guild[FFFFFF] : [00FF00]" + killer.DisplayName + "[FFFFFF] has ended [FF00FF]" + player.DisplayName + "[FFFFFF]'s life and has secured the bounty on their head!");
            }
        }
		
		private Collection<Collection<string>> GetBountyOnPlayer(Player player)
		{
			var reward = new Collection<Collection<string>>();
			
			for(var i=0; i<bountyList.Count; i++)
			{
				if(bountyList[i][1].ToLower() == player.Name.ToLower() && bountyList[i][4] == "active")
				{
					// Add this bounty to the list of rewards
					var bountyReward = new Collection<string>();
					bountyReward.Add(bountyList[i][2]);
					bountyReward.Add(bountyList[i][3].ToString());
					reward.Add(bountyReward);
					
					// remove this bounty from the list
					bountyList.RemoveAt(i);
					i--;
				}
			}
			
			// Save the data
			SaveBountyListData();
			
			return reward;
		}
		
		// Capitalise the Starting letters
		private string Capitalise(string word)
		{
			string finalText = "";
			finalText = Char.ToUpper(word[0]).ToString();
			var spaceFound = 0;
			for(var i=1; i<word.Length;i++)
			{
				if(word[i] == ' ')
				{
					spaceFound = i + 1;
				}
				if(i == spaceFound)
				{
					finalText = finalText + Char.ToUpper(word[i]).ToString();
				}
				else finalText = finalText + word[i].ToString();
			}
			return (string)finalText;
		}
		
		
        private bool PlayerHasTheResources(Player player, string resource, string amountAsString)
        {
            // Convert the amount to int
            int amount = Int32.Parse(amountAsString);

            // Check player's inventory
            var inventory = player.CurrentCharacter.Entity.GetContainerOfType(CollectionTypes.Inventory);

            // Check how much the player has
            int foundAmount = 0;
            foreach (var item in inventory.Contents.Where(item => item != null))
            {
                if(item.Name.ToLower() == resource.ToLower())
                {
                    foundAmount = foundAmount + item.StackAmount;
                }
            }

            if(foundAmount >= amount) return true;
            return false;
            
        }

        private void AskThePlayerToConfirmTheBounty(Player player, string playerName, string bountyName, string bountyResource, string bountyAmount)
        {

			// Load the next Popup!
			player.ShowConfirmPopup("Set Bounty Details", "[FFFFFF]You have set a bounty reward of [FF0000]" + bountyAmount + " " + bountyResource + "[FFFFFF] for the death of [00FF00]" + Capitalise(bountyName) + "[FFFFFF]! Confirm the bounty?", "Make it so!", "Actually, no!", (selection, dialogue, data) => ConfirmTheBounty(player, selection, dialogue, data));
        }


	}
}