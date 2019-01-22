using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Combat Log DB", "mvrb", "1.1.0")]
    [Description("Translate CombatLog IDs into playernames and lookup players' combat logs.")]
    class CombatLogDb : RustPlugin
    {
        private StoredData storedData;

        private string permissionUse = "combatlogdb.use";

        private void Init()
        {
            LoadData();

            permission.RegisterPermission(permissionUse, this);
        }

        private void OnServerInitialized()
        {
            foreach (var player in BasePlayer.activePlayerList) OnPlayerInit(player);
        }

        new private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPlayersFound"] = "No players found with the name {0}",
                ["MultiplePlayersFound"] = "Multiple players found with the name {0}",
                ["NoDataFound"] = "No data found for {0}",
                ["CombatLogIdBelongsTo"] = "The CombatLogID '{0}' belongs to {1}",
                ["CheckConsoleForData"] = "Check your console for CombatLog data on {0}",
                ["CheckingCombatLogFor"] = "---------- CHECKING COMBATLOG FOR {0} ----------",
                ["EndCombatLogOutput"] = "------------------------------------------------------------",
                ["ErrorNameRequired"] = "You must enter a PlayName or SteamID.",
                ["ErrorIdRequired"] = "You must enter an ID found in a Combat Log.",
                ["ErrorInvalidId"] = "You must enter a valid CombatLogID (Numbers only)",
                ["ErrorNoPermission"] = "You do not have permission to use this command."
            }, this);
        }

        [ChatCommand("cid")]
        private void CmdCombatLogId(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionUse))
            {
                player.ChatMessage(Lang("ErrorNoPermission", player.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage(Lang("ErrorIdRequired", player.UserIDString));
                return;
            }

            uint input;

            if (!uint.TryParse(args[0], out input))
            {
                player.ChatMessage(Lang("ErrorInvalidId", player.UserIDString));
                return;
            }

            if (!storedData.Players.ContainsKey(input))
            {
                player.ChatMessage(Lang("NoDataFound", player.UserIDString, input));
                return;
            }

            player.ChatMessage(Lang("CombatLogIdBelongsTo", player.UserIDString, input, GetNameFromId(storedData.Players[input].ToString())));

        }

        [ConsoleCommand("combatlogdb.get")]
        private void ConsoleCmdCombatLog(ConsoleSystem.Arg conArgs)
        {
            if (conArgs?.Connection != null && !permission.UserHasPermission(conArgs?.Player()?.UserIDString, permissionUse)) return;

            var args = conArgs.Args;

            ulong steamID;
            if (!ulong.TryParse(args[0], out steamID))
            {
                SendReply(conArgs, $"{steamID} isn't a valid SteamID64.");
                return;
            }

            int rows = args.Length > 1 ? int.Parse(args[1]) : 20;

            string combatLog = GetCombatLog(steamID, rows);

            SendReply(conArgs, "\n" + combatLog);
        }

        [ChatCommand("combatlog")]
        private void ChatCmdCombatLog(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionUse))
            {
                player.ChatMessage(Lang("ErrorNoPermission", player.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage(Lang("ErrorNameRequired", player.UserIDString));
                return;
            }

            var input = args[0].ToLower();

            var found = new List<BasePlayer>();
            foreach (var t in BasePlayer.activePlayerList)
            {
                if (t.UserIDString == input)
                {
                    found.Clear();
                    found.Add(t);
                    break;
                }
                else if (t.displayName.ToLower() == input)
                {
                    found.Clear();
                    found.Add(t);
                    break;
                }
                if (t.displayName.ToLower().Contains(input))
                {
                    found.Add(t);
                }
            }

            if (found.Count == 0)
            {
                player.ChatMessage(Lang("NoPlayersFound", player.UserIDString, input));
                return;
            }
            else if (found.Count > 1)
            {
                string msg = Lang("MultiplePlayersFound", player.UserIDString, input) + ": \n";
                foreach (var p in found) msg += $"- {p.displayName} \n";
                player.ChatMessage(msg);
                return;
            }

            var target = found[0];

            int rows = args.Length > 1 ? int.Parse(args[1]) : 20;

            player.ConsoleMessage(Lang("CheckingCombatLogFor", player.UserIDString, target.displayName));
            player.ConsoleMessage("\n");
            player.ConsoleMessage(GetCombatLog(target.userID, rows));
            player.ConsoleMessage("\n");
            player.ConsoleMessage(Lang("EndCombatLogOutput", player.UserIDString));

            player.ChatMessage(Lang("CheckConsoleForData", player.UserIDString, target.displayName));
        }

        string GetCombatLog(ulong steamid, int count)
        {
            var storage = CombatLog.Get(steamid);

            TextTable textTable = new TextTable();
            textTable.AddColumn("time");
            textTable.AddColumn("attacker");
            textTable.AddColumn("id");
            textTable.AddColumn("target");
            textTable.AddColumn("id");
            textTable.AddColumn("weapon");
            textTable.AddColumn("ammo");
            textTable.AddColumn("area");
            textTable.AddColumn("distance");
            textTable.AddColumn("old_hp");
            textTable.AddColumn("new_hp");
            textTable.AddColumn("info");

            int num = storage.Count - count;
            int num1 = ConVar.Server.combatlogdelay;
            int num2 = 0;

            foreach (CombatLog.Event evt in storage)
            {
                if (num <= 0)
                {
                    float single = Time.realtimeSinceStartup - evt.time;
                    if (single < (float)num1)
                    {
                        num2++;
                    }
                    else
                    {
                        string str = single.ToString("0.0s");
                        string str1 = evt.attacker == "you" ? GetNameFromId(steamid.ToString()) : evt.attacker;

                        string str2 = storedData.Players.ContainsKey((uint)evt.attacker_id) ? GetNameFromId(storedData.Players[(uint)evt.attacker_id].ToString()) : evt.attacker_id.ToString();

                        string str3 = evt.target == "you" ? GetNameFromId(steamid.ToString()) : evt.target;

                        string str4 = storedData.Players.ContainsKey((uint)evt.target_id) ? GetNameFromId(storedData.Players[(uint)evt.target_id].ToString()) : evt.target_id.ToString();

                        string str5 = evt.weapon;
                        string str6 = evt.ammo;
                        string lower = HitAreaUtil.Format(evt.area).ToLower();
                        string str7 = evt.distance.ToString("0.0m");
                        string str8 = evt.health_old.ToString("0.0");
                        string str9 = evt.health_new.ToString("0.0");
                        string str10 = evt.info;
                        textTable.AddRow(new string[] { str, str1, str2, str3, str4, str5, str6, lower, str7, str8, str9, str10 });
                    }
                }
                else
                {
                    num--;
                }
            }

            string str11 = textTable.ToString();
            if (num2 > 0)
            {
                string str12 = str11;
                object[] objArray = new object[] { str12, "+ ", num2, " ", null };
                objArray[4] = (num2 <= 1 ? "event" : "events");
                str11 = string.Concat(objArray);
                str12 = str11;
                object[] objArray1 = new object[] { str12, " in the last ", num1, " ", null };
                objArray1[4] = (num1 <= 1 ? "second" : "seconds");
                str11 = string.Concat(objArray1);
            }
            return str11;
        }

        private void OnNewSave(string s)
        {
            storedData.Players.Clear();
            SaveData();
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (!storedData.Players.ContainsKey(player.net.ID))
            {
                storedData.Players.Add(player.net.ID, player.userID);
            }

            SaveData();
        }

        private void LoadData() => storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(this.Name);
        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(this.Name, storedData);

        string GetNameFromId(string id) => covalence.Players.FindPlayer(id)?.Name;

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        public class StoredData
        {
            public Dictionary<uint, ulong> Players = new Dictionary<uint, ulong>();
        }
    }
}