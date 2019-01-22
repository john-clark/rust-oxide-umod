using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Storages Upgrade", "Orange", "1.5.0")]
    [Description("Allows players to increase  storages capacity to coffin")]
    public class StoragesUpgrade : RustPlugin
    {
        #region Vars

        private const string permUse = "storagesupgrade.use";

        #endregion

        #region Oxide Hooks

        private void OnServerInitialized()
        {
            OnStart();
        }
        
        private void OnEntitySpawned(BaseNetworkable entity)
        {
            CheckEntity(entity);
        }
        
        [ChatCommand("box.up")]
        private void Cmd(BasePlayer player)
        {
            Command(player);
        }

        #endregion

        #region Helpers

        private void OnStart()
        {
            lang.RegisterMessages(EN, this);
            permission.RegisterPermission(permUse, this);

            var list = new List<BasePlayer>();
            list.AddRange(BasePlayer.activePlayerList);
            list.AddRange(BasePlayer.sleepingPlayerList);
            
            foreach (var player in list.ToList())
            {
                if (permission.UserHasPermission(player.UserIDString, permUse))
                {
                    CheckAllContainers(player.userID);
                }
            }
        }

        private void Upgrade(StorageContainer container)
        {
            container.inventory.capacity = 42;
            container.panelName = "genericlarge";
            container.SendNetworkUpdate();
        }

        private bool IsBlocked(string name)
        {
            return config.blocked.Contains(name);
        }

        private void CheckAllContainers(ulong id)
        {
            foreach (var container in UnityEngine.Object.FindObjectsOfType<StorageContainer>())
            {
                if (!container.OwnerID.IsSteamId() || container.OwnerID != id) {continue;}
                if (IsBlocked(container.ShortPrefabName)) {continue;}
                Upgrade(container);
            }
        }
        
        private void CheckEntity(BaseNetworkable a)
        {
            var entity = a.GetComponent<StorageContainer>();
            if (entity == null) {return;}
            if (!entity.OwnerID.IsSteamId()) {return;}
            if (!permission.UserHasPermission(entity.OwnerID.ToString(), permUse)) {return;}
            if (IsBlocked(entity.ShortPrefabName)) {return;}
            Upgrade(entity);
        }
        
        private void Command(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, permUse))
            {
                message(player, "Perm");
                return;
            }
            
            CheckAllContainers(player.userID);
        }

        #endregion

        #region Configuration

        private ConfigData config;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "1. Blocked shortnames of storages")]
            public List<string> blocked;
        }

        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                blocked = new List<string>
                {
                    "campfire",
                    "waterpurifier.deployed",
                    "water_catcher_small",
                    "water_catcher_large",
                    "dropbox.deployed",
                    "waterbarrel",
                    "researchtable_deployed",
                    "repairbench_deployed",
                    "locker.deployed",
                    "workbench3.deployed",
                    "workbench1.deployed",
                    "workbench2.deployed"
                }
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintError("Configuration file is corrupt(or not exists), creating new one!");
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region Localization

        private Dictionary<string, string> EN = new Dictionary<string, string>
        {
            {"Perm", "You don't have permission to use that command!"},
        };

        private void message(BasePlayer player, string key, params object[] args)
        {
            player.ChatMessage(string.Format(lang.GetMessage(key, this, player.UserIDString), args));
        }

        #endregion
    }
}