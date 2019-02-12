using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Building Block GUI", "Iv Misticos", "2.0.0")]
    [Description("Displays GUI to player when he enters or leaves building block without need of Planner")]
    public class BuildingBlockGUI : RustPlugin
    {
        #region Variables

        private GameObject _controller;

        private static BuildingBlockGUI _ins;
        
        #endregion
        
        #region Configuration

        private static Configuration _config;
        
        private class Configuration
        {
            [JsonProperty(PropertyName = "Use Image")]
            public bool UseImage = false;
            
            [JsonProperty(PropertyName = "Image URL")]
            public string ImageURL = "";
            
            [JsonProperty(PropertyName = "Use GameTips")]
            public bool UseGameTips = false;
            
            [JsonProperty(PropertyName = "Check Frequency")]
            public float CheckFrequency = 0.75f;
            
            [JsonProperty(PropertyName = "Background Color")]
            public string BackgroundColor = "1.0 0.0 0.0 0.15";
            
            [JsonProperty(PropertyName = "Anchor Min")]
            public string AnchorMin = "0.35 0.11";
            
            [JsonProperty(PropertyName = "Anchor Max")]
            public string AnchorMax = "0.63 0.14";
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

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"text", "BUILDING BLOCKED" }

            }, this);
            
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"text", "СТРОИТЕЛЬСТВО ЗАПРЕЩЕНО" }
            }, this, "ru");
        }

        private void OnServerInitialized()
        {
            _ins = this;
            _controller = new GameObject();
            _controller.AddComponent<BuildingController>();
        }

        private void Unload()
        {
            UnityEngine.Object.Destroy(_controller);
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            BuildingController.DestroyUI(player);
        }

        #endregion

        #region Helpers

        private class BuildingController : MonoBehaviour
        {
            private static List<BasePlayer> _activeUI = new List<BasePlayer>();
            
            private void OnDestroy()
            {
                for (var i = _activeUI.Count - 1; i >= 0; i--)
                {
                    DestroyUI(_activeUI[i]);
                }
            }

            private void Awake()
            {
                InvokeRepeating(nameof(OnControllerTick), _config.CheckFrequency, _config.CheckFrequency);
            }

            private void OnControllerTick()
            {
                for (var i = 0; i < BasePlayer.activePlayerList.Count; i++)
                {
                    var player = BasePlayer.activePlayerList[i];
                    if (player == null || player.IsNpc)
                        continue;

                    if (player.IsBuildingBlocked())
                    {
                        CreateUI(player);
                    }
                    else
                    {
                        DestroyUI(player);
                    }
                }
            }

            private static void CreateUI(BasePlayer player)
            {
                if (_activeUI.Contains(player))
                    return;
                
                if (_config.UseGameTips)
                {
                    player.SendConsoleCommand("gametip.showgametip", GetMsg("text", player.UserIDString));
                }
                else
                {
                    var container = new CuiElementContainer();
                    var background = container.Add(new CuiPanel
                    {
                        Image = {Color = _config.BackgroundColor},
                        RectTransform = {AnchorMin = _config.AnchorMin, AnchorMax = _config.AnchorMax}
                    }, "Hud", "BuildingBlockGUI.Background");

                    container.Add(_config.UseImage
                        ? new CuiElement
                        {
                            Parent = background,
                            Components =
                            {
                                new CuiRawImageComponent
                                {
                                    Url = _config.ImageURL,
                                    Sprite = "assets/content/textures/generic/fulltransparent.tga"
                                },
                                new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                            }
                        }
                        : new CuiElement
                        {
                            Parent = background,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = GetMsg("text", player.UserIDString), FontSize = 15,
                                    Align = TextAnchor.MiddleCenter
                                },
                                new CuiRectTransformComponent {AnchorMin = "0.0 0.0", AnchorMax = "1.0 1.0"}
                            }
                        });

                    CuiHelper.AddUi(player, container);
                }

                _activeUI.Add(player);
            }

            public static void DestroyUI(BasePlayer player)
            {
                if (!_activeUI.Contains(player))
                    return;

                if (_config.UseGameTips) player.SendConsoleCommand("gametip.hidegametip");
                else CuiHelper.DestroyUi(player, "BuildingBlockGUI.Background");
                _activeUI.Remove(player);
            }
        }
        
        private static string GetMsg(string key, string userId = null) => _ins.lang.GetMessage(key, _ins, userId);

        #endregion
    }
}