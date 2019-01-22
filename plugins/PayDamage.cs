        #region [HEADER]
using System.Linq;
using System.Collections.Generic;
using CodeHatch.Common;
using CodeHatch.Engine.Networking;
using Oxide.Core.Plugins;
using CodeHatch.ItemContainer;
using CodeHatch.Blocks.Networking.Events;
using CodeHatch.Engine.Modules.SocialSystem;
using CodeHatch.Thrones.SocialSystem;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("PayDamage", "Karltubing", "1.0.4")]
    public class PayDamage : ReignOfKingsPlugin
    {
        [PluginReference]
        private Plugin GrandExchange;
        #endregion
        #region [LANGUAGE API]
        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"AdminOnly" , "[FF0000]PayDamage[FFFFFF] :[00FF00]Only Admins can use this command!"},
                {"PayDmgOff" , "[FF0000]PayDamage[FFFFFF] :[00FF00]Paying for DAMAGE is now [FF0000]OFF"},
                {"PayDmgOn" , "[FF0000]PayDamage[FFFFFF] :[00FF00]Paying for DAMAGE is now [00FF00]ON"},
                {"KeepItemsOff" , "[FF0000]PayDamage[FFFFFF] :[00FF00]Keeping Items for DAMAGE is now [FF0000]OFF"},
                {"KeepItemsOn" , "[FF0000]PayDamage[FFFFFF] :[00FF00]Keeping Items for DAMAGE is now [00FF00]ON"},
                {"FineSpamOff" , "[FF0000]PayDamage[FFFFFF] :[00FF00]Fine Spam now [FF0000]OFF"},
                {"FineSpamOn" , "[FF0000]PayDamage[FFFFFF] :[00FF00]Fine Spam is now [00FF00]ON"}            
            }, this);
        }
        string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);
        #endregion
        #region [BOOLS]
        private bool allowPayDmg = true; // Turns on/off paying for Damages For Damages.
        private bool allowKeepItems = true; // Turns on/off keeping the items needed to cause Damage.
        private bool allowFineSpam = true; // Turns on/off Warning messages.
        #endregion
        #region [CONFIG]
        string Resource1;
        string Resource2;
        int Amount1;
        int Amount2;
        int SuccessOrFail;
        int StoneOrWood;
        int OneOrTwo;
        void Init() => LoadDefaultConfig();
        protected override void LoadDefaultConfig()
        {
            Config["ResourceType1 needed to cause damage"] = Resource1 = GetConfig("ResourceType1 needed to cause damage", "Wood");
            Config["ResourceType2 needed to cause damage"] = Resource2 = GetConfig("ResourceType2 needed to cause damage", "Stone");
            Config["ResourceAmount1 needed to cause damage"] = Amount1 = GetConfig("ResourceAmount1 needed to cause damage", 2);
            Config["ResourceAmount2 needed to cause damage"] = Amount2 = GetConfig("ResourceAmount2 needed to cause damage", 2);
            //Config["Chance for to Pay or Not"] = SuccessOrFail = GetConfig("Chance for Success or Fail", 50);
            //Config["Chance for Resource1 or Resource2"] = StoneOrWood = GetConfig("Chance for StoneOrWood or Pay", 50);
            //Config["Chance to pay One or Two"] = OneOrTwo = GetConfig("Chance to pay One or Two", 50);
            SaveConfig();
        }
        T GetConfig<T>(string name, T defaultValue) => Config[name] == null ? defaultValue : (T)System.Convert.ChangeType(Config[name], typeof(T));
        #endregion
        #region [COMMANDS]
        [ChatCommand("paydmg")]
        private void AllowResourcesForDmG(Player player, string cmd)
        {
            ToggleDmgResourcePaying(player, cmd);
        }
        [ChatCommand("keepitems")]
        private void AllowKeepingResourcesForDmG(Player player, string cmd)
        {
            ToggleDmgResourceKeeping(player, cmd);
        }
        [ChatCommand("finespam")]
        private void AllowFineSpam(Player player, string cmd)
        {
            ToggleFineSpam(player, cmd);
        }        
        [ChatCommand("dmgfine")]
        void SendPlayerHelpText(Player player, string command, string[] args)
        {
            PrintToChat(player, "You need at least[FF0000] " + Amount1.ToString() + "[00FF00] " + Resource1.ToString() + " " + "[FFFFFF]and[FF0000] " + Amount2.ToString() + "[00FF00] " + Resource2.ToString() + " " + "[FFFFFF] to cause block damage ");
        }
        #endregion
        #region [FUNCTIONS]
        public void RemoveItemsFromInventory(Player player, string resource, int amount)
        {
            ItemCollection inventory = player.GetInventory().Contents;
            int removeAmount = 0;
            int amountRemaining = amount;
            foreach (InvGameItemStack item in inventory.Where(item => item != null))
            {
                if (item.Name != resource) continue;
                removeAmount = amountRemaining;
                if (item.StackAmount < removeAmount) removeAmount = item.StackAmount;
                inventory.SplitItem(item, removeAmount);
                amountRemaining = amountRemaining - removeAmount;
            }
        }
        private bool CanRemoveResource(Player player, string resource, int amount)
        {
            // Check player's inventory
            var inventory = player.CurrentCharacter.Entity.GetContainerOfType(CollectionTypes.Inventory);
            // Check how much the player has
            var foundAmount = 0;
            foreach (var item in inventory.Contents.Where(item => item != null))
            {
                if (item.Name == resource)
                {
                    foundAmount = foundAmount + item.StackAmount;
                }
            }
            if (foundAmount >= amount) return true;
            return false;
        }
        private void ToggleDmgResourcePaying(Player player, string cmd)
        {
            if (!player.HasPermission("admin"))
            {
                PrintToChat(player, string.Format(GetMessage("AdminOnly", player.Id.ToString()), ""));
                return;
            }
            if (allowPayDmg)
            {
                allowPayDmg = false;
                PrintToChat(player, string.Format(GetMessage("PayDmgOff", player.Id.ToString()), ""));
                return;
            }
            allowPayDmg = true;
            PrintToChat(player, string.Format(GetMessage("PayDmgOn", player.Id.ToString()), ""));
        }
        private void ToggleDmgResourceKeeping(Player player, string cmd)
        {
            if (!player.HasPermission("admin"))
            {
                PrintToChat(player, string.Format(GetMessage("AdminOnly", player.Id.ToString()), ""));
                return;
            }
            if (allowKeepItems)
            {
                allowKeepItems = false;
                PrintToChat(player, string.Format(GetMessage("KeepItemsOff", player.Id.ToString()), ""));
                return;
            }
            allowKeepItems = true;
            PrintToChat(player, string.Format(GetMessage("KeepItemsOn", player.Id.ToString()), ""));
        }
        private void ToggleFineSpam(Player player, string cmd)
        {
            if (allowFineSpam)
            {
                allowFineSpam = false;
                PrintToChat(player, string.Format(GetMessage("FineSpamOff", player.Id.ToString()), ""));
                return;
            }
            allowFineSpam = true;
            PrintToChat(player, string.Format(GetMessage("FineSpamOn", player.Id.ToString()), ""));
        }
        private bool IsInOwnCrestArea(Player player, Vector3 position)
        {
            var crestScheme = SocialAPI.Get<CrestScheme>();
            var crest = crestScheme.GetCrestAt(position);
            return crest?.SocialId == player.GetGuild().BaseID;
        }
        #endregion
        #region [HOOKS]
        private void SendHelpText(Player player)
        {
            player.SendMessage("[0000FF]Pay Damage[-]");
            player.SendMessage("[00FF00]/paydmg[-] - Paying for Damage On/Off");
            player.SendMessage("[00FF00]/keepitems[-] - Keeping Items or Paying Resources On/Off.");
            player.SendMessage("[00FF00]/finespam[-] - Turns Fine Messages On/Off.");
            player.SendMessage("[00FF00]/dmgfine[-] - Shows you what resources are required to do damage");
        }
        static System.Random RNG = new System.Random();        
        private void OnCubeTakeDamage(CubeDamageEvent e)
        {
            int SuccessOrFail;
            int StoneOrWood;
            int OneOrTwo;
            var player = e.Damage.DamageSource.Owner;
            if (player == null) return;
            var worldCoordinate = e.Grid.LocalToWorldCoordinate(e.Position);
            var crestScheme = SocialAPI.Get<CrestScheme>();
            var crest = crestScheme.GetCrestAt(worldCoordinate);
            if (crest == null) return;
            if (allowPayDmg)
            {               
                 SuccessOrFail = RNG.Next(1, 100);
                 StoneOrWood = RNG.Next(1, 100);
                 OneOrTwo = RNG.Next(1, 100);
                 if (!CanRemoveResource(player, Resource1, Amount1) || !CanRemoveResource(player, Resource2, Amount2))
                 {
                     e.Damage.Amount = 0f;
                     e.Damage.ImpactDamage = 0f;
                     e.Damage.MiscDamage = 0f;
                    if (allowFineSpam)
                    {
                        PrintToChat(player, "You need at least[FF0000] " + Amount1.ToString() + "[00FF00] " + Resource1.ToString() + " " + "[FFFFFF]and[FF0000] " + Amount2.ToString() + "[00FF00] " + Resource2.ToString() + " " + "[FFFFFF] to cause damage ");
                        return;
                    }    
                 }
                if (allowKeepItems) return;
                    if (StoneOrWood <= 50)
                    {
                     if (OneOrTwo <= 50)
                     {
                         RemoveItemsFromInventory(player, Resource1, 1);
                         return;
                     }
                     else
                     {
                         RemoveItemsFromInventory(player, Resource1, 2);
                         return;
                     }
                 }
                 else
                 {
                     if (OneOrTwo <= 50)
                     {
                         RemoveItemsFromInventory(player, Resource2, 1);
                         return;
                     }
                     else
                     {
                         RemoveItemsFromInventory(player, Resource2, 2);
                         return;
                     }
                }                
            }                
        }
    }
}
        #endregion