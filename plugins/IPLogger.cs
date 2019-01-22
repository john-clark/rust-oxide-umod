using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("IP Logger", "redBDGR", "1.0.0")]
    [Description("Logs all player IP addresses for easy comparison")]
    class IPLogger : CovalencePlugin
    {
        bool Changed = false;

        #region Data

        DynamicConfigFile IPInfo;
        StoredData storedData;

        Dictionary<string, List<string>> PlayerInfo = new Dictionary<string, List<string>>();

        class StoredData
        {
            public Dictionary<string, List<string>> IPLog = new Dictionary<string, List<string>>();
        }

        void Init()
        {
            IPInfo = Interface.Oxide.DataFileSystem.GetFile(Name);
            LoadData();
        }

        void LoadData()
        {
            try
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(this.Title);
                PlayerInfo = storedData.IPLog;
            }
            catch
            {
                Puts("Failed to load data, creating new file");
                storedData = new StoredData();
            }
        }

        void SaveData()
        {
            storedData.IPLog = PlayerInfo;
            IPInfo.WriteObject(storedData);
        }

        #endregion

        #region Loading / Saving

        void Unload() => SaveData();
        void OnServerSave() => SaveData();

        #endregion

        void OnUserConnected(IPlayer player)
        {
            string IP = player.Address;
            int index = IP.IndexOf(":");
            if (index > 0)
                    IP = IP.Substring(0, index);
            if (PlayerInfo.ContainsKey(player.Id))
            {
                if (PlayerInfo[player.Id].Contains(IP))
                    return;
                else
                    PlayerInfo[player.Id].Add(IP);
            }
            else
            {
                PlayerInfo.Add(player.Id, new List<string>());
                PlayerInfo[player.Id].Add(IP);
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
    }
}
