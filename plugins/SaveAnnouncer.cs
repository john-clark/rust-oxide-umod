using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Save Announcer", "Ryan", "1.0.4")]
    [Description("Announces to all players when the server saves")]
    public class SaveAnnouncer : RustPlugin
    {
        [PluginReference]
        private Plugin GUIAnnouncements;

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file.");
            Config["Settings", "Enable Console Notice"] = false;
            Config["Entity Settings", "Enable Entity Message"] = true;
            Config["Entity Settings", "Amount To Trigger Entity Message"] = 150000;
            Config["GUIAnnouncements Settings", "Enable GUI Announcements"] = false;
            Config["GUIAnnouncements Settings", "Banner Color"] = "Grey";
            Config["GUIAnnouncements Settings", "Text Color"] = "White";
        }

        // Settings
        private bool consoleAnnoucement() => Convert.ToBoolean(Config["Settings", "Enable Console Notice"]);

        // Entity Settings
        private bool entAnnoucement() => Convert.ToBoolean(Config["Entity Settings", "Enable Entity Message"]);
        private int entAmount() => int.Parse(Config["Entity Settings", "Amount To Trigger Entity Message"].ToString());

        // GUIAnnouncements Settings
        private bool guiAnnoucements() => Convert.ToBoolean(Config["GUIAnnouncements Settings", "Enable GUI Announcements"]);
        private string guiColor() => Config["GUIAnnouncements Settings", "Banner Color"].ToString();
        private string guiTextColor() => Config["GUIAnnouncements Settings", "Text Color"].ToString();

        private string constructMsg()
        {
            int entCount = ConVar.Admin.ServerInfo().EntityCount;
            if (entAnnoucement())
            {
                if (entCount >= entAmount())
                {
                    return Lang("EntityMsg", null, entCount);
                }
                return Lang("AnnouncementMsg");
            }
            return Lang("AnnouncementMsg");
        }

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AnnouncementMsg"] = "Server saving, expect some lag...",
                ["EntityMsg"] = "Server is saving {0:n0} entities, expect some lag.",
                ["ConsoleMsg"] = "Server is saving {0:n0} entities."
            }, this);
        }

        private void OnServerSave()
        {
            if (consoleAnnoucement()) Puts(Lang("ConsoleMsg", null, ConVar.Admin.ServerInfo().EntityCount));
            if (guiAnnoucements())
            {
                GUIAnnouncements?.Call("CreateAnnouncement", constructMsg(), guiColor(), guiTextColor());
                return;
            }
            PrintToChat(constructMsg());
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
    }
}
