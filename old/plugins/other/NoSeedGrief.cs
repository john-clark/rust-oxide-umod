using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info( "NoSeedGrief", "mvrb", "1.0.2", ResourceId = 2260 )]
    class NoSeedGrief : RustPlugin
    {		
		void Init() => LoadDefaultMessages();
		
		void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages( new Dictionary<string, string>
            {
                ["CantPlantWithoutPrivilege"] = "You cannot place seeds without Building Privilege."
            }, this );
        }
		
		void OnEntityBuilt( Planner plan, GameObject go )
		{
			var player = plan.GetOwnerPlayer();
			
			if ( !player ) return;
			
			if ( player.CanBuild() ) return;
			
			if ( go.name.Contains( "hemp" ) )
			{
				go.GetComponent<BaseEntity>()?.KillMessage();
				player.inventory.GiveItem( ItemManager.CreateByPartialName( "seed.hemp" ) );
			}
			else if ( go.name.Contains( "corn" ) )
			{
				go.GetComponent<BaseEntity>()?.KillMessage();
				player.inventory.GiveItem( ItemManager.CreateByPartialName( "seed.corn" ) );
			}
			else if ( go.name.Contains( "pumpkin" ) )
			{
				go.GetComponent<BaseEntity>()?.KillMessage();
				player.inventory.GiveItem( ItemManager.CreateByPartialName( "seed.pumpkin" ) );
			}
			
			player.ChatMessage( Lang( "CantPlantWithoutPrivilege", player.UserIDString ) );
		}
		
		string Lang( string key, string id = null, params object[] args ) => string.Format( lang.GetMessage( key, this, id ), args );
	}
}