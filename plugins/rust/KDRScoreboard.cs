using System.Collections.Generic;
using Oxide.Core;
using System.Linq;
using System;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("KDRScoreboard", "Ankawi", "1.0.1")]
    [Description("Enables scoreboards that can show Kills, Deaths, or K/D Ratio")]
    class KDRScoreboard : RustPlugin
    {
        [PluginReference]
        Plugin Scoreboards;

        private static KDRScoreboard Instance;

        Dictionary<ulong, HitInfo> LastWounded = new Dictionary<ulong, HitInfo>();

        static HashSet<PlayerData> LoadedPlayerData = new HashSet<PlayerData>();

        #region Data
        class PlayerData
        {
            public ulong id;
            public string name;
            public int kills;
            public int deaths;
            internal float KDR => deaths == 0 ? kills : (float)Math.Round(((float)kills) / deaths, 1);

            internal static void TryLoad(BasePlayer player)
            {
                if (Find(player) != null)
                    return;

                PlayerData data = Interface.Oxide.DataFileSystem.ReadObject<PlayerData>($"KDRScoreboard/{player.userID}");

                if (data == null || data.id == 0)
                {
                    data = new PlayerData
                    {
                        id = player.userID,
                        name = player.displayName
                    };
                }
                else
                    data.Update(player);

                data.Save();
                LoadedPlayerData.Add(data);
            }

            internal void Update(BasePlayer player)
            {
                name = player.displayName;
                Save();
            }

            internal void Save()
            {
                Instance.UpdateScoreboard();
                Interface.Oxide.DataFileSystem.WriteObject($"KDRScoreboard/{id}", this, true);
            }

            internal static PlayerData Find(BasePlayer player)
            {

                PlayerData data = LoadedPlayerData.ToList().Find((p) => p.id == player.userID);

                return data;
            }
        }
        #endregion

        #region Hooks
        void OnPlayerInit(BasePlayer player)
        {
            PlayerData.TryLoad(player);
        }
        void OnPlayerDisconnected(BasePlayer player)
        {
            PlayerData.TryLoad(player);
        }
        void Loaded()
        {
            if (!Scoreboards)
            {
                PrintWarning("The Scoreboard API must be enabled for this plugin to fully function, Get it here: http://oxidemod.org/plugins/scoreboards-api.2054/");
            }

            Instance = this;

            foreach (var player in BasePlayer.activePlayerList)
            {
                PlayerData.TryLoad(player);
            }

            KDR_Scoreboard();
            KillsScoreboard();
            DeathsScoreboard();
        }
        HitInfo TryGetLastWounded(ulong id, HitInfo info)
        {
            if (LastWounded.ContainsKey(id))
            {
                HitInfo output = LastWounded[id];
                LastWounded.Remove(id);
                return output;
            }

            return info;
        }
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity?.ToPlayer() != null && info?.Initiator?.ToPlayer() != null)
            {
                NextTick(() =>
                {
                    if (entity.ToPlayer().IsWounded())
                        LastWounded[entity.ToPlayer().userID] = info;
                });
            }
        }
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            try
            {
                if (entity == info.Initiator) return;
                if (entity == null || info.Initiator == null) return;

                if (info?.Initiator?.ToPlayer() == null && (entity?.name?.Contains("autospawn") ?? false))
                    return;
                if (entity.ToPlayer() != null)
                {
                    if (entity.ToPlayer().IsWounded())
                    {
                        info = TryGetLastWounded(entity.ToPlayer().userID, info);
                    }
                }
                if (entity != null && entity is BasePlayer && info?.Initiator != null && info.Initiator is BasePlayer)
                {
                    PlayerData victimData = PlayerData.Find((BasePlayer)entity);
                    PlayerData attackerData = PlayerData.Find((BasePlayer)info.Initiator);

                    victimData.deaths++;
                    attackerData.kills++;

                    victimData.Save();
                    attackerData.Save();
                }
            }
            catch (Exception ex)
            {
            }
        }
        #endregion

        #region Scoreboards
        void KDR_Scoreboard()
        {
            Dictionary<string, string> topKdr = new Dictionary<string, string>();
            List<PlayerData> topKDRData = LoadedPlayerData.OrderByDescending(d => d.KDR).Take(15).ToList();

            foreach (var playerData in topKDRData)
                topKdr.Add(playerData.name, playerData.KDR.ToString());

            Scoreboards?.Call("CreateScoreboard", "K/D Ratio", "Lists the top KDRs", topKdr.ToArray<KeyValuePair<string, string>>());
        }
        void KillsScoreboard()
        {
            Dictionary<string, string> topKills = new Dictionary<string, string>();
            List<PlayerData> topKillsData = LoadedPlayerData.OrderByDescending(d => d.kills).Take(15).ToList();

            foreach (var playerData in topKillsData)
                topKills.Add(playerData.name, playerData.KDR.ToString());

            Scoreboards?.Call("CreateScoreboard", "Top Kills", "Lists the top Kills", topKills.ToArray<KeyValuePair<string, string>>());
        }
        void DeathsScoreboard()
        {
            Dictionary<string, string> topDeaths = new Dictionary<string, string>();
            List<PlayerData> topDeathsData = LoadedPlayerData.OrderByDescending(d => d.KDR).Take(15).ToList();

            foreach (var playerData in topDeathsData)
                topDeaths.Add(playerData.name, playerData.KDR.ToString());

            Scoreboards?.Call("CreateScoreboard", "Top Deaths", "Lists the top Deaths", topDeaths.ToArray<KeyValuePair<string, string>>());
        }

        void UpdateScoreboard()
        {
            Dictionary<string, string> topKdr = new Dictionary<string, string>();
            List<PlayerData> topKDRData = LoadedPlayerData.OrderByDescending(d => d.KDR).Take(15).ToList();
            foreach (var kdrData in topKDRData)
            {
                topKdr.Add(kdrData.name, kdrData.KDR.ToString("0.00"));
            }

            Dictionary<string, string> topKills = new Dictionary<string, string>();
            List<PlayerData> topKillsData = LoadedPlayerData.OrderByDescending(d => d.kills).Take(15).ToList();

            foreach(var killsData in topKillsData)
            {
                topKills.Add(killsData.name, killsData.kills.ToString());
            }

            Dictionary<string, string> topDeaths = new Dictionary<string, string>();
            List<PlayerData> topDeathsData = LoadedPlayerData.OrderByDescending(d => d.deaths).Take(15).ToList();
            foreach(var deathsData in topDeathsData)
            {
                topDeaths.Add(deathsData.name, deathsData.deaths.ToString());
            }
            Scoreboards?.Call("UpdateScoreboard", "K/D Ratio", topKdr.ToArray<KeyValuePair<string, string>>());
            Scoreboards?.Call("UpdateScoreboard", "Top Kills", topKills.ToArray<KeyValuePair<string, string>>());
            Scoreboards?.Call("UpdateScoreboard", "Top Deaths", topDeaths.ToArray<KeyValuePair<string, string>>());
        }
        #endregion

        #region Commands

        [ChatCommand("kdr")]
        void cmdKdr(BasePlayer player, string command, string[] args)
        {
            GetCurrentStats(player);
        }
        void GetCurrentStats(BasePlayer player)
        {
            PlayerData data = Interface.Oxide.DataFileSystem.ReadObject<PlayerData>($"KDRScoreboard/{player.userID}");
            int kills = data.kills;
            int deaths = data.deaths;
            string playerName = data.name;
            float kdr = data.KDR;

            PrintToChat(player, "<color=red> Player Name : </color>" + $"{playerName}"
                                        + "\n" + "<color=lime> Kills : </color>" + $"{kills}"
                                        + "\n" + "<color=lime> Deaths : </color>" + $"{deaths}"
                                        + "\n" + "<color=lime> K/D Ratio : </color>" + $"{kdr}");
        }
        #endregion
    }
}