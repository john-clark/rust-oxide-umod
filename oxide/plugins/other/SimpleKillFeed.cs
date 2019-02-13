using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Facepunch;
using Facepunch.Math;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;
using ProtoBuf;

namespace Oxide.Plugins
{
    [Info("Simple Kill Feed", "CosaNosatra", "1.4.9")]
    [Description("A simple kill feed, that displays in the top right corner various kill events.")]
    public class SimpleKillFeed : RustPlugin
    {
        #region Fields

        private readonly Dictionary<uint, string> _itemNameMapping = new Dictionary<uint, string>();
        private GameObject _holder;
        private KillQueue _killQueue;
        private static SKFConfig _config;
        private static SimpleKillFeedData _data;

        #endregion

        #region Config

        private class SKFConfig
        {
            [JsonProperty("Show suicides in KillFeed (Default: true)")]
            public bool EnableSuicides;
            [JsonProperty("Show radiation kills in KillFeed (Default: true)")]
            public bool EnableRadiationKills;
            [JsonProperty("Chat Icon Id (Steam profile ID)")]
            public ulong IconId;
            [JsonProperty("Max messages in feed (Default: 5)")]
            public int MaxFeedMessages;
            [JsonProperty("Max player name length in feed (Default: 18)")]
            public int MaxPlayerNameLength;
            [JsonProperty("Feed message TTL in seconds (Default: 7)")]
            public int FeedMessageTtlSec;
            [JsonProperty("Allow kill messages in chat along with kill feed")]
            public bool EnableChatFeed;
            [JsonProperty("Height ident (space between messages). Default: 0.0185")]
            public float HeightIdent;
            [JsonProperty("Feed Position - Anchor Max. (Default: 0.995 0.986")]
            public string AnchorMax;
            [JsonProperty("Feed Position - Anchor Min. (Default: 0.723 0.964")]
            public string AnchorMin;
            [JsonProperty("Font size of kill feed (Default: 12)")]
            public int FontSize;
            [JsonProperty("Outline Text Size (Default: 0.5 0.5)")]
            public string OutlineSize;
            [JsonProperty("Default color for distance (if too far from any from the list). Default: #FF8000")]
            public string DefaultDistanceColor;
            [JsonProperty("Distance Colors List (Certain color will apply if distance is <= than specified)")]
            public DistanceColor[] DistanceColors;
            [JsonProperty("Remove Entitys that should not shown in KillFeed")]
            public List<string> Ents = new List<string>();
            [JsonProperty("Custom Weapon Names, you can add more!")]
            public Dictionary<string, string> Weapons = new Dictionary<string, string>();

            [OnDeserialized]
            internal void OnDeserialized(StreamingContext ctx) => Array.Sort(DistanceColors, (o1, o2) => o1.DistanceThreshold.CompareTo(o2.DistanceThreshold));

            public class DistanceColor
            {
                public int DistanceThreshold;
                public string Color;

                public bool TestDistance(int distance) => distance <= DistanceThreshold;
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<SKFConfig>();
            if (_config.AnchorMax != null && _config.AnchorMin != null && _config.OutlineSize != null) return;
            _config.AnchorMax = "0.995 0.986";
            _config.AnchorMin = "0.723 0.964";
            _config.OutlineSize = "0.5 0.5";
            SaveConfig();
            PrintWarning("Config got updated! New entries are added.");
        }

