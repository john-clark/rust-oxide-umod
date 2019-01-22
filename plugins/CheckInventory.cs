using System;
using System.Collections.Generic;
using System.Linq;  

using CodeHatch.Engine.Networking;
using CodeHatch.Common;
using CodeHatch.Permissions; 

namespace Oxide.Plugins
{
    [Info("CheckInventory By Tammy", "CheckInventory", "1.0.0")]
    public class CheckInventory : ReignOfKingsPlugin
    {        
        [ChatCommand("checkinv")]
        void CheckInv(Player player, string cmd, string[] args)
        {        
            if (!player.HasPermission("admin"))
            {                   
                PrintToChat(player, "You are not allowed to use this command.");
                return;
            }
          
            if (args.Length != 1)
            {
                PrintToChat(player, "Invalid arguments");
                return;
            }
            
            var target = Server.GetPlayerByName(args[0]);
            if (target == null)
            {
                PrintToChat(player, "Invalid target");
                return;
            }
            
            var inventory = target.GetInventory();
            var contents = new Dictionary<string, int>();       
                    
            foreach (var item in inventory.Contents.Where(item => item != null))
            {
                if (contents.ContainsKey(item.Name))
                    contents[item.Name] += item.StackAmount;
                else
                    contents.Add(item.Name, item.StackAmount);
            } 
            
            var content = "Inventory contents for '" + target.Name + "':\n";
            content = contents.Count == 0 ? content + "No items found." : contents.Aggregate(content, (current, item) => current + $"{item.Value} x {item.Key}\n");          
            PrintToChat(player, content);    
        }
    }
}