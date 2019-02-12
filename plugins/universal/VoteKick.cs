using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    // TODO LIST
    // Nothing, yet.

    [Info("VoteKick", "Kappasaurus", "1.0.2")]

    class VoteKick : CovalencePlugin
    {
        #region Variables

        private List<string> voted = new List<string>();
        private bool activeVote;
        private int votes = 0;
        private IPlayer target;
        private Timer voteTimer;

        // CONFIG RELATED
        private float percentageRequired = 0.6f;
        private int playersRequired = 10;

        #endregion

        #region Hooks

        void Init()
        {
            permission.RegisterPermission("votekick.able", this);
            LoadConfig();
        }

        void OnUserDisconnected(IPlayer player)
        {
            if (target != null && activeVote && player.Id == target.Id)
            {
                server.Broadcast(lang.GetMessage("Target Disconnected", this));
                activeVote = false;
                voteTimer?.Destroy();
                voted.Clear();
                target = null;
            }
        }

        #endregion

        #region Command

        [Command("votekick")]
        void VoteKickCmd(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission("votekick.able"))
            {
                player.Message(lang.GetMessage("No Permission", this, player.Id));
                return;
            }

            if (players.Connected.Count() < playersRequired)
            {
                player.Message(lang.GetMessage("Not Enough Players", this, player.Id));
                return;
            }

            if (activeVote)
            {
                player.Message(lang.GetMessage("Other Vote Open", this, player.Id));
                return;
            }

            if (args.Length == 0)
            {
                player.Message(lang.GetMessage("No Target", this, player.Id));
                return;
            }

            target = covalence.Players.FindPlayer(args[0]);

            if (target == null || !target.IsConnected)
            {
                player.Message(lang.GetMessage("Target Not Found", this, player.Id));
                return;
            }

            if (player.Equals(target))
            {
                player.Message(lang.GetMessage("Vote Self", this, player.Id));
                return;
            }

            if (target.IsAdmin)
            {
                player.Message(lang.GetMessage("No Permission", this, player.Id));
                return;
            }

            activeVote = true;
            server.Broadcast(lang.GetMessage("Kick Started", this).Replace("{player}", player.Name).Replace("{target}", target.Name));

            voteTimer = timer.Once(600f, () =>
            {
                if (!activeVote)
                    return;

                activeVote = false;
                target = null;
                server.Broadcast(lang.GetMessage("Timed Out", this));
                voted.Clear();
            });
        }

        [Command("vote")]
        void VoteCmd(IPlayer player, string command, string[] args)
        {
            var requiredVotes = players.Connected.Count() * percentageRequired;

            if (!activeVote)
            {
                player.Message(lang.GetMessage("No Vote Open", this, player.Id));
                return;
            }

            if (player.Equals(target))
            {
                player.Message(lang.GetMessage("Vote Self", this, player.Id));
                return;
            }

            if (voted.Contains(player.Id))
            {
                player.Message(lang.GetMessage("Already Voted", this, player.Id));
                return;
            }

             voted.Add(player.Id);
             player.Message(lang.GetMessage("Vote Placed", this, player.Id));

             if (voted.Count >= requiredVotes)
             {
                server.Broadcast(lang.GetMessage("Vote Successful", this));
                target.Kick(lang.GetMessage("Kick Message", this, player.Id));
                activeVote = false;
                 target = null;
                voteTimer?.Destroy();
                voted.Clear();
             }

        }

        #endregion

        #region Configuration

        private new void LoadConfig()
        {
            GetConfig(ref percentageRequired, "Percentage required");
            GetConfig(ref playersRequired, "Players required");

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => PrintWarning("Generating new configuration file...");

        #endregion

        #region Helpers

        private void GetConfig<T>(ref T variable, params string[] path)
        {
            if (path.Length == 0)
                return;

            if (Config.Get(path) == null)
            {
                Config.Set(path.Concat(new object[] { variable }).ToArray());
                PrintWarning($"Added field to config: {string.Join("/", path)}");
            }

            variable = (T)Convert.ChangeType(Config.Get(path), typeof(T));
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Kick Message"] = "You have been voted out.",
                ["Vote Self"] = "<size=12>Error, you can't vote to kick yourself.</size>",
                ["Timed Out"] = "<size=12>Error, not enough votes, vote cancelled.</size>",
                ["Vote Successful"] = "<size=12>Vote successful.</size>",
                ["Not Enough Players"] = "<size=12>Error, not enough players online.</size>",
                ["No Arguments"] = "<size=12>Error, no arguments supplied.</size>",
                ["No Target"] = "<size=12>Error, no target supplied.</size>",
                ["Target Not Found"] = "<size=12>Error, player not found, try more specific terms.</size>",
                ["Already Voted"] = "<size=12>Error, you already voted.</size>",
                ["Vote Placed"] = "<size=12>Vote sucessfully placed.</size>",
                ["No Vote Open"] = "<size=12>Error, no vote open.</size>",
                ["No Permission"] = "<size=12>Error, no permission.</size>",
                ["Kick Started"] = "<size=12>{player} called a kick vote on {target}.</size> Use /vote to vote yes.",
                ["Target Disconnected"] = "<size=12>Vote cancelled, target disconnected.</size>",
                ["Another Vote Open"] = "<size=12>Error, another vote is already open.</size>"
            }, this);
        }

        #endregion
    }
}