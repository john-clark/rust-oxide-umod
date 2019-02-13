using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("SuicideKill", "Ankawi", "1.0.0")]
    [Description("Allows you to suicide, kill, or hurt players through chat and/or console commands")]
    class SuicideKill: CovalencePlugin
    {
        private const string killPerm = "suicidekill.kill";
        private const string hurtPerm = "suicidekill.hurt";
        private const string suicidePerm = "suicidekill.suicide";

        private void Init()
        {
            LoadDefaultMessages();
            permission.RegisterPermission(killPerm, this);
            permission.RegisterPermission(hurtPerm, this);
            permission.RegisterPermission(suicidePerm, this);
        }
        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["YouWereKilled"] = "You were killed by {0}",
                ["YouWereHurt"] = "You were damaged for {0} health by {1}",
                ["Suicide"] = "You killed yourself",
                ["PlayerNotFound"] = "{0} was not found",
                ["TargetSyntax"] = "{0} <target>",
                ["SuicideSyntax"] = "{0}",
                ["HurtSyntax"] = "{0} <target> <amount>",
                ["NoAccess"] = "You don't have access to this command"
            }, this, "en");
        }
        [Command("kill")]
        private void KillCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(killPerm))
            {
                player.Reply(GetMsg("NoAccess", player.Id));
                return;
            }
            if (args.Length == 0)
            {
                player.Reply(GetMsg("TargetSyntax", player.Id, command));
                return;
            }

            var target = players.FindPlayer(args[0]);

            if (target == null || !target.IsConnected)
            {
                player.Reply(GetMsg("PlayerNotFound", player.Id, args[0].Sanitize()));
                return;
            }
            if (target.Equals(player))
            {
                player.Reply(GetMsg("TargetSyntax", player.Id, command));
                return;
            }
            target.Hurt(1000);
            target.Message(GetMsg("YouWereKilled", target.Id, player.Name.Sanitize()));
        }

        [Command("suicide")]
        private void SuicideCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(suicidePerm))
            {
                player.Reply(GetMsg("NoAccess", player.Id));
                return;
            }
            if (args.Length != 0)
            {
                player.Reply(GetMsg("SuicideSyntax", player.Id, command));
                return;
            }
            player.Hurt(1000);
            player.Reply(GetMsg("Suicide", player.Id));
        }

        [Command("hurt")]
        private void DamageCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(hurtPerm))
            {
                player.Reply(GetMsg("NoAccess", player.Id));
                return;
            }
            if (args.Length != 2)
            {
                player.Reply(GetMsg("HurtSyntax", player.Id, command));
                return;
            }
            var target = players.FindPlayer(args[0]);

            if (target == null || !target.IsConnected)
            {
                player.Reply(GetMsg("PlayerNotFound", player.Id, args[0].Sanitize()));
                return;
            }

            float amount = float.Parse(args[1]);
            target.Hurt(amount);
            target.Message(GetMsg("YouWereHurt", target.Id, amount, player.Name.Sanitize()));
        }

        #region Helpers

        private string GetMsg(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
    }
}