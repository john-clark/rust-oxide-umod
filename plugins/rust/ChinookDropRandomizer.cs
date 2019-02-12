using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Chinook Drop Randomizer", "shinnova", "1.2.8")]
    [Description("Make the chinook drop location more random")]
    public class ChinookDropRandomizer : RustPlugin
    {
        #region Configuration
        Configuration config;
        public class Configuration
        {
            [JsonProperty(PropertyName = "Minimum time until drop (in seconds)")]
            public int minTime { get; set; } = 30;
            [JsonProperty(PropertyName = "Maximum time until drop (in seconds)")]
            public int maxTime { get; set; } = 300;
            [JsonProperty(PropertyName = "Don't drop above water")]
            public bool checkWater { get; set; } = false;
            [JsonProperty(PropertyName = "Don't drop above monuments")]
            public bool checkMonument { get; set; } = false;
            [JsonProperty(PropertyName = "What monuments to check (only works if monument checking is enabled)")]
            public Dictionary<string, bool> monumentsToCheck { get; set; } = new Dictionary<string, bool>
            {
                ["AbandonedCabins"] = true,
                ["Airfield"] = true,
                ["BanditCamp"] = true,
                ["Compound"] = true,
                ["Compound1"] = true,
                ["Compound2"] = true,
                ["Dome"] = true,
                ["GasStation"] = true,
                ["GasStation1"] = true,
                ["Harbor1"] = true,
                ["Harbor2"] = true,
                ["Junkyard"] = true,
                ["Launchsite"] = true,
                ["Lighthouse"] = true,
                ["Lighthouse1"] = true,
                ["Lighthouse2"] = true,
                ["MilitaryTunnel"] = true,
                ["MiningOutpost"] = true,
                ["MiningOutpost1"] = true,
                ["MiningOutpost2"] = true,
                ["PowerPlant"] = true,
                ["QuarryHQM"] = true,
                ["QuarryStone"] = true,
                ["QuarrySulfur"] = true,
                ["Satellite"] = true,
                ["SewerBranch"] = true,
                ["SuperMarket"] = true,
                ["SuperMarket1"] = true,
                ["Trainyard"] = true,
                ["WaterTreatment"] = true,
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                    LoadDefaultConfig();
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Generating new configuration file");
            config = new Configuration();
        }

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion 

        Dictionary<string, float> monumentSizes = new Dictionary<string, float>
        {
            ["AbandonedCabins"] = 50,
            ["Airfield"] = 200,
            ["BanditCamp"] = 80,
            ["Compound"] = 115,
            ["Compound1"] = 115,
            ["Compound2"] = 115,
            ["Dome"] = 65,
            ["GasStation"] = 40,
            ["GasStation1"] = 40,
            ["Harbor1"] = 125,
            ["Harbor2"] = 125,
            ["Junkyard"] = 150,
            ["Launchsite"] = 265,
            ["Lighthouse"] = 40,
            ["Lighthouse1"] = 40,
            ["Lighthouse2"] = 40,
            ["MilitaryTunnel"] = 120,
            ["MiningOutpost"] = 40,
            ["MiningOutpost1"] = 40,
            ["MiningOutpost2"] = 40,
            ["PowerPlant"] = 150,
            ["QuarryHQM"] = 30,
            ["QuarryStone"] = 30,
            ["QuarrySulfur"] = 30,
            ["Satellite"] = 95,
            ["SewerBranch"] = 80,
            ["SuperMarket"] = 30,
            ["SuperMarket1"] = 30,
            ["Trainyard"] = 130,
            ["WaterTreatment"] = 190,
        };
        Dictionary<string, Vector3> monumentPosition = new Dictionary<string, Vector3>();
        System.Random rnd = new System.Random();

        void OnServerInitialized()
        {
            if (config.checkMonument)
                FindMonuments();
        }

        void OnEntitySpawned(BaseEntity entity)
        {
            if (entity is CH47HelicopterAIController)
            {
                CH47HelicopterAIController chinook = entity as CH47HelicopterAIController;
                TryDropCrate(chinook);
            }
        }

        void FindMonuments()
        {
            // Based on BotSpawn's Monument Finder function
            var allGameObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();

            int miningoutpost = 0;
            int lighthouse = 0;
            int gasstation = 0;
            int supermarket = 0;
            int compound = 0;

            foreach (var gameObject in allGameObjects)
            {
                var pos = gameObject.transform.position;
                if (gameObject.name.Contains("autospawn/monument") && pos != new Vector3(0, 0, 0))
                {
                    if (gameObject.name.Contains("swamp_c"))
                    {
                        monumentPosition.Add("AbandonedCabins", pos);
                        continue;
                    }
                    if (gameObject.name.Contains("airfield_1"))
                    {
                        monumentPosition.Add("Airfield", pos);
                        continue;
                    }
                    if (gameObject.name.Contains("bandit_town"))
                    {
                        monumentPosition.Add("BanditCamp", pos);
                        continue;
                    }
                    if (gameObject.name.Contains("compound") && compound == 0)
                    {
                        monumentPosition.Add("Compound", pos);
                        compound++;
                        continue;
                    }
                    if (gameObject.name.Contains("compound") && compound == 1)
                    {
                        monumentPosition.Add("Compound1", pos);
                        compound++;
                        continue;
                    }
                    if (gameObject.name.Contains("compound") && compound == 2)
                    {
                        monumentPosition.Add("Compound2", pos);
                        compound++;
                        continue;
                    }
                    if (gameObject.name.Contains("sphere_tank"))
                    {
                        monumentPosition.Add("Dome", pos);
                        continue;
                    }
                    if (gameObject.name.Contains("gas_station_1") && gasstation == 0)
                    {
                        monumentPosition.Add("GasStation", pos);
                        gasstation++;
                        continue;
                    }
                    if (gameObject.name.Contains("gas_station_1") && gasstation == 1)
                    {
                        monumentPosition.Add("GasStation1", pos);
                        gasstation++;
                        continue;
                    }
                    if (gameObject.name.Contains("harbor_1"))
                    {
                        monumentPosition.Add("Harbor1", pos);
                        continue;
                    }

                    if (gameObject.name.Contains("harbor_2"))
                    {
                        monumentPosition.Add("Harbor2", pos);
                        continue;
                    }
                    if (gameObject.name.Contains("junkyard"))
                    {
                        monumentPosition.Add("Junkyard", pos);
                        continue;
                    }
                    if (gameObject.name.Contains("launch_site"))
                    {
                        monumentPosition.Add("Launchsite", pos);
                        continue;
                    }
                    if (gameObject.name.Contains("lighthouse") && lighthouse == 0)
                    {
                        monumentPosition.Add("Lighthouse", pos);
                        lighthouse++;
                        continue;
                    }

                    if (gameObject.name.Contains("lighthouse") && lighthouse == 1)
                    {
                        monumentPosition.Add("Lighthouse1", pos);
                        lighthouse++;
                        continue;
                    }

                    if (gameObject.name.Contains("lighthouse") && lighthouse == 2)
                    {
                        monumentPosition.Add("Lighthouse2", pos);
                        lighthouse++;
                        continue;
                    }

                    if (gameObject.name.Contains("military_tunnel_1"))
                    {
                        monumentPosition.Add("MilitaryTunnel", pos);
                        continue;
                    }
                    if (gameObject.name.Contains("powerplant_1"))
                    {
                        monumentPosition.Add("PowerPlant", pos);
                        continue;
                    }
                    if (gameObject.name.Contains("mining_quarry_c"))
                    {
                        monumentPosition.Add("QuarryHQM", pos);
                        continue;
                    }
                    if (gameObject.name.Contains("mining_quarry_b"))
                    {
                        monumentPosition.Add("QuarryStone", pos);
                        continue;
                    }
                    if (gameObject.name.Contains("mining_quarry_a"))
                    {
                        monumentPosition.Add("QuarrySulfur", pos);
                        continue;
                    }
                    if (gameObject.name.Contains("radtown_small_3"))
                    {
                        monumentPosition.Add("SewerBranch", pos);
                        continue;
                    }
                    if (gameObject.name.Contains("satellite_dish"))
                    {
                        monumentPosition.Add("Satellite", pos);
                        continue;
                    }
                    if (gameObject.name.Contains("supermarket_1") && supermarket == 0)
                    {
                        monumentPosition.Add("SuperMarket", pos);
                        supermarket++;
                        continue;
                    }

                    if (gameObject.name.Contains("supermarket_1") && supermarket == 1)
                    {
                        monumentPosition.Add("SuperMarket1", pos);
                        supermarket++;
                        continue;
                    }
                    if (gameObject.name.Contains("trainyard_1"))
                    {
                        monumentPosition.Add("Trainyard", pos);
                        continue;
                    }
                    if (gameObject.name.Contains("warehouse") && miningoutpost == 0)
                    {
                        monumentPosition.Add("MiningOutpost", pos);
                        miningoutpost++;
                        continue;
                    }

                    if (gameObject.name.Contains("warehouse") && miningoutpost == 1)
                    {
                        monumentPosition.Add("MiningOutpost1", pos);
                        miningoutpost++;
                        continue;
                    }

                    if (gameObject.name.Contains("warehouse") && miningoutpost == 2)
                    {
                        monumentPosition.Add("MiningOutpost2", pos);
                        miningoutpost++;
                        continue;
                    }
                    if (gameObject.name.Contains("water_treatment_plant_1"))
                    {
                        monumentPosition.Add("WaterTreatment", pos);
                        continue;
                    }
                }
            }
        }

        RaycastHit CheckDown(Vector3 location)
        {
            var ray = new Ray(location, Vector3.down);
            RaycastHit hit;
            Physics.Raycast(ray, out hit, 5000);
            return hit;
        }

        bool AboveWater(Vector3 location)
        {
            RaycastHit check = CheckDown(location);
            float ChinookToObject = check.distance;
            if (location.y - ChinookToObject <= TerrainMeta.WaterMap.GetHeight(location))
                return true;
            return false;
        }

        bool AboveMonument(Vector3 location)
        {
            RaycastHit check = CheckDown(location);
            Vector3 collision = check.point;
            foreach (KeyValuePair<string, Vector3> entry in monumentPosition)
            {
                var monumentName = entry.Key;
                if (config.monumentsToCheck[monumentName])
                {
                    var monumentVector = entry.Value;
                    float realdistance = monumentSizes[monumentName];
                    monumentVector.y = collision.y;
                    float dist = Vector3.Distance(collision, monumentVector);
                    if (dist < realdistance)
                        return true;
                }
            }
            return false;
        }

        void TryDropCrate(CH47HelicopterAIController chinook)
        {
            int randomtime = rnd.Next(config.minTime, config.maxTime);
            timer.Once(randomtime,() => {
                if (!chinook.IsDead())
                {
                    if (chinook.CanDropCrate())
                    {
                        if ((config.checkWater && !AboveWater(chinook.transform.position)) || !config.checkWater)
                        {
                            if ((config.checkMonument && !AboveMonument(chinook.transform.position)) || !config.checkMonument)
                                chinook.DropCrate();
                        }
                        else
                            TryDropCrate(chinook);
                    }
                }
            });
        }
    }
}