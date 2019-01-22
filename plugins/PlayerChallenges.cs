using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("PlayerChallenges", "k1lly0u", "2.0.44")]
    [Description("Keep track of various statistics and set titles to players when certain criteria have been met or when they are a leader of a challenge category")]
    class PlayerChallenges : RustPlugin
    {
        #region Fields
        [PluginReference] Plugin BetterChat;
        [PluginReference] Plugin EventManager;
        [PluginReference] Plugin LustyMap;
        [PluginReference] Plugin Clans;
        [PluginReference] Plugin Friends;        

        ChallengeData chData;
        private DynamicConfigFile data;

        private Dictionary<ulong, StatData> statCache = new Dictionary<ulong, StatData>();
        private Dictionary<Challenges, LeaderData> titleCache = new Dictionary<Challenges, LeaderData>();
        private Dictionary<ulong, WoundedData> woundedData = new Dictionary<ulong, WoundedData>();

        private Hash<uint, Hash<ulong, float>> damageData = new Hash<uint, Hash<ulong, float>>();

        private static Dictionary<string, string> uiColors = new Dictionary<string, string>();

        private bool UIDisabled = false;
        #endregion

        #region UI Creation
        public static class UI
        {
            static public CuiElementContainer Container(string panelName, string aMin, string aMax, bool cursor = false)
            {
                var NewElement = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = uiColors["background"]},
                            RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                            CursorEnabled = cursor
                        },
                        new CuiElement().Parent = "Overlay",
                        panelName
                    }
                };
                return NewElement;
            }

            static public void Panel(ref CuiElementContainer container, string panel, string aMin, string aMax, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = uiColors["panel"]},
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    CursorEnabled = cursor
                },
                panel);
            }

            static public void Label(ref CuiElementContainer container, string panel, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter, float fadein = 0f)
            {               
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Align = align, FadeIn = fadein, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);

            }

            static public void Button(ref CuiElementContainer container, string panel, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter, float fadein = 0f)
            {                
                container.Add(new CuiButton
                {
                    Button = { Color = uiColors["button"], Command = command, FadeIn = fadein },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel);
            }

            static public string Color(string hexColor, float alpha)
            {
                if (hexColor.StartsWith("#"))
                    hexColor = hexColor.Substring(1);
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }
        #endregion

        #region UI Leaderboard
        private string UIMain = "PCUI_Main";

        private void CreateMenu(BasePlayer player)
        {
            CloseMap(player);
            CuiHelper.DestroyUi(player, UIMain);            
            CreateMenuContents(player, 0);
        }

        private void CreateMenuContents(BasePlayer player, int page = 0)
        {
            CuiElementContainer container = UI.Container(UIMain, "0 0", "1 1", true);
            UI.Panel(ref container, UIMain, "0.005 0.93", "0.995 0.99");
            UI.Label(ref container, UIMain, $"<color={configData.Colors.TextColor1}>{MSG("UITitle").Replace("{Version}", Version.ToString())}</color>", 22, "0.05 0.93", "0.6 0.99", TextAnchor.MiddleLeft);

            var elements = configData.ChallengeSettings.Where(x => x.Value.Enabled).OrderByDescending(x => x.Value.UIPosition).Reverse().ToArray();
            int count = page * 5;
            int number = 0;
            float dimension = 0.19f;
            for (int i = count; i < count + 5; i++)
            {
                if (elements.Length < i + 1) continue;
                float leftPos = 0.005f + (number * (dimension + 0.01f));
                AddMenuStats(ref container, UIMain, elements[i].Key, leftPos, 0.01f, leftPos + dimension, 0.92f);
                number++;
            }

            if (page > 0)
                UI.Button(ref container, UIMain, "Previous", 16, "0.63 0.94", "0.73 0.98", $"PCUI_ChangePage {page - 1}");
            if (elements.Length > count + 5)
                UI.Button(ref container, UIMain, "Next", 16, "0.74 0.94", "0.84 0.98", $"PCUI_ChangePage {page + 1}");

            UI.Button(ref container, UIMain, "Close", 16, "0.85 0.94", "0.95 0.98", "PCUI_DestroyAll");
            CuiHelper.AddUi(player, container);
        }

        private void AddMenuStats(ref CuiElementContainer MenuElement, string panel, Challenges type, float left, float bottom, float right, float top)
        {
            if (configData.ChallengeSettings[type].Enabled)
            {
                UI.Panel(ref MenuElement, UIMain, $"{left} {bottom}", $"{right} {top}");
                UI.Label(ref MenuElement, UIMain, GetLeaders(type), 16, $"{left + 0.005f} {bottom + 0.01f}", $"{right - 0.005f} {top - 0.01f}", TextAnchor.UpperLeft);
            }       
        }

        #region UI Commands       
        [ConsoleCommand("PCUI_ChangePage")]
        private void cmdChangePage(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            CuiHelper.DestroyUi(player, UIMain);
            var page = int.Parse(arg.GetString(0));
            CreateMenuContents(player, page);
        }
        [ConsoleCommand("PCUI_DestroyAll")]
        private void cmdDestroyAll(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;           
            DestroyUI(player);
            OpenMap(player);
        }
        #endregion

        #region UI Functions
        private string GetLeaders(Challenges type)
        {
            var listNames = $" -- <color={configData.Colors.TextColor1}>{MSG(type.ToString()).ToUpper()}</color>\n\n";

            var userStats = new List<KeyValuePair<string, int>>();

            foreach (var entry in statCache)
            {
                var name = entry.Value.DisplayName;                
                userStats.Add(new KeyValuePair<string, int>(name, entry.Value.Stats[type]));
            }                

            var leaders = userStats.OrderByDescending(a => a.Value).Take(25);

            int i = 1;

            foreach (var entry in leaders)
            {
                listNames += $"{i}.  - <color={configData.Colors.TextColor1}>{entry.Value}</color> -  {entry.Key}\n";
                i++;            
            }
            return listNames;
        }
        private object GetTypeFromString(string name)
        {
            foreach(var type in Enum.GetValues(typeof(Challenges)))
            {
                if (type.ToString() == name)
                    return type;
            }
            return null;
        }
        private void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UIMain);
        }
        #endregion
        #endregion

        #region External Calls
        private void CloseMap(BasePlayer player)
        {
            if (LustyMap)
            {
                LustyMap.Call("DisableMaps", player);
            }
        }
        private void OpenMap(BasePlayer player)
        {
            if (LustyMap)
            {
                LustyMap.Call("EnableMaps", player);
            }
        }
        private bool IsPlaying(BasePlayer player)
        {
            if (EventManager)
            {
                var isPlaying = EventManager.Call("isPlaying", player);
                if (isPlaying is bool && (bool)isPlaying)
                    return true;
            }
            return false;
        }
        private bool IsClanmate(ulong playerId, ulong friendId)
        {
            if (!Clans) return false;
            object playerTag = Clans?.Call("GetClanOf", playerId);
            object friendTag = Clans?.Call("GetClanOf", friendId);
            if (playerTag is string && friendTag is string)
                if (playerTag == friendTag) return true;
            return false;
        }
        private bool IsFriend(ulong playerId, ulong friendId)
        {
            if (!Friends) return false;
            object isFriend = Friends?.Call("IsFriend", playerId, friendId);
            if (isFriend is bool && (bool)isFriend)
                return true;
            return false;            
        }
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            data = Interface.Oxide.DataFileSystem.GetFile("challenge_data");
            lang.RegisterMessages(Messages, this);
        }

        private void OnServerInitialized()
        {
            LoadData();
            CheckValidData();
            RegisterTitles();
            RegisterGroups();
            AddAllUsergroups();

            if (configData.Options.NPCKillSeperate)
                configData.ChallengeSettings[Challenges.NPCKills].Enabled = false;

            uiColors = new Dictionary<string, string>()
            {
                ["background"] = UI.Color(configData.Colors.Background.Color, configData.Colors.Background.Alpha),
                ["button"] = UI.Color(configData.Colors.Button.Color, configData.Colors.Button.Alpha),
                ["panel"] = UI.Color(configData.Colors.Panel.Color, configData.Colors.Panel.Alpha),
            };

            if (configData.Options.UseUpdateTimer)          
                CheckUpdateTimer();
            
            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerInit(player);
        }

        private void Unload()
        {
            SaveData();
            foreach (var player in BasePlayer.activePlayerList)
                DestroyUI(player);
            RemoveAllUsergroups();
            uiColors = null;
        }

        private void OnPluginLoaded(Plugin plugin)
        {
            if (plugin?.Title == "BetterChat")
                RegisterTitles();
        }

        private void OnServerSave() => SaveData();

        private void OnPlayerInit(BasePlayer player)
        {
            if (statCache.ContainsKey(player.userID))
            {
                if (statCache[player.userID].DisplayName != player.displayName)
                    statCache[player.userID].DisplayName = player.displayName;                             
            }
        }

        private void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            if (player == null || player is NPCPlayer || !configData.ChallengeSettings[Challenges.RocketsFired].Enabled) return;            
            AddPoints(player, Challenges.RocketsFired, 1);
        }

        private void OnHealingItemUse(HeldEntity item, BasePlayer target)
        {
            var player = item.GetOwnerPlayer();
            if (player == null || player is NPCPlayer) return;
            if (player != target && configData.ChallengeSettings[Challenges.PlayersHealed].Enabled)
            {
                AddPoints(player, Challenges.PlayersHealed, 1);
            }            
        }

        private void OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            var player = task.owner;
            if (player == null || player is NPCPlayer) return;

            if (item.info.category == ItemCategory.Attire && configData.ChallengeSettings[Challenges.ClothesCrafted].Enabled)
                AddPoints(player, Challenges.ClothesCrafted, 1);
            if (item.info.category == ItemCategory.Weapon && configData.ChallengeSettings[Challenges.WeaponsCrafted].Enabled)
                AddPoints(player, Challenges.WeaponsCrafted, 1);
        }

        private void OnPlantGather(PlantEntity plant, Item item, BasePlayer player)
        {
            if (player == null || player is NPCPlayer || !configData.ChallengeSettings[Challenges.PlantsGathered].Enabled) return;
            AddPoints(player, Challenges.PlantsGathered, 1);
        }

        private void OnCollectiblePickup(Item item, BasePlayer player, CollectibleEntity entity)
        {
            if (item == null) return;
            if (player == null || player is NPCPlayer || !configData.ChallengeSettings[Challenges.PlantsGathered].Enabled) return;
            if (plantShortnames.Contains(item?.info?.shortname))
                AddPoints(player, Challenges.PlantsGathered, 1);
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            var player = entity.ToPlayer();
            if (player == null || player is NPCPlayer || dispenser == null) return;

            if (dispenser.gatherType == ResourceDispenser.GatherType.Tree && configData.ChallengeSettings[Challenges.WoodGathered].Enabled)
                AddPoints(player, Challenges.WoodGathered, item.amount);

            if (dispenser.gatherType == ResourceDispenser.GatherType.Ore && configData.ChallengeSettings[Challenges.RocksGathered].Enabled)
                AddPoints(player, Challenges.RocksGathered, item.amount);               
        }

        private void OnEntityBuilt(Planner plan, GameObject go)
        {           
            BasePlayer player = plan?.GetOwnerPlayer();
            if (player == null || player is NPCPlayer || !configData.ChallengeSettings[Challenges.StructuresBuilt].Enabled) return;

            AddPoints(player, Challenges.StructuresBuilt, 1);
        }

        private void CanBeWounded(BasePlayer player, HitInfo hitInfo)
        {
            if (player == null || player is NPCPlayer || hitInfo == null) return;

            var attacker = hitInfo.InitiatorPlayer;
            if (attacker != null)
            {
                if (attacker == player || IsPlaying(attacker) || IsFriend(attacker.userID, player.userID) || IsClanmate(attacker.userID, player.userID)) return;
                woundedData[player.userID] = new WoundedData {distance = Vector3.Distance(player.transform.position, attacker.transform.position), attackerId = attacker.userID };
            }            
        }

        private void OnPlayerRecover(BasePlayer player)
        {
            if (player == null || player is NPCPlayer)
                return;

            if (woundedData.ContainsKey(player.userID))
                woundedData.Remove(player.userID);
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null)
                return;

            if (entity is BaseHelicopter || entity is BradleyAPC)
            {
                BasePlayer attacker = info.InitiatorPlayer;
                if (attacker == null)
                    return;

                Hash<ulong, float> entityAttackers;
                if (!damageData.TryGetValue(entity.net.ID, out entityAttackers))                
                    entityAttackers = damageData[entity.net.ID] = new Hash<ulong, float>();

                entityAttackers[attacker.userID] += info.damageTypes.Total();
            }
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null)
                return;

            BasePlayer attacker = info.InitiatorPlayer;
            if (attacker != null)
            {
                if (!attacker.IsNpc)
                    CheckEntry(attacker);
            }
                      
            if (entity is BasePlayer)
            {                
                BasePlayer victim = entity.ToPlayer();

                if (attacker == null || attacker is NPCPlayer || victim == null)
                    return;

                bool isExplosiveKill = false;

                if (attacker == victim || IsPlaying(attacker) || IsFriend(attacker.userID, victim.userID) || IsClanmate(attacker.userID, victim.userID) || (configData.Options.IgnoreSleepers && victim.IsSleeping())) return;
                
                if (info.isHeadshot && configData.ChallengeSettings[Challenges.Headshots].Enabled && !attacker.IsNpc)
                    AddPoints(attacker, Challenges.Headshots, 1);

                string weapon = info?.Weapon?.GetItem()?.info?.shortname;
                if (!string.IsNullOrEmpty(weapon) && !attacker.IsNpc)
                {
                    isExplosiveKill = weapon == "explosive.timed" || weapon == "grenade.f1" || weapon == "grenade.beancan";

                    if (victim.IsNpc && configData.Options.NPCKillSeperate)
                    {
                        if (configData.ChallengeSettings[Challenges.NPCKills].Enabled)
                            AddPoints(attacker, Challenges.NPCKills, 1);
                    }
                    else
                    {
                        if (bladeShortnames.Contains(weapon) && configData.ChallengeSettings[Challenges.BladeKills].Enabled)
                            AddPoints(attacker, Challenges.BladeKills, 1);
                        else if (meleeShortnames.Contains(weapon) && configData.ChallengeSettings[Challenges.MeleeKills].Enabled)
                            AddPoints(attacker, Challenges.MeleeKills, 1);
                        else if (weapon == "bow.hunting" && configData.ChallengeSettings[Challenges.ArrowKills].Enabled)
                            AddPoints(attacker, Challenges.ArrowKills, 1);
                        else if (weapon == "pistol.revolver" && configData.ChallengeSettings[Challenges.RevolverKills].Enabled)
                            AddPoints(attacker, Challenges.RevolverKills, 1);
                        else if (configData.ChallengeSettings[Challenges.PlayersKilled].Enabled)
                            AddPoints(attacker, Challenges.PlayersKilled, 1);
                    }
                }

                if (isExplosiveKill && configData.Options.IgnoreExplosiveDistance)
                    return;

                float distance = Vector3.Distance(attacker.transform.position, entity.transform.position);
                if (woundedData.ContainsKey(victim.userID))
                {
                    WoundedData woundData = woundedData[victim.userID];
                    if (attacker.userID == woundData.attackerId)
                        distance = woundData.distance;
                    woundedData.Remove(victim.userID);
                }
                if (!attacker.IsNpc)
                    AddDistance(attacker, victim.IsNpc && configData.Options.NPCPVEKills ? Challenges.PVEKillDistance : Challenges.PVPKillDistance, (int)distance);

            }
            else if (entity.GetComponent<BaseNpc>() != null)
            {
                if (attacker == null || attacker.IsNpc)
                    return;

                float distance = Vector3.Distance(attacker.transform.position, entity.transform.position);
                AddDistance(attacker, Challenges.PVEKillDistance, (int)distance);
                AddPoints(attacker, Challenges.AnimalKills, 1);
            }
            else
            {
                Hash<ulong, float> entityAttackers;
                if (damageData.TryGetValue(entity.net.ID, out entityAttackers))
                {
                    ulong mostDamage = entityAttackers.OrderByDescending(x => x.Value)?.First().Key ?? 0U;
                    if (mostDamage != 0U)                    
                        AddPoints(mostDamage, entity is BaseHelicopter ? Challenges.HelicopterKills : Challenges.APCKills, 1);                    
                    damageData.Remove(entity.net.ID);
                }
            }
        }

        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (player == null || entity == null || !configData.ChallengeSettings[Challenges.ExplosivesThrown].Enabled) return;
            if (entity.ShortPrefabName == "survey_charge.deployed" && configData.Options.IgnoreSurveyCharges) return;
            if (entity.ShortPrefabName == "flare.deployed" && configData.Options.IgnoreFlares) return;
            if (entity.ShortPrefabName == "grenade.smoke.deployed" && configData.Options.IgnoreSupplySignals) return;
            AddPoints(player, Challenges.ExplosivesThrown, 1);
        }

        private void OnStructureRepair(BaseCombatEntity block, BasePlayer player)
        {
            if (player == null || !configData.ChallengeSettings[Challenges.StructuresRepaired].Enabled) return;
            if (block.health < block.MaxHealth())
                AddPoints(player, Challenges.StructuresRepaired, 1);
        }
        #endregion

        #region Hooks
        [HookMethod("CompletedQuest")]
        public void CompletedQuest(BasePlayer player)
        {
            CheckEntry(player);
            AddPoints(player, Challenges.QuestsCompleted, 1);            
        }
        #endregion

        #region Functions        
        private void AddPoints(BasePlayer player, Challenges type, int amount)
        {
            if (configData.Options.IgnoreAdmins && player.IsAdmin || player.IsNpc)
                return;

            CheckEntry(player);
            statCache[player.userID].Stats[type] += amount;            
            CheckForUpdate(player.userID, type);
        }

        private void AddPoints(ulong playerId, Challenges type, int amount)
        {
            if (configData.Options.IgnoreAdmins)
            {
                if (ServerUsers.Get(playerId)?.group == ServerUsers.UserGroup.Moderator || ServerUsers.Get(playerId)?.group == ServerUsers.UserGroup.Owner)
                    return;
            }

            CheckEntry(playerId);
            statCache[playerId].Stats[type] += amount;
            CheckForUpdate(playerId, type);
        }

        private void AddDistance(BasePlayer player, Challenges type, int amount)
        {
            if (configData.Options.IgnoreAdmins && player.IsAdmin || player.IsNpc) return;
            CheckEntry(player);
            if (statCache[player.userID].Stats[type] < amount)
                statCache[player.userID].Stats[type] = amount;
            CheckForUpdate(player.userID, type);
        }

        private void CheckForUpdate(ulong playerId, Challenges type)
        {
            if (titleCache[type].UserID == playerId)
            {
                titleCache[type].Count = statCache[playerId].Stats[type];
                return;
            }
            if (!configData.Options.UseUpdateTimer)
            {
                if (statCache[playerId].Stats[type] > titleCache[type].Count)
                {
                    SwitchLeader(playerId, titleCache[type].UserID, type);
                }
            }         
        }

        private void SwitchLeader(ulong newId, ulong oldId, Challenges type)
        {
            var name = GetGroupName(type);

            if (configData.Options.UseOxideGroups)
            {      
                if (oldId != 0U && permission.GroupExists(name))
                    RemoveUserFromGroup(name, oldId.ToString());
                if (newId != 0U && permission.GroupExists(name))
                    AddUserToGroup(name, newId.ToString());                
            }

            titleCache[type] = new LeaderData
            {
                Count = statCache[newId].Stats[type],
                DisplayName = statCache[newId].DisplayName,
                UserID = newId
            };

            if (configData.Options.AnnounceNewLeaders)
            {
                string message = MSG("newLeader")
                    .Replace("{playername}", $"<color={configData.Colors.TextColor1}>{statCache[newId].DisplayName}</color><color={configData.Colors.TextColor2}>")
                    .Replace("{ctype}", $"</color><color={configData.Colors.TextColor1}>{MSG(type.ToString())}</color>");
                PrintToChat(message);
            }            
        }
      
        private void CheckUpdateTimer()
        {
            if ((GrabCurrentTime() - chData.LastUpdate) > configData.Options.UpdateTimer)
            {
                var updates = new Dictionary<Challenges, UpdateInfo>();
                foreach (var type in Enum.GetValues(typeof(Challenges)))
                {
                    bool hasChanged = false;
                    UpdateInfo info = new UpdateInfo
                    {
                        newId = titleCache[(Challenges)type].UserID,
                        oldId = titleCache[(Challenges)type].UserID,
                        count = titleCache[(Challenges)type].Count
                    };
                    foreach (var player in statCache)
                    {
                        if (info.oldId == player.Key) continue;
                        if (player.Value.Stats[(Challenges)type] > info.count)
                        {
                            hasChanged = true;
                            info.newId = player.Key;
                            info.count = player.Value.Stats[(Challenges)type];
                        }
                    }
                    if (hasChanged)
                        SwitchLeader(info.newId, info.oldId, (Challenges)type);
                }               
            }
            else
            {
                var timeRemaining = ((configData.Options.UpdateTimer - (GrabCurrentTime() - chData.LastUpdate)) * 60) * 60;
                timer.Once((int)timeRemaining + 10, () => CheckUpdateTimer());
            }
        }

        class UpdateInfo
        {
            public ulong newId;
            public ulong oldId;
            public int count;
        }
        #endregion

        #region Chat Commands
        [ChatCommand("pc")]
        private void cmdPC(BasePlayer player, string command, string[] args)
        {
            if (!UIDisabled)
                CreateMenu(player);
            else SendReply(player, MSG("UIDisabled", player.UserIDString));
        }

        [ChatCommand("pc_wipe")]
        private void cmdPCWipe(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            RemoveAllUsergroups();
            titleCache = new Dictionary<Challenges, LeaderData>();
            statCache = new Dictionary<ulong, StatData>();            
            CheckValidData();
            SendReply(player, MSG("dataWipe", player.UserIDString));
            SaveData();
        }

        [ConsoleCommand("pc_wipe")]
        private void ccmdPCWipe(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            RemoveAllUsergroups();
            titleCache = new Dictionary<Challenges, LeaderData>();
            statCache = new Dictionary<ulong, StatData>();
            CheckValidData();
            SendReply(arg, MSG("dataWipe"));
            SaveData();
        }
        #endregion

        #region Helper Methods
        private void CheckEntry(BasePlayer player)
        {
            if (!statCache.ContainsKey(player.userID))
            {
                statCache.Add(player.userID, new StatData
                {
                    DisplayName = player.displayName,
                    Stats = new Dictionary<Challenges, int>()
                });

                foreach (var type in Enum.GetValues(typeof(Challenges)))
                    statCache[player.userID].Stats.Add((Challenges)type, 0);
            }
        }

        private void CheckEntry(ulong playerId)
        {
            if (!statCache.ContainsKey(playerId))
            {
                statCache.Add(playerId, new StatData
                {
                    DisplayName = covalence.Players.FindPlayerById(playerId.ToString())?.Name ?? "NoName",
                    Stats = new Dictionary<Challenges, int>()
                });
                foreach (var type in Enum.GetValues(typeof(Challenges)))
                    statCache[playerId].Stats.Add((Challenges)type, 0);
            }
        }

        private string GetGroupName(Challenges type) => configData.ChallengeSettings[type].Title; 
        
        private double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).Hours;
        #endregion

        #region Titles and Groups
        private void RegisterGroups()
        {
            if (!configData.Options.UseOxideGroups) return;           
            foreach (var type in Enum.GetValues(typeof(Challenges)))
                RegisterGroup((Challenges)type);
        }

        private void RegisterGroup(Challenges type)
        {
            var name = GetGroupName(type);
            if (!permission.GroupExists(name))
            {
                permission.CreateGroup(name, string.Empty, 0);                
            }
        }

        private void RegisterTitles()
        {
            if (!configData.Options.UseBetterChat || !BetterChat)
                return;                    
            BetterChat?.Call("API_RegisterThirdPartyTitle", new object[] { this, new Func<IPlayer, string>(GetPlayerTitles) });
        }

        private string GetPlayerTitles(IPlayer player)
        {
            if (!configData.Options.UseBetterChat) return string.Empty;
            string playerTitle = string.Empty;
            int count = 0;
            var titles = titleCache.OrderByDescending(x => configData.ChallengeSettings[x.Key].Priority).Reverse();
            foreach (var title in titles)
            {
                if (!configData.ChallengeSettings[title.Key].Enabled || title.Value.UserID == 0U) continue;
                if (title.Value.UserID.ToString() == player.Id)
                {
                    playerTitle += $"{(count > 0 ? " " : "")}{configData.Options.TagFormat.Replace("{TAG}", GetGroupName(title.Key))}";
                    count++;
                    if (count >= configData.Options.MaximumTags)
                        break;
                }
            }
            return count == 0 ? string.Empty : $"[{configData.Colors.TitleColor}]{playerTitle}[/#]";
        }

        private void AddAllUsergroups()
        {
            if (configData.Options.UseOxideGroups)
            {
                foreach (var type in titleCache)
                {
                    var name = GetGroupName(type.Key);
                    if (titleCache[type.Key].UserID == 0 || !GroupExists(name)) continue;
                    if (!UserInGroup(name, titleCache[type.Key].UserID.ToString()))
                        AddUserToGroup(name, titleCache[type.Key].UserID.ToString());
                }
            }
        }

        private void RemoveAllUsergroups()
        {
            if (configData.Options.UseOxideGroups)
            {
                foreach (var type in titleCache)
                {
                    var name = GetGroupName(type.Key);
                    if (titleCache[type.Key].UserID == 0 || !GroupExists(name)) continue;
                    if (UserInGroup(name, titleCache[type.Key].UserID.ToString()))
                        RemoveUserFromGroup(name, titleCache[type.Key].UserID.ToString());
                }
            }
        }

        private bool GroupExists(string name) => permission.GroupExists(name);
        private bool UserInGroup(string name, string playerId) => permission.UserHasGroup(playerId, name);
        private void AddUserToGroup(string name, string playerId) => permission.AddUserGroup(playerId, name);
        private void RemoveUserFromGroup(string name, string playerId) => permission.RemoveUserGroup(playerId, name);
        #endregion

        #region Config        
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Challenge Settings")]
            public Dictionary<Challenges, ChallengeInfo> ChallengeSettings { get; set; }
            public Option Options { get; set; }
            public TextColor Colors { get; set; }

            public class ChallengeInfo
            {
                [JsonProperty(PropertyName = "Title for name tag")]
                public string Title;
                [JsonProperty(PropertyName = "Enable this challenge")]
                public bool Enabled;
                [JsonProperty(PropertyName = "Position in the UI leaderboard")]
                public int UIPosition;
                [JsonProperty(PropertyName = "Title priority")]
                public int Priority;
            }
            public class Option
            {
                [JsonProperty(PropertyName = "Ignore kills against sleeping players (Players killed)")]
                public bool IgnoreSleepers;
                [JsonProperty(PropertyName = "Ignore explosive kill distance")]
                public bool IgnoreExplosiveDistance;
                [JsonProperty(PropertyName = "Kills against NPC players are counted seperate to player kills")]
                public bool NPCKillSeperate;
                [JsonProperty(PropertyName = "NPC kill distance counts as PVE distance")]
                public bool NPCPVEKills;
                [JsonProperty(PropertyName = "Show challenge leader title tags (Requires BetterChat)")]
                public bool UseBetterChat;
                [JsonProperty(PropertyName = "Ignore all statistics recorded by admins")]
                public bool IgnoreAdmins;
                [JsonProperty(PropertyName = "Ignore kills for event players (Players killed)")]
                public bool IgnoreEventKills;
                [JsonProperty(PropertyName = "Ignore supply signals thrown (Explosives thrown)")]
                public bool IgnoreSupplySignals;
                [JsonProperty(PropertyName = "Ignore survey charges thrown (Explosives thrown)")]
                public bool IgnoreSurveyCharges;
                [JsonProperty(PropertyName = "Ignore flares thrown (Explosives thrown)")]
                public bool IgnoreFlares;
                [JsonProperty(PropertyName = "Broadcast new challenge leaders to chat")]
                public bool AnnounceNewLeaders;
                [JsonProperty(PropertyName = "Update leaders on a timer (Recommended)")]
                public bool UseUpdateTimer;
                [JsonProperty(PropertyName = "Create and use Oxide groups for each challenge type")]
                public bool UseOxideGroups;
                [JsonProperty(PropertyName = "Update timer (hours)")]
                public int UpdateTimer;
                [JsonProperty(PropertyName = "Maximum tags to display (Requires BetterChat)")]
                public int MaximumTags;               
                [JsonProperty(PropertyName = "Format of tags displayed (Requires BetterChat)")]
                public string TagFormat;
            }
            public class TextColor
            {
                [JsonProperty(PropertyName = "Primary message color (hex)")]
                public string TextColor1;
                [JsonProperty(PropertyName = "Secondary message color (hex)")]
                public string TextColor2;
                [JsonProperty(PropertyName = "Title color (hex) (Requires BetterChat)")]
                public string TitleColor;

                [JsonProperty(PropertyName = "UI Color - Background")]
                public UIColor Background;
                [JsonProperty(PropertyName = "UI Color - Panel")]
                public UIColor Panel;
                [JsonProperty(PropertyName = "UI Color - Button")]
                public UIColor Button;

                public class UIColor
                {
                    [JsonProperty(PropertyName = "Color (hex)")]
                    public string Color;
                    [JsonProperty(PropertyName = "Alpha (0.0 - 1.0)")]
                    public float Alpha;
                }
            }

            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();
        
        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                ChallengeSettings = new Dictionary<Challenges, ConfigData.ChallengeInfo>
                {
                    {
                        Challenges.AnimalKills, new ConfigData.ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Hunter",
                            UIPosition = 0,
                            Priority = 5
                        }
                    },
                    {
                        Challenges.ArrowKills, new ConfigData.ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Archer",
                            UIPosition = 1,
                            Priority = 11
                        }
                    },
                    {
                        Challenges.StructuresBuilt, new ConfigData.ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Architect",
                            UIPosition = 2,
                            Priority = 12
                        }
                    },
                    {
                        Challenges.ClothesCrafted, new ConfigData.ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Tailor",
                            UIPosition = 3,
                            Priority = 19
                        }
                    },
                    {
                        Challenges.ExplosivesThrown, new ConfigData.ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Bomb-tech",
                            UIPosition = 4,
                            Priority = 10
                        }
                    },
                    {
                        Challenges.Headshots, new ConfigData.ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Assassin",
                            UIPosition = 5,
                            Priority = 1
                        }
                    },
                    {
                        Challenges.PlayersHealed, new ConfigData.ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Medic",
                            UIPosition = 6,
                            Priority = 18
                        }
                    },
                    {
                        Challenges.PlayersKilled, new ConfigData.ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Murderer",
                            UIPosition = 7,
                            Priority = 2
                        }
                    },
                    {
                        Challenges.MeleeKills, new ConfigData.ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Fighter",
                            UIPosition = 8,
                            Priority = 3
                        }
                    },
                    {
                        Challenges.PlantsGathered, new ConfigData.ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Harvester",
                            UIPosition = 9,
                            Priority = 17
                        }
                    },
                    {
                        Challenges.PVEKillDistance, new ConfigData.ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Deadshot",
                            UIPosition = 10,
                            Priority = 6
                        }
                    },
                    {
                        Challenges.PVPKillDistance, new ConfigData.ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Sniper",
                            UIPosition = 11,
                            Priority = 4
                        }
                    },
                    {
                        Challenges.StructuresRepaired, new ConfigData.ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Handyman",
                            UIPosition = 12,
                            Priority = 13
                        }
                    },
                    {
                        Challenges.RevolverKills, new ConfigData.ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Gunslinger",
                            UIPosition = 13,
                            Priority = 7
                        }
                    },
                    {
                        Challenges.RocketsFired, new ConfigData.ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Rocketeer",
                            UIPosition = 14,
                            Priority = 8
                        }
                    },
                    {
                        Challenges.RocksGathered, new ConfigData.ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Miner",
                            UIPosition = 15,
                            Priority = 16
                        }
                    },
                    {
                        Challenges.BladeKills, new ConfigData.ChallengeInfo
                        {
                            Enabled = true,
                            Title = "BladeKillsman",
                            UIPosition = 16,
                            Priority = 9
                        }
                    },
                    {
                        Challenges.WeaponsCrafted, new ConfigData.ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Gunsmith",
                            UIPosition = 17,
                            Priority = 14
                        }
                    },
                    {
                        Challenges.WoodGathered, new ConfigData.ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Lumberjack",
                            UIPosition = 18,
                            Priority = 15
                        }
                    },
                    {
                        Challenges.HelicopterKills, new ConfigData.ChallengeInfo
                        {
                            Enabled = true,
                            Title = "HeliHunter",
                            UIPosition = 19,
                            Priority = 20
                        }
                    },
                    {
                        Challenges.APCKills, new ConfigData.ChallengeInfo
                        {
                            Enabled = true,
                            Title = "TankHunter",
                            UIPosition = 20,
                            Priority = 21
                        }
                    },
                    {
                        Challenges.NPCKills, new ConfigData.ChallengeInfo
                        {
                            Enabled = true,
                            Title = "BotHunter",
                            UIPosition = 21,
                            Priority = 22
                        }
                    },
                    {
                        Challenges.QuestsCompleted, new ConfigData.ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Adventurer",
                            UIPosition = 22,
                            Priority = 23
                        }
                    }
                },

                Options = new ConfigData.Option
                {
                    AnnounceNewLeaders = false,
                    IgnoreAdmins = true,
                    IgnoreSleepers = true,
                    IgnoreSupplySignals = false,
                    NPCKillSeperate = true,
                    NPCPVEKills = true,
                    IgnoreSurveyCharges = false,
                    IgnoreFlares = true,
                    IgnoreEventKills = true,
                    MaximumTags = 2,
                    TagFormat = "[{TAG}]",
                    UseBetterChat = true,
                    UseOxideGroups = false,
                    UseUpdateTimer = false,
                    UpdateTimer = 168
                },
                Colors = new ConfigData.TextColor
                {
                    TextColor1 = "#ce422b",
                    TextColor2 = "#939393",
                    TitleColor = "#ce422b",
                    Background = new ConfigData.TextColor.UIColor
                    {
                        Alpha = 0.98f,
                        Color = "#2b2b2b"
                    },
                    Panel = new ConfigData.TextColor.UIColor
                    {
                        Alpha = 1f,
                        Color = "#404141"
                    },
                    Button = new ConfigData.TextColor.UIColor
                    {
                        Alpha = 1f,
                        Color = "#393939"
                    }
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();
            if (configData.Version < new VersionNumber(2, 0, 30))
                configData = baseConfig;

            if (configData.Version < new VersionNumber(2, 0, 41))
            {
                configData.Options.IgnoreFlares = true;
                configData.Options.NPCKillSeperate = true;
                configData.Options.NPCPVEKills = true;
                if (!configData.ChallengeSettings.ContainsKey(Challenges.APCKills))
                    configData.ChallengeSettings.Add(Challenges.APCKills, baseConfig.ChallengeSettings[Challenges.APCKills]);
                if (!configData.ChallengeSettings.ContainsKey(Challenges.HelicopterKills))
                    configData.ChallengeSettings.Add(Challenges.HelicopterKills, baseConfig.ChallengeSettings[Challenges.HelicopterKills]);
                if (!configData.ChallengeSettings.ContainsKey(Challenges.NPCKills))
                    configData.ChallengeSettings.Add(Challenges.NPCKills, baseConfig.ChallengeSettings[Challenges.NPCKills]);
            }

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }       
        #endregion

        #region Data Management
        private void SaveData()
        {
            chData.Stats = statCache;
            chData.Titles = titleCache;
            data.WriteObject(chData);
        }

        private void LoadData()
        {
            try
            {
                chData = data.ReadObject<ChallengeData>();
                statCache = chData.Stats;
                titleCache = chData.Titles;
            }
            catch
            {
                chData = new ChallengeData();
            }
        }

        private void CheckValidData()
        {
            if (titleCache.Count < Enum.GetValues(typeof(Challenges)).Length)
            {
                foreach (var type in Enum.GetValues(typeof(Challenges)))
                {
                    if (!titleCache.ContainsKey((Challenges)type))
                        titleCache.Add((Challenges)type, new LeaderData());
                }
            }
            for (int i = statCache.Count - 1; i >= 0; i--)
            {
                var player = statCache.ElementAt(i);
                if (player.Key < 76560000000000000)
                {
                    statCache.Remove(player.Key);
                    continue;
                }

                foreach (var type in Enum.GetValues(typeof(Challenges)))
                {
                    if (!player.Value.Stats.ContainsKey((Challenges)type))
                        player.Value.Stats.Add((Challenges)type, 0);
                }
            }           
        }

        private class ChallengeData
        {
            public Dictionary<ulong, StatData> Stats = new Dictionary<ulong, StatData>();
            public Dictionary<Challenges, LeaderData> Titles = new Dictionary<Challenges, LeaderData>();
            public double LastUpdate = 0;
        }

        private class StatData
        {
            public string DisplayName = string.Empty;
            public Dictionary<Challenges, int> Stats = new Dictionary<Challenges, int>();
        }

        private class LeaderData
        {
            public ulong UserID = 0U;
            public string DisplayName = null;
            public int Count = 0;
        }

        private class WoundedData
        {
            public float distance;
            public ulong attackerId;
        }
        
        enum Challenges
        {
            AnimalKills, ArrowKills, ClothesCrafted, Headshots, PlantsGathered, PlayersHealed, PlayersKilled, MeleeKills, RevolverKills, RocketsFired, RocksGathered, BladeKills, StructuresBuilt, StructuresRepaired, ExplosivesThrown, WeaponsCrafted, WoodGathered, QuestsCompleted, PVPKillDistance, PVEKillDistance, HelicopterKills, APCKills, NPCKills
        }

        #endregion

        #region Lists       
        List<string> meleeShortnames = new List<string> { "bone.club", "hammer.salvaged", "hatchet", "icepick.salvaged", "knife.bone", "mace", "machete", "pickaxe", "rock", "stone.pickaxe", "stonehatchet", "torch" };
        List<string> bladeShortnames = new List<string> { "salvaged.sword", "salvaged.cleaver", "longsword", "axe.salvaged" };
        List<string> plantShortnames = new List<string> { "pumpkin", "cloth", "corn", "mushroom", "seed.hemp", "seed.corn", "seed.pumpkin" };
        #endregion

        #region Messaging
        private string MSG(string key, string id = null) => lang.GetMessage(key, this, id);

        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            {"newLeader", "{playername} has topped the leader board for most {ctype}" },
            {"AnimalKills", "animal kills" },
            {"ArrowKills", "kills with arrows" },
            {"ClothesCrafted", "clothes crafted" },
            {"Headshots", "headshots" },
            {"PlantsGathered", "plants gathered" },
            {"PlayersHealed", "players healed" },
            {"PlayersKilled", "players killed" },
            {"MeleeKills", "melee kills" },
            {"RevolverKills", "revolver kills" },
            {"RocketsFired", "rockets fired" },
            {"RocksGathered", "ore gathered" },
            {"BladeKills", "blade kills" },
            {"StructuresBuilt", "structures built" },
            {"StructuresRepaired", "structures repaired" },
            {"ExplosivesThrown", "explosives thrown" },
            {"WeaponsCrafted", "weapons crafted" },
            {"WoodGathered", "wood gathered" },
            {"PVEKillDistance", "longest PVE kill"},
            {"PVPKillDistance", "longest PVP kill" },
            {"QuestsCompleted", "quests completed" },
            {"UITitle", "Player Challenges   v{Version}" },
            {"UIDisabled", "The UI has been disabled as there is a error in the config. Please contact a admin" },
            {"dataWipe", "You have wiped all player stats and titles" }
        };
        #endregion
    }
}
