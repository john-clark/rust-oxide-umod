using Oxide.Game.Rust;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("No Give Notices", "Wulf/lukespragg", "0.2.0")]
    [Description("Prevents F1 item giving notices from showing in the chat and console")]
    class NoGiveNotices : RustPlugin
    {
        private void Init()
        {
            List<string> logFilter = RustExtension.Filter.ToList();
            logFilter.Add("[ServerVar] giving");
            RustExtension.Filter = logFilter.ToArray();
        }

        private object OnServerMessage(string message, string name)
        {
            if (message.Contains("gave") && name == "SERVER")
            {
                return true;
            }

            return null;
        }

        private void Unload()
        {
            List<string> logFilter = RustExtension.Filter.ToList();
            logFilter.Remove("[ServerVar] giving");
            RustExtension.Filter = logFilter.ToArray();
        }
    }
}
