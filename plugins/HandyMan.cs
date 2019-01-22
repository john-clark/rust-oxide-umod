using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Facepunch;
using System;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Handy Man", "nivex", "1.3.1")]
    [Description("Provides AOE repair functionality to the player. Repair is only possible where you can build.")]
    public class HandyMan : RustPlugin
    {
        [PluginReference]
        Plugin NoEscape;

        readonly DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetFile(nameof(Name));
        Dictionary<ulong, bool> playerData = new Dictionary<ulong, bool>(); //player preference values
        Dictionary<BuildingPrivlidge, BaseCombatEntity> entities = new Dictionary<BuildingPrivlidge, BaseCombatEntity>();
        bool _allowHandyManFixMessage = true;
        bool _allowAOERepair = true;
        PluginTimers RepairMessageTimer; //Timer to control HandyMan chats
        static int constructionMask = LayerMask.GetMask("Construction");
        static int allMask = LayerMask.GetMask("Construction", "Deployed");
        static float lastAttackLimit = 30f;
        static float privDistance = 21f;

        bool IsRaidBlocked(string targetId) => UseRaidBlocker && (bool)(NoEscape?.Call("IsRaidBlocked", targetId) ?? false);
        bool HasPerm(BasePlayer player) => permission.UserHasPermission(player.UserIDString, "handyman.use") || player.IsAdmin || player.IsDeveloper || player.net.connection.authLevel > 0;
        bool HasResources(BasePlayer player) => player.inventory.AllItems().Any(item => item.info.shortname == "wood" || item.info.shortname == "metal.refined" || item.info.shortname == "stones" || item.info.shortname == "metal.fragments");

        private void Loaded()
        {
            permission.RegisterPermission("handyman.use", this);
            LoadVariables();

            try
            {
                playerData = dataFile.ReadObject<Dictionary<ulong, bool>>();
            }
            catch { }

            if (playerData == null)
                playerData = new Dictionary<ulong, bool>();

            //Set message timer to prevent user spam
            RepairMessageTimer = new PluginTimers(this);
            RepairMessageTimer.Every(HandyManChatInterval, () => _allowHandyManFixMessage = true);
        }

        void OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (!_allowAOERepair || !HasPerm(player) || IsRaidBlocked(player.UserIDString) || info.HitEntity == null || info.HitEntity.IsDestroyed)
            {
                return;
            }

            var entity = info.HitEntity as BaseCombatEntity;

            if (!entity)
            {
                return;
            }

            if (!playerData.ContainsKey(player.userID))
            {
                playerData[player.userID] = DefaultHandyManOn;
                dataFile.WriteObject(playerData);
            }

            if (playerData[player.userID])
            {
                if (!HasResources(player))
                {
                    SendChatMessage(player, msg("No Resources", player.UserIDString));
                    return;
                }

                Repair(entity, player);
            }
        }

        void Repair(BaseCombatEntity entity, BasePlayer player)
        {
            if (player.CanBuild())
            {
                if (_allowHandyManFixMessage)
                {
                    SendChatMessage(player, msg("Fix", player.UserIDString));
                    _allowHandyManFixMessage = false;
                }

                RepairAOE(entity, player);
            }
            else
                SendChatMessage(player, msg("NotAllowed", player.UserIDString));
        }

        private void RepairAOE(BaseCombatEntity entity, BasePlayer player)
        {
            //Prevent infinite loop
            _allowAOERepair = false;

            //gets the position of the block we just hit
            var position = new OBB(entity.transform, entity.bounds).ToBounds().center;
            //sets up the collection for the blocks that will be affected
            var entities = Pool.GetList<BaseCombatEntity>();

            //gets a list of entities within a specified range of the current target
            Vis.Entities(position, RepairRange, entities, repairDeployables ? allMask : constructionMask);
            int repaired = 0;

            if (entities.Count == 1)
            {
                _allowAOERepair = true;
                Pool.FreeList(ref entities);
                return;
            }

            //check if we have blocks - we should always have at least 1
            if (entities.Count > 0)
            {
                var resources = new Dictionary<string, float>();
                int lastAttacked = 0;

                //cycle through our block list - figure out which ones need repairing
                foreach (var ent in entities)
                {
                    //check to see if the block has been damaged before repairing.
                    if (ent.health < ent.MaxHealth())
                    {
                        if (ent.SecondsSinceAttacked <= lastAttackLimit)
                        {
                            lastAttacked++;
                            continue;
                        }

                        var ret = CanRepair(ent, player, entities);

                        if (ret is KeyValuePair<string, float>)
                        {
                            var kvp = (KeyValuePair<string, float>)ret;

                            if (!resources.ContainsKey(kvp.Key))
                            {
                                resources.Add(kvp.Key, kvp.Value);
                            }
                            else
                            {
                                resources[kvp.Key] += kvp.Value;
                            }
                        }
                        else if (ret is bool && (bool)ret)
                        {
                            if (DoRepair(ent, player))
                            {
                                if (markRepairedTime > 0f && player.IsAdmin)
                                {
                                    player.SendConsoleCommand("ddraw.text", markRepairedTime, Color.green, ent.WorldSpaceBounds().ToBounds().center, "R");
                                }

                                if (++repaired > maxRepairEnts)
                                {
                                    break;
                                }
                            }
                        }
                    }
                }

                Pool.FreeList(ref entities);

                if (resources.Count > 0)
                {
                    if (resources.Count > 1 || (resources.Count == 1 && resources.First().Key == "High Quality Metal" && resources.First().Value > 3))
                    {
                        foreach (var kvp in resources)
                        {
                            SendChatMessage(player, msg("Missing Resources Multiple", player.UserIDString, kvp.Key, kvp.Value));
                        }

                        SendChatMessage(player, msg("Missing Resources Partial", player.UserIDString));
                    }
                    else
                    {
                        SendChatMessage(player, msg("Missing Resources Single", player.UserIDString, resources.First().Key, resources.First().Value));
                    }

                    if (repaired == 0)
                    {
                        _allowAOERepair = true;
                    }
                }

                if (!_allowAOERepair)
                {
                    SendChatMessage(player, repaired > 0 ? msg("IFixedEx", player.UserIDString, repaired) : msg(lastAttacked > 0 && repaired == 0 ? "CannotFixYet" : "FixDone", player.UserIDString));
                }
            }
            else
            {
                SendChatMessage(player, msg("MissingFix", player.UserIDString));
            }

            _allowAOERepair = true;
        }

        object CanRepair(BaseCombatEntity entity, BasePlayer player, List<BaseCombatEntity> entities)
        {
            float num = entity.MaxHealth() - entity.health;
            float num2 = num / entity.MaxHealth();
            var list = entity.RepairCost(num2);

            if (list != null && list.Count > 0)
            {
                foreach (var ia in list)
                {
                    var items = player.inventory.FindItemIDs(ia.itemid);
                    int sum = items.Sum(item => item.amount);

                    if (sum * repairMulti < ia.amount * repairMulti)
                    {
                        return new KeyValuePair<string, float>(ia.itemDef.displayName.english, ia.amount);
                    }
                }
            }

            var privs = entities.Where(ent => ent != null && ent.net != null && !ent.IsDestroyed && ent is BuildingPrivlidge).Cast<BuildingPrivlidge>().ToList();

            if (privs.Count == 0)
            {
                return true;
            }

            foreach(var priv in privs)
            {
                if (priv.Distance(entity) <= privDistance)
                {
                    return !priv.AnyAuthed() || priv.IsAuthed(player);
                }
            }

            return false; // player.CanBuild(new OBB(entity.transform, entity.bounds));
        }

        // BaseCombatEntity
        public bool DoRepair(BaseCombatEntity entity, BasePlayer player)
        {
            if (!entity.repair.enabled)
            {
                return false;
            }
            if (Interface.CallHook("OnStructureRepair", new object[]
            {
                entity,
                player
            }) != null)
            {
                return false;
            }
            if (entity.SecondsSinceAttacked <= lastAttackLimit)
            {
                entity.OnRepairFailed();
                return false;
            }
            float num = entity.MaxHealth() - entity.Health();
            float num2 = num / entity.MaxHealth();
            if (num <= 0f || num2 <= 0f)
            {
                entity.OnRepairFailed();
                return false;
            }
            var list = entity.RepairCost(num2);
            if (list == null || list.Count == 0)
            {
                return false;
            }
            foreach (var ia in list)
            {
                ia.amount *= repairMulti;
            }
            float num3 = list.Sum(x => x.amount);

            if (num3 > 0f)
            {
                float num4 = list.Min(x => Mathf.Clamp01((float)player.inventory.GetAmount(x.itemid) / x.amount));
                num4 = Mathf.Min(num4, 50f / num);
                if (num4 <= 0f)
                {
                    entity.OnRepairFailed();
                    return false;
                }
                int num5 = 0;
                foreach (var current in list)
                {
                    int amount = Mathf.CeilToInt(num4 * current.amount);
                    num5 += player.inventory.Take(null, current.itemid, amount);
                }

                float num7 = (float)num5 / num3;
                entity.health += num * num7;
                entity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            }
            else
            {
                entity.health += num;
                entity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            }
            if (entity.health >= entity.MaxHealth())
            {
                entity.OnRepairFinished();
            }
            else
            {
                entity.OnRepair();
            }

            return true;
        }

        [ChatCommand("handyman")]
        private void ChatCommand_HandyMan(BasePlayer player, string command, string[] args)
        {
            if (!HasPerm(player))
            {
                SendChatMessage(player, msg("NoPermission", player.UserIDString));
                return;
            }

            if (!playerData.ContainsKey(player.userID))
            {
                playerData[player.userID] = DefaultHandyManOn;
                dataFile.WriteObject(playerData);
            }

            if (args.Length > 0)
            {
                playerData[player.userID] = args[0].ToLower() == "on";
                dataFile.WriteObject(playerData);
            }

            SendChatMessage(player, msg(playerData[player.userID] ? "Hired" : "Fired", player.UserIDString));
        }

        [ConsoleCommand("healthcheck")]
        private void ConsoleCommand_HealthCheck() => Puts("HandyMan is running.");

        #region Config
        private bool Changed;
        private bool UseRaidBlocker;
        private bool DefaultHandyManOn;
        private int RepairRange;
        private int HandyManChatInterval;
        private float repairMulti;
        private bool repairDeployables;
        private int maxRepairEnts;
        private float markRepairedTime;

        protected override void LoadDefaultMessages()
        {
            string helpText =
                  "HandyMan - Help - v {ver} \n"
                + "-----------------------------\n"
                + "/HandyMan - Shows your current preference for HandyMan.\n"
                + "/HandyMan on/off - Turns HandyMan on/off.";

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Hired", "HandyMan has been Hired."},
                {"Fired", "HandyMan has been Fired."},
                {"Fix", "You fix this one, I'll get the rest."},
                {"NotAllowed", "You are not allowed to build here - I can't repair for you."},
                {"IFixed", "I fixed some damage over here..."},
                {"IFixedEx", "I fixed {0} constructions over here..."},
                {"FixDone", "Guess I fixed them all..."},
                {"MissingFix", "I'm telling you... it disappeared... I can't find anything to fix."},
                {"NoPermission", "You don't have permission to use this command." },
                {"Help", helpText},
                {"Missing Resources Single", "Missing resources: {0} ({1}). I'll need the full amount to repair this." },
                {"Missing Resources Multiple", "Missing resources: {0} ({1})" },
                {"Missing Resources Partial", "I can do some repairs with a partial amount of these resources." },
                {"No Resources", "You must have some resources in order to repair!" },
                {"CannotFixYet", "Everything has been attacked recently and cannot be repaired yet." }
            }, this);
        }

        void LoadVariables() //Assigns configuration data once read
        {
            HandyManChatInterval = Convert.ToInt32(GetConfig("Settings", "Chat Interval", 30));
            DefaultHandyManOn = Convert.ToBoolean(GetConfig("Settings", "Default On", true));
            RepairRange = Convert.ToInt32(GetConfig("Settings", "Repair Range", 50));
            UseRaidBlocker = Convert.ToBoolean(GetConfig("Settings", "Use Raid Blocker", false));
            repairMulti = Convert.ToSingle(GetConfig("Settings", "Repair Cost Multiplier", 1.0f));
            repairDeployables = Convert.ToBoolean(GetConfig("Settings", "Repair Deployables", false));
            maxRepairEnts = Convert.ToInt32(GetConfig("Settings", "Maximum Entities To Repair", 50));
            markRepairedTime = Convert.ToSingle(GetConfig("Settings", "Mark Repaired Entities For X Seconds (Admins Only)", 0f));

            if (repairMulti < 1.0f)
                repairMulti = 1.0f;

            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }

        /// <summary>
        /// Responsible for loading default configuration.
        /// Also creates the initial configuration file
        /// </summary>
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Config.Clear();
            LoadVariables();
        }

        object GetConfig(string menu, string dataValue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(dataValue, out value))
            {
                value = defaultValue;
                data[dataValue] = value;
                Changed = true;
            }
            return value;
        }

        public string msg(string key, string id = null, params object[] args)
        {
            string message = id == null ? RemoveFormatting(lang.GetMessage(key, this, id)) : lang.GetMessage(key, this, id);

            return args.Length > 0 ? string.Format(message, args) : message;
        }

        public string RemoveFormatting(string source)
        {
            return source.Contains(">") ? System.Text.RegularExpressions.Regex.Replace(source, "<.*?>", string.Empty) : source;
        }

        private void SendChatMessage(BasePlayer player, string msg) => player.ChatMessage($"<color=#00FF8D>{Title}</color>: {msg}");
        #endregion
    }
}
