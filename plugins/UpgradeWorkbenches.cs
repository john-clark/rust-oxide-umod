using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using Newtonsoft.Json;
using System.Text;
using System.IO.Compression;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("UpgradeWorkbenches", "Jake_Rich", "1.0.4", ResourceId = 2741)]
    [Description("Lets players upgrade workbenches")]

    public class UpgradeWorkbenches : RustPlugin
    {
        public static UpgradeWorkbenches _plugin;

        void Init()
        {
            _plugin = this;
        }

        void Loaded()
        {
            lang.RegisterMessages(lang_en, this);
        }

        void Unload()
        {

        }

        object CanMoveItem(Item movedItem, PlayerInventory playerInventory, uint targetContainerID, int targetSlot, int amount)
        {
            if (movedItem.info.shortname != "workbench1" && movedItem.info.shortname != "workbench2" && movedItem.info.shortname != "workbench3")
            {
                return null;
            }
            var container = playerInventory.FindContainer(targetContainerID);
            if (container == null)
            {
                return null;
            }
            if (container.entityOwner == null)
            {
                return null;
            }
            if (!(container.entityOwner is Workbench))
            {
                return null;
            }
            int workbenchItemLevel = int.Parse(movedItem.info.shortname.Replace("workbench", ""));
            int workbenchLevel = int.Parse(container.entityOwner.ShortPrefabName.Replace("workbench", "").Replace(".deployed", ""));
            if (workbenchItemLevel <= workbenchLevel)
            {
                return null;
            }
            var player = playerInventory.GetComponent<BasePlayer>();
            var workbench = GameManager.server.CreateEntity($"assets/prefabs/deployable/tier {workbenchItemLevel} workbench/workbench{workbenchItemLevel}.deployed.prefab", container.entityOwner.transform.position, container.entityOwner.transform.rotation) as Workbench;
            workbench.SendMessage("SetDeployedBy", player, SendMessageOptions.DontRequireReceiver);
            workbench.OwnerID = container.entityOwner.OwnerID;
            workbench.Spawn();
            //Since you can only upgrade to a tier 2 or tier 3 workbench, always can play the same sound effect (tier 2 & 3 share sound effect)
            Effect.server.Run("assets/prefabs/deployable/quarry/effects/mining-quarry-deploy.prefab", workbench.transform.position, Vector3.up, null, false);
            movedItem.UseItem();
            var oldItem = ItemManager.CreateByName($"workbench{workbenchLevel}");
            foreach(var item in (container.entityOwner as Workbench).inventory.itemList.ToList())
            {
                player.GiveItem(item);
            }
            player.GiveItem(oldItem);
            player.EndLooting();
            playerInventory.ServerUpdate(0);
            container.entityOwner.Kill();
            return false;
        }

        void OnEntityBuilt(Planner planner, GameObject go)
        {
            var player = planner.GetOwnerPlayer();
            if (player == null)
            {
                return;
            }
            var workbench = go.GetComponent<Workbench>();
            if (workbench == null)
            {
                return;
            }
            PrintToChat(player, $"<color=#FF7F00>UpgradeWorkbenches:</color> {GetLangMessage("InfoMessage", player)}");
        }

        #region Lang API

        public Dictionary<string, string> lang_en = new Dictionary<string, string>()
        {
            {"InfoMessage", "Workbenches can be upgraded!\n\nTo upgrade, drag a workbench item into a placed workbench's inventory!" },
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
            public bool SaveOnUnload = false;
            public bool Compressed = false;

            public JSONFile(string name, ConfigLocation location = ConfigLocation.Data, string path = null, string extension = ".json", bool saveOnUnload = false)
            {
                SaveOnUnload = saveOnUnload;
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
                _plugin.OnRemovedFromManager.Add(new Action<Plugin, PluginManager>(Unload));
                Load();
                Save();
                Load();
            }

            public virtual void Load()
            {
                if (Compressed)
                {
                    LoadCompressed();
                    return;
                }

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

            private void LoadCompressed()
            {
                string str = _file.ReadObject<string>();
                if (str == null || str == "")
                {
                    Instance = Activator.CreateInstance<Type>();
                    return;
                }
                using (var compressedStream = new MemoryStream(Convert.FromBase64String(str)))
                using (var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
                using (var resultStream = new MemoryStream())
                {
                    var buffer = new byte[4096];
                    int read;

                    while ((read = zipStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        resultStream.Write(buffer, 0, read);
                    }

                    Instance = JsonConvert.DeserializeObject<Type>(Encoding.UTF8.GetString(resultStream.ToArray()));
                }
            }

            public virtual void Save()
            {
                if (Compressed)
                {
                    SaveCompressed();
                    return;
                }

                _file.WriteObject(Instance);
                return;
            }

            private void SaveCompressed()
            {
                using (var stream = new MemoryStream())
                {
                    using (GZipStream zipStream = new GZipStream(stream, CompressionMode.Compress))
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(Instance));
                        zipStream.Write(bytes, 0, bytes.Length);
                        zipStream.Close();
                        _file.WriteObject(Convert.ToBase64String(stream.ToArray()));
                    }
                }
            }

            public virtual void Reload()
            {
                Load();
            }

            private void Unload(Plugin sender, PluginManager manager)
            {
                if (SaveOnUnload)
                {
                    Save();
                }
            }
        }

        #endregion

    }
}