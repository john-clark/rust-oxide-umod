using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Upgrade Permissions", "Sonny-Boi", "1.0.0")]
    [Description("Allows players to upgrade structures based on permissions")]
    class UpgradePermissions : RustPlugin
    {
        #region Configuration
        
        public bool Changed { get; private set; }
        public string WoodPermission { get; private set; }
        public string StonePermission { get; private set; }
        public string SheetMetalPermission { get; private set; }
        public string ArmouredPermission { get; private set; }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        private void ConfigVariables()
        {
            WoodPermission = Convert.ToString(GetConfig("Permissions", "Wood", "upgradepermissions.wood"));
            StonePermission = Convert.ToString(GetConfig("Permissions", "Stone", "upgradepermissions.stone"));
            SheetMetalPermission = Convert.ToString(GetConfig("Permissions", "SheetMetal", "upgradepermissions.sheetmetal"));
            ArmouredPermission = Convert.ToString(GetConfig("Permissions", "Armoured", "upgradepermissions.armoured"));
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            ConfigVariables();
        }

        #endregion

        #region Hooks
        private void Init()
        {
            ConfigVariables();
            permission.RegisterPermission(WoodPermission, this);
            permission.RegisterPermission(StonePermission, this);
            permission.RegisterPermission(SheetMetalPermission, this);
            permission.RegisterPermission(ArmouredPermission, this);
            permission.RegisterPermission("upgradepermissions.all", this);
        }
        object CanChangeGrade(BasePlayer player, BuildingBlock block, BuildingGrade.Enum grade)
        {
            if (permission.UserHasPermission(player.UserIDString, "upgradepermissions.all"))
                return true;
            if (grade == BuildingGrade.Enum.Wood && !permission.UserHasPermission(player.UserIDString, WoodPermission))
            {
                SendReply(player, Lang("Wood", player.UserIDString));
                return false;
            }
            if (grade == BuildingGrade.Enum.Stone && !permission.UserHasPermission(player.UserIDString, StonePermission))
            {
                SendReply(player, Lang("Stone", player.UserIDString));
                return false;
            }
            if (grade == BuildingGrade.Enum.Metal && !permission.UserHasPermission(player.UserIDString, SheetMetalPermission))
            {
                SendReply(player, Lang("SheetMetal", player.UserIDString));
                return false;
            }
            if (grade == BuildingGrade.Enum.TopTier && !permission.UserHasPermission(player.UserIDString, ArmouredPermission))
            {
                SendReply(player, Lang("Armoured", player.UserIDString));
                return false;
            }
            return null;
        }
        #endregion

        #region Localization
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Wood"] = "You do not have the permission to upgrade this structure to wood!",
                ["Stone"] = "You do not have the permission to upgrade this structure to stone!",
                ["SheetMetal"] = "You do not have the permission to upgrade this structure to SheetMetal!",
                ["Armoured"] = "You do not have permission to upgrade this structure to Armoured!"
            }, this);
        }
        private string Lang(string key, string userId = null, params object[] args) => string.Format(lang.GetMessage(key, this, userId), args);
        #endregion
    }
}