using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Libraries;
using UnityEngine;
using Random = System.Random;

namespace Oxide.Plugins
{
    [Info("RunningMan", "sami37 - Мизантроп", "1.6.3", ResourceId = 777)]
    [Description("Get reward by killing runner or just survive as runner.")]
    class RunningMan : RustPlugin
    {
        private Timer stillRunnerTimer;
        private Command command = Interface.Oxide.GetLibrary<Command>();
        private Dictionary<string, Dictionary<string, RewardData>> SavedReward = new Dictionary<string, Dictionary<string, RewardData>>(StringComparer.OrdinalIgnoreCase);
        private BasePlayer runningman;
        private Timer eventstart;
        private Timer eventpause;
        private Timer ingameTimer;
        private bool EventStarted;
        private double time1;
        private double time2;
        private Random rnd = new Random();
        private bool displayDistance;

        [PluginReference]
        Plugin Economics;
        
        [PluginReference]
        Plugin KarmaSystem;

        [PluginReference]
        Plugin ServerRewards;

        [PluginReference]
        Plugin Clans;

        [PluginReference]
        Plugin Friends;
        
        void SetConfig(params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList();
            stringArgs.RemoveAt(args.Length - 1);

            if (Config.Get(stringArgs.ToArray()) == null) Config.Set(args);
        }

        bool hasAccess(BasePlayer player, string permissionName) { if (player.net.connection.authLevel > 1) return true; return permission.UserHasPermission(player.userID.ToString(), permissionName); }
		
        void Loaded()
        {
            LoadDefaultConfig();
            command.AddChatCommand("eventon", this, "cmdEvent");
            command.AddChatCommand("eventoff", this, "cmdEventOff");
            command.AddChatCommand("run", this, "cmdRun");
            command.AddConsoleCommand("eventon", this, "ccmdEvent");
            command.AddConsoleCommand("eventoff", this, "cmdEventOf");
            EventStarted = false;
            if (!Economics)
            {
                Puts("Economics not found!");
            }
            if ((string) Config["Default", "AutoStart"] == "true" && (string) Config["IngameTime", "Use ingame timer"] == "false")
            {
                eventpause = timer.Once(60*(int) Config["Default", "PauseeventTime"], Startevent);
                time1 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            }
            else if ((string) Config["Default", "AutoStart"] == "true" &&
                     (string) Config["IngameTime", "Use ingame timer"] == "true")
            {
                ingameTimer = timer.Once(20, CheckTime);
                CheckTime();
            }
            LoadSavedData();
        }

        private void CheckTime()
        {
            if ((TOD_Sky.Instance.Cycle.Hour >= (int) Config["IngameTime", "Start War Time"] &&
                 TOD_Sky.Instance.Cycle.Hour < 24) ||
                (TOD_Sky.Instance.Cycle.Hour >= 0 &&
                 TOD_Sky.Instance.Cycle.Hour < (int) Config["IngameTime", "End War Time"]))
            {
                if (!EventStarted)
                {
                    eventpause = timer.Once(60*(int) Config["Default", "PauseeventTime"], Startevent);
                    EventStarted = true;
                }
            }
            else
            {
                if (!EventStarted)
                {
                    EventStarted = false;
                    DestroyEvent();
                }
                eventpause = timer.Once(20, CheckTime);
            }
        }

