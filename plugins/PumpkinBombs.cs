using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("PumpkinBombs", "k1lly0u", "0.1.2", ResourceId = 2070)]
    class PumpkinBombs : RustPlugin
    {
        #region Fields      
        static PumpkinBombs ins; 
         
        private Dictionary<string, ItemDefinition> itemDefs;
        private List<ulong> craftedBombs;

        const string jackAngry = "jackolantern.angry";
        const string jackHappy = "jackolantern.happy";
        #endregion

        #region Oxide Hooks
        void Loaded()
        {
            permission.RegisterPermission("pumpkinbombs.use", this);
            permission.RegisterPermission("pumpkinbombs.free", this);
            lang.RegisterMessages(Messages, this);
            craftedBombs = new List<ulong>();
        }
        void OnServerInitialized()
        {
            ins = this;
            LoadVariables();
            itemDefs = ItemManager.itemList.ToDictionary(i => i.shortname);
        }
        void OnPlayerDisconnected(BasePlayer player)
        {
            if (craftedBombs.Contains(player.userID))
            {
                if (player != null)
                {
                    foreach (var item in configData.Costs)
                        player.GiveItem(ItemManager.CreateByItemID(itemDefs[item.Name].itemid, item.Amount), BaseEntity.GiveItemReason.PickedUp);
                }
                craftedBombs.Remove(player.userID);
            }
        }
        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity is BaseOven)
            {
                if (entity.ShortPrefabName == "jackolantern.happy" || entity.ShortPrefabName == "jackolantern.angry")
                {
                    var baseOven = entity as BaseOven;
                    if (craftedBombs.Contains(baseOven.OwnerID))
                    {
                        baseOven.gameObject.AddComponent<BombLight>();                        
                        craftedBombs.Remove(baseOven.OwnerID);
                    }
                }
            }
        }
        #endregion

        #region Helpers
        bool CanUse(BasePlayer player) => permission.UserHasPermission(player.UserIDString, "pumpkinbombs.use") || player.IsAdmin;
        bool IsFree(BasePlayer player) => permission.UserHasPermission(player.UserIDString, "pumpkinbombs.free");
        private bool HasEnoughRes(BasePlayer player, int itemid, int amount) => player.inventory.GetAmount(itemid) >= amount;
        private void TakeResources(BasePlayer player, int itemid, int amount) => player.inventory.Take(null, itemid, amount);
        #endregion

        #region Classes
        class BombLight : MonoBehaviour
        {
            private BaseOven entity;
            private bool lastOn;

            public void Awake()
            {
                entity = GetComponent<BaseOven>();
                lastOn = false;
                entity.SetFlag(BaseEntity.Flags.On, false);

                var expEnt = GameManager.server.CreateEntity("assets/prefabs/tools/c4/explosive.timed.deployed.prefab", entity.transform.position, new Quaternion(), true);
                TimedExplosive explosive = expEnt.GetComponent<TimedExplosive>();
                explosive.timerAmountMax = ins.configData.Explosives.Timer;
                explosive.timerAmountMin = ins.configData.Explosives.Timer;
                explosive.explosionRadius = ins.configData.Explosives.Radius;
                explosive.damageTypes = new List<Rust.DamageTypeEntry> { new Rust.DamageTypeEntry {amount = ins.configData.Explosives.Amount, type = Rust.DamageType.Explosion } };
                explosive.Spawn();

                entity.InvokeRepeating(this.ToggleLight, 0.5f, 0.5f);
            }

            public void OnDestroy()
            {
                entity.CancelInvoke();
            }

            private void ToggleLight()
            {
                if (lastOn)                
                    entity.SetFlag(BaseEntity.Flags.On, false);
                else entity.SetFlag(BaseEntity.Flags.On, true);

                lastOn = !lastOn;
            }            
        }
        #endregion

        #region Chat Commands
        [ChatCommand("pb")]
        void cmdPB(BasePlayer player, string command, string[] args)
        {
            if (!CanUse(player)) return;
            if (craftedBombs.Contains(player.userID))
            {
                if (!HasEnoughRes(player, -1284735799, 1))
                {
                    SendReply(player, $"<color={configData.Main}>{msg("lostBomb", player.UserIDString)}</color>");
                    craftedBombs.Remove(player.userID);
                    return;
                }
                SendReply(player, $"<color={configData.Main}>{msg("alreadyhave", player.UserIDString)}</color>");
                return;
            }
            if (!IsFree(player))
            {
                bool canCraft = true;
                foreach (var item in configData.Costs)
                {
                    if (!HasEnoughRes(player, itemDefs[item.Name].itemid, item.Amount)) { canCraft = false; break; }
                }
                if (canCraft)
                {
                    foreach (var item in configData.Costs)
                        TakeResources(player, itemDefs[item.Name].itemid, item.Amount);
                }
                else
                {
                    SendReply(player, $"<color={configData.Main}>{msg("noRes", player.UserIDString)}</color>");
                    foreach (var item in configData.Costs)
                        SendReply(player, $"<color={configData.Main}>{item.Amount}x {itemDefs[item.Name].displayName.english}</color>");
                    return;
                }
            }
            craftedBombs.Add(player.userID);
            player.inventory.GiveItem(ItemManager.CreateByItemID(-1284735799, 1));
            SendReply(player, $"<color={configData.Main}>{msg("readyMsg", player.UserIDString)}</color>");
        }
        #endregion

        #region Config         
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Explosive Settings")]
            public Explosive Explosives { get; set; }
            [JsonProperty(PropertyName = "Crafting Costs")]
            public List<CraftCost> Costs { get; set; }
            [JsonProperty(PropertyName = "Message Color (hex)")]
            public string Main { get; set; }

            public class CraftCost
            {
                [JsonProperty(PropertyName = "Item shortname")]
                public string Name;
                [JsonProperty(PropertyName = "Amount required")]
                public int Amount;
            }
            public class Explosive
            {
                [JsonProperty(PropertyName = "Detonation timer (seconds)")]
                public int Timer { get; set; }
                [JsonProperty(PropertyName = "Explosive radius")]
                public float Radius { get; set; }
                [JsonProperty(PropertyName = "Damage amount")]
                public float Amount { get; set; }
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
                Costs = new List<ConfigData.CraftCost>
                {
                    new ConfigData.CraftCost
                    {
                        Amount = 1,
                        Name = "explosive.timed"
                    },
                    new ConfigData.CraftCost
                    {
                        Amount = 1,
                        Name = "pumpkin"
                    }
                },
                Explosives = new ConfigData.Explosive
                {
                    Timer = 10,
                    Radius = 5,
                    Amount = 70
                },
                Main = "#D85540"
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Messaging
        string msg(string key, string id = null) => lang.GetMessage(key, this, id);
        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            {"readyMsg","Your pumpkin bomb is ready. Simply place the Jack'O'Lantern you just received on the floor to activate it" },
            { "noRes","You do not have enough resources to create a pumpkin bomb. You will need the following;"},
            {"alreadyhave", "You already have a pumpkin bomb ready for deployment" },
            {"lostBomb", "It seems you have lost your bomb. Now you must create a new one..." }
        };
        #endregion
    }
}