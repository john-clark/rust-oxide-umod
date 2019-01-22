namespace Oxide.Plugins
{
    [Info("NoSupplySignal", "Wulf/lukespragg", 0.1, ResourceId = 2375)]
    [Description("Prevents supply drops triggering from supply signals")]

    class NoSupplySignal : CovalencePlugin
    {
        void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (entity.name.Contains("signal")) entity.KillMessage();
        }
    }
}