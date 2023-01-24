using UnityEngine;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Beds Limit", "Bruno Puccio", "1.1")]
    [Description("allows the admin to limit the amount of beds that are placed per base")]
    class BedsLimit : RustPlugin
    {
        const string perm = "bedslimit.allowed";
        private ConfigData bedsConfig;

        private class ConfigData
        {
            public int radius, maxplayers;
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            ConfigData bedsConfig = new ConfigData
            {
                radius = 35,
                maxplayers = 3
            };
            SaveConfig(bedsConfig);
        }

        private void LoadConfigVariables() => bedsConfig = Config.ReadObject<ConfigData>();

        void SaveConfig(ConfigData bedsConfig) => Config.WriteObject(bedsConfig, true);

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
                ["ValidValues"] = "valid values from:   {0}   to   {1}",
                ["DontHavePermission"] = "<color=#00ffffff>[Beds Limit]</color> You do not have permission to use this command",
                ["CurrentConfig"] = "<color=#00ffffff>[Beds Limit]</color>\n\n/bedslimit maxplayers: {0} \n/bedslimit radius: {1}",
                ["ConfigChanged"] = "{0} set to {1}",
                ["CantPlaceBed"] = "you can't place a bed here, there are too many beds around",
                ["CantGiveBed"] = "you can't share that bed, max limit reached"
            }, this);
        }



        void OnEntityBuilt(Planner plan, GameObject go)
        {
            BaseNetworkable baseNet = go.GetComponent<BaseNetworkable>();
            string shortName = baseNet.ShortPrefabName;
            if (shortName.Contains("sleepingbag") || shortName.Contains("bed"))
            {
                if (FindBedsNearby(go.GetComponent<SleepingBag>()) > bedsConfig.maxplayers)
                {
                    BasePlayer baseP = plan.GetOwnerPlayer();
                    SendReply(baseP, Lang("CantPlaceBed", baseP.UserIDString));
                    

                    if (shortName.Contains("sleepingbag"))
                        timer.Once(1f, () => { baseP.inventory.GiveItem(ItemManager.CreateByItemID(1253290621)); });
                    else
                        timer.Once(1f, () => { baseP.inventory.GiveItem(ItemManager.CreateByItemID(97409)); });

                    baseNet.Kill();
                }
            }
        }

        object CanAssignBed(SleepingBag bag, BasePlayer player, ulong targetPlayerId)
        {

            if (FindBedsNearby(0, player.transform.position, targetPlayerId) > 1)
                return null;

            if (FindBedsNearby(0, player.transform.position, player.userID) > 1 && (FindBedsNearby(bag) + 1) > bedsConfig.maxplayers)
            {
                SendReply(player, Lang("CantGiveBed", player.UserIDString));
                bag.deployerUserID = player.userID;
                return true;
            }
            return null;
        }

        int FindBedsNearby(int i, Vector3 pos, ulong targetPlayerId)
        {
            
            List<SleepingBag> list = LookForBeds(pos), finalList = new List<SleepingBag>();

            foreach (SleepingBag baseEnt in list)
            {
                bool bandera = false;
                foreach (BaseEntity baseEnt2 in finalList)
                {
                    if (baseEnt.transform.position == baseEnt2.transform.position)
                        bandera = true;
                }
                if (!bandera)
                    finalList.Add(baseEnt);
            }

            foreach (BaseEntity entityFound in finalList)
            {
                if (entityFound.GetComponent<SleepingBag>().deployerUserID == targetPlayerId)
                    i++;
            }

            return i;
        }

        int FindBedsNearby(SleepingBag bag)
        {
            List<SleepingBag> list = LookForBeds(bag.transform.position), finalList = new List<SleepingBag>();

            foreach (SleepingBag baseEnt in list)
            {
                bool bandera = false;
                foreach (BaseEntity baseEnt2 in finalList)
                {
                    if (baseEnt.transform.position == baseEnt2.transform.position || baseEnt.GetComponent<SleepingBag>().deployerUserID == baseEnt2.GetComponent<SleepingBag>().deployerUserID)
                        bandera = true;
                }
                if (!bandera)
                    finalList.Add(baseEnt);
            }

            return finalList.Count;
        }

        List<SleepingBag> LookForBeds(Vector3 pos)
        {
            List<SleepingBag> list = new List<SleepingBag>();
            Vis.Entities<SleepingBag>(pos, bedsConfig.radius, list);
            Vis.Entities<SleepingBag>(pos - new Vector3(0, bedsConfig.radius / 2, 0), bedsConfig.radius, list);
            Vis.Entities<SleepingBag>(pos + new Vector3(0, bedsConfig.radius / 2, 0), bedsConfig.radius, list);
            Vis.Entities<SleepingBag>(pos - new Vector3(0, bedsConfig.radius, 0), bedsConfig.radius, list);
            Vis.Entities<SleepingBag>(pos + new Vector3(0, bedsConfig.radius, 0), bedsConfig.radius, list);
            return list;
        }


        [ChatCommand("bedslimit")]
        void VanillaCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, perm) && player.net.connection.authLevel < 2)
            {
                SendReply(player, Lang("DontHavePermission", player.UserIDString));
                return;
            }

            if (args.Length != 2)
                SendReply(player, Lang("CurrentConfig", player.UserIDString, bedsConfig.maxplayers.ToString(), bedsConfig.radius.ToString()));


            else
            {
                switch (args[0])
                {
                    case "maxplayers":
                        int beds;
                        if ((int.TryParse(args[1], out beds)) && beds >= 1 && beds <= 20)
                        {
                            bedsConfig.maxplayers = beds;
                            SaveConfig(bedsConfig);
                            SendReply(player, Lang("ConfigChanged", player.UserIDString, "maxplayers", beds.ToString()));
                        }
                        else
                            SendReply(player, Lang("ValidValues", player.UserIDString, 1, 20));
                        break;

                    case "radius":
                        int radius;
                        if ((int.TryParse(args[1], out radius)) && radius >= 5 && radius <= 100)
                        {
                            bedsConfig.radius = radius;
                            SaveConfig(bedsConfig);
                            SendReply(player, Lang("ConfigChanged", player.UserIDString, "radius", radius.ToString()));
                        }
                        else
                            SendReply(player, Lang("ValidValues", player.UserIDString, 5, 100));
                        break;

                    default:
                        SendReply(player, Lang("CurrentConfig", player.UserIDString, bedsConfig.maxplayers.ToString(), bedsConfig.radius.ToString()));
                        break;
                }
            }
        }
    }
}