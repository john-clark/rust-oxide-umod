using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Healing Arrows", "1.0.0", "Kappasaurus")]
    [Description("Shoot arrows that heal their targets!")]

    internal class HealingArrows : RustPlugin
    {
        #region Data

        private Dictionary<ulong, DateTime> usageTimes = new Dictionary<ulong, DateTime>();
        private void LoadData() => Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, DateTime>>(Title);
        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Title, usageTimes);

        #endregion

        #region Variables

        private List<BasePlayer> activePlayers = new List<BasePlayer>();

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            LoadData();
            LoadConfig();
            if (Configuration.PermissionEnabled)
            {
                permission.RegisterPermission(Configuration.Permission, this);
            }
        }
         
        private object OnPlayerAttack(BasePlayer player, HitInfo hitInfo)
        {
            if (!UsedBow(player, hitInfo) || !activePlayers.Contains(player))
            { 
                return null;
            }

            DateTime usageTime; 
            if (usageTimes.TryGetValue(player.userID, out usageTime) && DateTime.UtcNow - usageTime < new TimeSpan(0, Configuration.Cooldown, 0))
            {
                PrintToChat(player, lang.GetMessage("Cooldown", this, player.UserIDString), (new TimeSpan(0, Configuration.Cooldown, 0) - (DateTime.UtcNow - usageTime)).ToShortString());
                return null; 
            }

            if (!HasResources(player))
            {
                PrintToChat(player, lang.GetMessage("Resources", this, player.UserIDString));
                return null;
            }

            if (usageTimes.ContainsKey(player.userID))
            {
                usageTimes[player.userID] = DateTime.UtcNow;
            }
            else
            {
                usageTimes.Add(player.userID, DateTime.UtcNow);
            }

            SaveData();
            TakeResources(player);
            if (!(hitInfo.HitEntity is BasePlayer))
            {
                return null;
            }

            var target = (BasePlayer) hitInfo.HitEntity;
            var random = new System.Random();
            var amount = random.Next(Configuration.HealAmountMin, Configuration.HealAmountMax);
            if (target.health + amount > player.MaxHealth())
            {
                target.health = player.MaxHealth();
            }
            else
            {
                target.health += amount;
            }

            PrintToChat(target, lang.GetMessage("Healing", this, target.UserIDString), player.displayName);
            Effect.server.Run("assets/bundled/prefabs/fx/missing.prefab", hitInfo.HitPositionWorld);
            return false;
        }

        private bool HasResources(BasePlayer player)
        {
            foreach (var item in Configuration.RequiredItems)
            {
                var itemID = ItemManager.itemList.FirstOrDefault(x => x.shortname == item.Key).itemid;
                if (player.inventory.GetAmount(itemID) < Convert.ToInt32(item.Value))
                {
                    return false;
                }
            }

            return true;
        }

        private void TakeResources(BasePlayer player)
        {
            foreach (var item in Configuration.RequiredItems)
            { 
                var itemID = ItemManager.itemList.FirstOrDefault(x => x.shortname == item.Key).itemid;
                player.inventory.Take(null, itemID, Convert.ToInt32(item.Value));
            }
        }

        private bool UsedBow(BasePlayer player, HitInfo hitInfo)
        {
            var item = player.GetActiveItem();
            if (item == null || hitInfo.ProjectilePrefab == null)
            {
                return false;
            }

            return hitInfo.ProjectilePrefab.ToString().Contains("arrow") && player.GetActiveItem().info.shortname.Contains("bow");
        }

        #endregion

        #region Command

        [ChatCommand("toggleheal")]
        private void HealArrowCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, Configuration.Permission) && Configuration.PermissionEnabled)
            {
                PrintToChat(player, lang.GetMessage("Permission", this, player.UserIDString));
                return;
            }

            if (activePlayers.Contains(player))
            {
                activePlayers.Remove(player);
            }
            else
            {
                activePlayers.Add(player);
            }

            PrintToChat(player, lang.GetMessage("Toggled", this, player.UserIDString), activePlayers.Contains(player) ? "enabled" : "disabled");
        }

        #endregion

        #region Configuration

        private struct Configuration
        {
            public static int Cooldown = 1;
            public static bool CooldownEnabled;

            public static int HealAmountMax = 15;
            public static int HealAmountMin = 10;

            public static string Permission = "healingarrows.use";
            public static bool PermissionEnabled = true;

            public static bool EffectEnabled = true;

            public static Dictionary<string, object> RequiredItems = new Dictionary<string, object>
            {
                {"syringe.medical", 1}
            };
        }

        private new void LoadConfig()
        {
            GetConfig(ref Configuration.Cooldown, "Cooldown", "Cooldown (minutes)");
            GetConfig(ref Configuration.CooldownEnabled, "Cooldown", "Cooldown enabled (true/false)");
            GetConfig(ref Configuration.HealAmountMax, "Healing", "Heal amount max");
            GetConfig(ref Configuration.HealAmountMin, "Healing", "Heal amount min");
            GetConfig(ref Configuration.Permission, "Permission", "Permission");
            GetConfig(ref Configuration.PermissionEnabled, "Permission", "Permission enabled (true/false)");
            GetConfig(ref Configuration.RequiredItems, "Items", "Required items");

            SaveConfig();
        }

        private void GetConfig<T>(ref T variable, params string[] path)
        {
            if (path.Length == 0)
            {
                return;
            }

            if (Config.Get(path) == null)
            {
                Config.Set(path.Concat(new object[] { variable }).ToArray());
                PrintWarning($"Added field to config: {string.Join("/", path)}");
            }

            variable = (T)Convert.ChangeType(Config.Get(path), typeof(T));
        }

        protected override void LoadDefaultConfig() => PrintWarning("Generating new config file...");

        #endregion

        #region Localization

        protected override void LoadDefaultMessages() => lang.RegisterMessages(new Dictionary<string, string>
        { 
            {"Cooldown", "Error, you may not shoot another healing arrow until your cooldown expires. Time left: {0}."},
            {"Disabled", "Healing arrows automatically disabled, your cooldown is now in effect."},
            {"Permission", "Error, you lack permission. If you believe this to be an error please contact an administrator."},
            {"Resources", "Error, you lack the required resources, *EDIT LANG*."},
            {"Toggled", "Healing arrows sucessfully {0}."},
            {"Healing", "{0} hit you with a healing arrow!"}
        }, this);

        #endregion
    }
}