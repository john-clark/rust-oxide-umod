using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Animal Home", "Orange", "1.0.2")]
    [Description("Adding to animals home points so they can't run away")]
    public class AnimalHome : RustPlugin
    {
        #region Oxide Hooks
        
        private void OnServerInitialized()
        {
            OnStart();
        }

        private void Unload()
        {
            OnEnd();
        }
        
        private void OnEntitySpawned(BaseNetworkable entity)
        {
            AddLogic(entity);
        }

        #endregion

        #region Helpers

        private void OnStart()
        {       
            foreach (var entity in UnityEngine.Object.FindObjectsOfType<BaseEntity>())
            {
                AddLogic(entity);
            }
        }

        private void OnEnd()
        {
            foreach (var logic in UnityEngine.Object.FindObjectsOfType<OLogic>().ToList())
            {
                logic.DoDestroy();
            }
        }

        private void AddLogic(BaseNetworkable entity)
        {
            var npc = entity.GetComponent<BaseNpc>();
            if (npc == null) {return;}
            if (config.blocked.Contains(entity.ShortPrefabName)) {return;}
            if (entity.GetComponent<OLogic>() != null) {return;}
            entity.gameObject.AddComponent<OLogic>();
        }

        #endregion

        #region Config

        private static ConfigData config;
        
        private class ConfigData
        {    
            [JsonProperty(PropertyName = "Time between checks")]
            public float timer;
            
            [JsonProperty(PropertyName = "Max distance between Home and NPC")]
            public float distance;
            
            [JsonProperty(PropertyName = "Blocked NPC types (logic will not work for them)")]
            public List<string> blocked;
        }
        
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                timer = 30f,
                distance = 50f,
                blocked = new List<string>
                {
                    "example.name",
                    "example.name",
                    "example.name"
                }
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

        #region Scripts

        private class OLogic: MonoBehaviour
        {
            private Vector3 home;
            private BaseNpc npc;
            private float distance;
            private float time;

            private void Awake()
            {
                npc = GetComponent<BaseNpc>();
                home = npc.transform.position;
                distance = config.distance;
                time = config.timer;
                
                InvokeRepeating("CheckDistance", 1f, time);
            }

            private void CheckDistance()
            {
                if (Vector3.Distance(home, npc.transform.position) > distance)
                {
                    npc.UpdateDestination(home);
                }
            }

            public void DoDestroy()
            {
                Destroy(this);
            }

            private void OnDestroy()
            {
                CancelInvoke("CheckDistance");
            }
        }

        #endregion
    }
}