using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Force Emote", "Wulf/lukespragg", "0.3.1")]
    [Description("Forces another player to do an emote on command")]
    public class ForceEmote : CovalencePlugin
    {
        #region Localization

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandEmote"] = "forceemote",
                ["CommandUsage"] = "Usage: {0} <name or id> <emote>",
                ["Emotes"] = "Available emotes: airhump, bird, facepalm, handsup, point, salute, surrender",
                ["ForcedPlayer"] = "You forced {0} to use the {1} emote!",
                ["NoEmote"] = "You must provide an available emote",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["NoPlayersFound"] = "No players found with name or ID '{0}'",
                ["PlayerForced"] = "{0} forced you use the {1} emote!",
                ["PlayersFound"] = "Multiple players were found, please specify: {0}"
            }, this);
        }

        #endregion Localization

        #region Initailization

        private const string permUse = "forceemote.use";

        private void Init()
        {
            permission.RegisterPermission(permUse, this);

            AddLocalizedCommand("CommandEmote", "EmoteCommand");
        }

        #endregion Initailization

        #region Command

        private void EmoteCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUse))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args.Length == 0)
            {
                Message(player, "CommandUsage", command);
                return;
            }

            if (args[0] == "emotes")
            {
                Message(player, "Emotes");
                return;
            }

            IPlayer target = FindPlayer(args[0], player);
            if (target == null)
            {
                return;
            }

            if (args.Length != 0)
            {
                string emote = args[1];

                if (emote.Length == 0)
                {
                    Message(player, "NoEmote");
                    return;
                }

                ForceEmotes(target, player, emote);
            }
        }

        #endregion Command

        #region Emotes

        private void ForceEmotes(IPlayer target, IPlayer player, string arg)
        {
            PlayerSession targetSession = target.Object as PlayerSession;
            EmoteManagerServer emoteManager = targetSession?.WorldPlayerEntity.GetComponent<EmoteManagerServer>();
            if (emoteManager == null)
            {
                return;
            }

            string emoteName = null;
            int emoteIndex = 0;
            foreach (EmoteConfiguration data in emoteManager.Emotes.Data)
            {
                if (!data.NameKey.ToLower().Contains(arg.ToLower()))
                {
                    emoteName = data.NameKey;
                    break;
                }

                ++emoteIndex;
            }

            if (emoteName == null)
            {
                Message(player, "InvalidEmote", arg);
                return;
            }

            emoteManager.BeginEmoteServer(emoteIndex);
            Message(target, "PlayerForced", player.Name.Sanitize(), emoteName);
            Message(player, "ForcedPlayer", target.Name.Sanitize(), emoteName);
        }

        #endregion Emotes

        #region Helpers

        private IPlayer FindPlayer(string nameOrId, IPlayer player)
        {
            IPlayer[] foundPlayers = players.FindPlayers(nameOrId).ToArray();
            if (foundPlayers.Length > 1)
            {
                Message(player, "PlayersFound", string.Join(", ", foundPlayers.Select(p => p.Name).ToArray()));
                return null;
            }

            IPlayer target = foundPlayers.Length == 1 ? foundPlayers[0] : null;
            if (target == null || !target.IsConnected)
            {
                Message(player, "NoPlayersFound", nameOrId);
                return null;
            }

            return target;
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void AddLocalizedCommand(string key, string command)
        {
            foreach (string language in lang.GetLanguages(this))
            {
                Dictionary<string, string> messages = lang.GetMessages(language, this);
                foreach (KeyValuePair<string, string> message in messages.Where(m => m.Key.Equals(key)))
                {
                    if (!string.IsNullOrEmpty(message.Value))
                    {
                        AddCovalenceCommand(message.Value, command);
                    }
                }
            }
        }

        private void Message(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));

        #endregion Helpers
    }
}
