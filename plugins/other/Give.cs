using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if REIGNOFKINGS
using CodeHatch.Engine.Networking;
using CodeHatch.Inventory.Blueprints;
using CodeHatch.Inventory.Blueprints.Components;
using CodeHatch.ItemContainer;
using UnityEngine;
#endif

// TODO: Add optional cooldown for commands
// TODO: Implement logging to file and log rotation
// TODO: Add support for games other than Rust (ie. Reign of Kings, Hurtworld, etc.)

namespace Oxide.Plugins
{
    [Info("Give", "Wulf/lukespragg", "3.0.2")]
    [Description("Allows players with permission to give items or kits")]
    public class Give : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Item blacklist (name or item ID)")]
            public List<string> ItemBlacklist { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "Log usage to console (true/false)")]
            public bool LogToConsole { get; set; } = false;

            //[JsonProperty(PropertyName = "Log usage to file (true/false)")]
            //public bool LogToFile { get; set; } = false;

            //[JsonProperty(PropertyName = "Rotate logs daily (true/false)")]
            //public bool RotateLogs { get; set; } = true;

            [JsonProperty(PropertyName = "Show chat notices (true/false)")]
            public bool ShowChatNotices { get; set; } = false;

#if RUST

            [JsonProperty(PropertyName = "Show popup notices (true/false)")]
            public bool ShowPopupNotices { get; set; } = false;

#endif
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            string configPath = $"{Interface.Oxide.ConfigDirectory}{Path.DirectorySeparatorChar}{Name}.json";
            LogWarning($"Could not load a valid configuration file, creating a new configuration file at {configPath}");
            config = new Configuration();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion Configuration

        #region Localization

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["GiveKitFailed"] = "Could not give kit {0} to '{1}', giving failed",
                ["GiveKitSuccessful"] = "Giving kit {0} to '{1}' successful",
                ["GiveToFailed"] = "Could not give item {0} to '{1}', giving failed",
                ["GiveToSuccessful"] = "Giving item {0} x {1} to '{2}' successful",
                ["InvalidItem"] = "{0} is not a valid item or is blacklisted",
                ["InvalidKit"] = "{0} is not a valid kit",
                ["ItemNotFound"] = "Could not find any item by name or ID '{0}' to give",
                ["ItemReceived"] = "You've received {0} x {1}",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["NoPlayersFound"] = "No players found with name or ID '{0}'",
                ["PlayersFound"] = "Multiple players were found, please specify: {0}",
                ["PlayersOnly"] = "Command '{0}' can only be used by a player",
                ["UsageGive"] = "Usage: {0} <item id or item name> [amount] [player name or id]",
                ["UsageGiveKit"] = "Usage: {0} <kit name> [player name or id]",
            }, this);
        }

        #endregion Localization

        #region Initialization

        [PluginReference]
        private Plugin Kits;

        private const string permGive = "give.self";
        private const string permGiveAll = "give.all";
#if REIGNOFKINGS || RUST
        private const string permGiveArm = "give.arm";
#endif
        private const string permGiveKit = "give.kit";
        private const string permGiveTo = "give.to";

        private void Init()
        {
            permission.RegisterPermission(permGive, this);
            permission.RegisterPermission(permGiveAll, this);
#if REIGNOFKINGS || RUST
            permission.RegisterPermission(permGiveArm, this);
#endif
            permission.RegisterPermission(permGiveKit, this);
            permission.RegisterPermission(permGiveTo, this);

            AddCovalenceCommand(new[] { "inventory.give", "inventory.giveid", "give", "giveid" }, "GiveCommand");
            AddCovalenceCommand(new[] { "inventory.giveall", "giveall" }, "GiveAllCommand");
#if REIGNOFKINGS || RUST
            AddCovalenceCommand(new[] { "inventory.givearm", "givearm" }, "GiveArmCommand");
#endif
            AddCovalenceCommand(new[] { "inventory.givekit", "givekit" }, "GiveKitCommand");
            AddCovalenceCommand(new[] { "inventory.giveto", "giveto" }, "GiveToCommand");

            // TODO: Localized commands
        }

        #endregion Initialization

        #region Item Giving

#if HURTWORLD
        private ItemGeneratorAsset FindItem(string itemNameOrId)
        {
            Dictionary<int, ItemGeneratorAsset> items = GlobalItemManager.Instance.ItemGenerators;

            ItemGeneratorAsset item;
            int itemId;
            if (int.TryParse(itemNameOrId, out itemId))
            {
                item = items.Values.First(i => i.GeneratorId == itemId);
            }
            else
            {
                item = items.Values.First(i => i.DataProvider.NameKey.ToLower().Contains(itemNameOrId.ToLower()));
            }
            return item;
        }
