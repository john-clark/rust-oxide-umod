using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Building Scanner", "Orange", "1.1.0")]
    [Description("Allow players to scan bases for boxes and etc")]
    public class BuildingScanner : RustPlugin
    {
        #region Vars

        private const string permUse = "scan.use";
        private const string permNoCD = "scan.nocooldown";

        #endregion

        #region Config
        
        private ConfigData config;
        
        private class ConfigData
        {    
            [JsonProperty(PropertyName = "Usage cooldown")]
            public int useCooldown;
        }
        
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                useCooldown = 600
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
        
        #region Oxide Hooks

        private void Init()
        {
            OnStart();
        }

        #endregion
        
        #region Commands

        [ChatCommand("scan")]
        private void CmdScan(BasePlayer player)
        {
            if (!HasPerm(player, permUse))
            {
                message(player, "No perm", permUse);
                return;
            }

            if (!HasPerm(player, permNoCD))
            {
                DoScan(player);
                return;
            }
            
            if (cooldowns.ContainsKey(player.userID))
            {
                var left = config.useCooldown - Passed(cooldowns[player.userID]);

                if (left > 0)
                {
                    message(player, "Cooldown", left.ToString());
                    return;
                }
            }

            if (DoScan(player))
            {
                cooldowns.TryAdd(player.userID, Now());
            }
        }
        
        #endregion

        #region Language

        private Dictionary<string, string> EN = new Dictionary<string, string>
        {
            {"No perm" , "You don't have permission ({0}) to use this command!"},
            {"Result" , "<color=cyan>Inside this building:</color>{0}"},
            {"Cooldown", "Cooldown {0} seconds..."},
            {"Output", "{0} x{1}"}
        };
        
        private void message(BasePlayer player, string key, params object[] args)
        {
            player.ChatMessage(string.Format(lang.GetMessage(key, this, player.UserIDString), args));
        }

        #endregion

        #region Helpers

        private void OnStart()
        {
            permission.RegisterPermission(permNoCD, this);
            permission.RegisterPermission(permUse, this);
            lang.RegisterMessages(EN, this);
        }

        private bool HasPerm(BasePlayer p, string perm)
        {
            return p.IsAdmin || permission.UserHasPermission(p.UserIDString, perm);
        }
        
        private Dictionary<ulong, double> cooldowns = new Dictionary<ulong, double>();
        
        private double Now()
        {
            return DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
        }

        private double Passed(double value)
        {
            return Convert.ToInt32(Now() - value);
        }
        
        private bool DoScan(BasePlayer p)
        {
            RaycastHit rhit;

            if (!Physics.Raycast(p.eyes.HeadRay(), out rhit, 10f, LayerMask.GetMask("Construction"))) {return false;}
            if (rhit.GetEntity() == null) {return false;}
            var entity = rhit.GetEntity();
            var privelege = entity.GetBuildingPrivilege();
            if (privelege == null) {return false;}
            var building = privelege.GetBuilding();
            if (building == null) {return false;}
            if (!building.HasDecayEntities()) {return false;}
            var result = new Dictionary<string, List<uint>>();

            foreach (var ent in building.decayEntities)
            {
                if (ent.OwnerID == 0) continue;
                if (ent is BuildingBlock) continue;

                result.TryAdd(ent.ShortPrefabName, new List<uint>());

                if (!result[ent.ShortPrefabName].Contains(ent.net.ID))
                {
                    result[ent.ShortPrefabName].Add(ent.net.ID);
                }
            }

            var res = "\n";

            foreach (var r in result)
            {
                res += string.Format(lang.GetMessage("Object", this), r.Key, r.Value.Count) + "\n";
            }

            message(p, "Result", res);

            return true;
        }

        #endregion
    }
}