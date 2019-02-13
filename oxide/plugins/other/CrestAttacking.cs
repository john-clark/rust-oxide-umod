using CodeHatch.Networking.Events.Entities;

namespace Oxide.Plugins
{
    [Info("No Crest Attacks", "A RoK Player", "0.2.2")]
    public class CrestAttacking : ReignOfKingsPlugin
    {
        private void OnEntityHealthChange(EntityDamageEvent damageEvent)
        {
            if (damageEvent.Damage.Amount < 0) return;
            if (!damageEvent.Entity.name.Contains("Crest")) return;
            damageEvent.Cancel("No damage to crest");
            damageEvent.Damage.Amount = 0f;
            PrintToChat(damageEvent.Damage.DamageSource.Owner, "[FF0000]You can't damage another players crest.  If this crest needs removed due to a rule violation please request assistance from an admin.#ffffff");
        }
    }
}