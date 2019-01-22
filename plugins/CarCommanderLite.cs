using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;
using System.Linq;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("CarCommanderLite", "k1lly0u", "0.1.0", ResourceId = 0)]
    class CarCommanderLite : RustPlugin
    {
        #region Fields
        [PluginReference]
        Plugin Spawns;

        List<uint> storedCars = new List<uint>();
        private DynamicConfigFile data;

        static CarCommanderLite ins;
        
        private List<CarController> saveableCars = new List<CarController>();

        private bool initialized;
        private bool wipeData = false;

        private int fuelType;
        private int repairType;
        private string fuelTypeName;
        private string repairTypeName;

        const string carPrefab = "assets/content/vehicles/sedan_a/sedantest.entity.prefab";
        const string heliExplosion = "assets/prefabs/npc/patrol helicopter/effects/heli_explosion.prefab";

        const string uiHealth = "CCUI_Health";
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            permission.RegisterPermission("carcommanderlite.admin", this);
            permission.RegisterPermission("carcommanderlite.use", this);
            permission.RegisterPermission("carcommanderlite.canspawn", this);

            lang.RegisterMessages(Messages, this);
            data = Interface.Oxide.DataFileSystem.GetFile("carcommander_data");
        }

        private void OnServerInitialized()
        {
            ins = this;
            LoadVariables();
            LoadData();

            initialized = true;

            if (wipeData)
            {
                PrintWarning("Map wipe detected! Wiping previous car data");
                saveableCars.Clear();
                SaveData();
            }

            timer.In(3, RestoreVehicles);
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;
            if (entity.GetComponent<CarController>())            
                entity.GetComponent<CarController>().ManageDamage(info);                       
        }
        
        private void OnEntityKill(BaseNetworkable networkable)
        {
            CarController controller = networkable.GetComponent<CarController>();
            if (controller != null && saveableCars.Contains(controller))
            {
                saveableCars.Remove(controller);
            }
        }

        private void OnNewSave(string filename) => wipeData = true;

        private void OnServerSave()
        {
            for (int i = saveableCars.Count - 1; i >= 0; i--)
            {
                CarController controller = saveableCars[i];
                if (controller == null || controller.entity == null || !controller.entity.IsValid() || controller.entity.IsDestroyed)
                {
                    saveableCars.RemoveAt(i);
                    continue;
                }                
            }
            SaveData();
        }

        private void Unload()
        {
            var objects = UnityEngine.Object.FindObjectsOfType<CarController>();
            if (objects != null)
            {
                foreach (var obj in objects)
                    UnityEngine.Object.Destroy(obj);
            }
        }

        private object CanMountEntity(BaseMountable mountable, BasePlayer player)
        {
            CarController controller = mountable.GetComponent<CarController>();
            if (controller != null)
            {
                if (player.isMounted)
                    return false;
                
                if (controller.isDieing)
                    return false;

                if (!HasPermission(player, "carcommanderlite.use"))
                {
                    SendReply(player, msg("nopermission", player.UserIDString));
                    return false;
                }                
            }            
            return null;
        }

        private void OnEntityMounted(BaseMountable mountable, BasePlayer player)
        {
            CarController controller = mountable.GetComponent<CarController>();
            if (controller != null && controller.player == null)            
                controller.OnDriverEnter(player);                       
        }

        private void OnEntityDismounted(BaseMountable mountable, BasePlayer player)
        {
            CarController controller = mountable.GetComponent<CarController>();
            if (controller != null && controller.player == player)            
                controller.OnDriverExit();
        }
        #endregion

        #region Functions        
        private bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm) || permission.UserHasPermission(player.UserIDString, "carcommanderlite.admin");

        private void RestoreVehicles()
        {
            if (storedCars.Count > 0)
            {
                BaseEntity[] objects = BaseEntity.saveList.Where(x => x is BaseCar).ToArray();
                if (objects != null)
                {
                    foreach (var obj in objects)
                    {
                        if (!obj.IsValid() || obj.IsDestroyed)
                            continue;

                        if (storedCars.Contains(obj.net.ID))
                            obj.gameObject.AddComponent<CarController>();
                    }
                }
            }
            CheckForSpawns();
        }

        private BaseEntity SpawnAtLocation(Vector3 position, Quaternion rotation = default(Quaternion), bool enableSaving = false)
        {
            BaseCar entity = (BaseCar)GameManager.server.CreateEntity(carPrefab, position + Vector3.up, rotation);
            entity.enableSaving = enableSaving;
            entity.Spawn();

            CarController controller = entity.gameObject.AddComponent<CarController>();

            if (enableSaving)
            {
                saveableCars.Add(controller);
                SaveData();
            }

            return entity;
        }

        private void CheckForSpawns()
        {
            if (configData.Spawnable.Enabled)
            {
                if (saveableCars.Count < configData.Spawnable.Max)
                {
                    object position = null;
                    if (!Spawns)
                    {
                        PrintError("Spawns Database can not be found! Unable to autospawn cars");
                        return;
                    }

                    object success = Spawns.Call("GetSpawnsCount", configData.Spawnable.Spawnfile);
                    if (success is string)
                    {
                        PrintError("An invalid spawnfile has been set in the config. Unable to autospawn cars : " + (string)success);
                        return;
                    }

                    success = Spawns.Call("GetRandomSpawn", configData.Spawnable.Spawnfile);
                    if (success is string)
                    {
                        PrintError((string)success);
                        return;
                    }
                    else position = (Vector3)success;

                    if (position != null)
                    {
                        List<BaseCar> entities = Facepunch.Pool.GetList<BaseCar>();
                        Vis.Entities((Vector3)position, 5f, entities);
                        if (entities.Count > 0)
                            timer.In(60, CheckForSpawns);
                        else
                        {
                            SpawnAtLocation((Vector3)position, new Quaternion(), true);
                            timer.In(configData.Spawnable.Time, CheckForSpawns);
                        }
                        Facepunch.Pool.FreeList(ref entities);
                    }
                }
            }
        }
        #endregion

        #region Component
        public class CarController : MonoBehaviour
        {
            public BaseCar entity;
            public BasePlayer player;
            public bool isDieing;

            private bool allowHeldItems;
            private string[] disallowedItems;

            private void Awake()
            {
                entity = GetComponent<BaseCar>();

                allowHeldItems = !ins.configData.ActiveItems.Disable;
                disallowedItems = ins.configData.ActiveItems.BlackList;
            }

            private void Update()
            {
                UpdateHeldItems();
                CheckWaterLevel();
            }

            public void OnDriverEnter(BasePlayer player)
            {
                this.player = player;
                ins.CreateHealthUI(player, this);
            }

            public void OnDriverExit()
            {
                ins.DestroyUI(player);
                this.player = null;                
            }

            public void ManageDamage(HitInfo info)
            {
                if (isDieing)
                {
                    NullifyDamage(info);
                    return;
                }

                if (info.damageTypes.GetMajorityDamageType() == DamageType.Bullet)
                    info.damageTypes.ScaleAll(200);

                if (info.damageTypes.Total() >= entity.health)
                {
                    isDieing = true;
                    NullifyDamage(info);
                    OnDeath();
                    return;
                }

                if (player != null)
                    ins.NextTick(() => ins.CreateHealthUI(player, this));
            }

            private void NullifyDamage(HitInfo info)
            {
                info.damageTypes = new DamageTypeList();
                info.HitEntity = null;
                info.HitMaterial = 0;
                info.PointStart = Vector3.zero;
            }

            public void UpdateHeldItems()
            {
                if (player == null)
                    return;

                var item = player.GetActiveItem();
                if (item == null || item.GetHeldEntity() == null)
                    return;

                if (disallowedItems.Contains(item.info.shortname) || !allowHeldItems)
                {
                    player.ChatMessage(ins.msg("itemnotallowed", player.UserIDString));

                    var slot = item.position;
                    item.SetParent(null);
                    item.MarkDirty();

                    ins.timer.Once(0.15f, () =>
                    {
                        if (player == null || item == null) return;
                        item.SetParent(player.inventory.containerBelt);
                        item.position = slot;
                        item.MarkDirty();
                    });
                }
            }

            public void CheckWaterLevel()
            {
                if (WaterLevel.Factor(entity.WorldSpaceBounds().ToBounds()) > 0.7f)                
                    StopToDie();                
            }

            public void StopToDie()
            {
                if (entity != null)
                {
                    entity.SetFlag(BaseEntity.Flags.Reserved1, false, false);

                    foreach (var wheel in entity.wheels)
                    {
                        wheel.wheelCollider.motorTorque = 0;
                        wheel.wheelCollider.brakeTorque = float.MaxValue;
                    }

                    entity.GetComponent<Rigidbody>().velocity = Vector3.zero;                    
                }
                OnDeath();
            }

            private void OnDeath()
            {
                isDieing = true;

                if (player != null)
                {
                    entity.DismountPlayer(player);
                    player.EnsureDismounted();
                }

                InvokeHandler.Invoke(this, () =>
                {
                    Effect.server.Run(heliExplosion, transform.position);
                    ins.NextTick(() =>
                    {
                        if (entity != null && !entity.IsDestroyed)
                            entity.DieInstantly();
                        Destroy(this);
                    });
                }, 5f);
            }
        }
        #endregion

        #region UI
        #region UI Elements
        public static class UI
        {
            static public CuiElementContainer ElementContainer(string panelName, string color, UI4 dimensions, bool useCursor = false, string parent = "Overlay")
            {
                var NewElement = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax()},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
                        panelName
                    }
                };
                return NewElement;
            }
            static public void Panel(ref CuiElementContainer container, string panel, string color, UI4 dimensions, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() },
                    CursorEnabled = cursor
                },
                panel);
            }
            static public void Label(ref CuiElementContainer container, string panel, string text, int size, UI4 dimensions, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Align = align, Text = text, Font = "droidsansmono.ttf" },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() }
                },
                panel);

            }
            public static string Color(string hexColor, float alpha)
            {
                if (hexColor.StartsWith("#"))
                    hexColor = hexColor.Substring(1);
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }
        public class UI4
        {
            public float xMin, yMin, xMax, yMax;
            public UI4(float xMin, float yMin, float xMax, float yMax)
            {
                this.xMin = xMin;
                this.yMin = yMin;
                this.xMax = xMax;
                this.yMax = yMax;
            }
            public string GetMin() => $"{xMin} {yMin}";
            public string GetMax() => $"{xMax} {yMax}";
        }
        #endregion

        #region UI Creation        
        private void CreateHealthUI(BasePlayer player, CarController controller)
        {
            var opt = configData.UI.Health;
            if (!opt.Enabled)
                return;

            var container = UI.ElementContainer(uiHealth, UI.Color(opt.Color1, opt.Color1A), new UI4(opt.Xmin, opt.YMin, opt.XMax, opt.YMax));
            UI.Label(ref container, uiHealth, msg("health", player.UserIDString), 12, new UI4(0.03f, 0, 1, 1), TextAnchor.MiddleLeft);
            var percentHealth = System.Convert.ToDouble((float)controller.entity.health / (float)controller.entity.MaxHealth());
            float yMaxHealth = 0.25f + (0.73f * (float)percentHealth);
            UI.Panel(ref container, uiHealth, UI.Color(opt.Color2, opt.Color2A), new UI4(0.25f, 0.1f, yMaxHealth, 0.9f));
            DestroyUI(player);
            CuiHelper.AddUi(player, container);
        }
       
        private void DestroyUI(BasePlayer player) => CuiHelper.DestroyUi(player, uiHealth);        
        #endregion
        #endregion
        
        #region Commands
        [ChatCommand("spawncar")]
        void cmdSpawnCar(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, "carcommanderlite.canspawn")) return;

            Vector3 position = player.transform.position + (player.transform.forward * 3);

            RaycastHit hit;
            if (Physics.SphereCast(player.eyes.position, 0.1f, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward, out hit, 20f))
                position = hit.point;

            SpawnAtLocation(position, new Quaternion(), (args.Length == 1 && args[0].ToLower() == "save"));
        }

        [ChatCommand("clearcars")]
        void cmdClearCars(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, "carcommanderlite.admin")) return;

            for (int i = saveableCars.Count - 1; i >= 0; i--)
            {
                var car = saveableCars[i];
                if (car != null && car.entity != null && !car.entity.IsDestroyed)
                    car.StopToDie();
            }
        }

        [ConsoleCommand("clearcars")]
        void ccmdClearCars(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null || arg.Args == null)
                return;

            for (int i = saveableCars.Count - 1; i >= 0; i--)
            {
                var car = saveableCars[i];
                if (car != null && car.entity != null && !car.entity.IsDestroyed)
                    car.StopToDie();
            }
        }

        [ConsoleCommand("spawncar")]
        void ccmdSpawnCar(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null || arg.Args == null)
                return;

            if (arg.Args.Length == 1 || arg.Args.Length == 2)
            {
                BasePlayer player = covalence.Players.Connected.FirstOrDefault(x => x.Id == arg.GetString(0))?.Object as BasePlayer;
                if (player != null)
                {
                    Vector3 position = player.transform.position + (player.transform.forward * 3) + Vector3.up;

                    RaycastHit hit;
                    if (Physics.SphereCast(player.eyes.position, 0.5f, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward, out hit, 20f))
                        position = hit.point;

                    SpawnAtLocation(position, new Quaternion(), (arg.Args.Length == 2 && arg.Args[1].ToLower() == "save"));
                }
                return;
            }
            if (arg.Args.Length > 2)
            {
                float x;
                float y;
                float z;

                if (float.TryParse(arg.GetString(0), out x))
                {
                    if (float.TryParse(arg.GetString(1), out y))
                    {
                        if (float.TryParse(arg.GetString(2), out z))
                        {
                            SpawnAtLocation(new Vector3(x, y, z), new Quaternion(), (arg.Args.Length == 4 && arg.Args[3].ToLower() == "save"));
                            return;
                        }
                    }
                }
                PrintError($"Invalid arguments supplied to spawn a car at position : (x = {arg.GetString(0)}, y = {arg.GetString(1)}, z = {arg.GetString(2)})");
            }
        }
        #endregion
       
        #region Config        
        private ConfigData configData;
        class ConfigData
        {            
            [JsonProperty(PropertyName = "Spawnable Options")]
            public SpawnableOptions Spawnable { get; set; }            
            [JsonProperty(PropertyName = "Active Item Options")]
            public ActiveItemOptions ActiveItems { get; set; }
            [JsonProperty(PropertyName = "UI Options")]
            public UIOptions UI { get; set; }
                     
            public class SpawnableOptions
            {
                [JsonProperty(PropertyName = "Enable automatic vehicle spawning")]
                public bool Enabled { get; set; }               
                [JsonProperty(PropertyName = "Spawnfile name")]
                public string Spawnfile { get; set; }
                [JsonProperty(PropertyName = "Maximum spawned vehicles at any time")]
                public int Max { get; set; }
                [JsonProperty(PropertyName = "Time between autospawns (seconds)")]
                public int Time { get; set; }
            }           
            public class ActiveItemOptions
            {
                [JsonProperty(PropertyName = "Disable all held items")]
                public bool Disable { get; set; }
                [JsonProperty(PropertyName = "List of disallowed held items (item shortnames)")]
                public string[] BlackList { get; set; }               
            }
            public class UIOptions
            {
                [JsonProperty(PropertyName = "Health settings")]
                public UICounter Health { get; set; }                

                public class UICounter
                {
                    [JsonProperty(PropertyName = "Display to player")]
                    public bool Enabled { get; set; }
                    [JsonProperty(PropertyName = "Position - X minimum")]
                    public float Xmin { get; set; }
                    [JsonProperty(PropertyName = "Position - X maximum")]
                    public float XMax { get; set; }
                    [JsonProperty(PropertyName = "Position - Y minimum")]
                    public float YMin { get; set; }
                    [JsonProperty(PropertyName = "Position - Y maximum")]
                    public float YMax { get; set; }
                    [JsonProperty(PropertyName = "Background color (hex)")]
                    public string Color1 { get; set; }
                    [JsonProperty(PropertyName = "Background alpha")]
                    public float Color1A { get; set; }
                    [JsonProperty(PropertyName = "Status color (hex)")]
                    public string Color2 { get; set; }
                    [JsonProperty(PropertyName = "Status alpha")]
                    public float Color2A { get; set; }
                }
            }
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
                ActiveItems = new ConfigData.ActiveItemOptions
                {
                    Disable = true,
                    BlackList = new string[]
                    {
                        "explosive.timed", "rocket.launcher", "surveycharge", "explosive.satchel"
                    }
                },                
                Spawnable = new ConfigData.SpawnableOptions
                {
                    Enabled = true,
                    Max = 5,
                    Time = 1800,
                    Spawnfile = ""
                },               
                UI = new ConfigData.UIOptions
                {                   
                    Health = new ConfigData.UIOptions.UICounter
                    {
                        Color1 = "#F2F2F2",
                        Color1A = 0.05f,
                        Color2 = "#ce422b",
                        Color2A = 0.6f,
                        Enabled = true,
                        Xmin = 0.69f,
                        XMax = 0.83f,
                        YMin = 0.1f,
                        YMax = 0.135f
                    }
                }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Data Management
        void SaveData() => data.WriteObject(saveableCars.Where(x => x != null).Select(x => x.entity.net.ID).ToList());
        void LoadData()
        {
            try
            {
                storedCars = data.ReadObject<List<uint>>();
            }
            catch
            {
                storedCars = new List<uint>();
            }
        }
        #endregion

        #region Localization
        string msg(string key, string playerId = null) => lang.GetMessage(key, this, playerId);

        Dictionary<string, string> Messages = new Dictionary<string, string>
        {            
            ["nopermission"] = "<color=#D3D3D3>You do not have permission to drive this car</color>",
            ["health"] = "HLTH: ",
            ["itemnotallowed"] = "<color=#D3D3D3>You can not use that item whilst you are in a car</color>"
        };
        #endregion
    }
}