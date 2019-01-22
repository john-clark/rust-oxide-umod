using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using UnityEngine;
using Oxide.Plugins.BlueprintManagerExtensions;

namespace Oxide.Plugins
{
    [Info("Blueprint Manager", "Jacob", "1.0.4")]
    internal class BlueprintManager : RustPlugin
    {
        /*
         * Full credit to k1lly0u for the code from "NoWorkbench."
         */

        #region Fields

        public static BlueprintManager Instance;
        private Configuration _configuration;
        private TriggerWorkbench _workbenchTrigger;
        private Workbench _workbench;

        #endregion

        #region Properties

        public List<string> GetDefaultBlueprints => _configuration.DefaultBlueprints.ConvertAll(x => x.ToString());

        #endregion

        #region Configuration 

        private class Configuration
        {
            public readonly List<object> DefaultBlueprints = new List<object>();

            public Configuration()
            {
                GetConfig(ref DefaultBlueprints, "Settings", "Default blueprints");

                Instance.SaveConfig();
            }

            private void GetConfig<T>(ref T variable, params string[] path)
            {
                if (path.Length == 0)
                    return;

                if (Instance.Config.Get(path) == null)
                {
                    SetConfig(ref variable, path);
                    Instance.PrintWarning($"Added field to config: {string.Join("/", path)}");
                }

                variable = (T)Convert.ChangeType(Instance.Config.Get(path), typeof(T));
            }

            private void SetConfig<T>(ref T variable, params string[] path) => Instance.Config.Set(path.Concat(new object[] { variable }).ToArray());
        }

        protected override void LoadDefaultConfig() => PrintWarning("Generating new config file...");

        #endregion

        #region Commands

