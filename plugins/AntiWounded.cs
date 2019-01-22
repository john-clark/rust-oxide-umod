// ReSharper disable UnusedMember.Local
namespace Oxide.Plugins
{
    [Info("Anti-Wounded", "Iv Misticos", "3.0.1")]
    [Description("Players will skip the wounded state before dying.")]
    class AntiWounded : RustPlugin
    {
        private const string PermUse = "antiwounded.use";
        
        private void OnServerInitialized() => permission.RegisterPermission(PermUse, this);

        private object CanBeWounded(BasePlayer player, HitInfo info)
        {
            if (permission.UserHasPermission(player.UserIDString, PermUse))
                return false;

            return null;
        }
    }
}