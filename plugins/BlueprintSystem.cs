using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using ProtoBuf;
using Rust;
using System.Collections;
using System.Text;

//
//          FOR ANYONE READING THIS, PLEASE NOTE: THIS IS STILL IN EARLY STAGES.
//

    // check the issues with crafting
    // especially when there is a single item that is needed

namespace Oxide.Plugins
{
    [Info("BlueprintSystem", "redBDGR / Fujikura", "2.1.4", ResourceId = 2381)]
    [Description("Bring back the old blueprint system, with some small changes")]
    class BlueprintSystem : RustPlugin
    {

        #region config

        bool Changed = false;

        // Fuji
        FieldInfo _cachedData = typeof(UserPersistance).GetField("cachedData", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        FieldInfo _user = typeof(ResearchTable).GetField("user", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        static BlueprintSystem bpS = null;

        static List<object> BlockedBPs()
        {
            var bBP = new List<object>()
            {
                "antiradpills",
                "attire.hide.skirt",
                "attire.hide.boots",
                "attire.hide.helterneck",
                "attire.hide.pants",
                "attire.hide.poncho",
                "attire.hide.vest",
                "burlap.gloves",
                "burlap.headwrap",
                "burlap.shirt",
                "burlap.shoes",
                "burlap.trousers",
                "bandage",
                "apple",
                "apple.spoiled",
                "battery.small",
                "bearmeat",
                "bearmeat.burned",
                "bearmeat.cooked",
                "black.raspberries",
                "blueberries",
                "bleach",
                "blood",
                "blueprintbase",
                "bone.fragments",
                "cactusflesh",
                "door.closer",
                "mining.pumpjack",
                "can.beans",
                "can.beans.empty",
                "can.tuna",
                "can.tuna.empty",
                "candycane",
                "cctv.camera",
                "charcoal",
                "chicken.burned",
                "chicken.cooked",
                "chicken.raw",
                "chicken.spoiled",
                "clone.corn",
                "clone.hemp",
                "clone.pumpkin",
                "cloth",
                "coal",
                "corn",
                "crude.oil",
                "door.key",
                "door.hinged.wood",
                "door.hinged.metal",
                "fat.animal",
                "fish.cooked",
                "fish.minnows",
                "fish.raw",
                "fish.troutsmall",
                "flare",
                "furnace",
                "gears",
                "generator.wind.scrap",
                "glue",
                "granolabar",
                "hat.beenie",
                "hat.boonie",
                "hat.candle",
                "hat.cap",
                "hat.miner",
                "hat.wolf",
                "hammer",
                "hq.metal.ore",
                "humanmeat.burned",
                "humanmeat.cooked",
                "humanmeat.raw",
                "humanmeat.spoiled",
                "leather",
                "lmg.m249",
                "map",
                "meat.boar",
                "meat.pork.burned",
                "meat.pork.cooked",
                "metal.fragments",
                "metal.ore",
                "metal.refined",
                "metalblade",
                "metalpipe",
                "metalspring",
                "mushroom",
                "pistol.m92",
                "propanetank",
                "pumpkin",
                "pookie.bear",
                "research.table",
                "rifle.lr300",
                "riflebody",
                "rope",
                "jackolantern.angry",
                "jackolantern.happy",
                "santahat",
                "seed.corn",
                "seed.hemp",
                "seed.pumpkin",
                "semibody",
                "sewingkit",
                "sheetmetal",
                "shirt.tanktop",
                "skull.human",
                "skull.wolf",
                "shirt.collared",
                "smallwaterbottle",
                "sticks",
                "stocking.large",
                "stocking.small",
                "stones",
                "stone.pickaxe",
                "stonehachet",
                "sulfur",
                "sulfur.ore",
                "supply.signal",
                "tarp",
                "targeting.computer",
                "techparts",
                "tool.camera",
                "water",
                "water.salt",
                "waterjug",
                "wolfmeat.burned",
                "wolfmeat.cooked",
                "wolfmeat.raw",
                "wolfmeat.spoiled",
                "wood",
                "wood.armor.jacket",
                "wood.armor.pants",
                "vending.machine",
                "xmas.present.large",
                "xmax.present.medium",
                "xmas.present.small",
                "ducttape",
                "chocholate",
                "smgbody",
                "ammo.rocket.smoke",
                "researchpaper",
                "hazmatsuit",
                "roadsigns",
                "deermeat.burned",
                "deermeat.cooked",
                "deermeat.raw",
                "wood.armor.helmet",
            };
            return bBP;
        }
        static List<object> DefaultBPs()
        {
            var dBP = new List<object>()
            {
                "rock",
                "torch",
                "paper",
                "hammer",
                "lowgradefuel",
                "gunpowder",
                "ammo.pistol",
                "arrow.wooden",
                "attire.hide.boots",
                "attire.hide.helterneck",
                "attire.hide.pants",
                "attire.hide.poncho",
                "attire.hide.skirt",
                "attire.hide.vest",
                "bandage",
                "mask.bandana",
                "mask.balaclava",
                "pants.shorts",
                "bone.armor.suit",
                "bone.club",
                "botabag",
                "bow.hunting",
                "box.wooden",
                "bucket.water",
                "building.planner",
                "burlap.headwrap",
                "burlap.shirt",
                "burlap.shoes",
                "burlap.trousers",
                "campfire",
                "cupboard.tool",
                "deer.skull.mask",
                "door.hinged.metal",
                "door.hinged.toptier",
                "door.hinged.wood",
                "door.key",
                "fishtrap.small",
                "furnace",
                "hat.beenie",
                "hat.boonie",
                "hat.candle",
                "hat.cap",
                "hat.miner",
                "hat.wolf",
                "hoodie",
                "jackolantern.angry",
                "jackolantern.happy",
                "knife.bone",
                "ladder.wooden.wall",
                "lantern",
                "lock.code",
                "lock.key",
                "lowgradefuel",
                "map",
                "note",
                "pants",
                "pants.short",
                "pistol.eoka",
                "pistol.revolver",
                "rug",
                "shirt.collared",
                "shirt.tanktop",
                "shoes.boots",
                "shotgun.waterpipe",
                "sleepingbag",
                "spear.stone",
                "spear.wooden",
                "stash.small",
                "stone.pickaxe",
                "stonehatchet",
                "tool.camera",
                "tshirt",
                "tshirt.long",
                "tunalight",
                "wall.window.bars.wood",
                "water.catcher.small",
                "wood.armor.jacket",
                "wood.armor.pants",
                "planter.small",
                "wall.frame.shopfront",
                "sign.post.single",
                "sign.wooden.huge",
                "sign.wooden.large",
                "sign.wooden.medium",
                "sign.wooden.small",
                "barricade.wood",
                "explosive.satchel",
                "shutter.wood.a",
                "sign.post.double",
                "xmas.present.large",
                "xmax.present.medium",
                "xmas.present.small",
                "weapon.mod.simplesight",
                "ammo.handmade.shell",
            };
            return dBP;
        }

        static List<object> LibraryItems()
        {
            var x = new List<object>()
            {
                "weapon.mod.small.scope",
                "rifle.ak",
                "autoturret",
                "rifle.bolt",
                "smg.thompson",
                "smg.mp5",
                "gates.external.high.stone",
                "wall.external.high.stone",
                "weapon.mod.holosight",
                "metal.plate.torso",
                "metal.facemask",
                "rocket.launcher",
                "weapon.mod.silencer",
                "explosive.timed",
                "ammo.rocket.basic",
            };
            return x;
        }

        static List<object> BookItems()
        {
            var x = new List<object>()
            {
                "smg.2",
                "weapon.mod.lasersight",
                "weapon.mod.flashlight",
                "weapon.mod.muzzleboost",
                "weapon.mod.muzzlebrake",
                "ammo.pistol",
                "ammo.pistol.hv",
                "ammo.rifle",
                "ammo.rifle.explosive",
                "ammo.rifle.hv",
                "ammo.rifle.incendiary",
                "ammo.rocket.fire",
                "ammo.rocket.hv",
                "ammo.shotgun",
                "ammo.shotgun.slug",
                "coffeecan.helmet",
                "roadsign.jacket",
                "roadsign.kilt",
                "grenade.f1",
                "flamethrower",
                "trap.landmine",
                "sign.hanging.banner.large",
                "sign.pole.banner.large",
                "shotgun.pump",
                "axe.salvaged",
                "icepick.salvaged",
                "pistol.semiauto",
                "rifle.semiauto",
                "jacket.snow",
                "sign.pictureframe.xl",
                "sign.pictureframe.xxl",
                "explosives",
                "pistol.python",
            };
            return x;
        }

        static List<object> PageItems()
        {
            var x = new List<object>()
            {
                "ammo.shotgun",
                "ammo.rifle",
                "fun.guitar",
                "barricade.woodwire",
                "grenade.beancan",
                "bed",
                "bone.armor.suit",
                "bucket.helmet",
                "ceilinglight",
                "crossbow",
                "sign.post.double",
                "hatchet",
                "pickaxe",
                "gates.external.high.wood",
                "wall.external.high",
                "jacket",
                "sign.pictureframe.landscape",
                "lantern",
                "largemedkit",
                "water.catcher.large",
                "sign.wooden.large",
                "barricade.metal",
                "hat.miner",
                "mining.quarry",
                "sign.post.town",
                "sign.pictureframe.portrait",
                "wall.window.bars.toptier",
                "pistol.revolver",
                "riot.helmet",
                "hammer.salvaged",
                "barricade.sandbags",
                "small.oil.refinery",
                "trap.bear",
                "surveycharge",
                "sign.pictureframe.tall",
                "sign.hanging",
                "sign.hanging.ornate",
                "water.barrel",
                "furnace.large",
                "box.wooden.large",
                "shelves",
            };
            return x;
        }

        static List<object> FragItems()
        {
            var x = new List<object>()
            {
                "burlap.gloves",
                "machete",
                "syringe.medical",
                "shutter.metal.embrasure.a",
                "shutter.metal.embrasure.b",
                "ammo.pistol",
                "salvaged.sword",
                "sign.post.single",
                "barricade.stone",
                "spear.stone",
                "bucket.water",
                "wood.armor.helmet",
                "wood.armor.jacket",
                "wood.armor.pants",
                "shutter.wood.a",
                "spikes.floor",
            };
            return x;
        }

        static Dictionary<string, object> CraftingRequirements()
        {
            var cr = new Dictionary<string, object>();
            cr.Add("metal.fragments", 1000);
            cr.Add("wood", 500);
            return cr;
        }

        List<object> defaultBpLlist;
        Dictionary<string, object> craftingNeeds;

        // Fuji
        List<int> defaultBpItemIds;
        Dictionary<ulong, PersistantPlayer> persistanceData = null;
        // Fuji

        List<object> BlockedItemList;

        List<object> FragList;
        List<object> PageList;
        List<object> BookList;
        List<object> LibList;

        Dictionary<string, string> GUIinfo = new Dictionary<string, string>();
        Dictionary<string, ResearchGui> ResearchGUIinfo = new Dictionary<string, ResearchGui>();
        List<string> fullItemList = new List<string>();
        Dictionary<BasePlayer, BaseEntity> researchers = new Dictionary<BasePlayer, BaseEntity>();
        List<Item> containerCheck = new List<Item>();

        #region Classes for above ^

        class ResearchGui
        {
            public string panel;
            public BaseEntity entity;
        }

        #endregion

        public const string failEffect = "assets/prefabs/deployable/research table/effects/research-fail.prefab";
        public const string successEffect = "assets/prefabs/deployable/research table/effects/research-success.prefab";
        public const string storagePrefab = "assets/prefabs/deployable/research table/researchtable_deployed.prefab";

        public const string permissionNamePortable = "blueprintsystem.portable";
        public const string permissionNameCraft = "blueprintsystem.craft";

        const ulong bookID = 901625426;
        const ulong pageID = 901619272;
        const ulong fragsID = 901617490;
        const ulong libraryID = 901628622;

        public float notificationShowTime = 4.0f;
        public float researchStudyTime = 5.0f;
        public float barrelRNG = 0.15f;
        public float crateRNG = 0.10f;

        public int fragsToTakeUPGRADE = 60;
        public int pagesToTakeUPGRADE = 5;
        public int booksToTakeUPGRADE = 4;

        public int costToRevealFRAGS = 50;
        public int costToRevealPAGE = 1;
        public int costToRevealBOOK = 1;
        public int costToRevealLIB = 1;

        public int itemListLength = 0;
        public float researchBenchRadius = 1f;
        public bool portableResearch = false;
        public bool researchLogging = false;
        public bool ResearchOnlyInBuilding = true;
        public bool useBpSpawning = true;
        public bool useBpFragsSystem = true;

        void LoadVariables()
        {
            defaultBpLlist = (List<object>)GetConfig("Blueprint Lists", "Default Blueprints", DefaultBPs());
            BlockedItemList = (List<object>)GetConfig("Blueprint Lists", "Blocked Blueprints", BlockedBPs());
            researchStudyTime = Convert.ToSingle(GetConfig("Settings", "Research Study Time", 5.0f));
            ResearchOnlyInBuilding = Convert.ToBoolean(GetConfig("Settings", "No Research When Building Blocked", true));
            notificationShowTime = Convert.ToSingle(GetConfig("Settings", "Notification Show Length", 4.0f));
            portableResearch = Convert.ToBoolean(GetConfig("Settings", "Portable Research", false));
            researchLogging = Convert.ToBoolean(GetConfig("Settings", "Console BP Research Logging", false));
            craftingNeeds = (Dictionary<string, object>)GetConfig("Settings", "Items Needed to Craft Bench", CraftingRequirements());
            researchBenchRadius = Convert.ToSingle(GetConfig("Settings", "Research Bench Radius", 1f));

            // BP Barrel / Create Spawns
            barrelRNG = Convert.ToSingle(GetConfig("Random Barrel / Crate BPs", "% For Item To Spawn As BP (Barrel)", 0.15f));
            crateRNG = Convert.ToSingle(GetConfig("Random Barrel / Crate BPs", "% For Item To Spawn As BP (Crate)", 0.10f));
            useBpSpawning = Convert.ToBoolean(GetConfig("Random Barrel / Crate BPs", "Random Spawns Enabled", true));

            // BP frags / pages / books / libraries
            useBpSpawning = Convert.ToBoolean(GetConfig("BP Fragment System", "Use BP Frags System", true));
            fragsToTakeUPGRADE = Convert.ToInt32(GetConfig("BP Fragment System", "[ Fragments To Take On Upgrade", 60));
            pagesToTakeUPGRADE = Convert.ToInt32(GetConfig("BP Fragment System", "[ Pages To Take On Upgrade", 5));
            booksToTakeUPGRADE = Convert.ToInt32(GetConfig("BP Fragment System", "[ Books To Take On Upgrade", 4));
            costToRevealFRAGS = Convert.ToInt32(GetConfig("BP Fragment System", "] Cost To Reveal (Fragments)", 50));
            costToRevealPAGE = Convert.ToInt32(GetConfig("BP Fragment System", "] Cost To Reveal (Page)", 1));
            costToRevealBOOK = Convert.ToInt32(GetConfig("BP Fragment System", "] Cost To Reveal (Book)", 1));
            costToRevealLIB = Convert.ToInt32(GetConfig("BP Fragment System", "] Cost To Reveal (Library)", 1));
            FragList = (List<object>)GetConfig("BP Fragment System", "Fragment List", FragItems());
            PageList = (List<object>)GetConfig("BP Fragment System", "Page List", PageItems());
            BookList = (List<object>)GetConfig("BP Fragment Sytem", "Book List", BookItems());
            LibList = (List<object>)GetConfig("BP Fragment System", "Library List", LibraryItems());

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        #endregion

        void Init()
        {
            LoadVariables();

            bpS = this;

            defaultBpItemIds = new List<int>();
            foreach (var bp in defaultBpLlist)
            {
                int itemId = 0;
                try { itemId = ItemManager.FindItemDefinition((string)bp).itemid; } catch { }
                if (itemId != 0)
                    defaultBpItemIds.Add(itemId);
            }

            foreach (var block in BlockedItemList)
            {
                ItemDefinition def = ItemManager.FindItemDefinition((string)block);
                if (def == null || def.Blueprint == null) continue;
                def.Blueprint.isResearchable = false;
            }

            permission.RegisterPermission(permissionNamePortable, this);
            permission.RegisterPermission(permissionNameCraft, this);
        }

        void Loaded()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["Blueprint Not Studied"] = "You do not have this blueprint studied!",
                ["You can now craft"] = "You learnt how to craft a {0}",
                ["You can already craft"] = "You have already researched this blueprint!",
                ["Cannot Research"] = "You are not allowed to research this item",
                ["No Research Enabled"] = "This item isn't researchable",
                ["Outside Building Zone"] = "You are not allowed to research outside of a building authorized zone",
                ["Random Study"] = "You discovered a {0} blueprint!",
                ["BP Invalid Syntax"] = "Invalid Syntax! bp <add/remove> <playername/id> <item shortname>",
                ["BP Player Not Found / Offline"] = "Player was not found or is offline",
                ["BP Already Has This Blueprint"] = "{0} already has this blueprint",
                ["BP Can Now Craft"] = "{0} can now craft {1}",
                ["BP Forgot"] = "{0} forgot how to craft {1}",
                ["BP Doesn't know"] = "{0} doesn't know how to craft this item",
                ["Wiping Database"] = "Wiping blueprint database...",
                ["Wipe Complete"] = "Blueprint database has been wiped!",
                ["No Permission"] = "You cannot use this command!",
                ["No Blueprint Page"] = "You require a blueprint page in order to use this command!",
                ["No Space"] = "You need more space in your inventory to use this command!",
                ["Not Enough Pages"] = "You do not have enough pages to reveal a random blueprint ({0} needed)",
                ["Researchbench Crafted"] = "You have crafted a research bench!",
                ["Not Enough Resources"] = "You do not have enough resources to craft this! To craft this item you need:",
                ["Resource Requirement Line"] = "• {0} [x{1}]",
                ["Cannot Unwrap"] = "You cannot unwrap this item",
                ["Inventory Full"] = "You inventory is currently full and this action was cancelled",
                ["Not Enough Items"] = "You do not have enough items to reveal this!",
            }, this);

        }

