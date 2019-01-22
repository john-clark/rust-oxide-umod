namespace Oxide.Plugins
{
	[Info("Airdrop Without Parachute", "BuzZ[PHOQUE]", "0.0.1")]
	[Description("Remove parachute on Airdrops")]

/*======================================================================================================================= 
*
*   
*   6th september 2018
* 
*
*	THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*
*=======================================================================================================================*/


	public class AirdropWithoutParachute : RustPlugin
	{

        private void OnEntitySpawned(BaseEntity Entity)
        {
            if (Entity == null) return;

            if (Entity is SupplyDrop)
            {
                SupplyDrop dropped = Entity as SupplyDrop;

                dropped.RemoveParachute();

            }


		}


    }
}