using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Reflection;
using System;
using System.Collections;
using Oxide.Core;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("VisualDebug", "Jake_Rich", "1.0.2")]
    [Description("A visual object explorer, to browse rust plugins")]

    public class VisualDebug : RustPlugin
    {
        public MemoryTableUIManager UI { get; set; }

        public static VisualDebug _plugin { get; set; }

        void Init()
        {
            _plugin = this;
            UI = new MemoryTableUIManager();
        }

        void Unload()
        {
            UI.Destroy();
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            if (UI._player == player)
            {
                UI.Destroy();
            }
        }

        public Dictionary<string, string> LangAPI = new Dictionary<string, string>()
        {
            { "AdminCommandWarning", "This command is for admins only." },

        };

        void LoadDefaultMessages()
        {
            lang.RegisterMessages(LangAPI, this);
        }

        [ChatCommand("visualdbg")]
        void VisualDebugging(BasePlayer player)
        {
            if (!player.IsAdmin)
            {
                PrintToChat(player, lang.GetMessage("AdminCommandWarning", this, player.UserIDString));
                return;
            }

            if (UI._player == null)
            {
                UI.ShowPlayer(player);
            }
            else
            {
                UI.HidePlayer(player);
            }
        }

        public static bool IsValueType(object obj)
        {
            if (obj == null)
            {
                return true;
            }
            if (obj.GetType() == null)
            {
                return true;
            }
            return (obj is ValueType || obj is string) && !obj.GetType().IsGenericType && (obj.GetType().Namespace == null ? true : obj.GetType().Namespace.Contains("System"));
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
                string valueText = (IsValueType(_target) ? $" = <color=#3B8AD6FF>{_target?.ToString()}</color>" : "");
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
                string generic = type.IsGenericType ? $"<{string.Join(",",type.GetGenericArguments().Select(x=>GetTypeName(x)).ToArray())}>" : "";
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
                return string.Join(Environment.NewLine, GetOutput(0, justLists).ToArray());
            }
        }

        #region Jakes UI Framework - Old (Could be replaced later, but works for this plugin)

        private Dictionary<string, UIButton> UIButtonCallBacks { get; set; } = new Dictionary<string, UIButton>();

        void OnButtonClick(ConsoleSystem.Arg arg)
        {
            UIButton button;
            if (UIButtonCallBacks.TryGetValue(arg.cmd.Name, out button))
            {
                button.OnClicked(arg);
                return;
            }
            Puts("Unknown button command: {0}", arg.cmd.Name);
        }

        public class UIElement
        {
            public CuiElement Element { get; protected set; }
            public CuiRectTransformComponent transform { get; protected set; }
            public HashSet<BasePlayer> players { get; set; } = new HashSet<BasePlayer>();
            public Vector2 position { get; set; } = new Vector2();
            public Vector2 size { get; set; } = new Vector2();
            private VisualDebug _plugin;

            protected UIElement(UIContainer container)
            {
                transform = new CuiRectTransformComponent();
                Element = new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = "Hud",
                    Components =
                        {
                            transform
                        }
                };

                container.Elements.Add(this);
            }

            public void SetParent(UIElement element)
            {
                Element.Parent = element.Element.Name;
            }

            public void Refresh(BasePlayer player)
            {
                Hide(player);
                Show(player);
            }

            private bool AddPlayer(BasePlayer player)
            {
                if (!players.Contains(player))
                {
                    players.Add(player);
                    return true;
                }

                return false;
            }

            private bool RemovePlayer(BasePlayer player)
            {
                return players.Remove(player);
            }

            public virtual void Show(BasePlayer player)
            {
                if (AddPlayer(player))
                {
                    SafeAddUi(player, Element);
                }
            }

            public void Show(List<BasePlayer> players)
            {
                foreach (BasePlayer player in players)
                {
                    Show(player);
                }
            }

            public void Show(HashSet<BasePlayer> players)
            {
                foreach (BasePlayer player in players)
                {
                    Show(player);
                }
            }

            public virtual void Hide(BasePlayer player)
            {
                if (RemovePlayer(player))
                {
                    SafeDestroyUi(player, Element);
                }
            }

            public void HideAll()
            {
                foreach (BasePlayer player in players.ToList())
                {
                    Hide(player);
                }
            }

            public void RefreshAll()
            {
                foreach (BasePlayer player in players.ToList())
                {
                    Refresh(player);
                }
            }

            private void SafeAddUi(BasePlayer player, CuiElement element)
            {
                try
                {
                    //_plugin.Puts($"Adding {element.Name} to {player.userID}");
                    List<CuiElement> elements = new List<CuiElement>();
                    elements.Add(element);
                    CuiHelper.AddUi(player, elements);
                }
                catch (Exception ex)
                {

                }
            }

            private void SafeDestroyUi(BasePlayer player, CuiElement element)
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
                size = new Vector2(x, y);
                UpdatePlacement();
            }

            public void SetPosition(float x, float y)
            {
                position = new Vector2(x, y);
                UpdatePlacement();
            }

            public void UpdatePlacement()
            {
                transform.AnchorMin = $"{position.x} {position.y}";
                transform.AnchorMax = $"{position.x + size.x} {position.y + size.y}";

                //_plugin.Puts($"POSITION [{transform.AnchorMin},{transform.AnchorMax}]");
                RefreshAll();
            }


            public void SetPositionAndSize(CuiRectTransformComponent trans)
            {
                transform.AnchorMin = trans.AnchorMin;
                transform.AnchorMax = trans.AnchorMax;

                //_plugin.Puts($"POSITION [{transform.AnchorMin},{transform.AnchorMax}]");

                RefreshAll();
            }
        }

        public class UIButton : UIElement
        {
            public CuiButtonComponent buttonComponent { get; private set; }
            public CuiTextComponent textComponent { get; private set; }
            private UILabel label { get; set; }

            private int _fontSize;

            public Action<ConsoleSystem.Arg> onClicked;

            public UIButton(UIContainer container, string buttonText = "", string buttonColor = "0 0 0 0.85", string textColor = "1 1 1 1", int fontSize = 15) : base(container)
            {
                _fontSize = fontSize;

                var button = new CuiButton();

                buttonComponent = button.Button;
                textComponent = button.Text;

                buttonComponent.Command = CuiHelper.GetGuid();
                buttonComponent.Color = buttonColor;

                textComponent.Text = buttonText;
                textComponent.FontSize = _fontSize;
                textComponent.Align = TextAnchor.MiddleCenter;

                Element.Components.Insert(0, buttonComponent);

                _plugin.cmd.AddConsoleCommand(buttonComponent.Command, _plugin, "OnButtonClick");

                _plugin.UIButtonCallBacks[buttonComponent.Command] = this;

                label = new UILabel(container, buttonText, parent: Element.Name, fontSize: _fontSize);

                label.SetPosition(0, 0);
                label.SetSize(1f, 1f);
                label.text.Align = TextAnchor.MiddleCenter;
                label.text.Color = textColor;

                textComponent = label.text;
            }

            public virtual void OnClicked(ConsoleSystem.Arg args)
            {
                onClicked.Invoke(args);
            }

            public void AddChatCommand(string fullCommand)
            {
                if (fullCommand == null)
                {
                    return;
                }
                /*
                List<string> split = fullCommand.Split(' ').ToList();
                string command = split[0];
                split.RemoveAt(0); //Split = command args now*/
                onClicked += (arg) =>
                {
                    _plugin.rust.RunClientCommand(arg.Player(), $"chat.say \"/{fullCommand}\"");
                    //plugin.Puts($"Calling chat command {command} {string.Join(" ",split.ToArray())}");
                    //Need to call chat command somehow here
                };
            }

            public void AddCallback(Action<BasePlayer> callback)
            {
                if (callback == null)
                {
                    return;
                }
                onClicked += (args) => { callback(args.Player()); };
            }

            public override void Hide(BasePlayer player)
            {
                base.Hide(player);
                //No way to remove console commands
            }
        }

        public class UILabel : UIElement
        {
            public CuiTextComponent text { get; private set; }

            public UILabel(UIContainer container, string labelText, int fontSize = 12, string fontColor = "1 1 1 1", TextAnchor alignment = TextAnchor.MiddleCenter, string parent = "") : base(container)
            {

                text = new CuiTextComponent();

                text.Text = labelText;
                text.Color = fontColor;
                text.Align = alignment;
                text.FontSize = fontSize;

                if (!string.IsNullOrEmpty(parent))
                {
                    Element.Parent = parent;
                }

                Element.Components.Insert(0, text);
            }
        }

        public class UIPanel : UIElement
        {
            private CuiImageComponent panel;
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

            public UIPanel(UIContainer container, Vector2 position, float width, float height, string color = "0 0 0 .85", string pngImage = "") : base(container)
            {
                panel = new CuiImageComponent
                {
                    Color = color
                };

                this.SetPosition(position.x, position.y);
                this.SetSize(width, height);

                Element.Components.Insert(0, panel);
            }
        }

        public class UIButtonContainer : UIContainer
        {
            private IEnumerable<UIButtonConfiguration> _buttonConfiguration;
            private Vector2 _position;
            private float _width;
            private float _height;
            private string _title;
            private string _panelColor;
            private bool _stackedButtons;
            private float _paddingPercentage;
            private int _titleSize;
            private int _buttonFontSize;


            const float TITLE_PERCENTAGE = 0.20f;

            private float _paddingAmount;
            private bool _hasTitle;

            public UIButtonContainer(IEnumerable<UIButtonConfiguration> buttonConfiguration, string panelBgColor, Vector2 position, float width, float height, float paddingPercentage = 0.05f, string title = "", int titleSize = 30, int buttonFontSize = 15, bool stackedButtons = true)
            {
                _buttonConfiguration = buttonConfiguration;
                _position = position;
                _width = width;
                _height = height;
                _title = title;
                _titleSize = titleSize;
                _panelColor = panelBgColor;
                _stackedButtons = stackedButtons;
                _paddingPercentage = paddingPercentage;
                _buttonFontSize = buttonFontSize;

                Init();
            }

            private void Init()
            {
                var panel = new UIPanel(this, new Vector2(_position.x, _position.y), _width, _height, _panelColor);

                _paddingAmount = (_stackedButtons ? _height : _width) * _paddingPercentage / _buttonConfiguration.Count();

                var firstButtonPosition = new Vector2(_position.x + _paddingAmount, _position.y + _paddingAmount);
                var titleHeight = TITLE_PERCENTAGE * _height;

                if (!string.IsNullOrEmpty(_title))
                {
                    _hasTitle = true;

                    var titlePanel = new UIPanel(this, new Vector2(_position.x, _position.y + _height - titleHeight), _width, titleHeight);
                    var titleLabel = new UILabel(this, _title, fontSize: _titleSize, parent: titlePanel.Element.Name);
                }

                var buttonHeight = (_height - (_paddingAmount * 2) - (_hasTitle ? titleHeight : 0) - (_paddingAmount * (_buttonConfiguration.Count() - 1))) / (_stackedButtons ? _buttonConfiguration.Count() : 1);
                var buttonWidth = _stackedButtons
                    ? (_width - (_paddingAmount * 2))
                    : ((_width - (_paddingAmount * 2) - (_paddingAmount * (_buttonConfiguration.Count() - 1))) / _buttonConfiguration.Count());

                _plugin.Puts($"ButtonHeight {buttonHeight} ButtonWidth {buttonWidth}");
                for (var buttonId = 0; buttonId < _buttonConfiguration.Count(); buttonId++)
                {
                    var buttonConfig = _buttonConfiguration.ElementAt(buttonId);
                    var button = new UIButton(this, buttonText: buttonConfig.ButtonName, buttonColor: buttonConfig.ButtonColor, fontSize: _buttonFontSize);

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
                        buttonHeight - (_stackedButtons ? _paddingAmount * 2 : 0));

                    button.AddCallback(buttonConfig.callback);
                    button.AddChatCommand(buttonConfig.ButtonCommand);
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

        public class UIImage : UIPanel
        {
            public CuiImageComponent Image { get; private set; }

            public UIImage(UIContainer container, Vector2 position, float width, float height) : base(container, position, width, height)
            {
                Image = new CuiImageComponent();
                Element.Components.Insert(0, Image);
            }
        }

        public class UIRawImage : UIElement
        {
            public CuiRawImageComponent Image { get; private set; }

            public UIRawImage(UIContainer container, Vector2 position, float width, float height, string png) : base(container)
            {
                var panel = new UIPanel(container, position, width, height);

                Element.Parent = panel.Element.Name;

                Image = new CuiRawImageComponent()
                {
                    Png = png,
                    Sprite = "assets/content/textures/generic/fulltransparent.tga"
                };

                Element.Components.Insert(0, Image);
            }
        }

        public class UIContainer
        {
            public List<UIContainer> ChildContainers { get; private set; } = new List<UIContainer>();
            public List<UIElement> Elements { get; private set; } = new List<UIElement>();

            private HashSet<BasePlayer> _players { get; set; } = new HashSet<BasePlayer>();

            public UIContainer()
            {
            }

            private void AddPlayer(BasePlayer player)
            {
                if (!_players.Contains(player))
                    _players.Add(player);
            }

            public void Show(BasePlayer player)
            {
                AddPlayer(player);

                foreach (UIElement element in Elements)
                {
                    element.Show(player);
                }

                foreach (UIContainer container in ChildContainers)
                {
                    container.Show(player);
                }
            }

            public void RemoveElements<T>(IEnumerable<T> elementsToRemove) where T : UIElement
            {
                foreach (var element in elementsToRemove)
                {
                    if (Elements.Contains(element))
                    {
                        element.HideAll();
                        Elements.Remove(element);
                    }
                }
            }

            public void Hide(BasePlayer player)
            {
                foreach (UIElement element in Elements)
                {
                    element.Hide(player);
                }

                foreach (UIContainer container in ChildContainers)
                {
                    container.Hide(player);
                }
            }

            public void HideAll()
            {
                foreach (BasePlayer player in _players)
                {
                    Hide(player);
                }
            }

            public void AddElement(UIElement element)
            {
                Elements.Add(element);
            }

            public void AddChildContainer(UIContainer container)
            {
                ChildContainers.Add(container);
            }
        }

        #endregion

        public const int objectsPerScreen = 30;

        public class MemoryObjectInfoUI : UIContainer
        {
            public UIButton expand { get; set; }
            private UIPanel background { get; set; }
            private UILabel label { get; set; }
            public ObjectMemoryInfo memoryObject { get; set; }

            public MemoryObjectInfoUI(ObjectMemoryInfo obj, Vector2 pos, MemoryTableUIManager manager, bool parent = true)
            {
                memoryObject = obj;
                background = new UIPanel(this, pos, 1f - pos.x - 0.05f, (0.90f / objectsPerScreen));
                AddElement(background);

                label = new UILabel(this, obj.GetVisualText(), alignment: TextAnchor.MiddleLeft);
                label.SetSize(1f, 1f);
                label.SetPosition(0.0f, 0f);
                label.SetParent(background);
                AddElement(label);

                if (!IsValueType(memoryObject._target))
                {
                    expand = new UIButton(this, buttonColor: parent ? "1 0 0 1" : "0 1 0 1", fontSize: 10, textColor: "1 1 1 1");
                    expand.AddCallback((arg) =>
                    {
                        if (parent)
                        {
                            manager.Shrink(this);
                        }
                        else
                        {
                            manager.Expand(this);
                        }

                    });
                    expand.SetSize(0.005f, 0.45f);
                    expand.SetPosition(0.0f, 0.5f - expand.size.y / 2);
                    expand.SetParent(background);
                    //expand.textComponent.Text = parent ? "-" : "+";
                    AddElement(expand);
                    label.SetPosition(label.position.x + expand.size.x + 0.005f, label.position.y);
                }

                Show(manager._player);
            }

            public MemoryObjectInfoUI(MethodInfo method, Vector2 pos, MemoryTableUIManager manager, ObjectMemoryInfo obj)
            {
                background = new UIPanel(this, pos, 1f - pos.x - 0.05f, (0.90f / objectsPerScreen));
                AddElement(background);

                label = new UILabel(this, ObjectMemoryInfo.GetMethodText(method), alignment: TextAnchor.MiddleLeft);
                label.SetSize(1f, 1f);
                label.SetPosition(0.0f, 0f);
                label.SetParent(background);
                AddElement(label);

                expand = new UIButton(this, buttonColor: "0 0 1 1", fontSize: 10, textColor: "1 1 1 1");
                expand.AddCallback((arg) =>
                {
                    if (method.ReturnType != null)
                    {
                        if (method.ReturnType.IsValueType || method.ReturnType == typeof(string))
                        {
                            _plugin.PrintToChat(manager._player, method.Invoke(obj._target, null)?.ToString());
                        }
                        else //When clicking on a function that returns a object to explore
                        {
                            var objectReturn = method.Invoke(obj._target, null);
                            if (objectReturn != null) //Explore blue function
                            {
                                var rootObject = new ObjectMemoryInfo(objectReturn, int.MaxValue, objectReturn.GetType().Name, obj);
                                manager.ShowObject(rootObject);
                            }
                        }
                    }
                    else
                    {
                        _plugin.PrintToChat($"Executed {ObjectMemoryInfo.GetMethodText(method)}");
                    }
                
                });
                expand.SetSize(0.005f, 0.45f);
                expand.SetPosition(0.0f, 0.5f - expand.size.y / 2);
                expand.SetParent(background);
                AddElement(expand);
                label.SetPosition(label.position.x + expand.size.x + 0.005f, label.position.y);

                Show(manager._player);
            }

        }

        public class MemoryTableUIManager : UIContainer
        {
            public ObjectMemoryInfo rootObject { get; set; }

            public MemoryObjectInfoUI currentObject { get; set; }

            public List<MemoryObjectInfoUI> shownObjects { get; set; } = new List<MemoryObjectInfoUI>();

            private UIButton nextPage { get; set; }
            private UIButton prevPage { get; set; }

            public BasePlayer _player { get; set; }

            private int currentIndex { get; set; }

            public MemoryTableUIManager()
            {

                nextPage = new UIButton(this, "Next");
                nextPage.SetPosition(0.51f, 0f);
                nextPage.SetSize(0.09f, 0.03f);
                nextPage.AddCallback((player) => { NextPage(); });
                //AddElement(nextPage);

                prevPage = new UIButton(this, "Prev");
                prevPage.SetPosition(0.4f, 0f);
                prevPage.SetSize(0.09f, 0.03f);
                prevPage.AddCallback((player) => { PrevPage(); });
                //AddElement(prevPage);
            }

            public void Destroy() 
            {
                foreach(var item in shownObjects)
                {
                    item.HideAll();
                }
                currentObject.HideAll();
                nextPage?.HideAll();
                prevPage?.HideAll();
                HideAll();
            }

            public void ShowPlayer(BasePlayer player)
            {
                _player = player;
                rootObject = new ObjectMemoryInfo(Oxide.Core.Interface.Oxide.RootPluginManager.GetPlugins(), int.MaxValue, "Plugins");
                ShowObject(rootObject);
            }

            public void HidePlayer(BasePlayer player)
            {
                _player = null;
                Destroy();
            }

            public void LinqQuery(BasePlayer player, string cmd)
            {
                string[] commands = cmd.Split('.');
            }

            public void Expand(MemoryObjectInfoUI obj)
            {
                if (obj.memoryObject != null)
                {
                    ShowObject(obj.memoryObject);
                }
            }

            public void Shrink(MemoryObjectInfoUI obj)
            {
                if (currentObject.memoryObject == rootObject)
                {
                    return;
                }
                if (obj.memoryObject._parent != null)
                {
                    ShowObject(obj.memoryObject._parent);
                }
            }

            public void NextPage()
            {
                currentIndex += objectsPerScreen;
                ShowObject(currentObject.memoryObject, true);
            }

            public void PrevPage()
            {
                currentIndex = Mathf.Clamp(currentIndex -= objectsPerScreen, 0, currentIndex);
                ShowObject(currentObject.memoryObject, true);
            }

            private void HideCurrent()
            {
                currentObject?.HideAll();
                foreach (var item in shownObjects)
                {
                    item.HideAll();
                }
            }

            public void ShowObject(ObjectMemoryInfo obj, bool changePage = false)
            {
                HideCurrent();
                if (!changePage)
                {
                    currentIndex = 0;
                }
                int i = 0;
                currentObject = new MemoryObjectInfoUI(obj, new Vector2(0.25f, 0.95f), this);
                obj.Expand();
                if (obj.children.Count == 0)
                {
                    _plugin.Puts("No children!");
                    _plugin.Puts(IsValueType(obj._target).ToString());
                    return;
                }
                bool nextPageNeeded = false;
                Vector2 pos = new Vector2(0.25f, 0.90f);
                foreach (var item in obj.children)
                {
                    //_plugin.Puts($"i: {i}, currentIndex: {currentIndex}");
                    if (i < currentIndex)
                    {
                        i++;
                        continue;
                    }
                    if (i >= currentIndex + objectsPerScreen && (obj.children.Count != objectsPerScreen))
                    {
                        nextPageNeeded = true;
                        break;
                    }
                    shownObjects.Add(new MemoryObjectInfoUI(item, pos, this, false));
                    pos.y -= (0.90f / objectsPerScreen);
                    i++;
                }

                foreach(var method in obj.methods)
                {
                    if (i < currentIndex)
                    {
                        i++;
                        continue;
                    }
                    if (i >= currentIndex + objectsPerScreen)
                    {
                        nextPageNeeded = true;
                        break;
                    }
                    shownObjects.Add(new MemoryObjectInfoUI(method, pos, this, obj));
                    pos.y -= (0.90f / objectsPerScreen);
                    i++;
                }

                nextPage.HideAll();
                if (nextPageNeeded)
                {
                    nextPage.Show(_player);
                }
                prevPage.HideAll();
                if (currentIndex > 0)
                {
                    prevPage.Show(_player);
                }
            }
        }
    }

}