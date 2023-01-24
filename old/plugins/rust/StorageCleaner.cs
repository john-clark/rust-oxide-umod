using Oxide.Core;
using Oxide.Core.Configuration;

namespace Oxide.Plugins
{
    [Info("StorageCleaner", "k1lly0u", "0.1.1", ResourceId = 2257)]
    class StorageCleaner : RustPlugin
    {        
        StoredData storedData;
        private DynamicConfigFile data;
        
        void Loaded()
        {
            data = Interface.Oxide.DataFileSystem.GetFile("storage_data");
        }
        void OnServerInitialized()
        {
            LoadData();
            Clean();
        }
        
        void Clean()
        {
            var Id = CommunityEntity.ServerInstance.net.ID;
            var last = storedData.InstanceId_1;
            var last2 = storedData.InstanceId_2;
            if (Id == last) return;
            if (last2 != 0U)
            {
                PrintWarning($"Removing left over imagery from folder {last2}");
                FileStorage.server.RemoveEntityNum(last2, 0);
            }
            storedData.InstanceId_2 = last;
            storedData.InstanceId_1 = Id;
            data.WriteObject(storedData);
        }
        
        void LoadData()
        {
            try
            {
                storedData = data.ReadObject<StoredData>();
            }
            catch
            {
                storedData = new StoredData();
            }
        }
        class StoredData
        {
            public uint InstanceId_1 = 0U;
            public uint InstanceId_2 = 0U;
        }        
    }
}