namespace Oxide.Plugins
{
    [Info("No Loot", "Wulf/lukespragg", "1.0.0", ResourceId = 1488)]
    [Description("Removes all loot containers and prevents them from spawning")]
    public class NoLoot : CovalencePlugin
    {
        private bool serverReady = false;

        #region Loot Handling

        private bool ProcessContainers(BaseEntity entity)
        {
            if (!entity.isActiveAndEnabled || entity.IsDestroyed) return false;
            if (!(entity is LootContainer || entity is JunkPile)) return false;

            var junkPile = entity as JunkPile;
            if (junkPile != null)
            {
                junkPile.CancelInvoke("TimeOut");
                junkPile.CancelInvoke("CheckEmpty");
                junkPile.CancelInvoke("Effect");
                junkPile.CancelInvoke("SinkAndDestroy");
                junkPile.Kill();
            }
            else
                entity.Kill();

            return true;
        }

        private void OnServerInitialized()
        {
            var loot = UnityEngine.Resources.FindObjectsOfTypeAll<LootContainer>();
            var count = 0;
            foreach (var entity in loot)
                if (ProcessContainers(entity)) count++;
            Puts($"Removed {count} loot containers");
            serverReady = true;
        }

        private void OnEntitySpawned(BaseEntity entity)
        {
            if (!serverReady) return;
            if (entity.OwnerID == 0) ProcessContainers(entity);
        }

        #endregion
    }
}