// Requires: ZoneManager
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Zone Perms", "MisterPixie", "1.0.3")]
    [Description("Grant players permissions when entering a zone.")]
    class ZonePerms : RustPlugin
    {
        [PluginReference] Plugin ZoneManager;

        #region Data Related
        Dictionary<ulong, ZonePermData> zonePermData = new Dictionary<ulong, ZonePermData>();

        public class ZonePermData
        {
            public List<string> permission { get; set; }
        }
        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("ZonePermData", zonePermData);
        }

        void Unload()
        {
            foreach (var player in zonePermData)
            {
                foreach (var perm in player.Value.permission)
                {
                    permission.RevokeUserPermission(player.Key.ToString(), perm);
                }
            }
            zonePermData.Clear();
            SaveData();
        }

        void OnServerSave()
        {
            SaveData();
        }
        #endregion

        #region Hooks
        private void Init()
        {
            LoadVariables();
            zonePermData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, ZonePermData>>("ZonePermData");
        }

        void OnEnterZone(string ZoneID, BasePlayer player)
        {
            if (!configData.Enable)
                return;

            ZoneAddons zoneaddvalue;
            ZonePermData zonepermvalue;
            if (configData.Zones.TryGetValue(ZoneID, out zoneaddvalue))
            {
                if (!zoneaddvalue.EnableZone)
                {
                    return;
                }

                if (!zonePermData.TryGetValue(player.userID, out zonepermvalue))
                {
                    zonePermData.Add(player.userID, new ZonePermData()
                    {
                        permission = new List<string>
                        {

                        }
                    });
                }

                foreach (var perm in zoneaddvalue.Permissions)
                {
                    permission.GrantUserPermission(player.UserIDString, perm, null);
                    zonePermData[player.userID].permission.Add(perm);
                }

                SaveData();
                
            }
        }

        void OnExitZone(string ZoneID, BasePlayer player)
        {
            if (!configData.Enable)
                return;

            ZoneAddons zoneaddvalue;
            ZonePermData zonepermvalue;
            if (configData.Zones.TryGetValue(ZoneID, out zoneaddvalue))
            {
                if (!zoneaddvalue.EnableZone)
                {
                    return;
                }

                if (!zonePermData.TryGetValue(player.userID, out zonepermvalue))
                {
                    return;
                }

                foreach (var perm in zoneaddvalue.Permissions)
                {
                    permission.RevokeUserPermission(player.UserIDString, perm);
                    zonepermvalue.permission.Remove(perm);
                }

                zonePermData.Remove(player.userID);
                SaveData();
            }
        }

        #endregion

        #region Config

        public class ZoneAddons
        {
            public bool EnableZone;
            public List<string> Permissions;
        }

        private ConfigData configData;
        private class ConfigData
        {
            public bool Enable;
            public Dictionary<string, ZoneAddons> Zones;
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
                Enable = false,
                Zones = new Dictionary<string, ZoneAddons>
                {
                    ["431235"] = new ZoneAddons()
                    {
                        EnableZone = false,
                        Permissions = new List<string>
                        {
                            "permission1",
                            "permisson2"
                        }
                    },
                    ["749261"] = new ZoneAddons()
                    {
                        EnableZone = false,
                        Permissions = new List<string>
                        {
                            "permission1",
                            "permisson2"
                        }
                    }
                }

            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion
    }
}