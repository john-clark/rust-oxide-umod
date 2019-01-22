using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Ban Delete By", "Ryan", "1.0.4")]
    [Description("Removes all entities placed by a player when they get banned.")]

    class BanDeleteBy : RustPlugin
    {
        private void OnUserBanned(string name, string id)
        {
            var ID = Convert.ToUInt64(id);
            if (ID.IsSteamId())
            {
                ConVar.Entity.DeleteBy(ID);
                LogToFile("", $"Deleting all entities owned by {name} ({id}) because they got banned", this, false);
            }
        }

        private IEnumerator Delete(IEnumerable<BaseNetworkable> list, HashSet<ulong> banList)
        {
            var count = 0;
            foreach (var networkable in list)
            {
                var entity = networkable as BaseEntity;
                if (entity != null && banList.Contains(entity.OwnerID))
                {
                    networkable.Kill();
                    count++;
                    yield return new WaitWhile(() => !networkable.IsDestroyed);
                }
            }
            if(count > 0)
                Puts($"Removed {count} entities belonging to {banList.Count} banned players");
            else
                NextTick(() => { Puts($"No entities found belonging to {banList.Count} banned players"); });
        }

        [ConsoleCommand("deleteby.removeall")]
        private void RemoveAllCommand(ConsoleSystem.Arg args)
        {
            var bannedPlayers = new HashSet<ulong>(ServerUsers.GetAll(ServerUsers.UserGroup.Banned).Select(x => x.steamid));
            ServerMgr.Instance.StartCoroutine(Delete(BaseNetworkable.serverEntities, bannedPlayers));
            args.ReplyWith($"Started to remove entities belonging to banned players");
        }
    }
}