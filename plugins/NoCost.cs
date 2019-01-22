using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oxide.Core;
using System.IO;

namespace Oxide.Plugins
{
    [Info("NoCost", "Pastori", 0.2)]
    [Description("Choose a materials that won't be consumed while crafting.")]

    class NoCost : RustPlugin
    {
        List<PluginItem> itemList = new List<PluginItem>();

        class PluginItem
        {
            public string shortname { get; set; }
            public PluginItem(string sn)
            {
                shortname = sn;
            }
        }

        void Loaded()
        {
            for(int i = 0; i < (Config["itemList"] as List<object>).Count; i++)
            {
                itemList.Add(new PluginItem((Config["itemList"] as List<object>)[i].ToString()));
            }

            Puts("Making: " + itemList.Count.ToString() + " items to non-consumable.");
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file.");
            Config.Clear();

            Config["itemList"] = new List<string> {
                "gears", "metalblade", "metalpipe",
                "riflebody", "roadsigns", "rope",
                "sewingkit", "smgbody", "metalspring",
                "techparts", "propanetank"
            };

            SaveConfig();
        }

        void OnItemCraft(ItemCraftTask item)
        {
            foreach (var stuff in item.blueprint.ingredients)
            {
                int itemToCraft = stuff.itemDef.itemid;
                string itemShort = stuff.itemDef.shortname;
                int amountToCraft = Convert.ToInt32(stuff.amount);
                
                foreach(PluginItem pluginItem in itemList)
                {
                    if(pluginItem.shortname == itemShort)
                    {
                        item.owner.GiveItem(ItemManager.CreateByName(itemShort, amountToCraft));
                    }
                }
            }
        }

        void OnItemCraftCancelled(ItemCraftTask task)
        {
            foreach (var stuff in task.blueprint.ingredients)
            {
                int itemToCraft = stuff.itemDef.itemid;
                string itemShort = stuff.itemDef.shortname;
                int amountToCraft = Convert.ToInt32(stuff.amount);

                foreach(PluginItem pluginItem in itemList)
                {
                    if(pluginItem.shortname == itemShort)
                    {
                        task.owner.inventory.Take(null, itemToCraft, amountToCraft);
                    }
                }
            }
        }
    }
}