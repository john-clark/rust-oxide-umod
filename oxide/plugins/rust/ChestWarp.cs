using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ChestWarp", "Hougan", "0.1.5")]
    [Description("Create warp between two chests")]
    public class ChestWarp : RustPlugin
    {
        #region Variables

        private class Warp
        {
            [JsonProperty("First chest")]
            public uint FirstPoint = 0;
            [JsonProperty("Second chest")]
            public uint SecondPoint = 0;
        }

        [JsonProperty("Permission to create Warps")]
        private string AdminPermission = "ChestWarp.Admin";
        
        [JsonProperty("Dictionary of created warps")]
        private Dictionary<string, Warp> chestWarps = new Dictionary<string, Warp>();

        #endregion

        #region Initialization

        private void OnServerInitialized()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("ChestWarp/Chests"))
                chestWarps = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, Warp>>("ChestWarp/Chests");
            
            permission.RegisterPermission(AdminPermission, this);
            
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PERMISSION"] = "You do not have permissions!",
                ["SYNTAX"] = "Please, use this syntax:" +
                             "\n /cw add Name - Create new warp" +
                             "\n /cw Name first - Set first point of warp (you should look at box)" +
                             "\n /cw Name second - Set second point of warp (you should look at box)" +
                             "\n /cw remove Name - Remove warp with this name",
                
                ["CREATE.NAME.EXIST"] = "Warp with this name already exist",
                ["CREATE.SUCCESS"] = "You created warp {0} successful!",
                
                ["REMOVE.NAME.EXIST"] = "Warp with this name doesnot exist",
                ["REMOVE.SUCCESS"] = "You removed warp {0} successful!",
                
                ["EDIT.NAME.EXIST"] = "Warp with this name doesnot exist",
                ["EDIT.SUCCESS"] = "You changed warp successful!",
                
                ["RAY.NULL"] = "You are not looking at box!",
                ["SUCCESS.TP"] = "You teleported successfull!"
            }, this);
        }


        private void Unload() => Interface.Oxide.DataFileSystem.WriteObject("ChestWarp/Chests", chestWarps);

        #endregion

        #region OxideHooks

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            Warp warp = null;
            if (chestWarps.Count(p => p.Value.FirstPoint == entity.net.ID) == 0)
            {
                if (chestWarps.Count(p => p.Value.SecondPoint == entity.net.ID) == 0)
                    return;
                
                warp = chestWarps.First(p => p.Value.SecondPoint == entity.net.ID).Value;
            }
            else
                warp = chestWarps.First(p => p.Value.FirstPoint == entity.net.ID).Value;
            
            if (warp == null)
                return;

            BaseEntity firstBox = BaseNetworkable.serverEntities.Find(warp.FirstPoint) as BaseEntity;
            BaseEntity secondBox = BaseNetworkable.serverEntities.Find(warp.SecondPoint) as BaseEntity;

            if (firstBox == null || secondBox == null)
                return;
            
            timer.Once(0.01f, player.EndLooting);

            if (entity.net.ID == warp.FirstPoint)
            {
                player.transform.position = secondBox.transform.position;
                player.Teleport(secondBox.transform.position);
                
            }

            if (entity.net.ID == warp.SecondPoint)
            {
                player.transform.position = firstBox.transform.position;
                player.Teleport(firstBox.transform.position);
            }
            
            player.ChatMessage(lang.GetMessage("SUCCES.TP", this, player.UserIDString));
        }

        #endregion
        
        #region Commands

        [ChatCommand("cw")]
        private void cmdWarp(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, AdminPermission))
            {
                player.ChatMessage(lang.GetMessage("PERMISSION", this, player.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage(lang.GetMessage("SYNTAX", this, player.UserIDString));
                return;
            }

            switch (args[0].ToLower())
            {
                case "add":
                {
                    if (args.Length != 2)
                    {
                        player.ChatMessage(lang.GetMessage("SYNTAX", this, player.UserIDString));
                        return;
                    }

                    if (chestWarps.ContainsKey(args[1]))
                    {
                        player.ChatMessage(lang.GetMessage("CREATE.NAME.EXIST", this, player.UserIDString));
                        return;
                    }
                    
                    chestWarps.Add(args[1], new Warp());
                    player.ChatMessage(string.Format(lang.GetMessage("CREATE.SUCCESS", this, player.UserIDString), args[1]));
                    break;
                }
                case "remove":
                {
                    if (args.Length != 2)
                    {
                        player.ChatMessage(lang.GetMessage("SYNTAX", this, player.UserIDString));
                        return;
                    }

                    if (!chestWarps.ContainsKey(args[1]))
                    {
                        player.ChatMessage(lang.GetMessage("REMOVE.NAME.EXIST", this, player.UserIDString));
                        return;
                    }

                    chestWarps.Remove(args[1]);
                    player.ChatMessage(string.Format(lang.GetMessage("REMOVE.SUCCESS", this, player.UserIDString), args[1]));
                    break;
                }
                default:
                {
                    if (args.Length != 2)
                    {
                        player.ChatMessage(lang.GetMessage("SYNTAX", this, player.UserIDString));
                        return;
                    }
                    
                    if (!chestWarps.ContainsKey(args[0]))
                    {
                        player.ChatMessage(lang.GetMessage("EDIT.NAME.EXIST", this, player.UserIDString));
                        return;
                    }

                    RaycastHit hitInfo;
                    if (!Physics.Raycast(player.eyes.position, player.GetNetworkRotation() * Vector3.forward, out hitInfo, 5f, LayerMask.GetMask(new string[] {"Deployed"})))
                    {
                        SendReply(player, lang.GetMessage("RAY.NULL", this, player.UserIDString));
                        return;
                    }

                    if (!(hitInfo.GetEntity() is BoxStorage))
                    {
                        SendReply(player, lang.GetMessage("RAY.NULL", this, player.UserIDString));
                        return;
                    }
    
                    BaseEntity boxEntity = hitInfo.GetEntity();
    
                    switch (args[1].ToLower())
                    {
                        case "first":
                        {
                            chestWarps[args[0]].FirstPoint = hitInfo.GetEntity().net.ID;
                            break;
                        }
                        case "second":
                        {
                            chestWarps[args[0]].SecondPoint = hitInfo.GetEntity().net.ID;
                            break;
                        }
                    }
                    player.ChatMessage(lang.GetMessage("EDIT.SUCCESS", this, player.UserIDString));
                    break;
                }
            }
        }

        #endregion
    }
}