using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using System.Collections;

namespace Oxide.Plugins
{
    [Info("QuickLoadouts", "carny666", "1.0.1", ResourceId = 2731)]
    class QuickLoadouts : RustPlugin
    {
        const string adminPermission = "QuickLoadouts.admin";
        const string allPermission = "QuickLoadouts.all";
        const string loadoutFileName = "QuickLoadouts.loadouts";

        const string AdminCommandChatConsole = "quickloadout";
        const string UseCommandChatConsole = "useloadout";

        Loadouts loadouts = new Loadouts();

        void Init()
        {
            try
            {
                loadouts = Interface.Oxide.DataFileSystem.ReadObject<Loadouts>(loadoutFileName);

                permission.RegisterPermission(adminPermission, this);

                foreach (var l in loadouts.loadouts)
                    permission.RegisterPermission($"QuickLoadouts.{l.name}", this);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error in Loaded {ex.Message}");
            }
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
                {
                    { "listreply", "Listing Loadouts:" },
                    { "nolistreply", "There are no loadouts." },
                    { "removeusagereply", "usage:  /loadout rem LoadoutName" },
                    { "removereply", "'{name}' Removed." },
                    { "addusagereply", "usage:  /loadout add LoadoutName" },
                    { "addreply", "Added '{name}'. You have to supply the proper permissions to use this loadout. " },
                    { "usereply", "Loadout supplied." },
                    { "clearreply", "Loadouts all cleared." },
                    { "loadoutnotfoundreply", "Loadouts not found." },
                    { "loadoutnoexistreply", "Loadout not found." }
                }, this, "en");
        }

        #region classes 
        class Loadouts
        {
            public List<Loadout> loadouts;

            public Loadouts()
            {
                loadouts = new List<Loadout>();
            }

            public bool AddPlayersLoadout(BasePlayer player, string Name)
            {
                try
                {
                    loadouts.Add(new Loadout(player) { name = Name.ToLower() });
                    return true;
                }
                catch (Exception ex)
                {
                    throw new Exception("Loadouts Error: AddPlayersLoadout:" + ex.StackTrace);
                }
            }

            public bool ReplacePlayersLoadout(BasePlayer player, string Name)
            {
                try
                {
                    if (loadouts.Any(x => x.name.ToString() == Name))
                    {
                        var index = loadouts.IndexOf(loadouts.First(x => x.name.ToString().ToLower() == Name.ToLower()));
                        loadouts.RemoveAt(index);
                        loadouts.Insert(index, new Loadout(player) { name = Name.ToLower() });
                        return true;
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    throw new Exception("Loadouts Error: AddPlayersLoadout:" + ex.StackTrace);
                }
            }

            public bool LoadoutExistByName(string Name)
            {
                return (loadouts.Any(x => x.name.ToString().ToLower() == Name.ToLower()));
            }

        }

        class Loadout
        {
            public object name { get; set; }
            public List<ModItem> containerWear = new List<ModItem>();
            public List<ModItem> containerBelt = new List<ModItem>();
            public List<ModItem> containerMain = new List<ModItem>();

            public Loadout()
            {
                name = "";
                containerWear = new List<ModItem>();
                containerBelt = new List<ModItem>();
                containerMain = new List<ModItem>();
            }

            public Loadout(object packName)
            {
                if (packName != null)
                    name = packName.ToString().ToLower();
                else
                    name = "";

                containerWear = new List<ModItem>();
                containerBelt = new List<ModItem>();
                containerMain = new List<ModItem>();
            }

            public Loadout(BasePlayer player)
            {
                try
                {
                    containerWear = new List<ModItem>();
                    containerBelt = new List<ModItem>();
                    containerMain = new List<ModItem>();

                    foreach (var i in player.inventory.containerWear.itemList)
                    {
                        containerWear.Add(new ModItem(i));
                        player.SendNetworkUpdate();
                    }

                    foreach (var i in player.inventory.containerMain.itemList)
                    {
                        containerMain.Add(new ModItem(i));
                        player.SendNetworkUpdate();
                    }

                    foreach (var i in player.inventory.containerBelt.itemList)
                    {
                        containerBelt.Add(new ModItem(i));
                        player.SendNetworkUpdate();
                    }

                }
                catch (Exception ex)
                {
                    throw new Exception("Error PlayerLoadout:", ex);
                }
            }

