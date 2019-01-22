using System.Collections.Generic;
using Oxide.Core.Configuration;
using UnityEngine;
using Oxide.Core;
using System;

namespace Oxide.Plugins
{
    [Info("AdminHammer", "mvrb", "1.4.1")]
    class AdminHammer : RustPlugin
    {
        private const string permAllow = "adminhammer.allow";
        private bool logToConsole = true;
        private float toolDistance = 200f;
        private string toolUsed = "hammer";
        private bool showSphere = false;

        private int layerMask = LayerMask.GetMask("Construction", "Deployed", "Default");

        private readonly DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetFile("AdminHammer");

        private List<ulong> Users = new List<ulong>();

        protected override void LoadDefaultConfig()
        {
            Config["LogToConsole"] = logToConsole = GetConfig("LogToFile", true);
            Config["ShowSphere"] = showSphere = GetConfig("ShowSphere", false);
            Config["ToolDistance"] = toolDistance = GetConfig("ToolDistance", 200f);
            Config["ToolUsed"] = toolUsed = GetConfig("ToolUsed", "hammer");

            SaveConfig();
        }

        private void Init()
        {
            Users = dataFile.ReadObject<List<ulong>>();

            LoadDefaultConfig();
            permission.RegisterPermission(permAllow, this);

            cmd.AddChatCommand("ah", this, "CmdAdminHammer");
            cmd.AddChatCommand("adminhammer", this, "CmdAdminHammer");
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoAuthorizedPlayers"] = "No authorized players.",
                ["AuthorizedPlayers"] = "Authorized players in the {0} owned by {1}:",
                ["NoEntityFound"] = "No entity found. Look at an entity and right-click while holding a {0}.",
                ["NoOwner"] = "No owner found for this entity.",
                ["ChatEntityOwnedBy"] = "This {0} is owned by {1}",
                ["DoorCode"] = "Door Code: <color=yellow>{0}</color>",
                ["ConsoleEntityOwnedBy"] = "This {0} is owned by www.steamcommunity.com/profiles/{1}",
                ["ToolActivated"] = "You have enabled AdminHammer.",
                ["ToolDeactivated"] = "You have disabled AdminHammer."
            }, this);
        }

        private void CmdAdminHammer(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, permAllow)) return;

            if (Users.Contains(player.userID))
            {
                Users.Remove(player.userID);
                player.ChatMessage(Lang("ToolDeactivated", player.UserIDString));
            }
            else
            {
                Users.Add(player.userID);
                player.ChatMessage(Lang("ToolActivated", player.UserIDString));
            }

            dataFile.WriteObject(Users);
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!Users.Contains(player.userID)) return;

            if (!input.WasJustPressed(BUTTON.FIRE_SECONDARY) || (player.GetActiveItem() as Item)?.info.shortname != toolUsed) return;

            RaycastHit hit;
            var raycast = Physics.Raycast(player.eyes.HeadRay(), out hit, toolDistance, layerMask);
            BaseEntity entity = raycast ? hit.GetEntity() : null;

            if (!entity)
            {
                player.ChatMessage(Lang("NoEntityFound", player.UserIDString, toolUsed));
                return;
            }
                        
            if (entity is Door)
            {
                var door = entity as Door;
                var lockSlot = door.GetSlot(BaseEntity.Slot.Lock);

                if (lockSlot is CodeLock)
                {
                    var codeLock = (CodeLock)lockSlot;
                    string msg = Lang("AuthorizedPlayers", player.UserIDString, door.ShortPrefabName, GetName(entity.OwnerID.ToString())) + "\n";

                    int authed = 0;

                    foreach (var user in codeLock.whitelistPlayers)
                    {
                        authed++;
                        msg += $"{authed}. {GetName(user.ToString())}\n";
                    }

                    player.ChatMessage(authed == 0 ? Lang("NoAuthorizedPlayers", player.UserIDString) : msg);
                }
                else if (lockSlot is BaseLock)
                {
                    player.ChatMessage(entity.OwnerID == 0 ? Lang("NoOwner", player.UserIDString, entity.ShortPrefabName) : Lang("ChatEntityOwnedBy", player.UserIDString, entity.ShortPrefabName, GetName(entity.OwnerID.ToString())));
                    Puts(entity.OwnerID == 0 ? Lang("NoOwner", player.UserIDString, entity.ShortPrefabName) : Lang("ConsoleEntityOwnedBy", player.UserIDString, entity.ShortPrefabName, entity.OwnerID.ToString()));
                }

            }
            else if (entity is SleepingBag)
            {
                SleepingBag sleepingBag = entity as SleepingBag;

                player.ChatMessage($"This SleepingBag has been assigned to {GetName(sleepingBag.deployerUserID.ToString())} by {GetName(sleepingBag.OwnerID.ToString())}");
            }
            else if (entity is BuildingPrivlidge || entity is AutoTurret)
            {
                player.ChatMessage(GetAuthorized(entity, player));
            }
            else if (entity is StorageContainer)
            {
                var storageContainer = entity as StorageContainer;
                string msg = $"Items in the {storageContainer.ShortPrefabName} owned by {GetName(storageContainer.OwnerID.ToString())}:\n";
                foreach (var item in storageContainer.inventory.itemList)
                    msg += $"{item.amount}x {item.info.displayName.english}\n";
                player.ChatMessage(msg);
            }
            else
            {
                player.ChatMessage(entity.OwnerID == 0 ? Lang("NoOwner", player.UserIDString, entity.ShortPrefabName) : Lang("ChatEntityOwnedBy", player.UserIDString, entity.ShortPrefabName, GetName(entity.OwnerID.ToString())));
                Puts(entity.OwnerID == 0 ? Lang("NoOwner", player.UserIDString, entity.ShortPrefabName) : Lang("ConsoleEntityOwnedBy", player.UserIDString, entity.ShortPrefabName, entity.OwnerID.ToString()));
            }

            if (showSphere) player.SendConsoleCommand("ddraw.sphere", 2f, Color.blue, entity.CenterPoint(), 1f);
        }

        private string GetAuthorized(BaseEntity entity, BasePlayer player)
        {
            string msg = Lang("AuthorizedPlayers", player.UserIDString, entity.ShortPrefabName, GetName(entity.OwnerID.ToString())) + "\n";
            var turret = entity as AutoTurret;
            var priv = entity as BuildingPrivlidge;
            int authed = 0;

            foreach (var user in (turret ? turret.authorizedPlayers : priv.authorizedPlayers))
            {
                authed++;
                msg += $"{authed}. {GetName(user.userid.ToString())}\n";
            }

            return authed == 0 ? Lang("NoAuthorizedPlayers", player.UserIDString) : msg;
        }

        private string GetName(string id) => id == "0" ? "[SERVERSPAWN]" : covalence.Players.FindPlayer(id)?.Name;

        private T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
    }
}