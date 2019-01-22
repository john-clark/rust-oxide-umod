using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AutomatedEvents", "k1lly0u", "0.1.0", ResourceId = 0)]
    class AutomatedEvents : RustPlugin
    {
        #region Fields
        private Dictionary<EventType, Timer> eventTimers = new Dictionary<EventType, Timer>(); 
        #endregion

        #region Oxide Hooks
        
        void OnServerInitialized()
        {
            LoadVariables();
            foreach (var eventType in configData.Events)
                StartEventTimer(eventType.Key);
        }
        void Unload()
        {
            foreach(var timer in eventTimers)
            {
                if (timer.Value != null)
                    timer.Value.Destroy();
            }
        }
        #endregion

        #region Functions
        void StartEventTimer(EventType type)
        {
            var config = configData.Events[type];
            if (!config.Enabled) return;
            eventTimers[type] = timer.In(UnityEngine.Random.Range(config.MinimumTimeBetween, config.MaximumTimeBetween) * 60, () => RunEvent(type));
        }
        void RunEvent(EventType type)
        {
            string prefabName = string.Empty;
            switch (type)
            {
                case EventType.CargoPlane:
                    prefabName = "assets/prefabs/npc/cargo plane/cargo_plane.prefab";
                    break;
                case EventType.Helicopter:
                    prefabName = "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab";
                    break;
                case EventType.XMasEvent:
                    rust.RunServerCommand("xmas.refill");                                        
                    break;                
            }
            if (!string.IsNullOrEmpty(prefabName))
            {
                var entity = GameManager.server.CreateEntity(prefabName, new Vector3(), new Quaternion(), true);
                entity.Spawn();
            }
            StartEventTimer(type);
        }
        #endregion

        #region Config        
        enum EventType { CargoPlane, Helicopter, XMasEvent }
        private ConfigData configData;
        class ConfigData
        {
            public Dictionary<EventType, EventEntry> Events { get; set; }           
        }
        class EventEntry
        {
            public bool Enabled { get; set; }
            public int MinimumTimeBetween { get; set; }
            public int MaximumTimeBetween { get; set; }
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
                Events = new Dictionary<EventType, EventEntry>
                {
                    { EventType.CargoPlane, new EventEntry
                    {
                        Enabled = true,
                        MinimumTimeBetween = 30,
                        MaximumTimeBetween = 45
                    }
                    },
                    { EventType.Helicopter, new EventEntry
                    {
                        Enabled = true,
                        MinimumTimeBetween = 45,
                        MaximumTimeBetween = 60
                    }
                    },
                    { EventType.XMasEvent, new EventEntry
                    {
                        Enabled = false,
                        MinimumTimeBetween = 60,
                        MaximumTimeBetween = 120
                    }
                    }
                }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion        
    }
}