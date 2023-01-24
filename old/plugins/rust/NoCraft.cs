using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("No Craft", "Ryan", "1.0.0")]
    [Description("Blacklist craft items or disable crafting altogether")]
    class NoCraft : RustPlugin
    {
        #region Declaration

        private ConfigFile CFile;
        private const string Perm = "nocraft.bypass";

        #endregion

        #region Config

        private class ConfigFile
        {
            public Dictionary<string, bool> BlockedItems { get; set; }

            public BlockAll BlockAll { get; set; }

            public ConfigFile()
            {
                BlockedItems = new Dictionary<string, bool>
                {
                    ["rock"] = true,
                    ["note"] = false
                };
                BlockAll = new BlockAll()
                {
                    Enabled = false,
                    SendMessage = true
                };
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating default configuration file...");
            CFile = new ConfigFile();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                CFile = Config.ReadObject<ConfigFile>();
                if(CFile == null) Regenerate();
            }
            catch { Regenerate(); }
        }

        protected override void SaveConfig() => Config.WriteObject(CFile);

        private void Regenerate()
        {
            PrintWarning($"Configuration file at 'oxide/config/{Name}.json' seems to be corrupt! Regenerating...");
            CFile = new ConfigFile();
            SaveConfig();
        }

        #endregion

        #region Lang

        private class Msg
        {
            public const string CantCraft = "CantCraft";
            public const string CraftingDisabled = "CraftingDisabled";
        }

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Msg.CantCraft] = "You're not allowed to craft {0}s!",
                [Msg.CraftingDisabled] = "Crafting is disabled on this server"
            }, this);
        }

        #endregion

        #region Classes

        private class BlockAll
        {
            public bool Enabled { get; set; }
            public bool SendMessage { get; set; }
        }

        #endregion

        #region Methods

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion

        #region Hooks

        private void Init() => permission.RegisterPermission(Perm, this);

        private object CanCraft(ItemCrafter itemCrafter, ItemBlueprint bp, int amount)
        {
            var player = itemCrafter.GetComponent<BasePlayer>();
            var item = bp.GetComponent<ItemDefinition>();

            if (permission.UserHasPermission(player.UserIDString, Perm))
                return null;

            if (CFile.BlockAll.Enabled)
            {
                if (CFile.BlockAll.SendMessage)
                    PrintToChat(player, Lang(Msg.CraftingDisabled, player.UserIDString));
                return false;
            }

            if (CFile.BlockedItems.ContainsKey(item.shortname))
            {
                if(CFile.BlockedItems[item.shortname])
                    PrintToChat(player, Lang(Msg.CantCraft, player.UserIDString, item.displayName.english));
                return false;
            }

            return null;
        }

        #endregion
    }
}