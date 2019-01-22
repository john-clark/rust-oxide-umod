using System;
using System.Collections.Generic;
using System.Linq;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Advanced Arrows", "Ryan", "2.1.0")]
    [Description("Allows players with permission to use custom arrow types")]
    class AdvancedArrows : RustPlugin
    {
        #region Declaration

        private readonly Dictionary<ulong, ActiveArrow> ActiveArrows = new Dictionary<ulong, ActiveArrow>();

        private ConfigFile configFile;

        private readonly System.Random rnd = new System.Random();

        private enum ArrowType
        {
            Wind,
            Fire,
            Explosive,
            Knockdown,
            Narco,
            Poison,
            None
        }

        #endregion

        #region Config

        private class ConfigFile
        {
            public Dictionary<ArrowType, Arrow> Arrows;
            public ExplosionSettings ExplosionSettings;

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    Arrows = new Dictionary<ArrowType, Arrow>()
                    {
                        { ArrowType.Wind, new Arrow() },
                        { ArrowType.Fire, new Arrow() },
                        { ArrowType.Explosive, new Arrow() },
                        { ArrowType.Knockdown, new Arrow() },
                        { ArrowType.Narco, new Arrow() },
                        { ArrowType.Poison, new Arrow() },
                    },
                    ExplosionSettings = new ExplosionSettings()
                };
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating default configuration file...");
            configFile = ConfigFile.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configFile = Config.ReadObject<ConfigFile>();
        }

        protected override void SaveConfig() => Config.WriteObject(configFile);

        #endregion

        #region Lang

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Cmd_Types"] = "<color=orange>ARROW TYPES</color> \n{0}",
                ["Cmd_Switched"] = "You've switched your arrow type to '<color=orange>{0}</color>', you have <color=orange>{1}</color> uses until it's automatically deactivated.",
                ["Cmd_Disabled"] = "You've sucessfully <color=orange>disabled</color> your active arrow",
                ["Cmd_NoPerm"] = "You don't have permission to use arrow '<color=orange>{0}</color>'",
                ["Cmd_Price"] = "(<color=orange>{0}</color>x {1})",
                ["Cmd_Active"] = "<color=orange>Active Arrow</color>: {0}",

                ["Arrow_Disabled"] = "Your arrow has been automatically disabled",

                ["Error_NotSelected"] = "Your bow isn't drawn, you must select am arrow using '<color=orange>/arrow</color>'.",
                ["Error_InvalidEnt"] = "Your arrow didn't hit anything",
                ["Error_NotPlayer"] = "You can't hit an entity with arrow type '<color=orange>{0}</color>'.",

                ["Hit_Wind"] = "You hit <color=orange>{0}</color> with a <color=orange>Wind</color> arrow!",
                ["Hit_Fire"] = "You used a <color=orange>Fire</color> arrow! (<color=orange>{0}</color> HP remaining)",
                ["Hit_Explosive"] = "You used a <color=orange>Explosive</color> arrow! (<color=orange>{0}</color> HP remaining)",
                ["Hit_Knockdown"] = "You knocked <color=orange>{0}</color> out, choose his fate!",
                ["Hit_Narco"] = "You've sent <color=orange>{0}</color> to sleep, act quick before they wake up again!",
                ["Hit_Poison"] = "You've sucessfully poisoned <color=orange>{0}</color> (<color=orange>{1}</color> HP remaining)",

                ["Damaged_Poison"] = "You've been poisoned by an poisoned arrow, there's no cure!",

                ["Resources_Needed"] = "You need <color=orange>{0}</color>x <color=orange>{1}</color> to use that arrow",
                ["Resources_Spent"] = "The arrow you just used costed <color=orange>{0}</color>x <color=orange>{1}</color>"
            }, this);
        }

        #endregion

        #region Classes

        private class ExplosionSettings
        {
            public float ExplosiveDamage;
            public float ExplosionRadius;

            public ExplosionSettings()
            {
                ExplosiveDamage = 50f;
                ExplosionRadius = 10f;
            }
        }

        private class Price
        {
            public bool Enabled;
            public string ItemShortname;
            public int ItemAmount;

            public Price()
            {
                Enabled = true;
                ItemShortname = "metal.refined";
                ItemAmount = 30;
            }
        }

        private class Arrow
        {
            public Price ArrowPrice;
            public string Permission;
            public int Uses;

            public Arrow()
            {
                ArrowPrice = new Price();
                Permission = "able";
                Uses = 1;
            }
        }

        private class ActiveArrow
        {
            public int Uses;
            public ArrowType ArrowType;

            public ActiveArrow()
            {
            }

            public ActiveArrow(ArrowType type)
            {
                Uses = 0;
                ArrowType = type;
            }
        }

        #endregion

        #region Methods

        private bool IsPlayerArrow(ArrowType type)
        {
            switch (type)
            {
                case ArrowType.Explosive:
                    return false;
                case ArrowType.Fire:
                    return false;
                default:
                    return true;
            }
        }

        private bool CanUseArrow(BasePlayer player, ArrowType type, BaseCombatEntity combatEntity, out Item outItem)
        {
            if (IsPlayerArrow(type) && combatEntity.ToPlayer() == null)
            {
                PrintToChat(player, Lang("Error_NotPlayer", player.UserIDString, type));
                outItem = null;
                return false;
            }
            var typeConfig = configFile.Arrows[type];
            if (!typeConfig.ArrowPrice.Enabled)
            {
                outItem = null;
                return true;
            }
            if (player.inventory.FindItemID(typeConfig.ArrowPrice.ItemShortname) == null)
            {
                PrintToChat(player, Lang("Resources_Needed", player.UserIDString, typeConfig.ArrowPrice.ItemAmount,
                    ItemManager.CreateByPartialName(typeConfig.ArrowPrice.ItemShortname).info.displayName.english) ?? "<color=red>ITEM NOT FOUND</color>");
                ActiveArrows.Remove(player.userID);
                outItem = null;
                return false;
            }
            var item = player.inventory.FindItemID(typeConfig.ArrowPrice.ItemShortname);
            var amount = player.inventory.GetAmount(item.info.itemid);
            if (amount >= typeConfig.ArrowPrice.ItemAmount)
            {
                outItem = ItemManager.CreateByName(typeConfig.ArrowPrice.ItemShortname, typeConfig.ArrowPrice.ItemAmount) ?? ItemManager.CreateByName("metal.refined", 30);
                return true;
            }
            outItem = null;
            var neededAmount = typeConfig.ArrowPrice.ItemAmount - amount;
            PrintToChat(player, Lang("Resources_Needed", player.UserIDString, neededAmount, item.info.displayName.english));
            return false;
        }

        private void TakeItems(BasePlayer player, Item item)
        {
            player.inventory.Take(player.inventory.FindItemIDs(item.info.itemid), item.info.itemid, item.amount);
            PrintToChat(player, Lang("Resources_Spent", player.UserIDString, item.amount, item.info.displayName.english));
        }

        private void FireArrow(Vector3 targetPos)
        {
            var entity = GameManager.server.CreateEntity("assets/bundled/prefabs/fireball.prefab", targetPos);
            Effect.server.Run("assets/prefabs/weapons/satchelcharge/effects/satchel-charge-explosion.prefab", targetPos);
            entity.Spawn();
        }

        private void ExplosiveArrow(Vector3 targetPos, BaseCombatEntity combatEnt)
        {
            Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/rocket_explosion.prefab", targetPos);
            var vicinityEnts = new List<BaseCombatEntity>();
            var damage = configFile.ExplosionSettings.ExplosiveDamage;
            Vis.Entities(targetPos, configFile.ExplosionSettings.ExplosionRadius, vicinityEnts);
            foreach (var vicinityEnt in vicinityEnts)
            {
                var scaledDamage = damage - Vector3.Distance(targetPos, vicinityEnt.transform.position) * 2;
                vicinityEnt.Hurt(scaledDamage, DamageType.Explosion);
            }
        }

        private bool CanActivateArrow(BasePlayer player, ArrowType type)
        {
            var typeConfig = configFile.Arrows[type];
            if (!permission.UserHasPermission(player.UserIDString, Name + "." + typeConfig.Permission))
            {
                PrintToChat(player, Lang("Cmd_NoPerm", player.UserIDString, type));
                return false;
            }
            return true;
        }

        private void DealWithArrow(BasePlayer player, ActiveArrow arrow)
        {
            if (!CanActivateArrow(player, arrow.ArrowType))
                return;
            if (!ActiveArrows.ContainsKey(player.userID))
                ActiveArrows.Add(player.userID, arrow);
            else
                ActiveArrows[player.userID] = arrow;
            PrintToChat(player, Lang("Cmd_Switched", player.UserIDString, arrow.ArrowType, configFile.Arrows[arrow.ArrowType].Uses));
        }

        private void DealWithRemoval(BasePlayer player)
        {
            ActiveArrows[player.userID].Uses++;
            if (ActiveArrows[player.userID].Uses >= configFile.Arrows[ActiveArrows[player.userID].ArrowType].Uses)
            {
                PrintToChat(player, Lang("Arrow_Disabled", player.UserIDString));
                ActiveArrows.Remove(player.userID);
            }
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private string GetHelpMsg(BasePlayer player)
        {
            var msgList = new List<string>();
            var msg = "";
            foreach (var type in Enum.GetValues(typeof(ArrowType)).Cast<ArrowType>())
            {
                if(type.Equals(ArrowType.None)) continue;
                var configType = configFile.Arrows[type];
                msgList.Add(type + " " + (configType.ArrowPrice.Enabled ? Lang("Cmd_Price", player.UserIDString, configType.ArrowPrice.ItemAmount,
                    ItemManager.CreateByPartialName(configType.ArrowPrice.ItemShortname).info.displayName.english ?? "<color=red>ITEM NOT FOUND</color>") : ""));
            }
            msg = "\t" + string.Join("\n\t", msgList.ToArray());
            if (ActiveArrows.ContainsKey(player.userID))
                msg += "\n" + Lang("Cmd_Active", player.UserIDString, ActiveArrows[player.userID].ArrowType);
            return msg;
        }

        #endregion

        #region Hooks

        private void Init()
        {
            foreach (var arrow in configFile.Arrows)
            {
                if (!permission.PermissionExists(Name + "." + arrow.Value.Permission, this))
                    permission.RegisterPermission(Name + "." + arrow.Value.Permission, this);
            }
        }

        private void OnPlayerAttack(BasePlayer attacker, HitInfo hitInfo)
        {
            if (hitInfo == null || attacker == null || hitInfo.WeaponPrefab == null || hitInfo.Weapon == null)
                return;

            if (hitInfo.WeaponPrefab.ToString().Contains("hunting") || hitInfo.Weapon.name.Contains("bow") && attacker.IsAdmin)
            {
                var hitEntity = hitInfo.HitEntity as BaseCombatEntity;

                if (hitEntity != null)
                {
                    ActiveArrow arrow;
                    if (ActiveArrows.TryGetValue(attacker.userID, out arrow))
                    {
                        Item foundItem;
                        switch (arrow.ArrowType)
                        {
                            case ArrowType.Wind:
                            {
                                if (!CanUseArrow(attacker, arrow.ArrowType, hitEntity, out foundItem))
                                    return;
                                TakeItems(attacker, foundItem);
                                var windPlayer = hitEntity.ToPlayer();
                                windPlayer.MovePosition(windPlayer.transform.position + new Vector3(1, rnd.Next(5, 11), 1));
                                timer.Repeat(0.1f, 30, () => windPlayer.violationLevel = 0);
                                PrintToChat(attacker, Lang("Hit_Wind", attacker.UserIDString, windPlayer.name));
                                DealWithRemoval(attacker);
                                return;
                            }

                            case ArrowType.Fire:
                            {
                                if (!CanUseArrow(attacker, arrow.ArrowType, hitEntity, out foundItem))
                                    return;
                                TakeItems(attacker, foundItem);
                                NextTick(() =>
                                {
                                    FireArrow(hitInfo.HitPositionWorld);
                                    PrintToChat(attacker, Lang("Hit_Fire", attacker.UserIDString, Math.Round(hitEntity.Health(), 1)));
                                });
                                DealWithRemoval(attacker);
                                return;
                            }

                            case ArrowType.Explosive:
                            {
                                if (!CanUseArrow(attacker, arrow.ArrowType, hitEntity, out foundItem))
                                    return;
                                TakeItems(attacker, foundItem);
                                NextTick(() =>
                                {
                                    ExplosiveArrow(hitInfo.HitPositionWorld, hitEntity);
                                    PrintToChat(attacker, Lang("Hit_Explosive", attacker.UserIDString, Math.Round(hitEntity.Health(), 1)));
                                });
                                DealWithRemoval(attacker);
                                return;
                            }

                            case ArrowType.Knockdown:
                            {
                                if (!CanUseArrow(attacker, arrow.ArrowType, hitEntity, out foundItem))
                                    return;
                                TakeItems(attacker, foundItem);
                                var knockdownPlayer = hitEntity.ToPlayer();
                                NextTick(() => PrintToChat(attacker, Lang("Hit_Knockdown", attacker.UserIDString, knockdownPlayer.name, Math.Round(knockdownPlayer.health, 1))));
                                knockdownPlayer.StartWounded();
                                DealWithRemoval(attacker);
                                return;
                            }

                            case ArrowType.Narco:
                            {
                                if (!CanUseArrow(attacker, arrow.ArrowType, hitEntity, out foundItem))
                                    return;
                                TakeItems(attacker, foundItem);
                                var narcoPlayer = hitEntity.ToPlayer();
                                NextTick(() => PrintToChat(attacker, Lang("Hit_Narco", attacker.UserIDString, narcoPlayer.name, Math.Round(narcoPlayer.health, 1))));
                                narcoPlayer.StartSleeping();
                                DealWithRemoval(attacker);
                                return;
                            }

                            case ArrowType.Poison:
                            {
                                if (!CanUseArrow(attacker, arrow.ArrowType, hitEntity, out foundItem))
                                    return;
                                TakeItems(attacker, foundItem);
                                var poisonPlayer = hitEntity.ToPlayer();
                                NextTick(() => PrintToChat(attacker, Lang("Hit_Poison", attacker.UserIDString, poisonPlayer.name, Math.Round(poisonPlayer.Health(), 1))));
                                PrintToChat(poisonPlayer, Lang("Damaged_Poison", poisonPlayer.UserIDString));
                                poisonPlayer.metabolism.poison.value = 30;
                                DealWithRemoval(attacker);
                                return;
                            }
                        }
                    }
                    else
                    {
                        if (ActiveArrows.ContainsKey(attacker.userID))
                            ActiveArrows.Remove(attacker.userID);
                    }
                }
            }
        }

        [ChatCommand("arrow")]
        private void ArrowCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length >= 1)
            {
                switch (args[0].ToLower())
                {
                    case "wind":
                        DealWithArrow(player, new ActiveArrow(ArrowType.Wind));
                        return;

                    case "fire":
                        DealWithArrow(player, new ActiveArrow(ArrowType.Fire));
                        return;

                    case "explosive":
                        DealWithArrow(player, new ActiveArrow(ArrowType.Explosive));
                        return;

                    case "knockdown":
                        DealWithArrow(player, new ActiveArrow(ArrowType.Knockdown));
                        return;

                    case "narco":
                        DealWithArrow(player, new ActiveArrow(ArrowType.Narco));
                        return;

                    case "poision":
                        DealWithArrow(player, new ActiveArrow(ArrowType.Poison));
                        return;

                    case "none":
                        if (!ActiveArrows.ContainsKey(player.userID))
                            goto default;
                        ActiveArrows[player.userID] = new ActiveArrow(ArrowType.None);
                        PrintToChat(player, Lang("Cmd_Disabled", player.UserIDString, ArrowType.None));
                        return;

                    default:
                        PrintToChat(player, Lang("Cmd_Types", player.UserIDString, GetHelpMsg(player)));
                        return;
                }
            }
            PrintToChat(player, Lang("Cmd_Types", player.UserIDString, GetHelpMsg(player)));
        }

        #endregion
    }
}
