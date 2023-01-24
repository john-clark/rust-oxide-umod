using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Suicide Modifier", "Ryan", "1.0.3")]
    [Description("Adds the ability to change the suicide cooldown or remove it completely.")]
    class SuicideModifier : RustPlugin
    {
        string permissionName = "suicidemodifier.use";
        int defaultCooldown = 60;

        void Init()
        {
            permission.RegisterPermission(permissionName, this);
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file.");
            Config.Clear();
            Config["Settings", "Cooldown of default kill command (seconds)"] = defaultCooldown;
            SaveConfig();
        }

        private int getCooldown()
        {
            int output;
            if (int.TryParse(Config["Settings", "Cooldown of default kill command (seconds)"].ToString(), out output))
                return output;

            PrintWarning("Configuration file invalid, using default value.");
            return defaultCooldown;
        }

        void OnPlayerRespawn(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, permissionName))
                player.nextSuicideTime = Time.realtimeSinceStartup + getCooldown();

            return;
        }
    }
}
