/* TODO: * 
 * Cannot get Loot Sacks to work right, they do not have an owner assigned, I have to find a way to catch 
 the Loot bag on death, grab the netviewId so I can associate it with the player in a list, and I have not 
 been successful, I believe it will take another hook. 
*/
using System.Collections.Generic;
using System.Text;
using System;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Text.RegularExpressions;
using CodeHatch.Engine.Networking;
using CodeHatch.Common;
using CodeHatch.Thrones.SocialSystem;
using CodeHatch.Engine.Modules.SocialSystem;
using CodeHatch.Engine.Core.Interaction.Behaviours.Networking;
using CodeHatch.Engine.Modules.SocialSystem.Objects;

namespace Oxide.Plugins
{
    [Info("AntiLoot", "Mordeus", "1.1.0")]
    public class AntiLoot : ReignOfKingsPlugin
    {
        [PluginReference]
        Plugin DeclarationOfWar, ProtectedZone;
        //config
        private bool MessagesOn => GetConfig("MessagesOn", true);
        //private bool AllowPlayerLooting => GetConfig("AllowPlayerLooting", true);
        private bool AllowVillagerLooting => GetConfig("AllowVillagerLooting", true);
        private bool AllowChestLooting => GetConfig("AllowChestLooting", false);
        private bool AllowWoodChestLooting => GetConfig("AllowWoodChestLooting", false);
        private bool AllowStationLooting => GetConfig("AllowStationLooting", false);
        private bool AllowTorchLooting => GetConfig("AllowTorchLooting", false);
        private bool AllowCampFireLooting => GetConfig("AllowCampFireLooting", false);
        private bool AllowFirePlaceLooting => GetConfig("AllowFirePlaceLooting", false);
        private bool AllowFurnitureLooting => GetConfig("AllowFurnitureLooting", false);
        private bool AllowGuildAccess => GetConfig("AllowGuildAccess", true);
        private bool AdminCanLoot => GetConfig("AdminCanLoot", true);
        private bool LogLooting => GetConfig("LogLooting", true);
        private bool ProtectedZoneEnable => GetConfig("ProtectedZoneEnable", false);
        private bool DeclarationOfWarEnable => GetConfig("DeclarationOfWarEnable", false);

