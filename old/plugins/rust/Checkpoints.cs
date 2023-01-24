using System;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Checkpoints", "Ryan", "1.0.1")]
    [Description("Restore points for players to teleport to when the server restarts")]
    class Checkpoints : RustPlugin
    {
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private static StoredData sData;

        private static Checkpoints Instance;

        private const string Perm = "checkpoints.allow";

        #region Data

        private class StoredData
        {
            public Dictionary<ulong, PlayerData> Players = new Dictionary<ulong, PlayerData>();
        }

        private class PlayerData
        {
            public float PosX { get; set; }
            public float PosY { get; set; }
            public float PosZ { get; set; }
            public uint ID { get; set; }

            public PlayerData()
            {
            }

            public PlayerData(SleepingBag bag)
            {
                PosX = bag.transform.position.x;
                PosY = bag.transform.position.y;
                PosZ = bag.transform.position.z;
                ID = bag.net.ID;
            }
        }

        private class Data
        {
            public static void Add(BasePlayer player, PlayerData data)
            {
                if (!Exists(player.userID))
                {
                    sData.Players.Add(player.userID, data);
                    Save();
                }
            }

            public static void Remove(ulong id)
            {
                if (Exists(id))
                {
                    sData.Players.Remove(id);
                    Save();
                }
            }

            public static bool Exists(ulong id)
            {
                return sData.Players.ContainsKey(id);
            }

            private static void Save() => Interface.Oxide.DataFileSystem.WriteObject(Instance.Name, sData);
        }

        #endregion

        #region Lang

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Checkpoint_NotExist"] = "You have no checkpoint set, to set a checkpoint use <color=#55aaff>/checkpoint set</color> at your desired position.",
                ["Checkpoint_Exists"] = "Your checkpoint is <color=#55aaff>{0}</color>m away",
                ["Checkpoint_AlreadyExists"] = "You already have a checkpoint set, it's <color=#55aaff>{0}</color>m away",
                ["Checkpoint_Set"] = "You've set your checkpoint at <color=#55aaff>X</color>: {0}, <color=#55aaff>Y</color>: {1}, <color=#55aaff>Z</color>: {2}",
                ["Checkpoint_NotFound"] = "You don't seem to have a checkpoint to remove",
                ["Checkpoint_Removed"] = "You've removed your checkpoint at <color=#55aaff>X</color>: {0}, <color=#55aaff>Y</color>: {1}, <color=#55aaff>Z</color>: {2} (<color=#55aaff>{3}</color>m away)",
                ["Checkpoint_Killed"] = "Your checkpoint <color=#55aaff>{0}</color>m away from you has been reset because your bag has been removed!",

                ["NoPermission"] = "You don't have permission to use that command",

                ["SleepingBag_NotOwner"] = "You do not own the sleeping bag you're looking at",

                ["Raycast_NotFound"] = "Didn't find a valid sleeping bag"
            }, this);
        }

        #endregion

        #region Methods

        private class Checks
        {
            public static bool HasPermission(string id)
            {
                return Instance.permission.UserHasPermission(id, Perm);
            }

            public static bool CanUseSleepingBag(BasePlayer player, SleepingBag bag)
            {
                if (player.userID == bag.deployerUserID)
                    return true;

                return false;
            }
        }

        private SleepingBag FindBag(BasePlayer player)
        {
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, Mathf.Infinity))
            {
                PrintToChat(player, Lang("Raycast_NotFound", player.UserIDString));
                return null;
            }
            if (hit.GetEntity() is SleepingBag)
                return hit.GetEntity() as SleepingBag;

            PrintToChat(player, Lang("Raycast_NotFound", player.UserIDString));
            return null;
        }

        #endregion

        #region Hooks

        private void Init()
        {
            Instance = this;
            sData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            permission.RegisterPermission(Perm, this);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (reason.ToLower().Contains("restarting") && Data.Exists(player.userID))
            {
                var data = sData.Players[player.userID];
                player.Teleport(new Vector3(data.PosX, data.PosY, data.PosZ));
            }
        }

        private void OnEntityKill(BaseNetworkable networkable)
        {
            var bag = networkable as SleepingBag;
            if (bag != null && Data.Exists(bag.OwnerID) && sData.Players[bag.OwnerID].ID == bag.net.ID)
            {
                Data.Remove(bag.OwnerID);
                var player = BasePlayer.FindByID(bag.OwnerID);
                if (player != null && player.IsConnected)
                {
                    var data = sData.Players[player.userID];
                    PrintToChat(player, Lang("Checkpoint_Killed", player.UserIDString,
                            Vector3.Distance(player.transform.position, new Vector3(data.PosX, data.PosY, data.PosZ))));
                }
            }
        }

        #endregion

        #region Commands

        [ChatCommand("checkpoint")]
        private void checkpointCmd(BasePlayer player, string command, string[] args)
        {
            if (!Checks.HasPermission(player.UserIDString))
            {
                PrintToChat(player, Lang("NoPermission", player.UserIDString));
                return;
            }
            if (args.Length == 0)
            {
                if (Data.Exists(player.userID))
                {
                    var data = sData.Players[player.userID];
                    PrintToChat(player, Lang("Checkpoint_Exists", player.UserIDString,
                        Math.Round(Vector3.Distance(player.transform.position, new Vector3(data.PosX, data.PosY, data.PosZ)))));
                    return;
                }
                PrintToChat(player, Lang("Checkpoint_NotExist", player.UserIDString));
                return;
            }
            switch (args[0].ToLower())
            {
                case "set":
                    if (!Data.Exists(player.userID))
                    {
                        var bag = FindBag(player);
                        if (bag == null)
                            return;
                        if (!Checks.CanUseSleepingBag(player, bag))
                        {
                            PrintToChat(player, Lang("SleepingBag_NotOwner", player.UserIDString));
                            return;
                        }
                        Data.Add(player, new PlayerData(bag));
                        PrintToChat(player, Lang("Checkpoint_Set", player.UserIDString, Math.Round(bag.transform.position.x, 2),
                            Math.Round(bag.transform.position.y, 2), Math.Round(bag.transform.position.z, 2)));
                    }
                    PrintToChat(player, Lang("Checkpoint_AlreadyExists", player.UserIDString, Math.Round(Vector3.Distance(player.transform.position,
                        new Vector3(sData.Players[player.userID].PosX, sData.Players[player.userID].PosY, sData.Players[player.userID].PosZ)))));
                    return;

                case "remove":
                    if (Data.Exists(player.userID))
                    {
                        var data = sData.Players[player.userID];
                        Data.Remove(player.userID);
                        PrintToChat(player, Lang("Checkpoint_Removed", player.UserIDString, Math.Round(data.PosX, 2), Math.Round(data.PosY, 2), Math.Round(data.PosZ, 2),
                            Math.Round(Vector3.Distance(player.transform.position, new Vector3(data.PosX, data.PosY, data.PosZ)))));
                        return;
                    }
                    PrintToChat(player, Lang("Checkpoint_NotFound", player.UserIDString));
                    return;
            }
        }

        #endregion
    }
}