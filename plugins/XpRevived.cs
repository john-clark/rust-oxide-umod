using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

using Oxide.Core;
using Oxide.Game.Rust.Cui;

// TODO: Better audio.
// TODO: Custom popup UIs.
// TODO: Merge /learn and /level into a UI.
// TODO: XP from recycling.

namespace Oxide.Plugins
{
    [Info("XpRevived", "Mattparks", "0.2.7", ResourceId = 2753)]
    [Description("A plugin that brings back an XP system.")]
    class XpRevived : RustPlugin
    {
        #region Managers

        [PluginReference] RustPlugin ImageLibrary;
        static XpRevived _plugin;

        public class Images
        {
            public static string TryForImage(string shortname, ulong skin = 99, bool localimage = true)
            {
                if (localimage)
                {
                    if (skin == 99)
                    {
                        return GetImage(shortname, (ulong)_plugin.ResourceId);
                    }
                    else
                    {
                        return GetImage(shortname, skin);
                    }
                }
                else if (skin == 99)
                {
                    return GetImageURL(shortname, (ulong)_plugin.ResourceId);
                }
                else
                {
                    return GetImageURL(shortname, skin);
                }
            }

            public static string GetImageURL(string shortname, ulong skin = 0) => (string)_plugin.ImageLibrary?.Call("GetImageURL", shortname, skin);
            public static uint GetTextureID(string shortname, ulong skin = 0) => (uint)_plugin.ImageLibrary?.Call("GetTextureID", shortname, skin);
            public static string GetImage(string shortname, ulong skin = 0) => (string)_plugin.ImageLibrary?.Call("GetImage", shortname, skin);
            public static bool AddImage(string url, string shortname, ulong skin = 0) => (bool)_plugin.ImageLibrary?.Call("AddImage", url, shortname, skin);
            public static bool HasImage(string shortname, ulong skin = 0) => (bool)_plugin.ImageLibrary?.Call("HasImage", shortname, skin);
            public static void TryAddImage(string url, string shortname, ulong skin = 0)
            {
                if (!HasImage(shortname, skin))
                {
                    AddImage(url, shortname, skin);
                }
            }

            public static List<ulong> GetImageList(string shortname) => (List<ulong>)_plugin.ImageLibrary?.Call("GetImageList", shortname);
        }

        public class UI
        {
            public static CuiElementContainer CreateElementContainer(string panelName, string color, string aMin, string aMax, bool useCursor = false, string parent = "Overlay")
            {
                var NewElement = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
                        panelName
                    }
                };
                return NewElement;
            }

