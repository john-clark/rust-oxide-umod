using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Timed Events", "Orange", "1.0.3")]
    [Description("Triggers various types of events like Airdrops, Helicopters and same")]
    public class TimedEvents : RustPlugin
    {
        #region Vars
        
        private List<string> entities = new List<string>
        {
            "assets/prefabs/npc/m2bradley/bradleyapc.prefab",
            "assets/prefabs/npc/ch47/ch47scientists.entity.prefab",
            "assets/prefabs/npc/cargo plane/cargo_plane.prefab",
            "assets/content/vehicles/boats/cargoship/cargoshiptest.prefab",
            "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab"
        };

        #endregion
        
        #region Oxide Hooks

        private void OnServerInitialized()
        {
            InitTimers();
        }

        private void OnEntitySpawned(BaseEntity entity)
        {
            if (entity is SupplySignal)
            {
                DelaySubscribe(10);
                return;
            }

            CheckEntity(entity);
        }

        #endregion

        #region Core
        
        private void InitTimers()
        {
            foreach (var entity in entities)
            {
                Spawn(entity, true);
            }
        }

        private void DelaySubscribe(int time = 0)
        {
            Unsubscribe("OnEntitySpawned");
            
            timer.Once(time, () =>
            {
                Subscribe("OnEntitySpawned");
            });
        }
        
        private void CheckEntity(BaseEntity entity)
        {
            var name = entity.ShortPrefabName;
            var config = GetConfig(name);
            
            if (config == null)
            {
                return;
            }

            if (config.disableDefault)
            {
                entity.Kill();
            }
        }
        
        private ConfigData.OEvent GetConfig(string name)
        {
            switch (name)
            {
                default:
                    return null;

                case "assets/prefabs/npc/ch47/ch47scientists.entity.prefab":
                case "ch47scientists.entity":
                    return config.ch47;

                case "assets/prefabs/npc/m2bradley/bradleyapc.prefab":
                case "bradleyapc":
                    return config.tank;

                case "assets/content/vehicles/boats/cargoship/cargoshiptest.prefab":
                case "cargoshiptest":
                    return config.ship;

                case "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab":
                case "patrolhelicopter":
                    return config.patrol;

                case "assets/prefabs/npc/cargo plane/cargo_plane.prefab":
                case "cargo_plane":
                    return config.plane;
            }
        }

        private void Spawn(string prefab, bool noSpawn = false)
        {
            var config = GetConfig(prefab);
            if (config == null) {return;}
            var time = Core.Random.Range(config.timerMin, config.timerMax);
            if (time == 0) {return;}
            
            timer.Once(time, () => Spawn(prefab));
            
            if (noSpawn)
            {
                return;
            }

            if (BasePlayer.activePlayerList.Count < config.playersMin)
            {
                return;
            }

            var amount = Core.Random.Range(config.spawnMin, config.spawnMax);
            DelaySubscribe(1);

            for (var i = 0; i < amount; i++)
            {
                if (prefab.Contains("bradleyapc"))
                {
                    SpawnTank();
                    continue;
                }

                if (prefab.Contains("cargoship"))
                {
                    SpawnShip();
                    continue;
                }

                var position = new Vector3(ConVar.Server.worldsize, 100, ConVar.Server.worldsize) - new Vector3(50f, 0f, 50f);
                var entity = GameManager.server.CreateEntity(prefab, position);
                entity?.Spawn();
            }
        }

        private void SpawnTank()
        {
            BradleySpawner.singleton?.SpawnBradley();
        }

        private void SpawnShip()
        {
            var x = TerrainMeta.Size.x;
            var vector3 = Vector3Ex.Range(-1f, 1f);
            vector3.y = 0.0f;
            vector3.Normalize();
            var worldPos = vector3 * (x * 1f);
            worldPos.y = TerrainMeta.WaterMap.GetHeight(worldPos);
            var entity = GameManager.server.CreateEntity(entities[3], worldPos);
            entity?.Spawn();
        }

        #endregion

        #region Configuration

        private static ConfigData config;
        
        private class ConfigData
        {    
            [JsonProperty(PropertyName = "1. Cargo plane settings:")]
            public OEvent plane;
            
            [JsonProperty(PropertyName = "2. Patrol Helicopter settings:")]
            public OEvent patrol;
            
            [JsonProperty(PropertyName = "3. Bradley APC settings:")]
            public OEvent tank;
            
            [JsonProperty(PropertyName = "4. CH47 settings:")]
            public OEvent ch47;
            
            [JsonProperty(PropertyName = "5. Cargo ship settings:")]
            public OEvent ship;

            public class OEvent
            {
                [JsonProperty(PropertyName = "1. Disable default spawns")]
                public bool disableDefault;
                
                [JsonProperty(PropertyName = "2. Minimal respawn time (in seconds)")]
                public int timerMin;
                
                [JsonProperty(PropertyName = "3. Maximal respawn time (in seconds)")]
                public int timerMax;
                
                [JsonProperty(PropertyName = "4. Minimal amount that spawned by once")]
                public int spawnMin;
                
                [JsonProperty(PropertyName = "5. Maximal amount that spawned by once")]
                public int spawnMax;
                
                [JsonProperty(PropertyName = "6. Minimal players to start event")]
                public int playersMin;
            }
        }
        
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                plane = new ConfigData.OEvent(),
                patrol = new ConfigData.OEvent(),
                tank = new ConfigData.OEvent(),
                ch47 = new ConfigData.OEvent(),
                ship = new ConfigData.OEvent(),
            };
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
   
            try
            {
                config = Config.ReadObject<ConfigData>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintError("Configuration file is corrupt(or not exists), creating new one!");
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion
    }
}