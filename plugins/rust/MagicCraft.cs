using System.Collections.Generic;
using System.Linq;
using Rust;
using Newtonsoft.Json;
using Facepunch.Steamworks;

namespace Oxide.Plugins
{
    [Info("Magic Craft", "Norn", "0.3.4")]
    [Description("An alternative crafting system.")]
    public class MagicCraft : RustPlugin
    {
        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private int MAX_INV_SLOTS = 30;
        private const string PERMISSION = "MagicCraft.able";

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CraftSuccess"] = "You have crafted <color=#66FF66>{0}</color> <color=#66FFFF>{1}</color>\n[Batch Amount: <color=#66FF66>{2}</color>]",
                ["DifferentSlots"] = "You <color=yellow>only</color> have <color=green>{0}</color> slots left, crafting <color=green>{1}</color> / <color=red>{2}</color> requested.",
                ["InventoryFull"] = "Your <color=yellow>inventory</color> is <color=red>full</color>!",
                ["InventoryFullBypass"] = "Magic Craft has been <color=yellow>bypassed</color> because your <color=yellow>inventory</color> is <color=red>full</color>!",
                ["InventoryFullBypassStack"] = "Magic Craft has been <color=yellow>bypassed</color>!\nYou need <color=red>{0}</color> inventory slots free to craft <color=yellow>{1} {2}</color>.",
                ["BypassExcluded"] = "Magic Craft has been <color=yellow>bypassed</color>!\nItem <color=yellow>{0}</color> has been <color=red>excluded</color>."
            }, this);
        }

        private ConfigFile config;
        public class ConfigFile
        {
            [JsonProperty(PropertyName = "BypassInventoryFull")]
            public bool bypassInventoryFull;

            [JsonProperty(PropertyName = "MessagesEnabled")]
            public bool messagesEnabled;

            [JsonProperty(PropertyName = "MessagesItemCrafted")]
            public bool messagesItemCrafted;

            [JsonProperty(PropertyName = "MessagesItemFailed")]
            public bool messagesItemFailed;

            [JsonProperty(PropertyName = "BypassExcluded")]
            public bool messagesBypassExcluded;

            [JsonProperty(PropertyName = "ExcludeList")]
            public List<string> excludeList;

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile()
                {
                    messagesEnabled = true,
                    messagesItemCrafted = false,
                    messagesItemFailed = true,
                    messagesBypassExcluded = true,
                    bypassInventoryFull = true,
                    excludeList = new List<string>{}
                };
            }
        }

        void Finalise()
        {
            permission.RegisterPermission(PERMISSION, this);
            if (config.excludeList.Count != 0) { Puts($"Excluded Items: {config.excludeList.Count} | Total Items: {ItemManager.itemList.Count}"); }
            webrequest.Enqueue("http://s3.amazonaws.com/s3.playrust.com/icons/inventory/rust/schema.json", null, PullSkins, this);
        }

        private void OnServerInitialized() { Finalise(); }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<ConfigFile>();
            if (config == null) { LoadDefaultConfig(); }
        }

        protected override void LoadDefaultConfig()
        {
            config = ConfigFile.DefaultConfig();
            PrintWarning("Default configuration initiated.");
        }

        protected override void SaveConfig() { Config.WriteObject(config); }

        public int InventorySlots(BasePlayer player, bool incwear = true, bool incbelt = true)
        {
            List<Item> list = new List<Item>();
            list.AddRange(player.inventory.containerMain.itemList);                     // 24
            if (incbelt) { list.AddRange(player.inventory.containerBelt.itemList); }    // 6
            if (incwear) { list.AddRange(player.inventory.containerWear.itemList); }    // 6
            return list.Count;
        }

        public int FreeInventorySlots(BasePlayer player, bool incwear = true, bool incbelt = true) { return MAX_INV_SLOTS - InventorySlots(player, false, true); }

        private object OnItemCraft(ItemCraftTask task, BasePlayer crafter)
        {
            if (permission.UserHasPermission(crafter.UserIDString, PERMISSION))
            {
                ItemDefinition item = task.blueprint.targetItem;
                if (config.excludeList.Contains(item.shortname))
                {
                    if (config.messagesBypassExcluded && config.messagesEnabled) { PrintToChatEx(crafter, Lang("BypassExcluded", crafter.UserIDString, item.displayName.english)); }
                    return null;
                }
                if (config.bypassInventoryFull && InventorySlots(crafter, false, true) >= MAX_INV_SLOTS)
                {
                    if (config.messagesEnabled && config.messagesItemFailed) { PrintToChatEx(crafter, Lang("InventoryFullBypass", crafter.UserIDString)); }
                    return null;
                }
                int amount = task.amount;
                int final_amount = task.blueprint.amountToCreate * amount;
                var results = CalculateStacks(final_amount, item);
                if (results.Count() > 1)
                {
                    if (config.bypassInventoryFull && InventorySlots(crafter, false, true) + results.Count() >= MAX_INV_SLOTS)
                    {
                        if (config.messagesEnabled && config.messagesItemFailed) { PrintToChatEx(crafter, Lang("InventoryFullBypassStack", crafter.UserIDString, results.Count(), final_amount.ToString(), item.displayName.english)); }
                        return null;
                    }
                    foreach (var stack_amount in results) { GiveItem(crafter, item, task.skinID, (int)stack_amount); }
                }
                else { GiveItem(crafter, item, task.skinID, final_amount); }
                if (config.messagesEnabled && config.messagesItemCrafted) { PrintToChatEx(crafter, Lang("CraftSuccess", crafter.UserIDString, amount.ToString(), item.displayName.english.ToString(), final_amount.ToString())); }
                return false;
            }
            return null;
        }

        private IEnumerable<int> CalculateStacks(int amount, ItemDefinition item)
        {
            var results = Enumerable.Repeat(item.stackable, amount / item.stackable); if (amount % item.stackable > 0) { results = results.Concat(Enumerable.Repeat(amount % item.stackable, 1)); }
            return results;
        }

        private void GiveItem(BasePlayer player, ItemDefinition item, int skinid, int amount)
        {
            if (!player.IsConnected) return;
            Item itemToGive = ItemManager.Create(item, amount, ItemDefinition.FindSkin(item.itemid, skinid));
            if (itemToGive != null) { player.GiveItem(itemToGive); }
        }

        private void PullSkins(int code, string response)
        {
            if (response != null && code == 200)
            {
                var schema = JsonConvert.DeserializeObject<Rust.Workshop.ItemSchema>(response);
                var defs = new List<Inventory.Definition>();
                foreach (var item in schema.items)
                {
                    if (string.IsNullOrEmpty(item.itemshortname)) continue;
                    var steamItem = Global.SteamServer.Inventory.CreateDefinition((int)item.itemdefid);
                    steamItem.Name = item.name;
                    steamItem.SetProperty("itemshortname", item.itemshortname);
                    steamItem.SetProperty("workshopid", item.workshopid);
                    steamItem.SetProperty("workshopdownload", item.workshopdownload);
                    defs.Add(steamItem);
                }
                Global.SteamServer.Inventory.Definitions = defs.ToArray();
                foreach (var item in ItemManager.itemList) { item._skins2 = Global.SteamServer.Inventory.Definitions.Where(x => (x.GetStringProperty("itemshortname") == item.shortname) && !string.IsNullOrEmpty(x.GetStringProperty("workshopdownload"))).ToArray(); }
                Puts($"Loaded {Global.SteamServer.Inventory.Definitions.Length} approved workshop skins.");
            }
            else { PrintWarning($"Failed to pull skins... Error {code}"); }
        }

        private void PrintToChatEx(BasePlayer player, string result, string tcolour = "#66FF66") { PrintToChat(player, "<color=\"" + tcolour + "\">[" + this.Title.ToString() + "]</color> " + result); }
    }
}