            public void SupplyLoadout(BasePlayer Player, bool clearInventory = false)
            {
                try
                {
                    if (clearInventory)
                    {
                        foreach (Item i in Player.inventory.AllItems())
                            i.Remove(0f);
                        Player.SendNetworkUpdate();
                    }

                    Player.inventory.containerMain.Clear();
                    Player.inventory.containerWear.Clear();
                    Player.inventory.containerBelt.Clear();

                    foreach (ModItem e in containerWear)
                        if (e.Item() != null)
                            Player.inventory.GiveItem(e.Item(), Player.inventory.containerWear);


                    foreach (ModItem e in containerMain)
                        if (e.Item() != null)
                            Player.inventory.GiveItem(e.Item(), Player.inventory.containerMain);

                    foreach (ModItem e in containerBelt)
                        if (e.Item() != null)
                            Player.inventory.GiveItem(e.Item(), Player.inventory.containerBelt);

                    Player.SendNetworkUpdate();
                }
                catch (Exception ex)
                {
                    throw new Exception("Error ArenaPlayer:SupplyLoadout:", ex);
                }
            }

        }

        class ModItem
        {
            public string equipName;
            public int amount;
            public ulong skinId;

            public ModItemType etype;

            public List<string> addons = new List<string>();

            public ModItem()
            {
                equipName = "";
                amount = 0;
                skinId = 0;
                addons = new List<string>();
            }

            public ModItem(string EquipName, ModItemType type, int Amount = 1)
            {
                equipName = EquipName;
                amount = Amount;
                etype = type;
                addons = new List<string>();
            }

            public ModItem(string EquipName, string[] Addons, int AmmoAmount = -1)
            {
                equipName = EquipName;
                amount = 1;
                etype = ModItem.ModItemType.Weapon;
                addons = new List<string>();
                foreach (string s in Addons)
                    addons.Add(s);
            }

