using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
#if RUST
using UnityEngine;
#endif

namespace Oxide.Plugins
{
    [Info("Respawner", "Wulf/lukespragg", "1.0.3")]
    [Description("Automatically respawns players with permission and optionally wakes them up")]
    public class Respawner : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Automatically wake up (true/false)")]
            public bool AutoWakeUp;

            /*[JsonProperty(PropertyName = "Respawn at custom location (true/false)")]
            public bool CustomLocation;*/

            [JsonProperty(PropertyName = "Respawn at same location (true/false)")]
            public bool SameLocation;

            //[JsonProperty(PropertyName = "Show chat respawned messages (true/false)")]
            //public bool ShowMessages;

#if RUST
            [JsonProperty(PropertyName = "Use sleeping bags if available (true/false)")]
            public bool SleepingBags;
#endif

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    AutoWakeUp = true,
                    //CustomLocation = false,
                    SameLocation = true,
                    //ShowMessages = true,
#if RUST
                    SleepingBags = true
#endif
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.AutoWakeUp == null) LoadDefaultConfig();
            }
            catch
            {
                LogWarning($"Could not read oxide/config/{Name}.json, creating new config file");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion Configuration

        #region Localization

        /*private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AtLocation"] = "You've respawned at {0}",
                ["AtSameLocation"] = "You've respawned at the same location",
                ["AtSleepingBag"] = "You've respawned at your sleeping bag"
            }, this);
        }*/

        #endregion Localization

        #region Initialization

        private System.Random random = new System.Random();

        private const string permUse = "respawner.use";

        private void Init() => permission.RegisterPermission(permUse, this);

        #endregion Initialization

        #region Respawn Handling

#if RUST
        private BasePlayer.SpawnPoint FindSpawnPoint(BasePlayer basePlayer)
        {
            var player = basePlayer.IPlayer;
            var spawnPoint = new BasePlayer.SpawnPoint();
            var bags = FindSleepingBags(basePlayer);
            #if DEBUG
            LogWarning($"# of sleeping bags for {player.Name}: {bags.Length}");
            #endif

            if (config.SleepingBags && bags.Length >= 1)
            {
                var bag = bags[random.Next(0, bags.Length - 1)];
                # if DEBUG
                LogWarning($"Original location for {player.Name}: {player.Position()}");
                LogWarning($"Target location for {player.Name}: {bag.transform.position}");
                #endif
                var pos = bag.transform.position;
                spawnPoint.pos = new Vector3(pos.x, pos.y + 0.3f, pos.z);
                spawnPoint.rot = bag.transform.rotation;
            }
            else if (config.SameLocation)
            {
                var pos = basePlayer.transform.position;
                spawnPoint.pos = new Vector3(pos.x, pos.y + 0.3f, pos.z);
                spawnPoint.rot = basePlayer.transform.rotation;
            }
            /*else if (config.CustomLocation)
            {
                // TODO: Implement custom spawn location(s) (universal)
            }*/

            return spawnPoint;
        }

        private void OnEntityDeath(BaseEntity entity)
        {
            var basePlayer = entity.ToPlayer();
            if (basePlayer == null || !permission.UserHasPermission(basePlayer.UserIDString, permUse)) return;

            NextTick(() => { if (basePlayer.IsDead() && basePlayer.IsConnected) basePlayer.Respawn(); });
        }

        private BasePlayer.SpawnPoint OnPlayerRespawn(BasePlayer basePlayer) => FindSpawnPoint(basePlayer);

        private void OnPlayerRespawned(BasePlayer basePlayer)
        {
            if (permission.UserHasPermission(basePlayer.UserIDString, permUse) && basePlayer.IsSleeping()) basePlayer.EndSleeping();
        }
#else
        private void OnUserRespawned(IPlayer player)
        {
            //player.Teleport(); // TODO: Support for other games
        }
#endif

        #endregion Respawn Handling

        #region Helpers

        //private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

#if RUST
        private SleepingBag[] FindSleepingBags(BasePlayer basePlayer)
        {
            var bags = SleepingBag.FindForPlayer(basePlayer.userID, true);
            return bags.Where((SleepingBag b) => b.deployerUserID == basePlayer.userID).ToArray();
        }
#endif

        #endregion Helpers
    }
}
