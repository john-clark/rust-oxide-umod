using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using Facepunch.Steamworks;
using Rust;

namespace Oxide.Plugins
{
    [Info("Instant Craft", "Orange", "2.0.5")]
    [Description("Allows players to instantly craft items with features")]
    public class InstantCraft : RustPlugin
    {
        #region Vars

        private const string permUse = "instantcraft.use";
        private const string permRandom = "instantcraft.random";

        #endregion
        
        #region Oxide Hooks

        private void OnServerInitialized()
        {
            OnStart();
            LoadSkins();
        }
        
        private object OnItemCraft(ItemCraftTask item)
        {
            return OnCraft(item);
        }

        #endregion
        
        #region Configuration

        private ConfigData config;
        
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Check for free place")]
            public bool checkPlace;
            
            [JsonProperty(PropertyName = "Normal Speed")]
            public List<string> normal;

            [JsonProperty(PropertyName = "Blacklist")]
            public List<string> blocked;
            
            [JsonProperty(PropertyName = "Split crafted stacks")]
            public bool split;
        }
        
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                normal = new List<string>
                {
                    "hammer"
                },
                blocked = new List<string>
                {
                    "rock"
                },
                checkPlace = false,
                split = false
            };
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            
            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config?.normal == null || config?.blocked == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintError("Configuration file is corrupt(or not exists), creating new one");
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region Helpers

        private void OnStart()
        {
            permission.RegisterPermission(permUse, this);
            lang.RegisterMessages(EN, this);
            permission.RegisterPermission(permRandom, this);
        }

        private object OnCraft(ItemCraftTask task)
        {
            var player = task.owner;
            var target = task.blueprint.targetItem;
            var name = target.shortname;

            if (IsBlocked(name))
            {
                task.cancelled = true;
                message(player, "Blocked");
                GiveRefund(player, task.takenItems);
                return null;
            }
            
            if (!HasPerm(player, permUse))
            {
                return null;
            }

            var stacks = GetStacks(target, task.amount * task.blueprint.amountToCreate);
            var slots = FreeSlots(player);

            if (!HasPlace(slots, stacks))
            {
                task.cancelled = true;
                message(player, "Slots", stacks.Count, slots);
                GiveRefund(player, task.takenItems);
                return null;
            }
            
            if (IsNormalItem(name))
            {
                message(player, "Normal");
                return null;
            }
            
            GiveItem(player, target, stacks, task.skinID);
            task.cancelled = true;
            return null;
        }

        private void GiveItem(BasePlayer player, ItemDefinition item, List<int> stacks, int craftSkin)
        {
            var skin = ItemDefinition.FindSkin(item.itemid, craftSkin);

            if (skin == 0 && HasPerm(player, permRandom))
            {
                skin = GetRandomSkin(item);
            }
            
            if (!config.split)
            {
                var final = 0;

                foreach (var i in stacks)
                {
                    final += i;
                }
                
                var x = ItemManager.Create(item, final, skin);
                player.GiveItem(x);
                return;
            }
            
            foreach (var stack in stacks)
            {
                var x = ItemManager.Create(item, stack, skin);
                player.GiveItem(x);
            }
        }

        private int FreeSlots(BasePlayer player)
        {
            var slots = player.inventory.containerMain.capacity + player.inventory.containerBelt.capacity;
            var taken = player.inventory.containerMain.itemList.Count + player.inventory.containerBelt.itemList.Count;
            return slots - taken;
        }

        private void GiveRefund(BasePlayer player, List<Item> items)
        {
            foreach (var item in items)
            {
                player.GiveItem(item);
            }
        }

        private List<int> GetStacks(ItemDefinition item, int amount) 
        {
            var list = new List<int>();
            var maxStack = item.stackable;

            while (amount > maxStack)
            {
                amount -= maxStack;
                list.Add(maxStack);
            }
            
            list.Add(amount);
            
            return list; 
        }

        private bool IsNormalItem(string name)
        {
            return config.normal.Contains(name);
        }

        private bool IsBlocked(string name)
        {
            return config.blocked.Contains(name);
        }

        private bool HasPlace(int slots, List<int> stacks)
        {
            if (!config.checkPlace)
            {
                return true;
            }

            if (config.split && slots - stacks.Count < 0)
            {
                return false;
            }

            return slots > 0;
        }

        private bool HasPerm(BasePlayer player, string perm)
        {
            return permission.UserHasPermission(player.UserIDString, perm);
        }
        
        private ulong GetRandomSkin(ItemDefinition def)
        {
            if (def.skins.Length == 0 && def.skins2.Length == 0) {return 0;}
            var skins = new List<int> {0};
            if (def.skins != null) skins.AddRange(def.skins.Select(skin => skin.id));
            if (def.skins2 != null) skins.AddRange(def.skins2.Select(skin => skin.Id));
            var value = ItemDefinition.FindSkin(def.itemid, skins.GetRandom());
            var final = Convert.ToUInt64(value);
            return final;
        }
        
        private void LoadSkins()
        {
            webrequest.Enqueue("http://s3.amazonaws.com/s3.playrust.com/icons/inventory/rust/schema.json", null, ReadScheme, this);
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
                {
                    item._skins2 = Global.SteamServer.Inventory.Definitions.Where(x => (x.GetStringProperty("itemshortname") == item.shortname) && !string.IsNullOrEmpty(x.GetStringProperty("workshopdownload"))).ToArray();
                }
                    
                Puts($"Loaded {Global.SteamServer.Inventory.Definitions.Length} approved workshop skins.");
            }
            else
            {
                PrintWarning($"Failed to load approved workshop skins... Error {code}");
            }
        }

        #endregion

        #region Localization
        
        private Dictionary<string, string> EN = new Dictionary<string, string>
        {
            {"Blocked", "Crafting of that item is blocked!"},
            {"Slots", "You don't have enough place to craft! Need {0}, have {1}!"},
            {"Normal", "Item will be crafted with normal speed."}
        };
        
        private void message(BasePlayer player, string key, params object[] args)
        {
            player.ChatMessage(string.Format(lang.GetMessage(key, this, player.UserIDString), args));
        }

        #endregion
    }
}