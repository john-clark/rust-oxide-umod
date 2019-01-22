namespace Oxide.Plugins
{
    [Info("NoDeathScreen", "Wulf/lukespragg", "0.1.1", ResourceId = 2332)]
    [Description("Disables the death screen by automatically respawning players")]

    class NoDeathScreen : RustPlugin
    {
        void OnEntityDeath(BaseCombatEntity entity)
        {
            var player = entity.ToPlayer();
            NextTick(() => { if (player && player.IsConnected) player.Respawn(); });
        }
    }
}