        protected override void LoadDefaultConfig()
        {
            _config = new SKFConfig
            {
                EnableSuicides = true,
                EnableRadiationKills = true,
                IconId = 76561197960839785UL,
                MaxFeedMessages = 5,
                MaxPlayerNameLength = 18,
                FeedMessageTtlSec = 7,
                EnableChatFeed = true,
                HeightIdent = 0.0185f,
                AnchorMax = "0.995 0.986",
                AnchorMin = "0.723 0.964",
                FontSize = 12,
                OutlineSize = "0.5 0.5",
                DefaultDistanceColor = "#FF8000",
                DistanceColors = new[]
                {
                    new SKFConfig.DistanceColor
                    {
                        Color = "#FFFFFF",
                        DistanceThreshold = 50
                    },
                    new SKFConfig.DistanceColor
                    {
                        Color = "#91D6FF",
                        DistanceThreshold = 100
                    },
                    new SKFConfig.DistanceColor
                    {
                        Color = "#FFFF00",
                        DistanceThreshold = 150
                    }
                },
                Ents = new List<string>()
                {
                    "AutoTurret",
                    "FlameTurret",
                    "GunTrap",
                    "Landmine",
                    "BearTrap",
                    "BaseHelicopter",
                    "BradleyAPC"
                },
                Weapons = new Dictionary<string, string>()
                {
                    { "Assault Rifle","Ak-47" },
                    { "LR-300 Assault Rifle","LR-300" },
                    { "L96 Rifle","L96" },
                    { "Bolt Action Rifle","Bolt" },
                    { "Semi-Automatic Rifle","Semi-AR" },
                    { "Semi-Automatic Pistol","Semi-AP" },
                    { "Spas-12 Shotgun","Spas-12" },
                    { "M92 Pistol","M92" }
                }
            };
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion

        #region Data (ProtoBuf)

        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        private class SimpleKillFeedData
        {
            public List<ulong> DisabledUsers = new List<ulong>();
        }

        private void LoadData()
        {
            _data = ProtoStorage.Load<SimpleKillFeedData>(nameof(SimpleKillFeed)) ?? new SimpleKillFeedData();
        }

        private void SaveData()
        {
            if (_data == null)
                return;
            ProtoStorage.Save(_data, nameof(SimpleKillFeed));
        }

        #endregion

        #region ChatCommand

        [ChatCommand("feed")]
        private void ToggleFeed(BasePlayer player)
        {
            if (!_data.DisabledUsers.Contains(player.userID))
            {
                _data.DisabledUsers.Add(player.userID);
                Player.Message(player, _("Disabled", player), null, _config.IconId);
            }
            else
            {
                _data.DisabledUsers.Remove(player.userID);
                Player.Message(player, _("Enabled", player), null, _config.IconId);
            }
        }

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            foreach (var blueprint in ItemManager.bpList.Where(bp => bp.targetItem.category == ItemCategory.Weapon || bp.targetItem.category == ItemCategory.Tool))
            {
                var md = blueprint.targetItem.GetComponent<ItemModEntity>();
                if (!md)
                    continue;
                if (!_itemNameMapping.ContainsKey(md.entityPrefab.resourceID))
                    _itemNameMapping.Add(md.entityPrefab.resourceID, blueprint.targetItem.displayName.english);
            }
            _holder = new GameObject("SKFHolder");
            UnityEngine.Object.DontDestroyOnLoad(_holder);
            _killQueue = _holder.AddComponent<KillQueue>();
            Pool.FillBuffer<KillEvent>(_config.MaxFeedMessages);
        }

        private void Init() => LoadData();

        private void Unload()
        {
            _killQueue = null;
            UnityEngine.Object.Destroy(_holder);
            _holder = null;
            for (var i = 0; i < _config.MaxFeedMessages; i++)
                KillQueue.RemoveKillCui($"kf-{i}");
            _config = null;
            Pool.directory.Remove(typeof(KillEvent));
            SaveData();
        }

        private void OnPlayerDie(BasePlayer victim, HitInfo hitInfo)
        {
            if (victim == null || victim.IsNpc)
                return;

            if (hitInfo == null)
            {
                var wAttacker = victim.lastAttacker?.ToPlayer();
                if (wAttacker != null && victim.IsWounded() && !wAttacker.IsNpc)
                    OnDeathFromWounds(wAttacker, victim);
                return;
            }

            if (IsTrap(hitInfo.Initiator))
            {
                OnKilledByEnt(hitInfo.Initiator, victim, hitInfo);
                return;
            }

            if (IsRadiation(hitInfo))
            {
                if (!_config.EnableRadiationKills)
                    return;
                OnKilledByRadiation(victim);
                return;
            }

            var attacker = hitInfo.InitiatorPlayer;
            if (attacker == null || attacker.IsNpc)
                return;

            if (attacker == victim)
            {
                if (!_config.EnableSuicides)
                    return;
                OnSuicide(victim);
                return;
            }

            var distance = !hitInfo.IsProjectile() ? (int)Vector3.Distance(hitInfo.PointStart, hitInfo.HitPositionWorld) : (int)hitInfo.ProjectileDistance;

            if (IsExplosion(hitInfo))
                OnExploded(attacker, victim);
            else if (IsFlame(hitInfo))
                OnBurned(attacker, victim);
            else
                OnKilled(attacker, victim, hitInfo, distance);
        }

        #endregion

        #region Oxide Lang

