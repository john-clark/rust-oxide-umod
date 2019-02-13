/*
 * TODO:
 * Add optional GUI icons to indicate what is enabled/disabled
 * Finish implementing pre-purge warning countdown
 */

using System;
using System.Collections.Generic;
using Facepunch;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Purge", "Wulf/lukespragg", "1.2.2", ResourceId = 1102)]
    [Description("Allows damage and killing only between specific in-game hours")]

    class Purge : CovalencePlugin
    {
        #region Initialization

        const string permAllow = "purge.allow";
        const string permProtect = "purge.protect";

        bool configChanged;
        bool purgeActive;
        bool purgeAnimal;
        bool purgeHeli;
        bool purgeLoot;
        bool purgeStructure;
        bool purgeTurret;
        bool purgeWorld;
        bool realTime;
        bool safeAnimal;
        bool safeHeli;
        bool safeLoot;
        bool safeStructure;
        bool safeTurret;
        bool safeWorld;
        //bool warningStarted;

        TimeSpan purgeBegin;
        TimeSpan purgeEnd;
        //int warningPeriod;

        protected override void LoadDefaultConfig()
        {
            // Options
            purgeAnimal = GetConfig("Purge Rules", "Animal Damage (true/false)", true);
            purgeHeli = GetConfig("Purge Rules", "Heli Damage (true/false)", true);
            purgeLoot = GetConfig("Purge Rules", "Loot Damage (true/false)", true);
            purgeStructure = GetConfig("Purge Rules", "Structure Damage (true/false)", true);
            purgeTurret = GetConfig("Purge Rules", "Turret Damage (true/false)", true);
            purgeWorld = GetConfig("Purge Rules", "World Damage (true/false)", true);
            realTime = GetConfig("Real Time (true/false)", false);
            safeAnimal = GetConfig("Safe Rules", "Animal Damage (true/false)", false);
            safeHeli = GetConfig("Safe Rules", "Heli Damage (true/false)", false);
            safeLoot = GetConfig("Safe Rules", "Loot Damage (true/false)", false);
            safeStructure = GetConfig("Safe Rules", "Structure Damage (true/false)", false);
            safeTurret = GetConfig("Safe Rules", "Turret Damage (true/false)", false);
            safeWorld = GetConfig("Safe Rules", "World Damage (true/false)", true);

            // Settings
            TimeSpan.TryParse(GetConfig("Purge Time (24-hour format)", "Begin (00:00:00)", "18:00:00"), out purgeBegin);
            TimeSpan.TryParse(GetConfig("Purge Time (24-hour format)", "End (00:00:00)", "06:00:00"), out purgeEnd);
            //warningPeriod = GetConfig("Warning Seconds (0 - 60)", 10);

            if (!configChanged) return;

            configChanged = false;
            SaveConfig();
        }

        void OnServerInitialized()
        {
            List<PlantEntity> list = Pool.GetList<PlantEntity>();
            foreach (var p in list)
            {
                PrintWarning(p.PrefabName);
            }

            LoadDefaultConfig();
            LoadDefaultMessages();
            permission.RegisterPermission(permAllow, this);
            permission.RegisterPermission(permProtect, this);
        }

        #endregion

        #region Localization

        void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PurgeEnded"] = "Purge has ended! PvP disabled",
                ["PurgeStarted"] = "Purge has begun! PvP enabled"
            }, this);

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PurgeEnded"] = "Purge est terminée ! PvP désactivé",
                ["PurgeStarted"] = "Purge a commencé ! PvP activé"
            }, this, "fr");

            // German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PurgeEnded"] = "Säuberung ist beendet! PvP deaktiviert",
                ["PurgeStarted"] = "Purge hat begonnen! PvP aktiviert"
            }, this, "de");

            // Russian
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PurgeEnded"] = "Продувка закончился! PvP отключен",
                ["PurgeStarted"] = "Продувка началась! PvP включен"
            }, this, "ru");

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PurgeEnded"] = "¡Ha terminado la purga! PvP desactivado",
                ["PurgeStarted"] = "¡Ha comenzado la purga! PvP activado"
            }, this, "es");
        }

        #endregion

        #region Purging

        /*bool PurgeWarning
        {
            get
            {
                var totalSeconds = purgeBegin.Subtract(server.Time.TimeOfDay).TotalSeconds;
                return totalSeconds > 0 && totalSeconds <= (warningPeriod * ConVar.Server.tickrate);
            }
        }*/

        bool PurgeTime
        {
            get
            {
                var time = realTime ? DateTime.Now.TimeOfDay : server.Time.TimeOfDay;
                return purgeBegin < purgeEnd ? time >= purgeBegin && time < purgeEnd : time >= purgeBegin || time < purgeEnd;
            }
        }

        void OnTick()
        {
            /*if (!purgeActive && PurgeWarning && !warningStarted)
            {
                warningStarted = true;
                var countdown = warningPeriod - 1;
                timer.Repeat((warningPeriod * ConVar.Server.tickrate) / 60, warningPeriod, () =>
                {
                    PrintWarning(countdown.ToString());
                    if (countdown == 0) warningStarted = false;
                    Puts($"Purge commencing in {countdown}...");
                    countdown--;
                });
                return;
            }*/

            if (PurgeTime && !purgeActive)
            {
                Puts(Lang("PurgeStarted"));
                Broadcast("PurgeStarted");
                purgeActive = true;
            }
            else if (!PurgeTime && purgeActive)
            {
                Puts(Lang("PurgeEnded"));
                Broadcast("PurgeEnded");
                purgeActive = false;
            }
        }

        void OnEntityTakeDamage(BaseEntity entity, HitInfo info)
        {
            var target = entity.PrefabName;
            var attacker = info.Initiator;

            if (purgeActive)
            {
                var player = entity.ToPlayer();
                if (player != null && !permission.UserHasPermission(player.UserIDString, permProtect)) return;

                if (purgeAnimal && (target.Contains("animals") || target.Contains("corpse") || (attacker != null && attacker.name.Contains("animals")))) return;
                if (purgeHeli && (entity is BaseHelicopter || attacker is BaseHelicopter)) return;
                if (purgeLoot && (target.Contains("loot") || target.Contains("barrel"))) return;
                if (purgeStructure && attacker != null && (entity is Barricade || entity is BuildingBlock || entity is Door || entity is SimpleBuildingBlock)) return;
                if (purgeTurret && (entity is AutoTurret || attacker is AutoTurret)) return;
                if (purgeWorld && (entity is BasePlayer && attacker == null)) return;
            }
            else
            {
                if (safeAnimal && (target.Contains("animals") || target.Contains("corpse") || (attacker != null && attacker.name.Contains("animals")))) return;
                if (safeHeli && (entity is BaseHelicopter || attacker is BaseHelicopter)) return;
                if (safeLoot && (target.Contains("loot") || target.Contains("barrel"))) return;
                if (safeStructure && attacker != null && (entity is Barricade || entity is BuildingBlock || entity is Door || entity is SimpleBuildingBlock)) return;
                if (safeTurret && (entity is AutoTurret || attacker is AutoTurret)) return;
                if (safeWorld && (entity is BasePlayer && attacker == null)) return;
            }

            info.damageTypes = new DamageTypeList();
            info.PointStart = Vector3.zero;
            info.HitMaterial = 0;
        }

        #endregion

        #region Helpers

        T GetConfig<T>(string parent, string key, T defaultValue)
        {
            var data = Config[parent] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[parent] = data;
                configChanged = true;
            }
            object value;
            if (!data.TryGetValue(key, out value))
            {
                value = defaultValue;
                data[key] = value;
                configChanged = true;
            }
            return (T)Convert.ChangeType(value, typeof(T));
        }

        T GetConfig<T>(string key, T defaultValue)
        {
            if (Config[key] == null)
            {
                Config[key] = defaultValue;
                configChanged = true;
                return defaultValue;
            }
            return (T)Convert.ChangeType(Config[key], typeof(T));
        }

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        void Broadcast(string key, params object[] args)
        {
            foreach (var player in players.Connected) player.Message(Lang(key, player.Id, args));
        }

        #endregion
    }
}