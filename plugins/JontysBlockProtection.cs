using System;
using System.Collections.Generic;
using CodeHatch.Build;
using CodeHatch.Engine.Networking;
using CodeHatch.Engine.Core.Networking;
using CodeHatch.Blocks;
using CodeHatch.Blocks.Networking.Events;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.Networking.Events.Entities.Players;
using CodeHatch.Networking.Events.Players;
using CodeHatch.Networking.Events.Social;


namespace Oxide.Plugins
{
	[Info("SimpleBlockProtection", "Jonty", "1.0.0")]
    public class JontysBlockProtection : ReignOfKingsPlugin
    {
		void Loaded()
		{
			
		}
		
		private void OnCubeTakeDamage(CubeDamageEvent Event)
        {
            Player BlockOwner = Event.Entity.Owner;
            Player Me = Event.Damage.DamageSource.Owner;

            if (Me != BlockOwner)
            {
                return;
            }
        }
    }
}