using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Rock Block", "Nogrod", "1.1.1")]
    [Description("Blocks players from building in rocks")]
    class RockBlock : RustPlugin
    {
        private ConfigData config;
        private const string permBypass = "rockblock.bypass";
        private readonly int worldLayer = LayerMask.GetMask("World", "Default");

        private class ConfigData
        {
            public bool AllowCave { get; set; }
            public bool Logging { get; set; }
            public int MaxHeight { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData
            {
                AllowCave = false,
                Logging = true,
                MaxHeight = -1
            };
            Config.WriteObject(config, true);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["DistanceTooHigh"] = "Distance to ground too high: {0}",
                ["PlayerSuspected"] = "{0} is suspected of building {1} inside a rock at {2}!"
            }, this);
        }

        private void Init()
        {
            config = Config.ReadObject<ConfigData>();
            permission.RegisterPermission(permBypass, this);
        }

        private void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            BasePlayer player = planner.GetOwnerPlayer();
            if (player == null || permission.UserHasPermission(player.UserIDString, permBypass))
            {
                return;
            }

            RaycastHit hitInfo;
            BaseEntity entity = gameObject.GetComponent<BaseEntity>();
            if (config.MaxHeight > 0 && Physics.Raycast(new Ray(entity.transform.position, Vector3.down), out hitInfo, float.PositiveInfinity, Rust.Layers.Terrain))
            {
                if (hitInfo.distance > config.MaxHeight)
                {
                    SendReply(player, string.Format(lang.GetMessage("DistanceTooHigh", this, player.UserIDString), hitInfo.distance));
                    entity.Kill();
                    return;
                }
            }
            CheckEntity(entity, player);
        }

        private void OnItemDeployed(Deployer deployer, BaseEntity entity)
        {
            BasePlayer player = deployer.GetOwnerPlayer();
            if (player != null && !permission.UserHasPermission(player.UserIDString, permBypass))
            {
                CheckEntity(entity, player);
            }
        }

        private void CheckEntity(BaseEntity entity, BasePlayer player)
        {
            if (entity != null)
            {
                RaycastHit[] targets = Physics.RaycastAll(new Ray(entity.transform.position + Vector3.up * 200f, Vector3.down), 250, worldLayer);
                foreach (RaycastHit hit in targets)
                {
                    MeshCollider collider = hit.collider.GetComponent<MeshCollider>();
                    if (collider == null || !collider.sharedMesh.name.StartsWith("rock_") || !IsInside(hit.collider, entity) && (config.AllowCave || !IsInCave(entity)))
                    {
                        continue;
                    }

                    if (config.Logging)
                    {
                        Puts(lang.GetMessage("PlayerSuspected", this), player.displayName, entity.PrefabName, entity.transform.position);
                    }

                    entity.Kill();
                    break;
                }
            }
        }

        private bool IsInCave(BaseEntity entity)
        {
            RaycastHit[] targets = Physics.RaycastAll(new Ray(entity.transform.position, Vector3.up), 250, worldLayer);
            foreach (RaycastHit hit in targets)
            {
                if (hit.collider.name.StartsWith("rock_"))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInside(Collider collider, BaseEntity entity)
        {
            Vector3 point = entity.WorldSpaceBounds().ToBounds().max;
            Vector3 center = collider.bounds.center;
            Vector3 direction = center - point;
            Ray ray = new Ray(point, direction);
            RaycastHit hitInfo;
            return !collider.Raycast(ray, out hitInfo, direction.magnitude + 1);
        }
    }
}
