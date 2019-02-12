using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Remove Vanilla", "Orange", "1.2.1")]
    [Description("Remove without any useless things")]
    public class RemoveVanilla : RustPlugin
    {
        #region Vars

        private const string permUse = "removevanilla.use";

        private Dictionary<uint, double> entities = new Dictionary<uint, double>();

        #endregion
        
        #region Oxide Hooks

        private void Init()
        {
            permission.RegisterPermission(permUse, this);

            if (config.pickupTime != 0)
            {
                timer.Every(300f, () =>
                {
                    foreach (var entity in entities.ToList())
                    {
                        if (Passed(entity.Value) > config.pickupTime)
                        {
                            entities.Remove(entity.Key);
                        }
                    }
                });
            }
            else
            {
                Unsubscribe("OnEntitySpawned");
            }
        }
        
        private void OnEntitySpawned(BaseCombatEntity entity)
        {
            if (!entity.OwnerID.IsSteamId()) {return;}
            entities.TryAdd(entity.net.ID, 0);
            entities[entity.net.ID] = Now();
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            CheckInput(player, input);
        }

        #endregion

        #region Core

        private void CheckInput(BasePlayer player, InputState input)
        {
            if (!input.WasJustPressed(BUTTON.USE))
            {
                return;
            }
            
            if (!ActiveItemIsHammer(player))
            {
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, permUse))
            {
                return;
            }
            
            var entity = GetLookEntity(player);
            if (entity == null)
            {
                return;
            }

            if (!CanPickup(entity, player))
            {
                return;
            }
            
            if (GiveRefund(entity, player))
            {
                entity.Kill();
            }
        }

        private bool GiveRefund(BaseEntity entity, BasePlayer player)
        {
            var name = entity.ShortPrefabName.Replace(".deployed", "").Replace("_deployed", "");
            if (name == "wall.external.high.wood")
            {
                name = name.Replace(".wood", "");
            }
            
            var item = ItemManager.CreateByName(name);
            if (item != null)
            {
                player.inventory.GiveItem(item);
                return true;
            }
            
            var combat = entity.GetComponent<BaseCombatEntity>();
            if (combat != null)
            {
                var cost = combat.BuildCost();
                if (cost != null)
                {
                    foreach (var value in cost)
                    {
                        var x = ItemManager.Create(value.itemDef, Convert.ToInt32(value.amount));
                        if (x == null) {continue;}
                        player.GiveItem(x);
                    }

                    return true;
                }

                return false;
            }

            return false;
        }
        
        private bool CanPickup(BaseEntity entity, BasePlayer player)
        {
            if (entity.OwnerID == 0 || !player.CanBuild())
            {
                return false;
            }
            
            var name = entity.ShortPrefabName;

            if (config.blocked.Contains(name))
            {
                return false;
            }
            
            if (entity.HasAnySlot())
            {
                return false;
            }

            var container = entity.GetComponent<StorageContainer>();
            if (container != null && container?.inventory.itemList.Count > 0)
            {
                return false;
            }

            var combat = entity.GetComponent<BaseCombatEntity>();
            if (combat != null && combat.SecondsSinceAttacked < 30f)
            {
                return false;
            }

            if (entities.ContainsKey(entity.net.ID))
            {
                return Passed(entities[entity.net.ID]) < config.pickupTime;
            }

            return config.pickupTime == 0;
        }

        private BaseEntity GetLookEntity(BasePlayer player)
        {
            RaycastHit rhit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out rhit)) {return null;}
            var entity = rhit.GetEntity();
            return rhit.distance > 5f ? null : entity;
        }

        private bool ActiveItemIsHammer(BasePlayer player)
        {
            var item = player.GetActiveItem()?.info.shortname ?? "null";
            return item == "hammer";
        }

        #endregion

        #region Configuration
        
        private static ConfigData config;
        
        private class ConfigData
        {    
            [JsonProperty(PropertyName = "1. Timer while pickup will be available (0 to disable)")]
            public int pickupTime;

            [JsonProperty(PropertyName = "2. Blocked entities to remove (shortname of entity, not item):")]
            public List<string> blocked;
        }
        
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                pickupTime = 300,
                blocked = new List<string>
                {
                    "example",
                    "example",
                    "example"
                }
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
        
        #region Time
        
        private double Now()
        {
            return DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
        }
        
        private int Passed(double a)
        {
            return Convert.ToInt32(Now() - a);
        }

        #endregion
    }
}