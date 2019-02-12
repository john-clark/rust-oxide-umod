// Requires: Economics
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Coin Flip", "Gachl", "1.0.7")]
    [Description("Try your luck by challenging players to a coin flip and betting on it using Economics")]
    class CoinFlip : CovalencePlugin
    {
        #region Internal structures
        /// <summary>
        /// Store information about a coin flip
        /// </summary>
        private struct CoinFlipParams
        {
            /// <summary>
            /// Amount of the bet
            /// </summary>
            internal int Amount;

            /// <summary>
            /// If set, the coin flip is private, and the target is the opponent that was asked to join. Otherwise null.
            /// </summary>
            internal IPlayer Target;

            /// <summary>
            /// The timer that will cancel the coin flip after the configured timeout
            /// </summary>
            internal Timer Timer;
        }

        /// <summary>
        /// Message enumeration for localisation
        /// </summary>
        private enum Messages
        {
            FLIP_USAGE,
            DUPLICATE_PLAYERS,
            AMBIGUOUS_PLAYERS,
            NO_PLAYERS,
            ALREADY_FLIPPING,
            COINFLIP_INITIATED,
            PRIVATE_COINFLIP_INITIATED,
            AMOUNT_TOO_LOW,
            BALANCE_TOO_LOW,
            COINFLIP_READY,
            PRIVATE_COINFLIP_READY,
            COINFLIP_CANCELED,
            PRIVATE_COINFLIP_CANCELED,
            NO_FLIPS,
            JOIN_USAGE,
            NOT_FLIPPING,
            FLIP_JOINED,
            FLIP_JOIN,
            COUNT_THREE,
            COUNT_TWO,
            COUNT_ONE,
            FLIP,
            COIN_LAND,
            WIN,
            LOSS,
            LEFT,
            YOURSELF,
            PRIVATE,
            WIN_ALL,
            JOIN_ALL,
            CANT_PARTICIPATE
        }
        #endregion

        #region Static and global data
        // Random Number Generator
        private static Random RNG = new Random();

        // Configuration keys
        private const string CONFIG_MINIMUM_AMOUNT = "MinimumAmount";
        private const string CONFIG_TIMEOUT = "Timeout";
        private const string CONFIG_ANNOUNCE_EVERYBODY = "AnnounceEverybody";
        private const string CONFIG_PREFIX = "Prefix";

        // Permissions
        private const string PERMISSION_CREATE = "coinflip.create";
        private const string PERMISSION_JOIN = "coinflip.join";
        #endregion

        #region Data
        // Configuration values
        private int MinimumAmount => (int)Config[CoinFlip.CONFIG_MINIMUM_AMOUNT];
        private int Timeout => (int)Config[CoinFlip.CONFIG_TIMEOUT];
        private bool AnnounceEverybody => (bool)Config[CoinFlip.CONFIG_ANNOUNCE_EVERYBODY];
        private string Prefix => (string)Config[CoinFlip.CONFIG_PREFIX];

        // Economics reference
        [PluginReference]
        private Plugin Economics;

        // All active and unjoined coin flips
        private Dictionary<string, CoinFlipParams> coinFlips = new Dictionary<string, CoinFlipParams>();
        #endregion

        #region Plugin control
        protected override void LoadDefaultMessages()
        {
            // Register all default messages

            // English
            this.lang.RegisterMessages(new Dictionary<string, string> {
                { Enum.GetName(typeof(Messages), Messages.FLIP_USAGE), $"Usage:{Environment.NewLine}/coinflip Amount{Environment.NewLine}/coinflip Amount PlayerNameOrId" },
                { Enum.GetName(typeof(Messages), Messages.DUPLICATE_PLAYERS), "The name {0} exactly matches {1} players, please use a steamid to identify the player." },
                { Enum.GetName(typeof(Messages), Messages.AMBIGUOUS_PLAYERS), $"The name {{0}} matches {{1}} players, please be more specific:{Environment.NewLine}{{2}}" },
                { Enum.GetName(typeof(Messages), Messages.NO_PLAYERS), "The name {0} matches no players." },
                { Enum.GetName(typeof(Messages), Messages.COINFLIP_INITIATED), "Player {0} has started a coin flip for {1}, type /joinflip {0} to join." },
                { Enum.GetName(typeof(Messages), Messages.PRIVATE_COINFLIP_INITIATED), "Player {0} requested a coin flip with you for {1}, type /joinflip {0} to join." },
                { Enum.GetName(typeof(Messages), Messages.AMOUNT_TOO_LOW), "The minimum amount for a coin flip is {0}." },
                { Enum.GetName(typeof(Messages), Messages.BALANCE_TOO_LOW), "Your balance of {0} is not sufficient for a coin flip for {1}" },
                { Enum.GetName(typeof(Messages), Messages.COINFLIP_READY), "You paid {0} for a coin flip. If nobody joins within {1} minute(s) it will automatically be canceled." },
                { Enum.GetName(typeof(Messages), Messages.PRIVATE_COINFLIP_READY), "You paid {0} for a coin flip with {2}. It will automatically be canceled in {1} minute(s)." },
                { Enum.GetName(typeof(Messages), Messages.COINFLIP_CANCELED), "Your coin flip was canceled and {0} has been refunded." },
                { Enum.GetName(typeof(Messages), Messages.PRIVATE_COINFLIP_CANCELED), "Your coin flip with {1} was canceled and {0} has been refunded." },
                { Enum.GetName(typeof(Messages), Messages.NO_FLIPS), "You have no active coin flips." },
                { Enum.GetName(typeof(Messages), Messages.ALREADY_FLIPPING), "You already have an active coin flip. Use /cancelflip to cancel it." },
                { Enum.GetName(typeof(Messages), Messages.JOIN_USAGE), $"Usage:{Environment.NewLine}/joinflip PlayerNameOrId" },
                { Enum.GetName(typeof(Messages), Messages.NOT_FLIPPING), "There is no coin flip from player {0}." },
                { Enum.GetName(typeof(Messages), Messages.FLIP_JOINED), "Player {0} has joined your coin flip. Prepare to flip!" },
                { Enum.GetName(typeof(Messages), Messages.FLIP_JOIN), "You paid {1} to join the coin flip of player {0}. Prepare to flip!" },
                { Enum.GetName(typeof(Messages), Messages.COUNT_THREE), "3..." },
                { Enum.GetName(typeof(Messages), Messages.COUNT_TWO), "2..." },
                { Enum.GetName(typeof(Messages), Messages.COUNT_ONE), "1..." },
                { Enum.GetName(typeof(Messages), Messages.FLIP), "The coin is in the air!" },
                { Enum.GetName(typeof(Messages), Messages.COIN_LAND), "The coin has landed on the side of {0}." },
                { Enum.GetName(typeof(Messages), Messages.WIN), "Congratulations! You have won {0}!" },
                { Enum.GetName(typeof(Messages), Messages.LOSS), "Too bad! You have lost {0}." },
                { Enum.GetName(typeof(Messages), Messages.LEFT), "Your opponent has forfeited, {0} has been refunded." },
                { Enum.GetName(typeof(Messages), Messages.YOURSELF), "You can not have a coin flip against yourself." },
                { Enum.GetName(typeof(Messages), Messages.PRIVATE), "You can not join this coin flip because it's private." },
                { Enum.GetName(typeof(Messages), Messages.WIN_ALL), "Player {0} has just won {2} in a coin flip against player {1}." },
                { Enum.GetName(typeof(Messages), Messages.JOIN_ALL), "Players {0} and {1} will flip a coin for {2}." },
                { Enum.GetName(typeof(Messages), Messages.CANT_PARTICIPATE), "Player {0} can not participate in coin flips." }
            }, this, "en");

            // German
            this.lang.RegisterMessages(new Dictionary<string, string> {
                { Enum.GetName(typeof(Messages), Messages.FLIP_USAGE), $"Verwendung:{Environment.NewLine}/coinflip Betrag{Environment.NewLine}/coinflip Betrag SpielerNameOderId" },
                { Enum.GetName(typeof(Messages), Messages.DUPLICATE_PLAYERS), "Der Name {0} trifft auf {1} Spieler exakt zu, bitte verwende die steamid um den Spieler zu identifizieren." },
                { Enum.GetName(typeof(Messages), Messages.AMBIGUOUS_PLAYERS), $"Der Name {{0}} trifft auf {{1}} Spieler zu, bitte gebe den Namen präziser an:{Environment.NewLine}{{2}}" },
                { Enum.GetName(typeof(Messages), Messages.NO_PLAYERS), "Der Name {0} trifft auf keinen Spieler zu." },
                { Enum.GetName(typeof(Messages), Messages.COINFLIP_INITIATED), "Spieler {0} hat einen Münzwurf für {1} begonnen, gebe /joinflip {0} ein um beizutreten." },
                { Enum.GetName(typeof(Messages), Messages.PRIVATE_COINFLIP_INITIATED), "Spieler {0} möchte mit dir einen Münzwurf für {1} machen, gebe /joinflip {0} ein um beizutreten." },
                { Enum.GetName(typeof(Messages), Messages.AMOUNT_TOO_LOW), "Der minimale Einsatz für einen Münzwurf beträgt {0}." },
                { Enum.GetName(typeof(Messages), Messages.BALANCE_TOO_LOW), "Dein Kontostand von {0} ist nicht ausreichend für einen Münzwurf für {1}" },
                { Enum.GetName(typeof(Messages), Messages.COINFLIP_READY), "Du hast {0} für einen Münzwurf bezahlt. Wenn niemand innerhalb von {1} Minute(n) beitritt wird er automatisch storniert." },
                { Enum.GetName(typeof(Messages), Messages.PRIVATE_COINFLIP_READY), "Du hast {0} für einen Münzwurf gegen {2} gezahlt. Er wird automatisch in {1} Minute(n) storniert." },
                { Enum.GetName(typeof(Messages), Messages.COINFLIP_CANCELED), "Dein Münzwurf wurde storniert und {0} zurückgezahlt." },
                { Enum.GetName(typeof(Messages), Messages.PRIVATE_COINFLIP_CANCELED), "Dein Münzwurf gegen {1} wurde storniert und {0} zurückgezahlt." },
                { Enum.GetName(typeof(Messages), Messages.NO_FLIPS), "Du hast keinen aktiven Münzwurf." },
                { Enum.GetName(typeof(Messages), Messages.ALREADY_FLIPPING), "Du hast bereits einen aktiven Münzwurf. Verwende /cancelflip um ihn zu stornieren." },
                { Enum.GetName(typeof(Messages), Messages.JOIN_USAGE), $"Verwendung:{Environment.NewLine}/joinflip SpielerNameOderId" },
                { Enum.GetName(typeof(Messages), Messages.NOT_FLIPPING), "Der Spieler {0} hat keinen Münzwurf." },
                { Enum.GetName(typeof(Messages), Messages.FLIP_JOINED), "Spieler {0} ist einem Münzwurf beigetreten. Bereit machen zum Wurf!" },
                { Enum.GetName(typeof(Messages), Messages.FLIP_JOIN), "Du hast {1} bezahlt um dem Münzwurf gegen Spieler {0} beizutreten. Bereit machen zum Wurf!" },
                { Enum.GetName(typeof(Messages), Messages.COUNT_THREE), "3..." },
                { Enum.GetName(typeof(Messages), Messages.COUNT_TWO), "2..." },
                { Enum.GetName(typeof(Messages), Messages.COUNT_ONE), "1..." },
                { Enum.GetName(typeof(Messages), Messages.FLIP), "Die Münze ist in der Luft!" },
                { Enum.GetName(typeof(Messages), Messages.COIN_LAND), "Die Münze ist auf der Seite von {0} gelandet." },
                { Enum.GetName(typeof(Messages), Messages.WIN), "Glückwunsch! Du hast {0} gewonnen!" },
                { Enum.GetName(typeof(Messages), Messages.LOSS), "Schade! Du hast {0} verloren." },
                { Enum.GetName(typeof(Messages), Messages.LEFT), "Dein Gegner hat aufgegeben, {0} wurde zurückgezahlt." },
                { Enum.GetName(typeof(Messages), Messages.YOURSELF), "Du kannst keinen Münzwurf gegen dich selbst spielen." },
                { Enum.GetName(typeof(Messages), Messages.PRIVATE), "Du kannst diesem Münzwurf nicht beitreten da er Privat ist." },
                { Enum.GetName(typeof(Messages), Messages.WIN_ALL), "Spieler {0} hat gerade {2} in einem Münzwurf gegen Spieler {1} gewonnen." },
                { Enum.GetName(typeof(Messages), Messages.JOIN_ALL), "Die Spieler {0} und {1} werfen eine Münze für {2}." },
                { Enum.GetName(typeof(Messages), Messages.CANT_PARTICIPATE), "Spieler {0} kann nicht an Münzwürfen teilnehmen." }
            }, this, "de");
        }

        protected override void LoadDefaultConfig()
        {
            // Setup default values for configuration
            Config[CoinFlip.CONFIG_MINIMUM_AMOUNT] = 0;
            Config[CoinFlip.CONFIG_TIMEOUT] = 5;
            Config[CoinFlip.CONFIG_ANNOUNCE_EVERYBODY] = true;
            Config[CoinFlip.CONFIG_PREFIX] = "[Coin Flip]";
        }

        private void Unload()
        {
            // Refund all active coin flips to prevent loss of balance when un- or reloading the plugin
            this.coinFlips.Keys.ToList().ForEach(k => this.cancelCoinFlip(k));
        }
        #endregion

        #region Tool methods
        /// <summary>
        /// Send a localised chat message to a player
        /// </summary>
        /// <param name="player">Target player to receive the message</param>
        /// <param name="message">The localised message to be sent</param>
        /// <param name="args">Additional information of this message</param>
        private void sendLocalisedMessage(IPlayer player, Messages message, params object[] args)
        {
            // Argument validation
            if (player == null)
                throw new ArgumentNullException("player");

            // Get the string representation of the messsage key
            string messageKey = Enum.GetName(typeof(Messages), message);
            
            // Send the localised message to the player
            player.Reply(this.lang.GetMessage(messageKey, this, player.Id), this.Prefix, args);
        }

        /// <summary>
        /// Send a localised chat message to a player if a condition is true
        /// </summary>
        /// <param name="player">Target player to receive the message</param>
        /// <param name="condition">The condition that has to be true if the message should be sent</param>
        /// <param name="message">The localised message to be sent</param>
        /// <param name="args">Additional information of this message</param>
        /// <returns>The value of the condition</returns>
        private bool sendConditionalLocalisedMessage(IPlayer player, bool condition, Messages message, params object[] args)
        {
            // Argument validation
            if (player == null)
                throw new ArgumentNullException("player");

            if (condition)
                this.sendLocalisedMessage(player, message, args);
            
            return condition;
        }

        /// <summary>
        /// Find a player by name or id. Use id match, then exact match, then case insensitive match, then partial match to find a player.
        /// </summary>
        /// <param name="requester">Player who receives messages if matching was unsuccessful</param>
        /// <param name="targetPlayerNameOrId">Search string to use for finding the player</param>
        /// <returns>The player if found, otherwise null</returns>
        private IPlayer findPlayer(IPlayer requester, string targetPlayerNameOrId)
        {
            // Argument validation
            if (requester == null)
                throw new ArgumentNullException("requester");

            // Refuse empty search strings
            if (string.IsNullOrEmpty(targetPlayerNameOrId))
                throw new ArgumentNullException("targetPlayerNameOrId");

            // Get active player list and store as array to dereference the unterlying IEnumerable
            // and prevent changes to occur while this method is running.
            IPlayer[] connectedPlayers = this.covalence.Players.Connected.ToArray();

            // Try an id match
            IPlayer[] matches = connectedPlayers.Where(p => p.Id == targetPlayerNameOrId).ToArray();
            if (matches.Length == 1)
                return matches[0];

            // Try an exact match
            matches = connectedPlayers.Where(p => p.Name == targetPlayerNameOrId).ToArray();
            if (matches.Length == 1)
                return matches[0];
            // This probably happens very rarely but if there are two players with *exactly* the same name force use of steam ID instead of name
            else if (this.sendConditionalLocalisedMessage(requester, matches.Length > 1, Messages.DUPLICATE_PLAYERS, targetPlayerNameOrId, matches.Length))
                return null;

            // Try a case insensitive match
            matches = connectedPlayers.Where(p => p.Name.ToLower() == targetPlayerNameOrId.ToLower()).ToArray();
            if (matches.Length == 1)
                return matches[0];
            else if (this.sendConditionalLocalisedMessage(requester, matches.Length > 1, Messages.AMBIGUOUS_PLAYERS, targetPlayerNameOrId, matches.Length, string.Join(Environment.NewLine, matches.Select(p => p.Name).ToArray())))
                return null;

            // Try a partial match
            matches = connectedPlayers.Where(p => p.Name.ToLower().Contains(targetPlayerNameOrId.ToLower())).ToArray();
            if (matches.Length == 1)
                return matches[0];
            else if (this.sendConditionalLocalisedMessage(requester, matches.Length > 1, Messages.AMBIGUOUS_PLAYERS, targetPlayerNameOrId, matches.Length, string.Join(Environment.NewLine, matches.Select(p => p.Name).ToArray())))
                return null;

            // No matches have returned results
            this.sendLocalisedMessage(requester, Messages.NO_PLAYERS, targetPlayerNameOrId);
            return null;
        }
        #endregion

        #region Commands
        [Command("coinflip"), Permission(CoinFlip.PERMISSION_CREATE)]
        private void coinFlipChatCommand(IPlayer player, string command, string[] args)
        {
            // Validate caller is a player that can interact with Economy
            if (player.IsServer)
            {
                player.Reply("This command can not be used from RCON.");
                return;
            }

            // Validate argument count
            if (this.sendConditionalLocalisedMessage(player, args.Length == 0, Messages.FLIP_USAGE))
                return;

            // Parse bet amount from argument
            int amount = 0;
            if (this.sendConditionalLocalisedMessage(player, !int.TryParse(args[0], out amount), Messages.FLIP_USAGE))
                return;

            // Limit bet amount to configured minimum value
            if (this.sendConditionalLocalisedMessage(player, amount < this.MinimumAmount || amount < 0, Messages.AMOUNT_TOO_LOW, this.MinimumAmount))
                return;

            // If a target player was supplied, try to find it
            string targetPlayerName = String.Join(" ", args.Skip(1).ToArray());
            IPlayer targetPlayer = null;
            if (!string.IsNullOrEmpty(targetPlayerName))
            {
                targetPlayer = this.findPlayer(player, targetPlayerName);

                if (targetPlayer == null)
                    return;
            }

            // Prevent targeting yourself as private coin flip opponent
            if (this.sendConditionalLocalisedMessage(player, targetPlayer != null && player.Id == targetPlayer.Id, Messages.YOURSELF))
                return;

            // Prevent targeting players who can not participate in coin flips
            if (this.sendConditionalLocalisedMessage(player, targetPlayer != null && !targetPlayer.HasPermission(CoinFlip.PERMISSION_JOIN), Messages.CANT_PARTICIPATE, targetPlayer?.Name))
                return;

            // Prevent player from starting multiple coin flips at once
            if (this.coinFlips.ContainsKey(player.Id))
            {
                this.sendLocalisedMessage(player, Messages.ALREADY_FLIPPING, this.coinFlips[player.Id].Amount);
                return;
            }

            // Check and withdraw balance for coin flip
            double availableBalance = this.Economics.Call<double>("Balance", player.Id);
            if (this.sendConditionalLocalisedMessage(player, !this.Economics.Call<bool>("Withdraw", player.Id, (double)amount), Messages.BALANCE_TOO_LOW, availableBalance, amount))
                return;

            // Create active coin flip
            this.coinFlips.Add(player.Id, new CoinFlipParams()
            {
                Amount = amount,
                Target = targetPlayer,

                // Start a timer that cancels the coin flip after the configured timeout
                Timer = timer.Once(60 * this.Timeout, () => this.cancelCoinFlip(player.Id))
            });

            // Send messages depending on whether a target player was supplied or not
            if (targetPlayer == null)
            {
                // Send messages to all players
                this.covalence.Players.Connected.ToArray().Where(p => p.Id != player.Id && p.HasPermission(CoinFlip.PERMISSION_JOIN)).ToList().ForEach(p => this.sendLocalisedMessage(p, Messages.COINFLIP_INITIATED, player.Name, amount));
                // Inform initiating player about the coin flip and withdrawal
                this.sendLocalisedMessage(player, Messages.COINFLIP_READY, amount, this.Timeout);
            }
            else
            {
                // Inform targeted opponent about coin flip 
                this.sendLocalisedMessage(targetPlayer, Messages.PRIVATE_COINFLIP_INITIATED, player.Name, amount);
                // Inform initiator about coin flip and withdrawal
                this.sendLocalisedMessage(player, Messages.PRIVATE_COINFLIP_READY, amount, this.Timeout, targetPlayer.Name);
            }
        }

        [Command("joinflip"), Permission(CoinFlip.PERMISSION_JOIN)]
        private void joinFlipChatCommand(IPlayer player, string command, string[] args)
        {
            // Validate caller is a player that can interact with Economy
            if (player.IsServer)
            {
                player.Reply("This command can not be used from RCON.");
                return;
            }

            // Validate argument count
            if (this.sendConditionalLocalisedMessage(player, args.Length == 0, Messages.JOIN_USAGE))
                return;

            // Try and find the target player
            IPlayer targetPlayer = this.findPlayer(player, String.Join(" ", args));
            if (targetPlayer == null)
                return;

            // Prevent joining your own coin flip
            if (this.sendConditionalLocalisedMessage(player, player.Id == targetPlayer.Id, Messages.YOURSELF))
                return;

            // Check if target player has an active coin flip
            if (this.sendConditionalLocalisedMessage(player, !this.coinFlips.ContainsKey(targetPlayer.Id), Messages.NOT_FLIPPING, targetPlayer.Name))
                return;

            // Retrieve active coin flip of target player
            CoinFlipParams coinFlipParams = this.coinFlips[targetPlayer.Id];
            if (this.sendConditionalLocalisedMessage(player, coinFlipParams.Target != null && coinFlipParams.Target.Id != player.Id, Messages.PRIVATE))
                return;

            // Check and withdraw balance for coin flip
            double availableBalance = this.Economics.Call<double>("Balance", player.Id);
            if (this.sendConditionalLocalisedMessage(player, !this.Economics.Call<bool>("Withdraw", player.Id, (double)coinFlipParams.Amount), Messages.BALANCE_TOO_LOW, availableBalance, coinFlipParams.Amount))
                return;

            // Remove coin flip from active coin flips to prevent any other players from joining
            this.coinFlips.Remove(targetPlayer.Id);
            coinFlipParams.Timer.Destroy();

            // Inform both players about the commencing coin flip
            this.sendLocalisedMessage(targetPlayer, Messages.FLIP_JOINED, player.Name);
            this.sendLocalisedMessage(player, Messages.FLIP_JOIN, targetPlayer.Name, coinFlipParams.Amount);

            // Announce the coin flip to everybody else
            if (this.AnnounceEverybody)
                this.covalence.Players.Connected.ToArray().Where(p => p.Id != player.Id && p.Id != targetPlayer.Id).ToList().ForEach(p => this.sendLocalisedMessage(p, Messages.JOIN_ALL, targetPlayer.Name, player.Name, coinFlipParams.Amount));

            // Start 3 second countdown (it's actually 5 seconds, but sssssh)
            timer.Once(1, () =>
            {
                this.sendLocalisedMessage(player, Messages.COUNT_THREE);
                this.sendLocalisedMessage(targetPlayer, Messages.COUNT_THREE);
                timer.Once(1, () =>
                {
                    this.sendLocalisedMessage(player, Messages.COUNT_TWO);
                    this.sendLocalisedMessage(targetPlayer, Messages.COUNT_TWO);

                    timer.Once(1, () =>
                    {
                        this.sendLocalisedMessage(player, Messages.COUNT_ONE);
                        this.sendLocalisedMessage(targetPlayer, Messages.COUNT_ONE);

                        timer.Once(1, () =>
                        {
                            this.sendLocalisedMessage(player, Messages.FLIP);
                            this.sendLocalisedMessage(targetPlayer, Messages.FLIP);

                            // Start actually flipping
                            timer.Once(1, () => this.flip(targetPlayer, player, coinFlipParams));
                        });
                    });
                });
            });
        }

        [Command("cancelflip"), Permission(CoinFlip.PERMISSION_CREATE)]
        private void cancelFlipChatCommand(IPlayer player, string command, string[] args)
        {
            // Validate caller is a player that can interact with Economy
            if (player.IsServer)
            {
                player.Reply("This command can not be used from RCON.");
                return;
            }

            // Check if player has an active coin flip
            if (this.sendConditionalLocalisedMessage(player, !this.coinFlips.ContainsKey(player.Id), Messages.NO_FLIPS))
                return;

            // Cancel the coin flip
            this.cancelCoinFlip(player.Id);
        }
        #endregion

        #region Coin flip control
        /// <summary>
        /// Flips a coin
        /// </summary>
        /// <param name="initiator">Player who initiated this coin flip</param>
        /// <param name="player">Player who has joined this coin flip</param>
        /// <param name="coinFlipParams">Parameters of this coin flip</param>
        private void flip(IPlayer initiator, IPlayer player, CoinFlipParams coinFlipParams)
        {
            // Argument validation
            if (initiator == null)
                throw new ArgumentNullException("initiator");

            if (player == null)
                throw new ArgumentNullException("player");

            // Check if both opponents are still connected
            if (!initiator.IsConnected || !player.IsConnected)
            {
                // Send abort message
                if (initiator.IsConnected)
                    this.sendLocalisedMessage(initiator, Messages.LEFT, coinFlipParams.Amount);
                if (player.IsConnected)
                    this.sendLocalisedMessage(player, Messages.LEFT, coinFlipParams.Amount);

                // Refund balance
                this.Economics.Call("Deposit", initiator.Id, (double)coinFlipParams.Amount);
                this.Economics.Call("Deposit", player.Id, (double)coinFlipParams.Amount);

                return;
            }

            // Pick a winner and loser by throwing a coin
            IPlayer winner, loser = null;
            if (CoinFlip.RNG.Next(0, 2) == 1)
            {
                winner = initiator;
                loser = player;
            }
            else
            {
                winner = player;
                loser = initiator;
            }

            // Inform both opponents about the result
            this.sendLocalisedMessage(initiator, Messages.COIN_LAND, winner.Name);
            this.sendLocalisedMessage(player, Messages.COIN_LAND, winner.Name);

            // Congratulate the winner, condole the loser
            this.sendLocalisedMessage(winner, Messages.WIN, coinFlipParams.Amount * 2);
            this.sendLocalisedMessage(loser, Messages.LOSS, coinFlipParams.Amount);

            // Deposit the pot to the winner
            this.Economics.Call("Deposit", winner.Id, (double)coinFlipParams.Amount * 2.0);

            // Announce result to everybody excluding the two opponents
            if (this.AnnounceEverybody)
                this.covalence.Players.Connected.ToArray().Where(p => p.Id != player.Id && p.Id != initiator.Id).ToList().ForEach(p => this.sendLocalisedMessage(p, Messages.WIN_ALL, winner.Name, loser.Name, coinFlipParams.Amount * 2));
        }

        /// <summary>
        /// Cancel an active coin flip
        /// </summary>
        /// <param name="userID">User ID of the coin flip that is to be canceled</param>
        private void cancelCoinFlip(string userID)
        {
            // Argument validation
            if (String.IsNullOrEmpty(userID))
                throw new ArgumentNullException("userID");

            // Check if player has an active coin flip
            if (!this.coinFlips.ContainsKey(userID))
                return;

            // Get the coin flip parameters
            CoinFlipParams coinFlipParams = this.coinFlips[userID];

            // Destroy the timer that cancels the coin flip after the timeout
            coinFlipParams.Timer.Destroy();

            // Remove the coin flip from the active coin flip list
            this.coinFlips.Remove(userID);
            
            // Refund the balance to the player
            this.Economics.Call("Deposit", userID, (double)coinFlipParams.Amount);

            // Get the player if he is still online
            IPlayer player = this.covalence.Players.FindPlayerById(userID);
            if (player == null || !player.IsConnected)
                return;

            // Inform the player about the cancellation
            if (coinFlipParams.Target == null)
                this.sendLocalisedMessage(player, Messages.COINFLIP_CANCELED, coinFlipParams.Amount);
            else
                this.sendLocalisedMessage(player, Messages.PRIVATE_COINFLIP_CANCELED, coinFlipParams.Amount, coinFlipParams.Target.Name);
        }
        #endregion
    }
}
