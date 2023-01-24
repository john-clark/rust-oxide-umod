using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CodeHatch.Damaging;
using CodeHatch.Engine.Networking;
using CodeHatch.Common;
using CodeHatch.Inventory.Blueprints;
using Oxide.Core;
using Oxide.Core.Plugins;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.ItemContainer;
using CodeHatch.UserInterface.Dialogues;
using CodeHatch.Engine.Events.Prefab;
using CodeHatch.Blocks.Networking.Events;
using CodeHatch.Engine.Core.Cache;
using CodeHatch.Engine.Modules.SocialSystem;
using CodeHatch.Thrones.AncientThrone;
using CodeHatch.Thrones.SocialSystem;

namespace Oxide.Plugins
{
    [Info("RaidBoss", "Scorpyon & D-Kay", "1.4.1", ResourceId = 1142)]
    public class RaidBoss : ReignOfKingsPlugin
    {
        #region Variables

        [PluginReference("GrandExchange")]
        private Plugin GrandExchange;

        private Dictionary<ulong, Boss> BossList { get; set; } = new Dictionary<ulong, Boss>();

        private float DefaultDamageMultiplier { get; set; } = 1.3f;
        private float DefaultDefenseMulitplier { get; set; } = 0.5f;

        private int DefaultRewardAmount { get; set; } = 10000;

        private bool BossGold { get; set; } = true;
        private bool GuildBossGold { get; set; } = false;

        private bool KingIsBoss { get; set; } = false;

        private class Boss
        {
            public ulong Id { get; private set; }
            public string Name { get; private set; }
            public float DamageMultiplier { get; private set; }
            public float DefenseMultiplier { get; private set; }
            public int GoldReward { get; private set; }

            public Boss(ulong id, string name, float damageMultiplier, float defenseMultiplier, int goldReward)
            {
                Id = id;
                Name = name;
                DamageMultiplier = damageMultiplier;
                DefenseMultiplier = defenseMultiplier;
                GoldReward = goldReward;
            }

            public Boss(Player player, float damageMultiplier, float defenseMultiplier, int goldReward)
                : this(player.Id, player.Name, damageMultiplier, defenseMultiplier, goldReward)
            {
            }

            public bool ChangeDamageMulitplier(string amount)
            {
                float newAmount;
                if (!float.TryParse(amount, out newAmount)) return false;
                ChangeDamageMulitplier(newAmount);
                return true;
            }

            public void ChangeDamageMulitplier(float amount)
            {
                DamageMultiplier = amount;
            }

            public bool ChangeDefenseMulitplier(string amount)
            {
                float newAmount;
                if (!float.TryParse(amount, out newAmount)) return false;
                ChangeDefenseMulitplier(newAmount);
                return true;
            }

            public void ChangeDefenseMulitplier(float amount)
            {
                DefenseMultiplier = amount;
            }


            public bool ChangeReward(string amount)
            {
                int newAmount;
                if (!int.TryParse(amount, out newAmount)) return false;
                return ChangeReward(newAmount);
            }

            public bool ChangeReward(int amount)
            {
                if (amount < 0) return false;
                GoldReward = amount;
                return true;
            }


            public float CalculateDamage(float damage)
            {
                return damage * DamageMultiplier;
            }

            public float CalculateDefense(float damage)
            {
                return damage * DefenseMultiplier;
            }


            public override string ToString()
            {
                return Name;
            }
        }

        #endregion

        #region Save and Load Data

        private void Loaded()
        {
            LoadDefaultMessages();
            LoadConfigData();

            permission.RegisterPermission("RaidBoss.Toggle", this);
            permission.RegisterPermission("RaidBoss.Modify", this);
        }

        protected override void LoadDefaultConfig()
        {
            LoadConfigData();
            SaveConfigData();
        }

