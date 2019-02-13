using UnityEngine;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Entity Reducer", "Bruno Puccio", "1.3")]
    [Description("Reduces the amount of entities on the server")]
    class EntityReducer : RustPlugin
    {
        const string perm = "entityreducer.allowed";
        private ConfigData reducerConfig;

        class ConfigData
        {
            public bool chicken, cactus, deadlog, field, driftwood, collectable, birch;
        }

        protected override void LoadDefaultConfig()
        {
            ConfigData reducerconfig = new ConfigData
            {
                chicken = false,
                cactus = false,
                deadlog = false,
                field = false,
                driftwood = false,
                collectable = false,
                birch = false
            };
            SaveConfig(reducerconfig);
        }

        private void LoadConfigVariables() => reducerConfig = Config.ReadObject<ConfigData>();

        void SaveConfig(ConfigData reducerConfig) => Config.WriteObject(reducerConfig, true);

        private string Lang(string key, string id, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        void Init()
        {
            permission.RegisterPermission(perm, this);
            LoadConfigVariables();
        }
        
        private new void LoadDefaultMessages()
        {
            //English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ValidValues"] = "valid values:    True   or   False",
                ["DontHavePermission"] = "<color=#00ffffff>[Entity Reducer]</color> You do not have permission to use this command",
                ["CurrentConfig"] = "<color=#00ffffff>[Entity Reducer]</color>\n\n<color=#00ffffff>True</color> or <color=#00ffffff>False</color>\n/reduce chicken {0} \n/reduce cactus {1} \n/reduce deadlog {2} \n/reduce field {3} \n/reduce driftwood {4} \n/reduce collectable {5} \n/reduce birch {6}",
                ["ConfigChanged"] = "{0} set to {1}",
                ["SetToTrue"] = "{0} entities have been removed"
            }, this);
        }


        bool Check(BaseNetworkable entity)
        {
            if (entity.isActiveAndEnabled &&
                ((reducerConfig.chicken && entity.ShortPrefabName.Contains("chicken"))
                || (reducerConfig.cactus && entity.ShortPrefabName.Contains("cactus"))
                || (reducerConfig.deadlog && entity.ShortPrefabName.Contains("dead_log"))
                || (reducerConfig.field && entity.ShortPrefabName.Contains("field"))
                || (reducerConfig.birch && (entity.ShortPrefabName.Contains("birch_small") || entity.ShortPrefabName.Contains("birch_tiny")))
                || (reducerConfig.driftwood && entity.ShortPrefabName.Contains("driftwood"))
                || (reducerConfig.collectable && entity.ShortPrefabName.Contains("collectable"))))
            {
                entity.Kill();
                return true;
            }
            return false;
        }


        void OnEntitySpawned(BaseNetworkable entity)
        {
            Check(entity);
        }


        void SetToTrue(BasePlayer player)
        {
            int i = 0;
            BaseNetworkable[] bases = Resources.FindObjectsOfTypeAll<BaseNetworkable>();

            foreach (var singleBase in bases)
            {
                if (Check(singleBase))
                    i++;
            }

            SendReply(player, Lang("SetToTrue", player.UserIDString, i));
        }


        [ChatCommand("reduce")]
        void CmdReduce(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, perm) && player.net.connection.authLevel < 2)
            {
                SendReply(player, Lang("DontHavePermission", player.UserIDString));
                return;
            }

            if (args.Length != 2)
                SendReply(player, Lang("CurrentConfig", player.UserIDString, reducerConfig.chicken.ToString(), reducerConfig.cactus.ToString(), reducerConfig.deadlog.ToString(), reducerConfig.field.ToString(), reducerConfig.driftwood.ToString(), reducerConfig.collectable.ToString(), reducerConfig.birch.ToString()));

            else
            {
                bool arg;
                if (bool.TryParse(args[1], out arg))
                {
                    switch (args[0].ToLower())
                    {
                        case "chicken":
                            reducerConfig.chicken = arg;
                            Message("chicken", arg, player);
                            break;

                        case "cactus":
                            reducerConfig.cactus = arg;
                            Message("cactus", arg, player);
                            break;

                        case "deadlog":
                            reducerConfig.deadlog = arg;
                            Message("deadlog", arg, player);
                            break;

                        case "field":
                            reducerConfig.field = arg;
                            Message("field", arg, player);
                            break;

                        case "driftwood":
                            reducerConfig.driftwood = arg;
                            Message("driftwood", arg, player);
                            break;

                        case "collectable":
                            reducerConfig.collectable = arg;
                            Message("collectable", arg, player);
                            break;

                        case "birch":
                            reducerConfig.birch = arg;
                            Message("birch", arg, player);
                            break;

                        default:
                            SendReply(player, Lang("CurrentConfig", player.UserIDString, reducerConfig.chicken.ToString(), reducerConfig.cactus.ToString(), reducerConfig.deadlog.ToString(), reducerConfig.field.ToString(), reducerConfig.driftwood.ToString(), reducerConfig.collectable.ToString(), reducerConfig.birch.ToString()));
                            break;
                    }
                }
                else
                    SendReply(player, Lang("ValidValues", player.UserIDString));
            }
        }

        void Message(string val, bool arg, BasePlayer player)
        {
            SaveConfig(reducerConfig);
            if (arg)
                SetToTrue(player);
            else
                SendReply(player, Lang("ConfigChanged", player.UserIDString, val, arg.ToString()));
        }
    }
}