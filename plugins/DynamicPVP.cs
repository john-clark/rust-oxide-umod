using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("DynamicPVP", "CatMeat", "3.2.4", ResourceId = 2728)]
    [Description("Create temporary PVP zones around SupplyDrops, LockedCrates, APC and/or Heli")]

    public class DynamicPVP : RustPlugin
    {
        #region References
        [PluginReference]
        Plugin ZoneManager, TruePVE, ZoneDomes, BotSpawn;
        #endregion

        #region Declarations
        ConfigFileStructure Settings = new ConfigFileStructure();
        DataProfile BotSpawnProfileSettings = new DataProfile();

        Dictionary<BaseEntity, Vector3> activeSupplySignals = new Dictionary<BaseEntity, Vector3>();
        Dictionary<string, Vector3> ActiveDynamicZones = new Dictionary<string, Vector3>();
        Dictionary<ulong, Timer> PVPDelay = new Dictionary<ulong, Timer>();

        bool starting = true;
        bool validcommand;

        float zoneRadius;
        float zoneDuration;

        string botProfile;
        string msg;
        static string PluginVersion;
        string debugfilename = "debug";
        string BotSpawnProfileName = "DynamicPVP";

        ConsoleSystem.Arg arguments;
        #endregion

        #region Commands
        [ChatCommand("dynpvp")]
        private void CmdChatCommand(BasePlayer player, string command, string[] args)
        {
            if (player?.net?.connection != null && player.net.connection.authLevel > 0)
                if (args.Count() > 0) ProcessCommand(player, args);
        }

        [ConsoleCommand("dynpvp")]
        private void CmdConsoleCommand(ConsoleSystem.Arg arg)
        {
            arguments = arg; //save for responding later
            if (arg.IsAdmin)
                if (arg.Args.Count() > 0) ProcessCommand(null, arg.Args);
        }

        private void ProcessCommand(BasePlayer player, string[] args)
        {
            var command = args[0];
            var value = "";

            if (args.Count() > 1) value = args[1];

            var commandToLower = command.Trim().ToLower();
            var valueToLower = value.Trim().ToLower();
            float numberValue;
            var number = Single.TryParse(value, out numberValue);

            validcommand = true;

            switch (commandToLower)
            {
                case "debug":
                    switch (valueToLower)
                    {
                        case "true":
                            Settings.Global.DebugEnabled = true;
                            break;
                        case "false":
                            Settings.Global.DebugEnabled = false;
                            break;
                        default:
                            validcommand = false;
                            break;
                    }
                    if (validcommand) SaveConfig(Settings);
                    break;
                default:
                    validcommand = false;
                    break;
            }
            if (validcommand)
            {
                msg = "DynamicPVP: " + command + " set to: " + value;
                arguments.ReplyWith(msg);
            }
            else
            {
                msg = "Syntax error! (" + command + ":" + value;
                arguments.ReplyWith(msg);
            }
        }
        #endregion

        #region OxideHooks
        void Init()
        {
            PluginVersion = Version.ToString();
            LoadConfigVariables();
        }

        void Unload()
        {
            List<string> keys = new List<string>(ActiveDynamicZones.Keys);

            if (keys.Count > 0) DebugPrint($"Deleting {keys.Count} ActiveZones", false);
            foreach (string key in keys) DeleteDynZone(key);
        }

        object CanEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (!Settings.Global.PVPDelayEnabled) return null;
            if (entity is BasePlayer)
            {
                var player = entity as BasePlayer;

                if (PVPDelay.ContainsKey(player.userID)) return true; // force allow damage
            }
            return null; //allow default behavior
        }

        void OnServerInitialized()
        {
            starting = false;
            if (BotSpawnAllowed()) BotSpawnProfileCreate();
            DeleteOldZones();
            DeleteOldMappings();
            DeleteOldDomes();
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (!Settings.Global.PluginEnabled || starting || entity == null || entity.IsDestroyed) return;
            switch (entity.ShortPrefabName)
            {
                case "supply_drop":
                    SupplyDropEvent(entity);
                    break;
                case "codelockedhackablecrate":
                    LockedCrateEvent(entity);
                    break;
                default:
                    return;
            }
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!Settings.Global.PluginEnabled || starting || entity == null || entity.IsDestroyed) return;
            switch (entity.ShortPrefabName)
            {
                case "patrolhelicopter":
                    PatrolHelicopterEvent(entity);
                    break;
                case "bradleyapc":
                    BradleyApcEvent(entity);
                    break;
                default:
                    return;
            }
        }

        void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (entity == null || !(entity is SupplySignal))
                return;
            if (entity.net == null)
                entity.net = Network.Net.sv.CreateNetworkable();

            Vector3 position = entity.transform.position;

            if (activeSupplySignals.ContainsKey(entity))
                return;
            SupplyThrown(player, entity, position);
            return;
        }

        void OnExplosiveDropped(BasePlayer player, BaseEntity entity)
        {
            if (entity == null || !(entity is SupplySignal)) return;
            if (activeSupplySignals.ContainsKey(entity)) return;

            Vector3 position = entity.transform.position;
            SupplyThrown(player, entity, position);
            return;
        }
        #endregion

        #region Entity Testing and Tracking
        bool IsProbablySupplySignal(Vector3 landingposition)
        {
            // potential issues with signals thrown near each other (<40m)
            // definite issues with modifications that create more than one supply drop per cargo plane.
            // potential issues with player moving while throwing signal.

            DebugPrint($"Checking {activeSupplySignals.Count()} active supply signals", false);
            //if (!Settings.Events.SupplySignal.Enabled) return false;
            if (activeSupplySignals.Count() > 0)
            {
                foreach (var supplysignal in activeSupplySignals.ToList())
                {
                    if (supplysignal.Key == null)
                    {
                        activeSupplySignals.Remove(supplysignal.Key);

                        continue;
                    }

                    var thrownposition = supplysignal.Value;
                    thrownposition.y = 0;
                    landingposition.y = 0;
                    var distance = Vector3.Distance(thrownposition, landingposition);
                    DebugPrint($"Found SupplySignal at {supplysignal.Value} located {distance}m away.", false);

                    if (distance < Settings.Global.CompareRadius)
                    {
                        //activeSupplySignals.Remove(supplysignal.Key);
                        timer.Once(10, () => {
                            if (activeSupplySignals.ContainsKey(supplysignal.Key))
                            {
                                activeSupplySignals.Remove(supplysignal.Key);
                                DebugPrint($"Removing Supply signal from active list", false);
                            }
                        });

                        DebugPrint("Found matching SupplySignal.", false);
                        DebugPrint($"Active supply signals remaining: {activeSupplySignals.Count()}", false);
                        return true;
                    }
                }
                DebugPrint($"No matches found, probably from a timed event cargo_plane", false);
                return false;
            }
            DebugPrint($"No active signals, must be from a timed event cargo_plane", false);
            return false;
        }

        void SupplyThrown(BasePlayer player, BaseEntity entity, Vector3 position)
        {
            //Vector3 thrownposition = entity.transform.position;

            timer.Once(2.0f, () =>
            {
                if (entity == null)
                {
                    activeSupplySignals.Remove(entity);
                    return;
                }
            });

            timer.Once(2.3f, () =>
            {
                if (entity == null) return;
                activeSupplySignals.Add(entity, position);
                DebugPrint($"SupplySignal thrown at position of {position}", false);
            });
        }
        #endregion

        #region Events
        private void SupplyDropEvent(BaseNetworkable entity)
        {
            DebugPrint($"Supply drop spawned at {entity.transform.position}", false);
            bool IsFromSupplySignal = IsProbablySupplySignal(entity.transform.position);

            DebugPrint($"IsFromSupplySignal: {IsFromSupplySignal}", false);
            if (IsFromSupplySignal)
            {
                DebugPrint($"Settings.Events.SupplySignal.Enabled: {Settings.Events.SupplySignal.Enabled}", false);
                if (!Settings.Events.SupplySignal.Enabled)
                {
                    DebugPrint($"PVP for Supply Signals disabled: Skipping zone creation", false);
                    return;
                }
                CreateDynZone("Signal",
                    entity.transform.position,
                    Settings.Events.SupplySignal.Radius,
                    Settings.Events.SupplySignal.Duration,
                    Settings.Events.SupplySignal.BotProfile
                    );
            }
            else
            {
                if (!Settings.Events.TimedDrop.Enabled) return;
                CreateDynZone("AirDrop",
                    entity.transform.position,
                    Settings.Events.TimedDrop.Radius,
                    Settings.Events.TimedDrop.Duration,
                    Settings.Events.TimedDrop.BotProfile
                    );
            }
        }

        private void LockedCrateEvent(BaseNetworkable entity)
        {
            if (!Settings.Events.TimedCrate.Enabled) return;
            CreateDynZone("Crate",
                entity.transform.position,
                Settings.Events.TimedCrate.Radius,
                Settings.Events.TimedCrate.Duration,
                Settings.Events.TimedCrate.BotProfile
                );
        }

        private void PatrolHelicopterEvent(BaseCombatEntity entity)
        {
            if (!Settings.Events.PatrolHelicopter.Enabled) return;
            CreateDynZone("Heli",
                entity.transform.position,
                Settings.Events.PatrolHelicopter.Radius,
                Settings.Events.PatrolHelicopter.Duration,
                Settings.Events.PatrolHelicopter.BotProfile
                );
        }

        private void BradleyApcEvent(BaseCombatEntity entity)
        {
            if (!Settings.Events.BradleyAPC.Enabled) return;
            CreateDynZone("APC",
                entity.transform.position,
                Settings.Events.BradleyAPC.Radius,
                Settings.Events.BradleyAPC.Duration,
                Settings.Events.BradleyAPC.BotProfile
                );
        }
        #endregion

        #region ZoneHandling
        void CreateDynZone(string EventID, Vector3 DynPosition, float _radius, float _duration, string _profile)
        {
            DynPosition.y = TerrainMeta.HeightMap.GetHeight(DynPosition);

            if (ZoneCreateAllowed())
            {
                string DynZoneID = DateTime.Now.ToString("HHmmssff");

                List<string> DynArgs = new List<string>
                {
                    "name",
                    "DynamicPVP",
                    "radius",
                    _radius.ToString(),
                    "enter_message",
                    "Entering a PVP area!",
                    "leave_message",
                    "Leaving a PVP area.",
                    "undestr",
                    "true"
                };

                if (Settings.Global.BlockTeleport)
                {
                    DynArgs.Add("notp");
                    DynArgs.Add("true");
                }
                if (!String.IsNullOrEmpty(Settings.Global.ExtraZoneFlags))
                {
                    List<string> _xtraArgs = Settings.Global.ExtraZoneFlags.Split(' ').ToList();

                    foreach (var _arg in _xtraArgs)
                    {
                        DynArgs.Add(_arg);
                    }
                }

                string[] DynZoneArgs = DynArgs.ToArray();
                DebugPrint($"EventID {DynZoneID} {EventID}{DynPosition} {_radius.ToString()}m {_duration.ToString()}s", false);
                bool ZoneAdded = AddZone(DynZoneID, DynZoneArgs, DynPosition);

                if (ZoneAdded)
                {
                    string successString = "";
                    ActiveDynamicZones.Add(DynZoneID, DynPosition);
                    bool MappingAdded = AddMapping(DynZoneID);

                    if (!MappingAdded) DebugPrint("ERROR: PVP Mapping failed.", true);
                    else successString = successString + " Mapping,";
                    if (DomeCreateAllowed())
                    {
                        bool DomeAdded = AddDome(DynZoneID);

                        if (!DomeAdded) DebugPrint("ERROR: Dome NOT added for Zone: " + DynZoneID, true);
                        else successString = successString + " Dome,";
                    }
                    if (BotSpawnAllowed())
                    {
                        bool botsSpawned = SpawnBots(DynPosition, _profile, DynZoneID);

                        if (botsSpawned) successString = successString + " Bots,";
                    }
                    timer.Once(_duration, () => { DeleteDynZone(DynZoneID); });
                    if (successString.EndsWith(",")) successString = successString.Substring(0, successString.Length - 1);
                    DebugPrint($"Created Zone {DynZoneID} ({successString.Trim()})", false);
                }
                else DebugPrint("ERROR: Zone creation failed.", true);
            }
        }

        bool DeleteDynZone(string DynZoneID)
        {
            if (ZoneCreateAllowed())
            {
                string successString = "";

                if (String.IsNullOrEmpty(DynZoneID))
                {
                    DebugPrint("Invalid ZoneID", false);
                    return false;
                }
                if (BotSpawnAllowed())
                {
                    DebugPrint("Calling RemoveBots", false);

                    bool botsRemoved = RemoveBots(DynZoneID);

                    if (botsRemoved) successString = successString + " Bots,";
                }
                if (DomeCreateAllowed())
                {
                    DebugPrint("Calling RemoveDome", false);

                    bool DomeRemoved = RemoveDome(DynZoneID);

                    if (!DomeRemoved) DebugPrint("ERROR: Dome NOT removed for Zone: " + DynZoneID, true);
                    else successString = successString + " Dome,";
                }
                DebugPrint("Calling RemoveMapping", false);

                bool MappingRemoved = RemoveMapping(DynZoneID);

                if (!MappingRemoved) DebugPrint("ERROR: PVP NOT disabled for Zone: " + DynZoneID, true);
                else successString = successString + " Mapping,";

                DebugPrint("Calling RemoveZone", false);

                bool ZoneRemoved = RemoveZone(DynZoneID);

                if (!ZoneRemoved) DebugPrint("ERROR: Zone removal failed.", true);
                else
                {
                    if (successString.EndsWith(",")) successString = successString.Substring(0, successString.Length - 1);
                    DebugPrint($"Deleted Zone {DynZoneID} ({successString.Trim()})", false);
                    ActiveDynamicZones.Remove(DynZoneID);
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region ZoneDome Integration
        bool AddDome(string zoneID) => (bool)ZoneDomes?.Call("AddNewDome", null, zoneID);

        bool RemoveDome(string zoneID) => (bool)ZoneDomes?.Call("RemoveExistingDome", null, zoneID);

        private void DeleteOldDomes()
        {
        }

        private bool DomeCreateAllowed()
        {
            Plugin ZoneDomes = (Plugin)plugins.Find("ZoneDomes");

            if (ZoneDomes != null && Settings.Global.DomesEnabled) return true;
            return false;
        }
        #endregion

        #region TruePVE Integration
        bool AddMapping(string zoneID) => (bool)TruePVE?.Call("AddOrUpdateMapping", zoneID, "exclude");

        bool RemoveMapping(string zoneID) => (bool)TruePVE?.Call("RemoveMapping", zoneID);

        private void DeleteOldMappings()
        {
        }
        #endregion

        #region BotSpawn Integration
        bool SpawnBots(Vector3 zoneLocation, string zoneProfile, string zoneGroupID)
        {
            if (!String.IsNullOrEmpty(zoneProfile))
            {
                string[] result = (string[])BotSpawn?.CallHook("AddGroupSpawn", zoneLocation, zoneProfile, zoneGroupID);

                if (result == null || result.Length < 2)
                {
                    DebugPrint("AddGroupSpawn returned invalid response.", false);
                    return false;
                }

                switch (result[0])
                {
                    case "true":
                        return true;
                    case "false":
                        return false;
                    case "error":
                        DebugPrint($"ERROR: AddGroupSpawn failed: {result[1]}", true);
                        return false;
                    default:
                        return false;
                }
            }
            return false;
        }

        bool RemoveBots(string zoneGroupID)
        {
            string[] result = (string[])BotSpawn?.CallHook("RemoveGroupSpawn", zoneGroupID);

            if (result == null || result.Length < 2)
            {
                DebugPrint("RemoveGroupSpawn returned invalid response.", false);
                return false;
            }
            else if (result[0] == "error")
            {
                DebugPrint($"ERROR: RemoveGroupSpawn failed: {result[1]}", true);
                return false;
            }
            else return true;
        }

        string[] CheckProfile(string profile) => (string[])BotSpawn?.CallHook("ProfileExists", profile);

        string[] AddProfile(DataProfile profile) => (string[])BotSpawn?.CallHook("CreateNewProfile", "DynamicPVP", profile);

        private void DeleteOldBots()
        {
        }

        private bool BotSpawnAllowed()
        {
            Plugin BotSpawn = (Plugin)plugins.Find("BotSpawn");

            if (BotSpawn != null && Settings.Global.BotsEnabled) return true;
            return false;
        }
        #endregion

        #region ZoneManager Integration
        private bool ZoneCreateAllowed()
        {
            Plugin ZoneManager = (Plugin)plugins.Find("ZoneManager");
            Plugin TruePVE = (Plugin)plugins.Find("TruePVE");

            if ((TruePVE != null) && (ZoneManager != null))
                if (Settings.Global.PluginEnabled)
                    return true;
            return false;
        }

        private void DeleteOldZones()
        {
            int _attempts = 0;
            int _sucesses = 0;
            string[] ZoneIDs = (string[])ZoneManager?.Call("GetZoneIDs");

            if (ZoneIDs != null)
            {
                for (int i = 0; i < ZoneIDs.Length; i++)
                {
                    string zoneName = (string)ZoneManager?.Call("GetZoneName", ZoneIDs[i]);

                    if (zoneName == "DynamicPVP")
                    {
                        _attempts++;

                        bool _success = DeleteDynZone(ZoneIDs[i]);

                        if (_success) _sucesses++;
                    }
                }
                DebugPrint($"Deleted {_sucesses} of {_attempts} existing DynamicPVP zones", true);
            }
        }

        bool AddZone(string zoneID, string[] zoneArgs, Vector3 zoneLocation) => (bool)ZoneManager?.Call("CreateOrUpdateZone", zoneID, zoneArgs, zoneLocation);

        bool RemoveZone(string zoneID) => (bool)ZoneManager?.Call("EraseZone", zoneID);

        private void OnEnterZone(string zoneID, BasePlayer player)
        {
            string zoneName = (string)ZoneManager?.Call("GetZoneName", zoneID);

            if (zoneName == "DynamicPVP")
            {
                var _name = GetPlayerName(player);

                if (_name != null) DebugPrint($"{_name} has entered PVP Zone {zoneID}.", true);
            }
        }

        private void OnExitZone(string zoneID, BasePlayer player)
        {
            if (!Settings.Global.PVPDelayEnabled) return;

            string zoneName = (string)ZoneManager?.Call("GetZoneName", zoneID);

            if (zoneName != "DynamicPVP" || player is NPCPlayer) return;
            if (player.userID < 76560000000000000L) return;
            if (!player.userID.IsSteamId()) return;

            var _name = GetPlayerName(player);

            if (_name == null) return;
            DebugPrint($"{_name} has left a PVP Zone {zoneID}.", true);

            if (!PVPDelay.ContainsKey(player.userID))
            {
                var time = Settings.Global.PVPDelayTime;
                DebugPrint($"Adding {player.displayName} to PVPDelay.", true);
                PVPDelay.Add(player.userID, timer.Repeat(1, time, () =>
                {
                    time--;
                    if (time == 0)
                    {
                        DebugPrint($"Remove {player.displayName} from PVPDelay.", true);
                        PVPDelay.Remove(player.userID);
                    }
                }));
            }
        }
        #endregion

        #region Messaging
        void DebugPrint(string msg, bool warning)
        {
            if (Settings.Global.DebugEnabled)
            {
                switch (warning)
                {
                    case true:
                        PrintWarning(msg);
                        break;
                    case false:
                        Puts(msg);
                        break;
                }
            }

            LogToFile(debugfilename, "[" + DateTime.Now.ToString() + "] | " + msg, this, true);
        }

        void RespondWith(BasePlayer player, string msg)
        {
            if (player == null)
                arguments.ReplyWith(msg);
            else
                SendReply(player, msg);
            return;
        }
        #endregion

        #region Classes
        private class ActiveZone
        {
            public string DynZoneID { get; set; }
        }

        private void BotSpawnProfileCreate()
        {
            string[] result = (string[])BotSpawn?.CallHook("ProfileExists", "DynamicPVP");

            if (true)
            //if (result[0] == "false")
            {
                //DebugPrint("BotsSpawn Does not contain custom profile `DynamicPVP`.", true);

                var _profile = JsonConvert.SerializeObject(new DataProfile());

                result = (string[])BotSpawn?.CallHook("CreateNewProfile", BotSpawnProfileName, _profile);
                if (result[0] == "false") DebugPrint($"BotsSpawn failed to add/update `DynamicPVP`.\n{result[1]}", true);
                else
                {
                    result = (string[])BotSpawn?.CallHook("ProfileExists", "DynamicPVP");

                    if (result[0] == "false") DebugPrint($"Added but failed show `DynamicPVP`.\n{result[1]}", true);
                    else DebugPrint("Succesfully updated custom profile `DynamicPVP`.", true);
                }
            }
            else DebugPrint("Custom profile `DynamicPVP` already exists.", true);
        }

        public class DataProfile
        {
            public bool AutoSpawn = false;
            public bool Murderer = false;
            public int Bots = 2;
            public int BotHealth = 100;
            public int Radius = 20;
            public List<string> Kit = new List<string>();
            public string BotNamePrefix = "Sgt.";
            public List<string> BotNames = new List<string>();
            public int Bot_Accuracy = 4;
            public float Bot_Damage = 0.4f;
            public bool Disable_Radio = false;
            public int Roam_Range = 20;
            public bool Peace_Keeper = true;
            public int Peace_Keeper_Cool_Down = 5;
            public bool Weapon_Drop = true;
            public bool Keep_Default_Loadout = false;
            public bool Wipe_Belt = false;
            public bool Wipe_Clothing = false;
            public bool Allow_Rust_Loot = true;
            public int Suicide_Timer = 1200;
            public bool Chute = false;
            public int Long_Attack_Distance = 120;
            public int Respawn_Timer = 0;
            public float LocationX;
            public float LocationY;
            public float LocationZ;
            public string Parent_Monument = "";
        }

        #endregion

        #region NewConfig

        private void LoadConfigVariables()
        {
            Settings = Config.ReadObject<ConfigFileStructure>();
            SaveConfig(Settings);
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");

            var config = new ConfigFileStructure();
            SaveConfig(config);
        }

        void SaveConfig(ConfigFileStructure config)
        {
            Config.WriteObject(config, true);
        }

        public class ConfigFileStructure
        {
            public GlobalOptions Global = new GlobalOptions() { };
            public SpecificEventOptions Events = new SpecificEventOptions() { };
        }

        public class GlobalOptions
        {
            public string ConfigVersion = "3.2.4";
            public bool PluginEnabled = true;
            public bool DebugEnabled = false;
            public float CompareRadius = 100;

            public string ExtraZoneFlags = "";
            public string MsgEnter = "Entering a PVP area!";
            public string MsgLeave = "Leaving a PVP area.";

            public bool BlockTeleport = true;
            public bool BotsEnabled = true;
            public bool DomesEnabled = true;

            public bool PVPDelayEnabled = true;
            public int PVPDelayTime = 10;
        }

        public class SpecificEventOptions
        {
            public StandardEventOptions BradleyAPC = new StandardEventOptions() { };
            public StandardEventOptions PatrolHelicopter = new StandardEventOptions() { };
            public StandardEventOptions SupplySignal = new StandardEventOptions() { Enabled = false };
            public StandardEventOptions TimedCrate = new StandardEventOptions() { Duration = 1200 };
            public StandardEventOptions TimedDrop = new StandardEventOptions() { };
        }

        public class StandardEventOptions
        {
            public string BotProfile = "DynamicPVP";
            public float Duration = 600;
            public bool Enabled = true;
            public float Radius = 100;
        }

        #endregion

        #region Helper Methods
        public string GetPlayerName(BasePlayer player)
        {
            if (player.displayName == "")
            {
                if (player.name == "")
                {
                    return null;
                }
                else return player.name;
            }
            else return player.displayName;
        }
        #endregion
    }
}