using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("instantBuy", "Jake_Rich", "1.0.0")]
    [Description("Vending Machine has no delay")]

    public class InstantBuy : RustPlugin
    {
        void Init()
        {

        }

        void Loaded()
        {

        }

        object OnBuyVendingItem(VendingMachine machine, BasePlayer player, int sellOrderID, int amount)
        {
            machine.ClientRPC<int>(null, "CLIENT_StartVendingSounds", sellOrderID);
            machine.DoTransaction(player, sellOrderID, amount);
            return false;
        }
    }
}