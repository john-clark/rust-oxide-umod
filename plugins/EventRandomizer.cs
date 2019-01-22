using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Event Randomizer", "mvrb", "0.2.0")]
    [Description("Random timers for Cargo Ship, Patrol Helicopter and Chinook")]
    class EventRandomizer : RustPlugin
    {
        private float heliInterval;
        private float chinookInterval;
        private float cargoInterval;

        private int lastHeli;
        private int lastChinook;
        private int lastCargo;

        private string permSpawnChinook = "eventrandomizer.spawn.ch47";
        private string permSpawnHeli = "eventrandomizer.spawn.heli";
        private string permSpawnCargo = "eventrandomizer.spawn.cargo";
        private string permCheckTimer = "eventrandomizer.check";

        private class EventTimer
        {
            public float Min;
            public float Max;
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NextHeliIn"] = "Next Patrol Helicopter spawns in {0}",
                ["NextChinookIn"] = "Next Chinook Helicopter spawns in {0}",
                ["NextCargoIn"] = "Next Cargo Ship spawns in {0}",

                ["CargoSpawned"] = "A Cargo Ship has been spawned.",
                ["PatrolHelicopterSpawned"] = "A Patrol Helicopter has been spawned.",
                ["ChinookSpawned"] = "A Chinook Helicopter has been spawned.",
				
                ["FormatTime"] = "{0} Hours {1} Minutes",

                ["EventNotEnabled"] = "The Min and Max timer for {0} is less than or equal to 0 so this event has been disabled.",

                ["Warning: MinGreaterThanMax"] = "The minimum value ({0}) for {1} is greater than the maximum value ({2})!",

                ["Error: NoPermission"] = "You do not have permission to use this command.",
            }, this);
        }

        private void OnServerInitialized()
        {
            permission.RegisterPermission(permSpawnChinook, this);
            permission.RegisterPermission(permSpawnHeli, this);
            permission.RegisterPermission(permSpawnCargo, this);
            permission.RegisterPermission(permCheckTimer, this);

            LoadVariables();

            foreach (var entry in configData.EventTimers)
            {
                if (entry.Value.Min > entry.Value.Max)
                {
                    PrintWarning(Lang("Warning: MinGreaterThanMax", null, entry.Value.Min, entry.Key, entry.Value.Max));
                }
            }

            if (configData.EventTimers["Helicopter"].Min > 0 && configData.EventTimers["Helicopter"].Max > 0)
            {
                heliInterval = UnityEngine.Random.Range(configData.EventTimers["Helicopter"].Min, configData.EventTimers["Helicopter"].Max);
                timer.Once(heliInterval, SpawnHeliRandom);
                PrintWarning(Lang("NextHeliIn", null, FormatTime(heliInterval)));
            }
            else
            {
                PrintWarning(Lang("EventNotEnabled", null, "Helicopter"));
            }

            if (configData.EventTimers["Chinook"].Min > 0 && configData.EventTimers["Chinook"].Max > 0)
            {
                chinookInterval = UnityEngine.Random.Range(configData.EventTimers["Chinook"].Min, configData.EventTimers["Chinook"].Max);
                timer.Once(chinookInterval, SpawnChinookRandom);
                PrintWarning(Lang("NextChinookIn", null, FormatTime(chinookInterval)));
            }
            else
            {
                PrintWarning(Lang("EventNotEnabled", null, "Chinook"));
            }

            if (configData.EventTimers["Cargo"].Min > 0 && configData.EventTimers["Cargo"].Max > 0)
            {
                cargoInterval = UnityEngine.Random.Range(configData.EventTimers["Cargo"].Min, configData.EventTimers["Cargo"].Max);
                timer.Once(cargoInterval, SpawnCargoRandom);
                PrintWarning(Lang("NextCargoIn", null, FormatTime(cargoInterval)));
            }
            else
            {
                PrintWarning(Lang("EventNotEnabled", null, "Cargo"));
            }

            var currentTime = GetUnix();

            lastHeli = currentTime;
            lastChinook = currentTime;
            lastCargo = currentTime;
        }

        [ChatCommand("heli")]
        private void CmdHeli(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, permCheckTimer))
            {
                player.ChatMessage(Lang("Error: NoPermission", player.UserIDString));
                return;
            }

            player.ChatMessage(Lang("NextHeliIn", player.UserIDString, FormatTime(heliInterval + lastHeli - GetUnix())));
        }

        [ChatCommand("chinook")]
        private void CmdChinook(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, permCheckTimer))
            {
                player.ChatMessage(Lang("Error: NoPermission", player.UserIDString));
                return;
            }

			player.ChatMessage(Lang("NextChinookIn", player.UserIDString, FormatTime(chinookInterval + lastChinook - GetUnix())));
        }

        [ChatCommand("cargo")]
        private void CmdCargo(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, permCheckTimer))
            {
                player.ChatMessage(Lang("Error: NoPermission", player.UserIDString));
                return;
            }

			player.ChatMessage(Lang("NextCargoIn", player.UserIDString, FormatTime(cargoInterval + lastCargo - GetUnix())));
        }

        [ConsoleCommand("ch47.spawn")]
        void ConsoleCmdSpawnCh47(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();

            if (arg?.Connection != null && player && !permission.UserHasPermission(player.UserIDString, permSpawnChinook))
            {
                player.ChatMessage(Lang("Error: NoPermission", player.UserIDString));
                return;
            }

            SpawnChinook();
        }

        [ConsoleCommand("heli.spawn")]
        void ConsoleCmdSpawnHeli(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();

            if (arg?.Connection != null && player && !permission.UserHasPermission(player.UserIDString, permSpawnHeli))
            {
                player.ChatMessage(Lang("Error: NoPermission", player.UserIDString));
                return;
            }

            SpawnHeli();
        }

        [ConsoleCommand("cargo.spawn")]
        void ConsoleCmdSpawnCargo(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();

            if (arg?.Connection != null && player && !permission.UserHasPermission(player.UserIDString, permSpawnCargo))
            {
                player.ChatMessage(Lang("Error: NoPermission", player.UserIDString));
                return;
            }

            SpawnCargo();
        }

        private void SpawnCargoRandom()
        {
            SpawnCargo();
            lastCargo = GetUnix();

            cargoInterval = UnityEngine.Random.Range(configData.EventTimers["Cargo"].Min, configData.EventTimers["Cargo"].Max);
            timer.Once(cargoInterval, SpawnCargoRandom);
        }


        private void SpawnHeliRandom()
        {
            SpawnHeli();
            lastHeli = GetUnix();

            heliInterval = UnityEngine.Random.Range(configData.EventTimers["Helicopter"].Min, configData.EventTimers["Helicopter"].Max);
            timer.Once(heliInterval, SpawnHeliRandom);
        }

        private void SpawnChinookRandom()
        {
            SpawnChinook();
            lastChinook = GetUnix();

            chinookInterval = UnityEngine.Random.Range(configData.EventTimers["Chinook"].Min, configData.EventTimers["Chinook"].Max);
            timer.Once(chinookInterval, SpawnChinookRandom);
        }


        private string FormatTime(float seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(seconds);

            return Lang("FormatTime", null, time.Hours, time.Minutes);
        }

        private void SpawnCargo()
        {
            var cargoShip = GameManager.server.CreateEntity("assets/content/vehicles/boats/cargoship/cargoshiptest.prefab") as CargoShip;
            if (cargoShip == null) return;
            cargoShip.TriggeredEventSpawn();
            cargoShip.Spawn();

            PrintWarning(Lang("CargoSpawned"));
        }

        private void SpawnHeli()
        {
            var heli = GameManager.server.CreateEntity("assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab");
            if (heli == null) return;
            heli.Spawn();

            PrintWarning(Lang("PatrolHelicopterSpawned"));
        }

        private void SpawnChinook()
        {
            var ch47 = (CH47HelicopterAIController)GameManager.server.CreateEntity("assets/prefabs/npc/ch47/ch47scientists.entity.prefab", new Vector3(0, 100, 0));
            if (ch47 == null) return;
            ch47.Spawn();

            PrintWarning(Lang("ChinookSpawned"));
        }

        private int GetUnix() => (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #region Config        
        private ConfigData configData;

        class ConfigData
        {
            [JsonProperty(PropertyName = "Event Timers in seconds")]
            public Dictionary<string, EventTimer> EventTimers { get; set; }
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                EventTimers = new Dictionary<string, EventTimer>()
                {
                    { "Helicopter", new EventTimer(){ Min = 3600, Max = 7200 } },
                    { "Chinook", new EventTimer(){ Min = 7200, Max = 14400 } },
                    { "Cargo", new EventTimer(){ Min = 7200, Max = 10800 } }
                }
            };

            SaveConfig(config);
        }

        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();

        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion
    }
}