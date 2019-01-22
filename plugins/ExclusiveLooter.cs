using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("ExclusiveLooter", "redBDGR", "1.0.0")]
    [Description("Allow only one player to loot an entity at a time")]

    class ExclusiveLooter : RustPlugin
    {
        private bool Changed;
        private const string permissionNameEXEMPT = "exclusivelooter.exempt";

        private Dictionary<BaseEntity, string> containerDic = new Dictionary<BaseEntity, string>();

        private List<object> entityBlacklist = new List<object>();
        static List<object> entityBlacklistGet()
        {
            var eb = new List<object>();
            eb.Add("woodbox_deployed");
            eb.Add("refinery_small_deployed");
            return eb;
        }

        #region Config

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {
            entityBlacklist = (List<object>)GetConfig("Settings", "Entity Blacklist", entityBlacklistGet());

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            permission.RegisterPermission(permissionNameEXEMPT, this);
            LoadVariables();

            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["Not Allowed"] = "You are not allowed to loot this object because someone else is already looting it!",
            }, this);
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (permission.UserHasPermission(player.UserIDString, permissionNameEXEMPT))
                return;
            if (entityBlacklist.Contains(entity.ShortPrefabName))
                return;
            StorageContainer container = entity.GetComponent<StorageContainer>();
            if (container)
                if (!container.IsLocked())
                    NextTick(() => container.SetFlag(BaseEntity.Flags.Locked, true));
        }

        private void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            if (permission.UserHasPermission(player.UserIDString, permissionNameEXEMPT))
                return;
            if (entityBlacklist.Contains(entity.ShortPrefabName))
                return;
            StorageContainer container = entity.GetComponent<StorageContainer>();
            if (container)
                if (container.IsLocked())
                    NextTick(() => container.SetFlag(BaseEntity.Flags.Locked, false));
        }

        #endregion

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
            if (data.TryGetValue(datavalue, out value)) return value;
            value = defaultValue;
            data[datavalue] = value;
            Changed = true;
            return value;
        }

        private string msg(string key, string id = null) => lang.GetMessage(key, this, id);
    }
}