using Oxide.Core;
using System.Collections.Generic;
using System.Linq;
using System;
using Oxide.Core.Libraries;
using UnityEngine;

using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("Stash Traps", "Jacob", "1.0.1", ResourceId = 2735)]
    public class StashTraps : RustPlugin
    {
        public List<ulong> StashUsers = new List<ulong>();

        private static StashTraps _instance;

        private Configuration _configuration;

        private Data _data;

        private const string MessageJson = @"{
            ""content"": ""Stash trap alert!"",
            ""embeds"":[{
                    ""fields"": [
                    {
                        ""name"": ""Player"",
                        ""value"": ""{player}"",
                        ""inline"": true
                    },
                    {
                        ""name"": ""Nearby players"",
                        ""value"": ""{nearbyPlayers}"",
                        ""inline"": false
                    },
                    {
                        ""name"": ""Position"",
                        ""value"": ""{position}"",
                        ""inline"": true
                    },
                    {
                        ""name"": ""Network ID"",
                        ""value"": ""{networkID}"",
                        ""inline"": true
                    }
                ]
            }]
        }";

        protected override void LoadDefaultConfig() => PrintWarning("Generating new configuration file...");

        protected override void LoadDefaultMessages() => lang.RegisterMessages(new Dictionary<string, string>
        {
            {"ArgumentError", "Error, the amount argument must be an integer."},
            {"PermissionError", "Error, you lack permission to use that command. If you believe this is an error please contact an administrator."},
            {"StashAlertAlone", "[#ADD8E6]{0}[/#] discovered a stash at [#ADD8E6]{1}[/#] with the network ID [#ADD8E6]{2}[/#]."},
            {"StashAlertTogether", "[#ADD8E6]{0}[/#] along with [#ADD8E6]{1}[/#] discovered a stash at [#ADD8E6]{2}[/#] with the network ID [#ADD8E6]{3}[/#]."},
            {"StashGive", "You were sucessfully given [#ADD8E6]{0}[/#] stashes."},
            {"StashLogAlone", "[{0}] {1} at {2}."},
            {"StashLogTogether", "[{0}] {1} along with {2} at {3}."},
            {"StashPlaced", "Stash sucessfully added to watch list with network ID [#ADD8E6]{0}[/#]."},
            {"StashStatus", "Stash placement mode sucessfully [#ADD8E6]{0}[/#]."}
        }, this);

        private string FormatCoordinates(Vector3 position) =>
            $"{Math.Ceiling(position.x)}, {Math.Ceiling(position.y)}, {Math.Ceiling(position.z)}";

        private string FormatNearbyPlayers(BasePlayer player)
        {
            var players = Physics.OverlapSphere(player.transform.position, 10, LayerMask.GetMask("Player (Server)"))
                .Select(x => x.GetComponentInParent<BasePlayer>())
                .Where(x => x != null && x != player && !HasPermission(x));
            return players.Any() ? players.Select(x => $"{x.displayName} ({x.userID})").ToSentence().Replace(".", "") : "";
        }

        private bool HasPermission(BasePlayer player, string permission = "stashtraps.admin") =>
            this.permission.UserHasPermission(player.UserIDString, permission) || player.IsAdmin;

        private void MessagePlayer(BasePlayer player, string key, params object[] args) => PrintToChat(player,
            covalence.FormatText(lang.GetMessage(key, this, player.UserIDString)), args);

        private void CanSeeStash(StashContainer stash, BasePlayer player)
        {
            if (!_data.Stashes.Contains(stash.net.ID) || HasPermission(player))
                return;

            _data.Stashes.Remove(stash.net.ID);
            _data.SaveData(_data.Stashes, "Stashes");
            var nearbyPlayers = FormatNearbyPlayers(player);
            var body = MessageJson
                .Replace("{player}", $"{player.displayName} ({player.UserIDString})")
                .Replace("{nearbyPlayers}", string.IsNullOrEmpty(nearbyPlayers) ? "None" : nearbyPlayers)
                .Replace("{position}", FormatCoordinates(stash.transform.position))
                .Replace("{networkID}", stash.net.ID.ToString());

            webrequest.Enqueue(_configuration.WebHookURL, body, (code, response) =>
            {
                if (code != 204)
                    PrintWarning($"Warning, the Discord API responded with code {code}.");
            }, this, RequestMethod.POST);

            if (string.IsNullOrEmpty(nearbyPlayers))
            {
                LogToFile("Stashes", string.Format(lang.GetMessage("StashLogAlone", this), DateTime.Now.ToShortDateString(), $"{player.displayName} ({player.UserIDString})", FormatCoordinates(stash.transform.position)), this, false);
                foreach (var target in BasePlayer.activePlayerList.Where(x => HasPermission(x)))
                    MessagePlayer(target, "StashAlertAlone", $"{player.displayName} ({player.UserIDString})", FormatCoordinates(stash.transform.position), stash.net.ID);

                return;
            }

            LogToFile("Stashes", string.Format(lang.GetMessage("StashLogTogether", this), DateTime.Now.ToShortDateString(), $"{player.displayName} ({player.UserIDString})", nearbyPlayers, FormatCoordinates(stash.transform.position)), this, false);
            foreach (var target in BasePlayer.activePlayerList.Where(x => HasPermission(x)))
                MessagePlayer(target, "StashAlertTogether", $"{player.displayName} ({player.UserIDString})", nearbyPlayers, FormatCoordinates(stash.transform.position), stash.net.ID);
        }

        private void Init()
        {
            _instance = this;
            _configuration = new Configuration();
            _data = new Data();

            permission.RegisterPermission("stashtraps.admin", this);
        }

        private void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            var stash = gameObject.GetComponent<StashContainer>();
            if (stash == null)
                return;

            var player = planner.GetOwnerPlayer();
            if (player == null || !HasPermission(player) || !StashUsers.Contains(player.userID))
                return;

            MessagePlayer(player, "StashPlaced", stash.net.ID);
            stash.SetFlag(BaseEntity.Flags.Reserved5, true);
            for (var i = 0; i < Random.Range(1, 6); i++)
            {
                var keyValuePair = _configuration.FillerItems.ElementAt(Random.Range(0, _configuration.FillerItems.Count));
                var item = ItemManager.CreateByName(keyValuePair.Key, Random.Range(1, Convert.ToInt32(keyValuePair.Value)));
                item.MoveToContainer(stash.inventory);
            }

            _data.Stashes.Add(stash.net.ID);
            _data.SaveData(_data.Stashes, "Stashes");
        }

        private void OnNewSave()
        {
            _data.Stashes.Clear();
            _data.SaveData(_data.Stashes, "Stashes");
        }

        [ChatCommand("stash")]
        private void StashCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player))
            {
                MessagePlayer(player, "PermissionError");
                return;
            }

            if (args.Length != 0)
            {
                int amount;
                if (!int.TryParse(args[0], out amount))
                {
                    MessagePlayer(player, "ArgumentError");
                    return;
                }

                MessagePlayer(player, "StashGive", amount);
                var item = ItemManager.CreateByName("stash.small", amount);
                player.inventory.GiveItem(item);
                if (!player.inventory.AllItems().Contains(item))
                    item.Drop(player.transform.position, Vector3.zero);

                return;
            }

            if (StashUsers.Contains(player.userID))
                StashUsers.Remove(player.userID);
            else
                StashUsers.Add(player.userID);

            MessagePlayer(player, "StashStatus", StashUsers.Contains(player.userID) ? "enabled" : "disabled");
            _data.SaveData(StashUsers, "stashUsers");
        }

        private class Configuration
        {
            public Dictionary<string, object> FillerItems = new Dictionary<string, object>
            {
                {"fat.animal", 100},
                {"bone.fragments", 100},
                {"charcoal", 100},
                {"cloth", 100},
                {"gunpowder", 100},
                {"metal.refined", 10},
                {"hq.metal.ore", 10},
                {"leather", 100},
                {"lowgradefuel", 100},
                {"metal.fragments", 100},
                {"metal.ore", 100},
                {"scrap", 10},
                {"stones", 100},
                {"sulfur", 100},
                {"sulfur.ore", 100},
                {"wood", 100}
            };

            public string WebHookURL = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";

            public Configuration()
            {
                GetConfig(ref FillerItems, "Stashes", "Filler items");
                GetConfig(ref WebHookURL, "Discord", "Web hook URL");
                _instance.SaveConfig();
            }

            private void GetConfig<T>(ref T variable, params string[] path)
            {
                if (path.Length == 0)
                    return;

                if (_instance.Config.Get(path) == null)
                {
                    SetConfig(ref variable, path);
                    _instance.PrintWarning($"Added field to config: {string.Join("/", path)}");
                }

                variable = (T)Convert.ChangeType(_instance.Config.Get(path), typeof(T));
            }

            private void SetConfig<T>(ref T variable, params string[] path) => _instance.Config.Set(path.Concat(new object[] { variable }).ToArray());
        }

        private class Data
        {
            public List<ulong> Stashes = new List<ulong>();

            public Data()
            {
                GetData(ref Stashes, "Stashes");
            }

            private void GetData<T>(ref T data, string filename) => data = Interface.Oxide.DataFileSystem.ReadObject<T>($"{_instance.Name}/{filename}");

            public void SaveData<T>(T data, string filename) => Interface.Oxide.DataFileSystem.WriteObject($"{_instance.Name}/{filename}", data);
        }
    }
}