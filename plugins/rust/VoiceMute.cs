using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Voice Mute", "Tori1157", "1.0.2")]
    [Description("Voice mute via commands with time and reason.")]

    class VoiceMute : CovalencePlugin
    {
        #region Fields

        private const string permMute = "voicemute.mute";
        private const string permList = "voicemute.list";
        private const string permInfo = "voicemute.info";
        private const string permUnmute = "voicemute.unmute";
        private const string permCheckInfo = "voicemute.checkinfo";
        private const string mData = "Mutes";

        private bool Changed;
        private bool addReason;
        private bool broadcastMessage;
        private bool hasExpired = false;

        private DataFileSystem MuteData;

        #endregion Fields

        #region Initialization & Loading

        private void OnServerInitialized() => Interface.Oxide.ReloadPlugin(Name);

        private void Loaded() => CheckMutes();

        private void Init()
        {
            LoadVariables();
            LoadStoredData();
        }

        private void LoadVariables()
        {
            MuteData = new DataFileSystem($"{Interface.Oxide.DataDirectory}{Path.DirectorySeparatorChar}VoiceMute");

            Mute = new MuteManager();

            permission.RegisterPermission(permMute, this);
            permission.RegisterPermission(permList, this);
            permission.RegisterPermission(permUnmute, this);

            addReason = BoolConfig("General Settings", "Replace Existing Reason", true);
            broadcastMessage = BoolConfig("General Settings", "Broadcast Mutes", true);

            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new config file");
            Config.Clear();
            LoadVariables();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Help Message"] = "There are shortened versions of the commands.\n\n- [#add8e6]Mute[/#] -> M\n- [#add8e6]Unmute[/#] -> UM\n- [#add8e6]List[/#] -> L\n- [#add8e6]Info[/#] -> I\n\n- [#orange]/voice[/#] [i](Displays this message)[/i]\n- [#orange]/voice mute[/#] [#add8e6]<\"user name\" | SteamID64> <time: 1d1h1m1s> <\"optional reason\">[/#] [i](Voice-mutes a player within specified time)[/i]\n- [#orange]/voice unmute[/#] [#add8e6]<\"user name\" | SteamID64>[/#] [i](Voice-unmute's a player)[/i]\n- [#orange]/voice list[/#] [i](Displays all voice-mutes)[/i]\n- [#orange]/voice info[/#] [i](Displays your mute info)[/i]\n- [#orange]/voice info[/#] [#add8e6]<\"user name\" | SteamID64>[/#] [i](Displays targeted player's mute info)[/i]",
                ["You Are Muted"] = "You are currently [#lightblue]voice-muted[/#], no one can hear you.",
                ["Mute Info"] = "[#lightblue]Muted Name[/#]: {0}\n[#lightblue]Muter Name[/#]: {1}\n[#lightblue]Mute Reason[/#]: {2}\n[#lightblue]Mute Time Left[/#]: {3}",

                ["Broadcast Mute Message"] = "[#lightblue]{0}[/#] has been voice-muted by [#lightblue]{1}[/#] for [#lightblue]{2}[/#]{3}",
                ["Broadcast Unmute Message"] = "[#lightblue]{0}[/#] has been voice-unmuted.",
                ["Player Muted"] = "You have voice-muted [#lightblue]{0}[/#] for [#lightblue]{1}[/#][#lightblue]{2}[/#].",
                ["Target Muted"] = "You have been voice-muted by [#lightblue]{0}[/#] for [#lightblue]{1}[/#][#lightblue]{2}[/#].",
                ["Player Unmuted"] = "You have voice-unmuted [#lightblue]{0}[/#].",
                ["Target Unmuted"] = "You have been voice-unmuted.",
                ["No Mutes"] = "There are currently no voice-muted players.",
                ["Not Muted"] = "[#lightblue]{0}[/#] is not voice-muted.",
                ["You Not Muted"] = "You're currently not voice-muted.",

                ["No Permission"] = "You do not have permission to use the '[#lightblue]{0}[/#]' command.",
                ["SteamID Not Found"] = "Could not find this SteamID: [#lightblue]{0}[/#].",
                ["Player Not Found"] = "Could not find this player: [#lightblue]{0}[/#].",
                ["Multiple Players Found"] = "Found multiple players!\n\n{0}",

                ["Invalid Parameter"] = "'[#lightblue]{0}[/#]' is an invalid parameter, do [#orange]/voice[/#] for more information.",
                ["Invalid Syntax Mute"] = "Invalid Syntax! | /voice mute <\"user name\" | SteamID64> <time: 1d1h1m1s> <\"optional reason\">",
                ["Invalid Syntax Unmute"] = "Invalid Syntax! | /voice unmute <\"user name\" | SteamID64>",

                ["Because"] = "because",

                ["Prefix Help"] = "Voice Mute Help",
                ["Prefix Info"] = "Voice Mute Info",
                ["Prefix List"] = "Voice Mute List",
            }, this);
        }

        #endregion Initialization & Loading

        #region Commands

        [Command("voice")]
        private void VoiceCommand(IPlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                SendInfoMessage(player, Lang("Prefix Help", player.Id), $"[+13]{Lang("Help Message", player.Id)}[/+]");
                return;
            }

            var CommandArg = args[0].ToLower();
            var CaseArgs = (new List<object>
            {
                "mute", "m", "unmute", "um", "list", "l", "info", "i"
            });

            if (!CaseArgs.Contains(CommandArg))
            {
                SendChatMessage(player, Lang("Invalid Parameter", player.Id, CommandArg));
                return;
            }

            switch (CommandArg)
            {
                case "m":
                case "mute":
                    MutePlayer(player, command, args);
                    return;
                case "um":
                case "unmute":
                    UnmutePlayer(player, command, args);
                    return;
                case "l":
                case "list":
                    ListMutes(player, command, args);
                    return;
                case "i":
                case "info":
                    InfoMute(player, command, args);
                    return;
            }
        }

        #endregion Commands

        #region Functions

        private bool CanMute(IPlayer player) => player.HasPermission(permMute);
        private bool CanList(IPlayer player) => player.HasPermission(permList);
        private bool CanInfo(IPlayer player) => player.HasPermission(permInfo);
        private bool CanUnmute(IPlayer player) => player.HasPermission(permUnmute);
        private bool CanCheckInfo(IPlayer player) => player.HasPermission(permCheckInfo);

        private void OnUserConnected(IPlayer player)
        {
            var BPlayer = player.Object as BasePlayer;

            if (Mute.MuteExists(player) && !BPlayer.HasPlayerFlag(BasePlayer.PlayerFlags.VoiceMuted))
                BPlayer.SetPlayerFlag(BasePlayer.PlayerFlags.VoiceMuted, true);

            if (!Mute.MuteExists(player) && BPlayer.HasPlayerFlag(BasePlayer.PlayerFlags.VoiceMuted))
                BPlayer.SetPlayerFlag(BasePlayer.PlayerFlags.VoiceMuted, false);
        }

        private void OnPlayerVoice(BasePlayer player, Byte[] data)
        {
            if (Mute.MuteExists(player.IPlayer) && !Mute.MessageSent(player.UserIDString))
            {
                SendChatMessage(player.IPlayer, Lang("You Are Muted", player.UserIDString));

                // Not saving, so we don't have to change it on unload.
                Mute.SetMessageSent(player.UserIDString, true);
                timer.Once(30f, () => { Mute.SetMessageSent(player.UserIDString, false); });
            }
        }

        private void MutePlayer(IPlayer player, string command, string[] args)
        {
            var CommandInfo = $"{command} {args[0].ToLower()}";

            BasePlayer BPlayer = player.Object as BasePlayer;
            // /voice mute Tori1157 1d1h1m1s "Testing stuff"

            if (!CanMute(player) && !player.IsServer)
            {
                SendChatMessage(player, Lang("No Permission", player.Id, CommandInfo));
                return;
            }

            if (args.Length < 3)
            {
                SendChatMessage(player, Lang("Invalid Syntax Mute", player.Id));
                return;
            }

            TimeSpan timeSpan;

            if (!TryParseTimeSpan(args[2], out timeSpan))
            {
                SendChatMessage(player, Lang("Invalid Time Span", player.Id));
                return;
            }

            IPlayer target = GetPlayer(args[1], player);
            
            if (target == null)
                return;

            var TBPlayer = target.Object as BasePlayer;

            string reason = (args.Length < 4 ? null : args[3]);

            Mute.AddMute(target, player.Name, DateTime.UtcNow + timeSpan, reason, !addReason);
            SaveMutes();
            TBPlayer.SetPlayerFlag(BasePlayer.PlayerFlags.VoiceMuted, true);

            if (broadcastMessage)
                SendBroadcastMessage(Lang("Broadcast Mute Message", player.Id, target.Name, player.Name, FormatTime(timeSpan), $" {Lang("Because", player.Id)} [#lightblue]{reason}[/#]"));
            else
            {
                SendChatMessage(player, Lang("Player Muted", player.Id, target.Name, FormatTime(timeSpan), reason));
                SendChatMessage(target, Lang("Target Muted", target.Id, player.Name, FormatTime(timeSpan), reason));
            }
        }

        private void UnmutePlayer(IPlayer player, string command, string[] args)
        {
            var CommandInfo = $"{command} {args[0].ToLower()}";

            BasePlayer BPlayer = player.Object as BasePlayer;
            // /voice unmute Tori1157

            if (!CanUnmute(player) && !player.IsServer)
            {
                SendChatMessage(player, Lang("No Permission", player.Id, CommandInfo));
                return;
            }

            if (args.Length < 2)
            {
                SendChatMessage(player, Lang("Invalid Syntax Unmute", player.Id));
                return;
            }

            IPlayer target = GetPlayer(args[1], player);

            if (target == null)
                return;

            var TBPlayer = target.Object as BasePlayer;

            if (!Mute.MuteExists(target))
            {
                SendChatMessage(player, Lang("Not Muted", player.Id, target.Name));
                return;
            }

            Mute.RemoveMute(target);
            SaveMutes();
            TBPlayer.SetPlayerFlag(BasePlayer.PlayerFlags.VoiceMuted, false);

            if (broadcastMessage)
                SendBroadcastMessage(Lang("Broadcast Unmute Message", player.Id, target.Name));
            else
            {
                SendChatMessage(player, Lang("Player Unmuted", player.Id, target.Name));
                SendChatMessage(target, Lang("Target Unmuted", target.Id));
            }
        }

        private void ListMutes(IPlayer player, string command, string[] args)
        {
            var CommandInfo = $"{command} {args[0].ToLower()}";
            // /voice list

            if (!CanList(player) && !player.IsServer)
            {
                SendChatMessage(player, Lang("No Permission", player.Id, CommandInfo));
                return;
            }

            if (Mutes.Count == 0)
            {
                SendChatMessage(player, Lang("No Mutes", player.Id));
                return;
            }

            var mutes = string.Join(Environment.NewLine, Mutes.Select(kvp => $"- [#add8e6]{Mutes[kvp.Key].PlayerName}[/#]: {FormatTime(kvp.Value.ExpireDate - DateTime.UtcNow)}").ToArray());

            SendInfoMessage(player, Lang("Prefix List", player.Id), mutes);
        }

        private void InfoMute(IPlayer player, string command, string[] args)
        {
            var CommandInfo = $"{command} {args[0].ToLower()}";
            // /voice info
            // /voice info tori

            if (args.Length == 2)
            {
                if (!CanCheckInfo(player) && !player.IsServer)
                {
                    SendChatMessage(player, Lang("No Permission", player.Id, CommandInfo));
                    return;
                }

                IPlayer target = GetPlayer(args[1], player);

                if (target == null)
                    return;

                if (!Mute.MuteExists(player))
                {
                    SendChatMessage(player, Lang("Not Muted", player.Id, target.Name));
                    return;
                }

                SendInfoMessage(player, Lang("Prefix Info", player.Id), Lang("Mute Info", player.Id, Mute.GetPlayerName(target), Mute.GetAdmin(target), Mute.GetReason(target), FormatTime((DateTime)Mute.GetExpireDate(target) - DateTime.UtcNow)));
            }

            if (!Mute.MuteExists(player))
            {
                SendChatMessage(player, Lang("You Not Muted", player.Id));
                return;
            }

            SendInfoMessage(player, Lang("Prefix Info", player.Id), Lang("Mute Info", player.Id, Mute.GetPlayerName(player), Mute.GetAdmin(player), Mute.GetReason(player), FormatTime((DateTime)Mute.GetExpireDate(player) - DateTime.UtcNow)));
        }

        #endregion Functions

        #region Time

        // Credit to LaserHydra for this code
        string FormatTime(TimeSpan time) => $"{(time.Days == 0 ? string.Empty : $"{time.Days} day(s)")}{(time.Days != 0 && time.Hours != 0 ? $", " : string.Empty)}{(time.Hours == 0 ? string.Empty : $"{time.Hours} hour(s)")}{(time.Hours != 0 && time.Minutes != 0 ? $", " : string.Empty)}{(time.Minutes == 0 ? string.Empty : $"{time.Minutes} minute(s)")}{(time.Minutes != 0 && time.Seconds != 0 ? $", " : string.Empty)}{(time.Seconds == 0 ? string.Empty : $"{time.Seconds} second(s)")}";

        private bool TryParseTimeSpan(string source, out TimeSpan timeSpan)
        {
            int seconds = 0, minutes = 0, hours = 0, days = 0;

            Match s = new Regex(@"(\d+?)s", RegexOptions.IgnoreCase).Match(source);
            Match m = new Regex(@"(\d+?)m", RegexOptions.IgnoreCase).Match(source);
            Match h = new Regex(@"(\d+?)h", RegexOptions.IgnoreCase).Match(source);
            Match d = new Regex(@"(\d+?)d", RegexOptions.IgnoreCase).Match(source);

            if (s.Success)
                seconds = Convert.ToInt32(s.Groups[1].ToString());

            if (m.Success)
                minutes = Convert.ToInt32(m.Groups[1].ToString());

            if (h.Success)
                hours = Convert.ToInt32(h.Groups[1].ToString());

            if (d.Success)
                days = Convert.ToInt32(d.Groups[1].ToString());

            source = source.Replace($"{seconds}s", string.Empty);
            source = source.Replace($"{minutes}m", string.Empty);
            source = source.Replace($"{hours}h", string.Empty);
            source = source.Replace($"{days}d", string.Empty);

            if (!string.IsNullOrEmpty(source) || (!s.Success && !m.Success && !h.Success && !d.Success))
            {
                timeSpan = default(TimeSpan);
                return false;
            }

            timeSpan = new TimeSpan(days, hours, minutes, seconds);

            return true;
        }
        // End credit for LaserHydra

        private void CheckMutes()
        {
            timer.Repeat(10, 0, () =>
            {
                List<string> expired = Mutes.Where(m => m.Value.Expired).Select(m => m.Key).ToList();

                foreach (string id in expired)
                {
                    IPlayer player = players.FindPlayerById(id);
                    var BPlayer = player.Object as BasePlayer;

                    if (player != null)
                    {
                        if (Mute.IsExpired(id))
                        {
                            if (broadcastMessage)
                                SendBroadcastMessage(Lang("Broadcast Unmute Message", null, player.Name));
                            else
                                SendChatMessage(player, Lang("Target Unmuted", player.Id));

                            Mute.RemoveMute(id);
                            SaveMutes();

                            if (BPlayer != null)
                                BPlayer.SetPlayerFlag(BasePlayer.PlayerFlags.VoiceMuted, false);
                        }
                    }

                    if (!hasExpired)
                        hasExpired = true;
                }

                if (hasExpired)
                {
                    SaveMutes();
                    hasExpired = false;
                }
            });
        }

        #endregion Time

        #region Data

        MuteManager Mute;

        private static Dictionary<string, MuteInfo> Mutes = new Dictionary<string, MuteInfo>();
        public class MuteInfo
        {
            public string PlayerName { get; set; }
            public string PlayerId { get; set; }
            public string Reason { get; set; }
            public string Admin { get; set; }
            public bool Sent { get; set; }
            public DateTime ExpireDate { get; set; }

            public bool Expired => ExpireDate < DateTime.UtcNow;

            public MuteInfo() { }

            public MuteInfo(IPlayer target, string adminName, DateTime expireDate, string reason)
            {
                PlayerName = target.Name;
                PlayerId = target.Id;
                Reason = reason;
                Admin = adminName;
                Sent = false;
                ExpireDate = expireDate;
            }
        }

        public class MuteManager
        {
            public MuteInfo AddMute(IPlayer target, string adminName, DateTime expireDate, string reason = null, bool addReason = false)
            {
                MuteInfo info;

                if (Mutes.ContainsKey(target.Id))
                {
                    MuteInfo m = GetMute(target);

                    if (m == null)
                        return null;

                    m.ExpireDate = expireDate;
                    m.Reason = (addReason ? $"{m.Reason} | {reason}" : reason);

                    return null;
                }

                info = new MuteInfo(target, adminName, expireDate, reason);
                Mutes.Add(target.Id, info);

                return info;
            }

            public void RemoveMute(string steamid) => RemoveMute(GetMute(steamid));
            public void RemoveMute(IPlayer player) => RemoveMute(GetMute(player));
            private void RemoveMute(MuteInfo info)
            {
                if (info == null)
                    return;

                Mutes.Remove(info.PlayerId);
            }

            MuteInfo GetMute(IPlayer player) => GetMute(player.Id);
            MuteInfo GetMute(string steamid)
            {
                MuteInfo info;

                if (Mutes.TryGetValue(steamid, out info))
                    return info;
                else
                    return null;
            }

            public bool MuteExists(string steamid) => MuteExists(GetMute(steamid));
            public bool MuteExists(IPlayer player) => MuteExists(GetMute(player));
            private bool MuteExists(MuteInfo info)
            {
                if (info != null)
                    return true;

                return false;
            }

            public bool IsExpired(string steamid) => IsExpired(GetMute(steamid));
            public bool IsExpired(IPlayer player) => IsExpired(GetMute(player));
            private bool IsExpired(MuteInfo info)
            {
                if (info != null)
                    return info.ExpireDate < DateTime.UtcNow;

                return false;
            }

            public bool MessageSent(string steamid) => MessageSent(GetMute(steamid));
            public bool MessageSent(IPlayer player) => MessageSent(GetMute(player));
            private bool MessageSent(MuteInfo info)
            {
                if (info != null)
                    return info.Sent;

                return false;
            }

            public string GetPlayerName(IPlayer player) => GetPlayerName(GetMute(player));
            private string GetPlayerName(MuteInfo info)
            {
                if (info != null)
                    return info.PlayerName;

                return null;
            }

            public string GetPlayerId(IPlayer player) => GetPlayerId(GetMute(player));
            private string GetPlayerId(MuteInfo info)
            {
                if (info != null)
                    return info.PlayerId;

                return null;
            }

            public string GetReason(IPlayer player) => GetReason(GetMute(player));
            private string GetReason(MuteInfo info)
            {
                if (info != null)
                    return info.Reason;

                return null;
            }

            public string GetAdmin(IPlayer player) => GetAdmin(GetMute(player));
            private string GetAdmin(MuteInfo info)
            {
                if (info != null)
                    return info.Admin;

                return null;
            }

            public DateTime? GetExpireDate(IPlayer player) => GetExpireDate(GetMute(player));
            private DateTime? GetExpireDate(MuteInfo info)
            {
                if (info != null)
                    return info.ExpireDate;

                return null;
            }

            public void SetReason(IPlayer player, string reason, bool addReason = false) => SetReason(GetMute(player), reason, addReason);
            private void SetReason(MuteInfo info, string reason, bool addReason = false)
            {
                if (info != null)
                    info.Reason = (addReason ? $"{info.Reason} | {reason}" : reason);
            }

            public void SetAdmin(IPlayer player, IPlayer admin) => SetAdmin(GetMute(player), admin);
            private void SetAdmin(MuteInfo info, IPlayer admin)
            {
                if (info != null)
                    info.Admin = admin.Name;
            }

            public void SetExpireDate(IPlayer player, DateTime expireDate) => SetExpireDate(GetMute(player), expireDate);
            private void SetExpireDate(MuteInfo info, DateTime expireDate)
            {
                if (info != null)
                    info.ExpireDate = expireDate;
            }

            public void SetMessageSent(string steamid, bool sent) => SetMessageSent(GetMute(steamid), sent);
            public void SetMessageSent(IPlayer player, bool sent) => SetMessageSent(GetMute(player), sent);
            private void SetMessageSent(MuteInfo info, bool sent)
            {
                if (info != null)
                    info.Sent = sent;
            }
        }

        private void LoadStoredData()
        {
            LoadData(ref Mutes, mData);
            SaveData(Mutes, mData);
        }

        private void SaveMutes() => timer.Once(1f, () => { SaveData(Mutes, mData); });

        private void SaveData<T>(T data, string filename = null) => MuteData.WriteObject(filename ?? Name, data);
        private void LoadData<T>(ref T data, string filename = null) => data = MuteData.ReadObject<T>(filename ?? Name);

        #endregion Data

        #region Helpers

        private IPlayer GetPlayer(string nameOrID, IPlayer player)
        {
            if (nameOrID.IsSteamId())
            {
                IPlayer result = players.All.ToList().Find((p) => p.Id == nameOrID);

                if (result == null)
                    SendChatMessage(player, Lang("SteamID Not Found", player.Id, nameOrID));

                return result;
            }

            List<IPlayer> foundPlayers = new List<IPlayer>();

            foreach (IPlayer current in players.All)
            {
                if (current.Name.ToLower() == nameOrID.ToLower())
                    return current;

                if (current.Name.ToLower().Contains(nameOrID.ToLower()))
                    foundPlayers.Add(current);
            }

            switch (foundPlayers.Count)
            {
                case 0:
                    SendChatMessage(player, Lang("Player Not Found", player.Id, nameOrID));
                    break;
                case 1:
                    return foundPlayers[0];
                default:
                    string[] names = (from current in foundPlayers select $"- {current.Name}").ToArray();
                    SendChatMessage(player, Lang("Multiple Players Found", player.Id, string.Join("\n", names)));
                    break;
            }
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

            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        private bool BoolConfig(string menu, string dataValue, bool defaultValue) => Convert.ToBoolean(GetConfig(menu, dataValue, defaultValue));

        #endregion Helpers

        #region Messaging

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void SendChatMessage(IPlayer player, string message) => player.Reply(message);

        private void SendBroadcastMessage(string message)
        {
            foreach (IPlayer current in players.Connected)
                SendChatMessage(current, message);
        }

        private void SendInfoMessage(IPlayer player, string prefix, string message) => player.Reply($"[+18][#orange]{prefix}[/#][/+]\n\n{message}");

        #endregion Messaging
    }
}