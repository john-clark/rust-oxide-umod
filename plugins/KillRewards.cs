using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("KillRewards", "CodeHarbour.com", "1.1.0")]
    class KillRewards : RustPlugin
    {
        PluginConfig config;
		
		void Init()
		{
			LoadDefaultConfig();
			LoadConfig();
		}

        void OnEntityDeath(BaseEntity entity, HitInfo info)
        {
            var victim = entity as BasePlayer;
            if (victim == null) return;
            var killer = info?.Initiator as BasePlayer;
            if (killer == null) return;

            if (config.Settings.Health > 0)
                killer.health = config.Settings.Health;

            if (config.Settings.OnlyReloadCurrentWeapon)
            {
                (killer.GetHeldEntity() as BaseProjectile).primaryMagazine.contents = (killer.GetHeldEntity() as BaseProjectile).primaryMagazine.capacity;
                (killer.GetHeldEntity() as BaseProjectile).SendNetworkUpdateImmediate();
                return;
            }

            foreach (KeyValuePair<string, int> entry in config.Settings.Weapons)
            {
                var item = ItemManager.CreateByName(entry.Key);
                if (item == null)
                {
                    PrintWarning($"Invalid item '{entry.Key}' - Skipping item!");
                    continue;
                }

                var weapon = item.GetHeldEntity() as BaseProjectile;
                if (weapon != null)
                {
                    (item.GetHeldEntity() as BaseProjectile).primaryMagazine.contents = entry.Value;
                }

                killer.inventory.GiveItem(item, killer.inventory.containerBelt);
            }

            foreach (var itemData in config.Settings.Items)
            {
                var item = ItemManager.CreateByName(itemData.ItemShortname, itemData.Amount);
                if (item == null)
                {
                    PrintWarning($"Invalid item '{itemData.ItemShortname}' - Skipping item!");
                    continue;
                }

                item.skin = itemData.skinID;

                ItemContainer container = killer.inventory.containerMain;

                if (itemData.Container == "belt") container = killer.inventory.containerBelt;
                else if (itemData.Container == "wear") container = killer.inventory.containerWear;

                killer.inventory.GiveItem(item, container);
            }
        }

        public class SettingsClass
        {
            public float Health;
            public bool OnlyReloadCurrentWeapon;

            public Dictionary<string, int> Weapons = new Dictionary<string, int>();
            public List<ItemData> Items = new List<ItemData>();
        }

        public class ItemData
        {
            public int Amount;
            public string Container, ItemShortname;
            public ulong skinID = 0;
        }

        public class PluginConfig
        {
            public SettingsClass Settings;

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig
                {
                    Settings = new SettingsClass
                    {
                        Health = 100.0f,
                        OnlyReloadCurrentWeapon = true,
                        Weapons = new Dictionary<string, int>()
                        {
                            { "pistol.semiauto", 10 },
                            { "rifle.ak", 30 }
                        },
                        Items = new List<ItemData>()
                        {
                            new ItemData()
                            {
                                ItemShortname = "largemedkit",
                                Amount = 1,
                                Container = "belt",
                                skinID = 0
                            },
                            new ItemData()
                            {
                                ItemShortname = "syringe.medical",
                                Amount = 2,
                                Container = "belt",
                                skinID = 0
                            },
                            new ItemData()
                            {
                                ItemShortname = "hoodie",
                                Amount = 1,
                                Container = "wear",
                                skinID = 0
                            },
                        }
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = PluginConfig.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config);
    }
}