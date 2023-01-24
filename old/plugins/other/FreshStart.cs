using System;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("FreshStart", "Canopy Sheep", "1.1.0")]
    [Description("Removes all entities when killed by another player")]

    class FreshStart : RustPlugin
    {
        #region Variables and Bools
        private ConfigData configData;
		string grade;
		string entity2;
		private const string Permission = "freshstart.excluded";

        bool CheckPermission(BasePlayer player, string perm)
        {
            if (permission.UserHasPermission(player.UserIDString, perm)) return true;
            return false;
        }

        private List<string> LootContainers = new List<string>()
        {
            "small_stash_deployed",
            "refinery_small_deployed",
            "furnace",
            "campfire",
            "vendingmachine.deployed",
            "furnace.large",
            "box.wooden.large",
            "woodbox_deployed",
            "fridge.deployed"
        };
        #endregion
        #region Config
        class ConfigData
        {
            public SettingsData Settings { get; set; }
            public ExclusionData ExclusionList { get; set; }
        }

        class SettingsData
        {
            public bool NoCorpses { get; set; }
            public bool KillAllEntitiesOnDeath { get; set; }
			public bool LimitToPVPDeath { get; set; }
            public bool DropLootFromChests { get; set; }
        }

        class ExclusionData
        {
            public List<object> Excluded { get; set; }
        }

        void TryConfig()
        {
            try
            {
                configData = Config.ReadObject<ConfigData>();
            }
            catch (Exception)
            {
				Puts("Corrupt config");
                Config.WriteObject(configData, true);
                LoadDefaultConfig();
			}
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Generating a new config file...");
            LoadConfig();
        }

        void LoadConfig()
        {
            Config.WriteObject(new ConfigData
            {
                Settings = new SettingsData
                {
                    NoCorpses = true,
                    KillAllEntitiesOnDeath = true,
					LimitToPVPDeath = true,
                    DropLootFromChests = true
                },
                ExclusionList = new ExclusionData
                {
                    Excluded = new List<object>
                    {
                        "foundation.toptier",
                        "foundation"
                    }
                }
            }, true);
        }
        #endregion
        #region Hooks
		void Init()
		{
			TryConfig();
			permission.RegisterPermission(Permission, this);
		}
		
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!configData.Settings.KillAllEntitiesOnDeath) { return; }
            if (entity == null) { return; }
            if (!entity.ToPlayer()) { return; }
            if (configData.Settings.LimitToPVPDeath && !info?.Initiator?.ToPlayer()) { return; }

            var player = entity.ToPlayer();
            if (CheckPermission(player, Permission)) { return; }

            foreach (var entityofplayer in BaseNetworkable.serverEntities.Where(x => (x as BaseEntity).OwnerID == player.userID).ToList())
            {
				entity2 = entityofplayer.ShortPrefabName;
                if (configData.Settings.DropLootFromChests && LootContainers.Contains(entity2))
                {
                    var entityofplayertest = entityofplayer as BaseCombatEntity;
                    entityofplayertest.Hurt(99999999999, Rust.DamageType.Slash);
                    continue;
                }
                if (entityofplayer is BuildingBlock)
                {
                    if (configData.ExclusionList.Excluded.Contains(entityofplayer.ShortPrefabName)) { continue; }
                    var entityasbuildingblock = entityofplayer as BuildingBlock;
					grade = entityasbuildingblock.grade.ToString().ToLower();
					if (grade == "twigs") { grade = "twig"; }
					entity2 = entityofplayer.ShortPrefabName + "." + grade;
                }
                if (configData.ExclusionList.Excluded.Contains(entity2)) { continue; }
                entityofplayer.Kill();
            }
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (!configData.Settings.NoCorpses) { return; }
            if (entity.ShortPrefabName == "player_corpse") { entity.Kill(); }
        }
        #endregion
    }
}
