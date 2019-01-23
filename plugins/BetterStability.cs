// #define DEBUG

using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using System.Text;

namespace Oxide.Plugins
{
    [Info("BetterStability", "playrust.io/ dcode", "1.0.6", ResourceId = 862)]
    public class BetterStability : RustPlugin
    {
        // Singleton instance
        public static BetterStability Instance { get; private set; }

        // Update queues
        private List<BuildingBlock> stabilityQueueDelayed = new List<BuildingBlock>();
        private List<BuildingBlock> stabilityQueue = new List<BuildingBlock>();

        // Stats
        private Stats currentStats = new Stats();
        private Stats lastStats = new Stats();
        private static int statsInterval = 60 * 1000;
        private DateTime nextStatsTime = DateTime.MinValue;

        // Messages / Translations
        private string[] buildingFailedMessages = new string[] {
            "Man, that looks unstable as shit. Here's your refund!",
            "Even Bob couldn't build that. Refunded!",
            "That wouldn't be stable even with Lego. Have a refund!",
            "That would require a shitload of Duck Tape®. Apparently, you have none.",
            "Well, that looks interesting. Isn't stable, though. Here's your refund!"
        };
        private List<string> texts = new List<string>() {
            "Buildings are either 100% or 0% stable.",
            "All building parts must be supported by pillars."
        };
        private Dictionary<string, string> messages = new Dictionary<string, string>();
        private System.Random rng = new System.Random();

        // Translates a string
        private string _(string text, Dictionary<string, string> replacements = null) {
            if (messages.ContainsKey(text) && messages[text] != null)
                text = messages[text];
            if (replacements != null)
                foreach (var replacement in replacements)
                    text = text.Replace("%" + replacement.Key + "%", replacement.Value);
            return text;
        }

        // Logs a message to console
        public void Log(string message) {
            Puts("{0}: {1}", Title, message);
        }

        // Logs an error to console
        public void Error(string message, Exception ex = null) {
            if (ex == null)
                PrintError("{0}: {1}", Title, message);
            else
                PrintError("{0}: {1}: {2}\n{3}", Title, message, ex.Message, ex.StackTrace);
        }

        protected override void LoadDefaultConfig() {
            var messages = new Dictionary<string, object>();
            foreach (var msg in buildingFailedMessages)
                texts.Add(msg);
            foreach (var text in texts) {
                if (messages.ContainsKey(text))
                    Puts("{0}: {1}", Title, "Duplicate translation string: " + text);
                else
                    messages.Add(text, text);
            }
            Config["messages"] = messages;
        }

        #region Hooks

        [HookMethod("Init")]
        private void Init() {
            Instance = this;
            if (ConVar.Server.stability) {
                ConVar.Server.stability = false;
                Log("Default stability has been disabled");
            }
        }

        [HookMethod("OnServerInitialized")]
        private void OnServerInitialized() {
            LoadConfig();
            var customMessages = (Dictionary<string,object>)Config["messages"];
            if (customMessages != null) {
                foreach (var pair in customMessages)
                    messages[pair.Key] = Convert.ToString(pair.Value);
                Log("Loaded " + customMessages.Count + " translation strings");
            }

            // Initialize helpers
            BuildingBlockHelpers.Initialize();

            // Force an update on all blocks when first started
            var fs = Interface.GetMod().DataFileSystem;
            var data = fs.GetDatafile("BetterStability");
            bool firstStart = false;
            if (data["firststart"] == null) {
                Log("Starting the first time, forcing update on ALL blocks");
                firstStart = true;
                data["firststart"] = false;
                fs.SaveDatafile("BetterStability");
            }

            // Schedule stability updates for all blocks that support anything
            var allBlocks = UnityEngine.Object.FindObjectsOfType<BuildingBlock>();
            int n = 0;
            foreach (var block in allBlocks) {
                if (firstStart || (!BuildingBlockHelpers.IsFoundation(block) && BuildingBlockHelpers.IsSupportForAnything(block))) {
                    EnqueueUpdate(block);
                    ++n;
                }
            }
            Log("Queued " + n + " blocks for stability updates");
        }