        protected override void LoadDefaultMessages() => lang.RegisterMessages(new Dictionary<string, string>
        {
            {"MsgAttacker", "You killed <color=#ff686b>{0}</color> from {1}m in <color=#ff686b>{2}</color>."},
            {"MsgVictim", "<color=#ff686b>{0}</color> killed you from {1}m with their {2} to <color=#ff686b>{3}</color>."},
            {"MsgFeedKill", "<color=#00ff00>{0}</color> killed <color=#ff686b>{1}</color>, <color=#ff686b>{2}</color>, <color=#ff686b>{3}</color><color={4}>({5}m)</color>"},

            {"MsgAtkWounded", "You wounded <color=#ff686b>{0}</color> till death."},
            {"MsgVictimWounded", "<color=#ff686b>{0}</color> has wounded you till death."},
            {"MsgFeedWounded", "<color=#00ff00>{0}</color> finished <color=#ff686b>{1}</color>"},

            {"MsgAttackerBlown", "You blown <color=#ff686b>{0}</color>!"},
            {"MsgVictimBlown", "<color=#ff686b>{0}</color> has blown you!"},
            {"MsgFeedBlown", "<color=#00ff00>{0}</color> blown up <color=#ff686b>{1}</color>"},

            {"MsgAtkBurned", "You burned <color=#ff686b>{0}</color> alive!"},
            {"MsgVictimBurned", "<color=#ff686b>{0}</color> has burned you alive!"},
            {"MsgFeedBurned", "<color=#00ff00>{0}</color> burned <color=#ff686b>{1}</color>"},

            {"MsgFeedKillBaseHelicopter", "<color=#ff686b>{0}</color> was killed by <color=orange>Helicopter</color>"},
            {"MsgFeedKillBradleyAPC", "<color=#ff686b>{0}</color> was killed by <color=orange>APC</color>"},

            {"MsgFeedKillAutoTurret", "<color=#ff686b>{0}</color> was killed by <color=orange>Auto Turret</color>"},
            {"MsgFeedKillFlameTurret", "<color=#ff686b>{0}</color> got burned down by a <color=orange>Flame Turret</color>"},
            {"MsgFeedKillGunTrap", "<color=#ff686b>{0}</color> was killed by <color=orange>Shotgun Trap</color>"},
            {"MsgFeedKillBearTrap", "<color=#ff686b>{0}</color> was killed by <color=orange>Bear Trap</color>"},
            {"MsgFeedKillLandmine", "<color=#ff686b>{0}</color> got blown up by a <color=orange>Landmine</color>"},

            {"MsgFeedKillSuicide", "<color=#ff686b>{0}</color> committed <color=orange>Suicide</color>"},
            {"MsgFeedKillRadiation", "<color=#ff686b>{0}</color> died to <color=orange>Radiation</color>"},

            {"Enabled", "KillFeed Enabled"},
            {"Disabled", "KillFeed Disabled"}
        }, this);

        #endregion

        #region Kill Events

        private void OnKilled(BasePlayer attacker, BasePlayer victim, HitInfo hitInfo, int dist)
        {
            if (_config.EnableChatFeed)
            {
                if (!_data.DisabledUsers.Contains(attacker.userID))
                    Player.Message(attacker, _("MsgAttacker", attacker), null, _config.IconId, victim.displayName, dist, hitInfo.boneArea);
                if (!_data.DisabledUsers.Contains(victim.userID))
                    Player.Message(victim, _("MsgVictim", victim), null, _config.IconId, attacker.displayName, dist, GetCustomWeaponName(hitInfo), hitInfo.boneArea);
            }
            _killQueue.OnDeath(victim, attacker, string.Format(_("MsgFeedKill"), SanitizeName(attacker.displayName), SanitizeName(victim.displayName), GetCustomWeaponName(hitInfo), hitInfo.boneArea, GetDistanceColor(dist), dist));
        }

        private void OnSuicide(BasePlayer victim) => _killQueue.OnDeath(victim, null, string.Format(_("MsgFeedKillSuicide"), SanitizeName(victim.displayName)));

        private void OnKilledByRadiation(BasePlayer victim) => _killQueue.OnDeath(victim, null, string.Format(_("MsgFeedKillRadiation"), SanitizeName(victim.displayName)));

        private void OnKilledByEnt(BaseEntity attacker, BasePlayer victim, HitInfo hitInfo)
        {
            _killQueue.OnDeath(victim, null, string.Format(_($"MsgFeedKill{attacker.GetType()}"), SanitizeName(victim.displayName)));
        }

        private void OnDeathFromWounds(BasePlayer attacker, BasePlayer victim)
        {
            if (_config.EnableChatFeed)
            {
                if (!_data.DisabledUsers.Contains(attacker.userID))
                    Player.Message(attacker, _("MsgAtkWounded", attacker), null, _config.IconId, victim.displayName);
                if (!_data.DisabledUsers.Contains(victim.userID))
                    Player.Message(victim, _("MsgVictimWounded", victim), null, _config.IconId, attacker.displayName);
            }
            _killQueue.OnDeath(victim, attacker, string.Format(_("MsgFeedWounded"), SanitizeName(attacker.displayName), SanitizeName(victim.displayName)));
        }