        private void LoadConfigData()
        {
            DefaultDamageMultiplier = GetConfig("Multipliers", "Default damage", 1.3f);
            DefaultDefenseMulitplier = GetConfig("Multipliers", "Default defense", 0.5f);

            DefaultRewardAmount = GetConfig("Gold", "Default reward amount", 10000);

            BossGold = GetConfig("Gold", "Boss kill reward", true);
            GuildBossGold = GetConfig("Gold", "Rewards available for guild members", false);

            KingIsBoss = GetConfig("Tunables", "King is always a boss", false);
        }

        private void SaveConfigData()
        {
            Config["Multipliers", "Default damage"] = DefaultDamageMultiplier;
            Config["Multipliers", "Default defense"] = DefaultDefenseMulitplier;

            Config["Gold", "Default reward amount"] = DefaultRewardAmount;

            Config["Gold", "Boss kill reward"] = BossGold;
            Config["Gold", "Rewards available for guild members"] = GuildBossGold;

            SaveConfig();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "No permission", "You do not have permission to use this command." },
                { "Invalid args", "Incorrect usage. Type /boss if you need information about how to use the commands." },
                { "Toggle boss gold", "[FF0000]RaidBoss[-] : Boss gold was turned {0}." },
                { "Toggle boss gold guild", "[FF0000]RaidBoss[-] : Boss gold for guild members was turned {0}." },
                { "Change reward amount", "[FF0000]RaidBoss[-] : The reward amount has been changed to {0} gold for {1}." },
                { "No player", "[FF0000]RaidBoss[-] : That player does not appear to be online right now." },
                { "Already a boss", "[FF0000]RaidBoss[-] : That player already is a boss." },
                { "No boss", "[FF0000]RaidBoss[-] : There is no boss with that name." },
                { "Boss added", "[FF0000]RaidBoss[-] : {0} has been turned into a devastating evil knight by the Gods! Kill him quick!" },
                { "Boss removed", "[FF0000]RaidBoss[-] : An evil knight has been reduced to a mere mortal." },
                { "Boss killed", "[FF0000]RaidBoss[-] : An evil knight has been killed!" },
                { "Boss left", "[FF0000]RaidBoss[-] : An evil knight has left this plain of existance!" },
                { "All bosses gone", "[FF0000]RaidBoss[-] : All evil knight are gone!" },
                { "No bosses", "[FF0000]RaidBoss[-] : There are no bosses." },
                { "Bosslist title", "     Boss list :" },
                { "Bosslist player", "       {0}" },
                { "Guild member kill", "[FF0000]RaidBoss[-] : You won't gain the reward for killing a guild member!" },
                { "Gold gained", "[FF0000]RaidBoss[-] : {0} gold reward received." },
                { "Multiplier changed damage", "[FF0000]RaidBoss[-] : The damage multiplier for {0} was set to {1}." },
                { "Multiplier changed defense", "[FF0000]RaidBoss[-] : The defense multiplier for {0} was set to {1}" }
            }, this);
        }

        #endregion

        #region Commands

        [ChatCommand("boss")]
        private void CmdBoss(Player player, string cmd, string[] args)
        {
            if (!args.Any())
            {
                SendHelpText(player);
                return;
            }
            args = Array.ConvertAll(args, a => a.ToLower());
            switch (args[0])
            {
                case "reward":
                    Reward(player, args.Skip(1));
                    break;
                case "add":
                    Add(player, args.Skip(1));
                    break;
                case "remove":
                    Remove(player, args.Skip(1));
                    break;
                case "list":
                    List(player);
                    break;
                case "damage":
                    Mulitplier(player, args.Skip(1), 1);
                    break;
                case "defense":
                    Mulitplier(player, args.Skip(1), 2);
                    break;
                case "help":
                    SendHelpText(player);
                    break;
                default:
                    player.SendError(GetMessage("Invalid args", player));
                    break;
            }
        }

        #endregion

        #region Command Functions

        private void Reward(Player player, IEnumerable<string> args)
        {
            switch (args.First())
            {
                case "guild":
                    RewardGuildToggle(player, args.Skip(1));
                    break;
                case "amount":
                    RewardAmount(player, args.Skip(1));
                    break;
                default:
                    RewardToggle(player, args);
                    break;
            }
        }

        private void RewardToggle(Player player, IEnumerable<string> args)
        {
            if (!CheckPermission(player, "RaidBoss.Toggle")) return;
            if (!CheckArgs(player, args)) return;

            switch (args.First())
            {
                case "on":
                    BossGold = true;
                    player.SendMessage(GetMessage("ToggleBossGold", player), "on");
                    break;
                case "off":
                    BossGold = false;
                    player.SendMessage(GetMessage("ToggleBossGold", player), "off");
                    break;
                default:
                    player.SendError(GetMessage("Invalid args", player));
                    break;
            }
        }

        private void RewardGuildToggle(Player player, IEnumerable<string> args)
        {
            if (!CheckPermission(player, "RaidBoss.Toggle")) return;
            if (!CheckArgs(player, args)) return;

            switch (args.First())
            {
                case "on":
                    GuildBossGold = true;
                    player.SendMessage(GetMessage("ToggleBossGoldGuild", player), "on");
                    break;
                case "off":
                    GuildBossGold = false;
                    player.SendMessage(GetMessage("ToggleBossGoldGuild", player), "on");
                    break;
                default:
                    player.SendError(GetMessage("Invalid args", player));
                    break;
            }
        }

        private void RewardAmount(Player player, IEnumerable<string> args)
        {
            if (!CheckPermission(player, "RaidBoss.modify")) return;
            if (!CheckArgs(player, args, 2)) return;

            var boss = GetBoss(args.First());
            if (boss == null)
            {
                player.SendError(GetMessage("No boss", player));
                return;
            }
            args = args.Skip(1);

            if (!boss.ChangeReward(args.First()))
            {
                player.SendError(GetMessage("Invalid args", player));
                return;
            }

            player.SendMessage(GetMessage("Change reward amount", player), DefaultRewardAmount, boss);
        }

        private void Add(Player player, IEnumerable<string> args)
        {
            if (!CheckPermission(player, "RaidBoss.Modify")) return;
            if (!CheckArgs(player, args)) return;

            var playerName = args.JoinToString(" ");
            var target = Server.GetPlayerByName(playerName);

            if (target == null)
            {
                player.SendError(GetMessage("No player", player));
                return;
            }
            if (BossList.ContainsKey(target.Id))
            {
                player.SendError(GetMessage("Already a boss", player));
                return;
            }

            MakeBoss(target);
        }

        private void Remove(Player player, IEnumerable<string> args)
        {
            if (!CheckPermission(player, "RaidBoss.Modify")) return;
            if (!CheckArgs(player, args)) return;
            if (args.First().EqualsIgnoreCase("all"))
            {
                RemoveAll(player);
                return;
            }

            var boss = GetBoss(args.JoinToString(" "));
            if (boss == null)
            {
                player.SendError(GetMessage("No boss", player));
                return;
            }

            RemoveBoss(boss);
        }

        private void RemoveAll(Player player)
        {
            if (!CheckPermission(player, "RaidBoss.Modify")) return;
            if (!BossList.Any())
            {
                player.SendError(GetMessage("No bosses", player));
                return;
            }

            BossList.Clear();
            PrintToChat(GetMessage("All bosses gone"));
        }

        private void List(Player player)
        {
            if (!BossList.Any())
            {
                player.SendError(GetMessage("No bosses", player));
                return;
            }

            player.SendMessage(GetMessage("Bosslist title", player));
            BossList.Foreach(b => player.SendMessage(GetMessage("Bosslist player", player), b.Value));
        }

        private void Mulitplier(Player player, IEnumerable<string> args, int type)
        {
            if (!CheckPermission(player, "RaidBoss.modify")) return;
            if (!CheckArgs(player, args, 2)) return;

            var boss = GetBoss(args.First());
            if (boss == null)
            {
                player.SendError(GetMessage("No boss", player));
                return;
            }
            args = args.Skip(1);
            
            switch (type)
            {
                case 1:
                    if (boss.ChangeDamageMulitplier(args.First()))
                    {
                        player.SendMessage(GetMessage("Multiplier changed damage", player), boss, boss.DamageMultiplier);
                        return;
                    }
                    break;
                case 2:
                    if (boss.ChangeDefenseMulitplier(args.First()))
                    {
                        player.SendMessage(GetMessage("Multiplier changed defense", player), boss, boss.DefenseMultiplier);
                        return;
                    }
                    break;
            }

            player.SendError(GetMessage("Invalid args", player));
        }

        #endregion

        #region System Functions

        private bool CheckPermission(Player player, string permission)
        {
            if (player.HasPermission(permission)) return true;
            player.SendError(GetMessage("No permission", player));
            return false;
        }

        private bool CheckArgs(Player player, IEnumerable<string> args, int count = 1)
        {
            if (!args.Any())
            {
                player.SendError(GetMessage("Invalid args", player));
                return false;
            }
            if (count <= 1) return true;

            if (args.Count() >= count) return true;
            player.SendError(GetMessage("Invalid args", player));
            return false;
        }

        private void MakeBoss(Player player)
        {
            BossList.Add(player.Id, new Boss(player, DefaultDamageMultiplier, DefaultDefenseMulitplier, DefaultRewardAmount));
            PrintToChat(GetMessage("Boss added"), player.DisplayName);
        }

        private void RemoveBoss(Player player)
        {
            if (!BossList.ContainsKey(player.Id)) return;
            BossList.Remove(player.Id);
            PrintToChat(GetMessage("Boss removed"));
            CheckAllBossesAreGone();
        }

        private void RemoveBoss(Boss boss)
        {
            BossList.Remove(boss.Id);
            PrintToChat(GetMessage("Boss removed"));
            CheckAllBossesAreGone();
        }

        private Boss GetBoss(string target)
        {
            Boss boss;
            ulong id;
            if (ulong.TryParse(target, out id))
            {
                if (BossList.TryGetValue(id, out boss))
                    return boss;
            }
            var player = Server.GetPlayerByName(target);
            if (player == null) return null;
            return BossList.TryGetValue(player.Id, out boss) ? boss : null;
        }

        private Boss GetBoss(Player target)
        {
            Boss boss;
            return BossList.TryGetValue(target.Id, out boss) ? boss : null;
        }

        private void CheckAllBossesAreGone()
        {
            if (!BossList.Any()) PrintToChat(GetMessage("All bosses gone"));
        }

        private bool IsKing(ulong id)
        {
            return SocialAPI.Get<KingsScheme>().IsKing(id);
        }

        #endregion

        #region Hooks

        private void SendHelpText(Player player)
        {
            player.SendMessage("[0000FF]Raid Boss[-]");
            player.SendMessage("[00FF00]/boss help[-] - Show a list of available commands.");
            player.SendMessage("[00FF00]/boss list[-] - Show a list of which players are bosses.");
            if (player.HasPermission("raidboss.toggle"))
            {
                player.SendMessage("[00FF00]/boss reward (on/off)[-] - Toggle the gold reward for killing a boss.");
                player.SendMessage("[00FF00]/boss reward guild (on/off)[-] - Toggle the gold reward for the boss getting killed by one of his guild members.");
            }
            if (player.HasPermission("raidboss.modify"))
            {
                player.SendMessage("[00FF00]/boss reward amount (boss name/id) (amount)[-] - Change the gold reward for a boss.");
                player.SendMessage("[00FF00]/boss add (playername)[-] - Turn a player into a boss.");
                player.SendMessage("[00FF00]/boss remove (playername)[-] - Remove a boss.");
                player.SendMessage("[00FF00]/boss remove all[-] - Remove all bosses.");
                player.SendMessage("[00FF00]/boss damage (boss name/id) (multiplier)[-] - Changes the damage multiplier of a boss.");
                player.SendMessage("[00FF00]/boss defense (boss name/id) (multiplier)[-] - Changes the defense mulitplier of a boss.");
            }
        }

        private void OnEntityHealthChange(EntityDamageEvent damageEvent)
        {
            #region Null Checks
            if (damageEvent == null) return;
            if (damageEvent.Damage == null) return;
            if (damageEvent.Damage.DamageSource == null) return;
            if (!damageEvent.Damage.DamageSource.IsPlayer) return;
            if (damageEvent.Entity == null) return;
            if (!damageEvent.Entity.IsPlayer) return;
            if (damageEvent.Entity == damageEvent.Damage.DamageSource) return;
            #endregion

            var victim = damageEvent.Entity.Owner;
            var damager = damageEvent.Damage.DamageSource.Owner;

            var boss = GetBoss(damager);
            if (boss != null)
            {
                damageEvent.Damage.Amount = boss.CalculateDamage(damageEvent.Damage.Amount);
                return;
            }

            boss = GetBoss(victim);
            if (boss == null) return;

            damageEvent.Damage.Amount = boss.CalculateDefense(damageEvent.Damage.Amount);
        }

        private void OnEntityDeath(EntityDeathEvent deathEvent)
        {
            #region Null checks
            if (deathEvent == null) return;
            if (deathEvent.Entity == null) return;
            if (!deathEvent.Entity.IsPlayer) return;
            if (deathEvent.KillingDamage == null) return;
            if (deathEvent.KillingDamage.DamageSource == null) return;
            if (!deathEvent.KillingDamage.DamageSource.IsPlayer) return;
            #endregion

            var victim = deathEvent.Entity.Owner;
            var killer = deathEvent.KillingDamage.DamageSource.Owner;

            var boss = GetBoss(victim);
            if (boss == null) return;

            BossList.Remove(boss.Id);
            PrintToChat(GetMessage("Boss killed"));
            CheckAllBossesAreGone();

            if (!BossGold) return;
            if (victim.Equals(killer)) return;
            if (boss.GoldReward <= 0) return;

            if (!GuildBossGold && victim.GetGuild().Equals(killer.GetGuild()))
            {
                killer.SendError(GetMessage("Guild member kill", killer));
                return;
            }

            if (GrandExchange == null) return;

            GrandExchange.Call("GiveGold", killer, boss.GoldReward);
            killer.SendMessage(GetMessage("Gold gained", killer), boss.GoldReward);
        }

        private void OnPlayerConnected(Player player)
        {
            if (player == null) return;
            if (!IsKing(player.Id)) return;
            MakeBoss(player);
        }

        private void OnPlayerDisconnected(Player player)
        {
            if (player == null) return;
            if (!BossList.ContainsKey(player.Id)) return;
            BossList.Remove(player.Id);
            PrintToChat(GetMessage("Boss left"));
            CheckAllBossesAreGone();
        }

        private void OnThroneCaptured(AncientThroneCaptureEvent captureEvent)
        {
            #region Checks
            if (captureEvent == null) return;
            if (captureEvent.Cancelled) return;
            if (!KingIsBoss) return;
            #endregion

            var player = captureEvent.Player;
            if (player == null) return;
            MakeBoss(player);
        }

        private void OnThroneReleased(AncientThroneReleaseEvent releaseEvent)
        {
            #region Checks
            if (releaseEvent == null) return;
            if (releaseEvent.Cancelled) return;
            if (!KingIsBoss) return;
            #endregion

            var player = releaseEvent.Sender;
            if (player == null) return;
            RemoveBoss(player);
        }

        #endregion

        #region Utility

        private T GetConfig<T>(string category, string setting, T defaultValue)
        {
            var data = Config[category] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[category] = data;
            }
            object value;
            if (data.TryGetValue(setting, out value)) return (T)Convert.ChangeType(value, typeof(T));
            value = defaultValue;
            data[setting] = value;
            SaveConfig();
            return (T)Convert.ChangeType(value, typeof(T));
        }

        private string GetMessage(string key, Player player = null) => lang.GetMessage(key, this, player?.Id.ToString());

        #endregion
    }
}