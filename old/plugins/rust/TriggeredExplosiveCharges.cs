using System;
using System.Collections.Generic;

using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Triggered Explosive Charges", "EnigmaticDragon", "1.0.19", ResourceId = 2383)]
    [Description("Adds the option to set off C4 manually without a timer")]
    public class TriggeredExplosiveCharges : RustPlugin
    {
        #region Constants
        private const string PERMISSION_PLACE = "triggeredexplosivecharges.place";
        private const string PERMISSION_NOTRIGGER = "triggeredexplosivecharges.notrigger";
        private const string PERMISSION_CRAFTING = "triggeredexplosivecharges.crafting";

        private const int TRIGGER_ITEM_ID = 1711033574; // bone.club
        private const ulong TRIGGER_SKIN_TIMED_MODE = 881325138;
        private const ulong TRIGGER_SKIN_TRIGGERD_MODE = 881319299;

        private const int C4_ITEM_ID = 1248356124;
        enum C4_MODE : ulong
        {
            TIMED = TRIGGER_SKIN_TIMED_MODE,
            TRIGGERED = TRIGGER_SKIN_TRIGGERD_MODE
        }
        #endregion

        #region Classes

        private class TriggeredExplosivesManager
        {
            #region Member Variables

            public static Dictionary<ulong, TriggeredExplosivesManager> allManagers
                = new Dictionary<ulong, TriggeredExplosivesManager>();
            private static Dictionary<uint, ulong> fakeC4_To_PlayerID = new Dictionary<uint, ulong>();

            private BasePlayer player;
            private List<TimedExplosive> triggeredExplosives;
            private List<DroppedItem> fakeExplosives;

            private ulong c4_mode;
            private Item trigger;

            #endregion

            public TriggeredExplosivesManager(BasePlayer player)
            {
                this.player = player;

                triggeredExplosives = new List<TimedExplosive>();
                fakeExplosives = new List<DroppedItem>();
                c4_mode = (ulong)C4_MODE.TIMED;
                trigger = null;

                List<uint> deployedExplosives;
                if (!TriggeredExplosiveCharges.saveData.deployedExplosives.TryGetValue(player.userID, out deployedExplosives))
                    saveData.deployedExplosives.Add(player.userID, new List<uint>());
                else
                {
                    for (int i = deployedExplosives.Count - 1; i > -1; i--)
                    {
                        BaseNetworkable bn = BaseNetworkable.serverEntities.Find(deployedExplosives[i]);
                        if (bn)
                        {
                            DroppedItem fakeC4 = bn.GetComponent<DroppedItem>();
                            if (fakeC4)
                            {
                                fakeC4_To_PlayerID[fakeC4.net.ID] = player.userID;
                                fakeC4.allowPickup = configuration.C4_ALLOW_PICKUP;
                                fakeExplosives.Add(fakeC4);
                            }
                            else
                                triggeredExplosives.Add(bn.GetComponent<TimedExplosive>());
                        }

                        else
                            deployedExplosives.RemoveAt(i);
                    }
                }
                SaveDataToFile();
            }

            public void DeployExplosive(TimedExplosive te)
            {
                if (c4_mode == (ulong)C4_MODE.TIMED || (configuration.BLOCK_IN_EVENTS && Instance.IsPlayingEvent(player)))
                    return;

                te.SetFuse(float.MaxValue);
                triggeredExplosives.Add(te);

                saveData.deployedExplosives[player.userID].Add(te.net.ID);
                SaveDataToFile();

                if (configuration.C4_BEEP_DURATION == -1)
                    return;

                Instance.timer.Once(configuration.C4_BEEP_DURATION, () =>
                {
                    if (te != null)
                        RealC4_To_FakeC4(te);
                });
            }
            public void Explode(bool forceExplode = false)
            {
                if (!forceExplode && trigger == null)
                    return;

                for (int i=fakeExplosives.Count-1; i > -1; i--)
                    FakeC4_To_RealC4(fakeExplosives[i]);

                if (configuration.TRIGGER_ONE_TIME_USE && triggeredExplosives.Count > 0)
                        trigger.Remove();

                Instance.timer.Once(0.1f, () =>
                {
                    foreach (TimedExplosive te in triggeredExplosives)
                        te.Explode();

                    triggeredExplosives.Clear();
                    saveData.deployedExplosives[player.userID].Clear();
                    SaveDataToFile();
                });

            }

            private void RealC4_To_FakeC4(TimedExplosive realC4)
            {
                DroppedItem worldModel = ItemManager.CreateByItemID(C4_ITEM_ID).Drop(realC4.transform.localPosition, Vector3.zero).GetComponent<DroppedItem>();

                if (realC4.GetParentEntity())
                {
                    worldModel.SetParent(realC4.GetParentEntity(), StringPool.closest);
                    worldModel.GetComponent<Rigidbody>().isKinematic = true;
                    worldModel.GetComponent<Rigidbody>().useGravity = false;               
                }

                //realC4.transform.localRotation = Quaternion.identity;
                worldModel.transform.localRotation = realC4.transform.localRotation * Quaternion.Euler(90, 0, 0); //realC4.transform.rotation;// * Quaternion.Euler(90, 0, 0);

                Instance.Puts("fc: " + worldModel.transform.forward.ToString());
                Instance.Puts("rc: " + realC4.transform.forward.ToString());

                worldModel.allowPickup = configuration.C4_ALLOW_PICKUP;
                worldModel.CancelInvoke((Action)Delegate.CreateDelegate(typeof(Action), worldModel, "IdleDestroy"));
                worldModel.item.amount = worldModel.item.info.stackable;

                BaseEntity.saveList.Remove(worldModel);

                fakeExplosives.Add(worldModel);
                triggeredExplosives.Remove(realC4);
                fakeC4_To_PlayerID[worldModel.net.ID] = player.userID;

                saveData.deployedExplosives[player.userID].Remove(realC4.net.ID);
                saveData.deployedExplosives[player.userID].Add(worldModel.net.ID);
                SaveDataToFile();

                realC4.Kill();
            }

            private void FakeC4_To_RealC4(DroppedItem fakeC4)
            {
                if (fakeC4 == null)
                    return;

                TimedExplosive realC4 = GameManager.server.CreateEntity("assets/prefabs/tools/c4/explosive.timed.deployed.prefab",
                    fakeC4.transform.localPosition, Quaternion.identity).GetComponent<TimedExplosive>();

                if (realC4 == null)
                {
                    saveData.deployedExplosives[player.userID].Remove(fakeC4.net.ID);
                    SaveDataToFile();
                    return;
                }

                realC4.transform.localRotation = fakeC4.transform.localRotation * Quaternion.Euler(-90, 0, 0);

                if (fakeC4.GetParentEntity() != null)
                {
                    realC4.GetComponent<Rigidbody>().isKinematic = true;
                    realC4.GetComponent<Rigidbody>().useGravity = false;

                    realC4.SetParent(fakeC4.GetParentEntity(), StringPool.closest);
                }
                else
                {
                    if (realC4.transform.forward.y > 0)
                        realC4.transform.rotation *= Quaternion.Euler(180, 0, 0);
                }

                realC4.Spawn();

                realC4.SetFuse(float.MaxValue);

                triggeredExplosives.Add(realC4);
                fakeExplosives.Remove(fakeC4);
                fakeC4_To_PlayerID.Remove(fakeC4.net.ID);

                saveData.deployedExplosives[player.userID].Remove(fakeC4.net.ID);
                saveData.deployedExplosives[player.userID].Add(realC4.net.ID);
                SaveDataToFile();

                fakeC4.Kill();
            }

            public void Toggle_C4_Mode(bool forceToggle = false)
            {
                if (trigger == null && !forceToggle)
                    return;

                c4_mode = (c4_mode == (ulong)C4_MODE.TIMED ? (ulong)C4_MODE.TRIGGERED : (ulong)C4_MODE.TIMED);
                UpdateTriggerSkin();
            }

            public void Reset_C4_Mode()
            {
                if (trigger != null && trigger.skin != c4_mode)
                    return;

                c4_mode = (ulong)C4_MODE.TIMED;
            }

            public string GetC4Mode()
            {
                return c4_mode == (ulong)C4_MODE.TIMED ? "Timed" : "Triggered"; 
            }

            private void UpdateTriggerSkin()
            {
                if (trigger == null || trigger.skin == c4_mode)
                    return;

                Item newItem = ItemManager.CreateByItemID(TRIGGER_ITEM_ID, 1, c4_mode);
                newItem.condition = trigger.condition;

                int position = trigger.position;

                trigger.RemoveFromContainer();
                trigger = newItem;
                newItem.MoveToContainer(player.inventory.containerBelt, position);
            }

            public void UpdateActiveItem(Item activeItem)
            {
                if (activeItem != null && (activeItem.skin == TRIGGER_SKIN_TIMED_MODE || activeItem.skin == TRIGGER_SKIN_TRIGGERD_MODE))
                {
                    trigger = activeItem;
                    UpdateTriggerSkin();
                }
                else
                    trigger = null;
            }

            public void CraftTrigger()
            {
                int item_1_amount = 0;
                int item_2_amount = 0;

                ItemContainer[] containers = new ItemContainer[] { player.inventory.containerMain, player.inventory.containerBelt };
                foreach (ItemContainer container in containers)
                {
                    item_1_amount += container.GetAmount(configuration.CRAFTING_ITEM_1_ID, true);
                    item_2_amount += container.GetAmount(configuration.CRAFTING_ITEM_2_ID, true);
                }

                if (item_1_amount >= configuration.CRAFTING_ITEM_1_NEEDED && item_2_amount >= configuration.CRAFTING_ITEM_2_NEEDED)
                {
                    item_1_amount = configuration.CRAFTING_ITEM_1_NEEDED;
                    item_2_amount = configuration.CRAFTING_ITEM_2_NEEDED;

                    foreach (ItemContainer container in containers)
                    {
                        if (item_1_amount > 0)
                            item_1_amount -= container.Take(null, configuration.CRAFTING_ITEM_1_ID, item_1_amount);

                        if (item_2_amount > 0)
                            item_2_amount -= container.Take(null, configuration.CRAFTING_ITEM_2_ID, item_2_amount);
                    }

                    if (GiveTrigger(1) != 1)
                    {
                        ChatMessage(player, L_CRAFTING_FAILED_SPACE, new object[] { Instance.lang.GetMessage(L_INVENTORY_FULL, Instance, player.UserIDString) });
                        player.inventory.GiveItem(ItemManager.Create(ItemManager.FindItemDefinition(configuration.CRAFTING_ITEM_1_ID), configuration.CRAFTING_ITEM_1_NEEDED));
                        player.inventory.GiveItem(ItemManager.Create(ItemManager.FindItemDefinition(configuration.CRAFTING_ITEM_2_ID), configuration.CRAFTING_ITEM_2_NEEDED));
                        return;
                    }

                    ChatMessage(player, L_CRAFTING_SUCCESS);
                }

                else
                    ChatMessage(player, L_CRAFTING_FAILED_RESOURCES, new object[] {
                        ItemManager.FindItemDefinition(configuration.CRAFTING_ITEM_1_ID).displayName.translated + " (" +
                        configuration.CRAFTING_ITEM_1_NEEDED + "), " + ItemManager.FindItemDefinition(configuration.CRAFTING_ITEM_2_ID).displayName.translated
                        + " (" + configuration.CRAFTING_ITEM_2_NEEDED + ")" });
            }

            public int GiveTrigger(int amount)
            {
                int given = 0;
                while (amount > 0)
                {
                    if (!player.inventory.GiveItem(ItemManager.CreateByItemID(TRIGGER_ITEM_ID, 1, c4_mode)))
                        break;
                    given++;
                    amount--;
                }

                return given;
            }

            public static void Pickup(Item item)
            {
                BaseEntity entity = item.GetWorldEntity();
                ulong playerID;

                if (fakeC4_To_PlayerID.TryGetValue(entity.net.ID, out playerID))
                {
                    allManagers[playerID].Pickup(entity.GetComponent<DroppedItem>());
                    item.amount = 1;
                }

            }

            private void Pickup(DroppedItem item)
            {
                fakeExplosives.Remove(item);
                saveData.deployedExplosives[player.userID].Remove(item.net.ID);
                SaveDataToFile();
            }
        }

        private static class TriggerShop
        {
            struct CreateInfo_BuildingBlock
            {
                public readonly Vector3 position, rotation;
                public readonly string prefabName;
                public readonly int grade;
                public CreateInfo_BuildingBlock(Vector3 position, Vector3 rotation, string prefabName)
                {
                    this.position = position;
                    this.rotation = rotation;
                    this.prefabName = prefabName;
                    this.grade = 4;
                }
            }
            struct CreateInfo_WoodenSign
            {
                public readonly Vector3 position, rotation;
                public readonly string prefabName, texture;
                public readonly bool locked;

                public CreateInfo_WoodenSign(Vector3 position, Vector3 rotation)
                {
                    this.position = position;
                    this.rotation = rotation;
                    this.prefabName = "assets/prefabs/deployable/signs/sign.medium.wood.prefab";
                    this.texture = "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";
                    this.locked = true;
                }
            }
            struct CreatInfo_Item
            {
                public readonly Vector3 position, rotation;
                public readonly string prefabName;
                public readonly ulong skinId;
                public CreatInfo_Item(Vector3 position, Vector3 rotation, string prefabName)
                {
                    this.position = position;
                    this.rotation = rotation;
                    this.prefabName = prefabName;
                    this.skinId = 0;
                }
            }

            private static CreateInfo_BuildingBlock[] buildingBlockInfos = new CreateInfo_BuildingBlock[]
            {
                new CreateInfo_BuildingBlock(new Vector3(0f, 0f, 0f),
                                             new Vector3(0f, 0f, 0f),
                                             "assets/prefabs/building core/foundation.triangle/foundation.triangle.prefab"),
                new CreateInfo_BuildingBlock(new Vector3(-0.75f, 0f, 1.3f),
                                             new Vector3(0f, 210f, 0f),
                                             "assets/prefabs/building core/wall/wall.prefab"),
                new CreateInfo_BuildingBlock(new Vector3(0.75f, 0f, 1.3f),
                                             new Vector3(0f, -30f, 0f),
                                             "assets/prefabs/building core/wall/wall.prefab"),
                new CreateInfo_BuildingBlock(new Vector3(0f,  0f, 0f),
                                             new Vector3(0f, 90f, 0f),
                                             "assets/prefabs/building core/wall.doorway/wall.doorway.prefab"),
                new CreateInfo_BuildingBlock(new Vector3(0f, 3f, 0f),
                                             new Vector3(0f, 0f, 0f),
                                             "assets/prefabs/building core/floor.triangle/floor.triangle.prefab")
            };
            private static CreateInfo_WoodenSign signInfo = new CreateInfo_WoodenSign(new Vector3(0f, 1.9f, -0.15f),
                                                                                      new Vector3(0f, 180f, 0f));

            private static CreatInfo_Item toolCupboardInfo = new CreatInfo_Item(new Vector3(0f, 0f, 1.0f),
                                                                                new Vector3(0f, 0f, 0f),
                                                                                "assets/prefabs/deployable/tool cupboard/cupboard.tool.deployed.prefab");
            private static CreatInfo_Item vendingMachineInfo = new CreatInfo_Item(new Vector3(0f, 0f, 0.3f),
                                                                                  new Vector3(0f, 180f, 0f),
                                                                                  "assets/prefabs/deployable/vendingmachine/vendingmachine.deployed.prefab");

            public static VendingMachine Place(Vector3 position, float y_rotation, BasePlayer player)
            {
                BaseEntity entity;
                BaseCombatEntity basecombat;
                uint? buidlingID = null;

                // BuildingBlocks
                foreach (CreateInfo_BuildingBlock ci_BB in buildingBlockInfos)
                {
                    entity = basecombat = null;
                    entity = CreateEntity(position, y_rotation, ci_BB.position, ci_BB.rotation, ci_BB.prefabName, player);

                    BuildingBlock buildingBlock = entity.GetComponentInParent<BuildingBlock>();
                    buildingBlock.blockDefinition = PrefabAttribute.server.Find<Construction>(buildingBlock.prefabID);
                    buildingBlock.SetGrade((BuildingGrade.Enum)ci_BB.grade);
                    if (buidlingID == null)
                        buidlingID = BuildingManager.server.NewBuildingID();
                    buildingBlock.buildingID = (uint)buidlingID;

                    entity.Spawn();
                    saveData.shopEntities.Add(entity.net.ID);
                    basecombat = entity.GetComponentInParent<BaseCombatEntity>();
                    basecombat.ChangeHealth(basecombat.MaxHealth());
                }

                // ToolCupboard
                entity = basecombat = null;
                entity = CreateEntity(position, y_rotation, toolCupboardInfo.position, toolCupboardInfo.rotation, toolCupboardInfo.prefabName, player);
                ((DecayEntity)entity).buildingID = (uint)buidlingID;
                entity.Spawn();
                saveData.shopEntities.Add(entity.net.ID);
                basecombat = entity.GetComponentInParent<BaseCombatEntity>();
                basecombat.ChangeHealth(basecombat.MaxHealth());

                //Vending Machine
                entity = basecombat = null;
                entity = CreateEntity(position, y_rotation, vendingMachineInfo.position, vendingMachineInfo.rotation, vendingMachineInfo.prefabName, player);
                entity.Spawn();
                saveData.shopEntities.Add(entity.net.ID);
                basecombat = entity.GetComponentInParent<BaseCombatEntity>();
                basecombat.ChangeHealth(basecombat.MaxHealth());

                VendingMachine vendingMachine = entity.GetComponentInParent<VendingMachine>();
                Refill(vendingMachine);
                SetupSellOrder(vendingMachine);

                // Sign
                entity = basecombat = null;
                entity = CreateEntity(position, y_rotation, signInfo.position, signInfo.rotation, signInfo.prefabName, player);
                entity.Spawn();
                saveData.shopEntities.Add(entity.net.ID);
                basecombat = entity.GetComponentInParent<BaseCombatEntity>();
                basecombat.ChangeHealth(basecombat.MaxHealth());

                ApplySignTexture(entity.GetComponentInParent<Signage>());

                SaveDataToFile();

                return vendingMachine;
            }

            public static void ApplySignTexture (Signage sign)
            {
                byte[] texture = Convert.FromBase64String(signInfo.texture);
                sign.textureID = FileStorage.server.Store(texture, FileStorage.Type.png, sign.net.ID);

                sign.SetFlag(BaseEntity.Flags.Locked, signInfo.locked);
                sign.SendNetworkUpdate();
            }

            private static BaseEntity CreateEntity(Vector3 placementPos, float placementRotY, Vector3 pos, Vector3 rot, string prefabName, BasePlayer player)
            {
                Vector3 placementRot = new Vector3(0f, placementRotY, 0f);

                Quaternion newRot = Quaternion.Euler(placementRot + rot);
                Vector3 newPos = Quaternion.Euler(placementRot) * pos + placementPos;

                BaseEntity entity = GameManager.server.CreateEntity(prefabName, newPos, newRot);

                entity.transform.position = newPos;
                entity.transform.rotation = newRot;
                entity.SendMessage("SetDeployedBy", player, SendMessageOptions.DontRequireReceiver);

                return entity;
            }

            public static void Refill(VendingMachine vendingMachine)
            {
                ItemManager.CreateByItemID(TRIGGER_ITEM_ID, 1, TRIGGER_SKIN_TIMED_MODE).MoveToContainer(vendingMachine.inventory);
            }

            public static void RemoveCurrency(VendingMachine vendingMachine)
            {
                foreach (Item i in vendingMachine.inventory.FindItemsByItemID(configuration.CURRENCY_ID))
                    i.RemoveFromContainer();
            }

            public static void SetupSellOrder (VendingMachine vendingMachine)
            {
                vendingMachine.sellOrders.sellOrders.RemoveAll(v => v.itemToSellID == TRIGGER_ITEM_ID);
                vendingMachine.RefreshSellOrderStockLevel();

                ProtoBuf.VendingMachine.SellOrder order = new ProtoBuf.VendingMachine.SellOrder();
                order.itemToSellID = TRIGGER_ITEM_ID;
                order.itemToSellAmount = 1;
                order.currencyID = configuration.CURRENCY_ID;
                order.currencyAmountPerItem = configuration.CURRENCY_NEEDED;
                vendingMachine.sellOrders.sellOrders.Add(order);
                vendingMachine.RefreshSellOrderStockLevel();
            }
        }

        public class SaveData
        {
            public Dictionary<ulong, List<uint>> deployedExplosives = new Dictionary<ulong, List<uint>>();  // stores all deployed explosives per player (real & fake)

            public HashSet<uint> vendingMachines = new HashSet<uint>();
            public HashSet<uint> shopEntities = new HashSet<uint>();
        }
        #endregion

        private static TriggeredExplosiveCharges Instance;
        private static Configuration configuration;
        private static SaveData saveData;

        #region Commands
        bool PermissionGranted(BasePlayer player, string permissionName) { return (player.net.connection.authLevel > 1) || permission.UserHasPermission(player.UserIDString, permissionName); }

        [ChatCommand("tec.shop")]
        private void PlaceShop(BasePlayer player, string command, string[] args)
        {
            if (!PermissionGranted(player, PERMISSION_PLACE))
            {
                ChatMessage(player, L_PERMISSION_FAILED);
                return;
            }

            RaycastHit hitinfo;
            if (Physics.Raycast(player.eyes.position, player.GetNetworkRotation() * Vector3.forward, out hitinfo, 30f,
                LayerMask.GetMask(new string[] { "Construction", "Deployed", "Terrain", "World", "Water", "Default"})))
            {
                VendingMachine vendingMachine = hitinfo.transform.GetComponent<VendingMachine>();
                Signage sign = hitinfo.transform.GetComponent<Signage>();
                if (vendingMachine)
                {
                    TriggerShop.Refill(vendingMachine);
                    TriggerShop.SetupSellOrder(vendingMachine);
                }
                else if (sign)
                {
                    TriggerShop.ApplySignTexture(sign);
                    return;
                }
                else
                    vendingMachine = TriggerShop.Place(hitinfo.point, player.GetNetworkRotation().eulerAngles.y, player);

                saveData.vendingMachines.Add(vendingMachine.net.ID);
                SaveDataToFile();
            }
            else
                ChatMessage(player, L_SHOP_FAILED);
        }

        [ChatCommand("tec.givetrigger")]
        private void GiveTrigger_Chat(BasePlayer player, string command, string[] args)
        {
            GiveTrigger(player, args);
        }

        [ConsoleCommand("tec.givetrigger")]
        private void GiveTrigger_Console(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                GiveTrigger(arg.Player(), arg.Args);
            else if (arg.IsRcon && arg.IsAdmin)
                GiveTrigger(null, arg.Args);
        }

        private void GiveTrigger(BasePlayer player, string[] args)
        {
            int amount = 1;
            bool playerNotNull = (player != null);

            if (playerNotNull && player.net.connection.authLevel < 2)
            {
                ChatMessage(player, L_PERMISSION_FAILED);
                return;
            }
            if (args == null || args.Length == 0)
            {
                if (playerNotNull)
                    ChatMessage(player, L_GIVE_MISSING_ARGUMENT, new object[] { "/givetrigger " + lang.GetMessage(L_GIVE_SYNTAX, this, player.UserIDString) });
                else
                    ServerMessage(L_GIVE_MISSING_ARGUMENT, new object[] { "givetrigger " + lang.GetMessage(L_GIVE_SYNTAX, this) });

                return;
            }
            if (args.Length > 1)
            {
                if (!Int32.TryParse(args[1], out amount))
                {
                    if (playerNotNull)
                        ChatMessage(player, L_GIVE_WRONG_FORMAT, new object[] { "/givetrigger " + lang.GetMessage(L_GIVE_SYNTAX, this, player.UserIDString) });
                    else
                        ServerMessage(L_GIVE_WRONG_FORMAT, new object[] { "givetrigger " + lang.GetMessage(L_GIVE_SYNTAX, this) });

                    return;
                }
            }

            BasePlayer givePlayer = BasePlayer.Find(args[0]);

            if (givePlayer == null)
            {
                if (playerNotNull)
                    ChatMessage(player, L_GIVE_NOT_FOUND);
                else
                    ServerMessage(L_GIVE_NOT_FOUND);
                return;
            }

            int given = TriggeredExplosivesManager.allManagers[givePlayer.userID].GiveTrigger(amount);

            if (playerNotNull)
                ChatMessage(player, L_GIVE_SUCCESS, new object[] { givePlayer.displayName, given + "/" + amount, (given != amount ? "(" + lang.GetMessage(L_INVENTORY_FULL, this, player.UserIDString) + ")" : "") });
            else
                ServerMessage(L_GIVE_SUCCESS, new object[] { givePlayer.displayName, given + "/" + amount, (given != amount ? "(" + lang.GetMessage(L_INVENTORY_FULL, this) + ")": "") });
        }

        [ChatCommand("tec.craft")]
        private void CraftTrigger(BasePlayer player, string command, string[] args)
        {
            if (!configuration.CRAFTING_ENABLED && (player.net.connection.authLevel < 2) && !PermissionGranted(player, PERMISSION_CRAFTING))
            {
                ChatMessage(player, L_PERMISSION_FAILED);
                return;
            }

            TriggeredExplosivesManager.allManagers[player.userID].CraftTrigger();
        }

        [ChatCommand("tec.mode")]
        private void Console_Toggle_C4_Mode(BasePlayer player, string command, string[] args)
        {
            if (!PermissionGranted(player, PERMISSION_NOTRIGGER))
            {
                ChatMessage(player, L_PERMISSION_FAILED);
                return;
            }

            TriggeredExplosivesManager.allManagers[player.userID].Toggle_C4_Mode(true);
            if (player != null)
                ChatMessage(player, L_MODE_TOGGLE, new object[] { TriggeredExplosivesManager.allManagers[player.userID].GetC4Mode() });
        }

        [ChatCommand("tec.explode")]
        private void Console_Explode(BasePlayer player, string command, string[] args)
        {
            if (!PermissionGranted(player, PERMISSION_NOTRIGGER))
            {
                ChatMessage(player, L_PERMISSION_FAILED);
                return;
            }
            TriggeredExplosivesManager.allManagers[player.userID].Explode(true);
        }
        #endregion

        #region Initialization & Termination

        private void OnServerInitialized()
        {
            LoadDataFromFile();
        }

        private void Init()
        {
            permission.RegisterPermission(PERMISSION_PLACE, this);
            permission.RegisterPermission(PERMISSION_CRAFTING, this);
            permission.RegisterPermission(PERMISSION_NOTRIGGER, this);

            Instance = this;

            LoadDefaultConfig();
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                OnPlayerDisconnected(player, "");
            foreach (TriggeredExplosivesManager tem in TriggeredExplosivesManager.allManagers.Values)
                tem.Explode(true);

            TriggeredExplosivesManager.allManagers.Clear();

            SaveDataToFile();
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (!TriggeredExplosivesManager.allManagers.ContainsKey(player.userID))
                TriggeredExplosivesManager.allManagers.Add(player.userID, new TriggeredExplosivesManager(player));
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason) { TriggeredExplosivesManager.allManagers.Remove(player.userID); }

        #endregion

        #region Hooks

        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            TimedExplosive te = entity as TimedExplosive;
            if (entity.name.Equals("assets/prefabs/tools/c4/explosive.timed.deployed.prefab"))
                TriggeredExplosivesManager.allManagers[player.userID].DeployExplosive(te);
        }

        private void OnPlayerActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            OnPlayerInit(player);
            TriggeredExplosivesManager.allManagers[player.userID].UpdateActiveItem(newItem);
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (input.WasJustPressed(BUTTON.FIRE_PRIMARY))
                TriggeredExplosivesManager.allManagers[player.userID].Explode();
            else if (input.WasJustPressed(BUTTON.FIRE_SECONDARY))
                TriggeredExplosivesManager.allManagers[player.userID].Toggle_C4_Mode();
        }

        private void OnVendingTransaction(VendingMachine vendingMachine, BasePlayer player)
        {
            if (saveData.vendingMachines.Contains(vendingMachine.net.ID))
            {
                TriggerShop.Refill(vendingMachine);
                TriggerShop.RemoveCurrency(vendingMachine);
            }
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null || entity.net == null)
                return;

            if (saveData.vendingMachines.Contains(entity.net.ID) || saveData.shopEntities.Contains(entity.net.ID))
                info.damageTypes = new Rust.DamageTypeList();
        }

        private void OnItemPickup(Item item, BasePlayer player)
        {
            if (configuration.C4_ALLOW_PICKUP && item.info.itemid == C4_ITEM_ID)
                TriggeredExplosivesManager.Pickup(item);
        }

        void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            BasePlayer player = container.GetOwnerPlayer();

            if (player != null && (item.skin == TRIGGER_SKIN_TIMED_MODE || item.skin == TRIGGER_SKIN_TRIGGERD_MODE))
            {
                foreach (Item i in player.inventory.FindItemIDs(item.info.itemid))
                {
                    if(item.skin == TRIGGER_SKIN_TIMED_MODE || item.skin == TRIGGER_SKIN_TRIGGERD_MODE)
                        return;
                }
                TriggeredExplosivesManager.allManagers[player.userID].Reset_C4_Mode();
            }
        }
        #endregion

        #region Configuration & Data

        private struct Configuration
        {
            public const string S_CURRENCY_ID =     "SHOP | Currency item for buying a trigger [item shortname]";
            public const string S_CURRENCY_NEEDED = "SHOP | Needed amount of currency item [number]";

            public const string S_CRAFTING_ENABLED =        "CRAFTING | Allow all players to craft triggers [true,false]";
            public const string S_CRAFTING_ITEM_1_ID =      "CRAFTING | Ingredient 1 for crafting [item shortname]";
            public const string S_CRAFTING_ITEM_1_NEEDED =  "CRAFTING | Needed amount of ingredient 1 [number]";
            public const string S_CRAFTING_ITEM_2_ID =      "CRAFTING | Ingredient 2 for crafting [item shortname]";
            public const string S_CRAFTING_ITEM_2_NEEDED =  "CRAFTING | Needed amount of ingredient 2 [number]";

            public const string S_C4_BEEP_DURATION = "TRIGGERED C4 | Disable beeping sound after duration (minimum: 5; infinite: -1) [seconds]";
            public const string S_C4_ALLOW_PICKUP = "TRIGGERED C4 | Allow C4 pickup (only after beeping got disabled) [true, false]";

            public const string S_TRIGGER_ONE_TIME_USE = "TRIGGER | Trigger destroys itself after using it once [true, false]";

            public const string S_BLOCK_IN_EVENTS = "Event Manager | Block trigger usage during events [true, false]";

            public readonly int CURRENCY_ID;
            public readonly int CURRENCY_NEEDED;

            public readonly bool CRAFTING_ENABLED;

            public readonly int CRAFTING_ITEM_1_ID;
            public readonly int CRAFTING_ITEM_1_NEEDED;
            public readonly int CRAFTING_ITEM_2_ID;
            public readonly int CRAFTING_ITEM_2_NEEDED;

            public readonly int C4_BEEP_DURATION;
            public readonly bool C4_ALLOW_PICKUP;

            public readonly bool TRIGGER_ONE_TIME_USE;

            public readonly bool BLOCK_IN_EVENTS;

            public Configuration(int currency_id, int currency_needed, bool crafting_enabled,
                                 int item_1_id, int item_1_needed, int item_2_id, int item_2_needed,
                                 int beepDuration, bool allowPickup, bool oneTimeUse, bool blockInEvents)
            {
                CURRENCY_ID = currency_id;
                CURRENCY_NEEDED = currency_needed;

                CRAFTING_ENABLED = crafting_enabled;
                CRAFTING_ITEM_1_ID = item_1_id;
                CRAFTING_ITEM_1_NEEDED = item_1_needed;
                CRAFTING_ITEM_2_ID = item_2_id;
                CRAFTING_ITEM_2_NEEDED = item_2_needed;

                C4_BEEP_DURATION = beepDuration;
                C4_ALLOW_PICKUP = allowPickup;

                TRIGGER_ONE_TIME_USE = oneTimeUse;

                BLOCK_IN_EVENTS = blockInEvents;
            }
        }

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        protected override void LoadDefaultConfig()
        {
            string currency_shortname;
            int currency_needed;

            bool craft;
            string item_1_shortname, item_2_shortname;
            int item_1_needed, item_2_needed;

            int beepDuration;
            bool allowPickup;

            bool oneTimeUse;

            bool blockInEvents;

            Config[Configuration.S_CURRENCY_ID] = currency_shortname = GetConfig(Configuration.S_CURRENCY_ID, "techparts");
            Config[Configuration.S_CURRENCY_NEEDED] = currency_needed = GetConfig(Configuration.S_CURRENCY_NEEDED, 5);

            Config[Configuration.S_CRAFTING_ENABLED] = craft = GetConfig(Configuration.S_CRAFTING_ENABLED, false);
            Config[Configuration.S_CRAFTING_ITEM_1_ID] = item_1_shortname = GetConfig(Configuration.S_CRAFTING_ITEM_1_ID, "metal.refined");
            Config[Configuration.S_CRAFTING_ITEM_1_NEEDED] = item_1_needed = GetConfig(Configuration.S_CRAFTING_ITEM_1_NEEDED, 50);
            Config[Configuration.S_CRAFTING_ITEM_2_ID] = item_2_shortname = GetConfig(Configuration.S_CRAFTING_ITEM_2_ID, "techparts");
            Config[Configuration.S_CRAFTING_ITEM_2_NEEDED] = item_2_needed = GetConfig(Configuration.S_CRAFTING_ITEM_2_NEEDED, 2);

            Config[Configuration.S_C4_BEEP_DURATION] = beepDuration = GetConfig(Configuration.S_C4_BEEP_DURATION, 10);
            Config[Configuration.S_C4_ALLOW_PICKUP] = allowPickup = GetConfig(Configuration.S_C4_ALLOW_PICKUP, false);

            Config[Configuration.S_TRIGGER_ONE_TIME_USE] = oneTimeUse = GetConfig(Configuration.S_TRIGGER_ONE_TIME_USE, false);

            Config[Configuration.S_BLOCK_IN_EVENTS] = blockInEvents = GetConfig(Configuration.S_BLOCK_IN_EVENTS, false);

            ItemDefinition item_1_definition = ItemManager.FindItemDefinition(item_1_shortname);
            ItemDefinition item_2_definition = ItemManager.FindItemDefinition(item_2_shortname);
            ItemDefinition currency_definition = ItemManager.FindItemDefinition(currency_shortname);

            int item_1_id = 374890416, item_2_id = 1471284746, currency_id = 1471284746;

            if (item_1_definition != null) item_1_id = item_1_definition.itemid;
            else ServerMessage(L_CONFIG_ERROR, new object[] { "Ingredient 1 for crafting",  "\"metal.refined\""});

            if (item_2_definition != null) item_2_id = item_2_definition.itemid;
            else ServerMessage(L_CONFIG_ERROR, new object[] { "Ingredient 2 for crafting", "\"techparts\"" });

            if (currency_definition != null) currency_id = currency_definition.itemid;
            else ServerMessage(L_CONFIG_ERROR, new object[] { "Currency item for buying a trigger", "\"techparts\"" });

            if (beepDuration!=-1 && beepDuration < 5.0f) beepDuration = 5;

            configuration = new Configuration(currency_id, currency_needed,
                craft, item_1_id, item_1_needed, item_2_id, item_2_needed,
                beepDuration, allowPickup, oneTimeUse, blockInEvents);

            SaveConfig();
        }

        private static void SaveDataToFile() { Core.Interface.Oxide.DataFileSystem.WriteObject(Instance.Name, saveData); }

        private static void LoadDataFromFile()
        {
            saveData = Core.Interface.Oxide.DataFileSystem.ReadObject<SaveData>(Instance.Name);
            if (saveData == null)
                saveData = new SaveData();

            saveData.vendingMachines.RemoveWhere(s => BaseNetworkable.serverEntities.Find(s) == null);
            saveData.shopEntities.RemoveWhere(s => BaseNetworkable.serverEntities.Find(s) == null);

            foreach (uint vmID in saveData.vendingMachines)
                TriggerShop.SetupSellOrder(BaseNetworkable.serverEntities.Find(vmID).GetComponent<VendingMachine>());

            foreach (BasePlayer player in BasePlayer.activePlayerList)
                Instance.OnPlayerInit(player);

            SaveDataToFile();
        }

        #endregion

        #region Localization

        private const string L_CRAFTING_SUCCESS = "Crafting | Success";
        private const string L_CRAFTING_FAILED_SPACE = "Crafting | NotEnoughSpace";
        private const string L_CRAFTING_FAILED_RESOURCES = "Crafting | NotEnoughResources";
        private const string L_PERMISSION_FAILED = "Permission | NotGranted";
        private const string L_SHOP_FAILED = "Shop | NoValidTarget";
        private const string L_GIVE_MISSING_ARGUMENT = "Give | MissingArgument";
        private const string L_GIVE_WRONG_FORMAT = "Give | WrongFormat";
        private const string L_GIVE_NOT_FOUND = "Give | PlayerNotFound";
        private const string L_GIVE_SYNTAX = "Give | Syntax";
        private const string L_GIVE_SUCCESS = "Give | Success";
        private const string L_CONFIG_ERROR = "Config | Error";
        private const string L_INVENTORY_FULL = "Inventory | NotEnoughSpace";
        private const string L_MODE_TOGGLE = "Mode | Toggle";

        private static void ChatMessage(BasePlayer player, string key) { player.ChatMessage(Instance.lang.GetMessage(key, Instance, player.UserIDString)); }
        private static void ChatMessage(BasePlayer player, string key, object[] args) { player.ChatMessage(String.Format(Instance.lang.GetMessage(key, Instance, player.UserIDString), args)); }
        private static void ServerMessage(string key) { Instance.Puts(Instance.lang.GetMessage(key, Instance)); }
        private static void ServerMessage(string key, object[] args) { Instance.Puts(String.Format(Instance.lang.GetMessage(key, Instance), args)); }

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [L_CRAFTING_SUCCESS] = "Trigger crafted!",
                [L_CRAFTING_FAILED_SPACE] = "Crafting Failed: {0}",
                [L_CRAFTING_FAILED_RESOURCES] = "Not enough resources. Needed: {0}",
                [L_PERMISSION_FAILED] = "You don't have permission to use this command",
                [L_SHOP_FAILED] = "Not a valid target for shop placement",
                [L_GIVE_MISSING_ARGUMENT] = "Missing Argument\n(Syntax: {0})",
                [L_GIVE_WRONG_FORMAT] = "Argument amount must be a number\n(Syntax: {0})",
                [L_GIVE_SYNTAX] = "\"PLAYERNAME or STEAMID\" \"optional:AMOUNT\"",
                [L_GIVE_NOT_FOUND] = "Player not found",
                [L_GIVE_SUCCESS] = "Gave \"{0}\" {1} triggers {2}",
                [L_CONFIG_ERROR] = "Configuration Error: Invalid Value for \"{0}\" (loading default value: {1})",
                [L_INVENTORY_FULL] = "Not enough inventory space",
                [L_MODE_TOGGLE] = "C4 Mode: {0}"
            }, this);

            // German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [L_CRAFTING_SUCCESS] = "Trigger gecrafted!",
                [L_CRAFTING_FAILED_SPACE] = "Craften fehlgeschlagen: {0}",
                [L_CRAFTING_FAILED_RESOURCES] = "Zu wenig Ressourcen. Benötigt: {0}",
                [L_PERMISSION_FAILED] = "Du bist nicht berechtigt diesen Befehl zu benutzen",
                [L_SHOP_FAILED] = "Du kannst hier keinen Shop platzieren",
                [L_GIVE_MISSING_ARGUMENT] = "Fehlende Parameter\n(Syntax: {0})",
                [L_GIVE_WRONG_FORMAT] = "Anzahl muss eine Zahl sein\n(Syntax: {0})",
                [L_GIVE_SYNTAX] = "\"SPIELERNAME oder STEAMID\" \"optional:ANZAHL\"",
                [L_GIVE_NOT_FOUND] = "Spieler nicht gefunden",
                [L_GIVE_SUCCESS] = "\"{0}\" {1} Trigger gegeben {2}",
                [L_CONFIG_ERROR] = "Configuration Fehler: Ungültiger Wert für \"{0}\" (lade Standardwert: {1})",
                [L_INVENTORY_FULL] = "Nicht genug Platz im Inventar",
                [L_MODE_TOGGLE] = "C4 Modus: {0}"
            }, this, "de");
        }

        #endregion

        #region External Plugins
        [PluginReference]
        Core.Plugins.Plugin EventManager;
        bool IsPlayingEvent(BasePlayer player) => (bool?)EventManager?.Call("isPlaying", player) ?? false;
        #endregion
    }
}
