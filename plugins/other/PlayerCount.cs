using System;
using System.Linq;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("PlayerCount", "Kappasaurus", "1.0.2")]
    class PlayerCount : RustPlugin
    {
        int messageTime = 10;      
		
		#region Config
		private new void LoadConfig()
		{
			GetConfig(ref messageTime, "Message time (minutes)");
			SaveConfig();
		}
        #endregion

        #region Init
        void Init()
        {
			LoadConfig();
			
			lang.RegisterMessages(new Dictionary<string, string> { ["Chat Message"] = "There are currently <color=orange>{0}</color> players online and <color=orange>{1}</color> sleepers." }, this);
			
            timer.Every(messageTime * 60, () =>
            {
				var playerCount = BasePlayer.activePlayerList.Count;
				var sleeperCount = BasePlayer.sleepingPlayerList.Count;
                PrintToChat(lang.GetMessage("Chat Message", this),String.Format("{0:n}", playerCount), String.Format("{0:n0}", sleeperCount));
            });
        }
        #endregion
		
		#region Helpers
        protected override void LoadDefaultConfig() => PrintWarning("Generating new configuration file...");

        private void GetConfig<T>(ref T variable, params string[] path)
        {
            if (path.Length == 0)
                return;

            if (Config.Get(path) == null)
            {
                Config.Set(path.Concat(new object[] { variable }).ToArray());
                PrintWarning($"Added field to config: {string.Join("/", path)}");
            }

            variable = (T)Convert.ChangeType(Config.Get(path), typeof(T));
        }
		#endregion
    }
}