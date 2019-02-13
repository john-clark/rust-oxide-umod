using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("ResearchControl", "Vlad-00003", "1.0.4")]
    [Description("Allow you to adjust price for a research")]
    /*
     * Author info:
     *   E-mail: Vlad-00003@mail.ru
     *   Vk: vk.com/vlad_00003
     */
    class ResearchControl : RustPlugin
    {
        #region vars
        private PluginConfig config;
        #endregion

        #region Config
        private class CustomPermission
        {
            [JsonProperty("Research cost modifier")]
            public int PriceModifier;
            [JsonProperty("Research speed modifier")]
            public decimal ResearchSpeed;
            [JsonProperty("The speed is absolute value, not modifier")]
            public bool SpeedIsAbsolute;
            public CustomPermission(int PriceModifier, decimal ResearchSpeed, bool ModifierIsSpeed)
            {
                this.PriceModifier = PriceModifier;
                this.ResearchSpeed = ResearchSpeed;
                this.SpeedIsAbsolute = ModifierIsSpeed;
            }
        }
        private class ItemConfig
        {
            [JsonProperty("Research cost")]
            public int Cost;
            [JsonProperty("Research speed")]
            public decimal Speed;
            public ItemConfig(int Cost, decimal Speed)
            {
                this.Cost = Cost;
                this.Speed = Speed;
            }
        }
        private class PluginConfig
        {
            [JsonProperty("Custom permission multipliers")]
            public Dictionary<string, CustomPermission> Permissions = new Dictionary<string, CustomPermission>()
            {
                ["researchcontrol.vip"] = new CustomPermission(70, 70M, false),
                ["researchcontrol.gold"] = new CustomPermission(50, 50M, false),
                ["researchcontrol.punish"] = new CustomPermission(120, 40M, true)
            };
            [JsonProperty("Default price list")]
            public Dictionary<string, Dictionary<string, ItemConfig>> _Prices = new Dictionary<string, Dictionary<string, ItemConfig>>();
            [JsonIgnore]
            public Dictionary<ItemDefinition, ItemConfig> Prices = new Dictionary<ItemDefinition, ItemConfig>();
        }
        #endregion

        #region Config initialization
        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig();
            var bplist = ItemManager.GetBlueprints();
            foreach (var bp in bplist)
            {
                if (bp.userCraftable && !bp.defaultBlueprint)
                {
                    if (!config._Prices.ContainsKey(bp.targetItem.category.ToString("F")))
                    {
                        config._Prices[bp.targetItem.category.ToString("F")] = new Dictionary<string, ItemConfig>() { [bp.targetItem.displayName.english] =
                            new ItemConfig(GetDefaultPrice(bp.targetItem), 10M) };
                    }
                    else
                    {
                        config._Prices[bp.targetItem.category.ToString("F")].Add(bp.targetItem.displayName.english, new ItemConfig(GetDefaultPrice(bp.targetItem), 10M));
                    }
                }
            }
            PrintWarning("Default config file created!");
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            foreach (var key in config.Permissions.Keys)
                permission.RegisterPermission(key, this);
            var itemdefs = ItemManager.GetItemDefinitions();
            foreach (var category in config._Prices)
            {
                foreach(var item in category.Value)
                {
                    ItemDefinition itemdef = itemdefs.Where(p => p.displayName.english == item.Key || p.shortname == item.Key).FirstOrDefault();
                    if (itemdef == null)
                    {
                        PrintWarning("No defenition found for item {0}", item.Key);
                        continue;
                    }
                    if (!config.Prices.ContainsKey(itemdef))
                    {
                        config.Prices[itemdef] = item.Value;
                    }
                    else
                    {
                        config.Prices[itemdef] = item.Value;
                    }
                }
            }
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        #endregion

        #region Oxide hooks
        void OnItemResearch(ResearchTable table, Item item, BasePlayer player)
        {
            if (!player)
            {
                table.researchDuration = 10;
                return;
            }
            var speed = GetPlayerSpeed(player);
            if (!speed.IsModifier)
                table.researchDuration = (float)speed.Speed;
            else
                if (config.Prices.ContainsKey(item.info))
                    table.researchDuration = (float)(config.Prices[item.info].Speed * speed.Speed);
                else
                    table.researchDuration = (float)(10 * speed.Speed);
        }
        int OnItemScrap(ResearchTable table, Item item)
        {
            decimal rate = GetPlayerRate(table.user);
            if (config.Prices.ContainsKey(item.info))
            {
                return (int)(config.Prices[item.info].Cost * rate);
            }
            else
            {
                PrintWarning("{0} has created blueprint for unlisted item - \"{1}\". Setting default price.", table.user == null ? "Unknown player" : $"Player \"{table.user.displayName}\"",
                    item.info.displayName.english);
                return (int)(GetDefaultPrice(item.info) * rate);
            }
        }
        #endregion

        #region Helpers
        decimal GetPlayerRate(BasePlayer player = null)
        {
            if (player == null)
                return 1M;
            var allowed = config.Permissions.Where(p => permission.UserHasPermission(player.UserIDString, p.Key)).Select(p => p.Value.PriceModifier / 100M);
            return allowed.Count() > 0 ? allowed.Aggregate((p1, p2) => p1 > p2 ? p2 : p1) : 1M;
        }
        private class PlayerSpeed
        {
            public decimal Speed;
            public bool IsModifier;
            public PlayerSpeed(decimal Speed, bool IsModifier)
            {
                this.Speed = Speed;
                this.IsModifier = IsModifier;
            }
        }
        PlayerSpeed GetPlayerSpeed(BasePlayer player = null)
        {
            if (player == null)
                return new PlayerSpeed(1, true);
            var allowed = config.Permissions.Where(p => permission.UserHasPermission(player.UserIDString, p.Key));
            if(allowed.Count() < 0)
                return new PlayerSpeed(1, true);
            if(allowed.Count() == 1)
            {
                var perm = allowed.First();
                if (perm.Value.SpeedIsAbsolute)
                    return new PlayerSpeed(perm.Value.ResearchSpeed, false);
                else
                    return new PlayerSpeed(perm.Value.ResearchSpeed / 100M, true);
            }
            var absolute = allowed.Where(p => p.Value.SpeedIsAbsolute).Select(p => p.Value.ResearchSpeed);
            if (absolute.Count() > 0)
            {
                return new PlayerSpeed(absolute.Aggregate((p1, p2) => p1 > p2 ? p2 : p1), false);
            }
            var mod = allowed.Where(p => !p.Value.SpeedIsAbsolute).Select(p => p.Value.ResearchSpeed / 100M);
            if(mod.Count() > 0)
            {
                return new PlayerSpeed(mod.Aggregate((p1, p2) => p1 > p2 ? p2 : p1), true);
            }
            return new PlayerSpeed(1, true);
        }
        public int GetDefaultPrice(ItemDefinition def)
        {
            int num = 0;
            if (def.rarity == Rust.Rarity.Common)
                num = 20;
            if (def.rarity == Rust.Rarity.Uncommon)
                num = 75;
            if (def.rarity == Rust.Rarity.Rare)
                num = 250;
            if (def.rarity == Rust.Rarity.VeryRare || def.rarity == Rust.Rarity.None)
                num = 750;
            return num;
        }
        #endregion
    }
}