using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("ClothingSlots", "Jake_Rich", "1.1.0")]
    [Description("Available Inventory Slots Depends On Clothing Worn")]

    public partial class ClothingSlots : RustPlugin
    {
        public static ClothingSlots _plugin;
        public static JSONFile<ConfigData> _settingsFile; //I know static stuff persists, it's handled in this case :)
        public static ConfigData Settings { get { return _settingsFile.Instance; } }
        public PlayerDataController<SlotPlayerData> PlayerData;

        void Init()
        {
            _plugin = this;
        }

        void Loaded()
        {
            _settingsFile = new JSONFile<ConfigData>($"{Name}", ConfigLocation.Config, extension: ".cfg");
            PlayerData = new PlayerDataController<SlotPlayerData>();
            if (lang_en.Count > 0)
            {
                lang.RegisterMessages(lang_en, this); //Setup lang now by default in case it is needed
            }
        }

        void OnServerInitialized()
        {
            Settings.Setup();

            foreach(var player in BasePlayer.activePlayerList)
            {
                OnPlayerInit(player);
            }
        }

        void Unload()
        {
            foreach(var data in PlayerData.All)
            {
                //Reset slot limit when unloading plugin
                data.SetSlots(30);
            }
            PlayerData.Unload();
        }

        void OnPlayerInit(BasePlayer player)
        {
            PlayerData.Get(player).UpdateSlots();
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            PlayerData.Get(player).UpdateSlots();
        }

        object CanMoveItem(Item movedItem, PlayerInventory inventory, uint targetContainerID, int targetSlot, int amount)
        {
            var container = inventory.FindContainer(targetContainerID);
            var player = inventory.GetComponent<BasePlayer>();

            if (targetSlot != -1)
            {
                return null;
            }

            if (movedItem.parent != inventory.containerBelt)
            {
                return null;
            }

            if (movedItem.info.GetComponent<ItemModWearable>() != null)
            {
                if (movedItem.MoveToContainer(inventory.containerWear))
                {
                    return false;
                }
            }
            return null;
        }

        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            var player = container.playerOwner as BasePlayer;
            if (player == null)
            {
                return;
            }
            if (container != player.inventory.containerWear)
            {
                return;
            }
            PlayerData.Get(player).UpdateSlots();
        }

        void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            var player = container.playerOwner as BasePlayer;
            if (player == null)
            {
                return;
            }
            if (container != player.inventory.containerWear)
            {
                return;
            }
            NextFrame(() =>
            {
                //NextFrame it, so when clothing is put into the inventory it will move when capacity decreases
                if (player == null)
                {
                    return;
                }
                if (player.IsDead())
                {
                    return;
                }
                PlayerData.Get(player).UpdateSlots();
            });
        }

        public class ClothingSetting
        {
            public int Slots = 0;
            public float Stacksize = 1f;

            public ClothingSetting()
            {

            }

            public ClothingSetting(ItemDefinition clothing)
            {

            }
        }

        public class ConfigData
        {
            public int DefaultSlots = 6;
            public Dictionary<string, ClothingSetting> Clothing = new Dictionary<string, ClothingSetting>() //Default values are as follows
            {
                {"burlap.shirt", new ClothingSetting() { Slots = 3, } },
                {"burlap.shoes", new ClothingSetting() { Slots = 2, } },
                {"facialhair.style01", new ClothingSetting() { Slots = 0, } },
                {"femalearmpithair.style01", new ClothingSetting() { Slots = 0, } },
                {"femaleeyebrow.style01", new ClothingSetting() { Slots = 0, } },
                {"femalepubichair.style01", new ClothingSetting() { Slots = 0, } },
                {"female_hairstyle_01", new ClothingSetting() { Slots = 0, } },
                {"female_hairstyle_03", new ClothingSetting() { Slots = 0, } },
                {"burlap.gloves", new ClothingSetting() { Slots = 2, } },
                {"gloweyes", new ClothingSetting() { Slots = 0, } },
                {"male_hairstyle_01", new ClothingSetting() { Slots = 0, } },
                {"female_hairstyle_02", new ClothingSetting() { Slots = 0, } },
                {"attire.hide.helterneck", new ClothingSetting() { Slots = 3, } },
                {"hat.beenie", new ClothingSetting() { Slots = 2, } },
                {"hat.boonie", new ClothingSetting() { Slots = 2, } },
                {"bucket.helmet", new ClothingSetting() { Slots = 0, } },
                {"burlap.headwrap", new ClothingSetting() { Slots = 2, } },
                {"hat.candle", new ClothingSetting() { Slots = 0, } },
                {"hat.cap", new ClothingSetting() { Slots = 0, } },
                {"coffeecan.helmet", new ClothingSetting() { Slots = 0, } },
                {"deer.skull.mask", new ClothingSetting() { Slots = 0, } },
                {"heavy.plate.helmet", new ClothingSetting() { Slots = 4, } },
                {"hat.miner", new ClothingSetting() { Slots = 0, } },
                {"attire.reindeer.headband", new ClothingSetting() { Slots = 0, } },
                {"riot.helmet", new ClothingSetting() { Slots = 0, } },
                {"hat.wolf", new ClothingSetting() { Slots = 0, } },
                {"wood.armor.helmet", new ClothingSetting() { Slots = 0, } },
                {"hazmatsuit", new ClothingSetting() { Slots = 15, } },
                {"attire.hide.boots", new ClothingSetting() { Slots = 3, } },
                {"attire.hide.skirt", new ClothingSetting() { Slots = 3, } },
                {"attire.hide.vest", new ClothingSetting() { Slots = 5, } },
                {"hoodie", new ClothingSetting() { Slots = 12, } },
                {"bone.armor.suit", new ClothingSetting() { Slots = 6, } },
                {"heavy.plate.jacket", new ClothingSetting() { Slots = 4, } },
                {"jacket.snow", new ClothingSetting() { Slots = 16, } },
                {"jacket", new ClothingSetting() { Slots = 8, } },
                {"male.facialhair.style02", new ClothingSetting() { Slots = 0, } },
                {"male_hairstyle_02", new ClothingSetting() { Slots = 0, } },
                {"malearmpithair.style01", new ClothingSetting() { Slots = 0, } },
                {"maleeyebrow.style01", new ClothingSetting() { Slots = 0, } },
                {"malepubichair.style01", new ClothingSetting() { Slots = 0, } },
                {"male_hairstyle_03", new ClothingSetting() { Slots = 0, } },
                {"mask.balaclava", new ClothingSetting() { Slots = 2, } },
                {"mask.bandana", new ClothingSetting() { Slots = 2, } },
                {"metal.facemask", new ClothingSetting() { Slots = 0, } },
                {"metal.plate.torso", new ClothingSetting() { Slots = 0, } },
                {"burlap.trousers", new ClothingSetting() { Slots = 3, } },
                {"pants", new ClothingSetting() { Slots = 8, } },
                {"heavy.plate.pants", new ClothingSetting() { Slots = 4, } },
                {"attire.hide.pants", new ClothingSetting() { Slots = 4, } },
                {"roadsign.kilt", new ClothingSetting() { Slots = 0, } },
                {"pants.shorts", new ClothingSetting() { Slots = 4, } },
                {"attire.hide.poncho", new ClothingSetting() { Slots = 6, } },
                {"pumpkin", new ClothingSetting() { Slots = 0, } },
                {"roadsign.jacket", new ClothingSetting() { Slots = 0, } },
                {"santahat", new ClothingSetting() { Slots = 0, } },
                {"shirt.collared", new ClothingSetting() { Slots = 4, } },
                {"shirt.tanktop", new ClothingSetting() { Slots = 4, } },
                {"shoes.boots", new ClothingSetting() { Slots = 4, } },
                {"tshirt", new ClothingSetting() { Slots = 8, } },
                {"tshirt.long", new ClothingSetting() { Slots = 10, } },
                {"wood.armor.jacket", new ClothingSetting() { Slots = 0, } },
                {"wood.armor.pants", new ClothingSetting() { Slots = 0, } },
            };

            public void Setup()
            {
                var clothing = ItemManager.itemList.Where(x => x.GetComponent<ItemModWearable>());
                bool modified = false;
                foreach(var attire in clothing)
                {
                    if (!Clothing.ContainsKey(attire.shortname))
                    {
                        Clothing.Add(attire.shortname, new ClothingSetting(attire));
                        modified = true;
                    }
                }
                if (modified)
                {
                    _settingsFile.Save();
                }
                //Outputs default values I put above
                //_plugin.Puts(string.Join("\n", Clothing.Select(x=>$"{{\"{x.Key}\", new ClothingSetting() {{ Slots = {x.Value.Slots}, }} }},").ToArray()));

            }
        }

        public class SlotPlayerData : BasePlayerData
        {
            public void SetSlots(int slots)
            {
                if (Player == null)
                {
                    return;
                }
                Player.inventory.containerBelt.MarkDirty();
                Player.inventory.containerMain.MarkDirty();
                if (slots <= 6)
                {
                    Player.inventory.containerBelt.capacity = slots;
                    Player.inventory.containerMain.capacity = 0;
                    UpdateInventories();
                    return;
                }
                Player.inventory.containerBelt.capacity = 6;
                if (slots >= 30)
                {
                    Player.inventory.containerMain.capacity = 24;
                }
                else
                {
                    Player.inventory.containerMain.capacity = slots - 6;
                }
                UpdateInventories();
            }

            private void UpdateInventory(ItemContainer container)
            {
                foreach (var invalidItem in container.itemList.ToList())
                {
                    if (invalidItem.position >= container.capacity)
                    {
                        bool hasMovedItem = false;
                        for (int slot = 0; slot < Player.inventory.containerMain.capacity; slot++)
                        {
                            var slotItem = container.GetSlot(slot);
                            if (slotItem != null && invalidItem != null)
                            {
                                if (slotItem.CanStack(invalidItem))
                                {
                                    int maxStack = slotItem.MaxStackable();
                                    if (slotItem.amount < maxStack)
                                    {
                                        slotItem.amount += invalidItem.amount;
                                        if (slotItem.amount > maxStack)
                                        {
                                            invalidItem.amount = slotItem.amount - maxStack;
                                            slotItem.amount = maxStack;
                                        }
                                        else
                                        {
                                            //Combined item
                                            hasMovedItem = true;
                                            invalidItem.Remove();
                                            ItemManager.DoRemoves();
                                            break;
                                        }
                                    }
                                }
                                continue;
                            }
                            invalidItem.position = slot;
                            hasMovedItem = true;
                            break;
                        }
                        if (hasMovedItem == false)
                        {
                            invalidItem.Drop(Player.GetDropPosition(), Player.GetDropVelocity());
                        }
                    }
                }
            }

            private void UpdateInventories()
            {
                if (Player.inventory.containerBelt.capacity < 6)
                {
                    UpdateInventory(Player.inventory.containerBelt);
                }
                if (Player.inventory.containerMain.capacity < 24)
                {
                    UpdateInventory(Player.inventory.containerMain);
                }
            }

            public void UpdateSlots()
            {
                int targetSlots = Settings.DefaultSlots;
                foreach(var clothing in Player.inventory.containerWear.itemList)
                {
                    targetSlots += Settings.Clothing[clothing.info.shortname].Slots;
                }
                SetSlots(targetSlots);
            }
        }

        #region Lang API

        public Dictionary<string, string> lang_en = new Dictionary<string, string>()
        {
            
        };

        public static string GetLangMessage(string key, BasePlayer player)
        {
            return _plugin.lang.GetMessage(key, _plugin, player.UserIDString);
        }

        public static string GetLangMessage(string key, ulong player)
        {
            return _plugin.lang.GetMessage(key, _plugin, player.ToString());
        }

        public static string GetLangMessage(string key, string player)
        {
            return _plugin.lang.GetMessage(key, _plugin, player);
        }

        #endregion

        #region PlayerData

        public class BasePlayerData
        {
            [JsonIgnore]
            public BasePlayer Player { get; set; }

            public string userID { get; set; } = "";

            public BasePlayerData()
            {

            }
            public BasePlayerData(BasePlayer player) : base()
            {
                userID = player.UserIDString;
                Player = player;
            }
        }

        public class PlayerDataController<T> where T : BasePlayerData
        {
            [JsonPropertyAttribute(Required = Required.Always)]
            private Dictionary<string, T> playerData { get; set; } = new Dictionary<string, T>();
            private JSONFile<Dictionary<string, T>> _file;
            private Timer _timer;
            public IEnumerable<T> All { get { return playerData.Values; } }

            public PlayerDataController()
            {

            }

            public PlayerDataController(string filename = null)
            {
                if (filename == null)
                {
                    return;
                }
                _file = new JSONFile<Dictionary<string, T>>(filename);
                _timer = _plugin.timer.Every(120f, () =>
                {
                    _file.Save();
                });
            }

            public void Unload()
            {
                if (_file == null)
                {
                    return;
                }
                _file.Save();
            }

            public T Get(string identifer)
            {
                T data;
                if (!playerData.TryGetValue(identifer, out data))
                {
                    data = Activator.CreateInstance<T>();
                    playerData[identifer] = data;
                }
                return data;
            }

            public T Get(ulong userID)
            {
                return Get(userID.ToString());
            }

            public T Get(BasePlayer player)
            {
                var data = Get(player.UserIDString);
                data.Player = player;
                return data;
            }

            public bool Has(ulong userID)
            {
                return playerData.ContainsKey(userID.ToString());
            }

            public void Set(string userID, T data)
            {
                playerData[userID] = data;
            }

            public bool Remove(string userID)
            {
                return playerData.Remove(userID);
            }

            public void Update(T data)
            {
                playerData[data.userID] = data;
            }
        }

        #endregion

        #region Configuration Files

        public enum ConfigLocation
        {
            Data = 0,
            Config = 1,
            Logs = 2,
            Plugins = 3,
            Lang = 4,
            Custom = 5,
        }

        public class JSONFile<Type> where Type : class
        {
            private DynamicConfigFile _file;
            public string _name { get; set; }
            public Type Instance { get; set; }
            private ConfigLocation _location { get; set; }
            private string _path { get; set; }

            public JSONFile(string name, ConfigLocation location = ConfigLocation.Data, string path = null, string extension = ".json")
            {
                _name = name.Replace(".json", "");
                _location = location;
                switch (location)
                {
                    case ConfigLocation.Data:
                        {
                            _path = $"{Oxide.Core.Interface.Oxide.DataDirectory}/{name}{extension}";
                            break;
                        }
                    case ConfigLocation.Config:
                        {
                            _path = $"{Oxide.Core.Interface.Oxide.ConfigDirectory}/{name}{extension}";
                            break;
                        }
                    case ConfigLocation.Logs:
                        {
                            _path = $"{Oxide.Core.Interface.Oxide.LogDirectory}/{name}{extension}";
                            break;
                        }
                    case ConfigLocation.Lang:
                        {
                            _path = $"{Oxide.Core.Interface.Oxide.LangDirectory}/{name}{extension}";
                            break;
                        }
                    case ConfigLocation.Custom:
                        {
                            _path = $"{path}/{name}{extension}";
                            break;
                        }
                }
                _file = new DynamicConfigFile(_path);
                _file.Settings = new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
                Init();
            }

            public virtual void Init()
            {
                Load();
                Save();
                Load();
            }

            public virtual void Load()
            {

                if (!_file.Exists())
                {
                    Save();
                }
                Instance = _file.ReadObject<Type>();
                if (Instance == null)
                {
                    Instance = Activator.CreateInstance<Type>();
                    Save();
                }
                return;
            }

            public virtual void Save()
            {
                _file.WriteObject(Instance);
                return;
            }

            public virtual void Reload()
            {
                Load();
            }

            private void Unload(Plugin sender, PluginManager manager)
            {

            }
        }

        #endregion

    }
}