        void OnServerInitialized()
        {
            persistanceData = (Dictionary<ulong, PersistantPlayer>)_cachedData.GetValue(ServerMgr.Instance.persistance);
            foreach (var pers in persistanceData.ToList())
            {
                if (pers.Value.unlockedItems == null)
                {
                    PersistantPlayer playerInfo = ServerMgr.Instance.persistance.GetPlayerInfo(pers.Key);
                    playerInfo.unlockedItems = new List<int>(defaultBpItemIds);
                    ServerMgr.Instance.persistance.SetPlayerInfo(pers.Key, playerInfo);
                }
            }
            foreach (var entry in ItemManager.itemList)
                fullItemList.Add(entry.shortname);
            itemListLength = fullItemList.Count;

            foreach(var table in GameObject.FindObjectsOfType<ResearchTable>())
            {
                BaseEntity entity = table.GetEntity();
                BasePlayer player = FindPlayer(entity.OwnerID.ToString());
                if (entity == null) return;
                table.researchDuration = researchStudyTime;
                _user.SetValue(table, player);
                var prox = entity.gameObject.GetComponent<ResearchRadius>();
                var research = entity.gameObject.GetComponent<ResearchHandler>();

                if (research == null)
                    table.gameObject.AddComponent<ResearchHandler>().researcher = FindPlayer(entity.OwnerID.ToString());
                if (prox == null)
                    entity.gameObject.AddComponent<ResearchRadius>();
            }
        }

