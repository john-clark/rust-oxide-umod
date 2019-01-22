using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Facepunch.Extend;
using Oxide.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Oxide.Plugins
{
    [Info("Team Manager", "Quantum", "1.0.3")]
    [Description("Manage teams from commands.")]

    class TeamManager : RustPlugin
    {
        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["SyntaxError"] = "Syntax Error!",
                ["PlayerNotFound"] = "Player not found!",
                ["MultiplePlayersFound"] = "Multiple players found",
                ["TeamsDisabled"] = "Teams are disabled on this server!",
                ["NotInTeam"] = "You are not in a team!",
                ["CannotPromoteYourself"] = "You cannot promote yourself!",
                ["CannotInviteYourself"] = "You cannot invite yourself!",
                ["NotTeamLeader"] = "You are not the team leader!",
                ["NotInYourTeam"] = "does not seem to be in your team!",
                ["Promoted"] = "Promoted",
                ["AlreadyInTeam"] = "You're already in a team!",
                ["Invited"] = "Invited"
            }, this);
        }
        #endregion Localization

        #region Commands
        [ChatCommand("team")]
        void Team(BasePlayer player, string command, string[] args)
        {
            string msgSyntaxError = lang.GetMessage("SyntaxError", this, player.UserIDString); // Used more than once

            if (args.Length < 1)
            {
                player.ChatMessage(msgSyntaxError);
                return;
            }

            if (RelationshipManager.maxTeamSize == 0)
            {
                player.ChatMessage(lang.GetMessage("TeamsDisabled", this, player.UserIDString));
            }
            else
            {
                switch (args[0].ToLower())
                {
                    case "invite":
                        {
                            if (args.Length <= 1)
                            {
                                player.ChatMessage(msgSyntaxError);
                                return;
                            }
                            Invite(player, args[1]);
                            break;
                        }
                    case "promote":
                        {
                            if (args.Length <= 1)
                            {
                                player.ChatMessage(msgSyntaxError);
                                return;
                            }

                            Promote(player, args[1]);
                            break;
                        }
                    case "create":
                        {
                            CreateTeam(player);
                            break;
                        }
                    default:
                        {
                            player.ChatMessage(msgSyntaxError);
                            break;
                        }
                }
            }
        }
        #endregion Commands

        #region Promote
        void Promote(BasePlayer player, string targetPlayer)
        {
            string msgPlayerNotFound = lang.GetMessage("PlayerNotFound", this, player.UserIDString); // Used more than once

            // Check if player is in team
            if (player.currentTeam == 0UL)
            {
                player.ChatMessage(lang.GetMessage("NotInTeam", this, player.UserIDString));
                return;
            }

            RelationshipManager.PlayerTeam aTeam = RelationshipManager.Instance.playerTeams[player.currentTeam];

            var aTarget = FindPlayersOnline(targetPlayer);
           
            // Check if player exists
            if (aTarget.Count <= 0)
            {
                player.ChatMessage(msgPlayerNotFound);
                return;
            }
            // Found multiple players
            if (aTarget.Count > 1)
            {
                player.ChatMessage(string.Format("{0}: {1}",
                                   lang.GetMessage("MultiplePlayersFound", this, player.UserIDString),
                                   string.Join(", ", aTarget.ConvertAll(p => p.displayName).ToArray())));
                return;
            }

            var theTarget = aTarget[0];

            // Null Check
            if (theTarget == null)
            {
                player.ChatMessage(msgPlayerNotFound);
                return;
            }
            // Check if target is player
            if (theTarget == player)
            {
                player.ChatMessage(lang.GetMessage("CannotPromoteYourself", this, player.UserIDString));
                return;
            }
            // Check if team leader
            if (aTeam.teamLeader != player.userID)
            {
                player.ChatMessage(lang.GetMessage("NotTeamLeader", this, player.UserIDString));
                return;
            }
            // Check if target is in player's team
            if (aTeam.teamID != theTarget.currentTeam)
            {
                player.ChatMessage(string.Format("{0} {1}",
                                   aTarget[0].displayName,
                                   lang.GetMessage("NotInYourTeam", this, player.UserIDString)));
                return;
            }
            // Promote target if all checks pass
            aTeam.SetTeamLeader(theTarget.userID);
            player.ChatMessage(string.Format("{0}: {1}",
                                             lang.GetMessage("Promoted", this, player.UserIDString),
                                             theTarget.displayName));
        }
        #endregion Promote

        #region Invite
        void Invite(BasePlayer player, string targetPlayer)
        {
            string msgPlayerNotFound = lang.GetMessage("PlayerNotFound", this, player.UserIDString); // Used more than once

            // Check if player is in team
            if (player.currentTeam == 0UL)
            {
                player.ChatMessage(lang.GetMessage("NotInTeam", this, player.UserIDString));
                return;
            }

            RelationshipManager.PlayerTeam aTeam = RelationshipManager.Instance.FindTeam(player.currentTeam);
            var aTarget = FindPlayersOnline(targetPlayer);
           
            // Check if player exists
            if (aTarget.Count <= 0)
            {
                player.ChatMessage(msgPlayerNotFound);
                return;
            }
            // Found Multiple players
            if (aTarget.Count > 1)
            {               
                player.ChatMessage(string.Format("{0}: {1}",
                                   lang.GetMessage("MultiplePlayersFound", this, player.UserIDString),
                                   string.Join(", ", aTarget.ConvertAll(p => p.displayName).ToArray())));
                return;
            }

            var theTarget = aTarget[0];

            // Null Check
            if (theTarget == null)
            {
                player.ChatMessage(msgPlayerNotFound);
                return;
            }
            // Check if team leader
            if (aTeam.teamLeader != player.userID)
            {
                player.ChatMessage(lang.GetMessage("NotTeamLeader", this, player.UserIDString));
                return;
            }
            // Check it target is player
            if (theTarget == player)
            {
                player.ChatMessage(lang.GetMessage("CannotInviteYourself", this, player.UserIDString));
                return;
            }

            // Invite if all checks pass
            aTeam.SendInvite(theTarget);
            player.ChatMessage(string.Format("{0}: {1}",
                                             lang.GetMessage("Invited", this, player.UserIDString),
                                             theTarget.displayName));
        }
        #endregion Invite

        #region CreateTeam
        void CreateTeam(BasePlayer player)
        {
            if (player.currentTeam != 0UL)
            {
                player.ChatMessage(lang.GetMessage("AlreadyInTeam", this, player.UserIDString));
                return;
            }

            RelationshipManager.PlayerTeam aTeam = RelationshipManager.Instance.CreateTeam();
            aTeam.teamLeader = player.userID;
            aTeam.AddPlayer(player);
        }
        #endregion CreateTeam 

        #region Misc
        private static List<BasePlayer> FindPlayersOnline(string nameOrIdOrIp)
        {
            var players = new List<BasePlayer>();
            if (string.IsNullOrEmpty(nameOrIdOrIp)) return players;
            foreach (var activePlayer in BasePlayer.activePlayerList.ToList())
            {
                if (activePlayer.UserIDString.Equals(nameOrIdOrIp))
                    players.Add(activePlayer);
                else if (!string.IsNullOrEmpty(activePlayer.displayName) && activePlayer.displayName.Contains(nameOrIdOrIp, CompareOptions.IgnoreCase))
                    players.Add(activePlayer);
                else if (activePlayer.net?.connection != null && activePlayer.net.connection.ipaddress.Equals(nameOrIdOrIp))
                    players.Add(activePlayer);
            }
            return players;
        }
        #endregion Misc
    }
}
