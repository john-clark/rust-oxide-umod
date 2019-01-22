
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("QueueHolder", "Jake_Rich", 1.0)]
    [Description("Saves your position in queue if you disconnect")]

    public class QueueHolder : RustPlugin
    {
        public static QueueHolder _plugin { get; set; }

        public Dictionary<ulong, QueueData> queueData { get; set; } = new Dictionary<ulong, QueueData>();

        public const float timeToHoldSpot = 5; //Minutes

        public class QueueData
        {
            public ulong userID { get; set; }
            public DateTime disconnectTime { get; set; } = new DateTime();
            public Timer _timer { get; set; }

            public QueueData()
            {

            }

            public QueueData(ulong id)
            {
                userID = id;
            }
        }

        void Loaded()
        {
            _plugin = this;
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            var data = GetQueueData(player);
            data.disconnectTime = DateTime.Now;
            data._timer = timer.In(60 * timeToHoldSpot, () => { queueData.Remove(player.userID); });
        }

        public QueueData GetQueueData(BasePlayer player)
        {
            return GetQueueData(player.userID);
        }

        public QueueData GetQueueData(ulong player)
        {
            QueueData data;
            if (!queueData.TryGetValue(player, out data))
            {
                data = new QueueData(player);
                queueData[player] = data;
            }
            return data;
        }

        object CanBypassQueue(Network.Connection connection)
        {
            if (connection.authLevel > 0)
            {
                return true;
            }
            var data = GetQueueData(connection.userid);
            data._timer?.Destroy();
            if (DateTime.Now.Subtract(data.disconnectTime).TotalSeconds < timeToHoldSpot * 60f)
            {
                return true;
            }
            return null;
        }
    }
}


