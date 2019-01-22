#define DEBUG

using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Linq;
using System.Net;
#if REIGNOFKINGS
using CodeHatch.Blocks.Networking.Events;
using CodeHatch.Common;
using CodeHatch.Engine.Core.Commands;
using CodeHatch.Engine.Core.Networking;
using CodeHatch.Engine.Networking;
using CodeHatch.Networking.Events;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.Networking.Events.Players;
using CodeHatch.Thrones.AncientThrone;
#endif
#if RUST
using UnityEngine;
using static ItemContainer;
#endif

namespace Oxide.Plugins
{
    [Info("Dev Test", "Oxide Community", "0.1.0")]
    [Description("Tests all of the available Oxide hooks and provides useful helpers")]
    public class DevTest : CovalencePlugin
    {
        #region Hook Verification

        private Dictionary<string, bool> hooksRemaining = new Dictionary<string, bool>();
        private int hookCount;
        private int hooksVerified;

        public void HookCalled(string hook)
        {
            if (!hooksRemaining.ContainsKey(hook)) return;

            hookCount--;
            hooksVerified++;
            LogWarning($"{hook} is working");
            hooksRemaining.Remove(hook);
            LogWarning(hookCount == 0 ? "All hooks verified!" : $"{hooksVerified} hooks verified, {hookCount} hooks remaining");
        }

        #endregion Hook Verification

        #region Plugin Hooks (universal)

        private void Init()
        {
            hookCount = Hooks.Count;
            hooksRemaining = Hooks.Keys.ToDictionary(k => k, k => true);
            LogWarning("{0} hook(s) to test!", hookCount);

            HookCalled("Init");
        }

        private new void LoadDefaultConfig()
        {
            HookCalled("LoadDefaultConfig");
        }

        private new void LoadDefaultMessages()
        {
            HookCalled("LoadDefaultMessages");
        }

        private void Loaded() => HookCalled("Loaded");

        private void Unloaded() => HookCalled("Unloaded");

        private void OnFrame() => HookCalled("OnFrame");

        private void OnPluginLoaded(Plugin plugin)
        {
            LogWarning($"Plugin '{plugin.Name}' has been loaded");

            HookCalled("OnPluginLoaded");
        }

        private void OnPluginUnloaded(Plugin plugin)
        {
            LogWarning($"Plugin '{plugin.Name}' has been unloaded");

            HookCalled("OnPluginUnloaded");
        }

        #endregion Plugin Hooks (universal)

        #region Server Hooks (universal)

        private void OnServerInitialized()
        {
            LogWarning($"{server.Name} at {server.Address}:{server.Port}");
            LogWarning($"Oxide {OxideMod.Version} for {covalence.Game} {server.Version}");
            LogWarning($"Server language detected as: {server.Language.TwoLetterISOLanguageName}");
            LogWarning($"World time is: {server.Time.ToString("h:mm tt").ToLower()}");
            LogWarning($"World date is: {server.Time}");

            HookCalled("OnServerInitialized");
        }

        private void OnServerSave()
        {
            server.Broadcast("Server is saving, hang tight!");
            LogWarning("Server is saving...");

            HookCalled("OnServerSave");
        }

        private void OnServerShutdown()
        {
            server.Broadcast("Server is going offline, stash yo' loot!");

            HookCalled("OnServerShutdown");
        }

        #endregion Server Hooks (universal)

        #region Player Hooks (universal)

        private object CanUserLogin(string name, string id, string ip)
        {
            LogWarning($"{name} ({id}) at {ip} is attempting to login");

            HookCalled("CanUserLogin");
            return null;
        }

        private void OnUserApproved(string name, string id, string ip)
        {
            LogWarning($"{name} ({id}) at {ip} has been approved");

            HookCalled("OnUserApproved");
        }

        private object OnUserChat(IPlayer player, string message)
        {
            LogWarning($"{player.Name} said: {message}");

            HookCalled("OnUserChat");
            return null;
        }

        private object OnUserCommand(IPlayer player, string command, string[] args)
        {
            LogWarning($"{player.Name} ({player.Id}) ran command: {command} {string.Join(" ", args)}");

            HookCalled("OnUserCommand");
            return null;
        }

        private void OnUserConnected(IPlayer player)
        {
            LogWarning($"{player.Name} ({player.Id}) connected from {player.Address}");
            if (player.IsAdmin) LogWarning($"{player.Name} is admin");
            LogWarning($"{player.Name} is {(player.IsBanned ? "banned" : "not banned")}");
            LogWarning($"{player.Name} ({player.Id}) language detected as {player.Language}");

            server.Broadcast($"Welcome {player.Name} to {server.Name}!");
            foreach (var target in players.Connected) target.Message($"Look out... {player.Name} is coming to get you!");

            HookCalled("OnUserConnected");
        }

        private void OnUserDisconnected(IPlayer player, string reason)
        {
            LogWarning($"{player.Name} ({player.Id}) disconnected for: {reason ?? "Unknown"}");
            server.Broadcast($"{player.Name} has abandoned us... free loot!");

            HookCalled("OnUserDisconnected");
        }

        private void OnUserRespawn(IPlayer player)
        {
            LogWarning($"{player.Name} is respawning now");

            HookCalled("OnUserRespawn");
        }

        private void OnUserRespawned(IPlayer player)
        {
            LogWarning($"{player.Name} respawned at {player.Position()}");

            HookCalled("OnUserRespawned");
        }

        private void OnUserSpawn(IPlayer player)
        {
            LogWarning($"{player.Name} is spawning now");
            HookCalled("OnUserSpawn");
        }

        private void OnUserSpawned(IPlayer player)
        {
            GenericPosition position = new GenericPosition((float)40.75811, (float)11.20799, (float)58.23539);
            LogWarning($"{player.Name} spawned at {player.Position()}");
            LogWarning($"Teleporting {player.Name} to {position}");
            player.Teleport(position);

            HookCalled("OnUserSpawned");
        }

        private void OnUserBanned(string name, string id, string address, string reason)
        {
            LogWarning($"Player {name} ({id}) was banned: {reason}");

            HookCalled("OnUserBanned");
        }

