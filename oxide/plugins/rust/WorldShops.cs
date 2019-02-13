//todo: lessen lambda use a bit to optimize performance
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;
// ReSharper disable UnusedMember.Local
// ReSharper disable SuggestBaseTypeForParameter
// ReSharper disable CheckNamespace
// ReSharper disable UnusedParameter.Local

namespace Oxide.Plugins
{
    [Info("WorldShops", "4aiur", "1.1.1", ResourceId = 2820)]
    [Description("Allows for automated vending machines.")]
    public class WorldShops : RustPlugin
    {
        private static PluginTimers _timer;

        private Dictionary<BasePlayer, WorldShopsSettings.Shop> queuedShops;
        private List<BasePlayer> queuedWipes;
        private Dictionary<BasePlayer, WorldShopsSettings.Shop> queuedSaves;
        private List<BasePlayer> queuedDisables;
        private List<BasePlayer> queuedSpawns;

        private static Dictionary<VendingMachine, WorldShopsSettings.Shop> _activeShops;

        private void Init()
        {
            _timer = this.timer;
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                Formatting = Formatting.Indented
            };

            WorldShopsSettings.Loaded = this.Config.ReadObject<WorldShopsSettings.General>();

            WorldShopsData.Loaded = Interface.Oxide.DataFileSystem.ReadObject<WorldShopsData.General>(nameof(WorldShops));
            if (WorldShopsData.Loaded.Shops == null)
                WorldShopsData.Loaded.Shops = new Dictionary<string, string>();

            for (int i = 0; i < WorldShopsSettings.Loaded.Shops.Count; i++)
            {
                WorldShopsSettings.Shop shop = WorldShopsSettings.Loaded.Shops[i];

                if (shop.CommandName == null)
                {
                    this.PrintError($"Unable to load WorldShops. The {nameof(WorldShopsSettings.Shop.CommandName)} of {nameof(WorldShopsSettings.Shop)} #{i + 1} is null. Please set a value.");
                    this.Manager.RemovePlugin(this);
                    return;
                }

                if (shop.SellOrders == null)
                {
                    shop.SellOrders = new WorldShopsSettings.SellOrder[0];
                    this.PrintWarning($"The {nameof(WorldShopsSettings.Shop.SellOrders)} of {nameof(WorldShopsSettings.Shop)} \"{shop.CommandName}\" is null. The value has been set to an empty array.");
                }

                if (shop.WorldName == null)
                {
                    shop.WorldName = "A Shop";
                    this.PrintWarning($"The {nameof(WorldShopsSettings.Shop.WorldName)} of {nameof(WorldShopsSettings.Shop)} \"{shop.CommandName}\" is null. The value has been set \"A Shop\".");
                }

                for (int j = 0; j < shop.SellOrders.Length; j++)
                {
                    if (shop.SellOrders[j].BuyItem.Definition == null)
                    {
                        this.RaiseError($"Unable to load WorldShops. The {nameof(WorldShopsSettings.SellOrder.BuyItem)} of {nameof(WorldShopsSettings.SellOrder)} #{j + 1} in {nameof(WorldShopsSettings.Shop)} \"{shop.CommandName}\" has an invalid item shortname.");
                        this.Manager.RemovePlugin(this);
                        return;
                    }
                    if (shop.SellOrders[j].SellItem.Definition == null)
                    {
                        this.RaiseError($"Unable to load WorldShops. The {nameof(WorldShopsSettings.SellOrder.SellItem)} of {nameof(WorldShopsSettings.SellOrder)} #{j + 1} in {nameof(WorldShopsSettings.Shop)} \"{shop.CommandName}\" has an invalid item shortname.");
                        this.Manager.RemovePlugin(this);
                        return;
                    }
                }
            }

            string[] names = WorldShopsSettings.Loaded.Shops.Select(x => x.CommandName).ToArray();
            string conflictingName = names.FirstOrDefault(x => names.Length - names.Except(new string[] {x}).Count() > 1);
            if (conflictingName != null)
            {
                this.RaiseError($"Unable to load WorldShops. Two or more shops have a conflicting command name: {conflictingName}");
                this.Manager.RemovePlugin(this);
                return;
            }

