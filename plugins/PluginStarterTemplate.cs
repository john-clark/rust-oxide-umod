/**
 * Note: This is a starter template for educational and jump-start
 * purposes. For best performance, it is suggested to remove all  
 * hooks, use statements, and other features you don't require.
 *
 * The following links may be helpful If you're new to Oxide plugin development:
 * http://docs.oxidemod.org/
 * http://oxidemod.org/threads/c-plugin-development-guide-and-advice-dump.23738/
 * http://oxidemod.org/threads/plugin-submission-guidelines-and-requirements.23233/
 */

namespace Oxide.Plugins
{
    /**
     * The `Info` attribute should contain the plugin's name,
     * author's name, plugin's version, and a resource id
     * which can be obtained when submit to oxide.org.
     *
     * Plugin Name: Should be description, unique, and avoid
     * redundant words like "plugin", "mod", "admin", etc.
     *
     * Plugin Version: This should follow SemVer (semver.org).
     * The developmental builds ought to be 0.X.X and your
     * initial release will usually increment to 1.0.0.
     * 
     * Resource ID: The Resource ID can be found after publishing
     * your plugin to oxide.org. Your plugin's URL will end in
     * a number. For example: "plugin-starter-template.0000"
     * 
     * The `Description` attribute should contain a brief
     * description or summary about what the plugin is
     * designed to do. Don't get too lengthy on it.
     */
    [Info("PluginStarterTemplate", "AuthorName", "0.1.0", ResourceId = 0000)]
    [Description("A basic plugin template with some commonly used hooks.")]

    public class PluginStarterTemplate : RustPlugin
    {
        /// <summary>
        /// This is called when a plugin is being initialized.
        /// Other plugins may or may not be present just yet
        /// dependant on the order in which they're loaded.
        /// </summary>
        private void Init()
        {
            Puts("[INFO] Plugin initialized!");
        }

        /// <summary>
        /// This is called when a plugin has finished loading.
        /// Other plugins may or may not be present just yet
        /// dependant on the order in which they're loaded.
        /// </summary>
        private void Loaded()
        {
            Puts("[INFO] Plugin loaded!");
        }

        /// <summary>
        /// This is called when a plugin is being unloaded.
        /// </summary>
        private void Unload() {
            Puts("[INFO] Plugin unloaded!");
        }

        /// <summary>
        /// This is called when the player is being initialized,
        /// they have already connected but have not woken up
        /// yet. Great place to take notice of new players.
        /// </summary>
        /// <param name="player">The player that is being initialized.</param>
        private void OnPlayerInit(BasePlayer player)
        {
            Puts($"[INFO] Player {player.displayName} initialized!");
        }

        /// <summary>
        /// This is called when a player wakes up. This is a great
        /// place to attach your custom UI elements that should
        /// appear quickly or when the player enters the game.
        /// </summary>
        /// <param name="player">The player that has woken up.</param>
        private void OnPlayerSleepEnded(BasePlayer player)
        {
            Puts($"[INFO] Player {player.displayName} has woken!");
        }

        /// <summary>
        /// This is called when a player is about to go to sleep. Returning a 
        /// non-null value overrides the default behavior. This is a great 
        /// place to destroy custom UIs to hide while they're sleeping.
        /// </summary>
        /// <param name="player">The player that has fallen asleep.</param>
        private void OnPlayerSleep(BasePlayer player)
        {
            Puts($"[INFO] Player {player.displayName} has fallen asleep!");
        }

        /// <summary>
        /// This is called after a player has been disconnected.
        /// </summary>
        /// <param name="player">The player that has disconnected.</param>
        private void OnPlayerDisconnected(BasePlayer player)
        {
            Puts($"[INFO] Player {player.displayName} has disconnected!");
        }
    }
}
