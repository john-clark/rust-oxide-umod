using Oxide.Core;
using System.Collections.Generic;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using System;

namespace Oxide.Plugins
{
    [Info("Hazmat Diving", "BuzZ", "2.0.1")]
    [Description("This will protect you from drowning and cold damage while swimming.")]

/*======================================================================================================================= 
*
*   
*   16th november 2018
*
*	THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*   2.0.0   20181116    Rewrite of plugin by new maintainer _ hazmat clothes parts no more existing. Switched to Suit. added messages on wear
*   2.0.1   20190123    permission hazmatdiving.use
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
        private float dmgdrowning = 30f;
        private float dmgcold = 30f;

        string Prefix = "[HazmatDiving] ";                  // CHAT PLUGIN PREFIX
        string PrefixColor = "#ebdf42";                 // CHAT PLUGIN PREFIX COLOR
        string ChatColor = "#8bd9ff";                   // CHAT MESSAGE COLOR
        ulong SteamIDIcon = 76561198134466821;  

        const string HazmatDivingBuddy = "hazmatdiving.use"; 

        void Init()
        {
            LoadVariables();
            permission.RegisterPermission(HazmatDivingBuddy, this);
        }

        protected override void LoadDefaultConfig()
        {
            LoadVariables();
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
            if (debug){Puts("Loading Config File:");}

            dmgcold = Convert.ToSingle(GetConfig("On Cold - Damage to reduce", "in percent", "30"));
            if (debug){Puts($"Cold damage = X - {dmgcold}%");}

            dmgdrowning = Convert.ToSingle(GetConfig("On Drowning - Damage to reduce", "in percent", "30"));
            if (debug){Puts($"Drown damage = X - {dmgdrowning}%");}

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
            if (hitinfo == null) return;
            BasePlayer onlinecheck = entity.ToPlayer();
            if (onlinecheck == null) return;
            if (onlinecheck.IsConnected == false)
            {
                if (debug){Puts($"-> IGNORED DAMAGE. From not steam online player.");}
                return;
            }
            bool diver = permission.UserHasPermission(onlinecheck.UserIDString, HazmatDivingBuddy);
            if (!diver) return;
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
                    if (debug){Puts($"-> DROWNED damage");}
                }
                if (hitinfo.damageTypes?.Get(Rust.DamageType.Cold) > 0 && onlinecheck.IsSwimming())
                {
                    damagedone = getDamageDeduction(onlinecheck, Rust.DamageType.Cold);
                    float newdamage = getScaledDamage(hitinfo.damageTypes.Get(Rust.DamageType.Cold), damagedone);
                    hitinfo.damageTypes.Set(Rust.DamageType.Cold, newdamage);
                    armourDamaged = true;
                    if (debug){Puts($"-> COLD damage on SWIMMING");}
                }
//////////////////////////////
// IF CONFIG damageArmour is true ... damage the armour !
/////////////////////////////
/////// FROM ORIGINAL AUTHOR

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
                    if (item.info.shortname.Contains("hazmatsuit"))
                    {
                        if (damageType == Rust.DamageType.Drowned)
                        {
                            dd += (dmgdrowning/100);
                        }
                        if (damageType == Rust.DamageType.Cold)
                        {
                            dd += (dmgcold/100);
                        }
                    }
                }
            }
            return dd;
        }

        void CanWearItem(PlayerInventory inventory, Item item, int targetPos)
        {
            if (loaded == false) return;
            if (item == null) return;
            if (inventory == null) return;
            BasePlayer wannawear = inventory.GetComponent<BasePlayer>();
            if (wannawear == null) return;
            if (wannawear.IsConnected == false) return;
            bool diver = permission.UserHasPermission(wannawear.UserIDString, HazmatDivingBuddy);
            if (!diver) return;
            if (item.info.shortname.Contains("hazmatsuit"))
            {
                Player.Message(wannawear, $"<color={ChatColor}>{lang.GetMessage("coldMsg", this, wannawear.UserIDString)} {dmgcold}%</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon); 
                Player.Message(wannawear, $"<color={ChatColor}>{lang.GetMessage("drowningMsg", this, wannawear.UserIDString)} {dmgdrowning}%</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon); 
            }    
        }
    }
}