            public static void LoadImage(ref CuiElementContainer container, string panel, string url, string aMin, string aMax)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    FadeOut = 0.15f,
                    Components =
                    {
                        new CuiRawImageComponent { Url = url, FadeIn = 0.3f },
                        new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax }
                    }
                });
            }

            public static void CreateInput(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, bool password, int charLimit, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiInputFieldComponent { Text = text, FontSize = size, Align = align, Color = color, Command = command, IsPassword = password, CharsLimit = charLimit},
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax }
                    }
                });
            }

            public static void CreatePanel(ref CuiElementContainer container, string panel, string color, string aMin, string aMax, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    CursorEnabled = cursor
                },
                panel);
            }

            public static void CreateText(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);
            }

            public static void CreateButton(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 1.0f },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel);
            }

            public static string Color(string hexColor, float alpha)
            {
                if (hexColor.StartsWith("#"))
                {
                    hexColor = hexColor.TrimStart('#');
                }

                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }

        public class UIManager
        {
            public List<string> activeUis = new List<string>();

            public void AddContainer(string container)
            {
                activeUis.Add(container);
            }

            public void RemoveContainer(string container)
            {
                activeUis.Remove(container);
            }

            public void DestroyUI(BasePlayer player, bool destroyNav = false)
            {
                foreach (var active in activeUis)
                {
                    CuiHelper.DestroyUi(player, active);
                }

                activeUis.Clear();
            }
        }

        #endregion

        #region Configuration

        private List<string> BlockedBps = new List<string>{
            "scrap",
            "blueprintbase",
            "bleach",
            "ducttape",
            "gears",
            "glue",
            "techparts",
            "tarp",
            "sticks",
            "metalspring",
            "sewingkit",
            "rope",
            "metalpipe",
            "riflebody",
            "smgbody",
            "semibody",
            "propanetank",
            "metalblade",
            "roadsigns",
            "sheetmetal",
            "targeting.computer",
            "cctv.camera"
        };

        public class ItemData
        {
            public string ShortName;
            public string EnglishName;
            public int UnlockLevel;
            public int CostXP;
			public bool UnlockOnLevel;
        }

        public class Options
        {
            public bool UnlimitedComponents = false; // Allows unlimited components while crafting (items defined in blockedItems list).
            public List<string> BlockedItems = null; // A list of blocked items, they will be removed from crafting requirements and loot.
            public List<ItemData> ItemDatas = new List<ItemData>(); // A list of all items that have level and xp requirements.
            public string LevelUpPrefab = "assets/prefabs/misc/xmas/presents/effects/unwrap.prefab"; // The sound that will play when leveling up.
            public string LearnPrefab = "assets/prefabs/misc/xmas/presents/effects/unwrap.prefab"; // The sound that will play the player learns.
            public float LevelPivot = 7.0f; // The point where level growth delines.
            public float LevelXpRatio = 2.666f; // The amount of leveling defined in LevelRates times this value is the XP gained from a level task.
            public float XpFromGivenTool = 0.05f; // The amount of XP shared to a tools original owner. (TODO)
			public bool LootRemoveScrap = true; // Removes scrap from loot containers.
            public bool RemoveIngredients = false; // Removes ingredients from blockedItems from item crafting blueprints.
			public bool UnlockAllOnLevel = false; // This will unlock every BP for a level when the level is unlocked, disables learn commands.
		}

        public class Display
        {
			public string LevelIcon = "https://i.imgur.com/lXpowuB.png"; // The icon displayed by the level.
			public string XpIcon = "https://i.imgur.com/RoKRyG7.png"; // The icon displayed by the xp.
			public float OffsetX = 0.0f; // HUD offset X.
			public float OffsetY = 0.0f; // HUD offset Y.
            public string TextColourLearned = "#27ae60";
            public string TextColourNotLearned = "#e74c3c";
            public string TextColourAutomatic = "#aa42f4";
            public string HudColourLevel = "#CD7C41";
            public string HudColourXp = "#95BB42";
			public bool HudEnabled = true;
        }
		
        public class LevelRates
        {
            public float RepeatDelay = 120.0f; // The timeout between repeated XP (AFK points).
            public float Repeat = 0.054f; // The amount of repeat XP.

            public float PlayerKilled = 0.072f; // Amount from killing a player.
            public float PlayerRecovered = 0.087f; // Amount recovering from wounded.
            public float PlayerHelped = 0.1f; // Amount from helping a player up. (TODO)

            public float ItemCrafted = 0.081f; // Amount from crafting.

            public float Recycling = 0.082f; // Amount from recycling. (TODO)
            public float Looting = 0.072f; // Amount from looting. (TODO)

            public float KilledHeli = 0.92f; // Amount from killing a heli.
            public float KilledAnimal = 0.082f; // Amount from killing a animal (scales with animals health).
            public float BrokeBarrel = 0.063f; // Amount from breaking a barrel.
            public float ItemPickup = 0.045f; // Amount from picking a item up (like hemp or stones, not items).

            public float HitTree = 0.017f; // Amount from hitting a tree.
            public float HitOre = 0.020f; // Amount from hitting a node.
            public float HitFlesh = 0.009f; // Amount from corpse.

            public float SupplyThrown = 0.09f; // Amount from throwing a supply signal.
			
            public float StructureUpgraded = 0.02f; // Amount from upgrading a structure.
        }

        public class NoobKill
        {
            public int MaxNoobLevel = 4; // The highest level to be considered as a noob.
            public float XpPunishment = -0.2f; // The XP removed from a noob killer (per kill).
        }

        public class ConfigData
        {
            public Options Options = new Options();
			public Display Display = new Display();
            public LevelRates LevelRates = new LevelRates();
            public NoobKill NoobKill = new NoobKill();
        }

        public class PlayerData
        {
            public float Level;
            public float Xp;
			public bool ResetBps;
        }

        public class StoredData
        {
            public Dictionary<ulong, PlayerData> PlayerData = new Dictionary<ulong, PlayerData>();
        }

        private UIManager _uiManager;
        private ConfigData _config;
        private StoredData _storedData;

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new config file!");
            _config = new ConfigData();
			_config.Options.BlockedItems = null;
			_config.Options.ItemDatas = null;
            Config.WriteObject(_config, true);
        }

        protected override void LoadDefaultMessages()
        {
            // English messages.
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["XP_ABOUT"] = "<color=#e74c3c>Xp Revived {Version}:</color> by <color=green>mattparks</color>. Xp Revived is a plugin that brings back a XP system. Use the commands as follows: \n <color=#3498db>-</color> /level # (Describes what you can learn from a level) \n <color=#3498db>-</color> /learn 'item' (Lets you learn a item)",
                ["XP_LEVEL_ITEMS"] = "These items are unlocked over level {Level}: ",
                ["XP_LEVEL_NONE"] = "No items unlocked for level {Level}",
                ["XP_LEARN_USAGE"] = "Usage: /learn itemname (You try to learn a item, if you are a high enough level with XP)",
                ["XP_LEARN_MIN_LEVEL"] = "You must be level {Level} to learn {ItemName}",
                ["XP_LEARN_UNKNOWN"] = "Could not find item by name of: {ItemName}",
                ["XP_LEARN_KNOWN"] = "You already know: {ItemName}",
                ["XP_LEARN_NEEDS_XP"] = "You must have {Cost} XP to learn {ItemName}",
                ["XP_LEARN_SUCESS"] = "You learned {ItemName}",
                ["XP_NOOB_KILL"] = "You killed a new player, you will be punished!"
            }, this, "en");
        }

        private void LoadStoredData()
        {
            _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);

            if (_storedData == null)
            {
                PrintWarning("Creating a new data file!");
                _storedData = new StoredData();
                SaveStoredData();
            }
        }

        private void SaveStoredData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, _storedData);
        }

        #endregion

        #region Hooks

        private void Init()
        {
            _plugin = this;
            _uiManager = new UIManager();
            _config = Config.ReadObject<ConfigData>();
            LoadStoredData();
			
			timer.Repeat(300.0f, 0, () =>
			{
				Puts("Saving stored data!");
				SaveStoredData();
			});
        }

        private void OnServerInitialized()
        {
			if (_config.Options.UnlimitedComponents && _config.Options.BlockedItems == null)
			{
				PrintWarning("Generating block list!");
				_config.Options.BlockedItems = new List<string>()
				{
					"scrap", "blueprintbase", "bleach", "ducttape", "gears", "glue", "techparts", "tarp", "sticks", "metalspring", "sewingkit", "rope", "metalpipe", "riflebody", "smgbody", "semibody", "propanetank", "metalblade", "roadsigns", "sheetmetal", // "targeting.computer", "cctv.camera"
				};
			}
			
			if (_config.Options.ItemDatas == null)
			{
				_config.Options.ItemDatas = new List<ItemData>();
				ResetConfigXpLevels();
			}
			
            if (_config.Options.RemoveIngredients)
            {
                foreach (var bp in ItemManager.itemList)
                {
                    bp?.Blueprint?.ingredients?.RemoveAll(x =>
                        _config.Options.BlockedItems.Contains(x.itemDef.shortname));
                }
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                InfiniteComponents(player, true, true);
                UpdatePlayerUi(player);
            }
            
			if (_config.LevelRates.Repeat != 0.0f)
			{
				timer.Repeat(_config.LevelRates.RepeatDelay, 0, () =>
				{
					foreach (var player in BasePlayer.activePlayerList)
					{
						IncreaseXp(player, _config.LevelRates.Repeat, false);
					}
				});
			}
			
            Config.WriteObject(_config, true);
        }
        
        private void OnPlayerInit(BasePlayer player)
        {
            if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.Once(2.0f, () => OnPlayerInit(player));
                return;
            }

            timer.In(1.0f, () =>
            {
                InfiniteComponents(player, true, true);
                UpdatePlayerUi(player);
            });
        }

        private void OnPlayerSpawn(BasePlayer player)
        {
            InfiniteComponents(player, true, true);
            UpdatePlayerUi(player);
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            InfiniteComponents(player, true, true);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            InfiniteComponents(player, true, false);
            _uiManager.DestroyUI(player, true);
        }

        private object OnPlayerDie(BasePlayer player, HitInfo info)
        {
			if (player is NPCPlayerApex)
			{
				return null;
			}
			
            var playerData = GetPlayerData(player.userID);

            if (info?.InitiatorPlayer != null && player != info.InitiatorPlayer)
            {
                var killerData = GetPlayerData(info.InitiatorPlayer.userID);

                if (Math.Floor(playerData.Level) <= _config.NoobKill.MaxNoobLevel &&
                    Math.Floor(killerData.Level) > _config.NoobKill.MaxNoobLevel)
                {
                    MessagePlayer(Lang("XP_NOOB_KILL", info.InitiatorPlayer), info.InitiatorPlayer);
                    IncreaseXp(info.InitiatorPlayer, _config.NoobKill.XpPunishment);
                }
                else
                {
                    IncreaseXp(info.InitiatorPlayer, _config.LevelRates.PlayerKilled);
                }
            }

            return null;
        }

        private void OnPlayerRecover(BasePlayer player)
        {
            IncreaseXp(player, _config.LevelRates.PlayerRecovered);
        }

        private void OnItemCraftCancelled(ItemCraftTask task)
        {
            foreach (Item item in task.takenItems.ToList())
            {
                if (_config.Options.BlockedItems.Contains(item.info.shortname))
                {
                    task.takenItems.Remove(item);
                    item.Remove();
                }
            }
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (_config.Options.LootRemoveScrap && entity is LootContainer)
            {
                LootContainer container = entity as LootContainer;
                AssignLoot(container);
            }
        }

        private object OnRecycleItem(Recycler recycler, Item item)
        {
            if (_config.Options.BlockedItems == null || !_config.Options.BlockedItems.Any())
            {
                return null;
            }

            if (item.info.Blueprint != null)
            {
                if (item.info.Blueprint.ingredients.Any(x => _config.Options.BlockedItems.Contains(x?.itemDef?.shortname)))
                {
                    foreach (var itemAmount in item.info.Blueprint.ingredients)
                    {
                        if (!_config.Options.BlockedItems.Contains(itemAmount.itemDef.shortname))
                        {
                            recycler.MoveItemToOutput(ItemManager.Create(itemAmount.itemDef, Mathf.CeilToInt(itemAmount.amount * recycler.recycleEfficiency))); // Give normal items.
                            continue;
                        }

                        foreach (var componentIngredient in itemAmount.itemDef.Blueprint.ingredients) // Directly convert components into sub materials.
                        {
                            Item newItem = ItemManager.Create(componentIngredient.itemDef, Mathf.CeilToInt((componentIngredient.amount * recycler.recycleEfficiency)) * Mathf.CeilToInt(itemAmount.amount * recycler.recycleEfficiency), 0uL);
                            recycler.MoveItemToOutput(newItem);
                        }
                    }

                    item.UseItem();
                    return true;
                }
            }

            return null;
        }

        private object OnItemResearch(Item item, BasePlayer player)
        {
            return false;
        }

        private void OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            IncreaseXp(task.owner, _config.LevelRates.ItemCrafted);
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            BasePlayer victum = entity as BasePlayer;
            
			if (victum != null)
            {
                InfiniteComponents(victum, true, false);
            }
			
            if (info?.InitiatorPlayer != null)
            {
                if (entity.PrefabName.Contains("npc"))
                {
                    if (entity.PrefabName.Contains("patrolhelicopter"))
                    {
                        IncreaseXp(info.InitiatorPlayer, _config.LevelRates.KilledHeli);
                    }
                }
                else if (entity.PrefabName.Contains("rust.ai") && !entity.PrefabName.Contains("corpse"))
                {
                    IncreaseXp(info.InitiatorPlayer, _config.LevelRates.KilledAnimal * (entity._maxHealth / 90.0f));
                }
                else if (entity.PrefabName.Contains("radtown") || entity.PrefabName.Contains("loot-barrel"))
                {
                    IncreaseXp(info.InitiatorPlayer, _config.LevelRates.BrokeBarrel);
                }
            }
        }

        private void OnCollectiblePickup(Item item, BasePlayer player)
        {
            IncreaseXp(player, _config.LevelRates.ItemPickup);
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            var player = entity.ToPlayer();

            if (player == null || player is NPCPlayer || dispenser == null)
            {
                return;
            }

            if (dispenser.gatherType == ResourceDispenser.GatherType.Tree)
            {
                IncreaseXp(player, _config.LevelRates.HitTree);
            }

            if (dispenser.gatherType == ResourceDispenser.GatherType.Ore)
            {
                IncreaseXp(player, _config.LevelRates.HitOre);
            }

            if (dispenser.gatherType == ResourceDispenser.GatherType.Flesh)
            {
                IncreaseXp(player, _config.LevelRates.HitFlesh);
            }
        }
		
		private object OnStructureUpgrade(BaseCombatEntity entity, BasePlayer player, BuildingGrade.Enum grade)
		{
            IncreaseXp(player, _config.LevelRates.StructureUpgraded);
			return null;
		}
		
        //    private void SupplyThrown(BasePlayer player, BaseEntity entity)
        //    {
        //        IncreaseXp(player, _config.LevelRates.SupplyThrown);
        //    }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                InfiniteComponents(player, true, false);
                _uiManager.DestroyUI(player, true);
				player.SendConsoleCommand("gametip.hidegametip");
            }

            SaveStoredData();
        }

        #endregion

        #region Chat/Console Commands

        [ChatCommand("xp")]
        private void CommandXp(BasePlayer player, string command, string[] args)
        {
            MessagePlayer(Lang("XP_ABOUT", player).Replace("{Version}", Version.ToString()), player);
        }

        [ChatCommand("level")]
        private void CommandLevel(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 1)
            {
                int level = -1;
				bool isNumeric = int.TryParse(args[0], out level);
                var message = new StringBuilder();
				
				if (!isNumeric)
				{
					return;
				}

                foreach (ItemData item in _config.Options.ItemDatas)
                {
                    if (item.UnlockLevel == level)
                    {
                        if (message.Length == 0)
                        {
                            message.Append(Lang("XP_LEVEL_ITEMS", player).Replace("{Level}", level.ToString()));
                        }

                        var learned = player.blueprints.IsUnlocked(ItemManager.CreateByPartialName(item.ShortName).info);
						var colour = learned ? _config.Display.TextColourLearned : _config.Display.TextColourNotLearned;
						
						if (item.UnlockOnLevel)
						{
							colour = _config.Display.TextColourAutomatic;
						}
						
                        message.Append($"\n<color={colour}> - {GetItemDefinition(item.ShortName).displayName.english} ({item.CostXP} XP)</color>");
                    }
                }


                if (message.Length != 0)
                {
                    MessagePlayer(message.ToString(), player);
                    return;
                }

                MessagePlayer(Lang("XP_LEVEL_NONE", player).Replace("{Level}", level.ToString()), player);
            }
        }

        [ChatCommand("unlock")]
        private void CommandUnlock(BasePlayer player, string command, string[] args)
        {
			CommandLearn(player, command, args);
		}
		
        [ChatCommand("learn")]
        private void CommandLearn(BasePlayer player, string command, string[] args)
        {
			if (_config.Options.UnlockAllOnLevel)
			{
				return;
			}
			
            PlayerData playerData = GetPlayerData(player.userID);
            string givenName = String.Join(" ", args);

            if (givenName.Trim().Length == 0 || givenName.Trim().ToLower() == "help")
            {
                MessagePlayer(Lang("XP_LEARN_USAGE", player), player);
                return;
            }
            
            ItemData itemData = null;
            ItemDefinition itemDefinition = null;

            foreach (ItemData item in _config.Options.ItemDatas)
            {
                if (!string.Equals(item.ShortName, givenName, StringComparison.CurrentCultureIgnoreCase) &&
                    !string.Equals(item.EnglishName, givenName, StringComparison.CurrentCultureIgnoreCase))
                {
                    continue;
                }

                itemData = item;
                itemDefinition = GetItemDefinition(item.ShortName);

                if (item.UnlockLevel > playerData.Level)
                {
                    MessagePlayer(Lang("XP_LEARN_MIN_LEVEL", player).Replace("{Level}", item.UnlockLevel.ToString()).Replace("{ItemName}", itemDefinition.displayName.english), player);
                    return;
                }

                continue;
            }

            if (itemData == null || itemDefinition == null)
            {
                MessagePlayer(Lang("XP_LEARN_UNKNOWN", player).Replace("{ItemName}", givenName), player);
                return;
            }

            if (player.blueprints.IsUnlocked(itemDefinition))
            {
                MessagePlayer(Lang("XP_LEARN_KNOWN", player).Replace("{ItemName}", givenName), player);
                return;
            }

            if (itemData.CostXP > playerData.Xp)
            {
                MessagePlayer(Lang("XP_LEARN_NEEDS_XP", player).Replace("{Cost}", itemData.CostXP.ToString()).Replace("{ItemName}", givenName), player);
                return;
            }

            playerData.Xp -= itemData.CostXP;
            Effect.server.Run(_config.Options.LearnPrefab, player.transform.position);
            player.blueprints.Unlock(itemDefinition);

            MessagePlayer(Lang("XP_LEARN_SUCESS", player).Replace("{ItemName}", itemDefinition.displayName.english), player);
            player.SendConsoleCommand("gametip.hidegametip");
            player.SendConsoleCommand("gametip.showgametip", $"You learned {itemDefinition.displayName.english}");

            timer.Once(2.2f, () =>
            {
                player.SendConsoleCommand("gametip.hidegametip");
            });

            UpdatePlayerUi(player, false);
        }

        /*[ConsoleCommand("resetLoot")]
        private void ConsoleResetLoot(ConsoleSystem.Arg arg)
        {
            foreach (var ent in UnityEngine.Object.FindObjectsOfType<LootContainer>())
            {
                if (!ent.IsDestroyed)
                {
                    ent.Kill();
                }
            }

            Puts("Resetting world loot!");

            rust.RunServerCommand("spawn.fill_populations");
            rust.RunServerCommand("spawn.fill_groups");
        }*/

        /*[ConsoleCommand("resetBlueprints")]
        private void ConsoleResetBlueprints(ConsoleSystem.Arg arg)
        {
            Puts("Resetting player blueprints!");

            foreach (var val in _storedData.PlayerData)
            {
				val.Value.ResetBps = true;
            }
        }*/

        #endregion

        #region XpRevived

        private PlayerData GetPlayerData(ulong playerID)
        {
            PlayerData playerdata;

            if (!_storedData.PlayerData.TryGetValue(playerID, out playerdata))
            {
                playerdata = new PlayerData();
                playerdata.Level = 1.0f;
                playerdata.Xp = 0.0f;
                _storedData.PlayerData[playerID] = playerdata;
            }

            return playerdata;
        }
		
		public float GetPlayerLevel(ulong playerID)
		{
            PlayerData playerdata = GetPlayerData(playerID);
			return playerdata.Level;
		}

		public float GetPlayerXp(ulong playerID)
		{
            PlayerData playerdata = GetPlayerData(playerID);
			return playerdata.Xp;
		}

        public void IncreaseXp(ulong playerID, float amount, bool updateAFK = true)
        {
            IncreaseXp(GetPlayerFromId(playerID, false), amount, updateAFK);
        }

        public void IncreaseXp(BasePlayer player, float amount, bool updateAFK = true)
        {
            PlayerData playerData = GetPlayerData(player.userID);

            float oldLevel = playerData.Level;
            float oldXP = playerData.Xp;

            float levelAmount = amount;

            if (playerData.Level >= _config.Options.LevelPivot)
            {
                levelAmount *= _config.Options.LevelPivot / (playerData.Level);
            }

            playerData.Level += levelAmount;
            playerData.Xp += _config.Options.LevelXpRatio * amount;

            if (playerData.Level < 0.0f)
            {
                playerData.Level = 0.0f;
            }

            if (playerData.Xp < 0.0f)
            {
                playerData.Xp = 0.0f;
            }

            if (Math.Floor(oldLevel) != Math.Floor(playerData.Level))
            {
                playerData.Xp += 2.0f;

                timer.In(2.2f, () =>
                {
                    UnlockLevel(player, (int)Math.Floor(playerData.Level));
                });
            }

            if (!_config.Options.UnlockAllOnLevel && Math.Floor(oldXP) != Math.Floor(playerData.Xp))
            {
                player.SendConsoleCommand("gametip.hidegametip");
                player.SendConsoleCommand("gametip.showgametip", "You Gained 1 XP");

				timer.Once(2.2f, () =>
				{
					player.SendConsoleCommand("gametip.hidegametip");

					if (playerData.Level <= 3.0f)
					{
						player.SendConsoleCommand("gametip.showgametip", "Remember to spend XP using /learn");
					}
				});

				if (playerData.Level < 5.0f)
				{
					timer.Once(4.8f, () =>
					{
						player.SendConsoleCommand("gametip.hidegametip");
					});
				}
            }

            if (BasePlayer.activePlayerList.Contains(player))
            {
                UpdatePlayerUi(player, false);
            }
        }

        public void UnlockLevel(BasePlayer player, int level)
        {
			PlayerData playerData = GetPlayerData(player.userID);
			
			if (playerData.ResetBps)
			{
				player.blueprints.Reset();
				playerData.ResetBps = false;
			}
			
            Effect.server.Run(_config.Options.LevelUpPrefab, player.transform.position);

            player.SendConsoleCommand("gametip.hidegametip");
            player.SendConsoleCommand("gametip.showgametip", "Welcome to Level " + level);
            float timerOffset = 2.2f;

            foreach (ItemData item in _config.Options.ItemDatas)
            {
                if (item.UnlockLevel <= level)
                {
                    ItemDefinition itemDefinition = GetItemDefinition(item.ShortName);

					if ((_config.Options.UnlockAllOnLevel || item.UnlockOnLevel) && !player.blueprints.IsUnlocked(itemDefinition))
					{
						timer.Once(timerOffset, () =>
						{
							player.blueprints.Unlock(itemDefinition);
							player.SendConsoleCommand("gametip.hidegametip");
							player.SendConsoleCommand("gametip.showgametip", "You Learned " + itemDefinition.displayName.english);
						});

						timerOffset += 1.8f;
					}
					else if (item.UnlockLevel == level)
					{
						timer.Once(timerOffset, () =>
						{
							player.SendConsoleCommand("gametip.hidegametip");
							player.SendConsoleCommand("gametip.showgametip", "Unlocked " + itemDefinition.displayName.english);
						});

						timerOffset += 1.8f;
					}
                }
            }

            timer.Once(timerOffset, () =>
            {
                player.SendConsoleCommand("gametip.hidegametip");
            });
        }

        private bool IsUnlockable(string shortName, int level)
        {
            foreach (ItemData item in _config.Options.ItemDatas)
            {
                if (item.ShortName == shortName && item.UnlockLevel <= level)
                {
                    return true;
                }
            }

            return false;
        }

        private void ResetConfigXpLevels()
        {
			Server.Command("oxide.unload NoWorkbench", new object[0]);
            PrintWarning("Resetting xp levels!");
            _config.Options.ItemDatas.Clear();

            foreach (ItemDefinition itemDefinition in ItemManager.itemList)
            {
                if (itemDefinition.Blueprint != null && itemDefinition.Blueprint.isResearchable && !itemDefinition.Blueprint.defaultBlueprint && !BlockedBps.Contains(itemDefinition.shortname))
                {
                    float score = 0.0f;

                    Rust.Rarity rarity = itemDefinition.Blueprint.rarity;
                    score += 1.19f * (float)rarity;
                    int workbench = itemDefinition.Blueprint.workbenchLevelRequired;
                    score += 6.34f * (float)workbench;
                    int ingredients = itemDefinition.Blueprint.ingredients.Count;
                    score += 0.13f * (float)ingredients;

                    if (score > 14.0f)
                    {
                        score += (float)Math.Pow(1.25f, score - 14.0f) - 1.0f;
                    }

                    ItemData itemData = new ItemData();
                    itemData.ShortName = itemDefinition.shortname;
                    itemData.EnglishName = itemDefinition.displayName.english;
                    itemData.UnlockLevel = (int)Math.Floor(score);
                    itemData.CostXP = (int)(0.7f * itemData.UnlockLevel + 1.0f);
					itemData.UnlockOnLevel = false;
                    _config.Options.ItemDatas.Add(itemData);
                }
            }

            Config.WriteObject(_config, true);
			Server.Command("oxide.load NoWorkbench", new object[0]);
        }

        private void InfiniteComponents(BasePlayer player, bool removeComponents, bool giveComponents)
        {
            if (!_config.Options.UnlimitedComponents)
            {
                return;
            }

            if (_config.Options.BlockedItems == null || !_config.Options.BlockedItems.Any())
            {
                return;
            }

            if (removeComponents && player.inventory.containerMain.capacity > 24)
            {
                if (player?.inventory?.containerMain == null)
                {
                    return;
                }

                var retainedMainContainer = player.inventory.containerMain.uid;

                foreach (Item item in player.inventory.containerMain.itemList.ToList())
                {
                    if (_config.Options.BlockedItems.Contains(item?.info.shortname))
                    {
                        item.RemoveFromContainer();
                    }
                }

                ItemManager.DoRemoves();
                player.inventory.containerMain.capacity = 24;
            }

            if (giveComponents)
            {
                player.inventory.containerMain.capacity = 24 + _config.Options.BlockedItems.Count;

                NextFrame(() =>
                {
                    int hiddenSlotNumber = 0;

                    foreach (string itemName in _config.Options.BlockedItems)
                    {
                        Item item = ItemManager.CreateByName(itemName, 99999);
                        item.MoveToContainer(player.inventory.containerMain, 24 + hiddenSlotNumber, false);
                        item.LockUnlock(true, player);
                        hiddenSlotNumber++;
                    }
                });
            }
        }

        private void AssignLoot(LootContainer container)
        {
            if (_config.Options.BlockedItems == null || !_config.Options.BlockedItems.Any())
            {
                return;
            }

            if (container == null || container.inventory == null)
            {
                return;
            }

            foreach (var item in new List<Item>(container.inventory.itemList))
            {
                if (_config.Options.BlockedItems.Contains(item.info.shortname))
                {
                    item.RemoveFromContainer();
                }
            }

            container.inventory.dirty = true;
        }

        #endregion

        #region UIs

        private string colourText = UI.Color("#ffffff", 0.8f);
        private string colourClear = UI.Color("#ffffff", 0.0f);
        private string colourBackground = UI.Color("#b3b3b3", 0.05f);

        private const string uiPrefix = "XP_";
        private const string uiPanel = uiPrefix + "Panel";
        private const string uiLevel = uiPrefix + "Level";
        private const string uiXp = uiPrefix + "Xp";

        private void UpdatePlayerUi(BasePlayer player, bool refreshAll = true)
        {
			if (!_config.Display.HudEnabled)
			{
				return;
			}
			
            string colourProgressLevel = UI.Color(_config.Display.HudColourLevel, 1.0f);
            string colourProgressXp = UI.Color(_config.Display.HudColourXp, 1.0f);
            PlayerData playerData = GetPlayerData(player.userID);
            float playerLevel = playerData.Level;
            float playerXp = playerData.Xp;

            if (refreshAll)
            {
                // Element Panel.
                CuiHelper.DestroyUi(player, uiPanel);
                _uiManager.RemoveContainer(uiPanel);

                var elementPanel = UI.CreateElementContainer(uiPanel, colourClear,
                    (0.01f + _config.Display.OffsetX) + " " + (0.025f + _config.Display.OffsetY),
                    (0.13f + _config.Display.OffsetX) + " " + (0.1 + _config.Display.OffsetY));

                UI.LoadImage(ref elementPanel, uiPanel, _config.Display.LevelIcon, "0.025 0.65", "0.092 0.895");
				
				if (!_config.Options.UnlockAllOnLevel)
				{
					UI.LoadImage(ref elementPanel, uiPanel, _config.Display.XpIcon, "0.025 0.10", "0.092 0.365");
				}

                _uiManager.AddContainer(uiPanel);
                CuiHelper.AddUi(player, elementPanel);
            }

            // Element Level.
            CuiHelper.DestroyUi(player, uiLevel);
            _uiManager.RemoveContainer(uiLevel);

            var elementLevel = UI.CreateElementContainer(uiLevel, colourBackground,
                (0.01f + _config.Display.OffsetX) + " " + (0.065f + _config.Display.OffsetY),
                (0.135f + _config.Display.OffsetX) + " " + (0.1 + _config.Display.OffsetY));

            UI.CreatePanel(ref elementLevel, uiLevel, colourProgressLevel, "0.13 0.13",
                (playerLevel - (float)Math.Floor(playerLevel)) + " 0.85");
            UI.CreateText(ref elementLevel, uiLevel, colourText, "" + (int)Math.Floor(playerLevel), 14, "0.165 0.0", "1.0 1.0", TextAnchor.MiddleLeft);

            _uiManager.AddContainer(uiLevel);
            CuiHelper.AddUi(player, elementLevel);
			
			
			// Element Xp.
			CuiHelper.DestroyUi(player, uiXp);
			_uiManager.RemoveContainer(uiXp);

			if (!_config.Options.UnlockAllOnLevel)
			{
				var elementXp = UI.CreateElementContainer(uiXp, colourBackground,
					(0.01f + _config.Display.OffsetX) + " " + (0.025f + _config.Display.OffsetY),
					(0.135f + _config.Display.OffsetX) + " " + (0.06 + _config.Display.OffsetY));

				UI.CreatePanel(ref elementXp, uiXp, colourProgressXp, "0.12 0.13",
					(playerXp - (float)Math.Floor(playerXp)) + " 0.87");
				UI.CreateText(ref elementXp, uiXp, colourText, "" + (int)Math.Floor(playerXp), 14, "0.16 0.0", "1.0 1.0", TextAnchor.MiddleLeft);

				_uiManager.AddContainer(uiXp);
				CuiHelper.AddUi(player, elementXp);
			}
        }

        #endregion

        #region Helpers

        private string Lang(string key, BasePlayer player)
        {
            var userString = player == null ? "null" : player.UserIDString;
            return lang.GetMessage(key, this, userString);
        }

        private void MessagePlayer(string message, BasePlayer player)
        {
            player.ChatMessage(message);
        }

        private BasePlayer GetPlayerFromId(ulong id, bool canBeOffline)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.userID == id)
                {
                    return player;
                }
            }

            if (canBeOffline)
            {
                foreach (var player in BasePlayer.sleepingPlayerList)
                {
                    if (player.userID == id)
                    {
                        return player;
                    }
                }
            }

            return null;
        }

        private BasePlayer FindPlayer(string partialName, bool canBeOffline)
        {
            if (string.IsNullOrEmpty(partialName))
            {
                return null;
            }

            var players = new HashSet<BasePlayer>();

            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString.Equals(partialName))
                {
                    players.Add(activePlayer);
                }
                else if (!string.IsNullOrEmpty(activePlayer.displayName) && activePlayer.displayName.Contains(partialName, CompareOptions.IgnoreCase))
                {
                    players.Add(activePlayer);
                }
                else if (activePlayer.net?.connection != null && activePlayer.net.connection.ipaddress.Equals(partialName))
                {
                    players.Add(activePlayer);
                }
            }

            if (canBeOffline)
            {
                foreach (var sleepingPlayer in BasePlayer.sleepingPlayerList)
                {
                    if (sleepingPlayer.UserIDString.Equals(partialName))
                    {
                        players.Add(sleepingPlayer);
                    }
                    else if (!string.IsNullOrEmpty(sleepingPlayer.displayName) && sleepingPlayer.displayName.Contains(partialName, CompareOptions.IgnoreCase))
                    {
                        players.Add(sleepingPlayer);
                    }
                }
            }

            if (players.Count <= 0)
            {
                return null;
            }

            return players.First();
        }

        public ItemDefinition GetItemDefinition(string shortName)
        {
            if (string.IsNullOrEmpty(shortName) || shortName == "")
            {
                return null;
            }

            return ItemManager.FindItemDefinition(shortName.ToLower());
        }

        #endregion
    }
}