using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Command Blocker", "Orange", "1.1.0")]
    [Description("Block commands temporarily or permanently")]
    public class TimedCommandBlocker : RustPlugin
    {
        #region Oxide Hooks

        private void Init()
        {
            OnStart();
        }

        private object OnPlayerCommand(ConsoleSystem.Arg arg)
        {
            return CheckCommand(arg);
        }
        
        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            return CheckCommand(arg);
        }

        #endregion

        #region Helpers

        private void OnStart()
        {
            lang.RegisterMessages(EN, this);
        }
        
        private object CheckCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) {return null;}
            if (player.IsAdmin) {return null;}
            var command = arg.cmd.FullName;

            foreach (var a in arg.Args)
            {
                command += $" {a}";
            }
            
            command = command.Replace("chat.say /", string.Empty);
            
            var blocked = 0;

            foreach (var item in config.commands)
            {
                if (command.Contains(item.Key))
                {
                    var blocktime = item.Value;
                    
                    if (blocktime == 0)
                    {
                        blocked = 0;
                        break;
                    }

                    var left = blocktime - Passed(SaveTime());
                    
                    if (left > 0)
                    {
                        blocked = left;
                        break;
                    }

                    blocked = -1;
                }
            }

            if (blocked != -1)
            {
                message(player, "Blocked", command);
                
                if (blocked != 0)
                {
                    message(player, "Unblock", command, blocked, blocked / 60);
                }
                
                return false;
            }

            return null;
        }

        private static double Now()
        {
            return DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
        }

        private int Passed(double a)
        {
            return Convert.ToInt32(Now() - a);
        }
        
        private double SaveTime()
        {
            return SaveRestore.SaveCreatedTime.ToOADate();
        }

        #endregion
        
        #region Config
        
        private ConfigData config;
        
        private class ConfigData
        {    
            [JsonProperty(PropertyName = "Command - time in seconds")]
            public Dictionary<string, int> commands;
        }
        
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                commands = new Dictionary<string, int>
                {
                    ["test"] = 0,
                    ["kit orange"] = 86400,
                }
            };
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
   
            try
            {
                config = Config.ReadObject<ConfigData>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintError("Configuration file is corrupt(or not exists), creating new one!");
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region Localization

        private Dictionary<string, string> EN = new Dictionary<string, string>
        {
            {"Blocked", "Command '{0}' is blocked."},
            {"Unblock", "Unblock time {1} seconds [{2} min]"},
        };

        private void message(BasePlayer p, string key, params object[] args)
        {
            p.ChatMessage(string.Format(lang.GetMessage(key, this, p.UserIDString), args));
        }

        #endregion
    }
}