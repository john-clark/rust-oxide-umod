/*ToDo:  
 * BUG: if two nobuild zones overlap players will gain resources when placing a block.
1. Look into getting resources from salvage in a nodamage zone, and prefabs seem to give more resources.
 Known Bugs: 
 2 zones overlapping causes enter/exit messages to not work properly.
 For some reason when using a treb, and a ballista is outside of the structure your attacking, it does 1500 damage to the structure(in a nodamage zone) if the blocks already have damage
 */ 
using System.Collections.Generic;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using CodeHatch.Engine.Networking;
using CodeHatch.Blocks.Networking.Events;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.Common;
using CodeHatch.StarForge.Sleeping;
using CodeHatch.Thrones.Weapons.Salvage;
using CodeHatch.Thrones.SocialSystem;
using CodeHatch.Engine.Modules.SocialSystem;
using CodeHatch.Blocks.Inventory;
using CodeHatch.Blocks;
using CodeHatch.ItemContainer;
using CodeHatch.Engine.Core.Cache;
using CodeHatch.Engine.Events.Prefab;
using CodeHatch.Inventory.Blueprints;
using CodeHatch.Networking.Events;
using CodeHatch.TerrainAPI;
using CodeHatch.Networking.Events.Players;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info("ProtectedZone", "Mordeus", "1.3.2")]
    public class ProtectedZone : ReignOfKingsPlugin
    {
        [PluginReference]
        Plugin AntiLoot;

        private DynamicConfigFile ProtectedZoneData;
        private StoredData storedData;
        private readonly Dictionary<string, ZoneInfo> ZoneDefinitions = new Dictionary<string, ZoneInfo>();
        private Dictionary<Player, PlayerData> PData;
        public bool UpdateData = false;
        //config
        private bool MessagesOn => GetConfig("MessagesOn", false);
        private bool ZoneCheckOn => GetConfig("ZoneCheckOn", false);
        private float MessageInterval => GetConfig("MessageInterval", 100f);
        private float ZoneCheckInterval => GetConfig("ZoneCheckInterval", 1f);
        private bool CrestCheckOn => GetConfig("CrestCheckOn", false);
        private bool AdminCanBuild => GetConfig("AdminCanBuild", true);
        private bool AdminCanKill => GetConfig("AdminCanKill", true);
        private bool UseAntiLoot => GetConfig("UseAntiLoot", false);
        private bool TakeBlocks => GetConfig("TakeBlocksFromPlayer", false);
        List<Vector2> zones = new List<Vector2>();
        private Dictionary<string, Timer> timers = new Dictionary<string, Timer>();
        private Dictionary<string, Timer> ZoneCheckTimer = new Dictionary<string, Timer>();
        private bool AntiLootLoaded = false;

        #region Data
        protected override void LoadDefaultConfig()
        {
            Config["MessageInterval"] = MessageInterval;
            Config["ZoneCheckInterval"] = ZoneCheckInterval;
            Config["MessagesOn"] = MessagesOn;
            Config["ZoneCheckOn"] = ZoneCheckOn;
            Config["CrestCheckOn"] = CrestCheckOn;
            Config["AdminCanBuild"] = AdminCanBuild;
            Config["AdminCanKill"] = AdminCanKill;
            Config["UseAntiLoot"] = UseAntiLoot;
            Config["TakeBlocksFromPlayer"] = TakeBlocks;

            SaveConfig();
        }

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {

                { "notAllowed", "[F5D400]You are not allowed to do this![FFFFFF]" },
                { "areaProtected", "[F5D400]This area is Protected![FFFFFF]" },
                { "noBuild", "[F5D400]No building in this area![FFFFFF]" },
                { "noPlace", "[F5D400]You cannot place a {0} Here![FFFFFF]" },
                { "noPvP", "[F5D400]This area is Protected, no PvP![FFFFFF]" },
                { "noSleeper", "[F5D400]This area is Protected, You can not damage a sleeper![FFFFFF]" },
                { "noCrest", "[F5D400]This area is Protected, You can not damage a crest![FFFFFF]" },
                { "noRope", "[F5D400]This area is Protected, You can not rope another player![FFFFFF]" },
                { "noPVE", "[F5D400]This area is Protected, You can not kill{0}'s![FFFFFF]" },
                { "noPreFab", "[F5D400]This area is Protected, You can not damage a{0}![FFFFFF]" },
                { "noEntry", "[F5D400]You cannot enter this zone![FFFFFF]" },
                { "help", "[F5D400]type /zone help to open the help menu[FFFFFF]"},
                { "synError", "[F5D400]Syntax Error: [FFFFFF]Type '/zone help' to view available options" },
                { "nameExists", "[0000FF]The Name {0} already exists[FFFFFF]" },
                { "zoneAdded", "[4F9BFF]Zone [FFFFFF]{0}[4F9BFF] sucessfully added, named [FFFFFF]{1}." },
                { "zoneInfo", "[FFFFFF]This is ZoneID [4F9BFF]{0}[FFFFFF], Zone Name [4F9BFF]{1}[FFFFFF]" },
                { "zoneList", "[FFFFFF]ZoneID [4F9BFF]{0}[FFFFFF], Zone Name [4F9BFF]{1}[FFFFFF], Location [4F9BFF]{2}[FFFFFF]" },
                { "zoneEdited", "[4F9BFF]You have changed the {0} of ZoneID {1} to {2}.[FFFFFF]" },
                { "zoneLocError", "[F5D400]You are not standing in a zone.[FFFFFF]" },
                { "noZoneError",  "[F5D400]No Zones loaded.[FFFFFF]" },
                { "zoneError", "[F5D400]That zone does not exist.[FFFFFF]" },
                { "inZoneError", "[F5D400]You are currently to close to a zone, you cannot make another.[FFFFFF]" },
                { "zoneRemove", "[0000FF]ZoneID {0} was removed.[FFFFFF]" },
                { "zoneMessage", "[4F9BFF]You have entered {0} zone.[FFFFFF]" },
                { "zoneFlag1", "[4F9BFF]radius: [FFFFFF]{0}" },
                { "zoneFlag2", "[4F9BFF]nopvp Flag: [FFFFFF]{0}" },
                { "zoneFlag3", "[4F9BFF]nobuild Flag: [FFFFFF]{0}" },                
                { "zoneFlag4", "[4F9BFF]nodamage Flag: [FFFFFF]{0}" },
                { "zoneFlag5", "[4F9BFF]nosleeperdamage Flag: [FFFFFF]{0}" },
                { "zoneFlag6", "[4F9BFF]nocrestdamage Flag: [FFFFFF]{0}" },
                { "zoneFlag7", "[4F9BFF]messageon Flag: [FFFFFF]{0}" },
                { "zoneFlag8", "[4F9BFF]entermessageon Flag: [FFFFFF]{0}" },
                { "zoneFlag9", "[4F9BFF]exitmessageon Flag: [FFFFFF]{0}" },
                { "zoneFlag10", "[4F9BFF]zonemessage: [FFFFFF]{0}" },
                { "zoneFlag11", "[4F9BFF]entermessage: [FFFFFF]{0}" },
                { "zoneFlag12", "[4F9BFF]exitmessage: [FFFFFF]{0}" },
                { "zoneFlag13", "[4F9BFF]noroping Flag: [FFFFFF]{0}" },
                { "zoneFlag14", "[4F9BFF]nopve Flag: [FFFFFF]{0}" },
                { "zoneFlag15", "[4F9BFF]noprefabdamage Flag: [FFFFFF]{0}" },
                { "zoneFlag16", "[4F9BFF]ejectplayer Flag: [FFFFFF]{0}" },
                { "zoneFlag17", "[4F9BFF]ejectsleeper Flag: [FFFFFF]{0}" },
                { "zoneFlag18", "[4F9BFF]nochestlooting Flag: [FFFFFF]{0}"  },
                { "zoneFlag19", "[4F9BFF]nostationlooting Flag: [FFFFFF]{0}"  },
                { "zoneFlag20", "[4F9BFF]nocampfirelooting Flag: [FFFFFF]{0}" },
                { "zoneFlag21", "[4F9BFF]notorchlooting Flag: [FFFFFF]{0}" },
                { "zoneFlag22", "[4F9BFF]nofireplacelooting Flag: [FFFFFF]{0}" },
                { "zoneFlag23", "[4F9BFF]nofurniturelooting Flag: [FFFFFF]{0}" },
                { "zoneFlag24", "[4F9BFF]novillagerlooting Flag: [FFFFFF]{0}" },
                { "zoneFlag25", "[4F9BFF]nodecay Flag: [FFFFFF]{0}" },
                { "zoneFlag26", "[4F9BFF]noprefabplace Flag: [FFFFFF]{0}" },
                { "logPvP", "player {0} attempted to a harm a player ,cancelling damage." },
                { "logSleeper", "player {0} attempted to kill a Sleeper ,cancelling damage." },
                { "logCrest", "player {0} attempted to damage a crest ,cancelling damage." },
                { "logNoBuild", "player {0} attempted to build in a no-build zone,cancelling placement."},
                { "logNoDamage", "player {0} attempted to damage a block ,cancelling damage."},                
                { "logCrestPlace", "player {0} attempted to place a {1} in a no-build zone, cancelling placement."},
                { "logObjectPlace", "player {0} attempted to place a {1} in a no-prefab zone, cancelling placement."},
                { "logNoRope", "player {0} attempted to rope a player in a no-roping zone, cancelling."},
                { "logNoPVE", "player {0} attempted to kill a {1} in a no-pve zone, cancelling."},
                { "logNoPreFab", "player {0} attempted to damage a {1} in a no-prefab damage zone, cancelling."},
                { "logEjectSleeper", "player {0} ejected from a no-sleeper zone."},
                { "logEjectPlayer", "player {0} ejected from a no-player zone."},
                { "helpTitle", $"[4F9BFF]{Title}  v{Version}"},
                { "helpHelp", "[4F9BFF]/zone help[FFFFFF] - Display the help menu"},
                { "helpAdd", "[4F9BFF]/zone add <name> [FFFFFF]- Sets Zone."},
                { "helpList", "[4F9BFF]/zone list [FFFFFF]- Lists all zones"},
                { "helpRemove", "[4F9BFF]/zone remove <num> [FFFFFF]- Removes zone."},
                { "helpInfo", "[4F9BFF]/zone info [FFFFFF]- Zone info"},
                { "helpEdit", "[4F9BFF]/zone edit <name>[FFFFFF]- Edit zone values."},
                { "cslzoneAdded", "Zone {0} sucessfully addded, named {1}." },
                { "cslnameExists", "The Name {0} already exists" },
                { "cslinZoneError", "This is too close to a zone, you cannot make another." },
                { "cslformatError", "Invalid location format, use: \"x y z\" or here." },                               

        }, this);
        }
        void OnServerInitialized()
        {            
            CacheAllOnlinePlayers();
            if (UseAntiLoot)
            {
                try
                {
                    AntiLoot.Call("isLoaded", null);
                    Puts("AntiLoot is loaded, you now have more options in ProtectedZone.");
                    AntiLootLoaded = true;

                }
                catch (Exception)
                {
                    PrintWarning("AntiLoot is missing. Change AntiLootEnabled in the config to false to use without.");
                    AntiLootLoaded = false;                    
                    return;
                }     
                
            }
        }        
        private void Init()
        {
            ProtectedZoneData = Interface.Oxide.DataFileSystem.GetFile("ProtectedZone");
            ProtectedZoneData.Settings.Converters = new JsonConverter[] { new StringEnumConverter(), new UnityVector3Converter(), };
            LoadZones();
            LoadData();
            PData = new Dictionary<Player, PlayerData>();
        }
        private void LoadData()
        {
            
            ZoneDefinitions.Clear();
            try
            {
                ProtectedZoneData.Settings.NullValueHandling = NullValueHandling.Ignore;
                storedData = ProtectedZoneData.ReadObject<StoredData>();                  
                Puts("Loaded {0} Zone definitions", storedData.ZoneDefinitions.Count);
            }
            catch
            {
                Puts("Failed to load StoredData");
                storedData = new StoredData();
            }
            ProtectedZoneData.Settings.NullValueHandling = NullValueHandling.Include;

            foreach (var zonedef in storedData.ZoneDefinitions)
                ZoneDefinitions[zonedef.Id] = zonedef;
            SaveData();
        }       
        private class StoredData
        {
            public readonly HashSet<ZoneInfo> ZoneDefinitions = new HashSet<ZoneInfo>();
        }
        private void SaveData()
        {
            ProtectedZoneData.WriteObject(storedData);
            PrintWarning("Saved ProtectedZone data");
        }
        #endregion
        #region Zone Definition
        public class ZoneInfo
        {
            public string ZoneName;
            public string Id;
            public Vector3 Location;            
            public float ZoneX;
            public float ZoneY;
            public float ZoneZ;
            public string ZoneCreatorName;
            public float ZoneRadius;
            public bool ZoneNoPVP = false;
            public bool ZoneNoBuild = false;
            public bool ZoneNoPreFabPlace = false;
            public bool ZoneNoDamage = false;
            public bool ZoneNoDecay = false;
            public bool ZoneNoSleeperDamage = false;
            public bool ZoneNoCrestDamage = false;
            public bool ZoneNoPlayerRoping = false;
            public bool ZoneNoPVE = false;
            public bool ZoneNoPreFabDamage = false;
            public bool ZoneEjectPlayer = false;
            public bool ZoneEjectSleeper = false;
            public bool ZoneNoChestLooting = false;
            public bool ZoneNoStationLooting = false;
            public bool ZoneNoCampfireLooting = false;
            public bool ZoneNoFireplaceLooting = false;
            public bool ZoneNoTorchLooting = false;
            public bool ZoneNoFurnitureLooting = false;
            public bool ZoneNoVillagerLooting = false;
            public bool ZoneMessageOn = false;
            public bool ZoneEnterMessageOn = false;
            public bool ZoneExitMessageOn = false;
            public string ZoneMessage = "This is a no PvP zone.";
            public string EnterZoneMessage = "You have entered a no PvP zone.";
            public string ExitZoneMessage = "You have exited a no PvP zone.";
            public List<string> Flags = new List<string>();

            public ZoneInfo()
            {
            }

            public ZoneInfo(Vector3 position)
            {
                ZoneRadius = 20f;
                Location = position;
            }

        }
        #endregion
        #region Player Data
        class PlayerData
        {
            public ulong PlayerId;
            public bool EnterZone;
            public bool ExitZone;
            public bool InZone;
            public string ZoneId;
            public DateTime TimeEnterZone;

            public PlayerData(ulong playerId)
            {
                PlayerId = playerId;
                ZoneId = "0";
                EnterZone = false;
                ExitZone = false;
                InZone = false;
                TimeEnterZone = DateTime.Now;
            }
        }
        #endregion

        #region Commands

        [ChatCommand("zone")]
        private void ZoneCommand(Player player, string cmd, string[] args)
        {
            string playerId = player.Id.ToString();
            if (!player.HasPermission("admin"))
            {
                player.SendError(lang.GetMessage("notAllowed", this, playerId));
                return;
            }
            if (args == null || args.Length == 0)
            {
                player.SendError(lang.GetMessage("help", this, playerId));
                return;
            }
            switch (args[0])
            {
                case "help":
                    {

                        SendReply(player, lang.GetMessage("helpTitle", this, playerId));
                        SendReply(player, lang.GetMessage("helpHelp", this, playerId));
                        SendReply(player, lang.GetMessage("helpAdd", this, playerId));
                        SendReply(player, lang.GetMessage("helpList", this, playerId));
                        SendReply(player, lang.GetMessage("helpRemove", this, playerId));
                        SendReply(player, lang.GetMessage("helpInfo", this, playerId));
                        SendReply(player, lang.GetMessage("helpEdit", this, playerId));
                    }
                    return;

                case "add":
                    {
                        PlayerData Player = GetCache(player);
                        if (args.Length != 2)
                        {
                            SendReply(player, lang.GetMessage("helpAdd", this, playerId));
                            return;
                        }
                        string name = args[1];
                        foreach (var zoneDef in ZoneDefinitions)
                        {
                            if (zoneDef.Value.ZoneName == name)
                            {
                                SendReply(player, lang.GetMessage("nameExists", this, playerId), name);
                                return;
                            }
                            if (IsInZone(player, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) == true)
                            {
                                SendReply(player, lang.GetMessage("inZoneError", this, playerId));
                                return;
                            }
                        }
                        var newzoneinfo = new ZoneInfo(player.Entity.Position) { Id = UnityEngine.Random.Range(1, 99999999).ToString() };
                        if (ZoneDefinitions.ContainsKey(newzoneinfo.Id)) storedData.ZoneDefinitions.Remove(ZoneDefinitions[newzoneinfo.Id]);
                        ZoneDefinitions[newzoneinfo.Id] = newzoneinfo;
                        storedData.ZoneDefinitions.Add(newzoneinfo);
                        SaveData();
                        float zonex = player.Entity.Position.x;
                        float zoney = player.Entity.Position.y;
                        float zonez = player.Entity.Position.z;
                        ZoneDefinitions[newzoneinfo.Id].ZoneX = zonex;
                        ZoneDefinitions[newzoneinfo.Id].ZoneZ = zonez;
                        ZoneDefinitions[newzoneinfo.Id].ZoneName = name;
                        ZoneDefinitions[newzoneinfo.Id].ZoneCreatorName = player.ToString();
                        SendReply(player, lang.GetMessage("zoneAdded", this, playerId), newzoneinfo.Id, name);
                        SaveData();
                        LoadZones();
                        return;
                    }
                case "list":
                    foreach (var zoneDef in ZoneDefinitions)
                    {
                        SendReply(player, lang.GetMessage("zoneList", this, playerId), zoneDef.Value.Id, zoneDef.Value.ZoneName, zoneDef.Value.Location);
                    }

                    return;
                case "remove":

                    var id = args[1];
                    if (ZoneDefinitions.ContainsKey(id))
                    {
                        storedData.ZoneDefinitions.Remove(ZoneDefinitions[id]);
                        SendReply(player, lang.GetMessage("zoneRemove", this, playerId), id);
                        SaveData();
                        LoadData();
                        LoadZones();
                    }
                    else
                        SendReply(player, lang.GetMessage("zoneError", this, playerId));
                    return;
                case "info":
                    int count = 0;
                    int zcount = 0;
                    foreach (var zoneDef in ZoneDefinitions)
                    {
                        count++;                        
                        if (IsInZone(player, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) == true)
                        {
                            zcount++;                            
                            SendReply(player, lang.GetMessage("zoneInfo", this, playerId), zoneDef.Value.Id, zoneDef.Value.ZoneName);
                            SendReply(player, lang.GetMessage("zoneFlag1", this, playerId), zoneDef.Value.ZoneRadius);
                            SendReply(player, lang.GetMessage("zoneFlag2", this, playerId), zoneDef.Value.ZoneNoPVP);
                            SendReply(player, lang.GetMessage("zoneFlag3", this, playerId), zoneDef.Value.ZoneNoBuild);
                            SendReply(player, lang.GetMessage("zoneFlag26", this, playerId), zoneDef.Value.ZoneNoPreFabPlace);
                            SendReply(player, lang.GetMessage("zoneFlag4", this, playerId), zoneDef.Value.ZoneNoDamage);
                            SendReply(player, lang.GetMessage("zoneFlag25", this, playerId), zoneDef.Value.ZoneNoDecay);
                            SendReply(player, lang.GetMessage("zoneFlag5", this, playerId), zoneDef.Value.ZoneNoSleeperDamage);
                            SendReply(player, lang.GetMessage("zoneFlag6", this, playerId), zoneDef.Value.ZoneNoCrestDamage);
                            SendReply(player, lang.GetMessage("zoneFlag7", this, playerId), zoneDef.Value.ZoneMessageOn);
                            SendReply(player, lang.GetMessage("zoneFlag8", this, playerId), zoneDef.Value.ZoneEnterMessageOn);
                            SendReply(player, lang.GetMessage("zoneFlag9", this, playerId), zoneDef.Value.ZoneExitMessageOn);
                            SendReply(player, lang.GetMessage("zoneFlag10", this, playerId), zoneDef.Value.ZoneMessage);
                            SendReply(player, lang.GetMessage("zoneFlag11", this, playerId), zoneDef.Value.EnterZoneMessage);
                            SendReply(player, lang.GetMessage("zoneFlag12", this, playerId), zoneDef.Value.ExitZoneMessage);
                            SendReply(player, lang.GetMessage("zoneFlag13", this, playerId), zoneDef.Value.ZoneNoPlayerRoping);
                            SendReply(player, lang.GetMessage("zoneFlag14", this, playerId), zoneDef.Value.ZoneNoPVE);
                            SendReply(player, lang.GetMessage("zoneFlag15", this, playerId), zoneDef.Value.ZoneNoPreFabDamage);
                            SendReply(player, lang.GetMessage("zoneFlag16", this, playerId), zoneDef.Value.ZoneEjectPlayer);
                            SendReply(player, lang.GetMessage("zoneFlag17", this, playerId), zoneDef.Value.ZoneEjectSleeper);
                            if (AntiLootLoaded)
                            {
                                SendReply(player, lang.GetMessage("zoneFlag18", this, playerId), zoneDef.Value.ZoneNoChestLooting);
                                SendReply(player, lang.GetMessage("zoneFlag19", this, playerId), zoneDef.Value.ZoneNoStationLooting);
                                SendReply(player, lang.GetMessage("zoneFlag20", this, playerId), zoneDef.Value.ZoneNoCampfireLooting);
                                SendReply(player, lang.GetMessage("zoneFlag21", this, playerId), zoneDef.Value.ZoneNoTorchLooting);
                                SendReply(player, lang.GetMessage("zoneFlag22", this, playerId), zoneDef.Value.ZoneNoFireplaceLooting);
                                SendReply(player, lang.GetMessage("zoneFlag23", this, playerId), zoneDef.Value.ZoneNoFurnitureLooting);
                                SendReply(player, lang.GetMessage("zoneFlag24", this, playerId), zoneDef.Value.ZoneNoVillagerLooting);
                            }          
                            return;
                        }                        
                    }
                    if (zcount == 0 && count >= 1)
                        SendReply(player, lang.GetMessage("zoneLocError", this, playerId));
                    if (count == 0)
                        SendReply(player, lang.GetMessage("noZoneError", this, playerId));

                    return;
                case "edit":
                    
                    
                    var currentzone = GetZoneId(player);
                     EditZone(currentzone.ToString(), args, player);
                       
                    

                    return;
                default:
                    break;
            }
            SendReply(player, lang.GetMessage("synError", this, playerId));
        }

        #endregion
        private void OnEntityHealthChange(EntityDamageEvent damageEvent)
        {           
            if (damageEvent == null) return;
            if (damageEvent.Damage == null) return;            
            if (damageEvent.Damage.DamageSource == null) return;
            if (damageEvent.Entity == null) return;
            if (damageEvent.Damage.DamageSource.name == damageEvent.Entity.name && damageEvent.Damage.DamageSource.IsPlayer) return;            
            var npc = IsNPC(damageEvent);
            //sleeper 
            
            var sleeper = damageEvent.Entity.GetComponentInChildren<PlayerSleeperObject>();           
            Player attacker = damageEvent.Damage.DamageSource.Owner;
            string victim;
            if (npc)
            {
                victim = damageEvent.Entity.name.ToString();
                string input = victim;
                string regex = "(\\[.*\\])|(\".*\")|('.*')|(\\(.*\\))";
                string output = Regex.Replace(input, regex, "");
                victim = output;
            }
            else
                victim = damageEvent.Entity.Owner.DisplayName;            

            if (damageEvent.Damage.Amount < 0) return;
            if (attacker.Name != "Server" && attacker.HasPermission("admin") && AdminCanKill) return;           
            
            if (attacker != null || damageEvent.Entity.name.Contains("Crest") || sleeper == true || npc == true || damageEvent.Damage.DamageTypes == CodeHatch.Damaging.DamageType.Suicide)
            {
                foreach (var zoneDef in ZoneDefinitions)
                {                    
                    if (IsEntityInZone(damageEvent.Entity.Position, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) || IsInZone(attacker, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius))
                    {                        
                        if (attacker != null &&zoneDef.Value.ZoneNoPVP == true && sleeper == false && !damageEvent.Entity.name.Contains("Crest") && npc == false && damageEvent.Entity.IsPlayer && damageEvent.Damage.DamageSource.IsPlayer)
                        {
                            damageEvent.Cancel(lang.GetMessage("logPvP", this, attacker.ToString()), attacker);
                            Puts(lang.GetMessage("logPvP", this, attacker.ToString()), attacker);
                            damageEvent.Damage.Amount = 0f;
                            SendReply(attacker, lang.GetMessage("noPvP", this, attacker.ToString()));
                        }
                        if (attacker != null && zoneDef.Value.ZoneNoSleeperDamage == true && sleeper == true)
                        {
                            damageEvent.Cancel(lang.GetMessage("logSleeper", this, attacker.ToString()), attacker);
                            Puts(lang.GetMessage("logSleeper", this, attacker.ToString()), attacker);
                            damageEvent.Damage.Amount = 0f;
                            SendReply(attacker, lang.GetMessage("noSleeper", this, attacker.ToString()));

                        }
                        if (attacker != null && zoneDef.Value.ZoneNoPVE == true && npc == true || !damageEvent.Damage.DamageSource.IsPlayer && zoneDef.Value.ZoneNoPVE == true)
                        {
                            if (npc)
                            {
                                damageEvent.Cancel(lang.GetMessage("logNoPVE", this, attacker.ToString()), attacker, victim);
                                Puts(lang.GetMessage("logNoPVE", this, attacker.ToString()), attacker, victim);
                                damageEvent.Damage.Amount = 0f;
                                SendReply(attacker, lang.GetMessage("noPVE", this, attacker.ToString()), victim);
                            }
                            if (damageEvent.Entity.IsPlayer)
                            {
                                damageEvent.Cancel("NoPVE zone, damage cancelled");
                                //Puts(lang.GetMessage("logNoPVE", this, attacker.ToString()), attacker, victim);
                                damageEvent.Damage.Amount = 0f;
                            }

                        }
                        if (attacker != null && damageEvent.Entity.name.Contains("Crest") && zoneDef.Value.ZoneNoCrestDamage == true)
                        {                            
                            damageEvent.Cancel(lang.GetMessage("logCrest", this, attacker.ToString()), attacker);
                            Puts(lang.GetMessage("logCrest", this, attacker.ToString()), attacker);
                            damageEvent.Damage.Amount = 0f;
                            SendReply(attacker, lang.GetMessage("noCrest", this, attacker.ToString()));
                        }
                        if (attacker != null && zoneDef.Value.ZoneNoPreFabDamage == true && !damageEvent.Entity.IsPlayer && damageEvent.Entity.GetBlueprint() && !damageEvent.Entity.name.Contains("Crest") && sleeper == false && npc == false && damageEvent.Damage.DamageTypes != CodeHatch.Damaging.DamageType.Suicide)
                        {                            
                            victim = damageEvent.Entity.name;
                            string input = victim;
                            string regex = "(\\[.*\\])|(\".*\")|('.*')|(\\(.*\\))";
                            string output = Regex.Replace(input, regex, "");
                            victim = output;
                            damageEvent.Cancel(lang.GetMessage("logNoPreFab", this, attacker.ToString()), attacker, victim);
                            Puts(lang.GetMessage("logNoPreFab", this, attacker.ToString()), attacker, victim);
                            damageEvent.Damage.Amount = 0f;
                            SendReply(attacker, lang.GetMessage("noPreFab", this, attacker.ToString()), victim);

                        }                        
                        if (attacker.Name == "Server" && zoneDef.Value.ZoneNoDecay == true && !damageEvent.Entity.IsPlayer && damageEvent.Damage.DamageTypes == CodeHatch.Damaging.DamageType.Suicide && !damageEvent.Entity.name.Contains("Crest") && sleeper == false && npc == false)
                        {                            
                            damageEvent.Cancel();                            
                            damageEvent.Damage.Amount = 0f;
                        }
                    }
                }
            }
        }
        private void OnPlayerCapture(PlayerCaptureEvent Event)
        {            
            if (Event == null) return;
            if (Event.Captor == null) return;
            if (Event.TargetEntity == null) return;
            if (Event.Captor == Event.TargetEntity) return;
            if (!Event.Captor.IsPlayer) return;
            
            Player player = Event.Captor.Owner;
            string playerId = player.Id.ToString();
                        
            if (player.HasPermission("admin") && AdminCanKill) return;
            foreach (var zoneDef in ZoneDefinitions)
            {                
                if (IsInZone(Event.Entity.Owner, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) == true)
                {                    
                    if (zoneDef.Value.ZoneNoPlayerRoping == true)
                    {                        
                        if (CrestCheckOn)
                        {
                            if (!OwnsCrestArea(player))//allows crest owners to rope
                            {
                                Event.Cancel(lang.GetMessage("logNoRope", this, playerId), player);
                                Puts(lang.GetMessage("logNoRope", this, playerId), player);
                                SendReply(player, lang.GetMessage("noRope", this, playerId));
                            }
                            else
                                return;
                        }
                        else
                        {                            
                            Event.Cancel(lang.GetMessage("logNoRope", this, playerId), player);
                            Puts(lang.GetMessage("logNoRope", this, playerId), player);
                            SendReply(player, lang.GetMessage("noRope", this, playerId));
                        }
                    }
                }
            }
        }
        private void OnCubePlacement(CubePlaceEvent Event)
        {
            if (Event == null) return;
            if (Event.Entity == null) return;
            Player player = Event.Entity.Owner;
            string playerId = player.Id.ToString();
            if (player.HasPermission("admin") && AdminCanBuild) return;
            foreach (var zoneDef in ZoneDefinitions)
            {
                if (IsInZone(Event.Entity.Owner, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) == true)
                {

                    if (zoneDef.Value.ZoneNoBuild == true)
                    {
                        if (Event.Material != CubeInfo.Air.MaterialID)
                        {
                            if (CrestCheckOn)
                            {
                                if (!OwnsCrestArea(player))//allows crest owners to rope//allows crest owners to remove/add blocks
                                {
                                    if (!TakeBlocks)
                                        InventoryUtil.CollectTileset(Event.Sender, Event.Material, 1, Event.PrefabId);
                                    Event.Cancel(lang.GetMessage("logNoBuild", this, playerId), player);
                                    Puts(lang.GetMessage("logNoBuild", this, playerId), player);
                                    SendReply(player, lang.GetMessage("noBuild", this, playerId));
                                }
                                else
                                    return;
                            }
                            else
                            {
                                if (!TakeBlocks)
                                    InventoryUtil.CollectTileset(Event.Sender, Event.Material, 1, Event.PrefabId);
                                Event.Cancel(lang.GetMessage("logNoBuild", this, playerId), player);
                                Puts(lang.GetMessage("logNoBuild", this, playerId), player);
                                SendReply(player, lang.GetMessage("noBuild", this, playerId));
                            }

                        }
                    }
                }
            }
        }
        private void OnCubeTakeDamage(CubeDamageEvent Event)
        {           
            if (Event == null) return;            
            if (Event.Damage == null) return;
            if (Event.Damage.Amount <= 0f) return;
            if (Event.Damage.DamageSource == null) return;
            if (Event.Damage.Damager == null) return;            
            if (!Event.Damage.DamageSource.IsPlayer) return;
            Player player = Event.Damage.DamageSource.Owner;
            var damageSource = Event.Damage.Damager.name;            
            if (player == null) return;
            string playerId = player.Id.ToString();
            TilesetColliderCube centralPrefabAtLocal = BlockManager.DefaultCubeGrid.GetCentralPrefabAtLocal(Event.Position);
            Vector3 pos;
            Vector3 Vect = new Vector3(0f, 0f, 0f);
            pos = Event.Damage.DamageSource.Position;
            if (Event.Damage.point != Vect)
                pos = Event.Damage.point; //Use for treb and ballista
            if (damageSource.Contains("Bolt") || damageSource.Contains("Arrow"))
                pos = Event.Damage.Damager.TryGetEntity().Position; ;
            
            foreach (var zoneDef in ZoneDefinitions)
            {
                if (IsEntityInZone(pos, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) == true)
                {
                    if (zoneDef.Value.ZoneNoDamage == true)
                    {
                        Event.Cancel(lang.GetMessage("logNoDamage", this, playerId), player);
                        SalvageModifier component = centralPrefabAtLocal.GetComponent<SalvageModifier>();
                        if (component != null && !component.info.NotSalvageable)
                        {                            
                            component.info.SalvageAmount = 0;
                        }

                        Event.Damage.Amount = 0f;
                        Event.Damage.ImpactDamage = 0f;
                        Event.Damage.MiscDamage = 0f;
                        Puts(lang.GetMessage("logNoDamage", this, playerId), player);
                        SendReply(player, lang.GetMessage("areaProtected", this, playerId));
                        return;
                    }
                }
            }
        }
        private void OnObjectDeploy(NetworkInstantiateEvent Event)
        {           
            Player player = Server.GetPlayerById(Event.SenderId);
            string playerId = player.Id.ToString();
            if (player == null) return;
            if (player.HasPermission("admin") && AdminCanBuild) return;
            foreach (var zoneDef in ZoneDefinitions)
            {
                if (IsInZone(Event.Sender, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) == true)
                {
                    InvItemBlueprint bp = InvDefinitions.Instance.Blueprints.GetBlueprintForID(Event.BlueprintId);
                    if (zoneDef.Value.ZoneNoBuild == true)
                    {
                        
                        if (bp.Name.Contains("Crest"))
                        {
                            timer.In(1, () => ObjectRemove(player, Event.Position, bp.name));
                            Puts(lang.GetMessage("logCrestPlace", this, playerId), player, bp.Name);
                            SendReply(Event.Sender, lang.GetMessage("noPlace", this, playerId), bp.Name);
                        }

                    }
                    if (zoneDef.Value.ZoneNoPreFabPlace == true && !bp.Name.Contains("Crest"))
                    {                       
                        timer.In(1, () => ObjectRemove(player, Event.Position, bp.name));
                        Puts(lang.GetMessage("logObjectPlace", this, playerId), player, bp.Name);
                        SendReply(Event.Sender, lang.GetMessage("noPlace", this, playerId), bp.Name);
                    }
                }
            }
        }
        private void OnPlayerDisconnected(Player player)
        {
            string playerId = player.Id.ToString();
                    
            if (PData.ContainsKey(player))
            {
                PlayerData Player = GetCache(player);
                PData.Remove(player);
            }
            if (timers.ContainsKey(playerId))
            {
                timers[playerId].Destroy();
                timers.Remove(playerId);
            }
            if (ZoneCheckTimer.ContainsKey(playerId))
            {
                ZoneCheckTimer[playerId].Destroy();
                ZoneCheckTimer.Remove(playerId);
            }
        }        
        private void OnPlayerConnected(Player player)
        {            
		    string playerId = player.Id.ToString();
            if (!PData.ContainsKey(player)) 
            {
                PData.Add(player, new PlayerData (player.Id));                
            }
            if (ZoneCheckOn == true && player.Name != "Server" && player.Id != 9999999999) //fixes error
            {
                if (!ZoneCheckTimer.ContainsKey(playerId))
                {
                    ZoneCheckTimer.Add(playerId, timer.Repeat(ZoneCheckInterval, 0, () => CheckPlayerLocation(player)));
                }
            }
        }
        
        private void OnPlayerSleep(PlayerSleepEvent Event)
        {
            Entity entity = Event.Player.Entity;                      
            foreach (var zoneDef in ZoneDefinitions)
            {                
                if (IsInZone(entity.Owner, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) == true)
                {                    
                    if (zoneDef.Value.ZoneEjectSleeper == true)
                    {                        
                        GameObject sleepGameObject = Event.SleepGameObject;                       
                        PlayerSleeperObject sleeperId = sleepGameObject.TryGetEntity().Get<PlayerSleeperObject>();
                        EjectSleeper(zoneDef.Value.ZoneRadius, zoneDef.Value.Location, sleeperId);
                        Puts(lang.GetMessage("logEjectSleeper", this, Event.SleeperId.ToString()), Event.DisplayName);
                    }
                }
            }
        }
        
        #region Functions
        private void ObjectRemove(Player player, Vector3 position, string itemname)
        {            
            foreach (var entity in Entity.TryGetAll())
            {
                if (entity.Position == position)
                {                    
                    EntityKiller.Kill(entity);
                    GiveInventoryStack(player, itemname);
                }
            }
        }
        
        void GiveInventoryStack(Player player, string itemname)
        {
            var inventory = player.CurrentCharacter.Entity.GetContainerOfType(CollectionTypes.Inventory);
            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName(itemname, true, true);           
            var invGameItemStack = new InvGameItemStack(blueprintForName, 1, null);
            ItemCollection.AutoMergeAdd(inventory.Contents, invGameItemStack);
        }
        void LoadZones()
        {           
            timer.In(1, () => {
                foreach (var zoneDef in ZoneDefinitions)
                    zones.Add(new Vector2(zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ));
            });

        }
        private void CheckPlayerLocation(Player player)
        {
            if (player.Name == "Server" && player.Id == 9999999999) return; //fixes error
            if (player == null) return;
            if (player.Entity == null) return; //fixed NRE  
            string playerId = player.Id.ToString();
            if (PData.ContainsKey(player))
            {
                foreach (var zoneDef in ZoneDefinitions)
                {
                    PlayerData Player = GetCache(player);
                    if (IsInZone(player, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) == true && zoneDef.Value.ZoneEjectPlayer == true && !player.HasPermission("admin"))
                    {
                        EjectPlayer(zoneDef.Value.ZoneRadius, zoneDef.Value.Location, player);
                        SendReply(player, lang.GetMessage("noEntry", this, playerId));
                        Puts(lang.GetMessage("logEjectPlayer", this, playerId), player);
                    }
                    if (IsInZone(player, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) == true)
                    {
                        Player.ZoneId = zoneDef.Value.Id;

                        if (Player.EnterZone == false)
                        {
                            if (zoneDef.Value.ZoneEnterMessageOn == true && Player.EnterZone == false)
                            {
                                SendMessage(player, zoneDef.Value.EnterZoneMessage, false, true);
                            }
                            if (zoneDef.Value.ZoneMessageOn == true)
                            {
                                SendMessage(player, zoneDef.Value.ZoneMessage, true, true);
                            }
                            Player.EnterZone = true;
                            Player.ExitZone = false;
                            return;
                        }

                    }
                    else
                    {
                        if (Player.EnterZone == true && Player.ZoneId == zoneDef.Value.Id && Player.ExitZone == false)
                        {
                            Player.EnterZone = false;
                            Player.ExitZone = true;

                            if (zoneDef.Value.ZoneExitMessageOn == true)
                            {
                                SendMessage(player, zoneDef.Value.ExitZoneMessage, false, false);
                            }

                        }

                        if (Player.EnterZone == false && Player.ExitZone == true)
                        {
                            Player.ExitZone = false;
                        }
                    }
                }
            }
        }

        private bool IsInZone(Player player, float zoneX, float zoneZ, float radius)
        {
            if (PData.ContainsKey(player))
            {

                PlayerData Player = GetCache(player);

                if (Server.PlayerIsOnline(player.Id))
                {
                    foreach (Vector2 zone in zones)
                    {

                        Vector2 vector = new Vector2(zoneX, zoneZ);
                        float distance = Math.Abs(Vector2.Distance(vector, new Vector2(player.Entity.Position.x, player.Entity.Position.z)));
                        if (distance <= radius)
                        {
                            return true;
                        }
                        else
                            return false;
                    }
                    return false;
                }
                return false;
            }
            return false;
        }
        private bool IsEntityInZone(Vector3 location, float zoneX, float zoneZ, float radius)
        {
            foreach (Vector2 zone in zones)
            {

                Vector2 vector = new Vector2(zoneX, zoneZ);
                float distance = Math.Abs(Vector2.Distance(vector, new Vector2(location.x, location.z)));
                if (distance <= radius)
                {
                    return true;
                }
                else
                    return false;
            }

            return false;

        }
        private void SendMessage(Player player, string message , bool repeat, bool inZone)
        {
			string playerId = player.Id.ToString();
            if (MessagesOn == true)
            {
                if (repeat == true && !timers.ContainsKey(playerId))
                {                              
                        timers.Add(playerId, timer.Repeat(MessageInterval, 0, () => SendReply(player, message)));
                }
                else
                {
                    if (repeat == false)
                    {
                        if (inZone == false && timers.ContainsKey(playerId)) 
                        {
                            timers[playerId].Destroy();
                            timers.Remove(playerId);
                        }
                        SendReply(player, message);
                    }
                }
            }
        }
        private bool OwnsCrestArea(Player player)
        {
            CrestScheme crestScheme = SocialAPI.Get<CrestScheme>();
            Crest crest = crestScheme.GetCrestAt(player.Entity.Position);

            if (crest == null) return false;
            if (crest.GuildName == player.GetGuild().Name) return true;
            return false;
        }
        private bool IsNPC(EntityDamageEvent damageEvent)
        {
            string npcName = damageEvent.Entity.name;
            if (npcName.Contains("Plague Villager") || npcName.Contains("Grizzly Bear") || npcName.Contains("Wolf") || npcName.Contains("Werewolf") || npcName.Contains("Baby Chicken") || npcName.Contains("Bat") || npcName.Contains("Chicken") || npcName.Contains("Crab") || npcName.Contains("Crow") || npcName.Contains("Deer") || npcName.Contains("Duck") || npcName.Contains("Moose") || npcName.Contains("Pigeon") || npcName.Contains("Rabbit") || npcName.Contains("Rooster") || npcName.Contains("Seagull") || npcName.Contains("Sheep") || npcName.Contains("Stag"))
                return true;
            return false;
        }
        private void EjectPlayer(float radius, Vector3 location, Player player)
        {            
            Vector3 newPos = Vector3.zero;
            
            if (newPos == Vector3.zero)
            {
                float dist;                
                dist = radius;
                newPos = location + (player.Entity.transform.position - location).normalized * (dist + 5f);
                newPos.y = TerrainAPIBase.GetTerrainHeightAt(newPos);
            }            
            EventManager.CallEvent((BaseEvent)new TeleportEvent(player.Entity, newPos));
            
        }
         
        private void EjectSleeper(float radius, Vector3 location, PlayerSleeperObject sleeper)
        {
            Vector3 newPos = Vector3.zero;
            var sleeperPos = sleeper.Entity.Position;            
            if (newPos == Vector3.zero)
            {
                float dist;
                dist = radius;
                newPos = location + (sleeper.transform.position - location).normalized * (dist + 5f);
                newPos.y = TerrainAPIBase.GetTerrainHeightAt(newPos);
            }

            sleeper.Entity.Position = new Vector3(newPos.x, newPos.y, newPos.z);
            sleeperPos = sleeper.Entity.Position;                          
        }
        
        PlayerData GetCache(Player Player)
        {
            PlayerData CachedPlayer = null;
            return PData.TryGetValue(Player, out CachedPlayer) ? CachedPlayer : null;
        }
        void CacheAllOnlinePlayers()
        {           
            if (Server.AllPlayers.Count > 0)
            {
                foreach (Player player in Server.AllPlayers)
                {
                    if (player.Name.ToLower() == "server")
                    {
                        continue;
                    }
                   if (player.Id == 9999999999) continue; //fixes error

                    if (Server.PlayerIsOnline(player.Id))
                    {
                        string playerId = player.Id.ToString();                        
                        if (!PData.ContainsKey(player))
                        {
                            PData.Add(player, new PlayerData(player.Id));
                            if (ZoneCheckOn == true)
                            {
                                if (!ZoneCheckTimer.ContainsKey(playerId))
                                {
                                    ZoneCheckTimer.Add(playerId, timer.Repeat(ZoneCheckInterval, 0, () => CheckPlayerLocation(player)));
                                }
                            }

                        }
                    }
                }
            }
            
        }
        #endregion
        #region API
        private bool EditZone(string zoneId, string[] args, Player player = null)
        {
            ZoneInfo zonedef;
            ZoneDefinitions.TryGetValue(zoneId, out zonedef);            
            UpdateZoneInfo(zonedef, args, player);
            ZoneDefinitions[zoneId] = zonedef;
            storedData.ZoneDefinitions.Add(zonedef);
            SaveData();
            if (zonedef.Location == null) return false;
            return true;
        }
        private bool NewZone(string[] args, Player player = null)//NewZone needs name and location or "here" in args
        {            
            var location = Vector3.zero;
            if (player != null)
            {
                foreach (var zoneDef in ZoneDefinitions)
                {
                    if (zoneDef.Value.ZoneName == args[0])
                    {
                        PrintWarning(lang.GetMessage("cslnameExists", this), args[0]);
                        return false;
                    }
                    if (IsInZone(player, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) == true)
                    {
                        PrintWarning(lang.GetMessage("cslinZoneError", this));
                        return false;
                    }
                }
            }
            if (player != null && args[1].Equals("here", StringComparison.OrdinalIgnoreCase))
            {
                location = player.Entity.Position;
            }

            if (!args[1].Equals("here", StringComparison.OrdinalIgnoreCase))
            {
                var loc = args[1].Trim().Split(' ');
                if (loc.Length == 3)
                {
                    location = new Vector3(Convert.ToSingle(loc[0]), Convert.ToSingle(loc[1]), Convert.ToSingle(loc[2]));
                }
                else
                {
                    if (player != null) SendReply(player, lang.GetMessage("cslformatError", this, player.Id.ToString()));
                    else
                        PrintError(lang.GetMessage("cslformatError", this));
                    return false;
                }
            }
            var newzoneinfo = new ZoneInfo(location) { Id = UnityEngine.Random.Range(1, 99999999).ToString() };
            if (ZoneDefinitions.ContainsKey(newzoneinfo.Id)) storedData.ZoneDefinitions.Remove(ZoneDefinitions[newzoneinfo.Id]);
            ZoneDefinitions[newzoneinfo.Id] = newzoneinfo;
            storedData.ZoneDefinitions.Add(newzoneinfo);
            SaveData();
            string name = args[0];
            float zonex = location.x; 
            float zoney = location.y;
            float zonez = location.z;           
            ZoneDefinitions[newzoneinfo.Id].ZoneX = zonex;
            ZoneDefinitions[newzoneinfo.Id].ZoneZ = zonez;
            ZoneDefinitions[newzoneinfo.Id].ZoneName = name;
            if (player != null)
                ZoneDefinitions[newzoneinfo.Id].ZoneCreatorName = player.ToString();
            else
                ZoneDefinitions[newzoneinfo.Id].ZoneCreatorName = "Server";
            if (player != null)
                SendReply(player, lang.GetMessage("zoneAdded", this, player.Id.ToString()), newzoneinfo.Id, name);
            else
                Puts(lang.GetMessage("cslzoneAdded", this), newzoneinfo.Id, name);
            SaveData();
            LoadZones();
            return true;
        }
        private bool RemoveZone(string zoneId)
        {
            ZoneInfo zone;
            if (!ZoneDefinitions.TryGetValue(zoneId, out zone)) return false;
            storedData.ZoneDefinitions.Remove(zone);
            ZoneDefinitions.Remove(zoneId);
            SaveData();
            LoadZones();
            return true;
        }
        private void AddFlag(string zoneId, string flagString)
        {
            ZoneDefinitions[zoneId].Flags.Add(flagString);
            SaveData();
        }
        
        private void RemoveFlag(string zoneId, string flagString)
        {
            ZoneDefinitions[zoneId].Flags.Remove(flagString);
            SaveData();
        }
        private bool IsPlayerInZone(Player player)
        {
            foreach (var zoneDef in ZoneDefinitions)                
            if (IsInZone(player, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius))
            {
                    var zoneId = zoneDef.Value.Id;
                    return ZoneDefinitions.Any(ZoneDefinitions => ZoneDefinitions.Value.Id == zoneId);                
            }
            return false;
        }
        private bool IsPlayerInZone(Vector3 pos)
        {
            if (IsEntityInZone(pos)) return true;
            return false;               
        }
        private bool IsEntityInZone(Vector3 pos)
        {
            foreach (var zoneDef in ZoneDefinitions)
                if (IsEntityInZone(pos, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius))
                {
                    var zoneId = zoneDef.Value.Id;
                    return ZoneDefinitions.Any(ZoneDefinitions => ZoneDefinitions.Value.Id == zoneId);
                }
            return false;
        }
        private object GetZoneId(Player player)
        {
            foreach (var zoneDef in ZoneDefinitions)
                if (IsInZone(player, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius))
                {
                    var zoneId = zoneDef.Value.Id;                    
                    return ZoneDefinitions.ContainsKey(zoneId) ? ZoneDefinitions[zoneId].Id : null;                   
                }
            return null;
        }
        private object GetZoneId(Vector3 pos)
        {
            foreach (var zoneDef in ZoneDefinitions)
                if (IsEntityInZone(pos, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius))
                {
                    var zoneId = zoneDef.Value.Id;
                    return ZoneDefinitions.ContainsKey(zoneId) ? ZoneDefinitions[zoneId].Id : null;
                }
            return null;
        }
        private object GetZoneName(string zoneID) => GetZoneByID(zoneID)?.ZoneName;                
        private ZoneInfo GetZoneByID(string zoneId)
        {
            return ZoneDefinitions.ContainsKey(zoneId) ? ZoneDefinitions[zoneId] : null;
        }
        private bool HasFlag(string zoneId, string zoneFlag)
        {
            try
            {
                var zone = GetZoneByID(zoneId);                
                if (HasZoneFlag(zone, zoneFlag))
                    return true;
            }
            catch
            {
            }
            return false;
           
        }
        private bool HasZoneFlag(ZoneInfo zone, string flag)
        {
            foreach (string flagstring in zone.Flags)
            {
                if (flagstring == flag)
                    return true;
            }

            if (flag == "pve" && zone.ZoneNoPVP == true || flag == "nobuild" && zone.ZoneNoBuild == true || flag == "nodamage" && zone.ZoneNoDamage == true || flag == "nosleeperdamage" && zone.ZoneNoSleeperDamage == true || flag == "nocrestdamage" && zone.ZoneNoCrestDamage == true || flag == "noroping" && zone.ZoneNoPlayerRoping == true || flag == "nopve" && zone.ZoneNoPVE == true || flag == "noprefabdamage" && zone.ZoneNoPreFabDamage == true || flag == "ejectplayer" && zone.ZoneEjectPlayer == true || flag == "ejectsleeper" && zone.ZoneEjectSleeper == true || flag == "nochestlooting" && zone.ZoneNoChestLooting == true || flag == "nocampfirelooting" && zone.ZoneNoCampfireLooting == true || flag == "notorchlooting" && zone.ZoneNoTorchLooting == true || flag == "nofireplacelooting" && zone.ZoneNoFireplaceLooting == true || flag == "nofurniturelooting" && zone.ZoneNoFurnitureLooting == true || flag == "nostationlooting" && zone.ZoneNoStationLooting == true || flag == "novillagerlooting" && zone.ZoneNoVillagerLooting == true || flag == "messageon" && zone.ZoneMessageOn == true || flag == "entermessageon" && zone.ZoneEnterMessageOn == true || flag == "exitmessageon" && zone.ZoneExitMessageOn == true)
                return true;
            return false;
        }        
        private void UpdateZoneInfo(ZoneInfo zone, string[] args, Player player = null)
        {
            bool isFlag = false;
            for (var i = 0; i < args.Length; i = i + 1)
            {
                object editflag;
                switch (args[i].ToLower())
                {
                    case "name":
                        editflag = zone.ZoneName = args[i + 1];
                        isFlag = true;
                        break;
                    case "id":
                        editflag = zone.Id = args[i + 1];
                        isFlag = true;
                        break;
                    case "radius":
                        editflag = zone.ZoneRadius = Convert.ToSingle(args[i + 1]);
                        isFlag = true;
                        break;
                    case "location":
                        if (player != null && args[i + 1].Equals("here", StringComparison.OrdinalIgnoreCase))
                        {
                            editflag = zone.Location = player.Entity.Position;
                            isFlag = true;
                            break;
                        }
                        var loc = args[i + 1].Trim().Split(' ');
                        if (loc.Length == 3)
                        {
                            editflag = zone.Location = new Vector3(Convert.ToSingle(loc[0]), Convert.ToSingle(loc[1]), Convert.ToSingle(loc[2]));
                            isFlag = true;
                        }
                        else
                        {
                            if (player != null) SendReply(player, "Invalid location format, use: \"x y z\" or here");
                            continue;
                        }
                        break;
                    case "entermessage":
                        editflag = zone.EnterZoneMessage = args[i + 1];
                        isFlag = true;
                        break;
                    case "leavemessage":
                        editflag = zone.ExitZoneMessage = args[i + 1];
                        isFlag = true;
                        break;
                    case "zonemessage":
                        editflag = zone.ZoneMessage = args[i + 1];
                        isFlag = true;
                        break;
                    case "nopvp":
                        editflag = zone.ZoneNoPVP = Convert.ToBoolean(args[i + 1]);
                        isFlag = true;
                        break;
                    case "nobuild":
                        editflag = zone.ZoneNoBuild = Convert.ToBoolean(args[i + 1]);
                        isFlag = true;
                        break;
                    case "noprefabplace":
                        editflag = zone.ZoneNoPreFabPlace = Convert.ToBoolean(args[i + 1]);
                        isFlag = true;
                        break;
                    case "nodamage":
                        editflag = zone.ZoneNoDamage = Convert.ToBoolean(args[i + 1]);
                        isFlag = true;
                        break;
                    case "nodecay":
                        editflag = zone.ZoneNoDecay = Convert.ToBoolean(args[i + 1]);
                        isFlag = true;
                        break;
                    case "nosleeperdamage":
                        editflag = zone.ZoneNoSleeperDamage = Convert.ToBoolean(args[i + 1]);
                        isFlag = true;
                        break;
                    case "nocrestdamage":
                        editflag = zone.ZoneNoCrestDamage = Convert.ToBoolean(args[i + 1]);
                        isFlag = true;
                        break;
                    case "noroping":
                        editflag = zone.ZoneNoPlayerRoping = Convert.ToBoolean(args[i + 1]);
                        isFlag = true;
                        break;
                    case "nopve":
                        editflag = zone.ZoneNoPVE = Convert.ToBoolean(args[i + 1]);
                        isFlag = true;
                        break;
                    case "noprefabdamage":
                        editflag = zone.ZoneNoPreFabDamage = Convert.ToBoolean(args[i + 1]);
                        isFlag = true;
                        break;
                    case "ejectplayer":
                        editflag = zone.ZoneEjectPlayer = Convert.ToBoolean(args[i + 1]);
                        isFlag = true;
                        break;
                    case "ejectsleeper":
                        editflag = zone.ZoneEjectSleeper = Convert.ToBoolean(args[i + 1]);
                        isFlag = true;
                        break;
                    case "nochestlooting":
                        editflag = zone.ZoneNoChestLooting = Convert.ToBoolean(args[i + 1]);
                        isFlag = true;
                        break;
                    case "nocampfirelooting":
                        editflag = zone.ZoneNoCampfireLooting = Convert.ToBoolean(args[i + 1]);
                        isFlag = true;
                        break;
                    case "notorchlooting":
                        editflag = zone.ZoneNoTorchLooting = Convert.ToBoolean(args[i + 1]);
                        isFlag = true;
                        break;
                    case "nofireplacelooting":
                        editflag = zone.ZoneNoFireplaceLooting = Convert.ToBoolean(args[i + 1]);
                        isFlag = true;
                        break;
                    case "nofurniturelooting":
                        editflag = zone.ZoneNoFurnitureLooting = Convert.ToBoolean(args[i + 1]);
                        isFlag = true;
                        break;
                    case "nostationlooting":
                        editflag = zone.ZoneNoStationLooting = Convert.ToBoolean(args[i + 1]);
                        isFlag = true;
                        break;
                    case "novillagerlooting":
                        editflag = zone.ZoneNoVillagerLooting = Convert.ToBoolean(args[i + 1]);
                        isFlag = true;
                        break;
                    case "messageon":
                        editflag = zone.ZoneMessageOn = Convert.ToBoolean(args[i + 1]);
                        isFlag = true;
                        break;
                    case "entermessageon":
                        editflag = zone.ZoneEnterMessageOn = Convert.ToBoolean(args[i + 1]);
                        isFlag = true;
                        break;
                    case "exitmessageon":
                        editflag = zone.ZoneExitMessageOn = Convert.ToBoolean(args[i + 1]);
                        isFlag = true;
                        break;
                    default:
                        isFlag = false;
                        editflag = false;
                        break;
                }                
                SaveData();
                LoadZones();
                if (isFlag)
                    if (player != null) SendReply(player, lang.GetMessage("zoneEdited", this, player.Id.ToString()), args[i], zone.Id, editflag); 
                         
            }
        }
        #endregion         
        
        T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }
        #region Vector3 Json Converter         
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
        #endregion
    }
}