using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("CupboardRange", "DEUSNEXUS", "1.0.0")]
    class CupboardRange : RustPlugin
    {
        private Dictionary<ulong, DateTime> _cooldown = new Dictionary<ulong, DateTime>();

        void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Construction prohibited", "Construction prohibited"}
            }, this);
        }

        private object CanBuild(Planner plan, Construction prefab)
        {
            var player = plan.GetOwnerPlayer();

            var cupboard = player.GetBuildingPrivilege();
            if (cupboard != null && !cupboard.IsAuthed(player))
            {
                if (!_cooldown.ContainsKey(player.userID) || _cooldown[player.userID].AddSeconds(5) < DateTime.Now)
                    PrintToChat(player, Lang("Construction prohibited", player.UserIDString));

                _cooldown[player.userID] = DateTime.Now;
                return false;
            }

            return null;
        }

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

    }
}