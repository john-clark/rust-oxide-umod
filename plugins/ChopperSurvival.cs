// Requires: EventManager
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Linq;
using Rust;

namespace Oxide.Plugins
{
    [Info("ChopperSurvival", "k1lly0u", "0.2.83", ResourceId = 1590)]
    [Description("Helicopter survival event for Event Manager")]
    class ChopperSurvival : RustPlugin
    {
        [PluginReference] EventManager EventManager;
        [PluginReference] Plugin Spawns;

        static ChopperSurvival instance;

        private bool usingCS;
        private bool hasStarted;
        private bool isEnding;

        private List<CSPlayer> CSPlayers = new List<CSPlayer>();
        private List<Timer> GameTimers = new List<Timer>();
        private List<CSHelicopter> CSHelicopters = new List<CSHelicopter>();

        private float adjHeliHealth;
        private float adjHeliBulletDamage;
        private float adjMainRotorHealth;
        private float adjEngineHealth;
        private float adjTailRotorHealth;
        private float adjHeliAccuracy;

        private int gameRounds;
        private int enemyCount;
        private int currentWave;

        private string spawnFile;
        private string kit;

        const string heliExplosion = "assets/prefabs/npc/patrol helicopter/effects/heli_explosion.prefab";

        #region Classes
        class CSPlayer : MonoBehaviour
        {
            public BasePlayer player;
            public int deaths;
            public int points;
            public List<string> openUi;

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                enabled = false;
                deaths = 0;
                points = 0;
                openUi = new List<string>();
            }
            void OnDestroy()
            {
                DestroyAllUI();
            }
            public void AddUi(CuiElementContainer container, string name)
            {
                openUi.Add(name);
                CuiHelper.AddUi(player, container);
            }
            public void DestroyUi(string name)
            {
                CuiHelper.DestroyUi(player, name);
                if (openUi.Contains(name))
                    openUi.Remove(name);
            }
            public void DestroyAllUI()
            {
                foreach (var element in openUi)
                    CuiHelper.DestroyUi(player, element);
                openUi.Clear();
            }
        }

        class CSHelicopter : MonoBehaviour
        {
            public BaseHelicopter helicopter;
            private PatrolHelicopterAI ai;
            private Vector3 targetPos;
            private bool isDieing;

            public Dictionary<StatType, StatMonitor> stats;
            public string heliId;

            void Awake()
            {
                helicopter = GetComponent<BaseHelicopter>();
                ai = GetComponent<PatrolHelicopterAI>();
                heliId = CuiHelper.GetGuid();
                isDieing = false;
                enabled = false;
            }
            void OnDestroy()
            {
                enabled = false;
                instance.DestroyHealthUI(heliId);
                CancelInvoke();
            }
            public void SpawnHelicopter(Vector3 targetPos, float rotor, float tail, float engine, float body, float damage)
            {
                this.targetPos = targetPos;
                stats = new Dictionary<StatType, StatMonitor>
                {
                    {StatType.Body, new StatMonitor {max = body, value = body } },
                    {StatType.Damage, new StatMonitor {max = damage, value = damage } },
                    {StatType.Engine, new StatMonitor {max = engine, value = engine } },
                    {StatType.Rotor, new StatMonitor {max = rotor, value = rotor } },
                    {StatType.Tail, new StatMonitor {max = tail, value = tail } }
                };
                ai.spawnTime = UnityEngine.Time.realtimeSinceStartup * 10;
                var spawnPos = FindSpawnPosition(targetPos);
                helicopter.transform.position = spawnPos;
                enabled = true;
                CheckDistance();
            }

