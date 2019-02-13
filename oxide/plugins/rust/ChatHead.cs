// Reference: System.Drawing

/*
TODO:
- Add option for StartProtection to display "PROTECTED"
- Add option to show if player is armed or harmless (weapons in inventory)
- Add support for clan plugins with option to disable or only show for clan members
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ChatHead", "Wulf/lukespragg", "1.0.1", ResourceId = 1803)]
    [Description("Displays chat messages above player to other players in range")]

    class ChatHead : RustPlugin
    {
        #region Initialization

        //bool showArmed;

        string textColor;
        int textSize;

        protected override void LoadDefaultConfig()
        {
            // Options
            //Config["Show Armed (true/false)"] = showArmed = GetConfig("Show Armed (true/false)", false);

            // Settings
            Config["Text Color (# Hex Format)"] = textColor = GetConfig("Text Color (# Hex Format)", "#ffffff");
            Config["Text Size (Default 25)"] = textSize = GetConfig("Text Size (Default 25)", 25);

            SaveConfig();
        }

        void Init() => LoadDefaultConfig();

        #endregion

        #region Chat Handling

        readonly Dictionary<string, string> lastMessage = new Dictionary<string, string>();

        void OnPlayerChat(ConsoleSystem.Arg arg)
        {
            var player = (BasePlayer)arg.Connection.player;
            var message = arg.GetString(0);

            if (lastMessage.ContainsKey(player.UserIDString)) lastMessage[player.UserIDString] = message;
            else lastMessage.Add(player.UserIDString, message);

            foreach (var target in BasePlayer.activePlayerList) DrawChat(target, player);
        }

        void DrawChat(BasePlayer target, BasePlayer player)
        {
            var distance = Vector3.Distance(target.transform.position, player.transform.position);
            if (!target.IsConnected || !player.IsConnected || distance >= 20) return;

            var message = lastMessage[player.UserIDString];
            //var armed = player.GetActiveItem()?.GetHeldEntity();
            var color = textColor.Contains("#") ? ColorTranslator.FromHtml(textColor) : ColorTranslator.FromHtml("#{textColor}");

            target.SendConsoleCommand("ddraw.text", 0.1f, color, player.transform.position + new Vector3(0, 1.9f, 0), message);
            timer.Repeat(0.1f, 80, () =>
            {
                if (!target.IsConnected || !player.IsConnected || !Equals(message, lastMessage[player.UserIDString])) return;

                var format = $"<size={textSize}>{lastMessage[player.UserIDString]}</size>";
                target.SendConsoleCommand("ddraw.text", 0.1f, color, player.transform.position + new Vector3(0, 1.9f, 0), format);
            });
        }

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
    }
}