            public ModItem(Item item)
            {
                try
                {
                    var id = ItemManager.FindItemDefinition(item.info.itemid);
                    equipName = id.shortname;
                    amount = item.amount;
                    skinId = item.skin;

                    if (item.contents != null)
                    {
                        foreach (Item i in item.contents.itemList)
                        {
                            var iid = ItemManager.FindItemDefinition(i.info.itemid);
                            addons.Add(iid.shortname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("Error ModItem:", ex);
                }

            }

            public Item Item()
            {
                try
                {
                    var definition = ItemManager.FindItemDefinition(this.equipName);
                    if (definition != null)
                    {
                        Item item = ItemManager.CreateByItemID((int)definition.itemid, this.amount, skinId);
                        if (item != null)
                        {
                            if (this.etype == ModItem.ModItemType.Weapon)
                            {
                                // If weapon fill magazine to capacity
                                var weapon = item.GetHeldEntity() as BaseProjectile;
                                if (weapon != null)
                                {
                                    (item.GetHeldEntity() as BaseProjectile).primaryMagazine.contents = weapon.primaryMagazine.capacity;
                                }

                                foreach (var a in this.addons)
                                {
                                    var addonDef = ItemManager.FindItemDefinition(a);
                                    Item addonItem = ItemManager.CreateByItemID((int)addonDef.itemid, 1);
                                    item.contents.AddItem(addonItem.info, 1);
                                }
                            }
                            return item;
                        }
                    }
                    return null;
                }
                catch (Exception ex)
                {
                    throw new Exception("Error ModItem:Item", ex);
                }
            }
            public enum ModItemType
            {
                Weapon,
                Item
            }

        }
        #endregion

        void UseLoadout(BasePlayer player, string LoaoutName)
        {
            var lo = (loadouts.loadouts.Any(x => x.name.ToString() == LoaoutName)) ? loadouts.loadouts.First(x => x.name.ToString() == LoaoutName) : null;
            if (lo == null)
            {
                PrintToChat(player, lang.GetMessage("loadoutnotfoundreply", this, player.UserIDString));
                return;
            }
            UseLoadout(player, lo);

        }

        void UseLoadout(BasePlayer player, Loadout l)
        {
            if (permission.UserHasPermission(player.UserIDString, allPermission)
                ||
                permission.UserHasPermission(player.UserIDString, "QuickLoadouts." + l.name))
            {
                l.SupplyLoadout(player);
                PrintToChat(player, lang.GetMessage("usereply", this, player.UserIDString));
            }
        }

        [ChatCommand(UseCommandChatConsole)]
        void chatCommandUseLoadout(BasePlayer player, string command, string[] args)
        {
            try
            {
                if (args.Length < 1) return;
                UseLoadout(player, args[0]);
            }
            catch (Exception ex)
            {
                throw new Exception("Error consoleCommandUseLoadout.", ex);
            }
        }

        [ConsoleCommand(UseCommandChatConsole)]
        void consoleCommandUseLoadout(ConsoleSystem.Arg arg)
        {
            try
            {
                if (arg.Args.Length < 1) return;
                if (arg.Player() == null) return;
                BasePlayer player = arg.Player();
                UseLoadout(player, arg.Args[0]);
            }
            catch (Exception ex)
            {
                throw new Exception("Error consoleCommandUseLoadout.", ex);
            }
        }

        [ChatCommand(AdminCommandChatConsole)]
        void chatCommandquickloadout(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, adminPermission))
                return;

            #region  List
            if (args.Length == 0) // list loadouts
            {
                int ii = 0;
                if (loadouts.loadouts.Count() > 0)
                {
                    PrintToChat(player, lang.GetMessage("listreply", this, player.UserIDString));
                    foreach (var l in loadouts.loadouts)
                        PrintToChat(player, $"{(++ii).ToString()} {l.name}");
                }
                else
                    PrintToChat(player, lang.GetMessage("nolistreply", this, player.UserIDString));
                return;
            }
            #endregion

            switch (args[0].ToLower())
            {
                case "rem":
                case "remove":
                    if (args.Count() < 2)
                        PrintToChat(player, lang.GetMessage("removeusagereply", this, player.UserIDString));
                    else
                    {

                        if (loadouts.loadouts.Any(x => x.name.ToString().ToLower() == args[1].ToLower().ToString()))
                        {
                            loadouts.loadouts.Remove(loadouts.loadouts.First(x => x.name.ToString().ToLower() == args[1].ToLower().ToString()));
                            Interface.Oxide.DataFileSystem.WriteObject(loadoutFileName, loadouts);
                            PrintToChat(player, lang.GetMessage("removereply", this, player.UserIDString).Replace("{name}", args[1].ToString()));
                        }
                    }
                    break;

                case "add":
                    if (args.Count() < 2)
                        PrintToChat(player, lang.GetMessage("addusagereply", this, player.UserIDString));
                    else
                    {
                        string Name = "";
                        foreach (var s in args)
                            if (s != "add") Name += " " + s;
                        var name = Name.Replace("'", "").Trim();

                        if (loadouts.LoadoutExistByName(name))
                            loadouts.ReplacePlayersLoadout(player, name);
                        else
                            loadouts.AddPlayersLoadout(player, name);

                        permission.RegisterPermission($"QuickLoadouts.{name}", this);
                        Interface.Oxide.DataFileSystem.WriteObject(loadoutFileName, loadouts);

                        PrintToChat(player, lang.GetMessage("addreply", this, player.UserIDString).Replace("{name}", name));
                    }
                    break;

                case "use":
                    var l = (loadouts.loadouts.Any(x => x.name.ToString().ToLower() == args[1].ToLower().ToString())) ? loadouts.loadouts.First(x => x.name.ToString().ToLower() == args[1].ToLower().ToString()) : null;
                    if (l != null)
                    {
                        l.SupplyLoadout(player);
                        PrintToChat(player, lang.GetMessage("usereply", this, player.UserIDString));
                    }
                    else
                        PrintToChat(player, lang.GetMessage("loadoutnoexistreply", this, player.UserIDString));
                    break;

                case "clear":
                    loadouts.loadouts.Clear();
                    Interface.Oxide.DataFileSystem.WriteObject(loadoutFileName, loadouts);
                    PrintToChat(player, lang.GetMessage("clearreply", this, player.UserIDString));

                    break;
                default:
                    break;
            }

        }

    }
}