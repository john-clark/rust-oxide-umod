namespace Oxide.Plugins
{
    [Info("LimitedLadders", "redBDGR", "2.0.1")]
    [Description("Prevents the placement of ladders where building is blocked")]

    class LimitedLadders : RustPlugin
    {
        private void Unload()
        {
            AllowBuildingBypass(true);
        }

        private void OnServerInitialized()
        {
            AllowBuildingBypass(false);
        }

        private void AllowBuildingBypass(bool allow)
        {
            Construction con = PrefabAttribute.server.Find<Construction>(2150203378);
            if (con)
                con.canBypassBuildingPermission = allow;
        }
    }
}
