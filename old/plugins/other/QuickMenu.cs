using System.Collections.Generic;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using System;
using System.Linq;
using Oxide.Core.Configuration;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("QuickMenu", "Xianith", "0.0.7", ResourceId = 2419)]

    class QuickMenu : RustPlugin
    {
        #region Config
        private bool Changed = false;

        private Dictionary<string, string> Anchors = new Dictionary<string, string>
        {
           {"BotAnchMin", "0.02 0.025" }, //0  +.200
           {"BotAnchMax", "0.97 0.2" }, //0  +.2
           {"MidBotAnchMin", "0.02 0.225" },
           {"MidBotAnchMax", "0.97 0.4" },
           {"MidTopAnchMin", "0.02 0.425" },
           {"MidTopAnchMax", "0.97 0.6" },
           {"TopAnchMin", "0.02 0.625" }, //0  +.37
           {"TopAnchMax", "0.97 0.97" },
        };

        private Dictionary<ulong, screen> QuickMenuInfo = new Dictionary<ulong, screen>();
        class screen
        {
            public bool open;
        }

        private string TopButtonColor;
        private string TopButtonText;
        private string TopButtonFontColor;
        private int TopButtonFontSize;
        private string TopButtonCommand;

        private string MidTopButtonColor;
        private string MidTopButtonText;
        private int MidTopButtonFontSize;
        private string MidTopButtonFontColor;
        private string MidTopButtonCommand;

        private string MidBotButtonColor;
        private string MidBotButtonText;
        private int MidBotButtonFontSize;
        private string MidBotButtonFontColor;
        private string MidBotButtonCommand;

        private string BotButtonColor;
        private string BotButtonText;
        private int BotButtonFontSize;
        private string BotButtonFontColor;
        private string BotButtonCommand;

        private string BackgroundColor;

        private object GetConfig(string menu, string datavalue, object defaultValue)
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
        
        void LoadVariables()
        {
            TopButtonFontColor = Convert.ToString(GetConfig("Top Button", "Font Color", "0.60 0.70 0.51 1.0"));
            TopButtonFontSize = Convert.ToInt32(GetConfig("Top Button", "Font Size", "15"));
            TopButtonColor = Convert.ToString(GetConfig("Top Button", "Color", "0.31 0.37 0.20 1.0"));
            TopButtonText = Convert.ToString(GetConfig("Top Button", "Text", "Trade Accept"));
            TopButtonCommand = Convert.ToString(GetConfig("Top Button", "Command", "chat.say /trade accept"));

            MidTopButtonFontColor = Convert.ToString(GetConfig("Top Middle Button", "Font Color", "0.56 0.74 0.89 1.0"));
            MidTopButtonFontSize = Convert.ToInt32(GetConfig("Top Middle Button", "Font Size", "15"));
            MidTopButtonColor = Convert.ToString(GetConfig("Top Middle Button", "Color", "0.16 0.34 0.49 1.0"));
            MidTopButtonText = Convert.ToString(GetConfig("Top Middle Button", "Text", "Teleport Cancel"));
            MidTopButtonCommand = Convert.ToString(GetConfig("Top Middle Button", "Command", "chat.say /tpc"));

            MidBotButtonFontColor = Convert.ToString(GetConfig("Mid Bot Button", "Font Color", "0.96 0.60 0.55 1.0"));
            MidBotButtonFontSize = Convert.ToInt32(GetConfig("Mid Bot Button", "Font Size", "15"));
            MidBotButtonColor = Convert.ToString(GetConfig("Mid Bot Button", "Color", "0.56 0.20 0.15 1.0"));
            MidBotButtonText = Convert.ToString(GetConfig("Mid Bot Button", "Text", "Remover"));
            MidBotButtonCommand = Convert.ToString(GetConfig("Mid Bot Button", "Command", "chat.say /remove"));

            BotButtonFontColor = Convert.ToString(GetConfig("Bot Button", "Font Color", "1 1 1 1.0"));
            BotButtonFontSize = Convert.ToInt32(GetConfig("Bot Button", "Font Size", "15"));
            BotButtonColor = Convert.ToString(GetConfig("Bot Button", "Color", "0.34 0.34 0.34 1.0"));
            BotButtonText = Convert.ToString(GetConfig("Bot Button", "Text", "CANCEL"));

            BackgroundColor = Convert.ToString(GetConfig("Settings", "Background Color", "0.1 0.1 0.1 0.98"));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        void Loaded()
        {
            LoadVariables();
        }

        void Unload()
        {
            foreach (BasePlayer p in BasePlayer.activePlayerList)
            {
                DestroyGui(p);
            }
        }

        private void DestroyGui(BasePlayer player)
        {
            if (QuickMenuInfo.ContainsKey(player.userID))
                QuickMenuInfo.Remove(player.userID);
            CuiHelper.DestroyUi(player, "quickMenu");
        }
        #endregion

        #region GUI Display
        private void OpenQuickMenu(BasePlayer player)
        {
            if (!QuickMenuInfo.ContainsKey(player.userID))
                QuickMenuGuiCreate(player);
            return;
        }

        private void DestroyQuickMenu(BasePlayer player)
        {
            if (QuickMenuInfo.ContainsKey(player.userID))
                if (QuickMenuInfo[player.userID].open)
                    QuickMenuInfo[player.userID].open = false;
            CuiHelper.DestroyUi(player, "quickMenu");
        }
        #endregion

        #region GUI Generation
        public void QuickMenuGuiCreate(BasePlayer player)
        {
            if (player == null)
                return;

            var quickMenuElements = new CuiElementContainer();
            var quickMain = quickMenuElements.Add(new CuiPanel
            {
                Image = { Color = $"{BackgroundColor}" },
                RectTransform = { AnchorMin = "0.395 0.25", AnchorMax = "0.59 0.6" },
                CursorEnabled = true,
            }, "Overlay", "quickMenu");

            // Cancel Button
            quickMenuElements.Add(new CuiButton
            {
                Button = { Command = $"UI_QuickMenu", Color = $"{BotButtonColor}" },
                RectTransform = { AnchorMin = Anchors["BotAnchMin"], AnchorMax = Anchors["BotAnchMax"] },
                Text = { Color = $"{BotButtonFontColor}", Text = $"{BotButtonText}", FontSize = BotButtonFontSize, Align = TextAnchor.MiddleCenter }
            }, quickMain);

            // Bot Button
            quickMenuElements.Add(new CuiButton
            {
                Button = { Command = $"UI_QuickMenu_Cmd {MidBotButtonCommand}", Color = $"{MidBotButtonColor}" },
                RectTransform = { AnchorMin = Anchors["MidBotAnchMin"], AnchorMax = Anchors["MidBotAnchMax"] },
                Text = { Color = $"{MidBotButtonFontColor}", Text = $"{MidBotButtonText}", FontSize = MidBotButtonFontSize, Align = TextAnchor.MiddleCenter }
            }, quickMain);

            // Middle Button
            quickMenuElements.Add(new CuiButton
            {
                Button = { Command = $"UI_QuickMenu_Cmd {MidTopButtonCommand}", Color = $"{MidTopButtonColor}" },
                RectTransform = { AnchorMin = Anchors["MidTopAnchMin"], AnchorMax = Anchors["MidTopAnchMax"] },
                Text = { Color = $"{MidTopButtonFontColor}", Text = $"{MidTopButtonText}", FontSize = MidTopButtonFontSize, Align = TextAnchor.MiddleCenter }
            }, quickMain);

            // Top Button
            quickMenuElements.Add(new CuiButton
            {
                Button = { Command = $"UI_QuickMenu_Cmd {TopButtonCommand}", Color = $"{TopButtonColor}" },
                RectTransform = { AnchorMin = Anchors["TopAnchMin"], AnchorMax = Anchors["TopAnchMax"] },
                Text = { Color = $"{TopButtonFontColor}", Text = $"{TopButtonText}", FontSize = TopButtonFontSize, Align = TextAnchor.MiddleCenter }
            }, quickMain);
            CuiHelper.AddUi(player, quickMenuElements);
        }
        #endregion

        #region Commands
        [ChatCommand("qm")]
            void cmdQuickMenu(BasePlayer player, string command, string[] args)
            {
                if (QuickMenuInfo.ContainsKey(player.userID))
                    if (!QuickMenuInfo[player.userID].open)
                    {
                        QuickMenuInfo[player.userID].open = true;
                        QuickMenuGuiCreate(player);
                    }
                    else
                        DestroyQuickMenu(player);
                else
                    QuickMenuInfo.Add(player.userID, new screen { open = false });
                    OpenQuickMenu(player);
            }

        [ConsoleCommand("UI_QuickMenu")]
            private void cmdUI_QuickMenu(ConsoleSystem.Arg arg)
            {
                var player = arg.Connection.player as BasePlayer;
                if (player == null)
                    return;

                if (QuickMenuInfo.ContainsKey(player.userID))
                    if (!QuickMenuInfo[player.userID].open)
                    {
                        QuickMenuInfo[player.userID].open = true;
                        QuickMenuGuiCreate(player);
                    }
                    else
                        DestroyQuickMenu(player);
                else
                    QuickMenuInfo.Add(player.userID, new screen { open = false });
                    OpenQuickMenu(player);
            }

        [ConsoleCommand("UI_QuickMenu_Cmd")]
            private void cmdUI_QuickMenu_Cmd(ConsoleSystem.Arg arg)
            {
                var cmd = "";
                var player = arg.Connection.player as BasePlayer;
                if (player == null)
                    return;

                CuiHelper.DestroyUi(player, "quickMenu");
                QuickMenuInfo[player.userID].open = false;

               cmd = string.Join(" ", arg.Args.Skip(0).ToArray());
               player.Command(cmd);
            }
        #endregion
    }
}
