using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Oxide.Plugins
{
    [Info("Custom Loot Spawns", "k1lly0u", "0.2.11", ResourceId = 1655)]
    [Description("Creates additional custom spawn points for loot boxes of your choosing")]
    class CustomLootSpawns : RustPlugin
    {
        #region Fields

        private CLSData clsData;
        private DynamicConfigFile clsdata;

        private Dictionary<BaseEntity, int> boxCache = new Dictionary<BaseEntity, int>();
        private Dictionary<int, CustomBoxData> boxTypes = new Dictionary<int, CustomBoxData>();
        private List<Timer> refreshTimers = new List<Timer>();
        private List<BaseEntity> wipeList = new List<BaseEntity>();

        private Dictionary<ulong, BoxCreator> boxCreators = new Dictionary<ulong, BoxCreator>();

        #endregion Fields

        #region Oxide Hooks

        private void Loaded()
        {
            permission.RegisterPermission("customlootspawns.admin", this);
            lang.RegisterMessages(messages, this);
            clsdata = Interface.Oxide.DataFileSystem.GetFile("CustomSpawns/cls_data");
            clsdata.Settings.Converters = new JsonConverter[] { new StringEnumConverter(), new UnityVector3Converter() };
        }

        private void OnServerInitialized()
        {
            LoadVariables();
            LoadData();
            FindBoxTypes();
            InitializeBoxSpawns();
        }

        private void OnEntityKill(BaseNetworkable entity)
        {
            BaseEntity baseEnt = entity as BaseEntity;
            if (baseEnt == null) return;
            if (wipeList.Contains(baseEnt)) return;
            if (entity.GetComponent<LootContainer>())
            {
                if (boxCache.ContainsKey(baseEnt))
                {
                    InitiateRefresh(baseEnt, boxCache[baseEnt]);
                }
            }
            else if (entity.GetComponent<StorageContainer>())
            {
                if (boxCache.ContainsKey(baseEnt))
                {
                    InitiateRefresh(baseEnt, boxCache[baseEnt]);
                }
            }
        }

        private void OnPlayerLootEnd(PlayerLoot inventory)
        {
            BasePlayer player = inventory.GetComponent<BasePlayer>();
            if (boxCreators.ContainsKey(player.userID))
            {
                StoreBoxData(player);
                boxCreators.Remove(player.userID);
            }

            if (inventory.entitySource != null)
            {
                BaseEntity box = inventory.entitySource;
                if (boxCache.ContainsKey(box))
                {
                    if (box is LootContainer) return;
                    if (box is StorageContainer)
                    {
                        if ((box as StorageContainer).inventory.itemList.Count == 0)
                            box.KillMessage();
                    }
                }
            }
        }

        private void OnLootSpawn(LootContainer container)
        {
            if (boxCache.ContainsKey(container))
                SpawnLoot(container, boxCache[container]);
        }

        private void Unload()
        {
            foreach (Timer time in refreshTimers)
                time.Destroy();

            foreach (KeyValuePair<BaseEntity, int> box in boxCache)
            {
                if (box.Key == null) continue;

                ClearContainer(box.Key);
                box.Key.KillMessage();
            }
            boxCache.Clear();
        }

        #endregion Oxide Hooks

        #region Box Control

        private void InitializeBoxSpawns()
        {
            foreach (KeyValuePair<int, CLBox> box in clsData.lootBoxes)
            {
                InitializeNewBox(box.Key);
            }
        }

        private void InitiateRefresh(BaseEntity box, int ID)
        {
            CLBox boxData;
            if (!clsData.lootBoxes.TryGetValue(ID, out boxData))
                return;
           
            int time = configData.RespawnTimer * 60;
            if (boxData.time > 0)
                time = boxData.time;

            refreshTimers.Add(timer.Once(time, () =>
            {
                InitializeNewBox(ID);
            }));

            boxCache.Remove(box);
        }

        private void InitializeNewBox(int ID)
        {
            CLBox boxData;
            if (!clsData.lootBoxes.TryGetValue(ID, out boxData))
                return;

            BaseEntity newBox = SpawnBoxEntity(boxData.boxType.Type, boxData.Position, boxData.yRotation, boxData.boxType.SkinID);

            SpawnLoot(newBox, ID);
            boxCache.Add(newBox, ID);
        }

        private void SpawnLoot(BaseEntity entity, int ID)
        {
            CLBox boxData;
            if (!clsData.lootBoxes.TryGetValue(ID, out boxData))
                return;

            if (!string.IsNullOrEmpty(boxData.customLoot) && clsData.customBoxes.ContainsKey(boxData.customLoot))
            {
                CustomBoxData customLoot = clsData.customBoxes[boxData.customLoot];
                if (customLoot.itemList.Count > 0)
                {
                    timer.In(3, () =>
                    {
                        ClearContainer(entity);
                        for (int i = 0; i < customLoot.itemList.Count; i++)
                        {
                            ItemStorage itemInfo = customLoot.itemList[i];
                            Item item = CreateItem(itemInfo.ID, itemInfo.Amount, itemInfo.SkinID);
                            if (entity is LootContainer)
                                item.MoveToContainer((entity as LootContainer).inventory);
                            else item.MoveToContainer((entity as StorageContainer).inventory);
                        }
                    });
                }
            }
        }

        private BaseEntity SpawnBoxEntity(string type, Vector3 pos, float rot, ulong skin = 0)
        {
            BaseEntity entity = InstantiateEntity(type, pos, Quaternion.Euler(0, rot, 0));
            entity.skinID = skin;
            entity.Spawn();
            return entity;
        }

        private BaseEntity InstantiateEntity(string type, Vector3 position, Quaternion rotation)
        {
            GameObject gameObject = Instantiate.GameObject(GameManager.server.FindPrefab(type), position, rotation);
            gameObject.name = type;

            SceneManager.MoveGameObjectToScene(gameObject, Rust.Server.EntityScene);

            UnityEngine.Object.Destroy(gameObject.GetComponent<Spawnable>());

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            BaseEntity component = gameObject.GetComponent<BaseEntity>();
            return component;
        }

        private void ClearContainer(BaseEntity container)
        {
            if (container is LootContainer)
            {
                (container as LootContainer).minSecondsBetweenRefresh = -1;
                (container as LootContainer).maxSecondsBetweenRefresh = 0;
                (container as LootContainer).CancelInvoke("SpawnLoot");

                while ((container as LootContainer).inventory.itemList.Count > 0)
                {
                    Item item = (container as LootContainer).inventory.itemList[0];
                    item.RemoveFromContainer();
                    item.Remove(0f);
                }
            }
            else
            {
                while ((container as StorageContainer).inventory.itemList.Count > 0)
                {
                    Item item = (container as StorageContainer).inventory.itemList[0];
                    item.RemoveFromContainer();
                    item.Remove(0f);
                }
            }
        }

        #endregion Box Control

        #region Custom Loot Creation

        private void AddSpawn(BasePlayer player, int type, int time)
        {
            CustomBoxData boxData = boxTypes[type];
            Vector3 pos = GetSpawnPos(player);
            int ID = GenerateRandomID();
            clsData.lootBoxes.Add(ID, new CLBox { Position = pos, yRotation = player.GetNetworkRotation().y, boxType = boxData.boxType, customLoot = boxData.Name, time = time });
            SaveData();
            InitializeNewBox(ID);
        }

        private void CreateNewCLB(BasePlayer player, string name, int type, ulong skin = 0)
        {
            if (boxCreators.ContainsKey(player.userID))
            {
                if (boxCreators[player.userID].entity != null)
                {
                    ClearContainer(boxCreators[player.userID].entity);
                    boxCreators[player.userID].entity.KillMessage();
                }
                boxCreators.Remove(player.userID);
            }
            CustomBoxData boxData = boxTypes[type];
            Vector3 pos = GetGroundPosition(player.transform.position + (player.eyes.BodyForward() * 2));

            BaseEntity box = GameManager.server.CreateEntity(boxData.boxType.Type, pos);
            if (boxData.boxType.SkinID != 0)
                box.skinID = boxData.boxType.SkinID;

            box.SendMessage("SetDeployedBy", player, UnityEngine.SendMessageOptions.DontRequireReceiver);
            box.Spawn();

            ClearContainer(box);

            boxCreators.Add(player.userID, new BoxCreator { entity = box, boxData = new CustomBoxData { Name = name, boxType = boxData.boxType } });
        }

        private void StoreBoxData(BasePlayer player)
        {
            ulong ID = player.userID;
            BoxCreator boxData = boxCreators[ID];

            List<Item> itemList = new List<Item>();
            if (boxData.entity is LootContainer) itemList = (boxData.entity as LootContainer).inventory.itemList;
            else itemList = (boxData.entity as StorageContainer).inventory.itemList;

            List<ItemStorage> storedList = new List<ItemStorage>();
            for (int i = 0; i < itemList.Count; i++)
            {
                storedList.Add(new ItemStorage { ID = itemList[i].info.itemid, Amount = itemList[i].amount, Shortname = itemList[i].info.shortname, SkinID = itemList[i].skin });
            }

            if (storedList.Count == 0)
            {
                SendMSG(player, MSG("noItems", player.UserIDString));
                boxData.entity.KillMessage();
                boxCreators.Remove(player.userID);
                return;
            }
            CustomBoxData data = new CustomBoxData { boxType = boxData.boxData.boxType, Name = boxData.boxData.Name, itemList = storedList };
            clsData.customBoxes.Add(boxData.boxData.Name, data);
            boxTypes.Add(boxTypes.Count + 1, data);
            SaveData();
            SendMSG(player, string.Format(MSG("boxCreated", player.UserIDString), boxTypes.Count, boxData.boxData.Name));
            ClearContainer(boxData.entity);
            boxData.entity.KillMessage();
            boxCreators.Remove(player.userID);
        }

        #endregion Custom Loot Creation

        #region Helper Methods

        private Item CreateItem(int itemID, int itemAmount, ulong itemSkin) => ItemManager.CreateByItemID(itemID, itemAmount, itemSkin);
        private int GenerateRandomID() => UnityEngine.Random.Range(0, 999999999);
        private static Vector3 GetGroundPosition(Vector3 sourcePos) // credit Wulf & Nogrod
        {
            RaycastHit hitInfo;

            if (Physics.Raycast(sourcePos, Vector3.down, out hitInfo, LayerMask.GetMask("Terrain", "World", "Construction")))
            {
                sourcePos.y = hitInfo.point.y;
            }
            sourcePos.y = Mathf.Max(sourcePos.y, TerrainMeta.HeightMap.GetHeight(sourcePos));
            return sourcePos;
        }
        private void FindBoxTypes()
        {
            var files = FileSystemBackend.cache;
            int i = 1;
            foreach (var str in files.Keys)
            {
                if ((str.StartsWith("assets/content/") || str.StartsWith("assets/bundled/") || str.StartsWith("assets/prefabs/")) && str.EndsWith(".prefab"))
                {
                    if (str.Contains("resource/loot") || str.Contains("radtown/crate") || str.Contains("radtown/loot") || str.Contains("loot") || str.Contains("radtown/oil"))
                    {
                        if (!str.Contains("ot/dm tier1 lootb"))
                        {
                            var gmobj = GameManager.server.FindPrefab(str);

                            if (gmobj?.GetComponent<BaseEntity>() != null)
                            {
                                boxTypes.Add(i, new CustomBoxData { boxType = new BoxType { Type = str, SkinID = 0 } });
                                i++;
                            }
                        }
                    }
                }
            }
            boxTypes.Add(i, new CustomBoxData { boxType = new BoxType { Type = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab", SkinID = 0 } });
            i++;
            boxTypes.Add(i, new CustomBoxData { boxType = new BoxType { Type = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab", SkinID = 10124, SkinName = "Ammo" } });
            i++;
            boxTypes.Add(i, new CustomBoxData { boxType = new BoxType { Type = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab", SkinID = 10123, SkinName = "FirstAid" } });
            i++;
            boxTypes.Add(i, new CustomBoxData { boxType = new BoxType { Type = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab", SkinID = 10141, SkinName = "Guns" } });
            i++;
            foreach (var box in clsData.customBoxes)
            {
                boxTypes.Add(i, box.Value);
                i++;
            }
        }
        private Vector3 GetSpawnPos(BasePlayer player)
        {
            Vector3 closestHitpoint;
            Vector3 sourceEye = player.transform.position + new Vector3(0f, 1.5f, 0f);
            Quaternion currentRot = Quaternion.Euler(player.serverInput.current.aimAngles);
            Ray ray = new Ray(sourceEye, currentRot * Vector3.forward);

            var hits = Physics.RaycastAll(ray);
            float closestdist = 999999f;
            closestHitpoint = player.transform.position;
            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<TriggerBase>() == null)
                {
                    if (hit.distance < closestdist)
                    {
                        closestdist = hit.distance;
                        closestHitpoint = hit.point;
                    }
                }
            }
            return closestHitpoint;
        }
        private BaseEntity FindContainer(BasePlayer player)
        {
            var currentRot = Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward;
            Vector3 eyesAdjust = new Vector3(0f, 1.5f, 0f);

            var rayResult = CastRay(player.transform.position + eyesAdjust, currentRot);
            if (rayResult is BaseEntity)
            {
                var box = rayResult as BaseEntity;
                return box;
            }
            return null;
        }
        private object CastRay(Vector3 Pos, Vector3 Aim)
        {
            var hits = Physics.RaycastAll(Pos, Aim);
            object target = null;

            foreach (var hit in hits)
            {
                if (hit.distance < 100)
                {
                    if (hit.collider.GetComponentInParent<StorageContainer>() != null)
                        target = hit.collider.GetComponentInParent<StorageContainer>();
                    else if (hit.collider.GetComponentInParent<LootContainer>() != null)
                        target = hit.collider.GetComponentInParent<LootContainer>();
                }
            }
            return target;
        }
        private List<BaseEntity> FindInRadius(Vector3 pos, float rad)
        {
            var foundBoxes = new List<BaseEntity>();
            foreach (var item in boxCache)
            {
                var itemPos = item.Key.transform.position;
                if (GetDistance(pos, itemPos.x, itemPos.y, itemPos.z) < rad)
                {
                    foundBoxes.Add(item.Key);
                }
            }
            return foundBoxes;
        }
        private float GetDistance(Vector3 v3, float x, float y, float z)
        {
            float distance = 1000f;

            distance = (float)Math.Pow(Math.Pow(v3.x - x, 2) + Math.Pow(v3.y - y, 2), 0.5);
            distance = (float)Math.Pow(Math.Pow(distance, 2) + Math.Pow(v3.z - z, 2), 0.5);

            return distance;
        }
        private bool IsUncreateable(string name)
        {
            foreach (var entry in unCreateable)
            {
                if (name.Contains(entry))
                    return true;
            }
            return false;
        }
        private void ShowBoxList(BasePlayer player)
        {
            foreach (var entry in boxTypes)
            {               
                SendEchoConsole(player.net.connection, string.Format("{0} - {1} {2}", entry.Key, entry.Value.boxType.Type, entry.Value.boxType.SkinName));
            }
        }
        private void ShowCurrentBoxes(BasePlayer player)
        {
            foreach (var box in clsData.lootBoxes)
            {
                string str = string.Empty;
                if (box.Value.time > 0)
                    str = string.Format("Position: {0} - Type: {1} - Respawn Time: {2} seconds", box.Value.Position, box.Value.boxType.Type, box.Value.time);
                else str = string.Format("Position: {0} - Type: {1}", box.Value.Position, box.Value.boxType.Type);

                SendEchoConsole(player.net.connection, str);
            }
        }
        private void SendEchoConsole(Network.Connection cn, string msg)
        {
            if (Network.Net.sv.IsConnected())
            {
                Network.Net.sv.write.Start();
                Network.Net.sv.write.PacketID(Network.Message.Type.ConsoleMessage);
                Network.Net.sv.write.String(msg);
                Network.Net.sv.write.Send(new Network.SendInfo(cn));
            }
        }
        private bool IsLootBox(BaseEntity entity) => boxCache.ContainsKey(entity);

        #endregion Helper Methods

        #region Chat Commands

        [ChatCommand("cls")]
        private void chatLootspawn(BasePlayer player, string command, string[] args)
        {
            if (!canSpawnLoot(player)) return;
            if (args.Length == 0)
            {
                SendReply(player, MSG("synAdd1", player.UserIDString));
                SendReply(player, MSG("synRem", player.UserIDString));
                SendReply(player, MSG("createSyn", player.UserIDString));
                SendReply(player, MSG("synList", player.UserIDString));
                SendReply(player, MSG("synBoxes", player.UserIDString));
                SendReply(player, MSG("synWipe", player.UserIDString));
                return;
            }
            if (args.Length >= 1)
            {
                switch (args[0].ToLower())
                {
                    case "add":
                        {
                            int type;
                            if (!int.TryParse(args[1], out type))
                            {
                                SendMSG(player, MSG("notNum", player.UserIDString));
                                return;
                            }
                            int time;
                            if (args.Length > 2)
                            {
                                if (!int.TryParse(args[2], out time))
                                {
                                    SendMSG(player, MSG("notTime", player.UserIDString));
                                    return;
                                }
                            }
                            else time = -1;

                            if (boxTypes.ContainsKey(type))
                            {
                                AddSpawn(player, type, time);
                                return;
                            }
                            SendMSG(player, MSG("notType", player.UserIDString));
                        }
                        return;

                    case "create":
                        {
                            if (!(args.Length == 3))
                            {
                                SendMSG(player, MSG("createSyn", player.UserIDString));
                                return;
                            }
                            if (!(args[1] == "") || (args[1] == null))
                            {
                                if (clsData.customBoxes.ContainsKey(args[1]))
                                {
                                    SendMSG(player, MSG("nameExists", player.UserIDString));
                                    return;
                                }
                                int type;
                                if (!int.TryParse(args[2], out type))
                                {
                                    SendMSG(player, MSG("notNum", player.UserIDString));
                                    return;
                                }
                                if (boxTypes.ContainsKey(type))
                                {
                                    if (IsUncreateable(boxTypes[type].boxType.Type))
                                    {
                                        SendMSG(player, MSG("unCreateable", player.UserIDString));
                                        return;
                                    }
                                    CreateNewCLB(player, args[1], type, boxTypes[type].boxType.SkinID);
                                    return;
                                }
                                SendMSG(player, MSG("notType", player.UserIDString));
                                return;
                            }
                            SendReply(player, MSG("createSyn", player.UserIDString));
                        }
                        return;

                    case "remove":
                        {
                            var box = FindContainer(player);
                            if (box != null)
                            {
                                if (boxCache.ContainsKey(box))
                                {
                                    if (clsData.lootBoxes.ContainsKey(boxCache[box]))
                                    {
                                        clsData.lootBoxes.Remove(boxCache[box]);
                                        SaveData();
                                    }
                                    ClearContainer(box);
                                    box.KillMessage();
                                    SendMSG(player, MSG("removedBox", player.UserIDString));
                                    return;
                                }
                                else
                                    SendMSG(player, MSG("notReg", player.UserIDString));
                                return;
                            }
                            SendMSG(player, MSG("notBox", player.UserIDString));
                        }
                        return;

                    case "list":
                        ShowCurrentBoxes(player);
                        SendMSG(player, MSG("checkConsole", player.UserIDString));
                        return;

                    case "boxes":
                        ShowBoxList(player);
                        SendMSG(player, MSG("checkConsole", player.UserIDString));
                        return;

                    case "near":
                        {
                            float rad = 3f;
                            if (args.Length == 2) float.TryParse(args[1], out rad);

                            var boxes = FindInRadius(player.transform.position, rad);
                            if (boxes != null)
                            {
                                SendMSG(player, string.Format(MSG("foundBoxes", player.UserIDString), boxes.Count));
                                foreach (var box in boxes)
                                {
                                    player.SendConsoleCommand("ddraw.box", 30f, Color.magenta, box.transform.position, 1f);
                                }
                            }
                            else
                                SendMSG(player, string.Format(MSG("noFind", player.UserIDString), rad));
                        }
                        return;

                    case "wipe":
                        {
                            var count = clsData.lootBoxes.Count;

                            foreach (var box in boxCache)
                            {
                                wipeList.Add(box.Key);
                                ClearContainer(box.Key);
                                box.Key.KillMessage();
                            }
                            clsData.lootBoxes.Clear();
                            wipeList.Clear();
                            SaveData();
                            SendMSG(player, string.Format(MSG("wipedAll1", player.UserIDString), count));
                        }
                        return;

                    case "wipeall":
                        {
                            var count = clsData.lootBoxes.Count;
                            var count2 = clsData.customBoxes.Count;
                            foreach (var box in boxCache)
                            {
                                wipeList.Add(box.Key);
                                ClearContainer(box.Key);
                                box.Key.KillMessage();
                            }
                            clsData.lootBoxes.Clear();
                            clsData.customBoxes.Clear();
                            wipeList.Clear();
                            SaveData();
                            SendMSG(player, string.Format(MSG("wipedData1", player.UserIDString), count, count2));
                        }
                        return;

                    default:
                        break;
                }
            }
        }
        private bool canSpawnLoot(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, "customlootspawns.admin")) return true;
            SendMSG(player, MSG("noPerms", player.UserIDString));
            return false;
        }

        #endregion Chat Commands

        #region Config

        private ConfigData configData;

        private class ConfigData
        {
            public int RespawnTimer { get; set; }
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
                RespawnTimer = 20
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);

        #endregion Config

        #region Data Management

        private void SaveData() => clsdata.WriteObject(clsData);
        private void LoadData()
        {
            try
            {
                clsData = clsdata.ReadObject<CLSData>();
            }
            catch
            {
                clsData = new CLSData();
            }
        }

        class CLSData
        {
            public Dictionary<int, CLBox> lootBoxes = new Dictionary<int, CLBox>();
            public Dictionary<string, CustomBoxData> customBoxes = new Dictionary<string, CustomBoxData>();
        }

        #endregion Data Management

        #region Classes

        private class CLBox
        {
            public float yRotation;
            public Vector3 Position;
            public BoxType boxType;
            public string customLoot;
            public int time;
        }

        private class BoxCreator
        {
            public BaseEntity entity;
            public CustomBoxData boxData;
        }

        private class CustomBoxData
        {
            public string Name = null;
            public BoxType boxType = new BoxType();
            public List<ItemStorage> itemList = new List<ItemStorage>();
        }

        private class BoxType
        {
            public string SkinName = null;
            public ulong SkinID;
            public string Type;
        }

        private class ItemStorage
        {
            public string Shortname;
            public int ID;
            public ulong SkinID;
            public int Amount;
        }

        private class UnityVector3Converter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var vector = (Vector3)value;
                writer.WriteValue($"{vector.x} {vector.y} {vector.z}");
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.String)
                {
                    var values = reader.Value.ToString().Trim().Split(' ');
                    return new Vector3(Convert.ToSingle(values[0]), Convert.ToSingle(values[1]), Convert.ToSingle(values[2]));
                }
                var o = JObject.Load(reader);
                return new Vector3(Convert.ToSingle(o["x"]), Convert.ToSingle(o["y"]), Convert.ToSingle(o["z"]));
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Vector3);
            }
        } 
        #endregion Classes

        #region Messaging

        private void SendMSG(BasePlayer player, string message) => SendReply(player, $"<color=orange>{Title}:</color> <color=#939393>{message}</color>");
        private string MSG(string key, string playerid = null) => lang.GetMessage(key, this, playerid);

        private Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"checkConsole", "Check your console for a list of boxes" },
            {"noPerms", "You do not have permission to use this command" },
            {"notType", "The number you have entered is not on the list" },
            {"notNum", "You must enter a box number" },
            {"notTime", "You must enter a valid time in seconds" },
            {"notBox", "You are not looking at a box" },
            {"notReg", "This is not a custom placed box" },
            {"removedBox", "Box deleted" },
            {"synAdd1", "<color=orange>/cls add id <opt:time></color><color=#939393>- Adds a new box, optional argument of time between respawn in seconds</color>" },
            {"createSyn", "<color=orange>/cls create yourboxname ## </color><color=#939393>- Builds a custom loot box with boxID: ## and Name: yourboxname</color>" },
            {"nameExists", "You already have a box with that name" },
            {"synRem", "<color=orange>/cls remove </color><color=#939393>- Remove the box you are looking at</color>" },
            {"synBoxes", "<color=orange>/cls boxes </color><color=#939393>- List available box types and their ID</color>" },
            {"synWipe", "<color=orange>/cls wipe </color><color=#939393>- Wipes all custom placed boxes</color>" },
            {"synList", "<color=orange>/cls list </color><color=#939393>- Puts all custom box details to console</color>" },
            {"synNear", "<color=orange>/cls near XX </color><color=#939393>- Shows custom loot boxes in radius XX</color>" },
            {"wipedAll1", "Wiped {0} custom loot spawns" },
            {"wipedData1", "Wiped {0} custom loot spawns and {1} custom loot kits" },
            {"foundBoxes", "Found {0} loot spawns near you"},
            {"noFind", "Couldn't find any boxes in radius: {0}M" },
            {"noItems", "You didnt place any items in the box" },
            {"boxCreated", "You have created a new loot box. ID: {0}, Name: {1}" },
            {"unCreateable", "You can not create custom loot for this type of box" }
        };

        #endregion Messaging

        private List<string> unCreateable = new List<string> { "barrel", "trash", "giftbox" };
    }
}