            this.queuedShops = new Dictionary<BasePlayer, WorldShopsSettings.Shop>();
            this.queuedWipes = new List<BasePlayer>();
            this.queuedSaves = new Dictionary<BasePlayer, WorldShopsSettings.Shop>();
            this.queuedDisables = new List<BasePlayer>();
            this.queuedSpawns = new List<BasePlayer>();

            _activeShops = new Dictionary<VendingMachine, WorldShopsSettings.Shop>();
            
            this.permission.RegisterPermission("worldshops.build", this);
            this.permission.RegisterPermission("worldshops.spawn", this);
            this.permission.RegisterPermission("worldshops.apply", this);
            this.permission.RegisterPermission("worldshops.disable", this);
            this.permission.RegisterPermission("worldshops.wipe", this);
            this.permission.RegisterPermission("worldshops.save", this);
            this.permission.RegisterPermission("worldshops.delete", this);
            foreach (string name in names)
                this.permission.RegisterPermission($"worldshops.remote.{name}", this);

            this.Config.WriteObject(WorldShopsSettings.Loaded);

            if (WorldShopsSettings.Loaded.Notification.Enabled)
                BasePlayer.activePlayerList.ForEach(x => x.gameObject.AddComponent<ShopBlock>());
        }

        private void OnPlayerSleep(BasePlayer player)
        {
            ShopBlock shopBlock = player.gameObject.GetComponent<ShopBlock>();
            if (shopBlock == null)
                return;

            shopBlock.Dispose();
            UnityEngine.Object.Destroy(shopBlock);
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!WorldShopsSettings.Loaded.Notification.Enabled)
                return;

