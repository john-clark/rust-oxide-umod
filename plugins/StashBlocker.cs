using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Stash Blocker", "Orange", "1.0.0")]
    [Description("Controls stashes placement")]
    public class StashBlocker : RustPlugin
    {
        #region Oxide Hooks

        private void Init()
        {
            lang.RegisterMessages(EN, this);
        }

        private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            return CheckBuild(planner, prefab, target);
        }

        #endregion

        #region Configuration

        private static ConfigData config;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "1. Block placing stashes (globally)")]
            public bool global;

            [JsonProperty(PropertyName = "2. Radius of allowed distance between stashes and any entities (set to 0 to disable)")]
            public float entities;
        }

        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                global = false,
                entities = 5f
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintError("Configuration file is corrupt(or not exists), creating new one!");
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region Language

        private Dictionary<string, string> EN = new Dictionary<string, string>
        {
            {"Global", "Stashes placing is blocked!"},
            {"Near Stashes", "You can't place entities near stashes!"},
            {"Near Entities", "You can't place stashes near buildings!"}
        };

        private void message(BasePlayer player, string key, params object[] args)
        {
            var message = string.Format(lang.GetMessage(key, this, player.UserIDString), args);
            player.ChatMessage(message);
        }

        #endregion

        #region Core

        private object CheckBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            var player = planner.GetOwnerPlayer();
            if (player == null)
            {
                return null;
            }

            var name = prefab.fullName;
            var position = target.entity?.transform.position ?? target.position;
            var check = config.entities > 0.001f;
            
            if (IsStash(name))
            {
                if (config.global)
                {
                    message(player, "Global");
                    return false;
                }
                
                if (check && HasBuildingsNearby(position))
                {
                    message(player, "Near Entities");
                    return false;
                }
            }
            
            if (check && HasStashesNearby(position))
            {
                message(player, "Near Stashes");
                return false;
            }

            return null;
        }

        private bool IsStash(string name)
        {
            return name.ToLower().Contains("stash");
        }

        private bool HasStashesNearby(Vector3 position)
        {
            var list = new List<StashContainer>();
            Vis.Entities(position, config.entities, list);
            return list.Count > 0;
        }

        private bool HasBuildingsNearby(Vector3 position)
        {
            var list = new List<BuildingBlock>();
            Vis.Entities(position, 5f, list);
            return list.Count > 0;
        }

        #endregion
    }
}