using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;

using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Libraries.Covalence;

using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AutoChat", "Frenk92", "0.5.0", ResourceId = 2230)]
    [Description("Automatic clans/private chat switching")]
    class AutoChat : RustPlugin
    {
        [PluginReference]
        Plugin BetterChat, AdminChatroom;

        bool BC = true;
        const string PermAdmin = "autochat.admin";
        const string PermUse = "autochat.use";
        static AutoChat ac;

        #region ChatInfo

        class ChatInfo : MonoBehaviour
        {
            public BasePlayer player { get; set; }
            public string cmd { get; set; }
            public string target { get; set; }
            public int count { get; set; }
            public bool ui { get; set; }

            public string fullcmd => $"{cmd}{target}";

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                cmd = "g";
                target = "";
                count = 0;
                ui = false;
            }

            void Start()
            {
                if (ac._config.SwitchTime == 0) return;
                CancelInvoke("ChatUpdate");
                count = ac._config.SwitchTime;
                InvokeRepeating("ChatUpdate", 0f, 1f);
            }

            public void Stop()
            {
                CancelInvoke("ChatUpdate");
                cmd = "g";
                target = "";
                Switch(cmd, target);
            }

            void ChatUpdate()
            {
                --count;
                if (count <= 0) Stop();
            }

            void FixedUpdate()
            {
                if (!ac._config.UIEnabled)
                {
                    if (ui) ToggleUI(false);
                    return;
                }

                if (ui)
                {
                    if (player.IsSleeping() || player.IsWounded() || player.IsDead())
                        ToggleUI(false);
                }
                else
                {
                    if (player.IsAlive() && !player.IsSleeping() && !player.IsWounded())
                        ToggleUI(true);
                }
            }

            void ToggleUI(bool flag)
            {
                if (ui && !flag)
                {
                    DestroyUI(player);
                    ui = false;
                }
                if (!ui && flag)
                {
                    ac.AddUI(player, cmd);
                    ui = true;
                }
            }

            public void Switch(string cmd, string target = "")
            {
                this.cmd = cmd;
                this.target = target;
                if (ac._config.UIEnabled && ui)
                {
                    DestroyUI(player);
                    ac.AddUI(player, cmd);
                }
                if (cmd != "g") Start();
            }

            public void Destroy()
            {
                CancelInvoke("ChatUpdate");
                DestroyUI(player);
                GameObject.Destroy(this);
            }
        }

        #endregion

        #region Config

        ConfigData _config;
        class ConfigData
        {
            public bool Enabled { get; set; } = true;
            public bool PlayerActive { get; set; } = false;
            public bool ShowSwitchMessage { get; set; } = true;
            public int SwitchTime { get; set; } = 600;
            public Dictionary<string, List<string>> CustomChat { get; set; } = new Dictionary<string, List<string>>() { { "Test", new List<string> { "command1", "command2" } } };
            public bool UIEnabled { get; set; } = true;
            public UIConfig UISettings { get; set; } = new UIConfig();
        }

        class UIConfig
        {
            public string BackgroundColor { get; set; } = "0.29 0.49 0.69 0.5";
            public string TextColor { get; set; } = "#0000FF";
            public string FontSize { get; set; } = "15";
            public string AnchorMin { get; set; } = "0 0.125";
            public string AnchorMax { get; set; } = "0.012 0.1655";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<ConfigData>();
                if (_config == null) LoadDefaultConfig();
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning(Lang("NoConfig"));
            _config = new ConfigData();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion

        #region Data

        List<string> ChatType = new List<string>();
        List<string> LoadedPlugins = new List<string>();

        Dictionary<string, List<string>> cmdPlugins = new Dictionary<string, List<string>>
        {
            { "Clans", new List<string> { "c", "a" } }, //Universal Clans
            { "Rust:IO Clans", new List<string> { "c" } }, //Rust:IO Clans
            { "Friends.dcode", new List<string> { "fm", "f", "pm $target", "m $target", "rm", "r" } }, //Universal Friends
            { "PrivateMessage", new List<string> { "pm $target", "r" } }, //Private Message
            { "Private Messages", new List<string> { "pm $target", "r" } }, //Private Messages (Universal)
            { "Admin Chatroom", new List<string> { "a" } }, //Admin Chatroom
            { "Admin Chat", new List<string> { "a" } } //Admin Chat
        };

        Dictionary<ulong, PlayerChat> Users = new Dictionary<ulong, PlayerChat>();
        class PlayerChat
        {
            public string Name { get; set; }
            public bool Active { get; set; }

            public PlayerChat(string Name, bool Active)
            {
                this.Name = Name;
                this.Active = Active;
            }
        }

        private void LoadData() { Users = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, PlayerChat>>("AutoChat"); }
        private void SaveData() { Interface.Oxide.DataFileSystem.WriteObject("AutoChat", Users); }

        PlayerChat GetPlayerData(BasePlayer player)
        {
            PlayerChat playerData;
            if (!Users.TryGetValue(player.userID, out playerData))
            {
                Users[player.userID] = playerData = new PlayerChat(player.displayName, _config.PlayerActive);
                SaveData();
            }

            return playerData;
        }

        #endregion

        #region Hooks

        void OnServerInitialized() { CheckPlugins(); }

        void Loaded()
        {
            LoadData();
            DefaultMessages();

            permission.RegisterPermission(PermAdmin, this);
            permission.RegisterPermission(PermUse, this);

            if (_config.Enabled && _config.UIEnabled)
            {
                foreach (var p in BasePlayer.activePlayerList)
                {
                    if (!Users.ContainsKey(p.userID) && !_config.PlayerActive) continue;
                    var d = GetPlayerData(p);
                    if (d.Active) p.gameObject.AddComponent<ChatInfo>();
                }
            }

            ac = this;
        }

        void Unload()
        {
            ChatType.Clear();
            LoadedPlugins.Clear();

            foreach (var c in Resources.FindObjectsOfTypeAll<ChatInfo>()) c.Destroy();
        }

        void OnPlayerInit(BasePlayer player)
        {
            if (!_config.Enabled || (!_config.PlayerActive && !Users.ContainsKey(player.userID))) return;
			var d = GetPlayerData(player);
            if (d.Active) player.gameObject.AddComponent<ChatInfo>();
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            var info = player.GetComponent<ChatInfo>();
            if (info != null) info.Destroy();
        }

        void OnPluginLoaded(Plugin pl)
        {
            if (LoadedPlugins.Contains(pl.Name)) return;

            var load = false;
            if (cmdPlugins.ContainsKey(pl.Title))
            {
                var cp = cmdPlugins.FirstOrDefault(x => x.Key.Equals($"{pl.Title}.{pl.Author}") || x.Key.Equals(pl.Title)).Value;
                foreach (var cmd in cp) ChatType.Add(cmd);
                load = true;
            }
            else if (_config.CustomChat.ContainsKey(pl.Name))
            {
                foreach (var cc in _config.CustomChat[pl.Name]) ChatType.Add(cc);
                load = true;
            }

            if (load)
            {
                LoadedPlugins.Add(pl.Name);
                Print("Loaded", pl.Name);
            }
        }

        void OnPluginUnloaded(Plugin pl)
        {
            if (cmdPlugins.ContainsKey(pl.Title))
                timer.Once(3, () =>
                {
                    if (!plugins.Exists(pl.Name))
                    {
                        var cp = cmdPlugins.FirstOrDefault(x => x.Key.Equals($"{pl.Title}.{pl.Author}") || x.Key.Equals(pl.Title)).Value;
                        foreach (var cmd in cp) ChatType.Remove(cmd);
                        LoadedPlugins.Remove(pl.Name);
                        Print("Unloaded", pl.Name);
                    }
                });

            if (_config.CustomChat.ContainsKey(pl.Name))
                timer.Once(3, () =>
                {
                    if (!plugins.Exists(pl.Name))
                    {
                        foreach (var cc in _config.CustomChat[pl.Name]) ChatType.Remove(cc);
                        LoadedPlugins.Remove(pl.Name);
                        Print("Unloaded", pl.Name);
                    }
                });
        }

        #endregion

        #region Commands

        [ChatCommand("ac")]
        private void cmdAutoChat(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0 || args == null) return;
            if (!_config.Enabled && !HasPermission(player.UserIDString, PermAdmin))
            {
                MessageChat(player, "IsDisabled");
                return;
            }
            if (!HasPermission(player.UserIDString, PermUse))
            {
                MessageChat(player, "NoPerm");
                return;
            }

            try
            {
                switch (args[0].ToLower())
                {
                    case "help":
                        {
                            if (HasPermission(player.UserIDString, PermAdmin))
                                PrintToChat(player, Lang("Help", player.UserIDString) + Lang("HelpAdmin", player.UserIDString));
                            else
                                MessageChat(player, "Help");
                            break;
                        }
                    case "active":
                        {
                            var playerData = GetPlayerData(player);
                            var flag = playerData.Active = !playerData.Active;
                            if (args.Length > 1)
                            {
                                if (!bool.TryParse(args[1], out flag))
                                {
                                    MessageChat(player, "ErrorBool");
                                    break;
                                }
                            }
                            playerData.Active = flag;

                            var c = player.GetComponent<ChatInfo>();
                            if (playerData.Active)
                            {
                                if (c == null) player.gameObject.AddComponent<ChatInfo>();
                                MessageChat(player, "Activated");
                            }
                            else
                            {
                                if (c != null) c.Destroy();
                                MessageChat(player, "Deactivated");
                            }
                            SaveData();
                            break;
                        }
                    case "enable":
                        {
                            if (!HasPermission(player.UserIDString, PermAdmin))
                            {
                                MessageChat(player, "NoPerm");
                                break;
                            }

                            var enabled = _config.Enabled = !_config.Enabled;
                            if (args.Length > 1)
                            {
                                if (!bool.TryParse(args[1], out enabled))
                                {
                                    MessageChat(player, "ErrorBool");
                                    break;
                                }
                            }
                            _config.Enabled = enabled;

                            if (enabled)
                            {
                                MessageChat(player, "Enabled");
                                foreach (var p in BasePlayer.activePlayerList)
                                {
                                    if (!Users.ContainsKey(p.userID) && !_config.PlayerActive) continue;
                                    var d = GetPlayerData(p);
                                    if (d.Active) p.gameObject.AddComponent<ChatInfo>();
                                }
                            }
                            else
                            {
                                foreach (var c in Resources.FindObjectsOfTypeAll<ChatInfo>()) c.Destroy();
                                MessageChat(player, "Disabled");
                            }
                            SaveConfig();
                            break;
                        }
                    case "auto":
                        {
                            if (!HasPermission(player.UserIDString, PermAdmin))
                            {
                                MessageChat(player, "NoPerm");
                                break;
                            }

                            var pa = _config.PlayerActive = !_config.PlayerActive;
                            if (args.Length > 1)
                            {
                                if (!bool.TryParse(args[1], out pa))
                                {
                                    MessageChat(player, "ErrorBool");
                                    break;
                                }
                            }
                            _config.PlayerActive = pa;

                            if (pa)
                                MessageChat(player, "AutoON");
                            else
                                MessageChat(player, "AutoOFF");
                            SaveConfig();
                            break;
                        }
                    case "ui":
                        {
                            if (!HasPermission(player.UserIDString, PermAdmin))
                            {
                                MessageChat(player, "NoPerm");
                                break;
                            }

                            var ui = _config.UIEnabled = !_config.UIEnabled;
                            if (args.Length > 1)
                            {
                                if (!bool.TryParse(args[1], out ui))
                                {
                                    MessageChat(player, "ErrorBool");
                                    break;
                                }
                            }
                            _config.UIEnabled = ui;

                            if (ui)
                                MessageChat(player, "UIEnabled");
                            else
                                MessageChat(player, "UIDisabled");
                            SaveConfig();
                            break;
                        }
                    case "msg":
                        {
                            if (!HasPermission(player.UserIDString, PermAdmin))
                            {
                                MessageChat(player, "NoPerm");
                                break;
                            }

                            var msg = _config.ShowSwitchMessage = !_config.ShowSwitchMessage;
                            if (args.Length > 1)
                            {
                                if (!bool.TryParse(args[1], out msg))
                                {
                                    MessageChat(player, "ErrorBool");
                                    break;
                                }
                            }
                            _config.ShowSwitchMessage = msg;

                            if (msg)
                                MessageChat(player, "MsgON");
                            else
                                MessageChat(player, "MsgOFF");
                            SaveConfig();
                            break;
                        }
                }
            }
            catch { }
        }

        [ChatCommand("g")]
        private void cmdGlobalChat(BasePlayer player, string command, string[] args)
        {
            if (!_config.Enabled || !isActive(player.userID)) return;

            var c = player.GetComponent<ChatInfo>();
            if (c == null) return;
            if (args.Length == 0 || args == null)
            {
                if (c.cmd != "g")
                {
                    c.Stop();
                    MessageChat(player, "GlobalChat");
                }
                return;
            }
            if (c.cmd != "g") c.Stop();

            rust.RunClientCommand(player, "chat.say", string.Join(" ", args));
        }

        [ConsoleCommand("ac")]
        private void consAutoChat(ConsoleSystem.Arg arg)
        {
            if ((arg.Connection != null && arg.Connection.authLevel < 2) || arg.Args.Length == 0 || arg.Args == null) return;

            switch (arg.Args[0].ToLower())
            {
                case "enable":
                    {
                        var enabled = _config.Enabled = !_config.Enabled;
                        if (arg.Args.Length > 1)
                        {
                            if (!bool.TryParse(arg.Args[1], out enabled))
                            {
                                Print("ErrorBool");
                                break;
                            }
                        }
                        _config.Enabled = enabled;

                        if (enabled)
                        {
                            Print("Enabled");
                            foreach (var p in BasePlayer.activePlayerList)
                            {
                                if (!Users.ContainsKey(p.userID) && !_config.PlayerActive) continue;
                                var d = GetPlayerData(p);
                                if (d.Active) p.gameObject.AddComponent<ChatInfo>();
                            }
                        }
                        else
                        {
                            foreach (var c in Resources.FindObjectsOfTypeAll<ChatInfo>()) c.Destroy();
                            Print("Disabled");
                        }
                        SaveConfig();
                        break;
                    }
                case "auto":
                    {
                        var pa = _config.PlayerActive = !_config.PlayerActive;
                        if (arg.Args.Length > 1)
                        {
                            if (!bool.TryParse(arg.Args[1], out pa))
                            {
                                Print("ErrorBool");
                                break;
                            }
                        }
                        _config.PlayerActive = pa;

                        if (pa)
                            Print("AutoON");
                        else
                            Print("AutoOFF");
                        SaveConfig();
                        break;
                    }
                case "ui":
                    {
                        var ui = _config.UIEnabled = !_config.UIEnabled;
                        if (arg.Args.Length > 1)
                        {
                            if (!bool.TryParse(arg.Args[1], out ui))
                            {
                                Print("ErrorBool");
                                break;
                            }
                        }
                        _config.UIEnabled = ui;

                        if (ui)
                            Print("UIEnabled");
                        else
                            Print("UIDisabled");
                        SaveConfig();
                        break;
                    }
                case "msg":
                    {
                        var msg = _config.ShowSwitchMessage = !_config.ShowSwitchMessage;
                        if (arg.Args.Length > 1)
                        {
                            if (!bool.TryParse(arg.Args[1], out msg))
                            {
                                Print("ErrorBool");
                                break;
                            }
                        }
                        _config.UIEnabled = msg;

                        if (msg)
                            Print("MsgON");
                        else
                            Print("MsgOFF");
                        SaveConfig();
                        break;
                    }
            }
        }

        #endregion

        #region Methods

        private void OnUserCommand(IPlayer player, string command, string[] args)
        {
            if (!_config.Enabled || !HasPermission(player.Id, PermUse) || !isActive(player.Id) || !player.IsConnected || args == null) return;
            if (AdminChatroom && command == "a" && (args[0] == "invite" || args[0] == "kick")) return;
            var bpl = player.Object as BasePlayer;
            if (!bpl) return;

            var cmdtarget = command + " $target";
            if (!ChatType.Contains(command) && !ChatType.Contains(cmdtarget)) return;

            var target = "";
            if (ChatType.Contains(cmdtarget))
            {
                if (args.Length > 0) target = " " + args[0];
                else return;
            }
            var c = bpl.GetComponent<ChatInfo>();
            if (c == null) c = bpl.gameObject.AddComponent<ChatInfo>();
            if (c.cmd != command || c.target != target)
            {
                if (_config.ShowSwitchMessage && c.cmd == "g") MessageChat(bpl, "Switch");
                c.Switch(command, target);
            }
        }

        object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            if (!_config.Enabled) return null;

            var player = (BasePlayer)arg.Connection.player;
            if (!player || !HasPermission(player.UserIDString, PermUse) || !isActive(player.userID)) return null;

            var c = player.GetComponent<ChatInfo>();
            if (c == null || c.cmd == "g") return null;

            var message = arg.GetString(0, "text");
            rust.RunClientCommand(player, "chat.say", $"/{c.fullcmd} {message}");

            if (BC)
                return null;
            else
                return false;
        }

        object OnBetterChat(Dictionary<string, object> data)
        {
            var player = (IPlayer)data["Player"];
            if (!_config.Enabled || !HasPermission(player.Id, PermUse) || !isActive(player.Id)) return data;
            var bPlayer = Game.Rust.RustCore.FindPlayerByIdString(player.Id);
            var c = bPlayer.GetComponent<ChatInfo>();
            if (c == null || c.cmd == "g") return data;

            return false;
        }

        #endregion

        #region Localization

        void DefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>            {
                ["Enabled"] = "AutoChat is enabled.",
                ["Disabled"] = "AutoChat is disabled.",
                ["Activated"] = "You have active the autochat.",
                ["Deactivated"] = "You have deactive the autochat.",
                ["UIEnabled"] = "UI was enabled.",
                ["UIDisabled"] = "UI was disabled.",
                ["AutoON"] = "AutoChat is now auto-activated for new players.",
                ["AutoOFF"] = "AutoChat is now auto-deactivated for new players.",
                ["MsgON"] = "Message when switching chat is enabled.",
                ["MsgOFF"] = "Message when switching chat is disabled",
                ["GlobalChat"] = "You switched to the global chat.",
                ["NoPerm"] = "You don't have permission to use this command.",
                ["IsDisabled"] = "The plugin is disabled.",
                ["ErrorBool"] = "Error. Only \"true\" or \"false\".",
                ["NoPlugins"] = "The plugin was disabled because weren't found supported plugins.",
                ["ListPlugins"] = "Supported plugins: {0}{1}",
                ["Loaded"] = "Loaded plugin: {0}",
                ["Unloaded"] = "Unloaded plugin: {0}",
                ["NoConfig"] = "Could not read config file. Creating new one...",
                ["Switch"] = "You switched chat. Type \"/g\" to return to the global chat (\"/ac help\" for other commands).",
                ["Help"] = ">> AUTOCHAT HELP <<\n/ac active \"true/false:OPTIONAL\" - to active/deactive autochat.\n/g \"message:OPTIONAL\" - to send message and switch to global chat.",
                ["HelpAdmin"] = "\nAdmin Commands:\n/ac enable \"true/false:OPTIONAL\" - to enable/disable plugin.\n/ac auto \"true/false:OPTIONAL\" - to auto-active/deactive plugin for new players.\n/ac msg \"true/false:OPTIONAL\" - to enable/disable message when switching chat.",
            }, this);
        }

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        void MessageChat(BasePlayer player, string key, params object[] args)
        {
            var msg = Lang(key, player.UserIDString, args);
            PrintToChat(player, msg);
        }
        void Print(string key, params object[] args) => Puts(Lang(key, null, args));

        #endregion

        #region Utilities

        void CheckPlugins()
        {
            var list = new List<string>();
            var listPlugins = new List<Plugin>(plugins.GetAll());
            foreach (var cp in cmdPlugins)
            {
                var pl = listPlugins.Find(p => cp.Key.Equals($"{p.Title}.{p.Author}") || cp.Key.Equals(p.Title));
                if (pl)
                {
                    foreach (var cmd in cp.Value) ChatType.Add(cmd);
                    list.Add(pl.Name);
                    LoadedPlugins.Add(pl.Name);
                }
            }

            var lcus = new List<string>();
            foreach (var p in _config.CustomChat)
                if (plugins.Exists(p.Key))
                {
                    foreach (var c in p.Value) ChatType.Add(c);
                    lcus.Add(p.Key);
                    LoadedPlugins.Add(p.Key);
                }

            if (ChatType.Count == 0)
            {
                _config.Enabled = false;
                PrintWarning(Lang("NoPlugins"));
            }
            else
            {
                Print("ListPlugins", string.Join(", ", list.ToArray()), lcus.Count != 0 ? "\nCustomChat: " + string.Join(", ", lcus.ToArray()) : null);
                list.Clear();
                lcus.Clear();
            }

            if (BetterChat)
            {
                var v = Convert.ToInt32(BetterChat.Version.ToString().Split('.')[0]);
                if (v >= 5) BC = false;
            }
            else
                BC = false;
        }

        bool isActive(string id) => id.IsSteamId() && isActive(Convert.ToUInt64(id));
        bool isActive(ulong id) => Users.ContainsKey(id) && Users[id].Active;

        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);

        #endregion

        #region CUI

        static string cuiJson = @"[
        {
            ""name"": ""backAC"",
            ""parent"": ""Hud"",
            ""components"": [
              {
                ""type"": ""UnityEngine.UI.Image"",
                ""color"": ""{BackColor}""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""{AnchorMin}"",
                ""anchormax"": ""{AnchorMax}""
              }
            ]
          },
          {
            ""name"": ""lblAC"",
            ""parent"": ""backAC"",
            ""components"": [
              {
                ""text"": ""{Command}"",
                ""type"": ""UnityEngine.UI.Text"",
                ""color"": ""{TextColor}"",
                ""fontSize"": {FontSize},
                ""align"": ""MiddleCenter""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0 0"",
                ""anchormax"": ""1 1""
              }
            ]
          }
        ]";

        void AddUI(BasePlayer player, string cmd)
        {
            var backColor = Color(_config.UISettings.BackgroundColor);
            var textColor = Color(_config.UISettings.TextColor);
            var cui = cuiJson.Replace("{BackColor}", backColor)
                            .Replace("{TextColor}", textColor)
                            .Replace("{FontSize}", _config.UISettings.FontSize)
                            .Replace("{AnchorMin}", _config.UISettings.AnchorMin)
                            .Replace("{AnchorMax}", _config.UISettings.AnchorMax)
                            .Replace("{Command}", cmd);
            CuiHelper.AddUi(player, cui);
        }

        static void DestroyUI(BasePlayer player) => CuiHelper.DestroyUi(player, "backAC");

        public static string Color(string hexColor)
        {
            if (!hexColor.StartsWith("#")) return hexColor;
            if (hexColor.StartsWith("#")) hexColor = hexColor.TrimStart('#');
            int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
            int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
            int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
            return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} 1";
        }

        #endregion
    }
}
