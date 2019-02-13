using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Rust;
using UnityEngine;
using Random = System.Random;

namespace Oxide.Plugins
{
    [Info("Power Spawn", "Iv Misticos", "1.0.3")]
    [Description("Control players' spawning")]
    class PowerSpawn : RustPlugin
    {
        #region Variables

        private int _worldSize;

        private readonly int _layerTerrain = LayerMask.NameToLayer("Terrain");

        private readonly Random _random = new Random();
        
        #endregion
        
        #region Configuration

        private static Configuration _config;
        
        private class Configuration
        {
            [JsonProperty(PropertyName = "Minimal Distance To Building")]
            public int DistanceBuilding = 10;

            [JsonProperty(PropertyName = "Minimal Distance To Collider")]
            public int DistanceCollider = 10;

            [JsonProperty(PropertyName = "Maximum Number Of Attempts To Find A Location")]
            public int AttemptsMax = 200;

            [JsonProperty(PropertyName = "Debug")]
            public bool Debug = false;
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

        private void OnServerInitialized()
        {
            _worldSize = ConVar.Server.worldsize;
        }

        private object OnPlayerRespawn(BasePlayer player)
        {
            var position = FindPosition();
            if (!position.HasValue)
            {
                PrintDebug($"Haven't found a position for {player.displayName}");
                return null;
            }
            
            PrintDebug($"Found position for {player.displayName}: {position}");
            
            return new BasePlayer.SpawnPoint
            {
                pos = position.Value
            };
        }

        #endregion
        
        #region Helpers

        private Vector3? FindPosition()
        {
            for (var i = 0; i < _config.AttemptsMax; i++)
            {
                var position = TryFindPosition();
                if (position.HasValue)
                    return position;
            }

            return null;
        }

        private Vector3? TryFindPosition()
        {
            var position = new Vector3(GetRandomPosition(), 0, GetRandomPosition());
            var height = TerrainMeta.HeightMap.GetHeight(position);
            if (height > 0)
                position.y = height;
            else
                return null;

            return CheckBadBuilding(position) || CheckBadCollider(position) ? (Vector3?) null : position;
        }

        private int GetRandomPosition() => _random.Next(_worldSize / -2, _worldSize / 2);

        private void PrintDebug(string message)
        {
            if (_config.Debug)
                Interface.Oxide.LogDebug($"{Name} > " + message);
        }

        private bool CheckBadBuilding(Vector3 position)
        {
            var buildings = new List<BuildingBlock>();
            Vis.Entities(position, _config.DistanceBuilding, buildings, Layers.Construction);
            return buildings.Count > 0;
        }

        private bool CheckBadCollider(Vector3 position)
        {
            var colliders = new List<Collider>();
            Vis.Components(position, _config.DistanceCollider, colliders);
            foreach (var collider in colliders)
            {
                var gameObject = collider.gameObject;
                if (gameObject.layer == _layerTerrain)
                    continue;
                
                return true;
            }
            
            return false;
        }

        #endregion
    }
}