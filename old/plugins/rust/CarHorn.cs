using System.Collections.Generic;
using UnityEngine;
using System;

namespace Oxide.Plugins
{
    [Info("Car Horn", "Default", "1.1.1")]
    [Description("Adds an FX similar to that of a car horn to the driver in the sedan.")]
    class CarHorn : RustPlugin
    {
        #region variables
        private bool Changed = false;
        private string hornPrefab = "assets/prefabs/instruments/guitar/effects/guitarpluck.prefab";
        private const string permissionUse = "carhorn.use";
        //private string carPrefab = "assets/prefabs/vehicle/seats/driverseat";
        #endregion
            
        #region Main plugin
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            LoadVariables();
        }

        private void OnPlayerInput(BasePlayer player, InputState input, BaseEntity entity)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionUse))
            {
                return;
            }

            if (player.GetMounted() == null)
                return;

            if (!input.IsDown(BUTTON.FIRE_PRIMARY))
                return;

            if (player.isMounted && player.GetMounted().name.Contains("driverseat"))
            {
                Effect.server.Run(hornPrefab, player.transform.position);
            }

        }



        void Init()
        {
            LoadVariables();
            permission.RegisterPermission(permissionUse, this);
        }

        #endregion 

        #region Config
        void LoadVariables()
        {
            hornPrefab = Convert.ToString(GetConfig("Horn Prefab", "FX used", "assets/prefabs/instruments/guitar/effects/guitarpluck.prefab"));
            //carPrefab = Convert.ToString(GetConfig("Car Prefab", "Seat", "assets/prefabs/vehicle/seats/driverseat"));
            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }


        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }
        #endregion
    }
}