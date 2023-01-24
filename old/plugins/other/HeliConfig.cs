using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Network;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("HeliConfig", "Kappasaurus", "1.0.0")]

    class HeliConfig : RustPlugin
    {
        private int timeBetweenSpawns = 2;
        private bool heliEnabled = false;

        HashSet<BaseEntity> allowedEntities { get; set; } = new HashSet<BaseEntity>();

        #region Init
        void Init()
        { 
			LoadConfig();
			
            timer.Every(timeBetweenSpawns * 3600, () =>
            {
				if(heliEnabled)
					SpawnHeli();
            });
        }
        #endregion

        #region Config
        private new void LoadConfig()
        {
            GetConfig(ref timeBetweenSpawns, "Time between spawns (hours)");
            GetConfig(ref heliEnabled, "Helicopter enabled");

            SaveConfig();
        }
        #endregion
 
        #region Hooks
        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity.GetType() == typeof(BaseHelicopter))
            {
                if (!allowedEntities.Contains((BaseEntity)entity) && heliEnabled == false)
                {
                    entity.Kill();
                }
            }
        }
        #endregion

        #region Helpers
        protected override void LoadDefaultConfig() => PrintWarning("Generating new configuration file...");
		
		private void GetConfig<T>(ref T variable, params string[] path)
        {
            if (path.Length == 0)
                return;

            if (Config.Get(path) == null)
            {
                Config.Set(path.Concat(new object[] { variable }).ToArray());
                PrintWarning($"Added field to config: {string.Join("/", path)}");
            }

            variable = (T)Convert.ChangeType(Config.Get(path), typeof(T));
        }

        public void SpawnHeli()
        {
            float rand = UnityEngine.Random.Range(0f, 360f);
            Vector3 pos = new Vector3(Mathf.Sin(rand) * ConVar.Server.worldsize, 100f, Mathf.Cos(rand) * ConVar.Server.worldsize);
            BaseEntity baseEntity = GameManager.server.CreateEntity("Assets/Prefabs/NPC/Patrol Helicopter/PatrolHelicopter.prefab", pos, default(Quaternion), true);
            allowedEntities.Add(baseEntity);
            if (baseEntity)
            {
                baseEntity.Spawn();
            }
        }
        #endregion
    }
}