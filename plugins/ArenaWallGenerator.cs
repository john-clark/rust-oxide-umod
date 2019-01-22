using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Arena Wall Generator", "nivex", "1.0.3")]
    [Description("An easy to use arena wall generator.")]
    public class ArenaWallGenerator : RustPlugin
    {
        private static readonly string hewwPrefab = "assets/prefabs/building/wall.external.high.wood/wall.external.high.wood.prefab";
        private static readonly string heswPrefab = "assets/prefabs/building/wall.external.high.stone/wall.external.high.stone.prefab";
        private readonly int wallMask = LayerMask.GetMask("Terrain", "World", "Default", "Construction", "Deployed");
        private readonly int worldMask = LayerMask.GetMask("World");
        private StoredData storedData = new StoredData();

        public class StoredData
        {
            public readonly Dictionary<string, float> Arenas = new Dictionary<string, float>();
            public StoredData() { }
        }

        private void OnServerSave()
        {
            timer.Once(5f, () => SaveData());
        }

        private void Unload()
        {
            SaveData();
        }

        private void Loaded()
        {
            try
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            catch { }

            if (storedData?.Arenas == null)
                storedData = new StoredData();

            LoadVariables();

            if (!respawnWalls)
            {
                Unsubscribe(nameof(OnEntityKill));
                Unsubscribe(nameof(OnEntityDeath));
            }
        }

        private void OnEntityKill(BaseNetworkable entity)
        {
            var e = entity as BaseEntity;

            if (e?.transform != null && e.ShortPrefabName.Contains("wall.external.high"))
            {
                RecreateZoneWall(e.PrefabName, e.transform.position, e.transform.rotation, e.OwnerID);
            }
        }

        private void OnEntityDeath(BaseEntity entity, HitInfo hitInfo)
        {
            if (entity?.transform != null && entity.ShortPrefabName.Contains("wall.external.high"))
            {
                RecreateZoneWall(entity.PrefabName, entity.transform.position, entity.transform.rotation, entity.OwnerID);
            }
        }

        public void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        }

        public void RecreateZoneWall(string prefab, Vector3 pos, Quaternion rot, ulong ownerId)
        {
            if (ArenaTerritory(pos) && storedData.Arenas.Any(entry => GetOwnerId(entry.Key) == ownerId))
            {
                var e = GameManager.server.CreateEntity(prefab, pos, rot, false);

                if (e != null)
                {
                    e.OwnerID = ownerId;
                    e.Spawn();
                    e.gameObject.SetActive(true);
                }
            }
        }

        public bool ArenaTerritory(Vector3 position, float offset = 5f)
        {
            var currentPos = new Vector3(position.x, 0f, position.z);

            foreach (var zone in storedData.Arenas)
            {
                var key = zone.Key.ToVector3();
                var zonePos = new Vector3(key.x, 0f, key.z);

                if (Vector3.Distance(zonePos, currentPos) <= zone.Value + offset)
                    return true;
            }

            return false;
        }

        public string FormatPosition(Vector3 position)
        {
            string x = position.x.ToString("N2");
            string y = position.y.ToString("N2");
            string z = position.z.ToString("N2");

            return $"{x} {y} {z}";
        }

        public ulong GetOwnerId(string uid)
        {
            return Convert.ToUInt64(Math.Abs(uid.GetHashCode()));
        }

        public bool ZoneWallsExist(ulong ownerId, List<BaseEntity> entities)
        {
            if (entities == null || entities.Count < 3)
                entities = BaseNetworkable.serverEntities.Where(e => e?.name != null && e.name.Contains("wall.external.high")).Cast<BaseEntity>().ToList();

            return entities.Any(entity => entity.OwnerID == ownerId);
        }

        public bool RemoveCustomZoneWalls(Vector3 center)
        {
            foreach (var entry in storedData.Arenas.ToList())
            {
                if (Vector3.Distance(entry.Key.ToVector3(), center) <= entry.Value)
                {
                    ulong ownerId = GetOwnerId(entry.Key);
                    storedData.Arenas.Remove(entry.Key);
                    RemoveZoneWalls(ownerId);
                    return true;
                }
            }

            return false;
        }

        public int RemoveZoneWalls(ulong ownerId)
        {
            int removed = 0;

            if (respawnWalls)
            {
                Unsubscribe(nameof(OnEntityKill));
            }

            foreach (var entity in BaseNetworkable.serverEntities.Where(e => e?.name != null && e.name.Contains("wall.external.high")).Cast<BaseEntity>().ToList())
            {
                if (entity.OwnerID == ownerId)
                {
                    entity.Kill();
                    removed++;
                }
            }

            if (respawnWalls)
            {
                Subscribe(nameof(OnEntityKill));
            }

            return removed;
        }

        public List<Vector3> GetCircumferencePositions(Vector3 center, float radius, float next, float y)
        {
            var positions = new List<Vector3>();
            float degree = 0f;

            while (degree < 360)
            {
                float angle = (float)(2 * Math.PI / 360) * degree;
                float x = center.x + radius * (float)Math.Cos(angle);
                float z = center.z + radius * (float)Math.Sin(angle);
                var position = new Vector3(x, center.y, z);

                position.y = y == 0f ? TerrainMeta.HeightMap.GetHeight(position) : y;
                positions.Add(position);

                degree += next;
            }

            return positions;
        }

        public bool CreateZoneWalls(Vector3 center, float radius, string prefab, List<BaseEntity> entities, BasePlayer player = null)
        {
            var tick = DateTime.Now;
            ulong ownerId = GetOwnerId(center.ToString());

            //if (ZoneWallsExist(ownerId, entities))
                //return true;

            float maxHeight = -200f;
            float minHeight = 200f;
            int spawned = 0;
            int raycasts = Mathf.CeilToInt(360 / radius * 0.1375f);

            foreach (var position in GetCircumferencePositions(center, radius, raycasts, 0f))
            {
                RaycastHit hit;
                if (Physics.Raycast(new Vector3(position.x, position.y + 200f, position.z), Vector3.down, out hit, Mathf.Infinity, wallMask))
                {
                    maxHeight = Mathf.Max(hit.point.y, maxHeight);
                    minHeight = Mathf.Min(hit.point.y, minHeight);
                    center.y = minHeight;
                }
            }

            float gap = prefab == heswPrefab ? 0.3f : 0.5f;
            int stacks = Mathf.CeilToInt((maxHeight - minHeight) / 6f) + extraWallStacks;
            float next = 360 / radius - gap;

            for (int i = 0; i < stacks; i++)
            {
                foreach (var position in GetCircumferencePositions(center, radius, next, center.y))
                {
                    float groundHeight = TerrainMeta.HeightMap.GetHeight(new Vector3(position.x, position.y + 6f, position.z));

                    if (groundHeight > position.y + 9f)
                        continue;

                    if (useLeastAmount && position.y - groundHeight > 6f + extraWallStacks * 6f)
                        continue;

                    var entity = GameManager.server.CreateEntity(prefab, position, default(Quaternion), false);

                    if (entity != null)
                    {
                        entity.OwnerID = ownerId;
                        entity.transform.LookAt(center, Vector3.up);
                        entity.Spawn();
                        entity.gameObject.SetActive(true);
                        spawned++;
                    }
                    else
                        return false;

                    if (stacks == i - 1)
                    {
                        RaycastHit hit;
                        if (Physics.Raycast(new Vector3(position.x, position.y + 6f, position.z), Vector3.down, out hit, 12f, worldMask))
                            stacks++;
                    }
                }

                center.y += 6f;
            }

            if (player == null)
                Puts(msg("GeneratedWalls", null, spawned, stacks, FormatPosition(center), (DateTime.Now - tick).TotalSeconds));
            else
                player.ChatMessage(msg("GeneratedWalls", player.UserIDString, spawned, stacks, FormatPosition(center), (DateTime.Now - tick).TotalSeconds));

            return true;
        }

        private bool API_CreateZoneWalls(Vector3 center, float radius)
        {
            if (CreateZoneWalls(center, radius, useWoodenWalls ? hewwPrefab : heswPrefab, null, null))
            {
                storedData.Arenas[center.ToString()] = radius;
                return true;
            }

            return false;
        }

        private bool API_RemoveZoneWalls(Vector3 center)
        {
            storedData.Arenas.Remove(center.ToString());

            return RemoveCustomZoneWalls(center);
        }

        private void CommandWalls(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
                return;

            if (args.Length >= 1)
            {
                float radius;
                if (float.TryParse(args[0], out radius) && radius > 2f)
                {
                    if (radius > maxCustomWallRadius)
                        radius = maxCustomWallRadius;

                    RaycastHit hit;
                    if (Physics.Raycast(player.eyes.HeadRay(), out hit, Mathf.Infinity, wallMask))
                    {
                        string prefab = useWoodenWalls ? hewwPrefab : heswPrefab;

                        if (args.Any(arg => arg.ToLower().Contains("stone")))
                            prefab = heswPrefab;
                        else if (args.Any(arg => arg.ToLower().Contains("wood")))
                            prefab = hewwPrefab;

                        storedData.Arenas[hit.point.ToString()] = radius;
                        CreateZoneWalls(hit.point, radius, prefab, null, player);
                        player.ChatMessage("ok");
                    }
                    else
                        player.ChatMessage(msg("FailedRaycast", player.UserIDString));
                }
                else
                    player.ChatMessage(msg("InvalidNumber", player.UserIDString, args[0]));
            }
            else
            {
                if (!RemoveCustomZoneWalls(player.transform.position))
                    player.ChatMessage(msg("WallSyntax", player.UserIDString, chatCommandName));

                foreach (var entry in storedData.Arenas)
                    player.SendConsoleCommand("ddraw.text", 30f, Color.yellow, entry.Key.ToVector3(), entry.Value);
            }
        }

        #region Config

        private bool Changed;
        private int extraWallStacks;
        private bool useLeastAmount;
        private float maxCustomWallRadius;
        private bool useWoodenWalls;
        private string chatCommandName;
        private bool respawnWalls;

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["GeneratedWalls"] = "Generated {0} arena walls {1} high at {2} in {3}ms",
                ["FailedRaycast"] = "Look towards the ground, and try again.",
                ["InvalidNumber"] = "Invalid number: {0}",
                ["WallSyntax"] = "Use <color=orange>/{0} [radius] <wood|stone></color>, or stand inside of an existing arena and use <color=orange>/{0}</color> to remove it."
            }, this);
        }

        private void LoadVariables()
        {
            extraWallStacks = Convert.ToInt32(GetConfig("Settings", "Extra High External Wall Stacks", 2));
            useLeastAmount = Convert.ToBoolean(GetConfig("Settings", "Create Least Amount Of Walls", false));
            maxCustomWallRadius = Convert.ToSingle(GetConfig("Settings", "Maximum Arena Radius", 300f));
            useWoodenWalls = Convert.ToBoolean(GetConfig("Settings", "Use Wooden Walls", false));
            chatCommandName = Convert.ToString(GetConfig("Settings", "Chat Command", "awg"));
            respawnWalls = Convert.ToBoolean(GetConfig("Settings", "Respawn Zone Walls On Death", false));

            cmd.AddChatCommand(chatCommandName, this, CommandWalls);

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Config.Clear();
            LoadVariables();
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        public string msg(string key, string id = null, params object[] args)
        {
            string message = id == null ? RemoveFormatting(lang.GetMessage(key, this, id)) : lang.GetMessage(key, this, id);

            return args.Length > 0 ? string.Format(message, args) : message;
        }

        public string RemoveFormatting(string source)
        {
            return source.Contains(">") ? Regex.Replace(source, "<.*?>", string.Empty) : source;
        }

        #endregion
    }
}
