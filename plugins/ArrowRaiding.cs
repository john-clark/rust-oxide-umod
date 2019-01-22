using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Arrow Raiding", "Bruno Puccio", "1.2.0")]
    [Description("allow players to break wooden doors using bow and arrow")]
    class ArrowRaiding : RustPlugin
    {
        const string perm = "arrowraiding.allowed";
        private ConfigData arrowConfig;

        private class ConfigData
        {
            public int bowWooden, bowHV, bowBone,
                crossbowWooden, crossbowHV, crossbowBone;
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            ConfigData arrowConfig = new ConfigData
            {
                bowWooden = 200,
                bowHV = 250,
                bowBone = 250,
                crossbowWooden = 100,
                crossbowHV = 125,
                crossbowBone = 125 
            };
            SaveConfig(arrowConfig);
        }

        private void LoadConfigVariables() => arrowConfig = Config.ReadObject<ConfigData>();

        void SaveConfig(ConfigData arrowConfig) => Config.WriteObject(arrowConfig, true);

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
                ["ValidValues"] = "valid values from:   1   to   5000",
                ["DontHavePermission"] = "<color=#00ffffff>[Arrow Raiding]</color> You do not have permission to use this command",
                ["CurrentConfig"] = "<color=#00ffffff>[Arrow Raiding]</color>\n\nAmount of arrows needed to break a wooden door\n\n/arrow bowWooden {0} \n/arrow bowHV {1} \n/arrow bowBone {2} \n\n/arrow crossbowWooden {3} \n/arrow crossbowHV {4} \n/arrow crossbowBone {5}",
                ["ConfigChanged"] = "Now you need to shoot {0} {1} arrows to break a wooden door"
            }, this);
        }


        
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity?.ShortPrefabName == "door.hinged.wood")
            {
                string weaponName = info?.Weapon?.ToString();
                if (weaponName != null)
                {
                    if (weaponName.Contains("bow_hunting"))
                        info.damageTypes.types[10] = LookForDamage(info.ProjectilePrefab.name, true);

                    else if (weaponName.Contains("crossbow"))
                        info.damageTypes.types[10] = LookForDamage(info.ProjectilePrefab.name, false);
                }
            }

            if (entity?.ShortPrefabName == "door.double.hinged.wood")
            {
                string weaponName = info?.Weapon?.ToString();
                if (weaponName != null)
                {
                    if (weaponName.Contains("bow_hunting"))
                        info.damageTypes.types[10] = LookForDamage(info.ProjectilePrefab.name, true)/2;

                    else if (weaponName.Contains("crossbow"))
                        info.damageTypes.types[10] = LookForDamage(info.ProjectilePrefab.name, false)/2;
                }
            }

            return null;
        }

        float LookForDamage(string name, bool isBow)
        {
            float newDamage = 0;
            switch (name)
            {
                case "arrow_wooden":
                    if (isBow)
                        newDamage = 200f / arrowConfig.bowWooden;
                    else
                        newDamage = 200f / arrowConfig.crossbowWooden;
                    break;

                case "arrow_hv":
                    if (isBow)
                        newDamage = 200f / arrowConfig.bowHV;
                    else
                        newDamage = 200f / arrowConfig.crossbowHV;
                    break;

                case "arrow_bone":
                    if (isBow)
                        newDamage = 200f / arrowConfig.bowBone;
                    else
                        newDamage = 200f / arrowConfig.crossbowBone;
                    break;

            }
            return newDamage;
        }

        [ChatCommand("arrow")]
        void cmdArrow(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, perm) && player.net.connection.authLevel < 2)
            {
                SendReply(player, Lang("DontHavePermission", player.UserIDString));
                return;
            }

            if (args.Length != 2)
                SendReply(player, Lang("CurrentConfig", player.UserIDString, arrowConfig.bowWooden.ToString(),  arrowConfig.bowHV.ToString(), arrowConfig.bowBone.ToString(), arrowConfig.crossbowWooden.ToString(), arrowConfig.crossbowHV.ToString(), arrowConfig.crossbowBone.ToString()));

            else
            {
                int arg;
                if (int.TryParse(args[1], out arg) && arg > 0 && arg <= 5000)
                {
                    switch (args[0].ToLower())
                    {
                        case "bowwooden":
                            arrowConfig.bowWooden = arg;
                            Message(arg.ToString(), "bow wooden", player);
                            break;

                        case "crossbowwooden":
                            arrowConfig.crossbowWooden = arg;
                            Message(arg.ToString(), "crossbow wooden", player);
                            break;

                        case "bowhv":
                            arrowConfig.bowHV = arg;
                            Message(arg.ToString(), "bow hv", player);
                            break;

                        case "crossbowhv":
                            arrowConfig.crossbowHV = arg;
                            Message(arg.ToString(), "crossbow hv", player);
                            break;


                        case "bowbone":
                            arrowConfig.bowBone = arg;
                            Message(arg.ToString(), "bow bone", player);
                            break;

                        case "crossbowbone":
                            arrowConfig.crossbowBone = arg;
                            Message(arg.ToString(), "crossbow bone", player);
                            break;


                        default:
                            SendReply(player, Lang("CurrentConfig", player.UserIDString, arrowConfig.bowWooden.ToString(), arrowConfig.bowHV.ToString(), arrowConfig.bowBone.ToString(), arrowConfig.crossbowWooden.ToString(), arrowConfig.crossbowHV.ToString(), arrowConfig.crossbowBone.ToString()));
                            break;
                    }
                }
                else
                    SendReply(player, Lang("ValidValues", player.UserIDString));
            }
        }

        void Message(string val, string arg, BasePlayer player)
        {
            SaveConfig(arrowConfig);
            SendReply(player, Lang("ConfigChanged", player.UserIDString, val, arg));
        }
    }
}