using System.Collections.Generic;
using System.Linq;
using Network;
using Newtonsoft.Json;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("Loading Messages", "VVoid", "1.0.4", ResourceId = 2762)]
    [Description("Shows custom texts on loading screen")]
    public class LoadingMessages : RustPlugin
    {
        private readonly Dictionary<ulong, Connection> _clients = new Dictionary<ulong, Connection>();
        private readonly List<ulong> _disconnectedClients = new List<ulong>();

        #region Variables

        private MsgConfig _config;
        private Timer _timer;
        private List<Connection> _queueConnections;
        private MsgCollection _messages, _messagesQueue;
        private static LoadingMessages _instance;

        #endregion

        #region Classes

        private class MsgCollection
        {
            public List<MsgEntry> MessagesList;
            public MsgEntry CurrentMessage;
            //
            private int _messageIndex;
            private float _nextMessageChange;

            public void AdvanceMessage()
            {
                if (!_instance._config.EnableCyclicity || Time.realtimeSinceStartup < _nextMessageChange)
                    return;
                _nextMessageChange = Time.realtimeSinceStartup + _instance._config.CyclicityFreq;
                if (_instance._config.EnableRandomCyclicity)
                    CurrentMessage = PickRandom(MessagesList);
                else
                {
                    CurrentMessage = MessagesList[_messageIndex++];
                    if (_messageIndex >= MessagesList.Count)
                        _messageIndex = 0;
                }
            }

            public void SelectFirst() => CurrentMessage = MessagesList.First();
        }

        #endregion

        #region Config

        private class MsgConfig
        {
            [JsonProperty("Text Display Frequency (Seconds)")]
            public float TimerFreq;
            [JsonProperty("Enable Messages Cyclicity")]
            public bool EnableCyclicity;
            [JsonProperty("Use Random Cyclicity (Instead of sequential)")]
            public bool EnableRandomCyclicity;
            [JsonProperty("Cycle Messages Every ~N Seconds")]
            public float CyclicityFreq;
            [JsonProperty("Messages")]
            public List<MsgEntry> Msgs;
            [JsonProperty("Enable Queue Messages")]
            public bool EnableQueueMessages;
            [JsonProperty("Queue Messages")]
            public List<MsgEntry> QueueMsgs;
            public string QueuePosition;
            [JsonProperty("Last Message (When entering game)")]
            public MsgEntry LastMessage;
        }

        private class MsgEntry
        {
            [JsonProperty("Top Status")]
            public string TopString;
            [JsonProperty("Bottom Status")]
            public string BottomString;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<MsgConfig>();
            _messages = new MsgCollection {MessagesList = _config.Msgs};
            _messagesQueue = new MsgCollection {MessagesList = _config.QueueMsgs};
            if (_config.EnableQueueMessages || _config.QueueMsgs != null)
                return;
            _config.QueueMsgs = new List<MsgEntry>
            {
                new MsgEntry{TopString = "<color=yellow>You're in queue...</color>", BottomString = "<color=lightblue>{AHEAD} players ahead of you.</color>"},
                new MsgEntry{TopString = "<color=lightblue>You're in queue...</color>", BottomString = "<color=yellow>{BEHIND} players behind you.</color>"}
            };
            SaveConfig();
            PrintWarning("Detected probably outdated config. New entries added. Check your config.");
        }

        protected override void LoadDefaultConfig()
        {
            _config = new MsgConfig
            {
                TimerFreq = .30f,
                EnableCyclicity = false,
                EnableRandomCyclicity = false,
                CyclicityFreq = 3.0f,
                Msgs = new List<MsgEntry>
                {
                    new MsgEntry{TopString = "<color=yellow>Welcome to our server!</color>", BottomString = "<color=lightblue>Enjoy your stay.</color>"},
                    new MsgEntry{TopString = "<color=lightblue>Welcome to our server!</color>", BottomString = "<color=yellow>Enjoy your stay.</color>"}
                },
                EnableQueueMessages = false,
                QueueMsgs = new List<MsgEntry>
                {
                    new MsgEntry{TopString = "<color=yellow>You're in queue...</color>", BottomString = "<color=lightblue>{AHEAD} players ahead of you.</color>"},
                    new MsgEntry{TopString = "<color=lightblue>You're in queue...</color>", BottomString = "<color=yellow>{BEHIND} players behind you.</color>"}
                },
                LastMessage = new MsgEntry {TopString = "<color=yellow>Welcome to our server!</color>", BottomString = "<color=green>Entering game...</color>"}
            };
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion

        #region Hooks

        private void Init() => _instance = this;

        private void Unload()
        {
            _instance = null;
            if (_config.EnableQueueMessages)
                ServerMgr.Instance.connectionQueue.nextMessageTime = 0f;
        }

        private void Loaded()
        {
            if (_config?.Msgs == null || _config.Msgs.Count == 0)
            {
                Unsubscribe(nameof(OnUserApprove));
                Unsubscribe(nameof(OnPlayerInit));
                PrintWarning("No loading messages defined! Check your config.");
                return;
            }
            if (_config.EnableCyclicity && _config.Msgs.Count <= 1)
            {
                _config.EnableCyclicity = false;
                PrintWarning("You have message cyclicity enabled, but only 1 message is defined. Check your config.");
            }

            if (_config.EnableQueueMessages && _config.QueueMsgs == null || _config.QueueMsgs.Count == 0)
            {
                _config.EnableQueueMessages = false;
                PrintWarning("You have queue messages enabled, but no queue messages is defined. Check your config.");
            }

            _messages.SelectFirst();
            if (_config.EnableQueueMessages)
                _messagesQueue.SelectFirst();
        }

        private void OnServerInitialized()
        {
            _queueConnections = ServerMgr.Instance.connectionQueue.queue;
        }

        private void OnUserApprove(Connection connection)
        {
            _clients[connection.userid] = connection;
            if (_timer == null)
                _timer = timer.Every(_config.TimerFreq, HandleClients);
            DisplayMessage(connection, GetCurrentMessage());
        }

        private void OnPlayerInit(BasePlayer player)
        {
            _clients.Remove(player.userID);
            DisplayMessage(player.Connection, GetLastMessage() ?? GetCurrentMessage());
        }

        #endregion

        #region Logic

        private void HandleClients()
        {
            if (_clients.Count == 0)
            {
                _timer.Destroy();
                _timer = null;
                return;
            }
            UpdateCurrentMessages();
            if (_config.EnableQueueMessages && ServerMgr.Instance.connectionQueue.Queued > 0)
                SuppressDefaultQueueMessage();
            foreach (var client in _clients.Values)
            {
                if (!client.active)
                {
                    _disconnectedClients.Add(client.userid);
                    continue;
                }

                if (client.state == Connection.State.InQueue)
                {
                    if (!_config.EnableQueueMessages)
                        continue;
                    DisplayQueueMessage(client, GetCurrentQueueMessage());
                    continue;
                }
                DisplayMessage(client, GetCurrentMessage());
            }

            if (_disconnectedClients.Count == 0)
                return;
            _disconnectedClients.ForEach(uid=>_clients.Remove(uid));
            _disconnectedClients.Clear();
        }

        private static void DisplayMessage(Connection con, MsgEntry msgEntry)
        {
            if (!Net.sv.write.Start())
                return;
            Net.sv.write.PacketID(Message.Type.Message);
            Net.sv.write.String(msgEntry.TopString);
            Net.sv.write.String(msgEntry.BottomString);
            Net.sv.write.Send(new SendInfo(con));
        }

        private void DisplayQueueMessage(Connection con, MsgEntry msgEntry)
        {
            if (!Net.sv.write.Start())
                return;
            var ahead = GetQueuePosition(con);
            var behind = (ServerMgr.Instance.connectionQueue.Queued - ahead) - 1;
            Net.sv.write.PacketID(Message.Type.Message);
            Net.sv.write.String(msgEntry.TopString);
            Net.sv.write.String(msgEntry.BottomString.Replace("{AHEAD}", ahead.ToString()).Replace("{BEHIND}", behind.ToString()));
            Net.sv.write.Send(new SendInfo(con));
        }

        #endregion

        #region Utils

        private static T PickRandom<T>(List<T> list) => list[Random.Range(0, list.Count - 1)];
        private MsgEntry GetCurrentMessage() => _messages.CurrentMessage;
        private MsgEntry GetLastMessage() => _config.LastMessage;
        private MsgEntry GetCurrentQueueMessage() => _messagesQueue.CurrentMessage;
        private MsgCollection GetMessagesCollection() => _messages;
        private MsgCollection GetQueueMessagesCollection() => _messagesQueue;
        private void UpdateCurrentMessages()
        {
            GetMessagesCollection().AdvanceMessage();;
            GetQueueMessagesCollection().AdvanceMessage();
        }
        private int GetQueuePosition(Connection con) => _queueConnections.IndexOf(con);
        private void SuppressDefaultQueueMessage() => ServerMgr.Instance.connectionQueue.nextMessageTime = float.MaxValue;

        #endregion
    }
}