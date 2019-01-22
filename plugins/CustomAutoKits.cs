using System.Collections.Generic;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("CustomAutoKits", "Absolut", "1.0.0", ResourceId = 41234154)]

    class CustomAutoKits : RustPlugin
    {

        [PluginReference]Plugin EventManager;
        [PluginReference]Plugin Kits;

        void OnServerInitialized()
        {
            LoadVariables();
            foreach (var entry in configData.AutoKits)
                if (!permission.PermissionExists(this.Name+"."+entry.Key, this))
                    permission.RegisterPermission(this.Name + "." + entry.Key, this);
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (EventManager)
            {
                object isPlaying = EventManager?.Call("isPlaying", new object[] { player });
                if (isPlaying is bool)
                    if ((bool)isPlaying)
                        return;
            }
            foreach (var entry in configData.AutoKits)
                if (permission.UserHasPermission(player.UserIDString, this.Name + "." + entry.Key))
                {
                    Kits?.Call("GiveKit", player, entry.Value);
                    break;
                }
        }


        private ConfigData configData;
        class ConfigData
        {
            public Dictionary<string, string> AutoKits { get; set; }
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                AutoKits = new Dictionary<string, string>
                {
                    {"permission1.allow", "KitName1" },
                    {"permission2.allow", "KitName2" },
                    {"permission3.allow", "KitName3" },
                }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);

    }
}