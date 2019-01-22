using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;
using Oxide.Core;
using System;

namespace Oxide.Plugins
{
    [Info("Backpacks", "LaserHydra", "2.1.8", ResourceId = 1408)]
    [Description("Allows players to have a Backpack which provides them extra inventory space.")]
    internal class Backpacks : RustPlugin
    {
        private const string BackpackPrefab = "assets/prefabs/misc/item drop/item_drop_backpack.prefab";
        private const string BackBone = "spine3";

	    private static Backpacks _instance;
        private static Dictionary<ulong, Backpack> _backpacks = new Dictionary<ulong, Backpack>();

        [PluginReference]
        private RustPlugin EventManager;

        #region Classes

        private static class Configuration
        {
            public static StorageSize BackpackSize = StorageSize.Medium;

            public static int BackpackSizeInt
            {
                get { return (int)BackpackSize; }
                set { BackpackSize = (StorageSize)value; }
            }

            public static bool ShowOnBack = true;
            public static bool HideOnBackIfEmpty = true;
            public static bool DropOnDeath = true;
            public static bool EraseOnDeath = false;

            public static bool UseBlacklist = false;

            public static List<object> BlacklistedItems = new List<object>
            {
                "rocket.launcher",
                "lmg.m249"
            };
        }

        private class StorageCloser : MonoBehaviour
        {
            private Action _callback;

            public static void Attach(BaseEntity entity, Action callback)
                => entity.gameObject.AddComponent<StorageCloser>()._callback = callback;

            private void PlayerStoppedLooting(BasePlayer player) => _callback.Invoke();
        }

        private class VisualBackpack : MonoBehaviour
        {
            public Backpack Backpack { get; set; }
        }

        private class Backpack
        {
            public BackpackInventory Inventory = new BackpackInventory();
            public ulong ownerID;

            private BaseEntity _boxEntity;
            private BaseEntity _visualEntity;
            private BasePlayer _looter;

            [JsonIgnore] public bool IsOpen => _boxEntity != null;
            [JsonIgnore] public StorageContainer Container => _boxEntity?.GetComponent<StorageContainer>();

            public StorageSize Size =>
                _instance.permission.UserHasPermission(ownerID.ToString(), "backpacks.use.large") ? StorageSize.Large :
                    (_instance.permission.UserHasPermission(ownerID.ToString(), "backpacks.use.medium") ? StorageSize.Medium :
                        (_instance.permission.UserHasPermission(ownerID.ToString(), "backpacks.use.small") ? StorageSize.Small : Configuration.BackpackSize));

            public Backpack(ulong id)
            {
                ownerID = id;
            }

            public void Drop(Vector3 position)
            {
                if (Inventory.Items.Count > 0)
                {
                    BaseEntity entity = GameManager.server.CreateEntity(BackpackPrefab, position, Quaternion.identity);
                    DroppedItemContainer container = entity as DroppedItemContainer;

                    container.inventory = new ItemContainer();
                    container.inventory.ServerInitialize(null, Inventory.Items.Count);
                    container.inventory.GiveUID();
                    container.inventory.entityOwner = container;
                    container.inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);

                    foreach (Item item in Inventory.Items.Select(i => i.ToItem()))
                    {
                        if (!item.MoveToContainer(container.inventory))
                            item.Remove();
                    }

                    container.ResetRemovalTime();

                    container.Spawn();

                    container.playerName = "Backpack";
                    container.playerSteamID = ownerID;

                    entity.name = "droppedbackpack";
                }

                EraseContents();
            }

            public void EraseContents()
            {
                RemoveVisual();
                Inventory.Items.Clear();
                SaveData(this, $"{_instance.DataFileName}/{ownerID}");
            }

            public void Open(BasePlayer looter)
            {
                if (IsOpen)
                {
                    _instance.PrintToChat(looter, _instance.lang.GetMessage("Backpack Already Open", _instance, looter.UserIDString));
                    return;
                }

                if (_instance.EventManager?.Call<bool>("isPlaying", looter) ?? false)
                {
                    _instance.PrintToChat(looter, _instance.lang.GetMessage("May Not Open Backpack In Event", _instance, looter.UserIDString));
                    return;
                }

                _boxEntity = SpawnContainer(Size, looter.transform.position - new Vector3(0, UnityEngine.Random.Range(100, 5000), 0));

                foreach (var backpackItem in Inventory.Items)
                {
                    var item = backpackItem.ToItem();
                    item?.MoveToContainer(Container.inventory, item.position);
                }

                _looter = looter;

                PlayerLootContainer(looter, Container);
                StorageCloser.Attach(_boxEntity, Close);
            }

