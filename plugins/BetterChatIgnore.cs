using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BetterChatIgnore", "Togoshige", "1.0.2", ResourceId = 2490)]
    [Description("Players can ignore chat messages from other players")]
    public class BetterChatIgnore : RustPlugin
    {
        [PluginReference]
        private Plugin BetterChat, Ignore;

        void OnServerInitialized()
        {
            if (!BetterChat) { PrintWarning("BetterChat not detected"); }
            else
            {
                bool isSupported = new Version($"{BetterChat.Version.Major}.{BetterChat.Version.Minor}.{BetterChat.Version.Patch}") < new Version("5.0.12") ? false : true;
                if (!isSupported) { PrintWarning("This plugin is only compatible with BetterChat version 5.0.12 or greater!"); }
            }
            if (!Ignore) { PrintWarning("Ignore API not detected"); }
        }

        object OnBetterChat(Dictionary<string, object> messageData)
        {
            if (!Ignore) { PrintWarning("Ignore API not detected"); return messageData; }

            IPlayer playerSendingMessage = (IPlayer)messageData["Player"];
            ulong playerSendingMessage_userID = Convert.ToUInt64(playerSendingMessage.Id);

            List<string> blockedReceivers = (List<string>)messageData["BlockedReceivers"];
            foreach (BasePlayer playerReceivingMessage in BasePlayer.activePlayerList)
            {
                var hasIgnored = Ignore?.CallHook("HasIgnored", playerReceivingMessage.userID, playerSendingMessage_userID);
                if (hasIgnored != null && (bool)hasIgnored)
                {
                    blockedReceivers.Add(playerReceivingMessage.userID.ToString());
                }
            }

            messageData["BlockedReceivers"] = blockedReceivers;
            return messageData;
        }
    }
}