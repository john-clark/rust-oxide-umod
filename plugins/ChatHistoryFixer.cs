using Oxide;

namespace Oxide.Plugins
{
    [Info("Chat History Fixer", "Waizujin", 1.2)]
    [Description("")]
    public class ChatHistoryFixer : RustPlugin
    {
        [ChatCommand("fixchat")]
        private void FixChatCommand(BasePlayer player, string command, string[] args)
        {
            timer.Repeat(30, 0, () => PrintToChat("Server: Blank Lines to Fix Chat...\n\n\n\n\n"));
        }
    }
}
