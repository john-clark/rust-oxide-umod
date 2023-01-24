using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Libraries;

namespace Oxide.Plugins
{
    [Info("Gather Rewards", "CanopySheep", "1.5.3", ResourceId = 770)]
    [Description("Earn rewards through Economics/Server Rewards for killing and gathering")]
    public class GatherRewards : RustPlugin
    {
        #region Helpers

        [PluginReference]
        private Plugin Economics, ServerRewards, Friends, Clans;
        private string amountstring;
        private float amount;
        private bool foundAnimal = false;
        private string resource = null;

        private static string UppercaseFirst(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;
            var a = s.ToCharArray();
            a[0] = char.ToUpper(a[0]);
            return new string(a);
        }

        private bool CheckPermission(BasePlayer player, string perm)
        {
            if (permission.UserHasPermission(player.UserIDString, perm)) return true;
            return false;
        }

        #endregion

        #region Configuration Defaults

        PluginConfig DefaultConfig()
        {
            var defaultConfig = new PluginConfig
            {
                Settings = new PluginSettings
                {
                    ChatEditCommand = "gatherrewards",
                    ConsoleEditCommand = "gatherrewards",
                    EditPermission = "gatherrewards.canedit",
                    ShowMessagesOnKill = true,
                    ShowMessagesOnGather = true,
                    UseEconomics = true,
                    UseServerRewards = false,
                    PluginPrefix = "<color=cyan>[GatherRewards]</color>",
                    Rewards = new Dictionary<string, float>
                    {
                        { PluginRewards.Player, 0 },
                        { PluginRewards.PlayerFriend, -25 },
                        { PluginRewards.ClanMember, -25 },
                        { PluginRewards.Ore, 25 },
                        { PluginRewards.Wood, 25 },
                        { PluginRewards.Stone, 25},
                        { PluginRewards.Corn, 25},
                        { PluginRewards.Hemp, 25},
                        { PluginRewards.Mushroom, 25},
                        { PluginRewards.Pumpkin, 25}
                    }
                }
            };
            foreach (GameManifest.PooledString str in GameManifest.Current.pooledStrings)
            {
                if (str.str.StartsWith("assets/rust.ai/agents"))
                {
                    if (str.str.Contains("-") || str.str.Contains("_")) { continue; }
					if (str.str.Contains("bottest")) { continue; }
                    var animal = str.str.Substring(str.str.LastIndexOf("/") + 1).Replace(".prefab", "");
                    if (animal.Contains(".")) { continue; }
                    defaultConfig.Settings.Rewards[UppercaseFirst(animal)] = 25;
                }
				else if (str.str.StartsWith("assets/prefabs/npc"))
				{
					if (!(str.str.Contains("murderer.prefab") || str.str.Contains("scientist.prefab"))) { continue; }
					var animal = str.str.Substring(str.str.LastIndexOf("/") + 1).Replace(".prefab", "");
                    if (animal.Contains(".")) { continue; }
                    defaultConfig.Settings.Rewards[UppercaseFirst(animal)] = 25;
				}
            }
            return defaultConfig;
        }

        #endregion

        #region Configuration Setup

        private bool configChanged;
        private PluginConfig config;

        private static class PluginRewards
        {
            public const string Player = "Player";
            public const string PlayerFriend = "Player's Friend";
            public const string ClanMember = "Clan Member";
            public const string Ore = "Ore";
            public const string Wood = "Wood";
            public const string Stone = "Stone";
            public const string Corn = "Corn";
            public const string Hemp = "Hemp";
            public const string Mushroom = "Mushroom";
            public const string Pumpkin = "Pumpkin";
        }

        private class PluginSettings
        {
            public string ChatEditCommand { get; set; }
            public string ConsoleEditCommand { get; set; }
            public string EditPermission { get; set; }
            public bool ShowMessagesOnKill { get; set; }
            public bool ShowMessagesOnGather { get; set; }
            public bool UseEconomics { get; set; }
            public bool UseServerRewards { get; set; }
            public string PluginPrefix { get; set; }
            public Dictionary<string, float> Rewards { get; set; }
        }

        private class PluginConfig
        {
            public PluginSettings Settings { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(DefaultConfig(), true);
            PrintWarning("New configuration file created.");
        }

