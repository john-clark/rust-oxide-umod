using Oxide.Core.Configuration;
using Rust;


namespace Oxide.Plugins
{

	///////////////////////////////////////////////////////////////////////////
	//
	//  Recycle Blueprints!
	//
	//  Allows players to recycle blueprints for scrap.  
	//  
	//	Set scrap yields for each category in RecycleBlueprints.json
	//  Using a value of zero will disable that category.
	//
	///////////////////////////////////////////////////////////////////////////

	
    [Info("Recycle Blueprints", "Zugzwang", "1.0.5")]
	[Description("Allows players to recycle blueprints for scrap.")]
    class RecycleBlueprints : RustPlugin
    {
		
		#region Config

		// Scrap ID
		int scrapID = -932201673; 
		
		// default scrap yields
		int common = 10;
		int uncommon = 25;		
		int rare = 100;
		int veryrare = 300;	
	
	
		// save scrap yields if file doesnt exist
		protected override void LoadDefaultConfig()
        {
			Config["Category_Common"] = common;
			Config["Category_Uncommon"] = uncommon;
			Config["Category_Rare"] = rare;
			Config["Category_VeryRare"] = veryrare;
			
			// prevent exploiting these 'rare' category blueprints
			// that only cost 75 in level 1 workbench experimentation
			Config["Custom_explosive.satchel"] = 75;
			Config["Custom_gates.external.high.wood"] = 75;
			Config["Custom_guntrap"] = 75;
			Config["Custom_ladder.wooden.wall"] = 75;
			Config["Custom_shotgun.double"] = 75;
			Config["Custom_wall.external.high"] = 75;
			
			PrintWarning("New configuration file created.");
        }
	
	
		// load scrap yields on startup
        void Init()
        {
			common = (int)Config["Category_Common"];
			uncommon = (int)Config["Category_Uncommon"];
			rare = (int)Config["Category_Rare"];
			veryrare = (int)Config["Category_VeryRare"];
        }

		
		
		void OnServerInitialized()
        {
			// Look for scrap ID on load, just in case they change it again...
			ItemDefinition scrap = ItemManager.FindItemDefinition("scrap");
			if (scrap?.itemid != null)
				scrapID = scrap.itemid;

			bool changed = false;
			var bpList = ItemManager.bpList;
			
			// Look for new blueprints	
			foreach (ItemBlueprint bp in bpList)
			{
				if (bp.defaultBlueprint || !(bp.isResearchable))
					continue;

				if (Config["Custom_" + bp.targetItem.shortname] == null)
				{
					Config["Custom_" + bp.targetItem.shortname] = -1;
					changed = true;
				}
			}
			
			if (changed)
			{				
				PrintWarning("Updating configuration file with new blueprints.");
				SaveConfig();				
			}

		}	
		
		
		#endregion Config

		
		
		
		
        #region Oxide Hooks 


		// allow recycling of enabled blueprint categories
		object CanRecycle(Recycler recycler, Item item)
		{
			if (item.IsBlueprint())
			{
				ItemDefinition target = ItemManager.FindItemDefinition(item.blueprintTarget);

				if (target?.rarity == null)
					return false;
				
				if ((int)Config["Custom_" + target.shortname] == 0)
					return false;

				if	((target.rarity == Rarity.Common && common > 0) ||
					 (target.rarity == Rarity.Uncommon && uncommon > 0) ||
					 (target.rarity == Rarity.Rare && rare > 0) ||
					 ((target.rarity == Rarity.VeryRare || target.rarity == Rarity.None) && veryrare > 0))
					return true;
				else
					return false;
			}
			else 
				return null;
		}
		
		
		// turn those blueprints into scrap
		void OnRecycleItem(Recycler recycler, Item item)
		{
			if (item.IsBlueprint())
			{
				int amount = 0;
			
				ItemDefinition target = ItemManager.FindItemDefinition(item.blueprintTarget);
				if (target == null) return;
				
				int custom = (int)Config["Custom_" + target.shortname];
				
				// set scrap amount based on custom setting, or rarity
				if (custom > 0)
					amount = custom;
				else if (target?.rarity == null)
					amount = 1;
				else if (target.rarity == Rarity.Common)
					amount = common;
				else if (target.rarity == Rarity.Uncommon)
					amount = uncommon;
				else if (target.rarity == Rarity.Rare)
					amount = rare;
				else if (target.rarity == Rarity.VeryRare || target.rarity == Rarity.None)
					amount = veryrare;

				
				// reward player with scrap
				if (amount > 0)
				{
					Item reward = ItemManager.CreateByItemID(scrapID, amount);
					if (reward != null)
					{
						recycler.MoveItemToOutput(reward);
						item.UseItem(1);
					}
				}
			}
		}
		

        #endregion
    }
}
