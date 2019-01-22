using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Godmode", "Wulf/lukespragg", "4.1.1", ResourceId = 673)]
    [Description("Allows players with permission to be invulerable and god-like")]

    class Godmode : CovalencePlugin
    {
        #region Initialization

        readonly Dictionary<string, long> informHistory = new Dictionary<string, long>();
        readonly Dictionary<string, string> gods = new Dictionary<string, string>();
        readonly DateTime epoch = new DateTime(1970, 1, 1);

        const string permAdmin = "godmode.admin";
        const string permInvulerable = "godmode.invulnerable";
        const string permLootPlayers = "godmode.lootplayers";
        const string permLootProtection = "godmode.lootprotection";
        const string permNoAttacking = "godmode.noattacking";
        const string permMoreDurable = "godmode.moredurable"; // TODO: Register when finalized
        const string permToggle = "godmode.toggle";
        const string permUntiring = "godmode.untiring";

        void Init()
        {
            LoadDefaultConfig();
            LoadDefaultMessages();

            permission.RegisterPermission(permAdmin, this);
            permission.RegisterPermission(permInvulerable, this);
            permission.RegisterPermission(permLootPlayers, this);
            permission.RegisterPermission(permLootProtection, this);
            permission.RegisterPermission(permNoAttacking, this);
            permission.RegisterPermission(permToggle, this);
            permission.RegisterPermission(permUntiring, this);

            if (gods.Count > 0)
            {
                Subscribe(nameof(CanBeWounded));
                Subscribe(nameof(OnEntityTakeDamage));
                Subscribe(nameof(OnRunPlayerMetabolism));
            }
            else
            {
                Unsubscribe(nameof(CanBeWounded));
                Unsubscribe(nameof(OnEntityTakeDamage));
                Unsubscribe(nameof(OnRunPlayerMetabolism));
            }
        }

        #endregion

        #region Configuration

        bool informOnAttack;
        bool showNamePrefix;

        string namePrefix;
        int durability = 0;
        int timeLimit;

        protected override void LoadDefaultConfig()
        {
            // Options
            Config["Inform On Attack (true/false)"] = informOnAttack = GetConfig("Inform On Attack (true/false)", true);
            Config["Show Name Prefix (true/false)"] = showNamePrefix = GetConfig("Show Name Prefix (true/false)", true);

            // Settings
            Config["Name Prefix (Default [God])"] = namePrefix = GetConfig("Name Prefix (Default [God])", "[God]");
            //Config["Durability Boost (0 - 100, 0 to Disable)"] = durability = GetConfig("Durability Boost (0 - 100 Percent, 0 to Disable)", 0); // TODO: Finish
            Config["Time Limit (Seconds, 0 to Disable)"] = timeLimit = GetConfig("Time Limit (Seconds, 0 to Disable)", 0);

            // Cleanup
            Config.Remove("CanBeHurt");
            Config.Remove("CanBeLooted");
            Config.Remove("CanEarnXp");
            Config.Remove("CanHurtPlayers");
            Config.Remove("CanLootPlayers");
            Config.Remove("InfiniteRun");
            Config.Remove("InformOnAttack");
            Config.Remove("PrefixEnabled");
            Config.Remove("PrefixFormat");

            SaveConfig();
        }

        #endregion

        #region Localization

        void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["GodmodeDisabled"] = "You have disabled godmode",
                ["GodmodeDisabledBy"] = "Your godmode has been disabled by {0}",
                ["GodmodeDisabledFor"] = "You have disabled godmode for {0}",
                ["GodmodeEnabled"] = "You have enabled godmode",
                ["GodmodeEnabledBy"] = "Your godmode has been enabled by {0}",
                ["GodmodeEnabledFor"] = "You have enabled godmode for {0}",
                ["InformAttacker"] = "{0} is in godmode and can't take any damage",
                ["InformVictim"] = "{0} just tried to deal damage to you",
                ["NoGods"] = "No players currently have godmode enabled",
                ["NoLooting"] = "You are not allowed to loot a player with godmode",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["PlayerNotFound"] = "Player '{0}' was not found"
            }, this);
        }

        #endregion

        #region Chat Commands

        [Command("god", "godmode")]
        void GodCommand(IPlayer player, string command, string[] args)
        {
            if ((args.Length > 0 && !player.HasPermission(permAdmin)) || !player.HasPermission(permToggle))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            var target = args.Length > 0 ? players.FindPlayer(string.Join(" ", args.ToArray())) : player;
            if (target == null || !target.IsConnected)
            {
                player.Reply(Lang("PlayerNotFound", player.Id, args[0].Sanitize()));
                return;
            }

            if (player.Id == "server_console" && player == target)
            {
                player.Reply("The server console cannot use godmode");
                return;
            }

            ToggleGodmode(target, player);
        }

        [Command("gods", "godlist")]
        void GodsCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAdmin))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            if (gods.Count == 0) player.Reply(Lang("NoGods", player.Id));
            else foreach (var god in gods) player.Reply($"{god.Value} [{god.Key}]");
        }

        #endregion

        #region Godmode Toggle

        void Rename(IPlayer player, bool isGod)
        {
            if (isGod && !player.Name.Contains(namePrefix)) player.Rename($"{namePrefix} {player.Name}");
            else player.Rename(player.Name.Replace(namePrefix, "").Trim());
        }

        void DisableGodmode(IPlayer player)
        {
            if (IsGod(player.Id)) gods.Remove(player.Id);
            ModifyMetabolism(player.Object as BasePlayer, false);
            if (showNamePrefix) Rename(player, false);

            if (gods.Count == 0)
            {
                Unsubscribe(nameof(CanBeWounded));
                Unsubscribe(nameof(OnEntityTakeDamage));
                Unsubscribe(nameof(OnRunPlayerMetabolism));
            }
        }

        void EnableGodmode(IPlayer player)
        {
            if (!IsGod(player.Id)) gods.Add(player.Id, player.Name);
            ModifyMetabolism(player.Object as BasePlayer, true);
            if (showNamePrefix) Rename(player, true);

            if (gods.Count > 0)
            {
                Subscribe(nameof(CanBeWounded));
                Subscribe(nameof(OnEntityTakeDamage));
                Subscribe(nameof(OnRunPlayerMetabolism));
            }
        }

        void ToggleGodmode(IPlayer target, IPlayer player)
        {
            if (IsGod(target.Id))
            {
                DisableGodmode(target);

                if (target == player)
                {
                    player.Reply(Lang("GodmodeDisabled", player.Id));
                }
                else
                {
                    player.Reply(Lang("GodmodeDisabledFor", player.Id, target.Name));
                    target.Reply(Lang("GodmodeDisabledBy", target.Id, player.Name));
                }
            }
            else
            {
                EnableGodmode(target);

                if (target == player)
                {
                    player.Reply(Lang("GodmodeEnabled", player.Id));
                }
                else
                {
                    player.Reply(Lang("GodmodeEnabledFor", player.Id, target.Name));
                    target.Reply(Lang("GodmodeEnabledBy", target.Id, player.Name));
                }

                if (timeLimit > 0) timer.Once(timeLimit, () => DisableGodmode(target));
            }
        }

        #endregion

        #region Loot Handling

        bool IsLootable(BasePlayer target, BasePlayer looter)
        {
            if (permission.UserHasPermission(target.UserIDString, permLootProtection) && !permission.UserHasPermission(looter.UserIDString, permLootPlayers))
            {
                NextTick(() =>
                {
                    looter.EndLooting();
                    looter.ChatMessage(Lang("NoLooting", looter.UserIDString));
                });
                return false;
            }
            return true;
        }

        object CanLootPlayer(BasePlayer target, BasePlayer looter) => !IsLootable(target, looter) ? (object)false : null;

        void OnLootPlayer(BasePlayer looter, BasePlayer target) => IsLootable(target, looter);

        #endregion

        #region Invulerability

        object CanBeWounded(BasePlayer player) => IsGod(player.UserIDString) ? (object)false : null;

        void OnEntityTakeDamage(BaseEntity entity, HitInfo info)
        {
            var target = entity as BasePlayer;
            var attacker = info.Initiator as BasePlayer;
            if (target == null) return;

            if (IsGod(target.UserIDString) && permission.UserHasPermission(target.UserIDString, permInvulerable))
            {
                if (informOnAttack && attacker != null) InformPlayers(target, attacker);
                if (durability > 0) ScaleDamage(ref info); else NullifyDamage(ref info);
                return;
            }

            if (attacker != null && IsGod(attacker.UserIDString) && permission.UserHasPermission(attacker.UserIDString, permNoAttacking))
            {
                if (informOnAttack) InformPlayers(target, attacker);
                if (durability > 0) ScaleDamage(ref info); else NullifyDamage(ref info);
            }
        }

        void NullifyDamage(ref HitInfo info)
        {
            info.damageTypes = new DamageTypeList();
            info.HitMaterial = 0;
            info.PointStart = Vector3.zero;
        }

        void ScaleDamage(ref HitInfo info)
        {
            info.damageTypes = new DamageTypeList();
            info.damageTypes.ScaleAll(durability);
        }

        #endregion

        #region Untiring

        object OnRunPlayerMetabolism(PlayerMetabolism metabolism, BaseCombatEntity entity)
        {
            var player = entity.ToPlayer();
            if (!IsGod(player.UserIDString) || !permission.UserHasPermission(player.UserIDString, permUntiring)) return null;

            var craftLevel = player.currentCraftLevel;
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Workbench1, craftLevel == 1f);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Workbench2, craftLevel == 2f);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Workbench3, craftLevel == 3f);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.NoSprint, false);
            return true;
        }

        static void ModifyMetabolism(BasePlayer player, bool isGod)
        {
            if (isGod)
            {
                player.health = 100;
                player.metabolism.bleeding.max = 0;
                player.metabolism.bleeding.value = 0;
                player.metabolism.calories.min = 500;
                player.metabolism.calories.value = 500;
                player.metabolism.dirtyness.max = 0;
                player.metabolism.dirtyness.value = 0;
                player.metabolism.heartrate.min = 0.5f;
                player.metabolism.heartrate.max = 0.5f;
                player.metabolism.heartrate.value = 0.5f;
                player.metabolism.hydration.min = 250;
                player.metabolism.hydration.value = 250;
                player.metabolism.oxygen.min = 1;
                player.metabolism.oxygen.value = 1;
                player.metabolism.poison.max = 0;
                player.metabolism.poison.value = 0;
                player.metabolism.radiation_level.max = 0;
                player.metabolism.radiation_level.value = 0;
                player.metabolism.radiation_poison.max = 0;
                player.metabolism.radiation_poison.value = 0;
                player.metabolism.temperature.min = 32;
                player.metabolism.temperature.max = 32;
                player.metabolism.temperature.value = 32;
                player.metabolism.wetness.max = 0;
                player.metabolism.wetness.value = 0;
            }
            else
            {
                player.metabolism.bleeding.min = 0;
                player.metabolism.bleeding.max = 1;
                player.metabolism.calories.min = 0;
                player.metabolism.calories.max = 500;
                player.metabolism.dirtyness.min = 0;
                player.metabolism.dirtyness.max = 100;
                player.metabolism.heartrate.min = 0;
                player.metabolism.heartrate.max = 1;
                player.metabolism.hydration.min = 0;
                player.metabolism.hydration.max = 250;
                player.metabolism.oxygen.min = 0;
                player.metabolism.oxygen.max = 1;
                player.metabolism.poison.min = 0;
                player.metabolism.poison.max = 100;
                player.metabolism.radiation_level.min = 0;
                player.metabolism.radiation_level.max = 100;
                player.metabolism.radiation_poison.min = 0;
                player.metabolism.radiation_poison.max = 500;
                player.metabolism.temperature.min = -100;
                player.metabolism.temperature.max = 100;
                player.metabolism.wetness.min = 0;
                player.metabolism.wetness.max = 1;
            }
            player.metabolism.SendChangesToClient();
        }

        #endregion

        #region Notifications

        void InformPlayers(BasePlayer victim, BasePlayer attacker)
        {
            if (victim == null || attacker == null) return;
            if (victim == attacker) return;

            if (!informHistory.ContainsKey(victim.UserIDString)) informHistory.Add(victim.UserIDString, 0);
            if (!informHistory.ContainsKey(attacker.UserIDString)) informHistory.Add(attacker.UserIDString, 0);

            if (Timestamp - informHistory[victim.UserIDString] > 15)
            {
                attacker.ChatMessage(Lang("InformAttacker", attacker.UserIDString, victim.displayName));
                informHistory[victim.UserIDString] = Timestamp;
            }

            if (Timestamp - informHistory[attacker.UserIDString] > 15)
            {
                victim.ChatMessage(Lang("InformVictim", victim.UserIDString, attacker.displayName));
                informHistory[victim.UserIDString] = Timestamp;
            }
        }

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        long Timestamp => (long)DateTime.UtcNow.Subtract(epoch).TotalSeconds;

        bool IsGod(string id) => gods.ContainsKey(id);

        #endregion
    }
}