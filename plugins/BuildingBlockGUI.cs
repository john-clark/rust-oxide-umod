using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("BuildingBlockGUI", "wazzzup", "1.1.0")]
    [Description("Displays GUI to player when he enters or leaves building block without need of Planner")]
    public class BuildingBlockGUI : RustPlugin
    {

        #region Config
        List<ulong> activeUI = new List<ulong>();
        private bool configChanged = false;
        private float configTimerSeconds;
        private bool configUseTimer;
        private bool configUseImage;
        private string configImageURL;
        private bool configUseGameTips;
        private string configAnchorMin;
        private string configAnchorMax;
        private string configUIColor;
        private string configUITextColor;

        Timer _timer;

        protected override void LoadDefaultConfig()
        {
            PrintWarning("No configuration file found, generating...");
            Config.Clear();
            LoadVariables();
        }

        void LoadVariables()
        {
            configUseTimer = Convert.ToBoolean(GetConfig("useTimer", true));
            configUseImage = Convert.ToBoolean(GetConfig("useImage", false));
            configImageURL = Convert.ToString(GetConfig("ImageURL", "http://oxidemod.org/data/resource_icons/2/2713.jpg?1512759786"));
            configUseGameTips = Convert.ToBoolean(GetConfig("UseGameTips", false));
            configTimerSeconds = Convert.ToSingle(GetConfig("timerSeconds", 0.5f));
            configAnchorMin = Convert.ToString(GetConfig("AnchorMin", "0.35 0.11"));
            configAnchorMax = Convert.ToString(GetConfig("AnchorMax", "0.63 0.14"));
            configUIColor = Convert.ToString(GetConfig("UIColor", "1 0 0 0.15"));
            configUITextColor = Convert.ToString(GetConfig("UITextColor", "1 1 1"));
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

        #endregion

        #region Messages
        void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"text", "BUILDING BLOCKED" }

            }, this);
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"text", "СТРОИТЕЛЬСТВО ЗАПРЕЩЕНО" }
            }, this, "ru");
        }
        private string msg(string key, BasePlayer player = null) => lang.GetMessage(key, this, player?.UserIDString);
        #endregion

        #region Oxide hooks

        void Init()
        {
            LoadVariables();
        }

        void OnServerInitialized()
        {
            if (configUseTimer)
            {
                _timer = timer.Repeat(configTimerSeconds, 0, () => PluginTimerTick());
            }
        }

        void Unload()
        {
            if (_timer != null) _timer.Destroy();
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUI(player);
            }
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            DestroyUI(player);
        }

        #endregion

        #region UI

        void DestroyUI(BasePlayer player)
        {
            if (!activeUI.Contains(player.userID)) return;
            if (configUseGameTips) player.SendConsoleCommand("gametip.hidegametip");
            else CuiHelper.DestroyUi(player, "BuildingBlockGUI");
            activeUI.Remove(player.userID);
        }

        void CreateUI(BasePlayer player)
        {
            if (activeUI.Contains(player.userID)) return;
            if (configUseGameTips)
            {
                player.SendConsoleCommand("gametip.hidegametip");
                player.SendConsoleCommand("gametip.showgametip", msg("text", player));
                activeUI.Add(player.userID);
                return;
            }
            DestroyUI(player);
            CuiElementContainer container = new CuiElementContainer();
            if (configUseImage)
            {
                var panel = container.Add(new CuiPanel()
                {
                    Image = { Color = configUIColor },
                    RectTransform = { AnchorMin = configAnchorMin, AnchorMax = configAnchorMax }
                }, "Hud", "BuildingBlockGUI");
                container.Add(new CuiElement()
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent {
                            Url = configImageURL,
                            Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                        new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1" }
                    }
                });
            }
            else
            {
                var panel = container.Add(new CuiPanel()
                {
                    Image = { Color = configUIColor },
                    RectTransform = { AnchorMin = configAnchorMin, AnchorMax = configAnchorMax }
                }, "Hud", "BuildingBlockGUI");
                CuiElement element = new CuiElement
                {
                    Parent = panel,
                    Components = {
                        new CuiTextComponent { Text = msg("text",player), FontSize = 15, Color = configUITextColor, Align = TextAnchor.MiddleCenter },
                        new CuiRectTransformComponent { AnchorMin = "0.0 0.0", AnchorMax = "1.0 1.0" }
                    }
                };
                container.Add(element);
            }

            CuiHelper.AddUi(player, container);
            activeUI.Add(player.userID);
        }
        #endregion

        #region Helpers
        void PluginTimerTick()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.IsBuildingBlocked())
                {
                    CreateUI(player);
                } else
                {
                    DestroyUI(player);
                }
            }
        }

        #endregion
    }
}