        private void OnExploded(BasePlayer attacker, BasePlayer victim)
        {
            if (_config.EnableChatFeed)
            {
                if (!_data.DisabledUsers.Contains(attacker.userID))
                    Player.Message(attacker, _("MsgAttackerBlown", attacker), null, _config.IconId, victim.displayName);
                if (!_data.DisabledUsers.Contains(victim.userID))
                    Player.Message(victim, _("MsgVictimBlown", victim), null, _config.IconId, attacker.displayName);
            }
            _killQueue.OnDeath(victim, attacker, string.Format(_("MsgFeedBlown"), SanitizeName(attacker.displayName), SanitizeName(victim.displayName)));
        }

        private void OnBurned(BasePlayer attacker, BasePlayer victim)
        {
            if (_config.EnableChatFeed)
            {
                if (!_data.DisabledUsers.Contains(attacker.userID))
                    Player.Message(attacker, _("MsgAtkBurned", attacker), null, _config.IconId, victim.displayName);
                if (!_data.DisabledUsers.Contains(victim.userID))
                    Player.Message(victim, _("MsgVictimBurned", victim), null, _config.IconId, attacker.displayName);
            }
            _killQueue.OnDeath(victim, attacker, string.Format(_("MsgFeedBurned"), SanitizeName(attacker.displayName), SanitizeName(victim.displayName)));
        }

        #endregion

        #region UI

        private class KillEvent : Pool.IPooled
        {
            public int DisplayUntil;
            public string Text;

            public KillEvent Init(string text, int displayUntil)
            {
                Text = text;
                DisplayUntil = displayUntil;
                return this;
            }

            public void EnterPool()
            {
                Text = null;
                DisplayUntil = 0;
            }

            public void LeavePool() { }
        }

        private class KillQueue : MonoBehaviour
        {
            private readonly WaitForSeconds _secondDelay = new WaitForSeconds(1f);
            private readonly Queue<KillEvent> _queue = new Queue<KillEvent>(_config.MaxFeedMessages);
            private readonly CuiOutlineComponent _outlineStatic = new CuiOutlineComponent { Distance = _config.OutlineSize, Color = "0 0 0 1" };
            private readonly CuiRectTransformComponent[] _rectTransformStatic = new CuiRectTransformComponent[_config.MaxFeedMessages];
            private readonly CuiTextComponent[] _textStatic = new CuiTextComponent[_config.MaxFeedMessages];
            private readonly CuiElementContainer _cui = new CuiElementContainer();
            private bool _needsRedraw;
            private int _currentlyDrawn;

            public void OnDeath(BasePlayer victim, BasePlayer attacker, string text)
            {
                if (_queue.Count == _config.MaxFeedMessages)
                    DequeueEvent(_queue.Dequeue());
                PushEvent(Pool.Get<KillEvent>().Init(text, Epoch.Current + _config.FeedMessageTtlSec));
            }

            private void PushEvent(KillEvent evt)
            {
                _queue.Enqueue(evt);
                _needsRedraw = true;
                DoProccessQueue();
            }

            private void Start()
            {
                for (var i = 0; i < _config.MaxFeedMessages; i++)
                {
                    _rectTransformStatic[i] = new CuiRectTransformComponent
                    {
                        AnchorMax =
                            $"{_config.AnchorMax.Split(Convert.ToChar(' '))[0]} {float.Parse(_config.AnchorMax.Split(Convert.ToChar(' '))[1]) - (_config.HeightIdent * i)}",
                        AnchorMin =
                            $"{_config.AnchorMin.Split(Convert.ToChar(' '))[0]} {float.Parse(_config.AnchorMin.Split(Convert.ToChar(' '))[1]) - (_config.HeightIdent * i)}"
                    };
                    _textStatic[i] = new CuiTextComponent { Align = TextAnchor.MiddleRight, FontSize = _config.FontSize, Text = string.Empty };
                }
                StartCoroutine(ProccessQueue());
            }

            private void DequeueEvent(KillEvent evt)
            {
                Pool.Free(ref evt);
                _needsRedraw = true;
            }

