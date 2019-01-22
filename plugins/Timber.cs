using System;
using System.Collections.Generic;

using UnityEngine;

using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;

// TODO: Fully implement hits options.
namespace Oxide.Plugins
{
	[Info("Timber", "Mattparks", "0.1.11", ResourceId = 2565)]
	[Description("Makes trees and cacti fall before being destroyed.")]
	class Timber : RustPlugin 
	{
		#region Fields
	   
		private readonly static float maxFirstDistance = 18.0f;

		private readonly static string soundWoundedPrefab = "assets/bundled/prefabs/fx/player/beartrap_scream.prefab";
		private readonly static string soundFallNormalPrefab = "assets/bundled/prefabs/fx/player/groundfall.prefab";
		private readonly static string soundFallLargePrefab = "assets/prefabs/building/door.hinged/effects/gate-external-wood-close-end.prefab";
		private readonly static string soundGroundPrefab = "assets/bundled/prefabs/fx/player/groundfall.prefab";
		private readonly static string soundDespawnPrefab = "assets/bundled/prefabs/fx/player/groundfall.prefab";
		private readonly static string despawnPrefab = "assets/prefabs/misc/junkpile/effects/despawn.prefab";
		private readonly static string stumpPrefab = "assets/bundled/prefabs/autospawn/collectable/stone/wood-collectable.prefab";

		#endregion

        #region Configuration

		public class Options
		{	 
			public float harvestStanding = 0.5f; // The rate of gather in fallen trees.
			public float harvestFallen = 2.805f; // The rate of gather in standing trees.
			public int hitsStanding = 10; // The amount of hits from a hatchet to take a standing tree down.
			public int hitsFallen = 10; // The amount of hits from a hatchet to finish a fallen tree.
			public float despawnLength = 30.0f; // How long the fallen tree will sit on the ground before despawning.
			public float screamPercent = 0.03f; // The percent of trees that will scream when chopped down.
			public bool includeCacti = true; // If cacti will be included in the timber plugin.
			public bool logToPlayer = true; // If enabled a message will be displayed to a player after they chop down there first tree.
		}
		
		public class ConfigData
		{
			public Options options = new Options();
		}

		public class StoredData
		{
			public List<string> loggedTo = new List<string>();
		}
		
		private ConfigData configs;
		private StoredData storedData;
		
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
			
			storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("Timber");
			
			if (storedData == null)
			{
				storedData = new StoredData();
			}
			
			SaveConfig();
			SaveStored();
		}

		private void SaveConfig()
		{
			Config.WriteObject(configs, true);
		}

		private void SaveStored()
		{
			Interface.Oxide.DataFileSystem.WriteObject("Timber", storedData);
		}

        #endregion

        #region Messages/Localization