        [HookMethod("OnEntityBuilt")]
        private void OnEntityBuilt(Planner planner, GameObject obj) {
            if (obj == null)
                return;
            var block = obj.GetComponent<BuildingBlock>();
            if (!BuildingBlockHelpers.IsValidBlock(block))
                return;
#if DEBUG
            Log("Placing block " + block.blockDefinition.hierachyName);
#endif
            ++currentStats.blocksBuilt;
            try {
                var defaultGrade = block.blockDefinition.defaultGrade;
                if (!UpdateStability(block, false)) {
                    ++currentStats.blocksFailedBuilding;
                    // If this isn't stable, refund.
                    var player = planner.ownerPlayer;
                    foreach (var cost in defaultGrade.costToBuild) {
                        var item = ItemManager.CreateByItemID(cost.itemid, (int)cost.amount, false);
                        player.GiveItem(item, BaseEntity.GiveItemReason.Generic);
                    }
                    player.ChatMessage(_(buildingFailedMessages[rng.Next(0, buildingFailedMessages.Length)]));
                    return;
                }
            } catch (Exception ex) {
                Error("OnEntityBuilt failed", ex);
            }
        }

        // [HookMethod("OnBuildingBlockDoRotation")]
        // private void OnBuildingBlockRotate(BuildingBlock block, object whatev)

        [HookMethod("OnBuildingBlockDemolish")]
        private void OnBuildingBlockDemolish(BuildingBlock block, BasePlayer player) {
            if (!BuildingBlockHelpers.IsValidBlock(block))
                return;
            OnDemolish(block);
        }

        [HookMethod("OnEntityDeath")]
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info) {
            var block = entity as BuildingBlock;
            if (!BuildingBlockHelpers.IsValidBlock(block))
                return;
            OnDemolish(block);
        }

        [HookMethod("OnTick")]
        private void OnTick() {
            var now = DateTime.Now;
            if (nextStatsTime < now) {
                nextStatsTime = now.AddMilliseconds(statsInterval);
                lastStats = currentStats;
                currentStats = new Stats();
            }
            // Never use more than half the available tick time
            // but always process at least one queued block.
            var maxTime = (1000 / ConVar.Server.tickrate) / 2;
            var n = 0;
            while (stabilityQueue.Count > 0 && (n == 0 || (DateTime.Now - now).TotalMilliseconds < maxTime)) {
                var block = stabilityQueue[0];
                stabilityQueue.RemoveAt(0);
                if (!BuildingBlockHelpers.IsValidBlock(block))
                    continue;
                UpdateStability(block);
                ++n;
            }
            // Always delay queued updates until the next tick.
            // This also gives us a nice bottom up effect.
            while (stabilityQueueDelayed.Count > 0) {
                stabilityQueue.Add(stabilityQueueDelayed[0]);
                stabilityQueueDelayed.RemoveAt(0);
            }
        }

        [HookMethod("BuildServerTags")]
        private void BuildServerTags(IList<string> taglist) {
            taglist.Add("betterstability");
        }

        [HookMethod("SendHelpText")]
        private void SendHelpText(BasePlayer player) {
            var sb = new StringBuilder()
               .Append("<size=18>BetterStability</size> by <color=#ce422b>http://playrust.io</color>\n")
               .Append("  ").Append(_("Buildings are either 100% or 0% stable.")).Append("\n")
               .Append("  ").Append(_("All building parts must be supported by pillars."));
            player.ChatMessage(sb.ToString());
        }

        #endregion

        #region Interface

        // Updates supported blocks once a block has been demolished
        private void OnDemolish(BuildingBlock block) {
            ++currentStats.blocksDemolished;
            List<BuildingBlock> supports;
            List<BuildingBlock> supported;
            BuildingBlockHelpers.GetAdjacentBlocks(block, out supports, out supported);
#if DEBUG
            Log("OnDemolish called for " + BuildingBlockHelpers.Name(block) + " (support for " + supported.Count + " blocks)");
#endif
            foreach (var supportedBlock in supported)
                EnqueueUpdate(supportedBlock); // Must be queued as this block is still alive
            var deployables = BuildingBlockHelpers.GetOrphanedDeployables(block);
            foreach (var entity in deployables)
                DemolishDeployable(entity);
        }

        // Enqueues a stability update for the next tick
        private void EnqueueUpdate(BuildingBlock block) {
            ++currentStats.blocksEnqueued;
            if (!stabilityQueueDelayed.Contains(block)) {
                ++currentStats.blocksEnqueuedUnique;
#if DEBUG
                Log("Enqueued " + BuildingBlockHelpers.Name(block));
#endif
                stabilityQueueDelayed.Add(block);
            }
        }

        // Demolishes a block
        private void DemolishBlock(BuildingBlock block) {
            block.Kill(BaseNetworkable.DestroyMode.Gib);
            ++currentStats.blocksDestroyed;
        }

