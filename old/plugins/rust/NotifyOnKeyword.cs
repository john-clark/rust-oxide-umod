using Oxide.Core.Libraries;
using System;   //String.
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Notify On Keyword", "BuzZ[PHOQUE]", "0.0.2")]
    [Description("Be notified in Chat/Discord when your Keyword (exemple : @admin) is used in chat")]

/*======================================================================================================================= 
*
*   IT IS NOT CASE SENSITIVE (everything.ToLower) . Troubles with special characters for now.
*   THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*
*=======================================================================================================================*/

    public class NotifyOnKeyword : RustPlugin
    {
        bool debug = false;
        string Prefix = "[NOK] :";                       // CHAT PLUGIN PREFIX
        ulong SteamIDIcon = 76561198044414155;          // SteamID FOR PLUGIN ICON
        string SteamToNotify = "set SteamID to notify here";
        string KeyWord = "Set your keyword here";
        string WebhookURL = "set your WebHook URL here";
        bool discordnotify = false;
        bool chatnotify = true;

        private bool ConfigChanged;

		private void Init()
        {
            LoadVariables();
        }

#region CONFIG

    protected override void LoadDefaultConfig()
        {
            LoadVariables();
        }

        private void LoadVariables()
        {
            Prefix = Convert.ToString(GetConfig("Chat Settings", "Prefix", "[NOK] :"));                       // CHAT PLUGIN PREFIX
            SteamIDIcon = Convert.ToUInt64(GetConfig("Chat Settings", "SteamIDIcon", 76561198120571165));        // SteamID FOR PLUGIN ICON - STEAM PROFILE CREATED FOR THIS PLUGIN / 76561198842176097 /
            KeyWord = Convert.ToString(GetConfig("Settings", "KeyWord", "Set your keyword here")); 
            SteamToNotify = Convert.ToString(GetConfig("Settings", "SteamID", "set SteamID to notify here"));                             
            discordnotify = Convert.ToBoolean(GetConfig("Notify", "Discord", false));               
            chatnotify = Convert.ToBoolean(GetConfig("Notify", "Chat", true));               
            WebhookURL = Convert.ToString(GetConfig("Notify", "Discord WebHook", "set your WebHook URL here"));

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

#region MESSAGES

    protected override void LoadDefaultMessages()
    {
        lang.RegisterMessages(new Dictionary<string, string>
        {
            //{"NoPermMsg", "You don't have permission for this."},
            {"UseKeywordMsg", "{0} has used your keyword."},
            {"MsgKeywordMsg", "the message was : {0}"},
            {"NullMsg", "Chat activity detected, but your keyword is null."},
            {"ToDiscordMsg", "--- YOUR KEYWORD HAS BEEN USED by {0} | message was - {1}"},

        }, this, "en");

        lang.RegisterMessages(new Dictionary<string, string>
        {
            //{"NoPermMsg", "Vous n'avez pas la permission pour cela."},
            {"UseKeywordMsg", "{0} a utilisé votre mot clef."},
            {"MsgKeywordMsg", "le message était : {0}"},
            {"NullMsg", "Activité sur le chat détectée, mais aucun mot clé défini."},
            {"ToDiscordMsg", "--- VOTRE MOT CLE A ETE UTILISE par {0} | message - {1}"},

        }, this, "fr");
    }

#endregion

#region ON PLAYER CHAT

        void OnPlayerChat(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            string playername;
            playername = player.displayName;
            if (player.displayName.Length > 24) playername = playername.Substring(0,24);
            if (debug) Puts($"-> CHECK FOR KEYWORD. IT IS : {KeyWord}");
            if (KeyWord == string.Empty)
            {
                foreach (var playeron in BasePlayer.activePlayerList.ToList())
                {
                    if (SteamToNotify == playeron.UserIDString) Player.Message(playeron, $"{lang.GetMessage("NullMsg", this, playeron.UserIDString)}",Prefix, SteamIDIcon);
                    if (debug) Puts("-> CHAT ACTIVITY DETECTED BUT KEYWORD IS NULL");
                }
                return;
            }
            string[] blabla;
            string message = string.Empty;
            string KeyWordLow = KeyWord.ToLower();    
            blabla = arg.Args.ToArray();
            foreach(string value in blabla) message = $"{message} {value.ToLower()}";
            if (message.Contains($"{KeyWordLow}"))
            {
                if (discordnotify)
                {
                    string todiscord = String.Format(lang.GetMessage("ToDiscordMsg", this, null),playername,message);
                    string messagetodiscord = @"{""content"": ""{dasmessage}""}";
                    var MessageWebHook = messagetodiscord.Replace("{dasmessage}", $"{todiscord}");
                    webrequest.Enqueue(WebhookURL, MessageWebHook, (code, response) =>
                    {
                        if (code != 204)
                        PrintWarning($"Discord API responded with code {code}.");
                    }, this, RequestMethod.POST);
                }
                if (chatnotify)
                {
                    foreach (var playeron in BasePlayer.activePlayerList.ToList())
                    {
                        if (SteamToNotify == playeron.UserIDString) Player.Message(playeron, $"{playername} {lang.GetMessage("UseKeywordMsg", this, playeron.UserIDString)}\n{lang.GetMessage("MsgKeywordMsg", this, playeron.UserIDString)} {message}",Prefix, SteamIDIcon);
                    }  
                }
                if (debug) Puts($"-> {playername} USED KEYWORD : {message}");
            }
        }

#endregion

    }
}



