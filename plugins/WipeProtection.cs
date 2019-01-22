using Oxide.Core;
using System;
using System.Collections.Generic;
using Rust;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("WipeProtection", "Slydelix", "1.2.1", ResourceId = 2722)]
    class WipeProtection : RustPlugin
    {
        private List<BasePlayer> cooldown = new List<BasePlayer>();
        private Dictionary<string, string> raidtools = new Dictionary<string, string>
        {
            {"ammo.rocket.fire", "rocket_fire" },
            {"ammo.rocket.hv", "rocket_hv" },
            {"ammo.rocket.basic", "rocket_basic" },
            {"explosive.timed", "explosive.timed.deployed" },
            {"surveycharge", "survey_charge.deployed" },
            {"explosive.satchel", "explosive.satchel.deployed" },
            {"grenade.beancan", "grenade.beancan.deployed" },
            {"grenade.f1", "grenade.f1.deployed" }
        };

        private float wipeprotecctime;
        private bool refund, broadcastend, msgadmin;

        #region Config
        protected override void LoadDefaultConfig()
        {
            Config["Wipe protection time (hours)"] = wipeprotecctime = GetConfig("Wipe protection time (hours)", 24f);
            Config["Broadcast to chat when raid block has ended"] = broadcastend = GetConfig("Broadcast to chat when raid block has ended", true);
            Config["Message admins on connection with info on when the raid block is ending"] = msgadmin = GetConfig("Message admins on connection with info on when the raid block is ending", true);
            Config["Refund explosives"] = refund = GetConfig("Refund explosives", true);
            SaveConfig();
        }

        private T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        #endregion
        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"adminmsg", "<color=silver>Wipe protection ending at {0} ({1})</color>"},
                {"console_manual", "Manually setting {0} as wipe time and {1} as time after which raiding is possible"},
                {"console_auto", "Detected wipe, setting {0} as wipe time and {1} as time after which raiding is possible"},
                {"console_stopped", "Everything is now raidable"},
                {"raidprotection_ended", "<size=20>Wipe protection is now over.</size>"},
                {"dataFileWiped", "Data file successfully wiped"},
                {"refunded", "Your '{0}' was refunded."},
                {"wipe_blocked", "This entity cannot be destroyed because all raiding is currently blocked."}
            }, this);
        }

        #endregion
        #region DataFile
        private class StoredData
        {
            public bool wipeprotection;
            public string lastwipe;
            public string RaidStartTime;

            public StoredData()
            {

            }
        }

        private StoredData storedData;

        #endregion
        #region Hooks

        private void Unload() => SaveFile();

        private void Init()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(this.Name);
            LoadDefaultConfig();
            CheckTime();
        }

        private void CheckTime()
        {
            timer.Every(30f, () => {
                if (!storedData.wipeprotection) return;
                if (DateTime.Now >= Convert.ToDateTime(storedData.RaidStartTime))
                {
                    if (broadcastend)
                        covalence.Server.Broadcast(lang.GetMessage("raidprotection_ended", this, null));

                    storedData.wipeprotection = false;
                    SaveFile();
                    return;
                }
            });
        }

        private void OnNewSave(string filename)
        {
            DateTime now = DateTime.Now;
            DateTime rs = now.AddHours(wipeprotecctime);
            storedData.wipeprotection = true;
            storedData.lastwipe = now.ToString();
            storedData.RaidStartTime = rs.ToString();
            SaveFile();
            PrintWarning(lang.GetMessage("console_auto", this, null), now, rs);
        }

        private void OnUserConnected(IPlayer player)
        {
            if (!player.IsAdmin || !msgadmin || !storedData.wipeprotection) return;
            string remaining = Convert.ToDateTime(storedData.RaidStartTime).Subtract(DateTime.Now).ToShortString();
            player.Message(lang.GetMessage("adminmsg", this, player.Id), "<color=orange>[WipeProtection]</color>", storedData.RaidStartTime, remaining);
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (hitinfo == null || entity == null || entity.OwnerID == hitinfo?.InitiatorPlayer?.userID || entity?.OwnerID == 0 || hitinfo?.WeaponPrefab?.ShortPrefabName == null) return null;
            if (!(entity is BuildingBlock || entity is Door || entity.PrefabName.Contains("deployable"))) return null;

            BasePlayer attacker = hitinfo.InitiatorPlayer;

            string name = hitinfo?.WeaponPrefab?.ShortPrefabName;

            if (cooldown.Contains(attacker))
            {
                RemoveCD(attacker);
                if (WipeProtected())
                {
                    hitinfo.damageTypes = new DamageTypeList();
                    hitinfo.DoHitEffects = false;
                    hitinfo.HitMaterial = 0;
                    return true;
                }
                return null;
            }

            cooldown.Add(attacker);
            RemoveCD(attacker);

            if (WipeProtected())
            {
                hitinfo.damageTypes = new DamageTypeList();
                hitinfo.DoHitEffects = false;
                hitinfo.HitMaterial = 0;
                msgPlayer(attacker, entity);
                Refund(attacker, name, entity);
                return true;
            }

            return null;
        }

        #endregion
        #region Functions
        private void RemoveCD(BasePlayer player)
        {
            if (player == null) return;
            timer.In(0.1f, () => {
                if (cooldown.Contains(player)) cooldown.Remove(player);
            });
        }

        bool WipeProtected()
        {
            if (!storedData.wipeprotection) return false;
            if (DateTime.Now < (Convert.ToDateTime(storedData.RaidStartTime))) return true;

            return false;
        }

        private void msgPlayer(BasePlayer attacker, BaseEntity entity)
        {
            if (WipeProtected())
            {
                SendReply(attacker, lang.GetMessage("wipe_blocked", this, attacker.UserIDString));
                return;
            }
        }

        private void Refund(BasePlayer attacker, string name, BaseEntity ent)
        {
            if (!refund) return;

            foreach (var entry in raidtools)
            {
                if (name == entry.Value)
                {
                    Item item = ItemManager.CreateByName(entry.Key, 1);
                    attacker.GiveItem(item);
                    SendReply(attacker, lang.GetMessage("refunded", this, attacker.UserIDString), item.info.displayName.english);
                }
            }
        }

        private void SaveFile() => Interface.Oxide.DataFileSystem.WriteObject(this.Name, storedData);

        #endregion
        #region Commands
        [ConsoleCommand("wipeprotection.manual")]
        private void wipeStartCmd(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            DateTime now = DateTime.Now;
            DateTime rs = now.AddHours(wipeprotecctime);
            storedData.wipeprotection = true;
            storedData.lastwipe = now.ToString();
            storedData.RaidStartTime = rs.ToString();
            SaveFile();

            Puts(lang.GetMessage("console_manual", this, null), now, rs);
            return;
        }

        [ConsoleCommand("wipeprotection.stop")]
        private void wipeEndCmd(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            storedData.wipeprotection = false;
            SaveFile();
            Puts(lang.GetMessage("console_stopped", this, null));
            return;
        }
        #endregion
    }
}