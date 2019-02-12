using System.Collections.Generic;   //dict
using System.Linq;  //list
using Convert = System.Convert;
using System;   //String.

namespace Oxide.Plugins
{
    [Info("Who Knocks", "BuzZ[PHOQUE]", "0.0.4")]
    [Description("Get information messages on door knock")]

/*======================================================================================================================= 
*
*   25th november 2018
*   chat commands : 
*
*
*   0.0.1   20181125    creation
*   0.0.3               code simplified
*
*=======================================================================================================================*/

    class WhoKnocks : RustPlugin
    {
        bool testmode = false;

        const string MessageMe = "whoknocks.message";   // receive message when your door is knocked
        const string IWillKnow = "whoknocks.knows";     // know when you knock if player is online or offline

        void Init()
        {
            permission.RegisterPermission(MessageMe, this);
            permission.RegisterPermission(IWillKnow, this);
            if (testmode) PrintWarning("...testmode is [ON]");
        }

#region MESSAGES

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"KnocksMsg", "{0} is knocking one of your door."},
                {"OwnerOnlineMsg", "Owner {0} is online and has been informed."},
                {"OwnerOfflineMsg", "Owner {0} is actually sleeping !"},

            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"KnocksMsg", "{0} est en train de tocquer à une de vos porte."},
                {"OwnerOnlineMsg", "L'habitant {0} est en ligne et a été informé."},
                {"OwnerOfflineMsg", "L'habitant {0} est en train de dormir !"},

            }, this, "fr");
        }

#endregion

///////////////////////////////////////////////////////////////

        private void OnDoorKnocked(Door door, BasePlayer knocker)
        {
            if (door == null || knocker == null) return;
            BaseEntity dooritem = door as BaseEntity;
            if (dooritem == null) return;
            if (dooritem.OwnerID == null) return;
            bool know = permission.UserHasPermission(knocker.UserIDString, IWillKnow);
            BasePlayer playerdoor = BasePlayer.FindByID(dooritem.OwnerID);
            if (playerdoor == null) return;
            if (!testmode) if (playerdoor == knocker) return;
            bool messageme = permission.UserHasPermission(playerdoor.UserIDString, MessageMe);
            if (messageme == false) return;
            if (playerdoor.IsConnected) SendToChat(playerdoor, String.Format(lang.GetMessage("KnocksMsg", this, playerdoor.UserIDString),knocker.displayName));
            if (know)
            {
                SendToChat(knocker, String.Format(lang.GetMessage("OwnerOnlineMsg", this, knocker.UserIDString),playerdoor.displayName));
                if (!playerdoor.IsConnected) SendToChat(knocker, String.Format(lang.GetMessage("OwnerOfflineMsg", this, knocker.UserIDString),playerdoor.displayName));
            }
        }

        private void SendToChat(BasePlayer player, string Message)
        {
            PrintToChat(player, Message);
        }
    }
}