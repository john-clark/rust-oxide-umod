﻿using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.IO;
using System;
using Oxide.Game.Rust.Cui;
using Oxide.Core;
using Newtonsoft.Json;
using UnityEngine;
using Network;
using Rust;

namespace Oxide.Plugins
{
    [Info("Kill Feed", "Tuntenfisch", "1.17", ResourceId = 1433)]
    [Description("Displays a basic Kill Feed on screen!")]
    public class KillFeed : RustPlugin
    {
        #region Fields
        const float _screenAspectRatio = 9f / 16f;
        const float _height = 0.1f;
        const float _halfHeight = _height / 2f;

        static int _debugging;

        static bool useServerFileStorage;

        static char _formattingChar = '§';

        static List<string> debugLog;

        static GameObject _killFeedObject;

        static FileManager _fileManager;

        static Timer _timer;

        static Dictionary<ulong, KillFeedPlayer> _players;

        float width;
        float horizontalSpacing;
        float verticalSpacing;
        float fadeIn;
        float fadeOut;
        float destroyAfter;
        float iconHalfHeight;
        float iconHalfWidth;
        float halfWidth;

        int numberOfCharacters;
        int fontSize;

        bool outline;
        bool removeTags;
        bool removeSpecialCharacters;
        bool enableAnimals;
        bool displayPlayerDeaths;
        bool logEntries;
        bool printEntriesToConsole;
        bool[] allowedCharacters;

        string chatIcon;
        string font;
        string formatting;

        Vector2 anchormin;
        Vector2 anchormax;

        CuiElementContainer[] entries;

        List<string> entryLog;
        #endregion

        #region Hooks
        /// <summary>
        /// Effectively, the entry point for this plugin.
        /// </summary>
        void OnServerInitialized()
        {
            // initialize _killFeedObject
            _killFeedObject = new GameObject();

            // initialize _fileManager
            _fileManager = _killFeedObject.AddComponent<FileManager>();

            // initialize _timer
            _timer = _killFeedObject.AddComponent<Timer>();

            // initialize allowedCharacters and populate initial allowedCharacters population
            allowedCharacters = new bool[65536];
            for (char c = '0'; c <= '9'; c++) allowedCharacters[c] = true;
            for (char c = 'A'; c <= 'Z'; c++) allowedCharacters[c] = true;
            for (char c = 'a'; c <= 'z'; c++) allowedCharacters[c] = true;

            LoadConfig();

            LoadDefaultLang();

            // initialize and populate _players
            _players = new Dictionary<ulong, KillFeedPlayer>();
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                AddPlayer(player);
            }

