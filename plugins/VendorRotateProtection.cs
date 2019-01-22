using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("VendorRotateProtection", "nuchacho", "1.0.3")]
    [Description("Prevents anyone except the entity owner from rotating vending machine.")]
    public class VendorRotateProtection : RustPlugin
    {
        #region Config        
        private ConfigData configData;

        private class ConfigData
        {
            public bool ShouldLog { get; set; }
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            ConfigData config = new ConfigData
            {
                ShouldLog = false
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private new void LoadDefaultMessages()
        {
            //English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CantRotateVendor"] = "You can only rotate vending machines that you placed."
            }, this);

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CantRotateVendor"] = "Vous pouvez uniquement faire pivoter les distributeurs automatiques que vous avez placés."
            }, this, "fr");

            // German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CantRotateVendor"] = "Sie können nur Verkaufsautomaten drehen, die Sie platziert haben."
            }, this, "de");

            // Russian
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CantRotateVendor"] = "Вы можете вращать только торговые автоматы, которые вы разместили."
            }, this, "ru");

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CantRotateVendor"] = "Solo puede girar las máquinas expendedoras que haya colocado."
            }, this, "es");
        }

        void Init()
        {
            LoadConfigVariables();
        }

        private object OnRotateVendingMachine(VendingMachine machine, BasePlayer player)
        {
            if (machine.OwnerID == player.userID)
            {
                return null;
            }
            else
            {
                if (configData.ShouldLog)
                {
                    string output = String.Format("{0} attempted to rotate machine owned by {1}.", player.userID, machine.OwnerID);
                    Puts(output);
                }
                Player.Reply(player, Lang("CantRotateVendor", player.UserIDString));
                return 1;
            }
        }
    }
}