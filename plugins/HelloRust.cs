using UnityEngine;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("HelloRust", "open_mailbox", "0.1")]
    [Description("A simple Hello World example for writing Rust plugins")]
    partial class HelloRust : RustPlugin
    {
        [ChatCommand("hellorust")]
        void CommandHello(BasePlayer player, string command, string[] args)
        {
            Puts(player.displayName + "typed /hello");
            player.IPlayer.Reply("Hello World!");
            ShowGui(player);
        }

        void Init()
        {
            Puts("Hello Rust initialized.");
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Hello Rust default config loaded.");
        }

        void Loaded()
        {
            Puts("Hello Rust loaded.");
        }

        void OnPlayerInit(BasePlayer player)
        {
        }

        private void ShowGui(BasePlayer player)
        {
            Puts(player.displayName + " has connected");

            var elements = new CuiElementContainer();
            var panel = new CuiPanel
            {
                Image = { Color = "1.0 0.0 0.0 0.5" },
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            };

            var panelName = elements.Add(panel);

            var button = new CuiButton
            {
                Button = { Close = panelName, Color = "0 0.5 0 1" },
                RectTransform = { AnchorMin = "0.4 0.4", AnchorMax = "0.6 0.6" },
                Text = { Text = "Close", FontSize = 22, Align = TextAnchor.MiddleCenter }
            };

            elements.Add(button, panelName);

            CuiHelper.AddUi(player, elements);
        }

        void Unload()
        {
            Puts("Hello Rust unloaded.");
        }
    }
}