            if (logEntries)
            {
                if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Title}/EntryLog"))
                {
                    entryLog = Interface.Oxide.DataFileSystem.ReadObject<List<string>>($"{Title}/EntryLog");        // either get existing entryLog or...
                }
                else
                {
                    entryLog = new List<string>();                                                                  // ...create a new entryLog if no existing entryLog is found
                }
            }
            if (_debugging == 2)
            {
                if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Title}/DebugLog"))
                {
                    debugLog = Interface.Oxide.DataFileSystem.ReadObject<List<string>>($"{Title}/DebugLog");        // either get existing debugLog or...
                }
                else
                {
                    debugLog = new List<string>();                                                                  // ...create a new debugLog if no existing debugLog is found
                }
            }
        }

        /// <summary>
        /// Makes sure that the plugin doesn't leave traces behind if the plugin is unloaded.
        /// </summary>
        void Unload()
        {
            if (entries != null)
            {
                DestroyUI(ConvertToSingleContainer(entries));
            }

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                RemovePlayer(player);
            }

            if (logEntries && entryLog.Count > 0)
            {
                Interface.Oxide.DataFileSystem.WriteObject($"{Title}/EntryLog", entryLog);                          // save the entryLog to EntryLog.json if necessary
            }
            if (_debugging == 2 && debugLog.Count > 0)
            {
                Interface.Oxide.DataFileSystem.WriteObject($"{Title}/DebugLog", debugLog);                          // save the debugLog to DebugLog.json if necessary
            }

            UnityEngine.Object.Destroy(_killFeedObject);
        }

        /// <summary>
        /// Adds a new player to _players if one connects.
        /// </summary>
        /// <param name="player"> The player who connected.</param>
        void OnPlayerInit(BasePlayer player)
        {
            AddPlayer(player);
        }

        /// <summary>
        /// Removes an existing player from the _players if he disconnects.
        /// </summary>
        /// <param name="player"> The <c>BasePlayer</c> instance which disconnected.</param>
        void OnPlayerDisconnected(BasePlayer player)
        {
            RemovePlayer(player);
        }

        /// <summary>
        /// The entry point for a new Kill Feed entry. Only encompasses players being wounded.
        /// </summary>
        /// <remarks>
        /// If <c>displayPlayerDeaths</c> is true, player deaths will be processed by <c>OnEntityDeath(BaseCombatEntity entity, HitInfo info)</c> instead.
        /// </remarks>
        /// <param name="player"> The player who was wounded.</param>
        /// <param name="info"> Contains information about the, in this case, wounding hit.</param>
        void CanBeWounded(BasePlayer player, HitInfo info)
        {
            if (displayPlayerDeaths) return;

            if (info == null) return;

            if (!enableAnimals && info.Initiator is BaseNpc) return;                                            // if animals are off and the initiator is an animal, return

            EntryData entryData = new EntryData(player, info);

            if (entryData.weaponInfo == null) return;

            OnWoundedOrDeath(entryData);
        }

        /// <summary>
        /// The second entry point for a new Kill Feed entry. Encompasses all entity deaths.
        /// </summary>
        /// <param name="entity"> The entity that died.</param>
        /// <param name="info"> Contains information about the, in this case, killing hit.</param>
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!enableAnimals && !displayPlayerDeaths) return;

            if (info == null) return;

            if (!enableAnimals && (info.Initiator is BaseNpc || entity is BaseNpc)) return;                     // if animals are off and either the initator or the entity is an animal, return
            if (!displayPlayerDeaths && entity.ToPlayer() != null) return;

            if (!(entity is BaseNpc) && !(entity is BaseHelicopter) && entity.ToPlayer() == null) return;       // if the entity isn't an animal, patrolhelicopter or player, return
            if ((info.Initiator is BaseNpc || info.Initiator is BaseHelicopter) && entity is BaseNpc) return;   // if the initator is either an animal or a patrolhelicopter and the entity is an animal, return
            if (entity is BaseNpc && (info.damageTypes?.GetMajorityDamageType() == DamageType.Hunger
                || info.damageTypes?.GetMajorityDamageType() == DamageType.Thirst)) return;                     // if an animal either died of hunger or of thirst, return

            EntryData entryData = new EntryData(entity, info);

            if (entryData.weaponInfo == null) return;

            OnWoundedOrDeath(entryData);
        }
        #endregion

        #region Commands
        /// <summary>
        /// Chat command for disabling, enabling the visual component of the plugin on a per player basis.
        /// </summary>
        /// <param name="player"> The player who used the command.</param>
        /// <param name="command"> The command that was entered.</param>
        /// <param name="args"> List of chat arguments trailing the initial command.</param>
        [ChatCommand("killfeed")]
        void ChatCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)                                                                               // if no arguments are given, output additional information about possible arguments to the player
            {
                player.SendConsoleCommand("chat.add", chatIcon, lang.GetMessage("killfeed", this));
            }
            else if (args[0].Equals("enable"))                                                                  // the player wants to enable the visual component of the plugin
            {
                KillFeedPlayer p;
                if (_players.TryGetValue(player.userID, out p))
                {
                    if (!p.enabled)
                    {
                        p.enabled = true;
                        AddUI(ConvertToSingleContainer(entries), p);

                        player.SendConsoleCommand("chat.add", chatIcon, lang.GetMessage("killfeed enable > enabled", this));
                    }
                    else
                    {
                        player.SendConsoleCommand("chat.add", chatIcon, lang.GetMessage("killfeed enable > already enabled", this));
                    }
                }
            }
            else if (args[0].Equals("disable"))                                                                 // the player wants to disable the visual component of the plugin
            {
                KillFeedPlayer p;
                if (_players.TryGetValue(player.userID, out p))
                {
                    if (p.enabled)
                    {
                        DestroyUI(ConvertToSingleContainer(entries), p);
                        p.enabled = false;

                        player.SendConsoleCommand("chat.add", chatIcon, lang.GetMessage("killfeed disable > disabled", this));
                    }
                    else
                    {
                        player.SendConsoleCommand("chat.add", chatIcon, lang.GetMessage("killfeed disable > already disabled", this));
                    }
                }
            }
            else if (args[0].Equals("status"))                                                                  // the player wants to know whether the visual component of the plugin is enabled or disabled
            {
                KillFeedPlayer p;
                if (_players.TryGetValue(player.userID, out p))
                {
                    if (p.enabled)
                    {
                        player.SendConsoleCommand("chat.add", chatIcon, lang.GetMessage("killfeed status > is enabled", this));
                    }
                    else
                    {
                        player.SendConsoleCommand("chat.add", chatIcon, lang.GetMessage("killfeed status > is disabled", this));
                    }
                }
            }
        }
        #endregion

        #region Config
        /// <summary>
        /// Provides the default values for each configuration file value for easy access.
        /// </summary>
        static class DefaultConfig
        {
            public static readonly Dictionary<string, ConfigValue> values = new Dictionary<string, ConfigValue>()
            {
                { "chatIcon",                   new ConfigValue("76561198263554080",                                                        "1. General", "1.1 chat icon") },
                { "font",                       new ConfigValue("robotocondensed-bold.ttf",                                                 "1. General", "1.2 Text", "1.2.1 font") },
                { "outline",                    new ConfigValue(false,                                                                      "1. General", "1.2 Text", "1.2.2 outline") },
                { "fontSize",                   new ConfigValue(18,                                                                         "1. General", "1.2 Text", "1.2.3 font size") },
                { "numberOfCharacters",         new ConfigValue(12,                                                                         "1. General", "1.2 Text", "1.2.4 number of characters") },
                { "removeSpecialCharacters",    new ConfigValue(true,                                                                       "1. General", "1.2 Text", "1.2.5 remove special characters") },
                { "removeTags",                 new ConfigValue(false,                                                                      "1. General", "1.2 Text", "1.2.6 remove tags") },
                { "enableAnimals",              new ConfigValue(true,                                                                       "1. General", "1.3 Eligible For Entry", "1.3.1 animals") },
                { "displayPlayerDeaths",        new ConfigValue(false,                                                                      "1. General", "1.3 Eligible For Entry", "1.3.2 player deaths") },
                { "logEntries",                 new ConfigValue(false,                                                                      "1. General", "1.4 Monitoring", "1.4.1 log entries") },
                { "printEntriesToConsole",      new ConfigValue(false,                                                                      "1. General", "1.4 Monitoring", "1.4.2 print entries to console") },
                { "debugging",                  new ConfigValue(0,                                                                          "1. General", "1.4 Monitoring", "1.4.3 debugging") },

                { "formatting",                 new ConfigValue("{initiator}§{hitBone}§{distance}§{hitEntity}",                             "2. Kill Feed", "2.1 formatting") },
                { "numberOfEntries",            new ConfigValue(3,                                                                          "2. Kill Feed", "2.2 number of entries") },
                { "destroyAfter",               new ConfigValue(30.0f,                                                                      "2. Kill Feed", "2.3 destroy after") },
                { "width",                      new ConfigValue(0.3f,                                                                       "2. Kill Feed", "2.4 Dimensions", "2.4.1 width") },
                { "iconHalfHeight",             new ConfigValue(0.35f,                                                                      "2. Kill Feed", "2.4 Dimensions", "2.4.2 icon half-height") },
                { "x",                          new ConfigValue(0.175f,                                                                     "2. Kill Feed", "2.5 Position", "2.5.1 x") },
                { "y",                          new ConfigValue(0.95f,                                                                      "2. Kill Feed", "2.5 Position", "2.5.2 y") },
                { "horizontal",                 new ConfigValue(0.0f,                                                                       "2. Kill Feed", "2.6 Spacing", "2.6.1 horizontal") },
                { "vertical",                   new ConfigValue(-0.005f,                                                                    "2. Kill Feed", "2.6 Spacing", "2.6.2 vertical") },
                { "in",                         new ConfigValue(0.0f,                                                                       "2. Kill Feed", "2.7 Fade", "2.7.1 in") },
                { "out",                        new ConfigValue(0.0f,                                                                       "2. Kill Feed", "2.7 Fade", "2.7.2 out") },
                { "initiator",                  new ConfigValue("#336699",                                                                  "2. Kill Feed", "2.8 Colors", "2.8.1 inititator") },
                { "info",                       new ConfigValue("#b38600",                                                                  "2. Kill Feed", "2.8 Colors", "2.8.2 info") },
                { "hitEntity",                  new ConfigValue("#800000",                                                                  "2. Kill Feed", "2.8 Colors", "2.8.3 hit entity") },
                { "npc",                        new ConfigValue("#267326",                                                                  "2. Kill Feed", "2.8 Colors", "2.8.4 npc") },

                { "useServerFileStorage",       new ConfigValue(true,                                                                       "3. Data", "3.1 use server file storage") },
                { "fileDirectory",              new ConfigValue("http://vignette1.wikia.nocookie.net/play-rust/images/",                    "3. Data", "3.2 file directory") },
                { "Files",                      new ConfigValue(GetDefaultFiles(),                                                          "3. Data", "3.3 Files") },
                { "DamageTypeFiles",            new ConfigValue(GetDefaultDamageTypeFiles(),                                                "3. Data", "3.4 Damagetype Files") },
                { "NPCNames",                   new ConfigValue(GetDefaultNPCNames(),                                                       "3. Data", "3.5 NPC Names") },
                { "BoneNames",                  new ConfigValue(GetDefaultBoneNames(),                                                      "3. Data", "3.6 Bone Names") },
                { "AllowedSpecialCharacters",   new ConfigValue(GetDefaultAllowedSpecialCharacters(),                                       "3. Data", "3.7 Allowed Special Characters") }
            };

            private static Dictionary<string, string> GetDefaultFiles()
            {
                Dictionary<string, string> files = new Dictionary<string, string>()
                {
                    { "autoturret", "f/f9/Auto_Turret_icon.png" },
                    { "axe.salvaged", "c/c9/Salvaged_Axe_icon.png" },
                    { "barricade.metal", "b/bb/Metal_Barricade_icon.png" },
                    { "barricade.wood", "e/e5/Wooden_Barricade_icon.png" },
                    { "barricade.woodwire", "7/7b/Barbed_Wooden_Barricade_icon.png" },
                    { "bone.club", "1/19/Bone_Club_icon.png" },
                    { "bow.hunting", "2/25/Hunting_Bow_icon.png" },
                    { "crossbow", "2/23/Crossbow_icon.png" },
                    { "explosive.satchel", "0/0b/Satchel_Charge_icon.png" },
                    { "explosive.timed", "6/6c/Timed_Explosive_Charge_icon.png" },
                    { "flamethrower", "5/55/Flame_Thrower_icon.png" },
                    { "flameturret", "f/f9/Flame_Turret_icon.png" },
                    { "gates.external.high.stone", "8/85/High_External_Stone_Gate_icon.png" },
                    { "gates.external.high.wood", "5/53/High_External_Wooden_Gate_icon.png" },
                    { "grenade.beancan", "b/be/Beancan_Grenade_icon.png" },
                    { "grenade.f1", "5/52/F1_Grenade_icon.png" },
                    { "guntrap", "http://i.imgur.com/10MOtwu.png" },
                    { "hammer.salvaged", "f/f8/Salvaged_Hammer_icon.png" },
                    { "hatchet", "0/06/Hatchet_icon.png" },
                    { "icepick.salvaged", "e/e1/Salvaged_Icepick_icon.png" },
                    { "knife.bone", "c/c7/Bone_Knife_icon.png" },
                    { "landmine", "8/83/Land_Mine_icon.png" },
                    { "lmg.m249", "c/c6/M249_icon.png" },
                    { "lock.code", "0/0c/Code_Lock_icon.png" },
                    { "longsword", "3/34/Longsword_icon.png" },
                    { "mace", "4/4d/Mace_icon.png" },
                    { "machete", "3/34/Machete_icon.png" },
                    { "pickaxe", "8/86/Pick_Axe_icon.png" },
                    { "pistol.eoka", "b/b5/Eoka_Pistol_icon.png" },
                    { "pistol.m92", "4/43/M92_Pistol_icon.png" },
                    { "pistol.python", "d/d4/Python_Revolver_icon.png" },
                    { "pistol.revolver", "5/58/Revolver_icon.png" },
                    { "pistol.semiauto", "6/6b/Semi-Automatic_Pistol_icon.png" },
                    { "rifle.ak", "d/d1/Assault_Rifle_icon.png" },
                    { "rifle.bolt", "5/55/Bolt_Action_Rifle_icon.png" },
                    { "rifle.lr300", "d/d9/LR-300_Assault_Rifle_icon.png" },
                    { "rifle.semiauto", "8/8d/Semi-Automatic_Rifle_icon.png" },
                    { "rock", "f/ff/Rock_icon.png" },
                    { "rocket.launcher", "0/06/Rocket_Launcher_icon.png" },
                    { "salvaged.cleaver", "7/7e/Salvaged_Cleaver_icon.png" },
                    { "salvaged.sword", "7/77/Salvaged_Sword_icon.png" },
                    { "shotgun.pump", "6/60/Pump_Shotgun_icon.png" },
                    { "shotgun.waterpipe", "1/1b/Waterpipe_Shotgun_icon.png" },
                    { "shotgun.double", "3/3f/Double_Barrel_Shotgun_icon.png" },
                    { "smg.2", "9/95/Custom_SMG_icon.png" },
                    { "smg.mp5", "c/c0/MP5A4_icon.png" },
                    { "smg.thompson", "4/4e/Thompson_icon.png" },
                    { "spear.stone", "0/0a/Stone_Spear_icon.png" },
                    { "spear.wooden", "f/f2/Wooden_Spear_icon.png" },
                    { "spikes.floor", "f/f7/Wooden_Floor_Spikes_icon.png" },
                    { "stone.pickaxe", "7/77/Stone_Pick_Axe_icon.png" },
                    { "stonehatchet", "9/9b/Stone_Hatchet_icon.png" },
                    { "surveycharge", "9/9a/Survey_Charge_icon.png" },
                    { "torch", "4/48/Torch_icon.png" },
                    { "trap.bear", "b/b0/Snap_Trap_icon.png" },
                    { "wall.external.high", "9/96/High_External_Wooden_Wall_icon.png" },
                    { "wall.external.high.stone", "b/b6/High_External_Stone_Wall_icon.png" }
                };
                return files;
            }

            private static Dictionary<string, string> GetDefaultDamageTypeFiles()
            {
                Dictionary<string, string> files = new Dictionary<string, string>()
                {
                    { "bite", "1/17/Bite_icon.png" },
                    { "bleeding", "e/e5/Bleeding_icon.png" },
                    { "blunt", "8/83/Blunt_icon.png" },
                    { "bullet", "5/5a/Bullet_icon.png" },
                    { "cold", "7/74/Freezing_icon.png" },
                    { "drowned", "8/81/Drowning_icon.png" },
                    { "electricShock", "a/af/Electric_icon.png" },
                    { "explosion", "5/50/Explosion_icon.png" },
                    { "fall", "f/ff/Fall_icon.png" },
                    { "generic", "b/be/Missing_icon.png" },
                    { "heat", "e/e4/Ignite_icon.png" },
                    { "hunger", "8/84/Hunger_icon.png" },
                    { "poison", "8/84/Poison_icon.png" },
                    { "radiation", "4/44/Radiation_icon.png" },
                    { "slash", "5/50/Slash_icon.png" },
                    { "stab", "3/3e/Stab_icon.png" },
                    { "suicide", "b/be/Missing_icon.png" },
                    { "thirst", "8/8e/Thirst_icon.png" }
                };
                return files;
            }

            private static Dictionary<string, string> GetDefaultNPCNames()
            {
                Dictionary<string, string> names = new Dictionary<string, string>()
                {
                    { "bear", "Bear" },
                    { "boar", "Boar" },
                    { "chicken", "Chicken" },
                    { "horse", "Horse" },
                    { "patrolhelicopter", "Helicopter" },
                    { "stag", "Stag" },
                    { "wolf", "Wolf" }
                };
                return names;
            }

            private static Dictionary<string, string> GetDefaultBoneNames()
            {
                Dictionary<string, string> names = new Dictionary<string, string>()
                {
                    { "body", "Body" },
                    { "chest", "Chest" },
                    { "groin", "Groin" },
                    { "head", "Head" },
                    { "hip", "Hip" },
                    { "jaw", "Jaw" },
                    { "left arm", "Arm" },
                    { "left eye", "Eye" },
                    { "left foot", "Foot" },
                    { "left forearm", "Forearm" },
                    { "left hand", "Hand" },
                    { "left knee", "Knee" },
                    { "left ring finger", "Finger" },
                    { "left shoulder", "Shoulder" },
                    { "left thumb", "Thumb" },
                    { "left toe", "Toe" },
                    { "left wrist", "Wrist" },
                    { "lower spine", "Spine" },
                    { "neck", "Neck" },
                    { "pelvis", "Pelvis" },
                    { "right arm", "Arm" },
                    { "right eye", "Eye" },
                    { "right foot", "Foot" },
                    { "right forearm", "Forearm" },
                    { "right hand", "Hand" },
                    { "right knee", "Knee" },
                    { "right ring finger", "Finger" },
                    { "right shoulder", "Shoulder" },
                    { "right thumb", "Thumb" },
                    { "right toe", "Toe" },
                    { "right wrist", "Wrist" },
                    { "stomach", "Stomach" }
                };
                return names;
            }

            private static List<char> GetDefaultAllowedSpecialCharacters()
            {
                List<char> characters = new List<char>
                {
                    '.',
                    ' ',
                    '[',
                    ']',
                    '(',
                    ')',
                    '<',
                    '>'
                };
                return characters;
            }
        }

        /// <summary>
        /// Wrapper for a config value.
        /// </summary>
        /// <remarks>
        /// Contains both, the config value and the corresponding path.
        /// </remarks>
        class ConfigValue
        {
            public object value { private set; get; }
            public string[] path { private set; get; }

            public ConfigValue(object value, params string[] path)
            {
                this.value = value;
                this.path = path;
            }
        }

        /// <summary>
        /// Responsible for getting a value from the configuration file.
        /// </summary>
        /// <typeparam name="T"> The type that should be returned.</typeparam>
        /// <param name="saveConfig"> Indicates whether the configuration file should be saved.</param>
        /// <param name="defaultValue"> The defaultValue that will be returned if the actual value cannot be found.</param>
        /// <param name="keys"> The keys pointing to the value which should be returned.</param>
        /// <returns> Returns either the defaultValue or the value of the configuration file associated with the provided keys.</returns>
        T GetConfig<T>(ref bool saveConfig, T defaultValue, params string[] keys)
        {
            object value = Config.Get(keys);

            // get the value associated with the provided keys and check if the value is valid
            if (!IsValueValid(value, defaultValue))
            {
                object[] objArray = new object[keys.Length + 1];
                for (int i = 0; i < keys.Length; i++)
                {
                    objArray[i] = keys[i];
                }
                objArray[keys.Length] = defaultValue;

                Config.Set(objArray);

                saveConfig = true;
                return defaultValue;
            }
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
        }

        /// <summary>
        /// Overload function for getting a value from the configuration file.
        /// </summary>
        /// <typeparam name="T"> The type that should be returned.</typeparam>
        /// <param name="saveConfig"> Indicates whether the configuration file should be saved.</param>
        /// <param name="configValue"> Holds information both about the actual config value and the corresponding path.</param>
        /// <returns> Returns either the defaultValue or the value of the configuration file associated with the provided keys.</returns>
        T GetConfig<T>(ref bool saveConfig, ConfigValue configValue)
        {
            string[] keys = configValue.path;
            object defaultValue = configValue.value;

            return GetConfig(ref saveConfig, (T)defaultValue, keys);
        }

        /// <summary>
        /// Checks if a value is valid by comparing the types of two objects and checking if both objects are of the same type or not.
        /// </summary>
        /// <param name="value"> The value that needs to be checked for validity.</param>
        /// <param name="defaultValue"> The defaultValue which is used for checking if the value is valid.</param>
        /// <returns> A bool indicating whether the value is valid.</returns>
        bool IsValueValid(object value, object defaultValue)
        {
            if (value == null) return false;

            Type type = value.GetType();
            Type defaultType = defaultValue.GetType();

            if (type != defaultType && type != typeof(double) && defaultType != typeof(float))
            {
                if (type.IsGenericType && defaultType.IsGenericType)
                {
                    if (type.GetGenericTypeDefinition() != defaultType.GetGenericTypeDefinition())
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Checks whether the configuration file contains unused keys and removes them.
        /// </summary>
        void CleanupConfig()
        {
            List<string[]> paths = new List<string[]>();

            // iterate over the current configuration file to find all existing paths
            IEnumerator enumerator = Config.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    KeyValuePair<string, object> pair = (KeyValuePair<string, object>)enumerator.Current;

                    if (pair.Value.GetType().IsGenericType && pair.Value.GetType().GetGenericTypeDefinition() == typeof(Dictionary<,>) && ((IDictionary)pair.Value).Count != 0)
                    {
                        Dictionary<string, object> dictionary = new Dictionary<string, object>();
                        foreach (DictionaryEntry entry in (IDictionary)pair.Value)
                        {
                            dictionary.Add((string)entry.Key, entry.Value);
                        }

                        foreach (KeyValuePair<string, object> pair2 in dictionary)
                        {
                            if (pair2.Value.GetType().IsGenericType && pair2.Value.GetType().GetGenericTypeDefinition() == typeof(Dictionary<,>) && ((IDictionary)pair.Value).Count != 0)
                            {
                                Dictionary<string, object> dictionary2 = new Dictionary<string, object>();
                                foreach (DictionaryEntry entry in (IDictionary)pair2.Value)
                                {
                                    dictionary2.Add((string)entry.Key, entry.Value);
                                }

                                foreach (KeyValuePair<string, object> pair3 in dictionary2)
                                {
                                    if (char.IsDigit(pair3.Key[0]) && pair3.Key[1].Equals('.') && char.IsDigit(pair3.Key[2]) && pair3.Key[3].Equals('.') && char.IsDigit(pair3.Key[4]))
                                    {
                                        paths.Add(new string[3] { pair.Key, pair2.Key, pair3.Key });
                                    }
                                    else
                                    {
                                        paths.Add(new string[2] { pair.Key, pair2.Key });
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                paths.Add(new string[2] { pair.Key, pair2.Key });
                            }
                        }
                    }
                    else
                    {
                        paths.Add(new string[1] { pair.Key });
                    }
                }
            }
            finally
            {
                ((IDisposable)enumerator).Dispose();
            }

            // iterate over the found paths and determine whether they are part of the default configuration
            foreach (string[] path in paths)
            {
                int index = -1;

                foreach (ConfigValue value in DefaultConfig.values.Values)
                {
                    if (path.Length != value.path.Length) continue;

                    for (int j = 0; j < path.Length; j++)
                    {
                        if (path[j].Equals(value.path[j]))
                        {
                            index = j > index ? j : index;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                // if a path is not part of the default configuration remove it
                if (index < path.Length - 1)
                {
                    if (index == -1)
                    {
                        Config.Remove(path[index + 1]);
                    }
                    else
                    {
                        string[] strArray = new string[index + 1];
                        for (int j = 0; j < strArray.Length; j++)
                        {
                            strArray[j] = path[j];
                        }
                        ((Dictionary<string, object>)Config.Get(strArray)).Remove(path[index + 1]);
                    }
                }
            }
        }

        /// <summary>
        /// Responsible for creating a configuration file and populating it with the required default values.
        /// </summary>
        /// <remarks>
        /// This function is called automatically by oxide if no configuration file could be found.
        /// </remarks>
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating configuration file!");

            Config.Clear();

            foreach (ConfigValue value in DefaultConfig.values.Values)
            {
                object[] objArray = new object[value.path.Length + 1];
                for (int i = 0; i < value.path.Length; i++)
                {
                    objArray[i] = value.path[i];
                }
                objArray[value.path.Length] = value.value;

                Config.Set(objArray);
            }
            SaveConfig();
        }

        /// <summary>
        /// Responsible for loading the configuration file.
        /// </summary>
        new void LoadConfig()
        {
            Puts("Loading configuration file!");

            bool saveConfig = false;

            // 1. General
            chatIcon = GetConfig<string>(ref saveConfig, DefaultConfig.values["chatIcon"]);
            font = GetConfig<string>(ref saveConfig, DefaultConfig.values["font"]);
            outline = GetConfig<bool>(ref saveConfig, DefaultConfig.values["outline"]);
            fontSize = GetConfig<int>(ref saveConfig, DefaultConfig.values["fontSize"]);
            numberOfCharacters = GetConfig<int>(ref saveConfig, DefaultConfig.values["numberOfCharacters"]);
            removeSpecialCharacters = GetConfig<bool>(ref saveConfig, DefaultConfig.values["removeSpecialCharacters"]);
            removeTags = GetConfig<bool>(ref saveConfig, DefaultConfig.values["removeTags"]);
            enableAnimals = GetConfig<bool>(ref saveConfig, DefaultConfig.values["enableAnimals"]);
            displayPlayerDeaths = GetConfig<bool>(ref saveConfig, DefaultConfig.values["displayPlayerDeaths"]);
            logEntries = GetConfig<bool>(ref saveConfig, DefaultConfig.values["logEntries"]);
            printEntriesToConsole = GetConfig<bool>(ref saveConfig, DefaultConfig.values["printEntriesToConsole"]);
            _debugging = GetConfig<int>(ref saveConfig, DefaultConfig.values["debugging"]);

            // 2. Kill Feed
            formatting = GetConfig<string>(ref saveConfig, DefaultConfig.values["formatting"]);

            int numberOfEntries = GetConfig<int>(ref saveConfig, DefaultConfig.values["numberOfEntries"]);
            entries = new CuiElementContainer[numberOfEntries];

            width = GetConfig<float>(ref saveConfig, DefaultConfig.values["width"]);
            halfWidth = width / 2f;

            iconHalfHeight = GetConfig<float>(ref saveConfig, DefaultConfig.values["iconHalfHeight"]);
            iconHalfWidth = iconHalfHeight * _screenAspectRatio / (width / _height);

            destroyAfter = GetConfig<float>(ref saveConfig, DefaultConfig.values["destroyAfter"]);

            float x = GetConfig<float>(ref saveConfig, DefaultConfig.values["x"]);
            float y = GetConfig<float>(ref saveConfig, DefaultConfig.values["y"]);
            anchormin = new Vector2(x - halfWidth, y - _halfHeight);
            anchormax = new Vector2(x + halfWidth, y + _halfHeight);

            float xSpacing = GetConfig<float>(ref saveConfig, DefaultConfig.values["horizontal"]);
            if (xSpacing != 0.0f)
            {
                bool positive = xSpacing > 0.0f;
                horizontalSpacing = positive ? xSpacing + halfWidth : xSpacing - halfWidth;
            }
            else
            {
                horizontalSpacing = xSpacing;
            }

            float ySpacing = GetConfig<float>(ref saveConfig, DefaultConfig.values["vertical"]);
            if (ySpacing != 0.0f)
            {
                bool positive = ySpacing > 0.0f;
                verticalSpacing = positive ? ySpacing + _halfHeight : ySpacing - _halfHeight;
            }
            else
            {
                verticalSpacing = ySpacing;
            }

            fadeIn = GetConfig<float>(ref saveConfig, DefaultConfig.values["in"]);
            fadeOut = GetConfig<float>(ref saveConfig, DefaultConfig.values["out"]);
            EntryData._initiatorColor = GetConfig<string>(ref saveConfig, DefaultConfig.values["initiator"]);
            EntryData._infoColor = GetConfig<string>(ref saveConfig, DefaultConfig.values["info"]);
            EntryData._hitEntityColor = GetConfig<string>(ref saveConfig, DefaultConfig.values["hitEntity"]);
            EntryData._npcColor = GetConfig<string>(ref saveConfig, DefaultConfig.values["npc"]);

            // 3. Data
            useServerFileStorage = GetConfig<bool>(ref saveConfig, DefaultConfig.values["useServerFileStorage"]);

            FileManager.fileDirectory = GetConfig<string>(ref saveConfig, DefaultConfig.values["fileDirectory"]);

            FileManager.fileIDs.Clear();
            FileManager.Store(GetConfig<Dictionary<string, string>>(ref saveConfig, DefaultConfig.values["Files"]));
            FileManager.Store(GetConfig<Dictionary<string, string>>(ref saveConfig, DefaultConfig.values["DamageTypeFiles"]));

            EntryData._npcNames.Clear();
            EntryData._npcNames = GetConfig<Dictionary<string, string>>(ref saveConfig, DefaultConfig.values["NPCNames"]);

            EntryData._boneNames.Clear();
            EntryData._boneNames = GetConfig<Dictionary<string, string>>(ref saveConfig, DefaultConfig.values["BoneNames"]);

            foreach (char c in GetConfig<List<char>>(ref saveConfig, DefaultConfig.values["AllowedSpecialCharacters"]))
            {
                if (c == _formattingChar) continue;
                allowedCharacters[c] = true;
            }

            if (saveConfig)
            {
                CleanupConfig();

                PrintWarning("Updating configuration file!");

                SaveConfig();
            }
        }
        #endregion

        #region Lang
        /// <summary>
        /// Provides the default values for each language file value for easy access.
        /// </summary>
        static class DefaultLangValues
        {
            public static Dictionary<string, string> GetDefaultMessages()
            {
                Dictionary<string, string> messages = new Dictionary<string, string>()
                {
                    { "killfeed", "<color=red>[KillFeed]</color> /killfeed disable<color=red>|</color>enable<color=red>|</color>status" },
                    { "killfeed enable > enabled", "<color=red>[KillFeed]</color> enabled!" },
                    { "killfeed enable > already enabled", "<color=red>[KillFeed]</color> already enabled!" },
                    { "killfeed disable > disabled", "<color=red>[KillFeed]</color> disabled!" },
                    { "killfeed disable > already disabled", "<color=red>[KillFeed]</color> already disabled!" },
                    { "killfeed status > is enabled", "<color=red>[KillFeed]</color> is enabled!" },
                    { "killfeed status > is disabled", "<color=red>[KillFeed]</color> is disabled!" },
                };
                return messages;
            }
        }

        /// <summary>
        /// Responsible for creating a language file and populating it with the required default values.
        /// </summary>
        void LoadDefaultLang()
        {
            lang.RegisterMessages(DefaultLangValues.GetDefaultMessages(), this);
        }
        #endregion

        #region Helper
        /// <summary>
        /// Set of helper functions for modifying strings.
        /// </summary>
        static class StringHelper
        {
            /// <summary>
            /// Removes a tag infront of a string.
            /// </summary>
            /// <param name="str"> The string that contains the tag.</param>
            /// <returns> The string without the tag.</returns>
            public static string RemoveTag(string str)
            {
                if (str.StartsWith("[") && str.Contains("]") && str.Length > str.IndexOf("]"))
                {
                    str = str.Substring(str.IndexOf("]") + 1).Trim();
                }
                return str;
            }

            /// <summary>
            /// Removes special characters from a string.
            /// </summary>
            /// <param name="str"> The string that should be modified.</param>
            /// <param name="allowedCharacters"> The characters that are allowed.</param>
            /// /// <param name="flag"> The flag indicates whether the characters provided are allowed characters (false) or banned characters (true).</param>
            /// <returns> The string with all non allowed characters removed.</returns>
            public static string RemoveSpecialCharacters(string str, bool[] characters, bool flag = false)
            {
                char[] buffer = new char[str.Length];
                int index = 0;
                foreach (char c in str)
                {
                    if (characters[c] ^ flag)
                    {
                        buffer[index] = c;
                        index++;
                    }
                }
                return new string(buffer, 0, index);
            }

            /// <summary>
            /// Trims a string to a defined size if the string is longer.
            /// </summary>
            /// <param name="str"> the string that should be trimmed.</param>
            /// <param name="size"> the maximum size of the string.</param>
            /// <returns> The string trimmed to the defined size.</returns>
            public static string TrimToSize(string str, int size)
            {
                if (str.Length > size)
                {
                    str = str.Substring(0, size);
                }
                return str;
            }
        }

        /// <summary>
        /// Wrapper for logging debug messages.
        /// </summary>
        /// <param name="args"> The debug messages which should be logged.</param>
        static void LogDebug(params string[] args)
        {
            if (_debugging != 1 && _debugging != 2) return;

            StringBuilder builder = new StringBuilder("{1}");
            for (int i = 0; i < args.Length; i++)
            {
                builder.Append(" ");
                builder.Append("{" + i + "}");
            }

            if (_debugging == 1)
            {
                Interface.Oxide.LogDebug(builder.ToString(), args);
            }
            else if (_debugging == 2)
            {
                debugLog.Add(string.Format(builder.ToString(), args));
            }
        }
        #endregion

        #region FileManager
        /// <summary>
        /// Responsible for storing the png files in the server's file storage and keeping track of them.
        /// </summary>
        class FileManager : MonoBehaviour
        {
            public static string fileDirectory;

            public static Dictionary<string, string> fileIDs = new Dictionary<string, string>();

            /// <summary>
            /// Stores a value inside the server's file storage.
            /// </summary>
            /// <param name="key"> The key that is used to keep track of the value.</param>
            /// <param name="value"> The value containing the url to the png file.</param>
            public static void Store(string key, string value)
            {
                StringBuilder url = new StringBuilder();
                if (value.StartsWith("file:///") || value.StartsWith("http://"))
                {
                    url.Append(value);
                }
                else
                {
                    url.Append(fileDirectory);
                    url.Append(value);
                }

                if (useServerFileStorage)
                {
                    _fileManager.StartCoroutine(WaitForRequest(key, url.ToString()));
                }
                else
                {
                    fileIDs[key] = url.ToString();
                }
            }

            /// <summary>
            /// Stores a list of value inside the server's file storage.
            /// </summary>
            /// <param name="files"> The list of values that should be stored. The list of keys is used to keep track of the values.</param>
            public static void Store(Dictionary<string, string> files)
            {
                foreach (KeyValuePair<string, string> pair in files)
                {
                    Store(pair.Key, pair.Value);
                }
            }

            /// <summary>
            /// Coroutine that waits for the url data to be loaded and stores it once it is loaded.
            /// </summary>
            /// <param name="shortname"> The shortname of a given item.</param>
            /// <param name="url"> The url that points to the data that should be stored.</param>
            /// <returns></returns>
            private static IEnumerator WaitForRequest(string shortname, string url)
            {
                WWW www = new WWW(url);

                yield return www;

                if (string.IsNullOrEmpty(www.error))
                {
                    fileIDs[shortname] = FileStorage.server.Store(www.bytes, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
                }
                else
                {
                    LogDebug("[Kill Feed]", "[" + DateTime.Now.ToString("mm'/'HH'/'dd'/'MM'/'yyyy") + "]", "[WaitForRequest(...)]", shortname, url, www.error);
                }
            }
        }
        #endregion

        #region Timer
        /// <summary>
        /// Provides a basic timer for executing an action after a specified delay.
        /// </summary>
        class Timer : MonoBehaviour
        {
            bool isRunning;

            Coroutine instance;

            /// <summary>
            /// Functions as a wrapper for Unity coroutines.
            /// </summary>
            /// <param name = "action"> The action that should be executed.</param>
            /// <param name="delay"> The delay after which the action should be executed.</param>
            public void DelayedAction(Action action, float delay)
            {
                if (isRunning)
                {
                    StopCoroutine(instance);
                    isRunning = false;

                    instance = StartCoroutine(WaitForDelay(action, delay));
                }
                else
                {
                    instance = StartCoroutine(WaitForDelay(action, delay));
                }
            }

            /// <summary>
            /// Waits for a specified amount of time and exectues the specified action.
            /// </summary>
            /// <param name = "action"> The action that should be executed.</param>
            /// <param name="delay"> The delay after which the action should be executed.</param>
            /// <returns></returns>
            private IEnumerator WaitForDelay(Action action, float delay)
            {
                isRunning = true;

                yield return new WaitForSeconds(delay);

                action();
                isRunning = false;
            }
        }
        #endregion

        #region Key
        /// <summary>
        /// Wrapper for a keyboard or mouse key.
        /// </summary>
        class Key
        {
            public string key;
            public string action;
            public string defaultAction;
        }
        #endregion

        #region KillFeedPlayer
        /// <summary>
        /// Handles player related features on a per player basis, e.g. enabling/disabling the ui.
        /// </summary>
        class KillFeedPlayer
        {
            public bool enabled { get; set; } = true;

            public string username { get; private set; }
            public string lastAction { get; set; }

            public Connection connection { get; private set; }

            public KillFeedPlayer(Connection connection, string username)
            {
                this.username = username;
                this.connection = connection;
                this.lastAction = "";
            }
        }

        /// <summary>
        /// Formats a string/username.
        /// </summary>
        /// <param name="username"> The username that should be formatted.</param>
        /// <returns> The formatted username</returns>
        string FormatUsername(string username)
        {
            if (removeTags)
            {
                username = StringHelper.RemoveTag(username);
            }
            if (removeSpecialCharacters)
            {
                username = StringHelper.RemoveSpecialCharacters(username, allowedCharacters);
                username = StringHelper.TrimToSize(username, numberOfCharacters);
            }
            else
            {
                bool[] characters = new bool[65536];
                characters[_formattingChar] = true;
                username = StringHelper.RemoveSpecialCharacters(username, characters, true);
                username = StringHelper.TrimToSize(username, numberOfCharacters);
            }
            return username;
        }

        /// <summary>
        /// Adds a player to the _players dictionary.
        /// </summary>
        /// <param name="player"> The player that should be added.</param>
        void AddPlayer(BasePlayer player)
        {
            if (_players == null) return;

            string username = FormatUsername(player.displayName);

            _players[player.userID] = new KillFeedPlayer(player.net.connection, username);
        }

        /// <summary>
        /// Removes a player from the _players dictionary.
        /// </summary>
        /// <param name="player"> The player that should be removed.</param>
        void RemovePlayer(BasePlayer player)
        {
            if (_players == null) return;

            _players.Remove(player.userID);
        }
        #endregion

        #region Entry Setup
        class EntryData
        {
            #region Fields
            public static string _initiatorColor { private get; set; }
            public static string _infoColor { private get; set; }
            public static string _hitEntityColor { private get; set; }
            public static string _npcColor { private get; set; }

            public static Dictionary<string, string> _npcNames { get; set; } = new Dictionary<string, string>();
            public static Dictionary<string, string> _boneNames { get; set; } = new Dictionary<string, string>();

            public bool selfInflicted { get; private set; }
            public bool needsFormatting { get; private set; }

            public EntityInfo initiatorInfo { get; private set; }
            public EntityInfo hitEntityInfo { get; private set; }

            public string initiatorColor { get; private set; }
            public string hitEntityColor { get; private set; }

            public WeaponInfo weaponInfo { get; private set; }

            public string hitBone { get; private set; }
            public string distance { get; private set; }

            public string infoColor
            {
                get
                {
                    return _infoColor;
                }
            }
            #endregion

            /// <summary>
            /// Constructer that is used to generate the data that is needed for a new Kill Feed entry.
            /// </summary>
            /// <param name="entity"> The entity that either died or got wounded.</param>
            /// <param name="info"> Contains information about the killing or wounding hit.</param>
            public EntryData(BaseCombatEntity entity, HitInfo info)
            {
                initiatorInfo = GetInitiator(info);
                hitEntityInfo = GetHitEntity(entity);

                weaponInfo = GetWeapon(info);

                hitBone = GetHitBone(info);

                distance = GetDistance(entity, info);

                // determines the colors of the initiator and the hitEntity
                if (initiatorInfo.userID != 0)                                                                  // initiator is a player
                {
                    initiatorColor = _initiatorColor;
                }
                else                                                                                            // initiator is a npc
                {
                    initiatorColor = _npcColor;
                }

                if (hitEntityInfo.userID != 0)                                                                  // hitEntity is a player
                {
                    hitEntityColor = _hitEntityColor;
                }
                else                                                                                            // hitEntity is a npc
                {
                    hitEntityColor = _npcColor;
                }

                if (initiatorInfo.userID == hitEntityInfo.userID)
                {
                    selfInflicted = true;
                }

                if (selfInflicted)                                                                              // initiator and hitEntity are the same entity
                {
                    initiatorInfo.name = hitEntityInfo.name;
                    initiatorColor = hitEntityColor;
                }
            }

            /// <summary>
            /// Determines the name and the userID of the initiator.
            /// </summary>
            /// <param name="info"> Contains information about the initiator.</param>
            /// <returns> A instance that contains both the name and the userID of the initiator.</returns>
            EntityInfo GetInitiator(HitInfo info)
            {
                string name = "";
                ulong userID = 0;

                if (info.Initiator?.ToPlayer() != null)                                                         // initiator is player
                {
                    userID = info.Initiator.ToPlayer().userID;

                    KillFeedPlayer player;
                    if (_players.TryGetValue(info.Initiator.ToPlayer().userID, out player))
                    {
                        name = player.username;
                    }
                    else
                    {
                        needsFormatting = true;
                        name = info.Initiator.ToPlayer().displayName;
                    }
                }
                else if (info.WeaponPrefab != null && info.WeaponPrefab.name.Equals("rocket_heli"))             // initiator is patrolhelicopter
                {
                    if (!_npcNames.TryGetValue("patrolhelicopter", out name))
                    {
                        name = "patrolhelicopter";
                    }
                }
                else                                                                                            // initiator is npc
                {
                    string npcName = info.Initiator?.ShortPrefabName ?? "";
                    if (!_npcNames.TryGetValue(npcName, out name))
                    {
                        name = npcName;
                    }
                }
                return new EntityInfo(name, userID);
            }

            /// <summary>
            /// Determines the name and the userID of the entity that died or got wounded.
            /// </summary>
            /// <param name="entity"> The entity that died or got wounded.</param>
            /// <returns> A instance that contains both the name and the userID of the entity.</returns>
            EntityInfo GetHitEntity(BaseCombatEntity entity)
            {
                string name = "";
                ulong userID = 0;

                if (entity.ToPlayer() != null)                                                                  // hitEntity is player
                {
                    userID = entity.ToPlayer().userID;

                    KillFeedPlayer player;
                    if (_players.TryGetValue(entity.ToPlayer().userID, out player))
                    {
                        name = player.username;
                    }
                    else if (entity.ToPlayer().HasPlayerFlag(BasePlayer.PlayerFlags.Sleeping))                  // hitEntity (player) is sleeping and his name needs to be formatted
                    {
                        needsFormatting = true;
                        name = entity.ToPlayer().displayName;
                    }
                    else if (entity.ToPlayer().userID > 0L && entity.ToPlayer().userID < 76560000000000000L)    // hitEntity (player) is actually a npc and his name needs to be formatted
                    {
                        needsFormatting = true;
                        name = entity.ToPlayer().displayName;
                    }
                }
                else                                                                                            // hitEntity is npc
                {
                    string npcName = entity.ShortPrefabName ?? "";
                    if (npcName.Equals("patrolhelicopter"))
                    {
                        selfInflicted = true;
                    }
                    if (!_npcNames.TryGetValue(npcName, out name))
                    {
                        name = npcName;
                    }
                }
                return new EntityInfo(name, userID);
            }

            /// <summary>
            /// Used to store an entity's name and userID.
            /// </summary>
            public class EntityInfo
            {
                public string name { get; set; }
                public ulong userID { get; private set; }

                public EntityInfo(string name, ulong userID)
                {
                    this.name = name;
                    this.userID = userID;
                }
            }

            /// <summary>
            /// Determines the weapon that was used to deliver the killing or wounding hit and returns the shortname and id of that weapon.
            /// </summary>
            /// <remarks>
            /// The weapon id represents the id of the stored png file of that weapon's icon.
            /// </remarks>
            /// <param name="info"> Contains information about the killing or wounding hit.</param>
            /// <returns> A instance containing both the shortname and the id of a weapon</returns>
            WeaponInfo GetWeapon(HitInfo info)
            {
                string weaponID;

                string weapon = info.Weapon?.GetItem()?.info?.shortname;

                // special case handling if the traditional way of getting a weapons shortname doesn't return results.
                if (string.IsNullOrEmpty(weapon))
                {
                    if (info.WeaponPrefab != null)
                    {
                        if (info.WeaponPrefab.ShortPrefabName.Equals("axe_salvaged.entity")) weapon = "axe.salvaged";
                        else if (info.WeaponPrefab.ShortPrefabName.Equals("bone_club.entity")) weapon = "bone.club";
                        else if (info.WeaponPrefab.ShortPrefabName.Equals("explosive.satchel.deployed")) weapon = "explosive.satchel";
                        else if (info.WeaponPrefab.ShortPrefabName.Equals("explosive.timed.deployed")) weapon = "explosive.timed";
                        else if (info.WeaponPrefab.ShortPrefabName.Equals("flamethrower.entity")) weapon = "flamethrower";
                        else if (info.WeaponPrefab.ShortPrefabName.Equals("flameturret.deployed"))
                        {
                            weapon = "flameturret";
                            selfInflicted = true;
                        }
                        else if (info.WeaponPrefab.ShortPrefabName.Equals("grenade.beancan.deployed")) weapon = "grenade.beancan";
                        else if (info.WeaponPrefab.ShortPrefabName.Equals("grenade.f1.deployed")) weapon = "grenade.f1";
                        else if (info.WeaponPrefab.ShortPrefabName.Equals("hammer_salvaged.entity")) weapon = "hammer.salvaged";
                        else if (info.WeaponPrefab.ShortPrefabName.Equals("hatchet.entity")) weapon = "hatchet";
                        else if (info.WeaponPrefab.ShortPrefabName.Equals("hatchet_pickaxe.entity")) weapon = "stone.pickaxe";
                        else if (info.WeaponPrefab.ShortPrefabName.Equals("icepick_salvaged.entity")) weapon = "icepick.salvaged";
                        else if (info.WeaponPrefab.ShortPrefabName.Equals("knife_bone.entity")) weapon = "knife.bone";
                        else if (info.WeaponPrefab.ShortPrefabName.Equals("landmine"))
                        {
                            weapon = "landmine";
                            selfInflicted = true;
                        }
                        else if (info.WeaponPrefab.ShortPrefabName.Equals("longsword.entity")) weapon = "longsword";
                        else if (info.WeaponPrefab.ShortPrefabName.Equals("mace.entity")) weapon = "mace";
                        else if (info.WeaponPrefab.ShortPrefabName.Equals("machete.weapon")) weapon = "machete";
                        else if (info.WeaponPrefab.ShortPrefabName.Equals("pickaxe.entity")) weapon = "pickaxe";
                        else if (info.WeaponPrefab.ShortPrefabName.Equals("rock.entity")) weapon = "rock";
                        else if (info.WeaponPrefab.ShortPrefabName.Contains("rocket")) weapon = "rocket.launcher";
                        else if (info.WeaponPrefab.ShortPrefabName.Equals("salvaged_cleaver.entity")) weapon = "salvaged.cleaver";
                        else if (info.WeaponPrefab.ShortPrefabName.Equals("salvaged_sword.entity")) weapon = "salvaged.sword";
                        else if (info.WeaponPrefab.ShortPrefabName.Equals("spear_stone.entity")) weapon = "spear.stone";
                        else if (info.WeaponPrefab.ShortPrefabName.Equals("spear_wooden.entity")) weapon = "spear.wooden";
                        else if (info.WeaponPrefab.ShortPrefabName.Equals("stonehatchet.entity")) weapon = "stonehatchet";
                        else if (info.WeaponPrefab.ShortPrefabName.Equals("survey_charge.deployed")) weapon = "surveycharge";
                    }
                    else if (info.Initiator != null)
                    {
                        if (info.Initiator.ShortPrefabName.Equals("autoturret_deployed"))
                        {
                            weapon = "autoturret";
                            selfInflicted = true;
                        }
                        else if (info.Initiator.ShortPrefabName.Equals("beartrap"))
                        {
                            weapon = "trap.bear";
                            selfInflicted = true;
                        }
                        else if (info.Initiator.ShortPrefabName.Equals("barricade.metal"))
                        {
                            weapon = "barricade.metal";
                            selfInflicted = true;
                        }
                        else if (info.Initiator.ShortPrefabName.Equals("barricade.wood"))
                        {
                            weapon = "barricade.wood";
                            selfInflicted = true;
                        }
                        else if (info.Initiator.ShortPrefabName.Equals("barricade.woodwire"))
                        {
                            weapon = "barricade.woodwire";
                            selfInflicted = true;
                        }
                        else if (info.Initiator.ShortPrefabName.Equals("gates.external.high.stone"))
                        {
                            weapon = "gates.external.high.stone";
                            selfInflicted = true;
                        }
                        else if (info.Initiator.ShortPrefabName.Equals("gates.external.high.wood"))
                        {
                            weapon = "gates.external.high.wood";
                            selfInflicted = true;
                        }
                        else if (info.Initiator.ShortPrefabName.Equals("guntrap.deployed"))
                        {
                            weapon = "guntrap";
                            selfInflicted = true;
                        }
                        else if (info.Initiator.ShortPrefabName.Equals("lock.code")) weapon = "lock.code";
                        else if (info.Initiator.ShortPrefabName.Equals("spikes.floor"))
                        {
                            weapon = "spikes.floor";
                            selfInflicted = true;
                        }
                        else if (info.Initiator.ShortPrefabName.Equals("wall.external.high.stone"))
                        {
                            weapon = "wall.external.high.stone";
                            selfInflicted = true;
                        }
                        else if (info.Initiator.ShortPrefabName.Equals("wall.external.high.wood"))
                        {
                            weapon = "wall.external.high";
                            selfInflicted = true;
                        }
                    }
                }

                // mainly used for determining whether the killing or wounding hit was self inflicted or not.
                if (!selfInflicted && (string.IsNullOrEmpty(weapon) || !weapon.Equals("flamethrower")))
                {
                    switch (info.damageTypes.GetMajorityDamageType())
                    {
                        case DamageType.Bite:
                            if (string.IsNullOrEmpty(weapon)) weapon = "bite";
                            break;

                        case DamageType.Bleeding:
                            if (string.IsNullOrEmpty(weapon)) weapon = "bleeding";
                            selfInflicted = true;
                            break;

                        case DamageType.Blunt:
                            if (string.IsNullOrEmpty(weapon)) weapon = "blunt";
                            break;

                        case DamageType.Bullet:
                            if (string.IsNullOrEmpty(weapon)) weapon = "bullet";
                            break;

                        case DamageType.Cold:
                        case DamageType.ColdExposure:
                            if (string.IsNullOrEmpty(weapon)) weapon = "cold";
                            selfInflicted = true;
                            break;

                        case DamageType.Drowned:
                            if (string.IsNullOrEmpty(weapon)) weapon = "drowned";
                            selfInflicted = true;
                            break;

                        case DamageType.ElectricShock:
                            if (string.IsNullOrEmpty(weapon)) weapon = "electricShock";
                            selfInflicted = true;
                            break;

                        case DamageType.Explosion:
                            if (string.IsNullOrEmpty(weapon)) weapon = "explosion";
                            break;

                        case DamageType.Fall:
                            if (string.IsNullOrEmpty(weapon)) weapon = "fall";
                            selfInflicted = true;
                            break;

                        case DamageType.Generic:
                            if (string.IsNullOrEmpty(weapon)) weapon = "generic";
                            break;

                        case DamageType.Heat:
                            if (string.IsNullOrEmpty(weapon)) weapon = "heat";
                            selfInflicted = true;
                            break;

                        case DamageType.Hunger:
                            if (string.IsNullOrEmpty(weapon)) weapon = "hunger";
                            selfInflicted = true;
                            break;

                        case DamageType.Poison:
                            if (string.IsNullOrEmpty(weapon)) weapon = "poison";
                            selfInflicted = true;
                            break;

                        case DamageType.Radiation:
                        case DamageType.RadiationExposure:
                            if (string.IsNullOrEmpty(weapon)) weapon = "radiation";
                            selfInflicted = true;
                            break;

                        case DamageType.Slash:
                            if (string.IsNullOrEmpty(weapon)) weapon = "slash";
                            break;

                        case DamageType.Stab:
                            if (string.IsNullOrEmpty(weapon)) weapon = "stab";
                            break;

                        case DamageType.Suicide:
                            if (string.IsNullOrEmpty(weapon)) weapon = "suicide";
                            selfInflicted = true;
                            break;

                        case DamageType.Thirst:
                            if (string.IsNullOrEmpty(weapon)) weapon = "thirst";
                            selfInflicted = true;
                            break;
                    }
                }

                if (!string.IsNullOrEmpty(weapon) && FileManager.fileIDs.TryGetValue(weapon, out weaponID))
                {
                    return new WeaponInfo(weapon, weaponID);
                }
                return null;
            }

            /// <summary>
            /// Used to store a weapon's shortname and id.
            /// </summary>
            public class WeaponInfo
            {
                public string shortname { get; private set; }
                public string weaponID { get; private set; }

                public WeaponInfo(string shortname, string weaponID)
                {
                    this.shortname = shortname;
                    this.weaponID = weaponID;
                }
            }

            /// <summary>
            /// Gets the distance between the entity that got killed or wounded and the initiator right when the entity was killed or wounded.
            /// </summary>
            /// <param name="entity"> The entity that was killed or wounded.</param>
            /// <param name="info"> Contains information about the initiator.</param>
            /// <returns> The distance.</returns>
            string GetDistance(BaseCombatEntity entity, HitInfo info)
            {
                float distance = 0.0f;

                if (entity != null && info.Initiator != null)
                {
                    distance = Vector3.Distance(info.Initiator.transform.position, entity.transform.position);
                }
                return distance.ToString("0.0").Equals("0.0") ? "" : distance.ToString("0.0") + "m";
            }

            /// <summary>
            /// Gets the name of the bone that was hit.
            /// </summary>
            /// <param name="info"> Contains information about the bone that was hit.</param>
            /// <returns> The name of the bone.</returns>
            string GetHitBone(HitInfo info)
            {
                if (info.HitEntity == null || info.HitEntity.ToPlayer() == null) return "";

                string hitBone;

                BaseCombatEntity hitEntity = info.HitEntity as BaseCombatEntity;

                SkeletonProperties.BoneProperty boneProperty = hitEntity.skeletonProperties?.FindBone(info.HitBone);

                string bone = boneProperty?.name?.english ?? "";

                if (!_boneNames.TryGetValue(bone, out hitBone))
                {
                    hitBone = bone;
                }
                return hitBone;
            }
        }

        /// <summary>
        /// Creates a new Kill Feed entry formatted with the information provided through the parameters.
        /// </summary>
        /// <param name="entryData"> The data that is need for a new entry.</param>
        /// <returns> A list containing all UI elements.</returns>
        CuiElementContainer GetKillFeedEntry(EntryData entryData)
        {
            StringBuilder builder = new StringBuilder(formatting);
            builder.Replace("{initiator}", "<color=" + entryData.initiatorColor + ">" + entryData.initiatorInfo.name + "</color>");
            builder.Replace("{hitBone}", "<color=" + entryData.infoColor + ">" + entryData.hitBone + "</color>");
            builder.Replace("{distance}", "<color=" + entryData.infoColor + ">" + entryData.distance + "</color>");
            builder.Replace("{hitEntity}", "<color=" + entryData.hitEntityColor + ">" + (entryData.needsFormatting ? FormatUsername(entryData.hitEntityInfo.name) : entryData.hitEntityInfo.name) + "</color>");

            string[] splitStrings = builder.ToString().Split(new char[] { _formattingChar }, 4, StringSplitOptions.None);
            string[] stringElements = { "", "", "", "" };
            Array.Copy(splitStrings, stringElements, splitStrings.Length);

            CuiElementContainer container = new CuiElementContainer();

            #region feedEntryElement
            CuiElement feedEntryElement = new CuiElement
            {
                Name = "{0} feedEntry",
                Parent = "Hud",
                FadeOut = fadeOut,
                Components =
                    {
                        new CuiRawImageComponent
                        {
                            Sprite = "assets/content/textures/generic/fulltransparent.tga"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = anchormin.x + " " + anchormin.y,
                            AnchorMax = anchormax.x + " " + anchormax.y
                        }
                    }
            };
            #endregion

            #region outerLeftHandElement
            CuiElement outerLeftHandElement = new CuiElement
            {
                Name = "{0} outerLeftHandString",
                Parent = "{0} feedEntry",
                FadeOut = fadeOut,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = stringElements[0],
                        Font = font,
                        FontSize = fontSize,
                        Align = TextAnchor.MiddleLeft,
                        FadeIn = fadeIn,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.0 0.0",
                        AnchorMax = 0.5f - iconHalfWidth + " 1.0"
                    }
                }
            };
            #endregion

            #region innerLeftHandElement
            CuiElement innerLeftHandElement = new CuiElement
            {
                Name = "{0} innerLeftHandString",
                Parent = "{0} feedEntry",
                FadeOut = fadeOut,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = stringElements[1],
                        Font = font,
                        FontSize = fontSize,
                        Align = TextAnchor.MiddleRight,
                        FadeIn = fadeIn,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.0 0.0",
                        AnchorMax = 0.5f - iconHalfWidth + " 1.0"
                    }
                }
            };
            #endregion

            #region weaponElement
            CuiElement weaponElement = new CuiElement
            {
                Name = "{0} weapon",
                Parent = "{0} feedEntry",
                FadeOut = fadeOut
            };

            CuiRawImageComponent weaponRawImageComponent = new CuiRawImageComponent();
            weaponRawImageComponent.Sprite = "assets/content/textures/generic/fulltransparent.tga";
            if (useServerFileStorage) weaponRawImageComponent.Png = entryData.weaponInfo.weaponID;
            else weaponRawImageComponent.Url = entryData.weaponInfo.weaponID;
            weaponRawImageComponent.FadeIn = fadeIn;

            CuiRectTransformComponent weaponRectTransformComponent = new CuiRectTransformComponent();
            weaponRectTransformComponent.AnchorMin = 0.5f - iconHalfWidth + " " + (0.5f - iconHalfHeight);
            weaponRectTransformComponent.AnchorMax = 0.5f + iconHalfWidth + " " + (0.5f + iconHalfHeight);

            weaponElement.Components.Add(weaponRawImageComponent);
            weaponElement.Components.Add(weaponRectTransformComponent);
            #endregion

            #region outerRightHandElement
            CuiElement outerRightHandElement = new CuiElement
            {
                Name = "{0} outerRightHandString",
                Parent = "{0} feedEntry",
                FadeOut = fadeOut,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = stringElements[3],
                        Font = font,
                        FontSize = fontSize,
                        Align = TextAnchor.MiddleRight,
                        FadeIn = fadeIn,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = 0.5f + iconHalfWidth + " 0.0",
                        AnchorMax = "1.0 1.0",
                    }
                }
            };
            #endregion

            #region innerRightHandElement
            CuiElement innerRightHandElement = new CuiElement
            {
                Name = "{0} innerRightHandString",
                Parent = "{0} feedEntry",
                FadeOut = fadeOut,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = stringElements[2],
                        Font = font,
                        FontSize = fontSize,
                        Align = TextAnchor.MiddleLeft,
                        FadeIn = fadeIn,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = 0.5f + iconHalfWidth + " 0.0",
                        AnchorMax = "1.0 1.0",
                    }
                }
            };
            #endregion

            #region outline
            if (outline)
            {
                CuiOutlineComponent outlineComponent = new CuiOutlineComponent
                {
                    Distance = "1.0 1.0",
                    Color = "0.0 0.0 0.0 1.0"
                };
                outerLeftHandElement.Components.Add(outlineComponent);
                innerLeftHandElement.Components.Add(outlineComponent);
                outerRightHandElement.Components.Add(outlineComponent);
                innerRightHandElement.Components.Add(outlineComponent);
            }
            #endregion

            container.Add(feedEntryElement);
            container.Add(outerLeftHandElement);
            container.Add(innerLeftHandElement);
            container.Add(weaponElement);
            container.Add(outerRightHandElement);
            container.Add(innerRightHandElement);

            return container;
        }

        /// <summary>
        /// Handles moving of existing entries and the addition of new entries.
        /// </summary>
        /// <param name="entryData"> The data that is need for a new entry.</param>
        void OnWoundedOrDeath(EntryData entryData)
        {
            // move existing entries
            for (int i = entries.Length - 1; i > 0; i--)
            {
                if (entries[i - 1] == null) continue;

                CuiElementContainer container = entries[i - 1];

                foreach (CuiElement element in container)
                {
                    element.Name = element.Name.Replace("{" + (i - 1) + "}", "{" + i + "}");

                    if (element.Parent.Equals("Hud"))
                    {
                        CuiRectTransformComponent transform = (CuiRectTransformComponent)element.Components.Find(x => x.Type.Equals("RectTransform"));
                        transform.AnchorMin = (anchormin.x + horizontalSpacing * i) + " " + (anchormin.y + verticalSpacing * i);
                        transform.AnchorMax = (anchormax.x + horizontalSpacing * i) + " " + (anchormax.y + verticalSpacing * i);
                    }
                    else
                    {
                        element.Parent = element.Parent.Replace("{" + (i - 1) + "}", "{" + i + "}");
                    }
                }
                entries[i] = container;
            }

            // setup new entry
            entries[0] = GetKillFeedEntry(entryData);

            AddUI(ConvertToSingleContainer(entries));

            // handel delayed destroying of entries
            if (destroyAfter > 0.0f)
            {
                _timer.DelayedAction(() =>
                {
                    DestroyUI(ConvertToSingleContainer(entries));

                    for (int i = 0; i < entries.Length; i++)
                    {
                        entries[i] = null;
                    }
                }, destroyAfter);
            }

            if (logEntries || printEntriesToConsole)
            {
                int count = 30;

                object[] columns = new object[6];
                columns[0] = DateTime.Now.ToString("mm'/'HH'/'dd'/'MM'/'yyyy");
                columns[1] = entryData.initiatorInfo.name.PadRight(count);
                columns[2] = entryData.hitBone.PadRight(count);
                columns[3] = entryData.weaponInfo.shortname.PadRight(count);
                columns[4] = entryData.distance.PadRight(count);
                columns[5] = entryData.needsFormatting ? FormatUsername(entryData.hitEntityInfo.name) : entryData.hitEntityInfo.name;

                string entry = string.Format("[{0}] {1} {2} {3} {4} {5}", columns);

                if (printEntriesToConsole)
                {
                    Puts(entry);
                }
                if (logEntries)
                {
                    entryLog.Add(entry);
                }
            }
        }
        #endregion

        #region UI Wrapper
        /// <summary>
        /// Adds the specified UI elements to every player on the server.
        /// </summary>
        /// <param name = "elements"> The UI elements that should be added.</param>>
        static void AddUI(CuiElementContainer elements)
        {
            foreach (KillFeedPlayer player in _players.Values)
            {
                DestroyUI(elements, player);

                AddUI(elements, player);
            }
        }

        /// <summary>
        /// Adds the specified UI elements to one specific player.
        /// </summary>
        /// <param name = "elements"> The UI elements that should be added.</param>
        /// <param name="player"> That player that the UI elements should be added to.</param>
        static void AddUI(CuiElementContainer elements, KillFeedPlayer player)
        {
            if (player.connection == null || !player.enabled) return;

            CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo(player.connection), null, "AddUI", elements.ToJson());
        }

        /// <summary>
        /// Destroys the specified UI elements for every player on the server.
        /// </summary>
        /// <param name = "elements"> The UI elements that should be destroyed.</param>
        static void DestroyUI(CuiElementContainer elements)
        {
            foreach (KillFeedPlayer player in _players.Values)
            {
                DestroyUI(elements, player);
            }
        }

        /// <summary>
        /// Destroys the specified UI elements for one specific player.
        /// </summary>
        /// <param name = "elements"> The UI elements that should be destroyed.</param>
        /// <param name="player"> The player that should have his UI elements destroyed.</param>
        static void DestroyUI(CuiElementContainer elements, KillFeedPlayer player)
        {
            if (player.connection == null || !player.enabled) return;

            foreach (CuiElement element in elements)
            {
                CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo(player.connection), null, "DestroyUI", element.Name);
            }
        }

        /// <summary>
        /// Converts an array of containers into a single container.
        /// </summary>
        /// <param name="containers"> The container array that should be converted.</param>
        /// <returns> The container containing all elements of the container array.</returns>
        static CuiElementContainer ConvertToSingleContainer(CuiElementContainer[] containers)
        {
            CuiElementContainer elements = new CuiElementContainer();
            for (int i = 0; i < containers.Length; i++)
            {
                if (containers[i] == null) break;

                CuiElementContainer container = containers[i];

                foreach (CuiElement element in container)
                {
                    if (element == null) continue;

                    elements.Add(element);
                }
            }
            return elements;
        }
        #endregion
    }
}