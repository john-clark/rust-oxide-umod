namespace Oxide.Plugins
{
    [Info("No Medical Healing", "BuzZ", "0.0.1")]
    [Description("Null medical healing")]

/*======================================================================================================================= 
*
*   
*   08th february 2019
*
*   0.0.1   20190129    creation

*
*	THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*=======================================================================================================================*/

    public class NoMedicalHealing : RustPlugin
    {

        bool debug = false;
        const string CanNotUseMedical = "nomedicalhealing.apply";

        void Init()
        {
            permission.RegisterPermission(CanNotUseMedical, this);
        }

//////////////////////////////////

        object OnHealingItemUse(MedicalTool tool, BasePlayer player)
        {
            bool noheal = permission.UserHasPermission(player.UserIDString, CanNotUseMedical);
            if (noheal)
            {
                if (debug) Puts($"OnHealingItemUse nulled for - {player.displayName}");
                return true;
            }
            else return null;
        }

    }
}