        #region Config
        protected override void LoadDefaultConfig()
        {
            Config["MessagesOn"] = MessagesOn;
            //Config["AllowPlayerLooting"] = AllowPlayerLooting;
            Config["AllowVillagerLooting"] = AllowVillagerLooting;
            Config["AllowChestLooting"] = AllowChestLooting;
            Config["AllowWoodChestLooting"] = AllowWoodChestLooting;
            Config["AllowStationLooting"] = AllowStationLooting;
            Config["AllowTorchLooting"] = AllowTorchLooting;
            Config["AllowCampFireLooting"] = AllowCampFireLooting;
            Config["AllowFirePlaceLooting"] = AllowFirePlaceLooting;
            Config["AllowFurnitureLooting"] = AllowFurnitureLooting;
            Config["AllowGuildAccess"] = AllowGuildAccess;
            Config["AdminCanLoot"] = AdminCanLoot;
            Config["LogLooting"] = LogLooting;
            Config["ProtectedZoneEnable"] = ProtectedZoneEnable;
            Config["DeclarationOfWarEnable"] = DeclarationOfWarEnable;
            SaveConfig();
        }

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {                
                { "noChestLoot", "You cannot open this {0} it is not yours!" },
                { "noStationLoot", "You cannot use this {0} it is not yours!" },
                { "noTorchLoot", "You cannot open this {0} it is not yours! " },
                { "noCampFireLoot", "You cannot open this {0} it is not yours!" },
                { "noFurnitureLoot", "You cannot open this {0} it is not yours!" },
                { "noPlayerLoot", "You cannot loot this." },
                { "logNoChestLoot", "player {0} attempted to loot a {1} located at {2}."},
                { "logNoStationLoot", "player {0} attempted to use a {1} located at {2}."},
                { "logNoTorchLoot", "player {0} attempted to loot a {1} located at {2}."},
                { "logNoCampFireLoot", "player {0} attempted to loot a {1} located at {2}."},
                { "logNoFurnitureLoot", "player {0} attempted to loot a {1} located at {2}."},
                //{ "logNoPlayerLoot", "player {0} attempted to loot {1}."},                 

        }, this);
        }
        #endregion
        # region Oxide
        void OnServerInitialized()
        {
            if (ProtectedZoneEnable)
            {
                try
                {
                    ProtectedZone.Call("isLoaded", null);
                }
                catch (Exception)
                {
                    PrintWarning($"ProtectedZone is missing. Unloading {Name} as it will not work without ProtectedZone, change ProtectedZoneEnabled in the config to false to use without.");
                    Interface.Oxide.UnloadPlugin(Name);
                    return;
                }
            }
            if (DeclarationOfWarEnable)
            {
                try
                {
                    DeclarationOfWar.Call("isLoaded", null);
                }
                catch (Exception)
                {
                    PrintWarning($"DeclarationOfWar is missing. Unloading {Name} as it will not work without DeclarationOfWar, change DeclarationOfWarEnabled in the config to false to use without.");
                    Interface.Oxide.UnloadPlugin(Name);
                    return;
                }
            }
        }
        private void Init()
        {
            LoadDefaultConfig();                       
        }         
        private void OnPlayerInteract(InteractEvent Event)
        {            
            if (Event.Entity == null) return;
            if (Event.Interactable == null) return;
            if (Event.ControllerEntity == null) return;                        
            Player player = Event.Sender; //player thats interacting                         
            if (player.HasPermission("admin") && AdminCanLoot) return;
            if (SocialAPI.Get<SecurityScheme>().OwnsObject(player.Id, Event.Entity.TryGet<ISecurable>())) return;//checks for ownership    
            if (DeclarationOfWarEnable)
            {
                var playerguild = player.GetGuild();
                ulong playerguildId = playerguild.BaseID;
                CrestScheme crestScheme = SocialAPI.Get<CrestScheme>();
                Crest crest = crestScheme.GetCrestAt(player.Entity.Position);
                if (crest != null)
                {
                    var crestguid = crest.ObjectGUID;
                    Guild targetguild = SocialAPI.Get<GuildScheme>().TryGetGuildByObject(crestguid);
                    if (IsAtWar(playerguildId, targetguild.BaseID) && targetguild != null)
                    {
                        return;
                    }
                }
            }
            var container = Clean(Event.Entity.name);             
            var playerId = player.ToString();
            var position = Event.Entity.Position;
            if (OwnsCrestArea(player) && AllowGuildAccess) return;//allows crest owners/guilds to access loot, but no one else             
            if (Event.Entity.name.Contains("Chest") && !AllowChestLooting || ProtectedZoneEnable && Event.Entity.name.Contains("Chest"))
            {
                if (Event.Entity.name.Contains("Wood Chest") && AllowWoodChestLooting) return;
                if (ProtectedZoneEnable)
                {
                    var zoneId = TryGetZoneId(player);
                    var zoneFlag = "nochestlooting";
                    if (ProtectedZoneEnable && AllowChestLooting) { if (!IsInZone(player)) return; }
                    if (IsInZone(player) && !hasFlag(zoneId.ToString(), zoneFlag)) return;
                }
                if (MessagesOn)
                    SendReply(player, lang.GetMessage("noChestLoot", this, playerId), container);
                if (LogLooting)
                    Puts(lang.GetMessage("logNoChestLoot", this, playerId), player, container, position);
                Event.Cancel("Interact cancelled");
            }
            if (Event.Entity.name.Contains("Station") && !AllowStationLooting || ProtectedZoneEnable && Event.Entity.name.Contains("Station"))
            {
                if (ProtectedZoneEnable)
                {
                    var zoneId = TryGetZoneId(player);
                    var zoneFlag = "nostationlooting";
                    if (ProtectedZoneEnable && AllowStationLooting) { if (!IsInZone(player)) return; }
                    if (IsInZone(player) && !hasFlag(zoneId.ToString(), zoneFlag)) return;
                }
                if (MessagesOn)
                    SendReply(player, lang.GetMessage("noStationLoot", this, playerId), container);
                if (LogLooting)
                    Puts(lang.GetMessage("logNoStationLoot", this, playerId), player, container, position);
                Event.Cancel("Interact cancelled");
            }
            if (Event.Entity.name.Contains("Torch") && !AllowTorchLooting || Event.Entity.name.Contains("Lantern") && !AllowTorchLooting || Event.Entity.name.Contains("CandleStand") && !AllowTorchLooting || Event.Entity.name.Contains("Chandelier") && !AllowTorchLooting || ProtectedZoneEnable && Event.Entity.name.Contains("Torch") || ProtectedZoneEnable && Event.Entity.name.Contains("Lantern") || ProtectedZoneEnable && Event.Entity.name.Contains("CandleStand") || ProtectedZoneEnable && Event.Entity.name.Contains("Chandelier"))
            {
                if (ProtectedZoneEnable)
                {
                    var zoneId = TryGetZoneId(player);
                    var zoneFlag = "notorchlooting";
                    if (ProtectedZoneEnable && AllowTorchLooting) { if (!IsInZone(player)) return; }
                    if (IsInZone(player) && !hasFlag(zoneId.ToString(), zoneFlag)) return;
                }
                if (MessagesOn)
                    SendReply(player, lang.GetMessage("noTorchLoot", this, playerId), container);
                if (LogLooting)
                    Puts(lang.GetMessage("logNoTorchLoot", this, playerId), player, container, position);
                Event.Cancel("Interact cancelled");
            }
            if (Event.Entity.name.Contains("Campfire") && !AllowCampFireLooting || Event.Entity.name.Contains("Firepit") && !AllowCampFireLooting || ProtectedZoneEnable && Event.Entity.name.Contains("Campfire") || ProtectedZoneEnable && Event.Entity.name.Contains("Firepit"))
            {
                if (ProtectedZoneEnable)
                {
                    var zoneId = TryGetZoneId(player);
                    var zoneFlag = "nocampfirelooting";
                    if (ProtectedZoneEnable && AllowCampFireLooting) { if (!IsInZone(player)) return; }
                    if (IsInZone(player) && !hasFlag(zoneId.ToString(), zoneFlag)) return;
                }
                if (MessagesOn)
                    SendReply(player, lang.GetMessage("noCampFireLoot", this, playerId), container);
                if (LogLooting)
                    Puts(lang.GetMessage("logNoCampFireLoot", this, playerId), player, container, position);
                Event.Cancel("Interact cancelled");
            }
            if (Event.Entity.name.Contains("Fire Place") && !AllowFirePlaceLooting || ProtectedZoneEnable && Event.Entity.name.Contains("Fire Place"))
            {
                if (ProtectedZoneEnable)
                {
                    var zoneId = TryGetZoneId(player);
                    var zoneFlag = "nofireplacelooting";
                    if (ProtectedZoneEnable && AllowFirePlaceLooting) { if (!IsInZone(player)) return; }
                    if (IsInZone(player) && !hasFlag(zoneId.ToString(), zoneFlag)) return;
                }
                if (MessagesOn)
                    SendReply(player, lang.GetMessage("noCampFireLoot", this, playerId), container);
                if (LogLooting)
                    Puts(lang.GetMessage("logNoCampFireLoot", this, playerId), player, container, position);
                Event.Cancel("Interact cancelled");
            }
            if (Event.Entity.name.Contains("Cupboard") && !AllowFurnitureLooting || Event.Entity.name.Contains("Dresser") && !AllowFurnitureLooting || Event.Entity.name.Contains("Bookcase") && !AllowFurnitureLooting || ProtectedZoneEnable && Event.Entity.name.Contains("Cupboard") || ProtectedZoneEnable && Event.Entity.name.Contains("Dresser") || ProtectedZoneEnable && Event.Entity.name.Contains("Bookcase"))
            {
                if (ProtectedZoneEnable)
                {
                    var zoneId = TryGetZoneId(player);
                    var zoneFlag = "nofurniturelooting";
                    if (ProtectedZoneEnable && AllowFurnitureLooting) { if (!IsInZone(player)) return; }
                    if (IsInZone(player) && !hasFlag(zoneId.ToString(), zoneFlag)) return;
                }
                if (MessagesOn)
                    SendReply(player, lang.GetMessage("noFurnitureLoot", this, playerId), container);
                if (LogLooting)
                    Puts(lang.GetMessage("logNoFurnitureLoot", this, playerId), player, container, position);
                Event.Cancel("Interact cancelled");
            }
            /*
            //Cannot get Loot Sacks to work right, they do not have an owner assigned, I have to find a way to catch 
            //the Loot bag on death, grab the netviewId so I can associate it with the player in a list, and I have not 
            //been successful, I believe it will take another hook.
            if (Event.Entity.name.Contains("Loot Sack") && !AllowPlayerLooting)
            {                
                var location = Event.Entity.Position;
                var entityId = Event.Entity.NetViewID;                
                SendReply(player, "Owner is : " + owner + "location is: " + location + "ID is " + entityId);
                if (MessagesOn)
                    SendReply(player, lang.GetMessage("noPlayerLoot", this, playerId));
                if (LogLooting)
                    Puts(lang.GetMessage("logNoPlayerLoot", this, playerId), player, owner);
                Event.Cancel("Interact cancelled");
            }
            */
            if (Event.Entity.name.Contains("Villager") && !AllowVillagerLooting || ProtectedZoneEnable && Event.Entity.name.Contains("Villager"))
            {
                if (ProtectedZoneEnable)
                {
                    var zoneId = TryGetZoneId(player);
                    var zoneFlag = "novillagerlooting";
                    if (ProtectedZoneEnable && AllowVillagerLooting) { if (!IsInZone(player)) return; }
                    if (IsInZone(player) && !hasFlag(zoneId.ToString(), zoneFlag)) return;
                }
                if (MessagesOn)
                    SendReply(player, lang.GetMessage("noPlayerLoot", this, playerId), container);
                if (LogLooting)
                    Puts(lang.GetMessage("logNoPlayerLoot", this, playerId), player, container);
                Event.Cancel("Interact cancelled");
            }
        }
        #endregion
        #region ProtectedZone 
        bool IsInZone(Player player)
        {
            return (bool)ProtectedZone.Call("IsPlayerInZone", player);
        }
        bool hasFlag(string zoneId, string zoneFlag)
        {            
            return (bool)ProtectedZone.Call("HasFlag", zoneId, zoneFlag);
        }
        object TryGetZoneId(Player player)
        {           
            return ProtectedZone.Call("GetZoneId", player);
        }
        #endregion
        #region DeclarationOfWar 
        bool IsAtWar(ulong guildId1, ulong guildId2)
        {
            if ((bool)DeclarationOfWar.Call("IsAtWar", guildId1) && (bool)DeclarationOfWar.Call("IsAtWar", guildId2))
                return true;
            else
                return false;
        }

        #endregion DeclarationOfWar
        #region Functions
        private bool OwnsCrestArea(Player player)
        {
            CrestScheme crestScheme = SocialAPI.Get<CrestScheme>();
            Crest crest = crestScheme.GetCrestAt(player.Entity.Position);

            if (crest == null) return false;
            if (crest.GuildName == player.GetGuild().Name) return true;
            return false;
        }        
        #endregion
        #region Helpers
        T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }
        public static string Clean(string s)
        {
            StringBuilder sb = new StringBuilder(s);

            sb.Replace("[Entity] ", "");
            sb.Replace(" lv", "");            
            sb.Replace(" 051", "");
            sb.Replace(" (V1)", "");
            sb.Replace(" (V2)", "");            
            string sb2 = CleanNumbers(sb.ToString());
            return sb2.ToString();
        }
        private static Regex textOnly = new Regex(@"[\d-]");

        public static string CleanNumbers(string s)
        {
            return textOnly.Replace(s, "");
        }
        #endregion
    }
}