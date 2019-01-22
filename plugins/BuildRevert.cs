using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BuildRevert", "nivex", "1.1.0")]
    [Description("Prevent building in blocked area.")]
    public class BuildRevert : RustPlugin
    {
        class ConstructionSetting
        {
            public bool Default;
            public bool CanBypass;
        }

        Dictionary<Construction, ConstructionSetting> constructions = new Dictionary<Construction, ConstructionSetting>();

        void Init() => Unsubscribe(nameof(CanBuild));
        void OnServerInitialized() => LoadVariables();

        void Unload()
        {
            foreach (var entry in constructions)
            {
                if (entry.Key.canBypassBuildingPermission != entry.Value.Default)
                {
                    entry.Key.canBypassBuildingPermission = entry.Value.Default;
                }
            }
        }

        object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            if (!constructions.ContainsKey(prefab) || constructions[prefab].CanBypass || permission.UserHasPermission(target.player.UserIDString, bypassPerm))
                return null;

            var buildPos = target.entity && target.entity.transform && target.socket ? target.GetWorldPosition() : target.position;

            if (target.player.IsBuildingBlocked(new OBB(buildPos, default(Quaternion), default(Bounds))))
            {
                target.player.ChatMessage(msg("Building is blocked!", target.player.UserIDString));
                return false;
            }

            return null;
        }

        #region Config
        bool Changed;
        string bypassPerm;
        bool usePerm;

        void LoadVariables()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Building is blocked!"] = "<color=red>Building is blocked!</color>",
            }, this);

            bypassPerm = Convert.ToString(GetConfig("Settings", "Bypass Permission Name", "buildrevert.bypass"));
            usePerm = Convert.ToBoolean(GetConfig("Settings", "Use Bypass Permission (set false to increase performance)", true)) && !string.IsNullOrEmpty(bypassPerm);

            var pooledStrings = GameManifest.Current.pooledStrings.ToDictionary(p => p.str.ToLower(), p => p.hash); // Thanks Fuji
            
            foreach (string str in GameManifest.Current.entities)
            {
                var construction = PrefabAttribute.server.Find<Construction>(pooledStrings[str.ToLower()]);
                {
                    if (construction != null && (construction.grades[(int)BuildingGrade.Enum.Twigs] != null || construction.hierachyName.Contains("ladder")))
                    {
                        constructions[construction] = new ConstructionSetting
                        {
                            Default = construction.canBypassBuildingPermission,
                            CanBypass = Convert.ToBoolean(GetConfig("Constructions", string.Format("Allow {0}", construction.hierachyName), true)),
                        };

                        if (!usePerm)
                        {
                            construction.canBypassBuildingPermission = constructions[construction].CanBypass;
                        }
                    }
                }
            }

            if (usePerm)
            {
                permission.RegisterPermission(bypassPerm, this);

                if (constructions.Values.Any(x => !x.CanBypass))
                {
                    Subscribe(nameof(CanBuild));
                }
            }

            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Config.Clear();
            LoadVariables();
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
        #endregion
    }
}