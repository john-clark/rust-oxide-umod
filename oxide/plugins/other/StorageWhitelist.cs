using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("StorageWhitelist", "Kaleidos", "0.2.1")]
    class StorageWhitelist : RustPlugin
    {
        private const string storageWhitelistIgnore = "storagewhitelist.ignore";

        private List<string> rafinery;
        private List<string> furnaceLarge;
        private List<string> furnace;
        private List<string> campfire;
        private List<string> fuelstorage;
        private List<string> hopperoutput;
        private List<string> crudeoutput;

        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new config file");
            Config.Clear();

            SetConfig("Storage Container", "refinery_small_deployed", new List<object>
{
                "wood",
                "crude.oil",
                "lowgradefuel"
            });

            SetConfig("Storage Container", "furnace", new List<object> {
                "wood",
                "charcoal",
                "sulfur.ore",
                "sulfur",
                "metal.ore",
                "metal.fragments",
                "hq.metal.ore",
                "metal.refined"
            });
            SetConfig("Storage Container", "furnace.large", new List<object>
            {
                "wood",
                "charcoal",
                "sulfur.ore",
                "sulfur",
                "metal.ore",
                "metal.fragments",
                "hq.metal.ore",
                "metal.refined"
            });
            SetConfig("Storage Container", "campfire", new List<object>
            {
                "wood",
                "charcoal",
                "bearmeat",
                "bearmeat.burned",
                "bearmeat.cooked",
                "humanmeat.burned",
                "humanmeat.cooked",
                "humanmeat.raw",
                "humanmeat.spoiled",
                "meat.boar",
                "meat.pork.burned",
                "meat.pork.cooked",
                "wolfmeat.burned",
                "wolfmeat.cooked",
                "wolfmeat.raw",
                "wolfmeat.spoiled",
                "chicken.burned",
                "chicken.cooked",
                "chicken.raw",
                "chicken.spoiled",
                "fish.raw",
                "fish.cooked"
            });
            SetConfig("Storage Container", "fuelstorage", new List<object>
            {
                "lowgradefuel"
            });
            SetConfig("Storage Container", "hopperoutput", new List<object>
            {
                "sulfur.ore",
                "metal.ore",
                "metal.fragments",
                "hq.metal.ore",
                "stones"
            });
            SetConfig("Storage Container", "crudeoutput", new List<object>
            {
                "crude.oil"
            });
            SaveConfig();
            LoadVariables();
        }
        private void LoadVariables()
        {
            rafinery = ConvertList(Config.Get("Storage Container", "refinery_small_deployed"));
            furnaceLarge = ConvertList(Config.Get("Storage Container", "furnace.large"));
            furnace = ConvertList(Config.Get("Storage Container", "furnace"));
            campfire = ConvertList(Config.Get("Storage Container", "campfire"));
            fuelstorage = ConvertList(Config.Get("Storage Container", "fuelstorage"));
            hopperoutput = ConvertList(Config.Get("Storage Container", "hopperoutput"));
            crudeoutput = ConvertList(Config.Get("Storage Container", "crudeoutput"));
        }
        private void OnServerInitialized()
        {
            LoadVariables();
            permission.RegisterPermission(storageWhitelistIgnore, this);
        }
        private object CanAcceptItem(ItemContainer container, Item item)
        {
            var storage = container.entityOwner as StorageContainer;
            if (storage == null) return null;
            var player = item.GetOwnerPlayer();
            if (player == null || permission.UserHasPermission(player.UserIDString, storageWhitelistIgnore)) return null;
            var storageContainers = (Dictionary<string, object>)Config.Get("Storage Container");
            if (storageContainers.Count == 0 || !storageContainers.ContainsKey(storage.ShortPrefabName)) return null;

            List<string> storageList;
            switch (storage.ShortPrefabName)
            {
                case "refinery_small_deployed":
                    storageList = rafinery;
                    break;
                case "furnace.large":
                    storageList = furnaceLarge;
                    break;
                case "furnace":
                    storageList = furnace;
                    break;
                case "campfire":
                    storageList = campfire;
                    break;
                case "fuelstorage":
                    storageList = fuelstorage;
                    break;
                case "hopperoutput":
                    storageList = hopperoutput;
                    break;
                case "crudeoutput":
                    storageList = crudeoutput;
                    break;
                default:
                    storageList = new List<string>();
                    break;
            }

            if (!(storageList.Contains(item.info.shortname)))
                return ItemContainer.CanAcceptResult.CannotAccept;
            return null;
        }

        private void SetConfig(params object[] args)
        {
            string[] stringArgs = GetConfigPath(args);
            if (Config.Get(stringArgs) == null) Config.Set(args);
        }
        private string[] GetConfigPath(params object[] args)
        {
            string[] stringArgs = new string[args.Length - 1];
            for (var i = 0; i < args.Length - 1; i++)
                stringArgs[i] = args[i].ToString();
            return stringArgs;
        }
        private List<string> ConvertList(object value)
        {
            if (value is List<object>)
                return ((List<object>)value).ConvertAll(l => (string)l);
            return (List<string>)value;
        }        
    }
}
