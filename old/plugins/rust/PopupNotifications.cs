using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Popup Notifications", "emu / k1lly0u", "0.2.0", ResourceId = 1252)]
    public class PopupNotifications : RustPlugin
    {
        private static PopupNotifications ins;       
        const string popupPanel = "PopupNotification";

        private string panelColor;
        private string buttonColor;
        private string font;
        private int fontSize;

        #region Oxide Hooks
        private void OnServerInitialized()
        {
            lang.RegisterMessages(Messages, this);
            permission.RegisterPermission("popupnotifications.send", this);

            panelColor = UI.Color(configData.Options.Color, configData.Options.Alpha);
            buttonColor = UI.Color(configData.Options.CloseColor, configData.Options.CloseAlpha);
            font = configData.Options.Font;
            fontSize = configData.Options.FontSize;

            ins = this;            
        }

        private void Unload()
        {
            foreach(BasePlayer player in BasePlayer.activePlayerList)
            {
                Notifier notifier = player.GetComponent<Notifier>();
                if (notifier != null)
                    UnityEngine.Object.Destroy(notifier);
            }

            ins = null;
        }
        #endregion

        #region Functions
        private Notifier GetPlayerNotifier(BasePlayer player) => player.GetComponent<Notifier>() ?? player.gameObject.AddComponent<Notifier>();

        private void CreatePopupOnPlayer(string message, BasePlayer player, float duration = 0f) => GetPlayerNotifier(player).PopupMessage(message, duration);        

        private void CreateGlobalPopup(string message, float duration = 0f)
        {
            foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
            {
                CreatePopupOnPlayer(message, activePlayer, duration);
            }
        }
        #endregion     

        #region API
        [HookMethod("CreatePopupNotification")]
        private void CreatePopupNotification(string message, BasePlayer player = null, float duration = 0f)
		{
			if(player != null)			
				CreatePopupOnPlayer(message, player, (float)duration);			
			else CreateGlobalPopup(message, (float)duration);			
		}
        #endregion

        #region Component
        private class Notifier : MonoBehaviour
		{
            private BasePlayer player;

            private List<string> openPanels = new List<string>();
            private List<MessageData> messageQueue = new List<MessageData>();

            private Vector2 initialPos;
            private Vector2 dimensions;

            private int lastElementId;
            private int activeElements;
            private int maxElements;

            private void Awake()
			{
				player = GetComponent<BasePlayer>();
                enabled = false;

                initialPos = new Vector2(ins.configData.Position.PositionX, ins.configData.Position.PositionY);
                dimensions = new Vector2(ins.configData.Position.Width, ins.configData.Position.Height);
                maxElements = ins.configData.MaximumMessages;
            }		

            private void OnDestroy()
            {
                messageQueue.Clear();

                foreach (string panel in openPanels)
                    CuiHelper.DestroyUi(player, panel);
            }

            public void PopupMessage(string message, float duration)
            {
                messageQueue.Add(new MessageData(message, popupPanel + lastElementId, duration == 0f ? duration = ins.configData.Duration : duration));
                lastElementId++;
                if (activeElements < maxElements)
                    UpdateMessages();
            }

            private void UpdateMessages()
            {
                if (activeElements < maxElements)
                {
                    ClearAllElements();
                    for (int i = 0; i < maxElements; i++)
                    {
                        if (messageQueue.Count - 1 < i)
                            break;

                        MessageData messageData = messageQueue.ElementAt(i);

                        CuiElementContainer container = CreateNotification(messageData, i);

                        AddUi(container, messageData.elementId);

                        if (!messageData.started)
                        {
                            messageData.started = true;
                            StartCoroutine(DestroyNotification(messageData));
                        }
                    }
                }
            }

            private IEnumerator DestroyNotification(MessageData messageData)
            {
                yield return new WaitForSeconds(messageData.duration);

                if (messageData == null)
                    yield break;

                messageQueue.Remove(messageData);
                DestroyUi(messageData.elementId);

                if (messageQueue.Count > 0)
                    UpdateMessages();
            }

            public void DestroyNotification(string elementId)
            {
                MessageData messageData = messageQueue.Find(x => x.elementId == elementId) ?? null;
                if (messageData == null)
                    return;

                messageQueue.Remove(messageData);
                DestroyUi(messageData.elementId);

                if (messageQueue.Count > 0)
                    UpdateMessages();
            }

            private void AddUi(CuiElementContainer container, string elementId)
            {
                openPanels.Add(elementId);
                CuiHelper.AddUi(player, container);
                activeElements++;
            }

            private void DestroyUi(string elementId)
            {
                openPanels.Remove(elementId);
                CuiHelper.DestroyUi(player, elementId);
                activeElements--;
            }

            private void ClearAllElements()
            {
                foreach (string elementId in openPanels)
                    CuiHelper.DestroyUi(player, elementId);
                openPanels.Clear();
                activeElements = 0;
            }

            private float[] GetFeedPosition(int number)
            {               
                float yPos = initialPos.y - ((dimensions.y + ins.configData.Position.Spacing) * number);
                return new float[] { initialPos.x, yPos, initialPos.x + dimensions.x, yPos + dimensions.y };
            }

            private CuiElementContainer CreateNotification(MessageData messageData, int number)
            {
                float[] position = GetFeedPosition(number);

                CuiElementContainer container = UI.Container(messageData.elementId, $"{position[0]} {position[1]}", $"{position[2]} {position[3]}");
                UI.Label(ref container, messageData.elementId, messageData.message);

                if (ins.configData.Options.Close)
                    UI.Button(ref container, messageData.elementId, "X", $"popupmsg.close {messageData.elementId}");

                return container;
            }

            public class MessageData
            {
                public string message { get; private set; }
                public string elementId { get; private set; }
                public float duration { get; private set; }
                public bool started { get; set; }

                public MessageData(string message, string elementId, float duration)
                {
                    this.message = message;
                    this.elementId = elementId;
                    this.duration = duration;
                    started = false;                    
                }
            }
        }
        #endregion

        #region UI
        static public class UI
        {
            static public CuiElementContainer Container(string panel, string aMin, string aMax)
            {
                CuiElementContainer container = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = { Color = ins.panelColor },
                            RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                            CursorEnabled = false
                        },
                        new CuiElement().Parent = "Overlay",
                        panel
                    }
                };
                return container;
            }
            
            static public void Label(ref CuiElementContainer container, string panel, string text)
            {
                container.Add(new CuiLabel
                {
                    Text = { FontSize = ins.fontSize, Align = TextAnchor.MiddleCenter, Text = text, Font = ins.font },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
                },
                panel);
            }

            static public void Button(ref CuiElementContainer container, string panel, string text, string command)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = ins.buttonColor, Command = command },
                    RectTransform = { AnchorMin = "0.89 0.79", AnchorMax = "0.99 0.99" },
                    Text = { Text = text, FontSize = 10, Align = TextAnchor.MiddleCenter, Font = ins.font }
                },
                panel);
            }
           
            static public string Color(string hexColor, float alpha)
            {
                if (hexColor.StartsWith("#"))
                    hexColor = hexColor.Substring(1);
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }
        #endregion

        #region Commands
        [ChatCommand("popupmsg")]
        private void cmdSendPopupMessage(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin || permission.UserHasPermission(player.UserIDString, "popupnotifications.send"))
            {
                if (args.Length == 1)
                {
                    CreateGlobalPopup(args[0]);
                }
                else if (args.Length == 2)
                {
                    var target = GetPlayerByName(args[0]);
                    if (target is string)
                    {
                        SendReply(player, (string)target);
                        return;
                    }
                    if (target as BasePlayer != null)
                        CreatePopupOnPlayer(args[1], target as BasePlayer);
                }
                else
                    SendReply(player, msg("Usage: /popupmsg \"Your message here.\" OR /popupmsg \"player name\" \"You message here.\"", player.UserIDString));
            }
        }

        [ConsoleCommand("popupmsg.global")]
        private void ccmdPopupMessageGlobal(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
                return;

            if (arg.Args == null || arg.Args.Length == 0)
            {
                Puts(msg("Usage: popupmsg.global \"Your message here.\" <duration>"));
                return;
            }

            if (arg.Args.Length == 1)
                CreateGlobalPopup(arg.Args[0]);
            
            else if (arg.Args.Length == 2)
            {
                float duration;
                if (float.TryParse(arg.Args[1], out duration))
                    CreateGlobalPopup(arg.Args[0], duration);
                else
                    Puts(msg("Invalid duration"));
            }           
        }

        [ConsoleCommand("popupmsg.toplayer")]
        private void ccmdPopupMessageToPlayer(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin || arg.Connection != null)
                return;

            if (arg.Args == null || arg.Args.Length == 0)
            {
                Puts(msg("Usage: popupmsg.toplayer \"Your message here.\" \"Player name\" <duration>"));
                return;
            }

            if (arg.Args.Length >= 1)
            {
                var player = GetPlayerByName(arg.Args[1]);
                if (player is string)
                {
                    SendReply(arg, (string)player);
                    return;
                }

                if (arg.Args.Length == 2)
                {
                    if (player as BasePlayer != null && (player as BasePlayer).IsConnected)
                        CreatePopupOnPlayer(arg.Args[0], player as BasePlayer);
                    else Puts(msg("Couldn't send popup notification to player"));
                }
                else if (arg.Args.Length == 3)
                {

                    if (player as BasePlayer != null && (player as BasePlayer).IsConnected)
                    {
                        float duration;
                        if (float.TryParse(arg.Args[2], out duration))
                            CreatePopupOnPlayer(arg.Args[0], player as BasePlayer, duration);
                        else Puts(msg("Invalid duration"));
                    }
                    else Puts(msg("Couldn't send popup notification to player"));
                }                
            }
        }

        private object GetPlayerByName(string name)
        {
            var players = covalence.Players.FindPlayers(name);
            if (players != null)
            {
                if (players.ToList().Count == 0)
                    return msg("No players found with that name");
                else if (players.ToList().Count > 1)
                    return msg("Multiple players found with that name");
                else if (players.ToArray()[0].Object is BasePlayer)
                {
                    if (!(players.ToArray()[0].Object as BasePlayer).IsConnected)
                        return string.Format(msg("{0} is not online"), players.ToArray()[0].Name);
                    return players.ToArray()[0].Object as BasePlayer;
                }
            }
            return msg("Unable to find a valid player");
        }

        [ConsoleCommand("popupmsg.close")]
        private void ccmdCloseCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
                return;

            if (arg.Args.Length == 1)
            {
                string elementId = arg.Args[0];

                GetPlayerNotifier(player).DestroyNotification(elementId);
            }
        }
        #endregion

        #region Config        
        private ConfigData configData;
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Notification duration (in seconds)")]
            public int Duration { get; set; }
            [JsonProperty(PropertyName = "Maximum notifications shown at any time")]
            public int MaximumMessages { get; set; }            
            [JsonProperty(PropertyName = "UI Positioning")]
            public UIPosition Position { get; set; }
            [JsonProperty(PropertyName = "UI Options")]
            public UIOptions Options { get; set; }

            public class UIPosition
            {
                [JsonProperty(PropertyName = "Position of the left side of notification (0.0 - 1.0)")]
                public float PositionX { get; set; }
                [JsonProperty(PropertyName = "Position of the bottom of noticiation (0.0 - 1.0)")]
                public float PositionY { get; set; }
                [JsonProperty(PropertyName = "Width (0.0 - 1.0)")]
                public float Width { get; set; }
                [JsonProperty(PropertyName = "Height (0.0 - 1.0)")]
                public float Height { get; set; }
                [JsonProperty(PropertyName = "Space between notification (0.0 - 1.0)")]
                public float Spacing { get; set; }
            }
            public class UIOptions
            {
                [JsonProperty(PropertyName = "Show close button")]
                public bool Close { get; set; }
                [JsonProperty(PropertyName = "Panel color (hex)")]
                public string Color { get; set; }
                [JsonProperty(PropertyName = "Panel transparency (0.0 - 1.0)")]
                public float Alpha { get; set; }
                [JsonProperty(PropertyName = "Close button color (hex)")]
                public string CloseColor { get; set; }
                [JsonProperty(PropertyName = "Close button transparency (0.0 - 1.0)")]
                public float CloseAlpha { get; set; }                
                [JsonProperty(PropertyName = "Font")]
                public string Font { get; set; }
                [JsonProperty(PropertyName = "Font size")]
                public int FontSize { get; set; }
            }
            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {                
                Duration = 8,               
                MaximumMessages = 6,               
                Options = new ConfigData.UIOptions
                {
                    Alpha = 0.5f,
                    CloseAlpha = 0.5f,
                    Close = true,
                    CloseColor = "#d85540",
                    Color = "#2b2b2b",
                    Font = "droidsansmono.ttf",
                    FontSize = 12
                },
                Position = new ConfigData.UIPosition
                {
                    Height = 0.1f,
                    PositionX = 0.8f,
                    PositionY = 0.78f,
                    Spacing = 0.01f,
                    Width = 0.19f
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();
            if (configData.Version < new Core.VersionNumber(0, 2, 0))
                configData = baseConfig;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion        

        #region Localization
        private string msg(string key, string id = null) => lang.GetMessage(key, this, id);

        Dictionary<string, string> Messages = new Dictionary<string, string>
            {
            {"Usage: /popupmsg \"Your message here.\" OR /popupmsg \"player name\" \"You message here.\"","Usage: /popupmsg \"Your message here.\" OR /popupmsg \"player name\" \"You message here.\"" },
            {"Invalid duration","Invalid duration" },
            {"Usage: popupmsg.global \"Your message here.\" duration","Usage: popupmsg.global \"Your message here.\" duration" },
            {"Couldn't send popup notification to player","Couldn't send popup notification to player" },
            {"Usage: popupmsg.toplayer \"Your message here.\" \"Player name\" <duration>","Usage: popupmsg.toplayer \"Your message here.\" \"Player name\" <duration>" },
            {"No players found with that name","No players found with that name" },
            {"Multiple players found with that name","Multiple players found with that name" },
            {"{0} is not online","{0} is not online" },
            {"Unable to find a valid player","Unable to find a valid player" }
            };
        #endregion
    }
}