        void Init()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                 {"StartEventRunner", "<color=#C4FF00>{0}</color>: Running man {1}\nKill him and get the reward!\nCommand: /run - to know the distance to the target."}, 
                 {"NotEnoughPlayers", "<color=#C4FF00>{0}</color>: There aren't enough players to start the event."},
                 {"RunnerSaved", "<color=#C4FF00>{0}</color>: {1} ran away from the chase and received a reward!"},
                 {"StillRunner", "<color=#C4FF00>{0}</color>: {1} your are still the runner."},
                 {"RunnerBackOnline", "<color=#C4FF00>{0}</color>: {1} is back online.\nKill him and get the reward!"},
                 {"RunnerKilled", "<color=#C4FF00>{0}</color>: Player - {1} kill {2} and received a reward!"},
                 {"RunnerDistance", "<color=#C4FF00>{0}</color>: Player - {1},\n is at a distance of {2}\nKill him and get the reward!"},
                 {"UntilEndOfEvent", "<color=#C4FF00>{0}</color>: Until the end of event left: {1} minutes"},
                 {"NotRunningEvent", "<color=#C4FF00>{0}</color>: At the moment the event is not running"},
                 {"UntilStartOfEvent", "<color=#C4FF00>{0}</color>: Before the start of the event remained: {1} minutes"},
                 {"RunCommandHelp", "Use \"/run\" to find out information about the running man"},
                 {"AdminCommandHelp", "Use \"/eventon\" for start event Running Man\nUse \"/eventoff\" for start event Running Man"},
                 {"AdminAddCommandHelp", "Use \"/running add <Package Name> <ItemName or money or karma> <MinAmount> <MaxAmount>\" to add item."},
                 {"AdminRemoveCommandHelp", "Use \"/running remove <PackageName> <ItemName or karma or money>\" to remove item."},
                 {"NobodyOnline", "<color=#C4FF00>{0}</color>: You can't run event while there is nobody online"},
                 {"NoPerm", "<color=#C4FF00>{0}</color>: You have no rights to do this!"},
                 {"RunnerLeaved", "<color=#C4FF00>{0}</color>: {1} got scared and ran away!"},
                 {"EventStopped", "<color=#C4FF00>{0}</color>: Event has stopped!"},
                 {"PackageDontExist", "<color=#C4FF00>{0}</color>: This package don't exist."},
                 {"MissingItemFromPackage", "<color=#C4FF00>{0}</color>: Item not found in package."},
                 {"ItemRemoved", "<color=#C4FF00>{0}</color>: Successfully removed item {1}."},
                 {"ItemAdded", "<color=#C4FF00>{0}</color>: Successfully added item {1} to package {2}."},
                 {"PackageAdded", "<color=#C4FF00>{0}</color>: Successfully added package {1} and inserted item to it."},
                 {"ItemExist", "<color=#C4FF00>{0}</color>: Item already exist in package."},
                 {"Rewarded", "<color=#C4FF00>{0}</color>: You won a reward : {1}."}
            }, this);
            permission.RegisterPermission("runningman.admin", this);
        }

        protected override void LoadDefaultConfig()
        {
            SetConfig("Default", "ChatName", "EVENT");
            SetConfig("Default", "authLevel", 1);
            SetConfig("Default", "AutoStart", "true");
            SetConfig("Default", "Display Distance", "true");
            SetConfig("Default", "Count", 2);
            SetConfig("Default", "StarteventTime", 30);
            SetConfig("Default", "PauseeventTime", 30);
            SetConfig("Default", "DisconnectPendingTimer", 30);
            SetConfig("IngameTime", "Start War Time", 18);
            SetConfig("IngameTime", "End War Time", 8);
            SetConfig("IngameTime", "Use ingame timer", "false");
            SetConfig("Reward", "Random", "true");
            SetConfig("Reward", "RewardFixing", "wood");
            SetConfig("Reward", "RewardFixingAmount", 10000);
            SetConfig("Reward", "KarmaSystem", "PointToRemove", 0);
            SetConfig("Reward", "KarmaSystem", "PointToAdd", 1);
            SetConfig("Default", "Excluded auth level", 1);
            SetConfig("Default", "Block friends kill reward", 1);
            SetConfig("Default", "Block clans kill reward", 1);
            SaveConfig();
        }

        class RewardData
        {
            public Dictionary<string, ValueAmount> RewardItems; 
        }

        private class ValueAmount
        {
            public int MinValue;
            public int MaxValue;
        }

        void LoadSavedData()
        {
            SavedReward = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, Dictionary<string, RewardData>>>(nameof(RunningMan));
            if (SavedReward.Count == 0)
            {
                SavedReward["runner"] = new Dictionary<string, RewardData>
                {
                    {"Karma", new RewardData
                        {
                            RewardItems = new Dictionary<string, ValueAmount>
                            {
                                {"Karma", new ValueAmount
                                    {
                                        MinValue = 0,
                                        MaxValue = 1
                                    }
                                }
                            }
                        }
                    },
                    {"ServerReward", new RewardData
                        {
                            RewardItems = new Dictionary<string, ValueAmount>
                            {
                                {"serverreward", new ValueAmount
                                    {
                                        MinValue = 0,
                                        MaxValue = 1
                                    }
                                }
                            }
                        }
                    },
                    {"build", new RewardData
                        {
                            RewardItems = new Dictionary<string, ValueAmount>
                            {
                                {"wood", new ValueAmount
                                    {
                                        MinValue = 1000,
                                        MaxValue = 10000
                                    }
                                },
                                {
                                    "stones", new ValueAmount
                                    {
                                        MinValue = 1000,
                                        MaxValue = 10000
                                    }
                                }
                            }
                        }
                    }
                };
                SavedReward["killer"] = new Dictionary<string, RewardData>
                {
                    {"Karma", new RewardData
                        {
                            RewardItems = new Dictionary<string, ValueAmount>
                            {
                                {"Karma", new ValueAmount
                                    {
                                        MinValue = 0,
                                        MaxValue = 1
                                    }
                                }
                            }
                        }
                    },
                    {"ServerReward", new RewardData
                        {
                            RewardItems = new Dictionary<string, ValueAmount>
                            {
                                {"serverreward", new ValueAmount
                                    {
                                        MinValue = 0,
                                        MaxValue = 1
                                    }
                                }
                            }
                        }
                    },
                    {"build", new RewardData
                        {
                            RewardItems = new Dictionary<string, ValueAmount>
                            {
                                {"wood", new ValueAmount
                                    {
                                        MinValue = 1000,
                                        MaxValue = 10000
                                    }
                                },
                                {
                                    "stones", new ValueAmount
                                    {
                                        MinValue = 1000,
                                        MaxValue = 10000
                                    }
                                }
                            }
                        }
                    }
                };
                PrintWarning("Failed to load data file, generating a new one...");
            }
            SaveLoadedData();
        }

        void SaveLoadedData()
        {
            try
            {
                Interface.Oxide.DataFileSystem.WriteObject(nameof(RunningMan), SavedReward);
            }
            catch (Exception)
            {
                PrintWarning("Failed to save data file.");
            }
        }

        void Unload()
        {
            eventpause?.Destroy();
            eventstart?.Destroy();
            ingameTimer?.Destroy();
            runningman = null;
        }

        private void Startevent()
        {
            if (eventpause != null)
            {
                eventpause.Destroy();
                runningman = null;
            }
            if (eventstart != null)
            {
                eventstart.Destroy();
                runningman = null;
            }
            if (BasePlayer.activePlayerList != null)
            {
				int auth = (int) Config["Default", "Excluded auth level"];
                var t = auth == 0 ? BasePlayer.activePlayerList : BasePlayer.activePlayerList.Where(x => x.net.connection.authLevel < auth);
                var enumerable = t as IList<BasePlayer> ?? t.ToList();
                if (enumerable.Count >= (int) Config["Default", "Count"] && enumerable.Count > 0)
                {
                    
                    var basePlayers = t as BasePlayer[] ?? enumerable.ToArray();
                    var randI = rnd.Next(0, basePlayers.Length);
                    runningman = basePlayers[randI];
                    Runlog("Running man: " + runningman.displayName);
                    BroadcastChat(string.Format(lang.GetMessage("StartEventRunner", this), (string) Config["Default", "ChatName"], runningman.displayName));
                    eventstart = timer.Once(60*(int) Config["Default", "StarteventTime"], Runningstop);
                    time1 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                }
                else
                {
                    BroadcastChat(string.Format(lang.GetMessage("NotEnoughPlayers", this), (string) Config["Default", "ChatName"]));
                    eventpause?.Destroy();
                    eventpause = timer.Once(60*(int) Config["Default", "PauseeventTime"], Startevent);
                    time1 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                }
            }
        }

        void Runningstop()
        {
            Runlog("Running man - " + runningman.displayName + " ran away from the chase and received as a reward!");

            BroadcastChat(string.Format(lang.GetMessage("RunnerSaved", this), (string) Config["Default", "ChatName"], runningman.displayName));
            var inv = runningman.inventory;
            if ((string) Config["Reward", "Random"] == "true")
            {
                if (SavedReward?["runner"] == null)
                {
                    PrintWarning("Reward list is empty, please add items");
                    inv?.GiveItem(ItemManager.CreateByName((string) Config["Reward", "RewardFixing"],
                        (int) Config["Reward", "RewardFixingAmount"]), inv.containerMain);
                    return;
                }
                Runlog("random");
                var rand = SavedReward["runner"].ElementAt(rnd.Next(0, SavedReward["runner"].Count));
                foreach (var data in rand.Value.RewardItems)
                {
                    int randomReward = rnd.Next(data.Value.MinValue, data.Value.MaxValue);
                    switch (data.Key.ToLower())
                    {
                        case "karma":
                            if (KarmaSystem != null && KarmaSystem.IsLoaded)
                            {
                                IPlayer player = covalence.Players.FindPlayerById(runningman.UserIDString);
                                KarmaSystem.Call("AddKarma", player, (double) randomReward);
                                SendReply(runningman, string.Format(lang.GetMessage("Rewarded", this, runningman.UserIDString), (string) Config["Default", "ChatName"], data.Key + " x " + randomReward));
                            }
                            else
                            {
                                inv?.GiveItem(
                                    ItemManager.CreateByName((string) Config["Reward", "RewardFixing"],
                                        (int) Config["Reward", "RewardFixingAmount"]), inv.containerMain);
                                SendReply(runningman, string.Format(lang.GetMessage("Rewarded", this, runningman.UserIDString), (string) Config["Default", "ChatName"], (string) Config["Reward", "RewardFixing"] + " x " +
                                    (int) Config["Reward", "RewardFixingAmount"]));
                            }
                            break;
                        case "money":
                            if (Economics != null && Economics.IsLoaded)
                            {
                                Economics?.CallHook("Deposit", runningman.userID,
                                    randomReward);
                                SendReply(runningman, string.Format(lang.GetMessage("Rewarded", this, runningman.UserIDString), (string) Config["Default", "ChatName"], "money x " + randomReward));
                            }
                            else
                            {
                                inv?.GiveItem(
                                    ItemManager.CreateByName((string) Config["Reward", "RewardFixing"],
                                        (int) Config["Reward", "RewardFixingAmount"]), inv.containerMain);
                                SendReply(runningman, string.Format(lang.GetMessage("Rewarded", this, runningman.UserIDString), (string) Config["Default", "ChatName"], (string) Config["Reward", "RewardFixing"] + " x " +
                                    (int) Config["Reward", "RewardFixingAmount"]));
                            }
                            break;
                        case "serverreward":
                            if (ServerRewards != null && ServerRewards.IsLoaded)
                            {
                                ServerRewards?.CallHook("AddPoints", new object[] {runningman.userID, randomReward});
                                SendReply(runningman, string.Format(lang.GetMessage("Rewarded", this, runningman.UserIDString), (string) Config["Default", "ChatName"], "ServerRewards points x " + randomReward));
                            }
                            else
                            {
                                inv?.GiveItem(
                                    ItemManager.CreateByName((string) Config["Reward", "RewardFixing"],
                                        (int) Config["Reward", "RewardFixingAmount"]), inv.containerMain);
                                SendReply(runningman, string.Format(lang.GetMessage("Rewarded", this, runningman.UserIDString), (string) Config["Default", "ChatName"], (string) Config["Reward", "RewardFixing"] + " x " +
                                    (int) Config["Reward", "RewardFixingAmount"]));
                            }
                            break;
                        default:
                            Item item = ItemManager.CreateByName(data.Key,
                                randomReward);
                            if (item != null)
                            {
                                inv?.GiveItem(item, inv.containerMain);
                                SendReply(runningman, string.Format(lang.GetMessage("Rewarded", this, runningman.UserIDString), (string) Config["Default", "ChatName"], item.info.displayName.english + " x " + item.amount));
                            }
                            else
                                PrintError($"Failed to create item...{rand.Key}");
                            break;
                    }
                }
            }
            else
            {
                inv?.GiveItem(ItemManager.CreateByName((string) Config["Reward", "RewardFixing"],
                    (int) Config["Reward", "RewardFixingAmount"]), inv.containerMain);
            }
            eventstart.Destroy();
            eventstart = null;
            runningman = null;
            Runlog("timer eventstart stopped");
            eventpause?.Destroy();
            eventpause = timer.Once(60*(int) Config["Default", "PauseeventTime"], Startevent);
            time1 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        void BroadcastChat(string msg = null)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                SendReply(player, msg ?? " ", "");
            }
        }

        private void Runlog(string text)
        {
            Puts("[EVENT] +--------------- RUNNING MAN -----------------");
            Puts("[EVENT] | "+text);
            Puts("[EVENT] +---------------------------------------------");
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            if (runningman != null)
                if (player == runningman)
                {
                    stillRunnerTimer = timer.Once(60*(int) Config["Default", "DisconnectPendingTimer"], DestroyLeaveEvent);
                }
        }

        void OnPlayerInit(BasePlayer player)
        {
            if (runningman != null)
            {
                if (runningman == player)
                {
                    SendReply(player,
                        string.Format(lang.GetMessage("StillRunner", this, player.UserIDString), (string) Config["Default", "ChatName"], runningman.displayName));
                    BroadcastChat(string.Format(lang.GetMessage("RunnerBackOnline", this), (string) Config["Default", "ChatName"], runningman.displayName));
                    stillRunnerTimer?.Destroy();
                }
                else
                {
                    SendHelpText(player);
                }
            }
        }

        void PlayerKilled(BasePlayer victim, HitInfo hitinfo)
        {
            var attacker = hitinfo?.Initiator?.ToPlayer();
            if (attacker == null) return;
            if (victim == null) return;
            if (attacker == victim) return;
            if (runningman == null) return;
            if(victim != runningman) return;
            if(Friends != null && (bool)Friends?.CallHook("AreFriends", attacker.userID, victim.userID) && (bool)Config["Default", "Block friends kill reward"]) return;
            var attackerclan = Clans?.CallHook("GetClanOf", attacker.userID);
            var victimclan = Clans?.CallHook("GetClanOf", victim.userID);
            if (victimclan != null && attackerclan != null && victimclan == attackerclan && (bool) Config["Default", "Block clans kill reward"]) return;
            Runlog(string.Format(lang.GetMessage("RunnerKilled", this), (string) Config["Default", "ChatName"], attacker.displayName, runningman.displayName));
            BroadcastChat(string.Format(lang.GetMessage("RunnerKilled", this), (string) Config["Default", "ChatName"], attacker.displayName, runningman.displayName));
            var inv = attacker.inventory;
            if ((string) Config["Reward", "Random"] == "true")
            {
                if (SavedReward?["killer"] == null)
                {
                    PrintWarning("Reward list is empty, please add items, using FixingReward option...");
                    inv?.GiveItem(ItemManager.CreateByName((string) Config["Reward", "RewardFixing"],
                        (int) Config["Reward", "RewardFixingAmount"]), inv.containerMain);
                    return;
                }
                var rand = SavedReward["killer"].ElementAt(rnd.Next(0, SavedReward["killer"].Count));
                foreach (var data in rand.Value.RewardItems)
                {
                    int randomReward = rnd.Next(data.Value.MinValue, data.Value.MaxValue);
                    switch (data.Key.ToLower())
                    {
                        case "karma":
                            if (KarmaSystem != null && KarmaSystem.IsLoaded)
                            {
                                IPlayer player = covalence.Players.FindPlayerById(attacker.UserIDString);
                                KarmaSystem.Call("AddKarma", player, (double) randomReward);
                                SendReply(attacker, string.Format(lang.GetMessage("Rewarded", this, attacker.UserIDString), (string) Config["Default", "ChatName"], data.Key + " x " + randomReward));
                            }
                            else
                            {
                                inv?.GiveItem(
                                    ItemManager.CreateByName((string) Config["Reward", "RewardFixing"],
                                        (int) Config["Reward", "RewardFixingAmount"]), inv.containerMain);
                                SendReply(attacker, string.Format(lang.GetMessage("Rewarded", this, attacker.UserIDString), (string) Config["Default", "ChatName"], (string) Config["Reward", "RewardFixing"] + " x " +
                                    (int) Config["Reward", "RewardFixingAmount"]));
                            }
                            break;
                        case "money":
                            if (Economics != null && Economics.IsLoaded)
                            {
                                Economics?.CallHook("Deposit", attacker.userID,
                                    randomReward);
                                SendReply(attacker, string.Format(lang.GetMessage("Rewarded", this, attacker.UserIDString), (string) Config["Default", "ChatName"], "money x " + randomReward));
                            }
                            else
                            {
                                inv?.GiveItem(
                                    ItemManager.CreateByName((string) Config["Reward", "RewardFixing"],
                                        (int) Config["Reward", "RewardFixingAmount"]), inv.containerMain);
                                SendReply(attacker, string.Format(lang.GetMessage("Rewarded", this, attacker.UserIDString), (string) Config["Default", "ChatName"], (string) Config["Reward", "RewardFixing"] + " x " +
                                    (int) Config["Reward", "RewardFixingAmount"]));
                            }
                            break;
                        case "serverreward":
                            if (ServerRewards != null && ServerRewards.IsLoaded)
                            {
                                ServerRewards?.CallHook("AddPoints", new object[] {attacker.userID, randomReward});
                                SendReply(attacker, string.Format(lang.GetMessage("Rewarded", this, attacker.UserIDString), (string) Config["Default", "ChatName"], "ServerRewards points x " + randomReward));
                            }
                            else
                            {
                                inv?.GiveItem(
                                    ItemManager.CreateByName((string) Config["Reward", "RewardFixing"],
                                        (int) Config["Reward", "RewardFixingAmount"]), inv.containerMain);
                                SendReply(attacker, string.Format(lang.GetMessage("Rewarded", this, attacker.UserIDString), (string) Config["Default", "ChatName"], (string) Config["Reward", "RewardFixing"] + " x " +
                                    (int) Config["Reward", "RewardFixingAmount"]));
                            }
                            break;
                        default:
                            Item item = ItemManager.CreateByName(data.Key,
                                randomReward);
                            if (item != null)
                            {
                                inv?.GiveItem(item, inv.containerMain);
                                SendReply(attacker, string.Format(lang.GetMessage("Rewarded", this, attacker.UserIDString), (string) Config["Default", "ChatName"], item.info.displayName.english + " x " + item.amount));
                            }
                            else
                                PrintError($"Failed to create item...{rand.Key}");
                            break;
                    }
                }
            }
            else
            {
                switch (((string)Config["Reward", "RewardFixing"]).ToLower())
                {
                    case "karma":
                        if (KarmaSystem != null && KarmaSystem.IsLoaded)
                        {
                            IPlayer player = covalence.Players.FindPlayerById(attacker.UserIDString);
                            KarmaSystem.Call("AddKarma", player, (double) Config["Reward", "RewardFixingAmount"]);
                            SendReply(attacker,
                                string.Format(lang.GetMessage("Rewarded", this, attacker.UserIDString),
                                    (string) Config["Default", "ChatName"], "Karma point x " + Config["Reward", "RewardFixingAmount"]));
                        }
                        else
                        {
                            inv?.GiveItem(
                                ItemManager.CreateByName((string) Config["Reward", "RewardFixing"],
                                    (int) Config["Reward", "RewardFixingAmount"]), inv.containerMain);
                            SendReply(attacker, string.Format(lang.GetMessage("Rewarded", this, attacker.UserIDString),
                                (string) Config["Default", "ChatName"],
                                (string) Config["Reward", "RewardFixing"] + " x " +
                                (int) Config["Reward", "RewardFixingAmount"]));
                        }
                        break;
                    case "money":
                        if (Economics != null && Economics.IsLoaded)
                        {
                            Economics?.CallHook("Deposit", attacker.userID,
                                (int) Config["Reward", "RewardFixingAmount"]);
                            SendReply(attacker,
                                string.Format(lang.GetMessage("Rewarded", this, attacker.UserIDString),
                                    (string) Config["Default", "ChatName"], "money x " + Config["Reward", "RewardFixingAmount"]));
                        }
                        break;
                    case "serverreward":
                        if (ServerRewards != null && ServerRewards.IsLoaded)
                        {
                            ServerRewards?.CallHook("AddPoints",
                                new object[] {attacker.userID, (int) Config["Reward", "RewardFixingAmount"]});

                            SendReply(attacker,
                                string.Format(lang.GetMessage("Rewarded", this, attacker.UserIDString),
                                    (string) Config["Default", "ChatName"], "ServerRewards points x " + Config["Reward", "RewardFixingAmount"]));
                        }
                        break;
                    default:
                        Item item = ItemManager.CreateByName((string)Config["Reward", "RewardFixing"],
                            (int)Config["Reward", "RewardFixingAmount"]);
                        if (item != null)
                        {
                            inv?.GiveItem(item, inv.containerMain);
                            SendReply(attacker,
                                string.Format(lang.GetMessage("Rewarded", this, attacker.UserIDString),
                                    (string) Config["Default", "ChatName"],
                                    item.info.displayName.english + " x " + item.amount));
                        }
                        else
                            PrintError($"Failed to create item...{Config["Reward", "RewardFixing"]}");
                        break;
                }
            }
            eventstart?.Destroy();
            eventstart = null;
            runningman = null;
            Runlog("timer eventstart stopped");
            eventpause?.Destroy();
            eventpause = timer.Once(60*(int) Config["Default", "PauseeventTime"], Startevent);
            time1 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        void OnEntityDeath(BaseEntity entity, HitInfo hitinfo)
        {
            if (entity.ToPlayer())
                PlayerKilled(entity.ToPlayer(), hitinfo);
        }

        void cmdRun(BasePlayer player, string cmd, string[] args)
        {
            if (!player)
                return;
            if (runningman != null)
            {
                var xr = runningman.transform.position.x;
                var zr = runningman.transform.position.z;
                var xk = player.transform.position.x;
                var zk = player.transform.position.z;
                var dist = Math.Floor(Math.Sqrt(Math.Pow(xr - xk, 2) + Math.Pow(zr - zk, 2)));
                if((string) Config["Default", "Display Distance"] == "true")
                    SendReply(player, string.Format(lang.GetMessage("RunnerDistance", this, player.UserIDString), (string) Config["Default", "ChatName"], runningman.displayName, dist));
                else
                    SendReply(player, string.Format(lang.GetMessage("RunnerDistance", this, player.UserIDString), (string) Config["Default", "ChatName"], runningman.displayName, "unknown"));
                time2 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                var time3 = time2 - time1;
                time3 = eventstart.Delay - time3;
                time3 = Math.Floor(time3/60);
                SendReply(player, string.Format(lang.GetMessage("UntilEndOfEvent", this, player.UserIDString), (string) Config["Default", "ChatName"], time3));
            }
            else
            {
                time2 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

                var time3 = time2 - time1;
                if (eventpause != null)
                {
                    time3 = eventpause.Delay - time3;
                    time3 = Math.Floor(time3/60);
                    SendReply(player, string.Format(lang.GetMessage("NotRunningEvent", this, player.UserIDString), (string)Config["Default", "ChatName"]));
                    SendReply(player,
                        string.Format(lang.GetMessage("UntilStartOfEvent", this, player.UserIDString), (string)Config["Default", "ChatName"], time3));
                }
                else
                {
                    SendReply(player, string.Format(lang.GetMessage("NotRunningEvent", this, player.UserIDString), (string)Config["Default", "ChatName"]));
                }
            }
        }

        void SendHelpText(BasePlayer player)
        {
            player.ChatMessage(lang.GetMessage("RunCommandHelp", this, player.UserIDString));
            var authlevel = player.net.connection.authLevel;
            if (authlevel >= (int) Config["Default", "authLevel"]) 
            {
                player.ChatMessage(lang.GetMessage("AdminCommandHelp", this, player.UserIDString));
                player.ChatMessage(lang.GetMessage("AdminAddCommandHelp", this, player.UserIDString));
                player.ChatMessage(lang.GetMessage("AdminRemoveCommandHelp", this, player.UserIDString));
            }
        }

        void cmdEvent(BasePlayer player, string cmd, string[] args)
        {
            if (!hasAccess(player, "runningman.admin"))
            {
                SendReply(player,
                    string.Format(lang.GetMessage("NoPerm", this, player.UserIDString), Config["Default", "ChatName"]));
                return;
            }
            if ((string) Config["IngameTime", "Use ingame timer"] == "true")
            {
                ingameTimer = timer.Once(20, CheckTime);
            }
            else
            {
                Startevent();
                time1 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            }
        }

        void ccmdEvent(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null) return;
            if (arg.Player().net.connection.authLevel >= (int) Config["Default", "authLevel"])
            {
                if (eventpause != null)
                {
                    eventpause.Destroy();
                    runningman = null;
                    Runlog("timer eventpause stopped");
                }
                if (eventstart != null)
                {
                    eventstart.Destroy();
                    runningman = null;
                    Runlog("timer eventstart stopped");
                }
                List<BasePlayer> onlineplayers = BasePlayer.activePlayerList;
                var randI = rnd.Next(0, onlineplayers.Count);
                runningman = onlineplayers[randI];
                Runlog("Running man: " + runningman.displayName);
                BroadcastChat(string.Format(lang.GetMessage("StartEventRunner", this),
                    (string) Config["Default", "ChatName"], runningman.displayName));
                eventstart = timer.Once(60*(int) Config["Default", "StarteventTime"], Runningstop);
                time1 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            }
            else
                arg.ReplyWith(string.Format(lang.GetMessage("NoPerm", this, arg.Player().UserIDString), Config["Default", "ChatName"]));
        }

        void cmdEventOf(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null) return;
            if (arg.Player().net.connection.authLevel >= (int) Config["Default", "authLevel"])
                DestroyEvent();
            else
                arg.ReplyWith(string.Format(lang.GetMessage("NoPerm", this, arg.Player().UserIDString),
                    Config["Default", "ChatName"]));
        }

        void DestroyEvent()
        {
            if (eventpause != null)
            {
                eventpause.Destroy();
                eventpause = null;
                runningman = null;
                Runlog("timer eventpause stopped");

            }
            if (eventstart != null)
            {
                eventstart.Destroy();
                eventstart = null;
                runningman = null;
                Runlog("timer eventstart stopped");
            }
            ingameTimer?.Destroy();
        }

        void DestroyLeaveEvent()
        {
            if (runningman != null)
            {
                Runlog("Player " + runningman.displayName + " got scared and ran away!");
                BroadcastChat(string.Format(lang.GetMessage("RunnerLeaved", this),
                    (string) Config["Default", "ChatName"], runningman.displayName));
            }
            if (eventpause != null)
            {
                eventpause.Destroy();
                eventpause = null;
                runningman = null;
                Runlog("timer eventpause stopped");

            }
            if (eventstart != null)
            {
                eventstart.Destroy();
                eventstart = null;
                runningman = null;
                Runlog("timer eventstart stopped");
            }
            Runlog("Running Man has stopped");
            eventpause = timer.Once(60*(int) Config["Default", "PauseeventTime"], Startevent);
            time1 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        void cmdEventOff(BasePlayer player, string cmd, string[] args)
        {
            if (!hasAccess(player, "runningman.admin"))
            {
                SendReply(player,
                    string.Format(lang.GetMessage("NoPerm", this, player.UserIDString), Config["Default", "ChatName"]));
                return;
            }
            if (eventpause != null)
            {
                eventpause.Destroy();
                eventpause = null;
                runningman = null;
                Runlog("timer eventpause stopped");
            }
            if (eventstart != null)
            {
                eventstart.Destroy();
                eventstart = null;
                runningman = null;
                Runlog("timer eventstart stopped");
            }
            if (ingameTimer != null)
            {
                ingameTimer.Destroy();
                Runlog("ingameTimer stopped");
            }
            Runlog("Running Man has stopped");
            SendReply(player, string.Format(lang.GetMessage("EventStopped", this, player.UserIDString), Config["Default", "ChatName"]));
        }

        [ChatCommand("running")]
        void cmdChat(BasePlayer player, string cmd, string[] args)
        {
            if (!hasAccess(player, "runningman.admin"))
            {
                SendReply(player,
                    string.Format(lang.GetMessage("NoPerm", this, player.UserIDString), Config["Default", "ChatName"]));
                return;
            }
            if (args == null)
            {
                SendHelpText(player);
                return;
            }
            string action;
            string package;
            string item;
            string type;
            if (args.Length < 5)
            {
                type = args[0].ToLower();
                action = args[1].ToLower();
                package = args[2].ToLower();
                item = args[3].ToLower();
                if (action == "remove")
                {
                    if (type == "k" || type == "killer")
                        type = "killer";
                    else
                        type = "runner";
                        switch (args.Length)
                        {
                            case 2:
                                if (SavedReward[type].ContainsKey(package))
                                    SavedReward[type].Remove(package);
                                else
                                    SendReply(player,
                                        string.Format(lang.GetMessage("PackageDontExist", this, player.UserIDString),
                                            (string) Config["Default", "ChatName"]));
                                break;
                            case 3:
                                if (SavedReward.ContainsKey(package))
                                    if (SavedReward[type][package].RewardItems.ContainsKey(item))
                                    {
                                        SavedReward[type][package].RewardItems.Remove(item);
                                        SendReply(player,
                                            string.Format(lang.GetMessage("ItemRemoved", this, player.UserIDString),
                                                (string) Config["Default", "ChatName"], item));
                                    }
                                    else
                                        SendReply(player,
                                            string.Format(
                                                lang.GetMessage("MissingItemFromPackage", this, player.UserIDString),
                                                (string) Config["Default", "ChatName"]));
                                else
                                    SendReply(player,
                                        string.Format(lang.GetMessage("PackageDontExist", this, player.UserIDString),
                                            (string) Config["Default", "ChatName"]));
                                break;
                        }
                    }
                    else
                        SendHelpText(player);
                }
                if (args.Length == 6)
                {
                    type = args[0].ToLower();
                    action = args[1].ToLower();
                    package = args[2].ToLower();
                    item = args[3].ToLower();
                    int minamount = int.Parse(args[4]);
                    int maxamount = int.Parse(args[5]);

                    if (action == "add")
                    {
                        if (type == "k" || type == "killer")
                            type = "killer";
                        else
                            type = "runner";
                        if (SavedReward[type].ContainsKey(package))
                        {

                            if (SavedReward[type][package].RewardItems.ContainsKey(item))
                            {
                                SendReply(player,
                                    string.Format(lang.GetMessage("ItemExist", this, player.UserIDString),
                                        (string) Config["Default", "ChatName"]));
                                return;
                            }
                            SavedReward[type][package].RewardItems.Add(item, new ValueAmount
                            {
                                MinValue = minamount,
                                MaxValue = maxamount
                            });
                            SendReply(player,
                                string.Format(lang.GetMessage("ItemAdded", this, player.UserIDString),
                                    (string) Config["Default", "ChatName"], item, package));
                            SaveLoadedData();
                        }
                        else
                        {
                            SavedReward[type].Add(package, new RewardData
                            {
                                RewardItems = new Dictionary<string, ValueAmount>
                                {
                                    {
                                        item, new ValueAmount
                                        {
                                            MinValue = minamount,
                                            MaxValue = maxamount
                                        }
                                    }
                                }
                            });
                            SendReply(player,
                                string.Format(lang.GetMessage("PackageAdded", this, player.UserIDString),
                                    (string) Config["Default", "ChatName"], package));
                            SaveLoadedData();
                        }
                    }
                    else
                        SendHelpText(player);
                }
        }
    }
}