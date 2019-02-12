using System;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("UCompass", "Nimant", "1.0.3", ResourceId = 2583)]
    class UCompass : RustPlugin
    {                    
     
        #region Variables 
        
        private HashSet<BasePlayer> ShowCompass = new HashSet<BasePlayer>();
        private Vector3 DefaultPos = new Vector3(0f, 0f, 0f);
        private string CompassPermission = "ucompass.allow";
        private ConfigData configData;        
        
        #endregion                
        
        #region Init
        
        private void Init()
        {
            permission.RegisterPermission(CompassPermission, this);    
            LoadConfigVariables();        
        }                            
        
        #endregion        

        #region Compass
        
        private void DrawCompas(BasePlayer player, string color, int size, string color2, int size2, string color3, int size3, float delay) 
        {       
            bool isAdmin = player.IsAdmin;
            
            if (!isAdmin)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                player.SendNetworkUpdateImmediate();        
            }
                
            Text(player, new Vector3(DefaultPos.x,        DefaultPos.y+configData.CompassHeight, DefaultPos.z+30000f), string.Format("<size={0}><color={1}>{2}</color></size>", size, color, GetMessage("NORTH", player.UserIDString)), delay);

            Text(player, new Vector3(DefaultPos.x+7070f,  DefaultPos.y+configData.CompassHeight, DefaultPos.z+29150f), string.Format("<size={0}><color={1}>15</color></size>", size3, color3), delay);
            Text(player, new Vector3(DefaultPos.x+14160f, DefaultPos.y+configData.CompassHeight, DefaultPos.z+26440f), string.Format("<size={0}><color={1}>30</color></size>", size3, color3), delay);
            
            Text(player, new Vector3(DefaultPos.x+21190f, DefaultPos.y+configData.CompassHeight, DefaultPos.z+21230f), string.Format("<size={0}><color={1}>{2}</color></size>", size2, color2, GetMessage("NORTHEAST", player.UserIDString)), delay);            

            Text(player, new Vector3(DefaultPos.x+26420f, DefaultPos.y+configData.CompassHeight, DefaultPos.z+14210f), string.Format("<size={0}><color={1}>60</color></size>", size3, color3), delay);
            Text(player, new Vector3(DefaultPos.x+29120f, DefaultPos.y+configData.CompassHeight, DefaultPos.z+7190f),  string.Format("<size={0}><color={1}>75</color></size>", size3, color3), delay);
            
            Text(player, new Vector3(DefaultPos.x+30000f, DefaultPos.y+configData.CompassHeight, DefaultPos.z),        string.Format("<size={0}><color={1}>{2}</color></size>", size, color, GetMessage("EAST", player.UserIDString)), delay);            
            
            Text(player, new Vector3(DefaultPos.x+28970f, DefaultPos.y+configData.CompassHeight, DefaultPos.z-7780f),  string.Format("<size={0}><color={1}>105</color></size>", size3, color3), delay);
            Text(player, new Vector3(DefaultPos.x+26090f, DefaultPos.y+configData.CompassHeight, DefaultPos.z-14800f), string.Format("<size={0}><color={1}>120</color></size>", size3, color3), delay);
            
            Text(player, new Vector3(DefaultPos.x+20590f, DefaultPos.y+configData.CompassHeight, DefaultPos.z-21810f), string.Format("<size={0}><color={1}>{2}</color></size>", size2, color2, GetMessage("SOUTHEAST", player.UserIDString)), delay);            

            Text(player, new Vector3(DefaultPos.x+13570f, DefaultPos.y+configData.CompassHeight, DefaultPos.z-26750f), string.Format("<size={0}><color={1}>150</color></size>", size3, color3), delay);
            Text(player, new Vector3(DefaultPos.x+6550f,  DefaultPos.y+configData.CompassHeight, DefaultPos.z-29270f), string.Format("<size={0}><color={1}>165</color></size>", size3, color3), delay);
            
            Text(player, new Vector3(DefaultPos.x,        DefaultPos.y+configData.CompassHeight, DefaultPos.z-30000f), string.Format("<size={0}><color={1}>{2}</color></size>", size, color, GetMessage("SOUTH", player.UserIDString)), delay);            
                                    
            Text(player, new Vector3(DefaultPos.x-7020f,  DefaultPos.y+configData.CompassHeight, DefaultPos.z-29160f), string.Format("<size={0}><color={1}>195</color></size>", size3, color3), delay);
            Text(player, new Vector3(DefaultPos.x-14040f, DefaultPos.y+configData.CompassHeight, DefaultPos.z-26510f), string.Format("<size={0}><color={1}>210</color></size>", size3, color3), delay);
            
            Text(player, new Vector3(DefaultPos.x-21060f, DefaultPos.y+configData.CompassHeight, DefaultPos.z-21360f), string.Format("<size={0}><color={1}>{2}</color></size>", size2, color2, GetMessage("SOUTHWEST", player.UserIDString)), delay);            

            Text(player, new Vector3(DefaultPos.x-26350f, DefaultPos.y+configData.CompassHeight, DefaultPos.z-14340f), string.Format("<size={0}><color={1}>240</color></size>", size3, color3), delay);
            Text(player, new Vector3(DefaultPos.x-29090f, DefaultPos.y+configData.CompassHeight, DefaultPos.z-7310f),  string.Format("<size={0}><color={1}>255</color></size>", size3, color3), delay);                                    
            
            Text(player, new Vector3(DefaultPos.x-30000f, DefaultPos.y+configData.CompassHeight, DefaultPos.z),        string.Format("<size={0}><color={1}>{2}</color></size>", size, color, GetMessage("WEST", player.UserIDString)), delay);
                        
            Text(player, new Vector3(DefaultPos.x-28970f, DefaultPos.y+configData.CompassHeight, DefaultPos.z+7780f),  string.Format("<size={0}><color={1}>285</color></size>", size3, color3), delay);
            Text(player, new Vector3(DefaultPos.x-26090f, DefaultPos.y+configData.CompassHeight, DefaultPos.z+14800f), string.Format("<size={0}><color={1}>300</color></size>", size3, color3), delay);
            
            Text(player, new Vector3(DefaultPos.x-21060f, DefaultPos.y+configData.CompassHeight, DefaultPos.z+21360f), string.Format("<size={0}><color={1}>{2}</color></size>", size2, color2, GetMessage("NORTHWEST", player.UserIDString)), delay);            

            Text(player, new Vector3(DefaultPos.x-13570f, DefaultPos.y+configData.CompassHeight, DefaultPos.z+26750f), string.Format("<size={0}><color={1}>330</color></size>", size3, color3), delay);
            Text(player, new Vector3(DefaultPos.x-6550f,  DefaultPos.y+configData.CompassHeight, DefaultPos.z+29270f), string.Format("<size={0}><color={1}>345</color></size>", size3, color3), delay);                                                                                
            
            if (!isAdmin)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                player.SendNetworkUpdateImmediate();        
            }
        }
        
        private void StartShowCompass(BasePlayer player, bool start)
        {
            if (player == null) return;            
            
            if (start)
            {
                if (!ShowCompass.Contains(player))
                    ShowCompass.Add(player);
            }    
                
            DrawCompas(player, configData.MajorDirectionColor, configData.MajorDirectionSize, 
                               configData.MinorDirectionColor, configData.MinorDirectionSize, 
                               configData.AngleDirectionColor, configData.AngleDirectionSize, 1.0f);    
                
            timer.Once(1.0f, ()=>
            {
                if (ShowCompass.Contains(player))                                        
                    StartShowCompass(player, false);                
            });
        }                
        
        private void StopShowCompass(BasePlayer player)
        {
            if (player == null) return;
            
            if (ShowCompass.Contains(player))
                ShowCompass.Remove(player);
        }    
        
        private void OnPlayerInit(BasePlayer player) => StopShowCompass(player);        
        
        private void OnPlayerDie(BasePlayer player, HitInfo info) => StopShowCompass(player);
        
        private void OnPlayerDisconnected(BasePlayer player, string reason) => StopShowCompass(player);        
        
        private void OnPlayerSleep(BasePlayer player) => StopShowCompass(player);

        private void OnPlayerSpectate(BasePlayer player, string spectateFilter) => StopShowCompass(player);
        
        private void OnPlayerTeleport(BasePlayer player) => StopShowCompass(player);
        
        #endregion
        
        #region Helpers
                                
        private static void Text(BasePlayer player, Vector3 pos, string text, float duration) 
        {                                   
            player.SendConsoleCommand("ddraw.text", duration, Color.white, pos, text);                                                                        
        }            

        private bool HasPermission(BasePlayer player)
        {
            return player.IsAdmin || permission.UserHasPermission(player.UserIDString, CompassPermission);
        }    
        
        private string GetMessage(string name, string steamId = null) => lang.GetMessage(name, this, steamId);
        
        #endregion
        
        #region Commands
        
        [ChatCommand("compass")]
        private void CmdChatCompass(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;        
                        
            if (configData.UsePrivilege && !HasPermission(player)) 
            {
                SendReply(player, GetMessage("NoPerm", player.UserIDString));
                return;
            }        
                        
            if (!ShowCompass.Contains(player))
                StartShowCompass(player, true);
            else
                StopShowCompass(player);
        }
        
        [ConsoleCommand("ucompass.toggle")]
        private void CmdConsoleCompass(ConsoleSystem.Arg arg)
        {            
            if (arg == null) return;                                    
            BasePlayer player = arg.Player();                                    
            
            if (player == null) return;
            
            if (configData.UsePrivilege && !HasPermission(player))
            {
                SendReply(player, GetMessage("NoPerm", player.UserIDString));
                return;
            }
            
            if (!ShowCompass.Contains(player))
                StartShowCompass(player, true);
            else
                StopShowCompass(player);
        }        

        #endregion                    
        
        #region Config & Lang                  
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {               
                ["NORTH"] = "North",
                ["NORTHEAST"] = "North-East",
                ["EAST"] = "East",
                ["SOUTHEAST"] = "South-East",
                ["SOUTH"] = "South",
                ["SOUTHWEST"] = "South-West",
                ["WEST"] = "West",
                ["NORTHWEST"] = "North-West",
                ["NoPerm"] = "You don't have permission to use this command."
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {             
                ["NORTH"] = "Север",
                ["NORTHEAST"] = "Северо-Восток",
                ["EAST"] = "Восток",
                ["SOUTHEAST"] = "Юго-Восток",
                ["SOUTH"] = "Юг",
                ["SOUTHWEST"] = "Юго-Запад",
                ["WEST"] = "Запад",
                ["NORTHWEST"] = "Северо-Запад",
                ["NoPerm"] = "У вас нет прав использовать эту команду."
            }, this, "ru");
        }                        
        
        private class ConfigData
        {
            public string MajorDirectionColor;
            public int MajorDirectionSize;
            public string MinorDirectionColor;
            public int MinorDirectionSize;
            public string AngleDirectionColor;
            public int AngleDirectionSize;
            public int CompassHeight;
            public bool UsePrivilege;
        }                                        
        
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                MajorDirectionColor = "red",
                MajorDirectionSize  = 18,
                MinorDirectionColor = "yellow",
                MinorDirectionSize  = 16,
                AngleDirectionColor = "white",
                AngleDirectionSize  = 13,
                CompassHeight       = 1000,
                UsePrivilege        = false
            };            
            timer.Once(0.1f, ()=>SaveConfig(config)); // Save right sort in config file
        }
        
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);        
        
        #endregion            
    }    
    
}