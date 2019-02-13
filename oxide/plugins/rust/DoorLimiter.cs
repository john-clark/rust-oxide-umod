using System.Collections.Generic;
using UnityEngine;
using System;
using Oxide.Core;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("DoorLimiter", "redBDGR", "1.0.6", ResourceId = 2334)]
    [Description("Only allow a certain number of people to use one door")]
    class DoorLimiter : RustPlugin
    {
        bool Changed = false;

        public bool silentMode = false;
        public int authedPlayersAllowed = 5;
        public const string permissionName = "doorlimiter.exempt";
        public const string permissionNameADMIN = "doorlimiter.admin";
        public const string permissionNameREMOVE = "doorlimiter.remove";

        void Init()
        {
            permission.RegisterPermission(permissionName, this);
            permission.RegisterPermission(permissionNameADMIN, this);
            permission.RegisterPermission(permissionNameREMOVE, this);
            LoadVariables();
            DoLang();
        }

        void DoLang()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["Max Authorised"] = "There are already too many players authorized to this door!",
                ["Auth Successful"] = "You have been successfully authorized to this door!",
                ["No Perms"] = "You are not allowed to use this command!",
                ["Invalid Syntax REMOVE"] = "Invalid syntax! /doorlimit remove <playername / id>",
                ["No Player Found"] = "No players with that name / steamid were found",
                ["No Entity Found"] = "No entity was found! make sure you are looking at a door",
                ["Entity Not Registered"] = "This door was not found in the database!",
                ["You Are Not The Owner"] = "You are not the owner of this door!",
                ["Player Not Authed To This Door"] = "The target player is not authorised to this door!",
                ["Player Removed"] = "The player was succesfully removed from the doors authorized list",
                ["doorlimit Help"] = "Type /doorlimit remove <playername / id> whilst looking at a door to remove them from the authorization list",

            }, this);
        }

        #region Handling

        object CanUseLockedEntity(BasePlayer player, BaseLock baselock)
        {
            if (player == null || baselock == null) return null;
            if (permission.UserHasPermission(player.UserIDString, permissionName)) return null;
            if (!(baselock.GetParentEntity() is BaseNetworkable)) return null;
            BaseNetworkable door = baselock.GetParentEntity() as BaseNetworkable;
            if (baselock.ShortPrefabName == "lock.code")
            {
                CodeLock codelock = (CodeLock)baselock;
                if (codelock.whitelistPlayers.Contains(player.userID))
                    return null;
                else
                {
                    if (codelock.whitelistPlayers.Count >= authedPlayersAllowed)
                    {
                        if (!silentMode)
                            player.ChatMessage(msg("Max Authorised", player.UserIDString));
                        return false;
                    }
                    else
                    {
                        if (!silentMode)
                            player.ChatMessage(msg("Auth Successful", player.UserIDString));
                        return null;
                    }
                }
            }
            else
                return null;
        }

        #endregion

        void LoadVariables()
        {
            silentMode = Convert.ToBoolean(GetConfig("Settings", "Silent Mode", true));
            authedPlayersAllowed = Convert.ToInt32(GetConfig("Settings", "Authed Players Allowed", 5));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        [ChatCommand("doorlimit")]
        void doorlimitCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionNameREMOVE))
            {
                player.ChatMessage(msg("No Perms", player.UserIDString));
                return;
            }

            if (args.Length == 2)
            {
                if (args[0] == "remove")
                {
                    RaycastHit hitInfo;
                    if (!UnityEngine.Physics.Raycast(player.eyes.HeadRay(), out hitInfo, 3.0f))
                    {
                        player.ChatMessage(msg("No Entity Found", player.UserIDString));
                        return;
                    }
                    BaseEntity entity = hitInfo.transform.GetComponentInParent<BaseEntity>();
                    if (!entity) return;
                    CodeLock codelock = entity as CodeLock;
                    if (!codelock) return;
                    if (codelock.OwnerID != player.userID)
                    {
                        player.ChatMessage(msg("You Are Not The Owner", player.UserIDString));
                        return;
                    }
                    BasePlayer targetplayer = FindPlayer(args[1]);
                    if (DoPlayerChecks(codelock, player) == false)
                        return;

                    codelock.whitelistPlayers.Remove(targetplayer.userID);
                    player.ChatMessage(msg("Player Removed", player.UserIDString));
                }
                else
                {
                    player.ChatMessage(msg("Invalid Syntax REMOVE", player.UserIDString));
                    return;
                }
            }
            else
                DoDoorLimitHelp(player);
        }

        bool DoPlayerChecks(CodeLock codelock, BasePlayer player)
        {
            if (player == null)
            {
                player.ChatMessage(msg("No Player Found", player.UserIDString));
                return false;
            }
            else if (!codelock.whitelistPlayers.Contains(player.userID))
            {
                player.ChatMessage(msg("Player Not Authed To This Door", player.UserIDString));
                return false;
            }
            else
                return true;
        }

        void DoDoorLimitHelp(BasePlayer player)
        {
            player.ChatMessage(msg("doorlimit Help", player.UserIDString));
            return;
        }

        private static BasePlayer FindPlayer(string nameOrId)
        {
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString == nameOrId)
                    return activePlayer;
                if (activePlayer.displayName.Contains(nameOrId, CompareOptions.OrdinalIgnoreCase))
                    return activePlayer;
                if (activePlayer.net?.connection != null && activePlayer.net.connection.ipaddress == nameOrId)
                    return activePlayer;
            }
            return null;
        }

        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        string msg(string key, string id = null) => lang.GetMessage(key, this, id);
    }
}