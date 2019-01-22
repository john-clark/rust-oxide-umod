using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Oxide.Game.Rust.Cui;
using UnityEngine;
namespace Oxide.Plugins
{
    [Info("Wounded Screams", "Death/Skipcast", "2.1.7")]
    [Description("Restores the screams when a player gets wounded.")]
    public class WoundedScreams : RustPlugin
    {

        #region Declarations
        private const string effectName = "assets/bundled/prefabs/fx/player/beartrap_scream.prefab";
        private readonly Dictionary<BasePlayer, Scream> screams = new Dictionary<BasePlayer, Scream>();
        private readonly Dictionary<ulong, string> openUis = new Dictionary<ulong, string>();
        #endregion

        #region Functions
        private class PluginConfig
        {
            public bool ScreamOnDemand = false;
        }

        private class Scream
        {
            public float NextPlay;
            private float GetRandomDelay() => 6f;
            public void ApplyDelay() => NextPlay = Time.time + GetRandomDelay();
            public void Play(Vector3 position) => Effect.server.Run(effectName, position);
        }       

        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating default configuration");
            config = new PluginConfig();
            Config.Clear();
            Config.WriteObject(config);
        }

        void Init() => config = Config.ReadObject<PluginConfig>();

        private void OnServerInitialized()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"helptext", "Press your USE key to scream"}
            }, this, "en");
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        private void AddPlayerScream(BasePlayer player)
        {
            if (screams.ContainsKey(player))
            {
                Debug.LogWarning("Trying to add more than 1 scream to player.");
                return;
            }
            var scream = new Scream();
            screams.Add(player, scream);
            CreateUI(player);
        }

        private void RemovePlayerScream(BasePlayer player)
        {
            DestroyUI(player);
            if (screams.ContainsKey(player)) screams.Remove(player);
        }

        private void CreateUI(BasePlayer player)
        {
	        if (!config.ScreamOnDemand) return;
            DestroyUI(player);
            bool canScream = screams[player].NextPlay <= Time.time;
            var ui = new CuiElementContainer();
            var rootPanelName = ui.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0"
                },
                RectTransform =
                {
                    AnchorMin = "0 0.924",
                    AnchorMax = "1 1"
                }
            });
            ui.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                },
                Text =
                {
                    Text = lang.GetMessage("helptext", this, player.UserIDString),
                    Align = TextAnchor.LowerCenter,
                    Color = canScream ? "0.968 0.921 0.882 1" : "0.968 0.921 0.882 0.5",
                    FontSize = canScream ? 14 : 13
                }
            }, rootPanelName);
            openUis.Add(player.userID, rootPanelName);
            CuiHelper.AddUi(player, ui);
        }

        private void DestroyUI(BasePlayer player)
        {
            if (!config.ScreamOnDemand || !openUis.ContainsKey(player.userID)) return;
            CuiHelper.DestroyUi(player, openUis[player.userID]);
            openUis.Remove(player.userID);
        }
        #endregion

        #region Rust hooks
        void OnTick()
        {
            foreach (var kv in screams)
            {
                if (Time.time >= kv.Value.NextPlay)
                {
                    if (!kv.Key || kv.Key.IsDestroyed || !kv.Key.IsConnected || !kv.Key.IsWounded()) continue;
                    if ((config.ScreamOnDemand && kv.Key.serverInput.WasJustPressed(BUTTON.USE)) || !config.ScreamOnDemand)
                    {
                        Vector3 position = kv.Key.GetNetworkPosition();
                        kv.Value.Play(position);
                        kv.Value.ApplyDelay();
                        CreateUI(kv.Key);
                        timer.In(kv.Value.NextPlay - Time.time, () =>
                        {
                            if (!screams.ContainsKey(kv.Key)) return;
                            CreateUI(kv.Key);
                        });
                    }
                }
            }
        }

        void Unload()
        {
            foreach (var kv in openUis.ToDictionary(kv => kv.Key, kv => kv.Value))
            {
                var player = BasePlayer.FindByID(kv.Key);
                DestroyUI(player);
            }
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            var player = entity as BasePlayer;
            if (player != null) RemovePlayerScream(player);
        }

        void OnPlayerWound(BasePlayer player)
        {
            if (!player || !player.gameObject || player.IsDestroyed) return;
            AddPlayerScream(player);
        }

        void OnPlayerRecover(BasePlayer player) => RemovePlayerScream(player);

        void OnEntityKill(BaseNetworkable entity)
        {
            var player = entity as BasePlayer;
            if (player != null) RemovePlayerScream(player);
        }

        void OnPlayerInit(BasePlayer player)
        {
            if (player.IsWounded()) AddPlayerScream(player);
        }

        void OnPlayerDisconnected(BasePlayer player, string reason) => RemovePlayerScream(player);
        #endregion
    }
}