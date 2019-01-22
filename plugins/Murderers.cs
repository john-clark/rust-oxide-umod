using System.Text.RegularExpressions;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using System;
using System.Text;
using Oxide;
using Oxide.Game;
using UnityEngine;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Murderers", "Shadow", "1.0.2")]
    [Description("Murderers")]
    class Murderers : RustPlugin
    {

        [PluginReference]
        Plugin Kits;

        #region Variable

        public static List<BasePlayer> fakePlayers = new List<BasePlayer>();    
        public static List<BaseNetworkable> fakeEntities = new List<BaseNetworkable>();
        public string Kit;
        private bool changed;
        public bool Turret_Safe;
        public bool Animal_Safe;
        public bool bradley_Safe;
        private const string permissionNameKILL = "murderers.kill";
        private const string permissionNamePOINT = "murderers.point";
        private const string permissionNameSPAWN = "murderers.spawn";
        private const string permissionNameREMOVE = "murderers.remove";
        private Dictionary<string, int> murdersPoint = new Dictionary<string, int>();
        private Dictionary<uint, string> npcCreated = new Dictionary<uint, string>();
        private string npcPrefab = "assets/prefabs/npc/murderer/murderer.prefab";        
        int npcCoolDown;
        private bool initialized;

        #endregion

        #region Initialize
        protected override void LoadDefaultConfig()
        {
            //
            npcCoolDown = Convert.ToInt32(GetVariable("NPC", "Time to respawn after death", "80"));
            Kit = Convert.ToString(GetConfig("Kit", "Kits what bots use", "default"));
            Turret_Safe = true;
            Animal_Safe = true;
           
            if (!changed) return;
            SaveConfig();
            changed = true;
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                changed = true;
            }
            object value;
            if (data.TryGetValue(datavalue, out value)) return value;
            value = defaultValue;
            data[datavalue] = value;
            changed = true;
            return value;
        }

        void Init()
        {
            LoadDefaultConfig();
            permission.RegisterPermission(permissionNameKILL, this);
            permission.RegisterPermission(permissionNamePOINT, this);
            permission.RegisterPermission(permissionNameSPAWN, this);
            permission.RegisterPermission(permissionNameREMOVE, this);
            lang.RegisterMessages(new Dictionary<string, string>

            {
                ["Remove"] = "Removing all Murderers",   
                ["Delete"] = "Removing all Murderers Points",
                ["StartSpawn"] = "Start spawning all Murderers on their points",
                ["EndSpawn"] = "All Murderers spawned on their points",
                ["Added"] = "Added Murderers point to Data-file!",
                ["AddPoint"] = "No Murderers points found! Add new!"
            }, this);
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile("murdersPoint"))
            {
                Puts(msg("AddPoint"));
                return;
            }
            murdersPoint = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, int>>("murdersPoint");



        }


        void Kill()
        {

            foreach (var check in npcCreated)
            BaseNetworkable.serverEntities.Find(check.Key).Kill();
            Interface.Oxide.ReloadPlugin("Murderers");           
        }

        void Delete()
        {
            foreach (var check in npcCreated)
            BaseNetworkable.serverEntities.Find(check.Key).Kill();
            Interface.Oxide.DataFileSystem.WriteObject("murdersPoint", npcCreated);
            Puts(msg("Delete"));
            Interface.Oxide.ReloadPlugin("Murderers");

        }

        #endregion


        #region Function

        void startSpawn(string position = null)
        {
            BaseEntity entity = null;
            if (position != null)
            {
                timer.Once(npcCoolDown, () =>
                {
                    entity = GameManager.server.CreateEntity(npcPrefab, position.ToVector3()) as NPCPlayer;
                    entity.Spawn();
                    npcCreated.Add(entity.net.ID, position);
 
                   Kits?.Call($"GiveKit", entity, Kit);
             entity.SendNetworkUpdate();
                });
                return;
            }
            foreach (var check in murdersPoint)
            {
                    entity = GameManager.server.CreateEntity(npcPrefab, check.Key.ToVector3()) as NPCPlayer;
                    if (entity != null)
                    entity.Spawn();
 
                   Kits?.Call($"GiveKit", entity, Kit);
                    npcCreated.Add(entity.net.ID, check.Key);
             entity.SendNetworkUpdate();
            }
            Puts(msg("EndSpawn"));
        }

     object CanBeTargeted(BaseCombatEntity player, MonoBehaviour turret)//stops autoturrets targetting bots

        {

            player = player as NPCPlayer;
            if (player is NPCPlayer && Turret_Safe)

            return false;

            return null;

        }




        object CanNpcAttack(BaseNpc npc, BaseEntity target) //nulls animal damage to bots

        {

            if (target is NPCPlayer && Animal_Safe)

            return true;

            return null;

        }


        #endregion

        #region Hook
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (npcCreated.ContainsKey(entity.net.ID))

            {
                startSpawn(npcCreated[entity.net.ID]);
                npcCreated.Remove(entity.net.ID);

            }
        }

        
        public object CheckKit(string name) //credit K1lly0u

        {

            object success = Kits?.Call("isKit", name);
            if ((success is bool))

                if (!(bool)success)

                {

                    PrintWarning("NpcPoint : The Specified Kit Does Not Exist. Please update your config and reload.");

                    return null;

                }

            return true;

        }



        #endregion 



        [ConsoleCommand("murders.spawn")]
        void CmdSpawn(ConsoleSystem.Arg arg)
        {
            startSpawn();

        }

        [ConsoleCommand("murders.kill")]
        void CmdBotKill(ConsoleSystem.Arg arg)
        {

            Kill();

        }

        [ConsoleCommand("murders.wipepoint")]
        void CmdBotWipe(ConsoleSystem.Arg arg)
        {
            Delete();
        }

        [ChatCommand("murders.spawn")]
        void npcSpawn(BasePlayer player)
        {
           if (!permission.UserHasPermission(player.UserIDString, permissionNameSPAWN))
               return;
           {          
          
            startSpawn();
              {
               player.ChatMessage(msg("Murderers ReSpawning", player.UserIDString));
              }
           }
        }
        [ChatCommand("murders.point")]
        void npcMain(BasePlayer player, string command, string[] args)
        {
           if (!permission.UserHasPermission(player.UserIDString, permissionNamePOINT))
               return;
           {
            int amount = 1;
            if (args.Length == 1 && Int32.Parse(args[0]) > 0)
                amount = Int32.Parse(args[0]);
            murdersPoint.Add(player.transform.position.ToString(), amount);
            Puts(msg("Added"));
              {
               Interface.Oxide.DataFileSystem.WriteObject("murdersPoint", murdersPoint);
               player.ChatMessage(msg("Saving SpawnPoints", player.UserIDString));

              }
           }
        }

        [ChatCommand("murders.wipepoint")]
        void npcWipe(BasePlayer player)
        {
           if (!permission.UserHasPermission(player.UserIDString, permissionNameREMOVE))
               return;
           {
            Delete();
              {
               player.ChatMessage(msg("Delete", player.UserIDString));
              }
           }
        }

        [ChatCommand("murders.kill")]
        void npcKill(BasePlayer player, string command, string[] args)
        {
           if (!permission.UserHasPermission(player.UserIDString, permissionNameKILL))
               return;
           {

                npcCreated.Remove(player.net.ID);
           Kill();
              {
            player.ChatMessage(msg("Remove", player.UserIDString));
              }
           }
        }


        #region Helper

        string msg(string key, string id = null) => lang.GetMessage(key, this, id);

        object GetVariable(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
            }
            return value;
        }
        #endregion
    }
}