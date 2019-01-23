using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// TODO: Add SQLite and MySQL database support?

namespace Oxide.Plugins
{
    [Info("Economics", "Wulf/lukespragg", "3.5.0")]
    [Description("Basic economics system and economy API")]
    public class Economics : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Allow negative balance for accounts (true/false)")]
            public bool NegativeBalance { get; set; } = false;

            [JsonProperty(PropertyName = "Maximum balance for accounts (0 to disable)")]
            public int MaximumBalance { get; set; } = 0;

            [JsonProperty(PropertyName = "Remove unused accounts (true/false)")]
            public bool RemoveUnused { get; set; } = true;

            [JsonProperty(PropertyName = "Starting money amount (0 or higher)")]
            public int StartAmount { get; set; } = 1000;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            string configPath = $"{Interface.Oxide.ConfigDirectory}{Path.DirectorySeparatorChar}{Name}.json";
            LogWarning($"Could not load a valid configuration file, creating a new configuration file at {configPath}");
            config = new Configuration();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion Configuration

        #region Stored Data

        private DynamicConfigFile data;
        private StoredData storedData;
        private bool changed;

        private class StoredData
        {
            public readonly Dictionary<string, double> Balances = new Dictionary<string, double>();
        }

        private void SaveData()
        {
            if (changed)
            {
                Puts("Saving balances for players...");
                Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
            }
        }

        private void OnServerSave() => SaveData();

        private void Unload() => SaveData();

        #endregion Stored Data

