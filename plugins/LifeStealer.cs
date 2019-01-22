using System;
using System.Collections.Generic;
using Rust;

namespace Oxide.Plugins
{
    [Info("LifeStealer", "redBDGR", "1.0.5")]
    [Description("Give players health when they damage someone")]

    class LifeStealer : RustPlugin
    {
        private const string permissionName = "lifestealer.use";
        private List<object> animals;
        public bool animalsEnabled;
        private bool playersEnabled;

        public double healPercent = 0.5;
        public float staticHealAmount = 5.0f;
        public bool staticHealEnabled;

        private static List<object> AnimalList()
        {
            var al = new List<object> {"boar", "horse", "stag", "chicken", "wolf", "bear"};
            return al;
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (animals.Contains(entity.ShortPrefabName))
                goto skip;
            if (!(entity is BasePlayer))
                return;
            if (!(info.Initiator is BasePlayer))
                return;
            if (playersEnabled == false)
                return;
            skip:
            if (info.damageTypes.GetMajorityDamageType() == DamageType.Bleeding) return;

            var initPlayer = info.InitiatorPlayer;
            if (initPlayer == null) return;
            if (!permission.UserHasPermission(initPlayer.UserIDString, permissionName)) return;

            // Static Healing
            if (staticHealEnabled)
            {
                initPlayer.Heal(staticHealAmount);
                return;
            }

            // % Healing
            var healAmount = Convert.ToSingle(info.damageTypes.Total() * healPercent);
            if (!initPlayer.IsConnected) return;
            if (healAmount < 1) return;
            initPlayer.Heal(healAmount);
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        #region config

        private void LoadVariables()
        {
            healPercent = Convert.ToSingle(GetConfig("Settings", "Heal Percent", 0.5f));
            staticHealAmount = Convert.ToSingle(GetConfig("Static Heal", "Static Heal Amount", 5.0f));
            staticHealEnabled = Convert.ToBoolean(GetConfig("Static Heal", "Static Heal Enabled", false));
            animalsEnabled = Convert.ToBoolean(GetConfig("Settings", "animals Enabled", false));
            playersEnabled = Convert.ToBoolean(GetConfig("Settings", "Players Enabeld", true));
            animals = (List<object>) GetConfig("Settings", "Enabled animals", AnimalList());

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private void Init()
        {
            LoadVariables();
            permission.RegisterPermission(permissionName, this);
            Puts("Still works");
        }

        private bool Changed;

        #endregion
    }
}