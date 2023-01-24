using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    // TODO LIST
    // Nothing, yet.

    [Info("KickCooldown", "Kappasaurus", "1.0.1")]

    class KickCooldown : CovalencePlugin
    {
        List<string> kicked = new List<string>();
        float kickCooldown = 300f;

        void Init() => LoadConfig();

        void OnUserDisconnected(IPlayer player, string reason)
        {
            if (reason.ToLower().Contains("kick"))
                kicked.Add(player.Id);

            timer.Once(kickCooldown, () =>
            {
                kicked.Remove(player.Id);
            });
        }

        object CanUserLogin(string name, string id, string ip)
        {
            return kicked.Contains(id) ? lang.GetMessage("Kick Cooldown", this, id) : null;
        }

        #region Configuration

        private new void LoadConfig()
        {
            GetConfig(ref kickCooldown, "Kick cooldown (seconds)");

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => PrintWarning("Generating new configuration file...");

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

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Kick Cooldown"] = "You can't join, you're on kick cooldown!"
            }, this);
        }

        #endregion
    }
}