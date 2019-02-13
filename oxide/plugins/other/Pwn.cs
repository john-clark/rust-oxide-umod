using System;
using CodeHatch.Engine.Networking;
using CodeHatch.Permissions;
using CodeHatch.Blocks.Networking.Events;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.Common;

namespace Oxide.Plugins
{
    [Info("Pwn", "DumbleDora", "0.1")]
    public class Pwn : ReignOfKingsPlugin {
	
	public bool pwn = false;
	public float pwnDMG = 10000f;
	public float pwnRepair = -10000f;
		
		[ChatCommand("pwn")]
        private void WarTimeCommand(Player player, string cmd, string[] args){
			if (!player.HasPermission("admin")) return;
			
			if (!pwn){
				pwn = true;
				PrintToChat(player, "Pwn mode is now on");
				return;
			} 
			pwn = false;
			PrintToChat(player, "Pwn mode is now off");
        }
		
		[ChatCommand("checkpwn")]
        private void CheckWarTimeCommand(Player player, string cmd, string[] args){
			if (!player.HasPermission("admin")) return;
			
			if (pwn){
				PrintToChat(player, "Pwn mode is on");
				return;
			}
			PrintToChat(player, "Pwn mode is off");
			
        }
		
		private void OnEntityHealthChange(EntityDamageEvent damageEvent) {		
			if (!pwn) return;	
			if (damageEvent.Damage.DamageSource.Owner is Player){
			
				Player damager = damageEvent.Damage.DamageSource.Owner;				
				if (!damager.HasPermission("admin")) return;	
			
				//this is needed for some reason, otherwise all animals on server also deal pwnDMG
				if(damager.DisplayName == "Server") return;					
			
				
				if(damageEvent.Damage.Amount < 0) {
					damageEvent.Damage.Amount = pwnRepair;
					PrintToChat(damager, "PWN repair! healing " + pwnRepair.ToString() + " damage.");						
					return;
				}	
				PrintToChat(damager, "PWN hit! dealing " + pwnDMG.ToString() + " damage.");	
				damageEvent.Damage.Amount = pwnDMG;
			}			
						
		}
	
		void OnCubeTakeDamage(CubeDamageEvent e) {			 
			Player damager = e.Damage.DamageSource.Owner;
			
			if (!pwn) return; 			
			if (!damager.HasPermission("admin")) return;
			
			
			if(e.Damage.Amount < 0) {
				e.Damage.Amount = pwnRepair;
				PrintToChat(damager, "PWN repair! healing " + pwnRepair.ToString() + " damage.");
				return;
			}
			PrintToChat(damager, "PWN hit! dealing " + pwnDMG.ToString() + " damage.");	
			e.Damage.Amount = pwnDMG;						
        }
    }
}