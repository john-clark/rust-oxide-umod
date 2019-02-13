//Requires: ImageLibrary

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Oxide.Core;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("SimpleLogo", "Sami37", "1.2.6")]
    [Description("Place your own logo to your player screen.")]
    public class SimpleLogo : RustPlugin
    {
        #region config
        [PluginReference]
        ImageLibrary ImageLibrary;

        private string Perm = "simplelogo.display", NoDisplay = "simplelogo.nodisplay";
        static string dataDirectory = $"file://{Interface.Oxide.DataDirectory}{Path.DirectorySeparatorChar}unitycore{Path.DirectorySeparatorChar}Images{Path.DirectorySeparatorChar}";
        List<object> Url_List = new List<object>();
        private int _currentlySelected, _intervals;
        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadConfig();
        }

        string ListToString<T>(List<T> list, int first = 0, string seperator = ", ") => string.Join(seperator, (from val in list select val.ToString()).Skip(first).ToArray());
        void SetConfig(params object[] args) { List<string> stringArgs = (from arg in args select arg.ToString()).ToList(); stringArgs.RemoveAt(args.Length - 1); if (Config.Get(stringArgs.ToArray()) == null) Config.Set(args); }
        T GetConfig<T>(T defaultVal, params object[] args) { List<string> stringArgs = (from arg in args select arg.ToString()).ToList(); if (Config.Get(stringArgs.ToArray()) == null) { PrintError($"The plugin failed to read something from the config: {ListToString(stringArgs, 0, "/")}{Environment.NewLine}Please reload the plugin and see if this message is still showing. If so, please post this into the support thread of this plugin."); return defaultVal; } return (T)System.Convert.ChangeType(Config.Get(stringArgs.ToArray()), typeof(T)); }

        private string GetImage(string shortname) => (string)ImageLibrary.GetImage(shortname, 0);

        private void AddImage(string shortname)
        {
            if (ImageLibrary == null || !ImageLibrary.IsLoaded)
            {
                NextTick(() => AddImage(shortname));
            }
            else
            {
                string url = shortname;
                if (!url.StartsWith("http") && !url.StartsWith("www") && !url.StartsWith("file://"))
                    url = $"{dataDirectory}{shortname}.png";
                ImageLibrary.AddImage(url, shortname, 0);
            }
        }

        void LoadConfig()
        {
            List<object> list_url = new List<object> { "http://i.imgur.com/KVmbhyB.png" };
            SetConfig("UI", "GUIAnchorMin", "0.01 0.02");
            SetConfig("UI", "GUIAnchorMax", "0.15 0.1");
            SetConfig("UI", "BackgroundMainColor", "0 0 0 0");
            SetConfig("UI", "BackgroundMainURL", list_url);
            SetConfig("UI", "IntervalBetweenImage", 30);

            SaveConfig();

            _intervals = GetConfig(30, "UI", "IntervalBetweenImage");
            Url_List = (List<object>)Config["UI", "BackgroundMainURL"];
            foreach (var url in Url_List)
            {
                AddImage(url.ToString());
            }
        }

        #endregion

        #region data_init

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                GUIDestroy(player);
            }

        }
        #endregion

        private CuiElement CreateImage(string panelName)
        {
            var element = new CuiElement();
            var url = GetImage(Url_List[_currentlySelected].ToString());
            var color = Config["UI", "BackgroundMainColor"].ToString();
            var image = new CuiRawImageComponent
            {
                Png = url
            };

            var rectTransform = new CuiRectTransformComponent
            {
                AnchorMin = "0 0",
                AnchorMax = "1 1"
            };
            element.Components.Add(image);
            element.Components.Add(rectTransform);
            element.Name = CuiHelper.GetGuid();
            element.Parent = panelName;
            return element;
        }

        void GUIDestroy(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "containerSimpleUI");
        }

        void CreateUi(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, Perm) && !permission.UserHasPermission(player.UserIDString, NoDisplay))
            {
                var panel = new CuiElementContainer
                {
                    {
                        new CuiPanel
                        {
                            Image =
                            {
                                Color = Config["UI", "BackgroundMainColor"].ToString()
                            },
                            RectTransform =
                            {
                                AnchorMin = Config["UI", "GUIAnchorMin"].ToString(),
                                AnchorMax = Config["UI", "GUIAnchorMax"].ToString()
                            },
                            CursorEnabled = false
                        },
                        "Hud", "containerSimpleUI"
                    }
                };
                var backgroundImageWin = CreateImage("containerSimpleUI");
                panel.Add(backgroundImageWin);
                CuiHelper.AddUi(player, panel);
            }
        }

        void RefreshUI()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                GUIDestroy(player);
                CreateUi(player);
            }
            timer.In(_intervals, () =>
            {
                if (_currentlySelected >= Url_List.Count)
                    _currentlySelected = 0;
                RefreshUI();
                _currentlySelected += 1;
            });
        }

        void OnServerInitialized()
        {
            LoadConfig();
            NextTick(RefreshUI);
            permission.RegisterPermission(Perm, this);
            permission.RegisterPermission(NoDisplay, this);
        }
    }
}