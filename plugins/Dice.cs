﻿using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Dice", "Wulf/lukespragg", "0.3.0", ResourceId = 655)]
    [Description("Feeling lucky? Roll dice to get a random number")]
    public class Dice : CovalencePlugin
    {
        #region Initialization

        private static System.Random random = new System.Random();

        private const string permUse = "dice.use";

        private void Init()
        {
            AddCommandAliases("CommandAlias", "DiceCommand");
            AddCovalenceCommand("dice", "DiceCommand");
            permission.RegisterPermission(permUse, this);
        }

        #endregion

        #region Localization

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandAliasDice"] = "roll",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["PlayerRolled"] = "{0} rolled {1}",
                ["UsageDice"] = "Usage: '{0} #' to roll dice (# being optional number of dice)",
            }, this);
        }

        #endregion

        #region Dice Command

        private void DiceCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUse))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            var arg = args.Length > 0 ? args[0] : "1";

            int dice;
            if (int.TryParse(arg, out dice))
            {
                if (dice >= 1000) dice = 1;
                var roll = random.Next(1, dice * 7);
                Broadcast("PlayerRolled", player.Name, roll);
            }
            else if (arg == "help")
                Message(player, "UsageDice", command);
        }

        #endregion

        #region Helpers

        private void AddCommandAliases(string key, string command)
        {
            foreach (var language in lang.GetLanguages(this))
            {
                var messages = lang.GetMessages(language, this);
                foreach (var message in messages.Where(m => m.Key.StartsWith(key))) AddCovalenceCommand(message.Value, command);
            }
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void Broadcast(string key, params object[] args)
        {
            foreach (var player in players.Connected.Where(p => p.IsConnected)) player.Message(Lang(key, player.Id, args));
        }

        private void Message(IPlayer player, string key, params object[] args) => player.Message(Lang(key, player.Id, args));

        #endregion
    }
}