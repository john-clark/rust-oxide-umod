using System;
using System.Collections.Generic;
using Facepunch;
using UnityEngine;
using System.Reflection;
using System.Linq;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("BuildingBlocker", "Vlad-00003", "2.4.0", ResourceId = 2456)]
    [Description("Blocks building in the building privilage zone. Deactivates raids update.")]
    //Author info:
    //E-mail: Vlad-00003@mail.ru
    //Vk: vk.com/vlad_00003

    class BuildingBlocker : RustPlugin
    {		
        #region Config setup
        private string BypassPrivilege = "buildingblocker.bypass";
        private string Prefix = "[BuildingBlocker]";
        private string PrefixColor = "#FF3047";
        private bool LadderBuilding = false;
        private bool BlockBuildings = true;
        #endregion

        #region Vars
        [PluginReference]
        Plugin NoEscape;
        private static float CupRadius = 1.9f;
        //private readonly int triggerLayer = LayerMask.GetMask("Trigger");
        Collider[] colBuffer = (Collider[])typeof(Vis).GetField("colBuffer", (BindingFlags.Static | BindingFlags.NonPublic))?.GetValue(null);
        #endregion

        #region Localization
        private string BypassPrivilageCfg = "Bypass block privilage";
        private string PrefixCfg = "Chat prefix";
        private string PrefixColorCfg = "Prefix color";
        private string LadderBuildingCfg = "Allow building ladders in the privilage zone";
        private string BlockBuildingsCfg = "Block building";
        string GetMsg(string key, object userID = null) => lang.GetMessage(key, this, userID == null ? null : userID.ToString());
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Building blocked"] = "Building is blocked."
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Building blocked"] = "Строительство заблокировано."
            }, this, "ru");
        }
        #endregion

        #region Config Init
        protected override void LoadDefaultConfig()
        {
            PrintWarning("New configuration file created.");
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            bool changed = false;
            if (GetConfig(BypassPrivilageCfg, ref BypassPrivilege))
                changed = true;
            if (GetConfig(PrefixCfg, ref Prefix))
                changed = true;
            if (GetConfig(PrefixColorCfg, ref PrefixColor))
                changed = true;
            if (GetConfig(LadderBuildingCfg, ref LadderBuilding))
                changed = true;
            if (GetConfig(BlockBuildingsCfg, ref BlockBuildings))
                changed = true;
            if (changed)
                SaveConfig();
            permission.RegisterPermission(BypassPrivilege, this);
        }
        #endregion

        #region Main
        object CanBuild(Planner plan, Construction prefab)
        {
            BasePlayer player = plan.GetOwnerPlayer();
            if (!player) return null;
            var result = NoEscape?.Call("CanDo", "build", player);
            if(result is string)
            {
                return null;
            }
            object Block = BuildingBlocked(plan, prefab);
            if (Block != null && (bool)Block)
            {
                SendToChat(player, GetMsg("Building blocked", player.UserIDString));
                return false;
            }
            return null;
        }
        public object BuildingBlocked(Planner plan, Construction prefab)
        {
            BasePlayer player = plan.GetOwnerPlayer();
            if (!player) return null;
            if (permission.UserHasPermission(player.UserIDString, BypassPrivilege)) return null;
            if (LadderBuilding && prefab.fullName.Contains("ladder.wooden")) return null;
            if (!BlockBuildings && !prefab.fullName.Contains("ladder.wooden")) return null;

            var pos = player.ServerPosition;
            //pos.y += player.GetHeight();
            var targetLocation = pos + (player.eyes.BodyForward() * 4f);
            //Puts($"{targetLocation} | {player.transform.position}");

            //var entities = Pool.GetList<BaseCombatEntity>();
            //Vis.Entities(targetLocation, CupRadius, entities, triggerLayer);
            //if (entities.Count > 0)
            //{
            //    foreach (var entity in entities)
            //    {
            //        var cup = entity.GetComponentInParent<BuildingPrivlidge>();
            //        if (cup == null) continue;

            //        if (cup.IsAuthed(player))
            //        {
            //            Pool.FreeList(ref entities);
            //            return true;
            //        }
            //        else
            //        {
            //            Pool.FreeList(ref entities);
            //            return false;
            //        }
            //    }
            //}
            //Pool.FreeList(ref entities);
            //return true;
            //int entities = Physics.OverlapSphereNonAlloc(targetLocation, CupRadius, colBuffer, triggerLayer);
            /*
             * Switched to IsBuildingBlocked
             */
            //int entities = Physics.OverlapSphereNonAlloc(targetLocation, CupRadius, colBuffer, Rust.Layers.Trigger);
            //BuildingPrivlidge FoundCup = null;
            //for (var i = 0; i < entities; i++)
            //{
            //    var cup = colBuffer[i].GetComponentInParent<BuildingPrivlidge>();
            //    //if (cup == null) continue;
            //    if (cup != null && cup.Dominates(FoundCup))
            //        FoundCup = cup;
            //    //if (!cup.IsAuthed(player))
            //    //{
            //    //    return true;
            //    //}
            //    //if (!cup.authorizedPlayers.Any((ProtoBuf.PlayerNameID x) => x.userid == player.userID))
            //    //{
            //    //    return true;
            //    //}
            //}
            //if (FoundCup != null && !FoundCup.authorizedPlayers.Any((ProtoBuf.PlayerNameID x) => x.userid == player.userID))
            //    return true;
            //return null;

            return player.IsBuildingBlocked(targetLocation, new Quaternion(0, 0, 0, 0), new Bounds(Vector3.zero, Vector3.zero));
        }
        #endregion

        #region Helpers
        private void SendToChat(BasePlayer Player, string Message)
        {
            PrintToChat(Player, "<color=" + PrefixColor + ">" + Prefix + "</color> " + Message);
        }
        private bool GetConfig<T>(string Key, ref T var)
        {
            if (Config[Key] != null)
            {
                var = (T)Convert.ChangeType(Config[Key], typeof(T));
                return false;
            }
            Config[Key] = var;
            return true;
        }
        #endregion
    }
}