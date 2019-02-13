// Requires: PopupNotifications
// Requires: DeathNotes

using Oxide.Core.Plugins;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Death Notes GUI", "LaserHydra", "1.0.0")]
    [Description("Provides an GUI output option for Death Notes")]
    public class DeathNotesGui : RustPlugin
    {
        [PluginReference("PopupNotifications")]
        Plugin _popupNotifications;
        
        private void OnDeathNotice(Dictionary<string, object> data, string message)
        {
            _popupNotifications?.Call("CreatePopupNotification", message);
        }
    }
}