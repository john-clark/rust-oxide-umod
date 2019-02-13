using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("GatherControl", "CaseMan", "1.6.0", ResourceId = 2477)]
    [Description("Control gather rates by day and night with permissions")]

    class GatherControl : RustPlugin
    {	
		#region Variables
	    [PluginReference]
        Plugin GUIAnnouncements;
		
		static GatherControl GC = null;
		bool IsDay;	
		bool UseZeroIndexForDefaultGroup;
		bool UseMessageBroadcast;
		bool UseGUIAnnouncements;
		bool AdminMode;
		string BannerColor;
		string TextColor;
		public Dictionary<ulong, int> Temp = new Dictionary<ulong, int>();
		float Sunrise;
		float Sunset;
		float DayRateMultStaticQuarry;
		float NightRateMultStaticQuarry;
		string PLPerm = "gathercontrol.AllowChatCommand";
		string AdmPerm = "gathercontrol.AllowConsoleCommand";
		string BypassPerm = "gathercontrol.bypass";

		class PermData
        {
            public Dictionary<int, PermGroups> PermissionsGroups = new Dictionary<int, PermGroups>();
            public PermData(){}
        }

        class PermGroups
        {
			public float DayRateMultQuarry;			
			public float DayRateMultPickup;			
            public float DayRateMultResource;			
			public float DayRateMultResourceBonus;			
			public float DayRateMultResourceHQM;
			public float DayRateMultCropGather;			
			public float NightRateMultQuarry;			
			public float NightRateMultPickup;			           
            public float NightRateMultResource;			
			public float NightRateMultResourceBonus;			
			public float NightRateMultResourceHQM;
			public float NightRateMultCropGather;			
			public Dictionary<string, string> CustomRateMultQuarry = new Dictionary<string, string>();
			public Dictionary<string, string> CustomRateMultPickup = new Dictionary<string, string>();
			public Dictionary<string, string> CustomRateMultResource = new Dictionary<string, string>();
			public Dictionary<string, string> CustomRateMultResourceBonus = new Dictionary<string, string>();
			public Dictionary<string, string> CustomRateMultCropGather = new Dictionary<string, string>();
			public Dictionary<string, string> ToolMultiplier = new Dictionary<string, string>();
			public string PermGroup;
			public PermGroups(){}
        }

		PermData permData;
		#endregion
		#region Initialization
		void Init()
        {
            LoadDefaultConfig();
			permData = Interface.Oxide.DataFileSystem.ReadObject<PermData>("GatherControl");
			LoadDefaultData();
			permission.RegisterPermission(PLPerm, this);
			permission.RegisterPermission(AdmPerm, this);
			permission.RegisterPermission(BypassPerm, this);
            foreach(var perm in permData.PermissionsGroups)
			{
                permission.RegisterPermission(perm.Value.PermGroup, this);
			}
			CheckPlayers();
        }
		void LoadDefaultData()
		{
			if(!permData.PermissionsGroups.ContainsKey(0))
			{
				var def = new PermGroups();
                def.DayRateMultQuarry = 2;
                def.DayRateMultPickup = 2;
                def.DayRateMultResource = 2;
				def.DayRateMultResourceBonus = 2;
				def.DayRateMultResourceHQM = 2;
				def.DayRateMultCropGather = 2;
                def.NightRateMultQuarry = 3;
                def.NightRateMultPickup = 3;
                def.NightRateMultResource = 3;
				def.NightRateMultResourceBonus = 3;
				def.NightRateMultResourceHQM = 3;
				def.NightRateMultCropGather =3;
				def.PermGroup = "gathercontrol.default";
				permData.PermissionsGroups.Add(0, def);
				Interface.Oxide.DataFileSystem.WriteObject("GatherControl", permData);
			}			
		}
		void OnPlayerInit(BasePlayer player)
		{
			CheckPlayer(player);
		}
		void OnPlayerDisconnected(BasePlayer player, string reason)
		{
			if(Temp.ContainsKey(player.userID)) Temp.Remove(player.userID);
		}

		#endregion
		#region Configuration
        protected override void LoadDefaultConfig()
        {
			Config["UseZeroIndexForDefaultGroup"] = UseZeroIndexForDefaultGroup = GetConfig("UseZeroIndexForDefaultGroup", true);
			Config["UseMessageBroadcast"] = UseMessageBroadcast = GetConfig("UseMessageBroadcast", true);
			Config["UseGUIAnnouncements"] = UseGUIAnnouncements = GetConfig("UseGUIAnnouncements", false);
			Config["BannerColor"] = BannerColor = GetConfig("BannerColor", "Blue");
			Config["TextColor"] = TextColor = GetConfig("TextColor", "Yellow");
			Config["Sunrise"] = Sunrise = GetConfig("Sunrise", 7);
			Config["Sunset"] = Sunset = GetConfig("Sunset", 19);
			Config["DayRateMultStaticQuarry"] = DayRateMultStaticQuarry = GetConfig("DayRateMultStaticQuarry", 1);
			Config["NightRateMultStaticQuarry"] = NightRateMultStaticQuarry = GetConfig("NightRateMultStaticQuarry", 1);
			Config["AdminMode"] = AdminMode = GetConfig("AdminMode", false);
			SaveConfig();
		}
		#endregion		
		#region Localization
		void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
				["SunriseMessage"] = "Morning comes. Production rating is lowered.",
				["SunsetMessage"] = "Night is coming. Production rating is increased.",
				["GatherRateInfo"] = "Your gather multipliers:",
				["NoGatherRate"] = "You do not have gather multipliers!",
				["GatherRateInfoPlayer"] = "Player {0} have gather multipliers:",
				["NoGatherRatePlayer"] = "Player {0} do not have gather multipliers!",
				["RateResource"] = "Resource gather multiplier (day/night)",
				["RateResourceBonus"] = "Resource gather multiplier for bonus from ore (day/night)",
				["RateResourceHQM"] = "Resource gather multiplier for HQM from ore (day/night)",
				["RatePickup"] = "Pickup multiplier (day/night)",
				["RateQuarry"] = "Multiplier of quarrying (day/night)",
				["RateCropGather"] = "Multiplier for your crop gather (day/night)",
				["InvalidSyntax"] = "Invalid syntax! Use: showrate <name/ID>",
				["NoPlayer"] = "Player not found!",
				["NoPermission"] = "You don't have the permission to use this command",
				["AdminMode"] = "This is <color=lime>{0}</color> gather type.\nName: <color=lime>{1}</color>.\nItem short name: <color=lime>{2}</color>",
            }, this);
			lang.RegisterMessages(new Dictionary<string, string>
            {
                ["SunriseMessage"] = "Наступает утро. Рейтинг добычи уменьшен.",
                ["SunsetMessage"] = "Наступает ночь. Рейтинг добычи увеличен.",
				["GatherRateInfo"] = "Ваши множители добычи ресурсов:",
				["NoGatherRate"] = "У вас нет множителей добычи!",
				["GatherRateInfoPlayer"] = "У игрока {0} есть следующие множители:",
				["NoGatherRatePlayer"] = "У игрока {0} нет множителей добычи!",
				["RateResource"] = "Множитель добычи ресурсов (день/ночь)",
				["RateResourceBonus"] = "Множитель добычи бонуса из руды (день/ночь)",
				["RateResourceHQM"] = "Множитель добычи МВК из руды (день/ночь)",
				["RatePickup"] = "Множитель поднятия предметов (день/ночь)",
				["RateQuarry"] = "Множитель добычи карьеров (день/ночь)",
				["RateCropGather"] = "Множитель сбора своего урожая (день/ночь)",
				["InvalidSyntax"] = "Неправильный синтаксис. Используйте: showrate <имя/ID>",
				["NoPlayer"] = "Игрок не найден!",
				["NoPermission"] = "У вас недостаточно прав для выполнения этой команды",
				["AdminMode"] = "This is <color=lime>{0}</color> gather type.\nName: <color=lime>{1}</color>.\nItem short name: <color=lime>{2}</color>",
            }, this, "ru");
        }
        #endregion
		#region Hooks
		private void CheckDay()
		{
			var time = TOD_Sky.Instance.Cycle.Hour;
			if(time >= Sunrise && time <= Sunset)
			{
				if(!IsDay && time>=Sunrise && time<=Sunrise + 0.1) MessageToAll("SunriseMessage");
				IsDay = true;	
			}
			else
			{
				if(IsDay && time>=Sunset && time<=Sunset + 0.1) MessageToAll("SunsetMessage");
				IsDay = false;
			}	
		}
		private void CheckPlayers()
		{
			foreach (var player in BasePlayer.activePlayerList) CheckPlayer(player);  
		}
		private void CheckPlayer(BasePlayer player)
		{
			if(player == null) return;
			int index=-1;
			if(Temp.ContainsKey(player.userID)) Temp.Remove(player.userID);
			if(permission.UserHasPermission(player.UserIDString, BypassPerm)) return;
			index = CheckPlayerPerm(player, index);
			if(index >= 0)Temp.Add(player.userID, index);
		}
		private int CheckPlayerPerm(BasePlayer player, int index)
		{
			foreach (var perm in permData.PermissionsGroups)
            {
                if (permission.UserHasPermission(player.UserIDString, perm.Value.PermGroup) && perm.Key>=index) index = perm.Key;          				  
            }
			if(index==-1 && UseZeroIndexForDefaultGroup) index = 0;
			return index;			
		}	
		private void CustomList(Item item, string str)
		{
			float day, night;
			ParseFromString(str, out day, out night);
			GatherMultiplier(item, day, night);
		}	
		private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {			
            BasePlayer player = entity.ToPlayer();
			if(player == null) return;
			if(AdminMode)
				if(player.IsAdmin) SendReply(player, (string.Format(lang.GetMessage("AdminMode", this), "RESOURCE", item.info.displayName.english, item.info.shortname)));	
			int gr = CheckPlayerPerms(player);
			if(gr >= 0)
			{		
				if(permData.PermissionsGroups[gr].ToolMultiplier.ContainsKey(player.GetActiveItem().info.shortname) && player.GetActiveItem() != null) CustomList(item, permData.PermissionsGroups[gr].ToolMultiplier[player.GetActiveItem().info.shortname]);
				else if(permData.PermissionsGroups[gr].CustomRateMultResource.ContainsKey(item.info.shortname)) CustomList(item, permData.PermissionsGroups[gr].CustomRateMultResource[item.info.shortname]);
				else GatherMultiplier(item, permData.PermissionsGroups[gr].DayRateMultResource, permData.PermissionsGroups[gr].NightRateMultResource);					
			}		
        }
		private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item) 
		{
			int gr = CheckPlayerPerms(player);
			if(gr >= 0)
			{
				if(permData.PermissionsGroups[gr].ToolMultiplier.ContainsKey(player.GetActiveItem().info.shortname) && player.GetActiveItem() != null) CustomList(item, permData.PermissionsGroups[gr].ToolMultiplier[player.GetActiveItem().info.shortname]);			
				else if(item.info.shortname=="hq.metal.ore") GatherMultiplier(item, permData.PermissionsGroups[gr].DayRateMultResourceHQM, permData.PermissionsGroups[gr].NightRateMultResourceHQM);
				else if(item.info.shortname!="hq.metal.ore" && permData.PermissionsGroups[gr].CustomRateMultResourceBonus.ContainsKey(item.info.shortname)) CustomList(item, permData.PermissionsGroups[gr].CustomRateMultResourceBonus[item.info.shortname]);
				else GatherMultiplier(item, permData.PermissionsGroups[gr].DayRateMultResourceBonus, permData.PermissionsGroups[gr].NightRateMultResourceBonus);	
			}		
		}		
		void OnCollectiblePickup(Item item, BasePlayer player)
		{
			if(player == null) return;
			if(AdminMode)
				if(player.IsAdmin) SendReply(player, (string.Format(lang.GetMessage("AdminMode", this), "PICKUP", item.info.displayName.english, item.info.shortname)));		
			int gr = CheckPlayerPerms(player);
			if(gr >= 0) 
			{
				if(permData.PermissionsGroups[gr].CustomRateMultPickup.ContainsKey(item.info.shortname)) CustomList(item, permData.PermissionsGroups[gr].CustomRateMultPickup[item.info.shortname]);
				else GatherMultiplier(item, permData.PermissionsGroups[gr].DayRateMultPickup, permData.PermissionsGroups[gr].NightRateMultPickup);
			}			
		}
		void OnQuarryGather(MiningQuarry quarry, Item item)
		{
			if(quarry.OwnerID == 0)
			{
				GatherMultiplier(item, DayRateMultStaticQuarry, NightRateMultStaticQuarry);
				return;				
			}
			int gr=-1;
			BasePlayer player = BasePlayer.FindByID(quarry.OwnerID);
            if(player == null)
			{
				BasePlayer player1 = BasePlayer.FindSleeping(quarry.OwnerID);
				if(player1 == null) return;
				gr = CheckPlayerPerm(player1, -1);
			}
			else
			{	
				gr = CheckPlayerPerms(player);
			}
			if(gr >= 0) 
			{
				if(permData.PermissionsGroups[gr].CustomRateMultQuarry.ContainsKey(item.info.shortname)) CustomList(item, permData.PermissionsGroups[gr].CustomRateMultQuarry[item.info.shortname]);
				else GatherMultiplier(item, permData.PermissionsGroups[gr].DayRateMultQuarry, permData.PermissionsGroups[gr].NightRateMultQuarry);
			}	
		}
		void OnCropGather(PlantEntity plant, Item item, BasePlayer player)
		{
			if(player == null) return;
			if(AdminMode)
				if(player.IsAdmin) SendReply(player, (string.Format(lang.GetMessage("AdminMode", this), "CROP GATHER", item.info.displayName.english, item.info.shortname)));
			int gr = CheckPlayerPerms(player);
			if(gr >= 0) 
			{	
				if(permData.PermissionsGroups[gr].CustomRateMultCropGather.ContainsKey(item.info.shortname)) CustomList(item, permData.PermissionsGroups[gr].CustomRateMultCropGather[item.info.shortname]);
				else GatherMultiplier(item, permData.PermissionsGroups[gr].DayRateMultCropGather, permData.PermissionsGroups[gr].NightRateMultCropGather);
			}	
		}
		private int CheckPlayerPerms(BasePlayer player)
        {			
           	if(Temp.ContainsKey(player.userID))
			{ 
				return Temp[player.userID];
			}        
			return -1;	
        }
		private void GatherMultiplier(Item item, float daymult, float nightmult) 
        {			
			if(IsDay) item.amount = (int)(item.amount * daymult); 
			else item.amount = (int)(item.amount * nightmult); 
        }
		void OnTick()
		{
			CheckDay();	
		}
		void OnUserPermissionGranted(string id, string permis)
		{
			OnChangePermsUser(id, permis);
		}
		void OnUserPermissionRevoked(string id, string permis)
		{
			OnChangePermsUser(id, permis);
		}
		void OnUserGroupAdded(string id, string name)
		{
			OnChangeUserGroup(id);
		}
		void OnUserGroupRemoved(string id, string name)
		{			
			OnChangeUserGroup(id);
		}
		void OnGroupPermissionGranted(string name, string permis)
		{
			OnChangePermsGroup(permis);
		}
		void OnGroupPermissionRevoked(string name, string permis)
		{
			OnChangePermsGroup(permis);
		}
		private void OnChangePermsGroup(string permis)
		{
			if(permis == BypassPerm)
			{
				CheckPlayers();
				return;	
			}	
			foreach(var perm in permData.PermissionsGroups)
			{
                if(perm.Value.PermGroup==permis) CheckPlayers();					
			}
		}	
		private void OnChangePermsUser(string id, string permis)
		{
			var player = BasePlayer.Find(id);
			if(player == null) return;
			if(permis == BypassPerm)
			{
				CheckPlayer(player);
				return;	
			}	
			foreach(var perm in permData.PermissionsGroups)
			{
                if(perm.Value.PermGroup==permis) CheckPlayer(player);					
			}
		}
		private void OnChangeUserGroup(string id)
		{
			var player = BasePlayer.Find(id);
			if(player == null) return;
			CheckPlayer(player);
		}
		#endregion
		#region Commands
        [ChatCommand("showrate")]
        void ShowRate(BasePlayer player, string command, string[] args)
        {
			if (!player.IsAdmin && !permission.UserHasPermission(player.userID.ToString(), PLPerm)) 
			{
				SendReply(player, lang.GetMessage("NoPermission", this, player.UserIDString));
                return;
			}
			int gr = CheckPlayerPerms(player);
			if(gr >= 0)	SendReply(player, lang.GetMessage("GatherRateInfo", this, player.UserIDString) + GatherInfo(player, gr));	
			else SendReply(player, lang.GetMessage("NoGatherRate", this, player.UserIDString)); 
		}
		[ConsoleCommand("showrate")]
        private void conShowRate(ConsoleSystem.Arg arg)
		{
			BasePlayer player0 = arg.Player();
			if (player0 is BasePlayer && arg.Connection != null && (arg.Connection.authLevel < 2 && !permission.UserHasPermission(player0.userID.ToString(), AdmPerm))) 
			{
				SendReply(arg, lang.GetMessage("NoPermission", this));
                return;
			}
			if (arg.Args == null || arg.Args.Length <= 0)
			{
                SendReply(arg, lang.GetMessage("InvalidSyntax", this));
                return;
            }
			BasePlayer player = BasePlayer.Find(arg.Args[0]) ?? BasePlayer.FindSleeping(arg.Args[0]);
			if(player == null)
			{
				SendReply(arg, (string.Format(lang.GetMessage("NoPlayer", this))));
				return;
			}	
			int gr = CheckPlayerPerm(player, -1);
			if(gr >= 0)	SendReply(arg, string.Format(lang.GetMessage("GatherRateInfoPlayer", this), player.displayName) + GatherInfo(player, gr));
			else SendReply(arg, string.Format(lang.GetMessage("NoGatherRatePlayer", this), player.displayName));               				          			
		}
		#endregion
		#region Helpers
		T GetConfig<T>(string name, T defaultValue) => Config[name] == null ? defaultValue : (T) Convert.ChangeType(Config[name], typeof(T)); 
		void MessageToAll(string key)
        {		
			foreach (var player in BasePlayer.activePlayerList)
			{
				if(permission.UserHasPermission(player.UserIDString, BypassPerm)) return;
				if(UseMessageBroadcast) SendReply(player, lang.GetMessage(key, this, player.UserIDString));
				if(GUIAnnouncements && UseGUIAnnouncements) GUIAnnouncements?.Call("CreateAnnouncement", lang.GetMessage(key, this, player.UserIDString), BannerColor, TextColor, player);
			}
        }
		private string GatherInfo(BasePlayer player, int gr)
		{
			string message = "";
			{
				message= string.Format("\n{0}: {6}/{12}\n{1}: {7}/{13}\n{2}: {8}/{14}\n{3}: {9}/{15}\n{4}: {10}/{16}\n{5}: {11}/{17}\n", 
					lang.GetMessage("RateResource", this, player.UserIDString),
					lang.GetMessage("RateResourceBonus", this, player.UserIDString),
					lang.GetMessage("RateResourceHQM", this, player.UserIDString),
					lang.GetMessage("RatePickup", this, player.UserIDString),
					lang.GetMessage("RateQuarry", this, player.UserIDString),
					lang.GetMessage("RateCropGather", this, player.UserIDString),	
					permData.PermissionsGroups[gr].DayRateMultResource,
					permData.PermissionsGroups[gr].DayRateMultResourceBonus,
					permData.PermissionsGroups[gr].DayRateMultResourceHQM,
					permData.PermissionsGroups[gr].DayRateMultPickup,
					permData.PermissionsGroups[gr].DayRateMultQuarry,
					permData.PermissionsGroups[gr].DayRateMultCropGather,				
					permData.PermissionsGroups[gr].NightRateMultResource,
					permData.PermissionsGroups[gr].NightRateMultResourceBonus,
					permData.PermissionsGroups[gr].NightRateMultResourceHQM,
					permData.PermissionsGroups[gr].NightRateMultPickup,
					permData.PermissionsGroups[gr].NightRateMultQuarry,
					permData.PermissionsGroups[gr].NightRateMultCropGather
					);
			}
			return message;	
		}	
		private void ParseFromString(string str, out float day, out float night)
		{
			string[] parts = str.Split('/');
			day = float.Parse(parts[0]);
			night = float.Parse(parts[1]);
		}
		#endregion
    }
}
