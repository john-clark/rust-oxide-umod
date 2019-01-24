using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("StashViz", "open_mailbox", "0.1")]
    [Description("A simple plugin to help with locating your hidden stashes.")]
    public class StashViz : RustPlugin
    {
        private List<StashContainer> cache = new List<StashContainer>();

        #region commands
        [ChatCommand("sviz")]
        void CommandStash(BasePlayer player, string command, string[] args)
        {
            var ownedStashes = cache.FindAll(x => x.OwnerID == player.userID);

            foreach(var stash in ownedStashes)
            {
                player.SendConsoleCommand("ddraw.box", 10, Color.green, stash.transform.position, 0.5f);
            }
        }
        #endregion

        #region hooks
        void OnEntityKill(BaseEntity entity)
        {
            if (entity is StashContainer)
            {
                var stash = entity.GetComponent<StashContainer>();

                if (cache.Contains(stash)) cache.Remove(stash);
            }
        }

        void OnEntitySpawned(BaseEntity entity, GameObject gameObject)
        {
            if (entity is StashContainer)
            {
                var stash = entity.GetComponent<StashContainer>(); 
                var player = BasePlayer.FindByID(stash.OwnerID);

                if (player == null) return;

                if (!cache.Contains(stash)) cache.Add(stash);
            }
        }
        #endregion
    }
}
