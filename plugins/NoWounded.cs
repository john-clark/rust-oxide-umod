namespace Oxide.Plugins
{
    [Info("NoWounded", "k1lly0u", "0.1.0", ResourceId = 0)]
    class NoWounded : RustPlugin
    {
        private Hash<ulong, HitData> lastHits = new Hash<ulong, HitData>();

        class HitData
        {
            public Timer removeIn;
            public HitInfo info;
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            try
            {
                var victim = entity.ToPlayer();
                if (victim != null && info != null)
                {
                    HitData data;
                    if (lastHits.TryGetValue(victim.userID, out data))
                        data.removeIn.Destroy();

                    lastHits[victim.userID] = new HitData { info = info, removeIn = timer.In(3, () => lastHits.Remove(victim.userID)) };
                }
            }
            catch { }
        }
        object OnPlayerWound(BasePlayer player)
        {
            player.Die(lastHits.ContainsKey(player.userID) ? lastHits[player.userID].info : null);
            return true;
        }       
    }
}