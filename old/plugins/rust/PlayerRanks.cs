using Rust;
using System.Collections.Generic;
using System;
using System.Text;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries; 
using Oxide.Core.Configuration;
using System.Linq;
using Oxide.Game.Rust.Cui;
using System.Reflection;

namespace Oxide.Plugins 
{
    [Info("PlayerRanks", "Steenamaroo", "1.4.1", ResourceId = 2359)]
    class PlayerRanks : RustPlugin
    {                                                              
        [PluginReference]
        Plugin Clans, Friends, EventManager, PlaytimeTracker, Economics;

        #region RustIO
        private Library lib;
        private MethodInfo isInstalled;
        private MethodInfo hasFriend;
         
        bool IsInstalled()
        {
            return (lib != null) && (bool)isInstalled.Invoke(lib, new object[] { });
        }

        bool HasFriend(string playerId, string friendId)
        {
            return (lib != null) && (bool)hasFriend.Invoke(lib, new object[] { playerId, friendId });
        }
        #endregion
        Dictionary<uint, Dictionary<ulong, float>> BradleyAttackers = new Dictionary<uint, Dictionary<ulong, float>>();
        Dictionary<uint, Dictionary<ulong, int>> HeliAttackers = new Dictionary<uint, Dictionary<ulong, int>>();
        Dictionary<ulong, WoundedData> woundedData = new Dictionary<ulong, WoundedData>();
        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);
        private Dictionary<string, bool> allowedCats = new Dictionary<string, bool>();
        List<ulong> airdrops = new List<ulong>();
        const string permAllowed = "playerranks.allowed";
        List<string> Broadcast = new List<string>(); 
        List<ulong> MenuOpen = new List<ulong>();
        bool catsOnOff; 
        Timer broadcastTimer; 

        public class DataStorage
        {
            public Dictionary<ulong, PRDATA> PlayerRankData = new Dictionary<ulong, PRDATA>();
            public Dictionary<DateTime, Dictionary<string, LeaderBoardData>> leaderBoards = new Dictionary<DateTime, Dictionary<string, LeaderBoardData>>();
        }
        
        public class LeaderBoardData
        {
            public ulong UserID;
            public string UserName;
            public double Score;
        }
        
        public class PRDATA
        {
            public object GetValue(String category)
            {
                     if (category == "PVPKills") return PVPKills;
                if (category == "PVPDistance") return PVPDistance;
                if (category == "PVEKills") return PVEKills;
                if (category == "PVEDistance") return PVEDistance;
                if (category == "NPCKills") return NPCKills;
                if (category == "NPCDistance") return NPCDistance;
                if (category == "SleepersKilled") return SleepersKilled;
                if (category == "HeadShots") return HeadShots;
                if (category == "Deaths") return Deaths;
                if (category == "Suicides") return Suicides;
                if (category == "KDR") return KDR;
                if (category == "SDR") return SDR;
                if (category == "SkullsCrushed") return SkullsCrushed;
                if (category == "TimesWounded") return TimesWounded;
                if (category == "TimesHealed") return TimesHealed;
                if (category == "HeliHits") return HeliHits;
                if (category == "HeliKills") return HeliKills;
                if (category == "APCHits") return APCHits;
                if (category == "APCKills") return APCKills;
                if (category == "BarrelsDestroyed") return BarrelsDestroyed;
                if (category == "ExplosivesThrown") return ExplosivesThrown;
                if (category == "ArrowsFired") return ArrowsFired;
                if (category == "BulletsFired") return BulletsFired;
                if (category == "RocketsLaunched") return RocketsLaunched;
                if (category == "WeaponTrapsDestroyed") return WeaponTrapsDestroyed;
                if (category == "DropsLooted") return DropsLooted;
                if (category == "StructuresBuilt") return StructuresBuilt;
                if (category == "StructuresDemolished") return StructuresDemolished;
                if (category == "ItemsDeployed") return ItemsDeployed;
                if (category == "ItemsCrafted") return ItemsCrafted;
                if (category == "EntitiesRepaired") return EntitiesRepaired;
                if (category == "ResourcesGathered") return ResourcesGathered;
                if (category == "StructuresUpgraded") return StructuresUpgraded;
                return "Not Found";
            }

            public bool Admin;
            public ulong UserID;
            public string Name;
            public string TimePlayed = "0";
            public string Status = "offline";
            public int Economics;
            public DateTime ActiveDate = DateTime.UtcNow;
            public int PVPKills;
            public double PVPDistance;
            public int PVEKills;
            public double PVEDistance;
            public int NPCKills;
            public double NPCDistance;
            public int SleepersKilled;  
            public int HeadShots;
            public int Deaths;
            public int Suicides;
            public double KDR;
            public double SDR;
            public int SkullsCrushed;
            public int TimesWounded;
            public int TimesHealed;
            public int HeliHits;
            public int HeliKills;
            public int APCHits;
            public int APCKills;
            public int BarrelsDestroyed;
            public int ExplosivesThrown;
            public int ArrowsFired;
            public int BulletsFired;
            public int RocketsLaunched;
            public int WeaponTrapsDestroyed;        
            public int DropsLooted;

            //intense options
            public int StructuresBuilt;
            public int StructuresDemolished; 
            public int ItemsDeployed;
            public int ItemsCrafted;
            public int EntitiesRepaired;
            public int ResourcesGathered;
            public int StructuresUpgraded; 
        }

        class WoundedData
        {
            public float distance;
            public ulong attackerId;
        }
        
        DataStorage data;
        private DynamicConfigFile PRData;

        void Loaded()
        {
            SetUp();
            lang.RegisterMessages(messages, this);
            permission.RegisterPermission(permAllowed, this);
            cmd.AddChatCommand($"{conf.Options.chatCommandAlias}", this, "CmdTarget");
            CheckDependencies();
            Sql_conn = Sql.OpenDb(conf.MySQL.sql_host, conf.MySQL.sql_port, conf.MySQL.sql_db, conf.MySQL.sql_user, conf.MySQL.sql_pass + ";CharSet=utf8mb4", this);
        }

        void Unload()
        {
            Sql_conn.Con.Dispose();
            foreach (BasePlayer current in BasePlayer.activePlayerList)
                if (MenuOpen.Contains(current.userID))
                {
                    CuiHelper.DestroyUi(current, "ranksgui");
                    MenuOpen.Remove(current.userID);
                }
            SaveData(false);
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            data.PlayerRankData[player.userID].Status = "offline";
            if (MenuOpen.Contains(player.userID))
            {
                CuiHelper.DestroyUi(player, "ranksgui");
                MenuOpen.Remove(player.userID);
            }
        }

        void SetUp()
        {
            PRData = Interface.Oxide.DataFileSystem.GetFile("PlayerRanks");
            LoadData();
            LoadConfigVariables();
            foreach (var entry in data.PlayerRankData)
                entry.Value.Status = "offline";
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                OnPlayerInit(player);

            timer.Every(conf.Options.saveTimer * 60, () =>
            {
                SaveData(true);
                Puts("Player Ranks Local Database Was Saved.");
            }
        );
            UpdateCategories();
            SetUpBroadcast();
        }

        void SetUpBroadcast()    
        {
            if (conf.Options.useTimedTopList)  
            {
                if (broadcastTimer != null)
                    broadcastTimer.Destroy(); 
                Broadcast.Clear();
                foreach (var cat in allowedCats)
                {
                    if (allowedCats[cat.Key] == true)
                    Broadcast.Add(cat.Key);
                }
                if (Broadcast.Count != 0)
                    BroadcastLooper(0);
            }
        }
        
        void BroadcastLooper(int counter) 
        {
            var time = 10f;
            if (BroadcastMethod(Broadcast[counter]))
                time = conf.Options.TimedTopListTimer * 60; 

            counter++;
            if (counter == Broadcast.Count) 
                counter = 0;
            broadcastTimer = timer.Once(time, () => BroadcastLooper(counter));
        }
        
        void UpdateCategories()
        {
            allowedCats.Clear();
            allowedCats.Add("PVPKills", conf.Categories.usepvpkills);
            allowedCats.Add("PVPDistance", conf.Categories.usepvpdistance);
            allowedCats.Add("PVEKills", conf.Categories.usepvekills);
            allowedCats.Add("PVEDistance", conf.Categories.usepvedistance);
            allowedCats.Add("NPCKills", conf.Categories.usenpckills);
            allowedCats.Add("NPCDistance", conf.Categories.usenpcdistance);
            allowedCats.Add("SleepersKilled", conf.Categories.usesleeperskilled);
            allowedCats.Add("HeadShots", conf.Categories.useheadshots);
            allowedCats.Add("Deaths", conf.Categories.usedeaths);
            allowedCats.Add("Suicides", conf.Categories.usesuicides);
            allowedCats.Add("KDR", conf.Categories.usekdr);
            allowedCats.Add("SDR", conf.Categories.usesdr);
            allowedCats.Add("SkullsCrushed", conf.Categories.useskullscrushed);
            allowedCats.Add("TimesWounded", conf.Categories.usetimeswounded);
            allowedCats.Add("TimesHealed", conf.Categories.usetimeshealed);
            allowedCats.Add("HeliHits", conf.Categories.usehelihits);
            allowedCats.Add("HeliKills", conf.Categories.usehelikills);
            allowedCats.Add("APCHits", conf.Categories.useapchits);
            allowedCats.Add("APCKills", conf.Categories.useapckills);
            allowedCats.Add("BarrelsDestroyed", conf.Categories.usebarrelsdestroyed);
            allowedCats.Add("ExplosivesThrown", conf.Categories.useexplosivesthrown);
            allowedCats.Add("ArrowsFired", conf.Categories.usearrowsfired);
            allowedCats.Add("BulletsFired", conf.Categories.usebulletsfired);
            allowedCats.Add("RocketsLaunched", conf.Categories.userocketslaunched);
            allowedCats.Add("WeaponTrapsDestroyed", conf.Categories.useweapontrapsdestroyed);
            allowedCats.Add("DropsLooted", conf.Categories.usedropslooted);
	    
            if (conf.Options.useIntenseOptions)
            {
                allowedCats.Add("StructuresBuilt", conf.Categories.usestructuresbuilt);
                allowedCats.Add("StructuresDemolished", conf.Categories.usestructuresdemolished);
                allowedCats.Add("ItemsDeployed", conf.Categories.useitemsdeployed);
                allowedCats.Add("ItemsCrafted", conf.Categories.useitemscrafted);
                allowedCats.Add("EntitiesRepaired", conf.Categories.useentitiesrepaired);
                allowedCats.Add("ResourcesGathered", conf.Categories.useresourcesgathered);
                allowedCats.Add("StructuresUpgraded", conf.Categories.usestructuresupgraded);
            }
        }
                
        void CheckDependencies()
        {
            //warn if enabled and missing
            if (Friends == null)
            if (conf.Options.useFriendsAPI && conf.Options.useFriendsAPI)
                PrintWarning("{0}: {1}", Title, "FriendsAPI is not installed and will not be used.");
            if (Clans == null && conf.Options.useClans)
                PrintWarning("{0}: {1}", Title, "Clans is not installed and will not be used.");
            lib = Interface.GetMod().GetLibrary<Library>("RustIO");
            if (lib == null || (isInstalled = lib.GetFunction("IsInstalled")) == null || (hasFriend = lib.GetFunction("HasFriend")) == null)
            {
                lib = null;
                if (conf.Options.useRustIO)
                PrintWarning("{0}: {1}", Title, "Rust:IO is not installed and will not be used.");
            }
            //just warn if missing
            if (PlaytimeTracker == null)
                PrintWarning("{0}: {1}", Title, "PlayTime Tracker is not installed and will not be used.");
            if (Economics == null)
                PrintWarning("{0}: {1}", Title, "Economics is not installed and will not be used.");   
        }
 
        void OnPlayerInit(BasePlayer player)
        {
            if (ServerUsers.Is(player.userID, ServerUsers.UserGroup.Banned))
                if (data.PlayerRankData.ContainsKey(player.userID))
                {
                    data.PlayerRankData.Remove(player.userID);
                    SaveData(true);
                    return;
                }
                            
            if (MenuOpen.Contains(player.userID))
            {
                MenuOpen.Remove(player.userID);
                CuiHelper.DestroyUi(player, "ranksgui");
            }

            if (!data.PlayerRankData.ContainsKey(player.userID))
            {
                data.PlayerRankData.Add(player.userID, new PRDATA()
                {
                    Admin = IsAuth(player),
                    UserID = player.userID,
                    Name = player.displayName,
                    TimePlayed = "0",
                    Status = "online",
                    ActiveDate = DateTime.UtcNow,
                    Economics = 0,
                    PVPKills = 0,
                    PVPDistance = 0.0,
                    PVEKills = 0,
                    PVEDistance = 0.0,
                    NPCKills = 0,
                    NPCDistance = 0.0,
                    SleepersKilled = 0,
                    HeadShots = 0,
                    Deaths = 0,
                    Suicides = 0,
                    KDR = 0,
                    SDR = 0,
                    SkullsCrushed = 0,
                    TimesHealed = 0,
                    TimesWounded = 0,
                    HeliHits = 0,
                    HeliKills = 0,
                    APCHits = 0,
                    APCKills = 0,
                    BarrelsDestroyed = 0,
                    ExplosivesThrown = 0,
                    ArrowsFired = 0,
                    BulletsFired = 0,
                    RocketsLaunched = 0,
                    WeaponTrapsDestroyed = 0,
                    DropsLooted = 0,

                    //intense options
                    StructuresBuilt = 0,
                    StructuresDemolished = 0,
                    ItemsDeployed = 0,
                    ItemsCrafted = 0,
                    EntitiesRepaired = 0,
                    ResourcesGathered = 0,
                    StructuresUpgraded = 0,
                });
            }
            else
                UpdatePlayer(player);
        } 

