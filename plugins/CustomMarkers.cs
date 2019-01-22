using UnityEngine;
using Oxide.Core;
using System.Collections.Generic;
using System.Linq;
using System;


namespace Oxide.Plugins
{
    [Info("Custom Markers", "shinnova", "2.0.1")]
    [Description("Allows the placing of vending machine map markers with your own text")]
    public class CustomMarkers : RustPlugin
    {
        string markerPrefab = "assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab";
        string usePerm = "custommarkers.allowed";
        List<BaseEntity> placedMarkers = new List<BaseEntity>();

        #region Data
        class StoredData
        {
            public Dictionary<int, MarkerInfo> Markers = new Dictionary<int, MarkerInfo>();
            public StoredData(){}
        }
        StoredData storedData;

        class MarkerInfo
        {
            public Vector3 Location = new Vector3();
            public string Text = "";
            public MarkerInfo(){}
            public MarkerInfo(Vector3 playerpos, string marktext)
            {
                Location = playerpos;
                Text = marktext;
            }
        }

        MarkerInfo markerInfo;

        void SaveData() => Interface.Oxide.DataFileSystem.WriteObject("CustomMarkers", storedData);

        void OnNewSave(string filename)
        {
            Unload();
            storedData = new StoredData();
            SaveData();
        }
        #endregion

        new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AddSuccess"] = "Marker #{0} successfully added!",
                ["RemoveSuccess"] = "Successfully removed marker #{0}!",
                ["RemoveAllSuccess"] = "Successfully removed all markers!",
                ["NotExist"] = "There is no marker with ID {0}.",
                ["ConsoleListAvailable"] = "[CustomMarkers] Active markers:",
                ["ForRemoval"] = "Check your console for a list of markers, then run <color=#ffa500ff>/marker remove ID/all</color> to remove one or more",
                ["ActiveMarkers"] = "Check your console for a list of active markers",
                ["NoMarkers"] = "There are currently no active markers",
                ["NotAllowed"] = "You do not have permission to use that command.",
                ["Help"] = "<color=#ffa500ff>/marker add {name}</color> to add a new marker\n<color=#ffa500ff>/marker remove {id}/all</color> to remove a marker\n<color=#ffa500ff>/marker list</color> to list all markers",
            }, this);
        }

        void OnServerInitialized()
        {
            permission.RegisterPermission(usePerm, this);
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("CustomMarkers");
            foreach (MarkerInfo mInfo in storedData.Markers.Values)
            {
                BaseEntity ent = GameManager.server.CreateEntity(markerPrefab, mInfo.Location);
                VendingMachineMapMarker customMarker = ent.GetComponent<VendingMachineMapMarker>();
                customMarker.markerShopName = mInfo.Text;
                ent.Spawn();
                placedMarkers.Add(ent);
            }
        }

        void Unload()
        {
            foreach (BaseEntity marker in placedMarkers)
                marker.Kill();
        }

        int GetId()
        {
            int LastID = 0;
            if (storedData.Markers.Count() > 0)
                LastID = storedData.Markers.Keys.Last();
            int uid = LastID + 1;
            return uid;
        }

        void SendConsoleReply(Network.Connection cn, string msg)
        {
            if (Network.Net.sv.IsConnected())
            {
                Network.Net.sv.write.Start();
                Network.Net.sv.write.PacketID(Network.Message.Type.ConsoleMessage);
                Network.Net.sv.write.String(msg);
                Network.Net.sv.write.Send(new Network.SendInfo(cn));
            }
        }

        [ChatCommand("marker")]
        void chatCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, usePerm))
            {
                SendReply(player, lang.GetMessage("NotAllowed", this, player.UserIDString));
                return;
            }
            List<string> argsList = new List<string>(args);
            if (argsList.Count == 0)
            {
                SendReply(player, lang.GetMessage("Help", this, player.UserIDString));
                return;
            }
            switch (argsList[0].ToLower())
            {
                case "add":
                    {
                        string customtext = "";
                        if (args.Count() > 1)
                        {
                            argsList.RemoveAt(0);
                            customtext = String.Join(" ", argsList);
                        }
                        Vector3 playerpos = player.transform.position;
                        int uid = GetId();
                        BaseEntity ent = GameManager.server.CreateEntity(markerPrefab, playerpos);
                        VendingMachineMapMarker custommarker = ent.GetComponent<VendingMachineMapMarker>();
                        custommarker.markerShopName = customtext;
                        ent.Spawn();
                        placedMarkers.Add(ent);
                        markerInfo = new MarkerInfo(playerpos, customtext);
                        storedData.Markers.Add(uid, markerInfo);
                        SaveData();
                        SendReply(player, String.Format(lang.GetMessage("AddSuccess", this, player.UserIDString), uid));
                        return;
                    }
                case "remove":
                    {
                        if (args.Count() == 1)
                        {
                            if (storedData.Markers.Count() == 0)
                            {
                                SendReply(player, lang.GetMessage("NoMarkers", this, player.UserIDString));
                                return;
                            }
                            SendConsoleReply(player.net.connection, String.Format(lang.GetMessage("ConsoleListAvailable", this, player.UserIDString)));
                            foreach (var marker in storedData.Markers)
                               SendConsoleReply(player.net.connection, String.Format("{0}. {1}", marker.Key, marker.Value.Text));
                            SendReply(player, String.Format(lang.GetMessage("ForRemoval", this, player.UserIDString)));
                            return;
                        }
                        if (args[1] == "all")
                        {
                            foreach (BaseEntity marker in placedMarkers)
                                marker.Kill();
                            storedData = new StoredData();
                            SaveData();
                            SendReply(player, lang.GetMessage("RemoveAllSuccess", this, player.UserIDString));
                            return;
                        }
                        argsList.RemoveAt(0);
                        foreach (string sid in argsList)
                        {
                            int uid = Convert.ToInt32(sid);
                            if (!storedData.Markers.ContainsKey(uid))
                            {
                                SendReply(player, String.Format(lang.GetMessage("NotExist", this, player.UserIDString), uid));
                                continue;
                            }
                            storedData.Markers.TryGetValue(uid, out markerInfo);
                            Vector3 markerPos = markerInfo.Location;
                            foreach (BaseEntity marker in placedMarkers)
                            {
                                if (markerPos == marker.transform.position)
                                {
                                    marker.Kill();
                                    placedMarkers.Remove(marker);
                                    storedData.Markers.Remove(uid);
                                    SaveData();
                                    SendReply(player, String.Format(lang.GetMessage("RemoveSuccess", this, player.UserIDString), uid));
                                    break;
                                }
                            }
                        }
                        return;
                    }
                case "list":
                    {
                        if (storedData.Markers.Count() == 0)
                        {
                            SendReply(player, lang.GetMessage("NoMarkers", this, player.UserIDString));
                            return;
                        }
                        SendConsoleReply(player.net.connection, String.Format(lang.GetMessage("ConsoleListAvailable", this, player.UserIDString)));
                        foreach (var marker in storedData.Markers)
                            SendConsoleReply(player.net.connection, String.Format("{0}. {1}", marker.Key, marker.Value.Text));
                        SendReply(player, String.Format(lang.GetMessage("ActiveMarkers", this, player.UserIDString)));
                        return;
                    }
                default:
                    {
                        SendReply(player, lang.GetMessage("Help", this, player.UserIDString));
                        return;
                    }
            }
        }
    }
}