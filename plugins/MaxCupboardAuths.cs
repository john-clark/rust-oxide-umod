using Oxide.Core;
using Oxide.Core.Configuration;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("MaxCupboardAuths", "redBDGR", "1.0.2", ResourceId = 2368)]
    [Description("Limit how many tool cupboards each player can authorise to")]

    class MaxCupboardAuths : RustPlugin
    {
        private DynamicConfigFile MaxCupboardAuthsData;
        StoredData storedData;

        bool Changed = false;
        Dictionary<string, int> playerInfo = new Dictionary<string, int>();

        class StoredData
        {
            public Dictionary<string, int> MaxCupboardInfo = new Dictionary<string, int>();
        }

        public int MaxAllowedPerPlayer = 5;
        public int MaxAllowedPerCupboard = 5;
        public const string permissionName = "maxcupboardauths.exempt";
        public bool monitorByPlayer = true;
        public bool monitorByCupboard = false;

        void OnServerSave()
        {
            SaveData();
        }

        void SaveData()
        {
            storedData.MaxCupboardInfo = playerInfo;
            MaxCupboardAuthsData.WriteObject(storedData);
        }

        void LoadData()
        {
            try
            {
                storedData = MaxCupboardAuthsData.ReadObject<StoredData>();
                playerInfo = storedData.MaxCupboardInfo;
            }
            catch
            {
                Puts("Failed to load data, creating new file");
                storedData = new StoredData();
            }
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        void Unload() => SaveData();

        void Init()
        {
            LoadVariables();
            permission.RegisterPermission(permissionName, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["Auth Denied PLAYER"] = "You cannot authorise to this cupboard! you have already authorized to the maximum amount of cupboards!",
                ["Auth Denied CUPBOARD"] = "You cannot authorise to this cupboard! there are already the maximum amount of people authed to it!",
            }, this);

            MaxCupboardAuthsData = Interface.Oxide.DataFileSystem.GetFile("MaxCupboardAuths");
            LoadData();
        }

        void LoadVariables()
        {
            MaxAllowedPerPlayer = Convert.ToInt32(GetConfig("Settings", "Max Auths Allowed Per Player", 5));
            MaxAllowedPerCupboard = Convert.ToInt32(GetConfig("Settings", "Max Auths Allowed Per Cupboard", 5));
            monitorByCupboard = Convert.ToBoolean(GetConfig("Settings", "Monitor Auths Per Cupboard", false));
            monitorByPlayer = Convert.ToBoolean(GetConfig("Settings", "Monitor Auths Per Player", true));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        object OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, permissionName)) return null;
            if (monitorByPlayer)
            {
                if (playerInfo.ContainsKey(player.UserIDString))
                {
                    if (playerInfo[player.UserIDString] >= MaxAllowedPerPlayer)
                    {
                        player.ChatMessage(msg("Auth Denied PLAYER", player.UserIDString));
                        return true;
                    }
                    else
                    {
                        playerInfo[player.UserIDString] += 1;
                        return null;
                    }
                }
                else
                {
                    playerInfo.Add(player.UserIDString, 1);
                    return null;
                }
            }

            if (monitorByCupboard)
                if (privilege.authorizedPlayers.Count >= MaxAllowedPerCupboard)
                {
                    player.ChatMessage(msg("Auth Denied CUPBOARD", player.UserIDString));
                    return true;
                }
                else return null;
            return null;
        }

        object OnCupboardDeauthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, permissionName)) return null;
            if (!monitorByPlayer) return null;
            if (playerInfo.ContainsKey(player.UserIDString))
            {
                if (playerInfo[player.UserIDString] == 0)
                    return null;
                playerInfo[player.UserIDString] -= 1;
                return null;
            }
            else return null;
        }
        
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            var cupboard = entity.GetComponent<BuildingPrivlidge>();
            if (cupboard)
                foreach (var player in cupboard.authorizedPlayers)
                {
                    var ppl = BasePlayer.Find(player.userid.ToString());
                    if (ppl)
                        OnCupboardDeauthorize(cupboard, ppl);
                }
        }

        object GetConfig(string menu, string datavalue, object defaultValue)
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

        string msg(string key, string id = null) => lang.GetMessage(key, this, id);
    }
}