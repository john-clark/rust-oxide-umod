using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("InvLock", "Sonny-Boi", 1.0)]
    [Description("Locks the inventory or hotbar of players")]
    class InvLock : RustPlugin
    {
        private const string hotbar = "invlock.hotbar";
        private const string wear = "invlock.wear";
        private const string main = "invlock.main";
        private const string update = "invlock.update";

        #region Oxide Hooks

        private void Init()
        {
            permission.RegisterPermission(hotbar, this);
            permission.RegisterPermission(wear, this);
            permission.RegisterPermission(main, this);
            permission.RegisterPermission(update, this);
        }
        void OnServerInitialized()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (permission.UserHasPermission(player.UserIDString, hotbar)) { ToggleInvLock(player, "belt", true); } else { ToggleInvLock(player, "belt", false); }
                if (permission.UserHasPermission(player.UserIDString, wear)) { ToggleInvLock(player, "wear", true); } else { ToggleInvLock(player, "wear", false); }
                if (permission.UserHasPermission(player.UserIDString, main)) { ToggleInvLock(player, "main", true); } else { ToggleInvLock(player, "main", false); }
            }
        }

        void OnServerSave() => OnServerInitialized();

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                ToggleInvLock(player, "belt", false);
                ToggleInvLock(player, "main", false);
                ToggleInvLock(player, "wear", false);
            }
        }

        #endregion

        #region Chat
        [ChatCommand("invlock")]
        private void chatCommandCmd(BasePlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.UserIDString, update)) { OnServerInitialized(); }  else { SendReply(player, Lang("NoPermission", player.UserIDString)); }
        }
        #endregion

        #region Toggling
        private void ToggleInvLock(BasePlayer player, string container, bool status)
        {
            ItemContainer inventory = null;
            switch (container.ToLower())
            {
                case "belt":
                    inventory = player.inventory.containerBelt;
                    break;
                case "main":
                    inventory = player.inventory.containerMain;
                    break;
                case "wear":
                    inventory = player.inventory.containerWear;
                    break;
            }
            inventory.SetLocked(status);
            player.SendNetworkUpdateImmediate();
        }
        #endregion

        #region Localization
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "<color=red>You do not have permission to execute this command!</color>"
            }, this);
        }
        private string Lang(string key, string userId = null, params object[] args) => string.Format(lang.GetMessage(key, this, userId), args);
        #endregion

    }
}
