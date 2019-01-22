/*ToDo:
 * Currently doesnt find the owner of blocks outside of crest zones, not even sure if that is possible, because technically there isnt one, but I will keep looking.
 *Credits: I gave up on this a while back, and then saw a post by Wulf asking how to check where a player is looking with Raycast for ROK for his Port gun plugin, 
 * this gave me the info I was looking for, and allowed me to complete this, so thanks Wulf and D-Kay.
 */
using CodeHatch;
using CodeHatch.Engine.Networking;
using UnityEngine;
using CodeHatch.Common;
using CodeHatch.StarForge.Sleeping;
using CodeHatch.Thrones.SocialSystem;
using CodeHatch.Engine.Modules.SocialSystem;
using CodeHatch.Blocks;
using CodeHatch.Engine.Core.Cache;
using CodeHatch.Engine.Modules.SocialSystem.Objects;
using CodeHatch.Blocks.Geometry;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("FindOwner", "Mordeus", "1.0.1")]
    public class FindOwner : ReignOfKingsPlugin
    {
        private int layers;
        #region Oxide Hooks
        private void OnServerInitialized()
        {
            layers = LayerMask.GetMask("Blocks");
            permission.RegisterPermission("findowner.use", this);
        }       
            #endregion
            #region lang API
            private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "notAllowed", "You are not allowed to do this!" },
                { "noOwnerDetected", "No Owner Detected." },
                { "guildNameIs", "Guild name is: {0}" },
                { "guildMembers", "Guild {0}" },
                { "noTarget", "No Target Found!" },
                { "sleeperName", "This sleepers name is {0}." },
                { "getInfo", "You are looking at a {0} at position {1} Owned by {2}({3})" },
                { "noInfo", "No info found, try again."},

        }, this);
        }
        #endregion
        #region commands
        [ChatCommand("prod")]
        void cmdProd(Player player, string command, string[] args)
        {
            string playerId = player.Id.ToString();
            ulong ownerId = 0;
            if (!hasPermission(player)) return;
            
            var position = new Vector3();
            RaycastHit hit;

            var playerentity = player.Entity;
            if (Physics.Raycast(playerentity.Position, playerentity.GetOrCreate<LookBridge>().Forward, out hit, float.MaxValue))
            {
                position = new Vector3(hit.point.x, hit.point.y, hit.point.z);
                Collider component = hit.collider;
                Collider mainCollider = component as Collider;
                var collidertype = hit.collider.GetType().Name;
                var target = mainCollider.TryGetEntity();
                if (target == null)
                {
                    if (Physics.Raycast(playerentity.Position, playerentity.GetOrCreate<LookBridge>().Forward, out hit, float.MaxValue, layers))
                    {
                        position = new Vector3(hit.point.x, hit.point.y, hit.point.z);                        
                        var block = component.gameObject;
                        bool flag = block.FirstComponentAncestor<OctPrefab>() != null;
                        if (!flag && block.layer == BlockManager.CubeLayer && block.tag != "NonOctPrefab")
                        {
                            flag = true;
                        }
                        if (flag)
                        {
                            position = block.gameObject.transform.position; //get position of actual block
                            CrestScheme crestScheme = SocialAPI.Get<CrestScheme>();
                            Crest crest = crestScheme.GetCrestAt(position);

                            if (crest == null)
                            {
                                SendReply(player, lang.GetMessage("noOwnerDetected", this, playerId));
                                return;
                            }
                            SendReply(player, lang.GetMessage("guildNameIs", this, playerId), crest.GuildName);
                            var crestguid = crest.ObjectGUID;
                            Guild guild = SocialAPI.Get<GuildScheme>().TryGetGuildByObject(crestguid);
                            if (guild != null)
                            {
                                var guildOwner = guild.Members();                                
                                SendReply(player, lang.GetMessage("guildMembers", this, playerId), guildOwner);
                            }
                        }
                    }
                    else
                        SendReply(player, lang.GetMessage("noTarget", this, playerId));
                    return;
                }                
                position = target.Position; //get position of actual entity
                if (target.name.Contains("Sleeper")) //check for Sleepers 
                {
                    PlayerSleeperObject sleeper = target.GetComponentInChildren<PlayerSleeperObject>();
                    SendReply(player, lang.GetMessage("sleeperName", this, playerId), sleeper.DisplayName);
                }
                else
                {

                    foreach (var entity in Entity.TryGetAll()) //check all entities positions and find the one closest to where player is looking
                    {
                        if (entity.Position == position)
                        {
                            var name = entity.name;
                            var isecurable = entity.TryGet<ISecurable>();
                            var owner = "";

                            foreach (var sleeper in PlayerSleeperObject.AllSleeperObjects) //Loop through sleepers and check for ownership
                            {

                                if (SocialAPI.Get<SecurityScheme>().OwnsObject(sleeper.Key, entity.TryGet<ISecurable>()))
                                {
                                    PlayerSleeperObject sleeperobject = sleeper.Value.GetComponentInChildren<PlayerSleeperObject>();
                                    ownerId = sleeper.Key;
                                    owner = sleeperobject.DisplayName;
                                }
                            }
                            foreach (var players in Server.AllPlayers)// Check all online players for ownership
                            {
                                if (SocialAPI.Get<SecurityScheme>().OwnsObject(players.Id, entity.TryGet<ISecurable>()))
                                {
                                    ownerId = players.Id;
                                    owner = players.Name;
                                }
                            }
                            foreach (var players in Server.DeadPlayerMessages)// Check all dead players for ownership
                            {
                                if (SocialAPI.Get<SecurityScheme>().OwnsObject(players.Key, entity.TryGet<ISecurable>()))
                                {                                    
                                    ownerId = players.Key;
                                    owner = players.Value;                                    
                                }
                            }                            
                            SendReply(player, lang.GetMessage("getInfo", this, playerId), name, position, owner, ownerId);
                        }
                    }
                }
            }
            else
                SendReply(player, lang.GetMessage("noInfo", this, playerId));
            return;
        }
        #endregion
        #region Helpers
        private bool hasPermission(Player player)
        {
            string playerId = player.Id.ToString();
            if (!(player.HasPermission("admin") || player.HasPermission("findowner.use")))
            {
                player.SendError(lang.GetMessage("notAllowed", this, playerId));
                return false;
            }            
            return true;
        }
        #endregion
    }
}