using System.Collections.Generic;
using Oxide.Core.Plugins;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Configuration;

namespace Oxide.Plugins
{
    [Info("PrivateZones", "k1lly0u", "0.1.4", ResourceId = 1703)]
    class PrivateZones : RustPlugin
    {
        #region Fields
        ZoneDataStorage data;
        private DynamicConfigFile ZoneData;

        [PluginReference] Plugin ZoneManager;		
		[PluginReference] Plugin PopupNotifications;
        #endregion

        #region Oxide Hooks
        void Loaded()
        {
            permission.RegisterPermission("privatezones.admin", this);
            lang.RegisterMessages(messages, this);
            ZoneData = Interface.Oxide.DataFileSystem.GetFile("privatezone_data");
        }
        void OnServerInitialized()
        {
            LoadData();
            foreach (var entry in data.zones)
                permission.RegisterPermission(entry.Value, this);
        }       
        void Unload() => SaveData();
        
        #endregion

        #region Functions       
        private void EjectPlayer(BasePlayer player, string zoneId)
        {
            float distance = 0;
            object success = ZoneManager?.Call("GetZoneLocation", zoneId);
            if (success is Vector3)
            {
                Vector3 position = (Vector3)success;
                success = ZoneManager?.Call("GetZoneSize", zoneId);
                if (success is Vector3 && (Vector3)success != Vector3.zero)
                {
                    Vector3 size = (Vector3)success;
                    distance = size.x > size.z ? size.x : size.z;
                }
                else success = ZoneManager?.Call("GetZoneRadius", zoneId);
                if (success is float && (float)success != 0)
                    distance = (float)success;

                Vector3 newPosition = position + (player.transform.position - position).normalized * (distance + 10f);
                newPosition = CalculateGroundPos(newPosition);

                player.MovePosition(newPosition);
                player.ClientRPCPlayer(null, player, "ForcePositionTo", player.transform.position);
                player.SendNetworkUpdateImmediate();
            }            
        }
        private Vector3 CalculateGroundPos(Vector3 sourcePos)
        {
            RaycastHit hitInfo;
            if (Physics.Raycast(sourcePos, Vector3.down, out hitInfo, LayerMask.GetMask("Terrain", "World", "Construction")))            
                sourcePos.y = hitInfo.point.y;            
            sourcePos.y = Mathf.Max(sourcePos.y, TerrainMeta.HeightMap.GetHeight(sourcePos));
            return sourcePos;
        }

        #endregion

        #region Zone Management
       
        void OnEnterZone(string zoneID, BasePlayer player)
        {            
            if (player == null || string.IsNullOrEmpty(zoneID)) return;
            if (player.IsSleeping()) return; 
            if (data.zones.ContainsKey(zoneID))
            {
                string perm = data.zones[zoneID];
                if (permission.UserHasPermission(player.userID.ToString(), perm) || IsAuth(player)) return;                
				if (PopupNotifications)
                    PopupNotifications?.Call("CreatePopupNotification", lang.GetMessage("noPerms", this, player.UserIDString), player);
                else SendMsg(player, lang.GetMessage("noPerms", this, player.UserIDString));
                EjectPlayer(player, zoneID);                
            }
        }
        #endregion

        #region Commands
        [ChatCommand("pz")]
        private void cmdPZ(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player)) return;
            if (args == null || args.Length == 0)
            {
                SendReply(player, lang.GetMessage("synAdd", this, player.UserIDString));
                SendReply(player, lang.GetMessage("synRem", this, player.UserIDString));
                SendReply(player, lang.GetMessage("synList", this, player.UserIDString));
                return;
            }
            switch (args[0].ToLower())
            {
                case "add":
                    if (args.Length == 3)
                    {
                        object zoneid = ZoneManager.Call("CheckZoneID", new object[] { args[1] });

                        if (zoneid is string && !string.IsNullOrEmpty((string)zoneid))
                        {
                            string perm = args[2].ToLower();
                            if (!perm.StartsWith("privatezones."))
                                perm = "privatezones." + perm;
                            Puts(perm);

                            data.zones.Add((string)zoneid, perm);

                            SendMsg(player, string.Format(lang.GetMessage("newZone", this, player.UserIDString), (string)zoneid, perm));
                            permission.RegisterPermission(perm, this);
                            SaveData();
                            return;
                        }
                        SendMsg(player, lang.GetMessage("invID", this, player.UserIDString));
                        return;
                    }
                    SendMsg(player, lang.GetMessage("synError", this, player.UserIDString));
                    return;
                case "remove":
                    if (args.Length == 2)
                    {
                        if (data.zones.ContainsKey(args[1].ToLower()))
                        {
                            data.zones.Remove(args[1].ToLower());
                            SendMsg(player, string.Format(lang.GetMessage("remZone", this, player.UserIDString), args[1]));
                            SaveData();
                            return;
                        }
                        SendMsg(player, lang.GetMessage("invID", this, player.UserIDString));
                        return;
                    }
                    SendMsg(player, lang.GetMessage("synError", this, player.UserIDString));
                    return;
                case "list":
                    foreach(var entry in data.zones)
                    {
                        SendReply(player, string.Format(lang.GetMessage("list", this, player.UserIDString), entry.Key, entry.Value));
                    }
                    return;
            }
        }
        bool IsAuth(BasePlayer player) => player?.net?.connection?.authLevel == 2;      
        bool HasPermission(BasePlayer player) => IsAuth(player) || permission.UserHasPermission(player.userID.ToString(), "privatezones.admin");       
        #endregion

        #region Data management      
        void SaveData() => ZoneData.WriteObject(data);        
        void LoadData()
        {
            try
            {
                data = Interface.GetMod().DataFileSystem.ReadObject<ZoneDataStorage>("privatezone_data");
            }
            catch
            {
                data = new ZoneDataStorage();
            }
        }
        class ZoneDataStorage
        {
            public Dictionary<string, string> zones = new Dictionary<string, string>();
            public ZoneDataStorage() { }
        }
        #endregion

        #region Localization
        private void SendMsg(BasePlayer player, string msg) => SendReply(player, lang.GetMessage("title", this, player.UserIDString) + lang.GetMessage("MsgColor", this, player.UserIDString) + msg + "</color>");        
        private Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"title", "<color=#afff00>PrivateZones:</color> " },
            {"list", "ZoneID: {0}, Permission: {1}" },
            {"synError", "Syntax Error" },
            {"invID", "Invalid ZoneID" },
            {"remZone", "Removed Zone: {0}" },
            {"newZone", "Created new private zone for ZoneID: {0}, using permission: {1}" },
            {"synAdd", "/pz add <zoneid> <permission>" },
            {"synRem", "/pz remove <zoneid>" },
            {"synList", "/pz list" },
            {"noPerms", "You don't have permission to enter this zone" },
            {"MsgColor", "<color=#d3d3d3>" }
        };
        #endregion

    }
}
