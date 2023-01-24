using UnityEngine;
using System;
using CodeHatch.Engine.Modules.SocialSystem;
using CodeHatch.Engine.Networking;
using CodeHatch.Thrones.SocialSystem;
using CodeHatch.Blocks.Networking.Events;
using CodeHatch.Common;
using CodeHatch.Damaging;
using Oxide.Core;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Oxide.Plugins{
    [Info("RaidOffDefender", "PierreA", "1.0.1")]
    public class RaidOffDefender : ReignOfKingsPlugin{
		
		#region Configuration Data
		private bool raidOnlineProtection = false;
		private bool raidOfflineProtection = true;
		private bool isProtectionActive;
		private bool killOrKick = true;
		private CrestScheme crestScheme = SocialAPI.Get<CrestScheme>();
		private Collection<PlayerInfos> _PlayersInfos = new Collection<PlayerInfos>();
		private Collection<TimeRanges> _TimeRanges = new Collection<TimeRanges>();
		private int lastWarning;
		private List<string> _listDay = new List<string>{
			"Mon",
			"Tue",
			"Wed",
			"Thu",
			"Fri",
			"Sat",
			"Sun"
		};
		#endregion
		
		#region classes
		private class PlayerInfos{
			public ulong playerId;
			public string playerName;
			public string guildName;
		}
		
		private class TimeRanges{
			public string dayOfWeek;
			public int startHoure;
			public int stopHoure;
		}
		#endregion
		
		#region Config save/load	
		private void Loaded()
        {
			raidOnlineProtection = GetConfig("raidOnlineProtection", false);
			raidOfflineProtection = GetConfig("raidOfflineProtection", true);
			killOrKick = GetConfig("killOrKick", true); // true = kick
            LoadDefaultMessages();
			LoadData();
			setUpTimerUpdatePlayersData();
			setUpTimerCheckProtectionStatus();
			isProtectionActive = isInATimeRange();
        }
		protected override void LoadDefaultConfig()
        {
            Config["raidOnlineProtection"] = raidOnlineProtection;
            Config["raidOfflineProtection"] = raidOfflineProtection;
            Config["killOrKick"] = killOrKick;
            SaveConfig();
        }
		
		private void LoadData(){
			_PlayersInfos = Interface.GetMod().DataFileSystem.ReadObject<Collection<PlayerInfos>>("SavedPlayersInfos");
			_TimeRanges = Interface.GetMod().DataFileSystem.ReadObject<Collection<TimeRanges>>("TimeRanges");
		}
		
		private void SaveData(){
			Interface.GetMod().DataFileSystem.WriteObject("SavedPlayersInfos", _PlayersInfos);
			Interface.GetMod().DataFileSystem.WriteObject("TimeRanges", _TimeRanges);
		}
		
		private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
				{ "RaidOnActivated", "Protection raidOn activée!" },
				{ "RaidOnDeactivated", "Protection raidOn désactivée!" },
				{ "RaidOffActivated", "Protection raidOff activée!" },
				{ "RaidOffDeactivated", "Protection raidOff désactivée!" },
				{ "RaidOffVictimKill", "[0080FF]{0} [FFFFFF] fait du [0080FF]raidOff [FFFFFF]avec un [0080FF]{1}[FFFFFF] sur la base de [0080FF]{2}[FFFFFF], sa punition est la mort!" },
				{ "RaidOffVictimKick", "[0080FF]{0} [FFFFFF] fait du [0080FF]raidOff [FFFFFF]avec un [0080FF]{1}[FFFFFF] sur la base de [0080FF]{2}[FFFFFF], au revoir!" },
				{ "raidOnlineKill", "[0080FF]{0} [FFFFFF]fait du [0080FF]raidOn [FFFFFF]avec un [0080FF]{1}[FFFFFF] sur la base de [0080FF]{2}[FFFFFF], sa punition est la mort!" },
				{ "raidOnlineKick", "[0080FF]{0} [FFFFFF]fait du [0080FF]raidOn [FFFFFF]avec un [0080FF]{1}[FFFFFF] sur la base de [0080FF]{2}[FFFFFF], au revoir!" },
				{ "RaidOffUnknownVictimKill", "[0080FF]{0} [FFFFFF] fait du [0080FF]raidOff [FFFFFF]avec un [0080FF]{1}[FFFFFF], sa punition est la mort!" },
				{ "RaidOffUnknownVictimKick", "[0080FF]{0} [FFFFFF] fait du [0080FF]raidOff [FFFFFF]avec un [0080FF]{1}[FFFFFF], au revoir!" },
				{ "BaseUnderAttack", "[FFFFFF]La base de [0080FF]{0} [FFFFFF]est attaquée!" },
				{ "KillActivated", "[FFFFFF]Les joueurs seront dorénavant tués en tant que punition!" },
				{ "KickActivated", "[FFFFFF]Les joueurs seront dorénavant kické en tant que punition!" },
				{ "AttackingBase", "[0080FF]{0}[FFFFFF] attaque une base!" },
				{ "InvalidRangeFormat", "Format de commande invalide. [0080FF]/addRange nomJour (Mon Tue Wed Thu Fri Sat Sun AllDays) heureDebut (0-23) heureFin (0-23)[FFFFFF]." },
				{ "InvalidDay", "[0080FF]Jour invalide[FFFFFF] => Mon Tue Wed Thu Fri Sat Sun AllDays." },
				{ "InvalidHourRange", "[0080FF]Heures invalides[FFFFFF] : heureDebut >0 | heureFin <23 | heureDebut<heureFin" },
				{ "RangeAdded", "Plage horaire ajoutée." },
				{ "RangeErased", "Plages horaires supprimées." },
				{ "ProtectionTimeRange", "Les plagues horaires de protection sont les suivantes: " },
				{ "NoRangeSet", "Aucune plage définie." },
				{ "ProtectionIsActive", "La protection est active!" },
				{ "ProtectionIsNotActive", "Les protections ne sont pas actives." },
				{ "ProtectionStarted", "La protection contre les raids a [0080FF]démarrée![FFFFFF] Pour plus de detail => [0080FF]/displayRanges.[FFFFFF]" },
				{ "ProtectionStopped", "La protection contre les raids s'est [0080FF]arrêtée![FFFFFF] Pour plus de detail => [0080FF]/displayRanges.[FFFFFF]" }
            }, this,"fr");
			
			lang.RegisterMessages(new Dictionary<string, string>
            {
				{ "RaidOnActivated", "RaidOn protection activated!" },
				{ "RaidOnDeactivated", "RaidOn protection deactivated!" },
				{ "RaidOffActivated", "RaidOff protection activated!" },
				{ "RaidOffDeactivated", "RaidOff protection deactivated!" },
				{ "RaidOffVictimKill", "[0080FF]{0} [FFFFFF] make [0080FF]raidOff [FFFFFF]with a [0080FF]{1}[FFFFFF] on the base from [0080FF]{2}[FFFFFF], his punishment is the death!" },
				{ "RaidOffVictimKick", "[0080FF]{0} [FFFFFF] make [0080FF]raidOff [FFFFFF]with a [0080FF]{1}[FFFFFF] on the base from [0080FF]{2}[FFFFFF], see you later!" },
				{ "raidOnlineKill", "[0080FF]{0} [FFFFFF] make [0080FF]raidOn [FFFFFF]with a [0080FF]{1}[FFFFFF] on the base from [0080FF]{2}[FFFFFF], his punishment is the death!" },
				{ "raidOnlineKick", "[0080FF]{0} [FFFFFF] make [0080FF]raidOn [FFFFFF]with a [0080FF]{1}[FFFFFF] on the base from [0080FF]{2}[FFFFFF], see you later!" },
				{ "RaidOffUnknownVictimKill", "[0080FF]{0} [FFFFFF] make [0080FF]raidOff [FFFFFF]with a [0080FF]{1}[FFFFFF], his punishment is the death!" },
				{ "RaidOffUnknownVictimKick", "[0080FF]{0} [FFFFFF] make [0080FF]raidOff [FFFFFF]with a [0080FF]{1}[FFFFFF], see you later!" },
				{ "BaseUnderAttack", "[FFFFFF]The base from [0080FF]{0} [FFFFFF]is under attack!" },
				{ "KillActivated", "[FFFFFF]Player will now get killed as punishment!" },
				{ "KickActivated", "[FFFFFF]Player will now get kick as punishment!" },
				{ "AttackingBase", "[0080FF]{0}[FFFFFF] is attacking a base!" },
				{ "InvalidRangeFormat", "Invalid command format. [0080FF]/addRange dayName (Mon Tue Wed Thu Fri Sat Sun AllDays) startHour (0-23) endHour (0-23)[FFFFFF]." },
				{ "InvalidDay", "[0080FF]Invalid day[FFFFFF] => Mon Tue Wed Thu Fri Sat Sun AllDays." },
				{ "InvalidHourRange", "[0080FF]Invalid houres[FFFFFF] : startHour >0 | endHour <23 | startHour<endHour" },
				{ "RangeAdded", "Time range added." },
				{ "RangeErased", "All time range have been deleted." },
				{ "ProtectionTimeRange", "The protection is active in following time ranges: " },
				{ "NoRangeSet", "No time range set." },
				{ "ProtectionIsActive", "Protection is active!" },
				{ "ProtectionIsNotActive", "Protection are not active." },
				{ "ProtectionStarted", "Anti raid protection [0080FF]started![FFFFFF] For more details => [0080FF]/displayRanges[FFFFFF]." },
				{ "ProtectionStopped", "Anti raid protection [0080FF]stopped![FFFFFF] For more details => [0080FF]/displayRanges[FFFFFF]." }
            }, this,"en");
        }	
		#endregion
		
		#region Commands
		[ChatCommand("raidOnToggle")]
		private void raidOnToggle(Player player){
			if (player.HasPermission("admin")){
				if(raidOnlineProtection){
					raidOnlineProtection = false;
					PrintToChat(player, GetMessage("RaidOnDeactivated"));
				}
				else{
					raidOnlineProtection = true;
					PrintToChat(player, GetMessage("RaidOnActivated"));				
				}
				UpdateConfig();
				checkProtectionStatus();
			}
		}
		
		[ChatCommand("addRange")]
		private void addRange(Player player,string cmd, string[] args){
			if(player.HasPermission("admin")){
				int startTime, endTime;
				if(args.Length != 3 || Int32.TryParse(args[1], out startTime) == false || Int32.TryParse(args[2], out endTime) == false){
					PrintToChat(player,GetMessage("InvalidRangeFormat"));
				}
				else{
					string day = args[0];
					bool dayValid = false;
					foreach(string aDay in _listDay){
						if(aDay.Equals(day) || day.Equals("AllDays")){
							dayValid = true;
							break;
						}
					}
					if(!dayValid){
						PrintToChat(player,GetMessage("InvalidDay"));
					}
					else if(startTime>endTime || startTime<0 || endTime>23){
					PrintToChat(player,GetMessage("InvalidHourRange"));
					}
					else{
						if(day == "AllDays"){
							foreach(string aDay in _listDay){
								_TimeRanges.Add(
									new TimeRanges{
										dayOfWeek = aDay,
										startHoure = startTime,
										stopHoure = endTime
									}
								);
							}
						}
						else{
							_TimeRanges.Add(
									new TimeRanges{
										dayOfWeek = day,
										startHoure = startTime,
										stopHoure = endTime
									}
							);
						}
						PrintToChat(player,GetMessage("RangeAdded"));
						SaveData();
						checkProtectionStatus();
					}
				}
			}
		}
		
		[ChatCommand("displayRanges")]
		private void displayRanges(Player player){
			if(_TimeRanges.Count == 0){
				PrintToChat(player,GetMessage("NoRangeSet"));
				PrintToChat(player, "RaidOn protection = "+raidOnlineProtection);
				PrintToChat(player, "RaidOff protection = "+raidOfflineProtection);
			}
			else{
				PrintToChat(player,GetMessage("ProtectionTimeRange"));
				foreach(TimeRanges aRange in _TimeRanges){
					PrintToChat(player,"["+aRange.dayOfWeek+"] "+aRange.startHoure+"h00 - "+aRange.stopHoure+"h00");
				}
			}
		}
		
		[ChatCommand("clearRanges")]
		private void clearRanges(Player player){
			if (player.HasPermission("admin")){
				_TimeRanges = new Collection<TimeRanges>();
				PrintToChat(player,GetMessage("RangeErased"));
				SaveData();
				checkProtectionStatus();
			}
		}
		
		
		private bool isInATimeRange(){
			if(_TimeRanges.Count != 0){
				DateTime dt = DateTime.Now;
				string currentDay = dt.ToString("ddd");
				int currentHour = Int32.Parse(dt.ToString("HH"));
				foreach(TimeRanges aRange in _TimeRanges){					
					if(aRange.dayOfWeek.Equals(currentDay) && aRange.startHoure<=currentHour && aRange.stopHoure>=currentHour){
						return true;
						
					}
				}
			}
			else{
				return true;
			}
			return false;
		}
		
		[ChatCommand("protectionStatus")]
		private void protectionStatus(Player player){
			if(isInATimeRange() && (raidOnlineProtection || raidOfflineProtection)){
				PrintToChat(player,GetMessage("ProtectionIsActive"));
				PrintToChat(player, "RaidOn protection = "+raidOnlineProtection);
				PrintToChat(player, "RaidOff protection = "+raidOfflineProtection);
			}
			else{
				PrintToChat(player,GetMessage("ProtectionIsNotActive"));
			}
		}
		
		[ChatCommand("killOrKickToggle")]
		private void killOrKickToggle(Player player){
			if (player.HasPermission("admin")){
				if(killOrKick){
					killOrKick = false;
					PrintToChat(player, GetMessage("KillActivated"));
				}
				else{
					killOrKick = true;
					PrintToChat(player, GetMessage("KickActivated"));				
				}
				UpdateConfig();
			}
		}
		
		[ChatCommand("raidOffToggle")]
		private void raidOffToggle(Player player){
			if(player.HasPermission("admin")){
				if(raidOfflineProtection){
					raidOfflineProtection = false;
					PrintToChat(player, GetMessage("RaidOffDeactivated"));
				}
				else{
					raidOfflineProtection = true;
					PrintToChat(player, GetMessage("RaidOffActivated"));				
				}
				UpdateConfig();
				checkProtectionStatus();
			}
		}
		
		#endregion
		
		#region Hooks
		private void OnCubeTakeDamage(CubeDamageEvent e){	
			if(e.Damage.Amount > 0 && e.Damage.DamageSource.IsPlayer){
				Player attacker = e.Damage.DamageSource.Owner;
				string weapon = e.Damage.Damager.name;
				Vector3 positionCube = convertPosCubeInCoordinates(e.Position);
				var victimeOwnerId = crestScheme.GetCrestPlayer(positionCube);
				
				//if there is an owner
				if(victimeOwnerId != 0){
					//if not attacking his guild
					if(attacker.Id != victimeOwnerId){
						//check if a guild member is online
						var victimInfos = getDatasFromPlayer(victimeOwnerId);
						bool victimGuildOnline = false;
						bool sameGuild = false;
						if(victimInfos != null){
							//search for online members
							foreach(Player guildMember in getPlayersOnline()){
								if(guildMember.GetGuild().Name == victimInfos.guildName){
									victimGuildOnline = true;
								}
							}
							if(attacker.GetGuild().Name == victimInfos.guildName){
								sameGuild = true;
							}
						}
						if(!sameGuild){
							//victim offline
							if(!victimGuildOnline){
								//offline protection active
								if(raidOfflineProtection && isInATimeRange()){
									e.Damage.Amount = 0f;
									
									if(getTimestamp()-lastWarning>5){
										if(!killOrKick){
											if(victimInfos != null){
												PrintToChat(GetMessage("RaidOffVictimKill"), attacker.DisplayName, weapon, victimInfos.playerName);
											}
											else{
												PrintToChat(GetMessage("RaidOffUnknownVictimKill"), attacker.DisplayName, weapon);
											}
										}
										else{
											if(victimInfos != null){
												PrintToChat(GetMessage("RaidOffVictimKick"), attacker.DisplayName, weapon, victimInfos.playerName);
											}
											else{
												PrintToChat(GetMessage("RaidOffUnknownVictimKick"), attacker.DisplayName, weapon);
											}
										}
										lastWarning = getTimestamp();
									}
									if(!killOrKick){
										attacker.GetHealth().Kill();
									}
									else{
										Server.Kick(attacker,"RaidOff");
									}
								}
								else{
									//offline protection inactive
									if(getTimestamp()-lastWarning>5){
										PrintToChat(GetMessage("AttackingBase"), attacker.DisplayName);
										lastWarning = getTimestamp();
									}
								}
							}
							else{
								if(raidOnlineProtection && isInATimeRange()){
									e.Damage.Amount = 0f;
									if(getTimestamp()-lastWarning>5){
										if(!killOrKick){
											PrintToChat(GetMessage("raidOnlineKill"), attacker.DisplayName,weapon, Server.GetPlayerById(victimeOwnerId).DisplayName);
										}
										else{
											PrintToChat(GetMessage("raidOnlineKick"), attacker.DisplayName,weapon, Server.GetPlayerById(victimeOwnerId).DisplayName);
										}
										lastWarning = getTimestamp();
									}
									if(!killOrKick){
										attacker.GetHealth().Kill();
									}
									else{
										Server.Kick(attacker,"RaidOn");
									}
								}
								else{
									if(getTimestamp()-lastWarning>5){
										PrintToChat(GetMessage("BaseUnderAttack"), Server.GetPlayerById(victimeOwnerId).DisplayName);
										lastWarning = getTimestamp();
									}
								}
							}
						}
					}
				}
			}
		}
		#endregion
		
		#region Functions
		private bool isOnline(ulong playerId){
			Player playerExist = Server.GetPlayerById(playerId);
			if (playerExist != null){
				return true;
			}
			return false;
		}
		
		private void UpdateConfig()
        {
            Config["raidOnlineProtection"] = raidOnlineProtection;
            Config["raidOfflineProtection"] = raidOfflineProtection;
            Config["killOrKick"] = killOrKick;
            SaveConfig();
        }
		
		private void setUpTimerUpdatePlayersData(){
			timer.Repeat(10f, 0, () =>
			{			
				updatePlayersData();
			});
		}
		
		private void setUpTimerCheckProtectionStatus(){
			timer.Repeat(60f, 0, () =>
			{			
				checkProtectionStatus();
			});
		}
		
		private void checkProtectionStatus(){
			if(!isProtectionActive && (isInATimeRange() && (raidOnlineProtection || raidOfflineProtection))){
				PrintToChat(GetMessage("ProtectionStarted"));
				isProtectionActive = true;
			}
			else if((isProtectionActive && !isInATimeRange()) || isProtectionActive&&(isInATimeRange() && (!raidOnlineProtection && !raidOfflineProtection))){
				PrintToChat(GetMessage("ProtectionStopped"));
				isProtectionActive = false;
			}
		}
		
		private void updatePlayersData(){
			foreach(Player player in getPlayersOnline()){
				//new player
				PlayerInfos playerInfos = getDatasFromPlayer(player);
				if(playerInfos == null){
					_PlayersInfos.Add(
						new PlayerInfos{
							playerId=player.Id,
							playerName=player.Name,
							guildName=PlayerExtensions.GetGuild(player).Name,
						}
					);
				}
				else{
					//update player	
					playerInfos.playerId=player.Id;
					playerInfos.playerName=player.Name;
					playerInfos.guildName=PlayerExtensions.GetGuild(player).Name;
				}
				SaveData();
			}
		}
		#endregion
		
		#region Helpers
		private T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }
		
		private Vector3 convertPosCubeInCoordinates(Vector3Int positionCube){
			if(positionCube != new Vector3(0,0,0)){
				if(positionCube.x != 0){
					return new Vector3(positionCube.x*1.2f,positionCube.y*1.2f,positionCube.z*1.2f);
				}
			}
			return new Vector3(0,0,0);
		}
		
		private int getTimestamp(){
			return (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;		
		}
		
		private PlayerInfos getDatasFromPlayer(Player player){
			foreach(var playerData in _PlayersInfos){
				if(player.Id == playerData.playerId)
					return playerData;
			}
			return (PlayerInfos)null;
		}
		
		private PlayerInfos getDatasFromPlayer(ulong playerId){
			foreach(var playerData in _PlayersInfos){
				if(playerId == playerData.playerId)
					return playerData;
			}
			return (PlayerInfos)null;
		}
		
		private List<Player> getPlayersOnline(){
			List<Player> listPlayersOnline = new List<Player>();
			foreach(Player player in Server.AllPlayers){
				if(player.Id != 9999999999){
					listPlayersOnline.Add(player);
				}
			}
			return listPlayersOnline;
		}
		
		string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);
		#endregion
	}
}