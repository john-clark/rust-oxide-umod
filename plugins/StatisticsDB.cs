using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;
using Time = Oxide.Core.Libraries.Time;

namespace Oxide.Plugins
{
    [Info("Statistics DB", "Iv Misticos", "1.0.1")]
    [Description("Statistics database for developers")]
    public class StatisticsDB : RustPlugin
    {
        #region Variables
        
        // ReSharper disable once InconsistentNaming
        [PluginReference] private Plugin ConnectionDB = null;
        
        private static PluginData _data = new PluginData();

        private static readonly Time Time = GetLibrary<Time>();
        
        #endregion
        
        #region Configuration

        private static Configuration _config = new Configuration();
        
        public class Configuration
        {
            [JsonProperty(PropertyName = "Debug")]
            public bool Debug = false;
            
            [JsonProperty(PropertyName = "Inactive Entry Lifetime")]
            public uint Lifetime = 259200;

            [JsonProperty(PropertyName = "Collect Joins")]
            public bool CollectJoins = true;

            [JsonProperty(PropertyName = "Collect Leaves")]
            public bool CollectLeaves = true;

            [JsonProperty(PropertyName = "Collect Kills")]
            public bool CollectKills = true;

            [JsonProperty(PropertyName = "Collect Deaths")]
            public bool CollectDeaths = true;

            [JsonProperty(PropertyName = "Collect Suicides")]
            public bool CollectSuicides = true;

            [JsonProperty(PropertyName = "Collect Shots")]
            public bool CollectShots = true;

            [JsonProperty(PropertyName = "Collect Headshots")]
            public bool CollectHeadshots = true;

            [JsonProperty(PropertyName = "Collect Experiments")]
            public bool CollectExperiments = true;

            [JsonProperty(PropertyName = "Collect Recoveries")]
            public bool CollectRecoveries = true;

            [JsonProperty(PropertyName = "Collect Voice Bytes")]
            public bool CollectVoiceBytes = true;

            [JsonProperty(PropertyName = "Collect Wounded Times")]
            public bool CollectWoundedTimes = true;

            [JsonProperty(PropertyName = "Collect Crafted Items")]
            public bool CollectCraftedItems = true;

            [JsonProperty(PropertyName = "Collect Repaired Items")]
            public bool CollectRepairedItems = true;

            [JsonProperty(PropertyName = "Collect Lift Usages")]
            public bool CollectLiftUsages = true;

            [JsonProperty(PropertyName = "Collect Wheel Spins")]
            public bool CollectWheelSpins = true;

            [JsonProperty(PropertyName = "Collect Hammer Hits")]
            public bool CollectHammerHits = true;

            [JsonProperty(PropertyName = "Collect Explosives Thrown")]
            public bool CollectExplosivesThrown = true;

            [JsonProperty(PropertyName = "Collect Weapon Reloads")]
            public bool CollectWeaponReloads = true;

            [JsonProperty(PropertyName = "Collect Rockets Launched")]
            public bool CollectRocketsLaunched = true;

            [JsonProperty(PropertyName = "Collect Collectible Pickups")]
            public bool CollectCollectiblePickups = true;

            [JsonProperty(PropertyName = "Collect Plant Pickups")]
            public bool CollectPlantPickups = true;

            [JsonProperty(PropertyName = "Collect Gathered")]
            public bool CollectGathered = true;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
            }
            catch
            {
                Config.WriteObject(_config, false, $"{Interface.GetMod().ConfigDirectory}/{Name}.jsonError");
                PrintError("The configuration file contains an error and has been replaced with a default config.\n" +
                           "The error configuration file was saved in the .jsonError extension");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);
        
        #endregion
        
        #region Work with Data

        private void SaveData() => ConnectionDB?.Call("API_SetValue", Name, _data);

        private void LoadData()
        {
            try
            {
                _data = JsonConvert.DeserializeObject<PluginData>(ConnectionDB?.Call<string>("API_GetValueRaw", Name),
                    new JsonSerializerSettings
                    {
                        ObjectCreationHandling = ObjectCreationHandling.Replace,
                        NullValueHandling = NullValueHandling.Ignore
                    });
            }
            catch (Exception e)
            {
                PrintError($"Error: {e.Message}\n" +
                           $"Description: {e.StackTrace}");
            }

            if (_data == null) _data = new PluginData();
            SaveData();
        }

