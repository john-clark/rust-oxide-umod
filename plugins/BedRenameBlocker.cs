using System.Collections.Generic;


namespace Oxide.Plugins
{
    [Info("Bed Rename Blocker", "Gimax", "1.0.2")]
    [Description("Blocks people of renaming a bed/sleeping bag if they are not the owner of it")]
    class BedRenameBlocker : RustPlugin
    {
        #region Languages
        protected override void LoadDefaultMessages() => lang.RegisterMessages(new Dictionary<string, string>
        {
            ["NotOwner"] = "You can not rename the bed if you are not the owner of it!"
        }, this, "en");

        #endregion

        #region Hook
        private object CanRenameBed(BasePlayer player, SleepingBag bed, string bedName)
        {
            if (bed == null || bed.OwnerID.ToString() == null || player == null || bedName == null) return true;
            if (bed.OwnerID.ToString() != player.UserIDString)
            {
                player.ChatMessage(Lang("NotOwner", player.UserIDString));
                return true;
            }
            return null;
        }
        #endregion

        #region Helper
        private string Lang(string key, string id, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        #endregion

    }
}






