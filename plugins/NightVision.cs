
using Facepunch;
using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NightVision", "Jake_Rich", "1.3.1")]
    [Description("See at night")]

    //1.1.0: Made more performance friendly
    //1.2.0: Removed CanNetworkTo (performance)
    //1.3.0: Added Plugin API
    //1.3.1: ArrayPool.Free

    public class NightVision : RustPlugin
    {
        public static string permissionName = "nightvision.allowed";

        public Timer _timer { get; set; }

        public UILabel label { get; set; }

        public static MethodInfo SendAsSnapshotMethod;

        private void TimerLoop()
        {
            label.HideAll();

            foreach (var player in BasePlayer.activePlayerList)
            {
                UpdateEnvironmentSync(player);
            }
        }

        private void UpdateEnvironmentSync(BasePlayer player)
        {
            var data = GetPlayerData(player);
            if (data.LockTime)
            {
                if (data.ShowUI)
                {
                    label.Refresh(player);
                }
                #region Send Overridden EnvSync
                if (Net.sv.write.Start())
                {
                    Connection connection = player.net.connection;
                    connection.validate.entityUpdates = connection.validate.entityUpdates + 1;
                    BaseNetworkable.SaveInfo saveInfo = new global::BaseNetworkable.SaveInfo
                    {
                        forConnection = player.net.connection,
                        forDisk = false
                    };
                    Net.sv.write.PacketID(Message.Type.Entities);
                    Net.sv.write.UInt32(player.net.connection.validate.entityUpdates);
                    using (saveInfo.msg = Pool.Get<Entity>())
                    {
                        EnvSync.Save(saveInfo);
                        if (saveInfo.msg.baseEntity == null)
                        {
                            Debug.LogError(this + ": ToStream - no BaseEntity!?");
                        }
                        saveInfo.msg.environment.dateTime = new DateTime().AddHours(data.Time).ToBinary();
                        saveInfo.msg.environment.fog = data.Fog == -1 ? 0 : data.Fog;
                        saveInfo.msg.environment.rain = data.Rain == -1 ? 0 : data.Rain;
                        saveInfo.msg.environment.clouds = 0;
                        if (saveInfo.msg.baseNetworkable == null)
                        {
                            Debug.LogError(this + ": ToStream - no baseNetworkable!?");
                        }
                        saveInfo.msg.ToProto(Net.sv.write);
                        EnvSync.PostSave(saveInfo);
                        Net.sv.write.Send(new SendInfo(player.net.connection));
                    }
                }
                #endregion
            }
            else
            {
                //Send the EnvSync to the client directly (limitNetworking enabled)
                if (SendAsSnapshotMethod == null)
                {
                    SendAsSnapshotMethod = typeof(BaseNetworkable).GetMethod("SendAsSnapshot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                var args = ArrayPool.Get(2);
                args[0] = player.Connection;
                args[1] = false;
                SendAsSnapshotMethod.Invoke(EnvSync, args);
                ArrayPool.Free(args);
            }
        }

        private void SetupUI()
        {
            label = new UILabel(new Vector2(0.01f, 0.90f), new Vector2(0.08f, 0.99f), "", 16, "1 1 1 1", null, TextAnchor.UpperLeft);
            label.conditionalShow = delegate (BasePlayer player)
            {
                return player.IsAdmin;
            };
            label.variableText = delegate (BasePlayer player)
            {
                bool daytime = TOD_Sky.Instance.Cycle.Hour < 20 && TOD_Sky.Instance.Cycle.Hour > 6;
                float minute = (int)((TOD_Sky.Instance.Cycle.Hour - Math.Truncate(TOD_Sky.Instance.Cycle.Hour)) * 60);
                int hour = TOD_Sky.Instance.Cycle.Hour < 13 ? (int)TOD_Sky.Instance.Cycle.Hour : (int)TOD_Sky.Instance.Cycle.Hour - 12;
                if (hour == 0)
                {
                    hour = 12;
                }
                return
                $"{(daytime ? "Day" : "Night")}\n" +
                $"{hour}:{minute.ToString("00")}{(TOD_Sky.Instance.Cycle.Hour < 12 ? "am" : "pm")}"; //I hate this bracketed statment, but I won't change it so it should be fine
            };
            label.AddOutline();
        }

        #region PlayerData

        public class PlayerData
        {
            private BasePlayer _player { get; set; }
            public bool LockTime;
            public bool ShowUI;
            public float Time;
            public float Rain;
            public float Fog;

            public PlayerData(BasePlayer player)
            {
                _player = player;
            }

            public PlayerData()
            {

            }
        }

        public static PlayerData GetPlayerData(BasePlayer player)
        {
            PlayerData data;
            if (!playerData.TryGetValue(player, out data))
            {
                data = new PlayerData(player);
                playerData.Add(player, data);
            }
            return data;
        }

        public static Dictionary<BasePlayer, PlayerData> playerData { get; set; } = new Dictionary<BasePlayer, PlayerData>();

        #endregion

        #region Hooks

        public static NightVision _plugin { get; set; }
        public static EnvSync EnvSync;

        void Init()
        {
            _plugin = this;
            permission.RegisterPermission(permissionName, this);
            lang.RegisterMessages(lang_en, this);
        }

        void OnServerInitialized()
        {
            SetupUI();
            EnvSync = BaseNetworkable.serverEntities.OfType<EnvSync>().FirstOrDefault();
            EnvSync.limitNetworking = true;
            _timer = timer.Every(5f, TimerLoop);
        }

        void Unload()
        {
            label.HideAll();
            EnvSync.limitNetworking = false;
            _timer?.Destroy();
        }

        #endregion

        #region NightVision Plugin API 1.3.0

        [PluginReference("NightVision")]
        RustPlugin NightVisionRef;

        public void LockPlayerTime(BasePlayer player, float time, float fog = -1, float rain = -1)
        {
            var args = Core.ArrayPool.Get(4);
            args[0] = player;
            args[1] = time;
            args[2] = fog;
            args[3] = rain;
            NightVisionRef?.CallHook("LockPlayerTime", args);
            Core.ArrayPool.Free(args);
        }

        public void UnlockPlayerTime(BasePlayer player)
        {
            var args = Core.ArrayPool.Get(1);
            args[0] = player;
            NightVisionRef?.CallHook("UnlockPlayerTime", args);
            Core.ArrayPool.Free(args);
        }

        #endregion

        #region Plugin-API

        [HookMethod("LockPlayerTime")]
        void LockPlayerTime_PluginAPI(BasePlayer player, float time, float fog, float rain)
        {
            var data = GetPlayerData(player);
            data.LockTime = true;
            data.Time = time;
            data.Fog = fog;
            data.Rain = rain;
        }

        [HookMethod("UnlockPlayerTime")]
        void UnlockPlayerTime_PluginAPI(BasePlayer player)
        {
            var data = GetPlayerData(player);
            data.LockTime = false;
        }

        #endregion

        #region Chat Commands

        [ChatCommand("nightvision")]
        void NightVisionCommand(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName))
            {
                PrintToChat(player, string.Format(lang.GetMessage("AdminsOnly", _plugin, player.UserIDString), permissionName));
                return;
            }
            var data = GetPlayerData(player);
            data.LockTime = !data.LockTime;

            if (data.LockTime)
            {
                PrintToChat(player, lang.GetMessage("Activated", _plugin, player.UserIDString));
                data.Time = 12;
                UpdateEnvironmentSync(player);
            }
            else
            {
                PrintToChat(player, lang.GetMessage("Deactivated", _plugin, player.UserIDString));
                label.Hide(player);
                UpdateEnvironmentSync(player);
            }
        }

        #endregion

        #region Other Existing Stuff

        public static void Log(string str)
        {
            _plugin.Puts(str);
        }

        public static void Log(object obj)
        {
            _plugin.Puts(obj.ToString());
        }

        public class ConfigurationAccessor<Type> where Type : class
        {
            #region Typed Configuration Accessors

            private Type GetTypedConfigurationModel(string storageName)
            {
                return Interface.Oxide.DataFileSystem.ReadObject<Type>(storageName);
            }

            private void SaveTypedConfigurationModel(string storageName, Type storageModel)
            {
                Interface.Oxide.DataFileSystem.WriteObject(storageName, storageModel);
            }

            #endregion

            public string name { get; set; }
            public Type Instance { get; set; }

            public ConfigurationAccessor(string name)
            {
                this.name = name;
                Init();
            }

            public virtual void Init()
            {
                Reload();
            }

            public virtual void Load()
            {
                Instance = GetTypedConfigurationModel(name);
            }

            public virtual void Save()
            {
                SaveTypedConfigurationModel(name, Instance);
            }

            public virtual void Reload()
            {
                Load(); //Need to load and save to init list
                Save();
                Load();
            }
        }

        #endregion

        #region Lang API

        public Dictionary<string, string> lang_en = new Dictionary<string, string>()
        {
            {"Activated","Night vision activated"},
            {"Deactivated","Night vision deactivated"},
            {"AdminsOnly","This command is only for people with the permission \"{0}\"!"},
        };

        #endregion

        #region Jake's UI Framework

        private Dictionary<string, UICallbackComponent> UIButtonCallBacks { get; set; } = new Dictionary<string, UICallbackComponent>();

        void OnButtonClick(ConsoleSystem.Arg arg)
        {
            UICallbackComponent button;
            if (UIButtonCallBacks.TryGetValue(arg.cmd.Name, out button))
            {
                button.InvokeCallback(arg);
                return;
            }
            Puts("Unknown button command: {0}", arg.cmd.Name);
        }

        public class UIElement : UIBaseElement
        {
            public CuiElement Element { get; protected set; }
            public UIOutline Outline { get; set; }
            public CuiRectTransformComponent transform { get; protected set; }
            public float FadeOut
            {
                get
                {
                    return Element == null ? _fadeOut : Element.FadeOut;
                }
                set
                {
                    if (Element != null)
                    {
                        Element.FadeOut = value;
                    }
                    _fadeOut = value;
                }
            }
            private float _fadeOut = 0f;

            public string Name { get { return Element.Name; } }

            public UIElement(UIBaseElement parent = null) : base(parent)
            {

            }

            public UIElement(Vector2 position, float width, float height, UIBaseElement parent = null) : this(position, new Vector2(position.x + width, position.y + height), parent)
            {

            }

            public UIElement(Vector2 min, Vector2 max, UIBaseElement parent = null) : base(min, max, parent)
            {
                transform = new CuiRectTransformComponent();
                Element = new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = this._parent == null ? this.Parent : this._parent.Parent,
                    Components =
                        {
                            transform,
                        },
                    FadeOut = _fadeOut,
                };
                UpdatePlacement();

                Init();
            }

            public void AddOutline(string color = "0 0 0 1", string distance = "1 -1")
            {
                Outline = new UIOutline(color, distance);
                Element.Components.Add(Outline.component);
            }

            public virtual void Init()
            {

            }

            public override void Show(BasePlayer player, bool children = true)
            {
                if (this is UIElement)
                {
                    if (!CanShow(player))
                    {
                        _shouldShow = false;
                        return;
                    }
                    _shouldShow = true;

                    if (conditionalSize != null)
                    {
                        Vector2 returnSize = conditionalSize.Invoke(player);
                        if (returnSize != null)
                        {
                            SetSize(returnSize.x, returnSize.y);
                        }
                    }

                    if (conditionalPosition != null)
                    {
                        Vector2 returnPos = conditionalPosition.Invoke(player);
                        if (returnPos != null)
                        {
                            SetPosition(returnPos.x, returnPos.y);
                        }
                    }
                }
                if (AddPlayer(player))
                {
                    SafeAddUi(player, Element);
                }
                base.Show(player, children);
            }

            public override void Hide(BasePlayer player, bool children = true)
            {
                base.Hide(player, children);
                if (RemovePlayer(player))
                {
                    SafeDestroyUi(player, Element);
                }
            }

            public override void UpdatePlacement()
            {
                base.UpdatePlacement();
                if (transform != null)
                {
                    transform.AnchorMin = $"{globalPosition.x} {globalPosition.y}";
                    transform.AnchorMax = $"{globalPosition.x + globalSize.x} {globalPosition.y + globalSize.y}";
                }
                //RefreshAll();
            }

            public void SetPositionAndSize(CuiRectTransformComponent trans)
            {
                transform.AnchorMin = trans.AnchorMin;
                transform.AnchorMax = trans.AnchorMax;

                //_plugin.Puts($"POSITION [{transform.AnchorMin},{transform.AnchorMax}]");

                RefreshAll();
            }

            public void SetParent(UIElement element)
            {
                Element.Parent = element.Element.Name;
                UpdatePlacement();
            }

            public void SetParent(string parent)
            {
                Element.Parent = parent;
                Parent = parent;
            }

        }

        public class UIButton : UIElement, UICallbackComponent
        {
            public CuiButtonComponent buttonComponent { get; private set; }
            public CuiTextComponent textComponent { get; private set; }
            public UILabel Label { get; set; }
            private string _textColor { get; set; }
            private string _buttonText { get; set; }
            public string Text { set { textComponent.Text = value; } }
            public Func<BasePlayer, string> variableText { get; set; }

            public Action<ConsoleSystem.Arg> onCallback;

            private int _fontSize;

            public UIButton(Vector2 min = default(Vector2), Vector2 max = default(Vector2), string buttonText = "", string buttonColor = "0 0 0 0.85", string textColor = "1 1 1 1", int fontSize = 15, UIBaseElement parent = null) : base(min, max, parent)
            {
                buttonComponent = new CuiButtonComponent();

                _fontSize = fontSize;
                _textColor = textColor;
                _buttonText = buttonText;

                buttonComponent.Command = CuiHelper.GetGuid();
                buttonComponent.Color = buttonColor;

                Element.Components.Insert(0, buttonComponent);

                _plugin.cmd.AddConsoleCommand(buttonComponent.Command, _plugin, "OnButtonClick");

                _plugin.UIButtonCallBacks[buttonComponent.Command] = this;

                Label = new UILabel(new Vector2(0, 0), new Vector2(1, 1), fontSize: _fontSize, parent: this);

                textComponent = Label.text;

                Label.text.Align = TextAnchor.MiddleCenter;
                Label.text.Color = _textColor;
                Label.Text = _buttonText;
                Label.text.FontSize = _fontSize;

            }

            public UIButton(Vector2 position, float width, float height, string buttonText = "", string buttonColor = "0 0 0 0.85", string textColor = "1 1 1 1", int fontSize = 15, UIBaseElement parent = null) : this(position, new Vector2(position.x + width, position.y + height), buttonText, buttonColor, textColor, fontSize, parent)
            {

            }

            public override void Init()
            {
                base.Init();

            }

            public void AddChatCommand(string fullCommand)
            {
                if (fullCommand == null)
                {
                    return;
                }
                onCallback += (arg) =>
                {
                    _plugin.rust.RunClientCommand(arg.Player(), $"chat.say \"/{fullCommand}\"");
                };
            }

            public void AddCallback(Action<BasePlayer> callback)
            {
                if (callback == null)
                {
                    return;
                }
                onCallback += (args) => { callback(args.Player()); };
            }

            public override void Show(BasePlayer player, bool children = true)
            {
                if (variableText != null)
                {
                    try
                    {
                        Text = variableText.Invoke(player);
                    }
                    catch (Exception ex)
                    {
                        _plugin.Puts($"UIButton.variableText failed!\n{ex}");
                    }
                }
                base.Show(player, children);
            }

            public void InvokeCallback(ConsoleSystem.Arg args)
            {
                if (onCallback == null)
                {
                    return;
                }
                onCallback.Invoke(args);
            }
        }

        public class UIBackgroundText : UIPanel
        {
            public UILabel Label;

            public UIBackgroundText(Vector2 min = default(Vector2), Vector2 max = default(Vector2), UIBaseElement parent = null, string backgroundColor = "0 0 0 0.85", string labelText = "", int fontSize = 12, string fontColor = "1 1 1 1", TextAnchor alignment = TextAnchor.MiddleCenter) : base(min, max, parent)
            {
                Label = new UILabel(new Vector2(0, 0), new Vector2(1, 1), labelText, fontSize, fontColor, parent, alignment);
            }
        }

        public class UILabel : UIElement
        {
            public CuiTextComponent text { get; private set; }

            public UILabel(Vector2 min = default(Vector2), Vector2 max = default(Vector2), string labelText = "", int fontSize = 12, string fontColor = "1 1 1 1", UIBaseElement parent = null, TextAnchor alignment = TextAnchor.MiddleCenter) : base(min, max, parent)
            {

                if (min == Vector2.zero && max == Vector2.zero)
                {
                    max = Vector2.one;
                }

                text = new CuiTextComponent();

                text.Text = labelText;
                ColorString = fontColor;
                text.Align = alignment;
                text.FontSize = fontSize;

                Element.Components.Insert(0, text);
            }

            public UILabel(Vector2 min, float width, float height, string labelText = "", int fontSize = 12, string fontColor = "1 1 1 1", UIBaseElement parent = null, TextAnchor alignment = TextAnchor.MiddleCenter) : this(min, new Vector2(min.x + width, min.y + height), labelText, fontSize, fontColor, parent, alignment)
            {

            }

            public string Text { set { if (value == null) { text.Text = ""; } else { text.Text = value; } text.Text = value; } } //I love single line statments
            public TextAnchor Allign { set { text.Align = value; } }
            public Color Color { set { text.Color = value.ToString(); } }
            public string ColorString { set { text.Color = value.Replace("f", ""); } } //Prevent me from breaking UI with 0.1f instead of 0.1

            public Func<BasePlayer, string> variableText { get; set; }
            public Func<BasePlayer, string> variableFontColor { get; set; }

            public override void Show(BasePlayer player, bool children = true)
            {
                if (variableText != null)
                {
                    try
                    {
                        Text = variableText.Invoke(player);
                    }
                    catch (Exception ex)
                    {
                        _plugin.Puts($"UILabel.variableText failed!\n{ex}");
                    }
                }
                if (variableFontColor != null)
                {
                    try
                    {
                        ColorString = variableFontColor.Invoke(player);
                    }
                    catch (Exception ex)
                    {
                        _plugin.Puts($"UILabel.variableFontColor failed!\n{ex}");
                    }
                }
                base.Show(player, children);
            }

            public override void Init()
            {
                base.Init();

                if (_parent != null)
                {
                    if (_parent is UIButton)
                    {
                        Element.Parent = (_parent as UIButton).Name;
                        transform.AnchorMin = $"{localPosition.x} {localPosition.y}";
                        transform.AnchorMax = $"{localPosition.x + localSize.x} {localPosition.y + localSize.y}";
                    }
                }
            }

        }

        public class UIImageBase : UIElement
        {
            public UIImageBase(Vector2 min, Vector2 max, UIBaseElement parent) : base(min, max, parent)
            {
            }

            private CuiNeedsCursorComponent needsCursor { get; set; }

            private bool requiresFocus { get; set; }

            public bool CursorEnabled
            {
                get
                {
                    return requiresFocus;
                }
                set
                {
                    if (value)
                    {
                        needsCursor = new CuiNeedsCursorComponent();
                        Element.Components.Add(needsCursor);
                    }
                    else
                    {
                        Element.Components.Remove(needsCursor);
                    }

                    requiresFocus = value;
                }
            }
        }

        public class UIPanel : UIImageBase
        {
            private CuiImageComponent panel;

            public string Color { get { return panel.Color; } set { panel.Color = value; } }
            public string Material { get { return panel.Material; } set { panel.Material = value; } }

            public Func<BasePlayer, string> variableColor { get; set; }

            public UIPanel(Vector2 min, Vector2 max, UIBaseElement parent = null, string color = "0 0 0 0.85") : base(min, max, parent)
            {
                panel = new CuiImageComponent
                {
                    Color = color,
                };

                Element.Components.Insert(0, panel);
            }

            public UIPanel(Vector2 position, float width, float height, UIBaseElement parent = null, string color = "0 0 0 .85") : this(position, new Vector2(position.x + width, position.y + height), parent, color)
            {

            }

            public override void Show(BasePlayer player, bool children = true)
            {
                if (variableColor != null)
                {
                    try
                    {
                        panel.Color = variableColor.Invoke(player);
                    }
                    catch (Exception ex)
                    {
                        _plugin.Puts($"UIPanel.variableColor failed!\n{ex}");
                    }
                }
                base.Show(player, children);
            }
        }

        public class UIButtonContainer : UIPanel
        {
            private IEnumerable<UIButtonConfiguration> _buttonConfiguration;
            private Vector2 _position;
            private float _width;
            private float _height;
            private string _title;
            private string _panelColor;
            private bool _horizontalButtons;
            private float _paddingPercentage;
            private int _titleSize;
            private int _buttonFontSize;


            const float TITLE_PERCENTAGE = 0.20f;

            private float _paddingAmount;
            private bool _hasTitle;

            public UIButtonContainer(IEnumerable<UIButtonConfiguration> buttonConfiguration, string panelBgColor, Vector2 position, float width, float height, float paddingPercentage = 0.05f, string title = "", int titleSize = 30, int buttonFontSize = 15, bool horizontalButtons = true, UIBaseElement parent = null) : base(position, width, height, parent, panelBgColor)
            {
                _buttonConfiguration = buttonConfiguration;
                _position = position;
                _width = width;
                _height = height;
                _title = title;
                _titleSize = titleSize;
                _panelColor = panelBgColor;
                _horizontalButtons = horizontalButtons;
                _paddingPercentage = paddingPercentage;
                _buttonFontSize = buttonFontSize;

                Init();
            }

            private void Init()
            {
                _paddingAmount = _paddingPercentage / _buttonConfiguration.Count();

                var firstButtonPosition = new Vector2(_paddingAmount, _paddingAmount);
                var titleHeight = TITLE_PERCENTAGE;

                if (!string.IsNullOrEmpty(_title))
                {
                    _hasTitle = true;

                    var titlePanel = new UIPanel(new Vector2(0, 1f - titleHeight), 1f, titleHeight, this);
                    var titleLabel = new UILabel(Vector2.zero, Vector2.one, _title, fontSize: _titleSize, parent: titlePanel);
                }

                var buttonHeight = (1f - (_paddingAmount * 2) - (_hasTitle ? titleHeight : 0) - (_paddingAmount * (_buttonConfiguration.Count() - 1))) / (_horizontalButtons ? _buttonConfiguration.Count() : 1);
                var buttonWidth = _horizontalButtons ? (1f - (_paddingAmount * 2)) : ((1f - (_paddingAmount * 2) - (_paddingAmount * (_buttonConfiguration.Count() - 1))) / _buttonConfiguration.Count());

                for (var buttonId = 0; buttonId < _buttonConfiguration.Count(); buttonId++)
                {
                    //Fuck this shit is confusing, null conditional operators aren't that great
                    var buttonConfig = _buttonConfiguration.ElementAt(buttonId);
                    var button = new UIButton(
                        _horizontalButtons ?
                            new Vector2(firstButtonPosition.x, firstButtonPosition.y + ((buttonHeight + _paddingAmount) * buttonId + _paddingAmount)) :
                            new Vector2(firstButtonPosition.x + ((buttonWidth + _paddingAmount) * buttonId + _paddingAmount), firstButtonPosition.y + (_paddingAmount) * 2),
                        buttonWidth - (_horizontalButtons ? 0 : _paddingAmount * 2),
                        buttonHeight - (!_horizontalButtons ? _paddingAmount * 4 : 0),
                        buttonText: buttonConfig.ButtonName, buttonColor: buttonConfig.ButtonColor, fontSize: _buttonFontSize, parent: this);
                    /*
                    if (!_stackedButtons)
                    {
                        button.SetPosition(
                            firstButtonPosition.x + ((buttonWidth + _paddingAmount) * buttonId + _paddingAmount),
                            firstButtonPosition.y + (_paddingAmount) * 2);
                    }
                    else
                    {
                        button.SetPosition(
                            firstButtonPosition.x,
                            firstButtonPosition.y + ((buttonHeight + _paddingAmount) * buttonId + _paddingAmount));
                    }

                    button.SetSize(
                        buttonWidth - (_stackedButtons ? 0 : _paddingAmount * 2),
                        buttonHeight - (_stackedButtons ? _paddingAmount * 2 : 0));*/

                    _plugin.Puts($"Button At {button.localPosition} {button.localSize}");
                    button.AddCallback(buttonConfig.callback);
                    button.AddChatCommand(buttonConfig.ButtonCommand);
                }
            }
        }

        public class UIPagedElements : UIBaseElement
        {
            private UIButton nextPage { get; set; }
            private UIButton prevPage { get; set; }
            private float _elementHeight { get; set; }
            private float _elementSpacing { get; set; }
            private int _elementWidth { get; set; }
            private Dictionary<BasePlayer, int> ElementIndex = new Dictionary<BasePlayer, int>();

            private List<UIBaseElement> Elements = new List<UIBaseElement>();

            public UIPagedElements(Vector2 min, Vector2 max, float elementHeight, float elementSpacing, UIBaseElement parent = null, int elementWidth = 1) : base(min, max, parent)
            {
                _elementHeight = elementHeight;
                _elementSpacing = elementSpacing;
                _elementWidth = elementWidth;
            }

            public void NewElement(UIBaseElement element)
            {
                SetParent(this);
                Elements.Add(element);
            }

            public void NewElements(IEnumerable<UIBaseElement> elements)
            {
                foreach (var element in elements)
                {
                    SetParent(this);
                }
                Elements.AddRange(elements);
            }

            public override void Show(BasePlayer player, bool showChildren = true)
            {
                foreach (var element in Elements)
                {
                    element.Hide(player);
                }
                int elements = Mathf.FloorToInt((1f - (_elementSpacing * 2)) / (_elementHeight + _elementSpacing));
                int index = 0;
                ElementIndex.TryGetValue(player, out index);
                for (int i = index; i < elements; i++)
                {
                    _plugin.Puts($"Index is {index}");
                    if (i >= Elements.Count)
                    {
                        break;
                    }
                    var element = Elements[i];
                    element.SetPosition(0f, 1f - (_elementHeight * (i + 1)) - (_elementWidth * (i + 1)));
                    element.SetSize(1f, _elementHeight);
                    element.Show(player);
                    _plugin.Puts($"Element at {element.localPosition} {element.localSize}");
                }
                base.Show(player, showChildren);
            }

            public override void Hide(BasePlayer player, bool hideChildren = true)
            {
                base.Hide(player, hideChildren);
                foreach (var element in Elements)
                {
                    element.Hide(player);
                }
            }
        }

        public class UIButtonConfiguration
        {
            public string ButtonName { get; set; }
            public string ButtonCommand { get; set; }
            public string ButtonColor { get; set; }
            public Action<BasePlayer> callback { get; set; }
        }

        public class UIImage : UIImageBase
        {
            public CuiImageComponent Image { get; private set; }
            public string Sprite { get { return Image.Sprite; } set { Image.Sprite = value; } }
            public string Material { get { return Image.Material; } set { Image.Material = value; } }
            public string PNG { get { return Image.Png; } set { Image.Png = value; } }

            public UIImage(Vector2 min, Vector2 max, UIBaseElement parent = null) : base(min, max, parent)
            {
                Image = new CuiImageComponent();
                Element.Components.Insert(0, Image);
            }

            public UIImage(Vector2 position, float width, float height, UIBaseElement parent = null) : this(position, new Vector2(position.x + width, position.y + height), parent)
            {

            }

            public Func<BasePlayer, string> variableSprite { get; set; }
            public Func<BasePlayer, string> variablePNG { get; set; }

            public override void Show(BasePlayer player, bool children = true)
            {
                if (variableSprite != null)
                {
                    try
                    {
                        Image.Sprite = variableSprite.Invoke(player);
                    }
                    catch (Exception ex)
                    {
                        _plugin.Puts($"UIImage.variableSprite failed!\n{ex}");
                    }
                }
                if (variablePNG != null)
                {
                    try
                    {
                        Image.Png = variablePNG.Invoke(player);
                    }
                    catch (Exception ex)
                    {
                        _plugin.Puts($"UIImage.variablePNG failed!\n{ex}");
                    }
                }
                base.Show(player, children);
            }
        }

        public class UIRawImage : UIImageBase
        {
            public CuiRawImageComponent Image { get; private set; }

            public string Material { get { return Image.Material; } set { Image.Material = value; } }
            public string Sprite { get { return Image.Sprite; } set { Image.Sprite = value; } }
            public string PNG { get { return Image.Png; } set { Image.Png = value; } }
            public string Color { get { return Image.Color; } set { Image.Color = value; } }

            public UIRawImage(Vector2 position, float width, float height, UIBaseElement parent = null, string url = null) : this(position, new Vector2(position.x + width, position.y + height), parent, url)
            {

            }

            public UIRawImage(Vector2 min, Vector2 max, UIBaseElement parent = null, string url = null) : base(min, max, parent)
            {
                Image = new CuiRawImageComponent()
                {
                    Url = url,
                    Sprite = "assets/content/textures/generic/fulltransparent.tga"
                };

                Element.Components.Insert(0, Image);
            }

            public Func<BasePlayer, string> variablePNG { get; set; }

            public Func<BasePlayer, string> variableURL { get; set; }

            public Func<BasePlayer, string> variablePNGURL { get; set; }

            public override void Show(BasePlayer player, bool children = true)
            {
                if (variablePNGURL != null)
                {
                    string url = variablePNGURL.Invoke(player);
                    if (string.IsNullOrEmpty(url))
                    {
                        Image.Png = null;
                        Image.Url = null;
                    }
                    ulong num;
                    if (ulong.TryParse(url, out num))
                    {
                        Image.Png = url;
                        Image.Url = null;
                    }
                    else
                    {
                        Image.Png = null;
                        Image.Url = url;
                    }
                }
                else
                {
                    if (variablePNG != null)
                    {
                        Image.Png = variablePNG.Invoke(player);
                        if (string.IsNullOrEmpty(Image.Png))
                        {
                            Image.Png = null;
                        }
                    }
                    if (variableURL != null)
                    {
                        Image.Url = variableURL.Invoke(player);
                        if (string.IsNullOrEmpty(Image.Url))
                        {
                            Image.Url = null;
                        }
                    }
                }

                base.Show(player, children);
            }
        }

        public class UIBaseElement
        {
            public Vector2 localPosition { get; set; } = new Vector2();
            public Vector2 localSize { get; set; } = new Vector2();
            public Vector2 globalSize { get; set; } = new Vector2();
            public Vector2 globalPosition { get; set; } = new Vector2();
            public HashSet<BasePlayer> players { get; set; } = new HashSet<BasePlayer>();
            public UIBaseElement _parent { get; set; }
            public HashSet<UIBaseElement> children { get; set; } = new HashSet<UIBaseElement>();
            public Vector2 min { get { return localPosition; } }
            public Vector2 max { get { return localPosition + localSize; } }
            public string Parent { get; set; } = "Hud.Menu";
            public bool _shouldShow = true;

            public Func<BasePlayer, bool> conditionalShow { get; set; }
            public Func<BasePlayer, Vector2> conditionalSize { get; set; }
            public Func<BasePlayer, Vector2> conditionalPosition { get; set; }

            public UIBaseElement(UIBaseElement parent = null)
            {
                this._parent = parent;
            }

            public UIBaseElement(Vector2 min, Vector2 max, UIBaseElement parent = null) : this(parent)
            {
                localPosition = min;
                localSize = max - min;
                SetParent(parent);
                UpdatePlacement();
            }

            public void AddElement(UIBaseElement element)
            {
                if (element == this)
                {
                    _plugin.Puts("[UI FRAMEWORK] WARNING: AddElement() trying to add self as parent!");
                    return;
                }
                if (!children.Contains(element))
                {
                    children.Add(element);
                }
            }

            public void RemoveElement(UIBaseElement element)
            {
                children.Remove(element);
            }

            public void Refresh(BasePlayer player)
            {
                Hide(player);
                Show(player);
            }

            public bool AddPlayer(BasePlayer player)
            {
                if (!players.Contains(player))
                {
                    players.Add(player);
                    return true;
                }

                foreach (var child in children)
                {
                    child.AddPlayer(player);
                }

                return false;
            }

            public bool RemovePlayer(BasePlayer player)
            {
                return players.Remove(player);
            }

            public void Show(IEnumerable<BasePlayer> players)
            {
                foreach (BasePlayer player in players.ToList())
                {
                    Show(player);
                }
            }

            public virtual void SetParent(UIBaseElement parent)
            {
                if (parent != null && this != parent)
                {
                    parent.AddElement(this);
                }
                _parent = parent;
            }

            public virtual void Hide(BasePlayer player, bool hideChildren = true)
            {
                foreach (var child in children)
                {
                    child.Hide(player, hideChildren);
                }

                if (GetType() == typeof(UIBaseElement))
                {
                    RemovePlayer(player);
                }
            }

            public void Hide(IEnumerable<BasePlayer> players)
            {
                foreach (BasePlayer player in players.ToList())
                {
                    Hide(player);
                }
            }

            public virtual bool Toggle(BasePlayer player)
            {
                if (players.Contains(player))
                {
                    Hide(player);
                    return false;
                }
                Show(player);
                return true;
            }

            public virtual void Show(BasePlayer player, bool showChildren = true)
            {
                if (player == null || player.gameObject == null)
                {
                    players.Remove(player);
                    return;
                }

                if (GetType() == typeof(UIBaseElement))
                {
                    if (!CanShow(player))
                    {
                        _shouldShow = false;
                        return;
                    }
                    _shouldShow = true;

                    if (conditionalSize != null)
                    {
                        Vector2 returnSize = conditionalSize.Invoke(player);
                        if (returnSize != null)
                        {
                            SetSize(returnSize.x, returnSize.y);
                        }
                    }

                    if (conditionalPosition != null)
                    {
                        Vector2 returnPos = conditionalPosition.Invoke(player);
                        if (returnPos != null)
                        {
                            SetPosition(returnPos.x, returnPos.y);
                        }
                    }

                    AddPlayer(player);
                }

                foreach (var child in children)
                {
                    child.Show(player, showChildren);
                }
            }

            public bool CanShow(BasePlayer player)
            {
                if (_parent != null)
                {
                    if (!_parent.CanShow(player))
                    {
                        return false;
                    }
                }
                if (conditionalShow == null)
                {
                    return true;
                }
                if (player == null)
                {
                    return false;
                }
                if (player.gameObject == null)
                {
                    return false;
                }
                if (!player.IsConnected)
                {
                    return false;
                }
                try
                {
                    if (conditionalShow.Invoke(player))
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _plugin.Puts($"UIBaseElement.conditionShow failed!\n{ex}");
                }
                return false;
            }

            public void HideAll()
            {
                foreach (BasePlayer player in players.ToList())
                {
                    if (player == null || player.gameObject == null)
                    {
                        players.Remove(player);
                        continue;
                    }
                    Hide(player);
                }
            }

            public void RefreshAll()
            {
                foreach (BasePlayer player in players.ToList())
                {
                    if (player == null || player.gameObject == null)
                    {
                        players.Remove(player);
                        continue;
                    }
                    Refresh(player);
                }
            }

            public void SafeAddUi(BasePlayer player, CuiElement element)
            {
                try
                {
                    //_plugin.Puts(JsonConvert.SerializeObject(element));
                    List<CuiElement> elements = new List<CuiElement>();
                    elements.Add(element);
                    CuiHelper.AddUi(player, elements);
                }
                catch (Exception ex)
                {

                }
            }

            public void SafeDestroyUi(BasePlayer player, CuiElement element)
            {
                try
                {
                    //_plugin.Puts($"Deleting {element.Name} to {player.userID}");
                    CuiHelper.DestroyUi(player, element.Name);
                }
                catch (Exception ex)
                {

                }
            }

            public void SetSize(float x, float y)
            {
                localSize = new Vector2(x, y);
                UpdatePlacement();
            }

            public void SetPosition(float x, float y)
            {
                localPosition = new Vector2(x, y);
                UpdatePlacement();
            }

            public virtual void UpdatePlacement()
            {
                if (_parent == null)
                {
                    globalSize = localSize;
                    globalPosition = localPosition;
                }
                else
                {
                    globalSize = Vector2.Scale(_parent.globalSize, localSize);
                    globalPosition = _parent.globalPosition + Vector2.Scale(_parent.globalSize, localPosition);
                }

                foreach (var child in children)
                {
                    child.UpdatePlacement();
                }
            }
        }

        public class UIReflectionElement : UIPanel
        {
            private object config { get; set; }
            private object _target { get { return Field.GetValue(config); } set { Field.SetValue(config, value); } }
            private FieldInfo Field { get; set; }

            private UILabel Text { get; set; }
            private UIInputField InputField { get; set; }
            private UIButton editButton { get; set; }

            private bool EditBox { get; set; } = false;

            public UIReflectionElement(Vector2 min, Vector2 max, FieldInfo field, object configuration, UIBaseElement parent = null) : base(min, max, parent)
            {
                config = configuration;
                Field = field;

                Text = new UILabel(new Vector2(0.05f, 0f), new Vector2(0.4f, 1f), "Amount", parent: this, alignment: TextAnchor.MiddleLeft);
                Text.variableText = delegate (BasePlayer player)
                {
                    return GetVisualText();
                };
                Text.AddOutline("0 0 0 1");

                //editButton = new UIButton(new Vector2(0.0125f, 0.15f), new Vector2(0.0375f, 0.85f), "","1 1 1 1", "1 1 1 1", 12, this);
                editButton = new UIButton(new Vector2(0.80f, 0.15f), new Vector2(0.90f, 0.85f), "Edit", "0 0 0 1", "1 1 1 1", 12, this);
                editButton.AddCallback((player) =>
                {
                    EditBox = true;
                    InputField.Refresh(player);
                });
                editButton.AddOutline("1 1 1 1", "0.75 -0.75");

                InputField = new UIInputField(new Vector2(0.45f, 0f), new Vector2(0.60f, 1f), this, "", TextAnchor.MiddleCenter);
                InputField.AddCallback((player, text) =>
                {
                    //TODO: Check if player has permissions to edit config values
                    if (String.IsNullOrEmpty(text))
                    {
                        return;
                    }
                    EditBox = false;
                    AssignValue(text);
                    InputField.InputField.Text = _target.ToString();
                    Text.Refresh(player);
                    InputField.Refresh(player);
                });
                InputField.AddOutline("1 1 1 1", "0.75 -0.75");
                InputField.conditionalShow = delegate (BasePlayer player)
                {
                    return EditBox;
                };
            }

            public override void Show(BasePlayer player, bool children = true)
            {

                base.Show(player, children);
            }

            public string GetVisualText()
            {
                if (_target == null)
                {
                    return $"{Field.FieldType.Name} = <color=#3B8AD6FF>NULL</color>";
                }
                string elementText = ((_target is IEnumerable && !(_target is string)) ? $" Count : {0}" : "");
                string valueText = (IsValueType(_target) ? $" = <color=#3B8AD6FF>{_target.ToString()}</color>" : "");
                if (_target is string)
                {
                    if (string.IsNullOrEmpty(_target as string))
                    {
                        valueText = $" = <color=#D69D85FF>\'\' \'\'</color>";
                    }
                    else
                    {
                        valueText = $" = <color=#D69D85FF>\'\'{_target}\'\'</color>";
                    }
                }
                return $"{Field.Name.Replace("<", "").Replace(">", "").Replace("k__BackingField", "")}{elementText}{valueText}";
                //return $"<color=#4EC8B0FF>{Field.FieldType.Name}</color> {Field.Name.Replace("<","").Replace(">","").Replace("k__BackingField","")}{elementText}{valueText}";
            }

            public void AssignValue(string text)
            {
                if (_target is string)
                {
                    _target = text;
                }
                else if (_target is int)
                {
                    int val = 0;
                    if (int.TryParse(text, out val))
                    {
                        _target = val;
                    }
                }
                else if (_target is uint)
                {
                    uint val = 0;
                    if (uint.TryParse(text, out val))
                    {
                        _target = val;
                    }
                }
                else if (_target is float)
                {
                    float val = 0;
                    if (float.TryParse(text, out val))
                    {
                        _target = val;
                    }
                }
            }
        }

        public class UIGridDisplay
        {
            public UIGridDisplay(Vector2 min, Vector2 max, int width, int height, float paddingX, float paddingY)
            {

            }
        }

        public static bool IsValueType(object obj)
        {
            return obj.GetType().IsValueType || obj is string;
        }

        public class ObjectMemoryInfo
        {
            public string name { get; set; }
            public int memoryUsed { get; set; }
            public int elements { get; set; }
            public object _target { get; set; }
            private int currentLayer { get; set; } = 0;
            public ObjectMemoryInfo _parent { get; set; }
            private bool _autoExpand { get; set; }

            public List<ObjectMemoryInfo> children { get; set; } = new List<ObjectMemoryInfo>();
            public List<MethodInfo> methods { get; set; } = new List<MethodInfo>();


            public ObjectMemoryInfo(object targetObject, int layers, string variableName = "", ObjectMemoryInfo parent = null, bool autoExpand = false)
            {
                _autoExpand = autoExpand;
                _parent = parent;
                name = variableName;
                name = name.Replace("<", "");
                name = name.Replace(">", "");
                name = name.Replace("k__BackingField", "");
                currentLayer = layers - 1;
                _target = targetObject;
                SetupObject();
                if (autoExpand)
                {
                    Expand();
                }
            }

            public void Expand()
            {
                CalculateSubObjects();
            }

            public void SetupObject()
            {
                #region Elements

                if (_target is IEnumerable)
                {
                    elements = GetCount();
                }
                if (_target is HashSet<object>)
                {
                    elements = (_target as HashSet<object>).Count;
                }

                #endregion

                #region Memory Usage
                var Type = _target?.GetType();
                if (Type == null)
                {
                    return;
                }
                if (Type == typeof(int))
                {
                    memoryUsed = 4;
                }
                else if (Type == typeof(string))
                {
                    memoryUsed = (_target as string).Length;
                }
                else if (Type == typeof(BaseNetworkable))
                {
                    memoryUsed = 8;
                }
                else if (_target is IDictionary)
                {
                    memoryUsed = elements * 16;
                }
                else if (_target is IList)
                {
                    memoryUsed = elements * 8;
                }
                else if (Type == typeof(int))
                {
                    memoryUsed = 4;
                }
                foreach (var child in children)
                {
                    memoryUsed += child.memoryUsed;
                }
                #endregion


                #region Methods

                foreach (var method in Type.GetMethods())
                {
                    if (method?.GetParameters().Length != 0)
                    {
                        continue;
                    }
                    methods.Add(method);
                }
                #endregion
            }
            private int GetCount()
            {
                int? c = (_target as IEnumerable).Cast<object>()?.Count();
                if (c != null)
                {
                    return (int)c;
                }
                return 0;
            }

            public string GetInfo()
            {
                return (_target is IEnumerable) ? $"Count : {elements}" : GetMemoryUsage();
            }

            public string GetVisualText()
            {
                if (_target == null)
                {
                    return $"{name} = <color=#3B8AD6FF>NULL</color>";
                }
                string elementText = ((_target is IEnumerable && !(_target is string)) ? $" Count : {elements}" : "");
                string valueText = (IsValueType(_target) ? $" = <color=#3B8AD6FF>{_target.ToString()}</color>" : "");
                return $"<color=#4EC8B0FF>{GetTypeName(_target.GetType())}</color> {name}{elementText}{valueText}";
            }

            public static string GetMethodText(MethodInfo info)
            {
                return $"{(info.IsPublic ? "<color=#3B8AD6FF>public " : "<color=#3B8AD6FF>private ")}{(info.IsVirtual ? "virtual</color> " : "</color>")}{$"<color=#4EC8B0FF>{GetTypeName(info.ReturnType)}</color> "}{info.Name}()";
            }

            private static string GetTypeName(Type type)
            {
                if (type == null)
                {
                    return "";
                }
                string generic = type.IsGenericType ? $"<{string.Join(",", type.GetGenericArguments().Select(x => GetTypeName(x)).ToArray())}>" : "";
                string name = type.Name;
                if (name.Contains("`"))
                {
                    name = name.Remove(name.IndexOf('`', 2));
                }
                return $"{name}{generic}";
            }

            private string GetMemoryUsage()
            {
                if (memoryUsed > 1000000000)
                {
                    return $"{Math.Round((double)memoryUsed / 1000000000, 2)}GB";
                }
                if (memoryUsed > 1000000)
                {
                    return $"{Math.Round((double)memoryUsed / 1000000, 2)}MB";
                }
                if (memoryUsed > 1000)
                {
                    return $"{Math.Round((double)memoryUsed / 1000, 2)}KB";
                }
                return $"{memoryUsed}B";
            }

            public void CalculateSubObjects()
            {
                children.Clear();
                try
                {
                    if (currentLayer < 0)
                    {
                        return;
                    }
                    if (_target == null)
                    {
                        return;
                    }
                    var Type = _target.GetType();
                    if (Type == null)
                    {
                        return;
                    }
                    if (_target is string) //No need to expand these
                    {
                        return;
                    }
                    if (_target is IEnumerable)
                    {
                        int index = 0;
                        var objects = (_target as IEnumerable).Cast<object>();
                        if (objects == null)
                        {
                            return;
                        }
                        foreach (var item in objects)
                        {
                            children.Add(new ObjectMemoryInfo(item, currentLayer, index.ToString(), this, _autoExpand));
                            index++;
                        }
                    }
                    else
                    {
                        foreach (var field in Type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static))
                        {
                            object target = field.GetValue(_target);
                            if (!CheckParents())
                            {
                                continue;
                            }
                            children.Add(new ObjectMemoryInfo(target, currentLayer, field.Name, this));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _plugin.Puts(ex.ToString());
                }
            }

            public bool CheckParents()
            {
                ObjectMemoryInfo parent = _parent;
                while (parent != null)
                {
                    if (parent._target == _target)
                    {
                        return false;
                    }
                    parent = parent._parent;
                }
                return true;
            }

            public List<string> GetOutput(int layer = 0, bool justLists = false)
            {
                List<string> returnValue = new List<string>();
                string padding = new string('\t', layer);
                if (_target != null)
                {
                    if (_target is IEnumerable || !justLists || children.Count != 0)
                    {
                        returnValue.Add(padding + $"{_target.GetType().Name} {name} {GetMemoryUsage()}");
                    }
                }
                if (children.Count > 0)
                {
                    returnValue.Add(padding + "{");
                    foreach (var child in children)
                    {
                        returnValue.AddRange(child.GetOutput(layer + 1, justLists));
                    }
                    returnValue.Add(padding + "}");
                }
                return returnValue;
            }

            public string PrintOutput(bool justLists = false)
            {
                return string.Join(System.Environment.NewLine, GetOutput(0, justLists).ToArray());
            }
        }

        public class UICheckbox : UIButton
        {
            public UICheckbox(Vector2 min, Vector2 max, UIBaseElement parent = null) : base(min, max, parent: parent)
            {

            }
        }

        public class UIOutline
        {
            public CuiOutlineComponent component;

            public string Color { get { return _color; } set { _color = value; UpdateComponent(); } }
            public string Distance { get { return _distance; } set { _distance = value; UpdateComponent(); } }

            private string _color = "0 0 0 1";
            private string _distance = "0.25 0.25";

            public UIOutline()
            {

            }

            public UIOutline(string color, string distance)
            {
                _color = color;
                _distance = distance;
                UpdateComponent();
            }

            private void UpdateComponent()
            {
                if (component == null)
                {
                    component = new CuiOutlineComponent();
                }
                component.Color = _color;
                component.Distance = _distance;
            }
        }

        public interface UICallbackComponent
        {
            void InvokeCallback(ConsoleSystem.Arg args);

        }

        public class UIInputField : UIPanel, UICallbackComponent
        {
            public CuiInputFieldComponent InputField { get; set; }

            public Action<ConsoleSystem.Arg> onCallback;

            public UIInputField(Vector2 min, Vector2 max, UIBaseElement parent, string defaultText = "Enter Text Here", TextAnchor align = TextAnchor.MiddleLeft, int fontSize = 12, string panelColor = "0 0 0 0.85", string textColor = "1 1 1 1", bool password = false, int charLimit = 100) : base(min, max, parent, panelColor)
            {
                var input = new UIInput_Raw(Vector2.zero, Vector2.one, this, defaultText, align, fontSize, textColor, password, charLimit);

                InputField = input.InputField;

                _plugin.cmd.AddConsoleCommand(InputField.Command, _plugin, "OnButtonClick");

                _plugin.UIButtonCallBacks[InputField.Command] = this;
            }

            public void AddCallback(Action<BasePlayer, string> callback)
            {
                if (callback == null)
                {
                    return;
                }
                onCallback += (args) => { callback(args.Player(), string.Join(" ", args.Args)); };
            }

            public void InvokeCallback(ConsoleSystem.Arg args)
            {
                if (onCallback == null)
                {
                    return;
                }
                onCallback.Invoke(args);
            }
        }

        public class UIInput_Raw : UIElement
        {
            public CuiInputFieldComponent InputField { get; set; }

            public UIInput_Raw(Vector2 min, Vector2 max, UIBaseElement parent, string defaultText = "Enter Text Here", TextAnchor align = TextAnchor.MiddleLeft, int fontSize = 12, string textColor = "1 1 1 1", bool password = false, int charLimit = 100) : base(min, max, parent)
            {
                InputField = new CuiInputFieldComponent()
                {
                    Align = align,
                    CharsLimit = charLimit,
                    Color = textColor,
                    FontSize = fontSize,
                    IsPassword = password,
                    Text = defaultText,
                    Command = CuiHelper.GetGuid(),
                };

                Element.Components.Insert(0, InputField);
            }
        }

        #endregion
    }
}


