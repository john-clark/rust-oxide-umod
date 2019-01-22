using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Bloodtrail", "hoppel", "1.0.3")]
    [Description("Leaves a Bloodtrail behind players while bleeding")]
    public class Bloodtrail : RustPlugin
    {
        private const string permAllow = "bloodtrail.allow";
        private const string permBypass = "bloodtrail.bypass";

        private static Bloodtrail ins;

        private void Init()
        {
            permission.RegisterPermission(permAllow, this);
            permission.RegisterPermission(permBypass, this);
        }

        private void OnServerInitialized()
        {
            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerInit(player);
            ins = this;
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (HasPerm(player) && !player.gameObject.GetComponent<Blood>())
                player.gameObject.AddComponent<Blood>();
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player.gameObject.GetComponent<Blood>())
                UnityEngine.Object.Destroy(player.gameObject.GetComponent<Blood>());
        }

        private void Unload()
        {
            var objects = UnityEngine.Object.FindObjectsOfType(typeof(Blood));

            if (objects != null)
                foreach (var gameObj in objects)
                    UnityEngine.Object.Destroy(gameObj);
        }

        private bool HasPerm(BasePlayer player)
        {
            return permission.UserHasPermission(player.UserIDString, "bloodtrail.allow") && !permission.UserHasPermission(player.UserIDString, "bloodtrail.bypass");
        }

        public class Blood : MonoBehaviour
        {
            private BasePlayer _player;
            private Vector3 _position;

            private void Awake()
            {
                _player = GetComponent<BasePlayer>();
                _position = _player.transform.position;
                InvokeRepeating("Track", 0.2f, config.refreshtime);
            }

            private void Track()
            {
                if (!_player || !ins.HasPerm(_player))
                    return;
                {
                    if (_position == _player.transform.position)
                        return;

                    _position = _player.transform.position;

                    if (!_player || !_player.IsConnected)
                    {
                        Destroy(this);
                        return;
                    }

                    if (_player.metabolism.bleeding.value > 0)
                        Effect.server.Run("assets/bundled/prefabs/fx/player/beartrap_blood.prefab", _player.transform.position, Vector3.up, null, true);
                }
            }

            private void OnDestroy()
            {
                CancelInvoke("Track");
                Destroy(this);
            }
        }

        private static Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Refresh Time")]
            public float refreshtime = 0.2f;

            public static Configuration DefaultConfig()
            {
                return new Configuration();
            }
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
                PrintWarning("Creating new config file.");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config);
    }
}
