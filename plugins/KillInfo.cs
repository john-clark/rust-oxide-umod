using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{

    [Info("Kill Info", "birthdates", "1.0", ResourceId = 0)]
	[Description("Kill and Wound Info")]
    public class KillInfo : RustPlugin
    {
		private bool Wounded_Info;
		private bool Killed_Info;
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating a new config...");
            Config["Wounded_Info"] = true;
            Config["Killed_Info"] = true;
        }

		void Init() {
			Wounded_Info = (bool)Config["Wounded_Info"];
			Killed_Info = (bool)Config["Killed_Info"];
		}

        protected override void LoadDefaultMessages() => lang.RegisterMessages(new Dictionary<string, string>
        {
                {"Wounded_Info","Wounded - You have wounded {0}."},
                {"Killed_Info", "Killed - You have killed {0} with a hit to the {1} and did a finishing damage of {2}."},
            }, this);

        void OnPlayerWound(BasePlayer player)
        {
           

            if (player != null)
            {
                
                if(player.lastAttacker != null) {
					BasePlayer attacker = player.lastAttacker.ToPlayer();
					if (Wounded_Info && attacker != null)
					{
						SendReply(attacker, lang.GetMessage("Wounded_Info",this,player.UserIDString),player.displayName);
					}
				}


            }
        }

        void OnPlayerDie(BasePlayer player, HitInfo info)
        {
            BasePlayer attacker = null;
            if(player.lastAttacker != null) {
			    attacker = player.lastAttacker.ToPlayer();
			}
            
            if(Killed_Info && attacker != null)
            {
                SendReply(attacker, lang.GetMessage("Killed_Info", this,player.UserIDString), player.displayName,info.boneName,Math.Round(info.damageTypes.Total()));
            }


        }
    }
}