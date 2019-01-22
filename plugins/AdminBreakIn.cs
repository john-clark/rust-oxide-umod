using System;
using UnityEngine;
using CodeHatch.Engine.Networking;    
using CodeHatch.Engine.Behaviours;
using CodeHatch.Common;
using CodeHatch.Permissions; 
namespace Oxide.Plugins
{
    [Info("GetPos By Tammy", "AdminBreakIn", "1.0.0")]
    public class AdminBreakIn : ReignOfKingsPlugin
    {             
        [ChatCommand("breakin")]
        void BreakIn(Player player, string cmd, string[] args)
        {            
            if (!player.HasPermission("admin")){                   
                PrintToChat(player, "You are not allowed to use this command.");
                return;
            }     
                     
            if( args.Length == 2 ){ 
            var destination = new Vector3(player.Entity.Position.x , player.Entity.Position.y, player.Entity.Position.z);
                if(args[0] == "x"){
                    float x = Convert.ToSingle(args[1]);                    
                    if(x > 0){
                    destination = new Vector3(player.Entity.Position.x + x, player.Entity.Position.y, player.Entity.Position.z);
                    } else {
                    x = Convert.ToSingle(args[1].Replace("-", "").Trim());
                    destination = new Vector3(player.Entity.Position.x - x, player.Entity.Position.y, player.Entity.Position.z);
                    }                     
                } else if(args[0] == "z"){
                    float z = Convert.ToSingle(args[1]);
                    if(z > 0){
                    destination = new Vector3(player.Entity.Position.x, player.Entity.Position.y, player.Entity.Position.z + z);
                    } else {
                    z = Convert.ToSingle(args[1].Replace("-", "").Trim()); 
                    destination = new Vector3(player.Entity.Position.x, player.Entity.Position.y, player.Entity.Position.z - z);
                    }  
                } else if(args[0] == "y"){
                    float y = Convert.ToSingle(args[1]);
                    if(y > 0){
                    destination = new Vector3(player.Entity.Position.x, player.Entity.Position.y + y, player.Entity.Position.z);
                    } else {
                    y = Convert.ToSingle(args[1].Replace("-", "").Trim()); 
                    destination = new Vector3(player.Entity.Position.x, player.Entity.Position.y - y, player.Entity.Position.z);
                    }  
                }  
                player.Entity.GetOrCreate<CharacterTeleport>().Teleport(destination);                     
            } else {
                PrintToChat(player, "Usage: /breakin (x|y|z) (-|+)[number]");
            }  
        }
    }
}