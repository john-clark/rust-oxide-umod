using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using UnityEngine;

using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
	[Info("BradleyControl", "Mattparks", "0.2.2", ResourceId = 2611)]
	[Description("A plugin that controls Bradley properties.")]
	class BradleyControl : RustPlugin 
	{
		#region Fields
	   
		FieldInfo tooHotUntil = typeof(HelicopterDebris).GetField("tooHotUntil", (BindingFlags.Instance | BindingFlags.NonPublic));
		
		private System.Random random = new System.Random();
        private List<BradleyAPC> bradleys = new List<BradleyAPC>();
        private List<FireBall> fireBalls = new List<FireBall>();
        private List<HelicopterDebris> gibs = new List<HelicopterDebris>();
        private List<LockedByEntCrate> lockedCrates = new List<LockedByEntCrate>();
        private float lastBradleyDeath = 0.0f;

        #endregion

        #region Configuration

        public class Options
		{	 
			public bool bradleyEnabled = true; // Enables Bradley spawning.
			public float respawnDelay = 5.0f; // The delay (minutes) before respawning the Bradley after its death.
			public float startHealth = 1000.0f; // How much health the Bradley starts with.
			public float maxTurretRange = 100.0f; // The range of the turrets.
			public float gunAccuracy = 1.0f; // The guns accuracy (scale of 0 to 1).
			public float gunDamage = 1.0f; // The guns damage (scale of 0 to 1).
			public float speed = 1.0f; // The speed of movement.
			public bool targetsNakeds = true; // If the Bradley will only target if you have explosives, more than 2 clothing or a radsuit, or a weapon better than a crossbow.
			public bool ammoDoesDamage = false; // If ammo does damage to the Bradley.
			public int maxLootCrates = 2; // How many loot boxes are dropped.
			public bool customLootTables = false; // If the custom loot tables will be used.
			public bool enableNapalm = true; // If there is napalm at all after death (makes boxes unlocked).
            public int napamWaterRequired = -1; // How much water is needed to extengwish the napalm.
            public float lootAccessDelay = -1.0f; // How long (in seconds) before the dropped boxes can be accessed.
			public bool enableGibs = true; // If the Bradley will drop parts on destruction.
			public float gibsHotDelay = -1.0f; // How long (in seconds) the gibs parts will be unminable and 'hot'.
			public float gibsHealth = 500.0f; // How much health the gibs parts will have, more health will give more resources.
			
			public bool disableStartSearch = false; // Some dumb thing causing people issues.
		}
		
		public class LootTables
		{
			public Dictionary<string, string[]> bradleyCrate = new Dictionary<string, string[]>();
		}
		
		public class Targetable
		{
			public int mostClothing = 2;
			public List<string> targetable = new List<string>();
			public List<string> nonTargetable = new List<string>();
		}

		public class ConfigData
		{
			public Options options = new Options();
			public LootTables lootTables = new LootTables();
			public Targetable targetable = new Targetable();
		}

		private ConfigData configs;

		private void LoadDefaultConfig()
		{
			Puts("Creating a new config file!"); 
			configs = new ConfigData();
			SaveConfig();
		}

		private void LoadVariables()
		{
			configs = Config.ReadObject<ConfigData>();
			
			if (configs == null)
			{
				LoadDefaultConfig();
			}
			
			if (configs.options.customLootTables && configs.lootTables.bradleyCrate.Count == 0)
			{
				configs.lootTables.bradleyCrate = new Dictionary<string, string[]>() {
				//	{"explosive.timed", new string[]{ "1", "0", "1" } },
					{ "ammo.rocket.basic", new string[]{ "1", "0", "1" } },
					{ "ammo.rocket.fire", new string[]{ "1", "0", "2" } },
					{ "ammo.rocket.hv", new string[]{ "1", "0", "2" } },
					{ "ammo.rifle", new string[]{ "100", "0", "3" } },
					{ "ammo.pistol", new string[]{ "100", "0", "3" } },
					{ "ammo.rifle.incendiary", new string[]{ "100", "0", "3" } },
					{ "ammo.rifle.explosive", new string[]{ "100", "0", "3" } },
					{ "ammo.rifle.hv", new string[]{ "100", "0", "3" } },
					{ "ammo.pistol.fire", new string[]{ "100", "0", "3" } },
					{ "ammo.pistol.hv", new string[]{ "100", "0", "3" } },
					{ "lmg.m249", new string[]{ "1", "0", "1" } },
					{ "pistol.m92", new string[]{ "1", "0", "2" } },
					{ "rifle.ak", new string[]{ "1", "0", "2" } },
					{ "smg.2", new string[]{ "1", "0", "2" } },
					{ "smg.mp5", new string[]{ "1", "0", "2" } },
					{ "weapon.mod.holosight", new string[]{ "1", "0", "3" } },
					{ "weapon.mod.silencer", new string[]{ "1", "0", "3" } },
					{ "weapon.mod.flashlight", new string[]{ "1", "0", "3" } },
					{ "weapon.mod.lasersight", new string[]{ "1", "0", "3" } },
					{ "weapon.mod.small.scope", new string[]{ "1", "0", "3" } },
					{ "targeting.computer", new string[]{ "1", "0", "3" } },
					{ "cctv.camera", new string[]{ "1", "0", "3" } },
				};
			}
			
			if (!configs.options.targetsNakeds && configs.targetable.targetable.Count == 0)
			{
				configs.targetable.targetable = new List<string>() {
					"hazmatsuit",
					"explosive.timed",
					"rocket.launcher"
				};
			}
				
			if (!configs.options.targetsNakeds && configs.targetable.nonTargetable.Count == 0)
			{
				configs.targetable.nonTargetable = new List<string>() {
					"bone.club",
					"knife.bone",
					"bow.hunting",
					"longsword",
					"mace",
					"machete",
					"salvaged.cleaver",
					"salvaged.sword",
					"spear.stone",
					"spear.wooden"
				};
			}
				
			SaveConfig(); 
		}

		private void SaveConfig()
		{
			Config.WriteObject(configs, true);
		}
        
		private void LoadMessages() 
		{
			// English messages.
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["BRADLEY_ABOUT"] = "<color=#ff3b3b>Bradley Control {Version}</color>: by <color=green>mattparks</color>. Bradley Control is a plugin that controls Bradley properties. Use the /bradley command as follows: \n <color=#1586db>•</color> /bradley - Displays Bradley Control about and help. \n <color=#1586db>•</color> /bradley reset - Resets the Bradley in the area. \n <color=#1586db>•</color> /bradley clearGibs - Clears all Bradley gibs/parts. \n <color=#1586db>•</color> /bradley clearFire - Clears all Bradley fire. \n <color=#1586db>•</color> /bradley clearCrates - Clears all Bradley crates. \n <color=#1586db>•</color> /bradley unlockCrates - Unlocks all Bradley crates. \n <color=#1586db>•</color> /bradley clearAll - Clears all Bradley stuff.",
                ["BRADLEY_RESET"] = "<color=#ff3b3b>Resetting the Bradley!</color>",
                ["BRADLEY_RESET_FAIL"] = "<color=#ff3b3b>Failed to reset the Bradley!</color>",
                ["BRADLEY_REMOVE_GIBS"] = "<color=#ff3b3b>Removing Bradley gibs from the world!</color>",
                ["BRADLEY_REMOVE_FIRE"] = "<color=#ff3b3b>Removing Bradley fire from the world!</color>",
                ["BRADLEY_UNLOCK_CRATES"] = "<color=#ff3b3b>Unlocking Bradley crates!</color>",
                ["BRADLEY_REMOVE_CRATES"] = "<color=#ff3b3b>Removing Bradley crates!</color>",
                ["BRADLEY_REMOVE_ALL"] = "<color=#ff3b3b>Removing all Bradleys, parts, fires, and crates!</color>",
                ["BRADLEY_COUNT"] = "There are {Count} Bradleys.",
            }, this, "en"); 
		}
		
		#endregion

		#region Hooks

		private void Init()
		{
			LoadVariables();
			LoadMessages();
			permission.RegisterPermission("bradleycontrol.allow", this);
		}
 
		bool isAllowed(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);

        private void OnServerInitialized()
        {
			if (configs.options.disableStartSearch)
			{
				return;
			}
			
            foreach (var gobject in UnityEngine.Object.FindObjectsOfType<BaseEntity>())
            {
              //  var nearGibs = new List<ServerGib>();
              //  Vis.Entities<ServerGib>(gobject.transform.position, 7.0f, nearGibs);

               // if (nearGibs?.Any(p => (p?.ShortPrefabName).Contains("bradley")) ?? false)
               // {
                    var prefabName = gobject?.ShortPrefabName ?? string.Empty;
                    var bradley = gobject?.GetComponent<BradleyAPC>() ?? null;
                    var debris = gobject?.GetComponent<HelicopterDebris>() ?? null;
                    var fireball = gobject?.GetComponent<FireBall>() ?? null;
                    var crate = gobject?.GetComponent<LockedByEntCrate>() ?? null;

                    if (bradley != null)
                    {
                        bradleys.Add(bradley);
                    }

                    if (crate != null)
                    {
                        lockedCrates.Add(crate);
                    }

                    if (fireball != null && (prefabName.Contains("napalm") || prefabName.Contains("oil")))
                    {
                        fireBalls.Add(fireball);
                    }

                    if (debris != null)
                    {
                        gibs.Add(debris);
                    }
               // }
            }
			
			if (BradleySpawner.singleton != null && configs.options.respawnDelay != -1.0f)
			{
				BradleySpawner.singleton.minRespawnTimeMinutes = configs.options.respawnDelay;
				BradleySpawner.singleton.maxRespawnTimeMinutes = configs.options.respawnDelay;
			}
        }

		private void OnEntitySpawned(BaseNetworkable entity)
        {
            var prefabname = entity.name;

            if (prefabname.Contains("bradleyapc"))
            {
                bradleys.Add(entity as BradleyAPC);
            }

            if (lastBradleyDeath == 0.0f || Time.realtimeSinceStartup - lastBradleyDeath > 2.0f) // TODO: Check if around a Bradley instead.
            {
                lastBradleyDeath = 0.0f;
                return;
            }

            var debris = entity?.GetComponent<HelicopterDebris>() ?? null;
            gibs.Add(debris);

            if (prefabname.Contains("servergibs_bradley"))
			{
				NextTick(() => 
				{ 
					if (debris == null || entity.IsDestroyed) 
					{
						return;
					}
						
					debris.InitializeHealth(configs.options.gibsHealth, configs.options.gibsHealth);
						
					if (configs.options.gibsHotDelay != -1.0f) 
					{
						tooHotUntil.SetValue(debris, Time.realtimeSinceStartup + configs.options.gibsHotDelay);
					}

					if (!configs.options.enableGibs)
					{
						entity.Kill();
					}
					
					debris.SendNetworkUpdate();
                });
			}

			if ((prefabname.Contains("napalm") || prefabname.Contains("oilfireball")) && !prefabname.Contains("rocket"))
            {
                fireBalls.Add(entity as FireBall);

                NextTick(() =>
                {
                    var fireball = entity?.GetComponent<FireBall>() ?? null;
	
					if (fireball == null || (entity?.IsDestroyed ?? true)) 
					{
						return;
					}

                    if (configs.options.lootAccessDelay != -1)
                    {
                        fireball.lifeTimeMin = configs.options.lootAccessDelay - 10.0f;
                        fireball.lifeTimeMax = configs.options.lootAccessDelay + 10.0f;
                    }

                    if (configs.options.napamWaterRequired != -1)
                    {
                        fireball.waterToExtinguish = configs.options.napamWaterRequired;
                    }
						
					if (!configs.options.enableNapalm) 
					{
						fireball.enableSaving = false;
						entity.Kill(); 
					}
					
					fireball.SendNetworkUpdate();
                });
			}
			
			if (prefabname.Contains("bradley_crate"))
            {
                var loot = entity?.GetComponent<LootContainer>() ?? null;
                lockedCrates.Add(entity?.GetComponent<LockedByEntCrate>() ?? null);

                if (configs.options.customLootTables && configs.lootTables.bradleyCrate.Count != 0)
				{
					loot.inventory.itemList.Clear();

					for (int i = 0; i < 6; i++)
					{
						var keys = Enumerable.ToList(configs.lootTables.bradleyCrate.Keys);
						int t = i == 0 ? 1 : i < 3 ? 2 : 3;
						int r = random.Next(keys.Count);
						var val = configs.lootTables.bradleyCrate[keys[r]];
						int j = 0;

						while ((Convert.ToInt32(val[2]) != t || Convert.ToInt32(val[0]) == 0) && j != 20)
						{
							r = random.Next(keys.Count);
							val = configs.lootTables.bradleyCrate[keys[r]];
							j++;
						}
								
						var item = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(keys[r]).itemid, Convert.ToInt32(val[0]), Convert.ToUInt64(val[1]));
						item.MoveToContainer(loot.inventory);
					}
						
					loot.inventory.MarkDirty();
				}

				if (configs.options.lootAccessDelay != -1.0f)
				{
					timer.Once(configs.options.lootAccessDelay, () =>
					{
						var crate = entity?.GetComponent<LockedByEntCrate>() ?? null;

						if (crate == null || (entity?.IsDestroyed ?? true))
                        {
							return;
						}

						var lockingEnt = (crate?.lockingEnt != null) ? crate.lockingEnt.GetComponent<FireBall>() : null;

						if (lockingEnt != null && !(lockingEnt?.IsDestroyed ?? true))
						{
							lockingEnt.enableSaving = false;
							lockingEnt.CancelInvoke(lockingEnt.Extinguish);
							lockingEnt.Invoke(lockingEnt.Extinguish, 30.0f);
						}

						crate.CancelInvoke(crate.Think);
						crate.SetLocked(false);
						crate.lockingEnt = null;
                    });
				}
			}
		}
		
		private void OnEntityDeath(BaseCombatEntity victim, HitInfo info)
		{
			var prefabname = victim.name;

            var crate = victim?.GetComponent<LockedByEntCrate>() ?? null;

            if (crate != null && lockedCrates.Contains(crate))
            {
                lockedCrates.Remove(crate);
            }

            if (prefabname.Contains("servergibs_bradley"))
            {
                var debris = victim?.GetComponent<HelicopterDebris>() ?? null;

                if (debris != null && gibs.Contains(debris))
                {
                    gibs.Remove(debris);
                }
            }

            if (prefabname.Contains("bradleyapc"))
            {
                var bradley = victim?.GetComponent<BradleyAPC>() ?? null;

                if (bradley != null && bradleys.Contains(bradley))
                {
                    bradleys.Remove(bradley);
                }

                lastBradleyDeath = Time.realtimeSinceStartup;
            }
        }
		
		private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
		{
			if (info?.Initiator is BradleyAPC && entity is BasePlayer)
			{
				float rand = (float) random.Next(1, 100) / 100.0f;

				if (configs.options.gunAccuracy < rand)
				{
					return true;
				}
				else
				{
					info.damageTypes.ScaleAll(configs.options.gunDamage);
					return null;
				}
			}

			return null;
		}
		
		private void OnBradleyApcInitialize(BradleyAPC bradley)
		{
			bradley._maxHealth = configs.options.startHealth;
			bradley.health = bradley._maxHealth;
			bradley.viewDistance = configs.options.maxTurretRange;
			bradley.searchRange = configs.options.maxTurretRange;
			bradley.throttle = configs.options.speed; // TODO: Ensure Bradley speed.
			bradley.leftThrottle = bradley.throttle;
			bradley.rightThrottle = bradley.throttle;
			bradley.maxCratesToSpawn = configs.options.maxLootCrates;

            // Ensures there is a AI path to follow.
            Vector3 position = BradleySpawner.singleton.path.interestZones[UnityEngine.Random.Range(0, BradleySpawner.singleton.path.interestZones.Count)].transform.position;
            bradley.transform.position = position;
            bradley.DoAI = true;
            bradley.DoSimpleAI();
            bradley.InstallPatrolPath(BradleySpawner.singleton.path);

            if (!configs.options.bradleyEnabled)
			{
				bradley.Kill(); 
			}
		}
		
		private object CanBradleyApcTarget(BradleyAPC bradley, BaseEntity target)
		{
			if (target is NPCPlayer)
			{
				return false;
			}
			
			var player = target as BasePlayer;
			
			if (player != null)
			{
				Vector3 mainTurretPosition = bradley.mainTurret.transform.position;
				
				if (!(player.IsVisible(mainTurretPosition, bradley.CenterPoint()) || player.IsVisible(mainTurretPosition, player.eyes.position) || player.IsVisible(mainTurretPosition, player.transform.position)))
				{
					return false;
				}
	  
				if (!configs.options.targetsNakeds)
				{
					foreach (var item in player.inventory.containerWear.itemList)
					{
						if (configs.targetable.targetable.Contains(item.info.shortname) && !configs.targetable.nonTargetable.Contains(item.info.shortname))
						{
							return true;
						}
					}
					
					if (player.inventory.containerWear.itemList.Count > configs.targetable.mostClothing)
					{
						return true;
					}
					
					foreach (var item in player.inventory.containerBelt.itemList)
					{
						if (configs.targetable.targetable.Contains(item.info.shortname) && !configs.targetable.nonTargetable.Contains(item.info.shortname))
						{
							return true;
						}
					}
					
					return false;
				}
			}

			return bradley.IsVisible(target.CenterPoint());
		}
		
		#endregion
		
		#region Chat/Console Commands

		[ChatCommand("bradley")]
		private void CommandBradley(BasePlayer player, string command, string[] args)
		{
			if (!player.IsAdmin && !(isAllowed(player, "bradleycontrol.allow")))
			{
				MessagePlayer(Lang("No Permission", player), player);
				return;
			}

            if (args.Length == 0)
            {
                MessagePlayer(Lang("BRADLEY_ABOUT", player).Replace("{Version}", Version.ToString()), player);
            }
            else
            {
                if (args[0].ToLower() == "reset")
                {
                    if (configs.options.bradleyEnabled)
                    {
                        MessagePlayer(Lang("BRADLEY_RESET", player), player);
                        ResetBradley();
                    }
                    else
                    {
                        MessagePlayer(Lang("BRADLEY_RESPAWN_FAIL", player), player);
                    }
                }
                else if (args[0].ToLower() == "unlockcrates")
                {
                    foreach (var crate in lockedCrates)
                    {
                        UnlockCrate(crate);
                    }

                 //   lockedCrates.Clear();
                    MessagePlayer(Lang("BRADLEY_UNLOCK_CRATES", player), player);
                }
                else if (args[0].ToLower() == "clearcrates")
                {
                    foreach (var crate in lockedCrates)
                    {
                        RemoveCrate(crate);
                    }

                    lockedCrates.Clear();
                    MessagePlayer(Lang("BRADLEY_REMOVE_CRATES", player), player);
                }
                else if (args[0].ToLower() == "cleargibs")
                {
                    foreach (var gib in gibs)
                    {
                        RemoveGib(gib);
                    }

                    gibs.Clear();
                    MessagePlayer(Lang("BRADLEY_REMOVE_GIBS", player), player);
                }
                else if (args[0].ToLower() == "clearfire")
                {
                    foreach (var ball in fireBalls)
                    {
                        RemoveFireBall(ball);
                    }

                    fireBalls.Clear();
                    MessagePlayer(Lang("BRADLEY_REMOVE_FIRE", player), player);
                }
                else if (args[0].ToLower() == "clearall")
                {
                    foreach (var crate in lockedCrates)
                    {
                        RemoveCrate(crate);
                    }

                    foreach (var gib in gibs)
                    {
                        RemoveGib(gib);
                    }

                    foreach (var ball in fireBalls)
                    {
                        RemoveFireBall(ball);
                    }

                    foreach (var bradley in bradleys)
                    {
                        RemoveBradley(bradley);
                    }

                    lockedCrates.Clear();
                    gibs.Clear();
                    fireBalls.Clear();
                    bradleys.Clear();
                    MessagePlayer(Lang("BRADLEY_REMOVE_ALL", player), player);
                }
            }
		}

		[ConsoleCommand("bradleycontrol.reset")]
		private void CommandBradley()
		{
			ResetBradley();
			Puts("Bradley Reset");
		}

        [ConsoleCommand("bradley.count")]
        void CommandTagsPlayers()
        {
            Puts(Lang("BRADLEY_COUNT", null).Replace("{Count}", bradleys.Count.ToString()));
        }

        #endregion

        #region BradleyControl

        private void ResetBradley()
        {
            BradleySpawner singleton = BradleySpawner.singleton;

            if (singleton == null)
            {
                Puts("No Bradley Spawner!");
            }
            else
            {
                if ((bool)singleton.spawned)
                {
                    singleton.spawned.Kill(BaseNetworkable.DestroyMode.None);
                }

                singleton.spawned = null;
                singleton.DoRespawn();
            }
        }

        private void UnlockCrate(LockedByEntCrate crate)
        {
            if (crate == null || (crate?.IsDestroyed ?? true))
            {
                return;
            }

            var lockingEnt = (crate?.lockingEnt != null) ? crate.lockingEnt.GetComponent<FireBall>() : null;

            if (lockingEnt != null && !(lockingEnt?.IsDestroyed ?? true))
            {
                lockingEnt.enableSaving = false; //again trying to fix issue with savelist
                lockingEnt.CancelInvoke(lockingEnt.Extinguish);
                lockingEnt.Invoke(lockingEnt.Extinguish, 30.0f);
            }

            crate.CancelInvoke(crate.Think);
            crate.SetLocked(false);
            crate.lockingEnt = null;
        }
        
        private void RemoveCrate(LockedByEntCrate crate)
        {
            if (crate == null || (crate?.IsDestroyed ?? true))
            {
                return;
            }

            crate.Kill();
        }

        private void RemoveGib(HelicopterDebris gib)
        {
            if (gib == null || (gib?.IsDestroyed ?? true))
            {
                return;
            }

            gib.Kill();
        }

        private void RemoveFireBall(FireBall fireBall)
        {
            if (fireBall == null || (fireBall?.IsDestroyed ?? true))
            {
                return;
            }

            fireBall.Kill();
        }

        private void RemoveBradley(BradleyAPC bradley)
        {
            if (bradley == null || (bradley?.IsDestroyed ?? true))
            {
                return;
            }

            bradley.Kill();
        }

        #endregion

        #region Helpers

        private string Lang(string key, BasePlayer player)
        {
            var userString = player == null ? "null" : player.UserIDString;
            return lang.GetMessage(key, this, userString);
        }

        private void MessagePlayer(string message, BasePlayer player)
        {
            player.ChatMessage(message);
        }

        #endregion
    }
}