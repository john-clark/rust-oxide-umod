using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Skins", "MalkoR", "1.2.9", ResourceId = 2431)]
    [Description("Allow players to change items skin with the skin from steam workshop.")]
    class Skins : RustPlugin
    {
        private string box = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab";

        private Dictionary<string, List<ulong>> SkinsList = new Dictionary<string, List<ulong>>();
        private List<SkinContainer> Containers = new List<SkinContainer>();
        private HashSet<int> Boxes = new HashSet<int>();

        #region Oxide hooks
        void Init()
        {
            LoadVariables();
            cmd.AddChatCommand(configData?.ChatCommand ?? "skin", this, "CmdSkin");
            cmd.AddConsoleCommand("skin.add", this, "CcmdAddSkin");
            cmd.AddConsoleCommand("skin.remove", this, "CcmdRemoveSkin");
            cmd.AddConsoleCommand("skin.list", this, "CcmdListSkin");
            cmd.AddConsoleCommand("skin.unique", this, "CcmdListUnique");
            permission.RegisterPermission("skins.allow", this);
            permission.RegisterPermission("skins.admin", this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "You don't have permission to use this command.",
                ["ConsoleIncorrectSkinIdFormat"] = "Wrong format for the skinId, this must be numbers.",
                ["ConsoleItemIsNotFound"] = "Item with shortname {0} is not found.",
                ["ConsoleItemSkinExist"] = "The skinId {0} is already exist for item {1}",
                ["ConsoleItemAdded"] = "A new skinId {0} added for item {1}",
                ["ConsoleRemoveItemNotFound"] = "The item {0} is not found in config file. Nothing to remove.",
                ["ConsoleRemoveSkinNotFound"] = "The skinId {0} is not found in config file. Nothing to remove.",
                ["ConsoleRemoveSkinRemoved"] = "The skinId {0} is found in config file and removed.",
                ["ConsoleListItemNotFound"] = "The item {0} is not found in config file.",
                ["ConsoleWorkshopLoad"] = "Start to load workshop skins, it'll take some time, please wait.",
                ["ConsoleWorkshopLoaded"] = "The {0} new skins is loaded and append to config file.",
                ["ConsoleUniqueSort"] = "All skin duplicates was removed.",
            }, this);
        }

        void Unload()
        {
            List<SkinContainer> ToClose = new List<SkinContainer>();
            foreach (SkinContainer container in Containers)
                if (container.status != ContainerStatus.Ready)
                    ToClose.Add(container);

            foreach(SkinContainer container in ToClose)
                OnLootEntityEnd(container.inventory.playerOwner, container.storage as BaseCombatEntity);
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if(Boxes.Contains(entity.GetHashCode()))
            {
                info.damageTypes.ScaleAll(0);
            }
        }

        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (Containers.Exists(c => c.inventory == container))
            {
                SkinContainer _skinContainer = Containers.Find(c => c.inventory == container);
                if (_skinContainer.status == ContainerStatus.Ready)
                {
                    BasePlayer owner = _skinContainer.storage.inventory.playerOwner;
                    if (!SkinsList.ContainsKey(item.info.shortname))
                        LoadSkinsList(item.info);

                    if (!SkinsList.ContainsKey(item.info.shortname) || SkinsList[item.info.shortname].Count <= 1)
                    {
                        item.MoveToContainer(owner.inventory.containerMain);
                        return;
                    }

                    _skinContainer.status = ContainerStatus.FilledIn;

                    BaseProjectile ammo = item.GetHeldEntity() as BaseProjectile;
                    if (ammo != null)
                    {
                        ammo.UnloadAmmo(item, owner);
                        ammo.primaryMagazine.contents = 0;
                    }

                    if (item.contents != null)
                    {
                        List<Item> mods = new List<global::Item>();
                        foreach (Item mod in item.contents.itemList)
                            if (mod != null)
                                mods.Add(mod);

                        foreach (Item mod in mods)
                            MoveItemBack(mod, owner);
                    }
                    item.RemoveFromContainer();

                    NextTick(() =>
                    {
                        _skinContainer.storage.inventory.capacity = SkinsList[item.info.shortname].Count;
                        foreach (int skinId in SkinsList[item.info.shortname])
                        {
                            Item i = ItemManager.CreateByItemID(item.info.itemid, item.amount, (ulong)skinId);
                            i.condition = item.condition;
                            NextTick(() =>
                            {
                                BaseProjectile a = i.GetHeldEntity() as BaseProjectile;
                                if (a != null)
                                {
                                    a.primaryMagazine.contents = 0;
                                }
                                _skinContainer.ItemsList.Add(i);
                                i.MoveToContainer(_skinContainer.storage.inventory, -1, false);
                                NextTick(() => { _skinContainer.status = ContainerStatus.Filled; });
                            });
                        }
                    });
                }
            }
        }

        object CanMoveItem(Item item, PlayerInventory playerLoot, uint targetContainer, int targetSlot)
        {
            if(Containers.Exists(c => c.uid == item.GetRootContainer().uid))
            {
                if (playerLoot.containerMain.uid == targetContainer && playerLoot.containerMain.GetSlot(targetSlot) != null)
                    return false;
                if (playerLoot.containerBelt.uid == targetContainer && playerLoot.containerBelt.GetSlot(targetSlot) != null)
                    return false;
                if (playerLoot.containerWear.uid == targetContainer && playerLoot.containerWear.GetSlot(targetSlot) != null)
                    return false;
            }

            SkinContainer targetSkinContainer = Containers.Find(c => c.uid == targetContainer);
            if(targetSkinContainer != null)
            {
                if(targetSkinContainer.status != ContainerStatus.Ready)
                {
                    return false;
                }
            }
            return null;
        }

        object CanAcceptItem(ItemContainer container, Item item)
        {
            if (Containers.Exists(c => c.inventory == container && c.status != ContainerStatus.Ready))
            {
                return false;
            }
            return true;
        }

        void OnItemSplit(Item item, int amount)
        {
            if (Containers.Exists(c => c.inventory == item.GetRootContainer() && c.status != ContainerStatus.FilledIn))
            {
                NextTick(() =>
                {
                    SkinContainer _skinContainer = Containers.Find(c => c.inventory == item.GetRootContainer());
                    int new_amount = 0;
                    foreach (Item i in _skinContainer.inventory.itemList)
                        new_amount = (new_amount == 0) ? i.amount : (i.amount < new_amount) ? i.amount : new_amount;
                    foreach (Item i in _skinContainer.inventory.itemList)
                        i.amount = new_amount;
                });
            }
        }

        void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            if (Containers.Exists(c => c.inventory == container))
            {
                SkinContainer _skinContainer = Containers.Find(c => c.inventory == container);
                if (_skinContainer.status == ContainerStatus.Filled)
                {
                    foreach(Item i in _skinContainer.ItemsList)
                    {
                        if (i == item) continue;
                        i.Remove(0f);
                    }
                    _skinContainer.ItemsList = new List<Item>();
                    _skinContainer.storage.inventory.capacity = 1;
                    _skinContainer.status = ContainerStatus.Ready;
                }
            }
        }

        void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            SkinContainer container = Containers.Find(c => c.hashCode == entity.GetHashCode());
            if(container != null)
            {
                if(container.status != ContainerStatus.Ready)
                {
                    Item item = container.inventory.GetSlot(0);
                    if(item != null) container.inventory.GetOwnerPlayer().GiveItem(item);
                }
                container.storage.KillMessage();
                Boxes.Remove(entity.GetHashCode());
                Containers.Remove(container);
            }
        }

        object CanNetworkTo(BaseNetworkable entity, BasePlayer target)
        {
            if (Boxes.Contains(entity.GetHashCode()))
                return false;
            return null;
        }
        #endregion

        #region Console command
        void CcmdAddSkin(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if(player is BasePlayer && !player.IsAdmin && !permission.UserHasPermission(player.UserIDString, "skins.admin"))
            {
                SendReply(player, lang.GetMessage("NotAllowed", this, player.UserIDString));
            }

            string userIDString = (player is BasePlayer) ? player.UserIDString : null;

            string[] args = arg.Args;
            if(args.Length == 2)
            {
                string shortname = args.ElementAtOrDefault(0);
                ulong skinId = 0;
                ulong.TryParse(args.ElementAtOrDefault(1), out skinId);
                ItemDefinition def = ItemManager.FindItemDefinition(shortname);

                if(skinId == 0)
                {
                    SendReply(arg, lang.GetMessage("ConsoleIncorrectSkinIdFormat", this, userIDString));
                    return;
                }
                else if (def != null)
                {
                    if(!SkinsList.ContainsKey(shortname))
                    {
                        LoadSkinsList(def);
                    }
                    if(SkinsList[shortname].Contains(skinId))
                    {
                        SendReply(arg, lang.GetMessage("ConsoleItemSkinExist", this, userIDString), skinId, def.displayName.english);
                        return;
                    }
                    else
                    {
                        SkinsList[shortname].Add(skinId);
                        SendReply(arg, lang.GetMessage("ConsoleItemAdded", this, userIDString), skinId, def.displayName.english);
                        if(!configData.Workshop.ContainsKey(def.shortname))
                        {
                            configData.Workshop.Add(shortname, new List<ulong>() { (ulong)skinId });
                        }
                        else
                        {
                            configData.Workshop[shortname].Add((ulong)skinId);
                        }
                        SaveConfig(configData);
                        return;
                    }
                }
                else
                {
                    SendReply(arg, lang.GetMessage("ConsoleItemIsNotFound", this, userIDString),  shortname);
                    return;
                }
            }
        }

        void CcmdRemoveSkin(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player is BasePlayer && !player.IsAdmin && !permission.UserHasPermission(player.UserIDString, "skins.admin"))
            {
                SendReply(player, lang.GetMessage("NotAllowed", this, player.UserIDString));
            }

            string userIDString = (player is BasePlayer) ? player.UserIDString : null;

            string[] args = arg.Args;
            if (args.Length == 2)
            {
                string shortname = args.ElementAtOrDefault(0);
                ulong skinId = 0;
                ulong.TryParse(args.ElementAtOrDefault(1), out skinId);

                if (!configData.Workshop.ContainsKey(shortname))
                {
                    SendReply(arg, lang.GetMessage("ConsoleRemoveItemNotFound", this, userIDString), shortname);
                }
                else if (!configData.Workshop[shortname].Contains(skinId))
                {
                    SendReply(arg, lang.GetMessage("ConsoleRemoveSkinNotFound", this, userIDString), skinId);
                }
                else
                {
                    configData.Workshop[shortname].Remove(skinId);
                    if (configData.Workshop[shortname].Count == 0)
                        configData.Workshop.Remove(shortname);

                    SendReply(arg, lang.GetMessage("ConsoleRemoveSkinRemoved", this, userIDString), skinId);
                    SaveConfig(configData);
                }
            }
        }

        void CcmdListSkin(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player is BasePlayer && !player.IsAdmin && !permission.UserHasPermission(player.UserIDString, "skins.admin"))
            {
                SendReply(player, lang.GetMessage("NotAllowed", this, player.UserIDString));
            }

            string userIDString = (player is BasePlayer) ? player.UserIDString : null;

            if (arg.Args == null)
            {
                foreach (string name in configData.Workshop.Keys)
                    SendReply(arg, $"{name}");
                return;
            }
            string[] args = arg.Args;
            if (args.Length == 1)
            {
                string shortname = args.ElementAtOrDefault(0);
                if (!configData.Workshop.ContainsKey(shortname))
                {
                    SendReply(arg, lang.GetMessage("ConsoleListItemNotFound", this, userIDString), shortname);
                }
                else
                {
                    foreach (ulong skinId in configData.Workshop[shortname])
                        SendReply(arg, $"ID: {skinId}");
                }
            }
        }

        void CcmdListUnique(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player is BasePlayer && !player.IsAdmin && !permission.UserHasPermission(player.UserIDString, "skins.admin"))
            {
                SendReply(player, lang.GetMessage("NotAllowed", this, player.UserIDString));
            }
            string userIDString = (player is BasePlayer) ? player.UserIDString : null;

            Dictionary<string, List<ulong>> new_list = new Dictionary<string, List<ulong>>();
            foreach(KeyValuePair<string, List<ulong>> row in configData.Workshop)
            {
                new_list.Add(row.Key, row.Value.Distinct().ToList());
            }
            configData.Workshop = new_list;
            SaveConfig(configData);
            foreach(string shortname in new_list.Keys)
            {
                LoadSkinsList(ItemManager.FindItemDefinition(shortname));
            }

            SendReply(arg, lang.GetMessage("ConsoleUniqueSort", this, userIDString));
        }

        void CcmdWorkshopLoad(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player is BasePlayer && !player.IsAdmin && !permission.UserHasPermission(player.UserIDString, "skins.admin"))
            {
                SendReply(player, lang.GetMessage("NotAllowed", this, player.UserIDString));
            }

            string userIDString = (player is BasePlayer) ? player.UserIDString : null;

            SendReply(arg, lang.GetMessage("ConsoleWorkshopLoad", this, userIDString));

            webrequest.EnqueueGet("http://s3.amazonaws.com/s3.playrust.com/icons/inventory/rust/schema.json", (code, response) =>
            {
                if (!(response == null && code == 200))
                {
                    var schema = JsonConvert.DeserializeObject<Rust.Workshop.ItemSchema>(response);
                    var items = schema.items;
                    int added_count = 0;
                    foreach (var item in items)
                    {
                        if(item.workshopid != null && item.itemshortname != null)
                        {
                            ulong workshopid = ulong.Parse(item.workshopid);
                            string shortname = item.itemshortname;

                            if (!configData.Workshop.ContainsKey(shortname))
                                configData.Workshop.Add(shortname, new List<ulong>());

                            if (!configData.Workshop[shortname].Contains(workshopid))
                            {
                                configData.Workshop[item.itemshortname].Add(workshopid);
                                added_count += 1;
                            }

                            if (!SkinsList.ContainsKey(item.itemshortname))
                                SkinsList.Add(item.itemshortname, new List<ulong>());

                            if (!SkinsList[shortname].Contains(workshopid))
                                SkinsList[shortname].Add(workshopid);
                        }
                    }
                    SaveConfig(configData);
                    SendReply(arg, lang.GetMessage("ConsoleWorkshopLoaded", this, userIDString), added_count);
                }
            }, this);
        }
        #endregion

        #region Chat command
        void CmdSkin(BasePlayer player)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, "skins.allow"))
            {
                SendReply(player, lang.GetMessage("NotAllowed", this, player.UserIDString));
                return;
            }
            SkinContainer container = SpawnContainer(player);
            timer.In(0.25f, () =>
            {
                PlayerLootContainer(player, container.storage);
            });
        }
        #endregion

        #region Helpers
        void LoadSkinsList(ItemDefinition def)
        {
            if (SkinsList.ContainsKey(def.shortname))
                SkinsList.Remove(def.shortname);
            List<ulong> ids = new List<ulong>() { 0 };
            if (!configData.PreventDefaultSkins)
            {
                ItemSkinDirectory.Skin[] skins = ItemSkinDirectory.ForItem(def);
                List<string> uniqSkins = new List<string>();
                foreach (ItemSkinDirectory.Skin skin in skins)
                {
                    if (!skin.isSkin) continue;
                    if (uniqSkins.Contains(skin.name)) continue;
                    uniqSkins.Add(skin.name);
                    ids.Add((ulong)skin.id);
                }
            }
            if(configData.Workshop.ContainsKey(def.shortname))
            {
                foreach (ulong wid in configData.Workshop[def.shortname])
                    ids.Add(wid);
            }
            SkinsList.Add(def.shortname, ids);
        }

        private SkinContainer SpawnContainer(BasePlayer player)
        {
            Vector3 pos = player.transform.position;
            BaseEntity ent = GameManager.server.CreateEntity(box, new Vector3(pos.x, pos.y - 2000, pos.z));
            Boxes.Add(ent.GetHashCode());
            ent.Spawn();
            StorageContainer storage = ent.GetComponent<StorageContainer>();
            storage.inventory.playerOwner = player;
            storage.inventory.capacity = 1;
            storage.SetFlag(BaseEntity.Flags.Locked, true);
            SkinContainer c = new SkinContainer()
            {
                hashCode = ent.GetHashCode(),
                storage = storage,
                inventory = storage.inventory,
                uid = storage.inventory.uid,
                playerId = player.userID,
                status = ContainerStatus.Ready
            };
            Containers.Add(c);
            return c;
        }

        private static void PlayerLootContainer(BasePlayer player, StorageContainer container)
        {
            container.SetFlag(BaseEntity.Flags.Open, true, false);
            player.inventory.loot.StartLootingEntity(container, false);
            player.inventory.loot.AddContainer(container.inventory);
            player.inventory.loot.SendImmediate();
            player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", container.panelName);
        }

        private void MoveItemBack(Item item, BasePlayer player)
        {
            bool moved = false;

            if (player.inventory.containerMain.CanTake(item))
            {
                item.MoveToContainer(player.inventory.containerMain);
                moved = true;
            }
            if (!moved && player.inventory.containerBelt.CanTake(item))
            {
                item.MoveToContainer(player.inventory.containerBelt);
                moved = true;
            }
            if (!moved)
            {
                item.Drop(player.eyes.position, player.eyes.BodyForward() * 2f);
            }
        }
        #endregion

        #region Config
        private ConfigData configData;
        class ConfigData
        {
            public string ChatCommand;
            public bool PreventDefaultSkins;
            public Dictionary<string, List<ulong>> Workshop = new Dictionary<string, List<ulong>>();
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                ChatCommand = "skin",
                PreventDefaultSkins = false,
                Workshop = new Dictionary<string, List<ulong>>()
            };
            SaveConfig(config);
        }

        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Nested classes
        class SkinContainer
        {
            public StorageContainer storage;
            public ItemContainer inventory;
            public ContainerStatus status;
            public List<Item> ItemsList = new List<Item>();
            public int hashCode;
            public ulong playerId;
            public uint uid;
        }

        public enum ContainerStatus
        {
            Ready = 0,
            FilledIn = 1,
            Filled = 2
        }

        class WeaponMods
        {
            public int itemId;
            public float condition;
        }
        #endregion
    }
}