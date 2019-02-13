using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Underworld", "nivex", "1.0.4")]
    [Description("Teleports admins/developer under the world when they disconnect.")]
    public class Underworld : RustPlugin
	{
        [PluginReference]
        Plugin Vanish;

        const string permBlocked = "underworld.blocked";
        const string permName = "underworld.use";
        StoredData storedData = new StoredData();
        DynamicConfigFile dataFile;
        bool newSave;

        public class StoredData
        {
            public Dictionary<string, UserInfo> Users = new Dictionary<string, UserInfo>();
            public StoredData() { }
        }

        public class UserInfo
        {
            public string Home { get; set; } = Vector3.zero.ToString();
            public bool WakeOnLand { get; set; } = true;
            public bool SaveInventory { get; set; } = true;
            public bool AutoNoClip { get; set; } = true;
            public List<UnderworldItem> Items { get; set; } = new List<UnderworldItem>();

            public UserInfo() { }
        }

        public class UnderworldItem
        {
            public string container { get; set; } = "main";
            public string shortname { get; set; } = null;
            public int itemid { get; set; } = 0;
            public ulong skinID { get; set; } = 0;
            public int amount { get; set; } = 0;
            public float condition { get; set; } = 0;
            public float maxCondition { get; set; } = 0;
            public int position { get; set; } = -1;
            public float fuel { get; set; } = 0;
            public int keyCode { get; set; } = 0;
            public int ammo { get; set; } = 0;
            public string ammoTypeShortname { get; set; } = null;
            public string fogImages { get; set; } = null;
            public string paintImages { get; set; } = null;
            public List<UnderworldMod> contents { get; set; } = null;

            public UnderworldItem() { }

            public UnderworldItem(string container, Item item)
            {
                if (item == null)
                    return;

                this.container = container;
                this.shortname = ItemManager.FindItemDefinition(item.info.shortname).shortname;
                this.itemid = item.info.itemid;
                this.skinID = item.skin;
                this.amount = item.amount;
                this.condition = item.condition;
                this.maxCondition = item.maxCondition;
                this.position = item.position;
                this.fuel = item.fuel;
                this.keyCode = item.instanceData?.dataInt ?? 0;
                this.ammo = item?.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine?.contents ?? 0;
                this.ammoTypeShortname = item.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine?.ammoType?.shortname ?? null;

                var mapEntity = item.GetHeldEntity() as MapEntity;

                if (mapEntity != null)
                {
                    this.fogImages = JsonConvert.SerializeObject(mapEntity.fogImages);
                    this.paintImages = JsonConvert.SerializeObject(mapEntity.paintImages);
                }

                if (item.contents?.itemList?.Count > 0)
                {
                    this.contents = new List<UnderworldMod>();

                    foreach (var mod in item.contents.itemList)
                    {
                        this.contents.Add(new UnderworldMod
                        {
                            shortname = mod.info.shortname,
                            amount = mod.amount,
                            condition = mod.condition,
                            maxCondition = mod.maxCondition,
                            itemid = mod.info.itemid
                        });
                    }
                }
            }
        }

        public class UnderworldMod
        {
            public string shortname { get; set; } = null;
            public int amount { get; set; } = 0;
            public float condition { get; set; } = 0f;
            public float maxCondition { get; set; } = 0f;
            public int itemid { get; set; } = 0;
            public UnderworldMod() { }
        }

        void OnNewSave()
        {
            newSave = true;
        }

        void Init()
        {
            Unsubscribe(nameof(OnPlayerDisconnected));
            Unsubscribe(nameof(OnPlayerInit));
            Unsubscribe(nameof(OnPlayerSleepEnded));
        }

        void Loaded()
        {
            permission.RegisterPermission(permBlocked, this);
            permission.RegisterPermission(permName, this);
            dataFile = Interface.Oxide.DataFileSystem.GetFile(Name);

            try
            {
                storedData = dataFile.ReadObject<StoredData>();
            }
            catch { }

            if (storedData == null)
                storedData = new StoredData();

            LoadVariables();
        }
        
        void OnServerInitialized()
        {
            Subscribe(nameof(OnPlayerDisconnected));
            Subscribe(nameof(OnPlayerInit));
            Subscribe(nameof(OnPlayerSleepEnded));

            if (wipeSaves && newSave)
            {
                foreach(var entry in storedData.Users.ToList())
                {
                    entry.Value.Items.Clear();
                }

                newSave = false;
                SaveData();
            }
        }

        void Unload()
        {
            SaveData();
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
		{
            if (player.IsDead())
                return;

            var user = GetUser(player);

            if (user == null)
                return;

            var userHome = user.Home.ToVector3();
            var position = userHome != Vector3.zero ? userHome : defaultPos;

            if (position == Vector3.zero)
                player.Teleport(new Vector3(player.transform.position.x, TerrainMeta.HeightMap.GetHeight(player.transform.position) - 5f, player.transform.position.z));
            else
                player.Teleport(position);

            SaveInventory(player, user);
        }

        void OnPlayerInit(BasePlayer player)
        {
            var user = GetUser(player);

            if (user == null)
                return;

            if (player.IsDead() || player.IsSleeping())
            {
                timer.Once(0.5f, () => OnPlayerInit(player));
                return;
            }

            if (autoVanish)
            {
                Vanish?.Call("Disappear", player);
            }

            if (user.WakeOnLand)
            {
                float y = TerrainMeta.HeightMap.GetHeight(player.transform.position);
                player.Teleport(new Vector3(player.transform.position.x, y + 1f, player.transform.position.z));
            }

            if (user.AutoNoClip)
                player.SendConsoleCommand("noclip");
        }

		void OnPlayerSleepEnded(BasePlayer player)
		{
            if (!player || !player.IsConnected)
            {
                return;
            }

            var user = GetUser(player);

            if (user == null)
                return;

            if (maxHHT)
            {
                player.health = 100f;
                player.metabolism.hydration.value = player.metabolism.hydration.max;
                player.metabolism.calories.value = player.metabolism.calories.max;
            }

            if (allowSaveInventory && user.SaveInventory && user.Items.Count > 0)
            {
                if (user.Items.Any(item => item.amount > 0))
                {
                    foreach (var uwi in user.Items.ToList())
                    {
                        RestoreItem(player, uwi);
                    }
                }

                user.Items.Clear();
                SaveData();
            }

            if (autoVanish)
            {
                timer.Once(2f, () =>
                {
                    if (!player || !player.IsConnected)
                        return;

                    Vanish?.Call("VanishGui", player);
                });
            }
        }

        [ChatCommand("uw")]
        private void cmdUnderworld(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowed(player))
            {
                player.ChatMessage(msg("NoPermission", player.UserIDString));
                return;
            }

            var user = GetUser(player);

            if (user == null)
            {
                player.ChatMessage(msg("NoPermission", player.UserIDString));
                return;
            }

            if (args.Length >= 1)
            {
                switch (args[0].ToLower())
                {
                    case "save":
                        {
                            if (!allowSaveInventory)
                                return;
                            
                            user.SaveInventory = !user.SaveInventory;
                            player.ChatMessage(msg(user.SaveInventory ? "SavingInventory" : "NotSavingInventory", player.UserIDString));
                            SaveData();
                        }
                        return;
                    case "set":
                        {
                            var position = player.transform.position;

                            if (args.Length == 4)
                            {
                                if (args[1].All(char.IsDigit) && args[2].All(char.IsDigit) && args[3].All(char.IsDigit))
                                {
                                    var customPos = new Vector3(float.Parse(args[1]), 0f, float.Parse(args[3]));

                                    if (Vector3.Distance(customPos, Vector3.zero) <= TerrainMeta.Size.x / 1.5f)
                                    {
                                        customPos.y = float.Parse(args[2]);

                                        if (customPos.y > -100f && customPos.y < 4400f)
                                            position = customPos;
                                        else
                                            player.ChatMessage(msg("OutOfBounds", player.UserIDString));
                                    }
                                    else
                                        player.ChatMessage(msg("OutOfBounds", player.UserIDString));
                                }
                                else
                                    player.ChatMessage(msg("Help1", player.UserIDString, FormatPosition(user.Home.ToVector3())));
                            }

                            user.Home = position.ToString();
                            player.ChatMessage(msg("PositionAdded", player.UserIDString, FormatPosition(position)));
                            SaveData();
                        }
                        return;
                    case "reset":
                        {
                            user.Home = Vector3.zero.ToString();

                            if (defaultPos != Vector3.zero)
                            {
                                user.Home = defaultPos.ToString();
                                player.ChatMessage(msg("PositionRemoved2", player.UserIDString, user.Home));
                            }
                            else
                                player.ChatMessage(msg("PositionRemoved1", player.UserIDString));

                            SaveData();
                        }
                        return;
                    case "wakeup":
                        {
                            user.WakeOnLand = !user.WakeOnLand;
                            player.ChatMessage(msg(user.WakeOnLand ? "PlayerWakeUp" : "PlayerWakeUpReset", player.UserIDString));
                            SaveData();
                        }
                        return;
                    case "noclip":
                        {
                            user.AutoNoClip = !user.AutoNoClip;
                            player.ChatMessage(msg(user.AutoNoClip ? "PlayerNoClipEnabled" : "PlayerNoClipDisabled", player.UserIDString));
                            SaveData();
                        }
                        return;
                    case "g":
                    case "ground":
                        {
                            player.Teleport(new Vector3(player.transform.position.x, TerrainMeta.HeightMap.GetHeight(player.transform.position) + 1f, player.transform.position.z));
                        }
                        return;
                }
            }

            string homePos = FormatPosition(user.Home.ToVector3() == Vector3.zero ? defaultPos : user.Home.ToVector3());

            player.ChatMessage(msg("Help0", player.UserIDString, user.SaveInventory && allowSaveInventory));
            player.ChatMessage(msg("Help1", player.UserIDString, homePos));
            player.ChatMessage(msg("Help2", player.UserIDString));
            player.ChatMessage(msg("Help3", player.UserIDString, user.WakeOnLand));
            player.ChatMessage(msg("Help4", player.UserIDString, user.AutoNoClip));
            player.ChatMessage(msg("Help5", player.UserIDString));
        }

        UserInfo GetUser(BasePlayer player)
        {
            if (!player || !player.IsConnected || !IsAllowed(player) || permission.UserHasPermission(player.UserIDString, permBlocked))
                return null;

            if (!storedData.Users.ContainsKey(player.UserIDString))
                storedData.Users.Add(player.UserIDString, new UserInfo());

            return storedData.Users[player.UserIDString];
        }

        public string FormatPosition(Vector3 position)
        {
            string x = position.x.ToString("N2");
            string y = position.y.ToString("N2");
            string z = position.z.ToString("N2");

            return $"{x} {y} {z}";
        }

        void SaveData()
        {
            if (dataFile != null && storedData != null)
            {
                dataFile.WriteObject(storedData);
            }
        }

        void SaveInventory(BasePlayer player, UserInfo user)
        {
            if (!allowSaveInventory || !user.SaveInventory)
                return;

            if (player.inventory.AllItems().Count() == 0)
            {
                user.Items.Clear();
                SaveData();
                return;
            }

            var items = new List<UnderworldItem>();

            foreach (Item item in player.inventory.containerWear.itemList.Where(item => !Blacklisted(item)).ToList())
            {
                if (item.info.shortname == "map")
                    continue;

                items.Add(new UnderworldItem("wear", item));
                item.Remove(0.01f);
            }

            foreach (Item item in player.inventory.containerMain.itemList.Where(item => !Blacklisted(item)).ToList())
            {
                if (item.info.shortname == "map")
                    continue;

                items.Add(new UnderworldItem("main", item));
                item.Remove(0.01f);
            }

            foreach (Item item in player.inventory.containerBelt.itemList.Where(item => !Blacklisted(item)).ToList())
            {
                if (item.info.shortname == "map")
                    continue;

                items.Add(new UnderworldItem("belt", item));
                item.Remove(0.01f);
            }

            if (items.Count == 0)
            {
                return;
            }

            ItemManager.DoRemoves();
            user.Items.Clear();
            user.Items.AddRange(items);
            SaveData();
        }

        private void RestoreItem(BasePlayer player, UnderworldItem uwi)
        {
            if (uwi.itemid == 0 || uwi.amount < 1 || string.IsNullOrEmpty(uwi.container))
                return;

            Item item = ItemManager.CreateByItemID(uwi.itemid, uwi.amount, uwi.skinID);

            if (item == null)
                return;

            if (item.hasCondition)
            {
                item.maxCondition = uwi.maxCondition; // restore max condition after repairs
                item.condition = uwi.condition; // repair last known condition
            }

            item.fuel = uwi.fuel;

            var heldEntity = item.GetHeldEntity();

            if (heldEntity != null)
            {
                if (item.skin != 0)
                    heldEntity.skinID = item.skin;

                var weapon = heldEntity as BaseProjectile;

                if (weapon != null)
                {
                    if (!string.IsNullOrEmpty(uwi.ammoTypeShortname))
                    {
                        weapon.primaryMagazine.ammoType = ItemManager.FindItemDefinition(uwi.ammoTypeShortname);
                    }

                    weapon.primaryMagazine.contents = 0; // unload the old ammo
                    weapon.SendNetworkUpdateImmediate(false); // update
                    weapon.primaryMagazine.contents = weapon.primaryMagazine.capacity; // load new ammo
                    weapon.primaryMagazine.contents = uwi.ammo; // load new ammo
                }
            }

            if (uwi.contents != null)
            {
                foreach (var uwm in uwi.contents)
                {
                    Item mod = ItemManager.CreateByItemID(uwm.itemid, 1);

                    if (mod == null)
                        continue;

                    if (mod.hasCondition)
                    {
                        mod.maxCondition = uwm.maxCondition; // restore max condition after repairs
                        mod.condition = uwm.condition; // repair last known condition
                    }

                    item.contents.AddItem(mod.info, Math.Max(uwm.amount, 1)); // restore attachments / water amount
                }
            }

            if (uwi.keyCode != 0) // restore key data
            {
                item.instanceData = Facepunch.Pool.Get<ProtoBuf.Item.InstanceData>();
                item.instanceData.ShouldPool = false;
                item.instanceData.dataInt = uwi.keyCode;
            }

            if (!string.IsNullOrEmpty(uwi.fogImages) && !string.IsNullOrEmpty(uwi.paintImages))
            {
                var mapEntity = item.GetHeldEntity() as MapEntity;

                if (mapEntity != null)
                {
                    mapEntity.SetOwnerPlayer(player);
                    mapEntity.fogImages = JsonConvert.DeserializeObject<uint[]>(uwi.fogImages);
                    mapEntity.paintImages = JsonConvert.DeserializeObject<uint[]>(uwi.paintImages);
                }
            }

            item.MarkDirty();

            var container = uwi.container == "belt" ? player.inventory.containerBelt : uwi.container == "wear" ? player.inventory.containerWear : player.inventory.containerMain;

            if (!item.MoveToContainer(container, uwi.position, true))
            {
                if (!item.MoveToContainer(player.inventory.containerMain, -1, true))
                {
                    item.Remove(0.01f);
                }
            }
        }

        bool IsAllowed(BasePlayer player) => player != null && (player.IsAdmin || player.IsDeveloper || HasPermission(player) || player.net?.connection?.authLevel > 0u);
        bool Blacklisted(Item item) => Blacklist.Contains(item.info.shortname) || Blacklist.Contains(item.info.itemid.ToString());
        bool HasPermission(BasePlayer player) => player != null && player.IPlayer.HasPermission(permName);

        #region Config
        bool Changed;
        Vector3 defaultPos;
        bool allowSaveInventory;
        bool maxHHT;
        bool autoVanish;
        List<string> Blacklist = new List<string>();
        bool wipeSaves;

        List<object> DefaultBlacklist
        {
            get
            {
                return new List<object>
                {
                    "2080339268",
                    "can.tuna.empty"
                };
            }
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PositionAdded"] = "You will now teleport to <color=yellow>{0}</color> on disconnect.",
                ["PositionRemoved1"] = "You will now teleport under ground on disconnect.",
                ["PositionRemoved2"] = "You will now teleport to <color=yellow>{0}</color> on disconnect.",
                ["PlayerWakeUp"] = "You will now teleport above ground when you wake up.",
                ["PlayerWakeUpReset"] = "You will no longer teleport above ground when you wake up.",
                ["PlayerNoClipEnabled"] = "You will now automatically be noclipped on reconnect.",
                ["PlayerNoClipDisabled"] = "You will no longer be noclipped on reconnect.",
                ["SavingInventory"] = "Your inventory will be saved and stripped on disconnect, and restored when you wake up.",
                ["NotSavingInventory"] = "Your inventory will no longer be saved.",
                ["Help0"] = "/uw save - toggles saving inventory (enabled: {0})",
                ["Help1"] = "/uw set <x y z> - sets your log out position. can specify coordinates <color=yellow>{0}</color>",
                ["Help2"] = "/uw reset - resets your log out position to be underground unless a position is configured in the config file",
                ["Help3"] = "/uw wakeup - toggle waking up on land (enabled: {0})",
                ["Help4"] = "/uw noclip - toggle auto noclip on reconnect (enabled: {0})",
                ["Help5"] = "/uw g - teleport to the ground",
                ["OutOfBounds"] = "The specified coordinates are not within the allowed boundaries of the map.",
                ["NoPermission"] = "You do not have permission to use this command.",
            }, this);
        }

        void LoadVariables()
        {
            maxHHT = Convert.ToBoolean(GetConfig("Settings", "Set Health, Hunger and Thirst to Max", false));
            defaultPos = GetConfig("Settings", "Default Teleport To Position On Disconnect", "(0, 0, 0)").ToString().ToVector3();            
            allowSaveInventory = Convert.ToBoolean(GetConfig("Settings", "Allow Save And Strip Admin Inventory On Disconnect", true));
            Blacklist = (GetConfig("Settings", "Blacklist", DefaultBlacklist) as List<object>).Where(o => o != null && o.ToString().Length > 0).Cast<string>().ToList();
            autoVanish = Convert.ToBoolean(GetConfig("Settings", "Auto Vanish On Connect", true));
            wipeSaves = Convert.ToBoolean(GetConfig("Settings", "Wipe Saved Inventories On Map Wipe", false));
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Config.Clear();
            LoadVariables();
        }

        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        string msg(string key, string id = null, params object[] args)
        {
            string message = id == null ? RemoveFormatting(lang.GetMessage(key, this, id)) : lang.GetMessage(key, this, id);

            return args.Length > 0 ? string.Format(message, args) : message;
        }

        string RemoveFormatting(string source) => source.Contains(">") ? System.Text.RegularExpressions.Regex.Replace(source, "<.*?>", string.Empty) : source;
        #endregion
    }
}