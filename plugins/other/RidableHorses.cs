using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core;
using Oxide.Plugins;

namespace Oxide.Plugins
{
    [Info("RidableHorses", "Jake_Rich", 0.1)]
    [Description("Able to tame and ride horses around")]

    public class RidableHorses : RustPlugin
    {
        public static RidableHorses thisPlugin;

        #region Settings
        public static int displayMode = 0; //0 = Only from player's belt
                                           //1 = From player's belt and inventory
        #endregion

        void Loaded()
        {
            thisPlugin = this;
        }

        void Unload()
        {
            foreach(KeyValuePair<BasePlayer,BaseEntity> data in horseData)
            {
                data.Key.SetParent(null, "");
            }
            foreach (BaseNetworkable item in fakeEntities)
            {
                item.Kill();
            }
            foreach(BasePlayer item in fakePlayers)
            {
                item.Kill();
            }
        }

        #region Writing
        public void Output(params object[] text)
        {
            string str = "";
            for (int i = 0; i < text.Length; i++)
            {
                str += text[i].ToString() + " ";
            }
            Puts(str);
        }

        public static void Write(params object[] text)
        {
            thisPlugin.Output(text);
        }

        public static void Write(object text)
        {
            thisPlugin.Output(text);
        }

        #endregion

        public static Dictionary<BasePlayer, BaseEntity> horseData = new Dictionary<BasePlayer, BaseEntity>();
        public static List<BaseNetworkable> fakeEntities = new List<BaseNetworkable>();
        public static List<BasePlayer> fakePlayers = new List<BasePlayer>();
        public static Dictionary<BaseEntity, BasePlayer> weaponNetworking = new Dictionary<BaseEntity, BasePlayer>();

        void SpawnEntity(Vector3 pos, BasePlayer player)
        {
            string prefab = "assets/prefabs/ammo/arrow/arrow.prefab";
            BaseEntity entity = GameManager.server.CreateEntity(prefab);
            if (entity != null)
            {
                entity.Spawn();
                entity.SetParent(player, "RustPlayer");
                //entity.SetParent(bear, "RustPlayer");
                entity.transform.position = new Vector3(0, 0, 0);
                entity.transform.rotation = Quaternion.Euler(0, 0, 0);
                entity.SendNetworkUpdateImmediate();
                fakeEntities.Add(entity);
                Write("Object spawned");
            }
        }

        [ChatCommand("horse")]
        BaseEntity SpawnHorse(BasePlayer player)
        {
            string prefab = "assets/bundled/prefabs/autospawn/animals/horse.prefab";
            BaseEntity entity = GameManager.server.CreateEntity(prefab,player.transform.position);
            if (entity != null)
            {
                entity.Spawn();
                //entity.SetParent(player, "RustPlayer");
                //entity.transform.position = new Vector3(0, 0, 0);
                //entity.transform.rotation = Quaternion.Euler(0, 0, 0);
                entity.SendNetworkUpdate();
                fakeEntities.Add(entity);
                Write("Horse spawned");
            }
            return entity;
        }

        [ChatCommand("ride")]
        void RideHorse(BasePlayer player)
        {
            horseData.Add(player, SpawnHorse(player));
            MountHorse(player,horseData[player]);
        }

        #region Hooks

        int tickCount = 0;
        void OnTick()
        {
            if (tickCount >= 1)
            {
                tickCount = 0;
                foreach (KeyValuePair<BasePlayer, BaseEntity> data in horseData)
                {
                    //data.Key.transform.position = Vector3.zero;
                    //data.Key.transform.localRotation = data.Value.transform.rotation;
                    data.Key.transform.localRotation = Quaternion.identity;
                    data.Key.SendNetworkUpdateImmediate();
                    //data.Value.SendNetworkUpdateImmediate();
                }
            }
            tickCount++;
        }
        /*
        object OnFurnaceSwitch()
        {
            Puts("Furance activated!");
            return null;
        }*/

        void OnHammerHit(BasePlayer player, HitInfo info)
        {
            foreach (Item item in player.inventory.containerBelt.itemList)
            {
                Puts(item.info.shortname.ToString());
            }
        }

        #endregion

        void MountHorse(BasePlayer player1,BaseEntity entity)
        {
            BasePlayer player = SpawnPlayer(player1);
            //player = player1;
            player.SetParent(entity, "");
            player.transform.position = new Vector3(-0.8f, 0f, -0.5f);
            player.SendNetworkUpdateImmediate();
        }

        BasePlayer SpawnPlayer(BasePlayer player)
        {
            string prefab = "assets/prefabs/player/player.prefab";
            BasePlayer newPlayer = (BasePlayer)GameManager.server.CreateEntity(prefab, player.transform.position + new Vector3(0, 0, 1));
            newPlayer.Spawn();
            //newPlayer.InitializeHealth(1000, 1000); 
            newPlayer.Heal(100);
            fakePlayers.Add(newPlayer);
            return newPlayer;
        }


    }
}


