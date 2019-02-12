using UnityEngine;
using System.Collections.Generic;
namespace Oxide.Plugins
{
    [Info("Agriblock", "Death", "1.0.5")]
    [Description("Forces configured plant types to only be planted in planters.")]
    class Agriblock : RustPlugin
    {

        #region Hooks
        private void OnEntityBuilt(Planner plan, GameObject seed)
        {
            BasePlayer player = plan.GetOwnerPlayer();            
            var isSeed = seed.GetComponent<PlantEntity>();
            if (player == null) return;
            if (isSeed != null)
            {
                var held = player.GetActiveItem();
                NextTick(() =>
                {
                    if (isSeed.GetParentEntity() == null || !(isSeed.GetParentEntity() is PlanterBox))
                    {
                        if (held == null) return;
                        if (!configData.Types.CornEnabled && held.info.shortname.Equals("seed.corn")) return;
                        if (!configData.Types.CornCloneEnabled && held.info.shortname.Equals("clone.corn")) return;
                        if (!configData.Types.PumpkinEnabled && held.info.shortname.Equals("seed.pumpkin")) return;
                        if (!configData.Types.PumpkinCloneEnabled && held.info.shortname.Equals("clone.pumpkin")) return;
                        if (!configData.Types.HempEnabled && held.info.shortname.Equals("seed.hemp")) return;
                        if (!configData.Types.HempCloneEnabled && held.info.shortname.Equals("clone.hemp")) return;
                        SendReply(player, (msg("errmsg")).Replace("{seed}", held.info.displayName.english));
                        isSeed.Kill(BaseNetworkable.DestroyMode.None);
                        if (!configData.Options.Refund || held == null) return;
                        Item refund = ItemManager.CreateByName(held.info.shortname, 1);
                        player.inventory.GiveItem(refund);
                    }
                });
            }
        }
        #endregion

        #region Config
        void Init()
        {
            LoadConfigVariables();
        }
        private ConfigData configData;

        class ConfigData
        {
            public Options Options = new Options();
            public Types Types = new Types();
        }
        class Options
        {
            public bool Refund = true;
        }
        class Types
        {
            public bool CornEnabled = true;
            public bool CornCloneEnabled = true;
            public bool PumpkinEnabled = true;
            public bool PumpkinCloneEnabled = true;
            public bool HempEnabled = true;
            public bool HempCloneEnabled = true;
        }
        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();
            SaveConfig(configData);
        }
        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            var config = new ConfigData();
            SaveConfig(config);
        }
        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"errmsg", "You may not plant {seed} outside of a planter!" }
            }, this, "en");
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"errmsg", "Вы не можете сажать {seed} за пределами сеялки!" }
            }, this, "ru");
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"errmsg", "Sie dürfen nicht {seed} außerhalb eines pflanters Pflanzen!" }
            }, this, "de");
        }
        private string msg(string key, string id = null) => lang.GetMessage(key, this, id);
        #endregion

    }
}