#elif RUST
        private ItemDefinition FindItem(string itemNameOrId)
        {
            ItemDefinition itemDef = ItemManager.FindItemDefinition(itemNameOrId.ToLower());
            if (itemDef == null)
            {
                int itemId;
                if (int.TryParse(itemNameOrId, out itemId))
                {
                    itemDef = ItemManager.FindItemDefinition(itemId);
                }
            }
            return itemDef;
        }
#endif

        private object GiveItem(IPlayer target, string itemNameOrId, int amount = 1, string container = "main")
        {
            if (config.ItemBlacklist.Contains(itemNameOrId.ToLower()))
            {
                return null;
            }

            string itemName = itemNameOrId;
#if HURTWORLD
            PlayerSession session = target.Object as PlayerSession;
            PlayerInventory inventory = session.WorldPlayerEntity.GetComponent<PlayerInventory>();
            ItemGeneratorAsset generator = FindItem(itemNameOrId);
            ItemObject itemObj;
            if (generator.IsStackable())
            {
                itemObj = GlobalItemManager.Instance.CreateItem(generator, amount);
                if (!inventory.GiveItemServer(itemObj))
                {
                    GlobalItemManager.SpawnWorldItem(itemObj, inventory);
                }
            }
            else
            {
                int amountGiven = 0;
                while (amountGiven < amount)
                {
                    itemObj = GlobalItemManager.Instance.CreateItem(generator);
                    if (!inventory.GiveItemServer(itemObj))
                    {
                        GlobalItemManager.SpawnWorldItem(itemObj, inventory);
                    }
                    amountGiven++;
                }
            }
#elif REIGNOFKINGS
            Player player = target.Object as Player;
            if (player == null)
            {
                return false;
            }

            Container itemContainer = null;
            switch (container.ToLower())
            {
                case "belt":
                    itemContainer = player.CurrentCharacter.Entity.GetContainerOfType(CollectionTypes.Hotbar);
                    break;

                case "main":
                    itemContainer = player.CurrentCharacter.Entity.GetContainerOfType(CollectionTypes.Inventory);
                    break;
            }

            InvItemBlueprint blueprint = InvDefinitions.Instance.Blueprints.GetBlueprintForName(itemName, true, true);
            if (blueprint == null)
            {
                return false;
            }

            ContainerManagement containerManagement = blueprint.TryGet<ContainerManagement>();
            int stackableAmount = containerManagement != null ? containerManagement.StackLimit : 0;
            int amountGiven = 0;
            while (amountGiven < amount)
            {
                int amountToGive = Mathf.Min(stackableAmount, amount - amountGiven);
                InvGameItemStack itemStack = new InvGameItemStack(blueprint, amountToGive, null);
                if (!ItemCollection.AutoMergeAdd(itemContainer.Contents, itemStack))
                {
                    int stackAmount = amountToGive - itemStack.StackAmount;
                    if (stackAmount != 0)
                    {
                        amountGiven += stackAmount;
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    amountGiven += amountToGive;
                }
                if (itemContainer.Contents.FreeSlotCount == 0)
                {
                    break;
                }
            }
#elif RUST
            Item item = ItemManager.Create(FindItem(itemNameOrId));
            if (item == null)
            {
                return false;
            }

            item.amount = amount;

            BasePlayer basePlayer = target.Object as BasePlayer;
            if (basePlayer == null)
            {
                return false;
            }

            ItemContainer itemContainer = null;
            switch (container.ToLower())
            {
                case "belt":
                    itemContainer = basePlayer.inventory.containerBelt;
                    break;

                case "main":
                    itemContainer = basePlayer.inventory.containerMain;
                    break;
            }

            if (!basePlayer.inventory.GiveItem(item, itemContainer))
            {
                item.Remove();
                return false;
            }

            itemName = item.info.displayName.english;

            if (config.ShowPopupNotices)
            {
                target.Command("note.inv", item.info.itemid, amount);
                target.Command("gametip.showgametip", Lang("ItemReceived", target.Id, itemName, amount));
                timer.Once(2f, () => target.Command("gametip.hidegametip"));
            }
#endif
            if (config.ShowChatNotices)
            {
                Message(target, "ItemReceived", itemName, amount);
            }

            if (config.LogToConsole)
            {
                Log($"{target.Name} {amount} x {itemName}");
            }

            return true;
        }

        #endregion Item Giving

        #region Commands

        private void GiveCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permGive))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args.Length < 1)
            {
                Message(player, "UsageGive", command);
                return;
            }

            int amount = args.Length >= 2 ? Convert.ToInt32(args[1]) : 1;

            IPlayer target = null;
            if (args.Length >= 3)
            {
                target = FindPlayer(args[2], player); // TODO: Join args[2]+ for when quotations aren't used
                if (target == null)
                {
                    if (player.IsServer)
                    {
                        Message(player, "PlayersOnly", command);
                    }
                    return;
                }
            }

            if (target == null)
            {
                target = player;
            }

            object giveItem = GiveItem(target, args[0], amount);
            if (giveItem == null)
            {
                Message(player, "InvalidItem", args[0]);
            }
            else if (!(bool)giveItem)
            {
                Message(player, "GiveToFailed", args[0], target.Name);
            }
            else
            {
                Message(player, "GiveToSuccessful", args[0], amount, target.Name);
            }
        }

        private void GiveAllCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permGiveAll))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args.Length < 1)
            {
                Message(player, "UsageGive", command);
                return;
            }

            int amount = args.Length == 2 ? Convert.ToInt32(args[1]) : 1;

            foreach (IPlayer target in players.Connected)
            {
                if (!target.IsConnected)
                {
                    continue;
                }

                object giveItem = GiveItem(target, args[0], amount);
                if (giveItem == null)
                {
                    Message(player, "InvalidItem", args[0]); // TODO: Only show this message once
                }
                else if (!(bool)giveItem)
                {
                    Message(player, "GiveToFailed", args[0], amount, target.Name); // TODO: Only show this message once
                }
            }

            // TODO: Show single success message
        }

