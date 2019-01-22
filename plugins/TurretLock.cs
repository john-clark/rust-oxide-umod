using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Turret Lock", "redBDGR", "1.0.2")]
    [Description("Gives players the ability to lock their turrets")]
    class TurretLock : RustPlugin
    {
        private bool Changed;
        private const string codeLockPrefab = "assets/prefabs/locks/keypad/lock.code.prefab";
        private const string effectDenied = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab";
        private const string effectDeployed = "assets/prefabs/locks/keypad/effects/lock-code-deploy.prefab";
        private const string permissionName = "turretlock.use";

        private bool doEffects;

        #region Oxide Hooks

        private void Init()
        {
            permission.RegisterPermission(permissionName, this);
            LoadVariables();

            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["No Permission"] = "You are not allowed to use this command",
                ["Not Allowed"] = "You are not allowed to do this because you must be authorised to the turrets codelock",
                ["Not Enough Items"] = "You need a codelock in your inventory to lock this turret",
                ["Already Has Codelock"] = "This turret already has a codelock",
                ["Not A Turret"] = "This entity is not a turret",
            }, this);
        }

        private void LoadVariables()
        {
            doEffects = Convert.ToBoolean(GetConfig("Settings", "Do Effects", true));

            if (!Changed) return;

            SaveConfig();
            Changed = false;
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private object OnTurretAuthorize(AutoTurret turret, BasePlayer player)
        {
            return CheckTurretNoSphereCast(turret, player);
        }

        private object OnTurretDeauthorize(AutoTurret turret, BasePlayer player)
        {
            return CheckTurretNoSphereCast(turret, player);
        }

        private object OnTurretShutdown(AutoTurret turret)
        {
            return CheckTurret(turret);
        }

        private object OnTurretStartup(AutoTurret turret)
        {
            return CheckTurret(turret);
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (!entity.GetComponent<AutoTurret>())
                return;

            if (CheckTurretNoSphereCast(entity.GetComponent<AutoTurret>(), player) == null)
                return;

            NextTick(player.EndLooting);
        }

        private void OnTurretModeToggle(AutoTurret turret)
        {
            if (CheckTurret(turret) == null)
                return;

            turret.SetPeacekeepermode(!turret.PeacekeeperMode());
        }

        private object OnTurretClearList(AutoTurret turret, BasePlayer player)
        {
            if (CheckTurretNoSphereCast(turret, player) == null)
                return null;

            return true;
        }

        #endregion

        #region Chat Commands

        [ChatCommand("lockturret")]
        private void LockTurretCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName))
            {
                player.ChatMessage(msg("No Permission", player.UserIDString));
                return;
            }

            RaycastHit hit;
            if (!UnityEngine.Physics.Raycast(player.eyes.HeadRay(), out hit, 5f))
                return;

            BaseEntity turretEntity = hit.GetEntity();
            if (turretEntity is AutoTurret)
            {
                if (turretEntity.GetComponentInChildren<CodeLock>())
                {
                    player.ChatMessage(msg("Already Has Codelock", player.UserIDString));
                    return;
                }

                Item codelockItem = InventoryContainsCodelock(player);
                if (codelockItem != null)
                {
                    AddCodelock(hit.GetEntity().GetComponent<AutoTurret>());
                    RemoveThink(codelockItem);
                }
                else
                {
                    player.ChatMessage(msg("Not Enough Items", player.UserIDString));
                }
            }
            else
            {
                player.ChatMessage(msg("Not A Turret", player.UserIDString));
                return;
            }
        }

        #endregion

        #region Methods / Helpers

        private void AddCodelock(AutoTurret turret)
        {
            BaseEntity ent = GameManager.server.CreateEntity(codeLockPrefab, turret.transform.position);
            if (!ent)
                return;

            ent.Spawn();
            ent.SetParent(turret);
            ent.transform.localEulerAngles = new Vector3(0, 160, 0);
            ent.transform.localPosition = new Vector3(0.27f, 0.37f, 0.1f);
            //CodeLock _lock = ent.GetComponent<CodeLock>();
            ent.SendNetworkUpdateImmediate();
            if (doEffects)
                Effect.server.Run(effectDeployed, ent.transform.position);
        }

        private static BasePlayer FindBasePlayer(Vector3 pos)
        {
            RaycastHit[] hits = UnityEngine.Physics.SphereCastAll(pos, 4f, Vector3.up);
            return (from hit in hits where hit.GetEntity()?.GetComponent<BasePlayer>() select hit.GetEntity()?.GetComponent<BasePlayer>()).FirstOrDefault();
        }

        private object CheckTurretNoSphereCast(AutoTurret turret, BasePlayer player)
        {
            CodeLock _lock = turret.GetComponentInChildren<CodeLock>();
            if (!_lock) return null;

            if (_lock.code == string.Empty)
                return null;

            if (_lock.whitelistPlayers.Contains(player.userID))
                return null;

            player.ChatMessage(msg("Not Allowed", player.UserIDString));
            if (doEffects)
                Effect.server.Run(effectDenied, _lock.transform.position);
            return true;
        }

        private object CheckTurret(AutoTurret turret)
        {
            CodeLock _lock = turret.GetComponentInChildren<CodeLock>();
            if (!_lock)
                return null;

            BasePlayer player = FindBasePlayer(turret.transform.position);
            if (player == null)
                return null;

            if (_lock.code == string.Empty)
                return null;

            if (_lock.whitelistPlayers.Contains(player.userID))
                return null;

            player.ChatMessage(msg("Not Allowed", player.UserIDString));
            if (doEffects)
                Effect.server.Run(effectDenied, _lock.transform.position);
            return true;
        }

        private Item InventoryContainsCodelock(BasePlayer player)
        {
            foreach(Item item in player.inventory.containerBelt.itemList)
                if (item.info.shortname == "lock.code")
                    return item;

            foreach(Item item in player.inventory.containerMain.itemList)
                if (item.info.shortname == "lock.code")
                    return item;

            return null;
        }

        private void RemoveThink(Item item)
        {
            if (item.amount == 1)
            {
                item.RemoveFromContainer();
                item.RemoveFromWorld();
                item.Remove();
            }
            else if (item.amount >= 2)
            {
                item.amount--;
                item.MarkDirty();
            }
            else
                Puts("Player had > 1 items in their inventory");
        }

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
            if (data.TryGetValue(datavalue, out value)) return value;
            value = defaultValue;
            data[datavalue] = value;
            Changed = true;
            return value;
        }

        private string msg(string key, string id = null) => lang.GetMessage(key, this, id);

        #endregion
    }
}