            public int TakeDamage(HitInfo info)
            {
                if (isDieing)
                    return 0;

                int pointValue = 0;
                float damage = info.damageTypes.Total();
                bool hitWeakSpot = false;

                for (int i = 0; i < helicopter.weakspots.Length; i++)
                {
                    BaseHelicopter.weakspot _weakspot = helicopter.weakspots[i];
                    string[] strArrays = _weakspot.bonenames;
                    for (int j = 0; j < strArrays.Length; j++)
                    {
                        string str = strArrays[j];
                        if (info.HitBone == StringPool.Get(str))
                        {
                            switch (str)
                            {
                                case "engine_col":
                                    hitWeakSpot = true;
                                    stats[StatType.Engine].DealDamage(damage);
                                    pointValue = instance.configData.Scoring.HeliHitPoints;
                                    break;
                                case "tail_rotor_col":
                                    hitWeakSpot = true;
                                    stats[StatType.Tail].DealDamage(damage);
                                    pointValue = instance.configData.Scoring.RotorHitPoints;
                                    if (stats[StatType.Tail].value < 25)
                                        helicopter.weakspots[i].WeakspotDestroyed();
                                    break;
                                case "main_rotor_col":
                                    hitWeakSpot = true;
                                    stats[StatType.Rotor].DealDamage(damage);
                                    pointValue = instance.configData.Scoring.RotorHitPoints;
                                    if (stats[StatType.Rotor].value < 25)
                                        helicopter.weakspots[i].WeakspotDestroyed();
                                    break;
                            }
                        }
                    }
                }
                if (!hitWeakSpot)
                {
                    pointValue = 1;
                    stats[StatType.Body].DealDamage(damage);
                }
                if (stats[StatType.Body].value < 5 || stats[StatType.Engine].value < 5 || (stats[StatType.Tail].value < 5 && stats[StatType.Rotor].value < 5))
                {
                    KillHelicopter(true);
                    isDieing = true;
                    if (info?.InitiatorPlayer != null)
                        instance.EventManager.AddStats(info.InitiatorPlayer, EventManager.StatType.Choppers);
                }
                return pointValue;
            }
            public void KillHelicopter(bool dropGibs)
            {
                ai.ExitCurrentState();
                instance.DieInstantly(helicopter);
            }
            private void CheckDistance()
            {
                if (isDieing)
                    return;
                var currentPos = base.transform.position;
                if (currentPos.y < TerrainMeta.HeightMap.GetHeight(currentPos))
                {
                    KillHelicopter(false);
                    return;
                }
                if (targetPos != null)
                {
                    ai.SetTargetDestination(targetPos + new Vector3(0.0f, instance.configData.HelicopterSettings.DestinationHeightAdjust, 0.0f));
                    if (Vector3Ex.Distance2D(currentPos, targetPos) < 60)
                    {
                        if (instance.configData.HelicopterSettings.UseRockets)
                        {
                            if (UnityEngine.Random.Range(1, 3) == 2)
                                ai.State_Strafe_Think(0);
                        }
                        else ai.State_Orbit_Think(40f);
                    }
                    else
                        ai.State_Move_Enter(targetPos + new Vector3(0.0f, instance.configData.HelicopterSettings.DestinationHeightAdjust, 0.0f));
                }
                Invoke("CheckDistance", instance.configData.HelicopterSettings.CheckDistanceTimer);
            }
            private Vector3 FindSpawnPosition(Vector3 arenaPos)
            {
                float x = 0;
                float y = 0;
                float angleRadians = 0;
                Vector2 circleVector;
                angleRadians = UnityEngine.Random.Range(0, 180) * Mathf.PI / 180.0f;
                x = instance.configData.HelicopterSettings.SpawnDistance * Mathf.Cos(angleRadians);
                y = instance.configData.HelicopterSettings.SpawnDistance * Mathf.Sin(angleRadians);
                circleVector = new Vector2(x, y);
                Vector3 finalPos = new Vector3(circleVector.x + arenaPos.x, TerrainMeta.HeightMap.GetHeight(new Vector3(circleVector.x + arenaPos.x, 0, circleVector.y + arenaPos.z)), circleVector.y + arenaPos.z);
                if (finalPos.y < 1) finalPos.y = 5;
                finalPos.y = finalPos.y + 50;
                return finalPos;
            }
        }
        enum StatType
        {
            Rotor, Tail, Engine, Body, Damage
        }
        public class StatMonitor
        {
            public float max;
            public float value;
            public void DealDamage(float amount) => value -= amount;
        }
        #endregion

