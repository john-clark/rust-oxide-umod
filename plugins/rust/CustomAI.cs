#region Header
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Rust;
using Newtonsoft.Json;
namespace Oxide.Plugins {
    [Info("CustomAI", "serezhadelaet", "1.2.2", ResourceId = 2621)]
    [Description("CustomAI ;)")]
    class CustomAI : RustPlugin {
        #endregion

        #region Fields
        static int layerGround = LayerMask.GetMask("World", "Default", "Terrain", "Craters");
        static int blockLayer = LayerMask.GetMask("World", "Construction", "Tree", "Deployed", "Mesh");
        static int targetLayer = LayerMask.GetMask("Player (Server)", "Default");
        static string ignorePermission = "customai.ignore";
        static int layerBuilding = Rust.Layers.PlayerBuildings;
        static CustomAI instance;
        Dictionary<BaseEntity, AIAnimal> animalAI = new Dictionary<BaseEntity, AIAnimal>();
        enum AIState { Walking, Attack, Scared, Sleeping }
        enum AType { Passive, Aggressive }
        private PluginConfig pluginConfig;
        private class AnimalConfig {
            [JsonProperty("speed")]
            public float speed;
            [JsonProperty("boostSpeed")]
            public float boostSpeed;
            [JsonProperty("health")]
            public float health;
            [JsonProperty("armor")]
            public float armor;
            [JsonProperty("damage")]
            public float damage;
            [JsonProperty("agressive")]
            public bool agressive;
            [JsonProperty("attackAnimals")]
            public bool attackAnimals;
            [JsonProperty("attackRange")]
            public float attackRange;
        }
        private class PluginConfig {
            [JsonProperty("MainSettings")]
            public Dictionary<string, object> MainSettings = new Dictionary<string, object>();
            [JsonProperty("AnimalsSettings")]
            public Dictionary<string, AnimalConfig> AnimalsSettings = new Dictionary<string, AnimalConfig>();
            public static PluginConfig DefaultConfig() {
                var output = new PluginConfig();
                output.MainSettings["Sleep at night"] = true;
                output.MainSettings["Sleep start time"] = 23;
                output.MainSettings["Sleep end time"] = 7;
                output.MainSettings["Passive animals run away when they take damage"] = true;
                output.MainSettings["Radius to find target"] = 20;
                output.MainSettings["Remove all scientists"] = true;
                output.MainSettings["ConfigVersion"] = "1.1.3";
                output.AnimalsSettings["Bear"] = new AnimalConfig(){ speed = 1.9f, boostSpeed = 2.2f, health = 400, armor = 0, damage = 25, agressive = true, attackAnimals = true, attackRange = 2};
                output.AnimalsSettings["Wolf"] = new AnimalConfig() { speed = 2.1f, boostSpeed = 2.4f, health = 150, armor = 0, damage = 25, agressive = true, attackAnimals = true, attackRange = 2 };
                output.AnimalsSettings["Stag"] = new AnimalConfig() { speed = 1.9f, boostSpeed = 2.85f, health = 150, armor = 0, damage = 20, agressive = false, attackAnimals = false, attackRange = 2 };
                output.AnimalsSettings["Boar"] = new AnimalConfig() { speed = 1.3f, boostSpeed = 1.95f, health = 150, armor = 0, damage = 20, agressive = true, attackAnimals = true, attackRange = 2 };
                output.AnimalsSettings["Chicken"] = new AnimalConfig() { speed = 0.6f, boostSpeed = 0.9f, health = 25, armor = 0, damage = 5, agressive = false, attackAnimals = false, attackRange = 0.8f };
                output.AnimalsSettings["Horse"] = new AnimalConfig() { speed = 2f, boostSpeed = 2.3f, health = 500, armor = 0, damage = 25, agressive = false, attackAnimals = false, attackRange = 2 };
                output.AnimalsSettings["Zombie"] = new AnimalConfig() { speed = 1.5f, boostSpeed = 1.7f, health = 150, armor = 0, damage = 10, agressive = true, attackAnimals = true, attackRange = 2 };
                return output;
            }
        }
        class AIConfig {
            public AnimalConfig Bear;
            public AnimalConfig Wolf;
            public AnimalConfig Stag;
            public AnimalConfig Boar;
            public AnimalConfig Chicken;
            public AnimalConfig Horse;
            public AnimalConfig Zombie;
            public bool SleepAtNight { get; set; }
            public float SleepStartTime { get; set; }
            public float SleepEndTime { get; set; }
            public bool RemoveAllScientists { get; set; }
            public bool PassiveRunAway { get; set; }
            public float FindTargetRadius { get; set; }
        }
        class TargetMemory {
            public BaseEntity target;
            public float lastAttackTime;
            public Vector3 lastPosition;
        }
        static AIConfig config = new AIConfig();
        #endregion

