using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SurveyBlocker", "miRror", "1.0.1")]
	
    class SurveyBlocker : RustPlugin
    {
		private float cooldownTime = 20;
		private uint surveyID = 2141863453;	
		
		private Dictionary<ulong, float> p = new Dictionary<ulong, float>();
		
		private void Init()
		{
			Dictionary<string, Dictionary<string, string>> compiledLangs = new Dictionary<string, Dictionary<string, string>>();
			
			foreach(var line in messages)
			{
				foreach(var translate in line.Value)
				{
					if(!compiledLangs.ContainsKey(translate.Key))
						compiledLangs[translate.Key] = new Dictionary<string, string>();
					
					compiledLangs[translate.Key][line.Key] = translate.Value;
				}				
			}
			
			foreach(var cLangs in compiledLangs)
			{
				lang.RegisterMessages(cLangs.Value, this, cLangs.Key);
			}
		}
		
		private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
		{
			if(info.WeaponPrefab == null || info.WeaponPrefab.prefabID != surveyID)
				return null;
			
			BasePlayer attacker = info.InitiatorPlayer;
			
			if(attacker != null)
			{
				if(!p.ContainsKey(attacker.userID) || p[attacker.userID] < Time.realtimeSinceStartup)
					SendReply(attacker, Lang("USE_SURVEY", attacker.UserIDString));
				
				p[attacker.userID] = Time.realtimeSinceStartup + cooldownTime;
			}
			
			return false;
		}

		private void OnPlayerDisconnected(BasePlayer player) => p.Remove(player.userID);
		
		private string Lang(string key, string userID = null, params object[] args) => string.Format(lang.GetMessage(key, this, userID), args);
		
  		private readonly Dictionary<string, Dictionary<string, string>> messages = new Dictionary<string, Dictionary<string, string>> 
		{
            {"USE_SURVEY", new Dictionary<string, string>() {
				{"ru", "Урон от геологических зарядов <color=orange>отключен</color>"},
				{"en", "Damage for survey charges <color=orange>disabled</color>"}
			}}
		};
	}
}