        // Demolishes a deployable and drops its contents
        private void DemolishDeployable(BaseEntity entity) {
            if (entity is BaseCombatEntity) {
                (entity as BaseCombatEntity).DieInstantly();
            } else {
                entity.Kill(BaseNetworkable.DestroyMode.Gib);
            }
            ++currentStats.deployablesDestroyed;
            /* if (entity is StorageContainer) {
                (entity as StorageContainer).OnKilled(null); // Drops
                // var container = entity as StorageContainer;
                // DropUtil.DropItems(container.inventory, container.transform.position, container.dropChance);
            } */
        }

        // Updates the stability of a block and returns false if the block has just been destroyed
        private bool UpdateStability(BuildingBlock block, bool propagate = true) {
            ++currentStats.blocksUpdated;
            if (block.isDestroyed) {
#if DEBUG
                Log("Skipped " + BuildingBlockHelpers.Name(block) + ": Already destroyed");
#endif
                return true;
            }
            // Log("Updating stability on " + block.Name());
            // Exclude foundations from stability updates.
            if (BuildingBlockHelpers.IsFoundation(block)) {
#if DEBUG
                Log("Skipped " + BuildingBlockHelpers.Name(block) + ": Is foundation");
#endif
                return true;
            }
            List<BuildingBlock> supports;
            List<BuildingBlock> supported;
            BuildingBlockHelpers.GetAdjacentBlocks(block, out supports, out supported);
            if (supports.Count > 0) {
#if DEBUG
                Log("Skipped " + BuildingBlockHelpers.Name(block) + ": Still has " + supports.Count + " supports");
#endif
                return true;
            }
            // If this block has no more supports, destroy it.
#if DEBUG
            Log(BuildingBlockHelpers.Name(block) + " has no (more) supports, killing (supported " + supported.Count + ")" + (propagate ? " - propagating" : " - not propagating"));
#endif
            DemolishBlock(block);
            var deployables = BuildingBlockHelpers.GetOrphanedDeployables(block);
            foreach (var deployable in deployables)
                if (!deployable.isDestroyed)
                    DemolishDeployable(deployable);
            if (propagate)
                foreach (var supportedBlock in supported)
                    EnqueueUpdate(supportedBlock);
            return false;
        }

        #endregion

        #region Console Commands

        [ConsoleCommand("stability.status")]
        void cmdConsoleStatus(ConsoleSystem.Arg arg) {
            if (arg.connection != null && arg.connection.authLevel < 2) {
                return;
            }
            var sb = new StringBuilder();
            sb.Append("Current queue: ").Append(stabilityQueueDelayed.Count).Append(" -> ").Append(stabilityQueue.Count).Append("\n")
              .Append("\n-- Current stats ----------\n")
              .Append(currentStats.ToString())
              .Append("\n-- Last stats ----------\n")
              .Append(lastStats.ToString());
            SendReply(arg, sb.ToString());
        }

        [ConsoleCommand("stability.updateall")]
        void cmdConsoleUpdateAll(ConsoleSystem.Arg arg) {
            if (arg.connection != null && arg.connection.authLevel < 2)
                return;
            var allBlocks = UnityEngine.Object.FindObjectsOfType<BuildingBlock>();
            foreach (var block in allBlocks) {
                if (!BuildingBlockHelpers.IsValidBlock(block))
                    continue;
                UpdateStability(block, true);
            }
            SendReply(arg, "Updating stability on ALL " + allBlocks.Length + " blocks in the background now, this will take a while.");
        }

        #endregion

        public static class BuildingBlockHelpers
        {
            #region Methods

            // Tests if the block is valid
            public static bool IsValidBlock(BuildingBlock block) {
                return block != null && block.blockDefinition != null;
            }

            // Tests if the block is a foundation
            public static bool IsFoundation(BuildingBlock self) {
                return IsValidBlock(self) && foundationList.Contains(self.blockDefinition.hierachyName);
            }

            // Tests if the block is a wall
            public static bool IsWall(BuildingBlock self) {
                return IsValidBlock(self) && wallsList.Contains(self.blockDefinition.hierachyName);
            }

            // Tests if the block is a pillar
            public static bool IsPillar(BuildingBlock self) {
                return IsValidBlock(self) && self.blockDefinition.hierachyName == "pillar";
            }

            // Tests if the block is a support for anything
            public static bool IsSupportForAnything(BuildingBlock self) {
                return IsValidBlock(self) && supportNames.Contains(self.blockDefinition.hierachyName);
            }

