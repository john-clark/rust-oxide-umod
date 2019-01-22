using UnityEngine;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Chute", "ColonBlow", "2.0.1", ResourceId = 2279)]
    class Chute : RustPlugin
    {
        // added console commands for chute and chutup
        #region Load

        void Loaded()
        {
            LoadVariables();
            LoadMessages();
            permission.RegisterPermission("chute.allowed", this);
            layerMask = (1 << 29);
            layerMask |= (1 << 18);
            layerMask = ~layerMask;
        }

        #endregion

        #region Configuration

        static LayerMask layerMask;
        static List<ulong> chutePlayerList = new List<ulong>();
        static List<ulong> chuteCooldownList = new List<ulong>();

        bool Changed;
        bool BlockDamageToPlayer = true;
        static float parachuteFromHeight = 1000f;
        static float parachuteFwdSpeed = 9f;
        static float parachuteDownSpeed = 5f;

        bool UseCooldown = true;
        static float ChuteCooldown = 600f;

        string SteamID;

        void LoadDefaultConfig()
        {
            Puts("Creating a new config file");
            Config.Clear();
            LoadVariables();
        }

        private void LoadConfigVariables()
        {
            CheckCfg("Block Damage to Player with Parachute On : ", ref BlockDamageToPlayer);
            CheckCfgFloat("Parachute from Height using ChuteUp : ", ref parachuteFromHeight);
            CheckCfgFloat("Parachute Movement Speed : ", ref parachuteFwdSpeed);
            CheckCfgFloat("Parachute Downward Speed : ", ref parachuteDownSpeed);
            CheckCfgFloat("Cooldown - After using a chute, time player must wait to use another : ", ref ChuteCooldown);
            CheckCfg("Cooldown - Use chute cooldown ? ", ref UseCooldown);
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
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

        #endregion

        #region Localization

        private void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["noperms"] = "You don't have permission to use this command.",
                ["notjumping"] = "You are not able to use that right now.",
                ["alreadyusedchute"] = "You have already used your chute for this jump... sorry...",
                ["undercooldown"] = "You must wait, you are under a cooldown",
                ["nomorecooldown"] = "Your Chute cooldown as been removed.",
                ["openchute"] = "Press your 'RELOAD' Key to open your chute when your ready !!!!"
            }, this);
        }

        #endregion

        #region Hooks

        bool isAllowed(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);

        public bool ListConstainsPlayerID(BasePlayer player)
        {
            if (chutePlayerList.Contains(player.userID)) return true;
            return false;
        }

        void AddPlayerID(BasePlayer player)
        {
            if (ListConstainsPlayerID(player)) return;
            chutePlayerList.Add(player.userID);
        }

        public void RemovePlayerID(BasePlayer player)
        {
            if (ListConstainsPlayerID(player))
            {
                chutePlayerList.Remove(player.userID);
                return;
            }
        }

        bool CooldownListConstainsPlayerID(BasePlayer player)
        {
            if (chuteCooldownList.Contains(player.userID)) return true;
            return false;
        }

        void CooldownAddPlayerID(BasePlayer player)
        {
            if (CooldownListConstainsPlayerID(player)) return;
            chuteCooldownList.Add(player.userID);
            float cooldown = ChuteCooldown;
            timer.Once(cooldown, () => { chuteCooldownList.Remove(player.userID); });
        }

        public void MovePlayerToPosition(BasePlayer player, Vector3 position, Quaternion rotation)
        {
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, false);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Unused2, false);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Unused1, false);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.transform.position = (position);
            player.transform.rotation = (rotation);
            player.StopWounded();
            player.StopSpectating();
            player.UpdateNetworkGroup();
            player.UpdatePlayerCollider(true);
            player.UpdatePlayerRigidbody(false);
            player.StartSleeping();
            player.SendNetworkUpdateImmediate(false);
            player.ClearEntityQueue(null);
            player.ClientRPCPlayer(null, player, "StartLoading");
            player.SendFullSnapshot();
        }

        public void AttachChute(BasePlayer player)
        {
            if (player == null) return;
            string chairprefab = "assets/prefabs/deployable/chair/chair.deployed.prefab";
            var chutemount = GameManager.server.CreateEntity(chairprefab, player.transform.position, Quaternion.identity, true);
            chutemount.enableSaving = false;
            var hasstab = chutemount.GetComponent<StabilityEntity>();
            if (hasstab) hasstab.grounded = true;
            var hasmount = chutemount.GetComponent<BaseMountable>();
            if (hasmount) hasmount.isMobile = true;
            chutemount.skinID = 1311472987;
            chutemount?.Spawn();
            if (chutemount != null)
            {
                var parachute = GameManager.server.CreateEntity("assets/prefabs/misc/parachute/parachute.prefab", new Vector3(), new Quaternion(), true);
                parachute.SetParent(chutemount, 0);
                parachute?.Spawn();

                var addchute = chutemount.gameObject.AddComponent<PlayerParachute>();
                hasmount.MountPlayer(player);
            }
            return;
        }

        private object OnPlayerLand(BasePlayer player, float num)
        {
            if (player == null) return null;
            if (ListConstainsPlayerID(player)) return true;
            return null;
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || input == null) return;
            if (ListConstainsPlayerID(player))
            {
                if (!player.isMounted)
                {
                    if (input.WasJustPressed(BUTTON.RELOAD))
                    {
                        AttachChute(player);
                        return;
                    }
                }
                if (player.isMounted)
                {
                    var haschute1 = player.GetMounted().GetComponentInParent<PlayerParachute>() ?? null;
                    if (haschute1)
                    {
                        haschute1.ChuteInput(input, player);
                    }
                    return;
                }
            }
        }

        private object OnPlayerViolation(BasePlayer player, AntiHackType type, float amount)
        {
            if (player == null) return null;
            if (ListConstainsPlayerID(player)) return true;
            return null;
        }

        void SendInfoMessage(BasePlayer player, string message, float time)
        {
            player?.SendConsoleCommand("gametip.showgametip", message);
            timer.Once(time, () => player?.SendConsoleCommand("gametip.hidegametip"));
        }

        private void OnEntityDismounted(BaseMountable mountable, BasePlayer player)
        {
            if (mountable == null || player == null) return;
            if (player.GetComponent<PlayerJumpController>())
            {
                SendInfoMessage(player, "Press your <color=black>[ R E L O A D ]</color> key to open parachute !!", 10f);
            }
            return;
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity == null || hitInfo == null) return;
            if (!BlockDamageToPlayer) return;
            if (entity is BasePlayer)
            {
                BasePlayer victim = (BasePlayer)entity as BasePlayer;
                if (ListConstainsPlayerID(victim))
                {
                    hitInfo.damageTypes.ScaleAll(0);
                }
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null) return;
            RemovePlayerID(player);
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null) return;
            RemovePlayerID(player);
        }

        private void OnPlayerDie(BasePlayer player, HitInfo info)
        {
            if (player == null) return;
            RemovePlayerID(player);
        }

        static void DestroyAll<T>()
        {
            var objects = GameObject.FindObjectsOfType(typeof(T));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
        }

        void Unload()
        {
            chutePlayerList.Clear();
            chuteCooldownList.Clear();
            DestroyAll<PlayerParachute>();
            DestroyAll<PlayerJumpController>();
        }

        #endregion

        #region API

        private void ExternalAddPlayerChute(BasePlayer player, string chutecolor)
        {
            SteamID = player.userID.ToString();
            AddPlayerID(player);
            var hascontroller = player.GetComponent<PlayerJumpController>();
            if (!hascontroller) player.gameObject.AddComponent<PlayerJumpController>();
            AttachChute(player);
        }

        #endregion

        #region Commands

        [ChatCommand("chute")]
        void chatChute(BasePlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.UserIDString, "chute.allowed"))
            {
                if (player.isMounted) { SendReply(player, "You are already Mounted"); return; }
                if (chuteCooldownList.Contains(player.userID)) { PrintToChat(player, lang.GetMessage("undercooldown", this, player.UserIDString)); return; }
                if (UseCooldown) { CooldownAddPlayerID(player); }
                AddPlayerID(player);
                var hascontroller = player.GetComponent<PlayerJumpController>();
                if (!hascontroller) player.gameObject.AddComponent<PlayerJumpController>();
                AttachChute(player);
            }
            else
                PrintToChat(player, lang.GetMessage("noperms", this, player.UserIDString));
        }

        [ConsoleCommand("chute")]
        void cmdConsoleChute(ConsoleSystem.Arg arg)
        {
            var player = arg.Player() ?? null;
            if (player != null)
            {
                if (permission.UserHasPermission(player.UserIDString, "chute.allowed"))
                {
                    if (player.isMounted) { SendReply(player, "You are already Mounted"); return; }
                    if (chuteCooldownList.Contains(player.userID)) { PrintToChat(player, lang.GetMessage("undercooldown", this, player.UserIDString)); return; }
                    if (UseCooldown) { CooldownAddPlayerID(player); }
                    AddPlayerID(player);
                    var hascontroller = player.GetComponent<PlayerJumpController>();
                    if (!hascontroller) player.gameObject.AddComponent<PlayerJumpController>();
                    AttachChute(player);
                }
                else
                    PrintToChat(player, lang.GetMessage("noperms", this, player.UserIDString));
                return;
            }
            if (arg.Args.Length > 0)
            {
                ulong id = Convert.ToUInt64(arg.Args[0]);
                BasePlayer idplayer = BasePlayer.FindByID(id);
                if (idplayer.isMounted) { SendReply(idplayer, "You are already Mounted"); return; }
                if (chuteCooldownList.Contains(idplayer.userID)) { PrintToChat(idplayer, lang.GetMessage("undercooldown", this, idplayer.UserIDString)); return; }
                if (UseCooldown) { CooldownAddPlayerID(idplayer); }
                AddPlayerID(idplayer);
                var hascontroller = idplayer.GetComponent<PlayerJumpController>();
                if (!hascontroller) idplayer.gameObject.AddComponent<PlayerJumpController>();
                AttachChute(idplayer);
            }
        }

        [ChatCommand("chuteup")]
        void chatChuteUp(BasePlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.UserIDString, "chute.allowed"))
            {
                if (player.isMounted) { SendReply(player, "You are already Mounted"); return; }
                if (chuteCooldownList.Contains(player.userID)) { PrintToChat(player, lang.GetMessage("undercooldown", this, player.UserIDString)); return; }
                if (UseCooldown) { CooldownAddPlayerID(player); }
                AddPlayerID(player);
                var hascontroller = player.GetComponent<PlayerJumpController>();
                if (!hascontroller) player.gameObject.AddComponent<PlayerJumpController>();
                MovePlayerToPosition(player, player.transform.position + (Vector3.up * parachuteFromHeight), player.transform.rotation);
                AttachChute(player);
            }
            else
                PrintToChat(player, lang.GetMessage("noperms", this, player.UserIDString));
        }

        [ConsoleCommand("chuteup")]
        void cmdConsoleChuteUp(ConsoleSystem.Arg arg)
        {
            var player = arg.Player() ?? null;
            if (player != null)
            {
                if (permission.UserHasPermission(player.UserIDString, "chute.allowed"))
                {
                    if (player.isMounted) { SendReply(player, "You are already Mounted"); return; }
                    if (chuteCooldownList.Contains(player.userID)) { PrintToChat(player, lang.GetMessage("undercooldown", this, player.UserIDString)); return; }
                    if (UseCooldown) { CooldownAddPlayerID(player); }
                    AddPlayerID(player);
                    var hascontroller = player.GetComponent<PlayerJumpController>();
                    if (!hascontroller) player.gameObject.AddComponent<PlayerJumpController>();
                    MovePlayerToPosition(player, player.transform.position + (Vector3.up * parachuteFromHeight), player.transform.rotation);
                    AttachChute(player);
                }
                else
                    PrintToChat(player, lang.GetMessage("noperms", this, player.UserIDString));
                return;
            }
            if (arg.Args.Length > 0)
            {
                ulong id = Convert.ToUInt64(arg.Args[0]);
                BasePlayer idplayer = BasePlayer.FindByID(id);
                if (idplayer.isMounted) { SendReply(idplayer, "You are already Mounted"); return; }
                if (chuteCooldownList.Contains(idplayer.userID)) { PrintToChat(idplayer, lang.GetMessage("undercooldown", this, idplayer.UserIDString)); return; }
                if (UseCooldown) { CooldownAddPlayerID(idplayer); }
                AddPlayerID(idplayer);
                var hascontroller = idplayer.GetComponent<PlayerJumpController>();
                if (!hascontroller) idplayer.gameObject.AddComponent<PlayerJumpController>();
                MovePlayerToPosition(idplayer, idplayer.transform.position + (Vector3.up * parachuteFromHeight), idplayer.transform.rotation);
                AttachChute(idplayer);
            }
        }

        #endregion

        #region Player Parachute Entity

        class PlayerParachute : BaseEntity
        {
            BaseEntity mount;
            Vector3 direction;
            Vector3 position;
            public bool moveforward;
            public bool rotright;
            public bool rotleft;

            void Awake()
            {
                mount = GetComponentInParent<BaseEntity>();
                if (mount == null) { OnDestroy(); return; }
                position = mount.transform.position;
                moveforward = false;
            }

            bool PlayerIsMounted()
            {
                bool flag = mount.GetComponent<BaseMountable>().IsMounted();
                return flag;
            }

            public void ChuteInput(InputState input, BasePlayer player)
            {
                if (input == null || player == null) return;
                if (input.WasJustPressed(BUTTON.FORWARD)) moveforward = true;
                if (input.WasJustReleased(BUTTON.FORWARD)) moveforward = false;
                if (input.WasJustPressed(BUTTON.RIGHT)) rotright = true;
                if (input.WasJustReleased(BUTTON.RIGHT)) rotright = false;
                if (input.WasJustPressed(BUTTON.LEFT)) rotleft = true;
                if (input.WasJustReleased(BUTTON.LEFT)) rotleft = false;
            }

            void FixedUpdate()
            {
                if (!PlayerIsMounted() || mount == null) { OnDestroy(); return; }
                if (Physics.Raycast(new Ray(mount.transform.position, Vector3.down), 3f, layerMask))
                {
                    OnDestroy();
                    return;
                }
                if (rotright) mount.transform.eulerAngles += new Vector3(0, 2, 0);
                else if (rotleft) mount.transform.eulerAngles += new Vector3(0, -2, 0);

                if (moveforward) mount.transform.localPosition += ((transform.forward * parachuteFwdSpeed) * Time.deltaTime);

                mount.transform.position = Vector3.MoveTowards(mount.transform.position, mount.transform.position + Vector3.down, (parachuteDownSpeed) * Time.deltaTime);
                mount.transform.hasChanged = true;
                mount.SendNetworkUpdateImmediate();
                mount.UpdateNetworkGroup();
            }

            public void OnDestroy()
            {
                if (mount != null) { mount.Invoke("KillMessage", 0.1f); }
            }
        }

        #endregion

        #region Player Jump Controller

        class PlayerJumpController : MonoBehaviour
        {
            BasePlayer player;
            public bool usedchute;
            Chute _instance;
            float dismountcounter;

            void Awake()
            {
                _instance = new Chute();
                player = GetComponentInParent<BasePlayer>() ?? null;
                if (player == null) { OnDestroy(); return; }
                usedchute = false;
                dismountcounter = 0f;
                player.ClearEntityQueue();
            }

            void FixedUpdate()
            {
                if (player == null) { OnDestroy(); return; }
                if (Physics.Raycast(new Ray(player.transform.position, Vector3.down), 10f, layerMask))
                {
                    OnDestroy();
                    return;
                }
            }

            public void OnDestroy()
            {
                if (chutePlayerList.Contains(player.userID))
                {
                    chutePlayerList.Remove(player.userID);
                }
                GameObject.Destroy(this);
            }
        }

        #endregion
    }
}