            private void DoProccessQueue()
            {
                while (_queue.Count > 0 && _queue.Peek().DisplayUntil < Epoch.Current)
                    DequeueEvent(_queue.Dequeue());

                if (!_needsRedraw)
                    return;
                var toBeRemoved = _currentlyDrawn;
                _currentlyDrawn = 0;
                foreach (var killEvent in _queue)
                {
                    var cuiText = _textStatic[_currentlyDrawn];
                    cuiText.Text = killEvent.Text;
                    _cui.Add(new CuiElement
                    {
                        Name = $"kf-{_currentlyDrawn}",
                        Parent = "Hud",
                        Components =
                        {
                            cuiText,
                            _rectTransformStatic[_currentlyDrawn],
                            _outlineStatic
                        }
                    });
                    if (++_currentlyDrawn == _config.MaxFeedMessages)
                        break;
                }
                _needsRedraw = false;
                SendKillCui(_cui, toBeRemoved);
                _cui.Clear();
            }

            private IEnumerator ProccessQueue()
            {
                while (!Interface.Oxide.IsShuttingDown)
                {
                    DoProccessQueue();
                    yield return _secondDelay;
                }
            }

            private static void SendKillCui(CuiElementContainer cui, int toBeRemoved)
            {
                var json = cui.ToJson();
                BasePlayer.activePlayerList.ForEach(plr =>
                {
                    for (var i = toBeRemoved; i > 0; i--)
                        CuiHelper.DestroyUi(plr, $"kf-{i - 1}");
                    if (!_data.DisabledUsers.Contains(plr.userID))
                        CuiHelper.AddUi(plr, json);
                });
            }

            public static void RemoveKillCui(string name) => BasePlayer.activePlayerList.ForEach(plr => CuiHelper.DestroyUi(plr, name));
        }

        #endregion

        #region Utils

        private string _(string msg, BasePlayer player = null) => lang.GetMessage(msg, this, player?.UserIDString);

        private string GetCustomWeaponName(HitInfo hitInfo)
        {
            var name = GetWeaponName(hitInfo);
            if (string.IsNullOrEmpty(name))
                return null;

            string translatedName;
            if (_config.Weapons.TryGetValue(name, out translatedName))
                return translatedName;

            _config.Weapons.Add(name, name);
            Config.WriteObject(_config);
            return name;
        }

        private string GetWeaponName(HitInfo hitInfo)
        {
            var wpnName = "[N/A]";
            if (hitInfo.Weapon != null)
            {
                var wpn = hitInfo.Weapon;
                var item = wpn.GetItem();
                if (item != null)
                    wpnName = item.info.displayName.english;
            }
            else if (hitInfo.WeaponPrefab != null)
            {
                if (!_itemNameMapping.TryGetValue(hitInfo.WeaponPrefab.prefabID, out wpnName))
                    PrintWarning($"GetWeaponName failed. Unresolved prefab {hitInfo.WeaponPrefab.prefabID} ({hitInfo.damageTypes.GetMajorityDamageType()}/{hitInfo.damageTypes.Get(hitInfo.damageTypes.GetMajorityDamageType())})");
            }
            else
                PrintWarning($"GetWeaponName failed. Weapon and WeaponPrefab is null? ({hitInfo.damageTypes.GetMajorityDamageType()})");
            return wpnName;
        }


        private static bool IsExplosion(HitInfo hit) => (hit.WeaponPrefab != null && (hit.WeaponPrefab.ShortPrefabName.Contains("grenade") || hit.WeaponPrefab.ShortPrefabName.Contains("explosive")))
                                                        || hit.damageTypes.GetMajorityDamageType() == DamageType.Explosion || (!hit.damageTypes.IsBleedCausing() && hit.damageTypes.Has(DamageType.Explosion));

        private static bool IsFlame(HitInfo hit) => hit.damageTypes.GetMajorityDamageType() == DamageType.Heat || (!hit.damageTypes.IsBleedCausing() && hit.damageTypes.Has(DamageType.Heat));

        private static bool IsRadiation(HitInfo hit) => hit.damageTypes.GetMajorityDamageType() == DamageType.Radiation || (!hit.damageTypes.IsBleedCausing() && hit.damageTypes.Has(DamageType.Radiation));

        private static bool IsTrap(BaseEntity entity) => _config.Ents.Contains(entity?.GetType().ToString());

        private static string GetDistanceColor(int dist)
        {
            foreach (var distanceColor in _config.DistanceColors)
            {
                if (distanceColor.TestDistance(dist))
                    return distanceColor.Color;
            }
            return _config.DefaultDistanceColor ?? "white";
        }

        private static string SanitizeName(string name)
        {
            if (name.Length > _config.MaxPlayerNameLength)
                name = name.Substring(0, _config.MaxPlayerNameLength).Trim();
            return name.Replace("\"", "''");
        }

        #endregion
    }
}