        void Unload()
        {
            foreach (var key in ResearchGUIinfo)
            {
                BasePlayer player = FindPlayer(key.Key);
                if (player == null || !player.IsConnected) return;
                CuiHelper.DestroyUi(player, key.Value.panel);
            }

            /*
            var objs = UnityEngine.Object.FindObjectsOfType<ResearchHandler>().ToList();
            if (objs.Count > 0)
                foreach (var obj in objs)
                {
                    if (obj.researcher == null) continue;
                    obj.PlayerStoppedLooting(obj.researcher);
                    obj.researcher.EndLooting();
                    GameObject.Destroy(obj);
                }
                */
        }

        void OnPlayerInit(BasePlayer player)
        {
            persistanceData = (Dictionary<ulong, PersistantPlayer>)_cachedData.GetValue(ServerMgr.Instance.persistance);
            PersistantPlayer persistantPlayer = null;
            if (!persistanceData.TryGetValue(player.userID, out persistantPlayer))
            {
                ResetPlayer(player);
            }
            else
            {
                if (persistantPlayer.unlockedItems == null)
                    ResetPlayer(player);
            }
        }

        void ResetPlayer(BasePlayer player)
        {
            PersistantPlayer playerInfo = ServerMgr.Instance.persistance.GetPlayerInfo(player.userID);
            playerInfo.unlockedItems = new List<int>(defaultBpItemIds);
            ServerMgr.Instance.persistance.SetPlayerInfo(player.userID, playerInfo);
        }

        object CanCraft(ItemCrafter crafter, ItemBlueprint bp, int amount)
        {
            if (crafter == null || bp == null || amount == 0 || bp.targetItem == null) return null;
            BasePlayer player = crafter.GetComponent<BasePlayer>();
            if (player == null) return null;

