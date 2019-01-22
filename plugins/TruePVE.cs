
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("TruePVE", "ignignokt84", "0.9.0", ResourceId = 1789)]
    [Description("Improvement of the default Rust PVE behavior")]
    class TruePVE : RustPlugin
    {
        #region Variables

        static TruePVE Instance;

        // config/data container
        TruePVEData data = new TruePVEData();

        // ZoneManager plugin
        [PluginReference]
        Plugin ZoneManager;

        // LiteZone plugin (private)
        [PluginReference]
        Plugin LiteZones;

        // usage information string with formatting
        public string usageString;
        // valid commands
        enum Command { def, sched, trace, usage };
        // valid configuration options
        public enum Option {
            handleDamage,        // (true)    enable TruePVE damage handling hooks
            useZones            // (true)    use ZoneManager/LiteZones for zone-specific damage behavior (requires modification of ZoneManager.cs)
        };
        // default values array
        bool[] defaults = {
            true,    // handleDamage
            true    // useZones
        };

        // flags for RuleSets
        [Flags]
        enum RuleFlags
        {
            None = 0,
            SuicideBlocked = 1,
            AuthorizedDamage = 1 << 1,
            NoHeliDamage = 1 << 2,
            HeliDamageLocked = 1 << 3,
            NoHeliDamagePlayer = 1 << 4,
            HumanNPCDamage = 1 << 5,
            LockedBoxesImmortal = 1 << 6,
            LockedDoorsImmortal = 1 << 7,
            AdminsHurtSleepers = 1 << 8,
            ProtectedSleepers = 1 << 9,
            TrapsIgnorePlayers = 1 << 10,
            TurretsIgnorePlayers = 1 << 11,
            CupboardOwnership = 1 << 12
        }
        // timer to check for schedule updates
        Timer scheduleUpdateTimer;
        // current ruleset
        RuleSet currentRuleSet;
        // current broadcast message
        string currentBroadcastMessage;
        // internal useZones flag
        bool useZones = false;
        // constant "any" string for rules
        const string Any = "any";
        // constant "allzones" string for mappings
        const string AllZones = "allzones";
        // flag to prevent certain things from happening before server initialized
        bool serverInitialized = false;
        // permission for mapping command
        string PermCanMap = "truepve.canmap";

        // trace flag
        bool trace = false;
        // tracefile name
        string traceFile = "ruletrace";
        // auto-disable trace after 300s (5m)
        float traceTimeout = 300f;
        // trace timeout timer
        Timer traceTimer;

        #endregion

        #region Lang

        // load default messages to Lang
        void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                {"Prefix", "<color=#FFA500>[ TruePVE ]</color>" },

                {"Header_Usage", "---- TruePVE usage ----"},
                {"Cmd_Usage_def", "Loads default configuration and data"},
                {"Cmd_Usage_sched", "Enable or disable the schedule" },
                {"Cmd_Usage_prod", "Show the prefab name and type of the entity being looked at"},
                {"Cmd_Usage_map", "Create/remove a mapping entry" },
                {"Cmd_Usage_trace", "Toggle tracing on/off" },

                {"Warning_PveMode", "Server is set to PVE mode!  TruePVE is designed for PVP mode, and may cause unexpected behavior in PVE mode."},
                {"Warning_OldConfig", "Old config detected - moving to {0}" },
                {"Warning_NoRuleSet", "No RuleSet found for \"{0}\"" },
                {"Warning_DuplicateRuleSet", "Multiple RuleSets found for \"{0}\"" },

                {"Error_InvalidCommand", "Invalid command" },
                {"Error_InvalidParameter", "Invalid parameter: {0}"},
                {"Error_InvalidParamForCmd", "Invalid parameters for command \"{0}\""},
                {"Error_InvalidMapping", "Invalid mapping: {0} => {1}; Target must be a valid RuleSet or \"exclude\"" },
                {"Error_NoMappingToDelete", "Cannot delete mapping: \"{0}\" does not exist" },
                {"Error_NoPermission", "Cannot execute command: No permission"},
                {"Error_NoSuicide", "You are not allowed to commit suicide"},
                {"Error_NoEntityFound", "No entity found"},

                {"Notify_AvailOptions", "Available Options: {0}"},
                {"Notify_DefConfigLoad", "Loaded default configuration"},
                {"Notify_DefDataLoad", "Loaded default mapping data"},
                {"Notify_ProdResult", "Prod results: type={0}, prefab={1}"},
                {"Notify_SchedSetEnabled", "Schedule enabled" },
                {"Notify_SchedSetDisabled", "Schedule disabled" },
                {"Notify_InvalidSchedule", "Schedule is not valid" },
                {"Notify_MappingCreated", "Mapping created for \"{0}\" => \"{1}\"" },
                {"Notify_MappingUpdated", "Mapping for \"{0}\" changed from \"{1}\" to \"{2}\"" },
                {"Notify_MappingDeleted", "Mapping for \"{0}\" => \"{1}\" deleted" },
                {"Notify_TraceToggle", "Trace mode toggled {0}" },

                {"Format_NotifyColor", "#00FFFF"}, // cyan
                {"Format_NotifySize", "12"},
                {"Format_HeaderColor", "#FFA500"}, // orange
                {"Format_HeaderSize", "14"},
                {"Format_ErrorColor", "#FF0000"}, // red
                {"Format_ErrorSize", "12"},
            };
            lang.RegisterMessages(messages, this);
        }

        // get message from Lang
        string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion

        #region Loading/Unloading

        // load things
        void Loaded()
        {
            Instance = this;
            LoadDefaultMessages();
            string baseCommand = "tpve";
            // register console commands automagically
            foreach(Command command in Enum.GetValues(typeof(Command)))
                cmd.AddConsoleCommand((baseCommand + "." + command.ToString()), this, "CommandDelegator");
            // register chat commands
            cmd.AddChatCommand(baseCommand + "_prod", this, "HandleProd");
            cmd.AddChatCommand(baseCommand, this, "ChatCommandDelegator");
            // build usage string for console (without sizing)
            usageString = WrapColor("orange", GetMessage("Header_Usage")) + "\n" +
                          WrapColor("cyan", $"{baseCommand}.{Command.def.ToString()}") + $" - {GetMessage("Cmd_Usage_def")}{Environment.NewLine}" +
                          WrapColor("cyan", $"{baseCommand}.{Command.trace.ToString()}") + $" - {GetMessage("Cmd_Usage_trace")}{Environment.NewLine}" +
                          WrapColor("cyan", $"{baseCommand}.{Command.sched.ToString()} [enable|disable]") + $" - {GetMessage("Cmd_Usage_sched")}{Environment.NewLine}" +
                          WrapColor("cyan", $"/{baseCommand}_prod") + $" - {GetMessage("Cmd_Usage_prod")}{Environment.NewLine}" +
                          WrapColor("cyan", $"/{baseCommand} map") + $" - {GetMessage("Cmd_Usage_map")}";
            permission.RegisterPermission(PermCanMap, this);
        }

        // on unloaded
        void Unload()
        {
            if(scheduleUpdateTimer != null)
                scheduleUpdateTimer.Destroy();
            Instance = null;
        }

        // plugin loaded
        void OnPluginLoaded(Plugin plugin)
        {
            if(plugin.Name == "ZoneManager")
                ZoneManager = plugin;
            if (plugin.Name == "LiteZones")
                LiteZones = plugin;
            if (!serverInitialized) return;
            if (ZoneManager != null || LiteZones != null)
                useZones = data.config[Option.useZones];
        }

        // plugin unloaded
        void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin.Name == "ZoneManager")
                ZoneManager = null;
            if (plugin.Name == "LiteZones")
                LiteZones = null;
            if (!serverInitialized) return;
            if (ZoneManager == null && LiteZones == null)
                useZones = false;
            traceTimer?.Destroy();
        }

        // server initialized
        void OnServerInitialized()
        {
            // check for server pve setting
            if (ConVar.Server.pve)
                WarnPve();
            // load configuration
            LoadConfiguration();
            data.Init();
            currentRuleSet = data.GetDefaultRuleSet();
            if (currentRuleSet == null)
                PrintWarning(GetMessage("Warning_NoRuleSet"), data.defaultRuleSet);
            useZones = data.config[Option.useZones] && (LiteZones != null || ZoneManager != null);
            if (useZones && data.mappings.Count == 1 && data.mappings.First().Key.Equals(data.defaultRuleSet))
                useZones = false;
            if (data.schedule.enabled)
                TimerLoop(true);
            serverInitialized = true;
        }

        #endregion

        #region Command Handling

        // delegation method for console commands
        void CommandDelegator(ConsoleSystem.Arg arg)
        {
            // return if user doesn't have access to run console command
            if(!HasAccess(arg)) return;

            string cmd = arg.cmd.Name;
            if(!Enum.IsDefined(typeof(Command), cmd))
            {
                // shouldn't hit this
                SendMessage(arg, "Error_InvalidParameter");
            }
            else
            {
                switch((Command) Enum.Parse(typeof(Command), cmd))
                {
                    case Command.def:
                        HandleDef(arg);
                        return;
                    case Command.sched:
                        HandleScheduleSet(arg);
                        return;
                    case Command.trace:
                        trace = !trace;
                        SendMessage(arg, "Notify_TraceToggle", new object[] { trace ? "on" : "off" });
                        if (trace)
                            traceTimer = timer.In(traceTimeout, () => trace = false);
                        else
                            traceTimer?.Destroy();
                        return;
                    case Command.usage:
                        ShowUsage(arg);
                        return;
                }
                SendMessage(arg, "Error_InvalidParamForCmd", new object[] {cmd});
            }
            ShowUsage(arg);
        }

        // handle setting defaults
        void HandleDef(ConsoleSystem.Arg arg)
        {
            LoadDefaultConfiguration();
            SendMessage(arg, "Notify_DefConfigLoad");
            LoadDefaultData();
            SendMessage(arg, "Notify_DefDataLoad");

            SaveData();
        }

        // handle prod command (raycast to determine what player is looking at)
        void HandleProd(BasePlayer player, string command, string[] args)
        {
            if(!IsAdmin(player))
                SendMessage(player, "Error_NoPermission");

            object entity;
            if(!GetRaycastTarget(player, out entity) || entity == null)
            {
                SendReply(player, WrapSize(12, WrapColor("red", GetMessage("Error_NoEntityFound", player.UserIDString))));
                return;
            }
            SendMessage(player, "Notify_ProdResult", new object[] { entity.GetType(), (entity as BaseEntity).ShortPrefabName });
        }

        // delegation method for chat commands
        void ChatCommandDelegator(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, PermCanMap))
            {
                SendMessage(player, "Error_NoPermission");
                return;
            }

            // assume args[0] is the command (beyond /tpve)
            if (args != null && args.Length > 0)
                command = args[0];
            // shift arguments
            if (args != null)
            {
                if (args.Length > 1)
                    args = args.Skip(1).ToArray();
                else
                    args = new string[] { };
            }

            string message = "";
            object[] opts = new object[] { };

            if (command == null || command != "map")
            {
                message = "Error_InvalidCommand";
            }
            else if (args == null || args.Length == 0)
            {
                message = "Error_InvalidParamForCmd";
                opts = new object[] { command };
            }
            else
            {
                // args[0] should be mapping name
                // args[1] if exists should be target ruleset or "exclude"
                // if args[1] is empty, delete mapping
                string from = args[0];
                string to = null;
                if(args.Length == 2)
                    to = args[1];

                if (to != null && !data.ruleSets.Select(r => r.name).Contains(to) && to != "exclude")
                {
                    // target ruleset must exist, or be "exclude"
                    message = "Error_InvalidMapping";
                    opts = new object[] { from, to };
                }
                else
                {
                    bool dirty = false;
                    if (to != null)
                    {
                        dirty = true;
                        if (data.HasMapping(from))
                        {
                            // update existing mapping
                            string old = data.mappings[from];
                            data.mappings[from] = to;
                            message = "Notify_MappingUpdated";
                            opts = new object[] { from, old, to };
                        }
                        else
                        {
                            // add new mapping
                            data.mappings.Add(from, to);
                            message = "Notify_MappingCreated";
                            opts = new object[] { from, to };
                        }
                    }
                    else
                    {
                        if (data.HasMapping(from))
                        {
                            dirty = true;
                            // remove mapping
                            string old = data.mappings[from];
                            data.mappings.Remove(from);
                            message = "Notify_MappingDeleted";
                            opts = new object[] { from, old };
                        }
                        else
                        {
                            message = "Error_NoMappingToDelete";
                            opts = new object[] { from };
                        }
                    }

                    if(dirty)
                        // save changes to config file
                        SaveData();
                }
            }
            SendMessage(player, message, opts);
        }

        // handles schedule enable/disable
        void HandleScheduleSet(ConsoleSystem.Arg arg)
        {
            if (arg == null || arg.Args == null || arg.Args.Length == 0)
            {
                SendMessage(arg, "Error_InvalidParamForCmd");
                return;
            }
            string message = "";
            if(!data.schedule.valid)
            {
                message = "Notify_InvalidSchedule";
            }
            else if(arg.Args[0] == "enable")
            {
                if(data.schedule.enabled) return;
                data.schedule.enabled = true;
                TimerLoop();
                message = "Notify_SchedSetEnabled";
            }
            else if(arg.Args[0] == "disable")
            {
                if (!data.schedule.enabled) return;
                data.schedule.enabled = false;
                if (scheduleUpdateTimer != null)
                    scheduleUpdateTimer.Destroy();
                message = "Notify_SchedSetDisabled";
            }
            object[] opts = new object[] { };
            if(message == "")
            {
                message = "Error_InvalidParameter";
                opts = new object[] { arg.Args[0] };
            }
            SendMessage(arg, message, opts);
        }

        #endregion

        #region Configuration/Data

        // load config
        void LoadConfiguration()
        {
            CheckVersion();
            Config.Settings.NullValueHandling = NullValueHandling.Include;
            bool dirty = false;
            try {
                data = Config.ReadObject<TruePVEData>() ?? null;
            } catch (Exception) {
                data = new TruePVEData();
            }
            if (data == null)
                LoadDefaultConfig();

            dirty |= CheckConfig();
            dirty |= CheckData();
            // check config version, update version to current version
            if (data.configVersion == null || !data.configVersion.Equals(Version.ToString()))
            {
                data.configVersion = Version.ToString();
                dirty |= true;
            }
            if (dirty)
                SaveData();
        }

        // save data
        void SaveData() => Config.WriteObject(data);

        // verify/update configuration
        bool CheckConfig()
        {
            bool dirty = false;
            foreach(Option option in Enum.GetValues(typeof(Option)))
                if(!data.config.ContainsKey(option))
                {
                    data.config[option] = defaults[(int)option];
                    dirty = true;
                }
            return dirty;
        }

        // check rulesets and groups
        bool CheckData()
        {
            bool dirty = false;
            if ((data.ruleSets == null || data.ruleSets.Count == 0) ||
                (data.groups == null || data.groups.Count == 0))
                dirty = LoadDefaultData();
            if (data.schedule == null)
            {
                data.schedule = new Schedule();
                dirty = true;
            }
            dirty |= CheckMappings();
            return dirty;
        }

        // rebuild mappings
        bool CheckMappings()
        {
            bool dirty = false;
            foreach (RuleSet rs in data.ruleSets)
                if (!data.mappings.ContainsValue(rs.name))
                {
                    data.mappings[rs.name] = rs.name;
                    dirty = true;
                }
            return dirty;
        }

        // default config creation
        protected override void LoadDefaultConfig()
        {
            data = new TruePVEData();
            data.configVersion = Version.ToString();
            LoadDefaultConfiguration();
            LoadDefaultData();
            SaveData();
        }

        void CheckVersion()
        {
            if (Config["configVersion"] == null) return;
            Version config = new Version(Config["configVersion"].ToString());
            if (config < new Version("0.7.0"))
            {
                string fname = Config.Filename.Replace(".json", ".old.json");
                Config.Save(fname);
                PrintWarning(string.Format(GetMessage("Warning_OldConfig"), fname));
                Config.Clear();
            }
        }

        // populates default configuration entries
        bool LoadDefaultConfiguration()
        {
            foreach (Option option in Enum.GetValues(typeof(Option)))
                data.config[option] = defaults[(int)option];
            return true;
        }

        // load default data to mappings, rulesets, and groups
        bool LoadDefaultData()
        {
            data.mappings.Clear();
            data.ruleSets.Clear();
            data.groups.Clear();
            data.schedule = new Schedule();
            data.defaultRuleSet = "default";

            // build groups first
            EntityGroup dispenser = new EntityGroup("dispensers");
            dispenser.Add(typeof(BaseCorpse).Name);
            dispenser.Add(typeof(HelicopterDebris).Name);
            data.groups.Add(dispenser);

            EntityGroup players = new EntityGroup("players");
            players.Add(typeof(BasePlayer).Name);
            data.groups.Add(players);

            EntityGroup traps = new EntityGroup("traps");
            traps.Add(typeof(AutoTurret).Name);
            traps.Add(typeof(BearTrap).Name);
            traps.Add(typeof(FlameTurret).Name);
            traps.Add(typeof(Landmine).Name);
            traps.Add(typeof(GunTrap).Name);
            traps.Add(typeof(ReactiveTarget).Name); // include targets with traps, since behavior is the same
            traps.Add("spikes.floor");
            data.groups.Add(traps);

            EntityGroup barricades = new EntityGroup("barricades");
            barricades.Add(typeof(Barricade).Name);
            data.groups.Add(barricades);

            EntityGroup highwalls = new EntityGroup("highwalls");
            highwalls.Add("wall.external.high.stone");
            highwalls.Add("wall.external.high.wood");
            highwalls.Add("gates.external.high.wood");
            highwalls.Add("gates.external.high.wood");
            data.groups.Add(highwalls);

            EntityGroup heli = new EntityGroup("heli");
            heli.Add(typeof(BaseHelicopter).Name);
            data.groups.Add(heli);

            EntityGroup npcs = new EntityGroup("npcs");
            npcs.Add(typeof(NPCPlayerApex).Name);
            npcs.Add(typeof(BradleyAPC).Name);
            data.groups.Add(npcs);

            EntityGroup fire = new EntityGroup("fire"); ;
            fire.Add(typeof(FireBall).Name);
            data.groups.Add(fire);

            EntityGroup resources = new EntityGroup("resources");
            resources.Add(typeof(ResourceEntity).Name);
            resources.Add(typeof(TreeEntity).Name);
            resources.Add(typeof(OreResourceEntity).Name);
            data.groups.Add(resources);

            // create default ruleset
            RuleSet defaultRuleSet = new RuleSet(data.defaultRuleSet);
            defaultRuleSet.flags = RuleFlags.HumanNPCDamage | RuleFlags.LockedBoxesImmortal | RuleFlags.LockedDoorsImmortal;

            // create rules and add to ruleset
            defaultRuleSet.AddRule("anything can hurt " + dispenser.name); // anything hurts dispensers
            defaultRuleSet.AddRule("anything can hurt " + players.name); // anything hurts players
            defaultRuleSet.AddRule(players.name + " cannot hurt " + players.name); // players cannot hurt other players
            defaultRuleSet.AddRule("anything can hurt " + traps.name); // anything hurts traps
            defaultRuleSet.AddRule(traps.name + " cannot hurt " + players.name); // traps cannot hurt players
            defaultRuleSet.AddRule(players.name + " can hurt " + barricades.name); // players can hurt barricades
            defaultRuleSet.AddRule(barricades.name + " cannot hurt " + players.name); // barricades cannot hurt players
            defaultRuleSet.AddRule(highwalls.name + " cannot hurt " + players.name); // highwalls cannot hurt players
            defaultRuleSet.AddRule("anything can hurt " + heli.name); // anything can hurt heli
            defaultRuleSet.AddRule("anything can hurt " + npcs.name); // anything can hurt npcs
            defaultRuleSet.AddRule(fire.name + " cannot hurt " + players.name); // fire cannot hurt players
            defaultRuleSet.AddRule("anything can hurt " + resources.name); // anything can hurt resources (gather)

            data.ruleSets.Add(defaultRuleSet); // add ruleset to rulesets list

            data.mappings[data.defaultRuleSet] = data.defaultRuleSet; // create mapping for ruleset

            return true;
        }

        #endregion

        #region Hooks/Handler Procedures

        void OnPlayerInit(BasePlayer player)
        {
            if (data.schedule.enabled && data.schedule.broadcast && currentBroadcastMessage != null)
                SendReply(player, GetMessage("Prefix") + currentBroadcastMessage);
        }

        // handle damage - if another mod must override TruePVE damages or take priority,
        // set handleDamage to false and reference HandleDamage from the other mod(s)
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if(!data.config[Option.handleDamage])
                return null;
            return HandleDamage(entity, hitinfo);
        }

        // handle damage
        object HandleDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (!AllowDamage(entity, hitinfo))
                return true;
            return null;
        }

        // determines if an entity is "allowed" to take damage
        bool AllowDamage(BaseEntity entity, HitInfo hitinfo)
        {
            object extCanTakeDamage = Interface.CallHook("CanEntityTakeDamage", new object[] { entity, hitinfo });
            if (extCanTakeDamage != null)
                return (bool) extCanTakeDamage;

            // if default global is not enabled, return true (allow all damage)
            if (currentRuleSet == null || currentRuleSet.IsEmpty() || !currentRuleSet.enabled)
                return true;

            if (entity == null || hitinfo == null) return true;

            // allow decay
            if (hitinfo.damageTypes.Get(DamageType.Decay) > 0)
                return true;

            // allow NPCs to take damage
            if (entity is BaseNpc)
                return true;

            // allow damage to door barricades and covers
            if(entity is Barricade && (entity.ShortPrefabName.Contains("door_barricade") || entity.ShortPrefabName.Contains("cover")))
                return true;

            // if entity is a barrel, trash can, or giftbox, allow damage (exclude water barrels)
            if(entity.ShortPrefabName.Contains("barrel") ||
               entity.ShortPrefabName.Equals("loot_trash") ||
               entity.ShortPrefabName.Equals("giftbox_loot"))
                if(!entity.ShortPrefabName.Equals("waterbarrel"))
                    return true;

            if (trace)
            {
                Trace("======================" + Environment.NewLine +
                      "==  STARTING TRACE  ==" + Environment.NewLine +
                      "==  " + DateTime.Now.ToString("HH:mm:ss.fffff") + "  ==" + Environment.NewLine +
                      "======================");
                Trace($"From: {hitinfo.Initiator.GetType().Name}, {hitinfo.Initiator.ShortPrefabName}", 1);
                Trace($"To: {entity.GetType().Name}, {entity.ShortPrefabName}", 1);
            }
            // get entity and initiator locations (zones)
            List<string> entityLocations = GetLocationKeys(entity);
            List<string> initiatorLocations = GetLocationKeys(hitinfo.Initiator);
            // check for exclusion zones (zones with no rules mapped)
            if (CheckExclusion(entityLocations, initiatorLocations)) return true;

            if (trace) Trace("No exclusion found - looking up RuleSet...", 1);
            // process location rules
            RuleSet ruleSet = GetRuleSet(entityLocations, initiatorLocations);
            if (trace) Trace($"Using RuleSet \"{ruleSet.name}\"", 1);

            // handle suicide
            if (hitinfo.damageTypes.Get(DamageType.Suicide) > 0)
            {
                if (trace) Trace($"DamageType is suicide; blocked? { (ruleSet.HasFlag(RuleFlags.SuicideBlocked) ? "true; block and return" : "false; continue processing") }", 1);
                if (ruleSet.HasFlag(RuleFlags.SuicideBlocked))
                {
                    SendMessage(entity as BasePlayer, "Error_NoSuicide");
                    return false;
                }
                return true;
            }

            // Check storage containers and doors for locks
            if ((entity is StorageContainer && ruleSet.HasFlag(RuleFlags.LockedBoxesImmortal)) ||
               (entity is Door && ruleSet.HasFlag(RuleFlags.LockedDoorsImmortal)))
            {
                // check for lock
                object hurt = CheckLock(ruleSet, entity, hitinfo);
                if (trace) Trace($"Door/StorageContainer detected with immortal flag; lock check results: { (hurt == null ? "null (no lock or unlocked); continue checks" : (bool)hurt ? "allow and return" : "block and return") }", 1);
                if (hurt != null)
                    return (bool)hurt;
            }

            // check heli
            object heli = CheckHeliInitiator(ruleSet, hitinfo);
            if(heli != null)
            {
                if (entity is BasePlayer)
                {
                    if (trace) Trace($"Initiator is heli, and target is player; flag check results: { (ruleSet.HasFlag(RuleFlags.NoHeliDamagePlayer) ? "flag set; block and return" : "flag not set; allow and return") }", 1);
                    return !ruleSet.HasFlag(RuleFlags.NoHeliDamagePlayer);
                }
                if (trace) Trace($"Initiator is heli, target is non-player; results: { ((bool)heli ? "allow and return" : "block and return") }", 1);
                return (bool)heli;
            }
            // after heli check, return true if initiator is null
            if (hitinfo.Initiator == null)
            {
                if (trace) Trace("Initiator empty; allow and return", 1);
                return true;
            }

            // check for sleeper protection - return false if sleeper protection is on (true)
            if (ruleSet.HasFlag(RuleFlags.ProtectedSleepers) && hitinfo.Initiator is BaseNpc && entity is BasePlayer && (entity as BasePlayer).IsSleeping())
            {
                if (trace) Trace("Target is sleeping player, with ProtectedSleepers flag set; block and return", 1);
                return false;
            }

            // allow NPC damage to other entities if sleeper protection is off
            if (hitinfo.Initiator is BaseNpc)
            {
                if (trace) Trace("Initiator is NPC animal; allow and return", 1);
                return true;
            }

            // ignore checks if authorized damage enabled (except for players)
            if (ruleSet.HasFlag(RuleFlags.AuthorizedDamage) && !(entity is BasePlayer) && hitinfo.Initiator is BasePlayer && CheckAuthorized(entity, hitinfo.Initiator as BasePlayer, ruleSet))
            {
                if (trace) Trace("Initiator is player with authorization over non-player target; allow and return", 1);
                return true;
            }

            // allow sleeper damage by admins if configured
            if (ruleSet.HasFlag(RuleFlags.AdminsHurtSleepers) && entity is BasePlayer && hitinfo.Initiator is BasePlayer)
                if ((entity as BasePlayer).IsSleeping() && IsAdmin(hitinfo.Initiator as BasePlayer))
                {
                    if (trace) Trace("Initiator is admin player and target is sleeping player, with AdminsHurtSleepers flag set; allow and return", 1);
                    return true;
                }

            // allow Human NPC damage if configured
            if (ruleSet.HasFlag(RuleFlags.HumanNPCDamage) && entity is BasePlayer && hitinfo.Initiator is BasePlayer)
                if (IsHumanNPC(entity as BasePlayer) || IsHumanNPC(hitinfo.Initiator as BasePlayer))
                {
                    if (trace) Trace("Initiator or target is HumanNPC, with HumanNPCDamage flag set; allow and return", 1);
                    return true;
                }

            if (trace) Trace("No match in pre-checks; evaluating RuleSet rules...", 1);
            return EvaluateRules(entity, hitinfo, ruleSet);
        }

        // process rules to determine whether to allow damage
        bool EvaluateRules(BaseEntity entity, HitInfo hitinfo, RuleSet ruleSet)
        {
            List<string> e0Groups = data.ResolveEntityGroups(hitinfo.Initiator);
            List<string> e1Groups = data.ResolveEntityGroups(entity);
            if (trace)
            {
                Trace($"Initator EntityGroup matches: { (e0Groups.Count == 0 ? "none" : string.Join(", ", e0Groups.ToArray())) }", 2);
                Trace($"Target EntityGroup matches: { (e1Groups.Count == 0 ? "none" : string.Join(", ", e1Groups.ToArray())) }", 2);
            }
            return ruleSet.Evaluate(e0Groups, e1Groups);
        }

        // checks for a lock
        object CheckLock(RuleSet ruleSet, BaseEntity entity, HitInfo hitinfo)
        {
            // exclude deployed items in storage container lock check (since they can't have locks)
            if(entity.ShortPrefabName.Equals("lantern.deployed") ||
               entity.ShortPrefabName.Equals("ceilinglight.deployed") ||
               entity.ShortPrefabName.Equals("furnace.large") ||
               entity.ShortPrefabName.Equals("campfire") ||
               entity.ShortPrefabName.Equals("furnace") ||
               entity.ShortPrefabName.Equals("refinery_small_deployed") ||
               entity.ShortPrefabName.Equals("waterbarrel") ||
               entity.ShortPrefabName.Equals("jackolantern.angry") ||
               entity.ShortPrefabName.Equals("jackolantern.happy") ||
               entity.ShortPrefabName.Equals("repairbench_deployed") ||
               entity.ShortPrefabName.Equals("researchtable_deployed") ||
               entity.ShortPrefabName.Contains("shutter"))
                return null;

            // if unlocked damage allowed - check for lock
            BaseLock alock = entity.GetSlot(BaseEntity.Slot.Lock) as BaseLock; // get lock
            if (alock == null) return null; // no lock, return null

            if (alock.IsLocked()) // is locked, cancel damage except heli
            {
                // if heliDamageLocked option is false or heliDamage is false, all damage is cancelled
                if(!ruleSet.HasFlag(RuleFlags.HeliDamageLocked) || ruleSet.HasFlag(RuleFlags.NoHeliDamage)) return false;
                object heli = CheckHeliInitiator(ruleSet, hitinfo);
                if(heli != null)
                    return (bool) heli;
                return false;
            }
            return null;
        }

        // check for heli
        object CheckHeliInitiator(RuleSet ruleSet, HitInfo hitinfo)
        {
            // Check for heli initiator
            if(hitinfo.Initiator is BaseHelicopter ||
               (hitinfo.Initiator != null && (
                   hitinfo.Initiator.ShortPrefabName.Equals("oilfireballsmall") ||
                   hitinfo.Initiator.ShortPrefabName.Equals("napalm"))))
                return !ruleSet.HasFlag(RuleFlags.NoHeliDamage);
            else if(hitinfo.WeaponPrefab != null) // prevent null spam
            {
                if(hitinfo.WeaponPrefab.ShortPrefabName.Equals("rocket_heli") ||
                   hitinfo.WeaponPrefab.ShortPrefabName.Equals("rocket_heli_napalm"))
                    return !ruleSet.HasFlag(RuleFlags.NoHeliDamage);
            }
            return null;
        }

        // checks if the player is authorized to damage the entity
        bool CheckAuthorized(BaseEntity entity, BasePlayer player, RuleSet ruleSet)
        {
            // check if the player is the owner of the entity
            if ((!ruleSet.HasFlag(RuleFlags.CupboardOwnership) && player.userID == entity.OwnerID) || entity.OwnerID == 0L)
                return true; // player is the owner or the owner is undefined, allow damage/looting

            // block if building blocked
            if (player.IsBuildingBlocked(entity.transform.position, entity.transform.rotation, entity.bounds))
                return false;

            // if not CupboardOwnership, check for build authorization
            if (!ruleSet.HasFlag(RuleFlags.CupboardOwnership))
                return player.IsBuildingAuthed(entity.transform.position, entity.transform.rotation, entity.bounds);

            // else, allow damage
            return true;
        }

        // handle player attacking an entity - specifically, checks resource dispensers
        // to determine whether to prevent gathering, based on rules
        object OnPlayerAttack(BasePlayer attacker, HitInfo hitinfo)
        {
            if(hitinfo?.HitEntity is ResourceEntity)
            {
                if (!AllowDamage(hitinfo.HitEntity, hitinfo))
                    return false;
            }
            return null;
        }

        // check if entity can be targeted
        object CanBeTargeted(BaseCombatEntity target, object turret)
        {
            if (!serverInitialized || target == null || turret == null) return null;
            if (turret as HelicopterTurret)
                return null;
            if (target.GetType().IsSubclassOf(typeof(BasePlayer))) return null;
            BasePlayer player = target as BasePlayer;
            if (player == null) return null;
            RuleSet ruleSet = GetRuleSet(player, turret as BaseCombatEntity);
            if (ruleSet.HasFlag(RuleFlags.TurretsIgnorePlayers))
                return false;
            return null;
        }

        // ignore players stepping on traps if configured
        object OnTrapTrigger(BaseTrap trap, GameObject go)
        {
            BasePlayer player = go.GetComponent<BasePlayer>();
            if (player == null || trap == null) return null;
            RuleSet ruleSet = GetRuleSet(trap, player);
            if(ruleSet.HasFlag(RuleFlags.TrapsIgnorePlayers))
                return false;
            return null;
        }

        // Check exclusion for entities
        bool CheckExclusion(BaseEntity entity0, BaseEntity entity1)
        {
            // check for exclusion zones (zones with no rules mapped)
            List<string> e0Locations = GetLocationKeys(entity0);
            List<string> e1Locations = GetLocationKeys(entity1);
            return CheckExclusion(e0Locations, e1Locations);
        }

        RuleSet GetRuleSet(List<string> e0Locations, List<string> e1Locations)
        {
            RuleSet ruleSet = currentRuleSet;
            if (e0Locations != null && e1Locations != null && e0Locations.Count() > 0 && e1Locations.Count() > 0)
            {
                if(trace) Trace($"Beginning RuleSet lookup for [{ (e0Locations.Count == 0 ? "empty" : string.Join(", ", e0Locations.ToArray())) }] and [{ (e1Locations.Count == 0 ? "empty" : string.Join(", ", e1Locations.ToArray())) }]", 2);
                List<string> locations = GetSharedLocations(e0Locations, e1Locations);
                if (trace) Trace($"Shared locations: { (locations.Count == 0 ? "none" : string.Join(", ", locations.ToArray())) }", 3);
                if (locations != null && locations.Count > 0)
                {
                    List<string> names = locations.Select(s => data.mappings[s]).ToList();
                    List<RuleSet> sets = data.ruleSets.Where(r => names.Contains(r.name)).ToList();
                    if (trace) Trace($"Found {names.Count} location names, with {sets.Count} mapped RuleSets", 3);
                    if (sets.Count == 0 && data.mappings.ContainsKey(AllZones) && data.ruleSets.Any(r => r.name == data.mappings[AllZones]))
                    {
                        sets.Add(data.ruleSets.FirstOrDefault(r => r.name == data.mappings[AllZones]));
                        if (trace) Trace($"Found allzones mapped RuleSet", 3);
                    }

                    if (sets.Count > 1)
                    {
                        if (trace) Trace($"WARNING: Found multiple RuleSets: {string.Join(", ", sets.Select(s => s.name).ToArray())}", 3);
                        PrintWarning(GetMessage("Warning_MultipleRuleSets"), string.Join(", ", sets.Select(s => s.name).ToArray()));
                    }

                    ruleSet = sets.FirstOrDefault();
                    if (trace && ruleSet != null) Trace($"Found RuleSet: {ruleSet.name}", 3);
                }
            }
            if (ruleSet == null)
            {
                ruleSet = currentRuleSet;
                if (trace) Trace($"No RuleSet found; assigned current global RuleSet: {ruleSet.name}", 3);
            }
            return ruleSet;
        }

        RuleSet GetRuleSet(BaseEntity e0, BaseEntity e1)
        {
            List<string> e0Locations = GetLocationKeys(e0);
            List<string> e1Locations = GetLocationKeys(e1);

            return GetRuleSet(e0Locations, e1Locations);
        }

        // get locations shared between the two passed location lists
        List<string> GetSharedLocations(List<string> e0Locations, List<string> e1Locations)
        {
            return e0Locations.Intersect(e1Locations).Where(s => data.HasMapping(s)).ToList();
        }

        // Check exclusion for given entity locations
        bool CheckExclusion(List<string> e0Locations, List<string> e1Locations)
        {
            if (e0Locations == null || e1Locations == null)
            {
                if (trace) Trace("No shared locations (empty location) - no exclusions", 3);
                return false;
            }
            if (trace) Trace($"Checking exclusions between [{ (e0Locations.Count == 0 ? "empty" : string.Join(", ", e0Locations.ToArray())) }] and [{ (e1Locations.Count == 0 ? "empty" : string.Join(", ", e1Locations.ToArray())) }]", 2);
            List<string> locations = GetSharedLocations(e0Locations, e1Locations);
            if (trace) Trace($"Shared locations: {(locations.Count == 0 ? "none" : string.Join(", ", locations.ToArray()))}", 3);
            if (locations != null && locations.Count > 0)
                foreach (string loc in locations)
                    if (data.HasEmptyMapping(loc))
                    {
                        if (trace) Trace($"Found exclusion mapping for location: {loc}", 3);
                        return true;
                    }
            if (trace) Trace("No shared locations, or no matching exclusion mapping - no exclusions)", 3);
            return false;
        }

        // add or update a mapping
        bool AddOrUpdateMapping(string key, string ruleset)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            if (ruleset == null || (!data.ruleSets.Select(r => r.name).Contains(ruleset) && ruleset != "exclude"))
                return false;

            if (data.HasMapping(key))
                // update existing mapping
                data.mappings[key] = ruleset;
            else
                // add new mapping
                data.mappings.Add(key, ruleset);
            SaveData();

            return true;
        }

        // remove a mapping
        bool RemoveMapping(String key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            if (data.HasMapping(key))
            {
                data.mappings.Remove(key);
                SaveData();
                return true;
            }
            return false;
        }

        #endregion

        #region Messaging

        // send message to player (chat)
        void SendMessage(BasePlayer player, string key, object[] options = null) => SendReply(player, BuildMessage(player, key, options));

        // send message to player (console)
        void SendMessage(ConsoleSystem.Arg arg, string key, object[] options = null) => SendReply(arg, BuildMessage(null, key, options));

        // build message string
        string BuildMessage(BasePlayer player, string key, object[] options = null)
        {
            string message = player == null ? GetMessage(key) : GetMessage(key, player.UserIDString);
            if (options != null && options.Length > 0)
                message = string.Format(message, options);
            string type = key.Split('_')[0];
            if (player != null)
            {
                string size = GetMessage("Format_" + type + "Size");
                string color = GetMessage("Format_" + type + "Color");
                return WrapSize(size, WrapColor(color, message));
            }
            else
            {
                string color = GetMessage("Format_" + type + "Color");
                return WrapColor(color, message);
            }
        }

        // prints the value of an Option
        private void PrintValue(ConsoleSystem.Arg arg, Option opt)
        {
            SendReply(arg, WrapSize(GetMessage("Format_NotifySize"), WrapColor(GetMessage("Format_NotifyColor"), opt + ": ") + data.config[opt]));
        }

        // wrap string in <size> tag, handles parsing size string to integer
        string WrapSize(string size, string input)
        {
            int i = 0;
            if(int.TryParse(size, out i))
                return WrapSize(i, input);
            return input;
        }

        // wrap a string in a <size> tag with the passed size
        string WrapSize(int size, string input)
        {
            if(input == null || input.Equals(""))
                return input;
            return "<size=" + size + ">" + input + "</size>";
        }

        // wrap a string in a <color> tag with the passed color
        string WrapColor(string color, string input)
        {
            if(input == null || input.Equals("") || color == null || color.Equals(""))
                return input;
            return "<color=" + color + ">" + input + "</color>";
        }

        // show usage information
        void ShowUsage(ConsoleSystem.Arg arg) => SendReply(arg, usageString);

        // warn that the server is set to PVE mdoe
        void WarnPve() => PrintWarning(GetMessage("Warning_PveMode"));

        #endregion

        #region Helper Procedures

        // is admin
        private bool IsAdmin(BasePlayer player)
        {
            if (player == null) return false;
            if (player?.net?.connection == null) return true;
            return player.net.connection.authLevel > 0;
        }

        // check if player has permission or is an admin
        private bool hasPermission(BasePlayer player, string permname)
        {
            return IsAdmin(player) || permission.UserHasPermission(player.UserIDString, permname);
        }

        // is player a HumanNPC
        private bool IsHumanNPC(BasePlayer player)
        {
            return player.userID < 76560000000000000L && player.userID > 0L && !player.IsDestroyed;
        }

        // get location keys from ZoneManager (zone IDs) or LiteZones (zone names)
        private List<string> GetLocationKeys(BaseEntity entity)
        {
            if(!useZones || entity == null) return null;
            List<string> locations = new List<string>();
            if (ZoneManager != null)
            {
                List<string> zmloc = (List<string>)ZoneManager.Call("GetEntityZones", new object[] { entity });
                if (zmloc != null && zmloc.Count > 0)
                    locations.AddRange(zmloc);
            }
            if (LiteZones != null)
            {
                List<string> lzloc = (List<string>) LiteZones?.Call("GetEntityZones", new object[] { entity });
                if (lzloc != null && lzloc.Count > 0)
                    locations.AddRange(lzloc);
            }
            if (locations == null || locations.Count == 0) return null;
            return locations;
        }

        // check user access
        bool HasAccess(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                if (arg.Connection.authLevel < 1)
                {
                    SendMessage(arg, "Error_NoPermission");
                    return false;
                }
            }
            return true;
        }

        // handle raycast from player (for prodding)
        bool GetRaycastTarget(BasePlayer player, out object closestEntity)
        {
            closestEntity = false;

            RaycastHit hit;
            if(Physics.Raycast(player.eyes.HeadRay(), out hit, 10f))
            {
                closestEntity = hit.GetEntity();
                return true;
            }
            return false;
        }

        // loop to update current ruleset
        void TimerLoop(bool firstRun = false)
        {
            string ruleSetName;
            data.schedule.ClockUpdate(out ruleSetName, out currentBroadcastMessage);
            if (currentRuleSet.name != ruleSetName || firstRun)
            {
                currentRuleSet = data.ruleSets.FirstOrDefault(r => r.name == ruleSetName);
                if (currentRuleSet == null)
                    currentRuleSet = new RuleSet(ruleSetName); // create empty ruleset to hold name
                if (data.schedule.broadcast && currentBroadcastMessage != null)
                {
                    Server.Broadcast(currentBroadcastMessage, GetMessage("Prefix"));
                    Console.WriteLine(GetMessage("Prefix") + " Schedule Broadcast: " + currentBroadcastMessage);
                }
            }

            if (data.schedule.enabled)
                scheduleUpdateTimer = timer.Once(data.schedule.useRealtime ? 30f : 3f, () => TimerLoop());
        }

        internal void Trace(string message, int indentation = 0) => LogToFile(traceFile, "".PadLeft(indentation, ' ') + message, this);

        #endregion

        #region Subclasses

        // configuration and data storage container
        class TruePVEData
        {
            [JsonProperty(PropertyName="Config Version")]
            public string configVersion = null;
            [JsonProperty(PropertyName="Default RuleSet")]
            public string defaultRuleSet = "default";
            [JsonProperty(PropertyName="Configuration Options")]
            public Dictionary<Option,bool> config = new Dictionary<Option,bool>();
            [JsonProperty(PropertyName="Mappings")]
            public Dictionary<string,string> mappings = new Dictionary<string,string>();
            [JsonProperty(PropertyName="Schedule")]
            public Schedule schedule = new Schedule();
            [JsonProperty(PropertyName = "RuleSets")]
            public List<RuleSet> ruleSets = new List<RuleSet>();
            [JsonProperty(PropertyName = "Entity Groups")]
            public List<EntityGroup> groups = new List<EntityGroup>();

            Dictionary<uint, List<string>> groupCache = new Dictionary<uint, List<string>>();

            public void Init()
            {
                schedule.Init();
                foreach (RuleSet rs in ruleSets)
                    rs.Build();
                ruleSets.Remove(null);
            }

            public List<string> ResolveEntityGroups(BaseEntity entity)
            {
                if (entity == null || entity.net == null) return null;
                List<string> groupList;
                if (!groupCache.TryGetValue(entity.net.ID, out groupList))
                {
                    groupList = groups.Where(g => g.Contains(entity)).Select(g => g.name).ToList();
                    if(entity.net != null)
                        groupCache[entity.net.ID] = groupList;
                }
                return groupList;
            }

            public bool HasMapping(string key)
            {
                return mappings.ContainsKey(key) || mappings.ContainsKey(AllZones);
            }

            public bool HasEmptyMapping(string key)
            {
                if (mappings.ContainsKey(AllZones) && mappings[AllZones].Equals("exclude")) return true; // exlude all zones
                if (!mappings.ContainsKey(key)) return false;
                if (mappings[key].Equals("exclude")) return true;
                RuleSet r = ruleSets.First(rs => rs.name.Equals(mappings[key]));
                if (r == null) return true;
                return r.IsEmpty();
            }

            public RuleSet GetDefaultRuleSet()
            {
                try
                {
                    return ruleSets.Single(r => r.name == defaultRuleSet);
                }
                catch (Exception)
                {
                    Console.WriteLine("Warning - duplicate ruleset found for default RuleSet: \"" + defaultRuleSet + "\"");
                    return ruleSets.FirstOrDefault(r => r.name == defaultRuleSet);
                }
            }
        }

        class RuleSet
        {
            public string name;
            public bool enabled = true;
            public bool defaultAllowDamage = false;
            [JsonConverter(typeof(StringEnumConverter))]
            public RuleFlags flags = RuleFlags.None;
            public HashSet<string> rules = new HashSet<string>();
            HashSet<Rule> parsedRules = new HashSet<Rule>();

            public RuleSet() { }
            public RuleSet(string name) { this.name = name; }

            // evaluate the passed lists of entity groups against rules
            public bool Evaluate(List<string> eg1, List<string> eg2)
            {
                if (Instance.trace) Instance.Trace("Evaluating Rules...", 3);
                if (parsedRules == null || parsedRules.Count == 0)
                {
                    if (Instance.trace) Instance.Trace($"No rules found; returning default value: {defaultAllowDamage}", 4);
                    return defaultAllowDamage;
                }
                bool? res;
                if (Instance.trace) Instance.Trace("Checking direct initiator->target rules...", 4);
                // check all direct links
                if (eg1 != null && eg1.Count > 0 && eg2 != null && eg2.Count > 0)
                    foreach (string s1 in eg1)
                        foreach (string s2 in eg2)
                            if ((res = Evaluate(s1, s2)).HasValue) return res.Value;

                if (Instance.trace) Instance.Trace("No direct match rules found; continuing...", 4);
                if (eg1 != null && eg1.Count > 0)
                    // check group -> any
                    foreach (string s1 in eg1)
                        if ((res = Evaluate(s1, Any)).HasValue) return res.Value;

                if (Instance.trace) Instance.Trace("No matching initiator->any rules found; continuing...", 4);
                if (eg2 != null && eg2.Count > 0)
                    // check any -> group
                    foreach (string s2 in eg2)
                        if ((res = Evaluate(Any, s2)).HasValue) return res.Value;

                if (Instance.trace) Instance.Trace($"No matching any->target rules found; returning default value: {defaultAllowDamage}", 4);
                return defaultAllowDamage;
            }

            // evaluate two entity groups against rules
            public bool? Evaluate(string eg1, string eg2)
            {
                if (eg1 == null || eg2 == null || parsedRules == null || parsedRules.Count == 0) return null;
                if (Instance.trace) Instance.Trace($"Evaluating \"{eg1}->{eg2}\"...", 5);
                Rule rule = parsedRules.FirstOrDefault(r => r.valid && r.key.Equals(eg1 + "->" + eg2));
                if (rule != null)
                {
                    if (Instance.trace) Instance.Trace($"Match found; allow damage? {rule.hurt}", 6);
                    return rule.hurt;
                }
                if (Instance.trace) Instance.Trace($"No match found", 6);
                return null;
            }

            // build rule strings to rules
            public void Build()
            {
                foreach (string ruleText in rules)
                    parsedRules.Add(new Rule(ruleText));
                parsedRules.Remove(null);
                ValidateRules();
            }

            public void ValidateRules()
            {
                foreach(Rule rule in parsedRules)
                    if(!rule.valid)
                        Console.WriteLine("Warning - invalid rule: " + rule.ruleText);
            }

            // add a rule
            public void AddRule(string ruleText)
            {
                rules.Add(ruleText);
                parsedRules.Add(new Rule(ruleText));
            }

            public bool HasAnyFlag(RuleFlags flags) { return (this.flags | flags) != RuleFlags.None; }
            public bool HasFlag(RuleFlags flag) { return (flags & flag) == flag; }
            public bool IsEmpty() { return (rules == null || rules.Count == 0) && flags == RuleFlags.None; }
        }

        class Rule
        {
            public string ruleText;
            [JsonIgnore]
            public string key;
            [JsonIgnore]
            public bool hurt;
            [JsonIgnore]
            public bool valid;

            public Rule() { }
            public Rule(string ruleText)
            {
                this.ruleText = ruleText;
                valid = RuleTranslator.Translate(this);
            }

            public override int GetHashCode() { return key.GetHashCode(); }

            public override bool Equals(object obj)
            {
                if (obj == null) return false;
                if (obj == this) return true;
                if (obj is Rule)
                    return key.Equals((obj as Rule).key);
                return false;
            }
        }

        // helper class to translate rule text to rules
        class RuleTranslator
        {
            static readonly Regex regex = new Regex(@"\s+");
            static readonly List<string> synonyms = new List<string>() { "anything", "nothing", "all", "any", "none", "everything" };
            public static bool Translate(Rule rule)
            {
                if (rule.ruleText == null || rule.ruleText.Equals("")) return false;
                string str = rule.ruleText;
                string[] splitStr = regex.Split(str);
                // first and last words should be ruleset names
                string rs0 = splitStr[0];
                string rs1 = splitStr[splitStr.Length - 1];
                string[] mid = splitStr.Skip(1).Take(splitStr.Length-2).ToArray();
                if (mid == null || mid.Length == 0) return false;

                bool canHurt = true;
                foreach(string s in mid)
                    if (s.Equals("cannot") || s.Equals("can't"))
                        canHurt = false;

                // rs0 and rs1 shouldn't ever be "nothing" simultaneously
                if (rs0.Equals("nothing") || rs1.Equals("nothing") || rs0.Equals("none") || rs1.Equals("none")) canHurt = !canHurt;

                if (synonyms.Contains(rs0)) rs0 = Any;
                if (synonyms.Contains(rs1)) rs1 = Any;

                rule.key = rs0 + "->" + rs1;
                rule.hurt = canHurt;
                return true;
            }
        }

        // container for mapping entities
        class EntityGroup
        {
            public string name;
            public string members
            {
                get {
                    if (memberList == null || memberList.Count == 0) return "";
                    return string.Join(", ", memberList.ToArray());
                }
                set {
                    if (value == null || value.Equals("")) return;
                    memberList = value.Split(',').Select(s => s.Trim()).ToList();
                }
            }
            List<string> memberList = new List<string>();
            public string exclusions
            {
                get {
                    if (exclusionList == null || exclusionList.Count == 0) return "";
                    return string.Join(", ", exclusionList.ToArray());
                }
                set {
                    if (value == null || value.Equals("")) return;
                    memberList = value.Split(',').Select(s => s.Trim()).ToList();
                }
            }
            List<string> exclusionList = new List<string>();

            public EntityGroup() { }
            public EntityGroup(string name) { this.name = name; }
            public void Add(string prefabOrType)
            {
                memberList.Add(prefabOrType);
            }
            public bool Contains(BaseEntity entity)
            {
                if (entity == null) return false;
                return (memberList.Contains(entity.GetType().Name) || memberList.Contains(entity.ShortPrefabName)) &&
                      !(exclusionList.Contains(entity.GetType().Name) || exclusionList.Contains(entity.ShortPrefabName));
            }
        }

        // scheduler
        class Schedule
        {
            public bool enabled = false;
            public bool useRealtime = false;
            public bool broadcast = false;
            public List<string> entries = new List<string>();
            List<ScheduleEntry> parsedEntries = new List<ScheduleEntry>();
            [JsonIgnore]
            public bool valid = false;

            public void Init()
            {
                foreach (string str in entries)
                    parsedEntries.Add(new ScheduleEntry(str));
                // schedule not valid if entries are empty, there are less than 2 entries, or there are less than 2 rulesets defined
                if (parsedEntries == null || parsedEntries.Count == 0 || parsedEntries.Count(e => e.valid) < 2 || parsedEntries.Select(e => e.ruleSet).Distinct().Count() < 2)
                    enabled = false;
                else
                    valid = true;
            }

            // returns delta between current time and next schedule entry
            public void ClockUpdate(out string currentRuleSet, out string message)
            {
                TimeSpan time = useRealtime ? new TimeSpan((int)DateTime.Now.DayOfWeek, 0, 0, 0).Add(DateTime.Now.TimeOfDay) : TOD_Sky.Instance.Cycle.DateTime.TimeOfDay;
                try
                {
                    ScheduleEntry se = null;
                    // get the most recent schedule entry
                    if (parsedEntries.Where(t => !t.isDaily).Count() > 0)
                        se = parsedEntries.FirstOrDefault(e => e.time == parsedEntries.Where(t => t.valid && t.time <= time && ((useRealtime && !t.isDaily) || !useRealtime)).Max(t => t.time));
                    // if realtime, check for daily
                    if (useRealtime)
                    {
                        ScheduleEntry daily = null;
                        try
                        {
                            daily = parsedEntries.FirstOrDefault(e => e.time == parsedEntries.Where(t => t.valid && t.time <= DateTime.Now.TimeOfDay && t.isDaily).Max(t => t.time));
                        } catch(Exception)
                        { // no daily entries
                        }
                        if (daily != null && se == null)
                            se = daily;
                        if (daily != null && daily.time.Add(new TimeSpan((int)DateTime.Now.DayOfWeek, 0, 0, 0)) > se.time)
                            se = daily;
                    }
                    currentRuleSet = se.ruleSet;
                    message = se.message;
                }
                catch (Exception)
                {
                    ScheduleEntry se = null;
                    // if time is earlier than all schedule entries, use max time
                    if (parsedEntries.Where(t => !t.isDaily).Count() > 0)
                        se = parsedEntries.FirstOrDefault(e => e.time == parsedEntries.Where(t => t.valid && ((useRealtime && !t.isDaily) || !useRealtime)).Max(t => t.time));
                    if (useRealtime)
                    {
                        ScheduleEntry daily = null;
                        try
                        {
                            daily = parsedEntries.FirstOrDefault(e => e.time == parsedEntries.Where(t => t.valid && t.isDaily).Max(t => t.time));
                        } catch (Exception)
                        { // no daily entries
                        }
                        if (daily != null && se == null)
                            se = daily;
                        if (daily != null && daily.time.Add(new TimeSpan((int)DateTime.Now.DayOfWeek, 0, 0, 0)) > se.time)
                            se = daily;
                    }
                    currentRuleSet = se.ruleSet;
                    message = se.message;
                }
            }
        }

        // helper class to translate schedule text to schedule entries
        class ScheduleTranslator
        {
            static readonly Regex regex = new Regex(@"\s+");
            public static bool Translate(ScheduleEntry entry)
            {
                if (entry.scheduleText == null || entry.scheduleText.Equals("")) return false;
                string str = entry.scheduleText;
                string[] splitStr = regex.Split(str, 3); // split into 3 parts
                // first word should be a timespan
                string ts = splitStr[0];
                // second word should be a ruleset name
                string rs = splitStr[1];
                // remaining should be message
                string message = splitStr.Length > 2 ? splitStr[2] : null;

                try
                {
                    if (ts.StartsWith("*."))
                    {
                        entry.isDaily = true;
                        ts = ts.Substring(2);
                    }
                    entry.time = TimeSpan.Parse(ts);
                    entry.ruleSet = rs;
                    entry.message = message;
                    return true;
                }
                catch (Exception)
                { }

                return false;
            }
        }

        class ScheduleEntry
        {
            public string ruleSet;
            public string message;
            public string scheduleText;
            public bool valid;
            public TimeSpan time;
            [JsonIgnore]
            public bool isDaily = false;

            public ScheduleEntry() { }
            public ScheduleEntry(string scheduleText)
            {
                this.scheduleText = scheduleText;
                valid = ScheduleTranslator.Translate(this);
            }
        }

        #endregion
    }
}