#if REIGNOFKINGS || RUST
        private void GiveArmCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permGiveArm))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args.Length < 1)
            {
                Message(player, "UsageGive", command);
                return;
            }

            int amount = args.Length == 2 ? Convert.ToInt32(args[1]) : 1;

            object giveItem = GiveItem(player, args[0], amount, "belt");
            if (giveItem == null)
            {
                Message(player, "InvalidItem", args[0]);
            }
            else if (!(bool)giveItem)
            {
                Message(player, "GiveFailed", args[0], amount);
            }
        }
#endif

        private void GiveKitCommand(IPlayer player, string command, string[] args)
        {
            if (Kits == null || !Kits.IsLoaded)
            {
                // TODO: Show Kits not loaded message
                return;
            }

#if REIGNOFKINGS
            player.Reply($"The '{command}' command is not currently supported in Reign of Kings");
#endif

            if (!player.HasPermission(permGiveKit))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            // TODO: Handle giving kits to self

            if (args.Length < 1)
            {
                Message(player, "UsageGiveKit", command);
                return;
            }

            if (!Kits.Call<bool>("isKit", args[1]))
            {
                Message(player, "InvalidKit", args[1]);
                return;
            }

            IPlayer target = FindPlayer(args[0], player);
            if (target != null)
            {
                bool giveKit = false;
#if REIGNOFKINGS
                giveKit = Kits.Call<bool>("GiveKit", target.Object as Player, args[1]);
#elif RUST
                giveKit = Kits.Call<bool>("GiveKit", target.Object as BasePlayer, args[1]);
#endif
                if (!giveKit)
                {
                    Message(player, "GiveKitFailed", args[1], target.Name);
                }
                else
                {
                    Message(player, "GiveKitSuccessful", args[1], target.Name);
                }
            }
        }

        #endregion Commands

        #region Helpers

        private IPlayer FindPlayer(string nameOrId, IPlayer player)
        {
            IPlayer[] foundPlayers = players.FindPlayers(nameOrId).Where(p => p.IsConnected).ToArray();
            if (foundPlayers.Length > 1)
            {
                Message(player, "PlayersFound", string.Join(", ", foundPlayers.Select(p => p.Name).ToArray()));
                return null;
            }

            IPlayer target = foundPlayers.Length == 1 ? foundPlayers[0] : null;
            if (target == null || !target.IsConnected)
            {
                Message(player, "NoPlayersFound", nameOrId);
                return null;
            }

            return target;
        }

        private string Lang(string key, string id = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, id), args);
        }

        private void Message(IPlayer player, string key, params object[] args)
        {
            player.Reply(Lang(key, player.Id, args));
        }

        #endregion Helpers
    }
}