        private void OnUserKicked(IPlayer player, string reason)
        {
            LogWarning($"Player {player.Name} ({player.Id}) was kicked");

            HookCalled("OnUserKicked");
        }

        private void OnUserUnbanned(string name, string id, string ip)
        {
            LogWarning($"Player {name} ({id}) was unbanned");

            HookCalled("OnUserUnbanned");
        }

        #endregion Player Hooks (universal)

        #region Permission Hooks (universal)

        private void OnGroupPermissionGranted(string name, string perm)
        {
            LogWarning($"Group '{name}' granted permission: {perm}");

            HookCalled("OnGroupPermissionGranted");
        }

        private void OnGroupPermissionRevoked(string name, string perm)
        {
            LogWarning($"Group '{name}' revoked permission: {perm}");

            HookCalled("OnGroupPermissionRevoked");
        }

        private void OnUserPermissionGranted(string id, string perm)
        {
            LogWarning($"Player '{id}' granted permission: {perm}");

            HookCalled("OnUserPermissionGranted");
        }

        private void OnUserPermissionRevoked(string id, string perm)
        {
            LogWarning($"Player '{id}' revoked permission: {perm}");

            HookCalled("OnUserPermissionRevoked");
        }

        private void OnUserGroupAdded(string id, string name)
        {
            LogWarning($"Player '{id}' added to group: {name}");

            HookCalled("OnUserGroupAdded");
        }

        private void OnUserGroupRemoved(string id, string name)
        {
            LogWarning($"Player '{id}' removed from group: {name}");

            HookCalled("OnUserGroupRemoved");
        }

        #endregion Permission Hooks (universal)

        #region Commands (universal)

        [Command("dev.pos")]
        private void PositionCommand(IPlayer player, string command, string[] args)
        {
            LogWarning($"Position for {player.Name} is {player.Position()}");
            player.Reply($"Position for {player.Name} is {player.Position()}");
        }

        [Command("dev.tpto")]
        private void TeleportToCommand(IPlayer player, string command, string[] args)
        {
            if (args.Length != 3) return;

            player.Reply($"Teleporting {player.Name} to {args[0]}");
            string[] position = args[0].Split(',');
            player.Teleport(float.Parse(position[0].Trim()), float.Parse(position[1].Trim()), float.Parse(position[2].Trim()));
        }

        #endregion Commands (universal)

#if HURTWORLD

        #region Entity Hooks

        private void OnEntitySpawned(NetworkViewData data)
        {
            HookCalled("OnEntitySpawned");
        }

        private void OnEntityTakeDamage(AIEntity entity, EntityEffectSourceData source)
        {
            HookCalled("OnEntityTakeDamage");
        }

        #endregion Entity Hooks

        #region Player Hooks

        /*private bool CanCraft(PlayerSession session, CrafterServer crafter)
        {
            HookCalled("CanCraft");
            return true;
        }*/

        private void OnChatCommand(PlayerSession session, string command)
        {
            HookCalled("OnChatCommand");
        }

        private void OnPlayerConnected(PlayerSession session)
        {
            HookCalled("OnPlayerConnected");
        }

        private object OnPlayerChat(PlayerSession session, string message)
        {
            HookCalled("OnPlayerChat");

            return null;
        }

        private void OnPlayerDisconnected(PlayerSession session)
        {
            LogWarning($"{session.Identity.Name} ({session.SteamId}) disconnected");

            HookCalled("OnPlayerDisconnected");
        }

        private object OnPlayerDeath(PlayerSession session, EntityEffectSourceData source)
        {
            HookCalled("OnPlayerDeath");
            return null;
        }

        private bool CanUseMachine(PlayerSession session, BaseMachine<DrillMachine> machine)
        {
            HookCalled("CanUseMachine");
            return true;
        }

        private void OnPlayerRespawn(PlayerSession session)
        {
            HookCalled("OnPlayerRespawn");
        }

        private void OnPlayerSpawn(PlayerSession session)
        {
            HookCalled("OnPlayerSpawn");
        }

        private void OnPlayerApproved(PlayerSession session)
        {
            HookCalled("OnPlayerApproved");
        }

        private void OnPlayerInput(PlayerSession session, InputControls input)
        {
            HookCalled("OnPlayerInput");
        }

        private void OnPlayerTakeDamage(PlayerSession session, EntityEffectSourceData source)
        {
            HookCalled("OnPlayerTakeDamage");
        }

        #endregion Player Hooks

        #region Structure Hooks

        private bool CanUseDoubleDoor(PlayerSession session, DoubleDoorServer door)
        {
            HookCalled("CanUseDoubleDoor");
            return true;
        }

        private bool CanUseGarageDoor(PlayerSession session, GarageDoorServer door)
        {
            HookCalled("CanUseGarageDoor");
            return true;
        }

        private bool CanUsePillboxDoor(PlayerSession session, DoorPillboxServer door)
        {
            HookCalled("CanUsePillboxDoor");

            return true;
        }

        private bool CanUseSingleDoor(PlayerSession session, DoorSingleServer door)
        {
            HookCalled("CanUseSingleDoor");
            return true;
        }

        private void OnDoubleDoorUsed(DoubleDoorServer door, PlayerSession session)
        {
            HookCalled("OnDoubleDoorUsed");
        }

        private void OnGarageDoorUsed(GarageDoorServer door, PlayerSession session)
        {
            HookCalled("OnGarageDoorUsed");
        }

        private void OnSingleDoorUsed(DoorSingleServer door, PlayerSession session)
        {
            HookCalled("OnSingleDoorUsed");
        }

        #endregion Structure Hooks

        #region Vehicle Hooks

        private object CanEnterVehicle(PlayerSession session, VehiclePassenger vehicle)
        {
            HookCalled("CanEnterVehicle");
            return null;
        }

        private bool CanExitVehicle(PlayerSession session, VehiclePassenger vehicle)
        {
            HookCalled("CanExitVehicle");
            return true;
        }

        private void OnEnterVehicle(PlayerSession session, VehiclePassenger vehicle)
        {
            HookCalled("OnEnterVehicle");
        }

        private void OnExitVehicle(PlayerSession session, VehiclePassenger vehicle)
        {
            HookCalled("OnExitVehicle");
        }

        #endregion Vehicle Hooks

#endif

#if REIGNOFKINGS

