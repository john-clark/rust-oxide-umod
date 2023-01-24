using Oxide.Core;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("AutoLightsOut", "DylanSMR", "1.0.3")]
    [Description("Turn off all lights at night, user must manually turn on again, this allows users on modded servers with lots of wood to find houses at night with players in them.")]

    class AutoLightsOut : RustPlugin
    {
        TOD_Sky sky;
        Configuration config;
        bool Activated;
        List<uint> TurnedOff = new List<uint>();

        public class Configuration
        {
            [JsonProperty(PropertyName = "Trigger : 1 = Turn off all at the same time, 2 = Turn off each one when they burn a piece of fuel. (Default=1)")]
            public int Trigger;

            [JsonProperty(PropertyName = "Trigger Interval : How often the plugin checks if it is night, only change for trigger type one. (Default=60) (SECONDS)")]
            public int TriggerTime = 60;

            [JsonProperty(PropertyName = "Camp Fires : If camp fires should be turned off. (Default=true)")]
            public bool CampFires;

            [JsonProperty(PropertyName = "Furnaces : If furnaces should be turned off. (Default=true)")]
            public bool Furnaces;

            [JsonProperty(PropertyName = "Lanterns : If lanterns should be turned off. (Default=true)")]
            public bool Lanterns;
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file.");
            config = new Configuration()
            {
                CampFires = true,
                Furnaces = true,
                Lanterns = true,
                Trigger = 1,
                TriggerTime = 60
            };
            SaveConfig(config);
        }
        void SaveConfig(Configuration config)
        {
            Config.WriteObject(config, true);
            SaveConfig();
        }
        public void LoadConfigVars()
        {
            PrintWarning("Loading configuration.");
            config = Config.ReadObject<Configuration>();
            Config.WriteObject(config, true);
        }

        void Loaded()
        {
            sky = TOD_Sky.Instance;
            LoadConfigVars();

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"NIGHTTIME_REACHED", "Your lights, camp fires, and furnaces have been turned off! Feel free to turn them on if needed."}
            }, this);

            if (config == null)
            {
                Interface.Oxide.ReloadPlugin("AutoLightsOut");
                return;
            }

            if (config.Trigger == 1)
            {
                timer.Every(config.TriggerTime, () =>
                {
                    if (sky.IsNight && !Activated)
                    {
                        Activated = true;
                        PrintToChat(lang.GetMessage("NIGHTTIME_REACHED", this));
                        Puts(lang.GetMessage("NIGHTTIME_REACHED", this));

                        BaseOven[] Ovens = UnityEngine.Object.FindObjectsOfType<BaseOven>();
                        if (Ovens == null) return;

                        foreach (BaseOven oven in Ovens)
                        {
                            CheckOven(oven);
                        }
                    }
                    if (sky.IsDay && Activated)
                        Activated = false;
                });
            }
        }

        void CheckOven(BaseOven oven)
        {
            string pfn = oven.PrefabName.ToLower();
            if (pfn.Contains("campfire"))
            {
                if (config.CampFires)
                {
                    oven.StopCooking();
                    oven.SetFlag(BaseEntity.Flags.On, false);
                }
            }
            else if (pfn.Contains("furnace"))
            {
                if (config.Furnaces)
                {
                    oven.StopCooking();
                    oven.SetFlag(BaseEntity.Flags.On, false);
                }
            }
            else
            {
                if (config.Lanterns)
                {
                    oven.StopCooking();
                    oven.SetFlag(BaseEntity.Flags.On, false);
                }
            }
        }

        void OnConsumeFuel(BaseOven oven, Item fuel, ItemModBurnable burnable)
        {
            if (config.Trigger != 2) return;
            if (oven == null) return;

            if (sky.IsNight)
            {
                if (TurnedOff.Contains(oven.net.ID)) return;
                else TurnedOff.Add(oven.net.ID);

                if (!Activated)
                    Activated = true;

                CheckOven(oven);
            }
            if (sky.IsDay)
            {
                Activated = false;
                TurnedOff.Clear();
            }
        }
    }
}