using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Explosive Barrels", "Orange", "1.0.1")]
    [Description("Allows barrels to blow up on damage or death")]
    public class ExplosiveBarrels : RustPlugin
    {
        #region Vars
        
        private List<string> barrels = new List<string>
        {
            "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab",
            "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab",
            "assets/bundled/prefabs/radtown/loot_barrel_1.prefab",
            "assets/bundled/prefabs/radtown/loot_barrel_2.prefab",
            "assets/bundled/prefabs/radtown/oil_barrel.prefab",
        };
        
        #endregion

        #region Oxide Hooks

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            CheckBarrel(entity, false);
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            CheckBarrel(entity, true);
        }

        #endregion

        #region Core

        private void CheckBarrel(BaseCombatEntity entity, bool death)
        {
            if (entity == null) {return;}
            if (!IsBarrel(entity)) {return;}
            var cfg = IsOilBarrel(entity) ? config.oil : config.normal;
            var random = Core.Random.Range(0, 99);
            var chance = death ? cfg.death : cfg.damage;
            if (chance == 0 || random >  chance) {return;}
            Explode(entity, cfg);
        }

        private void Explode(BaseCombatEntity barrel, ConfigData.OBarrel config)
        {
            var position = barrel.transform.position;
            var radius = config.eRadius;
            var damage = config.eDamage;
            var effects = config.effects;

            if (barrel.health > 0)
            {
                try
                {
                    barrel.Hurt(100f);
                }
                catch
                {
                    // ignored
                }
            }

            foreach (var effect in effects)
            {
                var x = Core.Random.Range(-radius, radius);
                var y = Core.Random.Range(0, radius);
                var z = Core.Random.Range(-radius, radius);
                Effect.server.Run(effect, position + new Vector3(x,y,z));
            }
            
            Effect.server.Run("assets/content/weapons/_gestures/effects/eat_1hand_celery.prefab", position);

            var entities = new List<BaseCombatEntity>();
            Vis.Entities(position, radius, entities);
            
            foreach (var entity in entities.ToList())
            {
                try
                {
                    if (entity.health > 0)
                    {
                        entity.Hurt(damage, DamageType.Explosion);
                    }
                }
                catch
                {
                    // ignored
                }
            }
        }

        #endregion

        #region Helpers

        private bool IsBarrel(BaseEntity entity)
        {
            return entity.OwnerID == 0 && barrels.Contains(entity.PrefabName);
        }

        private bool IsOilBarrel(BaseEntity entity)
        {
            return entity.PrefabName.Contains("oil");
        }

        #endregion

        #region Config

        private static ConfigData config;
        
        private class ConfigData
        {    
            [JsonProperty(PropertyName = "1. NORMAL barrels settings:")]
            public OBarrel normal;
            
            [JsonProperty(PropertyName = "2. OIL barrels settings:")]
            public OBarrel oil;

            public class OBarrel
            {
                [JsonProperty(PropertyName = "1. Chance to explode on taking damage (set to 0 to disable)")]
                public int damage;
                
                [JsonProperty(PropertyName = "2. Chance to explode on death (set to 0 to disable)")]
                public int death;
                
                [JsonProperty(PropertyName = "3. Damage on explosion (set to 0 to disable)")]
                public int eDamage;
                
                [JsonProperty(PropertyName = "4. Radius of damage on explosion")]
                public int eRadius;
                
                [JsonProperty(PropertyName = "5. List of effects on explosion")]
                public List<string> effects;
            }
        }
        
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                normal = new ConfigData.OBarrel
                {
                    damage = 0,
                    death = 10,
                    eDamage = 0,
                    eRadius = 5,
                    effects = new List<string>
                    {
                        "assets/bundled/prefabs/fx/gas_explosion_small.prefab",
                        "assets/bundled/prefabs/fx/explosions/explosion_03.prefab"
                    }
                },
                oil = new ConfigData.OBarrel
                {
                    damage = 10,
                    death = 75,
                    eDamage = 50,
                    eRadius = 3,
                    effects = new List<string>
                    {
                        "assets/bundled/prefabs/fx/gas_explosion_small.prefab",
                        "assets/bundled/prefabs/fx/explosions/explosion_03.prefab"
                    }
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
    }
}