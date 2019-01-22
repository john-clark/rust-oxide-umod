using Oxide.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Cupboard Logs", "DylanSMR", "1.0.7", ResourceId = 1904)]
    [Description("Logs things related to cupboards.")]

    class CupboardLogs : RustPlugin
    {
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Config.Clear();
            Config["LogCupboardPlacement"] = true;
            Config["CupboardPlacementFile"] = "CupboardLogs/PlacementLogs";
            SaveConfig();
        }

        public List<string> PlacementLogs = new List<string>();

        void Loaded()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"CupboardLog_Spawned","[{time}] - [{player}:{steamid}] : A tool cupboard was placed at [{vector}]."},
            }, this);

            PlacementLogs = Interface.Oxide.DataFileSystem.ReadObject<List<string>>((string)Config["CupboardPlacementFile"]);
        }

        void Save() => Interface.Oxide.DataFileSystem.WriteObject((string)Config["CupboardPlacementFile"], PlacementLogs);
        void Unload() => Save();

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (go == null || plan == null) return;
            BasePlayer player = plan.GetOwnerPlayer();
            BaseEntity entity = go.ToBaseEntity();
            if (entity == null || player == null) return;

            if (entity.PrefabName.ToLower().Contains("cupboard"))
            {
                if ((bool)Config["LogCupboardPlacement"] != true) return;

                PlacementLogs.Add(lang.GetMessage("CupboardLog_Spawned", this).Replace("{time}", DateTime.Now.ToString()).Replace("{player}", player.displayName).Replace("{vector}", entity.transform.position.ToString()).Replace("{steamid}",player.UserIDString));
                Save();
            }
        }
    }
}