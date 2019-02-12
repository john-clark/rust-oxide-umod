using System.Collections.Generic;
namespace Oxide.Plugins
{
    [Info("Wounded Screams", "Death", "2.2.0")]
    class WoundedScreams : RustPlugin
    {
        #region Declarations
        Dictionary<ulong, Timer> Collection = new Dictionary<ulong, Timer>();
        List<string> Hooks = new List<string>
        {
            "OnPlayerRecover",
            "OnEntityDeath",
            "OnPlayerDisconnected"
        };
        bool Sub;
        const string perm = "woundedscreams.exclude";
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfigVariables();
            Unsub();
            if (configData.Options.Enable_Permission)
                permission.RegisterPermission(perm, this);
        }
        object OnPlayerWound(BasePlayer p)
        {
            if (!permission.UserHasPermission(p.UserIDString, perm) && !Collection.ContainsKey(p.userID))
            {
                PlayFX(p);
                Collection.Add(p.userID, timer.Every(6, ()
                    => PlayFX(p)));
            }
            if (!Sub)
            {
                foreach (var h in Hooks)
                    Subscribe(h);
                Sub = true;
            }
            return null;
        }
        object OnPlayerRecover(BasePlayer p)
        {
            Destroy(p);
            return null;
        }
        void OnEntityDeath(BaseCombatEntity e)
            => Destroy(e as BasePlayer);
        void OnPlayerDisconnected(BasePlayer p)
            => Destroy(p);
        #endregion

        #region Functions
        void PlayFX(BasePlayer p)
            => Effect.server.Run(configData.Options.FX_Sound, p.transform.position);
        void Destroy(BasePlayer p)
        {
            if (p != null && Collection.ContainsKey(p.userID))
            {
                Collection[p.userID].Destroy();
                Collection.Remove(p.userID);
            }
            if (Collection.Count == 0)
                Unsub();
        }
        void Unsub()
        {
            foreach (var h in Hooks)
                Unsubscribe(h);
            Sub = false;
        }
        #endregion

        #region Config
        private ConfigData configData;
        class ConfigData
        {
            public Options Options = new Options();
        }
        class Options
        {
            public bool Enable_Permission = false;
            public string FX_Sound = "assets/bundled/prefabs/fx/player/beartrap_scream.prefab";
        }
        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();
            SaveConfig(configData);
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData();
            SaveConfig(config);
        }
        void SaveConfig(ConfigData config)
            => Config.WriteObject(config, true);
        #endregion
    }
}