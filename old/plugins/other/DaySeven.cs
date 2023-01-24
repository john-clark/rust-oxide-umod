using System;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("DaySeven", "Mordeus", "2.0.1")]
	[Description("Tells a player when the 7th day is")]
    class DaySeven : CovalencePlugin
    {
        bool ConfigChanged;
        //chat settings
        public string ChatTitle { get; private set; }
        public string ChatColor { get; private set; }
        public string ChatColor5days { get; private set; }
        public string ChatColor3days { get; private set; }
        //broadcast settings
        public bool BroadcastEnabled { get; private set; }
        public int BroadcastInterval { get; private set; }       

        #region Localizations
        new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {                
                ["DaySeven"] = "{Title} It is day 7!",
				["Message"] = "{Title} Next Horde is in {0} days, {1} hours, {2} minutes, prepare!",				
                
            }, this);
        }
        #endregion
        #region Configuration
        protected override void LoadDefaultConfig()
        {
            //Chat Settings
            ChatTitle = GetConfig<string>("Settings", "Chat Title", "Day7:");
            ChatColor = GetConfig<string>("Settings", "Chat Color", "[00ba67] {0} [FFFFFF]");
            ChatColor5days = GetConfig<string>("Settings", "Chat Color 5 days", "[fafc57] {0} [FFFFFF]");
            ChatColor3days = GetConfig<string>("Settings", "Chat Color 3 days", "[ff0000] {0} [FFFFFF]");
            //Broadcast settings
            BroadcastEnabled = GetConfig<bool>("Broadcast", "Broadcast enabled(true/false)", true);
            BroadcastInterval = GetConfig<int>("Broadcast", "Broadcast interval(seconds)", 600);            

            if (!ConfigChanged) return;
            ConfigChanged = false;
            SaveConfig();
        }


        #endregion

        #region Oxide Hooks
        void Loaded()
		{
		LoadDefaultConfig();
            if (BroadcastEnabled)
                timer.Repeat(BroadcastInterval, 0, () => SendResponse());
        }
              
        void OnPlayerChat(ClientInfo player, string message)
		{            
            if (message.StartsWith("/"))
            {
                message = message.Replace("/", "");
                string msg = message.ToLower();
                if (message == "day7")
                {                    
                    var steamId = player.playerId;
                    var iplayer = FindPlayer(steamId);                    
                    SendResponse(iplayer);
                }
            }
        }        
        #endregion
        #region Functions
        void SendResponse(IPlayer player = null)
        {
            string playerId;
            if (player != null)
                playerId = player.Id.ToString();
            else
                playerId = null;
            int currentDay = GameUtils.WorldTimeToDays(GameManager.Instance.World.worldTime);
            int currentHour = GameUtils.WorldTimeToHours(GameManager.Instance.World.worldTime);
            int currentMinute = GameUtils.WorldTimeToMinutes(GameManager.Instance.World.worldTime);
            int dayLength = GameStats.GetInt(EnumGameStats.DayLightLength);
            string color = ChatColor;
            string response;

            // determine if we are within the horde period for day 7
            Boolean IsInDay7 = false;
            if (currentDay >= 7)
            {
                if (currentDay % 7 == 0 && currentHour >= 22)
                {
                    IsInDay7 = true;
                }
                // day 8 before 4 AM assuming default day length of 18, time will change otherwise
                else if (currentDay % 8 == 0 && currentHour < 24 - dayLength - 2)
                {
                    IsInDay7 = true;
                }
            }

            // not in day 7 horde period
            if (!IsInDay7)
            {
                // find the next day 7
                int daysUntilHorde = 0;

                if (currentDay % 7 != 0)
                {
                    daysUntilHorde = 7 - (currentDay % 7);
                }

                // when is the next horde?
                ulong nextHordeTime = GameUtils.DayTimeToWorldTime(currentDay + daysUntilHorde, 22, 0);
                ulong timeUntilHorde = nextHordeTime - GameManager.Instance.World.worldTime;
                int hoursUntilHorde = GameUtils.WorldTimeToHours(timeUntilHorde);
                int minutesUntilHorde = GameUtils.WorldTimeToMinutes(timeUntilHorde);
                // Chat color green more than 5
                if (daysUntilHorde < 3)
                {
                    // Chat color red if less than 3 days
                    color = ChatColor3days;
                }
                else if (daysUntilHorde < 5)
                {
                    // Chat color yellow if less that 5
                    color = ChatColor5days;
                }
                if (currentDay % 7 != 0)
                {
                    if (currentHour >= 22 && currentHour <= 23 && currentDay != 0)
                    {
                        daysUntilHorde = (daysUntilHorde - 1);
                    }
                }
                response = string.Format(Message("Message", playerId), daysUntilHorde, hoursUntilHorde, minutesUntilHorde);             

            }
            else
            {
                color = ChatColor3days;
                response = string.Format(Message("DaySeven", playerId));    
            }
            SendMessage(player, color, response);
        }
        void SendMessage(IPlayer player, string color, string response)
        {
            if (player != null)
            {
                NextFrame(() =>
                {
                    player.Reply(string.Format(color, response));
                });
            }
            else
                Broadcast(string.Format(color, response));
        }
        #endregion Functions
        #region Helpers
        void Broadcast(string key)
        {
            foreach (var player in players.Connected) player.Reply(Message(key, player.Id));
        }        
        private string Message(string key, string id = null, params object[] args)
        {
            return lang.GetMessage(key, this, id).Replace("{Title}", ChatTitle);
        }
        T GetConfig<T>(string parent, string key, T defaultValue)
        {
            var data = Config[parent] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[parent] = data;
                ConfigChanged = true;
            }
            object value;
            if (!data.TryGetValue(key, out value))
            {
                value = defaultValue;
                data[key] = value;
                ConfigChanged = true;
            }
            return (T)Convert.ChangeType(value, typeof(T));
        }
        private IPlayer FindPlayer(string playerid)
        {            
            return this.covalence.Players.FindPlayerById(playerid);
        }
        #endregion Helpers
    }
}