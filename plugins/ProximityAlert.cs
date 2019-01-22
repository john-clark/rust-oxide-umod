using System.Collections.Generic;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ProximityAlert", "k1lly0u", "0.2.3", ResourceId = 1801)]
    class ProximityAlert : RustPlugin
    {
        #region Fields
        [PluginReference] Plugin Clans;
        [PluginReference] Plugin Friends;
        [PluginReference] Plugin EventManager;
        [PluginReference] Plugin HumanNPC;

        static ProximityAlert ins;
        const string proxUI = "ProximityAlertUI";
        #endregion

        #region Functions
        void OnServerInitialized()
        {
            ins = this;
            lang.RegisterMessages(messages, this);            
            LoadVariables();
            RegisterPermissions();
            CheckDependencies();

            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerInit(player);
        }
        void Unload()
        {
            var objects = UnityEngine.Object.FindObjectsOfType<ProximityPlayer>();
            if (objects != null)
                foreach (var gameObj in objects)
                    UnityEngine.Object.Destroy(gameObj);

            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, proxUI);
        }
        void OnPlayerInit(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, "proximityalert.use")) return;
            if (player.IsSleeping() || player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.In(2, () => OnPlayerInit(player));
                return;
            }
            var proxPlayer = player.GetComponent<ProximityPlayer>();
            if (proxPlayer != null)                            
                UnityEngine.Object.DestroyImmediate(proxPlayer);

            NextTick(()=> player.gameObject.AddComponent<ProximityPlayer>());
        }
        void OnPlayerDisconnected(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, proxUI);
            var proxPlayer = player.GetComponent<ProximityPlayer>();
            if (proxPlayer != null)                            
                UnityEngine.Object.Destroy(proxPlayer);            
        }
        
        private void RegisterPermissions()
        {
            permission.RegisterPermission("proximityalert.use", this);
            if (configData.UseCustomPermissions)
            {
                foreach(var perm in configData.CustomPermissions)
                {
                    permission.RegisterPermission(perm.Key, this);
                }
            }
        }
        private void CheckDependencies()
        {
            if (!Friends) PrintWarning($"FriendsAPI could not be found! Unable to use friends feature");
            if (!Clans) PrintWarning($"Clans could not be found! Unable to use clans feature");
        }
        private void ProxCollisionEnter(BasePlayer player)
        {
            var UI = CreateUI(lang.GetMessage("warning", this, player.UserIDString));
            CuiHelper.DestroyUi(player, proxUI);
            CuiHelper.AddUi(player, UI);
        }
        private void ProxCollisionLeave(BasePlayer player)
        {
            var UI = CreateUI(lang.GetMessage("clear", this, player.UserIDString));
            CuiHelper.DestroyUi(player, proxUI);
            CuiHelper.AddUi(player, UI);            
        }
        private float GetPlayerRadius(BasePlayer player)
        {
            foreach(var perm in configData.CustomPermissions)
            {
                if (permission.UserHasPermission(player.UserIDString, perm.Key))
                    return perm.Value;
            }
            if (permission.UserHasPermission(player.UserIDString, "proximityalert.use"))
                return configData.TriggerRadius;
            return 0;
        }
        private bool IsClanmate(ulong playerId, ulong friendId)
        {
            if (!Clans) return false;
            object playerTag = Clans?.Call("GetClanOf", playerId);
            object friendTag = Clans?.Call("GetClanOf", friendId);
            if (playerTag is string && friendTag is string)
                if (playerTag == friendTag) return true;
            return false;
        }
        private bool IsFriend(ulong playerID, ulong friendID)
        {
            if (!Friends) return false;
            bool isFriend = (bool)Friends?.Call("IsFriend", playerID, friendID);
            return isFriend;
        }
        private bool IsPlaying(BasePlayer player)
        {
            if (EventManager)
            {
                object isPlaying = EventManager?.Call("isPlaying", new object[] { player });
                if (isPlaying is bool)
                {
                    if ((bool)isPlaying) return true;
                }
            }
            return false;
        }        
        private void JoinedEvent(BasePlayer player)
        {
            var proxPlayer = player.GetComponent<ProximityPlayer>();
            if (proxPlayer != null)
            {
                CuiHelper.DestroyUi(player, proxUI);
                proxPlayer.isEnabled = false;
            }
        }
        private void LeftEvent(BasePlayer player)
        {
            var proxPlayer = player.GetComponent<ProximityPlayer>();
            if (proxPlayer != null)
            {
                CuiHelper.DestroyUi(player, proxUI);
                proxPlayer.isEnabled = true;
            }
        }
        #endregion

        #region UI
        public CuiElementContainer CreateUI(string text)
        {
            var container = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = "0 0 0 0"},
                            RectTransform = {AnchorMin = $"{ins.configData.GUI_X_Pos} {ins.configData.GUI_Y_Pos}", AnchorMax = $"{ins.configData.GUI_X_Pos + ins.configData.GUI_X_Dim} {ins.configData.GUI_Y_Pos + ins.configData.GUI_Y_Dim}"},
                            CursorEnabled = false
                        },
                        new CuiElement().Parent = "Hud",
                        proxUI
                    }
                };
            container.Add(new CuiLabel
            {
                Text = { FontSize = ins.configData.FontSize, Align = TextAnchor.MiddleCenter, Text = text },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }

            },
            proxUI,
            CuiHelper.GetGuid());
            return container;
        }        
        #endregion

        #region Chat Command
        [ChatCommand("prox")]
        private void cmdProx(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "proximityalert.use")) return;
            var proxPlayer = player.GetComponent<ProximityPlayer>();
            if (proxPlayer != null)
            {
                CuiHelper.DestroyUi(player, proxUI);
                if (proxPlayer.isEnabled)
                {                   
                    proxPlayer.isEnabled = false;
                    SendReply(player, lang.GetMessage("deactive", this, player.UserIDString));
                }
                else
                {
                    proxPlayer.isEnabled = true;
                    SendReply(player, lang.GetMessage("active", this, player.UserIDString));
                }
            }           
        }
        #endregion

        #region Player Class
        class ProximityPlayer : MonoBehaviour
        {
            private BasePlayer player;
            private Timer destroyTimer;
            private List<ulong> inProximity = new List<ulong>();
            public bool isEnabled;          

            private void Awake()
            {
                player = GetComponent<BasePlayer>();

                var child = gameObject.CreateChild();
                var collider = child.AddComponent<SphereCollider>();
                collider.gameObject.layer = (int)Layer.Reserved1;
                collider.radius = ins.GetPlayerRadius(player);
                collider.isTrigger = true;

                isEnabled = true;
            }
            void OnTriggerEnter(Collider col)
            {
                var enemy = col.GetComponentInParent<BasePlayer>();
                if (enemy != null && enemy != player)
                {
                    if (ins.IsFriend(player.userID, enemy.userID)) return;
                    if (ins.IsClanmate(player.userID, enemy.userID)) return;
                    if (ins.IsPlaying(enemy)) return;
                    if (enemy.IsSleeping() && !ins.configData.DetectSleepers) return;
                    if (!ins.configData.DetectHumanNPCs && ins.HumanNPC)
                    {
                        if (player.userID <= 2147483647)
                            return;
                    }

                    if (inProximity.Count == 0 && isEnabled)
                    {
                        if (destroyTimer != null)
                            destroyTimer.Destroy();
                        ins.ProxCollisionEnter(player);
                    }
                    inProximity.Add(enemy.userID);
                }
            }
            void OnTriggerExit(Collider col)
            {
                var enemy = col.GetComponentInParent<BasePlayer>();
                if (enemy != null && enemy != player && inProximity.Contains(enemy.userID))
                {
                    inProximity.Remove(enemy.userID);
                    if (inProximity.Count == 0 && isEnabled)
                    {
                        ins.ProxCollisionLeave(player);
                        destroyTimer = ins.timer.In(5, () => CuiHelper.DestroyUi(player, proxUI));
                    }             
                }
            }
        }
        #endregion
        
        #region Config
        private ConfigData configData;
        class ConfigData
        {
            public bool DetectSleepers { get; set; }
            public bool DetectHumanNPCs { get; set; }
            public float GUI_X_Pos { get; set; }
            public float GUI_X_Dim { get; set; }
            public float GUI_Y_Pos { get; set; }
            public float GUI_Y_Dim { get; set; }
            public int FontSize { get; set; }
            public float TriggerRadius { get; set; }    
            public Dictionary<string, float> CustomPermissions { get; set; } 
            public bool UseCustomPermissions { get; set; }     
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
                CustomPermissions = new Dictionary<string, float>
                {
                    { "proximityalert.vip1", 50 },
                    { "proximityalert.vip2", 75 },
                },
                DetectHumanNPCs = false,
                DetectSleepers = false,
                FontSize = 18,
                GUI_X_Pos = 0.2f,
                GUI_X_Dim = 0.6f,
                GUI_Y_Pos = 0.1f,
                GUI_Y_Dim = 0.16f,
                TriggerRadius = 25f,
                UseCustomPermissions = true
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Localization
        Dictionary<string, string> messages = new Dictionary<string, string>
        {
            {"warning", "<color=#cc0000>Caution!</color> There are players nearby!" },
            {"clear", "<color=#ffdb19>Clear!</color>" },
            {"active", "You have activated ProximityAlert" },
            {"deactive", "You have deactivated ProximityAlert" }
        };
        #endregion
    }
}