using System.Collections.Generic;
using System;
using UnityEngine;
using Oxide.Core.Plugins;


namespace Oxide.Plugins
{
    [Info("ToggleNoggin", "carny666", "1.0.4", ResourceId = 2725)]
    class ToggleNoggin : RustPlugin
    {
        const string adminPermission = "ToggleNoggin.admin";
        const string candlePermission = "ToggleNoggin.candle";
        const string minersPermission = "ToggleNoggin.miners";

        static string lastHat = "";

        void Init()
        {
            try
            {
                permission.RegisterPermission(adminPermission, this);
                permission.RegisterPermission(candlePermission, this);
                permission.RegisterPermission(minersPermission, this);

            }
            catch (Exception ex)
            {
                throw new Exception($"Error in Loaded {ex.Message}");
            }
        }

        [ConsoleCommand("togglenoggin")]
        void ccToggleNoggin(ConsoleSystem.Arg arg)
        {
            try
            {
                var player = arg.Player();
                if (player == null) return;

                if ((permission.UserHasPermission(player.UserIDString, minersPermission) || permission.UserHasPermission(player.UserIDString, adminPermission)) && arg?.Args != null)
                    ToggleHat(player, "hat.miner");
                else if (permission.UserHasPermission(player.UserIDString, candlePermission) && permission.UserHasPermission(player.UserIDString, adminPermission))
                    ToggleHat(player, "hat.candle");

            }
            catch (Exception ex)
            {
                throw new Exception($"Error in ccToggleNoggin {ex.Message}");
            }
        }

        [ChatCommand("togglenoggin")]
        void chcToggleNoggin(BasePlayer player, string command, string[] args)
        {
            try
            {
                if (player == null) return;

                if ((permission.UserHasPermission(player.UserIDString, adminPermission) || permission.UserHasPermission(player.UserIDString, minersPermission)) && args.Length > 0)
                    ToggleHat(player, "hat.miner");
                else if (permission.UserHasPermission(player.UserIDString, adminPermission) || permission.UserHasPermission(player.UserIDString, candlePermission))
                    ToggleHat(player, "hat.candle");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error in chcToggleNoggin {ex.Message}");

            }

        }

        void ToggleHat(BasePlayer player, string hatItemName)
        {
            try
            {

                /// test is hat already exists
                if (player.inventory.containerWear.FindItemsByItemName("hat.miner") == null && player.inventory.containerWear.FindItemsByItemName("hat.candle") == null)
                {
                    var hatDef = ItemManager.FindItemDefinition(hatItemName);

                    // save last hat for removeal next time..
                    lastHat = hatItemName;

                    if (hatDef != null)
                    {
                        Item hatItem = ItemManager.CreateByItemID(hatDef.itemid, 1);
                        if (hatItem != null)
                        {
                            player.inventory.GiveItem(hatItem, player.inventory.containerWear);
                            hatItem.SetFlag(global::Item.Flag.IsOn, true);
                        }
                    }
                }
                else
                {
                    var p = player.inventory.containerWear.FindItemsByItemName(lastHat);
                    if (p != null)
                        p.RemoveFromContainer();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error in ToggleHat {ex.Message}");
            }
        }

    }
}