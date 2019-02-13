namespace Oxide.Plugins
{
    [Info("No Respawn Cooldowns", "Absolut", "1.0.2")]
    [Description("Disables respawn cooldown for players with permission")]
    class NoRespawnCooldowns : RustPlugin
    {
        private const string permAllow = "norespawncooldowns.allow";

        private void Init()
        {
            permission.RegisterPermission(permAllow, this);
        }

        private void OnPlayerDie(BasePlayer player, HitInfo info)
        {
            if (permission.UserHasPermission(player.UserIDString, permAllow))
                ResetSpawnTargets(player);
        }

        private void ResetSpawnTargets(BasePlayer player)
        {
            SleepingBag[] bags = SleepingBag.FindForPlayer(player.userID, true);
            foreach (SleepingBag bag in bags)
                bag.unlockTime = 0f;
        }
    }
}