            if (defaultBpLlist.Contains(bp.targetItem.shortname))
                return null;
            if (player.blueprints.IsUnlocked(ItemManager.FindItemDefinition(bp.targetItem.itemid)))
            {
                return null;
            }
            else
            {
                DoUI(player, msg("Blueprint Not Studied", player.UserIDString));
                return false;
            }
        }

        object OnItemAction(Item item, string action)
        {
            if (item == null) return false;
            if (item.info.shortname == "xmas.present.large" || item.info.shortname == "xmas.present.medium" || item.info.shortname == "xmas.present.small")
            {
                BasePlayer player = item.GetOwnerPlayer();
                if (player == null) return false;
                switch (item.skin)
                {
                    case fragsID:
                        if (action == "unwrap")
                        {
                            if (!CheckSpace(player))
                            {
                                DoUI(player, msg("Inventory Full", player.UserIDString));
                                return false;
                            }
                            if (item.amount >= costToRevealFRAGS)
                            {
                                if (item.amount == costToRevealFRAGS)
                                {
                                    item.Remove(0f);
                                    item.RemoveFromContainer();
                                }
                                else
                                    item.UseItem(costToRevealFRAGS);
                                Item newItem = RandomItem(FragList);
                                newItem.MoveToContainer(player.inventory.containerMain);
                            }
                            else
                                DoUI(player, msg("Not Enough Items", player.UserIDString));
                            return false;
                        }
                        else if (action == "upgrade_item")
                        {
                            if (!CheckSpace(player))
                            {
                                DoUI(player, msg("Inventory Full", player.UserIDString));
                                return false;
                            }
                            Item _item = HasItem(player, "xmas.present.small", fragsID, fragsToTakeUPGRADE);
                            if (_item != null)
                            {
                                if (_item.amount == fragsToTakeUPGRADE)
                                {
                                    _item.Remove(0f);
                                    _item.RemoveFromContainer();
                                }
                                else
                                    _item.UseItem(fragsToTakeUPGRADE);
                                Item newitem = ItemManager.CreateByItemID(-2130280721, 1, pageID);
                                newitem.name = "Blueprint Page";
                                if (player.inventory.containerMain.IsFull() && player.inventory.containerBelt.IsFull())
                                {
                                    DoUI(player, msg("Inventory Full", player.UserIDString));
                                    return false;
                                }
                                else if (player.inventory.containerMain.IsFull())
                                    newitem.MoveToContainer(player.inventory.containerBelt);
                                else
                                    newitem.MoveToContainer(player.inventory.containerMain);
                            }
                            return false;
                        }
                        break;
                    case pageID:
                        if (action == "unwrap")
                        {
                            if (!CheckSpace(player))
                            {
                                DoUI(player, msg("Inventory Full", player.UserIDString));
                                return false;
                            }
                            if (item.amount >= costToRevealPAGE)
                            {
                                if (item.amount == costToRevealPAGE)
                                {
                                    item.Remove(0f);
                                    item.RemoveFromContainer();
                                }
                                else
                                    item.UseItem(costToRevealPAGE);
                                Item newItem = RandomItem(FragList);
                                newItem.MoveToContainer(player.inventory.containerMain);
                            }
                            else
                                DoUI(player, msg("Not Enough Items", player.UserIDString));
                            return false;
                        }
                        else if (action == "upgrade_item")
                        {
                            if (!CheckSpace(player))
                            {
                                DoUI(player, msg("Inventory Full", player.UserIDString));
                                return false;
                            }
                            Item _item = HasItem(player, "xmas.present.medium", pageID, pagesToTakeUPGRADE);
                            if (_item != null)
                            {
                                if (_item.amount == pagesToTakeUPGRADE)
                                {
                                    _item.Remove(0f);
                                    _item.RemoveFromContainer();
                                }
                                else
                                    _item.UseItem(pagesToTakeUPGRADE);
                                Item newitem = ItemManager.CreateByItemID(-2130280721, 1, bookID);
                                newitem.name = "Blueprint Book";
                                if (player.inventory.containerMain.IsFull() && player.inventory.containerBelt.IsFull())
                                    DoUI(player, msg("Inventory Full", player.UserIDString));
                                else if (player.inventory.containerMain.IsFull())
                                    newitem.MoveToContainer(player.inventory.containerBelt);
                                else
                                    newitem.MoveToContainer(player.inventory.containerMain);
                            }
                            return false;
                        }
                        break;
                    case bookID:
                        if (action == "unwrap")
                        {
                            if (!CheckSpace(player))
                            {
                                DoUI(player, msg("Inventory Full", player.UserIDString));
                                return false;
                            }
                            if (item.amount >= costToRevealBOOK)
                            {
                                if (item.amount == costToRevealBOOK)
                                {
                                    item.Remove(0f);
                                    item.RemoveFromContainer();
                                }
                                else
                                    item.UseItem(costToRevealBOOK);
                                Item newItem = RandomItem(BookList);
                                newItem.MoveToContainer(player.inventory.containerMain);
                            }
                            else
                                DoUI(player, msg("Not Enough Items", player.UserIDString));
                            return false;
                        }
                        else if (action == "upgrade_item")
                        {
                            if (!CheckSpace(player))
                            {
                                DoUI(player, msg("Inventory Full", player.UserIDString));
                                return false;
                            }
                            Item _item = HasItem(player, "xmas.present.medium", bookID, booksToTakeUPGRADE);
                            if (_item != null)
                            {
                                if (_item.amount == booksToTakeUPGRADE)
                                {
                                    _item.Remove(0f);
                                    _item.RemoveFromContainer();
                                }
                                else
                                    _item.UseItem(booksToTakeUPGRADE);
                                Item newitem = ItemManager.CreateByItemID(-1732316031, 1, libraryID);
                                newitem.name = "Blueprint Library";
                                if (player.inventory.containerMain.IsFull() && player.inventory.containerBelt.IsFull())
                                {
                                    DoUI(player, msg("Inventory Full", player.UserIDString));
                                    return false;
                                }
                                else if (player.inventory.containerMain.IsFull())
                                    newitem.MoveToContainer(player.inventory.containerBelt);
                                else
                                    newitem.MoveToContainer(player.inventory.containerMain);
                            }
                            return false;
                        }
                        break;
                    case libraryID:
                        if (action == "unwrap")
                        {
                            if (!CheckSpace(player))
                            {
                                DoUI(player, msg("Inventory Full", player.UserIDString));
                                return false;
                            }
                            if (item.amount >= costToRevealLIB)
                            {
                                if (item.amount == costToRevealLIB)
                                {
                                    item.Remove(0f);
                                    item.RemoveFromContainer();
                                }
                                else
                                    item.UseItem(costToRevealLIB);
                                Item newItem = RandomItem(LibList);
                                newItem.MoveToContainer(player.inventory.containerMain);
                            }
                            else
                                DoUI(player, msg("Not Enough Items", player.UserIDString));
                            return false;
                        }
                        break;
                }
            }
            if (action == "craft")
            {
                BasePlayer player = item.GetOwnerPlayer();
                if (player == null) return false;
                ItemDefinition itemdef = ItemManager.FindItemDefinition(item.blueprintTarget);
                if (itemdef == null) return false;
                if (!player.blueprints.IsUnlocked(itemdef))
                {
                    if (item.amount == 1)
                        item.Remove(0f);
                    else
                        item.UseItem(1);
                    player.blueprints.Unlock(itemdef);
                    DoUI(player, string.Format(msg("You can now craft", player.UserIDString), itemdef.displayName.english));
                    if (researchLogging)
                        Puts($"{player.displayName} || {player.UserIDString} just studied a {itemdef.shortname}");
                    return false;
                }
                else
                {
                    DoUI(player, msg("You can already craft"));
                    return false;
                }
            }
            else if (action == "craft_all")
            {
                BasePlayer player = item.GetOwnerPlayer();
                if (player == null) return false;
                ItemDefinition itemdef = ItemManager.FindItemDefinition(item.blueprintTarget);
                if (itemdef == null) return false;
                if (!player.blueprints.IsUnlocked(itemdef))
                {
                    if (item.amount == 1)
                        item.Remove(0f);
                    else
                        item.UseItem(1);

                    player.blueprints.Unlock(itemdef);
                    DoUI(player, string.Format(msg("You can now craft", player.UserIDString), itemdef.displayName.english)); ;
                    if (researchLogging)
                        Puts($"{player.displayName} || {player.UserIDString} just studied a {itemdef.shortname}");
                    return false;
                }
                else
                {
                    DoUI(player, msg("You can already craft"));
                    return false;
                }
            }
            else
                return null;
        }

