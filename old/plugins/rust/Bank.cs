using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;

using Oxide.Core;
using Oxide.Core.Plugins;

using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info ("Bank", "Calytic", "1.0.3")]
    [Description ("Safe player storage")]
    class Bank : RustPlugin
    {
        #region Variables

        string defaultBoxPrefab;
        int defaultSlots;

        Dictionary<string, object> boxPrefabs;
        Dictionary<string, object> boxSlots;
        bool keyring;
        float cooldownMinutes;
        bool npconly;
        List<string> npcids = new List<string> ();
        float radiationMax;
        bool allowSafeZone;

        public static DataFileSystem datafile;
        public static DataFileSystem configFile;

        Dictionary<string, object> itemLimits = new Dictionary<string, object> ();

        [PluginReference]
        Plugin MasterKey;

        #endregion

        #region Bank/Item Profile

        public class ItemProfile
        {
            public string id;
            public int amount;
            public int slot;
            public Item.Flag flags;
            public float condition;
            public ulong skin;
            public List<ItemProfile> contents;
            public int primaryMagazine;
            public int ammoType;
            public int dataInt;

            [JsonConstructor]
            public ItemProfile (string id, int amount, int slot, Item.Flag flags, float condition = 0.0f, ulong skin = 0, List<ItemProfile> contents = null, int primaryMagazine = 0, int ammoType = 0, int dataInt = 0)
            {
                this.id = id;
                this.amount = amount;
                this.slot = slot;
                this.flags = flags;
                this.condition = condition;
                this.skin = skin;
                this.contents = contents;
                this.primaryMagazine = primaryMagazine;
                this.ammoType = ammoType;
                this.dataInt = dataInt;
            }

            public static ItemProfile Create (Item item)
            {
                List<ItemProfile> contents = new List<ItemProfile> ();
                int primaryMagazine = 0;
                int ammoType = 0;

                if (item.contents != null) {
                    if (item.contents.itemList.Count > 0) {
                        foreach (Item content in item.contents.itemList) {
                            contents.Add (Create (content));
                        }
                    }
                }

                var weapon = item.GetHeldEntity () as BaseProjectile;
                if (weapon != null) {
                    BaseProjectile projectile = weapon as BaseProjectile;
                    primaryMagazine = projectile.primaryMagazine.contents;
                    ammoType = projectile.primaryMagazine.ammoType.itemid;
                }


                int dataInt = 0;
                if (item.instanceData != null && item.info.shortname == "door.key") {
                    dataInt = item.instanceData.dataInt;
                }

                ItemProfile newItem = new ItemProfile (item.info.itemid.ToString (), item.amount, item.position, item.flags, item.condition, item.skin, contents, primaryMagazine, ammoType, dataInt);

                return newItem;
            }
        }

        public class BankProfile
        {
            protected ulong playerID;

            public List<ItemProfile> items = new List<ItemProfile> ();

            [JsonIgnore]
            public bool open = false;

            [JsonIgnore]
            public bool dirty = false;

            [JsonIgnore]
            public BasePlayer Player {
                get {
                    return BasePlayer.Find (playerID.ToString ());
                }
                protected set {
                    playerID = value.userID;
                }
            }

            [JsonIgnore]
            public ulong PlayerID {
                get { return playerID; }
                private set { }
            }

            [JsonIgnore]
            public int Count {
                get {
                    return items.Count;
                }
                private set { }
            }

            public BankProfile ()
            {

            }

            public BankProfile (BasePlayer player, List<ItemProfile> items = null)
            {
                playerID = player.userID;
                if (items is List<ItemProfile>) {
                    this.items = items;
                }
            }

            [JsonConstructor]
            public BankProfile (ulong playerID, List<ItemProfile> items)
            {
                this.playerID = playerID;
                this.items = items;
            }

            public bool Add (Item item)
            {
                ItemProfile profile = ItemProfile.Create (item);
                items.Add (profile);
                dirty = true;

                return true;
            }

            public bool Add (Item [] items)
            {
                foreach (Item item in items) {
                    if (!Add (item)) {
                        return false;
                    }
                }

                return true;
            }

            public bool Add (List<Item> items)
            {
                return Add (items.ToArray ());
            }

            public bool Remove (Item item)
            {
                ItemProfile removing = null;
                foreach (ItemProfile profile in this.items) {
                    if (profile.id == item.info.itemid.ToString () && profile.amount == item.amount) {
                        removing = profile;
                        break;
                    }
                }

                if (removing is ItemProfile) {
                    dirty = true;
                    items.Remove (removing);
                    return true;
                }

                return false;
            }

            public bool Remove (Item [] items)
            {
                foreach (Item item in items) {
                    if (!Remove (item)) {
                        return false;
                    }
                }

                return true;
            }

            public bool Remove (List<Item> items)
            {
                return Remove (items.ToArray ());
            }

            [JsonIgnore]
            ItemContainer container;

            public ItemContainer GetContainer (BasePlayer player, int slots = 30)
            {

                if (this.container is ItemContainer) {
                    this.container.playerOwner = player;
                    this.container.itemList.Clear ();
                    PopulateContainer (player, this.container);
                    return this.container;
                }
                ItemContainer container = new ItemContainer ();
                container.ServerInitialize (null, slots);
                if ((int)container.uid == 0)
                    container.GiveUID ();
                container.playerOwner = player;
                PopulateContainer (player, container);

                return this.container = container;
            }

            void PopulateContainer (BasePlayer player, ItemContainer container, List<ItemProfile> items = null)
            {
                if (items == null) {
                    items = this.items;
                }

                foreach (ItemProfile profile in items) {
                    Item item = ItemManager.CreateByItemID (Convert.ToInt32 (profile.id), profile.amount);

                    if (item is Item) {
                        item.flags = profile.flags;
                        item.skin = profile.skin;

                        if (item.hasCondition) {
                            item.condition = profile.condition;
                        }

                        var held = item.GetHeldEntity ();
                        if (held is BaseEntity) {
                            held.skinID = profile.skin;
                        }
                        var weapon = held as BaseProjectile;
                        if (weapon != null) {
                            BaseProjectile projectile = weapon as BaseProjectile;
                            projectile.primaryMagazine.contents = profile.primaryMagazine;
                            if (profile.ammoType != 0) {
                                projectile.primaryMagazine.ammoType = ItemManager.FindItemDefinition (profile.ammoType);
                            }
                        }

                        if (profile.contents != null) {
                            if (profile.contents.Count > 0) {
                                PopulateContainer (player, item.contents, profile.contents);
                            }
                        }

                        if (item.info.shortname == "door.key" && profile.dataInt != 0) {
                            ProtoBuf.Item.InstanceData instanceData = Facepunch.Pool.Get<ProtoBuf.Item.InstanceData> ();
                            item.instanceData = instanceData;
                            item.instanceData.ShouldPool = false;
                            item.instanceData.dataInt = profile.dataInt;
                        }

                        item.MoveToContainer (container, profile.slot);
                    }
                }
            }
        }
        #endregion

        #region Configuration

        class ItemLimit
        {
            public bool enabled;
            public int minimum;
            public int maximum;

            [JsonConstructor]
            public ItemLimit (bool enabled, int minimum, int maximum)
            {
                this.enabled = enabled;
                this.minimum = minimum;
                this.maximum = maximum;
            }
        }

        #endregion

        #region State

        class OnlinePlayer
        {
            public BasePlayer Player;
            public BasePlayer Target;
            public StorageContainer View;
            public List<BasePlayer> Matches;

            public OnlinePlayer (BasePlayer player)
            {
            }
        }

        public Dictionary<ItemContainer, ulong> containers = new Dictionary<ItemContainer, ulong> ();
        public Dictionary<ulong, BankProfile> banks = new Dictionary<ulong, BankProfile> ();

        [OnlinePlayers]
        Hash<BasePlayer, OnlinePlayer> onlinePlayers = new Hash<BasePlayer, OnlinePlayer> ();
        Dictionary<string, DateTime> bankCooldowns = new Dictionary<string, DateTime> ();

        #endregion

        #region Initialization & Data

        void Init ()
        {
            Unsubscribe (nameof (CanNetworkTo));
            Unsubscribe (nameof (OnEntityTakeDamage));
            ////Unsubscribe(nameof(OnItemAddedToContainer));

            configFile = new DataFileSystem (Interface.Oxide.ConfigDirectory);
            itemLimits = configFile.ReadObject<Dictionary<string, object>> ("Bank_ItemLimits");
            permission.RegisterPermission ("bank.use", this);

            CheckConfig ();

            datafile = new DataFileSystem (Interface.Oxide.DataDirectory + "\\" + this.GetConfig<string> ("subDirectory", "banks"));

            boxPrefabs = GetConfig ("Settings", "boxes", GetDefaultBoxes ());
            boxSlots = GetConfig ("Settings", "slots", GetDefaultSlots ());

            defaultBoxPrefab = GetConfig ("Settings", "defaultBox", "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab");
            defaultSlots = GetConfig ("Settings", "defaultSlots", 4);

            cooldownMinutes = GetConfig ("Settings", "cooldownMinutes", 5f);
            radiationMax = GetConfig ("Settings", "radiationMax", 1f);
            allowSafeZone = GetConfig ("Settings", "allowSafeZone", true);

            npconly = GetConfig ("Settings", "NPCBankersOnly", false);
            List<object> lnpcids = GetConfig ("Settings", "NPCIDs", new List<object> ());

            foreach (object id in lnpcids) {
                npcids.Add (id.ToString ());
            }

            keyring = GetConfig ("Settings", "Keyring", true);

            foreach (KeyValuePair<string, object> kvp in boxPrefabs) {
                permission.RegisterPermission (kvp.Key, this);
            }

            foreach (KeyValuePair<string, object> kvp in boxSlots) {
                if (!boxPrefabs.ContainsKey (kvp.Key)) {
                    permission.RegisterPermission (kvp.Key, this);
                }
            }

            foreach (BasePlayer player in BasePlayer.activePlayerList) {
                LoadProfile (player.userID);
            }
        }

        new void LoadDefaultMessages ()
        {
            lang.RegisterMessages (new Dictionary<string, string>
            {
                {"Denied: Permission", "You lack permission to do that"},
                {"Denied: Privilege", "You lack build privileges and cannot do that"},
                {"Denied: Swimming", "You cannot do that while swimming"},
                {"Denied: Falling", "You cannot do that while falling"},
                {"Denied: Wounded", "You cannot do that while wounded"},
                {"Denied: Mounted", "You cannot do that while mounted"},
                {"Denied: Generic", "You cannot do that right now"},
                {"Denied: Irradiated", "You cannot do that while irradiated"},
                {"Denied: Ship", "You cannot do that while on a ship"},
                {"Denied: Lift", "You cannot do that while on a lift"},
                {"Denied: Balloon", "You cannot do that while on a balloon"},
                {"Denied: Safe Zone", "You cannot do that while in a safe zone"},
                {"Cooldown: Seconds", "You are doing that too often, try again in a {0} seconds(s)."},
                {"Cooldown: Minutes", "You are doing that too often, try again in a {0} minute(s)."},
                {"Limit: Return", "Some items were returned to your inventory"}
            }, this);
        }

        Dictionary<string, object> GetDefaultBoxes ()
        {
            return new Dictionary<string, object> () {
                {"bank.default", "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab"},
                {"bank.big", "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab"}
            };
        }

        Dictionary<string, object> GetDefaultSlots ()
        {
            return new Dictionary<string, object> () {
                {"bank.default", 4},
                {"bank.big", 30}
            };
        }

        Dictionary<string, object> GetDefaultItemLimits ()
        {
            return new Dictionary<string, object> () {
                {"explosive.timed", new ItemLimit(false, 0, 1)}
            };
        }

        protected override void LoadDefaultConfig ()
        {
            Config ["Settings", "boxes"] = GetDefaultBoxes ();
            Config ["Settings", "slots"] = GetDefaultSlots ();
            Config ["Settings", "defaultBox"] = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";
            Config ["Settings", "defaultSlots"] = 4;
            Config ["Settings", "keyring"] = true;
            Config ["Settings", "cooldownMinutes"] = 5;
            Config ["Settings", "radiationMax"] = 1;
            Config ["Settings", "NPCBankersOnly"] = false;
            Config ["Settings", "NPCIDs"] = new List<object> ();
            Config ["Settings", "allowSafeZone"] = true;

            if (configFile == null)
                configFile = new DataFileSystem (Interface.Oxide.ConfigDirectory);

            configFile.WriteObject ("Bank_ItemLimits", GetDefaultItemLimits ());

            Config ["VERSION"] = Version.ToString ();
        }

        void Unloaded ()
        {
            foreach (var player in BasePlayer.activePlayerList) {
                OnlinePlayer onlinePlayer;
                if (onlinePlayers.TryGetValue (player, out onlinePlayer) && onlinePlayer.View != null) {
                    SaveProfileByUser (player.userID);
                    CloseBank (player, onlinePlayer.View);
                }
            }
        }

        void OnServerSave ()
        {
            SaveData ();
        }

        void CheckConfig ()
        {
            if (Config ["VERSION"] == null) {
                // FOR COMPATIBILITY WITH INITIAL VERSIONS WITHOUT VERSIONED CONFIG
                ReloadConfig ();
            } else if (GetConfig<string> ("VERSION", "") != Version.ToString ()) {
                // ADDS NEW, IF ANY, CONFIGURATION OPTIONS
                ReloadConfig ();
            }
        }

        protected void ReloadConfig ()
        {
            Config ["VERSION"] = Version.ToString ();

            // NEW CONFIGURATION OPTIONS HERE
            Config ["Settings", "radiationMax"] = GetConfig ("Settings", "radiationMax", 1f);
            Config ["Settings", "NPCBankersOnly"] = GetConfig ("Settings", "NPCBankersOnly", false);
            Config ["Settings", "NPCIDs"] = GetConfig ("Settings", "NPCIDs", new List<object> ());
            if (itemLimits.Count == 0) {
                itemLimits = GetDefaultItemLimits ();
            }
            configFile.WriteObject ("Bank_ItemLimits", itemLimits);
            // END NEW CONFIGURATION OPTIONS

            PrintWarning ("Upgrading configuration file");
            SaveConfig ();
        }

        void SaveProfileByUser (ulong userID)
        {
            if (banks.ContainsKey (userID)) {
                SaveProfile (userID, banks [userID]);
            }
        }

        void SaveData ()
        {
            int t = 0;

            foreach (KeyValuePair<ulong, BankProfile> kvp in banks) {
                if (kvp.Value.dirty) {
                    SaveProfile (kvp.Key, kvp.Value);
                    t++;
                }
            }

            PrintToConsole ("Saved " + t.ToString () + " banks");
        }

        protected bool LoadProfile (ulong playerID, bool reload = false)
        {
#if DEBUG
            Puts ($"Loading Bank {playerID}");
#endif

            if (playerID == 0) {
                return false;
            }

            //if (banks.ContainsKey (playerID)) {
            //    return true;
            //}

            string path = "bank_" + playerID.ToString ();

            BankProfile profile = datafile.ReadObject<BankProfile> (path);

            if (!(profile is BankProfile)) {
                return false;
            }

            if (profile.Count == 0) {
                return false;
            }

            if (banks.ContainsKey (playerID)) {
                banks [playerID] = profile;
            } else {
                banks.Add (playerID, profile);
            }

            return true;
        }

        void SaveProfile (ulong playerID, BankProfile profile = null)
        {
            if (profile == null) {
                if (!banks.ContainsKey (playerID)) {
                    return;
                }
                profile = banks [playerID];
            }

            string path = "bank_" + playerID.ToString ();
            int pc = profile.Count;
            datafile.WriteObject (path, profile);
            profile.dirty = false;
        }

        #endregion

        #region API

        bool IsBankBox (BaseNetworkable entity)
        {
            foreach (KeyValuePair<BasePlayer, OnlinePlayer> kvp in onlinePlayers) {
                if (kvp.Value?.View?.net != null && entity?.net != null && kvp.Value.View.net.ID == entity.net.ID) {
                    return true;
                }
            }

            return false;
        }

        void AddNpc (string id)
        {
            npcids.Add (id);
        }

        void AddNpc (ulong id)
        {
            AddNpc (id.ToString ());
        }

        void RemoveNpc (string id)
        {
            if (npcids.Contains (id)) {
                npcids.Remove (id);
            }
        }

        void RemoveNpc (ulong id)
        {
            RemoveNpc (id.ToString ());
        }

        #endregion

        #region Oxide Hooks

        void OnUseNPC (BasePlayer npc, BasePlayer player)
        {
            if (!npcids.Contains (npc.UserIDString)) return;
            ShowBank (player, player);
        }

        object CanNetworkTo (BaseNetworkable entity, BasePlayer target)
        {
            if (entity == null || target == null || entity == target || entity.net == null) return null;
            if (target.IsAdmin) return null;

            OnlinePlayer onlinePlayer;
            bool IsMyBank = false;
            if (onlinePlayers.TryGetValue (target, out onlinePlayer)) {
                if (onlinePlayer.View != null && onlinePlayer.View.net != null && onlinePlayer.View.net.ID == entity.net.ID) {
                    IsMyBank = true;
                }
            }

            if (IsBankBox (entity) && !IsMyBank) return false;

            return null;
        }

        object OnEntityTakeDamage (BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (hitInfo == null)
                return null;

            if (entity == null)
                return null;

            if (IsBankBox (entity))
                return false;

            return null;
        }

        object CanUseLockedEntity (BasePlayer player, BaseLock lockItem)
        {
            if (player == null) {
                return null;
            }

            if (lockItem == null) {
                return null;
            }

            if (!keyring) {
                return null;
            }

            if (MasterKey != null) {
                var result = MasterKey.Call ("CanUseLockedEntity", player, lockItem);
                if (result is bool) {
                    return null;
                }
            }

            BankProfile bank;
            if (lockItem is KeyLock && banks.TryGetValue (player.userID, out bank) && bank.items != null) {
                KeyLock keyLock = (KeyLock)lockItem;

                List<int> codes = bank.items.Select (profile => profile.dataInt).Where (dataInt => dataInt != 0).ToList ();
                //foreach(ItemProfile profile in bank.items) {
                //    if (profile.dataInt != 0) {
                //        codes.Add(profile.dataInt);
                //    }
                //}

                if (!keyLock.IsLocked ()) {
                    return null;
                }

                if (keyLock.HasLockPermission (player)) {
                    return null;
                }

                foreach (int code in codes) {
                    if (code == keyLock.keyCode) {
                        return true;
                    }
                }

                return false;
            }

            return null;
        }

        void OnPlayerInit (BasePlayer player)
        {
            onlinePlayers [player].View = null;
            onlinePlayers [player].Target = null;
            onlinePlayers [player].Matches = null;

            if (!LoadProfile (player.userID)) {
                if (!banks.ContainsKey (player.userID)) {
                    banks.Add (player.userID, new BankProfile (player));
                }
            }
        }

        void OnPlayerDisconnected (BasePlayer player)
        {
            if (onlinePlayers [player].View != null) {
                ShowBank (player, onlinePlayers [player].View);
                SaveProfile (player.userID);
            }
        }

        void OnPlayerLootEnd (PlayerLoot inventory)
        {
            BasePlayer player;
            if ((player = inventory.GetComponent<BasePlayer> ()) == null)
                return;

            if (onlinePlayers.ContainsKey (player) && onlinePlayers [player].View != null) {
                if (onlinePlayers [player].View == inventory.entitySource) {
                    CloseBank (player, (StorageContainer)inventory.entitySource);
                }
            }
        }

        //void OnItemAddedToContainer(ItemContainer container, Item item)
        //{
        //    BasePlayer player = container.playerOwner;
        //    OnlinePlayer onlinePlayer;
        //    if (player != null && onlinePlayers.TryGetValue(player, out onlinePlayer)) {
        //        if (onlinePlayer.View != null && onlinePlayer.View.inventory == container) {
        //            int validAmount = GetValidAmount(item, container);

        //            if (validAmount <= 0) {
        //                item.MoveToContainer(container.playerOwner.inventory.containerMain);
        //                return;
        //            }

        //            if (validAmount < item.amount) {
        //                Item splitItem = item.SplitItem(item.amount - validAmount);

        //                splitItem.MoveToContainer(container.playerOwner.inventory.containerMain);
        //            }
        //        }
        //    }
        //}

        #endregion

        #region Commands

        [ChatCommand ("viewbank")]
        void ViewBank (BasePlayer player, string command, string [] args)
        {
            if (!IsAllowed (player)) return;

            if (args.Length < 1) {
                return;
            }

            var name = args [0];
            var ply = onlinePlayers [player];
            if (name == "list") {
                if (ply.Matches == null) {
                    return;
                }

                if (args.Length == 1) {
                    ShowMatchingPlayers (player);
                    return;
                }

                int index;
                if (!int.TryParse (args [1], out index)) {
                    return;
                }

                if (index > ply.Matches.Count) { } else
                    ShowBank (player, ply.Matches [index - 1]);

                return;
            }

            var matches = FindPlayersByName (name);
            if (matches.Count < 1) {
                return;
            }

            if (matches.Count > 1) {
                ply.Matches = matches;
                ShowMatchingPlayers (player);
                return;
            }

            ShowBank (player, matches [0]);
        }

        [ConsoleCommand ("bank")]
        void ccBank (ConsoleSystem.Arg arg)
        {
            cmdBank (arg.Connection.player as BasePlayer, arg.cmd.Name, arg.Args);
        }

        [ChatCommand ("bank")]
        void cmdBank (BasePlayer player, string command, string [] args)
        {
            if (npconly) return;

            ShowBank (player, player);
        }

        #endregion

        #region Core methods

        ItemLimit GetItemLimit (Item item)
        {
            object limit;
            if (itemLimits.TryGetValue (item.info.shortname, out limit) && limit is JObject) {
                return ((JObject)limit).ToObject<ItemLimit> ();
            }

            return null;
        }

        //int GetValidAmount(Item item, ItemContainer container) {
        //    ItemLimit limit;
        //    int totalAmount = container.GetAmount(item.info.itemid, false) - item.amount;

        //    if (itemLimits.TryGetValue(item.info.shortname, out limit)) {
        //        if (!limit.enabled) {
        //            return 0;
        //        }

        //        if (totalAmount < limit.minimum) {
        //            return 0;
        //        }

        //        if (totalAmount == 0 && item.amount > limit.maximum) {
        //            return limit.maximum;
        //        }

        //        if (totalAmount > limit.maximum) {
        //            return totalAmount - limit.maximum;
        //        }

        //        if (totalAmount == limit.maximum) {
        //            return 0;
        //        }
        //    }

        //    return item.amount;
        //}

        bool CanPlayerBank (BasePlayer player)
        {
            if (player.IsAdmin) {
                return true;
            }

            if (!permission.UserHasPermission (player.UserIDString, "bank.use")) {
                SendReply (player, GetMsg ("Denied: Permission", player));
                return false;
            }

            if (!player.CanBuild ()) {
                SendReply (player, GetMsg ("Denied: Privilege", player));
                return false;
            }

            if (radiationMax > 0 && player.radiationLevel > radiationMax) {
                SendReply (player, GetMsg ("Denied: Irradiated", player));
                return false;
            }

            if (player.IsSwimming ()) {
                SendReply (player, GetMsg ("Denied: Swimming", player));
                return false;
            }

            if (!player.IsOnGround () || player.IsFlying || player.isInAir) {
                SendReply (player, GetMsg ("Denied: Falling", player));
                return false;
            }

            if (player.isMounted) {
                SendReply (player, GetMsg ("Denied: Mounted", player));
                return false;
            }

            if (player.IsWounded ()) {
                SendReply (player, GetMsg ("Denied: Wounded", player));
                return false;
            }

            if (player.GetComponentInParent<CargoShip> ()) {
                SendReply (player, GetMsg ("Denied: Ship", player));
                return false;
            }

            if (player.GetComponentInParent<HotAirBalloon> ()) {
                SendReply (player, GetMsg ("Denied: Balloon", player));
                return false;
            }

            if (player.GetComponentInParent<Lift> ()) {
                SendReply (player, GetMsg ("Denied: Lift", player));
                return false;
            }

            if (!allowSafeZone && player.InSafeZone ()) {
                SendReply (player, GetMsg ("Denied: Safe Zone", player));
                return false;
            }

            var canBank = Interface.Call ("CanBank", player);
            if (canBank != null) {
                if (canBank is string) {
                    SendReply (player, Convert.ToString (canBank));
                } else {
                    SendReply (player, GetMsg ("Denied: Generic", player));
                }
                return false;
            }

            return true;
        }

        void ShowBank (BasePlayer player, BaseEntity target)
        {
            if (!CanPlayerBank (player))
                return;

            string playerID = player.userID.ToString ();

            if (cooldownMinutes > 0 && player.net.connection.authLevel < 1) {
                if (bankCooldowns.ContainsKey (playerID)) {
                    DateTime startTime = bankCooldowns [playerID];
                    DateTime endTime = DateTime.Now;

                    TimeSpan span = endTime.Subtract (startTime);
                    if (span.TotalMinutes > 0 && span.TotalMinutes < Convert.ToDouble (cooldownMinutes)) {
                        double timeleft = System.Math.Round (Convert.ToDouble (cooldownMinutes) - span.TotalMinutes, 2);
                        if (timeleft < 1) {
                            double timelefts = System.Math.Round ((Convert.ToDouble (cooldownMinutes) * 60) - span.TotalSeconds);
                            SendReply (player, string.Format (GetMsg ("Cooldown: Seconds", player), timelefts.ToString ()));
                        } else {
                            SendReply (player, string.Format (GetMsg ("Cooldown: Minutes", player), System.Math.Round (timeleft).ToString ()));
                        }
                        return;
                    } else {
                        bankCooldowns.Remove (playerID);
                    }
                }
            }

            if (!LoadProfile (player.userID) && !banks.ContainsKey (player.userID)) {
                banks.Add (player.userID, new BankProfile (player));
            }

            if (!bankCooldowns.ContainsKey (player.userID.ToString ()) && player.net.connection.authLevel < 1) {
                bankCooldowns.Add (playerID, DateTime.Now);
            }
            var ply = onlinePlayers [player];
            if (ply.View == null) {
#if DEBUG
                Puts ($"Opening bank {player.userID}");
#endif
                OpenBank (player, target);
                return;
            }

            CloseBank (player, ply.View);
            timer.In (1f, () => OpenBank (player, target));
        }

        void HideBank (BasePlayer player)
        {
            player.EndLooting ();
            var ply = onlinePlayers [player];
            if (ply.View == null) {
                return;
            }

            CloseBank (player, ply.View);
        }

        string GetBox (BasePlayer player)
        {
            foreach (KeyValuePair<string, object> kvp in boxPrefabs) {
                if (permission.UserHasPermission (player.UserIDString, kvp.Key)) {
                    return kvp.Value.ToString ();
                }
            }

            return defaultBoxPrefab;
        }

        int GetSlots (BasePlayer player)
        {
            var availableSlots = new List<int> ();
            availableSlots.Add (defaultSlots);

            foreach (KeyValuePair<string, object> kvp in boxSlots) {
                if (permission.UserHasPermission (player.UserIDString, kvp.Key)) {
                    availableSlots.Add (Convert.ToInt32 (kvp.Value));
                }
            }

            return availableSlots.Max ();
        }

        void OpenBank (BasePlayer player, BaseEntity targArg)
        {
            Subscribe (nameof (CanNetworkTo));
            Subscribe (nameof (OnEntityTakeDamage));
            //Subscribe(nameof(OnItemAddedToContainer));
            var pos = new Vector3 (player.transform.position.x, player.transform.position.y - 1, player.transform.position.z);
            string box = GetBox (player);

            int slots = GetSlots (player);

            var view = GameManager.server.CreateEntity (box, pos) as StorageContainer;
            view.GetComponent<DestroyOnGroundMissing> ().enabled = false;
            view.GetComponent<GroundWatch> ().enabled = false;

            if (!view) return;
            view.limitNetworking = true;
            view.transform.position = pos;

            player.EndLooting ();
            if (targArg is BasePlayer) {
                BasePlayer target = targArg as BasePlayer;
                BankProfile profile = banks [target.userID];
                ItemContainer bank = profile.GetContainer (target, slots);
                if (!containers.ContainsKey (bank)) {
                    containers.Add (bank, player.userID);
                }
                view.enableSaving = false;
                view.Spawn ();
                view.inventory = bank;

                profile.open = true;
                onlinePlayers [player].View = view;
                onlinePlayers [player].Target = target;

                timer.In (0.1f, delegate () {
                    view.PlayerOpenLoot (player);
                });
            }
        }

        void CloseBank (BasePlayer player, StorageContainer view)
        {
            if (!onlinePlayers.ContainsKey (player)) return;
            if (onlinePlayers [player].View == null) return;

            if (!banks.ContainsKey (player.userID)) {
                return;
            }

            BankProfile profile = banks [player.userID];

            InvalidateBank (player, profile, view);

            profile.items.Clear ();
            foreach (Item item in view.inventory.itemList) {
                profile.Add (item);
            }

            SaveProfile (player.userID, profile);

            foreach (Item item in view.inventory.itemList.ToArray ()) {
                if (item.position != -1) {
                    item.RemoveFromContainer ();
                    item.Remove (0f);
                }
            }

            profile.open = false;

            if (containers.ContainsKey (view.inventory)) {
                containers.Remove (view.inventory);
            }

            player.inventory.loot.containers = new List<ItemContainer> ();
            view.inventory = new ItemContainer ();

            if (player.inventory.loot.IsLooting ()) {
                player.SendConsoleCommand ("inventory.endloot", null);
            }


            onlinePlayers [player].View = null;
            onlinePlayers [player].Target = null;

            UnityEngine.Object.Destroy (view);

            //view.Kill (BaseNetworkable.DestroyMode.None);

            timer.In (1f, delegate () {
                if (onlinePlayers.Values.Count (p => p.View != null) <= 0) {
                    Unsubscribe (nameof (CanNetworkTo));
                    Unsubscribe (nameof (OnEntityTakeDamage));
                }
            });
        }

        void InvalidateBank (BasePlayer player, BankProfile profile, StorageContainer view)
        {
            bool returned = false;
            if (view != null && view.inventory != null && view.inventory.itemList != null) {
                foreach (var item in view.inventory.itemList.ToArray ()) {
                    ItemLimit limit = GetItemLimit (item);

                    if (limit != null) {
                        if (item.amount < limit.minimum) {
                            returned = true;
                            if (player.inventory.containerMain == null || (player.inventory.containerMain != null && !item.MoveToContainer (player.inventory.containerMain))) {
                                if (player.inventory.containerBelt == null || (player.inventory.containerBelt != null && !item.MoveToContainer (player.inventory.containerBelt))) {
                                    item.Drop (player.eyes.HeadForward () * 2, default (Vector3));
                                }
                            }
                            continue;
                        }

                        if (item.amount > limit.maximum) {
                            returned = true;
                            Item invalidItem;

                            if (limit.maximum > 0) {
                                invalidItem = item.SplitItem (item.amount - limit.maximum);
                            } else {
                                invalidItem = item;
                            }
                            if (invalidItem != null) {
                                if (player.inventory.containerMain == null || (player.inventory.containerMain != null && !invalidItem.MoveToContainer (player.inventory.containerMain))) {
                                    if (player.inventory.containerBelt == null || (player.inventory.containerBelt != null && !invalidItem.MoveToContainer (player.inventory.containerBelt))) {
                                        invalidItem.Drop (player.eyes.HeadForward () * 2, default (Vector3));
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (returned) {
                SendReply (player, GetMsg ("Limit: Return", player));
            }
        }

        #endregion

        #region HelpText
        void SendHelpText (BasePlayer player)
        {
            var sb = new StringBuilder ()
               .Append ("Bank by <color=#ce422b>Calytic</color>\n")
               .Append ("  ").Append ("<color=\"#ffd479\">/bank</color> - Open your bank box").Append ("\n");

            if (player.IsAdmin) {
                sb.Append ("  ").Append ("<color=\"#ffd479\">/viewbank \"Player Name\"</color> - View any players bank").Append ("\n");
            }
            player.ChatMessage (sb.ToString ());
        }
        #endregion

        #region Helper methods

        string GetMsg (string key, BasePlayer player = null)
        {
            return lang.GetMessage (key, this, player == null ? null : player.UserIDString);
        }

        List<BasePlayer> FindPlayersByName (string name)
        {
            List<BasePlayer> matches = new List<BasePlayer> ();

            foreach (var ply in BasePlayer.activePlayerList) {
                if (ply.displayName.ToLower ().Contains (name.ToLower ()))
                    matches.Add (ply);
            }

            foreach (var ply in BasePlayer.sleepingPlayerList) {
                if (ply.displayName.ToLower ().Contains (name.ToLower ()))
                    matches.Add (ply);
            }

            return matches;
        }

        void ShowMatchingPlayers (BasePlayer player)
        {
            int i = 0;
            foreach (var ply in onlinePlayers [player].Matches) {
                i++;
                player.ChatMessage ($"{i} - {ply.displayName} ({ply.userID})");
            }
        }

        bool IsAllowed (BasePlayer player)
        {
            if (player.IsAdmin) return true;
            SendReply (player, GetMsg ("Denied: Permission", player));
            return false;
        }

        T GetConfig<T> (string name, string name2, T defaultValue)
        {
            if (Config [name, name2] == null) {
                return defaultValue;
            }

            return (T)Convert.ChangeType (Config [name, name2], typeof (T));
        }

        T GetConfig<T> (string name, T defaultValue)
        {
            if (Config [name] == null) {
                return defaultValue;
            }

            return (T)Convert.ChangeType (Config [name], typeof (T));
        }

        #endregion
    }
}