            private void Close()
            {
                if (_boxEntity != null)
                {
                    Inventory.Items = Container.inventory.itemList.Select(BackpackInventory.BackpackItem.FromItem).ToList();

                    _boxEntity.Kill();
                    _boxEntity = null;
                }

                if (_looter != null)
                {
                    if (_looter.userID != ownerID)
                    {
                        BasePlayer ownerPlayer = BasePlayer.FindByID(ownerID);

                        if (ownerPlayer != null)
                        {
                            if (Inventory.Items.Count == 0 && Configuration.HideOnBackIfEmpty)
                                RemoveVisual();
                            else if (_visualEntity == null)
                                SpawnVisual(ownerPlayer);
                        }
                    }
                    else
                    {
                        if (Inventory.Items.Count == 0 && Configuration.HideOnBackIfEmpty)
                            RemoveVisual();
                        else if (_visualEntity == null)
                            SpawnVisual(_looter);
                    }
                }

                _looter = null;

                SaveData(this, $"{_instance.DataFileName}/{ownerID}");
            }

            public void ForceClose() => Close();

            public void SpawnVisual(BasePlayer player)
            {
                if (_visualEntity != null || !Configuration.ShowOnBack)
                    return;

                BaseEntity entity = GameManager.server.CreateEntity(BackpackPrefab, new Vector3(0, -0.1f, 0), Quaternion.Euler(-5, -90, 180));
                DroppedItemContainer container = entity as DroppedItemContainer;
                container.inventory = new ItemContainer();
                container.inventory.ServerInitialize(null, 0);
                container.inventory.GiveUID();
                container.inventory.entityOwner = container;
                container.inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);
                container.CancelInvoke(container.RemoveMe);
                
                var visualBackpack = entity.gameObject.AddComponent<VisualBackpack>();
                visualBackpack.Backpack = this;

                entity.limitNetworking = true;

                entity.SetParent(player, StringPool.Get(BackBone));
                entity.SetFlag(BaseEntity.Flags.Locked, true);
                entity.Spawn();
                entity.name = "backpack";

                _visualEntity = entity;
            }

            public void RemoveVisual()
            {
                if (_visualEntity != null)
                {
                    _visualEntity.Kill();
                    _visualEntity = null;
                }
            }

