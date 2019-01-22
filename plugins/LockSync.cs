using System.Collections.Generic;
using System;
using System.IO;
using Newtonsoft.Json;
using System.Text;
using System.IO.Compression;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;

namespace Oxide.Plugins
{
    [Info("LockSync", "Jake_Rich", "1.0.3", ResourceId = 2738)]
    [Description("TC code lets you open all doors in privledge that share the same code.")]

    public partial class LockSync : RustPlugin
    {
        public static LockSync _plugin;
        public JSONFile<ConfigData> _settingsFile;
        public ConfigData Settings { get { return _settingsFile.Instance; } }
        public PlayerDataController<TemplatePlayerData> PlayerData;

        void Init()
        {
            _plugin = this;
        }

        void Loaded()
        {
            _settingsFile = new JSONFile<ConfigData>($"{Name}", ConfigLocation.Config, extension: ".cfg");
            PlayerData = new PlayerDataController<TemplatePlayerData>();
            lang.RegisterMessages(lang_en, this);
        }

        void Unload()
        {
            PlayerData.Unload();
        }

        object CanUseLockedEntity(BasePlayer player, CodeLock codeLock)
        {
            if (codeLock.IsLocked() == false)
            {
                return null;
            }
            var building = (codeLock.GetParentEntity() as DecayEntity)?.GetBuilding();
            if (building == null)
            {
                //TODO: Doesn't work for high walls. They don't store what building they are attached to
                //Puts($"Couldn't find building for {codeLock.GetParentEntity()?.PrefabName}!");
                return null;
            }
            if (building.buildingPrivileges == null)
            {
                return null;
            }
            if (building.buildingPrivileges.Count == 0)
            {
                return null;
            }
            var tcLock = building.buildingPrivileges[0].GetSlot(BaseEntity.Slot.Lock) as CodeLock;
            if (tcLock == null)
            {
                return null;
            }
            if (tcLock.code != codeLock.code)
            {
                return null;
            }
            if (tcLock.whitelistPlayers.Contains(player.userID))
            {
                return true;
            }
            return null;
        }

        void OnCodeEntered(CodeLock codeLock, BasePlayer player, string code)
        {
            if (!(codeLock.GetParentEntity() is BuildingPrivlidge))
            {
                return;
            }
            if (codeLock.code == code)
            {
                PrintToChat(player, $"<color=#FF7F00>LockSync:</color> {lang.GetMessage("AutoAuthInfo", this, player.UserIDString)}");
            }
        }

        void CanChangeCode(CodeLock codeLock, BasePlayer player, string code, bool flag)
        {
            PrintToChat(player, $"<color=#FF7F00>LockSync:</color> {lang.GetMessage("AutoAuthInfo", this, player.UserIDString)}");
        }

        public class ConfigData
        {

        }

        public class TemplatePlayerData : BasePlayerData
        {

        }

        #region Lang API

        public Dictionary<string, string> lang_en = new Dictionary<string, string>()
        {
            { "AutoAuthInfo", "You will be automatically authorized on code locks with the same code as the Tool Cupboard!" },
        };

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

            public JSONFile(string name, ConfigLocation location = ConfigLocation.Data, string path = null, string extension = ".json", bool saveOnUnload = false)
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

        }

        #endregion

    }
}