            // Tests if the block is a generally supported by the specified
            public static bool IsSupportedBy(BuildingBlock self, BuildingBlock by) {
                if (!IsValidBlock(self) || !IsValidBlock(by) || self.isDestroyed || by.isDestroyed)
                    return false;
                string[] supportFor;
                if (!supportMap.TryGetValue(self.blockDefinition.hierachyName, out supportFor))
                    return false;
                return supportFor.Contains(by.blockDefinition.hierachyName);
            }

            // Gets adjacent supports and supported blocks
            public static void GetAdjacentBlocks(BuildingBlock self, out List<BuildingBlock> supports, out List<BuildingBlock> supported) {
                supports = new List<BuildingBlock>();
                supported = new List<BuildingBlock>();
                if (!IsValidBlock(self))
                    return;
                List<StabilityPinPoint> pinPoints = GetPrefabPinPoints(self);
                // Find all blocks supporting this block
                foreach (var pinPoint in pinPoints) {
                    var worldPosition = LocalToWorld(self, pinPoint.worldPosition);
                    Collider[] colliders = Physics.OverlapSphere(worldPosition, 0.1f, constructionLayerMask);
                    foreach (var collider in colliders) {
                        BuildingBlock other = collider.gameObject.ToBaseEntity() as BuildingBlock;
                        if (!IsValidBlock(other) || other.isDestroyed || other == self) // Bogus, already destroyed or self
                            continue;
                        if (IsSupportedBy(self, other))
                            supports.Add(other);
                    }
                }
                // Find all blocks supported by this block
                if (!IsSupportForAnything(self))
                    return;
                List<Socket_Base> sockets = GetPrefabSockets(self);
                foreach (var socket in sockets) {
                    var worldPosition = LocalToWorld(self, socket.worldPosition);
                    Collider[] colliders = Physics.OverlapSphere(worldPosition, 0.2f, constructionLayerMask);
                    foreach (var collider in colliders) {
                        BuildingBlock other = collider.gameObject.ToBaseEntity() as BuildingBlock;
                        if (!IsValidBlock(other) || other.isDestroyed || other == self) // Bogus, already destroyed or self
                            continue;
                        if (IsSupportedBy(other, self))
                            supported.Add(other);
                    }
                }
            }

            public static List<BaseEntity> GetOrphanedDeployables(BuildingBlock self) {
                List<BaseEntity> deployables = new List<BaseEntity>();
                if (!IsValidBlock(self))
                    return deployables;
                Collider[] colliders = Physics.OverlapSphere(self.transform.position, 4f, deployedLayerMask);
                foreach (var collider in colliders) {
                    var entity = collider.gameObject.ToBaseEntity();
                    if (entity == null)
                        continue;
                    var subColliders = Physics.OverlapSphere(entity.transform.position, 0.1f, placementLayerMask);
                    var isOrphaned = true;
                    foreach (var subCollider in subColliders) {
                        if (subCollider is TerrainCollider) {
                            isOrphaned = false;
                            break;
                        }
                        var block = subCollider.gameObject.ToBaseEntity() as BuildingBlock;
                        if (block != null && block != self) {
                            isOrphaned = false;
                            break;
                        }
                    }
                    if (isOrphaned)
                        deployables.Add(entity);
                }
                return deployables;
            }

            public static string Name(BuildingBlock self) {
                if (!IsValidBlock(self))
                    return "invalid#" + self.GetInstanceID();
                return self.blockDefinition.hierachyName + "#" + self.GetInstanceID() + "[" + GetPrefabPinPoints(self).Count + "/" + GetPrefabSockets(self).Count + "]";
            }

            #endregion

            #region Definitions

            // Blocks considered a foundation, by name
            private static string[] foundationList = new string[] {
                "foundation",
                "foundation.steps",
                "foundation.triangle"
            };

            // Blocks considered a wall, by name
            private static string[] wallsList = new string[] {
                "wall",
                "wall.doorway",
                "wall.window"
            };

