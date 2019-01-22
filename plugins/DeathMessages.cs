using System;
using System.Collections.Generic;
using CodeHatch.Networking.Events.Entities;

namespace Oxide.Plugins
{
   [Info("Death Messages", "Ruby", "1.0.2")]
   [Description("Displays a server wide messages of the player deaths.")]
   public class DeathMessages : ReignOfKingsPlugin
    {
        #region Lang
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["DamageMessage"] = "{0} has killed {1} at {2}.",
                ["DamageDetails"] = "{0} to the {1} with a {2}.",
                ["DamageServer"] = "{0} was killed by a {1} at {2}.",
                ["ShortDmgDetails"] = "{0} to the {1}.",
                ["DamageType"] = "{0} has died from {1} at {2}."
        }, this);
        }
        #endregion

        void OnEntityDeath(EntityDeathEvent Event)
        {
            if (!Event.Entity.IsPlayer) return;

            #region Data
            var DamageType = Event.KillingDamage.DamageTypes.ToString();
            var DamageAmount = Event.KillingDamage.Amount.ToString();
            var HitBox = Event.KillingDamage.HitBoxBone.ToString();
            string Victim = Event.Entity.Owner.DisplayName;
            string Type = Event.KillingDamage.DamageTypes.ToString();
            string Amount = ($"{DamageAmount} damage");
            string Location = ($"{HitBox}");
            #endregion
			
			#region DeathLocations
			string posX = Event.Entity.Position.x + "";
			int indexX = posX.IndexOf(".");
			if (indexX > 0)
			{
				posX = posX.Substring(0, indexX);
			}
			string posZ = Event.Entity.Position.z + "";
			int indexZ = posZ.IndexOf(".");
			if (indexZ > 0)
			{
				posZ = posZ.Substring(0, indexZ);
			}
			string Pos = ($"[{posX} , {posZ}]");
			#endregion
			
			foreach (var player in covalence.Players.Connected)
			{
				#region DamageTypes
				if (DamageType.Contains("Impact"))
				{
					PrintToChat(lang.GetMessage("DamageType", this, player.Id), Victim, Type, Pos);
					return;
				}
				if (DamageType.Contains("Falling")) return;
				if (DamageType.Contains("Fire"))
				{
                    PrintToChat(lang.GetMessage("DamageType", this, player.Id), Victim, Type, Pos);
                    return;
				}
				if (DamageType.Contains("Drowning"))
				{
                    PrintToChat(lang.GetMessage("DamageType", this, player.Id), Victim, Type, Pos);
                    return;
				}
				if (DamageType.Contains("Plague"))
				{
                    PrintToChat(lang.GetMessage("DamageType", this, player.Id), Victim, Type, Pos);
                    return;
				}
				if (DamageType.Contains("Hunger"))
				{
                    PrintToChat(lang.GetMessage("DamageType", this, player.Id), Victim, Type, Pos);
                    return;
				}
				if (DamageType.Contains("Thirst"))
				{
                    PrintToChat(lang.GetMessage("DamageType", this, player.Id), Victim, Type, Pos);
                    return;
				}
				if (DamageType.Contains("Unknown"))
				{
                    PrintToChat(lang.GetMessage("DamageType", this, player.Id), Victim, Type, Pos);
                    return;
				}
				if (DamageType.Contains("OutOfBounds"))
				{
                    PrintToChat(lang.GetMessage("DamageType", this, player.Id), Victim, Type, Pos);
                    return;
				}
				#endregion

				#region Main
				bool Server = Event.KillingDamage.DamageSource.Owner.IsServer;
				bool Player = Event.KillingDamage.DamageSource.IsPlayer;
		   
				if (Player)
				{
					string Killer = Event.KillingDamage.DamageSource.Owner.DisplayName;
					string Damager = Event.KillingDamage.Damager.name;
					string Weapon = Damager.Replace("[Entity]", "");
					PrintToChat(lang.GetMessage("DamageMessage", this, player.Id), Killer, Victim, Pos);
					PrintToChat(lang.GetMessage("DamageDetails", this, player.Id), Amount, Location, Weapon);
					return;    
				}
				if (Server)
				{
					string Damage = Event.KillingDamage.DamageSource.name;
					string DamagerServer = Damage.Replace("[Entity]", "");
					PrintToChat(lang.GetMessage("DamageServer", this, player.Id), Victim, DamagerServer, Pos);
					PrintToChat(lang.GetMessage("ShortDmgDetails", this, player.Id), Amount, Location);
					return;
				}
				#endregion
			}
        }
      
    }
}