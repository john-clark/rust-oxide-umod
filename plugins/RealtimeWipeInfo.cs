// Reference: Facepunch.Sqlite

using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Realtime Wipe Info", "Ryan", "2.1.5")]
    class RealtimeWipeInfo : RustPlugin
    {
        #region Declaration

        // Plugin references
        [PluginReference] private Plugin BetterChat;

        // Datetimes
        private DateTime CachedWipeTime;
        private DateTime Epoch = new DateTime(1970, 1, 1);

        // Permissions
        private const string BypassPerm = "realtimewipeinfo.chatbypass";

        // Timers
        private Timer DescriptionTimer;
        private Timer TitleTimer;

        // Configuration and data
        private static ConfigFile CFile;
        private static DataFile DFile;

        // Instance
        private static RealtimeWipeInfo Instance;

        // Other variables
        private bool NewConfig;

        #endregion

        #region Configuration

        private class ConfigFile
        {
            [JsonProperty("Description Settings")]
            public DescriptionSettings Description;

            [JsonProperty("Title Settings")]
            public TitleSettings Title;

            [JsonProperty("Phrase Settings")]
            public PhraseSettings Phrase;

            [JsonProperty("Connect Message Settings")]
            public ConnectSettings Connect;

            [JsonProperty("Command Settings")]
            public CommandSettings Command;

            [JsonProperty("Blueprint settings")]
            public BlueprintSettings Blueprint;

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile()
                {
                    Description = new DescriptionSettings
                    {
                        Enabled = false,
                        Description = "Your description here. Put {0} where you want plugin info to go",
                        UseTime = true,
                        Date = new Date
                        {
                            Enabled = true,
                            Format = "dddd d/M"
                        },
                        Refresh = 120
                    },
                    Title = new TitleSettings
                    {
                        Enabled = false,
                        Title = "Your title here. Put {0} where you want plugin info to go",
                        UseTime = true,
                        Date = new Date
                        {
                            Enabled = true,
                            Format = "d/M"
                        },
                        Refresh = 60
                    },
                    Phrase = new PhraseSettings
                    {
                        Enabled = false,
                        Phrases = new Dictionary<string, PhraseItem>
                        {
                            ["wipe"] = new PhraseItem(true, true),
                            ["wipe?"] = new PhraseItem(true, false),
                            ["wiped"] = new PhraseItem(false, true),
                            ["wiped?"] = new PhraseItem(false, false)
                        },
                        UseTime = true,
                        Date = new Date
                        {
                            Enabled = true,
                            Format = "d/M"
                        },
                        Schedule = new ScheduleSettings
                        {
                            Enabled = true,
                            Schedule = 7,
                            Format = "dddd d/M"
                        }
                    },
                    Connect = new ConnectSettings
                    {
                        Enabled = false
                    },
                    Command = new CommandSettings()
                    {
                        Enabled = true,
                        Command = "wipe"
                    },
                    Blueprint = new BlueprintSettings()
                };
            }
        }

        #region Config Classes

        private class DescriptionSettings
        {
            [JsonProperty("Enable Description")]
            public bool Enabled;

            [JsonProperty("Full Server Description")]
            public string Description;

            [JsonProperty("Include Seed & Map Size")]
            public bool SeedSize;

            [JsonProperty("Enable Use Of Time")]
            public bool UseTime;

            public Date Date;

            [JsonProperty("Refresh Interval")]
            public float Refresh;
        }

        private class TitleSettings
        {
            [JsonProperty("Enable Title")]
            public bool Enabled;

            [JsonProperty("Full Server Hostname")]
            public string Title;

            [JsonProperty("Enable Use Of Time")]
            public bool UseTime;

            public Date Date;

            [JsonProperty("Refresh Interval")]
            public float Refresh;
        }

        private class PhraseSettings
        {
            [JsonProperty("Enable Phrases")]
            public bool Enabled;

            public Dictionary<string, PhraseItem> Phrases;

            [JsonProperty("Enable Use Of Time")]
            public bool UseTime;

            public Date Date;

            [JsonProperty("Schedule Settings")]
            public ScheduleSettings Schedule;
        }

        private class ConnectSettings
        {
            [JsonProperty("Enable Connect Messages")]
            public bool Enabled;
        }

        private class CommandSettings
        {
            public bool Enabled;
            public string Command;
        }

        private class Date
        {
            [JsonProperty("Enable Use Of Date")]
            public bool Enabled;

            [JsonProperty("Date format")]
            public string Format;
        }

        private class ScheduleSettings
        {
            [JsonProperty("Enable Wipe Schedule Messages")]
            public bool Enabled;

            [JsonProperty("Wipe Schedule In Days")]
            public int Schedule;

            [JsonProperty("Date Format")]
            public string Format;
        }

        private class PhraseItem
        {
            [JsonProperty("Send Reply")]
            public bool Message;

            [JsonProperty("Block Message")]
            public bool Block;

            public PhraseItem(bool message, bool block)
            {
                Message = message;
                Block = block;
            }
        }

        private class BlueprintSettings
        {
            [JsonProperty("Enable blueprint wipe tracking")]
            public bool Enabled;

            [JsonProperty("Add BP wipe to description")]
            public bool UseDescription;

            [JsonProperty("Use BP chat reply")]
            public bool UseChat;

            public BlueprintSettings()
            {
                Enabled = true;
                UseDescription = true;
                UseChat = true;
            }
        }

        #endregion

        protected override void LoadDefaultConfig()
        {
            PrintWarning($"All values are disabled by default, set them up at oxide/config/{Name}.json!");
            NewConfig = true;
            CFile = ConfigFile.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                CFile = Config.ReadObject<ConfigFile>();
                if (CFile == null)
                {
                    Regenerate();
                    return;
                }
                if (CFile.Blueprint == null)
                {
                    CFile.Blueprint = new BlueprintSettings();
                    SaveConfig();
                }
            }
            catch { Regenerate(); }
        }

        protected override void SaveConfig() => Config.WriteObject(CFile);

        private void Regenerate()
        {
            PrintWarning($"Configuration file at 'oxide/config/{Name}.json' seems to be corrupt! Regenerating...");
            CFile = ConfigFile.DefaultConfig();
            SaveConfig();
        }

        #endregion

        #region Lang

        private struct Msg
        {
            public const string TitleDay = "TitleDay";
            public const string TitleDays = "TitleDays";
            public const string TitleHour = "TitleHour";
            public const string TitleHours = "TitleHours";
            public const string TitleMinutes = "TitleMinutes";
            public const string DescLastWipe = "DescLastWipe";
            public const string DescNextWipe = "DescNextWipe";
            public const string DescSeedSize = "DescSeedSize";
            public const string MsgTime = "MsgTime";
            public const string MsgDate = "MsgDate";
            public const string MsgDateTime = "MsgDateTime";
            public const string MsgNextWipe = "MsgNextWipe";
            public const string DescBpWipe = "DescBpWipe";
            public const string DescMsgBpWipe = "DescMsgBpWipe";
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Msg.TitleDay] = "{0} day ago",
                [Msg.TitleDays] = "{0} days ago",
                [Msg.TitleHour] = "{0} hour ago",
                [Msg.TitleHours] = "{0} hrs ago",
                [Msg.TitleMinutes] = "{0} mins ago",
                [Msg.DescLastWipe] = "The last wipe was on {0}",
                [Msg.DescNextWipe] = "The next wipe will be on {0} ({1} day wipe schedule)",
                [Msg.DescSeedSize] = "The map size is {0} and the seed is {1}",
                [Msg.MsgTime] = "The last wipe was {0} ago",
                [Msg.MsgDate] = "The last wipe was on {0}",
                [Msg.MsgDateTime] = "The last wipe was on {0} ({1} ago)",
                [Msg.MsgNextWipe] = "The next wipe will be on <color=orange>{0}</color> (<color=orange>{1}</color> day wipe schedule)",
                ["DayFormat"] = "<color=orange>{0}</color> day and <color=orange>{1}</color> hours",
                ["DaysFormat"] = "<color=orange>{0}</color> days and <color=orange>{1}</color> hours",
                ["HourFormat"] = "<color=orange>{0}</color> hour and <color=orange>{1}</color> minutes",
                ["HoursFormat"] = "<color=orange>{0}</color> hours and <color=orange>{1}</color> minutes",
                ["MinFormat"] = "<color=orange>{0}</color> minute and <color=orange>{1}</color> seconds",
                ["MinsFormat"] = "<color=orange>{0}</color> minutes and <color=orange>{1}</color> seconds",
                ["SecsFormat"] = "<color=orange>{0}</color> seconds",
                [Msg.DescBpWipe] = "(BP wiped {0})",
                [Msg.DescMsgBpWipe] = "(Blueprints wiped <color=orange>{0}</color>)"
            }, this);
        }

        #endregion

        #region Data

        private class DataFile
        {
            public string Hostname;
            public string Description;
            public DateTime BlueprintWipe;

            public DataFile()
            {
                Hostname = "";
                Description = "";
                BlueprintWipe = DateTime.MinValue;
            }

            public DataFile(string hostname, string description)
            {
                Hostname = hostname;
                Description = description;
            }
        }

        #endregion

        #region Methods

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private string GetFormattedTime(double time)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(time);
            if (timeSpan.TotalSeconds < 1) return null;

            if (Math.Floor(timeSpan.TotalDays) >= 1)
                return string.Format(timeSpan.Days > 1 ? Lang("DaysFormat", null, timeSpan.Days, timeSpan.Hours) : Lang("DayFormat", null, timeSpan.Days, timeSpan.Hours));
            if (Math.Floor(timeSpan.TotalMinutes) >= 60)
                return string.Format(timeSpan.Hours > 1 ? Lang("HoursFormat", null, timeSpan.Hours, timeSpan.Minutes) : Lang("HourFormat", null, timeSpan.Hours, timeSpan.Minutes));
            if (Math.Floor(timeSpan.TotalSeconds) >= 60)
                return string.Format(timeSpan.Minutes > 1 ? Lang("MinsFormat", null, timeSpan.Minutes, timeSpan.Seconds) : Lang("MinFormat", null, timeSpan.Minutes, timeSpan.Seconds));
            return Lang("SecsFormat", null, timeSpan.Seconds);
        }

        #region Title Methods

        private void ApplyTitle(string title) => ConVar.Server.hostname = string.Format(CFile.Title.Title, title);

        private void StartTitleRefresh()
        {
            ApplyTitle(GetFormattedTitle());
            timer.Every(CFile.Title.Refresh, () =>
            {
                ApplyTitle(GetFormattedTitle());
            });
        }

        private string GetFormattedTitleTime()
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds((DateTime.UtcNow.ToLocalTime() - CachedWipeTime).TotalSeconds);
            if (timeSpan.TotalSeconds < 1) return null;

            if (Math.Floor(timeSpan.TotalDays) >= 1)
                return string.Format(timeSpan.Days > 1 ? Lang(Msg.TitleDays, null, timeSpan.Days) : Lang(Msg.TitleDay, null, timeSpan.Days));
            if (Math.Floor(timeSpan.TotalMinutes) >= 60)
                return string.Format(timeSpan.Hours > 1 ? Lang(Msg.TitleHours, null, timeSpan.Hours) : Lang(Msg.TitleHour, null, timeSpan.Hours));
            return Lang(Msg.TitleMinutes, null, timeSpan.Minutes);
        }

        private string GetFormattedTitle()
        {
            if (CFile.Title.UseTime && !CFile.Title.Date.Enabled)
            {
                return GetFormattedTitleTime();
            }
            if (CFile.Title.Date.Enabled && !CFile.Title.UseTime)
            {
                return CachedWipeTime.ToString(CFile.Title.Date.Format);
            }
            if (CFile.Title.Date.Enabled && CFile.Title.UseTime)
            {
                return CachedWipeTime.ToString(CFile.Title.Date.Format) + " " + GetFormattedTitleTime();
            }
            return string.Empty;
        }

        #endregion

        #region Description Methods

        private void ApplyDescription(string description) => ConVar.Server.description = description;

        private void StartDescriptionRefresh()
        {
            ApplyDescription(GetFormattedDescription());
            timer.Every(CFile.Description.Refresh, () =>
            {
                ApplyDescription(GetFormattedDescription());
            });
        }

        private string GetFormattedDescription()
        {
            var output = "";
            if (CFile.Phrase.Schedule.Enabled)
            {
                output = string.Format(Lang(Msg.DescLastWipe, null, CachedWipeTime.ToString(CFile.Description.Date.Format)) + "\n" +
                    Lang(Msg.DescNextWipe, null, CachedWipeTime.AddDays(CFile.Phrase.Schedule.Schedule)
                    .ToString(CFile.Description.Date.Format), CFile.Phrase.Schedule.Schedule));
            }
            else
            {
                output = Lang(Msg.DescLastWipe, null, CachedWipeTime.ToString(CFile.Description.Date.Format));
            }
            if (CFile.Description.SeedSize)
            {
                output += "\n" + Lang(Msg.DescSeedSize, null, ConVar.Server.worldsize, ConVar.Server.seed);
            }
            if (CFile.Blueprint.Enabled)
            {
                output += " " + Lang(Msg.DescBpWipe, null, DFile.BlueprintWipe.ToLocalTime().ToString(CFile.Description.Date.Format));
            }
            return string.Format(CFile.Description.Description, output);
        }

        #endregion

        #region Phrase Methods

        private object ChatMessageResult(BasePlayer player, string input, bool reply)
        {
            if (!CFile.Phrase.Enabled) return null;
            foreach (var phrase in CFile.Phrase.Phrases)
            {
                if (input.ToLower().Contains(phrase.Key.ToLower()))
                {
                    if (phrase.Value.Message && reply)
                    {
                        PrintToChat(player, GetFormattedMessage(player));
                    }
                    if (phrase.Value.Block)
                    {
                        return false;
                    }
                    return null;
                }
            }
            return null;
        }

        private string GetFormattedMessageTime() => GetFormattedTime((DateTime.UtcNow.ToLocalTime() - CachedWipeTime).TotalSeconds);

        private string GetFormattedMessage(BasePlayer player)
        {
            var addition = string.Empty;
            if (CFile.Blueprint.Enabled) addition = " " + Lang(Msg.DescMsgBpWipe, player.UserIDString, DFile.BlueprintWipe.ToLocalTime().ToString(CFile.Phrase.Date.Format));
            if (CFile.Phrase.UseTime && !CFile.Phrase.Date.Enabled)
            {
                var output = Lang(Msg.MsgTime, player.UserIDString, GetFormattedMessageTime());
                if (CFile.Phrase.Schedule.Enabled)
                    output += "\n" + Lang(Msg.MsgNextWipe, player.UserIDString, CachedWipeTime.AddDays(CFile.Phrase.Schedule.Schedule).ToString(CFile.Phrase.Schedule.Format),
                                  CFile.Phrase.Schedule.Schedule);
                return output + addition;
            }
            if (CFile.Phrase.Date.Enabled && !CFile.Phrase.UseTime)
            {
                var output = Lang(Msg.MsgDate, player.UserIDString, CachedWipeTime.ToString(CFile.Phrase.Date.Format));
                if (CFile.Phrase.Schedule.Enabled)
                {
                    output += Lang(Msg.MsgNextWipe, player.UserIDString, CachedWipeTime.AddDays(CFile.Phrase.Schedule.Schedule).ToString(CFile.Phrase.Schedule.Format),
                        CFile.Phrase.Schedule.Schedule);
                }
                return output + addition;
            }
            if (CFile.Phrase.Date.Enabled && CFile.Phrase.UseTime)
            {
                var output = Lang(Msg.MsgDateTime, player.UserIDString, CachedWipeTime.ToString(CFile.Phrase.Date.Format), GetFormattedMessageTime());
                if (CFile.Phrase.Schedule.Enabled)
                {
                    output += "\n" + Lang(Msg.MsgNextWipe, player.UserIDString, CachedWipeTime.AddDays(CFile.Phrase.Schedule.Schedule).ToString(CFile.Phrase.Schedule.Format),
                                  CFile.Phrase.Schedule.Schedule);
                }
                return output + addition;
            }
            return null;
        }

        #endregion

        private void OnBpsWiped()
        {
            PrintWarning("Blueprint wipe detected!");
            Interface.Oxide.CallHook("OnUsersCleared");
            Interface.Oxide.DataFileSystem.WriteObject(Name, DFile);
        }

        #endregion

        #region Hooks

        private void Init()
        {
            DFile = Interface.Oxide.DataFileSystem.ReadObject<DataFile>(Name);
            permission.RegisterPermission(BypassPerm, this);
            cmd.AddChatCommand(CFile.Command.Command, this, WipeCommand);
            Instance = this;
            if (CFile.Phrase.Enabled && BetterChat && BetterChat.Version < new VersionNumber(5, 0, 6))
            {
                PrintWarning($"This plugin is only compatible with BetterChat version 5.0.6 or greater!");
                Unsubscribe("OnBetterChat");
            }
            if (NewConfig)
            {
                PrintWarning("Saved your current hostname and description to apply at a later date if needed");
                DFile = new DataFile(ConVar.Server.hostname, ConVar.Server.description);
                Interface.Oxide.DataFileSystem.WriteObject(Name, DFile);
            }
        }

        private void OnServerInitialized()
        {
            CachedWipeTime = SaveRestore.SaveCreatedTime.ToLocalTime();
            if (CFile.Blueprint.Enabled)
            {
                var blueprints = UserPersistance.blueprints;
                var playerCount = blueprints?.QueryInt("SELECT COUNT(*) FROM data");
                if (playerCount != null && playerCount == 0)
                {
                    DFile.BlueprintWipe = DateTime.UtcNow;
                    OnBpsWiped();
                }
                if (DFile.BlueprintWipe == null || DFile.BlueprintWipe == DateTime.MinValue)
                {
                    DFile.BlueprintWipe = SaveRestore.SaveCreatedTime;
                    OnBpsWiped();
                }
            }
            if (!CFile.Phrase.Enabled)
            {
                Unsubscribe("OnPlayerChat");
                Unsubscribe("OnBetterChat");
            }
            if (!CFile.Connect.Enabled)
            {
                Unsubscribe("OnPlayerInit");
            }
            if (CFile.Description.Enabled)
            {
                if (CFile.Description.UseTime)
                    StartDescriptionRefresh();
                else
                    ApplyDescription(GetFormattedDescription());
            }
            if (CFile.Title.Enabled)
            {
                if (CFile.Title.UseTime)
                    StartTitleRefresh();
                else
                    ApplyTitle(GetFormattedTitle());
            }
        }

        private void Unload()
        {
            TitleTimer?.Destroy();
            DescriptionTimer?.Destroy();
            if (!ConVar.Admin.ServerInfo().Restarting)
            {
                PrintWarning($"Setting servers hostname and description to the originally stored ones in oxide/data/{Name}.json");
                if(CFile.Title.Enabled) ConVar.Server.hostname = DFile.Hostname;
                if(CFile.Description.Enabled) ConVar.Server.description = DFile.Description;
            }
        }

        private void OnPlayerInit(BasePlayer player)
        {
            timer.Once(3, () =>
            {
                PrintToChat(player, GetFormattedMessage(player));
            });
        }

        private object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!permission.UserHasPermission(player.UserIDString, BypassPerm) && !player.IsAdmin)
            {
                return ChatMessageResult(player, arg.FullString, true);
            }
            return null;
        }

        private object OnBetterChat(Dictionary<string, object> data)
        {
            var player = (IPlayer)data["Player"];
            if (!player.HasPermission(BypassPerm) && !player.IsAdmin)
            {
                return ChatMessageResult((BasePlayer)player.Object, data["Text"].ToString(), false);
            }
            return null;
        }

        #endregion

        #region Commands

        private void WipeCommand(BasePlayer player, string command, string[] args)
        {
            PrintToChat(player, GetFormattedMessage(player));
        }

        #endregion
    }
}