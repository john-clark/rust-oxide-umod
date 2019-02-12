// Requires: GatherManager

using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Day/Night Gather", "Dubz", "1.0.5", ResourceId = 2297)]
    [Description("Set different gather rates for day and night")]

    class DayNightGather : CovalencePlugin
    {
        #region Initialization

        ConfigData configData;

        bool dayRate;
        bool nightRate;

        void OnServerInitialized()
        {
            LoadVariables();
            LoadDefaultMessages();
            PrintWarning(server.Time.TimeOfDay.Hours.ToString());
            SetGatherRate();
        }

        void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"DayGatherRate", "Day: {0}x gather rate"},
                {"NightGatherRate", "Night: {0}x gather rate"}
            }, this);
        }

        #endregion

        #region Configuration

        class ConfigData
        {
            public bool BroadcastMessage { get; set; }
            public float DayGatherRateDispenser { get; set; }
            public float DayGatherRatePickup { get; set; }
            public float DayGatherRateQuarry { get; set; }
            public float NightGatherRateDispenser { get; set; }
            public float NightGatherRatePickup { get; set; }
            public float NightGatherRateQuarry { get; set; }
            public bool UseTOD { get; set; }
			public float SunRise { get; set; }
			public float SunSet { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                BroadcastMessage = true,
                DayGatherRateDispenser = 1f,
                DayGatherRatePickup = 1f,
                DayGatherRateQuarry = 1f,
                NightGatherRateDispenser = 1f,
                NightGatherRatePickup = 1f,
                NightGatherRateQuarry = 1f,
                UseTOD = false,
				SunRise = 7f,
				SunSet = 19f
            };
            SaveConfig(config);
        }

        void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();

        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);

        #endregion

        #region The Cycle

        bool IsDay => server.Time.TimeOfDay.Hours >= configData.SunRise && server.Time.TimeOfDay.Hours <= configData.SunSet;
        bool IsNight => !IsDay;

        void OnTick()
        {
            if (!configData.UseTOD)
            {
                SetGatherRate();
            }
        }

        void SetGatherRate()
        {
            if (IsDay && !dayRate)
            {
                DayGatherRate();
                dayRate = true;
                nightRate = false;
            }
            else if (IsNight && !nightRate)
            {
                NightGatherRate();
                nightRate = true;
                dayRate = false;
            }
        }
        void OnTimeSunset()
        {
            if (configData.UseTOD && plugins.Exists("TimeOfDay"))
            {
                NightGatherRate();
            }
        }

        void OnTimeSunrise()
        {
            if (configData.UseTOD && plugins.Exists("TimeOfDay"))
            {
                DayGatherRate();
            }
        }

        void DayGatherRate()
        {
            server.Command("gather.rate dispenser * " + configData.DayGatherRateDispenser);
            server.Command("gather.rate pickup * " + configData.DayGatherRatePickup);
            server.Command("gather.rate quarry * " + configData.DayGatherRateQuarry);
            server.Command("dispenser.scale tree " + configData.DayGatherRateDispenser);
            server.Command("dispenser.scale ore " + configData.DayGatherRateDispenser);
            server.Command("dispenser.scale corpse " + configData.DayGatherRateDispenser);
            if (configData.BroadcastMessage)
            {
                Broadcast("DayGatherRate", configData.DayGatherRateDispenser);
            }

            
        }

        void NightGatherRate()
        {
            server.Command("gather.rate dispenser * " + configData.NightGatherRateDispenser);
            server.Command("gather.rate pickup * " + configData.NightGatherRatePickup);
            server.Command("gather.rate quarry * " + configData.NightGatherRateQuarry);
            server.Command("dispenser.scale tree " + configData.NightGatherRateDispenser);
            server.Command("dispenser.scale ore " + configData.NightGatherRateDispenser);
            server.Command("dispenser.scale corpse " + configData.NightGatherRateDispenser);
            if (configData.BroadcastMessage)
            {
                Broadcast("NightGatherRate", configData.NightGatherRateDispenser);
            }
        }

        #endregion

        #region Helpers

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        void Broadcast(string key, params object[] args)
        {
            foreach (var player in players.Connected) player.Message(Lang(key, player.Id, args));
        }

        #endregion
    }
}