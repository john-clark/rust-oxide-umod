using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Rust;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Barricades", "Xianith / redBDGR", "0.0.3", ResourceId = 2460)]
    [Description("Legacy wooden barricade made out of double stacked sign posts. Can be picked up.")]
    class Barricades : RustPlugin
    {
        static Barricades bCades = null;
        Dictionary<BasePlayer, BaseEntity> barricaders = new Dictionary<BasePlayer, BaseEntity>();
        Dictionary<string, BarricadeGui> BarricadeGUIinfo = new Dictionary<string, BarricadeGui>();

        class BarricadeGui { public string panel; public BaseEntity entity; }

        #region Config
        public bConfig Settings { get; private set; }
        public class bConfig
        {
            [JsonProperty(PropertyName = "Initial Health of a placed Barricade")]
            public int Health { get; set; }

            [JsonProperty(PropertyName = "Can be picked up")]
            public bool CanBePicked { get; set; }

            public static bConfig DefaultConfig() {
                return new bConfig
                {
                    Health = 50,
                    CanBePicked = true
                };
            }
        }

        protected override void LoadConfig() {
            base.LoadConfig();
            Settings = Config.ReadObject<bConfig>();
            SaveConfig();
        }

        protected override void LoadDefaultConfig() { Settings = bConfig.DefaultConfig(); }
        protected override void SaveConfig() { Config.WriteObject(Settings); }
        #endregion

        void Unload() {
            foreach (BasePlayer player in BasePlayer.activePlayerList) {
                DestroyBarrUi(player);
            }
        }

        void Init() {
            bCades = this;
            permission.RegisterPermission("barricades.add", this);

            var Entity = UnityEngine.Object.FindObjectsOfType<BaseEntity>().ToList();

            foreach (BaseEntity e in Entity)
            {
                if (e.name.Contains("sign.post.town") && e.skinID == 1510599461) { e.Kill(); }
                // h?.Kill();
            }

        }

        void OnPlayerInput(BasePlayer player, InputState input) {
            if (Settings.CanBePicked == false) return;
            if (!input.IsDown(BUTTON.USE)) return;
            if (!barricaders.ContainsKey(player)) return;

            BaseEntity entity = barricaders[player];
            if (!entity || entity == null || !entity.IsValid()) {
                barricaders.Remove(player);
                return;
            }

            timer.Once(0.001f, () => {
                entity.Kill();
                DestroyBarrUi(player);
                createBarricade(player);
                return;
            });
        }

        void OnEntitySpawned(BaseNetworkable entityn) {
            BaseEntity parent = entityn as BaseEntity;

            if (parent.ShortPrefabName != "sign.post.town") return;
            if (parent.skinID != 1510599461) return;

            parent.gameObject.AddComponent<BarricadeRadius>();

            var ownerID = parent.OwnerID;

            BasePlayer player = BasePlayer.FindByID(ownerID);

            var ent = GameManager.server.CreateEntity("assets/prefabs/deployable/signs/sign.post.town.prefab", new Vector3(0, -1.05f, 0), Quaternion.Euler(0, 0, 0));

            ent.SetParent(parent);

            parent.SetFlag(BaseEntity.Flags.Busy, true);
            ent.SetFlag(BaseEntity.Flags.Busy, true);

            parent.GetComponent<BaseCombatEntity>().health = Settings.Health;
            ent.GetComponent<BaseCombatEntity>().startHealth = Settings.Health;

            ent.UpdateNetworkGroup();
            ent.SendNetworkUpdateImmediate();
            ent.Spawn();

            // decay(ent);
        }

        void decay(BaseEntity ent) {

        }

        class BarricadeRadius : MonoBehaviour {
            BaseEntity entity;
            public bool isEnabled;

            private void Awake() {
                entity = GetComponent<BaseEntity>();
                var collider = entity.gameObject.AddComponent<SphereCollider>();
                collider.gameObject.layer = (int)Layer.Reserved1;
                collider.radius = 1.5f;
                // collider.material = "assets/content/properties/materialconfig/dunes blended.asset";
                collider.isTrigger = true;
                isEnabled = true;
            }

            private void OnTriggerEnter(Collider col) {
                var player = col.GetComponent<BasePlayer>();
                if (player == null) return;
                if (!player.IsValid()) return;

                if (bCades.barricaders.ContainsKey(player))
                    bCades.barricaders.Remove(player);
                bCades.CreateBarrUi(player, entity);
                bCades.barricaders.Add(player, entity);
            }

            private void OnTriggerExit(Collider col) {
                var player = col.GetComponent<BasePlayer>();
                if (player == null) return;
                if (!player.IsValid()) return;

                if (bCades.barricaders.ContainsKey(player))
                    bCades.barricaders.Remove(player);
                bCades.DestroyBarrUi(player);

            }
        }

        void CreateBarrUi(BasePlayer player, BaseEntity entity) {
            if (Settings.CanBePicked == false) return;
            if (BarricadeGUIinfo.ContainsKey(player.UserIDString)) {
                CuiHelper.DestroyUi(player, BarricadeGUIinfo[player.UserIDString].panel);
                BarricadeGUIinfo.Remove(player.UserIDString);
            }

            var elements = new CuiElementContainer();
            var rpanel = elements.Add(new CuiPanel {
                Image = { Color = "0.1 0.1 0.1 0", FadeIn = 1.0f },
                RectTransform = { AnchorMin = "0.4 0.4", AnchorMax = "0.59 0.5" },
            }, "Hud");
            elements.Add(new CuiLabel {
                Text = { Text = "<size=35>⇧</size>\n" + lang.GetMessage("pickup", this), Color = "0.8 0.8 0.8 1", FontSize = 14, Align = TextAnchor.MiddleCenter, FadeIn = 1f },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, rpanel);
            CuiHelper.AddUi(player, elements);
            BarricadeGUIinfo.Add(player.UserIDString, new BarricadeGui() { panel = rpanel, entity = entity });
        }

        void DestroyBarrUi(BasePlayer player) {
            if (BarricadeGUIinfo.ContainsKey(player.UserIDString))
            {
                CuiHelper.DestroyUi(player, BarricadeGUIinfo[player.UserIDString].panel);
                BarricadeGUIinfo.Remove(player.UserIDString);
            }
        }

        void createBarricade(BasePlayer player) {
            Item newItem = ItemManager.Create(ItemManager.FindItemDefinition("sign.post.town"), 1, 0uL);
            newItem.name = lang.GetMessage("name", this);
            newItem.skin = 1510599461;

            if (!newItem.MoveToContainer(player.inventory.containerBelt))
                newItem.Drop(player.inventory.containerBelt.dropPosition, player.inventory.containerBelt.dropVelocity);
        }

        #region Commands
        [ConsoleCommand("barricade.add")] //Used to add barricade to belt if you have the barricades.user permission
        void cmdBarricadeAdd(ConsoleSystem.Arg arg) {
            BasePlayer player = arg.Player();
            if (permission.UserHasPermission(player.UserIDString, "barricades.use")) {
                createBarricade(player);
                SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("added", this)));
            }
            else
                SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("noperms", this)));
            return;
        }

        [ConsoleCommand("barricade.buy")] //Used as the command to buy a barricade in conjunction with a plugin like GUI Shop. Is run through the server and can not be used in game.
        void cmdBarricadeBuy(ConsoleSystem.Arg arg) {
            if (arg.Player()) return;
            if (!arg.HasArgs(1)) { Puts(lang.GetMessage("args", this)); return; }
                var targetPlayer = arg.GetPlayerOrSleeper(1);
                if (!targetPlayer) return;
                createBarricade(targetPlayer);
                Puts(lang.GetMessage("buy", this, targetPlayer.displayName));
            return;
        }
        #endregion

        #region LangAPI
        void LoadDefaultMessages() {
            lang.RegisterMessages(new Dictionary<string, string>() {
                {"title", "<color=orange>Barricades</color> : "},
                {"name", "Wooden Barricade"},
                {"noperms", "You do not have permission to add a barricade to your inventory!"},
                {"added", "1x Barricade added to inventory!"},
                {"pickup", "PICK UP"},
                {"args", "Missing arguments!"},
                {"buy", "{0} recieved a barricade!"}
            }, this);
        }
        #endregion
    }
} 