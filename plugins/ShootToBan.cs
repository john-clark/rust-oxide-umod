using System.Collections.Generic;
namespace Oxide.Plugins
{
    [Info("ShootToBan", "Death", "1.1.0")]
    [Description("Make banning players easy by just shooting your gun.")]
    public class ShootToBan : RustPlugin
    {

        #region Declarations
        List<ulong> Armed = new List<ulong>();
        const string stbPerm = "shoottoban.use";
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfigVariables();
            permission.RegisterPermission(stbPerm, this);
            if (!configData.Settings.Enabled)
                Unsubscribe(nameof(OnPlayerAttack));
        }
        void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (attacker != null && Armed.Contains(attacker.userID))
            {
                BasePlayer victim = info?.HitEntity.ToPlayer();
                if (victim == null) return;
                if (attacker.GetActiveItem().info?.shortname == configData.Settings.Ban_Weapon)
                {
                    if (victim.IsConnected)
                        victim.Kick(msg("reason").Replace("{0}", attacker.displayName));
                    ServerUsers.Set(victim.userID, ServerUsers.UserGroup.Banned, "", "");
                    ServerUsers.Save();
                    rust.BroadcastChat(null, msg("banned").Replace("{0}", attacker.displayName).Replace("{1}", victim.displayName));
                }
            }
        }
        #endregion

        #region Functions
        [ChatCommand("stb")]
        void stb(BasePlayer player)
        {
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, stbPerm))
            {
                MSG(player, msg("denied"));
                return;
            }
            if (Armed.Contains(player.userID))
                Disable(player);
            else
            {
                int time = configData.Settings.Command_Active_Time;
                Armed.Add(player.userID);
                MSG(player, msg("enabled").Replace("{0}", time.ToString()));
                timer.Once(time, ()
                    => Disable(player));
            }
        }
        void Disable(BasePlayer player)
        {
            if (!Armed.Contains(player.userID))
                return;
            Armed.Remove(player.userID);
            MSG(player, msg("disabled"));
            if (Armed.Count == 0)
                Unsubscribe(nameof(OnPlayerAttack));
        }
        void MSG(BasePlayer player, string m)
        {
            if (player == null) return;
            SendReply(player, m);
            Puts($"Message sent to {player.displayName}: {m}");
        }
        #endregion

        #region Config
        private ConfigData configData;
        class ConfigData
        {
            public Settings Settings = new Settings();
        }
        class Settings
        {
            public bool Enabled = true;
            public string Ban_Weapon = "rifle.bolt";
            public int Command_Active_Time = 30;
        }
        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();
            SaveConfig(configData);
        }
        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            var config = new ConfigData();
            SaveConfig(config);
        }
        void SaveConfig(ConfigData config)
            => Config.WriteObject(config, true);
        #endregion

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"enabled", "ShootToBan is now enabled. Will deactivate after {0} seconds!" },
                {"disabled", "ShootToBan is now disabled." },
                {"denied", "You do not have permission to use this command!" },
                {"banned", "{0} banned {1} from the server!" },
                {"reason", "You've been banned from this server by {0}" }
            }, this, "en");
        }
        private string msg(string key, string id = null) => lang.GetMessage(key, this, id);
        #endregion

    }
}