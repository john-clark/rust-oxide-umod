 using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("LootPrivilege", "CodeHarbour.com", "1.1.0")]
    class LootPrivilege : RustPlugin
    {
        PluginConfig config;
		
		void Init()
		{
			RegisterPermissions();
		}
		
		void RegisterPermissions()
		{
			permission.RegisterPermission(config.Permissions.BypassPermission, this);
		}

        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
			if (permission.UserHasPermission(player.UserIDString, config.Permissions.BypassPermission)) return;
			
            if (!player.CanBuild() && config.Settings.EntityList.Contains(entity.ShortPrefabName))
            {
                NextFrame(player.EndLooting);
                player.ChatMessage(Lang("CantLootEntity", player.UserIDString, entity.ShortPrefabName));
            }
        }

        public class PermissionsClass
        {
            public string BypassPermission;
        }

        public class SettingsClass
        {
            public List<string> EntityList = new List<string>();
        }

        public class PluginConfig
        {
            public PermissionsClass Permissions;
			
            public SettingsClass Settings;

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig
                {
                    Settings = new SettingsClass
                    {
                        EntityList = new List<string>()
                        {
                            "hopperoutput",
							"fuelstorage",
                            "furnace",
                            "furnace.large",
                            "refinery_small_deployed"
                        }
                    },
                    Permissions = new PermissionsClass
                    {
                        BypassPermission = "lootprivilege.bypass"
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = PluginConfig.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config);

        void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CantLootEntity"] = "You can't loot a {0} without Building Privilege."
            }, this);
        }

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
    }
}