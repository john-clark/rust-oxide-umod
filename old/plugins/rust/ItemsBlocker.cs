//#define CHECK
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using System.Linq;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using System.Collections;
using Oxide.Core;
using System.IO;

namespace Oxide.Plugins
{
    [Info("ItemsBlocker", "Vlad-00003", "3.1.2", ResourceId = 2407)]
    [Description("Prevents some items from being used for a limited period of time.")]

    class ItemsBlocker : RustPlugin
    {
        #region Vars
        //private string Image = null;
        private Dictionary<string, string> Image =  new Dictionary<string, string>();
        private PluginConfig config;
        [PluginReference]
        Plugin Duel;
        private Dictionary<BasePlayer, Timer> Main = new Dictionary<BasePlayer, Timer>();
        private List<BasePlayer> OnScreen = new List<BasePlayer>();
        private Timer OnScreenUpdater;
        #endregion

        #region Config setup

        #region GUI Settings
        private class GUIPanel
        {
            [JsonProperty("Minimum anchor")]
            public string Amin;
            [JsonProperty("Maximum anchor")]
            public string Amax;
            [JsonProperty("Color")]
            public string Color;
        }
        private class GUIText : GUIPanel
        {
            [JsonProperty("Size")]
            public int Size;
            [JsonProperty("Outline")]
            public GUIOutline Outline;
        }
        private class GUIImage
        {
            [JsonProperty("Minimum anchor")]
            public string Amin;
            [JsonProperty("Maximum anchor")]
            public string Amax;
            [JsonProperty("Link to the image or file in the data folder")]
            public string Image;
            [JsonProperty("Opacity of the image")]
            public float Opacity;
        }
        private class GUIOutline
        {
            [JsonProperty("Use Outline")]
            public bool Use = true;
            [JsonProperty("Outline color")]
            public string Color = "0 0 0 1";
            [JsonProperty("Outline distance")]
            public string Distance = "1.0 -1.0";
        }
        #endregion

        private class GUISettings
        { 
            [JsonProperty("Backgound for main panel")]
            public GUIPanel Background = new GUIPanel()
            {
                Amin = null,
                Amax = null,
                Color = "0 0 0 0.8"
            };
            [JsonProperty("Main panel settings (shows if player attemts to use blocked cloth/item)")]
            public GUIPanel MainPanel = new GUIPanel()
            {
                Amin = "0.266 0.361",
                Amax = "0.734 0.639",
                Color = "#42e2f49f"
            };
            [JsonProperty("Settings for the text on main panel")]
            public GUIText TextOnMain = new GUIText()
            {
                Amin = "0 0",
                Amax = "1 1",
                Color = "#f4d041",
                Size = 20,
                Outline = new GUIOutline()
            };
            [JsonProperty("Use On Screen Panel")]
            public bool UseOnScreenPanel = true;
            [JsonProperty("On Screen Panel (shown if the block is active)")]
            public GUIPanel OnScreenPanel = new GUIPanel()
            {
                Amin = "0.016 0.028",
                Amax = "0.172 0.167",
                Color = "0 0 0 0.7"
            };
            [JsonProperty("Text settings for On Screen Panel")]
            public GUIText TextOnScreen = new GUIText()
            {
                Amin = "0 0",
                Amax = "0.66 1",
                Color = "green",
                Size = 13,
                Outline = new GUIOutline()
            };
            [JsonProperty("Image (shown on On Screen Panel")]
            public GUIImage Image = new GUIImage()
            {
                Amin = "0.67 0.25",
                Amax = "0.92 0.75",
                Opacity = 0.8f,
                Image = "http://www.rigormortis.be/wp-content/uploads/rust-icon-512.png"
            };
        }
        private class PluginConfig
        {
            [JsonProperty("Block end time")]
            public string BlockEndStr;
            [JsonProperty("Hour of block after wipe")]
            public int HoursOfBlock = 30;
            [JsonProperty("Chat prefix")]
            public string Prefix = "[Items Blocker]";
            [JsonProperty("Chat prefix color")]
            public string PrefixColor = "#f44253";
            [JsonProperty("Use chat insted of GUI")]
            public bool UseChat = false;
            [JsonProperty("Bypass permission")]
            public string BypassPermission = "itemsblocker.bypass";
            [JsonProperty("GUI Settings")]
            public GUISettings Gui = new GUISettings();
            [JsonProperty("List of blocked items")]
            public List<string> BlockedItems;
            [JsonProperty("List of blocked clothes")]
            public List<string> BlockedClothes;
            [JsonProperty("List of blocked ammunition")]
            public List<string> BlockedAmmo;

