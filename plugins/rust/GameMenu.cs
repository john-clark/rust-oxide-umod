using System;
using System.Collections.Generic;
using System.Linq;
using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Game Menu", "Iv Misticos", "1.0.5")]
    [Description("Create your own GUI menu with buttons and title.")]
    class GameMenu : RustPlugin
    {
        #region Plugin Variables

        private static CuiButton _backgroundButton;
        
        #endregion
        
        #region Configuration

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Distance between buttons")]
            public float BetweenButtons = 0.02f;

            [JsonProperty(PropertyName = "FadeIn and FadeOut time")]
            public float FadeTime = 0.3f;
            
            [JsonProperty(PropertyName = "List of menus", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ConfigMenu> Menus = new List<ConfigMenu> { new ConfigMenu() };
        }

        private class ConfigMenu
        {
            [JsonIgnore] public CuiPanel Menu;
            [JsonIgnore] public CuiLabel MenuText;
            
            [JsonProperty(PropertyName = "Menu title")]
            public string Name = "Panel";

            [JsonProperty(PropertyName = "Menu title color")]
            public string NameColor = "#ffffff";

            [JsonProperty(PropertyName = "Menu title size")]
            public short NameSize = 12;

            [JsonProperty(PropertyName = "Menu title height")]
            public float NameHeight = 0.1f;

            [JsonProperty(PropertyName = "Menu permission")]
            public string Permission = "gamemenu.use";

            [JsonProperty(PropertyName = "Chat command to open the menu")]
            public string CommmandChat = "menu";

            [JsonProperty(PropertyName = "Console command to open the menu")]
            public string CommmandConsole = "menu";

            [JsonProperty(PropertyName = "Background color")]
            public string BackgroundColor = "0.66 0.66 0.66 0.9";
            
            [JsonProperty(PropertyName = "Menu buttons", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ConfigButton> Buttons = new List<ConfigButton> { new ConfigButton() };
        }

        private class ConfigButton
        {
            [JsonIgnore] public CuiButton Button;
            
            [JsonProperty(PropertyName = "Button color")]
            public string ButtonColor = "0.0 0.0 0.0 1.0";

            [JsonProperty(PropertyName = "Text color")]
            public string TextColor = "#ffffff";

            [JsonProperty(PropertyName = "Text size")]
            public short TextSize = 12;

            [JsonProperty(PropertyName = "Button width")]
            public float ButtonWidth = 0.4f;

            [JsonProperty(PropertyName = "Button height")]
            public float ButtonHeight = 0.1f;

            [JsonProperty(PropertyName = "Button text")]
            public string Text = "Accept TP";

            [JsonProperty(PropertyName = "Execute chat (true) or console (false) command")]
            public bool IsChatCommand = true;

            [JsonProperty(PropertyName = "Executing command")]
            public string Command = "/tpa";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();

        #endregion
        
        #region Hooks

        // ReSharper disable once UnusedMember.Local
        private void Init()
        {
            LoadConfig();
            
            cmd.AddConsoleCommand("gamemenu.exec", this, arg =>
            {
                if (!arg.HasArgs(2))
                    return false;

                var isChat = arg.Args[0] == "chat";
                SendCommand(arg.Connection, arg.Args.Skip(1).ToArray(), isChat);
                
                return false;
            });

            var menusCount = _config.Menus.Count;
            var btwButtons = _config.BetweenButtons;
            var fadeTime = _config.FadeTime;

            _backgroundButton = new CuiButton
            {
                Button =
                {
                    Close = "GameMenuCUI",
                    Color = "0.0 0.0 0.0 0.0",
                    FadeIn = fadeTime
                },
                Text =
                {
                    Text = string.Empty
                },
                RectTransform =
                {
                    AnchorMin = "0.0 0.0",
                    AnchorMax = "1.0 1.0"
                }
            };
            
            for (var i = 0; i < menusCount; i++)
            {
                var menu = _config.Menus[i];
                
                // Registering permissions
                var perm = menu.Permission;
                if (!string.IsNullOrEmpty(perm) && !permission.PermissionExists(perm, this))
                    permission.RegisterPermission(perm, this);
                
                // Loading CUIs
                
                var buttonsCount = menu.Buttons.Count;
                var totalHeight = 0.0f;
                var sum = 0f;
                for (var i2 = 0; i2 < buttonsCount; i2++)
                {
                    sum += menu.Buttons[i2].ButtonHeight;
                }
                
                var maxWidth = 0f;
                for (var i2 = 0; i2 < buttonsCount; i2++)
                {
                    var buttonWidth = menu.Buttons[i2].ButtonWidth;
                    if (buttonWidth > maxWidth)
                        maxWidth = buttonWidth;
                }
                
                var maxHeight = sum + btwButtons * (buttonsCount - 1);
                
                // Loading menu
                var mWidth = maxWidth + 2 * btwButtons;
                var mWidthMin = (1.0f - mWidth) / 2;
                var mWidthMax = mWidthMin + mWidth;

                var nHeight = menu.NameHeight;
                var mHeight = 3 * btwButtons + maxHeight + nHeight;
                var mHeightMin = (1.0f - mHeight) / 2;
                var mHeightMax = mHeightMin + mHeight;
                
                menu.Menu = new CuiPanel
                {
                    Image =
                    {
                        Color = menu.BackgroundColor,
                        FadeIn = fadeTime
                    },
                    CursorEnabled = true,
                    RectTransform =
                    {
                        AnchorMin = $"{mWidthMin} {mHeightMin}",
                        AnchorMax = $"{mWidthMax} {mHeightMax}"
                    }
                };

                var nHeightMax = mHeightMax - btwButtons;
                var nHeightMin = nHeightMax - nHeight;
                
                menu.MenuText = new CuiLabel
                {
                    Text =
                    {
                        Text = menu.Name,
                        Align = TextAnchor.MiddleCenter,
                        Color = menu.NameColor,
                        FadeIn = fadeTime,
                        FontSize = menu.NameSize
                    },
                    RectTransform =
                    {
                        AnchorMin = $"{mWidthMin} {nHeightMin}",
                        AnchorMax = $"{mWidthMax} {nHeightMax}"
                    }
                };
                
                // Loading buttons
                for (var i2 = 0; i2 < buttonsCount; i2++)
                {
                    var button = menu.Buttons[i2];
                    var width = button.ButtonWidth;
                    var height = button.ButtonHeight;

                    var widthMin = (1.0f - width) / 2;
                    var widthMax = widthMin + width;

                    var heightMin = mHeightMin + totalHeight + (i2 + 1) * btwButtons;
                    var heightMax = heightMin + height;

                    totalHeight += height;
                    var type = button.IsChatCommand ? "chat" : "console";

                    button.Button = new CuiButton
                    {
                        Text =
                        {
                            Text = $"<color={button.TextColor}>{button.Text}</color>",
                            FontSize = button.TextSize,
                            Align = TextAnchor.MiddleCenter,
                            FadeIn = fadeTime
                        },
                        Button =
                        {
                            Color = button.ButtonColor,
                            Command = $"gamemenu.exec {type} {button.Command}",
                            FadeIn = fadeTime
                        },
                        RectTransform =
                        {
                            AnchorMin = $"{widthMin} {heightMin}",
                            AnchorMax = $"{widthMax} {heightMax}"
                        }
                    };
                }
                
                // Registering chat commands for menus
                if (!string.IsNullOrEmpty(menu.CommmandChat))
                    cmd.AddChatCommand(menu.CommmandChat, this, (player, command, args) =>
                    {
                        if (!CanUse(player, perm))
                        {
                            player.ChatMessage(GetMsg("No Permission", player.UserIDString));
                            return;
                        }
                        
                        ShowUI(player, menu);
                    });
                
                // Registering console commands for menus
                if (!string.IsNullOrEmpty(menu.CommmandConsole))
                    cmd.AddConsoleCommand(menu.CommmandConsole, this, arg =>
                    {
                        if (arg.Connection == null || arg.IsRcon)
                        {
                            arg.ReplyWith(GetMsg("Only Player"));
                            return true;
                        }

                        var player = BasePlayer.FindByID(arg.Connection.userid);
                        if (!CanUse(player, perm))
                        {
                            arg.ReplyWith(GetMsg("No Permission", player.UserIDString));
                            return true;
                        }
                        
                        ShowUI(player, menu);
                        return false;
                    });
            }
        }

        // ReSharper disable once UnusedMember.Local
        private void Unload()
        {
            var playersCount = BasePlayer.activePlayerList.Count;
            for (var i = 0; i < playersCount; i++)
                CuiHelper.DestroyUi(BasePlayer.activePlayerList[i], "GameMenuCUI");
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"No Permission", "You don't have enough permission to run this command!"},
                {"Only Player", "This command can be used only by players!"}
            }, this);
            
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"No Permission", "У Вас недостаточно прав на выполнение данной команды!"},
                {"Only Player", "Данная команда доступна только игрокам!"}
            }, this, "ru");
        }

        #endregion
        
        #region Helpers

        private void ShowUI(BasePlayer player, ConfigMenu menu)
        {
            // ReSharper disable once UseObjectOrCollectionInitializer
            var container = new CuiElementContainer();
            container.Add(_backgroundButton, name:"GameMenuCUI");
            container.Add(menu.Menu, "GameMenuCUI", "GameMenuCUIBackground");
            container.Add(menu.MenuText, "GameMenuCUI", "GameMenuCUIBackgroundText");


            var buttonsCount = menu.Buttons.Count;
            for (var i = 0; i < buttonsCount; i++)
                container.Add(menu.Buttons[i].Button, "GameMenuCUI", "GameMenuCUIButton");

            CuiHelper.DestroyUi(player, "GameMenuCUI");
            CuiHelper.AddUi(player, container);
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        private void SendCommand(Connection conn, string[] args, bool isChat)
        {
            if (!Net.sv.IsConnected())
                return;

            var command = string.Empty;
            var argsLength = args.Length;
            for (var i = 0; i < argsLength; i++)
                command += $"{args[i]} ";
            
            if (isChat)
                command = $"chat.say {command.QuoteSafe()}";
            
            Net.sv.write.Start();
            Net.sv.write.PacketID(Message.Type.ConsoleCommand);
            Net.sv.write.String(command);
            Net.sv.write.Send(new SendInfo(conn));
        }

        private bool CanUse(BasePlayer player, string perm) =>
            player.IsAdmin || string.IsNullOrEmpty(perm) || permission.UserHasPermission(player.UserIDString, perm);

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
    }
}