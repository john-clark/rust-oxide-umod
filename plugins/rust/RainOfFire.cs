using System.Collections.Generic;
using Oxide.Core.Plugins;
using UnityEngine;
using Rust;

namespace Oxide.Plugins
{
    [Info("RainOfFire", "emu / k1lly0u", "0.2.52")]
    [Description("Simulate a meteor strike using rockets falling from the sky")]
    class RainOfFire : RustPlugin
    {
        #region Fields
        [PluginReference]
        Plugin PopupNotifications;

        private Timer EventTimer = null;
        private List<Timer> RocketTimers = new List<Timer>(); 
        #endregion

        #region Oxide Hooks       
        private void OnServerInitialized()
        {
            lang.RegisterMessages(Messages, this);
            LoadVariables();
            StartEventTimer();
        }  
        
        private void Unload()
        {
            StopTimer();
            foreach (var t in RocketTimers)
                t.Destroy();
            var objects = UnityEngine.Object.FindObjectsOfType<ItemCarrier>();
            if (objects != null)
                foreach (var gameObj in objects)
                    UnityEngine.Object.Destroy(gameObj);
        }
        #endregion

        #region Functions
        private void StartEventTimer()
        {
            if (configData.Options.EnableAutomaticEvents)
            {
                if (configData.Options.EventTimers.UseRandomTimer)
                {
                    var random = RandomRange(configData.Options.EventTimers.RandomTimerMin, configData.Options.EventTimers.RandomTimerMax);
                    EventTimer = timer.Once(random * 60, () => { StartRandomOnMap(); StartEventTimer(); });
                }
                else EventTimer = timer.Repeat(configData.Options.EventTimers.EventInterval * 60, 0, () => StartRandomOnMap());
            }
        }
        private void StopTimer()
        {
            if (EventTimer != null)
                EventTimer.Destroy();
        }

        private void StartRandomOnMap()
        {
            float mapsize = (TerrainMeta.Size.x / 2) - 600f;

            float randomX = UnityEngine.Random.Range(-mapsize, mapsize);
            float randomY = UnityEngine.Random.Range(-mapsize, mapsize);

            Vector3 callAt = new Vector3(randomX, 0f, randomY);

            StartRainOfFire(callAt, configData.z_IntensitySettings.Settings_Optimal);
        }
        private bool StartOnPlayer(string playerName, ConfigData.Settings setting)
        {
            BasePlayer player = GetPlayerByName(playerName);

            if (player == null)
                return false;

            StartRainOfFire(player.transform.position, setting);
            return true;
        }
        private void StartBarrage(Vector3 origin, Vector3 direction) => timer.Repeat(configData.BarrageSettings.RocketDelay, configData.BarrageSettings.NumberOfRockets, () => SpreadRocket(origin, direction));

        private void StartRainOfFire(Vector3 origin, ConfigData.Settings setting)
        {
            float radius = setting.Radius;
            int numberOfRockets = setting.RocketAmount;
            float duration = setting.Duration;
            bool dropsItems = setting.ItemDropControl.EnableItemDrop;
            ItemDrop[] itemDrops = setting.ItemDropControl.ItemsToDrop;

            float intervals = duration / numberOfRockets;

            if (configData.Options.NotifyEvent)
            {
                if (PopupNotifications)
                    PopupNotifications.Call("CreatePopupNotification", msg("incoming"));
                else PrintToChat(msg("incoming"));
            }

            timer.Repeat(intervals, numberOfRockets, () => RandomRocket(origin, radius, setting));
        }      

        private void RandomRocket(Vector3 origin, float radius, ConfigData.Settings setting)
        {
            bool isFireRocket = false;
            Vector2 rand = UnityEngine.Random.insideUnitCircle;
            Vector3 offset = new Vector3(rand.x * radius, 0, rand.y * radius);

            Vector3 direction = (Vector3.up * -2.0f + Vector3.right).normalized;
            Vector3 launchPos = origin + offset - direction * 200;

            if (RandomRange(1, setting.FireRocketChance) == 1)
                isFireRocket = true;

            BaseEntity rocket = CreateRocket(launchPos, direction, isFireRocket);
            if (setting.ItemDropControl.EnableItemDrop)
            {
                var comp = rocket.gameObject.AddComponent<ItemCarrier>();
                comp.SetCarriedItems(setting.ItemDropControl.ItemsToDrop);
                comp.SetDropMultiplier(configData.Options.GlobalDropMultiplier);
            }
        }

