using System;
using UnityEngine;
using System.Linq;
using Oxide.Core.Configuration;
using System.Collections.Generic; 
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("ArenaWalls", "Kappasaurus", "1.0.1")]
    public class ArenaWalls : RustPlugin
    {
        readonly DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetFile("ArenaWalls");
        Dictionary<string, bool> playerPrefs = new Dictionary<string, bool>();

        private float removeTime = 10f;

        #region Init
        void Init()
        {
            LoadConfig();
			LoadMessages();
            permission.RegisterPermission("arenawalls.admin", this);
        }
        #endregion

        #region Config
        private new void LoadConfig()
        {
            GetConfig(ref removeTime, "Remove time (seconds)");
            SaveConfig();
        }
        #endregion

        #region Comamnds
        [ChatCommand("walls")]
        void ToggleCommand(BasePlayer player, string command, string[] args)
        {
			if(!permission.UserHasPermission(player.net.connection.userid.ToString(), "arenawalls.admin"))
            {
                SendReply(player, Lang("No Permission", player.UserIDString));
                return;
            }
			
            if (!playerPrefs.ContainsKey(player.userID.ToString())) playerPrefs.Add(player.userID.ToString(), true);
            playerPrefs[player.userID.ToString()] = !playerPrefs[player.userID.ToString()];
            dataFile.WriteObject(playerPrefs);

            SendReply(player, playerPrefs[player.userID.ToString()] ? Lang("Enabled", player.UserIDString) : Lang("Disabled"));
        } 
        #endregion

        #region Hooks
        void OnEntityBuilt(Planner plan, GameObject go)
        {
            var player = plan.GetOwnerPlayer();

			if (!playerPrefs.ContainsKey(player.UserIDString) && !playerPrefs[player.UserIDString])
			{
				return;
			}

            BaseEntity entity = go.ToBaseEntity();

            if (entity.name.Contains("high"))
            {
                timer.Once(removeTime, () =>
                {
					if (entity != null)
					{
						entity.Kill();
					}
                });
            }
        }
        #endregion

        #region Helpers
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
		
		protected override void LoadDefaultConfig() => PrintWarning("Generating new configuration file...");
		
		private void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Enabled"] = "You have enabled admin exclusion mode.",
                ["Disabled"] = "You have disabled admin exclusion mode.",
				["No Permission"] = "Error! You don't have permission to use that command."
            }, this);
        }

		string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);	
        #endregion
    }
}