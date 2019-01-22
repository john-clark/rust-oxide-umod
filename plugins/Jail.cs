using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Jail", "Reneb / k1lly0u", "4.0.0", ResourceId = 794)]
    class Jail : RustPlugin
    {
        #region Fields
        [PluginReference] Plugin ZoneManager, Spawns, Kits;

        PrisonData prisonData;
        PrisonerData prisonerData;
        RestoreData restoreData;
        private DynamicConfigFile prisondata, prisonerdata, restoredata;

        private Dictionary<string, PrisonData.PrisonEntry> prisons = new Dictionary<string, PrisonData.PrisonEntry>();
        private Dictionary<ulong, PrisonerData.PrisonerEntry> prisoners = new Dictionary<ulong, PrisonerData.PrisonerEntry>();

        static Jail ins;

        private LayerMask layerMask;

        const string UIJailTimer = "JailUI_TimeRemaining";
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            prisondata = Interface.Oxide.DataFileSystem.GetFile("Jail/prison_data");
            prisonerdata = Interface.Oxide.DataFileSystem.GetFile("Jail/prisoner_data");
            restoredata = Interface.Oxide.DataFileSystem.GetFile("Jail/restoration_data");

            lang.RegisterMessages(Messages, this);
            permission.RegisterPermission("jail.canenter", this);
            permission.RegisterPermission("jail.admin", this);
        }

        private void OnServerInitialized()
        {
            LoadVariables();
            LoadData();
            ins = this;

            layerMask = (1 << 29);
            layerMask |= (1 << 18);
            layerMask = ~layerMask;            

            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerInit(player);
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (player.IsSleeping() || player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.In(1, () => OnPlayerInit(player));
                return;
            }

            if (IsPrisoner(player))
            {
                var time = prisoners[player.userID].releaseDate - GrabCurrentTime();
                
                if (time <= 0)
                {
                    if (configData.AutoReleaseWhenExpired)
                        FreeFromJail(player);
                    else SendReply(player, msg("freetoleave", player.userID));
                }
                else
                {        
                    string prisonName = prisoners[player.userID].prisonName;

                    if (prisons.ContainsKey(prisonName))
                    {
                        PrisonData.PrisonEntry entry = prisons[prisonName];

                        if (!IsInZone(player, entry.zoneId) && !configData.AllowBreakouts)
                        {
                            object spawnLocation = GetSpawnLocation(prisonName, prisoners[player.userID].cellNumber);
                            if (spawnLocation is Vector3)
                            {
                                MovePosition(player, (Vector3)spawnLocation);                                
                            }
                        }
                        ShowJailTimer(player);
                    }                    
                }
            }
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null || !configData.DisablePrisonerDamage)
                return;

            BasePlayer player = info.InitiatorPlayer;
            if (player != null && IsPrisoner(player))
            {
                info.damageTypes = new Rust.DamageTypeList();
                info.HitEntity = null;
                info.HitMaterial = 0;
                info.PointStart = Vector3.zero;
            }
        }

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (player == null || arg.Args == null || player.IsAdmin)
                return null;

            if (IsPrisoner(player))
            {
                string cmd = arg.Args[0].ToLower();

                if (configData.BlockChat && arg.cmd.FullName == "chat.say" && !cmd.StartsWith("/"))
                {
                    if (configData.InmateChat)
                    {
                        string prisonName = prisoners[player.userID].prisonName;
                        foreach (var prisoner in prisoners.Where(x => x.Value.prisonName == prisonName))
                        {
                            BasePlayer inmate = null;
                            IPlayer iPlayer = covalence.Players.FindPlayerById(prisoner.Key.ToString());
                            if (iPlayer != null && iPlayer.IsConnected)
                                inmate = iPlayer.Object as BasePlayer;

                            inmate.SendConsoleCommand("chat.add", new object[] { player.UserIDString, $"<color={(player.IsAdmin ? "#aaff55" : player.IsDeveloper ? "#fa5" : "#55AAFF")}>[Inmate Chat] {player.displayName}</color>: {string.Join(" ", arg.Args)}" });
                        }
                    }
                    else SendReply(player, msg("chatblocked", player.userID));
                    return false;
                }

                if (cmd.Length > 0 && cmd.StartsWith("/") && arg.cmd.FullName == "chat.say")
                {
                    if (configData.CommandBlacklist.Any(entry => entry.StartsWith("/") ? cmd.StartsWith(entry) : cmd.Substring(1).StartsWith(entry)))
                    {
                        SendReply(player, msg("blacklistcmd", player.userID));
                        return false;
                    }
                }
            }
            return null;
        }

        private void OnServerSave() => SavePrisonerData();

        private void Unload()
        {
            foreach(var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, UIJailTimer);
        }
        #endregion

        #region Functions
        private void SendToPrison(BasePlayer player, string prisonName, double time)
        {
            int cellNumber = GetEmptyCell(prisonName);

            object spawnLocation = GetSpawnLocation(prisonName, cellNumber);
            if (spawnLocation is Vector3)
            {
                PrisonerData.PrisonerEntry entry = new PrisonerData.PrisonerEntry
                {
                    cellNumber = cellNumber,
                    prisonName = prisonName,
                    releaseDate = time + GrabCurrentTime()
                };

                prisoners[player.userID] = entry;
                restoreData.AddData(player);
                player.inventory.Strip();
                NextTick(() =>
                {                    
                    MovePosition(player, (Vector3)spawnLocation);
                    CheckIn(player, prisonName);
                });
            }
            SavePrisonerData();
        }

        private int GetEmptyCell(string prisonName)
        {
            int cellNumber = prisons[prisonName].occupiedCells.Where(x => x.Value == false).ToList().GetRandom().Key;
            prisons[prisonName].occupiedCells[cellNumber] = true;
            return cellNumber;
        }

        private void CheckIn(BasePlayer player, string prisonName)
        {
            if (player.IsSleeping() || player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.In(1, () => CheckIn(player, prisonName));
                return;
            }
            PrisonData.PrisonEntry entry = prisons[prisonName];
            if (configData.GiveInmateKits && !string.IsNullOrEmpty(entry.inmateKit))
                Kits.Call("GiveKit", player, entry.inmateKit);
                        
            ShowJailTimer(player);
        }

        private void FreeFromJail(BasePlayer player)
        {
            PrisonerData.PrisonerEntry entry = prisoners[player.userID];
            prisons[entry.prisonName].occupiedCells[entry.cellNumber] = false;            
            
            CuiHelper.DestroyUi(player, UIJailTimer);
            prisoners.Remove(player.userID);

            restoreData.RestorePlayer(player, configData.ReturnHomeAfterRelease);

            if (!configData.ReturnHomeAfterRelease)
                MovePosition(player, CalculateFreePosition(entry.prisonName));

            SavePrisonerData();
        }
        #endregion

        #region Teleportation
        private object GetSpawnLocation(string prisonName, int cellNumber)
        {
            var success = Spawns.Call("GetSpawn", new object[] { prisons[prisonName].spawnFile, cellNumber });
            if (success is string)
            {
                PrintError($"There was a error retrieving spawn location. Cell #{cellNumber} at prison : {prisonName}");
                return null;
            }
            return (Vector3)success;
        }

        private Vector3 CalculateFreePosition(string prisonName)
        {
            PrisonData.PrisonEntry entry = prisons[prisonName];

            Vector3 exitPoint = new Vector3(entry.x, entry.y, entry.z) + (UnityEngine.Random.onUnitSphere * (entry.radius * 2));

            RaycastHit hitInfo;
            if (Physics.Raycast(exitPoint + (Vector3.up * 50), Vector3.down, out hitInfo, layerMask))
                exitPoint.y = hitInfo.point.y;

            float terrainHeight = TerrainMeta.HeightMap.GetHeight(exitPoint);
            if (exitPoint.y < terrainHeight)
                exitPoint.y = terrainHeight;

            return exitPoint;
        }

        private void PushBack(BasePlayer player, string prisonName)
        {
            PrisonData.PrisonEntry entry = prisons[prisonName];

            Vector3 prisonPos = new Vector3(entry.x, entry.y, entry.z);

            var direction = (prisonPos - player.transform.position).y;

            player.MovePosition(player.transform.position + (Quaternion.Euler(0, direction, 0) * (Vector3.back * 5)));

        }

        private void MovePosition(BasePlayer player, Vector3 destination)
        {
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "StartLoading");
            StartSleeping(player);
            player.MovePosition(destination);
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "ForcePositionTo", destination);
            if (player.net?.connection != null)
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.UpdateNetworkGroup();
            player.SendNetworkUpdateImmediate(false);
            if (player.net?.connection == null) return;
            try { player.ClearEntityQueue(null); } catch { }
            player.SendFullSnapshot();
        }

        private void StartSleeping(BasePlayer player)
        {
            if (player.IsSleeping())
                return;
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
            if (!BasePlayer.sleepingPlayerList.Contains(player))
                BasePlayer.sleepingPlayerList.Add(player);
            player.CancelInvoke("InventoryUpdate");
        }
        #endregion

        #region UI
        class UI
        {
            static public CuiElementContainer Element(string panelName, string color, string aMin, string aMax)
            {
                var NewElement = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                        },
                        new CuiElement().Parent = "Hud",
                        panelName
                    }
                };
                return NewElement;
            }

            static public void Label(ref CuiElementContainer container, string panel, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Align = align, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);

            }
        }

        private void ShowJailTimer(BasePlayer player)
        {
            if (player != null)
            {
                CuiHelper.DestroyUi(player, UIJailTimer);
                PrisonerData.PrisonerEntry entry;
                if (prisoners.TryGetValue(player.userID, out entry))
                {
                    var time = entry.releaseDate - GrabCurrentTime();
                    if (time > 0)
                    {
                        string clock = FormatTime(time);

                        var container = UI.Element(UIJailTimer, "0.3 0.3 0.3 0.6", "0.4 0.965", "0.6 0.995");
                        UI.Label(ref container, UIJailTimer, ins.msg("remaining", player.userID) + clock, 14, "0 0", "1 1");
                        CuiHelper.AddUi(player, container);
                        timer.In(1, () => ShowJailTimer(player));
                    }
                    else
                    {
                        if (configData.AutoReleaseWhenExpired)
                            FreeFromJail(player);
                        else
                        {
                            var container = UI.Element(UIJailTimer, "0.3 0.3 0.3 0.6", "0.35 0.965", "0.65 0.995");
                            UI.Label(ref container, UIJailTimer, ins.msg("freetoleave", player.userID), 14, "0 0", "1 1");
                            CuiHelper.AddUi(player, container);
                        }
                    }

                }
            }
        }

        private string FormatTime(double time)
        {
            TimeSpan dateDifference = TimeSpan.FromSeconds((float)time);
            var days = dateDifference.Days;
            var hours = dateDifference.Hours;
            hours += (days * 24);
            var mins = dateDifference.Minutes;
            var secs = dateDifference.Seconds;
            if (hours > 0)
                return string.Format("{0:00}:{1:00}:{2:00}", hours, mins, secs);
            else return string.Format("{0:00}:{1:00}", mins, secs);
        }
        #endregion

        #region Zone Management
        private bool IsInZone(BasePlayer player, string zoneID)
        {
            if (ZoneManager == null) return false;
            return (bool)ZoneManager.Call("isPlayerInZone", zoneID, player);
        }

        private void OnEnterZone(string zoneID, BasePlayer player)
        {
            string prisonName = string.Empty;

            foreach(var prison in prisons)
            {
                if (prison.Value.zoneId == zoneID)
                    prisonName = prison.Key;
            }

            if (!string.IsNullOrEmpty(prisonName))
            {
                if (!permission.UserHasPermission(player.UserIDString, "jail.canenter") && !IsPrisoner(player) && configData.BlockPublicAccessToPrisons)
                {
                    PushBack(player, prisonName);
                    SendReply(player, msg("trespassing", player.userID));
                }
                else SendReply(player, string.Format(msg("welcome", player.userID), prisonName, player.displayName));
            }
        }

        private void OnExitZone(string zoneID, BasePlayer player)
        {
            if (IsPrisoner(player))
            {
                PrisonerData.PrisonerEntry entry = prisoners[player.userID];

                if (prisons.ContainsKey(entry.prisonName) && zoneID == prisons[entry.prisonName].zoneId)
                {
                    if (configData.AllowBreakouts)
                    {
                        SendReply(player, string.Format(msg("escaped", player.userID), entry.prisonName));
                        prisons[entry.prisonName].occupiedCells[entry.cellNumber] = false;

                        if (configData.ReturnGearOnBreakout)
                            restoreData.RestorePlayer(player, false);

                        CuiHelper.DestroyUi(player, UIJailTimer);

                        prisoners.Remove(player.userID);
                    }
                    else
                    {
                        object spawnLocation = GetSpawnLocation(entry.prisonName, entry.cellNumber);
                        if (spawnLocation is Vector3)
                        {
                            MovePosition(player, (Vector3)spawnLocation);
                            SendReply(player, msg("noescape", player.userID));
                        }
                    }
                }
            }
        }
        #endregion

        #region Inventory Saving and Restoration
        public class RestoreData
        {
            public Hash<ulong, PlayerData> restoreData = new Hash<ulong, PlayerData>();

            public void AddData(BasePlayer player)
            {
                restoreData[player.userID] = new PlayerData(player);
            }

            public void RemoveData(ulong playerId)
            {
                if (HasRestoreData(playerId))
                    restoreData.Remove(playerId);
            }

            public bool HasRestoreData(ulong playerId) => restoreData.ContainsKey(playerId);

            public void RestorePlayer(BasePlayer player, bool returnHome)
            {
                PlayerData playerData;
                if (restoreData.TryGetValue(player.userID, out playerData))
                {
                    player.inventory.Strip();

                    if (player.IsSleeping() || player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
                    {
                        ins.timer.Once(1, () => RestorePlayer(player, returnHome));
                        return;
                    }

                    ins.NextTick(() =>
                    {
                        playerData.SetStats(player);
                        if (returnHome)
                            ins.MovePosition(player, playerData.GetPosition());
                        RestoreAllItems(player, playerData);
                    });
                }
            }

            private void RestoreAllItems(BasePlayer player, PlayerData playerData)
            {
                if (player == null || !player.IsConnected)
                    return;

                if (RestoreItems(player, playerData.containerBelt, "belt") && RestoreItems(player, playerData.containerWear, "wear") && RestoreItems(player, playerData.containerMain, "main"))
                    RemoveData(player.userID);
            }

            private bool RestoreItems(BasePlayer player, ItemData[] itemData, string type)
            {
                ItemContainer container = type == "belt" ? player.inventory.containerBelt : type == "wear" ? player.inventory.containerWear : player.inventory.containerMain;

                for (int i = 0; i < itemData.Length; i++)
                {
                    Item item = CreateItem(itemData[i]);
                    item.position = itemData[i].position;
                    item.SetParent(container);
                }
                return true;
            }

            private Item CreateItem(ItemData itemData)
            {
                var item = ItemManager.CreateByItemID(itemData.itemid, itemData.amount, itemData.skin);
                item.condition = itemData.condition;
                if (itemData.instanceData != null)
                    item.instanceData = itemData.instanceData;

                var weapon = item.GetHeldEntity() as BaseProjectile;
                if (weapon != null)
                {
                    if (!string.IsNullOrEmpty(itemData.ammotype))
                        weapon.primaryMagazine.ammoType = ItemManager.FindItemDefinition(itemData.ammotype);
                    weapon.primaryMagazine.contents = itemData.ammo;
                }
                if (itemData.contents != null)
                {
                    foreach (var contentData in itemData.contents)
                    {
                        var newContent = ItemManager.CreateByItemID(contentData.itemid, contentData.amount);
                        if (newContent != null)
                        {
                            newContent.condition = contentData.condition;
                            newContent.MoveToContainer(item.contents);
                        }
                    }
                }
                return item;
            }

            public class PlayerData
            {
                public float[] stats;
                public float[] position;
                public ItemData[] containerMain;
                public ItemData[] containerWear;
                public ItemData[] containerBelt;

                public PlayerData() { }

                public PlayerData(BasePlayer player)
                {
                    stats = GetStats(player);
                    position = GetPosition(player.transform.position);
                    containerBelt = GetItems(player.inventory.containerBelt).ToArray();
                    containerMain = GetItems(player.inventory.containerMain).ToArray();
                    containerWear = GetItems(player.inventory.containerWear).ToArray();
                }

                private IEnumerable<ItemData> GetItems(ItemContainer container)
                {
                    return container.itemList.Select(item => new ItemData
                    {
                        itemid = item.info.itemid,
                        amount = item.amount,
                        ammo = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.contents ?? 0,
                        ammotype = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.ammoType.shortname ?? null,
                        position = item.position,
                        skin = item.skin,
                        condition = item.condition,
                        instanceData = item.instanceData ?? null,
                        contents = item.contents?.itemList.Select(item1 => new ItemData
                        {
                            itemid = item1.info.itemid,
                            amount = item1.amount,
                            condition = item1.condition
                        }).ToArray()
                    });
                }

                private float[] GetStats(BasePlayer player) => new float[] { player.health, player.metabolism.hydration.value, player.metabolism.calories.value };

                public void SetStats(BasePlayer player)
                {
                    player.health = stats[0];
                    player.metabolism.hydration.value = stats[1];
                    player.metabolism.calories.value = stats[2];
                    player.metabolism.SendChangesToClient();
                }

                private float[] GetPosition(Vector3 position) => new float[] { position.x, position.y, position.z };

                public Vector3 GetPosition() => new Vector3(position[0], position[1], position[2]);
            }

            public class ItemData
            {
                public int itemid;
                public ulong skin;
                public int amount;
                public float condition;
                public int ammo;
                public string ammotype;
                public int position;
                public ProtoBuf.Item.InstanceData instanceData;
                public ItemData[] contents;
            }
        }
        #endregion

        #region Helpers
        private double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

        private bool HasPermission(ulong playerId, string perm) => permission.UserHasPermission(playerId.ToString(), perm);

        private bool IsPrisoner(BasePlayer player) => prisoners.ContainsKey(player.userID);

        private bool HasEmptyCells(string prisonName) => prisons[prisonName].occupiedCells.Where(x => x.Value == false).Count() > 0;
        #endregion

        #region Commands
        [ChatCommand("leavejail")]
        private void cmdLeaveJail(BasePlayer player, string command, string[] args)
        {
            if (IsPrisoner(player))
            {
                var time = prisoners[player.userID].releaseDate - GrabCurrentTime();
                if (time <= 0)
                    FreeFromJail(player);
                else SendReply(player, string.Format(msg("timeremaining", player.userID), FormatTime(time)));
            }
        }

        [ChatCommand("jail")]
        private void cmdJail(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "jail.admin"))
                return;

            if (args.Length == 0)
            {
                SendReply(player, msg("help1", player.userID));
                SendReply(player, msg("help2", player.userID));
                SendReply(player, msg("help3", player.userID));
                SendReply(player, msg("help4", player.userID));
                SendReply(player, msg("help5", player.userID));
                SendReply(player, msg("help6", player.userID));
                return;
            }
            switch (args[0].ToLower())
            {
                case "create":
                    if (args.Length >= 4)
                    {
                        string name = args[1];                        
                        string kit = args.Length > 4 ? args[4] : "";

                        if (prisons.ContainsKey(name))
                        {
                            SendReply(player, msg("alreadyexists", player.userID));
                            return;
                        }

                        if (ValidateSpawnFile(args[2]) != null)
                        {
                            SendReply(player, msg("invalidspawns", player.userID));
                            return;
                        }

                        if (ValidateZoneID(args[3]) != null)
                        {
                            SendReply(player, msg("invalidzone", player.userID));
                            return;
                        }

                        if (!string.IsNullOrEmpty(kit) && ValidateKit(kit) != null)
                        {
                            SendReply(player, msg("invalidkit", player.userID));
                            return;
                        }

                        Vector3 location = Vector3.zero;
                        float radius = 20;
                        int spawnCount = 1;

                        object success = ZoneManager.Call("GetZoneLocation", args[3]);
                        if (success != null)
                            location = (Vector3)success;

                        success = ZoneManager.Call("GetZoneRadius", args[3]);
                        if (success == null)
                        {
                            success = ZoneManager.Call("GetZoneSize", args[3]);
                            if (success != null)
                            {
                                Vector3 v3 = (Vector3)success;
                                radius = v3.x;
                            }
                        }
                        else radius = Convert.ToSingle(success);

                        success = Spawns?.Call("GetSpawnsCount", args[2]);
                        if (success is int)
                            spawnCount = Convert.ToInt32(success);

                        prisons.Add(name, new PrisonData.PrisonEntry(args[3], args[2], kit, location, radius, spawnCount));
                        SendReply(player, msg("createsuccess", player.userID));
                        SavePrisonData();
                    }
                    else SendReply(player, msg("help1", player.userID));
                    return;
                case "remove":
                    if (args.Length == 2)
                    {
                        if (prisons.ContainsKey(args[1]))
                        {
                            FreePrisoners(args[1]);
                            prisons.Remove(args[1]);
                            SendReply(player, string.Format(msg("removesuccess", player.userID), args[1]));
                            SavePrisonData();
                        }
                    }
                    else SendReply(player, msg("help2", player.userID));
                    return;
                case "list":
                    SendReply(player, string.Format(msg("list", player.userID), prisons.Count));
                    foreach (var prison in prisons)
                        SendReply(player, $"Name: {prison.Key} - Zone: {prison.Value.zoneId}, Location: {prison.Value.x} {prison.Value.y} {prison.Value.z}");
                    return;
                case "send":
                    if (args.Length >= 2)
                    {
                        BasePlayer inmate = null;
                        IPlayer iPlayer = covalence.Players.FindPlayer(args[1]);
                        if (iPlayer != null && iPlayer.IsConnected)
                            inmate = iPlayer.Object as BasePlayer;

                        if (inmate == null)
                        {
                            SendReply(player, string.Format(msg("noplayer", player.userID), args[1]));
                            return;
                        }

                        int time = int.MaxValue;
                        string reason = string.Empty;
                        string prisonName = string.Empty;

                        if (args.Length > 2)
                        {
                            if (!int.TryParse(args[2], out time))
                            {
                                SendReply(player, msg("notime", player.userID));
                                return;
                            }
                        }

                        if (args.Length > 3)
                            reason = args[3];

                        if (args.Length >= 4)
                        {
                            if (!prisons.ContainsKey(args[4]))
                            {
                                SendReply(player, string.Format(msg("invalidprison", player.userID), args[4]));
                                return;
                            }
                            prisonName = args[4];
                        }
                        else prisonName = prisons.Keys.ToList().GetRandom();

                        SendToPrison(inmate, prisonName, time);
                        SavePrisonerData();

                        if (configData.BroadcastImprisonment)
                            PrintToChat(string.IsNullOrEmpty(reason) ? string.Format(msg("sent1"), inmate.displayName, FormatTime(time)) : string.Format(msg("sent2"), inmate.displayName, time, reason));
                        else
                        {
                            SendReply(inmate, string.IsNullOrEmpty(reason) ? string.Format(msg("sent3", inmate.userID), time) : string.Format(msg("sent4", inmate.userID), time ,reason));
                            SendReply(player, string.IsNullOrEmpty(reason) ? string.Format(msg("sent5", player.userID), inmate.displayName, time) : string.Format(msg("sent6", player.userID), inmate.displayName, time, reason));
                        }
                    }
                    else SendReply(player, msg("help4", player.userID));
                    return;
                case "free":
                    if (args.Length >= 2)
                    {
                        BasePlayer inmate = null;
                        IPlayer iPlayer = covalence.Players.FindPlayer(args[1]);
                        if (iPlayer != null && iPlayer.IsConnected)
                            inmate = iPlayer.Object as BasePlayer;

                        if (inmate == null)
                        {
                            SendReply(player, string.Format(msg("noplayer", player.userID), args[1]));
                            return;
                        }

                        if (!IsPrisoner(inmate))
                        {
                            SendReply(player, string.Format(msg("notinjail", player.userID), inmate.displayName));
                            return;
                        }

                        FreeFromJail(inmate);
                        SendReply(player, string.Format(msg("releasefromjail", player.userID), inmate.displayName));
                    }
                    else SendReply(player, msg("help5", player.userID));
                    return;
                case "clear":
                    if (args.Length == 2)
                    {
                        if (prisons.ContainsKey(args[1]))
                        {
                            FreePrisoners(args[1]);
                            SendReply(player, string.Format(msg("freedall", player.userID), args[1]));
                        }
                    }
                    else SendReply(player, msg("help6", player.userID));
                    return;
                default:
                    SendReply(player, msg("help1", player.userID));
                    SendReply(player, msg("help2", player.userID));
                    SendReply(player, msg("help3", player.userID));
                    SendReply(player, msg("help4", player.userID));
                    SendReply(player, msg("help5", player.userID));
                    SendReply(player, msg("help6", player.userID));
                    break;
            }
        }

        [ConsoleCommand("jail")]
        private void ccmdJail(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
                return;

            if (arg.Args == null || arg.Args.Length == 0)
            {                
                SendReply(arg, "jail list - List all jail names");
                SendReply(arg, "jail send <name or ID> <opt: time> \"<opt: reason>\" <opt: jailname> - Send a player to jail, with the option to specify time (in seconds), reason for incarceration and which jail to send them to");
                SendReply(arg, "jail free <name or ID> - Free a player from jail");
                SendReply(arg, "jail clear <jailname> - Free all inmates from the specified jail");
                return;
            }
            switch (arg.Args[0].ToLower())
            {                
                case "list":
                    SendReply(arg, $"Prison List ({prisons.Count} prison(s))");
                    foreach (var prison in prisons)
                        SendReply(arg, $"Name: {prison.Key} - Zone: {prison.Value.zoneId}, Location: {prison.Value.x} {prison.Value.y} {prison.Value.z}");
                    return;
                case "send":
                    if (arg.Args.Length >= 2)
                    {
                        BasePlayer inmate = null;
                        IPlayer iPlayer = covalence.Players.FindPlayer(arg.Args[1]);
                        if (iPlayer != null && iPlayer.IsConnected)
                            inmate = iPlayer.Object as BasePlayer;

                        if (inmate == null)
                        {
                            SendReply(arg, string.Format("Unable to find a player with the name or ID : {0}", arg.Args[1]));
                            return;
                        }

                        int time = int.MaxValue;
                        string reason = string.Empty;
                        string prisonName = string.Empty;

                        if (arg.Args.Length > 2)
                        {
                            if (!int.TryParse(arg.Args[2], out time))
                            {
                                SendReply(arg, "You must enter a number for the amount of time (in seconds)");
                                return;
                            }
                        }

                        if (arg.Args.Length > 3)
                            reason = arg.Args[3];

                        if (arg.Args.Length >= 4)
                        {
                            if (!prisons.ContainsKey(arg.Args[4]))
                            {
                                SendReply(arg, string.Format("{0} is not a valid prison name", arg.Args[4]));
                                return;
                            }
                            prisonName = arg.Args[4];
                        }
                        else prisonName = prisons.Keys.ToList().GetRandom();

                        SendToPrison(inmate, prisonName, time);
                        SavePrisonerData();

                        if (configData.BroadcastImprisonment)
                            PrintToChat(string.IsNullOrEmpty(reason) ? string.Format("{0} is doing {1} in jail", inmate.displayName, FormatTime(time)) : string.Format("{0} is doing {1} in jail for {2}", inmate.displayName, time, reason));
                        else
                        {
                            SendReply(inmate, string.IsNullOrEmpty(reason) ? string.Format("You have been sent to jail for {0}", time) : string.Format("You have been sent to jail for {0} because {1}", time, reason));
                            SendReply(arg, string.IsNullOrEmpty(reason) ? string.Format("You have sent {0} to jail for {1}", inmate.displayName, time) : string.Format("You have sent {0} to jail for {1} because {2}", inmate.displayName, time, reason));
                        }
                    }
                    else SendReply(arg, "/jail send <name or ID> <opt: time> \"<opt: reason>\" <opt: jailname> - Send a player to jail, with the option to specify time (in seconds), reason for incarceration and which jail to send them to");
                    return;
                case "free":
                    if (arg.Args.Length >= 2)
                    {
                        BasePlayer inmate = null;
                        IPlayer iPlayer = covalence.Players.FindPlayer(arg.Args[1]);
                        if (iPlayer != null && iPlayer.IsConnected)
                            inmate = iPlayer.Object as BasePlayer;

                        if (inmate == null)
                        {
                            SendReply(arg, string.Format("Unable to find a player with the name or ID : {0}", arg.Args[1]));
                            return;
                        }

                        if (!IsPrisoner(inmate))
                        {
                            SendReply(arg, string.Format("{0} is not in jail", inmate.displayName));
                            return;
                        }

                        FreeFromJail(inmate);
                        SendReply(arg, string.Format("You have released {0} from jail", inmate.displayName));
                    }
                    else SendReply(arg, "/jail free <name or ID> - Free a player from jail");
                    return;
                case "clear":
                    if (arg.Args.Length == 2)
                    {
                        if (prisons.ContainsKey(arg.Args[1]))
                        {
                            FreePrisoners(arg.Args[1]);
                            SendReply(arg, string.Format("You have successfully removed the prison: {0}", arg.Args[1]));
                        }
                    }
                    else SendReply(arg, "/jail clear <jailname> - Free all inmates from the specified jail");
                    return;
                default:                   
                    SendReply(arg, "jail list - List all jail names");

                    SendReply(arg, "jail send <name or ID> <opt: time> \"<opt: reason>\" <opt: jailname> - Send a player to jail, with the option to specify time (in seconds), reason for incarceration and which jail to send them to");
                    SendReply(arg, "jail free <name or ID> - Free a player from jail");
                    SendReply(arg, "jail clear <jailname> - Free all inmates from the specified jail");
                    break;
            }
        }

        private void FreePrisoners(string prisonName)
        {
            if (prisons.ContainsKey(prisonName))
            {
                var release = prisoners.Where(x => x.Value.prisonName == prisonName).ToArray();
                if (release.Length > 0)
                {
                    for (int i = release.Length; i >= 0; i--)
                    {
                        BasePlayer inmate = null;
                        IPlayer iPlayer = covalence.Players.FindPlayerById(release[i].Key.ToString());
                        if (iPlayer != null && iPlayer.IsConnected)                        
                            inmate = iPlayer.Object as BasePlayer;
                        
                        if (inmate != null)
                            FreeFromJail(inmate);

                        else release[i].Value.releaseDate = 0;
                    }
                }
            }
        }

        public object ValidateSpawnFile(string name)
        {
            var success = Spawns?.Call("GetSpawnsCount", name);
            if (success is string)
                return false;
            else return null;
        }

        public object ValidateZoneID(string name)
        {
            var success = ZoneManager?.Call("CheckZoneID", name);
            if (name is string && !string.IsNullOrEmpty((string)name))
                return null;
            else return false;
        }

        public object ValidateKit(string name)
        {
            object success = Kits?.Call("isKit", name);
            if ((success is bool) && !(bool)success)               
                return false;
            return null;
        }
        #endregion

        #region Config        
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Return prisoners to the position they were incarcerated at when released")]
            public bool ReturnHomeAfterRelease { get; set; }
            [JsonProperty(PropertyName = "Block chat for inmates")]
            public bool BlockChat { get; set; }
            [JsonProperty(PropertyName = "Allow chat between inmates (when chat is blocked)")]
            public bool InmateChat { get; set; }
            [JsonProperty(PropertyName = "Allow players to escape jail")]
            public bool AllowBreakouts { get; set; }
            [JsonProperty(PropertyName = "Return prisoners belongings if they escape jail")]
            public bool ReturnGearOnBreakout { get; set; }
            [JsonProperty(PropertyName = "Automatically release prisoners when their sentence has expired")]
            public bool AutoReleaseWhenExpired { get; set; }
            [JsonProperty(PropertyName = "Give prisoners a designated kit")]
            public bool GiveInmateKits { get; set; }
            [JsonProperty(PropertyName = "Disable damage dealt by prisoners")]
            public bool DisablePrisonerDamage { get; set; }
            [JsonProperty(PropertyName = "Broadcast player imprisonment globally")]
            public bool BroadcastImprisonment { get; set; }
            [JsonProperty(PropertyName = "Restrict public access to prison zones")]
            public bool BlockPublicAccessToPrisons { get; set; }
            [JsonProperty(PropertyName = "Blacklisted commands for prisoners")]
            public string[] CommandBlacklist { get; set; }
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                AllowBreakouts = false,
                BlockChat = false,
                InmateChat = true,
                AutoReleaseWhenExpired = true,
                BlockPublicAccessToPrisons = true,
                BroadcastImprisonment = true,
                DisablePrisonerDamage = true,
                GiveInmateKits = true,
                ReturnHomeAfterRelease = true,
                ReturnGearOnBreakout = true,
                CommandBlacklist = new string[] { "tp", "event", "tpa", "tpr", "s" }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Data Management
        private void SavePrisonData()
        {
            prisonData.prisons = prisons;
            prisondata.WriteObject(prisonData);
        }

        private void SavePrisonerData()
        {
            prisonerData.prisoners = prisoners;
            prisonerdata.WriteObject(prisonerData);
            restoredata.WriteObject(restoreData);
        }

        private void LoadData()
        {
            try
            {
                prisonData = prisondata.ReadObject<PrisonData>();
                prisons = prisonData.prisons;
            }
            catch
            {
                prisonData = new PrisonData();
            }
            try
            {
                prisonerData = prisonerdata.ReadObject<PrisonerData>();
                prisoners = prisonerData.prisoners;
            }
            catch
            {
                prisonerData = new PrisonerData();
            }
            try
            {
                restoreData = restoredata.ReadObject<RestoreData>();
            }
            catch
            {
                restoreData = new RestoreData();
            }
        }

        class PrisonData
        {
            public Dictionary<string, PrisonEntry> prisons = new Dictionary<string, PrisonEntry>();

            public class PrisonEntry
            {
                public string zoneId, spawnFile, inmateKit;
                public float x, y, z, radius;
                public Dictionary<int, bool> occupiedCells = new Dictionary<int, bool>();

                public PrisonEntry() { }

                public PrisonEntry(string zoneId, string spawnFile, string inmateKit, Vector3 position, float radius, int spawnCount)
                {
                    this.zoneId = zoneId;
                    this.spawnFile = spawnFile;
                    this.inmateKit = inmateKit;
                    x = position.x;
                    y = position.y;
                    z = position.z;
                    this.radius = radius;

                    for (int i = 0; i < spawnCount; i++)                    
                        occupiedCells.Add(i, false);                    
                }
            }
        }

        class PrisonerData
        {
            public Dictionary<ulong, PrisonerEntry> prisoners = new Dictionary<ulong, PrisonerEntry>();

            public class PrisonerEntry
            {
                public string prisonName;
                public int cellNumber;
                public double releaseDate;
            }
        }        
        #endregion

        #region Localization
        string msg(string key, ulong playerId = 0U) => lang.GetMessage(key, this, playerId == 0U ? null : playerId.ToString());

        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["freetoleave"] = "You are free to leave jail! Type /leavejail when ready",
            ["remaining"] = "Remaining: ",
            ["trespassing"] = "You are trespassing on prison property!",
            ["welcome"] = "Welcome to {0} {1}",
            ["escaped"] = "You have broken out of {0} and are now on the run!",
            ["noescape"] = "There is no escape from this prison!",
            ["timeremaining"] = "You still have another {0} remaining on your sentence",
            ["help1"] = "/jail create <name> <spawnfile> <zoneId> <opt:kit> - Create a new jail",
            ["help2"] = "/jail remove <name> - Remove a jail",
            ["help3"] = "/jail list - List all jail names",
            ["help4"] = "/jail send <name or ID> <opt: time> \"<opt: reason>\" <opt: jailname> - Send a player to jail, with the option to specify time (in seconds), reason for incarceration and which jail to send them to",
            ["help5"] = "/jail free <name or ID> - Free a player from jail",
            ["help6"] = "/jail clear <jailname> - Free all inmates from the specified jail",
            ["alreadyexists"] = "There is already a jail with that name",
            ["invalidspawns"] = "Invalid spawnfile selected",
            ["invalidzone"] = "Invalid zone ID selected",
            ["invalidkit"] = "Invalid kit selected",
            ["createsuccess"] = "You have successfully created a new prison!",
            ["removesuccess"] = "You have successfully removed the prison: {0}",
            ["list"] = "Prison List ({0} prison(s))",
            ["noplayer"] = "Unable to find a player with the name or ID : {0}",
            ["notime"] = "You must enter a number for the amount of time (in seconds)",
            ["invalidprison"] = "{0} is not a valid prison name",
            ["sent1"] = "{0} is doing {1} in jail",
            ["sent2"] = "{0} is doing {1} in jail for {2}",
            ["sent3"] = "You have been sent to jail for {0}",
            ["sent4"] = "You have been sent to jail for {0} because {1}",
            ["sent5"] = "You have sent {0} to jail for {1}",
            ["sent6"] = "You have sent {0} to jail for {1} because {2}",
            ["notinjail"] = "{0} is not in jail",
            ["releasefromjail"] = "You have released {0} from jail",
            ["freedall"] = "You have successfully freed all the prisoners from : {0}",
            ["blacklistcmd"] = "You can not use that command whilst in jail",
            ["chatblocked"] = "Chat is blocked for inmates"
        };
        #endregion
    }
}