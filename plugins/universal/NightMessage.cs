//confirmed working with Hurtworld ItemV2, ROK, Rust, 7DaystoDie
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("NightMessage", "Mordeus", "1.0.1")]
    [Description("Universal Day/Night Message")]
    public class NightMessage : CovalencePlugin
    {
        public bool NightMessageSent = false;
        public bool DayMessageSent = false;
        //config
        public string ChatTitle;
        public bool UseNightMessage;
        public bool UseDayMessage;
        public float DawnTime;
        public float DuskTime;

        #region Lang API
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["NightMessage"] = "{Title} [#add8e6]Night is upon us, beware![/#]",
                ["DayMessage"] = "{Title} [#add8e6]Dawn has arrived![/#]"
            }, this);
        }
        #endregion Lang API
        #region Config

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating new configurationfile...");
        }

        private new void LoadConfig()
        {
            ChatTitle = GetConfig<string>("Title", "[#00ffff]Server:[/#]");
            UseNightMessage = GetConfig<bool>("Use Night Message", true);
            UseDayMessage = GetConfig<bool>("Use Day Message", true);
            DawnTime = GetConfig<float>("Dawn Time", 7f);
            DuskTime = GetConfig<float>("Dusk Time", 19f);
            SaveConfig();
        }
        #endregion Config
        #region Oxide    
        private void OnServerInitialized()
        {
            timer.Repeat(1, 0, CheckTime);
        }
        private void Init()
        {
            LoadConfig();   
        }
        #endregion Oxide
        #region Time Helpers
        private void CheckTime()
        {            
            if (IsNight && !NightMessageSent)
            {
                SendMessage(true, false);
                NightMessageSent = true;
                DayMessageSent = false;
            }
            else
            if (!IsNight && !DayMessageSent)
            {
                SendMessage(false, true);
                DayMessageSent = true;
                NightMessageSent = false;
            }
        }
        bool IsNight => server.Time.TimeOfDay.Hours < DawnTime || server.Time.TimeOfDay.Hours >= DuskTime;      
        
        private void SendMessage(bool night, bool day)
        {
            if (day && DayMessageSent == false && UseDayMessage)
                Broadcast("DayMessage");             

            if (night && NightMessageSent == false && UseNightMessage)                           
                Broadcast("NightMessage");              
        }
        #endregion Time Helpers
        #region Helpers        
        private string Message(string key, string id = null, params object[] args)
        {
            return lang.GetMessage(key, this, id).Replace("{Title}", ChatTitle);
        }         
        private T GetConfig<T>(params object[] pathAndValue)
        {
            List<string> pathL = pathAndValue.Select((v) => v.ToString()).ToList();
            pathL.RemoveAt(pathAndValue.Length - 1);
            string[] path = pathL.ToArray();

            if (Config.Get(path) == null)
            {
                Config.Set(pathAndValue);
                PrintWarning($"Added field to config: {string.Join("/", path)}");
            }

            return (T)Convert.ChangeType(Config.Get(path), typeof(T));
        }
        private void Broadcast(string key)
        {
            foreach (var player in players.Connected) player.Reply(Message(key, player.Id));
        }
        #endregion Helpers
    }
}