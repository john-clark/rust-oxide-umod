using System.Collections.Generic;
using Convert = System.Convert;

namespace Oxide.Plugins
{
    [Info("Medical Tool Tweaker", "BuzZ", "0.0.1")]
    [Description("Modify medical tools variables")]

/*======================================================================================================================= 
*
*   
*   08th february 2019
*
*   0.0.1   20190208    creation

*
*	THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*=======================================================================================================================*/

    public class MedicalToolTweaker : RustPlugin
    {

        bool debug = false;
        private bool ConfigChanged;
        const string MedicalTweaker = "medicaltooltweaker.use";

        float maxdistanceother = 2f;
        float healdurationother = 4f;
        bool canuseonother = true;
        float healdurationself = 4f;
        bool canrevive = false;


        void Init()
        {
            permission.RegisterPermission(MedicalTweaker, this);
            LoadVariables();
        }

//////////////////////////////////
#region CONFIG

    protected override void LoadDefaultConfig()
        {
            LoadVariables();
        }

        private void LoadVariables()
        {
            maxdistanceother = Convert.ToSingle(GetConfig("Healing Others", "Max Distance (2 by default)", "2"));
            healdurationother = Convert.ToSingle(GetConfig("Healing Others", "Heal duration (4 by default)", "4"));
            canuseonother = Convert.ToBoolean(GetConfig("Healing Others", "Can heal others (true by default)", "true"));
            healdurationself = Convert.ToSingle(GetConfig("Healing Self", "Heal duration (4 by default)", "4"));
            canrevive = Convert.ToBoolean(GetConfig("Revive", "Can be used to revive (false by default)", "false"));

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

#endregion

//////////////////////////////////////////////////////

        void OnHealingItemUse(MedicalTool tool, BasePlayer player)
        {
            bool tweaker = permission.UserHasPermission(player.UserIDString, MedicalTweaker);
            if (tweaker)
            {
                tool.maxDistanceOther = maxdistanceother;//2  serynge +bandage//
                tool.healDurationSelf = healdurationself;//4
                tool.healDurationOther = healdurationother;//4
                tool.canRevive = canrevive;//false
                tool.canUseOnOther = canuseonother;//true
            }
        }
    }
}