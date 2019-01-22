/*
 * TODO:
 * Add check to make sure structure is destroyed before refunding
 * Add option to allow/disallow refunds if not the owner
 * Check to make sure refunds given when using hammer
 */

using System;

namespace Oxide.Plugins
{
    [Info("StructureRefund", "Wulf/lukespragg", "1.3.0", ResourceId = 1692)]
    [Description("Refunds previous build materials when demolishing and/or upgrading")]

    class StructureRefund : CovalencePlugin
    {
        #region Initialization

        const string permDemolish = "structurerefund.demolish";
        const string permUpgrade = "structurerefund.upgrade";

        bool demolishRefunds;
        bool upgradeRefunds;

        protected override void LoadDefaultConfig()
        {
            foreach (var grade in Enum.GetNames(typeof(BuildingGrade.Enum)))
            {
                if (grade.Equals("None") || grade.Equals("Count")) continue;
                Config[$"Refund {grade} (true/false)"] = GetConfig($"Refund {grade} (true/false)", true);
            }

            // Options
            Config["Refund for Demolish (true/false)"] = demolishRefunds = GetConfig("Refund for Demolish (true/false)", true);
            Config["Refund for Upgrade (true/false)"] = upgradeRefunds = GetConfig("Refund for Upgrade (true/false)", true);

            // Cleanup
            Config.Remove("DemolishRefunds");
            Config.Remove("UpgradeRefunds");
            Config.Remove("RefundMetal");
            Config.Remove("RefundStone");
            Config.Remove("RefundTopTier");
            Config.Remove("RefundTwigs");
            Config.Remove("RefundWood");

            SaveConfig();
        }

        void Init()
        {
            LoadDefaultConfig();
            permission.RegisterPermission(permDemolish, this);
            permission.RegisterPermission(permUpgrade, this);

            if (!demolishRefunds) Unsubscribe("OnStructureDemolish");
            if (!upgradeRefunds) Unsubscribe("OnStructureUpgrade");
        }

        #endregion

        #region Refunding

        void RefundMaterials(BuildingBlock block, BasePlayer player)
        {
            if (block.OwnerID != player.userID || player.inventory.containerMain.IsFull()) return;

            foreach (var item in block.blockDefinition.grades[(int)block.grade].costToBuild)
                player.GiveItem(ItemManager.CreateByItemID(item.itemid, (int)item.amount));
        }

        void OnStructureDemolish(BuildingBlock block, BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, permDemolish)) RefundMaterials(block, player);
        }

        void OnStructureUpgrade(BuildingBlock block, BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, permUpgrade)) RefundMaterials(block, player);
        }

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        #endregion    }
}