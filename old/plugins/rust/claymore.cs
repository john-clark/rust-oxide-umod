using System;
using System.Linq;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;

namespace Oxide.Plugins
{
    [Info("Claymore", "wazzzup", "1.0.0")]
    [Description("Converts land mines to Claymore antipersonnel mines")]
    public class Claymore : RustPlugin
    {
        [PluginReference] Plugin Friends;

        public static Claymore instance;

        protected override void LoadDefaultConfig()
        {
            PrintWarning("New configuration file created");
            Config.Clear();
            LoadVariables();
        }

        private float playerDetectRadius;
        private float minExplosionDistance;
        private float maxExplosionDistance;
        private bool configChanged = false;

        void LoadVariables()
        {
            playerDetectRadius = Convert.ToSingle(GetConfig("playerDetectRadius", 3f));
            minExplosionDistance = Convert.ToSingle(GetConfig("minExplosionDistance", 2f));
            maxExplosionDistance = Convert.ToSingle(GetConfig("maxExplosionDistance", 4f));
            if (configChanged)
            {
                SaveConfig();
                configChanged = false;
            }
        }

        private object GetConfig(string dataValue, object defaultValue)
        {
            object value = Config[dataValue];
            if (value == null)
            {
                value = defaultValue;
                Config[dataValue] = value;
                configChanged = true;
            }
            return value;
        }

        public class ClaymoreTrigger: MonoBehaviour
        {
            ulong ownerID;

            void Awake()
            {
                ownerID = this.GetComponentInParent<BaseEntity>().OwnerID;
                gameObject.layer = (int)Layer.Reserved1;
                gameObject.name = "Claymore";

                var landmine = this.GetComponentInParent<Landmine>();
                landmine.minExplosionRadius = instance.minExplosionDistance;
                landmine.explosionRadius = instance.maxExplosionDistance;

                var sphereCollider = gameObject.AddComponent<SphereCollider>();
                sphereCollider.isTrigger = true;
                sphereCollider.radius = instance.playerDetectRadius;
            }

            private void OnTriggerEnter(Collider col)
            {
                var target = col.GetComponentInParent<BasePlayer>();
                if (target != null)
                {
                    if (target.userID == ownerID) return;
                    else if (Convert.ToBoolean(instance.Friends?.CallHook("AreFriends", target.userID, ownerID))) return;
                    this.GetComponentInParent<Landmine>().Explode();
                }
            }
        }

        void Init()
        {
            instance = this;
            LoadVariables();
        }

        void OnServerInitialized()
        {
            foreach (var ent in UnityEngine.Object.FindObjectsOfType<Landmine>())
            {
                ent.gameObject.AddComponent<ClaymoreTrigger>();
            }
        }

        private void Unload()
        {
            foreach (var ent in UnityEngine.Object.FindObjectsOfType<ClaymoreTrigger>())
            {
                UnityEngine.Object.Destroy(ent);
            }
        }

        void OnEntityBuilt(Planner plan, GameObject obj)
        {
            if (obj == null || obj.GetComponentInParent<Landmine>() == null) return;
            obj.AddComponent<ClaymoreTrigger>();
        }

        object OnTrapTrigger(BaseTrap trap, GameObject obj)
        {
            if (trap is Landmine)
            {
                BasePlayer target = obj.GetComponent<BasePlayer>();
                if (target)
                {
                    if (target.userID == trap.OwnerID)
                        return false;
                    else if (Convert.ToBoolean(Friends?.CallHook("AreFriends", target.userID, trap.OwnerID)))
                        return false;
                }
            }
            return null;
        }

    }
}
