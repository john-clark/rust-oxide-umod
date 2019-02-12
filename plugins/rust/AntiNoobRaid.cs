using Oxide.Core;
using System;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using Rust;
using UnityEngine;
using Newtonsoft.Json;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("AntiNoobRaid", "Slydelix", "1.8.1", ResourceId = 2697)]
    class AntiNoobRaid : RustPlugin
    {
        [PluginReference] private Plugin PlaytimeTracker, WipeProtection, GameTipAPI, Clans;

        //set this to true if you are having issues with the plugin
        private bool debug = false;

        private List<BasePlayer> cooldown = new List<BasePlayer>();
        private List<BasePlayer> MessageCooldown = new List<BasePlayer>();
        //private Dictionary<ulong, Timer> RaidTimerDictionary = new Dictionary<ulong, Timer>();
        private Dictionary<string, string> raidtools = new Dictionary<string, string>
        {
            {"ammo.rocket.fire", "rocket_fire"},
            {"ammo.rocket.hv", "rocket_hv"},
            {"ammo.rocket.basic", "rocket_basic"},
            {"explosive.timed", "explosive.timed.deployed"},
            {"surveycharge", "survey_charge.deployed"},
            {"explosive.satchel", "explosive.satchel.deployed"},
            {"grenade.beancan", "grenade.beancan.deployed"},
            {"grenade.f1", "grenade.f1.deployed"}
        };

        private int layers = LayerMask.GetMask("Construction", "Deployed");
        private readonly string AdminPerm = "antinoobraid.admin";
        private readonly string NoobPerm = "antinoobraid.noob";

        #region Config

        private class ConfigFile
        {
            [JsonProperty("Allow clan members to destroy each others entities")]
            public bool CheckClanForOwner;
            [JsonProperty("Allow twig to be destroyed even when owner is noob")]
            public bool AllowTwigDestruction;
            [JsonProperty("Check full ownership of the base instead of only one block")]
            public bool CheckFullOwnership;
            [JsonProperty("Check Steam for in game time")]
            public bool CheckSteam;
            [JsonProperty("Days of inactivity after which player will be raidable")]
            public double InactivityRemove;
            [JsonProperty("Ignore twig when calculating base ownership (prevents exploiting)")]
            public bool IgnoreTwig;
            [JsonProperty("In-game steam time which mark player as non-noob (hours)")]
            public double SteamInGameTime;
            [JsonProperty("Kill fireballs when someone tries to raid protected player with fire (prevents lag)")]
            public bool KillFire;
            [JsonProperty("List of entities that can be destroyed even if owner is a noob, true = destroyable everywhere (not inside of owners TC range)")]
            public Dictionary<string, bool> AllowedEntities = PlaceHolderDictionary;
            [JsonProperty("Manual Mode")]
            public bool ManualMode;
            [JsonProperty("Notify player on first connection with protection time")]
            public bool MessageOnFirstConnection;
            [JsonProperty("Prevent new players from raiding")]
            public bool PreventNew;
            [JsonProperty("Refund explosives")]
            public bool Refund;
            [JsonProperty("Refunds before player starts losing explosives")]
            public int RefundTimes;
            [JsonProperty("Remove noob status of a raider on raid attempt")]
            public bool UnNoobNew;
            [JsonProperty("Remove protection from all clan members when a member tries to raid")]
            public bool RemoveClanProtection;
            [JsonProperty("Show message for not being able to raid")]
            public bool ShowMessage;
            [JsonProperty("Show time until raidable")]
            public bool ShowTime;
            [JsonProperty("Steam API key")]
            public string ApiKey;
            [JsonProperty("Time (seconds) after which noob will lose protection (in-game time)")]
            public int ProtectionTime;
            [JsonProperty("Use game tips to send first connection message to players")]
            public bool UseGT;
            [JsonProperty("User data refresh interval (seconds)")]
            public int Frequency;
            [JsonProperty("Allow team members to destroy each others entities")]
            public bool CheckTeamForOwner;
            [JsonProperty("Remove protection from all team members when a member tries to raid")]
            public bool RemoveTeamProtection;
            //[JsonProperty("Automatically add raid protection to user after raid")]
            //public bool AutoAddProtection;
            //[JsonProperty("Time (minutes) after which the raided person gains raid protection")]
            //public int AutoMinutes;

            public static Dictionary<string, bool> PlaceHolderDictionary = new Dictionary<string, bool>
            {
                {"Placeholder1", true }, {"Placeholder2", true }, {"Placeholder3", true }, {"Placeholder4", true }, {"Placeholder5", true },
            };

            public static readonly ConfigFile DefaultConfigFile = new ConfigFile
            {
                ProtectionTime = 21600,
                SteamInGameTime = 200,
                Frequency = 30,
                RefundTimes = 1,
                InactivityRemove = 7,
                ApiKey = string.Empty,
                CheckFullOwnership = true,
                CheckSteam = true,
                CheckClanForOwner = true,
                RemoveClanProtection = false,
                Refund = true,
                IgnoreTwig = true,
                KillFire = true,
                ManualMode = false,
                MessageOnFirstConnection = true,
                UseGT = true,
                AllowTwigDestruction = true,
                ShowMessage = true,
                ShowTime = false,
                PreventNew = true,
                UnNoobNew = true,
                AllowedEntities = PlaceHolderDictionary,
                CheckTeamForOwner = true,
                RemoveTeamProtection = true,
                //AutoAddProtection = true,
                //AutoMinutes = 60
            };
        }

        private ConfigFile config;

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            PrintWarning("Creating new config file");
            Config.WriteObject(ConfigFile.DefaultConfigFile, true);
            SaveConfig();
        }

        #endregion
        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"NoPlayerFound", "Couldn't find '{0}'"},
                {"NotNumber",  "{0} is not a number" },

                {"antinoobcmd_syntax", "Wrong syntax! /antinoob <addnoob|checksteam|removenoob|wipe>" },
                {"antinoobcmd_addnoob_syntax", "Wrong syntax! /antinoob addnoob <name/steamid>" },
                {"antinoobcmd_alreadynoob", "{0} is already marked as a noob" },
                {"antinoobcmd_marked", "Marked {0} as a noob" },
                {"antinoobcmd_removenoob_syntax", "Wrong syntax! /antinoob removenoob <name/steamid>" },
                {"antinoobcmd_removednoob", "{0} does not have a noob status anymore" },
                {"antinoobcmd_missingnoob", "{0} is not a noob" },
                {"antinoobcmd_wipe_syntax", "Missing argument! <all|playerdata|attempts>" },
                {"dataFileWiped", "Data file successfully wiped"},
                {"dataFileWiped_attempts", "Data file successfully wiped (raid attempts)"},
                {"dataFileWiped_playerdata", "Data file successfully wiped (player data)"},

                {"struct_noowner","Structure at {0} has no owner!" },
                {"clan_lostnoob" , "Clan '{0}' lost their noob status because they tried to raid" },
                {"console_lostnoobstatus", "{0} hasn't connected for {1} days so he lost his noob status (can be raided)"},
                {"console_notenough", "{0} doesn't have enough hours in game to be marked as a non-noob"},

                {"rn_manual", "This will remove your noob status, type /removenoob 'yes' to confirm." },
                {"rn_success", "Successfully removed your noob status" },

                {"firstconnectionmessage", "You are a new player so your buildings are protected for first {0} hours of your time on server"},

                {"steam_checkstart", "{0} connected, checking Steam"},
                {"steam_marking", "Marking {0} as non noob"},
                {"steam_connected", "{0} has connected with {1} in game hours"},
                {"steam_private", "Steam profile of {0} is private"},
                {"steam_responsewrong", "Failed to contact steam API, profile is private/wrong API key"},
                {"steam_wrongapikey", "Invalid API key"},

                {"pt_notInstalled_first", "Playtime Tracker is not installed, will check again in 30 seconds"},
                {"pt_notInstalled", "Playtime Tracker is not installed!"},
                {"pt_detected", "Playtime Tracker detected"},

                {"userinfo_nofound", "Failed to get playtime info for {0}! trying again in 20 seconds!"},

                {"can_attack", "This structure is not raid protected"},
                {"NotLooking", "You are not looking at a building/deployable"},

                {"refunditem_help", "Wrong Syntax! /refunditem add <you have to hold the item you want to add>\n/refunditem remove <you have to hold the item you want to remove>\n/refunditem list\n/refunditem clear\n/refunditem all <sets all raid tools as refundable>"},
                {"refunditem_needholditem", "You need to hold the item you want to add/remove from refund list"},
                {"refunditem_notexplosive", "This item is not an explosive"},
                {"refunditem_added", "Added '{0}' to list of items to refund"},
                {"refunditem_alreadyonlist", "This item is already on the list"},
                {"refunditem_notonlist", "This item is not on the list"},
                {"refunditem_removed", "Removed '{0}' from the list of items to refund"},
                {"refunditem_addedall", "Added all raid tools to refund list"},
                {"refunditem_cleared", "Cleared list of items to refund"},
                {"refunditem_empty", "There are no item set up yet"},
                {"refunditem_list", "List of items which will get refunded: \n{0}"},

                {"refund_free", "Your '{0}' was refunded."},
                {"refund_last", "Your '{0}' was refunded but will not be next time."},
                {"refund_1time", "Your '{0}' was refunded. After 1 more attempt it wont be refunded."},
                {"refund_nTimes", "Your '{0}' was refunded. After {1} more attempts it wont be refunded"},
                {"cannot_attack_no_time", "This entity cannot be destroyed because it was built by a new player"},
                {"cannot_attack_new_raider", "Because you are a new player you cannot raid (yet)"},
                {"cannot_attack_time", "This entity cannot be destroyed because it was built by a new player ({0})"},

                {"secs", " seconds"},
                {"mins", " minutes"},
                {"min", " minute"},
                {"hours", " hours"},
                {"hour", " hour"},
                {"day", " day"},
                {"days", " days"}
            }, this);
        }

        #endregion
        #region DataFile/Classes

        private class BuildingInfo
        {
            public static HashSet<BuildingInfo> buildCache = new HashSet<BuildingInfo>();

            public BuildingInfo() { }

            public uint buildingID;

            public ulong OwnerID;

            public DateTime lastUpdate;

            public static BuildingInfo GetByBuildingID(uint bID)
            {
                foreach (var entry in buildCache)
                    if (entry.buildingID == bID) return entry;

                return null;
            }

            public double GetCacheAge()
            {
                if (lastUpdate == null) return -1;
                return DateTime.UtcNow.Subtract(lastUpdate).TotalSeconds;
            }
        }

        private class ClanInfo
        {
            public static HashSet<ClanInfo> clanCache = new HashSet<ClanInfo>();

            public ClanInfo() { }

            public static ClanInfo FindClanByName(string clanName)
            {
                foreach (var clan in ClanInfo.clanCache)
                    if (clan.clanName == clanName) return clan;

                return null;
            }

            public static ClanInfo GetClanOf(ulong ID)
            {
                foreach (var clan in ClanInfo.clanCache)
                    if (clan.members.Contains(ID)) return clan;

                return null;
            }

            public string clanName { get; set; }

            public List<ulong> members = new List<ulong>();
        }

        private class StoredData
        {
            public Dictionary<ulong, double> players = new Dictionary<ulong, double>();
            public Dictionary<ulong, int> AttackAttempts = new Dictionary<ulong, int>();
            public Dictionary<string, string> ItemList = new Dictionary<string, string>();
            public List<ulong> playersWithNoData = new List<ulong>();
            public List<ulong> FirstMessaged = new List<ulong>();
            public Dictionary<ulong, string> lastConnection = new Dictionary<ulong, string>();

            public StoredData()
            {
            }
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(this.Name, storedData);

        StoredData storedData;

        #endregion
        #region Steam

        private class SteamGames
        {
            public Content response;

            public class Content
            {
                public int game_count;
                public Game[] games;

                public class Game
                {
                    public uint appid;
                    public int playtime_2weeks;
                    public int playtime_forever;
                }
            }
        }

        #endregion
        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(AdminPerm, this);
            permission.RegisterPermission(NoobPerm, this);

            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(this.Name);
            config = Config.ReadObject<ConfigFile>();
            NextTick(() => Config.WriteObject(config));
            //if (config.AutoMinutes == 0 && config.AutoAddProtection) PrintError("Time (minutes) after which the raided person gains raid protection is set to 0!!! Change this!!");
        }

        private void RegisterCommands()
        {
            foreach (var command in AdminCommands)
                AddCovalenceCommand(command.Key, command.Value, AdminPerm);
        }

        private void Loaded()
        {
            RegisterCommands();

            foreach (var entry in storedData.players.Where(x => !storedData.lastConnection.ContainsKey(x.Key)))
                storedData.lastConnection.Add(entry.Key, string.Empty);

            if (!config.ManualMode)
            {
                StartChecking();
                CheckPlayersWithNoInfo();
            }

            timer.Every(60f, RefreshClanCache);

            if (config.ManualMode)
                foreach (var p in BasePlayer.activePlayerList.Where(x => !storedData.players.ContainsKey(x.userID)))
                    storedData.players.Add(p.userID, -50d);

            if (PlaytimeTracker == null)
            {
                PrintWarning(lang.GetMessage("pt_notInstalled_first", this, null));
                timer.Once(30f, () => {
                    if (PlaytimeTracker == null)
                    {
                        PrintWarning(lang.GetMessage("pt_notInstalled", this, null));
                        return;
                    }
                    PrintWarning(lang.GetMessage("pt_detected", this, null));
                });
            }
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (hitinfo == null || entity == null) return null;
            var dmgType = hitinfo?.damageTypes?.GetMajorityDamageType() ?? DamageType.Generic;
            if (dmgType == DamageType.Decay || dmgType == DamageType.Generic || dmgType == DamageType.Heat) return null;

            if (!(entity is BuildingBlock || entity is Door || entity.PrefabName.Contains("deployable"))) return null;
            if (config.AllowTwigDestruction && ((entity as BuildingBlock)?.grade == BuildingGrade.Enum.Twigs)) return null;

            if (config.AllowedEntities.ContainsKey(entity.ShortPrefabName))
            {
                if (config.AllowedEntities[entity.ShortPrefabName] || entity.GetBuildingPrivilege() == null) return null;
                if (!entity.GetBuildingPrivilege().authorizedPlayers.Select(x => x.userid).Contains(entity.OwnerID)) return null;
            }

            var owner = config.CheckFullOwnership ? FullOwner(entity) : entity.OwnerID;
            if (owner == 0u) return null;

            /*if (config.AutoAddProtection)
            {
                if (RaidTimerDictionary.ContainsKey(owner))
                {
                    RaidTimerDictionary[owner]?.Destroy();
                    if (RaidTimerDictionary.ContainsKey(owner)) RaidTimerDictionary.Remove(owner);

                    RaidTimerDictionary.Add(owner, timer.Once(60f * config.AutoMinutes, () => {
                        storedData.players[owner] = -25d;
                        if (RaidTimerDictionary.ContainsKey(owner)) RaidTimerDictionary.Remove(owner);
                    }));
                }  

                else RaidTimerDictionary.Add(owner, timer.Once(60f * config.AutoMinutes, () => {
                    storedData.players[owner] = -25d;
                    if (RaidTimerDictionary.ContainsKey(owner)) RaidTimerDictionary.Remove(owner);
                }));
            }*/

            BasePlayer attacker = hitinfo.InitiatorPlayer;
            if (attacker == null || entity?.OwnerID == attacker?.userID) return null;

            if (config.CheckClanForOwner)
            {
                ulong userID = attacker?.userID ?? 0u;
                var clan = ClanInfo.GetClanOf(userID) ?? null;
                if (clan == null)
                {
                    var clanName = Clans?.Call<string>("GetClanOf", userID) ?? string.Empty;
                    if (!string.IsNullOrEmpty(clanName))
                    {
                        var members = GetClanMembers(clanName);
                        var claninfo = new ClanInfo { clanName = clanName, members = members };
                        ClanInfo.clanCache.Add(claninfo);

                        if (claninfo.members.Contains(owner)) return null;
                    }
                }

                else if (clan.members.Contains(owner)) return null;
            }

            if (config.CheckTeamForOwner)
            {
                var instance = RelationshipManager._instance;
                if (instance == null) PrintWarning("RelationshipManager instance is null! how is this even possible?");

                else
                {
                    BasePlayer ownerPlayer;
                    if (instance.cachedPlayers.TryGetValue(owner, out ownerPlayer))
                        if (ownerPlayer.currentTeam == attacker.currentTeam && ownerPlayer.currentTeam != 0) return null;
                }
            }
                                               //I kinda forgot why this check is needed :/
            if (config.RemoveTeamProtection && !string.IsNullOrEmpty(hitinfo?.WeaponPrefab?.ShortPrefabName))
            {
                if (attacker.currentTeam != 0)
                {
                    var team = RelationshipManager.Instance?.playerTeams[attacker.currentTeam];
                    foreach (var member in team.members) storedData.players[member] = -50d;
                }
            }

            if (config.RemoveClanProtection && !string.IsNullOrEmpty(hitinfo?.WeaponPrefab?.ShortPrefabName))
            {
                string val;
                if (raidtools.TryGetValue(hitinfo?.WeaponPrefab?.ShortPrefabName, out val))
                    RemoveClanP(attacker?.userID ?? 0u);
            }

            bool wipe = WipeProtection?.Call<bool>("WipeProtected") ?? false;
            if (wipe) return true;

            if (cooldown.Contains(attacker))
            {
                RemoveCD(cooldown, attacker);
                if (PlayerIsNew(owner))
                {
                    hitinfo.damageTypes = new DamageTypeList();
                    hitinfo.DoHitEffects = false;
                    hitinfo.HitMaterial = 0;
                    return true;
                }
                return null;
            }

            cooldown.Add(attacker);
            RemoveCD(cooldown, attacker);
            LogPlayer(attacker);

            string name = hitinfo?.WeaponPrefab?.ShortPrefabName ?? string.Empty;

            if (config.PreventNew && PlayerIsNew(attacker.userID))
            {
                if (config.UnNoobNew) storedData.players[attacker.userID] = -50d;

                hitinfo.damageTypes = new DamageTypeList();
                hitinfo.DoHitEffects = false;
                hitinfo.HitMaterial = 0;
                NextTick(() => {
                    SendReply(attacker, lang.GetMessage("cannot_attack_new_raider", this, attacker.UserIDString));
                    Refund(attacker, name, entity);
                });
                return true;
            }

            if (PlayerIsNew(owner))
            {
                hitinfo.damageTypes = new DamageTypeList();
                hitinfo.DoHitEffects = false;
                hitinfo.HitMaterial = 0;
                NextTick(() => {
                    MessagePlayer(attacker, owner);
                    Refund(attacker, name, entity);
                });
                return true;
            }

            return null;
        }

        private void OnFireBallDamage(FireBall fireball, BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (hitinfo == null || fireball == null || entity == null || !config.KillFire) return;
            if (!(entity is BuildingBlock || entity is Door || entity.PrefabName.Contains("deployable")) || fireball.IsDestroyed) return;
            if (config.AllowTwigDestruction && ((entity as BuildingBlock)?.grade == BuildingGrade.Enum.Twigs)) return;

            if (config.AllowedEntities.ContainsKey(entity.ShortPrefabName))
            {
                if (config.AllowedEntities[entity.ShortPrefabName] || entity.GetBuildingPrivilege() == null) return;
                if (!entity.GetBuildingPrivilege().authorizedPlayers.Select(x => x.userid).Contains(entity.OwnerID)) return;
            }

            if (PlayerIsNew(entity.OwnerID))
            {
                fireball.Kill();
                var player = fireball.creatorEntity as BasePlayer;
                if (player != null)
                {
                    if (!MessageCooldown.Contains(player))
                    {
                        MessagePlayer(player, entity.OwnerID);
                        MessageCooldown.Add(player);
                        RemoveCD(MessageCooldown, player, 1f);
                    }
                }

                return;
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason) => LastConnect(player.userID);

        private void OnServerSave() => SaveData();

        private void OnUserConnected(IPlayer player)
        {
            BasePlayer bp = player.Object as BasePlayer;
            if (config.ManualMode)
            {
                if (!storedData.players.ContainsKey(bp.userID))
                {
                    storedData.players.Add(bp.userID, -50d);
                    return;
                }

                if (storedData.players[bp.userID] == -50d || storedData.players[bp.userID] == -25d) return;
                storedData.players[bp.userID] = -50d;
                return;
            }

            SteamCheck(bp.userID, true);

            if (storedData.players.ContainsKey(bp.userID))
                if (storedData.players[bp.userID] == -50d || storedData.players[bp.userID] == -25d) return;

            APICall(bp.userID);

            timer.Once(10f, () => FirstMessage(bp));
        }

        private void Unload()
        {
            SaveData();
        }

        #endregion
        #region Methods

        private void RefreshClanCache()
        {
            foreach (var player in covalence.Players.All.Where(x => !x.IsBanned))
            {
                ulong ID = ulong.Parse(player.Id);
                var clanName = Clans?.Call<string>("GetClanOf", ID) ?? string.Empty;
                if (!string.IsNullOrEmpty(clanName))
                {
                    if (!ClanInfo.clanCache.Select(x => x.clanName).Contains(clanName))
                    {
                        var clan = new ClanInfo { clanName = clanName, members = new List<ulong> { ID } };
                        ClanInfo.clanCache.Add(clan);
                    }

                    else
                    {
                        foreach (var clan in ClanInfo.clanCache)
                        {
                            if (clanName == clan.clanName && !clan.members.Contains(ID))
                                clan.members.Add(ID);
                        }
                    }
                }
            }
        }

        private void APICall(ulong ID, bool secondattempt = false)
        {
            if (PlaytimeTracker == null)
            {
                Puts(lang.GetMessage("pt_notInstalled", this, null));
                return;
            }

            double apitime = -1;

            try
            {
                apitime = PlaytimeTracker?.Call<double>("GetPlayTime", ID.ToString()) ?? -1d;
            }

            catch (Exception)
            {
                Puts(lang.GetMessage("userinfo_nofound", this, null), ID);
                if (!secondattempt)
                    timer.Once(20f, () => APICall(ID, true));
            }

            if (apitime == -1d)
            {
                if (secondattempt) storedData.playersWithNoData.Add(ID);

                Puts(lang.GetMessage("userinfo_nofound", this, null));
                return;
            }

            if (storedData.playersWithNoData.Contains(ID)) storedData.playersWithNoData.Remove(ID);

            if (storedData.players.ContainsKey(ID))
            {
                storedData.players[ID] = apitime;
                return;
            }

            storedData.players.Add(ID, apitime);
        }

        private void Check()
        {
            if (BasePlayer.activePlayerList.Count == 0) return;

            foreach (BasePlayer bp in BasePlayer.activePlayerList)
            {
                if (!bp.IsConnected || bp == null) continue;
                if (storedData.playersWithNoData.Contains(bp.userID)) continue;
                var val = 0d;
                if (storedData.players.TryGetValue(bp.userID, out val) && (val == -50d || val == -25d)) continue;

                APICall(bp.userID);
            }

            foreach (BasePlayer bp in BasePlayer.sleepingPlayerList)
            {
                if (storedData.playersWithNoData.Contains(bp.userID)) continue;
                var val = 0d;
                if (storedData.players.TryGetValue(bp.userID, out val) && (val == -50d || val == -25d)) continue;

                APICall(bp.userID);
            }
        }

        private void Check(ulong ID)
        {
            if (storedData.playersWithNoData.Contains(ID)) return;
            if (storedData.players[ID] == -50d || storedData.players[ID] == -25d) return;
            APICall(ID);
        }

        private string CheckLeft(int intsecs)
        {
            var time = DateTime.Now.AddSeconds(intsecs - 1);
            var timespan = time.Subtract(DateTime.Now);

            string t = string.Empty;
            if (timespan.Days != 0) t += timespan.Days + "d ";
            if (timespan.Hours != 0) t += timespan.Hours + "h ";
            if (timespan.Minutes != 0) t += timespan.Minutes + "m ";
            if (timespan.Seconds != 0) t += timespan.Seconds + "s";
            if (t.Last() == ' ')
            {
                string nw = t.Remove(t.Length - 1);
                return nw;
            }

            return t;
        }

        private void CheckPlayersWithNoInfo()
        {
            int rate = (config.Frequency <= 10) ? 10 : config.Frequency - 10;

            timer.Every(rate, () =>
            {
                if (storedData.playersWithNoData.Count < 1) return;

                foreach (ulong ID in storedData.playersWithNoData.ToList())
                {
                    double time = -1d;
                    try
                    {
                        time = PlaytimeTracker?.Call<double>("GetPlayTime", ID.ToString()) ?? -1d;
                    }

                    catch (Exception)
                    {
                        continue;
                    }

                    if (time == -1d) continue;

                    if (storedData.players.ContainsKey(ID))
                    {
                        string date = "[" + DateTime.Now.ToString() + "] ";
                        LogToFile(this.Name, date + "Somehow info exists for player that is in data file already (" + ID + ")", this, true);
                        storedData.players[ID] = time;
                        storedData.playersWithNoData.Remove(ID);
                        continue;
                    }

                    storedData.players.Add(ID, time);
                    storedData.playersWithNoData.Remove(ID);
                    continue;
                }
            });
        }

        private void FirstMessage(BasePlayer player)
        {
            if (!config.MessageOnFirstConnection || storedData.FirstMessaged.Contains(player.userID)) return;

            storedData.FirstMessaged.Add(player.userID);
            var val = 0d;
            if (storedData.players.TryGetValue(player.userID, out val) && (val > 100d || val == -50d || val == -25d)) return;

            string msg = string.Format(lang.GetMessage("firstconnectionmessage", this, player.UserIDString), (config.ProtectionTime / 3600d));

            if (config.UseGT)
            {
                if (GameTipAPI != null)
                {
                    GameTipAPI?.Call("ShowGameTip", player, msg, 10f, true);
                    return;
                }

                player.SendConsoleCommand("gametip.showgametip", msg);
                timer.Once(10f, () => {
                    player.SendConsoleCommand("gametip.hidegametip");
                    return;
                });
                return;
            }

            SendReply(player, msg);
        }

        private ulong FullOwner(BaseEntity ent, BasePlayer p = null)
        {
            if (ent == null) return 0u;

            var block = ent.GetComponent<BuildingBlock>();
            if (block == null) return ent.OwnerID;

            var cached = BuildingInfo.GetByBuildingID(block.buildingID);
            if (cached != null)
                if (cached.GetCacheAge() < 180 && cached.GetCacheAge() != -1) return cached.OwnerID;

            var ents = BaseEntity.saveList.Where(x => x.GetComponent<BuildingBlock>()?.buildingID == block.buildingID).ToList();
            var backup = ents.ToList();

            if (config.IgnoreTwig) ents.RemoveAll(x => (x as BuildingBlock)?.grade == BuildingGrade.Enum.Twigs);

            if (ents.Count == 0) ents = backup.ToList();

            Dictionary<ulong, int> ownership = new Dictionary<ulong, int>();

            foreach (var e in ents)
            {
                if (e.OwnerID == 0u) continue;
                var val = 0;
                if (!ownership.TryGetValue(e.OwnerID, out val)) ownership[e.OwnerID] = 1;
                else ownership[e.OwnerID]++;
            }

            if (ownership.Count == 0)
            {
                //Should this even happen?
                PrintWarning(lang.GetMessage("struct_noowner", this, null), ent.transform.position);
                return ent.OwnerID;
            }

            var owner = ownership.Max(x => x.Key);

            if (cached != null)
            {
                cached.OwnerID = owner;
                cached.lastUpdate = DateTime.UtcNow;
            }

            else BuildingInfo.buildCache.Add(new BuildingInfo { buildingID = block.buildingID, lastUpdate = DateTime.UtcNow, OwnerID = owner });

            return owner;
        }

        private List<ulong> GetClanMembers(string clanName)
        {
            if (string.IsNullOrEmpty(clanName)) return new List<ulong>();

            var claninfo = ClanInfo.FindClanByName(clanName);
            if (claninfo != null) return claninfo.members;

            if (Clans == null) return new List<ulong>();

            List<ulong> IDlist = new List<ulong>();

            foreach (var p in covalence.Players.All)
            {
                string clan = Clans?.Call<string>("GetClanOf", p) ?? string.Empty;

                if (clan == clanName)
                    IDlist.Add(ulong.Parse(p.Id));
            }

            RefreshClanCache();
            return IDlist;
        }

        private BaseEntity GetLookAtEntity(BasePlayer player, float maxDist = 10f)
        {
            if (player == null || player.IsDead()) return null;
            RaycastHit hit;
            var currentRot = Quaternion.Euler(player?.serverInput?.current?.aimAngles ?? Vector3.zero) * Vector3.forward;
            var ray = new Ray((player?.eyes?.position ?? Vector3.zero), currentRot);
            if (Physics.Raycast(ray, out hit, maxDist, layers))
            {
                var ent = hit.GetEntity() ?? null;
                if (ent != null && !(ent?.IsDestroyed ?? true)) return ent;
            }

            return null;
        }

        private void LastConnect(ulong ID) => storedData.lastConnection[ID] = DateTime.Now.ToString();

        private void LogPlayer(BasePlayer attacker)
        {
            if (attacker == null) return;
            var val = 0;
            if (!storedData.AttackAttempts.TryGetValue(attacker.userID, out val)) storedData.AttackAttempts[attacker.userID] = 1;
            else storedData.AttackAttempts[attacker.userID]++;
        }

        private void MessagePlayer(BasePlayer attacker, ulong ID)
        {
            if (!storedData.players.ContainsKey(ID)) return;
            double time2 = storedData.players[ID];
            int left = (int)(config.ProtectionTime - time2);

            if (config.ShowMessage)
            {
                if (PlayerIsNew(ID))
                {
                    if (config.ShowTime)
                    {
                        SendReply(attacker, lang.GetMessage("cannot_attack_time", this, attacker.UserIDString), CheckLeft(left));
                        return;
                    }

                    SendReply(attacker, lang.GetMessage("cannot_attack_no_time", this, attacker.UserIDString));
                    return;
                }
                SendReply(attacker, lang.GetMessage("can_attack", this, attacker.UserIDString));
            }
        }

        private bool PlayerIsNew(ulong ID)
        {
            if (permission.UserHasPermission(ID.ToString(), NoobPerm)) return true;
            var outDouble = 0d;
            if (!storedData.players.TryGetValue(ID, out outDouble) || outDouble == -50d) return false;
            if (outDouble < config.ProtectionTime || outDouble == -25d) return true;
            return false;
        }

        private void RemoveCD(List<BasePlayer> List, BasePlayer player, float time = 0.1f)
        {
            if (player == null) return;
            timer.Once(time, () => {
                if (List.Contains(player)) List.Remove(player);
            });
        }

        private void RemoveClanP(ulong ID)
        {
            if (ID == 0u) return;

            string clan = string.Empty;
            var claninfo = ClanInfo.GetClanOf(ID);

            if (claninfo != null) clan = claninfo.clanName;
            else clan = Clans?.Call<string>("GetClanOf", ID) ?? string.Empty;

            if (string.IsNullOrEmpty(clan)) return;

            var ClanMembers = ClanInfo.FindClanByName(clan)?.members ?? new List<ulong>();
            if (ClanMembers.Count < 1) return;

            var list = storedData.players.Where(x => x.Value != -50d && ClanMembers.Contains(x.Key));
            if (list.Count() < 1) return;

            foreach (var member in list) storedData.players[member.Key] = -50d;
            Puts(lang.GetMessage("clan_lostnoob", this, null), clan);
        }

        private void RemoveInactive()
        {
            foreach (var entry in storedData.lastConnection)
            {
                var val = 0d;
                if (string.IsNullOrEmpty(entry.Value) || !storedData.players.TryGetValue(entry.Key, out val)) continue;
                if (val == -50d) continue;
                var tp = DateTime.Now.Subtract(Convert.ToDateTime(entry.Value));

                if (tp.TotalDays > config.InactivityRemove)
                {
                    Puts(lang.GetMessage("console_lostnoobstatus", this, null), entry.Key, config.InactivityRemove);
                    storedData.players[entry.Key] = -50d;
                }
            }
        }

        private void Refund(BasePlayer attacker, string name, BaseEntity ent)
        {
            if (!config.Refund || storedData.ItemList.Count < 1) return;

            foreach (var entry in storedData.ItemList)
            {
                if (name == entry.Value)
                {
                    if (config.RefundTimes == 0)
                    {
                        Item item = ItemManager.CreateByName(entry.Key, 1);
                        attacker.GiveItem(item);
                        SendReply(attacker, lang.GetMessage("refund_free", this, attacker.UserIDString), item.info.displayName.english);
                        return;
                    }

                    if ((storedData.AttackAttempts[attacker.userID]) <= config.RefundTimes)
                    {
                        int a = config.RefundTimes - (storedData.AttackAttempts[attacker.userID]);
                        Item item = ItemManager.CreateByName(entry.Key, 1);
                        attacker.GiveItem(item);

                        switch (a)
                        {
                            case 0:
                                {
                                    SendReply(attacker, lang.GetMessage("refund_last", this, attacker.UserIDString), item.info.displayName.english);
                                    return;
                                }

                            case 1:
                                {
                                    SendReply(attacker, lang.GetMessage("refund_1time", this, attacker.UserIDString), item.info.displayName.english);
                                    return;
                                }

                            default:
                                {
                                    SendReply(attacker, lang.GetMessage("refund_nTimes", this, attacker.UserIDString), item.info.displayName.english, a);
                                    return;
                                }

                        }
                    }
                }
            }
        }

        private void StartChecking()
        {
            timer.Every(config.Frequency, () => {
                RemoveInactive();
                Check();
            });
        }

        private void SteamCheck(ulong ID, bool connecting)
        {
            if (!config.CheckSteam) return;
            if (string.IsNullOrEmpty(config.ApiKey))
            {
                Puts(lang.GetMessage("steam_wrongapikey", this, null));
                return;
            }

            string date = "[" + DateTime.Now.ToString() + "] ";

            if (storedData.players.ContainsKey(ID))
            {
                if (storedData.players[ID] == -50d)
                {
                    LogToFile(this.Name, date + "Player " + ID + " is already marked as non noob", this, false);
                    return;
                }

                else if (storedData.players[ID] == -25d)
                {
                    LogToFile(this.Name, date + "Player " + ID + " is already marked as noob (-25)", this, false);
                    return;
                }
            }

            Puts(lang.GetMessage("steam_checkstart", this, null), ID);

            webrequest.Enqueue("http://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key=" + config.ApiKey + "&steamid=" + ID, null, (code, response) =>
            {
                if (code != 200)
                {
                    Puts(lang.GetMessage("steam_responsewrong", this, null));
                    LogToFile(this.Name, date + "Wrong response for " + ID + " (" + code + ")", this, false);
                    return;
                }

                var des = Utility.ConvertFromJson<SteamGames>(response);
                if (des?.response?.games == null)
                {
                    Puts(lang.GetMessage("steam_private", this, null), ID);
                    LogToFile(this.Name, date + "Steam profile of " + ID + " is private", this, false);
                    return;
                }

                foreach (var game in des.response.games)
                {
                    if (game.appid == 252490)
                    {
                        double hours = game.playtime_forever / 60d;

                        if (connecting) Puts(lang.GetMessage("steam_connected", this, null), ID, System.Math.Round(hours, 2));

                        if (hours >= config.SteamInGameTime)
                        {
                            Puts(lang.GetMessage("steam_marking", this, null), ID);
                            LogToFile(this.Name, date + ID + " has " + hours + "h in game", this, false);

                            if (!storedData.players.ContainsKey(ID))
                            {
                                LogToFile(this.Name, date + "Adding new entry for " + ID, this, false);
                                storedData.players.Add(ID, -50d);
                                return;
                            }

                            LogToFile(this.Name, date + "Overwriting existing entry for " + ID, this, false);
                            storedData.players[ID] = -50d;
                            return;
                        }

                        Puts(lang.GetMessage("console_notenough", this, null));
                    }
                }
            }, this);
        }

        #endregion
        #region Commands
        private Dictionary<string, string> AdminCommands = new Dictionary<string, string>
        {
            { "antinoob", "AntiNoobCommand" },
            { "refunditem", "RefundCmd" }
        };

        private void AntiNoobCommand(IPlayer player, string command, string[] args)
        {
            if (args.Length < 1)
            {
                player.Reply(lang.GetMessage("antinoobcmd_syntax", this, player.Id));
                return;
            }

            switch (args[0].ToLower())
            {
                case "addnoob":
                    {
                        if (args.Length < 2)
                        {
                            player.Reply(lang.GetMessage("antinoobcmd_addnoob_syntax", this, player.Id));
                            return;
                        }

                        var p = covalence.Players.FindPlayer(args[1]);
                        if (p == null)
                        {
                            player.Reply(lang.GetMessage("NoPlayerFound", this, player.Id), null, args[1]);
                            return;
                        }

                        foreach (var entry in storedData.players)
                        {
                            if (entry.Key == ulong.Parse(p.Id))
                            {
                                if (storedData.players[entry.Key] == -25d)
                                {
                                    player.Reply(lang.GetMessage("antinoobcmd_alreadynoob", this, player.Id), null, p.Name);
                                    return;
                                }

                                storedData.players[entry.Key] = -25d;
                                player.Reply(lang.GetMessage("antinoobcmd_marked", this, player.Id), null, p.Name);
                                return;
                            }
                        }
                        player.Reply(lang.GetMessage("NoPlayerFound", this, player.Id), null, args[1]);
                        return;
                    }

                case "removenoob":
                    {
                        if (args.Length < 2)
                        {
                            player.Reply(lang.GetMessage("antinoobcmd_removenoob_syntax", this, player.Id));
                            return;
                        }

                        var p = covalence.Players.FindPlayer(args[1]);
                        if (p == null)
                        {
                            player.Reply(lang.GetMessage("NoPlayerFound", this, player.Id), null, args[1]);
                        }

                        foreach (var entry in storedData.players)
                        {
                            if (entry.Key == ulong.Parse(p.Id))
                            {
                                if (storedData.players[entry.Key] == -50d)
                                {

                                    player.Reply(lang.GetMessage("antinoobcmd_missingnoob", this, player.Id), null, p.Name);
                                    return;
                                }

                                storedData.players[entry.Key] = -50d;
                                player.Reply(lang.GetMessage("antinoobcmd_removednoob", this, player.Id), null, p.Name);
                                return;
                            }
                        }

                        player.Reply(lang.GetMessage("NoPlayerFound", this, player.Id), null, args[1]);
                        return;
                    }

                case "checksteam":
                    {
                        if (string.IsNullOrEmpty(config.ApiKey))
                        {
                            player.Reply(lang.GetMessage("steam_wrongapikey", this, player.Id));
                            return;
                        }

                        foreach (BasePlayer p in BasePlayer.activePlayerList) SteamCheck(p.userID, false);
                        return;
                    }

                case "wipe":
                    {
                        if (args.Length < 2)
                        {
                            player.Reply(lang.GetMessage("antinoobcmd_wipe_syntax", this, player.Id));
                            return;
                        }

                        switch (args[1].ToLower())
                        {
                            case "all":
                                {
                                    storedData.lastConnection.Clear();
                                    storedData.playersWithNoData.Clear();
                                    storedData.FirstMessaged.Clear();
                                    storedData.AttackAttempts.Clear();
                                    storedData.ItemList.Clear();
                                    storedData.players.Clear();
                                    player.Reply(lang.GetMessage("dataFileWiped", this, player.Id));
                                    return;
                                }

                            case "playerdata":
                                {
                                    storedData.players.Clear();
                                    player.Reply(lang.GetMessage("dataFileWiped_playerdata", this, player.Id));
                                    return;
                                }

                            case "attempts":
                                {
                                    storedData.AttackAttempts.Clear();
                                    player.Reply(lang.GetMessage("dataFileWiped_attempts", this, player.Id));
                                    return;
                                }

                            default:
                                {
                                    player.Reply(lang.GetMessage("antinoobcmd_wipe_syntax", this, player.Id));
                                    return;
                                }
                        }
                    }

                default:
                    {
                        player.Reply(lang.GetMessage("antinoobcmd_syntax", this, player.Id));
                        return;
                    }
            }
        }

        [ChatCommand("checknew")]
        private void CheckNewCmd(BasePlayer player, string command, string[] args)
        {
            BaseEntity hitEnt = GetLookAtEntity(player);
            if (hitEnt == null)
            {
                SendReply(player, lang.GetMessage("NotLooking", this, player.UserIDString));
                return;
            }

            ulong owner = config.CheckFullOwnership ? FullOwner(hitEnt) : hitEnt.OwnerID;
            if (owner == 0u || !storedData.players.ContainsKey(owner)) return;
            MessagePlayer(player, owner);
        }

        [ChatCommand("entdebug")]
        private void EntDebugCmd(BasePlayer player, string command, string[] args)
        {
            if (!debug || !player.IsAdmin) return;

            BaseEntity ent = GetLookAtEntity(player);

            if (ent == null)
            {
                SendReply(player, "No entity");
                return;
            }

            var ownerid = (FullOwner(ent) != 0) ? FullOwner(ent) : ent.OwnerID;

            if (args.Length < 1)
            {
                SendReply(player, "OwnerID: " + ent.OwnerID);
                SendReply(player, "FullOwner: " + FullOwner(ent));

                if (storedData.players.ContainsKey(ownerid))
                {
                    var tiem = (int)storedData.players[ownerid];
                    switch (tiem)
                    {
                        case -25:
                            {
                                SendReply(player, "Time: -25d (manually set noob)");
                                break;
                            }

                        case -50:
                            {
                                SendReply(player, "Time: -50d (manually set non-noob or flagged when connecting (steam))");
                                break;
                            }

                        default:
                            {
                                SendReply(player, "Time: " + tiem + " ('natural' time)");
                                if (tiem >= config.ProtectionTime) SendReply(player, "Should be raidable");
                                else SendReply(player, "Shouldn't be raidable");
                                break;
                            }
                    }
                }

                else SendReply(player, "StoredData does not contain info for " + ownerid);
                return;
            }

            var t = ulong.Parse(args[0]);
            ent.OwnerID = t;
            ent.SendNetworkUpdate();
            SendReply(player, "Set OwnerID: " + ent.OwnerID);
            return;
        }

        private void RefundCmd(IPlayer p, string command, string[] args)
        {
            BasePlayer player = p.Object as BasePlayer;
            if (player == null) return;

            if (args.Length < 1)
            {
                p.Reply(lang.GetMessage("refunditem_help", this, p.Id));
                return;
            }

            Item helditem = player.GetActiveItem();

            switch (args[0].ToLower())
            {
                case "add":
                    {
                        if (player.GetActiveItem() == null)
                        {
                            p.Reply(lang.GetMessage("refunditem_needholditem", this, player.UserIDString));
                            return;
                        }

                        if (raidtools.ContainsKey(helditem.info.shortname))
                        {
                            if (!storedData.ItemList.ContainsKey(helditem.info.shortname))
                            {
                                storedData.ItemList.Add(helditem.info.shortname, raidtools[helditem.info.shortname]);
                                p.Reply(lang.GetMessage("refunditem_added", this, player.UserIDString), null, helditem.info.displayName.english);
                                return;
                            }

                            p.Reply(lang.GetMessage("refunditem_alreadyonlist", this, player.UserIDString));
                            return;
                        }

                        p.Reply(lang.GetMessage("refunditem_notexplosive", this, player.UserIDString));
                        return;
                    }

                case "remove":
                    {
                        if (player.GetActiveItem() == null)
                        {
                            p.Reply(lang.GetMessage("refunditem_needholditem", this, player.UserIDString));
                            return;
                        }

                        if (storedData.ItemList.ContainsKey(helditem.info.shortname))
                        {
                            storedData.ItemList.Remove(helditem.info.shortname);
                            p.Reply(lang.GetMessage("refunditem_removed", this, player.UserIDString), null, helditem.info.displayName.english);
                            return;
                        }

                        p.Reply(lang.GetMessage("refunditem_notonlist", this, player.UserIDString));
                        return;
                    }

                case "all":
                    {
                        foreach (var t in raidtools)
                            if (!storedData.ItemList.ContainsKey(t.Key)) storedData.ItemList.Add(t.Key, t.Value);

                        p.Reply(lang.GetMessage("refunditem_addedall", this, player.UserIDString));
                        return;
                    }

                case "clear":
                    {
                        storedData.ItemList.Clear();
                        p.Reply(lang.GetMessage("refunditem_cleared", this, player.UserIDString));
                        return;
                    }

                case "list":
                    {
                        if (storedData.ItemList.Count < 1)
                        {
                            p.Reply(lang.GetMessage("refunditem_empty", this, player.UserIDString));
                            return;
                        }

                        List<string> T2 = new List<string>();

                        foreach (var entry in storedData.ItemList)
                        {
                            Item item = ItemManager.CreateByName(entry.Key, 1);

                            if (item.info.displayName.english == null)
                                LogToFile(this.Name, "Failed to find display name for " + entry.Key, this, true);
                            T2.Add(item?.info?.displayName?.english);
                        }

                        string final = string.Join("\n", T2.ToArray());
                        p.Reply(lang.GetMessage("refunditem_list", this, player.UserIDString), null, final);
                        return;
                    }

                default:
                    {
                        p.Reply(lang.GetMessage("refunditem_help", this, player.UserIDString));
                        return;
                    }
            }
        }

        [ChatCommand("removenoob")]
        private void RemoveManualCmd(BasePlayer player, string command, string[] args)
        {
            if (args.Length < 1)
            {
                SendReply(player, lang.GetMessage("rn_manual", this, player.UserIDString));
                return;
            }

            if (args[0].ToLower() == "yes")
            {
                if (storedData.players.ContainsKey(player.userID))
                {
                    storedData.players[player.userID] = -50d;
                    SendReply(player, lang.GetMessage("rn_success", this, player.UserIDString));
                    return;
                }

                LogToFile(this.Name, "Didn't find any info for player " + player.userID + " when manually removing??", this, true);
                storedData.players.Add(player.userID, -50d);
                SendReply(player, lang.GetMessage("rn_success", this, player.UserIDString));
            }
        }
        #endregion
    }
}