        [ChatCommand("blueprint")]
        private void BlueprintCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "blueprintmanager.admin"))
            {
                PrintToChat(player, Lang(player, "NoPermission"));
                return;
            }

            if (args.Length < 2)
            {
                if (args.Length == 1 && args[0].ToLower() == "help")
                    PrintToChat(player, Lang(player, "Help"));
                else
                    PrintToChat(player, Lang(player, "IncorrectArguments"));

                return;
            }

            var target = FindPlayer(player, args[1]);
            if (target == null)
                return;

            switch (args[0].ToLower())
            {
                case "unlock":
                    if (args.Length < 3)
                    {
                        PrintToChat(player, Lang(player, "IncorrectArguments"));
                        return;
                    }

                    var itemDefinition = GetItemDefinition(args[2]);
                    if (itemDefinition == null)
                    {
                        PrintToChat(player, Lang(player, "InvalidItem", args[2].ToLower()));
                        return;
                    }

                    PrintToChat(player, Lang(player, "ItemUnlocked", args[2].ToLower(), target.displayName));
                    player.UnlockItem(args[2]);
                    break;

                case "unlockall":
                    PrintToChat(player, Lang(player, "AllUnlocked", target.displayName));
                    target.UnlockAll();
                    break;

                case "resetall":
                    PrintToChat(player, Lang(player, "AllReset", target.displayName));
                    target.ResetAll();
                    break;
            }
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages() => lang.RegisterMessages(new Dictionary<string, string>
        {
            {"NoPermission", "Error, you lack permission."},
            {"IncorrectArguments", "Error, incorrect arguments. Try [#ADD8E6]/blueprint help.[/#]"},

            {"NoPlayerFound", "Error, no player found by the name of [#ADD8E6]{0}[/#]."},
            {"MultiplePlayersFound", "Error, multiple players found by the name of [#ADD8E6]{0}[/#]."},

            {"ItemUnlocked", "Sucessfully unlocked item [#ADD8E6]{0}[/#] for [#ADD8E6]{1}[/#]."},
            {"InvalidItem", "Error, no item found by the name of [#ADD8E6]{0}[/#], are you providing a short name?"},

            {"AllUnlocked", "Sucessfully unlocked all items for [#ADD8E6]{0}[/#]."},

            {"AllReset", "Sucessfully reset all items for [#ADD8E6]{0}[/#]."},

            {"Help", "Help\n[#ADD8E6]/blueprint <unlock> <player> <shortName>[/#]\n[#ADD8E6]/blueprint <unlockall|resetall> <player>[/#]"}

        }, this);

        private string Lang(BasePlayer player, string key, params object[] args)
        {
            var message = lang.GetMessage(key, this, player.UserIDString);
            if (args.Length != 0)
                message = string.Format(message, args);

            return covalence.FormatText(message);
        }

        #endregion

        #region Mehods

        private BasePlayer FindPlayer(BasePlayer player, string nameOrID)
        {
            var targets = BasePlayer.activePlayerList.FindAll(x => nameOrID.IsSteamId() ? x.UserIDString == nameOrID : x.displayName.ToLower().Contains(nameOrID));
            if (targets.Count == 1)
                return targets[0];

            PrintToChat(player, Lang(player, targets.Count == 0 ? "NoPlayerFound" : "MultiplePlayersFound"), nameOrID);
            return null;
        }

        public ItemDefinition GetItemDefinition(string shortName)
        {
            if (string.IsNullOrEmpty(shortName))
                return null;

            var itemDefinition = ItemManager.FindItemDefinition(shortName.ToLower());

            return itemDefinition;
        }

        private void SpawnWorkbench()
        {
            _workbench = GameManager.server.CreateEntity("assets/prefabs/deployable/tier 3 workbench/workbench3.deployed.prefab", new Vector3(0, -50, 0)) as Workbench;
            _workbench.enableSaving = false;
            _workbench.Spawn();

            _workbench.GetComponent<DestroyOnGroundMissing>().enabled = false;
            _workbench.GetComponent<GroundWatch>().enabled = false;

            _workbenchTrigger = _workbench.GetComponentInChildren<TriggerWorkbench>();

            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerInit(player);

            _workbenchTrigger.name = "workbench";

            timer.In(1, () =>
            {
                if (_workbench == null || _workbench.IsDestroyed)
                    SpawnWorkbench();
            });
        }

        #endregion

        #region Oxide Hooks

        private void Unload() => _workbench.DieInstantly();

        private void OnServerInitialized()
        {
            permission.RegisterPermission("blueprintmanager.admin", this);

            permission.RegisterPermission("blueprintmanager.all", this);
            permission.RegisterPermission("blueprintmanager.config", this);
            permission.RegisterPermission("blueprintmanager.noworkbench", this);

            Instance = this;
            _configuration = new Configuration();

            SpawnWorkbench();

            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerInit(player);
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, "blueprintmanager.all"))
                player.UnlockAll();
            else if (permission.UserHasPermission(player.UserIDString, "blueprintmanager.config"))
                player.UnlockConfig();

            if (!permission.UserHasPermission(player.UserIDString, "blueprintmanager.noworkbench"))
                return;

            if (_workbenchTrigger != null)
                player.EnterTrigger(_workbenchTrigger);
        }

        private void OnEntityLeave(TriggerBase trigger, BaseEntity entity)
        {
            var player = entity.ToPlayer();
            if (player == null)
                return;

            if (trigger.name == "workbench")
                player.EnterTrigger(trigger);
        }

        #endregion
    }

    namespace BlueprintManagerExtensions
    {
        static class Extensions
        {
            public static void UnlockAll(this BasePlayer player)
            {
                var info = SingletonComponent<ServerMgr>.Instance.persistance.GetPlayerInfo(player.userID);
                info.unlockedItems = ItemManager.bpList
                    .Select(x => x.targetItem.itemid)
                    .ToList();
                SingletonComponent<ServerMgr>.Instance.persistance.SetPlayerInfo(player.userID, info);
                player.SendNetworkUpdate();
            }

            public static void UnlockItem(this BasePlayer player, string shortName)
            {
                var blueprintComponent = player.blueprints;
                if (blueprintComponent == null) return;

                var itemDefinition = BlueprintManager.Instance.GetItemDefinition(shortName);
                if (itemDefinition == null)
                    return;

                blueprintComponent.Unlock(itemDefinition);
            }

            public static void UnlockConfig(this BasePlayer player)
            {
                foreach (var shortName in BlueprintManager.Instance.GetDefaultBlueprints)
                {
                    if (string.IsNullOrEmpty(shortName))
                        continue;

                    player.UnlockItem(shortName);
                }
            }

            public static void ResetAll(this BasePlayer player)
            {
                var blueprintComponent = player.blueprints;
                if (blueprintComponent == null)
                    return;

                blueprintComponent.Reset();
            }
        }
    }
}