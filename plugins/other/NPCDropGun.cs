using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NPC Drop Gun", "2CHEVSKII", "1.1.0")]
    [Description("Forces NPC to drop used gun and some ammo after death")]
    class NPCDropGun : RustPlugin
    {
        /*TODO:
         * Make configuration [x]
         * Optimize code a bit [x]
         * Add new features? [?]
         */
        #region [Config]
        protected class Configuration
        {
            public int minAmmo;
            public int maxAmmo;
            public float minCondition;
            public float maxCondition;
        }
        Configuration configuration = new Configuration();
        void SaveConfig(object configuration) => Config.WriteObject(configuration, true);
        void LoadCFGVars() => configuration = Config.ReadObject<Configuration>();
        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            PrintWarning("New config file generated...");
            var configuration = new Configuration
            {
                minAmmo = 10,
                maxAmmo = 120,
                minCondition = 30f,
                maxCondition = 150f
            };
            SaveConfig(configuration);
        }
        private void LoadVariables()
        {
            LoadCFGVars();
            SaveConfig();
        }
        #endregion
        #region [Global variables]
        int ammoType;
        string heldWeapon;
        #endregion
        #region [Oxide hooks]
        void Init()
        {
            LoadVariables();
            SaveConfig();
        }
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if(info != null && entity != null)
            {
                if(entity is HTNPlayer || entity is Scientist)
                {
                    BasePlayer basePlayer = entity as BasePlayer;
                    if(basePlayer != null)
                        ItemSpawner(basePlayer.GetHeldEntity(), basePlayer.ServerPosition);
                }
            }
        }
        void OnEntitySpawned(BaseNetworkable entity)
        {
            if(entity is NPCPlayerCorpse)
                MethodThatPutsLootIntoCorpse(entity);
        }
        #endregion
        #region [Helpers]
        void AmmoAssigner(string ammoString)
        {
            switch(ammoString)
            {
                case "lr300.entity":
                    ammoType = -1211166256;
                    break;
                case "ak47u.entity":
                    ammoType = -1211166256;
                    break;
                case "semi_auto_rifle.entity":
                    ammoType = -1211166256;
                    break;
                case "m249.entity":
                    ammoType = -1211166256;
                    break;
                case "spas12.entity":
                    ammoType = -1685290200;
                    break;
                case "m92.entity":
                    ammoType = 785728077;
                    break;
                case "mp5.entity":
                    ammoType = 785728077;
                    break;
                default:
                    ammoType = 0;
                    break;
            }
        }
        void ItemSpawner(BaseEntity heldEntity, Vector3 dropPosition)
        {
            if(heldEntity != null)
            {
                Item weapon = ItemManager.CreateByItemID(heldEntity.GetItem().info.itemid);
                if(weapon !=null)
                {
                    weapon.condition = UnityEngine.Random.Range(configuration.minCondition, configuration.maxCondition);
                    ItemContainer container = new ItemContainer();
                    container.Insert(weapon);
                    DropUtil.DropItems(container, dropPosition);
                    heldWeapon = heldEntity.ShortPrefabName;
                    AmmoAssigner(heldWeapon);
                }
            }
        }
        void MethodThatPutsLootIntoCorpse(BaseNetworkable entity)
        {
            if(entity != null && ammoType != 0)
            {
                Item ammo = ItemManager.CreateByItemID(ammoType, Random.Range(configuration.minAmmo, configuration.maxAmmo));
                PlayerCorpse corpse = entity.GetComponent<PlayerCorpse>();
                NextTick(() => {
                    if(ammo != null && corpse != null)
                        ammo.MoveToContainer(corpse.containers[0]);
                });
            }
        }
        #endregion
    }
}
