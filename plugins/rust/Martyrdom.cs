using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Martyrdom", "k1lly0u", "0.2.1")]
    [Description("Much like the Martyrdom perk in COD, allows players with permission to drop various explosives when they are killed")]
    class Martyrdom : RustPlugin
    {
        #region Fields
        private Dictionary<ulong, ExplosiveType> registeredMartyrs;
        private Dictionary<ExplosiveType, ExplosiveInfo> explosiveInfo;
        #endregion

        #region Oxide Hooks
        private void Init()
        {
            registeredMartyrs = new Dictionary<ulong, ExplosiveType>();
            explosiveInfo = new Dictionary<ExplosiveType, ExplosiveInfo>();
            permission.RegisterPermission("martyrdom.grenade", this);
            permission.RegisterPermission("martyrdom.beancan", this);
            permission.RegisterPermission("martyrdom.explosive", this);
            lang.RegisterMessages(messages, this);
        }

        private void OnServerInitialized()
        {
            LoadVariables();
            SetExplosiveInfo();
        }

        private void OnEntityDeath(BaseEntity entity, HitInfo info)
        {
            if (entity == null)
                return;

            BasePlayer player = entity.ToPlayer();
            if (player != null)
            {
                if (registeredMartyrs.ContainsKey(player.userID))                
                    TryDropExplosive(player);                
            }
        }
        #endregion

        #region Functions
        private void TryDropExplosive(BasePlayer player)
        {
            ExplosiveType type = registeredMartyrs[player.userID];
            if (HasPerm(player, type))
            {
                if (HasEnoughRes(player, explosiveInfo[type].ItemID, 1))
                {
                    TakeResources(player, explosiveInfo[type].ItemID, 1);
                    CreateExplosive(type, player);
                    registeredMartyrs.Remove(player.userID);
                    return;
                }
            }            
        }
        private void CreateExplosive(ExplosiveType type, BasePlayer player)
        {
            ExplosiveInfo info = explosiveInfo[type];
            BaseEntity entity = GameManager.server.CreateEntity(info.PrefabName, player.transform.position + new Vector3(0, 1.5f, 0), new Quaternion(), true);
            entity.OwnerID = player.userID;
            entity.creatorEntity = player;

            TimedExplosive explosive = entity.GetComponent<TimedExplosive>();
            explosive.timerAmountMax = info.Fuse;
            explosive.timerAmountMin = info.Fuse;
            explosive.explosionRadius = info.Radius;
            explosive.damageTypes = new List<Rust.DamageTypeEntry> { new Rust.DamageTypeEntry { amount = info.Damage, type = Rust.DamageType.Explosion } };
            explosive.Spawn();
        }
        #endregion

        #region Helpers
        private bool HasEnoughRes(BasePlayer player, int itemid, int amount) => player.inventory.GetAmount(itemid) >= amount;

        private void TakeResources(BasePlayer player, int itemid, int amount) => player.inventory.Take(null, itemid, amount);

        private bool HasPerm(BasePlayer player, ExplosiveType type)
        {
            switch (type)
            {
                case ExplosiveType.Grenade:
                    return permission.UserHasPermission(player.UserIDString, "martyrdom.grenade");
                case ExplosiveType.Beancan:
                    return permission.UserHasPermission(player.UserIDString, "martyrdom.beancan");
                case ExplosiveType.Explosive:
                    return permission.UserHasPermission(player.UserIDString, "martyrdom.explosive");                
            }
            return false;
        }

        private bool HasAnyPerm(BasePlayer player) => permission.UserHasPermission(player.UserIDString, "martyrdom.grenade") || permission.UserHasPermission(player.UserIDString, "martyrdom.beancan") || permission.UserHasPermission(player.UserIDString, "martyrdom.explosive");
        #endregion

        #region Explosive Info       
        private void SetExplosiveInfo()
        {
            if (configData.Beancan.Activated)
                explosiveInfo.Add(ExplosiveType.Beancan, new ExplosiveInfo { ItemID = 384204160, PrefabName = "assets/prefabs/weapons/beancan grenade/grenade.beancan.deployed.prefab", Damage = configData.Beancan.Damage, Fuse = configData.Beancan.Fuse, Radius = configData.Beancan.Radius });
            if (configData.Grenade.Activated)
                explosiveInfo.Add(ExplosiveType.Grenade, new ExplosiveInfo { ItemID = -1308622549, PrefabName = "assets/prefabs/weapons/f1 grenade/grenade.f1.deployed.prefab", Damage = configData.Grenade.Damage, Fuse = configData.Grenade.Fuse, Radius = configData.Grenade.Radius });
            if (configData.Explosive.Activated)
                explosiveInfo.Add(ExplosiveType.Explosive, new ExplosiveInfo { ItemID = 498591726, PrefabName = "assets/prefabs/tools/c4/explosive.timed.deployed.prefab", Damage = configData.Explosive.Damage, Fuse = configData.Explosive.Fuse, Radius = configData.Explosive.Radius });
        }

        private class ExplosiveInfo
        {
            public int ItemID;
            public string PrefabName;
            public float Damage;
            public float Radius;
            public float Fuse;
        }

        enum ExplosiveType
        {
            Grenade,
            Beancan,
            Explosive
        }
        #endregion

        #region Chat Commands        
        [ChatCommand("m")]
        private void cmdM(BasePlayer player, string command, string[] args)
        {
            if (!HasAnyPerm(player)) return;
            if (args == null || args.Length == 0)
            {
                if (HasPerm(player, ExplosiveType.Beancan) && configData.Beancan.Activated)
                    SendReply(player, "/m beancan");
                if (HasPerm(player, ExplosiveType.Grenade) && configData.Grenade.Activated)
                    SendReply(player, "/m grenade");
                if (HasPerm(player, ExplosiveType.Explosive) && configData.Explosive.Activated)
                    SendReply(player, "/m explosive");
                SendReply(player, "/m disable");
                return;
            }
            switch (args[0].ToLower())
            {
                case "beancan":
                    if (HasPerm(player, ExplosiveType.Beancan))
                    {
                        if (!registeredMartyrs.ContainsKey(player.userID))
                            registeredMartyrs.Add(player.userID, ExplosiveType.Beancan);
                        else registeredMartyrs[player.userID] = ExplosiveType.Beancan;
                        SendReply(player, msg("beanAct", player.UserIDString));
                    }
                    return;
                case "grenade":
                    if (HasPerm(player, ExplosiveType.Grenade))
                    {
                        if (!registeredMartyrs.ContainsKey(player.userID))
                            registeredMartyrs.Add(player.userID, ExplosiveType.Grenade);
                        else registeredMartyrs[player.userID] = ExplosiveType.Grenade;
                        SendReply(player, msg("grenAct", player.UserIDString));
                    }
                    return;
                case "explosive":
                    if (HasPerm(player, ExplosiveType.Explosive))
                    {
                        if (!registeredMartyrs.ContainsKey(player.userID))
                            registeredMartyrs.Add(player.userID, ExplosiveType.Explosive);
                        else registeredMartyrs[player.userID] = ExplosiveType.Explosive;
                        SendReply(player, msg("expAct", player.UserIDString));
                    }
                    return;
                case "disable":
                    if (registeredMartyrs.ContainsKey(player.userID))
                    {
                        registeredMartyrs.Remove(player.userID);
                        SendReply(player, msg("marDis", player.UserIDString));
                        return;
                    }
                    else SendReply(player, msg("notAct", player.UserIDString));
                    return;
                default:
                    break;
            }
        }
        #endregion

        #region Config        
        private ConfigData configData;    
        
        private class ConfigData
        {
            public ExpType Grenade { get; set; }
            public ExpType Beancan { get; set; }
            public ExpType Explosive { get; set; }

            public class ExpType
            {
                public bool Activated { get; set; }
                public float Damage { get; set; }
                public float Radius { get; set; }
                public float Fuse { get; set; }
            }
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                Beancan = new ConfigData.ExpType
                {
                    Activated = true,
                    Damage = 15f,
                    Fuse = 2f,
                    Radius = 4.5f
                },
                Grenade = new ConfigData.ExpType
                {
                    Activated = true,
                    Damage = 40f,
                    Fuse = 2f,
                    Radius = 4.5f
                },
                Explosive = new ConfigData.ExpType
                {
                    Activated = true,
                    Damage = 500,
                    Fuse = 3,
                    Radius = 10f
                }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();

        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Localization
        private string msg(string key, string id = null) => lang.GetMessage(key, this, id);

        private Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"beanAct", "You have activated the beancan Martyr drop" },
            {"grenAct", "You have activated the grenade Martyr drop" },
            {"expAct", "You have activated the explosive Martyr drop" },
            {"marDis", "You have disabled Martyrdom" },
            {"notAct", "You do not have Martyrdom activated" }
        };

        #endregion
    }
}
