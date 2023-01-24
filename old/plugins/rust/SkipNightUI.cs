using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("SkipNightUI", "k1lly0u", "0.1.2", ResourceId = 2506)]
    class SkipNightUI : RustPlugin
    {
        #region Fields
        [PluginReference]
        Plugin ImageLibrary;

        private List<ulong> votesReceived = new List<ulong>();

        private bool voteOpen;
        private bool isWaiting;
        private bool isILReady;
        private int timeRemaining;
        private int requiredVotes;
        private Timer voteTimer;
        private Timer timeMonitor;
        #endregion

        #region UI
        public class UI
        {
            static public CuiElementContainer CreateElementContainer(string panelName, string color, string aMin, string aMax, bool useCursor = false)
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
                        new CuiElement().Parent = "Overlay",
                        panelName
                    }
                };
                return NewElement;
            }
            static public void CreatePanel(ref CuiElementContainer container, string panel, string color, string aMin, string aMax, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    CursorEnabled = cursor
                },
                panel, CuiHelper.GetGuid());
            }
            static public void CreateLabel(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel, CuiHelper.GetGuid());

            }
            static public void LoadImage(ref CuiElementContainer container, string panel, string png, string aMin, string aMax)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent {Png = png },
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax }
                    }
                });
            }
            public static string Color(string hexColor, float alpha)
            {
                if (hexColor.StartsWith("#"))
                    hexColor = hexColor.TrimStart('#');
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }
        #endregion

        #region UI Creation
        private const string Main = "SNUIMain";
       
        private void CreateTimeUI()
        {
            UICreator uiConfig = configData.PanelTypes[configData.UIConfig];

            var element = UI.CreateElementContainer(Main, "0 0 0 0", $"{uiConfig.Size.XMin / 100} {uiConfig.Size.YMin / 100}", $"{uiConfig.Size.XMax / 100} {uiConfig.Size.YMax / 100}");
            foreach (var panel in uiConfig.PanelElements)
                UI.CreatePanel(ref element, Main, string.IsNullOrEmpty(panel.Color) ? "0 0 0 0" : UI.Color(panel.Color, panel.Alpha), $"{panel.Size.XMin / 100} {panel.Size.YMin / 100}", $"{panel.Size.XMax / 100} {panel.Size.YMax / 100}");

            foreach (var image in uiConfig.ImageElements)
            {
                string imageId = GetImage(image.URL);
                UI.LoadImage(ref element, Main, imageId, $"{image.Size.XMin / 100} {image.Size.YMin / 100}", $"{image.Size.XMax / 100} {image.Size.YMax / 100}");
            }

            foreach (var label in uiConfig.TextElements)
            {
                string text = label.Text.Text;
                if (!string.IsNullOrEmpty(label.Text.Color))
                    text = $"<color={label.Text.Color}>{text}</color>";
                UI.CreateLabel(ref element, Main, "", text, label.Text.Size, $"{label.Size.XMin / 100} {label.Size.YMin / 100}", $"{label.Size.XMax / 100} {label.Size.YMax / 100}", ParseAnchor(label.Text.Alignment));
            }

            var percentVotes = System.Convert.ToDouble((float)votesReceived.Count / (float)requiredVotes);
            var percentTime = System.Convert.ToDouble((float)timeRemaining / (float)configData.Options.Duration);
            var time = GetFormatTime();

            foreach(var bar in uiConfig.VoteProgress)
            {                
                switch (ParseType(bar.Type))
                {
                    case ProgressType.Solid:
                        {
                            var yMaxVotes = (bar.Size.XMin / 100) + (((bar.Size.XMax - bar.Size.XMin) / 100) * percentVotes);
                            UI.CreatePanel(ref element, Main, string.IsNullOrEmpty(bar.Color) ? "0 0 0 0" : UI.Color(bar.Color, bar.Alpha), $"{bar.Size.XMin / 100} {bar.Size.YMin / 100}", $"{yMaxVotes} {bar.Size.YMax / 100}");
                        }
                        break;
                    case ProgressType.Graphic:
                        {
                            var yMaxVotes = (bar.Size.XMin / 100) + (((bar.Size.XMax - bar.Size.XMin) / 100) * percentVotes);
                            string imageId = GetImage(bar.URL);
                            UI.LoadImage(ref element, Main, imageId, $"{bar.Size.XMin / 100} {bar.Size.YMin / 100}", $"{yMaxVotes} {bar.Size.YMax / 100}");
                        }
                        break;
                    case ProgressType.Text:
                        UI.CreateLabel(ref element, Main, "", bar.Text.Text.Replace("{currentAmount}", votesReceived.Count().ToString()).Replace("{requiredAmount}", requiredVotes.ToString()), bar.Text.Size, $"{bar.Size.XMin / 100} {bar.Size.YMin / 100}", $"{bar.Size.XMax / 100} {bar.Size.YMax / 100}", ParseAnchor(bar.Text.Alignment));
                        break;
                    default:
                        break;
                }
            }
            foreach (var bar in uiConfig.TimeProgress)
            {
                switch (ParseType(bar.Type))
                {
                    case ProgressType.Solid:
                        {
                            var yMaxRemaining = ((bar.Size.XMax / 100) * percentTime);
                            UI.CreatePanel(ref element, Main, string.IsNullOrEmpty(bar.Color) ? "0 0 0 0" : UI.Color(bar.Color, bar.Alpha), $"{bar.Size.XMin / 100} {bar.Size.YMin / 100}", $"{yMaxRemaining} {bar.Size.YMax / 100}");
                        }
                        break;
                    case ProgressType.Graphic:
                        {
                            var yMaxRemaining = ((bar.Size.XMax / 100) * percentTime);
                            string imageId = GetImage(bar.URL);
                            UI.LoadImage(ref element, Main, imageId, $"{bar.Size.XMin / 100} {bar.Size.YMin / 100}", $"{yMaxRemaining} {bar.Size.YMax / 100}");
                        }
                        break;
                    case ProgressType.Text:
                        if (!string.IsNullOrEmpty(bar.Text.Color))
                            time = $"<color={bar.Text.Color}>{time}</color>";
                        UI.CreateLabel(ref element, Main, "", time, bar.Text.Size, $"{bar.Size.XMin / 100} {bar.Size.YMin / 100}", $"{bar.Size.XMax / 100} {bar.Size.YMax / 100}", ParseAnchor(bar.Text.Alignment));
                        break;
                    default:
                        break;
                }
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Main);
                CuiHelper.AddUi(player, element);
            }
        }

        private void RefreshAllUI()
        {
            if (voteOpen)
                CreateTimeUI();
            else
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(player, Main);
                }
            }
        }
        #endregion

        #region Oxide Hooks
        void Loaded()
        {
            lang.RegisterMessages(Messages, this);
            permission.RegisterPermission("skipnightui.admin", this);
        }
        void OnServerInitialized()
        {
            LoadVariables();
            votesReceived = new List<ulong>();
            requiredVotes = 0;
            voteOpen = false;
            timeRemaining = 0;
            LoadImages();            
        }
        void OnPlayerDisconnected(BasePlayer player) => CuiHelper.DestroyUi(player, Main);
        void Unload()
        {
            if (voteTimer != null)
                voteTimer.Destroy();

            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, Main);
        }
        #endregion

        #region Functions
        private void OpenVote()
        {
            var required = BasePlayer.activePlayerList.Count * (configData.Options.Percentage / 100);
            if (required < 1) required = 1;
            requiredVotes = Convert.ToInt32(required);
            voteOpen = true;
            Print("commandSyn");
            VoteTimer();
        }
        private void VoteTimer()
        {
            timeRemaining = configData.Options.Duration;
            voteTimer = timer.Repeat(1, timeRemaining, () =>
            {
                RefreshAllUI();
                timeRemaining--;
                switch (timeRemaining)
                {
                    case 0:
                        TallyVotes();
                        return;
                    case 240:
                    case 180:
                    case 120:
                    case 60:
                    case 30:
                        MessageAll();
                        break;
                    default:
                        break;
                }
            });
        }
        private void MessageAll()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player != null)
                {
                    if (!AlreadyVoted(player))
                        Reply(player, "commandSyn");
                }
            }
        }
        private string GetFormatTime()
        {
            var time = timeRemaining;
            double minutes = Math.Floor((double)(time / 60));
            time -= (int)(minutes * 60);
            return string.Format("{0:00}:{1:00}", minutes, time);
        }
        private void CheckTime()
        {
            if (!voteOpen)
            {
                if (isWaiting)
                {
                    timeMonitor = timer.Once(20, () => CheckTime());
                    return;
                }

                if ((TOD_Sky.Instance.Cycle.Hour >= configData.Options.Open && TOD_Sky.Instance.Cycle.Hour < 24) || (TOD_Sky.Instance.Cycle.Hour >= 0 && TOD_Sky.Instance.Cycle.Hour < configData.Options.Set))
                    OpenVote();
                else timeMonitor = timer.Once(20, () => CheckTime());
            }
            else
            {
                if (TOD_Sky.Instance.Cycle.Hour >= configData.Options.Set && TOD_Sky.Instance.Cycle.Hour < configData.Options.Open)
                    VoteEnd(false);
            }
        }
        private void TallyVotes()
        {
            if (votesReceived.Count >= requiredVotes)
                VoteEnd(true);
            else VoteEnd(false);
        }
        private void VoteEnd(bool success)
        {
            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, Main);
            voteOpen = false;
            requiredVotes = 0;
            voteTimer.Destroy();
            votesReceived.Clear();
            timeRemaining = 0;

            if (success)
            {            
                TOD_Sky.Instance.Cycle.Hour = configData.Options.Set;
                TOD_Sky.Instance.Cycle.Day = TOD_Sky.Instance.Cycle.Day + 1;
                Print("votingSuccessful");
            }
            else Print("votingUnsuccessful");
            isWaiting = true;
            timer.In(600, () => isWaiting = false);
            CheckTime();
        }
        
        private bool AlreadyVoted(BasePlayer player) => votesReceived.Contains(player.userID);

        private void AddImage(string fileName)
        {
            var url = fileName;
            if (!url.StartsWith("http") && !url.StartsWith("www"))
                url = $"file://{Interface.Oxide.DataDirectory}{Path.DirectorySeparatorChar}SkipNightUI{Path.DirectorySeparatorChar}Images{Path.DirectorySeparatorChar}{fileName}.png";
            ImageLibrary?.Call("AddImage", url, fileName, 0);
        }
        private string GetImage(string fileName, ulong skin = 0)
        {
            string imageId = (string)ImageLibrary.Call("GetImage", fileName, skin);
            if (!string.IsNullOrEmpty(imageId))
                return imageId;
            return string.Empty;
        }
        private void LoadImages()
        {
            if (string.IsNullOrEmpty(configData.UIConfig))
            {
                PrintError("You must set the \"UI Configuration Name\" in your config. Unable to continue!");
                Interface.Oxide.UnloadPlugin(Title);
                return;
            }
            if (!configData.PanelTypes.ContainsKey(configData.UIConfig))
            {
                PrintError("Invalid \"UI Configuration Name\" set in your config. Unable to continue!");
                Interface.Oxide.UnloadPlugin(Title);
                return;
            }            

            Dictionary<string, string> newLoadOrder = new Dictionary<string, string>();
            UICreator uiConfig = configData.PanelTypes[configData.UIConfig];

            foreach (var element in uiConfig.ImageElements)
            {
                if (!string.IsNullOrEmpty(element.URL) && !newLoadOrder.ContainsKey(element.URL))
                    newLoadOrder.Add(element.URL, element.URL);
            }
            foreach (var element in uiConfig.TimeProgress)
            {
                if (!string.IsNullOrEmpty(element.URL) && !newLoadOrder.ContainsKey(element.URL))
                    newLoadOrder.Add(element.URL, element.URL);
            }
            foreach (var element in uiConfig.VoteProgress)
            {
                if (!string.IsNullOrEmpty(element.URL) && !newLoadOrder.ContainsKey(element.URL))
                    newLoadOrder.Add(element.URL, element.URL);
            }

            if (newLoadOrder.Count > 0)
            {
                if (!ImageLibrary)
                {
                    PrintError("Image Library is not installed. It is required to load the images. Unable to continue!");
                    Interface.Oxide.UnloadPlugin(Title);
                    return;
                }

                ImageLibrary.Call("ImportImageList", Title, newLoadOrder);
            }

            BeginUICreation();
        }

        private void BeginUICreation()
        {
            if (!(bool)ImageLibrary?.Call("IsReady"))
            {
                PrintWarning("Waiting for ImageLibrary to finish image processing!");
                timer.In(60, BeginUICreation);
                return;
            }
            isILReady = true;
            CheckTime();
        }

        TextAnchor ParseAnchor(string anchor)
        {
            TextAnchor textAnchor;
            try
            {
                textAnchor = (TextAnchor)Enum.Parse(typeof(TextAnchor), anchor);
            }
            catch
            {
                textAnchor = TextAnchor.MiddleCenter;
            }
            return textAnchor;
        }
        ProgressType ParseType(string type)
        {
            ProgressType textAnchor;
            try
            {
                textAnchor = (ProgressType)Enum.Parse(typeof(ProgressType), type);
            }
            catch
            {
                textAnchor = ProgressType.Solid;
            }
            return textAnchor;
        }
        #endregion

        #region ChatCommands
        [ChatCommand("voteday")]
        private void cmdVoteDay(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                if (voteOpen)
                {
                    if (!AlreadyVoted(player))
                    {
                        votesReceived.Add(player.userID);
                        Reply(player, "voteSuccess");
                        if (votesReceived.Count >= requiredVotes)
                            VoteEnd(true);
                        return;
                    }
                }
                else Reply(player, "noVote");
            }
            else
            {
                if (!permission.UserHasPermission(player.UserIDString, "skipnightui.admin")) return;
                switch (args[0].ToLower())
                {
                    case "open":
                        if (!voteOpen)
                            OpenVote();
                        else Reply(player, "alreadyOpen");
                        return;
                    case "close":
                        if (voteOpen)
                            VoteEnd(false);
                        else Reply(player, "noVote");
                        return;
                    default:
                        Reply(player, "invalidSyntax");
                        break;
                }
            }
        }
        #endregion

        #region UI Creator
        enum ProgressType { Solid, Graphic, Text }
        class UICreator
        {
            [JsonProperty(PropertyName = "Main Container Size")]
            public UISize Size { get; set; }
            [JsonProperty(PropertyName = "Panel Elements")]
            public List<UIPanel> PanelElements { get; set; }
            [JsonProperty(PropertyName = "Text Elements")]
            public List<UIText> TextElements { get; set; }
            [JsonProperty(PropertyName = "Image Elements")]
            public List<UIImage> ImageElements { get; set; }
            [JsonProperty(PropertyName = "Vote Progress Elements")]
            public List<UIProgress> VoteProgress { get; set; }
            [JsonProperty(PropertyName = "Time Progress Elements")]
            public List<UIProgress> TimeProgress { get; set; }
        }
        class UISize
        {
            [JsonProperty(PropertyName = "Horizontal Start")]
            public float XMin { get; set; }
            [JsonProperty(PropertyName = "Horizontal End")]
            public float XMax { get; set; }
            [JsonProperty(PropertyName = "Vertical Start")]
            public float YMin { get; set; }
            [JsonProperty(PropertyName = "Vertical End")]
            public float YMax { get; set; }
        }
        class UIPanel
        {
            public UISize Size { get; set; }
            [JsonProperty(PropertyName = "Background Color (Hex)")]
            public string Color { get; set; }
            [JsonProperty(PropertyName = "Background Alpha")]
            public float Alpha { get; set; }
        }
        class UIText
        {
            public UISize Size { get; set; }
            public TextComponent Text { get; set; }
        }
        class UIImage
        {
            public UISize Size { get; set; }
            [JsonProperty(PropertyName = "URL or Image filename")]
            public string URL { get; set; }
        }
        class UIProgress
        {
            public UISize Size { get; set; }
            [JsonProperty(PropertyName = "Progress Type")] // (Solid, Graphic, Text)
            public string Type { get; set; }
            [JsonProperty(PropertyName = "URL or Image filename (Graphic)")]
            public string URL { get; set; }
            [JsonProperty(PropertyName = "Bar Color (Solid)")]
            public string Color { get; set; }
            [JsonProperty(PropertyName = "Bar Alpha (Solid)")]
            public float Alpha { get; set; }
            [JsonProperty(PropertyName = "Text Options (Text)")]
            public TextComponent Text { get; set; }
        }
        class TextComponent
        {
            [JsonProperty(PropertyName = "Alignment")] // (LowerCenter, LowerLeft, LowerRight, MiddleCenter, MiddleLeft, MiddleRight, UpperCenter, UpperLeft, UpperRight)
            public string Alignment { get; set; }
            [JsonProperty(PropertyName = "Color (Hex)")]
            public string Color { get; set; }
            public string Text { get; set; }            
            [JsonProperty(PropertyName = "Size")]
            public int Size { get; set; }
        }
        #endregion

        #region Config        
        private ConfigData configData;
        class Colors
        {
            public string Primary { get; set; }
            public string Secondary { get; set; }            
        }
        class Options
        {
            [JsonProperty(PropertyName = "Required vote percentage")]
            public float Percentage { get; set; }
            [JsonProperty(PropertyName = "Time the vote will open")]
            public float Open { get; set; }
            [JsonProperty(PropertyName = "Time to set on successful vote")]
            public float Set { get; set; }
            [JsonProperty(PropertyName = "Duration the vote will be open")]
            public int Duration { get; set; }
        }
        class ConfigData
        {
            [JsonProperty(PropertyName = "UI Configuration Name")]
            public string UIConfig { get; set; }
            [JsonProperty(PropertyName = "UI Configurations")]
            public Dictionary<string, UICreator> PanelTypes { get; set; }
            [JsonProperty(PropertyName = "Message Colors")]
            public Colors Colors { get; set; }
            [JsonProperty(PropertyName = "Options")]
            public Options Options { get; set; }
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                Colors = new Colors
                {
                    Primary = "#ce422b",
                    Secondary = "#939393"
                },
                Options = new Options
                {
                    Percentage = 40f,
                    Open = 18f,
                    Set = 8f,
                    Duration = 240
                },
                PanelTypes = new Dictionary<string, UICreator>
                {
                    #region DayVoteUI
                    ["Design_1"] = new UICreator
                    {
                        Size = new UISize
                        {
                            XMin = 37.5f,
                            XMax = 62.5f,
                            YMin = 93f,
                            YMax = 98f
                        },
                        ImageElements = new List<UIImage> { },
                        PanelElements = new List<UIPanel>
                        {
                            new UIPanel
                            {
                                Alpha = 0.7f,
                                Color = "#4c4c4c",
                                Size = new UISize
                                {
                                    XMax = 100,
                                    XMin = 0,
                                    YMax = 100,
                                    YMin = 0
                                }
                            }
                        },
                        TextElements = new List<UIText>
                        {
                            new UIText
                            {
                                Size = new UISize
                                {
                                    XMin = 2,
                                    XMax = 100,
                                    YMin = 0f,
                                    YMax = 100
                                },
                                Text = new TextComponent
                                {
                                    Alignment = TextAnchor.MiddleLeft.ToString(),
                                    Color = "#ce422b",
                                    Size = 15,
                                    Text = "Skip Night"
                                }
                            }
                        },
                        TimeProgress = new List<UIProgress>
                        {
                            new UIProgress
                            {
                                Alpha = 0f,
                                Color = string.Empty,
                                Size = new UISize
                                {
                                    XMin = 80,
                                    XMax = 95,
                                    YMin = 10,
                                    YMax = 90
                                },
                                Text = new TextComponent
                                {
                                    Alignment = TextAnchor.MiddleRight.ToString(),
                                    Color = string.Empty,
                                    Size = 14,
                                    Text = string.Empty
                                },
                                Type = ProgressType.Text.ToString(),
                                URL = string.Empty
                            }
                        },
                        VoteProgress = new List<UIProgress>
                        {
                            new UIProgress
                            {
                                Alpha = 1f,
                                Color = "#EBB146",
                                Size = new UISize
                                {
                                    XMin = 25f,
                                    XMax = 80f,
                                    YMin = 15f,
                                    YMax = 85f
                                },
                                Type = ProgressType.Solid.ToString(),
                                Text = null,
                                URL = string.Empty
                            },
                            new UIProgress
                            {
                                Alpha = 0f,
                                Color = string.Empty,
                                Size = new UISize
                                {
                                    XMin = 25f,
                                    XMax = 80f,
                                    YMin = 15f,
                                    YMax = 85f
                                },
                                Type = ProgressType.Text.ToString(),
                                Text = new TextComponent
                                {
                                    Alignment = TextAnchor.MiddleCenter.ToString(),
                                    Color = string.Empty,
                                    Size = 14,
                                    Text = "{currentAmount} / {requiredAmount}"
                                },
                                URL = string.Empty
                            }
                        }
                    },
                    #endregion

                    #region GUI SkipNight
                    ["Design_2"] = new UICreator
                    {
                        Size = new UISize
                        {
                            XMin = 40,
                            XMax = 60,
                            YMin = 85,
                            YMax = 95
                        },
                        ImageElements = new List<UIImage>
                        {
                            new UIImage
                            {
                                Size = new UISize
                                {
                                    XMin =7,
                                    XMax = 13.5f,
                                    YMin = 10,
                                    YMax = 33
                                },
                                URL = "https://www.chaoscode.io/oxide/Images/timericon.png"
                            }
                        },
                        PanelElements = new List<UIPanel>
                        {
                            new UIPanel
                            {
                                Alpha = 0.85f,
                                Color = "#191919",
                                Size = new UISize
                                {
                                    XMin = 0,
                                    XMax = 100,
                                    YMin = 0,
                                    YMax = 100
                                }
                            }
                        },
                        TextElements = new List<UIText>
                        {
                            new UIText
                            {
                                Size = new UISize
                                {
                                    XMin = 20,
                                    XMax = 80,
                                    YMin = 70,
                                    YMax = 96
                                },
                                Text = new TextComponent
                                {
                                    Alignment = TextAnchor.MiddleCenter.ToString(),
                                    Color = "#C4FF00",
                                    Size = 14,
                                    Text = "Skip the Night"
                                }                                
                            }
                        },
                        TimeProgress = new List<UIProgress>
                        {
                            new UIProgress
                            {
                                Alpha = 0f,
                                Color = string.Empty,
                                Size = new UISize
                                {
                                    XMin = 20,
                                    XMax = 90,
                                    YMin = 15,
                                    YMax = 28
                                },
                                Text = null,
                                Type = ProgressType.Graphic.ToString(),
                                URL = "https://www.chaoscode.io/oxide/Images/progressbar.png"
                            }
                        },
                        VoteProgress = new List<UIProgress>
                        {
                            new UIProgress
                            {
                                Alpha = 1f,
                                Color = "#4ca5ff",
                                Size = new UISize
                                {
                                    XMin = 5,
                                    XMax = 95,
                                    YMin = 40,
                                    YMax = 68
                                },
                                Text = null,
                                Type = ProgressType.Solid.ToString(),
                                URL = string.Empty
                            },
                            new UIProgress
                            {
                                Alpha = 0f,
                                Color = string.Empty,
                                Size = new UISize
                                {
                                    XMin = 5,
                                    XMax = 95,
                                    YMin = 40,
                                    YMax = 68
                                },
                                Type = ProgressType.Text.ToString(),
                                Text = new TextComponent
                                {
                                    Alignment = TextAnchor.MiddleCenter.ToString(),
                                    Color = string.Empty,
                                    Size = 14,
                                    Text = "{currentAmount} / {requiredAmount}"
                                },
                                URL = string.Empty
                            }
                        }
                        
                    },
                    #endregion

                    #region Bar Design
                    ["Design_3"] = new UICreator
                    {
                        Size = new UISize
                        {
                            XMin = 40,
                            XMax = 60,
                            YMin = 90,
                            YMax = 98
                        },
                        ImageElements = new List<UIImage>
                        {
                            new UIImage
                            {
                                Size = new UISize
                                {
                                    XMin =75,
                                    XMax = 98f,
                                    YMin = 5,
                                    YMax = 95
                                },
                                URL = "https://images.vexels.com/media/users/3/132347/isolated/preview/be0aa6f53b4ac58a4a3612d6dc7a7854-stopwatch-timer-icon-by-vexels.png"
                            }
                        },
                        PanelElements = new List<UIPanel>
                        {
                            new UIPanel
                            {
                                Alpha = 0.85f,
                                Color = "#191919",
                                Size = new UISize
                                {
                                    XMin = 0,
                                    XMax = 72,
                                    YMin = 55,
                                    YMax = 100
                                }
                            },
                            new UIPanel
                            {
                                Alpha = 0.85f,
                                Color = "#191919",
                                Size = new UISize
                                {
                                    XMin = 73.25f,
                                    XMax = 100,
                                    YMin = 0,
                                    YMax = 100
                                }
                            },
                            new UIPanel
                            {
                                Alpha = 0.85f,
                                Color = "#191919",
                                Size = new UISize
                                {
                                    XMin = 0,
                                    XMax = 72,
                                    YMin = 0,
                                    YMax = 50
                                }
                            }
                        },
                        TextElements = new List<UIText>
                        {
                            new UIText
                            {
                                Size = new UISize
                                {
                                    XMin = 0,
                                    XMax = 72,
                                    YMin = 55,
                                    YMax = 100
                                },
                                Text = new TextComponent
                                {
                                    Alignment = TextAnchor.MiddleCenter.ToString(),
                                    Color = "#ce422b",
                                    Size = 14,
                                    Text = "Skip night by typing /voteday"
                                }
                            }
                        },
                        TimeProgress = new List<UIProgress>
                        {
                            new UIProgress
                            {
                                Alpha = 0f,
                                Color = string.Empty,
                                Size = new UISize
                                {
                                    XMin = 73,
                                    XMax = 100,
                                    YMin = 0,
                                    YMax = 100
                                },
                                Text = new TextComponent
                                {
                                    Alignment = TextAnchor.MiddleCenter.ToString(),
                                    Color = string.Empty,
                                    Size = 16,
                                    Text = string.Empty
                                },
                                Type = ProgressType.Text.ToString(),
                                URL = ""
                            }
                        },
                        VoteProgress = new List<UIProgress>
                        {
                            new UIProgress
                            {
                                Alpha = 1f,
                                Color = "#585858",
                                Size = new UISize
                                {
                                    XMin = 2,
                                    XMax = 70,
                                    YMin = 5,
                                    YMax = 45
                                },
                                Text = null,
                                Type = ProgressType.Solid.ToString(),
                                URL = string.Empty
                            },
                            new UIProgress
                            {
                                Alpha = 0f,
                                Color = string.Empty,
                                Size = new UISize
                                {
                                    XMin = 2,
                                    XMax = 70,
                                    YMin = 5,
                                    YMax = 45
                                },
                                Type = ProgressType.Text.ToString(),
                                Text = new TextComponent
                                {
                                    Alignment = TextAnchor.MiddleCenter.ToString(),
                                    Color = string.Empty,
                                    Size = 14,
                                    Text = "{currentAmount} / {requiredAmount}"
                                },
                                URL = string.Empty
                            }
                        }
                    }
                    #endregion
                },
                UIConfig = "Design_3"
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Localization
        void Reply(BasePlayer player, string langKey) => SendReply(player, msg(langKey, player.UserIDString).Replace("{main}", $"<color={configData.Colors.Primary}>").Replace("{msg}", $"<color={configData.Colors.Secondary}>").Replace("{percent}", (configData.Options.Percentage).ToString()));
        void Print(string langKey) => PrintToChat(msg(langKey).Replace("{main}", $"<color={configData.Colors.Primary}>").Replace("{msg}", $"<color={configData.Colors.Secondary}>").Replace("{percent}", (configData.Options.Percentage).ToString()));
        string msg(string key, string playerId = "") => lang.GetMessage(key, this, playerId);
        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            {"voteSuccess", "{msg}You have voted to skip night!</color>" },
            {"noVote", "{msg}There is not currently a vote open!</color>" },
            {"alreadyOpen", "{msg}The is already a vote in progress!</color>" },
            {"invalidSyntax", "{msg}Invalid Syntax! -</color> {main}/voteday open</color>{msg} || </color> {main}/voteday close</color>" },
            {"votingSuccessful", "{main}Voting was successful!</color>{msg} Skipping night</color>" } ,
            {"votingUnsuccessful", "{main}Voting was not successful!</color>{msg} Nighttime inbound</color>" },
            {"commandSyn", "{msg}Type </color>{main}/voteday</color>{msg} now if you want to skip the night!\n-- Requires </color>{main}{percent}%</color>{msg} of the players to vote</color>" },
            {"skipNight", "Skip Night" }
        };
        #endregion
    }
}