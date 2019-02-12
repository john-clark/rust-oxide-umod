using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Plugins.CupboardMessagesExt;

namespace Oxide.Plugins
{
    [Info("Cupboard Messages", "Ryan", "1.1.0")]
    [Description("Sends a configured message to a user when they place a tool cupboard")]

    public class CupboardMessages : RustPlugin
    {
        #region Declaration

        public static CupboardMessages Instance;
        private const string Perm = "cupboardmessages.use";
        private bool UseTooltips;
        private readonly Dictionary<ulong, Timer> NoticeTimers = new Dictionary<ulong, Timer>();

        #endregion

        #region Configuration

        protected override void LoadDefaultConfig()
        {
            Config["Use Tooltips"] = UseTooltips = true;
        }

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Notice.1"] = "Remember to put resources in your cupboard to prevent it decaying!",
                ["Notice.2"] = "This cupboard needs resources in its contents to prevent your base from being removed.",
                ["Notice.3"] = "Your base will removed if you don't put sufficient resources in it's storage!"
            }, this);
        }

        #endregion

        #region Methods

        private void NoticePlayer(BasePlayer player, string notice)
        {
            if (NoticeTimers.ContainsKey(player.userID))
            {
                NoticeTimers[player.userID]?.Destroy();
                player.SendConsoleCommand("gametip.hidegametip");
            }
            player.SendConsoleCommand("gametip.showgametip", notice);
            var noticeTimer = Instance.timer.Once(7.5f, () =>
            {
                if (player.IsConnected)
                    player.SendConsoleCommand("gametip.hidegametip");
            });
            if (!NoticeTimers.ContainsKey(player.userID))
                NoticeTimers.Add(player.userID, noticeTimer);
            else NoticeTimers[player.userID] = noticeTimer;
        }

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(Perm, this);
            Instance = this;
            if(!bool.TryParse(Config["Use Tooltips"].ToString(), out UseTooltips))
                LoadDefaultConfig();
        }

        private void OnEntitySpawned(BaseNetworkable networkable)
        {
            if (!(networkable is BuildingPrivlidge)) return;
            var cupboard = (BuildingPrivlidge) networkable;
            var player = BasePlayer.FindByID(cupboard.OwnerID);
            if (player != null && player.HasPermission(Perm))
            {
                if (UseTooltips)
                {
                    NoticePlayer(player, $"Notice.{UnityEngine.Random.Range(1, 3)}".Lang(player.UserIDString));
                    return;
                }
                PrintToChat(player, $"Notice.{UnityEngine.Random.Range(1, 3)}".Lang(player.UserIDString));
            }
        }

        #endregion
    }
}

namespace Oxide.Plugins.CupboardMessagesExt
{
    public static class Extensions
    {
        private static readonly Permission permission = Interface.Oxide.GetLibrary<Permission>();
        private static readonly Lang lang = Interface.Oxide.GetLibrary<Lang>();

        public static bool HasPermission(this BasePlayer player, string perm) =>
            permission.UserHasPermission(player.UserIDString, perm);

        public static string Lang(this string key, string id = null, params object[] args) =>
            string.Format(lang.GetMessage(key, CupboardMessages.Instance, id), args);
    }
}