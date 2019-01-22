using System.Collections.Generic;
using Oxide.Core;
using System.Linq;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Whisper", "Ryan/mTrX", "1.1.1")]
    class Whisper : CovalencePlugin
    {
        #region Declaration

        private const string UsePerm = "whisper.use";
        private const string ReplyPerm = "whisper.reply";
        private string Command;
        private string ReplyCmd;

        #endregion

        #region Configuration

        private void LoadDefaultConfig()
        {
            Puts("Generating default configuration file");
            Config["Command"] = Command = "whisper";
            Config["Reply Command"] = ReplyCmd = "wr";
            Config.Save();
        }

        #endregion

        #region Lang

        private struct Msg
        {
            public const string Prefix = "Prefix";
            public const string Message = "Message";
            public const string NoPermission = "NoPermission";
            public const string Whispered = "Whispered";
            public const string InvalidArgs = "InvalidArgs";
            public const string NotId = "NotId";
            public const string NoPlayer = "NoPlayer";
            public const string InvalidArgsReply = "InvalidArgsReply";
            public const string Replied = "Replied";
            public const string Reply = "Reply;";
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Msg.Prefix] = "[#ff6666][WHISPER][/#] ",
                [Msg.Message] = "[#c0c0c0ff]{0}[/#]",
                [Msg.NoPermission] = "You don't have permission to use that commannd",
                [Msg.Whispered] = "Whispered {0} ({1}) with message '{2}'",
                [Msg.InvalidArgs] = "Invalid arguments. Usage: '{0} <id> <message>'",
                [Msg.InvalidArgsReply] = "Invalid arguments. Usage '{0} <message>'",
                [Msg.NotId] = "The ID you entered doesn't seem to be a valid Steam ID",
                [Msg.NoPlayer] = "No online player found with that Steam ID",
                [Msg.Replied] = "You have sent your reply",
                [Msg.Reply] = "[REPLY] {0} replied with message '{1}'"
            }, this);
        }

        #endregion

        #region Methods

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(UsePerm, this);
            permission.RegisterPermission(ReplyPerm, this);
            Command = (string) Config["Command"];
            ReplyCmd = (string) Config["Reply Command"];
            if (string.IsNullOrEmpty(Command) || string.IsNullOrEmpty(ReplyCmd))
            {
                LogWarning("Configuration file not valid");
                LoadDefaultConfig();
            }
            AddCovalenceCommand(Command, "WhisperCommand");
            AddCovalenceCommand(ReplyCmd, "ReplyCommand");
        }

        #endregion

        #region Commands

        private void WhisperCommand(IPlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !player.HasPermission(UsePerm))
            {
                player.Reply(Lang(Msg.NoPermission, player.Id));
                return;
            }
            if (args.Length < 2)
            {
                player.Reply(Lang(Msg.InvalidArgs, player.Id, Command));
                return;
            }
            var userId = args[0];
            if (!userId.IsSteamId())
            {
                player.Reply(Lang(Msg.NotId, player.Id));
                return;
            }
            var foundPlayer = players.FindPlayerById(userId);
            if (foundPlayer == null || !foundPlayer.IsConnected)
            {
                player.Reply(Lang(Msg.NoPlayer, player.Id));
                return;
            }
            var fullArgs = string.Join(" ", args.ToList().Skip(1).ToArray());
            player.Reply(Lang(Msg.Whispered, player.Id, foundPlayer.Name, foundPlayer.Id, fullArgs));
            foundPlayer.Reply(covalence.FormatText(Lang(Msg.Prefix, player.Id) + Lang(Msg.Message, player.Id, fullArgs)));
        }

        private void ReplyCommand(IPlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !player.HasPermission(ReplyPerm))
            {
                player.Reply(Lang(Msg.NoPermission, player.Id));
                return;
            }
            if (args.Length < 1)
            {
                player.Reply(Lang(Msg.InvalidArgsReply, player.Id, ReplyCmd));
                return;
            }
            var fullArgs = string.Join(" ", args);
            Puts(Lang(Msg.Reply, null, player.Name, fullArgs));
            player.Reply(Lang(Msg.Replied, player.Id));
        }

        #endregion
    }
}