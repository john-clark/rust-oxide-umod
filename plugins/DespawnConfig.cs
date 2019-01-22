using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using System.Globalization;
using Oxide.Core.Configuration;

namespace Oxide.Plugins
{
    [Info("DespawnConfig", "Jake_Rich", "1.2.1", ResourceId = 2467)]
    [Description("Configurable Despawn Time")]
    class DespawnConfig : RustPlugin
    {
        public static DespawnConfig _plugin;

        public const float despawnTimerDelay = 1f;

        public JSONFile<Settings> settings { get; set; }

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
            public bool SaveOnUnload = false;
            public bool Compressed = false;

            public JSONFile(string name, ConfigLocation location = ConfigLocation.Data, string path = null, string extension = ".json", bool saveOnUnload = false)
            {
                SaveOnUnload = saveOnUnload;
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
                Init();
            }

            public virtual void Init()
            {
                _plugin.OnRemovedFromManager.Add(new Action<Plugin, PluginManager>(Unload));
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
            }

            public virtual void Save()
            {
                _file.WriteObject(Instance);
            }

            public virtual void Reload()
            {
                Load();
            }

            private void Unload(Plugin sender, PluginManager manager)
            {
                if (SaveOnUnload)
                {
                    Save();
                }
            }
        }

        #endregion

        #region Default Values

