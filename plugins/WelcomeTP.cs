using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Welcome TP", "Ryan", "1.1.1")]
    [Description("Teleports players to a position if they're new")]
    public class WelcomeTP : CovalencePlugin
    {
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private List<GenericPosition> Positions = new List<GenericPosition>();
        private System.Random random = new System.Random();

        private const string UsedPerm = "welcometp.used";
        private const string SetPerm = "welcometp.set";

        #region Lang

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Welcome"] = "Welcome to {0}, {1}! You've been teleported to our hub",
                ["PositionSet"] = "You've successfully added your current position to the data file",
                ["Permission"] = "You don't have permission to use that command"
            }, this);
        }

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(UsedPerm, this);
            permission.RegisterPermission(SetPerm, this);
            Positions = Interface.Oxide.DataFileSystem.ReadObject<List<GenericPosition>>(Name);
        }

        private void OnServerSave() => Interface.Oxide.DataFileSystem.WriteObject(Name, Positions);

        private void OnUserConnected(IPlayer player)
        {
            if (!player.HasPermission(UsedPerm))
            {
                if (Positions.Count < 1) return;
                player.Teleport(Positions[random.Next(Positions.Count)]);
                permission.GrantUserPermission(player.Id, UsedPerm, this);
                timer.Once(2f, () =>
                {
                    player.Reply(Lang("Welcome", player.Id, server.Name, player.Name));
                });
            }
        }

        #endregion

        #region Commands

        [Command("tpset")]
        private void SetCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(SetPerm))
            {
                player.Reply(Lang("Permission", player.Id));
                return;
            }
            if(!Positions.Contains(player.Position()))
                Positions.Add(player.Position());
            player.Reply(Lang("PositionSet", player.Id));
        }

        #endregion
    }
}
