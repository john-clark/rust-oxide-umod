namespace Oxide.Plugins
{
    [Info("MedicRevive", "k1lly0u", "0.1.0", ResourceId = 0)]
    class MedicRevive : RustPlugin
    {
        void Loaded() => permission.RegisterPermission("medicrevive.use", this);
        void OnHealingItemUse(MedicalTool tool, BasePlayer player)
        {
            if (tool.ShortPrefabName == "syringe_medical.entity")
            {
                var healingPlayer = tool.GetOwnerPlayer();
                if (healingPlayer != null)
                {
                    if (permission.UserHasPermission(healingPlayer.UserIDString, "medicrevive.use"))
                        player.StopWounded();
                }
            }
        }       
    }
}