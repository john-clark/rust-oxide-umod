using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Random Spawner", "LaserHydra", "1.2.1", ResourceId = 1456)]
    [Description("Randomly Spawn a specific amount of an entity on the map")]
    class RandomSpawner : RustPlugin
    {
        ////////////////////////////////////////
        ///     On Plugin Init
        ////////////////////////////////////////

        void Init()
        {
            permission.RegisterPermission("randomspawner.getprefabs", this);
            permission.RegisterPermission("randomspawner.use", this);
        }

        ////////////////////////////////////////
        ///     Commands
        ////////////////////////////////////////

        [ConsoleCommand("getprefabs")]
        void ccmdGetPrefabs(ConsoleSystem.Arg arg)
        {
            if (arg == null) return;

            BasePlayer player = null;

            if (arg.Connection != null && arg.Connection.player != null)
            {
                player = arg.Connection.player as BasePlayer;

                if (!permission.UserHasPermission(player.UserIDString, "randomspawner.getprefabs"))
                    return;
            }

            List<string> prefabs = (from prefab in GameManifest.Current.pooledStrings
                                    select prefab.str).ToList();

            ConVar.Server.Log("oxide/logs/Prefabs.txt", ListToString(prefabs, 0, Environment.NewLine));

            if (player == null)
                Puts("Created a list of entity prefabs located in oxide/logs/Prefabs.txt");
            else
            {
                Puts("Created a list of entity prefabs located in oxide/logs/Prefabs.txt");
                player.ConsoleMessage("Created a list of entity prefabs located in oxide/logs/Prefabs.txt");
            }
        }

        [ConsoleCommand("rspawn")]
        void ccmdSpawnRandom(ConsoleSystem.Arg arg)
        {
            RunAsChatCommand(arg, cmdSpawnRandom);
        }

        [ChatCommand("rspawn")]
        void cmdSpawnRandom(BasePlayer player, string cmd, string[] args)
        {
            if(!permission.UserHasPermission(player.UserIDString, "randomspawner.use"))
            {
                SendChatMessage(player, "You don't have permission to use this command.");
                return;
            }

            if(args.Length != 2)
            {
                SendChatMessage(player, "Syntax: /rspawn <amount> \"<entity prefab>\"");
                return;
            }

            string prefab = args[1];
            int amount = 0;

            try
            {
                amount = Convert.ToInt32(args[0]);
            }
            catch(FormatException ex)
            {
                SendChatMessage(player, "Amount must be a number value!");
                return;
            }

            for (int i = 1; i <= amount; i++)
            {
                SpawnEntity(prefab, GetRandomVector());
            }
        }

        ////////////////////////////////////////
        ///     Entity Related
        ////////////////////////////////////////

        private void SpawnEntity(string prefab, Vector3 location)
        {
            BaseEntity entity = GameManager.server.CreateEntity(prefab, location);

            if (entity != null)
            {
                entity.Spawn();
            }
        }

        ////////////////////////////////////////
        ///     Vector Related
        ////////////////////////////////////////

        Vector3 GetRandomVector()
        {
            float max = ConVar.Server.worldsize / 2;

            float x = UnityEngine.Random.Range(max * (-1), max);
            float y = UnityEngine.Random.Range(200, 300);
            float z = UnityEngine.Random.Range(max * (-1), max);

            object terrainHeight = GetTerrainHeight(new Vector3(x, 300, z));

            if (terrainHeight is Vector3)
                return (Vector3) terrainHeight;
            else
                return new Vector3(x, y, z);
        }

        object GetTerrainHeight(Vector3 location)
        {
            int mask = LayerMask.GetMask(new string[] { "Terrain", "World", "Construction" });
            float distanceToWater = location.y - TerrainMeta.WaterMap.GetHeight(location);
            RaycastHit rayHit;
            if(Physics.Raycast(new Ray(location, Vector3.down), out rayHit, distanceToWater, mask))
            {
                return rayHit.point;
            }

            return false;
        }

        ////////////////////////////////////////
        ///     Console Command Handling
        ////////////////////////////////////////

        void RunAsChatCommand(ConsoleSystem.Arg arg, Action<BasePlayer, string, string[]> command)
        {
            if (arg == null) return;

            BasePlayer player = null;
            string cmd = string.Empty;
            string[] args = new string[0];

            if (arg.HasArgs()) args = arg.Args;
            if (arg.Connection.player == null) return;

            player = arg.Connection.player as BasePlayer;
            cmd = arg.cmd?.Name ?? "unknown";

            command(player, cmd, args);
        }

        ////////////////////////////////////////
        ///     Converting
        ////////////////////////////////////////

        string ListToString(List<string> list, int first, string seperator)
        {
            return String.Join(seperator, list.Skip(first).ToArray());
        }

        ////////////////////////////////////////
        ///     Chat Handling
        ////////////////////////////////////////

        void BroadcastChat(string prefix, string msg = null) => PrintToChat(msg == null ? prefix : "<color=#00FF8D>" + prefix + "</color>: " + msg);

        void SendChatMessage(BasePlayer player, string prefix, string msg = null) => SendReply(player, msg == null ? prefix : "<color=#00FF8D>" + prefix + "</color>: " + msg);
    }
}
