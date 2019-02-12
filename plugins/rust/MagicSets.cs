using System;
using System.Collections.Generic;
using Oxide.Core;
using System.Linq;
namespace Oxide.Plugins
{
    [Info("Magic Sets", "Norn", "0.2.0")]
    [Description("Allows users to store and create custom gearsets with one command")]

    class MagicSets : RustPlugin
    {
        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private const string PERMISSION_DEFAULT = "MagicSets.able";
        private const string PERMISSION_VIP = "MagicSets.vip";

        private List<SetInfo> Sets = new List<SetInfo>();

        class SetInfo
        {
            public int ID;
            public ulong OwnerID;
            public string Name;
            public bool Public;
            public int TimesUsed;
            public long LastUsed;
            public List<InternalItemInfo> Contents = new List<InternalItemInfo>();
            public SetInfo() { }
        }
        class InternalItemInfo
        {
            public string Shortname;
            public ulong SkinID;
            public InternalItemInfo() { }
        }

        void Init()
        {
            permission.RegisterPermission(PERMISSION_DEFAULT, this);
            permission.RegisterPermission(PERMISSION_VIP, this);
            LoadData();
        }

        void Unload() { SaveData(); }

        private long UnixTimestamp() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        private DateTime UnixToDateTime(long unixTime) { DateTime dt = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc); return dt.AddSeconds(unixTime).ToLocalTime(); }
        private long DateTimeToUnixTimestamp(DateTime dateTime) => (long)(TimeZoneInfo.ConvertTimeToUtc(dateTime) - new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc)).TotalSeconds;

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoValue"] = "<color=#e5f441>USAGE:</color> /sets [Add | Remove | Create | Reskin | List | Public | Clear]",
                ["ClearSuccess"] = "<color=#e5f441>INFO:</color> You have <color=#41f45e>successfully</color> removed {0} sets.",
                ["NoSets"] = "<color=#e5f441>INFO:</color> You have <color=#f44141>no</color> sets.",
                ["SetAlreadyExists"] = "<color=#e5f441>INFO:</color> A set with the name <color=#f44141>{0}</color> already exists!",
                ["SetAdded"] = "<color=#e5f441>INFO:</color> You have <color=#41f45e>added</color> a new set! (<color=#e5f441>{0}</color>)\n<color=#e5f441>/sets create <color=#41f45e>{1}</color> to create it</color>",
                ["SetAddUsage"] = "<color=#e5f441>USAGE:</color> /sets add <color=#41f45e>name</color>",
                ["SetNotExist"] = "<color=#e5f441>INFO:</color> A set with the name <color=#f44141>{0}</color> does not exist.",
                ["SetNotExistID"] = "<color=#e5f441>INFO:</color> Set with the ID <color=#f44141>{0}</color> does not exist.",
                ["SetNotPublic"] = "<color=#e5f441>INFO:</color> Set with id <color=#f44141>{0}</color> is not public.",
                ["SetRemoved"] = "<color=#e5f441>INFO:</color> You have successfully removed the set <color=#f44141>{0}</color>.",
                ["SetPublicUpdated"] = "<color=#e5f441>INFO:</color> Set <color=#f44141>{0}</color> is now <color=#e5f441>Public:</color> {1}.",
                ["RemoveUsage"] = "<color=#e5f441>USAGE:</color> /sets remove <color=#41f45e>name</color>",
                ["ReskinUsage"] = "<color=#e5f441>USAGE:</color> /sets reskin <color=#41f45e>name</color> or /sets reskin id <color=#41f45e>id</color> to reskin wearable items to a specific set.",
                ["ReskinSuccessful"] = "<color=#e5f441>INFO:</color> Successfully reskinned <color=#41f45e>{0}</color> items using set: <color=#e5f441>{1}</color>, ID: <color=#e5f441>{2}</color>.",
                ["PublicUsage"] = "<color=#e5f441>USAGE:</color> /sets public <color=#41f45e>name</color>",
                ["MaxSets"] = "You have reached your <color=#e5f441>maximum</color> allowed sets. (<color=#f44141>{0}</color>)",
                ["ListFormat"] = "<color=#42ebf4>{0}</color>\n<color=#e5f441>ID:</color> {1}, <color=#e5f441>Times Used:</color> {2}, <color=#e5f441>Public:</color> {3},\n<color=#e5f441>Contents:</color> {4}.",
                ["SetCreated"] = "<color=#e5f441>INFO:</color> You have successfully created a set!\n<color=#e5f441>Name:</color> {0}, <color=#e5f441>ID:</color> {1}.",
                ["NotEnoughResources"] = "<color=#f44141>ERROR:</color> You do not have enough resources to create set (<color=#41f45e>{0}</color>).",
                ["NOBPS"] = "<color=#f44141>ERROR:</color> You do not have the blueprints required to craft any of the items in this set or the set has corrupted.",
                ["MissingBPS"] = "<color=#e5f441>INFO:</color> <color=#f44141>{0}</color> items weren't crafted due to missing blueprints.",
                ["SetCreateUsage"] = "<color=#e5f441>USAGE:</color> /sets create <color=#41f45e>name</color> or /sets create id <color=#41f45e>id</color>",
                ["NoItems"] = "<color=#e5f441>INFO:</color> You are <color=#f44141>not</color> wearing any <color=#e5f441>suitable</color> gear. Please equip the items you want to add to a set.",
                ["NoPermission"] = "<color=#e5f441>INFO:</color> You <color=#f44141>don't</color> have access to this command.",
                ["NoWearables"] = "<color=#e5f441>INFO:</color> You <color=#f44141>must</color> be wearing attire to be able to reskin.",
                ["ReskinFailed"] = "<color=#e5f441>INFO:</color> Reskin has failed, you have <color=#f44141>0</color> items that match the set."
            }, this);
        }

        protected override void LoadDefaultConfig()
        {
            Puts("No configuration file found, generating...");

            Config["General", "RequireBP"] = true;
            Config["General", "RemoveOldEntries"] = true;
            Config["General", "ChargeForSets"] = true;

            Config["Limits", "Limit"] = 2;
            Config["Limits", "LimitVIP"] = 5;
            Config["Limits", "DaysUntilRemoval"] = 30;

            Config["Misc", "ForceWear"] = false;
        }

        private List<ItemAmount> ReturnIngredients(ItemDefinition item)
        {
            var bp = ItemManager.FindBlueprint(item);
            if (bp == null) return null;
            return bp.ingredients;
        }

        void LoadData()
        {
            Sets = Interface.Oxide.DataFileSystem.ReadObject<List<SetInfo>>(Name);
            if(Config["General", "ChargeForSets"] == null) { Config["General", "ChargeForSets"] = true; SaveConfig(); }
            Puts("Loaded " + Sets.Count + " sets from /data/" + Name + ".json");
            Puts("Users require blueprints to craft set items: " + Convert.ToString(Config["General", "RequireBP"]).ToUpper() + ".");
            if (Convert.ToBoolean(Config["General", "RemoveOldEntries"])) { Puts("Old entries will be removed after " + Convert.ToString(Config["Limits", "DaysUntilRemoval"]) + " days."); }
            RemoveOldEntries();
        }

        void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, Sets);

        void RemoveOldEntries()
        {
            if (Convert.ToBoolean(Config["General", "RemoveOldEntries"])) {
                DateTime expiryDate = DateTime.Now - TimeSpan.FromDays(Convert.ToInt32(Config["Limits", "DaysUntilRemoval"]));
                int count = Sets.Count(x => x.LastUsed <= DateTimeToUnixTimestamp(expiryDate));
                if (count != 0) { Sets.RemoveAll(x => x.LastUsed <= DateTimeToUnixTimestamp(expiryDate)); Puts("Removed " + count.ToString() + " old entries from /data/" + Name + ".json"); } else { Puts("No outdated entries to remove."); }
            }
        }

        private bool HasItem(BasePlayer player, string item, int amount)
        {
            if (amount <= 0) return false;
            var definition = ItemManager.FindItemDefinition(item);
            if (definition == null) return false;
            var pamount = player.inventory.GetAmount(definition.itemid);
            if (pamount < amount) return false;
            return true;
        }

        private bool TakeItem(BasePlayer player, string item, int amount)
        {
            if(!HasItem(player, item, amount)) { return false; }
            player.inventory.Take(null, ItemManager.FindItemDefinition(item).itemid, amount);
            return true;
        }

        private bool GiveSetItem(BasePlayer player, int itemid, ulong skinid, int amount, bool wear=false)
        {
            Item itemToGive;
            if (!player.IsConnected) { return false; }
            itemToGive = ItemManager.CreateByItemID(itemid, amount, skinid);
            if (itemToGive == null) { return false; }
            if (Convert.ToBoolean(Config["Misc", "ForceWear"]))
            {
                if (!itemToGive.MoveToContainer(player.inventory.containerWear) 
                    && !itemToGive.MoveToContainer(player.inventory.containerMain) 
                    && !itemToGive.MoveToContainer(player.inventory.containerBelt))
                { itemToGive.Drop(player.eyes.position, player.eyes.BodyForward() * 2f); }
            }
            else
            {
                if (!itemToGive.MoveToContainer(player.inventory.containerMain) 
                    && !itemToGive.MoveToContainer(player.inventory.containerBelt))
                { itemToGive.Drop(player.eyes.position, player.eyes.BodyForward() * 2f); }
            }
            return true;
        }

        [ChatCommand("sets")]
        private void SetsCommand(BasePlayer player, string command, string[] args) => SetsCommandHandler(player, args);

        private void SetsCommandHandler(BasePlayer player, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_DEFAULT)) { PrintToChat(player, Lang("NoPermission", player.UserIDString)); return; }
            if (args.Length == 0 || args.Length > 3) { PrintToChat(player, Lang("NoValue", player.UserIDString)); return; }
            string cmd = args[0].ToLower();
            switch (cmd)
            {
                case "clear":
                    ClearSets(player);
                    break;
                case "add":
                    AddFromArgs(player, args);
                    break;
                case "remove":
                    RemoveFromArgs(player, args);
                    break;
                case "public":
                    PublicStatusFromArgs(player, args);
                    break;
                case "list":
                    SetList(player);
                    break;
                case "create":
                    CreateFromArgs(player, args);
                    break;
                case "reskin":
                    ReskinFromArgs(player, args);
                    break;
                default:
                    PrintToChat(player, Lang("NoValue", player.UserIDString));
                    break;
            }
        }

        private void Reskin(BasePlayer player, int id)
        {
            if (!player.IsConnected) { return; }
            SetInfo Set = SetFromID(id);
            if(Set == null) { PrintToChat(player, Lang("SetNotExistID", player.UserIDString, id)); return; }
            if(player.inventory.containerWear.itemList.Count == 0) { PrintToChat(player, Lang("NoWearables", player.UserIDString)); return;  }
            int updateCount = 0;
            foreach(InternalItemInfo item in Set.Contents)
            {
                Item itemToUpdate = player.inventory.containerWear.itemList.FirstOrDefault(x => x.info.shortname.ToLower() == item.Shortname.ToLower());
                if(itemToUpdate == null) { continue; }
                itemToUpdate.skin = item.SkinID;
                itemToUpdate.MarkDirty();
                updateCount++;
            }
            if(updateCount != 0) { PrintToChat(player, Lang("ReskinSuccessful", player.UserIDString, updateCount.ToString(), Set.Name, Set.ID.ToString())); Set.TimesUsed++; Set.LastUsed = UnixTimestamp(); }
            else { PrintToChat(player, Lang("ReskinFailed", player.UserIDString)); }
        }

        private void ReskinFromArgs(BasePlayer player, string[] args)
        {
            if(args.Length == 3)
            {
                if (args[1].ToLower() == "id")
                {
                    int id = 0;
                    if (Int32.TryParse(args[2], out id))
                    {
                        SetInfo set = SetFromID(id);
                        if (set == null) { PrintToChat(player, Lang("SetNotExistID", player.UserIDString, id)); return; }
                        if (set.Public == false && set.OwnerID != player.userID) { PrintToChat(player, Lang("SetNotPublic", player.UserIDString, id.ToString())); return; }
                        Reskin(player, set.ID);
                    } else { PrintToChat(player, Lang("ReskinUsage", player.UserIDString)); }
                }
            }
            else if (args.Length == 2)
            {
                int setID = SetIDFromName(player, args[1]);
                if (setID == 0) { PrintToChat(player, Lang("SetNotExist", player.UserIDString, args[1].ToLower())); return; }
                Reskin(player, setID);
            }
            else { PrintToChat(player, Lang("ReskinUsage", player.UserIDString)); }
        }

        private void CreateFromArgs(BasePlayer player, string[] args)
        {
            if (args.Length == 3)
            {
                if (args[1].ToLower() == "id")
                {
                    int id = 0;
                    if (Int32.TryParse(args[2], out id))
                    {
                        SetInfo set = SetFromID(id);
                        if (set == null) { PrintToChat(player, Lang("SetNotExistID", player.UserIDString, id)); return; }
                        if (set.Public == false && set.OwnerID != player.userID) { PrintToChat(player, Lang("SetNotPublic", player.UserIDString, id.ToString())); return; }
                        CreateSet(player, set.ID);
                    }
                    else { PrintToChat(player, Lang("SetCreateUsage", player.UserIDString)); }
                }
            }
            else if (args.Length == 2)
            {
                int setID = SetIDFromName(player, args[1]);
                if (setID == 0) { PrintToChat(player, Lang("SetNotExist", player.UserIDString, args[1])); return; }
                CreateSet(player, setID);
            }
            else { PrintToChat(player, Lang("SetCreateUsage", player.UserIDString)); }
        }

        private void PublicStatusFromArgs(BasePlayer player, string[] args)
        {
            if (args.Length != 2) { PrintToChat(player, Lang("PublicUsage", player.UserIDString)); return; }
            UpdatePublicStatus(player, args[1]);
        }

        private void RemoveFromArgs(BasePlayer player, string[] args)
        {
            if (args.Length != 2) { PrintToChat(player, Lang("RemoveUsage", player.UserIDString)); return; }
            RemoveSet(player, args[1]);
        }

        private void AddFromArgs(BasePlayer player, string[] args)
        {
            if (args.Length != 2) { PrintToChat(player, Lang("SetAddUsage", player.UserIDString)); return; }
            AddSet(player, args[1]);
        }

        private void UpdatePublicStatus(BasePlayer player, string args)
        {
            if (!player.IsConnected) { return; }
            if (string.IsNullOrEmpty(args)) { PrintToChat(player, Lang("PublicUsage", player.UserIDString)); return; }
            string setname = args.ToLower();
            if (!SetExistsFromName(player, setname)) { PrintToChat(player, Lang("SetNotExist", player.UserIDString, setname)); return; }
            SetInfo setToUpdate = SetFromName(player, setname);
            if (setToUpdate == null) { return; }
            if (setToUpdate.Public) { setToUpdate.Public = false; } else { setToUpdate.Public = true; }
            PrintToChat(player, Lang("SetPublicUpdated", player.UserIDString, setname, FormatPublicString(setToUpdate.Public)));
        }

        private void CreateSet(BasePlayer player, int id)
        {
            if (!player.IsConnected) { return; }
            List<InternalItemInfo> filteredItemList = new List<InternalItemInfo>();
            Dictionary<ItemDefinition, float> Costs = new Dictionary<ItemDefinition, float>();

            SetInfo setToCreate = SetFromID(id);
            if (setToCreate == null) { return; }

            int missingBlueprints = 0;

            if (Convert.ToBoolean(Config["General", "RequireBP"]))
            {
                foreach (var item in setToCreate.Contents) // BP Check
                {
                    var def = ItemManager.FindItemDefinition(item.Shortname);
                    if (player.blueprints.HasUnlocked(def)) { filteredItemList.Add(item); } else { missingBlueprints++; }
                }
            }
            else { filteredItemList = setToCreate.Contents; }

            if (filteredItemList.Count == 0) { PrintToChat(player, Lang("NOBPS", player.UserIDString)); return; }

            if (Convert.ToBoolean(Config["General", "ChargeForSets"]))
            {
                foreach (InternalItemInfo item in filteredItemList)
                {
                    List<ItemAmount> ingredientsList = ReturnIngredients(ItemManager.FindItemDefinition(item.Shortname));
                    foreach (ItemAmount ingredients in ingredientsList)
                    {
                        if (!Costs.ContainsKey(ingredients.itemDef)) { Costs.Add(ingredients.itemDef, ingredients.amount); }
                        else { Costs[ingredients.itemDef] += ingredients.amount; }
                    }
                }
                int requiredCostItems = Costs.Count, currentItems = 0;
                foreach (var individualCost in Costs) { if (HasItem(player, individualCost.Key.shortname, Convert.ToInt32(individualCost.Value))) { currentItems++; } }
                if (currentItems != requiredCostItems) { PrintToChat(player, Lang("NotEnoughResources", player.UserIDString, setToCreate.Name)); return; }
                foreach (var individualCost in Costs) { TakeItem(player, individualCost.Key.shortname, Convert.ToInt32(individualCost.Value)); }
            }
            foreach (InternalItemInfo item in filteredItemList) { GiveSetItem(player, ItemManager.FindItemDefinition(item.Shortname).itemid, item.SkinID, 1); }
            PrintToChat(player, Lang("SetCreated", player.UserIDString, setToCreate.Name, setToCreate.ID));
            setToCreate.TimesUsed++;
            setToCreate.LastUsed = UnixTimestamp();
            int itemcount = setToCreate.Contents.Count - filteredItemList.Count;
            if (itemcount != 0 && Convert.ToBoolean(Config["General", "RequireBP"])) { PrintToChat(player, Lang("MissingBPS", player.UserIDString, itemcount.ToString())); }
        }

        private SetInfo SetFromName(BasePlayer player, string setname) => Sets.FirstOrDefault(x => x.OwnerID == player.userID && x.Name == setname);
        private SetInfo SetFromID(int id) => Sets.FirstOrDefault(x => x.ID == id);
        private bool SetExistsFromName(BasePlayer player, string setname) => Sets.Any(x => x.OwnerID == player.userID && x.Name == setname);
        private bool SetExistsFromID(int id) => Sets.Any(x => x.ID == id);
        private int SetIDFromName(BasePlayer player, string setname) { var set = Sets.Where(x => x.OwnerID == player.userID && x.Name == setname).FirstOrDefault(); if(set == null) { return 0; } return set.ID; }
        private IEnumerable<SetInfo> ReturnSets(BasePlayer player) { if (!HasSets(player) || !player.IsConnected) { return null; } return Sets.Where(x => x.OwnerID == player.userID); }
        private string FormatPublicString(bool status) { if (status) { return "<color=#41f45e>true</color>"; } else { return "<color=#f44141>false</color>"; } }
        private bool HasSets(BasePlayer player) => Sets.Any(x => x.OwnerID == player.userID);
        private int SetsNextID() { List<int> currentIdList = Sets.ConvertAll<int>(set => set.ID); return Enumerable.Range(1, Int32.MaxValue).Except(currentIdList).First(); }
        private int SetCount(BasePlayer player) => Sets.Count(x => x.OwnerID == player.userID);
        private int RemoveAllSets(BasePlayer player) { if (!player.IsConnected) { return 0; } int removed = SetCount(player); Sets.RemoveAll(x => x.OwnerID == player.userID); return removed; }

        private void SetList(BasePlayer player)
        {
            if (!player.IsConnected) { return; }
            if (!HasSets(player)) { PrintToChat(player, Lang("NoSets", player.UserIDString)); return; }
            foreach (var set in ReturnSets(player)) {
                string contents = string.Empty;
                foreach (var item in set.Contents.ToList())
                {
                    var defFromShortname = ItemManager.FindItemDefinition(item.Shortname);
                    if (defFromShortname == null) { set.Contents.Remove(item); continue; }
                    if (contents.Length == 0) { contents = defFromShortname.displayName.english; }
                    else { contents = contents + ", " + defFromShortname.displayName.english; }
                }
                string publicStatus = FormatPublicString(set.Public);
                PrintToChat(player, Lang("ListFormat", player.UserIDString, set.Name, set.ID.ToString(), set.TimesUsed.ToString(), publicStatus, contents));
            }
        }

        private void RemoveSet(BasePlayer player, string arg)
        {
            if (!player.IsConnected) { return; }
            if (string.IsNullOrEmpty(arg)) { PrintToChat(player, Lang("RemoveUsage", player.UserIDString)); return; }
            string setname = arg.ToLower();
            if (!HasSets(player)) { PrintToChat(player, Lang("NoSets", player.UserIDString)); return; }
            var setToRemove = SetFromName(player, setname);
            if (setToRemove == null) { PrintToChat(player, Lang("SetNotExist", player.UserIDString, setname)); return; }
            Sets.Remove(setToRemove);
            PrintToChat(player, Lang("SetRemoved", player.UserIDString, setname));
        }

        private void AddSet(BasePlayer player, string arg)
        {
            if (!player.IsConnected) { return; }
            if (string.IsNullOrEmpty(arg)) { PrintToChat(player, Lang("SetAddUsage", player.UserIDString)); return; }
            if(player.inventory.containerWear.itemList.Count == 0) { PrintToChat(player, Lang("NoItems", player.UserIDString)); return; }

            int count = SetCount(player);

            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_VIP) 
                && count >= Convert.ToInt16(Config["Limits", "Limit"])) {
                PrintToChat(player, Lang("MaxSets", player.UserIDString, Config["Limits", "Limit"].ToString()));
                return;
            } else if(permission.UserHasPermission(player.UserIDString, PERMISSION_VIP)  
                && count >= Convert.ToInt16(Config["Limits", "LimitVIP"])) {
                PrintToChat(player, Lang("MaxSets", player.UserIDString, Config["Limits", "LimitVIP"].ToString()));
                return;
            }

            string setname = arg.ToLower();

            if(SetExistsFromName(player, setname)) { PrintToChat(player, Lang("SetAlreadyExists", player.UserIDString, setname)); return; }

            SetInfo newSet = new SetInfo();
            newSet.OwnerID = player.userID;
            newSet.Name = setname;
            newSet.TimesUsed = 0;
            newSet.LastUsed = UnixTimestamp();
            newSet.ID = SetsNextID();
            newSet.Public = false;
			int itemsToAdd = 0;
            foreach (var wearItem in player.inventory.containerWear.itemList) {
                if (ReturnIngredients(wearItem.info) == null) { continue;  }
                InternalItemInfo setItem = new InternalItemInfo();
                setItem.Shortname = wearItem.info.shortname;
                setItem.SkinID = wearItem.skin;
                newSet.Contents.Add(setItem);
				itemsToAdd++;
            }
            if(itemsToAdd != 0) { Sets.Add(newSet); PrintToChat(player, Lang("SetAdded", player.UserIDString, newSet.Name, newSet.Name)); } 
			else { PrintToChat(player, Lang("NoItems", player.UserIDString)); }
        }

        private void ClearSets(BasePlayer player)
        {
            if (!player.IsConnected) { return; }
            if(!HasSets(player)) { PrintToChat(player, Lang("NoSets", player.UserIDString)); return; }
            PrintToChat(player, Lang("ClearSuccess", player.UserIDString, RemoveAllSets(player).ToString()));
        }
    }
}