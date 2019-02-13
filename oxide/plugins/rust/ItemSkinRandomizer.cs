using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Facepunch.Steamworks;
using Rust;

namespace Oxide.Plugins
{
    [Info("Item Skin Randomizer", "Mughisi", "1.3.3", ResourceId = 1328)]
    [Description("Simple plugin that will select a random skin for an item when crafting.")]
    class ItemSkinRandomizer : RustPlugin
    {
        private RandomizerConfig config;
        private readonly Dictionary<string, List<int>> skinsCache = new Dictionary<string, List<int>>();
        private readonly List<int> randomizedTasks = new List<int>();

        public class RandomizerConfig
        {
            public bool EnablePermissions;
            public bool EnableDefaultSkin;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<RandomizerConfig>();
        }

        protected override void LoadDefaultConfig()
        {
            config = new RandomizerConfig { EnableDefaultSkin = true, EnablePermissions = false };
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private void OnServerInitialized()
        {
            webrequest.Enqueue("http://s3.amazonaws.com/s3.playrust.com/icons/inventory/rust/schema.json", null, ReadScheme, this);
            if (config.EnablePermissions) permission.RegisterPermission("itemskinrandomizer.use", this);
        }

        private void OnItemCraft(ItemCraftTask task, BasePlayer crafter)
        {
            if (config.EnablePermissions && !permission.UserHasPermission(crafter.UserIDString, "itemskinrandomizer.use")) return;
            var skins = GetSkins(task.blueprint.targetItem);
            if (skins.Count == 0 || task.skinID != 0) return;
            randomizedTasks.Add(task.taskUID);
            task.skinID = skins.GetRandom();
        }

        private void OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            if (!randomizedTasks.Contains(task.taskUID)) return;
            if (task.amount == 0)
            {
                randomizedTasks.Remove(task.taskUID);
                return;
            }
            var skins = GetSkins(task.blueprint.targetItem);
            task.skinID = skins.GetRandom();
        }

        private void ReadScheme(int code, string response)
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

                foreach (var item in ItemManager.itemList)
                    item._skins2 = Global.SteamServer.Inventory.Definitions.Where(x => (x.GetStringProperty("itemshortname") == item.shortname) && !string.IsNullOrEmpty(x.GetStringProperty("workshopdownload"))).ToArray();

                Puts($"Loaded {Global.SteamServer.Inventory.Definitions.Length} approved workshop skins.");
            }
            else
            {
                PrintWarning($"Failed to load approved workshop skins... Error {code}");
            }
        }

        private List<int> GetSkins(ItemDefinition def)
        {
            List<int> skins;
            if (skinsCache.TryGetValue(def.shortname, out skins)) return skins;
            skins = new List<int>();
            if (config.EnableDefaultSkin) skins.Add(0);
            if (def.skins != null) skins.AddRange(def.skins.Select(skin => skin.id));
            if (def.skins2 != null) skins.AddRange(def.skins2.Select(skin => skin.Id));
            skinsCache.Add(def.shortname, skins);
            return skins;
        }
    }
}