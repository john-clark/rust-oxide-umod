using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Admin Panel", "BuzZ", "1.3.0")]
    [Description("GUI admin panel with command buttons")]

/*======================================================================================================================= 
*
*   
*   20th november 2018
*
*	THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*   1.3.0   20181120    New maintainer (BuzZ)   added GUI button for set new tp pos (config for color, and bool for on/off)
*
*
*********************************************
*   Original author :   DaBludger on versions <1.3.0
*   Maintainer(s)   :   BuzZ since 20181116 from v1.3.0
*********************************************   
*
*=======================================================================================================================*/


    public class AdminPanel : RustPlugin
    {
        [PluginReference]
        private Plugin AdminRadar, EnhancedBanSystem, Godmode, NTeleportation, Vanish;

        private const string permAdminPanel = "adminpanel.allowed";
        private const string permAdminRadar = "adminradar.allowed";
        private const string permGodmode = "godmode.toggle";
        private const string permVanish = "vanish.use";

        #region Integrations

        #region Godmode

        private bool IsGod(string UserID)
        {
            return Godmode != null && Godmode.Call<bool>("IsGod", UserID);
        }

        private void ToggleGodmode(BasePlayer player)
        {
            if (Godmode == null) return;

            if (IsGod(player.UserIDString))
                Godmode.Call("DisableGodmode", player.IPlayer);
            else
                Godmode.Call("EnableGodmode", player.IPlayer);
            AdminGui(player);
        }

        #endregion Godmode

        #region Vanish

        private bool IsInvisible(BasePlayer player)
        {
            return Vanish != null && Vanish.Call<bool>("IsInvisible", player);
        }

        private void ToggleVanish(BasePlayer player)
        {
            if (Vanish == null) return;

            if (!IsInvisible(player))
                Vanish.Call("Disappear", player);
            else
                Vanish.Call("Reappear", player);
            AdminGui(player);
        }

        #endregion Vanish

        #region Admin Radar

        private bool IsRadar(string id)
        {
            return AdminRadar != null && AdminRadar.Call<bool>("IsRadar", id);
        }

        private void ToggleRadar(BasePlayer player)
        {
            if (AdminRadar == null) return;

            AdminRadar.Call("cmdESP", player, "radar", new string[0]);
            AdminGui(player);
        }

        #endregion Admin Radar

        #endregion Integrations

        private void Init()
        {
            LoadDefaultConfig();
            permission.RegisterPermission(permAdminPanel, this);
        }

        #region Configuration

        private bool ToggleMode;
        private bool newtp;
        private string PanelPosMax;
        private string PanelPosMin;
        private string adminZoneCords;
        private string btnActColor;
        private string btnInactColor;
        private string btnNewtpColor;

        protected override void LoadDefaultConfig()
        {
            Config["AdminPanelToggleMode"] = ToggleMode = GetConfig("AdminPanelToggleMode", false);
            Config["AdminPanelPosMax"] = PanelPosMax = GetConfig("AdminPanelPosMax", "0.991 0.67");
            Config["AdminPanelPosMin"] = PanelPosMin = GetConfig("AdminPanelPosMin", "0.9 0.5");
            Config["AdminZoneCoordinates"] = adminZoneCords = GetConfig("AdminZoneCoordinates", "0;0;0;");
            Config["PanelButtonActiveColor"] = btnActColor = GetConfig("PanelButtonActiveColor", "0 2.55 0 0.3");
            Config["PanelButtonInactiveColor"] = btnInactColor = GetConfig("PanelButtonInactiveColor", "2.55 0 0 0.3");
            Config["PanelButtonNewtp"] = newtp = GetConfig("PanelButtonNewtp", false);
            Config["PanelButtonNewtpColor"] = btnNewtpColor = GetConfig("PanelButtonNewtpColor", "1.0 0.65 0.85 0.3");

            SaveConfig();
        }

        #endregion Configuration

        #region Localization

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AdminTP"] = "Teleport",
                ["Godmode"] = "God",
                ["Radar"] = "Radar",
                ["Vanish"] = "Vanish",
                ["NewTP"] = "NewTP"


            }, this);

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AdminTP"] = "Teleport",
                ["Godmode"] = "Dios",
                ["Radar"] = "Radar",
                ["Vanish"] = "Desaparecer",
                ["NewTP"] = "NewTP"

            }, this, "es");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AdminTP"] = "Teleport",
                ["Godmode"] = "Dieu",
                ["Radar"] = "Radar",
                ["Vanish"] = "Invisible",
                ["NewTP"] = "NewTP"

            }, this, "fr");

        }

        #endregion Localization

        #region Hooks

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!ToggleMode)
            {
                if (IsAllowed(player.UserIDString, permAdminPanel)) AdminGui(player);
            }
        }

        private void OnPlayerDie(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Name);
        }

        private void OnPluginLoaded(Plugin plugin)
        {
            if (plugin.Name == "AdminRadar" || plugin.Name == "Godmode" || plugin.Name == "Vanish")
            {
                OnServerInitialized();
            }
        }

        private void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin.Name == "AdminRadar" || plugin.Name == "Godmode" || plugin.Name == "Vanish")
            {
                OnServerInitialized();
            }
        }

        private void OnServerInitialized()
        {
            if (!ToggleMode)
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (IsAllowed(player.UserIDString, permAdminPanel)) AdminGui(player);
                }
            }
        }

        #endregion Hooks

        #region Command Structure

        [ConsoleCommand("adminpanel")]
        private void ccmdAdminPanel(ConsoleSystem.Arg arg) // TODO: Make universal command
        {
            var player = arg.Player();
            if (player == null) return;

            var args = arg.Args;
            if (IsAllowed(player.UserIDString, permAdminPanel))
            {
                switch (args[0].ToLower()) // TODO: Fix possible NRE
                {
                    case "action":
                        if (args[1] == "vanish") // TODO: ToLower() args[1] here and below, use switch?
                        {
                            if (Vanish) ToggleVanish(player);
                        }
                        else if (args[1] == "admintp")
                        {
                            var pos = adminZoneCords.Split(';');
                            var loc = new Vector3(float.Parse(pos[0]), float.Parse(pos[1]), float.Parse(pos[2]));
                            covalence.Players.FindPlayer(player.UserIDString).Teleport(loc.x, loc.y, loc.z);
                        }
                        else if (args[1] == "radar")
                        {
                            if (AdminRadar) ToggleRadar(player);
                        }
                        else if (args[1] == "god")
                        {
                            if (Godmode) ToggleGodmode(player);
                        }
                        else if (args[1] == "newtp")
                        {
                            if (newtp)
                            {
                                string[] argu = new string[1];
                                argu[0] = "settp";
                                ccmdAdminPanel(player, null, argu);
                            }
                        }

                        break;

                    case "toggle":
                        if (IsAllowed(player.UserIDString, permAdminPanel))
                        {
                            if (args[1] == "True" && ToggleMode) // TODO: Convert to bool to check
                            {
                                AdminGui(player);
                            }
                            else if (args[1] == "False" && ToggleMode) // TODO: Convert to bool to check
                            {
                                CuiHelper.DestroyUi(player, Name);
                            }
                            // TODO: Show reply
                        }
                        break;

                    default:
                        SendReply(player, "Invalid syntax"); // TODO: Localization
                        return;
                }
            }

            //Reply(player, null); // TODO: Show actual reply or not at all
        }

        [ChatCommand("adminpanel")]
        private void ccmdAdminPanel(BasePlayer player, string command, string[] args) // TODO: Make universal command
        {
            if (!IsAllowed(player.UserIDString, permAdminPanel))
            {
                SendReply(player, $"Unknown command: {command}"); // TODO: Localization
                return;
            }

            if (args.Length == 0)
            {
                SendReply(player, $"Usage: /{command} show/hide/settp"); // TODO: Localization
                return;
            }

            switch (args[0].ToLower())
            {
                case "hide":
                    CuiHelper.DestroyUi(player, Name);
                    SendReply(player, "Admin panel hidden"); // TODO: Localization
                    break;

                case "show":
                    AdminGui(player);
                    SendReply(player, "Admin panel refreshed/shown"); // TODO: Localization
                    break;

                case "settp":
                    Vector3 coord = player.transform.position + new Vector3(0, 1, 0);
                    Config["AdminZoneCoordinates"] = adminZoneCords = $"{coord.x};{coord.y};{coord.z}";
                    Config.Save();
                    SendReply(player, $"Admin zone coordinates set to current position {player.transform.position + new Vector3(0, 1, 0)}"); // TODO: Localization
                    break;

                default:
                    SendReply(player, $"Invalid syntax: /{command} {args[0]}"); // TODO: Localization
                    break;
            }
        }

        #endregion Command Structure

        #region GUI Panel

        private void AdminGui(BasePlayer player)
        {
            NextTick(() =>
            {
                // Destroy existing UI
                CuiHelper.DestroyUi(player, Name);

                var BTNColorVanish = btnInactColor;
                var BTNColorGod = btnInactColor;
                var BTNColorRadar = btnInactColor;
                var BTNColorNewTP = btnNewtpColor;


                if (AdminRadar) { if (IsRadar(player.UserIDString)) { BTNColorRadar = btnActColor; } }
                if (Godmode) { if (IsGod(player.UserIDString)) { BTNColorGod = btnActColor; } }
                if (Vanish) { if (IsInvisible(player)) { BTNColorVanish = btnActColor; } }

                var GUIElement = new CuiElementContainer();

                var GUIPanel = GUIElement.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "1 1 1 0"
                    },
                    RectTransform =
                    {
                        AnchorMin = PanelPosMin,
                        AnchorMax = PanelPosMax
                    },
                    CursorEnabled = ToggleMode
                }, "Hud", Name);

                if (AdminRadar && permission.UserHasPermission(player.UserIDString, permAdminRadar))
                {
                    GUIElement.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "adminpanel action radar",
                            Color = BTNColorRadar
                        },
                        Text =
                        {
                            Text = Lang("Radar", player.UserIDString),
                            FontSize = 8,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.062 0.21",
                            AnchorMax = "0.51 0.37"
                        }
                    }, GUIPanel);
                }

                GUIElement.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "adminpanel action admintp",
                        Color = "1.28 0 1.28 0.3"
                    },
                    Text =
                    {
                        Text = Lang("AdminTP", player.UserIDString),
                        FontSize = 8,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.52 0.21",
                        AnchorMax = "0.95 0.37"
                    }
                }, GUIPanel);

            if (newtp)
            {
                GUIElement.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "adminpanel action newtp",
                        Color = BTNColorNewTP
                    },
                    Text =
                    {
                        Text = Lang("newTP", player.UserIDString),
                        FontSize = 8,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.52 0.39",
                        AnchorMax = "0.95 0.47"
                    }
                }, GUIPanel);
            }

                if (Godmode && permission.UserHasPermission(player.UserIDString, permGodmode))
                {
                    GUIElement.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "adminpanel action god",
                            Color = BTNColorGod
                        },
                        Text =
                        {
                            Text = Lang("Godmode", player.UserIDString),
                            FontSize = 8,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.52 0.02",
                            AnchorMax = "0.95 0.19"
                        }
                    }, GUIPanel);
                }

                if (Vanish && permission.UserHasPermission(player.UserIDString, permVanish))
                {
                    GUIElement.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "adminpanel action vanish",
                            Color = BTNColorVanish
                        },
                        Text =
                        {
                            Text = Lang("Vanish", player.UserIDString),
                            FontSize = 8,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.062 0.02",
                            AnchorMax = "0.51 0.19"
                        }
                    }, GUIPanel);
                }

                CuiHelper.AddUi(player, GUIElement);
            });
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Name);
            }
        }

        #endregion GUI Panel

        #region Helpers

        private T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        private bool IsAdmin(string id) => permission.UserHasGroup(id, "admin");

        private bool IsAllowed(string id, string perm) => permission.UserHasPermission(id, perm) || IsAdmin(id);

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion Helpers
    }
}
