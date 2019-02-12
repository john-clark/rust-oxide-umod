using Oxide.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NoFuelRequirements", "k1lly0u", "1.3.6", ResourceId = 1179)]
    class NoFuelRequirements : RustPlugin
    {
        #region Fields        
        private bool usingPermissions;
        private bool isInitialized;

        private string[] ValidFuelTypes = new string[] { "wood", "lowgradefuel" };
        #endregion

        #region Oxide Hooks        
        private void OnServerInitialized()
        {
            RegisterPermissions();
            isInitialized = true;
        }

        private void OnConsumeFuel(BaseOven oven, Item fuel, ItemModBurnable burnable)
        {
            if (!isInitialized || oven == null || fuel == null) return;
            ConsumeType type = StringToType(oven?.ShortPrefabName ?? string.Empty);
            if (type == ConsumeType.None) return;

            if (IsActiveType(type))
            {
                if (usingPermissions && oven.OwnerID != 0U)
                {
                    if (!HasPermission(oven.OwnerID.ToString(), type)) return;
                }
                fuel.amount += 1;
            }
        }
        
        private void OnItemUse(Item item, int amount)
        {
            if (!isInitialized || item == null || amount == 0 || !ValidFuelTypes.Contains(item.info.shortname)) return;
           
            string shortname = item?.parent?.parent?.info?.shortname ?? item?.GetRootContainer()?.entityOwner?.ShortPrefabName;
            if (string.IsNullOrEmpty(shortname)) return;

            ConsumeType type = StringToType(shortname);
            if (type == ConsumeType.None) return;

            if (IsActiveType(type))
            {
                if (usingPermissions)
                {
                    string playerId = item?.GetRootContainer()?.playerOwner?.UserIDString;
                    string entityId = item?.GetRootContainer()?.entityOwner?.OwnerID.ToString();

                    if (!string.IsNullOrEmpty(playerId))
                        if (!HasPermission(playerId, type)) return;
                    if (!string.IsNullOrEmpty(entityId) && entityId != "0")
                        if (!HasPermission(entityId, type)) return;
                }                
                item.amount += amount;
            }
        }
        #endregion

        #region Functions

        private void RegisterPermissions()
        {
            if (configData.UsePermissions)
            {
                usingPermissions = true;
                foreach (var type in Enum.GetValues(typeof(ConsumeType)))
                {
                    permission.RegisterPermission($"nofuelrequirements.{type}", this);
                }
            }
        }

        private object IgnoreFuelConsumption(string consumeTypeStr, ulong ownerId)
        {
            ConsumeType consumeType = ParseType(consumeTypeStr);
            if (consumeType != ConsumeType.None && configData.AffectedTypes[consumeType])
            {
                if (usingPermissions && !HasPermission(ownerId.ToString(), consumeType))
                    return null;
                return true;
            }
            return null;
        }

        private bool HasPermission(string ownerId, ConsumeType type) => permission.UserHasPermission(ownerId, $"nofuelrequirements.{type}");

        private bool IsActiveType(ConsumeType type) => configData.AffectedTypes[type];

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
                case "refinery_small_deployed":
                    return ConsumeType.OilRefinery;
                case "ceilinglight.deployed":
                    return ConsumeType.CeilingLight;
                case "lantern.deployed":
                    return ConsumeType.Lanterns;
                case "hat.miner":
                    return ConsumeType.MinersHat;
                case "hat.candle":
                    return ConsumeType.CandleHat;
                case "fuelstorage":
                    return ConsumeType.Quarry;
                case "tunalight.deployed":
                    return ConsumeType.TunaLight;
                case "searchlight.deployed":
                    return ConsumeType.Searchlight;
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
        #endregion

        #region Config  
        enum ConsumeType { Campfire, CandleHat, CeilingLight, Firepit, Fireplace, Furnace, Lanterns, LargeFurnace, MinersHat, OilRefinery, Quarry, TunaLight, Searchlight, None }
       
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Entities that ignore fuel consumption")]
            public Dictionary<ConsumeType, bool> AffectedTypes { get; set; }
            [JsonProperty(PropertyName = "Require permission to ignore fuel consumption")]
            public bool UsePermissions { get; set; }
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
                AffectedTypes = new Dictionary<ConsumeType, bool>
                {
                    [ConsumeType.Campfire] = true,
                    [ConsumeType.CandleHat] = true,
                    [ConsumeType.CeilingLight] = true,
                    [ConsumeType.Firepit] = true,
                    [ConsumeType.Fireplace] = true,
                    [ConsumeType.Furnace] = true,
                    [ConsumeType.Lanterns] = true,
                    [ConsumeType.LargeFurnace] = true,
                    [ConsumeType.MinersHat] = true,
                    [ConsumeType.OilRefinery] = true,
                    [ConsumeType.Quarry] = true,
                    [ConsumeType.TunaLight] = true,
                    [ConsumeType.Searchlight] = true
                },
                UsePermissions = false,
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();
            if (configData.Version < new VersionNumber(1, 3, 6))
                configData = baseConfig;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion
    }
}