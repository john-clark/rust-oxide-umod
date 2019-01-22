using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CodeHatch.Blocks.Networking.Events;
using CodeHatch.Common;
using CodeHatch.Engine.Networking;
using CodeHatch.Networking.Events;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.Networking.Events.Players;
using CodeHatch.Permissions;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Hulk", "SweetLouHD & D-Kay", "2.0.1", ResourceId = 1463)]
    public class Hulk : ReignOfKingsPlugin
    {
        #region Variables

        private static float DefaultDamage { get; set; } = 100000;
        public static string Prefix = "[FFFFFF][[64CEE1]Server[FFFFFF]]: ";
        private Dictionary<ulong, PlayerData> Data { get; set; } = new Dictionary<ulong, PlayerData>();

        private class PlayerData
        {
            public string Name { get; set; }
            public float Damage { get; set; }

            public PlayerData() { }

            public PlayerData(string name)
            {
                Name = name;
                Damage = DefaultDamage;
            }

            public PlayerData(string name, float damage)
            {
                Name = name;
                Damage = damage;
            }

            public float GetDamage()
            {
                return Damage;
            }

            public float GetHeal()
            {
                return Damage * -1;
            }
        }

        #endregion

        #region Save and Load Data

        private void Loaded()
        {
            LoadConfigData();

            permission.RegisterPermission("hulk.use", this);
            permission.RegisterPermission("hulk.status", this);
            permission.RegisterPermission("hulk.status.others", this);
            permission.RegisterPermission("hulk.list", this);
            permission.RegisterPermission("hulk.amount", this);
            permission.RegisterPermission("hulk.amount.others", this);
            permission.RegisterPermission("hulk.admin", this);
            permission.RegisterPermission("hulk.heal", this);
            permission.RegisterPermission("hulk.damage", this);
        }

        protected override void LoadDefaultConfig()
        {
            LoadConfigData();
            SaveConfigData();
        }
        
        private void LoadConfigData()
        {
            DefaultDamage = GetConfig("Default Hulk Damage", 100000);
            Prefix = GetConfig("Message Prefix", "[FFFFFF][[64CEE1]Server[FFFFFF]]: ");
        }

        private void SaveConfigData()
        {
            Config["Default Hulk Damage"] = DefaultDamage;
            Config["Message Prefix"] = Prefix;
        }

        #endregion

        #region Commands

        [ChatCommand("hulk")]
        private void CmdHulk(Player player, string cmd, string[] args)
        {
            if (!args.Any())
            {
                SendHelpText(player);
                return;
            }

            if (!HasPermission(player, "hulk.use"))
            {
                player.SendError("You are not permitted to use this command.[-]");
                return;
            }

            switch (args[0].ToLower())
            {
                case "on":
                    Activate(player);
                    break;
                case "off":
                    Deactivate(player);
                    break;
                case "status":
                    Status(player, args.Skip(1).ToArray());
                    break;
                case "list":
                    List(player);
                    break;
                case "amt":
                    Amount(player, args.Skip(1).ToArray());
                    break;
                default:
                    PrintToChat(player, "{prefix}Incorrect usage. Type /hulk to see the available commands.[-]");
                    break;
            }
        }

        #endregion

        #region Functions

        private void Activate(Player player)
        {
            if (Data.ContainsKey(player.Id))
            {
                player.SendError("You have already activated hulk mode.");
                return;
            }
            var data = new PlayerData(player.Name);
            Data.Add(player.Id, data);
            player.SendMessage($"{Prefix}Hulk mode is now turned on. Amount: {data.GetDamage()}[-]");
        }

        private void Deactivate(Player player)
        {
            if (!Data.ContainsKey(player.Id))
            {
                player.SendError("You have not activated hulk mode yet.");
                return;
            }
            Data.Remove(player.Id);
            player.SendMessage($"{Prefix}Hulk mode is now turned off.[-]");
        }

        private void Status(Player player, string[] args)
        {
            if (!HasPermission(player, "hulk.status")) return;

            if (!args.Any())
            {
                player.SendMessage(
                    Data.ContainsKey(player.Id)
                        ? $"{Prefix}You currently have Hulk turned on. Amount: {Data[player.Id].Damage}[-]"
                        : $"{Prefix}You currently have Hulk turned off.[-]");
                return;
            }
            
            if (!HasPermission(player, "hulk.status.others")) return;

            var targetName = args.JoinToString(" ");
            var target = Server.ClientPlayers.Find(p => string.Equals(p.Name, targetName, StringComparison.CurrentCultureIgnoreCase));
            if (target == null)
            {
                player.SendError($"{Prefix}{targetName} is currently not online.[-]");
                return;
            }

            player.SendMessage(
                Data.ContainsKey(target.Id)
                    ? $"{Prefix}{target.Name} currently has Hulk turned on. Amount: {Data[target.Id].Damage}[-]"
                    : $"{Prefix}{args[1]} currently has Hulk turned off.[-]");
        }

        private void List(Player player)
        {
            if (!HasPermission(player, "hulk.list")) return;

            if (!Data.Any())
            {
                player.SendError($"{Prefix}No one has Hulk Turned on.[-]");
                return;
            }

            player.SendMessage($"{Prefix}List of Hulks:");
            Data.Foreach(p => player.SendMessage($"     {p.Value.Name}"));
        }

        private void Amount(Player player, string[] args)
        {
            if (!HasPermission(player, "hulk.amount")) return;

            if (!Data.ContainsKey(player.Id))
            {
                player.SendError($"You do not have hulk mode turned on.");
                return;
            }

            if (!args.Any())
            {
                player.SendMessage($"{Prefix}Hulk Amount is currently set to {Data[player.Id].Damage}.[-]");
                return;
            }

            float amt;
            if (!float.TryParse(args[0], out amt))
            {
                player.SendError($"{Prefix}{args[0]} is not a valid number. Please try again.[-]");
                return;
            }

            var target = player;
            if (args.Length > 1)
            {
                if (!HasPermission(player, "hulk.amount.others")) return;

                var targetName = args.Skip(1).JoinToString(" ");
                target = Server.GetPlayerByName(targetName);
                if (target == null || Equals(target, player))
                {
                    player.SendError($"{Prefix}{targetName} is currently not online.[-]");
                    return;
                }
                if (!Data.ContainsKey(target.Id))
                {
                    player.SendError($"{target.Name} does not have hulk mode turned on.");
                    return;
                }
            }

            Data[target.Id].Damage = amt;
            player.SendMessage($"{Prefix}You have changed the Hulk Amount of {target.Name} to {amt}.[-]");
        }

        private bool HasPermission(Player player, string permission)
        {
            var hasPermission = player.HasPermission("hulk.admin") || player.HasPermission(permission);
            if (!hasPermission) player.SendError("You are not permitted to use this command.[-]");
            return hasPermission;
        }

        #endregion

        #region Hooks

        private void SendHelpText(Player player)
        {
            player.SendMessage("[0000FF]Hulk[-]");

            if (player.HasPermission("hulk.admin"))
            {
                player.SendMessage("/hulk on - [666666]Turn on Hulk Mode.[-]");
                player.SendMessage("/hulk off - [666666]Turn off Hulk Mode.[-]");
                player.SendMessage("/hulk status - [666666]Displays your current hulk status.[-]");
                player.SendMessage("/hulk status (playername) - [666666]Displays the current hulk status of the target player.[-]");
                player.SendMessage("/hulk list - [666666]List of players that currently have Hulk turned on.[-]");
                player.SendMessage("/hulk amt - [666666]Displays your current Hulk Amount.[-]");
                player.SendMessage("/hulk amt (number) - [666666]Sets the amount of Damage and Repair your Hulk mode does.[-]");
                player.SendMessage("/hulk amt (number) (playername) - [666666]Sets the amount of Damage and Repair Hulk mode does for the target player.[-]");
                return;
            }

            if (player.HasPermission("hulk.use"))
            {

                player.SendMessage("/hulk on - [666666]Turn on Hulk Mode.[-]");
                player.SendMessage("/hulk off - [666666]Turn off Hulk Mode.[-]");
                player.SendMessage("/hulk amt - [666666]Displays the current Hulk Amount.[-]");
            }

            if (player.HasPermission("hulk.status"))
            {
                player.SendMessage("/hulk status - [666666]Displays your current hylk mode status.[-]");
            }

            if (player.HasPermission("hulk.status.others"))
            {
                player.SendMessage("/hulk status (playername) - [666666]Displays the current hulk status of the taget player.[-]");
            }

            if (player.HasPermission("hulk.list"))
            {
                player.SendMessage("/hulk list - [666666]List of players that currently have Hulk turned on.[-]");
            }

            if (player.HasPermission("hulk.amount"))
            {
                player.SendMessage("/hulk amt - [666666]Displays your current Hulk Amount.[-]");
                player.SendMessage("/hulk amt (number) - [666666]Sets the amount of Damage and Repair your Hulk mode does.[-]");
            }

            if (player.HasPermission("hulk.amount.others"))
            {
                player.SendMessage("/hulk amt (number) (playername) - [666666]Sets the amount of Damage and Repair Hulk mode does for the target player.[-]");
            }
        }

        private void OnPlayerConnected(Player player)
        {
            if (Data.ContainsKey(player.Id)) Data.Remove(player.Id);
        }

        private void OnPlayerDisconnected(Player player)
        {
            if (Data.ContainsKey(player.Id)) Data.Remove(player.Id);
        }

        private void OnEntityHealthChange(EntityDamageEvent e)
        {
            #region Checks
            if (e?.Damage == null) return;
            if (e.Damage.DamageSource == null) return;
            if (!e.Damage.DamageSource.IsPlayer) return;
            if (e.Entity == null) return;
            if (e.Entity == e.Damage.DamageSource) return;
            if (!Data.ContainsKey(e.Damage.DamageSource.Owner.Id)) return;
            #endregion

            var player = e.Damage.DamageSource.Owner;
            var data = Data[player.Id];

            if (e.Damage.Amount >= 0)
            {
                if (!HasPermission(player, "hulk.damage")) return;
                if (e.Cancelled) e.Uncancel();
                e.Damage.Amount = data.GetDamage();
                player.SendMessage($"{Prefix}Hulk dealing {e.Damage.Amount} damage.[-]");
            }
            else
            {
                if (!HasPermission(player, "hulk.heal")) return;
                if (e.Cancelled) e.Uncancel();
                e.Damage.Amount = data.GetHeal();
                player.SendMessage($"{Prefix}Hulk healing {e.Damage.Amount} damage.[-]");
            }
        }

        private void OnCubeTakeDamage(CubeDamageEvent e)
        {
            #region Checks
            if (e?.Damage == null) return;
            if (e.Damage.Damager == null) return;
            if (e.Damage.DamageSource == null) return;
            if (!e.Damage.DamageSource.IsPlayer) return;
            if (!Data.ContainsKey(e.Damage.DamageSource.Owner.Id)) return;
            #endregion

            var player = e.Damage.DamageSource.Owner;
            var data = Data[player.Id];

            if (e.Damage.Amount >= 0)
            {
                if (!HasPermission(player, "hulk.damage")) return;
                if (e.Cancelled) e.Uncancel();
                e.Damage.Amount = data.GetDamage();
                player.SendMessage($"{Prefix}Hulk dealing {e.Damage.Amount} damage.[-]");
            }
            else
            {
                if (!HasPermission(player, "hulk.heal")) return;
                if (e.Cancelled) e.Uncancel();
                e.Damage.Amount = data.GetHeal();
                player.SendMessage($"{Prefix}Hulk healing {e.Damage.Amount} damage.[-]");
            }
        }

        #endregion

        #region Utility

        private T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        #endregion
    }
}