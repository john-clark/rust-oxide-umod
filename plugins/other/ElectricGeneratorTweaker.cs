using System.Collections.Generic;   //list.config
using System;   //Convert

namespace Oxide.Plugins
{
    [Info("Electric Generator Tweaker", "BuzZ", "0.0.1")]
    [Description("Change Electric Generator Attributes")]

/*======================================================================================================================= 
*   
*   08 february 2019
*
*   0.0.1   20190208    creation
*
*	THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*=======================================================================================================================*/

    public class ElectricGeneratorTweaker : RustPlugin
    {
        bool debug = false;
        private bool ConfigChanged;
        const string Tweaker = "electricgeneratortweaker.tweak"; 
        float electricAmount = 100f;
        bool ElectricGeneratorWorld = false;

		void Init()
        {
            LoadVariables();
            permission.RegisterPermission(Tweaker, this);
        }

        private void OnServerInitialized()
        {
            if (ElectricGeneratorWorld) SetAnElectricGeneratorWorld();
        }

        protected override void LoadDefaultConfig()
        {
            LoadVariables();
        }

        private void LoadVariables()
        {
            electricAmount = Convert.ToSingle(GetConfig("Electric Generator Attributes", "Amount of electricity (100 by default)", "100"));    
            ElectricGeneratorWorld = Convert.ToBoolean(GetConfig("Electric Generator", "Setting for all World", "false"));    

            if (!ConfigChanged) return;
            SaveConfig();
            ConfigChanged = false;
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                ConfigChanged = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                ConfigChanged = true;
            }
            return value;
        }

/////// ON LOAD FIND
        void SetAnElectricGeneratorWorld()
        {
            foreach (var generator in UnityEngine.Object.FindObjectsOfType<ElectricGenerator>())
            {
                ElectricGeneratorTweakerizer(generator);
            }
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
    	    ElectricGenerator generator = entity.GetComponent<ElectricGenerator>();
            if (generator != null)
            {
                if (debug) Puts($"ELECTRIC GENERATOR SPAWN !");
                bool istweaker = permission.UserHasPermission(generator.OwnerID.ToString(), Tweaker);
                if (ElectricGeneratorWorld || istweaker)
                {
                    ElectricGeneratorTweakerizer(generator);
                }
            }
        }

        void ElectricGeneratorTweakerizer(ElectricGenerator generator)
        {
            if (generator.OwnerID == 0) return;
            generator.electricAmount = electricAmount;
            if (debug) Puts($"electricAmount {generator.electricAmount}");   //100
        }

    }
}
