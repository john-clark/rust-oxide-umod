using System;

namespace Oxide.Plugins
{
    [Info("Anon", "Iv Misticos", "0.0.2")]
    [Description("Anon messages")]
    class Anon : RustPlugin
    {
        [ChatCommand("anon")]
        void AnonCMD(BasePlayer player, string cmd, string[] args)
        {
            Server.Broadcast(string.Join(" ", args), "<color=#ff8000>Аноним:</color>", 0, "");
            Log(player.displayName + ": " + string.Join(" ", args));
        }

        [ChatCommand("an")]
        void AnonCMD2(BasePlayer player, string cmd, string[] args)
        {
            AnonCMD(player, cmd, args);
        }

        void Log(string message)
        {
            LogToFile("", $"[{DateTime.Now}] " + message, this, true);
        }
    }
}