        #region Entity Hooks

        private void OnEntityHealthChange(EntityDamageEvent evt)
        {
            HookCalled("OnEntityHealthChange");
        }

        private void OnEntityDeath(EntityDeathEvent evt)
        {
            HookCalled("OnEntityDeath");
        }

        #endregion Entity Hooks

        #region Player Hooks

        private void OnUserApprove(ConnectionLoginData data)
        {
            HookCalled("OnUserApprove");
        }

        private void OnChatCommand(Player player, string command, string[] args)
        {
            HookCalled("OnChatCommand");
        }

        private void OnPlayerConnected(Player player)
        {
            HookCalled("OnPlayerConnected");
        }

        private void OnPlayerDisconnected(Player player)
        {
            HookCalled("OnPlayerDisconnected");
        }

        private void OnPlayerSpawn(PlayerFirstSpawnEvent evt)
        {
            HookCalled("OnPlayerSpawn");
        }

        private void OnPlayerSpawned(PlayerPreSpawnCompleteEvent evt)
        {
            HookCalled("OnPlayerSpawned");
        }

        private void OnPlayerRespawn(PlayerRespawnEvent evt)
        {
            HookCalled("OnPlayerRespawn");
        }

        private void OnPlayerChat(PlayerMessageEvent evt)
        {
            HookCalled("OnPlayerChat");
        }

        private void OnPlayerCapture(PlayerCaptureEvent evt)
        {
            HookCalled("OnPlayerCapture");
        }

        private void OnPlayerRelease(PlayerEscapeEvent evt)
        {
            HookCalled("OnPlayerRelease");
        }

        #endregion Player Hooks

        #region Structure Hooks

        private void OnCubePlacement(CubePlaceEvent evt)
        {
            HookCalled("OnCubePlacement");
        }

        private void OnCubeTakeDamage(CubeDamageEvent evt)
        {
            HookCalled("OnCubeTakeDamage");
        }

        private void OnCubeDestroyed(CubeDestroyEvent evt)
        {
            HookCalled("OnCubeDestroyed");
        }

        #endregion Structure Hooks

        #region Throne Hooks

        private void OnThroneCapture(AncientThroneCaptureEvent evt)
        {
            HookCalled("OnThroneCapture");
        }

        private void OnThroneCaptured(AncientThroneCaptureEvent evt)
        {
            HookCalled("OnThroneCaptured");
        }

        private void OnThroneReleased(AncientThroneReleaseEvent evt)
        {
            HookCalled("OnThroneReleased");
        }

        private void OnThroneRename(AncientThroneRenameEvent evt)
        {
            HookCalled("OnThroneRename");
        }

        private void OnThroneTax(AncientThroneTaxEvent evt)
        {
            HookCalled("OnThroneTax");
        }

        #endregion Throne Hooks

#endif

#if RUST

        #region Server Hooks

        private object OnMessagePlayer(string message, BasePlayer player)
        {
            HookCalled("OnMessagePlayer");
            return null;
        }

        private void OnNewSave(string name) => HookCalled("OnNewSave");

        private void OnRconCommand(string ip, string command, string[] args)
        {
            HookCalled("OnRconCommand");
        }

        private object OnRconConnection(IPEndPoint ip)
        {
            LogWarning($"{ip.Address} connected via RCON on port {ip.Port}");

            HookCalled("OnRconConnection");
            return null;
        }

        private void OnSaveLoad(Dictionary<BaseEntity, ProtoBuf.Entity> dictionary)
        {
            LogWarning($"{dictionary.Count} entities loaded from save");

            HookCalled("OnSaveLoad");
        }

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            LogWarning($"{(player == null ? "Server" : $"{player.displayName} ({player.UserIDString})")} ran command: {arg.cmd.FullName} {arg.FullString}");
            HookCalled("OnServerCommand");
            return null;
        }

        private object OnServerMessage(string message, string name, string color, ulong id)
        {
            HookCalled("OnServerMessage");
            return null;
        }

        private void OnTerrainInitialized() => HookCalled("OnTerrainInitialized");

        private void OnTick() => HookCalled("OnTick");

        #endregion Server Hooks

        #region Player Hooks

        private bool CanAttack(BasePlayer player)
        {
            HookCalled("CanAttack");
            return true;
        }

        private bool CanBeTargeted(BaseCombatEntity player, MonoBehaviour behaviour)
        {
            HookCalled("CanBeTargeted");
            return true;
        }

        private bool CanBeWounded(BasePlayer player, HitInfo info)
        {
            HookCalled("CanBeWounded");
            return true;
        }

        private bool CanBypassQueue(Network.Connection connection)
        {
            LogWarning($"Can {connection.username} ({connection.userid}) bypass the queue? Yes.");

            HookCalled("CanBypassQueue");
            return true;
        }

        private bool CanClientLogin(Network.Connection connection)
        {
            HookCalled("CanClientLogin");
            return true;
        }

        private bool CanCraft(ItemCrafter itemCrafter, ItemBlueprint bp, int amount)
        {
            HookCalled("CanCraft");
            return true;
        }

        private bool CanEquipItem(PlayerInventory inventory, Item item)
        {
            var player = inventory.containerBelt.playerOwner;
            LogWarning($"Can {player.displayName} ({player.UserIDString}) equip {item.info.displayName.english}? Yes.");

            HookCalled("CanEquipItem");
            return true;
        }

        private bool CanLootPlayer(BasePlayer target, BasePlayer looter)
        {
            HookCalled("CanLootPlayer");
            return true;
        }

        private bool CanUseMailbox(BasePlayer player, Mailbox mailbox)
        {
            HookCalled("CanUseMailbox");
            return true;
        }

        private bool CanWearItem(PlayerInventory inventory, Item item)
        {
            var player = inventory.containerWear.playerOwner;
            LogWarning($"Can {player.displayName} ({player.UserIDString}) wear {item.info.displayName.english}? Yes.");

            HookCalled("CanWearItem");
            return true;
        }

        private void OnClientAuth(Network.Connection connection)
        {
            HookCalled("OnClientAuth");
        }

