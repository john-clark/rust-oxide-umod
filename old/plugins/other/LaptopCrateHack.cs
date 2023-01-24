using System;
using System.Collections.Generic;

namespace Oxide.Plugins {

    [Info("Laptop Crate Hack", "TheSurgeon", "1.0.6")]
    [Description("Require a laptop to hack a crate.")]
    public class LaptopCrateHack : RustPlugin {
        bool requireInHand;
        bool consumeLaptop;
        bool ownCrate;
        int numberRequired;
		int targetingComputerID;

        protected override void LoadDefaultConfig() {
            Config["Require laptop to be in hand (True/False)"] = requireInHand = GetConfig("Require laptop to be in hand (True/False)", false);
            Config["Consume laptop (True/False)"] = consumeLaptop = GetConfig("Consume laptop (True/False)", true);
            Config["Laptops Required (Must be greater than 0)"] = numberRequired = GetConfig("Laptops Required (Must be greater than 0)", 1);
            Config["Only player that hacked can loot? (True/False)"] = ownCrate = GetConfig("Only player that hacked can loot ? (True / False)", false);
            SaveConfig();
        }

        private Dictionary<string, string> EN = new Dictionary<string, string>  {
            {"YouNeed", "Error: You need {0} Targeting Computers and you only have {1}."},
            {"NotHolding", "Error: You must be holding a Targeting Computer in your hand to hack this crate."},
            {"YouDontOwn", "Error: Only the player that hacked this crate can loot it."}
        };

        void Init() {            
            LoadDefaultConfig();			
        }

		
		void OnServerInitialized() {
            var itemDef = ItemManager.FindItemDefinition("targeting.computer");
            if (itemDef != null) {
                targetingComputerID = itemDef.itemid;
            } else {
				// If this id cannot be found we need to disable the plugin to prevent further errors.
				Unsubscribe(nameof(CanHackCrate));
			}
		}
		
        protected override void LoadDefaultMessages() {
            lang.RegisterMessages(EN, this);
        }

        object CanHackCrate(BasePlayer player, HackableLockedCrate crate) {
            if (!requireInHand) {
                var has = player.inventory.GetAmount(targetingComputerID);
                if (has >= numberRequired) {
                    if (consumeLaptop) {
                        player.inventory.Take(null, targetingComputerID, numberRequired);
                        return null;
                    }                   
                } else {
                    message(player, "YouNeed", numberRequired.ToString(), has.ToString());
                    return true;
                }
            } else {
				var activeItem = player.GetActiveItem()?.info?.shortname;
                if (activeItem == null || activeItem == "") {
                    message(player, "NotHolding");
                    return true;
                }
                if(activeItem == "targeting.computer") {
                    var has = player.inventory.GetAmount(targetingComputerID);
                    if (has >= numberRequired) {
                        if (consumeLaptop) {
                            player.inventory.Take(null, targetingComputerID, numberRequired);
                        }
                    } else {
                        message(player, "YouNeed", numberRequired.ToString(), has.ToString());
                        return true;
                    }
                } else {
                    message(player, "NotHolding");
                    return true;
                }
            }
            if(ownCrate) {
                crate.OwnerID = player.userID;
            }
            return null;
        }

        object CanLootEntity(BasePlayer player, HackableLockedCrate crate) {
            Puts(player.userID.ToString() + " : " + crate.OwnerID.ToString());
            if(ownCrate) {
                if(player.userID == crate.OwnerID) {
                    return null;
                } else {
                    message(player, "YouDontOwn");
                    return false;
                }
            }
            return null;
        }

        private void message(BasePlayer player, string key, params object[] args) {
            var message = string.Format(lang.GetMessage(key, this, player.UserIDString), args);
            player.ChatMessage(message);
        }

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

    }
}