        private class PluginData
        {
            [JsonProperty(PropertyName = "Statistics", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<ulong, PlayerStats> Statistics = new Dictionary<ulong, PlayerStats>();
            
            [JsonProperty(PropertyName = "Players", DefaultValueHandling = DefaultValueHandling.Ignore, ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<OldPlayerStats> Players = new List<OldPlayerStats>();
        }

        private class PlayerStats
        {
            // ReSharper disable once MemberCanBePrivate.Local
            public uint LastUpdate { get; private set; }

            private uint _joins;
            public uint Joins
            {
                get { return _joins; }
                set
                {
                    Update();
                    _joins = value;
                }
            }

            private uint _leaves;
            public uint Leaves
            {
                get { return _leaves; }
                set
                {
                    Update();
                    _leaves = value;
                }
            }

            private uint _kills;
            public uint Kills
            {
                get { return _kills; }
                set
                {
                    Update();
                    _kills = value;
                }
            }

            private uint _deaths;
            public uint Deaths
            {
                get { return _deaths; }
                set
                {
                    Update();
                    _deaths = value;
                }
            }

            private uint _suicides;
            public uint Suicides
            {
                get { return _suicides; }
                set
                {
                    Update();
                    _suicides = value;
                }
            }

            private uint _shots;
            public uint Shots
            {
                get { return _shots; }
                set
                {
                    Update();
                    _shots = value;
                }
            }

            private uint _headshots;
            public uint Headshots
            {
                get { return _headshots; }
                set
                {
                    Update();
                    _headshots = value;
                }
            }

            private uint _experiments;
            public uint Experiments
            {
                get { return _experiments; }
                set
                {
                    Update();
                    _experiments = value;
                }
            }

            private uint _recoveries;
            public uint Recoveries
            {
                get { return _recoveries; }
                set
                {
                    Update();
                    _recoveries = value;
                }
            }

            private uint _voiceBytes;
            public uint VoiceBytes
            {
                get { return _voiceBytes; }
                set
                {
                    Update();
                    _voiceBytes = value;
                }
            }

            private uint _woundedTimes;
            public uint WoundedTimes
            {
                get { return _woundedTimes; }
                set
                {
                    Update();
                    _woundedTimes = value;
                }
            }

            private uint _craftedItems;
            public uint CraftedItems
            {
                get { return _craftedItems; }
                set
                {
                    Update();
                    _craftedItems = value;
                }
            }

            private uint _repairedItems;
            public uint RepairedItems
            {
                get { return _repairedItems; }
                set
                {
                    Update();
                    _repairedItems = value;
                }
            }

            private uint _liftUsages;
            public uint LiftUsages
            {
                get { return _liftUsages; }
                set
                {
                    Update();
                    _liftUsages = value;
                }
            }

            private uint _wheelSpins;
            public uint WheelSpins
            {
                get { return _wheelSpins; }
                set
                {
                    Update();
                    _wheelSpins = value;
                }
            }

            private uint _hammerHits;
            public uint HammerHits
            {
                get { return _hammerHits; }
                set
                {
                    Update();
                    _hammerHits = value;
                }
            }

            private uint _explosivesThrown;
            public uint ExplosivesThrown
            {
                get { return _explosivesThrown; }
                set
                {
                    Update();
                    _explosivesThrown = value;
                }
            }

            private uint _weaponReloads;
            public uint WeaponReloads
            {
                get { return _weaponReloads; }
                set
                {
                    Update();
                    _weaponReloads = value;
                }
            }

            private uint _rocketsLaunched;
            public uint RocketsLaunched
            {
                get { return _rocketsLaunched; }
                set
                {
                    Update();
                    _rocketsLaunched = value;
                }
            }

            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, uint> CollectiblePickups = new Dictionary<string, uint>();
            
            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, uint> PlantPickups = new Dictionary<string, uint>();
            
            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, uint> Gathered = new Dictionary<string, uint>();

            public PlayerStats()
            {
                PrintDebug("Called PlayerStats Constructor");
            }

            internal PlayerStats(ulong id)
            {
                PrintDebug("Called PlayerStats Constructor 2");
                if (!id.IsSteamId())
                    return;
                
                Update();
                _data.Statistics.Add(id, this);
            }

            private void Update() => LastUpdate = Time.GetUnixTimestamp();

            public static PlayerStats Find(ulong id)
            {
                var playersCount = _data.Statistics.Count;
                for (var i = 0; i < playersCount; i++)
                {
                    if (_data.Statistics.ContainsKey(id))
                        return _data.Statistics[id];
                }

                return null;
            }

            public static PlayerStats TryFind(ulong id) => Find(id) ?? new PlayerStats(id);
        }

        private class OldPlayerStats : PlayerStats
        {
            public ulong ID;

            public void Convert()
            {
                if (_data.Statistics.ContainsKey(ID))
                    return;
                
                // ReSharper disable once ObjectCreationAsStatement
                _data.Statistics.Add(ID, this);
            }
        }

        #endregion
        
        #region Hooks

        private void Loaded()
        {
            if (ConnectionDB == null || !ConnectionDB.IsLoaded)
            {
                PrintWarning("This plugin requires ConnectionDB!");
                Interface.GetMod().UnloadPlugin(Name);
                return;
            }

            LoadData();

            if (_data.Players != null)
            {
                for (var i = _data.Players.Count - 1; i >= 0; i--)
                {
                    var entry = _data.Players[i];
                    entry.Convert();
                    _data.Players.RemoveAt(i);
                }
            }

            var playersCount = BasePlayer.activePlayerList.Count;
            for (var i = 0; i < playersCount; i++)
            {
                OnPlayerInit(BasePlayer.activePlayerList[i]);
            }

            var current = Time.GetUnixTimestamp();
            var data = _data.Statistics.ToArray();
            var removed = 0;
            for (var i = _data.Statistics.Count - 1; i >= 0; i--)
            {
                var entry = data[i].Value;
                if (entry.LastUpdate + _config.Lifetime > current) continue;
                
                _data.Statistics.Remove(data[i].Key);
                removed++;
            }

            PrintDebug($"Removed old data entries: {removed}");
            SaveData();
            
            if (!_config.CollectJoins)
                Unsubscribe(nameof(OnPlayerInit));
            
            if (!_config.CollectLeaves)
                Unsubscribe(nameof(OnPlayerDisconnected));
            
            if (!_config.CollectExperiments)
                Unsubscribe(nameof(CanExperiment));
            
            if (!_config.CollectHeadshots)
                Unsubscribe(nameof(OnEntityTakeDamage));
            
            if (!_config.CollectWoundedTimes)
                Unsubscribe(nameof(OnPlayerWound));
            
            if (!_config.CollectRecoveries)
                Unsubscribe(nameof(OnPlayerRecover));
            
            if (!_config.CollectVoiceBytes)
                Unsubscribe(nameof(OnPlayerVoice));
            
            if (!_config.CollectCraftedItems)
                Unsubscribe(nameof(OnItemCraftFinished));
            
            if (!_config.CollectRepairedItems)
                Unsubscribe(nameof(OnItemRepair));

            if (!_config.CollectLiftUsages)
                Unsubscribe(nameof(OnLiftUse));

            if (!_config.CollectWheelSpins)
                Unsubscribe(nameof(OnSpinWheel));

            if (!_config.CollectHammerHits)
                Unsubscribe(nameof(OnHammerHit));

            if (!_config.CollectExplosivesThrown)
                Unsubscribe(nameof(OnExplosiveThrown));

            if (!_config.CollectWeaponReloads)
                Unsubscribe(nameof(OnReloadWeapon));

            if (!_config.CollectRocketsLaunched)
                Unsubscribe(nameof(OnRocketLaunched));

            if (!_config.CollectShots)
                Unsubscribe(nameof(OnWeaponFired));

            if (!_config.CollectCollectiblePickups)
                Unsubscribe(nameof(OnCollectiblePickup));

            if (!_config.CollectPlantPickups)
                Unsubscribe(nameof(OnCropGather));

            if (!_config.CollectGathered)
                Unsubscribe(nameof(OnDispenserGather));
        }

        private void OnServerSave() => SaveData();

        private void Unload() => SaveData();

        private void OnPlayerInit(BasePlayer player) => PlayerStats.TryFind(player.userID).Joins++;

        private void OnPlayerDisconnected(BasePlayer player) => PlayerStats.TryFind(player.userID).Leaves++;

        private void CanExperiment(BasePlayer player, Workbench workbench) =>
            PlayerStats.TryFind(player.userID).Experiments++;

        private void OnPlayerDie(BasePlayer player, HitInfo info)
        {
            if (info == null || player == null || player.IsNpc)
                return;
            
            var stats = PlayerStats.TryFind(player.userID);
            if (_config.CollectSuicides && info.damageTypes.GetMajorityDamageType() == DamageType.Suicide)
                stats.Suicides++;
            else
            {
                if (_config.CollectDeaths)
                    stats.Deaths++;

                if (!_config.CollectKills) return;
                
                var attacker = info.InitiatorPlayer;
                if (attacker == null || attacker.IsNpc)
                    return;

                stats = PlayerStats.TryFind(attacker.userID);
                stats.Kills++;
            }
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (info?.InitiatorPlayer == null || !info.isHeadshot)
                return;

            PlayerStats.TryFind(info.InitiatorPlayer.userID).Headshots++;
        }

        private void OnPlayerWound(BasePlayer player) => PlayerStats.TryFind(player.userID).WoundedTimes++;

        private void OnPlayerRecover(BasePlayer player) => PlayerStats.TryFind(player.userID).Recoveries++;
        
        // ReSharper disable once ParameterTypeCanBeEnumerable.Local
        private void OnPlayerVoice(BasePlayer player, byte[] data) =>
            PlayerStats.TryFind(player.userID).VoiceBytes += (uint) data.Length;

        private void OnItemCraftFinished(ItemCraftTask task, Item item) =>
            PlayerStats.TryFind(task.owner.userID).CraftedItems++;

        private void OnItemRepair(BasePlayer player, Item item) => PlayerStats.TryFind(player.userID).RepairedItems++;
        
        private void OnLiftUse(Lift lift, BasePlayer player) => PlayerStats.TryFind(player.userID).LiftUsages++;

        private void OnLiftUse(ProceduralLift lift, BasePlayer player) => PlayerStats.TryFind(player.userID).LiftUsages++;

        private void OnSpinWheel(BasePlayer player, SpinnerWheel wheel) => PlayerStats.TryFind(player.userID).WheelSpins++;

        private void OnHammerHit(BasePlayer player, HitInfo info) => PlayerStats.TryFind(player.userID).HammerHits++;

        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity) =>
            PlayerStats.TryFind(player.userID).ExplosivesThrown++;

        private void OnReloadWeapon(BasePlayer player, BaseProjectile projectile) =>
            PlayerStats.TryFind(player.userID).WeaponReloads++;

        private void OnRocketLaunched(BasePlayer player, BaseEntity entity) =>
            PlayerStats.TryFind(player.userID).RocketsLaunched++;

        private void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod,
            ProtoBuf.ProjectileShoot projectiles) => PlayerStats.TryFind(player.userID).Shots++;

        private void OnCollectiblePickup(Item item, BasePlayer player)
        {
            PrintDebug("OnCollectiblePickup called");
            var dict = PlayerStats.TryFind(player.userID).CollectiblePickups;
            uint count;
            if (dict.TryGetValue(item.info.shortname, out count))
                count += (uint) item.amount;
            else
            {
                count = (uint) item.amount;
                dict.Add(item.info.shortname, count);
            }

            dict[item.info.shortname] = count;
        }

        private void OnCropGather(PlantEntity plant, Item item, BasePlayer player)
        {
            PrintDebug("OnCropGather called");
            var dict = PlayerStats.TryFind(player.userID).PlantPickups;
            uint count;
            if (dict.TryGetValue(item.info.shortname, out count))
                count += (uint) item.amount;
            else
            {
                count = (uint) item.amount;
                dict.Add(item.info.shortname, count);
            }
            
            dict[item.info.shortname] = count;
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            PrintDebug("OnDispenserGather called");
            var dict = PlayerStats.TryFind(((BasePlayer) entity).userID).Gathered;
            if (dict.ContainsKey(item.info.shortname))
                dict[item.info.shortname] += (uint) item.amount;
            else
            {
                dict.Add(item.info.shortname, (uint) item.amount);
            }
        }

        #endregion
        
        #region API
        
        // General API

        private uint? API_GetJoins(ulong id) => PlayerStats.Find(id)?.Joins;
        private uint? API_GetLeaves(ulong id) => PlayerStats.Find(id)?.Leaves;
        private uint? API_GetKills(ulong id) => PlayerStats.Find(id)?.Kills;
        private uint? API_GetDeaths(ulong id) => PlayerStats.Find(id)?.Deaths;
        private uint? API_GetSuicides(ulong id) => PlayerStats.Find(id)?.Suicides;
        private uint? API_GetShots(ulong id) => PlayerStats.Find(id)?.Shots;
        private uint? API_GetHeadshots(ulong id) => PlayerStats.Find(id)?.Headshots;
        private uint? API_GetExperiments(ulong id) => PlayerStats.Find(id)?.Experiments;
        private uint? API_GetRecoveries(ulong id) => PlayerStats.Find(id)?.Recoveries;
        private uint? API_GetVoiceBytes(ulong id) => PlayerStats.Find(id)?.VoiceBytes;
        private uint? API_GetWoundedTimes(ulong id) => PlayerStats.Find(id)?.WoundedTimes;
        private uint? API_GetCraftedItems(ulong id) => PlayerStats.Find(id)?.CraftedItems;
        private uint? API_GetRepairedItems(ulong id) => PlayerStats.Find(id)?.RepairedItems;
        private uint? API_GetLiftUsages(ulong id) => PlayerStats.Find(id)?.LiftUsages;
        private uint? API_GetWheelSpins(ulong id) => PlayerStats.Find(id)?.WheelSpins;
        private uint? API_GetHammerHits(ulong id) => PlayerStats.Find(id)?.HammerHits;
        private uint? API_GetExplosivesThrown(ulong id) => PlayerStats.Find(id)?.ExplosivesThrown;
        private uint? API_GetWeaponReloads(ulong id) => PlayerStats.Find(id)?.WeaponReloads;
        private uint? API_GetRocketsLaunched(ulong id) => PlayerStats.Find(id)?.RocketsLaunched;
        
        // Gather API

        private Dictionary<string, uint> API_GetCollectiblePickups(ulong id) =>
            PlayerStats.Find(id)?.CollectiblePickups;
        private Dictionary<string, uint> API_GetPlantPickups(ulong id) =>
            PlayerStats.Find(id)?.PlantPickups;
        private Dictionary<string, uint> API_GetGathered(ulong id) =>
            PlayerStats.Find(id)?.Gathered;

        private uint? API_GetCollectiblePickups(ulong id, string shortname)
        {
            var data = API_GetCollectiblePickups(id);
            uint amount = 0;
            if (data?.TryGetValue(shortname, out amount) == true)
                return amount;

            return null;
        }

        private uint? API_GetPlantPickups(ulong id, string shortname)
        {
            var data = API_GetPlantPickups(id);
            uint amount = 0;
            if (data?.TryGetValue(shortname, out amount) == true)
                return amount;

            return null;
        }

        private uint? API_GetGathered(ulong id, string shortname)
        {
            var data = API_GetGathered(id);
            uint amount = 0;
            if (data?.TryGetValue(shortname, out amount) == true)
                return amount;

            return null;
        }
        
        #endregion
        
        #region Helpers

        private static void PrintDebug(string message)
        {
            if (!_config.Debug) return;
            Debug.Log($"DEBUG: {message}");
        }
        
        #endregion
    }
}