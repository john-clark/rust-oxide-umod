using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Night Lantern", "k1lly0u", "2.0.94", ResourceId = 1182)]
    [Description("Automatically turns ON and OFF lanterns after sunset and sunrise")]
    class NightLantern : RustPlugin
    {
        #region Fields

        [PluginReference]
        private Plugin NoFuelRequirements;
        private static NightLantern ins;

        private Dictionary<ulong, Dictionary<ConsumeType, bool>> toggleList = new Dictionary<ulong, Dictionary<ConsumeType, bool>>();
        private HashSet<LightController> lightControllers = new HashSet<LightController>();

        private bool isInitialized;

        private bool lightsOn = false;
        private bool globalToggle = true;

        private Timer timeCheck;

        #endregion Fields

        #region Oxide Hooks

        private void Loaded()
        {
            permission.RegisterPermission("nightlantern.global", this);
            foreach (var type in Enum.GetValues(typeof(ConsumeType)))
                permission.RegisterPermission($"nightlantern.{type}", this);

            lang.RegisterMessages(Messages, this);
        }

        private void OnServerInitialized()
        {
            ins = this;
            ServerMgr.Instance.StartCoroutine(CreateAllLights(BaseNetworkable.serverEntities.Where(x => x is BaseOven || x is SearchLight)));
            isInitialized = true;
        }

        private void OnConsumeFuel(BaseOven oven, Item fuel, ItemModBurnable burnable)
        {
            if (!isInitialized)
                return;

            LightController lightController = oven.GetComponent<LightController>();
            if (lightController != null)
                lightController.OnConsumeFuel(fuel);
        }

        private void OnItemUse(Item item, int amount)
        {
            if (!isInitialized)
                return;

            LightController lightController = item?.parent?.entityOwner?.GetComponent<LightController>();
            if (lightController != null)
                lightController.OnConsumeFuel(item);
        }

        private void OnEntitySpawned(BaseEntity entity)
        {
            if (!isInitialized)
                return;

            InitializeLightController(entity);
        }

        private void OnEntityKill(BaseNetworkable entity)
        {
            if (!isInitialized)
                return;

            LightController lightController = entity.GetComponent<LightController>();
            if (lightController != null)
            {
                lightControllers.Remove(lightController);
                UnityEngine.Object.DestroyImmediate(lightController);
            }
        }

        private void OnServerSave() => SaveData();

        private void Unload()
        {
            foreach (LightController lightController in lightControllers)
            {
                lightController.ToggleLight(false);
                UnityEngine.Object.DestroyImmediate(lightController);
            }

            if (timeCheck != null)
                timeCheck.Destroy();

            lightControllers.Clear();
            ins = null;
        }

        #endregion Oxide Hooks

        #region Functions

        private IEnumerator CreateAllLights(IEnumerable<BaseNetworkable> entities)
        {
            IEnumerable<BaseNetworkable> baseNetworkables = entities.ToList();
            if (baseNetworkables.Any())
            {
                for (int i = baseNetworkables.Count() - 1; i >= 0; i--)
                {
                    BaseNetworkable entity = baseNetworkables.ElementAt(i);

                    yield return new WaitForSeconds(UnityEngine.Random.Range(0.1f, 0.25f));
                    if (entity == null || entity.IsDestroyed)
                        continue;
                    InitializeLightController(entity as BaseEntity);
                }
            }

            CheckCurrentTime();
        }

        private void InitializeLightController(BaseEntity entity)
        {
            if (entity == null || entity.IsDestroyed)
                return;

            ConsumeType consumeType = StringToType(entity?.ShortPrefabName ?? string.Empty);

            if (consumeType == ConsumeType.None || !configData.Types[consumeType].Enabled)
                return;

            lightControllers.Add(entity.GetComponent<LightController>() ?? entity.gameObject.AddComponent<LightController>());
        }

        private void CheckCurrentTime()
        {
            if (globalToggle)
            {
                float time = TOD_Sky.Instance.Cycle.Hour;
                if (time >= configData.Sunset || (time >= 0 && time < configData.Sunrise))
                {
                    if (!lightsOn)
                    {
                        ServerMgr.Instance.StartCoroutine(ToggleAllLights(lightControllers, true));
                        lightsOn = true;
                    }
                }
                else if (time >= configData.Sunrise && time < configData.Sunset)
                {
                    if (lightsOn)
                    {
                        ServerMgr.Instance.StartCoroutine(ToggleAllLights(lightControllers, false));
                        lightsOn = false;
                    }
                }
            }
            timeCheck = timer.Once(20, CheckCurrentTime);
        }

        private IEnumerator ToggleAllLights(IEnumerable<LightController> lights, bool status)
        {
            IEnumerable<LightController> controllers = lights.ToList();
            if (controllers.Any())
            {
                for (int i = controllers.Count() - 1; i >= 0; i--)
                {
                    LightController lightController = controllers.ElementAt(i);
                    yield return new WaitForSeconds(UnityEngine.Random.Range(0.1f, 0.25f));

                    if (lightController != null)
                        lightController.ToggleLight(status);
                }
            }
        }

        private ConsumeType StringToType(string name)
        {
            switch (name)
            {
                case "campfire":
                    return ConsumeType.Campfire;

                case "skull_fire_pit":
                    return ConsumeType.Firepit;

                case "fireplace.deployed":
                    return ConsumeType.Fireplace;

                case "furnace":
                    return ConsumeType.Furnace;

                case "furnace.large":
                    return ConsumeType.LargeFurnace;

                case "ceilinglight.deployed":
                    return ConsumeType.CeilingLight;

                case "lantern.deployed":
                    return ConsumeType.Lanterns;

                case "jackolantern.angry":
                case "jackolantern.happy":
                    return ConsumeType.JackOLantern;

                case "tunalight.deployed":
                    return ConsumeType.TunaLight;

                case "searchlight.deployed":
                    return ConsumeType.Searchlight;

                case "bbq.deployed":
                    return ConsumeType.BBQ;

                case "refinery_small_deployed":
                    return ConsumeType.Refinery;

                default:
                    return ConsumeType.None;
            }
        }

        private ConsumeType ParseType(string type)
        {
            try
            {
                return (ConsumeType)Enum.Parse(typeof(ConsumeType), type, true);
            }
            catch
            {
                return ConsumeType.None;
            }
        }

        private bool UserHasToggled(ulong playerId, ConsumeType consumeType)
        {
            Dictionary<ConsumeType, bool> userPreferences;
            if (toggleList.TryGetValue(playerId, out userPreferences))
                return userPreferences[consumeType];
            return configData.Types[consumeType].Enabled;
        }

        #endregion Functions

        #region Component

        class LightController : MonoBehaviour
        {
            private BaseEntity entity;
            private ConfigData.LightSettings config;
            private bool isSearchlight;
            private bool ignoreFuelConsumtion;

            public ConsumeType consumeType
            {
                get;
                private set;
            }

            private void Awake()
            {
                entity = GetComponent<BaseEntity>();
                consumeType = ins.StringToType(entity.ShortPrefabName);
                config = ins.configData.Types[consumeType];
                isSearchlight = entity.prefabID == 1912179101;

                object success = ins.NoFuelRequirements?.Call("IgnoreFuelConsumption", consumeType.ToString(), entity.OwnerID);
                if (success != null)
                    ignoreFuelConsumtion = true;
            }

            public void ToggleLight(bool status)
            {
                if (config.Owner && !ins.UserHasToggled(entity.OwnerID, consumeType))
                    status = false;

                if (isSearchlight)
                {
                    SearchLight searchLight = entity as SearchLight;
                    if (searchLight != null)
                    {
                        if (status)
                        {
                            Item slot = searchLight.inventory.GetSlot(0);
                            if (slot == null)
                            {
                                ItemManager.Create(searchLight.fuelType).MoveToContainer(searchLight.inventory);
                            }
                        }
                        searchLight.SetFlag(BaseEntity.Flags.On, status);
                    }
                }
                else
                {
                    BaseOven baseOven = entity as BaseOven;
                    if (baseOven != null)
                    {
                        if (config.ConsumeFuel)
                        {
                            if (status)
                                baseOven.StartCooking();
                            else baseOven.StopCooking();
                        }
                        else
                        {
                            if (baseOven.IsOn() != status)
                                baseOven.SetFlag(BaseEntity.Flags.On, status);
                        }
                    }
                }
                entity.SendNetworkUpdate();
            }

            public void OnConsumeFuel(Item fuel)
            {
                if (ignoreFuelConsumtion || config.ConsumeFuel)
                    return;

                fuel.amount++;
            }

            public bool IsOwner(ulong playerId) => entity.OwnerID == playerId;
        }

        #endregion Component

        #region Commands

        [ChatCommand("lantern")]
        private void cmdLantern(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                Dictionary<ConsumeType, bool> userPreferences;
                if (!toggleList.TryGetValue(player.userID, out userPreferences))
                    userPreferences = configData.Types.ToDictionary(x => x.Key, y => y.Value.Enabled);

                bool canToggle = false;
                foreach (var lightType in configData.Types)
                {
                    if (lightType.Value.Owner)
                    {
                        if (lightType.Value.Permission && !permission.UserHasPermission(player.UserIDString, $"nightlantern.{lightType.Key}"))
                            continue;
                        player.ChatMessage(string.Format(msg("user.type", player.userID), lightType.Key, userPreferences[lightType.Key] ? msg("user.enabled", player.userID) : msg("user.disabled", player.userID)));
                        canToggle = true;
                    }
                }

                if (canToggle)
                    player.ChatMessage(msg("user.toggle.opt", player.userID));

                if (permission.UserHasPermission(player.UserIDString, "nightlantern.global"))
                {
                    player.ChatMessage(string.Format(msg("global.toggle", player.userID), globalToggle ? msg("user.enabled", player.userID) : msg("user.disabled", player.userID)));
                    player.ChatMessage(msg("global.toggle.opt", player.userID));
                }
                return;
            }

            if (args[0].ToLower() == "global" && permission.UserHasPermission(player.UserIDString, "nightlantern.global"))
            {
                globalToggle = !globalToggle;
                ServerMgr.Instance.StartCoroutine(ToggleAllLights(lightControllers, globalToggle));
                player.ChatMessage(string.Format(msg("global.toggle", player.userID), globalToggle ? msg("user.enabled", player.userID) : msg("user.disabled", player.userID)));
            }
            else
            {
                ConsumeType consumeType = ParseType(args[0]);
                if (consumeType == ConsumeType.None || !permission.UserHasPermission(player.UserIDString, $"nightlantern.{consumeType}"))
                {
                    player.ChatMessage(string.Format(msg("toggle.invalid", player.userID), consumeType));
                    return;
                }

                if (!toggleList.ContainsKey(player.userID))
                    toggleList.Add(player.userID, configData.Types.ToDictionary(x => x.Key, y => y.Value.Enabled));

                toggleList[player.userID][consumeType] = !toggleList[player.userID][consumeType];

                IEnumerable<LightController> ownedLights = lightControllers.Where(x => x.IsOwner(player.userID) && x.consumeType == consumeType).ToList();
                if (ownedLights.Any())
                    ServerMgr.Instance.StartCoroutine(ToggleAllLights(ownedLights, toggleList[player.userID][consumeType]));

                player.ChatMessage(string.Format(msg("user.type", player.userID), consumeType, toggleList[player.userID][consumeType] ? msg("user.enabled", player.userID) : msg("user.disabled", player.userID)));
            }
        }

        #endregion Commands

        #region Config

        private enum ConsumeType { BBQ, Campfire, CeilingLight, Firepit, Fireplace, Furnace, LargeFurnace, Lanterns, JackOLantern, TunaLight, Searchlight, Refinery, None }

        private ConfigData configData;

        class ConfigData
        {
            [JsonProperty(PropertyName = "Light Settings")]
            public Dictionary<ConsumeType, LightSettings> Types { get; set; }

            [JsonProperty(PropertyName = "Time autolights are disabled")]
            public float Sunrise { get; set; }

            [JsonProperty(PropertyName = "Time autolights are enabled")]
            public float Sunset { get; set; }

            public class LightSettings
            {
                [JsonProperty(PropertyName = "This type is enabled")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "This type consumes fuel")]
                public bool ConsumeFuel { get; set; }

                [JsonProperty(PropertyName = "This type can be toggled by the owner")]
                public bool Owner { get; set; }

                [JsonProperty(PropertyName = "This type requires permission to be toggled by the owner")]
                public bool Permission { get; set; }
            }

            public VersionNumber Version { get; set; }
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
                Types = new Dictionary<ConsumeType, ConfigData.LightSettings>
                {
                    [ConsumeType.BBQ] = new ConfigData.LightSettings
                    {
                        ConsumeFuel = true,
                        Enabled = true,
                        Permission = true,
                        Owner = true
                    },
                    [ConsumeType.Campfire] = new ConfigData.LightSettings
                    {
                        ConsumeFuel = true,
                        Enabled = true,
                        Permission = true,
                        Owner = true
                    },
                    [ConsumeType.CeilingLight] = new ConfigData.LightSettings
                    {
                        ConsumeFuel = true,
                        Enabled = true,
                        Permission = true,
                        Owner = true
                    },
                    [ConsumeType.Firepit] = new ConfigData.LightSettings
                    {
                        ConsumeFuel = true,
                        Enabled = true,
                        Permission = true,
                        Owner = true
                    },
                    [ConsumeType.Fireplace] = new ConfigData.LightSettings
                    {
                        ConsumeFuel = true,
                        Enabled = true,
                        Permission = true,
                        Owner = true
                    },
                    [ConsumeType.Furnace] = new ConfigData.LightSettings
                    {
                        ConsumeFuel = true,
                        Enabled = true,
                        Permission = false
                    },
                    [ConsumeType.JackOLantern] = new ConfigData.LightSettings
                    {
                        ConsumeFuel = true,
                        Enabled = true,
                        Permission = true,
                        Owner = true
                    },
                    [ConsumeType.Lanterns] = new ConfigData.LightSettings
                    {
                        ConsumeFuel = true,
                        Enabled = true,
                        Permission = true,
                        Owner = true
                    },
                    [ConsumeType.LargeFurnace] = new ConfigData.LightSettings
                    {
                        ConsumeFuel = true,
                        Enabled = true,
                        Permission = true,
                        Owner = true
                    },
                    [ConsumeType.Searchlight] = new ConfigData.LightSettings
                    {
                        ConsumeFuel = true,
                        Enabled = true,
                        Permission = true,
                        Owner = true
                    },
                    [ConsumeType.TunaLight] = new ConfigData.LightSettings
                    {
                        ConsumeFuel = true,
                        Enabled = true,
                        Permission = true,
                        Owner = true
                    },
                    [ConsumeType.Refinery] = new ConfigData.LightSettings
                    {
                        ConsumeFuel = true,
                        Enabled = true,
                        Permission = true,
                        Owner = true
                    }
                },
                Sunrise = 7.5f,
                Sunset = 18.5f,
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new VersionNumber(2, 0, 9))
                configData = baseConfig;

            if (configData.Version < new VersionNumber(2, 0, 92))
                configData.Types.Add(ConsumeType.BBQ, baseConfig.Types[ConsumeType.BBQ]);

            if (configData.Version < new VersionNumber(2, 0, 94))
                configData.Types.Add(ConsumeType.Refinery, baseConfig.Types[ConsumeType.Refinery]);

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion Config

        #region Data Management

        private void SaveData() => Interface.Oxide.DataFileSystem.GetFile("nightlantern_data").WriteObject(toggleList);

        private void LoadData()
        {
            try
            {
                toggleList = Interface.Oxide.DataFileSystem.GetFile("nightlantern_data").ReadObject<Dictionary<ulong, Dictionary<ConsumeType, bool>>>();
            }
            catch
            {
                toggleList = new Dictionary<ulong, Dictionary<ConsumeType, bool>>();
            }
        }

        #endregion Data Management

        #region Localization

        private string msg(string key, ulong playerId = 0U) => lang.GetMessage(key, this, playerId == 0U ? null : playerId.ToString());

        private readonly Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["global.toggle"] = "Autolights are {0} server wide",
            ["global.toggle.opt"] = "You can toggle autolights globally by typing '/lantern global'",
            ["user.disable"] = "You have disabled autolights that you own of the type {0}",
            ["user.enable"] = "You have enabled autolights that you own of the type {0}",
            ["user.type"] = "{0} : {1}",
            ["user.enabled"] = "<color=#8ee700>enabled</color>",
            ["user.disabled"] = "<color=#e90000>disabled</color>",
            ["user.toggle.opt"] = "You can toggle the various types by typing '/lantern <light type>'",
            ["toggle.invalid"] = "{0} is an invalid option!"
        };

        #endregion Localization
    }
}