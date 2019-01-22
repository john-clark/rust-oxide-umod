namespace Oxide.Plugins
{
    [Info("NoGreen", "JakeKillsAll", 1.2)]
    [Description("No Green Admin")]

    class NoGreen : RustPlugin
    {
        //intercepts the chat message
        object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            BasePlayer player = (BasePlayer)arg.Connection.player;
            string Message = arg.GetString(0);
            
            if (string.IsNullOrEmpty(Message)) return false;
            rust.BroadcastChat($"<color=#5af>{player.displayName}</color>", Message, player.UserIDString);
            Puts($"{player.displayName}: {Message}");

            return true;
        }
    }
}