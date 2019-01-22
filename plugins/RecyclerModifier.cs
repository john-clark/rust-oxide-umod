using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{

    [Info("Recycler Modifier", "birthdates", "1.0", ResourceId = 0)]
    [Description("Allows you to change recycler loot based off of multiplication")]
    public class RecyclerModifier : RustPlugin
    {
        protected override void LoadDefaultConfig()
        {
            Config["Multiplier"] = 1;
        }

        void OnRecycleItem(Recycler recycler, Item item)
        {
            int mult = 1;
            if (item != null && int.TryParse((string)Config["Multiplier"], out mult)) item.amount = item.amount * mult;
        }
    }
}