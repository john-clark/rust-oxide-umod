using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Extended Recycler", "Orange", "1.0.5")]
    [Description("Extend recyclers for personal use and more")]
    public class ExtendedRecycler : RustPlugin
    {
        #region Vars

        private const ulong skinID = 1594245394;
        private const string prefab = "assets/bundled/prefabs/static/recycler_static.prefab";
        private static ExtendedRecycler plugin;
        private const string permUse = "extendedrecycler.use";

        #endregion

        #region Config
        
        private static ConfigData config;
        
        private class ConfigData
        {    
            [JsonProperty(PropertyName = "1. Pickup settings:")]
            public OPickup pickup;
            
            [JsonProperty(PropertyName = "2. Craft settings:")]
            public OCraft craft;

            [JsonProperty(PropertyName = "3. Destroy settings:")]
            public ODestroy destroy;

            public class OPickup
            {
                [JsonProperty(PropertyName = "1. Enabled for personal recyclers (placed by player)")]
                public bool personal;

                [JsonProperty(PropertyName = "2. Check ability to build for pickup")]
                public bool privilege;
                
                [JsonProperty(PropertyName = "3. Only owner can pickup")]
                public bool onlyOwner;
            }

            public class OCraft
            {
                [JsonProperty(PropertyName = "1. Enabled")]
                public bool enabled;
                
                [JsonProperty(PropertyName = "2. Cost (shortname - amount):")]
                public Dictionary<string, int> cost;
            }

            public class ODestroy
            {
                [JsonProperty(PropertyName = "1. Check ground for recyclers (destroy on missing)")]
                public bool checkGround;
                
                [JsonProperty(PropertyName = "2. Give item on destroy recycler")]
                public bool destroyItem;

                [JsonProperty(PropertyName = "3. Effects on destroy recycler")]
                public List<string> effects;
            }
        }
        
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                pickup = new ConfigData.OPickup
                {
                    personal = false,
                    privilege = true,
                    onlyOwner = false
                },
                craft = new ConfigData.OCraft
                {
                    enabled = true,
                    cost = new Dictionary<string, int>
                    {
                        {"scrap", 500},
                        {"metal.fragments", 5000},
                        {"metal.refined", 50},
                        {"gears", 10}
                    }
                },
                destroy = new ConfigData.ODestroy
                {
                    checkGround = true,
                    destroyItem = true,
                    effects = new List<string>
                    {
                        "assets/bundled/prefabs/fx/item_break.prefab",
                        "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
                    }
                }
            };
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
   
            try
            {
                config = Config.ReadObject<ConfigData>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintError("Configuration file is corrupt(or not exists), creating new one!");
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region Language

        private Dictionary<string, string> EN = new Dictionary<string, string>
        {
            {"Name", "Recycler"},
            {"Pickup", "You picked up recycler!"},
            {"Receive", "You received recycler!"},
            {"Disabled", "Pickup disabled!"},
            {"Build", "You must have ability to build to do that!"},
            {"Damaged", "Recycler was recently damaged, you can pick it up in next 30s!"},
            {"NoCraft", "Craft disabled!"},
            {"Owner", "Only owner can pickup recycler!"},
            {"Craft", "For craft you need more resources:\n{0}"},
            {"Permission", "You need permission to do that!"}
        };

        private void message(BasePlayer player, string key, params object[] args)
        {
            var message = string.Format(lang.GetMessage(key, this, player.UserIDString), args);
            player.ChatMessage(message);
        }

        #endregion

        #region Oxide Hooks
        
        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            CheckDeploy(go.ToBaseEntity());
        }

        private void OnServerInitialized()
        {
            plugin = this;
            lang.RegisterMessages(EN, this);
            permission.RegisterPermission(permUse, this);
            CheckRecyclers();
        }
        
        private void OnHammerHit(BasePlayer player, HitInfo info)
        {
            CheckHit(player, info?.HitEntity);
        }
        
        #endregion

        #region Core

        private void SpawnRecycler(Vector3 position, Quaternion rotation = default(Quaternion), ulong ownerID = 0)
        {
            var recycler = GameManager.server.CreateEntity(prefab, position, rotation);
            if (recycler == null) {return;}
            recycler.skinID = skinID;
            recycler.OwnerID = ownerID;
            recycler.gameObject.AddComponent<ExtendedRecyclerComponent>();
            recycler.Spawn();
        }

        private void CheckRecyclers()
        {
            foreach (var recycler in UnityEngine.Object.FindObjectsOfType<Recycler>())
            {
                if (recycler.OwnerID != 0 && recycler.GetComponent<ExtendedRecyclerComponent>() == null)
                {
                    recycler.gameObject.AddComponent<ExtendedRecyclerComponent>();
                }
            }
        }
        
        private void GiveRecycler(BasePlayer player, bool pickup = false)
        {
            var item = CreateItem();
            player.inventory.GiveItem(item);
            message(player, pickup ? "Pickup" : "Receive");
        }

        private void GiveRecycler(Vector3 position)
        {
            var item = CreateItem();
            item.Drop(position, Vector3.down);
        }

        private Item CreateItem()
        {
            var item = ItemManager.CreateByName("box.repair.bench", 1, skinID);
            if (item == null)
            {
                return null;
            }
            
            item.name = plugin.GetRecyclerName();
            return item;
        }
        
        private void CheckDeploy(BaseEntity entity)
        {
            if (entity == null) {return;}
            if (!IsRecycler(entity.skinID)) {return;}
            SpawnRecycler(entity.transform.position, entity.transform.rotation, entity.OwnerID);
            entity.Kill();
        }
        
        private void CheckHit(BasePlayer player, BaseEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            if (!IsRecycler(entity.skinID))
            {
                return;
            }
            
            entity.GetComponent<ExtendedRecyclerComponent>()?.TryPickup(player);
        }

        [ChatCommand("recycler.craft")]
        private void Craft(BasePlayer player)
        {
            if (CanCraft(player))
            {
                GiveRecycler(player);
            }
        }
        
        private bool CanCraft(BasePlayer player)
        {
            if (!config.craft.enabled)
            {
                message(player, "NoCraft");
                return false;
            }

            if (!permission.UserHasPermission(player.UserIDString, permUse))
            {
                message(player, "Permission");
                return false;
            }
            
            var recipe = config.craft.cost;
            var more = new Dictionary<string, int>();

            foreach (var component in recipe)
            {
                var name = component.Key;
                var has = player.inventory.GetAmount(ItemManager.FindItemDefinition(component.Key).itemid);
                var need = component.Value;
                if (has < component.Value)
                {
                    if (!more.ContainsKey(name))
                    {
                        more.Add(name, 0);
                    }

                    more[name] += need;
                }
            }

            if (more.Count == 0)
            {
                foreach (var item in recipe)
                {
                    player.inventory.Take(null, ItemManager.FindItemDefinition(item.Key).itemid, item.Value);
                }

                return true;
            }
            else
            {
                var text = "";

                foreach (var item in more)
                {
                    text += $" * {item.Key} x{item.Value}\n";
                }
                
                player.ChatMessage(string.Format(lang.GetMessage("Craft", this), text));
                return false;
            }
        }

        #endregion

        #region Helpers

        private string GetRecyclerName()
        {
            return lang.GetMessage("Name", this);
        }

        private bool IsRecycler(ulong skin)
        {
            return skin != 0 && skin == skinID;
        }

        #endregion

        #region Command

        [ConsoleCommand("recycler.give")]
        private void Cmd(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin && arg.Args?.Length > 0)
            {
                var player = BasePlayer.Find(arg.Args[0]) ?? BasePlayer.FindSleeping(arg.Args[0]);
                if (player == null)
                {
                    PrintWarning($"We can't find player with that name/ID! {arg.Args[0]}");
                    return;
                }

                GiveRecycler(player);
            }
        }

        #endregion

        #region Scripts

        private class ExtendedRecyclerComponent : MonoBehaviour
        {
            private Recycler recycler;

            private void Awake()
            {
                recycler = GetComponent<Recycler>();

                if (config.destroy.checkGround)
                {
                    InvokeRepeating("CheckGround", 5f, 5f);
                }
            }

            private void CheckGround()
            {
                RaycastHit rhit;
                var cast = Physics.Raycast(recycler.transform.position + new Vector3(0, 0.1f, 0), Vector3.down, out rhit, 4f, LayerMask.GetMask("Terrain", "Construction"));
                var distance =  cast ? rhit.distance : 3f;

                if (distance > 0.2f)
                {
                    GroundMissing();
                }
            }

            private void GroundMissing()
            {
                recycler.Kill();

                if (config.destroy.destroyItem)
                {
                    plugin.GiveRecycler(recycler.transform.position);
                }
                
                foreach (var effect in config.destroy.effects)
                {
                    Effect.server.Run(effect, recycler.transform.position);
                }
            }

            public void TryPickup(BasePlayer player)
            {
                if (config.pickup.personal == false)
                {
                    plugin.message(player, "Disabled");
                    return;
                }

                if (config.pickup.privilege && !player.CanBuild())
                {
                    plugin.message(player, "Build");
                    return;
                }

                if (config.pickup.onlyOwner && recycler.OwnerID != player.userID)
                {
                    plugin.message(player, "Owner");
                    return;
                }

                if (recycler.SecondsSinceDealtDamage < 30f)
                {
                    plugin.message(player, "Damaged");
                    return;
                }
                
                recycler.Kill();
                plugin.GiveRecycler(player, true);
            }

            public void DoDestroy()
            {
                Destroy(this);
            }
        }

        #endregion
    }
}