        #region Config
        void InitializeConfig() {
            object value;
            if (!pluginConfig.MainSettings.TryGetValue("ConfigVersion", out value))
                OwerwriteConfig();
            else if ((string)value != "1.1.3")
                OwerwriteConfig();
            config.Bear = pluginConfig.AnimalsSettings["Bear"];
            config.Wolf = pluginConfig.AnimalsSettings["Wolf"];
            config.Stag = pluginConfig.AnimalsSettings["Stag"];
            config.Boar = pluginConfig.AnimalsSettings["Boar"];
            config.Chicken = pluginConfig.AnimalsSettings["Chicken"];
            config.Horse = pluginConfig.AnimalsSettings["Horse"];
            config.Zombie = pluginConfig.AnimalsSettings["Zombie"];
            config.SleepAtNight = Convert.ToBoolean(pluginConfig.MainSettings["Sleep at night"]);
            config.SleepStartTime = Convert.ToSingle(pluginConfig.MainSettings["Sleep start time"]);
            config.SleepEndTime = Convert.ToSingle(pluginConfig.MainSettings["Sleep end time"]);
            config.RemoveAllScientists = Convert.ToBoolean(pluginConfig.MainSettings["Remove all scientists"]);
            config.PassiveRunAway = Convert.ToBoolean(pluginConfig.MainSettings["Passive animals run away when they take damage"]);
            config.FindTargetRadius = Convert.ToSingle(pluginConfig.MainSettings["Radius to find target"]);
        }
        void OwerwriteConfig() {
            base.Config.Clear();
            pluginConfig = PluginConfig.DefaultConfig();
            Config.WriteObject(pluginConfig);
            pluginConfig = Config.ReadObject<PluginConfig>();
            Debug.LogWarning("------------\n--CUSTOMAI--\n------------\n--------------------------\n--Config was overwritten--\n--------------------------");
        }
        protected override void LoadDefaultConfig() {
            Debug.LogWarning("------------\n--CUSTOMAI--\n------------\n-------------------------------------\n--Creating a new configuration file--\n-------------------------------------");
            pluginConfig = PluginConfig.DefaultConfig();
        }
        protected override void LoadConfig() {
            base.LoadConfig();
            pluginConfig = Config.ReadObject<PluginConfig>();
        }
        protected override void SaveConfig() {
            Config.WriteObject(pluginConfig);
        }
        #endregion