        public static Dictionary<string, string> DefaultDespawnTimes = new Dictionary<string, string>()
        {
            {"rifle.ak","4h"},
            {"ammo.handmade.shell","10m"},
            {"ammo.pistol","20m"},
            {"ammo.pistol.fire","20m"},
            {"ammo.pistol.hv","20m"},
            {"ammo.rifle","20m"},
            {"ammo.rifle.explosive","20m"},
            {"ammo.rifle.incendiary","20m"},
            {"ammo.rifle.hv","20m"},
            {"ammo.rocket.basic","1d"},
            {"ammo.rocket.fire","1d"},
            {"ammo.rocket.hv","1d"},
            {"ammo.rocket.smoke","10m"},
            {"ammo.shotgun","20m"},
            {"ammo.shotgun.slug","20m"},
            {"antiradpills","5m"},
            {"apple","5m"},
            {"apple.spoiled","5m"},
            {"arrow.hv","5m"},
            {"arrow.wooden","5m"},
            {"autoturret","4h"},
            {"axe.salvaged","20m"},
            {"bandage","5m"},
            {"barricade.concrete","10m"},
            {"barricade.metal","10m"},
            {"barricade.sandbags","10m"},
            {"barricade.stone","10m"},
            {"barricade.wood","10m"},
            {"barricade.woodwire","10m"},
            {"battery.small","10m"},
            {"trap.bear","10m"},
            {"bed","10m"},
            {"tool.binoculars","10m"},
            {"black.raspberries","5m"},
            {"bleach","5m"},
            {"blood","5m"},
            {"blueberries","5m"},
            {"blueprintbase","15m"},
            {"rifle.bolt","4h"},
            {"bone.club","5m"},
            {"bone.fragments","5m"},
            {"botabag","5m"},
            {"bow.hunting","10m"},
            {"box.wooden.large","10m"},
            {"box.wooden","10m"},
            {"bucket.water","10m"},
            {"building.planner","10m"},
            {"burlap.shirt","10m"},
            {"burlap.shoes","10m"},
            {"cactusflesh","5m"},
            {"tool.camera","10m"},
            {"campfire","5m"},
            {"can.beans","5m"},
            {"can.beans.empty","5m"},
            {"can.tuna","5m"},
            {"can.tuna.empty","5m"},
            {"candycane","5m"},
            {"cctv.camera","30m"},
            {"ceilinglight","5m"},
            {"chair","10m"},
            {"charcoal","20m"},
            {"chicken.burned","1m"},
            {"chicken.cooked","10m"},
            {"chicken.raw","10m"},
            {"chicken.spoiled","10m"},
            {"chocholate","5m"},
            {"cloth","30m"},
            {"coal","10m"},
            {"corn","5m"},
            {"clone.corn","5m"},
            {"seed.corn","5m"},
            {"crossbow","30m"},
            {"crude.oil","30m"},
            {"cupboard.tool","15m"},
            {"door.double.hinged.metal","10m"},
            {"door.double.hinged.toptier","1h"},
            {"door.double.hinged.wood","5m"},
            {"door.hinged.metal","10m"},
            {"door.hinged.toptier","1h"},
            {"door.hinged.wood","5m"},
            {"door.key","5m"},
            {"door.closer","10m"},
            {"ducttape","10m"},
            {"explosive.satchel","4h"},
            {"explosive.timed","6h"},
            {"explosives","1h"},
            {"fat.animal","20m"},
            {"fish.cooked","5m"},
            {"fish.raw","5m"},
            {"flamethrower","3h"},
            {"flameturret","1h"},
            {"flare","10m"},
            {"weapon.mod.flashlight","30m"},
            {"floor.grill","10m"},
            {"floor.ladder.hatch","10m"},
            {"fridge","10m"},
            {"lowgradefuel","30m"},
            {"furnace","10m"},
            {"furnace.large","20m"},
            {"gates.external.high.stone","30m"},
            {"gates.external.high.wood","15m"},
            {"gears","10m"},
            {"generator.wind.scrap","10m"},
            {"burlap.gloves","5m"},
            {"glue","10m"},
            {"granolabar","15m"},
            {"grenade.beancan","30m"},
            {"grenade.f1","30m"},
            {"fun.guitar","5m"},
            {"gunpowder","2h"},
            {"attire.hide.helterneck","5m"},
            {"hammer","5m"},
            {"hammer.salvaged","30m"},
            {"hat.beenie","5m"},
            {"hat.boonie","5m"},
            {"bucket.helmet","10m"},
            {"burlap.headwrap","5m"},
            {"hat.candle","5m"},
            {"hat.cap","5m"},
            {"coffeecan.helmet","1h"},
            {"deer.skull.mask","5m"},
            {"heavy.plate.helmet","30m"},
            {"hat.miner","5m"},
            {"riot.helmet","15m"},
            {"hat.wolf","10m"},
            {"hatchet","20m"},
            {"hazmat.boots","10m"},
            {"hazmat.gloves","10m"},
            {"hazmat.helmet","10m"},
            {"hazmat.jacket","10m"},
            {"hazmat.pants","10m"},
            {"hazmatsuit","20m"},
            {"clone.hemp","5m"},
            {"seed.hemp","20m"},
            {"attire.hide.boots","10m"},
            {"attire.hide.skirt","10m"},
            {"attire.hide.vest","10m"},
            {"weapon.mod.holosight","30m"},
            {"hoodie","10m"},
            {"hq.metal.ore","30m"},
            {"humanmeat.burned","5m"},
            {"humanmeat.cooked","5m"},
            {"humanmeat.raw","5m"},
            {"humanmeat.spoiled","5m"},
            {"icepick.salvaged","30m"},
            {"bone.armor.suit","5m"},
            {"heavy.plate.jacket","20m"},
            {"jacket.snow","15m"},
            {"jacket","15m"},
            {"jackolantern.angry","10m"},
            {"jackolantern.happy","10m"},
            {"knife.bone","10m"},
            {"ladder.wooden.wall","10m"},
            {"trap.landmine","10m"},
            {"lantern","15m"},
            {"largemedkit","30m"},
            {"weapon.mod.lasersight","90m"},
            {"leather","15m"},
            {"lock.code","10m"},
            {"lock.key","10m"},
            {"locker","10m"},
            {"longsword","10m"},
            {"rifle.lr300","1d"},
            {"lmg.m249","1d"},
            {"pistol.m92","2h"},
            {"mace","10m"},
            {"machete","10m"},
            {"map","10m"},
            {"mask.balaclava","10m"},
            {"mask.bandana","10m"},
            {"metal.facemask","1h"},
            {"bearmeat.burned","10m"},
            {"bearmeat.cooked","10m"},
            {"bearmeat","10m"},
            {"meat.pork.burned","10m"},
            {"meat.pork.cooked","10m"},
            {"meat.boar","10m"},
            {"wolfmeat.burned","10m"},
            {"wolfmeat.cooked","10m"},
            {"wolfmeat.raw","10m"},
            {"wolfmeat.spoiled","10m"},
            {"metal.fragments","20m"},
            {"metal.ore","15m"},
            {"metal.plate.torso","1h"},
            {"metal.refined","1h"},
            {"metalblade","10m"},
            {"metalpipe","15m"},
            {"mining.pumpjack","10m"},
            {"mining.quarry","30m"},
            {"fish.minnows","10m"},
            {"smg.mp5","2h"},
            {"mushroom","10m"},
            {"weapon.mod.muzzleboost","30m"},
            {"weapon.mod.muzzlebrake","30m"},
            {"note","10m"},
            {"burlap.trousers","10m"},
            {"pants","10m"},
            {"heavy.plate.pants","1h"},
            {"attire.hide.pants","10m"},
            {"roadsign.kilt","15m"},
            {"pants.shorts","10m"},
            {"paper","10m"},
            {"pickaxe","10m"},
            {"pistol.eoka","5m"},
            {"pistol.revolver","30m"},
            {"pistol.semiauto","1h"},
            {"planter.large","10m"},
            {"planter.small","10m"},
            {"attire.hide.poncho","10m"},
            {"pookie.bear","10m"},
            {"xmas.present.large","10m"},
            {"xmas.present.medium","10m"},
            {"xmas.present.small","10m"},
            {"propanetank","10m"},
            {"pumpkin","15m"},
            {"clone.pumpkin","10m"},
            {"seed.pumpkin","10m"},
            {"pistol.python","1h"},
            {"target.reactive","10m"},
            {"box.repair.bench","10m"},
            {"research.table","20m"},
            {"researchpaper","20m"},
            {"riflebody","1h"},
            {"roadsign.jacket","20m"},
            {"roadsigns","15m"},
            {"rock","30s"},
            {"rocket.launcher","6h"},
            {"rope","10m"},
            {"rug.bear","10m"},
            {"rug","10m"},
            {"water.salt","10m"},
            {"salvaged.cleaver","10m"},
            {"salvaged.sword","10m"},
            {"santahat","10m"},
            {"weapon.mod.small.scope","2h"},
            {"searchlight","10m"},
            {"rifle.semiauto","2h"},
            {"semibody","30m"},
            {"sewingkit","10m"},
            {"sheetmetal","10m"},
            {"shelves","10m"},
            {"shirt.collared","10m"},
            {"shirt.tanktop","10m"},
            {"shoes.boots","10m"},
            {"shotgun.double","30m"},
            {"shotgun.pump","2h"},
            {"shotgun.waterpipe","20m"},
            {"shutter.metal.embrasure.a","10m"},
            {"shutter.metal.embrasure.b","10m"},
            {"shutter.wood.a","10m"},
            {"sign.hanging.banner.large","10m"},
            {"sign.hanging","10m"},
            {"sign.hanging.ornate","10m"},
            {"sign.pictureframe.landscape","10m"},
            {"sign.pictureframe.portrait","10m"},
            {"sign.pictureframe.tall","10m"},
            {"sign.pictureframe.xl","10m"},
            {"sign.pictureframe.xxl","10m"},
            {"sign.pole.banner.large","10m"},
            {"sign.post.double","10m"},
            {"sign.post.single","10m"},
            {"sign.post.town","10m"},
            {"sign.post.town.roof","10m"},
            {"sign.wooden.huge","10m"},
            {"sign.wooden.large","10m"},
            {"sign.wooden.medium","10m"},
            {"sign.wooden.small","10m"},
            {"weapon.mod.silencer","30m"},
            {"weapon.mod.simplesight","30m"},
            {"skull.human","10m"},
            {"skull.wolf","10m"},
            {"sleepingbag","10m"},
            {"small.oil.refinery","30m"},
            {"stash.small","10m"},
            {"fish.troutsmall","5m"},
            {"smallwaterbottle","10m"},
            {"smg.2","2h"},
            {"smgbody","30m"},
            {"spear.stone","10m"},
            {"spear.wooden","10m"},
            {"spikes.floor","10m"},
            {"spinner.wheel","5m"},
            {"metalspring","15m"},
            {"sticks","10m"},
            {"stocking.large","5m"},
            {"stocking.small","5m"},
            {"stone.pickaxe","10m"},
            {"stonehatchet","10m"},
            {"stones","30m"},
            {"sulfur","1h"},
            {"sulfur.ore","1h"},
            {"supply.signal","1h"},
            {"surveycharge","15m"},
            {"fishtrap.small","5m"},
            {"syringe.medical","20m"},
            {"table","5m"},
            {"targeting.computer","1h"},
            {"tarp","20m"},
            {"techparts","1h"},
            {"smg.thompson","2h"},
            {"torch","5m"},
            {"tshirt","5m"},
            {"tshirt.long","5m"},
            {"tunalight","5m"},
            {"vending.machine","20m"},
            {"wall.external.high.stone","20m"},
            {"wall.external.high","10m"},
            {"wall.frame.cell.gate","15m"},
            {"wall.frame.cell","15m"},
            {"wall.frame.fence.gate","15m"},
            {"wall.frame.fence","15m"},
            {"wall.frame.netting","15m"},
            {"wall.frame.shopfront","15m"},
            {"wall.frame.shopfront.metal","15m"},
            {"wall.window.bars.metal","15m"},
            {"wall.window.bars.toptier","15m"},
            {"wall.window.bars.wood","15m"},
            {"water","10m"},
            {"water.catcher.large","5m"},
            {"water.catcher.small","5m"},
            {"water.barrel","5m"},
            {"waterjug","5m"},
            {"water.purifier","5m"},
            {"wood","15m"},
            {"wood.armor.jacket","10m"},
            {"wood.armor.pants","10m"},
            {"wood.armor.helmet","10m"},
            {"deermeat.burned","10m"},
            {"deermeat.cooked","10m"},
            {"deermeat.raw","10m"},
            {"mailbox","10m"},
            {"scrap","10m"},
            {"dropbox","10m"},
            {"guntrap","10m"},
            {"workbench1","15m"},
            {"workbench2","1h"},
            {"workbench3","2h"},
        };

