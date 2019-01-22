using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("DecayNotifications", "Ankawi", "1.0.1")]
    [Description("Automatic decay notifications and a command for upkeep status in minutes.")]
    class DecayNotifications : RustPlugin
    {
        [PluginReference] Plugin HelpText;

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating new configuration file for " + this.Title + "--Version#: " + this.Version);
            Config.Clear();
            Config["AutomaticNotificationsEnabled"] = true;
            Config["NotificationIntervalInMinutes"] = 10f;
            Config["MinutesLeftNeededToSendNotification"] = 30f;
            SaveConfig();
        }
        private void Init()
        {
            LoadConfig();
            permission.RegisterPermission("decaynotifications.use", this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PrimaryDecayNotification"] = "<color=lime>DecayNotifications</color>" +
                "<color=cyan>: Your base is going to decay in <color=lime>{0}</color> minutes </color>",
                ["FinalDecayNotification"] = "<color=lime>DecayNotifications</color>" +
                "<color=cyan>: You have no items in your tool cupboard and your base has started to decay!</color>",
                ["NoPriviledge"] = "<color=lime>DecayNotifications</color>" +
                "<color=cyan>: You do not have any building priviledge</color>",
                ["NoPermission"] = "<color=lime>DecayNotifications</color>" +
                "<color=cyan>: You do not have permission to use this command",
                ["HelpTextAPI"] = "<color=yellow>/tcstatus - Check the amount of minutes left you have before your tool cupboard decays!</color>"
            }, this, "en");
            SendNotifications();

        }
        private void SendNotifications()
        {
            if ((bool)Config["AutomaticNotificationsEnabled"])
            {
                timer.Repeat(Convert.ToSingle(Config["NotificationIntervalInMinutes"]) * 60, 0, () =>
                {
                    foreach (var player in BasePlayer.activePlayerList)
                    {
                        BuildingPrivlidge priv = player.GetBuildingPrivilege();
                        if (!priv) return;
                        if (!priv.IsAuthed(player) || !player.IsAdmin) return;
                        float minutesLeft = priv.GetProtectedMinutes();

                        if (minutesLeft < Convert.ToSingle(Config["MinutesLeftNeededToSendNotification"]) && minutesLeft > 0f)
                        {
                            PrintToChat(player, String.Format(lang.GetMessage("PrimaryDecayNotification", this, player.UserIDString), minutesLeft));
                        }
                        else if (minutesLeft == 0f)
                        {
                            PrintToChat(player, lang.GetMessage("FinalDecayNotification", this, player.UserIDString));
                        }
                    }
                });
            }
        }
        [ChatCommand("tcstatus")]
        void TcstatusCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "decaynotifications.use")) {
                PrintToChat(player, lang.GetMessage("NoPermission", this, player.UserIDString));
                return;
            }
            BuildingPrivlidge priviledge = player.GetBuildingPrivilege();

            if (!priviledge) {
                PrintToChat(player, lang.GetMessage("NoPriviledge", this, player.UserIDString));
                return;
            }
            if (!priviledge.IsAuthed(player) || !player.IsAdmin) return;

            float minutesLeft = priviledge.GetProtectedMinutes();
            if (minutesLeft > 0f)
            {
                PrintToChat(player, String.Format(lang.GetMessage("PrimaryDecayNotification", this, player.UserIDString), minutesLeft));
            }
            else if (minutesLeft == 0)
            {
                PrintToChat(player, lang.GetMessage("FinalDecayNotification", this, player.UserIDString));
            }
            return;
        }
        private void SendHelpText(BasePlayer player)
        {
            player.ChatMessage(lang.GetMessage("HelpTextAPI", this, player.UserIDString));
        }
    }
}