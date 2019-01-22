using UnityEngine;
using System.Collections.Generic;
using System;
using Oxide.Core.Libraries.Covalence;
namespace Oxide.Plugins
{
    [Info("MagicStakes", "Norn", "0.1.2", ResourceId = 2497)]
    [Description("Change spawnpoint via command.")]

    class MagicStakes : CovalencePlugin
    {
        private new void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                {"spawnset","<color=green>MagicStakes:</color> You have <color=green>updated</color> your spawnpoint to stake: <color=yellow>{stake}</color> (<color=yellow>{territory}</color>)."},
                {"nopermission","<color=green>MagicStakes:</color> You <color=red>don't</color> have permission to use this command."},
                {"nostakes","<color=green>MagicStakes:</color> You're <color=red>not</color> authorized on any stakes."},
                {"nostakeexists","<color=green>MagicStakes:</color> Stake ID <color=yellow>{id}</color> does <color=red>not</color> exist."},
                {"stakes","<color=green>MagicStakes:</color> You're authorized on <color=yellow>{count}</color> stakes and will spawn at territory <color=yellow>{territory}</color>."},
                {"stakeinfo",  "<color=green>MagicStakes:</color> [Stake <color=yellow>{id}</color> | <color=red>{territory}</color>] Authorized players: <color=green>{authcount}</color>" }
            };

            lang.RegisterMessages(messages, this);
        }
        private void PrintToChat(IPlayer player, string text) => player.Message(text);
        string Msg(string msg, string SteamId = null) => lang.GetMessage(msg, this, SteamId);
        private OwnershipStakeServer GetSpawnStake(IPlayer player)
        {
            PlayerSession session = player.Object as PlayerSession; if (player == null) { return null; }
            foreach (OwnershipStakeServer stake in Resources.FindObjectsOfTypeAll<OwnershipStakeServer>()) { if(stake.SpawnPlayers.Contains(session.Identity) && stake.isActiveAndEnabled) { return stake; } }
            return null;
        }
        void Init() { permission.RegisterPermission("magicstakes.use", this); AddCovalenceCommand("setspawn", "SetSpawnCommand"); }

        private void SetSpawnCommand(IPlayer player, string command, string[] args)
        {
            PlayerSession session = player.Object as PlayerSession; if(player == null) { return; }
            int count = 0;
            if (permission.UserHasPermission(session.SteamId.ToString(), "magicstakes.use"))
            {
                if (args.Length == 0)
                {
                    foreach (OwnershipStakeServer stake in Resources.FindObjectsOfTypeAll<OwnershipStakeServer>())
                    {
                        if (stake.AuthorizedPlayers.Contains(session.Identity) && stake.isActiveAndEnabled)
                        {
                            count++;
                            if (stake.SpawnPlayers.Contains(session.Identity)) { PrintToChat(player, Msg("stakeinfo", session.SteamId.ToString()).Replace("{id}", count.ToString()).Replace("{territory}", stake.TerritoryName).Replace("{authcount}", stake.AuthorizedPlayers.Count.ToString()) + "  | <color=yellow>(SPAWN)</color>."); } else { PrintToChat(player, Msg("stakeinfo", session.SteamId.ToString()).Replace("{id}", count.ToString()).Replace("{territory}", stake.TerritoryName).Replace("{authcount}", stake.AuthorizedPlayers.Count.ToString())); }
                        }
                    }
                    if (count != 0)
                    {
                        string spawnter;
                        if(GetSpawnStake(player) == null) { spawnter = "NO SPAWN SET"; } else { spawnter = GetSpawnStake(player).TerritoryName; }
                        PrintToChat(player, Msg("stakes", session.SteamId.ToString()).Replace("{count}", count.ToString()).Replace("{territory}", spawnter));
                    }
                    else { PrintToChat(player, Msg("nostakes", session.SteamId.ToString())); }
                }
                if (args.Length == 1)
                {
                    bool found = false;
                    foreach (OwnershipStakeServer stake in Resources.FindObjectsOfTypeAll<OwnershipStakeServer>())
                    {
                        if (stake.AuthorizedPlayers.Contains(session.Identity) && stake.isActiveAndEnabled)
                        {
                            count++;
                            if(Convert.ToInt32(args[0]) == count)
                            {
                                if (stake == null) return;
                                OwnershipStakeServer OriginalStake = GetSpawnStake(player); if(OriginalStake != null) { OriginalStake.SpawnPlayers.Remove(session.Identity); }
                                if(!stake.SpawnPlayers.Contains(session.Identity)) { stake.SpawnPlayers.Add(session.Identity); }
                                PrintToChat(player, Msg("spawnset", session.SteamId.ToString()).Replace("{stake}", args[0]).Replace("{territory}", stake.TerritoryName));
                                found = true;
                                return;
                            }
                        }
                    }
                    if(count == 0) { PrintToChat(player, Msg("nostakes", session.SteamId.ToString())); return; }
                    if (!found) { PrintToChat(player, Msg("nostakeexists", session.SteamId.ToString()).Replace("{id}", args[0])); return; }
                }
            }
            else { PrintToChat(player, Msg("nopermission", session.SteamId.ToString())); }
        }
    }
}