        #region MonoBehaviours
        class AIAnimal : MonoBehaviour {
            public BaseEntity entity;
            public BaseNpc npc;
            private AType type;
            private AType originalType;
            private AIState state;
            private BaseEntity target;
            float updateRate = UnityEngine.Random.Range(0.07f, 0.075f);
            float rateScale = 0.21f;
            float nextUpdate;
            AnimalConfig animalConfig;
            float nextWalkTime = Time.realtimeSinceStartup;
            float stuckTime;
            Vector3 lastPosition;
            HashSet<uint> blockedTargets = new HashSet<uint>();
            Dictionary<uint, TargetMemory> targetMemory = new Dictionary<uint, TargetMemory>();
            bool wasDamaged = false;
            private void Awake() {
                entity = GetComponent<BaseEntity>();
                npc = entity.GetComponent<BaseNpc>();
                npc.CancelInvoke(npc.TickAi);
                npc.SetAiFlag(BaseNpc.AiFlags.Sleeping, false);
                animalConfig = GetAnimalSpeed(npc);
                npc._maxHealth = animalConfig.health;
                npc.health = animalConfig.health;
                instance.animalAI[entity] = this;
                type = animalConfig.agressive ? AType.Aggressive : AType.Passive;
                originalType = type;
                state = AIState.Walking;
                lastPosition = entity.transform.position;
                InvokeHandler.InvokeRepeating(this, UpdateAI, 0, updateRate);
                if (IsStucked())
                    DestroyAndKill();
                if (config.SleepAtNight)
                    InvokeHandler.InvokeRepeating(this, CheckTime, updateRate, 10 + updateRate);
            }
            void UpdateAI() {
                if (Pause()){
                    if (type == AType.Aggressive && target != null)
                        npc.ServerRotation = GetRotationToTarget(target.transform.position);
                    return;
                }
                if (state == AIState.Sleeping) return;
                CheckStuck();
                lastPosition = entity.transform.position;
                if (type == AType.Passive) {
                    if (state == AIState.Scared)
                        target = GetTargetVis(entity, config.FindTargetRadius, animalConfig.attackAnimals, blockedTargets, false);
                    else
                        target = GetTargetVis(entity, config.FindTargetRadius / 2, animalConfig.attackAnimals, blockedTargets, false);
                    if (target != null)
                        state = AIState.Scared;
                    else
                        state = AIState.Walking;
                    Walk();
                    return;
                }
                if (IsLowHP()) {
                    state = AIState.Walking;
                    Walk();
                    return;
                }
                var newTarget = GetTargetVis(entity, config.FindTargetRadius, animalConfig.attackAnimals, blockedTargets);
                if (target != newTarget && IsOriginalPassive())
                    return;
                target = newTarget;
                if (!CanAttack(target))
                    target = GetTargetVis(entity, config.FindTargetRadius, animalConfig.attackAnimals, blockedTargets);
                if (state == AIState.Walking) {
                    if (target == null)
                        Walk();
                    else
                        state = AIState.Attack;
                }
                if (state == AIState.Attack) {
                    if (target == null) {
                        IsOriginalPassive();
                        state = AIState.Walking;
                    } else {
                        var distance = Vector3.Distance(entity.transform.position, target.transform.position);
                        if (distance > config.FindTargetRadius) {
                            state = AIState.Walking;
                            target = null;
                            IsOriginalPassive();
                            return;
                        }
                        if (distance > animalConfig.attackRange)
                            MoveToTarget(target.transform.position);
                        if (distance <= animalConfig.attackRange) {
                            if (!Physics.Linecast(new Vector3(target.transform.position.x, target.WorldSpaceBounds().ToBounds().center.y, target.transform.position.z), entity.transform.position, blockLayer))
                                Attack();
                            else
                                MoveToTarget(target.transform.position);
                        }
                        npc.SendNetworkUpdate(global::BasePlayer.NetworkQueue.Update);
                    }
                }
            }
            bool IsLowHP() => npc._maxHealth / 10 > npc.health;
            bool IsOriginalPassive() {
                if (type == AType.Aggressive && originalType == AType.Passive) {
                    type = AType.Passive;
                    return true;
                }
                return false;
            }
            void MoveToTarget(Vector3 targetPosition) {
                npc.health -= 0.1f;
                int rotate = 0;
                Move:
                Quaternion rotation = Quaternion.Euler(GetRotationToTarget(targetPosition).eulerAngles + new Vector3(0, rotate * 10f, 0));
                Vector3 position = GetGround(npc.ServerPosition + (rotation * Vector3.forward) * animalConfig.speed * rateScale);
                if (position != Vector3.zero && !IsNearBuilding(position) && position.y > 0f && position.y - npc.ServerPosition.y <= 2f && Vector3.Distance(entity.transform.position, targetPosition) > 1f) {
                    npc.ServerPosition = position;
                    npc.ServerRotation = rotation;
                    return;
                }
                if (rotate > -16) {
                    rotate--;
                    goto Move;
                }
                else if (target != null)
                    npc.ServerRotation = GetRotationToTarget(target.transform.position);
            }
            void Walk() {
                int moveAttempt = 0;
                Move:
                if (UnityEngine.Random.Range(0, 7) == 0)
                    moveAttempt += UnityEngine.Random.Range(-4, 4);
                if (wasDamaged) {
                    moveAttempt = UnityEngine.Random.Range(-18, 18);
                    wasDamaged = false;
                }
                Quaternion rotation = Quaternion.Euler(npc.ServerRotation.eulerAngles + new Vector3(0, UnityEngine.Random.Range(moveAttempt * 10f * -1, moveAttempt * 10f), 0));
                Vector3 position = GetGround(npc.ServerPosition + ((rotation * Vector3.forward) * (state == AIState.Scared ? animalConfig.boostSpeed * rateScale : animalConfig.speed * rateScale)));
                if (position != Vector3.zero && !IsNearBuilding(position) && position.y > 0f && position.y - npc.ServerPosition.y <= 2f) {
                    if (!CanWalk()) return;
                    if (UnityEngine.Random.Range(0, 100) == 1 && state != AIState.Scared)
                        nextWalkTime += UnityEngine.Random.Range(2, 10);
                    npc.ServerPosition = position;
                    npc.ServerRotation = rotation;
                    return;
                } else {
                    if (moveAttempt < 18) {
                        moveAttempt++;
                        goto Move;
                    }
                }
            }
            bool Sleep {
                set {
                    if (!value) {
                        if (!InvokeHandler.IsInvoking(this, UpdateAI))
                            InvokeHandler.InvokeRepeating(this, UpdateAI, 0, updateRate);
                        Pause(3.3f);
                    } else
                        InvokeHandler.CancelInvoke(this, UpdateAI);
                    npc.SetAiFlag(global::BaseNpc.AiFlags.Sleeping, value);
                    state = value ? AIState.Sleeping : AIState.Walking;
                }
            }
            void CheckTime() {
                if (state == AIState.Attack || state == AIState.Scared) return;
                var currentHour = (int)TOD_Sky.Instance.Cycle.Hour;
                if (currentHour == (int)config.SleepStartTime && state != AIState.Sleeping) {
                    Sleep = true;
                    return;
                }
                if (currentHour == (int)config.SleepEndTime && state == AIState.Sleeping) {
                    Sleep = false;
                    return;
                }
                if (currentHour < (int)config.SleepStartTime && currentHour > (int)config.SleepEndTime) {
                    if (state == AIState.Sleeping)
                        Sleep = false;
                }
                else {  
                    if (state != AIState.Sleeping)
                        Sleep = true;
                }
            }
            bool CanAttack(BaseEntity target) {
                if (target == null) return true;
                TargetMemory value;
                if (targetMemory.TryGetValue(target.net.ID, out value)) {
                    if (Time.realtimeSinceStartup - value.lastAttackTime > 15) {
                        if (Vector3.Distance(value.lastPosition, target.transform.position) < 0.1f) {
                            if (blockedTargets.Count == 10)
                                blockedTargets.Remove(blockedTargets.ElementAt(0));
                            blockedTargets.Add(target.net.ID);
                            return false;
                        }
                        else
                            value.lastAttackTime = Time.realtimeSinceStartup;
                    }
                    value.lastPosition = target.transform.position;
                }
                else {
                    if (targetMemory.Count == 10)   
                        targetMemory.Remove(targetMemory.ElementAt(0).Key);
                    var newTm = new TargetMemory();
                    newTm.target = target;
                    newTm.lastAttackTime = Time.realtimeSinceStartup;
                    newTm.lastPosition = target.transform.position;
                    targetMemory[target.net.ID] = newTm;
                }
                return true;
            }
            bool CanWalk() {
                if (Time.realtimeSinceStartup < nextWalkTime)
                    return false;
                nextWalkTime = Time.realtimeSinceStartup;
                return true;
            }
            void CheckStuck() {
                if (state != AIState.Attack && state != AIState.Sleeping && CanWalk() && entity.transform.position == lastPosition) {
                    stuckTime+= updateRate;
                    if (stuckTime > 180)
                        DestroyAndKill();
                }
                else
                    stuckTime = 0;
            }
            Quaternion GetRotationToTarget(Vector3 targetPosition) => Quaternion.LookRotation(targetPosition - entity.transform.position);
            Vector3 GetGround(Vector3 point) {
                RaycastHit hit;
                if (Physics.Raycast(new Ray(new Vector3(point.x, point.y + 50, point.z), Vector3.down), out hit, float.PositiveInfinity, LayerMask.GetMask("World")))
                    return Vector3.zero;
                if (!Physics.Raycast(new Ray(new Vector3(point.x, point.y + 50, point.z), Vector3.down), out hit, float.PositiveInfinity, layerGround))
                    return Vector3.zero;
                return hit.point;
            }
            bool IsStucked() {
                var colliders = Physics.OverlapSphere(entity.transform.position, 10, blockLayer);
                foreach (var col in colliders)
                    if (col.name.Contains("cave"))
                        return true;
                RaycastHit hit;
                if (Physics.Raycast(new Ray(new Vector3(entity.transform.position.x, entity.transform.position.y + 50, entity.transform.position.z), Vector3.down), out hit, float.PositiveInfinity, blockLayer))
                    return true;
                return false;
            }
            bool IsNearBuilding(Vector3 point) => Physics.OverlapSphere(point, 1.1f, layerBuilding).Count() > 0;
            private void Attack() {
                var combatTarget = target as BaseCombatEntity;
                if (combatTarget != null) {
                    targetMemory[target.net.ID].lastAttackTime = Time.realtimeSinceStartup;
                    npc.ServerRotation = GetRotationToTarget(target.transform.position);
                    combatTarget.Hurt(animalConfig.damage, npc.AttackDamageType, npc, true);
                    npc.SignalBroadcast(global::BaseEntity.Signal.Attack, null);
                    npc.ClientRPC<Vector3>(null, "Attack", combatTarget.ServerPosition);
                    npc.FoodTarget = target;
                    Pause(1.1f);
                } else
                    target = null;
            }
            public void OnTakeDamage(BaseEntity from, HitInfo info) {
                if (from == null || from.net == null || info == null) return;
                wasDamaged = true;
                TargetMemory value;
                if (targetMemory.TryGetValue(from.net.ID, out value)) {
                    value.lastAttackTime = Time.realtimeSinceStartup;
                    value.lastPosition = from.transform.position;
                }
                blockedTargets.Remove(from.net.ID);
                if (animalConfig.armor > 0) {
                    float armor = animalConfig.armor;
                    if (armor >= 100)
                        info.damageTypes.ScaleAll(0);
                    else
                        for (int i = 0; i < info.damageTypes.types.Length; i++)
                            info.damageTypes.Set((DamageType)i, info.damageTypes.Get((DamageType)i) / (100 / armor));
                }
                nextWalkTime = Time.realtimeSinceStartup;
                if (state == AIState.Sleeping)
                    Sleep = false;
                if (config.PassiveRunAway) {
                    if (originalType == AType.Passive) {
                        state = AIState.Scared;
                        return;
                    }
                }
                if (Vector3.Distance(from.transform.position, entity.transform.position) <= config.FindTargetRadius) {
                    target = from;
                    state = AIState.Attack;
                    type = AType.Aggressive;
                }
            }
            bool Pause(float time = 0) {
                if (time != 0) {
                    nextUpdate = Time.realtimeSinceStartup + time;
                    return true;
                } 
                if (nextUpdate > Time.realtimeSinceStartup)
                    return true;
                return false;
            }
            public void DestroyAndKill() {
                instance.animalAI.Remove(entity);
                npc.Kill();
                Destroy();
            }
            public void Destroy() => Destroy(this);
        }
        #endregion

