using System.Collections.Generic;
using Facepunch;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Oxide.Plugins
{
    [Info("RadtownAnimals", "k1lly0u", "0.2.82", ResourceId = 1561)]
    class RadtownAnimals : RustPlugin
    {
        #region Fields
        private List<BaseCombatEntity> animalList = new List<BaseCombatEntity>();

        const string zombiePrefab = "assets/prefabs/npc/murderer/murderer.prefab";
        const string scientistPrefab = "assets/prefabs/npc/scientist/scientist.prefab";
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            lang.RegisterMessages(messages, this);
        }

        private void OnServerInitialized()
        {            
            
            InitializeAnimalSpawns();
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null) return;

            if (animalList.Contains(entity))
            {
                AnimalController baseNpc = entity.GetComponent<AnimalController>();
                if (baseNpc != null)
                {
                    baseNpc.naturalDeath = true;
                    timer.In(configData.Settings.Respawn, () => SpawnAnimalEntity(baseNpc.prefabName, baseNpc.homePos));
                    UnityEngine.Object.Destroy(entity.GetComponent<AnimalController>());  
                }

                HumanController npcPlayer = entity.GetComponent<HumanController>();
                if (npcPlayer != null)
                {
                    npcPlayer.naturalDeath = true;
                    timer.In(configData.Settings.Respawn, () => SpawnNpcEntity(npcPlayer.prefabName, npcPlayer.homePos)); 
                    UnityEngine.Object.Destroy(entity.GetComponent<HumanController>());
                }

                animalList.Remove(entity);
            }
        }

        private void Unload()
        {            
            foreach (var animal in animalList)
            {
                if (animal != null)
                {
                    if (animal.GetComponent<AnimalController>())
                        UnityEngine.Object.Destroy(animal.GetComponent<AnimalController>());
                    else UnityEngine.Object.Destroy(animal.GetComponent<HumanController>());
                }
            }

            var animals = UnityEngine.Object.FindObjectsOfType<AnimalController>();
            if (animals != null)
            {
                foreach (var gameObj in animals)
                    UnityEngine.Object.Destroy(gameObj);
            }

            var npcs = UnityEngine.Object.FindObjectsOfType<HumanController>();
            if (npcs != null)
            {
                foreach (var gameObj in npcs)
                    UnityEngine.Object.Destroy(gameObj);
            }

            animalList.Clear();
        }
        #endregion

        #region Initial Spawning
        private void InitializeAnimalSpawns()
        {
            var allobjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            foreach (var gobject in allobjects)
            {
                if (gobject.name.Contains("autospawn/monument"))
                {
                    var pos = gobject.transform.position;                    

                    if (gobject.name.Contains("lighthouse"))
                    {
                        if (configData.Zone.Lighthouse.Enabled)
                            SpawnAnimals(pos, GetSpawnList(configData.Zone.Lighthouse.Counts));
                        continue;
                    }

                    if (gobject.name.Contains("powerplant_1"))
                    {
                        if (configData.Zone.Powerplant.Enabled)
                            SpawnAnimals(pos, GetSpawnList(configData.Zone.Powerplant.Counts));
                        continue;
                    }

                    if (gobject.name.Contains("military_tunnel_1"))
                    {
                        if(configData.Zone.Tunnels.Enabled)
                            SpawnAnimals(pos, GetSpawnList(configData.Zone.Tunnels.Counts));
                        continue;
                    }

                    if (gobject.name.Contains("harbor_1"))
                    {
                        if (configData.Zone.LargeHarbor.Enabled)
                            SpawnAnimals(pos, GetSpawnList(configData.Zone.LargeHarbor.Counts));
                        continue;
                    }

                    if (gobject.name.Contains("harbor_2"))
                    {
                        if (configData.Zone.SmallHarbor.Enabled)
                            SpawnAnimals(pos, GetSpawnList(configData.Zone.SmallHarbor.Counts));
                        continue;
                    }

                    if (gobject.name.Contains("airfield_1"))
                    {
                        if (configData.Zone.Airfield.Enabled)
                            SpawnAnimals(pos, GetSpawnList(configData.Zone.Airfield.Counts));
                        continue;
                    }

                    if (gobject.name.Contains("trainyard_1"))
                    {
                        if (configData.Zone.Trainyard.Enabled)
                            SpawnAnimals(pos, GetSpawnList(configData.Zone.Trainyard.Counts));
                        continue;
                    }

                    if (gobject.name.Contains("water_treatment_plant_1"))
                    {
                        if (configData.Zone.WaterTreatment.Enabled)
                            SpawnAnimals(pos, GetSpawnList(configData.Zone.WaterTreatment.Counts));
                        continue;
                    }

                    if (gobject.name.Contains("warehouse"))
                    {
                        if (configData.Zone.Warehouse.Enabled)
                            SpawnAnimals(pos, GetSpawnList(configData.Zone.Warehouse.Counts));
                        continue;
                    }

                    if (gobject.name.Contains("satellite_dish"))
                    {
                        if (configData.Zone.Satellite.Enabled)
                            SpawnAnimals(pos, GetSpawnList(configData.Zone.Satellite.Counts));
                        continue;
                    }

                    if (gobject.name.Contains("sphere_tank"))
                    {
                        if (configData.Zone.Dome.Enabled)
                            SpawnAnimals(pos, GetSpawnList(configData.Zone.Dome.Counts));
                        continue;
                    }

                    if (gobject.name.Contains("radtown_small_3"))
                    {
                        if (configData.Zone.Radtown.Enabled)
                            SpawnAnimals(pos, GetSpawnList(configData.Zone.Radtown.Counts));
                        continue;
                    }

                    if (gobject.name.Contains("launch_site_1"))
                    {
                        if (configData.Zone.RocketFactory.Enabled)
                            SpawnAnimals(pos, GetSpawnList(configData.Zone.RocketFactory.Counts));
                        continue;
                    }

                    if (gobject.name.Contains("gas_station_1"))
                    {
                        if (configData.Zone.GasStation.Enabled)
                            SpawnAnimals(pos, GetSpawnList(configData.Zone.GasStation.Counts));
                        continue;
                    }

                    if (gobject.name.Contains("supermarket_1"))
                    {                        
                        if (configData.Zone.Supermarket.Enabled)
                            SpawnAnimals(pos, GetSpawnList(configData.Zone.Supermarket.Counts));
                        continue;
                    }

                    if (gobject.name.Contains("mining_quarry_c"))
                    {
                        if (configData.Zone.Quarry_HQM.Enabled)
                            SpawnAnimals(pos, GetSpawnList(configData.Zone.Quarry_HQM.Counts));
                        continue;
                    }

                    if (gobject.name.Contains("mining_quarry_a"))
                    {
                        if (configData.Zone.Quarry_Sulfur.Enabled)
                            SpawnAnimals(pos, GetSpawnList(configData.Zone.Quarry_Sulfur.Counts));
                        continue;
                    }

                    if (gobject.name.Contains("mining_quarry_b"))
                    {
                        if (configData.Zone.Quarry_Stone.Enabled)
                            SpawnAnimals(pos, GetSpawnList(configData.Zone.Quarry_Stone.Counts));
                        continue;
                    }

                    if (gobject.name.Contains("junkyard_1"))
                    {
                        if (configData.Zone.Junkyard.Enabled)
                            SpawnAnimals(pos, GetSpawnList(configData.Zone.Junkyard.Counts));
                        continue;
                    }
                }               
            }
        }

        private Dictionary<string, int> GetSpawnList(ConfigData.Zones.MonumentSettings.AnimalCounts counts)
        {
            var spawnList = new Dictionary<string, int>
            {
                {"bear", counts.Bears},
                {"boar", counts.Boars },
                {"chicken", counts.Chickens },
                {"horse", counts.Horses },
                {"murderer", counts.Murderers },
                {"scientist", counts.Scientists },
                {"stag", counts.Stags },
                {"wolf", counts.Wolfs },
                {"zombie", counts.Zombies }
            };
            return spawnList;
        }        

        private void SpawnAnimals(Vector3 position, Dictionary<string,int> spawnList)
        {
            if (animalList.Count >= configData.Settings.Total)
            {
                PrintError(lang.GetMessage("spawnLimit", this));
                return;
            }
            foreach (var type in spawnList)
            {
                for (int i = 0; i < type.Value; i++)
                {
                    if (type.Key == "murderer")
                        SpawnNpcEntity(zombiePrefab, position);
                    else if (type.Key == "scientist")
                        SpawnNpcEntity(scientistPrefab, position);
                    else SpawnAnimalEntity(type.Key, position); 
                }
            }
        }
        #endregion

        #region Spawn Control   
        private void SpawnAnimalEntity(string type, Vector3 pos)
        {
            Vector3 point;
            if (FindPointOnNavmesh(pos, 50, out point))
            {
                BaseCombatEntity entity = InstantiateEntity($"assets/rust.ai/agents/{type}/{type}.prefab", point);
                entity.enableSaving = false;
                entity.Spawn();

                entity.InitializeHealth(entity.StartHealth(), entity.StartMaxHealth());
                entity.lifestate = BaseCombatEntity.LifeState.Alive;

                var npc = entity.gameObject.AddComponent<AnimalController>();
                npc.SetInfo(type, point);
                animalList.Add(entity);                
            }            
        }

        private void SpawnNpcEntity(string prefabPath, Vector3 pos)
        {
            Vector3 point;
            if (FindPointOnNavmesh(pos, 50, out point))
            {
                BaseCombatEntity entity = InstantiateEntity(prefabPath, point);
                entity.enableSaving = false;
                entity.Spawn();

                entity.InitializeHealth(entity.StartHealth(), entity.StartMaxHealth());

                var npc = entity.gameObject.AddComponent<HumanController>();
                npc.SetInfo(prefabPath, point);
                animalList.Add(entity);                
            }
        }

        private BaseCombatEntity InstantiateEntity(string type, Vector3 position)
        {
            var gameObject = Instantiate.GameObject(GameManager.server.FindPrefab(type), position, new Quaternion());
            gameObject.name = type;

            SceneManager.MoveGameObjectToScene(gameObject, Rust.Server.EntityScene);

            UnityEngine.Object.Destroy(gameObject.GetComponent<Spawnable>());

            if (!gameObject.activeSelf)                                       
                gameObject.SetActive(true);

            BaseCombatEntity component = gameObject.GetComponent<BaseCombatEntity>();
            return component;
        }

        private bool FindPointOnNavmesh(Vector3 center, float range, out Vector3 result)
        {
            for (int i = 0; i < 30; i++)
            {
                Vector3 randomPos = center + new Vector3(Random.insideUnitCircle.x * range, 0, Random.insideUnitCircle.x * range);
                NavMeshHit hit;
                if (NavMesh.SamplePosition(randomPos, out hit, 50, 1))
                {
                    if (hit.position.y - TerrainMeta.HeightMap.GetHeight(hit.position) > 3)
                        continue;
                    result = hit.position;
                    return true;
                }
            }
            result = Vector3.zero;
            return false;
        }
        #endregion

        #region NPCController
        class AnimalController : MonoBehaviour
        {
            public BaseNpc npc;
            public string prefabName;
            public Vector3 homePos;
            public bool naturalDeath = false;

            private void Awake()
            {
                npc = GetComponent<BaseNpc>();
                enabled = false;
            }

            private void OnDestroy()
            {
                InvokeHandler.CancelInvoke(this, CheckLocation);

                if (npc != null && !npc.IsDestroyed && !naturalDeath)
                    npc.Kill();
            }

            public void SetInfo(string prefabName, Vector3 homePos)
            {
                this.prefabName = prefabName;
                this.homePos = homePos;
                InvokeHandler.InvokeRepeating(this, CheckLocation, 1f, 20f);
            }

            private void CheckLocation()
            {
                if (Vector3.Distance(npc.transform.position, homePos) > 40)
                {
                    npc.UpdateDestination(homePos);
                }
            }
        }
        class HumanController : MonoBehaviour
        {
            public NPCPlayer npc;
            public string prefabName;
            public Vector3 homePos;
            public bool naturalDeath = false;

            private void Awake()
            {
                npc = GetComponent<NPCPlayer>();
                npc.displayName = RandomUsernames.Get(Random.Range(0, RandomUsernames.All.Length - 1));
                enabled = false;
            }

            private void OnDestroy()
            {
                InvokeHandler.CancelInvoke(this, CheckLocation);

                if (npc != null && !npc.IsDestroyed && !naturalDeath)
                    npc.Kill();
            }

            public void SetInfo(string prefabName, Vector3 homePos)
            {
                this.prefabName = prefabName;
                this.homePos = homePos;
                InvokeHandler.InvokeRepeating(this, CheckLocation, 1f, 20f);
            }

            private void CheckLocation()
            {
                if (Vector3.Distance(npc.transform.position, homePos) > 40)
                {
                    npc.SetDestination(homePos);
                }
            }
        }
        #endregion

        #region Commands
        [ChatCommand("ra_killall")]
        private void chatKillAnimals(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            foreach(var animal in animalList)
            {
                if (animal.GetComponent<AnimalController>())
                    UnityEngine.Object.Destroy(animal.GetComponent<AnimalController>());
                else UnityEngine.Object.Destroy(animal.GetComponent<HumanController>());
            }
            animalList.Clear();
            SendReply(player, lang.GetMessage("title", this, player.UserIDString) + lang.GetMessage("killedAll", this, player.UserIDString));
        }

        [ConsoleCommand("ra_killall")]
        private void ccmdKillAnimals(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
            {
                foreach (var animal in animalList)
                {
                    if (animal.GetComponent<AnimalController>())
                        UnityEngine.Object.Destroy(animal.GetComponent<AnimalController>());
                    else UnityEngine.Object.Destroy(animal.GetComponent<HumanController>());
                }
                animalList.Clear();
                SendReply(arg, lang.GetMessage("killedAll", this));
            }
        }
        #endregion

        #region Config         
        private ConfigData configData;
        class ConfigData
        {
            public Options Settings { get; set; }
            [JsonProperty(PropertyName = "Zone Settings")]
            public Zones Zone { get; set; }

            public class Options
            {
                [JsonProperty(PropertyName = "Animal respawn timer (seconds)")]
                public int Respawn;
                [JsonProperty(PropertyName = "Spawn spread distance from center of monument")]
                public float Spread;
                [JsonProperty(PropertyName = "Maximum amount of animals to spawn")]
                public int Total;
            }

            public class Zones
            {
                public MonumentSettings Airfield { get; set; }
                public MonumentSettings Dome { get; set; }
                public MonumentSettings Junkyard { get; set; }
                public MonumentSettings Lighthouse { get; set; }
                public MonumentSettings LargeHarbor { get; set; }
                public MonumentSettings GasStation { get; set; }
                public MonumentSettings Powerplant { get; set; }
                [JsonProperty(PropertyName = "Stone Quarry")]
                public MonumentSettings Quarry_Stone { get; set; }
                [JsonProperty(PropertyName = "Sulfur Quarry")]
                public MonumentSettings Quarry_Sulfur { get; set; }
                [JsonProperty(PropertyName = "HQM Quarry")]
                public MonumentSettings Quarry_HQM { get; set; }
                public MonumentSettings Radtown { get; set; }
                public MonumentSettings RocketFactory { get; set; }
                public MonumentSettings Satellite { get; set; }
                public MonumentSettings SmallHarbor { get; set; }
                public MonumentSettings Supermarket { get; set; }
                public MonumentSettings Trainyard { get; set; }
                public MonumentSettings Tunnels { get; set; }
                public MonumentSettings Warehouse { get; set; }
                public MonumentSettings WaterTreatment { get; set; }

                public class MonumentSettings
                {
                    [JsonProperty(PropertyName = "Enable spawning at this monument")]
                    public bool Enabled { get; set; }
                    [JsonProperty(PropertyName = "Amount of animals to spawn at this monument")]
                    public AnimalCounts Counts { get; set; }

                    public class AnimalCounts
                    {
                        public int Bears;
                        public int Boars;
                        public int Chickens;
                        public int Horses;
                        public int Murderers;
                        public int Scientists;
                        public int Stags;
                        public int Wolfs;
                        public int Zombies;
                    }
                }
            }
            public Oxide.Core.VersionNumber Version { get; set; }
        }       

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                Settings = new ConfigData.Options
                {
                    Respawn = 900,
                    Spread = 100,
                    Total = 40
                },
                Zone = new ConfigData.Zones
                {
                    Airfield = new ConfigData.Zones.MonumentSettings
                    {
                        Counts = new ConfigData.Zones.MonumentSettings.AnimalCounts
                        {
                            Bears = 0,
                            Boars = 0,
                            Chickens = 0,
                            Horses = 0,
                            Murderers = 0,
                            Scientists = 0,
                            Stags = 0,
                            Wolfs = 0,
                            Zombies = 0
                        },
                        Enabled = false
                    },
                    Dome = new ConfigData.Zones.MonumentSettings
                    {
                        Counts = new ConfigData.Zones.MonumentSettings.AnimalCounts
                        {
                            Bears = 0,
                            Boars = 0,
                            Chickens = 0,
                            Horses = 0,
                            Murderers = 0,
                            Scientists = 0,
                            Stags = 0,
                            Wolfs = 0,
                            Zombies = 0
                        },
                        Enabled = false
                    },
                    GasStation = new ConfigData.Zones.MonumentSettings
                    {
                        Counts = new ConfigData.Zones.MonumentSettings.AnimalCounts
                        {
                            Bears = 0,
                            Boars = 0,
                            Chickens = 0,
                            Horses = 0,
                            Murderers = 0,
                            Scientists = 0,
                            Stags = 0,
                            Wolfs = 0,
                            Zombies = 0
                        },
                        Enabled = false
                    },
                    Junkyard = new ConfigData.Zones.MonumentSettings
                    {
                        Counts = new ConfigData.Zones.MonumentSettings.AnimalCounts
                        {
                            Bears = 0,
                            Boars = 0,
                            Chickens = 0,
                            Horses = 0,
                            Murderers = 0,
                            Scientists = 0,
                            Stags = 0,
                            Wolfs = 0,
                            Zombies = 0
                        },
                        Enabled = false
                    },
                    LargeHarbor = new ConfigData.Zones.MonumentSettings
                    {
                        Counts = new ConfigData.Zones.MonumentSettings.AnimalCounts
                        {
                            Bears = 0,
                            Boars = 0,
                            Chickens = 0,
                            Horses = 0,
                            Murderers = 0,
                            Scientists = 0,
                            Stags = 0,
                            Wolfs = 0,
                            Zombies = 0
                        },
                        Enabled = false
                    },
                    Lighthouse = new ConfigData.Zones.MonumentSettings
                    {
                        Counts = new ConfigData.Zones.MonumentSettings.AnimalCounts
                        {
                            Bears = 0,
                            Boars = 0,
                            Chickens = 0,
                            Horses = 0,
                            Murderers = 0,
                            Scientists = 0,
                            Stags = 0,
                            Wolfs = 0,
                            Zombies = 0
                        },
                        Enabled = false
                    },
                    Powerplant = new ConfigData.Zones.MonumentSettings
                    {
                        Counts = new ConfigData.Zones.MonumentSettings.AnimalCounts
                        {
                            Bears = 0,
                            Boars = 0,
                            Chickens = 0,
                            Horses = 0,
                            Murderers = 0,
                            Scientists = 0,
                            Stags = 0,
                            Wolfs = 0,
                            Zombies = 0
                        },
                        Enabled = false
                    },
                    Quarry_HQM = new ConfigData.Zones.MonumentSettings
                    {
                        Counts = new ConfigData.Zones.MonumentSettings.AnimalCounts
                        {
                            Bears = 0,
                            Boars = 0,
                            Chickens = 0,
                            Horses = 0,
                            Murderers = 0,
                            Scientists = 0,
                            Stags = 0,
                            Wolfs = 0,
                            Zombies = 0
                        },
                        Enabled = false
                    },
                    Quarry_Stone = new ConfigData.Zones.MonumentSettings
                    {
                        Counts = new ConfigData.Zones.MonumentSettings.AnimalCounts
                        {
                            Bears = 0,
                            Boars = 0,
                            Chickens = 0,
                            Horses = 0,
                            Murderers = 0,
                            Scientists = 0,
                            Stags = 0,
                            Wolfs = 0,
                            Zombies = 0
                        },
                        Enabled = false
                    },
                    Quarry_Sulfur = new ConfigData.Zones.MonumentSettings
                    {
                        Counts = new ConfigData.Zones.MonumentSettings.AnimalCounts
                        {
                            Bears = 0,
                            Boars = 0,
                            Chickens = 0,
                            Horses = 0,
                            Murderers = 0,
                            Scientists = 0,
                            Stags = 0,
                            Wolfs = 0,
                            Zombies = 0
                        },
                        Enabled = false
                    },
                    Radtown = new ConfigData.Zones.MonumentSettings
                    {
                        Counts = new ConfigData.Zones.MonumentSettings.AnimalCounts
                        {
                            Bears = 0,
                            Boars = 0,
                            Chickens = 0,
                            Horses = 0,
                            Murderers = 0,
                            Scientists = 0,
                            Stags = 0,
                            Wolfs = 0,
                            Zombies = 0
                        },
                        Enabled = false
                    },
                    RocketFactory = new ConfigData.Zones.MonumentSettings
                    {
                        Counts = new ConfigData.Zones.MonumentSettings.AnimalCounts
                        {
                            Bears = 0,
                            Boars = 0,
                            Chickens = 0,
                            Horses = 0,
                            Murderers = 0,
                            Scientists = 0,
                            Stags = 0,
                            Wolfs = 0,
                            Zombies = 0
                        },
                        Enabled = false
                    },
                    Satellite = new ConfigData.Zones.MonumentSettings
                    {
                        Counts = new ConfigData.Zones.MonumentSettings.AnimalCounts
                        {
                            Bears = 0,
                            Boars = 0,
                            Chickens = 0,
                            Horses = 0,
                            Murderers = 0,
                            Scientists = 0,
                            Stags = 0,
                            Wolfs = 0,
                            Zombies = 0
                        },
                        Enabled = false
                    },
                    SmallHarbor = new ConfigData.Zones.MonumentSettings
                    {
                        Counts = new ConfigData.Zones.MonumentSettings.AnimalCounts
                        {
                            Bears = 0,
                            Boars = 0,
                            Chickens = 0,
                            Horses = 0,
                            Murderers = 0,
                            Scientists = 0,
                            Stags = 0,
                            Wolfs = 0,
                            Zombies = 0
                        },
                        Enabled = false
                    },
                    Supermarket = new ConfigData.Zones.MonumentSettings
                    {
                        Counts = new ConfigData.Zones.MonumentSettings.AnimalCounts
                        {
                            Bears = 0,
                            Boars = 0,
                            Chickens = 0,
                            Horses = 0,
                            Murderers = 0,
                            Scientists = 0,
                            Stags = 0,
                            Wolfs = 0,
                            Zombies = 0
                        },
                        Enabled = false
                    },
                    Trainyard = new ConfigData.Zones.MonumentSettings
                    {
                        Counts = new ConfigData.Zones.MonumentSettings.AnimalCounts
                        {
                            Bears = 0,
                            Boars = 0,
                            Chickens = 0,
                            Horses = 0,
                            Murderers = 0,
                            Scientists = 0,
                            Stags = 0,
                            Wolfs = 0,
                            Zombies = 0
                        },
                        Enabled = false
                    },
                    Tunnels = new ConfigData.Zones.MonumentSettings
                    {
                        Counts = new ConfigData.Zones.MonumentSettings.AnimalCounts
                        {
                            Bears = 0,
                            Boars = 0,
                            Chickens = 0,
                            Horses = 0,
                            Murderers = 0,
                            Scientists = 0,
                            Stags = 0,
                            Wolfs = 0,
                            Zombies = 0
                        },
                        Enabled = false
                    },
                    Warehouse = new ConfigData.Zones.MonumentSettings
                    {
                        Counts = new ConfigData.Zones.MonumentSettings.AnimalCounts
                        {
                            Bears = 0,
                            Boars = 0,
                            Chickens = 0,
                            Horses = 0,
                            Murderers = 0,
                            Scientists = 0,
                            Stags = 0,
                            Wolfs = 0,
                            Zombies = 0
                        },
                        Enabled = false
                    },
                    WaterTreatment = new ConfigData.Zones.MonumentSettings
                    {
                        Counts = new ConfigData.Zones.MonumentSettings.AnimalCounts
                        {
                            Bears = 0,
                            Boars = 0,
                            Chickens = 0,
                            Horses = 0,
                            Murderers = 0,
                            Scientists = 0,
                            Stags = 0,
                            Wolfs = 0,
                            Zombies = 0
                        },
                        Enabled = false
                    }
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new Core.VersionNumber(0, 2, 80))
            {
                configData.Zone.Junkyard = baseConfig.Zone.Junkyard;
                configData.Zone.Quarry_HQM = baseConfig.Zone.Quarry_HQM;
                configData.Zone.Quarry_Stone = baseConfig.Zone.Quarry_Stone;
                configData.Zone.Quarry_Sulfur = baseConfig.Zone.Quarry_Sulfur;
            }

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion      

        #region Messaging
        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"nullList", "<color=#939393>Error getting a list of monuments</color>" },
            {"title", "<color=orange>Radtown Animals:</color> " },
            {"killedAll", "<color=#939393>Killed all animals</color>" },
            {"spawnLimit", "The animal spawn limit has been hit." }
        };
        #endregion
    }
}