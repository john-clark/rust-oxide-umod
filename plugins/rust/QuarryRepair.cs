

namespace Oxide.Plugins
{
    [Info("Quarry Repair", "Orange", "1.0.1")] 
    [Description("Allows players to repair quarries")]
    public class QuarryRepair : RustPlugin
    {
        #region Oxide Hooks

        private void OnHammerHit(BasePlayer p, HitInfo i)
        {
            if (i == null)
            {
                return;
            }

            if (i.HitEntity == null)
            {
                return;
            }

            var quarry = i.HitEntity as MiningQuarry;

            if (quarry == null)
            {
                return;
            }

            if (quarry.Health() == quarry.MaxHealth())
            {
                return;
            }

            if (quarry.lastAttackedTime < 30f)
            {
                return;
            }

            quarry.Heal(100f);
            quarry.OnRepair();
        }

        #endregion
    }
}