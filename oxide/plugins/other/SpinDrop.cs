using System;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Spin Drop", "Iv Misticos", "1.0.4")]
    [Description("Spin around dropped items")]
    class SpinDrop : RustPlugin
    {
        #region Configuration

        private static Configuration _config;
        
        private class Configuration
        {
            [JsonProperty(PropertyName = "Speed Modifier")]
            public float SpeedModifier = 125f;
            
            [JsonProperty(PropertyName = "Move Item UP On N")]
            public float HeightOnDrop = 0.4f;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
            }
            catch
            {
                Config.WriteObject(_config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}.jsonError");
                PrintError("The configuration file contains an error and has been replaced with a default config.\n" +
                           "The error configuration file was saved in the .jsonError extension");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion
        
        #region Hooks

        private void OnItemDropped(Item item, BaseEntity entity)
        {
            var gameObject = item?.GetWorldEntity()?.gameObject;
            if (gameObject == null)
                return;
            
            gameObject.AddComponent<SpinDropControl>();
        }

        #endregion
        
        #region Controller

        public class SpinDropControl : MonoBehaviour
        {
            private bool _triggered = false;
            
            private void OnCollisionEnter(Collision collision)
            {
                if (_triggered || collision.gameObject.ToBaseEntity()?.GetItem() != null)
                    return;
                
                var rigidbody = gameObject.GetComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;

                var t = transform;
                var pos = t.position;
                t.rotation = Quaternion.Euler(0, 0, 0);
                t.position = new Vector3(pos.x, pos.y + _config.HeightOnDrop, pos.z);
                _triggered = true;
            }

            private void FixedUpdate()
            {
                gameObject.transform.Rotate(Vector3.down * Time.deltaTime * _config.SpeedModifier);
            }
        }
        
        #endregion
    }
}