using System;
using UnityEngine;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Bearrels", "redBDGR", "2.0.0")]
    [Description("Random chance of bears spawning when a barrel breaks")]

    class Bearrels : RustPlugin
    {

        #region Variables

        private bool Changed;

        #endregion

        #region Configuration

        private class ConfigFile
        {
            public static float chanceOfBear = 0.1f;
        }

        private void LoadVariables()
        {
            ConfigFile.chanceOfBear = Convert.ToSingle(GetConfig("Settings", "Chance of bear", 0.1f));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        #endregion

        #region Hooks

        void Init()
        {
            LoadVariables();

            lang.RegisterMessages(new Dictionary<string, string>
            {
                /* chat */
                ["Bearrel"] = "That wasn't just a barrel- it was a Bearrel!",
            }, this);
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!entity.name.Contains("barrel"))
                return;
            if (UnityEngine.Random.Range(0f, 1f) <= ConfigFile.chanceOfBear)
            {
                SpawnBear(entity.transform.position);
                BasePlayer player = info.InitiatorPlayer;
                if (player != null)
                    PrintToChat(player, msg("Bearrel", player.UserIDString));
            }
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        #endregion

        #region Methods

        private void SpawnBear(Vector3 pos)
        {
            BaseEntity bear = GameManager.server.CreateEntity("assets/rust.ai/agents/bear/bear.prefab", pos, new Quaternion(), true);
            bear.Spawn();
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        private string msg(string key, string id = null) => lang.GetMessage(key, this, id);

        #endregion

    }
}