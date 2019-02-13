using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("DCProtect", "FireStorm78", "1.0.5", ResourceId = 2724)]
    [Description("Prevents players from looting other players that have disconnected for a specified time.")]
    class DCProtect : RustPlugin
    {

        #region Initialization

        List<string> PlayerList = new List<string>();

        void Init()
        {
            LoadDefaultConfig();
        }

        #endregion

        #region Configuration

        int DC_DelayInSeconds;
        int Start_DelayInSeconds;
        bool PreventLooting;
        bool PreventDamage;

        protected override void LoadDefaultConfig()
        {
            Config["DC_DelayInSeconds"] = DC_DelayInSeconds = GetConfig("DC_DelayInSeconds", 300);
            Config["Start_DelayInSeconds"] = Start_DelayInSeconds = GetConfig("Start_DelayInSeconds", 300);
            Config["PreventLooting"] = PreventLooting = GetConfig("PreventLooting", true);
            Config["PreventDamage"] = PreventDamage = GetConfig("PreventDamage", true);
            SaveConfig();
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {

            string Usage = "<size=30><color=blue>DC Protect</color></size> v{0}\nby {1}\n\n";
            Usage += "NOTE: All commands are meant for testing by a moderator or administrator.\n\n";
            Usage += "Usage Syntax:\n";
            Usage += "/DCP add - Manually add the player you are looking at to the protection list.\n";
            Usage += "/DCP remove - Manually remove the player you are looking at from the protection list.\n";
            Usage += "/DCP list - List all current protected players by SteamID.\n";
            Usage += "/DCP time - Show the number of seconds since the server has started.\n";

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "This player has just disconnected. They are protected for '{0}' seconds. This action has been logged.",
                ["ServerStart"] = "The server just started. All players are protected for '{0}' seconds. This action has been logged.",
                ["InvalidCommand"] = "[DCP] That is an invalid command.",
                ["ManualAdd"]= "[DCP] You are looking at player {0}|{1}. Manually adding protection.",
                ["ManualRemove"] = "[DCP] You are looking at player {0}|{1}. Manually removing protection.",
                ["ListTitle"] = "[DCP] Current Protected Player List",
                ["TimerComplete"] = "Timer Complete. Removing Player: {0}|{1}.",
                ["PlayerDisconnected"] = "Player Disconnected. Adding Player {0}|{1} to protection list.",
                ["LogViolation_DC_Loot"] = "Player {0}|{1} tried to loot {2}|{3} after they disconnected...tisk tisk.",
                ["LogViolation_ServerStart_Loot"] = "Player {0}|{1} tried to loot {2}|{3} after the server started...tisk tisk.",
                ["LogViolation_DC_Damage"] = "Player {0}|{1} tried to damage {2}|{3} after they disconnected...tisk tisk.",
                ["LogViolation_ServerStart_Damage"] = "Player {0}|{1} tried to damage {2}|{3} after the server started...tisk tisk.",
                ["Usage"] = Usage
            }, this);
        }

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T defaultValue) => Config[name] == null ? defaultValue : (T)Convert.ChangeType(Config[name], typeof(T));

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private bool IsAdmin(BasePlayer player)
        {
            if (player == null) return false;
            if (player?.net?.connection == null) return true;
            return player.net.connection.authLevel > 0;
        }
        #endregion

        [ChatCommand("DCP")]
        private void cmdDCP(BasePlayer player, string command, string[] args)
        {
            if (args.Length > 0)
            {
                switch (args[0].ToLower())
                {
                    case "add":
                        cmdDCP_Add(player, command, args.Skip(1).ToArray());
                        break;
                    case "remove":
                        cmdDCP_Remove(player, command, args.Skip(1).ToArray());
                        break;
                    case "list":
                        cmdDCP_List(player, command, args.Skip(1).ToArray());
                        break;
                    case "time":
                        PrintToChat(player, Time.realtimeSinceStartup.ToString());
                        break;
                    default:
                        PrintToChat(player, Lang("InvalidCommand", player.UserIDString));
                        PrintToChat(player, Lang("Usage", player.UserIDString, this.Version, this.Author));
                        break;
                }
            }
            else
            {
                PrintToChat(player, Lang("Usage", player.UserIDString, this.Version, this.Author));
            }
        }

        private void cmdDCP_Add(BasePlayer player, string command, string[] args)
        {
            if (IsAdmin(player))
            {

                RaycastHit hit;
                if (Physics.Raycast(player.eyes.HeadRay(), out hit, 10f))
                {
                    BaseEntity closestEntity = hit.GetEntity();

                    if (closestEntity is BasePlayer)
                    {
                        BasePlayer target = (BasePlayer) closestEntity;
                        PrintToChat(player, Lang("ManualAdd", player.UserIDString, target.displayName, target.UserIDString));

                        PlayerList.Add(target.UserIDString);
                        timer.Once(DC_DelayInSeconds, () =>
                        {
                            PrintWarning(Lang("TimerComplete", null, target.UserIDString, target.displayName));
                            PlayerList.Remove(target.UserIDString);
                        });
                    }                    
                }
            }
        }

        private void cmdDCP_Remove(BasePlayer player, string command, string[] args)
        {
            if (IsAdmin(player))
            {
                RaycastHit hit;
                if (Physics.Raycast(player.eyes.HeadRay(), out hit, 10f))
                {
                    BaseEntity closestEntity = hit.GetEntity();
                    if (closestEntity is BasePlayer)
                    {
                        BasePlayer target = (BasePlayer)closestEntity;
                        PrintToChat(player, Lang("ManualRemove", player.UserIDString, target.displayName, target.UserIDString));
                        PlayerList.Remove(target.UserIDString);
                    }
                }
            }
        }

        private void cmdDCP_List(BasePlayer player, string command, string[] args)
        {
            if (IsAdmin(player))
            {
                PrintToChat(player, Lang("ListTitle", player.UserIDString));
                foreach (string item in PlayerList)
                {
                    SendReply(player, item);
                }
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            PrintWarning(Lang("PlayerDisconnected", null, player.UserIDString, player.displayName));
            PlayerList.Add(player.UserIDString);
            timer.Once(DC_DelayInSeconds, () =>
            {
                PrintWarning(Lang("TimerComplete", null, player.UserIDString, player.displayName));
                PlayerList.Remove(player.UserIDString);
            });
        }

        bool CanLootPlayer(BasePlayer target, BasePlayer looter)
        {
            if (PreventLooting)
            {
                if (Time.realtimeSinceStartup < Start_DelayInSeconds)
                {
                    PrintWarning(Lang("LogViolation_ServerStart_Loot", null, looter.UserIDString, looter.displayName, target.UserIDString, target.displayName));
                    PrintToChat(looter, Lang("ServerStart", looter.UserIDString, Start_DelayInSeconds));
                    return false;
                }
                else if (PlayerList.Contains(target.UserIDString))
                {
                    PrintWarning(Lang("LogViolation_DC_Loot", null, looter.UserIDString, looter.displayName, target.UserIDString, target.displayName));
                    PrintToChat(looter, Lang("NotAllowed", looter.UserIDString, DC_DelayInSeconds));
                    return false;
                }
            }
            return true;

        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (PreventDamage && entity is BasePlayer && (entity as BasePlayer).IsSleeping())
            {
                if (info.Initiator == null) return null;

                if (info.Initiator is BasePlayer)
                {
                    BasePlayer attacker = info.Initiator as BasePlayer;
                    BasePlayer target = entity as BasePlayer;

                    if (Time.realtimeSinceStartup < Start_DelayInSeconds)
                    {
                        PrintWarning(Lang("LogViolation_ServerStart_Damage", null, attacker.UserIDString, attacker.displayName, target.UserIDString, target.displayName));
                        PrintToChat(attacker, Lang("ServerStart", attacker.UserIDString, Start_DelayInSeconds));
                        return false;
                    }
                    else if (PlayerList.Contains(target.UserIDString))
                    {
                        PrintWarning(Lang("LogViolation_DC_Damage", null, attacker.UserIDString, attacker.displayName, target.UserIDString, target.displayName));
                        PrintToChat(attacker, Lang("NotAllowed", attacker.UserIDString, DC_DelayInSeconds));
                        return false;
                    }
                }
                else
                {
                    BaseEntity attacker = info.Initiator;
                    BasePlayer target = entity as BasePlayer;

                    if (Time.realtimeSinceStartup < Start_DelayInSeconds)
                    {
                        PrintWarning(Lang("LogViolation_ServerStart_Damage", null, "0", attacker.name, target.UserIDString, target.displayName));
                        return false;
                    }
                    else if (PlayerList.Contains(target.UserIDString))
                    {
                        PrintWarning(Lang("LogViolation_DC_Damage", null, "0", attacker.name, target.UserIDString, target.displayName));
                        return false;
                    }
                }
            }

            return null;
        }
    }
}