        Item HasItem(BasePlayer player, string itemname, ulong skin, int amount)
        {
            foreach (var entry in player.inventory.containerMain.itemList)
                if (entry.info.shortname == itemname)
                    if (entry.amount >= amount)
                        if (entry.skin == skin)
                            return entry;
            return null;
        }

        Item RandomItem(List<object> list)
        {
            int index = Convert.ToInt32(UnityEngine.Random.Range(0f, Convert.ToSingle(list.Count - 1)));
            Item newitem = ItemManager.Create(ItemManager.FindItemDefinition("blueprintbase"), 1, 0uL);
            newitem.blueprintAmount = 1;
            newitem.blueprintTarget = ItemManager.CreateByName(list[index].ToString()).info.itemid;
            return newitem;
        }

        bool CheckSpace(BasePlayer player)
        {
            if (player.inventory.containerMain.IsFull())
                return false;
            return true;
        }

        object CanStackItem(Item item, Item targetItem)
        {
            if (item.info.shortname != "xmas.present.medium") return null;
            if (item.skin == targetItem.skin)
                return null;
            else
                return false;
        }

        object OnItemResearch(Item researchItem, BasePlayer player)
        {
            if (player == null) return false;
            if (researchItem.info.Blueprint == null || BlockedItemList.Contains(researchItem.info.shortname))
            {
                DoUI(player, msg("No Research Enabled", player.UserIDString));
                return false;
            }
            ResearchHandler handler = player.inventory?.loot?.entitySource?.GetComponent<ResearchHandler>();
            if (!handler) Puts("Handler was null");
            handler.DoResearch(researchItem, player);
            return false;
        }

