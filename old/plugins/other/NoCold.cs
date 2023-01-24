namespace Oxide.Plugins
{
    [Info("NoCold", "Waizujin", 1.0)]
    [Description("Disables cold and heat effecting player.")]
    public class NoCold : RustPlugin
    {
        void OnRunPlayerMetabolism(PlayerMetabolism metabolism)
        {
            metabolism.temperature.Reset();
        }
    }
}
