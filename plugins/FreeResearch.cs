using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Configuration;
using System;
using System.Linq;

namespace Oxide.Plugins
{

	///////////////////////////////////////////////////////////////////////////
	//
	//  Free Research and Experimentation!
	//
	//  Puts scrap into ResearchTables and Workbenches on spawn,
	//  and hopefully prevents the players from stealing it.
	//
	///////////////////////////////////////////////////////////////////////////

	
    [Info("Free Research", "Zugzwang", "1.0.7")]
	[Description("Makes blueprint research and workbench experiments free.")]
    class FreeResearch : RustPlugin
    {

		// Scrap
		int scrapID = -932201673; 

		
        #region Oxide Hooks 

				
		// Check and/or fill ResearchTable and Workbench with scrap on load.
		void OnServerInitialized()
        {

			// just in case devs change the scrap ID again...
			if (ItemManager.FindItemDefinition("scrap")?.itemid != null)
				scrapID = ItemManager.FindItemDefinition("scrap").itemid;
			
			foreach (ResearchTable z in BaseNetworkable.serverEntities.Where(x => x is ResearchTable))
			{
				Item tuition = z.inventory.GetSlot(1);

				// changed to 777 because we can't refund scrap to a locked empty slot
				if (tuition != null) 
					tuition.amount = 777;
				else
				{
					tuition = ItemManager.CreateByItemID(scrapID, 777);
					if (tuition == null) continue;
					NextFrame( () => {	tuition.MoveToContainer(z.inventory, 1, true);	});
				}				
			}
			
			foreach (Workbench z in BaseNetworkable.serverEntities.Where(x => x is Workbench))
			{
				Item tuition = z.inventory.GetSlot(1);

				if (tuition != null) 
					tuition.amount = z.GetScrapForExperiment();
				else
				{
					tuition = ItemManager.CreateByItemID(scrapID, z.GetScrapForExperiment());
					if (tuition == null) continue;
					NextFrame( () => {	tuition.MoveToContainer(z.inventory, 1, true);	});
				}
			}
			
        }

		
		// Fill ResearchTable and Workbench with scrap on spawn.
		void OnEntitySpawned(BaseNetworkable entity)
        {
			if (entity is ResearchTable)
			{
				ResearchTable table = entity as ResearchTable;
				Item tuition = ItemManager.CreateByItemID(scrapID, 777);
				if (tuition != null)
					tuition.MoveToContainer(table.inventory, 1, true);
			} 
			else if (entity is Workbench)
			{
				Workbench bench = entity as Workbench;
				Item tuition = ItemManager.CreateByItemID(scrapID, bench.GetScrapForExperiment());
				if (tuition != null)
					tuition.MoveToContainer(bench.inventory, 1, true);
			}
		}
		
		
		// Research is instant.
		void OnItemResearch(ResearchTable table, Item item, BasePlayer player)
		{
			table.researchDuration = 0;
        }

		
		// Scrap is refunded; free workbench experments.
		void OnItemUse(Item item, int amountToUse)
		{
			if (item.info.itemid == scrapID)
				item.amount += amountToUse;
		}

		
		// Prevent dropping whole stacks of pre-loaded scrap.
		void OnItemRemovedFromContainer(ItemContainer container, Item item)
		{
			if (item?.info?.itemid != scrapID || container?.entityOwner == null)
				return;

			if (container.entityOwner is Workbench || container.entityOwner is ResearchTable)
				NextFrame(() => { item.MoveToContainer(container, 1, true); });
		}

		
		// Prevent right click dragging single scrap,
		// and middle click dragging half stacks of scrap.
		Item OnItemSplit(Item item, int amount)
		{
			if (item?.parent?.entityOwner is Workbench || item?.parent?.entityOwner is ResearchTable)
				if (item.position == 1) return item;

			return null;
		}
		

		// Prevent moving scrap in or out of tables and benches.
        private object CanMoveItem(Item item, PlayerInventory playerLoot, uint targetContainerId, int targetSlot)
        {
			if (item?.info?.itemid != scrapID)
				return null;
		
			if (item?.parent?.entityOwner is Workbench || item?.parent?.entityOwner is ResearchTable)
				return false;

			ItemContainer targetContainer = playerLoot?.FindContainer(targetContainerId);
			
			if (targetContainer?.entityOwner is Workbench || targetContainer?.entityOwner is ResearchTable)
				return false;

			return null;
		}
		
        #endregion
    }
}
