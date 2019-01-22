using Newtonsoft.Json;
using Oxide.Core;
using System.IO;

namespace Oxide.Plugins
{
    [Info("Everlight", "Wulf/lukespragg", "3.3.1")]
    [Description("Allows infinite light from configured objects by not consuming fuel")]
    public class Everlight : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Campfires (true/false)")]
            public bool Campfires { get; set; } = true;

            [JsonProperty(PropertyName = "Ceiling lights (true/false)")]
            public bool CeilingLights { get; set; } = true;

            [JsonProperty(PropertyName = "Fire pits (true/false)")]
            public bool FirePits { get; set; } = true;

            [JsonProperty(PropertyName = "Fireplaces (true/false)")]
            public bool Fireplaces { get; set; } = true;

            [JsonProperty(PropertyName = "Furnaces (true/false)")]
            public bool Furnaces { get; set; } = true;

            [JsonProperty(PropertyName = "Grills (true/false)")]
            public bool Grills { get; set; } = true;

            [JsonProperty(PropertyName = "Hats (true/false)")]
            public bool Hats { get; set; } = true;

            [JsonProperty(PropertyName = "Lanterns (true/false)")]
            public bool Lanterns { get; set; } = true;

            [JsonProperty(PropertyName = "Refineries (true/false)")]
            public bool Refineries { get; set; } = true;

            [JsonProperty(PropertyName = "Search lights (true/false)")]
            public bool SearchLights { get; set; } = true;

            [JsonProperty(PropertyName = "Use permissions (true/false)")]
            public bool UsePermissions { get; set; } = false;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            string configPath = $"{Interface.Oxide.ConfigDirectory}{Path.DirectorySeparatorChar}{Name}.json";
            LogWarning($"Could not load a valid configuration file, creating a new configuration file at {configPath}");
            config = new Configuration();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion Configuration

        #region Initialization

        private const string permCampfires = "everlight.campfires";
        private const string permCeilingLights = "everlight.ceilinglights";
        private const string permFirePits = "everlight.firepits";
        private const string permFireplaces = "everlight.fireplaces";
        private const string permFurnaces = "everlight.furnaces";
        private const string permGrills = "everlight.grills";
        private const string permHats = "everlight.hats";
        private const string permLanterns = "everlight.lanterns";
        private const string permRefineries = "everlight.refineries";
        private const string permSearchLights = "everlight.searchlights";

        public static Everlight instance;

        private void Init()
        {
            instance = this;

            if (config.UsePermissions)
            {
                permission.RegisterPermission(permCampfires, this);
                permission.RegisterPermission(permCeilingLights, this);
                permission.RegisterPermission(permFirePits, this);
                permission.RegisterPermission(permFurnaces, this);
                permission.RegisterPermission(permGrills, this);
                permission.RegisterPermission(permHats, this);
                permission.RegisterPermission(permLanterns, this);
                permission.RegisterPermission(permRefineries, this);
                permission.RegisterPermission(permSearchLights, this);
            }
            else
            {
                LogWarning("Enabled for all players; permissions not enabled in configuration");
            }
        }

        #endregion Initialization

        #region Fuel Magic

        public class EverlightItem
        {
            public bool Enabled { get; set; }
            public string Permission { get; set; }

            public EverlightItem(string shortName)
            {
                switch (shortName)
                {
                    case "campfire": // OnFindBurnable
                        Enabled = instance.config.Campfires;
                        Permission = permCampfires;
                        break;

                    case "ceilinglight.deployed": // OnFindBurnable
                        Enabled = instance.config.CeilingLights;
                        Permission = permCeilingLights;
                        break;

                    case "skull_fire_pit": // OnFindBurnable
                        Enabled = instance.config.FirePits;
                        Permission = permFirePits;
                        break;

                    case "furnace": // OnFindBurnable
                    case "furnace.large": // OnFindBurnable
                        Enabled = instance.config.Furnaces;
                        Permission = permFurnaces;
                        break;

                    case "fireplace.deployed": // OnFindBurnable
                        Enabled = instance.config.Fireplaces;
                        Permission = permFireplaces;
                        break;

                    case "bbq.deployed": // OnFindBurnable
                        Enabled = instance.config.Grills;
                        Permission = permGrills;
                        break;

                    case "hat.candle": // OnItemUse
                    case "hat.miner": // OnItemUse
                        Enabled = instance.config.Hats;
                        Permission = permHats;
                        break;

                    case "lantern.deployed": // OnFindBurnable
                    case "tunalight.deployed": // OnFindBurnable
                        Enabled = instance.config.Lanterns;
                        Permission = permLanterns;
                        break;

                    case "refinery_small_deployed": // OnFindBurnable
                        Enabled = instance.config.Refineries;
                        Permission = permRefineries;
                        break;

                    case "searchlight.deployed": // OnItemUse
                        Enabled = instance.config.SearchLights;
                        Permission = permSearchLights;
                        break;

                    default:
                        Enabled = false;
                        Permission = "";
                        break;
                }
            }
        }

        private object OnFindBurnable(BaseOven oven)
        {
            if (oven == null || oven.fuelType == null)
            {
                return null;
            }

            string shortName = oven.ShortPrefabName;
            EverlightItem eItem = new EverlightItem(shortName);
#if DEBUG
            server.Broadcast("OnFindBurnable: " + shortName);
#endif

            if (config.UsePermissions && !permission.UserHasPermission(oven.OwnerID.ToString(), eItem.Permission))
            {
                return null;
            }

            if (eItem.Enabled)
            {
                return ItemManager.CreateByItemID(oven.fuelType.itemid);
            }

            return null;
        }

        private void OnItemUse(Item item, int amount)
        {
            string shortName = item.parent?.parent?.info?.shortname ?? item.GetRootContainer()?.entityOwner?.ShortPrefabName;

            if (string.IsNullOrEmpty(shortName))
            {
                return;
            }

            BasePlayer owner = item.GetOwnerPlayer();
            EverlightItem eItem = new EverlightItem(shortName);
#if DEBUG
            server.Broadcast("OnItemUse: " + shortName);
#endif

            if (owner != null && config.UsePermissions && permission.UserHasPermission(owner.OwnerID.ToString(), eItem.Permission))
            {
                return;
            }

            if (eItem.Enabled)
            {
                item.amount += amount;
            }
        }

        #endregion Fuel Magic
    }
}
