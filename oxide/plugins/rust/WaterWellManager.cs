using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("WaterWellManager", "redBDGR", "1.0.0")]
    [Description("Configure how the water wells work")]

    /*
     *  TODO:
     *  
     */ 

    class WaterWellManager : RustPlugin
    {
        private bool Changed;

        private float caloriesPerPump = 5f;
        private float pressurePerPump = 0.2f;
        private float pressureForProduction = 1f;
        private int waterPerPump = 50;

        #region Config

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {
            caloriesPerPump = Convert.ToSingle(GetConfig("Settings", "Calories Needed Per Pump", 5f));
            pressurePerPump = Convert.ToSingle(GetConfig("Settings", "Pressure Per Pump", 0.2f));
            pressureForProduction = Convert.ToSingle(GetConfig("Settings", "Pressure Needed For Production", 1f));
            waterPerPump = Convert.ToInt32(GetConfig("Settings", "Water Per Pump", 50));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        #endregion

        private void OnServerInitalized()
        {
            foreach(WaterWell well in UnityEngine.Object.FindObjectsOfType<WaterWell>())
                SetWellVariables(well);
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            WaterWell well = entity.GetComponent<WaterWell>();
            if (!well)
                return;
            SetWellVariables(well);
        }

        private void SetWellVariables(WaterWell well)
        {
            well.caloriesPerPump = caloriesPerPump;
            well.pressurePerPump = pressurePerPump;
            well.pressureForProduction = pressureForProduction;
            well.waterPerPump = waterPerPump;
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
    }
}