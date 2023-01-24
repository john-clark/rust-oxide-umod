using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("LootPlus", "Iv Misticos", "1.0.2")]
    [Description("Modify loot on your server.")]
    class LootPlus : RustPlugin
    {
        #region Configuration

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Loot Skins")]
            public Dictionary<string, Dictionary<string, ulong>> Skins = new Dictionary<string, Dictionary<string, ulong>>
            {
                { "global.example", new Dictionary<string, ulong>
                {
                    { "stones.example", 0 }
                } },
                { "crate_basic.example", new Dictionary<string, ulong>
                {
                    { "wood.example", 0 }
                } }
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();

        #endregion
        
        #region Oxide Hooks

        // ReSharper disable once UnusedMember.Local
        private void OnServerInitialized()
        {
            LoadConfig();
            
            var containers = UnityEngine.Object.FindObjectsOfType<LootContainer>();
            var containersCount = containers.Length;
            for (var i = 0; i < containersCount; i++)
            {
                var container = containers[i];
                LootHandler(container);
            }
        }

        // ReSharper disable once UnusedMember.Local
        private void OnLootSpawn(LootContainer container) => NextFrame(() => LootHandler(container));

        // ReSharper disable once SuggestBaseTypeForParameter
        private void LootHandler(LootContainer entity)
        {
            if (entity == null)
                return;

            Dictionary<string, ulong> items1;
            Dictionary<string, ulong> itemsGlobal = null;
            if (!_config.Skins.TryGetValue(entity.ShortPrefabName, out items1) && !_config.Skins.TryGetValue("global", out itemsGlobal))
                return;

            var items2 = entity.inventory.itemList;
            var items2Count = items2.Count;
            for (var i = 0; i < items2Count; i++)
            {
                var item = items2[i];
                itemsGlobal?.TryGetValue(item.info.shortname, out item.skin);
                items1?.TryGetValue(item.info.shortname, out item.skin);
            }
        }
        
        #endregion
    }
}