            player.gameObject.AddComponent<ShopBlock>();
        }

        private void OnServerInitialized()
        {
            foreach (VendingMachine machine in UnityEngine.Object.FindObjectsOfType<VendingMachine>())
            {
                string machineId = machine.ServerPosition.ToString("f2");
                if (!WorldShopsData.Loaded.Shops.ContainsKey(machineId))
                    return;

                string shopName = WorldShopsData.Loaded.Shops[machineId];
                if (shopName == null)
                    return;

                WorldShopsSettings.Shop shop = WorldShopsSettings.Loaded.Shops.FirstOrDefault(x => x.CommandName == shopName);
                if (shop == null)
                    return;
                
                this.ApplyMachine(machine, shop);
                _activeShops.Add(machine, shop);
            }
        }

        protected override void LoadDefaultMessages()
        {
            this.lang.RegisterMessages(new Dictionary<string, string>
            {
                ["InsufficientPermission"] = "You do not have permission to use that command.",
                ["Help"] = "Use of /wshop:\n" +
                           "/wshop spawn - Spawns a vending machine, anywhere*\n" +
                           "/wshop apply [name] - Applies a specified shop to a vending machine*\n" +
                           "/wshop disable - No longer treats the vending machine as a shop*\n" +
                           "/wshop wipe - Resets a vending machine*\n" +
                           "/wshop save [name] - Creates a shop out of a customized vending machine*\n" +
                           "/wshop delete [name] - Deletes a shop*\n" +
						   "/wshop remote [name] - Remotes into a shop if accessable*\n" +
                           "/wshop list - Lists all shops\n" +
                           "/wshop help - Shows this\n" +
                           "* Special permissions needed to execute",
                ["InvalidShop"] = "No shop with the name \"{0}\" exists.",
                ["ShopNotPlaced"] = "The shop \"{0}\" is not on the map.",
                ["ShopExists"] = "The shop name \"{0}\" is already taken.",
                ["ApplyReady"] = "Selected shop \"{0}\"",
                ["ApplyTimeout"] = "Deselected shop \"{0}\"",
                ["ApplySuccess"] = "Applied shop \"{0}\"",
                ["SaveReady"] = "Ready to save shop \"{0}\"",
                ["SaveTimeout"] = "Saving shop \"{0}\" timed out",
                ["SaveSuccess"] = "Saved shop \"{0}\"",
                ["DeletedShop"] = "Deleted shop \"{0}\"",
                ["DeletedShopPlaced"] = "Warning: This shop was placed and all instances of this shop in the world were deleted.",
                ["WipeReady"] = "Ready to wipe shop",
                ["WipeTimeout"] = "Shop wipe has timed out",
                ["WipeSuccess"] = "Wiped shop",
                ["DisableReady"] = "Ready to disable shop",
                ["DisableTimeout"] = "Shop disable has timed out",
                ["DisableSuccess"] = "Disabled shop",
                ["SpawnReady"] = "Ready to spawn vending machine",
                ["SpawnTimeout"] = "Spawn has timed out",
                ["SpawnSuccess"] = "Spawned vending machine",
                ["ShopTooClose"] = "Too close to shop ({0})",
                ["ShopListElement"] = "{0} ({1}) - {2} instances on map"
            }, this);
        }

        [ChatCommand("wshop")]
        private void ShopCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                player.ChatMessage(this.Lang("Help", player));
                return;
            }

            bool commandPlaced;
            string fullName;
            bool fullPlaced;
            WorldShopsSettings.Shop foundShop;
            switch (args[0])
            {
                case "spawn":
                    if (!player.IPlayer.HasPermission("worldshops.spawn"))
                    {
                        player.ChatMessage(this.Lang("InsufficientPermission", player));
                        return;
                    }

                    this.queuedSpawns.Add(player);
                    player.ChatMessage(this.Lang("SpawnReady", player));

                    _timer.Once(5f, () =>
                    {
                        if (!this.queuedSpawns.Contains(player))
                            return;

                        this.queuedSpawns.Remove(player);
                        player.ChatMessage(this.Lang("SpawnTimeout", player));
                    });
                    break;

                case "apply":
                    if (args.Length != 2)
                    {
                        player.ChatMessage(this.Lang("Help", player));
                        return;
                    }

                    if (!player.IPlayer.HasPermission("worldshops.apply"))
                    {
                        player.ChatMessage(this.Lang("InsufficientPermission", player));
                        return;
                    }

                    if (!WorldShopsSettings.Loaded.Shops.Select(x => x.CommandName).Contains(args[1]))
                    {
                        player.ChatMessage(this.Lang("InvalidShop", player, args[1]));
                        return;
                    }
                    WorldShopsSettings.Shop applyShop = WorldShopsSettings.Loaded.Shops.First(x => x.CommandName == args[1]);

                    this.queuedShops.Add(player, applyShop);
                    player.ChatMessage(this.Lang("ApplyReady", player, args[1]));

                    _timer.Once(5f, () =>
                    {
                        if (!this.queuedShops.ContainsKey(player))
                            return;

                        this.queuedShops.Remove(player);
                        player.ChatMessage(this.Lang("ApplyTimeout", player, args[1]));
                    });
                    break;

                case "save":
                    if (args.Length != 2)
                    {
                        player.ChatMessage(this.Lang("Help", player));
                        return;
                    }

                    if (!player.IPlayer.HasPermission("worldshops.save"))
                    {
                        player.ChatMessage(this.Lang("InsufficientPermission", player));
                        return;
                    }

                    if (WorldShopsSettings.Loaded.Shops.Select(x => x.CommandName).Contains(args[1]))
                    {
                        player.ChatMessage(this.Lang("ShopExists", player, args[1]));
                        return;
                    }

                    WorldShopsSettings.Shop newShop = new WorldShopsSettings.Shop
                    {
                        CommandName = args[1]
                    };

                    WorldShopsSettings.Loaded.Shops.Add(newShop);
                    this.queuedSaves.Add(player, newShop);
                    player.ChatMessage(this.Lang("SaveReady", player, args[1]));

                    _timer.Once(5f, () =>
                    {
                        if (!this.queuedSaves.ContainsKey(player))
                            return;

                        this.queuedSaves.Remove(player);
                        player.ChatMessage(this.Lang("SaveTimeout", player, args[1]));
                    });
                    break;

                case "delete":
                    if (args.Length < 2)
                    {
                        player.ChatMessage(this.Lang("Help", player));
                        return;
                    }

                    if (!player.IPlayer.HasPermission("worldshops.delete"))
                    {
                        player.ChatMessage(this.Lang("InsufficientPermission", player));
                        return;
                    }

                    if (!WorldShopsSettings.Loaded.Shops.Any(x => x.CommandName == args[1] || x.WorldName == string.Join(" ", args)))
                    {
                        player.ChatMessage(this.Lang("InvalidShop", player, args[1]));
                        return;
                    }

                    fullName = string.Join(" ", args);
                    foundShop = WorldShopsSettings.Loaded.Shops.First(x => x.CommandName == args[1] || x.WorldName == fullName);

                    commandPlaced = _activeShops.Any(x => x.Value.CommandName == args[1]);
                    fullPlaced = _activeShops.Any(x => x.Value.WorldName == fullName);

                    WorldShopsSettings.Loaded.Shops.RemoveAll(x => x.CommandName == args[1] || x.WorldName == string.Join(" ", args));
                    this.Config.WriteObject(WorldShopsSettings.Loaded);

                    player.ChatMessage(this.Lang("DeletedShop", player, args[1]));

                    if (!commandPlaced || !fullPlaced)
                    {
                        player.ChatMessage(this.Lang("DeletedShopPlaced", player, !commandPlaced ? args[1] : fullName));
                        foreach (VendingMachine vendingMachine in _activeShops.Where(x => x.Value == foundShop).Select(x => x.Key).ToArray())
                            vendingMachine.Kill();
                    }
                    break;

                case "wipe":
                    if (!player.IPlayer.HasPermission("worldshops.wipe"))
                    {
                        player.ChatMessage(this.Lang("InsufficientPermission", player));
                        return;
                    }

                    this.queuedWipes.Add(player);
                    player.ChatMessage(this.Lang("WipeReady", player));

                    _timer.Once(5f, () =>
                    {
                        if (!this.queuedWipes.Contains(player))
                            return;

                        this.queuedWipes.Remove(player);
                        player.ChatMessage(this.Lang("WipeTimeout", player));
                    });
                    break;

                case "disable":
                    if (!player.IPlayer.HasPermission("worldshops.disable"))
                    {
                        player.ChatMessage(this.Lang("InsufficientPermission", player));
                        return;
                    }

                    this.queuedDisables.Add(player);
                    player.ChatMessage(this.Lang("DisableReady", player));

                    _timer.Once(5f, () =>
                    {
                        if (!this.queuedDisables.Contains(player))
                            return;

                        this.queuedDisables.Remove(player);
                        player.ChatMessage(this.Lang("DisableTimeout", player));
                    });
                    break;

                case "list":
                    player.ChatMessage(string.Join("\n", WorldShopsSettings.Loaded.Shops.Select(x => this.Lang("ShopListElement", player, x.WorldName, x.CommandName, _activeShops.Count(y => y.Value == x))).ToArray()));
                    break;

                case "remote":
                    if (args.Length < 2)
                    {
                        player.ChatMessage(this.Lang("Help", player));
                        return;
                    }

                    commandPlaced = _activeShops.Any(x => x.Value.CommandName == args[1]);
                    fullName = string.Join(" ", args);
                    fullPlaced = _activeShops.Any(x => x.Value.WorldName == fullName);
                    if (!commandPlaced || !fullPlaced)
                    {
                        player.ChatMessage(this.Lang("ShopNotPlaced", player, !commandPlaced ? args[1] : fullName));
                        return;
                    }
                    
                    KeyValuePair<VendingMachine, WorldShopsSettings.Shop> remoteMachine = _activeShops.First(x => x.Value.CommandName == args[1] || x.Value.WorldName == fullName);

                    if (!player.IPlayer.HasPermission($"worldshops.remote.{remoteMachine.Value.CommandName}"))
                    {
                        player.ChatMessage(this.Lang("InsufficientPermission", player));
                        return;
                    }

                    _timer.Once(0.1f, () => PlayerLootContainer(player, remoteMachine.Key, remoteMachine.Key.customerPanel));
                    break;

                default:
                    player.ChatMessage(this.Lang("Help", player));
                    break;
            }
        }

        private static void PlayerLootContainer(BasePlayer player, StorageContainer container, string panelName = null)
        {
            container.SetFlag(BaseEntity.Flags.Open, true);
            player.inventory.loot.StartLootingEntity(container, false);
            player.inventory.loot.AddContainer(container.inventory);
            player.inventory.loot.SendImmediate();
            player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", panelName ?? container.panelName);
        }

        protected override void LoadDefaultConfig()
        {
            this.Config.WriteObject(new WorldShopsSettings.General
            {
                Notification = new WorldShopsSettings.ShopNotification
                {
                    Enabled = true,
                    FadeIn = 0.5f,
                    WaitTime = 2f,
                    FadeOut = 0.5f
                },
                Shops = new List<WorldShopsSettings.Shop>
                {
                    new WorldShopsSettings.Shop
                    {
                        WorldName = "Test Shop",
                        CommandName = "test",
                        SkinId = 0,
                        SellOrders = new WorldShopsSettings.SellOrder[]
                        {
                            new WorldShopsSettings.SellOrder
                            {
                                SellItem = new WorldShopsSettings.Item
                                {
                                    ShortName = "rifle.ak",
                                    Quantity = 1,
                                    Blueprint = true
                                },
                                BuyItem = new WorldShopsSettings.Item
                                {
                                    ShortName = "scrap",
                                    Quantity = 1500
                                }
                            }
                        },
                        BuildingBlockedDistance = 50f
                    }
                }
            }, true);
        }

        private void Unload()
        {
            if (WorldShopsData.Loaded != null)
            {
                WorldShopsData.Loaded.Shops.Clear();
                foreach (VendingMachine machine in _activeShops.Keys)
                    WorldShopsData.Loaded.Shops.Add(machine.ServerPosition.ToString("f2"), _activeShops[machine].CommandName);

                Interface.Oxide.DataFileSystem.WriteObject(nameof(WorldShops), WorldShopsData.Loaded);
            }

            foreach (ShopBlock shopBlock in UnityEngine.Object.FindObjectsOfType<ShopBlock>())
            {
                shopBlock.Dispose();
                UnityEngine.Object.Destroy(shopBlock);
            }
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (_activeShops?.Keys == null)
                return;

            if (this.queuedSpawns.Contains(info.InitiatorPlayer))
            {
                info.damageTypes.ScaleAll(0f);
                return;
            }

            DecayEntity deployable = entity as DecayEntity;
            if (deployable != null && _activeShops.Any(x => x.Key.GetNearbyBuildingBlock()?.buildingID == deployable.buildingID || Vector3.Distance(x.Key.CenterPoint(), deployable.CenterPoint()) < x.Value.BuildingBlockedDistance))
                info.damageTypes.ScaleAll(0f);
        }

        private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            Vector3 position = target.position.y == 0 ? target.GetWorldPosition() : target.position; //differs if youre attaching the block to an existing building or creating a new building

            if (planner.GetOwnerPlayer().IPlayer.HasPermission("worldshops.build"))
                return null;

            KeyValuePair<WorldShopsSettings.Shop, float>[] distances = _activeShops.Select(x => new KeyValuePair<WorldShopsSettings.Shop, float>(x.Value, Vector3.Distance(x.Key.CenterPoint(), position))).ToArray();
            WorldShopsSettings.Shop[] closeShops = distances.Where(x => x.Value < x.Key.BuildingBlockedDistance).OrderBy(x => x.Value).Select(x => x.Key).ToArray();
            if (closeShops.Length > 0)
            {
                BasePlayer player = planner.GetOwnerPlayer();
                player.ChatMessage(this.Lang("ShopTooClose", player, string.Join(", ", closeShops.Select(x => x.WorldName).ToArray())));
                return false;
            }

            return null;
        }

        /*
        This area may come of use later once I do more advanced damage detection on shop buildings

        private BasePlayer Owner(BaseEntity entity) =>
            this.Player.Players.FirstOrDefault(x => x.userID == entity.OwnerID);
        */

        //this would normally be hammer only but since spawning on ground is a thing i made it for all weapons
        private void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            VendingMachine machine = info.HitEntity as VendingMachine;
            if (machine == null)
            {
                if (this.queuedSpawns.Contains(attacker))
                {
                    Vector3 spawn = info.HitPositionWorld;

                    Quaternion rotation = Quaternion.LookRotation(attacker.ServerPosition - spawn); //flipped so front faces player
                    rotation = Quaternion.Euler(0f, rotation.eulerAngles.y, rotation.eulerAngles.z); //lock X so it doesnt rotate up or down

                    VendingMachine entity = (VendingMachine)GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vendingmachine.deployed.prefab", spawn, rotation);
                    entity.Spawn();

                    attacker.ChatMessage(this.Lang("SpawnSuccess", attacker));
                    this.NextFrame(() => this.queuedSpawns.Remove(attacker)); //remove next frame so the hit entity doesnt take damage
                }

                return;
            }

            if (this.queuedShops.ContainsKey(attacker))
            {
                this.ApplyMachine(machine, this.queuedShops[attacker]);

                if (_activeShops.ContainsKey(machine))
                    _activeShops[machine] = this.queuedShops[attacker];
                else
                    _activeShops.Add(machine, this.queuedShops[attacker]);

                attacker.ChatMessage(this.Lang("ApplySuccess", attacker, this.queuedShops[attacker].CommandName));
                this.queuedShops.Remove(attacker);

            }
            else if (this.queuedWipes.Contains(attacker))
            {
                if (_activeShops.ContainsKey(machine))
                {
                    this.WipeMachine(machine);
                    this.queuedWipes.Remove(attacker);
                    attacker.ChatMessage(this.Lang("WipeSuccess", attacker));
                }
            }
            else if (this.queuedDisables.Contains(attacker))
            {
                if (_activeShops.ContainsKey(machine))
                {
                    _activeShops.Remove(machine);
                    this.queuedDisables.Remove(attacker);
                    attacker.ChatMessage(this.Lang("DisableSuccess", attacker));
                }
            }
            else if (this.queuedSaves.ContainsKey(attacker))
            {
                this.SaveMachine(machine, this.queuedSaves[attacker]);
                this.Config.WriteObject(WorldShopsSettings.Loaded);

                attacker.ChatMessage(this.Lang("SaveSuccess", attacker, this.queuedSaves[attacker].CommandName));
                this.queuedSaves.Remove(attacker);
            }
        }

        private void OnHammerHit(BasePlayer player, HitInfo info) =>
            this.OnPlayerAttack(player, info); //reroute so people can use the traditional hammer too

        private void OnEntityKill(BaseNetworkable entity)
        {
            VendingMachine machine = entity as VendingMachine;
            if (machine == null)
                return;

            if (_activeShops.ContainsKey(machine))
                _activeShops.Remove(machine);
        }

        private void OnVendingTransaction(VendingMachine machine, BasePlayer buyer, int sellOrderId, int numberOfTransactions)
        {
            if (!_activeShops.ContainsKey(machine))
                return;

            this.NextFrame(() =>
            {
                WorldShopsSettings.SellOrder order = _activeShops[machine].SellOrders[sellOrderId];

                Item stack = machine.inventory.itemList.FirstOrDefault(x => x.info.itemid == machine.sellOrders.sellOrders[sellOrderId].itemToSellID);
                if (stack == null)
                {
                    if (order.SellItem.Blueprint)
                        this.GetBlueprint(order.SellItem.Definition, order.SellItem.Quantity).MoveToContainer(machine.inventory);
                    else 
                        ItemManager.Create(order.SellItem.Definition, order.SellItem.Quantity).MoveToContainer(machine.inventory); //restock
                }
                else
                    stack.amount += order.SellItem.Quantity;

                Item currencyStack = machine.inventory.itemList.First(x => x.info.itemid == machine.sellOrders.sellOrders[sellOrderId].currencyID);
                if (currencyStack.amount == machine.sellOrders.sellOrders[sellOrderId].currencyAmountPerItem)
                    currencyStack.Remove();
                else
                    currencyStack.amount -= machine.sellOrders.sellOrders[sellOrderId].currencyAmountPerItem;
            });
        }

        private void ApplyMachine(VendingMachine machine, WorldShopsSettings.Shop shop)
        {
            machine.sellOrders.sellOrders = shop.SellOrders.Select(x => x.ProtoBuf).ToList();
            machine.shopName = shop.WorldName;
            machine.skinID = shop.SkinId;
            machine.health = machine.MaxHealth();

            machine.inventory.Clear();
            foreach (WorldShopsSettings.SellOrder order in shop.SellOrders)
            {
                if (machine.inventory.itemList.Select(x => x.info).Contains(order.SellItem.Definition))
                    continue;

                if (order.SellItem.Blueprint)
                    this.GetBlueprint(order.SellItem.Definition, order.SellItem.Quantity).MoveToContainer(machine.inventory);
                else
                    ItemManager.Create(order.SellItem.Definition, order.SellItem.Quantity).MoveToContainer(machine.inventory); //restock
            }
        }

        private void WipeMachine(VendingMachine machine)
        {
            machine.sellOrders.sellOrders = new List<ProtoBuf.VendingMachine.SellOrder>();
            machine.inventory.Clear();
            machine.shopName = "A Shop";
            machine.skinID = 0UL;
            machine.health = machine.MaxHealth();

            _activeShops.Remove(machine);
        }

        private void SaveMachine(VendingMachine machine, WorldShopsSettings.Shop shop)
        {
            shop.SellOrders = machine.sellOrders.sellOrders.Select(x =>
                new WorldShopsSettings.SellOrder
                {
                    BuyItem = new WorldShopsSettings.Item
                    {
                        Definition = ItemManager.FindItemDefinition(x.currencyID),
                        Quantity = x.currencyAmountPerItem,
                        Blueprint = x.currencyIsBP
                    },
                    SellItem = new WorldShopsSettings.Item
                    {
                        Definition = ItemManager.FindItemDefinition(x.itemToSellID),
                        Quantity = x.itemToSellAmount,
                        Blueprint = x.itemToSellIsBP
                    }
                }).ToArray();

            shop.SkinId = machine.skinID;
            shop.WorldName = machine.shopName;
        }

        private Item GetBlueprint(ItemDefinition learnableItem, int amount = 1)
        {
            Item item = ItemManager.Create(ResearchTable.GetBlueprintTemplate(), amount);
            item.blueprintTarget = learnableItem.itemid;

            return item;
        }

        private object CanAdministerVending(VendingMachine machine, BasePlayer player)
        {
            if (_activeShops.ContainsKey(machine))
                return false;
            
            return null;
        }
        
        private object OnRotateVendingMachine(VendingMachine machine, BasePlayer player)
        {
            if (_activeShops.ContainsKey(machine))
            {
                if (player.IPlayer.HasPermission("worldshops.build"))
                    return null;

                return false;
            }

            return null;
        }

        private string Lang(string key, BasePlayer player, params object[] args) => string.Format(this.lang.GetMessage(key, this, player.UserIDString), args);

        private class ShopBlock : MonoBehaviour
        {
            private BasePlayer player;
            private bool notificaitonActive;
            private bool notificationShown;

            public void Start()
            {
                this.player = this.GetComponent<BasePlayer>();
                this.notificationShown = _activeShops.Any(x => Vector3.Distance(x.Key.CenterPoint(), this.player.CenterPoint()) < x.Value.BuildingBlockedDistance);
            }

            public void Update()
            {
                if (_activeShops.Any(x => Vector3.Distance(x.Key.CenterPoint(), this.player.CenterPoint()) < x.Value.BuildingBlockedDistance))
                {
                    if (!this.notificationShown && !this.notificaitonActive)
                    {
                        this.ShowGui(false);
                        this.notificationShown = true;
                    }
                }
                else if (this.notificationShown && !this.notificaitonActive)
                {
                    this.ShowGui(true);
                    this.notificationShown = false;
                }
            }

            private void ShowGui(bool exiting)
            {
                this.notificaitonActive = true;

                float[] position = { 0.3945f, 0.11f };
                float[] maxPosition = { position[0] + 0.1953125f, position[1] + 0.104166667f };
                CuiHelper.AddUi(this.player, new List<CuiElement>
                {
                    new CuiElement
                    {
                       Name = "ShopBlockedIcon",
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Url = exiting ? "https://i.imgur.com/0BH83NH.png" : "https://i.imgur.com/MWY2a6x.png",
                                FadeIn = WorldShopsSettings.Loaded.Notification.FadeIn
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"{position[0]} {position[1]}",
                                AnchorMax = $"{maxPosition[0]} {maxPosition[1]}"
                            }
                        },
                        FadeOut = WorldShopsSettings.Loaded.Notification.FadeOut
                    }
                });

                _timer.Once(WorldShopsSettings.Loaded.Notification.WaitTime + WorldShopsSettings.Loaded.Notification.FadeIn, () => CuiHelper.DestroyUi(this.player, "ShopBlockedIcon"));
                _timer.Once(WorldShopsSettings.Loaded.Notification.WaitTime + WorldShopsSettings.Loaded.Notification.FadeIn + WorldShopsSettings.Loaded.Notification.FadeOut, () => this.notificaitonActive = false);
            }

            public void Dispose()
            {
                CuiHelper.DestroyUi(this.player, "ShopBlockedIcon");

                this.player = null;
                this.notificaitonActive = false;
                this.notificationShown = false;
            }
        }

        private class WorldShopsSettings
        {
            public class Item
            {
                [JsonIgnore]
                public ItemDefinition Definition { get; set; }
                
                public string ShortName
                {
                    get
                    {
                        return this.Definition.shortname;
                    }
                    set
                    {
                        this.Definition = ItemManager.FindItemDefinition(value);
                    }
                }
                
                public int Quantity { get; set; }
                public bool Blueprint { get; set; }
            }

            public class SellOrder
            {
                [JsonIgnore]
                public ProtoBuf.VendingMachine.SellOrder ProtoBuf
                {
                    get
                    {
                        return new ProtoBuf.VendingMachine.SellOrder
                        {
                            currencyID = this.BuyItem.Definition.itemid,
                            currencyAmountPerItem = this.BuyItem.Quantity,
                            currencyIsBP = this.BuyItem.Blueprint,
                            itemToSellID = this.SellItem.Definition.itemid,
                            itemToSellAmount = this.SellItem.Quantity,
                            itemToSellIsBP = this.SellItem.Blueprint
                        };
                    }
                }
                
                public Item SellItem { get; set; }
                public Item BuyItem { get; set; }
            }

            public class Shop
            {
                public SellOrder[] SellOrders { get; set; }
                public string WorldName { get; set; }
                public string CommandName { get; set; }
                public ulong SkinId { get; set; }
                public float BuildingBlockedDistance { get; set; }
            }

            public class ShopNotification
            {
                public bool Enabled { get; set; }
                public float FadeIn { get; set; }
                public float WaitTime { get; set; }
                public float FadeOut { get; set; }
            }

            public class General
            {
                public List<Shop> Shops { get; set; }
                public ShopNotification Notification { get; set; }
            }

            public static General Loaded;
        }

        private class WorldShopsData
        {
            public class General
            {
                public Dictionary<string, string> Shops { get; set; }
            }

            public static General Loaded;
        }
    }
}