        #region Localization

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandBalance"] = "balance",
                ["CommandDeposit"] = "deposit",
                ["CommandSetMoney"] = "setmoney",
                ["CommandTransfer"] = "transfer",
                ["CommandWithdraw"] = "withdraw",
                ["CommandWipe"] = "ecowipe",
                ["DataSaved"] = "Economics data saved!",
                ["DataWiped"] = "Economics data wiped!",
                ["NegativeBalance"] = "Balance can not be negative!",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["NoPlayersFound"] = "No players found with name or ID '{0}'",
                ["PlayerBalance"] = "Balance for {0}: {1:C}",
                ["PlayerLacksMoney"] = "'{0}' does not have enough money!",
                ["PlayersFound"] = "Multiple players were found, please specify: {0}",
                ["ReceivedFrom"] = "You have received {0} from {1}",
                ["TransactionFailed"] = "Transaction failed! Make sure amount is above 0",
                ["TransferredTo"] = "{0} transferred to {1}",
                ["TransferToSelf"] = "You can not transfer money yourself!",
                ["UsageBalance"] = "{0} - check your balance",
                ["UsageBalanceOthers"] = "{0} <player name or id> - check balance of a player",
                ["UsageDeposit"] = "{0} <player name or id> <amount> - deposit amount to player",
                ["UsageSetMoney"] = "Usage: {0} <player name or id> <amount> - set money for player",
                ["UsageTransfer"] = "Usage: {0} <player name or id> <amount> - transfer money to player",
                ["UsageWithdraw"] = "Usage: {0} <player name or id> <amount> - withdraw money from player",
                ["UsageWipe"] = "Usage: {0} - wipe all economics data",
                ["YouLackMoney"] = "You do not have enough money!",
                ["YouLostMoney"] = "You lost: {0:C}",
                ["YouReceivedMoney"] = "You received: {0:C}",
                ["YourBalance"] = "Your balance is: {0:C}"
            }, this);
        }

        #endregion Localization

        #region Initialization

        // New permissions
        private const string permBalance = "economics.balance";
        private const string permDeposit = "economics.deposit";
        private const string permSetMoney = "economics.setmoney";
        private const string permTransfer = "economics.transfer";
        private const string permWithdraw = "economics.withdraw";
        private const string permWipe = "economics.wipe";

        // Deprecated permissions
        private const string permAdmin = "economics.admin";

        private void Init()
        {
            // New permissions
            permission.RegisterPermission(permBalance, this);
            permission.RegisterPermission(permDeposit, this);
            permission.RegisterPermission(permSetMoney, this);
            permission.RegisterPermission(permTransfer, this);
            permission.RegisterPermission(permWithdraw, this);
            permission.RegisterPermission(permWipe, this);

            // Deprecated permissions
            permission.RegisterPermission(permAdmin, this);

            AddLocalizedCommand("CommandBalance", "BalanceCommand");
            AddLocalizedCommand("CommandDeposit", "DepositCommand");
            AddLocalizedCommand("CommandSetMoney", "SetMoneyCommand");
            AddLocalizedCommand("CommandTransfer", "TransferCommand");
            AddLocalizedCommand("CommandWithdraw", "WithdrawCommand");
            AddLocalizedCommand("CommandWipe", "WipeCommand");

            data = Interface.Oxide.DataFileSystem.GetFile(Name);
            try
            {
                Dictionary<ulong, double> temp = data.ReadObject<Dictionary<ulong, double>>();
                try
                {
                    storedData = new StoredData();
                    foreach (KeyValuePair<ulong, double> old in temp.ToArray())
                    {
                        if (!storedData.Balances.ContainsKey(old.Key.ToString()))
                        {
                            storedData.Balances.Add(old.Key.ToString(), old.Value);
                        }
                    }
                    changed = true;
                }
                catch
                {
                    // Ignored
                }
            }
            catch
            {
                storedData = data.ReadObject<StoredData>();
                changed = true;
            }

            string[] playerData = storedData.Balances.Keys.ToArray();

            if (config.MaximumBalance > 0)
            {
                foreach (string p in playerData.Where(p => storedData.Balances[p] > config.MaximumBalance))
                {
                    storedData.Balances[p] = config.MaximumBalance;
                    changed = true;
                }
            }

            if (config.RemoveUnused)
            {
                foreach (string p in playerData.Where(p => storedData.Balances[p].Equals(config.StartAmount)))
                {
                    storedData.Balances.Remove(p);
                    changed = true;
                }
            }

            SaveData();
        }

        #endregion Initialization

        #region API Methods

        private double Balance(string playerId)
        {
            double playerData;
            return storedData.Balances.TryGetValue(playerId, out playerData) ? playerData : config.StartAmount;
        }

        private double Balance(ulong playerId) => Balance(playerId.ToString());

        private bool Deposit(string playerId, double amount)
        {
            return amount > 0 && SetBalance(playerId, amount + Balance(playerId));
        }

        private bool Deposit(ulong playerId, double amount) => Deposit(playerId.ToString(), amount);

        private bool SetBalance(string playerId, double amount)
        {
            if (amount >= 0 || config.NegativeBalance)
            {
                amount = Math.Round(amount, 2);

                storedData.Balances[playerId] = amount;
                changed = true;

                Interface.Call("OnBalanceChanged", playerId, amount);
                Interface.Call("OnBalanceChanged", Convert.ToUInt64(playerId), amount);

                return true;
            }

            return false;
        }

        private bool SetBalance(ulong playerId, double amount) => SetBalance(playerId.ToString(), amount);

        [Obsolete("SetMoney is deprecated, use SetBalance instead")]
        private bool SetMoney(string playerId, double amount) => SetBalance(playerId, amount);

        private bool SetMoney(ulong playerId, double amount) => SetBalance(playerId.ToString(), amount);

        private bool Transfer(string playerId, string targetId, double amount)
        {
            return Withdraw(playerId, amount) && Deposit(targetId, amount);
        }

        private bool Transfer(ulong playerId, ulong targetId, double amount)
        {
            return Transfer(playerId.ToString(), targetId.ToString(), amount);
        }

        private bool Withdraw(string playerId, double amount)
        {
            if (amount >= 0 || config.NegativeBalance)
            {
                double balance = Balance(playerId);
                return (balance >= amount || config.NegativeBalance) && SetBalance(playerId, balance - amount);
            }

            return false;
        }

        private bool Withdraw(ulong playerId, double amount) => Withdraw(playerId.ToString(), amount);

        #endregion API Methods

        #region Commands

        #region Balance Command

        private void BalanceCommand(IPlayer player, string command, string[] args)
        {
            if (args != null && args.Length > 0)
            {
                if (!player.HasPermission(permAdmin) || !player.HasPermission(permBalance))
                {
                    Message(player, "NotAllowed", command);
                    return;
                }

                IPlayer target = FindPlayer(args[0], player);
                if (target == null)
                {
                    return;
                }

                Message(player, "PlayerBalance", target.Name, Balance(target.Id));
                return;
            }

            if (player.IsServer)
            {
                Message(player, "UsageBalanceOthers", command);
            }
            else
            {
                Message(player, "YourBalance", Balance(player.Id));
            }
        }

        #endregion Balance Command

        #region Deposit Command

        private void DepositCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAdmin) && !player.HasPermission(permDeposit))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args == null || args.Length <= 1)
            {
                Message(player, "UsageDeposit", command);
                return;
            }

            IPlayer target = FindPlayer(args[0], player);
            if (target == null)
            {
                return;
            }

            double amount;
            double.TryParse(args[1], out amount);
            if (amount <= 0)
            {
                Message(player, "NegativeBalance");
                return;
            }

            if (Deposit(target.Id, amount))
            {
                Message(player, "PlayerBalance", target.Name, Balance(target.Id));
            }
            else
            {
                Message(player, "TransactionFailed", target.Name);
            }
        }

        #endregion Deposit Command

        #region Set Money Command

        private void SetMoneyCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAdmin) || !player.HasPermission(permSetMoney))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args == null || args.Length <= 1)
            {
                Message(player, "UsageSetMoney", command);
                return;
            }

            IPlayer target = FindPlayer(args[0], player);
            if (target == null)
            {
                return;
            }

            double amount;
            double.TryParse(args[1], out amount);
            if (amount < 0)
            {
                Message(player, "NegativeBalance");
                return;
            }

            if (SetMoney(target.Id, amount))
            {
                Message(player, "PlayerBalance", target.Name, Balance(target.Id));
            }
            else
            {
                Message(player, "TransactionFailed", target.Name);
            }
        }

        #endregion Set Money Command

        #region Transfer Command

        private void TransferCommand(IPlayer player, string command, string[] args)
        {
            if (args == null || args.Length <= 1)
            {
                Message(player, "UsageTransfer", command);
                return;
            }

            IPlayer target = FindPlayer(args[0], player);
            if (target == null)
            {
                return;
            }

            double amount;
            double.TryParse(args[1], out amount);
            if (amount <= 0)
            {
                Message(player, "NegativeBalance");
                return;
            }

            if (target.Equals(player))
            {
                Message(player, "TransferToSelf");
                return;
            }

            if (!Withdraw(player.Id, amount))
            {
                Message(player, "YouLackMoney");
                return;
            }

            if (Deposit(target.Id, amount))
            {
                Message(player, "TransferredTo", amount, target.Name);
                Message(target, "ReceivedFrom", amount, player.Name);
            }
            else
            {
                Message(player, "TransactionFailed", target.Name);
            }
        }

        #endregion Transfer Command

        #region Withdraw Command

        private void WithdrawCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAdmin) || !player.HasPermission(permWithdraw))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args == null || args.Length <= 1)
            {
                Message(player, "UsageWithdraw", command);
                return;
            }

            IPlayer target = FindPlayer(args[0], player);
            if (target == null)
            {
                return;
            }

            double amount;
            double.TryParse(args[1], out amount);
            if (amount <= 0)
            {
                Message(player, "NegativeBalance");
                return;
            }

            if (Withdraw(target.Id, amount))
            {
                Message(player, "PlayerBalance", target.Name, Balance(target.Id));
            }
            else
            {
                Message(player, "YouLackMoney", target.Name);
            }
        }

        #endregion Withdraw Command

        #region Wipe Command

        private void WipeCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAdmin) || !player.HasPermission(permWipe))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            storedData = new StoredData();
            changed = true;
            SaveData();

            Message(player, "DataWiped");
        }

        #endregion Wipe Command

        #endregion Commands

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
            if (target == null)
            {
                Message(player, "NoPlayersFound", nameOrId);
                return null;
            }

            return target;
        }

        private string Lang(string key, string playerId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, playerId), args);
        }

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

        private void Message(IPlayer player, string key, params object[] args)
        {
            player.Reply(Lang(key, player.Id, args));
        }

        #endregion Helpers
    }
}
