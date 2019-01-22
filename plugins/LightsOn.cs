using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Lights On", "mspeedie", "1.4.1")]
    [Description("Toggle lights on/off either as configured or by name.")]
    public class LightsOn : CovalencePlugin
	//RustPlugin
    {

        const string perm_lightson     = "lightson.allowed";
        const string perm_freelights   = "lightson.freelights";
		private bool InitialPassNight  = true;
		private bool NightToggleactive = false;
		private bool nightcross24      = false;
		private bool config_changed    = false;
        private Timer Nighttimer;
        private Timer Alwaystimer;
        private Timer Devicetimer;
        private Configuration config;

		// strings to compare to check object names
		const string bbq_name               = "bbq";
		const string campfire_name          = "campfire";
		const string ceilinglight_name      = "ceilinglight";
		const string cursedcauldron_name    = "cursedcauldron";
		const string fireplace_name         = "fireplace";
		const string fogmachine_name        = "fogmachine";
		const string furnace_name           = "furnace";
		const string furnace_large_name     = "furnace.large";
		const string hobobarrel_name        = "hobobarrel";
		const string jackolanternangry_name = "jackolantern.angry";
		const string jackolanternhappy_name = "jackolantern.happy";
		const string lantern_name           = "lantern";
		const string largecandleset_name    = "largecandleset";
		const string refinerysmall_name     = "small_refinery";
		const string searchlight_name       = "searchlight";
		const string simplelight_name       = "simplelight";
		const string skullfirepit_name      = "skull_fire_pit";
		const string smallrefinery_name     = "small.oil.refinery";
		const string smallcandleset_name    = "smallcandleset";
		const string snowmachine_name       = "snowmachine";
		const string spookyspeaker_name     = "spookyspeaker";
		const string strobelight_name       = "strobelight";
		const string tunalight_name         = "tunalight";
		const string hatminer_name          = "hat.miner";
		const string hatcandle_name         = "hat.candle";

        public class Configuration
        {
			// True means turn them on
			[JsonProperty(PropertyName = "Hats do not use fuel (true/false)")]
			public bool Hats { get; set; } = true;

			[JsonProperty(PropertyName = "BBQs (true/false)")]
			public bool BBQs { get; set; } = false;

			[JsonProperty(PropertyName = "Campfires (true/false)")]
			public bool Campfires { get; set; } = false;

			[JsonProperty(PropertyName = "Candles (true/false)")]
			public bool Candles { get; set; } = true;

			[JsonProperty(PropertyName = "Cauldrons (true/false)")]
			public bool Cauldrons { get; set; } = false;

			[JsonProperty(PropertyName = "Ceiling Lights (true/false)")]
			public bool CeilingLights { get; set; } = true;

			[JsonProperty(PropertyName = "Fire Pits (true/false)")]
			public bool FirePits { get; set; } = false;

			[JsonProperty(PropertyName = "Fireplaces (true/false)")]
			public bool Fireplaces { get; set; } = true;

			[JsonProperty(PropertyName = "Fog Machines (true/false)")]
			public bool Fog_Machines { get; set; } = true;

			[JsonProperty(PropertyName = "Furnaces (true/false)")]
			public bool Furnaces { get; set; } = false;

			[JsonProperty(PropertyName = "Hobo Barrels (true/false)")]
			public bool HoboBarrels { get; set; } = true;

			[JsonProperty(PropertyName = "Lanterns (true/false)")]
			public bool Lanterns { get; set; } = true;

			[JsonProperty(PropertyName = "Refineries (true/false)")]
			public bool Refineries { get; set; } = false;

			[JsonProperty(PropertyName = "Search Lights (true/false)")]
			public bool SearchLights { get; set; } = true;

			[JsonProperty(PropertyName = "Simple Lights (true/false)")]
			public bool SimpleLights { get; set; } = false;

			[JsonProperty(PropertyName = "SpookySpeakers (true/false)")]
			public bool Speakers { get; set; } = false;

			[JsonProperty(PropertyName = "Strobe Lights (true/false)")]
			public bool StrobeLights { get; set; } = false;

			[JsonProperty(PropertyName = "SnowMachines (true/false)")]
			public bool Snow_Machines { get; set; } = true;

			[JsonProperty(PropertyName = "Protect BBQs (true/false)")]
			public bool ProtectBBQs { get; set; } = true;

			[JsonProperty(PropertyName = "Protect Campfires (true/false)")]
			public bool ProtectCampfires { get; set; } = true;

			[JsonProperty(PropertyName = "Protect Cauldron (true/false)")]
			public bool ProtectCauldrons { get; set; } = true;

			[JsonProperty(PropertyName = "Protect Fire Pits (true/false)")]
			public bool ProtectFirePits { get; set; } = true;

			[JsonProperty(PropertyName = "Protect Fireplaces (true/false)")]
			public bool ProtectFireplaces { get; set; } = true;

			[JsonProperty(PropertyName = "Protect Furnaces (true/false)")]
			public bool ProtectFurnaces { get; set; } = true;

			[JsonProperty(PropertyName = "Protect Hobo Barrels (true/false)")]
			public bool ProtectHoboBarrels { get; set; } = false;

			[JsonProperty(PropertyName = "Protect Refineries (true/false)")]
			public bool ProtectRefineries { get; set; } = true;

			[JsonProperty(PropertyName = "Devices Always On (true/false)")]
			public bool DevicesAlwaysOn { get; set; } = true;

			[JsonProperty(PropertyName = "Always On (true/false)")]
			public bool AlwaysOn { get; set; } = false;

			[JsonProperty(PropertyName = "Night Toggle (true/false)")]
			public bool NightToggle { get; set; } = true;

			[JsonProperty(PropertyName = "Console Output (true/false)")]
			public bool ConsoleMsg { get; set; } = true;

			// this is checked more frequently to get the lights on/off closer to the time the operator sets
			[JsonProperty(PropertyName = "Night Toggle Check Frequency (in seconds)")]
			public int NightCheckFrequency { get; set; } = 30;

			// these less frequent checks as most devices will be on when placed
			[JsonProperty(PropertyName = "Always On Check Frequency (in seconds)")]
			public int AlwaysCheckFrequency { get; set; } = 300;

			// these less frequent checks as most devices will be on when placed
			[JsonProperty(PropertyName = "Device Check Frequency (in seconds)")]
			public int DeviceCheckFrequency { get; set; } = 300;


			[JsonProperty(PropertyName = "Dusk Time (HH in a 24 hour clock)")]
			public float DuskTime { get; set; } = 17.5f;

			[JsonProperty(PropertyName = "Dawn Time (HH in a 24 hour clock)")]
			public float DawnTime { get; set; } = 09.0f;

        }
        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["bad check frequency"] = "Check frequency must be between 10 and 600",
                ["bad check frequency2"] = "Check frequency must be between 60 and 6000",
                ["bad prefab"] = "Bad Prefab Name, not found in devices or lights: ",
                ["bad dusk time"] = "Dusk time must be between 0 and 24",
                ["bad dawn time"] = "Dawn time must be between 0 and 24",
				["dawn=dusk"] = "Dawn can't be the same value as dusk",
				["dawn"] = "Lights going off.  Next lights on at ",
				["default"] = "Loading default config for LightsOn",
                ["dusk"] = "Lights coming on.  Ending at ",
                ["lights off"] = "Lights Off",
                ["lights on"] = "Lights On",
                ["nopermission"] = "You do not have permission to use that command.",
				["one or the other"] = "Please select one (and only one) of Always On or Night Toggle",
                ["prefix"] = "LightsOn: ",
                ["state"] = "unknown state: please use on or off",
                ["syntax"] = "syntax: Lights State (on/off) Optional: prefabshortname (part of the prefab name) to change their state, use all to force all lights' state",
            }, this);
        }

        protected override void LoadConfig()
        {
			config_changed = false;
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
				{
					if (config.ConsoleMsg)
						Puts(Lang("default"));
                    LoadDefaultConfig();
					config_changed = true;
				}
            }
            catch
            {
				if (config.ConsoleMsg)
					Puts(Lang("default"));
				LoadDefaultConfig();
				config_changed = true;
            }

			// check data is ok because people can make mistakes
			if (config.AlwaysOn && config.NightToggle)
			{
				Puts(Lang("one or the other"));
				config.NightToggle = false;
				config_changed = true;
			}
			if (config.DuskTime < 0f || config.DuskTime > 24f)
			{
				Puts(Lang("bad dusk time"));
				config.DuskTime = 17f;
				config_changed = true;
			}
			if (config.DawnTime < 0f || config.DawnTime > 24f)
			{
				Puts(Lang("bad dawn time"));
				config.DawnTime = 9f;
				config_changed = true;
			}
			if (config.DawnTime == config.DuskTime)
			{
				Puts(Lang("dawn=dusk"));
				config.DawnTime = 9f;
				config.DuskTime = 17f;
				config_changed = true;
			}
			if (config.NightCheckFrequency < 10 || config.NightCheckFrequency > 600)
			{
				Puts(Lang("bad check frequency"));
				config.NightCheckFrequency = 30;
				config_changed = true;
			}

			if (config.AlwaysCheckFrequency < 60 || config.AlwaysCheckFrequency > 6000)
			{
				Puts(Lang("bad check frequency2"));
				config.AlwaysCheckFrequency = 300;
				config_changed = true;
			}

			if (config.DeviceCheckFrequency < 60 || config.DeviceCheckFrequency > 6000)
			{
				Puts(Lang("bad check frequency2"));
				config.DeviceCheckFrequency = 300;
				config_changed = true;
			}

			// determine correct light timing logic
			if  (config.DuskTime > config.DawnTime)
				nightcross24 = true;
			else
				nightcross24 = false;

			if (config.AlwaysOn)
			{
				// start timer to lights always on
				Alwaystimer = timer.Once(config.AlwaysCheckFrequency, AlwaysTimerProcess);
			}
			else if (config.NightToggle)
			{
				// start timer to toggle lights based on time
				InitialPassNight = true;
				Nighttimer = timer.Once(config.NightCheckFrequency, NightTimerProcess);
			}

			if (config.DevicesAlwaysOn)
			{
				// start timer to toggle devices
				Devicetimer = timer.Once(config.DeviceCheckFrequency, DeviceTimerProcess);
			}

			if (config_changed)
				SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        private void OnServerInitialized()
        {
			permission.RegisterPermission(perm_freelights,this);
		}

		string CleanedName(string prefabName)
		{
			if (string.IsNullOrEmpty(prefabName))
				return prefabName;

			string CleanedString = prefabName;
			int clean_loc = CleanedString.IndexOf("deployed");
			if (clean_loc > 1)
				CleanedString = CleanedString.Remove(clean_loc-1);
			clean_loc = CleanedString.IndexOf("static");
			if (clean_loc > 1)
				CleanedString = CleanedString.Remove(clean_loc-1);

			return CleanedString;
		}

        bool IsOvenPrefabName(string prefabName)
        {
            if (furnace_name.Contains(prefabName))
				return true;
			else if (furnace_large_name.Contains(prefabName))
				return true;
			else if (bbq_name.Contains(prefabName))
				return true;
			else if (campfire_name.Contains(prefabName))
				return true;
			else if (fireplace_name.Contains(prefabName))
				return true;
			else if (hobobarrel_name.Contains(prefabName))
				return true;
			else if (lantern_name.Contains(prefabName))
				return true;
			else if (tunalight_name.Contains(prefabName))
				return true;
			else if (cursedcauldron_name.Contains(prefabName))
				return true;
			else if (skullfirepit_name.Contains(prefabName))
				return true;
			else if (refinerysmall_name.Contains(prefabName))
				return true;
			else if (smallrefinery_name.Contains(prefabName))
				return true;
			else if (jackolanternangry_name.Contains(prefabName))
				return true;
			else if (jackolanternhappy_name.Contains(prefabName))
				return true;
			else
				return false;
        }

        bool IsLightPrefabName(string prefabName)
        {
            if (ceilinglight_name.Contains(prefabName))
				return true;
			else if (lantern_name.Contains(prefabName))
				return true;
			else if (tunalight_name.Contains(prefabName))
				return true;
			else if (jackolanternangry_name.Contains(prefabName))
				return true;
			else if (jackolanternhappy_name.Contains(prefabName))
				return true;
			else if (largecandleset_name.Contains(prefabName))
				return true;
			else if (smallcandleset_name.Contains(prefabName))
				return true;
			else if (searchlight_name.Contains(prefabName))
				return true;
			else if (simplelight_name.Contains(prefabName))
				return true;
			else
				return false;
        }

        bool IsHatPrefabName(string prefabName)
        {
			// this uses only internal names so do not need the Contains logic
            switch (prefabName)
            {
				case hatminer_name: 	return true;
				case hatcandle_name:	return true;
				default:				return false;
            }
        }

        bool IsDevicePrefabName(string prefabName)
        {
            if (fogmachine_name.Contains(prefabName))
				return true;
			else if (snowmachine_name.Contains(prefabName))
				return true;
			else if (strobelight_name.Contains(prefabName))
				return true;
			else if (spookyspeaker_name.Contains(prefabName))
				return true;
			else
                return false;
        }

        bool CanCookShortPrefabName(string prefabName)
        {
            if (furnace_name.Contains(prefabName))
				return true;
            else if (furnace_large_name.Contains(prefabName))
				return true;
            else if (campfire_name.Contains(prefabName))
				return true;
            else if (bbq_name.Contains(prefabName))
				return true;
            else if (fireplace_name.Contains(prefabName))
				return true;
            else if (refinerysmall_name.Contains(prefabName))
				return true;
            else if (smallrefinery_name.Contains(prefabName))
				return true;
            else if (skullfirepit_name.Contains(prefabName))
				return true;
            else if (hobobarrel_name.Contains(prefabName))
				return true;
            else if (cursedcauldron_name.Contains(prefabName))
				return true;
			else
				return false;
        }

        bool ProtectShortPrefabName(string prefabName)
        {
            switch (CleanedName(prefabName))
            {
				case "bbq":					return config.ProtectBBQs;
				case "campfire":			return config.ProtectCampfires;
				case "cursedcauldron":		return config.ProtectCauldrons;
				case "fireplace":			return config.ProtectFireplaces;
				case "furnace":				return config.ProtectFurnaces;
				case "furnace.large":		return config.ProtectFurnaces;
				case "hobobarrel":			return config.ProtectHoboBarrels;
				case "refinery_small":		return config.ProtectRefineries;
				case "small.oil.refinery":	return config.ProtectRefineries;
				case "skull_fire_pit":		return config.ProtectFirePits;
				default:
				{
                    return false;
				}
            }
        }

        bool ProcessShortPrefabName(string prefabName)
        {
            switch (CleanedName(prefabName))
            {
				case "bbq":					return config.BBQs;
				case "campfire":			return config.Campfires;
				case "ceilinglight":		return config.CeilingLights;
				case "cursedcauldron":		return config.Cauldrons;
				case "fireplace":			return config.Fireplaces;
				case "fogmachine":			return config.Fog_Machines;
				case "furnace":				return config.Furnaces;
				case "furnace.large":		return config.Furnaces;
				case "hobobarrel":			return config.HoboBarrels;
				case "jackolantern.angry":	return config.Lanterns;
				case "jackolantern.happy":	return config.Lanterns;
				case "lantern":				return config.Lanterns;
				case "largecandleset":		return config.Candles;
				case "refinery_small":		return config.Refineries;
				case "searchlight":			return config.SearchLights;
				case "simplelight":			return config.SimpleLights;
				case "skull_fire_pit":		return config.FirePits;
				case "small.oil.refinery":	return config.Refineries;
				case "smallcandleset":		return config.Candles;
				case "snowmachine":			return config.Snow_Machines;
				case "spookyspeaker":		return config.Speakers;
				case "strobelight":			return config.StrobeLights;
				case "tunalight":			return config.Lanterns;
				case "hat.miner": 			return config.Hats;
				case "hat.candle": 			return config.Hats;
				default:
				{
                    return false;
				}
            }
        }


		private void AlwaysTimerProcess()
		{
			if (config.AlwaysOn)
			{
				ProcessLights(true, "all");
				// submit for the next pass
				Alwaystimer = timer.Once(config.AlwaysCheckFrequency, AlwaysTimerProcess);
			}
		}

		private void DeviceTimerProcess()
		{
			if (config.DevicesAlwaysOn)
			{
				ProcessDevices(true, "all");
				// submit for the next pass
				Devicetimer = timer.Once(config.DeviceCheckFrequency, DeviceTimerProcess);
			}
		}

		private void NightTimerProcess()
		{
			if (config.NightToggle)
			{
				ProcessNight();
				// clear the Inital flag as we now accurately know the state
				InitialPassNight = false;
				// submit for the next pass
				Nighttimer = timer.Once(config.NightCheckFrequency, NightTimerProcess);
			}
		}

		private void ProcessNight()
		{
			var gtime = TOD_Sky.Instance.Cycle.Hour;
			if ((nightcross24 == false && gtime >= config.DuskTime && gtime < config.DawnTime) ||
				(nightcross24 && ((gtime >= config.DuskTime && gtime < 24) || gtime < config.DawnTime))
				&& (!NightToggleactive || InitialPassNight))
			{
				NightToggleactive = true;
				ProcessLights(true,"all");
				if (!config.DevicesAlwaysOn)
					ProcessDevices(true,"all");
				if (config.ConsoleMsg)
					Puts(Lang("dusk") + config.DawnTime);
			}
			else if ((nightcross24 == false &&  gtime >= config.DawnTime) ||
					(nightcross24 && (gtime <  config.DuskTime && gtime >= config.DawnTime))
					&& (NightToggleactive || InitialPassNight))
			{
				NightToggleactive = false;
				ProcessLights(false,"all");
				if (!config.DevicesAlwaysOn)
					ProcessDevices(false,"all");
				if (config.ConsoleMsg)
					Puts(Lang("dawn") + config.DuskTime);
			}
		}

        private void ProcessLights(bool state, string prefabName)
        {
			if (prefabName == "all" || IsOvenPrefabName(prefabName))
			{
				//if (string.IsNullOrEmpty(prefabName) || prefabName == "all")
				//	Puts("all lights");
				//else
				//	Puts("turing on: " + prefabName);

				BaseOven[] ovens = UnityEngine.Object.FindObjectsOfType<BaseOven>() as BaseOven[];

				foreach (BaseOven oven in ovens)
				{
					if (oven == null || oven.IsDestroyed || oven.IsOn() == state)
                        continue;
					else if (state == false && ProtectShortPrefabName(prefabName))
						continue;
					
					// not super efficient find a better way
					if (prefabName != "all" &&
					   (furnace_name.Contains(prefabName) ||
					    furnace_large_name.Contains(prefabName) ||
					    lantern_name.Contains(prefabName) ||
					    tunalight_name.Contains(prefabName) ||
					    jackolanternangry_name.Contains(prefabName) ||
					    jackolanternhappy_name.Contains(prefabName) ||
					    campfire_name.Contains(prefabName) ||
					    fireplace_name.Contains(prefabName) ||
					    bbq_name.Contains(prefabName) ||
					    cursedcauldron_name.Contains(prefabName) ||
					    skullfirepit_name.Contains(prefabName) ||
					    hobobarrel_name.Contains(prefabName) ||
					    smallrefinery_name.Contains(prefabName) ||
					    refinerysmall_name.Contains(prefabName)
						))
 					{
						oven.SetFlag(BaseEntity.Flags.On, state);
					}
					// not super efficient find a better way
					else
					{
						string oven_name = CleanedName(oven.ShortPrefabName);

						if ((config.Furnaces    && (furnace_name.Contains(oven_name) ||
													 furnace_large_name.Contains(oven_name))) ||
							 (config.Lanterns    && (lantern_name.Contains(oven_name) ||
													 tunalight_name.Contains(oven_name) ||
													 jackolanternangry_name.Contains(oven_name) ||
													 jackolanternhappy_name.Contains(oven_name))) ||
							 (config.Campfires   && campfire_name.Contains(oven_name)) ||
							 (config.Fireplaces  && fireplace_name.Contains(oven_name)) ||
							 (config.BBQs        && bbq_name.Contains(oven_name)) ||
							 (config.Cauldrons   && cursedcauldron_name.Contains(oven_name)) ||
							 (config.FirePits    && skullfirepit_name.Contains(oven_name)) ||
							 (config.HoboBarrels && hobobarrel_name.Contains(oven_name)) ||
							 (config.Refineries  && (smallrefinery_name.Contains(oven_name) || refinerysmall_name.Contains(oven_name)))
							)
						{
							oven.SetFlag(BaseEntity.Flags.On, state);
						}
					}
				}
			}

			if ((prefabName == "all" && config.SearchLights) || searchlight_name.Contains(prefabName))
			{
				SearchLight[] searchlights = UnityEngine.Object.FindObjectsOfType<SearchLight>() as SearchLight[];

				foreach (SearchLight search_light in searchlights)
				{
					if (!(search_light == null || search_light.IsDestroyed || search_light.IsOn() == state))
 					{
						search_light.SetFlag(BaseEntity.Flags.On, state);
						search_light.secondsRemaining = 99999999;
					}
				}
			}

			if ((prefabName == "all" && config.Candles) || (largecandleset_name.Contains(prefabName) || smallcandleset_name.Contains(prefabName)))
			{
				Candle[] candles = UnityEngine.Object.FindObjectsOfType<Candle>() as Candle[];
				foreach (Candle candle in candles)
				{
					if (!candle == null || candle.IsDestroyed || candle.IsOn() == state)
 					{
						candle.SetFlag(BaseEntity.Flags.On, state);
					}
				}
			}

			if ((prefabName == "all" && config.CeilingLights) || ceilinglight_name.Contains(prefabName))
			{
				CeilingLight[] ceilinglights = UnityEngine.Object.FindObjectsOfType<CeilingLight>() as CeilingLight[];

				foreach (CeilingLight ceiling_light in ceilinglights)
				{
					if (!(ceiling_light == null || ceiling_light.IsDestroyed || ceiling_light.IsOn() == state))
 					{
						ceiling_light.SetFlag(BaseEntity.Flags.On, state);
					}
				}
			}

			if ((prefabName == "all" && config.SimpleLights) || simplelight_name.Contains(prefabName))
			{
				SimpleLight[] simplelights = UnityEngine.Object.FindObjectsOfType<SimpleLight>() as SimpleLight[];

				foreach (SimpleLight simple_light in simplelights)
				{
					if (!(simple_light == null || simple_light.IsDestroyed || simple_light.IsOn() == state))
 					{
						simple_light.SetFlag(BaseEntity.Flags.On, state);
					}
				}
			}
        }

        private void ProcessDevices(bool state, string prefabName)
        {
			//Puts("In ProcessDevices ");

			if ((prefabName == "all" && config.Fog_Machines) || fogmachine_name.Contains(prefabName))
			{
				FogMachine[] fogmachines = UnityEngine.Object.FindObjectsOfType<FogMachine>() as FogMachine[];
				foreach (FogMachine fog_machine in fogmachines)
				{
					if (!(fog_machine == null || fog_machine.IsDestroyed))
 					{
						// there is bug with IsOn so force state
						if (state) // if (fogmachine.IsOn() != state)
						{
							fog_machine.SetFlag(BaseEntity.Flags.On, state);
							fog_machine.EnableFogField();
							fog_machine.StartFogging();
							fog_machine.SetFlag(BaseEntity.Flags.On, state);
						}
						else
						{
							fog_machine.SetFlag(BaseEntity.Flags.On, state);
							fog_machine.FinishFogging();
							fog_machine.DisableNozzle();
							fog_machine.SetFlag(BaseEntity.Flags.On, state);
						}
					}
				}
			}

			//if (!string.IsNullOrEmpty(prefabName) && snowmachine_name.Contains(prefabName)) Puts ("Snow machine"); else Puts("Not snow: " + prefabName);
			//if (config.Snow_Machines) Puts("Snow is configure"); else Puts("Snow is not active");
			if ((prefabName == "all" && config.Snow_Machines) || snowmachine_name.Contains(prefabName))
			{
				//if (state) Puts("Snow On"); else Puts("Snow Off");
				SnowMachine[] snowmachines = UnityEngine.Object.FindObjectsOfType<SnowMachine>() as SnowMachine[];
				foreach (SnowMachine snow_machine in snowmachines)
				{
					if (!(snow_machine == null || snow_machine.IsDestroyed))
					{
						// there is bug with IsOn so force state
						if (state) // if (fogmachine.IsOn() != state)
						{
							snow_machine.SetFlag(BaseEntity.Flags.On, state);
							snow_machine.EnableFogField();
							snow_machine.StartFogging();
							snow_machine.SetFlag(BaseEntity.Flags.On, state);
						}
						else
						{
							snow_machine.SetFlag(BaseEntity.Flags.On, state);
							snow_machine.FinishFogging();
							snow_machine.DisableNozzle();
							snow_machine.SetFlag(BaseEntity.Flags.On, state);
						}
					}
				}
			}

			if ((prefabName == "all" && config.StrobeLights) || strobelight_name.Contains(prefabName))
			{
				StrobeLight[] strobelights = UnityEngine.Object.FindObjectsOfType<StrobeLight>() as StrobeLight[];
				foreach (StrobeLight strobelight in strobelights)
				{
					if (!(strobelight == null || strobelight.IsDestroyed) && strobelight.IsOn() != state)
						strobelight.SetFlag(BaseEntity.Flags.On, state);
				}
			}

			if ((prefabName == "all" && config.Speakers) || spookyspeaker_name.Contains(prefabName))
			{
				SpookySpeaker[] spookyspeakers = UnityEngine.Object.FindObjectsOfType<SpookySpeaker>() as SpookySpeaker[];
				foreach (SpookySpeaker spookyspeaker in spookyspeakers)
				{
					if (!(spookyspeaker == null || spookyspeaker.IsDestroyed) && spookyspeaker.IsOn() != state)
 					{
						spookyspeaker.SetFlag(BaseEntity.Flags.On, state);
						spookyspeaker.SendPlaySound();
					}
				}
			}
		}

        private object OnFindBurnable(BaseOven oven)
        {
			bool hasperm = false;

			if (oven == null || string.IsNullOrEmpty(oven.ShortPrefabName) ||
				oven.OwnerID == null || oven.OwnerID == 0U || oven.OwnerID.ToString() == null)
				return null;
			else
				hasperm = permission.UserHasPermission(oven.OwnerID.ToString(), perm_freelights);

			if (hasperm != true ||
				!ProcessShortPrefabName(oven.ShortPrefabName) ||
				!IsLightPrefabName(oven.ShortPrefabName))
				return null;
			else
			{
				//Puts("OnFindBurnable: " + oven.ShortPrefabName + " : " + oven.cookingTemperature);
				oven.StopCooking();
				oven.allowByproductCreation = false;
				oven.SetFlag(BaseEntity.Flags.On, true);
				if (oven.fuelType != null && oven.fuelType.itemid != null)
					return ItemManager.CreateByItemID(oven.fuelType.itemid);
				else
					return null;
			}
			// catch all
			return null;
        }

		// for jack o laterns
        private void OnConsumeFuel(BaseOven oven, Item fuel, ItemModBurnable burnable)
        {
			if (oven == null || string.IsNullOrEmpty(oven.ShortPrefabName) ||
				oven.OwnerID == null || oven.OwnerID == 0U || oven.OwnerID.ToString() == null)
				return;

			if (!permission.UserHasPermission(oven.OwnerID.ToString(), perm_freelights) ||
				!ProcessShortPrefabName(oven.ShortPrefabName) ||
				!IsLightPrefabName(oven.ShortPrefabName))
				return;
			else
			{
				fuel.amount += 1;
				oven.StopCooking();
				oven.allowByproductCreation = false;
				oven.SetFlag(BaseEntity.Flags.On, true);
			}
			// catch all
			return;
        }

		// for hats
        private void OnItemUse(Item item, int amount)
        {
			string ShortPrefabName = item?.parent?.parent?.info?.shortname ?? item?.GetRootContainer()?.entityOwner?.ShortPrefabName;
			BasePlayer player = null;
			string entityId = null;

			if (string.IsNullOrEmpty(ShortPrefabName))
				return;
            else if (IsHatPrefabName(ShortPrefabName))
			{
				try
				{
					player = item?.GetRootContainer()?.playerOwner;
					entityId = item?.GetRootContainer()?.entityOwner?.OwnerID.ToString();
				}
				catch
				{
					player = null;
					entityId = null;
				}

				if (string.IsNullOrEmpty(player.UserIDString) && string.IsNullOrEmpty(entityId))
				{
					//Puts("OnItemUse no perm");
					return;  // no owner so no permission
				}
				try
				{
					if (permission.UserHasPermission(player.UserIDString, perm_freelights) ||
						permission.UserHasPermission(entityId, perm_freelights))
						item.amount += amount;
				}
				catch
				{
					return;
				}
			}
			return;
		}

		// automatically set lights on that are deployed if the lights are in the on state
		private void OnEntitySpawned(BaseNetworkable entity)
		{
			// Puts(entity.ShortPrefabName);
			// will turn the light on during a lights on phase or if neither is set to on
			if ((config.AlwaysOn || NightToggleactive) &&
				 ProcessShortPrefabName(entity.ShortPrefabName))
			{
				if (entity is BaseOven)
				{
					var bo = entity as BaseOven;
					bo.SetFlag(BaseEntity.Flags.On, true);
				}
				else if (entity is CeilingLight)
				{
					var cl = entity as CeilingLight;
					cl.SetFlag(BaseEntity.Flags.On, true);
				}
				else if (entity is SimpleLight)
				{
					var sl = entity as SimpleLight;
					sl.SetFlag(BaseEntity.Flags.On, true);
				}
				else if (entity is SearchLight)
				{
					var sl = entity as SearchLight;
					sl.SetFlag(BaseEntity.Flags.On, true);
					sl.secondsRemaining = 99999999;
				}
				else if (entity is Candle)
				{
					var ca = entity as Candle;
					ca.SetFlag(BaseEntity.Flags.On, true);
				}
			}
			if ((config.DevicesAlwaysOn || NightToggleactive) &&
				 ProcessShortPrefabName(entity.ShortPrefabName))
			{
				if (entity is FogMachine)
				{
                    var fm = entity as FogMachine;
					fm.SetFlag(BaseEntity.Flags.On, true);
                    fm.EnableFogField();
                    fm.StartFogging();
				}
				else if (entity is SnowMachine)
				{
 					var sl = entity as SnowMachine;
					sl.SetFlag(BaseEntity.Flags.On, true);
				}
				else if (entity is StrobeLight)
				{
					var sl = entity as StrobeLight;
					sl.SetFlag(BaseEntity.Flags.On, true);
				}
				else if (entity is SpookySpeaker)
				{
					var ss = entity as SpookySpeaker;
                    ss.SetFlag(BaseEntity.Flags.On, true);
                    ss.SendPlaySound();
				}
			}
		}

		[Command("lights"), Permission(perm_lightson)]
		private void ChatCommandlo(IPlayer player, string cmd, string[] args)
        {
            bool   state		= false;
			string statestring	= null;
            string prefabName	= null;

            if (args == null || args.Length < 1)
			{
                player.Message(String.Concat(Lang("prefix", player.Id), Lang("syntax", player.Id)));
				return;
			}
			else
			{
				// set the parameters
				statestring = args[0].ToLower();

				// make sure we have something to process default to all on
				if (string.IsNullOrEmpty(statestring))
					state = true;
				else if (statestring == "off" || statestring == "false" || statestring == "0" || statestring == "out")
					state = false;
				else if (statestring == "on" || statestring == "true" || statestring == "1" || statestring == "go")
					state = true;
				else
				{
					player.Message(String.Concat(Lang("prefix", player.Id), Lang("state", player.Id)) + " " + statestring);
					return;
				}

				// see if there is a prefabname specified and if so that it is valid
				if (args.Length > 1)
				{
					prefabName = CleanedName(args[1].ToLower());

					if(string.IsNullOrEmpty(prefabName))
						prefabName = "all";
					else if (prefabName != "all" && 
							!IsLightPrefabName(prefabName) && 
							!CanCookShortPrefabName(prefabName) &&
							!IsDevicePrefabName(prefabName)
							)
					{
						player.Message(String.Concat(Lang("prefix") , Lang("bad prefab", player.Id))+ " " + prefabName);
						return;
					}
				}
				else
					prefabName = "all";

				if (prefabName == "all")
				{
					ProcessLights(state, prefabName);
					ProcessDevices(state, prefabName);
				}
				else
				{
					if (IsDevicePrefabName(prefabName))
						ProcessDevices(state, prefabName);
					if (IsLightPrefabName(prefabName) || CanCookShortPrefabName(CleanedName(prefabName)))
						ProcessLights(state, prefabName);
				}
	
				if (state)
					player.Message(String.Concat(Lang("prefix") , Lang("lights on", player.Id)) + " " + prefabName);
				else
					player.Message(String.Concat(Lang("prefix") , Lang("lights off", player.Id)) + " " + prefabName);
			}
		}
    }
}
