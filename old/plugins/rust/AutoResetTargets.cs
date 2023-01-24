namespace Oxide.Plugins
{
    [Info("Auto Reset Targets", "Dyceman", "1.0.1")]
    [Description("Automatically resets knocked down targets after 3 seconds")]
    public class AutoResetTargets : RustPlugin
    {
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            ReactiveTarget target = entity as ReactiveTarget;
            if (target != null && target.IsKnockedDown())
            {
                timer.Once(3f, () =>
                {
                    target.ResetTarget();
                });
            }
        }
    }
}
