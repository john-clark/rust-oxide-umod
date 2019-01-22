using Oxide.Core;
using System.Collections.Generic;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using System;

namespace Oxide.Plugins
{
    [Info("Hazmat Diving", "BuzZ", "2.0.0")]
    [Description("This will protect you from drowning and cold damage while swimming.")]

/*======================================================================================================================= 
*
*   
*   16th november 2018
*
*	THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*   2.0.0   20181116    Rewrite of plugin by new maintainer _ hazmat clothes parts no more existing. Switched to Suit. added messages on wear
*
*   add scientist suit ?
*
*
*********************************************
*   Original author :   DaBludger on versions <2.0.0
*   Maintainer(s)   :   BuzZ since 20181116 from v2.0.0
*********************************************   
*
*=======================================================================================================================*/



    public class HazmatDiving : RustPlugin
    {
        private bool Changed;
        bool debug = false;
        bool loaded;

        private bool applydamageArmour = false;
        //private bool configloaded = false;
        private float armourDamageAmount = 0f;
        private float dmgdrowning = 0.3f;
        private float dmgcold = 0.3f;

        string Prefix = "[HazmatDiving] ";                  // CHAT PLUGIN PREFIX
        string PrefixColor = "#ebdf42";                 // CHAT PLUGIN PREFIX COLOR
        string ChatColor = "#8bd9ff";                   // CHAT MESSAGE COLOR
        ulong SteamIDIcon = 76561198134466821;  

        void Init()
        {
            LoadVariables();
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
			//configloaded = true;
        }

        void Loaded()
        {
            loaded = true;
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        private void LoadVariables()
        {
            if (debug == true){Puts("Loading Config File:");}

            dmgcold = Convert.ToSingle(GetConfig("Damage to reduce", "on cold", "0.3"));
            if (debug == true){Puts($"Cold damage = X - {dmgcold*100}%");}

            dmgdrowning = Convert.ToSingle(GetConfig("Damage to reduce", "on drowning", "0.3"));
            if (debug == true){Puts($"Drown damage = X - {dmgdrowning*100}%");}

            Prefix = Convert.ToString(GetConfig("Chat Settings", "Prefix", "[HazmatDiving] "));                       // CHAT PLUGIN PREFIX
            PrefixColor = Convert.ToString(GetConfig("Chat Settings", "PrefixColor", "#ebdf42"));                // CHAT PLUGIN PREFIX COLOR
            ChatColor = Convert.ToString(GetConfig("Chat Settings", "ChatColor", "#8bd9ff"));                    // CHAT MESSAGE COLOR
            SteamIDIcon = Convert.ToUInt64(GetConfig("Chat Settings", "SteamIDIcon", "76561198134466821"));        // SteamID FOR PLUGIN ICON - STEAM PROFILE CREATED FOR THIS PLUGIN / NONE YET /

////////////////// FROM AUTHOR
            //applydamageArmour = Convert.ToBoolean(GetConfig("Attire", "TakesDamage", "false"));
            //Puts("Amour takes damage: "+ applydamageArmour);
            //armourDamageAmount = Convert.ToSingle(GetConfig("Attire", "DamageAmount", "0.0"));
            //Puts("How much damage does the armour take: "+ armourDamageAmount);

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }


#region MESSAGES

        protected override void LoadDefaultMessages()
        {

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"coldMsg", "Cold damages will be reduce by"},
                {"drowningMsg", "Drowning damages will be reduce by"},


            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"coldMsg", "Les dommages de froid seront réduits de"},
                {"drowningMsg", "Les dommages de noyade seront réduits de"},

            }, this, "fr");
        }

#endregion


        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {

            if (hitinfo == null)
            {return;}

            BasePlayer onlinecheck = entity.ToPlayer();

            if (BasePlayer.activePlayerList.Contains(onlinecheck) == false)
            {
                if (debug == true){Puts($"-> IGNORED DAMAGE. From not steam online player.");}
                return;
            }

            if (hitinfo.hasDamage)
            {
                float damagedone;
                bool armourDamaged = false;
                if (hitinfo.damageTypes?.Get(Rust.DamageType.Drowned) > 0)
                {
                    damagedone = getDamageDeduction(onlinecheck, Rust.DamageType.Drowned);
                    float newdamage = getScaledDamage(hitinfo.damageTypes.Get(Rust.DamageType.Drowned), damagedone);
                    hitinfo.damageTypes.Set(Rust.DamageType.Drowned, newdamage);
                    armourDamaged = true;
                    if (debug == true){Puts($"-> DROWNED damage");}
                }

                if (hitinfo.damageTypes?.Get(Rust.DamageType.Cold) > 0 && onlinecheck.IsSwimming())
                {
                    damagedone = getDamageDeduction(onlinecheck, Rust.DamageType.Cold);
                    float newdamage = getScaledDamage(hitinfo.damageTypes.Get(Rust.DamageType.Cold), damagedone);
                    hitinfo.damageTypes.Set(Rust.DamageType.Cold, newdamage);
                    armourDamaged = true;
                    if (debug == true){Puts($"-> COLD damage on SWIMMING");}
                }

//////////////////////////////
// IF CONFIG damageArmour is true ... damage the armour !
/////////////////////////////
/////// FROM AUTHOR

                /*if (armourDamaged && applydamageArmour)
                {
                    foreach (Item item in onlinecheck.inventory.containerWear.itemList) // foreach is not a good point
                    {
                        if (item.info.name.ToLower().Contains("hazmat"))
                        {
                            item.condition = item.condition - armourDamageAmount;
                        }
                    }
                }*/
            }
        }

        private float getScaledDamage(float current, float deduction)
        {
            float newdamage = current - (current * deduction);
            if (newdamage < 0)
            {
                newdamage = 0;
            }
            return newdamage;
        }

        private float getDamageDeduction(BasePlayer player, Rust.DamageType damageType)
        {
            float dd = 0.0f;
            foreach (Item item in player.inventory.containerWear.itemList)
            {
                if (!item.isBroken)
                {
                    if (item.info.name.ToLower().Contains("hazmat"))
                    {
                        if (damageType == Rust.DamageType.Drowned)
                        {
                            dd += dmgdrowning;
                        }
                        if (damageType == Rust.DamageType.Cold)
                        {
                            dd += dmgcold;
                        }
                    }
                }
            }
            return dd;
        }

        void CanWearItem(PlayerInventory inventory, Item item, int targetPos)
        {
            if (loaded == false){return;}
            if (item == null){return;}
            if (inventory == null){return;}
            BasePlayer wannawear = inventory.GetComponent<BasePlayer>();
            if (wannawear == null){return;}
            if (BasePlayer.activePlayerList.Contains(wannawear) == false){return;}
            if (item.info.shortname == "hazmatsuit")
            {
                Player.Message(wannawear, $"<color={ChatColor}>{lang.GetMessage("coldMsg", this, wannawear.UserIDString)} {dmgcold*100}%</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon); 
                Player.Message(wannawear, $"<color={ChatColor}>{lang.GetMessage("drowningMsg", this, wannawear.UserIDString)} {dmgdrowning*100}%</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon); 
            }    
        }
    }
}