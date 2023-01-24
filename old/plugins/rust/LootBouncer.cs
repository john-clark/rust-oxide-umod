using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Loot Bouncer", "Sorrow", "0.3.1")]
    [Description("Empty the containers when players do not pick up all the items")]

    class LootBouncer : RustPlugin
    {
        [PluginReference]
        Plugin Slap, Trade;

        Dictionary<uint, int> lootEntity = new Dictionary<uint, int>();
        private float _timeBeforeLootDespawn;
        private bool _emptyAirdrop;
        private bool _emptyCrashsite;
        private bool _slapPlayer;

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (entity == null || player == null) return;
            if (Trade != null && Trade.Call<bool>("IsTradeBox", entity)) return;

            var entityId = entity.net.ID;
            var loot = entity.GetComponent<LootContainer>();
            if (loot == null || LootContainer.spawnType.AIRDROP.Equals(loot.SpawnType) && !_emptyAirdrop || LootContainer.spawnType.CRASHSITE.Equals(loot.SpawnType) && !_emptyCrashsite) return;

            var originalValue = 0;
            if (lootEntity.TryGetValue(entityId, out originalValue))
            {
                originalValue = loot.inventory.itemList.Count;
            }
            else
            {
                lootEntity.Add(entityId, loot.inventory.itemList.Count);
            }
        }

        private void OnLootEntityEnd(BasePlayer player, BaseEntity entity)
        {
            if (entity == null || player == null) return;
            if (Trade != null && Trade.Call<bool>("IsTradeBox", entity)) return;

            if (entity.net == null) return;
            var entityId = entity.net.ID;
            var loot = entity.GetComponent<LootContainer>();
            if (loot == null || LootContainer.spawnType.AIRDROP.Equals(loot.SpawnType) && !_emptyAirdrop || LootContainer.spawnType.CRASHSITE.Equals(loot.SpawnType) && !_emptyCrashsite) return;

            var originalValue = 0;
            if (lootEntity.TryGetValue(entityId, out originalValue))
            {
                if (loot.inventory.itemList.Count < originalValue)
                {
                    if (loot.inventory.itemList.Count == 0) return;
                    if (Slap != null && _slapPlayer) Slap.Call("SlapPlayer", player.IPlayer);
                    timer.Once(_timeBeforeLootDespawn, () =>
                    {
                        if (loot == null) return;
                        DropUtil.DropItems(loot?.inventory, loot.transform.position);
                        BaseNetworkable.serverEntities.Find(entityId)?.Kill();
                    });
                }
                lootEntity.Remove(entityId);
            }
        }

        private new void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Config.Clear();

            Config["Time before the loot containers are empties"] = 30;
            Config["Empty the airdrops"] = false;
            Config["Empty the crates of the crashsites"] = false;
            Config["Slaps players who don't empty containers"] = false;

            SaveConfig();
        }

        private void OnServerInitialized()
        {
            _timeBeforeLootDespawn = Convert.ToInt32(Config["Time before the loot containers are empties"]);
            _emptyAirdrop = Convert.ToBoolean(Config["Empty the airdrops"]);
            _emptyCrashsite = Convert.ToBoolean(Config["Empty the crates of the crashsites"]);
            _slapPlayer = Convert.ToBoolean(Config["Slaps players who don't empty containers"]);

            if (Slap == null && _slapPlayer)
            {
                PrintWarning("Slap is not loaded, get it at https://umod.org");
            }
        }
    }
}