        void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            if (container.GetOwnerPlayer() != null)
            {
                containerCheck.Add(item);
                timer.Once(0.1f, () => containerCheck.Remove(item));
            }
        }

        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            // random BPs from barrels / crates
            if (useBpSpawning)
                if (item.info.shortname != "blueprintbase")
                {
                    if (!BlockedItemList.Contains(item.info.shortname) && !defaultBpLlist.Contains(item.info.shortname))
                    {
                        BaseEntity entity = container?.entityOwner;
                        if (entity)
                        {
                            if (entity.ShortPrefabName == "loot-barrel-1" || entity.ShortPrefabName == "loot-barrel-2" || entity.ShortPrefabName == "loot_barrel_1" || entity.ShortPrefabName == "loot_barrel_2")
                            {
                                if (UnityEngine.Random.Range(0f, 1f) < barrelRNG)
                                {
                                    Item newitem = ItemManager.Create(ItemManager.FindItemDefinition("blueprintbase"), 1, 0uL);
                                    newitem.blueprintAmount = 1;
                                    newitem.blueprintTarget = item.info.itemid;
                                    item.Remove(0f);
                                    item.RemoveFromContainer();
                                    newitem.MoveToContainer(container, GetSlot(container));
                                }
                            }
                            else if (entity.ShortPrefabName == "crate_mine" || entity.ShortPrefabName == "crate_normal" || entity.ShortPrefabName == "crate_normal_2" || entity.ShortPrefabName == "crate_normal_2_food" || entity.ShortPrefabName == "crate_normal_2_medical" || entity.ShortPrefabName == "crate_tools")
                            {
                                if (!containerCheck.Contains(item))
                                    if (UnityEngine.Random.Range(0f, 1f) < crateRNG)
                                    {
                                        Item newitem = ItemManager.Create(ItemManager.FindItemDefinition("blueprintbase"), 1, 0uL);
                                        newitem.blueprintAmount = 1;
                                        newitem.blueprintTarget = item.info.itemid;
                                        item.Remove(0f);
                                        item.RemoveFromContainer();
                                        newitem.MoveToContainer(container, GetSlot(container));
                                    }
                            }
                        }
                    }
                }
            if (useBpFragsSystem)
            {
                if (container.entityOwner?.ShortPrefabName == "loot-barrel-1" || container.entityOwner?.ShortPrefabName == "loot-barrel-2" || container.entityOwner?.ShortPrefabName == "loot_barrel_1" || container.entityOwner?.ShortPrefabName == "loot_barrel_2" || container.entityOwner?.ShortPrefabName == "crate_mine" || container.entityOwner?.ShortPrefabName == "crate_normal" || container.entityOwner?.ShortPrefabName == "crate_normal_2" || container.entityOwner?.ShortPrefabName == "crate_normal_2_food" || container.entityOwner?.ShortPrefabName == "crate_normal_2_medical" || container.entityOwner?.ShortPrefabName == "crate_tools")
                {
                    if (item.info.shortname == "xmas.present.small")
                        if (item.skin != fragsID)
                        {
                            item.name = "Blueprint Fragments";
                            item.skin = fragsID;
                        }
                        else if (item.info.shortname == "xmas.present.medium")
                        {
                            if (item.skin != pageID || item.skin != bookID)
                            {
                                float x = UnityEngine.Random.Range(0f, 1f);
                                if (x < 0.25)
                                {
                                    item.name = "Blueprint Page";
                                    item.skin = pageID;
                                }
                                else
                                {
                                    item.name = "Blueprint Book";
                                    item.skin = bookID;
                                }
                            }
                        }
                        else if (item.info.shortname == "xmas.present.large")
                            if (item.skin != libraryID)
                            {
                                item.name = "Blueprint Library";
                                item.skin = libraryID;
                            }
                }
            }
            if (item.info.shortname == "xmas.present.small" || item.info.shortname == "xmas.present.medium" || item.info.shortname == "xmas.present.large")
            {
                if (item.skin == 0)
                {
                    item.Remove(0f);
                    item.RemoveFromContainer();
                    return;
                }
            }
            if (container.entityOwner == null || !(container.entityOwner is ResearchTable)) return;
            if (container.entityOwner.GetComponent<ResearchHandler>() == null || container.entityOwner.GetComponent<ResearchHandler>().researcher == null) return;
            if (item.info.shortname == "blueprintbase" || item.info.shortname == "xmas.present.small" || item.info.shortname == "xmas.present.medium" || item.info.shortname == "xmas.present.large") return;
            if (item.info.Blueprint == null || BlockedItemList.Contains(item.info.shortname))
            {
                DoUI(container.entityOwner.GetComponent<ResearchHandler>().researcher, msg("No Research Enabled", container.entityOwner.GetComponent<ResearchHandler>().researcher.UserIDString));
                return;
            }
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!input.IsDown(BUTTON.USE)) return;
            if (!researchers.ContainsKey(player)) return;

            BaseEntity entity = researchers[player];
            if (!entity || entity == null || !entity.IsValid())
            {
                researchers.Remove(player);
                return;
            }
            StorageContainer container = entity.GetComponent<StorageContainer>();
            ResearchTable table = researchers[player] as ResearchTable;
            table.researchDuration = researchStudyTime;
            _user.SetValue(table, player);
            var x = table.gameObject.GetComponent<ResearchHandler>();
            if (x)
                x.researcher = player;

            timer.Once(0.1f, () =>
            {
                player.inventory.loot.StartLootingEntity(container, false);
                player.inventory.loot.entitySource = container;
                player.inventory.loot.AddContainer(container.inventory);
                player.inventory.loot.SendImmediate();
                player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", container.panelName);
                container.SetFlag(BaseEntity.Flags.Open, true, false);
                player.SendNetworkUpdate();
                container.SendNetworkUpdate();
            });
        }

        void OnEntitySpawned(BaseNetworkable entityn)
        {
            BaseEntity entity = entityn as BaseEntity;
            if (entity.ShortPrefabName != "researchtable_deployed") return;

            entity.gameObject.AddComponent<ResearchHandler>();
            entity.gameObject.AddComponent<ResearchRadius>();
        }

        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity is ResearchTable)
            {
                var prox = entity.GetComponent<ResearchRadius>();
                var research = entity.GetComponent<ResearchHandler>();

                foreach (var entry in ResearchGUIinfo)
                    if (entry.Value.entity == entity as BaseEntity)
                        DestroyBenchUI(FindPlayer(entry.Key));

                NextTick(() =>
                {
                    if (prox)
                        UnityEngine.Object.DestroyImmediate(prox);
                    if (research)
                        UnityEngine.Object.DestroyImmediate(research);
                });
            }
        }

        #region Handler

        public class ResearchHandler : MonoBehaviour
        {
            public BasePlayer researcher;
            public BaseEntity entity;
            public StorageContainer container;
            public ItemContainer inventory;
            public ResearchTable table;

            void Awake()
            {
                entity = GetComponent<BaseEntity>();
                container = GetComponent<StorageContainer>();
                inventory = container.inventory;
                table = GetComponent<ResearchTable>();
            }

            public void DoResearch(Item researchItem, BasePlayer player)
            {
                researcher = player;
                if (researchItem.amount > 1)
                    return;
                /*
                if (!table.IsItemResearchable(researchItem))
                {
                    bpS.DoUI(researcher, string.Format(bpS.msg("No Research Enabled", researcher.UserIDString), researchItem.info.displayName.translated));
                    return;
                }
                */
                if (bpS.defaultBpLlist.Contains(researchItem.info.shortname))
                {
                    bpS.DoUI(researcher, bpS.msg("No Research Enabled"));
                    return;
                }
                if (bpS.BlockedItemList.Contains(researchItem.info.shortname))
                {
                    bpS.DoUI(researcher, bpS.msg("Cannot Research"));
                    return;
                }
                researchItem.CollectedForCrafting(player);
                table.researchFinishedTime = Time.realtimeSinceStartup + table.researchDuration;
                InvokeHandler.Invoke(entity.GetComponent<MonoBehaviour>(), new Action(this.ResearchAttemptFinished), table.researchDuration);
                table.inventory.SetLocked(true);
                entity.SetFlag(BaseEntity.Flags.On, true, false);
                entity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                player.inventory.loot.SendImmediate();
                if (table.researchStartEffect.isValid)
                    Effect.server.Run(table.researchStartEffect.resourcePath, player, 0u, Vector3.zero, Vector3.zero, null, false);
            }

            void ResearchAttemptFinished()
            {
                Item researchItem = bpS.GetResearchItem(inventory);
                if (researchItem == null) return;
                Item researchPaperItem = bpS.GetResearchPaper(inventory);
                if (researchItem != null)
                {
                    float num = UnityEngine.Random.Range(0f, 1f);
                    int num2 = bpS.GetReserachAmount(researchItem);
                    if (num <= this.GetSuccessChance(researchItem, table.inventory.GetSlot(1)))
                    {
                        //researchItem.Remove(0f);
                        researchItem.RemoveFromContainer();
                        if (table.researchSuccessEffect.isValid)
                            Effect.server.Run(table.researchSuccessEffect.resourcePath, researcher, 0u, Vector3.zero, Vector3.zero, null, false);
                        //bpS.DoUI(researcher, string.Format(bpS.msg("Already Researched", researcher.UserIDString), researchItem.info.displayName.translated));
                        //int blueprintAmount = (researchItem.info.Blueprint.blueprintStackSize != -1) ? researchItem.info.Blueprint.blueprintStackSize : this.GetBlueprintStacksize(researchItem);
                        Item item = ItemManager.Create(ItemManager.FindItemDefinition("blueprintbase"), 1, 0uL);
                        item.blueprintAmount = 1;
                        item.blueprintTarget = researchItem.info.itemid;
                        item.MoveToContainer(table.inventory, 0, true);
                        /*
                        if (!item.MoveToContainer(table.inventory, 0, true))
                            researcher.GiveItem(item);
                            */
                    }
                    else
                    {
                        if (researchItem.hasCondition)
                            researchItem.LoseCondition(researchItem.condition);
                        else
                            researchItem.Remove(0f);
                        if (table.researchFailEffect.isValid)
                            Effect.server.Run(table.researchFailEffect.resourcePath, researcher, 0u, Vector3.zero, Vector3.zero, null, false);
                    }
                    if (researchPaperItem != null)
                    {
                        if (researchPaperItem.amount <= num2)
                        {
                            researchPaperItem.SetParent(null);
                            researchPaperItem.RemoveFromContainer();
                            researchPaperItem.Remove(0f);
                        }
                        else
                            researchPaperItem.UseItem(num2);
                    }
                }
                entity.SendNetworkUpdateImmediate(false);
                if (researcher != null)
                    researcher.inventory.loot.SendImmediate();
                this.EndResearch();
            }

            void EndResearch()
            {
                table.inventory.SetLocked(false);
                entity.SetFlag(BaseEntity.Flags.On, false, false);
                table.researchFinishedTime = 0f;
                entity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                if (researcher != null)
                    researcher.inventory.loot.SendImmediate();
            }

            Single GetSuccessChance(Item item, Item bpFrags)
            {
                float num = 0f;
                if (bpFrags != null && bpFrags.info.shortname == "xmas.present.small" && bpFrags.amount > 0)
                {
                    int num2 = bpS.GetReserachAmount(item);
                    float num3 = Mathf.Clamp01((float)bpFrags.amount / (float)num2);
                    num += 0.7f * num3;
                }
                if (!item.hasCondition)
                    return num + 0.3f;

                return num + 0.3f * (item.condition / item.info.condition.max);
            }

            Int32 GetBlueprintStacksize(Item sourceItem)
            {
                int result = this.RarityMultiplier(sourceItem.info.rarity);
                if (sourceItem.info.category == ItemCategory.Ammunition)
                    result = Mathf.FloorToInt((float)sourceItem.info.stackable / (float)sourceItem.info.Blueprint.amountToCreate) * 2;
                return result;
            }

            Int32 RarityMultiplier(Rust.Rarity rarity)
            {
                if (rarity == Rust.Rarity.None)
                    return 15;
                if (rarity == Rust.Rarity.Common)
                    return 10;
                if (rarity == Rust.Rarity.Uncommon)
                    return 5;
                if (rarity == Rust.Rarity.Rare)
                    return 2;
                return 1;
            }

            public void PlayerStoppedLooting(BasePlayer player)
            {
                /*
                Item input = table.GetResearchItem();
                if (input != null)
                    researcher.GiveItem(input);
                Item paper = table.GetResearchPaperItem();
                if (paper != null)
                    researcher.GiveItem(paper);
                if (!GetComponent<BaseEntity>().IsDestroyed)
                    GetComponent<BaseEntity>().Kill(BaseNetworkable.DestroyMode.None);
                    */
            }

            void OnDestroy()
            {
                researcher.EndLooting();
                foreach (var entry in bpS.researchers)
                    if (entry.Value == entity)
                        bpS.DestroyBenchUI(researcher);
                Destroy(this);
            }
        }

        class ResearchRadius : MonoBehaviour
        {
            BaseEntity entity;
            public bool isEnabled;

            private void Awake()
            {
                entity = GetComponent<BaseEntity>();
                var collider = entity.gameObject.AddComponent<SphereCollider>();
                collider.center = new Vector3(collider.center.z, collider.center.y + 1f, collider.center.z);
                collider.gameObject.layer = (int)Layer.Reserved1;
                collider.radius = bpS.researchBenchRadius;
                collider.isTrigger = true;
                isEnabled = true;
            }

            private void OnTriggerEnter(Collider col)
            {
                var player = col.GetComponent<BasePlayer>();
                if (player == null) return;
                if (!player.IsValid()) return;

                if (bpS.researchers.ContainsKey(player))
                    bpS.researchers.Remove(player);
                //entity.GetComponent<ResearchHandler>().researcher = player;
                bpS.DoBenchUI(player, entity);
                bpS.researchers.Add(player, entity);

                StorageContainer container = entity.GetComponent<StorageContainer>();
                ResearchTable table = bpS.researchers[player] as ResearchTable;
                table.researchDuration = bpS.researchStudyTime;
                bpS._user.SetValue(table, player);
                var x = table.gameObject.GetComponent<ResearchHandler>();
                if (x)
                    x.researcher = player;

                bpS.timer.Once(0.1f, () =>
                {
                    container.SetFlag(BaseEntity.Flags.Open, true, false);
                    player.inventory.loot.StartLootingEntity(container, false);
                    player.inventory.loot.entitySource = container;
                    player.inventory.loot.AddContainer(container.inventory);
                    player.inventory.loot.SendImmediate();
                    player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", "researchtable");
                    container.SendNetworkUpdate();
                });
            }

            private void OnTriggerExit(Collider col)
            {
                var player = col.GetComponent<BasePlayer>();
                if (player == null) return;
                if (!player.IsValid()) return;

                if (bpS.researchers.ContainsKey(player))
                    bpS.researchers.Remove(player);
                bpS.DestroyBenchUI(player);
            }
        }

        #endregion

        #region Commands

        [ConsoleCommand("bpwipe")]
        void bpwipeConsoleCMD(ConsoleSystem.Arg args)
        {
            if (args.Connection != null) return;
            args.ReplyWith(msg("Wiping Database"));

            persistanceData = (Dictionary<ulong, PersistantPlayer>)_cachedData.GetValue(ServerMgr.Instance.persistance);

            foreach (var persistance in persistanceData.ToList())
            {
                PersistantPlayer playerInfo = ServerMgr.Instance.persistance.GetPlayerInfo(persistance.Key);
                playerInfo.unlockedItems = new List<int>(defaultBpItemIds);
                ServerMgr.Instance.persistance.SetPlayerInfo(persistance.Key, playerInfo);
                var target = BasePlayer.FindByID(persistance.Key);
            }
            args.ReplyWith(msg("Wipe Complete"));
        }

        [ChatCommand("researchbench")]
        void researchbenchCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionNameCraft))
            {
                player.ChatMessage(msg("No Permission", player.UserIDString));
                return;
            }
            if (CheckForItems(player))
            {
                player.ChatMessage(msg("Researchbench Crafted", player.UserIDString));
                Item newitem = ItemManager.CreateByItemID(1987447227, 1, 0);
                newitem.MoveToContainer(player.inventory.containerMain);
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(msg("Not Enough Resources", player.UserIDString));
                sb.AppendLine("");
                Dictionary<string, int> dic = ConvertDic(craftingNeeds);
                foreach (var entry in dic)
                {
                    foreach (var item in ItemManager.itemList)
                        if (item.shortname == entry.Key)
                            sb.AppendLine(string.Format(msg("Resource Requirement Line", player.UserIDString), item.displayName.english, entry.Value.ToString()));
                }
                player.ChatMessage(sb.ToString().TrimEnd());
            }
        }

        bool CheckForItems(BasePlayer player)
        {
            List<Item> x = new List<Item>();
            Dictionary<string, bool> xz = new Dictionary<string, bool>();
            Dictionary<string, int> dic = ConvertDic(craftingNeeds);

            foreach (var entry in dic)
                xz.Add(entry.Key, false);
            foreach (var item in player.inventory.containerMain.itemList)
            {
                if (xz.ContainsKey(item.info.shortname))
                {
                    if (xz[item.info.shortname] == true) continue;
                    if (item.amount >= dic[item.info.shortname])
                    {
                        xz[item.info.shortname] = true;
                        item.UseItem(dic[item.info.shortname]);
                    }
                }
            }

            foreach (var item in player.inventory.containerBelt.itemList)
            {
                if (xz.ContainsKey(item.info.shortname))
                {
                    if (xz[item.info.shortname] == true) continue;
                    if (item.amount >= dic[item.info.shortname])
                    {
                        xz[item.info.shortname] = true;
                        item.UseItem(dic[item.info.shortname]);
                    }
                }
            }

            foreach (var entry in xz)
            {
                if (entry.Value == false)
                    return false;
            }

            return true;
        }

        public static Dictionary<string, int> ConvertDic(object obj)
        {
            if (typeof(IDictionary).IsAssignableFrom(obj.GetType()))
            {
                IDictionary idict = (IDictionary)obj;

                Dictionary<string, int> newDict = new Dictionary<string, int>();
                foreach (object key in idict.Keys)
                {
                    newDict.Add(key.ToString(), Convert.ToInt32(idict[key]));
                }
                return newDict;
            }
            return null;
        }

        int GetSlot(ItemContainer container)
        {
            for (int i = 0; i < container.capacity; i++)
                if (container.GetSlot(i) == null)
                    return i;
            return -1;
        }

        [ConsoleCommand("bp")]
        void bpConsoleCMD(ConsoleSystem.Arg args)
        {
            if (args.Connection != null) return;
            if (args.Args == null)
            {
                args.ReplyWith(msg("BP Invalid Syntax"));
                return;
            }

            switch (args.Args.Length)
            {
                case 0:
                    args.ReplyWith(msg("BP Invalid Syntax"));
                    break;

                case 1:
                    args.ReplyWith(msg("BP Invalid Syntax"));
                    break;

                case 2:
                    args.ReplyWith(msg("BP Invalid Syntax"));
                    break;

                case 3:
                    BasePlayer targetplayer = FindPlayer(args.Args[1]);
                    if (targetplayer == null || !targetplayer.IsConnected)
                    {
                        args.ReplyWith(msg("BP Player Not Found / Offline"));
                        return;
                    }

                    if (args.Args[0] == "add")
                    {
                        var itemDef = ItemManager.FindItemDefinition(args.Args[2]);
                        if (itemDef != null)
                        {
                            if (targetplayer.blueprints.IsUnlocked(itemDef))
                            {
                                args.ReplyWith(string.Format(msg("BP Already Has This Blueprint"), targetplayer.displayName));
                                return;
                            }
                            else
                            {
                                targetplayer.blueprints.Unlock(itemDef);
                                args.ReplyWith(string.Format(msg("BP Can Now Craft"), targetplayer.displayName, args.Args[2]));
                                return;
                            }
                        }
                    }
                    else if (args.Args[0] == "remove")
                    {
                        var itemDef = ItemManager.FindItemDefinition(args.Args[2]);
                        if (itemDef != null)
                        {
                            if (targetplayer.blueprints.IsUnlocked(itemDef))
                            {
                                PersistantPlayer playerInfo = ServerMgr.Instance.persistance.GetPlayerInfo(targetplayer.userID);
                                playerInfo.unlockedItems.Remove(itemDef.itemid);
                                ServerMgr.Instance.persistance.SetPlayerInfo(targetplayer.userID, playerInfo);
                                args.ReplyWith(string.Format(msg("BP Forgot"), targetplayer.displayName, args.Args[2]));
                                return;
                            }
                            else
                            {
                                args.ReplyWith(string.Format(msg("BP Doesn't Know"), targetplayer.displayName));
                                return;
                            }
                        }
                    }
                    break;
            }
            return;
        }

        [ChatCommand("research")]
        void researchCMD(BasePlayer player, string command, string[] args)
        {
            if (!portableResearch) return;
            if (!permission.UserHasPermission(player.UserIDString, permissionNamePortable))
            {
                player.ChatMessage(msg("No Permission", player.UserIDString));
                return;
            }
            if (ResearchOnlyInBuilding)
                if (!player.CanBuild())
                {
                    player.ChatMessage(msg("Outside Building Zone", player.UserIDString));
                    return;
                }

            BaseEntity entity = GameManager.server.CreateEntity(storagePrefab, new Vector3(player.transform.position.x, player.transform.position.y - 200.0f, player.transform.position.z));
            entity.Spawn();
            StorageContainer container = entity as StorageContainer;
            ResearchTable table = entity as ResearchTable;
            table.researchDuration = researchStudyTime;
            _user.SetValue(table, player);
            table.gameObject.AddComponent<ResearchHandler>().researcher = player;

            timer.Once(0.1f, () =>
            {
                container.SetFlag(BaseEntity.Flags.Open, true, false);
                player.inventory.loot.StartLootingEntity(container, false);
                player.inventory.loot.entitySource = container;
                player.inventory.loot.AddContainer(container.inventory);
                player.inventory.loot.SendImmediate();
                player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", container.panelName);
                container.SendNetworkUpdate();
            });
        }

        #endregion

        void DoUI(BasePlayer player, string message)
        {
            if (GUIinfo.ContainsKey(player.UserIDString))
            {
                CuiHelper.DestroyUi(player, GUIinfo[player.UserIDString]);
                GUIinfo.Remove(player.UserIDString);
            }

            var elements = new CuiElementContainer();
            var panel = elements.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.4", FadeIn = 1.0f },
                RectTransform = { AnchorMin = "0.654 0.025", AnchorMax = "0.82 0.14" },
            }, "Hud.Menu");
            elements.Add(new CuiLabel
            {
                Text = { Text = message, FontSize = 14, Align = TextAnchor.MiddleCenter, FadeIn = 2.0f },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, panel);

            CuiHelper.AddUi(player, elements);
            GUIinfo.Add(player.UserIDString, panel);

            timer.Once(notificationShowTime, () =>
            {
                CuiHelper.DestroyUi(player, panel);
                GUIinfo.Remove(player.UserIDString);
            });
        }

        void DoBenchUI(BasePlayer player, BaseEntity entity)
        {
            if (ResearchGUIinfo.ContainsKey(player.UserIDString))
            {
                CuiHelper.DestroyUi(player, ResearchGUIinfo[player.UserIDString].panel);
                ResearchGUIinfo.Remove(player.UserIDString);
            }

            var elements = new CuiElementContainer();
            var rpanel = elements.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.7", FadeIn = 1.0f },
                RectTransform = { AnchorMin = "0.4 0.49", AnchorMax = "0.59 0.55" },
            }, "Hud");
            elements.Add(new CuiLabel
            {
                Text = { Text = "Press \'USE\' to use the research bench", Color = "0.8 0.8 0.8 1", FontSize = 14, Align = TextAnchor.MiddleCenter, FadeIn = 1f },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, rpanel);

            CuiHelper.AddUi(player, elements);
            ResearchGUIinfo.Add(player.UserIDString, new ResearchGui() { panel = rpanel, entity = entity });
        }

        void DestroyBenchUI(BasePlayer player)
        {
            if (ResearchGUIinfo.ContainsKey(player.UserIDString))
            {
                CuiHelper.DestroyUi(player, ResearchGUIinfo[player.UserIDString].panel);
                ResearchGUIinfo.Remove(player.UserIDString);
            }
        }

        Item GetResearchItem(ItemContainer container)
        {
            Item item = container.GetSlot(0);
            if (BlockedItemList.Contains(item.info.shortname) || defaultBpLlist.Contains(item.info.shortname) || item.info.shortname == "xmas.present.small")
                return null;
            return item;
        }

        Item GetResearchPaper(ItemContainer container)
        {
            Item item = container.GetSlot(1);
            if (item.info.shortname != "xmas.present.small" || item.info.shortname != "researchpaper")
                return null;
            return item;
        }

        int GetReserachAmount(Item item)
        {
            if (FragList.Contains(item.info.shortname))
                return 100;
            else if (PageList.Contains(item.info.shortname))
                return 250;
            else if (BookList.Contains(item.info.shortname))
                return 500;
            else if (LibList.Contains(item.info.shortname))
                return 1000;
            else
                return 200;
        }

        private static BasePlayer FindPlayer(string nameOrId)
        {
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString == nameOrId)
                    return activePlayer;
                if (activePlayer.displayName.Contains(nameOrId, CompareOptions.OrdinalIgnoreCase))
                    return activePlayer;
                if (activePlayer.net?.connection != null && activePlayer.net.connection.ipaddress == nameOrId)
                    return activePlayer;
            }
            return null;
        }

        #region API

        public List<object> GetDefaultBPList()
        {
            return defaultBpLlist;
        }

        public List<object> GetBlockedBPList()
        {
            return BlockedItemList;
        }

        #endregion

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

        string msg(string key, string id = null) => lang.GetMessage(key, this, id);
    }
}