        private void OnFindSpawnPoint() => HookCalled("OnFindSpawnPoint");

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            HookCalled("OnLootEntity");
        }

        private void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            HookCalled("OnLootEntityEnd");
        }

        private void OnLootItem(BasePlayer player, Item item)
        {
            HookCalled("OnLootItem");
        }

        private void OnLootPlayer(BasePlayer player, BasePlayer target)
        {
            HookCalled("OnLootPlayer");
        }

        private object OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            HookCalled("OnPlayerAttack");
            return null;
        }

        private void OnPlayerBanned(string name, ulong id, string address, string reason)
        {
            HookCalled("OnPlayerBanned");
        }

        private object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null) return null;

            LogWarning($"{player.displayName} said: {arg.GetString(0)}");

            HookCalled("OnPlayerChat");
            return null;
        }

        private void OnPlayerConnected(Network.Message packet)
        {
            HookCalled("OnPlayerConnected");
        }

        private object OnPlayerDie(BasePlayer player, HitInfo info)
        {
            HookCalled("OnPlayerDie");
            return null;
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            HookCalled("OnPlayerDisconnected");
        }

        private object OnPlayerHealthChange(BasePlayer player, float oldValue, float newValue)
        {
            HookCalled("OnPlayerHealthChange");
            return null;
        }

        private void OnPlayerInit(BasePlayer player)
        {
            HookCalled("OnPlayerInit");
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            //LogWarning($"{player.displayName} sent input: {input.current}");

            HookCalled("OnPlayerInput");
        }

        private void OnPlayerKicked(BasePlayer player, string reason)
        {
            HookCalled("OnPlayerKicked");
        }

        private object OnPlayerLand(BasePlayer player, float num)
        {
            HookCalled("OnPlayerLand");
            return null;
        }

        private void OnPlayerLanded(BasePlayer player, float num)
        {
            HookCalled("OnPlayerLanded");
        }

        private void OnPlayerLootEnd(PlayerLoot inventory)
        {
            HookCalled("OnPlayerLootEnd");
        }

        private object OnPlayerRecover(BasePlayer player)
        {
            HookCalled("OnPlayerRecover");
            return null;
        }

        private object OnPlayerRespawn(BasePlayer player)
        {
            HookCalled("OnPlayerRespawn");
            return null;
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            HookCalled("OnPlayerRespawned");
        }

        private object OnPlayerSleep(BasePlayer player)
        {
            HookCalled("OnPlayerSleep");
            return null;
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            HookCalled("OnPlayerSleepEnded");
        }

        private object OnPlayerSpawn(BasePlayer player)
        {
            HookCalled("OnPlayerSpawn");
            return null;
        }

        private object OnPlayerSpectate(BasePlayer player, string spectateFilter)
        {
            HookCalled("OnPlayerSpectate");
            return null;
        }

        private object OnPlayerSpectateEnd(BasePlayer player, string spectateFilter)
        {
            HookCalled("OnPlayerSpectateEnd");
            return null;
        }

        private object OnPlayerTick(BasePlayer player, PlayerTick msg, bool wasPlayerStalled)
        {
            HookCalled("OnPlayerTick");
            return null;
        }

        private void OnPlayerUnbanned(string name, ulong id, string address)
        {
            HookCalled("OnPlayerUnbanned");
        }

        private object OnPlayerViolation(BasePlayer player, AntiHackType type, float amount)
        {
            HookCalled("OnPlayerViolation");
            return null;
        }

        private object OnPlayerWound(BasePlayer player)
        {
            HookCalled("OnPlayerWound");
            return null;
        }

        private object OnRunPlayerMetabolism(PlayerMetabolism metabolism, BaseCombatEntity entity, float delta)
        {
            var player = entity as BasePlayer;
            if (player == null) return null;

            //LogWarning($"{player.displayName} health: {player.health}, thirst: {metabolism.hydration.value}, calories: {metabolism.calories.value}," +
            //             $"dirty: {metabolism.isDirty}, poisoned: {(metabolism.poison.value.Equals(1) ? "true" : "false")}");

            HookCalled("OnRunPlayerMetabolism");
            return null;
        }

        private object OnUserApprove(Network.Connection connection)
        {
            HookCalled("OnUserApprove");
            return null;
        }

        #endregion Player Hooks

        #region Entity Hooks

        private bool CanBradleyApcTarget(BradleyAPC apc, BaseEntity entity)
        {
            HookCalled("CanBradleyApcTarget");
            return true;
        }

        private bool CanHelicopterStrafe(PatrolHelicopterAI heli)
        {
            HookCalled("CanHelicopterStrafe");
            return true;
        }

        private bool CanHelicopterStrafeTarget(PatrolHelicopterAI entity, BasePlayer target)
        {
            HookCalled("CanHelicopterStrafeTarget");
            return true;
        }

        private bool CanHelicopterTarget(PatrolHelicopterAI heli, BasePlayer player)
        {
            HookCalled("CanHelicopterTarget");
            return true;
        }

        private bool CanHelicopterUseNapalm(PatrolHelicopterAI heli)
        {
            HookCalled("CanHelicopterUseNapalm");
            return true;
        }

        private bool CanNetworkTo(BaseNetworkable entity, BasePlayer target)
        {
            HookCalled("CanNetworkTo");
            return true;
        }

        private bool CanNpcAttack(BaseNpc npc, BaseEntity target)
        {
            HookCalled("CanNpcAttack");
            return true;
        }

        private bool CanNpcEat(BaseNpc npc, BaseCombatEntity target)
        {
            HookCalled("CanNpcEat");
            return true;
        }

        private bool CanPickupEntity(BaseCombatEntity entity, BasePlayer player)
        {
            HookCalled("CanPickupEntity");
            return false;
        }

        private bool CanRecycle(Recycler recycler, Item item)
        {
            HookCalled("CanRecycle");
            return true;
        }

        private void OnAirdrop(CargoPlane plane, UnityEngine.Vector3 location)
        {
            LogWarning($"Airdrop incoming via plane {plane.net.ID}, target: {location}");

            HookCalled("OnAirdrop");
        }

        private object OnBradleyApcInitialize(BradleyAPC apc)
        {
            HookCalled("OnBradleyApcInitialize");
            return null;
        }

        private object OnBradleyApcHunt(BradleyAPC apc)
        {
            HookCalled("OnBradleyApcHunt");
            return null;
        }

        private object OnBradleyApcPatrol(BradleyAPC apc)
        {
            HookCalled("OnBradleyApcPatrol");
            return null;
        }

        private object OnContainerDropItems(ItemContainer container)
        {
            HookCalled("OnContainerDropItems");
            return null;
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo)
        {
            // TODO: Print player died
            // TODO: Automatically respawn admin after X time?

            HookCalled("OnEntityDeath");
        }

        private void OnEntityEnter(TriggerBase trigger, BaseEntity entity)
        {
            HookCalled("OnEntityEnter");
        }

        private object OnEntityGroundMissing(BaseEntity entity)
        {
            HookCalled("OnEntityGroundMissing");
            return null;
        }

        private void OnEntityKill(BaseNetworkable entity)
        {
            HookCalled("OnEntityKill");
        }

        private void OnEntityLeave(TriggerBase trigger, BaseEntity entity)
        {
            HookCalled("OnEntityLeave");
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            HookCalled("OnEntitySpawned");
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            HookCalled("OnEntityTakeDamage");
        }

        private object OnHelicopterTarget(HelicopterTurret turret, BaseCombatEntity entity)
        {
            HookCalled("OnHelicopterTarget");
            return null;
        }

        private object OnLiftUse(Lift lift, BasePlayer player)
        {
            HookCalled("OnLiftUse");
            return null;
        }

        private object OnNpcPlayerTarget(BaseEntity npc, BaseEntity entity)
        {
            //LogWarning($"OnNpcPlayerTarget - {npc.ShortPrefabName} wants to target {entity?.ShortPrefabName}!");
            HookCalled("OnNpcPlayerTarget");
            return null;
        }

        private object OnNpcTarget(BaseNpc npc, BaseEntity entity)
        {
            //LogWarning($"OnNpcTarget - {npc.ShortPrefabName} wants to target {entity?.ShortPrefabName}!");
            HookCalled("OnNpcTarget");
            return null;
        }

        private void OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            HookCalled("OnOvenToggle");
        }

        private object OnRecycleItem(Recycler recycler, Item item)
        {
            HookCalled("OnRecycleItem");
            return null;
        }

        private object OnRecyclerToggle(Recycler recycler, BasePlayer player)
        {
            HookCalled("OnRecyclerToggle");
            return null;
        }

        private void OnResourceDepositCreated(ResourceDepositManager.ResourceDeposit deposit)
        {
            HookCalled("OnResourceDepositCreated");
        }

        private object OnShopCompleteTrade(ShopFront shop, BasePlayer customer)
        {
            HookCalled("OnShopCompleteTrade");
            return null;
        }

        private object OnTurretAuthorize(AutoTurret turret, BasePlayer player)
        {
            HookCalled("OnTurretAuthorize");
            return null;
        }

        private object OnTurretDeauthorize(AutoTurret turret, BasePlayer player)
        {
            HookCalled("OnTurretDeauthorize");
            return null;
        }

        private object OnTurretShutdown(AutoTurret turret)
        {
            HookCalled("OnTurretShutdown");
            return null;
        }

        private object OnTurretStartup(AutoTurret turret)
        {
            HookCalled("OnTurretStartup");
            return null;
        }

        private object OnTurretTarget(AutoTurret turret, BaseCombatEntity entity)
        {
            HookCalled("OnTurretTarget");
            return null;
        }

        private object OnTurretToggle(AutoTurret turret)
        {
            HookCalled("OnTurretToggle");
            return null;
        }

        #endregion Entity Hooks

        #region Item Hooks

        private ItemContainer.CanAcceptResult CanAcceptItem(ItemContainer container, Item item)
        {
            HookCalled("CanAcceptItem");
            return CanAcceptResult.CanAccept;
        }

        private bool CanCombineDroppedItem(DroppedItem item, DroppedItem targetItem)
        {
            HookCalled("CanCombineDroppedItem");
            return true;
        }

        private object CanMoveItem(Item item, PlayerInventory playerLoot, uint targetContainer, int targetSlot)
        {
            HookCalled("CanMoveItem");
            return null;
        }

        private bool CanStackItem(Item item, Item targetItem)
        {
            HookCalled("CanStackItem");
            return true;
        }

        private void OnConsumeFuel(BaseOven oven, Item fuel, ItemModBurnable burnable)
        {
            // TODO: Print fuel consumed

            HookCalled("OnConsumeFuel");
        }

        /*private Item OnFindBurnable(BaseOven oven)
        {
            HookCalled("OnFindBurnable");
            return ??; // TODO: Create Item
        }*/

        private object OnItemAction(Item item, string action, BasePlayer player)
        {
            HookCalled("OnItemAction");
            return null;
        }

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            // TODO: Print item added

            HookCalled("OnItemAddedToContainer");
        }

        private bool OnItemCraft(ItemCraftTask item)
        {
            // TODO: Print item crafting

            HookCalled("OnItemCraft");
            return true;
        }

        private void OnItemCraftCancelled(ItemCraftTask task)
        {
            HookCalled("OnItemCraftCancelled");
        }

        private void OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            HookCalled("OnItemCraftFinished");
        }

        private void OnItemDeployed(Deployer deployer, BaseEntity entity)
        {
            // TODO: Print item deployed

            HookCalled("OnItemDeployed");
        }

        private void OnItemDropped(Item item, BaseEntity entity)
        {
            HookCalled("OnItemDropped");
        }

        private object OnItemPickup(Item item, BasePlayer player)
        {
            HookCalled("OnItemPickup");
            return null;
        }

        private void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            // TODO: Print item removed

            HookCalled("OnItemRemovedToContainer");
        }

        private void OnItemRepair(BasePlayer player, Item item)
        {
            HookCalled("OnItemRepair");
        }

        private object OnItemResearch(Item item, BasePlayer player)
        {
            HookCalled("OnItemResearch");
            return null;
        }

        private float OnItemResearchEnd(ResearchTable table, float chance)
        {
            HookCalled("OnItemResearchEnd");
            return chance;
        }

        private void OnItemResearchStart(ResearchTable table)
        {
            HookCalled("OnItemResearchStart");
        }

        /*private Item OnItemSplit(Item item, int amount)
        {
            HookCalled("OnItemSplit");
            return ??; // TODO: Create Item
        }*/

        private void OnItemUpgrade(Item item, Item upgraded, BasePlayer player)
        {
            HookCalled("OnItemUpgrade");
        }

        private void OnItemUse(Item item, int amountToUse)
        {
            HookCalled("OnItemUse");
        }

        private void OnLoseCondition(Item item, ref float amount)
        {
            HookCalled("OnLoseCondition");
        }

        private int OnMaxStackable(Item item)
        {
            HookCalled("OnMaxStackable");
            return 100;
        }

        private object OnTrapArm(BearTrap trap, BasePlayer player)
        {
            HookCalled("OnTrapArm");
            return null;
        }

        private object OnTrapDisarm(Landmine trap, BasePlayer player)
        {
            HookCalled("OnTrapDisarm");
            return null;
        }

        private void OnTrapSnapped(BaseTrapTrigger trap, GameObject go)
        {
            HookCalled("OnTrapSnapped");
        }

        private object OnTrapTrigger(BearTrap trap, GameObject go)
        {
            HookCalled("OnTrapTrigger");
            return null;
        }

        #endregion Item Hooks

        #region Resource Gathering Hooks

        private void OnCollectiblePickup(Item item, BasePlayer player)
        {
            LogWarning($"{player.displayName} picked up {item.info.displayName.english}");

            HookCalled("OnCollectiblePickup");
        }

        private void OnCropGather(PlantEntity plant, Item item, BasePlayer player)
        {
            // TODO: Print item to be gathered

            HookCalled("OnCropGather");
        }

        private object OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            HookCalled("OnDispenserBonus");
            return null;
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            // TODO: Print item to be gathered

            HookCalled("OnDispenserGather");
        }

        private void OnQuarryEnabled() => HookCalled("OnQuarryEnabled");

        private void OnQuarryGather(MiningQuarry quarry, Item item)
        {
            HookCalled("OnQuarryGather");
        }

        private void OnSurveyGather(SurveyCharge survey, Item item)
        {
            HookCalled("OnSurveyGather");
        }

        #endregion Resource Gathering Hooks

        #region Sign Hooks

        private bool CanUpdateSign(BasePlayer player, Signage sign)
        {
            HookCalled("CanUpdateSign");
            return true;
        }

        private void OnSignLocked(Signage sign, BasePlayer player)
        {
            HookCalled("OnSignLocked");
        }

        private void OnSignUpdated(Signage sign, BasePlayer player, string text)
        {
            HookCalled("OnSignUpdated");
        }

        private void OnSpinWheel(BasePlayer player, SpinnerWheel wheel)
        {
            HookCalled("OnSpinWheel");
        }

        #endregion Sign Hooks

        #region Structure Hooks

        private bool CanAffordUpgrade(BasePlayer player, BuildingBlock block, BuildingGrade.Enum grade)
        {
            HookCalled("CanAffordUpgrade");
            return true;
        }

        private object CanAssignBed(SleepingBag bag, BasePlayer player, ulong targetPlayerId)
        {
            HookCalled("CanAssignBed");
            return true;
        }

        private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            HookCalled("CanBuild");
            return null;
        }

        private object CanChangeCode(CodeLock codeLock, BasePlayer player, string newCode, bool isGuestCode)
        {
            HookCalled("CanChangeCode");
            return null;
        }

        private bool CanChangeGrade(BasePlayer player, BuildingBlock block, BuildingGrade.Enum grade)
        {
            HookCalled("CanChangeGrade");
            return true;
        }

        private bool CanDemolish(BasePlayer player, BuildingBlock block, BuildingGrade.Enum grade)
        {
            HookCalled("CanDemolish");
            return true;
        }

        private object CanHideStash(StashContainer stash, BasePlayer player)
        {
            HookCalled("CanHideStash");
            return null;
        }

        private object CanLock(BaseLock baseLock, BasePlayer player)
        {
            HookCalled("CanLock");
            return null;
        }

        private bool CanPickupLock(BasePlayer player, BaseLock baseLock)
        {
            HookCalled("CanPickupLock");
            return true;
        }

        private object CanSeeStash(StashContainer stash, BasePlayer player)
        {
            HookCalled("CanSeeStash");
            return null;
        }

        private object CanSetBedPublic(SleepingBag bed, BasePlayer player)
        {
            HookCalled("CanSetBedPublic");
            return null;
        }

        private object CanUnlock(BaseLock baseLock, BasePlayer player)
        {
            HookCalled("CanUnlock");
            return null;
        }

        private bool CanUseLockedEntity(BasePlayer player, BaseLock baseLock)
        {
            HookCalled("CanUseLockedEntity");
            return true;
        }

        private object OnCodeEntered(CodeLock codeLock, BasePlayer player, string code)
        {
            HookCalled("OnCodeEntered");
            return null;
        }

        private object OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            HookCalled("OnCupboardAuthorize");
            return null;
        }

        private object OnCupboardClearList(BuildingPrivlidge privilege, BasePlayer player)
        {
            HookCalled("OnCupboardClearList");
            return null;
        }

        private object OnCupboardDeauthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            HookCalled("OnCupboardDeauthorize");
            return null;
        }

        private void OnDoorClosed(Door door, BasePlayer player)
        {
            HookCalled("OnDoorClosed");
        }

        private void OnDoorOpened(Door door, BasePlayer player)
        {
            HookCalled("OnDoorOpened");
        }

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            HookCalled("OnEntityBuilt");
        }

        private void OnHammerHit(BasePlayer player, HitInfo info)
        {
            HookCalled("OnHammerHit");
        }

        private void OnStructureDemolish(BuildingBlock block, BasePlayer player)
        {
            HookCalled("OnStructureDemolish");
        }

        private void OnStructureRepair(BaseCombatEntity entity, BasePlayer player)
        {
            HookCalled("OnStructureRepair");
        }

        private void OnStructureRotate(BuildingBlock block, BasePlayer player)
        {
            HookCalled("OnStructureRotate");
        }

        private object OnStructureUpgrade(BaseCombatEntity entity, BasePlayer player, BuildingGrade.Enum grade)
        {
            HookCalled("OnStructureUpgrade");
            return null;
        }

        #endregion Structure Hooks

        #region Vending Hooks

        private bool CanAdministerVending(VendingMachine vending, BasePlayer player)
        {
            HookCalled("CanAdministerVending");
            return true;
        }

        private bool CanUseVending(VendingMachine vending, BasePlayer player)
        {
            HookCalled("CanUseVending");
            return true;
        }

        private bool CanVendingAcceptItem(VendingMachine vending, Item item)
        {
            HookCalled("CanVendingAcceptItem");
            return true;
        }

        private void OnAddVendingOffer(VendingMachine vending, BasePlayer player) => HookCalled("OnAddVendingOffer");

        private void OnBuyVendingItem(VendingMachine vending, BasePlayer player) => HookCalled("OnBuyVendingItem");

        private void OnDeleteVendingOffer(VendingMachine vending, BasePlayer player) => HookCalled("OnDeleteVendingOffer");

        private void OnOpenVendingAdmin(VendingMachine vending, BasePlayer player) => HookCalled("OnOpenVendingAdmin");

        private void OnOpenVendingShop(VendingMachine vending, BasePlayer player) => HookCalled("OnOpenVendingShop");

        private void OnRefreshVendingStock(VendingMachine vending, BasePlayer player) => HookCalled("OnRefreshVendingStock");

        private object OnRotateVendingMachine(VendingMachine vending, BasePlayer player)
        {
            HookCalled("OnRotateVendingMachine");
            return null;
        }

        private void OnToggleVendingBroadcast(VendingMachine vending, BasePlayer player) => HookCalled("OnToggleVendingBroadcast");

        private object OnVendingTransaction(VendingMachine vending, BasePlayer player)
        {
            HookCalled("OnVendingTransaction");
            return null;
        }

        #endregion Vending Hooks

        #region Weapon Hooks

        private object OnCreateWorldProjectile(HitInfo info, Item item)
        {
            HookCalled("OnCreateWorldProjectile");
            return null;
        }

        private void OnExplosiveDropped(BasePlayer player, BaseEntity entity)
        {
            HookCalled("OnExplosiveDropped");
        }

        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            HookCalled("OnExplosiveThrown");
        }

        private void OnMeleeThrown(BasePlayer player, Item item)
        {
            HookCalled("OnMeleeThrown");
        }

        private object OnReloadMagazine(BasePlayer player, BaseProjectile projectile)
        {
            HookCalled("OnReloadMagazine");
            return null;
        }

        private object OnReloadWeapon(BasePlayer player, BaseProjectile projectile)
        {
            HookCalled("OnReloadWeapon");
            return null;
        }

        private void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            HookCalled("OnRocketLaunched");
        }

        private void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod, ProtoBuf.ProjectileShoot projectiles)
        {
            HookCalled("OnWeaponFired");
        }

        #endregion Weapon Hooks

        [Command("dev.entityi")]
        private void EntityInfoCommand(IPlayer player, string command, string[] args)
        {
            var entity = FindEntity(player.Object as BasePlayer, 3);
            if (entity != null)
            {
                player.Reply($"Prefab: {entity.PrefabName}\nPrefab ID: {entity.prefabID.ToString()}\nNetwork ID: {entity.net.ID}\nOwner ID: {entity.OwnerID}");
            }
        }

        [Command("dev.entity")]
        private void EntityCommand(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;

            foreach (var str in GameManifest.Current.pooledStrings)
            {
                if (!str.str.StartsWith("assets/prefabs/") || !str.str.EndsWith(".prefab") || str.str.Contains("effects/") || str.str.Contains(".item")) continue;
                if (!str.str.Contains(args[0])) continue;
                LogWarning(str.str);

                if (str.str.Contains("building core"))
                {
                    var block = (BuildingBlock)GameManager.server.CreateEntity(str.str);
                    block.transform.position = basePlayer.transform.position;
                    block.transform.rotation = basePlayer.transform.rotation;
                    block.gameObject.SetActive(true);
                    block.blockDefinition = PrefabAttribute.server.Find<Construction>(block.prefabID);
                    block.Spawn();
                    block.SetGrade(BuildingGrade.Enum.Metal);
                    block.SetHealthToMax();
                    block.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                }
                else
                {
                    var entity = GameManager.server.CreateEntity(str.str, basePlayer.transform.position, basePlayer.transform.rotation);
                    entity.Spawn();
                }
            }
        }

        [Command("dev.heli")]
        private void HeliCommand(IPlayer player, string command, string[] args)
        {
            var entity = GameManager.server.CreateEntity("assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab");
            if (entity != null)
            {
                var heli = entity.GetComponent<PatrolHelicopterAI>();
                heli.SetInitialDestination((player.Object as BasePlayer).transform.position + new Vector3(0f, 10f, 0f), 0.25f);
                entity.Spawn();
            }
        }

        private Timer npcMove;

        [Command("dev.npc")]
        private void NpcCommand(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            var pos = basePlayer.transform.position;

            BasePlayer npc;
            var type = args.Length > 0 ? args[0] : null;
            switch (type)
            {
                case "mur":
                case "murderer":
                {
                    var entity = GameManager.server.CreateEntity("assets/prefabs/npc/murderer/murderer.prefab",
                        new Vector3(pos.x + 1, pos.y, pos.z), new Quaternion());
                    npc = entity as NPCPlayer;
                    break;
                }

                case "sci":
                case "scientist":
                {
                    var entity = GameManager.server.CreateEntity("assets/prefabs/npc/scientist/scientist.prefab",
                        new Vector3(pos.x + 1, pos.y, pos.z), new Quaternion());
                    npc = entity as NPCPlayerApex;
                    break;
                }

                default:
                {
                    var entity = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab",
                        new Vector3(pos.x + 1, pos.y, pos.z), new Quaternion());
                    npc = entity as BasePlayer;
                    break;
                }
            }

            if (npc != null)
            {
                npc.Spawn();
                // TODO: Give some items
                if (npc is BasePlayer)
                {
                    npc.displayName = "Bob";
                    npcMove = timer.Every(0.1f, () =>
                    {
                        if (basePlayer.IsDead() || npc.IsDead() || basePlayer.IsWounded() || npc.IsWounded())
                        {
                            npcMove.Destroy();
                            return;
                        }

                        var newPos = player.Position();
                        npc.MovePosition(new Vector3(newPos.X + 1, newPos.Y, newPos.Z + 1));
                        npc.transform.rotation = basePlayer.transform.rotation;
                    });
                }
                npc.SendNetworkUpdate();
            }
        }

        [Command("dev.npc murderer")]
        private void MurdererCommand(IPlayer player, string command, string[] args)
        {
            var pos = (player.Object as BasePlayer).transform.position;
            var entity = GameManager.server.CreateEntity("assets/prefabs/npc/murderer/murderer.prefab", new Vector3(pos.x + 1, pos.y, pos.z));
            var npc = entity as NPCPlayer;
            if (npc != null)
            {
                npc.Spawn();
                // TODO: Give some items
                npc.displayName = "Bob";
                npc.SendNetworkUpdate();
            }
        }

        [Command("dev.plane")]
        private void PlaneCommand(IPlayer player, string command, string[] args)
        {
            var entity = GameManager.server.CreateEntity("assets/prefabs/npc/cargo plane/cargo_plane.prefab", new Vector3(), new Quaternion());
            entity?.Spawn();
        }

        [Command("dev.supply")]
        private void SupplyDropCommand(IPlayer player, string command, string[] args)
        {
            var pos = new Vector3(player.Position().X, player.Position().Y + 15, player.Position().Z);
            var entity = GameManager.server.CreateEntity("assets/prefabs/misc/supply drop/supply_drop.prefab", pos, new Quaternion());
            entity?.Spawn();
        }

        [Command("dev.remove")]
        private void RemoveCommand(IPlayer player, string command, string[] args)
        {
            var entity = FindEntity(player.Object as BasePlayer, 3);
            entity?.Kill(BaseNetworkable.DestroyMode.Gib);
        }

        [Command("dev.removeall")]
        private void RemoveAllCommand(IPlayer player, string command, string[] args)
        {
            var entities = UnityEngine.Object.FindObjectsOfType<BaseEntity>();
            foreach (var entity in entities) { if (entity is BasePlayer) continue; entity.Kill(BaseNetworkable.DestroyMode.Gib); }
        }

        private BaseEntity FindEntity(BasePlayer player, float distance)
        {
            RaycastHit hit;
            var ray = new Ray(player.eyes.position, player.eyes.HeadForward());
            return Physics.Raycast(ray, out hit, distance) ? hit.GetEntity() : null;
        }

        [Command("dev.view1")]
        private void View1Command(IPlayer player, string command, string[] args)
        {
            BasePlayer basePlayer = player.Object as BasePlayer;
            basePlayer.SetPlayerFlag(BasePlayer.PlayerFlags.EyesViewmode, false);
            basePlayer.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, false);
        }

        [Command("dev.view2")]
        private void View2Command(IPlayer player, string command, string[] args)
        {
            BasePlayer basePlayer = player.Object as BasePlayer;
            basePlayer.SetPlayerFlag(BasePlayer.PlayerFlags.EyesViewmode, true);
            basePlayer.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, false);
        }

        [Command("dev.view3")]
        private void View3Command(IPlayer player, string command, string[] args)
        {
            BasePlayer basePlayer = player.Object as BasePlayer;
            basePlayer.SetPlayerFlag(BasePlayer.PlayerFlags.EyesViewmode, false);
            basePlayer.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, true);
        }

