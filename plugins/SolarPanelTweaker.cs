using System.Collections.Generic;   //list.config
using System;   //Convert

namespace Oxide.Plugins
{
    [Info("Solar Panel Tweaker", "BuzZ", "0.0.2")]
    [Description("Change Solar Panel Attributes")]

/*======================================================================================================================= 
*   
*   17th december 2018
*
*   0.0.1   20181217    creation
*   0.0.2   20181227    fix mistake in bool
*
*	THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*=======================================================================================================================*/

    public class SolarPanelTweaker : RustPlugin
    {
        bool debug = false;
        private bool ConfigChanged;
        const string Tweaker = "solarpaneltweaker.tweak"; 
        int MaxiPowa = 20;
        bool SolarWorld = false;

		void Init()
        {
            LoadVariables();
            permission.RegisterPermission(Tweaker, this);
        }

        private void OnServerInitialized()
        {
            if (SolarWorld) SetASolarWorld();
        }

        protected override void LoadDefaultConfig()
        {
            LoadVariables();
        }

        private void LoadVariables()
        {
            MaxiPowa = Convert.ToInt32(GetConfig("Solar Panel Settings", "Maximum Power Output", "20"));    
            SolarWorld = Convert.ToBoolean(GetConfig("Solar Panel", "Setting for all World", "false"));    

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
        void SetASolarWorld()
        {
            foreach (var solarpanel in UnityEngine.Object.FindObjectsOfType<SolarPanel>())
            {
                SolarPanelTweakerizer(solarpanel);
            }
        }

// FOR FUTURE USE
        /*List<SolarPanel> SolarPanelSpawned = new List<SolarPanel>();
        void OnServerInitialized()
        {
            timer.Every(10f, () =>
            {
                insidetimerstuff();
            });
        }*/

        void OnEntitySpawned(BaseNetworkable entity)
        {
    	    SolarPanel solarpanel = entity.GetComponent<SolarPanel>();
            if (solarpanel != null)
            {
                if (debug) Puts($"SOLAR PANEL SPAWN !");
                bool istweaker = permission.UserHasPermission(solarpanel.OwnerID.ToString(), Tweaker);
                if (SolarWorld || istweaker)
                {
                    SolarPanelTweakerizer(solarpanel);
                }
                //SolarPanelSpawned.Add(solarpanel);
            }
        }

        void SolarPanelTweakerizer(SolarPanel solarpanel)
        {
            solarpanel.maximalPowerOutput = MaxiPowa;
            if (debug) Puts($"ConsumptionAmount {solarpanel.ConsumptionAmount()}");   //0
            if (debug) Puts($"dot_maximum {solarpanel.dot_maximum}");   //0.7
            if (debug) Puts($"dot_minimum {solarpanel.dot_minimum}");   //0.3
            if (debug) Puts($"maximalPowerOutput {solarpanel.maximalPowerOutput}");    //20
        }

// FOR FUTURE USE
        /*void insidetimerstuff()
        {
            if (SolarPanelSpawned == null) return;
            foreach (var solarpanel in SolarPanelSpawned)
            {
                if (debug) Puts($"ConsumptionAmount {solarpanel.ConsumptionAmount()}");   //0
                if (debug) Puts($"dot_maximum {solarpanel.dot_maximum}");   //0.7
                if (debug) Puts($"dot_minimum {solarpanel.dot_minimum}");   //0.3
            }
        }*/
    }
}
