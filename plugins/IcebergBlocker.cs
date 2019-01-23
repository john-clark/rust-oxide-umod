using Oxide.Core.Libraries;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Iceberg Blocker", "Slut", "1.1.2")]
    class IcebergBlocker : RustPlugin
    {
        private void Loaded()
        {
            LoadConfiguration();
            permission.RegisterPermission(adminPermission, this);
        }
        private void LoadConfiguration()
        {
            CheckCfg<bool>("Block Building onto Icesheet", ref blockIceSheet);
            CheckCfg<List<object>>("List of prefabs to blacklist/whitelist", ref listOfPrefabs);
            CheckCfg<bool>("Treat list of prefabs as blacklist", ref asBlacklist);
            SaveConfig();
        }
        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }
        private void LoadDefaultConfig()
        {
            Puts("Generating new configuration.");
        }
        private bool asBlacklist = false;
        private bool blockIceSheet = true;
        private List<object> listOfPrefabs = new List<object>()
        {
            "prefab.fullname",
        };
        private string adminPermission = "icebergblocker.admin";

        private object CanBuild(Planner plan, Construction prefab)
        {
            var player = plan.GetOwnerPlayer();
            Vector3 pos = plan.GetEstimatedWorldPosition();
            if (player != null && !permission.UserHasPermission(player.UserIDString, adminPermission))
            {
                List<Collider> list = new List<Collider>();
                Vis.Colliders(pos, 5f, list);
                for (int x = 0; x < list.Count; x++)
                {
                    if ((list[x].name.Contains("iceberg") | list[x].name.Contains("ice_berg")) || (blockIceSheet && (list[x].name.Contains("icesheet") | list[x].name.Contains("ice_sheet"))))
                    {
                        if (asBlacklist && listOfPrefabs.Contains(prefab.fullName))
                        {
                            return false;
                        }
                        else if (!asBlacklist && listOfPrefabs.Contains(prefab.fullName))
                        {
                            return null;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }
            return null;
        }
    }
}