            // A map specifying which blocks are considered a support
            private static Dictionary<string, string[]> supportMap = new Dictionary<string, string[]>() {
                { "block.halfheight", new string[] {
                    "block.halfheight",
                    "floor",
                    "floor.triangle",
                    "foundation",
                    "foundation.triangle"
                }},
                { "block.halfheight.slanted", new string[] {
                    "block.halfheight",
                    "floor",
                    "foundation"
                }},
                { "block.stair.ushape", new string[] {
                    "block.halfheight",
                    "floor",
                    "foundation"
                }},
                { "block.stair.lshape", new string[] {
                    "block.halfheight",
                    "floor",
                    "foundation"
                }},
                { "floor", new string[] {
                    "foundation",
                    "foundation.triangle",
                    "pillar"
                }},
                { "floor.triangle", new string[] {
                    "foundation",
                    "foundation.triangle",
                    "pillar"
                }},
                { "pillar", new string[] {
                    "foundation",
                    "foundation.triangle",
                    "pillar"
                }},
                { "roof", new string[] {
                    "pillar"
                }},
                { "wall", new string[] {
                    "pillar"
                }},
                { "wall.doorway", new string[] {
                    "pillar"
                }},
                { "door.hinged", new string[] {
                    "wall.doorway"
                }},
                { "wall.low", new string[] {
                    "floor",
                    "floor.triangle",
                    "foundation",
                    "foundation.triangle"
                }},
                { "wall.window", new string[] {
                    "pillar"
                }},
                { "wall.window.bars", new string[] {
                    "wall.window"
                }}
            };

            private static string[] supportNames;

            // Layer masks used
            private static LayerMask constructionLayerMask;
            private static LayerMask deployedLayerMask;
            private static LayerMask placementLayerMask;

            // Cached prefab pinpoints
            private static Dictionary<uint, List<StabilityPinPoint>> prefabPinPoints = new Dictionary<uint, List<StabilityPinPoint>>();

            // Gets the prefab pinpoints of a block
            public static List<StabilityPinPoint> GetPrefabPinPoints(BuildingBlock self) {
                List<StabilityPinPoint> pinPoints;
                if (!prefabPinPoints.TryGetValue(self.prefabID, out pinPoints))
                    prefabPinPoints.Add(self.prefabID, pinPoints = PrefabAttribute.server.FindAll<StabilityPinPoint>(self.prefabID).ToList());
                return pinPoints;
            }

            // Cached prefab sockets
            private static Dictionary<uint, List<Socket_Base>> prefabSockets = new Dictionary<uint, List<Socket_Base>>();

            // Gets the prefab sockets of a block
            public static List<Socket_Base> GetPrefabSockets(BuildingBlock self) {
                List<Socket_Base> sockets;
                if (!prefabSockets.TryGetValue(self.prefabID, out sockets))
                    prefabSockets.Add(self.prefabID, sockets = PrefabAttribute.server.FindAll<Socket_Base>(self.prefabID).ToList());
                return sockets;
            }

            // Translates a local prefab coordinate to world coordinates
            private static Vector3 LocalToWorld(BuildingBlock block, Vector3 local) {
                return block.transform.localToWorldMatrix.MultiplyPoint3x4(local);
            }

            // Logs a message to console
            private static void Log(string message) {
                BetterStability.Instance.Log(message);
            }

            #endregion

            #region Initialization

            public static void Initialize() {
                constructionLayerMask = LayerMask.GetMask("Construction", "Construction Trigger");
                deployedLayerMask = LayerMask.GetMask("Deployed");
                placementLayerMask = LayerMask.GetMask("Construction", "Terrain");
                var supports = new List<string>();
                foreach (var pair in supportMap) {
                    foreach (var name in pair.Value) {
                        if (!supports.Contains(name))
                            supports.Add(name);
                    }
                }
                supportNames = supports.ToArray();
            }

            #endregion
        }

        public class Stats
        {
            public int blocksBuilt = 0;
            public int blocksFailedBuilding = 0;
            public int blocksDemolished = 0;
            public int blocksEnqueued = 0;
            public int blocksEnqueuedUnique = 0;
            public int blocksUpdated = 0;
            public int blocksDestroyed = 0;
            public int deployablesDestroyed = 0;

            public override string ToString() {
                var sb = new StringBuilder();
                sb.Append("Blocks built: ").Append(blocksBuilt).Append("\n")
                  .Append("Blocks failed building: ").Append(blocksFailedBuilding).Append("\n")
                  .Append("Blocks demolished: ").Append(blocksDemolished).Append("\n")
                  .Append("Blocks enqueued: ").Append(blocksEnqueued).Append("\n")
                  .Append("Unique blocks enqueued: ").Append(blocksEnqueued).Append("\n")
                  .Append("Blocks updated: ").Append(blocksUpdated).Append("\n")
                  .Append("Blocks destroyed: ").Append(blocksDestroyed).Append("\n")
                  .Append("Deployables destroyed: ").Append(deployablesDestroyed).Append("\n");
                return sb.ToString();
            }
        }
    }
}
