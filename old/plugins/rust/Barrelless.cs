using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Barrelless", "Kechless", "0.0.7")]
    [Description("Npc spawn after player destroys a barrel")]
    class Barrelless : RustPlugin
    {
        #region Vars
        //Plugin object
        private PluginConfig myConfig;
        //Random object
        System.Random rnd = new System.Random();
        //prefab strings
        const string bearString = "assets/rust.ai/agents/bear/bear.prefab";
        const string scientistString = "assets/prefabs/npc/scientist/scientist.prefab";
        const string zombieString = "assets/prefabs/npc/murderer/murderer.prefab";
        const string airdropString = "assets/prefabs/misc/supply drop/supply_drop.prefab";
        //files
        const string file_main = "barrelless_players/";

        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Msg_Bearspawned"] = "<color=maroon>A wild bear just appeared!</color>",
                ["Msg_Scienistspawned"] = "<color=maroon>A Scienist saw you destroying barrels!</color>",
                ["Msg_Airdropspawned"] = "<color=maroon>An airdrop has been sent to this location!</color>",
                ["Msg_Zombiespawned"] = "<color=maroon>A zombie just appeared!</color>"

            }, this);
        }
        #endregion

        #region Config
        private void Init()
        {
            myConfig = Config.ReadObject<PluginConfig>();

        }

        protected override void LoadDefaultConfig()
        {
            Puts("[Barrelless] Creating a new configuration file");
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                BearRate = 0,

                ScientistRate = 0,

                AirdropRate = 0,

                ZombieRate = 0,

                Barrelcountdrop = 1
            };
        }

        private class PluginConfig
        {
            public int BearRate;

            public int ScientistRate;

            public int AirdropRate;

            public int ZombieRate;

            public int Barrelcountdrop;
        }
        #endregion

        #region Hooks

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            //Checks if entity is a barrel.          
            if (!entity.ShortPrefabName.StartsWith("loot-barrel") && !entity.ShortPrefabName.StartsWith("loot_barrel") && entity.ShortPrefabName != "oil_barrel")
                return;

            if (CheckPlayer(info) == false)
            {
                return;
            }
            else
            {
                Playerinfo user = get_user(info.InitiatorPlayer);
                if ( user.barrelCount < myConfig.Barrelcountdrop)
                {
                    user.barrelCount += 1;
                    update_user(info.InitiatorPlayer,user);
                }
                else
                {
                    user.barrelCount = 0;
                    update_user(info.InitiatorPlayer, user);

                    if (entity.transform.position != null)
                    {
                        if (SpawnRate(myConfig.BearRate) == true)
                        {
                            SpawnBear(bearString, entity.transform.position);
                            SendMsg(info.InitiatorPlayer, "Msg_Bearspawned");
                        }

                        if (SpawnRate(myConfig.ScientistRate) == true)
                        {
                            SpawnScientist(scientistString, entity.transform.position);
                            SendMsg(info.InitiatorPlayer, "Msg_Scienistspawned");
                        }

                        if (SpawnRate(myConfig.AirdropRate) == true)
                        {
                            SpawnSupplyCrate(airdropString, entity.transform.position);
                            SendMsg(info.InitiatorPlayer, "Msg_Airdropspawned");
                        }

                        if (SpawnRate(myConfig.ZombieRate) == true)
                        {
                            SpawnZombie(zombieString, entity.transform.position);
                            SendMsg(info.InitiatorPlayer, "Msg_Zombiespawned");
                        }
                    }
                }

                          
            }
        }

            #endregion

            #region Methodes

            private bool SpawnRate(int npcRate)
        {
            if (rnd.Next(1, 101) <= npcRate)
            {
                return true;
            }
            return false;
        }

        //Checks if hitinfo is a Baseplayer
        private bool CheckPlayer(HitInfo info)
        {
            bool Checker = false;
            BasePlayer player = info.InitiatorPlayer;
            if (player != null)
            {
                Checker = true;
            }

            return Checker;
        }

        private void SpawnSupplyCrate(string prefab, Vector3 position)
        {
            Vector3 newPosition = position + new Vector3(0, 20, 0);
            BaseEntity SupplyCrateEntity = GameManager.server.CreateEntity(prefab, newPosition);
            if (SupplyCrateEntity != null)
            {
                SupplyDrop Drop = SupplyCrateEntity.GetComponent<SupplyDrop>();
                Drop.Spawn();
            }

        }

        private void SpawnBear(string prefab, Vector3 position)
        {
            BaseEntity Bear = GameManager.server.CreateEntity(prefab, position);
            if (Bear != null)
            {
                Bear.Spawn();
            }
        }

        private void SpawnScientist(string prefab, Vector3 position)
        {
            BaseEntity scientist = GameManager.server.CreateEntity(prefab, position);

            if (scientist != null)
            {
                scientist.Spawn();
            }
        }

        private void SpawnZombie(string prefab, Vector3 position)
        {
            BaseEntity murderer = GameManager.server.CreateEntity(zombieString, position);
            if (murderer != null)
            {
                murderer.Spawn();
            }
        }

        Playerinfo get_user(BasePlayer player)
        {
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile(file_main + player.UserIDString))
            {
                Playerinfo user = new Playerinfo();
                user.userName = player.displayName.ToString();
                user.barrelCount = 0;
                update_user(player, user);
                return user;
            }
            else
            {
                string raw_player_file = Interface.Oxide.DataFileSystem.ReadObject<string>(file_main + player.UserIDString);
                return JsonConvert.DeserializeObject<Playerinfo>(raw_player_file);
            }
        }

        void update_user(BasePlayer player, Playerinfo user)
        {
            Interface.Oxide.DataFileSystem.WriteObject<string>(file_main + player.UserIDString, JsonConvert.SerializeObject(user));
        }

        #endregion

        #region Helpers
        //Send message to a player by giving baseplayer and key of the dictionary.
        private void SendMsg(BasePlayer player, string key)
        {
            PrintToChat(player, lang.GetMessage(key, this, player.UserIDString));
        }
        #endregion

        #region Classes
        public class Playerinfo
        {
            private string _userName;
            private int _barrelCount;

            public Playerinfo()
            {

            }

            public int barrelCount
            {
                get { return _barrelCount; }
                set { _barrelCount = value; }
            }

            public string userName
            {
                get { return _userName; }
                set { _userName = value; }
            }

        }
        #endregion
    }
}