        void UpdatePlayer(BasePlayer player)
        {
            var path = data.PlayerRankData[player.userID];
            var time = PlaytimeTracker?.Call("GetPlayTime", player.UserIDString);
            var playTime = (time != null) ? GetPlaytimeClock((double)time) : null;
                    
            path.Admin = IsAuth(player);
            path.Name = player.displayName;
            path.Status = "online";
            path.ActiveDate = DateTime.UtcNow;
            if (Economics) 
                path.Economics = Convert.ToInt32(Economics?.Call("Balance", player.userID));
            path.TimePlayed = (!string.IsNullOrEmpty(playTime)) ? playTime : string.Empty;
        }

        void OnPlayerBanned(string name, ulong id, string address, string reason)=>data.PlayerRankData.Remove(id);
        
        private string GetPlaytimeClock(double time)
        {
            TimeSpan dateDifference = TimeSpan.FromSeconds((float)time); 
            var days = dateDifference.Days;
            var hours = dateDifference.Hours;
            hours += (days * 24);
            var mins = dateDifference.Minutes;
            var secs = dateDifference.Seconds;
            return string.Format("{0:00}:{1:00}:{2:00}", hours, mins, secs);
        }

        void OnEntityTakeDamage(BaseEntity entity, HitInfo hitinfo)
        {
            if (hitinfo.Initiator == null || !(hitinfo.Initiator is BasePlayer)) return;
            var player = hitinfo.Initiator as BasePlayer;
            DamageType type = hitinfo.damageTypes.GetMajorityDamageType();
            float amount = hitinfo.damageTypes.Total();

            if (conf.Options.blockEvents)
            if (CheckEvents(player))return;
            
            if (entity is BaseHelicopter)
            {
                if (!HeliAttackers.ContainsKey(entity.net.ID))
                    HeliAttackers.Add(entity.net.ID, new Dictionary<ulong, int>());
                if (!HeliAttackers[entity.net.ID].ContainsKey(player.userID))
                    HeliAttackers[entity.net.ID].Add(player.userID, 1);
                else
                {
                    HeliAttackers[entity.net.ID][player.userID]++;
                    ProcessHeliHits(player);
                }
            }
            if (entity is BradleyAPC)
            {
                if (type.ToString() == "Bullet")
                {
                    ProcessAPCHits(player);//plain bullets count as hits, but not towards majority damage
                    return;
                }
                if (!BradleyAttackers.ContainsKey(entity.net.ID)) //explosive ammo does get this far, because two damage types are processed.
                    BradleyAttackers.Add(entity.net.ID, new Dictionary<ulong, float>());
                if (!BradleyAttackers[entity.net.ID].ContainsKey(player.userID))
                    BradleyAttackers[entity.net.ID].Add(player.userID, amount);
                else
                {
                    BradleyAttackers[entity.net.ID][player.userID] = BradleyAttackers[entity.net.ID][player.userID] + amount;
                    ProcessAPCHits(player);
                }
            }
            if (entity is BasePlayer)
                if (hitinfo.isHeadshot && !FriendCheck(player, entity as BasePlayer))
                    ProcessHeadShot(player);
        }	
	
        ulong GetMajorityAttacker(uint id)
        {
            ulong majorityPlayer = 0U;
            if (HeliAttackers.ContainsKey(id))
            {
                Dictionary<ulong, int> majority = HeliAttackers[id].OrderByDescending(pair => pair.Value).Take(1).ToDictionary(pair => pair.Key, pair => pair.Value);
                foreach (var name in majority)
                {
                    majorityPlayer = name.Key;
                }
            }
            if (BradleyAttackers.ContainsKey(id))
            {
                Dictionary<ulong, float> majority = BradleyAttackers[id].OrderByDescending(pair => pair.Value).Take(1).ToDictionary(pair => pair.Key, pair => pair.Value);
                foreach (var name in majority)
                {
                    majorityPlayer = name.Key;
                }
            }
            return majorityPlayer;
        }
        
        void OnEntityDeath(BaseEntity entity, HitInfo hitinfo)
        {
            if (entity.name.Contains("corpse"))
                return;
            
            var victim = entity as BasePlayer;

            if (hitinfo?.Initiator == null && entity is BasePlayer)
            {
                if (woundedData.ContainsKey(victim.userID))
                { 
                    BasePlayer attacker = BasePlayer.FindByID(woundedData[victim.userID].attackerId);
                    if (conf.Options.blockEvents)
                    if (CheckEvents(attacker))return;
                    var distance = woundedData[victim.userID].distance;
                    if (!victim.userID.IsSteamId() || victim is NPCPlayer || victim is HTNPlayer)
                    {
                        if (attacker != null)
                        {
                            if (data.PlayerRankData.ContainsKey(attacker.userID))
                                data.PlayerRankData[attacker.userID].NPCKills++;
                            if (distance > data.PlayerRankData[attacker.userID].NPCDistance)
                                data.PlayerRankData[attacker.userID].NPCDistance = Math.Round(distance, 2); //process method not called, because distance is from record
                        }
                        return;
                    }
        
                    if (victim.userID.IsSteamId())
                    {
                        ProcessDeath(victim);
                        if (attacker != null)
                        {
                            if (data.PlayerRankData.ContainsKey(attacker.userID))
                                data.PlayerRankData[attacker.userID].PVPKills++;
                            if (distance > data.PlayerRankData[attacker.userID].PVPDistance)
                                data.PlayerRankData[attacker.userID].PVPDistance = Math.Round(distance, 2);
                        }
                        return;
                    } 
                    woundedData.Remove(victim.userID);
                }
                String [] stringArray = {"Cold", "Drowned", "Heat", "Suicide", "Generic", "Posion", "Radiation", "Thirst", "Hunger", "Fall"};
                if (stringArray.Any(victim.lastDamage.ToString().Contains))
                    ProcessSuicide(victim);
                ProcessDeath(victim);
                return;
            }
                                     
            if (entity is BaseHelicopter)  
            {
                BasePlayer player = null;
                player = BasePlayer.FindByID(GetMajorityAttacker(entity.net.ID));
                if (player != null)
                {                           
                    ProcessHeliKills(player);
                    HeliAttackers.Remove(entity.net.ID);
                }
                return; 
            }

            if (entity is BradleyAPC)  
            {
                BasePlayer player;
                var BradleyID = entity.net.ID;
                player = BasePlayer.FindByID(GetMajorityAttacker(BradleyID));
                if (player != null)
                {
                    ProcessAPCKills(player);
                    BradleyAttackers.Remove(BradleyID);
                }
                return; 
            }

            if (hitinfo?.Initiator is BasePlayer)
            {
                var player = hitinfo.Initiator as BasePlayer;
                if (player.userID.IsSteamId() && !(player is NPCPlayer) && !(player is HTNPlayer))
                {
                    if (entity.name.Contains("agents/"))
                        ProcessPVEKill(player, entity);
                    else if (entity.name.Contains("barrel"))	    
                        ProcessBarrelsDestroyed(player);
                    else if (!(entity is BasePlayer) && entity.name.Contains("turret"))                                                                                  
                        ProcessWeaponTrapsDestroyed(player);
                    else if (entity.name.Contains("guntrap"))                                                                                  
                        ProcessWeaponTrapsDestroyed(player);
                    else if (victim is BasePlayer && !victim.userID.IsSteamId())
                        ProcessNPCKills(player, victim);
                    else if (victim is BasePlayer && (victim is NPCPlayer || victim is HTNPlayer))
                        ProcessNPCKills(player, victim);
                    else if (victim is BasePlayer && victim.userID.IsSteamId())
                    {
                        ProcessDeath(victim);
                        if (hitinfo.Initiator != entity)
                            ProcessPVPKill(player, victim);
                        if (victim.IsSleeping())
                            ProcessSleepersKilled(player, victim);
                        if (hitinfo.Initiator == entity)
                            ProcessSuicide(player);
                    }
                    return;
                }
            }
            if (victim == null) return;

            if (victim is BasePlayer && !(victim is NPCPlayer) && !(victim is HTNPlayer))
                ProcessDeath(victim);
                
            if (woundedData.ContainsKey(victim.userID))
            woundedData.Remove(victim.userID);
            return;
        }
        
        void OnExplosiveThrown(BasePlayer player, BaseEntity entity, Item item)
        {
            if (!(player.GetActiveItem().info.displayName.english == "Supply Signal")) 
                ProcessExplosivesThrown(player);
        }