        #endregion

        public class BaseConfigClass
        {
            public virtual void Initialize()
            {

            }
        }

        public class Settings : BaseConfigClass
        {
            public float GlobalDespawnTime = -1f;
            public Dictionary<string, float> despawnTimes { get; set; } = new Dictionary<string, float>();

            public override void Initialize()
            {
                foreach (var item in ItemManager.itemList)
                {
                    if (!despawnTimes.ContainsKey(item.shortname))
                    {
                        despawnTimes.Add(item.shortname, item.quickDespawn ? 30f : Mathf.Clamp(((int)item.rarity - 1) * 4, 1, 100) * 300 / 60);
                    }
                }

                //Used to get Dictionary of default values
                //_plugin.Puts(string.Join("\n", despawnTimes.Select(x => $"{{\"{x.Key}\",\"{x.Value}\"}},").ToArray())); //I hate escaping characters, makes it impossible to read
            }

            public float GetDespawnTime(Item item)
            {
                if (item == null)
                {
                    return 0;
                }
                if (GlobalDespawnTime >= 0)
                {
                    return GlobalDespawnTime * 60;
                }
                if (!despawnTimes.ContainsKey(item.info.shortname))
                {
                    _plugin.PrintError($"Couldn't find despawn time for {item.info.shortname}! (This message should NEVER show...)");
                    return 5f;
                }
                float time = despawnTimes[item.info.shortname] * 60;
                return time;
            }
        }

