using System;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Purifier Config", "Shady", "1.0.4", ResourceId = 1911)]
    [Description("Tweak settings for water purifiers.")]
    class PurifierConfig : RustPlugin
    {
        bool init = false;
        #region Config
        int WPM;
        int WaterRatio;

        protected override void LoadDefaultConfig()
        {
            Config["WaterToProcessPerMinute"] = WPM = GetConfig("WaterToProcessPerMinute", 120);
            Config["FreshWaterRatio"] = WaterRatio = GetConfig("FreshWaterRatio", 4);
            SaveConfig();
        }
        #endregion
        #region Hooks
        void OnServerInitialized()
        {
            var purifiers = BaseEntity.serverEntities?.Where(p => p != null && (p is WaterPurifier))?.Select(p => p as WaterPurifier)?.ToList() ?? null;
            if (purifiers != null && purifiers.Count > 0)
            {
                for (int i = 0; i < purifiers.Count; i++) ConfigurePurifier(purifiers[i]);
                Puts("Configured " + purifiers.Count.ToString("N0") + " water purifiers successfully!");
            }
            init = true;
        }

        void Init() => LoadDefaultConfig();

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity == null || !init || !(entity is WaterPurifier)) return;
            var purifier = entity?.GetComponent<WaterPurifier>() ?? null;
            if (purifier != null) ConfigurePurifier(purifier);
        }
        #endregion
        #region ConfigurePurifiers
        void ConfigurePurifier(WaterPurifier purifier)
        {
            if (purifier == null) return;
            purifier.waterToProcessPerMinute = WPM;
            purifier.freshWaterRatio = WaterRatio;
        }
        #endregion
        #region Util
        T GetConfig<T>(string name, T defaultValue) { return (Config[name] == null) ? defaultValue : (T)Convert.ChangeType(Config[name], typeof(T)); }
        #endregion
    }
}