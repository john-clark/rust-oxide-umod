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
	[Info("AntiSleeperKiller", "Jonty", "1.0.0")]
    public class JontysSleeperPlugin : ReignOfKingsPlugin
    {
		void Loaded()
		{
			
		}
		
		private void OnEntityHealthChange(EntityDamageEvent e)
        {
            Player Damager = e.Damage.DamageSource.Owner;
            bool IsSleeper = e.Entity.name.ToString().Contains("Player Sleeper");

            if (e.Damage.Amount > 0 && IsSleeper)
            {
                PrintToChat("[SERVER] A sleeper was attacked by " + Damager.DisplayName + ", and they were kicked for it. Killing sleeping players is uncool, yo.");
                Server.Kick(Damager, "Attacking a sleeping player.");
            }
        }
    }
}