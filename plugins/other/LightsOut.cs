using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Libraries.Covalence;
 
namespace Oxide.Plugins
{
    [Info("LightsOut", "DylanSMR", "1.0.1", ResourceId = 2384)]
    [Description("A plugin to control all light sources.")]
 
    class LightsOut : CovalencePlugin
    {
		void Init(){
			permission.RegisterPermission("lightsout.admin", this);
			lang.RegisterMessages(new Dictionary<string, string>(){
				{"NoPerm", "You do not have the correct permissions to preform this command"},
				{"Lights", "{0} lights/ovens were turned off!"},
			}, this);
		}
		
		[Command("lightsout")]
		void LightsOutCMD(IPlayer player, string command, string[] args)
		{
			if(!player.HasPermission("lightsout.admin")){
				player.Reply(lang.GetMessage("NoPerm", this, player.Id));
				return;
			}
			player.Reply(string.Format(lang.GetMessage("Lights", this, player.Id), TurnOutLights()));
		}
		object TurnOutLights(){
			int r = 0;
			var ovens = BaseEntity.serverEntities.Where(x => x is BaseOven).ToList();
			for(int i = 0; i < ovens.Count; i++){
				var light = ovens[i] as BaseOven;
				if(light == null || !light.IsOn()) continue;
				light.SetFlag(BaseEntity.Flags.On, false); r++;
			}	
			return r;
		}
	}
}