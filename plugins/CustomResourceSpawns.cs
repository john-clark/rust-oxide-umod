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
    [Info("Custom Resource Spawns", "k1lly0u", "0.2.27", ResourceId = 1783)]
    [Description("Creates additional spawn points for resources of your choosing, that re-spawn on a timer")]
    class CustomResourceSpawns : RustPlugin
    {
        #region Fields

        private CRSData crsData;
        private DynamicConfigFile crsdata;

        private List<BaseEntity> resourceCache = new List<BaseEntity>();
        private Dictionary<int, string> resourceTypes = new Dictionary<int, string>();
        private List<Timer> refreshTimers = new List<Timer>();

        private Dictionary<ulong, int> resourceCreators = new Dictionary<ulong, int>();

        #endregion Fields

        #region Oxide Hooks

        private void Loaded()
        {
            permission.RegisterPermission("customresourcespawns.admin", this);
            lang.RegisterMessages(messages, this);
            crsdata = Interface.Oxide.DataFileSystem.GetFile("CustomSpawns/crs_data");
            crsdata.Settings.Converters = new JsonConverter[] { new StringEnumConverter(), new UnityVector3Converter() };
        }
        private void OnServerInitialized()
        {
            LoadVariables();
            LoadData();
            FindResourceTypes();
            InitializeResourceSpawns();
        }

        private void OnEntityKill(BaseNetworkable entity)
        {
            var baseEnt = entity as BaseEntity;
            if (baseEnt == null) return;
            if (resourceCache.Contains(baseEnt))
            {
                InitiateRefresh(baseEnt);
                resourceCache.Remove(baseEnt);
            }
        }
        private void OnCollectiblePickup(Item item, BasePlayer player, CollectibleEntity entity)
        {
            var ent = entity.GetEntity();
            if (ent != null)
            {
                if (resourceCache.Contains(ent))
                {
                    InitiateRefresh(ent);
                    resourceCache.Remove(ent);
                }
            }
        }
        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (resourceCreators.ContainsKey(player.userID))
                if (input.WasJustPressed(BUTTON.FIRE_PRIMARY))
                {
                    int type = resourceCreators[player.userID];
                    AddSpawn(player, type);
                }
        }
        private void Unload()
        {
            foreach (var time in refreshTimers)
                time.Destroy();

            foreach (var resource in resourceCache)
                resource.KillMessage();
            resourceCache.Clear();
        }

        #endregion Oxide Hooks

        #region Resource Control

        private void InitializeResourceSpawns()
        {
            foreach (var resource in crsData.resources)
            {
                InitializeNewSpawn(resource.Type, resource.Position);
            }
        }
        private void InitiateRefresh(BaseEntity resource)
        {
            var position = resource.transform.position;
            var type = resource.PrefabName;
            refreshTimers.Add(timer.Once(configData.RespawnTimer * 60, () =>
            {
                InitializeNewSpawn(type, position);
            }));
            resourceCache.Remove(resource);
        }
        private void InitializeNewSpawn(string type, Vector3 position)
        {
            var entity = InstantiateEntity(type, position);
            entity.enableSaving = false;
            entity.Spawn();
            resourceCache.Add(entity);
        }

        private BaseEntity InstantiateEntity(string type, Vector3 position)
        {
            var gameObject = Instantiate.GameObject(GameManager.server.FindPrefab(type), position, new Quaternion());
            gameObject.name = type;

            SceneManager.MoveGameObjectToScene(gameObject, Rust.Server.EntityScene);

            UnityEngine.Object.Destroy(gameObject.GetComponent<Spawnable>());

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            BaseEntity component = gameObject.GetComponent<BaseEntity>();
            return component;
        }

        #endregion Resource Control

        #region Resource Spawning

        private void AddSpawn(BasePlayer player, int type)
        {
            string resource = resourceTypes[type];
            var pos = GetSpawnPos(player);
            BaseEntity entity = InstantiateEntity(resource, pos);
            entity.enableSaving = false;
            entity.Spawn();
            crsData.resources.Add(new CLResource { Position = entity.transform.position, Type = resource });
            resourceCache.Add(entity);
            SaveData();
        }

        #endregion Resource Spawning

        #region Helper Methods

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
        private void FindResourceTypes()
        {
            var files = FileSystemBackend.cache;
            int i = 1;
            foreach (var str in files.Keys)
                if (str.StartsWith("assets/bundled/prefabs/autospawn/resource") || str.StartsWith("assets/bundled/prefabs/autospawn/collectable"))
                    if (!str.Contains("loot"))
                    {
                        var gmobj = GameManager.server.FindPrefab(str);
                        if (gmobj?.GetComponent<BaseEntity>() != null)
                        {
                            resourceTypes.Add(i, str);
                            i++;
                        }
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
        private BaseEntity FindResource(BasePlayer player)
        {
            Ray ray = new Ray(player.eyes.position, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward);
            RaycastHit hit;
            if (!UnityEngine.Physics.Raycast(ray, out hit, 10f))
                return null;
            return hit.GetEntity();
        }
        private BaseEntity FindResourcePos(Vector3 pos)
        {
            foreach (var entry in resourceCache)
            {
                if (entry.transform.position == pos)
                    return entry;
            }
            return null;
        }
        private List<Vector3> FindInRadius(Vector3 pos, float rad)
        {
            var foundResources = new List<Vector3>();
            foreach (var item in crsData.resources)
            {
                var itemPos = item.Position;
                if (GetDistance(pos, itemPos) < rad)
                {
                    foundResources.Add(itemPos);
                }
            }
            return foundResources;
        }
        private bool RemoveResource(BaseEntity entity)
        {
            if (resourceCache.Contains(entity))
            {
                RemoveFromData(entity.transform.position);
                resourceCache.Remove(entity);
                entity.KillMessage();
                return true;
            }
            return false;
        }
        private bool RemoveFromData(Vector3 pos)
        {
            foreach (var resource in crsData.resources)
            {
                if (GetDistance(pos, resource.Position) < 1)
                {
                    crsData.resources.Remove(resource);
                    return true;
                }
            }
            return false;
        }
        private float GetDistance(Vector3 v3, Vector3 v32) => Vector3.Distance(v3, v32);

        private void ShowResourceList(BasePlayer player)
        {
            foreach (var entry in resourceTypes)
                SendEchoConsole(player.net.connection, string.Format("{0} - {1}", entry.Key, entry.Value));
        }
        private void ShowCurrentResources(BasePlayer player)
        {
            foreach (var resource in crsData.resources)
                SendEchoConsole(player.net.connection, string.Format("{0} - {1}", resource.Position, resource.Type));
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

        #endregion Helper Methods

        #region Chat Commands

        [ChatCommand("crs")]
        private void chatResourceSpawn(BasePlayer player, string command, string[] args)
        {
            if (!canSpawnResources(player)) return;
            if (resourceCreators.ContainsKey(player.userID))
            {
                resourceCreators.Remove(player.userID);
                SendMSG(player, lang.GetMessage("endAdd", this, player.UserIDString));
                return;
            }
            if (args.Length == 0)
            {
                SendReply(player, lang.GetMessage("synAdd", this, player.UserIDString));
                SendReply(player, lang.GetMessage("synRem", this, player.UserIDString));
                SendReply(player, lang.GetMessage("synRemNear", this, player.UserIDString));
                SendReply(player, lang.GetMessage("synList", this, player.UserIDString));
                SendReply(player, lang.GetMessage("synResource", this, player.UserIDString));
                SendReply(player, lang.GetMessage("synWipe", this, player.UserIDString));
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
                            if (resourceTypes.ContainsKey(type))
                            {
                                resourceCreators.Add(player.userID, type);
                                SendMSG(player, MSG("adding", player.UserIDString));
                                return;
                            }
                            SendMSG(player, MSG("notType", player.UserIDString));
                        }
                        return;

                    case "remove":
                        {
                            if (args.Length >= 2 && args[1].ToLower() == "near")
                            {
                                float rad = 10f;
                                if (args.Length == 3) float.TryParse(args[2], out rad);

                                var resources = FindInRadius(player.transform.position, rad);
                                if (resources != null)
                                {
                                    int i = 0;
                                    foreach (var entry in resources)
                                    {
                                        var entity = FindResourcePos(entry);
                                        if (entity != null)
                                        {
                                            if (RemoveResource(entity))
                                            {
                                                i++;
                                            }
                                        }
                                    }
                                    SendMSG(player, string.Format(MSG("removedNear", player.UserIDString), i, rad));
                                    return;
                                }
                                else
                                    SendMSG(player, string.Format(MSG("noFind", player.UserIDString), rad.ToString()));
                                return;
                            }
                            var resource = FindResource(player);
                            if (resource != null)
                            {
                                if (resourceCache.Contains(resource))
                                {
                                    if (RemoveResource(resource))
                                    {
                                        SaveData();
                                        SendMSG(player, MSG("RemovedResource", player.UserIDString));
                                        return;
                                    }
                                }
                                else
                                    SendMSG(player, MSG("notReg", player.UserIDString));
                                return;
                            }
                            SendMSG(player, MSG("notBox", player.UserIDString));
                        }
                        return;

                    case "list":
                        ShowCurrentResources(player);
                        SendMSG(player, MSG("checkConsole", player.UserIDString));
                        return;

                    case "resources":
                        ShowResourceList(player);
                        SendMSG(player, MSG("checkConsole", player.UserIDString));
                        return;

                    case "near":
                        {
                            float rad = 10f;
                            if (args.Length == 2) float.TryParse(args[1], out rad);

                            var resources = FindInRadius(player.transform.position, rad);
                            if (resources != null)
                            {
                                SendMSG(player, string.Format(MSG("foundResources", player.UserIDString), resources.Count.ToString()));
                                foreach (var resource in resources)
                                    player.SendConsoleCommand("ddraw.box", 30f, Color.magenta, resource, 1f);
                            }
                            else
                                SendMSG(player, string.Format(MSG("noFind", player.UserIDString), rad.ToString()));
                        }
                        return;

                    case "wipe":
                        {
                            var count = crsData.resources.Count;
                            foreach (var resource in resourceCache)
                            {
                                resource.KillMessage();
                            }
                            crsData.resources.Clear();
                            SaveData();
                            SendMSG(player, string.Format(MSG("wipedAll1", player.UserIDString), count));
                        }
                        return;

                    default:
                        break;
                }
            }
        }
        private bool canSpawnResources(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, "customresourcespawns.admin")) return true;
            SendMSG(player, MSG("noPerms", player.UserIDString));
            return false;
        }

        #endregion Chat Commands

        #region Config

        private ConfigData configData;

        class ConfigData
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

        private void SaveData() => crsdata.WriteObject(crsData);
        private void LoadData()
        {
            try
            {
                crsData = crsdata.ReadObject<CRSData>();
            }
            catch
            {
                crsData = new CRSData();
            }
        }

        class CRSData
        {
            public List<CLResource> resources = new List<CLResource>();
        }

        #endregion Data Management

        #region Classes

        class CLResource
        {
            public string Type;
            public Vector3 Position;
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
        } // borrowed from ZoneManager

        #endregion Classes

        #region Messaging

        private void SendMSG(BasePlayer player, string message) => SendReply(player, $"<color=orange>{Title}:</color> <color=#939393>{message}</color>");
        private string MSG(string key, string playerid = null) => lang.GetMessage(key, this, playerid);

        private Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"checkConsole", "Check your console for a list of resources" },
            {"noPerms", "You do not have permission to use this command" },
            {"notType", "The number you have entered is not on the list" },
            {"notNum", "You must enter a resource number" },
            {"notBox", "You are not looking at a resource" },
            {"notReg", "This is not a custom placed resource" },
            {"RemovedResource", "Resource deleted" },
            {"synAdd", "<color=orange>/crs add id </color><color=#939393>- Adds a new resource</color>" },
            {"synRem", "<color=orange>/crs remove </color><color=#939393>- Remove the resource you are looking at</color>" },
            {"synRemNear", "<color=orange>/crs remove near <radius> </color><color=#939393>- Removes the resources within <radius> (default 10M)</color>" },
            {"synResource", "<color=orange>/crs resources </color><color=#939393>- List available resource types and their ID</color>" },
            {"synWipe", "<color=orange>/crs wipe </color><color=#939393>- Wipes all custom placed resources</color>" },
            {"synList", "<color=orange>/crs list </color><color=#939393>- Puts all custom resource details to console</color>" },
            {"synNear", "<color=orange>/crs near XX </color><color=#939393>- Shows custom resources in radius XX</color>" },
            {"wipedAll1", "Wiped {0} custom resource spawns" },
            {"foundResources", "Found {0} resource spawns near you"},
            {"noFind", "Couldn't find any resources in radius: {0}M" },
            {"adding", "You have activated the resouce tool. Look where you want to place and press shoot. Type /crs to end" },
            {"endAdd", "You have de-activated the resouce tool" },
            {"removedNear", "Removed {0} resources within a {1}M radius of your position" }
        };

        #endregion Messaging
    }
}