using System.Collections.Generic;
using System;
using System.Linq;
using CodeHatch.Engine.Networking;
using CodeHatch.Common;
using CodeHatch.Networking.Events.Entities;

namespace Oxide.Plugins
{
    [Info("PlagueDamageToggle", "Mordeus", "1.0.0")]
    public class PlagueDamageToggle : ReignOfKingsPlugin
    {
        //config
        public string ChatTitle;
        public bool PlagueDamageOff;        

        #region Lang API
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["NoAccess"] = "{Title} [FF0000]You can't use this command.[FFFFFF]",               
                ["Toggle"] = "{Title} [FF0000]Plague Damage {0}.[FFFFFF]"
                
            }, this);
        }
        #endregion Lang API
        #region Config

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating new configurationfile...");
        }

        private new void LoadConfig()
        {
            ChatTitle = GetConfig<string>("Title", "[4F9BFF]Server:");
            PlagueDamageOff = GetConfig<bool>("Plague Damage Off", true);            

            SaveConfig();
        }
        #endregion Config
        #region Oxide  
        private void Init()
        {
            permission.RegisterPermission("plaguedamagetoggle.admin", this);
            LoadConfig();
        }
        private void OnEntityHealthChange(EntityDamageEvent damageEvent)
        {
            if (damageEvent == null) return;            
            if (damageEvent.Damage == null) return;                     
            if (damageEvent.Entity == null) return;            
            if (!damageEvent.Entity.IsPlayer) return;              
            if (damageEvent.Damage.Amount < 0) return;
            if (damageEvent.Damage.DamageTypes == CodeHatch.Damaging.DamageType.Plague && PlagueDamageOff)
            {
                damageEvent.Cancel();
                damageEvent.Damage.Amount = 0f;
            }
        }

        #endregion Oxide
        #region Commands
        //command: /plague - toggles plague damage on and off
        [ChatCommand("plague")]
        private void CmdPlague(Player player, string cmd)
        {
            TogglePlagueDamage(player);
        }

        #endregion Commands 

        #region Functions
        private void TogglePlagueDamage(Player player)
        {
            var playerId = player.Id.ToString();
            if (!hasPermission(player)) { player.SendError(Message("NoAccess", playerId)); return; }
            if (PlagueDamageOff)
            {
                PlagueDamageOff = false;
                SendReply(player, Message("Toggle", playerId), "on");
            }
            else
            {
                PlagueDamageOff = true;
                SendReply(player, Message("Toggle", playerId), "off");
            }
            SaveConfig();
        }


        #endregion Functions

        #region Helpers
        private string Message(string key, string id = null, params object[] args)
        {
            return lang.GetMessage(key, this, id).Replace("{Title}", ChatTitle);
        }

        private T GetConfig<T>(params object[] pathAndValue)
        {
            List<string> pathL = pathAndValue.Select((v) => v.ToString()).ToList();
            pathL.RemoveAt(pathAndValue.Length - 1);
            string[] path = pathL.ToArray();

            if (Config.Get(path) == null)
            {
                Config.Set(pathAndValue);
                PrintWarning($"Added field to config: {string.Join("/", path)}");
            }

            return (T)Convert.ChangeType(Config.Get(path), typeof(T));
        }
        private bool hasPermission(Player player)
        {
            if (!player.HasPermission("plaguedamagetoggle.admin"))
                return false;
            return true;
        }
       #endregion Helpers
    }

}