using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
using UnityEngine;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
	[Info("CustomDecay", "Wizera", "1.0.2", ResourceId = 2690)]
	[Description("Custom decay for all individual entities")]

	class CustomDecay : CovalencePlugin
	{
		private bool configChanged = false;
		private int saveConfigInterval = 60;
		private ConfigData config;

		protected override void LoadConfig()
		{
			base.LoadConfig();
			config = Config.ReadObject<ConfigData>();
		}

		protected override void LoadDefaultConfig()
		{
			config = new ConfigData
			{
				DefaultMultiplier = 0.0f,

				PreventDecayWithinCupboardRange = false,

				DeveloperDebug = false,

				DecayConfig = new ConfigData.DecayConfigEntry()
				{
					buildingBlocks = new Dictionary<string, float>
					{
						{"Twigs", 1.0f },
						{"Wood", 0.0f },
						{"Stone", 0.0f },
						{"Metal", 0.0f },
						{"TopTier", 0.0f }
					},

					deployables = new Dictionary<string, float>
					{
						{ "barricade.concrete", 0.0f },
						{ "barricade.metal", 0.0f },
						{ "barricade.sandbags", 0.0f },
						{ "barricade.stone", 0.0f },
						{ "barricade.wood", 0.0f },
						{ "barricade.woodwire", 0.0f },
						{ "BBQ.Deployed", 0.0f },
						{ "beartrap", 0.0f },
						{ "box.wooden.large", 0.0f },
						{ "campfire", 0.0f },
						{ "fridge.deployed", 0.0f },
						{ "furnace", 0.0f },
						{ "furnace.large", 0.0f },
						{ "gates.external.high.wood", 0.0f },
						{ "jackolantern.angry", 0.0f },
						{ "jackolantern.happy", 0.0f },
						{ "landmine", 0.0f },
						{ "lantern.deployed", 0.0f },
						{ "locker.deployed", 0.0f },
						{ "reactivetarget_deployed", 0.0f },
						{ "refinery_small_deployed", 0.0f },
						{ "repairbench_deployed", 0.0f },
						{ "researchtable_deployed", 0.0f },
						{ "skull_fire_pit", 0.0f },
						{ "sleepingbag_leather_deployed", 0.0f },
						{ "spikes.floor", 0.0f },
						{ "survivalfishtrap.deployed", 0.0f },
						{ "tunalight.deployed", 0.0f },
						{ "wall.external.high.stone", 0.0f },
						{ "wall.external.high.wood", 0.0f },
						{ "water_catcher_large", 0.0f },
						{ "water_catcher_small", 0.0f },
						{ "WaterBarrel", 0.0f },
						{ "woodbox_deployed", 0.0f },
						{ "wall.window.glass.reinforced", 0.0f }
					}
				}
			};

			Puts("New configuration file created.");
		}

		protected override void SaveConfig() => Config.WriteObject(config);

		private void OnServerInitialized()
		{
			timer.Every(saveConfigInterval, () =>
			{
				try
				{
					if (configChanged)
					{
						configChanged = false;
						Puts("Saving decay confiuration file");
						SaveConfig();
					}
				}
				finally
				{
				}
			});
		}

		[Command("customdecay.toggledebug")]
		private void CustomDecayToggleDebug(IPlayer player, string command, string[] args)
		{
			if (!player.IsAdmin)
			{
				Puts("No permission to execute this command. You need auth level 2");
				return;
			}

			config.DeveloperDebug = !config.DeveloperDebug;
			if (config.DeveloperDebug)
				Puts("Debug switched on");
			else
				Puts("Debug switched off");
			SaveConfig();
		}

		void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
		{
			var started = DateTime.Now;

			try
			{
				if (hitInfo.damageTypes.Has(Rust.DamageType.Decay))
				{
					var block = entity as BuildingBlock;
					if (block == null)
					{
						// Process deployables
						ProcessEntity(entity, hitInfo, config.DecayConfig.deployables, entity.LookupPrefab().name);
					}
					else if (block.grade == BuildingGrade.Enum.Twigs || block.grade == BuildingGrade.Enum.Wood || block.grade == BuildingGrade.Enum.Stone || block.grade == BuildingGrade.Enum.Metal || block.grade == BuildingGrade.Enum.TopTier)
					{
						// Process Twigs + all foundation types of higher tiers
						ProcessEntity(entity, hitInfo, config.DecayConfig.buildingBlocks, block.grade.ToString());
					}
				}
			}
			finally
			{
				if (config.DeveloperDebug)
				{
					var ms = (DateTime.Now - started).TotalMilliseconds;
					if (ms > 10) Puts($"OnEntityTakeDamage took {ms} ms to execute.");
				}
			}
		}

		private bool UserHasCupboardPrivAtEntityPosition(Vector3 position, ulong entityOwnerID)
		{
			List<BaseEntity> list = new List<BaseEntity>();
			Vis.Entities<BaseEntity>(position, 1f, list);

			foreach (var ent in list)
			{
				var buildingPrivlidge = ent.GetComponentInParent<BuildingPrivlidge>();
				if (buildingPrivlidge != null)
				{
					foreach (var auth in buildingPrivlidge.authorizedPlayers.Select(x => x.userid).ToArray())
					{
						if (auth.ToString() == entityOwnerID.ToString())
						{
							return true;
						}
					}
				}
			}

			return false;
		}

		private void ProcessEntity(BaseCombatEntity entity, HitInfo hitInfo, Dictionary<string, float> dictionary, string prefabName)
		{
			float configMultiplier = 0.0f;

			if (config.PreventDecayWithinCupboardRange && prefabName != "Twigs" && UserHasCupboardPrivAtEntityPosition(entity.transform.position, entity.OwnerID != 0 ? entity.OwnerID : hitInfo.HitEntity.OwnerID))
			{
				// Entity is within users cupboard range, disable decay for entity
				configMultiplier = 0.0f;
			}
			else if (!GetMultiplierValueFromConfig(prefabName, dictionary, out configMultiplier))
			{
				AddDefaultToConfig(prefabName, dictionary);
			}

			bool skipped = true;
			float before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);
			if (!configMultiplier.Equals(1.0f))
			{
				skipped = false;
				hitInfo.damageTypes.Scale(Rust.DamageType.Decay, configMultiplier);
			}

			if (config.DeveloperDebug)
			{
				float after = hitInfo.damageTypes.Get(Rust.DamageType.Decay);
				bool more = after > before;
				bool less = after < before;
				string damage = more ? "more" : (less ? (after.Equals(0f) ? "none" : "less") : (after.Equals(before) ? "default" : "not changed"));

				Puts($"Entity: {prefabName} before={before}, after={after}, multiplier={configMultiplier}, skipped={skipped}, damage={damage}");
			}
		}

		private bool GetMultiplierValueFromConfig(string prefabName, Dictionary<string, float> dictionary, out float configMultiplier)
		{
			float localConfigMultiplier = 0.0f;
			bool exists = dictionary.TryGetValue(prefabName, out localConfigMultiplier);
			if (localConfigMultiplier < 0f || localConfigMultiplier > 10000f)
			{
				localConfigMultiplier = 0f;
			}

			configMultiplier = localConfigMultiplier;
			return exists;
		}

		private void AddDefaultToConfig(string prefabName, Dictionary<string, float> dictionary)
		{
			if (config.DeveloperDebug)
			{
				Puts($"Adding missing config item for {prefabName} set to default multiplier {config.DefaultMultiplier}");
			}

			dictionary.Add(prefabName, config.DefaultMultiplier);

			configChanged = true;
		}

		private class ConfigData
		{
			[JsonProperty(PropertyName = "Default Decay Multiplier")]
			public float DefaultMultiplier { get; set; } = 0f;

			[JsonProperty(PropertyName = "Prevent Decay Within Cupboard Range")]
			public bool PreventDecayWithinCupboardRange { get; set; } = false;

			[JsonProperty(PropertyName = "Developer Debug")]
			public bool DeveloperDebug { get; set; } = false;

			[JsonProperty(PropertyName = "Decay Config")]
			public DecayConfigEntry DecayConfig { get; set; } = new DecayConfigEntry();

			public class DecayConfigEntry
			{
				[JsonProperty(PropertyName = "Building Blocks")]
				public Dictionary<string, float> buildingBlocks;

				[JsonProperty(PropertyName = "Deployables")]
				public Dictionary<string, float> deployables;
			}
		}
	}
}