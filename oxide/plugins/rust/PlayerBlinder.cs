using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Globalization;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Player Blinder", "redBDGR", "1.0.5")]
    [Description("Gives an extra way of punishing players")]
    class PlayerBlinder : CovalencePlugin
    {
        public bool Changed = false;

        public const string permissionName = "playerblinder.admin";

        public bool useImage = false;
        public string ImageURL = "";
        public string ImageAMIN = "0 0";
        public string ImageAMAX = "1 1";

        public bool useText = true;
        public string TextTEXT = "You have been blinded by an admin!";
        public int TextSize = 20;

        public string BlindColour = "0 0 0 1";

        //public bool noTalk = true;

        Dictionary<string, bool> GUIinfo = new Dictionary<string, bool>();
        Dictionary<string, int> adminProtection = new Dictionary<string, int>();

        void Init()
        {
            permission.RegisterPermission(permissionName, this);
            LoadVariables();
        }

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["player already blinded"] = "This player is already blinded! use unblind <playername / id>",
                ["player blinded"] = "{0} was blinded!",
                ["player unblinded"] = "{0} was unblinded!",
                ["no permissions"] = "You are not allowed to use this command!",
                ["player not found"] = "The player was not found / is offline",
                ["unblind invalid syntax"] = "Invalid syntax! unblind <playername / id>",
                ["blind invalid syntax"] = "Invalid syntax! blind <playername / id> <length> <\"message\">",
                ["target offline"] = "The target you selected is currently offline!",
                ["More than one result"] = "There was more than one result! please give a clearer search term",
                ["Blindprotect Invalid Syntax"] = "Invalid syntax! /blindprotect <length of punishment>",
                ["protection disabled"] = "Your blind protection was disabled!",
                ["protection enabled"] = "Your blind protection was enabled!"
            }, this);
        }

        void LoadVariables()
        {
            ImageURL = Convert.ToString(GetConfig("Image UI", "URL", ""));
            useImage = Convert.ToBoolean(GetConfig("Settings", "ImageOverlay Enabled", false));
            useText = Convert.ToBoolean(GetConfig("Settings", "Use Text", true));
            TextTEXT = Convert.ToString(GetConfig("Text UI", "Default Text", "You have been blinded by an admin!"));
            BlindColour = Convert.ToString(GetConfig("Blind UI", "Colour", "0 0 0 1"));
            ImageAMIN = Convert.ToString(GetConfig("Image UI", "AnchorMIN", "0 0"));
            ImageAMAX = Convert.ToString(GetConfig("Image UI", "AnchorMAX", "1 1"));
            TextSize = Convert.ToInt32(GetConfig("Text UI", "Size", 20));
            //noTalk = Convert.ToBoolean(GetConfig("Settings", "No Talk While Blinded", true));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                if (GUIinfo.ContainsKey(player.UserIDString))
                    if (GUIinfo[player.UserIDString])
                        GUIDestroy(player);
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (!(entity is BasePlayer)) return;
            if (!adminProtection.ContainsKey(entity.ToPlayer().UserIDString)) return;
            if (!(info.Initiator is BasePlayer)) return;
            BasePlayer targetplayer = info.InitiatorPlayer;
            if (targetplayer.IsConnected)
            {
                if (GUIinfo.ContainsKey(targetplayer.UserIDString)) return;
                DoGUI(targetplayer, adminProtection[entity.ToPlayer().UserIDString], false, null);
            }
            return;
        }

        [Command("blind")]
        void BlindCMD(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permissionName))
            {
                player.Reply(msg("no permissions", player.Id));
                return;
            }

            if (args.Length == 1)
            {
                List<BasePlayer> PlayerList = FindPlayer(args[0]);
                if (PlayerList.Count > 1)
                {
                    player.Reply(msg("More than one result", player.Id));
                    return;
                }
                BasePlayer targetplayer = PlayerList[0];
                if (targetplayer == null)
                {
                    player.Reply(msg("player not found", player.Id));
                    return;
                }

                if (!targetplayer.IsConnected)
                {
                    player.Reply(msg("target offline", player.Id));
                    return;
                }

                if (GUIinfo.ContainsKey(targetplayer.UserIDString))
                {
                    if (GUIinfo[targetplayer.UserIDString])
                    {
                        player.Reply(msg("player already blinded", player.Id));
                        return;
                    }
                    DoGUI(targetplayer, 0.0f, true, null);
                    return;
                }
                else
                {
                    DoGUI(targetplayer, 0.0f, false, null);
                    player.Message(string.Format(msg("player blinded", player.Id), targetplayer.displayName));
                    return;
                }
            }
            else if (args.Length == 2)
            {
                List<BasePlayer> PlayerList = FindPlayer(args[0]);
                if (PlayerList.Count > 1)
                {
                    player.Reply(msg("More than one result", player.Id));
                    return;
                }
                BasePlayer targetplayer = PlayerList[0];
                if (targetplayer != null)
                {
                    player.Reply(msg("player not found", player.Id));
                    return;
                }

                if (!targetplayer.IsConnected)
                {
                    player.Reply(msg("target offline", player.Id));
                    return;
                }

                int lengthINT = Convert.ToInt32(args[1]);
                if (GUIinfo.ContainsKey(targetplayer.UserIDString))
                {
                    if (GUIinfo[targetplayer.UserIDString])
                    {
                        player.Reply(msg("player already blinded", player.Id));
                        return;
                    }
                    DoGUI(targetplayer, Convert.ToSingle(lengthINT), true, null);
                    return;
                }
                else
                {
                    DoGUI(targetplayer, Convert.ToSingle(lengthINT), false, null);
                    player.Reply(string.Format(msg("player blinded", player.Id), targetplayer.displayName));
                    return;
                }
            }
            else if (args.Length == 3)
            {
                List<BasePlayer> PlayerList = FindPlayer(args[0]);
                if (PlayerList.Count > 1)
                {
                    player.Reply(msg("More than one result", player.Id));
                    return;
                }
                BasePlayer targetplayer = PlayerList[0];
                if (targetplayer == null)
                {
                    player.Reply(msg("player not found", player.Id));
                    return;
                }

                if (!targetplayer.IsConnected)
                {
                    player.Reply(msg("target offline", player.Id));
                    return;
                }

                int lengthINT = Convert.ToInt32(args[1]);
                string message = Convert.ToString(args[2]);

                if (GUIinfo.ContainsKey(targetplayer.UserIDString))
                {
                    if (GUIinfo[targetplayer.UserIDString])
                    {
                        player.Reply(msg("player already blinded", player.Id));
                        return;
                    }
                    DoGUI(targetplayer, Convert.ToSingle(lengthINT), true, message);
                    return;
                }
                else
                {
                    DoGUI(targetplayer, Convert.ToSingle(lengthINT), false, message);
                    player.Reply(string.Format(msg("player blinded", player.Id), targetplayer.displayName));
                    return;
                }
            }
            else
                player.Reply(msg("blind invalid syntax", player.Id));
        }

        [Command("unblind")]
        void UnblindCMD(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permissionName))
            {
                player.Reply(msg("no permissions", player.Id));
                return;
            }

            if (args.Length == 1)
            {
                List<BasePlayer> PlayerList = FindPlayer(args[0]);
                if (PlayerList.Count > 1)
                {
                    player.Reply(msg("More than one result", player.Id));
                    return;
                }
                BasePlayer targetplayer = PlayerList[0];
                if (targetplayer == null)
                {
                    player.Reply(msg("player not found", player.Id));
                    return;
                }

                if (!targetplayer.IsConnected)
                {
                    player.Reply(msg("player not found", player.Id));
                    return;
                }

                if (GUIinfo.ContainsKey(targetplayer.UserIDString))
                {
                    if (!GUIinfo[targetplayer.UserIDString])
                    {
                        player.Reply(msg("player already blinded", player.Id));
                        return;
                    }
                    else
                    {
                        GUIDestroy(targetplayer);
                        player.Reply(string.Format(msg("player unblinded", player.Id), targetplayer.displayName));
                        return;
                    }
                }
                return;
            }
            else
                player.Reply(msg("unblind invalid syntax", player.Id));
        }

        [ChatCommand("blindprotect")]
        void BlindProtect(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName))
            {
                player.ChatMessage(msg("no permissions", player.UserIDString));
                return;
            }

            if (args.Length == 1)
            {
                if (adminProtection.ContainsKey(player.UserIDString))
                {
                    adminProtection.Remove(player.UserIDString);
                    player.ChatMessage(msg("protection disabled", player.UserIDString));
                    return;
                }
                else
                {
                    adminProtection.Add(player.UserIDString, Convert.ToInt32(args[0]));
                    player.ChatMessage(msg("protection enabled", player.UserIDString));
                    return;
                }
            }
            else
            {
                player.ChatMessage(msg("Blindprotect Invalid Syntax", player.UserIDString));
                return;
            }
        }

        void GUIDestroy(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Panel);
            GUIinfo[player.UserIDString] = false;
        }

        void DoGUI(BasePlayer targetplayer, float length, bool indic, string message)
        {
            if (indic == true)
            {
                var element = UI.CreateElementContainer(Panel, BlindColour, "0 0", "1 1", false);
                if (useImage)
                    UI.CreateImage(ref element, Panel, ImageURL, ImageAMIN, ImageAMAX);

                if (useText)
                {
                    if (message == null)
                        UI.CreateTextOutline(ref element, Panel, TextTEXT, "1 1 1 1", "0 0 0 0", "1", "1", TextSize, "0 0", "1 1", TextAnchor.MiddleCenter);
                    else
                        UI.CreateTextOutline(ref element, Panel, message, "1 1 1 1", "0 0 0 0", "1", "1", TextSize, "0 0", "1 1", TextAnchor.MiddleCenter);
                }

                CuiHelper.AddUi(targetplayer, element);
                GUIinfo[targetplayer.UserIDString] = true;

                if (length > 0.0f)
                    timer.Once(length, () => { GUIDestroy(targetplayer); });
            }
            else if (indic == false)
            {
                GUIinfo.Add(targetplayer.UserIDString, false);

                var element = UI.CreateElementContainer(Panel, BlindColour, "0 0", "1 1", false);
                if (useImage)
                    UI.CreateImage(ref element, Panel, ImageURL, ImageAMIN, ImageAMAX);

                if (useText)
                    UI.CreateTextOutline(ref element, Panel, TextTEXT, "1 1 1 1", "0 0 0 0", "1", "1", TextSize, "0 0", "1 1", TextAnchor.MiddleCenter);

                CuiHelper.AddUi(targetplayer, element);
                GUIinfo[targetplayer.UserIDString] = true;

                if (length > 0.0f)
                    timer.Once(length, () => { GUIDestroy(targetplayer); });
            }
        }

        private static List<BasePlayer> FindPlayer(string nameOrId)
        {
            List<BasePlayer> x = new List<BasePlayer>();

            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString == nameOrId)
                    x.Add(activePlayer);
                if (activePlayer.displayName.Contains(nameOrId, CompareOptions.OrdinalIgnoreCase))
                    if (!x.Contains(activePlayer))
                        x.Add(activePlayer);
                if (activePlayer.net?.connection != null && activePlayer.net.connection.ipaddress == nameOrId)
                    if (!x.Contains(activePlayer))
                        x.Add(activePlayer);
            }
            return x;
        }

        #region UI

        private string Panel = "BlindPanel";

        public class UI
        {
            static public CuiElementContainer CreateElementContainer(string panel, string color, string aMin, string aMax, bool cursor = false)
            {
                var NewElement = new CuiElementContainer()
            {
                {
                    new CuiPanel
                    {
                        Image = {Color = color},
                        RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                        CursorEnabled = cursor,
                    },
                    new CuiElement().Parent,
                    panel
                }
            };
                return NewElement;
            }

            static public void CreateTextOutline(ref CuiElementContainer element, string panel, string text, string colorText, string colorOutline, string DistanceA, string DistanceB, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                element.Add(new CuiElement
                {
                    Parent = panel,
                    Components =
                    {
                        new CuiTextComponent{Color = colorText, FontSize = size, Align = align, Text = text },
                        new CuiOutlineComponent {Distance = DistanceA + " " + DistanceB, Color = colorOutline},
                        new CuiRectTransformComponent {AnchorMax = aMax, AnchorMin = aMin }
                    }
                });
            }

            static public void CreateImage(ref CuiElementContainer element, string panel, string imageURL, string aMin, string aMax)
            {
                element.Add(new CuiElement
                {
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent { Url = imageURL, Color = "1 1 1 1" },
                        new CuiRectTransformComponent {AnchorMax = aMax, AnchorMin = aMin }
                    }
                });
            }
        }

        #endregion

        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        string msg(string key, string id = null) => lang.GetMessage(key, this, id);
    }
}
