namespace Oxide.Plugins
{
    [Info("Item History", "birthdates", "1.0.8")]
    [Description("Keep history of an item")]
    public class ItemHistory : RustPlugin
    {

        #region Variables

        private const string Permission = "ItemHistory.use";

        #endregion

        #region Hooks

        private void Init() => permission.RegisterPermission(Permission, this);

        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (item == null) return;
            var player = item.GetOwnerPlayer() ?? container.GetOwnerPlayer();
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, "ItemHistory.use") && !player.IsAdmin)
            {
                return;
            }

            if (!string.IsNullOrEmpty(item.name)) return;
            if (!container.Equals(player.inventory.containerMain) &&
                !container.Equals(player.inventory.containerBelt) &&
                !container.Equals(player.inventory.containerWear)) return;
            item.name = player.displayName + "'s " + item.info.displayName.english;

        }

        #endregion

    }
}