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
using CodeHatch.Engine.Core.Cache;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Sleeper", "A RoK Player", "0.2")]
    public class Sleeper : ReignOfKingsPlugin {
	
		#region Configuration Data
		
		string chatPrefix = "Sleeper";
		string message;
		#endregion
		
		
		
	
		private void OnEntityHealthChange(EntityDamageEvent damageEvent) {
				if (damageEvent.Damage.Amount > 0) {
						bool sleepingplayer = damageEvent.Entity.name.ToString().Contains("Player Sleeper");

								if (sleepingplayer)
								{
									damageEvent.Cancel("No damage to sleeping player");
									damageEvent.Damage.Amount = 0f;                        
									PrintToChat(damageEvent.Damage.DamageSource.Owner, "[FF0000]You can't attack a sleeping player.");
								}

						
                }
   
				}
			}
		}