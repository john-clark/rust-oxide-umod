using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("BetterChat Mute", "LaserHydra", "1.1.2", ResourceId = 118491460)]
    [Description("Simple mute system, made for use with Better Chat")]
    internal class BetterChatMute : CovalencePlugin
    {
        private static Dictionary<string, MuteInfo> _mutes;
        private bool _isDataDirty, _globalMute;
        
        #region Hooks

        private void Loaded()
        {
            permission.RegisterPermission("betterchatmute.permanent", this);

            LoadData(out _mutes);
            SaveData(_mutes);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["No Permission"] = "You don't have permission to use this command.",
                ["No Reason"] = "Unknown reason",
                ["Muted"] = "{player} was muted by {initiator}: {reason}.",
                ["Muted Time"] = "{player} was muted by {initiator} for {time}: {reason}.",
                ["Unmuted"] = "{player} was unmuted by {initiator}.",
                ["Not Muted"] = "{player} is currently not muted.",
                ["Mute Expired"] = "{player} is no longer muted.",
                ["Invalid Time Format"] = "Invalid time format. Example: 1d2h3m4s = 1 day, 2 hours, 3 min, 4 sec",
                ["Nobody Muted"] = "There is nobody muted at the moment.",
                ["Invalid Syntax Mute"] = "/mute <player|steamid> \"[reason]\" [time: 1d1h1m1s]",
                ["Invalid Syntax Unmute"] = "/unmute <player|steamid>",
                ["Player Name Not Found"] = "Could not find player with name '{name}'",
                ["Player ID Not Found"] = "Could not find player with ID '{id}'",
                ["Multiple Players Found"] = "Multiple matching players found: \n{matches}",
                ["Time Muted Player Joined"] = "{player} is temporarily muted. Remaining time: {time}",
                ["Time Muted Player Chat"] = "You may not chat, you are temporarily muted. Remaining time: {time}",
                ["Muted Player Joined"] = "{player} is permanently muted.",
                ["Muted Player Chat"] = "You may not chat, you are permanently muted.",
                ["Global Mute Enabled"] = "Global mute was enabled. Nobody can chat while global mute is active.",
                ["Global Mute Disabled"] = "Global mute was disabled. Everybody can chat again.",
                ["Global Mute Active"] = "Global mute is active, you may not chat."
            }, this);

            timer.Repeat(10, 0, () =>
            {
                List<string> expired = _mutes.Where(m => m.Value.Expired).Select(m => m.Key).ToList();

                foreach (string id in expired)
                {
                    var player = players.FindPlayerById(id);

                    _mutes.Remove(id);
                    PublicMessage("Mute Expired", new KeyValuePair<string, string>("player", player?.Name));

                    Interface.CallHook("OnBetterChatMuteExpired", player);

                    if (!_isDataDirty)
                        _isDataDirty = true;
                }

                if (_isDataDirty)
                {
                    SaveData(_mutes);
                    _isDataDirty = false;
                }
            });
        }

        private object OnUserChat(IPlayer player, string message)
        {
            object result = HandleChat(player);

            if (result is bool && !(bool)result)
            {
                if (!MuteInfo.IsMuted(player) && _globalMute)
                {
                    player.Reply(lang.GetMessage("Global Mute Active", this, player.Id));
                }
                else if (_mutes[player.Id].Timed)
                {
                    player.Reply(
                        lang.GetMessage("Time Muted Player Chat", this, player.Id)
                            .Replace("{time}", FormatTime(_mutes[player.Id].ExpireDate - DateTime.UtcNow))
                   );
                }
                else
                {
                    player.Reply(lang.GetMessage("Muted Player Chat", this, player.Id));
                } 
            }

            return result;
        }

        private object OnBetterChat(Dictionary<string, object> messageData) => HandleChat((IPlayer) messageData["Player"]);

        private void OnUserInit(IPlayer player)
        {
            UpdateMuteStatus(player);

            if (MuteInfo.IsMuted(player))
            {
                if (_mutes[player.Id].Timed)
                    PublicMessage("Time Muted Player Joined",
                        new KeyValuePair<string, string>("player", player.Name), 
                        new KeyValuePair<string, string>("time", FormatTime(_mutes[player.Id].ExpireDate - DateTime.UtcNow)));
                else
                    PublicMessage("Muted Player Joined", new KeyValuePair<string, string>("player", player.Name));
            }
        }

        #endregion

        #region Commands

        [Command("toggleglobalmute"), Permission("betterchatmute.use.global")]
        private void CmdGlobalMute(IPlayer player, string cmd, string[] args)
        {
            _globalMute = !_globalMute;
            PublicMessage(_globalMute ? "Global Mute Enabled" : "Global Mute Disabled");
        }

        [Command("mutelist"), Permission("betterchatmute.use")]
        private void CmdMuteList(IPlayer player, string cmd, string[] args)
        {
            if (_mutes.Count == 0)
                player.Reply(lang.GetMessage("Nobody Muted", this, player.Id));
            else
            {
                player.Reply(string.Join(Environment.NewLine,
                    _mutes.Select(kvp =>
                        $"{players.FindPlayerById(kvp.Key).Name}: {FormatTime(kvp.Value.ExpireDate - DateTime.UtcNow)}"
                    ).ToArray()
                ));
            }
        }

        [Command("mute"), Permission("betterchatmute.use")]
        private void CmdMute(IPlayer player, string cmd, string[] args)
        {
            if (args.Length == 0)
            {
                player.Reply(lang.GetMessage("Invalid Syntax Mute", this, player.Id));
                return;
            }

            string reason = string.Empty;
            TimeSpan? timeSpan = null;

            var target = GetPlayer(args[0], player);

            if (target == null)
                return;
            
            for (var i = 1; i < args.Length; i++)
            {
                if (TryParseTimeSpan(args[i], out timeSpan))
                {
                    args[i] = null;
                    break;
                }
            }
            
            // No time given; make sure user has permanent muting permission
            if (timeSpan == null && !permission.UserHasPermission(player.Id, "betterchatmute.permanent") && player.Id != "server_console")
            {
                player.Reply(lang.GetMessage("No Permission", this, player.Id));
                return;
            }

            reason = string.Join(" ", args.Skip(1).Where(a => a != null).ToArray());
            reason = string.IsNullOrEmpty(reason) ? lang.GetMessage("No Reason", this) : reason;

            var expireDate = timeSpan == null ? MuteInfo.NonTimedExpireDate : DateTime.UtcNow + (TimeSpan) timeSpan;

            _mutes[target.Id] = new MuteInfo(expireDate, reason);
            SaveData(_mutes);

            if (timeSpan == null)
            {
                Interface.CallHook("OnBetterChatMuted", target, player, reason);

                PublicMessage("Muted",
                    new KeyValuePair<string, string>("initiator", player.Name),
                    new KeyValuePair<string, string>("player", target.Name),
                    new KeyValuePair<string, string>("reason", reason));
            }
            else
            {
                Interface.CallHook("OnBetterChatTimeMuted", target, player, (TimeSpan) timeSpan, reason);

                PublicMessage("Muted Time",
                    new KeyValuePair<string, string>("initiator", player.Name),
                    new KeyValuePair<string, string>("player", target.Name),
                    new KeyValuePair<string, string>("time", FormatTime((TimeSpan) timeSpan)),
                    new KeyValuePair<string, string>("reason", reason));
            }
        }

        [Command("unmute"), Permission("betterchatmute.use")]
        private void CmdUnmute(IPlayer player, string cmd, string[] args)
        {
            if (args.Length != 1)
            {
                player.Reply(lang.GetMessage("Invalid Syntax Unmute", this, player.Id));
                return;
            }

            IPlayer target = GetPlayer(args[0], player);

            if (target == null)
                return;

            if (!MuteInfo.IsMuted(target))
            {
                player.Reply(lang.GetMessage("Not Muted", this, player.Id).Replace("{player}", target.Name));
                return;
            }

            _mutes.Remove(target.Id);
            SaveData(_mutes);

            Interface.CallHook("OnBetterChatUnmuted", target, player);

            PublicMessage("Unmuted",
                new KeyValuePair<string, string>("initiator", player.Name),
                new KeyValuePair<string, string>("player", target.Name));
        }

        #endregion

        #region API Methods

        private void API_Mute(IPlayer target, IPlayer player, string reason = "", bool callHook = true, bool broadcast = true)
        {
            _mutes[target.Id] = new MuteInfo(MuteInfo.NonTimedExpireDate, reason);
            SaveData(_mutes);

            reason = string.IsNullOrEmpty(reason) ? lang.GetMessage("No Reason", this) : reason;

            if (callHook)
                Interface.CallHook("OnBetterChatMuted", target, player, reason);

            if (broadcast)
            {
                PublicMessage("Muted",
                    new KeyValuePair<string, string>("initiator", player.Name),
                    new KeyValuePair<string, string>("player", target.Name),
                    new KeyValuePair<string, string>("reason", reason));
            }
        }

        private void API_TimeMute(IPlayer target, IPlayer player, TimeSpan timeSpan, string reason = "", bool callHook = true, bool broadcast = true)
        {
            _mutes[target.Id] = new MuteInfo(DateTime.UtcNow + timeSpan, reason);
            SaveData(_mutes);

            reason = string.IsNullOrEmpty(reason) ? lang.GetMessage("No Reason", this) : reason;

            if (callHook)
                Interface.CallHook("OnBetterChatTimeMuted", target, player, timeSpan,
                    string.IsNullOrEmpty(reason) ? lang.GetMessage("No Reason", this) : reason);

            if (broadcast)
            {
                PublicMessage("Muted Time",
                    new KeyValuePair<string, string>("initiator", player.Name),
                    new KeyValuePair<string, string>("player", target.Name),
                    new KeyValuePair<string, string>("time", FormatTime(timeSpan)),
                    new KeyValuePair<string, string>("reason", reason));
            }
        }

        private bool API_Unmute(IPlayer target, IPlayer player, bool callHook = true, bool broadcast = true)
        {
            if (!MuteInfo.IsMuted(target))
                return false;

            _mutes.Remove(target.Id);
            SaveData(_mutes);

            if (callHook)
                Interface.CallHook("OnBetterChatUnmuted", target, player);

            if (broadcast)
            {
                PublicMessage("Unmuted",
                    new KeyValuePair<string, string>("initiator", player.Name),
                    new KeyValuePair<string, string>("player", target.Name));
            }

            return true;
        }

        private void API_SetGlobalMuteState(bool state, bool broadcast = true)
        {
            _globalMute = state;

            if (broadcast)
                PublicMessage(_globalMute ? "Global Mute Enabled" : "Global Mute Disabled");
        }

        private bool API_GetGlobalMuteState() => _globalMute;

        private bool API_IsMuted(IPlayer player) => _mutes.ContainsKey(player.Id);

        private List<string> API_GetMuteList() => _mutes.Keys.ToList();

        #endregion

        #region Helpers

        private void PublicMessage(string key, params KeyValuePair<string, string>[] replacements)
        {
            var message = lang.GetMessage(key, this);

            foreach (var replacement in replacements)
                message = message.Replace($"{{{replacement.Key}}}", replacement.Value);

            server.Broadcast(message);
            Puts(message);
        }

        private object HandleChat(IPlayer player)
        {
            UpdateMuteStatus(player);

            var result = Interface.CallHook("OnBetterChatMuteHandle", player, MuteInfo.IsMuted(player) ? JObject.FromObject(_mutes[player.Id]) : null);

            if (result != null)
                return null;

            if (MuteInfo.IsMuted(player))
                return false;

            if (_globalMute && !permission.UserHasPermission(player.Id, "betterchatmute.use.global"))
                return false;

            return null;
        }

        private void UpdateMuteStatus(IPlayer player)
        {
            if (MuteInfo.IsMuted(player) && _mutes[player.Id].Expired)
            {
                _mutes.Remove(player.Id);
                SaveData(_mutes);

                PublicMessage("Mute Expired", new KeyValuePair<string, string>("player", players.FindPlayerById(player.Id)?.Name));

                Interface.CallHook("OnBetterChatMuteExpired", player);
            }
        }

        private IPlayer GetPlayer(string nameOrId, IPlayer requestor)
        {
            if (nameOrId.IsSteamId())
            {
                IPlayer player = players.All.ToList().Find(p => p.Id == nameOrId);

                if (player == null)
                    requestor.Reply(lang.GetMessage("Player ID Not Found", this, requestor.Id).Replace("{id}", nameOrId));

                return player;
            }

            List<IPlayer> foundPlayers = new List<IPlayer>();

            foreach (var player in players.Connected)
            {
                if (string.Equals(player.Name, nameOrId, StringComparison.CurrentCultureIgnoreCase))
                    return player;

                if (player.Name.ToLower().Contains(nameOrId.ToLower()))
                    foundPlayers.Add(player);
            }

            switch (foundPlayers.Count)
            {
                case 0:
                    requestor.Reply(lang.GetMessage("Player Name Not Found", this, requestor.Id).Replace("{name}", nameOrId));
                    break;

                case 1:
                    return foundPlayers[0];

                default:
                    var names = (from current in foundPlayers select current.Name).ToArray();
                    requestor.Reply(lang.GetMessage("Multiple Players Found", this, requestor.Id).Replace("{matches}", string.Join(", ", names)));
                    break;
            }

            return null;
        }

        #region DateTime Helper

        private static string FormatTime(TimeSpan time)
        {
            var values = new List<string>();

            if (time.Days != 0)
                values.Add($"{time.Days} day(s)");

            if (time.Hours != 0)
                values.Add($"{time.Hours} hour(s)");

            if (time.Minutes != 0)
                values.Add($"{time.Minutes} minute(s)");

            if (time.Seconds != 0)
                values.Add($"{time.Seconds} second(s)");

            return values.ToSentence();
        }

        private static bool TryParseTimeSpan(string source, out TimeSpan? timeSpan)
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

            source = source.Replace(seconds + "s", string.Empty);
            source = source.Replace(minutes + "m", string.Empty);
            source = source.Replace(hours + "h", string.Empty);
            source = source.Replace(days + "d", string.Empty);

            if (!string.IsNullOrEmpty(source) || !(s.Success || m.Success || h.Success || d.Success))
            {
                timeSpan = null;
                return false;
            }

            timeSpan = new TimeSpan(days, hours, minutes, seconds);
            return true;
        }

        #endregion

        #region Data & Config Helper

        private string DataFileName => Title.Replace(" ", "");

        private void LoadData<T>(out T data, string filename = null) => data = Interface.Oxide.DataFileSystem.ReadObject<T>(filename ?? DataFileName);

        private void SaveData<T>(T data, string filename = null) => Interface.Oxide.DataFileSystem.WriteObject(filename ?? DataFileName, data);

        #endregion

        #endregion
        
        #region Classes

        public class MuteInfo
        {
            public DateTime ExpireDate = DateTime.MinValue;

            [JsonIgnore]
            public bool Timed => ExpireDate != DateTime.MinValue;

            [JsonIgnore]
            public bool Expired => Timed && ExpireDate < DateTime.UtcNow;

            public string Reason { get; set; }

            public static bool IsMuted(IPlayer player) => _mutes.ContainsKey(player.Id);

            public static readonly DateTime NonTimedExpireDate = DateTime.MinValue;

            public MuteInfo()
            {
            }

            public MuteInfo(DateTime expireDate, string reason)
            {
                ExpireDate = expireDate;
                Reason = reason;
            }
        }

        #endregion
    }
}