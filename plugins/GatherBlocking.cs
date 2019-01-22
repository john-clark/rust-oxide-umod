using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("GatherBlocking", "DylanSMR", "1.0.2", ResourceId = 2552)]
    [Description("A plugin to block gathering via certain tools.")]

    class GatherBlocking : RustPlugin
    {
        #region Configuration
        static Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "The list of tools that cannot gather. Based on their item name.")]
            public List<string> BlockedItems;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    BlockedItems = new List<string>() { "knife.bone", "bone.club" },
                };
            }
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                    SaveConfig();
                }
            }
            catch
            {
                PrintWarning($"Creating new config file.");
                LoadDefaultConfig();
            }
        }
        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion
        #region Gather
        void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (config.BlockedItems.Contains(player.GetHeldEntity().GetItem().info.shortname))
            {
                item.amount = 0;
                SendReply(player, string.Format(lang.GetMessage("CannotGather", this, player.UserIDString), player.GetHeldEntity().GetItem().info.displayName.english));
            }
        }
        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (entity.ToPlayer() == null) return;
            if (config.BlockedItems.Contains(entity.ToPlayer().GetHeldEntity().GetItem().info.shortname))
            {
                item.amount = 0;
                SendReply(entity.ToPlayer(), string.Format(lang.GetMessage("CannotGather", this, entity.ToPlayer().UserIDString), entity.ToPlayer().GetHeldEntity().GetItem().info.displayName.english));
            }
        }
        void Loaded() => lang.RegisterMessages(new Dictionary<string, string>() { { "CannotGather", "You may not gather with the tool of: {0}" } }, this);
        #endregion
    }
}