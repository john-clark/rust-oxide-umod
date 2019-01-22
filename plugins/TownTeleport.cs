using System.Collections.Generic;
using UnityEngine;
using System;
using Rust;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("TownTeleport", "mvrb", "1.5.1")]
    [Description("Teleport to Outpost and Bandit Camp")]
    class TownTeleport : RustPlugin
    {
		[PluginReference] Plugin NoEscape;
		
        private StoredData storedData;

        private List<Vector3> OutpostSpawns = new List<Vector3>();
        private List<Vector3> BanditSpawns = new List<Vector3>();

        private Dictionary<BasePlayer, Timer> TeleportTimers = new Dictionary<BasePlayer, Timer>();

        private string permissionOutpost = "townteleport.outpost";
        private string permissionBandit = "townteleport.bandit";
        private string permissionNoCooldown = "townteleport.nocooldown";
        private string teleportCommandOutpost = "otp";
        private string teleportCommandBandit = "btp";
        private string cancelTeleportCommand = "ttc";
        private bool cancelTpPlayerDamage = true;
        private bool cancelTpFallDamage = true;
        private int teleportCountdown = 30;
        private int teleportCooldown = 30;

        protected override void LoadDefaultConfig()
        {
            Config["CancelTpPlayerDamage"] = cancelTpPlayerDamage = GetConfig("CancelTpPlayerDamage", true);
            Config["CancelTpFallDamage"] = cancelTpFallDamage = GetConfig("CancelTpFallDamage", true);
            Config["OutpostTeleportCommand"] = teleportCommandOutpost = GetConfig("OutpostTeleportCommand", "otp");
            Config["BanditCampTeleportCommand"] = teleportCommandBandit = GetConfig("BanditCampTeleportCommand", "btp");
            Config["CancelTeleportCommand"] = cancelTeleportCommand = GetConfig("CancelTeleportCommand", "ttc");
            Config["TeleportCooldown"] = teleportCooldown = GetConfig("TeleportCooldown", 0);
            Config["TeleportCountdown"] = teleportCountdown = GetConfig("TeleportCountdown", 30);

            SaveConfig();
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["OutpostTeleport"] = "TownTeleport: Teleporting to Outpost in {0} seconds...\nType /{1} to cancel the teleport.",
                ["BanditTeleport"] = "TownTeleport: Teleporting to BanditCamp in {0} seconds...\nType /{1} to cancel the teleport.",
                ["TeleportSuccessMessage"] = "TownTeleport: You have successfully teleported to the Outpost.",
                ["NoActiveTeleport"] = "TownTeleport: You are not about to teleport to Bandit Camp or Outpost.\nType /{0} to teleport to the Outpost.\nType /{1} to teleport to Bandit Camp.",
                ["AlreadyTeleporting"] = "TownTeleport: You are already about to teleport to the Outpost.",

                ["Error: Seated"] = "You can't teleport while seated.",
                ["Error: NoBuildingPrivilege"] = "You can't teleport without Building Privilege.",
                ["Error: Wounded"] = "You can't teleport while wounded.",
                ["Error: Hostile"] = "You can't teleport while you are marked as Hostile.\nYou will be unmarked as hostile in {0} {1}",
                ["Error: Cooldown"] = "You can't teleport yet.\nYou will be able to teleport in {0} {1}",
                ["Error: RaidBlocked"] = "You can't teleport while raid blocked.",
                ["Error: CombatBlocked"] = "You can't teleport while combat blocked.",

                ["TeleportCancelled"] = "TownTeleport: You have cancelled the teleport timer.",
                ["TeleportCancelledPlayerDamage"] = "TownTeleport: Teleport cancelled due to player damage.",
                ["TeleportCancelledFallDamage"] = "TownTeleport: Teleport cancelled due to fall damage.",
                ["OutpostNotFound"] = "TownTeleport: Plugin was unable to locate Outpost.",
                ["BanditNotFound"] = "TownTeleport: Plugin was unable to locate BanditCamp.",
                ["NoPermission"] = "TownTeleport: You do not have permission to use this command."
            }, this);
        }

        private void OnServerInitialized() => FindTowns();

        private void Loaded()
        {
            LoadDefaultConfig();
            LoadData();

            permission.RegisterPermission(permissionOutpost, this);
            permission.RegisterPermission(permissionBandit, this);
            permission.RegisterPermission(permissionNoCooldown, this);

            cmd.AddChatCommand(teleportCommandOutpost, this, "CmdOutpost");
            cmd.AddChatCommand(teleportCommandBandit, this, "CmdBandit");
            cmd.AddChatCommand(cancelTeleportCommand, this, "CmdCancelTp");
        }

        private class StoredData
        {
            public Dictionary<ulong, int> Cooldowns = new Dictionary<ulong, int>();
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (!TeleportTimers.ContainsKey(player)) return;

            TeleportTimers[player]?.Destroy();
            TeleportTimers.Remove(player);
        }

        private void CmdOutpost(BasePlayer player)
        {
            var canTeleport = CanTownTeleport(player, "outpost", true);

            if (canTeleport is string)
            {
                player.ChatMessage(canTeleport as string);
                return;
            }

            StartTeleportTimer(player, "outpost");

            player.ChatMessage(Lang("OutpostTeleport", player.UserIDString, teleportCountdown, cancelTeleportCommand));
        }

        private void CmdBandit(BasePlayer player)
        {
            var canTeleport = CanTownTeleport(player, "bandit", true);

            if (canTeleport is string)
            {
                player.ChatMessage(canTeleport as string);
                return;
            }

            StartTeleportTimer(player, "bandit");

            player.ChatMessage(Lang("BanditTeleport", player.UserIDString, teleportCountdown, cancelTeleportCommand));
        }

        private object CanTownTeleport(BasePlayer player, string town, bool checkTimers)
        {
            if (town == "outpost")
            {
                if (!permission.UserHasPermission(player.UserIDString, permissionOutpost))
                {
                    return Lang("NoPermission", player.UserIDString);
                }

                if (OutpostSpawns.Count == 0)
                {
                    return Lang("OutpostNotFound", player.UserIDString);
                }

            }
            else if (town == "bandit")
            {
                if (!permission.UserHasPermission(player.UserIDString, permissionBandit))
                {
                    return Lang("NoPermission", player.UserIDString);
                }

                if (BanditSpawns.Count == 0)
                {
                    return Lang("BanditNotFound", player.UserIDString);
                }
            }

            if (!permission.UserHasPermission(player.UserIDString, permissionNoCooldown))
            {
                int currentTime = GetUnix();

                if (storedData.Cooldowns.ContainsKey(player.userID))
                {
                    int lastTeleport = storedData.Cooldowns[player.userID];

                    if (teleportCooldown > 0 && currentTime < lastTeleport + teleportCooldown)
                    {
                        int remaining = lastTeleport - currentTime + teleportCooldown;

                        if (remaining > 0 && remaining < 60)
                        {
                            return Lang("Error: Cooldown", player.UserIDString, (remaining).ToString("#,#"), "seconds");

                        }
                        else if (remaining > 60 && remaining < 3600)
                        {
                            return Lang("Error: Cooldown", player.UserIDString, (remaining / 60).ToString("#,#"), "minutes");

                        }
                        else if (remaining > 3600)
                        {
                            return Lang("Error: Cooldown", player.UserIDString, (remaining / 60 / 60).ToString("#,#"), "hours");
                        }
                    }
                }
            }

            if (!player.CanBuild())
            {
                return Lang("Error: NoBuildingPrivilege", player.UserIDString);
            }

            if (player.isMounted)
            {
                return Lang("Error: Seated", player.UserIDString);
            }

            if (player.IsWounded())
            {
                return Lang("Error: Wounded", player.UserIDString);
            }

            if (player.IsHostile())
            {
                float remaining = player.unHostileTime - Time.realtimeSinceStartup;

                if (remaining > 0 && remaining < 60)
                {
                    return Lang("Error: Hostile", player.UserIDString, (remaining).ToString("#,#"), "seconds");

                }
                else if (remaining > 60 && remaining < 3600)
                {
                    return Lang("Error: Hostile", player.UserIDString, (remaining / 60).ToString("#,#"), "minutes");

                }
                else if (remaining > 3600)
                {
                    return Lang("Error: Hostile", player.UserIDString, (remaining / 60 / 60).ToString("#,#"), "hours");
                }
            }
			
			if (NoEscape)
			{
				if ((bool)NoEscape?.Call("IsRaidBlocked", player))
				{
					return Lang("Error: RaidBlocked", player.UserIDString);
				}
				if ((bool)NoEscape?.Call("IsCombatBlocked", player))
				{
					return Lang("Error: CombatBlocked", player.UserIDString);
				}
			}

            if (checkTimers && TeleportTimers.ContainsKey(player))
            {
                return Lang("AlreadyTeleporting", player.UserIDString);
            }

            return null;
        }

        private void StartTeleportTimer(BasePlayer player, string town)
        {
            TeleportTimers[player] = timer.Once(teleportCountdown, () =>
            {
                HeldEntity heldEntity = player.GetHeldEntity();
                if (heldEntity != null)
                {
                    heldEntity.SetHeld(false);
                }

                var canTeleport = CanTownTeleport(player, town, false);

                if (canTeleport is string)
                {
                    player.ChatMessage(canTeleport as string);
                    return;
                }

                if (town == "outpost")
                {
                    Teleport(player, OutpostSpawns[new System.Random().Next(OutpostSpawns.Count)]);
                }
                else if (town == "bandit")
                {
                    Teleport(player, BanditSpawns[new System.Random().Next(BanditSpawns.Count)]);
                }
				
				int currentTime = GetUnix();
				
				if (!storedData.Cooldowns.ContainsKey(player.userID))
                {
                    storedData.Cooldowns.Add(player.userID, currentTime);
                }
				else
				{
					storedData.Cooldowns[player.userID] = currentTime;
				}
				
                SaveData();

                TeleportTimers[player]?.Destroy();
                if (TeleportTimers.ContainsKey(player)) TeleportTimers.Remove(player);

                player.ChatMessage(Lang("TeleportSuccessMessage", player.UserIDString));
            });
        }

        private void CmdCancelTp(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, permissionOutpost) || permission.UserHasPermission(player.UserIDString, permissionBandit))
            {
                if (!TeleportTimers.ContainsKey(player))
                {
                    player.ChatMessage(Lang("NoActiveTeleport", player.UserIDString, teleportCommandOutpost, teleportCommandBandit));
                    return;
                }

                CancelTp(player, Lang("TeleportCancelled", player.UserIDString));
            }
            else
            {
                player.ChatMessage(Lang("NoPermission", player.UserIDString));
            }
        }

        private void FindTowns()
        {
            foreach (MonumentInfo monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
            {
                if (monument.name.Contains("compound"))
                {
                    List<BaseEntity> list = new List<BaseEntity>();
                    Vis.Entities(monument.transform.position, 25, list);
                    foreach (BaseEntity entity in list)
                    {
                        if (entity.name.Contains("chair"))
                        {
                            Vector3 chairPos = entity.transform.position;
                            chairPos.y += 1;
                            if (!OutpostSpawns.Contains(chairPos)) OutpostSpawns.Add(chairPos);
                        }
                    }
                }
                else if (monument.name.Contains("bandit"))
                {
                    var list = new List<BaseEntity>();
                    Vis.Entities(monument.transform.position, 50, list);
                    foreach (BaseEntity entity in list)
                    {
                        if (entity.name.Contains("chair.invisible.static"))
                        {
                            Vector3 chairPos = entity.transform.position;
                            chairPos.y += 1;
                            if (!BanditSpawns.Contains(chairPos)) BanditSpawns.Add(chairPos);
                        }
                    }
                }
            }
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            var player = entity.ToPlayer();

            if (player == null || info == null) return;

            if (!TeleportTimers.ContainsKey(player)) return;

            NextTick(() =>
            {
                if (info.damageTypes.Total() <= 0) return;

                if (cancelTpPlayerDamage && info?.Initiator is BasePlayer)
                {
                    CancelTp(player, Lang("TeleportCancelledPlayerDamage", player.UserIDString));
                }
                else if (cancelTpFallDamage && info.damageTypes.Has(DamageType.Fall))
                {
                    CancelTp(player, Lang("TeleportCancelledFallDamage", player.UserIDString));
                }
            });
        }

        private void CancelTp(BasePlayer player, string reason)
        {
            TeleportTimers[player]?.Destroy();
            TeleportTimers.Remove(player);
            player.ChatMessage(reason);
        }

        private void Teleport(BasePlayer player, Vector3 position)
        {
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "StartLoading");

            StartSleeping(player);
            player.MovePosition(position);

            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "ForcePositionTo", position);

            if (player.net?.connection != null)
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);

            player.UpdateNetworkGroup();
            player.SendNetworkUpdateImmediate();

            if (player.net?.connection == null) return;

            try { player.ClearEntityQueue(); } catch { }

            player.SendFullSnapshot();
        }

        private void StartSleeping(BasePlayer player)
        {
            if (player.IsSleeping()) return;

            player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);

            if (!BasePlayer.sleepingPlayerList.Contains(player))
                BasePlayer.sleepingPlayerList.Add(player);

            player.CancelInvoke("InventoryUpdate");
        }

        private void LoadData() => storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>(this.Name);

        private void SaveData() => Interface.GetMod().DataFileSystem.WriteObject(this.Name, storedData);

        private Int32 GetUnix() => (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

        private T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
    }
}