        private void LoadConfigValues()
        {
            config = Config.ReadObject<PluginConfig>();
            var defaultConfig = DefaultConfig();
            Merge(config.Settings.Rewards, defaultConfig.Settings.Rewards);

            if (!configChanged) return;
            PrintWarning("Configuration file updated.");
            Config.WriteObject(config);
        }

        private void Merge<T1, T2>(IDictionary<T1, T2> current, IDictionary<T1, T2> defaultDict)
        {
            foreach (var pair in defaultDict)
            {
                if (current.ContainsKey(pair.Key)) continue;
                current[pair.Key] = pair.Value;
                configChanged = true;
            }
            var oldPairs = current.Keys.Except(defaultDict.Keys).ToList();
            foreach (var oldPair in oldPairs)
            {
                current.Remove(oldPair);
                configChanged = true;
            }
        }

        #endregion

        #region Hooks

        private string Lang(string key, object userID = null) => lang.GetMessage(key, this, userID == null ? null : userID.ToString());

        private void Language()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "ReceivedForGather", "You have received ${0} for gathering {1}." },
                { "LostForGather", "You have lost ${0} for gathering {1}." },
                { "ReceivedForKill", "You have received ${0} for killing a {1}." },
                { "LostForKill", "You have lost ${0} for killing a {1}." },
                { "NoPermission", "You have no permission to use this command." },
                { "Usage", "Usage: /{0} [value] [amount]" },
                { "NotaNumber", "Error: value is not a number." },
                { "Success", "Successfully changed '{0}' to earn amount '{1}'." },
                { "ValueDoesNotExist", "Value '{0}' does not exist." }
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "ReceivedForGather", "Вы получили ${0} за сбор {1}." },
                { "LostForGather", "Вы потеряли ${0} за сбор {1}." },
                { "ReceivedForKill", "Вы получили ${0} за убийство {1}." },
                { "LostForKill", "Вы потеряли $ {0} за убийство {1}." },
                { "NoPermission", "У вас нет прав использовать эту команду." },
                { "Usage", "Использование: / {0} [значение] [количество]" } ,
                { "NotaNumber", "Ошибка: значение не является числом." },
                { "Success", "Успешно изменено '{0}', чтобы заработать деньги '{1}'." },
                { "ValueDoesNotExist", "Значение '{0}' не существует." }
            }, this, "ru");
        }

        private void Init()
        {
            LoadConfigValues();
            Language();
            RegisterPermsAndCommands();
        }

        private void RegisterPermsAndCommands()
        {
            permission.RegisterPermission(config.Settings.EditPermission, this);

            var command = Interface.Oxide.GetLibrary<Command>();
            command.AddChatCommand(config.Settings.ChatEditCommand, this, "cmdGatherRewards");
            command.AddConsoleCommand(config.Settings.ConsoleEditCommand, this, "cmdConsoleGatherRewards");
        }

        private void GiveCredit(BasePlayer player, string type, float amount, string gathered)
        {
            if (amount > 0)
            {
                if (config.Settings.UseEconomics && Economics) { Economics.Call("Deposit", player.UserIDString, (double)amount); }
                if (config.Settings.UseServerRewards && ServerRewards) { ServerRewards.Call("AddPoints", new object[] { player.userID, (int)amount }); }
                if (type == "gather" && config.Settings.ShowMessagesOnGather) { PrintToChat(player, config.Settings.PluginPrefix + " " + string.Format(Lang("ReceivedForGather", player), amount, gathered.ToLower())); }
                else if (type == "kill" && config.Settings.ShowMessagesOnKill) { PrintToChat(player, config.Settings.PluginPrefix + " " + string.Format(Lang("ReceivedForKill", player), amount, gathered.ToLower())); }
            }
            else
            {
                amountstring = amount.ToString().Replace("-", "");
                amount = float.Parse(amountstring);

                if (config.Settings.UseEconomics && Economics) { Economics.Call("Withdraw", player.UserIDString, (double)amount); }
                if (config.Settings.UseServerRewards && ServerRewards) { ServerRewards.Call("TakePoints", new object[] { player.userID, (int)amount }); }

                if (type == "gather" && config.Settings.ShowMessagesOnGather) { PrintToChat(player, config.Settings.PluginPrefix + " " + string.Format(Lang("LostForGather", player), amount, gathered.ToLower())); }
                else if (type == "kill" && config.Settings.ShowMessagesOnKill) { PrintToChat(player, config.Settings.PluginPrefix + " " + string.Format(Lang("LostForKill", player), amount, gathered.ToLower())); }
            }
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (!Economics && !ServerRewards) return;
            var player = entity.ToPlayer();

            if (player)
            {
                amount = 0;
                var shortName = item.info.shortname;
                resource = null;

                if (shortName.Contains(".ore"))
                {
                    amount = config.Settings.Rewards[PluginRewards.Ore];
                    resource = "ore";
                }

                if (shortName.Contains("stones"))
                {
                    amount = config.Settings.Rewards[PluginRewards.Stone];
                    resource = "stone";
                }

                if (dispenser.GetComponentInParent<TreeEntity>() && shortName.Contains("wood"))
                {
                    amount = config.Settings.Rewards[PluginRewards.Wood];
                    resource = "wood";
                }

                if (resource != null && amount != 0)
                {
                    GiveCredit(player, "gather", amount, resource);
                }
            }
        }

		private void OnCollectiblePickup(Item item, BasePlayer player)
		{
			if (!Economics && !ServerRewards) return;
			if (player == null) return;
			resource = null;
            amount = 0;

			if (item.ToString().Contains("stones"))
			{
				amount = config.Settings.Rewards[PluginRewards.Stone];
                resource = "stone";
			}

			if (item.ToString().Contains(".ore"))
			{
				amount = config.Settings.Rewards[PluginRewards.Ore];
                resource = "ore";
			}

			if (item.ToString().Contains("wood"))
			{
				amount = config.Settings.Rewards[PluginRewards.Wood];
                resource = "wood";
			}

            if (item.ToString().Contains("mushroom"))
            {
                amount = config.Settings.Rewards[PluginRewards.Mushroom];
                resource = "mushrooms";
            }

            if (item.ToString().Contains("seed.corn"))
            {
                amount = config.Settings.Rewards[PluginRewards.Corn];
                resource = "corn";
            }

            if (item.ToString().Contains("seed.hemp"))
            {
                amount = config.Settings.Rewards[PluginRewards.Hemp];
                resource = "hemp";
            }

            if (item.ToString().Contains("seed.pumpkin"))
            {
                amount = config.Settings.Rewards[PluginRewards.Pumpkin];
                resource = "pumpkins";
            }

            if (resource != null && amount != 0)
			{
                GiveCredit(player, "gather", amount, resource);
            }
		}

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null) return;
            if (!Economics && !ServerRewards) return;
            if (!info?.Initiator?.ToPlayer()) return;

            var player = info.Initiator.ToPlayer();
            var animal = UppercaseFirst(entity.LookupPrefab().ToString().Replace("[0]", ""));
            amount = 0;

            if (entity.ToPlayer() != null && !(entity is NPCPlayer))
            {
                var victim = entity.ToPlayer();
                if (player == victim) { return; }
                amount = config.Settings.Rewards[PluginRewards.Player];
                animal = "player";
                if (Friends && config.Settings.Rewards[PluginRewards.PlayerFriend] != 0)
                {
                    bool isFriend = Friends.Call<bool>("HasFriend", victim.userID, player.userID);
                    bool isFriendReverse = Friends.Call<bool>("HasFriend", player.userID, victim.userID);
                    if (isFriend && isFriendReverse)
                    {
                        amount = config.Settings.Rewards[PluginRewards.PlayerFriend];
                        animal = "friend";
                    }
                }
                if (Clans && config.Settings.Rewards[PluginRewards.ClanMember] != 0)
                {
                    string victimclan = Clans.Call<string>("GetClanOf", victim.userID);
                    string playerclan = Clans.Call<string>("GetClanOf", player.userID);
                    if (victimclan == playerclan)
                    {
                        amount = config.Settings.Rewards[PluginRewards.ClanMember];
                        animal = "clan member";
                    }
                }
            }
            else
            {
				if (entity is NPCMurderer) { animal = "Murderer"; config.Settings.Rewards.TryGetValue(animal, out amount); }
				else if (entity is NPCPlayerApex) { animal = "Scientist"; config.Settings.Rewards.TryGetValue(animal, out amount); }
                else if (entity.GetComponent("BaseNpc"))
				{
					animal = UppercaseFirst(entity.LookupPrefab().ToString().Replace("[0]", ""));
					config.Settings.Rewards.TryGetValue(animal, out amount);
				}
            }

            if (amount != 0)
            {
                GiveCredit(player, "kill", amount, animal);
            }
        }

        #endregion

        #region Commands

        private void cmdGatherRewards(BasePlayer player, string command, string[] args)
        {
            if (!(CheckPermission(player, config.Settings.EditPermission))) { SendReply(player, config.Settings.PluginPrefix + " " + Lang("NoPermission", player.UserIDString)); return; }
            if (args.Length < 2) { SendReply(player, config.Settings.PluginPrefix + " " + string.Format(Lang("Usage", player.UserIDString), config.Settings.ChatEditCommand)); return; }

            float value = 0;
            if (float.TryParse(args[1], out value) == false) { SendReply(player, config.Settings.PluginPrefix + " " + Lang("NotaNumber", player.UserIDString)); return; }

            switch (args[0].ToLower())
            {
                case "clan":
                {
                    config.Settings.Rewards[PluginRewards.ClanMember] = value;
                    Config.WriteObject(config);
                    SendReply(player, config.Settings.PluginPrefix + " " + string.Format(Lang("Success", player.UserIDString), "clan member", value));
                    break;
                }
                case "friend":
                {
                    config.Settings.Rewards[PluginRewards.PlayerFriend] = value;
                    Config.WriteObject(config);
                    SendReply(player, config.Settings.PluginPrefix + " " + string.Format(Lang("Success", player.UserIDString), "friend", value));
                    break;
                }
                default:
                {
                    bool found = false;
                    foreach (KeyValuePair<string, float> entry in config.Settings.Rewards)
                    {
                        if (found) { continue; }
                        if (!(entry.Key == UppercaseFirst(args[0].ToLower()))) { continue; }
                        found = true;
                    }
                    if (found)
                    {
                        config.Settings.Rewards[UppercaseFirst(args[0].ToLower())] = float.Parse(args[1]);
                        Config.WriteObject(config);
                        SendReply(player, config.Settings.PluginPrefix + " " + string.Format(Lang("Success", player.UserIDString), args[0].ToLower(), value));
                    }
                    else { SendReply(player, config.Settings.PluginPrefix + " " + string.Format(Lang("ValueDoesNotExist", player.UserIDString), args[0].ToLower())); }
                    break;
                }
                break;
            }
        }

        private void cmdConsoleGatherRewards(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin != true) { return; }

            if (arg.Args == null) { Puts(string.Format(Lang("Usage"), config.Settings.ConsoleEditCommand)); return; }
            if (arg.Args.Length <= 1) { Puts(string.Format(Lang("Usage"), config.Settings.ConsoleEditCommand)); return; }

            float value = 0;
            if (float.TryParse(arg.Args[1], out value) == false) { Puts(Lang("NotaNumber")); return; }

            switch (arg.Args[0].ToLower())
            {
                case "clan":
                {
                    config.Settings.Rewards[PluginRewards.ClanMember] = value;
                    Config.WriteObject(config);
                    Puts(string.Format(Lang("Success"), "clan member", value));
                    break;
                }
                case "friend":
                {
                    config.Settings.Rewards[PluginRewards.PlayerFriend] = value;
                    Config.WriteObject(config);
                    Puts(string.Format(Lang("Success"), "friend", value));
                    break;
                }
                default:
                {
                    bool found = false;
                    foreach (KeyValuePair<string, float> entry in config.Settings.Rewards)
                    {
                        if (found) { continue; }
                        if (!(entry.Key == UppercaseFirst(arg.Args[0].ToLower()))) { continue; }
                        found = true;
                    }
                    if (found)
                    {
                        config.Settings.Rewards[UppercaseFirst(arg.Args[0].ToLower())] = float.Parse(arg.Args[1]);
                        Config.WriteObject(config);
                        Puts(string.Format(Lang("Success"), arg.Args[0].ToLower(), value));
                    }
                    else { Puts(string.Format(Lang("ValueDoesNotExist"), arg.Args[0].ToLower())); }
                    break;
                }
                break;
            }
        }
        #endregion
    }
}