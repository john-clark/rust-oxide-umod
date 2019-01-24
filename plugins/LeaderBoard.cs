using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("LeaderBoard", "open_mailbox", "0.1")]
    [Description("A leaderboard for encouraging competition in various categories.")]
    class LeaderBoard : RustPlugin
    {
        #region definitions
        private class StoredData
        {
            public const string FILE_NAME = "leaderboard_data";

            public List<PlayerInfo> Players = new List<PlayerInfo>();

            public StoredData() { }
        }

        private class PlayerInfo
        {
            public ulong  UserId { get; set; }
            public string Name   { get; set; }

            public int Kills  { get; set; }
            public int Deaths { get; set; }
            public int Streak { get; set; }

            public PlayerInfo() { }

            public PlayerInfo(BasePlayer player)
            {
                UserId = player.userID;
                Name   = player.displayName;
                Kills  = 0;
                Deaths = 0;
                Streak = 0;
            }
        }
        #endregion

        private StoredData _storedData;

        #region commands
        [ChatCommand("addkill")]
        void CommandAddkill(BasePlayer player, string command, string[] args)
        {
            BasePlayer victim = null;

            foreach (var potential in BasePlayer.activePlayerList)
            {
                if (potential.displayName.ToLower() == args[0].ToLower())
                {
                    victim = potential;
                    break;
                }
            }

            if (victim == null)
            {
                player.IPlayer.Reply("No player found.");
                return;
            }

            AddKill(player, victim);
            player.IPlayer.Reply("Registering kill for " + player.displayName + " against " + victim.displayName);
        }

        [ChatCommand("kills")]
        void CommandKills(BasePlayer player, string command, string[] args)
        {
            var data = GetPlayerInfo(player);
            player.IPlayer.Reply("You have " + data.Kills + " kills and " + data.Deaths + " deaths.");
        }

        [ChatCommand("lb")]
        void CommandLb(BasePlayer player, string command, string[] args)
        {
            var elements = new CuiElementContainer();

            var panel = new CuiPanel
            {
                Image = { Color = "0.0 0.0 0.0 0.75" },
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0.3 0.3", AnchorMax = "0.75 0.75" }
            };
            var panelName = elements.Add(panel);

            var button = new CuiButton
            {
                Button = { Close = panelName, Color = "0 0.5 0 1" },
                RectTransform = { AnchorMin = "0.9 0.9", AnchorMax = "0.99 0.97" },
                Text = { Text = "Close", FontSize = 14, Align = TextAnchor.MiddleCenter }
            };
            elements.Add(button, panelName);

            elements.Add(CreateLabel("Name", 18, TextAnchor.UpperCenter, 0.01f, 0.9f, 0.3f, 0.1f), panelName);

            var yPos = 0.87f;

            for (var i = 0; i < 10; i++)
            {
                var info = _storedData.Players[i];

                if (info == null) break;

                elements.Add(CreateLabel(info.Name, 12, TextAnchor.MiddleCenter, 0.01f, yPos, 0.3f, 0.04f), panelName);
                yPos -= 0.1f;
            }

            CuiHelper.AddUi(player, elements);
        }
        #endregion

        #region oxide hooks
        void Loaded()
        {
            _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(StoredData.FILE_NAME);
        }

        void OnPlayerDie(BasePlayer player, HitInfo info)
        {
            Puts(player.displayName + " was kilt." + " | IPlayer = " + player.IPlayer);
            if (player.IPlayer != null) {
              Puts("isConnected = " + player.IPlayer.IsConnected + " | isServer = " + player.IPlayer.IsServer + " | Address = " + player.IPlayer.Address);
            }

            if (player.lastAttacker == null) return; // Can't determine killer
            if (!(player.lastAttacker is BasePlayer)) return;

            // Filters out AI bots
            var attacker = player.lastAttacker as BasePlayer;
            if (!(BasePlayer.activePlayerList.Contains(attacker))) return;

            Puts("Registering kill for " + attacker.displayName);
            if (attacker.IPlayer != null) {
              Puts("isConnected = " + attacker.IPlayer.IsConnected + " | isServer = " + attacker.IPlayer.IsServer + " | Address = " + attacker.IPlayer.Address);
            }

            AddKill(player, attacker);
        }
        #endregion

        #region util functions
        private void AddKill(BasePlayer killer, BasePlayer victim)
        {
            var data         = GetPlayerInfo(victim);
            var attackerData = GetPlayerInfo(killer);

            data.Deaths++;
            attackerData.Kills++;

            data.Streak = 0;
            attackerData.Streak++;

            Interface.Oxide.DataFileSystem.WriteObject(StoredData.FILE_NAME, _storedData);
        }

        private PlayerInfo GetPlayerInfo(BasePlayer player)
        {
            var data = _storedData.Players.Find(x => x.UserId == player.userID);

            if (data == null)
            {
                data = new PlayerInfo(player);
                _storedData.Players.Add(data);
            }

            return data;
        }

        private CuiLabel CreateLabel(string text, int fontSize, TextAnchor anchor, float xPos, float yPos, float xSize, float ySize)
        {
            var label = new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = xPos + " " + yPos,
                    AnchorMax = (xPos + xSize) + " " + (yPos + ySize)
                },
                Text =
                {
                    Align = anchor,
                    FontSize = fontSize,
                    Text = text
                }
            };

            return label;
        }
        #endregion
    }
}