		private void LoadMessages() 
		{
			// English messages.
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["TIMBER_ABOUT"] = "<color=#ff3b3b>Timber {Version}</color>: by <color=green>mattparks</color>. Timber is a plugin that animates the destruction of trees and cacti.",
				["TIMBER_FIRST"] = "<color=#ff3b3b>Timber!</color> Tree falling is not in vanilla Rust, hit the tree on the ground for more wood, read more from the command <color=green>/timber</color>",
			}, this, "en");
		}
		
        #endregion

        #region Hooks

		private void Init()
		{
			LoadVariables();
			LoadMessages();
			SaveStored();
		}

		private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
		{
			var gatherType = dispenser.gatherType.ToString("G");
			var fallDefined = dispenser.GetComponent<FallControl>() != null;
			var despawnDefined = dispenser.GetComponent<DespawnControl>() != null;

			// Does not change cacti when disabled.
			if (dispenser.containedItems.Count == 0)
			{
				return;
			}

			// Changes the harvest amount in objects that are falling or despawning.
			if (fallDefined || despawnDefined)
			{
				item.amount = (int) (item.amount * configs.options.harvestFallen);
			}
			else if (dispenser.gatherType == ResourceDispenser.GatherType.Tree)
			{
				item.amount = (int) (item.amount * configs.options.harvestStanding);
			}
		}
		
		private void OnEntityKill(BaseNetworkable entity)
		{
			bool fallDefined = entity.GetComponent<FallControl>() != null;
			bool despawnDefined = entity.GetComponent<DespawnControl>() != null;

			var entityPosition = entity.transform.position;
			var entityRotation = entity.transform.rotation;

			// Creates the fall behaviour if none is defined.
			if (!fallDefined && !despawnDefined)
			{
				if (entity is TreeEntity || (configs.options.includeCacti && StringPool.Get(entity.prefabID).Contains("cactus")))
				{
					Effect.server.Run(despawnPrefab, entityPosition);

					if (configs.options.logToPlayer)
					{
						foreach (var player in BasePlayer.activePlayerList)
						{
							float distance = Vector3.Distance(player.transform.position, entityPosition);
							
							if (distance < maxFirstDistance)
							{
								if (!storedData.loggedTo.Contains(player.UserIDString))
								{
									MessagePlayer(Lang("TIMBER_FIRST", player), player);
									storedData.loggedTo.Add(player.UserIDString);
								}
							}
						}
					}

					var newFalling = GameManager.server.CreateEntity(StringPool.Get(entity.prefabID), entityPosition, entityRotation, true);

					var controlFall = newFalling.gameObject.AddComponent<FallControl>();
					controlFall.Load(newFalling, configs.options.despawnLength, configs.options.screamPercent);

					newFalling.Spawn();
				}
			}
			// Creates the despawn behaviour if fall is defined.
			else if (fallDefined && !despawnDefined)
			{
				Effect.server.Run(soundDespawnPrefab, entityPosition);

				// TODO: Effects down length of fallen tree.
				Effect.server.Run(despawnPrefab, entityPosition);

				var newFalling = GameManager.server.CreateEntity(StringPool.Get(entity.prefabID), entityPosition, entityRotation, true);

				var controlDespawn = newFalling.gameObject.AddComponent<DespawnControl>();
				controlDespawn.Load(newFalling);

				newFalling.Spawn();
			}
		}

		private void Unload()
		{
			SaveStored();
		}
		
        #endregion
		
        #region Chat/Console Commands

		[ChatCommand("timber")]
		private void CommandTimber(BasePlayer player, string command, string[] args)
		{
			MessagePlayer(Lang("TIMBER_ABOUT", player).Replace("{Version}", Version.ToString()), player); 
		}
		
        #endregion

		#region Behaviours

		public class FallControl : MonoBehaviour
		{
			private readonly static float ACCELERATION_Y = 0.07f;
			private readonly static float RADIUS_OFFSET_SPEED = 0.02f;
			private readonly static float MIN_STUMP_RADIUS = 0.3f;
			private readonly static float LARGE_SOUND_HEIGHT = 10.0f;

			private BaseEntity parentEntity;
			private float entityHeight;
			private float despawnLength;

			private float colliderHeight;
			private float colliderRadius;
			private float targetAngle;

			private float currentSpeed;
			private Vector3 currentAngle;
			private float currentOffsetY;
			private float timeDespawn;

			private int lastFrame = Time.frameCount;
			
			public FallControl()
			{
				this.parentEntity = null;
				this.despawnLength = 30.0f;

				this.colliderHeight = 0.0f;
				this.colliderRadius = 0.0f;
				this.targetAngle = 82.0f;

				this.currentSpeed = 0.0f;
				this.currentAngle = new Vector3();
				this.currentOffsetY = 0.0f;
				this.timeDespawn = 0.0f;
			}

			public void Load(BaseEntity parentEntity, float despawnLength, float screamPercent)
			{
				this.parentEntity = parentEntity;
				this.despawnLength = despawnLength;

				var capsuleCollider = parentEntity.GetComponent<CapsuleCollider>();

				if (capsuleCollider != null)
				{
					this.colliderHeight = capsuleCollider.height;
					this.colliderRadius = capsuleCollider.radius;
					// TODO: Calculate target angle from terrain.
				}

				if (colliderRadius >= MIN_STUMP_RADIUS)
				{
					var stumpPosition = gameObject.transform.position;
					var stumpHeight = TerrainMeta.HeightMap.GetHeight(stumpPosition);
					var stumpEntity = GameManager.server.CreateEntity(stumpPrefab, new Vector3(stumpPosition.x, stumpHeight, stumpPosition.z));
					stumpEntity.Spawn();
				}
				
				if (screamPercent > 0 && new System.Random().NextDouble() <= screamPercent)
				{
					Effect.server.Run(soundWoundedPrefab, gameObject.transform.position);
				}
				else 
				{
					if (colliderHeight >= LARGE_SOUND_HEIGHT)
					{
						Effect.server.Run(soundFallLargePrefab, gameObject.transform.position);
					}
					else
					{
						Effect.server.Run(soundFallNormalPrefab, gameObject.transform.position);
					}
				}
			}

			private void Update()
			{
				if (Time.frameCount - lastFrame > 1)
				{
					// Falls until the target angle has been reached.
					if (Math.Abs(currentAngle.x) <= targetAngle) 
					{
						currentSpeed += ACCELERATION_Y * Time.deltaTime;
						Vector3 deltaAngle = Vector3.left * currentSpeed;
						currentAngle += deltaAngle;
						gameObject.transform.rotation *= Quaternion.Euler(deltaAngle.x, deltaAngle.y, deltaAngle.z);
						gameObject.transform.hasChanged = true;

						if (currentOffsetY < colliderRadius)
						{
							currentOffsetY += RADIUS_OFFSET_SPEED * currentSpeed;
							parentEntity.transform.position += new Vector3(0.0f, RADIUS_OFFSET_SPEED * currentSpeed, 0.0f);
						}

						// TODO: Fix rendering rotation from far distance.
						parentEntity.SendNetworkUpdateImmediate();
					}
					// This is when the tree has hit the ground.
					else if (currentSpeed != 0.0f)
					{
						Effect.server.Run(soundGroundPrefab, gameObject.transform.position);
						currentSpeed = 0.0f;
					}
					else
					{
						timeDespawn += Time.deltaTime;
					}

					if (timeDespawn > despawnLength)
					{
						parentEntity.Kill();
					}
				
					lastFrame = Time.frameCount;
				}
			}
		}

		public class DespawnControl : MonoBehaviour
		{
			private readonly static float ACCELERATION_Y = 0.001f;
			private readonly static float DESPAWN_HEIGHT = -15.0f;

			private BaseEntity parentEntity;
			private float currentSpeed;
			
			private int lastFrame = Time.frameCount;

			public DespawnControl()
			{
				this.parentEntity = null;
				this.currentSpeed = 0.0f;
			}

			public void Load(BaseEntity parentEntity)
			{
				this.parentEntity = parentEntity;
			}

			private void Update()
			{
				if (Time.frameCount - lastFrame > 1)
				{
					currentSpeed += ACCELERATION_Y * Time.deltaTime;
					gameObject.transform.position += new Vector3(0.0f, -currentSpeed, 0.0f);
					gameObject.transform.hasChanged = true;

					parentEntity.SendNetworkUpdateImmediate();

					if (gameObject.transform.position.y < DESPAWN_HEIGHT)
					{
						parentEntity.Kill();
					}
					
					lastFrame = Time.frameCount;
				}
			}
		}
		
        #endregion

        #region Helpers

		private string Lang(string key, BasePlayer player)
		{
			return lang.GetMessage(key, this, player.UserIDString);
		}

		private void MessagePlayer(string message, BasePlayer player)
		{
			player.ChatMessage(message);
		}

        #endregion
	}
}
