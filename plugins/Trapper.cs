using System;
using System.Collections.Generic;
using System.Globalization;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Trapper", "redBDGR", "1.0.13")]
    [Description("Adds a few new features to traps")]
    class Trapper : RustPlugin
    {
        private const string permissionNameADMIN = "trapper.admin";
        private const string permissionName = "trapper.auto";
        private const string permissionNameOWNER = "trapper.owner";
        private const string permissionNameFRIENDS = "trapper.friends";
        private const string permissionNameCLAN = "trapper.clan";

        private bool Changed;
        [PluginReference] private Plugin Friends; private bool friendsEnabled;
        [PluginReference] private Plugin RustIO; private bool rustIOEnabled;
        [PluginReference] private Plugin ClansReborn; private bool clansRebornEnabled;
        [PluginReference] private Plugin Clans; private bool clansEnabled;

        private bool hurtFriends;
        private bool hurtClanMates;
        private bool hurtOwner;
        private bool ignoreProjectiles;
        private float resetTime = 5f;

        private void Init()
        {
            LoadVariables();
            permission.RegisterPermission(permissionName, this);
            permission.RegisterPermission(permissionNameOWNER, this);
            permission.RegisterPermission(permissionNameFRIENDS, this);
            permission.RegisterPermission(permissionNameADMIN, this);
            permission.RegisterPermission(permissionNameCLAN, this);
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {
            resetTime = Convert.ToSingle(GetConfig("Settings", "Reset Time", 5f));
            hurtOwner = Convert.ToBoolean(GetConfig("Settings", "Trigger for Owner", true));
            hurtFriends = Convert.ToBoolean(GetConfig("Settings", "Trigger for Friends", true));
            hurtClanMates = Convert.ToBoolean(GetConfig("Settings", "Trigger for clan mates", true));
            ignoreProjectiles = Convert.ToBoolean(GetConfig("Settings", "Ignore Projectiles", false));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        private void Loaded()
        {
            rustIOEnabled = RustIO != null && RustIO.Call<bool>("IsInstalled");
            friendsEnabled = Friends != null;
            clansRebornEnabled = ClansReborn != null;
            clansEnabled = Clans != null;
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (!ignoreProjectiles) return null;
            BaseTrap trap = entity.GetComponent<BaseTrap>();
            if (!trap) return null;
            if (!info.IsProjectile()) return null;
            BearTrap bearTrap = entity.GetComponent<BearTrap>();
            if (bearTrap)
                bearTrap.Arm();
            return true;
        }

        private object OnTrapTrigger(BaseTrap trap, GameObject obj)
        {
            if (!(trap is BearTrap) && !(trap is Landmine)) return null;
            BasePlayer target = obj.GetComponent<BasePlayer>();
            if (target != null)
            {
                if (permission.UserHasPermission(target.UserIDString, permissionNameADMIN))
                    return false;
            }
            var player = FindPlayer(trap.OwnerID.ToString());
            if (target != null && player != null)
            {
                // Owner protection
                if (!hurtOwner)
                    if (target == player)
                        if (permission.UserHasPermission(target.UserIDString, permissionNameOWNER))
                            return false;

                // Friends protection
                if (!hurtFriends)
                {
                    if (friendsEnabled)
                        if (Friends)
                            if (Friends.Call<bool>("AreFriends", target.userID, player.userID))
                                if (permission.UserHasPermission(player.UserIDString, permissionNameFRIENDS))
                                    return false;
                    if (rustIOEnabled)
                        if (RustIO)
                            if (RustIO.Call<bool>("HasFriend", target.UserIDString, player.UserIDString))
                                if (permission.UserHasPermission(player.UserIDString, permissionNameFRIENDS))
                                    return false;
                }

                // Clanmates protection
                if (!hurtClanMates)
                {
                    if (clansEnabled)
                    {
                        if (Clans.Call<string>("GetClanTag", target.UserIDString) == Clans.Call<string>("GetClanTag", player.UserIDString))
                        {
                            if (permission.UserHasPermission(player.UserIDString, permissionNameCLAN))
                                return false;
                        }
                        else if (Clans.Call<string>("GetClanOf", target.UserIDString) == Clans.Call<string>("GetClanOf", player.UserIDString))
                            if (permission.UserHasPermission(player.UserIDString, permissionNameCLAN))
                                return false;
                    }
                }
            }

            if (!(trap is BearTrap)) return null;
            if (player == null) return null;
            if (permission.UserHasPermission(player.UserIDString, permissionName))
                timer.Once(resetTime, () =>
                {
                    if (trap != null)
                        ((BearTrap)trap).Arm();
                });
            return null;
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (data.TryGetValue(datavalue, out value)) return value;
            value = defaultValue;
            data[datavalue] = value;
            Changed = true;
            return value;
        }

        private static BasePlayer FindPlayer(string nameOrId)
        {
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString == nameOrId)
                    return activePlayer;
                if (activePlayer.displayName.Contains(nameOrId, CompareOptions.OrdinalIgnoreCase))
                    return activePlayer;
            }
            foreach (var sleepingPlayer in BasePlayer.sleepingPlayerList)
            {
                if (sleepingPlayer.UserIDString == nameOrId)
                    return sleepingPlayer;
                if (sleepingPlayer.displayName.Contains(nameOrId, CompareOptions.OrdinalIgnoreCase))
                    return sleepingPlayer;
            }
            return null;
        }
    }
}
