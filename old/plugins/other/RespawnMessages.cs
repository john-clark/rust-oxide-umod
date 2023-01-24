using System;using System.Collections.Generic;using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("RespawnMessages", "Kappasaurus", "0.2.1", ResourceId = 1996)]
    [Description("Customize respawn messages sent to players on respawn")]

    class RespawnMessages : RustPlugin
    {
        [PluginReference]
        Plugin PopupNotifications;

        bool chatMessage;
        bool popupMessage;

        protected override void LoadDefaultConfig()
        {
            Config["Chat Message (true/false)"] = chatMessage = GetConfig("Chat Message (true/false)", true);
            Config["Popup Message (true/false)"] = popupMessage = GetConfig("Popup Message (true/false)", false);

            SaveConfig();
        }
        void Init()        {            LoadDefaultConfig();

            // English
            lang.RegisterMessages(new Dictionary<string, string> { ["Respawn"] = "Hey, try not to die this time!" }, this);        }        void OnPlayerRespawned(BasePlayer player)
        {
            if (chatMessage) SendReply(player, lang.GetMessage("Respawn", this, player.UserIDString));
            if (popupMessage) PopupNotifications?.Call("CreatePopupNotification", lang.GetMessage("Respawn", this, player.UserIDString), player);
        }

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));
    }
}