        private void SpreadRocket(Vector3 origin, Vector3 direction)
        {
            var barrageSpread = configData.BarrageSettings.RocketSpread;
            direction = Quaternion.Euler(UnityEngine.Random.Range((float)(-(double)barrageSpread * 0.5), barrageSpread * 0.5f), UnityEngine.Random.Range((float)(-(double)barrageSpread * 0.5), barrageSpread * 0.5f), UnityEngine.Random.Range((float)(-(double)barrageSpread * 0.5), barrageSpread * 0.5f)) * direction;
            CreateRocket(origin, direction, false);
        }

        private BaseEntity CreateRocket(Vector3 startPoint, Vector3 direction, bool isFireRocket)
        {
            ItemDefinition projectileItem;
         
            if (isFireRocket)
                projectileItem = ItemManager.FindItemDefinition("ammo.rocket.fire");
            else projectileItem = ItemManager.FindItemDefinition("ammo.rocket.basic");

            ItemModProjectile component = projectileItem.GetComponent<ItemModProjectile>();
            BaseEntity entity = GameManager.server.CreateEntity(component.projectileObject.resourcePath, startPoint, new Quaternion(), true);

            TimedExplosive timedExplosive = entity.GetComponent<TimedExplosive>();
            ServerProjectile serverProjectile = entity.GetComponent<ServerProjectile>();

            serverProjectile.gravityModifier = 0;
            serverProjectile.speed = 25;
            timedExplosive.timerAmountMin = 300;
            timedExplosive.timerAmountMax = 300;
            ScaleAllDamage(timedExplosive.damageTypes, configData.DamageControl.DamageMultiplier);

            serverProjectile.InitializeVelocity(direction.normalized * 25);
            entity.Spawn();
            return entity;
        }

        private void ScaleAllDamage(List<DamageTypeEntry> damageTypes, float scale)
        {
            for (int i = 0; i < damageTypes.Count; i++)
            {
                damageTypes[i].amount *= scale;
            }
        }
        #endregion

        #region Config editing
        private void SetIntervals(int intervals)
        {
            StopTimer();

            configData.Options.EventTimers.EventInterval = intervals;
            SaveConfig(configData);

            StartEventTimer();
        }
        private void SetDamageMult(float scale)
        {
            configData.DamageControl.DamageMultiplier = scale;
            SaveConfig(configData);
        }
        private void SetNotifyEvent(bool notify)
        {
            configData.Options.NotifyEvent = notify;
            SaveConfig(configData);
        }
        private void SetDropRate(float rate)
        {
            configData.Options.GlobalDropMultiplier = rate;
            SaveConfig(configData);
        }
        #endregion

        #region Commands
        [ChatCommand("rof")]
        private void cmdROF(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin || args.Length == 0)
            {
                SendReply(player, msg("help1", player.UserIDString));
                SendReply(player, msg("help2", player.UserIDString));
                SendReply(player, msg("help3", player.UserIDString));
                SendReply(player, msg("help4", player.UserIDString));
                SendReply(player, msg("help5", player.UserIDString));
                SendReply(player, msg("help6", player.UserIDString));
                SendReply(player, msg("help7", player.UserIDString));
                SendReply(player, msg("help8", player.UserIDString));
                return;
            }
                