#endif

#if SEVENDAYSTODIE

        #region Server Hooks

        private object OnServerCommand(ClientInfo client, string[] args)
        {
            HookCalled("OnServerCommand");
            return null;
        }

        #endregion Server Hooks

        #region Entity Hooks

        private void OnAirdrop(UnityEngine.Vector3 location)
        {
            HookCalled("OnAirdrop");
        }

        private void OnEntitySpawned(Entity entity)
        {
            HookCalled("OnEntitySpawned");
        }

        private void OnEntityTakeDamage(EntityAlive entity, DamageSource source)
        {
            HookCalled("OnEntityTakeDamage");
        }

        private void OnEntityDeath(Entity entity, DamageResponse response)
        {
            HookCalled("OnEntityDeath");
        }

        #endregion Entity Hooks

        #region Player Hooks

        private void OnPlayerConnected(ClientInfo client)
        {
            HookCalled("OnPlayerConnected");
        }

        private void OnPlayerDisconnected(ClientInfo client)
        {
            LogWarning("OnPlayerDisconnected");
        }

        private object OnPlayerChat(ClientInfo client, string message)
        {
            LogWarning("OnPlayerChat");
            return null;
        }

        private void OnPlayerRespawned(ClientInfo client, string reason)
        {
            LogWarning("OnPlayerRespawned");
        }

        private void OnExperienceGained(ClientInfo client, uint exp)
        {
            LogWarning("OnExperienceGained");
        }

        #endregion Player Hooks

