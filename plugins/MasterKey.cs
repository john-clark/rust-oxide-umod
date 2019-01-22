using System;
using System.Collections.Generic;

using ProtoBuf;
using UnityEngine;

using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Master Key", "Tori1157", "0.7.4")]
    [Description("Gain access and/or authorization to any locked object.")]
    public class MasterKey : CovalencePlugin
    {
        #region Initialization

        private readonly DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetFile("MasterKey");
        private readonly string[] lockableTypes = { "box", "cell", "door", "gate", "hatch", "shop", "cupboard", "locker", "fridge", "hackable", "turret" };
        private Dictionary<string, bool> playerPrefs = new Dictionary<string, bool>();
        private const string permCom = "masterkey.command";
        //private const string permBuild = "masterkey.build";
        private bool logUsage;
        private bool showMessages;

        private new void LoadDefaultConfig()
        {
            Config["Log Usage (true/false)"] = logUsage = GetConfig("Log Usage (true/false)", true);
            Config["Show Messages (true/false)"] = showMessages = GetConfig("Show Messages (true/false)", true);
            SaveConfig();
        }

        private void Init()
        {
            LoadDefaultConfig();
            playerPrefs = dataFile.ReadObject<Dictionary<string, bool>>();

            permission.RegisterPermission(permCom, this);
            //permission.RegisterPermission(permBuild, this);
            foreach (var type in lockableTypes)
                permission.RegisterPermission($"masterkey.{type}", this);
        }

        #endregion Initialization

        #region Localization

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["MasterKeyDisabled"] = "Master key access is now disabled.",
                ["MasterKeyEnabled"] = "Master key access is now enabled.",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command.",
                ["UnlockedWith"] = "Unlocked {0} with master key!",
                ["LockedWith"] = "Locked {0} with master key!",
                ["OpenWith"] = "Opened {0} with master key!",
                ["LogUnlock"] = "{0} unlocked '{1}' with master key at {2}",
                ["LogLock"] = "{0} locked '{1}' with master key at {2}",
                ["LogOpen"] = "{0} opened '{1}' with master key at {2}",
            }, this);
        }

        #endregion Localization

        #region Chat Command

        [Command("masterkey", "mkey", "mk")]
        private void ChatCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permCom))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            if (!playerPrefs.ContainsKey(player.Id)) playerPrefs.Add(player.Id, true);
            playerPrefs[player.Id] = !playerPrefs[player.Id];
            dataFile.WriteObject(playerPrefs);

            player.Reply(playerPrefs[player.Id] ? Lang("MasterKeyEnabled", player.Id) : Lang("MasterKeyDisabled"));
        }

        #endregion Chat Command

        #region Build Anywhere

        /*private void OnEntityEnter(TriggerBase trigger, BaseEntity entity)
        {
            var player = entity as BasePlayer;
            if (player == null || !(trigger is BuildPrivilegeTrigger)) return;

            if (playerPrefs.ContainsKey(player.UserIDString) && !playerPrefs[player.UserIDString]) return;
            if (!permission.UserHasPermission(player.UserIDString, permBuild)) return;

            NextTick(() =>
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.InBuildingPrivilege, true);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.HasBuildingPrivilege, true);
            });
            if (logUsage) Log(Lang("MasterKeyUsed", null, player.displayName, player.UserIDString, player.transform.position));
        }*/

        #endregion Build Anywhere

        #region Lock Access

        private object CanUseLockedEntity(BasePlayer player, BaseLock @lock)
        {
            if (!@lock.IsLocked()) return null;
            if (playerPrefs.ContainsKey(player.UserIDString) && !playerPrefs[player.UserIDString]) return null;

            var prefab = @lock.parentEntity.Get(true).ShortPrefabName;
            if (prefab == null) return null;

            foreach (var type in lockableTypes)
            {
                if (!prefab.Contains(type)) continue;
                if (!permission.UserHasPermission(player.UserIDString, $"masterkey.{type}")) return null;

                if (showMessages) player.ChatMessage(Lang("OpenWith", player.UserIDString, type));
                if (logUsage) Log(Lang("LogOpen", null, PlayerName(player), type, player.transform.position));

                return true;
            }

            return null;
        }

        private object CanUnlock(BasePlayer player, BaseLock @lock)
        {
            if (player == null || @lock == null) return null;
            if (playerPrefs.ContainsKey(player.UserIDString) && !playerPrefs[player.UserIDString]) return null;

            var prefab = @lock.parentEntity.Get(true).ShortPrefabName;
            if (prefab == null) return null;

            var codeLock = @lock as CodeLock;

            foreach (var type in lockableTypes)
            {
                if (!prefab.Contains(type)) continue;
                if (!permission.UserHasPermission(player.UserIDString, $"masterkey.{type}")) return null;

                if (showMessages) player.ChatMessage(Lang("UnlockedWith", player.UserIDString, type));
                if (logUsage) Log(Lang("LogUnlock", null, PlayerName(player), type, player.transform.position));

                if (@lock != null) @lock.SetFlag(BaseEntity.Flags.Locked, false);
                if (player != null && codeLock != null) EffectNetwork.Send(new Effect(codeLock.effectUnlocked.resourcePath, player.transform.position, Vector3.zero));

                return false;
            }

            return null;
        }

        private object CanLock(BasePlayer player, BaseLock @lock)
        {
            if (player == null || @lock == null) return null;
            if (@lock.IsLocked()) return false;
            if (playerPrefs.ContainsKey(player.UserIDString) && !playerPrefs[player.UserIDString]) return null;

            var prefab = @lock.parentEntity.Get(true).ShortPrefabName;
            if (prefab == null) return null;

            var codeLock = @lock as CodeLock;

            foreach (var type in lockableTypes)
            {
                if (!prefab.Contains(type)) continue;
                if (!permission.UserHasPermission(player.UserIDString, $"masterkey.{type}")) return null;

                if (showMessages) player.ChatMessage(Lang("UnlockedWith", player.UserIDString, type));
                if (logUsage) Log(Lang("LogLock", null, PlayerName(player), type, player.transform.position));

                if (@lock != null) @lock.SetFlag(BaseEntity.Flags.Locked, true);
                if (player != null && codeLock != null) EffectNetwork.Send(new Effect(codeLock.effectLocked.resourcePath, player.transform.position, Vector3.zero));

                return false;
            }

            return null;
        }

        private object CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            if (player == null) return null;
            if (playerPrefs.ContainsKey(player.UserIDString) && !playerPrefs[player.UserIDString]) return null;

            var prefab = crate.ShortPrefabName;
            if (prefab == null) return null;

            foreach (var type in lockableTypes)
            {
                if (!prefab.Contains(type)) continue;
                if (!permission.UserHasPermission(player.UserIDString, $"masterkey.{type}")) return null;

                if (showMessages) player.ChatMessage(Lang("UnlockedWith", player.UserIDString, type));
                if (logUsage) Log(Lang("LogUnlock", null, PlayerName(player), type, player.transform.position));

                crate.SetFlag(BaseEntity.Flags.Reserved2, true, false);
                crate.isLootable = true;

                return false;
            }

            return null;
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null)
                return;

            if (input.current.buttons == 0)
                return;

            if (input.IsDown(BUTTON.USE))
            {
                if (input.IsDown(BUTTON.USE) && !input.WasDown(BUTTON.USE))
                {
                    var entity = GetEntity(player) as AutoTurret;
                    if (entity == null)
                        return;

                    if (playerPrefs.ContainsKey(player.UserIDString) && !playerPrefs[player.UserIDString]) return;

                    var prefab = entity.ShortPrefabName;
                    foreach (var type in lockableTypes)
                    {
                        if (!prefab.Contains(type)) continue;
                        if (!permission.UserHasPermission(player.UserIDString, $"masterkey.{type}")) return;

                        var client = GrabPlayer(player);
                        if (client == null)
                            return;

                        if (!entity.IsAuthed(player) && entity.IsOnline())
                        {
                            entity.authorizedPlayers.Add(client);

                            if (entity.HasTarget() && entity.target == player)
                                entity.SetTarget(null);

                            entity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

                            if (showMessages) player.ChatMessage(Lang("UnlockedWith", player.UserIDString, type));
                            if (logUsage) Log(Lang("LogUnlock", null, PlayerName(player), type, player.transform.position));
                        }
                        else if (entity.IsOnline())
                        {
                            entity.authorizedPlayers.RemoveAll((PlayerNameID x) => x.userid == player.userID);
                            entity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

                            if (showMessages) player.ChatMessage(Lang("LockedWith", player.UserIDString, type));
                            if (logUsage) Log(Lang("LogLock", null, PlayerName(player), type, player.transform.position));
                        }
                    }
                }
            }
        }

        private PlayerNameID GrabPlayer(BasePlayer player)
        {
            if (player == null)
                return null;

            PlayerNameID playerNameID = new PlayerNameID()
            {
                userid = player.userID,
                username = player.displayName
            };

            return playerNameID;
        }

        #endregion Lock Access

        #region Get Entity

        // Edited borrowed code - Not sure from where
        private BaseEntity GetEntity(BasePlayer player)
        {
            if (player == null || player.IsDead() || player.IsSleeping())
                return null;

            RaycastHit hit;

            var currentRot = Quaternion.Euler(player?.serverInput?.current?.aimAngles ?? Vector3.zero) * Vector3.forward;
            var ray = new Ray(player.eyes.position, currentRot);

            if (UnityEngine.Physics.Raycast(ray, out hit, 2f))
            {
                var entity = hit.GetEntity() ?? null;

                if (entity != null && !entity.IsDestroyed)
                    return entity;
            }
            return null;
        }

        #endregion Get Entity

        #region Helpers

        private string PlayerName(BasePlayer player) => $"{player.displayName}({player.UserIDString})";

        private T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void Log(string text) => LogToFile("usage", $"[{DateTime.Now}] {text}", this);

        #endregion Helpers
    }
}