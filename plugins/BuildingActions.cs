using System;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Building Actions", "Iv Misticos", "1.1.0")]
    [Description("Rotate and demolish buildings when you want!")]
    class BuildingActions : RustPlugin
    {
        #region Configuration

        private Configuration _config;
        
        private class Configuration
        {
            [JsonProperty(PropertyName = "Owner Can Demolish")]
            public bool DemolishOwner = true;
            
            [JsonProperty(PropertyName = "Owner Can Repair")]
            public bool RepairOwner = true;
            
            [JsonProperty(PropertyName = "Owner Can Rotate")]
            public bool RotateOwner = true;
            
            [JsonProperty(PropertyName = "Owner Can Upgrade")]
            public bool UpgradeOwner = true;
            
            [JsonProperty(PropertyName = "In-game Friend Can Demolish")]
            public bool DemolishFriend = true;
            
            [JsonProperty(PropertyName = "In-game Friend Can Repair")]
            public bool RepairFriend = true;
            
            [JsonProperty(PropertyName = "In-game Friend Can Rotate")]
            public bool RotateFriend = true;
            
            [JsonProperty(PropertyName = "In-game Friend Can Upgrade")]
            public bool UpgradeFriend = true;
            
            [JsonProperty(PropertyName = "Authorized Can Demolish")]
            public bool DemolishAuthorized = true;
            
            [JsonProperty(PropertyName = "Authorized Can Repair")]
            public bool RepairAuthorized = true;
            
            [JsonProperty(PropertyName = "Authorized Can Rotate")]
            public bool RotateAuthorized = true;
            
            [JsonProperty(PropertyName = "Authorized Can Upgrade")]
            public bool UpgradeAuthorized = true;
            
            [JsonProperty(PropertyName = "Admin Can Demolish")]
            public bool DemolishAdmin = true;
            
            [JsonProperty(PropertyName = "Admin Can Repair")]
            public bool RepairAdmin = true;
            
            [JsonProperty(PropertyName = "Admin Can Rotate")]
            public bool RotateAdmin = true;
            
            [JsonProperty(PropertyName = "Admin Can Upgrade")]
            public bool UpgradeAdmin = true;

            [JsonProperty(PropertyName = "Ignore settings")]
            public bool IgnoreSettings = true;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
            }
            catch
            {
                Config.WriteObject(_config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}.jsonError");
                PrintError("The configuration file contains an error and has been replaced with a default config.\n" +
                           "The error configuration file was saved in the .jsonError extension");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion
        
        #region Hooks
        
        private void OnServerInitialized()
        {
            foreach (var entity in UnityEngine.Object.FindObjectsOfType<BuildingBlock>())
            {
                TryChangeProperties(entity);
            }
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            var block = entity as BuildingBlock;
            if (block == null)
                return;
            
            TryChangeProperties(block);
        }

        private object OnStructureDemolish(BaseCombatEntity entity, BasePlayer player, bool immediate)
        {
            var block = entity as BuildingBlock;
            if (block == null || _config.IgnoreSettings)
                return null;

            return _config.DemolishOwner && block.OwnerID == player.userID ||
                   _config.DemolishFriend && AreRelationshipFriends(player, block.OwnerID) ||
                   _config.DemolishAuthorized && IsAuthorized(player, block) ||
                   _config.DemolishAdmin && player.IsAdmin
                ? (object) null
                : false;
        }

        private object OnStructureRepair(BaseCombatEntity entity, BasePlayer player)
        {
            var block = entity as BuildingBlock;
            if (block == null || _config.IgnoreSettings)
                return null;

            return _config.RepairOwner && block.OwnerID == player.userID ||
                   _config.RepairFriend && AreRelationshipFriends(player, block.OwnerID) ||
                   _config.RepairAuthorized && IsAuthorized(player, block) ||
                   _config.RepairAdmin && player.IsAdmin
                ? (object) null
                : false;
        }

        private object OnStructureRotate(BaseCombatEntity entity, BasePlayer player)
        {
            var block = entity as BuildingBlock;
            if (block == null || _config.IgnoreSettings)
                return null;

            return _config.RotateOwner && block.OwnerID == player.userID ||
                   _config.RotateFriend && AreRelationshipFriends(player, block.OwnerID) ||
                   _config.RotateAuthorized && IsAuthorized(player, block) ||
                   _config.RotateAdmin && player.IsAdmin
                ? (object) null
                : false;
        }

        private object OnStructureUpgrade(BaseCombatEntity entity, BasePlayer player, BuildingGrade.Enum grade)
        {
            var block = entity as BuildingBlock;
            if (block == null || _config.IgnoreSettings)
                return null;

            return _config.UpgradeOwner && block.OwnerID == player.userID ||
                   _config.UpgradeFriend && AreRelationshipFriends(player, block.OwnerID) ||
                   _config.UpgradeAuthorized && IsAuthorized(player, block) ||
                   _config.UpgradeAdmin && player.IsAdmin
                ? (object) null
                : false;
        }
        
        #endregion
        
        #region Helpers

        private void TryChangeProperties(BuildingBlock block)
        {
            block.CancelInvoke(block.StopBeingRotatable);
            block.CancelInvoke(block.StopBeingDemolishable);
            
            block.SetFlag(BaseEntity.Flags.Reserved1, true);
            block.SetFlag(BaseEntity.Flags.Reserved2, true);
        }

        private bool AreRelationshipFriends(BasePlayer player, ulong target) =>
            RelationshipManager.Instance.FindTeam(player.currentTeam)?.members?.Contains(target) ?? false;

        private bool IsAuthorized(BasePlayer player, BaseEntity block) =>
            block.GetBuildingPrivilege()?.IsAuthed(player) ?? false;

        #endregion
    }
}