        void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod)
        {
            if (mod.ToString().Contains("arrow"))
                ProcessArrowsFired(player);

            if (mod.ToString().Contains("ammo"))
                ProcessBulletsFired(player);
        }
        
        void OnRocketLaunched(BasePlayer player)=>ProcessRocketsLaunched(player);
        
        void OnEntityBuilt(Planner plan, GameObject objectBlock)
        {
            if (conf.Options.useIntenseOptions)
            {
                BasePlayer player = plan.GetOwnerPlayer();
                if (player.GetActiveItem().info.displayName.english == "Building Plan")
                    ProcessStructuresBuilt(player);
                else
                    ProcessItemsDeployed(player);
            }
        }
             
        void OnStructureDemolish(BaseCombatEntity entity, BasePlayer player)=>ProcessStructuresDemolished(player);
        
        void OnItemCraft(ItemCraftTask item)
        {
            if (conf.Options.useIntenseOptions)
            {
                BasePlayer crafter = item.owner;
                if (crafter != null)
                    ProcessItemsCrafted(crafter);
            }
        }

        void OnStructureRepair(BaseCombatEntity entity, BasePlayer player)
        {
            float before = 0,after = 0;
            if (conf.Options.useIntenseOptions)
            {
                if (entity.health != null)
                {
                    before = entity.health;
                    timer.Once(0.1f,()=>
                    {
                        after = entity.health;
                        if (!Mathf.Approximately(before,after))
                            ProcessEntitiesRepaired(player);
                    });
                }
            }
        }
        
        void OnHealingItemUse(HeldEntity item, BasePlayer target)=>ProcessTimesHealed(target);

        void OnItemUse(Item item)
        {
            BasePlayer player = item?.GetOwnerPlayer();
            if (item.GetOwnerPlayer() == null) return;
            
            if (player != null && item.info.displayName.english == "Large Medkit")
                ProcessTimesHealed(player);
		
            if (item.info.shortname != "skull.human") return;//credit redBDGR
            string skullName = null;
            if (item.name != null)
                skullName = item.name.Substring(10, item.name.Length - 11);
            else return;
            
            if (!player.displayName.Contains($"{skullName}")) //.contains is for [God] - UserID would be better here
                ProcessSkullsCrushed(player);
        }

        void CanBeWounded(BasePlayer player, HitInfo hitInfo)
        {
            if (player == null || hitInfo == null) return;
            if (!(player.userID.IsSteamId()) || player is NPCPlayer || player is HTNPlayer) return; 
            var attacker = hitInfo.InitiatorPlayer;
            if (attacker != null)
            {
                if (attacker == player || IsFriend(attacker.userID, player.userID) || IsClanmate(attacker.userID, player.userID)) return;
                woundedData[player.userID] = new WoundedData {distance = Vector3.Distance(player.transform.position, attacker.transform.position), attackerId = attacker.userID };
                {
                    NextTick(() => 
                    {       
                        if (player.IsWounded())
                            ProcessTimesWounded(player);
                    });
                } 
            }
        }

        void OnPlayerRecover(BasePlayer player)
        {
            if (woundedData.ContainsKey(player.userID))
                woundedData.Remove(player.userID);
        }

        void OnStructureUpgrade(BaseCombatEntity entity, BasePlayer player, BuildingGrade.Enum grade)=>ProcessStructuresUpgraded(player);
        
        void OnCollectiblePickup(Item item, BasePlayer player)
        {
            if (conf.Options.useIntenseOptions)
                ProcessResourcesGathered(player, item.amount);
        }
        
        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (conf.Options.useIntenseOptions)
            {
                BasePlayer player = entity?.ToPlayer();
                ProcessResourcesGathered(player, item.amount);
            }
        }
        
		void OnEntitySpawned(BaseEntity entity)
		{
            if (entity.name.Contains("supply_drop"))
                airdrops.Add(entity.net.ID);    
		}

        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (airdrops.Contains(entity.net.ID))
            {
                airdrops.Remove(entity.net.ID);
                ProcessDropsLooted(player);
            }
        }
        
        #region processes    
        bool ProcessChecks(BasePlayer player)
        {
            if(!conf.Options.statCollection) return false;
            if (conf.Options.blockEvents)
                if (CheckEvents(player))return false;

            return data.PlayerRankData.ContainsKey(player.userID);
        }

        bool FriendCheck(BasePlayer player, BasePlayer victim)
        {
            if (victim != null)
            {
                if (conf.Options.useClans && IsClanmate(player.userID, victim.userID))
                    return true;
                if (conf.Options.useFriendsAPI && IsFriend(player.userID, victim.userID))
                    return true;
                if (conf.Options.useRustIO && HasFriend(player.userID.ToString(), victim.userID.ToString()))
                    return true;
            }
            return false;
        }
        void ProcessPVPKill(BasePlayer player, BasePlayer victim) 
        {
            if (FriendCheck(player, victim))
                return;
            
            if (ProcessChecks(player))
            { 
                data.PlayerRankData[player.userID].PVPKills++;
                if (victim.Distance(player.transform.position) > data.PlayerRankData[player.userID].PVPDistance)
                    data.PlayerRankData[player.userID].PVPDistance = Math.Round(victim.Distance(player.transform.position), 2);

                if ((data.PlayerRankData[player.userID].Deaths) > 0)
                {
                    var KDR = Convert.ToDouble(data.PlayerRankData[player.userID].PVPKills) / (data.PlayerRankData[player.userID].Deaths);
                    data.PlayerRankData[player.userID].KDR = Math.Round(KDR, 2);
                }
                else
                    data.PlayerRankData[player.userID].KDR = (data.PlayerRankData[player.userID].PVPKills);
            }
        }
        
        void ProcessPVEKill(BasePlayer player, BaseEntity victim)
        {
            if (ProcessChecks(player))
            {
                data.PlayerRankData[player.userID].PVEKills++;
                if (victim.Distance(player.transform.position) > data.PlayerRankData[player.userID].PVEDistance)
                    data.PlayerRankData[player.userID].PVEDistance = Math.Round(victim.Distance(player.transform.position), 2);
            }
        }
        
        void ProcessNPCKills(BasePlayer player, BaseEntity victim)
        {
            if (ProcessChecks(player))
            {
                data.PlayerRankData[player.userID].NPCKills++;
                if (victim.Distance(player.transform.position) > data.PlayerRankData[player.userID].NPCDistance)
                    data.PlayerRankData[player.userID].NPCDistance = Math.Round(victim.Distance(player.transform.position), 2);
            }
        }
        
        void ProcessSleepersKilled(BasePlayer player, BaseEntity victim)
        {
            if (ProcessChecks(player))
            {
                data.PlayerRankData[player.userID].SleepersKilled++;
                if (victim.Distance(player.transform.position) > data.PlayerRankData[player.userID].PVPDistance)
                    data.PlayerRankData[player.userID].PVPDistance = Math.Round(victim.Distance(player.transform.position), 2);
            }
        }
        
        void ProcessHeadShot(BasePlayer player)
        {
            if (ProcessChecks(player))
                data.PlayerRankData[player.userID].HeadShots++;	    
        }
        
        void ProcessDeath(BasePlayer player)
        {
            if (ProcessChecks(player))
            {
                data.PlayerRankData[player.userID].Deaths++;

                var SDR = Convert.ToDouble(data.PlayerRankData[player.userID].Suicides) / (data.PlayerRankData[player.userID].Deaths);
                data.PlayerRankData[player.userID].SDR = Math.Round(SDR, 2);

                var KDR = Convert.ToDouble(data.PlayerRankData[player.userID].PVPKills) / (data.PlayerRankData[player.userID].Deaths);
                data.PlayerRankData[player.userID].KDR = Math.Round(KDR, 2);
            }
        }
        
        void ProcessSuicide(BasePlayer player)
        {
            if (ProcessChecks(player))
            {
                data.PlayerRankData[player.userID].Suicides++;

                if ((data.PlayerRankData[player.userID].Deaths) > 0)
                {
                    var SDR = Convert.ToDouble(data.PlayerRankData[player.userID].Suicides) / (data.PlayerRankData[player.userID].Deaths);
                    data.PlayerRankData[player.userID].SDR = Math.Round(SDR, 2);
        
                    var KDR = Convert.ToDouble(data.PlayerRankData[player.userID].PVPKills) / (data.PlayerRankData[player.userID].Deaths);
                    data.PlayerRankData[player.userID].KDR = Math.Round(KDR, 2);
                }
                else
                {
                    data.PlayerRankData[player.userID].SDR = (data.PlayerRankData[player.userID].Suicides);
                    data.PlayerRankData[player.userID].KDR = (data.PlayerRankData[player.userID].PVPKills);
                }
            }
        }
        
        void ProcessSkullsCrushed(BasePlayer player)
        {
            if (ProcessChecks(player))
                data.PlayerRankData[player.userID].SkullsCrushed++;	    
        }
        
        void ProcessTimesWounded(BasePlayer player)
        {
            if (ProcessChecks(player))
                data.PlayerRankData[player.userID].TimesWounded++;
        }
        
        void ProcessTimesHealed(BasePlayer player)
        {
            if (ProcessChecks(player))
                data.PlayerRankData[player.userID].TimesHealed++;
        }
        
        void ProcessHeliHits(BasePlayer player)
        {
            if (ProcessChecks(player))
                data.PlayerRankData[player.userID].HeliHits++;
        }
        
        void ProcessHeliKills(BasePlayer player)
        {
            if (ProcessChecks(player))
                data.PlayerRankData[player.userID].HeliKills++;
        }

        void ProcessAPCHits(BasePlayer player)
        {
            if (ProcessChecks(player))
                data.PlayerRankData[player.userID].APCHits++;
        }
        
        void ProcessAPCKills(BasePlayer player)
        {
            if (ProcessChecks(player))
                data.PlayerRankData[player.userID].APCKills++;
        }
        
        void ProcessBarrelsDestroyed(BasePlayer player)
        {
            if (ProcessChecks(player))
                data.PlayerRankData[player.userID].BarrelsDestroyed++;
        }
        
        void ProcessExplosivesThrown(BasePlayer player)
        {
            if (ProcessChecks(player))
                data.PlayerRankData[player.userID].ExplosivesThrown++;
        }
        
        void ProcessArrowsFired(BasePlayer player)
        {
            if (ProcessChecks(player))
                data.PlayerRankData[player.userID].ArrowsFired++;
        }
        
        void ProcessBulletsFired(BasePlayer player)
        {
            if (ProcessChecks(player))
                data.PlayerRankData[player.userID].BulletsFired++;
        }
        
        void ProcessRocketsLaunched(BasePlayer player)
        {
            if (ProcessChecks(player))
                data.PlayerRankData[player.userID].RocketsLaunched++;
        }
        
        void ProcessWeaponTrapsDestroyed(BasePlayer player)
        {
            if (ProcessChecks(player))
                data.PlayerRankData[player.userID].WeaponTrapsDestroyed++;
        }
        
        void ProcessDropsLooted(BasePlayer player)
        {
            if (ProcessChecks(player))
                data.PlayerRankData[player.userID].DropsLooted++;
        }
        
        void ProcessStructuresBuilt(BasePlayer player)
        {
            if (ProcessChecks(player))
                data.PlayerRankData[player.userID].StructuresBuilt++;
        }
        
        void ProcessStructuresDemolished(BasePlayer player)
        {
            if (ProcessChecks(player))
                data.PlayerRankData[player.userID].StructuresDemolished++;
        }
        
        void ProcessItemsDeployed(BasePlayer player)
        {
            if (ProcessChecks(player))
                data.PlayerRankData[player.userID].ItemsDeployed++;
        }
        
        void ProcessItemsCrafted(BasePlayer player)
        {
            if (ProcessChecks(player))
                data.PlayerRankData[player.userID].ItemsCrafted++;
        }

        void ProcessEntitiesRepaired(BasePlayer player)
        {
            if (ProcessChecks(player))
                data.PlayerRankData[player.userID].EntitiesRepaired++;
        }

        void ProcessResourcesGathered(BasePlayer player, int amount = 0)
        {
            if (ProcessChecks(player))
                data.PlayerRankData[player.userID].ResourcesGathered+=amount;
        }
    
        void ProcessStructuresUpgraded(BasePlayer player) 
        {
            if (ProcessChecks(player))
                data.PlayerRankData[player.userID].StructuresUpgraded++;
        } 

        void BroadcastToAll(string msg, string keyword) => PrintToChat(conf.GUI.fontColor1 + keyword + " </color>" + conf.GUI.fontColor2 + msg + "</color>");
        
        bool IsClanmate(ulong playerId, ulong friendId)
        {
        if (!Clans || !conf.Options.useClans) return false;
            object playerTag = Clans?.Call("GetClanOf", playerId);
            object friendTag = Clans?.Call("GetClanOf", friendId);
            if (playerTag is string && friendTag is string)
            if (playerTag == friendTag) return true;
            return false;
        }

        bool IsFriend(ulong playerID, ulong friendID)
        {
            if (!Friends || !conf.Options.useFriendsAPI) return false;
            bool isFriend = (bool)Friends?.Call("IsFriend", playerID, friendID);
            return isFriend;
        }
        
        bool CheckEvents(BasePlayer player) 
        {
            object isPlaying = EventManager?.Call("isPlaying", new object[] { player });
            if (isPlaying is bool)
            if ((bool)isPlaying)
            return true;
            return false;
        }
        #endregion
        void PersonalAndCategoryUI(BasePlayer player, string personalStatsCat, string personalStatsVal, string pageTitle)
        {
            string guiString = String.Format("0.1 0.1 0.1 {0}", conf.GUI.guitransparency);
            var elements = new CuiElementContainer();
            var buttonColour = conf.GUI.buttonColour;
            var mainName = elements.Add(new CuiPanel { Image = { Color = guiString }, RectTransform = { AnchorMin = "0.1 0.12", AnchorMax = "0.9 0.98" }, CursorEnabled = true, FadeOut = 0.1f }, "Overlay", "ranksgui"); 
                elements.Add(new CuiElement { Parent = "ranksgui", Components = { new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
        
                elements.Add(new CuiButton { Button = { Command = $"CallPersonalStatsUI true", Color = buttonColour }, RectTransform = { AnchorMin = "0.03 0.95", AnchorMax = "0.22 0.98" }, Text = { Text = lang.GetMessage("mystats", this, player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"CallLeaderBoardUI true", Color = buttonColour }, RectTransform = { AnchorMin = "0.27 0.95", AnchorMax = "0.47 0.98" }, Text = { Text = lang.GetMessage("leaderboard", this, player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"CallCategoriesUI", Color = buttonColour }, RectTransform = { AnchorMin = "0.53 0.95", AnchorMax = "0.72 0.98" }, Text = { Text = lang.GetMessage("categories", this, player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter } }, mainName);
                if (HasPermission(player.UserIDString, permAllowed))
                elements.Add(new CuiButton { Button = { Command = $"CallAdminUI", Color = buttonColour }, RectTransform = { AnchorMin = "0.77 0.95", AnchorMax = "0.97 0.98" }, Text = { Text = lang.GetMessage("admin", this, player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter } }, mainName);

                elements.Add(new CuiLabel { Text = { Text = pageTitle, FontSize = 20, Align = TextAnchor.MiddleCenter },  RectTransform = { AnchorMin = "0 0.88", AnchorMax = "1 0.92" } }, mainName);
                
                elements.Add(new CuiLabel { Text = { Text = personalStatsCat, FontSize = 12, Align = TextAnchor.MiddleRight }, RectTransform = { AnchorMin = "0.1 0.10", AnchorMax = "0.48 0.88" } }, mainName);
                elements.Add(new CuiLabel { Text = { Text = personalStatsVal, FontSize = 12, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = "0.52 0.10", AnchorMax = "0.9 0.88" } }, mainName);
                
                elements.Add(new CuiButton { Button = { Command = "Close", Color = buttonColour }, RectTransform = { AnchorMin = "0.4 0.02", AnchorMax = "0.6 0.082" }, Text = { Text = lang.GetMessage("close", this, player.UserIDString), FontSize = 20, Align = TextAnchor.MiddleCenter } }, mainName);
                
            CuiHelper.AddUi(player, elements);
        }
        
        void CategoriesUI(BasePlayer player, string pageTitle)
        {
            string guiString = String.Format("0.1 0.1 0.1 {0}", conf.GUI.guitransparency);
            var elements = new CuiElementContainer();
            var buttonColour = conf.GUI.buttonColour;
            var mainName = elements.Add(new CuiPanel { Image = { Color = guiString }, RectTransform = { AnchorMin = "0.1 0.12", AnchorMax = "0.9 0.98" }, CursorEnabled = true, FadeOut = 0.1f }, "Overlay", "ranksgui"); 
                elements.Add(new CuiElement { Parent = "ranksgui", Components = { new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
            
                elements.Add(new CuiButton { Button = { Command = $"CallPersonalStatsUI true", Color = buttonColour }, RectTransform = { AnchorMin = "0.03 0.95", AnchorMax = "0.22 0.98" }, Text = { Text = lang.GetMessage("mystats", this, player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"CallLeaderBoardUI true", Color = buttonColour }, RectTransform = { AnchorMin = "0.27 0.95", AnchorMax = "0.47 0.98" }, Text = { Text = lang.GetMessage("leaderboard", this, player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter } }, mainName);
                elements.Add(new CuiButton { Button = { Command = null, Color = buttonColour }, RectTransform = { AnchorMin = "0.53 0.95", AnchorMax = "0.72 0.98" }, Text = { Text = lang.GetMessage("categories", this, player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter } }, mainName);
                
                if (HasPermission(player.UserIDString, permAllowed))
                elements.Add(new CuiButton { Button = { Command = $"CallAdminUI false", Color = buttonColour }, RectTransform = { AnchorMin = "0.77 0.95", AnchorMax = "0.97 0.98" }, Text = { Text = lang.GetMessage("admin", this, player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter } }, mainName);
                
                var buttonTop = 0.853;
                var buttonBottom = 0.873;
                foreach (var cat in allowedCats)
                {
                    if (cat.Value == true)
                    {
                        elements.Add(new CuiButton { Button = { Command = $"GetCategory {cat.Key}", Color = "0.7 0.32 0.17 0.0" }, RectTransform = { AnchorMin = $"0.3 {buttonTop}", AnchorMax = $"0.7 {buttonBottom}" }, Text = { Text = lang.GetMessage(cat.Key, this), FontSize = 12, Align = TextAnchor.MiddleCenter } }, mainName);
                        
                        buttonTop = buttonTop - 0.021;
                        buttonBottom = buttonBottom - 0.021;
                    }
                }
                elements.Add(new CuiLabel { Text = { Text = pageTitle, FontSize = 20, Align = TextAnchor.MiddleCenter },  RectTransform = { AnchorMin = "0 0.88", AnchorMax = "1 0.92" } }, mainName);
                
                elements.Add(new CuiButton { Button = { Command = "Close", Color = buttonColour }, RectTransform = { AnchorMin = "0.4 0.02", AnchorMax = "0.6 0.082" }, Text = { Text = lang.GetMessage("close", this, player.UserIDString), FontSize = 20, Align = TextAnchor.MiddleCenter } }, mainName);
                
            CuiHelper.AddUi(player, elements);
        }
        
        void LeaderBoardUI(BasePlayer player, string leaderBoardCat, string leaderBoardName, string leaderBoardScore, string pageTitle)
        {
            string guiString = String.Format("0.1 0.1 0.1 {0}", conf.GUI.guitransparency);
            var elements = new CuiElementContainer();
            var buttonColour = conf.GUI.buttonColour;
            var mainName = elements.Add(new CuiPanel { Image = { Color = guiString }, RectTransform = { AnchorMin = "0.1 0.12", AnchorMax = "0.9 0.98" }, CursorEnabled = true, FadeOut = 0.1f }, "Overlay", "ranksgui"); 
                elements.Add(new CuiElement { Parent = "ranksgui", Components = { new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
            
                elements.Add(new CuiButton { Button = { Command = $"CallPersonalStatsUI true", Color = buttonColour }, RectTransform = { AnchorMin = "0.03 0.95", AnchorMax = "0.22 0.98" }, Text = { Text = lang.GetMessage("mystats", this, player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter } }, mainName);
                elements.Add(new CuiButton { Button = { Command = null, Color = buttonColour }, RectTransform = { AnchorMin = "0.27 0.95", AnchorMax = "0.47 0.98" }, Text = { Text = lang.GetMessage("leaderboard", this, player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"CallCategoriesUI", Color = buttonColour }, RectTransform = { AnchorMin = "0.53 0.95", AnchorMax = "0.72 0.98" }, Text = { Text = lang.GetMessage("categories", this, player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter } }, mainName);
                
                if (HasPermission(player.UserIDString, permAllowed))
                elements.Add(new CuiButton { Button = { Command = $"CallAdminUI false", Color = buttonColour }, RectTransform = { AnchorMin = "0.77 0.95", AnchorMax = "0.97 0.98" }, Text = { Text = lang.GetMessage("admin", this, player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter } }, mainName);
                
                elements.Add(new CuiLabel { Text = { Text = pageTitle, FontSize = 20, Align = TextAnchor.MiddleCenter },  RectTransform = { AnchorMin = "0 0.88", AnchorMax = "1 0.92" } }, mainName);
                
                elements.Add(new CuiLabel { Text = { Text = leaderBoardCat, FontSize = 12, Align = TextAnchor.MiddleRight }, RectTransform = { AnchorMin = "0 0.10", AnchorMax = "0.38 0.88" } }, mainName);
                elements.Add(new CuiLabel { Text = { Text = leaderBoardScore, FontSize = 12, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = "0.4 0.10", AnchorMax = "0.6 0.88" } }, mainName);
                elements.Add(new CuiLabel { Text = { Text = leaderBoardName, FontSize = 12, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = "0.62 0.10", AnchorMax = "1 0.88" } }, mainName);
        
                elements.Add(new CuiButton { Button = { Command = "Close", Color = buttonColour }, RectTransform = { AnchorMin = "0.4 0.02", AnchorMax = "0.6 0.082" }, Text = { Text = lang.GetMessage("close", this, player.UserIDString), FontSize = 20, Align = TextAnchor.MiddleCenter } }, mainName);
                
            CuiHelper.AddUi(player, elements);
        }
        
        void AdminUI(BasePlayer player, bool wipe)
        {
            string guiString = String.Format("0.1 0.1 0.1 {0}", conf.GUI.guitransparency);
            var elements = new CuiElementContainer();
            var buttonColour = conf.GUI.buttonColour;
            var mainName = elements.Add(new CuiPanel { Image = { Color = guiString }, RectTransform = { AnchorMin = "0.1 0.12", AnchorMax = "0.9 0.98" }, CursorEnabled = true, FadeOut = 0.1f }, "Overlay", "ranksgui"); 
                elements.Add(new CuiElement { Parent = "ranksgui", Components = { new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
            
                elements.Add(new CuiButton { Button = { Command = $"CallPersonalStatsUI true", Color = buttonColour }, RectTransform = { AnchorMin = "0.03 0.95", AnchorMax = "0.22 0.98" }, Text = { Text = lang.GetMessage("mystats", this, player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"CallLeaderBoardUI true", Color = buttonColour }, RectTransform = { AnchorMin = "0.27 0.95", AnchorMax = "0.47 0.98" }, Text = { Text = lang.GetMessage("leaderboard", this, player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"CallCategoriesUI", Color = buttonColour }, RectTransform = { AnchorMin = "0.53 0.95", AnchorMax = "0.72 0.98" }, Text = { Text = lang.GetMessage("categories", this, player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter } }, mainName);
                if (HasPermission(player.UserIDString, permAllowed))
                elements.Add(new CuiButton { Button = { Command = null, Color = buttonColour }, RectTransform = { AnchorMin = "0.77 0.95", AnchorMax = "0.97 0.98" }, Text = { Text = lang.GetMessage("admin", this, player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter } }, mainName);
 
                var buttonTop = 0.853;
                var buttonBottom = 0.873;
                
                foreach (var cat in allowedCats)
                {
                    elements.Add(new CuiLabel { Text = { Text = cat.Key, FontSize = 11, Align = TextAnchor.MiddleCenter },  RectTransform = { AnchorMin = $"0 {buttonTop}", AnchorMax = $"0.2 {buttonBottom}" } }, mainName);
                    if (cat.Value == true)
                    elements.Add(new CuiButton { Button = { Command = $"ToggleCategory {cat.Key}", Color = buttonColour }, RectTransform = { AnchorMin = $"0.22 {buttonTop}", AnchorMax = $"0.32 {buttonBottom}" }, Text = { Text = lang.GetMessage("on", this, player.UserIDString), FontSize = 10, Align = TextAnchor.MiddleCenter } }, mainName); 
                    else
                    elements.Add(new CuiButton { Button = { Command = $"ToggleCategory {cat.Key}", Color = "0.7 0.32 0.17 0.5" }, RectTransform = { AnchorMin = $"0.22 {buttonTop}", AnchorMax = $"0.32 {buttonBottom}" }, Text = { Text = lang.GetMessage("off", this, player.UserIDString), FontSize = 10, Align = TextAnchor.MiddleCenter } }, mainName);
                    
                    buttonTop = buttonTop - 0.021;
                    buttonBottom = buttonBottom - 0.021; 
                }
                buttonTop = buttonTop - 0.031;
                buttonBottom = buttonBottom - 0.031;

                buttonTop = buttonTop - 0.031;
                buttonBottom = buttonBottom - 0.031;                       
                    elements.Add(new CuiLabel { Text = { Text = "Intense Options", FontSize = 11, Align = TextAnchor.MiddleCenter },  RectTransform = { AnchorMin = $"0 {buttonTop}", AnchorMax = $"0.2 {buttonBottom}" } }, mainName);
                    if (conf.Options.useIntenseOptions == true)
                    elements.Add(new CuiButton { Button = { Command = "ToggleIntenseOptions", Color = buttonColour }, RectTransform = { AnchorMin = $"0.22 {buttonTop}", AnchorMax = $"0.32 {buttonBottom}" }, Text = { Text = lang.GetMessage("on", this, player.UserIDString), FontSize = 10, Align = TextAnchor.MiddleCenter } }, mainName);
                    else
                    elements.Add(new CuiButton { Button = { Command = "ToggleIntenseOptions", Color = buttonColour }, RectTransform = { AnchorMin = $"0.22 {buttonTop}", AnchorMax = $"0.32 {buttonBottom}" }, Text = { Text = lang.GetMessage("off", this, player.UserIDString), FontSize = 10, Align = TextAnchor.MiddleCenter } }, mainName);
                    
                    
                if (conf.Options.statCollection)
                    elements.Add(new CuiButton { Button = { Command = "toggleStatCollection", Color = buttonColour }, RectTransform = { AnchorMin = "0.7 0.8", AnchorMax = "0.9 0.83" }, Text = { Text = lang.GetMessage("gatherStatsOnButton", this, player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter } }, mainName);
                else
                    elements.Add(new CuiButton { Button = { Command = "toggleStatCollection", Color = buttonColour }, RectTransform = { AnchorMin = "0.7 0.8", AnchorMax = "0.9 0.83" }, Text = { Text = lang.GetMessage("gatherStatsOffButton", this, player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter } }, mainName);
                
                if (conf.Options.allowadmin)
                    elements.Add(new CuiButton { Button = { Command = "AllowAdmin", Color = buttonColour }, RectTransform = { AnchorMin = "0.7 0.75", AnchorMax = "0.9 0.78" }, Text = { Text = lang.GetMessage("disableAdminStatsButton", this, player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter } }, mainName);
                else
                    elements.Add(new CuiButton { Button = { Command = "AllowAdmin", Color = buttonColour }, RectTransform = { AnchorMin = "0.7 0.75", AnchorMax = "0.9 0.78" }, Text = { Text = lang.GetMessage("AllowAdminStatsButton", this, player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter } }, mainName);      
                                
                elements.Add(new CuiButton { Button = { Command = "playerranks.save", Color = buttonColour }, RectTransform = { AnchorMin = "0.7 0.7", AnchorMax = "0.9 0.73" }, Text = { Text = lang.GetMessage("savePlayerDataButton", this, player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter } }, mainName);
         
                if (wipe == false)
                    elements.Add(new CuiButton { Button = { Command = "wipeFirst", Color = buttonColour }, RectTransform = { AnchorMin = "0.7 0.65", AnchorMax = "0.9 0.68" }, Text = { Text = lang.GetMessage("wipePlayerDataButton", this, player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter } }, mainName);
                else
                    elements.Add(new CuiButton { Button = { Command = "playerranks.wipe", Color = buttonColour }, RectTransform = { AnchorMin = "0.7 0.65", AnchorMax = "0.9 0.68" }, Text = { Text = lang.GetMessage("confirm", this, player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter } }, mainName); 
                
                elements.Add(new CuiButton { Button = { Command = "saveLeaderboard", Color = buttonColour }, RectTransform = { AnchorMin = "0.7 0.6", AnchorMax = "0.9 0.63" }, Text = { Text = lang.GetMessage("saveLeaderBoardButton", this, player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter } }, mainName);
                
                elements.Add(new CuiButton { Button = { Command = "wipeLeaderBoards", Color = buttonColour }, RectTransform = { AnchorMin = "0.7 0.55", AnchorMax = "0.9 0.58" }, Text = { Text = "Wipe LeaderBoards", FontSize = 16, Align = TextAnchor.MiddleCenter } }, mainName);

                elements.Add(new CuiButton { Button = { Command = "Close", Color = buttonColour }, RectTransform = { AnchorMin = "0.4 0.02", AnchorMax = "0.6 0.082" }, Text = { Text = lang.GetMessage("close", this, player.UserIDString), FontSize = 20, Align = TextAnchor.MiddleCenter } }, mainName);
                                
            CuiHelper.AddUi(player, elements);
        }
        #region UI methods

        [ConsoleCommand("CallPersonalStatsUI")]
        void PSUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            CallPersonalStatsUI(player, arg.Args[0]);
        }
        void CallPersonalStatsUI(BasePlayer player, string button)
        {
            if (player == null) return;
            var d = data.PlayerRankData[player.userID];
            var dictToUse = data.PlayerRankData;
            
            string pageTitle = conf.GUI.fontColor1 + lang.GetMessage("title", this, player.UserIDString) + "</color>" + conf.GUI.fontColor2 + d.Name + "</color> \n";
            string playerTopStatsCat = "";
            string playerTopStatsVal = "";
            
            foreach (var cat in allowedCats)
            {
                if (allowedCats[cat.Key] == true)
                {	
                    playerTopStatsCat += conf.GUI.fontColor3 + lang.GetMessage($"{cat.Key}", this)+ "</color> \n";
                    if (cat.Key.Contains("Distance"))
			playerTopStatsVal += conf.GUI.fontColor1 + d.GetValue(cat.Key) + "m</color> \n";//append M to distances
		    else
			playerTopStatsVal += conf.GUI.fontColor1 + d.GetValue(cat.Key) + "</color> \n";
                }
            }

            if (MenuOpen.Contains(player.userID))
            {
                CuiHelper.DestroyUi(player, "ranksgui");
                MenuOpen.Remove(player.userID);
                if (button != "true") return;
            }
            PersonalAndCategoryUI(player, playerTopStatsCat, playerTopStatsVal, pageTitle);
            MenuOpen.Add(player.userID);
            return;
        }
        
        [ConsoleCommand("CallCategoriesUI")]
        void CatUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            CallCategoriesUI(player);
            
        }
        void CallCategoriesUI(BasePlayer player)
        {
            string pageTitle = conf.GUI.fontColor1 + lang.GetMessage("title", this, player.UserIDString) + "</color>" + conf.GUI.fontColor2 + lang.GetMessage("categories", this, player.UserIDString) + "</color> \n";
            
            if (MenuOpen.Contains(player.userID))
            {
                CuiHelper.DestroyUi(player, "ranksgui");
                MenuOpen.Remove(player.userID);
            }
            CategoriesUI(player, pageTitle);
            MenuOpen.Add(player.userID);
        }
         
        [ConsoleCommand("GetCategory")]
        void GetCategory(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            string pageTitle = conf.GUI.fontColor1 + lang.GetMessage("title", this, player.UserIDString) + "</color>" + conf.GUI.fontColor2 + lang.GetMessage($"{arg.Args[0]}", this, player.UserIDString) + "</color> \n";
            string catTopName = "";
            string catTopVal = "";
            
            foreach (var cat in allowedCats)
            {
                if (allowedCats[cat.Key] == true)
                if (cat.Key.ToLower() == arg.Args[0].ToLower())
                {
                    var d = data.PlayerRankData[player.userID];
                    var dictToUse = data.PlayerRankData;
                    if (conf.Options.allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<ulong, PRDATA> top = dictToUse.OrderByDescending(pair => pair.Value.GetValue(cat.Key)).Take(30).ToDictionary(pair => pair.Key, pair => pair.Value);

                    var addPlayerManually = true;
                    foreach (var name in top)
                    {
                        catTopName += conf.GUI.fontColor3 + name.Value.Name + "</color> \n";
                        if (cat.Key.Contains("Distance"))
                            catTopVal += conf.GUI.fontColor1 + name.Value.GetValue(cat.Key) + "m</color> \n";//append M to distances
                        else
                            catTopVal += conf.GUI.fontColor1 + name.Value.GetValue(cat.Key) + "</color> \n";

                        if (player.userID == name.Key) addPlayerManually = false;
                    }
                    if (addPlayerManually && dictToUse.ContainsKey(player.userID)) //admin double check
                    {
                        catTopName += "\n" + conf.GUI.fontColor3 + d.Name + "</color>";
                        if (cat.Key.Contains("Distance"))
                            catTopVal += "\n" + conf.GUI.fontColor1 + d.GetValue(cat.Key) + "m</color>";//append M to distances
                        else
                            catTopVal += "\n" + conf.GUI.fontColor1 + d.GetValue(cat.Key) + "</color>";
                    }
                    break;
                }
            }
            if (MenuOpen.Contains(player.userID))
            {
                CuiHelper.DestroyUi(player, "ranksgui");
                MenuOpen.Remove(player.userID);
            }
            PersonalAndCategoryUI(player, catTopName, catTopVal, pageTitle);
            MenuOpen.Add(player.userID);
            return;
        }

        [ConsoleCommand("CallLeaderBoardUI")]
        void LBUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            CallLeaderBoardUI(player, arg.Args[0]);
        }

        void CallLeaderBoardUI(BasePlayer player, string button)
        {
            if (player == null) return;
            var d = data.PlayerRankData[player.userID];
            var dictToUse = data.PlayerRankData;
            
            string pageTitle = conf.GUI.fontColor1 + lang.GetMessage("title", this) + "</color>" + conf.GUI.fontColor2 + lang.GetMessage("leaderboard", this) +"</color> \n";
            string leaderBoardCat = "";
            string leaderBoardName = "";
            string leaderBoardScore = "";
        
            foreach (var cat in allowedCats)
            {
                if (allowedCats[cat.Key] == true)
                {
                    if (conf.Options.allowadmin == false)
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
            
                    Dictionary<string, object> top = dictToUse.OrderByDescending(pair => pair.Value.GetValue(cat.Key)).Take(1).ToDictionary(pair => pair.Value.Name, pair => pair.Value.GetValue(cat.Key));

                    foreach (var item in top)
                    {
                        leaderBoardCat += conf.GUI.fontColor3 + lang.GetMessage($"{cat.Key}", this) + "</color>\n";
                        if (cat.Key.Contains("Distance"))
                            leaderBoardScore += conf.GUI.fontColor1 + item.Value + "m</color>\n";//append M to distances
                        else
                            leaderBoardScore += conf.GUI.fontColor1 + item.Value + "</color>\n";
                        var nameString = item.Key;
                        if (nameString.Length > 32)
                        nameString = string.Format(item.Key.Substring(0,30) + "...");
                        leaderBoardName += conf.GUI.fontColor3 + $"{nameString}" + "</color>\n";
                        break;
                    }
                }
            }

            if (MenuOpen.Contains(player.userID))
            {
                CuiHelper.DestroyUi(player, "ranksgui");
                MenuOpen.Remove(player.userID);
                if (button != "true") return;
            }

            LeaderBoardUI(player, leaderBoardCat, leaderBoardName, leaderBoardScore, pageTitle);
            MenuOpen.Add(player.userID);
            return;
        }
        
        [ConsoleCommand("CallAdminUI")]
        void ADUI(ConsoleSystem.Arg arg) 
        {
            var player = arg.Connection.player as BasePlayer;
            CallAdminUI(player, false);
        }
        void CallAdminUI(BasePlayer player, bool wipe)
        {
            if (player == null) return;
            
            string pageTitle = conf.GUI.fontColor1 + lang.GetMessage("title", this) + "</color>" + conf.GUI.fontColor2 + lang.GetMessage("leaderBoard", this) +"</color> \n";


            if (MenuOpen.Contains(player.userID))
            {
                CuiHelper.DestroyUi(player, "ranksgui");
                MenuOpen.Remove(player.userID);
            }

            AdminUI(player, wipe);
            MenuOpen.Add(player.userID);
            return;
        }
        #endregion
        
        #region console commands

        [ConsoleCommand("ToggleCategory")]
        void ToggleCat(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (arg.Args[0] == "PVPKills") conf.Categories.usepvpkills = !conf.Categories.usepvpkills;
            if (arg.Args[0] == "PVPDistance") conf.Categories.usepvpdistance = !conf.Categories.usepvpdistance;
            if (arg.Args[0] == "PVEKills") conf.Categories.usepvekills = !conf.Categories.usepvekills;
            if (arg.Args[0] == "PVEDistance") conf.Categories.usepvedistance = !conf.Categories.usepvedistance;
            if (arg.Args[0] == "NPCKills") conf.Categories.usenpckills = !conf.Categories.usenpckills;
            if (arg.Args[0] == "NPCDistance") conf.Categories.usenpcdistance = !conf.Categories.usenpcdistance;
            if (arg.Args[0] == "SleepersKilled") conf.Categories.usesleeperskilled = !conf.Categories.usesleeperskilled;
            if (arg.Args[0] == "HeadShots") conf.Categories.useheadshots = !conf.Categories.useheadshots;
            if (arg.Args[0] == "Deaths") conf.Categories.usedeaths = !conf.Categories.usedeaths;
            if (arg.Args[0] == "Suicides") conf.Categories.usesuicides = !conf.Categories.usesuicides;
            if (arg.Args[0] == "KDR") conf.Categories.usekdr = !conf.Categories.usekdr;
            if (arg.Args[0] == "SDR") conf.Categories.usesdr = !conf.Categories.usesdr;
            if (arg.Args[0] == "SkullsCrushed") conf.Categories.useskullscrushed = !conf.Categories.useskullscrushed;
            if (arg.Args[0] == "TimesWounded") conf.Categories.usetimeswounded = !conf.Categories.usetimeswounded;
            if (arg.Args[0] == "TimesHealed") conf.Categories.usetimeshealed = !conf.Categories.usetimeshealed;
            if (arg.Args[0] == "HeliHits") conf.Categories.usehelihits = !conf.Categories.usehelihits;
            if (arg.Args[0] == "HeliKills") conf.Categories.usehelikills = !conf.Categories.usehelikills;
            if (arg.Args[0] == "APCHits") conf.Categories.useapchits = !conf.Categories.useapchits;
            if (arg.Args[0] == "APCKills") conf.Categories.useapckills = !conf.Categories.useapckills;
            if (arg.Args[0] == "BarrelsDestroyed") conf.Categories.usebarrelsdestroyed = !conf.Categories.usebarrelsdestroyed;
            if (arg.Args[0] == "ExplosivesThrown") conf.Categories.useexplosivesthrown = !conf.Categories.useexplosivesthrown;
            if (arg.Args[0] == "ArrowsFired") conf.Categories.usearrowsfired = !conf.Categories.usearrowsfired;
            if (arg.Args[0] == "BulletsFired") conf.Categories.usebulletsfired = !conf.Categories.usebulletsfired;
            if (arg.Args[0] == "RocketsLaunched") conf.Categories.userocketslaunched = !conf.Categories.userocketslaunched;
            if (arg.Args[0] == "WeaponTrapsDestroyed")  conf.Categories.useweapontrapsdestroyed = !conf.Categories.useweapontrapsdestroyed;
            if (arg.Args[0] == "DropsLooted") conf.Categories.usedropslooted = !conf.Categories.usedropslooted;
	    
            if (conf.Options.useIntenseOptions)
            {
                if (arg.Args[0] == "StructuresBuilt") conf.Categories.usestructuresbuilt = !conf.Categories.usestructuresbuilt;
                if (arg.Args[0] == "StructuresDemolished") conf.Categories.usestructuresdemolished = !conf.Categories.usestructuresdemolished;
                if (arg.Args[0] == "ItemsDeployed") conf.Categories.useitemsdeployed = !conf.Categories.useitemsdeployed;
                if (arg.Args[0] == "ItemsCrafted") conf.Categories.useitemscrafted = !conf.Categories.useitemscrafted;
                if (arg.Args[0] == "EntitiesRepaired") conf.Categories.useentitiesrepaired = !conf.Categories.useentitiesrepaired;
                if (arg.Args[0] == "ResourcesGathered") conf.Categories.useresourcesgathered = !conf.Categories.useresourcesgathered;
                if (arg.Args[0] == "StructuresUpgraded") conf.Categories.usestructuresupgraded = !conf.Categories.usestructuresupgraded;
            }
            SaveConfig(conf);
            UpdateCategories();
            SetUpBroadcast();
            CallAdminUI(player, false);
        }

        [ConsoleCommand("ToggleIntenseOptions")]
        void ToggleIntense(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (!HasPermission(player.UserIDString, permAllowed)) 
                return;
            conf.Options.useIntenseOptions = !conf.Options.useIntenseOptions;

            SaveConfig(conf);
            UpdateCategories();
            CallAdminUI(player, false);
        }
        
        [ConsoleCommand("toggleStatCollection")]
        void ToggleCollection(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (HasPermission(player.UserIDString, permAllowed))
            conf.Options.statCollection = !conf.Options.statCollection;

            SaveConfig(conf);
            UpdateCategories();
            CallAdminUI(player, false);
        }
        
        [ConsoleCommand("AllowAdmin")]
        void AllowAdmin(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (!HasPermission(player.UserIDString, permAllowed)) 
                return;
            conf.Options.allowadmin = !conf.Options.allowadmin;
            
            SaveConfig(conf);
            UpdateCategories();
            CallAdminUI(player, false);         
        }
        
        [ConsoleCommand("wipeFirst")]
        void WipeAttempt(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (!HasPermission(player.UserIDString, permAllowed)) return;
            CallAdminUI(player, true);
        }

        [ConsoleCommand("saveLeaderboard")]
        void SaveBoard(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (!HasPermission(player.UserIDString, permAllowed)) return;
            var dictToUse = data.PlayerRankData;
            var date = DateTime.UtcNow;
            data.leaderBoards.Add(date, new Dictionary<string, LeaderBoardData>());
            var lBoard = data.leaderBoards[date];
            //create leaderboard            
            foreach (var cat in allowedCats)
            {
                if (allowedCats[cat.Key] == true)
                {
                    if (conf.Options.allowadmin == false)
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
            
                    Dictionary<ulong, PRDATA> top = dictToUse.OrderByDescending(pair => pair.Value.GetValue(cat.Key)).Take(1).ToDictionary(pair => pair.Key, pair => pair.Value);
                    foreach (var leader in top)
                    {
                        data.leaderBoards[date].Add(cat.Key, new LeaderBoardData
                        {
                            UserID = leader.Key,
                            UserName = leader.Value.Name,
                            Score = Convert.ToDouble(data.PlayerRankData[leader.Key].GetValue(cat.Key))  
                        });
                    }
                }
            }
            SaveConfig(conf);
            SaveData(true);
            CallAdminUI(player, false);
        }        
     
        [ConsoleCommand("wipeLeaderBoards")]
        void WipeBoards(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (!HasPermission(player.UserIDString, permAllowed)) 
                return;
            var dictToUse = data.PlayerRankData;
            
            data.leaderBoards.Clear();
            SaveData(true);
            CallAdminUI(player, false);
        }
        
        [ConsoleCommand("Close")]
        void Close(ConsoleSystem.Arg arg)
        { 
            var player = arg.Connection.player as BasePlayer;
            if (MenuOpen.Contains(player.userID))
                {
                    CuiHelper.DestroyUi(player, "ranksgui");
                    MenuOpen.Remove(player.userID);
                }
            return;
        }
        
        [ConsoleCommand("playerranks.save")]
        void CmdSave()
        {
            SaveData(true);
            Puts("PlayerRanks database was saved.");
        }

        [ConsoleCommand("playerranks.wipe")]
        void CmdWipe(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                var player = arg.Connection.player as BasePlayer;
                if (!HasPermission(player.UserIDString, permAllowed))
                return;
            }
            data.PlayerRankData.Clear();
            PRData.WriteObject(data);
            SetUp();
            if (conf.MySQL.useMySQL)
                LoadMySQL(true);
            Puts("PlayerRanks database was wiped.");
        }
        #endregion
        
        #region chat commands
        [ChatCommand("pr")] 
        void CmdTarget(BasePlayer player, string command, string[] args)
        {
	        var dictToUse = data.PlayerRankData;
	    
            if (args == null || args.Length == 0)
            {
                CallPersonalStatsUI(player, "true");
                return;
            }
            
            var d = data.PlayerRankData[player.userID];
            
            switch (args[0].ToLower())
            {
                case "save":
                    if (HasPermission(player.UserIDString, permAllowed))
                    {
                        SaveData(true);
                        SendReply(player, conf.GUI.fontColor1 + lang.GetMessage("title", this) + "</color>" + lang.GetMessage("save", this));
                    }
                    return;            
            
                case "wipe":
                    if (HasPermission(player.UserIDString, permAllowed))
                    {
                        data.PlayerRankData.Clear();
                        PRData.WriteObject(data);
                        SetUp();
                        if (conf.MySQL.useMySQL)
                            LoadMySQL(true);
                        SendReply(player, conf.GUI.fontColor1 + lang.GetMessage("title", this) + "</color>" + lang.GetMessage("wipe", this));
                    }
                    return;
                    
                case "del":
                    if (HasPermission(player.UserIDString, permAllowed))
                    {
                        if (args.Length == 2)
                        {
                            string s = args[1];
                            ulong result;
                            if (ulong.TryParse(s, out result)) 
                            {
                                ulong arg = Convert.ToUInt64(args[1]);
                                if (data.PlayerRankData.ContainsKey(arg))
                                {
                                    data.PlayerRankData.Remove(arg);
                                    PRData.WriteObject(data);
                                    SetUp();
                                    SendReply(player, conf.GUI.fontColor1 + lang.GetMessage("title", this) + "</color>" + lang.GetMessage("dbremoved", this));
                                }
                                else
                                SendReply(player, conf.GUI.fontColor1 + lang.GetMessage("title", this) + "</color>" + lang.GetMessage("noentry", this));
                            }
                            else
                            SendReply(player, conf.GUI.fontColor1 + lang.GetMessage("title", this) + "</color>" + lang.GetMessage("syntax", this));
                        }
                    }
                    return;

                case "wipecategory":
                    if (args.Length == 2)
                    {
                        if (HasPermission(player.UserIDString, permAllowed))
                        {
                            var request = args[1].ToLower();
                            bool found = false;
                            foreach (var cat in allowedCats)
                            {
                                if (cat.Key.ToLower() == request)
                                {
                                    foreach (var Entry in data.PlayerRankData)
                                    {
                                        if (request == "pvpkills") data.PlayerRankData[Entry.Key].PVPKills = 0;
                                        if (request == "pvpdistance") data.PlayerRankData[Entry.Key].PVPDistance = 0;
                                        if (request == "pvekills") data.PlayerRankData[Entry.Key].PVEKills = 0;
                                        if (request == "pvedistance") data.PlayerRankData[Entry.Key].PVEDistance = 0;
                                        if (request == "npckills") data.PlayerRankData[Entry.Key].NPCKills = 0;
                                        if (request == "npcdistance") data.PlayerRankData[Entry.Key].NPCDistance = 0;
                                        if (request == "sleeperskilled") data.PlayerRankData[Entry.Key].SleepersKilled = 0;
                                        if (request == "headshots") data.PlayerRankData[Entry.Key].HeadShots = 0;
                                        if (request == "deaths") data.PlayerRankData[Entry.Key].Deaths = 0;
                                        if (request == "suicides") data.PlayerRankData[Entry.Key].Suicides = 0;
                                        if (request == "kdr") data.PlayerRankData[Entry.Key].KDR = 0;
                                        if (request == "sdr") data.PlayerRankData[Entry.Key].SDR = 0;
                                        if (request == "skullscrushed") data.PlayerRankData[Entry.Key].SkullsCrushed = 0;
                                        if (request == "timeswounded") data.PlayerRankData[Entry.Key].TimesWounded = 0;
                                        if (request == "timeshealed") data.PlayerRankData[Entry.Key].TimesHealed = 0;
                                        if (request == "helihits") data.PlayerRankData[Entry.Key].HeliHits = 0;
                                        if (request == "helikills") data.PlayerRankData[Entry.Key].HeliKills = 0;
                                        if (request == "apchits") data.PlayerRankData[Entry.Key].APCHits = 0;
                                        if (request == "apckills") data.PlayerRankData[Entry.Key].APCKills = 0;
                                        if (request == "barrelsdestroyed") data.PlayerRankData[Entry.Key].BarrelsDestroyed = 0;
                                        if (request == "explosivesthrown") data.PlayerRankData[Entry.Key].ExplosivesThrown = 0;
                                        if (request == "arrowsfired") data.PlayerRankData[Entry.Key].ArrowsFired = 0;
                                        if (request == "bulletsfired") data.PlayerRankData[Entry.Key].BulletsFired = 0;
                                        if (request == "rocketslaunched") data.PlayerRankData[Entry.Key].RocketsLaunched = 0;
                                        if (request == "weapontrapsdestroyed") data.PlayerRankData[Entry.Key].WeaponTrapsDestroyed = 0;
                                        if (request == "dropslooted") data.PlayerRankData[Entry.Key].DropsLooted = 0;
                                        if (request == "structuresbuilt") data.PlayerRankData[Entry.Key].StructuresBuilt = 0;
                                        if (request == "structuresdemolished") data.PlayerRankData[Entry.Key].StructuresDemolished = 0;
                                        if (request == "itemsdeployed") data.PlayerRankData[Entry.Key].ItemsDeployed = 0;
                                        if (request == "itemscrafted") data.PlayerRankData[Entry.Key].ItemsCrafted = 0;
                                        if (request == "entitiesrepaired") data.PlayerRankData[Entry.Key].EntitiesRepaired = 0;
                                        if (request == "resourcesgathered") data.PlayerRankData[Entry.Key].ResourcesGathered = 0;
                                        if (request == "structuresupgraded") data.PlayerRankData[Entry.Key].StructuresUpgraded = 0;
                                    }
                                    found = true;
                                    break;
                                }
                            }
                            if (found ==true)
                            {
                                PRData.WriteObject(data);
                                SetUp();
                                SendReply(player, conf.GUI.fontColor1 + lang.GetMessage("title", this) + "</color>" + lang.GetMessage("category", this));
                            }
                            else
                                SendReply(player, conf.GUI.fontColor1 + lang.GetMessage("title", this) + "</color>" + lang.GetMessage("nocategory", this));
                        }
                    }
                    return;
            }
        }
 
        bool BroadcastMethod(String category)
        {
            var dictToUse = data.PlayerRankData;
            int amount = conf.Options.TimedTopListAmount;
            if (conf.Options.allowadmin == false)
            {
                dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
            }
            Dictionary<string, object> top = dictToUse.OrderByDescending(pair => pair.Value.GetValue(category)).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.GetValue(category));
            top = top.Where(kvp =>  Convert.ToDouble(kvp.Value) > 0).ToDictionary(x => x.Key, x => x.Value);
            if (top.Count > 0)
            {
                var outMsg = conf.GUI.fontColor1 + lang.GetMessage("title", this) + "</color>" + conf.GUI.fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage(category, this) + "</color> \n";
                foreach (var name in top)
                {
                    outMsg += string.Format(conf.GUI.fontColor3 + "{0} : " + "</color>" + conf.GUI.fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                }
                if (outMsg != "")
                Server.Broadcast($"<size={conf.Options.TimedTopListSize}>{outMsg}</size>");
                return true;
            }
            return false;
        }
        
        bool IsAuth(BasePlayer player)  
        {
            return (player.net.connection != null && player.net.connection.authLevel == 2); 
        }
        
        public static string RemoveSurrogatePairs(string str, string replacementCharacter = "?")
        {
            if (str == null) return null;
        
            StringBuilder sb = null;
        
            for (int i = 0; i < str.Length; i++)
            {
                char ch = str[i];
                if (char.IsSurrogate(ch))
                {
                    if (sb == null)
                        sb = new StringBuilder(str, 0, i, str.Length);
        
                    sb.Append(replacementCharacter);
        
                    if (i + 1 < str.Length && char.IsHighSurrogate(ch) && char.IsLowSurrogate(str[i + 1]))
                        i++;
                }
                else if (sb != null)
                sb.Append(ch);
            }
            return sb == null ? str : sb.ToString();
        }
   
   
        Core.MySql.Libraries.MySql Sql = Interface.Oxide.GetLibrary<Core.MySql.Libraries.MySql>();
        Core.Database.Connection Sql_conn;
        
        void LoadMySQL(bool wipe) 
        {
            try { Sql_conn.Con.Open(); }
            catch (Exception e) { PrintWarning(e.Message); return; }

            try
            {
                if (Sql_conn == null || Sql_conn.Con == null) 
                {
                    Puts("Player Ranks MySQL connection has failed. Please check your credentials.");     
                    return; 
                }
                if (wipe && conf.MySQL.autoWipe)
                {    
                    Sql.Insert(Core.Database.Sql.Builder.Append($"DROP TABLE IF EXISTS {conf.MySQL.tablename}"), Sql_conn);
                    Puts("Player Ranks MySQL Table Was Dropped."); 
                }

                Sql.Insert(Core.Database.Sql.Builder.Append("SET NAMES utf8mb4"), Sql_conn);
                Sql.Insert(Core.Database.Sql.Builder.Append($"CREATE TABLE IF NOT EXISTS {conf.MySQL.tablename} ( `UserID` VARCHAR(17) NOT NULL, `Name` LONGTEXT NOT NULL, `PVPKills` INT(11) NOT NULL, `PVPDistance` DOUBLE NOT NULL, `PVEKills` INT(11) NOT NULL, `PVEDistance` DOUBLE NOT NULL, `NPCKills` INT(11) NOT NULL, `NPCDistance` DOUBLE NOT NULL, `SleepersKilled` INT(11) NOT NULL, `HeadShots` Int(11) NOT NULL, `Deaths` INT(11) NOT NULL, `Suicides` INT(11) NOT NULL, `KDR` DOUBLE NOT NULL, `SDR` DOUBLE NOT NULL, `SkullsCrushed` INT(11) NOT NULL, `TimesWounded` INT(11) NOT NULL, `TimesHealed` INT(11) NOT NULL, `HeliHits` INT(11) NOT NULL, `HeliKills` INT(11) NOT NULL, `APCHits` INT(11) NOT NULL, `APCKills` INT(11) NOT NULL, `BarrelsDestroyed` INT(11) NOT NULL, `ExplosivesThrown` INT(11) NOT NULL, `ArrowsFired` INT(11) NOT NULL, `BulletsFired` INT(11) NOT NULL, `RocketsLaunched` INT(11) NOT NULL, `WeaponTrapsDestroyed` INT(11) NOT NULL, `DropsLooted` Int(11) NOT NULL,  `StructuresBuilt` INT(11) NOT NULL, `StructuresDemolished` INT(11) NOT NULL, `ItemsDeployed` INT(11) NOT NULL, `ItemsCrafted` INT(11) NOT NULL, `EntitiesRepaired` INT(11) NOT NULL, `ResourcesGathered` INT(11) NOT NULL, `StructuresUpgraded` INT(11) NOT NULL, `Status` VARCHAR(11) NOT NULL, `TimePlayed` TIME NOT NULL, `Admin` BOOLEAN NOT NULL, `Economics` INT(11) NOT NULL, `ActiveDate` DateTime NOT NULL, PRIMARY KEY (`UserID`))DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin;"), Sql_conn);
                Sql.Insert(Core.Database.Sql.Builder.Append($"CREATE TABLE IF NOT EXISTS {conf.MySQL.LBtableName} ( `Date` DateTime NOT NULL,`PVPKillsName` LONGTEXT NOT NULL,`PVPKills` INT(11) NOT NULL,`PVPDistanceName` LONGTEXT NOT NULL,`PVPDistance` DOUBLE NOT NULL,`PVEKillsName` LONGTEXT NOT NULL,`PVEKills` INT(11) NOT NULL,`PVEDistanceName` LONGTEXT NOT NULL,`PVEDistance` DOUBLE NOT NULL,`NPCKillsName` LONGTEXT NOT NULL,`NPCKills` INT(11) NOT NULL,`NPCDistanceName` LONGTEXT NOT NULL,`NPCDistance` DOUBLE NOT NULL,`SleepersKilledName` LONGTEXT NOT NULL,`SleepersKilled` INT(11) NOT NULL,`HeadShotsName` LONGTEXT NOT NULL,`HeadShots` Int(11) NOT NULL,`DeathsName` LONGTEXT NOT NULL,`Deaths` INT(11) NOT NULL,`SuicidesName` LONGTEXT NOT NULL,`Suicides` INT(11) NOT NULL,`KDRName` LONGTEXT NOT NULL,`KDR` DOUBLE NOT NULL,`SDRName` LONGTEXT NOT NULL,`SDR` DOUBLE NOT NULL,`SkullsCrushedName` LONGTEXT NOT NULL,`SkullsCrushed` INT(11) NOT NULL,`TimesWoundedName` LONGTEXT NOT NULL,`TimesWounded` INT(11) NOT NULL,`TimesHealedName` LONGTEXT NOT NULL,`TimesHealed` INT(11) NOT NULL,`HeliHitsName` LONGTEXT NOT NULL,`HeliHits` INT(11) NOT NULL,`HeliKillsName` LONGTEXT NOT NULL,`HeliKills` INT(11) NOT NULL,`APCHitsName` LONGTEXT NOT NULL,`APCHits` INT(11) NOT NULL,`APCKillsName` LONGTEXT NOT NULL,`APCKills` INT(11) NOT NULL,`BarrelsDestroyedName` LONGTEXT NOT NULL,`BarrelsDestroyed` INT(11) NOT NULL,`ExplosivesThrownName` LONGTEXT NOT NULL,`ExplosivesThrown` INT(11) NOT NULL,`ArrowsFiredName` LONGTEXT NOT NULL,`ArrowsFired` INT(11) NOT NULL,`BulletsFiredName` LONGTEXT NOT NULL,`BulletsFired` INT(11) NOT NULL,`RocketsLaunchedName` LONGTEXT NOT NULL,`RocketsLaunched` INT(11) NOT NULL,`WeaponTrapsDestroyedName` LONGTEXT NOT NULL,`WeaponTrapsDestroyed` INT(11) NOT NULL,`DropsLootedName` LONGTEXT NOT NULL,`DropsLooted` Int(11) NOT NULL,`StructuresBuiltName` LONGTEXT NOT NULL,`StructuresBuilt` INT(11) NOT NULL,`StructuresDemolishedName` LONGTEXT NOT NULL,`StructuresDemolished` INT(11) NOT NULL,`ItemsDeployedName` LONGTEXT NOT NULL,`ItemsDeployed` INT(11) NOT NULL,`ItemsCraftedName` LONGTEXT NOT NULL,`ItemsCrafted` INT(11) NOT NULL,`EntitiesRepairedName` LONGTEXT NOT NULL,`EntitiesRepaired` INT(11) NOT NULL,`ResourcesGatheredName` LONGTEXT NOT NULL,`ResourcesGathered` INT(11) NOT NULL,`StructuresUpgradedName` LONGTEXT NOT NULL,`StructuresUpgraded` INT(11) NOT NULL,PRIMARY KEY (`Date`))DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin;"), Sql_conn);
            } 
            catch (Exception e)
            {
                Puts("Player Ranks did not succesfully create a table.");     
            }

            try
            {
                foreach(var c in data.PlayerRankData)   
                {
                    Sql.Insert(Core.Database.Sql.Builder.Append($"INSERT INTO {conf.MySQL.tablename} ( `UserID`, `Name`, `PVPKills`, `PVPDistance`, `PVEKills`, `PVEDistance`, `NPCKills`, `NPCDistance`, `SleepersKilled`, `Headshots`, `Deaths`, `Suicides`, `KDR`, `SDR`, `SkullsCrushed`, `TimesWounded`, `TimesHealed`, `HeliHits`, `HeliKills`, `APCHits`, `APCKills`, `BarrelsDestroyed`, `ExplosivesThrown`, `ArrowsFired`, `BulletsFired`, `RocketsLaunched`, `WeaponTrapsDestroyed`, `DropsLooted`, `StructuresBuilt`, `StructuresDemolished`, `ItemsDeployed`, `ItemsCrafted`, `EntitiesRepaired`, `ResourcesGathered`, `StructuresUpgraded`, `Status`, `TimePlayed`, `Admin`, `Economics`, `ActiveDate`) VALUES ( @0, @1, @2, @3, @4, @5, @6, @7, @8, @9, @10, @11, @12, @13, @14, @15, @16, @17, @18, @19, @20, @21, @22, @23, @24, @25, @26, @27, @28, @29, @30, @31, @32, @33, @34, @35, @36, @37, @38, @39) ON DUPLICATE KEY UPDATE UserID = @0, Name = @1, PVPKills = @2, PVPDistance = @3, PVEKills = @4, PVEDistance = @5, NPCKills = @6, NPCDistance = @7,SleepersKilled = @8, HeadShots = @9, Deaths = @10, Suicides = @11, KDR = @12, SDR = @13, SkullsCrushed = @14, TimesWounded = @15, TimesHealed = @16, HeliHits = @17, HeliKills = @18, APCHits = @19, APCKills = @20, BarrelsDestroyed = @21, ExplosivesThrown = @22, ArrowsFired = @23, BulletsFired = @24, RocketsLaunched = @25, WeaponTrapsDestroyed = @26, DropsLooted = @27, StructuresBuilt = @28, StructuresDemolished = @29, ItemsDeployed = @30, ItemsCrafted = @31, EntitiesRepaired = @32, ResourcesGathered = @33, StructuresUpgraded = @34, Status = @35, TimePlayed = @36, Admin = @37, Economics = @38, ActiveDate = @39;", c.Value.UserID, RemoveSurrogatePairs(c.Value.Name, ""), c.Value.PVPKills, c.Value.PVPDistance, c.Value.PVEKills, c.Value.PVEDistance, c.Value.NPCKills, c.Value.NPCDistance, c.Value.SleepersKilled, c.Value.HeadShots, c.Value.Deaths, c.Value.Suicides, c.Value.KDR, c.Value.SDR, c.Value.SkullsCrushed, c.Value.TimesWounded, c.Value.TimesHealed, c.Value.HeliHits, c.Value.HeliKills, c.Value.APCHits, c.Value.APCKills, c.Value.BarrelsDestroyed, c.Value.ExplosivesThrown, c.Value.ArrowsFired, c.Value.BulletsFired, c.Value.RocketsLaunched, c.Value.WeaponTrapsDestroyed, c.Value.DropsLooted, c.Value.StructuresBuilt, c.Value.StructuresDemolished, c.Value.ItemsDeployed, c.Value.ItemsCrafted, c.Value.EntitiesRepaired, c.Value.ResourcesGathered, c.Value.StructuresUpgraded, c.Value.Status, c.Value.TimePlayed, c.Value.Admin, c.Value.Economics, c.Value.ActiveDate), Sql_conn);
                }

                foreach(var c in data.leaderBoards)   
                {
                    Sql.Insert(Core.Database.Sql.Builder.Append($"INSERT INTO {conf.MySQL.LBtableName} ( `Date`,`PVPKillsName`, `PVPKills`, `PVPDistanceName`, `PVPDistance`, `PVEKillsName`, `PVEKills`, `PVEDistanceName`, `PVEDistance`, `NPCKillsName`, `NPCKills`, `NPCDistanceName`, `NPCDistance`, `SleepersKilledName`, `SleepersKilled`, `HeadshotsName`,`Headshots`,`DeathsName`, `Deaths`, `SuicidesName`, `Suicides`, `KDRName`, `KDR`, `SDRName`,`SDR`,`SkullsCrushedName`, `SkullsCrushed`, `TimesWoundedName`, `TimesWounded`, `TimesHealedName`, `TimesHealed`, `HeliHitsName`,`HeliHits`,`HeliKillsName`, `HeliKills`, `APCHitsName`, `APCHits`, `APCKillsName`, `APCKills`, `BarrelsDestroyedName`,`BarrelsDestroyed`,`ExplosivesThrownName`, `ExplosivesThrown`, `ArrowsFiredName`, `ArrowsFired`, `BulletsFiredName`, `BulletsFired`, `RocketsLaunchedName`,`RocketsLaunched`,`WeaponTrapsDestroyedName`, `WeaponTrapsDestroyed`, `DropsLootedName`, `DropsLooted`, `StructuresBuiltName`, `StructuresBuilt`, `StructuresDemolishedName`,`StructuresDemolished`,`ItemsDeployedName`, `ItemsDeployed`, `ItemsCraftedName`, `ItemsCrafted`, `EntitiesRepairedName`, `EntitiesRepaired`, `ResourcesGatheredName`,`ResourcesGathered`,`StructuresUpgradedName`, `StructuresUpgraded`) VALUES ( @0, @1, @2, @3, @4, @5, @6, @7, @8, @9, @10, @11, @12, @13, @14, @15, @16, @17, @18, @19, @20, @21, @22, @23, @24, @25, @26, @27, @28, @29, @30, @31, @32, @33, @34, @35, @36, @37, @38, @39, @40, @41, @42, @43, @44, @45, @46, @47, @48, @49, @50, @51, @52, @53, @54, @55, @56, @57, @58, @59, @60, @61, @62, @63, @64, @65, @66 ) ON DUPLICATE KEY UPDATE Date = @0, PVPKillsName = @1, PVPKills = @2, PVPDistanceName = @3, PVPDistance = @4, PVEKillsName = @5, PVEKills = @6, PVEDistanceName = @7,PVEDistance = @8,NPCKillsName = @9, NPCKills = @10, NPCDistanceName = @11, NPCDistance = @12, SleepersKilledName = @13, SleepersKilled = @14, HeadshotsName = @15, Headshots = @16,DeathsName = @17, Deaths = @18, SuicidesName = @19, Suicides = @20, KDRName = @21, KDR = @22, SDRName = @23, SDR = @24,SkullsCrushedName = @25, SkullsCrushed = @26, TimesWoundedName = @27, TimesWounded = @28, TimesHealedName = @29, TimesHealed = @30, HeliHitsName = @31, HeliHits = @32,HeliKillsName = @33, HeliKills = @34, APCHitsName = @35, APCHits = @36, APCKillsName = @37, APCKills = @38, BarrelsDestroyedName = @39, BarrelsDestroyed = @40,ExplosivesThrownName = @41, ExplosivesThrown = @42, ArrowsFiredName = @43, ArrowsFired = @44, BulletsFiredName = @45, BulletsFired = @46, RocketsLaunchedName = @47, RocketsLaunched = @48,WeaponTrapsDestroyedName = @49, WeaponTrapsDestroyed = @50, DropsLootedName = @51, DropsLooted = @52, StructuresBuiltName = @53, StructuresBuilt = @54, StructuresDemolishedName = @55, StructuresDemolished = @56,ItemsDeployedName = @57, ItemsDeployed = @58, ItemsCraftedName = @59, ItemsCrafted = @60, EntitiesRepairedName = @61, EntitiesRepaired = @62, ResourcesGatheredName = @63, ResourcesGathered = @64,StructuresUpgradedName = @65, StructuresUpgraded = @66;",c.Key,RemoveSurrogatePairs(c.Value["PVPKills"].UserName, ""),c.Value["PVPKills"].Score,RemoveSurrogatePairs(c.Value["PVPDistance"].UserName, ""),c.Value["PVPDistance"].Score,RemoveSurrogatePairs(c.Value["PVEKills"].UserName, ""),c.Value["PVEKills"].Score,RemoveSurrogatePairs(c.Value["PVEDistance"].UserName, ""),c.Value["PVEDistance"].Score,RemoveSurrogatePairs(c.Value["NPCKills"].UserName, ""),c.Value["NPCKills"].Score,RemoveSurrogatePairs(c.Value["NPCDistance"].UserName, ""),c.Value["NPCDistance"].Score,RemoveSurrogatePairs(c.Value["SleepersKilled"].UserName, ""),c.Value["SleepersKilled"].Score,RemoveSurrogatePairs(c.Value["HeadShots"].UserName, ""),c.Value["HeadShots"].UserName,RemoveSurrogatePairs(c.Value["Deaths"].UserName, ""),c.Value["Deaths"].Score,RemoveSurrogatePairs(c.Value["Suicides"].UserName, ""),c.Value["Suicides"].Score,RemoveSurrogatePairs(c.Value["KDR"].UserName, ""),c.Value["KDR"].Score,RemoveSurrogatePairs(c.Value["SDR"].UserName, ""),c.Value["SDR"].Score,RemoveSurrogatePairs(c.Value["SkullsCrushed"].UserName, ""),c.Value["SkullsCrushed"].Score,RemoveSurrogatePairs(c.Value["TimesWounded"].UserName, ""),c.Value["TimesWounded"].Score,RemoveSurrogatePairs(c.Value["TimesHealed"].UserName, ""),c.Value["TimesHealed"].Score,RemoveSurrogatePairs(c.Value["HeliHits"].UserName, ""),c.Value["HeliHits"].Score,RemoveSurrogatePairs(c.Value["HeliKills"].UserName, ""),c.Value["HeliKills"].Score,RemoveSurrogatePairs(c.Value["APCHits"].UserName, ""),c.Value["APCHits"].Score,RemoveSurrogatePairs(c.Value["APCKills"].UserName, ""),c.Value["APCKills"].Score,RemoveSurrogatePairs(c.Value["BarrelsDestroyed"].UserName, ""),c.Value["BarrelsDestroyed"].Score,RemoveSurrogatePairs(c.Value["ExplosivesThrown"].UserName, ""),c.Value["ExplosivesThrown"].Score,RemoveSurrogatePairs(c.Value["ArrowsFired"].UserName, ""),c.Value["ArrowsFired"].Score,RemoveSurrogatePairs(c.Value["BulletsFired"].UserName, ""),c.Value["BulletsFired"].Score,RemoveSurrogatePairs(c.Value["RocketsLaunched"].UserName, ""),c.Value["RocketsLaunched"].Score,RemoveSurrogatePairs(c.Value["WeaponTrapsDestroyed"].UserName, ""),c.Value["WeaponTrapsDestroyed"].Score,RemoveSurrogatePairs(c.Value["DropsLooted"].UserName, ""),c.Value["DropsLooted"].Score,RemoveSurrogatePairs(c.Value["StructuresBuilt"].UserName, ""),c.Value["StructuresBuilt"].Score,RemoveSurrogatePairs(c.Value["StructuresDemolished"].UserName, ""),c.Value["StructuresDemolished"].Score,RemoveSurrogatePairs(c.Value["ItemsDeployed"].UserName, ""),c.Value["ItemsDeployed"].Score,RemoveSurrogatePairs(c.Value["ItemsCrafted"].UserName, ""),c.Value["ItemsCrafted"].Score,RemoveSurrogatePairs(c.Value["EntitiesRepaired"].UserName, ""),c.Value["EntitiesRepaired"].Score,RemoveSurrogatePairs(c.Value["ResourcesGathered"].UserName, ""),c.Value["ResourcesGathered"].Score,RemoveSurrogatePairs(c.Value["StructuresUpgraded"].UserName, ""),c.Value["StructuresUpgraded"].Score), Sql_conn);
                }
                Puts("Player Ranks MySQL Table Was Saved.");
            }
            catch (Exception e)  
            {
                Puts("Player Ranks did not succesfully save data to SQL.");   
            }

        }     
        #endregion

        #region config
        private ConfigData conf;
        public class ConfigData
        {
            public Options Options = new Options();
            public GUI GUI = new GUI();
            public Categories Categories = new Categories();
            public MySQL MySQL = new MySQL();
        }
        public class Options
        {
            public bool useFriendsAPI;
            public bool useClans;
            public bool useRustIO;
            public bool blockEvents = true;
            public bool useIntenseOptions = true;
            public int TimedTopListTimer = 10;
            public int TimedTopListAmount = 3;
            public bool useTimedTopList = true;
            public int TimedTopListSize = 12;
            public int saveTimer = 30;
            public string chatCommandAlias = "ranks";
            public bool allowadmin;
            public bool statCollection = true;
            public int lastLoginLimit;
        }
        
        public class GUI
        {
            public string fontColor1 = "<color=orange>";
            public string fontColor2 = "<color=#939393>";
            public string fontColor3 = "<color=white>";
            public string buttonColour = "0.7 0.32 0.17 1";
            public double guitransparency = 0.5;            
        }
        public class Categories
        {
            public bool usepvpkills = true;
            public bool usepvpdistance = true;
            public bool usepvekills = true;
            public bool usepvedistance = true;
            public bool usenpckills = true;
            public bool usenpcdistance = true;
            public bool usesleeperskilled = true;
            public bool useheadshots = true;
            public bool usedeaths = true;
            public bool usesuicides = true;
            public bool usekdr = true;
            public bool usesdr = true;
            public bool useskullscrushed = true;
            public bool usetimeswounded = true;
            public bool usetimeshealed = true;
            public bool usehelihits = true;
            public bool usehelikills = true;
            public bool useapchits = true;
            public bool useapckills = true;
            public bool usebarrelsdestroyed = true;
            public bool useexplosivesthrown = true;
            public bool usearrowsfired = true;
            public bool usebulletsfired = true;
            public bool userocketslaunched = true;
            public bool useweapontrapsdestroyed = true;
            public bool usedropslooted = true;
            public bool usestructuresbuilt = true;
            public bool usestructuresdemolished = true;
            public bool useitemsdeployed = true;
            public bool useitemscrafted = true;
            public bool useentitiesrepaired = true;
            public bool useresourcesgathered = true;
            public bool usestructuresupgraded = true;
        }
            
        public class MySQL
        {
            public bool useMySQL;
            public string sql_host = "";
            public int sql_port = 3306;
            public string sql_db = "";
            public string sql_user = "";
            public string sql_pass = "";
            public string tablename = "playerranksdb";
            public string LBtableName = "playerranksLeaderdb";
            public bool autoWipe = true;
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            var config = new ConfigData();
            SaveConfig();
        }
        
        private void LoadConfigVariables()
        {
            conf = Config.ReadObject<ConfigData>();
            SaveConfig(conf);
        }
        
        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        #endregion

        #region classes and data storage

        void SaveData(bool sql) 
        {
            var banlist = new List<ulong>();
            foreach(var entry in data.PlayerRankData)   
            {
                if (ServerUsers.Is(entry.Key, ServerUsers.UserGroup.Banned))
                {
                    banlist.Add(entry.Key);
                }
                entry.Value.Status = "offline";
            }
            foreach (var banned in banlist)
                if (data.PlayerRankData.ContainsKey(banned))
                    data.PlayerRankData.Remove(banned);
                    
            DateTime cutoff = DateTime.UtcNow.Subtract(TimeSpan.FromDays(conf.Options.lastLoginLimit));
            if (conf.Options.lastLoginLimit > 0)
            data.PlayerRankData = data.PlayerRankData.Where(x=>x.Value.ActiveDate > cutoff).ToDictionary(x=>x.Key,x=>x.Value);

            foreach(BasePlayer player in BasePlayer.activePlayerList)
                if (data.PlayerRankData.ContainsKey(player.userID))
                    UpdatePlayer(player);

            PRData.WriteObject(data);
            if (conf.MySQL.useMySQL && sql)
                LoadMySQL(false); 
        }

        void LoadData()
        {
            try
            {
                data = Interface.GetMod().DataFileSystem.ReadObject<DataStorage>("PlayerRanks");
                PRData.WriteObject(data);//forces to conform immediately if structure has changed
            }
            catch
            {
                data = new DataStorage();
            }
        }
        #endregion

        #region messages

        readonly Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"title", "PlayerRanks: " }, 
            {"wipe", "PlayerRanks database wiped."},
            {"nowipe", "PlayerRanks database was already empty."},
            {"save", "PlayerRanks database saved."},
            {"del", "PlayerRanks for this player were wiped."}, 
            {"bestHits", "Top " },
            {"dbremoved", "Details for this ID have been removed." },           
            {"noentry", "There is no entry in the database for this ID." },
            {"syntax", "ID must be 17 digits." },
            {"category", "Stats for this category have been removed." },
            {"nocategory", "This is not a recognised category." },           
            {"noResults", "There are no statistics for this category." },
            {"disabled", "This category has been disabled." },
            {"leaderboard", "Leader Board" },
            {"categories", "Categories" },
            {"close", "Close" },
            {"mystats", "My Stats" },
            {"admin", "Admin" },
            
            
            {"gatherStatsOnButton", "Gather Stats - On" },
            {"gatherStatsOffButton", "Gather Stats - Off" },
            {"disableAdminStatsButton", "Disable Admin Stats" },
            {"AllowAdminStatsButton", "Allow Admin Stats" },
            {"savePlayerDataButton", "Save Player Data" },
            {"wipePlayerDataButton", "Wipe Player Data" },
            {"confirmbutton", "Confirm" },
            {"saveLeaderBoardButton", "Save Leaderboard" },
            {"wipeLeaderBoardButton", "Wipe Leaderboards" },
            {"on", "On" },
            {"off", "Off" },

            {"PVPKills", "PVP Kills " }, 
            {"PVPDistance", "PVP Distance " },
            {"PVEKills", "PVE Kills " },
            {"PVEDistance", "PVE Distance " },
            {"NPCKills", "NPC Kills " },
            {"NPCDistance", "NPC Distance " },
            {"SleepersKilled", "Sleepers Killed " },   
            {"HeadShots", "Head Shots " },
            {"Deaths", "Deaths " },
            {"Suicides", "Suicides " },
            {"KDR", "KDR " },
            {"SDR", "SDR " },
            {"SkullsCrushed", "Skulls Crushed " },
            {"TimesWounded", "Times Wounded " },
            {"TimesHealed", "Times Healed " },
            {"HeliHits", "Heli Hits " },
            {"HeliKills", "Heli Kills " },
            {"APCHits", "APC Hits " },
            {"APCKills", "APC Kills " },
            {"BarrelsDestroyed", "Barrels Destroyed " },
            {"ExplosivesThrown", "Explosives Thrown " },
            {"ArrowsFired", "Arrows Fired " },
            {"BulletsFired", "Bullets Fired " },
            {"RocketsLaunched", "Rockets Launched " },
            {"WeaponTrapsDestroyed", "Weapon Traps Destroyed " },
            {"DropsLooted", "Airdrops Looted " },

            //intense options
            {"StructuresBuilt", "Structures Built " },
            {"StructuresDemolished", "Structures Demolished " },
            {"ItemsDeployed", "Items Deployed " },
            {"ItemsCrafted", "Items Crafted " },
            {"EntitiesRepaired", "Entities Repaired " },
            {"ResourcesGathered", "Resources Gathered " },
            {"StructuresUpgraded", "Structures Upgraded " }, 
        };
        #endregion
    }
}