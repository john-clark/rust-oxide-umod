using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Terrain Blocker", "Slut", "2.0.0")]
    class TerrainBlocker : RustPlugin
    {
        Configuration config;
        public class Configuration
        {
            [JsonProperty("Blocked Colliders")]
            public string[] BlockedColliders { get; set; }
            [JsonProperty("Blacklist/Whitelist")]
            public Blacklist blacklist;
            public class Blacklist
            {
                [JsonProperty("Prefabs (Leave empty to ignore)")]
                public string[] Prefabs { get; set; }
                [JsonProperty("Blacklist = True | Whitelist = False")]
                public bool _Blacklist { get; set; }
            }
            public static Configuration LoadDefaults()
            {
                return new Configuration
                {
                    BlockedColliders = new string[]
                    {
                        "iceberg",
                        "ice_berg",
                        "ice_sheet",
                        "icesheet",
                    },
                    blacklist = new Blacklist
                    {
                        Prefabs = new string[]
                        {
                            "prefab.fullname"
                        },
                        _Blacklist = true
                    }
                };
            }
        }
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating new configuration!");
            config = Configuration.LoadDefaults();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();
        }
        protected override void SaveConfig() => Config.WriteObject(config);

        private void Loaded()
        {
            permission.RegisterPermission(adminPermission, this);
        }
        const string adminPermission = "icebergblocker.admin";

        private object CanBuild(Planner plan, Construction prefab)
        {
            var player = plan.GetOwnerPlayer();
            Vector3 pos = plan.transform.position;
            if (player != null && !permission.UserHasPermission(player.UserIDString, adminPermission))
            {
                List<Collider> list = new List<Collider>();
                Vis.Colliders(pos, 5f, list);
                if (list.Any(x => config.BlockedColliders.Any(x.name.StartsWith)))
                {
                    if (config.blacklist.Prefabs.Length > 0)
                    {
                        bool contains = config.blacklist.Prefabs.Contains(prefab.fullName);
                        if ((config.blacklist._Blacklist && !contains) || (!config.blacklist._Blacklist && contains))
                        {
                            return null;
                        }
                    }
                    return false;
                }
            }
            return null;
        }
    }
}