#endif

#if THEFOREST

        #region Server Hooks

        private object OnServerCommand(string command, string[] args)
        {
            LogWarning($"{command} {string.Concat(args)} executed");
            HookCalled("OnServerCommand");
            return null;
        }

        #endregion Server Hooks

        #region Player Hooks

        private bool CanClientLogin(BoltConnection connection)
        {
            HookCalled("CanClientLogin");
            return true;
        }

        private object OnPlayerApproved(BoltConnection connection)
        {
            HookCalled("OnPlayerApproved");
            return null;
        }

        private object OnPlayerChat(BoltEntity entity, string message)
        {
            HookCalled("OnPlayerChat");
            return null;
        }

        private void OnPlayerConnected(BoltEntity entity)
        {
            HookCalled("OnPlayerConnected");
        }

        private void OnPlayerDisconnected(BoltEntity entity)
        {
            HookCalled("OnPlayerDisconnected");
        }

        private object OnPlayerSpawn(BoltEntity entity)
        {
            HookCalled("OnPlayerSpawn");
            return null;
        }

        #endregion Player Hooks

#endif

#if UNTURNED

        #region Server Hooks

        private object OnServerCommand(Steamworks.CSteamID steamId, string command, string arg)
        {
            HookCalled("OnServerCommand");
            return null;
        }

        #endregion Server Hooks

#endif
    }
}
