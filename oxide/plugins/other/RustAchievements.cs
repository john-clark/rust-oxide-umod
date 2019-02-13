using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("RustAchievements", "open_maibox", "0.1")]
    [Description("An achievements system for a Rust server.")]
    class RustAchievements : RustPlugin
    {
        abstract class Achievement
        {
            public bool IsComplete = false;
            public BasePlayer Player { get; set; }

            public abstract bool Advance();
        }

        class BearsKilled : Achievement
        {
            public const uint BEAR_PREFAB_ID = 2826174939;
            public const int TARGET = 3;
            public int Killed { get; set; }

            public override bool Advance()
            {
                Killed++;

                if (Killed >= BearsKilled.TARGET && !IsComplete)
                {
                    IsComplete = true;
                    return true;
                }

                return false;
            }
        }

        private Dictionary<BasePlayer, List<Achievement>> database = new Dictionary<BasePlayer, List<Achievement>>();

        void Init()
        {
            Puts("RustAchievements initialized.");
        }

        void Loaded()
        {
            Puts("RustAchievements loaded.");
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Loading default config file for RustAchievements.");
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity.lastAttacker == null) return; // i.e. when a plant is harvested

            if (entity.prefabID == BearsKilled.BEAR_PREFAB_ID)
            {
                var player       = entity.lastAttacker.ToPlayer();
                var achievements = null as List<Achievement>;

                if (database.ContainsKey(player))
                {
                    achievements = database[player];
                } else
                {
                    achievements = new List<Achievement>();
                    database[player] = achievements;
                }

                var bearAchievement = achievements.Find(x => x.GetType() == typeof(BearsKilled)) as BearsKilled;

                if (bearAchievement == null)
                {
                    bearAchievement = new BearsKilled();
                    bearAchievement.Player = player;
                    achievements.Add(bearAchievement);
                }

                if (bearAchievement.Advance())
                {
                    Puts(player.displayName + " has earned the achievement for slaughtering helpless little bears.");
                }

                Puts(player.displayName + " has killed " + bearAchievement.Killed + " of " + BearsKilled.TARGET + " bears.");
            }
        }
    }
}
