using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BetterCharcoal", "Jake_Rich", "1.0.2", ResourceId = 2754)]
    [Description("Multiple stacks of wood will burn at once in a furnace when no ore is present")]

    public partial class BetterCharcoal : RustPlugin
    {
        public static BetterCharcoal _plugin;
        public JSONFile<ConfigData> _settingsFile;
        public ConfigData Settings { get { return _settingsFile.Instance; } }

        public const string PermAllowed = "bettercharcoal.allowed";

        void Init()
        {
            _plugin = this;
        }

        void Loaded()
        {
            //Dont create empty config files
            if (typeof(ConfigData).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Length > 0)
            {
                _settingsFile = new JSONFile<ConfigData>($"{Name}", ConfigLocation.Config, extension: ".cfg");
            }
            permission.RegisterPermission(PermAllowed, this);
        }

        void Unload()
        {

        }

        void OnConsumeFuel(BaseOven oven, Item fuel, ItemModBurnable burnable)
        {
            if (Settings.UsePermissions)
            {
                if (permission.UserHasPermission(oven.OwnerID.ToString(), PermAllowed) == false)
                {
                    return;
                }
            }
            if (oven.inventory.itemList.Any(x=>x.info.GetComponent<ItemModCookable>()))
            {
                foreach(var item in oven.inventory.itemList)
                {
                    if (item == fuel)
                    {
                        continue;
                    }
                    if (item.HasFlag(global::Item.Flag.OnFire))
                    {
                        item.SetFlag(global::Item.Flag.OnFire, false);
                    }
                }
                return;
            }
            foreach(var item in oven.inventory.itemList.Where(x=>x.info.shortname == "wood" && x != fuel))
            {
                item.SetFlag(global::Item.Flag.OnFire, true);
                var charcoalMod = item.info.GetComponent<ItemModBurnable>();
                if (UnityEngine.Random.Range(0f, 1f) > Settings.CharcoalChance)
                {
                    TryAddItem(oven.inventory, charcoalMod.byproductItem, Settings.CharcoalPerWood);
                }
                item.UseItem(Settings.WoodSmeltedPerTick);
            }
            ItemManager.DoRemoves();
        }

        public class ConfigData
        {
            public float CharcoalChance = 0.7f;
            public int CharcoalPerWood = 1;
            public int WoodSmeltedPerTick = 1;
            public bool UsePermissions = false;
        }

        public static void TryAddItem(ItemContainer container, ItemDefinition definition, int amount)
        {
            int amountLeft = amount;
            foreach (var item in container.itemList)
            {
                if (item.info != definition)
                {
                    continue;
                }
                if (amountLeft <= 0)
                {
                    return;
                }
                if (item.amount < item.MaxStackable())
                {
                    int amountToAdd = Mathf.Min(amountLeft, item.MaxStackable() - item.amount);
                    item.amount += amountToAdd;
                    item.MarkDirty();
                    amountLeft -= amountToAdd;
                }
            }
            if (amountLeft <= 0)
            {
                return;
            }
            var smeltedItem = ItemManager.Create(definition, amountLeft);
            if (!smeltedItem.MoveToContainer(container))
            {
                smeltedItem.Drop(container.dropPosition, container.dropVelocity);
                var oven = container.entityOwner as BaseOven;
                if (oven != null)
                {
                    oven.OvenFull();
                }
            }
        }

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