using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using UnityEngine;
using System.Linq;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("WeaponTrajectory", "Jake_Rich", "1.0.1")]
    [Description("Shows where explosives will land")]

    public class WeaponTrajectory : RustPlugin
    {
        public static WeaponTrajectory _plugin;
        public JSONFile<ConfigData> _settingsFile;
        public ConfigData Settings { get { return _settingsFile.Instance; } }
        public PlayerDataController<GrenadePlayerData> PlayerData;
        public const float UpdateRate = 0.1f;
        public const string Permission_Path = "weapontrajectory.show";

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
            PlayerData = new PlayerDataController<GrenadePlayerData>();

            //Don't create empty lang files
            if (lang_en.Count > 0) 
            {
                lang.RegisterMessages(lang_en, this);
            }

            permission.RegisterPermission(Permission_Path, this);

            timer.Every(UpdateRate, DoPlayerUpdate);
        }

        void Unload()
        {
            PlayerData.Unload();
        }

        public class ConfigData
        {
            public float GrenadeVelocityScale = 1.25f;
            public float RocketVelocityScale = 1f;
            public float HVRocketVelocityScale = 1f;
            public float FireRocketVelocityScale = 1f;
            public bool UsePermissions = false;
        }

        public class GrenadePlayerData : BasePlayerData
        {

        }

        void DoPlayerUpdate()
        {
            foreach(var player in BasePlayer.activePlayerList)
            {
                try
                {
                    CheckPlayerInput(player);
                }
                catch
                {

                }
            }
        }

        void CheckPlayerInput(BasePlayer player)
        {
            if (player.serverInput.IsDown(BUTTON.FIRE_PRIMARY))
            {
                var heldItem = player.GetActiveItem();
                if (heldItem.info.shortname == "grenade.f1")
                {
                    DrawGrenadePath(player);
                }
            }
            else if (player.serverInput.IsDown(BUTTON.FIRE_SECONDARY))
            {
                var heldItem = player.GetActiveItem();
                if (heldItem.info.shortname == "rocket.launcher")
                {
                    DrawRocketPath(player, heldItem.GetHeldEntity() as BaseLauncher);
                }
            }
        }

        void DrawGrenadePath(BasePlayer player)
        {
            if (Settings.UsePermissions)
            {
                if (!permission.UserHasPermission(player.UserIDString, Permission_Path) && !player.IsAdmin)
                {
                    return;
                }
            }
            //Puts($"{comp.maxThrowVelocity}");
            //DrawArrow(player, UpdateRate, Color.green, player.eyes.position, player.eyes.position + player.eyes.BodyForward());
            List<Vector3> list = Pool.GetList<Vector3>();
            DoMovement(list, player.eyes.position, (player.eyes.BodyForward() * 13 + player.estimatedVelocity * 0.5f) * Settings.GrenadeVelocityScale);
            for (int i = 0; i < list.Count - 1; i++)
            {
                DrawArrow(player, UpdateRate, Color.green, list[i], list[i+1]);
            }
            Pool.FreeList(ref list);
        }

        void DrawRocketPath(BasePlayer player,BaseLauncher launcher)
        {
            if (Settings.UsePermissions)
            {
                if (!permission.UserHasPermission(player.UserIDString, Permission_Path) && !player.IsAdmin)
                {
                    return;
                }
            }
            if (launcher.primaryMagazine.contents == 0)
            {
                return;
            }

            var ammoType = launcher.primaryMagazine.ammoType.GetComponent<ItemModProjectile>().projectileObject.Get().GetComponent<ServerProjectile>();

            float velocityScale = Settings.RocketVelocityScale;
            if (launcher.primaryMagazine.ammoType.shortname == "ammo.rocket.hv")
            {
                velocityScale = Settings.HVRocketVelocityScale;
            }
            else if (launcher.primaryMagazine.ammoType.shortname == "ammo.rocket.fire")
            {
                velocityScale = Settings.FireRocketVelocityScale;
            }

            List<Vector3> list = Pool.GetList<Vector3>();
            float step = 0.3f;
            DoMovement(list, player.eyes.position + (player.eyes.BodyForward() * ammoType.speed) * velocityScale * step, (player.eyes.BodyForward() * ammoType.speed) * velocityScale, step, 30, ammoType.gravityModifier);
            for (int i = 0; i < list.Count - 1; i++)
            {
                DrawArrow(player, UpdateRate, Color.green, list[i], list[i + 1]);
            }
            Pool.FreeList(ref list);
        }

        void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (entity.ShortPrefabName == "grenade.f1.deployed")
            {
                entity.SetVelocity(entity.GetComponent<Rigidbody>().velocity * Settings.GrenadeVelocityScale);
            }
        }

        void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            var rocket = entity as TimedExplosive;
            if (rocket == null)
            {
                return;
            }
            var projectile = rocket.GetComponent<ServerProjectile>();
            float velocityScale = Settings.RocketVelocityScale;
            if (entity.ShortPrefabName == "rocket_hv")
            {
                velocityScale = Settings.HVRocketVelocityScale;
            }
            else if (entity.ShortPrefabName == "rocket_fire")
            {
                velocityScale = Settings.FireRocketVelocityScale;
            }
            projectile._currentVelocity *= velocityScale;
            Puts(entity.ShortPrefabName);
            //rocket.explosionEffect.
            //assets/prefabs/npc/patrol helicopter/effects/rocket_explosion.prefab
        }

        private void DoMovement(List<Vector3> output, Vector3 currentPos, Vector3 currentVelocity, float step = 0.1f, int maxSteps = 20, float gravityModifier = 1f)
        {
            for(int i = 0; i < maxSteps; i++)
            {
                output.Add(currentPos);
                //DoMovement

                currentVelocity += Physics.gravity * gravityModifier * step;
                currentPos = currentPos + currentVelocity * step;

                //Update velocity

                //currentVelocity -= currentVelocity.normalized * (currentVelocity.magnitude * this.drag * step);
            }
            output.Add(currentPos);
        }

        private class VelocityUpdate
        {
            public Vector3 Position;
            public Vector3 Velocity;
        }

        void OnFrame()
        {
            SendDDrawCommands();
        }

        #region DDraw

        private static Dictionary<BasePlayer, List<string>> DDrawCommandQueue = new Dictionary<BasePlayer, List<string>>();

        private static void QueueDDrawCommand(BasePlayer player, string command, params object[] args)
        {
            List<string> list;
            if (!DDrawCommandQueue.TryGetValue(player, out list))
            {
                list = new List<string>();
                DDrawCommandQueue.Add(player, list);
            }
            list.Add(ConsoleSystem.BuildCommand(command, args));
        }

        public static void DrawCube(BasePlayer player, float duration, UnityEngine.Color color, Vector3 position, float size = 1)
        {
            QueueDDrawCommand(player, "ddraw.box", duration, color, position, size);
        }

        public static void DrawBox(BasePlayer player, float duration, UnityEngine.Color color, Vector3 min, Vector3 max)
        {
            QueueDDrawCommand(player, "ddraw.line", duration, color, new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z));
            QueueDDrawCommand(player, "ddraw.line", duration, color, new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z));
            QueueDDrawCommand(player, "ddraw.line", duration, color, new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z));
            QueueDDrawCommand(player, "ddraw.line", duration, color, new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z));
            QueueDDrawCommand(player, "ddraw.line", duration, color, new Vector3(max.x, max.y, max.z), new Vector3(max.x, min.y, max.z));
            QueueDDrawCommand(player, "ddraw.line", duration, color, new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z));

            QueueDDrawCommand(player, "ddraw.line", duration, color, new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z));
            QueueDDrawCommand(player, "ddraw.line", duration, color, new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z));
            QueueDDrawCommand(player, "ddraw.line", duration, color, new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z));
            QueueDDrawCommand(player, "ddraw.line", duration, color, new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z));
            QueueDDrawCommand(player, "ddraw.line", duration, color, new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z));
            QueueDDrawCommand(player, "ddraw.line", duration, color, new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z));
        }

        public static void DrawText(BasePlayer player, string text, float duration, UnityEngine.Color color, Vector3 position, int size = 12)
        {
            QueueDDrawCommand(player, "ddraw.text", duration, color, position, size == 12 ? text : $"<size={size}>{text.Replace("\n", "\\n")}</size>");
        }

        public static void DrawSphere(BasePlayer player, float duration, UnityEngine.Color color, Vector3 position, float size)
        {
            QueueDDrawCommand(player, "ddraw.sphere", duration, color, position, size);
        }

        public static void DrawArrow(BasePlayer player, float duration, Color color, Vector3 start, Vector3 end)
        {
            QueueDDrawCommand(player, "ddraw.arrow", duration, color, start, end, 0.1);
        }

        public void SendDDrawCommands()
        {
            if (DDrawCommandQueue.Count == 0)
            {
                return;
            }
            foreach (var request in DDrawCommandQueue)
            {
                if (request.Key == null)
                {
                    continue;
                }
                bool fakeAdmin = request.Key.IsAdmin == false;
                if (fakeAdmin)
                {
                    request.Key.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                    request.Key.SendNetworkUpdateImmediate();
                }
                foreach (var cmd in request.Value)
                {
                    request.Key.SendConsoleCommand(cmd);
                }
                if (fakeAdmin)
                {
                    request.Key.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                    request.Key.SendNetworkUpdateImmediate();
                }
            }
            DDrawCommandQueue.Clear();
        }

        #endregion

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