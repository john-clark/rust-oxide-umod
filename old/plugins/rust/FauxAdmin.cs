using System;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("FauxAdmin", "Colon Blow", "1.0.15", ResourceId = 1933)]
    class FauxAdmin : RustPlugin
    {

        #region Loadup

        void Loaded()
        {
            LoadVariables();
            if (DisableFlyHackProtection) ConVar.AntiHack.flyhack_protection = 0;
            if (DisableNoclipProtection) ConVar.AntiHack.noclip_protection = 0;
            lang.RegisterMessages(Messages, this);
            permission.RegisterPermission("fauxadmin.allowed", this);
            permission.RegisterPermission("fauxadmin.bypass", this);
            permission.RegisterPermission("fauxadmin.blocked", this);
        }

        bool isAllowed(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);

        #endregion

        #region Configuration

        bool Changed;
        static bool DisableFlyHackProtection = true;
        static bool DisableNoclipProtection = true;
        static bool DisableFauxAdminDemolish = true;
        static bool DisableFauxAdminRotate = true;
        static bool DisableFauxAdminUpgrade = true;
        static bool DisableNoclipOnNoBuild = true;
        static bool EntKillOwnOnly = true;
        static bool UseFauxAdminBanBlocker = true;

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        void LoadDefaultConfig()
        {
            Puts("Creating a new config file");
            Config.Clear();
            LoadVariables();
        }

        private void LoadConfigVariables()
        {
            CheckCfg("Disable Flyhack Protection on plugin load : ", ref DisableFlyHackProtection);
            CheckCfg("Disable NoClip Protection on plugin load : ", ref DisableNoclipProtection);
            CheckCfg("Disable FauxAdmin Ability to Demolish others building parts? ", ref DisableFauxAdminDemolish);
            CheckCfg("Disable FauxAdmin Ability to Rotate others building parts ? ", ref DisableFauxAdminRotate);
            CheckCfg("Disable FauxAdmin Ability to upgrade others building parts ? ", ref DisableFauxAdminUpgrade);
            CheckCfg("Disable FauxAdmins Noclip when not authorized on local tool cupboard ? ", ref DisableNoclipOnNoBuild);
            CheckCfg("Only allow FauxAdmins to use entkill in there own stuff ? ", ref EntKillOwnOnly);
            CheckCfg("Enable the FauxAdmin ban blocker (helps to catch false positives) ? ", ref UseFauxAdminBanBlocker);
        }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }

        private void CheckCfgFloat(string Key, ref float var)
        {

            if (Config[Key] != null)
                var = Convert.ToSingle(Config[Key]);
            else
                Config[Key] = var;
        }

        object GetConfig(string menu, string datavalue, object defaultValue)
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

        #region Localization

        string msg(string key, string playerId = null) => lang.GetMessage(key, this, playerId);

        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["restricted"] = "You are not allowed to noclip here.",
            ["notallowed"] = "You are not authorized to use that command !!"
        };

        #endregion

        #region EntKill

        BaseEntity baseEntity;
        RaycastHit RayHit;
        static int layermask = LayerMask.GetMask("Construction", "Deployed", "Default");

        [ConsoleCommand("entkill")]
        void cmdConsoleEntKill(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!isAllowed(player, "fauxadmin.allowed"))
            {
                SendReply(player, msg("notallowed", player.UserIDString));
                return;
            }
            EntKillProcess(player);
        }

        void EntKillProcess(BasePlayer player)
        {
            bool flag1 = Physics.Raycast(player.eyes.HeadRay(), out RayHit, 10f, layermask);
            baseEntity = flag1 ? RayHit.GetEntity() : null;
            if (baseEntity == null) return;
            if (baseEntity is BasePlayer) return;
            if (EntKillOwnOnly && player.userID != baseEntity.OwnerID) return;
            baseEntity.Kill(BaseNetworkable.DestroyMode.Gib);

        }

        #endregion

        #region EntWho

        [ConsoleCommand("entwho")]
        void cmdConsoleEntWho(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!isAllowed(player, "fauxadmin.allowed"))
            {
                SendReply(player, msg("notallowed", player.UserIDString));
                return;
            }
            EntWhoProcess(player);
        }

        void EntWhoProcess(BasePlayer player)
        {
            bool flag2 = Physics.Raycast(player.eyes.HeadRay(), out RayHit, 10f, layermask);
            baseEntity = flag2 ? RayHit.GetEntity() : null;
            if (baseEntity == null) return;
            SendReply(player, "Owner ID: " + baseEntity.OwnerID.ToString());
        }

        #endregion

        #region Noclip

        [ChatCommand("noclip")]
        void cmdChatnoclip(BasePlayer player, string command, string[] args)
        {
            if (player.net?.connection?.authLevel > 0)
            {
                rust.RunClientCommand(player, "noclip");
                return;
            }
            if (!isAllowed(player, "fauxadmin.allowed"))
            {
                SendReply(player, msg("notallowed", player.UserIDString));
                return;
            }
            if (isAllowed(player, "fauxadmin.allowed"))
            {
                rust.RunClientCommand(player, "noclip");
                return;
            }
            return;
        }

        #endregion

        #region Player Control

        class FauxControl : MonoBehaviour
        {
            BasePlayer player;
            FauxAdmin instance;

            static Dictionary<ulong, RestrictedData> _restricted = new Dictionary<ulong, RestrictedData>();

            class RestrictedData
            {
                public BasePlayer player;
            }

            void Awake()
            {
                player = GetComponentInParent<BasePlayer>();
                instance = new FauxAdmin();
            }

            void DeactivateNoClip(Vector3 pos)
            {
                if (player == null) return;
                if (_restricted.ContainsKey(player.userID)) return;
                instance.timer.Repeat(0.1f, 10, () => instance.ForcePlayerPosition(player, pos));

                _restricted.Add(player.userID, new RestrictedData
                {
                    player = player
                });
                instance.SendReply(player, instance.msg("restricted", player.UserIDString));
                instance.rust.RunClientCommand(player, "noclip");
                instance.timer.Once(1, () => _restricted.Remove(player.userID));
            }

            void FixedUpdate()
            {
                if (!DisableNoclipOnNoBuild) return;
                if (player.IsFlying)
                {
                    if (player.IsBuildingBlocked())
                    {
                        DeactivateNoClip(player.transform.position);
                    }
                }
            }

        }

        void OnPlayerInit(BasePlayer player)
        {
            if (isAllowed(player, "fauxadmin.blocked"))
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsDeveloper, false);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                return;
            }
            //if (player.net?.connection?.authLevel > 0) return;
            if (!isAllowed(player, "fauxadmin.allowed"))
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsDeveloper, false);
                return;
            }
            if (isAllowed(player, "fauxadmin.allowed"))
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsDeveloper, true);
                if (isAllowed(player, "fauxadmin.bypass")) return;
                player.gameObject.AddComponent<FauxControl>();
                return;
            }
            return;
        }

        #endregion

        #region Ban Blocker

        object OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg == null || arg.cmd == null) return null;
            string command = arg.cmd.Name;
            string reason = arg.GetString(1).ToString();
            if (command.Equals("ban") || command.Equals("banid"))
            {
                if (UseFauxAdminBanBlocker && reason.Equals("Cheat Detected!"))
                {
                    BasePlayer player = arg.GetPlayer(0);
                    if ((player) && isAllowed(player, "fauxadmin.allowed"))
                    {
                        PrintWarning($"FauxAdmin Ban Blocker stopped a ban of " + player.ToString() + " for " + reason);
                        return false;
                    }
                }
            }
            return null;
        }

        #endregion

        #region Structure Hooks

        object OnStructureDemolish(BuildingBlock block, BasePlayer player)
        {
            if (isAllowed(player, "fauxadmin.allowed"))
            {
                if (block.OwnerID == 0 || player.userID == 0) return null;
                if (block.OwnerID == player.userID) return null;
                if (block.OwnerID != player.userID && DisableFauxAdminDemolish)
                {
                    return true;
                }
            }
            return null;
        }

        object OnStructureRotate(BuildingBlock block, BasePlayer player)
        {
            if (isAllowed(player, "fauxadmin.allowed"))
            {
                if (block.OwnerID == 0 || player.userID == 0) return null;
                if (block.OwnerID == player.userID) return null;
                if (block.OwnerID != player.userID && DisableFauxAdminRotate)
                {
                    return true;
                }
            }
            return null;
        }

        object OnStructureUpgrade(BuildingBlock block, BasePlayer player)
        {
            if (isAllowed(player, "fauxadmin.allowed"))
            {
                if (block.OwnerID == 0 || player.userID == 0) return null;
                if (block.OwnerID == player.userID) return null;
                if (block.OwnerID != player.userID && DisableFauxAdminUpgrade)
                {
                    return true;
                }
            }
            return null;
        }

        #endregion

        void Unload()
        {
            DestroyAll<FauxControl>();
        }

        static void DestroyAll<T>()
        {
            var objects = GameObject.FindObjectsOfType(typeof(T));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
        }

	void OnPlayerDisconnected(BasePlayer player, string reason)
	{
		var hasFauxControl = player.GetComponent<FauxControl>();
		if (hasFauxControl != null)
		{
                	GameObject.Destroy(hasFauxControl);
		}
		return;
	}
    }
}