using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using Newtonsoft.Json;
using System.Text;
using System.IO.Compression;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;

namespace Oxide.Plugins
{
    [Info("UpgradeTimer", "Jake_Rich", "2.0.0", ResourceId = 2740)]
    [Description("Time limit to upgrade twig after it has been placed")]

    public class UpgradeTimer : RustPlugin
    {
        public static UpgradeTimer _plugin;
        public static JSONFile<ConfigData> _settingsFile;
        public static ConfigData Settings { get { return _settingsFile.Instance; } }
        public Dictionary<BaseEntity,EntityData> EntityDictionary;

        void Init()
        {
            _plugin = this;
        }

        void Loaded()
        {
            _settingsFile = new JSONFile<ConfigData>($"{Name}", ConfigLocation.Config, extension: ".cfg");
            EntityDictionary = new Dictionary<BaseEntity, EntityData>();
        }

        void Unload()
        {

        }

        public class ConfigData
        {
            public int UpgradeDelay = 10;
            public int WoodUpgradeDelay = 3 * 60;
            public int StoneUpgradeDelay = 10 * 60;
            public int MetalUpgradeDelay = 20 * 60;
            public int ArmouredUpgradeDelay = 30 * 60;
            public float ExplosiveDamageDelay = 90f;
            public float NormalDamageDelay = 10f;

            public Dictionary<string, int> EntityUpgradeTime = new Dictionary<string, int>()
            {
                { "wall.external.high.stone", 20 * 60},
                { "wall.external.high.wood",  10 * 60},
                { "gates.external.high.stone", 20 * 60},
                { "gates.external.high.wood", 10 * 60},
                { "door.hinged.wood", 3 * 60},
                { "door.hinged.metal", 10 * 60},
                { "door.hinged.toptier", 20 * 60},
                { "door.double.hinged.wood", 3 * 60},
                { "door.double.hinged.metal", 10 * 60},
                { "door.double.hinged.toptier", 20 * 60},
                { "wall.frame.garagedoor", 10 * 60},
            };
        }

        public class EntityData
        {
            public BaseCombatEntity Entity;
            public Timer timer;
            public int TimerRate = 6;
            public float NextRepairTime;
            public float NextUpgradeTime;

            public EntityData(BaseCombatEntity block)
            {
                Entity = block;
            }

            public void ResetTimer()
            {
                StartTimer();
            }

            public void StartTimer()
            {
                NextRepairTime = Mathf.Max(UnityEngine.Time.time + TimerRate + 4, NextRepairTime);
                timer?.Destroy();
                timer = _plugin.timer.Every(TimerRate, TimerLoop);
            }

            private void TimerLoop()
            {
                if (Entity == null)
                {
                    return;
                }
                NextRepairTime = Mathf.Max(UnityEngine.Time.time + TimerRate + 4, NextRepairTime);
                float rate = 0;
                if (Entity is BuildingBlock)
                {
                    switch (((BuildingBlock)Entity).grade)
                    {
                        case BuildingGrade.Enum.Wood: { rate = Settings.WoodUpgradeDelay; break; }
                        case BuildingGrade.Enum.Stone: { rate = Settings.StoneUpgradeDelay; break; }
                        case BuildingGrade.Enum.Metal: { rate = Settings.MetalUpgradeDelay; break; }
                        case BuildingGrade.Enum.TopTier: { rate = Settings.ArmouredUpgradeDelay; break; }
                        default: { return; }
                    }
                }
                int amount;
                if (Settings.EntityUpgradeTime.TryGetValue(Entity.ShortPrefabName, out amount))
                {
                    rate = amount;
                }
                Entity.healthFraction = Mathf.Min(1f, Entity.healthFraction + (TimerRate / rate));
                if (Entity.healthFraction >= 1f)
                {
                    Destroy();
                }
                if (!(Entity is BuildingBlock))
                {
                    Entity.SendNetworkUpdate();
                }
            }

            public void Destroy()
            {
                timer?.Destroy();
            }
        }

        public EntityData GetEntityData(BaseCombatEntity entity)
        {
            EntityData data;
            if (!EntityDictionary.TryGetValue(entity, out data))
            {
                data = new EntityData(entity);
                EntityDictionary.Add(entity, data);
            }
            return data;
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            BaseCombatEntity entity = go.ToBaseEntity() as BaseCombatEntity;

            if (entity == null)
            {
                return;
            }

            if (Settings.EntityUpgradeTime.ContainsKey(entity.ShortPrefabName) == false)
            {
                return;
            }

            var data = GetEntityData(entity);
            entity.healthFraction = (float)data.TimerRate / Settings.EntityUpgradeTime[entity.ShortPrefabName];
            entity.SendNetworkUpdate();
            data.StartTimer();

            return;

            Timer _timer = null;
            entity.healthFraction = 0;
            entity.lastAttackedTime = Time.time;
            _timer = timer.Repeat(1f, Settings.UpgradeDelay, () =>
            {
                if (entity == null || entity.IsDestroyed || _timer == null)
                {
                    _timer?.Destroy();
                    return;
                }
                entity.lastAttackedTime = Time.time;
                entity.healthFraction = 1f / Settings.UpgradeDelay * (Settings.UpgradeDelay - _timer.Repetitions);
                if (_timer.Repetitions == 0)
                {
                    entity.lastAttackedTime = 0;
                    //timers.Remove(entity);
                }
            });
            //timers.Add(entity, _timer);
        }

        void OnEntityKill(BaseNetworkable net)
        {
            var entity = net as BaseCombatEntity;
            if (entity == null)
            {
                return;
            }
            if (entity is BuildingBlock || Settings.EntityUpgradeTime.ContainsKey(net.ShortPrefabName))
            {
                GetEntityData(entity).Destroy();
                EntityDictionary.Remove(entity);
            }
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if ((entity is BuildingBlock || Settings.EntityUpgradeTime.ContainsKey(entity.ShortPrefabName)) == false)
            {
                return;
            }
            var data = GetEntityData(entity);
            if (info.damageTypes.Has(Rust.DamageType.Explosion))
            {
                data.Destroy();
                data.NextRepairTime = Mathf.Max(UnityEngine.Time.time + Settings.ExplosiveDamageDelay, data.NextRepairTime);
                data.NextUpgradeTime = data.NextRepairTime;
                return;
            }
            if (info.damageTypes.Has(Rust.DamageType.Decay))
            {
                return;
            }
            data.NextRepairTime = Mathf.Max(UnityEngine.Time.time + Settings.NormalDamageDelay, data.NextRepairTime);
            data.NextUpgradeTime = data.NextRepairTime;
            data.ResetTimer();
        }

        object OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade)
        {
            var data = GetEntityData(block);
            if (data.NextUpgradeTime > UnityEngine.Time.time)
            {
                return false;
            }
            float oldHealth = block.health;
            NextFrame(() =>
            {
                if (block == null)
                {
                    return;
                }
                block.health = oldHealth;
                data.ResetTimer();
            });
            return null;
        }

        object OnStructureRepair(BaseCombatEntity entity, BasePlayer player)
        {
            var block = entity as BuildingBlock;
            if (block == null)
            {
                return null;
            }
            var data = GetEntityData(block);
            if (data.NextRepairTime <= UnityEngine.Time.time)
            {
                return null;
            }
            return false;
        }

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