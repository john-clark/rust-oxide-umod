using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using Rust;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Vanish", "nivex", "0.7.2")]
    [Description("Allows players with permission to become truly invisible")]
    public class Vanish : RustPlugin
    {
        #region Configuration

        static Vanish ins;
        private Configuration config;

        public class Configuration
        {
            // TODO: Add config option to not show other vanished for custom group

            [JsonProperty(PropertyName = "Image URL for vanish icon (.png or .jpg)")]
            public string ImageUrlIcon { get; set; } = "http://i.imgur.com/Gr5G3YI.png";

            [JsonProperty(PropertyName = "Performance mode (true/false)")]
            public bool PerformanceMode { get; set; } = false;

            [JsonProperty(PropertyName = "Play sound effect (true/false)")]
            public bool PlaySoundEffect { get; set; } = true;

            [JsonProperty(PropertyName = "Show visual indicator (true/false)")]
            public bool ShowGuiIcon { get; set; } = true;

            [JsonProperty(PropertyName = "Vanish timeout (seconds, 0 to disable)")]
            public int VanishTimeout { get; set; } = 0;

            [JsonProperty(PropertyName = "Visible to admin (true/false)")]
            public bool VisibleToAdmin { get; set; } = false;

            //[JsonProperty(PropertyName = "Visible to moderators (true/false)")]
            //public bool VisibleToMods { get; set; } = false;

            [JsonProperty(PropertyName = "Command cooldown (seconds, 0 to disable)")]
            public int CommandCooldown { get; set; } = 0;

            [JsonProperty(PropertyName = "Daily limit (amount, 0 to disable)")]
            public int DailyLimit { get; set; } = 0;

            [JsonProperty(PropertyName = "Sound effect prefab")]
            public string DefaultEffect { get; set; } = "assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab";

            [JsonProperty(PropertyName = "Appear while wounded")]
            public bool AppearWhileWounded { get; set; } = false;

            [JsonProperty(PropertyName = "Appear while running")]
            public bool AppearWhileRunning { get; set; } = false;

            [JsonProperty(PropertyName = "Bypass Antihack")]
            public bool BypassAntihack { get; set; } = false;

            [JsonProperty(PropertyName = "Image Color")]
            public string ImageColor { get; set; } = "1 1 1 0.3";

            [JsonProperty(PropertyName = "Image AnchorMin")]
            public string ImageAnchorMin { get; set; } = "0.175 0.017";

            [JsonProperty(PropertyName = "Image AnchorMax")]
            public string ImageAnchorMax { get; set; } = "0.22 0.08";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            string configPath = $"{Interface.Oxide.ConfigDirectory}{Path.DirectorySeparatorChar}{Name}.json";
            PrintWarning($"Could not load a valid configuration file, creating a new configuration file at {configPath}");
            config = new Configuration();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion Configuration

        #region Localization

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CantDamageBuilds"] = "You can't damage buildings while vanished",
                ["CantHurtAnimals"] = "You can't hurt animals while vanished",
                ["CantHurtPlayers"] = "You can't hurt players while vanished",
                ["CantUseTeleport"] = "You can't teleport while vanished",
                ["CommandVanish"] = "vanish",
                ["NotAllowed"] = "Sorry, you can't use '{0}' right now",
                ["NotAllowedPerm"] = "You are missing permissions! ({0})",
                ["PlayersOnly"] = "Command '{0}' can only be used by a player",
                ["VanishDisabled"] = "You are no longer invisible!",
                ["VanishEnabled"] = "You have vanished from sight...",
                ["VanishTimedOut"] = "Vanish time limit reached!",
                ["Cooldown"] = "You must wait {0} seconds to use this command again!",
                ["DailyLimitReached"] = "Daily limit of {0} uses has been reached!",
                ["InvalidSoundPrefab"] = "Invalid sound effect prefab: {0}"
            }, this);
        }

        #endregion Localization

        #region Initialization

        private const string permAbilitiesInvulnerable = "vanish.abilities.invulnerable";
        private const string permAbilitiesTeleport = "vanish.abilities.teleport";
        private const string permAbilitiesHideWeapons = "vanish.abilities.hideweapons";
        private const string permDamageAnimals = "vanish.damage.animals";
        private const string permDamageBuildings = "vanish.damage.buildings";
        private const string permDamagePlayers = "vanish.damage.players";
        private const string permUse = "vanish.use";
        private bool soundEffectIsValid;

        long TimeStamp() => (DateTime.Now.Ticks - DateTime.Parse("01/01/1970 00:00:00").Ticks) / 10000000;

        private void Init()
        {
            ins = this;
            permission.RegisterPermission(permAbilitiesInvulnerable, this);
            permission.RegisterPermission(permAbilitiesTeleport, this);
            permission.RegisterPermission(permAbilitiesHideWeapons, this);
            permission.RegisterPermission(permDamageAnimals, this);
            permission.RegisterPermission(permDamageBuildings, this);
            permission.RegisterPermission(permDamagePlayers, this);
            permission.RegisterPermission(permUse, this);

            AddLocalizedCommand("CommandVanish", "VanishCommand");

            if (config.ImageUrlIcon == null)
            {
                config.ImageUrlIcon = "http://i.imgur.com/Gr5G3YI.png";
            }

            Unsubscribe();
        }

        private void Loaded()
        {
            soundEffectIsValid = !string.IsNullOrEmpty(config.DefaultEffect) && Prefab.DefaultManager.FindPrefab(config.DefaultEffect) != null;

            if (!string.IsNullOrEmpty(config.DefaultEffect) && !soundEffectIsValid && config.PlaySoundEffect)
            {
                Puts(Lang("InvalidSoundPrefab", null, config.DefaultEffect));
            }

            try
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            catch { }

            if (storedData == null)
                storedData = new StoredData();

            if (config.CommandCooldown <= 0)
                storedData.Cooldowns.Clear();

            if (config.DailyLimit <= 0)
                storedData.Limits.Clear();
        }

        private void OnServerInitialized()
        {
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                OnPlayerInit(basePlayer);
            }
        }

        private void OnServerSave() => timer.Once(5f, () => SaveData());

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);

        private void Subscribe()
        {
            if (config.PerformanceMode)
            {
                Unsubscribe(nameof(CanNetworkTo));
            }
            else if (Interface.CallHook("OnVanishNetwork") == null)
            {
                Subscribe(nameof(CanNetworkTo));
            }

            if (config.AppearWhileWounded)
            {
                Subscribe(nameof(OnPlayerRecover));
                Subscribe(nameof(OnPlayerWound));
            }

            if (config.BypassAntihack)
            {
                Subscribe(nameof(OnPlayerViolation));
            }

            Subscribe(nameof(CanBeTargeted));
            Subscribe(nameof(CanBradleyApcTarget));
            Subscribe(nameof(OnNpcPlayerTarget));
            Subscribe(nameof(OnNpcTarget));
            Subscribe(nameof(OnEntityTakeDamage));
            Subscribe(nameof(OnPlayerSleepEnded));
            Subscribe(nameof(OnPlayerLand));
        }

        private void UnsubscribeNetwork()
        {
            Unsubscribe(nameof(CanNetworkTo));
        }

        private void Unsubscribe()
        {
            Unsubscribe(nameof(CanNetworkTo));
            Unsubscribe(nameof(CanBeTargeted));
            Unsubscribe(nameof(CanBradleyApcTarget));
            Unsubscribe(nameof(OnNpcPlayerTarget));
            Unsubscribe(nameof(OnNpcTarget));
            Unsubscribe(nameof(OnEntityTakeDamage));
            Unsubscribe(nameof(OnPlayerSleepEnded));
            Unsubscribe(nameof(OnPlayerLand));
            Unsubscribe(nameof(OnPlayerRecover));
            Unsubscribe(nameof(OnPlayerWound));
            Unsubscribe(nameof(OnPlayerViolation));
        }

        #endregion Initialization

        #region Data Storage

        private class Runner : FacepunchBehaviour
        {
            private BasePlayer basePlayer;

            private void Awake()
            {
                basePlayer = GetComponent<BasePlayer>();
                InvokeRepeating(Repeater, 0f, 0.5f);
            }

            private void Repeater()
            {
                if (basePlayer == null || !basePlayer.IsConnected)
                {
                    GameObject.Destroy(this);
                    return;
                }

                if (basePlayer.IsRunning())
                {
                    if (ins.IsInvisible(basePlayer))
                    {
                        ins.Reappear(basePlayer);
                    }
                }
                else
                {
                    if (!ins.IsInvisible(basePlayer))
                    {
                        ins.Disappear(basePlayer);
                    }
                }
            }

            private void OnDestroy()
            {
                CancelInvoke(Repeater);
                GameObject.Destroy(this);
            }
        }

        private class WeaponBlock : FacepunchBehaviour
        {
            private BasePlayer basePlayer;
            private uint svActiveItemID;

            private void Awake()
            {
                basePlayer = GetComponent<BasePlayer>();
                svActiveItemID = basePlayer.svActiveItemID;
                InvokeRepeating(Repeater, 0f, 0.1f);
            }

            private void Repeater()
            {
                if (basePlayer == null || !basePlayer.IsConnected)
                {
                    GameObject.Destroy(this);
                    return;
                }

                var activeItem = basePlayer.svActiveItemID;

                if (activeItem == svActiveItemID)
                {
                    return;
                }

                svActiveItemID = activeItem;
                if (ins.IsInvisible(basePlayer))
                {
                    HeldEntity heldEntity = basePlayer.GetHeldEntity();
                    if (heldEntity != null)
                    {
                        heldEntity.SetHeld(false);
                    }
                }
            }

            private void OnDestroy()
            {
                CancelInvoke(Repeater);
                GameObject.Destroy(this);
            }
        }

        private class OnlinePlayer
        {
            public BasePlayer Player;
            public bool IsInvisible;
        }

        [OnlinePlayers]
        private Hash<BasePlayer, OnlinePlayer> onlinePlayers = new Hash<BasePlayer, OnlinePlayer>();

        private Dictionary<ulong, Timer> timers = new Dictionary<ulong, Timer>();
        private static StoredData storedData = new StoredData();
        private class StoredData
        {
            public class Limit
            {
                public string Date;
                public int Uses;

                public Limit()
                {
                }
            }

            public List<ulong> Invisible = new List<ulong>();
            public Dictionary<string, long> Cooldowns = new Dictionary<string, long>();
            public Dictionary<string, Limit> Limits = new Dictionary<string, Limit>();

            public StoredData()
            {
            }
        }

        #endregion Data Storage

        #region Public Helpers

        public void _Disappear(BasePlayer basePlayer) => Disappear(basePlayer);
        public void _Reappear(BasePlayer basePlayer) => Reappear(basePlayer);
        public void _VanishGui(BasePlayer basePlayer) => VanishGui(basePlayer);
        public bool _IsInvisible(BasePlayer basePlayer) => IsInvisible(basePlayer);

        #endregion

        #region Commands

        private void VanishCommand(IPlayer player, string command, string[] args)
        {
            BasePlayer basePlayer = player.Object as BasePlayer;
            if (basePlayer == null)
            {
                player.Reply(Lang("PlayersOnly", player.Id, command));
                return;
            }

            if (!player.HasPermission(permUse))
            {
                Message(player, Lang("NotAllowedPerm", player.Id, permUse));
                return;
            }

            if (config.DailyLimit > 0)
            {
                if (!storedData.Limits.ContainsKey(player.Id))
                {
                    storedData.Limits.Add(player.Id, new StoredData.Limit());
                    var limit = storedData.Limits[player.Id];
                    limit.Date = DateTime.Now.ToString();
                    limit.Uses = 0;
                }
                else
                {
                    var limit = storedData.Limits[player.Id];

                    if (DateTime.Parse(limit.Date).Day < DateTime.Now.Day)
                    {
                        limit.Date = DateTime.Now.ToString();
                        limit.Uses = 0;
                    }
                    else if (++limit.Uses > config.DailyLimit && !IsInvisible(basePlayer))
                    {
                        Message(player, Lang("DailyLimitReached", player.Id, config.DailyLimit));
                        return;
                    }
                }
            }

            long stamp = TimeStamp();

            if (config.CommandCooldown > 0 && storedData.Cooldowns.ContainsKey(player.Id) && !IsInvisible(basePlayer))
            {
                if (storedData.Cooldowns[player.Id] - stamp > 0)
                {
                    Message(player, Lang("Cooldown", player.Id, storedData.Cooldowns[player.Id] - stamp));
                    return;
                }

                storedData.Cooldowns.Remove(player.Id);
            }

            if (config.PlaySoundEffect && soundEffectIsValid)
            {
                Effect.server.Run(config.DefaultEffect, basePlayer.transform.position);
            }

            if (IsInvisible(basePlayer))
            {
                Reappear(basePlayer);
            }
            else
            {
                Disappear(basePlayer);
            }

            if (config.CommandCooldown > 0 && !storedData.Cooldowns.ContainsKey(player.Id))
            {
                storedData.Cooldowns.Add(player.Id, stamp + config.CommandCooldown);
            }

            if (config.DailyLimit > 0 && storedData.Limits.ContainsKey(player.Id) && IsInvisible(basePlayer))
            {
                storedData.Limits[player.Id].Uses++;
            }
        }

        #endregion Commands

        #region Vanishing Act

        private void Disappear(BasePlayer basePlayer, bool showNotification = true)
        {
            if (Interface.CallHook("OnVanishDisappear", basePlayer) != null)
            {
                return;
            }

            if (onlinePlayers[basePlayer] == null)
            {
                onlinePlayers[basePlayer] = new OnlinePlayer();
            }

            List<Connection> connections = new List<Connection>();
            foreach (BasePlayer target in BasePlayer.activePlayerList)
            {
                if (basePlayer == target || !target.IsConnected || config.VisibleToAdmin && target.IPlayer.IsAdmin)
                {
                    continue;
                }

                connections.Add(target.net.connection);
            }

            if (config.PerformanceMode)
            {
                basePlayer.limitNetworking = true;
            }

            HeldEntity heldEntity = basePlayer.GetHeldEntity();
            if (heldEntity != null)
            {
                heldEntity.SetHeld(false);
                heldEntity.UpdateVisiblity_Invis();
                heldEntity.SendNetworkUpdate();
            }

            if (Net.sv.write.Start())
            {
                Net.sv.write.PacketID(Network.Message.Type.EntityDestroy);
                Net.sv.write.EntityID(basePlayer.net.ID);
                Net.sv.write.UInt8((byte)BaseNetworkable.DestroyMode.None);
                Net.sv.write.Send(new SendInfo(connections));
            }

            basePlayer.UpdatePlayerCollider(false);

            onlinePlayers[basePlayer].IsInvisible = true;

            if (basePlayer.GetComponent<WeaponBlock>() == null && basePlayer.IPlayer.HasPermission(permAbilitiesHideWeapons))
            {
                basePlayer.gameObject.AddComponent<WeaponBlock>();
            }

            if (config.AppearWhileRunning && basePlayer.GetComponent<Runner>() == null)
            {
                basePlayer.gameObject.AddComponent<Runner>();
            }

            if (config.VanishTimeout > 0f)
            {
                ulong userId = basePlayer.userID;

                if (timers.ContainsKey(userId))
                {
                    timers[userId].Reset();
                }
                else
                {
                    timers.Add(userId, timer.Once(config.VanishTimeout, () =>
                    {
                        if (basePlayer != null && basePlayer.IsConnected && IsInvisible(basePlayer))
                        {
                            Reappear(basePlayer);
                            Message(basePlayer.IPlayer, "VanishTimedOut");
                        }

                        timers.Remove(userId);
                    }));
                }
            }

            if (!storedData.Invisible.Contains(basePlayer.userID))
            {
                storedData.Invisible.Add(basePlayer.userID);
            }

            Subscribe();

            if (config.ShowGuiIcon)
            {
                VanishGui(basePlayer);
            }

            if (showNotification)
            {
                Message(basePlayer.IPlayer, "VanishEnabled");
            }
        }

        // Hide from other players
        private object CanNetworkTo(BaseNetworkable entity, BasePlayer target)
        {
            var basePlayer = entity as BasePlayer ?? (entity as HeldEntity)?.GetOwnerPlayer();
            if (basePlayer == null || target == null || basePlayer == target || config.VisibleToAdmin && target.IsAdmin)
            {
                return null;
            }

            if (IsInvisible(basePlayer))
            {
                return false;
            }

            return null;
        }

        // Hide from helis/turrets
        private object CanBeTargeted(BaseCombatEntity entity)
        {
            BasePlayer basePlayer = entity as BasePlayer;
            if (IsInvisible(basePlayer))
            {
                return false;
            }

            return null;
        }

        // Hide from the bradley APC
        private object CanBradleyApcTarget(BradleyAPC apc, BaseEntity entity)
        {
            BasePlayer basePlayer = entity as BasePlayer;
            if (IsInvisible(basePlayer))
            {
                return false;
            }

            return null;
        }

        // Hide from the patrol helicopter
        private object CanHelicopterTarget(PatrolHelicopterAI heli, BasePlayer basePlayer)
        {
            if (IsInvisible(basePlayer))
            {
                return false;
            }

            return null;
        }

        // Hide from scientist NPCs
        private object OnNpcPlayerTarget(NPCPlayerApex npc, BaseEntity entity)
        {
            BasePlayer basePlayer = entity as BasePlayer;
            if (IsInvisible(basePlayer))
            {
                return true; // Cancel, not a bool hook
            }

            return null;
        }

        /*private object OnNpcPlayerTarget(HTNPlayer htnPlayer, BasePlayer basePlayer)
        {
            if (IsInvisible(basePlayer))
            {
                return true; // Cancel, not a bool hook
            }

            return null;
        }*/

        // Hide from all other NPCs
        private object OnNpcTarget(BaseNpc npc, BaseEntity entity)
        {
            BasePlayer basePlayer = entity as BasePlayer;
            if (IsInvisible(basePlayer))
            {
                return true; // Cancel, not a bool hook
            }

            return null;
        }

        // Disappear when waking up if vanished
        private void OnPlayerSleepEnded(BasePlayer basePlayer)
        {
            if (!basePlayer || !basePlayer.IsConnected)
            {
                return;
            }

            if (IsInvisible(basePlayer))
            {
                if (basePlayer.IPlayer.HasPermission(permUse))
                {
                    Disappear(basePlayer);
                }
                else
                {
                    Reappear(basePlayer);
                }
            }
            else if (storedData.Invisible.Contains(basePlayer.userID))
            {
                if (basePlayer.IPlayer.HasPermission(permUse))
                {
                    Disappear(basePlayer);
                }
                else
                {
                    storedData.Invisible.Remove(basePlayer.userID);
                }
            }
        }

        // Prevent sound on player landing
        private object OnPlayerLand(BasePlayer player, float num)
        {
            if (IsInvisible(player))
            {
                return true; // Cancel, not a bool hook
            }

            return null;
        }

        // Prevent hostility
        private object CanEntityBeHostile(BasePlayer player)
        {
            if (IsInvisible(player))
            {
                return false;
            }

            return null;
        }

        // Cancel hostility
        private object OnEntityMarkHostile(BasePlayer player)
        {
            if (IsInvisible(player))
            {
                return true;
            }

            return null;
        }

        private object CanUseLockedEntity(BasePlayer player, BaseLock baseLock)
        {
            if (IsInvisible(player))
            {
                // Allow vanished players to open boxes while vanished without noise
                if (permission.UserHasPermission(player.UserIDString, permAbilitiesInvulnerable) || player.IsImmortal())
                {
                    return true;
                }

                // Don't make lock sound if you're not authed while vanished
                CodeLock codeLock = baseLock as CodeLock;
                if (codeLock != null)
                {
                    if (!codeLock.whitelistPlayers.Contains(player.userID) && !codeLock.guestPlayers.Contains(player.userID))
                    {
                        return false;
                    }
                }
            }

            return null;
        }

        void OnPlayerWound(BasePlayer player)
        {
            if (IsInvisible(player))
            {
                Reappear(player);
            }
        }

        void OnPlayerRecover(BasePlayer player)
        {
            if (storedData.Invisible.Contains(player.userID))
            {
                Disappear(player);
            }
        }

        object OnPlayerViolation(BasePlayer player, AntiHackType type, float amount)
        {
            if (IsInvisible(player))
            {
                return false;
            }

            return null;
        }

        #endregion Vanishing Act

        #region Reappearing Act

        private void Reappear(BasePlayer basePlayer)
        {
            if (onlinePlayers[basePlayer] != null)
            {
                onlinePlayers[basePlayer].IsInvisible = false;

                if (basePlayer.GetComponent<WeaponBlock>() != null)
                {
                    GameObject.Destroy(basePlayer.GetComponent<WeaponBlock>());
                }

                if (basePlayer.GetComponent<Runner>() != null)
                {
                    GameObject.Destroy(basePlayer.GetComponent<Runner>());
                }
            }
            basePlayer.SendNetworkUpdate();
            basePlayer.limitNetworking = false;

            HeldEntity heldEnity = basePlayer.GetHeldEntity();
            if (heldEnity != null)
            {
                heldEnity.UpdateVisibility_Hand();
                heldEnity.SendNetworkUpdate();
            }

            basePlayer.UpdatePlayerCollider(true);

            string gui;
            if (guiInfo.TryGetValue(basePlayer.userID, out gui))
            {
                CuiHelper.DestroyUi(basePlayer, gui);
            }

            Message(basePlayer.IPlayer, "VanishDisabled");
            if (onlinePlayers.Values.Count(p => p.IsInvisible) <= 0)
            {
                Unsubscribe(nameof(CanNetworkTo));
            }
            Interface.CallHook("OnVanishReappear", basePlayer);
            storedData.Invisible.Remove(basePlayer.userID);
        }

        #endregion Reappearing Act

        #region GUI Indicator

        private readonly Dictionary<ulong, string> guiInfo = new Dictionary<ulong, string>();

        private void VanishGui(BasePlayer basePlayer)
        {
            if (!basePlayer || !basePlayer.IsConnected || !IsInvisible(basePlayer))
            {
                return;
            }

            if (basePlayer.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.Once(0.1f, () => VanishGui(basePlayer));
                return;
            }

            string gui;
            if (guiInfo.TryGetValue(basePlayer.userID, out gui))
            {
                CuiHelper.DestroyUi(basePlayer, gui);
            }

            CuiElementContainer elements = new CuiElementContainer();
            guiInfo[basePlayer.userID] = CuiHelper.GetGuid();

            elements.Add(new CuiElement
            {
                Name = guiInfo[basePlayer.userID],
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Color = config.ImageColor,
                        Url = config.ImageUrlIcon
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = config.ImageAnchorMin,
                        AnchorMax = config.ImageAnchorMax
                    }
                }
            });

            CuiHelper.AddUi(basePlayer, elements);
        }

        #endregion GUI Indicator

        #region Damage Blocking

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            // Check if victim or attacker is a player
            BasePlayer basePlayer = info?.Initiator as BasePlayer ?? entity as BasePlayer;
            if (basePlayer == null || !basePlayer.IsConnected)
            {
                return null;
            }

            // Check if player is invisible
            if (!IsInvisible(basePlayer))
            {
                return null;
            }

            IPlayer player = basePlayer.IPlayer;

            // Block damage to animals
            if (entity is BaseNpc)
            {
                if (player.HasPermission(permDamageAnimals))
                {
                    return null;
                }

                Message(player, "CantHurtAnimals");
                return true;
            }

            // Block damage to buildings
            if (!(entity is BasePlayer))
            {
                if (player.HasPermission(permDamageBuildings))
                {
                    return null;
                }

                Message(player, "CantDamageBuilds");
                return true;
            }

            // Block damage to players
            if (info?.Initiator is BasePlayer)
            {
                if (player.HasPermission(permDamagePlayers))
                {
                    return null;
                }

                Message(player, "CantHurtPlayers");
                return true;
            }

            // Block damage to self
            if (basePlayer == info?.HitEntity)
            {
                if (player.HasPermission(permAbilitiesInvulnerable))
                {
                    info.damageTypes = new DamageTypeList();
                    info.HitMaterial = 0;
                    info.PointStart = Vector3.zero;
                    return true;
                }
            }

            return null;
        }

        #endregion Damage Blocking

        #region Teleport Blocking

        private object CanTeleport(BasePlayer basePlayer)
        {
            // Ignore for normal teleport plugins
            if (onlinePlayers[basePlayer] == null || !onlinePlayers[basePlayer].IsInvisible)
            {
                return null;
            }

            bool canTeleport = basePlayer.IPlayer.HasPermission(permAbilitiesTeleport);
            return !canTeleport ? Lang("CantUseTeleport", basePlayer.UserIDString) : null;
        }

        #endregion Teleport Blocking

        #region Persistence Handling

        private void OnPlayerInit(BasePlayer basePlayer)
        {
            if (!basePlayer || !basePlayer.IsConnected)
                return;

            if (storedData.Invisible.Contains(basePlayer.userID))
            {
                if (basePlayer.IPlayer.HasPermission(permUse))
                {
                    Disappear(basePlayer, false);
                }
            }
        }

        #endregion Persistence Handling

        #region Cleanup

        private void Unload()
        {
            if (!Interface.Oxide.IsShuttingDown)
            {
                foreach (BasePlayer basePlayer in BasePlayer.activePlayerList.Where(p => p != null))
                {
                    string gui;
                    if (guiInfo.TryGetValue(basePlayer.userID, out gui))
                    {
                        CuiHelper.DestroyUi(basePlayer, gui);
                    }
                }

                var blocks = UnityEngine.Object.FindObjectsOfType(typeof(WeaponBlock));

                if (blocks != null)
                {
                    foreach (var gameObj in blocks)
                    {
                        UnityEngine.Object.Destroy(gameObj);
                    }
                }

                var runners = UnityEngine.Object.FindObjectsOfType(typeof(Runner));

                if (runners != null)
                {
                    foreach (var gameObj in runners)
                    {
                        UnityEngine.Object.Destroy(gameObj);
                    }
                }
            }

            foreach (var entry in storedData.Limits.ToList())
            {
                if (DateTime.Parse(entry.Value.Date).Day < DateTime.Now.Day)
                {
                    storedData.Limits.Remove(entry.Key);
                }
            }

            SaveData();
        }

        #endregion Cleanup

        #region Helpers

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void AddLocalizedCommand(string key, string command)
        {
            foreach (string language in lang.GetLanguages(this))
            {
                Dictionary<string, string> messages = lang.GetMessages(language, this);
                foreach (KeyValuePair<string, string> message in messages.Where(m => m.Key.Equals(key)))
                {
                    if (!string.IsNullOrEmpty(message.Value))
                    {
                        AddCovalenceCommand(message.Value, command);
                    }
                }
            }
        }

        private void Message(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));

        private bool IsInvisible(BasePlayer player) => player != null && (onlinePlayers[player]?.IsInvisible ?? false);

        #endregion Helpers
    }
}