            [JsonIgnore]
            public DateTime BlockEnd;

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    BlockEndStr = DateTime.Now.AddHours(30).ToString("dd.MM.yyyy HH:mm:ss"),
                    BlockedItems = new List<string>()
                    {
                        "Satchel Charge",
                        "Timed Explosive Charge",
                        "Eoka Pistol",
                        "Custom SMG",
                        "Assault Rifle",
                        "Bolt Action Rifle",
                        "Waterpipe Shotgun",
                        "Revolver",
                        "Thompson",
                        "Semi-Automatic Rifle",
                        "Semi-Automatic Pistol",
                        "Pump Shotgun",
                        "M249",
                        "Rocket Launcher",
                        "Flame Thrower",
                        "Double Barrel Shotgun",
                        "Beancan Grenade",
                        "F1 Grenade",
                        "MP5A4",
                        "LR-300 Assault Rifle",
                        "M92 Pistol",
                        "Python Revolver"
                    },
                    BlockedClothes = new List<string>()
                    {
                        "Metal Facemask",
                        "Metal Chest Plate",
                        "Road Sign Kilt",
                        "Road Sign Jacket",
                        "Heavy Plate Pants",
                        "Heavy Plate Jacket",
                        "Heavy Plate Helmet",
                        "Riot Helmet",
                        "Bucket Helmet",
                        "Coffee Can Helmet"
                    },
                    BlockedAmmo =  new List<string>()
                    {
                        "HV Pistol Ammo",
                        "Incendiary Pistol Bullet",
                        "HV 5.56 Rifle Ammo",
                        "Incendiary 5.56 Rifle Ammo",
                        "Explosive 5.56 Rifle Ammo",
                        "12 Gauge Slug",
                        "High Velocity Arrow",
                        "Incendiary Rocket",
                        "Rocket",
                        "High Velocity Rocket"
                    }
                };
            }
        }
        #endregion

        #region Config and Data Initialization
        protected override void LoadDefaultConfig()
        {
            PrintWarning("New configuration file created, Block Start now and will remain for 30 hours. You can change it into the config.");
            config = PluginConfig.DefaultConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            DateTime BlockEnd;
            if (!DateTime.TryParseExact(config.BlockEndStr, "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out BlockEnd))
            {
                BlockEnd = SaveRestore.SaveCreatedTime.AddHours(config.HoursOfBlock);
                PrintWarning($"Unable to parse block end date format, block end set to {BlockEnd.ToString("dd.MM.yyyy HH:mm:ss")}");
                config.BlockEndStr = BlockEnd.ToString("dd.MM.yyyy HH:mm:ss");
                SaveConfig();
            }
            config.BlockEnd = BlockEnd;
            permission.RegisterPermission(config.BypassPermission, this);
            if(!string.IsNullOrEmpty(config.Gui.Image.Image))
                if (!config.Gui.Image.Image.ToLower().Contains("http"))
                {
                    config.Gui.Image.Image = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + config.Gui.Image.Image;
                }
            LoadData();
            permission.RegisterPermission("itemsblocker.refresh", this);
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        #endregion

        #region Data (Image save\load)
        private void LoadData()
        {
            try
            {
                Image = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, string>>(Title);
            }
            catch (Exception ex)
            {
                if(ex is JsonSerializationException)
                {
                    try
                    {
                        string old = Interface.Oxide.DataFileSystem.ReadObject<string>(Title);
                        Image[config.Gui.Image.Image] = old;
                        SaveData();
                        return;
                    }
                    catch(Exception ex1)
                    {
                        PrintWarning("Failed to convert old data fromat to the new. Data wiped.\n{0}", ex1.Message);
                        Image = new Dictionary<string, string>();
                        return;
                    }
                }
                PrintWarning("Failed to load datafile (is the file corrupt?)\n{0}", ex.Message);
                Image = new Dictionary<string, string>();
            }
        }
        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Title, Image);
        }
        #endregion

        #region Initialization and quiting
        void OnServerInitialized()
        {
            if (!Image.ContainsKey(config.Gui.Image.Image))
                DownloadImage();
            else
                OnScreenPanel(true);
        }
        void Unload() => DestroyAllGui();
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ItemBlocked"] = "Using this item is blocked!",
                ["BlockTimeLeft"] = "\n{0}d {1:00}:{2:00}:{3:00} until unblock.",
                ["Weapon line 2"] = "\nYou can only use Hunting bow and Crossbow",
                ["Cloth line 2"] = "\nYou can only use wood and bone armor!",
                ["OnlyPlayer"] = "This command can be executed only from the game!",
                ["OnScreenText"] = "Some of the items are blocked!",
                ["Ammo blocked"] = "The ammo you are trying to use is blocked!"
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ItemBlocked"] = "Использование данного предмета заблокировано!",
                ["BlockTimeLeft"] = "\nДо окончания блокировки осталось {0}д. {1:00}:{2:00}:{3:00}",
                ["Weapon line 2"] = "\nВы можете использовать только Лук и Арбалет",
                ["Cloth line 2"] = "\nИспользуйте только деревянную и костяную броню!",
                ["OnlyPlayer"] = "Эту команду можно использовать только в игре!",
                ["OnScreenText"] = "Некоторые предметы заблокированы!",
                ["Ammo blocked"] = "Вид боеприпасов, которые вы пытаетесь использовать заблокирован!"
            }, this, "ru");
        }
        string GetMsg(string key, BasePlayer player = null) => lang.GetMessage(key, this, player?.UserIDString);
        string GetMsg(string key) => lang.GetMessage(key, this);
        #endregion

        #region Oxide hooks
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (Main.ContainsKey(player))
                Main.Remove(player);
            if (OnScreen.Contains(player))
                OnScreen.Remove(player);
        }
        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!InBlock)
                return;
            if (!OnScreen.Contains(player))
                OnScreenPanelMain(player);
        }
        void OnNewSave(string filename)
        {
            config.BlockEnd = DateTime.Now.AddHours(config.HoursOfBlock);
            config.BlockEndStr = config.BlockEnd.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture);
            SaveConfig();
            PrintWarning($"Wipe detected. Block end set to {config.BlockEndStr}");
        }
        object CanEquipItem(PlayerInventory inventory, Item item)
        {
            if (InBlock)
            {
                var player = inventory.GetComponent<BasePlayer>();
                if (InDuel(player) || IsNPC(player)) return null;
                if (permission.UserHasPermission(player.UserIDString, config.BypassPermission))
                    return null;
                if (config.BlockedItems.Contains(item.info.displayName.english) || config.BlockedItems.Contains(item.info.shortname))
                {
                    string reply = GetMsg("ItemBlocked", player);
                    reply += GetMsg("BlockTimeLeft", player);
                    reply += GetMsg("Weapon line 2", player);

                    if (config.UseChat)
                    {
                        SendToChat(player, reply);
                    }
                    else
                    {
                        BlockerUi(player, reply);
                    }
                    return false;
                }
            }
            return null;
        }
        object CanWearItem(PlayerInventory inventory, Item item)
        {
            if (InBlock)
            {
                var player = inventory.GetComponent<BasePlayer>();
                if (InDuel(player) || IsNPC(player)) return null;
                if (permission.UserHasPermission(player.UserIDString, config.BypassPermission))
                    return null;
                if (config.BlockedClothes.Contains(item.info.displayName.english) || config.BlockedClothes.Contains(item.info.shortname))
                {
                    string reply = GetMsg("ItemBlocked", player);
                    reply += GetMsg("BlockTimeLeft", player);
                    reply += GetMsg("Cloth line 2", player);
                    if (config.UseChat)
                    {
                        SendToChat(player, reply);
                    }
                    else
                    {
                        BlockerUi(player, reply);
                    }
                    return false;
                }
            }
            return null;
        }
        void OnReloadWeapon(BasePlayer player, BaseProjectile projectile)
        {
            if (!InBlock) return;
            if (InDuel(player) || IsNPC(player)) return;
            if(IsAmmoBlocked(player, projectile))
            {
                SendToChat(player, GetMsg("Ammo blocked", player) + GetMsg("BlockTimeLeft", player));
            }
        }
        object OnReloadMagazine(BasePlayer player, BaseProjectile projectile)
        {
            if (InBlock)
            {
                if (InDuel(player) || IsNPC(player)) return null;
                var ammo = projectile.primaryMagazine.ammoType;
                if (IsAmmoBlocked(player, projectile))
                {
                    projectile.SendNetworkUpdateImmediate();
                    return false;
                }
            }
            return null;
        }
        #endregion

        #region Image
        [ConsoleCommand("ib.refresh")]
        private void CmdRefresh(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.player != null)
            {
                BasePlayer player = arg.Connection.player as BasePlayer;

                if (!permission.UserHasPermission(player.UserIDString, "itemsblocker.refresh"))
                    return;
            }
            DownloadImage();
        }
        private void DownloadImage()
        {
            PrintWarning("Downloading image...");
            ServerMgr.Instance.StartCoroutine(DownloadImage(config.Gui.Image.Image));
        }
        IEnumerator DownloadImage(string url)
        {
            using (var www = new WWW(url))
            {
                yield return www;
                if (this == null) yield break;
                if (www.error != null)
                {
                    PrintError($"Failed to add image. File address possibly invalide\n {url}");
                }
                else
                {
                    var reply = 0;
                    var tex = www.texture;
                    byte[] bytes = tex.EncodeToPNG();
                    Image[url] = FileStorage.server.Store(www.bytes, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
                    SaveData();
                    PrintWarning("Image download is complete.");
                    OnScreenPanel(true);
                    UnityEngine.Object.DestroyImmediate(tex);
                    yield break;
                }
            }
        }
        #endregion

        #region GUI

        #region PanelNames
        private string BlockerParent = "BlockerUI";
        private string BlockerPanel = "BlockerUIPanel";
        private string BlockerText = "BlockerUIText";
        private string OnScreenParent = "BlockerUIOnScreen";
        private string OnScreenText = "BlockerUIOnScreenText";
        #endregion

        #region GUI Creation
        private class UI
        {
            private static string ToRustColor(string input)
            {
                Color color;
                if (!ColorUtility.TryParseHtmlString(input, out color))
                {
                    var split = input.Split(' ');
                    for (var i = 0; i < 4; i++)
                    {
                        float num;
                        if (!float.TryParse(split[i], out num))
                        {
                            return null;
                        }
                        color[i] = num;
                    }
                }
                return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
            }

            #region Override to custom classes
            public static void CreatePanel(ref CuiElementContainer container, string Parent, string Name, GUIPanel panel, bool CursorEnabled = false) =>
                CreatePanel(ref container, Parent, Name, panel.Color, panel.Amin, panel.Amax, CursorEnabled);
            public static void CreateImage(ref CuiElementContainer container, string Parent, string Name, GUIImage Image, string Png) =>
                CreateImage(ref container, Parent, Name, Image.Opacity, Image.Amin, Image.Amax, Png);
            public static void CreateText(ref CuiElementContainer container, string Parent, string Name, GUIText TextComp, string Text, TextAnchor Anchor = TextAnchor.MiddleCenter) =>
                CreateText(ref container, Parent, Name, TextComp.Amin, TextComp.Amax, Text, TextComp.Color, TextComp.Size, TextComp.Outline, Anchor);
            #endregion
            public static void CreatePanel(ref CuiElementContainer container, string Parent, string Name, string Color, string Amin, string Amax, bool CursorEnabled = false)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = Amin, AnchorMax = Amax },
                    Image = { Color = ToRustColor(Color) },
                    CursorEnabled = CursorEnabled
                }, Parent, Name);
            }
            public static void CreateImage(ref CuiElementContainer container, string Parent, string Name, float opacity, string Amin, string Amax, string Image)
            {
                var ImageComp = new CuiRawImageComponent { Sprite = "assets/content/textures/generic/fulltransparent.tga", Color = $"1 1 1 {opacity}" };
                if (Image != null)
                {
                    ImageComp.Png = Image;
                }
                container.Add(new CuiElement
                {
                    Name = Name ?? CuiHelper.GetGuid(),
                    Parent = Parent,
                    Components = { ImageComp, new CuiRectTransformComponent { AnchorMin = Amin, AnchorMax = Amax } }
                });
            }
            public static void CreateFulscreenButton(ref CuiElementContainer container, string Parent, string Name, string Command)
            {
                container.Add(new CuiButton
                {
                    Button = { Command = Command, Color = "0 0 0 0"},
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1"},
                    Text = { Text = ""}
                }, Parent, Name);
            }
            public static void CreateText(ref CuiElementContainer container, string Parent, string Name, string Amin, string Amax, string Text, string TextColor, int FontSize, GUIOutline outline = null, TextAnchor Anchor = TextAnchor.MiddleCenter)
            {
                var Element = new CuiElement
                {
                    Parent = Parent,
                    Name = Name ?? CuiHelper.GetGuid(),
                    Components =
                    {
                        new CuiTextComponent { Color = ToRustColor(TextColor), FontSize = FontSize, Text = Text, Align = Anchor },
                        new CuiRectTransformComponent { AnchorMin = Amin, AnchorMax = Amax }
                    }
                };
                if (outline != null && outline.Use)
                {
                    Element.Components.Add(
                        new CuiOutlineComponent { Color = ToRustColor(outline.Color), Distance = outline.Distance });
                }
                container.Add(Element);
            }
        }
        #endregion

        #region Commands
        [ConsoleCommand("ib.close")]
        private void CmdCloseUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if(player == null)
            {
                arg.ReplyWith(GetMsg("OnlyPlayer"));
                return;
            }
            if (Main.ContainsKey(player))
            {
                Main[player]?.Destroy();
                Main.Remove(player);
                CuiHelper.DestroyUi(player, BlockerParent);
            }
        }
        #endregion

        private void DestroyAllGui()
        {
            OnScreenUpdater?.Destroy();
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, OnScreenParent);
                if (Main.ContainsKey(player))
                {
                    Main[player]?.Destroy();
                    Main.Remove(player);
                    CuiHelper.DestroyUi(player, BlockerParent);
                }
            }

        }

        private void OnScreenPanel(bool init = false)
        {
            if (!config.Gui.UseOnScreenPanel)
                return;
            if (init)
                foreach (var player in BasePlayer.activePlayerList)
                    OnScreenPanelMain(player);
            if (!InBlock)
            {
                DestroyAllGui();
                return;
            }
            foreach(var player in OnScreen)
            {
                CuiHelper.DestroyUi(player, OnScreenText);
                var container = new CuiElementContainer();
                string text = GetMsg("OnScreenText", player) + GetMsg("BlockTimeLeft", player);
                var timeleft = TimeLeft;
                text = string.Format(text, timeleft.Days, timeleft.Hours, timeleft.Minutes, timeleft.Seconds);
                UI.CreateText(ref container, OnScreenParent, OnScreenText, config.Gui.TextOnScreen, text);
                CuiHelper.AddUi(player, container);
            }
            OnScreenUpdater = timer.Once(1f, () => OnScreenPanel());
        }
        private void OnScreenPanelMain(BasePlayer player)
        {
            if (!config.Gui.UseOnScreenPanel)
                return;
            CuiHelper.DestroyUi(player, OnScreenParent);
            var container = new CuiElementContainer();
            UI.CreatePanel(ref container, "Hud", OnScreenParent, config.Gui.OnScreenPanel);
            if(Image.ContainsKey(config.Gui.Image.Image))
                UI.CreateImage(ref container, OnScreenParent, null, config.Gui.Image, Image[config.Gui.Image.Image]);
            CuiHelper.AddUi(player, container);
            OnScreen.Add(player);
        }
        private void BlockerUi(BasePlayer player, string inputText)
        {
            if (!Main.ContainsKey(player))
                ShowBlocker(player);
            CuiHelper.DestroyUi(player, BlockerText);
            var timeleft = TimeLeft;
            string formatted = string.Format(inputText, timeleft.Days, timeleft.Hours, timeleft.Minutes, timeleft.Seconds);
            var container = new CuiElementContainer();
            UI.CreateText(ref container, BlockerPanel, BlockerText, config.Gui.TextOnMain, formatted);
            CuiHelper.AddUi(player, container);
            Main[player] = timer.Once(1f, () => BlockerUi(player, inputText));
        }
        private void ShowBlocker(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, BlockerParent);
            var container = new CuiElementContainer();
            UI.CreatePanel(ref container, "Overlay", BlockerParent, config.Gui.Background.Color, "0 0", "1 1", true);
            UI.CreatePanel(ref container, BlockerParent, BlockerPanel, config.Gui.MainPanel);
            UI.CreateFulscreenButton(ref container, BlockerParent, null, "ib.close");
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Helpers

        private void Debug(string text)
        {
#if CHECK
            PrintWarning(text);
#endif
        }
        private bool IsAmmoBlocked(BasePlayer owner, BaseProjectile proj)
        {
            Debug($"IsAmmoBlocked called on player \"{owner.displayName}\" for weapon \"{proj.GetItem()?.info.displayName.english ?? "Unknown"}\"");
            List<Item> currentAmmo = owner.inventory.FindItemIDs(proj.primaryMagazine.ammoType.itemid).ToList();
            Item newAmmo = null;
            Debug($"CurrentAmmo count = {currentAmmo.Count}");
            if (currentAmmo.Count == 0)
            {
                List<Item> newAmmoList = new List<Item>();
                owner.inventory.FindAmmo(newAmmoList, proj.primaryMagazine.definition.ammoTypes);
                Debug(
                    $"CurrentAmmo count equals 0. NewAmmoList count = {newAmmoList.Count}\nTrying to get the first item.");
                if (newAmmoList.Count == 0)
                    return false;
                try
                {
                    newAmmo = newAmmoList[0];
                    Debug($"Successfully got item {newAmmo.info.displayName.english}");
                }
                catch (Exception e)
                {
                    PrintError($"Error in attempt to get the item from NewAmmoList. {e.GetType()}:\n{e.Message}");
                    return false;
                }
            }
            else
            {
                Debug($"CurrentAmmo count doesn't equals 0. Getting NewAmmo out of it.");
                try
                {
                    newAmmo = currentAmmo[0];
                    Debug($"Successfully got item {newAmmo.info.displayName.english}");
                }
                catch (Exception e)
                {
                    PrintError($"Error in attempt to get the item from the CurrentAmmo list. {e.GetType()}:\n{e.Message}");
                    return false;
                }
            }
            return config.BlockedAmmo.Contains(newAmmo.info.displayName.english) || config.BlockedAmmo.Contains(newAmmo.info.shortname);
        }
        private bool IsNPC(BasePlayer player)
        {
            //BotSpawn
            if (player is NPCPlayer)
                return true;
            //HumanNPC
            if (!(player.userID >= 76560000000000000L || player.userID <= 0L))
                return true;
            return false;
        }
        private bool InDuel(BasePlayer player)
        {
            if (Duel == null) return false;
            return (bool)Duel.Call("IsPlayerOnActiveDuel", player);
        }
        TimeSpan TimeLeft => config.BlockEnd.Subtract(DateTime.Now);
        private bool InBlock
        {
            get
            {
                if (TimeLeft.TotalSeconds >= 0)
                {
                    return true;
                }
                return false;
            }
        }
        private void SendToChat(BasePlayer Player, string Message)
        {
            var timeleft = TimeLeft;
            Message = string.Format(Message, timeleft.Days, timeleft.Hours, timeleft.Minutes, timeleft.Seconds);
            PrintToChat(Player, "<color=" + config.PrefixColor + ">" + config.Prefix + "</color> " + Message);
        }
        #endregion
    }
}