        #region Helpers
        static AnimalConfig GetAnimalSpeed(BaseNpc npc) {
            if (npc.ShortPrefabName == "horse")
                return config.Horse;
            if (npc.ShortPrefabName == "wolf")
                return config.Wolf;
            if (npc.ShortPrefabName == "boar")
                return config.Boar;
            if (npc.ShortPrefabName == "stag")
                return config.Stag;
            if (npc.ShortPrefabName == "chicken")
                return config.Chicken;
            if (npc.ShortPrefabName == "bear")
                return config.Bear;
            if (npc.ShortPrefabName == "zombie")
                return config.Zombie;
            return config.Chicken;
        }
        static BaseEntity GetTargetVis(BaseEntity entity, float radius, bool attackAnimals, HashSet<uint> exceptList, bool getNearest = true) {
            Dictionary<BaseEntity, float> entityDistance = new Dictionary<BaseEntity, float>();
            var hits = new List<BaseEntity>();
            var needToCheckExceptList = exceptList.Count > 0 ? true : false;
            Vis.Entities(entity.transform.position, radius, hits, attackAnimals ? - 1 : Rust.Layers.Server.Players, QueryTriggerInteraction.Ignore);
            if (hits.Count == 0) return null;
            foreach (var e in hits) {
                if (e is NPCMurderer) continue;
                var player = e as BasePlayer;
                if (player == null && e as BaseNpc == null) continue;
                if (e == entity) continue;
                if (needToCheckExceptList && exceptList.Contains(e.net.ID)) continue;
                var dist = Vector3.Distance(e.transform.position, entity.transform.position);
                if (dist < radius && e.Health() > 0) {
                    if (player != null)
                        if (instance.IsPlayerHaveImmunity(player.UserIDString)) continue;
                    entityDistance[e] = dist;
                }
            }
            if (entityDistance.Count == 0) return null;
            if (!getNearest) return entityDistance.First().Key;
            return entityDistance.First(x => x.Value == entityDistance.Values.Min()).Key;
        }
        void TakeNPC(BaseEntity entity) {
            if (animalAI.ContainsKey(entity)) return;
            entity.gameObject.AddComponent<AIAnimal>();
        }
        AIAnimal GetAIFromEntity(BaseEntity entity) {
            AIAnimal value;
            if (animalAI.TryGetValue(entity, out value))
                return value;
            return null;
        }
        bool IsPlayerHaveImmunity(string userID) => permission.UserHasPermission(userID, ignorePermission);
        #endregion
        
        #region Oxide
        void Loaded() {
            instance = this;
            InitializeConfig();
            ConVar.AI.think = false;
        }
        void OnServerInitialized() {
            permission.RegisterPermission(ignorePermission, this);
            foreach (var npc in GameObject.FindObjectsOfType<BaseNpc>())
                TakeNPC((BaseEntity)npc);
            Puts($"Initialized {animalAI.Count} animals.");
        }
        void OnEntitySpawned(BaseNetworkable entity) {
            if (!(entity is BaseNpc)) return;
            TakeNPC((BaseEntity)entity);
        }
        void OnEntityKill(BaseNetworkable entity) {
            if (entity as BaseNpc == null) return;
            var ai = GetAIFromEntity((BaseEntity)entity);
            if (ai != null) {
                ai.Destroy();
                animalAI.Remove((BaseEntity)entity);
            }
        }
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info) {
            if (!(entity is BaseNpc)) return;
            var ai = GetAIFromEntity(entity);
            ai?.OnTakeDamage(info.Initiator, info);
        }
        void Unload() {
            foreach (var ai in animalAI)
                ai.Value?.Destroy();
        }
        #endregion

        #region Footer
    }
}
#endregion