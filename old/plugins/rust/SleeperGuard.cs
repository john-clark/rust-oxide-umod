using Newtonsoft.Json;
using Oxide.Core;
using Rust;
using System;
using System.IO;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Sleeper Guard", "Wulf/lukespragg", "0.5.0")]
    [Description("Protects sleeping players from being hurt, killed, or looted")]
    public class SleeperGuard : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Damage delay in seconds (0 to disable)")]
            public int DamageDelay { get; set; } = 30;

            [JsonProperty(PropertyName = "Loot delay in seconds (0 to disable)")]
            public int LootDelay { get; set; } = 30;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            string configPath = $"{Interface.Oxide.ConfigDirectory}{Path.DirectorySeparatorChar}{Name}.json";
            LogWarning($"Could not load a valid configuration file, creating a new configuration file at {configPath}");
            config = new Configuration();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion Configuration

        #region Initialization

        private readonly Hash<string, long> sleepers = new Hash<string, long>();
        private readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);

        private const string permDamageProtection = "sleeperguard.damageprotection";
        private const string permLootProtection = "sleeperguard.lootprotection";
        private const string permNoDamageDelay = "sleeperguard.nodamagedelay";
        private const string permNoLootDelay = "sleeperguard.nolootdelay";

        private long Timestamp => (long)DateTime.UtcNow.Subtract(epoch).TotalSeconds;

        private void OnServerInitialized()
        {
            permission.RegisterPermission(permDamageProtection, this);
            permission.RegisterPermission(permLootProtection, this);
            permission.RegisterPermission(permNoDamageDelay, this);
            permission.RegisterPermission(permNoLootDelay, this);

            foreach (BasePlayer sleeper in BasePlayer.sleepingPlayerList)
            {
                if (!sleepers.ContainsKey(sleeper.UserIDString))
                {
                    sleepers.Add(sleeper.UserIDString, Timestamp);
                }
            }
        }

        #endregion Initialization

        #region Damage Protection

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            BasePlayer target = entity.ToPlayer();
            BasePlayer attacker = info.InitiatorPlayer;

            if (target != null && target.IsSleeping())
            {
                bool delayLeft = Timestamp - sleepers[target.UserIDString] > config.DamageDelay;

                if (delayLeft || attacker != null && permission.UserHasPermission(attacker.UserIDString, permNoDamageDelay))
                {
                    if (permission.UserHasPermission(target.UserIDString, permDamageProtection))
                    {
                        NullifyDamage(ref info);
                    }
                }
            }
        }

        private static void NullifyDamage(ref HitInfo info)
        {
            info.damageTypes = new DamageTypeList();
            info.HitMaterial = 0;
            info.PointStart = Vector3.zero;
        }

        #endregion Damage Protection

        #region Loot Protection

        private object CanLootPlayer(BasePlayer target, BasePlayer looter)
        {
            string targetId = target.UserIDString;

            if (target.IsSleeping())
            {
                bool delayLeft = Timestamp - sleepers[targetId] > config.LootDelay;

                if (delayLeft || permission.UserHasPermission(looter.UserIDString, permNoLootDelay))
                {
                    if (permission.UserHasPermission(target.UserIDString, permLootProtection))
                    {
                        return false;
                    }
                }
            }

            return null;
        }

        #endregion Loot Protection

        #region Sleeper Handling

        private void OnPlayerSleep(BasePlayer player)
        {
            if (sleepers.ContainsKey(player.UserIDString))
            {
                sleepers.Remove(player.UserIDString);
            }

            sleepers.Add(player.UserIDString, Timestamp);
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (sleepers.ContainsKey(player.UserIDString))
            {
                sleepers.Remove(player.UserIDString);
            }
        }

        #endregion Sleeper Handling
    }
}
