using System;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;
using Random = System.Random;

namespace Oxide.Plugins
{
    [Info("Plant Drop", "Iv Misticos", "1.2.0")]
    [Description("Allows planting crops anywhere by dropping the seed")]
    class PlantDrop : RustPlugin
    {
        #region Variables
        
        private const string PrefabCorn = "assets/prefabs/plants/corn/corn.entity.prefab";
        private const string PrefabHemp = "assets/prefabs/plants/hemp/hemp.entity.prefab";
        private const string PrefabPumpkin = "assets/prefabs/plants/pumpkin/pumpkin.entity.prefab";
        
        private readonly Random _random = new Random();
        
        #endregion
        
        #region Configuration

        private Configuration _config;
        
        private class Configuration
        {
            [JsonProperty(PropertyName = "Randomize Position")]
            public bool RandomizePosition = false;
            
            [JsonProperty(PropertyName = "Randomize Position On N")]
            public float RandomizePositionOn = 2f;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
            }
            catch
            {
                Config.WriteObject(_config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}.jsonError");
                PrintError("The configuration file contains an error and has been replaced with a default config.\n" +
                           "The error configuration file was saved in the .jsonError extension");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion
        
        #region Hooks
        
        private void OnItemDropped(Item item, BaseNetworkable entity)
        {
            var shortname = item.info.shortname;
            var pos = entity.transform.position;

            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (shortname)
            {
                case "seed.corn":
                    CreatePlant(entity, PrefabCorn, pos, item.amount);
                    break;

                case "seed.hemp":
                    CreatePlant(entity, PrefabHemp, pos, item.amount);
                    break;

                case "seed.pumpkin":
                    CreatePlant(entity, PrefabPumpkin, pos, item.amount);
                    break;
            }
        }
        
        #endregion
        
        #region Helpers
        
        private void CreatePlant(BaseNetworkable seed, string prefab, Vector3 pos, int amount)
        {
            RaycastHit hit;
            Physics.Raycast(pos, Vector3.down, out hit);
            if (hit.GetEntity() != null)
                return;

            pos.y = TerrainMeta.HeightMap.GetHeight(seed.transform.position);
            seed.Kill();

            for (var i = 0; i < amount; i++)
            {
                if (_config.RandomizePosition)
                {
                    pos.x += UnityEngine.Random.Range(-_config.RandomizePositionOn, _config.RandomizePositionOn);
                    pos.z += UnityEngine.Random.Range(-_config.RandomizePositionOn, _config.RandomizePositionOn);
                    pos.y = TerrainMeta.HeightMap.GetHeight(pos);
                }
                
                var plant = GameManager.server.CreateEntity(prefab, pos, Quaternion.identity) as PlantEntity;
                if (plant == null) continue;

                plant.Spawn();
            }
        }
        
        #endregion
    }
}