using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SafeTraps", "Jake_Rich", "1.0.0")]
    [Description("Players won't trigger their own traps")]

    public class SafeTraps : RustPlugin
    {
        public JSONFile<ConfigData> _settingsFile;
        public ConfigData Settings { get { return _settingsFile.Instance; } }


        void Loaded()
        {
            //Dont create empty config files
            if (typeof(ConfigData).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Length > 0)
            {
                _settingsFile = new JSONFile<ConfigData>($"{Name}", ConfigLocation.Config, extension: ".cfg");
            }
        }

        public class ConfigData
        {
            public float SafeTime = 60;
            public bool UsePermissions = false;
        }

        public class EntityInfo
        {
            public ulong TrapOwner;
            public DateTime LastArmedTime = DateTime.UtcNow;
        }

        #region Entity Info

        private Dictionary<uint, EntityInfo> _entityInfo = new Dictionary<uint, EntityInfo>();

        public EntityInfo GetEntityInfo(BaseNetworkable entity)
        {
            EntityInfo info;
            if (!_entityInfo.TryGetValue(entity.net.ID, out info))
            {
                info = new EntityInfo();
                _entityInfo.Add(entity.net.ID, info);
            }
            return info;
        }

        #endregion

        #region Hooks

        //Landmine or bear trap
        object OnTrapTrigger(BaseEntity trap, GameObject go)
        {
            var player = go.GetComponent<BasePlayer>();
            if (player == null)
            {
                return null;
            }
            var info = GetEntityInfo(trap);
            if (Settings.SafeTime > 0 && DateTime.UtcNow.Subtract(info.LastArmedTime).TotalSeconds > Settings.SafeTime)
            {
                return null;
            }
            if (info.TrapOwner == 0)
            {
                if (player.userID == trap.OwnerID)
                {
                    return false;
                }
                return null;
            }
            if (player.userID == info.TrapOwner)
            {
                return false;
            }
            return null;
        }

        //Bear trap
        void OnTrapArm(BearTrap trap, BasePlayer player)
        {
            var info = GetEntityInfo(trap);
            info.TrapOwner = player.userID;
            info.LastArmedTime = DateTime.UtcNow;
        }

        void OnEntityBuilt(Planner planner, GameObject obj)
        {
            var player = planner.GetOwnerPlayer();
            var entity = obj.GetComponent<BaseEntity>();
            if (entity == null || player == null)
            {
                return;
            }
            if (entity is Landmine || entity is BearTrap)
            {
                var data = GetEntityInfo(entity);
                data.LastArmedTime = DateTime.UtcNow;
                data.TrapOwner = player.userID;
            }
        }

        #endregion

        #region Configuration Files

        public enum ConfigLocation
        {
            Data = 0,
            Config = 1,
            Logs = 2,
            Plugins = 3,
            Lang = 4,
            Custom = 5,
        }

        public class JSONFile<Type> where Type : class
        {
            private DynamicConfigFile _file;
            public string _name { get; set; }
            public Type Instance { get; set; }
            private ConfigLocation _location { get; set; }
            private string _path { get; set; }

            public JSONFile(string name, ConfigLocation location = ConfigLocation.Data, string path = null, string extension = ".json")
            {
                _name = name.Replace(".json", "");
                _location = location;
                switch (location)
                {
                    case ConfigLocation.Data:
                        {
                            _path = $"{Oxide.Core.Interface.Oxide.DataDirectory}/{name}{extension}";
                            break;
                        }
                    case ConfigLocation.Config:
                        {
                            _path = $"{Oxide.Core.Interface.Oxide.ConfigDirectory}/{name}{extension}";
                            break;
                        }
                    case ConfigLocation.Logs:
                        {
                            _path = $"{Oxide.Core.Interface.Oxide.LogDirectory}/{name}{extension}";
                            break;
                        }
                    case ConfigLocation.Lang:
                        {
                            _path = $"{Oxide.Core.Interface.Oxide.LangDirectory}/{name}{extension}";
                            break;
                        }
                    case ConfigLocation.Custom:
                        {
                            _path = $"{path}/{name}{extension}";
                            break;
                        }
                }
                _file = new DynamicConfigFile(_path);
                _file.Settings = new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
                Init();
            }

            public virtual void Init()
            {
                Load();
                Save();
                Load();
            }

            public virtual void Load()
            {

                if (!_file.Exists())
                {
                    Save();
                }
                Instance = _file.ReadObject<Type>();
                if (Instance == null)
                {
                    Instance = Activator.CreateInstance<Type>();
                    Save();
                }
                return;
            }

            public virtual void Save()
            {
                _file.WriteObject(Instance);
                return;
            }

            public virtual void Reload()
            {
                Load();
            }

            private void Unload(Plugin sender, PluginManager manager)
            {

            }
        }

        #endregion
    }
}