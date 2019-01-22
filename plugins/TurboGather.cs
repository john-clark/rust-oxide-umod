/*
 * TODO:
 * 
 * - finish /cancelturbo
 * - add GUI start / finish to other methods other than just StartTurbo for the /turbo command (admin given turbo, Global turbo and animal turbo)
 */

using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Configuration;
using System.Globalization;
using UnityEngine;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("TurboGather", "redBDGR", "1.1.11", ResourceId = 2221)]
    [Description("Lets players activate a resouce gather boost for a certain amount of time")]

    class TurboGather : RustPlugin
    {
        #region Data

        //
        // Shoutouts to k1lly0u for help with the database stuff
        //

        private DynamicConfigFile turboGatherData;
        StoredData storedData;

        Dictionary<string, Information> cacheDictionary = new Dictionary<string, Information>();
        Dictionary<string, bool> GUIinfo = new Dictionary<string, bool>();

        static List<object> Animals()
        {
            var al = new List<object>();
            al.Add("player");
            al.Add("boar");
            al.Add("horse");
            al.Add("stag");
            al.Add("chicken");
            al.Add("wolf");
            al.Add("bear");
            return al;
        }
        List<object> TurboAnimals;

        List<string> turboweapons = new List<string>();

        class StoredData
        {
            public Dictionary<string, Information> turboGatherInformation = new Dictionary<string, Information>();
        }

        class Information
        {
            public bool turboEnabled;
            public double activeAgain;
            public double turboEndTime;
            public bool adminTurboGiven;
            public double adminMultiplierGiven;
        }

        void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                EndGUI(player);
            SaveData();
        }

        void OnServerSave()
        {
            SaveData();
        }

        void SaveData()
        {
            storedData.turboGatherInformation = cacheDictionary;
            turboGatherData.WriteObject(storedData);
        }

        void LoadData()
        {
            try
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(this.Title);
                cacheDictionary = storedData.turboGatherInformation;
            }
            catch
            {
                Puts("Failed to load data, creating new file");
                storedData = new StoredData();
            }
        }
        #endregion

        #region Config / Lang

        bool Changed = false;

        // default
        public float activeTime = 30.0f;
        public double cooldownTime = 600.0;
        public double boostMultiplier = 1.0;

        public double endTime;

        public const string permissionName = "turbogather.use";
        public const string permissionNameVIP = "turbogather.vip";
        public const string permissionNameANIMAL = "turbogather.animal";
        public const string permissionNameADMIN = "turbogather.admin";

        // vip
        public float activeTimeVIP = 30.0f;
        public double cooldownTimeVIP = 600.0;
        public double boostMultiplierVIP = 1.0;

        // global
        public bool globalBoostEnabled = false;
        public float activeTimeGLOBAL;
        public double boostMultiplierGLOBAL;

        // animal
        public float activeTimeANIMAL = 30.0f;
        public double boostMultiplierANIMAL = 1.0;

        // gather modes
        public bool dispenserEnabled = true;
        public bool pickupEnabled = true;
        public bool quarryEnabled = true;

        public bool activateTurboOnAnimalKill = false;

        public string PrefixName = "[<color=#0080ff>TurboGather</color>]";

        public bool effectEnabled = true;
        public const string effect = "assets/prefabs/locks/keypad/effects/lock.code.shock.prefab";

        public bool GUIEnabled = true;
        public string minAnchor = "0.77 0.025";
        public string maxAnchor = "0.85 0.13";

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        //
        // Shoutouts to fujikura
        //

        void LoadVariables()
        {
            boostMultiplier = Convert.ToSingle(GetConfig("Options", "Boost multiplier", 1.0f));
            activeTime = Convert.ToSingle(GetConfig("Options", "Active time", 30.0f));
            cooldownTime = Convert.ToSingle(GetConfig("Options", "Cooldown time", 600.0f));

            boostMultiplierVIP = Convert.ToSingle(GetConfig("Options", "Boost multiplier VIP", 1.0f));
            activeTimeVIP = Convert.ToSingle(GetConfig("Options", "Active time VIP", 30.0f));
            cooldownTimeVIP = Convert.ToSingle(GetConfig("Options", "Cooldown time VIP", 600.0f));

            boostMultiplierANIMAL = Convert.ToSingle(GetConfig("Options", "Boost multiplier ANIMAL", 1.0f));
            activeTimeANIMAL = Convert.ToSingle(GetConfig("Options", "Active time ANIMAL", 30.0f));

            dispenserEnabled = Convert.ToBoolean(GetConfig("Settings", "Dispensers enabled", true));
            pickupEnabled = Convert.ToBoolean(GetConfig("Settings", "Pickups enabled", true));
            quarryEnabled = Convert.ToBoolean(GetConfig("Settings", "Quarries enabled", true));

            PrefixName = Convert.ToString(GetConfig("Settings", "Prefix Name", "[<color=#0080ff>TurboGather</color>]"));

            effectEnabled = Convert.ToBoolean(GetConfig("Settings", "Effect Enabled", true));

            TurboAnimals = (List<object>)GetConfig("Settings", "Enabled animals", Animals());

            activateTurboOnAnimalKill = Convert.ToBoolean(GetConfig("Settings", "Activate Turbo on Animal Kill", false));

            GUIEnabled = Convert.ToBoolean(GetConfig("Settings", "GUI Enabled", GUIEnabled));
            minAnchor = Convert.ToString(GetConfig("Settings", "GUI Min Anchor", ""));
            maxAnchor = Convert.ToString(GetConfig("Settings", "GUI Max Anchor", ""));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        void Loaded()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["BoostStart"] = "<color=#00FFFF>Engaging turbo gather! (x{0} resources for {1}s) </color>",
                ["BoostEnd"] = "<color=#00FFFF>Your ability has ended! (available again in {0}s) </color>",
                ["NoPermissions"] = "<color=#B20000>You do not have the required permissions to use this command! </color>",
                ["AlreadyInUse"] = "<color=#B20000>Your ability is already in use!</color>",
                ["OnCooldown"] = "<color=#B20000>You are currently on cooldown! ({0}s remaining) </color>",
                ["CooldownEnded"] = "<color=#00FFFF>Your cooldown has ended! </color>",
                ["AdminInvalidSyntax"] = "<color=#B20000>Invalid syntax! /giveturbo <playername> <length> <multiplier> </color>",
                ["PlayerOffline"] = "<color=#B20000>The playername / ID you entered is not online or invalid! </color>",
                ["PlayerGivenTurbo"] = "<color=#00FFFF>{0}'s TurboGather has been activated! (x{1} for {2}s) </color>",
                ["PlayerGivenTurbo(CONSOLE)"] = "{0}'s TurboGather has been activated! (x{1} for {2}s)",
                ["AdminBoostEnd"] = "<color=#00FFFF>Your admin applied ability has ended! </color>",
                ["AdminBoostStart"] = "<color=#00FFFF>An admin has given you turbo gather! (x{0} resources for {1}s) </color>",
                ["AnimalBoostEnd"] = "<color=#00FFFF>Your ability has ended!</color>",
                ["InvalidSyntaxGlobal"] = "<color=#B20000>Invalid syntax! /globalturbo <length> <multiplier> </color>",
                ["GlobalTurboInvoke"] = "<color=#00FFFF>An admin has started a global turbogather! (x{0} resources for {1}s!)</color>",
                ["GlobalTurboEnd"] = "<color=#00FFFF>Global turbogather has ended!</color>",

            }, this);

            turboGatherData = Interface.Oxide.DataFileSystem.GetFile("TurboGather");
        }

        #endregion

        #region Init

        void Init()
        {
            LoadVariables();
            permission.RegisterPermission(permissionName, this);
            permission.RegisterPermission(permissionNameVIP, this);
            permission.RegisterPermission(permissionNameADMIN, this);
            permission.RegisterPermission(permissionNameANIMAL, this);
            LoadData();
            AddWeapons();

            foreach (var key in cacheDictionary.Keys)
            {
                if (cacheDictionary[key].turboEndTime < GrabCurrentTime())
                {
                    BasePlayer player = BasePlayer.Find(key);
                    if (player == null) return;
                    if (permission.UserHasPermission(player.UserIDString, permissionNameADMIN) || permission.UserHasPermission(player.UserIDString, permissionName) || permission.UserHasPermission(player.UserIDString, permissionNameVIP) || permission.UserHasPermission(player.UserIDString, permissionNameANIMAL))
                        cacheDictionary[key].turboEnabled = false;
                    else
                        cacheDictionary.Remove(key);
                }
                else
                    cacheDictionary[key].turboEnabled = true;

            }
        }

        void AddWeapons()
        {
            turboweapons.Add("rock.entity");
            turboweapons.Add("hatchet.entity");
            turboweapons.Add("pickaxe.entity");
            turboweapons.Add("stone_pickaxe.entity");
            turboweapons.Add("stonehatchet.entity");
            turboweapons.Add("icepick_salvaged.entity");
            turboweapons.Add("axe_salvaged.entity");
            turboweapons.Add("hammer_salvaged.entity");
        }

        #endregion

        // handling animal / player killing
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!activateTurboOnAnimalKill) return;
            if (!(info?.Initiator is BasePlayer)) return;
            BasePlayer player = info.InitiatorPlayer;
            if (!permission.UserHasPermission(info.InitiatorPlayer.UserIDString, permissionNameANIMAL)) return;
            if (cacheDictionary.ContainsKey(info.InitiatorPlayer.UserIDString))
            {
                if (TurboAnimals.Contains(entity.ShortPrefabName))
                    StartAnimalTurbo(player);
            }
            else
                if (TurboAnimals.Contains(entity.ShortPrefabName))
                {
                    cacheDictionary.Add(player.UserIDString, new Information { turboEnabled = false, activeAgain = GrabCurrentTime(), turboEndTime = 0 });
                    StartAnimalTurbo(player);
                }
        }

        void OnPlayerDie(BasePlayer player, HitInfo info)
        {
            EndGUI(player);
        }

        void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (!effectEnabled) return;
            if (info?.HitEntity == null) return;
            if (cacheDictionary.ContainsKey(attacker.UserIDString))
                if (cacheDictionary[attacker.UserIDString].turboEnabled || cacheDictionary[attacker.UserIDString].adminTurboGiven || globalBoostEnabled)
                    if (turboweapons.Contains(info.Weapon.ShortPrefabName))
                    {
                        Effect.server.Run(effect, info.HitPositionWorld);
                        return;
                    }
            return;
        }

        #region Dispenser & Pickups

        void DoGather(BasePlayer player, Item item)
        {
            if (player == null) return;
            if (globalBoostEnabled)
            {
                item.amount = (int)(item.amount * boostMultiplierGLOBAL);
                return;
            }
            if (cacheDictionary.ContainsKey(player.UserIDString))
            {
                if (cacheDictionary[player.UserIDString].adminTurboGiven == true)
                {
                    if (cacheDictionary[player.UserIDString].turboEndTime > GrabCurrentTime())
                        item.amount = (int)(item.amount * cacheDictionary[player.UserIDString].adminMultiplierGiven);
                    else if (cacheDictionary[player.UserIDString].turboEndTime < GrabCurrentTime())
                        cacheDictionary[player.UserIDString].adminTurboGiven = false;
                }
                else
                {
                    if (permission.UserHasPermission(player.UserIDString, permissionName))
                        if (cacheDictionary[player.UserIDString].turboEnabled == true)
                            if (cacheDictionary[player.UserIDString].turboEndTime > GrabCurrentTime())
                            {
                                if (permission.UserHasPermission(player.UserIDString, permissionNameVIP))
                                {
                                    item.amount = (int)(item.amount * boostMultiplierVIP);
                                    return;
                                }
                                item.amount = (int)(item.amount * boostMultiplier);
                            }
                            else if (cacheDictionary[player.UserIDString].turboEndTime < GrabCurrentTime())
                                cacheDictionary[player.UserIDString].turboEnabled = false;
                            else if (permission.UserHasPermission(player.UserIDString, permissionNameVIP))
                                if (cacheDictionary[player.UserIDString].turboEnabled == true)
                                {
                                    if (cacheDictionary[player.UserIDString].turboEndTime > GrabCurrentTime())
                                        item.amount = (int)(item.amount * boostMultiplierVIP);
                                    else if (cacheDictionary[player.UserIDString].turboEndTime < GrabCurrentTime())
                                        cacheDictionary[player.UserIDString].turboEnabled = false;
                                }

                }
            }
        }

        // Dispensers
        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (dispenserEnabled)
                if (entity.ToPlayer() is BasePlayer)
                    DoGather(entity.ToPlayer(), item);
        }

        // collectables / pickups
        void OnCollectiblePickup(Item item, BasePlayer player)
        {
            if (pickupEnabled == true)
                DoGather(player, item);
        }

        // quarries
        void OnQuarryGather(MiningQuarry quarry, Item item)
        {
            if (quarryEnabled == true)
            {
                BasePlayer player = BasePlayer.FindByID(quarry.OwnerID) ?? BasePlayer.FindSleeping(quarry.OwnerID);
                DoGather(player, item);
            }
        }

        void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (dispenserEnabled)
                DoGather(player, item);
        }
        #endregion

        #region Command

        // if (OnEntityDeath = animal / player)
        void StartAnimalTurbo(BasePlayer player)
        {
            if (cacheDictionary.ContainsKey(player.UserIDString))
            {
                endTime = GrabCurrentTime() + activeTimeANIMAL;
                cacheDictionary[player.UserIDString].turboEnabled = true; /*  */ cacheDictionary[player.UserIDString].activeAgain = endTime; /*  */ cacheDictionary[player.UserIDString].turboEndTime = GrabCurrentTime() + activeTimeANIMAL;
                SendReply(player, PrefixName + " " + string.Format(msg("BoostStart", player.UserIDString), boostMultiplierANIMAL, activeTimeANIMAL));

                timer.Once(activeTimeANIMAL, () =>
                {
                    if (player == null) return;
                    cacheDictionary[player.UserIDString].turboEnabled = false;
                    SendReply(player, PrefixName + " " + msg("AnimalBoostEnd", player.UserIDString));
                });
            }
            else return;
        }

        // if player executes the chat command
        void StartTurbo(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, permissionNameVIP))
                endTime = GrabCurrentTime() + cooldownTimeVIP + activeTimeVIP;
            else
                endTime = GrabCurrentTime() + cooldownTime + activeTime;
            cacheDictionary[player.UserIDString].turboEnabled = true;
            cacheDictionary[player.UserIDString].activeAgain = endTime;
            cacheDictionary[player.UserIDString].turboEndTime = GrabCurrentTime() + activeTime;
            if (GUIEnabled)
                StartGui(player);

            if (permission.UserHasPermission(player.UserIDString, permissionNameVIP))
            {
                SendReply(player, PrefixName + " " + string.Format(msg("BoostStart", player.UserIDString), boostMultiplierVIP, activeTimeVIP));

                timer.Once(activeTimeVIP, () =>
                {
                    if (player == null) return;
                    cacheDictionary[player.UserIDString].turboEnabled = false;
                    SendReply(player, PrefixName + " " + string.Format(msg("BoostEnd", player.UserIDString), cooldownTimeVIP));
                    EndGUI(player);
                    float cooldownFloat = Convert.ToSingle(cooldownTimeVIP);

                    timer.Once(cooldownFloat, () =>
                    {
                        if (player != null)
                            SendReply(player, PrefixName + " " + msg("CooldownEnded", player.UserIDString));
                    });
                });
            }
            else
            {
                SendReply(player, PrefixName + " " + string.Format(msg("BoostStart", player.UserIDString), boostMultiplier, activeTime));

                timer.Once(activeTime, () =>
                {
                    if (player == null) return;
                    cacheDictionary[player.UserIDString].turboEnabled = false;
                    SendReply(player, PrefixName + " " + string.Format(msg("BoostEnd", player.UserIDString), cooldownTime));
                    EndGUI(player);
                    float cooldownFloat = Convert.ToSingle(cooldownTime);

                    timer.Once(cooldownFloat, () =>
                    {
                        if (player != null)
                            SendReply(player, PrefixName + " " + msg("CooldownEnded", player.UserIDString));
                    });
                });
            }
            return;
        }

        // chat command /turbo
        [ChatCommand("turbo")]
        void TurboCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName))
            {
                if (!permission.UserHasPermission(player.UserIDString, permissionNameVIP))
                {
                    SendReply(player, msg("NoPermissions", player.UserIDString));
                    return;
                }
            }

            if (cacheDictionary.ContainsKey(player.UserIDString))
            {
                if (GrabCurrentTime() > cacheDictionary[player.UserIDString].activeAgain)
                {
                    if (cacheDictionary[player.UserIDString].turboEnabled == false)
                        StartTurbo(player);

                    else if (cacheDictionary[player.UserIDString].turboEnabled == true)
                    {
                        cacheDictionary[player.UserIDString].turboEnabled = false;
                        StartTurbo(player);
                    }
                }
                else if (cacheDictionary[player.UserIDString].turboEnabled == true)
                    SendReply(player, PrefixName + " " + msg("AlreadyInUse", player.UserIDString));
                else
                {
                    double cooldownTimeLeft = cacheDictionary[player.UserIDString].activeAgain - GrabCurrentTime();
                    SendReply(player, PrefixName + " " + string.Format(msg("OnCooldown", player.UserIDString), (int)cooldownTimeLeft));
                }
            }
            else if (!cacheDictionary.ContainsKey(player.UserIDString))
            {
                cacheDictionary.Add(player.UserIDString, new Information { turboEnabled = false, activeAgain = GrabCurrentTime(), turboEndTime = 0 });
                StartTurbo(player);
            }
            return;
        }

        // chat command for cancelling your own, or another players turbo

            //
            // WORK ON THIS
            //
        [ChatCommand("cancelturbo")]
        void CancelturboCMD(BasePlayer player, string command, string[] args)
        {
            if (args.Length > 0)
            {
                if (!permission.UserHasPermission(player.UserIDString, permissionNameADMIN)) return;

                BasePlayer targetplayer = FindPlayer(args[0]);
                if (cacheDictionary.ContainsKey(targetplayer.UserIDString))
                {
                    cacheDictionary[targetplayer.UserIDString].adminTurboGiven = false;
                    cacheDictionary[targetplayer.UserIDString].turboEnabled = false;
                }
            }
            else
            {
                if (cacheDictionary.ContainsKey(player.UserIDString))
                {
                    cacheDictionary[player.UserIDString].adminTurboGiven = false;
                    cacheDictionary[player.UserIDString].turboEnabled = false;
                    // do other things with setting time and cancelling
                }
            }
        }

        //chat command /giveturbo
        [ChatCommand("giveturbo")]
        void GiveturboCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionNameADMIN))
            {
                SendReply(player, msg("NoPermissions", player.UserIDString));
                return;
            }

            if (args.Length == 3)
            {
                BasePlayer targetPlayer = FindPlayer(args[0]);
                string dictionaryPlayerName = targetPlayer.UserIDString;
                float playerActiveLengthInput = float.Parse(args[1]);
                double playerMultiplierInput = Convert.ToDouble(args[2]);

                if (targetPlayer != null)
                {
                    if (!cacheDictionary.ContainsKey(dictionaryPlayerName))
                        cacheDictionary.Add(targetPlayer.UserIDString, new Information { turboEnabled = false, activeAgain = GrabCurrentTime(), turboEndTime = GrabCurrentTime() + playerActiveLengthInput, adminTurboGiven = true, adminMultiplierGiven = playerMultiplierInput });
                    else
                    {
                        cacheDictionary[dictionaryPlayerName].turboEndTime = GrabCurrentTime() + playerActiveLengthInput;
                        cacheDictionary[dictionaryPlayerName].adminMultiplierGiven = Convert.ToDouble(args[2]);
                        cacheDictionary[dictionaryPlayerName].adminTurboGiven = true;
                    }
                    SendReply(player, PrefixName + " " + string.Format(msg("PlayerGivenTurbo", player.UserIDString), targetPlayer.displayName, playerMultiplierInput, playerActiveLengthInput));
                    targetPlayer.ChatMessage(PrefixName + " " + string.Format(msg("AdminBoostStart", targetPlayer.UserIDString), playerMultiplierInput, playerActiveLengthInput));

                    timer.Once(Convert.ToSingle(playerActiveLengthInput), () =>
                    {
                        cacheDictionary[dictionaryPlayerName].adminTurboGiven = false;
                        if (targetPlayer == null || !targetPlayer.IsConnected) return;
                        targetPlayer.ChatMessage(PrefixName + " " + string.Format(msg("AdminBoostEnd", targetPlayer.UserIDString)));
                    });
                }
                else
                    SendReply(player, PrefixName + " " + string.Format(msg("PlayerOffline", player.UserIDString)));
            }
            else
                SendReply(player, PrefixName + " " + string.Format(msg("AdminInvalidSyntax")));
            return;
        }

        // chat command /global turbo
        [ChatCommand("globalturbo")]
        void GlobalturboCMD(BasePlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.UserIDString, permissionNameADMIN))
            {
                if (globalBoostEnabled)
                {
                    SendReply(player, "Global turbo is already enabled!");
                    return;
                }
                if (args.Length == 2)
                {
                    activeTimeGLOBAL = Convert.ToSingle(args[0]);
                    boostMultiplierGLOBAL = Convert.ToDouble(args[1]);
                    globalBoostEnabled = true;
                    PrintToChat(PrefixName + " " + string.Format(msg("GlobalTurboInvoke"), boostMultiplierGLOBAL, activeTimeGLOBAL));
                    timer.Once(activeTimeGLOBAL, () =>
                    {
                        if (globalBoostEnabled)
                        {
                            PrintToChat(PrefixName + " " + string.Format(msg("GlobalTurboEnd")));
                            globalBoostEnabled = false;
                        }
                    });
                }
                else
                    SendReply(player, PrefixName + " " + string.Format(msg("InvalidSyntaxGlobal")));
            }
            else
                SendReply(player, PrefixName + " " + string.Format(msg("NoPermissions", player.UserIDString)));
            return;
        }

        // console command for starting / stopping global turbogather
        [ConsoleCommand("globalturbo")]
        void GlobalturboconsoleCMD(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            //start
            if (arg.Args != null && arg.Args[0] == "start")
            {
                if (arg.Args.Length == 3)
                {
                    if (globalBoostEnabled)
                        Puts("Global turbo is already enabled!");
                    else
                    {
                        globalBoostEnabled = true;
                        activeTimeGLOBAL = Convert.ToSingle(arg.Args[1]);
                        boostMultiplierGLOBAL = Convert.ToDouble(arg.Args[2]);
                        PrintToChat(string.Format(msg("GlobalTurboInvoke"), boostMultiplierGLOBAL, activeTimeGLOBAL));
                        Puts("Global Turbo has been activated! ( " + boostMultiplierGLOBAL + "x for " + activeTimeGLOBAL + "s )");
                        timer.Once(activeTimeGLOBAL, () =>
                        {
                            if (globalBoostEnabled)
                            {
                                PrintToChat(string.Format(msg("GlobalTurboEnd")));
                                globalBoostEnabled = false;
                            }
                        });
                    }
                }
                else Puts("Invalid syntax! /globalturbo start <length> <multiplier>");
            }
            //end
            else if (arg.Args != null && arg.Args[0] == "end")
            {
                if (globalBoostEnabled)
                {
                    globalBoostEnabled = false;
                    PrintToChat(string.Format(msg("GlobalTurboEnd")));
                }
                else
                    Puts("Global turbo is already disabled!");
            }
            else if (arg.Args == null || arg.Args.Length == 0)
            {
                Puts("Invalid syntax! /globalturbo <start/end>");
            }
        }

        // cancelling globalturbo (emergency?)
        [ChatCommand("cancelglobalturbo")]
        void CancelglobalturboCMD(BasePlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.UserIDString, permissionNameADMIN))
            {
                if (args == null || args.Length <= 0)
                {
                    if (globalBoostEnabled)
                    {
                        globalBoostEnabled = false;
                        PrintToChat(PrefixName + " " + msg("GlobalTurboEnd"));
                    }
                    else
                        SendReply(player, "Global turbo is already disabled!");
                }
                return;
            }
            else
                SendReply(player, PrefixName + " " + msg("NoPermissions", player.UserIDString));
        }

        // console command giveturbo
        [ConsoleCommand("giveturbo")]
        void GiveturboconsoleCMD(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            if (arg.Args == null || arg.Args.Length != 3)
            {
                Puts("Invalid syntax! giveturbo <playername> <length> <multiplier>");
                return;
            }
            else
            {
                string playerNameInput = arg.Args[0];
                BasePlayer player = FindPlayer(playerNameInput);
                float playerActiveLengthInput = float.Parse(arg.Args[1]);
                double playerInputMultiplier = Convert.ToDouble(arg.Args[2]);

                if (player != null)
                {
                    if (!cacheDictionary.ContainsKey(player.UserIDString))
                        cacheDictionary.Add(player.UserIDString, new Information { turboEnabled = false, activeAgain = GrabCurrentTime(), turboEndTime = GrabCurrentTime() + playerActiveLengthInput, adminTurboGiven = true, adminMultiplierGiven = playerInputMultiplier });
                    else
                    {
                        cacheDictionary[player.UserIDString].turboEndTime = GrabCurrentTime() + playerActiveLengthInput;
                        cacheDictionary[player.UserIDString].adminMultiplierGiven = playerInputMultiplier;
                        cacheDictionary[player.UserIDString].adminTurboGiven = true;
                    }

                    Puts(string.Format(msg("PlayerGivenTurbo(CONSOLE)"), player.UserIDString, playerInputMultiplier, playerActiveLengthInput));
                    player.ChatMessage(string.Format(msg("AdminBoostStart", playerNameInput), playerInputMultiplier, playerActiveLengthInput));

                    timer.Once(Convert.ToSingle(playerActiveLengthInput), () =>
                    {
                        if (player == null) return;
                        player.ChatMessage(string.Format(msg("AdminBoostEnd", playerNameInput)));
                        cacheDictionary[player.UserIDString].adminTurboGiven = false;
                    });
                }
                else
                    Puts("The playername / ID you entered is not online or invalid!");
            }
            return;
        }

        #endregion

        #region UI

        private string PanelOnScreen = "OnScreen";
        private string Panel = "TGPanel";

        public class UI
        {
            static public CuiElementContainer CreateElementContainer(string panel, string color, string aMin, string aMax, bool cursor = false)
            {
                var NewElement = new CuiElementContainer()
            {
                {
                    new CuiPanel
                    {
                        Image = {Color = color},
                        RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                        CursorEnabled = cursor
                    },
                    new CuiElement().Parent,
                    panel
                }
            };
                return NewElement;
            }

            static public void CreateImage(ref CuiElementContainer element, string panel, string imageURL, string aMin, string aMax)
            {
                element.Add(new CuiElement
                {
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent { Url = imageURL, Color = "1 1 1 1" },
                        new CuiRectTransformComponent {AnchorMax = aMax, AnchorMin = aMin }
                    }
                });
            }
        }

        #endregion

        #region Extras

        void StartGui(BasePlayer player)
        {
            if (GUIinfo.ContainsKey(player.UserIDString))
            {
                if (!GUIinfo[player.UserIDString])
                {
                    var element = UI.CreateElementContainer(Panel, "1 1 1 0", "0 0", "1 1", false);
                    UI.CreateImage(ref element, Panel, "http://i.imgur.com/eLFi2Gd.png", minAnchor, maxAnchor);
                    CuiHelper.AddUi(player, element);
                    GUIinfo[player.UserIDString] = true;
                }
                return;
            }
            else
            {
                var element = UI.CreateElementContainer(Panel, "1 1 1 0", "0 0", "1 1", false);
                UI.CreateImage(ref element, Panel, "http://i.imgur.com/eLFi2Gd.png", minAnchor, maxAnchor);
                CuiHelper.AddUi(player, element);
                GUIinfo[player.UserIDString] = true;
            }
            return;
        }

        void EndGUI(BasePlayer player)
        {
            if (GUIinfo.ContainsKey(player.UserIDString))
            {
                if (GUIinfo[player.UserIDString])
                {
                    CuiHelper.DestroyUi(player, Panel);
                    GUIinfo[player.UserIDString] = false;
                }
            }
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

        double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
        string msg(string key, string id = null) => lang.GetMessage(key, this, id);

        private static BasePlayer FindPlayer(string nameOrId)
        {
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString == nameOrId)
                    return activePlayer;
                if (activePlayer.displayName.Contains(nameOrId, CompareOptions.OrdinalIgnoreCase))
                    return activePlayer;
                if (activePlayer.net?.connection != null && activePlayer.net.connection.ipaddress == nameOrId)
                    return activePlayer;
            }
            return null;
        }

        #endregion

    }
}