﻿using System;
using System.Collections.Generic;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("WipeSchedule", "k1lly0u", "2.0.4", ResourceId = 1451)]
    class WipeSchedule : RustPlugin
    {
        #region Fields
        DateTime NextWipeDate;
        Timer announceTimer;
        #endregion

        #region Oxide Hooks 
        void Loaded()
        {
            lang.RegisterMessages(messages, this);
            LoadVariables();
        }
        void OnServerInitialized()
        {
            if (!configData.UseManualNextWipe)
                UpdateWipeDates();
            else LoadWipeDates();

            if (configData.AnnounceOnTimer)
            {
                announceTimer = timer.Repeat((configData.AnnounceTimer * 60) * 60, 0, ()=> BroadcastWipe()); 
            }
        }
        void OnPlayerInit(BasePlayer player)
        {
            if (configData.AnnounceOnJoin)
            {
                cmdNextWipe(player, "", new string[0]);
            }
        }
        void Unload()
        {
            if (announceTimer != null)
                announceTimer.Destroy();
        }
        #endregion

        #region Functions        
        private DateTime ParseTime(string time) => DateTime.ParseExact(time, configData.DateFormat, CultureInfo.InvariantCulture);
        private void UpdateWipeDates()
        {
            var lastWipe = ParseTime(configData.LastWipe);
            NextWipeDate = lastWipe.AddDays(configData.DaysBetweenWipes);            
        }
        private void LoadWipeDates()
        {
            NextWipeDate = ParseTime(configData.NextWipe);
        }
        private string NextWipeDays(DateTime WipeDate)
        {            
            TimeSpan t = WipeDate.Subtract(DateTime.Now);
            return string.Format(string.Format("{0:D2} Days",t.Days));
        }
        private void BroadcastWipe()
        {
            PrintToChat(string.Format(MSG("lastMapWipe", null), configData.LastWipe, NextWipeDays(NextWipeDate)));           
        }
        #endregion

        #region ChatCommands
        [ChatCommand("nextwipe")]
        private void cmdNextWipe(BasePlayer player, string command, string[] args)
        {
            SendReply(player, string.Format(MSG("lastMapWipe", player.UserIDString), configData.LastWipe, NextWipeDays(NextWipeDate)));            
        }
        [ChatCommand("setwipe")]
        private void cmdSetWipe(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            if (args == null || args.Length == 0)
            {
                SendReply(player, $"<color=#ffae1a>/setwipe</color>{MSG("setWipeMap", player.UserIDString)}");
                SendReply(player, $"<color=#ffae1a>/setwipe <date></color>{MSG("setWipeMapManual", player.UserIDString)}");
                return;
            }
            if (args.Length == 1)
            {
                configData.LastWipe = DateTime.Now.Date.ToString(configData.DateFormat);
                SaveConfig(configData);
                UpdateWipeDates();
                SendReply(player, string.Format(MSG("savedWipeMap", player.UserIDString), configData.LastWipe));
            }
            if (args.Length == 2)
            {
                DateTime time;
                if (DateTime.TryParse(args[1], out time))
                {
                    configData.LastWipe = time.ToString(configData.DateFormat);
                    SaveConfig(configData);
                    UpdateWipeDates();
                    SendReply(player, string.Format(MSG("savedWipeMap", player.UserIDString), configData.LastWipe));
                }
            }
        }

        [ConsoleCommand("setwipe")]
        private void ccmdSetWipe(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
            {
                if (arg.Args == null || arg.Args.Length == 0)
                {
                    SendReply(arg, $"setwipe {MSG("setWipeMap", null)}");
                    
                    return;
                }
                if (arg.Args.Length == 1)
                {
                    configData.LastWipe = DateTime.Now.Date.ToString(configData.DateFormat);
                    SaveConfig(configData);
                    UpdateWipeDates();
                    SendReply(arg, string.Format(MSG("savedWipeMap", null), configData.LastWipe));
                }                
            }
        }
        [ConsoleCommand("getwipe")]
        private void ccmdGetWipe(ConsoleSystem.Arg arg)
        {
            SendReply(arg, string.Format(MSG("lastMapWipe"), configData.LastWipe, NextWipeDays(NextWipeDate)));
        }
        [ChatCommand("setnextwipe")]
        private void cmdSetNextWipe(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            if (args == null || args.Length == 0)
            {                
                SendReply(player, $"<color=#ffae1a>/setnextwipe <date></color>{MSG("setNextWipeMapManual", player.UserIDString)}");
                return;
            }            
            if (args.Length == 2)
            {
                DateTime time;
                if (DateTime.TryParse(args[1], out time))
                {
                    configData.NextWipe = time.ToString(configData.DateFormat);
                    SaveConfig(configData);
                    LoadWipeDates();
                    SendReply(player, string.Format(MSG("savedNextWipeMap", player.UserIDString), configData.NextWipe));
                }
            }
        }

        [ConsoleCommand("setnextwipe")]
        private void ccmdSetNextWipe(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
            {
                if (arg.Args == null || arg.Args.Length == 0)
                {
                    SendReply(arg, $"setnextwipe <date>{MSG("setNextWipeMapManual", null)}");

                    return;
                }
                if (arg.Args.Length == 2)
                {
                    DateTime time;
                    if (DateTime.TryParse(arg.Args[1], out time))
                    {
                        configData.NextWipe = time.ToString(configData.DateFormat);
                        SaveConfig(configData);
                        LoadWipeDates();
                        SendReply(arg, string.Format(MSG("savedNextWipeMap"), configData.NextWipe));
                    }
                    return;
                }
            }
        }
        #endregion

        #region Config        
        private ConfigData configData;
        class ConfigData
        {
            public string DateFormat { get; set; }
            public int DaysBetweenWipes { get; set; }
            public string LastWipe { get; set; }
            public string NextWipe { get; set; }
            public bool AnnounceOnJoin { get; set; }
            public bool UseManualNextWipe { get; set; }
            public bool AnnounceOnTimer { get; set; }
            public int AnnounceTimer { get; set; }
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            if (string.IsNullOrEmpty(configData.LastWipe))
                configData.LastWipe = DateTime.Now.ToString(configData.DateFormat);           
            if (string.IsNullOrEmpty(configData.NextWipe))
                configData.NextWipe = DateTime.Now.ToString(configData.DateFormat);           
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                AnnounceOnJoin = true,
                AnnounceOnTimer = true,
                AnnounceTimer = 3,
                DateFormat = "MM/dd/yyyy",
                DaysBetweenWipes = 14,
                LastWipe = "",
                UseManualNextWipe = false,
                NextWipe = ""               
            };            
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Messaging
        private string MSG(string key, string playerid = null) => lang.GetMessage(key, this, playerid);

        Dictionary<string, string> messages = new Dictionary<string, string>()
        {            
            {"lastMapWipe", "<color=#b3b3b3>Last Map Wipe:</color> <color=#ffae1a>{0}</color> <color=#b3b3b3>Time Until Next Map Wipe:</color> <color=#ffae1a>{1}</color>" },
            {"setWipeMap", "<color=#b3b3b3> - Sets the current time as last map wipe</color>" },
            {"savedWipeMap", "<color=#b3b3b3>Successfully set last map wipe to:</color> <color=#ffae1a>{0}</color>" },
            {"setWipeMapManual", "<color=#b3b3b3> - Set the time of last map wipe. Format: MM/dd/yyyy</color>" },
            {"savedNextWipeMap", "<color=#b3b3b3>Successfully set next map wipe to:</color> <color=#ffae1a>{0}</color>" },
            {"setNextWipeMapManual", "<color=#b3b3b3> - Set the time of next map wipe. Format: MM/dd/yyyy</color>" }
        };
        #endregion
    }
}