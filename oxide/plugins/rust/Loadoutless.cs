using Oxide.Core;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using Rust;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Loadoutless", "Kechless", "1.3.2")]
    [Description("Players can respawn with a loadout")]
    class Loadoutless : RustPlugin
    {
        #region Variables
        private PluginConfig myConfig;
        //PERMISSIONS
        const string permission_save = "loadoutless.save";
        const string permission_setdefault = "loadoutless.setdefault";
        const string permission_getloadout = "loadoutless.getloadout";
        const string permission_banitem = "loadoutless.banitem";
        //FILES
        const string file_banlist = "Loadoutless_itembanlist";
        const string file_main = "Loadoutless_folder/";
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Error_Insuffarg"] = "Insufficient arguments!",
                ["Error_Noitem"] = "No item selected!",
                ["Error_Itemnotfound"] = "Item not found!",
                ["Error_Filenotexist"] = "Playerfile doesn't exist.",
                ["Error_Creatingnewfile"] = "Creating a new playerfile...",
                ["Cmd_Removebanitem"] = "Item is removed from the banfile.",
                ["Cmd_Removebanitem_notinfile"] = "Item was not banned!",
                ["Cmd_Banitem_already"] = "Item is already banned!",
                ["Cmd_Banitem"] = "Item <color=lime>sucessfully added</color> to the banfile!",
                ["Cmd_Setdefault"] = "Default loadout has <color=lime>succesfully been set!</color>",
                ["Cmd_Noperm"] = "You don't have the permsission to do that!",
                ["Cmd_Saved"] = "Loadout was <color=lime>sucessfully saved!</color>",
                ["Cmd_inv_added"] = "All inventory items are saved in the ban file"

            }, this);
        }
        #endregion

        #region Config
        private void Init()
        {
            myConfig = Config.ReadObject<PluginConfig>();
            permission.RegisterPermission(permission_save, this);
            permission.RegisterPermission(permission_setdefault, this);
            permission.RegisterPermission(permission_getloadout, this);
            permission.RegisterPermission(permission_banitem, this);
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new configuration file");
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                //default loadout can be set with in-game command '/loadout setdefault'
                Default_Loadout = "[{\"itemid\":-699558439,\"bp\":false,\"skinid\":0,\"container\":\"wear\",\"slot\":0,\"amount\":1,\"weapon\":false,\"mods\":[]},{\"itemid\":1850456855,\"bp\":false,\"skinid\":0,\"container\":\"wear\",\"slot\":1,\"amount\":1,\"weapon\":false,\"mods\":[]},{\"itemid\":-194953424,\"bp\":false,\"skinid\":0,\"container\":\"wear\",\"slot\":2,\"amount\":1,\"weapon\":false,\"mods\":[]},{\"itemid\":-1211166256,\"bp\":false,\"skinid\":0,\"container\":\"main\",\"slot\":0,\"amount\":74,\"weapon\":false,\"mods\":[]},{\"itemid\":1545779598,\"bp\":false,\"skinid\":0,\"container\":\"belt\",\"slot\":1,\"amount\":1,\"weapon\":true,\"mods\":[442289265]}]"
            };
        }

        private void SaveConfig()
        {
            Config.WriteObject(myConfig, true);
        }

        private class PluginConfig
        {
            public string Default_Loadout;
        }
        #endregion

        #region Commands

        [ChatCommand("loadout")]
        void loadoutcommand(BasePlayer player, string command, string[] args)
        {

            if (args.Length == 0)
            {
                SendMessage(player, "Error_Insuffarg");
            }
            else
            {
                switch (args[0].ToLower())
                {
                    //saves players current loadout with command '/loadout save'.
                    case "save":
                        PlayerLoadout user = get_user(player);
                        if (permission.UserHasPermission(player.UserIDString, permission_save))
                        {

                            user.items = check_banneditems(getPlayerLoadout(player));
                            SendMessage(player, "Cmd_Saved");
                            update_user(player, user);
                        }
                        else
                        {
                            SendMessage(player, "Cmd_Noperm");
                        }
                        break;
                    //sets default for all players when they don't have a player data file '/loadout setdefault'.
                    case "setdefault":
                        if (permission.UserHasPermission(player.UserIDString, permission_setdefault))
                        {
                            myConfig.Default_Loadout = JsonConvert.SerializeObject(getPlayerLoadout(player));
                            SaveConfig();
                            SendMessage(player, "Cmd_Setdefault");
                        }
                        else
                        {
                            SendMessage(player, "Cmd_Noperm");
                        }
                        break;
                    //bans current item in handslot and adds it to the banfile '/loadout banitem'.
                    case "banitem":
                        if (permission.UserHasPermission(player.UserIDString, permission_banitem))
                        {

                            try
                            {
                                List<string> ban_list = load_banfile();
                                string itemid = player.GetActiveItem().info.itemid.ToString();
                                if (check_banfile(itemid) == false)
                                {
                                    ban_list.Add(itemid);
                                    update_banfile(ban_list);
                                    SendMessage(player, "Cmd_Banitem");
                                }
                                else
                                {
                                    SendMessage(player, "Cmd_Banitem_already");
                                }
                            }
                            catch (NullReferenceException)
                            {

                                SendMessage(player, "Error_Noitem");

                            }


                        }
                        else
                        {
                            SendMessage(player, "Cmd_Noperm");
                        }
                        break;
                    //removes current item in handslot from the banfile '/loadout removebanitem'.
                    case "removebanitem":
                        if (permission.UserHasPermission(player.UserIDString, permission_banitem))
                        {
                            try
                            {
                                string itemid = player.GetActiveItem().info.itemid.ToString();
                                if (check_banfile(itemid) == true)
                                {
                                    update_banfile(removeItemBanlist(load_banfile(), itemid));
                                    SendMessage(player, "Cmd_Removebanitem");

                                }
                                else
                                {
                                    SendMessage(player, "Cmd_Removebanitem_notinfile");
                                }
                            }
                            catch (NullReferenceException)
                            {
                                SendMessage(player, "Cmd_Error_Noitem");
                            }
                        }
                        else
                        {
                            SendMessage(player, "Cmd_Noperm");
                        }
                        break;

                    case "baninv":
                        if (permission.UserHasPermission(player.UserIDString, permission_banitem))
                        {
                            addInvban(player.inventory.AllItems());
                            SendMessage(player, "Cmd_inv_added");
                        }
                        else
                        {
                            SendMessage(player, "Cmd_Noperm");
                        }
                        break;

                    default:
                        SendMessage(player, "Error_Insuffarg");
                        break;
                }
            }
        }

        #endregion

        #region Hooks

        object OnPlayerSpawn(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, permission_getloadout))
            {
                give_loadout(player);
            }

            return null;
        }

        //On player init checks if player file exsits if not, it will create one.
        void OnPlayerInit(BasePlayer player)
        {
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile(file_main + player.UserIDString))
            {
                PlayerLoadout user = new PlayerLoadout();
                user._username = player.displayName.ToString();
                user.items = JsonConvert.DeserializeObject<List<LoadoutItem>>(myConfig.Default_Loadout);
                update_user(player, user);
            }
        }

        //Set player inventory when player clicks on respawn.
        void OnPlayerRespawned(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, permission_getloadout))
            {
                give_loadout(player);
            }
        }
        #endregion

        #region Methodes

        public void addInvban(Item[] invItems)
        {
            List<string> list = new List<string>();
            List<string> ban_file = load_banfile();
            foreach (var item in invItems)
            {
                if (check_banfile(item.info.itemid.ToString()) == false)
                {
                    ban_file.Add(item.info.itemid.ToString());
                }
                else
                {
                    PrintToConsole(item.info.shortname + "already in banfile");
                }
                update_banfile(ban_file);
            }
        }
        //Reads player file and returns this as a object.
        PlayerLoadout get_user(BasePlayer player)
        {
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile(file_main + player.UserIDString))
            {
                PlayerLoadout user = new PlayerLoadout();
                user._username = player.displayName.ToString();
                user.items = JsonConvert.DeserializeObject<List<LoadoutItem>>(myConfig.Default_Loadout);
                update_user(player, user);
                SendMessage(player, "Error_Filenotexist");
                SendMessage(player, "Error_Creatingnewfile");
                return user;
            }
            else
            {
                string raw_player_file = Interface.Oxide.DataFileSystem.ReadObject<string>(file_main + player.UserIDString);
                return JsonConvert.DeserializeObject<PlayerLoadout>(raw_player_file);
            }
        }

        //Rewrites the player file.
        void update_user(BasePlayer player, PlayerLoadout user)
        {
            Interface.Oxide.DataFileSystem.WriteObject<string>(file_main + player.UserIDString, JsonConvert.SerializeObject(user));
        }
        //Updates the ban file.
        void update_banfile(List<string> ban_list)
        {
            Interface.Oxide.DataFileSystem.WriteObject<string>(file_main + file_banlist, JsonConvert.SerializeObject(ban_list));
        }
        //Checks if itemid already exists in the ban file.
        bool check_banfile(string itemid)
        {
            List<string> ban_file = load_banfile();
            if (ban_file.Contains(itemid))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        //Removes itemid from the ban file.
        List<string> removeItemBanlist(List<string> banfile, string itemid)
        {
            List<string> new_banfile = new List<string>();
            foreach (string item in banfile)
            {
                if (item != itemid)
                {
                    new_banfile.Add(item);
                }
            }
            return new_banfile;
        }

        List<string> load_banfile()
        {
            List<string> ban_file;
            //When ban file is needed checks if it exists if not, it will create one.
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile(file_main + file_banlist))
            {
                ban_file = new List<string>();
            }
            else
            {
                string raw_banfile = Interface.Oxide.DataFileSystem.ReadObject<string>(file_main + file_banlist);
                ban_file = JsonConvert.DeserializeObject<List<string>>(raw_banfile);
            }

            return ban_file;
        }

        //Gets player's loadout.
        static List<LoadoutItem> getPlayerLoadout(BasePlayer player)
        {
            List<LoadoutItem> Litem = new List<LoadoutItem>();
            foreach (Item item in player.inventory.containerWear.itemList)
            {
                if (item != null)
                {
                    LoadoutItem Iitem = ProcessItem(item, "wear");
                    Litem.Add(Iitem);
                }
            }
            foreach (Item item in player.inventory.containerMain.itemList)
            {
                if (item != null)
                {
                    LoadoutItem Iitem = ProcessItem(item, "main");
                    Litem.Add(Iitem);
                }
            }
            foreach (Item item in player.inventory.containerBelt.itemList)
            {
                if (item != null)
                {
                    LoadoutItem Iitem = ProcessItem(item, "belt");
                    Litem.Add(Iitem);
                }
            }
            return Litem;
        }

        static private LoadoutItem ProcessItem(Item item, string container)
        {
            LoadoutItem iItem = new LoadoutItem();
            iItem.amount = item.amount;
            iItem.mods = new List<int>();
            iItem.container = container;
            iItem.skinid = Convert.ToInt32(item.skin);
            iItem.itemid = item.info.itemid;
            iItem.weapon = false;
            iItem.slot = item.position;

            if (item.info.category.ToString() == "Weapon")
            {
                BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                if (weapon != null)
                {
                    if (weapon.primaryMagazine != null)
                    {
                        iItem.weapon = true;
                        if (item.contents != null)
                            foreach (var mod in item.contents.itemList)
                            {
                                if (mod.info.itemid != 0)
                                    iItem.mods.Add(mod.info.itemid);
                            }
                    }
                }
            }
            return iItem;
        }

        private Item BuildWeapon(int id, ulong skin, List<int> mods)
        {
            Item item = CreateByItemID(id, 1, skin);
            var weapon = item.GetHeldEntity() as BaseProjectile;
            if (weapon != null)
            {
                (item.GetHeldEntity() as BaseProjectile).primaryMagazine.contents = (item.GetHeldEntity() as BaseProjectile).primaryMagazine.capacity;
            }
            if (mods != null)
            {
                foreach (var mod in mods)
                {
                    item.contents.AddItem(BuildItem(mod, 1, 0).info, 1);
                }
            }
            return item;
        }

        private Item BuildItem(int itemid, int amount, ulong skin)
        {
            if (amount < 1) amount = 1;
            Item item = CreateByItemID(itemid, amount, skin);
            return item;
        }

        private Item CreateByItemID(int itemID, int amount = 1, ulong skin = 0)
        {
            return ItemManager.CreateByItemID(itemID, amount, skin);
        }

        bool GiveItem(PlayerInventory inv, Item item, ItemContainer container = null, int position = 0)
        {
            if (item == null) { return false; }
            return (((container != null) && item.MoveToContainer(container, position, true)) || (item.MoveToContainer(inv.containerMain, position, true) || item.MoveToContainer(inv.containerBelt, position, true)));
        }

        List<LoadoutItem> check_banneditems(List<LoadoutItem> list)
        {
            List<LoadoutItem> new_list = new List<LoadoutItem>();
            List<string> ban_file = load_banfile();

            foreach (LoadoutItem item in list)
            {
                if (!ban_file.Contains(item.itemid.ToString()))
                {
                    new_list.Add(item);
                }
            }

            return new_list;
        }

        //Clears players inventory and gives loadout of the player
        public object give_loadout(BasePlayer player)
        {
            var user = get_user(player);
            player.inventory.Strip();

            foreach (var kitem in user.items)
            {
                GiveItem(player.inventory,
                    kitem.weapon
                        ? BuildWeapon(kitem.itemid, Convert.ToUInt32(kitem.skinid), kitem.mods)
                        : BuildItem(kitem.itemid, kitem.amount, Convert.ToUInt32(kitem.skinid)),
                    kitem.container == "belt"
                        ? player.inventory.containerBelt
                        : kitem.container == "wear"
                            ? player.inventory.containerWear
                            : player.inventory.containerMain, kitem.slot);
            }
            return true;
        }

        //Send message to player.
        public void SendMessage(BasePlayer player, string message)
        {
            PrintToChat(player, lang.GetMessage(message, this, player.UserIDString));
        }
        #endregion

        #region Classes
        public class PlayerLoadout
        {
            public string _username;
            public List<LoadoutItem> items = new List<LoadoutItem>();

            public PlayerLoadout()
            {
            }

            [JsonConstructor]
            public PlayerLoadout(string userName, List<LoadoutItem> Items)
            {
                _username = userName;
                items = Items;
            }
        }

        public class LoadoutItem
        {
            public int itemid;
            public bool bp;
            public int skinid;
            public string container;
            public int slot;
            public int amount;

            public bool weapon;
            public List<int> mods = new List<int>();


            public LoadoutItem()
            {
            }

            [JsonConstructor]
            public LoadoutItem(int pitemid, bool pbp, int pskinid, string pcontainer, int pamount, int pslot, bool pweapon, List<int> pmods)
            {
                itemid = pitemid;
                pbp = bp;
                skinid = pskinid;
                container = pcontainer;
                amount = pamount;
                slot = pslot;
                weapon = pweapon;
                mods = pmods;
            }
        }
        #endregion
    }
}
