using System;
using System.Linq;
using Rust;

namespace Oxide.Plugins
{
    [Info("External Wall Decay Protection", "Orange", "1.0.0")]
    [Description("Adding protection from decay to walls")]
    public class ExternalWallDecayProtection : RustPlugin
    {
        #region Config
        
        private float protection1;
        private float protection2;

        protected override void LoadDefaultConfig()
        {
            Config["Walls protection from decay out of TC"] = protection1 = GetConfig("Walls protection from decay out of TC", 100);
            Config["Walls protection from decay in TC"] = protection2 = GetConfig("Walls protection from decay in TC", 100);
            SaveConfig();
        }
        
        T GetConfig<T>(string name, T value)
        {
            return Config[name] == null ? value : (T) Convert.ChangeType(Config[name], typeof(T));
        }

        #endregion

        #region Oxide Hooks

        private void OnServerInitialized()
        {
            LoadDefaultConfig();
            CheckEntities();
        }
        
        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity.PrefabName.Contains("external.high"))
            {
                AddProtection(entity);
            }
        }

        #endregion

        #region Helpers

        private void CheckEntities()
        {
            foreach (var entity in UnityEngine.Object.FindObjectsOfType<BaseEntity>().Where(x => x.PrefabName.Contains("external.high")))
            {
                AddProtection(entity);   
            }
        }
        
        private void AddProtection(BaseNetworkable entity)
        {
            var ent = entity.GetComponent<BaseCombatEntity>();
            if(ent == null) {return;}
            var value = ent.GetBuildingPrivilege() == null ? protection1 : protection2;
            if (ent.baseProtection.Get(DamageType.Decay) != value) {ent.baseProtection.Add(DamageType.Decay, value);}
        }

        #endregion
    }
}