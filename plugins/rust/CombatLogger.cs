using Rust;

using System;
using System.Collections.Generic;

using Oxide.Core.Libraries.Covalence;

using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Combat Logger", "Tori1157", "1.0.6", ResourceId = 2790)]
    [Description("Logs everything related to combat.")]

    class CombatLogger : CovalencePlugin
    {
        #region Fields

        /// -- Misc -- ///
        private bool Changed;

        /// -- Combat -- ///
        public bool logHealingItems;
        public bool putHealingItems;
        public bool logRespawns;
        public bool putRespawns;
        public bool logAnimalAttackPlayer;
        public bool putAnimalAttackPlayer;
        public bool logPlayerAttackAnimal;
        public bool putPlayerAttackAnimal;
        public bool logEntityAttackPlayer;
        public bool putEntityAttackPlayer;
        public bool logCombat;
        public bool putCombat;
        public bool logAnimalKillPlayer;
        public bool putAnimalKillPlayer;
        public bool logPlayerKillAnimal;
        public bool putPlayerKillAnimal;
        public bool logEntityKillPlayer;
        public bool putEntityKillPlayer;
        public bool logEntityDistance;
        public bool logOtherDeathCauses;
        public bool putOtherDeathCauses;
        public bool combatDebug;
        public bool logPositions;

        #endregion

        #region Initializing

        private void Init()
        {
            LoadVariables();
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new config file");
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {
            /// -- Combat -- ///
            logCombat = Convert.ToBoolean(GetConfig("Combat Main Logging", "Log Combat Damage", false));
            putCombat = Convert.ToBoolean(GetConfig("Combat Main Logging", "Print Combat Damage To Console", false));
            logHealingItems = Convert.ToBoolean(GetConfig("Combat Main Logging", "Log Healing Items", false));
            putHealingItems = Convert.ToBoolean(GetConfig("Combat Main Logging", "Print Healing Items To Console", false));
            logRespawns = Convert.ToBoolean(GetConfig("Combat Main Logging", "Log Respawns", false));
            putRespawns = Convert.ToBoolean(GetConfig("Combat Main Logging", "Print Respawns To Console", false));
            combatDebug = Convert.ToBoolean(GetConfig("Combat Main Logging", "Print Debug Info To Console (Dev)", false));
            logPositions = Convert.ToBoolean(GetConfig("Combat Main Logging", "Use Entity Position", false));
            logEntityDistance = Convert.ToBoolean(GetConfig("Combat Main Logging", "Log Entity Distance", false));

            logAnimalAttackPlayer = Convert.ToBoolean(GetConfig("Combat Hurt Logging", "Log Animal Attacking Player", false));
            putAnimalAttackPlayer = Convert.ToBoolean(GetConfig("Combat Hurt Logging", "Print Animal Attacking Player To Console", false));
            logPlayerAttackAnimal = Convert.ToBoolean(GetConfig("Combat Hurt Logging", "Log Player Attacking Animal", false));
            putPlayerAttackAnimal = Convert.ToBoolean(GetConfig("Combat Hurt Logging", "Print Player Attacking Animal To Console", false));
            logEntityAttackPlayer = Convert.ToBoolean(GetConfig("Combat Hurt Logging", "Log Entity Attack Player", false));
            putEntityAttackPlayer = Convert.ToBoolean(GetConfig("Combat Hurt Logging", "Print Entity Attack Player To Console", false));

            logAnimalKillPlayer = Convert.ToBoolean(GetConfig("Combat Death Logging", "Log Animal Killing Player", false));
            putAnimalKillPlayer = Convert.ToBoolean(GetConfig("Combat Death Logging", "Print Animal Killing Player To Console", false));
            logPlayerKillAnimal = Convert.ToBoolean(GetConfig("Combat Death Logging", "Log Player Killing Animal", false));
            putPlayerKillAnimal = Convert.ToBoolean(GetConfig("Combat Death Logging", "Print Player Killing Animal To Console", false));
            logEntityKillPlayer = Convert.ToBoolean(GetConfig("Combat Death Logging", "Log Entity Killing Player", false));
            putEntityKillPlayer = Convert.ToBoolean(GetConfig("Combat Death Logging", "Print Entity Killing Player To Console", false));
            logOtherDeathCauses = Convert.ToBoolean(GetConfig("Combat Death Logging", "Log Other Player Death Causes", false));
            putOtherDeathCauses = Convert.ToBoolean(GetConfig("Combat Death Logging", "Print Other Player Death Causes To Console", false));

            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                /// -- Combat -- ///
                ["Log Player Healing1"] = "'{0}({1})' used '{2}' {3}",
                ["Log Player Respawning1"] = "'{0}({1})' has respawned {2}",
                ["Log Entity Attack1"] = "'{0}{5}' were attacked by '{1}{6}' {2}for {3} damage {4}",
                ["Log Player Hurt Himself1"] = "'{0}' hurt himself {1}for {2} damage {3}",
                ["Log Entity Death1"] = "'{0}{4}' were killed by '{1}{5}' {2}{3}",
                ["Log Player Kill Himself1"] = "'{0}' committed suicide {1}{2}{3}",
                ["Log Player Hurt Other"] = "'{0}' were killed by '{1}' {2}",

                /// -- Misc -- ///
                ["Log Weapon"] = "with a",
                ["Log Suicide"] = "damage",
                ["Log Distance F"] = "from",
                ["Log Distance M"] = "meters",
                ["Log Damage"] = "for",
                ["Log At"] = "at",
            }, this);
        }

        #endregion Initializing

        #region Combat

        private void OnHealingItemUse(HeldEntity item, BasePlayer target)
        {
            if (logHealingItems) Log("Combat", Lang("Log Player Healing1", target.displayName, target.userID, item.GetItem().info.displayName.english, logPositions ? $"{Lang("Log At")} {EntityPosition(target)}" : null));
            if (putHealingItems) Puts(Lang("Log Player Healing1", target.displayName, target.userID, item.GetItem().info.displayName.english, logPositions ? $"{Lang("Log At")} {EntityPosition(target)}" : null));
        }

        private void OnItemUse(Item item, int amountToUse)
        {
            if (item == null) return;

            var itname = item?.info?.shortname;
            var player = item?.parent?.GetOwnerPlayer();
            if (player == null) return;

            if (itname == "largemedkit")
            {
                if (logHealingItems) Log("Combat", Lang("Log Player Healing1", player.displayName, player.userID, item.info.displayName.english, logPositions ? $"{Lang("Log At")} {EntityPosition(player)}" : null));
                if (putHealingItems) Puts(Lang("Log Player Healing1", player.displayName, player.userID, item.info.displayName.english, logPositions ? $"{Lang("Log At")} {EntityPosition(player)}" : null));
            }
        }

        private void OnEntityTakeDamage(BaseEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;

            var Bentity = entity as BaseCombatEntity;
            var Eattacker = info.Initiator as BaseEntity;
            if (Eattacker == null) return;
            if (BlacklistEntities(Eattacker) && CombatNpcList(entity) != null) return;
            if (PlayerCheck(entity)) PrevAttack(Bentity, info);

            if (combatDebug && !CheckDamageBL(Bentity)) PrintWarning($"|MAIN-HURT| Weapon: {(info.Weapon?.GetItem()?.info?.displayName.english ?? "No Weapon")}  |  Weapon2: {(info.WeaponPrefab ?? null)}  |  Damage: {(Bentity.lastDamage.ToString() ?? "No Damage")}  |  Attacker: {(Eattacker.ShortPrefabName ?? "No Attacker")}  |  Victim: {(entity.ShortPrefabName ?? "No Victim")}");

            var Eplayer = entity as BasePlayer;
            var Pattacker = info.Initiator as BasePlayer;
            var dmg = info.damageTypes.Total();

            // Doing checks to see if it's supposed to log certain lists.
            if (dmg == 1000) return;
            if (CombatItemList(entity) != null && Eplayer == null) return;
            if (CombatConvert(entity) == null) return;
            if (CombatItemList(Eattacker) != null && !logEntityAttackPlayer) return;
            if (CombatNpcList(Eattacker) != null && !logAnimalAttackPlayer) return;
            if (CombatNpcList(entity) != null && !logPlayerAttackAnimal) return;
            if (CombatEntityList(entity) != null && CombatEntityList(Eattacker) != null) return;

            // If it's self harm it proceeds
            if (Eattacker == entity && Pattacker != null)
            {
                if (combatDebug) PrintWarning($"|SELF-HURT| Weapon: {info.Weapon?.GetItem()?.info?.displayName.english}  |  Weapon2: {info.WeaponPrefab}  |  Damage: {Bentity.lastDamage.ToString()}  |  Attacker: {Eattacker.ShortPrefabName}  |  Victim: {entity.ShortPrefabName}");
                if (logCombat) Log("Combat", Lang("Log Player Hurt Himself1", CheckEntity(Eattacker), !CombatFireDamages(info) && CheckPrefab(info) ? $"{Lang("Log Weapon")} '{CombatWeapon(info)}' " : "", dmg, logPositions ? $"{Lang("Log At")} {EntityPosition(entity)}" : null));
                if (putCombat) Puts(Lang("Log Player Hurt Himself1", CheckEntity(Eattacker), !CombatFireDamages(info) && CheckPrefab(info) ? $"{Lang("Log Weapon")} '{CombatWeapon(info)}' " : "", dmg, logPositions ? $"{Lang("Log At")} {EntityPosition(entity)}" : null));
                return;
            }

            var message = Lang("Log Entity Attack1", CombatConvert(entity) ?? CombatEntity(entity), CombatConvert(Eattacker) ?? CombatEntity(Eattacker), (!BlacklistEntities(Eattacker) && CombatItemList(Eattacker) == null && PlayerCheck(Eattacker) && !CombatFireDamages(info) && CheckPrefab(info) ? $"{Lang("Log Weapon")} '{CombatWeapon(info)}' " : ""), dmg, (CombatItemList(Eattacker) == null && logEntityDistance ? GetDistance(Bentity, info) : null), logPositions ? EntityPosition(entity) : null, logPositions ? EntityPosition(Eattacker) : null);

            // 'Dougle' were attacked by 'Tori1157' for 50 damage | from 50 meters
            // 'Tori1157' were damaged by 'Wooden Spikes' for 25 damage
            // 'Tori1157' were attacked by 'Bear' for 30 damage | from 2 meters
            // 'Bear' were attacked by 'Tori1157' with 'Assault Rifle' for 35 damage from | 150 meters

            // Make sure it's not self harm then log
            if (Eattacker != entity)
            {
                if (logCombat) Log("Combat", message);
                if (putCombat) Puts(message);
                if (putPlayerAttackAnimal && !putCombat) Puts(message);
                if (putEntityAttackPlayer && !putCombat) Puts(message);
                if (putAnimalAttackPlayer && !putCombat) Puts(message);
            }
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null) return;

            var player = entity.ToPlayer();
            var bentity = entity as BaseEntity;

            if (player != null && CheckSteamId(entity))
            {
                // Checks if the death cause is by other means, if so logs/puts it
                if (CheckDamage(entity) != null)
                {
                    if (logOtherDeathCauses) Log("Combat", Lang("Log Player Hurt Other", CheckEntity(entity), CheckDamage(entity), logPositions ? $"{Lang("Log At")} {EntityPosition(entity)}" : null));
                    if (putOtherDeathCauses) Puts(Lang("Log Player Hurt Other", CheckEntity(entity), CheckDamage(entity), logPositions ? $"{Lang("Log At")} {EntityPosition(entity)}" : null));
                    return;
                }

                var CombatInfo = _previousAttack[player.userID];
                var Attacker = CombatInfo.Attacker;
                var Victim = CombatInfo.Victim as BaseCombatEntity;
                var Player = CombatInfo.Victim as BasePlayer;
                var Info = CombatInfo.HitInfo;

                if (combatDebug) PrintWarning($"|PLAYER-DEATH| Weapon1: {(info.Weapon?.GetItem()?.info?.displayName.english ?? "No Weapon")}  |  Weapon2: {(info.WeaponPrefab ?? null)}  |  Damage: {(entity.lastDamage.ToString() ?? "No Damage")}  |  Attacker: {(Attacker.ShortPrefabName ?? "No Attacker")}  |  Victim: {(entity.ShortPrefabName ?? "No Victim")}");

                // Checking lists & if it's supposed to log said list.
                if (CombatItemList(Victim) != null && Player == null) return;
                if (CombatConvert(Victim) == null) return;
                if (CombatItemList(Attacker) != null && !logEntityKillPlayer) return;
                if (CombatNpcList(Attacker) != null && !logAnimalKillPlayer) return;
                if (CombatNpcList(Victim) != null && !logPlayerKillAnimal) return;
                if (CombatEntityList(Victim) != null && CombatEntityList(Attacker) != null) return;

                // if the attacker is the victim (suicide)
                if (Info.Initiator == entity)
                {
                    var EAttacker = info.Initiator as BaseEntity;
                    var dmg = info.damageTypes.Total().ToString();

                    if (logCombat) Log("Combat", Lang("Log Player Kill Himself1", CheckEntity(EAttacker), (dmg != "1000") && !CombatFireDamages(info) && CheckPrefab(info) ? $"{Lang("Log Weapon")} '{CombatWeapon(info)}' " : "", $"{(dmg == "1000" ? null : $"{Lang("Log Damage")} {dmg} {Lang("Log Suicide")} ")}", logPositions ? $"{Lang("Log At")} {EntityPosition(entity)}" : null));
                    if (putCombat) Puts(Lang("Log Player Kill Himself1", CheckEntity(EAttacker), (dmg != "1000") && !CombatFireDamages(info) && CheckPrefab(info) ? $"{Lang("Log Weapon")} '{CombatWeapon(info)}' " : "", $"{(dmg == "1000" ? null : $"{Lang("Log Damage")} {dmg} {Lang("Log Suicide")} ")}", logPositions ? $"{Lang("Log At")} {EntityPosition(entity)}" : null));
                    return;
                }

                var Message = CombatMessage(entity, Info);

                // Checks & logs/puts
                if (logCombat) Log("Combat", Message);
                if (putCombat) Puts(Message);
                if (putPlayerKillAnimal && !putCombat) Puts(Message);
                if (putEntityKillPlayer && !putCombat) Puts(Message);
                if (putAnimalKillPlayer && !putCombat) Puts(Message);
                return;
            }

            // If the victim is not a player, AKA animal or otherwise.
            if (info == null) return;
            var Eplayer = entity as BasePlayer;
            var Eattacker = info.Initiator as BaseEntity;
            if (Eattacker == null) return;

            if (combatDebug) PrintWarning($"|COMBAT-DEATH| Weapon: {(info.Weapon?.GetItem()?.info?.displayName.english ?? "No Weapon")}  |  Damage: {(entity.lastDamage.ToString() ?? "No Damage")}  |  Attacker: {(Eattacker.ShortPrefabName ?? "No Attacker")}  |  Victim: {(entity.ShortPrefabName ?? "No Victim")}");

            if (CombatItemList(entity) != null && Eplayer == null) return;
            if (CombatConvert(entity) == null) return;
            if (CombatItemList(Eattacker) != null && !logEntityKillPlayer) return;
            if (CombatNpcList(Eattacker) != null && !logAnimalKillPlayer) return;
            if (CombatNpcList(entity) != null && !logPlayerKillAnimal) return;
            if (CombatEntityList(entity) != null && CombatEntityList(Eattacker) != null) return;

            var message = CombatMessage(entity, info);

            // Make sure it's not self harm
            if (info.Initiator != entity)
            {
                if (logCombat) Log("Combat", message);
                if (putCombat) Puts(message);
                if (putPlayerKillAnimal && !putCombat) Puts(message);
                if (putEntityKillPlayer && !putCombat) Puts(message);
                if (putAnimalKillPlayer && !putCombat) Puts(message);
                return;
            }
        }

        private void OnUserRespawned(IPlayer player)
        {
            var bplayer = player.Object as BasePlayer;
            if (logRespawns) Log("Combat", Lang("Log Player Respawning1", player.Name, player.Id, logPositions ? $"{Lang("Log At")} {EntityPosition(bplayer)}" : null));
            if (putRespawns) Puts(Lang("Log Player Respawning1", player.Name, player.Id, logPositions ? $"{Lang("Log At")} {EntityPosition(bplayer)}" : null));
        }

        #endregion Combat

        #region Entity Formatting

        // Getting entities position without "," for easier copy&paste teleport.
        private string EntityPosition(BaseEntity entity) => $"({entity.transform.position.x} {entity.transform.position.y} {entity.transform.position.z})";
        // Checks to see if the weapon is in the list, if not send the prefab name (so it always shows the weapon)
        private string CombatWeapon(HitInfo info) => CombatWeaponList(info) ?? info.WeaponPrefab.name;
        // Checks if this entity is in the list, if not send the prefab name (So it always shows the entity)
        private string CombatEntity(BaseEntity entity) => CombatEntityList(entity) ?? entity.ShortPrefabName;
        // Checks to see if it's a player, if it is not it will send the entity list, else it sends the player information.
        private string CheckEntity(BaseEntity entity) => entity.ToPlayer() == null ? CombatEntityList(entity) : $"{entity.ToPlayer().displayName.ToString()}({entity.ToPlayer().userID.ToString()})";
        // Checks if the entity is a player, used for cleaner code (i suppose)
        private bool PlayerCheck(BaseEntity entity) => entity.ToPlayer();
        // Checkign the steam id
        private bool CheckSteamId(BaseEntity entity) => entity.ToPlayer().UserIDString.StartsWith("7656119");
        // Getting the killers distance from the victim
        private string GetDistance(BaseCombatEntity entity, HitInfo info)
        {
            float distance = 0f;

            if (entity != null && info.Initiator != null)
            {
                distance = Vector3.Distance(info.Initiator.transform.position, entity.transform.position);
            }
            return distance.ToString("0").Equals("0") ? "" : $"{Lang("Log Distance F")} {distance.ToString("0")} {Lang("Log Distance M")}";
        }
        // Checking if it's other death causes, if so returns it.
        private string CheckDamage(BaseCombatEntity entity)
        {
            if (entity.ToPlayer() == null) return null;
            var damage = entity.lastDamage.ToString();
            if (damage == null) return null;

            if (DamageTypes.Contains(damage))
                return damage;
            return null;
        }
        // Checking damage info against blacklist
        private bool CheckDamageBL(BaseCombatEntity entity)
        {
            List<string> BlackList = new List<string> { "Decay" };
            var Damage = entity.lastDamage.ToString();

            return (BlackList.Contains(Damage));
        }
        // Checking if prefabname is null
        private bool CheckPrefab(HitInfo info) { return info.WeaponPrefab; }
        /*
        // Checking to see if it's fire attacking (not fool proof - need update)
        private bool CheckForFire(BaseCombatEntity entity, HitInfo info)
        {
            var entname = info.Initiator as BaseEntity;
            var attacker = info.Initiator as BasePlayer;
            var damage = entity.lastDamage.ToString();
            var wepfab = info.WeaponPrefab;
            var weapon = info.Weapon?.GetItem()?.info?.displayName.english;

            // There are 5 ways to receive fire damage, which are listed below.
            // - Arrow | When a player is hit by a fire arrow - It sends two damages, the first one is without weapon information, the second one has weapon information.... WTF FP?
            // - Arrow | When a player is damaged by fire - Campfire, incendiary and open flame
            // - Heat | When an entity/npc is damaged by fire - incendiary and open flame
            // - Generic | When an entity/npc is spawned in on an existing flame - Only first time damaged, afterwards it resorts to the above one. However other animals get this is first attack.
            // - Bite | When an entity/npc is closely hit, not directly but on the ground under certain cirumstanses(no idea which) - I literally have no clue why this happens
            //      - Bear - When at full health
            // --- Keeping this here for developing reasons in the future --- //
            PrintWarning("CHECKING FIRE");
            //Puts($"Weapon1: {wepfab}  ||  Weapon2: {weapon}  ||  Attacker: {attacker}");

            if (CombatFireDamages(info) && CombatNpcList(entname) == null && CombatItemList(entname) == null && wepfab == null && weapon == null && damage == "Arrow" || damage == "Heat" || damage == "Generic" || damage == "Bite" || damage == "Bullet" || damage == "Bleeding" && attacker != null)
            {
                Puts("It's Fire Damage!");
                Puts($"Weapon1: {weapon}  |  Weapon2: {wepfab}  |  Attacker: {entname}  |  Damage: {entity.lastDamage}");
                return true;
            }
            Puts("It's NOT Fire Damage!");
            Puts($"Weapon1: {weapon}  |  Weapon2: {wepfab}  |  Attacker: {entname}  |  Damage: {entity.lastDamage}");
            return false;
        }
        */
        /*
        private bool CheckPrevAttack(ulong userid)
        {
            var Pattack = _previousAttack[userid];

            return (Pattack.HitInfo.Initiator.GetEntity() != null);
        }
        */
        #region Lists

        // Converting of lists, it's doing some magic stuff (i forgot)
        private string CombatConvert(BaseEntity entity)
        {
            if (CombatEntityList(entity) != null)
                return CombatEntity(entity);
            if (CombatEntityList(entity) == null)
                return CheckEntity(entity);
            if (CombatEntityList(entity) != null)
                return CheckEntity(entity);
            return null;
        }

        // List of entities that can hurt the player and animals for names & checkers.
        private string CombatEntityList(BaseEntity entity)
        {
            var entname = entity.ShortPrefabName;

            switch (entname)
            {
                #region ITEMS
                case "spikess.floor":
                    return "Wooden Spikes";
                case "beartrap":
                    return "Bear Trap";
                case "landmine":
                    return "Landmine";
                case "flameturret.deployed":
                    return "Flameturret";
                case "flameturret_fireball":
                    return "Flameturret Flames";
                case "autoturret_deployed":
                    return "Auto Turret";
                case "campfire":
                    return "Campfire";
                case "skull_fire_pit":
                    return "Skull Firepit";
                case "campfire_static":
                    return "Campfire";
                case "fireplace.deployed":
                    return "Stone Fireplace";
                case "barricade.woodwire":
                    return "Wooden Wired Barricade";
                case "gates.external.high.stone":
                    return "High External Stone Gate";
                case "wall.external.high.stone":
                    return "High External Stone Wall";
                case "wall.external.high.wood":
                    return "High External Wood Wall";
                case "barricade.metal":
                    return "Metal Barricade";
                case "barricade.wood":
                    return "Wooden Barricade";
                case "cactus-1":
                    return "Cactus";
                case "cactus-2":
                    return "Cactus";
                case "cactus-3":
                    return "Cactus";
                case "cactus-4":
                    return "Cactus";
                case "cactus-5":
                    return "Cactus";
                case "cactus-6":
                    return "Cactus";
                case "cactus-7":
                    return "Cactus";
                #endregion ITEMS

                #region NPC
                case "bear":
                    return "Bear";
                case "boar":
                    return "Boar";
                case "chicken":
                    return "Chicken";
                case "horse":
                    return "Horse";
                case "wolf":
                    return "Wolf";
                case "stag":
                    return "Stag";
                case "zombie":
                    return "Zombie";
                case "bradleyapc":
                    return "Bradley APC";
                case "patrolhelicopter":
                    return "Patrol Helicopter";
                case "machete.weapon":
                    return "Murderer";
                case "murderer":
                    return "Murderer";
                case "scientist":
                    return "Scientist";
                case "scientistjunkpile":
                    return "Scientist";
                case "scientist_gunner":
                    return "Scientist";
                case "ch47scientists.entity":
                    return "Chinook";
                #endregion NPC

                #region MISC
                case "fireball_small_shotgun":
                    return "12 Gauge Incendiary Flame";
                case "fireball_small_arrow":
                    return "Fire Arrow Flame";
                case "fireball_small":
                    return "Incendiary Ammo Flame";
                #endregion MISC
                default:
                    return null;
            }
        }

        // List of entities that can hurt the player, for names & checkers.
        private string CombatItemList(BaseEntity entity)
        {
            var entname = entity.ShortPrefabName;

            switch (entname)
            {
                case "spikess.floor":
                    return "Wooden Spikes";
                case "beartrap":
                    return "Bear Trap";
                case "landmine":
                    return "Landmine";
                case "flameturret.deployed":
                    return "Flameturret";
                case "flameturret_fireball":
                    return "Flameturret Flames";
                case "autoturret_deployed":
                    return "Auto Turret";
                case "campfire":
                    return "Campfire";
                case "skull_fire_pit":
                    return "Skull Firepit";
                case "barricade.woodwire":
                    return "Wooden Wired Barricade";
                case "gates.external.high.stone":
                    return "High External Stone Gate";
                case "wall.external.high.stone":
                    return "High External Stone Wall";
                case "wall.external.high.wood":
                    return "High External Wood Wall";
                case "barricade.metal":
                    return "Metal Barricade";
                case "barricade.wood":
                    return "Wooden Barricade";
                case "fireball_small_shotgun":
                    return "12 Gauge Incendiary Flame";
                case "fireball_small_arrow":
                    return "Fire Arrow Flame";
                case "fireball_small":
                    return "Incendiary Ammo Flame";
                case "cactus-1":
                    return "Cactus";
                case "cactus-2":
                    return "Cactus";
                case "cactus-3":
                    return "Cactus";
                case "cactus-4":
                    return "Cactus";
                case "cactus-5":
                    return "Cactus";
                case "cactus-6":
                    return "Cactus";
                case "cactus-7":
                    return "Cactus";
                default:
                    return null;
            }
        }

        // List of animals that can hurt the player, for names & checkers,
        private string CombatNpcList(BaseEntity entity)
        {
            var entname = entity.ShortPrefabName;

            switch (entname)
            {
                case "bear":
                    return "Bear";
                case "boar":
                    return "Boar";
                case "chicken":
                    return "Chicken";
                case "horse":
                    return "Horse";
                case "wolf":
                    return "Wolf";
                case "stag":
                    return "Stag";
                case "zombie":
                    return "Zombie";
                case "bradleyapc":
                    return "Bradley APC";
                case "patrolhelicopter":
                    return "Patrol Helicopter";
                case "machete.weapon":
                    return "Murderer";
                case "murderer":
                    return "Murderer";
                case "scientist":
                    return "Scientist";
                case "scientistjunkpile":
                    return "Scientist";
                case "scientist_gunner":
                    return "Scientist";
                case "ch47scientists.entity":
                    return "Chinook";
                default:
                    return null;
            }
        }

        // List of weapons, used for convertion and checkers
        private string CombatWeaponList(HitInfo info)
        {
            var fwepname = info.Weapon?.GetItem()?.info?.displayName.english;
            var wepname = info.WeaponPrefab.name;

            if (fwepname == null)
            {
                switch (wepname)
                {
                    case "spear_stone":
                        return "Stone Spear";
                    case "spear_stone.entity":
                        return "Stone Spear";
                    case "grenade.beancan.deployed":
                        return "Beancan Grenade";
                    case "bone_club.entity":
                        return "Bone Club";
                    case "knife_bone.entity":
                        return "Bone Knife";
                    case "grenade.f1.deployed":
                        return "F1 Grenade";
                    case "longsword.entity":
                        return "Longsword";
                    case "mace.entity":
                        return "Mace";
                    case "machete.weapon":
                        return "Machete";
                    case "rocket_basic":
                        return "Rocket";
                    case "rocket_hv":
                        return "High Velocity Rocket";
                    case "rocket_smoke":
                        return "Smoke Rocket";
                    case "salvaged_cleaver.entity":
                        return "Salvaged Cleaver";
                    case "salvaged_sword.entity":
                        return "Salvaged Sword";
                    case "spear_wooden.entity":
                        return "Wooden Spear";
                    case "MainCannonShell":
                        return "Bradley APC";
                    case "rocket_heli":
                        return "Patrol Helicopter";
                    case "candy_cane.entity":
                        return "Candy Cane Club";
                    case "flamethrower.entity":
                        return "Flame Thrower";
                    default:
                        return null;
                }
            }
            return fwepname;
        }

        // List of fire damages, used to block out storing
        private bool CombatFireDamages(HitInfo info)
        {
            var attacker = info.Initiator as BaseEntity;
            var wepname = attacker.ShortPrefabName;

            switch (wepname)
            {
                case "fireball_small_shotgun":
                    return true;
                case "fireball_small_arrow":
                    return true;
                case "fireball_small":
                    return true;
                case "fireball":
                    return true;
                case "oilfireballsmall":
                    return true;
                case "campfire_static":
                    return true;
                case "fireplace.deployed":
                    return true;
                case "rocket_fire":
                    return true;
                case "campfire":
                    return true;
                case "skull_fire_pit":
                    return true;
                case "flameturret_fireball":
                    return true;
                case "oilfireball2":
                    return true;
                case "flamethrower_fireball":
                    return true;
                default:
                    return false;
            }
        }

        // List of entities that should not do stuff
        private bool BlacklistEntities(BaseEntity entity)
        {
            var entname = entity.ShortPrefabName;

            List<string> blacklist = new List<string>
            {
                "machete.weapon", "murderer", "scientist",
                "scientistjunkpile", "scientist_gunner",
                "cactus-1", "cactus-2", "cactus-3", "cactus-4",
                "cactus-5", "cactus-6", "cactus-7"
            };

            return blacklist.Contains(entname);
        }
     
        // List of other death causes
        List<string> DamageTypes = new List<string>
        {
            "Hunger", "Thirst", "Cold", "Drowned",
            "Poison", "Fall", "Radiation",
        };

        #endregion Lists

        #endregion Entity Formatting

        #region Helpers

        #region Config

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;

            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }

            object value;

            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }
        #endregion

        #region Data

        /// Thanks to LaserHydra for letting me use his code | From here
        private readonly Dictionary<ulong, AttackInfo> _previousAttack = new Dictionary<ulong, AttackInfo>();

        private void PrevAttack(BaseCombatEntity entity, HitInfo info)
        {
            if (entity.lastDamage == DamageType.Bleeding || entity.ToPlayer() == null)
                return;

            var userId = entity.ToPlayer().userID;
            //if (CombatFireDamages(info) && CheckPrevAttack(userId)) return;
            // ^ Blocks fire damage out
            _previousAttack[userId] = new AttackInfo
            {
                HitInfo = info,
                Attacker = entity.lastAttacker ?? info?.Initiator,
                Victim = entity,
                DamageType = entity.lastDamage
            };
        }

        private struct AttackInfo
        {
            public HitInfo HitInfo { get; set; }
            public DamageType DamageType { get; set; }
            public BaseEntity Attacker { get; set; }
            public BaseEntity Victim { get; set; }
        }
        /// To here

        #endregion Data

        private string CombatMessage(BaseCombatEntity entity, HitInfo info)
        {
            var player = entity.ToPlayer();
            if (player == null)
            {
                var Eattacker = info?.Initiator as BaseEntity;

                return Lang("Log Entity Death1", CombatConvert(entity) ?? CombatEntity(entity), CombatConvert(Eattacker) ?? CombatEntity(Eattacker), CombatEntityList(Eattacker) == null && PlayerCheck(Eattacker) && !CombatFireDamages(info) && CheckPrefab(info) ? $"{Lang("Log Weapon")} '{CombatWeapon(info)}' " : "", (CombatItemList(Eattacker) == null && logEntityDistance ? GetDistance(entity, info) : null), logPositions ? EntityPosition(entity) : null, logPositions ? EntityPosition(Eattacker) : null);
            }
            var PrevAttacker = _previousAttack[player.userID];
            var EAttacker = PrevAttacker.HitInfo?.Initiator as BaseEntity;
            var Info = PrevAttacker.HitInfo;

            return Lang("Log Entity Death1", CombatConvert(entity) ?? CombatEntity(entity), CombatConvert(EAttacker) ?? CombatEntity(EAttacker), CombatEntityList(EAttacker) == null && PlayerCheck(EAttacker) && !CombatFireDamages(info) && CheckPrefab(info) ? $"{Lang("Log Weapon")} '{CombatWeapon(info)}' " : "", (!BlacklistEntities(EAttacker) && CombatItemList(EAttacker) == null && logEntityDistance ? GetDistance(entity, Info) : null), logPositions ? EntityPosition(entity) : "fart", logPositions ? EntityPosition(EAttacker) : null);
        }

        string Lang(string key, params object[] args) => string.Format(lang.GetMessage(key, this), args);

        private void Log(string filename, string text)
        {
            LogToFile(filename, $"[{DateTime.Now}] {text.Replace("{", "").Replace("}", "")}", this);
        }

        /// --- TODO ---
        /// - Fix up the convertion of stuff
        ///     - Fix error when there is an entity missing (Scientist)
        /// - Fix up the debug message to have more information
        /// - Add better support for bleeding damage
        /// - Add option for hit positions
        /// - Use switch/case for the if statement walls
        /// - Add option to disable certain entities (Cactus)
        ///     - List in config
        /// - Optimize code
        /// - Do extensive testing of all weapons/NPC's
        /// --- WISHFULL ---
        /// - CUI/Chat death notes
        /// --- MISSING ---
        /// - Bradley
        /// - Helicopter
        ///     - Killer
        ///     - Rocket kill (Can't get entity???)

        #endregion
    }
}