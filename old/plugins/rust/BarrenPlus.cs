using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;
using Random = System.Random;

namespace Oxide.Plugins
{
    [Info("Barren Plus", "Iv Misticos", "1.0.5")]
    [Description("Let JunkPiles and DiveSites be alive on Barren!")]
    class BarrenPlus : RustPlugin
    {
        #region Variables

        private int _size = 0;

        private readonly string[] PrefabsJunkPile = { "assets/prefabs/misc/junkpile_water/junkpile_water_a.prefab", "assets/prefabs/misc/junkpile_water/junkpile_water_b.prefab", "assets/prefabs/misc/junkpile_water/junkpile_water_c.prefab" };
        private readonly string[] PrefabsDiveSite = { "assets/prefabs/misc/divesite/divesite_a.prefab", "assets/prefabs/misc/divesite/divesite_b.prefab", "assets/prefabs/misc/divesite/divesite_c.prefab" };

        private static readonly Random Random = new Random();
        
        #endregion
        
        #region Configuration
        
        private static Configuration _config = new Configuration();

        public class Configuration
        {
            [JsonProperty(PropertyName = "Junk Pile Spawn Frequency")]
            public float JunkPileTime = 30;

            [JsonProperty(PropertyName = "Dive Site Spawn Frequency")]
            public float DiveSiteTime = 45;
            
            [JsonProperty(PropertyName = "Minimal Distance Between Junk Piles")]
            public int JunkPileRange = 50;

            [JsonProperty(PropertyName = "Minimal Distance Between Dive Sites")]
            public int DiveSiteRange = 50;

            [JsonProperty(PropertyName = "Minimal Distance Between Water And Terrain")]
            public int BetweenWaterTerrain = 20;

            [JsonProperty(PropertyName = "Maximal Dive Site Angle")]
            public int AngleMax = 20;

            [JsonProperty(PropertyName = "Dive Sites' Lifetime")]
            public int DiveSiteLife = 600;

            [JsonProperty(PropertyName = "Junk Piles' Lifetime")]
            public int JunkPileLife = 300;

            [JsonProperty(PropertyName = "Maximum Number Of Attempts To Find A Location")]
            public int LocAttemptsMax = 500;
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

        // ReSharper disable once UnusedMember.Local
        private void OnServerInitialized()
        {
            LoadConfig();
            _size = ConVar.Server.worldsize;
            
            timer.Every(_config.DiveSiteTime, () =>
            {
                for (var i = 1; i < _config.LocAttemptsMax; i++)
                {
                    if (SpawnDiveSite())
                        break;
                }
            });
            timer.Every(_config.JunkPileTime, () =>
            {
                for (var i = 1; i < _config.LocAttemptsMax; i++)
                {
                    if (SpawnJunkPileWater())
                        break;
                }
            });
        }
        
        #endregion
        
        #region Helpers

        private Vector3? GetPosition(bool onWater)
        {
            var x = Random.Next(-_size / 2, _size / 2);
            var z = Random.Next(-_size / 2, _size / 2);
            var v = new Vector3(x, 0, z);
            
            var terrain = (int) TerrainMeta.HeightMap.GetHeight(v);
            if (_config.BetweenWaterTerrain > -terrain)
                return null;

            v.y = onWater ? 0 : terrain;
            return v;
        }

        private Quaternion GetRotation(bool isJunkPile)
        {
            return Quaternion.Euler(isJunkPile ? 0 : Random.Next(-_config.AngleMax, _config.AngleMax),
                Random.Next(0, 360), isJunkPile ? 0 : Random.Next(-_config.AngleMax, _config.AngleMax));
        }

        private bool SpawnDiveSite()
        {
            var position = GetPosition(false);
            if (position == null)
                return false;

            var ds = new List<DiveSite>();
            Vis.Entities(position.Value, _config.DiveSiteRange, ds);
            if (ds.Count > 0)
                return false;

            var entity =
                GameManager.server.CreateEntity(PrefabsDiveSite[Random.Next(0, PrefabsDiveSite.Length - 1)],
                    position.Value, GetRotation(false)) as DiveSite;
            if (entity == null) return true;
            
            entity.gameObject.AddComponent<DiveSiteController>();
            entity.Spawn();
            return true;
        }

        private bool SpawnJunkPileWater()
        {
            var position = GetPosition(true);
            if (position == null)
                return false;

            var jp = new List<JunkPileWater>();
            Vis.Entities(position.Value, _config.JunkPileRange, jp);
            if (jp.Count > 0)
                return false;

            var entity = GameManager.server.CreateEntity(PrefabsJunkPile[Random.Next(0, PrefabsJunkPile.Length - 1)],
                position.Value, GetRotation(true)) as JunkPileWater;
            if (entity == null) return true;
            
            entity.gameObject.AddComponent<JunkPileWaterController>();
            entity.Spawn();
            return true;
        }

        #endregion
        
        #region Controllers

        public class DiveSiteController : MonoBehaviour
        {
            private float LastTime = Time.realtimeSinceStartup;
            private DiveSite e;
            private bool isDestroed = false;

            private void Awake()
            {
                e = gameObject.GetComponent<DiveSite>();
            }
            
            private void FixedUpdate()
            {
                if (isDestroed)
                    return;

                var rt = Time.realtimeSinceStartup;
                if (LastTime + _config.DiveSiteLife > rt)
                    return;

                isDestroed = true;
                e.SinkAndDestroy();
            }
        }

        public class JunkPileWaterController : MonoBehaviour
        {
            private float LastTime = Time.realtimeSinceStartup;
            private JunkPileWater e;
            private bool isDestroed = false;

            private void Awake()
            {
                e = gameObject.GetComponent<JunkPileWater>();
            }
            
            private void FixedUpdate()
            {
                if (isDestroed)
                    return;
                
                var rt = Time.realtimeSinceStartup;
                if (LastTime + _config.JunkPileLife > rt)
                    return;

                isDestroed = true;
                e.SinkAndDestroy();
            }
        }
        
        #endregion
    }
}