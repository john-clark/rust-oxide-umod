using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Auto Fuel", "redBDGR", "1.1.0")]
    [Description("Automatically fuels lights using fuel from the tool cupboard's inventory")]
    class AutoFuel : RustPlugin
    {
        private bool Changed;

        private bool dontRequireFuel;
        private bool useBarbeque;
        private bool useCampfires;
        private bool useCeilingLight;
        private bool useFireplace;
        private bool useFurnace;
        private bool useJackOLantern;
        private bool useLantern;
        private bool useSearchLight;
        private bool useSkullFirepit;
        private bool useOilRefinery;
        private bool useTunaCanLamp;
        private bool useFogmachine;

        private List<string> activeShortNames = new List<string>();

        #region Configuration

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
            DoStartupItemNames();
        }

        private void LoadVariables()
        {
            dontRequireFuel = Convert.ToBoolean(GetConfig("Settings", "Don't require fuel", false));
            useBarbeque = Convert.ToBoolean(GetConfig("Types to autofuel", "Use Barbeque", false));
            useCampfires = Convert.ToBoolean(GetConfig("Types to autofuel", "Use Campfire", false));
            useCeilingLight = Convert.ToBoolean(GetConfig("Types to autofuel", "Use Ceiling Light", true));
            useFireplace = Convert.ToBoolean(GetConfig("Types to autofuel", "Use Fireplace", false));
            useFurnace = Convert.ToBoolean(GetConfig("Types to autofuel", "Use Furnace", false));
            useJackOLantern = Convert.ToBoolean(GetConfig("Types to autofuel", "Use JackOLanterns", true));
            useLantern = Convert.ToBoolean(GetConfig("Types to autofuel", "Use Lantern", true));
            useOilRefinery = Convert.ToBoolean(GetConfig("Types to autofuel", "Use Oil Refinery", false));
            useSearchLight = Convert.ToBoolean(GetConfig("Types to autofuel", "Use Search Light", true));
            useSkullFirepit = Convert.ToBoolean(GetConfig("Types to autofuel", "Use Skull Fire Pit", false));
            useTunaCanLamp = Convert.ToBoolean(GetConfig("Types to autofuel", "Use Tuna Can Lamp", true));
            useFogmachine = Convert.ToBoolean(GetConfig("Types to autofuel", "Use Fogmachine", false));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            LoadVariables();
            DoStartupItemNames();
        }

        private void OnServerInitialized()
        {
            ServerMgr.Instance.StartCoroutine(FindOvens());
        }

        private void Unload()
        {
            foreach (AutomaticRefuel refuel in UnityEngine.Object.FindObjectsOfType<AutomaticRefuel>())
                UnityEngine.Object.Destroy(refuel);
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity.GetComponent<BaseEntity>()?.ShortPrefabName == "jackolantern.angry" || entity.GetComponent<BaseEntity>()?.ShortPrefabName == "jackolantern.happy")
                entity.GetComponent<BaseOven>().fuelType = ItemManager.FindItemDefinition("wood");

            BaseOven oven = entity.GetComponent<BaseOven>();
            if (!oven)
                return;
            if (!activeShortNames.Contains(oven.ShortPrefabName))
                return;
            if (!oven.GetComponent<AutomaticRefuel>())
                oven.gameObject.AddComponent<AutomaticRefuel>();
        }

        private Item OnFindBurnable(BaseOven oven)
        {
            if (oven.fuelType == null)
                return null;
            if (!activeShortNames.Contains(oven.ShortPrefabName))
                return null;
            if (HasFuel(oven))
                return null;
            DecayEntity decayEnt = oven.GetComponent<DecayEntity>();
            if (decayEnt == null)
                return null;
            AutomaticRefuel refuel = decayEnt.GetComponent<AutomaticRefuel>();
            if (!refuel)
                decayEnt.gameObject.AddComponent<AutomaticRefuel>();
            if (refuel.cupboard == null)
            {
                refuel.SearchForCupboard();
                if (refuel.cupboard == null)
                    return null;
            }

            if (dontRequireFuel)
                return ItemManager.CreateByName(oven.fuelType.shortname, 1);
            Item fuelItem = refuel.GetFuel();
            if (fuelItem == null)
                return null;
            RemoveItemThink(fuelItem);
            ItemManager.CreateByName(oven.fuelType.shortname, 1)?.MoveToContainer(oven.inventory);
            return null;
        }

        #endregion

        #region Custom Components

        private class AutomaticRefuel : FacepunchBehaviour
        {
            public BuildingPrivlidge cupboard;
            public Item cachedFuelItem;
            private BaseOven oven;

            private void Awake()
            {
                oven = GetComponent<BaseOven>();
                SearchForCupboard();
                InvokeRepeating(() => { FuelStillRemains(); }, 5f, 5f);
            }

            public Item FuelStillRemains()
            {
                if (cachedFuelItem == null)
                    return cachedFuelItem;
                if (cachedFuelItem.GetRootContainer() == cupboard.inventory)
                    return cachedFuelItem;
                cachedFuelItem = null;
                return cachedFuelItem;
            }

            public BuildingPrivlidge SearchForCupboard()
            {
                cupboard = oven.GetBuildingPrivilege();
                return cupboard;
            }

            private Item SearchForFuel()
            {
                cachedFuelItem = cupboard.inventory?.itemList?.FirstOrDefault(item => item.info == oven.fuelType);
                return cachedFuelItem;
            }

            public Item GetFuel()
            {
                if (cachedFuelItem != null)
                {
                    Item item = FuelStillRemains();
                    if (item != null)
                        return item;
                }

                return SearchForFuel();
            }
        }


        #endregion

        #region Methods

        private IEnumerator FindOvens()
        {
            BaseOven[] ovens = UnityEngine.Object.FindObjectsOfType<BaseOven>();
            foreach (BaseOven oven in ovens)
            {
                yield return new WaitForSeconds(0.05f);
                if (oven.fuelType == null)
                    continue;
                if (!activeShortNames.Contains(oven.ShortPrefabName))
                    continue;
                AutomaticRefuel refuel = oven.GetComponent<AutomaticRefuel>();
                if (!refuel)
                    oven.gameObject.AddComponent<AutomaticRefuel>();
            }
        }

        private bool HasFuel(BaseOven oven)
        {
            return oven.inventory.itemList.Any(item => item.info == oven.fuelType);
        }

        private static void RemoveItemThink(Item item)
        {
            if (item == null)
                return;
            if (item.amount == 1)
            {
                item.RemoveFromContainer();
                item.RemoveFromWorld();
            }
            else
            {
                item.amount = item.amount - 1;
                item.MarkDirty();
            }
        }

        private void DoStartupItemNames()
        {
            if (useBarbeque)
                activeShortNames.Add("bbq.deployed");
            if (useCampfires)
                activeShortNames.Add("campfire");
            if (useCeilingLight)
                activeShortNames.Add("ceilinglight.deployed");
            if (useFireplace)
                activeShortNames.Add("fireplace.deployed");
            if (useFurnace)
            {
                activeShortNames.Add("furnace");
                activeShortNames.Add("furnace.large");
            }
            if (useJackOLantern)
            {
                activeShortNames.Add("jackolantern.angry");
                activeShortNames.Add("jackolantern.happy");
            }
            if (useLantern)
                activeShortNames.Add("lantern.deployed");
            if (useOilRefinery)
                activeShortNames.Add("refinery_small_deployed");
            if (useSearchLight)
                activeShortNames.Add("searchlight.deployed");
            if (useSkullFirepit)
                activeShortNames.Add("skull_fire_pit");
            if (useTunaCanLamp)
                activeShortNames.Add("tunalight.deployed");
            if (useFogmachine)
                activeShortNames.Add("fogmachine");
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (data.TryGetValue(datavalue, out value)) return value;
            value = defaultValue;
            data[datavalue] = value;
            Changed = true;
            return value;
        }

        #endregion
    }
}
