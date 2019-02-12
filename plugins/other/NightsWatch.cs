
using System.Collections.Generic;
using CodeHatch.Thrones.SocialSystem;
using CodeHatch.Engine.Modules.SocialSystem;
using CodeHatch.Common;
using CodeHatch.Engine.Core.Cache;
using CodeHatch.Engine.Networking;
using Oxide.Core;
using CodeHatch.Networking.Events.Players;

namespace Oxide.Plugins
{
    [Info("NightsWatch", "juk3b0x", 1.3)]
    public class NightsWatch : ReignOfKingsPlugin
    {
        #region LanguageAPI
        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
            { "OnlineReport" , "[CastleGuard] : [FF0000]MY LORD WE HAVE FOUND A MEMBER OF [FFFF00]{0}[FF0000] LURKING ON YOUR PROPERTY![FFFFFF] " },
            { "OfflineReport" , "[CastleGuard] : [FF0000]MY LORD, WHILE YOU WERE AWAY, AT [FFFFFF]{0}, [FF0000] WE HAVE SPOTTED A MEMBER OF [FFFF00]{1}[FF0000] LURKING AROUND ON YOUR PROPERTY![FFFFFF] " }		
			}, this);
        }
        int MinWatchInterval;
        int MaxWatchInterval;
        string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);
        #endregion
        #region Config
        T GetConfig<T>(string name, T defaultValue) => Config[name] == null ? defaultValue : (T)System.Convert.ChangeType(Config[name], typeof(T));

        protected override void LoadDefaultConfig()
        {
            Config["MinWatchInterval"] = MinWatchInterval = GetConfig("MinWatchInterval", 120);
            Config["MaxWatchInterval"] = MaxWatchInterval = GetConfig("MaxWatchInterval", 300);
            
            SaveConfig();
        }
        #endregion
        List<string[]> _NightsWatchReports = new List<string[]>();
        string time = "";
        void Loaded()
        {
            LoadDefaultMessages();
            LoadWatchReports();
            GenerateRandomTimer();
            
        }
        private void LoadWatchReports()
        {
            _NightsWatchReports = Interface.GetMod().DataFileSystem.ReadObject<List<string[]>>("NightsWatch");
        }
        private void SaveWatchReports()
        {
            Interface.GetMod().DataFileSystem.WriteObject("NightsWatch", _NightsWatchReports);
        }
        private void GenerateRandomTimer()
        {
            LoadDefaultConfig();
            System.Random RNG = new System.Random();
            int WatchInterval = RNG.Next(MinWatchInterval, MaxWatchInterval);
            Timer WatchTimer = timer.Repeat(WatchInterval, 0, () =>
            {
                WatchInterval = RNG.Next(MinWatchInterval, MaxWatchInterval);
                GetAllPlayerEntitiesAndCompareToCrest();
                Puts(WatchInterval.ToString());
            });
        }
        private void GetAllPlayerEntitiesAndCompareToCrest()
        {
            LoadDefaultMessages();
            Player ReportReceiver = null;
            string TrespassedGuildName = null;
            string TrespasserGuild = null;
            string ReportMessage = "";
            List<Entity> _globalEntList = new List<Entity>();
            _globalEntList = Entity.GetAll();
            var crestScheme = SocialAPI.Get<CrestScheme>();
            Entity TrespasserEntity = null;
            foreach (Entity ent in _globalEntList)
            {
                if (ent.IsPlayer && crestScheme.GetCrestAt(ent.Position) != null && crestScheme.GetCrestAt(ent.Position).GuildName.ToLower() != PlayerExtensions.GetGuild(ent.Owner).Name.ToLower())
                {
                    TrespassedGuildName = crestScheme.GetCrestAt(ent.Position).GuildName;
                    TrespasserEntity = ent;
                    TrespasserGuild = PlayerExtensions.GetGuild(ent.Owner).Name;
                    
                    time = Server.Instance.Time.DateCurrent.ToShortTimeString();
                }
            }
            foreach (Player player in Server.AllPlayers)
            {
                if (PlayerExtensions.GetGuild(player).Name != TrespassedGuildName) continue;
                ReportReceiver = player;  
            }
            if (ReportReceiver == null)
            {
                if (TrespasserGuild == null) return;

                _NightsWatchReports.Add(new string[] { TrespasserGuild, TrespassedGuildName, time.ToString() });
                SaveWatchReports();
                return;
            }
            ReportMessage = ReportMessage + string.Format(GetMessage("OnlineReport", ReportReceiver.Id.ToString()), TrespasserGuild);
            PrintToChat(ReportReceiver, ReportMessage);
        }
        private void OnPlayerSpawn(PlayerFirstSpawnEvent e)
        {
            List<string[]> TempList = new List<string[]>();
            LoadDefaultMessages();
            var Message = "";
            LoadWatchReports();
            foreach (string[] report in _NightsWatchReports)
            {
                if (report[1] != (PlayerExtensions.GetGuild(e.Player).Name))
                {
                    TempList.Add(report);
                    continue;
                }
                Message = string.Format(GetMessage("OfflineReport" , e.Player.Id.ToString()), report[2] , report[0]);
                PrintToChat(e.Player, Message);
                
            }
            _NightsWatchReports.Clear();
            _NightsWatchReports.AddRange(TempList);
            SaveWatchReports();
        }
    }
}