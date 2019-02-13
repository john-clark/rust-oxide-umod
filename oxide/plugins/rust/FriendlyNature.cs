namespace Oxide.Plugins
{
    [Info("Friendly Nature", "Bruno Puccio", "1.0.1")]
    [Description("Stop animals from eating each other")]
    public class FriendlyNature : RustPlugin
    {
        object OnNpcTarget(BaseNpc npc, BaseEntity target)
        {
            if ((target as BaseNpc) == null || (target is NPCPlayer))
                return null;

            return false;
        }
    }
}