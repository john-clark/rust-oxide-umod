using Rust;
using Oxide.Core;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("RotatingDeathBags", "k1lly0u", "0.1.0", ResourceId = 0)]
    class RotatingDeathBags : RustPlugin
    {
        #region Fields
        static RotatingDeathBags instance;
        static int[] layerTypes = new int[] { 4, 8, 16, 21, 23, 25, 26 };

        private bool initialized;
        #endregion

        #region Oxide Hooks       
        void OnServerInitialized()
        {
            instance = this;
            LoadVariables();
            initialized = true;
        }
        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (initialized)
            {
                if (entity.GetComponent<DroppedItemContainer>())
                {
                    NextTick(() =>
                    {
                        if (entity != null)
                            entity.gameObject.AddComponent<BagRotator>();
                    });
                }
            }
        }
        void Unload()
        {
            var objects = UnityEngine.Object.FindObjectsOfType<BagRotator>();
            if (objects != null)
            {
                foreach (var obj in objects)
                    UnityEngine.Object.Destroy(obj);
            }
            instance = null;
        }
        #endregion

        #region Item Rotator           
        class BagRotator : MonoBehaviour
        {
            private DroppedItemContainer entity;
            private Rigidbody rigidBody;
            private bool hasBegun;

            private float secsToTake;
            void Awake()
            {
                entity = GetComponent<DroppedItemContainer>();
                enabled = false;
            }
            void OnDestroy()
            {
                CancelInvoke();
                if (entity == null) return;
                if (rigidBody != null)
                {
                    rigidBody.useGravity = true;
                    rigidBody.isKinematic = false;
                }
            }
            void FixedUpdate()
            {
                entity.transform.RotateAround(entity.transform.position, Vector3.up, secsToTake);
                entity.transform.hasChanged = true;
            }
            void OnCollisionEnter(Collision collision)
            {
                if (hasBegun || collision.gameObject == null) return;

                if (layerTypes.Contains(collision.gameObject.layer) || collision.gameObject.name.Contains("junk_pile"))
                {
                    hasBegun = true;
                    Invoke("BeginRotation", instance.configData.RotateIn);
                }
            }
            private void BeginRotation()
            {
                gameObject.layer = (int)Layer.Reserved1;

                rigidBody = entity.GetComponent<Rigidbody>();
                rigidBody.useGravity = false;
                rigidBody.isKinematic = true;
                rigidBody.detectCollisions = true;
                rigidBody.collisionDetectionMode = CollisionDetectionMode.Discrete;

                secsToTake = instance.configData.Speed;

                entity.transform.position = entity.transform.position + Vector3.up;
                enabled = true;
            }
        }
        #endregion

        #region Config        
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Rotation speed")]
            public float Speed { get; set; }
            [JsonProperty(PropertyName = "Seconds before initiating rotation")]
            public float RotateIn { get; set; }
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                Speed = 5f,
                RotateIn = 3f
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion
    }
}