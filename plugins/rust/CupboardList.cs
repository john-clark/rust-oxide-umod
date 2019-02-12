using System.Collections.Generic;
using System.Text;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    // TODO LIST
    // Nothing, yet.

    [Info("CupboardList", "Kappasaurus", "1.0.2")]

    class CupboardList : RustPlugin
    {
        private const string Prefab = "cupboard.tool.deployed";

        void Init()
        {
            permission.RegisterPermission("cupboardlist.able", this);
            LoadConfig();
        }

        [ChatCommand("cupauth")]
        void AuthCmd(BasePlayer player, string commanmd, string[] args)
        {
            if (!permission.UserHasPermission(player.userID.ToString(), "cupboardlist.able"))
            {
                PrintToChat(player, lang.GetMessage("No Permission", this, player.UserIDString));
                return;
            }

            var targetEntity = GetViewEntity(player);

            if (!IsCupboardEntity(targetEntity))
            {
                PrintToChat(player, lang.GetMessage("Not a Cupboard", this, player.UserIDString));
                return;
            }

            var cupboard = targetEntity.gameObject.GetComponentInParent<BuildingPrivlidge>();

            if (cupboard.authorizedPlayers.Count == 0)
            {
                PrintToChat(player, lang.GetMessage("No Players", this, player.UserIDString));
                return;
            }

            var output = new List<string>();

            foreach (ProtoBuf.PlayerNameID playerNameOrID in cupboard.authorizedPlayers)
                output.Add($"{playerNameOrID.username} ({playerNameOrID.userid})"); 

            PrintToChat(player, lang.GetMessage("Player List", this, player.UserIDString).Replace("{authList}", output.ToSentence()));
        }

        [ChatCommand("cupowner")]
        void OwnerCmd(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.userID.ToString(), "cupboardlist.able"))
            {
                PrintToChat(player, lang.GetMessage("No Permission", this, player.UserIDString));
                return;
            }

            var targetEntity = GetViewEntity(player);

            if (!IsCupboardEntity(targetEntity))
            {
                PrintToChat(player, lang.GetMessage("Not a Cupboard", this, player.UserIDString));
                return;
            }

            var cupboard = targetEntity.gameObject.GetComponentInParent<BuildingPrivlidge>();
            var owner = covalence.Players.FindPlayerById(cupboard.OwnerID.ToString());

            PrintToChat(player, lang.GetMessage("Owner", this, player.UserIDString).Replace("{player}", $"{owner.Name} ({owner.Id})"));
        }

        #region Helpers

        private BaseEntity GetViewEntity(BasePlayer player)
        {
            RaycastHit hit;
            bool didHit = Physics.Raycast(player.eyes.HeadRay(), out hit, 5);

            return didHit ? hit.GetEntity() : null;
        }

        private bool IsCupboardEntity(BaseEntity entity) => entity != null && entity.ShortPrefabName == Prefab;

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["No Permission"] = "Error, no permission.",
                ["Not a Cupboard"] = "Error, that's not a cupboard.",
                ["No Players"] = "Error, no players authorized.",
                ["Player List"] = "The following player(s) are authorized: {authList}.",
                ["Owner"] = "Tool cupboard owner: {player}."
            }, this);
        }

        #endregion


    }
}