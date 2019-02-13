using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("UGather", "DylanSMR", "1.1.4", ResourceId = 1763)]
    [Description("Adds zones, permissions, and other options to modify gather rates!")]

    class UGather : RustPlugin
    {
        #region Variables
        [PluginReference] Plugin ZoneManager;
        public static UGather plugin;

        private DynamicConfigFile ZoneDataFile;
        private ZoneData zoneData;
        private Configuration config;

        private const string SphereEnt = "assets/prefabs/visualization/sphere.prefab"; // Thanks to ZoneDomes
        #endregion

        #region Data
        public class ZoneData
        {
            public List<GatherZone> Zones = new List<GatherZone>();

            public GatherZone GetZoneByID(string id)
            {
                return Zones.Find(x => x.id == id);
            }
        }
        public class GatherZone
        {
            // The message a user will see upon entering this gather zone, string.Empty for nothing
            public string enterMessage;
            // The message a user will see upon leaving this gather zone, string.Empty for nothing
            public string leaveMessage;

            // The permission we will use from the config to use certain gather rates.
            public string permissionToUse;
            // If a should have to have the permission to use these gather rates : This effects if a player will see the leave/enter mesage
            public bool requireUserHavePermission;
            // If this zone should have priority over everything else, or should it just leave it to the plugin to figure out.
            public bool overTakePriority;
            // If I should disable the regular permission, this prevents it from being used anywhere but the zone
            public bool restrictPermission;

            // The location of zone, probably dont need this. But just incase we have to recreate the zone
            public float x, y, z;
            // The id of the zone we use in ZoneManager, used to get at a later point.
            public string id;
            // The radius of the zone
            public int radius;

            public void OnPlayerEntered(BasePlayer player)
            {
                if (enterMessage == string.Empty) return; // No enter message, get out of here
                if (!plugin.permission.UserHasPermission(player.UserIDString, permissionToUse) && requireUserHavePermission) return;

                plugin.SendReply(player, enterMessage);
            }

            public void OnPlayerLeave(BasePlayer player)
            {
                if (leaveMessage == string.Empty) return; // No enter message, get out of here
                if (!plugin.permission.UserHasPermission(player.UserIDString, permissionToUse) && requireUserHavePermission) return;

                plugin.SendReply(player, leaveMessage);
            }
        }
        private void SaveData()
        {
            ZoneDataFile.WriteObject(zoneData);
        }
        private void LoadData()
        {
            plugin = this;
            try
            {
                ZoneDataFile.Settings.NullValueHandling = NullValueHandling.Ignore;
                zoneData = ZoneDataFile.ReadObject<ZoneData>();
                Puts($"Loaded {zoneData.Zones.Count} UGather Zones!");
            }
            catch
            {
                Puts("Failed to load ZoneData, creating new data.");
                zoneData = new ZoneData();
            }
        }
        #endregion

        #region ZoneManager
        private void OnEnterZone(string ZoneID, BasePlayer player)
        {
            GatherZone zone = zoneData.GetZoneByID(ZoneID);
            zone?.OnPlayerEntered(player);
        }

        private void OnExitZone(string ZoneID, BasePlayer player)
        {
            GatherZone zone = zoneData.GetZoneByID(ZoneID);
            zone?.OnPlayerLeave(player);
        }
        #endregion

        #region Configuration
        public class Configuration
        {
            [JsonProperty(PropertyName = "Gather Perms : The list of permissions that grant specified gather rates.")]
            public Dictionary<string, Dictionary<string, Dictionary<string, float>>> GatherPerms;

            [JsonProperty(PropertyName = "Base Permission : The basic permission given to group default.")]
            public string BasePerm;

            [JsonProperty(PropertyName = "Default Group : The group all players should be in, these players get the base perm.")]
            public string DefaultGroup;

            [JsonProperty(PropertyName = "Stackable Permissions : If a user has multiple perms, should they stack gather rate.")]
            public bool Stack;
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file.");
            config = new Configuration()
            {
                GatherPerms = new Dictionary<string, Dictionary<string, Dictionary<string, float>>>()
                {
                    {"ugather.basic", new Dictionary<string, Dictionary<string, float>>()
                    {
                        {"Dispenser", new Dictionary<string, float>(){
                            { "bone_fragments", 2 },
                            { "cloth", 2 },
                            {"fat.animal", 2 },
                            {"meat.horse.raw", 2 },
                            {"hq_metal_ore", 2 },
                            {"humanmeat_raw", 2 },
                            {"meat.bear.raw", 2 },
                            {"meat.pork.raw", 2 },
                            {"leather", 2 },
                            {"metal_ore", 2 },
                            {"stones", 2 },
                            {"sulfur_ore", 2 },
                            {"meat.wolf.raw", 2 },
                            {"skull_wolf", 2 },
                            {"wood", 2 },
                            {"metal_fragments", 2},
                            {"metal_refined", 2},
                            {"charcoal", 2 }
                        }},
                        {"Bonus", new Dictionary<string, float>(){
                            {"stones", 2},
                            {"sulfur_ore", 2},
                            {"metal_ore", 2},
                            {"hq_metal_ore", 2},
                            {"wood", 2}
                        }},
                        {"Quarry", new Dictionary<string, float>(){
                            {"stones", 2},
                            {"sulfur_ore", 2},
                            {"metal_ore", 2},
                            {"hq_metal_ore", 2}
                        }},
                        {"Survey", new Dictionary<string, float>(){
                            {"stones", 2},
                            {"sulfur_ore", 2},
                            {"metal_ore", 2},
                            {"hq_metal_ore", 2}
                        }},
                        {"Pickups", new Dictionary<string, float>(){
                            {"stones", 2},
                            {"sulfur_ore", 2},
                            {"metal_ore", 2},
                            {"wood", 2},
                            {"mushroom", 2},
                            {"cloth", 2},
                            {"corn", 2},
                            {"pumpkin", 2},
                            {"corn_seed", 2},
                            {"hemp_seed", 2},
                            {"pumpkin_seed", 2}
                        }}
                    }},
                    {"ugather.advanced", new Dictionary<string, Dictionary<string, float>>()
                    {
                        {"Dispenser", new Dictionary<string, float>(){
                            { "bone_fragments", 2 },
                            { "cloth", 2 },
                            {"fat.animal", 2 },
                            {"meat.horse.raw", 2 },
                            {"hq_metal_ore", 2 },
                            {"humanmeat_raw", 2 },
                            {"meat.bear.raw", 2 },
                            {"meat.pork.raw", 2 },
                            {"leather", 2 },
                            {"metal_ore", 2 },
                            {"stones", 2 },
                            {"sulfur_ore", 2 },
                            {"meat.wolf.raw", 2 },
                            {"skull_wolf", 2 },
                            {"wood", 2 },
                            {"metal_fragments", 2},
                            {"metal_refined", 2},
                            {"charcoal", 2 }
                        }},
                        {"Bonus", new Dictionary<string, float>(){
                            {"stones", 2},
                            {"sulfur_ore", 2},
                            {"metal_ore", 2},
                            {"hq_metal_ore", 2},
                            {"wood", 2}
                        }},
                        {"Quarry", new Dictionary<string, float>(){
                            {"stones", 2},
                            {"sulfur_ore", 2},
                            {"metal_ore", 2},
                            {"hq_metal_ore", 2}
                        }},
                        {"Survey", new Dictionary<string, float>(){
                            {"stones", 2},
                            {"sulfur_ore", 2},
                            {"metal_ore", 2},
                            {"hq_metal_ore", 2}
                        }},
                        {"Pickups", new Dictionary<string, float>(){
                            {"stones", 2},
                            {"sulfur_ore", 2},
                            {"metal_ore", 2},
                            {"wood", 2},
                            {"mushroom", 2},
                            {"cloth", 2},
                            {"corn", 2},
                            {"pumpkin", 2},
                            {"corn_seed", 2},
                            {"hemp_seed", 2},
                            {"pumpkin_seed", 2}
                        }}
                    }},
                    {"ugather.donor", new Dictionary<string, Dictionary<string, float>>()
                    {
                        {"Dispenser", new Dictionary<string, float>(){
                            { "bone_fragments", 2 },
                            { "cloth", 2 },
                            {"fat.animal", 2 },
                            {"meat.horse.raw", 2 },
                            {"hq_metal_ore", 2 },
                            {"humanmeat_raw", 2 },
                            {"meat.bear.raw", 2 },
                            {"meat.pork.raw", 2 },
                            {"leather", 2 },
                            {"metal_ore", 2 },
                            {"stones", 2 },
                            {"sulfur_ore", 2 },
                            {"meat.wolf.raw", 2 },
                            {"skull_wolf", 2 },
                            {"wood", 2 },
                            {"metal_fragments", 2},
                            {"metal_refined", 2},
                            {"charcoal", 2 }
                        }},
                        {"Bonus", new Dictionary<string, float>(){
                            {"stones", 2},
                            {"sulfur_ore", 2},
                            {"metal_ore", 2},
                            {"hq_metal_ore", 2},
                            {"wood", 2}
                        }},
                        {"Quarry", new Dictionary<string, float>(){
                            {"stones", 2},
                            {"sulfur_ore", 2},
                            {"metal_ore", 2},
                            {"hq_metal_ore", 2}
                        }},
                        {"Survey", new Dictionary<string, float>(){
                            {"stones", 2},
                            {"sulfur_ore", 2},
                            {"metal_ore", 2},
                            {"hq_metal_ore", 2}
                        }},
                        {"Pickups", new Dictionary<string, float>(){
                            {"stones", 2},
                            {"sulfur_ore", 2},
                            {"metal_ore", 2},
                            {"wood", 2},
                            {"mushroom", 2},
                            {"cloth", 2},
                            {"corn", 2},
                            {"pumpkin", 2},
                            {"corn_seed", 2},
                            {"hemp_seed", 2},
                            {"pumpkin_seed", 2}
                        }}
                    }},
                    {"ugather.admin", new Dictionary<string, Dictionary<string, float>>()
                    {
                        {"Dispenser", new Dictionary<string, float>(){
                            { "bone_fragments", 2 },
                            { "cloth", 2 },
                            {"fat.animal", 2 },
                            {"meat.horse.raw", 2 },
                            {"hq_metal_ore", 2 },
                            {"humanmeat_raw", 2 },
                            {"meat.bear.raw", 2 },
                            {"meat.pork.raw", 2 },
                            {"leather", 2 },
                            {"metal_ore", 2 },
                            {"stones", 2 },
                            {"sulfur_ore", 2 },
                            {"meat.wolf.raw", 2 },
                            {"skull_wolf", 2 },
                            {"wood", 2 },
                            {"metal_fragments", 2},
                            {"metal_refined", 2},
                            {"charcoal", 2 }
                        }},
                        {"Bonus", new Dictionary<string, float>(){
                            {"stones", 2},
                            {"sulfur_ore", 2},
                            {"metal_ore", 2},
                            {"hq_metal_ore", 2},
                            {"wood", 2}
                        }},
                        {"Quarry", new Dictionary<string, float>(){
                            {"stones", 2},
                            {"sulfur_ore", 2},
                            {"metal_ore", 2},
                            {"hq_metal_ore", 2}
                        }},
                        {"Survey", new Dictionary<string, float>(){
                            {"stones", 2},
                            {"sulfur_ore", 2},
                            {"metal_ore", 2},
                            {"hq_metal_ore", 2}
                        }},
                        {"Pickups", new Dictionary<string, float>(){
                            {"stones", 2},
                            {"sulfur_ore", 2},
                            {"metal_ore", 2},
                            {"wood", 2},
                            {"mushroom", 2},
                            {"cloth", 2},
                            {"corn", 2},
                            {"pumpkin", 2},
                            {"corn_seed", 2},
                            {"hemp_seed", 2},
                            {"pumpkin_seed", 2}
                        }}
                    }}
                },
                BasePerm = "ugather.basic",
                Stack = false,
                DefaultGroup = "default"
            };
            SaveConfig(config);
        }
        void SaveConfig(Configuration config)
        {
            Config.WriteObject(config, true);
            SaveConfig();
        }
        public void LoadConfigVars()
        {
            PrintWarning("Loading configuration.");
            config = Config.ReadObject<Configuration>();
            Config.WriteObject(config, true);
        }
        #endregion

        void Loaded()
        {
            LoadConfigVars();

            if(!permission.PermissionExists("ugather.admin"))
                permission.RegisterPermission("ugather.admin", this);

            foreach (var x in config.GatherPerms)
                permission.RegisterPermission(x.Key, this);

            if (!permission.GroupHasPermission(config.DefaultGroup, config.BasePerm))
                permission.GrantGroupPermission(config.DefaultGroup, config.BasePerm, this);

            ZoneDataFile = Interface.Oxide.DataFileSystem.GetFile("UGather_ZoneData");
            LoadData();

            lang.RegisterMessages(new Dictionary<string, string>()
            {
                // General Command Lang
                {"Command : Prefix", "<color=#0dba86>[UGather]: </color>"},
                {"Command : Help (General)",
                    "\nUse any of these commands to see more information about them:" +
                    "\n • /ugather rate - Show's information about config gather rates." +
                    "\n • /ugather zone - Show's information about the zone gather system." +
                    "\n\n - UGather Zone Status: <color={zonestatuscolor}>{zonestatus}</color>"
                },
                {"Command : No Permission", "You do not have permission to run that command!"},

                // Zone Command Lang
                {"Command : Help (Zone)", "Commands for the zoning system:" +
                    "\n <b><i>* = Optional Parameter</i></b>" +
                    "\n • /ugather zone add <permission> <radius> - Creates a gather zone using a permission from config for reference, and a radius using zonemanager." +
                    "\n • /ugather zone list *<page> - Show's the list of gather zones, the page param is optional if you have too many zones to show on one page." +
                    "\n • /ugather zone edit <number from list> <variable> <value> - Sets a flag/variable for the zone. Use /ugather zone editinfo for more info." +
                    "\n • /ugather zone show <number from list> - Creates a sphere showing the radius of the zone" +
                    "\n • /ugather zone variables - Shows a list of variables you can edit using the /ugather zone edit command" +
                    "\n • /ugather zone delete <number from list> - Deletes a zone based on the number from /ugather zone list!"
                },
                {"Command : Not A Number", "The parameter <i>{param}</i> requires a valid positive number! Ex: 5"},
                {"Command : Not A Boolean", "The paramter <i>{param}</i> requires a valid boolean! Ex: true or false"},
                {"Command : Value Updated", "The variable {var} has been changed to {val} for the selected zone!"},
                {"Command : Failed To Update", "Failed to update the following variable: {var}"},
                {"Command : No Available Zone With ID", "Unable to find zone from the list with the number of {num}"},
                {"Command : Zone Created", "A gather zone with the ID of {id} and radius of {rad} was successfully created! Permission: {perm}"},
                {"Command : Zone Failed", "Failed to create a zone with the id of {id}!"},
                {"Command : Zone Manager Not Loaded", "The zone portion of the plugin is not available as ZoneManager is not loaded!"},
                {"Command : No Zones", "There are currently no gather zones! Use /ugather zone to learn more about creating zones!"},
                {"Command : Delete Success", "You have successfully deleted the zone with the ID of {id}!"},
                {"Command : Zone List", "Available Zones:" +
                    "\nPage {p1} out of {p2}" +
                    "\n{zones}"
                },
                {"Command : Zone Variables", "Editable Variables:" +
                    "\n • position ( No Arguments ) ( The location of the zone )" +
                    "\n • radius ( Integer - Example: 50 ) ( The radius of the zone )" +
                    "\n • enter_message ( String - Example: \"Hi!\" ) ( The message you get upon entering the zone )" +
                    "\n • leave_message ( String - Example: \"Bye!\" ) ( The message you get upon leaving the zone )" +
                    "\n • require_permission ( Boolean - Example: true ) ( If you require permission to gain gather rates from the zone )" +
                    "\n • overtake_priority ( Boolean - Example: true ) ( If it should override all other gather rates )" +
                    "\n • restrict_permission ( Boolean - Example: true ) ( If the permission it is using should be disabled unless used for the zone )"
                },

                // Rate Command Lang
                {"Command : Help (Rate)", "Commands for the gather rates system:" +
                    "\n <b><i>* = Optional Parameter</i></b>" +
                    "\n • /ugather rate list - Show's a list of current permissions" +
                    "\n • /ugather rate info <permission> - Shows info about a permissions rates" +
                    "\n • /ugather rate stats <permission> <type> - Shows info about a permissions rates, in a category." +
                    "\n • /ugather rate types - Show's a list of valid types"
                },
                {"Command : Permission List", "Available Permissions:" +
                    "\n{perms}"
                },
                {"Command : Invalid Permission", "Could not find a permission with the name of {name}!"},
                {"Command : Perm Info", "Permission Info:" +
                    "\n • Types: {typeCount}" +
                    "\n • Registered Zone: {hasRegisteredZone}" +
                    "\n • Players With Permission: {playerCount}"
                },
                {"Command : Permission Info List", "Showing type of {type} for permission {perm}:" +
                    "\n{stats}"
                },
                {"Command : Gather Types", "Available Gather Types:\n" +
                    "\n • Dispenser - The materials you get from harvesting with a pickaxe, hatchet, etc." +
                    "\n • Bonus - That little extra you get at the end of a harvest." +
                    "\n • Pickups - The materials you get from picking things up, like food." +
                    "\n • Quarry - The materials you get from a quarry." +
                    "\n • Survey - The materials you get from a survey charge"
                },
            }, this);
        }

        void Reply(BasePlayer player, string raw, params string[] args)
        {
            var msg = lang.GetMessage(raw, this, player.UserIDString);
            if (args.Length > 0)
                for (var i = 0; i < args.Length; i += 2)
                    msg = msg.Replace("{"+args[i]+"}", args[i + 1]);

            SendReply(player, lang.GetMessage("Command : Prefix", this, player.UserIDString) + msg);
        }

        [ChatCommand("ugather")]
        void IGather_Command(BasePlayer player, string command, string[] args)
        {
            if(!permission.UserHasPermission(player.UserIDString, "ugather.admin"))
            {
                Reply(player, "Command : No Permission");
                return;
            }
            if(args.Length == 0)
            {
                Reply(player, "Command : Help (General)", "zonestatuscolor", (ZoneManager == null) ? "red" : "green", "zonestatus", (ZoneManager == null) ? "Disabled" : "Enabled");
                return;
            }

            if(args[0].ToLower() == "zone")
            {
                if(ZoneManager == null)
                {
                    Reply(player, "Command : Zone Manager Not Loaded");
                    return;
                }
                if(args.Length < 2)
                {
                    Reply(player, "Command : Help (Zone)");
                    return;
                }

                switch (args[1])
                {
                    case "variables":
                        Reply(player, "Command : Zone Variables");
                        break;
                    case "add":
                        if (args.Length != 4)
                        {
                            Reply(player, "Command : Help (Zone)");
                            return;
                        }

                        string permission = args[2];
                        int radius = 0;
                        if (!int.TryParse(args[3], out radius))
                        {
                            Reply(player, "Command : Not A Number", "param", "radius");
                            return;
                        }

                        if (!config.GatherPerms.ContainsKey(permission))
                        {
                            Reply(player, "Command : Invalid Permission", "name", permission);
                            return;
                        }

                        GatherZone zone = new GatherZone()
                        {
                            enterMessage = string.Empty,
                            leaveMessage = string.Empty,

                            id = $"UGATHER_ZONE_{permission}",
                            overTakePriority = false,
                            permissionToUse = permission,
                            requireUserHavePermission = true,
                            x = player.transform.position.x,
                            y = player.transform.position.y,
                            z = player.transform.position.z,
                            restrictPermission = false,
                            radius = radius
                        };

                        List<string> Arguments = new List<string>()
                        {
                            "radius",
                            radius.ToString()
                        };

                        bool zoneManagerZone = (bool)ZoneManager?.CallHook("CreateOrUpdateZone", zone.id, Arguments.ToArray(), new Vector3(zone.x, zone.y, zone.z));
                        if (zoneManagerZone)
                        {
                            zoneData.Zones.Add(zone);
                            Reply(player, "Command : Zone Created", "id", zone.id, "rad", radius.ToString(), "perm", zone.permissionToUse);
                            SaveData();
                        }
                        else
                        {
                            Reply(player, "Command : Zone Failed", "id", zone.id);
                            ZoneManager?.CallHook("EraseZone", "IGATHER_ZONE_" + permission);
                        }

                        break;
                    case "list":
                        int page = 1;
                        if (args.Length == 3)
                        {
                            if (int.TryParse(args[2], out page))
                                if (page < 0)
                                    page = 1;
                            else
                                Reply(player, "Command : Not A Number", "param", "page");
                        }

                        StringBuilder zoneBuilder = new StringBuilder();
                        if (zoneData.Zones.Count == 0)
                        {
                            Reply(player, "Command : No Zones");
                            return;
                        }
                        for (var i = (page - 1) * 8; i < page * 8; i++)
                        {
                            if (!(zoneData.Zones.Count >= i + 1)) break;

                            zoneBuilder.AppendLine($" • {i + 1} - {zoneData.Zones[i].id}");
                        }

                        Reply(player, "Command : Zone List", "zones", zoneBuilder.ToString(), "p1", page.ToString(), "p2", Math.Ceiling(zoneData.Zones.Count / 8.0).ToString());
                        break;
                    case "show":
                        if (args.Length != 3)
                        {
                            Reply(player, "Command : Help (Zone)");
                            return;
                        }

                        int set2 = -1;

                        if (!int.TryParse(args[2], out set2))
                        {
                            Reply(player, "Command : Not A Number", "param", "id");
                            return;
                        }

                        if (!(zoneData.Zones.Count >= (set2 - 1)))
                        {
                            Reply(player, "Command : No Available Zone With ID", "num", set2.ToString());
                            return;
                        }

                        GatherZone showingZone = zoneData.Zones[set2 - 1];

                        for (int i = 0; i < 15; i++)
                        {
                            BaseEntity sphere = GameManager.server.CreateEntity(SphereEnt, new Vector3(showingZone.x, showingZone.y, showingZone.z), new Quaternion(), true);
                            SphereEntity ent = sphere.GetComponent<SphereEntity>();
                            ent.currentRadius = showingZone.radius * 2;
                            ent.lerpSpeed = 0f;

                            sphere.Spawn();

                            timer.Once(15f, () =>
                            {
                                sphere.Kill();
                            });
                        }
                        break;
                    case "delete":
                        if (args.Length != 3)
                        {
                            Reply(player, "Command : Help (Zone)");
                            return;
                        }

                        int set3 = -1;

                        if (!int.TryParse(args[2], out set3))
                        {
                            Reply(player, "Command : Not A Number", "param", "id");
                            return;
                        }

                        if (!(zoneData.Zones.Count >= (set3 - 1)))
                        {
                            Reply(player, "Command : No Available Zone With ID", "num", set3.ToString());
                            return;
                        }

                        GatherZone editingZone2 = zoneData.Zones[set3 - 1];

                        ZoneManager?.CallHook("EraseZone", editingZone2.id);
                        Reply(player, "Command : Delete Success", "id", editingZone2.id);
                        zoneData.Zones.Remove(editingZone2);
                        SaveData();

                        break;
                    case "edit":
                        if (args.Length != 5)
                        {
                            Reply(player, "Command : Help (Zone)");
                            return;
                        }

                        int set = -1;

                        if(!int.TryParse(args[2], out set))
                        {
                            Reply(player, "Command : Not A Number", "param", "id");
                            return;
                        }

                        if(!(zoneData.Zones.Count >= (set - 1)))
                        {
                            Reply(player, "Command : No Available Zone With ID", "num", set.ToString());
                            return;
                        }

                        GatherZone editingZone = zoneData.Zones[set - 1];
                        switch (args[3])
                        {
                            case "radius":
                                int editingZoneRadius = 0;

                                if(!int.TryParse(args[4], out editingZoneRadius))
                                {
                                    Reply(player, "Command : Not A Number", "param", "radius");
                                    return;
                                }

                                if(editingZoneRadius <= 0)
                                {
                                    Reply(player, "Command : Not A Number", "param", "radius");
                                    return;
                                }

                                List<string> editingZoneArgumentsRadius = new List<string>()
                                {
                                    "radius",
                                    editingZoneRadius.ToString()
                                };

                                bool wasZoneRadiusUpdated = (bool)ZoneManager?.CallHook("CreateOrUpdateZone", editingZone.id, editingZoneArgumentsRadius.ToArray(), new Vector3(editingZone.x, editingZone.y, editingZone.z));
                                if (wasZoneRadiusUpdated)
                                {
                                    Reply(player, "Command : Value Updated", "var", "radius", "val", editingZoneRadius.ToString());
                                    editingZone.radius = editingZoneRadius;
                                }
                                else
                                    Reply(player, "Command : Failed To Update", "var", "radius");

                                break;
                            case "position":

                                List<string> editingPositionZone = new List<string>()
                                {
                                    "location",
                                    $"{player.transform.position.x} {player.transform.position.y} {player.transform.position.z}"
                                };

                                bool wasZonePositionUpdated = (bool)ZoneManager?.CallHook("CreateOrUpdateZone", editingZone.id, editingPositionZone.ToArray(), new Vector3(editingZone.x, editingZone.y, editingZone.z));
                                if (wasZonePositionUpdated)
                                {
                                    Reply(player, "Command : Value Updated", "var", "position", "val", player.transform.position.ToString());
                                    editingZone.x = player.transform.position.x;
                                    editingZone.y = player.transform.position.y;
                                    editingZone.z = player.transform.position.z;
                                }
                                else
                                    Reply(player, "Command : Failed To Update", "var", "position");

                                break;
                            case "enter_message":
                                Reply(player, "Command : Value Updated", "var", "enter_message", "val", args[4]);
                                editingZone.enterMessage = args[4];
                                break;
                            case "leave_message":
                                Reply(player, "Command : Value Updated", "var", "leave_message", "val", args[4]);
                                editingZone.leaveMessage = args[4];
                                break;

                            case "require_permission":
                                bool shouldUsePerm = editingZone.requireUserHavePermission;
                                if(bool.TryParse(args[4], out shouldUsePerm))
                                {
                                    Reply(player, "Command : Value Updated", "var", "require_permission", "val", shouldUsePerm.ToString());
                                    editingZone.requireUserHavePermission = shouldUsePerm;
                                } else
                                {
                                    Reply(player, "Command : Failed To Update", "var", "require_permission");
                                    Reply(player, "Command : Not A Boolean", "param", "require_permission");
                                }
                                break;

                            case "overtake_priority":
                                bool shouldOvertakePriority = editingZone.overTakePriority;
                                if (bool.TryParse(args[4], out shouldOvertakePriority))
                                {
                                    Reply(player, "Command : Value Updated", "var", "overtake_priority", "val", shouldOvertakePriority.ToString());
                                    editingZone.overTakePriority = shouldOvertakePriority;
                                }
                                else
                                {
                                    Reply(player, "Command : Failed To Update", "var", "overtake_priority");
                                    Reply(player, "Command : Not A Boolean", "param", "overtake_priority");
                                }
                                break;

                            case "restrict_permission":
                                bool shouldRestrictPermission = editingZone.overTakePriority;
                                if (bool.TryParse(args[4], out shouldRestrictPermission))
                                {
                                    Reply(player, "Command : Value Updated", "var", "restrict_permission", "val", shouldRestrictPermission.ToString());
                                    editingZone.restrictPermission = shouldRestrictPermission;
                                }
                                else
                                {
                                    Reply(player, "Command : Failed To Update", "var", "restrict_permission");
                                    Reply(player, "Command : Not A Boolean", "param", "restrict_permission");
                                }
                                break;
                            default:
                                Reply(player, "Command : Zone Variables");
                                break;
                        }
                        break;
                    default:
                        Reply(player, "Command : Help (Zone)");
                        break;
                }

                return;
            }
            else if (args[0].ToLower() == "rate")
            {
                if(args.Length < 2)
                {
                    Reply(player, "Command : Help (Rate)");
                    return;
                }

                switch (args[1])
                {
                    case "list":
                        StringBuilder permissionList = new StringBuilder();
                        foreach(var gatherPerm in config.GatherPerms)
                            permissionList.AppendLine($" • {gatherPerm.Key}");

                        Reply(player, "Command : Permission List", "perms", permissionList.ToString());
                        break;
                    case "info":
                        if(args.Length != 3)
                        {
                            Reply(player, "Command : Help (Rate)");
                            return;
                        }

                        if(config.GatherPerms.Where(x => x.Key.ToLower() == args[2].ToLower()).Count() == 0)
                        {
                            Reply(player, "Command : Invalid Permission", "name", args[2]);
                            return;
                        }

                        int countTypes = config.GatherPerms[args[2]].Count();
                        bool regZone = zoneData.Zones.Any(x => x.permissionToUse.ToLower() == args[2].ToLower());
                        int playersUsing = covalence.Players.All.Where(x => permission.UserHasPermission(x.Id, args[2].ToLower())).Count();

                        Reply(player, "Command : Perm Info", "typeCount", countTypes.ToString(), "hasRegisteredZone", regZone.ToString(), "playerCount", playersUsing.ToString());
                        break;
                    case "stats":
                        if (args.Length != 4)
                        {
                            Reply(player, "Command : Help (Rate)");
                            return;
                        }

                        if (config.GatherPerms.Where(x => x.Key.ToLower() == args[2].ToLower()).Count() == 0)
                        {
                            Reply(player, "Command : Invalid Permission", "name", args[2]);
                            return;
                        }

                        if(!config.GatherPerms[args[2]].ContainsKey(args[3]))
                        {
                            Reply(player, "Command : Invalid Permission Type", "name", args[2], "type", args[3]);
                            return;
                        }

                        StringBuilder statBuilder = new StringBuilder();
                        foreach (var v in config.GatherPerms[args[2]][args[3]])
                            statBuilder.AppendLine($" • {v.Key} : {v.Value}x");

                        Reply(player, "Command : Permission Info List", "type", args[3], "perm", args[2], "stats", statBuilder.ToString());
                        break;
                    case "types":
                        Reply(player, "Command : Gather Types");
                        break;
                    default:
                        Reply(player, "Command : Help (Rate)");
                        break;
                }
            } else
            {
                Reply(player, "Command : Help (General)", "zonestatuscolor", (ZoneManager == null) ? "red" : "green", "zonestatus", (ZoneManager == null) ? "Disabled" : "Enabled");
            }
        }

        public float GetZoneRate(string permission, string type, string name)
        {
            if (!config.GatherPerms.ContainsKey(permission)) return 1f;

            if (!config.GatherPerms[permission].ContainsKey(type)) return 1f;

            if (!config.GatherPerms[permission][type].ContainsKey(name))
            {
                config.GatherPerms[permission][type].Add(name, 1f);
                SaveConfig();
            }

            return config.GatherPerms[permission][type][name];
        }

        public float GetStackedMultiplier(BasePlayer player, string type, string name, bool forced = false, Vector3 usePosition = new Vector3())
        {
            float highestValue = 0;

            if (ZoneManager != null)
            {
                foreach (GatherZone zone in zoneData.Zones)
                {
                    if (usePosition != new Vector3())
                    {
                        if (Vector3.Distance(usePosition, new Vector3(zone.x, zone.y, zone.z)) > zone.radius)
                            continue; // Not within the zones radius
                    }
                    else
                    {
                        bool playerZone = (bool)ZoneManager?.CallHook("isPlayerInZone", zone.id, player);
                        if (!playerZone)
                            continue;
                    }

                    var zoneRate = GetZoneRate(zone.permissionToUse, type, name);

                    if (zone.requireUserHavePermission)
                        if (!permission.UserHasPermission(player.UserIDString, zone.permissionToUse))
                            continue;

                    if (zone.overTakePriority)
                        return zoneRate;

                    if (zoneRate > highestValue)
                        highestValue = zoneRate;
                }
            }

            if (config.Stack)
            {
                foreach (var pair in config.GatherPerms)
                {
                    if (permission.UserHasPermission(player.UserIDString, pair.Key) || forced)
                        if (pair.Value[type].ContainsKey(name))
                            highestValue += pair.Value[type][name];
                }
            }

            return highestValue;
        }

        public float GetSingleMultiplier(BasePlayer player, string type, string name, bool forced = false, Vector3 usePosition = new Vector3())
        {
            float highestValue = 1f;

            if(ZoneManager != null)
            {
                foreach (GatherZone zone in zoneData.Zones)
                {
                    // Distance Checks
                    if(usePosition != new Vector3())
                    {
                        if (Vector3.Distance(usePosition, new Vector3(zone.x, zone.y, zone.z)) > zone.radius)
                            continue; // Not within the zones radius
                    } else
                    {
                        bool playerZone = (bool)ZoneManager?.CallHook("isPlayerInZone", zone.id, player);
                        if (!playerZone)
                            continue;
                    }

                    var zoneRate = GetZoneRate(zone.permissionToUse, type, name);

                    if (zone.requireUserHavePermission)
                        if (!permission.UserHasPermission(player.UserIDString, zone.permissionToUse))
                            continue;

                    if (zone.overTakePriority)
                        return zoneRate;

                    if (zoneRate > highestValue)
                        highestValue = zoneRate;
                }
            }

            foreach (var pair in config.GatherPerms.Where(x => permission.UserHasPermission(player.UserIDString, x.Key) || forced).ToDictionary(p => p.Key, p => p.Value))
            {
                if (zoneData.Zones.Where(x => x.restrictPermission && x.permissionToUse == pair.Key).Count() != 0)
                    continue;

                if (!pair.Value.ContainsKey(type))
                {
                    PrintWarning($"GetSingleMultiplier :: Unable to find type of {type}");
                    return 0.0f;
                }
                if (!pair.Value[type].ContainsKey(name))
                {
                    PrintWarning($"GetSingleMultiplier :: Unable to find resource of {name} in type {type}. Adding to config and force saving. Assuming default value of 1.0");
                    config.GatherPerms[pair.Key][type].Add(name, 1f);
                    SaveConfig(config);
                }
                else
                {
                    if (pair.Value[type][name] > highestValue)
                        highestValue = pair.Value[type][name];
                }
            }

            return highestValue;
        }

        private void Unload()
        {
            SaveData();
        }

        private void OnQuarryGather(MiningQuarry quarry, Item item)
        {
            if (quarry == null) return;
            BasePlayer player = BasePlayer.FindByID(quarry.OwnerID);
            if (player == null) return;

            float resourceMult = -1;
            if (config.Stack)
                resourceMult = GetStackedMultiplier(player, "Quarry", item.info.name.Replace(".item", ""), false, quarry.transform.position);
            else
                resourceMult = GetSingleMultiplier(player, "Quarry", item.info.name.Replace(".item", ""), false, quarry.transform.position);

            if (resourceMult == -1) return;
            item.amount = (int)(item.amount * resourceMult);
        }

        private void OnCollectiblePickup(Item item, BasePlayer player)
        {
            float resourceMult = -1;
            if (config.Stack)
                resourceMult = GetStackedMultiplier(player, "Pickups", item.info.name.Replace(".item", ""));
            else
                resourceMult = GetSingleMultiplier(player, "Pickups", item.info.name.Replace(".item", ""));

            if (resourceMult == -1) return;
            item.amount = (int)(item.amount * resourceMult);
        }

        Dictionary<BaseEntity, BasePlayer> SurveyCharges = new Dictionary<BaseEntity, BasePlayer>();
        void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (!entity.name.ToLower().Contains("survey")) return;

            SurveyCharges.Add(entity, player);
        }

        void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            float resourceMult = -1;
            if (config.Stack)
                resourceMult = GetStackedMultiplier(player, "Bonus", item.info.name.Replace(".item", ""));
            else
                resourceMult = GetSingleMultiplier(player, "Bonus", item.info.name.Replace(".item", ""));

            if (resourceMult == -1) return;
            item.amount = (int)(item.amount * resourceMult);
        }

        private void OnSurveyGather(SurveyCharge survey, Item item)
        {
            BasePlayer player = null;

            if (SurveyCharges.ContainsKey(survey))
                player = SurveyCharges[survey];

            if (player == null)
                return;

            SurveyCharges.Remove(survey);

            float resourceMult = -1;
            if (config.Stack)
                resourceMult = GetStackedMultiplier(player, "Survey", item.info.name.Replace(".item", ""), false, survey.transform.position);
            else
                resourceMult = GetSingleMultiplier(player, "Survey", item.info.name.Replace(".item", ""), false, survey.transform.position);

            if (resourceMult == -1) return;
            item.amount = (int)(item.amount * resourceMult);
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            BasePlayer player = entity.ToPlayer();
            if (player == null) return;

            float resourceMult = -1;
            if (config.Stack)
                resourceMult = GetStackedMultiplier(player, "Dispenser", item.info.name.Replace(".item", ""));
            else
                resourceMult = GetSingleMultiplier(player, "Dispenser", item.info.name.Replace(".item", ""));

            if (resourceMult == -1) return;
            item.amount = (int)(item.amount * resourceMult);
        }
    }
}