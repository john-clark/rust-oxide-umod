using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CustomSpawnPoints", "Reneb / k1lly0u", "1.1.2")]
    [Description("Allows you to set a spawnfile created via SpawnsDatabase to override Rusts default spawn points")]
    class CustomSpawnPoints : RustPlugin
    {
        #region Fields
        [PluginReference] Plugin Spawns;

        private List<Vector3> spawnPoints = new List<Vector3>();
        private List<Vector3> remainingPoints = new List<Vector3>();
        private bool initialized;

        #endregion

        #region Oxide Hooks        
        private void OnServerInitialized()
        {
            LoadVariables();

            if (Spawns)
                LoadSpawnpoints();
        }
        void OnPluginLoaded(Plugin plugin)
        {
            if (!initialized && plugin?.Title == "Spawns")            
                LoadSpawnpoints();            
        }
        object OnPlayerRespawn(BasePlayer player)
        {
            if (!initialized) return null;

            object position = GetSpawnPoint();
            if (position is Vector3)            
                return new BasePlayer.SpawnPoint() { pos = (Vector3)position, rot = new Quaternion(0, 0, 0, 1) };
            return null;
        }
        #endregion

        #region Functions
        private void LoadSpawnpoints()
        {
            initialized = false;
            if (string.IsNullOrEmpty(configData.Spawnfile))
            {
                PrintError("No spawnfile set in the config. Unable to continue");
                return;
            }

            object success = Spawns?.Call("LoadSpawnFile", configData.Spawnfile);
            if (success is List<Vector3>)
            {
                spawnPoints = success as List<Vector3>;
                if (spawnPoints.Count == 0)
                {
                    PrintError("Loaded spawnfile contains no spawn points. Unable to continue");
                    return;
                }
                PrintWarning($"Successfully loaded {spawnPoints.Count} spawn points");
            }
            else
            {
                PrintError($"Unable to load the specified spawnfile: {configData.Spawnfile}");
                return;
            }
            remainingPoints = new List<Vector3>(spawnPoints);
            initialized = true;
        }
        object GetSpawnPoint(int attempt = 0)
        {
            if (attempt >= 10)
                return null;

            var position = remainingPoints.GetRandom();

            List<BaseEntity> entities = Facepunch.Pool.GetList<BaseEntity>();
            Vis.Entities(position, configData.Detect, entities, LayerMask.GetMask("Construction", "Deployable"));
            int count = entities.Count;
            Facepunch.Pool.FreeList(ref entities);

            remainingPoints.Remove(position);
            if (remainingPoints.Count == 0)            
                remainingPoints = new List<Vector3>(spawnPoints);            

            if (count > 0) 
                return GetSpawnPoint(++attempt);

            return position;
        }
        #endregion

        #region Commands
        [ConsoleCommand("spawns.config")]
        private void ccmdSpawnFile(ConsoleSystem.Arg arg)
        {
            if (arg == null)
                return;
          
            if (arg.Connection == null || (arg.Connection != null && arg.Connection.authLevel == 2))
            {
                if (arg.Args == null || arg.Args.Length == 0)
                {
                    SendReply(arg, "spawns.config \"spawnfile name\" - Set a new spawnfile");
                    return;
                }

                if (!Spawns)
                {
                    SendReply(arg, "Unable to find SpawnsDatabase. Unable to continue...");
                    return;
                }

                object success = Spawns.Call("GetSpawnsCount", new object[] { arg.Args[0] });
                if (success is string)
                {
                    SendReply(arg, $"Unable to load the specified spawnfile: {arg.Args[0]}");
                    return;
                }

                if (success is int)
                {
                    if ((int)success == 0)
                    {
                        PrintError("Loaded spawnfile contains no spawn points. Unable to continue");
                        return;
                    }
                    configData.Spawnfile = arg.Args[0];
                    SaveConfig(configData);
                    LoadSpawnpoints();
                }                
            }
            else SendReply(arg, "You do not have permission to use this command");           
        }
        #endregion

        #region Config        
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Spawnfile name")]
            public string Spawnfile { get; set; }
            [JsonProperty(PropertyName = "Entity detection radius")]
            public float Detect { get; set; }            
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
                Spawnfile = string.Empty,
                Detect = 10
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion       
    }
}
