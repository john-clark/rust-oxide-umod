using System;
using System.Text;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Where's My Corpse", "Fuji/LeoCurtss", "0.6.2", ResourceId = 1777)]
    [Description("Points a player to their corpse when they type a command.")]

    class WheresMyCorpse : RustPlugin
    {
		
        void Loaded()
        {
            LoadData();
			
			permission.RegisterPermission("wheresmycorpse.canuse", this);
			
			//Lang API dictionary
			lang.RegisterMessages(new Dictionary<string,string>{
				["WMC_NoData"] = "No data was found on your last death.  The WheresMyCorpse plugin may have been reloaded or you have not died yet.",
				["WMC_LastSeen"] = "Your corpse was last seen {0} meters from here.",
				["WMC_LastSeenDirection"] = "Your corpse was last seen <color=yellow>{0}m</color> away in direction <color=yellow>{1}</color>.",
				["WMC_Dir_North"] = "North",
				["WMC_Dir_NorthEast"] = "NorthEast",
				["WMC_Dir_East"] = "East",
				["WMC_Dir_SouthEast"] = "SouthEast",
				["WMC_Dir_South"] = "South",
				["WMC_Dir_SouthWest"] = "SouthWest",
				["WMC_Dir_West"] = "West",
				["WMC_Dir_NorthWest"] = "NorthWest"
				
			}, this);
        }
		
		private string GetMessage(string name, string sid = null) {
			return lang.GetMessage(name, this, sid);
		}
		
		Dictionary<string, string> deathInfo = new Dictionary<string, string>();

		void LoadData()
		{
			deathInfo = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<string, string>>("WheresMyCorpse");
		}

		void SaveData()
		{
			Interface.GetMod().DataFileSystem.WriteObject("WheresMyCorpse", deathInfo);
		}

		void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
		{
			
			if (entity.name.Contains("player.prefab"))
			{
				var player = entity as BasePlayer;
				string UserID = player.UserIDString;
				if (!IsSteamId(Convert.ToUInt64(UserID)))
					return;
				string DeathPosition = entity.transform.position.ToString("0");
				
				Puts("Player death info: " + UserID + " at " + DeathPosition);
				
				LoadData();
				
				string value;
				
				if (deathInfo.TryGetValue(UserID, out value))
				{
					deathInfo[UserID] = DeathPosition;
					SaveData();
				}
				else
				{
					deathInfo.Add(UserID,DeathPosition);
					SaveData();
				}
			}
		}
		
		string GetDirectionAngle(float angle, string UserIDString)
		{
			if (angle > 337.5 || angle < 22.5)
				return lang.GetMessage("WMC_Dir_North", this, UserIDString);
			else if (angle > 22.5 && angle < 67.5)
				return lang.GetMessage("WMC_Dir_NorthEast", this, UserIDString);
			else if (angle > 67.5 && angle < 112.5)
				return lang.GetMessage("WMC_Dir_East", this, UserIDString);
			else if (angle > 112.5 && angle < 157.5)
				return lang.GetMessage("WMC_Dir_SouthEast", this, UserIDString);
			else if (angle > 157.5 && angle < 202.5)
				return lang.GetMessage("WMC_Dir_South", this, UserIDString);
			else if (angle > 202.5 && angle < 247.5)
				return lang.GetMessage("WMC_Dir_SouthWest", this, UserIDString);
			else if (angle > 247.5 && angle < 292.5)
				return lang.GetMessage("WMC_Dir_West", this, UserIDString);
			else if (angle > 292.5 && angle < 337.5)
				return lang.GetMessage("WMC_Dir_NorthWest", this, UserIDString);
			return "";
		}
		
		void OnPlayerRespawned(BasePlayer player)
		{
            if (permission.UserHasPermission(player.userID.ToString(), "wheresmycorpse.canuse"))
			{
				if (deathInfo.ContainsKey(player.UserIDString))
				{
					Vector3 lastDeathPosition = getVector3(deathInfo[player.UserIDString]);
					Vector3 currentPosition = player.transform.position;
					SendReply(player,string.Format(GetMessage("WMC_LastSeenDirection",player.UserIDString),(int)Vector3.Distance(player.transform.position, lastDeathPosition), GetDirectionAngle(Quaternion.LookRotation((lastDeathPosition - player.eyes.position).normalized).eulerAngles.y, player.UserIDString)  ));
				}
				else
					SendReply(player,GetMessage("WMC_DirNoData",player.UserIDString));
			}
		}
		
		[ChatCommand("where")]
        void TestCommand(BasePlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.userID.ToString(), "wheresmycorpse.canuse"))
			{
				if (deathInfo.ContainsKey(player.UserIDString))
				{
					Vector3 lastDeathPosition = getVector3(deathInfo[player.UserIDString]);
					Vector3 currentPosition = player.transform.position;
					SendReply(player,string.Format(GetMessage("WMC_LastSeenDirection",player.UserIDString),(int)Vector3.Distance(player.transform.position, lastDeathPosition), GetDirectionAngle(Quaternion.LookRotation((lastDeathPosition - player.eyes.position).normalized).eulerAngles.y, player.UserIDString)  ));
				}
				else
					SendReply(player,GetMessage("WMC_NoData",player.UserIDString));
			}
			else
				SendReply(player,GetMessage("WMC_NoPermission",player.UserIDString));
        }
		
		bool IsSteamId(ulong id)
		{
			return id > 70000000000000000uL;
		}
		
		public Vector3 getVector3(string rString){
			string[] temp = rString.Substring(1,rString.Length-2).Split(',');
			float x = float.Parse(temp[0]);
			float y = float.Parse(temp[1]);
			float z = float.Parse(temp[2]);
			Vector3 rValue = new Vector3(x,y,z);
			return rValue;
		}
    }
}