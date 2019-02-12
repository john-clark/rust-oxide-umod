using Oxide.Core;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Simple PvP Stats", "6MyBad", "1.4.3")]
    [Description("Simple Pvp Statistics is a plugin to show its statistics by an in-game chat command.")]
    class SimplePVPStats : RustPlugin
    {
        #region Declaration

        private Dictionary<ulong, SimplePVPStatsData> cachedPlayerStats = new Dictionary<ulong, SimplePVPStatsData>();
        static SimplePVPStats ins;

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PlayerStatisticsMSG"] = "<color=#ffc300>Your PvP Statistics</color> : <color=#ffc300>{0}</color> Kills, <color=#ffc300>{1}</color> Deaths, <color=#ffc300>{2}</color> K/D Ratio",
                ["ConsoleWipeMSG"] = "{0} Players Pvp Statistics were wiped!",
                ["ConsoleResetMSG"] = "{0} PvP Statistics has been reset",
                ["ConsoleNotFoundMSG"] = "{0} Not Found!",
            }, this);

            ins = this;
            BasePlayer.activePlayerList.ForEach(player => { if (player != null) OnPlayerInit(player); });
        }

        private void OnPlayerInit(BasePlayer player) => SimplePVPStatsData.TryLoad(player.userID);

        private void OnPlayerDie(BasePlayer victim, HitInfo info)
        {
            BasePlayer killer = info?.Initiator as BasePlayer;

            if (killer == null || killer == victim) return;
            if (victim.IsNpc) return;

            if (cachedPlayerStats.ContainsKey(killer.userID)) cachedPlayerStats[killer.userID].Kills++;
            if (cachedPlayerStats.ContainsKey(victim.userID)) cachedPlayerStats[victim.userID].Deaths++;

            return;
        }

        private void OnServerShutDown() => Unload();

        private void Unload()
        {
            foreach (var data in cachedPlayerStats) data.Value.Save(data.Key);
        }

        #endregion

        #region Commands       

        [ConsoleCommand("stats.wipe")]
        private void WipeStatsCmd(ConsoleSystem.Arg arg)
        {
            if (!arg.IsRcon) return;

            GetAllPlayers().ForEach(ID => SimplePVPStatsData.Reset(ID));
            PrintWarning(string.Format(msg("ConsoleWipeMSG"), new object[] { GetAllPlayers().Count }));
        }

        [ConsoleCommand("stats.reset")]
        private void ResetStatsCmd(ConsoleSystem.Arg arg)
        {
            if (!arg.IsRcon) return;
            if (!arg.HasArgs()) return;

            if (arg.Args.Count() != 1)
            {
                PrintWarning($"Usage : stats.reset <SteamID64>");
                return;
            }

            string ID = arg.Args[0];

            if (!ID.IsSteamId())
            {
                PrintWarning(string.Format(msg("ConsoleNotFoundMSG"), new object[] { ID }));
                return;
            }

            string Name = GetPlayer(ulong.Parse(ID));

            PrintWarning(string.Format(msg("ConsoleResetMSG"), new object[] { Name }));
        }

        [ChatCommand("stats")]
        private void cmdShowStatistics(BasePlayer player, string command, string[] args)
        {
            PlayerMsg(player, string.Format(msg("PlayerStatisticsMSG", player.userID), new object[] { cachedPlayerStats[player.userID].Kills, cachedPlayerStats[player.userID].Deaths, cachedPlayerStats[player.userID].KDR }));
        }

        #endregion

        #region Methods

        public List<ulong> GetAllPlayers()
        {
            List<ulong> PlayersID = new List<ulong>();
            covalence.Players.All.ToList().ForEach(IPlayer => PlayersID.Add(ulong.Parse(IPlayer.Id)));
            return PlayersID;
        }

        public string GetPlayer(ulong id)
        {
            IPlayer player = covalence.Players.FindPlayerById(id.ToString());
            if (player == null) return string.Empty;
            return player.Name;
        }

        public void PlayerMsg(BasePlayer player, string msg) => SendReply(player, msg);

        #endregion

        #region Classes

        private class SimplePVPStatsData
        {
            public int Kills = 0;
            public int Deaths = 0;
            public float KDR => Deaths == 0 ? Kills : (float)Math.Round(((float)Kills) / Deaths, 2);

            internal static void TryLoad(ulong id)
            {
                if (ins.cachedPlayerStats.ContainsKey(id)) return;

                SimplePVPStatsData data = Interface.Oxide.DataFileSystem.ReadObject<SimplePVPStatsData>($"SimplePvPStats/{id}");

                if(data == null) data = new SimplePVPStatsData();

                ins.cachedPlayerStats.Add(id, data);
            }

            internal static void Reset(ulong id)
            {
                SimplePVPStatsData data = Interface.Oxide.DataFileSystem.ReadObject<SimplePVPStatsData>($"SimplePvPStats/{id}");

                if (data == null) return;

                data = new SimplePVPStatsData();
                data.Save(id);
            }

            internal void Save(ulong id) => Interface.Oxide.DataFileSystem.WriteObject(($"SimplePvPStats/{id}"), this, true);
        }

        #endregion

        #region Localization

        public string msg(string key, ulong playerId = 0U) => lang.GetMessage(key, this, playerId != 0U ? playerId.ToString() : null);

        #endregion
    }
}