            switch (args[0].ToLower())
            {
                case "onplayer":
                    if (args.Length == 2)
                    {
                        if (StartOnPlayer(args[1], configData.z_IntensitySettings.Settings_Optimal))
                            SendReply(player, string.Format(msg("calledOn", player.UserIDString), args[1]));
                        else
                            SendReply(player, msg("noPlayer", player.UserIDString));
                    }
                    else
                    {
                        StartRainOfFire(player.transform.position, configData.z_IntensitySettings.Settings_Optimal);
                        SendReply(player, msg("onPos", player.UserIDString));
                    }
                    break;

                case "onplayer_extreme":
                    if (args.Length == 2)
                    {
                        if (StartOnPlayer(args[1], configData.z_IntensitySettings.Settings_Extreme))
                            SendReply(player, msg("Extreme", player.UserIDString) + string.Format(msg("calledOn", player.UserIDString), args[1]));
                        else
                            SendReply(player, msg("noPlayer", player.UserIDString));
                    }
                    else
                    {
                        StartRainOfFire(player.transform.position, configData.z_IntensitySettings.Settings_Extreme);
                        SendReply(player, msg("Extreme", player.UserIDString) + msg("onPos", player.UserIDString));
                    }
                    break;

                case "onplayer_mild":
                    if (args.Length == 2)
                    {
                        if (StartOnPlayer(args[1], configData.z_IntensitySettings.Settings_Mild))
                            SendReply(player, msg("Mild", player.UserIDString) + string.Format(msg("calledOn", player.UserIDString), args[1]));
                        else
                            SendReply(player, msg("noPlayer", player.UserIDString));
                    }
                    else
                    {
                        StartRainOfFire(player.transform.position, configData.z_IntensitySettings.Settings_Mild);
                        SendReply(player, msg("Mild", player.UserIDString) + msg("onPos", player.UserIDString));
                    }
                    break;

                case "barrage":
                    StartBarrage(player.eyes.position + player.eyes.HeadForward() * 1f, player.eyes.HeadForward());
                    break;

                case "random":
                    StartRandomOnMap();
                    SendReply(player, msg("randomCall", player.UserIDString));
                    break;

                case "intervals":
                    if (args.Length > 1)
                    {
                        int newIntervals;
                        bool isValid;
                        isValid = int.TryParse(args[1], out newIntervals);

                        if (isValid)
                        {
                            if (newIntervals >= 4 || newIntervals == 0)
                            {
                                SetIntervals(newIntervals);
                                SendReply(player, string.Format(msg("interSet", player.UserIDString), newIntervals));
                                StopTimer();
                                StartEventTimer();
                            }
                            else
                            {
                                SendReply(player, msg("smallInter", player.UserIDString));
                            }
                        }
                        else
                        {
                            SendReply(player, string.Format(msg("invalidParam", player.UserIDString), args[1]));
                        }
                    }
                    break;
                case "droprate":
                    if (args.Length > 1)
                    {
                        float newDropMultiplier;
                        bool isValid;
                        isValid = float.TryParse(args[1], out newDropMultiplier);
                        if (isValid)
                        {
                            SetDropRate(newDropMultiplier);
                            SendReply(player, msg("dropMulti", player.UserIDString) + newDropMultiplier);
                        }
                        else
                        {
                            SendReply(player, string.Format(msg("invalidParam", player.UserIDString), args[1]));
                        }
                    }
                    break;
                case "damagescale":
                    if (args.Length > 1)
                    {
                        float newDamageMultiplier;
                        bool isValid;
                        isValid = float.TryParse(args[1], out newDamageMultiplier);

                        if (isValid)
                        {
                            SetDamageMult(newDamageMultiplier);
                            SendReply(player, msg("damageMulti", player.UserIDString) + newDamageMultiplier);
                        }
                        else
                        {
                            SendReply(player, string.Format(msg("invalidParam", player.UserIDString), args[1]));
                        }
                    }
                    break;

                case "togglemsg":
                    if (configData.Options.NotifyEvent)
                    {
                        SetNotifyEvent(false);
                        SendReply(player, msg("notifDeAct", player.UserIDString));
                    }
                    else
                    {
                        SetNotifyEvent(true);
                        SendReply(player, msg("notifAct", player.UserIDString));
                    }                    
                    break;

                default:
                    SendReply(player, string.Format(msg("unknown", player.UserIDString), args[0]));
                    break;
            }
        }

        [ConsoleCommand("rof.random")]
        private void ccmdEventRandom(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
                return;

            StartRandomOnMap();
            Puts("Random event started");
        }

        [ConsoleCommand("rof.onposition")]
        private void ccmdEventOnPosition(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
                return;

            float x, z;

            if (arg.Args.Length == 2 && float.TryParse(arg.Args[0], out x) && float.TryParse(arg.Args[1], out z))
            {
                var position = new Vector3(x, 0, z);
                StartRainOfFire(GetGroundPosition(position), configData.z_IntensitySettings.Settings_Optimal);
                Puts($"Random event started on position {x}, {position.y}, {z}");
            }
            else
                Puts("Usage: rof.onposition x z");
        }
        #endregion

