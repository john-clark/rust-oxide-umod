using System.Collections.Generic;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using CodeHatch.Engine.Networking;
using CodeHatch.Common;
using CodeHatch.Thrones.SocialSystem;
using CodeHatch.Engine.Modules.SocialSystem;
using CodeHatch.Engine.Core.Cache;
using CodeHatch.ItemContainer;
using CodeHatch.Thrones.Capture;
using CodeHatch.Thrones.SocialSystem.Objects;
using CodeHatch.Engine.Modules.SocialSystem.Objects;

namespace Oxide.Plugins
{
    [Info("LockPickManager", "Mordeus", "1.0.1")]
    public class LockPickManager : ReignOfKingsPlugin
    {
        [PluginReference]
        Plugin DeclarationOfWar, ProtectedZone;       
        //config
        public string ChatTitle;
        public bool AllowLockPicking;
        public bool AllowCageLockPicking;
        public bool AllowDoorLockPicking;
        public bool DeclarationOfWarEnable;
        public bool ProtectedZoneEnable;
        public bool NoLockPickingInZones;
        public bool ConsumeLockPicks;
        public bool MessageOn;
        public bool LoggingOn;
        
        #region Lang API
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["Warning"] = "{Title} [FF0000]You can't unlock this {0}.[FFFFFF]",                
                ["NoAccess"] = "{Title} [FF0000]You can't use this command.[FFFFFF]",
                ["Help"] = "{Title} [FF0000]Either stand in the zone or type the zone Id after the command.[FFFFFF]",
                ["AddedFlag"] = "{Title} [FF0000]Flag Added.[FFFFFF]",
                ["RemovedFlag"] = "{Title} [FF0000]Flag removed.[FFFFFF]",
                ["RemovedZone"] = "{Title} [FF0000]Zone removed.[FFFFFF]",
                ["HasFlag"] = "{Title} [FF0000]Zone Already has this Flag.[FFFFFF]",
                ["NoFlag"] = "{Title} [FF0000]Flag doesnt exist in this zone.[FFFFFF]",
                ["ZoneError"] = "{Title} [FF0000]Zone Error: command not completed.[FFFFFF]",
                ["NoPZ"] = "{Title} [FF0000]Enable ProtectedZone in the Config to use this command.[FFFFFF]",
                ["Log"] = "Player {0} attempted to lockpick a {1} at position {2}."
            }, this);
        }
        #endregion Lang API
        #region Config

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating new configurationfile...");
        }

        private new void LoadConfig()
        {
            ChatTitle = GetConfig<string>("Title", "[4F9BFF]Server:");
            AllowLockPicking = GetConfig<bool>("Allow Lockpicking(Global)", false);
            AllowCageLockPicking = GetConfig<bool>("Allow Cage Lockpicking", true);
            AllowDoorLockPicking = GetConfig<bool>("Allow Door Lockpicking", false);
            DeclarationOfWarEnable = GetConfig<bool>("DeclarationOfWar Enable", false);
            ProtectedZoneEnable = GetConfig<bool>("ProtectedZone Enable", false);
            NoLockPickingInZones = GetConfig<bool>("No LockPicking in any zone", false);
            ConsumeLockPicks = GetConfig<bool>("Consume Lockpicks on fail", false);
            MessageOn = GetConfig<bool>("Chat Messages On", true);
            LoggingOn = GetConfig<bool>("Log Messages On", true);            
            
            SaveConfig();
        }
        #endregion Config
        #region Oxide  
        private void Init()
        {
            permission.RegisterPermission("lockpickmanager.admin", this);
            LoadConfig();            
        }
        void OnServerInitialized()
        {
            if (DeclarationOfWarEnable)
            {
                try
                {
                    DeclarationOfWar.Call("isLoaded", null);                    
                }
                catch (Exception)
                {
                    PrintWarning($"DeclarationOfWar is missing. Unloading {Name} as it will not work without DeclarationOfWar, change DeclarationOfWarEnable in the config to false to use without.");
                    Interface.Oxide.UnloadPlugin(Name);
                    return;
                }
            }
            if (ProtectedZoneEnable)
            {
                try
                {
                    ProtectedZone.Call("isLoaded", null);                    
                }
                catch (Exception)
                {
                    PrintWarning($"ProtectedZone is missing. Unloading {Name} as it will not work without ProtectedZone, change ProtectedZoneEnable in the config to false to use without.");
                    Interface.Oxide.UnloadPlugin(Name);
                    return;
                }
            }
        }
        private void OnPlayerUnlock(ObjectUnlockEvent theEvent)
        {
            Player player = theEvent.Sender;
            var playerId = player.Id.ToString();
           
            if (AllowLockPicking) return;
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
            if (ProtectedZoneEnable)
            {
                var zoneId = TryGetZoneId(player);
                var zoneFlag = "lockpickingallowed";
                if (NoLockPickingInZones) { if (!IsInZone(player)) return; }
                if (IsInZone(player) && hasFlag(zoneId.ToString(), zoneFlag)) return;
            }
            var name = "";
            var position = new Vector3();
            var defenseLevel = 0;
            ISecurable securable = GUIDManager.TryGetObject<ISecurable>(theEvent.GUID);            
            Component component = securable as Component;            
            CageCaptureManager cageCaptureManager = GUIDManager.TryGetObject<CageCaptureManager>(theEvent.GUID);
            PlayerCaptureManager playerCaptureManager1 = player.Entity.TryGet<PlayerCaptureManager>();
            if (playerCaptureManager1 != null && playerCaptureManager1.Captive != null && playerCaptureManager1.Captive == player.Entity && !playerCaptureManager1.Captive.Has<CageCaptureManager>()) return;//allow prisoner to escape rope with lockpick

            if (cageCaptureManager)
            {
                if (AllowCageLockPicking) return;
                if (cageCaptureManager.Prisoner != null)
                    if (cageCaptureManager.PrisonerPlayerID == player.Id) return;//allow prisoner to escape with lockpick
                
                defenseLevel = securable.Defense;                               
            }
            else
            {
                if (AllowDoorLockPicking) return;
                defenseLevel = securable.Defense;                                 
            }

            if (component != null)
            {
                Entity entity = component.TryGetEntity(); //works for cages, entity is null on all doors 
                position = (entity == null ? component.gameObject.transform.position : entity.Position);
                if (entity != null)
                {
                    name = Clean(entity.name);
                }
                else
                    name = "Door";
            }            
            theEvent.Cancel(player + " tried to unlock a " + name + " at " + position);
            securable.Lock(true);
            //give lockpicks used back
            string itemname = "Lockpick";
            int amount = defenseLevel;
            if (!ConsumeLockPicks)
            {
                GiveInventoryStack(player, itemname, amount);
            }
            if (MessageOn)            
                SendReply(player, Message("Warning", playerId), name);

            if (LoggingOn)
                Puts(Message("Log"), player, name, player.Entity.Position);
        }

        #endregion Oxide
        #region Commands
        //command: /addlockpickingflag //be sure to stand in a zone
        [ChatCommand("addlockpickingflag")]
        private void SetZoneFlag(Player player, string cmd, string[] args)
        {
            var playerId = player.Id.ToString();
            if (!ProtectedZoneEnable) { SendReply(player, Message("NoPZ", playerId)); return; }
            if (!hasPermission(player)) { SendReply(player, Message("NoAccess", playerId)); return; }
            var zoneId = "0";
            var zoneFlag = "lockpickingallowed";            
                           
            if (IsInZone(player) && args.Length == 0)
            {
                zoneId = TryGetZoneId(player).ToString();
                if (hasFlag(zoneId.ToString(), zoneFlag)) { SendReply(player, Message("HasFlag", playerId)); return; }
                AddFlag(zoneId.ToString(), zoneFlag);
                SendReply(player, Message("AddedFlag", playerId));
            }
            else
                if (args.Length > 0)
            {                
                zoneId = args[0];
                if (hasFlag(zoneId.ToString(), zoneFlag)) { SendReply(player, Message("HasFlag", playerId)); return; }
                AddFlag(zoneId.ToString(), zoneFlag);
                SendReply(player, Message("AddedFlag", playerId));
            }
            else
                SendReply(player, Message("Help", playerId));
            return;

        }
        //command: /removelockpickingflag //be sure to stand in a zone with the flag you want to remove
        [ChatCommand("removelockpickingflag")]
        private void RemoveZoneFlag(Player player, string cmd, string[] args)
        {
            var playerId = player.Id.ToString();
            if (!ProtectedZoneEnable) { SendReply(player, Message("NoPZ", playerId)); return; }
            if (!hasPermission(player)) { SendReply(player, Message("NoAccess", playerId)); return; }
            var zoneId = "0";
            var zoneFlag = "lockpickingallowed";

            if (IsInZone(player) && args.Length == 0)
            {
                zoneId = TryGetZoneId(player).ToString();
                if (!hasFlag(zoneId.ToString(), zoneFlag)) { SendReply(player, Message("NoFlag", playerId)); return; }
                RemoveFlag(zoneId.ToString(), zoneFlag);
                SendReply(player, Message("RemovedFlag", playerId));
            }
            else
                if (args.Length > 0)
                {
                zoneId = args[0];
                if (!hasFlag(zoneId.ToString(), zoneFlag)) { SendReply(player, Message("NoFlag")); return; }
                RemoveFlag(zoneId.ToString(), zoneFlag);
                SendReply(player, Message("RemovedFlag", playerId));

            }
            else
            SendReply(player, Message("Help", playerId));
            return;

        }
        //command /addlockpickingzone<zonename> <optional:x y z> //either stand where you want the zone and type /addlockpickingzone <zonename>, or type the coordinates after you type the name(optional), you must at least type the name.
        [ChatCommand("addlockpickingzone")]
        private void SetZone(Player player, string cmd, string[] args)
        {
            var playerId = player.Id.ToString();
            if (!ProtectedZoneEnable) { SendReply(player, Message("NoPZ", playerId)); return; }
            if (!hasPermission(player)) { SendReply(player, Message("NoAccess", playerId)); return; }
            var zoneId = "0";
            var zoneFlag = "lockpickingallowed";
            if (args.Length == 1) { args = new string[] { args[0], "here" }; }
            if (AddNewZone(args, player))
            {
                zoneId = TryGetZoneId(player).ToString();
                AddFlag(zoneId.ToString(), zoneFlag);
                SendReply(player, Message("AddedFlag", playerId));
            }
            else
            SendReply(player, Message("ZoneError", playerId)); return;
        }
        //command: /removelockpickingzone //be sure to stand in the zone you want to remove
        [ChatCommand("removelockpickingzone")]
        private void RemoveLPZone(Player player, string cmd, string[] args)
        {
            var playerId = player.Id.ToString();
            if (!ProtectedZoneEnable) { SendReply(player, Message("NoPZ", playerId)); return; }
            if (!hasPermission(player)) { SendReply(player, Message("NoAccess", playerId)); return; }
            var zoneId = "0";
            if (IsInZone(player) && args.Length == 0)
            {
                zoneId = TryGetZoneId(player).ToString();
                if (RemoveZone(zoneId))
                    SendReply(player, Message("RemovedZone", playerId));
                else
                    SendReply(player, Message("ZoneError", playerId)); return;
            }
            else
                if (args.Length > 0)
            {
                zoneId = args[0];
                if (RemoveZone(zoneId))
                    SendReply(player, Message("RemovedZone", playerId));               
                else
                    SendReply(player, Message("ZoneError", playerId)); return;
            }
            
        }
        #endregion Commands 
        #region DeclarationOfWar 
        bool IsAtWar(ulong guildId1, ulong guildId2)
        {
            if ((bool)DeclarationOfWar.Call("IsAtWar", guildId1) && (bool)DeclarationOfWar.Call("IsAtWar", guildId2))
                return true;
            else
                return false;
        }

        #endregion DeclarationOfWar
        #region ProtectedZone 
        bool AddNewZone(string[] args, Player player)
        {
            return (bool)ProtectedZone.Call("NewZone", args, player);
        }
        bool RemoveZone(string zoneId)
        {
            return (bool)ProtectedZone.Call("RemoveZone", zoneId);
        }
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
        void AddFlag(string zoneId, string flagString)
        {
            ProtectedZone.Call("AddFlag", zoneId, flagString);
        }
        void RemoveFlag(string zoneId, string flagString)
        {
            ProtectedZone.Call("RemoveFlag", zoneId, flagString);
        }
        #endregion
        #region Inventory Helper
        void GiveInventoryStack(Player player, string itemname, int qty)
        {
            var inventory = player.CurrentCharacter.Entity.GetContainerOfType(CollectionTypes.Inventory);
            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName(itemname, true, true);
            var invGameItemStack = new InvGameItemStack(blueprintForName, qty, null);
            ItemCollection.AutoMergeAdd(inventory.Contents, invGameItemStack);
        }
        #endregion Inventory Helper
        #region Helpers
        private string Message(string key, string id = null, params object[] args)
        {
            return lang.GetMessage(key, this, id).Replace("{Title}", ChatTitle);
        }

        private T GetConfig<T>(params object[] pathAndValue)
        {
            List<string> pathL = pathAndValue.Select((v) => v.ToString()).ToList();
            pathL.RemoveAt(pathAndValue.Length - 1);
            string[] path = pathL.ToArray();

            if (Config.Get(path) == null)
            {
                Config.Set(pathAndValue);
                PrintWarning($"Added field to config: {string.Join("/", path)}");
            }

            return (T)Convert.ChangeType(Config.Get(path), typeof(T));
        }
        private bool hasPermission(Player player)
        {
            if (!player.HasPermission("lockpickmanager.admin"))
                return false;
            return true;
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
        #endregion Helpers
    }
}