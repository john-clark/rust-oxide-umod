using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Barrel Points", "redBDGR", "2.0.8")]
    [Description("Gives players extra rewards for destroying barrels")]
    public class BarrelPoints : RustPlugin
    {
        [PluginReference]
        private Plugin Economics, ServerRewards;

        private static Dictionary<string, object> _PermissionDic()
        {
            var x = new Dictionary<string, object>
            {
                {"barrelpoints.default", 2.0},
                {"barrelpoints.vip", 5.0}
            };
            return x;
        }

        private readonly Dictionary<string, int> playerInfo = new Dictionary<string, int>();
        private readonly List<uint> crateCache = new List<uint>();

        private Dictionary<string, object> permissionList;

        private bool changed;
        private bool useEconomy = true;
        private bool useServerRewards;
        private bool resetBarrelsOnDeath = true;
        private bool sendNotificationMessage = true;
        private bool useCrates;
        private bool useBarrels = true;
        private int givePointsEvery = 1;

        private void OnServerInitialized()
        {
            LoadVariables();

            foreach (var entry in permissionList)
                permission.RegisterPermission(entry.Key, this);

            if (useEconomy && !Economics)
            {
                PrintError("Economics was not found! Disabling the \"Use Economics\" setting");
                useEconomy = false;
            }

            if (useServerRewards && !ServerRewards)
            {
                PrintError("ServerRewards was not found! Disabling the \"Use ServerRewards\" setting");
                useServerRewards = false;
            }
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {
            permissionList = (Dictionary<string, object>)GetConfig("Settings", "Permission List", _PermissionDic());
            useEconomy = Convert.ToBoolean(GetConfig("Settings", "Use Economics", true));
            useServerRewards = Convert.ToBoolean(GetConfig("Settings", "Use ServerRewards", false));
            sendNotificationMessage = Convert.ToBoolean(GetConfig("Settings", "Send Notification Message", true));
            givePointsEvery = Convert.ToInt32(GetConfig("Settings", "Give Points Every x Barrels", 1));
            resetBarrelsOnDeath = Convert.ToBoolean(GetConfig("Settings", "Reset Barrel Count on Death", true));
            useBarrels = Convert.ToBoolean(GetConfig("Settings", "Give Points For Barrels", true));
            useCrates = Convert.ToBoolean(GetConfig("Settings", "Give Points For Crates", false));

            if (!changed)
                return;

            SaveConfig();
            changed = false;
        }

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Economy Notice (Barrel)"] = "You received ${0} for destroying a barrel!",
                ["Economy Notice (Crate)"] = "You received ${0} for looting a crate!",
                ["RP Notice (Barrel)"] = "You received {0} RP for destroying a barrel!",
                ["RP Notice (Crate)"] = "You received {0} RP for looting a crate!"
            }, this);
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!useBarrels || info?.Initiator == null)
                return;

            if (!entity.ShortPrefabName.StartsWith("loot-barrel") && !entity.ShortPrefabName.StartsWith("loot_barrel") && entity.ShortPrefabName != "oil_barrel")
                return;

            BasePlayer player = info.InitiatorPlayer;
            if (player == null || !player.IsValid())
                return;

            string userPermission = GetPermissionName(player);
            if (userPermission == null)
                return;

            // Checking for number of barrels hit
            if (!playerInfo.ContainsKey(player.UserIDString))
                playerInfo.Add(player.UserIDString, 0);

            if (playerInfo[player.UserIDString] == givePointsEvery - 1)
            {
                // Section that gives the player their money
                if (useEconomy && Economics)
                {
                    Economics.Call("Deposit", player.userID, Convert.ToDouble(permissionList[userPermission]));
                    if (sendNotificationMessage)
                        player.ChatMessage(string.Format(Msg("Economy Notice (Barrel)", player.UserIDString), permissionList[userPermission]));
                }
                if (useServerRewards && ServerRewards)
                {
                    ServerRewards.Call("AddPoints", player.userID, Convert.ToInt32(permissionList[userPermission]));
                    if (sendNotificationMessage)
                        player.ChatMessage(string.Format(Msg("RP Notice (Barrel)", player.UserIDString), permissionList[userPermission]));
                }
                playerInfo[player.UserIDString] = 0;
            }
            else
                playerInfo[player.UserIDString]++;
        }

        private void OnEntityKill(BaseNetworkable entity)
        {
            if (!useCrates || entity == null)
                return;

            if (!entity.ShortPrefabName.Contains("crate_") && entity.ShortPrefabName != "heli_crate")
                return;

            if (crateCache.Contains(entity.net.ID))
                crateCache.Remove(entity.net.ID);
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (!useCrates)
                return;

            if (!entity.ShortPrefabName.Contains("crate_") && entity.ShortPrefabName != "heli_crate")
                return;

            if (crateCache.Contains(entity.net.ID))
                return;

            crateCache.Add(entity.net.ID);
            string userPermission = GetPermissionName(player);
            if (userPermission == null)
                return;

            if (useEconomy && Economics)
            {
                Economics.Call("Deposit", player.userID, Convert.ToDouble(permissionList[userPermission]));
                if (sendNotificationMessage)
                    player.ChatMessage(string.Format(Msg("Economy Notice (Crate)", player.UserIDString), permissionList[userPermission]));
            }
            if (useServerRewards)
            {
                ServerRewards.Call("AddPoints", player.userID, Convert.ToInt32(permissionList[userPermission]));
                if (sendNotificationMessage)
                    player.ChatMessage(string.Format(Msg("RP Notice (Crate)", player.UserIDString), permissionList[userPermission]));
            }
        }

        private void OnPlayerDie(BasePlayer player)
        {
            if (!resetBarrelsOnDeath)
                return;

            if (playerInfo.ContainsKey(player.UserIDString))
                playerInfo[player.UserIDString] = 0;
        }

        private string GetPermissionName(BasePlayer player)
        {
            KeyValuePair<string, int> _perms = new KeyValuePair<string, int>(null, 0);
            Dictionary<string, int> perms = permissionList.Where(entry => permission.UserHasPermission(player.UserIDString, entry.Key))
                .ToDictionary(entry => entry.Key, entry => Convert.ToInt32(entry.Value));
            foreach (var entry in perms)
                if (Convert.ToInt32(entry.Value) > _perms.Value)
                    _perms = new KeyValuePair<string, int>(entry.Key, Convert.ToInt32(entry.Value));
            return _perms.Key;
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = (Dictionary<string, object>)Config[menu];
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                changed = true;
            }
            return value;
        }

        private string Msg(string key, string id = null) => lang.GetMessage(key, this, id);
    }
}
