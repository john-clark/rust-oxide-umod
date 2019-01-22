using System;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Chinook Drop Randomizer", "shinnova", "1.2.5")]
    [Description("Make the chinook drop location more random")]
    public class ChinookDropRandomizer : RustPlugin
    {
        #region Configuration
        Configuration config;
        public class Configuration
        {
            [JsonProperty(PropertyName = "Don't drop above water")]
            public bool checkWater { get; set; } = false;
            [JsonProperty(PropertyName = "Minimum time until drop (in seconds)")]
            public int minTime { get; set; } = 30;
            [JsonProperty(PropertyName = "Maximum time until drop (in seconds)")]
            public int maxTime { get; set; } = 300;
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

        System.Random rnd = new System.Random();

        void OnEntitySpawned(BaseEntity entity)
        {
            if (entity is CH47HelicopterAIController)
            {
                CH47HelicopterAIController chinook = entity as CH47HelicopterAIController;
                TryDropCrate(chinook);
            }
        }

        bool AboveWater(Vector3 location)
        {
            Vector3 downwards = new Vector3(0, -1, 0);
            var ray = new Ray(location, downwards);
            RaycastHit hit;
            Physics.Raycast(ray, out hit, 5000);
            float ChinookToObject = hit.distance;
            if (location.y - ChinookToObject <= TerrainMeta.WaterMap.GetHeight(location))
                return true;
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
                            chinook.DropCrate();
                        else
                            TryDropCrate(chinook);
                    }
                }
            });
        }
    }
}