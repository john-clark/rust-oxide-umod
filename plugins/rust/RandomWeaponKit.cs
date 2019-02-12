using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
     [Info("Random Weapon Kit", "Orange", "1.0.2")]
     [Description("Allows players to get random weapon kits with features")]
     public class RandomWeaponKit : RustPlugin
     {
         #region Vars

         private Dictionary<ulong, double> cooldowns = new Dictionary<ulong, double>();
         
         private const string permUse = "randomweaponkit.use";
         private const string permCooldown = "randomweaponkit.cooldown";

         #endregion

         #region Oxide Hooks

         private void Init()
         {
             permission.RegisterPermission(permUse, this);
             lang.RegisterMessages(EN, this);
         }

         [ChatCommand("kit.random")]
         private void Cmd(BasePlayer player)
         {
             Command(player);
         }

         #endregion
         
         #region Helpers

         private void Command(BasePlayer player)
         {
             if (!HasPerm(player, permUse))
             {
                 return;
             }

             var ignore = permission.UserHasPermission(player.UserIDString, permCooldown);
             
             if (!ignore && HasCooldown(player))
             {
                 return;
             }

             if (!ignore)
             {
                 cooldowns.Add(player.userID, Now());
             }
             
             var kit = config.list.GetRandom();
             var amount = Core.Random.Range(kit.minAmmo, kit.maxAmmo);
             var item1 = ItemManager.CreateByName(kit.weaponName);
             var item2 = ItemManager.CreateByName(kit.ammoName, amount);

             if (item1 != null)
             {
                 player.GiveItem(item1);
             }

             if (item2 != null)
             {
                 player.GiveItem(item2);
             }
             
             message(player, "Received");
         }

         private double Now()
         {
             return DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
         }

         private int Passed(double a)
         {
             return Convert.ToInt32(Now() - a);
         }

         private bool HasCooldown(BasePlayer player)
         {
             var id = player.userID;
             if (!cooldowns.ContainsKey(id)) {return false;}
             var value = cooldowns[id];
             var passed = Passed(value);
             if (!(passed < config.cooldown))
             {
                 cooldowns.Remove(id);
                 return false;
             }
             message(player, "Cooldown", config.cooldown - passed);
             return true;
         }
         
         private bool HasPerm(BasePlayer player, string perm)
         {
             if (player == null) {return true;}

             if (permission.UserHasPermission(player.UserIDString, perm))
             {
                 return true;
             }

             message(player, "Permission");
             return false;
         }
         

         #endregion

         #region Localization

         private Dictionary<string, string> EN = new Dictionary<string, string>
         {
             {"Permission", "You don't have permission to use that command"},
             {"Cooldown", "You have cooldown {0} seconds"},
             {"Received", "You get random weapon kit!"}
         };

         private void message(BasePlayer player, string key, params object[] args)
         {
             var message = string.Format(lang.GetMessage(key, this, player.UserIDString), args);
             player.ChatMessage(message);
         }

         #endregion

         #region Configuration
         
         private ConfigData config;
        
         private class ConfigData
         {    
             [JsonProperty(PropertyName = "1. Command cooldown")]
             public float cooldown;
             
             [JsonProperty(PropertyName = "2. List of weapons")]
             public List<OWeapon> list;
             
             public class OWeapon
             {
                 [JsonProperty(PropertyName = "Weapon shortname")]
                 public string weaponName;
                 
                 [JsonProperty(PropertyName = "Ammo shortname")]
                 public string ammoName;
                 
                 [JsonProperty(PropertyName = "Minimal ammo that be given")]
                 public int minAmmo;
                 
                 [JsonProperty(PropertyName = "Maximal ammo that be given")]
                 public int maxAmmo;
             }
             
         }
        
         private ConfigData GetDefaultConfig()
         {
             return new ConfigData
             {
                 cooldown = 600,
                 list = new List<ConfigData.OWeapon>
                 {
                     new ConfigData.OWeapon
                     {
                         weaponName = "smg.mp5",
                         ammoName = "ammo.pistol",
                         minAmmo = 10,
                         maxAmmo = 100
                     },
                     new ConfigData.OWeapon
                     {
                         weaponName = "rifle.bolt",
                         ammoName = "ammo.rifle",
                         minAmmo = 10,
                         maxAmmo = 100
                     },
                     new ConfigData.OWeapon
                     {
                         weaponName = "lmg.m249",
                         ammoName = "ammo.rifle",
                         minAmmo = 10,
                         maxAmmo = 100
                     }
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
     }
}