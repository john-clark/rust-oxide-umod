using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("DragableCorpses", "Jake_Rich", "1.0.1")]
    [Description("")]

    public partial class DragableCorpses : RustPlugin
    {
        public static DragableCorpses _plugin;
        public JSONFile<ConfigData> _settingsFile;
        public ConfigData Settings { get { return _settingsFile.Instance; } }
        public PlayerDataController<CorpsePlayerData> PlayerData;

        void Init()
        {
            _plugin = this;
        }

        void Loaded()
        {
            //Dont create empty config files
            if (typeof(ConfigData).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Length > 0)
            {
                _settingsFile = new JSONFile<ConfigData>($"{Name}", ConfigLocation.Config, extension: ".cfg");
            }
            PlayerData = new PlayerDataController<CorpsePlayerData>();

            //Don't create empty lang files
            if (lang_en.Count > 0) 
            {
                lang.RegisterMessages(lang_en, this);
            }
        }

        void Unload()
        {
            PlayerData.Unload();
        }

        object CanLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (!(entity is PlayerCorpse))
            {
                return null;
            }
            if (player.serverInput.IsDown(BUTTON.FIRE_PRIMARY) == false)
            {
                return null;
            }
            PlayerData.Get(player).StartDragingCorpse(entity as PlayerCorpse);
            return false;
        }

        public class ConfigData
        {

        }

        public class CorpsePlayerData : BasePlayerData
        {
            private Timer _timer;
            private PlayerCorpse Corpse;

            public void StartDragingCorpse(PlayerCorpse corpse)
            {
                Corpse = corpse;
                _timer?.Destroy();
                _timer = _plugin.timer.Every(0.1f, TimerLoop);
            }

            private void TimerLoop()
            {
                if (Corpse == null || Corpse.IsDestroyed)
                {
                    StopDraggingCorpse();
                    return;
                }
                if (Player.serverInput.IsDown(BUTTON.FIRE_PRIMARY) == false)
                {
                    StopDraggingCorpse();
                    return;
                }
                UpdateCorpsePosition();
            }

            private void StopDraggingCorpse()
            {
                _timer?.Destroy();
                UpdateCorpsePosition();
            }

            private void UpdateCorpsePosition()
            {
                if (Corpse == null || Corpse.IsDestroyed)
                {
                    return;
                }
                Vector3 targetPosition = Player.eyes.position + Player.eyes.BodyForward() * 2f;
                RaycastHit hit;
                if (Physics.Raycast(Player.eyes.BodyRay(), out hit, 3f, RaycastLayer))
                {
                    targetPosition = hit.point - Player.eyes.BodyForward();
                }
                Corpse.transform.position = targetPosition;
            }
        }

        private static int RaycastLayer = LayerMask.GetMask("Construction", "Deployed", "Default", "Debris", "Terrain", "Tree", "World");

        #region Lang API

        public Dictionary<string, string> lang_en = new Dictionary<string, string>()
        {
            
        };

        public static string GetLangMessage(string key, BasePlayer player)
        {
            return _plugin.lang.GetMessage(key, _plugin, player.UserIDString);
        }

        public static string GetLangMessage(string key, ulong player)
        {
            return _plugin.lang.GetMessage(key, _plugin, player.ToString());
        }

        public static string GetLangMessage(string key, string player)
        {
            return _plugin.lang.GetMessage(key, _plugin, player);
        }

        #endregion

        #region PlayerData

        public class BasePlayerData
        {
            [JsonIgnore]
            public BasePlayer Player { get; set; }

            public string userID { get; set; } = "";

            public BasePlayerData()
            {

            }
            public BasePlayerData(BasePlayer player) : base()
            {
                userID = player.UserIDString;
                Player = player;
            }
        }

        public class PlayerDataController<T> where T : BasePlayerData
        {
            [JsonPropertyAttribute(Required = Required.Always)]
            private Dictionary<string, T> playerData { get; set; } = new Dictionary<string, T>();
            private JSONFile<Dictionary<string, T>> _file;
            private Timer _timer;
            public IEnumerable<T> All { get { return playerData.Values; } }

            public PlayerDataController()
            {

            }

            public PlayerDataController(string filename = null)
            {
                if (filename == null)
                {
                    return;
                }
                _file = new JSONFile<Dictionary<string, T>>(filename);
                _timer = _plugin.timer.Every(120f, () =>
                {
                    _file.Save();
                });
            }

            public void Unload()
            {
                if (_file == null)
                {
                    return;
                }
                _file.Save();
            }

            public T Get(string identifer)
            {
                T data;
                if (!playerData.TryGetValue(identifer, out data))
                {
                    data = Activator.CreateInstance<T>();
                    playerData[identifer] = data;
                }
                return data;
            }

            public T Get(ulong userID)
            {
                return Get(userID.ToString());
            }

            public T Get(BasePlayer player)
            {
                var data = Get(player.UserIDString);
                data.Player = player;
                return data;
            }

            public bool Has(ulong userID)
            {
                return playerData.ContainsKey(userID.ToString());
            }

            public void Set(string userID, T data)
            {
                playerData[userID] = data;
            }

            public bool Remove(string userID)
            {
                return playerData.Remove(userID);
            }

            public void Update(T data)
            {
                playerData[data.userID] = data;
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