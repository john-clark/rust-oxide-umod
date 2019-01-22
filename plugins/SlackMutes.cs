// Requires: Slack

using System.Text.RegularExpressions;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;

using System.Linq;
using Oxide.Core;
using System;
using Oxide.Core.Plugins;


namespace Oxide.Plugins
{
    [Info("SlackMutes", "Kinesis", "1.0.2", ResourceId = 2364)]
    [Description("Sends information on Server Mutes to a Slack Channel")]

    class SlackMutes : CovalencePlugin
    {

        [PluginReference] Plugin Slack;

        #region Configuration

        string channel;
        bool notifypermanent;
        bool notifytimed;		
		bool notifyunmute;
		bool notifyexpiredunmute;		
        string style;		
		
        void Init()
        {
            LoadDefaultConfig();
            LoadDefaultMessages();
        }		
		
        protected override void LoadDefaultConfig()
        {	
            Config["Channel"] = channel = GetConfig("Channel", "");
            Config["NotifyPermanent"] = notifypermanent = GetConfig("NotifyPermanent", true);
            Config["NotifyTimed"] = notifytimed = GetConfig("NotifyTimed", true);
            Config["NotifyUnmute"] = notifyunmute = GetConfig("NotifyUnmute", true);		
            Config["NotifyExpiredUnmute"] = notifyexpiredunmute = GetConfig("NotifyExpiredUnmute", true);			
            Config["Style"] = style = GetConfig("Style", "fancy");			
            SaveConfig();
        }
        #endregion
		
        #region Localization

        void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Muted"] = "{player} was muted by {initiatorname}.",
                ["Muted Time"] = "{player} was muted by {initiatorname} for {time}.",
                ["Unmuted"] = "{player} was unmuted by {initiatorname}.",
                ["Mute Expired"] = "{player}'s Mute has expired."
            }, this);
        }

        #endregion		
		
        #region Mute Sending	

		void OnBetterChatMuted(IPlayer target, IPlayer initiator)
		{
			if (notifypermanent == true)
			{
				string message =  SlackMessage("Muted",
                        new KeyValuePair<string, string>("initiatorname", initiator.Name),
                        new KeyValuePair<string, string>("player", target.Name));
				Slack?.Call("FancyMessage", message, initiator, channel);
			}	
		}
	
		void OnBetterChatTimeMuted(IPlayer target, IPlayer initiator, DateTime expireDate)
		{
			if (notifytimed == true)
			{
				string message =  SlackMessage("Muted Time",
                        new KeyValuePair<string, string>("initiatorname", initiator.Name),
                        new KeyValuePair<string, string>("player", target.Name),
                        new KeyValuePair<string, string>("time", FormatTime(expireDate - DateTime.UtcNow)));							
				Slack?.Call("FancyMessage", message, initiator, channel);
			}
		}
	
	
		void OnBetterChatUnmuted(IPlayer target, IPlayer initiator)
		{
			if (notifyunmute == true) 
			{	
				string message =  SlackMessage("Unmuted",
					new KeyValuePair<string, string>("initiatorname", initiator.Name),
					new KeyValuePair<string, string>("player", target.Name));
				Slack?.Call("FancyMessage", message, initiator, channel);
			}	
		}
		
		void OnBetterChatMuteExpired(IPlayer player)
		{
			if (notifyexpiredunmute == true) 
			{	
				string message =  SlackMessage("Mute Expired",
					new KeyValuePair<string, string>("player", player.Name));
				Slack?.Call("FancyMessage", message, player, channel);
			}	
		}		
		
        #endregion
		
		#region DateTime

        string FormatTime(TimeSpan time) => $"{(time.Days == 0 ? string.Empty : $"{time.Days} day(s)")}{(time.Days != 0 && time.Hours != 0 ? $", " : string.Empty)}{(time.Hours == 0 ? string.Empty : $"{time.Hours} hour(s)")}{(time.Hours != 0 && time.Minutes != 0 ? $", " : string.Empty)}{(time.Minutes == 0 ? string.Empty : $"{time.Minutes} minute(s)")}{(time.Minutes != 0 && time.Seconds != 0 ? $", " : string.Empty)}{(time.Seconds == 0 ? string.Empty : $"{time.Seconds} second(s)")}";

        private bool TryGetDateTime(string source, out DateTime date)
        {
            int seconds = 0, minutes = 0, hours = 0, days = 0;

            Match s = new Regex(@"(\d+?)s", RegexOptions.IgnoreCase).Match(source);
            Match m = new Regex(@"(\d+?)m", RegexOptions.IgnoreCase).Match(source);
            Match h = new Regex(@"(\d+?)h", RegexOptions.IgnoreCase).Match(source);
            Match d = new Regex(@"(\d+?)d", RegexOptions.IgnoreCase).Match(source);

            if (s.Success)
                seconds = Convert.ToInt32(s.Groups[1].ToString());

            if (m.Success)
                minutes = Convert.ToInt32(m.Groups[1].ToString());

            if (h.Success)
                hours = Convert.ToInt32(h.Groups[1].ToString());

            if (d.Success)
                days = Convert.ToInt32(d.Groups[1].ToString());

            source = source.Replace(seconds + "s", string.Empty);
            source = source.Replace(minutes + "m", string.Empty);
            source = source.Replace(hours + "h", string.Empty);
            source = source.Replace(days + "d", string.Empty);

            if (!string.IsNullOrEmpty(source) || (!s.Success && !m.Success && !h.Success && !d.Success))
            {
                date = default(DateTime);
                return false;
            }

            date = DateTime.UtcNow + new TimeSpan(days, hours, minutes, seconds);

            return true;
        }

        #endregion
		
        #region Helpers
		
        private string SlackMessage(string key, params KeyValuePair<string, string>[] replacements)
        {
            string message = lang.GetMessage(key, this);

            foreach (var replacement in replacements)
                message = message.Replace($"{{{replacement.Key}}}", replacement.Value);

            return message;
        }

        T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

		string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);		
		
        #endregion
		
			
    }
}