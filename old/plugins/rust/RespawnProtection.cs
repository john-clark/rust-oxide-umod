using System;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("RespawnProtection", "sami37", "1.2.8", ResourceId = 2551)]
    [Description("RespawnProtection allow admin to set a respawn protection timer.")]
    public class RespawnProtection : RustPlugin
    {
        [PluginReference]
        private Plugin Economics;

        private int Respawn;
        private int PVERespawn;
        private bool Enabled;
		private bool DisableBuilding = true;
        private bool Punish;
        private bool PVEProtect;
        private int AmountPerHit;
        private int AmountPerKill;
        private Dictionary<ulong, DateTime> protectedPlayersList = new Dictionary<ulong, DateTime>();
        private Dictionary<ulong, DateTime> PVEprotectedPlayersList = new Dictionary<ulong, DateTime>();
        Dictionary<ulong, HitInfo> LastWounded = new Dictionary<ulong, HitInfo>();
        private void ReadFromConfig<T>(string key, ref T var)
        {
            if (Config[key] != null)
            {
                var = (T) Convert.ChangeType(Config[key], typeof (T));
            }
            Config[key] = var;
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Config.Clear();
            Config["Godmode Enabled"] = true;
            Config["PVE Protection"] = false;
            Config["PVE Respawn Protection"] = 60;
            Config["Default Respawn Protection"] = 60;
            Config["Punish fresh respawn kill (Enabled)"] = false;
            Config["Money withdraw per hit"] = 1;
            Config["Money withdraw per kill"] = 100;
            Config["Disable protection on building attack"] = true;
            SaveConfig();
        }
        
        private void OnServerInitialized()
        {
            ReadFromConfig("Default Respawn Protection", ref Respawn);
            ReadFromConfig("PVE Respawn Protection", ref PVERespawn);
            ReadFromConfig("Godmode Enabled", ref Enabled);
            ReadFromConfig("PVE Protection", ref PVEProtect);
            ReadFromConfig("Punish fresh respawn kill (Enabled)", ref Punish);
            ReadFromConfig("Money withdraw per hit", ref AmountPerHit);
            ReadFromConfig("Money withdraw per kill", ref AmountPerKill);
            ReadFromConfig("Disable protection on building attack", ref DisableBuilding);
            SaveConfig();

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Cant Hit", "You Cant Hit this player until protection is left. {0}"},
                {"Protected", "You have just respawned, you are protected for {0}s from pvp."},
                {"ProtectedPVE", "You have just respawned, you are protected for {0}s from pve."},
                {"NoLongerProtected", "You are no longer protected from pvp, take care."},
                {"NoLongerProtectedNPC", "You are no longer protected from pve, take care."},
                {"TryKill", "You are trying to kill a fresh respawned player, you lost {0} money."},
                {"Killed", "You just kill a fresh respawned player, you lost {0} money."},
                {"UserNotFound", "There is no player with this ID ({0})"},
                {"ProtectionRemoved", "Protection from player {0} removed."},
                {"NotProtected", "This player is not protected."}
            }, this);
        }

        private HitInfo TryGetLastWounded(ulong uid, HitInfo info)
        {
            if (LastWounded.ContainsKey(uid))
            {
                HitInfo output = LastWounded[uid];
                LastWounded.Remove(uid);
                return output;
            }

            return info;
        }

        private void OnEntityDeath(BaseCombatEntity victim, HitInfo info)
        {
            if (victim is NPCPlayer)
                return;
            BasePlayer victimBasePlayer = victim as BasePlayer;
            if (victimBasePlayer == null)
                return;
            if (victimBasePlayer.IsWounded())
                info = TryGetLastWounded(victimBasePlayer.userID, info);

            if (info?.InitiatorPlayer == null && (victimBasePlayer.name?.Contains("autospawn") ?? false))
                return;
            if (info?.InitiatorPlayer != null && Economics != null && Economics.IsLoaded)
            {
                SendReply(info.InitiatorPlayer, string.Format(lang.GetMessage("Killed", this, info.InitiatorPlayer.UserIDString), AmountPerKill));
                Economics?.CallHook("Withdraw", info.InitiatorPlayer.UserIDString,
                    AmountPerKill);
            }
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if ((entity is BaseNpc || entity is NPCPlayer) && !PVEProtect)
                return;
            if (hitinfo != null)
            {
                var attacker = hitinfo.Initiator;
                BasePlayer victim = entity as BasePlayer;
                var attack = attacker as BasePlayer;
                if ((attacker is BaseNpc || attacker is NPCPlayer) && PVEProtect)
                {
                    if (victim != null && PVEprotectedPlayersList != null &&
                        PVEprotectedPlayersList.ContainsKey(victim.userID))
                    {
                        hitinfo.damageTypes = new DamageTypeList();
                        hitinfo.DoHitEffects = false;
                    }

                    return;
                }
                if (victim != null && attack != null && !(entity is NPCPlayer))
                {
                    NextTick(() =>
                    {
                        if (entity.ToPlayer().IsWounded())
                            LastWounded[entity.ToPlayer().userID] = hitinfo;
                    });

                    if (!Enabled && !Punish)
                        return;
                    if (Punish)
                        if (Economics != null && Economics.IsLoaded)
                        {
                            SendReply(attack,
                                string.Format(lang.GetMessage("TryKill", this, attack.UserIDString), AmountPerHit));
                            Economics?.CallHook("Withdraw", attack.UserIDString,
                                AmountPerKill);
                        }
                    if (protectedPlayersList.ContainsKey(attack.userID))
                    {
                        protectedPlayersList.Remove(attack.userID);
                        if (Enabled)
                            SendReply(attack,
                                lang.GetMessage("NoLongerProtected", this, attack.UserIDString));
                    }
                    if (protectedPlayersList.ContainsKey(victim.userID))
                    {
                        DateTime now = DateTime.Now;
                        DateTime old = protectedPlayersList[victim.userID];
                        TimeSpan wait = now - old;
                        hitinfo.damageTypes = new DamageTypeList();
                        hitinfo.DoHitEffects = false;
                        if (Enabled)
                            SendReply(attack,
                                string.Format(lang.GetMessage("Cant Hit", this, attack.UserIDString),
                                    wait));
                    }

                    return;
                }
                if (entity is BuildingBlock && attack != null && protectedPlayersList.ContainsKey(attack.userID) && DisableBuilding)
                {
                    protectedPlayersList.Remove(attack.userID);
                    if (Enabled)
                        SendReply(attack,
                            lang.GetMessage("NoLongerProtected", this, attack.UserIDString));
                    return;
                }
                if ((entity is BaseNpc || entity is NPCPlayer || entity is BradleyAPC) && attack != null && PVEprotectedPlayersList.ContainsKey(attack.userID))
                {
                    PVEprotectedPlayersList.Remove(attack.userID);
                    if(PVEProtect)
                        SendReply(attack, lang.GetMessage("NoLongerProtectedNPC", this, attack.UserIDString));
                }
            }
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (protectedPlayersList.ContainsKey(player.userID))
                protectedPlayersList.Remove(player.userID);
            if (PVEprotectedPlayersList.ContainsKey(player.userID))
                PVEprotectedPlayersList.Remove(player.userID);
            protectedPlayersList.Add(player.userID, DateTime.Now);
            PVEprotectedPlayersList.Add(player.userID, DateTime.Now);
            if(Enabled)
                SendReply(player, string.Format(lang.GetMessage("Protected", this, player.UserIDString), Respawn));
            if(PVEProtect)
                SendReply(player, string.Format(lang.GetMessage("ProtectedPVE", this, player.UserIDString), PVERespawn));
            timer.Once(Respawn, () =>
            {
                protectedPlayersList.Remove(player.userID);
                if (Enabled)
                    SendReply(player, lang.GetMessage("NoLongerProtected", this, player.UserIDString));
            });
            timer.Once(PVERespawn, () =>
            {
                PVEprotectedPlayersList.Remove(player.userID);
                if (PVEProtect)
                    SendReply(player, lang.GetMessage("NoLongerProtectedNPC", this, player.UserIDString));
            });
        }

        private bool PlayerRespawn(ulong UserID)
        {
            var baseplayer = BasePlayer.Find(UserID.ToString());
            if (baseplayer == null) return false;
            if (protectedPlayersList.ContainsKey(UserID))
                protectedPlayersList.Remove(UserID);
            if (PVEprotectedPlayersList.ContainsKey(UserID))
                PVEprotectedPlayersList.Remove(UserID);
            protectedPlayersList.Add(UserID, DateTime.Now);
            PVEprotectedPlayersList.Add(UserID, DateTime.Now);
            if (Enabled)
                SendReply(baseplayer, string.Format(lang.GetMessage("Protected", this, UserID.ToString()), Respawn));
            if (PVEProtect)
                SendReply(baseplayer, string.Format(lang.GetMessage("ProtectedPVE", this, UserID.ToString()), PVERespawn));
            timer.Once(Respawn, () =>
            {
                if (Enabled)
                    protectedPlayersList.Remove(UserID);
                SendReply(baseplayer, lang.GetMessage("NoLongerProtected", this, UserID.ToString()));
            });
            timer.Once(PVERespawn, () =>
            {
                if (PVEProtect)
                    PVEprotectedPlayersList.Remove(UserID);
                SendReply(baseplayer, lang.GetMessage("NoLongerProtectedNPC", this, UserID.ToString()));
            });
            return true;
        }

        private bool AddProtection(ulong UserID)
        {
            return PlayerRespawn(UserID);
        }

        private string RemoveProtection(ulong UserID)
        {
            var baseplayer = BasePlayer.Find(UserID.ToString());
            if (baseplayer == null)
                return string.Format(lang.GetMessage("UserNotFound", this, UserID.ToString()), UserID);
            if (protectedPlayersList != null && protectedPlayersList.ContainsKey(UserID))
                return string.Format(lang.GetMessage("ProtectionRemoved", this, UserID.ToString()),
                    baseplayer.displayName);
            if (protectedPlayersList == null || !protectedPlayersList.ContainsKey(UserID))
                return string.Format(lang.GetMessage("NotProtected", this, UserID.ToString()));
            if (PVEprotectedPlayersList != null && PVEprotectedPlayersList.ContainsKey(UserID))
                return string.Format(lang.GetMessage("ProtectionRemoved", this, UserID.ToString()),
                    baseplayer.displayName);
            if (PVEprotectedPlayersList == null || !PVEprotectedPlayersList.ContainsKey(UserID))
                return string.Format(lang.GetMessage("NotProtected", this, UserID.ToString()));

            return null;
        }
    }
}