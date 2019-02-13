
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Dynamic Loot Drops", "birthdates", "1.1.0")]
    [Description("Adding a new way of looting.")]
    public class DynamicLootDrops : RustPlugin
    {

        private List<Item> pickUp = new List<Item>();
        private ConfigFile config;
        private Dictionary<BasePlayer, long> cooldowns = new Dictionary<BasePlayer, long>();
        private const string Permission = "DynamicLootDrops.use";

        void OnPlayerDie(BasePlayer player, HitInfo info)
        {
            if (player is NPCPlayer)
            {
                return;
            }

            var items = new List<Item>();
            foreach (var i in player.inventory.containerMain.itemList)
            {
                items.Add(i);
            }
            foreach (var i in player.inventory.containerBelt.itemList)
            {
                items.Add(i);
            }
            foreach (var i in player.inventory.containerWear.itemList)
            {
                items.Add(i);
            }

            if (items.Count < 1)
            {
                return;
            }

            foreach (var z in items)
            {
                if (!config.bAP.Contains(z.info.shortname))
                {
                    pickUp.Add(z);
                }

                z.Drop(player.transform.position, new Vector3(Core.Random.Range(0, 3), Core.Random.Range(0, 3), Core.Random.Range(0, 3)));
            }
            player.inventory.Strip();
        }

        bool CanTake(BasePlayer player) => !player.inventory.containerMain.IsFull() || !player.inventory.containerBelt.IsFull();

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!input.IsDown(BUTTON.FORWARD) && !input.IsDown(BUTTON.BACKWARD) && !input.IsDown(BUTTON.JUMP))
            {
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, Permission) && !player.IsAdmin)
            {
                return;
            }

            if (pickUp.Count < 1 && !config.pickUpWithoutDeath)
            {
                return;
            }

            if (!CanTake(player))
            {
                return;
            }

            var entity = Physics.OverlapSphere(player.transform.position, 0.5f).Select(col => col.GetComponentInParent<BaseEntity>());

            foreach (var e in entity)
            {

                if (e != null && e.GetItem() != null && !config.bAP.Contains(e.GetItem().info.shortname))
                {
                    if (config.cooldown > 0)
                    {
                        if (!cooldowns.ContainsKey(player))
                        {
                            cooldowns.Add(player, DateTime.Now.Ticks + TimeSpan.FromMilliseconds(config.cooldown).Ticks);
                        }
                        else
                        {
                            if (cooldowns[player] < DateTime.Now.Ticks)
                            {

                                cooldowns.Remove(player);
                            }
                        }
                    }
                    var item = e.GetItem();
                    pickUp.Remove(e.GetItem());
                    player.GiveItem(item);
                    if (!e.IsDestroyed)
                    {
                        e.Kill();
                    }
                }
            }
        }

        public class ConfigFile
        {
            [JsonProperty(PropertyName = "Blacklisted auto-pickups")]
            public List<string> bAP;

            [JsonProperty(PropertyName = "Pick up without death")]
            public bool pickUpWithoutDeath;

            [JsonProperty(PropertyName = "Cooldown in milliseconds")]
            public long cooldown;

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile()
                {
                    cooldown = 2,
                    pickUpWithoutDeath = false,
                    bAP = new List<string>()
                    {
                        "rifle.ak",
                        "rifle.bolt"
                    }

                };
            }

        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }


        protected override void LoadConfig()
        {
            base.LoadConfig();

            config = Config.ReadObject<ConfigFile>();
            if (config == null)
            {
                LoadDefaultConfig();
            }
        }


        protected override void LoadDefaultConfig()
        {
            config = ConfigFile.DefaultConfig();
            PrintWarning("Default configuration has been loaded.");
        }


        void Init()
        {
            permission.RegisterPermission(Permission, this);
            LoadConfig();
        }

    }
}