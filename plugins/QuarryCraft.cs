using Newtonsoft.Json;
using Oxide.Core;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("QuarryCraft", "Vlad-00003", "1.0.1")]
    [Description("Allow players with permissions craft quarry")]
    /*
     * Author info:
     *   E-mail: Vlad-00003@mail.ru
     *   Vk: vk.com/vlad_00003
     */
    class QuarryCraft : RustPlugin
    {
        #region Vars

        private PluginConfig config;
        private ItemBlueprint bp;
        private ItemDefinition def;
        #endregion

        #region Config
        private class PluginConfig
        {
            [JsonProperty("Crafting price")]
            public Dictionary<string, int> Price;
            [JsonProperty("Workbench level required")]
            public int Workbench = 2;
            [JsonProperty("Crafting time")]
            public float Time = 200f;
            [JsonProperty("Amount to create")]
            public int Amount = 1;
            [JsonProperty("Permission to craft")]
            public string Permission = "quarrycraft.use";
            [JsonProperty("Command to craft")]
            public string Command = "/quarry";
        }
        #endregion

        #region ConfigInitialization and quiting

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig()
            {
                Price = new Dictionary<string, int>()
                {
                    ["wood"] = 10000,
                    ["metal.fragments"] = 1750,
                    ["cloth"] = 500
                }
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            var defs = ItemManager.GetItemDefinitions();
            var ingridients = new List<ItemAmount>();
            foreach (var item in config.Price)
            {
                def = defs.FirstOrDefault(x =>
                    x.displayName.english == item.Key || x.shortname == item.Key || x.itemid.ToString() == item.Key);
                if (!def)
                {
                    PrintWarning(GetMsg("Nodef",null, item.Key));
                    continue;
                }
                ingridients.Add(new ItemAmount(def,item.Value));
            }

            def = ItemManager.FindItemDefinition("mining.quarry");
            if (!def)
            {
                PrintError("Unable to find the quarry defenition! The plugin can't work at all.\nPlease contact the developer - Vlad-00003 at oxide.");
                Interface.Oxide.UnloadPlugin(Title);
            }
            bp = def.Blueprint;
            if (bp == null)
            {
                bp = def.gameObject.AddComponent<ItemBlueprint>();
                bp.ingredients = ingridients;
                bp.defaultBlueprint = false;
                bp.userCraftable = true;
                bp.isResearchable = true;
                bp.workbenchLevelRequired = config.Workbench;
                bp.amountToCreate = config.Amount;
                bp.time = config.Time;
                bp.scrapRequired = 750;
                bp.blueprintStackSize = 1000;
            }
            cmd.AddChatCommand(config.Command.Replace("/", string.Empty), this,CmdCraft);
            permission.RegisterPermission(config.Permission,this);
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        void Unload()
        {
            UnityEngine.Object.Destroy(bp);
        }
        #endregion

        #region Localization

        private string GetMsg(string langkey, BasePlayer player, params object[] args)
        {
            string msg = lang.GetMessage(langkey, this, player?.UserIDString);
            if (args.Length > 0)
                msg = string.Format(msg, args);
            return msg;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["Nodef"] = "No defenition found for item {0}. It won't be used as the price for the craft.",
                ["Noingridient"] = "Not enought ingidients! Required to craft:\n{0}",
                ["EnoughtIngridient"] = "{0} - <color=#53f442>{1}</color>/{2}",
                ["NotEnoughtIngridient"] = "{0} - <color=#f44141>{1}</color>/{2}",
                ["Workbench"] = "Your current crafting level is too low. Required crafting level {0}",
                ["NoPermission"] = "You don't have the required permission"
        },this);
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["Nodef"] = "Не найдено определение предмета {0}. Он не будет добавлен к цене крафта.",
                ["Noingridient"] = "Недостаточно ингридиентов. На крафт нужно:\n{0}",
                ["EnoughtIngridient"] = "{0} - <color=#53f442>{1}</color>/{2}",
                ["NotEnoughtIngridient"] = "{0} - <color=#f44141>{1}</color>/{2}",
                ["Workbench"] = "Необходим верстак уровня {0}",
                ["NoPermission"] = "У вас нет необходимой привилегии"
            },this,"ru");
    }

        #endregion

        #region Main
        private void CmdCraft(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, config.Permission))
            {
                player.ChatMessage(GetMsg("NoPermission",player));
                return;
            }
            if (player.currentCraftLevel < config.Workbench)
            {
                player.ChatMessage(GetMsg("Workbench",player,config.Workbench));
                return;
            }
            foreach (var ingredient in bp.ingredients)
            {
                var playeram = player.inventory.GetAmount(ingredient.itemDef.itemid);
                if (playeram >= ingredient.amount) continue;
                var reply = bp.ingredients.Select(x =>
                    GetMsg(player.inventory.GetAmount(x.itemDef.itemid) >= x.amount
                            ? "EnoughtIngridient"
                            : "NotEnoughtIngridient"
                        , player, x.itemDef.displayName.translated, player.inventory.GetAmount(x.itemDef.itemid),
                        x.amount)).ToArray();
                player.ChatMessage(GetMsg("Noingridient",player,string.Join("\n",reply)));
                return;
            }
            ItemCrafter itemCrafter = player.inventory.crafting;
            if (!itemCrafter.CanCraft(bp))
                return;
            ++itemCrafter.taskUID;
            ItemCraftTask task = Facepunch.Pool.Get<ItemCraftTask>();
            task.blueprint = bp;
            List<Item> items = new List<Item>();
            foreach (var ingridient in bp.ingredients)
            {
                var amount = (int)ingridient.amount;
                foreach (var container in itemCrafter.containers)
                {
                    amount -= container.Take(items, ingridient.itemid, amount);
                    if(amount > 0)
                        continue;
                    break;
                }
            }
            task.potentialOwners = new List<ulong>();
            foreach (var item in items)
            {
                item.CollectedForCrafting(player);
                if (task.potentialOwners.Contains(player.userID))
                    continue;
                task.potentialOwners.Add(player.userID);
            }
            task.takenItems = items;
            task.endTime = 0.0f;
            task.taskUID = itemCrafter.taskUID;
            task.owner = player;
            task.instanceData = null;
            task.amount = 1;
            task.skinID = 0;
            object obj = Interface.CallHook("OnItemCraft", task, player, null);
            if (obj is bool)
            {
                return;
            }
            itemCrafter.queue.Enqueue(task);
            if(task.owner != null)
                player.Command("note.craft_add", task.taskUID, task.blueprint.targetItem.itemid, 1 );
        }
        #endregion
    }
}