using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    #region Plugin Information
    [Info("RustFallMobDrops", "Pois0n", "1.0")]
    [Description("Mob Drop Handler")]
    #endregion

    class RustfallMobDrops : RustPlugin
    {
        #region Globals
        private ConfigData _config { get; set; }
        #endregion

        #region Objects
        public class ConfigData
        {
            [JsonProperty(PropertyName = "Npcs")]
            public Dictionary<string, NPC> NPCs { get; set; }
            [JsonProperty(PropertyName = "Tiers")]
            public Dictionary<string, Tier> Tiers { get; set; }
            [JsonProperty(PropertyName = "Gear Sets")]
            public Dictionary<string, Gear> GearSets { get; set; }
        }

        public class Gear
        {
            [JsonProperty(PropertyName = "Gear")]
            public List<Drop> Drops { get; set; }
        }

        public class NPC
        {
            [JsonProperty(PropertyName = "Tiers")]
            public List<NPCTier> NPCTiers{get;set;}
            [JsonProperty(PropertyName = "GearSet")]
            public string GearSet { get; set; }
        }

        public class NPCTier
        {
            [JsonProperty(PropertyName = "Tier")]
            public string TierName { get; set; }
            [JsonProperty(PropertyName = "Number Of Drops")]
            public int DropAmount { get; set; }
            [JsonProperty(PropertyName = "Chance for drop (100 is guarenteed)")]
            public float DropChance { get; set; }
        }

        public class Tier
        {
            [JsonProperty(PropertyName = "Tier Drops")]
            public List<Drop> Drops { get; set; }
        }

        public class Drop
        {
            [JsonProperty(PropertyName = "Drop Name (Item ShortName)")]
            public string Shortname { get; set; }
            [JsonProperty(PropertyName = "Amount")]
            public int Amount { get; set; }
            [JsonProperty(PropertyName = "Drop Rarity")]
            public float  Rarity { get; set; }
            [JsonProperty(PropertyName = "Set Condition")]
            public bool SetCondition { get; set; }
            [JsonProperty(PropertyName = "MinCondition")]
            public float MinCondition { get; set; }
            [JsonProperty(PropertyName = "MaxCondition")]
            public float MaxCondition { get; set; }
            [JsonProperty(PropertyName = "Name (Leave Blank For Default)")]
            public string Name { get; set; }
            [JsonProperty(PropertyName = "Drop Skin")]
            public string Skin { get; set; }
        }

        public class DropRange
        {
            public float UpperBound;
            public float LowerBound;
            public Drop drop;
        }

        #endregion

        #region Oxide Hooks

        //Load Default Config
        protected override void LoadDefaultConfig()
        {
            Puts("is loading default config");
            //Load defaults into memory.
            InitDefaultConfig();
            //Save settings.
            SaveConfigData();
        }

        //Load Config
        void Loaded()
        {
            LoadConfigData();
        }

        //Handle Npc Drops
        void OnEntitySpawned(BaseEntity entity)
        {
            //Check that spawned entity is a corpse
            if (entity == null) return;
            if (entity is NPCPlayerCorpse)
            {
                var npcCorpse = entity as NPCPlayerCorpse;
                if (npcCorpse == null) return;
                if (_config.NPCs.ContainsKey(npcCorpse._playerName))
                {
                    var tiers = _config.NPCs[npcCorpse._playerName].NPCTiers;
                    List<Drop> itemsToDrop = new List<Drop>();
                    List<Drop> gearToDrop = new List<Drop>();

                    string gearSet = _config.NPCs[npcCorpse._playerName].GearSet;
                    itemsToDrop.AddRange(GetRandomGears(gearSet));

                    foreach(var tier in tiers)
                    {
                        itemsToDrop.AddRange(GetRandomDrops(tier.TierName, tier.DropChance, tier.DropAmount));
                    }

                    if(itemsToDrop.Count > 0)
                    {
                        timer.Once(1f, () =>
                        {
                            foreach (var drop in itemsToDrop)
                            {
                                try
                                {
                                    var ItemDef = ItemManager.FindItemDefinition(drop.Shortname);
                                    Item item = ItemManager.Create(ItemDef, drop.Amount, Convert.ToUInt64(drop.Skin));
                                    if (drop.SetCondition)
                                    {
                                        float condition = UnityEngine.Random.Range(drop.MinCondition, drop.MaxCondition);
                                        item.condition = condition;
                                    }
                                    if (drop.Name != "")
                                    {
                                        item.name = drop.Name;
                                    }
                                    item.MoveToContainer(npcCorpse.containers[0]);
                                }
                                catch (Exception ex)
                                {
                                    Debug.Log(ex);
                                }
                            }
                        });
                    }
                }
            }
        }

        #endregion

        #region Config

        private void InitDefaultConfig()
        {
            //Default config
            _config = new ConfigData
            {
                NPCs = new Dictionary<string, NPC>()
                {
                    {"Raging Marauder", new NPC()
                        {
                            NPCTiers =  new List<NPCTier>()
                            {
                                {
                                    new NPCTier() {
                                        TierName = "Tier1",
                                        DropAmount = 3,
                                        DropChance = 100
                                    }
                                },
                                {
                                    new NPCTier() {
                                        TierName = "Tier2",
                                        DropAmount = 1,
                                        DropChance = 25
                                    }
                                }
                            },
                            GearSet = "DHGear"
                        }
                    },
                    {"Desperate Highwayman", new NPC()
                        {
                            NPCTiers =  new List<NPCTier>()
                            {
                                {
                                    new NPCTier() {
                                        TierName = "Divine",
                                        DropAmount = 1,
                                        DropChance = 100
                                    }
                                }
                            },
                            GearSet = "DHGear"
                        }
                    }
                },
                Tiers = new Dictionary<string, Tier>()
                {
                    {
                        "Tier1", new Tier() {
                            Drops = new List<Drop>()
                            {
                                {
                                    new Drop()
                                    {
                                        Shortname = "gunpowder",
                                        Skin = "0",
                                        Rarity = 25,
                                        Amount = 10,
                                        MinCondition = 1,
                                        MaxCondition = 1,
                                        Name = "",
                                        SetCondition = false
                                    }
                                },
                                {
                                    new Drop()
                                    {
                                        Shortname = "hammer",
                                        Skin = "0",
                                        Rarity = 25,
                                        Amount = 1,
                                        MinCondition = 1,
                                        MaxCondition = 100,
                                        Name = "",
                                        SetCondition = false
                                    }
                                },
                                {
                                    new Drop()
                                    {
                                        Shortname = "scrap",
                                        Skin = "0",
                                        Rarity = 25,
                                        Amount = 5,
                                        MinCondition = 1,
                                        MaxCondition = 1,
                                        Name = "",
                                        SetCondition = false
                                    }
                                },
                                {
                                    new Drop()
                                    {
                                        Shortname = "scrap",
                                        Skin = "0",
                                        Rarity = 25,
                                        Amount = 10,
                                        MinCondition = 1,
                                        MaxCondition = 1,
                                        Name = "",
                                        SetCondition = false
                                    }
                                }
                            }
                        }
                    },
                    {
                        "Tier2", new Tier() {
                            Drops = new List<Drop>()
                            {
                                {
                                    new Drop()
                                    {
                                        Shortname = "gunpowder",
                                        Skin = "0",
                                        Rarity = 50,
                                        Amount = 50,
                                        MinCondition = 1,
                                        MaxCondition = 1,
                                        Name = "",
                                        SetCondition = false
                                    }
                                },
                                {
                                    new Drop()
                                    {
                                        Shortname = "hammer",
                                        Skin = "0",
                                        Rarity = 50,
                                        Amount = 1,
                                        MinCondition = 1,
                                        MaxCondition = 100,
                                        Name = "",
                                        SetCondition = false
                                    }
                                }
                            }
                        }
                    },                    
                    {
                        "Divine", new Tier() {
                            Drops = new List<Drop>()
                            {
                                {
                                    new Drop()
                                    {
                                        Shortname = "gunpowder",
                                        Skin = "0",
                                        Rarity = 100,
                                        Amount = 500,
                                        MinCondition = 1,
                                        MaxCondition = 1,
                                        Name = "",
                                        SetCondition = false
                                    }
                                }
                            }
                        }
                    }
                },
                GearSets = new Dictionary<string, Gear>()
                {
                    {
                        "DHGear", new Gear()
                        {
                            Drops = new List<Drop>()
                            {
                                {
                                    new Drop()
                                    {
                                        Shortname = "bow.hunting",
                                        Skin = "0",
                                        Rarity = 25,
                                        Amount = 1,
                                        MinCondition = 1,
                                        MaxCondition = 100,
                                        Name = "",
                                        SetCondition = true
                                    }
                                },
                                {
                                    new Drop()
                                    {
                                        Shortname = "attire.hide.boots",
                                        Skin = "0",
                                        Rarity = 25,
                                        Amount = 1,
                                        MinCondition = 1,
                                        MaxCondition = 100,
                                        Name = "",
                                        SetCondition = false
                                    }
                                },
                                {
                                    new Drop()
                                    {
                                        Shortname = "attire.hide.pants",
                                        Skin = "0",
                                        Rarity = 25,
                                        Amount = 1,
                                        MinCondition = 1,
                                        MaxCondition = 100,
                                        Name = "",
                                        SetCondition = false
                                    }
                                },
                                {
                                    new Drop()
                                    {
                                        Shortname = "attire.hide.poncho",
                                        Skin = "0",
                                        Rarity = 25,
                                        Amount = 1,
                                        MinCondition = 1,
                                        MaxCondition = 100,
                                        Name = "",
                                        SetCondition = true
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }

        private void SaveConfigData()
        {
            Puts("is saving.");
            Config.WriteObject(_config, true);
        }

        //Handles loading
        private void LoadConfigData()
        {
            Puts("is loading.");
            _config = new ConfigData();
            _config = Config.ReadObject<ConfigData>();
            Config.WriteObject(_config);
        }

        #endregion

        #region Helpers

        public List<Drop> GetRandomGears(string GearSet)
        {
            List<Drop> drops = new List<Drop>();
            if (!_config.GearSets.ContainsKey(GearSet))
            {
                return drops;
            }

            var gearItems = _config.GearSets[GearSet].Drops;

            foreach(var g in gearItems)
            {
                float chance = UnityEngine.Random.Range(1, 100f);
                if(g.Rarity >= chance)
                {
                    drops.Add(g);
                }
            }

            return drops;
        }

        public List<Drop> GetRandomDrops(string TierName, float DropChance, int DropAmount)
        {
            List<Drop> drops = new List<Drop>();
            if (!_config.Tiers.ContainsKey(TierName))
            {
                return drops;
            }

            for (int i = 0; i < DropAmount; i++)
            {
                float chance = UnityEngine.Random.Range(1, 100f);
                if (DropChance >= chance)
                {
                    Drop drop = GetRandomDrop(TierName);
                    if (drop != null)
                    {
                        drops.Add(drop);
                    }
                }
            }
            return drops;
        }

        public Drop GetRandomDrop(string TierName)
        {
            List<DropRange> dropRanges = new List<DropRange>();
            var tierDrops = _config.Tiers[TierName].Drops;
            float startValue = 100f;
            float dropChance = UnityEngine.Random.Range(1, 100f);
            foreach (var d in tierDrops)
            {
                var nextDrop = new DropRange() { drop = d, UpperBound = startValue, LowerBound = startValue - d.Rarity };
                dropRanges.Add(nextDrop);
                startValue -= d.Rarity;
            }
            return dropRanges.FirstOrDefault(x => x.LowerBound < dropChance && x.UpperBound >= dropChance)?.drop;
        }

        #endregion
    }
}