            public static Backpack LoadOrCreate(ulong id)
            {
                if (_backpacks.ContainsKey(id))
                    return _backpacks[id];

                Backpack backpack = null;

                if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{_instance.DataFileName}/{id}"))
                    LoadData(ref backpack, $"{_instance.DataFileName}/{id}");
                else
                {
                    backpack = new Backpack(id);
                    SaveData(backpack, $"{_instance.DataFileName}/{id}");
                }

                _backpacks.Add(id, backpack);

                return backpack;
            }
        }

        private class BackpackInventory
        {
            public List<BackpackItem> Items = new List<BackpackItem>();

            public class BackpackItem
            {
                public int ID;
                public int Position = -1;
                public int Amount;

                [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
                public ulong Skin;

                [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
                public float Fuel;

                [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
                public int FlameFuel;

                [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
                public float Condition;

                [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
                public float MaxCondition = -1;

                [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
                public int Ammo;

                [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
                public int AmmoType;

                [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
                public int DataInt;

                public bool IsBlueprint;
                public int BlueprintTarget;

                [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
                public List<BackpackItem> Contents = new List<BackpackItem>();

                public Item ToItem()
                {
                    if (Amount == 0)
                        return null;

                    Item item = ItemManager.CreateByItemID(ID, Amount, Skin);

                    item.position = Position;

                    if (IsBlueprint)
                    {
                        item.blueprintTarget = BlueprintTarget;
                        return item;
                    }

                    BaseProjectile.Magazine magazine = item.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine;
                    FlameThrower flameThrower = item.GetHeldEntity()?.GetComponent<FlameThrower>();

                    item.fuel = Fuel;
                    item.condition = Condition;

                    if (MaxCondition != -1)
                        item.maxCondition = MaxCondition;

                    if (Contents != null)
                        foreach (var contentItem in Contents)
                            contentItem.ToItem().MoveToContainer(item.contents);
                    else
                        item.contents = null;

                    if (magazine != null)
                    {
                        magazine.contents = Ammo;
                        magazine.ammoType = ItemManager.FindItemDefinition(AmmoType);
                    }

                    if (flameThrower != null)
                        flameThrower.ammo = FlameFuel;

                    if (item.instanceData != null)
                        item.instanceData.dataInt = DataInt;

                    return item;
                }

                public static BackpackItem FromItem(Item item) => new BackpackItem
                {
                    ID = item.info.itemid,
                    Position = item.position,
                    Ammo = item.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine?.contents ?? 0,
                    AmmoType = item.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine?.ammoType?.itemid ?? 0,
                    Amount = item.amount,
                    Condition = item.condition,
                    MaxCondition = item.maxCondition,
                    Fuel = item.fuel,
                    Skin = item.skin,
                    Contents = item.contents?.itemList?.Select(FromItem).ToList(),
                    FlameFuel = item.GetHeldEntity()?.GetComponent<FlameThrower>()?.ammo ?? 0,
                    IsBlueprint = item.IsBlueprint(),
                    BlueprintTarget = item.blueprintTarget,
                    DataInt = item.instanceData?.dataInt ?? 0
                };
            }
        }

        public enum StorageSize
        {
            Large = 3,
            Medium = 2,
            Small = 1
        }

        #endregion

        #region Container Related

        public static string GetContainerPrefab(StorageSize size)
        {
            switch (size)
            {
                case StorageSize.Large:
                    return "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab";
                case StorageSize.Medium:
                    return "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";
                case StorageSize.Small:
                    return "assets/prefabs/deployable/small stash/small_stash_deployed.prefab";
            }

            return null;
        }

        private static BaseEntity SpawnContainer(StorageSize size = StorageSize.Medium, Vector3 position = default(Vector3))
        {
            var ent = GameManager.server.CreateEntity(GetContainerPrefab(size), position);

            ent.UpdateNetworkGroup();
            ent.SendNetworkUpdateImmediate();

            ent.globalBroadcast = true;

            ent.Spawn();

            return ent;
        }

        private static void PlayerLootContainer(BasePlayer player, StorageContainer container)
        {
            container.SetFlag(BaseEntity.Flags.Open, true);
            player.inventory.loot.StartLootingEntity(container, false);
            player.inventory.loot.AddContainer(container.inventory);
            player.inventory.loot.SendImmediate();
            player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", container.panelName);
            container.DecayTouch();
            container.SendNetworkUpdate();
        }

        #endregion

        #region Loading

        private void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["No Permission"] = "You don't have permission to use this command.",
                ["Backpack Already Open"] = "Somebody already has this backpack open!",
                ["May Not Open Backpack In Event"] = "You may not open a backpack while participating in an event!"
            }, this);
        }

        private new void LoadConfig()
        {
            Configuration.BackpackSizeInt = GetConfig(Configuration.BackpackSizeInt, "Backpack Size (1-3)");

            GetConfig(ref Configuration.DropOnDeath, "Drop On Death");
            GetConfig(ref Configuration.EraseOnDeath, "Erase On Death");

            GetConfig(ref Configuration.ShowOnBack, "Show On Back");
            GetConfig(ref Configuration.HideOnBackIfEmpty, "Hide On Back If Empty");

            GetConfig(ref Configuration.UseBlacklist, "Use Blacklist");
            GetConfig(ref Configuration.BlacklistedItems, "Blacklisted Items (Item Shortnames)");

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => PrintWarning("Generating new configuration file...");

        #endregion

        #region Hooks

        private void Loaded()
        {
            _instance = this;

            LoadConfig();
            LoadMessages();

            permission.RegisterPermission("backpacks.use", this);
            permission.RegisterPermission("backpacks.use.small", this);
            permission.RegisterPermission("backpacks.use.medium", this);
            permission.RegisterPermission("backpacks.use.large", this);
            permission.RegisterPermission("backpacks.admin", this);

            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                try
                {
                    OnPlayerInit(basePlayer);
                }
                catch (Exception)
                {
                }
            }
        }

        private void Unload()
        {
            foreach (var backpack in _backpacks.Values)
            {
                if (backpack.IsOpen)
                    backpack.ForceClose();

                backpack.RemoveVisual();
            }

            foreach (var basePlayer in BasePlayer.activePlayerList)
                OnPlayerDisconnected(basePlayer);
        }

        private void OnPlayerInit(BasePlayer player)
        {
	        if (player is NPCPlayer)
		        return;

            var backpack = Backpack.LoadOrCreate(player.userID);

            if (permission.UserHasPermission(player.UserIDString, "backpacks.use") && Configuration.ShowOnBack)
            {
                if (backpack.Inventory.Items.Count == 0 && Configuration.HideOnBackIfEmpty)
                    return;

                backpack.SpawnVisual(player);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (_backpacks.ContainsKey(player.userID))
                _backpacks.Remove(player.userID);
        }

        private object CanAcceptItem(ItemContainer container, Item item)
        {
            if (!Configuration.UseBlacklist)
                return null;

            // Is the Item blacklisted and the target container is a backpack?
            if (Configuration.BlacklistedItems.Any(i => i.ToString() == item.info.shortname) &&
                _backpacks.Values.Any(b => b.Container != null && b.Container.inventory == container))
                return ItemContainer.CanAcceptResult.CannotAccept;

            return null;
        }

        private void OnUserPermissionGranted(string id, string perm)
        {
            if (perm == "backpacks.use" && Configuration.ShowOnBack)
            {
                var player = BasePlayer.Find(id);
                var backpack = Backpack.LoadOrCreate(player.userID);

                if (backpack.Inventory.Items.Count == 0 && Configuration.HideOnBackIfEmpty)
                    return;

                backpack.SpawnVisual(player);
            }
        }

        private void OnEntityDeath(BaseCombatEntity victim, HitInfo info)
        {
            if (victim is BasePlayer && !(victim is NPCPlayer))
            {
                var player = (BasePlayer)victim;
                var backpack = Backpack.LoadOrCreate(player.userID);

                backpack.ForceClose();

                if (Configuration.EraseOnDeath)
                    backpack.EraseContents();
                else if (Configuration.DropOnDeath)
                    backpack.Drop(player.transform.position);
            }
        }

		/* Work in progress
        private object CanBaseEntityNetworkTo(BaseEntity entity, BasePlayer target)
        {
            return null;

            var backpack = entity.GetComponent<VisualBackpack>();

            if (backpack?.Backpack?.ownerID == target.userID)
                return false;

            return null;
        }
		*/

        #endregion

        #region Commands

        [ChatCommand("backpack")]
        private void OpenBackpackChatCommand(BasePlayer player, string cmd, string[] args)
        {
            if (permission.UserHasPermission(player.UserIDString, "backpacks.use"))
                timer.Once(0.1f, () => Backpack.LoadOrCreate(player.userID).Open(player));
            else
                PrintToChat(player, lang.GetMessage("No Permission", this, player.UserIDString));
        }

        [ConsoleCommand("backpack.open")]
        private void OpenBackpackConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;

            BasePlayer player = arg.Player();

            if (permission.UserHasPermission(player.UserIDString, "backpacks.use"))
                Backpack.LoadOrCreate(player.userID).Open(player);
        }

        [ChatCommand("viewbackpack")]
        private void ViewBackpack(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "backpacks.admin"))
            {
                PrintToChat(player, lang.GetMessage("No Permission", this, player.UserIDString));
                return;
            }

            if (args.Length != 1)
            {
                PrintToChat(player, "Syntax: /viewbackpack <steamid>");
                return;
            }

            ulong id;

            if (!ulong.TryParse(args[0], out id) || !args[0].StartsWith("7656119") || args[0].Length != 17)
            {
                PrintToChat(player, $"{args[0]} is not a valid SteamID (64)!");
                return;
            }

            Backpack backpack = Backpack.LoadOrCreate(id);

            if (backpack.IsOpen)
            {
                PrintToChat(player, lang.GetMessage("Backpack Already Open", this, player.UserIDString));
                return;
            }

            timer.Once(0.5f, () => backpack.Open(player));
        }

        #endregion

        #region Data & Config Helper

        private static void GetConfig<T>(ref T variable, params string[] path) => variable = GetConfig(variable, path);

        private static T GetConfig<T>(T defaultValue, params string[] path)
        {
            if (path.Length == 0)
                return defaultValue;

            if (_instance.Config.Get(path) == null)
            {
                _instance.Config.Set(path.Concat(new object[] { defaultValue }).ToArray());
                _instance.PrintWarning($"Added field to config: {string.Join("/", path)}");
            }

            return (T)Convert.ChangeType(_instance.Config.Get(path), typeof(T));
        }

        private string DataFileName => Title.Replace(" ", "");

        private static void LoadData<T>(ref T data, string filename = null) => data = Core.Interface.Oxide.DataFileSystem.ReadObject<T>(filename ?? _instance.DataFileName);

        private static void SaveData<T>(T data, string filename = null) => Core.Interface.Oxide.DataFileSystem.WriteObject(filename ?? _instance.DataFileName, data);

        #endregion
    }
}