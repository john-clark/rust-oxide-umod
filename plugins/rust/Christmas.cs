using System;
using System.Linq;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Christmas", "Kappasaurus", "1.0.9")]
    public class Christmas : RustPlugin
    {
        private int refillTime = 30;
        private int playerDistance = 50;
        private int giftsPerPlayer = 2;
        private bool messagesEnabled = false;

        #region Init
        void Init()
        {
            LoadConfig();
            lang.RegisterMessages(new Dictionary<string, string> { ["Christmas Message"] = "Happy holidays!" }, this);

			permission.RegisterPermission("christmas.use", this);
			
            ConVar.XMas.enabled = true;
            ConVar.XMas.spawnRange = playerDistance;
			ConVar.XMas.giftsPerPlayer = giftsPerPlayer;

            timer.Every(refillTime * 60, () =>
            {
                RefillPresents();
				if(messagesEnabled)
					PrintToChat(lang.GetMessage("Christmas Message", this));
            });
        }
        #endregion

        #region Commands
        [ChatCommand("gifts")]
        void GiftsCommand(BasePlayer player, string command, string[] args)
        {
			if (!permission.UserHasPermission(player.userID.ToString(), "christmas.use"))
				return;
			RefillPresents();
	        if(messagesEnabled)
				PrintToChat(lang.GetMessage("Christmas Message", this));
        }
        #endregion

        #region Config
        private new void LoadConfig()
        {
            GetConfig(ref refillTime, "Time in-between presents and stocking refills (minutes)");
            GetConfig(ref playerDistance, "Distance a player in which to spawn");
            GetConfig(ref giftsPerPlayer, "Gifts per player");
            GetConfig(ref messagesEnabled, "Messages enabled (true/false)");

            SaveConfig();
        }
        #endregion
		
		#region Unload
		void Unload()
		{
			Puts("Disabling the Christmas event...");
			ConVar.XMas.enabled = false;
		}
		#endregion

        #region Helpers
        protected override void LoadDefaultConfig() => PrintWarning("Generating new configuration file...");

        // Laser's Code
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

        public void RefillPresents() => ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "xmas.refill");
        #endregion
    }
}