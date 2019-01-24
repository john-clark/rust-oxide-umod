using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Position", "Spicy", "1.0.2", ResourceId = 2192)]
    [Description("Shows players their positions.")]
    public class Position : CovalencePlugin
    {
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string> { ["Position"] = "Position: {0}." }, this);
            lang.RegisterMessages(new Dictionary<string, string> { ["Position"] = "Position: {0}." }, this, "fr");
            lang.RegisterMessages(new Dictionary<string, string> { ["Position"] = "Posici�n: {0}." }, this, "es");
            lang.RegisterMessages(new Dictionary<string, string> { ["Position"] = "Позиция: {0}." }, this, "ru");
        }

        [Command("position"), Permission("position.use")]
        private void cmdPosition(IPlayer player, string command, string[] args) =>
            player.Reply(string.Format(lang.GetMessage("Position", this, player.Id), player.Position().ToString()));
    }
}
