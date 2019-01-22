using System;
using System.Collections.Generic;
using System.Reflection;

namespace Oxide.Plugins
{
    [Info("VisionLocks", "Maurice", "1.0.1", ResourceId = 1654)]
    [Description("Prevents damage on anything that is locked")]
    class VisionLocks : RustPlugin
    {

        #region Variables

        private bool Changed;

        private bool usepermission;

        private FieldInfo codelockwhitelist;

        #endregion

        #region Server

        private void OnServerInitialized()
        {
            LoadVariables();
        }

        void Loaded()
        {

            permission.RegisterPermission("visionlocks.allow", this);

            codelockwhitelist = typeof(CodeLock).GetField("whitelistPlayers", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));

        }

        #endregion

        #region Methods

        #region EntityTakeDamage

        void OnEntityTakeDamage(BaseCombatEntity entity)
        {
            var @lock = entity.GetSlot(0);
            if (@lock != null && @lock.IsLocked())
            {

                if(usepermission)
                {

                    List<ulong> whitelisted = codelockwhitelist.GetValue(@lock) as List<ulong>;

                    if (whitelisted.Count >= 1)
                    {

                        if (permission.UserHasPermission(whitelisted[0].ToString(), "visionlocks.allow"))
                        {

                            entity.lastDamage = 0;

                        }

                    }

                }
                else
                {

                    entity.lastDamage = 0;

                }


            }

        }

        #endregion

        #region Config

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        void LoadVariables()
        {

            usepermission = Convert.ToBoolean(GetConfig("Generic", "Use Permission", true));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
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

        #endregion

        #endregion

    }

}
