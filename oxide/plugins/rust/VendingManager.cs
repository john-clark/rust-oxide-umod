using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("VendingManager", "ignignokt84", "0.2.6", ResourceId = 2331)]
    [Description("Improved vending machine control")]
    class VendingManager : RustPlugin
    {
        #region Variables

        [PluginReference]
        Plugin Economics, ServerRewards;

        // usage permission
        private const string PermCanUse = "vendingmanager.canuse";
        private const string PermCanEject = "vendingmanager.caneject";

        // valid commands
        private enum Command {
            add, clear, eject, info, list, load, reset, save, set, unset
        };

        // configuration options
        private enum Option {
            destroyOnUnload,    // destroy locks on unload
            ejectLocks,         // eject locks on unload/reload
            health,             // health
            lockable,           // allow attaching locks
            lockFailureMessage, // display message on lock attach failure
            saveLocks,          // save locks on unload
            setHealth,          // enable setting health
            noBroadcast,        // blocks broadcasting
            restricted,         // restrict panel access to owners
            useEconomics,       // use Economics
            useServerRewards,   // use ServerRewards
            transactionTimeout, // timeout to end transactions
            logTransSuccess,    // enable logging transaction success
            logTransFailure,    // enable logging transaction failures
            transMessages,      // enable transaction success messages
            currencyItem        // currency item shortname
        }

        // default configuration values
        object[] defaults = new object[] { false, false, defaultHealth, true, false, true, true, false, false, false, false, 300f, false, false, true, "blood" };

        // container for config/data
        VendingData data = new VendingData();
        Dictionary<uint, VendingMachineInfo> vms = new Dictionary<uint, VendingMachineInfo>();
        Dictionary<string, LockInfo> oldlocks = new Dictionary<string, LockInfo>();
        Dictionary<uint, LockInfo> locks = new Dictionary<uint, LockInfo>();
        const float defaultHealth = 500f;
        ProtectionProperties defaultProtection;
        ProtectionProperties customProtection;

        const string CodeLockPrefab = "assets/prefabs/locks/keypad/lock.code.prefab";
        const string KeyLockPrefab = "assets/prefabs/locks/keylock/lock.key.prefab";

        Dictionary<ulong, Timer> transactionTimers = new Dictionary<ulong, Timer>();
        Dictionary<ulong, Timer> timeoutTimers = new Dictionary<ulong, Timer>();

        bool isShuttingDown = false;
        bool useEconomics = false;
        bool useServerRewards = false;
        string currencyPlugin
        {
            get {
                if (useEconomics) return Economics.Name;
                if (useServerRewards) return ServerRewards.Name;
                return null;
            }
        }

        int currencyIndex = 24;
        ItemDefinition currencyItem;

        #endregion

        #region Lang

        // load default messages to Lang
        void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Prefix", "<color=orange>[ VendingManager ]</color> "},
                {"ClearSuccess", "Successfully cleared Vending Machine sell orders"},
                {"SaveSuccess", "Saved Vending Machine sell orders to \"{0}\""},
                {"LoadSuccess", "Loaded Vending Machine sell orders from \"{0}\""},
                {"ResetSuccess", "Successfully cleared and reset VendingManager configuration to defaults"},
                {"ConfirmReset", "To reset the VendingManager configuration and remove all saved templates, type: /vm reset confirm"},
                {"VMNotFound", "No Vending Machine found"},
                {"EmptyTemplate", "Template \"{0}\" is empty"},
                {"EmptyVM", "Vending Machine has no sell orders defined"},
                {"TemplateNotFound", "No sell order template found with name \"{0}\""},
                {"TemplateExists", "Template with name \"{0}\" already exists, add \"overwrite\" parameter to command to overwrite template"},
                {"InvalidCommand", "Invalid command: {0}"},
                {"InvalidParameter", "Invalid parameter"},
                {"NotAuthorized", "You cannot add a lock to that Vending Machine"},
                {"CommandList", "<color=cyan>Valid Commands:</color>" + Environment.NewLine + "{0}"},
                {"TemplateList", "<color=cyan>Templates:</color>" + Environment.NewLine + "{0}"},
                {"Ejected", "Ejected {0} locks from Vending Machines"},
                {"NoBroadcast", "Broadcasting is not allowed" },
                {"Restricted", "You do not have access to administrate that VendingMachine" },
                {"Information", "Vending Machine ID: <color=cyan>{0}</color>" + Environment.NewLine + "Has configuration? <color=cyan>{1}</color>" + Environment.NewLine + "Flags: <color=cyan>{2}</color>" },

                {"EconomicsNotEnoughMoney", "Transaction Cancelled (Economics): Not enough money" },
                {"EconomicsNotEnoughMoneyOwner", "Transaction Cancelled (Economics): Buyer doesn't have enough money" },
                {"EconomicsTransferFailed", "Transaction Cancelled (Economics): Money transfer failed" },
                {"EconomicsPurchaseSuccess", "Successfully purchased {0} {1} for {2:C}; Remaining balance: {3:C}" },
                {"EconomicsSellSuccess", "Successfully sold {0} {1} for {2:C}; New balance: {3:C}" },

                {"ServerRewardsNotEnoughMoney", "Transaction Cancelled (ServerRewards): Not enough RP" },
                {"ServerRewardsNotEnoughMoneyOwner", "Transaction Cancelled (ServerRewards): Buyer doesn't have enough RP" },
                {"ServerRewardsTransferFailed", "Transaction Cancelled (ServerRewards): RP transfer failed" },
                {"ServerRewardsPurchaseSuccess", "Successfully purchased {0} {1} for {2}RP; Remaining balance: {3}RP" },
                {"ServerRewardsSellSuccess", "Successfully sold {0} {1} for {2}RP; New balance: {3}RP" },

                {"SetSuccess", "Successfully set flag <color=cyan>{0}</color>" },
                {"UnsetSuccess", "Successfully removed flag <color=cyan>{0}</color>" },
                {"WarnEconAndSREnabled", "Economics and ServerRewards are both enabled as currency; ServerRewards has been forcibly disabled." },

                {"NotAllowed", "You are not allowed to use this command!"},
                {"CmdBase", "vm"}
            }, this);
        }

        // get message from Lang
        string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion

        #region Loading/Unloading

        // on load
        //void Loaded()
        void Init()
        {
            LoadDefaultMessages();
            cmd.AddChatCommand(GetMessage("CmdBase"), this, "CommandDelegator");
            permission.RegisterPermission(PermCanUse, this);
            permission.RegisterPermission(PermCanEject, this);
            LoadData();
        }

        // on unload, reset all vending machines
        void Unload()
        {
            if (ConfigValue<bool>(Option.saveLocks) || isShuttingDown)
                SaveVMsAndLocks();
            SetAll(false);
            if (ConfigValue<bool>(Option.destroyOnUnload) || isShuttingDown)
                DestroyLocks();

            foreach (Timer t in transactionTimers.Values)
                t?.Destroy();
            foreach (Timer t in timeoutTimers.Values)
                t?.Destroy();
        }

        // server initialized
        void OnServerInitialized()
        {
            SetAll(ConfigValue<bool>(Option.lockable), ConfigValue<float>(Option.health));
            if (ConfigValue<bool>(Option.saveLocks))
                LoadLocks();
            currencyItem = ItemManager.FindItemDefinition(ConfigValue<string>(Option.currencyItem)) ?? ItemManager.FindItemDefinition("blood"); // assume blood if item missing
            useEconomics = ConfigValue<bool>(Option.useEconomics);
            useServerRewards = ConfigValue<bool>(Option.useServerRewards);
            Check(true);
        }

        void OnPluginLoaded(Plugin plugin)
        {
            if (plugin.Name == "Economics" && ConfigValue<bool>(Option.useEconomics))
                Check();
            else if (plugin.Name == "ServerRewards" && ConfigValue<bool>(Option.useServerRewards))
                Check();
        }

        void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin.Name == "Economics" && ConfigValue<bool>(Option.useEconomics))
                Check();
            else if (plugin.Name == "ServerRewards" && ConfigValue<bool>(Option.useServerRewards))
                Check();
        }

        void Check(bool initial = false)
        {
            bool prev = useEconomics;
            useEconomics = ConfigValue<bool>(Option.useEconomics) && Economics != null;
            if (initial || prev != useEconomics)
                Puts("Economics " + (useEconomics ? "detected - money purchases enabled" : "not detected - money purchases disabled"));
            prev = useServerRewards;
            useServerRewards = ConfigValue<bool>(Option.useServerRewards) && ServerRewards != null;
            if (initial || prev != useServerRewards)
                Puts("ServerRewards " + (useServerRewards ? "detected - RP purchases enabled" : "not detected - RP purchases disabled"));
            if (useEconomics && useServerRewards)
            {
                useServerRewards = false;
                PrintWarning(GetMessage("WarnEconAndSREnabled"));
            }
        }

        // save/destroy locks on server shutdown to avoid NULL in saveList
        void OnServerShutdown()
        {
            isShuttingDown = true;
        }

        // save locks when server saves
        void OnServerSave()
        {
            // delayed save to fight the lag monster
            timer.In(5f, () => SaveVMsAndLocks());
        }

        #endregion

        #region Configuration

        // load default config
        bool LoadDefaultConfig()
        {
            data = new VendingData();
            CheckConfig();
            data.templates = new Dictionary<string, SellOrderTemplate>();
            return true;
        }

        void LoadData()
        {
            bool dirty = false;
            try {
                data = Config.ReadObject<VendingData>();
            } catch (Exception) { }
            dirty = CheckConfig();
            if (data.templates == null)
                dirty |= LoadDefaultConfig();
            if (dirty)
                SaveData();
            vms = Interface.GetMod()?.DataFileSystem?.ReadObject<Dictionary<uint, VendingMachineInfo>>("VendingManagerVMs");
            locks = Interface.GetMod()?.DataFileSystem?.ReadObject<Dictionary<uint, LockInfo>>("VendingManagerLocks");
        }

        // write data container to config
        void SaveData()
        {
            Config.WriteObject(data);
        }

        void SaveVendingMachineData()
        {
            foreach(uint k in vms.Keys.ToList())
            {
                BaseNetworkable net = BaseNetworkable.serverEntities.Find(k);
                VendingMachine vm = net as VendingMachine;
                if (net == null || vm == null)
                    vms.Remove(k);
            }
            Interface.GetMod().DataFileSystem.WriteObject("VendingManagerVMs", vms);
        }

        // save locks data to file
        void SaveLocksData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("VendingManagerLocks", locks);
        }

        // get value from config (handles type conversion)
        T GetConfig<T>(string group, string name, T value)
        {
            if (Config[group, name] == null)
            {
                Config[group, name] = value;
                SaveConfig();
            }
            return (T)Convert.ChangeType(Config[group, name], typeof(T));
        }

        // validate configuration
        bool CheckConfig()
        {
            bool dirty = false;
            foreach (Option option in Enum.GetValues(typeof(Option)))
                if (!data.config.ContainsKey(option))
                {
                    data.config[option] = defaults[(int)option];
                    dirty = true;
                }
            return dirty;
        }

        #endregion

        #region Hooks

        // set newly spawned vending machines to the value of lockable
        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity.GetType() == typeof(VendingMachine))
                Set(entity as VendingMachine, ConfigValue<bool>(Option.lockable), ConfigValue<float>(Option.health));
        }

        // block unauthorized lock deployment onto vending machines
        // only allow attachment from the rear, except if player is
        // the owner of the vending machine
        void OnItemDeployed(Deployer deployer, BaseEntity entity)
        {
            if (!ConfigValue<bool>(Option.lockable)) return;
            if (deployer == null || entity == null) return;
            BasePlayer player = deployer.GetOwnerPlayer();
            VendingMachine vm = entity as VendingMachine;
            if (vm == null || player == null) return;
            if (deployer.GetDeployable().slot == BaseEntity.Slot.Lock && !(vm.CanPlayerAdmin(player) || player.userID == vm.OwnerID))
            {
                BaseLock lockEntity = vm.GetSlot(BaseEntity.Slot.Lock) as BaseLock;
                if (lockEntity == null) return;
                deployer.GetItem().amount++;
                lockEntity.Kill();
                if (ConfigValue<bool>(Option.lockFailureMessage))
                    SendMessage(player, "NotAuthorized");
            }
        }

        // handle blocking broadcasting
        void OnToggleVendingBroadcast(VendingMachine vm, BasePlayer player)
        {
            if(ConfigValue<bool>(Option.noBroadcast))
            {
                vm.SetFlag(BaseEntity.Flags.Reserved4, false, false);
                SendMessage(player, "NoBroadcast");
            }
        }

        object OnVendingTransaction(VendingMachine vm, BasePlayer player, int sellOrderId, int numTransactions)
        {
            VendingMachineInfo i;
            vms.TryGetValue(vm.net.ID, out i);
            bool bottomless = i == null ? false : i.HasFlag(VendingMachineInfo.VMFlags.Bottomless);

            bool log = ConfigValue<bool>(Option.logTransSuccess) || ConfigValue<bool>(Option.logTransFailure) || (i != null && i.HasFlag(VendingMachineInfo.VMFlags.LogTransactions));
            bool force = i != null && i.HasFlag(VendingMachineInfo.VMFlags.LogTransactions);
            ProtoBuf.VendingMachine.SellOrder sellOrder = vm.sellOrders.sellOrders[sellOrderId];

            bool isCurrencySellOrder = (useEconomics || useServerRewards) && sellOrder.currencyID == currencyItem.itemid;
            bool isCurrencyBuyOrder = (useEconomics || useServerRewards) && sellOrder.itemToSellID == currencyItem.itemid;

            LogEntry logEntry = new LogEntry();
            if (log)
            {
                logEntry.id = vm.net.ID;
                logEntry.playerID = player.userID;
                logEntry.playerName = player.displayName;
            }

            List<Item> items = vm.inventory.FindItemsByItemID(sellOrder.itemToSellID);
            if (sellOrder.itemToSellIsBP)
            {
                items = (
                    from x in vm.inventory.FindItemsByItemID(vm.blueprintBaseDef.itemid)
                    where x.blueprintTarget == sellOrder.itemToSellID
                    select x).ToList();
            }
            if (items == null || items.Count == 0)
            {
                return false;
            }
            int numberOfTransactions = Mathf.Clamp(numTransactions, 1, (!items[0].hasCondition ? 1000000 : 1));
            int sellCount = sellOrder.itemToSellAmount * numberOfTransactions;
            int buyCount = sellOrder.currencyAmountPerItem * numberOfTransactions;

            if (sellCount > items.Sum(x => x.amount))
                return false;

            int cost = 0;
            if (!isCurrencySellOrder)
            {
                int num2 = sellOrder.currencyAmountPerItem * numberOfTransactions;

                if (log) logEntry.cost = num2 + " " + ItemManager.FindItemDefinition(sellOrder.currencyID).displayName.translated + (sellOrder.currencyIsBP ? " (BP)" : "");

                List<Item> items1 = player.inventory.FindItemIDs(sellOrder.currencyID);
                if (sellOrder.currencyIsBP)
                {
                    items1 = (
                        from x in player.inventory.FindItemIDs(vm.blueprintBaseDef.itemid)
                        where x.blueprintTarget == sellOrder.currencyID
                        select x).ToList();
                }
                if (items1.Count == 0)
                {
                    if (log)
                    {
                        logEntry.success = false;
                        logEntry.reason = LogEntry.FailureReason.NoItems;
                        LogTransaction(logEntry, force);
                    }
                    return false;
                }

                int num1 = items1.Sum(x => x.amount);
                if (num1 < num2)
                {
                    if (log)
                    {
                        logEntry.success = false;
                        logEntry.reason = LogEntry.FailureReason.NoItems;
                        LogTransaction(logEntry, force);
                    }
                    return false;
                }

                vm.transactionActive = true;
                int num3 = 0;
                Item item;
                foreach (Item item2 in items1)
                {
                    int num4 = Mathf.Min(num2 - num3, item2.amount);
                    item = (item2.amount > num4 ? item2.SplitItem(num4) : item2);
                    if(bottomless)
                        item.Remove();
                    else
                        if (!item.MoveToContainer(vm.inventory, -1, true))
                        {
                            item.Drop(vm.inventory.dropPosition, Vector3.zero, new Quaternion());
                        }
                    num3 = num3 + num4;
                    if (num3 < num2)
                        continue;
                    break;
                }
            }
            else
            {
                cost = sellOrder.currencyAmountPerItem * numberOfTransactions;
                if (log)
                {
                    logEntry.isBuyOrder = true;
                    logEntry.cost = string.Format("{0:C}", cost);
                }
                double money = GetBalance(player.userID);
                if (money < 1.0)
                {
                    if (log)
                    {
                        logEntry.success = false;
                        logEntry.reason = LogEntry.FailureReason.NoMoney;
                        LogTransaction(logEntry, force);
                    }
                    return false;
                }

                if (Mathf.FloorToInt((float)money) < cost)
                {
                    if (log)
                    {
                        logEntry.success = false;
                        logEntry.reason = LogEntry.FailureReason.NoMoney;
                        LogTransaction(logEntry, force);
                    }
                    SendMessage(player, currencyPlugin + "NotEnoughMoney");
                    return false;
                }

                vm.transactionActive = true;
                bool success = false;
                if (bottomless)
                    success = Withdraw(player.userID, cost);
                else
                    success = Transfer(player.userID, vm.OwnerID, cost);

                if(!success)
                {
                    if (log)
                    {
                        logEntry.success = false;
                        logEntry.reason = LogEntry.FailureReason.Unknown;
                        LogTransaction(logEntry, force);
                    }
                    SendMessage(player, currencyPlugin + "TransferFailed");
                    vm.transactionActive = false;
                    return false;
                }
            }
            int amount = 0;
            if (isCurrencyBuyOrder)
            {
                amount = sellOrder.itemToSellAmount * numberOfTransactions;
                if (log)
                {
                    logEntry.isBuyOrder = false;
                    logEntry.cost = string.Format("{0:C}", amount);
                    logEntry.bought = sellOrder.currencyAmountPerItem + " " + ItemManager.FindItemDefinition(sellOrder.currencyID).displayName.translated;
                }

                double money = GetBalance(vm.OwnerID);
                if (!bottomless)
                {
                    if (money < 1.0)
                    {
                        if (log)
                        {
                            logEntry.success = false;
                            logEntry.reason = LogEntry.FailureReason.NoMoney;
                            LogTransaction(logEntry, force);
                        }
                        return false;
                    }

                    if (Mathf.FloorToInt((float)money) < amount)
                    {
                        if (log)
                        {
                            logEntry.success = false;
                            logEntry.reason = LogEntry.FailureReason.NoMoney;
                            LogTransaction(logEntry, force);
                        }
                        SendMessage(player, currencyPlugin + "NotEnoughMoneyOwner");
                        return false;
                    }
                }

                vm.transactionActive = true;
                bool success = false;
                if (bottomless)
                    success = Deposit(player.userID, amount);
                else
                    success = Transfer(vm.OwnerID, player.userID, amount);

                if (!success)
                {
                    if (log)
                    {
                        logEntry.success = false;
                        logEntry.reason = LogEntry.FailureReason.Unknown;
                        LogTransaction(logEntry, force);
                    }
                    SendMessage(player, currencyPlugin + "TransferFailed");
                    vm.transactionActive = false;
                    return false;
                }
            }
            else
            {
                if (log) logEntry.bought = sellOrder.itemToSellAmount + " " + ItemManager.FindItemDefinition(sellOrder.itemToSellID).displayName.translated + (sellOrder.itemToSellIsBP ? " (BP)" : "");
                if (!bottomless)
                {
                    int num5 = 0;
                    Item item1 = null;
                    foreach (Item item3 in items)
                    {
                        item1 = (item3.amount > sellCount ? item3.SplitItem(sellCount) : item3);
                        num5 = num5 + item1.amount;
                        player.GiveItem(item1, BaseEntity.GiveItemReason.PickedUp);
                        if (num5 < sellCount)
                            continue;
                        break;
                    }
                }
                else
                {
                    Item item = null;
                    if (sellOrder.itemToSellIsBP)
                    {
                        item = ItemManager.CreateByItemID(vm.blueprintBaseDef.itemid, sellCount);
                        item.blueprintTarget = sellOrder.itemToSellID;
                    }
                    else
                        item = ItemManager.CreateByItemID(sellOrder.itemToSellID, sellCount, vm.inventory.FindItemsByItemID(sellOrder.itemToSellID).Select(e => e.skin).FirstOrDefault());
                    player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
                }
            }

            vm.UpdateEmptyFlag();
            vm.transactionActive = false;

            if (ConfigValue<bool>(Option.transMessages) && isCurrencySellOrder && cost > 0 )
            {
                double remaining = GetBalance(player.userID);
                SendMessage(player, currencyPlugin + "PurchaseSuccess", new object[] { sellCount, ItemManager.FindItemDefinition(sellOrder.itemToSellID).displayName.translated, cost, remaining });
            }
            else if(ConfigValue<bool>(Option.transMessages) && isCurrencyBuyOrder && amount > 0)
            {
                double balance = GetBalance(player.userID);
                SendMessage(player, currencyPlugin + "SellSuccess", new object[] { buyCount, ItemManager.FindItemDefinition(sellOrder.currencyID).displayName.translated, amount, balance });
            }
            if(log)
            {
                logEntry.success = true;
                LogTransaction(logEntry, force);
            }

            return true;
        }

        // override administration if restricted access on
        object CanAdministerVending(VendingMachine vm, BasePlayer player)
        {
            bool restricted = ConfigValue<bool>(Option.restricted);
            if(!restricted)
            {
                VendingMachineInfo i;
                if (vms.TryGetValue(vm.net.ID, out i))
                    restricted = i.HasFlag(VendingMachineInfo.VMFlags.Restricted);
            }
            if (restricted && vm.OwnerID != player.userID && !player.IsAdmin)
            {
                SendMessage(player, "Restricted");
                return false;
            }
            return null;
        }

        // block damage for Immortal vending machines
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (entity == null || hitinfo == null) return;
            if(entity is VendingMachine)
            {
                VendingMachineInfo i;
                if(vms.TryGetValue(entity.net.ID, out i))
                {
                    if (i.HasFlag(VendingMachineInfo.VMFlags.Immortal))
                        hitinfo.damageTypes = new DamageTypeList();
                }
            }
        }

        // hack to show vending buttons - when VM shop opened, add currency items
        // to hidden inventory slot to represent current economics balance
        void OnOpenVendingShop(VendingMachine vm, BasePlayer player)
        {
            if (!useEconomics && !useServerRewards) return;
            if (vm.sellOrders.sellOrders.Count == 0) return;

            bool hasCurrencySellOrder = false;
            bool hasCurrencyBuyOrder = true;
            // create and add items to player inventory to prevent "Can't Afford" button
            foreach (ProtoBuf.VendingMachine.SellOrder so in vm.sellOrders.sellOrders)
            {
                if (so.currencyID == currencyItem.itemid)
                    hasCurrencySellOrder = true;
                else if (so.itemToSellID == currencyItem.itemid)
                    hasCurrencyBuyOrder = true;
            }
            if (!hasCurrencySellOrder && !hasCurrencyBuyOrder) return;

            int playerMoney = 0;
            if (hasCurrencyBuyOrder)
            {
                vm.inventory.capacity = currencyIndex + 1;
                VendingMachineInfo i;
                vms.TryGetValue(vm.net.ID, out i);
                bool bottomless = i == null ? false : i.HasFlag(VendingMachineInfo.VMFlags.Bottomless);
                if (bottomless)
                {
                    Item money = ItemManager.CreateByItemID(currencyItem.itemid, 10000, 0);
                    money.MoveToContainer(vm.inventory, currencyIndex, true);
                }
                else
                {
                    playerMoney = Mathf.FloorToInt((float)GetBalance(vm.OwnerID));
                    if (playerMoney > 0)
                    {
                        Item money = ItemManager.CreateByItemID(currencyItem.itemid, playerMoney);
                        money.MoveToContainer(vm.inventory, currencyIndex, true);
                    }
                }
                /*
                int lastMoney = playerMoney;
                transactionTimers[player.userID] = timer.Every(0.5f, () => {
                    int m = Mathf.FloorToInt((float)GetBalance(vm.OwnerID));
                    if (lastMoney != m)
                    {
                        lastMoney = m;
                        Item item = vm.inventory.GetSlot(currencyIndex);
                        if (item != null)
                        {
                            if (lastMoney == 0)
                                item.Remove();
                            else
                                item.amount = lastMoney;
                            vm.RefreshSellOrderStockLevel();
                        }
                    }
                });
                */
            }
            playerMoney = 0;
            if(hasCurrencySellOrder)
            {
                player.inventory.containerMain.capacity = currencyIndex + 1;
                playerMoney = Mathf.FloorToInt((float)GetBalance(player.userID));
                if (playerMoney > 0)
                {
                    Item money = ItemManager.CreateByItemID(currencyItem.itemid, playerMoney, 0);
                    money.MoveToContainer(player.inventory.containerMain, currencyIndex, true);
                }

                int lastMoney = playerMoney;
                transactionTimers[player.userID] = timer.Every(0.5f, () => {
                    int m = Mathf.FloorToInt((float)GetBalance(player.userID));
                    if (lastMoney != m)
                    {
                        if(lastMoney == 0 && m > 0)
                        {
                            Item money = ItemManager.CreateByItemID(currencyItem.itemid, m, 0);
                            money.MoveToContainer(player.inventory.containerMain, currencyIndex, true);
                        }
                        lastMoney = m;
                        Item item = player.inventory.containerMain.GetSlot(currencyIndex);
                        if (item != null)
                        {
                            if (lastMoney == 0)
                                item.Remove();
                            else
                                item.amount = lastMoney;
                            player.inventory.SendSnapshot();
                        }
                    }
                });
            }
            if (ConfigValue<float>(Option.transactionTimeout) > 0f)
                timeoutTimers[player.userID] = timer.Once(ConfigValue<float>(Option.transactionTimeout), () => player.EndLooting());
        }

        // when VM shop closed, remove all currency items from player's inventory
        void OnLootEntityEnd(BasePlayer player, BaseEntity entity)
        {
            if ((!useEconomics && !useServerRewards) || entity == null || !(entity is VendingMachine)) return;
            if(transactionTimers.ContainsKey(player.userID))
                transactionTimers[player.userID]?.Destroy();
            if (timeoutTimers.ContainsKey(player.userID))
                timeoutTimers[player.userID]?.Destroy();

            int i = player.inventory.containerMain.capacity;
            while (i >= currencyIndex)
                player.inventory.containerMain.GetSlot(i--)?.Remove();
            Item b = player.inventory.containerMain.FindItemByItemID(currencyItem.itemid);
            if (b != null) b.Remove();
            player.inventory.containerMain.capacity = currencyIndex;

            VendingMachine vm = entity as VendingMachine;
            int j = vm.inventory.capacity;
            while (j >= currencyIndex)
                vm.inventory.GetSlot(j--)?.Remove();
            Item c = vm.inventory.FindItemByItemID(currencyItem.itemid);
            if (c != null) c.Remove();
            vm.inventory.capacity = currencyIndex;
        }

        // on rotate, send network update for lock position
        void OnRotateVendingMachine(VendingMachine vm, BasePlayer player)
        {
            BaseLock l = vm.GetSlot(BaseEntity.Slot.Lock) as BaseLock;
            if(l != null)
                NextTick(() => l.SendNetworkUpdate());
        }

        #endregion

        #region Command Handling

        // command delegator
        void CommandDelegator(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, PermCanUse))
            {
                SendReply(player, GetMessage("NotAllowed", player.UserIDString));
                return;
            }
            string message = "InvalidCommand";
            // assume args[0] is the command (beyond /vm)
            if (args != null && args.Length > 0)
                command = args[0];
            // shift arguments
            if (args != null)
            {
                if (args.Length > 1)
                    args = args.Skip(1).ToArray();
                else
                    args = new string[] { };
            }
            object[] opts = new object[] { command };
            if (Enum.IsDefined(typeof(Command), command))
            {
                switch ((Command)Enum.Parse(typeof(Command), command))
                {
                    case Command.add:
                        HandleLoad(player, args, false, out message, out opts);
                        break;
                    case Command.clear:
                        HandleClear(player, out message);
                        break;
                    case Command.eject:
                        if (hasPermission(player, PermCanEject))
                            EjectAll(out message, out opts);
                        break;
                    case Command.info:
                        HandleInfo(player, out message, out opts);
                        break;
                    case Command.list:
                        HandleList(out message, out opts);
                        break;
                    case Command.load:
                        HandleLoad(player, args, true, out message, out opts);
                        break;
                    case Command.reset:
                        HandleReset(args, out message);
                        break;
                    case Command.save:
                        HandleSave(player, args, out message, out opts);
                        break;
                    case Command.set:
                        HandleSet(player, args, out message, out opts);
                        break;
                    case Command.unset:
                        HandleSet(player, args, out message, out opts, true);
                        break;
                    default:
                        break;
                }
            }
            else
                ShowCommands(out message, out opts);
            if (message != null && message != "")
                SendMessage(player, message, opts);
        }

        // handle reset command
        void HandleReset(string[] args, out string message)
        {
            bool confirm = (args.Length > 0 && args[0] != null && args[0].ToLower() == "confirm");
            if (confirm)
            {
                Config.Clear();
                data = new VendingData();
                SaveData();
                message = "ResetSuccess";
            }
            else
                message = "ConfirmReset";
        }

        // handle clear command
        void HandleClear(BasePlayer player, out string message)
        {
            message = "VMNotFound";
            object entity;
            if (GetRaycastTarget(player, out entity))
                if (entity != null && entity is VendingMachine)
                {
                    (entity as VendingMachine).sellOrders.sellOrders.Clear();
                    message = "ClearSuccess";
                }
        }

        void HandleInfo(BasePlayer player, out string message, out object[] opts)
        {
            message = "VMNotFound";
            opts = new object[] { };
            object entity;
            if (GetRaycastTarget(player, out entity))
                if (entity != null && entity is VendingMachine)
                {
                    uint id = (entity as VendingMachine).net.ID;
                    bool isConfigured = vms.ContainsKey(id);
                    string flags = "None";
                    if (isConfigured)
                        flags = vms[id].flags.ToString();
                    message = "Information";
                    opts = new object[] { id, isConfigured, flags };
                }
        }

        // handle load command
        void HandleLoad(BasePlayer player, string[] args, bool replaceAll, out string message, out object[] opts)
        {
            message = "";
            opts = new object[] { };
            if (args == null || args.Length == 0 || args[0] == null || args[0] == "")
            {
                message = "InvalidParameter";
                return;
            }
            object entity;
            if (!GetRaycastTarget(player, out entity))
            {
                message = "VMNotFound";
                return;
            }

            opts = new object[] { args[0] };
            if (entity != null && entity is VendingMachine)
                LoadSellOrders(entity as VendingMachine, args[0], replaceAll, out message);
        }

        // handle loading the sell orders into a vending machine
        void LoadSellOrders(VendingMachine vm, string templateName, bool replace, out string message)
        {
            message = "LoadSuccess";
            if (!data.templates.ContainsKey(templateName))
            {
                message = "TemplateNotFound";
                return;
            }
            if (data.templates[templateName].Empty())
            {
                message = "EmptyTemplate";
                return;
            }
            if (replace)
                vm.sellOrders.sellOrders.Clear();
            foreach (SellOrderEntry e in data.templates[templateName].entries)
            {
                ProtoBuf.VendingMachine.SellOrder o = new ProtoBuf.VendingMachine.SellOrder();
                o.itemToSellID = ItemManager.FindItemDefinition(e.itemToSellName).itemid;
                o.itemToSellAmount = e.itemToSellAmount;
                o.itemToSellIsBP = e.itemToSellIsBP;
                o.currencyID = ItemManager.FindItemDefinition(e.currencyName).itemid;
                o.currencyAmountPerItem = e.currencyAmountPerItem;
                o.currencyIsBP = e.currencyIsBP;
                vm.sellOrders.sellOrders.Add(o);
            }
            vm.RefreshSellOrderStockLevel();
            return;
        }

        // handle save command
        void HandleSave(BasePlayer player, string[] args, out string message, out object[] opts)
        {
            message = "";
            opts = new object[] { };
            if (args == null || args.Length == 0 || args[0] == null || args[0] == "")
            {
                message = "InvalidParameter";
                return;
            }
            bool overwrite = (args.Length > 1 && args[1] != null && args[1].ToLower() == "overwrite");
            object entity;
            if (!GetRaycastTarget(player, out entity))
            {
                message = "VMNotFound";
                return;
            }
            opts = new object[] { args[0] };
            if (entity != null && entity is VendingMachine)
                SaveSellOrders(entity as VendingMachine, args[0], out message, overwrite);
        }

        // handle saving the sell orders from a vending machine
        void SaveSellOrders(VendingMachine vm, string templateName, out string message, bool overwrite = false)
        {
            message = "SaveSuccess";
            if (templateName == null || templateName == "")
            {
                message = "InvalidParameter";
                return;
            }
            if (data.templates.ContainsKey(templateName) && !overwrite)
            {
                message = "TemplateExists";
                return;
            }

            ProtoBuf.VendingMachine.SellOrderContainer sellOrderContainer = vm.sellOrders;
            if (sellOrderContainer == null || sellOrderContainer.sellOrders == null || sellOrderContainer.sellOrders.Count == 0)
            {
                message = "EmptyVM";
                return;
            }
            SellOrderTemplate template = new SellOrderTemplate();
            template.PopulateTemplate(sellOrderContainer.sellOrders);
            if (!template.Empty())
            {
                data.templates[templateName] = template;
                SaveData();
            }
            return;
        }

        // handle set/unset flag
        void HandleSet(BasePlayer player, string[] args, out string message, out object[] opts, bool unset = false)
        {
            message = unset ? "UnsetSuccess" : "SetSuccess";
            opts = new object[] { };
            if (args == null || args.Length == 0 || args[0] == null || args[0] == "" || !Enum.IsDefined(typeof(VendingMachineInfo.VMFlags), args[0]))
            {
                message = "InvalidParameter";
                return;
            }
            object entity;
            if (!GetRaycastTarget(player, out entity))
            {
                message = "VMNotFound";
                return;
            }
            opts = new object[] { args[0] };
            if (entity != null && entity is VendingMachine)
            {
                VendingMachineInfo.VMFlags flags = (VendingMachineInfo.VMFlags)Enum.Parse(typeof(VendingMachineInfo.VMFlags), args[0]);
                VendingMachineInfo i;
                if(!vms.TryGetValue((entity as VendingMachine).net.ID, out i))
                {
                    i = new VendingMachineInfo();
                    i.id = (entity as VendingMachine).net.ID;
                    vms[i.id] = i;
                }
                if (unset)
                    i.flags &= ~flags;
                else
                    i.flags |= flags;
                if (i.flags == VendingMachineInfo.VMFlags.None)
                    vms.Remove(i.id);
                SaveVendingMachineData();
            }
        }

        #endregion

        #region Messaging

        // send reply to a player
        void SendMessage(BasePlayer player, string message, object[] options = null)
        {
            string msg = GetMessage(message, player.UserIDString);
            if (options != null && options.Length > 0)
                msg = String.Format(msg, options);
            SendReply(player, GetMessage("Prefix", player.UserIDString) + msg);
        }

        // handle list command
        void HandleList(out string message, out object[] opts)
        {
            message = "TemplateList";
            opts = new object[] { data.GetTemplateList() };
        }

        // show list of valid commands
        void ShowCommands(out string message, out object[] opts)
        {
            message = "CommandList";
            opts = new object[] { string.Join(", ", Enum.GetValues(typeof(Command)).Cast<Command>().Select(x => x.ToString()).ToArray()) };
        }

        void LogTransaction(LogEntry logEntry, bool force = false)
        {
            if ((ConfigValue<bool>(Option.logTransSuccess) && logEntry.success) || (ConfigValue<bool>(Option.logTransFailure) && !logEntry.success) || force)
            {
                string logString = logEntry.ToString();
                LogToFile("Transactions", logString, this, true);
            }
        }

        #endregion

        #region Helper Procedures

        // set all vending machines
        void SetAll(bool lockable, float health = defaultHealth)
        {
            foreach (VendingMachine vm in GameObject.FindObjectsOfType(typeof(VendingMachine)))
                Set(vm, lockable, health);
        }

        // setup a specific vending machine
        void Set(VendingMachine vm, bool lockable, float health = defaultHealth, bool restoreProtection = false)
        {
            if (ConfigValue<bool>(Option.noBroadcast))
                vm.SetFlag(BaseEntity.Flags.Reserved4, false, false);
            if (defaultProtection == null)
            {
                defaultProtection = vm.baseProtection;
                if (data.resistances == null)
                {
                    data.SetResistances(defaultProtection.amounts);
                    SaveData();
                }
            }
            else
            {
                if (customProtection == null)
                    customProtection = UnityEngine.Object.Instantiate(vm.baseProtection) as ProtectionProperties;
                if (data.resistances != null && !restoreProtection)
                {
                    customProtection.amounts = data.GetResistances();
                    vm.baseProtection = customProtection;
                    //vm.baseProtection.amounts = data.GetResistances();
                }
                if (restoreProtection)
                    vm.baseProtection = defaultProtection;
            }
            if (!lockable && ConfigValue<bool>(Option.ejectLocks)) Eject(vm);
            vm.isLockable = lockable;
            if (ConfigValue<bool>(Option.setHealth))
            {
                float h = health * vm.healthFraction;
                vm.InitializeHealth(h, health);
                vm.SendNetworkUpdate();
            }
        }

        // eject lock from vending machine
        bool Eject(VendingMachine m)
        {
            BaseEntity lockEntity = m.GetSlot(BaseEntity.Slot.Lock);
            if (lockEntity != null && lockEntity is BaseLock)
            {
                Item lockItem = ItemManager.Create((lockEntity as BaseLock).itemType, 1, lockEntity.skinID);
                lockEntity.Kill();
                lockItem.Drop(m.GetDropPosition(), m.GetDropVelocity(), m.transform.rotation);
                m.isLockable = ConfigValue<bool>(Option.lockable);
                return true;
            }
            return false;
        }

        // eject locks from all vending machines
        void EjectAll(out string message, out object[] opts)
        {
            int counter = 0;
            foreach (VendingMachine m in GameObject.FindObjectsOfType(typeof(VendingMachine)))
                if (Eject(m)) counter++;

            message = "Ejected";
            opts = new object[] { counter };
        }

        // raycast to find entity being looked at
        bool GetRaycastTarget(BasePlayer player, out object closestEntity)
        {
            closestEntity = null;
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 5f))
                return false;
            closestEntity = hit.GetEntity();
            return true;
        }

        // check if player is an admin
        private static bool isAdmin(BasePlayer player)
        {
            if (player?.net?.connection == null) return true;
            return player.net.connection.authLevel > 0;
        }

        // check if player has permission or is an admin
        private bool hasPermission(BasePlayer player, string permname)
        {
            //return isAdmin(player) || permission.UserHasPermission(player.UserIDString, permname);
            return permission.UserHasPermission(player.UserIDString, permname);
        }

        // get config value and convert type
        T ConfigValue<T>(Option option)
        {
            return (T)Convert.ChangeType(data.config[option], typeof(T));
        }

        // save all locks
        void SaveVMsAndLocks()
        {
            locks.Clear();
            foreach (VendingMachine vm in GameObject.FindObjectsOfType(typeof(VendingMachine)))
            {
                BaseLock l = (BaseLock)vm.GetSlot(BaseEntity.Slot.Lock);
                if (l == null) continue;

                LockInfo li = new LockInfo(vm.net.ID, l);
                locks[vm.net.ID] = li;
            }
            SaveVendingMachineData();
            SaveLocksData();
        }

        // load all locks
        void LoadLocks()
        {
            foreach(LockInfo li in locks.Values)
            {
                VendingMachine vm = (VendingMachine) BaseNetworkable.serverEntities.Find(li.vmId);
                if (vm == null) continue;
                if (vm.GetSlot(BaseEntity.Slot.Lock) != null) continue;

                BaseLock l;
                if (li.isCodeLock)
                    l = (CodeLock)GameManager.server.CreateEntity(CodeLockPrefab);
                else
                    l = (KeyLock)GameManager.server.CreateEntity(KeyLockPrefab);

                if (l == null) continue;

                l.gameObject.Identity();
                li.ToLock(ref l);
                l.SetParent(vm, vm.GetSlotAnchorName(BaseEntity.Slot.Lock));
                l.OnDeployed(vm);
                l.Spawn();
                vm.SetSlot(BaseEntity.Slot.Lock, l);
            }
        }

        // Destroy all attached locks on shutdown
        void DestroyLocks()
        {
            foreach (VendingMachine vm in GameObject.FindObjectsOfType(typeof(VendingMachine)))
            {
                BaseEntity l;
                if ((l = vm.GetSlot(BaseEntity.Slot.Lock)) != null)
                    l.Kill();
            }
        }

        double GetBalance(ulong playerId)
        {
            if(useEconomics)
            {
                return (double) Economics.CallHook("Balance", playerId.ToString());
            }
            else if(useServerRewards)
            {
                return (int) (ServerRewards.CallHook("CheckPoints", playerId) ?? 0.0);
            }
            return 0.0;
        }

        bool Withdraw(ulong playerId, double amount)
        {
            if (useEconomics)
            {
                return (bool) Economics.CallHook("Withdraw", playerId.ToString(), amount);
            }
            else if (useServerRewards)
            {
                return (bool) ServerRewards.CallHook("TakePoints", playerId, (int)amount);
            }
            return false;
        }

        bool Deposit(ulong playerId, double amount)
        {
            if (useEconomics)
            {
                Economics.CallHook("Deposit", playerId.ToString(), amount);
                return true;
            }
            else if (useServerRewards)
            {
                return (bool) ServerRewards.Call("AddPoints", new object[] { playerId, (int)amount });
            }
            return false;
        }

        bool Transfer(ulong fromId, ulong toId, double amount)
        {
            if (useEconomics)
            {
                return (bool) Economics.CallHook("Transfer", fromId.ToString(), toId.ToString(), amount);
            }
            else if (useServerRewards)
            {
                if (Withdraw(fromId, amount))
                {
                    bool result = Deposit(toId, amount);
                    if (!result)
                        Deposit(fromId, amount); // if transfer failed, refund
                    return result;
                }
            }
            return false;
        }

        #endregion

        #region Subclasses

        // config/data container
        class VendingData
        {
            public Dictionary<Option, object> config = new Dictionary<Option, object>();
            public Dictionary<DamageType, float> resistances;
            public Dictionary<string, SellOrderTemplate> templates;

            public string GetTemplateList() {
                string list = string.Join(", ", templates.Keys.ToArray());
                if (list == null || list == "")
                    list = "(empty)";
                return list;
            }

            public void SetResistances(float[] amounts)
            {
                resistances = new Dictionary<DamageType, float>();
                for (int i = 0; i < amounts.Length; i++)
                    resistances[(DamageType)i] = amounts[i];
            }

            public float[] GetResistances()
            {
                float[] values = new float[22];
                if (resistances != null)
                    foreach (KeyValuePair<DamageType, float> entry in resistances)
                        values[(int)entry.Key] = entry.Value;
                return values;
            }
        }

        // helper class for building sell order entries
        class SellOrderTemplate
        {
            public List<SellOrderEntry> entries = new List<SellOrderEntry>();

            public void PopulateTemplate(List<ProtoBuf.VendingMachine.SellOrder> sellOrders)
            {
                if (sellOrders == null) return;
                foreach (ProtoBuf.VendingMachine.SellOrder o in sellOrders)
                    AddSellOrder(o);
            }

            public void AddSellOrder(ProtoBuf.VendingMachine.SellOrder o)
            {
                if (o == null) return;
                SellOrderEntry e = new SellOrderEntry();
                e.itemToSellName = ItemManager.FindItemDefinition(o.itemToSellID).shortname;
                e.itemToSellAmount = o.itemToSellAmount;
                e.currencyName = ItemManager.FindItemDefinition(o.currencyID).shortname;
                e.currencyAmountPerItem = o.currencyAmountPerItem;
                e.itemToSellIsBP = o.itemToSellIsBP;
                e.currencyIsBP = o.currencyIsBP;
                entries.Add(e);
            }

            public bool Empty()
            {
                return (entries == null || entries.Count == 0);
            }
        }

        // simple sell order entry container
        struct SellOrderEntry
        {
            public string itemToSellName;
            public int itemToSellAmount;
            public string currencyName;
            public int currencyAmountPerItem;
            public bool itemToSellIsBP;
            public bool currencyIsBP;
        }

        struct LogEntry
        {
            public enum FailureReason { NoMoney, NoItems, Unknown }
            public uint id;
            public ulong playerID;
            public string playerName;
            public string bought;
            public string cost;
            public bool success;
            public bool isBuyOrder;
            public FailureReason reason;

            public override string ToString()
            {
                if(isBuyOrder)
                    return "VM " + id + ": " + playerName + " [" + playerID + "] " + (success ? "bought " : "failed to buy ") + bought + " for " + cost + (success ? "" : " - Reason: " + GetReason());
                else
                    return "VM " + id + ": " + playerName + " [" + playerID + "] " + (success ? "sold " : "failed to sell ") + bought + " for " + cost + (success ? "" : " - Reason: " + GetReason());
            }
            string GetReason()
            {
                if (reason == FailureReason.NoItems)
                    return "Not enough currency items";
                if (reason == FailureReason.NoMoney)
                    return "Not enough money";
                return "Unknown reason";
            }
        }

        class VendingMachineInfo
        {
            [Flags]
            public enum VMFlags {
                None            = 0,
                Bottomless        = 1,
                Immortal        = 1 << 1,
                Restricted        = 1 << 2,
                LogTransactions    = 1 << 3
            }
            public uint id;
            [JsonConverter(typeof(StringEnumConverter))]
            public VMFlags flags;
            public bool HasFlag(VMFlags flag) => (flags & flag) == flag;
        }

        // Lock details container
        class LockInfo
        {
            static readonly byte[] entropy = new byte[] { 11, 7, 5, 3 };
            public uint vmId;
            public bool isCodeLock = false;
            public string codeEncrypted;
            [JsonIgnore]
            public string code
            {
                get {
                    return Shift(codeEncrypted, -4);
                }
                set {
                    codeEncrypted = Shift(value, 4);
                }
            }
            public string guestCodeEncrypted;
            [JsonIgnore]
            public string guestCode
            {
                get {
                    return Shift(guestCodeEncrypted, -4);
                }
                set {
                    guestCodeEncrypted = Shift(value, 4);
                }
            }
            public List<ulong> whitelist;
            public List<ulong> guests;
            public int keyCode;
            public bool firstKey;
            public bool isLocked;

            public LockInfo() { }
            public LockInfo(uint vmId, BaseLock l)
            {
                this.vmId = vmId;
                FromLock(l);
            }

            public void FromLock(BaseLock l)
            {
                if (l.GetType() == typeof(CodeLock))
                {
                    isCodeLock = true;
                    code = (l as CodeLock).code;
                    guestCode = (l as CodeLock).guestCode;
                    whitelist = (l as CodeLock).whitelistPlayers;
                    guests = (l as CodeLock).guestPlayers;
                }
                else if (l.GetType() == typeof(KeyLock))
                {
                    keyCode = (l as KeyLock).keyCode;
                    firstKey = (l as KeyLock).firstKeyCreated;
                }
                isLocked = l.IsLocked();
            }

            public void ToLock(ref BaseLock l)
            {
                if (l.GetType() == typeof(CodeLock))
                {
                    (l as CodeLock).code = code;
                    (l as CodeLock).guestCode = guestCode;
                    (l as CodeLock).whitelistPlayers = whitelist;
                    (l as CodeLock).guestPlayers = guests;
                }
                else if (l.GetType() == typeof(KeyLock))
                {
                    (l as KeyLock).keyCode = keyCode;
                    (l as KeyLock).firstKeyCreated = firstKey;
                }
                l.SetFlag(BaseEntity.Flags.Locked, isLocked);
            }

            // simple obfuscation for codes
            static string Shift(string source, int shift)
            {
                int maxChar = Convert.ToInt32(char.MaxValue);
                int minChar = Convert.ToInt32(char.MinValue);

                char[] buffer = source.ToCharArray();

                for (int i = 0; i < buffer.Length; i++)
                {
                    int shifted = Convert.ToInt32(buffer[i]) + (shift * entropy[i]);

                    if (shifted > maxChar)
                    {
                        shifted -= maxChar;
                    }
                    else if (shifted < minChar)
                    {
                        shifted += maxChar;
                    }

                    buffer[i] = Convert.ToChar(shifted);
                }

                return new string(buffer);
            }
        }

        #endregion
    }
}
