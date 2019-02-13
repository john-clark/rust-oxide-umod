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
    [Info("TankTurrets", "Shadow", "1.0.10")]
    [Description("use tank for turret")]
    class TankTurrets : RustPlugin
    {
		[PluginReference] Plugin Vanish;

        #region Variable
        private const string permissionNameKILL = "tankturrets.kill";
        private const string permissionNamePOINT = "tankturrets.point";
        private const string permissionNameSPAWN = "tankturrets.spawn";
        private const string permissionNameREMOVE = "tankturrets.remove";
        private Dictionary<string, int> TankTurret = new Dictionary<string, int>();
        private Dictionary<uint, string> npcCreated = new Dictionary<uint, string>();
        private string npcPrefab = "assets/prefabs/npc/m2bradley/bradleyapc.prefab";
        int npcCoolDown;
        #endregion


        #region Initialize
        protected override void LoadDefaultConfig()
        {
            //
            npcCoolDown = Convert.ToInt32(GetVariable("NPC", "Time to respawn after death", "60"));
            SaveConfig();


        }
       private void Init()
        {
            permission.RegisterPermission(permissionNameKILL, this);
            permission.RegisterPermission(permissionNamePOINT, this);
            permission.RegisterPermission(permissionNameSPAWN, this);
            permission.RegisterPermission(permissionNameREMOVE, this);
            LoadDefaultConfig();
            lang.RegisterMessages(new Dictionary<string, string>

            {
                //chat
                ["Delete"] = "Removing all Tank Points",
                ["StartSpawn"] = "Start spawning all TANK on their points",
                ["EndSpawn"] = "All TANK spawned on their points",
                ["Added"] = "Added TANK point to Data-file!",
                ["AddPoint"] = "No points found! Add new!",
                ["KillTank"] = "All Tanks its Removed!"
            }, this);
            TankTurret = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, int>>("TankTurret");
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile("TankTurret"))
            {
                Puts(msg("AddPoint"));
                return;
            }
            TankTurret = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, int>>("TankTurret");

        }

        #region Kill

        void Unload()
        {

            foreach (var check in npcCreated)
            BaseNetworkable.serverEntities.Find(check.Key).Kill();
            Puts(msg("KillTank"));
            Interface.Oxide.ReloadPlugin("TankTurrets");
        }

        #endregion

        #region Delete

        void Delete()
        {
            foreach (var check in npcCreated)
            BaseNetworkable.serverEntities.Find(check.Key).Kill();
            Interface.Oxide.DataFileSystem.WriteObject("TankTurret", npcCreated);
            Puts(msg("Delete"));
            Interface.Oxide.ReloadPlugin("TankTurrets");

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
                    entity = GameManager.server.CreateEntity(npcPrefab, position.ToVector3());
                    entity.Spawn();
                    npcCreated.Add(entity.net.ID, position);

                });
                return;
            }
            foreach (var check in TankTurret)
            {
                entity = GameManager.server.CreateEntity(npcPrefab, check.Key.ToVector3());
                if (entity != null)
                    entity.Spawn();
                npcCreated.Add(entity.net.ID, check.Key);

            }
            Puts(msg("EndSpawn"));
        }

		private bool CanBradleyApcTarget(BradleyAPC bradley, BaseEntity target)
		{
			if (target is NPCPlayer)
			{
				return false;
			}
			
			var player = target as BasePlayer;
			
			if (player != null)
			{
				var canNetwork = Vanish?.Call("IsInvisible", player);
							
				if (canNetwork is bool)
				{
					if ((bool) canNetwork)
					{
						return false;
					}
				}
							
			}
			return true;
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



        #endregion


        [ConsoleCommand("tankspawn")]
        void CmdBotCount(ConsoleSystem.Arg arg)
        {
            startSpawn();

        }


        [ConsoleCommand("killtank")]
        void CmdBotKill(ConsoleSystem.Arg arg)
        {

            Unload();

        }

        [ConsoleCommand("wipepoint")]
        void CmdBotWipe(ConsoleSystem.Arg arg)
        {
            Delete();
        }


        [ChatCommand("tankspawn")]
        void npcSpawn(BasePlayer player)
        {
           if (!permission.UserHasPermission(player.UserIDString, permissionNameSPAWN))
               return;
           {
            startSpawn();
              {
               player.ChatMessage(msg("EndSpawn", player.UserIDString));
              }
           }
        }

        [ChatCommand("wipepoint")]
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
        [ChatCommand("tankpoint")]
        void npcMain(BasePlayer player, string command, string[] args)
        {
           if (!permission.UserHasPermission(player.UserIDString, permissionNamePOINT))
                return;
           {

            int amount = 1;
            if (args.Length == 1 && Int32.Parse(args[0]) > 0)
                amount = Int32.Parse(args[0]);
            TankTurret.Add(player.transform.position.ToString(), amount);
            Puts(msg("Added"));
              {
               Interface.Oxide.DataFileSystem.WriteObject("TankTurret", TankTurret);
               player.ChatMessage(msg("Added", player.UserIDString));

              }
           }           
        }
        


        [ChatCommand("killtank")]
        void BotKill(BasePlayer player, string command, string[] args)
        {
           if (!permission.UserHasPermission(player.UserIDString, permissionNameKILL))
               return;
           {

           Unload();
              {
            player.ChatMessage(msg("KillTank", player.UserIDString));
              }
           }
        }

        #endregion

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