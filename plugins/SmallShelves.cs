using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Small Shelves", "redBDGR", "1.0.3", ResourceId = 2772)]
    [Description("Allow players to place smaller shelves")]

    class SmallShelves : RustPlugin
    {
        private const string permissionName = "smallshelves.use";
        List<ulong> activatedIDs = new List<ulong>();

        private void Init()
        {
            permission.RegisterPermission(permissionName, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["No Permission"] = "You are not allowed to use this command",
                ["Deactivated"] = "Small shelf placement de-activated",
                ["Activated"] = "Small shelf placement activated",

            }, this);
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity.ShortPrefabName != "shelves")
                return;
            if (!activatedIDs.Contains(entity.GetComponent<BaseEntity>().OwnerID))
                return;
            entity.Kill();
            RaycastHit hit;
            UnityEngine.Physics.Raycast(entity.transform.position + new Vector3(0, 0.1f, 0), Vector3.down, out hit, 3f);
            DecayEntity dEnt = hit.GetEntity()?.GetComponent<DecayEntity>();
            if (!dEnt)
                return;
            BaseEntity ent = SpawnSmallShelves(entity.transform.position - (entity.transform.forward * 0.35f), entity.transform.rotation, dEnt);
        }

        private ItemContainer.CanAcceptResult CanAcceptItem(ItemContainer container, Item item, int targetPos)
        {
            BaseEntity ent = container?.entityOwner;
            if (!ent)
                return ItemContainer.CanAcceptResult.CanAccept;
            if (ent.GetComponent<VisualStorageContainer>())
                return ItemContainer.CanAcceptResult.CannotAccept;
            return ItemContainer.CanAcceptResult.CanAccept;
        }

        [ChatCommand("smallshelves")]
        private void SmallShelvesToggleCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName))
            {
                player.ChatMessage(msg("No Permission", player.UserIDString));
                return;
            }
            if (activatedIDs.Contains(player.userID))
            {
                activatedIDs.Remove(player.userID);
                player.ChatMessage(msg("Deactivated", player.UserIDString));
            }
            else
            {
                activatedIDs.Add(player.userID);
                player.ChatMessage(msg("Activated", player.UserIDString));
            }
        }

        private BaseEntity SpawnSmallShelves(Vector3 pos, Quaternion rot, DecayEntity floor)
        {
            BaseEntity ent = GameManager.server.CreateEntity("assets/scripts/entity/misc/visualstoragecontainer/visualshelvestest.prefab", pos, rot);
            LootContainer container = ent.GetComponent<LootContainer>();
            container.destroyOnEmpty = false;
            container.initialLootSpawn = false;
            container.SetFlag(BaseEntity.Flags.Locked, true);
            ent.GetComponent<DecayEntity>().AttachToBuilding(floor);
            DestroyOnGroundMissing des = ent.gameObject.AddComponent<DestroyOnGroundMissing>();
            GroundWatch watch = ent.gameObject.AddComponent<GroundWatch>();
            watch.InvokeRepeating("OnPhysicsNeighbourChanged", 0f, 0.15f);
            ent.Spawn();
            return ent;
        }

        private string msg(string key, string id = null) => lang.GetMessage(key, this, id);
    }
}
