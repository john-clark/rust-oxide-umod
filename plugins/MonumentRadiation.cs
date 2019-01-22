using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("MonumentRadiation", "k1lly0u", "0.2.55", ResourceId = 1562)]
    class MonumentRadiation : RustPlugin
    {
        static MonumentRadiation ins;
        private bool radsOn;

        private int offTimer;
        private int onTimer;

        private List<RadiationZone> radiationZones = new List<RadiationZone>();

        #region Oxide Hooks  
        private void OnServerInitialized()
        {
            lang.RegisterMessages(Messages, this);

            ins = this;
            if (!ConVar.Server.radiation)
            {
                radsOn = false;
                ConVar.Server.radiation = true;
            }
            else radsOn = true;
                        
            FindMonuments();           
        }

        private void Unload()
        {
            for (int i = 0; i < radiationZones.Count; i++)            
                UnityEngine.Object.Destroy(radiationZones[i]);            
            radiationZones.Clear();

            DestroyAllComponents();

            if (!radsOn)
                ConVar.Server.radiation = false;
        }
        #endregion
      
        #region Functions
        private void DestroyAllComponents()
        {
            var components = UnityEngine.Object.FindObjectsOfType<RadiationZone>();
            if (components != null)
                foreach (var comp in components)
                    UnityEngine.Object.Destroy(comp);
        }

        private void FindMonuments()
        {
            if (configData.Settings.IsHapis)
            {
                CreateHapis();
                return;
            }

            var allobjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            foreach (var gobject in allobjects)
            {
                if (gobject.name.Contains("autospawn/monument"))
                {
                    var pos = gobject.transform.position;

                    if (gobject.name.Contains("lighthouse"))
                    {
                        if (configData.Zones.Lighthouse.Activate)
                            CreateZone(configData.Zones.Lighthouse, pos);
                        continue;
                    }

                    if (gobject.name.Contains("powerplant_1"))
                    {
                        if (configData.Zones.Powerplant.Activate)
                            CreateZone(configData.Zones.Powerplant, pos);
                        continue;
                    }

                    if (gobject.name.Contains("military_tunnel_1"))
                    {
                        if (configData.Zones.Tunnels.Activate)
                            CreateZone(configData.Zones.Tunnels, pos);
                        continue;
                    }

                    if (gobject.name.Contains("harbor_1"))
                    {
                        if (configData.Zones.LargeHarbor.Activate)
                            CreateZone(configData.Zones.LargeHarbor, pos);
                        continue;
                    }

                    if (gobject.name.Contains("harbor_2"))
                    {
                        if (configData.Zones.SmallHarbor.Activate)
                            CreateZone(configData.Zones.SmallHarbor, pos);
                        continue;
                    }

                    if (gobject.name.Contains("airfield_1"))
                    {
                        if (configData.Zones.Airfield.Activate)
                            CreateZone(configData.Zones.Airfield, pos);
                        continue;
                    }

                    if (gobject.name.Contains("trainyard_1"))
                    {
                        if (configData.Zones.Trainyard.Activate)
                            CreateZone(configData.Zones.Trainyard, pos);
                        continue;
                    }

                    if (gobject.name.Contains("water_treatment_plant_1"))
                    {
                        if (configData.Zones.WaterTreatment.Activate)
                            CreateZone(configData.Zones.WaterTreatment, pos);
                        continue;
                    }

                    if (gobject.name.Contains("warehouse"))
                    {
                        if (configData.Zones.Warehouse.Activate)
                            CreateZone(configData.Zones.Warehouse, pos);
                        continue;
                    }

                    if (gobject.name.Contains("satellite_dish"))
                    {

                        if (configData.Zones.Satellite.Activate)
                            CreateZone(configData.Zones.Satellite, pos);
                        continue;
                    }

                    if (gobject.name.Contains("sphere_tank"))
                    {
                        if (configData.Zones.Dome.Activate)
                            CreateZone(configData.Zones.Dome, pos);
                        continue;
                    }

                    if (gobject.name.Contains("radtown_small_3"))
                    {
                        if (configData.Zones.Radtown.Activate)
                            CreateZone(configData.Zones.Radtown, pos);
                        continue;
                    }

                    if (gobject.name.Contains("launch_site_1"))
                    {
                        if (configData.Zones.RocketFactory.Activate)
                        {
                            CreateZone(configData.Zones.RocketFactory, pos + -(gobject.transform.right * 80));
                            CreateZone(configData.Zones.RocketFactory, pos + gobject.transform.right * 150);
                        }
                        continue;
                    }

                    if (gobject.name.Contains("gas_station_1"))
                    {
                        if (configData.Zones.GasStation.Activate)
                            CreateZone(configData.Zones.GasStation, pos);
                        continue;
                    }

                    if (gobject.name.Contains("supermarket_1"))
                    {
                        if (configData.Zones.Supermarket.Activate)
                            CreateZone(configData.Zones.Supermarket, pos);
                        continue;
                    }

                    if (gobject.name.Contains("mining_quarry_c"))
                    {
                        if (configData.Zones.Quarry_HQM.Activate)
                            CreateZone(configData.Zones.Quarry_HQM, pos);                       
                        continue;
                    }

                    if (gobject.name.Contains("mining_quarry_a"))
                    {
                        if (configData.Zones.Quarry_Sulfur.Activate)
                            CreateZone(configData.Zones.Quarry_Sulfur, pos);
                        continue;
                    }

                    if (gobject.name.Contains("mining_quarry_b"))
                    {
                        if (configData.Zones.Quarry_Stone.Activate)
                            CreateZone(configData.Zones.Quarry_Stone, pos);
                        continue;
                    }

                    if (gobject.name.Contains("junkyard_1"))
                    {
                        if (configData.Zones.Junkyard.Activate)
                            CreateZone(configData.Zones.Junkyard, pos);
                        continue;
                    }
                }                
            }
            ConfirmCreation();
        }

        private void CreateHapis()
        {
            if (configData.Zones.Lighthouse.Activate)
            {
                CreateZone(new ConfigData.RadZones.MonumentSettings() { Name = "Lighthouse", Radiation = configData.Zones.Lighthouse.Radiation, Radius = HIMon["lighthouse_1"].Radius }, HIMon["lighthouse_1"].Position);
                CreateZone(new ConfigData.RadZones.MonumentSettings() { Name = "Lighthouse", Radiation = configData.Zones.Lighthouse.Radiation, Radius = HIMon["lighthouse_2"].Radius }, HIMon["lighthouse_2"].Position);
            }
            if (configData.Zones.WaterTreatment.Activate) CreateZone(new ConfigData.RadZones.MonumentSettings() { Name = "WaterTreatment", Radiation = configData.Zones.WaterTreatment.Radiation, Radius = HIMon["water"].Radius }, HIMon["water"].Position);
            if (configData.Zones.Tunnels.Activate) CreateZone(new ConfigData.RadZones.MonumentSettings() { Name = "Tunnels", Radiation = configData.Zones.Tunnels.Radiation, Radius = HIMon["tunnels"].Radius }, HIMon["tunnels"].Position);
            if (configData.Zones.Satellite.Activate) CreateZone(new ConfigData.RadZones.MonumentSettings() { Name = "Satellite", Radiation = configData.Zones.Satellite.Radiation, Radius = HIMon["satellite"].Radius }, HIMon["satellite"].Position);
            ConfirmCreation();
        }

        private void ConfirmCreation()
        {
            if (radiationZones.Count > 0)
            {
                if (configData.Settings.UseTimers) StartRadTimers();
                Puts("Created " + radiationZones.Count + " monument radiation zones");
                if (!ConVar.Server.radiation)
                {
                    radsOn = false;
                    ConVar.Server.radiation = true;
                }
            }
        }

        private void CreateZone(ConfigData.RadZones.MonumentSettings zone, Vector3 pos)
        {           
            var newZone = new GameObject().AddComponent<RadiationZone>();
            newZone.InitializeRadiationZone(zone.Name, pos, zone.Radius, zone.Radiation);
            radiationZones.Add(newZone);
        }      
        
        private void StartRadTimers()
        {
            int ontime = configData.Timers.StaticOn;
            int offtime = configData.Timers.StaticOff;
            if (configData.Settings.UseRandomTimers)
            {
                ontime = GetRandom(configData.Timers.ROnMin, configData.Timers.ROnmax);
                offtime = GetRandom(configData.Timers.ROffMin, configData.Timers.ROffMax);
            }

            onTimer = ontime * 60;
            timer.Repeat(1, onTimer, () =>
            {
                onTimer--;
                if (onTimer == 0)
                {                    
                    foreach (var zone in radiationZones)
                        zone.Deactivate();

                    if (configData.Settings.Infopanel)
                        ConVar.Server.radiation = false;

                    if (configData.Settings.ShowTimers)                    
                        PrintToChat(string.Format(msg("RadiationDisabled"), offtime));
                    
                    offTimer = offtime * 60;
                    timer.Repeat(1, offTimer, () =>
                    {
                        offTimer--;
                        if (offTimer == 0)
                        {
                            foreach (var zone in radiationZones)
                                zone.Reactivate();

                            if (configData.Settings.Infopanel)
                                ConVar.Server.radiation = true;

                            if (configData.Settings.ShowTimers)
                                PrintToChat(string.Format(msg("RadiationEnabled"), ontime));
                            
                            StartRadTimers();
                        }
                    });
                }
            });
        }

        private int GetRandom(int min, int max) => UnityEngine.Random.Range(min, max);

        private void SendEchoConsole(Network.Connection cn, string msg)
        {
            if (Network.Net.sv.IsConnected())
            {
                Network.Net.sv.write.Start();
                Network.Net.sv.write.PacketID(Network.Message.Type.ConsoleMessage);
                Network.Net.sv.write.String(msg);
                Network.Net.sv.write.Send(new Network.SendInfo(cn));
            }
        }
        #endregion

        #region Commands   
        private bool IsAdmin(BasePlayer player)
        {
            if (!player.IsAdmin)
            {
                SendReply(player, msg("Title", player.userID) + msg("NoPermission", player.userID));
                return false;
            }
            return true;
        }
        private bool IsAuthed(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                if (arg.Connection.authLevel < 1)
                {
                    SendReply(arg, "You have insufficient permission to use this command");
                    return false;
                }
            }
            return true;
        }                

        [ConsoleCommand("mr_list")]
        private void ccmdRadZoneList(ConsoleSystem.Arg arg)
        {
            if (!IsAuthed(arg)) return;

            if (radiationZones.Count == 0)
            {
                SendReply(arg, "There are no radiation zones setup");
                return;
            }

            for (int i = 0; i < radiationZones.Count; i++)
            {
                if (i == 0)
                    SendReply(arg, $"---- MonumentRadiation Zone List ----");

                RadiationZone zone = radiationZones[i];
                SendReply(arg, $"{zone.name} || Location: {zone.transform.position} || Radius: {zone.radius} || Radiation Amount: {zone.amount}");
            }
        }

        [ChatCommand("mr_list")]
        private void cmdRadZoneList(BasePlayer player, string command, string[] args)
        {
            if (!IsAdmin(player))
                return;

            if (radiationZones.Count == 0)
            {
                SendReply(player, msg("NoRadiationZones", player.userID));
                return;
            }

            for (int i = 0; i < radiationZones.Count; i++)
            {
                if (i == 0)
                    SendEchoConsole(player.net.connection, $"---- MonumentRadiation Zone List ----");

                RadiationZone zone = radiationZones[i];
                SendEchoConsole(player.net.connection, $"{zone.name} || Location: {zone.transform.position} || Radius: {zone.radius} || Radiation Amount: {zone.amount}");
            } 
            SendReply(player, msg("Title", player.userID) + msg("InfoToConsole", player.userID));
        }

        [ChatCommand("mr")]
        private void cmdCheckTimers(BasePlayer player, string command, string[] args)
        {
            if (onTimer != 0)
            {
                float timeOn = onTimer / 60 < 1 ? onTimer : onTimer / 60;
                string type = onTimer / 60 < 1 ? msg("Seconds") : msg("Minutes");
                
                SendReply(player, string.Format(msg("RadiationDownIn", player.userID), timeOn, type));
            }
            else if (offTimer != 0)
            {
                float timeOff = offTimer / 60 < 1 ? offTimer : offTimer / 60;
                string type = offTimer / 60 < 1 ? msg("Seconds") : msg("Minutes");
               
                SendReply(player, string.Format(msg("RadiationUpIn", player.userID), timeOff, type));
            }
        }
        [ChatCommand("mr_show")]
        private void cmdShowZones(BasePlayer player, string command, string[] args)
        {
            if (!IsAdmin(player)) return;
            foreach(var zone in radiationZones)            
                player.SendConsoleCommand("ddraw.sphere", 20f, Color.blue, zone.transform.position, zone.radius);            
        }
        #endregion

        #region Classes        
        private class RadiationZone : MonoBehaviour
        {
            private TriggerRadiation rads;
            public float radius;
            public float amount;

            private void Awake()
            {
                gameObject.layer = (int)Rust.Layer.Reserved1;
                enabled = false;
            }

            private void OnDestroy() => Destroy(gameObject);
            
            private void OnTriggerEnter(Collider obj)
            {
                BasePlayer player = obj?.GetComponentInParent<BasePlayer>();
                if (player != null)
                {
                    if (ins.configData.Messages.Enter && ConVar.Server.radiation)  
                        player.ChatMessage(ins.msg("EnterRadiation", player.userID));                    
                }
            }

            private void OnTriggerExit(Collider obj)
            {
                BasePlayer player = obj?.GetComponentInParent<BasePlayer>();
                if (player != null)
                {
                    if (ins.configData.Messages.Exit && ConVar.Server.radiation)                                            
                        player.ChatMessage(ins.msg("LeaveRadiation", player.userID));                    
                }
            }

            public void InitializeRadiationZone(string type, Vector3 position, float radius, float amount)
            {
                this.radius = radius;
                this.amount = amount;

                gameObject.name = type;
                transform.position = position;
                transform.rotation = new Quaternion();
                UpdateCollider();

                rads = gameObject.AddComponent<TriggerRadiation>();
                rads.RadiationAmountOverride = amount;
                rads.radiationSize = radius;
                rads.interestLayers = LayerMask.GetMask("Player (Server)");
                rads.enabled = true;
            }

            public void Deactivate() => rads.gameObject.SetActive(false);

            public void Reactivate() => rads.gameObject.SetActive(true);

            private void UpdateCollider()
            {
                var sphereCollider = gameObject.GetComponent<SphereCollider>() ?? gameObject.AddComponent<SphereCollider>();
                sphereCollider.isTrigger = true;
                sphereCollider.radius = radius;
            }
        }

        private class HapisIslandMonuments
        {
            public Vector3 Position;
            public float Radius;
        }

        private Dictionary<string, HapisIslandMonuments> HIMon = new Dictionary<string, HapisIslandMonuments>
        {
            {"lighthouse_1", new HapisIslandMonuments {Position = new Vector3(1562.30981f, 45.05141f, 1140.29382f), Radius = 15 } },
            {"lighthouse_2", new HapisIslandMonuments {Position = new Vector3(-1526.65112f, 45.3333473f, -280.0514f), Radius = 15 } },
            {"water", new HapisIslandMonuments {Position = new Vector3(-1065.191f, 125.3655f, 439.2279f), Radius = 100 } },
            {"tunnels", new HapisIslandMonuments {Position = new Vector3(-854.7694f, 72.34925f, -241.692f), Radius = 100 } },
            {"satellite", new HapisIslandMonuments {Position = new Vector3(205.2501f, 247.8247f, 252.5204f), Radius = 80 } }
        };
        #endregion

        #region Config      
        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Messaging Settings")]
            public Messaging Messages { get; set; }
            public RadiationTimers Timers { get; set; }
            public Options Settings { get; set; }
            [JsonProperty(PropertyName = "Zone Settings")]
            public RadZones Zones { get; set; }

            public class Messaging
            {
                [JsonProperty(PropertyName = "Display message to player when they enter a radiation zone")]
                public bool Enter { get; set; }
                [JsonProperty(PropertyName = "Display message to player when they leave a radiation zone")]
                public bool Exit { get; set; }
            }
            public class RadiationTimers
            {
                [JsonProperty(PropertyName = "Random on time (minimum minutes)")]
                public int ROnMin { get; set; }
                [JsonProperty(PropertyName = "Random on time (maximum minutes)")]
                public int ROnmax { get; set; }
                [JsonProperty(PropertyName = "Random off time (minimum minutes)")]
                public int ROffMin { get; set; }
                [JsonProperty(PropertyName = "Random off time (maximum minutes)")]
                public int ROffMax { get; set; }
                [JsonProperty(PropertyName = "Forced off time (minutes)")]
                public int StaticOff { get; set; }
                [JsonProperty(PropertyName = "Forced on time (minutes)")]
                public int StaticOn { get; set; }
            }
            public class RadZones
            {
                public MonumentSettings Airfield { get; set; }
                public MonumentSettings Dome { get; set; }
                public MonumentSettings Junkyard { get; set; }
                public MonumentSettings Lighthouse { get; set; }
                public MonumentSettings LargeHarbor { get; set; }
                public MonumentSettings GasStation { get; set; }
                public MonumentSettings Powerplant { get; set; }
                [JsonProperty(PropertyName = "Stone Quarry")]
                public MonumentSettings Quarry_Stone { get; set; }
                [JsonProperty(PropertyName = "Sulfur Quarry")]
                public MonumentSettings Quarry_Sulfur { get; set; }
                [JsonProperty(PropertyName = "HQM Quarry")]
                public MonumentSettings Quarry_HQM { get; set; }
                public MonumentSettings Radtown { get; set; }
                public MonumentSettings RocketFactory { get; set; }
                public MonumentSettings Satellite { get; set; }
                public MonumentSettings SmallHarbor { get; set; }
                public MonumentSettings Supermarket { get; set; }
                public MonumentSettings Trainyard { get; set; }
                public MonumentSettings Tunnels { get; set; }
                public MonumentSettings Warehouse { get; set; }
                public MonumentSettings WaterTreatment { get; set; }

                public class MonumentSettings
                {
                    [JsonProperty(PropertyName = "Enable radiation at this monument")]
                    public bool Activate;
                    [JsonProperty(PropertyName = "Monument name (internal use)")]
                    public string Name;
                    [JsonProperty(PropertyName = "Radius of radiation")]
                    public float Radius;
                    [JsonProperty(PropertyName = "Radiation amount")]
                    public float Radiation;
                }
            }
            public class Options
            {
                [JsonProperty(PropertyName = "Broadcast radiation status changes")]
                public bool ShowTimers { get; set; }
                [JsonProperty(PropertyName = "Using Hapis Island map")]
                public bool IsHapis { get; set; }
                [JsonProperty(PropertyName = "Enable InfoPanel integration")]
                public bool Infopanel { get; set; }
                [JsonProperty(PropertyName = "Use radiation toggle timers")]
                public bool UseTimers { get; set; }
                [JsonProperty(PropertyName = "Randomise radiation timers")]
                public bool UseRandomTimers { get; set; }
            }
            public Oxide.Core.VersionNumber Version { get; set; }
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                Messages = new ConfigData.Messaging
                {
                    Enter = true,
                    Exit = false
                },
                Timers = new ConfigData.RadiationTimers
                {
                    ROffMax = 60,
                    ROffMin = 25,
                    ROnmax = 30,
                    ROnMin = 5,
                    StaticOff = 15,
                    StaticOn = 45
                },
                Settings = new ConfigData.Options
                {
                    ShowTimers = true,
                    UseRandomTimers = false,
                    UseTimers = true,
                    IsHapis = false,
                    Infopanel = false
                },
                Zones = new ConfigData.RadZones
                {
                    Airfield = new ConfigData.RadZones.MonumentSettings
                    {
                        Activate = false,
                        Name = "Airfield",
                        Radiation = 10,
                        Radius = 85
                    },
                    Dome = new ConfigData.RadZones.MonumentSettings
                    {
                        Activate = false,
                        Name = "Dome",
                        Radiation = 10,
                        Radius = 50
                    },
                    Junkyard = new ConfigData.RadZones.MonumentSettings
                    {
                        Activate = false,
                        Name = "Junkyard",
                        Radiation = 10,
                        Radius = 50
                    },
                    GasStation = new ConfigData.RadZones.MonumentSettings
                    {
                        Activate = false,
                        Name = "GasStation",
                        Radiation = 10,
                        Radius = 15
                    },
                    LargeHarbor = new ConfigData.RadZones.MonumentSettings
                    {
                        Activate = false,
                        Name = "Large Harbor",
                        Radiation = 10,
                        Radius = 120
                    },
                    Lighthouse = new ConfigData.RadZones.MonumentSettings
                    {
                        Activate = false,
                        Name = "Lighthouse",
                        Radiation = 10,
                        Radius = 15
                    },
                    Powerplant = new ConfigData.RadZones.MonumentSettings
                    {
                        Activate = false,
                        Name = "Powerplant",
                        Radiation = 10,
                        Radius = 120
                    },
                    Quarry_HQM = new ConfigData.RadZones.MonumentSettings
                    {
                        Activate = false,
                        Name = "Quarry_HQM",
                        Radiation = 10,
                        Radius = 15
                    },
                    Quarry_Stone = new ConfigData.RadZones.MonumentSettings
                    {
                        Activate = false,
                        Name = "Quarry_Stone",
                        Radiation = 10,
                        Radius = 15
                    },
                    Quarry_Sulfur = new ConfigData.RadZones.MonumentSettings
                    {
                        Activate = false,
                        Name = "Quarry_Sulfur",
                        Radiation = 10,
                        Radius = 15
                    },
                    Radtown = new ConfigData.RadZones.MonumentSettings
                    {
                        Activate = true,
                        Name = "Radtown",
                        Radiation = 10,
                        Radius = 85
                    },
                    RocketFactory = new ConfigData.RadZones.MonumentSettings
                    {
                        Activate = true,
                        Name = "Rocket Factory",
                        Radiation = 10,
                        Radius = 140
                    },
                    Satellite = new ConfigData.RadZones.MonumentSettings
                    {
                        Activate = false,
                        Name = "Satellite",
                        Radiation = 10,
                        Radius = 60
                    },
                    SmallHarbor = new ConfigData.RadZones.MonumentSettings
                    {
                        Activate = true,
                        Name = "Small Harbor",
                        Radiation = 10,
                        Radius = 85
                    },
                    Supermarket = new ConfigData.RadZones.MonumentSettings
                    {
                        Activate = false,
                        Name = "Supermarket",
                        Radiation = 10,
                        Radius = 20
                    },
                    Trainyard = new ConfigData.RadZones.MonumentSettings
                    {
                        Activate = false,
                        Name = "Trainyard",
                        Radiation = 10,
                        Radius = 100
                    },
                    Tunnels = new ConfigData.RadZones.MonumentSettings
                    {
                        Activate = false,
                        Name = "Tunnels",
                        Radiation = 10,
                        Radius = 90
                    },
                    Warehouse = new ConfigData.RadZones.MonumentSettings
                    {
                        Activate = false,
                        Name = "Warehouse",
                        Radiation = 10,
                        Radius = 15
                    },
                    WaterTreatment = new ConfigData.RadZones.MonumentSettings
                    {
                        Activate = false,
                        Name = "WaterTreatment",
                        Radiation = 10,
                        Radius = 120
                    }
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new Core.VersionNumber(0, 1, 50))
            {
                configData.Zones.Junkyard = baseConfig.Zones.Junkyard;
                configData.Zones.Quarry_HQM = baseConfig.Zones.Quarry_HQM;
                configData.Zones.Quarry_Stone = baseConfig.Zones.Quarry_Stone;
                configData.Zones.Quarry_Sulfur = baseConfig.Zones.Quarry_Sulfur;
            }
            configData.Version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion

        #region Localization      
        private string msg(string key, ulong playerId = 0U) => lang.GetMessage(key, this, playerId == 0U ? null : playerId.ToString());

        Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            ["InfoToConsole"] = "<color=#B6B6B6>Check your ingame console for more information!</color>",
            ["Title"] = "<color=#B6B6B6><color=#ce422b>Monument Radiation</color> : </color>",
            ["NoRadiationZones"] = "<color=#B6B6B6>There are no radiation zones setup</color>",
            ["Minutes"] = "minutes",
            ["Seconds"] = "seconds",
            ["RadiationDownIn"] = "<color=#B6B6B6>Monument radiation levels will be down in <color=#00FF00>{0} {1}</color>!</color>",
            ["RadiationUpIn"] = "<color=#B6B6B6>Monument radiation levels will be back up in <color=#00FF00>{0} {1}</color>!</color>",
            ["RadiationEnabled"] = "<color=#B6B6B6>Monument radiation levels are back up for <color=#00FF00>{0} minutes</color>!</color>",
            ["RadiationDisabled"] = "<color=#B6B6B6>Monument radiation levels are down for <color=#00FF00>{0} minutes</color>!</color>",
            ["EnterRadiation"] = "<color=#ce422b>WARNING: </color><color=#B6B6B6>You are entering a irradiated area!</color>",
            ["LeaveRadiation"] = "<color=#ce422b>CAUTION: </color><color=#B6B6B6>You are leaving a irradiated area! </color>",
            ["NoPermission"] = "<color=#B6B6B6>You have insufficient permission to use this command</color>",
        };
        #endregion


    }
}