        #region Hooks

        private bool serverInitialized = false;

        void OnServerInitialized()
        {
            settings = new JSONFile<Settings>("DespawnConfig", ConfigLocation.Config);
            settings.Instance.Initialize();
            settings.Save();

            if (Manager.GetPlugins().Any(x => x.Name == "NoDespawning"))
            {
                rust.RunServerCommand("oxide.unload NoDespawing");
                PrintError("NoDespawning Detected! Please delete NoDespawning, it is obsolete!");
            }

            serverInitialized = true;

            foreach (var item in GameObject.FindObjectsOfType<DroppedItem>())
            {
                SetDespawnTime(item);
            }

            foreach(var container in GameObject.FindObjectsOfType<DroppedItemContainer>())
            {
                SetDespawnTime(container);
            }
        }

        void Loaded()
        {
            _plugin = this;
        }

        void Unload()
        {
        }

        void OnEntitySpawned(BaseNetworkable entity) //Assigns entities to chunks
        {
            if (serverInitialized == false)
            {
                return;
            }

            if (entity is DroppedItem)
            {
                SetDespawnTime(entity as DroppedItem);
            }
            else if (entity is DroppedItemContainer)
            {
                SetDespawnTime(entity as DroppedItemContainer);
            }
        }

        void SetDespawnTime(DroppedItem item)
        {
            if (item == null)
            {
                return;
            }

            if (item.IsDestroyed)
            {
                return;
            }

            item.Invoke(new Action(item.IdleDestroy), settings.Instance.GetDespawnTime(item.item));
        }

        void SetDespawnTime(DroppedItemContainer container)
        {
            if (container == null)
            {
                return;
            }
            float despawnTime = container.inventory.itemList.Max(x => settings.Instance.GetDespawnTime(x));
            timer.In(1f, () => //Time to make sure that despawn time isnt reset by the float.max value
            {
                container?.ResetRemovalTime(despawnTime);
            });
        }

        void CanCombineDroppedItem(DroppedItem item1, DroppedItem item2)
        {
            NextFrame(() =>
            {
                SetDespawnTime(item1);
            });
        }

        #endregion
    }
}