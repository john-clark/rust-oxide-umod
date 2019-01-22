using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("TemporaryBags", "Kappasaurus", "1.0.1")]

    class TemporaryBags : RustPlugin
    {
        private const string SleepingBag = "sleepingbag_leather_deployed";
        private const string ExcludePermission = "temporarybags.exclude";

        void Init()
        {
            permission.RegisterPermission("temporarybags.exclude", this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Remove Message"] = "<size=12>Bag removed...</size>",
                ["Place Message"] = "<size=12>Please notem sleeping bags are <i>one time use</i> items, if you'd like an infinite respawn points make a bed.</size>"

            }, this);
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            var player = plan.GetOwnerPlayer();
            var entity = go.ToBaseEntity();

            if (permission.UserHasPermission(player.UserIDString, ExcludePermission))
                return;

            if (entity.ShortPrefabName == "sleepingbag_leather_deployed")
                PrintToChat(player, lang.GetMessage("Place Message", this, player.UserIDString));
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, ExcludePermission))
                return;

            var entities = new List<BaseEntity>();
            Vis.Entities(player.transform.position, 0.1f, entities);

            foreach (var entity in entities)
                if (entity.ShortPrefabName == SleepingBag)
                {
                    PrintToChat(lang.GetMessage("Remove Message", this, player.UserIDString));
                    entity.Kill();
                    return;
                }
        }
    }
}