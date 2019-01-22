using System.Collections.Generic;   //dict
using System.Linq;  //list
using Convert = System.Convert;
using System;   //String.

namespace Oxide.Plugins
{
    [Info("Who Knocks", "BuzZ[PHOQUE]", "0.0.2")]
    [Description("Get information messages on door knock")]

/*======================================================================================================================= 
*
*   25th november 2018
*   chat commands : 
*
*
*   0.0.1   20181125    creation
*   
*
*=======================================================================================================================*/

    class WhoKnocks : RustPlugin
    {
        //bool debug = false;

        public Dictionary<Door, BasePlayer > knocked = new Dictionary<Door, BasePlayer>();

        //ulong SteamIDIcon = 76561197987461623;
        float doorcooldown = 120f;

        private bool ConfigChanged;

        const string MessageMe = "whoknocks.message";   // receive message when your door is knocked
        const string IWillKnow = "whoknocks.knows";     // know when you knock if player is online or offline

        void Init()
        {
            LoadVariables();
            permission.RegisterPermission(MessageMe, this);
            permission.RegisterPermission(IWillKnow, this);
        }

#region MESSAGES

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"KnocksMsg", "{0} is knocking one of your door."},
                {"OwnerOnlineMsg", "Owner {0}is online and has been informed."},
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

#region CONFIG

    protected override void LoadDefaultConfig()
        {
            LoadVariables();
        }

        private void LoadVariables()
        {
            //SteamIDIcon = Convert.ToUInt64(GetConfig("Chat Settings", "SteamIDIcon", "76561197987461623"));        // SteamID FOR PLUGIN ICON - STEAM PROFILE CREATED FOR THIS PLUGIN / NONE YET /
            doorcooldown = Convert.ToSingle(GetConfig("Cooldown setting", "for each door in seconds", "120"));

            if (!ConfigChanged) return;
            SaveConfig();
            ConfigChanged = false;
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                ConfigChanged = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                ConfigChanged = true;
            }
            return value;
        }

#endregion

        private void OnDoorKnocked(Door door, BasePlayer knocker)
        {
            if (door == null || knocker == null)return;
            BaseEntity dooritem = door as BaseEntity;
            if (dooritem == null)return;
            if (dooritem.OwnerID == null)return;
            bool know = permission.UserHasPermission(knocker.UserIDString, IWillKnow);

            foreach (BasePlayer playerdoor in BasePlayer.activePlayerList.ToList())
            {
                if (playerdoor == knocker)return;
                if (dooritem.OwnerID == playerdoor.userID)
                {
                    bool messageme = permission.UserHasPermission(playerdoor.UserIDString, MessageMe);
                    if (messageme == false)
                    {
                        return;
                    }
                    foreach (var doorknocked in knocked)
                    {
                        if (doorknocked.Key == door && doorknocked.Value == knocker)
                        {
                            return;
                        }
                    }
                    knocked.Add(door, knocker);
                    SendToChat(playerdoor, String.Format(lang.GetMessage("KnocksMsg", this, playerdoor.UserIDString),knocker.displayName));
                    if (know == true)
                    {
                        SendToChat(knocker, String.Format(lang.GetMessage("OwnerOnlineMsg", this, knocker.UserIDString),playerdoor.displayName));
                    }
                    timer.Once(doorcooldown, () =>
                    {
                        knocked.Remove(door);
                    });
                }
            }
            if (know == true)
            {
                foreach (BasePlayer playersleeperdoor in BasePlayer.sleepingPlayerList.ToList())
                {
                    if (dooritem.OwnerID == playersleeperdoor.userID)
                    {
                        SendToChat(knocker, String.Format(lang.GetMessage("OwnerOfflineMsg", this, knocker.UserIDString),playersleeperdoor.displayName));
                    }
                }   
            } 
        }

        private void SendToChat(BasePlayer player, string Message)
        {
            PrintToChat(player, Message);
        }
    }
}