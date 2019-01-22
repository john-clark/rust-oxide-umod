using System;
using System.Collections.Generic;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Entity Data", "Orange", "1.0.3")]
    [Description("Saving specified information about player-owned entities")]
    public class EntityData : RustPlugin
    {
        #region Vars

        private class OEntity
        {
            public ulong ownerID;
            public double spawnTime;
        }

        #endregion

        #region Oxide Hooks

        private void OnServerInitialized()
        {
            LoadData();
        }

        private void Unload()
        {
            SaveData();
        }
        
        private void OnNewSave()
        {
            SaveData();
        }

        private void OnEntitySpawned(BaseEntity entity)
        {
           CheckEntity(entity);
        }

        private void OnEntityKill(BaseEntity entity)
        {
            CheckEntity(entity);
        }

        #endregion

        #region Helpers
        
        private void CheckEntity(BaseEntity entity)
        {
            var owner = entity.OwnerID;
            
            if (!owner.IsSteamId())
            {
                return;
            }
            
            var id = entity.net.ID;

            if (entities.ContainsKey(id))
            {
                entities.Remove(id);
            }
            else
            {
                entities.Add(id, new OEntity{ownerID = entity.OwnerID, spawnTime = Now()});
            }
        }
        
        private double Now()
        {
            return DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
        }

        private int Passed(double a)
        {
            return Convert.ToInt32(Now() - a);
        }

        #endregion
        
        #region Data
        
        private Dictionary<uint, OEntity> entities = new Dictionary<uint, OEntity>();

        private const string filename = "EntityData";

        private void LoadData()
        {
            try
            {
                entities = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<uint, OEntity>>(filename);
            }
            catch (Exception e)
            {
                PrintWarning(e.Message);
            }

            SaveData();
            timer.Every(150f, SaveData);
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(filename, entities);
        }

        #endregion

        #region API

        private int API_GetLifeDuration(uint netID)
        {
            if (!entities.ContainsKey(netID))
            {
                return 0;
            }

            var spawnTime = entities[netID].spawnTime;

            return Passed(spawnTime);
        }

        private double API_GetSpawnTime(uint netID)
        {
            if (!entities.ContainsKey(netID))
            {
                return 0;
            }

            return entities[netID].spawnTime;
        }

        private ulong API_GetOwner(uint netID)
        {
            if (!entities.ContainsKey(netID))
            {
                return 0;
            }

            return entities[netID].ownerID;
        }

        #endregion
    }
}