        #region UI Elements
        private void UpdateScores()
        {
            if (usingCS && hasStarted && configData.EventSettings.ShowScoreboard)
            {
                var sortedList = CSPlayers.OrderByDescending(pair => pair.points).ToList();
                var scoreList = new Dictionary<ulong, EventManager.Scoreboard>();
                foreach (var entry in sortedList)
                {
                    if (scoreList.ContainsKey(entry.player.userID)) continue;
                    scoreList.Add(entry.player.userID, new EventManager.Scoreboard { Name = entry.player.displayName, Position = sortedList.IndexOf(entry), Score = entry.points });
                }
                EventManager.UpdateScoreboard(new EventManager.ScoreData { Additional = null, ScoreType = "Points", Scores = scoreList });
            }
        }
        private CuiElementContainer CreateHealthIndicator(CSHelicopter heli, int count)
        {
            var panelName = heli.heliId;
            var pos = CalcHealthPos(count);
            var element = EventManager.UI.CreateElementContainer(panelName, "0.1 0.1 0.1 0.7", $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", false, "Hud");

            CreateHealthElement(ref element, panelName, "Body Health", heli.stats[StatType.Body].max, heli.stats[StatType.Body].value, 0.75f);
            CreateHealthElement(ref element, panelName, "Main Rotor", heli.stats[StatType.Rotor].max, heli.stats[StatType.Rotor].value, 0.5f);
            CreateHealthElement(ref element, panelName, "Tail Rotor", heli.stats[StatType.Tail].max, heli.stats[StatType.Tail].value, 0.25f);
            CreateHealthElement(ref element, panelName, "Engine Health", heli.stats[StatType.Engine].max, heli.stats[StatType.Engine].value, 0f);

            return element;
        }
        private void CreateHealthElement(ref CuiElementContainer element, string panelName, string name, float maxHealth, float currentHealth, float minY)
        {
            var percent = System.Convert.ToDouble((float)currentHealth / (float)maxHealth);
            var yMax = 0.98 * percent;
            string color = "0.2 0.6 0.2 0.9";
            if (percent <= 0.5)
                color = "1 0.5 0 0.9";
            if (percent <= 0.15)
                color = "0.698 0.13 0.13 0.9";
            EventManager.UI.CreatePanel(ref element, panelName, color, $"0.01 {minY + 0.005}", $"{yMax} {minY + 0.24}");
            EventManager.UI.CreateLabel(ref element, panelName, "", name, 8, $"0 {minY}", $"1 {minY + 0.25}");
        }
        private void DestroyHealthUI(string heliId)
        {
            foreach (var entry in CSPlayers)
                entry.DestroyUi(heliId);
        }
        private void DestroyAllHealthUI(BasePlayer player)
        {
            var csPlayer = player.GetComponent<CSPlayer>();
            if (csPlayer != null)
                csPlayer.DestroyAllUI();
            else
            {
                foreach (var heli in CSHelicopters)
                    CuiHelper.DestroyUi(player, heli.heliId);
            }
        }
        private void RefreshHealthUI(CSHelicopter heli)
        {
            if (!heli) return;
            if (configData.EventSettings.ShowHeliHealthUI)
            {
                foreach (var entry in CSPlayers)
                {
                    entry.DestroyUi(heli.heliId);
                    entry.AddUi(CreateHealthIndicator(heli, CSHelicopters.IndexOf(heli)), heli.heliId);
                }
            }
        }
        private void RefreshPlayerHealthUI(CSPlayer player)
        {
            if (player == null) return;
            if (configData.EventSettings.ShowHeliHealthUI)
            {
                foreach (var heli in CSHelicopters)
                {
                    player.DestroyUi(heli.heliId);
                    player.AddUi(CreateHealthIndicator(heli, CSHelicopters.IndexOf(heli)), heli.heliId);
                }
            }
        }
        private float[] CalcHealthPos(int number)
        {
            Vector2 position = new Vector2(0.01f, 0.9f);
            Vector2 dimensions = new Vector2(0.07f, 0.08f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 3)
            {
                offsetX = (0.0033f + dimensions.x) * number;
            }
            if (number > 2 && number < 6)
            {
                offsetX = (0.0033f + dimensions.x) * (number - 3);
                offsetY = (-0.005f - dimensions.y) * 1;
            }
            if (number > 5 && number < 9)
            {
                offsetX = (0.0033f + dimensions.x) * (number - 6);
                offsetY = (-0.005f - dimensions.y) * 2;
            }
            if (number > 8 && number < 12)
            {
                offsetX = (0.0033f + dimensions.x) * (number - 9);
                offsetY = (-0.005f - dimensions.y) * 3;
            }
            if (number > 11 && number < 15)
            {
                offsetX = (0.0033f + dimensions.x) * (number - 12);
                offsetY = (-0.005f - dimensions.y) * 4;
            }

            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }
        #endregion

        #region Oxide hooks
        void OnServerInitialized()
        {
            usingCS = false;
            hasStarted = false;

            LoadVariables();
            RegisterMessages();

            instance = this;
            kit = configData.EventSettings.DefaultKit;
            spawnFile = configData.EventSettings.DefaultSpawnfile;
        }
        void Unload()
        {
            foreach (var eventPlayer in CSPlayers)
                eventPlayer.DestroyAllUI();
            if (usingCS && hasStarted)
                EventManager.EndEvent();
			else DestroyEvent();
        }
		void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
			if (usingCS && hasStarted && entity != null && !isEnding)
            {
                var attacker = hitInfo.InitiatorPlayer;
                if (attacker == null) return;
                var helicopter = entity.GetComponent<CSHelicopter>();

                if (helicopter != null && !attacker.GetComponent<CSPlayer>())
				{
                    hitInfo.damageTypes = new DamageTypeList();
                    hitInfo.HitEntity = null;
                    hitInfo.HitMaterial = 0;
                    hitInfo.PointStart = Vector3.zero;
                    return;
				}
                if (attacker.GetComponent<CSPlayer>())
                {
                    if (entity is BasePlayer)
                    {
                        if (entity.ToPlayer() == null || hitInfo == null) return;
                        if (entity.ToPlayer().userID != hitInfo.Initiator.ToPlayer().userID)
                        {
                            if (entity.GetComponent<CSPlayer>())
                            {
                                hitInfo.damageTypes.ScaleAll(configData.PlayerSettings.FFDamageScale);
                                SendReply(attacker, MSG("fFire"));
                            }
                        }
                    }
                    if (helicopter != null)
                    {
                        int points = entity.GetComponent<CSHelicopter>().TakeDamage(hitInfo);
                        RefreshHealthUI(helicopter);
                        hitInfo.damageTypes = new DamageTypeList();
                        hitInfo.HitEntity = null;
                        hitInfo.HitMaterial = 0;
                        hitInfo.PointStart = Vector3.zero;
                        attacker.GetComponent<CSPlayer>().points += points;
                    }
                }
			}
        }
        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (usingCS && hasStarted)
            {
                if (entity is BaseEntity)
                {
                    var entityName = entity.ShortPrefabName;

                    if (entityName.Contains("napalm"))
                    {
                        if (!configData.HelicopterSettings.UseRockets)
                        {
                            entity.Kill();
                        }
                    }

                    if (entityName.Contains("servergibs_patrolhelicopter"))
                        entity.Kill();
                }
            }
        }
        object CanBeTargeted(BaseCombatEntity target, MonoBehaviour turret)
        {
            if (usingCS && hasStarted)
            {
                if (target == null || turret == null) return null;
                if (target is BasePlayer && turret is HelicopterTurret)
                {
                    if ((turret as HelicopterTurret)._heliAI && (turret as HelicopterTurret)._heliAI.GetComponent<CSHelicopter>())
                    {
                        if ((target as BasePlayer).GetComponent<CSPlayer>())
                            return null;
                        else
                            return false;
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            return null;
        }
        void OnChopperDeath(CSHelicopter helicopter)
        {
            CSHelicopters.Remove(helicopter);
            ExtinguishFires(helicopter.helicopter.transform.position);
            UnityEngine.Object.Destroy(helicopter);
            if (CSHelicopters.Count <= 0)
                NextRound();
        }
        #endregion

        #region Round Management
        private void StartRounds()
        {
            currentWave = 1;
            SendMessage(string.Format(MSG("firstWave"), configData.HelicopterSettings.SpawnBeginTimer));
            SetPlayers();
            GameTimers.Add(timer.Once(configData.HelicopterSettings.SpawnBeginTimer, () => SpawnWave()));
            GameTimers.Add(timer.Repeat(5, 0, () => UpdateScores()));
        }
        private void NextRound()
        {
            DestroyTimers();
            GameTimers.Add(timer.Repeat(5, 0, () => UpdateScores()));
            currentWave++;
            AddPoints();
            if (EventManager.EventMode == EventManager.GameMode.Normal)
            {
                if (currentWave > gameRounds)
                {
                    FindWinner();
                    return;
                }
            }
            SetPlayers();
            SendMessage(string.Format(MSG("nextWave"), configData.HelicopterSettings.SpawnWaveTimer));
            GameTimers.Add(timer.Once(configData.HelicopterSettings.SpawnWaveTimer, () => SpawnWave()));
        }
        private void SetPlayers()
        {
            foreach (CSPlayer hs in CSPlayers)
            {
                EventManager.GivePlayerKit(hs.player, kit);
                hs.player.health = configData.PlayerSettings.StartHealth;
            }
        }
        #endregion

        #region Helicopter Spawning
        private void SpawnWave()
        {
            if (usingCS && hasStarted)
            {
                var num = System.Math.Ceiling(((float)currentWave / (float)gameRounds) * (float)enemyCount);
                if (num < 1) num = 1;
                if (currentWave == 1) InitStatModifiers();
                else SetStatModifiers();
                SpawnHelicopter((int)num);
                if (configData.EventSettings.ShowHeliHealthUI)
                {
                    foreach(var heli in CSHelicopters)
                    {
                        RefreshHealthUI(heli);
                    }
                }
                MessageAllPlayers("", string.Format(MSG("waveInbound"), currentWave));
            }
        }
		private void SpawnHelicopter(int num)
        {
            bool lifetime = false;
			if (ConVar.PatrolHelicopter.lifetimeMinutes == 0)
			{
				ConVar.PatrolHelicopter.lifetimeMinutes = 1;
				lifetime = true;
			}

            for (int i = 0; i < num; i++)
            {
                BaseHelicopter entity = (BaseHelicopter)GameManager.server.CreateEntity("assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab", new Vector3(), new Quaternion(), true);
                entity.enableSaving = false;
                entity.Spawn();
                var component = entity.gameObject.AddComponent<CSHelicopter>();
                component.SpawnHelicopter(GetDestination(), adjMainRotorHealth, adjTailRotorHealth, adjEngineHealth, adjHeliHealth, adjHeliBulletDamage);
                CSHelicopters.Add(component);
                if (entity == null) Puts("null heli");
            }
			if(lifetime)
				timer.Once(5f, () => ConVar.PatrolHelicopter.lifetimeMinutes = 0);
            ConVar.PatrolHelicopter.bulletAccuracy = adjHeliAccuracy;
        }
        private Vector3 GetDestination() => (Vector3)Spawns.Call("GetRandomSpawn", spawnFile);
        #endregion

        #region Stats
        private void InitStatModifiers()
        {
            adjHeliBulletDamage = configData.HelicopterSettings.HeliBulletDamage;
            adjHeliHealth = configData.HelicopterSettings.HeliHealth;
            adjMainRotorHealth = configData.HelicopterSettings.MainRotorHealth;
            adjEngineHealth = configData.HelicopterSettings.EngineHealth;
            adjTailRotorHealth = configData.HelicopterSettings.TailRotorHealth;
            adjHeliAccuracy = configData.HelicopterSettings.HeliAccuracy;
            if (configData.EventSettings.ShowStatsInConsole) ShowHeliStats();
        }
        private void SetStatModifiers()
        {
            if (usingCS)
            {
                var HeliModifier = configData.HelicopterSettings.HeliModifier;
                adjHeliBulletDamage *= HeliModifier;
                adjHeliHealth *= HeliModifier;
                adjMainRotorHealth *= HeliModifier;
                adjEngineHealth *= HeliModifier;
                adjTailRotorHealth = adjTailRotorHealth * HeliModifier;
                adjHeliAccuracy = adjHeliAccuracy - (HeliModifier / 1.5f);
                if (configData.EventSettings.ShowStatsInConsole) ShowHeliStats();
            }
        }
        private void ShowHeliStats()
        {
            Puts("---- CS Heli Stats ----");
            Puts("Modifier: " + configData.HelicopterSettings.HeliModifier);
            Puts("Damage: " + adjHeliBulletDamage);
            Puts("Health: " + adjHeliHealth);
            Puts("Main rotor: " + adjMainRotorHealth);
            Puts("Engine: " + adjEngineHealth);
            Puts("Tail rotor: " + adjTailRotorHealth);
            Puts("Accuracy: " + adjHeliAccuracy);
        }

        #endregion

        #region Event Destruction
        private void DieInstantly(BaseCombatEntity entity)
        {
            if (!entity.IsDestroyed)
            {
                Effect.server.Run(heliExplosion, entity.transform.position, Vector3.up, null, true);
                OnChopperDeath(entity.GetComponent<CSHelicopter>());
                entity.health = 0f;
                entity.lifestate = BaseCombatEntity.LifeState.Dead;
                entity.Kill(BaseNetworkable.DestroyMode.None);
            }
        }
        private void ExtinguishFires(Vector3 position)
        {
            timer.In(3, () =>
            {
                var allobjects = Physics.OverlapSphere(position, 150);
                foreach (var gobject in allobjects)
                {
                    if (gobject.name.ToLower().Contains("oilfireballsmall") || gobject.name.ToLower().Contains("napalm"))
                    {
                        var fire = gobject.GetComponent<BaseEntity>();
                        if (fire == null) continue;
                        if (BaseEntity.saveList.Contains(fire))
                            BaseEntity.saveList.Remove(fire);
                        fire.Kill();
                    }
                }
            });
        }
        private void DestroyEvent()
        {
            hasStarted = false;
            DestroyTimers();
            DestroyHelicopters();
        }
        private void DestroyHelicopters()
        {
            var helis = UnityEngine.Object.FindObjectsOfType<CSHelicopter>();
            if (helis != null)
                foreach (var heli in helis)
                    DieInstantly(heli.helicopter);

            CSHelicopters.Clear();
        }
        private void DestroyTimers()
        {
            if (GameTimers != null)
            {
                foreach (var time in GameTimers)
                    time.Destroy();
                GameTimers.Clear();
            }
        }
        #endregion

        private Vector3 FindGround(Vector3 sourcePos)
        {
            RaycastHit hitInfo;
            if (Physics.Raycast(sourcePos, Vector3.down, out hitInfo, LayerMask.GetMask("Terrain", "World", "Construction")))
                sourcePos.y = hitInfo.point.y;
            sourcePos.y = Mathf.Max(sourcePos.y, TerrainMeta.HeightMap.GetHeight(sourcePos));
            return sourcePos;
        }

        #region EventManager hooks
        void RegisterGame()
        {
            EventManager.Events eventData = new EventManager.Events
            {
                CloseOnStart = true,
                DisableItemPickup = false,
                EnemiesToSpawn = configData.EventSettings.MaximumHelicopters,
                EventType = Title,
                GameMode = EventManager.GameMode.Normal,
                GameRounds = configData.EventSettings.MaximumWaves,
                Kit = configData.EventSettings.DefaultKit,
                MaximumPlayers = 0,
                MinimumPlayers = 2,
                ScoreLimit = 0,
                Spawnfile = configData.EventSettings.DefaultSpawnfile,
                Spawnfile2 = null,
                SpawnType = EventManager.SpawnType.Consecutive,
                RespawnType = EventManager.RespawnType.Timer,
                RespawnTimer = 5,
                UseClassSelector = false,
                WeaponSet = null,
                ZoneID = configData.EventSettings.DefaultZoneID

            };
            EventManager.EventSetting eventSettings = new EventManager.EventSetting
            {
                CanChooseRespawn = true,
                CanUseClassSelector = true,
                CanPlayBattlefield = true,
                ForceCloseOnStart = true,
                IsRoundBased = true,
                LockClothing = false,
                RequiresKit = true,
                RequiresMultipleSpawns = false,
                RequiresSpawns = true,
                ScoreType = null,
                SpawnsEnemies = true
            };
            var success = EventManager.RegisterEventGame(Title, eventSettings, eventData);
            if (success == null)
                Puts(MSG("noEvent"));
        }
        void OnSelectEventGamePost(string name)
        {
            if (Title == name)
                usingCS = true;
            else usingCS = false;
            enemyCount = configData.EventSettings.MaximumHelicopters;
            gameRounds = configData.EventSettings.MaximumWaves;
        }
        void OnEventPlayerSpawn(BasePlayer player)
        {
            if (usingCS && hasStarted && !isEnding)
            {
                if (!player.GetComponent<CSPlayer>())
                    CSPlayers.Add(player.gameObject.AddComponent<CSPlayer>());
                if (player.IsSleeping())
                {
                    player.EndSleeping();
                    timer.In(1, () => OnEventPlayerSpawn(player));
                    return;
                }
                EventManager.GivePlayerKit(player, kit);
				player.metabolism.hydration.value = configData.PlayerSettings.StartHydration;
				player.metabolism.calories.value = configData.PlayerSettings.StartCalories;
				player.health = configData.PlayerSettings.StartHealth;
				timer.Once(3, ()=> { RefreshPlayerHealthUI(player.GetComponent<CSPlayer>()); });
            }
        }
        object OnEventOpenPost()
        {
            if (usingCS)
            {
                CSPlayers = new List<CSPlayer>();
                EventManager.BroadcastToChat(MSG("openBroad"));
            }
            return null;
        }

        object OnEventCancel()
        {
            if (usingCS && hasStarted)
                FindWinner();
            return null;
        }
        object OnEventEndPre()
        {
            if (usingCS)
            {
                DestroyTimers();
                DestroyHelicopters();
                foreach (var eventPlayer in CSPlayers)
                    eventPlayer.DestroyAllUI();
                FindWinner();
            }
            return null;
        }
        object OnEventEndPost()
        {
            if (usingCS)
            {
                hasStarted = false;
                var players = UnityEngine.Object.FindObjectsOfType<CSPlayer>();
                if (players != null)
                    foreach (var player in players)
                        UnityEngine.Object.Destroy(player);
                CSPlayers.Clear();
            }
            return null;
        }
        object OnEventStartPre()
        {
            if (usingCS)
            {
                hasStarted = true;
                isEnding = false;
            }
            return null;
        }
        object OnSelectKit(string kitname)
        {
            if (usingCS)
            {
                kit = kitname;
                return true;
            }
            return null;
        }

        object OnEventJoinPost(BasePlayer player)
        {
            if (usingCS)
            {
                if (player.GetComponent<CSPlayer>())
                    UnityEngine.Object.DestroyImmediate(player.GetComponent<CSPlayer>());
                CSPlayers.Add(player.gameObject.AddComponent<CSPlayer>());
                EventManager.CreateScoreboard(player);
                if (hasStarted)
                    OnEventPlayerSpawn(player);
            }
            return null;
        }
        object OnEventLeavePost(BasePlayer player)
        {
            if (usingCS)
            {
                var csPlayer = player.GetComponent<CSPlayer>();
                if (csPlayer != null)
                {
                    csPlayer.DestroyAllUI();
                    CSPlayers.Remove(csPlayer);
                    UnityEngine.Object.Destroy(csPlayer);
                }
            }
            if (hasStarted && CSPlayers.Count == 0)
            {
                isEnding = true;
                EventManager.EndEvent();
            }
            return null;
        }
        void OnEventPlayerDeath(BasePlayer victim, HitInfo hitinfo)
        {
            if (usingCS && hasStarted)
            {
                DestroyAllHealthUI(victim);
                victim.GetComponent<CSPlayer>().deaths++;
                int LivesLeft = (configData.PlayerSettings.DeathLimit - victim.GetComponent<CSPlayer>().deaths);

                SendMessage(string.Format(MSG("eventDeath"), victim.displayName, victim.GetComponent<CSPlayer>().deaths, configData.PlayerSettings.DeathLimit));
                SendReply(victim, string.Format(MSG("livesLeft"), LivesLeft));

                if (victim.GetComponent<CSPlayer>().deaths >= configData.PlayerSettings.DeathLimit)
                {
                    if (CSPlayers.Count == 1)
                    {
                        Winner(new List<BasePlayer> { victim });
                        return;
                    }
                    EventManager.LeaveEvent(victim);
                }
            }
            return;
        }

        object OnEventStartPost()
        {
            if (usingCS)
            {
                EventManager.CloseEvent();
                StartRounds();
                UpdateScores();
            }
            return null;
        }
        void SetEnemyCount(int number) => enemyCount = number;
        void SetGameRounds(int number) => gameRounds = number;
        void SetSpawnfile(bool isTeamA, string spawnfile) => spawnFile = spawnfile;
        #endregion

        #region Messaging
        void SendMessage(string message)
        {
            if (configData.EventSettings.UseUINotifications)
                EventManager.PopupMessage(message);
            else PrintToChat(message);
        }
        private string MSG(string msg) => lang.GetMessage(msg, this);

        private void MessageAllPlayers(string msg, string keyword = "", bool title = false)
        {
            string titlestring = "";
            if (title) titlestring = lang.GetMessage("title", this);
            EventManager.BroadcastEvent($"{configData.Messaging.MainColor} {titlestring} {keyword}</color> {configData.Messaging.MSGColor} {msg}</color>");
        }
        private void RegisterMessages() => lang.RegisterMessages(new Dictionary<string, string>()
        {
            {"noEvent", "Event plugin doesn't exist" },
            {"noConfig", "Creating a new config file" },
            {"title", "ChopperSurvival : "},
            {"fFire", "Friendly Fire!"},
            {"nextWave", "Next wave in {0} seconds!"},
            {"noPlayers", "The event has no more players, auto-closing."},
            {"openBroad", "Fend off waves of attacking helicopters! Each hit gives you a point, Rotor hits are worth more. The last player standing, or the player with the most points wins!"},
            {"eventWin", "{0} has won the event!"},
            {"eventDeath", "{0} has died {1}/{2} times!"},
            {"waveInbound", "Wave {0} inbound!"},
            {"firstWave", "You have {0} seconds to prepare for the first wave!"},
            {"heliDest", "Helicopter Destroyed!"},
            {"livesLeft", "You have {0} lives remaining!"},
            {"notEnough", "Not enough players to start the event"}
        },this);
        #endregion

        #region Config
        private ConfigData configData;
        class EventSettings
        {
            public string DefaultKit { get; set; }
            public string DefaultSpawnfile { get; set; }
            public string DefaultZoneID { get; set; }
            public int MaximumWaves { get; set; }
            public int MaximumHelicopters { get; set; }
            public bool ShowStatsInConsole { get; set; }
            public bool ShowHeliHealthUI { get; set; }
            public bool ShowScoreboard { get; set; }
            public bool UseUINotifications { get; set; }
        }
        class PlayerSettings
        {
            public float StartHealth { get; set; }
            public float StartHydration { get; set; }
            public float StartCalories { get; set; }
            public int DeathLimit { get; set; }
            public float FFDamageScale { get; set; }
        }
        class HeliSettings
        {
            public float HeliBulletDamage { get; set; }
            public float HeliHealth { get; set; }
            public float MainRotorHealth { get; set; }
            public float TailRotorHealth { get; set; }
            public float EngineHealth { get; set; }
            public float HeliSpeed { get; set; }
            public float HeliAccuracy { get; set; }
            public float HeliModifier { get; set; }
            public float SpawnDistance { get; set; }
            public float CheckDistanceTimer { get; set; }
            public float DestinationHeightAdjust { get; set; }
            public float SpawnWaveTimer { get; set; }
            public float SpawnBeginTimer { get; set; }
            public bool UseRockets { get; set; }
        }
        class Messaging
        {
            public string MainColor { get; set; }
            public string MSGColor { get; set; }
        }
        class ConfigData
        {
            public EventSettings EventSettings { get; set; }
            public HeliSettings HelicopterSettings { get; set; }
            public PlayerSettings PlayerSettings { get; set; }
            public Messaging Messaging { get; set; }
            public Scoring Scoring { get; set; }
        }
        class Scoring
        {
            public int RotorHitPoints { get; set; }
            public int HeliHitPoints { get; set; }
            public int SurvivalTokens { get; set; }
            public int WinnerTokens { get; set; }
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
                EventSettings = new EventSettings
                {
                    DefaultKit = "cskit",
                    DefaultSpawnfile = "csspawns",
                    DefaultZoneID = "cszone",
                    MaximumHelicopters = 4,
                    MaximumWaves = 10,
                    ShowHeliHealthUI = true,
                    ShowStatsInConsole = true,
                    ShowScoreboard = true,
                    UseUINotifications = true
                },
                HelicopterSettings = new HeliSettings
                {
                    CheckDistanceTimer = 10f,
                    DestinationHeightAdjust = 10f,
                    EngineHealth = 800f,
                    HeliAccuracy = 8f,
                    HeliBulletDamage = 4f,
                    HeliHealth = 3800f,
                    HeliModifier = 1.22f,
                    HeliSpeed = 24f,
                    MainRotorHealth = 420f,
                    SpawnBeginTimer = 20f,
                    SpawnDistance = 500f,
                    SpawnWaveTimer = 10f,
                    TailRotorHealth = 300f,
                    UseRockets = true
                },
                Messaging = new Messaging
                {
                    MainColor = "<color=#FF8C00>",
                    MSGColor = "<color=#939393>"
                },
                PlayerSettings = new PlayerSettings
                {
                    DeathLimit = 10,
                    FFDamageScale = 0,
                    StartCalories = 500f,
                    StartHealth = 100f,
                    StartHydration = 250f
                },
                Scoring = new Scoring
                {
                    HeliHitPoints = 1,
                    RotorHitPoints = 3,
                    SurvivalTokens = 1,
                    WinnerTokens = 10
                }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Scoring
        void AddPoints()
        {
            foreach (CSPlayer helisurvivalplayer in CSPlayers)
                EventManager.AddTokens(helisurvivalplayer.player.userID, configData.Scoring.SurvivalTokens);
        }
        void FindWinner()
        {
            if (isEnding) return;
            List<BasePlayer> winners = new List<BasePlayer>();
            int score = 0;
            foreach (var csPlayer in CSPlayers)
            {
                if (csPlayer.points > score)
                {
                    winners.Clear();
                    winners.Add(csPlayer.player);
                    score = csPlayer.points;
                }
                else if (csPlayer.points == score)
                    winners.Add(csPlayer.player);
            }
            Winner(winners);
        }
        void Winner(List<BasePlayer> winners)
        {
            isEnding = true;
            string winnerNames = "";
            for (int i = 0; i < winners.Count; i++)
            {
                EventManager.AddTokens(winners[i].userID, configData.Scoring.WinnerTokens, true);
                winnerNames += winners[i].displayName;
                if (winners.Count > i + 1)
                    winnerNames += ", ";
            }
            EventManager.BroadcastToChat(string.Format(MSG("eventWin"), winnerNames));
            EventManager.EndEvent();
        }
        #endregion
    }
}