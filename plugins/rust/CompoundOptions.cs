using System.Collections.Generic;
using System.Linq;
using Rust.Ai;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Compound Options", "nivex/rever", "1.1.1")]
    [Description("Compound monument options")]
    class CompoundOptions : RustPlugin
    {
        #region Save data classes

        private class StorageData
        {
            public Dictionary<string, Order[]> VendingMachinesOrders { get; set; }
        }

        private class Order
        {
            public string _comment;
            public int sellId;
            public int sellAmount;
            public bool sellAsBP;
            public int currencyId;
            public int currencyAmount;
            public bool currencyAsBP;
        }

        #endregion

        #region Config and data

        private bool dataChanged;
        private StorageData data;
        private static bool disallowBanditNPC;
        private static bool disallowCompoundNPC;
        private static bool disableCompoundTurrets;
        private static bool disableCompoundTrigger;
        private static bool disableCompoundVendingMachines;
        private static bool allowCustomCompoundVendingMachines = true;

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
            SaveConfig();
        }

        public void LoadVariables()
        {
            CheckCfg("Disallow Bandit NPC", ref disallowBanditNPC);
            CheckCfg("Disallow Compound NPC", ref disallowCompoundNPC);
            CheckCfg("Disable Compound Turrets", ref disableCompoundTurrets);
            CheckCfg("Disable Compound SafeZone trigger", ref disableCompoundTrigger);
            CheckCfg("Disable Compound Vending Machines", ref disableCompoundVendingMachines);
            CheckCfg("Allow custom sell list for Compound vending machines (see in data)", ref allowCustomCompoundVendingMachines);
        }

        private void SaveData()
        {
            if (dataChanged)
            {
                Interface.Oxide.DataFileSystem.WriteObject(Name, data);
                dataChanged = false;
            }
        }

        private void Init()
        {
            Unsubscribe(nameof(OnEntitySpawned));
            LoadVariables();
        }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }

        #endregion

        #region Implementation

        private void KillNPCPlayer(BaseNetworkable entity)
        {
            if (entity == null || entity.IsDestroyed || !(entity is NPCPlayer)) return;

            var npcApex = entity.gameObject.GetComponent<NPCPlayerApex>();
            if (npcApex == null) return;

            var npcLocationType = npcApex?.AiContext?.AiLocationManager?.LocationType;
            if (npcLocationType == null) return;

            if (npcLocationType == AiLocationSpawner.SquadSpawnerLocation.Compound && disallowCompoundNPC)
            {
                entity.Kill(BaseNetworkable.DestroyMode.Gib);
            }
            if (npcLocationType == AiLocationSpawner.SquadSpawnerLocation.BanditTown && disallowBanditNPC)
            {
                entity.Kill(BaseNetworkable.DestroyMode.Gib);
            }
        }

        private void ProcessNPCTurret(BaseNetworkable entity)
        {
            if (entity == null || entity.IsDestroyed || !(entity is NPCAutoTurret)) return;
            var npcTurret = entity as NPCAutoTurret;
            npcTurret.SetFlag(NPCAutoTurret.Flags.On, !disableCompoundTurrets, !disableCompoundTurrets);
            npcTurret.UpdateNetworkGroup();
            npcTurret.SendNetworkUpdateImmediate();
        }

        private void AddVendingOrders(NPCVendingMachine vending)
        {
            if (vending == null || vending.IsDestroyed || data.VendingMachinesOrders.ContainsKey(vending.vendingOrders.name))
            {
                return;
            }
            List<Order> orders = new List<Order>();
            foreach (var order in vending.vendingOrders.orders)
            {
                orders.Add(new Order
                {
                    _comment = $"Sell {order.sellItem.displayName.english} x {order.sellItemAmount} for {order.currencyItem.displayName.english} x {order.currencyAmount}",
                    sellAmount = order.currencyAmount,
                    currencyAmount = order.sellItemAmount,
                    sellId = order.sellItem.itemid,
                    sellAsBP = order.sellItemAsBP,
                    currencyId = order.currencyItem.itemid
                });
            }
            data.VendingMachinesOrders.Add(vending.vendingOrders.name, orders.ToArray());
            Puts($"Added Vending Machine: {vending.vendingOrders.name} to data file!");
            dataChanged = true;
        }

        private void UpdateVending(NPCVendingMachine vending)
        {
            if (vending == null || vending.IsDestroyed)
            {
                return;
            }

            AddVendingOrders(vending);

            if (disableCompoundVendingMachines)
            {
                vending.ClearSellOrders();
                vending.inventory.Clear();
            }
            else if (allowCustomCompoundVendingMachines)
            {
                vending.vendingOrders.orders = GetNewOrders(vending);
                vending.InstallFromVendingOrders();
            }
        }

        private NPCVendingOrder.Entry[] GetNewOrders(NPCVendingMachine vending)
        {
            List<NPCVendingOrder.Entry> temp = new List<NPCVendingOrder.Entry>();
            foreach (var order in data.VendingMachinesOrders[vending.vendingOrders.name])
            {
                temp.Add(new NPCVendingOrder.Entry
                {
                    currencyAmount = order.sellAmount,
                    currencyAsBP = order.currencyAsBP,
                    currencyItem = ItemManager.FindItemDefinition(order.currencyId),
                    sellItem = ItemManager.FindItemDefinition(order.sellId),
                    sellItemAmount = order.currencyAmount,
                    sellItemAsBP = order.sellAsBP
                });
            }
            return temp.ToArray();
        }

        #endregion

        #region Oxide hooks

        private void Loaded()
        {
            try
            {
                data = Interface.Oxide.DataFileSystem.ReadObject<StorageData>(Name);
            }
            catch { }

            if (data == null)
            {
                data = new StorageData();
            }

            if (data.VendingMachinesOrders == null)
            {
                data.VendingMachinesOrders = new Dictionary<string, Order[]>();
            }
        }

        private void OnServerInitialized()
        {
            Subscribe(nameof(OnEntitySpawned));

            foreach(var entity in BaseNetworkable.serverEntities.ToList())
            {
                if (entity is NPCVendingMachine)
                {
                    UpdateVending(entity as NPCVendingMachine);
                }
                else if (entity is NPCPlayer)
                {
                    KillNPCPlayer(entity);
                }
                else if (entity is NPCAutoTurret)
                {
                    ProcessNPCTurret(entity);
                }
            }

            SaveData();
        }

        private void OnEntityEnter(TriggerBase trigger, BaseEntity entity)
        {
            if (!(trigger is TriggerSafeZone) && !(entity is BasePlayer)) return;
            var safeZone = trigger as TriggerSafeZone;
            if (safeZone == null) return;
            
            safeZone.enabled = !disableCompoundTrigger;
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity is NPCVendingMachine)
            {
                UpdateVending(entity as NPCVendingMachine);
                SaveData();
            }
            else if (entity is NPCPlayerApex)
            {
                KillNPCPlayer(entity);
            }
            else if (entity is NPCAutoTurret)
            {
                ProcessNPCTurret(entity);
            }
        }

        #endregion
    }
}