        #region Helpers
        private BasePlayer GetPlayerByName(string name)
        {
            string currentName;
            string lastName;
            BasePlayer foundPlayer = null;
            name = name.ToLower();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                currentName = player.displayName.ToLower();

                if (currentName.Contains(name))
                {
                    if (foundPlayer != null)
                    {
                        lastName = foundPlayer.displayName;
                        if (currentName.Replace(name, "").Length < lastName.Replace(name, "").Length)
                        {
                            foundPlayer = player;
                        }
                    }

                    foundPlayer = player;
                }
            }

            return foundPlayer;
        }

        private static int RandomRange(int min, int max) => UnityEngine.Random.Range(min, max);
       
        private Vector3 GetGroundPosition(Vector3 sourcePos)
        {
            RaycastHit hitInfo;

            if (Physics.Raycast(sourcePos, Vector3.down, out hitInfo, LayerMask.GetMask("Terrain", "World", "Construction")))
            {
                sourcePos.y = hitInfo.point.y;
            }
            sourcePos.y = Mathf.Max(sourcePos.y, TerrainMeta.HeightMap.GetHeight(sourcePos));
            return sourcePos;
        }        
        #endregion

        #region Classes 
        private class ItemCarrier : MonoBehaviour
        {
            private ItemDrop[] carriedItems = null;

            private float multiplier;

            public void SetCarriedItems(ItemDrop[] carriedItems) => this.carriedItems = carriedItems;

            public void SetDropMultiplier(float multiplier) => this.multiplier = multiplier;

            private void OnDestroy()
            {
                if (carriedItems == null)
                    return;

                int amount;

                for (int i = 0; i < carriedItems.Length; i++)
                {
                    if ((amount = (int)(RandomRange(carriedItems[i].Minimum, carriedItems[i].Maximum) * multiplier)) > 0)
                        ItemManager.CreateByName(carriedItems[i].Shortname, amount).Drop(gameObject.transform.position, Vector3.up);
                }
            }           
        }

        private class ItemDrop
        {
            public string Shortname { get; set; }
            public int Minimum { get; set; }
            public int Maximum { get; set; }
        }
        #endregion

        #region Config        
        private ConfigData configData;
        
        class ConfigData
        {
            public BarrageOptions BarrageSettings { get; set; }
            public DamageOptions DamageControl { get; set; }
            public ConfigOptions Options { get; set; }
            public IntensityOptions z_IntensitySettings { get; set; }

            public class DamageOptions
            {
                public float DamageMultiplier { get; set; }
            }

            public class BarrageOptions
            {
                public int NumberOfRockets { get; set; }
                public float RocketDelay { get; set; }
                public float RocketSpread { get; set; }
            }

            public class Drops
            {
                public bool EnableItemDrop { get; set; }
                public ItemDrop[] ItemsToDrop { get; set; }
            }

            public class ConfigOptions
            {
                public bool EnableAutomaticEvents { get; set; }
                public Timers EventTimers { get; set; }
                public float GlobalDropMultiplier { get; set; }
                public bool NotifyEvent { get; set; }
            }

            public class Timers
            {
                public int EventInterval { get; set; }
                public bool UseRandomTimer { get; set; }
                public int RandomTimerMin { get; set; }
                public int RandomTimerMax { get; set; }
            }

            public class Settings
            {
                public int FireRocketChance { get; set; }
                public float Radius { get; set; }
                public int RocketAmount { get; set; }
                public int Duration { get; set; }
                public Drops ItemDropControl { get; set; }
            }

            public class IntensityOptions
            {
                public Settings Settings_Mild { get; set; }
                public Settings Settings_Optimal { get; set; }
                public Settings Settings_Extreme { get; set; }
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
                BarrageSettings = new ConfigData.BarrageOptions
                {
                    NumberOfRockets = 20,
                    RocketDelay = 0.33f,
                    RocketSpread = 16f
                },
                DamageControl = new ConfigData.DamageOptions
                {
                    DamageMultiplier = 0.2f,
                },                
                Options = new ConfigData.ConfigOptions
                {
                    EnableAutomaticEvents = true,
                    EventTimers = new ConfigData.Timers
                    {
                        EventInterval = 30,
                        RandomTimerMax = 45,
                        RandomTimerMin = 15,
                        UseRandomTimer = false
                    },
                    GlobalDropMultiplier = 1.0f,
                    NotifyEvent = true
                    
                },
                z_IntensitySettings = new ConfigData.IntensityOptions
                {
                    Settings_Mild = new ConfigData.Settings
                    {
                        FireRocketChance = 30,
                        Radius = 500f,
                        Duration = 240,
                        ItemDropControl = new ConfigData.Drops
                        {
                            EnableItemDrop = true,
                            ItemsToDrop = new ItemDrop[]
                        {
                            new ItemDrop
                            {
                                Maximum = 120,
                                Minimum = 80,
                                Shortname = "stones"
                            },
                            new ItemDrop
                            {
                                Maximum = 50,
                                Minimum = 25,
                                Shortname = "metal.ore"
                            }
                        }
                        },
                        RocketAmount = 20
                    },
                    Settings_Optimal = new ConfigData.Settings
                    {
                        FireRocketChance = 20,
                        Radius = 300f,
                        Duration = 120,
                        ItemDropControl = new ConfigData.Drops
                        {
                            EnableItemDrop = true,
                            ItemsToDrop = new ItemDrop[]
                        {
                            new ItemDrop
                            {
                                Maximum = 250,
                                Minimum = 160,
                                Shortname = "stones"
                            },
                            new ItemDrop
                            {
                                Maximum = 120,
                                Minimum = 60,
                                Shortname = "metal.fragments"
                            },
                            new ItemDrop
                            {
                                Maximum = 50,
                                Minimum = 20,
                                Shortname = "hq.metal.ore"
                            }
                        }
                        },
                        RocketAmount = 45
                    },
                    Settings_Extreme = new ConfigData.Settings
                    {
                        FireRocketChance = 10,
                        Radius = 100f,
                        Duration = 30,
                        ItemDropControl = new ConfigData.Drops
                        {
                            EnableItemDrop = true,
                            ItemsToDrop = new ItemDrop[]
                        {
                            new ItemDrop
                            {
                                Maximum = 400,
                                Minimum = 250,
                                Shortname = "stones"
                            },
                            new ItemDrop
                            {
                                Maximum = 300,
                                Minimum = 125,
                                Shortname = "metal.fragments"
                            },
                            new ItemDrop
                            {
                                Maximum = 50,
                                Minimum = 20,
                                Shortname = "metal.refined"
                            },
                            new ItemDrop
                            {
                                Maximum = 120,
                                Minimum = 45,
                                Shortname = "sulfur.ore"
                            }
                        }
                        },
                        RocketAmount = 70
                    }
                }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Localization
        string msg(string key, string playerId = "") => lang.GetMessage(key, this, playerId);
        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            {"incoming", "Meteor shower incoming" },
            {"help1", "/rof onplayer <opt:playername> - Calls a event on your position, or the player specified"},
            {"help2", "/rof onplayer_extreme <opt:playername> - Starts a extreme event on your position, or the player specified"},
            {"help3", "/rof onplayer_mild <opt:playername> - Starts a optimal event on your position, or the player specified"},
            {"help4", "/rof barrage - Fire a barrage of rockets from your position"},
            {"help5", "/rof random - Calls a event at a random postion"},
            {"help6", "/rof intervals <amount> - Change the time between events"},
            {"help7", "/rof damagescale <amount> - Change the damage scale"},
            {"help8", "/rof togglemsg - Toggle public event broadcast"},
            {"calledOn", "Event called on {0}'s position"},
            {"noPlayer", "No player found with that name"},
            {"onPos", "Event called on your position"},
            {"Extreme", "Extreme"},
            {"Mild", "Mild" },
            {"randomCall", "Event called on random position"},
            {"invalidParam", "Invalid parameter '{0}'"},
            {"smallInter", "Event intervals under 4 minutes are not allowed"},
            {"interSet", "Event intervals set to {0} minutes"},
            {"dropMulti", "Global item drop multiplier set to "},
            {"damageMulti", "Damage scale set to "},
            {"notifDeAct", "Event notification de-activated"},
            {"notifAct", "Event notification activated"},
            {"unknown", "Unknown parameter '{0}'"}
        };
        #endregion

    }
}
