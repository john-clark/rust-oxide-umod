using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    // TODO LIST
    // Nothing, yet.

    [Info("Crosshair", "Kappasaurus", "3.0.0")]

    class Crosshair : RustPlugin
    {
        private string mainUI = "UI_MAIN";
        private string panelUI = "UI_PANEL";
        private Dictionary<BasePlayer, bool> crosshairSettings = new Dictionary<BasePlayer, bool>();

        private static string HexToColor(string hexColor)
        {
            if (hexColor.IndexOf('#') != -1) hexColor = hexColor.Replace("#", "");

            var red = 0;
            var green = 0;
            var blue = 0;

            if (hexColor.Length == 6)
            {
                red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
            }
            else if (hexColor.Length == 3)
            {
                red = int.Parse(hexColor[0] + hexColor[0].ToString(), NumberStyles.AllowHexSpecifier);
                green = int.Parse(hexColor[1] + hexColor[1].ToString(), NumberStyles.AllowHexSpecifier);
                blue = int.Parse(hexColor[2] + hexColor[2].ToString(), NumberStyles.AllowHexSpecifier);
            }

            return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255}";
        }

        private class UI
        {
            public static CuiElementContainer CreateElementContainer(string panelName, string color, string aMin, string aMax, bool cursor = false)
            {
                var newElement = new CuiElementContainer
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                            CursorEnabled = cursor
                        },
                        new CuiElement().Parent = "Overlay",
                        panelName
                    }
                };

                return newElement;
            }

            public static void CreateLabel(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter, float fadein = 1f)
            {
                var label = new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, FadeIn = fadein, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                };

                container.Add(label, panel);
            }
        }

        private void ToggleCrosshair(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, "crosshair.use"))
            {
                PrintToChat(player, lang.GetMessage("No Permission", this, player.UserIDString));
                return;
            }

            if (!crosshairSettings.ContainsKey(player))
            {
                crosshairSettings.Add(player, false);
            }

            bool state;
            crosshairSettings.TryGetValue(player, out state);
            PrintToChat(player, lang.GetMessage("Crosshair Toggled", this, player.UserIDString), !state ? "enabled" : "disabled");
            DestroyCrosshair(player);
            if (!state)
            {
                var baseElement = UI.CreateElementContainer(mainUI, $"{HexToColor("000000")} 0", "0 0", "1 1");
                UI.CreateLabel(ref baseElement, mainUI, $"{HexToColor(Configuration.CrosshairColor)} 0.9", Configuration.CrosshairText, 25, "0.25 0.25", "0.75 0.75");
                CuiHelper.AddUi(player, baseElement);
            }

            crosshairSettings[player] = !state;
        }

        private void DestroyCrosshair(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, mainUI);
            CuiHelper.DestroyUi(player, panelUI);
        }

        [ChatCommand("crosshair")]
        private void CrosshairCommand(BasePlayer player, string command, string[] args) => ToggleCrosshair(player);

        [ConsoleCommand("UI_DESTROY")]
        private void DestroyAllCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null) return;

            DestroyCrosshair(player);
        }

        private void Init()
        {
            permission.RegisterPermission("crosshair.use", this);
            LoadConfig();
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyCrosshair(player);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            DestroyCrosshair(player);
            crosshairSettings.Remove(player); 
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Crosshair Toggled", "Crosshair sucessfully {0}."},
                {"No Permission", "Error, you lack permission."}
            }, this);
        }

        private void GetConfig<T>(ref T variable, params string[] path)
        {
            if (path.Length == 0)
            {
                return;
            }

            if (Config.Get(path) == null)
            {
                Config.Set(path.Concat(new object[] { variable }).ToArray());
                PrintWarning($"Added field to config: {string.Join("/", path)}");
            }

            variable = (T)Convert.ChangeType(Config.Get(path), typeof(T));
        }

        private struct Configuration
        {
            public static string CrosshairColor = "#008000";
            public static string CrosshairText = "+";
        }

        private new void LoadConfig()
        {
            GetConfig(ref Configuration.CrosshairColor, "Crosshair settings", "Color");
            GetConfig(ref Configuration.CrosshairText, "Crosshair settings", "Symbol");

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => PrintWarning("Generating new config file...");
    }
}